# Phase X — Build-Host Modernization (ADR-004 Migration)

- **Date:** 2026-05-02
- **Status:** PROPOSED — wave roadmap drafted; P0 Safety Baseline pending kickoff
- **Author:** Deniz İrgin (@denizirgin) + 2026-05-01 collaborative critique synthesis
- **Governing ADR:** [ADR-004 — Cake-Native Feature-Oriented Build-Host Architecture](../decisions/2026-05-02-cake-native-feature-architecture.md)
- **Supersedes:** No prior plan
- **Standalone phase:** This is a cross-cutting build-host refactor wave. It is **not tied to Phase 2 / Phase 2b / Phase 3** roadmap items. Individual P-stages (P0–P5) are scoped, sequenced, and gated independently and may run between or around Phase 2b release work.

---

## 1. Purpose and Scope

### 1.1 Why this plan exists

[ADR-004](../decisions/2026-05-02-cake-native-feature-architecture.md) supersedes [ADR-002](../decisions/2026-04-19-ddd-layering-build-host.md) and locks the target shape: `Host / Features / Shared / Tools / Integrations`, `BuildContext` discipline, size-triggered Pipelines, `Features/LocalDev/` as the designated orchestration feature, `Shared/Results/` admission criteria, `Tools/` restricted to Cake `Tool<TSettings>` wrappers. ADR-004 §6 enumerates six implementation phases (P0–P5) but defers the wave-by-wave sequencing, success criteria, smoke-witness baseline format, test migration ordering, and atomic-commit boundaries to this plan.

### 1.2 What is in scope

- Migration of `build/_build/` from the live ADR-002 layered shape to the ADR-004 feature-oriented shape.
- Migration of `build/_build.Tests/` test folders to mirror the new production shape.
- Renaming of `LayerDependencyTests.cs` to `ArchitectureTests.cs` with an invariant rewrite around the ADR-004 5-folder shape.
- Cake target naming cleanup (`PreFlightCheck` → `Preflight`, `Coverage-Check` → `CoverageCheck`, `Inspect-HarvestedDependencies` → `InspectHarvestedDependencies`).
- Atomic same-commit updates of `release.yml`, `tests/scripts/smoke-witness.cs`, `docs/playbook/cross-platform-smoke-validation.md`, and any target-name references throughout live docs at the rename wave.
- Retirement of `UnsupportedArtifactSourceResolver` (CLI parse-time validation replaces runtime failure).
- Pipeline `RunAsync(BuildContext, TRequest)` → `RunAsync(TRequest)` cut-over (closes ADR-004 §2.11.1 migration exception).
- Interface review wave applying ADR-004 §2.9 three-criteria rule to surviving `I*` types.
- `IPathService` fluent split into semantic path groups (`BuildPaths.Harvest`, `.Packages`, `.Smoke`, etc.).

### 1.3 What is explicitly out of scope

- Cake target dependency mapping (`IsDependentOn`/`IsDependeeOf`) semantics — unchanged.
- ADR-001 D-3seg versioning, package-first consumer contract, ArtifactProfile semantics — unchanged.
- ADR-003 release lifecycle invariants (provider/scope/version axes, stage-owned validation, matrix re-entry, G54/G58 placement) — unchanged.
- `manifest.json` schema v2.1 — no contract changes.
- `release.yml` 10-job topology — only target-name updates from §2.14, no job re-shuffle.
- Pack guardrails (G14/G15/G16/G46/G54/G58) — preserved verbatim, relocated to feature folders.
- `external/sdl2-cs` and any submodule — untouched.
- Public CLI semantics — unchanged, **except for** ADR-004 §2.14 target-name normalization (`PreFlightCheck` → `Preflight`, `Coverage-Check` → `CoverageCheck`, `Inspect-HarvestedDependencies` → `InspectHarvestedDependencies`) and §2.15 `--source` narrowing (`release` / `release-public` retired until Phase 2b PD-7). Both shifts are explicit ADR-004 deliverables, landed atomically with their callsite updates in P5.

### 1.4 Why a standalone phase

Phase 2 / 2b are release-pipeline tracks (CI/CD, Publishing, RemoteArtifactSourceResolver tail). This refactor is structural — it touches every feature folder but should not change observable behavior. Mixing this migration into a Phase 2b commit train would produce unreviewable diffs and increase the risk of behavior regressions hiding inside structural changes.

### 1.5 Mechanical vs structural waves

This refactor is **not** a folder-renaming pass. Some waves are mechanical by design; others are real code refactor that change class shape, signatures, DI graph, and mental model. The wave character matters because it sets review expectations and rollback granularity:

| Wave | Character | What changes in production code |
| --- | --- | --- |
| **P0** | Mechanical (additive) | smoke-witness gains an opt-in `--emit-baseline` flag. Production code untouched. |
| **P1** | **Mechanical (deliberately narrow)** | `git mv` + namespace + `using` adjustments only. Class internals, signatures, behavior unchanged. The narrow scope is **intentional**: 200+ files moving in one wave should not also carry behavior shifts — when something breaks, root cause is "the move" and nothing else. |
| **P2** | **Structural (real refactor)** | Class renames (`*TaskRunner` → `*Pipeline`, `SetupLocalDevTaskRunner` → `SetupLocalDevFlow`); per-feature `ServiceCollectionExtensions.cs` is **written**; `Program.cs` DI chain collapses to `services.Add*Feature()` calls; `BuildOptions` aggregate record is **written**; `BuildContext` slims from 7 properties + 6 sub-configs to 4 properties (`Paths`, `Runtime`, `Manifest`, `Options`); `ManifestConfig` moves onto `BuildContext` as data; **`new XValidator()` calls inside orchestrators replace with constructor injection** (ADR-004 §1.1.6 anti-pattern fix); `LayerDependencyTests.cs` is **renamed and rewritten** to `ArchitectureTests.cs` with the 5-invariant set (ADR-004 §2.13). |
| **P3** | **Structural (interface seam + test rewrite)** | Interface seams reviewed against ADR-004 §2.9 — kept or removed; constructor parameter types switch from `IFoo` to `Foo` for removals; mock-based unit tests rewrite as fixture-based concrete tests, integration tests under `Integration/<Scenario>/`, or §2.9.1 delegate-hook patterns; ~20–30 test methods rewritten across the wave. |
| **P4** | **Structural (largest signature evolution)** | Pipeline `RunAsync(BuildContext, TRequest, CT)` cuts over to `RunAsync(TRequest, CT)`; Cake Tasks gain `Request.From(context, config)` factory call sites; Pipeline constructors move from `BuildContext` per-call to narrow Cake abstractions (`ICakeLog`, `BuildPaths`) via DI; pure services (`PackageOutputValidator`, `G58CrossFamilyValidator`, etc.) drop `BuildContext` parameters in favor of explicit inputs; `IPathService` flat 50+-member interface splits into `BuildPaths.Harvest`, `.Packages`, `.Smoke`, `.Vcpkg`, etc. — **hundreds of callsites rewrite** (`paths.GetHarvestStageNativeDir(lib, rid)` → `paths.Harvest.GetStageNativeDir(lib, rid)`); optional internal refactor of large Pipelines (PackageConsumerSmoke 688 LOC, Harvest 628, Package 556) into smaller per-concern co-located helpers. |
| **P5** | Mechanical (atomic) | `[TaskName]` attribute string changes + smoke-witness step labels + `release.yml` `--target` references + `cross-platform-smoke-validation.md` A-K script + live-doc target-name mentions, **all in one commit per rename** per §9.3 ordering; `UnsupportedArtifactSourceResolver` retired with CLI parse-time validation replacing runtime failure. Behavior unchanged. |

**The substantive transformation is P2–P4.** P2 changes the mental model: from ADR-002's layered Application/Domain/Infrastructure shape with a half-service-locator `BuildContext` to ADR-004's "Features own behavior, BuildContext is invocation state, DI registers capabilities" model. P3 enforces the interface discipline. P4 closes the signature evolution — Pipelines become pure Request consumers; pure services take explicit inputs.

P1 and P5 are bracketed by design: mechanical, narrow-scoped, easy to review at the file-move and target-rename level respectively. They surround the real work, not replace it.

---

## 2. North Stars

Three invariants govern the migration. Every wave commit must keep all three green.

### 2.1 smoke-witness behavior signal

[`tests/scripts/smoke-witness.cs`](../../tests/scripts/smoke-witness.cs) is the **black-box behavior contract** for the build host. It exercises Cake targets through `dotnet run` from outside the host, in three modes:

- **`local`** — `CleanArtifacts → SetupLocalDev (--source=local) → PackageConsumerSmoke`
- **`remote`** — `CleanArtifacts → SetupLocalDev (--source=remote) → PackageConsumerSmoke`
- **`ci-sim`** — full pipeline replay: `CleanArtifacts → ResolveVersions → PreFlightCheck → EnsureVcpkgDependencies → Harvest → NativeSmoke → ConsolidateHarvest → Package → PackageConsumerSmoke`

#### 2.1.1 Why log SHA is not deterministic

smoke-witness emits per-run timestamps (`runId`, `# finished=...` log footers, Cake's own progress output) into every step log. Hashing the full output produces a unique value every run. Pure-SHA baselines do not work.

#### 2.1.2 Behavior-signal baseline format

Behavior signal = the **ordered tuple** of `(step label, exit code)` pairs plus the run's mode and host RID. Concretely:

```json
{
  "mode": "local",
  "host_rid": "win-x64",
  "step_count": 3,
  "steps": [
    { "label": "CleanArtifacts",        "exit": 0 },
    { "label": "SetupLocalDev",         "exit": 0 },
    { "label": "PackageConsumerSmoke",  "exit": 0 }
  ],
  "passed": 3,
  "failed": 0
}
```

This signal is **deterministic** across runs at a given commit. Wave commits compare baseline-before vs baseline-after — strict equality is the green criterion. Step duration, log path, and log content are intentionally outside the signal.

#### 2.1.3 When the signal changes

Step labels and step ordering are the part that **may legitimately change** under this refactor:

- **P5 naming cleanup wave:** `PreFlightCheck` → `Preflight`, `Coverage-Check` → `CoverageCheck`, `Inspect-HarvestedDependencies` → `InspectHarvestedDependencies`. The baseline file is updated atomically with the rename (same commit) and the new baseline is the wave's exit signal.
- All other waves must **not** alter the signal. If a P1/P2/P3/P4 wave changes the step list or any exit code from the baseline, the migration is leaking behavior — wave is rejected, root-cause is found before re-attempting.

#### 2.1.4 P0 deliverable: `--emit-baseline` flag

`smoke-witness.cs` gains an opt-in `--emit-baseline <path>` flag in P0. When passed, the witness writes the §2.1.2 JSON to the given path after the run completes. This is the mechanical hook that wave commits use to capture before/after baselines without grep'ing console output.

### 2.2 ArchitectureTests

`build/_build.Tests/Unit/CompositionRoot/LayerDependencyTests.cs` runs every CI build today and asserts the ADR-002 layer-direction rule set. P2 wave renames the file to `ArchitectureTests.cs` via `git mv` and rewrites the invariants around the ADR-004 shape (5 invariants, see ADR-004 §2.13).

P0 → P1 keep `LayerDependencyTests` green unchanged. P2 closes with `ArchitectureTests` green. P3+ keep `ArchitectureTests` green.

### 2.3 Test count ratchet

`build/_build.Tests/` test count baselines per wave close:

| Wave close | Test count target (formula) |
| --- | --- |
| P0 baseline | 500 (current) |
| P1 close | ≥ P0 baseline (move-only; namespace updates do not change test count) |
| P2 close | ≥ P1 close + 5 `ArchitectureTests` invariants + one `ServiceCollectionExtensions` smoke per migrated feature (~13 features → expected ~+18 total) |
| P3 close | flexible per §7.4 — ratchet exception with explicit per-removed-test gerekçe in commit message |
| P4 close | ≥ P3 close (Pipeline `RunAsync(TRequest)` cut-over preserves test count; signatures change but methods do not) |
| P5 close | ≥ P4 close (rename-only) |

The formula is intentional: hard-coded post-P2 numbers go stale as features are added or merged. Wave-close gates compare against the **previous wave's actual count**, not against P0, except for P2 which adds a known set (5 architecture invariants + 1-per-feature smokes).

Test count drops outside the P3 window are not allowed — drops in P3 require commit-message gerekçe per removed test method (see §7.4). Drops in any other wave are wave-rejection signal.

---

## 3. Wave Sequencing

ADR-004 §6 enumerates six waves; this plan locks the sequence and inter-wave dependencies.

```text
P0 — Safety Baseline             [must complete before P1]
       │
       ▼
P1 — Folder Migration            [N feature waves; greenness per wave]
       │
       ▼
P2 — Terminology Migration       [Pipeline rename + ServiceCollectionExtensions per feature
                                  + ArchitectureTests rewrite — single conceptual wave,
                                  may land per-feature commits]
       │
       ▼
P3 — Interface Review            [§2.9 criteria applied; bounded test rewrite; per-interface]
       │
       ▼
P4 — API Surface Refactors       [Pipeline RunAsync(TRequest) cut-over,
                                  IPathService fluent split, large-Pipeline internal refactor]
       │
       ▼
P5 — Naming Cleanup + Atomic     [target rename atomic-wave commit:
                                  Cake host + smoke-witness + release.yml + docs all in one]
```

**Cross-wave invariants:**

- Every wave commit boundary: smoke-witness behavior signal unchanged (or P5 atomic update), `LayerDependencyTests`/`ArchitectureTests` green, test count ratchet honored.
- No half-migrated state crosses a commit boundary. A wave may produce multiple commits internally, but each commit is itself green and consistent.
- P0 must close before P1 starts. P2 must close before P3 starts (interface review depends on the new feature folder shape). P3 → P4 → P5 dependencies are looser but recommended ordering matches the table above.

---

## 4. P0 — Safety Baseline

### 4.1 Goals

Establish the verifiable starting point. Capture baselines, freeze the public Cake target surface, build the rename inventory, add the `--emit-baseline` hook to smoke-witness.

### 4.2 Deliverables

| Deliverable | Location | Form |
|---|---|---|
| smoke-witness `--emit-baseline` flag | `tests/scripts/smoke-witness.cs` | Single PR; flag is additive (no behavior change to existing modes) |
| Baseline `local` mode | `tests/scripts/baselines/smoke-witness-local-<host-rid>.json` (committed) | JSON behavior signal per §2.1.2; per-host file (e.g. `-win-x64.json`, `-linux-x64.json`, `-osx-arm64.json`) so different developers contribute their own host's baseline |
| Baseline `ci-sim` mode | `tests/scripts/baselines/smoke-witness-ci-sim-<host-rid>.json` (committed) | JSON behavior signal per §2.1.2; per-host file |
| Test count baseline | `tests/scripts/baselines/test-count.txt` (committed) | Plain integer; behavior contract belongs in VCS |
| Public Cake target surface freeze | `tests/scripts/baselines/cake-targets.txt` (committed) | `dotnet run -- --tree` output captured at P0 commit; target names that must survive untouched until P5 |
| Target rename inventory | This plan §9.2 (a table embedded below) | Each old name → new name + atomic-wave callsites |
| LayerDependencyTests baseline | n/a — currently passing; just a snapshot of the test names and invariants | For P2 reference when rewriting to `ArchitectureTests` |

### 4.3 P0 success criteria

- [ ] smoke-witness `--emit-baseline <path>` lands; backwards compatible (no flag → old behavior).
- [ ] `smoke-witness local --emit-baseline ...` runs green on Windows + WSL Linux; baseline JSON committed.
- [ ] `smoke-witness ci-sim --emit-baseline ...` runs green on at least Windows; baseline JSON committed.
- [ ] `dotnet test build/_build.Tests/Build.Tests.csproj -c Release` runs green; test count is the baseline number.
- [ ] `dotnet run --project build/_build -- --tree` output captured.
- [ ] Public target surface freeze list is committed; any target name outside this list during P1–P4 is a wave-rejection signal.
- [ ] Target rename inventory (§9.2) reviewed and approved; this plan revision number incremented.

### 4.4 P0 risks

- **smoke-witness flag implementation noise.** Adding `--emit-baseline` is a CLI change; could regress existing modes if not careful. Mitigation: flag is purely additive, the new code path triggers only when the flag is present, both modes are exercised pre-merge.
- **Baseline non-portability across hosts.** A Windows host produces `host_rid=win-x64`; Linux produces `linux-x64`. Mitigation: per-host baseline file naming (`smoke-witness-local-win-x64.json`, etc.), or single baseline with platform-conditional fields. P0 picks one — recommended: per-host files.

---

## 5. P1 — Folder Migration

### 5.1 Goals

Move every code file from the ADR-002 layered shape (`Application/`, `Domain/`, `Infrastructure/`, `Tasks/`, `Context/`) to the ADR-004 feature-oriented shape (`Host/`, `Features/`, `Shared/`, `Tools/`, `Integrations/`). **No behavior changes; no class renames; no interface pruning.** Pure `git mv` + namespace updates + `using` adjustments + `csproj` reference touch-ups if any.

### 5.2 Wave-by-feature ordering

P1 lands as a sequence of small, independently-green commits — one per feature folder migration. Recommended order from smallest to largest to let the pattern settle on simple cases before tackling Packaging:

| Wave | Production move | Test mirror move |
|---|---|---|
| 1.1 | `Tasks/Common/InfoTask.cs` → `Features/Info/` | `Unit/Tasks/Common/` → `Unit/Features/Info/` |
| 1.2 | `Tasks/Maintenance/`, `Application/Maintenance/` → `Features/Maintenance/` (CleanArtifacts + CompileSolution only; Coverage stays separate, see 1.4) | mirror |
| 1.3 | `Tasks/Ci/GenerateMatrixTask.cs`, `Application/Ci/` → `Features/Ci/` | mirror |
| 1.4 | `Tasks/Coverage/`, `Application/Coverage/`, `Domain/Coverage/` → `Features/Coverage/` | mirror |
| 1.5 | `Tasks/Versioning/`, `Application/Versioning/`, `Domain/Versioning/` → `Features/Versioning/` | mirror |
| 1.6 | `Tasks/Vcpkg/`, `Application/Vcpkg/` → `Features/Vcpkg/` | mirror |
| 1.7 | `Tasks/Diagnostics/`, `Application/Diagnostics/` → `Features/Diagnostics/` | mirror |
| 1.8 | `Tasks/Dependency/`, `Application/DependencyAnalysis/`, `Infrastructure/DependencyAnalysis/` → `Features/DependencyAnalysis/` (plus dependency-scanner Tools split) | mirror |
| 1.9 | `Tasks/Preflight/`, `Application/Preflight/`, `Domain/Preflight/` → `Features/Preflight/` | mirror |
| 1.10 | `Tasks/Harvest/`, `Application/Harvesting/`, `Domain/Harvesting/` → `Features/Harvesting/` | mirror |
| 1.11 | `Tasks/Publishing/`, `Application/Publishing/`, `Domain/Publishing/` → `Features/Publishing/` | mirror |
| 1.12 | `Tasks/Packaging/`, `Application/Packaging/` (excluding `SetupLocalDev*`), `Domain/Packaging/` → `Features/Packaging/` | mirror |
| 1.13 | `SetupLocalDevTask.cs` + `SetupLocalDevTaskRunner.cs` (P2 will rename to `SetupLocalDevFlow`) → `Features/LocalDev/`. **`ArtifactSourceResolvers/` stay in `Features/Packaging/`** per ADR-004 §2.3 — feed-prep is a Packaging concern; LocalDev consumes `IArtifactSourceResolver` / `ArtifactSourceResolverFactory` through the §2.13 invariant #4 orchestration allowlist. | new test folder `Unit/Features/LocalDev/` |
| 1.14 | `Domain/Strategy/`, `Domain/Runtime/`, `Domain/Paths/`, `Domain/Results/`, `Context/Models/`, etc. → `Shared/Strategy/`, `Shared/Runtime/`, `Shared/Manifest/`, `Shared/Results/` per ADR-004 §2.6 + §2.6.1 admission | mirror |
| 1.15 | `Context/BuildContext.cs`, `Context/CakeExtensions.cs`, `Context/Options/`, `Context/Configs/` → `Host/BuildContext.cs`, `Host/Cake/CakeExtensions.cs` (single file, **no split yet** — see P2 §6.2 deliverables for the Json/Platform/FileSystem split), `Host/Cli/Options/`, `Host/Configuration/` | mirror |
| 1.16 | `Infrastructure/Paths/PathService.cs` → `Host/Paths/PathService.cs` (single-file pass-through; fluent split is P4) | mirror |
| 1.17 | `Infrastructure/Tools/{Vcpkg,Dumpbin,Ldd,Otool,Tar,NativeSmoke,CMake}/` → `Tools/` (Cake `Tool<T>` wrappers) | mirror |
| 1.18 | `Infrastructure/Tools/Msvc/`, `Infrastructure/DotNet/`, `Infrastructure/Vcpkg/`, `Infrastructure/Coverage/` (non-Cake-Tool adapters) → `Integrations/Msvc/`, `Integrations/DotNet/`, `Integrations/Vcpkg/`, `Integrations/Coverage/` | mirror |

The order above is **recommended, not strict**. Constraints:

- Wave 1.13 must follow waves 1.9 + 1.10 + 1.12 (LocalDev references Preflight + Harvesting + Packaging pipelines).
- Wave 1.14 should precede 1.15 (BuildContext references `Shared/Manifest`, `Shared/Runtime`).
- Wave 1.17 + 1.18 must run before any feature wave that constructs Tool/Integration types directly (most do via DI, so timing is loose).

### 5.3 Per-wave checklist (P1)

For every wave 1.X commit:

- [ ] `git mv` source files; verify history preserved (`git log --follow`).
- [ ] Update namespaces in moved files: `Build.Domain.Packaging` → `Build.Features.Packaging` (or `Build.Shared.X` per the move table).
- [ ] Update all `using` statements in callers across the repo.
- [ ] Update `Program.cs` DI registrations if class location changed (no class rename yet — type names are unchanged).
- [ ] Update test mirror in the same commit: `git mv` test files, namespace updates, `using` adjustments.
- [ ] `dotnet build build/_build/Build.csproj -c Release` succeeds.
- [ ] `dotnet test build/_build.Tests/Build.Tests.csproj -c Release` succeeds; test count ≥ P0 baseline.
- [ ] `LayerDependencyTests` green (still ADR-002 invariants, but the ADR-004 destinations also satisfy ADR-002 partially — see §5.4).
- [ ] `smoke-witness local --emit-baseline tmp.json` matches P0 baseline JSON byte-for-byte.
- [ ] Commit message: `refactor(build-host): P1.X migrate <feature> to Features/<X>/ shape (ADR-004)`.

### 5.4 LayerDependencyTests invariants during P1

P1 commits keep `LayerDependencyTests.cs` (ADR-002 shape) running. Some moves may temporarily violate ADR-002 invariants (e.g., when `Domain/<X>/` content moves to `Features/<X>/`, ADR-002's "Domain has no outward dependencies" no longer applies because the namespace shifts). Two options:

**A — Tolerate transitional violations.** Add namespace exclusions to `LayerDependencyTests` per wave, removed at P2 close. Risk: noise.

**B — Rewrite invariants incrementally.** Each P1 wave updates `LayerDependencyTests` to recognize the moved namespace under both old and new locations.

**Decision: A.** P1 is meant to be mechanical move; rewriting invariants per wave inflates wave scope. Transitional namespace exclusions are documented, removed at P2 atomic rewrite. Each P1 commit's `LayerDependencyTests` exclusion list is one line in the commit message.

### 5.5 P1 success criteria (cumulative across waves)

- [ ] Every file in `build/_build/Application/`, `build/_build/Domain/`, `build/_build/Infrastructure/`, `build/_build/Tasks/`, `build/_build/Context/` has been moved to its ADR-004 destination.
- [ ] Empty source folders (`Application/`, `Domain/`, `Infrastructure/`, `Tasks/`, `Context/`) deleted.
- [ ] Test mirror moved completely; `Unit/Application/`, `Unit/Domain/`, `Unit/Infrastructure/`, `Unit/Tasks/`, `Unit/Context/` deleted.
- [ ] `dotnet test` green; test count ≥ P0 baseline.
- [ ] smoke-witness `local` + `ci-sim` baseline byte-equal to P0.
- [ ] Class names unchanged (no `*TaskRunner` → `*Pipeline` yet — that's P2).
- [ ] No interface pruning (P3 territory).
- [ ] No CLI target rename (P5 territory).

---

## 6. P2 — Terminology Migration

### 6.1 Goals

Apply ADR-004 vocabulary to class names, DI registrations, and architecture tests. Behavior unchanged; signatures may change.

### 6.2 Deliverables

- **`*TaskRunner` → `*Pipeline` rename** for every Application-layer orchestrator from P1. Examples: `PackageTaskRunner` → `PackagePipeline`, `HarvestTaskRunner` → `HarvestPipeline`, `PackageConsumerSmokeRunner` → `PackageConsumerSmokePipeline`, `ConsolidateHarvestTaskRunner` → `ConsolidateHarvestPipeline`, `NativeSmokeTaskRunner` → `NativeSmokePipeline`, etc.
- **`SetupLocalDevTaskRunner` → `SetupLocalDevFlow`** (one-off; this is the only Flow per ADR-004 §2.5).
- **`I*TaskRunner` interfaces preserved as-is during P2.** Renamed type's interface follows: `IPackageTaskRunner` → `IPackagePipeline`. Pruning is P3 territory.
- **`ServiceCollectionExtensions.cs` per feature.** Each `Features/<X>/` folder gains `ServiceCollectionExtensions.cs` exposing `AddXFeature(this IServiceCollection)`. `Program.cs` collapses long `services.AddSingleton<...>()` chains into the per-feature extension calls.
- **`BuildOptions` aggregate.** Per ADR-004 §2.11, `BuildContext.Options` exposes a single aggregate record with sub-records for each operator-input axis (`Vcpkg`, `Package`, `Versioning`, `Repository`, `DotNet`, `Diagnostics`).
- **`BuildContext` slimming.** Per ADR-004 §2.11, BuildContext exposes only `Paths`, `Runtime`, `Manifest`, `Options`. No service properties.
- **`CakeExtensions.cs` split by concern.** `Host/Cake/CakeExtensions.cs` (carried over single-file from P1.15) splits into `CakeJsonExtensions.cs`, `CakePlatformExtensions.cs`, `CakeFileSystemExtensions.cs` per ADR-004 §2.2 — prevents the kitchen-drawer pattern. Extension-method namespaces are unchanged so callsites do not move.
- **Sub-validator DI injection (ADR-004 §1.1.6 anti-pattern fix).** Orchestrators that currently construct sub-validators via `new XValidator()` switch to constructor injection. The canonical example is `PackageOutputValidator` (~1,038 LOC at the time of ADR-002 §1.1.3 audit), which composes `NativePackageMetadataValidator`, `ReadmeMappingTableValidator`, and `SatelliteUpperBoundValidator` by direct construction — paying the abstraction tax (the sub-validators have interfaces) without the DI / substitutability benefit. P2 registers each sub-validator in its feature's `ServiceCollectionExtensions` and switches the orchestrator to constructor injection. The same pattern applies to any other `new XValidator()` / `new XGenerator()` call that surfaces during the wave. This is the structural debt ADR-002 §1.1.3 flagged but the layered shape did not solve; ADR-004 §1.1.6 keeps it on the books, P2 closes it.
- **`LayerDependencyTests.cs` → `ArchitectureTests.cs`.** `git mv` + invariant rewrite to ADR-004 §2.13 5-invariant set. Atomic in the same commit; no parallel test files.

### 6.3 Per-class P2 checklist

For each `*TaskRunner` → `*Pipeline` rename:

- [ ] Rename file: `git mv PackageTaskRunner.cs PackagePipeline.cs`.
- [ ] Rename type and all references across the repo.
- [ ] Update interface (if kept): `IPackageTaskRunner` → `IPackagePipeline`.
- [ ] Update test class: `git mv PackageTaskRunnerTests.cs PackagePipelineTests.cs`; rename test class.
- [ ] Update DI registration in `Features/<X>/ServiceCollectionExtensions.cs`.
- [ ] Update Cake Task class (if it injected the renamed type): constructor parameter name and field name update.
- [ ] `dotnet build` + `dotnet test` green.

### 6.4 ArchitectureTests rewrite

Same commit as the last P1 → P2 transition (i.e., when all type renames are complete and only the test invariant rewrite is left):

- [ ] `git mv build/_build.Tests/Unit/CompositionRoot/LayerDependencyTests.cs build/_build.Tests/Unit/CompositionRoot/ArchitectureTests.cs`.
- [ ] Replace the three ADR-002 invariants with the five ADR-004 invariants (ADR-004 §2.13).
- [ ] Test method names follow the convention: `Shared_Should_Have_No_Outward_Or_Cake_Dependencies`, `Tools_Should_Have_No_Feature_Dependencies`, `Integrations_Should_Have_No_Feature_Dependencies`, `Features_Should_Not_Cross_Reference_Except_From_LocalDev`, `Host_Is_Free`.
- [ ] LocalDev exception is implemented as an explicit allowlist inside the invariant #4 test, not as a generic "orchestration features" abstraction. Adding a second orchestration feature requires editing the allowlist.
- [ ] `dotnet test` green; new invariants assert against the post-P1 + post-P2 production tree (which is the ADR-004 shape end-to-end).

### 6.5 P2 success criteria

- [ ] Every `*TaskRunner` class renamed to `*Pipeline` (or `*Flow` for SetupLocalDev).
- [ ] No `*Runner` suffix remains in `build/_build/` outside Cake `Tool<T>` triad files in `Tools/` (where `XRunner.cs` is the canonical Cake-native multi-command tool name).
- [ ] Each `Features/<X>/` folder has a `ServiceCollectionExtensions.cs` with `AddXFeature(this IServiceCollection)`.
- [ ] `Program.cs` composition root reads as the feature roster — one `Add*Feature()` line per feature folder.
- [ ] `BuildContext` exposes only `Paths`, `Runtime`, `Manifest`, `Options`. No service properties. No `GetService<T>()` calls in production code.
- [ ] `LayerDependencyTests.cs` deleted; `ArchitectureTests.cs` exists with 5 invariants per ADR-004 §2.13.
- [ ] Test count ≥ P1 close + 5 `ArchitectureTests` invariants + one `ServiceCollectionExtensions` smoke per migrated feature (per §2.3 / §10.4 formula; expected ~+18 with the current 13-feature roster).
- [ ] smoke-witness `local` + `ci-sim` baseline byte-equal to P0 (target names unchanged).
- [ ] `Shared/` no Cake dependency invariant — P1 transitional exception (ADR-004 §2.6) is closed; if `Shared/Runtime/RuntimeProfile` still holds Cake `PlatformFamily`, replace with build-host-local enum or vocabulary type in this wave.

---

## 7. P3 — Interface Review

### 7.1 Goals

Apply ADR-004 §2.9 three-criteria rule to every surviving `I*` interface. Remove interfaces that fail criteria; convert their tests to fixture-based or integration-level patterns. **Bounded scope per interface — no mass deletion.**

### 7.2 Review targets (probable removals)

Interfaces flagged for review in ADR-004 §2.9:

- `IBinaryClosureWalker` — single impl + 6 test mocks
- `IArtifactPlanner` — single impl + 6 test mocks
- `IArtifactDeployer` — single impl + 6 test mocks
- `IPackagePipeline` (was `IPackageTaskRunner`) — single impl + 7 test mocks (incl. composition-root smoke)
- `IPackageConsumerSmokePipeline` (was `IPackageConsumerSmokeRunner`) — single impl + 4 test mocks
- `IPackageVersionProvider` — single direct impl after ADR-003 (`ExplicitVersionProvider`); `Manifest` and `GitTag` impls constructed inline; criterion 2 borderline

### 7.3 Retention candidates (probable keeps)

Interfaces expected to satisfy §2.9 criteria 1 or 2:

- `IRuntimeScanner` — criterion 1 (3 OS impls)
- `IPackagingStrategy`, `IDependencyPolicyValidator` — criterion 1 (2 impls each)
- `IArtifactSourceResolver` — criterion 1 (Local + Remote profiles + ADR-001 §2.7 ReleasePublic landing in Phase 2b)
- `INuGetFeedClient`, `IDotNetPackInvoker`, `IProjectMetadataReader`, `IVcpkgManifestReader`, `IMsvcDevEnvironment` — criterion 2 (external boundary contracts; the implementation can change without rippling into callers)

### 7.4 Per-interface P3 checklist

For each interface review:

- [ ] Document the §2.9 criterion check in the commit message: criterion 1 (impl count), criterion 2 (axis of change one-sentence statement), or none (removal candidate).
- [ ] If keep: skip; commit message records the rationale.
- [ ] If remove:
  - [ ] Replace constructor parameter type from `IFooBar` to `FooBar`.
  - [ ] Update all DI registrations: `services.AddSingleton<IFooBar, FooBar>()` → `services.AddSingleton<FooBar>()`.
  - [ ] Remove the interface file: `git rm`.
  - [ ] Convert mock-based tests:
    - **Pure-algorithm seam** → switch to concrete construction + fixture-based inputs.
    - **Filesystem-bound seam** (e.g., `IArtifactDeployer`, `IArtifactPlanner` consumers) → concrete class + `Cake.Testing.FakeFileSystem` virtual paths only. **Banned in unit tests:** `Path.GetTempPath()`, real `Directory.CreateDirectory`, real file I/O. Real-disk integration tests live under `Integration/<Scenario>/`, not in `Unit/`.
    - **Process-bound seam** (e.g., `IBinaryClosureWalker` indirectly invoking `dumpbin`/`ldd`/`otool`) → concrete class + substitutes for the still-`I*`-interfaced low-level boundaries (`IRuntimeScanner`, `IPackageInfoProvider` per §2.9 retention). Spawning real native processes from unit tests is banned.
    - **External-boundary seam** that needs substitution but doesn't qualify under §2.9 (rare) → ADR-004 §2.9.1 delegate-hook pattern, with the four invariants documented.
    - **Cross-class orchestration test** that mocks the orchestrator → switch to coarser-grained integration test under `Integration/<Scenario>/` if the original mock-test doesn't carry useful per-method coverage.
  - [ ] Per removed test method: commit message records gerekçe (covered by integration test, redundant after migration, etc.).
- [ ] Test count change is logged in commit message (e.g., "test count: 518 → 511, -7 due to redundant mocks").
- [ ] **Wall-time check:** capture `dotnet test` total wall time before and after the per-interface change; commit-message logs both. Cumulative P3 wall-time inflation is bounded by §11 risk #11 wave gate (≤ 1.20× P2 close wall time).

### 7.5 P3 success criteria

- [ ] Every interface in production code passes one of §2.9 criteria 1, 2, or 3 (transitional debt).
- [ ] No interface exists "because mocks reference it" without §2.9 cover.
- [ ] Test count change documented per wave commit; total test count drift across P3 is bounded and gerekçeli.
- [ ] **Test wall-time gate (§11 risk #11):** total `dotnet test` wall time at P3 close ≤ (P2 close wall time × 1.20). Inflation past 20% halts the wave; root cause identified and reverted before re-attempt.
- [ ] No `Unit/` test spawns real native processes or hits real disk paths (`Path.GetTempPath()`, real `Directory.CreateDirectory`, real `Process.Start` of native CLIs are banned in unit tests).
- [ ] `ArchitectureTests` green.
- [ ] smoke-witness baseline byte-equal to P0.

---

## 8. P4 — API Surface Refactors

### 8.1 Goals

Close ADR-004 §2.11.1 migration exception (Pipelines accept BuildContext). Split `IPathService` into semantic groups. Optionally refactor large Pipelines internally for readability.

### 8.2 Pipeline `RunAsync(BuildContext, TRequest)` → `RunAsync(TRequest)` cut-over

Per ADR-004 §2.11.1, Pipelines should consume `Request` DTOs only. Migration leaves Pipelines accepting `BuildContext` as a transitional state; P4 closes that exception.

For each Pipeline:

- [ ] Add `Request.From(BuildContext, ...)` factory if the Request needs to capture context-derived state (paths, runtime profile, options).
- [ ] Rewrite Pipeline signature: `RunAsync(BuildContext, TRequest, CancellationToken)` → `RunAsync(TRequest, CancellationToken)`.
- [ ] Pipeline constructor takes Cake-side dependencies through DI (`ICakeLog`, `BuildPaths`, etc.) instead of receiving `BuildContext` per-call.
- [ ] Cake Task body: `pipeline.RunAsync(context, ...)` → `pipeline.RunAsync(Request.From(context, _config))`.
- [ ] Update Pipeline tests: `RunAsync(BuildContext, TRequest)` → `RunAsync(TRequest)`. Test fixtures construct Request DTOs directly; BuildContext goes from arg to test-internal scaffolding.
- [ ] `dotnet test` + smoke-witness green.

### 8.3 `IPathService` fluent split

Per ADR-004 §2.2, the deferred work: split the 50+-member `IPathService` interface into semantic groups exposed through `BuildPaths`:

```csharp
public sealed class BuildPaths
{
    public RepoPaths Repo { get; }
    public ArtifactPaths Artifacts { get; }
    public HarvestPaths Harvest { get; }
    public PackagePaths Packages { get; }
    public SmokePaths Smoke { get; }
    public VcpkgPaths Vcpkg { get; }
    public MatrixPaths Matrix { get; }
}
```

Callsite migration: `paths.GetHarvestStageNativeDir(lib, rid)` → `paths.Harvest.GetStageNativeDir(lib, rid)`. This is a mechanical rewrite touching hundreds of callsites; expect a multi-commit P4 sub-wave.

P4 sub-waves:

- 4.1: Introduce `BuildPaths` aggregate with sub-groups; keep old `IPathService` available as an adapter layer.
- 4.2: Migrate callsites group by group (Harvest first, then Packaging, then Smoke, etc.).
- 4.3: Remove old `IPathService` flat interface once all callsites are on the fluent API.

### 8.4 Large Pipeline internal refactor (optional within P4)

`PackageConsumerSmokePipeline` (~688 LOC), `HarvestPipeline` (~628 LOC), `PackagePipeline` (~556 LOC) are candidates for internal restructuring per ADR-004 §3 rationale. Each can be broken into smaller per-concern services in the same feature folder. **No new interfaces unless §2.9 criteria justify**; concrete classes with explicit DI registration.

This is **deferred and per-Pipeline judgment-based**. Not all three need to refactor; only when readability burden is real.

### 8.5 P4 success criteria

- [ ] No Pipeline accepts `BuildContext` as a parameter to `RunAsync`. ADR-004 §2.11.1 migration exception is closed.
- [ ] `IPathService` flat interface either removed or marked obsolete in favor of `BuildPaths` fluent groups.
- [ ] Optional: large Pipelines simplified per §8.4.
- [ ] smoke-witness baseline byte-equal to P0.
- [ ] `ArchitectureTests` green.

---

## 9. P5 — Naming Cleanup + Atomic Wave

### 9.1 Goals

Apply ADR-004 §2.14 naming cleanup: `PreFlightCheck` → `Preflight`, `Coverage-Check` → `CoverageCheck`, `Inspect-HarvestedDependencies` → `InspectHarvestedDependencies`. Retire `UnsupportedArtifactSourceResolver` per ADR-004 §2.15.

### 9.2 Target rename inventory

| Old Cake target | New Cake target | Commit-time callsites to update atomically |
|---|---|---|
| `PreFlightCheck` | `Preflight` | `build/_build/Features/Preflight/PreflightTask.cs` `[TaskName]`; `tests/scripts/smoke-witness.cs` step labels + cake args; `.github/workflows/release.yml` `--target` references; `docs/playbook/cross-platform-smoke-validation.md` A-K checkpoint references; live docs that mention the target name (AGENTS.md, CLAUDE.md, onboarding.md, knowledge-base/cake-build-architecture.md) |
| `Coverage-Check` | `CoverageCheck` | `Features/Coverage/CoverageCheckTask.cs` `[TaskName]`; `release.yml` `Coverage-Check` step (build-cake-host job); `Directory.Build.props` if referenced; live docs |
| `Inspect-HarvestedDependencies` | `InspectHarvestedDependencies` | `Features/Diagnostics/InspectHarvestedDependenciesTask.cs` `[TaskName]`; live docs |

`PackageConsumerSmoke` is **unchanged** per ADR-004 §2.14 (the `Package` prefix preserves contextual scope).

### 9.3 Atomic-wave commit ordering

Per Deniz's operational discipline (2026-05-02): target rename is atomic at commit boundary, but **internal sub-step ordering** matters. For each rename:

```text
1. build/_build/Features/<X>/<Y>Task.cs        — change [TaskName("OldName")] to [TaskName("NewName")]
2. dotnet run --project build/_build -- --target NewName
                                                — local sanity: Cake host green with new target name?
                                                  Halt and fix before proceeding if red.
3. tests/scripts/smoke-witness.cs               — update step labels + cake args
                                                  (e.g., "PreFlightCheck" → "Preflight" in
                                                  RunCiSimAsync's step list)
4. dotnet run tests/scripts/smoke-witness.cs local
   dotnet run tests/scripts/smoke-witness.cs ci-sim
                                                — both modes green; capture new baseline JSON;
                                                  baseline file updated in same commit
5. .github/workflows/release.yml                — --target references updated
6. docs/playbook/cross-platform-smoke-validation.md
                                                — A-K checkpoint references updated
7. AGENTS.md / CLAUDE.md / docs/onboarding.md /
   docs/knowledge-base/cake-build-architecture.md
                                                — any narrative target-name references updated
8. tests/Sandbox/, tests/scripts/*.cs           — any other target-name references
9. Single git commit with message:
   refactor(build-host): P5.X rename Cake target <old> → <new> (ADR-004 §2.14)
```

The local-sanity check at step 2 is the **load-bearing safeguard**. If the Cake host doesn't green with the new target name, no downstream callsite update happens — the wave halts.

### 9.4 `UnsupportedArtifactSourceResolver` retirement

Separate commit (or last commit of P5):

- [ ] Remove `UnsupportedArtifactSourceResolver` class and its DI registration.
- [ ] Update `ArtifactSourceResolverFactory` to fail fast on `release` / `release-public` source values with a clear "not implemented yet" CLI parse-time error. Future activation lands when ADR-001 §2.7 ReleasePublic ships in Phase 2b.
- [ ] Tests: remove `UnsupportedArtifactSourceResolver` test fixtures; add factory-reject tests for `release` / `release-public`.
- [ ] No CLI surface change beyond the failure shifting from runtime to parse-time.

### 9.5 P5 success criteria

- [ ] All three Cake target renames landed atomically (each its own commit per §9.3 ordering): `PreFlightCheck` → `Preflight`, `Coverage-Check` → `CoverageCheck`, `Inspect-HarvestedDependencies` → `InspectHarvestedDependencies`. (`PackageConsumerSmoke` is unchanged per ADR-004 §2.14.)
- [ ] smoke-witness baseline JSON updated to the new step labels. Each P5 rename commit produces an updated baseline; **the updated baseline becomes the new comparison baseline for the next P5 commit and for all post-P5 wave verification.** This is the single allowed baseline shift across the migration; P1–P4 must all match the P0 baseline byte-for-byte.
- [ ] release.yml CI runs green on a smoke commit (push to a temp branch + observe CI).
- [ ] cross-platform-smoke-validation.md A-K checkpoint script copy-paste-able with new target names.
- [ ] `UnsupportedArtifactSourceResolver` removed; CLI parse-time validation in place.
- [ ] `ArchitectureTests` green.
- [ ] No legacy target names remain in any file under `build/`, `tests/`, `.github/`, `docs/` (other than `_archive/` and `docs/reviews/`).

---

## 10. Test Strategy (Cross-Cutting)

### 10.1 Mirror invariant

Every commit must keep the test folder structure mirrored to the production folder structure (per ADR-004 §2.7 in the superseded ADR-002 wording, preserved as repo convention):

| Production path | Test path |
|---|---|
| `build/_build/Host/<X>` | `build/_build.Tests/Unit/Host/<X>/` |
| `build/_build/Features/<X>/<Y>.cs` | `build/_build.Tests/Unit/Features/<X>/<Y>Tests.cs` (flat per feature — see §10.2) |
| `build/_build/Shared/<X>/<Y>.cs` | `build/_build.Tests/Unit/Shared/<X>/<Y>Tests.cs` |
| `build/_build/Tools/<X>/<Y>.cs` | `build/_build.Tests/Unit/Tools/<X>/<Y>Tests.cs` |
| `build/_build/Integrations/<X>/<Y>.cs` | `build/_build.Tests/Unit/Integrations/<X>/<Y>Tests.cs` |
| (composition root, cross-cutting) | `build/_build.Tests/Unit/CompositionRoot/<X>Tests.cs` |

**Integration tests** (`build/_build.Tests/Integration/<Scenario>/`) are **not mirrored** — they are organized by user-visible flow under test (e.g., `Integration/SetupLocalDev/`, `Integration/PackagingPipeline/`).

**Characterization tests** (`build/_build.Tests/Characterization/`) are not mirrored — they cover serialization / contract snapshots and are scenario-based.

**Fixtures** (`build/_build.Tests/Fixtures/`) are not mirrored — cross-cutting test infrastructure.

### 10.2 Flat per-feature inside `Unit/Features/<X>/`

Per Deniz decision 2026-05-02: tests inside a feature folder are **flat**, not sub-folder-by-kind. Production folder is flat (Task + Pipeline + Validators + Generators + Request co-located); tests mirror that.

```text
Unit/Features/Packaging/
├── PackageTaskTests.cs
├── PackagePipelineTests.cs
├── PackageConsumerSmokeTaskTests.cs
├── PackageConsumerSmokePipelineTests.cs
├── PackageOutputValidatorTests.cs
├── NativePackageMetadataValidatorTests.cs
├── ReadmeMappingTableValidatorTests.cs
├── G58CrossFamilyValidatorTests.cs
├── SatelliteUpperBoundValidatorTests.cs
├── FamilyTopologyHelpersTests.cs
├── LocalArtifactSourceResolverTests.cs
├── RemoteArtifactSourceResolverTests.cs
├── ArtifactSourceResolverFactoryTests.cs
└── ServiceCollectionExtensionsTests.cs
```

If a feature's test count grows past ~30 files and navigation suffers, sub-folders may be introduced **per feature, by exception**, with the gerekçe documented in commit message. This is not the default.

### 10.3 Wave-aligned migration

Test files migrate **in the same commit** as their production counterparts. No half-migrated state where production lives at `Features/Packaging/PackagePipeline.cs` but its test is still at `Unit/Application/Packaging/PackageTaskRunnerTests.cs`. This is enforced at code-review time.

### 10.4 Test count ratchet (recap from §2.3)

P0 baseline = 500. P1 ≥ P0 (move-only). P2 ≥ P1 + 5 ArchitectureTests + one ServiceCollectionExtensions smoke per migrated feature (~+18 expected). P3 flexible with explicit per-test-removal gerekçe. P4 ≥ P3. P5 ≥ P4. Each wave compares against the previous wave's actual close count, not against P0.

### 10.5 Architecture tests — invariant test methods

`ArchitectureTests.cs` (post-P2) carries 5 invariant test methods:

```csharp
public sealed class ArchitectureTests
{
    [Test] public Task Shared_Should_Have_No_Outward_Or_Cake_Dependencies() { ... }
    [Test] public Task Tools_Should_Have_No_Feature_Dependencies() { ... }
    [Test] public Task Integrations_Should_Have_No_Feature_Dependencies() { ... }
    [Test] public Task Features_Should_Not_Cross_Reference_Except_From_LocalDev() { ... }
    [Test] public Task Host_Is_Free() { ... }
}
```

Implementation: reflection over the loaded `Build` assembly, namespace-prefix matching as in the existing `LayerDependencyTests`. The `Features.LocalDev` allowlist in invariant #4 is hard-coded — adding a second orchestration feature requires explicit code change (this is the intent: drift-blocking).

### 10.6 ServiceCollectionExtensions smoke tests

Per feature, one smoke test that asserts `services.AddXFeature()` registers every type that the feature exports as DI-resolvable. Catches "I added a service but forgot to register it" drift.

A naive smoke test (`new ServiceCollection().AddPackagingFeature().BuildServiceProvider().GetService<PackagePipeline>()`) **will not resolve** — `PackagePipeline` constructor takes `ICakeLog`, `BuildPaths`, `ManifestConfig`, `BuildOptions`, validators, and so on. Many of those come from Host / Shared / Tools / Integrations — not from the feature itself.

#### 10.6.1 Shared test host fixture

Tests register a minimal-but-complete dependency floor before the feature under test:

```csharp
public static class TestHostFixture
{
    public static IServiceCollection AddTestHostBuildingBlocks(this IServiceCollection services)
    {
        // Cake ambient API — fakes
        services.AddSingleton<ICakeLog>(_ => Substitute.For<ICakeLog>());
        services.AddSingleton<ICakeContext>(_ => /* fake or BuildContext shim */);
        services.AddSingleton<IFileSystem>(_ => new FakeFileSystem(/* ... */));

        // Host-level singletons (data, not behavior)
        services.AddSingleton<BuildPaths>(_ => /* fixed test paths */);
        services.AddSingleton<RuntimeProfile>(_ => /* fixed test RID profile */);
        services.AddSingleton<ManifestConfig>(_ => /* canned minimal manifest */);
        services.AddSingleton<BuildOptions>(_ => /* default options */);

        // Tools and Integrations the feature transitively needs (interface seam keeps cost low)
        services.AddSingleton<INuGetFeedClient>(_ => Substitute.For<INuGetFeedClient>());
        services.AddSingleton<IDotNetPackInvoker>(_ => Substitute.For<IDotNetPackInvoker>());
        // ... etc

        return services;
    }
}
```

#### 10.6.2 Feature smoke shape

```csharp
[Test]
public async Task AddPackagingFeature_Should_Register_All_Pipeline_And_Validator_Types()
{
    var services = new ServiceCollection()
        .AddTestHostBuildingBlocks()
        .AddPackagingFeature();

    var provider = services.BuildServiceProvider();

    await Assert.That(provider.GetService<PackagePipeline>()).IsNotNull();
    await Assert.That(provider.GetService<PackageConsumerSmokePipeline>()).IsNotNull();
    await Assert.That(provider.GetService<PackageOutputValidator>()).IsNotNull();
    // ... every type the feature exports
}
```

#### 10.6.3 Fixture lives in `Build.Tests/Fixtures/`

`TestHostFixture.cs` is shared infrastructure (cross-cutting, not mirroring any single production folder). It evolves alongside the production composition root: when a new `Tool<T>` or `Integration` adapter type lands and a feature consumes it, the fixture gains the corresponding `services.AddSingleton<...>()` line. Drift between production composition root and test fixture is itself a regression — the smoke tests fail loud (provider resolution exception) when the fixture is incomplete.

Per §2.3 / §10.4 formula, these are the per-feature smoke tests counted in the P2 close ratchet (~+13 with the current 13-feature roster).

---

## 11. Risks & Mitigations

| # | Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|---|
| 1 | **Migration churn disrupts cross-PR reviews** | High | Medium | Per-feature waves keep diff scope small; `git mv` preserves blame; `git log --follow` works through renames |
| 2 | **smoke-witness signal regression mid-migration** | Medium | High | `--emit-baseline` flag (P0); per-wave baseline diff is the gate; behavior signal byte-equal across P1/P2/P3/P4 |
| 3 | **Test rewrite cost in P3 exceeds estimate** | Medium | Medium | Per-interface scoping with explicit gerekçe; ratchet exception; transitional retention (criterion 3) is allowed when removal cost dwarfs benefit |
| 4 | **`Shared/` no-Cake invariant blocks P2 close** | Low | Medium | Pre-emptive enum/vocabulary extraction for `RuntimeProfile.PlatformFamily` in late P1. **If P2 cannot close with `Shared/` Cake-free, the wave does not close** unless a named, gerekçeli `ArchitectureTests` exclusion is added to the test file with (a) the namespace pattern excluded, (b) a tracking issue link, and (c) a hard P3-close removal deadline. "Accept transitional Cake reference" without the three-part discipline is the path to permanent technical debt. |
| 5 | **Pipeline `RunAsync(TRequest)` cut-over ripples test fixtures** | Medium | Medium | P4 deferred until P3 lands; per-Pipeline scope; large Pipelines (PackageConsumerSmoke, Harvest) get their own sub-waves |
| 6 | **Atomic P5 commit too large to review** | Medium | Low | P5 lands one rename at a time per §9.3 (each rename = one atomic commit); 4 commits total in P5, each small |
| 7 | **CI surface drift between Cake target rename and release.yml update** | Low | High | §9.3 atomic ordering: Cake-side rename → smoke-witness verify → release.yml update — all in one commit. Splitting is wave-rejection. |
| 8 | **smoke-witness baseline file format bikeshed** | Low | Low | §2.1.2 locks JSON shape with concrete fields; debate is closed unless behavior signal proves insufficient |
| 9 | **Phase 2b release work collides with migration** | Medium | Medium | This phase is standalone; can pause between waves. Phase 2b release commits do not touch architecture; if a wave is mid-flight, finish the current wave commit before merging Phase 2b changes. No half-migrated state on master. |
| 10 | **Documentation drift between live docs and code** | Medium | Low | Live docs already updated to ADR-004 shape (Turn 2 closure 2026-05-02); each P-wave updates the relevant doc sections (§2.14 atomic-wave for P5, narrative updates as code lands) |
| 11 | **P3 test execution time regression — mock removals replaced with real I/O** | Medium-High | Medium | Interfaces like `IBinaryClosureWalker`, `IArtifactPlanner`, `IArtifactDeployer` are filesystem / process-bound. If their mock-based tests are converted to concrete tests that hit real disk or spawn real processes, P3 close will inflate test suite duration from milliseconds to seconds-or-minutes — developer feedback loop and CI duration both regress. **Mitigation:** every concrete-class test rewrite in P3 must use `Cake.Testing.FakeFileSystem` (already available — `Build.Tests` references `Cake.Testing`) for filesystem ops, and `Substitute.For<>()` against `IRuntimeScanner` / `IPackageInfoProvider` (kept §2.9 seams) for process boundaries. **Wave gate:** P3 close adds an explicit pre-merge check — total `dotnet test` wall time must not exceed (P2 close wall time × 1.20). Drift past 20% halts the wave; root cause is identified and reverted before re-attempt. Exception requires explicit gerekçe (e.g., a new integration test under `Integration/<Scenario>/` that genuinely needs real I/O — bounded to scenario, not unit, scope). |

---

## 12. Per-Wave Commit Policy

### 12.1 Branch model

- Refactor lands on master via short-lived feature branches per wave (or per major sub-wave): `refactor/build-host-modernization-p1.10-harvesting`, `refactor/build-host-modernization-p2-architecture-tests`, etc.
- Each wave branch merges to master via `--no-ff` to preserve the per-wave bisectable boundary. Squash merges are not used — per-commit greenness is part of the contract.
- Feature branches do not pile up; each wave merges before the next starts to avoid migration debt accumulating across branches.

### 12.2 Commit message convention

- **P1 waves:** `refactor(build-host): P1.X migrate <feature> to Features/<X>/ shape (ADR-004)` + body listing files moved + transitional `LayerDependencyTests` exclusions + smoke-witness baseline check result.
- **P2 waves:** `refactor(build-host): P2.X rename <X>TaskRunner → <X>Pipeline (ADR-004 §2.10)` + body + DI registration changes.
- **P2 architecture rewrite commit:** `refactor(build-host-tests): P2 rename LayerDependencyTests → ArchitectureTests + rewrite invariants (ADR-004 §2.13)`.
- **P3 commits:** `refactor(build-host): P3.X review <interface> (keep|remove); test count Δ` + body with §2.9 criterion result + per-removed-test gerekçe.
- **P4 commits:** `refactor(build-host): P4.X close §2.11.1 BuildContext exception for <Pipeline>` + body.
- **P5 atomic commits:** `refactor(build-host): P5.X rename Cake target <old> → <new> (ADR-004 §2.14)` + body listing all callsites updated.

### 12.3 Pre-merge checks

For every wave merge, the following must be green:

- [ ] `dotnet build build/_build/Build.csproj -c Release`
- [ ] `dotnet test build/_build.Tests/Build.Tests.csproj -c Release`
- [ ] Test count meets ratchet for that wave (§2.3)
- [ ] **Test wall-time gate (P3 only):** total `dotnet test` wall time ≤ (P2 close wall time × 1.20). Other waves capture wall time for trend visibility but do not gate on it.
- [ ] `LayerDependencyTests` (P0–P1) or `ArchitectureTests` (P2+) green
- [ ] `tests/scripts/smoke-witness.cs local --emit-baseline` matches baseline (P5 updates baseline)
- [ ] `tests/scripts/smoke-witness.cs ci-sim --emit-baseline` matches baseline (P5 updates baseline)
- [ ] No mid-wave commits — every commit on the branch is itself green (verifiable via `git rebase --exec`)

### 12.4 Rollback policy

If a wave breaks master:

- **P1 / P2 / P3 / P4:** revert the wave merge commit; wave is re-attempted as a fresh branch.
- **P5 atomic-wave:** revert the per-target-rename commit; the Cake target falls back to the old name; CI/smoke-witness are atomic-reverted in the same revert.

No partial rollback within a wave — all-or-nothing per commit.

---

## 13. References

### 13.1 Repo-internal

- [ADR-004 — Cake-Native Feature-Oriented Build-Host Architecture](../decisions/2026-05-02-cake-native-feature-architecture.md) — governing ADR.
- [ADR-002 — DDD Layered Architecture](../decisions/2026-04-19-ddd-layering-build-host.md) — superseded; historical record.
- [ADR-001 — D-3seg Versioning](../decisions/2026-04-18-versioning-d3seg.md) — external contracts unaffected.
- [ADR-003 — Release Lifecycle Orchestration](../decisions/2026-04-20-release-lifecycle-orchestration.md) — invariants unchanged; internal-layout references updated to ADR-004 shape.
- [`tests/scripts/smoke-witness.cs`](../../tests/scripts/smoke-witness.cs) — north-star behavior contract.
- [`build/_build.Tests/Unit/CompositionRoot/LayerDependencyTests.cs`](../../build/_build.Tests/Unit/CompositionRoot/LayerDependencyTests.cs) — current architecture test (P2 renamed to `ArchitectureTests.cs`).
- [`.github/workflows/release.yml`](../../.github/workflows/release.yml) — CI pipeline; P5 atomic-wave updates Cake target names.
- [`docs/playbook/cross-platform-smoke-validation.md`](../playbook/cross-platform-smoke-validation.md) — A-K checkpoint script; P5 atomic-wave updates target name references.
- [`docs/reviews/code-review-conversation.txt`](../reviews/code-review-conversation.txt) — 2026-05-01 critique pass.
- [`docs/reviews/conversation-2.txt`](../reviews/conversation-2.txt) — 2026-05-01 critique extension.
- [`docs/reviews/mycomments.txt`](../reviews/mycomments.txt) — Deniz's pre-finalization patch list.

### 13.2 External

- Cake Frosting documentation — Tool/Aliases/Settings/Runner filename triad preserved at `Tools/`.
- Vertical Slice Architecture (Jimmy Bogard) — feature-cohesion principle.

---

## 14. Change log

| Date | Change | Editor |
| --- | --- | --- |
| 2026-05-02 | Initial draft after ADR-004 finalization; Turn 3 deliverable from the 2026-05-01 to 2026-05-02 build-host refactor synthesis session | Deniz İrgin + collaborative critique synthesis |
| 2026-05-02 | Same-day pre-finalization patch wave (12 patches): §1.3 CLI surface contradiction (target rename + `--source` narrowing exception); §1.5 Mechanical vs structural waves table added; §4.2 baselines committed under `tests/scripts/baselines/` per-host (TBD closed); §5.2 P1.2 Coverage ambiguity removed; §5.2 P1.13 ArtifactSourceResolvers stay in Packaging per ADR-004 §2.3 (LocalDev consumes via §2.13 invariant #4 allowlist); §5.2 P1.15 CakeExtensions split moved to P2 (P1 stays mechanical); §6.2 sub-validator DI injection deliverable explicit (ADR-004 §1.1.6 anti-pattern fix); §6.2 CakeExtensions split deliverable added to P2; §7.4 + §7.5 + §11 risk #11 + §12.3 P3 test wall-time gate (≤ 1.20× P2 baseline) — addresses concrete-impl I/O test inflation risk; FakeFileSystem mandatory for filesystem-bound seam rewrites; §9.5 P5 "four renames" → "three" + rolling baseline note; §10.6 ServiceCollectionExtensions smoke tests use shared `TestHostFixture.AddTestHostBuildingBlocks()`; §11 risk #4 Shared no-Cake transitional exception sertleştirildi (named exclusion + tracking issue + P3 deadline); §2.3 + §10.4 + §6.5 test count ratchet hard sayıdan formüle dönüştü | Deniz İrgin |
