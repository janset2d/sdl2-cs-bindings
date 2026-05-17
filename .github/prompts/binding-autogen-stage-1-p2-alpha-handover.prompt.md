---
name: "Binding autogen Stage 1 — P2 design cleanup handover (post-PSTH-H/I/J)"
description: "Handover prompt for the next agent entering janset2d/sdl2-cs-bindings on branch spike/binding-autogen-sdl2-gfx after the PSTH-H/I/J + VcpkgManifest consolidation commit (9a5f59e) and partway through the P2-α design-cleanup sub-slice. Active dirty surface is P2-α (Sdl2CoreGenerationConfig field drop + PathService doc + plan retractions + Task 4 implementation seeds). Sub-slices P2-β/γ/δ/ε queued, plus Stage 1 Task 4/5/7/8, then Phase 2b. Working tree is dirty and not committed. Do not commit without Deniz approval."
argument-hint: "Optional override: jump to a specific P2 sub-slice (α/β/γ/δ/ε), pick a different track (Task 4 sprint, Phase 2b), or focus on the commit step for P2-α. Defaults to verifying P2-α dirty surface + surfacing the commit decision to Deniz, then deciding P2-β with Deniz."
agent: "agent"
model: "Claude Opus 4.7 (1M context)"
---

# Binding Autogen Stage 1 — P2 Design Cleanup Handover

You are entering `janset2d/sdl2-cs-bindings` on branch `spike/binding-autogen-sdl2-gfx` partway through the **P2 design-cleanup slice** that follows the Stage 1 Task 3.5 + Post-Implementation Review fixes (commit `0db0e31`) and the PSTH-H/I/J + VcpkgManifest consolidation slice (commit `9a5f59e`). The session that produced this handover:

1. Landed PSTH-H (G60 `OverlayPortVersionCoherenceValidator` for overlay-vs-upstream version drift) + mpg123 LGPL overlay retire + PSTH-I (vcpkg-setup multi-path cache extension for SDL2 buildtree source) + PSTH-J (new `.github/workflows/regenerate-bindings.yml` workflow + reusable `.github/workflows/build-cake-host.yml` extraction) + consolidated `VcpkgManifestRepository` to state-less, sync, per-call `Load(FilePath)` with a single `VcpkgManifest` model. **Committed as `9a5f59e`**.
2. Started the **P2 design-cleanup sub-slice α** (Task #19 from the carrying task list) — `Sdl2CoreGenerationConfig` dead-field removal + PathService SDL2-Core-only doc note + Stage 1 plan retraction for P2.3 / P2.4 / P2.5 / P2.6 / P2.13 + **forward-reference seeding** of the retired `OwnedPrefixes` / `DeferredDeclarations` literals into the Task 4 plan §Step 2 implementation snippets.

**Working tree is dirty. No commit has been made for P2-α.** Deniz explicitly held the commit decision after the previous session committed `9a5f59e` ahead of starting P2.

Your first job is **not** to barrel into P2-β or Stage 1 Task 4. Your first job is to verify the P2-α dirty surface still builds + tests pass, read the Stage 1 plan's P2 retraction notes, and surface the next decision (commit P2-α now or continue P2-β before committing) to Deniz.

## First Principle

> Treat this handover as current-as-of-authoring (2026-05-16). Verify against the live repo before acting. Run `git status --short`, inspect the dirty diff, and read the canonical docs listed below. Do not assume this prompt is more authoritative than the current code + docs. **Where this prompt and the live plan disagree, the live plan wins** — the plan was patched mid-session with forward-reference seeds; this prompt only summarises.

## Non-Negotiable Rules (AGENTS.md + accumulated feedback)

- **Approval gate:** no commit, no deployment, no publish, no CI apply without explicit Deniz approval ("go / apply / proceed / başla / yap"). The prior commit `9a5f59e` was approved explicitly with "Ok I committed the changes." Do not re-commit without a new approval.
- **No `--no-verify`, no `--no-gpg-sign`:** never skip git hooks or bypass signing.
- **Cake-native build host:** in `build/_build`, no raw `System.IO` at build boundaries. Use Cake `DirectoryPath` / `FilePath`, `ICakeContext`, `CakeFileSystemExtensions`, `CakeJsonExtensions`.
- **ADR-002 task-orchestrator pattern:** Cake tasks ARE the orchestrators. No `Runner` / `Pipeline` / `Operation` wrappers. Stage 1 Task 3.5's code review explicitly killed `IBindingGenerationRunner` — do not reintroduce it.
- **ADR-002 no IsDependentOn:** sequential dispatch lives at the caller boundary (`tools.cs setup` / `ci-sim` drive the Cake host sequentially; container-side, `docker/binding-generator-entrypoint.sh` runs the two targets back to back).
- **ADR-003 Data rule:** persisted / tool-read file contracts live under `build/_build/Data/<Cohort>/` with a repository abstraction + same-file interface. `Data/Manifest/VcpkgManifestRepository.cs` now exposes a single `Load(FilePath)` method serving both root and per-port manifests (no `VcpkgPortManifest`, no async variant — these were retired in `9a5f59e` after Deniz challenged the duplicate).
- **Validation/ rule:** cross-cutting validators live under `build/_build/Validation/<Family>/`. `OverlayPortVersionCoherenceValidator` (G60) sits at `Validation/Vcpkg/`.
- **Code comments self-contained:** no cross-doc references in code (`see docs/...`). Comments explain local logic; cross-doc references belong to commit messages / PR descriptions / ADRs. PSTH-F (still pending — see P2-β below) tracks the broader cleanup.
- **Rider for mass renames:** when you propose a rename that spans many files, draft the rename map and let Deniz execute via Rider. Do not script cross-file edits when a single Rider operation does it more reliably.
- **No timing pressure or motive assumptions:** don't push Deniz on "when?" / "ready?"; don't presume his decision criteria. He sets the pace.
- **No workarounds or shortcuts:** never hack code to make tests pass or errors disappear. Question spike-era workarounds codified into specs. Let libraries find their natural defaults.
- **No punting when solvable:** prefer "hand off to a fresh agent" over "park for later" when stuck.
- **Skip micro-checkpoints during execution:** batch routine task completions silently; stop only at significant boundaries.
- **Follow explicit paths:** when Deniz names a path/folder, use it exactly; never silently reroute to a discovered "better fit" — ask first.
- **Cross-OS container mount caveat:** never bind-mount the Windows-host repo into a Linux container running dotnet/vcpkg; the Stage 1 Dockerfile uses COPY-into-image + isolated cache volumes for exactly this reason.
- **Test V2 default:** when you touch a test, default to the V2 test infrastructure (broader than the AGENTS.md target-migration rule).
- **PowerShell on Windows host, zsh in WSL/macOS:** drive WSL non-interactively via `wsl -- zsh -lc`; macOS via `ssh ... 'zsh -lc'`. Bash doesn't load `DOTNET_ROOT`. The Bash tool inside this Claude session routes to Git Bash on Windows — use POSIX commands (`rm`, not `Remove-Item`).

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
10. `docs/binding-autogen/binding-autogen-strategy-brief.md` (§"vcpkg-state coherence guardrail" is the load-bearing context for `IGeneratedStampRepository` / `HeaderSetFingerprintCalculator` Task 8 surface — see "P2.4/P2.5 retraction" below)
11. `docs/superpowers/specs/2026-05-15-binding-generator-local-output-loop-design.md` (§"Out of scope" lists `BindingGenerationCoherenceValidator` as Task 8 deliverable)
12. `docs/superpowers/plans/2026-05-14-sdl2-core-binding-generator-stage-1.md` — primary plan; pay particular attention to:
    - **§"Post-Implementation Review Findings (2026-05-16)"** — P0/P1 landed in `0db0e31`, P2/P3 still pending. Inline status markers (✅ landed / ⚠ retracted / 🟡 partial / unmarked = pending) drive the sub-slice scope.
    - **§"Task 4: Binding Model, Merge Policy, and Fail-Closed Validators"** — implementation seeds for `CoreOwnedTypeMap.cs` and `KnownUnsupportedDeclarationPolicy.cs` were patched in mid-P2-α (2026-05-16); the previously-empty `CoreOwnedTypeMap.cs` slot now carries the full snippet, and `KnownUnsupportedDeclarationPolicy.CreateSdl2Core()` gained the SDL_SysWMinfo + SDL_SysWMmsg entries that were promised three times elsewhere in the plan but missing from the factory snippet.
    - **§"Pre-Stage-2 Transition Hardening"** — PSTH-A through PSTH-G inventory. PSTH-H/I/J added later as separate cep slices; H + I + J shipped in `9a5f59e`.
13. `docs/superpowers/plans/2026-05-15-binding-generator-local-output-loop.md` (Task 11.5 — AST inline filter + `-U__has_builtin` restore + dynapi cross-check; all shipped in `0db0e31`)
14. `docs/playbook/binding-generator-maintenance.md` (per-family maintenance, dynapi cross-check, parser-options audit, SyntheticHeaders, ExcludedHeaders)
15. `docs/parking-lot.md` index + `docs/parking-lot/validator-shape-standardization/README.md` — captures the `ValidationReport` vs custom `<Topic>Validation/Check/Status` split observed across 17 validators. New as of `9a5f59e`. Surfaces when P2-γ design-extraction work touches PreFlight reporter contract.
16. `docs/parking-lot/package-topology/` — pre-existing deferred 3-tier topology research; unchanged.

## Current Git / Working Tree Expectations

Expected branch:

```text
spike/binding-autogen-sdl2-gfx
```

Expected HEAD before this session's P2-α work:

```text
9a5f59e feat(binding-autogen): Stage 1 PSTH-H/I/J hardening + VcpkgManifest consolidation
0db0e31 feat(binding-autogen): Stage 1 Task 3.5 + Post-Implementation Review fixes
c00a2cb feat(binding-autogen): Stage 1 scaffold + Task 3.5 local-loop design
```

Verify with:

```pwsh
git status --short
git diff --stat
git log --oneline -5
```

Expected dirty surface (P2-α partial):

- `build/_build/Targets/GenerateBindings/Sdl2CoreGenerationConfig.cs` — dropped `OwnedPrefixes` + `DeferredDeclarations` record positional parameters + `Default` factory entries. The two surviving positional parameters (`ExcludedFunctionNames`, `RequiredFunctions`) carry the SDL_main / SDL_DYNAPI_entry / SDL.h-only-recovery work that landed in `0db0e31`.
- `build/_build.Tests/Unit/Targets/GenerateBindings/Sdl2CoreGenerationConfigTests.cs` — dropped the `OwnedPrefixes` assertion line from `Default_Should_Expose_Sdl2_Core_Identity` and the entire `Default_Should_Mark_SysWMinfo_Typed_Union_As_Deferred` test (1 test removed; suite count goes 704 → 703).
- `build/_build/Host/Paths/PathService.cs` — added `<para>` block on `IPathService.GetSdl2DynapiExportsGlob` explaining the `sdl2` segment is intentional (dynapi is SDL2-Core-only; SDL3 removed it; satellite ports have no dynapi manifest). Documenting, not parameterizing — this is P2.13's resolution.
- `build/_build/Targets/GenerateBindings/HeaderSet/HeaderSetResolver.cs` — added a header comment above `public sealed class HeaderSetResolver(...)` explaining why the type stays `public` (cascade-locked by the public Cake Frosting Task convention via `GenerateBindingsTask`'s ctor; CS0051 blocks the internal flip until 10 sibling tasks flip together). No visibility change.
- `build/_build/Targets/GenerateBindings/Parsing/CppAstParseRunner.cs` — same: explanatory comment above `ICppAstParseRunner` documenting the cascade-lock (the interface's method signature transitively forces `CppAstParseResult`, `ResolvedHeaderSet`, `PlatformParseView`, `PlatformConditionKind`, `ParseDiagnosticFormatter` to stay `public`). No visibility change.
- `docs/superpowers/plans/2026-05-14-sdl2-core-binding-generator-stage-1.md` — three plan edits:
  - **P2.3** marked ✅ landed with explicit literal-value seeding (`OwnedPrefixes` → `["SDL_", "SDLK_", "SDL_HINT_", "SDL_INIT_"]` → `Model/CoreOwnedTypeMap.cs::CreateSdl2Core()`; `DeferredDeclarations` → `["SDL_SysWMinfo", "SDL_SysWMmsg"]` → `Model/KnownUnsupportedDeclarationPolicy.cs::CreateSdl2Core()` with category `deferred-to-stage-2`).
  - **P2.4** marked ⚠ **retracted** — original "dead surface" framing was wrong. `IGeneratedStampRepository` is Task 8 `BindingGenerationCoherenceValidator` PreFlight stamp drift validator's consumer per strategy-brief §"vcpkg-state coherence guardrail" + spec §"Out of scope". **Keep as-is**, do not remove.
  - **P2.5** marked ⚠ **retracted** — same reason. `HeaderSetFingerprintCalculator` produces the `header_set_sha256` field of the Task 8 stamp.
  - **P2.6** marked 🟡 **partially un-actionable** — public Task convention cascade-lock; defer until a Task-visibility-convention slice flips all 10 sibling tasks to `internal sealed class` together.
  - **P2.13** marked ✅ landed (documented, not parameterized).
  - **Task 4 §Step 2** — `KnownUnsupportedDeclarationPolicy.CreateSdl2Core()` snippet now includes SDL_SysWMinfo + SDL_SysWMmsg entries (previously absent despite §"Component layout" + §2098 + §2127 promising coverage). New `CoreOwnedTypeMap.cs` implementation snippet inserted between the existing `KnownUnsupportedDeclarationPolicy` snippet and the merge-policy step — previously the file was named in §"Files" with no body.

## What Was Implemented Before This Handover (recap of `9a5f59e`)

`9a5f59e feat(binding-autogen): Stage 1 PSTH-H/I/J hardening + VcpkgManifest consolidation` covered:

### PSTH-H — G60 `OverlayPortVersionCoherenceValidator`

- New validator + 8 TUnit tests under `build/_build.Tests/Unit/Validation/Vcpkg/`.
- Cross-checks every overlay port's `vcpkg.json` version + port-version against `external/vcpkg/ports/<port>/vcpkg.json` at the current submodule pin.
- Wired into `PreFlightCheckTask` + `PreflightReporter`; G60 statuses are `Match` / `VersionDrift` / `OverlayManifestMissing` / `UpstreamPortMissing` / `InvalidJson`.
- `docs/knowledge-base/release-guardrails.md` §2.4 gained the G60 entry.

### mpg123 overlay retire (LGPL cleanup)

- Deleted `vcpkg-overlay-ports/mpg123/` (vcpkg.json + portfile.cmake + have-fpu.diff + pkgconfig.diff).
- `vcpkg-overlay-ports/README.md` row removed; playbooks (`adding-new-library.md`, `overlay-management.md`) updated.
- LGPL dependency had been removed from package surface earlier; overlay port had been stale baggage.

### PSTH-I — `.github/actions/vcpkg-setup/action.yml` multi-path cache extension

- `actions/cache@v5` now covers both binary cache path AND `external/vcpkg/buildtrees/sdl2/src` in one key.
- vcpkg's binary cache does not preserve source extraction; without this caching the dynapi manifest is unreachable on every cache-hit CI run.
- Restore-key fallback updated.

### PSTH-J — `.github/workflows/regenerate-bindings.yml` + reusable `.github/workflows/build-cake-host.yml`

- New `regenerate-bindings.yml` workflow (workflow_dispatch only) runs `GenerateBindings` on the pinned linux-builder container and uploads `generated-bindings-preview` artifact. Auto-PR via `peter-evans/create-pull-request` is deferred to Stage 1 Task 7 (committed emission location flag-flip).
- New `build-cake-host.yml` reusable workflow (workflow_call). `release.yml` + `regenerate-bindings.yml` both call it instead of duplicating ~50 lines of host build/test/publish.

### `VcpkgManifestRepository` consolidation

- Single `VcpkgManifest` record covers both root `vcpkg.json` (overrides[]) and port `vcpkg.json` (name + version + port-version); all fields optional. **`VcpkgPortManifest` record dropped.**
- Repository is state-less: ctor takes `ICakeContext` only; path supplied per call via `Load(FilePath)`. **Single sync method, no second async ToJson path.**
- DI factory replaced with plain singleton registration. `PreFlightCheckTask` passes `context.Paths.GetVcpkgManifestFile()`.
- `OverlayPortVersionCoherenceValidator` pre-checks `FileExists` itself (distinguishes `OverlayManifestMissing` vs `UpstreamPortMissing` from the path) and calls sync `Load()`; validator became sync `Validate()`, `PreFlightCheckTask.RunAsync` drops `async` and returns `Task.CompletedTask`.

### Parking-lot entry

- `docs/parking-lot/validator-shape-standardization/README.md` captures the `ValidationReport` (11 validators) vs custom-shape `<Topic>Validation/Check/Status` (6 validators incl. new G60) split, four migration options, and unpark triggers. `docs/parking-lot.md` index extended.

### Verification at commit time

- 704 TUnit tests pass / 0 failed / 2 skipped.
- `tools.cs ci-sim` 8/8 PASS, ~190s; PreFlight 3.4s with G60 active.
- Manual `NativeSmoke` 29/0, `PackageConsumerSmoke` 4 TFMs clean.

## P2 Sub-Slice Plan (Where We Are)

The Stage 1 plan §"Post-Implementation Review" carries the P2 inventory (P2.1–P2.18). The current session decomposed P2 into five sub-slices to avoid one mega-commit:

| Sub-slice | Scope | Risk | Status |
| --- | --- | --- | --- |
| **P2-α — Dead surface** | P2.3 (`OwnedPrefixes` / `DeferredDeclarations` retire), P2.6 (visibility rollback **deferred** — cascade-locked), P2.13 (hardcoded `"sdl2"` resolved via doc, not parameterization), plus plan retractions for P2.4 / P2.5 (lie correction — Task 8 forward-looking, **keep as-is**) | Low | **Dirty** — in progress, not committed |
| **P2-β — Stale comments (PSTH-F)** | P2.14 (`CppAstParseRunner.cs:81` lie), P2.15 (`GenerateBindingsTask.cs:17-21` entrypoint script comment lie), P2.16 (`ServiceCollectionExtensionsSmokeTests.cs:100-101` deleted `IBindingGenerationRunner` ref), P2.17 (`SyntheticHeaders/windows.h` + `Inspectable.h` `DeferredDeclarations` ghosts), P2.18 (`PreviewBindingModelData.cs:18,23` + `PreviewParseViewReportTests.cs:19-20` `SDL_main.h` mis-attribution) | Low | Pending |
| **P2-γ — Design extractions** | P2.1 (`BindingPublicApiCoherenceChecker` collaborator extract), P2.7 (`TypeMappingPolicy` extract), P2.8 (typedef recursion depth guard), P2.9 (`SafeIdentifier` Roslyn `SyntaxFacts.GetKeywordKind` keyword list) | Medium | Pending |
| **P2-δ — Cake-native sweep** | P2.2 (`Sdl2CoreGenerationConfig` DI singleton injection — `Default` static usage couples task to global), P2.10 (raw `Environment.GetEnvironmentVariable("CONTAINER_DIGEST")` → `ICakeEnvironment.GetEnvironmentVariable` or `BuildContext`), P2.11 (`GetSdl2DynapiExportsGlob` returns raw `string` → typed `FilePath`/`FilePathCollection` or documented intentional string), P2.12 (cosmetic multi-segment `.Combine("a/b/c")`) | Low | Pending |
| **P2-ε — PSTH-A naming purge** | Mass rename: `CppAstToPreviewModel` → `CppAstToBindingModel`, `PreviewBindingModelData` → `BindingModelData`, `Sdl2Preview_<view>` → `Sdl2Bindings_<view>`, `Janset.Sdl2.Preview` → `Janset.Sdl2`, "preview placeholder" `.g.cs` comment strip, `unsafe partial` drop where unused (overlaps P3.10). **Driven by Rider mass rename — Claude prepares rename map, Deniz executes in Rider, Claude validates.** | Medium-high | Pending — sequence last in P2 because Stage 1 Task 4 will retire `Preview*` naturally as the real model lands |

Recommended order: **α → β → δ → γ → ε**. α (current) is dirty. Discuss commit/continue with Deniz before moving on.

## Forward-Reference Seeds Patched Into The Plan (Critical for Task 4 Continuity)

Two retired literals from `Sdl2CoreGenerationConfig` had no destination snippet in the Stage 1 plan when P2-α removed them. The plan was patched mid-P2-α to seed them so the Task 4 agent doesn't have to mine `git log`:

- **`OwnedPrefixes = ["SDL_", "SDLK_", "SDL_HINT_", "SDL_INIT_"]`** → moves to `build/_build/Targets/GenerateBindings/Model/CoreOwnedTypeMap.cs::CreateSdl2Core()`. Plan §Task 4 §Step 2 now carries the full `CoreOwnedTypeMap.cs` implementation snippet (factory + `IsOwned(identifier)` method, ordinal comparison, comments explaining each prefix's role: `SDL_` for general API, `SDLK_` for keycode constants, `SDL_HINT_` for hint-name constants, `SDL_INIT_` for init-flag constants).
- **`DeferredDeclarations = ["SDL_SysWMinfo", "SDL_SysWMmsg"]`** → moves to `build/_build/Targets/GenerateBindings/Model/KnownUnsupportedDeclarationPolicy.cs::CreateSdl2Core()` with category `deferred-to-stage-2`. The factory snippet at plan §Task 4 §Step 2 previously only listed C-variadic functions; both SysWM entries now appear with the reason string `"deferred-to-stage-2: SDL_syswm typed-union layout requires the platform-handle forward-declaration stub library (HWND/HDC/Display*/Window/etc.) and `[StructLayout(LayoutKind.Explicit, Size = 64)]` emission. Stage 1 emits SDL_GetWindowWMInfo with an opaque SDL_SysWMinfo* parameter; the typed union lands in Stage 2."`.

If you find yourself in Task 4 wondering "where did the owned-prefix list come from?" — the answer is in the plan, not the git diff. Don't re-discover by archaeology.

## P2.4 / P2.5 Lie Correction (Don't Re-Delete)

The Stage 1 plan's original P2 inventory labelled `IGeneratedStampRepository` (`build/_build/Data/BindingGeneration/GeneratedStampRepository.cs`) and `HeaderSetFingerprintCalculator` as "forward-looking dead surface today" with the suggestion "Either wire stamp save/load into `GenerateBindingsTask` now or remove until Task 8 lands."

**This framing was wrong.** Both surfaces are **load-bearing forward infrastructure for Stage 1 Task 8** (`BindingGenerationCoherenceValidator` PreFlight stamp drift validator), as established in two canonical docs:

- `docs/binding-autogen/binding-autogen-strategy-brief.md` §"vcpkg-state coherence guardrail" — describes the `.generated-stamp` per-family contract, the PreFlight drift detection workflow, and the `header_set_sha256` field that `HeaderSetFingerprintCalculator` produces.
- `docs/superpowers/specs/2026-05-15-binding-generator-local-output-loop-design.md` §"Out of scope" — explicitly lists `BindingGenerationCoherenceValidator` PreFlight stamp drift validator as a Task 8 deliverable.

Removing the surface now would force re-adding the exact same code when Task 8 lands. The P2-α plan retraction marks both rows ⚠ **retracted** with a `Keep as-is` directive. If a future agent encounters these in another "dead surface" pass, do not remove them; just point at the retraction notes.

## P2.6 Cascade Lock (Why Visibility Rollback Was Deferred)

P2.6 in the original review listed eight types to flip from `public` → `internal`:

- `HeaderSetResolver`
- `CppAstParseResult`
- `CppAstParseRunner` + `ICppAstParseRunner`
- `ParseDiagnosticFormatter`
- `PlatformParseView`
- `PlatformConditionKind`
- `Sdl2CoreGenerationConfig`
- `ResolvedHeaderSet`

The reasoning was "the Build project grants `InternalsVisibleTo Build.Tests`, public surface for a build-host executable is unjustified."

The flip turned out to be **cascade-locked** by the Cake Frosting Task convention: `GenerateBindingsTask` is `public sealed class` like all 10 sibling tasks (`HarvestTask`, `PackageTask`, `ConsolidateHarvestTask`, `EnsureVcpkgDependenciesTask`, `GenerateMatrixTask`, `InfoTask`, `InspectHarvestedDependenciesTask`, `NativeSmokeTask`, `DumpbinDependentsTask`, `LddDependentsTask`). The public Task ctor takes `HeaderSetResolver` + `ICppAstParseRunner` (CS0051 if those are internal). The interface's method signature transitively forces `CppAstParseResult` / `ResolvedHeaderSet` / `PlatformParseView` (with its `PlatformConditionKind Kind` member) public. The class's own ctor takes `ParseDiagnosticFormatter`. Net: only `Sdl2CoreGenerationConfig` was already internal; no other type in the list stays off the public signature graph.

**Resolution captured in P2-α**: the two anchor types (`HeaderSetResolver`, `ICppAstParseRunner`) now carry header comments explaining the cascade-lock so a future reader doesn't re-attempt the rollback piecemeal. The full P2.6 only becomes actionable as part of a **Task-visibility-convention slice** that flips all 10 sibling tasks to `internal sealed class` together (and verifies Cake Frosting still discovers them via reflection — this needs empirical confirmation, not assumption).

If Deniz prioritizes that convention slice, do it as its own commit, not folded into P2.

## Verification Status For P2-α Dirty Surface

The following passed at last check:

```pwsh
dotnet build build/_build.Tests/Build.Tests.csproj -v minimal    # 0 warning, 0 error
dotnet test --project build/_build.Tests/Build.Tests.csproj      # 703/0/2 (was 704 before P2-α; 1 test deleted)
```

`slopwatch` finds 31 issues, **all pre-existing** in `artifacts/package-consumer-smoke/packages-cache/**` (extracted third-party TUnit targets — they bleed into the scan because the consumer smoke pre-staged the cache from the last `ci-sim` run), `external/vcpkg/buildtrees/**` (vcpkg's own zlib nuget contrib artifacts), and `tools/binding-spike/**` (parked spike). Zero findings in production code. The slopwatch exclude config covers `vcpkg_installed/**` + `external/**` + `**/bin/**` + `**/obj/**` but `artifacts/**` only catches generic artifacts — the consumer-smoke packages cache subdirectory leaks through. Not from P2-α.

`tools.cs ci-sim` was not re-run for P2-α because the slice changes are pure record-field removal + doc text — no runtime behavior change. The full chain (`ci-sim` → `setup` → manual `NativeSmoke` → `PackageConsumerSmoke`) was last validated at the `9a5f59e` commit boundary.

## Recommended Next Step

**Don't sprint into P2-β.** The next decision is whether to:

1. **Commit P2-α as its own slice now.** Single focused commit covering the dead-field removal + visibility-cascade explanation + PathService doc + plan retractions + Task 4 implementation seeds. Deniz approves the commit message.
2. **Continue with P2-β (stale comments) and bundle α + β into one commit.** P2-β is pure doc cleanup (P2.14–P2.18) and shares the "P2 design cleanup" mental frame. Smaller commit count, more cohesive history entry.
3. **Continue with the full P2 sweep (α + β + δ + γ + ε) as one large slice.** Larger diff, harder to review, but eliminates the in-between commits. PSTH-A naming purge (ε) is the riskiest sub-piece because it's Rider-driven; mixing it with the rest is not recommended.

**Surface both options 1 and 2 to Deniz** (option 3 is generally too big — flag it but don't recommend it). Do not commit unilaterally.

The most likely answer based on prior session patterns: Deniz prefers cohesive commits and is comfortable batching α + β before a checkpoint. **Bias toward option 2** as the recommendation, but ask.

## Future Sequence Beyond P2

Once P2 lands (all five sub-slices), the queued work is:

| Track | Stage / Phase | Notes |
| --- | --- | --- |
| **P3 polish (#20)** | Stage 1 hardening | CRLF normalize, `tools.cs --cpus` validation, docker pull retry, `ReadLinuxBaseImage` exception narrow, `FrozenSet<string>` for hot-path collections, `WriteAllTextAsync` ct honor, `ExcludedHeaders` cruft, `unsafe partial` modifier drop (P3.10 overlaps P2-ε), scenario test orchestration coverage, etc. |
| **Stage 1 Task 4** | Real binding model + merge policy + fail-closed validators | Plan §Task 4 has implementation seeds (now self-consistent post-P2-α). `Preview*` shape retires; the new model lives at `Model/BindingModel.cs` + `Model/BindingDeclaration.cs` + `Model/BindingTypeRef.cs` + `Model/BindingParameter.cs`. `TypeMappingPolicy` + `KnownUnsupportedDeclarationPolicy` + `CoreOwnedTypeMap` + `DeclarationCollector` + `DeclarationMergePolicy` are the policy + collection collaborators. Task 11.5 prerequisites already landed (AST inline filter, `-U__has_builtin`, dynapi cross-check). |
| **Stage 1 Task 5** | Per-category emitters | `CsCommandEmitter`, `CsHandleEmitter`, `CsConstantEmitter`, `CsEnumEmitter`, `CsStructEmitter`, `CsCallbackEmitter`. `SDL_SysWMinfo` emitted as opaque blob (typed union deferred to Stage 2). |
| **Stage 1 Task 7** | Wire SDL2.Core away from `external/sdl2-cs` | Emission location flag-flip: `artifacts/generated-bindings-preview/sdl2-core/` → `src/SDL2.Core/Generated/`. Consumer csproj updates: drop `external/sdl2-cs/src/SDL2.cs` Compile Include, add `AllowUnsafeBlocks=true`. Once landed, `regenerate-bindings.yml` becomes auto-PR (per its top-of-file comment block — see `.github/workflows/regenerate-bindings.yml` lines 18-25). |
| **Stage 1 Task 8** | `BindingGenerationCoherenceValidator` PreFlight stamp drift validator | Consumer of `IGeneratedStampRepository` + `HeaderSetFingerprintCalculator` (the surfaces P2.4 / P2.5 retraction protected). Implements the strategy-brief §"vcpkg-state coherence guardrail" workflow: recompute expected `vcpkg_state` + `header_set_sha256` against the current state; fail PreFlight on drift. |
| **Phase 2b** | PD-7 nuget.org promotion + PD-8 release recovery playbook + pipeline scope-assumption gaps | Outside Stage 1 scope; covered in `docs/plan.md`. |

## Things To Be Careful About

- **Do not reintroduce `IBindingGenerationRunner` or any `Runner` wrapper.** ADR-002 task-orchestrator pattern is enforced.
- **Do not reintroduce `[IsDependentOn]` on any Cake task in this project.** Sequential dispatch is a caller-side concern.
- **Do not add a WebFetch path for the dynapi manifest.** The decision is single-path (vcpkg buildtree only).
- **Do not vendor `SDL2.exports` into the repo.** vcpkg-only.
- **Do not invent custom result shapes.** Use `Build.Results.Result<T,TError>` and `ValidationReport` where the existing pattern fits. For new validators that need per-check typed comparison rendering (like G60), Camp B custom shape is acceptable — see `docs/parking-lot/validator-shape-standardization/README.md` for the camp distinction.
- **Do not remove `IGeneratedStampRepository` or `HeaderSetFingerprintCalculator`.** They are Task 8 forward-looking. The plan P2.4 / P2.5 lie retraction documents this.
- **Do not flip individual types from `public` to `internal` in `Targets/GenerateBindings/`.** The cascade-lock is documented in `HeaderSetResolver.cs` + `CppAstParseRunner.cs` header comments. Flip the convention as a whole-Task-class slice or not at all.
- **Do not run `git commit` unless Deniz explicitly approves.**
- **Do not script mass renames.** PSTH-A / P2-ε is Rider-driven; prepare the rename map and let Deniz execute.
- **Do not "fix" code-side doc references casually.** PSTH-F / P2-β tracks the cleanup; doing it ad-hoc misses the full audit.
- **Do not silence pre-existing slopwatch findings.** They live in `artifacts/package-consumer-smoke/packages-cache/**` and `external/vcpkg/buildtrees/**` and `tools/binding-spike/**`. Not from this slice. Slopwatch exclude config improvement is a separate concern.
- **Do not use `dotnet test --filter` or `dotnet test --nologo`.** TUnit / Microsoft.Testing.Platform doesn't support `--filter`; `--nologo` confuses the MTP adapter and produces "Zero tests ran" with exit code 5. Plain `dotnet test --project build/_build.Tests/Build.Tests.csproj` works.
- **Do not use PowerShell syntax in the Bash tool.** This Claude session routes the Bash tool through Git Bash on Windows; `Remove-Item` and other PS cmdlets fail. Use POSIX (`rm`, `cp`, `ls`).
- **Do not delete untracked files with `git rm`.** `git rm` only works on tracked files. For untracked, plain `rm path` (the Bash tool will route through Git Bash).

## Useful Commands

```pwsh
# Repo state
git status --short
git diff --stat
git log --oneline -5

# Build + tests (Stage 1 acceptance gates — P2-α dirty surface)
dotnet build build/_build/Build.csproj -v minimal
dotnet build build/_build.Tests/Build.Tests.csproj -v minimal
dotnet test --project build/_build.Tests/Build.Tests.csproj    # plain — no --filter, no --nologo

# slopwatch (CLI in dotnet tools global)
slopwatch

# tools.cs subcommands
dotnet run tools.cs -- build
dotnet run tools.cs -- setup
dotnet run tools.cs -- ci-sim
dotnet run tools.cs -- generate-bindings                       # binary cache hit ~4 min total
dotnet run tools.cs -- generate-bindings --rebuild-image       # force fresh image build (~5-10 min extra)
dotnet run tools.cs -- generate-bindings --cpus 6              # constrain container CPU

# Guards / verification post-P2-α
grep -rE "OwnedPrefixes|DeferredDeclarations" build/_build build/_build.Tests             # should be 0 production matches
grep -n "public sealed class HeaderSetResolver" build/_build/Targets/GenerateBindings/HeaderSet/HeaderSetResolver.cs   # should match — public is intentional (cascade-lock comment above)
grep -rE "IGeneratedStampRepository|HeaderSetFingerprintCalculator" build/_build build/_build.Tests   # should still match — Task 8 forward-looking
grep -n "<para>" build/_build/Host/Paths/PathService.cs                                   # should include the SDL2-Core-only note on GetSdl2DynapiExportsGlob

# Plan retraction sanity (these lines should be present after P2-α)
grep -n "✅ Landed in P2-α slice" docs/superpowers/plans/2026-05-14-sdl2-core-binding-generator-stage-1.md
grep -n "Retracted (2026-05-16)" docs/superpowers/plans/2026-05-14-sdl2-core-binding-generator-stage-1.md
grep -n "Partially un-actionable" docs/superpowers/plans/2026-05-14-sdl2-core-binding-generator-stage-1.md

# Output inspection (from last 9a5f59e ci-sim, if still on disk)
ls artifacts/generated-bindings-preview/sdl2-core/Platform/ 2>/dev/null || echo "no generated output on disk"
```

## Final Steering Note

This session corrected the Stage 1 plan's P2 inventory — three of its "dead surface" / "rollback safe" claims turned out to be wrong (P2.4 / P2.5 are Task 8 forward-looking; P2.6 is cascade-locked by the public Cake Frosting Task convention). The plan retraction notes capture the corrections inline so a future agent doesn't re-discover by archaeology. The P2-α slice itself is **small and safe** — record-field removal + doc note + plan edits + Task 4 implementation seeds — but the planning context behind it represents the kind of debugging Stage 1's Post-Implementation Review pass surfaces.

The next agent should not optimize for "land Task 4 fast." Optimize for:

- A clean P2-α commit (or α + β bundle) that captures the dead-surface removal + plan corrections as one durable history entry, **OR**
- Continued P2 sub-slice progress (β → δ → γ → ε) with periodic commits at sub-slice boundaries.

After P2 fully lands, the queued work in order is P3 polish, then Stage 1 Tasks 4 / 5 / 7 / 8, then Phase 2b. Each warrants its own slice and its own handover prompt.

Hold the line on ADR-002 / ADR-003 boundaries. Skip micro-checkpoints, respect approval gates. Sequence over speed. When the plan and the code disagree, fix the plan — that's the lesson P2-α taught.
