# Research: Full-Train Release Orchestration

**Date:** 2026-04-16
**Status:** Roadmap placeholder — research pending. **Amended 2026-04-17:** within-family dependency contract changed from exact pin to minimum range (S1 adoption). PD-7 scope (release-set mechanism, ordering, GitHub UX, failure recovery, release-notes aggregation) is unaffected; only background references to exact-pin csproj shape are now historical. See "S1 addendum" below.
**Context:** Stream D-ci blocker. PD-7 open. See [phase-2-adaptation-plan.md](../phases/phase-2-adaptation-plan.md) Pending Decisions.
**Prerequisite reading:** [release-lifecycle-direction.md](../knowledge-base/release-lifecycle-direction.md)

---

> **S1 addendum (2026-04-17).** References in this doc to "within-family exact pin `[x.y.z]`", `PrivateAssets="all"` on Native `ProjectReference`, and "exact pin precedent" (LibGit2Sharp comparison) are **historical** and describe the pre-S1 contract. S1 adoption retired the within-family exact-pin requirement; within-family is now minimum range (`>=`), matching cross-family. PD-11 in the adaptation plan records the S1 decision. **PD-7 scope is unaffected** — full-train orchestration is about the release-set mechanism (meta-tag + `release-set.json` workflow vs manual multi-tag vs pack-time override), not about the within-family dependency semantics of any individual family. Reading this doc in 2026-04-17+: mentally substitute "SkiaSharp-style minimum range" wherever the doc says "within-family exact pin"; the orchestration questions are orthogonal.

---

## 1. Problem Statement

The canonical release lifecycle direction locks **what** a full-train release is (coordinated release of all families together, triggered by cross-cutting changes) and **when** one is required (vcpkg baseline update, triplet/strategy change, shared toolchain bump, milestone coherence). It does **not** define **how** a full-train is invoked, orchestrated, or surfaced to consumers.

Today, the only mechanism that exists is:

1. Developer pushes one family tag at a time (`git push origin sdl2-core-1.2.0`).
2. Per-family release pipeline fires on `on: push: tags: - 'sdl2-core-*'` matcher.
3. Each family ships independently.

This works for targeted (single-family) releases. It does **not** scale cleanly to full-train because:

### 1.1 The GitHub Release UI trap

Clicking "Create release" in GitHub UI with a tag like `v2.0.0` creates **one** tag. MinVer per-project `<MinVerTagPrefix>` means Core csproj looks for `core-*`, Image looks for `image-*`, etc. A single `v2.0.0` tag matches none of them. Result: every project falls back to `0.0.0-alpha.0.N` prerelease. Full-train shipped, effectively, as garbage prereleases.

### 1.2 The manual multi-tag discipline problem

Today's only correct path is to push N family tags at the same commit:

```bash
git tag sdl2-core-2.0.0 sdl2-image-1.3.0 sdl2-mixer-1.2.0 sdl2-ttf-1.1.0 sdl2-gfx-1.0.5
git push origin sdl2-core-2.0.0 sdl2-image-1.3.0 sdl2-mixer-1.2.0 sdl2-ttf-1.1.0 sdl2-gfx-1.0.5
```

Relies on human memory to enumerate all affected families, pick correct versions, avoid typos, and push them together. Five families today; six once SDL2_net lands; twelve once SDL3 enters. Human discipline does not scale.

### 1.3 Release ordering is unenforced

Satellites depend on Core with `>=` minimum. If all five family tags fire their pipelines simultaneously, satellite pipelines may try to restore `Janset.SDL2.Core (>= 2.0.0)` before `sdl2-core-2.0.0` has published to the internal feed. Result: race condition between parallel pipelines; fragile at best, broken at worst.

### 1.4 GitHub Release UX is unresolved

A full-train release is a single conceptual event ("v2.0.0 train — SDL2 baseline bump"). But it produces N nupkgs across N family tags. How does a consumer discover "what shipped together" in one place? No current design.

### 1.5 Partial-failure recovery is undefined

If `sdl2-core-2.0.0` publishes successfully, `sdl2-image-1.3.0` publishes, then `sdl2-ttf-1.1.0` pack fails mid-train — what's the recovery? Three published packages plus two non-shipped. No documented rollback, no forward-fix protocol, no release-notes amendment process.

## 2. Canonical Constraints (Locked, Not Open)

The research must operate **within** these locked decisions from [release-lifecycle-direction.md](../knowledge-base/release-lifecycle-direction.md):

| Constraint | Source |
| --- | --- |
| Package family = one managed + one native, versioned and shipped as a unit | §1 Package Family |
| Family versions are independent; no cross-family version implication | §3 Version Independence |
| Family tag format: `<family-identifier>-<semver>` (e.g., `sdl2-core-2.0.0`) | §1 Family Tag |
| Family identifier format: `sdl<major>-<role>` (e.g., `sdl2-core`, `sdl3-image`) | §1 Family Identifier |
| Full-train triggered by specific change categories (vcpkg baseline, triplet/strategy, shared toolchain, milestone) | §1 Full-Train Trigger |
| Release ordering: core first, satellites after | §1 Release Ordering |
| Release promotion: local → internal → public, never skip stages | §6 Release Promotion |
| Historical pre-S1 within-family: exact pin `[x.y.z]`. Cross-family: minimum `>= x.y.z` | §4 Dependency Contract Model |
| Tag-derived versioning via MinVer (no manual csproj edits) | §3 Versioning Model |

**None of these are on the table for this research.** The research picks a mechanism that honors them.

## 3. Candidate Paths

Three mechanism families emerged from the A-risky alignment discussion. Any valid solution is one of these or a combination.

### 3.1 Path A — Manual Multi-Tag Push (current default)

Developer pushes N family tags at HEAD manually. Each tag triggers its per-family release pipeline. GitHub Release object is created post-hoc pointing at one of the tags (or a cosmetic meta-tag) with hand-curated notes listing all shipped versions.

**Pros:**
- Zero new infrastructure.
- Works with the existing per-family release pipeline.
- Auditable git history: each family's tag is a discrete release point.

**Cons:**
- Human discipline required for enumeration, versioning, and atomicity.
- Release ordering unenforced (parallel pipelines race).
- GitHub Release is a manual afterthought.
- No rollback protocol.

**Where it fits:** fine for 2a + early 2b while frequency is low. Not sustainable long-term.

### 3.2 Path B — Meta-Tag + `release-set.json` Workflow

Developer commits a `release-set.json` file at repo root enumerating families + target versions:

```json
{
  "train_id": "2026-Q2",
  "reason": "vcpkg baseline 2026.03 + SDL2 2.32.10",
  "families": {
    "sdl2-core":  "2.0.0",
    "sdl2-image": "1.3.0",
    "sdl2-mixer": "1.2.0",
    "sdl2-ttf":   "1.1.0",
    "sdl2-gfx":   "1.0.5"
  },
  "release_notes": {
    "path": "docs/release-notes/train-2026-Q2.md"
  }
}
```

Developer pushes a single meta-tag: `git push origin train-2026-Q2`. A dedicated workflow fires on `train-*` pattern and:

1. Parses `release-set.json`.
2. Validates it (versions are SemVer, family identifiers match `package_families[].name` in `manifest.json`, no duplicates).
3. Creates each family tag at the same commit: `sdl2-core-2.0.0`, `sdl2-image-1.3.0`, etc.
4. Pushes them in dependency order: core first, satellites after.
5. Waits on `sdl2-core-*` publish completion before releasing the `sdl2-<satellite>-*` tags.
6. Posts a single GitHub Release pointing at `train-2026-Q2` with consolidated notes auto-assembled from per-family changelogs + the `release_notes.path` header.

**Pros:**
- One click / one push atomically triggers a coordinated full-train.
- `release-set.json` is reviewable in a PR before it happens.
- Release ordering is enforced by the workflow, not by luck.
- GitHub Release UX is solved: one release, consolidated notes.
- Manifest-driven — extends naturally to SDL3 families.
- Auditable: `release-set.json` + `train-*` tag = immutable record of "what shipped together."

**Cons:**
- Infrastructure cost: new workflow, new JSON schema, new validation, new release-notes aggregator.
- Adds a repo file (`release-set.json`) that must be kept accurate.
- Complicates partial rollback: if `ttf` fails mid-train, what does the meta-tag mean? (Needs design.)

**Where it fits:** 2b scope. Aligns with canonical "full-train as atomic operation" semantics.

### 3.3 Path C — Pack-Time Version Override (escape hatch)

Bypass MinVer entirely during full-train. Cake reads family versions from `release-set.json` (or env vars, or CLI flags) and passes them explicitly to each pack invocation:

```bash
dotnet pack src/SDL2.Core/SDL2.Core.csproj \
  -p:MinVerVersionOverride=2.0.0 \
  -p:Version=2.0.0 \
  -p:Sdl2CoreFamilyVersion=2.0.0
```

No family git tags required. Version comes from the JSON, not from git state.

**Pros:**
- Works with any tag convention (or none at all).
- Decouples "what version ships" from "what's tagged in git."

**Cons:**
- **Loses git as the source of truth for versions.** If you need to rebuild `sdl2-core-2.0.0` from scratch six months later, there's no tag to check out.
- Breaks the canonical "tag-derived versioning" rule from §3 of the direction doc.
- Audit trail depends on preserving `release-set.json` snapshots per release — fragile.
- MinVer becomes decorative in full-train flow; behaves as "sometimes authoritative, sometimes overridden" — confusing mental model.

**Where it fits:** escape hatch only. **Strong presumption against adopting this as the primary mechanism.** Worth researching only because understanding *why* we rejected it is part of the record.

### 3.4 Hybrid Possibilities

Not mutually exclusive. Likely real answer is some combination:

- **A for low-frequency early Phase 2b**, **B for steady state** once train volume justifies the infra.
- **B for targeted-release orchestration too** (one family can be a "train of one" — lets the same workflow handle both).
- **B + C hybrid** where B handles tagging + workflow orchestration but uses MinVer-derived versions (C only as override mechanism when a rebuild-without-retag is required).

The research should evaluate whether hybrid shape is simpler or more complex than pure-B.

## 4. Questions the Research Must Answer

Each question below needs a documented decision + rationale before Stream D-ci starts.

### 4.1 Tag invocation mechanism — A, B, or hybrid?

Which path does the project adopt? Decision with rationale.

### 4.2 Release ordering enforcement — how does CI guarantee Core publishes before satellites restore?

Options to evaluate:

- **Serial workflow with `needs:`** — `publish-satellites` job waits on `publish-core` job completion. Simple, GitHub-native. Cost: total train time = core time + satellite fan-out time (satellites parallel amongst themselves).
- **Satellite retry-on-feed** — satellites poll internal feed until Core at target version is available, then proceed. Tolerates parallelism but complex, can mask bugs.
- **Artifact-wait** — satellites wait on upload-artifact signal from Core job before starting pack. Cleaner than polling, still requires Core to complete first.
- **Publish ordering at feed layer** — satellites publish conditionally (only if `Core >= x.y.z` is on the feed). Shifts ordering to push-time rather than build-time.

Exit criterion: one option chosen + documented + validated in a spike.

### 4.3 GitHub Release UX — single release or per-family?

Options:

- **One release per train** pointing at the `train-*` meta-tag with consolidated notes listing all families + versions.
- **One release per family tag** with auto-aggregation into a "train overview" page elsewhere (docs site? wiki?).
- **Both** — per-family releases (for discoverability + direct NuGet linking) plus a train-level release (for the "what shipped together" narrative).

Exit criterion: UX flow documented with screenshots or mockups; user journey clear.

### 4.4 Release-notes aggregation — how do consolidated notes get built?

Options:

- **Hand-curated** in `release_notes.path` file referenced from `release-set.json`. Highest quality, highest effort.
- **Auto-aggregated** from per-family CHANGELOG.md files. Zero effort, may be noisy.
- **Conventional commits + auto-generator** (like release-please, changesets). Medium effort, consistent output.
- **Hybrid**: auto-aggregated draft + hand-edited before publish.

Exit criterion: tooling choice made; template documented.

### 4.5 Failure recovery — what happens if mid-train something fails?

Scenarios to cover:

- **Pack failure on one family** (Core succeeded, Image pack failed): do we rollback Core, forward-fix Image, or mark train partial and patch?
- **Publish failure on one family** (nupkg produced but NuGet.org push fails): retry protocol, feed reconciliation, metadata correction.
- **Mid-train SDK or toolchain outage** (GitHub Actions runner issue, NuGet feed down): graceful abort, resumable retry.
- **Validation failure** (package-consumer smoke test fails on published Core before satellites restore): hold satellites or proceed?

Exit criterion: recovery playbook documented in `docs/playbook/release-recovery.md` or equivalent.

### 4.6 Industry precedents — how do peer projects do this?

Projects to survey:

| Project | Why relevant |
| --- | --- |
| **Avalonia** (AvaloniaUI/Avalonia) | Monorepo, multi-package NuGet, GitHub Actions, .NET ecosystem |
| **ASP.NET Core** (dotnet/aspnetcore) | Coordinated release of 50+ packages, patch pipeline, servicing branches |
| **ppy/osu-framework** | Independent bindings repo, SDL3-adjacent, active release cadence |
| **SkiaSharp** (mono/SkiaSharp) | Our closest architectural analogue (binding + native + hybrid) — but single-version model, not family-versioned |
| **LibGit2Sharp** | Native binaries + managed package, historical exact pin precedent |
| **Lerna / Changesets** (JS ecosystem) | Monorepo multi-package publishing, independent versioning patterns |
| **Nx release** (nx-monorepo) | .NET-rejected but worth understanding the pattern |
| **release-please** (googleapis/release-please) | GitHub-native release automation with conventional commits |

For each: tag strategy, release workflow shape, release-notes approach, handling of coordinated vs targeted releases, partial-failure recovery. Target: 1-2 paragraph summary per project + extracted patterns we should copy or avoid.

Exit criterion: survey complete; patterns extracted; anti-patterns named.

## 5. Decision Criteria

When the research proposes a mechanism, evaluate it against:

| Criterion | Weight |
| --- | --- |
| Honors all canonical constraints from §2 | Hard requirement |
| Scales to SDL2 (6 families) + SDL3 (4+ families) = 10+ families without modification | Hard requirement |
| Release ordering enforced by tooling, not human discipline | Hard requirement |
| Auditable: "what shipped on YYYY-MM-DD" answerable from git + CI logs alone | Hard requirement |
| Targeted-release path unchanged or simplified | Hard requirement |
| Failure recovery documented + tested | Hard requirement |
| Infra effort reasonable (< 2 weeks full-time equivalent to implement) | Preference |
| Surface area familiar to contributors (GitHub Actions + Cake, not exotic tooling) | Preference |
| GitHub Release UX coherent (consumers can find "what shipped together") | Preference |

## 6. Non-Goals

Out of scope for this research:

- Changing the independent family-versioning model.
- Changing MinVer or replacing it.
- Changing the per-family csproj shape from A-risky.
- Designing the internal-feed → public-NuGet.org promotion workflow (separate concern, Stream D-ci 2b).
- Designing the `DetectChangesTask` / affected-family filtering (Stream E scope).
- Changing the CI matrix shape (PA-1 scope).

This research is **purely about the orchestration layer** that turns "we want to ship families X, Y, Z together" into "N nupkgs published in the right order with coherent release metadata."

## 7. Relationship to A-Risky

**A-risky is unaffected by whichever path wins.** Mechanical analysis (2026-04-16):

| A-risky artifact | Path A impact | Path B impact | Path C impact |
| --- | --- | --- | --- |
| `<MinVerTagPrefix>{family}-</MinVerTagPrefix>` per csproj | None | None | None (bypassed by override, still present) |
| `<PackageVersion Include="...Native" Version="[$({Family}FamilyVersion)]" />` | None | None | None ($(Version) overridden externally) |
| `PrivateAssets="all"` on Native `ProjectReference` | None | None | None |
| PreFlight csproj structural validator | None | None | None |

Historical pre-S1 note: A-risky bakes in the csproj shape required for exact-pin. Full-train research decides CI workflow + `release-set.json` schema + GitHub Release UX. Orthogonal file sets, orthogonal concerns.

Research can proceed in parallel with or after A-risky without rework risk on either side.

## 8. Proposed Research Session Structure

When this research is picked up as its own session, the suggested structure:

1. **Precedent survey** (~50% of session) — read each project in §4.6, extract patterns, note decisions we should copy or reject.
2. **Path evaluation** (~25%) — apply criteria from §5 to each of Path A / B / C / hybrid; produce scored comparison.
3. **Recommendation** (~15%) — one path chosen + rationale; handle each question in §4.
4. **Implementation sketch** (~10%) — rough Cake task + GHA workflow shape + `release-set.json` schema if Path B wins. Not full implementation — just enough for Stream D-ci estimation.

Output: this doc rewritten from placeholder to completed research note, following the exact structure of the SUPERSEDED historical note [exact-pin-spike-and-nugetizer-eval-2026-04-16.md](exact-pin-spike-and-nugetizer-eval-2026-04-16.md) (empirical evidence + proof + decision).

## 9. Exit Criteria (for PD-7)

The research is considered complete when:

- Path chosen (A, B, C, or hybrid) with documented rationale.
- All six questions in §4 have documented answers.
- Industry precedent survey complete (§4.6).
- Decision checked against §5 criteria.
- A `docs/playbook/full-train-release.md` playbook drafted (or noted as Stream D-ci deliverable).
- PD-7 closed in [phase-2-adaptation-plan.md](../phases/phase-2-adaptation-plan.md).

Until then, full-train releases follow **Path A (manual multi-tag push)** as the interim mechanism. This is an explicit operational limitation, not a silent default.

## 10. Open Risk: Partial Migration of Canonical Docs

[release-lifecycle-direction.md](../knowledge-base/release-lifecycle-direction.md) already commits to "full-train is atomic" language. If the research lands on Path A as the mechanism (manual, non-atomic in practice), the direction doc's "atomic" framing needs amendment or clarification. Flag this when the recommendation crystallizes.

---

## Sources

- [release-lifecycle-direction.md](../knowledge-base/release-lifecycle-direction.md) — canonical policy
- [phase-2-adaptation-plan.md](../phases/phase-2-adaptation-plan.md) — implementation streams + pending decisions
- [exact-pin-spike-and-nugetizer-eval-2026-04-16.md](exact-pin-spike-and-nugetizer-eval-2026-04-16.md) — SUPERSEDED historical sibling research note (precedent for structure)
- MinVer docs: [adamralph/minver](https://github.com/adamralph/minver)
- GitHub Actions workflow matrix reference
- NuGet feed push API reference
