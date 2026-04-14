# Cake Frosting Testing Strategy — Research & Approach

**Date:** 2026-04-14
**Status:** Active implementation guide — updated with critical review findings and execution plan
**Related:** [#85](https://github.com/janset2d/sdl2-cs-bindings/issues/85), [tunit-testing-framework-2026-04-14.md](tunit-testing-framework-2026-04-14.md)

## Critical Review Update (2026-04-14)

Post-baseline review findings for `build/_build.Tests`:

- Initial baseline: 120/120 passing.
- Current baseline after first hardening slice: 123/123 passing.
- Current baseline after second hardening slice: 126/126 passing.
- Current baseline after third hardening slice: 129/129 passing.
- Current baseline after fourth hardening slice: 139/139 passing.
- Current baseline after fifth hardening slice: 150/150 passing.
- Initial coverage baseline from `artifacts/test-results/build-tests/coverage.cobertura.xml`:
  - Line coverage: 13.76%
  - Branch coverage: 12.76%
- Current coverage after first hardening slice:
  - Line coverage: 17.05%
  - Branch coverage: 15.18%
- Current coverage after second hardening slice:
    - Line coverage: 23.07%
    - Branch coverage: 17.99%
- Current coverage after third hardening slice:
    - Line coverage: 26.23%
    - Branch coverage: 20.50%
- Current coverage after fourth hardening slice:
    - Line coverage: 48.17%
    - Branch coverage: 37.19%
- Current coverage after fifth hardening slice:
    - Line coverage: 48.17%
    - Branch coverage: 37.19%
- Critical gap: several refactor-sensitive paths are still under-tested:
    - `Program.cs` composition root / CLI path
    - Async result chaining extensions
- Test architecture concern (initially observed): one test file mirrored production logic instead of validating production behavior.
  - `ConsolidateHarvestTests` no longer mirrors consolidation logic and now validates `ConsolidateHarvestTask` behavior with real RID status inputs.
- Test topology progress:
    - Core realignment is in place with tests organized under `Unit/Modules`, `Unit/Tasks`, `Unit/Context`, and `Characterization/ConfigContract`.
    - Runtime boundary tests are now present under `Unit/Modules/DependencyAnalysis` and `Unit/Modules/Harvesting` (`VcpkgCliProvider`).
    - Tool wrapper tests are now present under `Unit/Tools` (`Dumpbin`, `Ldd`, `Otool`, `Vcpkg`).
    - Remaining topology work is focused on explicit `Integration` buckets.

## Coverage in TUnit + Microsoft.Testing.Platform

### Why prior coverage attempts looked inconsistent

In this stack (`TUnit` + `Microsoft.Testing.Platform`), the most reliable coverage path is the test-application extension options (`--coverage`) instead of relying on VSTest-style collector habits.

### Recommended command (deterministic output path)

```powershell
dotnet test build/_build.Tests/Build.Tests.csproj -- \
    --results-directory "E:/repos/my-projects/janset2d/sdl2-cs-bindings/artifacts/test-results/build-tests" \
    --coverage \
    --coverage-output-format cobertura \
    --coverage-output "coverage.cobertura.xml"
```

### Notes

- If `--results-directory` is relative, output is resolved relative to the test app working location. Use an absolute path for deterministic CI/local behavior.
- Discover currently available extension options with:

```powershell
dotnet test build/_build.Tests/Build.Tests.csproj -- --help
```

## Target Test Topology (Mirror `build/_build` boundaries)

Current test structure is mostly unit-centric and partially mixed by concern. For refactor safety, reorganize to mirror production boundaries:

```text
build/_build.Tests/
├── Fixtures/
├── Unit/
│   ├── Modules/
│   │   ├── Harvesting/
│   │   ├── DependencyAnalysis/
│   │   └── Extensions/
│   ├── Tasks/
│   │   ├── Harvest/
│   │   ├── Preflight/
│   │   └── Dependency/
│   ├── Tools/
│   │   ├── Dumpbin/
│   │   ├── Ldd/
│   │   ├── Otool/
│   │   └── Vcpkg/
│   └── Context/
├── Integration/
│   ├── Pipeline/
│   └── TaskFlows/
└── Characterization/
        └── ConfigContract/
```

Intent:

- `Unit/*`: whitebox confidence at class/method behavior.
- `Integration/*`: blackbox confidence for orchestrated flows and side effects.
- `Characterization/*`: real-config stability tests for refactor guardrails.

## Key Finding: Cake.Testing Package

Cake provides an official `Cake.Testing` NuGet package (v5.0.0, compatible with our Cake.Frosting 5.0.0 / Cake.Core 5.0.0). This package provides fake implementations of core Cake infrastructure:

| Fake | Replaces | Purpose |
| --- | --- | --- |
| `FakeFileSystem` | `IFileSystem` | In-memory filesystem with `FakeFile` / `FakeDirectory` |
| `FakeEnvironment` | `ICakeEnvironment` | Simulated OS platform, working directory, env vars |
| `FakeLog` | `ICakeLog` | Captures all log entries for assertion |
| `FakeProcess` | Process execution | Intercepts process starts, returns configurable results |
| `FakeConfiguration` | `ICakeConfiguration` | In-memory build configuration |
| `FakePlatform` | Platform detection | Simulates Windows/Linux/macOS |
| `FakeRuntime` | Runtime information | Simulates .NET runtime details |
| `ToolFixture<TSettings>` | Full tool test harness | Wires up fake filesystem + process runner + environment for tool wrapper testing |

**This eliminates the need for `System.IO.Abstractions` / TestableIO.** Cake already provides its own fake filesystem that integrates with `ICakeContext`. Since all our code uses Cake's `IFileSystem` (via `ICakeContext`), we should use `Cake.Testing` fakes — not a third-party filesystem abstraction.

## Cake's Own Testing Pattern

Cake's codebase uses a consistent pattern for testing tool wrappers:

```csharp
// 1. Create a fixture that extends ToolFixture<TSettings>
public class DumpbinFixture : ToolFixture<DumpbinSettings>
{
    public DumpbinFixture() : base("dumpbin.exe") { }

    protected override void RunTool()
    {
        // Call the tool wrapper under test
        var tool = new DumpbinDependentsTool(FileSystem, Environment, ProcessRunner, Tools);
        tool.Run(Settings);
    }
}

// 2. Test uses the fixture
[Test]
public async Task DumpbinDependents_Should_Pass_Correct_Arguments()
{
    var fixture = new DumpbinFixture();
    fixture.Settings.DependentsPath = new FilePath("SDL2.dll");

    var result = fixture.Run();

    await Assert.That(result.Args).Contains("/dependents");
    await Assert.That(result.Args).Contains("SDL2.dll");
}
```

This pattern is ideal for our **dumpbin, ldd, otool, and vcpkg tool wrappers**.

## Our Build System — Testing Layers

### Layer 1: Tool Wrappers (dumpbin, ldd, otool, vcpkg)

**What:** `DumpbinDependentsTool`, `LddRunner`, `OtoolRunner`, `VcpkgPackageInfoTool`, `VcpkgInstallTool`

**How to test:** `Cake.Testing.ToolFixture` pattern.

- `FakeProcess` captures the command-line arguments the tool would pass
- No real process execution needed
- Verifies argument construction, settings mapping, output parsing
- Tests tool resolution logic (dumpbin's MSVC discovery, vcpkg path resolution)

**What this gives us:** Confidence that our tool wrappers generate correct CLI commands and parse output correctly.

### Layer 2: Runtime Scanners (WindowsDumpbinScanner, LinuxLddScanner, MacOtoolScanner)

**What:** `IRuntimeScanner` implementations that call Layer 1 tools and parse their output into dependency sets.

**How to test:** Two approaches:

**Option A — Mock the tool layer:**

```csharp
// NSubstitute mock of ICakeContext with canned process output
var ctx = Substitute.For<ICakeContext>();
// Configure ctx to return pre-recorded dumpbin output
var scanner = new WindowsDumpbinScanner(ctx);
var deps = await scanner.ScanAsync(new FilePath("SDL2.dll"));
// Assert deps contains expected dependency names
```

**Option B — Canned output fixtures:**
Store real dumpbin/ldd/otool output as test resources, feed them to the scanner's parsing logic. This tests the parsing without mocking.

**Recommended:** Option B for parsing logic (pure function extraction), Option A for integration.

### Layer 3: Core Services (BinaryClosureWalker, ArtifactPlanner)

**What:** Business logic that orchestrates tool calls and produces deployment plans.

**How to test:** Mock the boundaries (`IRuntimeScanner`, `IPackageInfoProvider`, `IRuntimeProfile`) via NSubstitute. Feed canned data in, assert output structure.

```csharp
// BinaryClosureWalker test setup
var mockScanner = Substitute.For<IRuntimeScanner>();
mockScanner.ScanAsync(Arg.Any<FilePath>(), Arg.Any<CancellationToken>())
    .Returns(new HashSet<FilePath> { new FilePath("SDL2.dll") }.ToImmutableHashSet());

var mockPkg = Substitute.For<IPackageInfoProvider>();
mockPkg.GetPackageInfoAsync("sdl2-image", "x64-windows-hybrid", default)
    .Returns(new PackageInfo(
        PackageName: "sdl2-image",
        Triplet: "x64-windows-hybrid",
        OwnedFiles: [new FilePath("bin/SDL2_image.dll")].ToImmutableList(),
        DeclaredDependencies: ["sdl2:x64-windows-hybrid"]));

var mockProfile = Substitute.For<IRuntimeProfile>();
mockProfile.IsSystemFile(Arg.Any<FilePath>()).Returns(false);
mockProfile.PlatformFamily.Returns(PlatformFamily.Windows);
mockProfile.Triplet.Returns("x64-windows-hybrid");

var ctx = Substitute.For<ICakeContext>();
// ... wire up ctx.FileExists, ctx.Log etc.

var walker = new BinaryClosureWalker(mockScanner, mockPkg, mockProfile, ctx);
var result = await walker.BuildClosureAsync(manifest);
```

**Critical tests:**

- `BuildClosureAsync_Should_Include_Primary_Binaries`
- `BuildClosureAsync_Should_Walk_Transitive_Dependencies`
- `BuildClosureAsync_Should_Exclude_System_Files`
- `BuildClosureAsync_Should_Exclude_Vcpkg_Internal_Packages`
- `CreatePlanAsync_Should_Filter_Core_Deps_From_Satellites`
- `CreatePlanAsync_Should_Use_Archive_Strategy_On_Unix`
- `CreatePlanAsync_Should_Use_DirectCopy_Strategy_On_Windows`

### Layer 4: ArtifactDeployer (filesystem operations)

**What:** Copies files, creates tar.gz archives.

**How to test:** `Cake.Testing.FakeFileSystem` for file copy operations. Process mock for tar invocation.

```csharp
var env = FakeEnvironment.CreateUnixEnvironment();
var fileSystem = new FakeFileSystem(env);
// Create fake source files
fileSystem.CreateFile("/vcpkg_installed/x64-linux-hybrid/lib/libSDL2.so");

var ctx = /* build ICakeContext with fake filesystem */;
var deployer = new ArtifactDeployer(ctx);
var plan = /* create deployment plan with actions */;

await deployer.DeployArtifactsAsync(plan);

// Assert files were copied to expected locations
Assert.That(fileSystem.Exist(new FilePath("/output/native/libSDL2.so")), Is.True);
```

**Challenge:** `ArtifactDeployer` uses `ctx.StartProcess("tar", ...)` for archive creation. This needs `FakeProcess` or a process mock.

### Layer 5: Task Orchestration (HarvestTask, ConsolidateHarvestTask)

**What:** Thin orchestrators that call services in sequence.

**How to test:** Mock all injected services. Verify call sequence and error handling.

```csharp
var mockWalker = Substitute.For<IBinaryClosureWalker>();
var mockPlanner = Substitute.For<IArtifactPlanner>();
var mockDeployer = Substitute.For<IArtifactDeployer>();

// Configure mocks to return success
mockWalker.BuildClosureAsync(Arg.Any<LibraryManifest>())
    .Returns(new BinaryClosure(...));

// After pipeline extraction, test IHarvestPipeline
var pipeline = new HarvestPipeline(mockWalker, mockValidator, mockPlanner, mockDeployer);
await pipeline.RunAsync(manifest, ctx);

// Verify sequence
await mockWalker.Received(1).BuildClosureAsync(manifest);
await mockPlanner.Received(1).CreatePlanAsync(manifest, Arg.Any<BinaryClosure>(), Arg.Any<DirectoryPath>());
await mockDeployer.Received(1).DeployArtifactsAsync(Arg.Any<DeploymentPlan>());
```

## ICakeContext Mocking Strategy

`ICakeContext` is a god-interface with many extensions. Two approaches:

### Approach A: Full NSubstitute mock (fragile, verbose)

```csharp
var ctx = Substitute.For<ICakeContext>();
ctx.FileSystem.Returns(fakeFileSystem);
ctx.Environment.Returns(fakeEnvironment);
ctx.Log.Returns(new FakeLog());
// + dozens of other members
```

**Problem:** ICakeContext has many properties and extension methods. Mocking them all is tedious and brittle.

### Approach B: Cake.Testing + thin adapter (recommended)

Use `Cake.Testing` fakes for filesystem/environment/log, and create a minimal `TestCakeContext` that wires them together:

```csharp
public sealed class TestCakeContext : ICakeContext
{
    public TestCakeContext(PlatformFamily platform = PlatformFamily.Windows)
    {
        Environment = platform switch
        {
            PlatformFamily.Windows => FakeEnvironment.CreateWindowsEnvironment(),
            PlatformFamily.Linux or PlatformFamily.OSX => FakeEnvironment.CreateUnixEnvironment(),
            _ => throw new ArgumentException(...)
        };
        FileSystem = new FakeFileSystem(Environment);
        Log = new FakeLog();
        // ... wire other required members
    }

    public IFileSystem FileSystem { get; }
    public ICakeEnvironment Environment { get; }
    public ICakeLog Log { get; }
    // ... other ICakeContext members
}
```

**This is the recommended approach.** It uses Cake's own fakes (not third-party), integrates naturally with our code, and is reusable across all test layers.

## Recommended Test Plan — Phase 1 Completion

### Already done (120 tests baseline)

Pure functions and config parsing are in place, plus first-pass closure/planner/deployer coverage.

### Critical gap from review (must fix before large refactor)

- Replace test-side mirrored logic in consolidation tests with real SUT behavior assertions.
- Add missing blackbox tests for task orchestration and side effects.
- Add scanner/provider/tool-wrapper tests for currently untested runtime boundaries. ✅

### Next batch: BinaryClosureWalker + ArtifactPlanner (~15-20 tests)

These are the core business logic. Use NSubstitute for `IRuntimeScanner`, `IPackageInfoProvider`, `IRuntimeProfile`. Mock `ICakeContext` minimally (just `ctx.Log` and `ctx.FileExists`).

**Key test scenarios:**

| Test | What it validates |
| --- | --- |
| `BuildClosureAsync_Should_Return_Primary_Binaries_From_Manifest_Patterns` | Pattern matching → primary file identification |
| `BuildClosureAsync_Should_Walk_Package_Dependencies_Recursively` | vcpkg metadata graph walk |
| `BuildClosureAsync_Should_Merge_Runtime_Scan_Results_Into_Closure` | Two-phase merge (metadata + runtime) |
| `BuildClosureAsync_Should_Skip_System_Files_In_Runtime_Scan` | System file filtering |
| `BuildClosureAsync_Should_Skip_Vcpkg_Internal_Packages` | `vcpkg-cmake`, `vcpkg-cmake-config` ignored |
| `BuildClosureAsync_Should_Return_Error_When_Package_Not_Found` | Error path for missing package |
| `CreatePlanAsync_Should_Exclude_Core_Deps_From_Satellite_Plans` | Core library filtering |
| `CreatePlanAsync_Should_Create_FileCopyActions_On_Windows` | Windows strategy |
| `CreatePlanAsync_Should_Create_ArchiveAction_On_Linux` | Unix strategy |
| `CreatePlanAsync_Should_Include_License_Files_From_Share_Dir` | License discovery |
| `CreatePlanAsync_Should_Calculate_Correct_Statistics` | Statistics accuracy |

### Future batch: Composition Root + Async Extensions

Focus remaining refactor-sensitive gaps in `Program.cs` argument/config composition and async extension behavior/error paths.

### Future batch: ArtifactDeployer with FakeFileSystem

File copy and tar operations. Requires `TestCakeContext` infrastructure.

## Refactor-Readiness Execution Plan (Whitebox + Blackbox)

### Phase A — Architecture Cleanup (test structure and anti-pattern removal)

1. Realign test folders to mirror `build/_build` boundaries.
2. Move real-file manifest contract checks into `Characterization` scope.
3. Rewrite consolidation tests to validate `ConsolidateHarvestTask` outputs, not copied logic.

### Phase B — Blackbox Task Coverage

1. Add `HarvestTask` flow tests (success/failure/status-file behavior).
2. Add `ConsolidateHarvestTask` flow tests from real `rid-status/*.json` inputs.
3. Add `PreFlightCheckTask.Run` integration-style behavior tests (pass/fail/log).

### Phase C — Whitebox Runtime Boundary Coverage

1. Add scanner parsing suites using canned outputs (`dumpbin`, `ldd`, `otool`).
2. Add `VcpkgCliProvider` tests for success, malformed JSON, and error paths.
3. Add tool-wrapper argument tests using `ToolFixture`.

Status: ✅ Completed for scanner/provider/tool-wrapper coverage baseline.

### Phase D — Flakiness and Determinism Hardening

1. Ensure no hidden shared mutable state in fixtures.
2. Isolate filesystem/process dependencies via fakes in unit tests.
3. Keep time/random/temp-path dependencies out of assertions unless explicitly controlled.

### Phase E — Coverage Ratchet and Enforcement

1. Store deterministic Cobertura output under `artifacts/test-results/build-tests/`.
2. Add no-regression rule first, then raise thresholds incrementally.
3. Use branch coverage as first-class signal (not line-only).

## Quality Gates for Refactor Start

Before major refactor implementation starts:

1. No mirrored production logic in tests.
2. Blackbox task-level flow coverage exists for Harvest/Consolidate/PreFlight.
3. Scanner/provider/tool-wrapper boundaries have at least baseline coverage.
4. Coverage command is deterministic and documented.
5. Coverage ratchet policy is defined and enforceable.

## Package to Add

```xml
<!-- Directory.Packages.props -->
<PackageVersion Include="Cake.Testing" Version="5.0.0" />
```

```xml
<!-- Build.Tests.csproj -->
<PackageReference Include="Cake.Testing" />
```

## Summary

| Question | Answer |
| --- | --- |
| Do we need System.IO.Abstractions? | **No** — Cake.Testing.FakeFileSystem covers our needs |
| How to mock ICakeContext? | Cake.Testing fakes + thin `TestCakeContext` adapter |
| How to test tool wrappers? | Cake.Testing.ToolFixture pattern |
| How to test core services? | NSubstitute mock boundaries (IRuntimeScanner, IPackageInfoProvider) |
| How to test filesystem ops? | Cake.Testing.FakeFileSystem for copy, FakeProcess for tar |
| Priority order? | BinaryClosureWalker → ArtifactPlanner → HarvestPipeline → Tool wrappers → ArtifactDeployer |
