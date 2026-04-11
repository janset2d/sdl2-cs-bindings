# Phase 2: CI/CD & Packaging

**Status**: IN PROGRESS (resumed 2026-04-11 after ~10 month hiatus)
**Started**: May 2025 (initial CI work) → Paused June 2025 → Resumed April 2026

## Objective

Complete the end-to-end pipeline from source code to publishable NuGet packages. Make the project buildable and testable by contributors.

## Scope

### 2.1 Complete vcpkg.json

**Priority: HIGH — Blocks everything else for Mixer/TTF/Net**

Current state: Only `sdl2` and `sdl2-image` are declared. Need to add:

```
sdl2-mixer   → features: mpg123, libflac, opusfile, libmodplug, wavpack, fluidsynth
sdl2-ttf     → features: harfbuzz
sdl2-gfx     → (no features)
sdl2-net     → (no features)
```

Also update vcpkg overrides for each library to pin versions matching `manifest.json`.

### 2.2 Update vcpkg Baseline

**Priority: HIGH — SDL2 is 3 patches behind**

Current baseline: `41c447cc...` (SDL2 2.32.4)
Target: Latest baseline (SDL2 2.32.10)

Steps:
1. Update `external/vcpkg` submodule to latest
2. Update `vcpkg.json` `builtin-baseline` to new commit hash
3. Update `manifest.json` versions to match
4. Run harvest on at least one RID to verify nothing broke

See [playbook/vcpkg-update.md](../playbook/vcpkg-update.md) for step-by-step recipe.

### 2.3 Implement Cake PackageTask

**Priority: HIGH — Core missing piece**

The harvest pipeline produces organized artifacts in `artifacts/harvest_output/`. What's missing is the step that turns these into `.nupkg` files.

Requirements:
- Read `harvest-manifest.json` to know which RIDs succeeded
- Copy harvested binaries into the correct `src/native/{Library}.Native/runtimes/{rid}/native/` layout
- Run `dotnet pack` on each `.Native` project
- Run `dotnet pack` on each binding project
- Output `.nupkg` + `.snupkg` files to `artifacts/packages/`

### 2.4 Make Release Candidate Pipeline Functional

**Priority: MEDIUM — Can use manual workflow initially**

Current `release-candidate-pipeline.yml` is a stub. Need to implement:
1. Pre-flight check job (version validation) — ✅ Already works
2. Build matrix job (call platform workflows + harvest) — Partially done
3. Consolidate harvest artifacts job — Needs implementation
4. Package and publish job — Needs PackageTask first

### 2.5 Clean Up Native Binaries from Git

**Priority: MEDIUM — Affects repo performance**

~50+ binary files are tracked in git under `src/native/*/runtimes/`. These were committed for testing but should be:
1. Added to `.gitignore`
2. Removed from tracking (`git rm --cached`)
3. Optionally cleaned from history (BFG or `git filter-repo`)

### 2.6 Local Development Playbook

**Priority: MEDIUM — Needed for contributors**

Document how to:
- Set up the project from scratch (clone, submodule init, .NET SDK, vcpkg bootstrap)
- Build managed bindings without native binaries
- Build native binaries locally for your platform
- Run the harvest pipeline locally
- Use CI artifacts for local development without building natives
- Test with sample projects

See [playbook/local-development.md](../playbook/local-development.md).

### 2.7 SDL2_net Binding Project

**Priority: LOW — Can be added quickly once vcpkg.json is complete**

- Add `external/sdl2-cs` doesn't include SDL2_net bindings (it's not part of flibitijibibo's project)
- Need to either find a community binding or write one (SDL2_net API is small)
- Create `src/SDL2.Net/` and `src/native/SDL2.Net.Native/`
- Add to solution and manifest.json

## Exit Criteria

- [ ] `vcpkg.json` declares all 6 SDL2 libraries with appropriate features
- [ ] vcpkg baseline updated to latest (SDL2 ≥ 2.32.10)
- [ ] Cake PackageTask produces valid .nupkg files from harvest output
- [ ] At least one full pipeline run: vcpkg build → harvest → consolidate → package
- [ ] Native binaries removed from git tracking
- [ ] `.gitignore` rules prevent re-committing binaries
- [ ] Local development playbook written and tested
- [ ] SDL2_net binding project created (even if native packaging is Phase 3)

## Dependencies

- Phase 1 (DONE) — All Phase 1 outputs are prerequisites
- No external blockers — all tools and infrastructure are available

## Risks

| Risk | Impact | Mitigation |
|------|--------|-----------|
| vcpkg baseline update breaks builds | HIGH | Test one triplet first, then matrix |
| SDL2_net has no C# bindings in SDL2-CS | LOW | API is small (~20 functions), can write manually or find community binding |
| PackageTask complexity | MEDIUM | Start with single-library manual test, then automate |
| Git history cleanup breaks forks | LOW | Repo has no known forks; announce before force-pushing |
