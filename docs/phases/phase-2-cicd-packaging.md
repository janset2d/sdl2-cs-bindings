# Phase 2: CI/CD & Packaging

**Status**: IN PROGRESS (resumed 2026-04-11 after ~10 month hiatus)
**Started**: May 2025 (initial CI work) → Paused June 2025 → Resumed April 2026

## Objective

Complete the end-to-end pipeline from source code to publishable NuGet packages. Make the project buildable and testable by contributors.

## Scope

### 2.1 Complete vcpkg.json

#### Priority: HIGH — Coverage declared, now needs validation

Current working-tree state: all six SDL2 libraries are declared in `vcpkg.json` with overrides aligned to `manifest.json`.

Remaining validation work:

1. Verify `PreFlightCheck` passes on clean environments
2. Validate at least one full matrix run with the expanded library set
3. Confirm no regression in dependency closure and packaging inputs

### 2.2 Update vcpkg Baseline

#### Priority: HIGH — Bump applied, cross-platform validation pending

Current working-tree baseline: `0b88aacd...` (SDL2 2.32.10).

Validation steps:

1. Verify `manifest.json` and `vcpkg.json` stay version-consistent under `PreFlightCheck`
2. Run Harvest for all libraries on at least one RID per OS family
3. Confirm no new closure or deployment regressions

See [playbook/vcpkg-update.md](../playbook/vcpkg-update.md) for step-by-step recipe.

### 2.3 Implement Cake PackageTask

#### Priority: HIGH — Core missing piece

The harvest pipeline produces organized artifacts in `artifacts/harvest_output/`. What's missing is the step that turns these into `.nupkg` files.

Requirements:

- Read `harvest-manifest.json` to know which RIDs succeeded
- Copy harvested binaries into the correct `src/native/{Library}.Native/runtimes/{rid}/native/` layout
- Run `dotnet pack` on each `.Native` project
- Run `dotnet pack` on each binding project
- Output `.nupkg` + `.snupkg` files to `artifacts/packages/`

### 2.4 Make Release Candidate Pipeline Functional

#### Priority: MEDIUM — Can use manual workflow initially

Current `release-candidate-pipeline.yml` is a stub. Need to implement:

1. Pre-flight check integration in release-candidate workflow (Cake `PreFlightCheck` exists, workflow wiring still placeholder)
2. Build matrix job (call platform workflows + harvest) — Partially done
3. Consolidate harvest artifacts job — Needs implementation
4. Package and publish job — Needs PackageTask first

Workflow command baseline (updated 2026-04-12):

- `prepare-native-assets-windows.yml`, `prepare-native-assets-linux.yml`, and `prepare-native-assets-macos.yml` now use the same full SDL2 satellite harvest list (`SDL2`, `SDL2_image`, `SDL2_mixer`, `SDL2_ttf`, `SDL2_gfx`, `SDL2_net`)
- all three platform workflows now pass explicit `--rid`
- Windows system-DLL exclusion list now includes `iphlpapi.dll` to avoid false unresolved dependency noise for `SDL2_net`
- remaining work is CI matrix validation for this expanded command set

Critical architectural note:

- current harvest output is still organized for local-first execution
- the future release pipeline cannot assume Windows, Linux, and macOS runners share a filesystem
- `PathService` already exposes harvest-staging helpers, but `HarvestTask`, `ConsolidateHarvestTask`, and workflow YAML still need to adopt a real staging-to-consolidated flow
- treat this as a release-pipeline prerequisite, not a cosmetic cleanup

### 2.5 Clean Up Native Binaries from Git

#### Priority: MEDIUM — Affects repo performance

~50+ binary files are tracked in git under `src/native/*/runtimes/`. These were committed for testing but should be:

1. Added to `.gitignore`
2. Removed from tracking (`git rm --cached`)
3. Optionally cleaned from history (BFG or `git filter-repo`)

### 2.6 Local Development Playbook Validation

#### Priority: MEDIUM — Needed for contributors

Current state: A playbook exists, but it drifted from the actual harvest output structure and needs validation against the current build host and workflows.

Document how to:

- Set up the project from scratch (clone, submodule init, .NET SDK, vcpkg bootstrap)
- Build managed bindings without native binaries
- Build native binaries locally for your platform
- Run the harvest pipeline locally
- Use CI artifacts for local development without building natives
- Test with sample projects

See [playbook/local-development.md](../playbook/local-development.md).

### 2.7 SDL2_net Binding Project

#### Priority: LOW — Can be added quickly once vcpkg.json is complete

- Add `external/sdl2-cs` doesn't include SDL2_net bindings (it's not part of flibitijibibo's project)
- Need to either find a community binding or write one (SDL2_net API is small)
- Create `src/SDL2.Net/` and `src/native/SDL2.Net.Native/`
- Add to solution and manifest.json

### 2.8 Shared Native Dependency Policy (Pre-Coding Deep Dive)

#### Priority: HIGH — Must be documented before workflow coding

Problem statement:

- multiple satellite native packages can carry the same dependency basename (example: `zlib1.dll` in both Image and Mixer on Windows)
- when consumers reference multiple satellites, output flattening can leave a single winner for same-name binaries
- Linux/macOS symlink preservation (`native.tar.gz`) solves SONAME-chain integrity, but does not by itself define cross-package version/collision policy

Required deliverables before implementation changes:

1. Per-RID duplicate basename inventory across all satellite native payloads
2. Hash equality report for duplicates (initial focus: zlib family)
3. Decision record: how to handle same-name duplicates
4. CI guardrail design (fail build when duplicate basenames have non-identical hashes for same RID)
5. Migration notes for moving to a shared dependency strategy if needed later

Acceptance gate:

- do not modify packaging/CI behavior for this topic until this deep-dive note is reviewed and approved

### 2.9 Platform Workflow Harvest Parity

#### Priority: HIGH — Required for reliable matrix behavior

Target behavior:

- all platform workflows pass explicit `--rid ${{ inputs.rid }}` to Harvest
- all platform workflows harvest the same SDL2 satellite set (SDL2, SDL2_image, SDL2_mixer, SDL2_ttf, SDL2_gfx, SDL2_net)
- command shape and inputs are consistent across Windows/Linux/macOS for easier auditing and troubleshooting

### 2.10 Windows Local Tooling Prerequisites Guide

#### Priority: MEDIUM — Documentation TODO before wider contributor rollout

Add a dedicated local-development guideline for Windows native/dependency tooling:

1. Required Visual Studio Build Tools component set (C++ toolchain requirement)
2. Recommended shell context (Developer PowerShell/Developer Command Prompt)
3. `dumpbin` discovery behavior and fallback order used by the build host
4. Troubleshooting for missing `dumpbin.exe` and `vswhere.exe`
5. Quick verification checklist contributors can run locally

### 2.11 Build Host Test Hardening (Whitebox + Blackbox)

#### Priority: HIGH — Required before large refactor work

Current state:

- Build host test project exists and runs reliably.
- Coverage baseline is low for refactor-sensitive orchestration/runtime-boundary paths.
- Test topology is partially mixed and does not yet fully mirror `build/_build` production boundaries.

Required deliverables:

1. Reorganize test hierarchy to mirror `Modules`, `Tasks`, and `Tools` boundaries.
2. Remove test-side mirrored production logic patterns (validate actual SUT behavior instead).
3. Add blackbox task-flow tests for `HarvestTask`, `ConsolidateHarvestTask`, and `PreFlightCheckTask`.
4. Add whitebox boundary tests for runtime scanners and vcpkg provider error paths.
5. Add tool-wrapper tests (argument construction and parsing behavior) using Cake test fixtures.
6. Document deterministic coverage execution for TUnit + Microsoft.Testing.Platform and standardize artifact output location.
7. Introduce a no-regression coverage gate with branch coverage tracking.

## Exit Criteria

- [ ] `vcpkg.json` declares all 6 SDL2 libraries with appropriate features
- [ ] vcpkg baseline updated to latest (SDL2 ≥ 2.32.10)
- [ ] Cake PackageTask produces valid .nupkg files from harvest output
- [ ] At least one full pipeline run: vcpkg build → harvest → consolidate → package
- [ ] Native binaries removed from git tracking
- [ ] `.gitignore` rules prevent re-committing binaries
- [ ] Local development playbook corrected, validated, and tested
- [ ] SDL2_net binding project created (even if native packaging is Phase 3)
- [ ] Shared native dependency deep-dive documented and approved before related CI/package code changes
- [ ] Platform workflows use explicit RID and full satellite harvest parity across Windows/Linux/macOS
- [ ] Windows local tooling prerequisites guide is documented and linked from the playbook
- [ ] Build-host tests are organized by production boundaries (`Modules`/`Tasks`/`Tools`) with explicit `Integration` and `Characterization` scopes
- [ ] No mirrored production logic remains in build-host tests; orchestration behavior is validated against real task outputs
- [ ] Deterministic coverage command and output path for TUnit/MTP are documented and reproducible
- [ ] Coverage gate policy is active with branch coverage tracked and no-regression enforced

## Dependencies

- Phase 1 (DONE) — All Phase 1 outputs are prerequisites
- No external blockers — all tools and infrastructure are available

## Risks

| Risk | Impact | Mitigation |
| --- | --- | --- |
| vcpkg baseline update breaks builds | HIGH | Test one triplet first, then matrix |
| SDL2_net has no C# bindings in SDL2-CS | LOW | API is small (~20 functions), can write manually or find community binding |
| PackageTask complexity | MEDIUM | Start with single-library manual test, then automate |
| Git history cleanup breaks forks | LOW | Repo has no known forks; announce before force-pushing |
