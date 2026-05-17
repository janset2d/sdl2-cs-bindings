---
name: "Binding autogen Stage 1 — manifest-pivot completion handover (post Phase 2A+2B+2C+2D + Docker fix)"
description: "Handover prompt for the next agent entering janset2d/sdl2-cs-bindings on branch spike/binding-autogen-sdl2-gfx after the unified-slice session that landed Phase 2A (manifest.json v2.2) + Phase 2B (BindingGenerationConfig + repository) + Phase 2C (IBindingFamilyValidator + 3 implementations) + Phase 2D (manifest-driven GenerateBindingsTask + Sdl2CoreGenerationConfig + BindingPublicApiCoherenceValidator retire) plus a Docker fix (binary cache key + SDL2 source clone) — all verified via real container smoke. Working tree is dirty post-smoke; Deniz holds the commit decision. Recommended next: commit the bulk + Docker fix slice (one or two commits — split offer in the session message), then Phase 3A Rider-driven Preview→Real rename."
argument-hint: "Optional override: jump to a specific Phase 3 sub-slice (A Rider rename / B BindingModel extension / C policy extractions / D translator refactor / E per-category emitters / F overloads + dual emit / G smoke + peer-oracle diff), commit the bulk, or pick a different track (Task 7 emission flag-flip / Task 8 PreFlight stamp validator / Phase 2b). Defaults to surfacing the commit decision to Deniz, then planning Phase 3A."
agent: "agent"
model: "Claude Opus 4.7 (1M context)"
---

# Binding Autogen Stage 1 — Manifest-Pivot Completion Handover

You are entering `janset2d/sdl2-cs-bindings` on branch `spike/binding-autogen-sdl2-gfx` after a long unified-slice session that:

1. Locked the **unified design + plan** (specs/2026-05-16, plans/2026-05-17) that supersede the prior 2026-05-14 architecture spec / 2026-05-15 local-output-loop spec / Stage 1 plan / local-output-loop plan — already committed as `66c5925`.
2. Landed the **manifest-driven per-family generator pivot** end-to-end: Phase 2A (`build/manifest.json` schema_version 2.1 → 2.2 with required `binding_generation` block per family) + Phase 2B (`BindingGenerationConfig` typed record set + sync repository) + Phase 2C (`IBindingFamilyValidator` interface + 3 implementations: `DynapiCoherenceValidator`, `NeutralViewNonEmptyValidator`, `RequiredFunctionsEmittedValidator`) + Phase 2D (`GenerateBindingsTask` rewritten as manifest-driven loop; `HeaderSetResolver.Resolve(config, ...)`; `CppAstParseRunner.Parse(config, ...)`; `PlatformCatalog.For(catalogId)`; `Sdl2CoreGenerationConfig` + `BindingPublicApiCoherenceValidator` deleted entirely).
3. Surfaced and fixed a **Docker-side dynapi-manifest-reach bug** that the user's binary-cache-primed environment triggered on the first Phase 2A manifest.json bump: BuildKit cache mounts are RUN-scoped (target paths don't commit into the image layer), so the previous "buildtree cache mount" approach was conceptually wrong. The replacement uses one binary-cache mount with a content-derived id (`tools.cs ComputeVcpkgCacheKey`) plus a conditional `git clone` fallback that materialises `buildtrees/sdl2/src/sdl2-dynapi-${SDL2_VERSION}/` when vcpkg's binary-cache hit skips source extraction.
4. Verified the full pipeline with a **real container smoke** (`dotnet run --file tools.cs -- generate-bindings`): GenerateBindings task succeeded in 4m 46s; 9 output files written; per-view function counts match expected SDL2.32.10 public surface (Neutral 836; platform overlays 8 / 12 / 12 / 2 / 0 / 4 / 13); 3 family validators dispatched per manifest opt-in.

**Working tree is dirty. No commit has been made for the bulk slice.** Deniz held the commit decision after seeing the smoke succeed. The conventional commit message draft is in the prior session output (`feat(binding-autogen): manifest-driven per-family generator + Docker dynapi fix`).

Your first job is **not** to barrel into Phase 3A. Your first job is to verify the dirty surface still builds + tests pass + smoke is reproducible, read the canonical docs listed below, and surface the next decision (commit the bulk now / split into two commits / continue with Phase 3A and bundle) to Deniz.

## First Principle

> Treat this handover as current-as-of-authoring (2026-05-17). Verify against the live repo before acting. Run `git status --short`, inspect the dirty diff, and read the canonical docs listed below. Do not assume this prompt is more authoritative than current code + docs. **Where this prompt and the live plan disagree, the live plan wins** — the plan was patched mid-session with a Phase 3B SDL.h-constants finding (see "Forward findings to read carefully" below); this prompt only summarises.

## Non-Negotiable Rules (AGENTS.md + accumulated feedback)

- **Approval gate:** no commit, no deployment, no publish, no CI apply without explicit Deniz approval ("go / apply / proceed / başla / yap"). Slopwatch baseline rebuild (`slopwatch init -f --exclude ...`) is in the dirty tree but `.slopwatch/baseline.json` is the canonical place; do not re-rebuild without slice-level deletion/rename evidence.
- **No `--no-verify`, no `--no-gpg-sign`:** never skip git hooks or bypass signing.
- **Cake-native build host:** in `build/_build`, no raw `System.IO` at build boundaries. Use Cake `DirectoryPath` / `FilePath`, `ICakeContext`, `CakeFileSystemExtensions`, `CakeJsonExtensions`.
- **ADR-002 task-orchestrator pattern:** Cake tasks ARE the orchestrators. No `Runner` / `Pipeline` / `Operation` wrappers. The unified spec preserves the existing `GenerateBindingsTask`-as-orchestrator shape; per-family loop sits inside the task body, not in a separate runner type.
- **ADR-003 Data rule:** persisted / tool-read file contracts live under `build/_build/Data/<Cohort>/` with a repository abstraction. `BindingGenerationConfigRepository` consumes `IManifestRepository` (no parallel JSON walking — the earlier "270-line custom JsonDocument walker" was retracted mid-session after Deniz called out the gap; the canonical implementation walks `ManifestConfig.PackageFamilies[].LibraryRef → LibraryManifests[].Name → BindingGeneration`).
- **Validators register in `Validation/ServiceCollectionExtensions.AddValidators()`**, not in target-local extensions. `IBindingFamilyValidator` implementations live alongside cross-cutting validators (G14/G15/G16/G54/G58/G60 + harvest/native-smoke/etc.).
- **Manifest is the per-family config single source of truth.** Family-id ↔ library-name mapping comes from `manifest.PackageFamilies[].LibraryRef` — no hardcoded switch in `BindingGenerationConfigRepository`. SDL3 entries (when they land) require no code change in the repo, only manifest entries.
- **Cache invalidation discipline matches CI:** `tools.cs ComputeVcpkgCacheKey` hashes `vcpkg.json + vcpkg-overlay-ports/** + vcpkg-overlay-triplets/** + vcpkg submodule commit` — same composition as `.github/actions/vcpkg-setup/action.yml`'s `actions/cache@v5` key. Do not weaken this without aligning the CI key too.
- **Rider for mass renames:** Phase 3A Rider rename (Preview* → real names) is the next slice; agent prepares the rename map, Deniz executes via Rider. Do not script the rename.
- **No timing pressure or motive assumptions:** don't push Deniz on "when?" / "ready?". He sets the pace.
- **No workarounds or shortcuts:** never hack code to make tests pass or errors disappear. The session's biggest correction was rejecting a hand-rolled `JsonDocument` walker in favour of the existing strongly-typed `ManifestConfig` path — that lesson is permanent.
- **Cross-OS container mount caveat:** never bind-mount the Windows-host repo into a Linux container running dotnet/vcpkg; the Stage 1 Dockerfile uses COPY-into-image + isolated cache volumes for exactly this reason.
- **PowerShell on Windows host, zsh in WSL/macOS:** drive WSL non-interactively via `wsl -- zsh -lc`; macOS via `ssh ... 'zsh -lc'`. The Bash tool inside the Claude session routes to Git Bash on Windows — use POSIX commands (`rm`, not `Remove-Item`).
- **Plan-doc references**: every plan/phase/refactor doc cross-references relevant ADRs + knowledge-base guidelines. The Phase 3B SDL.h-constants finding (just landed) is the recent example of where session findings land in the live plan, not in scratch notes.

## Mandatory Grounding

Read in this order before making further changes:

1. `CLAUDE.md` (relay) → `AGENTS.md` (the real contract)
2. `docs/onboarding.md`
3. `docs/plan.md`
4. `docs/decisions/2026-05-05-target-centric-build-host.md` (ADR-002)
5. `docs/decisions/2026-05-12-build-host-data-layer.md` (ADR-003)
6. `docs/decisions/2026-05-14-binding-autogen-toolchain.md` (ADR-004 trio pin)
7. `docs/knowledge-base/extraction-guidelines.md`
8. `docs/knowledge-base/testing-guidelines.md`
9. `docs/knowledge-base/release-guardrails.md` (G60 entry §2.4 is the most recent addition)
10. `docs/binding-autogen/binding-autogen-strategy-brief.md`
11. **`docs/superpowers/specs/2026-05-16-binding-generator-unified-design.md`** — the unified design spec accepted in this session. Supersedes 2026-05-14 architecture spec + 2026-05-15 local-output-loop spec (both in `superseded/`). Read §16 P0/P1/P2/P3/PSTH absorption map to understand which legacy items the unified slice closed vs deferred.
12. **`docs/superpowers/plans/2026-05-17-binding-generator-unified-plan.md`** — the unified plan. Pay particular attention to:
    - **Phase 3B opening Finding note** — SDL.h-constants gap surfaced 2026-05-17 by the manifest-pivot smoke. Resolution requires Phase 3B + 3D + 3E coordination on `RequiredConstants` manifest field. **Critical for Phase 3E `CsConstantEmitter` agent.**
    - **Phase 3A Rider rename map** — the next slice's hand-off contract.
    - **Phase 3F friendly overloads + dual P/Invoke emit** — the architecture spec's Rule 1 + Rule 4 + Rule 6 payoff; concrete code skeleton lives in this plan.
13. `docs/playbook/binding-generator-maintenance.md` (per-family parse_defines + clang_args rationale; the durable home for paragraph-length context that doesn't fit in manifest.json)
14. `docs/parking-lot.md` index + `docs/parking-lot/validator-shape-standardization/README.md` — captures the `ValidationReport` vs custom-shape `<Topic>Validation/Check/Status` split observed across validators. Surfaces when P2-γ design-extraction work touches PreFlight reporter contract.

## Current Git / Working Tree Expectations

Expected branch:

```text
spike/binding-autogen-sdl2-gfx
```

Expected HEAD before this session's bulk dirty surface:

```text
66c5925 feat(binding-autogen): unified spec + plan; land P2-α design cleanup
9a5f59e feat(binding-autogen): Stage 1 PSTH-H/I/J hardening + VcpkgManifest consolidation
0db0e31 feat(binding-autogen): Stage 1 Task 3.5 + Post-Implementation Review fixes
```

Verify with:

```pwsh
git status --short
git diff --stat
git log --oneline -5
```

Expected dirty surface (Phase 2A+2B+2C+2D + Docker fix):

**Modified production code:**

- `build/manifest.json` — schema_version 2.1 → 2.2 + `binding_generation` block per library_manifests[] entry (SDL2 full config, 4 satellites as `enabled: false` placeholders).
- `build/_build/Data/Manifest/Models/ManifestConfigModels.cs` — `LibraryManifest.BindingGeneration` is `required`; added `using Build.Data.BindingGeneration.Models;`.
- `build/_build/Data/ServiceCollectionExtensions.cs` — registered `IBindingGenerationConfigRepository`.
- `build/_build/Validation/ServiceCollectionExtensions.cs` — registered three `IBindingFamilyValidator` implementations + removed the legacy `IBindingPublicApiCoherenceValidator` line.
- `build/_build/Targets/GenerateBindings/GenerateBindingsTask.cs` — manifest-driven per-family loop; new ctor takes `IBindingGenerationConfigRepository` + `IEnumerable<IBindingFamilyValidator>` (replaces `IDynapiManifestRepository` + `IBindingPublicApiCoherenceValidator` direct injections).
- `build/_build/Targets/GenerateBindings/HeaderSet/HeaderSetResolver.cs` — `Resolve(BindingGenerationConfig, ...)` signature; reads include_dir_glob (triplet-root-relative) / header_glob / excluded_headers / excluded_header_prefixes from config.
- `build/_build/Targets/GenerateBindings/HeaderSet/ResolvedHeaderSet.cs` — `Sdl2IncludeDirectory` renamed to `FamilyIncludeDirectory`.
- `build/_build/Targets/GenerateBindings/Parsing/CppAstParseRunner.cs` — `Parse(BindingGenerationConfig, ...)` signature; `CreateOptions` is now `static`; reads `ParseDefines` + `ClangArgs` from config; `_baseDefines` + `BaseAdditionalArguments` fields retired.
- `build/_build/Targets/GenerateBindings/Parsing/PlatformCatalog.cs` — added `For(catalogId)` static factory; `CreateSdl2Catalog()` kept behind the dispatch.
- `build/_build/Targets/GenerateBindings/Model/Preview{BindingModel,ParseView,Function,Parameter}.cs` × 4 — flipped `internal` → `public` to satisfy `IBindingFamilyValidator` signature cascade-lock.
- `build/_build/Targets/GenerateBindings/ServiceCollectionExtensions.cs` — removed validator registrations (moved to canonical `AddValidators()` location); kept parser/header collaborator registrations.
- `tools.cs` — added `ReadSdl2VcpkgVersion()` (parses `manifest.library_manifests[name=SDL2].vcpkg_version`) + `ComputeVcpkgCacheKey()` (cross-OS deterministic SHA-256-trunc-8 over vcpkg.json + recursive overlay-ports + recursive overlay-triplets + vcpkg commit); both passed as `docker build --build-arg`.
- `docker/binding-generator.Dockerfile` — Layer E: single binary-cache BuildKit mount with content-derived id `janset-vcpkg-binarycache-${VCPKG_CACHE_KEY}`; post-install conditional `git clone` fallback at `buildtrees/sdl2/src/sdl2-dynapi-${SDL2_VERSION}/` when binary-cache hit skipped source extraction. Sanity check unchanged.

**Created production code (new files):**

- `build/_build/Data/BindingGeneration/BindingGenerationConfigRepository.cs` — sync repo, consumes `IManifestRepository`, walks `PackageFamilies[].LibraryRef → LibraryManifests[].Name → BindingGeneration`. **~60 lines — no hand-rolled JSON walking. This is the post-correction shape.**
- `build/_build/Data/BindingGeneration/Models/BindingGenerationConfig.cs` — typed record set: `BindingGenerationConfig` + `HeaderSetConfig` + `RequiredFunctionConfig` + `RequiredFunctionParameter` + `DeferredDeclarationConfig` + `DynapiConfig`. All with `[JsonPropertyName]` + `required init`.
- `build/_build/Data/BindingGeneration/Models/BindingGenerationConfigError.cs` — flat record + `Kind` enum (FamilyNotFound, Disabled). Pre-deserialization failures bubble as `CakeException` from `ManifestRepository`.
- `build/_build/Validation/BindingGeneration/IBindingFamilyValidator.cs` — family-scoped validator interface (kebab-case `ValidatorId`, `ValidateAsync(PreviewBindingModel, BindingGenerationConfig, ct)`).
- `build/_build/Validation/BindingGeneration/DynapiCoherenceValidator.cs` — replaces `BindingPublicApiCoherenceValidator`. Structurally opts out when `config.Dynapi == null` (satellites + SDL3 path).
- `build/_build/Validation/BindingGeneration/NeutralViewNonEmptyValidator.cs` — fail-closed on missing or empty Neutral view.
- `build/_build/Validation/BindingGeneration/RequiredFunctionsEmittedValidator.cs` — every `config.RequiredFunctions` entry must land in Neutral.

**Created test code (new files):**

- `build/_build.Tests/Fixtures/BindingGenerationFixture.cs` — `Sdl2CoreConfig()` (mirrors live manifest defaults — extended in Phase 2D to include all parse_defines / clang_args / header_set / excluded_functions so config-driven tests resolve realistically) + `Sdl2ImagePlaceholderConfig()` + `RequiredFunction()` helper + `ModelWithNeutralFunctions` / `ModelWithMultipleViews` / `ModelWithoutNeutralView` + `FakeDynapiManifestRepository`.
- `build/_build.Tests/Unit/Data/BindingGeneration/BindingGenerationConfigRepositoryTests.cs` — 8 tests (unit + sociable round-trip).
- `build/_build.Tests/Unit/Validation/BindingGeneration/DynapiCoherenceValidatorTests.cs` — 8 tests.
- `build/_build.Tests/Unit/Validation/BindingGeneration/NeutralViewNonEmptyValidatorTests.cs` — 6 tests.
- `build/_build.Tests/Unit/Validation/BindingGeneration/RequiredFunctionsEmittedValidatorTests.cs` — 7 tests.

**Modified test code:**

- `build/_build.Tests/Fixtures/Data/Manifest/manifest-{win-x64,linux-x64,osx-x64,real}.json` + `manifest.fixture.json` — schema_version 2.1 → 2.2 + binding_generation blocks on every library_manifests[] entry (required-field satisfaction).
- `build/_build.Tests/Fixtures/ManifestFixture.cs` — `CreateTestCoreBindingGeneration()` + `CreateTestSatellitePlaceholderBindingGeneration()` factories.
- `build/_build.Tests/Unit/Data/Manifest/ManifestRepositoryTests.cs` — 2.1 → 2.2 assertion bump.
- `build/_build.Tests/Unit/Targets/GenerateBindings/HeaderSet/HeaderSetResolverTests.cs` — rewritten for new `Resolve(config, ...)` signature via `BindingGenerationFixture.Sdl2CoreConfig()`.
- `build/_build.Tests/Unit/Targets/GenerateBindings/Parsing/CppAstParseRunnerTests.cs` — rewritten for new static `CreateOptions(config, ...)` signature.
- `build/_build.Tests/Unit/Validation/Versioning/CrossFamilyDependencyResolvabilityValidatorTests.cs` — inline `LibraryManifest` ctor gained required `BindingGeneration` field via `ManifestFixture.CreateTestSatellitePlaceholderBindingGeneration(...)`.
- `build/_build.Tests/Scenarios/GenerateBindings/GenerateBindingsTaskScenarioTests.cs` — rewritten for new ctor (NSubstitute `IBindingGenerationConfigRepository` returning sdl2-core enabled config).
- `build/_build.Tests/Unit/CompositionRoot/ServiceCollectionExtensionsSmokeTests.cs` — AddGenerateBindings smoke gains `AddValidators()` chain (validators moved to canonical location).

**Deleted production code (3 files):**

- `build/_build/Targets/GenerateBindings/Sdl2CoreGenerationConfig.cs` — replaced entirely by manifest-driven `BindingGenerationConfig`.
- `build/_build/Validation/BindingGeneration/BindingPublicApiCoherenceValidator.cs` — replaced by `DynapiCoherenceValidator` on `IBindingFamilyValidator`. `SeverityProfile.cs` survives (consumed by `DynapiCoherenceValidator`).
- `build/_build.Tests/Unit/Targets/GenerateBindings/Sdl2CoreGenerationConfigTests.cs` — Sdl2CoreGenerationConfig has no successor as a record-shape pin test; manifest-driven config behaviour is covered by `BindingGenerationConfigRepositoryTests`.
- `build/_build.Tests/Unit/Validation/BindingGeneration/BindingPublicApiCoherenceValidatorTests.cs` — superseded by `DynapiCoherenceValidatorTests`.

**Slopwatch baseline rebuilt:**

- `.slopwatch/baseline.json` regenerated 2026-05-17. 44 entries, unchanged count from pre-session — new code introduced zero slop. Manual verification: none of the 11 new production files appear in the baseline.

## What Was Implemented Before This Handover (recap of `66c5925`)

`66c5925 feat(binding-autogen): unified spec + plan; land P2-α design cleanup` covered:

- Phase 1 — Docs unification: superseded `specs/2026-05-14-binding-generator-architecture-design.md` + `specs/2026-05-15-binding-generator-local-output-loop-design.md` + `plans/2026-05-14-sdl2-core-binding-generator-stage-1.md` + `plans/2026-05-15-binding-generator-local-output-loop.md` to `superseded/` with banner notes pointing at the new unified spec + plan.
- Pointer doc updates: `docs/plan.md`, `docs/phases/phase-4-binding-autogen.md`, `docs/binding-autogen/README.md`, `docs/binding-autogen/binding-autogen-strategy-brief.md`, `docs/binding-autogen/research/binding-autogen-{onboarding,spike-findings}.md`, `docs/playbook/binding-generator-maintenance.md` — all pointing at unified docs.
- New: `docs/superpowers/specs/2026-05-16-binding-generator-unified-design.md` (18 sections).
- New: `docs/superpowers/plans/2026-05-17-binding-generator-unified-plan.md` (Phase 1 + 2A + 2B + 2C + 2D + 3A + 3B + 3C + 3D + 3E + 3F + 3G).
- P2-α dead-field removal (already dirty from prior session, folded into the commit): `Sdl2CoreGenerationConfig` `OwnedPrefixes` + `DeferredDeclarations` field drops, `PathService` `<para>` doc note, `HeaderSetResolver` + `CppAstParseRunner` cascade-lock explanation comments, Stage 1 plan P2.x retraction notes.

The bulk slice this handover documents (Phase 2A+2B+2C+2D + Docker fix) builds directly on top of `66c5925`.

## Forward findings to read carefully (Phase 3B Critical)

**SDL.h-only constants gap** — surfaced by 2026-05-17 manifest-pivot smoke. Full finding in the unified plan at `docs/superpowers/plans/2026-05-17-binding-generator-unified-plan.md` Phase 3B opening note. TL;DR:

- SDL.h is excluded from per-header parse for umbrella/failure-isolation reasons; the 5 base-API functions it declares are recovered via `manifest.binding_generation.required_functions`.
- SDL.h ALSO declares ~10 `SDL_INIT_*` macros (`SDL_INIT_TIMER`, `SDL_INIT_AUDIO`, ..., compound `SDL_INIT_EVERYTHING`) that **nothing else declares**.
- Stage 1's function-only emit doesn't surface these; consumer-facing constants would be missing when `CsConstantEmitter` lands at Phase 3E.
- **Resolution** (must coordinate across Phase 3B + 3D + 3E): add `RequiredConstantConfig` record + `BindingGenerationConfig.RequiredConstants` field; translator-side `CollectConstants` merges them into the parsed-macro set (Neutral-view-anchored); `CsConstantEmitter` emits `Literal` → `const`, `Computed` → `static readonly`; manifest's SDL2 entry gets a `required_constants` array seeded with the 10 entries; maintenance playbook gets the rationale section like `required_functions`.

If you are the Phase 3E `CsConstantEmitter` agent, **this is a hard prerequisite**, not a polish item. Otherwise consumers can't call `SDL_Init(SDL_INIT_VIDEO)` because `SDL_INIT_VIDEO` won't exist managed-side.

## Container smoke evidence (2026-05-17 verification)

Real container run via `dotnet run --file tools.cs -- generate-bindings`:

- Wall time: 4m 46s (warm binary cache, conditional git clone branch fired)
- 9 output files at `artifacts/generated-bindings-preview/sdl2-core/`
- Dynapi manifest resolved from `/workspace/external/vcpkg/buildtrees/sdl2/src/sdl2-dynapi-2.32.10/src/dynapi/SDL2.exports` (our independent clone path)
- Per-view function counts:

  | View | Functions |
  |---|---|
  | Neutral | 836 |
  | WindowsDesktop | 8 |
  | WinRT | 12 |
  | GDK | 12 |
  | Linux | 2 |
  | MacOS | 0 |
  | IOS | 4 |
  | Android | 13 |

- `[neutral-view-non-empty]`: silent pass.
- `[required-functions-emitted]`: silent pass (5 required functions all in Neutral).
- `[dynapi-coherence]`: **one expected warning** about `SDL_DYNAPI_entry` — manifest lists it (libSDL2 exports it for internal loader), public headers don't declare it, our `excluded_functions` list (a no-op for this symbol because parser never saw it anyway) doesn't change the result. Warning under Stage1Generator severity profile = doesn't fail validation. Cosmetic polish later: either remove `SDL_DYNAPI_entry` from `excluded_functions` (it's a misleading vestige) or add a manifest-level "known false negatives" allowlist. Not blocking.

## Verification Status For Dirty Surface

The following passed at last check:

```pwsh
dotnet build build/_build/Build.csproj -c Release          # 0 warning, 0 error
dotnet test --project build/_build.Tests/Build.Tests.csproj -c Release --framework net10.0
# 724 / 0 / 2 (+21 net new tests vs 9a5f59e baseline of 703)
slopwatch analyze --fail-on warning --exclude "artifacts/**,external/**,vcpkg_installed/**,tools/**,**/bin/**,**/obj/**"
# 0 issues
dotnet run --project build/_build --no-build -c Release -- --target ResolveVersionsFromManifest --versions-file artifacts/resolve-versions/versions.json --suffix=local.smoke
# Succeeded
dotnet run --project build/_build --no-build -c Release -- --target PreFlightCheck --versions-file artifacts/resolve-versions/versions.json
# Succeeded — all G14/G15/G16/G19/G54/G58/G60 guardrails green
dotnet run --file tools.cs -- generate-bindings
# Succeeded — see container smoke evidence above
```

`tools.cs ci-sim` was not re-run for this slice because no changes affect the managed pipeline; the binding-autogen container is the surface that changed and that's covered by the smoke run.

## Recommended Next Step

**Don't sprint into Phase 3A.** The next decision is whether to:

1. **Commit the bulk + Docker fix as one slice now.** Single mega-commit covering Phase 2A+2B+2C+2D + Docker fix. Commit message draft was provided in the session output (subject: `feat(binding-autogen): manifest-driven per-family generator + Docker dynapi fix`). Cleanest single history entry; matches the bundle Deniz approved.
2. **Split into two commits.** `git add -p` to separate: (a) `feat(binding-autogen): manifest-driven per-family generator (Phase 2A+2B+2C+2D)` covering the manifest schema + repository + validators + task refactor + legacy delete; (b) `fix(docker): SDL2 dynapi manifest reach via cache key + independent source clone` covering Dockerfile + tools.cs + .slopwatch/baseline.json. Cleaner narrative: the Docker fix is a separable infrastructure concern.
3. **Continue with Phase 3A (Rider rename) and bundle a/b/c.** Larger diff; aligns with "Approach 3 iterative finer-grained commits" decision but bundles infrastructure with naming refactor — not recommended unless Deniz prefers fewer commit checkpoints.

**Surface options 1 and 2 to Deniz** (option 3 is generally too big for one commit). Do not commit unilaterally. The session's commit-message draft is reusable for option 1 as-is; for option 2 split it into the two natural halves at the `== Docker fix ==` boundary.

## Future Sequence Beyond This Slice

Once the bulk slice commits (one or two commits), the queued work is:

| Track | Plan section | Notes |
|---|---|---|
| **Phase 3A** | Unified plan §"Phase 3A — Rider-driven mass rename" | Rider-driven mass rename: `PreviewBindingModel` → `BindingModel`, `PreviewParseView` → `BindingParseView`, `PreviewFunction` → `BindingFunction`, `PreviewParameter` → `BindingParameter`, `CppAstToPreviewModel` → `CppAstToBindingModel`, `PreviewEmitter` → `CsCommandEmitter`, `Janset.Sdl2.Preview` namespace → `Janset.SDL2`, `Sdl2Preview_<view>` → `Sdl2_<view>`. Agent prepares the rename map, Deniz Rider-executes, agent validates post-rename via tests + slopwatch + smoke. PSTH-A naming purge folds in here. |
| **Phase 3B** | Unified plan §"Phase 3B — BindingModel extension" | Add 5 new declaration records (`BindingTypeRef`, `BindingStruct`, `BindingEnum`, `BindingConstant`, `BindingHandle`, `BindingCallback`) + extend `BindingModel`. **Read the opening Finding note** before starting — `RequiredConstants` field must be designed-in here so Phase 3D + 3E can wire it. |
| **Phase 3C** | Unified plan §"Phase 3C — Policy extractions" | Extract `TypeMappingPolicy` (P0.1 + P0.2 + P0.3 + P2.8 + P2.9 fixes), `KnownUnsupportedDeclarationPolicy`, `CoreOwnedTypeMap`. DI factories. |
| **Phase 3D** | Unified plan §"Phase 3D — CppAstToBindingModel translator refactor" | Rewrite translator to populate all 6 categories. **`CollectConstants` merges `config.RequiredConstants` into the parsed-macros set (Neutral-anchored), same pattern as `CollectViews` does for `RequiredFunctions`.** |
| **Phase 3E** | Unified plan §"Phase 3E — Per-category emitters" | `EmitContext`, `CodeWriter`, `BindingEmitter` dispatcher, six per-category emitters. **`CsConstantEmitter` distinguishes Literal (`public const`) vs Computed (`public static readonly`) — `SDL_INIT_EVERYTHING` is the canonical Computed case.** Hands-off rename validation (Phase 3A) must be green before this phase starts. |
| **Phase 3F** | Unified plan §"Phase 3F — Friendly overloads + dual P/Invoke emit" | string / Span / out / ref overloads in `CsCommandEmitter` + `#if NET7_0_OR_GREATER` dual emit. Architecture spec Rule 1 + Rule 4 + Rule 6 payoff. |
| **Phase 3G** | Unified plan §"Phase 3G — Output wiring + smoke + peer-oracle diff" | Final acceptance: 5-TFM compile of generated SDL2.Core; all three family validators pass; peer-oracle diff vs SDL2-CS / pre-Stage-1 spike / Alimer / ppy. |
| **Stage 1 Task 7** | Unified spec §"Stage 1 / Stage 2 / Stage 3 boundary" | Wire SDL2.Core consumer csproj away from `external/sdl2-cs`; emission location flag-flip `artifacts/generated-bindings-preview/sdl2-core/` → `src/SDL2.Core/Generated/`. Separate slice. |
| **Stage 1 Task 8** | Unified spec §"Stage 1 / Stage 2 / Stage 3 boundary" | `BindingGenerationCoherenceValidator` PreFlight stamp drift validator. Consumes `IGeneratedStampRepository` + `HeaderSetFingerprintCalculator` (P2.4/P2.5 forward-looking surfaces). Separate slice. |
| **Stage 2** | Unified spec §"Stage 1 / Stage 2 / Stage 3 boundary" | SDL2 satellites: per-family slices flip `binding_generation.enabled: false → true`. `CoreOwnedTypeMap` enforcement activates for real here. |
| **Phase 2b** | `docs/plan.md` §"Phase 2b" | Unrelated CI/CD work; out of Stage 1 scope. |

## Things To Be Careful About

- **Do not reintroduce `IBindingPublicApiCoherenceValidator` or `Sdl2CoreGenerationConfig.Default`.** Both were retired by Phase 2D; their replacements are `DynapiCoherenceValidator` (on `IBindingFamilyValidator`) and `BindingGenerationConfigRepository.Load(familyId)`. If a future review surfaces them as "missing", point at the deletion commits.
- **Do not reintroduce a hand-rolled `JsonDocument` walker for binding-generation config.** The strongly-typed `ManifestConfig.LibraryManifests[].BindingGeneration` path is the canonical shape. Mid-session correction (Deniz's pushback) made this permanent; the lesson is: extend existing strongly-typed models, don't invent parallel parsers.
- **Do not add `--family` CLI arg to `BuildContext` yet.** Stage 1 has only sdl2-core enabled; default loop behaviour is identical to the previous single-family path. Add only when satellite generation needs narrowing (Stage 2 territory).
- **Do not weaken the CI/Docker cache key parity.** `tools.cs ComputeVcpkgCacheKey` mirrors `.github/actions/vcpkg-setup/action.yml`'s `actions/cache@v5` key composition (vcpkg.json + recursive overlay-ports + recursive overlay-triplets + vcpkg submodule commit). Changing one without the other creates local-vs-CI invalidation skew.
- **Do not put cache mounts on paths whose content needs to land in the image layer.** BuildKit cache mounts are RUN-scoped (target paths don't commit). This was the abandoned-mid-session approach; the conditional `git clone` fallback is the correct shape for content that must be image-layer-resident.
- **Do not break the manifest schema v2.2 contract by adding new `binding_generation` fields without coordinating model + tests.** `BindingGenerationConfig` has `required init` fields — adding a required field without updating all 7 fixture JSONs + `ManifestFixture.cs` factories will break the 724 test suite.
- **Do not flip individual types from `public` to `internal` in `Targets/GenerateBindings/`.** The Phase 2C public-flip of `PreviewBindingModel` / `PreviewParseView` / `PreviewFunction` / `PreviewParameter` is part of the public Cake Frosting Task signature cascade-lock (same as P2.6's `HeaderSetResolver` + `ICppAstParseRunner` finding). Flip the convention as a whole-Task-class slice or not at all.
- **Do not commit `artifacts/generated-bindings-preview/`.** Gitignored. The 9 files from the smoke run are non-source artifacts.
- **Do not delete `.slopwatch/baseline.json` regeneration commits.** The baseline was rebuilt 2026-05-17 (44 entries, same count, zero new slop). If you delete code in a future slice that has baseline entries, rebuild baseline with `slopwatch init -f --exclude "artifacts/**,external/**,vcpkg_installed/**,tools/**,**/bin/**,**/obj/**"`.
- **Do not use `dotnet test --filter` or `dotnet test --nologo`.** TUnit / Microsoft.Testing.Platform doesn't support `--filter`; `--nologo` confuses the MTP adapter and produces "Zero tests ran" with exit code 5. Plain `dotnet test --project build/_build.Tests/Build.Tests.csproj` works.
- **Do not run `git commit` unless Deniz explicitly approves.** The current dirty surface is significant (~30 files, ~600 lines net add).

## Useful Commands

```pwsh
# Repo state
git status --short
git diff --stat
git log --oneline -5

# Build + tests (Phase 2 acceptance gates)
dotnet build build/_build/Build.csproj -c Release
dotnet build build/_build.Tests/Build.Tests.csproj -c Release
dotnet test --project build/_build.Tests/Build.Tests.csproj -c Release --framework net10.0    # plain — no --filter, no --nologo

# slopwatch
slopwatch analyze --fail-on warning --exclude "artifacts/**,external/**,vcpkg_installed/**,tools/**,**/bin/**,**/obj/**"

# Build-host targets (require resolved versions file)
dotnet run --project build/_build --no-build -c Release -- --target ResolveVersionsFromManifest --versions-file artifacts/resolve-versions/versions.json --suffix=local.smoke
dotnet run --project build/_build --no-build -c Release -- --target PreFlightCheck --versions-file artifacts/resolve-versions/versions.json

# tools.cs subcommands
dotnet run --file tools.cs -- build
dotnet run --file tools.cs -- setup
dotnet run --file tools.cs -- ci-sim
dotnet run --file tools.cs -- generate-bindings                       # binary cache hit ~5 min total (manifest-pivot smoke)
dotnet run --file tools.cs -- generate-bindings --rebuild-image       # force fresh image build (~10-15 min extra)
dotnet run --file tools.cs -- generate-bindings --cpus 6              # constrain container CPU

# Guards / verification post-Phase-2D
grep -rE "Sdl2CoreGenerationConfig|BindingPublicApiCoherenceValidator" build/_build build/_build.Tests   # should be 0 matches (legacy retired)
grep -n "schema_version" build/manifest.json                                  # should show "2.2"
grep -rE "_baseDefines|BaseAdditionalArguments" build/_build/Targets/GenerateBindings/Parsing/CppAstParseRunner.cs   # should be 0 matches (config-driven now)
grep -n "MapLibraryNameToFamilyId\|switch" build/_build/Data/BindingGeneration/BindingGenerationConfigRepository.cs   # should be 0 matches (PackageFamilies-driven now)
grep -n "Sdl2IncludeDirectory" build/_build build/_build.Tests   # should be 0 matches (FamilyIncludeDirectory renamed)
grep -n "ResolveSdl2CoreHeaders" build/_build build/_build.Tests   # should be 0 matches (Resolve(config, ...) renamed)

# Container smoke output inspection
ls artifacts/generated-bindings-preview/sdl2-core/Platform/   # should list 8 platform views
cat artifacts/generated-bindings-preview/sdl2-core/parse-views.json | python -c "import json, sys; d=json.load(sys.stdin); [print(f\"{v['Name']}: {v['FunctionCount']} fns\") for v in d['Views']]"
```

## Final Steering Note

This session corrected one major architectural mistake mid-flight: an early attempt at a hand-rolled `JsonDocument` walker for `BindingGenerationConfigRepository` (270 lines of fragile per-field error handling) was retracted after Deniz pointed out that the strongly-typed `ManifestConfig.LibraryManifests[].BindingGeneration` path already existed. The replacement is ~60 lines that consumes `IManifestRepository`. The lesson: **extend existing strongly-typed models, don't invent parallel parsers**. Apply this discipline to every future "feels like I need a parser" instinct.

The session also corrected a BuildKit cache mount misconception (target paths aren't image-layer-resident) and converged on a content-derived cache key + conditional `git clone` fallback that mirrors CI invalidation discipline 1:1.

The next agent should not optimize for "land Phase 3 fast." Optimize for:

- A clean Phase 2A+2B+2C+2D + Docker fix commit (one or two commits) that captures the manifest-pivot end state, **OR**
- Continued Phase 3 sub-slice progress (3A → 3B → 3C → 3D → 3E → 3F → 3G) with the Phase 3B Finding note's `RequiredConstants` design propagated forward.

After Phase 3 fully lands (all sub-slices), the queued work in order is Stage 1 Task 7 (csproj flag-flip), Stage 1 Task 8 (PreFlight stamp drift validator), Stage 1 Task 9 (public-API snapshot tests). Each warrants its own slice and its own handover prompt.

Hold the line on ADR-002 / ADR-003 boundaries, the manifest-as-single-source-of-truth principle, and the cache-key parity with CI. Skip micro-checkpoints, respect approval gates. Sequence over speed. When the plan and the code disagree, fix the plan — that's the lesson this session reinforced.
