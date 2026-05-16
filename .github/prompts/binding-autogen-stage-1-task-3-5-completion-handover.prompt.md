---
name: "Binding autogen Stage 1 Task 3.5 completion handover"
description: "Handover prompt for the next agent entering janset2d/sdl2-cs-bindings on branch spike/binding-autogen-sdl2-gfx after Stage 1 Task 3.5 (local output loop) implementation, code review fixes (runner-inline, sequential dispatch, dynapi vcpkg-only), and Stage 2 backlog documentation. Working tree is still dirty. No commit. Next: verify the final smoke (or re-run it), then either land the commit Deniz approves or hand off to the Pre-Stage-2 transition hardening slice (PSTH-A through PSTH-G in the Stage 1 plan)."
argument-hint: "Optional override: jump to a specific PSTH item, run a fresh smoke, or focus on the commit step. Defaults to verifying smoke output and surfacing the commit decision to Deniz."
agent: "agent"
model: "Claude Opus 4.7 (1M context)"
---

# Binding Autogen Stage 1 Task 3.5 Completion Handover

You are entering `janset2d/sdl2-cs-bindings` on branch `spike/binding-autogen-sdl2-gfx` after a long Stage 1 Task 3.5 implementation session. The session covered:

1. Initial container-side smoke implementation (per-header parse loop, SDL_DISABLE_*MMINTRIN_H defines, SyntheticHeaders, HeaderSetResolver exclusions).
2. Dynapi cross-check validator landing (parser + repository + validator + DI wire + unit tests).
3. Deniz code-review pass on the slice — four fixes applied: BindingGenerationRunner inlined into the Cake task, `[IsDependentOn]` removed and replaced with a bash entrypoint script for sequential dispatch, dynapi WebFetch fallback removed (vcpkg buildtree only), `.dockerignore` selective-allow for `external/vcpkg/buildtrees/sdl2/src/`.
4. Stage 2 transition hardening backlog (PSTH-A through PSTH-G) documented in the Stage 1 plan.

**Working tree is still dirty. No commit has been made.** Deniz explicitly said he will decide commit timing after he reviews the final smoke output.

Your first job is not to barrel into Stage 1 Task 4 or to commit. Your first job is to verify the final smoke produced clean output, then surface the commit decision to Deniz with the actual file list.

## First Principle

> Treat this handover as current-as-of-authoring (2026-05-16). Verify against the live repo before acting. Run `git status --short`, inspect the dirty diff, and read the canonical docs listed below. Do not assume this prompt is more authoritative than the current code + docs.

## Non-Negotiable Rules

- **Approval gate:** no commit, no deployment, no publish, no CI apply without explicit Deniz approval ("go / apply / proceed / başla / yap").
- **Cake-native build host:** in `build/_build`, no raw `System.IO` at build boundaries. Use Cake `DirectoryPath` / `FilePath`, `ICakeContext`, `CakeFileSystemExtensions`, `CakeJsonExtensions`.
- **ADR-002 task-orchestrator pattern:** Cake tasks ARE the orchestrators. No `Runner` / `Pipeline` / `Operation` wrappers. The Stage 1 Task 3.5 code review explicitly killed an `IBindingGenerationRunner` abstraction for this reason — do not reintroduce it.
- **ADR-002 no IsDependentOn:** Cake task graphs are not used in this project. Sequential dispatch lives at the caller boundary (tools.cs setup / ci-sim drive Cake host sequentially; container-side, `docker/binding-generator-entrypoint.sh` runs the two targets back to back).
- **ADR-003 Data rule:** persisted / tool-read file contracts live under `build/_build/Data/<Contract>/` with a repository abstraction. `DynapiManifest` + parser + repository follow this pattern.
- **Validation/ rule:** cross-cutting validators live under `build/_build/Validation/<Family>/` (`BindingPublicApiCoherenceValidator` follows the same root-level placement as `HybridStaticOverlayValidator`).
- **Dynapi manifest source is vcpkg-only:** no WebFetch fallback, no vendored copy. The container Layer C COPY brings in `external/vcpkg/buildtrees/sdl2/src/` via `.dockerignore` selective-allow; `DynapiManifestRepository` reads from there. Host prerequisite: `tools.cs setup` (or any `vcpkg install sdl2` flow) must have populated the buildtree at least once.
- **No standalone generator project:** no `src/Janset.SDL2.Bindings.Generator/`, no new test project. The generator lives in the Cake build host.
- **Production generation is Linux-canonical:** no Windows/macOS production path. Local generation also runs in the Linux container via `tools.cs generate-bindings`.
- **No `[IsDependentOn]` on `GenerateBindingsTask`:** prerequisite (`EnsureVcpkgDependencies`) is dispatched by the container entrypoint script, not by the Cake graph.
- **Code comments self-contained:** no cross-doc references in code (`see docs/...`). Comments explain local logic; cross-doc references belong to commit messages / PR descriptions / ADRs. Some files still violate this rule — PSTH-F tracks the cleanup.

## Mandatory Grounding

Read in this order before making further changes:

1. `docs/onboarding.md`
2. `AGENTS.md`
3. `docs/plan.md`
4. `docs/decisions/2026-05-05-target-centric-build-host.md` (ADR-002)
5. `docs/decisions/2026-05-12-build-host-data-layer.md` (ADR-003)
6. `docs/decisions/2026-05-14-binding-autogen-toolchain.md` (ADR-004 trio pin)
7. `docs/knowledge-base/extraction-guidelines.md`
8. `docs/knowledge-base/testing-guidelines.md`
9. `docs/binding-autogen/binding-autogen-strategy-brief.md` (post-2026-05-16 includes Error 5 retraction notes)
10. `docs/binding-autogen/research/binding-autogen-spike-findings.md` §11 (Stage 1 Task 3.5 production findings — frictions 7-15, dynapi cross-check, retractions, output snapshot)
11. `docs/superpowers/specs/2026-05-15-binding-generator-local-output-loop-design.md` (post-2026-05-16 includes §8.7 parse-time config surface, §10.3 CPU allocation deferral)
12. `docs/superpowers/plans/2026-05-14-sdl2-core-binding-generator-stage-1.md` §"Pre-Stage-2 Transition Hardening" (PSTH-A through PSTH-G)
13. `docs/superpowers/plans/2026-05-15-binding-generator-local-output-loop.md` §"Task 11.5" (AST inline filter + `-U__has_builtin` + dynapi cross-check implementation)
14. `docs/playbook/binding-generator-maintenance.md` (4 new maintenance sections post-Task 3.5: SyntheticHeaders, ExcludedHeaders, Parser Options Audit, Dynapi Manifest Cross-Check)

## Current Git / Working Tree Expectations

Expected branch:

```text
spike/binding-autogen-sdl2-gfx
```

Expected HEAD before this session's dirty work:

```text
c00a2cb feat(binding-autogen): Stage 1 scaffold + Task 3.5 local-loop design
1c0b6e6 docs(binding-autogen): sync downstream docs + research retraction banners for 2026-05-15 revisions
c921e44 docs(binding-autogen): fold generator into Cake host, lock Linux-canonical, retract stub speculation
```

Verify with:

```pwsh
git status --short
git diff --stat
```

Expected dirty surface roughly (verify, do not trust this list literally):

- All Stage 1 Task 3.5 implementation files under `build/_build/Targets/GenerateBindings/`
- New `build/_build/Data/BindingGeneration/DynapiManifest*.cs` + `IDynapiManifestRepository.cs`
- New `build/_build/Validation/BindingGeneration/BindingPublicApiCoherenceValidator.cs` + `SeverityProfile.cs`
- New `build/_build/Targets/GenerateBindings/SyntheticHeaders/` (8 platform-stub headers + README)
- New `build/_build/Targets/GenerateBindings/Parsing/LibclangVersionAsserter.cs`
- `docker/binding-generator.Dockerfile` + new `docker/binding-generator-entrypoint.sh`
- `.dockerignore` (selective allow for `external/vcpkg/buildtrees/sdl2/src/`)
- `tools.cs` (simplified — entrypoint script handles dispatch)
- Test files under `build/_build.Tests/Unit/Data/BindingGeneration/`, `build/_build.Tests/Unit/Validation/BindingGeneration/`, `build/_build.Tests/Scenarios/GenerateBindings/`
- Docs: `docs/superpowers/specs/2026-05-15-binding-generator-local-output-loop-design.md` (§10.3 CPU + §8.7), `docs/superpowers/plans/2026-05-15-binding-generator-local-output-loop.md` (Task 11.5), `docs/superpowers/plans/2026-05-14-sdl2-core-binding-generator-stage-1.md` (PSTH section), `docs/binding-autogen/binding-autogen-strategy-brief.md` (Error 5 + §"Multi-pass parsing" refinements), `docs/binding-autogen/research/binding-autogen-spike-findings.md` (§11), `docs/playbook/binding-generator-maintenance.md`

## What Was Implemented (Task 11.5)

### Subtask 11.5.A — Translator AST inline filter

`CppAstToPreviewModel.cs` gained a `IsBindableSdl2Export` filter that skips functions with `CppFunctionFlags.Inline` set. Mirrors Silk.NET's `IsInlined` skip pattern. Eliminated 16 SDL_FORCE_INLINE leaks (verified in container log: Neutral 848 → 832).

### Subtask 11.5.B — `-U__has_builtin` restore with correct comment

`CppAstParseRunner.cs` `BaseAdditionalArguments` re-includes `-U__has_builtin` after it was misclassified as "redundant" mid-iteration. The comment block now documents the real mechanism: `SDL_stdinc.h:127-131` defines `_SDL_HAS_BUILTIN(x)` via `#ifdef __has_builtin`; undefining the macro short-circuits the SDL_FORCE_INLINE overflow-builtin helpers at parse time. Side-effect audit confirmed zero public-API declaration impact.

### Subtask 11.5.C — Dynapi cross-check validator

Five files implementing the post-emit validator pipeline:

- `Data/BindingGeneration/DynapiManifest.cs` — pure record carrying `IReadOnlySet<string> PublicSymbols` + `DynapiManifestOrigin` + `SourcePath`.
- `Data/BindingGeneration/DynapiManifestParser.cs` — pure parser, regex-driven (`^\+\+'_(\w+)'\.'SDL2\.dll'\.'(\w+)'$`), comment-line skip, malformed-line fail-closed.
- `Data/BindingGeneration/IDynapiManifestRepository.cs` + `DynapiManifestRepository.cs` — Cake-aware, vcpkg-only (`external/vcpkg/buildtrees/sdl2/src/*/src/dynapi/SDL2.exports` glob), Result<T, ManifestResolutionError> return shape.
- `Validation/BindingGeneration/SeverityProfile.cs` — `Stage1Generator` (FP=Error, FN=Warning) + `Stage2Strict` (FP=Error, FN=Error).
- `Validation/BindingGeneration/BindingPublicApiCoherenceValidator.cs` — set difference both ways, `ValidationReport` output, per-severity-profile classification.

Wired into `GenerateBindingsTask.RunAsync` post-emit before file write; DI registered in `ServiceCollectionExtensions`.

Unit tests: `DynapiManifestParserTests` (7 cases — happy path, Watcom-conditional comments, blank/BOM, comment-only, malformed, mismatch, non-export). `BindingPublicApiCoherenceValidatorTests` (5 cases — match, FP under Stage 1, FN under Stage 1, both-fail under Stage 2 Strict, both-directions). Repository unit tests deferred (WebFetch fallback was removed; remaining buildtree path is integration-tested via the smoke).

### Code review fixes (Deniz, mid-session)

1. **`.dockerignore` impact** — `./docker` build context is governed by `docker/.dockerignore` (allow-only-Dockerfile), so the repo-root `.dockerignore` does not bleed into the linux-builder workflow. Verified no impact.
2. **BindingGenerationRunner inlined into task** — `IBindingGenerationRunner` interface and `BindingGenerationRunner` class deleted; body inlined into `GenerateBindingsTask.RunAsync`. `GenerateBindingsRequest` record also deleted (task reads context directly, matching `HarvestTask` / `PackageTask` patterns). Two new collaborator interfaces extracted for test seam: `ICppAstParseRunner` (parses are libclang-bound, can't run in Windows test process — mock via NSubstitute) and `ILibclangVersionAsserter` (`clang.getClangVersion` DllNotFoundException on Windows test process — mock via NSubstitute).
3. **`[IsDependentOn]` removed** — sequential dispatch moved to `docker/binding-generator-entrypoint.sh` which runs `EnsureVcpkgDependencies` then `GenerateBindings` inside the container. `tools.cs generate-bindings` no longer passes Cake target args; container ENTRYPOINT is the script.
4. **Dynapi vcpkg-only** — WebFetch fallback + `HttpClient` dependency removed; `Sdl2UpstreamVersion` field removed from `Sdl2CoreGenerationConfig` (was needed only for the WebFetch URL). `.dockerignore` selective-allow `!external/vcpkg/buildtrees/sdl2/src/` re-includes the ~80 MB SDL2 source tree into the container's Layer C COPY. `DynapiManifestRepository.LoadAsync` is single-path; failure produces an actionable error message asking the operator to run `tools.cs setup` on the host first.

### Stage 2 backlog documented (PSTH-A through PSTH-G)

The Stage 1 plan (`docs/superpowers/plans/2026-05-14-sdl2-core-binding-generator-stage-1.md`) now has a `## Pre-Stage-2 Transition Hardening` section listing seven items that must land before Stage 2's satellite sweep / SDL_syswm typed union / `external/sdl2-cs` retirement work begins:

- **PSTH-A**: Preview / Stage 1 naming purge (rename `PreviewBindingModel`, `PreviewParseView`, `PreviewEmitter`, `GenerateBindingsPreviewRoot`, etc. — these were transient scaffolding names that don't survive Stage 2).
- **PSTH-B**: Family-based binding generation architecture (`GenerateBindingsTask --family <name>`, per-family `Sdl2*GenerationConfig`, family-resolver service, mirrors `HarvestTask --library <name>` pattern).
- **PSTH-C**: Header set rules — per-family, not global (`HeaderSetResolver.ExcludedHeaders`'s satellite/scaffolding hardcoded list moves into family config).
- **PSTH-D**: Manifest centralization decision (move family configs to `build/manifest.json` `package_families[]` with schema bump v2.2 — hybrid recommendation: identity in manifest, logic in C# record constructed at task entry).
- **PSTH-E**: ExcludedHeaders configurable (natural consequence of C+D).
- **PSTH-F**: Code-side doc-reference temizliği (current Stage 1 Task 3.5 code violates AGENTS.md "comments self-contained" rule in several files).
- **PSTH-G**: AGENTS.md update post-Stage-2 (Phase 6 settled-decisions table row).

Sequencing notes are in the plan: A + F are pure cleanup, can land any time. B + C + D + E are coupled — recommended as a single family-architecture refactor slice between this plan and Task 4.

## What Was Corrected In Docs

- **`docs/binding-autogen/research/binding-autogen-spike-findings.md`**: Added §11 with nine new friction entries (per-header loop, SDL_DISABLE_*MMINTRIN_H, `-U__has_builtin` correct mechanism, CppAst `TargetSystem="windows"` default, NuGet libclang resource-dir absence, SyntheticHeaders, `-fdeclspec`, Apple version macros, AST inline filter). §11.2 documents the dynapi cross-check direction. §11.3 records three retractions (banner "no shims required" partial walk-back, mid-iteration "redundant `-U__has_builtin`" mistake, one-TU `ParseFiles` assumption). §11.4 output snapshot. §11.5 open items for Stage 2.
- **`docs/binding-autogen/binding-autogen-strategy-brief.md`**: §"Multi-pass parsing strategy" now documents the two-tier isolation (outer per-view + inner per-header), `SyntheticHeaders` permanence, `-fdeclspec` and `-U__has_builtin` with correct mechanism. §"Symbol-existence validation guardrail" gained a defense-in-depth note for the dynapi cross-check (Stage 1 + Stage 2 PreFlight + Stage 2 Pack three-stage deployment). Decision Audit gained Error 5 with three sub-corrections.
- **`docs/superpowers/specs/2026-05-15-binding-generator-local-output-loop-design.md`**: Banner now says "Implementation complete 2026-05-16". §5.2 documents the per-header inner loop. §8.7 lists the actual parser configuration surface (CppParserOptions, BaseDefines, BaseAdditionalArguments, per-view defines, SyntheticHeaders, header exclusion set). §10.3 added — container CPU allocation tuning deferral (operator observation: ~100% one container core vs ~3% host).
- **`docs/playbook/binding-generator-maintenance.md`**: Four new maintenance sections (Synthetic Headers, Header Set Resolver Exclusions, Parser Options Audit Cadence, Dynapi Manifest Cross-Check). Checklist gained four corresponding items.
- **`docs/superpowers/plans/2026-05-15-binding-generator-local-output-loop.md`**: Banner "Tasks 1-11 complete". Task 11 implementation result subsection (smoke evidence). Task 11.5 added (A: AST inline filter + B: `-U__has_builtin` restore + C: dynapi cross-check with locked design decisions + D: determinism re-check). Open follow-up: container CPU tuning. `docs/parking-lot.md` updates retracted — binding-autogen-internal deferrals stay in superpowers docs.
- **`docs/superpowers/plans/2026-05-14-sdl2-core-binding-generator-stage-1.md`**: Task 3.5 update note. Task 4 prerequisite block (Task 11.5 deliverables + cross-stage validator integration + ADR-002 promotion criteria). Task 5 marshalling shape parity acceptance criterion. New "Pre-Stage-2 Transition Hardening" section with PSTH-A through PSTH-G.

## Verification Status

The following passed at last check (before the final smoke):

```pwsh
dotnet build build/_build/Build.csproj -c Release       # 0 warning, 0 error
dotnet test --project build/_build.Tests/Build.Tests.csproj -c Release --framework net10.0
# 671 succeeded, 2 skipped, 0 failed
slopwatch analyze --fail-on warning --exclude "artifacts/**,external/**,vcpkg_installed/**,tools/**,**/bin/**,**/obj/**"
# 0 issues
```

Final smoke (`tools.cs generate-bindings --rebuild-image`) was launched in-session but its completion notification may not have surfaced before this handover was written. **First action when picking up**: check if the smoke completed cleanly. Re-run if needed (~30 min wall time on first cold run; subsequent runs faster due to Docker layer cache + vcpkg binary cache volume).

Expected smoke outcome with all 4 code-review fixes + dynapi vcpkg-only:

- `EnsureVcpkgDependencies` completes ~4 seconds (binary cache hit, populates `installed/` from cached archive)
- `GenerateBindings` parses 8 views × ~54 headers × ~10s per TU
- Dynapi manifest resolves from `external/vcpkg/buildtrees/sdl2/src/se-2.32.10-*/src/dynapi/SDL2.exports` (now inside container via `.dockerignore` selective allow)
- Validator runs with `Stage1Generator` profile: zero false-positives (AST filter + `-U__has_builtin` removed all 16 inline leaks), some false-negative warnings (manifest entries SDL2-CS-like scoped-skip + Watcom-disabled platform-conditional symbols)
- 9 files written: `Platform/<8 views>/Commands.g.cs` + `parse-views.json`

If validator reports false-positives, the AST filter regressed or the manifest parsing has a bug — investigate before assuming the implementation is correct.

If smoke fails with `SDL2 dynapi manifest not found`: host's `external/vcpkg/buildtrees/sdl2/src/` is missing. Run `tools.cs setup --source=local` on the host first (this populates the vcpkg buildtree).

## Known Blockers — Fix Before Anything Else

The final smoke (2026-05-16, `--rebuild-image` run, ~33 min wall time) confirmed two distinct bugs in the validator path that cause `BindingPublicApiCoherenceValidator` to fail-closed with **28 false-positive errors**. Both are in the slice's own implementation, not in upstream / vcpkg / SDL2. The dynapi vcpkg-only path **itself works** (manifest read from `VcpkgBuildtree '/workspace/external/vcpkg/buildtrees/sdl2/src/se-2.32.10-3b143ac573.clean/src/dynapi/SDL2.exports'`) — these are downstream parsing / emission bugs:

### Blocker 1 — `DynapiManifestParser` rejects Watcom-disabled-but-still-exported symbols

**Symptom**: ~27 of the 28 false-positives are names like `SDL_WinRTGetDeviceFamily`, `SDL_WinRTGetFSPathUNICODE`, `SDL_WinRTGetFSPathUTF8`, `SDL_WinRTRunApp`, `SDL_iPhoneSetAnimationCallback`, `SDL_iPhoneSetEventPump`. The validator reports them as "binding emits P/Invoke for X but the dynapi manifest does not list X as a public export."

**Diagnosis** (verify with `grep "SDL_WinRTRunApp" external/vcpkg/buildtrees/sdl2/src/se-2.32.10-3b143ac573.clean/src/dynapi/SDL2.exports`):

```text
# ++'_SDL_WinRTRunApp'.'SDL2.dll'.'SDL_WinRTRunApp'
```

These entries are prefixed with `# ` — the Watcom-build-only build script disables them for the Watcom DEF-file path (Watcom is a legacy compiler nobody uses with modern SDL2). **The modern SDL2 shared library on Linux / macOS / Windows still exports these symbols** — the C dynapi function-pointer table at `SDL_dynapi_procs.h` lists them as real entries; only the Watcom-format `.exports` file disables them for that specific build path.

Our parser `DynapiManifestParser.cs` currently treats any line starting with `#` as a pure comment and skips it. That collapses both "real comments" (`# Windows exports file for Watcom`) and "Watcom-disabled-but-modern-exports" (`# ++'_SDL_WinRTRunApp'...`) into the same skipped bucket.

**Fix direction**: extend the parser to recognise two comment kinds:

- Pure comments (`# free text`, no `++` marker after the `#`) — skip.
- Watcom-disabled exports (`# ++'_Name'.'SDL2.dll'.'Name'`) — treat as **active** in the resulting `PublicSymbols` set.

Regex sketch: `^\s*(?:#\s*)?\+\+'_(?<name>\w+)'\.'SDL2\.dll'\.'(?<mirror>\w+)'\s*$` — same as today's pattern but with an optional `#\s*` prefix group. Or a two-pass approach: first scan for active `++` lines, then a second scan for `# ++` lines that get the same treatment.

Update `DynapiManifestParserTests` accordingly — add a case asserting `# ++'_SDL_iPhoneSetAnimationCallback'.'SDL2.dll'.'SDL_iPhoneSetAnimationCallback'` lands in `PublicSymbols`.

### Blocker 2 — `SDL_main` is application-defined, must not be emitted

**Symptom**: 1 of the 28 false-positives is `SDL_main`. Unlike the Blocker-1 names, `SDL_main` does NOT appear in the manifest at all (`grep "SDL_main\b" external/vcpkg/buildtrees/sdl2/src/.../src/dynapi/SDL2.exports` returns nothing).

**Diagnosis**: `SDL_main` is declared in `SDL_main.h` as `extern DECLSPEC int SDL_main(int argc, char *argv[]);` — but its body is **defined by the consuming application**, not by SDL2 itself. SDL2's `SDLmain.lib` (Windows) / `libSDL2main.a` (Unix) is a static-link wrapper that calls into the application's `SDL_main`. The runtime SDL2.dll / libSDL2-2.0.so does not export the symbol. A `[DllImport("SDL2", EntryPoint = "SDL_main")]` would always raise `EntryPointNotFoundException`.

The validator is correct to reject it. The bug is on the emit side — we should not be generating a P/Invoke for `SDL_main` in the first place.

**Fix direction**: two viable options. Pick one when implementing.

- **(a) Header-level exclude**: add `SDL_main.h` to `HeaderSetResolver.ExcludedHeaders`. Pro: simple, one-line change. Con: SDL_main.h also declares `SDL_SetMainReady`, `SDL_RegisterApp`, `SDL_UnregisterApp`, `SDL_RunApp`, `SDL_WinRTRunApp` (these are real exports; some are Blocker-1 candidates). Excluding the whole header loses those.
- **(b) Symbol-level exclude**: add an `ExcludedFunctionNames = ["SDL_main"]` collection to `Sdl2CoreGenerationConfig` and have `CppAstToPreviewModel.Translate` filter on it. Pro: surgical, keeps the rest of `SDL_main.h`. Con: tiny new translator code path.

Recommended: **(b)** — symbol-level exclude, because the other SDL_main.h symbols are real public API. The exclusion list is small (currently 1 entry); if it grows, that is a PSTH-D signal to migrate the list to `manifest.json`.

### Sequencing for Blocker fixes

1. Implement Blocker 1 (parser regex change + unit test addition). Verify with `dotnet test`.
2. Implement Blocker 2 (config + translator filter + unit test). Verify with `dotnet test`.
3. Re-run smoke (`tools.cs generate-bindings`, ~30 min — no `--rebuild-image` needed because the Dockerfile didn't change). Expect zero false-positives; some false-negative warnings (manifest entries the generator skipped because their headers are excluded — these are legitimate Stage 1 scoping).
4. Inspect `artifacts/generated-bindings-preview/sdl2-core/` to confirm output landed cleanly.
5. Then surface the commit decision to Deniz.

**Do not** proceed to PSTH-A through PSTH-G or to Task 4 until both blockers are resolved and a clean smoke run lands. The current dirty surface contains validator-failing code that is unsafe to commit as-is.

## Recommended Next Step

**Don't sprint into Task 4.** The next decision is whether to:

1. **Commit the current dirty surface as a single Stage 1 Task 3.5 landing.** Single combined commit (Tasks 1-11 + Task 11.5 + code review fixes + doc updates). Deniz must approve commit message.
2. **Start PSTH-A through PSTH-G first.** The current code has Stage-1-specific names + hardcoded sdl2-core scope that will collapse under Stage 2's multi-family generation. PSTH-A and PSTH-F are pure cleanup (low risk). PSTH-B + C + D + E are coupled and warrant a dedicated slice. Recommended single landing: a "family-architecture refactor" slice before any Task 4 work.

**Surface both options to Deniz with the commit-or-defer trade-off**. Do not commit unilaterally.

## Things To Be Careful About

- **Do not reintroduce `IBindingGenerationRunner` or any `Runner` wrapper.** ADR-002 task-orchestrator pattern is enforced; the slice was explicitly refactored to remove the abstraction.
- **Do not reintroduce `[IsDependentOn]` on any Cake task in this project.** Sequential dispatch is a caller-side concern.
- **Do not add a WebFetch path for the dynapi manifest.** The decision is single-path (vcpkg buildtree only); the operator pre-requisite is `tools.cs setup`.
- **Do not vendor `SDL2.exports` into the repo.** The vcpkg-only path is the agreed strategy.
- **Do not let plan snippets override ADR-002 / ADR-003.**
- **Do not invent custom result shapes.** Use `Build.Results.Result<T,TError>` and `ValidationReport`.
- **Do not run `git commit` unless Deniz explicitly approves.**
- **Do not silence the markdownlint warnings that pre-existed in the repo.** They were here before this slice; fixing them is out of scope unless explicitly requested.
- **Do not "fix" code-side doc references casually.** PSTH-F tracks the cleanup; doing it ad-hoc misses the full audit.

## Useful Commands

```pwsh
# Repo state
git status --short
git diff --stat
git log --oneline -5

# Build + tests + slopwatch (Stage 1 acceptance gates)
dotnet build build/_build/Build.csproj -c Release
dotnet test --project build/_build.Tests/Build.Tests.csproj -c Release --framework net10.0
slopwatch analyze --fail-on warning --exclude "artifacts/**,external/**,vcpkg_installed/**,tools/**,**/bin/**,**/obj/**"

# Final smoke
dotnet run --file tools.cs -- generate-bindings              # binary cache hit ~4 min total
dotnet run --file tools.cs -- generate-bindings --rebuild-image  # force fresh image build (~5-10 min extra)

# Output inspection
ls artifacts/generated-bindings-preview/sdl2-core/
ls artifacts/generated-bindings-preview/sdl2-core/Platform/
head -25 artifacts/generated-bindings-preview/sdl2-core/Platform/Neutral/Commands.g.cs
cat artifacts/generated-bindings-preview/sdl2-core/parse-views.json | head -30

# Guards / verification
grep -rE "IBindingGenerationRunner|BindingGenerationRunner" build/_build build/_build.Tests   # should be 0 matches
grep -rE "\[IsDependentOn" build/_build/Targets/GenerateBindings                              # should be 0 matches
grep -rE "WebFetch|HttpClient" build/_build/Data/BindingGeneration                            # should be 0 matches
```

## Final Steering Note

This session corrected a Stage 1 Task 3.5 implementation that started leaning toward Stage-1-only nomenclature and abstraction theatre (`Preview*`, `IBindingGenerationRunner`, runner-orchestrator dance). The code review pulled it back toward the rest of the build host's established discipline — direct task orchestration, family-aware naming, vcpkg-source-only dependency, sequential dispatch at the caller. The current state is **closer to production-shape than mid-session**, but still carries naming + hardcoding that PSTH-A through PSTH-G will purge.

The next agent should not optimize for "land Task 4 fast." Optimize for one of:

- a clean commit that captures Task 3.5 + code review + Stage 2 backlog docs as one durable history entry, OR
- a focused PSTH slice (B+C+D+E together, plus A+F cleanup) that lands the family-architecture refactor before Task 4 inherits the hardcoded sdl2-core assumptions.

Hold the line on ADR-002 / ADR-003 boundaries. Sequence over speed.
