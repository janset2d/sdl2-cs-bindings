---
name: "S14 Slice E CI polish continuation"
description: "Priming prompt for the next agent continuing the Slice E follow-up pass after the 2026-04-25 net4x AnyCPU + Linux/macOS native extraction layout session. This supersedes s13: buildTransitive native asset placement is now correct on all build modes (RID-specific flat / portable nested) AND .NET Framework AnyCPU consumers no longer need the manual <PlatformTarget>x64</PlatformTarget> workaround. Immediate roadmap tail is P5-P8."
argument-hint: "Optional focus area, constraints, or whether implementation is already approved for a specific item"
agent: "agent"
model: "Claude Opus 4.7 (1M context)"
---

You are a software engineer entering `janset2d/sdl2-cs-bindings` mid-pass. The **ADR-003 pass-1** merged to master at `bfc6713`, and the **Slice E follow-up pass** is still the active Phase 2b workstream. This prompt supersedes `.github/prompts/s13-slice-e-ci-polish-continuation.prompt.md`, which is now historical context rather than the live handoff.

## First Principle

Treat every claim here as **current-as-of-authoring (2026-04-25)** and **verify against the live repo, git log, and canonical docs before acting**. Drift exists. If code and docs disagree, trust the code, then report or fix the doc drift.

This follow-up pass is running as **master-direct commits** per Deniz direction. Do not create a branch unless there is a concrete reason.

## Onboarding Snapshot

This repo is the packaging/build/distribution layer for **modular C# SDL2 bindings first, SDL3 later**, with **native libraries built from source via vcpkg** and shipped through **NuGet**. The build backbone is:

- `.NET 9 / C# 13`
- `Cake Frosting` build host under `build/_build/`
- `vcpkg` for native builds
- `GitHub Actions` for the RID matrix
- `build/manifest.json` as the single source of truth

Locked strategic decisions you should not casually reopen:

- **Hybrid static + dynamic core** packaging model
- **Triplet = strategy**
- **Package-first consumer contract**
- **Cake owns orchestration policy; YAML stays thin**
- **7 RID coverage remains in scope**

Target RIDs currently surfaced by `build/manifest.json`:

- `win-x64`
- `win-arm64`
- `win-x86`
- `linux-x64`
- `linux-arm64`
- `osx-x64`
- `osx-arm64`

## Where the Pass Is

- **Master HEAD**: filled in at commit + push time (last commit before this prompt: `b9f63f4` Linux/macOS RuntimeIdentifier-aware extract; the .NETFramework AnyCPU fix lands on top of it as a separate commit).
- **Worktree state** at authoring: net4x fix staged in working tree (`src/native/_shared/Janset.SDL2.Native.Common.targets` + `build/_build/Application/Packaging/PackageConsumerSmokeRunner.cs`); Deniz commits + pushes manually.
- **Current phase**: Phase 2b, Slice E follow-up pass still open.
- **Build-host tests**: 460/460 green on the Windows host during this session (no test surface change in this pass).
- **Local witnesses (this session)**:
  - Windows `tests/scripts/smoke-witness.cs local`: 3/3 PASS.
  - Windows AnyCPU net462 manual `dotnet build` against fresh local-pack nupkgs: native SDL2.dll/SDL2_image.dll/SDL2_mixer.dll/SDL2_ttf.dll/SDL2_gfx.dll land in `bin/Release/net462/` at x64 sizes (~3.1 MB SDL2.dll = vcpkg static-linked x64). 0 warnings.
  - WSL Linux ext4 clone (`/home/deniz/repos/sdl2-cs-bindings`) `smoke-witness.cs local`: 3/3 PASS. Plus learning-sdl2 portable consumer build with fresh nupkgs: nested `runtimes/linux-x64/native/` symlink chain intact, bin root clean.
  - WSL Linux 3 publish scenarios (portable / RID-specific / self-contained): all green per the Linux fix commit.
- **CI run from this session**: `24833589266` was full-green BEFORE the targets fixes (validates hygiene-pass commit `3f6a2f1`). Deniz triggered fresh runs after each subsequent push; pull `gh run list --workflow=release.yml` to see the current state.

### Commit Timeline You Care About

The earlier P1-P4 groundwork is already covered by `s12` and `s13`. Three commits matter most for this handoff:

1. `3f6a2f1` — `ci(release): trim nuget-cache + platform-build-prereqs to actual needs` — FDD-only consumer jobs (resolve-versions, preflight, generate-matrix, harvest, consolidate-harvest) drop nuget-cache; consumer-smoke drops platform-build-prereqs. setup-dotnet kept everywhere for cross-runner SDK determinism. Reference table lives inline in `release.yml` header (P4e bullet).
2. `b9f63f4` — `fix(buildTransitive): RuntimeIdentifier-aware extract destination` — replaces an earlier broken `25f755f` (which extracted Unix tarballs into `runtimes/<rid>/native/` unconditionally and broke `dotnet test -r linux-x64`). Now branches by `$(RuntimeIdentifier)`: portable build → nested `$(OutDir)runtimes/<rid>/native/`; RID-specific build → flat `$(OutDir)`. Mirrors ppy.SDL3-CS / SkiaSharp single-`.so` SDK convention. Pre-extract cleanup gated to portable mode (RID-specific output root mixes our files with consumer's own; whole-folder wipe unsafe). 5 publish/build scenarios verified green on WSL.
3. **(staged at handoff time)** — `fix(buildTransitive,smoke): .NETFramework AnyCPU native DLL arch alignment` — see "What This Session Actually Closed" §1 below.

Reference for the SDK behaviour driving §1: <https://learn.microsoft.com/en-us/nuget/create-packages/native-files-in-net-packages> ("SDK style projects targeting .NET Framework").

## What This Session Actually Closed

### 1. .NET Framework AnyCPU consumers no longer need the `<PlatformTarget>x64</PlatformTarget>` workaround

Live code anchors:

- `src/native/_shared/Janset.SDL2.Native.Common.targets` — `_JansetSdlNativeCopyDllsForFrameworkWindows` target body now derives `_JansetSdlNet4xCopyRid` from Platform / Prefer32Bit / OSArchitecture and **deliberately ignores `$(RuntimeIdentifier)`**.
- `build/_build/Application/Packaging/PackageConsumerSmokeRunner.cs` — new `AppendNet4xPlatformArgument` helper forwards `-p:Platform=<arch>` alongside `-r <rid>` only when the smoke target framework starts with `net4`.

What landed and why:

- SDK net4x + AnyCPU + native package presence auto-sets `PlatformTarget=x86` + `RuntimeIdentifier=win-x86`, but `Prefer32Bit=false` (modern default) keeps the apphost at host arch → x64 process tries to load x86 SDL2.dll → `BadImageFormatException` at first P/Invoke. Documented Microsoft behaviour, not a bug we can suppress at the SDK level.
- User-explicit `-r win-x86` and SDK auto-x86 produce identical MSBuild property states (Platform=AnyCPU, PlatformTarget=x86, RuntimeIdentifier=win-x86). RuntimeIdentifier therefore cannot be used as the "user intent" signal — we have to consult Platform + Prefer32Bit + OSArchitecture instead.
- Resolution priority inside the target:
  1. Explicit Platform=x64/x86/ARM64 mapping wins.
  2. AnyCPU + Prefer32Bit=true → win-x86 (32-bit-required intent honoured).
  3. AnyCPU + Prefer32Bit unset/false → host OSArchitecture (canonical "AnyCPU runs at host arch" semantic; overrides SDK auto-x86).
- Smoke runner forwards `-p:Platform=<arch>` for `net4*` TFMs so its explicit `-r <rid>` intent maps to Priority 1 (no ambiguity with SDK auto-x86). Mapping: `win-x64 → x64`, `win-x86 → x86`, `win-arm64 → ARM64`. No-op for non-`net4*` TFMs and non-Windows RIDs.

Approach attempted and rolled back during the session: a buildTransitive `.props` that pre-emptively set `RuntimeIdentifier`. Hits a chicken-and-egg restore problem — NuGet generates `obj/.../<csproj>.nuget.g.props` (which auto-imports our `.props`) only AFTER the first restore, but our `.props` would need to influence that very restore's RID-aware asset selection. NETSDK1047 ("Assets file does not have a target for net462/win-x64") results. Build-time `.targets` resolution sidesteps the entire issue by not touching `RuntimeIdentifier` at all.

Not addressed (deferred to follow-up):

- Documented playbook guideline for `Prefer32Bit=true` consumers needing explicit `<PlatformTarget>x86</PlatformTarget>` to opt back into 32-bit native shipping. Code already supports it via Priority 1; only the docs are missing.
- Stale-residue mitigation for net4x builds across package version bumps (Linux equivalent landed in `b9f63f4` for Unix; net4x output root mixes our DLLs with the consumer's own binaries, so a wholesale wipe is unsafe — needs a glob-based stale-DLL approach).

### 2. CI hygiene pass

The `3f6a2f1` commit removed dead-weight composites:

- `nuget-cache` dropped from FDD-only jobs (`resolve-versions`, `preflight`, `generate-matrix`, `harvest`, `consolidate-harvest`). Cake.Frosting addins are compile-time bundled into the published cake-host FDD; those jobs do zero runtime NuGet work.
- `platform-build-prereqs` dropped from `consumer-smoke`. brew autotools are vcpkg port build-time deps, not consumer-side; consumer-smoke only does NuGet restore + `dotnet test`.
- `setup-dotnet` kept on every job: non-negotiable on container jobs (custom focal image has no .NET baked in by design — see `docker/linux-builder.Dockerfile`); on host runners it pins to `global.json` (`9.0.200`) to neutralise rollForward drift between pre-installed 9.0.x patches across runner images.

The `release.yml` header carries the inline reference table (P4e bullet) so the audit rationale survives.

## Important Drift Note

The docs are still behind reality on two surfaces:

1. The `s13` cache-identity drift (per-platform child manifest digest selection vs top-level GHCR digest) noted in `s13` is unchanged — `docs/plan.md` and `docs/phases/phase-2-release-cycle-orchestration-implementation-plan.md` still describe the older shape.
2. **New as of this session:** `docs/plan.md` and `docs/playbook/cross-platform-smoke-validation.md` describe the buildTransitive native extraction as flat-into-`$(OutDir)`. Live code at `b9f63f4` does **portable nested + RID-specific flat dual-mode** with stale-update wipe gated to portable mode. The Janset.SDL2.Native.Common.targets header comment block carries the canonical rationale; the canonical-doc surface needs an alignment pass during P8.
3. **New as of this session:** the existing `_JansetSdlNativeWarnAnyCpuArchAssumption` and `_JansetSdlNativeErrorOnUnresolvedFrameworkRid` targets reference `$(_JansetSdlEffectiveRid)`. The new `_JansetSdlNet4xCopyRid` resolution lives in a separate property to avoid disturbing the warning/error contracts; the warning text still talks about "OSArchitecture fallback" semantics that are now slightly drifted from the actual copy logic. Worth a comment-cleanup pass either inside P8 or alongside the playbook guideline addition (see "Suggested Re-entry Point").

If you touch the `Janset.SDL2.Native.Common.targets` file, **the in-file comments are the canonical reference** for the layout / arch-resolution contract. Update the canonical docs to point at the file, not the other way around.

## Current Workflow / Manifest Reality

Relevant live facts (mostly unchanged since `s13`):

- `build/manifest.json` uses GHCR `focal-latest` for both Linux rows.
- `release.yml` runs Linux harvest inside that convenience tag; cache-key identity is derived immutably inside `vcpkg-setup`.
- `consumer-smoke` matrix-re-enters by RID. win-x86 runtime bootstrap is Cake-owned. Smoke runner now also forwards `-p:Platform=<arch>` for `net4*` TFMs (live in `PackageConsumerSmokeRunner.RunSmokeForTfm`), which the CI matrix transparently picks up — no `release.yml` edit was needed for the AnyCPU fix.

## Immediate Roadmap Tail (Slice E Follow-up)

Per `docs/phases/phase-2-release-cycle-orchestration-implementation-plan.md`, the pass is now P5-P8 plus one new deferred item carried over from this session.

| Item | Description | Status |
| --- | --- | --- |
| **P5** | Lock-file discipline. **Decided this session**: scope is `build/_build/Build.csproj` + `build/_build.Tests/Build.Tests.csproj` (Cake build host) + `src/SDL2.*/*.csproj` (managed bindings) + `src/native/SDL2.*.Native/*.csproj` (native packaging). **Excluded**: `tests/smoke-tests/package-smoke/*.csproj` and any future smoke / library-specific test consumer that imports Janset packages with dynamic versions injected via `Janset.Smoke.props` — locking those would fight the CI per-run version mapping. Implementation: `<RestorePackagesWithLockFile>true</RestorePackagesWithLockFile>` on in-scope csprojs + commit `packages.lock.json`s + CI restores use `--locked-mode`. CI scope is small: only `build-cake-host` job's restore step needs `--locked-mode`; downstream consumers consume the cake-host artifact. | Not started |
| **P6** | `PublishTask` stubs. Add `build/_build/Application/Publishing/PublishTaskRunner.cs` + `build/_build/Tasks/Publish/PublishTask.cs` (body throws `NotImplementedException` with Phase-2b message); `release.yml` `publish-staging` / `publish-public` jobs already gated `if: false`. Pure scaffolding, ~50-100 LoC + 2-3 registration tests. | Not started |
| **P7** | Three-platform witness per `cross-platform-smoke-validation.md` A-K checkpoints. Deniz-direction during this session: macOS deferred to CI (no Mac hardware available locally); local witness scope = Windows + WSL Linux. Both already greened in this session at the spot-check level (smoke-witness 3/3 + manual AnyCPU manual build); the formal A-K walkthrough remains. | Not started (informally green) |
| **P8** | Retirement + doc sweep tail. Delete `prepare-native-assets-*.yml` + `release-candidate-pipeline.yml` (placeholder stub); align canonical docs (see "Important Drift Note"); resolve `§11 Q17` ADR-002 §2.3 delegate-hook stance; `§11 Q18` CMakePresets dev-experience refactor. Also fold in: native-DLL-layout doc alignment (Janset.SDL2.Native.Common.targets is now the canonical reference), .NETFramework AnyCPU consumer guideline (explicit x86 opt-in via `<PlatformTarget>x86</PlatformTarget>`), smoke runner net4x `-p:Platform=<arch>` behaviour mention. **Slice E follow-up pass closure commit.** | Not started |
| **P4e** | (Deferred from this session.) Bake .NET SDK into `docker/linux-builder.Dockerfile` via multi-stage `COPY --from=mcr.microsoft.com/dotnet/sdk:9.0.200 /usr/share/dotnet /usr/share/dotnet`. Saves ~30-60 s per container CI job × 4 container jobs = ~2-3 min/run; image grows ~200 MB. Currently container jobs always run `setup-dotnet`. Trade-off: two truth points (global.json + image SDK version) need synchronised bumps. Not blocking; revisit when CI wall-clock cost or `setup-dotnet` flakiness becomes the active complaint. | Not started, low priority |

The release-tag push gate is still in force: **do not treat Slice E as closed until this tail is done and witnessed**.

## Broader Phase 2b Tail After Slice E Closes

From `docs/plan.md`, the next big items after P5-P8 are still:

- **PA-2 behavioural validation** on the four newly-hybridized rows (`win-arm64`, `win-x86`, `linux-arm64`, `osx-arm64`).
- Full `release.yml` hardening as the real end-to-end release pipeline.
- `RemoteArtifactSourceResolver` implementation.
- Family/train orchestration follow-through.
- First real prerelease publication later in Phase 2b.

So Slice E closure is not "Phase 2 done"; it is "CI-polish pass done, next tail unlocked."

## Mandatory Grounding (read in this order)

1. `docs/onboarding.md`
2. `AGENTS.md`
3. `docs/plan.md`
4. `docs/phases/phase-2-release-cycle-orchestration-implementation-plan.md`
5. `docs/decisions/2026-04-18-versioning-d3seg.md`
6. `docs/decisions/2026-04-19-ddd-layering-build-host.md`
7. `docs/decisions/2026-04-20-release-lifecycle-orchestration.md`
8. `.github/workflows/release.yml`
9. `.github/actions/vcpkg-setup/action.yml`
10. `build/manifest.json`
11. `src/native/_shared/Janset.SDL2.Native.Common.targets` ← **new canonical reference for native-asset placement contract; the in-file comment block is the authoritative explanation for layout + arch resolution rules.**
12. `build/_build/Application/Packaging/PackageConsumerSmokeRunner.cs`
13. `build/_build/Infrastructure/DotNet/DotNetRuntimeEnvironment.cs`
14. `tests/scripts/smoke-witness.cs`

Historical archaeology only if needed:

- `.github/prompts/s13-slice-e-ci-polish-continuation.prompt.md`
- `.github/prompts/s12-slice-e-ci-polish-continuation.prompt.md`

## Locked Policy Recap

- **Master-direct follow-up pass.**
- **No commit without Deniz approval.**
- **Do not reintroduce inline workflow PowerShell for win-x86 runtime bootstrap.**
- **Do not resurrect stale `runner-os` input shape in `vcpkg-setup`.**
- **Cake remains the policy owner; workflow YAML should stay thin.**
- **Native asset layout contract lives in `Janset.SDL2.Native.Common.targets` in-file comments; doc surface follows the file, not vice versa.**
- **For .NETFramework AnyCPU consumer behaviour, the `_JansetSdlNet4xCopyRid` resolution is intentionally Platform / Prefer32Bit / OSArchitecture-driven — `$(RuntimeIdentifier)` is unreliable due to SDK auto-x86 inference indistinguishability. Do not add a "Priority 0: trust RuntimeIdentifier" branch unless you have a way to detect SDK-auto-set vs user-explicit that this session missed.**
- **Lock-file scope (P5)**: `build/**` + `src/**` only. Smoke / dynamic-version consumers under `tests/smoke-tests/` are explicitly excluded.
- **If docs and code disagree, verify then fix the doc drift.**

## Suggested Re-entry Point

If Deniz has not redirected the work elsewhere, the clean next move is **P5 lock-file discipline** with the scope decided this session (`build/**` + `src/**`, smoke consumers excluded). Mechanically:

1. Add `<RestorePackagesWithLockFile>true</RestorePackagesWithLockFile>` to in-scope csprojs (or to `build/Directory.Build.props` + `src/Directory.Build.props` if a folder-scoped property is cleaner — verify which scope's csprojs are actually in P5 scope before going broader).
2. `dotnet restore` each in-scope csproj to generate `packages.lock.json`. Commit them.
3. Update `release.yml` `build-cake-host` job's `dotnet restore` step to pass `--locked-mode`.
4. Verify smoke-witness still green (no consumer-side regression).
5. CI dispatch — verify build-cake-host job still green; the rest of the matrix consumes the cake-host artifact and is unaffected.

Adjacent micro-task (can land in the same commit or a follow-up): playbook guideline for `Prefer32Bit=true` net4x consumers explaining how to opt back into win-x86 native shipping (`<PlatformTarget>x86</PlatformTarget>` or `<RuntimeIdentifier>win-x86</RuntimeIdentifier>`), since the new build-time logic defaults to host arch when neither is set. Could also fold in a small comment-cleanup pass on the now-slightly-drifted `_JansetSdlNativeWarnAnyCpuArchAssumption` text. Both fit naturally inside P8 if you'd rather keep P5 surgical.
