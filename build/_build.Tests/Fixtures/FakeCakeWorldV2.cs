using System.Text.Json;
using Build.Host;
using Build.Host.Cake;
using Build.Host.Paths;
using Build.Manifest;
using Build.Runtime;
using Cake.Core;
using Cake.Core.Configuration;
using Cake.Core.IO;
using Cake.Core.Tooling;
using Cake.Testing;
using NSubstitute;
using Spectre.Console.Testing;

namespace Build.Tests.Fixtures;

public enum FakeRepoPlatformV2
{
    Windows,
    Unix,
}

public sealed record ProcessInvocation(FilePath Command, string Arguments, bool RedirectStandardOutput, bool Silent);

public sealed class FakeCakeWorldV2
{
    private readonly Dictionary<string, (int ExitCode, string StdOut, string StdErr)> _processResults = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<(string Command, string Arguments), (int ExitCode, string StdOut, string StdErr)> _processResultsByArguments = new(ProcessResultArgumentsComparer.Instance);
    private readonly Dictionary<string, Action<FakeCakeWorldV2>> _processSideEffects = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<ProcessInvocation> _processInvocations = [];
    private FilePath? _toolPath;
    private FilePath? _defaultToolPath;
    private readonly Dictionary<string, FilePath> _toolPathsByName = new(StringComparer.OrdinalIgnoreCase);
    private (int ExitCode, string StdOut, string StdErr)? _defaultProcessResult;
    private string _rid = "win-x64";
    private string _config = "Release";
    private string? _suffix;
    private readonly List<string> _scope = [];
    private readonly List<string> _explicitVersion = [];
    private string? _explicitVersions;
    private string? _versionsFile;
    private Build.Versioning.PackageFamilyVersionSet _familyVersions = Build.Versioning.PackageFamilyVersionSet.Empty;
    private readonly List<string> _dlls = [];
    private readonly List<string> _libraries = [];

    public FakeFileSystem FileSystem { get; }

    public FakeEnvironment Environment { get; }

    public TestLogV2 Log { get; }
    public ICakeContext CakeContext { get; }
    public DirectoryPath RepoRoot { get; }

    /// <summary>Active RID configured via <see cref="WithRid"/>; defaults to <c>win-x64</c>.</summary>
    public string Rid => _rid;

    /// <summary>Family versions configured via <see cref="WithFamilyVersions"/>; defaults to empty.</summary>
    public Build.Versioning.PackageFamilyVersionSet FamilyVersions => _familyVersions;

    public IReadOnlyList<ProcessInvocation> ProcessInvocations => _processInvocations;

    public TestConsole AnsiConsole { get; } = new();

    private FakeCakeWorldV2(FakeRepoPlatformV2 platform, string? repoRoot)
    {
        Environment = platform switch
        {
            FakeRepoPlatformV2.Windows => FakeEnvironment.CreateWindowsEnvironment(),
            FakeRepoPlatformV2.Unix => FakeEnvironment.CreateUnixEnvironment(),
            _ => throw new ArgumentOutOfRangeException(nameof(platform)),
        };

        RepoRoot = new DirectoryPath(repoRoot ?? (platform == FakeRepoPlatformV2.Windows ? "C:/repo" : "/repo"));
        Environment.WorkingDirectory = RepoRoot;
        FileSystem = new FakeFileSystem(Environment);
        Log = new TestLogV2();
        CakeContext = BuildCakeContext();
    }

    public static FakeCakeWorldV2 Create(FakeRepoPlatformV2 platform = FakeRepoPlatformV2.Windows, string? repoRoot = null)
    {
        return new FakeCakeWorldV2(platform, repoRoot);
    }

    public static FakeCakeWorldV2 CreateWindows(string? repoRoot = null)
    {
        var world = new FakeCakeWorldV2(FakeRepoPlatformV2.Windows, repoRoot);
        var manifestContent = FixtureLoader.Load("Manifest/manifest-win-x64.json");
        world.WithManifestFile(manifestContent);
        world.WithDefaultProcessResult(exitCode: 0, stdOut: "", stdErr: "");
        return world;
    }

    public static FakeCakeWorldV2 CreateLinux(string? repoRoot = null)
    {
        var world = new FakeCakeWorldV2(FakeRepoPlatformV2.Unix, repoRoot);
        var manifestContent = FixtureLoader.Load("Manifest/manifest-linux-x64.json");
        world.WithManifestFile(manifestContent);
        world.WithDefaultProcessResult(exitCode: 0, stdOut: "", stdErr: "");
        world.WithRid("linux-x64");
        return world;
    }

    public static FakeCakeWorldV2 CreateOsx(string? repoRoot = null)
    {
        var world = new FakeCakeWorldV2(FakeRepoPlatformV2.Unix, repoRoot);
        var manifestContent = FixtureLoader.Load("Manifest/manifest-osx-x64.json");
        world.WithManifestFile(manifestContent);
        world.WithDefaultProcessResult(exitCode: 0, stdOut: "", stdErr: "");
        world.WithRid("osx-x64");
        return world;
    }

    // ── fluent seeders ──

    public FakeCakeWorldV2 WithTextFile(string relativePath, string content)
    {
        return WithTextFile(new FilePath(relativePath), content);
    }

    public FakeCakeWorldV2 WithTextFile(FilePath path, string content)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(content);

        var fullPath = path.IsRelative
            ? RepoRoot.CombineWithFilePath(path)
            : path;

        var dir = FileSystem.GetDirectory(fullPath.GetDirectory());
        if (!dir.Exists)
        {
            dir.Create();
        }

        var file = FileSystem.GetFile(fullPath);
        using var stream = file.Open(FileMode.Create, FileAccess.Write, FileShare.None);
        using var writer = new StreamWriter(stream);
        writer.Write(content);
        return this;
    }

    public FakeCakeWorldV2 WithBinaryFile(string relativePath, byte[] content)
    {
        return WithBinaryFile(new FilePath(relativePath), content);
    }

    public FakeCakeWorldV2 WithBinaryFile(FilePath path, byte[] content)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(content);

        var fullPath = path.IsRelative
            ? RepoRoot.CombineWithFilePath(path)
            : path;

        var dir = FileSystem.GetDirectory(fullPath.GetDirectory());
        if (!dir.Exists)
        {
            dir.Create();
        }

        var file = FileSystem.GetFile(fullPath);
        using var stream = file.Open(FileMode.Create, FileAccess.Write, FileShare.None);
        stream.Write(content, 0, content.Length);
        return this;
    }

    public FakeCakeWorldV2 WithManifestFile(string json)
    {
        return WithTextFile("build/manifest.json", json);
    }

    public FakeCakeWorldV2 WithManifestObject(ManifestConfig manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        return WithManifestFile(JsonSerializer.Serialize(manifest, CakeJsonExtensions.DefaultJsonOptions));
    }

    public FakeCakeWorldV2 WithProcessResult(string command, int exitCode, string stdOut, string stdErr = "")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(command);
        _processResults[command] = (exitCode, stdOut, stdErr);
        return this;
    }

    public FakeCakeWorldV2 WithProcessResult(string command, string arguments, int exitCode, string stdOut, string stdErr = "")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(command);
        ArgumentNullException.ThrowIfNull(arguments);
        _processResultsByArguments[(command, arguments)] = (exitCode, stdOut, stdErr);
        return this;
    }

    /// <summary>
    /// Registers a callback that fires when a matching process is invoked, allowing the test to mutate the
    /// fake world (typically seeding files into the fake filesystem to simulate the side effects of a real tool
    /// — e.g. tar extraction creating files in the destination directory).
    /// </summary>
    public FakeCakeWorldV2 WithProcessSideEffect(string command, Action<FakeCakeWorldV2> sideEffect)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(command);
        ArgumentNullException.ThrowIfNull(sideEffect);
        _processSideEffects[command] = sideEffect;
        return this;
    }

    public FakeCakeWorldV2 WithDefaultProcessResult(int exitCode = 0, string stdOut = "", string stdErr = "")
    {
        _defaultProcessResult = (exitCode, stdOut, stdErr);
        return this;
    }

    public FakeCakeWorldV2 WithToolPath(FilePath toolPath)
    {
        _toolPath = toolPath;
        if (!FileSystem.GetFile(toolPath).Exists)
        {
            FileSystem.CreateFile(toolPath);
        }

        return this;
    }

    /// <summary>
    /// Per-tool path override keyed by IToolLocator query (tool name like "tar" or "ldd"). Use when a
    /// scenario invokes multiple Tool&lt;T&gt; wrappers and each must resolve to a distinct executable
    /// (so process invocations get distinct filename keys for WithProcessResult / WithProcessSideEffect).
    /// </summary>
    public FakeCakeWorldV2 WithToolPath(string toolName, FilePath toolPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(toolName);
        ArgumentNullException.ThrowIfNull(toolPath);
        _toolPathsByName[toolName] = toolPath;
        if (!FileSystem.GetFile(toolPath).Exists)
        {
            FileSystem.CreateFile(toolPath);
        }

        return this;
    }

    public FakeCakeWorldV2 WithDefaultToolPath(FilePath toolPath)
    {
        _defaultToolPath = toolPath;
        return this;
    }

    public FakeCakeWorldV2 WithRid(string rid)
    {
        _rid = rid;
        return this;
    }

    public FakeCakeWorldV2 WithConfig(string config)
    {
        _config = config;
        return this;
    }

    public FakeCakeWorldV2 WithSuffix(string? suffix)
    {
        _suffix = suffix;
        return this;
    }

    public FakeCakeWorldV2 WithScope(params string[] scope)
    {
        _scope.Clear();
        _scope.AddRange(scope);
        return this;
    }

    public FakeCakeWorldV2 WithExplicitVersion(params string[] entries)
    {
        _explicitVersion.Clear();
        _explicitVersion.AddRange(entries);
        return this;
    }

    public FakeCakeWorldV2 WithExplicitVersions(string? entries)
    {
        _explicitVersions = entries;
        return this;
    }

    public FakeCakeWorldV2 WithVersionsFile(string? versionsFile)
    {
        _versionsFile = versionsFile is null ? null : RepoRoot.CombineWithFilePath(versionsFile).FullPath;
        return this;
    }

    /// <summary>
    /// Sets the resolved <see cref="Build.Versioning.PackageFamilyVersionSet"/> stamped into
    /// <c>BuildContext.Options.Package.FamilyVersions</c>. <see cref="Build.Targets.Package.PackageTask"/>
    /// reads its scope from that set, so scenarios that exercise Pack must seed it explicitly.
    /// </summary>
    public FakeCakeWorldV2 WithFamilyVersions(Build.Versioning.PackageFamilyVersionSet familyVersions)
    {
        _familyVersions = familyVersions ?? throw new ArgumentNullException(nameof(familyVersions));
        return this;
    }

    public FakeCakeWorldV2 WithDll(string dll)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dll);
        _dlls.Add(dll);
        return this;
    }

    public FakeCakeWorldV2 WithDlls(params string[] dlls)
    {
        ArgumentNullException.ThrowIfNull(dlls);
        _dlls.Clear();
        _dlls.AddRange(dlls);
        return this;
    }

    public FakeCakeWorldV2 WithLibraries(params string[] libraries)
    {
        ArgumentNullException.ThrowIfNull(libraries);
        _libraries.Clear();
        _libraries.AddRange(libraries);
        return this;
    }

    // ── access helpers ──

    public string ReadAllText(string relativePath)
    {
        var path = RepoRoot.CombineWithFilePath(relativePath);
        using var stream = FileSystem.GetFile(path).OpenRead();
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    public bool FileExists(string relativePath)
    {
        var path = RepoRoot.CombineWithFilePath(relativePath);
        return FileSystem.GetFile(path).Exists;
    }

    public BuildContext CreateBuildContext(ManifestConfig? manifest = null)
    {
        var resolvedManifest = manifest ?? new ManifestConfig
        {
            SchemaVersion = "2.1",
            Runtimes = [],
            PackageFamilies = [],
            SystemExclusions = new SystemArtefactsConfig
            {
                Windows = new WindowsSystemArtefacts(),
                Linux = new LinuxSystemArtefacts(),
                Osx = new OsxSystemArtefacts(),
            },
            LibraryManifests = System.Collections.Immutable.ImmutableList<LibraryManifest>.Empty,
            PackagingConfig = new PackagingConfig
            {
                ValidationMode = ValidationMode.Strict,
                CoreLibrary = "sdl2",
            },
        };

        var parsedArgs = new ParsedArguments(
            RepoRoot: null,
            Config: _config,
            VcpkgDir: null,
            VcpkgInstalledDir: null,
            Library: [.. _libraries],
            Rid: _rid,
            Dll: [.. _dlls],
            Suffix: _suffix,
            Scope: [.. _scope],
            ExplicitVersion: [.. _explicitVersion],
            ExplicitVersions: _explicitVersions,
            VersionsFile: _versionsFile);

        var pathService = new PathService(
            RepoRoot,
            parsedArgs,
            Log);

        var runtimeProfile = Substitute.For<IRuntimeProfile>();
        string triplet;
        RuntimeFamily family;
        if (_rid.StartsWith("win", StringComparison.OrdinalIgnoreCase))
        {
            triplet = "x64-windows-hybrid";
            family = RuntimeFamily.Windows;
        }
        else if (_rid.StartsWith("linux", StringComparison.OrdinalIgnoreCase))
        {
            triplet = "x64-linux-hybrid";
            family = RuntimeFamily.Linux;
        }
        else
        {
            triplet = "x64-osx-hybrid";
            family = RuntimeFamily.OSX;
        }

        runtimeProfile.Rid.Returns(_rid);
        runtimeProfile.Triplet.Returns(triplet);
        runtimeProfile.Family.Returns(family);

        return new BuildContext(
            CakeContext,
            pathService,
            runtimeProfile,
            resolvedManifest,
            parsedArgs);
    }

    // ── internal: build faked ICakeContext ──

    private ICakeContext BuildCakeContext()
    {
        var processRunner = Substitute.For<IProcessRunner>();
        processRunner.Start(Arg.Any<FilePath>(), Arg.Any<ProcessSettings>())
            .Returns(call =>
            {
                var filePath = (FilePath)call[0];
                var settings = (ProcessSettings)call[1];
                var command = filePath.GetFilename().FullPath;

                var arguments = settings.Arguments?.Render() ?? "";
                _processInvocations.Add(new ProcessInvocation(
                    filePath,
                    arguments,
                    settings.RedirectStandardOutput,
                    settings.Silent));

                if (_processSideEffects.TryGetValue(command, out var sideEffect))
                {
                    sideEffect(this);
                }

                if (_processResultsByArguments.TryGetValue((command, arguments), out var exactResult))
                {
                    return CreateFakeProcess(exactResult.ExitCode, exactResult.StdOut, exactResult.StdErr);
                }

                if (_processResults.TryGetValue(command, out var result))
                {
                    return CreateFakeProcess(result.ExitCode, result.StdOut, result.StdErr);
                }

                if (_defaultProcessResult is { } def)
                {
                    return CreateFakeProcess(def.ExitCode, def.StdOut, def.StdErr);
                }

                var renderedArgs = string.IsNullOrWhiteSpace(arguments) ? "" : $" {arguments}";
                throw new InvalidOperationException(
                    $"Process '{command}{renderedArgs}' was not configured. " +
                    $"Call WithProcessResult(\"{command}\", exitCode: 0, stdOut: \"...\") to seed the result, " +
                    "or WithDefaultProcessResult(...) to accept any unconfigured command.");
            });

        var toolLocator = Substitute.For<IToolLocator>();

        // Lazy resolution: _toolPath / _defaultToolPath may be configured after construction.
        toolLocator.Resolve(Arg.Any<string>())
            .Returns(call =>
            {
                var toolName = (string)call[0];
                if (_toolPathsByName.TryGetValue(toolName, out var perTool)) return perTool;
                if (_toolPath is not null) return _toolPath;
                if (_defaultToolPath is not null) return _defaultToolPath;
                throw new InvalidOperationException(
                    "Tool path was not configured. " +
                    "Call WithToolPath(name, path) for per-tool, WithToolPath(path) for the legacy global default, or WithDefaultToolPath(path) to accept any unconfigured tool.");
            });
        toolLocator.Resolve(Arg.Any<IEnumerable<string>>())
            .Returns(call =>
            {
                var names = (IEnumerable<string>)call[0];
                foreach (var n in names)
                {
                    if (_toolPathsByName.TryGetValue(n, out var perTool)) return perTool;
                }
                if (_toolPath is not null) return _toolPath;
                if (_defaultToolPath is not null) return _defaultToolPath;
                throw new InvalidOperationException(
                    "Tool path was not configured. " +
                    "Call WithToolPath(name, path) for per-tool, WithToolPath(path) for the legacy global default, or WithDefaultToolPath(path) to accept any unconfigured tool.");
            });

        var globber = new Globber(FileSystem, Environment);

        var context = Substitute.For<ICakeContext>();
        context.Log.Returns(Log);
        context.Environment.Returns(Environment);
        context.FileSystem.Returns(FileSystem);
        context.Globber.Returns(globber);
        context.Arguments.Returns(Substitute.For<ICakeArguments>());
        context.Configuration.Returns(Substitute.For<ICakeConfiguration>());
        context.Data.Returns(Substitute.For<ICakeDataResolver>());
        context.ProcessRunner.Returns(processRunner);
        context.Registry.Returns(Substitute.For<IRegistry>());
        context.Tools.Returns(toolLocator);

        return context;
    }

    private static FakeProcess CreateFakeProcess(int exitCode, string stdOut, string stdErr)
    {
        var process = new FakeProcess();
        process.SetExitCode(exitCode);

        var stdOutLines = stdOut.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        process.SetStandardOutput(stdOutLines);

        var stdErrLines = stdErr.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        process.SetStandardError(stdErrLines);

        return process;
    }

    private sealed class ProcessResultArgumentsComparer : IEqualityComparer<(string Command, string Arguments)>
    {
        public static readonly ProcessResultArgumentsComparer Instance = new();

        private ProcessResultArgumentsComparer()
        {
        }

        public bool Equals((string Command, string Arguments) x, (string Command, string Arguments) y)
        {
            return StringComparer.OrdinalIgnoreCase.Equals(x.Command, y.Command)
                && StringComparer.Ordinal.Equals(x.Arguments, y.Arguments);
        }

        public int GetHashCode((string Command, string Arguments) obj)
        {
            return HashCode.Combine(
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Command),
                StringComparer.Ordinal.GetHashCode(obj.Arguments));
        }
    }
}
