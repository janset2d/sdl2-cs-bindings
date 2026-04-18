# Review Handover Prompt — 2026-04-18

Use this prompt to brief the next agent for a rigorous review of the current working tree in `janset2d/sdl2-cs-bindings`.

## Mission

Perform a hard review of the current uncommitted work with a code-review mindset.

Primary focus:

1. Validate that the recent Phase 2a / PA-1 / PA-2 work is technically sound.
2. Challenge behavioral assumptions, especially around harvest accumulation, hybrid-triplet expansion, smoke-test scope growth, and package-consumer validation.
3. Identify regressions, hidden coupling, portability risks, review gaps, and any place where the implementation quietly exceeds the design boundary.
4. Treat documentation accuracy as part of the review, not an optional follow-up.

Do not optimize for politeness over truth. Findings first.

## Required Review Mode

Use the repo's review convention:

- Findings must be the primary output.
- Order findings by severity.
- Cite concrete file locations.
- Focus on bugs, behavioral regressions, maintainability risks, missing tests, and design-boundary violations.
- Keep summary/overview secondary.

If no findings are discovered, say so explicitly and then call out residual risks and testing gaps.

## Golden Module / Structural Reference

The build-host reference standard is the Harvesting module.

Review all recent changes against these expectations:

- `BuildContext` stays at the task boundary.
- Thin tasks, narrow services, typed result boundaries.
- Explicit domain models over ad hoc plumbing.
- Task classes own orchestration and user-facing failure behavior.
- Service boundaries should not become vague just because the feature set grew.

If any recent code drifts away from that shape, call it out.

## Strategy Reality Check

The current repo strategy model matters to the review:

- Strategy-aware runtime behavior is still narrow.
- `Harvest` and `PreFlightCheck` are the main places where strategy coherence has teeth.
- Downstream pack/smoke paths are still mostly strategy-agnostic.
- PA-2 moved all 7 manifest runtime rows onto hybrid triplets.
- Behavioral validation is still deeper on the original proof slice than on the four newly-covered rows.

Do not give free credit for declarative coherence where behavioral validation is still thin.

## Review Scope

Pay special attention to these change areas:

1. `HarvestTask` behavior changed from whole-output cleaning to per-library/per-RID refresh.
2. `PackageConsumerSmokeRunner` expanded from `sdl2-core` + `sdl2-image` to the five-family concrete smoke scope.
3. Native smoke widened to cover all SDL2 satellites in scope and deeper decoder/render assertions.
4. Package-consumer smoke widened materially and now exercises more real runtime paths.
5. PA-1 and PA-2 docs/config/workflow updates changed the repo's declared strategy and CI model.
6. The SDL2-CS submodule must remain clean. Any solution that relies on a dirty submodule should be treated as a boundary failure.

## Change Rationale Record

This section exists so the next agent understands not just what changed, but why the changes were made.

### Harvest accumulation behavior

- The old `context.CleanDirectory(outputBase)` behavior in `HarvestTask` was removed because it destroyed previously harvested RID output before `ConsolidateHarvest` could merge it.
- The new behavior cleans only the active library/RID slice plus the library's refreshed license payload.
- This was done to make sequential local validation and CI-style per-RID accumulation compatible with the current RID-status design.

### Five-family package-consumer smoke scope

- `PackageConsumerSmokeRunner` was expanded from the older proof pair (`sdl2-core` + `sdl2-image`) to the concrete five-family consumer surface (`core`, `image`, `mixer`, `ttf`, `gfx`).
- This was done because Windows end-to-end validation had already outgrown the original proof pair, and the consumer path needed to exercise the actual package set now considered in scope.
- `sdl2-net` remains excluded from package-consumer smoke because it is still a manifest placeholder, not a concrete managed package path.

### Native smoke expansion

- Native smoke was widened to exercise all SDL2 satellites currently in native scope: image, mixer, ttf, gfx, and net, in addition to core.
- This was done to turn the smoke harness into a real runtime proof surface rather than a mostly loader-level sanity check.
- Decoder assertions and headless rendering checks were added because packaging/harvest correctness is not proven by a DLL loading alone.

### Submodule boundary correction

- A temporary local SDL2-CS patch was reverted because modifying the submodule worktree is the wrong ownership boundary for this repository.
- Upstream `master` is already at the same commit the submodule points to, so there was no newer upstream fix to adopt.
- Repo-local smoke code was adjusted instead so the validation path no longer depends on those broken upstream wrapper entrypoints.

### Documentation updates

- Docs were updated because the repo's declared state had drifted behind the actual implementation and validation surface.
- The intent was to keep canonical docs honest about what is truly green, what is only declaratively coherent, and which host slices are still thinly validated.

## Important Boundary Facts

- `external/sdl2-cs` is a submodule, not an owned code area.
- The submodule is intentionally transitional and untrusted long-term.
- Do not normalize local edits inside the submodule as an acceptable repo pattern.
- The correct bar is: either upstream update, or repo-local code that avoids depending on broken upstream surface.

## What Changed Most Recently

### 1. HarvestTask cleanup semantics

`HarvestTask` no longer does a destructive clean of the entire harvest output root before running.

Instead it now:

- ensures the harvest root exists,
- then cleans only the current library + current RID payload,
- deletes the current RID status file,
- refreshes the current library license payload.

### Why HarvestTask changed

Because the old global clean was incompatible with the RID-status / `ConsolidateHarvest` model. Sequential or matrix-style harvest runs would wipe previously collected RID outputs before consolidation. The new behavior preserves already harvested RIDs while still refreshing the active slice.

### What to scrutinize in HarvestTask

- Did the fix preserve necessary invariants while introducing stale-data risk elsewhere?
- Is license cleanup at library scope the correct granularity?
- Should there be a separate explicit clean task/flag instead of changing Harvest semantics globally?
- Does the current regression test sufficiently prove the intended behavior?

### 2. SDL2-CS boundary correction

An earlier validation pass temporarily patched two SDL2-CS wrapper entrypoints locally, which made the submodule dirty. That was reverted.

Current rule:

- keep the submodule clean,
- do not depend on carrying local edits in `external/sdl2-cs`,
- avoid broken upstream wrapper APIs from repo-local smoke/tests instead.

### Why SDL2-CS boundary changed

Because keeping local edits inside the submodule is the wrong ownership boundary, even if the upstream wrapper has bugs.

### What to scrutinize in SDL2-CS boundary

- Did the repo-local fallback weaken the validation surface too much?
- Is the current smoke scope still strong enough without those wrapper calls?
- Should the repo document the exact SDL2-CS upstream limitation more explicitly?

## Files Worth Reviewing First

- `build/_build/Tasks/Harvest/HarvestTask.cs`
- `build/_build.Tests/Unit/Tasks/Harvest/HarvestTaskTests.cs`
- `build/_build/Modules/Packaging/PackageConsumerSmokeRunner.cs`
- `tests/smoke-tests/native-smoke/main.c`
- `tests/smoke-tests/native-smoke/CMakeLists.txt`
- `tests/smoke-tests/package-smoke/PackageConsumer.Smoke/PackageSmokeTests.cs`
- `tests/smoke-tests/package-smoke/PackageConsumer.Smoke/PackageConsumer.Smoke.csproj`
- `tests/smoke-tests/package-smoke/Compile.NetStandard/Compile.NetStandard.csproj`
- `tests/smoke-tests/package-smoke/Compile.NetStandard/Probe.cs`
- `build/manifest.json`
- `.github/actions/vcpkg-setup/action.yml`
- `.github/workflows/prepare-native-assets-main.yml`
- `docs/plan.md`
- `docs/onboarding.md`
- `docs/phases/phase-2-adaptation-plan.md`
- `docs/playbook/cross-platform-smoke-validation.md`

## Current Validated Facts

- `win-x64` widened native-smoke reached `28 passed, 0 failed`.
- Windows `PostFlight` succeeded for `sdl2-core`, `sdl2-image`, `sdl2-mixer`, `sdl2-ttf`, and `sdl2-gfx` at `1.3.0-validation.win64.1`.
- `PackageConsumerSmokeRunner` now shuts down build servers before each executable TFM to stabilize the Windows multi-TFM sequence.
- Local manifest-mode `vcpkg_installed/` should be treated as effectively last-triplet-wins for this workflow.
- The SDL2-CS submodule is now back to a clean worktree and already points at upstream `master` head.
- After reverting the dirty SDL2-CS edits, the repo-local package smoke fallback still passes on Windows: `dotnet test ... -f net8.0` succeeded with `12/12` tests against `1.3.0-validation.win64.1` from the local package feed.

Do not confuse these validated facts with broader matrix closure. Linux/macOS widened-scope revalidation and the newly-covered four hybrid rows remain thinner.

## Expected Output Format

Return:

1. Findings first, highest severity first.
2. Each finding should include:
   - problem statement,
   - file reference,
   - why it matters,
   - recommended option first,
   - 2-3 options when the tradeoff is non-trivial.
3. Then list open questions / assumptions.
4. Only then include a short change-summary.

## Explicit Challenges For The Reviewer

Please actively test these assumptions instead of accepting them:

- The new Harvest semantics do not create stale consolidated manifests.
- The five-family smoke expansion is not overfitted to Windows-only behavior.
- The docs accurately distinguish declarative strategy alignment from behavioral proof.
- The widened smoke surfaces are meaningful and not just more assertions.
- No recent change quietly relied on repo-local environment quirks.
- The current implementation still respects the Harvesting module as the structural north star.

## Do Not Waste Time On

- Re-arguing whether the repo should keep SDL2-CS forever. That decision is already locked: it is transitional.
- Demanding broad unrelated refactors outside the touched surfaces unless they create an immediate correctness or maintenance risk.
- Treating unvalidated future rows as already broken without evidence; distinguish missing coverage from demonstrated failure.

## Final Reminder

This is a review prompt, not an implementation prompt. Be adversarial in the useful engineering sense: rigorous, evidence-based, and specific.
