---
name: "S13 Slice E CI polish continuation"
description: "Priming prompt for the next agent continuing the Slice E follow-up pass after the 2026-04-23 CI-polish session. This supersedes s12: win-x86 ConsumerSmoke runtime bootstrap now lives in Cake, and vcpkg cache-key identity now flows through platform-identity in the composite action. Immediate roadmap tail is P5-P8."
argument-hint: "Optional focus area, constraints, or whether implementation is already approved for a specific item"
agent: "agent"
model: "Claude Opus 4.7 (1M context)"
---

You are a software engineer entering `janset2d/sdl2-cs-bindings` mid-pass. The **ADR-003 pass-1** merged to master at `bfc6713`, and the **Slice E follow-up pass** is still the active Phase 2b workstream. This prompt supersedes `.github/prompts/s12-slice-e-ci-polish-continuation.prompt.md`, which is now historical context rather than the live handoff.

## First Principle

Treat every claim here as **current-as-of-authoring (2026-04-23)** and **verify against the live repo, git log, and canonical docs before acting**. Drift exists. If code and docs disagree, trust the code, then report or fix the doc drift.

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

- **Master HEAD**: `5d6c70b`
- **Worktree state** at authoring: clean (`git status --short` empty)
- **Current phase**: Phase 2b, Slice E follow-up pass still open
- **Build-host tests**: `460/460` green on the Windows host during this session
- **Local witness**: `tests/scripts/smoke-witness.cs local` was run successfully on this machine for `windows-local`, `3/3 PASS`
  - Important operator detail: run it from `tests/scripts`, not the repo root
  - Witness logs: `.logs/witness/windows-local-20260423T104512Z`

### Commit Timeline You Care About

The earlier P1-P4 groundwork is already covered by `s12`. The two commits that matter most for this handoff are:

1. `6618427` — `ci: move win-x86 smoke runtime bootstrap into Cake and inline vcpkg cache identity resolution`
2. `5d6c70b` — `feat: update vcpkg setup action to use platform identity for cache key resolution`

Useful immediate context before those two:

1. `b0ccda0` — `ci(release): replace jq/mapfile with --versions-file and absorb NativeSmoke into Harvest`
2. `bc652d1` — removed `SDL2_net` from `library_manifests[]` and updated tests/docs
3. `3fe0303` — added `.NET 8` SDK to ConsumerSmoke setup-dotnet

## What This Session Actually Closed

The two "open issues" from `s12` are no longer open in code.

### 1. win-x86 ConsumerSmoke is no longer solved by ugly workflow YAML

The win-x86 hostfxr/runtime problem now belongs to Cake, not `release.yml`.

Live code anchors:

- `build/_build/Domain/Runtime/IDotNetRuntimeEnvironment.cs`
- `build/_build/Infrastructure/DotNet/DotNetRuntimeEnvironment.cs`
- `build/_build/Application/Packaging/PackageConsumerSmokeRunner.cs`
- `build/_build/Program.cs`

What landed:

- `PackageConsumerSmokeRunner` resolves executable TFMs from the smoke project first.
- For `--rid win-x86` on Windows only, `DotNetRuntimeEnvironment` derives the required runtime channels from the TFM list.
- It ignores `netstandard*` and `net4*`, so `net462` is intentionally left alone.
- It installs x86 runtimes through the official `dotnet-install.ps1` into a temp cache.
- It injects only `DOTNET_ROOT_X86` and `DOTNET_ROOT(x86)` into the child `dotnet test` process.
- The parent Cake host remains on x64.

This is deliberately analogous to the existing MSVC environment ownership pattern: build-policy belongs in Cake, not as a random PowerShell branch jammed into workflow YAML.

### 2. vcpkg cache-key identity was simplified and renamed around execution surface, not "container image"

Live code anchors:

- `.github/actions/vcpkg-setup/action.yml`
- `.github/workflows/release.yml`
- `build/manifest.json`

What landed:

- `vcpkg-setup` no longer takes `runner-os` or `container-image`.
- The action now takes **one main identity input**: `platform-identity`.
- Callers pass either the runner label or the declared job container image via:
  - `platform-identity: ${{ matrix.container_image || matrix.runner }}`
- The action detects whether it is in a job container using:
  - primary: `job.container.id`
  - fallback: `/.dockerenv` or `/run/.containerenv`
- If it is in a non-Windows container job and the identity is a GHCR tag, it resolves the tag to an immutable digest for the cache key.
- Host jobs keep the raw runner label as their cache identity.
- The restore-key fallback was narrowed; the broad misleading fallback is gone.

The current live implementation resolves the **top-level GHCR manifest digest** by HEAD-ing the registry manifest endpoint and reading `docker-content-digest`. It does **not** currently parse the manifest-list JSON and pick a child digest by `runner.arch`.

## Important Drift Note

The docs are a little behind the code here.

At authoring time:

- `docs/plan.md`
- `docs/phases/phase-2-release-cycle-orchestration-implementation-plan.md`

still describe the 2026-04-23 cache-identity result in a slightly different form, mentioning **per-platform child manifest digest selection via `runner.os` + `runner.arch`** and "working tree" phrasing. That is not the live code at `5d6c70b`.

Live code reality at `5d6c70b` is:

- the design is **`platform-identity`**
- container detection is **`job.container.id` + filesystem fallback**
- digest resolution is **top-level GHCR digest**, not child-manifest selection
- the changes are **committed**, not just in the working tree

If you touch this area next, clean up the docs so they stop gaslighting the next poor soul.

## Current Workflow / Manifest Reality

Relevant live facts:

- `build/manifest.json` uses GHCR `focal-latest` for both Linux rows:
  - `linux-x64` -> `ghcr.io/janset2d/sdl2-bindings-linux-builder:focal-latest`
  - `linux-arm64` -> `ghcr.io/janset2d/sdl2-bindings-linux-builder:focal-latest`
- `release.yml` still runs Linux harvest inside that convenience tag, but the cache key identity is derived immutably inside `vcpkg-setup`.
- `consumer-smoke` still matrix-re-enters by RID, but win-x86's special runtime bootstrap is now Cake-owned. The workflow comment explicitly says so.

## Immediate Roadmap Tail (Slice E Follow-up)

Per `docs/phases/phase-2-release-cycle-orchestration-implementation-plan.md`, the pass is now basically the P5-P8 tail:

| Item | Description | Status |
| --- | --- | --- |
| **P5** | Lock-file discipline: `packages.lock.json`, `<RestorePackagesWithLockFile>true</RestorePackagesWithLockFile>`, CI `--locked-mode` | Not started |
| **P6** | `PublishTask` stubs with explicit Phase 2b message | Not started |
| **P7** | Three-platform witness per `cross-platform-smoke-validation.md` A-K checkpoints | Not started |
| **P8** | Retirement + doc sweep tail | Not started |

The release-tag push gate is still in force: **do not treat Slice E as closed until this tail is done and witnessed**.

## Broader Phase 2b Tail After Slice E Closes

From `docs/plan.md`, the next big items after P5-P8 are still:

- **PA-2 behavioral validation** on the four newly-hybridized rows:
  - `win-arm64`
  - `win-x86`
  - `linux-arm64`
  - `osx-arm64`
- Full `release.yml` hardening as the real end-to-end release pipeline
- `RemoteArtifactSourceResolver` implementation
- Family/train orchestration follow-through
- First real prerelease publication later in Phase 2b

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
11. `build/_build/Infrastructure/DotNet/DotNetRuntimeEnvironment.cs`
12. `build/_build/Application/Packaging/PackageConsumerSmokeRunner.cs`
13. `tests/scripts/smoke-witness.cs`

Historical archaeology only if needed:

- `.github/prompts/s12-slice-e-ci-polish-continuation.prompt.md`

## Locked Policy Recap

- **Master-direct follow-up pass**
- **No commit without Deniz approval**
- **Do not reintroduce inline workflow PowerShell for win-x86 runtime bootstrap**
- **Do not resurrect stale `runner-os` input shape in `vcpkg-setup`**
- **Cake remains the policy owner; workflow YAML should stay thin**
- **If docs and code disagree, verify then fix the doc drift**

## Suggested Re-entry Point

If Deniz has not redirected the work elsewhere, the clean next move is **P5 lock-file discipline**. The two operational blockers from `s12` are already landed, local host validation was green in this session, and the repo is sitting at a clean master HEAD ready for the next slice of boring-but-important CI hygiene.
