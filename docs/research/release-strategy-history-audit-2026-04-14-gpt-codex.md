# Release Strategy History Audit - 2026-04-14 (gpt-codex)

- Date: 2026-04-14
- Author: GitHub Copilot (GPT-5.3-Codex)
- Status: Research snapshot (no implementation changes)
- Scope: Determine whether release strategy has already been debated in repository docs and GitHub issues, and identify likely change targets for post-QA updates.

## Purpose

This note records where release strategy discussions already exist, what is already locked, and what is still open. It is intended to prevent re-debating settled decisions and to focus future edits on unresolved release-lifecycle details.

## Evidence Collected

1. Full docs keyword scan across docs/** for release strategy terms.
2. Full issue scan from GitHub API:
   - Repository issues fetched: 84 (state=all)
   - Strategy/lifecycle broad keyword matches: 50
   - Direct strategy-decision keyword matches: 5
3. Deep issue read for key items:
   - #75, #83, #85 (strategy decisions and implementation handoff)
   - #34, #35, #36, #37, #38 (legacy release workflow items)
   - #54, #55, #63, #71 (active release/packaging topology tracking)

## Findings - Documentation

### 1) Strategy has already been debated extensively and then locked

Primary canonical evidence:

- docs/plan.md
  - Strategic Decisions section explicitly locks Hybrid Static + Dynamic Core.
  - Pure Dynamic is explicitly rejected.
  - Triplet = strategy is explicitly locked.
  - Issue mapping ties these to #75 and #85.

Supporting research set (already present in repo):

- docs/research/packaging-strategy-hybrid-static-2026-04-13.md
- docs/research/packaging-strategy-pure-dynamic-2026-04-13.md
- docs/research/packaging-strategy-synthesis-2026-04-13-copilot.md
- docs/research/packaging-strategy-verdict-2026-04-14-claude-2.md
- docs/research/native-packaging-comparative-analysis-2026-04-13.md
- docs/research/execution-model-strategy-2026-04-13.md
- docs/research/cake-strategy-implementation-brief-2026-04-14.md
- docs/research/temp/README.md (records decision cycle completion and promoted documents)

Conclusion for docs: release packaging strategy debate already happened and was promoted into canonical guidance.

### 2) Release lifecycle details are discussed but not fully settled operationally

Evidence:

- docs/research/release-lifecycle-patterns-2026-04-14-claude-opus.md
  - Contains pattern survey and recommendation space.
  - Still contains explicit open questions (versioning tool, dependency constraints, CI migration order).

- docs/knowledge-base/ci-cd-packaging-and-release-plan.md
  - Documents desired pipeline architecture and phased rollout.
  - Labels several pieces as partially implemented or planned.

- docs/phases/phase-2-cicd-packaging.md
  - Tracks release-candidate pipeline and PackageTask as unfinished core work.

Conclusion for docs: strategy direction is locked, but release lifecycle operation model is still in active shaping.

## Findings - GitHub Issues

### 1) Direct strategy-decision issues (core)

- #75 Implement hybrid static packaging foundation (closed)
  - Includes explicit scope-change comment that locks the decision to Hybrid Static + Dynamic Core.
  - Includes resolved comment showing 3-OS validation summary and remaining follow-up split into #83/#85.

- #83 Hybrid Packaging Foundation Spike (open)
  - Carries explicit strategy-context text comparing pure dynamic vs hybrid static + dynamic core.

- #85 Introduce packaging strategy awareness in Cake build host (open)
  - Tracks strategy integration in build host with landed vs remaining items.

These are the strongest issue-level proof that strategy was discussed and decided.

### 2) Legacy release workflow issues (superseded, not current strategy debate)

- #34, #35, #36, #37, #38 are closed and explicitly marked as replaced by modern release-candidate/prerelease issues.
- They show historical release workflow decomposition, but they are no longer canonical trackers.

### 3) Active release execution issues (implementation, not strategy re-debate)

- #54 PackageTask
- #55 Distributed harvest staging for release-candidate pipeline
- #63 First SDL2 prerelease publication and release metadata

These are about delivering the chosen model, not selecting the model.

## Consolidated Answer

Question: "Have we already discussed release strategy before?"

Answer: Yes, repeatedly and at multiple layers:

1. Deep research cycle in docs/research.
2. Canonical lock-in in docs/plan.md strategic decisions.
3. Issue-level decision and rollout tracking in #75/#83/#85.

What remains open is not core strategy selection, but lifecycle execution choices (versioning/release cadence/pipeline orchestration details).

## Candidate Change Targets After QA (do not apply yet)

If upcoming QA confirms strategy remains unchanged, edits should likely focus on:

1. docs/research/release-lifecycle-patterns-2026-04-14-claude-opus.md
   - Resolve open questions into concrete operating policy.
2. docs/knowledge-base/ci-cd-packaging-and-release-plan.md
   - Promote selected lifecycle policy from research to implementation plan.
3. docs/plan.md and docs/phases/phase-2-cicd-packaging.md
   - Align roadmap items to final lifecycle decisions and remove ambiguity.
4. Issues: #54, #55, #63, #85
   - Update acceptance criteria and sequencing to match selected lifecycle policy.

If strategy itself is reopened, first update docs/plan.md strategic decisions and issue #75/#85 linkage before implementation tickets.

## Verdict

Release strategy has already been debated thoroughly and canonically locked (Hybrid Static + Dynamic Core); next changes should target unresolved release-lifecycle operating details rather than re-opening base strategy unless explicitly requested. - gpt-codex

---

## Addendum: Specific Cleanup Inventory (claude-opus, 2026-04-14)

Cross-referencing the codex audit above with line-level findings from a parallel research effort (see `release-lifecycle-patterns-2026-04-14-claude-opus.md`). This section catalogs **exactly what is stale, conflicting, or missing** in canonical docs and issues, so cleanup can be executed once lifecycle decisions are finalized.

### Agreement with codex audit

The codex distinction is correct: **base packaging strategy is locked** (Hybrid Static + Dynamic Core, triplet=strategy, per-library .Native split). The open frontier is **release lifecycle execution**: versioning model, per-family independence, CI matrix generation, tag-based triggers, change detection, dev feed choice.

### A. Canonical Doc Cleanup Targets

#### 1. `docs/knowledge-base/ci-cd-packaging-and-release-plan.md`

This is the most stale canonical doc relative to current reality:

| Line/Section | Issue | Action Needed |
| --- | --- | --- |
| §3 references `build/runtimes.json` and `build/system_artefacts.json` as separate files | Config merge to `manifest.json` v2 already happened | Remove separate file references, point to manifest.json sections |
| §3 `harvest-manifest.json` example shows `triplet: "x64-windows-release"` | Stale triplet — should be `x64-windows-hybrid` (or pure-dynamic equivalent) | Update to current manifest runtimes |
| §4.A `pre_flight_check` reads `build/runtimes.json` | File no longer exists separately | Update to read from `manifest.json` runtimes section |
| §6 PackageTask mentions `binding_version` field | No such field exists in manifest.json; only `native_lib_version` | Either add `binding_version` to schema or clarify versioning model |
| §6 PackageTask mentions `dotnet pack ... /p:Version={binding_version_from_manifest}` | Assumes manual version injection — incompatible with MinVer tag-based flow | Reconcile after versioning tool decision |
| Entire doc assumes all-at-once build/release | No per-family independence concept | Add family-level release granularity after decision |
| §4.A matrix output example uses `library/version/rid` flat shape | No family grouping, no `depends_on`, no `change_paths` | Align with final manifest schema |

#### 2. `docs/plan.md`

| Section | Issue | Action Needed |
| --- | --- | --- |
| Strategic Decisions table | Missing: per-family versioning, MinVer/tag-prefix, CI dynamic matrix, dev feed choice | Add rows after decisions are made |
| Known Issues #7 "Hybrid triplets not yet created" | Overlay triplets HAVE been created (vcpkg-overlay-triplets exists) | Verify and update/remove |
| Known Issues #8 "Symbol visibility unaddressed" | Symbol visibility analysis done, cosmetic leaks accepted | Update wording to reflect current state |
| `native_lib_version` fields in manifest | No corresponding `binding_version` or family version concept | Clarify after versioning model decision |

#### 3. `docs/phases/phase-2-cicd-packaging.md`

| Section | Issue | Action Needed |
| --- | --- | --- |
| §2.4 "Make Release Candidate Pipeline Functional" | Still references old 3-file config model | Update to manifest v2 |
| Phase 2 items list | No mention of dynamic matrix generation, per-family release, version tooling | Add after decisions |

#### 4. `docs/parking-lot.md`

| Section | Issue | Action Needed |
| --- | --- | --- |
| "Internal Feed, Pack-Only, And Public Promotion Flow" | Still generic; no concrete feed choice or family-level publish flow | Enrich with decisions when made |
| "Harvest Staging Path Model" | Still says "partially-implemented" but staging helpers exist in PathService | Verify current status, possibly promote |

#### 5. `docs/onboarding.md`

| Section | Issue | Action Needed |
| --- | --- | --- |
| Target Platforms table | Shows `x64-windows-release` (stale triplet) | Update to `x64-windows-hybrid` or current |
| "What Doesn't Work Yet" section | Several items may be outdated (overlay triplets, symbol visibility) | Verify each against current state |

### B. GitHub Issue Cleanup Targets

| Issue | State | Specific Stale Content | Action |
| --- | --- | --- | --- |
| **#48** — version.json | CLOSED | "Implement version.json reading and Compute-Version task" — entire approach superseded | Add comment noting this is superseded by MinVer/tag-prefix approach (if adopted). No reopen needed. |
| **#54** — PackageTask | OPEN | Exit criteria mention "manifest version model" but that model is undefined | Update exit criteria after versioning decision. Add family-level pack support. |
| **#63** — First prerelease | OPEN | "package publish flow" — no specifics on tag trigger, dev feed, pre-release versioning | Update with concrete lifecycle policy once decided. |
| **#65** — Package validation + local feed | OPEN | No concrete feed technology choice | Update after dev feed decision. |
| **#83** — Hybrid packaging spike | OPEN | Deliverable §3 "Minimal PackageTask" — may need version model input | Minor update: clarify version source. |
| **#34-38** — Legacy release workflow | CLOSED | Already superseded per codex audit | No action needed — already clean. |
| **#81** — Drift-prevention guardrail | OPEN | "harvest library lists in CI workflows" — dynamic matrix would solve this | Add note that dynamic matrix from manifest.json addresses this. |

### C. What's Genuinely New From the Parallel Research

These topics were **not previously discussed** in any repo document or issue:

1. **Per-family independent versioning with MinVer tag prefixes** — `core-1.0.0`, `image-1.0.3` pattern. No prior discussion exists.
2. **`package_families` manifest section** — family grouping, `depends_on`, `change_paths`, `tag_prefix`. Entirely new concept.
3. **dotnet-affected for change detection** — mentioned in onboarding.md as "Nx rejected, dotnet-affected instead" but never detailed or designed.
4. **Fat native package decision** — `native-packaging-patterns.md` recommended "keep per-library split" but never explicitly addressed all-RIDs-in-one-nupkg vs per-OS split. New research confirms fat package is correct for our size.
5. **Family-locked version coupling** — managed + native always same version within family, but families independent. Not previously formalized.
6. **Concrete endüstri comparison matrix** — LibGit2Sharp exact-pin vs SkiaSharp `>=` vs NSec bounded-range. Previously only topology was compared, not version constraint patterns.
7. **Pure-dynamic first CI migration strategy** — never discussed. All prior CI docs assume the target strategy directly.

### D. Documents That Should NOT Change

These research docs are archival and should be preserved as-is:

- `docs/research/packaging-strategy-hybrid-static-2026-04-13.md`
- `docs/research/packaging-strategy-pure-dynamic-2026-04-13.md`
- `docs/research/native-packaging-comparative-analysis-2026-04-13.md`
- `docs/research/packaging-strategy-verdict-2026-04-14-claude-2.md`
- `docs/research/packaging-strategy-synthesis-2026-04-13-copilot.md`
- `docs/research/execution-model-strategy-2026-04-13.md`
- `docs/research/cake-strategy-implementation-brief-2026-04-14.md`
- `docs/research/tunit-testing-framework-2026-04-14.md`
- `docs/research/release-lifecycle-patterns-2026-04-14-claude-opus.md`
- `docs/research/release-lifecycle-strategy-research-2026-04-14-gpt-codex.md`
- This file itself

These are point-in-time research artifacts. Their value is in recording the reasoning, not in being "current."

— claude-opus addendum end
