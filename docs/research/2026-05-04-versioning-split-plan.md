# Versioning Split + Configuration Retirement (start) — May 4, 2026

> **Status:** Plan v1 (ready, not yet implemented).
> **Successor to:** [2026-05-04-versioning-simplification-plan.md](2026-05-04-versioning-simplification-plan.md) (plan v3, mostly implemented; M6 partial-fix + P3a/P3b test backfill + P2 docs are open follow-ups but do not block this plan).
> **Session scope:** Versioning feature only. Configuration pattern retirement begins here; other features keep their Configuration records until a future session migrates them.

---

## 0. Decisions confirmed (this session)

The previous plan (v3) drove versioning from "single ResolveVersions task with --version-source dispatch" to a single foundational principle: stage tasks read versions-file only; explicit-version flags are ResolveVersions inputs. Plan v4 takes that further:

| Decision | Outcome |
| --- | --- |
| **Configuration record pattern is retired (gradual)** | Versioning is the first feature to drop typed Configuration. Tasks read `BuildContext.ParsedArguments` directly and self-validate. Other features keep their Configuration records until a future session re-touches them. |
| **`VersioningConfiguration` deleted entirely (not dead-data)** | The record is removed from `Host/Configuration/VersioningConfiguration.cs`, `Configurations` aggregate (slot count 6→5), and Program.cs construction. No archeology. |
| **`ResolveVersions` splits into two task-per-source** | `ResolveVersionsFromManifestTask` + `ResolveVersionsFromExplicitTask`. ResolveVersionsPipeline + ResolveVersionsTask + IPackageVersionProvider + ManifestVersionProvider + ExplicitVersionProvider all deleted; provider logic inlines into task bodies (with `internal` static helpers for testability). |
| **`--version-source` flag removed entirely** | Task name encodes the source. `ParsedArguments.VersionSource` field removed. `ResolveVersionsOptions.VersionSourceOption` removed. tools.cs and release.yml drop the flag. |
| **`--scope` is manifest-only** | Operator input mapping IS the scope in explicit mode; `--scope` adds nothing there. Manifest task keeps `--scope`; explicit task does not declare it. |
| **Per-task self-validation** | Each task validates its own `ParsedArguments` slice at `RunAsync` entry. No CLI-level required-option enforcement (architectural impossibility under flat root + Cake target dispatch — see plan v3 §0.1). |
| **Mutex enforcement moves to explicit task** | `--explicit-version` ↔ `--explicit-versions` mutex no longer in Program.cs (Program.cs becomes thinner: parse + DI wiring only, zero business logic). The check lives in the only consumer task. |
| **Provider classes deleted, not converted to resolver classes** | Inlined into task bodies. Pure-function helpers (`BuildVersionFor`, `NormalizeSuffix`, `BuildFilteredMapping`) become `internal static` methods on the task class. Tests target these via `[InternalsVisibleTo("Build.Tests")]` already present in repo, **OR** as integration-style task invocations — see §4 test transformation matrix. |
| **`ExplicitVersionParser` stays** | Pure CLI parsing helper; `ParseCliEntries` + `ParseCommaSeparated` remain at `Features/Versioning/ExplicitVersionParser.cs`. 16 existing tests in `ExplicitVersionParserTests.cs` carry forward unchanged. |
| **`VersionsJsonWriter` extracted as DI helper** | `Features/Versioning/VersionsJsonWriter.cs` (new). Both tasks inject it. Unit-tested in isolation. |
| **`BuildContext.ParsedArguments` setter removed** | `{ get; set; }` becomes `{ get; init; }` (or `{ get; }`) — invocation state is immutable post-construction. |
| **No test validation regression** | Every existing scenario maps to a destination, or is documented as DESIGNED OUT with explicit design-change rationale. See §4. |

---

## 1. Foundational principles (carry forward from plan v3 + new)

**Carries forward from plan v3 §0:**

- Stage tasks (PreFlight, Package, ConsumerSmoke, PublishStaging) read versions from `--versions-file` only. `--explicit-version` and `--explicit-versions` are ResolveVersions-only inputs. Stage tasks fail loud at task entry when `FamilyVersionMapping` is empty.
- Each task validates its own options (§0.1 — repo-wide rule). System.CommandLine cannot enforce per-target requiredness given flat root + Cake dispatch.
- CLI option organization mirrors consumer boundary (§0.2) — option file names state who consumes them.

**New for plan v4:**

- **Configuration record pattern is retired.** Tasks read `BuildContext.ParsedArguments` directly. Configuration records and `Configurations` aggregate slots are removed feature-by-feature as features are re-touched. Versioning is the first; the rest follow when needed (no big-bang migration).
- **One task per version source.** When a single task would dispatch on a CLI-supplied "mode" flag with substantively different code paths, prefer splitting into named tasks. Task name encodes the path; CLI flag goes away. Applies beyond versioning.
- **Provider/Pipeline classes are not free.** When a provider/pipeline is a thin wrapper around inlinable logic, inline it. Domain helpers stay as `internal static` methods on the task. ADR-004 §2.4 size threshold (~200 LOC) is the smell line for re-extraction.

---

## 2. File delta

```
Features/Versioning/
├── ExplicitVersionParser.cs              ← STAYS
├── VersionsJsonWriter.cs                 ← NEW (DI service)
├── ResolveVersionsFromManifestTask.cs    ← NEW
├── ResolveVersionsFromExplicitTask.cs    ← NEW
├── ServiceCollectionExtensions.cs        ← UPDATE (writer registration only)
├── ResolveVersionsPipeline.cs            ← DELETE
├── ResolveVersionsTask.cs                ← DELETE
├── IPackageVersionProvider.cs            ← DELETE
├── ManifestVersionProvider.cs            ← DELETE
└── ExplicitVersionProvider.cs            ← DELETE

Host/Configuration/
├── VersioningConfiguration.cs            ← DELETE
└── Configurations.cs                     ← UPDATE (Versioning slot removed; 6→5 fields)

Host/Cli/Options/
├── ResolveVersionsFromManifestOptions.cs ← NEW (VersionSuffixOption, VersionScopeOption)
├── ResolveVersionsFromExplicitOptions.cs ← NEW (ExplicitVersionOption, ExplicitVersionsOption)
├── StageVersionsOptions.cs               ← STAYS
└── ResolveVersionsOptions.cs             ← DELETE (replaced by the two task-specific files)

Host/
└── BuildContext.cs                       ← UPDATE (ParsedArguments setter → init/readonly)

Program.cs:
- Drop VersionSourceOption registration
- Drop VersioningConfiguration construction
- Drop --explicit-version ↔ --explicit-versions mutex (moves to explicit task)
- ParsedArguments record loses VersionSource field
- Register the two split option files

tools.cs:
- ci-sim ResolveVersions step → --target ResolveVersionsFromManifest --suffix=... --scope=... (drop --version-source=manifest)
- setup --source=local ResolveVersions step → same shape

.github/workflows/release.yml:
- resolve-versions/manifest-derived step → --target ResolveVersionsFromManifest --suffix=...
- resolve-versions/explicit step → --target ResolveVersionsFromExplicit --explicit-versions "..."
- Drop --version-source=... from both steps
```

**Net: 5 files DELETE, 4 files NEW, 5 files UPDATE.** (`Host/Cli/Options/ResolveVersionsOptions.cs` is one of the deletes; both new option files replace it.)

---

## 3. Phase-by-phase implementation

### Phase 1 — ParsedArguments + BuildContext cleanup

| Step | File | Action |
| --- | --- | --- |
| 1.1 | `Program.cs` | Remove `VersionSource` from `ParsedArguments` positional record. Remove `VersionSourceOption` registration on root. |
| 1.2 | All `ParsedArguments` callsites | `ProgramCompositionRootTests.CreateParsedArguments`, `PathConstructionTests`, `FakeRepoBuilder` — drop `VersionSource: null` arg. |
| 1.3 | `Host/BuildContext.cs` | `ParsedArguments` property: `{ get; set; }` → `{ get; init; }` or `{ get; }`. |

### Phase 2 — Option file split

| Step | File | Action |
| --- | --- | --- |
| 2.1 | `Host/Cli/Options/ResolveVersionsFromManifestOptions.cs` | NEW. Holds `VersionSuffixOption`, `VersionScopeOption`. Class-level XML doc: "Options consumed only by `--target ResolveVersionsFromManifest`. Sister file: `ResolveVersionsFromExplicitOptions.cs`." |
| 2.2 | `Host/Cli/Options/ResolveVersionsFromExplicitOptions.cs` | NEW. Holds `ExplicitVersionOption`, `ExplicitVersionsOption`. Class-level XML doc: similar scope marker. |
| 2.3 | `Host/Cli/Options/ResolveVersionsOptions.cs` | DELETE (`VersionSourceOption` was its 5th option; gone with this delete). |
| 2.4 | `Program.cs` root option registration | Replace 5× `ResolveVersionsOptions.*` with: <br>`root.AddOption(ResolveVersionsFromManifestOptions.VersionSuffixOption);` <br>`root.AddOption(ResolveVersionsFromManifestOptions.VersionScopeOption);` <br>`root.AddOption(ResolveVersionsFromExplicitOptions.ExplicitVersionOption);` <br>`root.AddOption(ResolveVersionsFromExplicitOptions.ExplicitVersionsOption);` |

### Phase 3 — `VersionsJsonWriter` extraction

| Step | File | Action |
| --- | --- | --- |
| 3.1 | `Features/Versioning/VersionsJsonWriter.cs` | NEW. `internal sealed class VersionsJsonWriter(ICakeContext, IPathService, ICakeLog)`. Single method `Task WriteAsync(IReadOnlyDictionary<string, NuGetVersion> mapping, CancellationToken ct)` — writes sorted, NuGet-normalized JSON to `IPathService.GetResolveVersionsOutputFile()` and emits the existing two log lines. |

### Phase 4 — `ResolveVersionsFromManifestTask`

| Step | File | Action |
| --- | --- | --- |
| 4.1 | `Features/Versioning/ResolveVersionsFromManifestTask.cs` | NEW. `[TaskName("ResolveVersionsFromManifest")]`. Ctor: `(ManifestConfig manifest, VersionsJsonWriter writer)`. `RunAsync(BuildContext)`: <ol><li>Validate `context.ParsedArguments.Suffix` not null/whitespace → `CakeException` with actionable message</li><li>Build scope from `context.ParsedArguments.Scope` (empty=all)</li><li>Inline `BuildVersionFor` + `NormalizeSuffix` as `internal static` private helpers (pure functions)</li><li>Compose mapping per family</li><li>`await writer.WriteAsync(mapping, ct: default)`</li></ol>Logic ports verbatim from `ManifestVersionProvider.cs` lines 35-139. |

### Phase 5 — `ResolveVersionsFromExplicitTask`

| Step | File | Action |
| --- | --- | --- |
| 5.1 | `Features/Versioning/ResolveVersionsFromExplicitTask.cs` | NEW. `[TaskName("ResolveVersionsFromExplicit")]`. Ctor: `(ManifestConfig manifest, IUpstreamVersionAlignmentValidator validator, VersionsJsonWriter writer)`. `RunAsync(BuildContext)`: <ol><li>Read `context.ParsedArguments.{ExplicitVersion, ExplicitVersions}`</li><li>Mutex check (both present → `CakeException`)</li><li>Empty check (neither present → `CakeException`)</li><li>Parse via `ExplicitVersionParser.ParseCliEntries` or `ParseCommaSeparated`</li><li>Inline `NormalizeMapping` as `internal static` helper (case-insensitive dedup)</li><li>G54 alignment via injected validator (logic from `ExplicitVersionProvider.cs` lines 56-71)</li><li>`await writer.WriteAsync(mapping, ct: default)`</li></ol>**No `--scope` consumption** — operator input mapping is the scope. |

### Phase 6 — `Versioning/ServiceCollectionExtensions` update

| Step | File | Action |
| --- | --- | --- |
| 6.1 | `Features/Versioning/ServiceCollectionExtensions.cs` | Update: <ul><li>**Drop** `IPackageVersionProvider` registration (interface gone)</li><li>**Drop** `ResolveVersionsPipeline` registration (class gone)</li><li>**Add** `services.AddSingleton<VersionsJsonWriter>();`</li><li>Comment rewritten to describe the new shape: "Two task-per-source pattern. Task classes self-register via Cake.Frosting `[TaskName]` attribute discovery; only the writer needs DI registration here."</li></ul> |

### Phase 7 — Dead code deletion

| Step | File | Action |
| --- | --- | --- |
| 7.1 | `Features/Versioning/ResolveVersionsPipeline.cs` | DELETE |
| 7.2 | `Features/Versioning/ResolveVersionsTask.cs` | DELETE |
| 7.3 | `Features/Versioning/IPackageVersionProvider.cs` | DELETE |
| 7.4 | `Features/Versioning/ManifestVersionProvider.cs` | DELETE (logic inlined in Phase 4) |
| 7.5 | `Features/Versioning/ExplicitVersionProvider.cs` | DELETE (logic inlined in Phase 5) |
| 7.6 | `Host/Configuration/VersioningConfiguration.cs` | DELETE |

### Phase 8 — `Configurations` aggregate slot removal

| Step | File | Action |
| --- | --- | --- |
| 8.1 | `Host/Configuration/Configurations.cs` | Remove `VersioningConfiguration Versioning` from positional record (6 fields → 5). |
| 8.2 | All `new Configurations(...)` callsites | `Program.cs` `BuildOptions` aggregate construction, `FakeRepoBuilder.cs` `BuildOptions` construction — drop the `Versioning:` arg. |
| 8.3 | All `Configurations.Versioning` reads | Search & verify zero remaining consumers. (Should be zero after Phase 4-7.) |

### Phase 9 — Program.cs cleanup

| Step | File | Action |
| --- | --- | --- |
| 9.1 | `Program.cs` `ConfigureBuildServices` | Drop `services.AddSingleton(new VersioningConfiguration(...))` line. Drop `--explicit-version` ↔ `--explicit-versions` mutex (moves to explicit task in Phase 5). Composition root becomes thinner: option registration + DI wiring only, no validation. |

### Phase 10 — Test transformation (no validation regression)

The discipline: every existing test scenario gets a destination or a documented `DESIGNED OUT` entry. See §4 for the full matrix.

| Step | File | Action |
| --- | --- | --- |
| 10.1 | `build/_build.Tests/Unit/Features/Versioning/ResolveVersionsFromManifestTaskTests.cs` | NEW. Ports 4 scenarios from `ManifestVersionProviderTests` + 1 from `ResolveVersionsPipelineTests` (manifest no-suffix). Integration-style with FakeRepoBuilder + stub `ParsedArguments`. |
| 10.2 | `build/_build.Tests/Unit/Features/Versioning/ResolveVersionsFromExplicitTaskTests.cs` | NEW. Ports 3 scenarios from `ExplicitVersionProviderTests` (full mapping happy path, G54 reject, empty mapping reject) + adds NEW: mutex check, comma-separated parser smoke, parser propagation. |
| 10.3 | `build/_build.Tests/Unit/Features/Versioning/VersionsJsonWriterTests.cs` | NEW. Unit tests: sorted keys, NuGet-normalized strings, output dir creation, log line emission. |
| 10.4 | `build/_build.Tests/Unit/Features/Versioning/ManifestVersionProviderTests.cs` | DELETE (scenarios moved to 10.1). |
| 10.5 | `build/_build.Tests/Unit/Features/Versioning/ExplicitVersionProviderTests.cs` | DELETE (scenarios moved to 10.2). |
| 10.6 | `build/_build.Tests/Unit/Features/Versioning/ResolveVersionsPipelineTests.cs` | DELETE (scenarios moved to 10.1/10.2 or DESIGNED OUT). |
| 10.7 | `build/_build.Tests/Unit/Features/Versioning/ExplicitVersionParserTests.cs` | STAYS unchanged (16 tests cover parser surface). |

### Phase 11 — tools.cs + release.yml mechanical update

| Step | File | Action |
| --- | --- | --- |
| 11.1 | `tools.cs` `RunLocalAsync` (line ~151) | Replace `["--target", "ResolveVersions", "--version-source=manifest", $"--suffix={suffix}"]` with `["--target", "ResolveVersionsFromManifest", $"--suffix={suffix}"]`. Drop `--version-source` flag. |
| 11.2 | `tools.cs` `CiSimCommand.ExecuteAsync` (line ~466) | Same replacement. |
| 11.3 | `.github/workflows/release.yml` `resolve-versions` job, manifest step | `--target ResolveVersions --version-source=manifest --suffix=...` → `--target ResolveVersionsFromManifest --suffix=...`. |
| 11.4 | `.github/workflows/release.yml` `resolve-versions` job, explicit step | `--target ResolveVersions --version-source=explicit --explicit-versions "..."` → `--target ResolveVersionsFromExplicit --explicit-versions "..."`. |

### Phase 12 — Build & verify

| Step | Action |
| --- | --- |
| 12.1 | `dotnet build build/_build/Build.csproj` — no compile errors |
| 12.2 | `dotnet test --project build/_build.Tests/Build.Tests.csproj -c Release` — every transformed test scenario passes; new tests pass; total count grows by ~6-8 (new mutex + parser propagation + writer unit tests minus DESIGNED OUT count) |
| 12.3 | `dotnet run --file tools.cs -- ci-sim` — full local pipeline rehearsal exercises both new tasks |
| 12.4 | Manual `dotnet run --project build/_build -- --help` — verify `--version-source` is gone from help output; the two task names appear under `--target` discovery |

---

## 4. Test scenario preservation matrix

**Discipline: zero validation regression. Each existing scenario maps to a destination, or is `DESIGNED OUT` with explicit rationale tied to a v4 design change.**

### 4.1 `ManifestVersionProviderTests.cs` (4 scenarios)

| # | Existing scenario | Destination |
| --- | --- | --- |
| 1 | `ResolveAsync_Should_Compose_UpstreamMajorMinor_Zero_Suffix_Per_Family` | `ResolveVersionsFromManifestTaskTests.RunAsync_Should_Write_Versions_With_UpstreamMajorMinor_Zero_Suffix_Per_Family` — assert via versions.json content |
| 2 | `ResolveAsync_Should_Return_Only_Requested_Families_When_Scope_Is_Subset` | `ResolveVersionsFromManifestTaskTests.RunAsync_Should_Write_Only_Scoped_Families_When_Scope_Is_Subset` |
| 3 | `ResolveAsync_Should_Throw_When_Requested_Family_Is_Missing_From_Manifest` | `ResolveVersionsFromManifestTaskTests.RunAsync_Should_Throw_When_Scoped_Family_Is_Missing_From_Manifest` |
| 4 | `ResolveAsync_Should_Throw_When_Suffix_Produces_Invalid_NuGet_SemVer` | `ResolveVersionsFromManifestTaskTests.RunAsync_Should_Throw_When_Suffix_Produces_Invalid_NuGet_SemVer` |

**Coverage: 4/4 ported.**

### 4.2 `ExplicitVersionProviderTests.cs` (5 scenarios)

| # | Existing scenario | Destination |
| --- | --- | --- |
| 1 | `ResolveAsync_Should_Return_Full_Mapping_When_RequestedScope_Is_Empty` | `ResolveVersionsFromExplicitTaskTests.RunAsync_Should_Write_Full_Mapping_From_Operator_Input` |
| 2 | `ResolveAsync_Should_Return_Filtered_Mapping_When_RequestedScope_Is_Subset` | **DESIGNED OUT.** `--scope` removed from explicit mode (v4 §0). Operator input mapping IS the scope; there is no separate filter axis to test. The capability exists as "operator passes only the families they want to resolve" — covered structurally by happy-path test #1. |
| 3 | `ResolveAsync_Should_Reject_Mapping_With_Invalid_Major_Version` | `ResolveVersionsFromExplicitTaskTests.RunAsync_Should_Throw_When_Operator_Mapping_Fails_G54_Alignment` |
| 4 | `ResolveAsync_Should_Throw_When_Constructed_With_Empty_Mapping` | `ResolveVersionsFromExplicitTaskTests.RunAsync_Should_Throw_When_No_ExplicitVersion_Or_ExplicitVersions_Supplied` |
| 5 | `ResolveAsync_Should_Throw_When_RequestedScope_Asks_For_Missing_Family` | **DESIGNED OUT.** Same rationale as #2 — `--scope` parameter removed from the explicit task. The "missing family" scenario is now covered indirectly: operator passes a family unknown to manifest → G54 validator rejects (test #3). |

**Coverage: 3/5 ported, 2/5 designed out (both --scope-related, deliberate per v4 §0).**

### 4.3 `ResolveVersionsPipelineTests.cs` (8 scenarios)

| # | Existing scenario | Destination |
| --- | --- | --- |
| 1 | `RunAsync_Should_Write_Canonical_Json_For_Manifest_Source_Happy_Path` | Covered by §4.1 #1 (`RunAsync_Should_Write_Versions_With_UpstreamMajorMinor_Zero_Suffix_Per_Family`) — same scenario, same JSON-content assertion |
| 2 | `RunAsync_Should_Honor_Scope_Filter_When_Supplied` (manifest mode) | Covered by §4.1 #2 |
| 3 | `RunAsync_Should_Write_Canonical_Json_For_Explicit_Source_Happy_Path` | Covered by §4.2 #1 |
| 4 | `RunAsync_Should_Honor_Scope_Filter_For_Explicit_Source_When_Supplied` | **DESIGNED OUT.** `--scope` removed from explicit task (v4 §0). |
| 5 | `RunAsync_Should_Throw_When_Explicit_Source_Has_No_ExplicitVersions` | Covered by §4.2 #4 |
| 6 | `RunAsync_Should_Throw_When_VersionSource_Is_Missing` | **DESIGNED OUT.** `--version-source` flag removed entirely (v4 §0). The "missing" scenario is replaced by "operator invoked the wrong target" — Cake's own dispatcher handles unknown task names. |
| 7 | `RunAsync_Should_Throw_When_Manifest_Source_Has_No_Suffix` | `ResolveVersionsFromManifestTaskTests.RunAsync_Should_Throw_When_Suffix_Is_Missing` |
| 8 | `RunAsync_Should_Throw_For_Unknown_Source` | **DESIGNED OUT.** Source dispatch removed (task-per-source replaces switch-on-flag). |

**Coverage: 5/8 ported (3 dedup with provider tests + 2 unique), 3/8 designed out (all --version-source-related).**

### 4.4 New scenarios introduced in v4 (not in v3 test surface)

| # | Scenario | Test file | Rationale |
| --- | --- | --- | --- |
| N1 | `RunAsync_Should_Throw_When_Both_ExplicitVersion_And_ExplicitVersions_Supplied` | `ResolveVersionsFromExplicitTaskTests` | Mutex check moves from Program.cs to the task; needs task-level coverage |
| N2 | `RunAsync_Should_Parse_ExplicitVersions_Comma_Separated_Form` | `ResolveVersionsFromExplicitTaskTests` | Smoke test that the comma-separated path reaches versions.json (parser-level coverage already exists in `ExplicitVersionParserTests`; this is integration-level wiring proof) |
| N3 | `RunAsync_Should_Throw_When_ExplicitVersions_Comma_Form_Has_Malformed_Entry` | `ResolveVersionsFromExplicitTaskTests` | Verifies `ArgumentException` from parser propagates as `CakeException` at task entry |
| N4 | `WriteAsync_Should_Sort_Keys_OrdinalIgnoreCase` | `VersionsJsonWriterTests` | Writer extracted; needs unit coverage |
| N5 | `WriteAsync_Should_Use_NuGet_Normalized_Version_Strings` | `VersionsJsonWriterTests` | Same |
| N6 | `WriteAsync_Should_Create_Output_Directory_When_Missing` | `VersionsJsonWriterTests` | Same |
| N7 | `WriteAsync_Should_Log_Line_Count_And_Output_Path` | `VersionsJsonWriterTests` | Same |

**Net new: 7 scenarios.**

### 4.5 Summary

- **Existing scenarios:** 17 across 3 files
- **Ported (1:1 destination):** 9 scenarios (4 manifest + 3 explicit + 5 from pipeline tests minus 3 dedups = 9 unique surviving scenarios)
- **DESIGNED OUT:** 5 scenarios (4× --scope-from-explicit + 3× --version-source-flag = 5 unique design-driven removals; some tested same axis from different test files)
- **New:** 7 scenarios
- **Final test count delta:** 17 deleted (3 files) + (9 + 7) = 16 new across 3 new files; net **-1 test method** but **zero validation regression** — every retained behavior has equal or stronger coverage, and every removed scenario is tied to an explicit v4 design decision.

---

## 5. Implementation strategy notes

### 5.1 Test ergonomics — `internal` exposure vs pure integration

Two options for how task tests reach internal logic:

| Approach | Mechanism | Tradeoff |
| --- | --- | --- |
| **(a) Integration-only** | Tests construct `BuildContext` with stub `ParsedArguments` via `FakeRepoBuilder`, invoke `task.RunAsync(context)`, assert on `versions.json` content + thrown exceptions | Tests behavior, not implementation. Slightly chunkier per test (FakeRepoBuilder setup) but stable across refactors. |
| **(b) `internal` static + `InternalsVisibleTo`** | Task class has `internal static` helpers (`BuildVersionFor`, `NormalizeSuffix`, etc.); tests call directly. The `[InternalsVisibleTo("Build.Tests")]` attribute already exists on `Build.csproj` (no setup needed). | Tests private logic. Less integration setup but couples tests to refactor-fragile internals. |

**Recommendation: (a) for the new tests**, with one exception: if a single helper function has high test density (e.g., `NormalizeSuffix` would have 6+ pure parser-style tests), expose it `internal static` and unit-test it directly to avoid duplicating FakeRepoBuilder setup. Pragmatic split.

The user's "transformation, not loss" principle is satisfied either way — every existing scenario gets a test method invoking the equivalent code path.

### 5.2 `IUpstreamVersionAlignmentValidator` consumer note

After Phase 7 deletes `ExplicitVersionProvider`, the only remaining consumer of `IUpstreamVersionAlignmentValidator` is `PreflightPipeline` (G54 in PreFlight) and the new `ResolveVersionsFromExplicitTask` (G54 in explicit resolution). The interface stays — both consumers genuinely need the abstraction.

### 5.3 Cancellation token

`AsyncFrostingTask<BuildContext>.RunAsync(BuildContext)` does not expose a `CancellationToken`. Both new tasks will pass `CancellationToken.None` to `VersionsJsonWriter.WriteAsync`. This matches the existing `ResolveVersionsTask.RunAsync` pattern (no ct propagation). Acceptable for this refactor; future Cake.Frosting upgrade may surface a ct.

---

## 6. Files NOT changed (no impact)

- `Features/Packaging/*` — stage tasks already migrated in v3 to read `FamilyVersionMapping`; no further changes needed.
- `Features/Preflight/*` — same.
- `Features/Publishing/*` — same.
- All other features (`Harvesting`, `Coverage`, `Vcpkg`, `Info`, `Ci`, `Diagnostics`, `DependencyAnalysis`, `Maintenance`) — Configuration records stay until a future session re-touches them.
- `Shared/*` — domain primitives untouched.
- `Tools/*`, `Integrations/*` — external adapters untouched.
- `Host/Configuration/{Vcpkg,Repository,DotNet,Dumpbin,PackageBuild}Configuration.cs` — stay (only Versioning Configuration is deleted in this session).
- `Directory.Packages.props`, csproj files, `release.yml` outside the `resolve-versions` job, all docs — untouched by this plan (orthogonal to versioning split).

---

## 7. Open follow-ups (out of scope)

- **Configuration pattern retirement for other features** — Vcpkg, Repository, DotNet, Dumpbin, PackageBuild configurations follow the same pattern as the deleted `VersioningConfiguration`. Each can be retired when its feature is re-touched. No mass migration; smell-triggered.
- **`Configurations` aggregate retirement** — once all 5 remaining slots are dead (each feature migrated to direct `ParsedArguments` reads), the aggregate is deleted and `BuildContext.Options` removed. Estimated 5 sessions out.
- **`BuildContext.Manifest` reuse** — currently `BuildContext.Manifest` exposes `ManifestConfig`. Tasks reach for it via `context.Manifest`, but the new tasks inject `ManifestConfig` via DI. Inconsistency to resolve in a future session: pick one access pattern (DI injection vs context field) and apply uniformly.
- **`Cake.Frosting` cancellation token** — if a future Cake.Frosting upgrade exposes `RunAsync(BuildContext, CancellationToken)`, propagate the ct through `VersionsJsonWriter.WriteAsync` and resolver loops.
- **Plan v3 leftovers** — M6 partial fix (CsprojPackContractValidator `<remarks>` block), P3 missing fail-loud tests for PackageTask + PublishStaging, P2 documentation rewrites (release-guardrails G4 row, ADR-001 §2.3, CLAUDE.md "Pipeline targets", playbook updates). These are independent of v4 and can ship in a separate cleanup PR.

---

## 8. Approval gate

Per [AGENTS.md](../../AGENTS.md), changes to feature code, the build system, and `.csproj` / `Program.cs` require explicit go-ahead. This plan is the proposal artifact. Implementation begins on operator's "go / apply / proceed / başla / yap".
