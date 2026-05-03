# Phase X — Build-Host Modernization (ADR-004 Migration)

- **Date:** 2026-05-02
- **Status:** P0 + P1 + P2 + Adım 13 + P3 + P4-A CLOSED (commits `b18002f`, `651ac2f`, `e602b6c`, `b6de515`, `3ab2e68`, `d79daa1` → `dfa4ed9`, `d1127e4` on `master`); P4-C (large Pipeline decomposition) + P5 not started
- **Author:** Deniz İrgin (@denizirgin) + 2026-05-01 collaborative critique synthesis
- **Governing ADR:** [ADR-004 — Cake-Native Feature-Oriented Build-Host Architecture](../decisions/2026-05-02-cake-native-feature-architecture.md)
- **Supersedes:** No prior plan
- **Standalone phase:** This is a cross-cutting build-host refactor wave. It is **not tied to Phase 2 / Phase 2b / Phase 3** roadmap items. Individual P-stages (P0–P5) are scoped, sequenced, and gated independently and may run between or around Phase 2b release work.

## Wave-Status Snapshot (2026-05-02)

| Wave | Status | Commit(s) | Notes |
| --- | --- | --- | --- |
| **P0** Safety Baseline | ✅ CLOSED | `b18002f`, `651ac2f`, `e602b6c` | `--emit-baseline` flag + verify-baselines.cs helper + 4 baselines (Win local fast-loop, Linux local milestone, Win ci-sim milestone, macOS Intel milestone) + test-count.txt (500) + cake-targets.txt (20 targets) + 6 doc updates. Mac sleep gracefully skipped. |
| **P1** Folder Migration | ✅ CLOSED | `b6de515` | 291 git mv + Rider Adjust Namespaces sweep + 3 stale `using` cleanup. ADR-002 layered shape (Tasks/Application/Domain/Infrastructure/Context) retired; ADR-004 5-folder shape (Host/Features/Shared/Tools/Integrations) established. 132 files in 13 feature folders. Verification all green (build clean, 500/500 tests, LayerDependencyTests natural-shrinkage green, fast-loop MATCH 89.8s). |
| **P2** Terminology + DI rewrite | ✅ CLOSED | `3ab2e68` | 12 sub-steps: cosmetic warmup, CakeExtensions split, BuildOptions aggregate, BuildContext slim (6→4 prop), `*TaskRunner` → `*Pipeline` × 16 + `SetupLocalDev` → Flow, per-feature `ServiceCollectionExtensions` × 13, initial Program.cs DI chain collapse, Shared/Runtime Cake-decoupling (`PlatformFamily` → `RuntimeFamily`, `IsSystemFile(FilePath)` → `string`), `LayerDependencyTests` → `ArchitectureTests` rewrite. 502 tests / 499 passed / 3 skipped at P2 close; Adım 13 closed the skips and smoke-test deferrals. Win fast-loop MATCH 92.5s, Win ci-sim 9/9 PASS 109.0s, WSL Linux 3/3 PASS 198.7s, Mac sleep skip. |
| **Adım 13** (post-P2 follow-up) | ✅ CLOSED | `d79daa1` → `dfa4ed9` | Shared/Harvesting + Shared/Coverage + Shared/Packaging + Shared/Versioning promotions closed 24/26 cross-tier violations; 2 IPathService Host-couplings documented as a permanent named exception; all 5 ArchitectureTests invariants active; 13 ServiceCollectionExtensions smokes landed via TestHostFixture; cake-build-architecture.md rewritten; Program.cs DI chain reads as 3 cross-cutting groups + 13 feature calls; 515 tests / 0 skipped; 4-host milestone-loop MATCH at close. |
| **P3** Interface Review | ✅ CLOSED | `d1127e4` | 32 production interfaces reviewed against ADR-004 §2.9; 4 mock-only / stateless seams removed; 28 retained with explicit criterion labels. 515/515 tests. |
| **P4-A** Pipeline RunAsync cut-over | ✅ CLOSED | `d1127e4` | 11 Pipelines + 2 interfaces: `RunAsync(BuildContext, TRequest, CT)` → `RunAsync(TRequest, CT)`. ADR-004 §2.11.1 migration exception closed — zero Pipelines accept `BuildContext` in `RunAsync`. CoverageCheckPipeline (sync, 11th) caught by adversarial review. VcpkgBootstrapTool relocated to `Integrations/Vcpkg/`. IPathService fluent split permanently discarded. Cross-platform: 515/515 tests, ci-sim 9/9 on Win+Linux+macOS. |
| **P4-C** Large Pipeline decomposition | ⏸️ NOT STARTED | — | Optional. Candidates: `PackageConsumerSmokePipeline` (~688 LOC), `HarvestPipeline` (~628 LOC), `PackagePipeline` (~556 LOC). |
| **P5** Naming + atomic | ⏸️ NOT STARTED | — | Awaits P4 close. |

**Behaviour-signal preservation:** smoke-witness `local` mode `(label, exit)` tuple has been byte-equal to the P0 baseline through every P1 + P2 + Adım 13 commit boundary on Windows fast-loop and on every milestone-loop run per phase-x §2.1. Adım 13 close verified MATCH across Win local, Win ci-sim, WSL Linux, and macOS Intel. P3 + P4-A close verified MATCH across all 3 maintainer hosts at commit `d1127e4`: Win local 88.4s + ci-sim 107.4s, WSL Linux local 77.0s + ci-sim 9/9 82.4s, macOS Intel local 122.3s + ci-sim 9/9 143.2s. 515/515 tests on all platforms.

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
- Retirement of `UnsupportedArtifactSourceResolver` (retired in Phase Y Wave B — see `phase-y-dev-tools-extraction-2026-05-03.md`).
- Pipeline `RunAsync(BuildContext, TRequest)` → `RunAsync(TRequest)` cut-over (closes ADR-004 §2.11.1 migration exception).
- Interface review wave applying ADR-004 §2.9 three-criteria rule to surviving `I*` types.

### 1.3 What is explicitly out of scope

- Cake target dependency mapping (`IsDependentOn`/`IsDependeeOf`) semantics — unchanged.
- ADR-001 D-3seg versioning, package-first consumer contract, ArtifactProfile semantics — unchanged.
- ADR-003 release lifecycle invariants (provider/scope/version axes, stage-owned validation, matrix re-entry, G54/G58 placement) — unchanged.
- `manifest.json` schema v2.1 — no contract changes.
- `release.yml` 10-job topology — only target-name updates from §2.14, no job re-shuffle.
- Pack guardrails (G14/G15/G16/G46/G54/G58) — preserved verbatim, relocated to feature folders.
- `external/sdl2-cs` and any submodule — untouched.
- Public CLI semantics — unchanged, **except for** ADR-004 §2.14 target-name normalization (`PreFlightCheck` → `Preflight`, `Coverage-Check` → `CoverageCheck`, `Inspect-HarvestedDependencies` → `InspectHarvestedDependencies`). `--source` narrowing was moved to Phase Y Wave B (2026-05-03) which retired the entire `--source` CLI option and its `ArtifactSourceResolver` family.

### 1.4 Why a standalone phase

Phase 2 / 2b are release-pipeline tracks (CI/CD, Publishing, RemoteArtifactSourceResolver tail). This refactor is structural — it touches every feature folder but should not change observable behavior. Mixing this migration into a Phase 2b commit train would produce unreviewable diffs and increase the risk of behavior regressions hiding inside structural changes.

Phase Y (2026-05-03) handles Cake mission narrowing + dev-tools extraction as a parallel track outside this plan — Phase X is architectural shape, Phase Y is architectural scope.

### 1.5 Mechanical vs structural waves

This refactor is **not** a folder-renaming pass. Some waves are mechanical by design; others are real code refactor that change class shape, signatures, DI graph, and mental model. The wave character matters because it sets review expectations and rollback granularity:

| Wave | Character | What changes in production code |
| --- | --- | --- |
| **P0** | Mechanical (additive) | smoke-witness gains an opt-in `--emit-baseline` flag. Production code untouched. |
| **P1** | **Mechanical (deliberately narrow)** | `git mv` + namespace + `using` adjustments only. Class internals, signatures, behavior unchanged. The narrow scope is **intentional**: 200+ files moving in one wave should not also carry behavior shifts — when something breaks, root cause is "the move" and nothing else. |
| **P2** | **Structural (real refactor)** | Class renames (`*TaskRunner` → `*Pipeline`, `SetupLocalDevTaskRunner` → `SetupLocalDevFlow`); per-feature `ServiceCollectionExtensions.cs` is **written**; `Program.cs` DI chain collapses to `services.Add*Feature()` calls; `BuildOptions` aggregate record is **written**; `BuildContext` slims from 7 properties + 6 sub-configs to 4 properties (`Paths`, `Runtime`, `Manifest`, `Options`); `ManifestConfig` moves onto `BuildContext` as data; **`new XValidator()` calls inside orchestrators replace with constructor injection** (ADR-004 §1.1.6 anti-pattern fix); `LayerDependencyTests.cs` is **renamed and rewritten** to `ArchitectureTests.cs` with the 5-invariant set (ADR-004 §2.13). |
| **P3** | **Structural (interface seam + test rewrite)** | Interface seams reviewed against ADR-004 §2.9 — kept or removed; constructor parameter types switch from `IFoo` to `Foo` for removals; mock-based unit tests rewrite as fixture-based concrete tests, integration tests under `Integration/<Scenario>/`, or §2.9.1 delegate-hook patterns; ~20–30 test methods rewritten across the wave. |
| **P3** | **Structural (interface seam)** | 32 interfaces reviewed; 4 stateless/mock-only seams removed; 28 retained with criterion labels. Test rewrites scoped per-interface. |
| **P4-A** | **Structural (signature evolution)** | Pipeline `RunAsync(BuildContext, TRequest, CT)` → `RunAsync(TRequest, CT)` across 10 Pipelines + 2 interfaces; Pipeline constructors take narrow Cake abstractions (`ICakeContext`, `ICakeLog`, `IPathService`) via DI; 15 Tasks updated. ADR-004 §2.11.1 migration exception closed. |
| **P4-C** | **Structural (decomposition, optional)** | Internal refactor of large Pipelines (PackageConsumerSmoke 688 LOC, Harvest 628, Package 556) into smaller per-concern co-located helpers. Per-Pipeline judgment; not gating. |
| **P5** | Mechanical (atomic) | Atomic same-commit target rename: `PreFlightCheck` → `Preflight`, `Coverage-Check` → `CoverageCheck`, `Inspect-HarvestedDependencies` → `InspectHarvestedDependencies`. `tools ci-sim` step labels + `release.yml` `--target` references + live-doc mentions updated in the same commit. `UnsupportedArtifactSourceResolver` retirement + `--source` narrowing moved to Phase Y Wave B. |

**The substantive transformation is P2–P4.** P2 changes the mental model: from ADR-002's layered Application/Domain/Infrastructure shape with a half-service-locator `BuildContext` to ADR-004's "Features own behavior, BuildContext is invocation state, DI registers capabilities" model. P3 enforces the interface discipline. P4 closes the signature evolution — Pipelines become pure Request consumers; pure services take explicit inputs.

P1 and P5 are bracketed by design: mechanical, narrow-scoped, easy to review at the file-move and target-rename level respectively. They surround the real work, not replace it.

---

## 2. North Stars

Three invariants govern the migration. Every wave commit must keep all three green.

### 2.1 smoke-witness behavior signal

**Retired by Phase Y Wave A (2026-05-03).** This section was load-bearing during P0–P4-A; Phase Y Wave A retired the baseline scaffolding (`--emit-baseline` flag, `BaselineSignal`/`BaselineStep` records, `verify-baselines.cs`, `tests/scripts/baselines/*`). The dev pre-merge ritual is now `tools ci-sim` + manual step-list/exit-code diff. The §2.1.x sub-sections below are preserved as chronological evidence, not as load-bearing mechanism.

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

#### 2.1.3 When the signal changed (historical — Phase Y Wave A retired baseline files)

The baseline-file mechanism is retired; step labels are still observable via `tools ci-sim` console output and manually diffed, but there is no longer a committed JSON baseline or an automated cadence enforcing it.

- **P5 naming cleanup wave** will rename `PreFlightCheck` → `Preflight`, `Coverage-Check` → `CoverageCheck`, `Inspect-HarvestedDependencies` → `InspectHarvestedDependencies`. The `tools ci-sim` step labels must be updated in the same atomic commit that renames the Cake targets.
- P1–P4-A waves were gated by baseline-file equality; all passed with MATCH. The baseline was a P0–P4-A safety net, now retired.

#### 2.1.4 P0 deliverable: `--emit-baseline` flag

`smoke-witness.cs` gained an opt-in `--emit-baseline <path>` flag in P0. When passed, the witness wrote the §2.1.2 JSON to the given path after the run completed. **Phase Y Wave A (2026-05-03) removed this flag, the `BaselineSignal`/`BaselineStep` record types, the `EmitBaselineAsync` method, and all related parsing code.**

#### 2.1.5 Loop cadence — fast vs milestone

**Phase Y Wave A (2026-05-03) retired the baseline-file cadence.** The fast/milestone loop was a Phase X migration safety net. Post-Phase-Y, the dev pre-merge check is `dotnet run --file tools.cs -- ci-sim` and manually diffing the step list and exit codes against the last known-good run. Automated cadence is gone; baseline files under `tests/scripts/baselines/` are deleted. The paragraphs below are historical record, not active mechanism.

Per-host baseline files were committed under `tests/scripts/baselines/` (deleted by Phase Y Wave A). Different files had different verification cadence to balance pre-merge friction against multi-host coverage:

**Fast loop — every wave commit boundary (retired):**

- `smoke-witness-local-win-x64.json` — Windows host, `local` mode.

The developer's pre-merge ritual was: run `smoke-witness.cs local` and diff the step list against the committed baseline. Post-Phase-Y, this is replaced by `dotnet run --file tools.cs -- ci-sim` with manual step-list/exit-code diff.

**Milestone loop — every P-wave close commit boundary (retired):**

- `smoke-witness-local-linux-x64.json` — WSL Linux host, `local` mode.
- `smoke-witness-local-osx-x64.json` — macOS Intel host, `local` mode (best-effort).
- `smoke-witness-ci-sim-win-x64.json` — Windows host, `ci-sim` mode (full pipeline replay).

Milestone-loop baselines were updated at P-wave close commits. macOS coverage was best-effort, not gating.

**`verify-baselines.cs` (deleted by Phase Y Wave A)** was a file-based app that operationalized both loops — spawning `smoke-witness.cs --emit-baseline <tmp>` per loop entry, comparing deserialized logical tuples, and printing a Spectre table. Non-zero exit on mismatch served as the wave-rejection signal in §12.3.

Both scripts were file-based .NET 10 apps with zero shell-flavor proliferation. Only `smoke-witness.cs` survives post-Phase-Y Wave A (minus the `--emit-baseline` flag); `verify-baselines.cs` is deleted.

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
                                  large-Pipeline internal refactor]
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
| Fast-loop baseline (`local` Win) | `tests/scripts/baselines/smoke-witness-local-win-x64.json` (committed) | JSON behavior signal per §2.1.2; gates every wave commit per §2.1.5 fast-loop cadence |
| Milestone baseline (`local` Linux) | `tests/scripts/baselines/smoke-witness-local-linux-x64.json` (committed) | JSON behavior signal per §2.1.2; gates P-wave close commits per §2.1.5 milestone-loop cadence |
| Milestone baseline (`local` macOS, opt-in) | `tests/scripts/baselines/smoke-witness-local-osx-x64.json` (committed when available; `osx-arm64` companion when Apple Silicon host is added) | Best-effort coverage per §2.1.5 + §10.5; not a wave-merge precondition |
| Milestone baseline (`ci-sim` Win) | `tests/scripts/baselines/smoke-witness-ci-sim-win-x64.json` (committed) | Full pipeline replay; gates P-wave close commits per §2.1.5 |
| `verify-baselines.cs` helper | `tests/scripts/verify-baselines.cs` (file-based app, .NET 10) | Default = fast loop; `--milestone` = milestone loop. Spawns `smoke-witness.cs --emit-baseline tmp.json` per entry, diffs via `JsonSerializer`, exits non-zero on mismatch. Operationalizes §2.1.5 + §12.3 pre-merge checks |
| Test count baseline | `tests/scripts/baselines/test-count.txt` (committed) | Plain integer; behavior contract belongs in VCS |
| Public Cake target surface freeze | `tests/scripts/baselines/cake-targets.txt` (committed) | `dotnet run -- --tree` output captured at P0 commit; target names that must survive untouched until P5 |
| Target rename inventory | This plan §9.2 (a table embedded below) | Each old name → new name + atomic-wave callsites |
| LayerDependencyTests baseline | n/a — currently passing; just a snapshot of the test names and invariants | For P2 reference when rewriting to `ArchitectureTests` |

### 4.3 P0 success criteria

P0 closed at commits `b18002f` (kickoff + fast-loop), `651ac2f` (milestone baselines + Mac analyzer fix + osx-x64 RID correction), `e602b6c` (macOS Intel milestone baseline).

- [x] smoke-witness `--emit-baseline <path>` lands; backwards compatible (no flag → old behavior). Plus `verbose=off` log persistence fix (smoke-witness now writes a forensic log file regardless of `--verbose`, addressing a follow-up gap that surfaced during P0 close — silent runs were dropping debug evidence).
- [x] `verify-baselines.cs` lands at `tests/scripts/verify-baselines.cs`; default fast loop, `--milestone` covers Linux + ci-sim Win + macOS Intel (host-RID gate skips entries the running host cannot reproduce).
- [x] Fast-loop baseline `smoke-witness-local-win-x64.json` runs green (3/3 PASS, 101.8s).
- [x] Milestone-loop baseline `smoke-witness-local-linux-x64.json` runs green on WSL Linux (3/3 PASS, 184.0s with hot vcpkg cache).
- [x] Milestone-loop baseline `smoke-witness-ci-sim-win-x64.json` runs green on Windows (9/9 PASS, 119.2s — full pipeline replay).
- [x] Milestone-loop baseline `smoke-witness-local-osx-x64.json` runs green on macOS Intel via SSH (3/3 PASS, 145.7s; opt-in per §10.5).
- [x] `dotnet test --project build/_build.Tests/Build.Tests.csproj -c Release --framework net10.0` runs green; `tests/scripts/baselines/test-count.txt` = `500`.
- [x] `dotnet run --project build/_build -- --tree` output captured (header-stripped + sorted) to `tests/scripts/baselines/cake-targets.txt` — 20 targets.
- [x] Public target surface freeze list is committed; any target name outside this list during P1–P4 is a wave-rejection signal.
- [x] Target rename inventory (§9.2) reviewed; ADR-004 §2.14 rename criterion added (plain-PascalCase trigger rule).

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
| 1.16 | `Infrastructure/Paths/PathService.cs` → `Host/Paths/PathService.cs` (single-file pass-through) | mirror |
| 1.17 | `Infrastructure/Tools/{Vcpkg,Dumpbin,Ldd,Otool,Tar,NativeSmoke,CMake}/` → `Tools/` (Cake `Tool<T>` wrappers) | mirror |
| 1.18 | `Infrastructure/Tools/Msvc/`, `Infrastructure/DotNet/`, `Infrastructure/Vcpkg/`, `Infrastructure/Coverage/` (non-Cake-Tool adapters) → `Integrations/Msvc/`, `Integrations/DotNet/`, `Integrations/Vcpkg/`, `Integrations/Coverage/` | mirror |

The order above is **recommended, not strict**. Constraints:

- Wave 1.13 must follow waves 1.6 + 1.9 + 1.10 + 1.12 (LocalDev references Vcpkg + Preflight + Harvesting + Packaging pipelines — `SetupLocalDevTaskRunner`'s constructor consumes `EnsureVcpkgDependenciesTaskRunner`, `PreflightTaskRunner`, `HarvestTaskRunner`, `ConsolidateHarvestTaskRunner`, and `IPackageTaskRunner` — all four feature folders must exist before LocalDev moves).
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
- [ ] `dotnet test --project build/_build.Tests/Build.Tests.csproj -c Release --framework net10.0` succeeds; test count ≥ P0 baseline.
- [ ] `LayerDependencyTests` green (still ADR-002 invariants, but the ADR-004 destinations also satisfy ADR-002 partially — see §5.4).
- [ ] `smoke-witness local --emit-baseline tmp.json` matches P0 baseline JSON byte-for-byte.
- [ ] Commit message: `refactor(build-host): P1.X migrate <feature> to Features/<X>/ shape (ADR-004)`.

### 5.4 LayerDependencyTests invariants during P1

P1 commits keep `LayerDependencyTests.cs` (ADR-002 shape) running unchanged. The test asserts three invariants over `Build.Domain.*`, `Build.Infrastructure.*`, and `Build.Tasks.*` namespace prefixes. As content migrates to `Build.Features.*`, `Build.Shared.*`, `Build.Host.*`, `Build.Tools.*`, and `Build.Integrations.*`, **the test's source-prefix coverage shrinks naturally** — newly-relocated types no longer match any of the three source prefixes, so they fall outside the test's scope without requiring exclusion lists or per-wave invariant rewrites.

The test stays mechanically green throughout P1 because:

- Migrated types live under prefixes the test does not inspect (`Features.*`, `Shared.*`, `Host.*`, `Tools.*`, `Integrations.*`); violations cannot accumulate against them.
- Not-yet-migrated content (still under `Domain.*` / `Infrastructure.*` / `Tasks.*`) keeps satisfying the original ADR-002 invariants — those types' outgoing references either stay within their original layer or point to types that haven't moved yet (i.e. still in the same legacy prefix).

P1 close leaves `Domain.*` / `Infrastructure.*` / `Tasks.*` empty (or near-empty); the test trivially passes against an empty source set. **P2 atomic rewrite** (§6.4) replaces the file via `git mv` to `ArchitectureTests.cs` with the five ADR-004 §2.13 invariants — exclusion lists are not introduced, removed, or maintained at any point.

**Why the test is preserved through P1 instead of deleted-and-recreated.** During the longest wave (P1, ~18 sub-waves), at least *some* mechanical enforcement of architecture direction is desirable — even if the surviving invariants only cover the shrinking pre-migration tree. Deleting the test now and recreating it at P2 leaves a P1-wide enforcement gap and forces a test-count exception in §2.3 ratchet. The natural-shrinkage path keeps the file alive (test count stable at the P0 baseline of 500) without per-wave maintenance cost. ADR-004's vertical-slice bet relies on architecture-test invariant #4 (`Features` cross-reference ban with LocalDev allowlist); the file *carrying* that bet stays continuous through P1, the *invariants* it carries flip atomically at P2.

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

P2 closed at commit `3ab2e68` on `master`. Original criteria + actual landing state:

- [x] Every `*TaskRunner` class renamed to `*Pipeline` (or `*Flow` for SetupLocalDev) — 16 classes + 2 interfaces + 14 test classes renamed via regex `(?<=\w)TaskRunner\b → Pipeline` plus targeted `(?<=ConsumerSmoke)Runner\b → Pipeline`. Cake-native `LddRunner` / `OtoolRunner` (Cake-Tool naming triad) preserved per ADR-004 §2.10.
- [x] No `*Runner` suffix remains in `build/_build/` outside Cake `Tool<T>` triad files in `Tools/` (where `XRunner.cs` is the canonical Cake-native multi-command tool name).
- [x] Each `Features/<X>/` folder has a `ServiceCollectionExtensions.cs` with `AddXFeature(this IServiceCollection)`. Packaging extension takes a `string source` parameter (CLI `--source` value) because the `IArtifactSourceResolver` factory closure consumes it at composition time. LocalDev registered last in the chain per ADR-004 §2.5 + §2.13 invariant #4 allowlist.
- [x] `Program.cs` composition root reads as the feature roster — 13 `services.AddXFeature()` calls. At P2 close, Tools / Integrations / Host registrations remained inline; Adım 13.9 later grouped them into `AddToolWrappers` / `AddIntegrations` / `AddHostBuildingBlocks`.
- [x] `BuildContext` exposes only `Paths`, `Runtime`, `Manifest`, `Options`. No service properties. No `GetService<T>()` calls in production code. Constructor 7-arg → 5-arg. `ManifestConfig` now flows in as data; `Repo` / `DotNetConfiguration` / `Vcpkg` / `DumpbinConfiguration` removed (consumers route through `Options.X`).
- [x] `LayerDependencyTests.cs` `git mv`'d to `ArchitectureTests.cs`; 5 ADR-004 §2.13 invariants written.
- [ ] Test count ≥ P1 close + 5 `ArchitectureTests` invariants + one `ServiceCollectionExtensions` smoke per migrated feature (per §2.3 / §10.4 formula; expected ~+18 with the current 13-feature roster).
      **PARTIAL** — 502 / 499 passed / 3 skipped at P2 close. Architecture invariants landed (5 added; 3 of them skipped with explicit P3-deadline tracking per §11 risk #4 — see [§14 Adım 13](#14-ad%C4%B1m-13-post-p2-follow-up-wave)). Per-feature `ServiceCollectionExtensions` smoke tests **deferred to Adım 13** alongside the `TestHostFixture.AddTestHostBuildingBlocks()` shared infrastructure that the smokes need. Justification: the smokes' assertion shape ("AddXFeature resolves every type the feature exports as DI-resolvable via the shared host fixture") only carries value once the cross-tier dependency graph stops violating ArchitectureTests #1, #3, #4 — otherwise the smokes either pass against an architecture-violating graph (and lock that violation in) or fail because the violated graph cannot resolve cleanly through the fixture. Adım 13 closes the cross-tier violations first, then the smokes land against the settled graph.
- [x] smoke-witness `local` + `ci-sim` baseline byte-equal to P0 (target names unchanged) — Win local fast-loop MATCH 92.5s, Win ci-sim 9/9 PASS 109.0s, WSL Linux 3/3 PASS 198.7s. macOS Intel skipped (sleep, non-gating per §10.5).
- [x] `Shared/` no Cake dependency invariant — P1 transitional exception closed in step 10:
  - `Shared/Runtime/PlatformFamily` (Cake type) replaced with build-host-local `Build.Shared.Runtime.RuntimeFamily` enum (Windows / Linux / OSX, value names match Cake's enum so `ToString()` callsites stay compatible).
  - `IRuntimeProfile.IsSystemFile(FilePath)` → `IsSystemFile(string fileName)`. Callers extract `path.GetFilename().FullPath` at the seam (`HybridStaticValidator`, `BinaryClosureWalker`, test fixtures).
  - `RuntimeProfile.PlatformFamily` property → `Family` (cleaner, paralels Cake's `ICakePlatform.Family`). 7 production callsites + 9 test callsites updated.
  - Cake-tier code (Tools, Integrations, ArtifactPlanner, CakePlatformExtensions) continues to consume `Cake.Core.PlatformFamily` directly via `ICakePlatform.Family` — that is the Cake-native side of the boundary.

---

## 7. P3 — Interface Review

### 7.1 Goals

Apply ADR-004 §2.9 three-criteria rule to every surviving `I*` interface. Remove interfaces that fail criteria; convert their tests to fixture-based or integration-level patterns. **Bounded scope per interface — no mass deletion.**

### 7.2 Review outcome

P3 reviewed the 32 production `I*` interfaces present after Adım 13. Four interfaces failed ADR-004 §2.9 and were removed in this wave; the remaining 28 have an explicit retention criterion.

Removed interfaces:

| Interface | Replacement | Why removal was correct |
| --- | --- | --- |
| `ICoverageThresholdValidator` | `CoverageThresholdValidator.Validate(...)` static call | Pure stateless pass/fail rule; no alternate implementation axis and no expensive boundary. |
| `IVersionConsistencyValidator` | `VersionConsistencyValidator.Validate(...)` static call | Pure stateless manifest/vcpkg comparison; interface existed only for mocks. |
| `ICoreLibraryIdentityValidator` | `CoreLibraryIdentityValidator.Validate(...)` static call | Pure stateless manifest invariant; interface existed only for mocks. |
| `IStrategyCoherenceValidator` | Concrete `StrategyCoherenceValidator` registered in DI | Single production implementation with one real dependency (`IStrategyResolver`); the interface added no independent seam. |

### 7.3 Retained interface ledger

| Interface | Criterion | Rationale |
| --- | --- | --- |
| `IRuntimeScanner` | 1 | Three production implementations (`dumpbin`, `ldd`, `otool`) selected by host OS. |
| `IPackagingStrategy` | 1 | Hybrid-static and pure-dynamic strategies are real production policy variants. |
| `IDependencyPolicyValidator` | 1 | Hybrid-static enforcement and pure-dynamic pass-through are distinct production policies. |
| `IArtifactSourceResolver` | 1 | Local, RemoteInternal, and unsupported-source resolvers exist today; ReleasePublic is the Phase 2b extension point. |
| `IPackageVersionProvider` | 1 | Manifest, git-tag, and explicit-version providers are all production implementations. |
| `INuGetFeedClient` | 2 | NuGet protocol boundary; callers should not depend on the concrete protocol client. |
| `IDotNetPackInvoker` | 2 | External `dotnet pack` process boundary. |
| `IProjectMetadataReader` | 2 | MSBuild/project metadata read boundary. |
| `IVcpkgManifestReader` | 2 | vcpkg manifest file adapter boundary. |
| `IPackageInfoProvider` | 2 | vcpkg package metadata CLI/cache boundary. |
| `ICoberturaReader` | 2 | Cobertura XML parsing boundary. |
| `ICoverageBaselineReader` | 2 | Coverage baseline file boundary. |
| `IMsvcDevEnvironment` | 2 | MSVC developer environment discovery boundary. |
| `IDotNetRuntimeEnvironment` | 2 | Runtime bootstrap boundary for cross-architecture consumer smoke tests. |
| `IRuntimeProfile` | 2 | Cross-feature runtime capability abstraction consumed by Host, Shared policy, and feature code. |
| `IStrategyResolver` | 2 | Shared triplet↔strategy mapping axis consumed by Preflight and DI factories. |
| `IUpstreamVersionAlignmentValidator` | 2 | Cross-stage G54 guardrail used by Preflight and version providers. |
| `IG58CrossFamilyDepResolvabilityValidator` | 2 | Cross-stage G58 guardrail shared by Preflight and Package; future feed-probe expansion remains isolated. |
| `IPathService` | 2 | Host-tier path abstraction; consumed as a cross-cutting concern by Integrations adapters and feature code. |
| `IBinaryClosureWalker` | 3 | Process-bound/high-cost harvesting seam; unit tests must substitute lower-level retained boundaries instead of spawning native tools. |
| `IArtifactPlanner` | 3 | Filesystem/path-heavy harvesting planner; removal would force noisy fixture rewrites before P4 path reshaping. |
| `IArtifactDeployer` | 3 | Filesystem/archive deployment seam; removal is deferred to avoid real-I/O pressure in unit tests. |
| `ICsprojPackContractValidator` | 3 | XML/project-file validation seam used to keep LocalDev orchestration tests focused. |
| `INativePackageMetadataGenerator` | 3 | Filesystem JSON generation seam in Packaging; tests avoid unnecessary file writes through the interface. |
| `IReadmeMappingTableGenerator` | 3 | Filesystem README generation seam in Packaging; tests avoid unnecessary file writes through the interface. |
| `IPackageOutputValidator` | 3 | Large aggregate package validator; interface keeps task/pipeline tests focused until Packaging is decomposed. |
| `IPackagePipeline` | 3 | LocalDev orchestration seam; P4 `RunAsync(TRequest)` cut-over will touch this boundary. |
| `IPackageConsumerSmokePipeline` | 3 | Task/orchestration seam; P4 `RunAsync(TRequest)` cut-over will touch this boundary. |

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

- [x] Every interface in production code passes one of §2.9 criteria 1, 2, or 3 (transitional debt). See §7.2 and §7.3.
- [x] No interface exists "because mocks reference it" without §2.9 cover.
- [x] Test count change documented per wave commit; total test count drift across P3 is bounded and gerekçeli. P3 keeps the Adım 13 count: 515 → 515.
- [x] **Test wall-time gate (§11 risk #11):** total `dotnet test` wall time at P3 close ≤ (P2 close wall time × 1.20). P3 measured 2.8s wall time locally; no real-I/O seam rewrites were introduced.
- [x] No `Unit/` test spawns real native processes or hits real disk paths (`Path.GetTempPath()`, real `Directory.CreateDirectory`, real `Process.Start` of native CLIs are banned in unit tests). P3 removed only pure/stateless or concrete-DI seams.
- [x] `ArchitectureTests` green as part of the 515-test local suite.
- [x] smoke-witness baseline byte-equal to P0: fast-loop MATCH and milestone-loop MATCH on this Windows host after stale MSBuild process cleanup.

---

## 8. P4 — API Surface Refactors

### 8.1 Goals

Close ADR-004 §2.11.1 migration exception (Pipelines accept BuildContext). Optionally refactor large Pipelines internally for readability.

### 8.2 Pipeline `RunAsync(BuildContext, TRequest)` → `RunAsync(TRequest)` cut-over

Per ADR-004 §2.11.1, Pipelines should consume `Request` DTOs only. Migration leaves Pipelines accepting `BuildContext` as a transitional state; P4 closes that exception.

For each Pipeline:

- [x] Add `Request.From(BuildContext, ...)` factory if the Request needs to capture context-derived state (paths, runtime profile, options).
- [x] Rewrite Pipeline signature: `RunAsync(BuildContext, TRequest, CancellationToken)` → `RunAsync(TRequest, CancellationToken)`.
- [x] Pipeline constructor takes Cake-side dependencies through DI (`ICakeLog`, `IPathService`, etc.) instead of receiving `BuildContext` per-call.
- [x] Cake Task body: `pipeline.RunAsync(context, ...)` → `pipeline.RunAsync(request)`.
- [x] Update Pipeline tests: `RunAsync(BuildContext, TRequest)` → `RunAsync(TRequest)`. Test fixtures construct Request DTOs directly; BuildContext goes from arg to test-internal scaffolding.
- [x] `dotnet test` + smoke-witness green.

### 8.3 Large Pipeline internal refactor (optional within P4)

`PackageConsumerSmokePipeline` (~688 LOC), `HarvestPipeline` (~628 LOC), `PackagePipeline` (~556 LOC) are candidates for internal restructuring per ADR-004 §3 rationale. Each can be broken into smaller per-concern services in the same feature folder. **No new interfaces unless §2.9 criteria justify**; concrete classes with explicit DI registration.

This is **deferred and per-Pipeline judgment-based**. Not all three need to refactor; only when readability burden is real.

### 8.4 P4 success criteria

- [x] No Pipeline accepts `BuildContext` as a parameter to `RunAsync`. ADR-004 §2.11.1 migration exception is closed.
- [ ] Optional: large Pipelines simplified per §8.3.
- [ ] smoke-witness baseline byte-equal to P0 (pending P4 close milestone-loop).
- [x] `ArchitectureTests` green (515/515, 0 skipped).

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

A naive smoke test (`new ServiceCollection().AddPackagingFeature().BuildServiceProvider().GetService<PackagePipeline>()`) **will not resolve** — `PackagePipeline` constructor takes `ICakeLog`, `IPathService`, `ManifestConfig`, `BuildOptions`, validators, and so on. Many of those come from Host / Shared / Tools / Integrations — not from the feature itself.

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
| 5 | **Pipeline `RunAsync(TRequest)` cut-over ripples test fixtures** | Medium | Medium | **CLOSED at P4-A** — 10 Pipelines + 7 test files updated; 515/515 tests green; large Pipeline decomposition deferred to P4-C |
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
- **P4-A commits:** `refactor(build-host): P4-A Pipeline RunAsync(BuildContext, TRequest) → RunAsync(TRequest) cut-over` + body (10 Pipelines, 2 interfaces, 15 Tasks).
- **P5 atomic commits:** `refactor(build-host): P5.X rename Cake target <old> → <new> (ADR-004 §2.14)` + body listing all callsites updated.

### 12.3 Pre-merge checks

For every wave merge, the following must be green:

- [ ] `dotnet build build/_build/Build.csproj -c Release`
- [ ] `dotnet test --project build/_build.Tests/Build.Tests.csproj -c Release --framework net10.0`
- [ ] Test count meets ratchet for that wave (§2.3)
- [ ] **Test wall-time gate (P3 only):** total `dotnet test` wall time ≤ (P2 close wall time × 1.20). Other waves capture wall time for trend visibility but do not gate on it.
- [ ] `LayerDependencyTests` (P0–P1) or `ArchitectureTests` (P2+) green
- [ ] **Fast loop (every wave commit):** `cd tests/scripts && dotnet run verify-baselines.cs` exits zero — i.e. `smoke-witness local` baseline byte-equal to `smoke-witness-local-win-x64.json` (P5 updates baseline).
- [ ] **Milestone loop (P-wave close commits only — P0/P1/P2/P3/P4/P5 close):** `cd tests/scripts && dotnet run verify-baselines.cs --milestone` exits zero, covering `smoke-witness-local-linux-x64.json` (WSL Linux) + `smoke-witness-ci-sim-win-x64.json` (Windows ci-sim). `smoke-witness-local-osx-x64.json` (macOS Intel) is best-effort per §10.5 — captured when host is reachable, not gating; an `osx-arm64` companion will be added when Apple Silicon hardware enters rotation. (P5 updates each baseline atomically per §9.3.)
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
- [`build/_build.Tests/Unit/CompositionRoot/ArchitectureTests.cs`](../../build/_build.Tests/Unit/CompositionRoot/ArchitectureTests.cs) — architecture-direction-of-dependency invariants (renamed from `LayerDependencyTests.cs` at P2 commit `3ab2e68`; 5 invariants per ADR-004 §2.13, all active after Adım 13).
- [`.github/workflows/release.yml`](../../.github/workflows/release.yml) — CI pipeline; P5 atomic-wave updates Cake target names.
- [`docs/playbook/cross-platform-smoke-validation.md`](../playbook/cross-platform-smoke-validation.md) — A-K checkpoint script; P5 atomic-wave updates target name references.
- [`docs/reviews/code-review-conversation.txt`](../reviews/code-review-conversation.txt) — 2026-05-01 critique pass.
- [`docs/reviews/conversation-2.txt`](../reviews/conversation-2.txt) — 2026-05-01 critique extension.
- [`docs/reviews/mycomments.txt`](../reviews/mycomments.txt) — Deniz's pre-finalization patch list.

### 13.2 External

- Cake Frosting documentation — Tool/Aliases/Settings/Runner filename triad preserved at `Tools/`.
- Vertical Slice Architecture (Jimmy Bogard) — feature-cohesion principle.

---

## 14. Adım 13 (post-P2 follow-up wave)

P2 closed at `3ab2e68` with three `ArchitectureTests` invariants explicitly skipped under
phase-x §11 risk #4 ("named exclusion + tracking issue + P3 deadline"). Adım 13 closes
those skips and unblocks the remaining `Tools` / `Integrations` / `Host` extension grouping
that was deliberately deferred from P2. **This wave must close before P3 starts** — P3
interface-review work assumes the architecture invariants are green; running P3 against an
architecture-violating graph would let interface-pruning decisions leak through cross-tier
violations that ADR-004 §2.13 forbids.

### 14.1 Goals

- Move every cross-tier-coupled type that triggers an `ArchitectureTests` violation into the
  layer ADR-004 §2.6 (Shared) prescribes.
- Un-skip the three `ArchitectureTests` invariants and confirm them green against the
  post-Adım-13 production tree.
- Land the per-feature `ServiceCollectionExtensions` smoke tests + shared
  `TestHostFixture.AddTestHostBuildingBlocks()` (phase-x §10.6) — these were deliberately
  deferred from P2 because their pass criterion ("DI graph resolves cleanly via the shared
  host fixture") only carries value once the cross-tier violations are gone.
- Rewrite `docs/knowledge-base/cake-build-architecture.md` from the ADR-002 layered shape
  to the ADR-004 5-folder shape, atomic with the architecture-fix commit (phase-x §1.5
  mid-migration mixed-shape rule allowed the doc to lag through P1/P2 — this wave closes
  the gap).

### 14.2 Cross-tier violation inventory (as of P2 close `3ab2e68`)

| # | Source | Forbidden reference | Resolution |
|---|---|---|---|
| 1 | `Build.Shared.Strategy.HybridStaticValidator` | `Build.Features.Harvesting.BinaryClosure` | Move `BinaryClosure` (and the closure model graph: `BinaryNode`, related result types) to `Build.Shared.Harvesting/`. Strategy validators consume the closure as their input contract — Shared → Shared cross-reference is legitimate. |
| 2 | `Build.Shared.Strategy.IDependencyPolicyValidator` | `Build.Features.Harvesting.BinaryClosure` | Same. |
| 3 | `Build.Shared.Strategy.PureDynamicValidator` | `Build.Features.Harvesting.BinaryClosure` | Same. |
| 4 | `Build.Shared.Strategy.ValidationError` | `Build.Features.Harvesting.BinaryNode` | Same — `BinaryNode` Shared/. |
| 5 | `Build.Shared.Strategy.ValidationResult` | `Build.Features.Harvesting.BinaryNode` | Same. |
| 6 | `Build.Shared.Strategy.ValidationSuccess` | `Build.Features.Harvesting.BinaryNode` | Same. |
| 7 | `Build.Integrations.Coverage.{ICoberturaReader,CoberturaReader}` | `Build.Features.Coverage.CoverageMetrics` | Move `CoverageMetrics`, `CoverageBaseline`, `CoverageCheckResult/Success/Error` model + result types to `Build.Shared.Coverage/`. Adapters depend on Shared vocabulary. Feature-specific orchestration / validators stay in `Features/Coverage/`. |
| 8 | `Build.Integrations.Coverage.{ICoverageBaselineReader,CoverageBaselineReader}` | `Build.Features.Coverage.CoverageBaseline` | Same. |
| 9 | `Build.Integrations.DotNet.{IDotNetPackInvoker,DotNetPackInvoker}` | `Build.Features.Packaging.DotNetPackResult` | Move `DotNetPackResult / DotNetPackError` to `Build.Shared.Packaging/` (or `Build.Shared.DotNet/` if a clearer Shared sub-namespace emerges). |
| 10 | `Build.Integrations.DotNet.{IProjectMetadataReader,ProjectMetadataReader}` | `Build.Features.Packaging.{ProjectMetadataResult,ProjectMetadataError}` | Same — promote project-metadata result/error types to Shared. |
| 11 | `Build.Integrations.Vcpkg.{IPackageInfoProvider,VcpkgCliProvider}` | `Build.Features.Harvesting.PackageInfoResult` | Promote `PackageInfoResult / PackageInfoError` to `Build.Shared.Harvesting/` (alongside the closure types). |
| 12 | `Build.Integrations.DotNet.DotNetPackInvoker` | `Build.Host.Paths.IPathService` | Accepted as a permanent named exception. `IPathService` is the canonical Host-tier path abstraction that Integrations adapters may consume; the BuildPaths fluent split originally scoped to P4 §8.3 was discarded on 2026-05-02. |
| 13 | `Build.Integrations.Vcpkg.VcpkgCliProvider` | `Build.Host.Paths.IPathService` | Same. |
| 14 | `Build.Features.Packaging.DotNetPackResult` | `Build.Features.Harvesting.Unit` | Promote `Unit` (or whichever `Build.Features.Harvesting.*` value type leaks into `DotNetPackResult`) to Shared. Likely an `OneOf`/`Result` discriminator. |
| 15 | `Build.Features.Packaging.PackagePipeline` | `Build.Features.Harvesting.HarvestManifest` | Promote `HarvestManifest` to `Build.Shared.Harvesting/`. Both Packaging and Harvesting consume the manifest as a cross-stage data contract — textbook Shared vocabulary. |
| 16 | `Build.Features.Preflight.PreflightPipeline` | `Build.Features.Packaging.IG58CrossFamilyDepResolvabilityValidator` | Two options: (a) move the G58 validator interface to `Build.Shared.Packaging/` (treating it as a cross-feature seam — §2.9 criterion 2 axis: G58 evaluation could be extracted into its own pipeline future-state); (b) move the Preflight callsite to Adım 13's parallel `Adım-13b` micro-fix that re-shapes the Preflight `ValidationGroup` so it lives behind a Shared abstraction. Decision per Adım-13 inventory. |
| 17 | `Build.Features.Preflight.PreflightReporter` | `Build.Features.Packaging.G58CrossFamilyValidation` | Same — promote the G58 result type to Shared. |
| 18-20 | `Build.Features.Versioning.{ExplicitVersionProvider, GitTagVersionProvider, ResolveVersionsPipeline}` | `Build.Features.Preflight.IUpstreamVersionAlignmentValidator` | Promote `IUpstreamVersionAlignmentValidator` to `Build.Shared.Versioning/` (or a co-located `Shared.Preflight/` if the upstream-alignment concern is preflight-specific). It is a multi-impl §2.9-criterion-1 seam (manifest version provider + git-tag provider + explicit provider all consume it) — promotion is structurally clean. |

Total: 26 individual violations across 20 callsites (some multi-violation, e.g. `DotNetPackInvoker` has two — one Feature + one Host). Resolution promotes ~10 model/result/interface types to a new `Build.Shared.<X>/` sub-namespace each.

### 14.3 Adım-13 sub-step plan (proposed; commit-ready order)

1. **Adım 13.1 — Shared/Harvesting promote.** Move `BinaryClosure`, `BinaryNode`, `HarvestManifest`, `PackageInfoResult / PackageInfoError`, related closure-graph types from `Features/Harvesting/` to `Shared/Harvesting/`. Adjust `Shared/Strategy/*` callsites to consume Shared. Build clean + fast-loop MATCH.
2. **Adım 13.2 — Shared/Coverage promote.** Move `CoverageMetrics`, `CoverageBaseline`, `CoverageCheckResult / Success / Error` to `Shared/Coverage/`. `Integrations/Coverage/*` adapters consume Shared. Build clean + fast-loop MATCH.
3. **Adım 13.3 — Shared/Packaging promote (subset).** Move `DotNetPackResult / DotNetPackError`, `ProjectMetadataResult / ProjectMetadataError`, G58 result + interface to `Shared/Packaging/`. Decide G58 interface location with same commit. `Integrations/DotNet/*`, `Features/Preflight/*` callsites adjusted. Build clean + fast-loop MATCH.
4. **Adım 13.4 — Shared/Versioning promote.** Move `IUpstreamVersionAlignmentValidator` (and its `UpstreamVersionAlignmentResult`) to `Shared/Versioning/`. `Features/Versioning/*` provider chain consumes Shared. Build clean + fast-loop MATCH.
5. **Adım 13.5 — Integrations Host-decoupling.** Accepted as a permanent named exception. `IPathService` is the canonical Host-tier path abstraction that Integrations adapters may consume. The decision at Adım 13.5 was to defer with a named exception on `Integrations_Should_Have_No_Feature_Dependencies`; the BuildPaths fluent split was later discarded on 2026-05-02. The allowlist entries are permanent. Build clean + fast-loop MATCH.
6. **Adım 13.6 — Un-skip 3 ArchitectureTests invariants.** Remove `[Skip(...)]` annotations; assert green. Test count target: 502 → 502 (same, since skip→pass within the same test methods). Wave-close gating only.
7. **Adım 13.7 — Per-feature ServiceCollectionExtensions smoke tests × 13.** Land `TestHostFixture.AddTestHostBuildingBlocks()` shared infrastructure first (Cake fakes + Shared vocabulary fakes + integration substitutes), then add `Add<X>Feature_Should_Register_All_Pipeline_And_Validator_Types` smoke per feature. Test count target: 502 → ~515 (+13 smokes; phase-x §10.4 P2 close ratchet expectation met retroactively).
8. **Adım 13.8 — `cake-build-architecture.md` ADR-004 rewrite.** Atomic same-commit update of the canonical architecture doc — replace ADR-002 `Tasks/Application/Domain/Infrastructure` tree description with ADR-004 `Host/Features/Shared/Tools/Integrations` shape, including the LocalDev orchestration-feature exception, the Pipeline / Flow vocabulary, and the BuildContext discipline rule. Other docs (AGENTS.md, CLAUDE.md, onboarding.md) updated only if they carry stale ADR-002 narrative; most already point at ADR-004 from prior batches.
9. **Adım 13.9 — Optional: `AddToolWrappers` / `AddIntegrations` / `AddHostBuildingBlocks` extension grouping.** Now that the cross-tier dependencies are settled, the inline registrations in `Program.cs` can collapse into three more extension methods in the `services.AddXFeature()` chain, completing the ADR-004 §2.12 composition-root architectural-index pattern. Optional — Program.cs is already readable post-P2; this is polish.

### 14.4 Adım-13 success criteria

- [x] 6 cross-tier types promoted to `Shared/Harvesting/`; `Shared.Strategy → Shared.Harvesting` references compile + green.
- [x] 4 cross-tier types promoted to `Shared/Coverage/`; `Integrations/Coverage` adapters consume Shared.
- [x] 6 cross-tier types promoted to `Shared/Packaging/`; `Integrations/DotNet` + `Features/Preflight` callsites consume Shared. G58 interface placement documented.
- [x] `IUpstreamVersionAlignmentValidator` (+ result) promoted to `Shared/Versioning/`; Versioning provider chain consumes Shared.
- [x] `Integrations/{DotNet,Vcpkg}` IPathService coupling resolved as a permanent named exception (fluent split discarded 2026-05-02).
- [x] All 5 `ArchitectureTests` invariants green (no `[Skip]` annotations; the 2 IPathService rows are documented as a permanent named exception inline).
- [x] 13 `ServiceCollectionExtensions` smoke tests green via shared `TestHostFixture.AddTestHostBuildingBlocks()`. Test count ≥ 515.
- [x] `cake-build-architecture.md` rewritten to ADR-004 shape; AGENTS.md / CLAUDE.md / onboarding.md cross-references audited.
- [x] (Optional) `AddToolWrappers / AddIntegrations / AddHostBuildingBlocks` extensions land; Program.cs DI section reads as 16 `AddX*()` calls instead of 13 + 10 inline.
- [x] smoke-witness fast-loop MATCH at every Adım-13 sub-step commit; milestone-loop (WSL Linux + Win ci-sim, plus macOS Intel if reachable) MATCH at Adım-13 close.

### 14.5 Adım-13 risks

- **Type-promotion ripple.** Each promoted type has 5–20 callsites. Rider's "Move to Namespace" + Adjust Namespaces handle most of it cleanly; manual touch-up needed for adapter constructors and DI registrations.
- **G58 placement decision.** Whether the validator interface itself moves to Shared or stays in Packaging behind a Preflight-side facade is a real design call. Recommend deciding atomic with sub-step 14.3.3.
- **IPathService Host-coupling.** Two options were evaluated — either type-decouple Integrations from Host (which would require a temporary record adapter), or accept the IPathService rows as a named exception. The decision at Adım 13.5 was to defer with a named exception, and the fluent split was later discarded on 2026-05-02. The exception is now permanent.
- **`TestHostFixture` infrastructure cost.** Per phase-x §10.6, the shared fixture must register Cake fakes (`ICakeLog`, `ICakeContext`, `IFileSystem` via `FakeFileSystem`), Host singletons (`BuildPaths`, `RuntimeProfile`, `ManifestConfig`, `BuildOptions`), and substitutes for every Tool / Integration the features transitively need. The fixture itself is a multi-hundred-line file; budget for its construction explicitly.

### 14.6 What stays out of Adım 13

- Interface review (P3) — Adım 13 does **not** prune any `I*` interface; it only relocates types. P3 §2.9 review starts only after Adım 13 closes green.
- Pipeline `RunAsync(BuildContext, TRequest)` → `RunAsync(TRequest)` cut-over (P4 §8.2) — Adım 13 stays at the P2 invocation shape; signature evolution belongs to P4.
- `IPathService` fluent split — discarded on 2026-05-02. `IPathService` remains the canonical Host-tier path abstraction; the 50+-member interface split into `BuildPaths` sub-groups didn't justify its churn cost.
- Cake target rename atomic-wave (P5 §9) — naming cleanup remains the last step of the migration arc.

---

## 15. Change log

| Date | Change | Editor |
| --- | --- | --- |
| 2026-05-02 | Initial draft after ADR-004 finalization; Turn 3 deliverable from the 2026-05-01 to 2026-05-02 build-host refactor synthesis session | Deniz İrgin + collaborative critique synthesis |
| 2026-05-02 | Same-day pre-finalization patch wave (12 patches): §1.3 CLI surface contradiction (target rename + `--source` narrowing exception); §1.5 Mechanical vs structural waves table added; §4.2 baselines committed under `tests/scripts/baselines/` per-host (TBD closed); §5.2 P1.2 Coverage ambiguity removed; §5.2 P1.13 ArtifactSourceResolvers stay in Packaging per ADR-004 §2.3 (LocalDev consumes via §2.13 invariant #4 allowlist); §5.2 P1.15 CakeExtensions split moved to P2 (P1 stays mechanical); §6.2 sub-validator DI injection deliverable explicit (ADR-004 §1.1.6 anti-pattern fix); §6.2 CakeExtensions split deliverable added to P2; §7.4 + §7.5 + §11 risk #11 + §12.3 P3 test wall-time gate (≤ 1.20× P2 baseline) — addresses concrete-impl I/O test inflation risk; FakeFileSystem mandatory for filesystem-bound seam rewrites; §9.5 P5 "four renames" → "three" + rolling baseline note; §10.6 ServiceCollectionExtensions smoke tests use shared `TestHostFixture.AddTestHostBuildingBlocks()`; §11 risk #4 Shared no-Cake transitional exception sertleştirildi (named exclusion + tracking issue + P3 deadline); §2.3 + §10.4 + §6.5 test count ratchet hard sayıdan formüle dönüştü | Deniz İrgin |
| 2026-05-02 | P0-kickoff session refinement: §2.1.5 fast/milestone loop cadence introduced (Win local fast-loop per wave commit; WSL Linux + Windows ci-sim milestone loop at P-wave close commits; macOS opt-in per §10.5 / not gating); §4.2 deliverables table + §4.3 success criteria absorb `verify-baselines.cs` file-based-app helper (`tests/scripts/verify-baselines.cs`, .NET 10 SDK directory-scope via `tests/scripts/global.json`); §5.2 wave 1.13 prerequisites extended from `1.9 + 1.10 + 1.12` to `1.6 + 1.9 + 1.10 + 1.12` reflecting `SetupLocalDevTaskRunner` ctor inventory (`EnsureVcpkgDependenciesTaskRunner`, `PreflightTaskRunner`, `HarvestTaskRunner`, `ConsolidateHarvestTaskRunner`, `IPackageTaskRunner`); §5.4 `LayerDependencyTests` strategy reframed from Decision-A "exclusion-list-grows-monotonically" to natural-prefix-shrinkage (test silently no-ops as content migrates, no exclusion list maintenance, atomic rewrite at P2 §6.4 — preserves test count and P1-wide mechanical enforcement coverage); §12.3 pre-merge checks split into fast-loop (every wave commit) vs milestone-loop (P-wave close commits only) entries | Deniz İrgin (+ P0-kickoff session refactor) |
| 2026-05-02 | P0 + P1 + P2 closed across commits `b18002f`, `651ac2f`, `e602b6c`, `b6de515`, `3ab2e68`. Doc sweep: top-of-doc Wave-Status Snapshot table added; §4.3 P0 success criteria checked off with closure metrics (Win 3/3 PASS 101.8s, Linux 3/3 PASS 184.0s, ci-sim 9/9 PASS 119.2s, macOS 3/3 PASS 145.7s, test-count 500, cake-targets 20); §6.5 P2 success criteria annotated with [x]/[ ] and post-P2 deferred items called out (ServiceCollectionExtensions smokes + cross-tier violation cleanup + cake-build-architecture.md doc rewrite all moved to Adım 13); §13 references updated to point at the `ArchitectureTests.cs` rename. **§14 Adım 13 (post-P2 follow-up wave) added** with full inventory of 26 cross-tier violations from `ArchitectureTests` skip output (Shared/Strategy → Features/Harvesting types, Integrations/Coverage \| DotNet \| Vcpkg → Features.* result types, Integrations/{DotNet,Vcpkg} → Host/Paths/IPathService, Features cross-references for HarvestManifest + G58 + IUpstreamVersionAlignmentValidator), 9-step sub-plan (Shared/{Harvesting,Coverage,Packaging,Versioning} promotes + IPathService decouple + un-skip + smokes + cake-build-arch doc rewrite + optional Tools/Integrations/Host extension grouping), and explicit "must close before P3" gate. §14 prior content (former change-log) renumbered to §15. **Session learnings folded in**: smoke-witness silent-mode log-write fix, verify-baselines `BuildEntries` dedup, Mac SSH liveness-probe pattern, lingering-dotnet-process flake mitigation (build-server shutdown ritual), WSL `wsl zsh -c` absolute-path bind requirement (PWD-leakage workaround already documented in cross-platform-smoke-validation.md §504-540 — referenced from there). | Deniz İrgin (+ P2-close session sweep) |
| 2026-05-02 | P3 interface review close-ready sweep: 32 production `I*` seams audited against ADR-004 §2.9; four stateless/mock-only seams removed; retained interfaces classified under criteria 1/2/3; P3 success criteria checked with 515/515 tests, fast-loop MATCH, and Windows milestone-loop local + ci-sim MATCH after stale MSBuild file-lock cleanup. | Deniz İrgin + Codex |
| 2026-05-02 | **P3 + P4-A CLOSED at `d1127e4`.** P3: 4 interfaces removed, 28 retained with criterion labels. **VcpkgBootstrapTool relocated** from `Tools/Vcpkg/` → `Integrations/Vcpkg/`. **IPathService fluent split permanently discarded** — every canonical doc updated. **P4-A Pipeline RunAsync cut-over**: 11 Pipelines + 2 interfaces + 15 Tasks updated; ADR-004 §2.11.1 migration exception closed; CoverageCheckPipeline (sync, 11th) caught by adversarial review. **Cross-platform verification at commit `d1127e4`**: 515/515 tests on all 3 hosts; Win local MATCH 88.4s + ci-sim MATCH 107.4s; WSL Linux local MATCH 77.0s + ci-sim 9/9 PASS 82.4s; macOS Intel local MATCH 122.3s + ci-sim 9/9 PASS 143.2s. Review fixes: p4DeferredAllowlist → permanentIntegrationsAllowlist, HarvestPipeline whitespace normalized, stale doc echoes fixed, linter-regressed Probe.cs `using SDL2;` restored. | Deniz İrgin + Codex |
