using System.Text.Json;
using Build.Features.Coverage;
using Build.Features.Harvesting;
using Build.Host;
using Build.Host.Configuration;
using Build.Host.Paths;
using Build.Shared.Manifest;
using Build.Shared.Runtime;
using Build.Tests.Fixtures.Seeders;
using Cake.Core;
using Cake.Core.Configuration;
using Cake.Core.IO;
using Cake.Core.Tooling;
using Cake.Testing;
using NSubstitute;


namespace Build.Tests.Fixtures;

public sealed class FakeRepoBuilder
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    private readonly FakeEnvironment _environment;
    private readonly FakeFileSystem _fileSystem;
    private readonly DirectoryPath _repoRoot;
    private readonly Dictionary<string, string> _arguments = new(StringComparer.OrdinalIgnoreCase);

    private IReadOnlyList<string> _libraries = [];
    private string? _rid;
    private string _config = "Release";

    public FakeRepoBuilder(FakeRepoPlatform platform = FakeRepoPlatform.Windows, string? repoRoot = null)
    {
        _environment = platform switch
        {
            FakeRepoPlatform.Windows => FakeEnvironment.CreateWindowsEnvironment(),
            FakeRepoPlatform.Unix => FakeEnvironment.CreateUnixEnvironment(),
            _ => throw new ArgumentOutOfRangeException(nameof(platform)),
        };

        _fileSystem = new FakeFileSystem(_environment);
        _repoRoot = new DirectoryPath(repoRoot ?? (platform == FakeRepoPlatform.Windows ? "C:/repo" : "/repo"));
    }

    public FakeRepoBuilder WithManifest(string json)
    {
        return WithTextFile("build/manifest.json", json);
    }

    public FakeRepoBuilder WithManifest(ManifestConfig manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        return WithManifest(JsonSerializer.Serialize(manifest, JsonOptions));
    }

    public FakeRepoBuilder WithVcpkgJson(string json)
    {
        return WithTextFile("vcpkg.json", json);
    }

    public FakeRepoBuilder WithVcpkgJson(VcpkgManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        return WithVcpkgJson(JsonSerializer.Serialize(manifest, JsonOptions));
    }

    public FakeRepoBuilder WithCoverageBaseline(string json)
    {
        return WithTextFile("build/coverage-baseline.json", json);
    }

    public FakeRepoBuilder WithCoverageBaseline(CoverageBaseline baseline)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        return WithCoverageBaseline(JsonSerializer.Serialize(baseline, JsonOptions));
    }

    public FakeRepoBuilder WithCoberturaReport(string xml, FilePath? relativePath = null)
    {
        return WithTextFile(relativePath ?? new FilePath("artifacts/test-results/build-tests/coverage.cobertura.xml"), xml);
    }

    public FakeRepoBuilder WithHarvestStatus(string libraryName, string rid, string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(libraryName);
        ArgumentException.ThrowIfNullOrWhiteSpace(rid);

        return WithTextFile($"artifacts/harvest_output/{libraryName}/rid-status/{rid}.json", json);
    }

    public FakeRepoBuilder WithHarvestStatus(string libraryName, string rid, RidHarvestStatus status)
    {
        ArgumentNullException.ThrowIfNull(status);
        return WithHarvestStatus(libraryName, rid, JsonSerializer.Serialize(status, JsonOptions));
    }

    public FakeRepoBuilder WithVcpkgInstalledLayout(string triplet, Action<VcpkgInstalledFake> configure)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(triplet);
        ArgumentNullException.ThrowIfNull(configure);

        configure(new VcpkgInstalledFake(this, triplet));
        return this;
    }

    public FakeRepoBuilder WithLibraries(params string[] libraries)
    {
        _libraries = libraries ?? throw new ArgumentNullException(nameof(libraries));
        return this;
    }

    public FakeRepoBuilder WithRid(string rid)
    {
        _rid = rid;
        return this;
    }

    public FakeRepoBuilder WithConfig(string config)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(config);
        _config = config;
        return this;
    }

    public FakeRepoBuilder WithArgument(string name, string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(value);

        _arguments[name] = value;
        return this;
    }

    public FakeRepoBuilder WithTextFile(string relativePath, string content)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        return WithTextFile(new FilePath(relativePath), content);
    }

    public FakeRepoBuilder WithTextFile(FilePath path, string content)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(content);

        WriteTextFile(ResolveFile(path), content);
        return this;
    }

    /// <summary>
    /// Applies a composable <see cref="IFixtureSeeder"/> onto this builder. Seeders are
    /// production-shaped fixture helpers living under <c>Fixtures/Seeders/</c>. Chain multiple
    /// seeders for multi-RID / multi-library fixtures without duplicating shape across tests.
    /// </summary>
    public FakeRepoBuilder Seed(IFixtureSeeder seeder)
    {
        ArgumentNullException.ThrowIfNull(seeder);
        seeder.Apply(this);
        return this;
    }

    public BuildContext BuildContext()
    {
        return BuildContextWithHandles().BuildContext;
    }

    public FakeRepoHandles BuildContextWithHandles()
    {
        var arguments = CreateArguments();
        var cakeContext = CreateCakeContext(arguments);
        var pathService = CreatePathService();
        var runtimeProfile = CreateRuntimeProfileStub();

        var manifest = ManifestConfigSeeder.FromDefaultFixture().Manifest;

        var options = new BuildOptions(
            Vcpkg: new VcpkgConfiguration(_libraries, _rid),
            Package: new PackageBuildConfiguration(new Dictionary<string, NuGet.Versioning.NuGetVersion>(StringComparer.OrdinalIgnoreCase)),
            Versioning: new VersioningConfiguration(null, null, []),
            Repository: new RepositoryConfiguration(_repoRoot),
            DotNet: new DotNetBuildConfiguration(_config),
            Dumpbin: new DumpbinConfiguration([]));

        var context = new BuildContext(
            cakeContext,
            pathService,
            runtimeProfile,
            manifest,
            options);

        return new FakeRepoHandles
        {
            BuildContext = context,
            CakeContext = cakeContext,
            Paths = pathService,
            FileSystem = _fileSystem,
            Environment = _environment,
        };
    }

    /// <summary>
    /// Minimal IRuntimeProfile stub covering the fields task classes read via
    /// <c>BuildContext.Runtime</c>. Defaults to the fixture's RID (if supplied) and a
    /// synthetic triplet. Tests that need richer profile behaviour still create their own
    /// NSubstitute-backed profile and inject it at the runner level.
    /// </summary>
    private IRuntimeProfile CreateRuntimeProfileStub()
    {
        var profile = Substitute.For<IRuntimeProfile>();
        profile.Rid.Returns(_rid ?? "win-x64");
        profile.Triplet.Returns("x64-windows-hybrid");
        profile.Family.Returns(RuntimeFamily.Windows);
        return profile;
    }

    private PathService CreatePathService()
    {
        var parsedArguments = new ParsedArguments(
            RepoRoot: null,
            Config: _config,
            VcpkgDir: null,
            VcpkgInstalledDir: null,
            Library: _libraries.ToList(),
            Source: "local",
            Rid: _rid ?? string.Empty,
            Dll: [],
            VersionSource: null,
            Suffix: null,
            Scope: [],
            ExplicitVersion: [],
            VersionsFile: null);

        return new PathService(new RepositoryConfiguration(_repoRoot), parsedArguments, new FakeLog());
    }

    private ICakeArguments CreateArguments()
    {
        var arguments = Substitute.For<ICakeArguments>();
        arguments.HasArgument(Arg.Any<string>())
            .Returns(callInfo => _arguments.ContainsKey((string)callInfo[0]));
        arguments.GetArguments(Arg.Any<string>())
            .Returns(callInfo =>
            {
                var name = (string)callInfo[0];
                return _arguments.TryGetValue(name, out var value)
                    ? [value]
                    : [];
            });

        return arguments;
    }

    private ICakeContext CreateCakeContext(ICakeArguments arguments)
    {
        var globber = new Globber(_fileSystem, _environment);

        var cakeContext = Substitute.For<ICakeContext>();
        cakeContext.Log.Returns(new FakeLog());
        cakeContext.Environment.Returns(_environment);
        cakeContext.FileSystem.Returns(_fileSystem);
        cakeContext.Globber.Returns(globber);
        cakeContext.Arguments.Returns(arguments);
        cakeContext.Configuration.Returns(Substitute.For<ICakeConfiguration>());
        cakeContext.Data.Returns(Substitute.For<ICakeDataResolver>());
        cakeContext.ProcessRunner.Returns(Substitute.For<IProcessRunner>());
        cakeContext.Registry.Returns(Substitute.For<IRegistry>());
        cakeContext.Tools.Returns(Substitute.For<IToolLocator>());

        return cakeContext;
    }

    private FilePath ResolveFile(FilePath path)
    {
        return path.IsRelative
            ? _repoRoot.CombineWithFilePath(path)
            : path;
    }

    private void WriteTextFile(FilePath path, string content)
    {
        var directory = _fileSystem.GetDirectory(path.GetDirectory());
        if (!directory.Exists)
        {
            directory.Create();
        }

        var file = _fileSystem.GetFile(path);
        using var stream = file.Open(FileMode.Create, FileAccess.Write, FileShare.None);
        using var writer = new StreamWriter(stream);
        writer.Write(content);
    }
}

public sealed class FakeRepoHandles
{
    public required BuildContext BuildContext { get; init; }

    public required ICakeContext CakeContext { get; init; }

    public required IPathService Paths { get; init; }

    public required FakeFileSystem FileSystem { get; init; }

    public required FakeEnvironment Environment { get; init; }

    public DirectoryPath RepoRoot => Paths.RepoRoot;

    public FilePath ResolveFile(string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);

        var path = new FilePath(relativePath);
        return path.IsRelative
            ? RepoRoot.CombineWithFilePath(path)
            : path;
    }

    public bool Exists(string relativePath)
    {
        var path = ResolveFile(relativePath);
        return FileSystem.GetFile(path).Exists;
    }

    public string ReadAllText(string relativePath)
    {
        var path = ResolveFile(relativePath);
        using var stream = FileSystem.GetFile(path).OpenRead();
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    public async Task<string> ReadAllTextAsync(string relativePath)
    {
        var path = ResolveFile(relativePath);
        using var stream = FileSystem.GetFile(path).OpenRead();
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync();
    }
}

public sealed class VcpkgInstalledFake
{
    private readonly FakeRepoBuilder _builder;
    private readonly string _triplet;

    internal VcpkgInstalledFake(FakeRepoBuilder builder, string triplet)
    {
        _builder = builder ?? throw new ArgumentNullException(nameof(builder));
        _triplet = triplet ?? throw new ArgumentNullException(nameof(triplet));
    }

    public VcpkgInstalledFake WithBinFile(string fileName, string content = "")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        _builder.WithTextFile($"vcpkg_installed/{_triplet}/bin/{fileName}", content);
        return this;
    }

    public VcpkgInstalledFake WithLibFile(string fileName, string content = "")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        _builder.WithTextFile($"vcpkg_installed/{_triplet}/lib/{fileName}", content);
        return this;
    }

    public VcpkgInstalledFake WithShareFile(string packageName, string relativePath, string content = "")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packageName);
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        _builder.WithTextFile($"vcpkg_installed/{_triplet}/share/{packageName}/{relativePath}", content);
        return this;
    }
}

public enum FakeRepoPlatform
{
    Windows,
    Unix,
}
