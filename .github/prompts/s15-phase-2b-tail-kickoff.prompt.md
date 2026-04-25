---
name: "S15 Phase 2b tail kickoff (post-Slice-E-follow-up-pass closure)"
description: "Priming prompt for the next agent entering janset2d/sdl2-cs-bindings after the 2026-04-25 Slice E follow-up pass closure + Plan A geniş doc-sweep audit. Slice E is fully closed (P1-P8 + Q17/Q18 ADR-002 §2.3.1 amendment + CMakePresets refactor + drift sweep across canonical docs). The pass-1 ADR-003 merge at `bfc6713` plus the follow-up closure commit close the CI-polish workstream; the immediate horizon is the broader Phase 2b tail. This supersedes s14 — the Slice E follow-up workstream is no longer active."
argument-hint: "Optional focus area, constraints, or Phase 2b sub-item to start with"
agent: "agent"
model: "Claude Opus 4.7 (1M context)"
---

You are a software engineer entering `janset2d/sdl2-cs-bindings` after the **Slice E follow-up pass closed (2026-04-25)** and the broader **Plan A geniş doc-sweep audit** landed alongside it. Phase 2b's tail is the active workstream; the CI-polish work and the ADR-003 implementation pass are both behind us.

This prompt supersedes `.github/prompts/s14-slice-e-ci-polish-continuation.prompt.md`. Read s14 only if you need archaeology on a specific Slice E P1-P8 commit.

## First Principle

Treat every claim here as **current-as-of-authoring (2026-04-25)** and **verify against the live repo, git log, and canonical docs before acting**. The Slice E closure commit just landed an extensive doc audit, so the canonical-doc surface is unusually well-aligned with code right now — but drift starts the moment new work begins.

If code and docs disagree, trust the code, then either fix the doc or note the drift visibly. The Plan A geniş audit set the precedent: when a doc has ≥50% amendment surface, archive the original to `docs/_archive/<name>-<YYYY-MM-DD>.md` and rewrite in place; for smaller drift, surgical edits are fine.

This pass continues to run as **master-direct commits** per Deniz direction. Do not create a branch unless there is a concrete reason.

## Onboarding Snapshot

This repo is the packaging/build/distribution layer for **modular C# SDL2 bindings first, SDL3 later**, with **native libraries built from source via vcpkg** and shipped through **NuGet**. The build backbone:

- `.NET 9 / C# 13`
- `Cake Frosting` build host under `build/_build/` (DDD-layered per ADR-002)
- `vcpkg` for native builds (custom `*-hybrid` overlay triplets)
- `GitHub Actions` for the RID matrix + release pipeline + builder image
- `build/manifest.json` schema v2.1 as the single source of truth

Locked strategic decisions you should not casually reopen (full list in `AGENTS.md`):

- **Hybrid static + dynamic core** packaging model
- **Triplet = strategy** (no `--strategy` CLI flag; manifest `runtimes[].strategy` is the formal mapping)
- **Package-first consumer contract** (per ADR-001)
- **Cake owns orchestration policy; YAML stays thin** (CI workflow files are dispatch + artifact-flow only; every policy-bearing decision lives in the build host)
- **7 RID coverage in scope**, all hybrid-static post-PA-2 (2026-04-18)
- **LGPL-free codec stack** for SDL2_mixer (minimp3 / drflac / libmodplug / Timidity / Native MIDI; mpg123 / libxmp / FluidSynth deliberately out)

Target RIDs (canonical in `build/manifest.json runtimes[]`):

- `win-x64` / `win-x86` / `win-arm64` (all `*-windows-hybrid`)
- `linux-x64` / `linux-arm64` (all `*-linux-hybrid`, run on `ghcr.io/janset2d/sdl2-bindings-linux-builder:focal-latest`)
- `osx-x64` (Intel) / `osx-arm64` (Apple Silicon, all `*-osx-hybrid`)

## Where the Pass Is

- **Master HEAD**: filled in at commit + push time (last commit before this prompt: Slice E follow-up pass closure commit covering P8.3-P8.5 + Plan A geniş doc-sweep audit). Pass-1 ADR-003 merge baseline is `bfc6713` (2026-04-22).
- **Current phase**: **Phase 2b**. Slice E follow-up pass officially closed; CI-polish workstream done.
- **Build-host tests**: 460/460 green on the Windows host; matches the closure-commit-time baseline.
- **Last full CI run from the prior session**: `release.yml` run 24932894291 — all 7 RIDs green end-to-end (Pack ✓ + ConsumerSmoke ✓ across `win-{x64,x86,arm64}` / `linux-{x64,arm64}` / `osx-{x64,arm64}`).
- **Local witnesses (prior session)**:
  - Windows `win-x64`: A-K checkpoints all green at master `d190b5b` (Build.Tests 460/460, NativeSmoke 29/29, Inspect-Deps no leaked codecs, SetupLocalDev 15 nupkgs in 41.8s, PackageConsumerSmoke 12+12+11 = 35/35 PASS across `net9.0`/`net8.0`/`net462`).
  - WSL Linux `linux-x64`: same shape, MIDI decoder fonctional via builder image's `freepats`, PackageConsumerSmoke 12+12 = 24/24 PASS (`net462` correctly auto-skipped per Mono+TUnit incompat).
  - macOS deferred to CI per Deniz direction (no local Mac hardware available).

## What The Slice E Follow-Up Pass Actually Closed

Eight phases plus a comprehensive doc-sweep audit. The phase plan (`docs/phases/phase-2-release-cycle-orchestration-implementation-plan.md` §2.2) carries the per-phase SHA + summary trail; this section is the executive view.

### CI infrastructure (P1-P4 + P4e)

- `release.yml` matrix jobs absorbed the full prepare-native-assets-* discipline via three composite actions: `vcpkg-setup` (cache identity + bootstrap + install with mutable-tag-to-immutable-digest resolution for GHCR images), `nuget-cache` (cross-OS workspace cache), `platform-build-prereqs` (macOS brew per-formula idempotent; Linux/Windows no-ops because the GHCR builder image + `IMsvcDevEnvironment` cover those surfaces).
- Cake host published as a single FDD artifact per pipeline run; every consumer downloads `cake-host` and invokes `dotnet ./cake-host/Build.dll` instead of per-job `dotnet restore/build`.
- Harvest + NativeSmoke collapsed to one matrix job (NativeSmoke runs inline after Harvest per Deniz direction "NativeSmoke is Harvest's guardrail").
- P4e hygiene table inline in `release.yml` header documents which composites land on which jobs and why (FDD-only consumer jobs drop nuget-cache; consumer-smoke drops platform-build-prereqs; setup-dotnet kept everywhere for cross-runner SDK pinning via `global.json`).

### buildTransitive native asset placement (`b9f63f4` + `357abe4`)

- `src/native/_shared/Janset.SDL2.Native.Common.targets` is now the **canonical reference** for the consumer-side native asset placement contract — its in-file comment block is the authoritative explanation. Two complementary modes: **portable build** (RuntimeIdentifier empty) extracts into `$(OutDir)runtimes/<rid>/native/` (nested, default-handler probing path); **RID-specific build** (`-r <rid>` set, e.g., `dotnet test -r linux-x64`, self-contained publish) extracts flat into `$(OutDir)`.
- .NETFramework AnyCPU consumers no longer need `<PlatformTarget>x64</PlatformTarget>` workaround. The `_JansetSdlNet4xCopyRid` resolution at target evaluation time consults `Platform` / `Prefer32Bit` / `OSArchitecture` and **deliberately ignores `$(RuntimeIdentifier)`** — SDK auto-x86 inference and user-explicit `-r win-x86` produce identical MSBuild property states, so RuntimeIdentifier is unreliable as user-intent signal. `PackageConsumerSmokeRunner.AppendNet4xPlatformArgument` forwards `-p:Platform=<arch>` for `net4*` TFMs alongside `-r <rid>` so smoke runner intent maps to the explicit-Platform priority.

### P5 lock-file discipline (4-commit sequence)

- `<RestorePackagesWithLockFile>true</RestorePackagesWithLockFile>` on 12 in-scope csprojs (build/_build + build/_build.Tests + 5 src/SDL2.* + 5 src/native/SDL2.*.Native). 12 `packages.lock.json` committed.
- **Strict mode (`<RestoreLockedMode Condition="'$(GITHUB_ACTIONS)'='true'">true</...>`) only on the build host** (`build/_build/Build.csproj` + `build/_build.Tests/Build.Tests.csproj` — single-TFM net9.0, bounded package surface). `src/` stays on lenient mode.
- Three CI failures shaped this scope: NU1004 on `Microsoft.NETFramework.ReferenceAssemblies` (Windows-vs-Linux SDK-implicit asymmetry — Windows ships local v4.6.2 ref assemblies, Linux/macOS auto-pull the package), NU1004 on `Microsoft.NET.ILLink.Tasks` (runtime-patch asymmetry — package ships per .NET runtime patch, drifts past local SDK every Microsoft cadence), NU1009 on `Microsoft.NETFramework.ReferenceAssemblies` (the explicit pin from the first NU1004 fix collided with Linux SDK's implicit-reference marker on every csproj because CPM is root-level).
- Final shape: lock files committed everywhere for PR-diff visibility, strict mode only where the package surface is bounded and not patch-driven, no explicit pins for SDK-implicit packages. `src/` lenient mode absorbs both host-OS and runtime-patch asymmetries automatically.
- **Memory rule:** [Lock-File Strict-Mode Scope Discipline](file:./../../C:/Users/deniz/.claude/projects/e--repos-my-projects-janset2d-sdl2-cs-bindings/memory/feedback_lockfile_cross_platform_discipline.md) — strict mode only on tightly-scoped, single-TFM build-host csprojs; multi-TFM library projects with `IsTrimmable=true` keep lock files for visibility but skip strict mode.

### P6 PublishTask scaffolding stubs

- `Application/Publishing/PublishTaskRunner.cs` (concrete; no interface per AGENTS.md interface discipline — single impl, single caller surface today). Throws `NotImplementedException` with Phase-2b pointer.
- `Tasks/Publishing/PublishStagingTask.cs` + `PublishPublicTask.cs` (golden thin adapters delegating to runner).
- `release.yml` `publish-staging` / `publish-public` jobs stay gated `if: false` until Phase 2b lands real feed transfer.
- `[SuppressMessage("Design", "MA0025")]` on `RunAsync` — `NotImplementedException` is semantically correct (planned, not unsupported).

### P7 three-platform witness

- A-K formal walkthrough green on **Windows + WSL Linux** at master `d190b5b`. macOS deferred to CI per Deniz direction (no local Mac hardware available).
- **WSL gotcha caught + documented**: `appendWindowsPath=true` default puts `/mnt/c/Program Files/dotnet` ahead of `~/.dotnet` → Cake host starts on Linux dotnet but child `dotnet pack` picks Windows dotnet via naked PATH lookup → MSBuild Windows fails `MSB1001` on Linux paths. D-G checkpoints unaffected (Cake's `IPathService` resolution doesn't go through naked PATH); J/K need Linux-only PATH override (`export PATH="$HOME/.dotnet:/usr/local/sbin:/usr/local/bin:/usr/sbin:/usr/bin:/sbin:/bin"`). Documented in `cross-platform-smoke-validation.md` §"WSL / Linux" + `unix-smoke-runbook.md` §0.

### P8 retirement + Q17/Q18 + Plan A geniş doc-sweep audit

- **P8.1 retire 5 YAMLs**: `prepare-native-assets-{linux,macos,windows,main}.yml` + `release-candidate-pipeline.yml` deleted (637 deletions). Retained workflows: `release.yml` + `build-linux-container.yml`.
- **P8.2 .NETFramework AnyCPU consumer guideline** added to `cross-platform-smoke-validation.md` (resolution priority table + three opt-in patterns + smoke runner pointer + Common.targets canonical reference declaration).
- **Q17 closed via ADR-002 §2.3.1 amendment**: delegate-hook test pattern formalized for non-mockable third-party boundaries (Cake.Frosting.Git/LibGit2Sharp bypassing `ICakeContext.FileSystem`). Four invariants + counter-rule (≥3 callsites or clustering promotes to a real interface or `Build.Tests/Fixtures/` extension).
- **Q18 closed via CMakePresets refactor**: `tests/smoke-tests/native-smoke/CMakePresets.json` reduced 21→14 buildPresets (drop `*-interactive` variants). New `CMakeUserPresets.json.example` template ships interactive entries for every RID; `.gitignore` excludes the user file; `tests/smoke-tests/native-smoke/README.md` rewritten around UserPresets opt-in flow per official `cmake-presets(7)` recommendation.
- **Plan A geniş doc-sweep audit**: post-Cake-+-CI hatm comparison of code-current state vs each canonical doc. Substantial rewrites (≥50% amendment threshold per Deniz direction): `docs/knowledge-base/ci-cd-packaging-and-release-plan.md` (484L → live-state pointer + Phase 2b/3 backlog index, originals carried as `known-issues.json`/`force_build_strategy`/`Promote-To-Public`/`PR-Version-Consistency` ideas), `docs/playbook/ci-troubleshooting.md` (177L → release.yml + build-linux-container.yml topology + carry-forward unique tips: mpg123 overlay, autoconf 2.72 rationale, vcpkg cache key debug, macOS Xcode/Homebrew). Both originals archived to `docs/_archive/<name>-2026-04-25.md`. Surgical edits across `cake-build-architecture.md` (folder tree + DI table refresh; `Tasks/Publishing` + 5 new Application namespaces + Domain/{Versioning,Publishing,Runtime/MsvcTargetArch} + Infrastructure/Tools/{NativeSmoke,Tar,Msvc} + DotNet/DotNetRuntimeEnvironment), `onboarding.md` (state refresh: 460 tests, runner labels, GHCR builder, What Works/Doesn't Work rewritten), `AGENTS.md` (7-RID triplet table all-hybrid, schema v2.1 + package_families[], runtimes.json drop, fat-task drift removal, NEXT WORK ITEM Cake-strategy-refactor pointer retired), `README.md` (Build Status badge release-candidate → release.yml, Platform Support Functional, mpg123 → minimp3 LGPL-free alignment, Project Status refreshed). Minor edits across `cross-platform-smoke-validation.md`, `local-development.md`, `release-lifecycle-direction.md`, `release-guardrails.md`, `unix-smoke-runbook.md`.
- **Skip per Deniz direction**: `docs/_archive/`, `docs/research/` (date-stamped historical), `docs/reviews/`, retire-to-stub phase docs.

## Important Drift Note

The doc-sweep audit just landed, so the canonical-doc surface is exceptionally clean right now. Two surfaces remain pre-existing minor drift (not closure-blocking, fold into Phase 2b doc updates if you touch them):

1. `docs/plan.md` line 299 has an MD056 markdownlint warning (`#58 Add SDL2_net binding` row carries 3 cells in a 2-cell table). Pre-existing, surgical fix when convenient.
2. `docs/plan.md` + `docs/phases/phase-2-release-cycle-orchestration-implementation-plan.md` cache-identity drift carried over from `s13` is **resolved as of P1-P4** (the `vcpkg-setup` composite now derives an immutable platform-identity by resolving the GHCR mutable tag's per-platform child manifest digest inline before computing the cache key). Verify via the live `action.yml` if you touch the cache surface.

If you discover new drift while doing Phase 2b work, **fix the canonical doc in the same change** — the Living Docs rule from AGENTS.md applies.

## Current Workflow / Manifest Reality

- `release.yml` runs the 10-job topology described in the inline header comments; matrix shape comes from `manifest.runtimes[]` (single source of truth — never hardcode).
- `build-linux-container.yml` builds the multi-arch GHCR builder image (`focal-<yyyymmdd>-<sha>` + `focal-latest`) on `workflow_dispatch` + monthly cron. Manifest references the mutable `:focal-latest` tag; `vcpkg-setup` resolves it to immutable per-platform digests inline for the cache key.
- `consumer-smoke` matrix re-enters by RID. `win-x86` runtime bootstrap is Cake-owned (`IDotNetRuntimeEnvironment` injects `DOTNET_ROOT_X86` only into child `dotnet test` invocations). Smoke runner forwards `-p:Platform=<arch>` for `net4*` TFMs.
- `publish-staging` / `publish-public` jobs gated `if: false` — `PublishTaskRunner` Cake stub throws `NotImplementedException` with Phase-2b pointer until real feed transfer logic lands.

## Immediate Roadmap Tail (Phase 2b)

Per `docs/plan.md`, the next big work items now that Slice E is closed:

| Item | Description | Status |
| --- | --- | --- |
| **PA-2 behavioural validation** | `workflow_dispatch` on the 4 newly-hybridized rows (`win-arm64`, `win-x86`, `linux-arm64`, `osx-arm64`); `mode=manifest-derived`, `suffix=pa2.<run-id>`; failure triage per playbook PA-2 section. Mechanism is in place (release.yml run 24932894291 already greened all 7 RIDs); the **formal witness pass with full guardrail-trail recording** remains. | Not started |
| **`release.yml` real release-tag hardening** | Currently the workflow runs end-to-end on `workflow_dispatch` and tag pushes, but `publish-staging` / `publish-public` are gated `if: false`. Phase 2b needs the real `PublishTaskRunner` body (NuGet push to staging feed → validation → promote to public feed) + auth surface (GitHub Packages staging + nuget.org public feed). | Not started — `PublishTaskRunner` is a stub |
| **`RemoteArtifactSourceResolver` (PD-5)** | Concrete impl for `SetupLocalDev --source=remote` — internal feed URL convention, auth pattern, cache strategy. Currently `UnsupportedArtifactSourceResolver` registered for `--source=remote`/`--source=release` profiles. Needs to operate on all 3 host platforms. | Not started |
| **Family / train orchestration follow-through (PD-7 / PD-8 closure deliverables)** | Direction selected in ADR-003; formal closure waits for: (a) `playbook/release-recovery.md` (PD-8 manual escape) + a real recovery scenario validated end-to-end; (b) full-train meta-tag mechanism validated with `manifest.package_families[].depends_on` topological ordering on a real multi-family release scenario. | Not started |
| **First real prerelease publication** | Once `PublishTask` is real and `RemoteArtifactSourceResolver` works, push the first prerelease set to a staging feed, validate consumer install end-to-end, then promote a subset to nuget.org. | Not started — gated on Publish + RemoteResolver |
| **PA-2 sub-items (PD-14 Linux MIDI packaging, PD-15 sdl2-gfx Unix visibility)** | Separate decision threads, lower priority but in Phase 2b scope. | Not started |

Order is suggestive, not prescriptive — pick what fits the session you're in.

## Mandatory Grounding (read in this order)

1. `docs/onboarding.md` — project overview + What Works/Doesn't Work refresh just landed
2. `AGENTS.md` — operating rules + 7-RID triplet table just refreshed all-hybrid
3. `docs/plan.md` — current status + Q3 2026 roadmap CMakePresets/CI rewrite [x]
4. `docs/phases/phase-2-release-cycle-orchestration-implementation-plan.md` — §2.2 P5-P8 status + §11 Q17 + Q18 closed list + §14 v2.17 change log
5. `docs/decisions/2026-04-18-versioning-d3seg.md` — ADR-001 D-3seg + Artifact Source Profile
6. `docs/decisions/2026-04-19-ddd-layering-build-host.md` — ADR-002 + new §2.3.1 delegate-hook amendment
7. `docs/decisions/2026-04-20-release-lifecycle-orchestration.md` — ADR-003 stage-owned validation + 3 providers + matrix re-entry
8. `.github/workflows/release.yml` — 10-job topology + inline P4e hygiene table
9. `.github/workflows/build-linux-container.yml` + `docker/linux-builder.Dockerfile` — multi-arch GHCR builder
10. `.github/actions/{vcpkg-setup,nuget-cache,platform-build-prereqs}/action.yml` — composite actions
11. `build/manifest.json` — schema v2.1 single source of truth
12. `src/native/_shared/Janset.SDL2.Native.Common.targets` — **canonical reference** for native asset placement contract
13. `build/_build/Application/Packaging/PackageConsumerSmokeRunner.cs` — smoke runner (note `AppendNet4xPlatformArgument`)
14. `build/_build/Infrastructure/DotNet/DotNetRuntimeEnvironment.cs` — win-x86 child runtime bootstrap

Live-state snapshots (look here before guessing):

- `docs/knowledge-base/cake-build-architecture.md` — folder tree + DI table just refreshed
- `docs/knowledge-base/ci-cd-packaging-and-release-plan.md` — live-state pointer + Phase 2b/3 backlog index
- `docs/playbook/cross-platform-smoke-validation.md` — A-K checkpoint command reference
- `docs/playbook/unix-smoke-runbook.md` — Windows + WSL + macOS witness (with WSL Linux-only PATH gotcha)

Historical archaeology only when needed:

- `.github/prompts/s14-slice-e-ci-polish-continuation.prompt.md` — Slice E follow-up pass kickoff (closed)
- `docs/_archive/` — pre-rewrite snapshots of substantially-rewritten canonical docs
- `.github/prompts/s13-...` and earlier — older priming context

## Locked Policy Recap

These do not change without an explicit Deniz override:

- **Master-direct commits.** No branch unless there's a concrete reason.
- **No commit without Deniz approval.** Diff summary + proposed commit message → wait for ack.
- **Cake remains the policy owner; workflow YAML stays thin.** Every policy-bearing decision lives in the build host. New "Cake CLI flag for X" requests should challenge whether YAML-thin is broken before introducing a new flag.
- **Native asset layout contract lives in `Janset.SDL2.Native.Common.targets` in-file comments.** Doc surface follows the file, not vice versa.
- **For .NETFramework AnyCPU consumer behaviour**, the `_JansetSdlNet4xCopyRid` resolution is intentionally Platform / Prefer32Bit / OSArchitecture-driven — `$(RuntimeIdentifier)` is unreliable due to SDK auto-x86 inference indistinguishability. Do not add a "Priority 0: trust RuntimeIdentifier" branch unless you have a way to detect SDK-auto-set vs user-explicit that the prior session missed.
- **Lock-file scope (P5)**: `build/**` + `src/**` only. Smoke / dynamic-version consumers under `tests/smoke-tests/` are explicitly excluded. Strict mode only on the build host; `src/` stays lenient to absorb SDK-implicit-package drift.
- **Do not reintroduce inline workflow PowerShell for win-x86 runtime bootstrap.** Cake-owned via `IDotNetRuntimeEnvironment`.
- **Do not resurrect stale `runner-os` input shape in `vcpkg-setup`.** `runner.os` branching is the current shape.
- **Doc convention**: `docs/_archive/<original-name>-<YYYY-MM-DD>.md` for pre-rewrite snapshots (≥50% amendment threshold). Archive index lives at `docs/_archive/README.md`. ADR-002 §2.3 interface discipline (3 criteria + delegate-hook §2.3.1 amendment) governs new build-host seams.
- **If docs and code disagree, verify then fix the doc drift in the same change.**
- **Commit message style**: short subject + 4-8 short bullets in the body, not paragraph narratives. Memory: [Short Bullet-Point Commit Messages](file:./../../C:/Users/deniz/.claude/projects/e--repos-my-projects-janset2d-sdl2-cs-bindings/memory/feedback_short_commit_messages.md).

## Suggested Re-entry Point (with freedom)

The phase plan §2.2 P1-P8 table is closed; the broader Phase 2b roadmap is open. Below is a short per-item entry sketch — but **you have freedom to start anywhere on the Phase 2b roadmap that Deniz directs, or propose a different reordering with rationale**. None of these is a "next thing must be" — they're parallel candidates.

### A. PA-2 behavioral witness pass (lower-friction starter)

The mechanism is in place; the witness recording isn't. If Deniz wants a clean witness-trail for the four newly-hybridized rows:

1. `workflow_dispatch` on `release.yml` with `mode=manifest-derived` + `suffix=pa2.<run-id>`.
2. Capture per-RID Pack + ConsumerSmoke evidence per `cross-platform-smoke-validation.md` PA-2 section.
3. Update `cross-platform-smoke-validation.md` Last-validated header + log the witness dates per RID.
4. If any RID surfaces a new failure mode, file a `docs/research/` note before triaging.

Friction: low (no code change, just CI dispatch + docs).

### B. `PublishTaskRunner` real implementation (medium-high friction; gated on auth setup)

Currently throws `NotImplementedException`. To unlock the first prerelease publication:

1. Decide the staging feed: GitHub Packages (per-org NuGet feed; auth via `GITHUB_TOKEN`) is the cheapest first cut. nuget.org public feed promotion is a separate workflow per ADR-003 §3.2.
2. Implement `PublishTaskRunner.RunAsync` to honour `PublishRequest.PackagesDir` + `FeedUrl` + `AuthToken`. Use `dotnet nuget push` via `IDotNetPackInvoker`-style wrapper (or a sibling `IDotNetNuGetInvoker`).
3. Wire `release.yml` `publish-staging` job: download `nupkg-output` artifact, invoke `PublishStaging` with feed URL + secret-injected auth token, gate on `consumer-smoke` matrix completion.
4. `publish-public` stays gated until manual promotion. Decide manual-trigger shape (separate workflow per `Promote-To-Public` backlog idea? Inline `workflow_dispatch` input on release.yml?).

Friction: medium-high (needs feed auth secrets configured + a real test publish target).

### C. `RemoteArtifactSourceResolver` concrete (medium friction; orthogonal to publish)

`SetupLocalDev --source=remote` accepted but stubbed. To make it operational:

1. Decide the remote feed convention: internal/staging feed URL stored where (manifest? env var? secret?). PD-5 is the open thread.
2. Implement download + cache strategy (use NuGet client API or shell out to `dotnet nuget` — pick one and document).
3. Replace `UnsupportedArtifactSourceResolver` registration with the real impl when `--source=remote`.
4. Validate on all 3 host platforms (Windows + WSL + macOS).

Friction: medium. Self-contained; doesn't require the publish surface to be real yet.

### D. Family / train orchestration formalization (paper-heavier, code-lighter)

PD-7 (full-train) + PD-8 (manual escape) directions are selected in ADR-003 but formal closure needs:

1. `playbook/release-recovery.md` walking through a real PD-8 recovery scenario (operator runs `--target Pack --explicit-version <family>=<semver>-hotfix.1` outside CI, validates, pushes to staging feed manually).
2. Full-train meta-tag mechanism validated against `manifest.package_families[].depends_on` topological ordering. Probably needs a real multi-family release scenario to exercise.

Friction: low-medium on the doc side, gated on B+C for the actual exercise.

### E. Drift cleanup work (cosmetic / nice-to-have)

- `docs/plan.md` line 299 MD056 fix.
- AGENTS.md / onboarding.md surface drift not covered by the closure-commit Plan A audit (the audit was the canonical scope; deeper passes can land if a reader spots specific drift).
- Any other Phase 2b adjacent drift discovered while doing A-D.

Friction: trivial. Good warm-up if the session opens cold.

---

**Pick whichever makes sense for the session Deniz wants.** Start by reading the mandatory grounding above, run `git log --oneline -20` to verify current master HEAD, and report what you see before proposing direction. The closure commit gave us a clean base; the next move is up to the work you and Deniz prioritize.
