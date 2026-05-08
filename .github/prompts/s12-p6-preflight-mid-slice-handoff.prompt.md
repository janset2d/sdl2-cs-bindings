---
name: "S12 P6 PreFlightCheck mid-slice handoff — Phase 1-3 done + Phase 4 partial (boundary types swapped, internal pipeline conversion pending)"
description: "Mid-slice handoff for the P6 PreFlight migration. The previous agent (Opus 4.7 1M context) drove Phases 1-3 + the public-boundary half of Phase 4 with high fidelity; ran long and is handing off mid-stream for a fresh agent to finish. Phase 1 (foundation: `Validation/` root, `IVcpkgManifestRepository`, `ManifestFamilyNameInvariantValidator` G59), Phase 2 (6 validator relocations to `Validation/{Manifest,Versioning,Packaging}/` + `Models/` + `Conventions/`), Phase 3 (`Targets/PreFlightCheck/` atomic migration, `Features/Preflight/` deleted) are complete. Phase 4 (dict retirement) is half-done: public boundaries (`PackageBuildConfiguration`, `PackRequest`, `PackageConsumerSmokeRequest`, `PublishRequest`, validator interfaces) all typed `PackageFamilyVersionSet`; pipeline internal helpers still use `Dictionary<string, NuGetVersion>`. **Decision: full typed required (B-mode, NOT half-typed boundary mode)** — internal helpers in `PackagePipeline` (~3), `PackageConsumerSmokePipeline` (~6), `PublishPipeline` (~2) need conversion to consume `PackageFamilyVersionSet` directly so zero `Dictionary<string, NuGetVersion>` matches in production. Tests are temporarily broken — production builds clean (0 warnings, 0 errors)."
argument-hint: "DO NOT continue from where the last agent left off — first walk through the verification checklist and review what's actually in the working tree. Then finish Phase 4 in B-mode (full typed). After Phase 4: Phase 5 (OneOf cleanup confirm), Phase 6 (V2 test migration + scenario tests), Phase 7 (docs sweep including spec/plan revision per the in-flight Phase-X backlog notes), Phase 8 (slopwatch + cross-platform validation + GH Actions release pipeline)."
agent: "agent"
model: "Claude Opus 4.7 (1M context)"
---

You are entering an in-progress P6 PreFlight migration slice in the janset2d/sdl2-cs-bindings repository. The previous agent ran long and is handing off mid-slice. **Do NOT just pick up where the last agent left off — verify state first.**

## First Principle

> Treat every claim in this prompt as **current-as-of-authoring** and verify against the live repo, git log, and canonical docs before acting.

## CRITICAL: Verify before continuing

Walk through this in order. **Do not skip steps.**

### Step 1 — Read canonical docs

In this order:

1. `docs/onboarding.md`
2. `AGENTS.md` (operating rules, approval gate, build-host reference pattern)
3. `CLAUDE.md` (quick pointers)
4. `docs/plan.md` (current status)
5. `docs/decisions/2026-05-05-target-centric-build-host.md` (ADR-002)
6. `docs/refactoring/README.md` (doc lifecycle — temp slice docs never enter git history)
7. `docs/refactoring/target-centric-build-host-refactor-plan.md` (§11 P6 is the slice scope)
8. `docs/refactoring/target-centric-build-host-review-checklist.md`
9. `docs/refactoring/extraction-guidelines.md`
10. `docs/refactoring/testing-guidelines.md`
11. `docs/superpowers/specs/2026-05-08-p6-preflight-migration-design.md` ← slice spec (temp scratch, includes earlier "no interface" wording that has been REVISED by mid-slice user clarification — see §"Decisions made along the way" below)
12. `docs/superpowers/plans/2026-05-08-p6-preflight-migration-plan.md` ← slice plan (temp scratch). **Read the §"Phase-X backlog items surfaced during execution" section near the end.**
13. `docs/playbook/cross-platform-smoke-validation.md` (final-gate validation playbook for Windows + WSL + macOS)
14. `docs/parking-lot.md` (Phase X backlog home — Phase 7 doc sweep migrates the in-flight backlog notes from the slice plan into here)

### Step 2 — Verify current state

```pwsh
git status --short
git log -3 --oneline
dotnet build build/_build/Build.csproj
grep -rn "IReadOnlyDictionary<string, NuGetVersion>" build/_build --include "*.cs"
ls build/_build/Validation
ls build/_build/Targets/PreFlightCheck
ls build/_build/Features/Preflight 2>&1   # MUST NOT exist
ls build/_build/Shared/Versioning 2>&1     # MUST NOT exist
```

Expected:

- Branch: `master`. Untracked: spec/plan/prompt slice docs (`docs/superpowers/specs/`, `docs/superpowers/plans/`, `.github/prompts/s12-*`). They are temp slice scratch per refactor doc lifecycle.
- HEAD: `730ce5c docs: add P6 PreFlightCheck migration design spec (S12)` or later.
- Production build: **0 warnings, 0 errors**.
- `IReadOnlyDictionary<string, NuGetVersion>` matches: **ZERO** in production code.
- `Validation/{Manifest,Versioning,Packaging,Models,Conventions}/` populated with 7 validators + interfaces + models + conventions.
- `Targets/PreFlightCheck/{PreFlightCheckTask.cs, ServiceCollectionExtensions.cs, Reporting/PreflightReporter.cs}` exists.
- `Features/Preflight/` directory **DOES NOT EXIST** (deleted in Phase 3).
- `Shared/Versioning/` directory **DOES NOT EXIST** (deleted in Phase 2 Task 9).

### Step 3 — Run tests (FAILURE EXPECTED — read carefully)

```pwsh
dotnet test --project build/_build.Tests/Build.Tests.csproj -c Release --framework net10.0
```

Expected failures:

- `Unit/Validation/Versioning/UpstreamVersionAlignmentValidatorTests.cs` — constructs `Dictionary<string, NuGetVersion>` and calls `validator.Validate(mapping, manifest)`. The validator's signature changed to accept `PackageFamilyVersionSet`; these tests need conversion (line items in Step 5 below).
- `Unit/Validation/Versioning/CrossFamilyDependencyResolvabilityValidatorTests.cs` — same pattern, same fix needed.

**These are intentional, expected failures pending Phase 4 finish + test update.** Do not panic and do not hand-fix individual tests until you've completed B-mode pipeline conversion (Step 5).

If anything else fails (production code red, ProgramCompositionRootTests red, ServiceCollectionExtensionsSmokeTests red, etc.), that's NOT expected — investigate and report before continuing.

### Step 4 — Review what's done (verify against actual code, don't trust this list)

**Phase 1 ✅ (3 logical commits, atomic-mode tracked in working tree):**

- `Validation/ServiceCollectionExtensions.cs` with `AddValidators()` registering 7 interface→concrete pairs:
  - `IManifestFamilyNameInvariantValidator` → `ManifestFamilyNameInvariantValidator`
  - `IVersionConsistencyValidator` → `VersionConsistencyValidator`
  - `ICoreLibraryIdentityValidator` → `CoreLibraryIdentityValidator`
  - `ICsprojPackContractValidator` → `CsprojPackContractValidator`
  - `ICrossFamilyDependencyResolvabilityValidator` → `CrossFamilyDependencyResolvabilityValidator`
  - `IUpstreamVersionAlignmentValidator` → `UpstreamVersionAlignmentValidator`
  - `IHybridStaticOverlayValidator` → `HybridStaticOverlayValidator`
- `Repositories/{IVcpkgManifestRepository, VcpkgManifestRepository}` + 5 unit tests, registered in `AddRepositories()`.
- `Validation/Manifest/{IManifestFamilyNameInvariantValidator, ManifestFamilyNameInvariantValidator}` (G59) + 6 unit tests, `[GeneratedRegex]` source generator with `matchTimeoutMilliseconds: 100`.

**Phase 2 ✅ (6 logical commits — validator relocations):**

- `HybridStaticOverlayValidator` → `Validation/Packaging/` + new `IHybridStaticOverlayValidator` interface.
- `VersionConsistencyValidator` → `Validation/Manifest/` (static→instance + new interface). Path metadata (`manifestPath`, `vcpkgManifestPath`) **dropped from validator surface** — reporter hardcodes "build/manifest.json" / "vcpkg.json".
- `CoreLibraryIdentityValidator` → `Validation/Manifest/` (static→instance + new interface).
- `CsprojPackContractValidator` → `Validation/Manifest/` (existing interface kept).
- G58 → `CrossFamilyDependencyResolvabilityValidator` in `Validation/Versioning/`. Type rename: `G58CrossFamily*` → `CrossFamilyDependency*`. Interface rename: `IG58...` → `ICrossFamilyDependencyResolvabilityValidator`. Log strings: bracketed metadata `[G58]` (not bare prefix `G58:`).
- `UpstreamVersionAlignmentValidator` → `Validation/Versioning/` (existing interface kept).
- All `*Validation` records relocated to `Validation/Models/`: `LibraryVersionCheck`, `VersionConsistencyValidation`, `CoreLibraryIdentityModels`, `CsprojPackContractModels`, `UpstreamVersionAlignmentModels`, `CrossFamilyDependencyModels`.
- `FamilyIdentifierConventions` moved to `Validation/Conventions/`.
- `Shared/Versioning/` directory deleted.
- `Shared/Packaging/G58*` files deleted (interface, validator, models).
- 4 OneOf result types deleted inline during validator moves: `VersionConsistencyResult`, `CoreLibraryIdentityResult`, `CsprojPackContractResult`, `UpstreamVersionAlignmentResult`. (`PreflightError` deleted in Phase 3.)
- All consumer files (PreflightPipeline, PackagePipeline, PackageConsumerSmokePipeline, PublishPipeline, ReadmeMappingTable, PackageOutputValidator, PackageValidationResult, ResolveVersionsFromExplicitTask) updated for namespace migrations + type renames + OneOf retirement (`.OnError(...)` patterns → `.HasErrors` + direct `CakeException`).
- DI smoke tests + ProgramCompositionRoot tests updated.

**Phase 3 ✅ (1 atomic logical commit — boss-fight migration):**

- `Targets/PreFlightCheck/PreFlightCheckTask.cs` new — owns orchestration directly, 11-parameter constructor (3 repositories + 7 validators + reporter), no private methods, no path plumbing, no `EnsureInputsReady` (repositories throw on missing files).
- `Targets/PreFlightCheck/ServiceCollectionExtensions.cs` — `AddPreFlightCheck()` only registers `PreflightReporter` (validators come from `AddValidators()`, task is discovered via `[TaskName]`).
- `Targets/PreFlightCheck/Reporting/PreflightReporter.cs` — moved from `Features/Preflight/`, namespace updated, `ReportRunStart` scope-line message lists all 7 validators, **new `ReportManifestFamilyNameInvariant(ValidationReport)` for G59**.
- `Features/Preflight/` directory **deleted entirely**: `PreflightPipeline`, old `PreFlightCheckTask`, `PreflightRequest`, `PreflightError`, `ServiceCollectionExtensions`, `PreflightReporter`.
- `Program.cs`: `using Build.Features.Preflight` → `using Build.Targets.PreFlightCheck`. `services.AddPreflightFeature()` → `services.AddPreFlightCheck()`. `services.AddValidators()` already added in Phase 1.
- V1 PreFlight tests deleted: `Unit/Features/Preflight/PreFlightCheckTaskRunTests.cs` + `PreflightRequestTests.cs`. (V2 scenario tests for `PreFlightCheckTask` come in Phase 6.)
- DI smoke test renamed `AddPreflightFeature_*` → `AddPreFlightCheck_*`.
- Test count: 532 (down from 540 — 8 V1 PreFlight tests deleted, V2 replacements pending Phase 6).

**Phase 4 ⚠️ HALF-DONE (single atomic boss-fight in progress — B-mode finish required):**

What's done:

- ✅ `Host/Configuration/PackageBuildConfiguration.FamilyVersions: PackageFamilyVersionSet` (rename + type swap from `FamilyVersionMapping: IReadOnlyDictionary<...>`).
- ✅ `Program.cs` factory simplified — drops `.ToDictionary(...)`, returns `new PackageBuildConfiguration(versionSet)` directly. Empty fallback uses `PackageFamilyVersionSet.Empty`.
- ✅ `PackRequest.Versions / PackageConsumerSmokeRequest.Versions / PublishRequest.Versions: PackageFamilyVersionSet`.
- ✅ `IUpstreamVersionAlignmentValidator.Validate(ManifestConfig, PackageFamilyVersionSet)` — single signature, dict overload deleted.
- ✅ `ICrossFamilyDependencyResolvabilityValidator.Validate(PackageFamilyVersionSet, ManifestConfig)` — typed.
- ✅ `PackageTask`, `PackageConsumerSmokeTask`, `PublishStagingTask` — `_packageBuildConfiguration.FamilyVersions` (rename), boundary types typed.
- ✅ `Targets/PreFlightCheck/PreFlightCheckTask.cs` — passes `versions` (typed) to both Upstream and CrossFamily validators directly. No dict bridge.
- ✅ `grep -rn "IReadOnlyDictionary<string, NuGetVersion>" build/_build --include "*.cs"` returns ZERO.

What's NOT done (B-mode finish):

- ⚠️ `PackagePipeline.RunAsync` does `var explicitVersions = request.Versions.ToDictionary(...)` at top; helpers `ResolveSelectedFamilies(Dictionary<string, NuGetVersion>)` and `explicitVersions[name].ToNormalizedString()` lookup still dict-shaped.
- ⚠️ `PackageConsumerSmokePipeline.RunAsync` does `var requestVersionsDict = request.Versions.ToDictionary(...)` at top; ~6 internal helpers (`EnsureSelectionSupportsCurrentSmokeScope`, `ResolveSmokeVersionMappingAndEnsureFeed`, `EnsurePackageArtifactsExist`, `RunCompileSanity`, the per-TFM smoke runner, `AppendSmokePackageVersionProperties`) all take `Dictionary<string, NuGetVersion> explicitVersions`.
- ⚠️ `PublishPipeline.RunAsync` does `var requestVersionsDict = request.Versions.ToDictionary(...)` at top; helper `ResolveConcreteFamiliesInScope(Dictionary<string, NuGetVersion>)` + version lookup `requestVersionsDict[family.Name]` still dict-shaped.

**B-mode finish (Step 5 below) converts these helpers to consume `PackageFamilyVersionSet` directly.** After conversion, neither `IReadOnlyDictionary<string, NuGetVersion>` nor `Dictionary<string, NuGetVersion>` should appear in production code (excluding the `PackageFamilyVersionSet` class's internal storage and any `Dictionary<string, string>` / unrelated dict types).

Test status: V1 test files updated for namespace + type renames; OneOf assertion patterns (`.IsError()`, `.IsSuccess()`, `result.Validation.X`) collapsed to direct `validation.HasErrors` / `validation.X` patterns. Two test files (`UpstreamVersionAlignmentValidatorTests`, `CrossFamilyDependencyResolvabilityValidatorTests`) still construct `Dictionary<string, NuGetVersion>` — they need rewrite to construct `PackageFamilyVersionSet` directly (Step 5).

**Phase 5-9 ⏳ pending (not started):**

- Phase 5: OneOf cleanup confirm. Audit `Features/Preflight/` (deleted, but verify), `Shared/Versioning/` (deleted, verify), `Shared/Packaging/G58*` (deleted, verify). 5 OneOf types deleted inline during Phase 2-3; Phase 5 is no-op confirmation (parking-lot 11→6 in Phase 7 doc sweep). If audit finds orphan OneOf result references, clean up. Otherwise: skip the commit.
- Phase 6: V2 test migration + scenario tests for `PreFlightCheckTask`. Add `Scenarios/PreFlightCheck/PreFlightCheckTaskScenarioTests.cs` with happy-path + 6 representative failure cases per spec §10.2. Add `Fixtures/Data/Vcpkg/{vcpkg-valid.json, vcpkg-version-mismatch.json}` embedded resources. Migrate any V1 fixture usage in touched test files (per testing-guidelines.md altın kural).
- Phase 7: Documentation sweep — refactor plan, parking-lot, release-guardrails, AGENTS.md, ADR-002, plan.md, plus **slice spec/plan revision per the in-flight "Phase-X backlog items" section in the slice plan**. Spec §3 D2 + §4.2 + §9 wording must change from "drop interface / no interface" to "interface + concrete via `AddSingleton<I,T>()`" (see §Decisions below). Code doc sweep (XML doc comments, stale phase comments).
- Phase 8: Slopwatch baseline rebuild + Windows ci-sim + WSL ci-sim + macOS ci-sim + GitHub Actions release pipeline (Deniz manual trigger).

## Step 5 — Phase 4 finish (B-mode full typed)

Convert pipeline internal helpers to consume `PackageFamilyVersionSet` directly. Drop the `request.Versions.ToDictionary(...)` bridge at top of each `RunAsync`.

### PackagePipeline

Drop the bridge at top of `RunAsync`. Update:

- `private IReadOnlyList<PackageFamilyConfig> ResolveSelectedFamilies(PackageFamilyVersionSet explicitVersions)` — iterate via `foreach (var entry in explicitVersions)`, use `entry.Family.Value` for the name lookup against `manifest.PackageFamilies`. Capacity hint: `new List<PackageFamilyConfig>(explicitVersions.Count)`.
- Per-family lookup in `RunAsync` foreach: `var familyVersion = explicitVersions.RequireVersion(new PackageFamilyId(family.Name)).ToNormalizedString();` (replaces `explicitVersions[family.Name].ToNormalizedString()`).

### PackageConsumerSmokePipeline

Drop the `requestVersionsDict` bridge at top of `RunAsync`. Pass `request.Versions` directly. Update helper signatures:

- `EnsureSelectionSupportsCurrentSmokeScope(IReadOnlyList<SmokePackage> smokePackages, PackageFamilyVersionSet explicitVersions)` — `explicitVersions.Contains(new PackageFamilyId(package.FamilyName))` instead of `explicitVersions.ContainsKey(package.FamilyName)`.
- `ResolveSmokeVersionMappingAndEnsureFeed(IReadOnlyList<SmokePackage>, PackageFamilyVersionSet, DirectoryPath)` returning `PackageFamilyVersionSet`.
- `EnsurePackageArtifactsExist(IReadOnlyList<SmokePackage>, PackageFamilyVersionSet, DirectoryPath)` — `explicitVersions.RequireVersion(new PackageFamilyId(smokePackage.FamilyName)).ToNormalizedString()`.
- `RunCompileSanity(FilePath, IReadOnlyList<SmokePackage>, PackageFamilyVersionSet, DirectoryPath, DirectoryPath)`.
- The per-TFM smoke runner that takes `explicitVersions` — same conversion.
- `AppendSmokePackageVersionProperties(arguments, smokePackages, PackageFamilyVersionSet)` — replace `explicitVersions.TryGetValue(smokePackage.FamilyName, out var version)` with `explicitVersions.TryGetVersion(new PackageFamilyId(smokePackage.FamilyName), out var version)` (note: `TryGetVersion` returns `out NuGetVersion?` per `PackageFamilyVersionSet`; verify against current implementation).

### PublishPipeline

Drop the `requestVersionsDict` bridge at top of `RunAsync`. Pass `request.Versions` directly. Update:

- `ResolveConcreteFamiliesInScope(PackageFamilyVersionSet versions)` — iterate, `versions.Contains(new PackageFamilyId(family.Name))` and `versions.Count` for capacity.
- `request.Versions[family.Name]` → `request.Versions.RequireVersion(new PackageFamilyId(family.Name))`.

### Verification gate after Phase 4 finish

```pwsh
dotnet build build/_build/Build.csproj
grep -rn "Dictionary<string, NuGetVersion>\|IReadOnlyDictionary<string, NuGetVersion>" build/_build --include "*.cs"
```

Expected: build clean, ZERO matches in production code.

### Test fixes after Phase 4 finish

Two test files still construct `Dictionary<string, NuGetVersion>`:

- `Unit/Validation/Versioning/UpstreamVersionAlignmentValidatorTests.cs`
- `Unit/Validation/Versioning/CrossFamilyDependencyResolvabilityValidatorTests.cs`

Convert each test that builds a `Dictionary<string, NuGetVersion>` to build a `PackageFamilyVersionSet` directly:

```csharp
// Before
var mapping = new Dictionary<string, NuGetVersion>(StringComparer.OrdinalIgnoreCase)
{
    ["sdl2-core"] = NuGetVersion.Parse("2.32.0-local.test"),
    ["sdl2-image"] = NuGetVersion.Parse("2.8.0-local.test"),
};
var validation = validator.Validate(mapping, manifest);

// After
var versions = new PackageFamilyVersionSet([
    new PackageFamilyVersion(new PackageFamilyId("sdl2-core"), NuGetVersion.Parse("2.32.0-local.test")),
    new PackageFamilyVersion(new PackageFamilyId("sdl2-image"), NuGetVersion.Parse("2.8.0-local.test")),
]);
var validation = validator.Validate(versions, manifest);
```

Then run full test suite. Expected: all tests pass (count ~532 — V1 PreFlight 8 deleted in Phase 3; V2 scenario adds in Phase 6).

## Decisions made along the way (NOT in spec/plan as written)

These are mid-execution clarifications from Deniz that contradict the spec/plan's original wording. Phase 7 doc sweep transcribes the durable ones to canonical docs.

### 1. Full interface for ALL validators (not "no interface" as spec §3 D2 originally said)

- Q3 of brainstorming initially produced "no interface" decision (sealed class + AddSingleton, no IFoo).
- Mid-execution Deniz clarified: *"hepsi interface ile DI'a eklenecek diye konuştuk"* — all 7 validators get `IFoo` + `Foo` + `AddSingleton<IFoo, Foo>()` registration.
- Slice now has 7 interface→concrete pairs registered via `AddValidators()`.
- **Bonus**: Interface implementation can't be marked static, so analyzers `CA1822` / `S2325` are silently satisfied. **No suppression attributes anywhere in `Validation/`** — the half-formed Phase-X "review suppressions" backlog item from the in-flight slice plan is moot; mark it resolved in Phase 7 doc sweep.
- **Phase 7 doc sweep tasks**: Spec `§3 D2`, `§4.2`, `§9`, plan task descriptions (Tasks 4-9) all need wording revision: *"drop interface"* → *"keep existing interface (or add new interface where missing)"*. ADR-002 §8 "Avoid ceremonial IFoo/Foo pairs" — leave ADR untouched, but optionally add a S12 amendment note that `Validation/` validators are an explicit exception (uniform DI shape).

### 2. Single atomic commit at slice end (not per-phase commits)

- Working tree accumulates all changes; one `git commit` happens at slice end after Deniz approves summary + commit message.
- Plan's per-phase `git commit` steps are review checkpoints, NOT actual commits.
- Commit message draft: TBD at Task 19; capture all phase deltas in a single conceptual summary.

### 3. Direct master, no worktree

- Deniz waived the AGENTS.md ADR-002 worktree rule for this slice (consent given explicitly during brainstorming).
- All edits in `e:\repos\my-projects\janset2d\sdl2-cs-bindings\`. No `e:\tmp\sdl2-cs-bindings-p6-preflight\` worktree.

### 4. Dict retirement — full typed (B-mode)

- Spec §17 open question said "either is acceptable; P7 will rewrite the internal structure."
- Mid-execution Deniz called the half-typed boundary mode (boundary `PackageFamilyVersionSet`, internal `Dictionary<string, NuGetVersion>`) *"yarım iş"* — explicitly chose B-mode (full typed end-to-end).
- The previous agent had completed the public-boundary half before this clarification; the full-typed pipeline-internal conversion is the immediate Phase 4 finish task (Step 5 above).
- **Phase 7 doc sweep**: Spec §17 open question — flip default to "full typed" recommended.

### 5. Phase-X backlog items surfaced

(Slice plan §"Phase-X backlog items surfaced during execution" near end of file. Phase 7 doc sweep migrates these to `docs/parking-lot.md` before the slice plan is deleted.)

- **PreflightReporter uses `ICakeLog`, not `IAnsiConsole`** — migrated targets (`InfoTask`, `OtoolAnalyze`) use `IAnsiConsole` for output. PreflightReporter was relocated as-is in Phase 3, still constructor-injects `ICakeContext` and uses `_cakeContext.Log.Information/Error`. Phase X migration to Spectre console for visual consistency. Deniz noted: *"using logging :D We can use IAnsiConsole here, but definitely not right now"*.
- **Other "weird" `ICakeLog` static-style logging in build host** — audit during Phase X for similar inconsistencies.
- **Validator analyzer suppressions review** — already obviated by interface introduction (no suppressions remain in `Validation/`). Item kept for documentation closure: confirm in Phase X that no `Validation/` class carries `CA1822` / `S2325` `SuppressMessage` attribute.
- **`Build.Validation.Versioning` namespace duplication with `Build.Versioning`** — two namespaces named "Versioning": one root (`Build.Versioning` — `PackageFamilyId`, `PackageFamilyVersionSet`, `ExplicitVersionParser`), one under Validation (`Build.Validation.Versioning` — Upstream + CrossFamily validators). Worth a re-read post-P10. Current state defensible (both names accurately describe their domain) but confusing import patterns possible.

### 6. G58/G54 in code: bracketed metadata, not prefix

- All exception messages, log strings: `[G58]` not `G58:` or `(G58)`.
- Behavior-first naming for types/files; guardrail IDs as report metadata only.
- Applied during Phase 2 Task 8 (G58 rename) + Task 9 (Upstream relocation).

## Approval gate (hard rule)

Per AGENTS.md:

- **Do NOT commit without Deniz approving the summary and proposed commit message.**
- Documentation-only edits are allowed without approval, but slice spec/plan are temp scratch — do not commit them.
- Slice ships with a single atomic git commit on `master` at Task 19.
- Deniz manually triggers the GitHub Actions release pipeline (Task 22).

## Communication style (Deniz preferences)

- Talkative + practical, sometimes clever humor.
- Innovative but prioritize what works.
- Challenge decisions when needed; explain reasoning. **Avoid yes-person behavior.**
- Bilingual: Turkish + English interchangeably.
- **Skip micro-checkpoints during execution** — batch routine work, stop only at significant boundaries (per `feedback_skip_micro_checkpoints.md` memory). Phase boundaries are usually significant; individual tasks within a phase usually aren't.

## Useful commands

```pwsh
# Build host
dotnet build build/_build/Build.csproj

# Build-host regression suite (TUnit on MTP)
dotnet test --project build/_build.Tests/Build.Tests.csproj -c Release --framework net10.0

# Targets exercised end-to-end after Phase 4 finish (final smoke)
dotnet run --project build/_build -- --target ResolveVersionsFromManifest --versions-file artifacts/resolve-versions/versions.json --suffix p6.test
dotnet run --project build/_build -- --target PreFlightCheck --versions-file artifacts/resolve-versions/versions.json

# tools.cs ci-sim (Phase 8)
dotnet run --file tools.cs -- ci-sim

# Slopwatch (Phase 8)
slopwatch analyze --fail-on warning --exclude "artifacts/**,external/**,vcpkg_installed/**,**/bin/**,**/obj/**"
slopwatch init -f --exclude "artifacts/**,external/**,vcpkg_installed/**,**/bin/**,**/obj/**"

# WSL ci-sim (Phase 8 — Deniz drives, optional for fresh agent unless asked)
wsl -- zsh -lc 'cd /home/deniz/repos/sdl2-cs-bindings && git fetch origin && git reset --hard origin/master && git submodule update --init --recursive && dotnet run --file tools.cs -- ci-sim'

# macOS ci-sim (Phase 8 — Deniz drives)
ssh Armut@192.168.50.178 'zsh -lc "cd /Users/armut/repos/sdl2-cs-bindings && git fetch origin && git reset --hard origin/master && git submodule update --init --recursive && dotnet run --file tools.cs -- ci-sim"'
```

## Memory check

Auto-memory at `C:\Users\deniz\.claude\projects\e--repos-my-projects-janset2d-sdl2-cs-bindings\memory\` — check the index file `MEMORY.md` and read relevant feedback entries before responding. Notable for this slice:

- `feedback_skip_micro_checkpoints.md` — batch routine work, stop only at significant boundaries.
- `feedback_test_infra_v2_default.md` — touch a test → use V2.
- `feedback_git_mv_during_migrations.md` — `git mv` FIRST before content edits, preserves `git log --follow` history.
- `feedback_wsl_macos_zsh.md` — WSL/macOS use `zsh -lc` for non-interactive driving; bash doesn't load `DOTNET_ROOT`.
- `feedback_session_pacing.md` — after a slice commits, default to "what's next?" not "let me wrap up".

## Final steering note

This is a long-running boss-fight slice (P6 first cross-cutting refactor of ADR-002). The previous agent ran Phases 1-3 + half of Phase 4 with high fidelity to spec, but introduced one interpretation bug: pipeline internal helpers got `Dictionary<string, NuGetVersion>` instead of full `PackageFamilyVersionSet`. Deniz called the half-typed approach *"yarım iş"* and asked for B-mode (full typed) finish.

Don't repeat that mistake — when typing the boundary, type all the way down. Pipeline internal orchestration logic stays as-is (the `foreach` loops, the family-by-family pack flow, the per-RID smoke matrix); only **type signatures** and **lookup patterns** (`dict[name]` → `versionSet.RequireVersion(new PackageFamilyId(name))`) change. **No business-logic rewrites.** P7/P8/P9 boss-fights still own internal rewrites of those pipelines; this slice only swaps types for purity, not orchestration.

Phase 7-9 are documentation + validation gates after Phase 4-6 finish. Phase 8 cross-platform validation happens AFTER `git commit + push` at Task 19 (slice ships first, validation hosts pull from master, GH Actions runs against pushed master). Don't try to validate WSL/macOS/GH Actions before Deniz approves the commit and pushes.

After Phase 8 closes (release pipeline green, run ID recorded), this prompt and the slice spec/plan in `docs/superpowers/{specs,plans}/2026-05-08-p6-*.md` are temp scratch — delete them after migrating any unique findings to canonical docs (refactor plan, AGENTS.md, parking-lot.md). Per `docs/refactoring/README.md` document lifecycle.
