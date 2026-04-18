# Research: Release Recovery + Manual Escape Hatch

**Date:** 2026-04-16
**Status:** Roadmap placeholder — research pending. **Amended 2026-04-18 (ADR-001):** family versioning adopted D-3seg shape + Cake `IArtifactSourceResolver` abstraction. See "ADR-001 addendum" below. **Prior amendment 2026-04-17 (S1):** within-family dependency contract changed from exact pin to minimum range. See "S1 addendum" below.
**Context:** Stream D-ci sibling of PD-7. PD-8 open. See [phase-2-adaptation-plan.md](../phases/phase-2-adaptation-plan.md) Pending Decisions.
**Prerequisite reading:** [release-lifecycle-direction.md](../knowledge-base/release-lifecycle-direction.md), [ADR-001: D-3seg Versioning](../decisions/2026-04-18-versioning-d3seg.md), [full-train-release-orchestration-2026-04-16.md](full-train-release-orchestration-2026-04-16.md)

---

> **ADR-001 addendum (2026-04-18).** Two changes touch PD-8's design space:
>
> 1. **D-3seg versioning.** Family versions are now `<UpstreamMajor>.<UpstreamMinor>.<FamilyPatch>` (e.g. `sdl2-core-2.32.0`). Example version strings in this doc (`sdl2-image-1.3.0`, `sdl2-core-2.0.0`, etc.) are illustrative — mentally substitute the D-3seg shape. Manual escape-hatch Cake helpers (`Pack-Family`, `Push-Family`) must accept + validate the D-3seg-shaped `--version` input and enforce G54 (UpstreamMajor.Minor ↔ manifest coherence) + G55/G56/G57 (post-pack metadata, satellite upper bound, README mapping table) at pack time — same gates that the CI flow runs.
>
> 2. **Artifact Source Profile abstraction.** Stream D-local now exposes `IArtifactSourceResolver` + `ArtifactProfile { Local, RemoteInternal, ReleasePublic }` (ADR-001 §2.7). The manual escape hatch fits naturally as a `--profile=ReleasePublic` invocation with an explicit operator-supplied API key + target feed. The interface is locked in Phase 2a; `ReleasePublic` concrete impl is a PD-8 deliverable. No separate "manual vs automated" abstraction layer — same resolver contract, different origin (CI workflow vs operator CLI).
>
> **PD-8 scope is otherwise unchanged.** The seven research questions (Cake helpers, API key provisioning, smoke-test-as-manual-gate, partial-train recovery, tag hygiene, auditability, industry precedents) remain open. PD-12 in the adaptation plan records the ADR-001 decision.

---

> **S1 addendum (2026-04-17).** This research note was drafted when within-family dependencies were expected to be exact-pinned (`[x.y.z]`) via Mechanism 3. S1 adoption retired that requirement; within-family is now minimum range (`>=`), matching cross-family. References in this doc to "within-family exact pin" / `[x.y.z]` / sentinel `0.0.0-restore` / `AllowSentinelExactPin=true` are **historical** and should be read as describing the pre-S1 contract. The manual escape hatch still mirrors the CI flow step-for-step, but the property set passed to each `dotnet pack` invocation is simpler: `-p:Version=X -p:NativePayloadSource=<root>` per pack, no per-family version property or sentinel-related flags. PD-11 in the adaptation plan records the S1 decision. Core PD-8 scope (seven research questions around Cake helpers, API key provisioning, audit trail, partial-train recovery) is unaffected by S1 — it's about operator workflow, not dependency semantics.

---

## 1. Problem Statement

Both targeted (per-family) and full-train release pipelines are GitHub-driven by design — tag push (or meta-tag push) fires CI, CI orchestrates restore + pack + smoke + publish. This is the happy path.

**The unhappy path is unspecified today.** Concrete scenarios:

1. **CI is broken** — GitHub Actions outage, runner pool exhaustion, transient infra failure. Family tag pushed but no pipeline fires (or pipeline starts and dies mid-run).
2. **Release-set workflow is broken** — full-train meta-tag fires the orchestrator workflow, workflow itself has a bug, no family tags get created.
3. **Mid-train partial failure** — Core publishes successfully, satellite pipeline crashes during pack. Three published packages, two non-shipped. Pipeline can't resume cleanly.
4. **Emergency hotfix needed faster than CI cycle** — security disclosure, critical bug, NuGet.org compromise. Need to publish from a developer machine to `internal feed` or even `nuget.org` faster than the pipeline allows.
5. **Internal feed is unavailable but NuGet.org direct push is needed** — staging stage broken, but downstream consumers blocked.
6. **Pipeline produced wrong artifact and we need to manually correct** — wrong version emitted, wrong dependency range, wrong metadata. Need to repackage + republish without going through full pipeline again.

For each scenario, the project needs a documented **manual escape hatch** that operators can execute by hand without depending on the broken automation.

## 2. Canonical Constraints (Locked, Not Open)

The escape hatch must operate within these locked decisions:

| Constraint | Source |
| --- | --- |
| Historical pre-S1 within-family exact pin `[x.y.z]`, cross-family minimum range `>= x.y.z` | release-lifecycle-direction.md §4 |
| Family tag is the source of truth for versioning | release-lifecycle-direction.md §1, §3 |
| Core releases first, satellites after | release-lifecycle-direction.md §1 (Release Ordering) |
| All public releases pass through internal feed first | release-lifecycle-direction.md §6 |
| Cake is the single orchestration surface | adaptation plan + plan.md |
| Family identifier convention: `sdl<major>-<role>` | release-lifecycle-direction.md §1 |

Manual escape hatches honor these — they do not let an operator bypass the dependency contract or skip the internal feed for production releases.

## 3. Two Escape Hatch Categories

### 3.1 Individual package manual release

Operator publishes a single family (managed + native pair) without going through CI.

**Mechanism:**

1. Operator creates the family tag locally if not already pushed: `git tag sdl2-image-1.3.0`.
2. Operator runs the equivalent of what Cake `PackageTask` would have run:
   - Pre-build native csproj with explicit version: `dotnet build src/native/SDL2.Image.Native/SDL2.Image.Native.csproj -p:Version=1.3.0 -p:Sdl2ImageFamilyVersion=1.3.0 -p:MinVerSkip=true -c Release`.
   - Restore + pack managed csproj as two steps: `dotnet restore src/SDL2.Image/SDL2.Image.csproj -p:Version=1.3.0 -p:Sdl2ImageFamilyVersion=1.3.0 -p:MinVerSkip=true`; `dotnet pack --no-restore src/SDL2.Image/SDL2.Image.csproj -c Release -p:Version=1.3.0 -p:Sdl2ImageFamilyVersion=1.3.0 -p:MinVerSkip=true -o artifacts/packages/`.
3. Operator pushes to internal feed first: `dotnet nuget push artifacts/packages/Janset.SDL2.Image.1.3.0.nupkg --source <internal-feed> --api-key <key>` (and same for `.snupkg`).
4. Operator runs the package-consumer smoke test against the internal feed.
5. Only if smoke passes, operator pushes to public NuGet.org: `dotnet nuget push ... --source nuget.org --api-key <key>`.
6. Operator pushes the family tag to remote: `git push origin sdl2-image-1.3.0`.

**Key principle:** the manual flow mirrors the CI flow step-for-step. Same restore + pack + smoke + publish sequence. Same historical exact-pin properties in the pre-S1 version of this flow. Same internal-feed-then-public promotion. The difference is who runs each step (human vs workflow).

**Cake helper for this:** ideally Cake exposes a `Pack-Family` target so the operator runs `dotnet cake --target=Pack-Family --family=sdl2-image --version=1.3.0` instead of remembering all the property flags. This is a Stream D-local deliverable — not strictly required for the escape hatch to exist, but reduces "human typing the wrong flag" risk.

### 3.2 Full-train manual release

Operator publishes a coordinated multi-family release without going through the meta-tag orchestrator workflow.

**Mechanism:**

1. Operator drafts (or already has) a `release-set.json` describing the train.
2. Operator creates each family tag locally: `git tag sdl2-core-2.0.0 sdl2-image-1.3.0 sdl2-mixer-1.2.0 sdl2-ttf-1.1.0 sdl2-gfx-1.0.5`.
3. Operator runs the per-family escape hatch (§3.1) for the **core family first**, end-to-end (build → pack → internal feed → smoke → public push). Only proceeds when core is on the public feed.
4. Operator runs the per-family escape hatch for each satellite family in any order (parallel-friendly), each end-to-end.
5. Operator pushes all family tags to remote together: `git push origin sdl2-core-2.0.0 sdl2-image-1.3.0 ...`.
6. Operator creates the GitHub Release manually pointing at a meta-tag (e.g., `train-2026.04.17`) with consolidated release notes.

**Key principle:** the full-train manual flow is the per-family flow run sequentially with respect to release ordering. No new mechanism — just N invocations of §3.1 with operator coordinating.

## 4. Questions the Research Must Answer

### 4.1 Which Cake targets land first as escape-hatch surface?

Minimum viable Cake surface to make the escape hatch tractable:

- `Pack-Family --family=<id> --version=<semver>` — produces both managed + native nupkg + snupkg.
- `Smoke-Family --family=<id> --version=<semver>` — runs the package-consumer smoke test against a specified feed source.
- `Push-Family --family=<id> --version=<semver> --source=<feed>` — publishes managed + native to the named feed.

Should these be defined now (Stream D-local minimum scope for escape hatch) or only when CI workflow is broken?

### 4.2 NuGet API key provisioning

Where do operators get the API keys? Personal access tokens? Shared keys? Vault? Per-feed?

- **Internal feed** (e.g., GitHub Packages) — likely each maintainer has their own GitHub PAT.
- **Public NuGet.org** — single project key vs per-maintainer keys.

Establish the policy + document where keys live.

### 4.3 Smoke test as manual gate

Manual flow per §3.1 step 4: operator must run smoke before publishing. What are the exact steps?

- Where does the consumer test project live?
- How does the operator point it at the internal feed?
- What does "pass" look like in the absence of CI green check?

### 4.4 Recovery from partial-train failure

Concrete: core-2.0.0 published to public, image-1.3.0 published to internal but failed smoke, mixer-1.2.0 not even built yet.

- Do we proceed (fix image, ship mixer, leave gap)?
- Do we hold (delay everything until image fixed, then re-trigger train)?
- Do we roll back (`dotnet nuget delete` core, restart)?

Answer needs to be documented per scenario.

### 4.5 Tag hygiene under manual escape

If the operator pushes `sdl2-image-1.3.0` but pack fails, the tag is now in remote git history without a corresponding published nupkg. Future MinVer runs will resolve this tag.

- Do we delete the bad tag (`git push --delete origin sdl2-image-1.3.0`)?
- Do we reuse the tag (force-push, dangerous)?
- Do we leave it and skip to `1.3.1` (preferred — tags are immutable in spirit)?

### 4.6 Auditability of manual releases

CI-driven releases have GitHub Actions logs as audit trail. Manual releases don't. How do we record:

- Who ran the manual release
- What commit was packed
- What flags were passed
- What the produced nupkg's hash was
- When it landed on each feed

Solution candidates: post-hoc annotation on the family tag (annotated tag with release metadata), separate manifest file appended to repo, GitHub Release object created after the fact.

### 4.7 Industry precedents

Surveys to do:

- **NuGet.org** itself — emergency package publish/unpublish protocols.
- **dotnet/aspnetcore** — internal release-recovery playbook (probably not public, but design hints in their docs).
- **SkiaSharp** — manual override workflows.
- **MinVer** itself — when MinVer needs a hotfix, how does Adam Ralph release it manually?

## 5. Decision Criteria

When the research lands a recommended manual escape hatch, evaluate against:

| Criterion | Weight |
| --- | --- |
| Honors all canonical constraints from §2 | Hard requirement |
| Mirrors CI flow step-for-step (no semantic divergence) | Hard requirement |
| Documented step-by-step playbook (operator can execute without re-reading code) | Hard requirement |
| Audit trail captured (who, what, when, hash) | Hard requirement |
| Recovery from partial-train failure documented | Hard requirement |
| Cake helpers exist to reduce typo risk | Strong preference |
| API key/auth model documented | Hard requirement |

## 6. Non-Goals

Out of scope for this research:

- Replacing the CI flow with manual escape (CI is the primary path, manual is fallback only).
- Bypassing the historical pre-S1 dependency contract (within-family exact pin, cross-family minimum, internal-then-public promotion stay locked).
- Designing a "permanent manual mode" — manual is recovery, not steady state.

## 7. Relationship to A-risky and Stream D-local

Historical pre-S1 note: A-risky landed the csproj shape and the MSBuild guard target (blocks shipping `0.0.0-restore` sentinel). The escape hatch can already be exercised TODAY for individual families using the manual `dotnet build` + `dotnet restore` + `dotnet pack` + `dotnet nuget push` sequence. What's missing:

- Cake `Pack-Family` / `Smoke-Family` / `Push-Family` helpers (Stream D-local deliverables).
- API key provisioning policy.
- Recovery playbook (this doc when complete).
- Manual audit trail mechanism.

## 8. Exit Criteria (for PD-8)

The research is considered complete when:

- All seven questions in §4 have documented answers.
- A `docs/playbook/release-recovery.md` playbook exists with concrete operator-executable step lists for §3.1 + §3.2 + §4.4 (partial failure recovery).
- Cake `Pack-Family` / `Smoke-Family` / `Push-Family` targets implemented (or explicitly deferred with rationale).
- API key provisioning policy documented.
- Industry precedent survey complete (§4.7).
- Decision checked against §5 criteria.
- PD-8 closed in [phase-2-adaptation-plan.md](../phases/phase-2-adaptation-plan.md).

Until then, manual escape hatch follows the unspoken contract in §3.1 and §3.2 — operators replicate CI step-for-step by hand.

---

## Sources

- [release-lifecycle-direction.md](../knowledge-base/release-lifecycle-direction.md) — canonical policy
- [full-train-release-orchestration-2026-04-16.md](full-train-release-orchestration-2026-04-16.md) — sibling research, Path A discusses manual multi-tag push
- [phase-2-adaptation-plan.md](../phases/phase-2-adaptation-plan.md) — Stream D-local + D-ci scope
- [exact-pin-spike-and-nugetizer-eval-2026-04-16.md](exact-pin-spike-and-nugetizer-eval-2026-04-16.md) — SUPERSEDED historical research note for the production-time version flow constraint that the manual escape hatch originally had to respect
