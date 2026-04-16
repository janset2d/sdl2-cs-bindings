# Test Infrastructure Modernization Plan

**Date:** 2026-04-15
**Status:** Planned — execute in a dedicated session after the coverage-ratchet session closes (#86 landing)
**Origin:** Discussion on 2026-04-15 about unit tests hitting the real filesystem via `TempDirectoryTestBase` + `File.WriteAllTextAsync`
**Parent reference:** [refactor-opportunities-2026-04-15.md item #9](./refactor-opportunities-2026-04-15.md)

## Motivation

Task-level "Run" tests (`PreFlightCheckTaskRunTests`, `CoverageCheckTaskRunTests`, `HarvestTaskTests`, `ConsolidateHarvestTests`, `ProgramCompositionRootTests`) create real temp directories and write real files to disk to simulate a repo layout. Reader-level unit tests (`CoberturaReaderTests`, `CoverageBaselineReaderTests`) were migrated to `FakeFileSystem` in the coverage-ratchet session; the task-level tier is the remaining straggler.

Keeping real disk I/O in the unit test tier is harmful because:

- **Non-determinism.** Antivirus file locks, CI container temp-dir permission quirks, and parallel writes (TUnit runs tests in parallel by default) introduce flakiness that has nothing to do with the code under test.
- **Speed.** Disk I/O is milliseconds per operation; a `FakeFileSystem` is microseconds. The cost compounds across five test classes with roughly 25 tests between them.
- **Asymmetry.** The repo now has two competing test styles (Cake-native `FakeFileSystem` for readers; real disk for task runs). New contributors will copy whichever they see first. Convergence is cheaper than divergence.
- **Hidden coupling.** Tests that write manifest JSON strings are re-encoding the schema by hand. Each new `IPathService` method has to be mocked in two places (`CreateBuildContext` and `CreateBuildContextForRepoRoot`) — drift slips in silently.

The coverage-ratchet session added `CoverageCheckTaskRunTests` that replicates the same pattern, so the debt grew before it shrank. The fix is to build a proper Cake-native fake-repo abstraction once and migrate all callers onto it.

## Scope

### Execution amendment (2026-04-15, same day)

Approved during execution:

- First pass widened from a strict pilot to a bigger initial wave: `PreFlightCheckTaskRunTests` + `CoverageCheckTaskRunTests` together, followed by the harvest/consolidation/composition-root task tier in the same session when practical.
- The cleanup target widened from "task-level run tests only" to a repo-wide build-test rule: `build/_build.Tests/` should not use `System.IO.File.*`, `System.IO.Directory.*`, or temp-directory scaffolding directly. Even characterization tests that intentionally inspect the real repo should do so through Cake's `FileSystem`, not raw `System.IO` static APIs.
- `FakeRepoBuilder` is a reusable test-infra primitive, not a one-off helper for this session. Async read helpers are part of the intended shape so future tests can assert on generated outputs without falling back to ad-hoc utilities.

### In scope — migrate to `FakeFileSystem`

| Test class | Tests | Disk payload today |
| --- | --- | --- |
| `PreFlightCheckTaskRunTests` | 4 | `build/manifest.json`, `vcpkg.json` |
| `CoverageCheckTaskRunTests` | 6 | `build/coverage-baseline.json`, `artifacts/test-results/build-tests/coverage.cobertura.xml` |
| `HarvestTaskTests` | *(to be counted)* | `vcpkg_installed/<triplet>/` tree simulation, manifest |
| `ConsolidateHarvestTests` | *(to be counted)* | per-RID status JSON files under `artifacts/harvest_output/<lib>/rid-status/` |
| `ProgramCompositionRootTests` | *(to be counted)* | manifest.json (for DI resolution that reads schema v2.1) |

### Out of scope — keep real-disk behaviour

| Test class | Rationale |
| --- | --- |
| `ManifestDeserializationTests` (Characterization) | Contract test between repo's real `build/manifest.json` and the schema. Purpose **is** to detect repo drift; fake FS defeats the intent. |
| `VersionConsistencyTests.RealVcpkgJson_*` | Same rationale — characterization against the real `vcpkg.json`. |
| Future harvest / deploy tool integration tests | Real `dumpbin` / `ldd` / `otool` invocations need real binaries on disk. If such tests are ever added, they live in a separate project (e.g., `Build.IntegrationTests`), not inside the unit-test suite. |

### Meta-improvement (related but separable)

Move characterization tests out of `Unit/` and into a dedicated `Characterization/` root so the category is visually distinct from hermetic unit tests. Low-priority; group with this plan if convenient, skip otherwise.

## Abstraction Design — `FakeRepoBuilder`

Fluent builder exposing the intent-level vocabulary the build host uses (manifest, vcpkg manifest, coverage baseline, cobertura report, harvest status, vcpkg installed tree). Each writer method encodes the canonical path in one place; tests read like prose.

### Target API

```csharp
namespace Build.Tests.Fixtures;

public sealed class FakeRepoBuilder
{
    private readonly FakeEnvironment _environment;
    private readonly FakeFileSystem _fileSystem;
    private readonly DirectoryPath _repoRoot;
    private readonly List<string> _libraries = [];
    private string? _rid;

    public FakeRepoBuilder(
        FakeRepoPlatform platform = FakeRepoPlatform.Unix,
        string repoRoot = "/repo")
    {
        _environment = platform switch
        {
            FakeRepoPlatform.Windows => FakeEnvironment.CreateWindowsEnvironment(),
            FakeRepoPlatform.Unix => FakeEnvironment.CreateUnixEnvironment(),
            _ => throw new ArgumentOutOfRangeException(nameof(platform)),
        };
        _fileSystem = new FakeFileSystem(_environment);
        _repoRoot = new DirectoryPath(repoRoot);
    }

    // ── Config writers ──
    public FakeRepoBuilder WithManifest(string json);                                 // build/manifest.json
    public FakeRepoBuilder WithManifest(ManifestConfig manifest);                     // serialised for you
    public FakeRepoBuilder WithVcpkgJson(string json);                                // vcpkg.json
    public FakeRepoBuilder WithVcpkgJson(VcpkgManifest vcpkg);
    public FakeRepoBuilder WithCoverageBaseline(string json);                         // build/coverage-baseline.json
    public FakeRepoBuilder WithCoverageBaseline(CoverageBaseline baseline);

    // ── Artifact writers ──
    public FakeRepoBuilder WithCoberturaReport(string xml, FilePath? relativePath = null);
    public FakeRepoBuilder WithHarvestStatus(string libraryName, string rid, string json);
    public FakeRepoBuilder WithVcpkgInstalledLayout(string triplet, Action<VcpkgInstalledFake> configure);

    // ── Context knobs (mirrors ParsedArguments today) ──
    public FakeRepoBuilder WithLibraries(params string[] libraries);
    public FakeRepoBuilder WithRid(string rid);
    public FakeRepoBuilder WithConfig(string config);         // Debug/Release
    public FakeRepoBuilder WithArgument(string name, string value);   // for ICakeArguments substitution

    // ── Terminal build ──
    public BuildContext BuildContext();
    public (BuildContext Context, FakeFileSystem FileSystem, FakeEnvironment Environment) BuildContextWithHandles();
}

public enum FakeRepoPlatform { Unix, Windows }
```

### Key design choices

- **Platform awareness** — The current `TaskTestHelpers` hard-codes `FakeEnvironment.CreateWindowsEnvironment()`. Several platform-dependent paths (system_exclusions, path separators, Cake's `IFile.OpenRead` encoding) change behaviour across platforms. `FakeRepoPlatform` makes the intent explicit at the call site.
- **Two writer flavours per config** — `WithManifest(string json)` for hand-crafted strings (when the test is about invalid JSON) and `WithManifest(ManifestConfig manifest)` for valid-path tests (serialise-from-object avoids hand-rolling the schema every time).
- **Return both `BuildContext` and handles when needed** — Most tests only need the context. Tests that want to assert on file system state (e.g., "did the task write this output file?") use `BuildContextWithHandles()` to reach the `FakeFileSystem`.
- **Fluent order-independence** — Writers can be called in any order; only the final `BuildContext()` call materialises the context.
- **No real disk dependency whatsoever** — `FakeRepoBuilder` never touches `System.IO.File` or `Directory.*`. Tests become truly hermetic.

### Composition with existing substitutes

`BuildContext` today takes `IPathService` as a Substitute with method-level returns wired up. The builder keeps this pattern: `IPathService` is still substituted (because Cake's `IFileSystem` abstraction doesn't own path construction, `PathService` does), but the substitute's paths point into the fake file system's tree instead of a real temp directory. `context.FileExists(path)` and `file.OpenRead()` then "just work" against the fake.

```csharp
// Inside BuildContext(), roughly:
var pathService = Substitute.For<IPathService>();
pathService.RepoRoot.Returns(_repoRoot);
pathService.GetManifestFile().Returns(_repoRoot.Combine("build").CombineWithFilePath("manifest.json"));
pathService.GetCoverageBaselineFile().Returns(_repoRoot.Combine("build").CombineWithFilePath("coverage-baseline.json"));
// … etc.

var cakeContext = Substitute.For<ICakeContext>();
cakeContext.FileSystem.Returns(_fileSystem);
cakeContext.Environment.Returns(_environment);
// … and the arguments/tools/etc. substitutes as today.
```

## Migration Plan

### Step 0 — Preparation (no code changes yet)

- Confirm `Cake.Testing.FakeFileSystem.CreateFile(FilePath)` + `FakeFileExtensions.SetContent(string)` round-trip cleanly for the binary-ish payloads we need (cobertura XML, JSON). Reader unit tests already exercise this path; should pass, but worth a sanity check before we rest the whole task tier on it.
- Verify `Cake.Common.IO.FileAliases.FileExists(ICakeContext, FilePath)` resolves through the substituted `ICakeContext.FileSystem`. (Expected yes; reader tests prove the `IFile.OpenRead` leg.)

### Step 1 — Land `FakeRepoBuilder` + pilot migration

- Add `build/_build.Tests/Fixtures/FakeRepoBuilder.cs` with the API above.
- Add `build/_build.Tests/Fixtures/VcpkgInstalledFake.cs` helper for the harvest-test scenarios (stubs out the per-triplet `vcpkg_installed/<triplet>/bin/*.dll` tree).
- Migrate **`PreFlightCheckTaskRunTests`** as the pilot. Smallest surface (4 tests, two config files). Validates the manifest-round-trip path and the strategy-coherence check.
- Expected diff: each test collapses from ~30 lines (temp-dir setup + hand-writing JSON) to ~10 lines (builder chain + `.BuildContext()`).

### Step 2 — Migrate the rest, one class per commit

1. `CoverageCheckTaskRunTests` — validates cobertura + coverage-baseline writers. Small payloads, easy second migration.
2. `ConsolidateHarvestTests` — per-RID status JSON; exercises the harvest-status writer.
3. `HarvestTaskTests` — biggest, requires `WithVcpkgInstalledLayout` with a real-ish binary closure. If this step grows, split into a dedicated sub-session.
4. `ProgramCompositionRootTests` — DI resolution against manifest.json; probably trivial with the builder.

Each commit keeps the old and the new alive temporarily; old `TaskTestHelpers` stays usable until the last class is migrated. No big-bang.

### Step 3 — Retire the old abstractions

- Delete `TaskTestHelpers.CreateBuildContext` and `CreateBuildContextForRepoRoot`.
- Keep `TaskTestHelpers.DeleteDirectoryQuietly` + `TempDirectoryTestBase` **only** if a future `Build.IntegrationTests` project needs them. Otherwise delete and un-add `TempDirectoryTestBase` inheritance from any remaining class.
- Remove `#pragma warning disable MA0045` from reader files if async read is introduced as part of this work (optional; see [refactor-opportunities #3](./refactor-opportunities-2026-04-15.md)).

### Step 4 — Meta-improvement (optional, same session)

Move `ManifestDeserializationTests` out of `Unit/Tasks/` / `Characterization/ConfigContract/` into a top-level `Characterization/` root. Rename folder if needed. Purpose: make the "this test deliberately reads the real repo state" category visually distinct from the hermetic unit tier.

## Risks and Edge Cases

| Risk | Mitigation |
| --- | --- |
| `FakeFileSystem` might not round-trip large XML or complex JSON cleanly | Reader unit tests already pass with cobertura XML and coverage-baseline JSON (proven in the ratchet session). Harvest/consolidate payloads are smaller (status JSONs). Risk low; if a specific edge case surfaces, fall back to real disk for that single class with an explicit comment. |
| `HarvestTask` may do things beyond file I/O that are tangled with disk (process spawn, real tool invocation) | The harvest *task* uses `IBinaryClosureWalker` — which is already interface-injected. Real process spawning happens inside scanners (`WindowsDumpbinScanner` et al.), which have their own substitution pattern. Task-level tests probably don't spawn processes; verify before migration. |
| `ProgramCompositionRootTests` tests the DI container wiring, which might depend on `Program.ConfigureBuildServices` reading a real file | Already uses the `ConfigureBuildServices` seam. The builder just provides a manifest.json file in the fake repo; DI resolution reads via `context.ToJson<T>()`, which goes through `IFileSystem`. Should be transparent. |
| Parallel test execution + shared `FakeFileSystem` instance | `FakeRepoBuilder` creates a new `FakeFileSystem` per call; no shared mutable state across tests. Parallel safety free. |
| Contributor muscle memory (everyone has internalised the temp-dir pattern) | One committed commit + deleted `CreateBuildContextForRepoRoot` is the forcing function. Update any "how to write a task test" docs (`cake-build-architecture.md`?) with the new builder as the canonical example. |
| Breaking parity with any playbook that describes the current test style | Search `docs/playbook/` for "TempDirectoryTestBase" or similar references before final commit. |

## Success Criteria

- Zero `System.IO.File.*` or `Directory.*` calls in `build/_build.Tests/` **except** inside the two characterization test classes (where they are intentional).
- `TaskTestHelpers.CreateBuildContextForRepoRoot` deleted.
- `TempDirectoryTestBase` deleted or scoped to integration-test use only.
- Full test suite green.
- Test suite runtime measurably lower (measure before/after; expect ~10–30% reduction on this test tier due to removed disk I/O).
- `cake-build-architecture.md` (or a dedicated "writing task tests" playbook) documents `FakeRepoBuilder` as the canonical entry point.

## Open Questions (resolve before or during Step 1)

1. Do any of the migrated test classes rely on `FakeEnvironment` platform behaviour (Windows vs Unix path separators, system DLL exclusions)? If so, `FakeRepoBuilder` needs an explicit `.WithPlatform(...)` call for those cases; default probably stays Unix.
2. Does `Cake.Common.IO.FileAliases.FileExists` short-circuit through `ICakeContext.FileSystem`, or does it bypass to `System.IO.File.Exists`? If the latter, every `context.FileExists(path)` call in production code needs a dedicated substitution on `ICakeContext` (not just `IFileSystem`). Worth checking early.
3. Should the builder expose a `.BuildContextAsync()` variant in case future async-read refactors (refactor-opportunities #3) land? Probably YAGNI for now.

## Done Criteria for This Plan

This plan itself is "done" when the coverage-ratchet session commits land. Execution of the plan opens a new session titled "Test Infra Modernization" that uses this document as its brief.
