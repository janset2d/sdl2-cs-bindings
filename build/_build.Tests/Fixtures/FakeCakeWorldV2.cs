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

namespace Build.Tests.Fixtures;

public enum FakeRepoPlatformV2
{
    Windows,
    Unix,
}

public sealed class FakeCakeWorldV2
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly FakeFileSystem _fileSystem;
    private readonly FakeEnvironment _environment;
    private readonly DirectoryPath _repoRoot;
    private readonly Dictionary<string, (int ExitCode, string StdOut, string StdErr)> _processResults = new(StringComparer.OrdinalIgnoreCase);
    private FilePath? _toolPath;
    private string _rid = "win-x64";
    private string _config = "Release";

    public FakeFileSystem FileSystem => _fileSystem;
    public FakeEnvironment Environment => _environment;
    public TestLogV2 Log { get; }
    public ICakeContext CakeContext { get; }
    public DirectoryPath RepoRoot => _repoRoot;

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

    public FakeCakeWorldV2 WithToolPath(FilePath toolPath)
    {
        _toolPath = toolPath;
        if (!_fileSystem.GetFile(toolPath).Exists)
        {
            _fileSystem.CreateFile(toolPath);
        }
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

    // ── compatibility shim (retires P5 with Host/Configuration) ──

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
                var command = filePath.GetFilename().FullPath;

                if (_processResults.TryGetValue(command, out var result))
                {
                    var process = new FakeProcess();
                    process.SetExitCode(result.ExitCode);

                    var stdOutLines = result.StdOut
                        .Split('\n', StringSplitOptions.RemoveEmptyEntries);
                    process.SetStandardOutput(stdOutLines);

                    if (!string.IsNullOrWhiteSpace(result.StdErr))
                    {
                        var stdErrLines = result.StdErr
                            .Split('\n', StringSplitOptions.RemoveEmptyEntries);
                        process.SetStandardError(stdErrLines);
                    }

                    return process;
                }

                var defaultProcess = new FakeProcess();
                defaultProcess.SetExitCode(0);
                return defaultProcess;
            });

        var toolLocator = Substitute.For<IToolLocator>();
        var resolvedToolPath = _toolPath ?? new FilePath("/dev/null");
        toolLocator.Resolve(Arg.Any<string>()).Returns(resolvedToolPath);
        toolLocator.Resolve(Arg.Any<IEnumerable<string>>()).Returns(resolvedToolPath);

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
}
