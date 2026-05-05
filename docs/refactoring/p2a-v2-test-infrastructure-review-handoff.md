# P2a V2 Test Infrastructure Review Handoff

- **Status:** Required implementation handoff
- **Scope:** `build/_build.Tests` V2 test infrastructure, P2a docs, and ADR-002 refactoring docs
- **Baseline merge:** `f55c9b5` (`worktree-p1-baseline-adr002` -> `master`)
- **Audience:** Agent implementing the required fixes

This document consolidates the required fixes from two reviews:

1. the first-pass review of P1 baseline + P2a V2 test infrastructure;
2. the follow-up review of an external deep review.

This is an implementation directive. Do the work below in order. Push back only if implementation exposes a concrete contradiction in the codebase.

## Ground rules

1. Keep production build behavior unchanged unless a listed item explicitly requires test-host composition changes.
2. Prefer Cake-native test seams: `FakeFileSystem`, `ICakeContext`, `ICakeLog`, `IProcessRunner`, `IToolLocator`, `FilePath`, and `DirectoryPath`.
3. Do not add reflection-based architecture tracker tests. ADR-002 intentionally moves away from design-police tests.
4. Do not add Linux/OSX fake platform split now. `FakeRepoPlatformV2.Unix` is acceptable; runtime differences are RID-driven.
5. Do not add `HasMessageExact` until a real test needs exact log text.
6. Avoid VSTest-only commands such as `dotnet test --filter`; this repo uses TUnit on Microsoft.Testing.Platform.
7. Treat files under `docs/superpowers/` as temporary execution artifacts. Useful durable decisions from those files must be promoted to canonical docs under `docs/refactoring/`, `docs/decisions/`, `AGENTS.md`, or an appropriate playbook before the implementation is considered complete.

## Implementation order

| Priority | Work item | Why |
| --- | --- | --- |
| 1 | Fail-fast process/tool fakes with explicit opt-in default behavior | Prevents false-green scenario tests |
| 2 | Process fake tests and invocation capture | Proves seeded commands are actually used |
| 3 | Production `AddHostBuildingBlocks(parsedArgs)` plus runtime-bearing fake manifest | Restores DI parity and production-shaped `RuntimeProfile` behavior |
| 4 | `ToLegacyBuildContext` behavior tests | Protects the V1/V2 bridge after the host-composition change |
| 5 | P2a docs drift and canonical-doc promotion rule | Keeps docs from becoming a stale GPS |
| 6 | Sync + async task support in `TargetTestHostV2` | Existing target surface includes sync `FrostingTask<BuildContext>` tasks |
| 7 | CA1031 suppression cleanup and direct exception capture | Keeps warning policy honest and debug behavior clear |
| 8 | `TestLogV2` lock/snapshot thread-safety | Cheap defensive improvement while preserving order |
| 9 | Code/document boundary cleanup | Keeps phase timing in canonical docs instead of `.cs` comments |

## 1. Process/tool fakes must fail fast by default

**Current code:** `build/_build.Tests/Fixtures/FakeCakeWorldV2.cs`

`BuildCakeContext()` currently returns a successful default process for unconfigured commands:

- `IProcessRunner.Start(...)` falls through to a `FakeProcess` with exit code `0`;
- `IToolLocator.Resolve(...)` falls back to `/dev/null`.

That can make a scenario pass even when the test forgot to seed the command, misspelled the command, or failed to configure tool resolution. This is the highest-priority false-green risk in P2a.

### Required changes

Change `FakeCakeWorldV2` so that:

1. Unknown process commands fail fast by default with an actionable exception containing:
   - command path/name;
   - rendered arguments when available;
   - a hint to call `WithProcessResult(...)` or the explicit default API.
2. Missing tool resolution fails fast by default with an actionable exception containing:
   - requested tool name(s);
   - a hint to call `WithToolPath(...)` or the explicit default API.
3. Add explicit opt-in permissive APIs for tests that intentionally do not care about a specific command/tool:
   - process API: `WithDefaultProcessResult(int exitCode = 0, string stdOut = "", string stdErr = "")`;
   - tool API: `WithDefaultToolPath(FilePath toolPath)`.

Do not preserve "unmatched command -> exit code 0" as the default contract.

### Tests to add

Add focused tests for `FakeCakeWorldV2` process behavior:

- matched command propagates exit code;
- matched command captures stdout lines;
- matched command captures stderr lines;
- command matching remains case-insensitive;
- unknown command throws by default;
- unknown command uses explicit default only when the default API is configured.

Add focused tests for tool behavior:

- configured tool path resolves;
- missing tool path throws by default;
- explicit default tool path resolves only when configured.

## 2. Capture process invocations, then strengthen `InfoTask` scenarios

**Current code:** `build/_build.Tests/Scenarios/Info/InfoTask_Scenarios.cs`

The first scenario currently proves that `InfoTask` can be resolved and run without throwing, but it does not prove that the seeded `dotnet --version` process was actually invoked.

### Required changes

1. Add invocation capture to `FakeCakeWorldV2` with this public API shape:

   ```csharp
   public IReadOnlyList<ProcessInvocation> ProcessInvocations { get; }

   public sealed record ProcessInvocation(
       FilePath Command,
       string Arguments,
       bool RedirectStandardOutput,
       bool Silent);
   ```

   `ProcessInvocations` returns a snapshot. Do not add fluent assertion helpers; tests should assert on the raw captured values directly.
2. Capture one `ProcessInvocation` for every `IProcessRunner.Start(...)` call before command-result lookup runs.
3. Update `InfoTask_Scenarios` to assert that `dotnet --version` was invoked.
4. After invocation capture is in place, add the failure scenario:
   - seed `dotnet` with non-zero exit code;
   - assert the task still completes because `InfoTask` reports the failed SDK probe instead of throwing;
   - assert the captured invocation proves the failure path used the fake process.

Do not introduce production `IAnsiConsole` refactoring in this P2a polish unless explicitly approved. The Spectre.Console limitation can remain deferred.

## 3. Track and test the legacy compatibility shim without architecture-police tests

**Current code:** `build/_build.Tests/Fixtures/FakeCakeWorldV2.cs`

`ToLegacyBuildContext(...)` bridges V2 test infrastructure to unmigrated code that still expects the old `BuildContext` / `Configurations` / `ParsedArguments` shape.

### Required changes

1. Add explicit P5 retirement work to the canonical refactor plan/checklist:
   - `ToLegacyBuildContext(...)` retires when `Host/Configuration` and `Configurations` retire;
   - migrated target tests should reduce reliance on legacy state;
   - P5 must remove the shim or explicitly document any remaining bridge.
2. Add behavior tests for the shim.
3. Do **not** add `[Obsolete]` or reflection-count tracker tests.

### Shim tests to add

Cover:

- Windows RID -> `RuntimeFamily.Windows` and expected triplet;
- Linux RID -> `RuntimeFamily.Linux` and expected triplet;
- macOS RID -> `RuntimeFamily.OSX` and expected triplet;
- configured `Rid` and `Config` flow into `ParsedArguments`;
- configured repo root flows into `PathService` / `BuildContext.Paths`;
- provided manifest instance is used;
- default manifest is valid enough for the shim's current contract;
- `BuildContext.Runtime.IsSystemFile(...)` uses production-shaped runtime behavior after the host-composition work in §5 is implemented.

## 4. Stop mocking `IRuntimeProfile` in the legacy shim

**Current code:** `build/_build.Tests/Fixtures/FakeCakeWorldV2.cs`

The shim currently creates `IRuntimeProfile` with NSubstitute and only sets `Rid`, `Triplet`, and `Family`. The actual interface also includes `IsSystemFile(string)`, and production creates a concrete `RuntimeProfile` through `AddHostBuildingBlocks(parsedArgs)`.

### Required changes

Do not implement this as a standalone "construct `RuntimeProfile` inside `ToLegacyBuildContext`" rewrite. The runtime profile must come from the production host-building block introduced in §5.

1. Remove `Substitute.For<IRuntimeProfile>()` from `ToLegacyBuildContext`.
2. Ensure `BuildContext.Runtime` is the `IRuntimeProfile` resolved from the service provider after `AddHostBuildingBlocks(parsedArgs)` has been applied.
3. Keep `ToLegacyBuildContext` as a compatibility bridge for unmigrated tasks only; it may assemble `BuildContext`, but it must not own runtime-profile creation.
4. Reuse existing fixture helpers (`RuntimeProfileFixture`, `ManifestFixture`) only when they reduce duplication without bypassing the production host-building block.

The reason is not hypothetical extra properties; the concrete behavior that matters is `IsSystemFile(...)`.

## 5. Move `TargetTestHostV2` to production host composition

**Current code:** `build/_build.Tests/Fixtures/TargetTestHostV2.cs`

The harness currently manually registers Cake primitives, `BuildContext`, and configuration sub-records. Production composition uses `AddHostBuildingBlocks(parsedArgs)` plus feature/tool/integration registration groups.

### Required changes

1. Do not call private/local `Program.cs` helpers.
2. Do call the production extension method `AddHostBuildingBlocks(parsedArgs)`.
3. Register fake Cake primitives and required configuration records before calling `AddHostBuildingBlocks(parsedArgs)`:
   - `ICakeContext`;
   - `ICakeLog`;
   - `ICakeEnvironment`;
   - `IFileSystem`;
   - `IGlobber`;
   - `ICakeArguments`;
   - `ICakeConfiguration`;
   - `RepositoryConfiguration`;
   - `VcpkgConfiguration`;
   - `PackageBuildConfiguration`;
   - `DotNetBuildConfiguration`;
   - `DumpbinConfiguration`.
4. Seed a runtime-bearing fake manifest before `AddHostBuildingBlocks(parsedArgs)` can resolve `RuntimeConfig` / `IRuntimeProfile`:
   - the default fake manifest must include at least one `RuntimeInfo` matching the fake world's active RID;
   - `WithManifest(...)` / `WithManifestObject(...)` must either provide a runtime entry matching the active RID or fail with an actionable error before `RuntimeConfig.Runtimes.Single(...)` produces a cryptic exception;
   - keep `SystemExclusions` populated so `RuntimeProfile.IsSystemFile(...)` has production-shaped data.
5. Build `BuildContext` from provider-resolved production-shaped services:
   - `IPathService`;
   - `IRuntimeProfile`;
   - `ManifestConfig`;
   - `ParsedArguments`;
   - `Configurations`.
6. Override fake-specific services after the production host block where needed, using explicit replacement rather than silent accidental shadowing.
7. Document in the P2a design/refactor docs that `TargetTestHostV2` intentionally uses production host-building blocks plus fake overrides.

Use `Microsoft.Extensions.DependencyInjection.Extensions.Replace` where it makes the override intent clearer than "last registration wins".

## 6. Support sync and async Frosting tasks

**Current code:** `build/_build.Tests/Fixtures/TargetTestHostV2.cs`

`TargetTestHostV2<TTask>` currently constrains `TTask` to `AsyncFrostingTask<BuildContext>`. The current build target surface includes sync `FrostingTask<BuildContext>` tasks, for example:

- `CoverageCheckTask`;
- `EnsureVcpkgDependenciesTask`.

### Required changes

1. Broaden the host so it can run both sync and async Frosting tasks.
2. Keep the public test API simple; do not create two parallel host types unless Cake type constraints force it.
3. Add at least one minimal test proving a sync task can be executed through the host.

## 7. Clean up broad exception capture in `TargetTestHostV2`

**Current code:** `build/_build.Tests/Fixtures/TargetTestHostV2.cs`

The file uses `#pragma warning disable CA1031` and captures an exception with `ExceptionDispatchInfo.Capture(ex).SourceException`, but it never rethrows through the dispatch info.

### Required changes

1. Remove the file-wide `#pragma warning disable CA1031`.
2. Remove the unused `System.Runtime.ExceptionServices` import.
3. Return `ex` directly in `TargetRunResultV2`.
4. Add a local, justified suppression for the method/helper that intentionally catches all exceptions.

Important: a `SuppressMessage` attribute cannot be applied to a `catch` clause. Put it on `RunAsync()` or extract the catch into a small helper if that gives a cleaner suppression target.

Use this justification:

> Test harness intentionally captures all exception types so scenario tests can assert on failure modes without losing the log.

## 8. Make `TestLogV2` thread-safe while preserving order

**Current code:** `build/_build.Tests/Fixtures/TestLogV2.cs`

`TestLogV2` stores entries in `List<LogEntry>`. That is fine for today's sequential tasks but cheap to harden.

### Required changes

Use a lock rather than `ConcurrentBag`:

1. Keep insertion order stable.
2. Protect writes and reads with a private lock.
3. Return snapshots from `Entries` and count/helper properties.
4. Preserve the existing per-call evaluation model: `HasMessage(...)`, `HasNoMessages(...)`, `ErrorCount`, `WarningCount`, and `InfoCount` should all observe the latest entries at the time they are called. `Entries` becoming a snapshot is consistent with that model and avoids exposing the mutable backing list.

This is low priority; do it after the process, shim, composition, and scenario work.

## 9. Update P2a docs and promote temporary superpowers decisions

**Current docs:**

- `docs/superpowers/specs/2026-05-05-p2a-test-infrastructure-design.md`
- `docs/superpowers/plans/2026-05-05-p2a-test-infrastructure.md`
- `docs/refactoring/target-centric-build-host-refactor-plan.md`
- `docs/refactoring/target-centric-build-host-review-checklist.md`

The superpowers plan/spec still describes the original implementation plan in places:

- 3 `InfoTask` scenarios;
- 499 tests;
- `dotnet test --filter ...` commands that do not work with the current MTP/TUnit runner.

### Required changes

1. Treat `docs/superpowers/` files as temporary execution artifacts, not canonical design.
2. Move any durable decisions into canonical docs:
   - this handoff document;
   - the refactor plan;
   - the review checklist;
   - ADR-002 or `AGENTS.md` only when the rule is truly agent-wide.
3. Update the superpowers P2a files to "as-built / superseded" status and make clear that canonical truth now lives in `docs/refactoring/`.
4. Replace or remove VSTest-style `--filter` commands. If a supported MTP/TUnit targeted command is not known, use the full build-host suite:

```pwsh
dotnet test --project build\_build.Tests\Build.Tests.csproj -c Release --framework net10.0
```

5. Update expected counts to match the actual suite after any new tests are added.

## 10. Code/document boundary cleanup

New `.cs` comments include phase-tracking language such as "retires P5" and "When InfoPipeline migrates to Targets/Info/ in P4". The local explanation is useful, but phase numbers belong in canonical docs, not logic-bearing comments.

### Required changes

1. Keep comments that explain local behavior.
2. Remove phase-number tracking from `.cs` comments.
3. Move migration timing to the refactor plan/checklist.
4. Update `AGENTS.md` and the review checklist so future work treats phase numbers, ADR references, and migration timing as canonical-doc content rather than `.cs` comment content.

Example replacement:

```csharp
// Compatibility shim for unmigrated tasks that still require the legacy BuildContext shape.
```

## 11. Explicit non-actions

Do not implement these unless Deniz reopens them:

1. Do not split `FakeRepoPlatformV2.Unix` into Linux and macOS yet.
2. Do not add `HasMessageExact(...)` until a test needs it.
3. Do not add reflection-based shim usage tracker tests.
4. Do not freeze the current "unmatched process succeeds" behavior in tests.
5. Do not refactor production `InfoPipeline` / Spectre.Console output just to satisfy P2a scenario assertions. The canonical fix is documented in §12 below for when this is reopened.

## 12. Deferred — Spectre.Console parallel test isolation

**Status:** Deferred. Do not implement as part of the P2a polish; record kept here so the fix is not lost.

### Why this section exists

`InfoPipeline.cs:51` calls `await AnsiConsole.Status().StartAsync(...)`. The static `AnsiConsole` facade owns one `IAnsiConsole.Console` instance with a per-instance `DefaultExclusivityMode` semaphore. Two parallel tests that exercise any code path through `Status()` / `Progress()` / `Live()` / `Prompt()` race on that single semaphore and the second one throws:

> `InvalidOperationException: Trying to run one or more interactive functions concurrently...`

This is by design (overlap on a real terminal corrupts output) and confirmed in [Spectre.Console DefaultExclusivityMode source](https://github.com/spectreconsole/spectre.console/blob/main/src/Spectre.Console/Internal/DefaultExclusivityMode.cs). `AnsiConsole.Console = null!` reset is worse — every other test hitting `AnsiConsole.Write` / `MarkupLine` then NREs.

### Scope

Repo-wide audit confirmed only `InfoPipeline.cs:51` uses an interactive widget today. `AnsiConsole.Write` / `WriteLine` / `MarkupLine` calls in `OtoolAnalyzePipeline`, `HarvestPipeline`, `Program.cs` are **not** interactive and do not race.

Fix surface: 1 production class, ~6 files total, ~30 lines of diff.

### Canonical fix

Constructor-inject `IAnsiConsole`. Endorsed by Patrik Svensson in [discussion #1020](https://github.com/spectreconsole/spectre.console/discussions/1020) and [discussion #831](https://github.com/spectreconsole/spectre.console/discussions/831). `Status` / `Progress` / `Live` are extension methods on `IAnsiConsole`, so migration is mechanical:

1. **Add package** using the repository package-management workflow; do not manually edit XML. The resulting central/package references should include:

   ```xml
   <!-- Directory.Packages.props -->
   <PackageVersion Include="Spectre.Console.Testing" Version="0.49.1" />
   ```

   ```xml
   <!-- build/_build.Tests/Build.Tests.csproj -->
   <PackageReference Include="Spectre.Console.Testing" />
   ```

2. **Production composition root** — register the global facade as `IAnsiConsole`:

   ```csharp
   // build/_build/Program.cs ConfigureBuildServices, before AddHostBuildingBlocks
   services.AddSingleton<IAnsiConsole>(AnsiConsole.Console);
   ```

3. **Refactor `InfoPipeline`** — first ctor parameter, replace every `AnsiConsole.X` with `_console.X`:

   ```csharp
   public sealed class InfoPipeline(
       IAnsiConsole console,
       ICakeContext cakeContext,
       ICakeLog log,
       IRuntimeProfile runtimeProfile)
   {
       private readonly IAnsiConsole _console = console ?? throw new ArgumentNullException(nameof(console));
       // ...
       public async Task RunAsync()
       {
           _console.Write(new FigletText("Build Info").Color(Color.CornflowerBlue));
           // ...
           await _console.Status()
               .Spinner(Spinner.Known.Dots)
               .StartAsync("[aqua]Checking .NET SDK Version...[/]", _ => { /* work */ });
           _console.MarkupLine($"[bold aqua].NET SDK Version:[/] {sdkVersion}");
       }
   }
   ```

4. **Test fixture** — each `FakeCakeWorldV2` exposes its own `TestConsole`:

   ```csharp
   // FakeCakeWorldV2.cs
   public Spectre.Console.Testing.TestConsole AnsiConsole { get; } = new();
   ```

   ```csharp
   // TargetTestHostV2.cs RunAsync, alongside other Cake primitives
   services.AddSingleton<IAnsiConsole>(_world.AnsiConsole);
   ```

   `TestConsole` ships with `NoopExclusivityMode` baked in — every test gets its own non-throwing console, fully parallel-safe.

5. **Scenario assertions become possible** — the `InfoTask` failure path scenario and log/output assertions deferred in `InfoTask_Scenarios.cs` can now be written:
   - capture rendered output via `world.AnsiConsole.Output`;
   - assert on log entries via `world.Log` (item 9 in this handoff applies);
   - exercise the `dotnet --version` failure path via `world.WithProcessResult("dotnet", exitCode: 1, ...)` (item 2 in this handoff applies).

### Why deferred and not done now

- ADR-002 P4 phase migrates `InfoTask` into `Targets/Info/`. The `IAnsiConsole` injection naturally fits in that same migration slice — touching `InfoPipeline.cs` once instead of twice.
- The Spectre fix is **not blocking** the P2a polish work. Items 1-10 in this handoff deliver real value (fail-fast fakes, invocation capture, shim tests, host composition parity, sync task support) without it.
- The deferred `InfoTask` failure-path scenario (item 2) can be written today using only `world.Log` and `world.ProcessInvocations` assertions; the spinner output capture is cherry on top.

### When to reopen

Reopen this section's work when **either** condition holds:

1. ADR-002 P4 reaches `InfoTask` migration. The `IAnsiConsole` injection should be folded into that migration slice.
2. A second `Status()` / `Progress()` / `Live()` call site is added anywhere in the build host. The fix surface stops being trivially small the moment a second site exists, so do it pre-emptively at that point.

Until then, leave `InfoPipeline.cs` on the static facade.

### Sources

- [discussion #1020 — Patrik Svensson DI guidance](https://github.com/spectreconsole/spectre.console/discussions/1020)
- [discussion #831 — TestConsole for unit tests (Patrik Svensson)](https://github.com/spectreconsole/spectre.console/discussions/831)
- [discussion #1500 — alternative IAnsiConsole / SimpleConsole](https://github.com/spectreconsole/spectre.console/discussions/1500)
- [Spectre.Console.Testing source dir](https://github.com/spectreconsole/spectre.console/tree/main/src/Spectre.Console.Testing)
- [Spectre.Console testing docs](https://spectreconsole.net/console/how-to/testing-console-output)
- [DefaultExclusivityMode source](https://github.com/spectreconsole/spectre.console/blob/main/src/Spectre.Console/Internal/DefaultExclusivityMode.cs)

## Validation

After implementing the fixes, run:

```pwsh
dotnet test --project build\_build.Tests\Build.Tests.csproj -c Release --framework net10.0
```

Also run any newly supported targeted MTP/TUnit command only after verifying the syntax works in this repository. Do not use `dotnet test --filter`.

## Completion criteria

The fix is complete when:

1. unconfigured process/tool fakes fail loudly by default;
2. an explicit opt-in default fake process/tool API exists;
3. process invocation capture proves `InfoTask` calls `dotnet --version`;
4. shim behavior is tested;
5. `RuntimeProfile` behavior is production-shaped through `AddHostBuildingBlocks(parsedArgs)`;
6. sync and async Frosting tasks are both runnable through V2 host;
7. host composition uses production `AddHostBuildingBlocks(parsedArgs)` plus documented fake overrides;
8. CA1031 suppression is local and justified;
9. `TestLogV2` is thread-safe without losing log order;
10. temporary superpowers decisions are promoted or marked superseded;
11. `.cs` comments explain local behavior without phase tracker language.

