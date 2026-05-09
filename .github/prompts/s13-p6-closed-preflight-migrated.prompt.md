---
name: "S13 ADR-002 P6 closed (PreFlight migration) — full GitHub release pipeline validated, ready for P7 Package boss-fight"
description: "Priming prompt for the next agent entering janset2d/sdl2-cs-bindings after the P6 closer landed (S12). S12 migrated `Features/Preflight/` → `Targets/PreFlightCheck/`, established root-level `Validation/` named concept with domain alt-folders housing all seven cross-cutting validators behind a single `AddValidators()` registration, retired `IReadOnlyDictionary<string, NuGetVersion>` end-to-end (boundaries + pipeline internals), added `IVcpkgManifestRepository` (mirrors `IManifestRepository` symmetry), added `ManifestFamilyNameInvariantValidator` (G59 — lowercase-kebab pattern enforcement), renamed G58-prefixed types to `CrossFamilyDependency*`, deleted 5 OneOf result types, amended ADR-002 §8 to declare `Validation/` an explicit exception (uniform `IFoo` + `AddSingleton<IFoo, Foo>()` shape). Validated end-to-end: 525 tests PASS, slopwatch 0 issues, ci-sim 8/8 PASS on Windows + WSL + macOS, paranoid setup + standalone NativeSmoke + ConsumerSmoke also green on all 3 hosts, **GitHub Actions release pipeline run `25575839739` 22/22 jobs PASS, 10 nupkgs pushed to GitHub Packages staging**. P0–P6 closed. Next is **P7 Package boss-fight migration** — decomposing the largest pipeline in the build host."
argument-hint: "Start by verifying current git state (HEAD should be the S12 commit on top of `730ce5c docs: add P6 PreFlightCheck migration design spec`). Then design S13 — P7 Package migration is the natural next per ADR-002 §11 sequencing. Per refactor plan §11 P7, this is the second boss fight: decompose `Features/Packaging/PackagePipeline.cs` (~564 lines mixing family selection, version mapping, pack invocation, cross-family dependency normalization, post-pack guardrail validation, license consolidation evidence checks, harvest-readiness gating) into a `Targets/Package/` module with named collaborators. Real domain work, larger surface than P6."
agent: "agent"
model: "Claude Opus 4.7 (1M context)"
---

You are an engineer entering the janset2d/sdl2-cs-bindings repository after the **P6 closer** of the ADR-002 build-host refactor. One slice closed P6:

**S12 — PreFlight migration** (commit on top of `730ce5c docs: add P6 PreFlightCheck migration design spec`). Closes P6. The slice spec/plan + handoff prompt were temp scratch and have been deleted post-ship per `docs/refactoring/README.md` doc lifecycle. **All durable findings are in canonical docs.**

**P6 is closed. Next phase is P7 — Package migration to `Targets/Package/`.** This is the second real boss fight of the ADR-002 refactor (largest pipeline in the build host), bigger surface than P6's PreFlight migration.

## First Principle

> Treat every claim here as **current-as-of-authoring (`2026-05-09` — `s13-p6-closed-preflight-migrated`)** and verify against the live repo, git log, and canonical docs before acting.

## What Just Happened

### S12 — PreFlight migration (commit on top of `730ce5c`)

**Production deletions (8 files):**

- `Features/Preflight/` directory retired entirely:
  - `PreflightPipeline.cs`, `PreFlightCheckTask.cs`, `PreflightRequest.cs`, `PreflightError.cs`, `ServiceCollectionExtensions.cs`
  - 4 OneOf result types: `VersionConsistencyResult.cs`, `CoreLibraryIdentityResult.cs`, `CsprojPackContractResult.cs`
- `Shared/Versioning/UpstreamVersionAlignmentResult.cs` (5th OneOf type) + `IUpstreamVersionAlignmentValidator.cs` (relocated)
- `Shared/Packaging/IG58CrossFamilyDepResolvabilityValidator.cs` (G58-prefixed name)

**Production additions (new files):**

- `Targets/PreFlightCheck/PreFlightCheckTask.cs` — owns orchestration directly, 11-param ctor (3 repos + 7 validators + reporter), no private methods, no path plumbing.
- `Targets/PreFlightCheck/ServiceCollectionExtensions.cs` — `AddPreFlightCheck()` registers only the reporter; validators come from `AddValidators()`.
- `Targets/PreFlightCheck/Reporting/PreflightReporter.cs` — relocated from `Features/Preflight/`, gained `ReportManifestFamilyNameInvariant(ValidationReport)` for G59.
- `Repositories/{IVcpkgManifestRepository, VcpkgManifestRepository}.cs` — mirrors `IManifestRepository` symmetry.
- `Validation/ServiceCollectionExtensions.cs` — single `AddValidators()` registration point: `AddSingleton<IFoo, Foo>()` for all 7 validators.
- `Validation/Manifest/{IManifestFamilyNameInvariantValidator, ManifestFamilyNameInvariantValidator}.cs` — new G59 guardrail with `[GeneratedRegex]` source generator (`matchTimeoutMilliseconds: 100`).
- `Validation/Manifest/{ICoreLibraryIdentityValidator, IVersionConsistencyValidator}.cs` — new interfaces for previously-static validators.
- `Validation/Versioning/{IUpstreamVersionAlignmentValidator, ICrossFamilyDependencyResolvabilityValidator}.cs` — interfaces (Upstream relocated + retargeted, G58 renamed `CrossFamilyDependency*`).
- `Validation/Packaging/IHybridStaticOverlayValidator.cs` — new interface for the existing impl.
- `Validation/Models/LibraryVersionCheck.cs` — extracted from `PreflightValidationModels.cs`.

**Production renames + relocations (`git mv` preserves history):**

- `Features/Preflight/{CoreLibraryIdentityValidator, CsprojPackContractValidator, ICsprojPackContractValidator, HybridStaticOverlayValidator, VersionConsistencyValidator, FamilyIdentifierConventions, PreflightValidationModels, CsprojPackContractModels}.cs` → `Validation/{Manifest, Packaging, Conventions, Models}/...`
- `Shared/Versioning/UpstreamVersionAlignment{Validator, Validation}.cs` → `Validation/{Versioning, Models}/...`
- `Shared/Packaging/{G58CrossFamilyDepResolvabilityValidator, G58CrossFamilyCheckModels}.cs` → `Validation/{Versioning, Models}/CrossFamilyDependency*.cs` (renamed)
- `Features/Preflight/PreflightReporter.cs` → `Targets/PreFlightCheck/Reporting/PreflightReporter.cs`
- `Shared/Versioning/` directory deleted (empty after relocations).

**Static→instance refactor:**

- `VersionConsistencyValidator` was static; now instance + interface. Path metadata (`manifestPath`, `vcpkgManifestPath`) dropped from validator surface — reporter hardcodes "build/manifest.json" / "vcpkg.json".
- `CoreLibraryIdentityValidator` was static; now instance + interface.
- `VersionConsistencyValidator.TryParseSemanticVersion` collapsed to `NuGetVersion.TryParse` delegation (post-review cleanup); custom parser dropped.

**Decision: full typed `PackageFamilyVersionSet` boundary end-to-end (B-mode).**

- `Host/Configuration/PackageBuildConfiguration.FamilyVersions: PackageFamilyVersionSet` (rename + type swap from `FamilyVersionMapping: IReadOnlyDictionary<...>`).
- `Program.cs` factory builds `PackageFamilyVersionSet` directly (no `.ToDictionary(...)` bridge).
- `PackRequest`, `PackageConsumerSmokeRequest`, `PublishRequest` — `Versions: PackageFamilyVersionSet`.
- `IUpstreamVersionAlignmentValidator.Validate(ManifestConfig, PackageFamilyVersionSet)` — single signature, dict overload deleted.
- `ICrossFamilyDependencyResolvabilityValidator.Validate(PackageFamilyVersionSet, ManifestConfig)` — typed.
- `PackagePipeline`, `PackageConsumerSmokePipeline`, `PublishPipeline` — internal helpers + lookups all converted to consume `PackageFamilyVersionSet` directly. No dict bridge anywhere. Lookup pattern: `set.RequireVersion(new PackageFamilyId(name))` / `set.Contains(id)` / `set.TryGetVersion(id, out var v)`.
- `grep -rn "Dictionary<string,\s*NuGetVersion>\|IReadOnlyDictionary<string,\s*NuGetVersion>" build/_build` returns **ZERO** matches in production code.

**Design refactors:**

- `UpstreamVersionAlignmentValidation.CheckedFamilies` excludes `DuplicateFamilyName`/`DuplicateLibraryName` rows — reporter's "evaluated count" reflects reality on manifest-shape errors.
- `PreflightReporter` G-number style normalized to `[Gnn]` brackets everywhere (`[G16]`, `[G49]`, `[G54]`, `[G58]`, `[G59]`); previously mixed brackets + parens.
- `Versioning/PackageFamilyVersionSet` XML doc cleaned — drops "replaces raw IReadOnlyDictionary" framing (old shape fully retired).

**S12 — Tests**

**Deletions (5 files):**

- `Unit/Features/Preflight/PreFlightCheckTaskRunTests.cs` (V1 PreFlight task tests; replaced by V2 scenarios)
- `Unit/Features/Preflight/PreflightRequestTests.cs` (PreflightRequest retired)
- `Unit/Features/Preflight/CoreLibraryIdentityValidatorTests.cs` (relocated, see below)
- `Unit/Shared/Versioning/UpstreamVersionAlignmentValidatorTests.cs` (relocated)
- `Unit/Validation/Manifest/SemanticVersionParsingTests.cs` (TryParseSemanticVersion collapsed; tests obsolete)

**Renames + updates (`git mv` first):**

- `Unit/Features/Preflight/{CoreLibraryIdentityValidatorTests, CsprojPackContractValidatorTests, FamilyIdentifierConventionsTests, HybridStaticOverlayValidatorTests, VersionConsistencyTests, VersionConsistencyValidatorTests}.cs` → `Unit/Validation/{Manifest, Conventions, Packaging}/...`
- `Unit/Features/Packaging/G58CrossFamilyDepResolvabilityValidatorTests.cs` → `Unit/Validation/Versioning/CrossFamilyDependencyResolvabilityValidatorTests.cs`
- `Unit/Shared/Versioning/UpstreamVersionAlignmentValidatorTests.cs` → `Unit/Validation/Versioning/UpstreamVersionAlignmentValidatorTests.cs`
- 3 test classes sealed + methods renamed to `<Method>_Should_<Verb>...` per AGENTS.md naming convention (post-cleanup pass).

**Additions:**

- `Unit/Repositories/VcpkgManifestRepositoryTests.cs` — 5 unit cases (file-exists happy path, missing-file CakeException, 3 ctor null-guard checks).
- `Unit/Validation/Manifest/ManifestFamilyNameInvariantValidatorTests.cs` — 6 unit cases for G59 (canonical pass, uppercase fail, underscore fail, empty fail, non-digit major fail, multi-violation aggregation).
- `Scenarios/PreFlightCheck/PreFlightCheckTaskScenarioTests.cs` — happy path + 8 failure paths (6 validator-specific + 2 boundary preconditions). **Each failure-path scenario asserts `result.Log.HasMessage(LogLevel.Error, "[Gnn]" | validator-keyword)`** so the test fails for the right reason rather than green-by-coincidence.

**New embedded fixtures (4 JSONs):**

- `Fixtures/Data/Vcpkg/{vcpkg-valid.json, vcpkg-version-mismatch.json}`
- `Fixtures/Data/Versions/{versions-upstream-mismatch.json, versions-cross-family-missing.json}`

**Test fixture mechanical updates:**

- `Fixtures/FakeRepoBuilder.cs` + `Fixtures/FakeCakeWorldV2.cs` — `PackageBuildConfiguration` ctor switched from `Dictionary<string, NuGetVersion>` to `PackageFamilyVersionSet.Empty`.
- `VcpkgManifestRepository` converted to primary constructor for sibling-style consistency with `ManifestRepository`.
- Stale `IStrategyResolver` comment removed from `Unit/CompositionRoot/ServiceCollectionExtensionsSmokeTests.cs:58` (S11 retired strategy; comment was orphan).

**No changes to release.yml.** Cake target names + CLI option contracts unchanged.

### S12 — Docs

- **`docs/refactoring/target-centric-build-host-refactor-plan.md`** — §3 hotspots row updated (`PreflightPipeline.cs` retired); §6 named concepts table gained `Validation` row; §11 P6 status block: closed (S12, deliverables, OneOf 12→7 surviving, post-commit cross-platform validation results, parking-lot pointers).
- **`docs/refactoring/target-centric-build-host-review-checklist.md`** — §6 added `Validation/` as elevated named concept; §11 confirms `PreflightPipeline` retired + lists surviving 7 OneOf types.
- **`docs/decisions/2026-05-05-target-centric-build-host.md`** — §8 amendment note: `Validation/` is an explicit exception to the ceremonial-IFoo discouragement (uniform `IFoo` + `AddSingleton<IFoo, Foo>()` for all 7 validators; `CA1822`/`S2325` analyzers silently satisfied; bounded to `Validation/`).
- **`docs/knowledge-base/release-guardrails.md`** — G59 row added (Manifest Family Name Invariant); PreFlight stage guardrail list gained G59; failure-mode catalog gained mixed-case manifest entry row.
- **`docs/plan.md`** — Phase X status sentence updated for P6 closure + cross-platform validation matrix + bumped test count to 525.
- **`docs/phases/README.md`** — Active Phase X paragraph drift-corrected (was stale at "P1 baseline next"; now reflects "P0–P6 closed... P7 next").
- **`docs/parking-lot.md`** — OneOf-Shaped Result Types entry updated (S11 retired 5 + S12 retired 5; surviving 7 enumerated, hedge dropped). New entries: PreflightReporter `IAnsiConsole` migration; `Build.Validation.Versioning` ↔ `Build.Versioning` namespace duplication; `PreFlightCheckTask` ctor style consistency; validator analyzer suppressions closure audit; CsprojPackContractValidatorTests pending V2 migration; `PreFlightCheckTask` sync-vs-async signature decision; `PackageFamilyVersionSet` API refinement.

### S12 — Slopwatch

Baseline rebuilt (`slopwatch init -f --exclude "..."`) — dropped entries pointing at deleted `Features/Preflight/`, `Shared/Versioning/`, `Shared/Packaging/G58*` paths. **48 → 48 entries** (carryover from non-slice scope).

### Multi-agent code review

Five independent reviewers ran on the staged diff post-ci-sim, pre-commit:

1. **Full-context architect** (all canonical docs): ship-it.
2. **Necessary-context architect** (AGENTS.md + ADR-002 + slice spec only): ship-with-cleanup.
3. **Test-quality specialist** (testing-guidelines.md + review-checklist §10/§13): tests-need-work.
4. **Unbiased cold-review #1** (no docs, pure C# lens): ship-with-fixes.
5. **Unbiased cold-review #2** (no docs, architecture lens): ship-with-fixes.

Cross-cutting findings absorbed into the cleanup commit (TryParseSemanticVersion → NuGetVersion.TryParse, scenario assertion tightening, sealed/Should renames, OneOf count fix, review-checklist update, VcpkgManifestRepository primary ctor, real-FS test removal, UpstreamVersionAlignment CheckedFamilies fix). Other findings deferred to parking-lot Phase X entries (see above). Some Agent claims were verified false-positive (e.g., `CoreLibraryIdentityValidator` was already sealed; PreFlightCheckTask "primary-ctor field shadowing dead weight" claim was technically wrong — C# compiler doesn't capture primary-ctor params unused outside ctor body).

### Cross-platform validation (post-commit)

Validated S12 end-to-end on three host families, **two passes each**:

**ci-sim pass (full 8-stage pipeline replay):**

| Platform | RID | Time | Result |
| --- | --- | --- | --- |
| Windows | win-x64 | 176s | 8/8 PASS |
| WSL Ubuntu | linux-x64 | 230s | 8/8 PASS |
| macOS Intel | osx-x64 | 199s | 8/8 PASS |

**Paranoid pass (`setup --source=local` auto-clean + standalone NativeSmoke + standalone PackageConsumerSmoke):**

| Platform | setup | NativeSmoke | ConsumerSmoke (TFM coverage) |
| --- | --- | --- | --- |
| Windows | 6/6 PASS, 5 nupkgs verified, props refreshed | 29/29 native tests (20.6s) | 47 tests across net10/net9/net8/net462 (71.3s) |
| WSL | 6/6 PASS | PASS | 36 tests across net10/net9/net8 (36.9s); net462 skipped (Mono can't host TUnit on Linux — documented `MissingMethodException`) |
| macOS | 6/6 PASS, 5 nupkgs (119.1s) | PASS | 36 tests across net10/net9/net8 (59.3s); net462 skipped (no Mono in PATH on macos-15) |

**zsh quoting gotcha re-validated**: macOS retry needed because `echo === ... ===` in chained zsh -lc command tripped on zsh's `==` regex match operator. Fixed by removing inline `echo` separators or quoting properly. Documented in `feedback_wsl_macos_zsh.md` agent memory.

### GitHub release pipeline validation

Run `25575839739`: **22/22 jobs PASS** (1 disabled by design: `Publish (Public)` — PD-7 stub-only). Critical observations:

- **PreFlight (23s)** — migrated `Targets/PreFlightCheck/PreFlightCheckTask` ran clean; all 7 validators green against real `manifest.json` + `vcpkg.json` + resolved versions.
- **Pack (2m33s)** — converted `PackagePipeline` consuming `PackageFamilyVersionSet` directly; 10 nupkgs emitted; G21–G27 / G46–G48 / G51–G58 post-pack assertions green.
- **Publish (Staging) (27s)** — converted `PublishPipeline` consuming `PackageFamilyVersionSet` directly; 10 nupkgs (5 families × {managed, native}) pushed to GitHub Packages within a 5-second window:
  - `Janset.SDL2.Core` + `.Native` → `2.32.0-ci.25575839739.1`
  - `Janset.SDL2.Image` + `.Native` → `2.8.0-ci.25575839739.1`
  - `Janset.SDL2.Mixer` + `.Native` → `2.8.0-ci.25575839739.1`
  - `Janset.SDL2.Ttf` + `.Native` → `2.24.0-ci.25575839739.1`
  - `Janset.SDL2.Gfx` + `.Native` → `1.0.0-ci.25575839739.1`
- **G54 upstream alignment** ran clean — versions wouldn't have packed otherwise (each family's Major.Minor matches its `library_manifests[].vcpkg_version` Major.Minor).

**4 NOTICE annotations** about `windows-2025` runner deprecation by 2026-05-12 — already a Phase X parking-lot item (`docs/plan.md`). Not blocking.

## Current State Verification (do this first)

```pwsh
git status
git log -3 --oneline
dotnet build build/_build/Build.csproj
dotnet test --project build/_build.Tests/Build.Tests.csproj -c Release --framework net10.0
slopwatch analyze --fail-on warning --exclude "artifacts/**,external/**,vcpkg_installed/**,**/bin/**,**/obj/**"
```

Expected:

- HEAD is the S12 commit on top of `730ce5c docs: add P6 PreFlightCheck migration design spec`.
- Working tree clean (or untracked S13-temp scratch only).
- Build: 0 W / 0 E.
- Tests: 525 PASS.
- Slopwatch: 0 issues vs baseline.

```pwsh
ls build/_build/Validation
ls build/_build/Targets/PreFlightCheck
ls build/_build/Features/Preflight 2>&1   # MUST NOT exist
ls build/_build/Shared/Versioning 2>&1     # MUST NOT exist
grep -rn "IReadOnlyDictionary<string, NuGetVersion>" build/_build --include "*.cs"
grep -rn "Dictionary<string, NuGetVersion>" build/_build --include "*.cs"
```

Expected:

- `Validation/{Manifest,Versioning,Packaging,Models,Conventions}/` populated (7 validators + 7 interfaces + models + conventions + `ServiceCollectionExtensions.cs`).
- `Targets/PreFlightCheck/{PreFlightCheckTask, ServiceCollectionExtensions, Reporting/PreflightReporter}.cs` exists.
- `Features/Preflight/` and `Shared/Versioning/` directories DO NOT exist.
- ZERO matches for either dictionary type in production code.

## What's Next — P7 Package boss-fight migration

Per refactor plan §11 P7 ([`docs/refactoring/target-centric-build-host-refactor-plan.md`](../../docs/refactoring/target-centric-build-host-refactor-plan.md)):

> Goal: decompose the package boss fight without losing behavior.

### Scope sketch

`Features/Packaging/PackagePipeline.cs` is currently ~564 lines mixing:

- Family selection + topological ordering (`ResolveSelectedFamilies`, `FamilyTopologyHelpers`)
- Per-family pack invocation (`PackFamilyAsync`, native-then-managed)
- Cross-family dependency range normalization (`NormalizeCrossFamilyDependencyRangesAsync`, ZipArchive nuspec rewrite)
- Harvest-readiness gating (`EnsureHarvestReadyAsync`, `AssertConsolidationReceiptValid`, `AssertPayloadSubtreesPopulated`)
- Cross-family resolvability re-check (delegates to `ICrossFamilyDependencyResolvabilityValidator` from S12 work)
- Post-pack output validation (delegates to `IPackageOutputValidator`)
- HEAD SHA resolution for `<repository>` element (`ResolveHeadCommitSha`, Cake.Git)
- Logging diagnostics + `dotnet pack` error translation

### Target shape (per refactor plan §11 P7 Candidate target module)

```text
Targets/
  Package/
    PackageTask.cs
    Requests/
      PackageRequest.cs
    Services/
      PackageFamilySelector.cs
      PackageFamilyPacker.cs
      DependencyRangeNormalizer.cs
      DotNetPackInvoker.cs           # integrations/DotNet/DotNetPackInvoker — relocate target-local
    Validation/
      HarvestReadinessValidator.cs
      PackageOutputValidator.cs
      CrossFamilyResolvableValidator.cs   # already lives in Validation/Versioning/ from S12
    Reporting/
      PackageReporter.cs
    Models/
      PackagePlan.cs
```

### Behavior to preserve

- D-3seg version mapping.
- family-lock between managed/native packages (same `--explicit-version <family>=<semver>` produces both).
- within-family minimum dependency range (managed `>= x.y.z` to native).
- cross-family lower/upper dependency range (`>= x.y.z` lower bound + `< (UpstreamMajor + 1).0.0` upper bound).
- G58 cross-family scope resolvability (re-check at pack time).
- harvest output readiness checks (consolidation receipt + payload subtree presence).
- nuspec/package payload validation (G21-G27, G46-G48, G51-G58).
- package output paths expected by `tools.cs` and `release.yml` (`artifacts/packages/{family}.{version}.nupkg`).

### Exit criteria

- `PackageTask` owns orchestration, reads as a build story.
- `PackagePipeline` deleted.
- 7 surviving OneOf result types in scope: `DotNetPackResult` + `ProjectMetadataResult` retire to `Result<T, TError>` patterns when their pipelines get reshaped.
- Pack OneOf retirements should bring the parking-lot count from 7 to ~4-5 (depending on how the validators decompose).
- Scenario tests at `Scenarios/Package/PackageTaskScenarioTests.cs` covering: happy path (multi-family pack with cross-family dep), missing harvest output fails clearly, versions file lacking required family fails, native+managed version-pair coherence, cross-family dependency range normalization correct, selected scope vs implied dependencies.
- Test count growth: ~10-15 new scenarios, possibly some unit tests for new collaborators.

### Slice estimate

P7 is **larger than P6**. Expect 2-3x the diff surface (PackagePipeline alone is ~3x the size of PreflightPipeline was). Multiple commits feasible if Deniz wants checkpoint reviews mid-slice.

## Open Phase X items (parking-lot)

Per [`docs/parking-lot.md`](../../docs/parking-lot.md), surfaced during S12 + still open:

1. **PreflightReporter `IAnsiConsole` migration** — currently uses `ICakeLog`; sibling migrated targets use `IAnsiConsole` for richer output.
2. **`Build.Validation.Versioning` ↔ `Build.Versioning` namespace duplication** — re-read post-P10.
3. **`PreFlightCheckTask` ctor style consistency** — 11 explicit `private readonly` fields + null-checks vs sibling primary-ctor-as-capture pattern. Style decision.
4. **Validator analyzer suppressions closure audit** — confirm `Validation/` stays free of `CA1822`/`S2325` `[SuppressMessage]` attributes during P10.
5. **`CsprojPackContractValidatorTests` pending V2 migration** — touched in S12 (relocated) but body still uses V1 `FakeRepoBuilder`. Mechanical V2 conversion deferred.
6. **`PreFlightCheckTask` sync-vs-async signature** — derives from `AsyncFrostingTask` but body is synchronous. Derive from `FrostingTask<BuildContext>` instead, or document intentional sync-only design.
7. **`PackageFamilyVersionSet` API refinement** — `IReadOnlyCollection` vs lookup boundary, `GetEnumerator` allocation, `GetHashCode` double-lookup.
8. **`UpstreamVersionAlignment` CheckedFamilies bug** — fixed in S12 cleanup pass; closed.
9. **`tools.cs` naming clarity (build vs setup)** — Deniz raised post-S12: `build` is a Cake passthrough escape hatch, `setup` is the local-dev environment bootstrap, `ci-sim` is the full CI replay. Decision pending: rename `build`→`cake` (Option A) or doc-only clarification (Option D).
10. **Harvest job per-RID time variance + vcpkg cache key mutability** — Linux jobs (~57s–1m15s) significantly faster than Windows/macOS (1m38s–2m33s). Run `25575839739` log inspection: BOTH cache layers worked correctly this time — GH Actions `Cache hit for: vcpkg-bin-windows-2025-x64-windows-hybrid-...` (~3s restore) AND vcpkg's binary cache layer "Restored 29 package(s) from .vcpkg-cache in 1.8 s" (NO recompile). But the cache key composition has a **latent risk**: `windows-2025` is a mutable runner label, and the key doesn't include runner image version. If GitHub rolls the runner image with an MSVC patch, the GH Actions cache HIT can be a false positive while vcpkg's compiler ABI hash differs, forcing recompile of all 29 packages from source (5-30 min/run). Real-world variance breakdown (NOT caching):
    - **macOS `platform-build-prereqs` (42-63s)** — `brew install` of build tooling on every run. **Largest single optimization win**: cache `/opt/homebrew` separately keyed on a brewfile hash (~60–100s savings × 2 macOS RIDs).
    - **Windows vcpkg fresh tool download (~31s)** — `vcpkg install` downloads its vendored CMake 4.2.3 (~9s) and PowerShell 7.5.4 (~22s) every run because they live outside `.vcpkg-cache/`. **Concrete fix**: extend cache `path:` to include `external/vcpkg/downloads/`. Saves ~31s × 3 Windows RIDs = 1.5 min per release pipeline.
    - **Windows compiler ABI detection (~22s)** — runs cl.exe to compute compiler hash for triplet. Unavoidable on Windows; affects every run.
    - **Windows checkout submodule init (28-50s)** — pulls full vcpkg port history. Try `submodules: false` + a separate `git submodule update --init --depth=1 external/vcpkg` step.
    - **macOS Harvest (24s on osx-x64 vs 6-7s on Linux)** — `otool -L` recursive closure walking slower than Unix `ldd`. Domain-specific; not caching. Out of scope.
    Concrete fix proposals captured in [parking-lot.md](../../docs/parking-lot.md) "Vcpkg Cache Key Mutability + CI Cache Audit" entry: include `runner.image_version` in cache key, cache `external/vcpkg/downloads/`, add Defender exclusion for Windows `.vcpkg-cache/`, consider container-fy for Windows runners. Worth a dedicated "CI cache audit" Phase X slice after P7-P9 boss fights settle.
11. **`windows-2025` runner deprecation by 2026-05-12** — `manifest.json runtimes[].runner` references it for `win-x64` and `win-x86`. GH redirects to `windows-2025-vs2026`. Update before deprecation date and re-run release pipeline.

12. **Windows runner architecture mapping (3 CPU archs) — current setup is correct** — investigated post-S12 against the GH Actions runner availability docs ([arm64 GA Sep 2024](https://github.blog/changelog/2024-09-03-github-actions-arm64-linux-and-windows-runners-are-now-generally-available/), [Windows arm64 public preview Apr 2025](https://github.blog/changelog/2025-04-14-windows-arm64-hosted-runners-now-available-in-public-preview/), [arm64 in private repos Jan 2026](https://github.blog/changelog/2026-01-29-arm64-standard-runners-are-now-available-in-private-repositories/)). Mapping: `win-x64` → `windows-2025` (Intel x64 native); `win-arm64` → `windows-11-arm` (ARM64 native, NOT x64-host cross-compile); `win-x86` → `windows-2025` (Intel x64 cross-compiling x86 via MSVC `/arch:IA32` — no native x86 runner exists on GitHub Actions, this is the only viable path). All three are correct. Deniz's intuition that "x64 compiles both x86 and arm" was half-right (win-x86 IS x64 host) and half-wrong (win-arm64 is on native ARM hardware). Documented in [parking-lot.md](../../docs/parking-lot.md) "Windows Runner Architecture Plan (3 CPU architectures)" entry.

13. **Cache invalidation cost — confirmed ~18-min penalty per cache-busting commit** — release pipeline run timing comparison (5 successful runs spanning May 1-8, 2026) shows clear pattern: cache MISS runs take 30 min total (Harvest jobs 6m33s-20m8s, all-29-package cold compile from source); cache HIT runs take 11-12 min (Harvest 57s-2m33s). Smoking gun: run `25550425138` (post-S11 retirement, May 8 morning) was cold MISS; run `25575839739` (S12 release, May 8 evening) HIT the warm cache. The S11 strategy retirement touched `vcpkg-overlay-triplets/`, busting the hash component. Each cache-busting commit pays this 18-min penalty once on the first release pipeline run after. Phase X CI cache audit candidate fixes: pre-warm cache on master push (separate workflow), include runner image version in cache key, cache `external/vcpkg/downloads/` for Windows tools.

## Communication style (Deniz preferences)

- Talkative + practical, sometimes clever humor.
- Innovative but prioritize what works.
- Challenge decisions when needed; explain reasoning. **Avoid yes-person behavior.**
- Bilingual: Turkish + English interchangeably.
- **Skip micro-checkpoints during execution** — batch routine work, stop only at significant boundaries (per `feedback_skip_micro_checkpoints.md`). Phase boundaries are usually significant; individual tasks within a phase usually aren't.

## Useful commands

```pwsh
# Build host
dotnet build build/_build/Build.csproj

# Build-host regression suite (TUnit on MTP)
dotnet test --project build/_build.Tests/Build.Tests.csproj -c Release --framework net10.0

# Targets exercised end-to-end after Phase 4 finish (final smoke)
dotnet run --project build/_build -- --target ResolveVersionsFromManifest --suffix p7.test
dotnet run --project build/_build -- --target Package --versions-file artifacts/resolve-versions/versions.json

# tools.cs setup (local-dev environment bootstrap, auto-clean artifacts/)
dotnet run --file tools.cs -- setup --source=local

# tools.cs ci-sim (8-stage full CI replay)
dotnet run --file tools.cs -- ci-sim

# tools.cs build (Cake passthrough escape hatch)
dotnet run --file tools.cs -- build --tree
dotnet run --file tools.cs -- build --target Info

# Slopwatch
slopwatch analyze --fail-on warning --exclude "artifacts/**,external/**,vcpkg_installed/**,**/bin/**,**/obj/**"
slopwatch init -f --exclude "artifacts/**,external/**,vcpkg_installed/**,**/bin/**,**/obj/**"

# WSL ci-sim
wsl -- zsh -lc 'cd /home/deniz/repos/sdl2-cs-bindings && git fetch origin && git reset --hard origin/master && git submodule update --init --recursive && dotnet run --file tools.cs -- ci-sim'

# macOS ci-sim
ssh Armut@192.168.50.178 'zsh -lc "cd /Users/armut/repos/sdl2-cs-bindings && git fetch origin && git reset --hard origin/master && git submodule update --init --recursive && dotnet run --file tools.cs -- ci-sim"'
```

## Memory check

Auto-memory at `C:\Users\deniz\.claude\projects\e--repos-my-projects-janset2d-sdl2-cs-bindings\memory\` — check `MEMORY.md` index and read relevant feedback entries before responding. Notable for the next slice:

- `feedback_skip_micro_checkpoints.md` — batch routine work, stop only at significant boundaries.
- `feedback_test_infra_v2_default.md` — touch a test → use V2.
- `feedback_git_mv_during_migrations.md` — `git mv` FIRST before content edits, preserves `git log --follow` history.
- `feedback_wsl_macos_zsh.md` — WSL/macOS use `zsh -lc` for non-interactive driving; bash doesn't load `DOTNET_ROOT`.
- `feedback_session_pacing.md` — after a slice commits, default to "what's next?" not "let me wrap up".
- `feedback_rider_for_mass_renames.md` — propose rename, let Deniz Rider-rename instead of scripting cross-file edits.
- `project_refactoring_doc_lifecycle.md` — `docs/refactoring/` mixes durable plan/ADR/checklist with temp per-slice design specs that delete after the slice ships.

## Final steering note

P7 Package is the second boss fight and the largest pipeline migration in ADR-002. Don't try to do everything in one commit — use the brainstorming skill first to pin design decisions (collaborator boundaries, what stays inline in `PackageTask`, which OneOf result types collapse to `Result<T, TError>`), then writing-plans skill to lay out commit boundaries, then executing-plans for the implementation.

After P7 closes, P8 (Harvest, NativeSmoke, ConsolidateHarvest migration) is the third boss fight, then P9 (PackageConsumerSmoke + Publish), then P10 final cleanup. The end state per ADR-002 §16 Definition of Done: `Targets/<name>/` modules everywhere, `Features/` deleted, `Host/Configuration` deleted, all OneOf result types retired (or `OneOf` package dependency dropped from `Build.csproj`).

Slice spec/plan + handoff prompt are temp scratch per `docs/refactoring/README.md` doc lifecycle. Delete after slice ships, after migrating durable findings to canonical docs (refactor plan §11 P7 status block + parking-lot Phase X entries + plan.md status sentence).
