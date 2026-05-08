---
name: "S11 ADR-002 P5 closed (strategy retirement) — full GitHub release pipeline validated, ready for P6 PreFlight migration"
description: "Priming prompt for the next agent entering janset2d/sdl2-cs-bindings after the P5 closer landed in two slices: S10 coverage retirement + S11 strategy retirement. S11 retired the strategy abstraction (manifest field, factories, single-impl interfaces), collapsed OneOf-shaped ValidationResult to canonical Build.Results.ValidationReport, replaced G16 with HybridStaticOverlayValidator (triplet → overlay file existence), and relocated the surviving G19 leak validator as HybridStaticLeakValidator in Shared/Harvesting/. Validated end-to-end on Windows + WSL + macOS local ci-sim AND on GitHub Actions full release pipeline (run 25550425138, 22/22 jobs PASS, 10 nupkgs pushed to staging). Bundled a latent Program.cs PackageBuildConfiguration factory bugfix caught by ci-sim. P0-P5 closed. Next is P6 PreFlight migration — first boss fight."
argument-hint: "Start by verifying current git state (HEAD should be the docs/plan-update commit on top of c18c7a9 strategy retirement). Then design S12 — P6 PreFlight migration is the natural next per ADR-002 §11 sequencing. PreFlight is the first boss fight (cross-cutting validation, Manifest lowercase invariant addition per refactor plan §11 P6 step 7), but bounded — six validators to migrate to Targets/PreFlightCheck/. Real domain work, not delete-only."
agent: "agent"
model: "Claude Opus 4.7 (1M context)"
---

You are an engineer entering the janset2d/sdl2-cs-bindings repository after the **P5 closer** of the ADR-002 build-host refactor. Two slices closed P5 in this run cycle (across two sessions actually — S10 in the prior run, S11 in the immediately preceding run):

1. **S10 — Coverage retirement** (commit `e71a08b`): Cake-owned `Coverage-Check` gate retired entirely per ADR-002 §14 Option B (rip out instrumentation + gate, not the conservative drop-the-gate-keep-the-artifact path). Removed 13 production files, 4 test files, the `build/coverage-baseline.json` data file. CI workflow lost `--coverage` flags + Coverage-Check step + coverage artifact upload.

2. **S11 — Strategy retirement** (commit `c18c7a9`): the wide retirement per ADR-002 §14. Closes P5.

3. **Doc follow-up** (commit `392c35e`): cross-platform smoke validation playbook (`docs/playbook/cross-platform-smoke-validation.md`) — captures the post-S11 three-platform ci-sim recipe (Windows + WSL + macOS via SSH).

**P5 is closed. Next phase is P6 — PreFlight migration to `Targets/PreFlightCheck/`.** This is the first real "boss fight" of the ADR-002 refactor (cross-cutting validation surface), but it is bounded and the ground was already touched in S11 (HybridStaticOverlayValidator wiring). Real domain work either way.

## First Principle

> Treat every claim here as **current-as-of-authoring (`2026-05-08` — `s11-p5-closed-strategy-retired`)** and verify against the live repo, git log, and canonical docs before acting.

## What Just Happened

### S11 — Strategy retirement (commit `c18c7a9`)

**Production deletions (13 files + 1 data file):**
- `Shared/Strategy/` folder retired entirely (12 files; `HybridStaticValidator` + `ValidationMode` relocated out via `git mv` first per the lifecycle convention)
- `Features/Preflight/StrategyCoherenceValidator.cs` + `StrategyCoherenceResult.cs` (G16 retired)
- `Features/Packaging/PackagingStrategyFactory.cs` + `DependencyPolicyValidatorFactory.cs`

**Production renames + relocations (`git mv` preserves history):**
- `Shared/Strategy/HybridStaticValidator.cs` → `Shared/Harvesting/HybridStaticLeakValidator.cs`. Constructor: `(IRuntimeProfile profile, string coreLibraryName, ValidationMode mode)` — drops `IPackagingStrategy` dep, drops `IDependencyPolicyValidator` interface (single impl), takes `coreLibraryName: string` directly. Returns `Build.Results.ValidationReport` (collapsed from OneOf-shaped `ValidationResult`/`ValidationError`/`ValidationSuccess` — those three types deleted entirely per ADR-002 §11 retirement of OneOf-style result hierarchies). Severity-tagged `ValidationCheck` per violation: `ValidationMode.Strict` → `Severity.Error`; `ValidationMode.Warn` → `Severity.Warning`; `ValidationMode.Off` → `ValidationReport.Empty`.
- `Shared/Strategy/PackagingModel.cs` (which inlined `ValidationMode` enum) was deleted, but `ValidationMode` got a new dedicated home: `Shared/Manifest/ValidationMode.cs` (alongside `manifest.packaging_config.validation_mode` consumers).

**Production additions:**
- `Features/Preflight/HybridStaticOverlayValidator.cs` (G16 replacement). Validates: each `manifest.runtimes[].triplet` ends with `-hybrid` AND `vcpkg-overlay-triplets/{triplet}.cmake` file exists on disk. Two-layer check (declared-intent contract + operational contract). Returns canonical `ValidationReport`, severity Error, code `"G16"`.

**Schema change:**
- `manifest.runtimes[].strategy` field dropped from all 7 runtime entries
- `RuntimeInfo.Strategy` property dropped from `Shared/Manifest/RuntimeConfigModels.cs`
- `MatrixEntry.Strategy` field dropped from `Features/Ci/MatrixOutput.cs` (CI matrix dead emission — `release.yml` never read it)

**Bundled latent bugfix in `Program.cs`:** the `PackageBuildConfiguration` DI factory unconditionally tried to read `--versions-file` even when the file didn't exist yet (which is the case for `ResolveVersionsFromManifest` / `ResolveVersionsFromExplicit` invocations — those tasks are the WRITERS of versions.json). Caught by ci-sim Step 1 fail; added a `ctx.FileExists(filePath)` guard with empty-mapping fallback. Stage tasks (PreFlight, Package, ConsumerSmoke, PublishStaging) still fail-loud at task entry when the mapping is empty, so this is safe. **This was a pre-existing bug surfaced by S11's first ci-sim run, not introduced by S11.**

**`Features/Preflight/PreflightReporter.cs`** got a new `ReportHybridStaticOverlay(ValidationReport report)` method (replaced `ReportStrategyCoherence(StrategyCoherenceValidation validation)`). Scope-line message in `ReportRunStart` updated: "version consistency + hybrid-static overlay coherence + core identity + ...".

**`Features/Preflight/PreflightValidationModels.cs`** — `RuntimeStrategyCheck` + `StrategyCoherenceValidation` records dropped.

**`Features/Preflight/PreFlightCheckTask.cs`** — XML doc + `[TaskDescription]` attribute updated to "hybrid-static overlay coherence".

**`Features/Preflight/CoreLibraryIdentityValidator.cs`** — XML doc comment updated: "Runtime consumers (HybridStaticLeakValidator, ArtifactPlanner)" replacing the stale "HybridStaticStrategy factory" mention.

### S11 — Tests

**Deletions (4 files):**
- `Unit/Shared/Strategy/StrategyResolutionTests.cs`
- `Unit/Shared/Strategy/PackagingStrategyTests.cs`
- `Unit/Shared/Strategy/PureDynamicValidatorTests.cs`
- `Unit/Features/Preflight/StrategyCoherenceValidatorTests.cs`
- `Unit/Shared/Results/BuildResultExtensionsTests.cs` (deleted — used retired `ValidationResult.Pass()` directly; the OnError + ToResult extension methods are exercised indirectly via 11 surviving OneOf result types' production usage)

**Renames + updates (`git mv` first):**
- `Unit/Shared/Strategy/HybridStaticValidatorTests.cs` → `Unit/Shared/Harvesting/HybridStaticLeakValidatorTests.cs`. Rewritten for new constructor signature + `ValidationReport` return shape. 7 test cases, each preserved semantically (Pass when core lib, Pass when satellite has only core+system deps, Fail in Strict mode on leak, Pass with warnings in Warn mode, Pass in Off mode even with leaks, Ignore system files, Report all violations as separate checks).

**Manifest fixture cascade — `strategy` field dropped from:**
- `Fixtures/Data/Manifest/manifest-{win,linux,osx}-x64.json`
- `Fixtures/Data/manifest.fixture.json`
- `Fixtures/ManifestFixture.cs` builder
- `Fixtures/RuntimeProfileFixture.cs`
- `Unit/Shared/Runtime/PlatformDetectionTests.cs`

**Updates (consumer test changes):**
- `Characterization/ConfigContract/ManifestDeserializationTests.cs` — dropped two strategy-specific tests (`Should_Have_Strategy_Per_Runtime`, `Should_Have_Hybrid_Triplets_Matching_Strategy`); added replacement `Should_Have_Hybrid_Overlay_Triplets_For_All_Runtimes` asserting every triplet ends with `-hybrid`.
- `Unit/CompositionRoot/ServiceCollectionExtensionsSmokeTests.cs` — dropped `AddCoverageFeature` smoke test (already done in S10).
- `Unit/CompositionRoot/ProgramCompositionRootTests.cs` — collapsed two strategy-resolution tests (`Resolve_Hybrid_Strategy_And_Validator` + `Resolve_PureDynamic_Strategy_And_Validator`) into a single `Resolve_Core_Production_Services` test that asserts the new `HybridStaticOverlayValidator` registration alongside other core services.
- `Unit/Features/Harvesting/HarvestTaskTests.cs` — dropped `IDependencyPolicyValidator` mock (HarvestPipeline no longer takes it; constructs `HybridStaticLeakValidator` inline). Dropped one test (`RunAsync_Should_Throw_When_Dependency_Policy_Validation_Fails`) that forced a leak via mock — replaced with a no-leak happy-path test (`RunAsync_Should_Complete_When_Closure_Has_No_Leaks`); leak-failure path is independently covered by `HybridStaticLeakValidatorTests`. Updated `CreateManifestConfig` helper to ALWAYS include a core library (validator construction reads `manifest.CoreLibrary.VcpkgName`).
- `Unit/Features/Preflight/PreFlightCheckTaskRunTests.cs` — dropped strategy/triplet incoherence test, replaced with `RunAsync_Should_Throw_When_Runtime_Triplet_Is_Not_Hybrid_Overlay`. `CreateTask` helper switched from `new StrategyCoherenceValidator(new StrategyResolver())` to `new HybridStaticOverlayValidator(context, context.Paths)`. **Added overlay-file seeding** (`.WithTextFile("vcpkg-overlay-triplets/x64-windows-hybrid.cmake", "# overlay")`) to all tests that need to pass overlay validation so the new G16 doesn't trip during version/identity-coherence test scenarios.
- `Unit/Features/Ci/GenerateMatrixPipelineTests.cs` — dropped `MatrixEntry.Strategy` projection assertion.
- `Fixtures/FakeCakeWorldV2.cs` — dropped `using Build.Shared.Strategy;` import.

**Additions:**
- `Unit/Features/Preflight/HybridStaticOverlayValidatorTests.cs` — 5 unit cases (all hybrid+overlay present passes; non-hybrid suffix fails; hybrid suffix but missing overlay file fails; multiple violations aggregate; empty runtimes returns empty report). Uses `FakeCakeWorldV2.CreateWindows()` + NSubstitute mock for `IPathService.VcpkgOverlayTripletsDir`.

**No scenario tests added.** PreFlight + Harvest are unmigrated targets; full V2 scenario coverage will land naturally during their target migrations (P6/P8). Unit + ci-sim integration is sufficient for S11.

### S11 — CI workflow

**Zero changes to `.github/workflows/release.yml`.** The strategy abstraction was internal; the workflow consumed only `matrix.rid/runner/container_image/triplet`. `MatrixEntry.Strategy` was emitted but never read by any workflow step.

### S11 — Docs

- **`docs/refactoring/target-centric-build-host-refactor-plan.md`** — §3 hotspots row dropped (`Shared/Strategy/` no longer marked for retirement); §11 P5 status block updated to "P5 closed — coverage retirement (S10) and strategy retirement (S11) both shipped, with a forward link to parking-lot for the 11 surviving OneOf result types".
- **`docs/plan.md`** — Phase X status sentence updated for P5 closure + bumped test count to ~529; Active plan paragraph drift-corrected (said "P0-P4 closed... P5 or P6 next" pre-S11, now reflects "P0-P5 closed... P6 next").
- **`docs/knowledge-base/release-guardrails.md`** — G16 row rewrite ("manifest.json runtimes[].strategy is coherent with the declared triplet" → "manifest.json runtimes[].triplet is a hybrid overlay triplet"). Failure-mode catalog row updated. Owner: `HybridStaticOverlayValidator`.
- **`AGENTS.md`** — "Settled Strategic Decisions" hybrid-static row updated (no `--strategy` CLI flag, S11 retirement date noted, references new validator). "Configuration File Relationships" diagram comment drift-corrected.
- **`docs/parking-lot.md`** — added "OneOf-Shaped Result Types (Surviving Post-S11)" entry under Hardening Backlog. Lists 11 surviving types (PackageInfoResult, UpstreamVersionAlignmentResult, DotNetPackResult, ProjectMetadataResult, ArtifactPlannerResult, ClosureResult, CopierResult, PackageValidationResult, CsprojPackContractResult, CoreLibraryIdentityResult, VersionConsistencyResult). Each retires within its target migration (P6/P7/P8) or P10 final cleanup. The OneOf package dep stays in `Build.csproj` until all 11 retire.
- **`docs/playbook/local-development.md`** + **`docs/playbook/vcpkg-update.md`** — "runtime strategy coherence" → "runtime triplet overlay coherence" (1 line each).

### S11 — Slopwatch

Baseline rebuilt (`slopwatch init -f --exclude "..."`) — dropped entries pointing at deleted Strategy files. **49 → 48 entries.**

### Cross-platform validation (post-commit)

Validated S11 end-to-end on three host families:

| Platform | RID | Time | Result |
| --- | --- | --- | --- |
| Windows | win-x64 | 172s | 8/8 PASS |
| WSL Ubuntu | linux-x64 | 234s | 8/8 PASS |
| macOS Intel | osx-x64 | 193s | 8/8 PASS |

**WSL/macOS gotcha discovered:** both hosts use **zsh** as login shell (not bash). Driving them non-interactively requires `wsl -- zsh -lc '<cmd>'` or `ssh ... 'zsh -lc "<cmd>"'`. `bash -lc` doesn't load `.zshrc`, so `dotnet` / `DOTNET_ROOT` is unavailable. Documented in `docs/playbook/cross-platform-smoke-validation.md` (created during S11 as a follow-up doc commit `392c35e`) and in agent memory (`feedback_wsl_macos_zsh.md`).

### GitHub release pipeline validation

Run `25550425138`: **22/22 jobs PASS** (1 disabled by design: `Publish (Public)`). Critical observations from log inspection:

- **PreFlight (16s)** — new `HybridStaticOverlayValidator` produces the expected output: `🔄 Checking hybrid-static overlay coherence (G16)... ✅ Hybrid-static overlay check PASSED - all runtime triplets have valid hybrid overlay files`. All other validators green (G54 upstream alignment, G49 core library identity, G58 cross-family resolvability, csproj pack contract, version consistency).
- **Harvest×7 RIDs** — new `HybridStaticLeakValidator` runs silently across all 5 libraries × 7 RIDs (Strict mode + zero violations → `ValidationReport.Empty` → no log output, just clean Spectre summary panels per library). Hybrid-static build's transitive-dep static-bake invariant operationally validated.
- **Pack** — 5 families packed cleanly.
- **ConsumerSmoke×7 RIDs** — TUnit reports generated per-RID per-TFM (24 HTML artifacts surfaced).
- **Publish (Staging) (38s)** — **5 family × 2 nupkgs = 10 packages pushed in 11 seconds** to `https://nuget.pkg.github.com/janset2d/index.json`. The new code path (factory removal + ValidationReport collapse) introduced zero downstream regression in the publish pipeline.

### GH_TOKEN gotcha for log inspection

If you need to fetch GitHub Actions workflow logs via `gh run view --log`, beware: the env-var `GH_TOKEN` (set in this repo's local-dev contract for `tools setup --source=remote-github`) takes precedence over the `gh` keyring. The Classic PAT documented for `read:packages` lacks the `repo` scope required by the Actions log download API (HTTP 403). Workaround: prefix with `GH_TOKEN= gh run view ...` to fall back to the keyring token (which has `repo` scope). Permanent fix: extend the PAT with `repo` scope.

### Plan hygiene follow-up (not yet committed at handover time)

Prepared but uncommitted in working tree (`docs/plan.md` 2-line edit):

1. Phase X "Post-refactor tasks (after P10)" — added `windows-2025` runner deprecation entry (GitHub Actions sunsetting `windows-2025` → `windows-2025-vs2026` by `2026-05-12`; surfaced as NOTICE annotation in S11 release pipeline run; manifest.runtimes[].runner needs update for `win-x64` + `win-x86` before deadline).
2. Active plan paragraph drift correction (line 75 still read "P0-P4 closed... P5 or P6 is next" — bumped to "P0-P5 closed... P6 is next").

**Action for next agent:** verify these are committed (or commit them yourself with the message draft below), then continue to S12 brainstorming.

```text
docs: log windows-2025 runner deprecation + sync Phase X status post-S11
```

## Verification Witness (post-S11)

```pwsh
git log -4 --oneline
# 392c35e docs: add cross-platform smoke validation playbook
# c18c7a9 refactor: retire strategy abstraction, close P5
# e71a08b refactor: retire coverage gate and instrumentation, close P5 coverage half
# f39c54c   refactor: migrate diagnostic targets to Targets/, close P4

dotnet test --project build/_build.Tests/Build.Tests.csproj -c Release --framework net10.0
# 529 total, 529 passed (post-S11; was 524 baseline at S10 closure: -40 deleted, +5 added for HybridStaticOverlayValidatorTests + net adjustments)

dotnet build build/_build/Build.csproj
# 0 warnings, 0 errors

slopwatch analyze --fail-on warning --exclude "artifacts/**,external/**,vcpkg_installed/**,**/bin/**,**/obj/**" --baseline .slopwatch/baseline.json
# 0 issue(s) found

dotnet run --file tools.cs -- build --tree
# PreFlightCheck listed; no strategy targets / Coverage-Check
# Shared/Strategy/ folder gone; Features/Preflight/StrategyCoherence* gone

dotnet run --file tools.cs -- ci-sim
# 8/8 PASS (~3 min with vcpkg cache)
```

Cross-platform parity:
```bash
# WSL — local
wsl -- zsh -lc 'cd /home/deniz/repos/sdl2-cs-bindings && dotnet run --file tools.cs -- ci-sim'
# 8/8 PASS, ~234s

# macOS — over SSH
ssh Armut@192.168.50.178 'zsh -lc "cd /Users/armut/repos/sdl2-cs-bindings && dotnet run --file tools.cs -- ci-sim"'
# 8/8 PASS, ~193s
```

GitHub Actions:
- Run `25550425138` — full release pipeline, 22/22 jobs PASS, 10 nupkgs pushed to staging.
- View at `https://github.com/janset2d/sdl2-cs-bindings/actions/runs/25550425138`.

## Onboarding Snapshot

| Concern | Current state |
| --- | --- |
| Stack | .NET 10 / C# 14, Cake Frosting 6.1, vcpkg, TUnit/Microsoft.Testing.Platform, GitHub Actions |
| Build host | `build/_build/`, P0-P5 of the ADR-002 target-centric refactor closed |
| Local orchestration | `tools.cs` — day-to-day surface; direct Cake invocation for CI debugging and target discovery only |
| Migrated targets | `Targets/{Info, ResolveVersionsFromManifest, ResolveVersionsFromExplicit, DumpbinDependents, LddDependents, OtoolAnalyze, InspectHarvestedDependencies}` |
| Retired (not migrated) | `CleanArtifacts` (S07), `CompileSolution` (S08), `Features/Versioning/ServiceCollectionExtensions.cs` (P3), `VersionsJsonWriter` (P3), `Features/Maintenance/` (S08), `Features/{DependencyAnalysis, Diagnostics}/` (S09), `Features/Coverage/` (S10), `Integrations/Coverage/` (S10), `Shared/Coverage/` (S10), `Shared/Strategy/` (S11), `Features/Preflight/StrategyCoherence*` (S11), `Features/Packaging/{PackagingStrategyFactory, DependencyPolicyValidatorFactory}` (S11), `OtoolAnalyzePipeline` (S09), `InspectHarvestedDependenciesPipeline` (S09), `PathService.ResolveVersionsOutputDirectory` (S07) |
| Unmigrated targets (`Features/`) | Harvesting, Packaging, Preflight, Vcpkg, Ci, Publishing |
| Tests | 529 build-host tests, all green; V2 across migrated targets; `HybridStaticLeakValidatorTests` + `HybridStaticOverlayValidatorTests` are the freshest additions |
| Slopwatch | 48 baseline entries, strict rule severities |
| Phase status | P0-P5 closed. Active = P6 (PreFlight migration) |
| Cross-platform validation | Three-host workflow documented in `docs/playbook/cross-platform-smoke-validation.md`; WSL `/home/deniz/repos/sdl2-cs-bindings` (zsh), macOS `Armut@192.168.50.178:/Users/armut/repos/sdl2-cs-bindings` (zsh) |
| Open Phase X follow-ups (after P10) | `FakeCakeWorldV2` fluent API redesign (S03 era); diagnostic target UX unification + binary input unification + hardcoded triplet cleanup (S09 era); 11 OneOf result type retirements (S11 parking-lot); `windows-2025` runner deprecation (S11 release pipeline NOTICE, deadline 2026-05-12); coverage as optional non-blocking signal (S10 parking-lot) |

## Recommended Next Step

**S12 — P6 PreFlight migration to `Targets/PreFlightCheck/`.**

Per ADR-002 §11 sequencing, P6 follows P5 directly. PreFlight is the **first real boss fight** of the target-centric migration — cross-cutting validation surface — but it is **bounded and the area was just touched in S11** (HybridStaticOverlayValidator wiring), so the code is in hot context.

**P6 scope per refactor plan §11:**

Candidate target module shape:

```text
Targets/
  PreFlightCheck/
    PreFlightCheckTask.cs
    Requests/
      PreFlightCheckRequest.cs
    Validation/
      ManifestVcpkgConsistencyValidator.cs    (currently VersionConsistencyValidator)
      CoreLibraryIdentityValidator.cs
      UpstreamVersionAlignmentValidator.cs
      HybridStaticOverlayValidator.cs          (already in Features/Preflight/, just relocates)
      PackageFamilyScopeValidator.cs           (G58 cross-family — currently in Features/Packaging/)
      ProjectPackContractValidator.cs           (currently CsprojPackContractValidator)
      ManifestFamilyNameInvariantValidator.cs   (NEW — see step 7)
    Reporting/
      PreflightReporter.cs
```

Tasks per refactor plan §11 P6:

1. Preserve existing G-numbered guardrail behavior where still valid.
2. Remove strategy-specific validation (already done in S11 — verify nothing missed).
3. Use `ValidationReport` / `ValidationCheck` for multi-check output (already canonical post-S11; PreflightPipeline currently uses a mix of OneOf-shaped per-validator results + ValidationReport for newer ones — unify on ValidationReport).
4. Use behavior-first validator names; guardrail IDs (G14, G15, G16, G17, G18, G49, G54, G58) remain report/documentation metadata.
5. Keep task orchestration readable: load manifest/version state → run validators → report warnings/errors → throw once if fatal.
6. Add scenario coverage for success and representative failure cases (V2 fake-Cake-world; this is the slice that establishes the V2 scenario testing pattern for migrated targets, since prior migrations were simpler — Info, ResolveVersions, diagnostic targets).
7. **Add the manifest lowercase invariant validator** — every `manifest.package_families[].name` must match `^sdl[0-9]+-[a-z][a-z0-9-]*$`. P2b's `PackageFamilyId` uses ordinal-exact equality; the manifest is canonical-lowercase by convention, but a hand-edited mixed-case entry (`SDL2-Core`) would silently bypass legacy ignore-case lookups and then break `PackageFamilyId` lookups in P3+/P4+ consumers. PreFlight is the canonical home for this contract.

**Exit criteria** (per refactor plan §11):
- `PreFlightCheckTask` tells the validation story directly (not a thin shell delegating to PreflightPipeline)
- Validators are named by the rule they enforce (behavior-first, guardrail IDs as metadata)
- No generic `PreflightPipeline` remains
- Manifest lowercase invariant is enforced on every `package_families[].name`

**Risks:**
1. **First boss fight** — cross-cutting validation harder than diagnostic target migrations. Pattern-establishing: scenario test approach for V2 + migrated targets needs to be solid here.
2. **OneOf result type cleanup along the way** — 5 of the 11 surviving OneOf-shaped result types live in `Features/Preflight/` (CsprojPackContractResult, CoreLibraryIdentityResult, UpstreamVersionAlignmentResult, VersionConsistencyResult). Plus G58 lives in Packaging. Decide upfront: collapse all PreFlight OneOf results to ValidationReport in the same slice (consistent + closes parking-lot entry partially), OR migrate validator structure first and defer OneOf retirement to a later micro-slice (smaller commit). Refactor plan §11 P6 step 3 leans toward collapse during P6.
3. **Manifest lowercase invariant** — new behavior addition, not pure refactor. Means the slice is bigger than pure-migration (S10/S11 patterns).
4. **G58 split** — currently lives in `Features/Packaging/` but PreFlight invokes it. Decision needed: relocate G58 into PreFlight target? Or keep cross-target reference? ADR-002 §6 named-concept rule says: cross-target code goes to a named concept. G58 logically belongs to PreFlight (it's a PreFlight check) but is also Pack-time mirrored. Ship G58 with PreFlight migration, or leave it.

**Estimated scope:** larger than S10 (delete-only) and S11 (delete + new validator + schema change). P6 is a structural refactor of cross-cutting code with at least one new behavior addition. Multi-session realistic if scenario tests are written thoroughly.

Talk to Deniz before settling slice scope. Specifically the OneOf-collapse-during-P6 decision and the G58 relocation decision are real opinionated calls.

Alternative: P7 (Package) first if Deniz wants to break the §11 sequencing. Defensible but Package consumes versions resolved by PreFlight; migrating PreFlight first keeps the dependency graph clean.

After P6, P7 (Package), P8 (Harvest + NativeSmoke + ConsolidateHarvest), P9 (Publishing + ConsumerSmoke), P10 (final cleanup including remaining OneOf retirements + Host/Configuration deletion).

## Mandatory Grounding (read in this order)

1. `docs/onboarding.md`
2. `AGENTS.md`
3. `CLAUDE.md`
4. `docs/plan.md` ← Phase X status reflects P5 closure (verify post-handover doc commit if uncommitted at handover time)
5. `docs/decisions/2026-05-05-target-centric-build-host.md`
6. `docs/refactoring/README.md` ← Document lifecycle section
7. `docs/refactoring/target-centric-build-host-refactor-plan.md` ← §11 P6 scope is the next focus
8. `docs/refactoring/target-centric-build-host-review-checklist.md`
9. `docs/refactoring/extraction-guidelines.md`
10. `docs/refactoring/testing-guidelines.md` ← V2 fixture rules, test data policy
11. `docs/playbook/cross-platform-smoke-validation.md` ← S11-introduced playbook for Windows + WSL + macOS validation
12. `build/_build/Targets/Info/InfoTask.cs` ← IAnsiConsole pattern reference
13. `build/_build/Targets/OtoolAnalyze/OtoolAnalyzeTask.cs` ← extraction shape reference
14. `build/_build/Features/Preflight/PreFlightCheckTask.cs` ← P6 source target
15. `build/_build/Features/Preflight/PreflightPipeline.cs` ← P6 source orchestrator (this is what gets unwound)
16. `build/_build/Features/Preflight/HybridStaticOverlayValidator.cs` ← S11-introduced PreFlight validator (already canonical shape; reference for new validators)
17. `build/_build/Features/Preflight/CoreLibraryIdentityValidator.cs` ← static validator pattern reference
18. `build/_build/Shared/Harvesting/HybridStaticLeakValidator.cs` ← S11-introduced relocated validator (canonical ValidationReport return shape)
19. `build/_build/Results/ValidationReport.cs` ← canonical multi-check report type used by all new + migrated validators
20. `docs/parking-lot.md` ← OneOf retirement entry (11 surviving types, retirement schedule)
21. `tools.cs`
22. `.github/workflows/release.yml` ← CI orchestration

## Locked Policy Recap

- No commits without Deniz approving the summary and proposed commit message.
- Docs-only edits are allowed; production code/refactor/build-system changes need explicit go/apply/proceed/başla/yap.
- `dotnet-slopwatch` is mandatory after LLM-authored code/project/test changes.
- Cake task classes are discovered via `[TaskName]`; do not explicitly register task classes in DI.
- Migrated tasks read named `BuildContext` properties; do not reintroduce public `BuildContext.ParsedArguments`.
- V2 tests instantiate task classes from the provider without adding task types as services.
- `ToLegacyBuildContext` is frozen and only supports unmigrated tests.
- `tools.cs` stays standalone and may not reference `build/_build` internals.
- Build-host IO/process/path/logging/tooling uses Cake-native abstractions by default.
- Guardrail IDs are metadata/report labels, not primary code names.
- Isolated ADR-002 worktrees are not native-build workspaces unless explicitly provisioned.
- **V2 test infrastructure is the default for anything touched** — broader than the target-migration rule. V1 fixtures (`FakeRepoBuilder`, `TestHostFixture`) are frozen.
- **Test data belongs in embedded fixture files** (`Fixtures/Data/`) or centralized static classes — never raw string literals in test methods.
- **`--versions-file` is universal** — both writers and readers use it. `IVersionFileRepository` path at call time. Tasks validate at entry.
- **`CleanArtifacts`, `CompileSolution`, `Coverage-Check` are not Cake targets.** Local cleanup, bare-build invocation, and coverage gate are not pipeline concerns.
- **`docs/refactoring/` temp docs never enter git history.** Audit findings/learnings/TODOs and migrate to canonical docs before deletion.
- **Migrate files with `git mv` FIRST, then edit content.** Preserves `git log --follow` history. Files being retired (no destination) stay as true deletes.
- **`Targets/` folder names are PascalCase** (Cake target hyphens live on `[TaskName]` only).
- **WSL/macOS validation hosts use zsh, not bash** — drive non-interactively via `zsh -lc`. Disposable validation hosts: hard-reset to origin/master, never develop on them.
- **Cross-platform smoke validation gate is multi-host** — for changes that touch manifest schema / DI / Harvest / Pack / Smoke / runtime profiles. Pure managed changes can stay Windows-only.
- **OneOf-style result hierarchies are on retirement path.** Don't introduce new ones; new validators emit canonical `Build.Results.ValidationReport`. The 11 surviving types die during their target migrations (P6/P7/P8) or P10 cleanup.

## Final Steering Note

P5 closed in two slices (S10 + S11), validated end-to-end on three local hosts and on the full GitHub Actions release pipeline. The migration pattern is mature for delete-only (S07, S08, S10) and for delete + new + schema change (S11). S12 = P6 is the **first cross-cutting refactor** — pattern-establishing for the bigger boss fights (P7 Package, P8 Harvest). Get the validator-by-validator migration shape right here; the rest cascades.

The two areas worth extra brainstorming time for S12 scope:

1. **OneOf collapse-during-P6 vs defer**. PreFlight has 4 OneOf-shaped result types (CsprojPackContractResult, CoreLibraryIdentityResult, UpstreamVersionAlignmentResult, VersionConsistencyResult). Collapsing all in S12 is consistent with ADR-002 §11 + closes the parking-lot entry partially, but enlarges the slice. Defer to a separate micro-slice keeps S12 focused on the structural migration.

2. **G58 ownership**. Currently cross-target (Pack + PreFlight invoke it via `IG58CrossFamilyDepResolvabilityValidator`). Real consumer is PreFlight at coherence-check time + Pack as mirror. Decide: relocate to `Targets/PreFlightCheck/Validation/` and Pack-side becomes a re-invocation, OR keep as named concept under `Shared/Versioning/` (ADR-002 §6 named-concept rule).

Talk to Deniz on both before scoping S12. Coverage retirement was easy mode; strategy retirement was medium with one bundled bugfix; PreFlight is real architecture work. Don't underestimate the scenario-test surface — it's where the pattern for migrated-target testing gets established.

Phase X post-refactor backlog now has 7 items (FakeCakeWorldV2 fluent API + 3 diagnostic UX/input/triplet items + 11 OneOf retirements + windows-2025 runner deprecation + optional coverage signal). Don't start any of them until P10 closes; they're explicit deferrals.
