# P2a — Test Infrastructure V2 Design

- **Status:** Superseded — canonical truth in `docs/refactoring/p2a-v2-test-infrastructure-review-handoff.md`, `docs/refactoring/target-centric-build-host-refactor-plan.md`, and git history. This file is a temporary execution artifact.
- **Date:** 2026-05-05
- **Scope:** `build/_build.Tests/Fixtures/` (new V2 files), `build/_build.Tests/Scenarios/` (first scenario)
- **Non-goal:** Foundation primitives (P2b), production code changes, retiring existing test infra

## Context

The build-host test suite (482 tests) depends on `FakeRepoBuilder` and `TestHostFixture`, both coupled to the old `Host/Configuration` aggregate and `Features/` layer model. ADR-002 retires these abstractions (P5), so test infrastructure needs a migration path that doesn't break existing tests while new code moves to the target architecture.

`homeruntech/dotnet-backend-monorepo-tool` provided the pattern reference: composable fake world, hybrid test logger with capture + assertion, scenario tests that build a fake filesystem and run real orchestration.

## Decision

Greenfield V2 test infrastructure with `V2` postfix, living alongside existing fixtures. New and migrated tests use V2; unmigrated tests continue using `FakeRepoBuilder` / `TestHostFixture` until their owning targets are refactored (P3–P9).

### Test migration rule

When a production target is migrated to `Targets/<CakeTargetName>/`, its existing tests under `Unit/Features/<OldFeature>/` must be migrated to the V2 test infrastructure in the same migration slice. New unit tests go under `Unit/Targets/<CakeTargetName>/`, new scenarios under `Scenarios/<CakeTargetName>/`. Old test fixtures are available for unmigrated code but must not be extended.

## Architecture

Three composable layers, each independently testable:

### Layer 1: `FakeCakeWorldV2` (`Fixtures/FakeCakeWorldV2.cs`)

The fake Cake world: environment + filesystem + log + context. Single factory, fluent seeding API.

```
FakeCakeWorldV2
├── FakeFileSystem (Cake FakeFileSystem)
├── FakeEnvironment (Cake FakeEnvironment — platform: Windows/Unix)
├── TestLogV2 (captured log output)
├── ICakeContext (NSubstitute — fake process runner, tool locator, arguments)
├── DirectoryPath RepoRoot
└── Seeding API:
    ├── WithTextFile(relativePath, content)
    ├── WithManifestFile(json)
    ├── WithManifestObject(ManifestConfig)
    ├── WithProcessResult(command, exitCode, stdOut, stdErr?)
    └── WithToolPath(FilePath)
```

- No dependency on `Configurations`, `BuildContext`, or `Host/Configuration`.
- Process/tool execution faked via NSubstitute on `IProcessRunner` + `IToolLocator`.
- `WithProcessResult` maps a command string to fake stdout/stderr/exit code — simple and explicit.
- `FakeFileSystem` is the only filesystem abstraction.

### Layer 2: `TestLogV2` (`Fixtures/TestLogV2.cs`)

Wraps Cake log output for capture and assertion. Replaces `FakeLog` and `NSubstitute` log verification.

```csharp
public sealed class TestLogV2 : ICakeLog
{
    // ICakeLog: every Write() call captured into _entries
    // Assertion: HasMessage(level, contains), HasNoMessages(level)
    // Inspection: Entries, ErrorCount, WarningCount, InfoCount
}
```

- Implements `ICakeLog` so it drops into Cake DI directly.
- Captures every log call as a `LogEntry` struct.
- Provides assertion helpers for scenario tests.
- Equivalent to monorepo tool's `HybridTestLogger` MockOnly mode but without Moq dependency.

### Layer 3: `TargetTestHostV2<TTask>` (`Fixtures/TargetTestHostV2.cs`)

Runs a real Frosting task class in a fake Cake world and captures the result.

```csharp
public sealed class TargetTestHostV2<TTask> where TTask : AsyncFrostingTask<BuildContext>
{
    public TargetTestHostV2(FakeCakeWorldV2 world);
    public TargetTestHostV2<TTask> WithServices(Action<IServiceCollection> register);
    public async Task<TargetRunResultV2> RunAsync();
}

public sealed record TargetRunResultV2(bool Success, Exception? Exception, TestLogV2 Log);
```

- `WithServices` accepts production `AddXFeature()` extension methods directly.
- `RunAsync` builds the DI container, resolves `TTask`, constructs a compatibility `BuildContext` from the world, and calls `RunAsync(buildContext)`.
- For unmigrated targets: a compatibility shim constructs `Configurations` + `BuildContext` from the fake world. This shim retires automatically in P5 when `Configurations` is removed.
- For migrated targets (post-P3): DI is configured with named concepts and `BuildContext` named properties.
- `TargetRunResultV2` is a simple record — no result monad, just success flag + exception + log.

### Compatibility shim

`FakeCakeWorldV2` exposes a `ToLegacyBuildContext()` factory method that constructs the old `Configurations` → `BuildContext` chain. This is an explicit, documented shim. It exists only to bridge V2 tests to unmigrated task code. It is deleted in P5 alongside `Host/Configuration`.

## First scenario: InfoTask

```csharp
// build/_build.Tests/Scenarios/Info/InfoTask_Should_Run_Without_Throwing_On_Windows.cs

public sealed class InfoTask_Scenarios
{
    [Test]
    public async Task RunAsync_Should_Complete_Without_Exception_When_DotNet_Is_Available()
    {
        var world = FakeCakeWorldV2.Create(FakeRepoPlatform.Windows)
            .WithProcessResult("dotnet", exitCode: 0, stdOut: "10.0.203\n");

        var host = new TargetTestHostV2<InfoTask>(world)
            .WithServices(s => s.AddInfoFeature());

        var result = await host.RunAsync();

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Log.HasMessage(LogLevel.Information, ".NET SDK Version"))
            .IsTrue();
    }
}
```

## File inventory

| File | Status | Notes |
|---|---|---|
| `Fixtures/FakeCakeWorldV2.cs` | New | Greenfield V2 world |
| `Fixtures/TestLogV2.cs` | New | Capture + assertion logger |
| `Fixtures/TargetTestHostV2.cs` | New | Task runner harness |
| `Scenarios/Info/InfoTask_Scenarios.cs` | New | First proof-of-concept scenario |
| `Fixtures/FakeRepoBuilder.cs` | Keep | Unchanged, retires P5 |
| `Fixtures/TestHostFixture.cs` | Keep | Unchanged, retires P5 |
| `Fixtures/FakeCakeToolContextBuilder.cs` | Keep | Used by V1 tests, retires P5 |

## Anti-goals

- Do not introduce `System.IO.Abstractions` — Cake `FakeFileSystem` is the standard.
- Do not create a giant abstract `TestBase` — TUnit creates instances per test; use constructors.
- Do not touch existing 482 tests — they continue using V1 fixtures.
- Do not create interfaces for V2 test fixtures — they are concrete test infrastructure.
- Do not support both `Configurations` and V2 patterns in the same test — new tests use V2 only.

## References

- [ADR-002: Target-Centric Cake Build Host Architecture](../decisions/2026-05-05-target-centric-build-host.md)
- [Target-Centric Build Host Refactoring Plan §11 P2](../refactoring/target-centric-build-host-refactor-plan.md#11-phase-plan)
- [homeruntech/dotnet-backend-monorepo-tool](https://github.com/homeruntech/dotnet-backend-monorepo-tool) — test infrastructure pattern reference
