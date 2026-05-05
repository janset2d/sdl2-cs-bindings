# P2a — Test Infrastructure V2 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Greenfield V2 test infrastructure (FakeCakeWorldV2, TestLogV2, TargetTestHostV2) with first InfoTask scenario test, living alongside existing V1 fixtures.

**Architecture:** Three composable layers with V2 postfix. FakeCakeWorldV2 provides fake filesystem + environment + process + log. TestLogV2 wraps ICakeLog for capture + assertion. TargetTestHostV2<TTask> builds DI, resolves task, runs it, returns structured result. Compatibility shim bridges to unmigrated Configurations/BuildContext.

**Tech Stack:** .NET 10 / C# 14, TUnit, Cake.Testing (FakeFileSystem, FakeEnvironment), NSubstitute, Microsoft.Extensions.DependencyInjection

---

### Task 1: TestLogV2 — capture logger with assertions

**Files:**
- Create: `build/_build.Tests/Fixtures/TestLogV2.cs`
- Create: `build/_build.Tests/Unit/Fixtures/TestLogV2Tests.cs`

- [ ] **Step 1: Write the unit test for TestLogV2**

Create `build/_build.Tests/Unit/Fixtures/TestLogV2Tests.cs`:

```csharp
using Cake.Core.Diagnostics;
using Build.Tests.Fixtures;

namespace Build.Tests.Unit.Fixtures;

public sealed class TestLogV2Tests
{
    [Test]
    public async Task Write_Should_Capture_LogEntry()
    {
        var log = new TestLogV2();

        log.Write(Verbosity.Normal, LogLevel.Information, "hello {0}", "world");

        await Assert.That(log.Entries).HasCount(1);
        await Assert.That(log.Entries[0].Level).IsEqualTo(LogLevel.Information);
        await Assert.That(log.Entries[0].Message).Contains("hello world");
    }

    [Test]
    public async Task HasMessage_Should_Return_True_When_Message_Contains_Substring()
    {
        var log = new TestLogV2();
        log.Write(Verbosity.Normal, LogLevel.Error, "something went wrong: disk full");

        await Assert.That(log.HasMessage(LogLevel.Error, "disk full")).IsTrue();
    }

    [Test]
    public async Task HasMessage_Should_Return_False_When_Message_Does_Not_Contain_Substring()
    {
        var log = new TestLogV2();
        log.Write(Verbosity.Normal, LogLevel.Information, "all good");

        await Assert.That(log.HasMessage(LogLevel.Error, "disk full")).IsFalse();
    }

    [Test]
    public async Task HasNoMessages_Should_Return_True_When_No_Entries_At_Level()
    {
        var log = new TestLogV2();
        log.Write(Verbosity.Normal, LogLevel.Information, "info only");

        await Assert.That(log.HasNoMessages(LogLevel.Error)).IsTrue();
    }

    [Test]
    public async Task HasNoMessages_Should_Return_False_When_Entries_Exist_At_Level()
    {
        var log = new TestLogV2();
        log.Write(Verbosity.Normal, LogLevel.Error, "fail");

        await Assert.That(log.HasNoMessages(LogLevel.Error)).IsFalse();
    }

    [Test]
    public async Task Write_Should_Filter_By_Verbosity()
    {
        var log = new TestLogV2 { Verbosity = Verbosity.Quiet };

        log.Write(Verbosity.Verbose, LogLevel.Information, "should be filtered");

        await Assert.That(log.Entries).IsEmpty();
    }

    [Test]
    public async Task ErrorCount_Should_Return_Count_Of_Error_Entries()
    {
        var log = new TestLogV2();
        log.Write(Verbosity.Normal, LogLevel.Error, "e1");
        log.Write(Verbosity.Normal, LogLevel.Error, "e2");
        log.Write(Verbosity.Normal, LogLevel.Information, "info");

        await Assert.That(log.ErrorCount).IsEqualTo(2);
    }

    [Test]
    public async Task WarningCount_And_InfoCount_Should_Return_Respective_Counts()
    {
        var log = new TestLogV2();
        log.Write(Verbosity.Normal, LogLevel.Warning, "w1");
        log.Write(Verbosity.Normal, LogLevel.Information, "i1");
        log.Write(Verbosity.Normal, LogLevel.Information, "i2");

        await Assert.That(log.WarningCount).IsEqualTo(1);
        await Assert.That(log.InfoCount).IsEqualTo(2);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

```pwsh
dotnet test --project build/_build.Tests/Build.Tests.csproj -c Release --framework net10.0 --filter "FullyQualifiedName~TestLogV2Tests"
```

Expected: FAIL — `TestLogV2` type not found.

- [ ] **Step 3: Write TestLogV2 implementation**

Create `build/_build.Tests/Fixtures/TestLogV2.cs`:

```csharp
using Cake.Core;
using Cake.Core.Diagnostics;

namespace Build.Tests.Fixtures;

public sealed class TestLogV2 : ICakeLog
{
    private readonly List<LogEntry> _entries = [];

    public Verbosity Verbosity { get; set; } = Verbosity.Normal;

    public IReadOnlyList<LogEntry> Entries => _entries;

    public int ErrorCount => _entries.Count(e => e.Level == LogLevel.Error);
    public int WarningCount => _entries.Count(e => e.Level == LogLevel.Warning);
    public int InfoCount => _entries.Count(e => e.Level == LogLevel.Information);

    public void Write(Verbosity verbosity, LogLevel level, string format, params object[] args)
    {
        if (verbosity > Verbosity)
        {
            return;
        }

        var message = args.Length > 0
            ? string.Format(format, args)
            : format;

        _entries.Add(new LogEntry(level, message, verbosity));
    }

    public bool HasMessage(LogLevel level, string contains)
    {
        return _entries.Any(e =>
            e.Level == level &&
            e.Message.Contains(contains, StringComparison.OrdinalIgnoreCase));
    }

    public bool HasNoMessages(LogLevel level)
    {
        return !_entries.Any(e => e.Level == level);
    }
}

public sealed record LogEntry(LogLevel Level, string Message, Verbosity Verbosity);
```

- [ ] **Step 4: Run tests to verify they pass**

```pwsh
dotnet test --project build/_build.Tests/Build.Tests.csproj -c Release --framework net10.0 --filter "FullyQualifiedName~TestLogV2Tests"
```

Expected: 8 tests PASS.

- [ ] **Step 5: Commit**

```bash
git add build/_build.Tests/Fixtures/TestLogV2.cs \
        build/_build.Tests/Unit/Fixtures/TestLogV2Tests.cs
git commit -m "feat: TestLogV2 — capture logger with ICakeLog implementation and assertion helpers"
```

---

### Task 2: FakeCakeWorldV2 — fake Cake world factory

**Files:**
- Create: `build/_build.Tests/Fixtures/FakeCakeWorldV2.cs`
- Create: `build/_build.Tests/Unit/Fixtures/FakeCakeWorldV2Tests.cs`

- [ ] **Step 1: Write unit tests for FakeCakeWorldV2**

Create `build/_build.Tests/Unit/Fixtures/FakeCakeWorldV2Tests.cs`:

```csharp
using Build.Tests.Fixtures;
using Cake.Core.IO;

namespace Build.Tests.Unit.Fixtures;

public sealed class FakeCakeWorldV2Tests
{
    [Test]
    public async Task Create_Should_Set_Up_Windows_Fake_Environment_By_Default()
    {
        var world = FakeCakeWorldV2.Create();

        await Assert.That(world.Environment.Platform.Family).IsEqualTo(Cake.Core.PlatformFamily.Windows);
        await Assert.That(world.FileSystem).IsNotNull();
        await Assert.That(world.Log).IsNotNull();
        await Assert.That(world.CakeContext).IsNotNull();
    }

    [Test]
    public async Task Create_Should_Set_Up_Unix_Fake_Environment_When_Specified()
    {
        var world = FakeCakeWorldV2.Create(FakeRepoPlatformV2.Unix);

        await Assert.That(world.Environment.Platform.Family).IsEqualTo(Cake.Core.PlatformFamily.Linux);
    }

    [Test]
    public async Task WithTextFile_Should_Create_File_In_Fake_Filesystem()
    {
        var world = FakeCakeWorldV2.Create();

        world.WithTextFile("test/data.json", "{\"key\":42}");

        var exists = world.FileSystem.GetFile(world.RepoRoot.CombineWithFilePath("test/data.json")).Exists;
        await Assert.That(exists).IsTrue();
    }

    [Test]
    public async Task ReadAllText_Should_Return_File_Content()
    {
        var world = FakeCakeWorldV2.Create();
        world.WithTextFile("readme.md", "hello");

        var content = world.ReadAllText("readme.md");

        await Assert.That(content).IsEqualTo("hello");
    }

    [Test]
    public async Task FileExists_Should_Return_True_For_Existing_File()
    {
        var world = FakeCakeWorldV2.Create();
        world.WithTextFile("exists.txt", "");

        await Assert.That(world.FileExists("exists.txt")).IsTrue();
    }

    [Test]
    public async Task FileExists_Should_Return_False_For_Missing_File()
    {
        var world = FakeCakeWorldV2.Create();

        await Assert.That(world.FileExists("missing.txt")).IsFalse();
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

```pwsh
dotnet test --project build/_build.Tests/Build.Tests.csproj -c Release --framework net10.0 --filter "FullyQualifiedName~FakeCakeWorldV2Tests"
```

Expected: FAIL — `FakeCakeWorldV2` type not found.

- [ ] **Step 3: Write FakeCakeWorldV2 implementation**

Create `build/_build.Tests/Fixtures/FakeCakeWorldV2.cs`:

```csharp
using System.Text.Json;
using Build.Host;
using Build.Host.Configuration;
using Build.Host.Paths;
using Build.Shared.Manifest;
using Build.Shared.Runtime;
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

        var pathService = new PathService(
            new RepositoryConfiguration(_repoRoot),
            new ParsedArguments(
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
                VersionsFile: null),
            Log);

        var runtimeProfile = Substitute.For<IRuntimeProfile>();
        runtimeProfile.Rid.Returns(_rid);
        runtimeProfile.Triplet.Returns("x64-windows-hybrid");
        runtimeProfile.Family.Returns(RuntimeFamily.Windows);

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
            new ParsedArguments(
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
                VersionsFile: null),
            options);
    }

    // ── internal: build faked ICakeContext ──

    private ICakeContext BuildCakeContext()
    {
        var processRunner = Substitute.For<IProcessRunner>();
        processRunner.Start(Arg.Any<FilePath>(), Arg.Any<ProcessSettings>())
            .Returns(call =>
            {
                var settings = (ProcessSettings)call[1];
                var command = settings.Arguments switch
                {
                    null => "",
                    string s => s.Split(' ')[0],
                    _ => settings.Arguments.ToString() ?? "",
                };

                if (_processResults.TryGetValue(command, out var result))
                {
                    var process = new FakeProcess();
                    process.SetExitCode(result.ExitCode);
                    if (!string.IsNullOrEmpty(result.StdOut))
                    {
                        process.SetStandardOutput(result.StdOut.Split('\n'));
                    }
                    if (!string.IsNullOrEmpty(result.StdErr))
                    {
                        process.SetStandardError(result.StdErr.Split('\n'));
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
```

- [ ] **Step 4: Run tests to verify they pass**

```pwsh
dotnet test --project build/_build.Tests/Build.Tests.csproj -c Release --framework net10.0 --filter "FullyQualifiedName~FakeCakeWorldV2Tests"
```

Expected: 6 tests PASS.

- [ ] **Step 5: Run ALL existing tests to verify no regressions**

```pwsh
dotnet test --project build/_build.Tests/Build.Tests.csproj -c Release --framework net10.0
```

Expected: 488 tests PASS (original 482 + 8 TestLogV2 + 6 FakeCakeWorldV2 - actually 482 + 8 + 6 = 496. Wait, let me recount. Actually I think we just need to verify all pass regardless).

- [ ] **Step 6: Commit**

```bash
git add build/_build.Tests/Fixtures/FakeCakeWorldV2.cs \
        build/_build.Tests/Unit/Fixtures/FakeCakeWorldV2Tests.cs
git commit -m "feat: FakeCakeWorldV2 — fake Cake world factory with fluent seeding and legacy BuildContext shim"
```

---

### Task 3: TargetTestHostV2 — task runner harness

**Files:**
- Create: `build/_build.Tests/Fixtures/TargetTestHostV2.cs`

- [ ] **Step 1: Write TargetTestHostV2 implementation (tested through Task 4 scenario)**

Create `build/_build.Tests/Fixtures/TargetTestHostV2.cs`:

```csharp
using System.Runtime.ExceptionServices;
using Build.Host;
using Cake.Frosting;
using Microsoft.Extensions.DependencyInjection;

namespace Build.Tests.Fixtures;

public sealed class TargetTestHostV2<TTask> where TTask : AsyncFrostingTask<BuildContext>
{
    private readonly FakeCakeWorldV2 _world;
    private readonly List<Action<IServiceCollection>> _registrations = [];
    private Build.Shared.Manifest.ManifestConfig? _manifest;

    public TargetTestHostV2(FakeCakeWorldV2 world)
    {
        _world = world ?? throw new ArgumentNullException(nameof(world));
    }

    public TargetTestHostV2<TTask> WithServices(Action<IServiceCollection> register)
    {
        ArgumentNullException.ThrowIfNull(register);
        _registrations.Add(register);
        return this;
    }

    public TargetTestHostV2<TTask> WithManifest(Build.Shared.Manifest.ManifestConfig manifest)
    {
        _manifest = manifest ?? throw new ArgumentNullException(nameof(manifest));
        return this;
    }

    public async Task<TargetRunResultV2> RunAsync()
    {
        var services = new ServiceCollection();

        // Register the fake Cake world components
        services.AddSingleton(_world.CakeContext);
        services.AddSingleton<Cake.Core.Diagnostics.ICakeLog>(_world.Log);
        services.AddSingleton(_world.CakeContext.Environment);
        services.AddSingleton(_world.CakeContext.FileSystem);
        services.AddSingleton(_world.CakeContext.Globber);
        services.AddSingleton(_world.CakeContext.Arguments);
        services.AddSingleton(_world.CakeContext.Configuration);

        // Register compatibility BuildContext
        var buildContext = _world.ToLegacyBuildContext(_manifest);
        services.AddSingleton(buildContext);
        services.AddSingleton(buildContext.Manifest);
        services.AddSingleton(buildContext.Runtime);
        services.AddSingleton(buildContext.Paths);
        services.AddSingleton(buildContext.Options);
        services.AddSingleton(buildContext.Options.Vcpkg);
        services.AddSingleton(buildContext.Options.Package);
        services.AddSingleton(buildContext.Options.Repository);
        services.AddSingleton(buildContext.Options.DotNet);
        services.AddSingleton(buildContext.Options.Dumpbin);

        // Register the task class itself so DI resolves it
        services.AddSingleton<TTask>();

        // Apply target-specific registrations (e.g. AddInfoFeature())
        foreach (var register in _registrations)
        {
            register(services);
        }

        // Build provider and resolve task
        using var provider = services.BuildServiceProvider();
        var task = provider.GetRequiredService<TTask>();

        try
        {
            await task.RunAsync(buildContext);
            return new TargetRunResultV2(true, null, _world.Log);
        }
        catch (Exception ex)
        {
            var captured = ExceptionDispatchInfo.Capture(ex);
            return new TargetRunResultV2(false, captured.SourceException, _world.Log);
        }
    }
}

public sealed record TargetRunResultV2(
    bool Success,
    Exception? Exception,
    TestLogV2 Log
);
```

- [ ] **Step 2: Verify build compiles**

```pwsh
dotnet build build/_build.Tests/Build.Tests.csproj -c Release --framework net10.0
```

Expected: Build succeeds.

- [ ] **Step 3: Commit**

```bash
git add build/_build.Tests/Fixtures/TargetTestHostV2.cs
git commit -m "feat: TargetTestHostV2<TTask> — task runner harness with DI container and structured result"
```

---

### Task 4: First scenario test — InfoTask_Scenarios

**Files:**
- Create: `build/_build.Tests/Scenarios/Info/InfoTask_Scenarios.cs`

- [ ] **Step 1: Write the InfoTask scenario test**

Create `build/_build.Tests/Scenarios/Info/InfoTask_Scenarios.cs`:

```csharp
using Build.Features.Info;
using Build.Tests.Fixtures;
using Cake.Core.Diagnostics;

namespace Build.Tests.Scenarios.Info;

public sealed class InfoTask_Scenarios
{
    [Test]
    public async Task RunAsync_Should_Complete_Without_Exception_When_DotNet_Is_Available()
    {
        var world = FakeCakeWorldV2.Create(FakeRepoPlatformV2.Windows)
            .WithProcessResult("dotnet", exitCode: 0, stdOut: "10.0.203\n");

        var host = new TargetTestHostV2<InfoTask>(world)
            .WithServices(s => s.AddInfoFeature());

        var result = await host.RunAsync();

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Exception).IsNull();
    }

    [Test]
    public async Task RunAsync_Should_Log_DotNet_SDK_Version_When_Command_Succeeds()
    {
        var world = FakeCakeWorldV2.Create(FakeRepoPlatformV2.Windows)
            .WithProcessResult("dotnet", exitCode: 0, stdOut: "10.0.203\n");

        var host = new TargetTestHostV2<InfoTask>(world)
            .WithServices(s => s.AddInfoFeature());

        var result = await host.RunAsync();

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Log.HasMessage(LogLevel.Information, ".NET SDK Version"))
            .IsTrue();
    }

    [Test]
    public async Task RunAsync_Should_Complete_Even_When_DotNet_Command_Fails()
    {
        var world = FakeCakeWorldV2.Create(FakeRepoPlatformV2.Windows)
            .WithProcessResult("dotnet", exitCode: 1, stdOut: "", stdErr: "command not found");

        var host = new TargetTestHostV2<InfoTask>(world)
            .WithServices(s => s.AddInfoFeature());

        var result = await host.RunAsync();

        // InfoTask catches dotnet failures and continues — does not throw
        await Assert.That(result.Success).IsTrue();
    }
}
```

- [ ] **Step 2: Run only the scenario tests**

```pwsh
dotnet test --project build/_build.Tests/Build.Tests.csproj -c Release --framework net10.0 --filter "FullyQualifiedName~InfoTask_Scenarios"
```

Expected: 3 tests PASS.

- [ ] **Step 3: Run the full test suite to verify no regressions**

```pwsh
dotnet test --project build/_build.Tests/Build.Tests.csproj -c Release --framework net10.0
```

Expected: All 499 tests PASS (482 original + 8 TestLogV2 + 6 FakeCakeWorldV2 + 3 InfoTask scenarios).

- [ ] **Step 4: Commit**

```bash
git add build/_build.Tests/Scenarios/Info/InfoTask_Scenarios.cs
git commit -m "test: first V2 scenario — InfoTask runs without exception via TargetTestHostV2"

# Also save number of tests, count of passed, count of failed, count of skipped
Total: 499
Passed: 499
Failed: 0
Skipped: 0
```

---

### Task 5: Verify clean baseline

- [ ] **Step 1: Full test suite final verification**

```pwsh
dotnet test --project build/_build.Tests/Build.Tests.csproj -c Release --framework net10.0
```

Expected: All tests pass.

- [ ] **Step 2: Verify Cake target graph still healthy**

```pwsh
dotnet run --file tools.cs -- build --tree
dotnet run --file tools.cs -- build --target Info
```

Expected: Tree displays all 20 targets, Info runs successfully.

- [ ] **Step 3: Verify git log**

```bash
git log --oneline -5
```

Expected: 5 commits on top of the P2a spec commit (3 implementation + spec = 4 new commits since P1).

---

### P2a File Inventory (after all tasks)

| File | Status |
|---|---|
| `build/_build.Tests/Fixtures/TestLogV2.cs` | New |
| `build/_build.Tests/Unit/Fixtures/TestLogV2Tests.cs` | New |
| `build/_build.Tests/Fixtures/FakeCakeWorldV2.cs` | New |
| `build/_build.Tests/Unit/Fixtures/FakeCakeWorldV2Tests.cs` | New |
| `build/_build.Tests/Fixtures/TargetTestHostV2.cs` | New |
| `build/_build.Tests/Scenarios/Info/InfoTask_Scenarios.cs` | New |
| `build/_build.Tests/Fixtures/FakeRepoBuilder.cs` | Unchanged |
| `build/_build.Tests/Fixtures/TestHostFixture.cs` | Unchanged |
| `build/_build.Tests/Fixtures/FakeCakeToolContextBuilder.cs` | Unchanged |
