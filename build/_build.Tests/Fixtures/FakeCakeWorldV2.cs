using System.Globalization;
using System.Text.Json;
using Build.Host;
using Build.Host.Configuration;
using Build.Host.Paths;
using Build.Shared.Manifest;
using Build.Shared.Runtime;
using Build.Shared.Strategy;
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

public sealed record ProcessInvocation(
    FilePath Command,
    string Arguments,
    bool RedirectStandardOutput,
    bool Silent);

public sealed class FakeCakeWorldV2
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly FakeFileSystem _fileSystem;
    private readonly FakeEnvironment _environment;
    private readonly DirectoryPath _repoRoot;
    private readonly Dictionary<string, (int ExitCode, string StdOut, string StdErr)> _processResults = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<ProcessInvocation> _processInvocations = [];
    private FilePath? _toolPath;
    private FilePath? _defaultToolPath;
    private (int ExitCode, string StdOut, string StdErr)? _defaultProcessResult;
    private string _rid = "win-x64";
    private string _config = "Release";

    public FakeFileSystem FileSystem => _fileSystem;
    public FakeEnvironment Environment => _environment;
    public TestLogV2 Log { get; }
    public ICakeContext CakeContext { get; }
    public DirectoryPath RepoRoot => _repoRoot;

    public IReadOnlyList<ProcessInvocation> ProcessInvocations => _processInvocations;

    public TestConsole AnsiConsole { get; } = new();

    private FakeCakeWorldV2(FakeRepoPlatformV2 platform, string? repoRoot)
    {
        _environment = platform switch
        {
            FakeRepoPlatformV2.Windows => FakeEnvironment.CreateWindowsEnvironment(),
            FakeRepoPlatformV2.Unix => FakeEnvironment.CreateUnixEnvironment(),
            _ => throw new ArgumentOutOfRangeException(nameof(platform)),
        };

        _fileSystem = new FakeFileSystem(_environment);
        _repoRoot = new DirectoryPath(repoRoot ?? (platform == FakeRepoPlatformV2.Windows ? "C:/repo" : "/repo"));
        Log = new TestLogV2();
        CakeContext = BuildCakeContext();
    }

    public static FakeCakeWorldV2 Create(
        FakeRepoPlatformV2 platform = FakeRepoPlatformV2.Windows,
        string? repoRoot = null)
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
        return world;
    }

    public static FakeCakeWorldV2 CreateOsx(string? repoRoot = null)
    {
        var world = new FakeCakeWorldV2(FakeRepoPlatformV2.Unix, repoRoot);
        var manifestContent = FixtureLoader.Load("Manifest/manifest-osx-x64.json");
        world.WithManifestFile(manifestContent);
        world.WithDefaultProcessResult(exitCode: 0, stdOut: "", stdErr: "");
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
            ? _repoRoot.CombineWithFilePath(path)
            : path;

        var dir = _fileSystem.GetDirectory(fullPath.GetDirectory());
        if (!dir.Exists)
        {
            dir.Create();
        }

        var file = _fileSystem.GetFile(fullPath);
        using var stream = file.Open(FileMode.Create, FileAccess.Write, FileShare.None);
        using var writer = new StreamWriter(stream);
        writer.Write(content);
        return this;
    }

    public FakeCakeWorldV2 WithManifestFile(string json)
    {
        return WithTextFile("build/manifest.json", json);
    }

    public FakeCakeWorldV2 WithManifestObject(ManifestConfig manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        return WithManifestFile(JsonSerializer.Serialize(manifest, JsonOptions));
    }

    public FakeCakeWorldV2 WithProcessResult(
        string command,
        int exitCode,
        string stdOut,
        string stdErr = "")
    {
        _processResults[command] = (exitCode, stdOut, stdErr);
        return this;
    }

    public FakeCakeWorldV2 WithDefaultProcessResult(
        int exitCode = 0,
        string stdOut = "",
        string stdErr = "")
    {
        _defaultProcessResult = (exitCode, stdOut, stdErr);
        return this;
    }

    public FakeCakeWorldV2 WithToolPath(FilePath toolPath)
    {
        _toolPath = toolPath;
        if (!_fileSystem.GetFile(toolPath).Exists)
        {
            _fileSystem.CreateFile(toolPath);
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

    // ── access helpers ──

    public string ReadAllText(string relativePath)
    {
        var path = _repoRoot.CombineWithFilePath(relativePath);
        using var stream = _fileSystem.GetFile(path).OpenRead();
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    public bool FileExists(string relativePath)
    {
        var path = _repoRoot.CombineWithFilePath(relativePath);
        return _fileSystem.GetFile(path).Exists;
    }

    // ── compatibility shim for unmigrated tasks that still require the legacy BuildContext shape ──

    public BuildContext ToLegacyBuildContext(ManifestConfig? manifest = null)
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
            Library: [],
            Rid: _rid,
            Dll: [],
            Suffix: null,
            Scope: [],
            ExplicitVersion: [],
            ExplicitVersions: null,
            VersionsFile: null);

        var pathService = new PathService(
            new RepositoryConfiguration(_repoRoot),
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

        var options = new Configurations(
            Vcpkg: new VcpkgConfiguration([], _rid),
            Package: new PackageBuildConfiguration(
                new Dictionary<string, NuGet.Versioning.NuGetVersion>(StringComparer.OrdinalIgnoreCase)),
            Repository: new RepositoryConfiguration(_repoRoot),
            DotNet: new DotNetBuildConfiguration(_config),
            Dumpbin: new DumpbinConfiguration([]));

        return new BuildContext(
            CakeContext,
            pathService,
            runtimeProfile,
            resolvedManifest,
            parsedArgs,
            options);
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
            .Returns(_ =>
            {
                if (_toolPath is not null) return _toolPath;
                if (_defaultToolPath is not null) return _defaultToolPath;
                throw new InvalidOperationException(
                    "Tool path was not configured. " +
                    "Call WithToolPath(path) for a specific tool, or WithDefaultToolPath(path) to accept any unconfigured tool.");
            });
        toolLocator.Resolve(Arg.Any<IEnumerable<string>>())
            .Returns(_ =>
            {
                if (_toolPath is not null) return _toolPath;
                if (_defaultToolPath is not null) return _defaultToolPath;
                throw new InvalidOperationException(
                    "Tool path was not configured. " +
                    "Call WithToolPath(path) for a specific tool, or WithDefaultToolPath(path) to accept any unconfigured tool.");
            });

        var globber = new Globber(_fileSystem, _environment);

        var context = Substitute.For<ICakeContext>();
        context.Log.Returns(Log);
        context.Environment.Returns(_environment);
        context.FileSystem.Returns(_fileSystem);
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

        var stdOutLines = stdOut
            .Split('\n', StringSplitOptions.RemoveEmptyEntries);
        if (stdOutLines.Length > 0)
        {
            process.SetStandardOutput(stdOutLines);
        }

        if (!string.IsNullOrWhiteSpace(stdErr))
        {
            var stdErrLines = stdErr
                .Split('\n', StringSplitOptions.RemoveEmptyEntries);
            process.SetStandardError(stdErrLines);
        }

        return process;
    }
}
