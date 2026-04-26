---
name: "S17 Phase 2b pickup (post-PD-5 closure, public-release horizon)"
description: "Priming prompt for the next agent entering janset2d/sdl2-cs-bindings after PD-5 closure (RemoteArtifactSourceResolver + PublishStagingTask + VersionsFileWriter + smoke-witness remote mode landed across commits 94a3696..549ad2f, plus local-dev / smoke-witness doc updates). The internal GitHub Packages staging feed is populated and end-to-end witnessed (CI run 24962876812; local Windows witness 3/3 PASS, ConsumerSmoke 35/35). Recommended next move: settle small post-session wins (publish-staging gate-lift, multi-platform --source=remote witness, PD-5 doc closures across canonical docs), then turn to the public-release horizon — Phase 2b PD-7 + first prerelease publish to nuget.org (#63) via Trusted Publishing OIDC."
argument-hint: "Optional focus area, constraints, or reason to override the recommended next step"
agent: "agent"
model: "Claude Opus 4.7 (1M context)"
---

You are a software engineer entering `janset2d/sdl2-cs-bindings` after **PD-5 closure (RemoteInternal artifact source profile) + first GitHub Packages publish witness**. The repo is several steps past the s16 baseline:

- **Latest CI evidence:** `release.yml` run `24962876812` on master `fdabcae` — `workflow_dispatch` `mode=manifest-derived` + `publish-staging=true`; all 7 RIDs Pack ✓ + ConsumerSmoke ✓ + Publish (Staging) ✓. 5 families × 2 nupkgs = 10 nupkgs landed in `https://nuget.pkg.github.com/janset2d/index.json` at version `<UpstreamMajor>.<UpstreamMinor>.0-ci.24962876812.1` per family.
- **Latest local witness:** `tests/scripts/smoke-witness.cs remote --verbose` on Windows `win-x64`, 3/3 PASS, ConsumerSmoke 35/35 (12+12+11), 78.6s total. Validated full e2e Pack → Push → Pull → Smoke loop.

This prompt supersedes `s16-phase-2b-post-green-ci-pickup.prompt.md` for sessions that start after the PD-5 closure session.

## First Principle

Treat every claim here as **current-as-of-authoring (2026-04-26)** and verify against the live repo, git log, and canonical docs before acting.

The codebase is in a strong post-PD-5 state:

- PA-2 behavioral validation closed (commit `94a3696` doc record).
- RemoteArtifactSourceResolver + PublishStagingTask real impl landed end-to-end (commits `bc7c677`, `fdabcae`, `549ad2f`).
- GH Packages NuGet feed populated and witnessed both from CI side (publish) and local-dev side (download + consumer smoke).
- `docs/playbook/local-development.md` + `tests/scripts/README.md` updated with `--source=remote` workflow + `GH_TOKEN` setup recipes.

That means the highest-value next move is **either**:

1. Tie up the small post-session wins (publish-staging gate-lift, multi-platform `--source=remote` witness on Linux + macOS, PD-5 doc closure across canonical surfaces).
2. **OR** — the bigger horizon — pick up **public-release work**: PD-7 full-train orchestration + Trusted Publishing OIDC setup + first prerelease publish to nuget.org (#63) + draft `playbook/release-recovery.md` (PD-8).

Talk to Deniz before committing to which one. Smaller wins are ~30 min of doc + tiny YAML edit + 2 platform reruns; the public-release work is a multi-session arc.

This repo still runs **master-direct commits** per Deniz direction. Do not create a branch unless there is a concrete reason.

## What Just Happened

This session landed four atomic commits on master plus a doc-update commit:

1. **`94a3696` — `docs: close PA-2 behavioral validation via release.yml run 24938451364`** — declared run 24938451364 (post-push on `8ec85c5`) as the PA-2 witness; surgical edits across `plan.md`, `phase-2-adaptation-plan.md`, `cross-platform-smoke-validation.md`, `onboarding.md`. Per-RID test counts recorded (35/35 win-arm64 + win-x86, 24/24 linux-arm64 + osx-arm64).
2. **`bc7c677` — `feat(packaging): RemoteArtifactSourceResolver + INuGetFeedClient (PD-5)`** — PD-5 read path. New `RemoteArtifactSourceResolver` short-circuits `SetupLocalDevTaskRunner`'s harvest+pack chain and pulls latest published nupkgs from GH Packages. New `INuGetFeedClient` abstraction over `NuGet.Protocol` v7.3.1; `NuGetProtocolFeedClient` adapter with Cake-native `IFileSystem` writes. `JansetLocalPropsWriter` extracted as shared helper. NuGet trio bumped 7.3.0 → 7.3.1 (advisory GHSA-g4vj-cjjj-v7hg). Tests 460 → 474.
3. **`fdabcae` — `feat(publishing): PublishStagingTask real impl + GitHub Packages push (PD-5 write path)`** — PD-5 write path. `PublishTaskRunner` real impl over `INuGetFeedClient.PushAsync` (`PackageUpdateResource.Push`, `skipDuplicate=false`). `local.*` prerelease guardrail refuses local-pack output reaching staging. `PublishStagingTask` thin wrapper with `GH_TOKEN → GITHUB_TOKEN` auth chain. `PublishPublicTask` cleaned to direct `CakeException` stub (Phase 2b). `release.yml` `publish-staging` job real steps; gated behind `workflow_dispatch.inputs.publish-staging` boolean (default false). Per-job `permissions: packages: write` override; workflow-level stays `packages: read`. Tests 474 → 488.
4. **`549ad2f` — `feat(packaging): VersionsFileWriter shared helper + RemoteResolver versions.json + smoke-witness remote mode`** — extract `VersionsFileWriter` (sibling to `JansetLocalPropsWriter`) so both Local and Remote profiles emit `artifacts/resolve-versions/versions.json` with identical shape. `RemoteArtifactSourceResolver.PrepareFeedAsync` now writes versions.json, closing the parity gap. `tests/scripts/smoke-witness.cs` gains `remote` mode. Tests 488 → 493.
5. **Doc-update commit (this session close)** — `local-development.md` gained `--source=remote` quick-start + GH_TOKEN env var entry + GitHub Packages auth recipe. `tests/scripts/README.md` gained `remote` mode row + per-mode requirement split + auth-scope troubleshooting. **PD-5 doc closure swept across canonical surfaces**: `plan.md` (Current Phase narrative, Phase 2b roll-up, Strategic Decisions Artifact Source Profile row, What Works Today CI/CD table, Stream F roadmap checkbox, RemoteArtifactSourceResolver Q3 checkbox, Known Issues #2/#5/#11/#19), `phase-2-adaptation-plan.md` (Status header, Current State PD-5 closure bullet, Remaining Phase 2b tail, Implementation Streams D-ci + F rows, ADR-003 Implementation Sequence step 3, Pending Decisions PD-5 row), `cross-platform-smoke-validation.md` (Planned Checkpoint L marked promotion-criteria-met + Phase 2b implementation-plan reference). New `s17` prompt (this file).

**Live witness chain**:

- **CI Publish witness** (`release.yml` run `24962876812`): `workflow_dispatch` with `mode=manifest-derived` + `publish-staging=true` on master `fdabcae`. All 10 jobs green; `Publish (Staging)` step transcript:

  ```text
  PublishTaskRunner pushing 'sdl2-core'  = 2.32.0-ci.24962876812.1
  PublishTaskRunner pushing 'sdl2-gfx'   = 1.0.0-ci.24962876812.1
  PublishTaskRunner pushing 'sdl2-image' = 2.8.0-ci.24962876812.1
  PublishTaskRunner pushing 'sdl2-mixer' = 2.8.0-ci.24962876812.1
  PublishTaskRunner pushing 'sdl2-ttf'   = 2.24.0-ci.24962876812.1
  PublishTaskRunner pushed 5 family/families to 'https://nuget.pkg.github.com/janset2d/index.json'.
  ```

- **Local Pull witness** (`tests/scripts/smoke-witness.cs remote --verbose` on Windows `win-x64`): 3/3 PASS, 78.6s. CleanArtifacts ✓ → SetupLocalDev (remote) ✓ (28.6s — discovered + downloaded 5 families × 2 nupkgs from feed) → PackageConsumerSmoke ✓ (47.9s — net9.0/x64 + net8.0/x64 + net462/x64 = 12+12+11 = 35/35).

**Key research output** (folded into `local-development.md`): GitHub Packages NuGet feed requires authentication even for public packages — anonymous read is **not supported** on the NuGet/npm/Maven registries (only `ghcr.io` containers allow anonymous public pulls). External-consumer story for Janset.SDL2 must therefore go through nuget.org (Phase 2b PD-7), NOT GH Packages. GH Packages stays internal CI staging only. Fine-grained PATs do not work for GH Packages NuGet — Classic PATs only.

## Onboarding Snapshot

This repo is the packaging/build/distribution layer for **modular C# SDL2 bindings first, SDL3 later**, with **native libraries built from source via vcpkg** and shipped through **NuGet**.

Build backbone:

- `.NET 9 / C# 13` (build host); `.NET 10 SDK` for `tests/scripts/*.cs` file-based apps
- `Cake Frosting` build host under `build/_build/` (DDD-layered per ADR-002)
- `vcpkg` for native builds (custom `*-hybrid` overlay triplets)
- `GitHub Actions` for the RID matrix + release pipeline + builder image
- `build/manifest.json` schema v2.1 as the single source of truth
- `NuGet.Protocol 7.3.1` as the in-process feed client (read + write)

Locked decisions still not open for casual re-debate:

- **Hybrid static + dynamic core** packaging model
- **Triplet = strategy** (no standalone `--strategy` CLI flag)
- **Package-first consumer contract** (ADR-001)
- **Cake owns orchestration policy; YAML stays thin**
- **7 RID coverage remains in scope**
- **LGPL-free codec stack** for SDL2_mixer
- **GH Packages = internal CI staging only**; external consumers via nuget.org (PD-7)
- **`local.*` prerelease versions cannot reach staging feed** (PublishTaskRunner guardrail)

Target RIDs (canonical in `build/manifest.json runtimes[]`):

- `win-x64` / `win-x86` / `win-arm64`
- `linux-x64` / `linux-arm64`
- `osx-x64` / `osx-arm64`

## Current State You Should Assume Until Verified

- **Master HEAD**: most recent commit is the doc/prompt close from this session; verify with `git log --oneline -10`.
- **Worktree expectation**: clean.
- **Build-host tests**: 493/493 green (was 460 at s16 start).
- **CI evidence**: `release.yml` run `24962876812` green end-to-end on `fdabcae`, including `Publish (Staging)`.
- **GH Packages feed populated**: 5 families × 2 nupkgs (managed + native) at `<UpstreamMajor>.<UpstreamMinor>.0-ci.24962876812.1`. Public visibility flipped at the org + per-package level, but feed itself still requires PAT auth (by-design GitHub).
- **Publish-staging gate**: still behind `workflow_dispatch.inputs.publish-staging=true`. Tag pushes do NOT auto-trigger publish-staging today.
- **Publish-public**: stays stubbed (Phase 2b PD-7).
- **`SetupLocalDev --source=remote`**: operational, end-to-end witnessed on `win-x64`. **Linux + macOS witnesses pending.**
- **Local `Janset.Local.props`**: written by `RemoteArtifactSourceResolver` after the live witness; may be stale across machine reboots / fresh clones — re-run `SetupLocalDev` to regenerate.

## What Changed In Canonical Docs

- `docs/playbook/local-development.md` — `--source=remote` quick-start section + `GH_TOKEN` env var entry + GitHub Packages auth recipe (gh CLI scope refresh vs dedicated Classic PAT). Status header refreshed to mark PD-5 closure.
- `tests/scripts/README.md` — `remote` mode added to Modes table; per-mode requirement split (vcpkg/CMake only for `local`+`ci-sim`; `GH_TOKEN` only for `remote`); troubleshooting entry for "Failed to retrieve information about ..." auth-scope failure mode.
- `docs/plan.md` — Current Phase narrative, §Phase 2b stage description, §Strategic Decisions Artifact Source Profile row, §Phase Roll-Up 2b row, §What Works Today CI/CD Publish-path row, §Roadmap Q3 2026 Stream F + RemoteArtifactSourceResolver checkboxes flipped, §Known Issues #2 (publish path partial-live), #5 (remote selector landed), #11 (release lifecycle implementation mostly landed), #19 (ADR-003 + PD-5 closures) — all updated.
- `docs/phases/phase-2-adaptation-plan.md` — Status header, §Current State PD-5 closure bullet added, §Remaining Phase 2b tail rewritten (publish-staging gate-lift, multi-platform `--source=remote` witness, PD-7, PD-8), §Implementation Streams D-ci + F rows updated, §ADR-003 Implementation Sequence step 3 updated, §Pending Decisions PD-5 row marked Resolved with closure record.
- `docs/playbook/cross-platform-smoke-validation.md` — §Planned Checkpoints L marked "promotion criteria met 2026-04-26" with closure record (structural move to §Active is deferred); §Implementation Plan reference table footer updated to drop the "(Phase 2b)" qualifier on `--source=remote`.
- `.github/prompts/s17-phase-2b-public-release-pickup.prompt.md` — this prompt.

`docs/onboarding.md` "What Doesn't Work Yet" / "What Works Today" updates were **not** included in this commit — small follow-up surgical edits before the next session opens are still appropriate (drop `RemoteArtifactSourceResolver` bullet, possibly add a "first prerelease publication pending" replacement). Same for `phase-2-release-cycle-orchestration-implementation-plan.md` §12 (Out of Scope for This Pass) — left alone as historical slice-pass scope; live state lives in plan.md + adaptation-plan.md.

## Recommended Next Step

### Recommended pickup A — small wins (lift gate + multi-platform witness + onboarding tail)

Lowest-friction, highest-confidence move. PD-5 doc closure across `plan.md` + `phase-2-adaptation-plan.md` + `cross-platform-smoke-validation.md` already landed in the session-close commit, so pickup A is now even smaller — three sub-items:

1. **Lift the `publish-staging` gate**. The job is currently `if: github.event_name == 'workflow_dispatch' && inputs.publish-staging == true`. Witness validated the e2e flow; the gate is now ceremony. Three options:
   - **(a)** drop the `if` entirely — publish-staging runs on every tag push too; simplest.
   - **(b)** keep the workflow_dispatch input as an opt-out (`default: true`) for safety dry-runs.
   - **(c)** condition on `github.event_name == 'push'` (tag triggers) OR `inputs.publish-staging == true` (dispatch opt-in).
   Preference depends on Deniz; (c) is the most controlled but adds expression complexity.

2. **Multi-platform `--source=remote` witness**. Run `tests/scripts/smoke-witness.cs remote --verbose` on:
   - WSL Linux (linux-x64) — `GH_TOKEN` exported in the WSL session.
   - macOS Intel (osx-x64) — SSH into `Armut@192.168.50.205`. `GH_TOKEN` set on the macOS side.
   PD-5 exit-criterion #4 ("operational on all 3 host platforms") only fully closes after these.

3. **Onboarding-tail doc closure** (small surgical edits not done in the session-close commit):
   - `docs/onboarding.md` "What Doesn't Work Yet" → drop the `RemoteArtifactSourceResolver` reference if any; possibly add a "First public release pending" replacement bullet.
   - `cross-platform-smoke-validation.md` Active vs Planned tables — structural promotion of checkpoint L from §Planned to §Active table (deferred from session-close commit; promotion criteria already marked met inline).

### Recommended pickup B — public-release horizon (the big topic)

Genuinely a multi-session arc. THE next major work after PD-5 closure:

1. **Decide promotion-path mechanism** — `Promote-To-Public.yml` separate workflow (manual `workflow_dispatch` with package-id + version inputs; download nupkg from staging, re-push to nuget.org), OR a stage on `release.yml` itself, OR a meta-tag-driven full-train workflow per PD-7. ADR-003 §6 / `phase-2-adaptation-plan.md` PD-7 has the framing.
2. **Trusted Publishing (OIDC)** for nuget.org — GitHub Actions OIDC token → nuget.org trust relationship → keyless push. Avoids API-key rotation. nuget.org docs + Andrew Lock 2025 article are the authoritative refs.
3. **`PublishPublicTask` real impl** — the Cake-side counterpart. Likely a separate runner from `PublishTaskRunner` because the auth model differs (OIDC vs PAT) and the source feed differs (read from staging, push to public).
4. **`docs/playbook/release-recovery.md`** drafted (PD-8). The unhappy-path runbook: CI broken mid-release, partial-train failure, manual operator escape via `ExplicitVersionProvider`. Closes PD-8.
5. **First prerelease publish** to nuget.org (#63). Likely `Janset.SDL2.Core 2.32.0-preview.1`, then satellites at their respective `2.x.0-preview.1`. End-to-end real-release walkthrough.

This is where Janset.SDL2 stops being "internal CI artifact" and becomes "consumable .NET ecosystem package". External-consumer friction drops to zero (no PAT, just `dotnet add package Janset.SDL2`).

If Deniz says "go big", pickup B; otherwise default to A. Pickup B should not start before pickup A's multi-platform witness completes — the public-release path is built on the assumption that `--source=remote` works on every host the team actually develops on.

## Recommended Workflow For The Next Agent

1. Read the mandatory grounding below in order.
2. Run `git log --oneline -10` and confirm the doc/prompt close from this session is the most recent commit.
3. Confirm worktree is clean; `gh run view 24962876812 --json conclusion` is `success`.
4. **If pickup A**:
   - Inspect `release.yml` lines around the `publish-staging` `if:` condition and decide gate shape with Deniz.
   - Run `tests/scripts/smoke-witness.cs remote` on Linux + macOS (or arrange for Deniz to run on macOS via SSH) and capture results.
   - Update PD-5 closure across canonical docs as listed above.
5. **If pickup B**:
   - Research-first pass on Trusted Publishing OIDC for nuget.org.
   - Sketch the workflow / Cake-runner shape with Deniz before writing code.
   - Smaller atomic commits than the big PD-5 trio (separate Trusted-Publishing-setup commit, separate `PublishPublicTask` commit, separate workflow commit).

## If Pickup A Lands Cleanly

Move toward pickup B (public-release horizon) as the next session's primary focus. PD-5 multi-platform witness on Linux + macOS is the natural prerequisite — make sure those are green before opening the public-release can.

## Mandatory Grounding (read in this order)

1. `docs/onboarding.md`
2. `AGENTS.md`
3. `docs/plan.md`
4. `docs/phases/phase-2-adaptation-plan.md` (especially PD-5 + PD-7 + PD-8 entries)
5. `docs/phases/phase-2-release-cycle-orchestration-implementation-plan.md`
6. `docs/decisions/2026-04-18-versioning-d3seg.md` (§2.7–§2.8 Artifact Source Profile abstraction)
7. `docs/decisions/2026-04-19-ddd-layering-build-host.md`
8. `docs/decisions/2026-04-20-release-lifecycle-orchestration.md` (especially §6 PD-7 framing)
9. `.github/workflows/release.yml` (publish-staging job + workflow_dispatch inputs)
10. `build/_build/Application/Packaging/RemoteArtifactSourceResolver.cs`
11. `build/_build/Application/Publishing/PublishTaskRunner.cs`
12. `build/_build/Infrastructure/DotNet/NuGetProtocolFeedClient.cs`
13. `docs/playbook/local-development.md` (post-session GH_TOKEN section)
14. `tests/scripts/README.md` (post-session `remote` mode entry)

Live-state snapshots:

- `docs/playbook/cross-platform-smoke-validation.md`
- `docs/playbook/unix-smoke-runbook.md`
- `docs/knowledge-base/cake-build-architecture.md`
- `docs/knowledge-base/release-lifecycle-direction.md`
- `docs/knowledge-base/ci-cd-packaging-and-release-plan.md`

Historical archaeology only when needed:

- `.github/prompts/s16-phase-2b-post-green-ci-pickup.prompt.md`
- `docs/_archive/`

External references for pickup B:

- nuget.org Trusted Publishing docs (NuGet team blog 2024/2025).
- Andrew Lock — "Easily publishing NuGet packages from GitHub Actions with Trusted Publishing" (2025).
- ServiceStack — `gh-nuget` consumer pattern (the closest counter-example for "ship with PAT-based GH Packages consumption"; use as anti-pattern reference for why we go nuget.org instead).

## Locked Policy Recap

These still do not change without explicit Deniz override:

- **Master-direct commits**
- **No commit without approval**
- **Cake remains the policy owner; YAML stays thin**
- **Native asset layout contract lives in `Janset.SDL2.Native.Common.targets` comments**
- **Lock-file strict mode stays scoped to the build host**
- **Do not reintroduce workflow-side win-x86 runtime bootstrap hacks**
- **Do not resurrect stale `runner-os` shape in `vcpkg-setup`**
- **If docs and code disagree, fix the canonical doc in the same change**
- **Commit message style**: short subject + 4-8 short bullets, not prose paragraphs
- **GH Packages NuGet always requires PAT auth** — anonymous read is not supported. Don't try to make external consumers depend on this feed.
- **External consumer feed = nuget.org** (Phase 2b PD-7); GH Packages stays internal CI staging only.
- **`local.*` prerelease versions cannot reach staging feed** — `PublishTaskRunner` guardrail enforces this.
- **`skipDuplicate=false` on push** — re-push at the same version fails loud; operators must bump the prerelease counter.

## Final Steering Note

PD-5 is implementation-closed and witnessed on win-x64 + CI. The natural rhythm for the next session:

- ~30 min on small wins (gate-lift + multi-platform witness + PD-5 doc closures).
- Then talk to Deniz about pickup B scope (public-release horizon is multi-session; consider scoping the first commit narrowly — Trusted Publishing OIDC setup alone is a reasonable first commit).

Don't open with five parallel futures. Pick one of A or B based on Deniz's signal.
