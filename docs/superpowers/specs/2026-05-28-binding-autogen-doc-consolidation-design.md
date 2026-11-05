# Binding-Autogen Documentation Consolidation — Design Spec

**Date:** 2026-05-28
**Status:** Design — ready for implementation plan (writing-plans next)
**Branch:** `spike/binding-autogen-sdl2-gfx`
**Trigger:** Pre-Layer-2 documentation cleanup; consolidation gate before Layer 2 typed public API work begins.

---

## Goal

Pre-Layer-2 documentation cleanup that:

1. Preserves all unique information (especially Layer 1/2 design content and testing strategy).
2. Retires sunset-Cake-implementation pollution across the doc tree.
3. Restructures the binding-autogen workstream around forward layer-based work (Layer 2 → Layer 3 → production flip → satellite SysWM → smoke expansion → SDL3).
4. Relocates canonical policy into the active spike during the spike → production-flip transition.
5. Keeps the constitution a pure principles document; mechanism + evidence move to sibling docs.

Direction is leaning ClangSharp + Roslyn postprocess based on Layer 1 closure evidence (~889 functions emitted, 98.1% dynapi coherence, multi-TFM clean across 5 SDL2 families), but the **formal toolchain commitment (ADR-004 amendment) is deferred to post-Layer-2-closure**. This consolidation operates with that neutrality.

## Authority

This is a doc-cleanup design spec, not a policy change. Doc decisions in this spec are binding on the implementation plan that follows; policy decisions (ABI rules, scalar mapping, opaque handles, etc.) remain in the canonical constitution and are not reopened here.

## Current State (Briefest Possible)

- **SDL2 Layer 1 raw ABI:** closed 2026-05-28 across Core/Image/GFX/TTF/Mixer on the ClangSharp + Roslyn postprocess spike under `spikes/binding-generators/clangsharp/`.
- **Sunset Cake-hosted CppAst implementation** lives at `build/_build/Targets/GenerateBindings/`; archived M1/M2 plans in `docs/parking-lot/binding-autogen-cake-implementation/`; M3 paused before implementation.
- **ADR-004** is "Reopened (2026-05-23)"; formal amendment deferred per user message ("final decision after Layer 1/2").
- **Doc surface today:** `docs/binding-autogen/` (4 canonical files) + `spikes/binding-generators/docs/` (22 files: charter, handoff, satellite-expansion-roadmap, closure summaries, items/, satellites/, testing/, oracle docs, ppy reference analysis) + parking-lot (4 files) + 2 playbooks + ADR-004 + phase-4 doc + cross-cuts in AGENTS.md, plan.md, release-strategy.md.

## Settled Decisions (Q1–Q15 + Multi-Agent Review Amendment)

| # | Decision | Outcome |
| --- | --- | --- |
| Q1 | Topology — where canonical docs live during spike | **Move into spike** during spike; restore to `docs/binding-autogen/` after production flip. Links update everywhere. |
| Q2 | Location inside spike | `spikes/binding-generators/docs/canonical/` subfolder. |
| Q3 | Constitution purification scope | **Aggressive (C)** — pure principles only (~350 lines target); mechanism/evidence move to NEW `binding-generator-implementation-notes.md` sibling. |
| Q4 | Roadmap restructure | **C leaning D** — collapse M0–M4 into "What's Done" section; forward milestones use **layer-based names** (Layer 2 Typed Public API / Layer 3 Friendly Overloads / Production Flip / SysWM Layout / Smoke Expansion / SDL3 Extension); retire M-numbering everywhere it appears; cut Canonical References table, Operating Rules wall, Continuous Maintenance list to slim ~250-line target. |
| Q5 | Testing-strategy purification | **Aggressive (C)** — preserved anchors: Generated Raw ABI Runtime Tests section (P0/P1 targets + ABI risk families), SDL2 Core + Satellite Research Summaries (upstream test inventory). Implementation Backlog stays inside. |
| Q6 | Spike top-level docs disposition (9 files) | Retire 5 (`llm-handoff`, `priority-c-closure-summary`, `generator-spike-goals`, `oracle-evidence-design`, `oracle-evidence-implementation-plan`); restructure 1 (`next-iteration-plan` — absorb forward backlog from satellite-expansion-roadmap, cut historical slice progress); retire 1 after fold (`satellite-expansion-roadmap` — closure facts → git history, cross-cutting facts → implementation-notes, forward backlog → next-iteration-plan); keep+move 1 (`ppy-reference-analysis-2026-05-25` → `canonical/references/`); keep standalone (`reference-clones`). |
| Q7 | items/ folder framework (10 files) | Delete 5 plans (execution scaffolding, empty unique-info kernel); audit-fold-delete 5 specs (unique kernels into constitution/implementation-notes/next-iteration-plan/satellites as appropriate). Folder retires entirely. |
| Q8 | satellites/ folder (7 files) | All 7 preserved; **move to `canonical/satellites/` subfolder**; rename `sdl2-image-cross-check.md` → `sdl2-image-header-analysis.md` for naming consistency. |
| Q9 | testing/ folder (3 files) | **Fold all 3 into purified testing-strategy.md.** raw-abi-upstream-testing-spec policy merges with testing-strategy overlapping sections; raw-abi-upstream-testing-plan multi-agent slice tables fold into testing-strategy "Raw ABI Upstream Port Backlog"; raw-abi-current-inventory deferred-blockers fold into testing-strategy deferred-coverage notes. Folder retires entirely. |
| Q10 | parking-lot disposition | **Audit-and-retire all 4** files (parking-lot README + 3 milestone plans). Strict audit gate per user emphasis: identify CppAst-independent / toolchain-neutral content (safety-harness patterns, parser/model/emitter separation, engine/policy boundary, satellite reconnaissance findings, M2 review naming/scoping principles), fold survivors into constitution/implementation-notes/testing-strategy/satellites. No blind retirement. `docs/parking-lot/binding-autogen-cake-implementation/` folder removes. |
| Q11 | Playbooks | Retire sunset `binding-generator-maintenance.md`; **write NEW ClangSharp+Roslyn-adapted `canonical/binding-generator-maintenance.md`** from scratch covering version-bump procedures, `family-config.json` schema maintenance, RSP file maintenance, postprocess pipeline maintenance, drift detection, new-family addition checklist. Purify + move `binding-output-oracle-validation.md` → `canonical/binding-output-oracle-validation.md` (toolchain-neutral methodology survives). |
| Q12 | Phase-4 doc | **Purify in place at `docs/phases/`** (keep structural taxonomy entry). Strip sunset Cake-impl paragraphs; keep "Phase 4 ships before Phase 3" sequencing + Stage 1/2/3 scope phasing; update cross-refs to canonical/ paths. Target ~30-35 lines after cuts. |
| Q13 | ADR-004 update scope | **Status update + path refresh, neutral banner.** Status: "Reopened (re-evaluation underway since 2026-05-23; Layer 1 closure on ClangSharp + Roslyn postprocess spike completed 2026-05-28 for SDL2 Core/Image/GFX/TTF/Mixer; formal amendment deferred pending Layer 2 closure)". Sections 1-6 (original CppAst rationale) untouched as historical record. Section 7 References path-update only. |
| Q14 | Cross-cutting refs in upstream docs | **A.α — apply framework with section-name refs.** AGENTS.md L91 paragraph rewrite (post-spike state, neutral toolchain formality); plan.md Phase 4 section refresh (path updates + Status banner + Stage 1/2 work-item text refresh, checkboxes stay open); release-strategy.md References list path updates + Stage 1/2 table minor cleanups; inline `postprocess/*.cs` comment grep+update; sunset Cake impl comments left for retirement with the code. **Section-name cross-references (`Constitution §"C \`long\`..."`) replace line-number refs** to survive future purifications. |
| Q15 | Spike-level structural | (a) Spike README major refresh (Status, Read first table, Layout, How a new agent picks this up). (b) `.github/prompts/binding-generator-spike-handoff.prompt.md` audit-during-execution. (c) `spikes/binding-generators/clangsharp/README.md` audit-during-execution. (d) **Keep `next-iteration-plan.md` name** (no rename). (e) Retire 2 legacy oracle-comparison reports (`oracle-comparison-clangsharp.md`, `oracle-comparison-alimer.md`). (f) Keep `reference-clones.md` standalone. (g) `Directory.Build.props` unchanged. **Amendment — `.github/prompts/`:** retire 3 spike-specific priming prompts (`binding-generator-spike-priority-c-handoff.prompt.md`, `satellite-expansion-research-cross-check.prompt.md`, `item-4-closure-item-5-mixer-handoff.prompt.md`); keep 2 generic templates (`general-deep-dive-code-reviewer.prompt.md`, `session-pickup-template.prompt.md`). |
| Amendment | Multi-agent review at critical stages | 6 critical review checkpoints during execution (see §"Critical Stage Review Checkpoints"). No-brainer stages (mechanical moves, path updates, items/ plan deletions, legacy report retirements, spike-specific prompt deletions) skip multi-agent review. |

## Target Doc Topology (Final State)

```text
spikes/binding-generators/                                         (root unchanged)
├── README.md                                                       (refreshed Q15a)
├── Directory.Build.props                                           (unchanged)
├── docs/
│   ├── canonical/                                                  (NEW subfolder — home of canonical content during spike)
│   │   ├── README.md                                               (purified from docs/binding-autogen/README.md)
│   │   ├── binding-generator-constitution.md                       (Q3C aggressive — pure principles)
│   │   ├── binding-generator-roadmap.md                            (Q4D — layer-based forward milestones)
│   │   ├── testing-strategy.md                                     (Q5/Q9 — aggressive + testing/ folded in)
│   │   ├── binding-generator-implementation-notes.md               (NEW — mechanism + evidence + classification labels + hardcoding rules + cross-cutting iteration-2 facts)
│   │   ├── binding-generator-maintenance.md                        (NEW — ClangSharp+Roslyn-adapted, written from scratch)
│   │   ├── binding-output-oracle-validation.md                     (purified from docs/playbook/, moved here)
│   │   ├── satellites/                                             (Q8 — moved from docs/satellites/)
│   │   │   ├── sdl2-image-header-analysis.md                       (renamed from sdl2-image-cross-check.md)
│   │   │   ├── sdl2-mixer-header-analysis.md
│   │   │   ├── sdl2-ttf-header-analysis.md
│   │   │   ├── sdl2-gfx-header-analysis.md
│   │   │   ├── sdl2-satellite-error-function-consolidation.md
│   │   │   ├── sdl2-endian-platform-macro-consolidation.md
│   │   │   └── sdl2-function-like-macro-consolidation.md
│   │   └── references/
│   │       └── ppy-reference-analysis-2026-05-25.md                (moved from spike docs/ top-level)
│   ├── next-iteration-plan.md                                      (restructured: Layer 2 forward iteration + absorbed backlog)
│   └── reference-clones.md                                         (utility doc, standalone)
├── output/reports/                                                 (Q15e cleanup)
│   ├── iteration-2-comparison.md                                   (keep)
│   ├── clangsharp-failure-buckets.md                               (keep)
│   ├── clangsharp-production.md                                    (keep)
│   ├── oracle-evidence-clangsharp.md                               (keep)
│   ├── alimer-full.md                                              (keep)
│   ├── oracle-comparison-clangsharp.md                             (RETIRE — legacy, not regenerated)
│   └── oracle-comparison-alimer.md                                 (RETIRE — legacy, not regenerated)
└── (clangsharp/, references/, alimer-style/, scope/ unchanged)

docs/
├── binding-autogen/                                                (RETIRED entire folder — content moved to spike canonical/)
├── parking-lot/binding-autogen-cake-implementation/                (RETIRED entire folder — Q10 audit-and-retire)
├── playbook/
│   ├── binding-generator-maintenance.md                            (RETIRED — sunset; new ClangSharp version at canonical/)
│   ├── binding-output-oracle-validation.md                         (RETIRED — moved to canonical/)
│   └── local-development.md                                        (unchanged, non-binding-autogen)
├── phases/
│   └── phase-4-binding-autogen.md                                  (purified in place, ~30-35 lines)
└── decisions/
    └── 2026-05-14-binding-autogen-toolchain.md                     (status+path refresh, neutral banner)

.github/prompts/
├── binding-generator-spike-priority-c-handoff.prompt.md            (RETIRE)
├── satellite-expansion-research-cross-check.prompt.md              (RETIRE)
├── item-4-closure-item-5-mixer-handoff.prompt.md                   (RETIRE)
├── general-deep-dive-code-reviewer.prompt.md                       (keep — generic template)
└── session-pickup-template.prompt.md                               (keep — generic template)

(AGENTS.md, docs/plan.md, docs/release-strategy.md, inline comments in spike postprocess: modify-in-place per Q14A.α)
```

## File-Level Change Inventory

**Net change: 51 files affected** (13 moved + 29 retired + 6 modified + 2 new + 1 restructured), plus 4 folder removals (`docs/binding-autogen/`, `docs/parking-lot/binding-autogen-cake-implementation/`, `spike docs/items/`, `spike docs/testing/`) implicit after their files leave.

| Class | Count | Files |
| --- | --- | --- |
| **MOVE + PURIFY into `canonical/`** | 13 | 4 from `docs/binding-autogen/` (README, constitution, roadmap, testing-strategy); 1 from `docs/playbook/` (binding-output-oracle-validation); 1 from spike docs/ top-level (ppy-reference-analysis-2026-05-25); 7 from spike `docs/satellites/` (including rename `sdl2-image-cross-check.md` → `sdl2-image-header-analysis.md`) |
| **RETIRE (audit-and-fold, then `git rm`)** | 29 | 4 parking-lot files (README + 3 milestone plans); 1 sunset playbook (`docs/playbook/binding-generator-maintenance.md`); 6 spike top-level docs (`llm-handoff`, `priority-c-closure-summary`, `generator-spike-goals`, `oracle-evidence-design`, `oracle-evidence-implementation-plan`, `satellite-expansion-roadmap` after fold); 10 spike `items/` files (5 plans + 5 specs after audit); 3 spike `testing/` files (after fold); 3 `.github/prompts/` priming files; 2 legacy `output/reports/` files (oracle-comparison-clangsharp, oracle-comparison-alimer) |
| **MODIFY-IN-PLACE** | 6 | spike README, AGENTS.md L91, `docs/plan.md` Phase 4 section, `docs/release-strategy.md` refs+stages, ADR-004 (status+paths), `docs/phases/phase-4-binding-autogen.md` (purify) |
| **NEW (written during execution)** | 2 | `canonical/binding-generator-implementation-notes.md`, `canonical/binding-generator-maintenance.md` |
| **RESTRUCTURE (substantial rewrite of existing)** | 1 | `spike docs/next-iteration-plan.md` (absorb forward backlog from satellite-expansion-roadmap; cut historical slice progress; restructure around Layer 2 active iteration) |
| **FOLDER REMOVALS (implicit after moves/retires)** | 4 | `docs/binding-autogen/` (after 4 files move to canonical/); `docs/parking-lot/binding-autogen-cake-implementation/` (after 4 retire); `spike docs/items/` (after 10 retire); `spike docs/testing/` (after 3 retire) |

## Three Standing Rules Across All Retirements

### Rule 1 — Unique-Info Audit Gate

Every retirement reads the file in full, identifies unique-info kernels not duplicated elsewhere, lands each kernel in a confirmed destination doc, then `git rm`. No blind retirement. The audit produces a per-file unique-info-landing table mapping each kernel to its destination (constitution / implementation-notes / testing-strategy / satellites / maintenance playbook / next-iteration-plan / oracle-validation / cross-references).

Audit format per retired file:

```text
File: <path>
Sections audited:
  - <section>: <unique kernel description> → <destination doc + section>
  - <section>: duplicate of <other doc + section> → no fold needed
  - <section>: sunset-specific / no durable kernel → no fold needed
Verdict: ready to git rm | additional fold needed
```

### Rule 2 — CppAst-Independent Fold

Toolchain-neutral patterns get adapted into the new ClangSharp+Roslyn-shaped docs even if their original framing was Cake-impl-specific. Examples flagged for audit attention:

- Safety-harness pattern (snapshot-first, embedded `.h` fixtures, compile-check gates, RED/GREEN discipline) → testing-strategy.
- Parser/model/emitter separation principle → constitution + implementation-notes.
- Engine/policy/profile boundary → constitution §"Manifest Configuration Vs Code-Owned Policy" + §"Generator Engine And SDL Policy".
- Trio-pinning concept (pin toolchain version coherently) → new maintenance playbook (adapted for ClangSharp + Microsoft.CodeAnalysis pinning).
- Naming/scoping principles (e.g., "ExternalNativeTypePolicy is foreign-type policy, not SDL policy"; "PlatformCatalog.AllPlatformMacros is SDL2.Core-specific macro hygiene") → constitution Foreign Type Boundary Policy + implementation-notes.
- M3 satellite reconnaissance findings (per-family header counts, declaration tokens) → cross-check against `canonical/satellites/` for completeness; fold gaps.
- Multi-oracle review methodology → oracle-validation playbook.

### Rule 3 — Section-Name Cross-References

Line-number cross-references (`Constitution L246-283 for C \`long\` policy`) become section-name cross-references (`Constitution §"C \`long\` And \`unsigned long\`"`). Section names survive constitution purifications; line numbers shift on every edit. Apply across:

- All cross-refs internal to canonical/ docs.
- All cross-refs in upstream docs (AGENTS.md, plan.md, release-strategy.md, ADR-004, phase-4).
- Inline code comments in `spikes/binding-generators/clangsharp/postprocess/*.cs`.
- Spike README + reference-clones.md (if any line-number refs exist).

## New Documents — Content Outlines

### `canonical/binding-generator-implementation-notes.md`

Sibling to the purified constitution. Captures mechanism + evidence content moved out during Q3C purification.

Top-level sections:

1. **Purpose** — relationship to constitution (policy in constitution, mechanism here).
2. **Generation Determinism Implementation** — the enumerated pin set + verification commands + family isolation specifics (moved out from constitution's Generation Determinism Contract section, which keeps only the durable invariants).
3. **Opaque Handles Implementation Mechanism** — `OpaqueHandleEmitRewriter` design, auto-detect criterion, family-keyed roster integration, cross-family handle name resolution data pull.
4. **C `long` Hybrid Implementation** — `ClongDualDispatchRewriter` design, per-header RSP exclude pattern, hybrid emit shape (Compat dual-dispatch vs Modern CULong + LibraryImport).
5. **wchar_t Implementation Mechanism** — RSP-level `--remap` literal-space pattern, `WcharStarToNintRewriter` fallback.
6. **SDL_GUID Substitution** — `GuidSubstitutionRewriter` design.
7. **Foreign Type Boundary Implementation** — per-header RSP `--remap` pattern, foreign-type allow-list mechanism.
8. **`[Flags]` Detection Mechanism** — `FlagsAttributeRewriter` suffix + allow-list policy.
9. **Cross-Assembly Pattern B Contract** — `[assembly: DisableRuntimeMarshalling]` mechanism, NET7_0_OR_GREATER gating.
10. **Classification Labels** — `Hard Bug` / `Likely Bug` / `Accepted Deferral` / `Compatibility Risk` / `Evidence Missing` / `Out Of Scope` (moved from oracle-evidence-design).
11. **Hardcoding Rules** — good code-owned policy vs good config-owned facts vs bad patterns (moved from generator-spike-goals).
12. **Config Surface Evolution** — 18-source pre-unification inventory + classification against Manifest Configuration Vs Code-Owned Policy (moved from iteration-2-config-surface-unification-spec).
13. **Postprocess Pipeline Order** — 7-step pipeline rationale (moved from satellite-expansion-roadmap cross-cutting facts).
14. **References** — cross-link to constitution sections + maintenance playbook.

### `canonical/binding-generator-maintenance.md`

New ClangSharp+Roslyn-adapted maintenance playbook. Replaces sunset `docs/playbook/binding-generator-maintenance.md`.

Top-level sections:

1. **Purpose + When to Use** — SDL version bumps (`library_version` field per family in `family-config.json`), vcpkg baseline updates, ClangSharp tool version bumps, Microsoft.CodeAnalysis postprocess version bumps, new satellite family addition, SDL3 planning, overlay/triplet changes.
2. **Toolchain Version Pinning** — ClangSharp tool (`dotnet-tools.json`), `libclang.runtime.*` (via ClangSharp tool's transitive dep — pin currently 20.x), `Microsoft.CodeAnalysis.CSharp` (VersionOverride in `postprocess/Janset.SDL2.PostProcess.csproj`), `oracle.cs` `#:package` directive. Coherent-set pinning discipline.
3. **Configuration Surface Map** — `family-config.json` schema with every editable field documented:
   - `families.<family>.namespace` / `library_version` / `raw_class` / `headers[]` / `platform_sensitive_headers` / `required_surface` / `owner_mode`
   - `families.<family>.opaque_handles.{auto_detect_well_known, force_opaque_exceptions, excluded_candidates}` — 3-source triangulation audit method (release headers + wiki.libsdl.org + ClangSharp Modern output)
   - `families.<family>.flags_enums.allow_list`
   - `families.<family>.clong_methods`
   - `global.platform_views` / `all_platform_macros` / `base_rsp`
4. **RSP File Maintenance** — three-tier organization (`base.rsp` cross-cutting / `sdl2-<family>.rsp` family identity / `per-header/<header>.rsp` per-header overrides); when to add an entry; type-remap discipline (literal-space `--remap` for libclang byte-exact match); additive-only semantics for keyed entries.
5. **Postprocess Pipeline Maintenance** — 7-step order documented (`platform-delta` → `strip-varargs` → `libraryimport` Modern only → `flags-detect` → `guid-substitute` → `clong-dispatch` → `uniform-opaque`); when to add a new rewriter; scope-bounded discipline (each rewriter targets one concept).
6. **Version-Bump Procedures:**
   - **SDL2 version bump** (e.g., 2.32.10 → 2.x.y): regenerate, diff with `--ignore-cr-at-eol`, run oracle, compare oracle deltas, update `family-config.json` `library_version` + `opaque_handles.last_audited` + `flags_enums.last_audited`, run AbiTests, run Slopwatch.
   - **ClangSharp tool version bump**: update `dotnet-tools.json`, regenerate, diff for output drift, check for new ClangSharp options/behaviors.
   - **Microsoft.CodeAnalysis postprocess version bump**: update VersionOverride, build postprocess project, run self-tests, regenerate with postprocess.
   - **vcpkg triplet bump**: regenerate, run cross-triplet ABI smoke, update overlay if needed.
7. **Adding a New SDL Satellite Family** — checklist:
   - manifest entry (`build/manifest.json package_families[]`)
   - `family-config.json` family entry with all required fields
   - `scope/`-removed → headers go into family-config.json
   - `rsp/sdl2-<family>.rsp` family identity
   - csproj with ProjectReference→Core
   - `Support/DisableRuntimeMarshalling.cs`
   - opaque handle audit (3-source triangulation)
   - flags enum audit
   - clong methods audit
   - oracle activation
   - AbiTests expansion
8. **Drift Detection Between Versions** — `git diff --ignore-cr-at-eol`, oracle.cs report diff (`oracle-evidence-clangsharp.md` before/after), AbiTests pass counts, dynapi coherence percentage, Slopwatch.
9. **Linux-Canonical Generation** — docker container for production evidence (`docker/binding-generator.Dockerfile`); `--use-platform-header-shims` for Windows-local iteration only.
10. **Build-Host Configuration** — `spikes/binding-generators/Directory.Build.props` keeps spike projects isolated from production multi-TFM inheritance; references to root `Directory.Build.props` and `Directory.Packages.props`.
11. **References** — cross-link to constitution + implementation-notes + ADR-004.

## Execution Sequencing (Dependency Order)

Execution proceeds in this order; each step may be a single commit or grouped depending on scope. Multi-agent review checkpoints intercept the order at critical stages (see §"Critical Stage Review Checkpoints").

1. **Audit Phase** — read every file slated for retirement; produce per-file unique-info-landing tables (Rule 1). **Mandatory before any `git mv` / `git rm`.**
   - Files to audit: 4 in `docs/binding-autogen/`, 7 in `spikes/binding-generators/docs/` top-level (charter, handoff, closure summary, oracle-design + plan, satellite-expansion-roadmap, ppy-ref), 5 specs in `items/`, 3 in `testing/`, 4 in parking-lot, 2 playbooks, 7 in `satellites/`, 3 in `.github/prompts/`.
   - Output: consolidated unique-info-landing table covering ~35 files.
   - **GATE: Multi-agent review checkpoint 1 (post-audit)** before proceeding.

2. **Move Canonical Docs** — `git mv` atomic operations:
   - `docs/binding-autogen/README.md` → `spikes/binding-generators/docs/canonical/README.md`
   - `docs/binding-autogen/binding-generator-constitution.md` → `canonical/binding-generator-constitution.md`
   - `docs/binding-autogen/binding-generator-roadmap.md` → `canonical/binding-generator-roadmap.md`
   - `docs/binding-autogen/testing-strategy.md` → `canonical/testing-strategy.md`
   - `docs/playbook/binding-output-oracle-validation.md` → `canonical/binding-output-oracle-validation.md`
   - `spikes/binding-generators/docs/satellites/*` → `canonical/satellites/*` (7 files; including rename `sdl2-image-cross-check.md` → `sdl2-image-header-analysis.md`)
   - `spikes/binding-generators/docs/ppy-reference-analysis-2026-05-25.md` → `canonical/references/ppy-reference-analysis-2026-05-25.md`
   - Atomic moves preserve `git log --follow` history.

3. **Purify Content** (in newly-moved canonical/ docs):
   - **Constitution Q3C** — strip Toolchain re-evaluation banners + Generation Determinism implementation detail + Opaque Handles implementation mechanism + Current SDL2.Core ABI Status closure-record content + Foreign Type Boundary Policy disposition column + Known Stage 1 flag enums data + Generator Home path enumeration. Keep all WHY/HOW/WHAT durable principles.
   - **Roadmap Q4D** — collapse M0–M4 into single "What's Done" section; rewrite M5–M10 as Layer 2 / Layer 3 / Production Flip / SysWM Layout / Smoke Expansion / SDL3 Extension; cut Canonical References table, Operating Rules wall, Continuous Maintenance list, Retired Active References section.
   - **Testing-strategy Q5/Q9** — fold `testing/raw-abi-upstream-testing-spec.md` policy into existing sections (Categories table merges with Layer Model; Coverage Strategy + ABI risk families merge with Generated Raw ABI Runtime Tests; Upstream Feasibility Summary merges with SDL2 Core Research Summary); fold `testing/raw-abi-upstream-testing-plan.md` multi-agent slice tables into Implementation Backlog as new "Raw ABI Upstream Port Backlog" section; fold `testing/raw-abi-current-inventory.md` Known Generated Binding Blockers into deferred-coverage notes.
   - **README** — update for canonical/ home; refresh reading order pointing at new sibling docs.
   - **Oracle-validation playbook** — purify path/command references (replace `tools.cs generate-bindings` with toolchain-neutral language; point evidence-input rows at `oracle-evidence-clangsharp.md`).
   - **Satellites/ index** — confirm filename consistency; cross-link refresh.

4. **Write New Docs** — from audit folds:
   - `canonical/binding-generator-implementation-notes.md` (per content outline above)
   - `canonical/binding-generator-maintenance.md` (per content outline above)
   - **GATE: Multi-agent review checkpoint 2 (post-constitution-purification + implementation-notes drafting)** before proceeding.

5. **Restructure next-iteration-plan.md** — cut historical slice progress, oracle repair queue (closed), open questions (mostly stale); keep + expand review follow-up backlog; absorb forward backlog from `satellite-expansion-roadmap.md`; restructure around Layer 2 active iteration. After restructure, `satellite-expansion-roadmap.md` ready for retirement.
   - **GATE: Multi-agent review checkpoint 4 (post-roadmap-restructure + testing-strategy fold + next-iteration-plan restructure)** before proceeding to retirements.

6. **Update Upstream References** (Q14A.α):
   - **AGENTS.md L91** — rewrite Settled Strategic Decision paragraph (post-spike state, neutral toolchain formality per Q13A).
   - **docs/plan.md** Phase 4 section — path updates + Status banner refresh + Stage 1/2 work-item text refresh (checkboxes stay open).
   - **docs/release-strategy.md** — References list path updates + Stage 1/2 table minor cleanups + ensure Phase 4 sequencing rationale stays.
   - **ADR-004** (`docs/decisions/2026-05-14-binding-autogen-toolchain.md`) per Q13A — Status banner update, Re-evaluation note path refresh, Sections 1-6 untouched, Section 7 References path updates.
   - **docs/phases/phase-4-binding-autogen.md** per Q12 — purify in place (strip sunset Cake-impl paragraphs; refresh cross-refs; simplify Stage Sequencing).

7. **Retire Files** (after audit + folds confirmed, per Rule 1):
   - `git rm` `docs/parking-lot/binding-autogen-cake-implementation/` (4 files + folder).
   - `git rm` `docs/playbook/binding-generator-maintenance.md`.
   - `git rm` 5 retired spike top-level docs (`llm-handoff`, `priority-c-closure-summary`, `generator-spike-goals`, `oracle-evidence-design`, `oracle-evidence-implementation-plan`).
   - `git rm` `satellite-expansion-roadmap.md` after fold.
   - `git rm` 10 items/ files (5 plans + 5 specs) — folder removes.
   - `git rm` 3 testing/ files — folder removes.
   - `git rm` 3 `.github/prompts/` priming files.
   - `git rm` 2 legacy `output/reports/` files (`oracle-comparison-clangsharp.md`, `oracle-comparison-alimer.md`).
   - **GATE: Multi-agent review checkpoint 3 (post-parking-lot-retirement)** captures the audit-fold-retire result for M1/M2/M3 specifically.

8. **Refresh Spike README** — Status, Read first table, Layout tree, Decision recorded section, How a new agent should pick this up section.

9. **Inline Comment Spot-Check** — grep `spikes/binding-generators/clangsharp/postprocess/*.cs` for stale `docs/binding-autogen/` paths + line-number refs; update to section-name refs per Rule 3. Sunset Cake impl comments in `build/_build/Targets/GenerateBindings/*.cs` left for retirement-with-the-code (no systematic update).

10. **Audit .github/prompts/ + clangsharp/README.md** — read each; retire spike-specific priming prompts; update clangsharp/README.md cross-refs.

11. **Validate** — `git diff --check`, broken-link scan across all touched docs (manual + agent-assisted), doc reading-order coherence check.
    - **GATE: Multi-agent review checkpoint 6 (final state validation)** before commit.

12. **Commit** — present commit summary + proposed commit message per AGENTS.md §"Approval Gate". Wait for explicit approval.

## Critical Stage Review Checkpoints

Multi-agent review at the user-emphasized stages. Each checkpoint dispatches 2+ parallel agents with different review angles per `superpowers:dispatching-parallel-agents` pattern. Reviews run in parallel; reports consolidated; stage gates pass on review consensus or explicit user override.

### Checkpoint 1 — Post-Audit

**Stage:** After per-file unique-info-landing tables produced; before any retirements / moves.

**Review question:** Did the audit miss any unique-info kernel? Does every kernel have a confirmed destination doc?

**Agents to dispatch (in parallel):**

- One `Explore` agent: search the audited files for sections matching durable-pattern keywords (algorithms, decision tables, rule definitions, fixtures, evidence tables) that don't appear in the unique-info landing table.
- One `general-purpose` agent: cross-check the destination docs (constitution / implementation-notes outline / testing-strategy / satellites / maintenance outline) against the audit table for landing-place coverage.

**Exit criteria:** zero kernels without destination; review consensus or explicit user override.

### Checkpoint 2 — Post-Constitution Purification + Implementation-Notes Drafting

**Stage:** After Q3C aggressive purification + new `binding-generator-implementation-notes.md` drafted.

**Review question:** Did all durable principles survive in the constitution? Are mechanism-vs-policy boundaries clean? Did anything that should be policy end up in implementation-notes (or vice versa)?

**Agents to dispatch (in parallel):**

- One `Explore` agent: compare the purified constitution against the pre-purification version for missing policy content (use git history).
- One `general-purpose` agent: read the new implementation-notes draft and flag any content that's actually policy (should be in constitution) vs mechanism (correctly placed here).

**Exit criteria:** policy-vs-mechanism boundary defensible; no durable policy lost.

### Checkpoint 3 — Post-Parking-Lot Audit (User-Emphasized in Q10)

**Stage:** After M1/M2/M3 plan audit + CppAst-independent fold.

**Review question:** Were toolchain-neutral features genuinely extracted and adapted to the ClangSharp + Roslyn shape? Is the M3 satellite reconnaissance / engine-policy-boundary content properly landed in the right destination docs?

**Agents to dispatch (in parallel):**

- One `Explore` agent: read M1/M2/M3 plans in full; extract every CppAst-independent feature/pattern; compare against the destination doc landing (constitution / implementation-notes / testing-strategy / satellites / maintenance).
- One `general-purpose` agent: verify the new maintenance playbook adequately adapted trio-pinning + satellite-enablement-checklist + parse-options-audit patterns from the sunset playbook's structure.

**Exit criteria:** every CppAst-independent feature has a documented landing place; user-emphasized "no blind retirement" gate satisfied.

### Checkpoint 4 — Post-Roadmap Restructure + Testing-Strategy Fold + Next-Iteration-Plan Restructure

**Stage:** After Q4D + Q5/Q9 + next-iteration-plan restructure.

**Review question:** Is the layer-based forward direction complete and self-consistent? Did the testing/ fold preserve Categories table + Coverage Strategy + Upstream Feasibility + multi-agent slice tables? Did next-iteration-plan absorb the forward backlog cleanly?

**Agents to dispatch (in parallel):**

- One `Explore` agent: cross-check the new roadmap forward milestones against constitution Layer Contract + production-flip prerequisites + SDL3 gating (PD-7) for completeness.
- One `general-purpose` agent: verify the testing-strategy fold preserved every anchor identified in Q5/Q9 (Coverage Strategy targets, ABI risk families, Upstream Feasibility Summary, agent slice tables).

**Exit criteria:** roadmap forward direction validated; testing anchors confirmed preserved; next-iteration-plan reflects current Layer 2 state.

### Checkpoint 5 — Post-New-Maintenance-Playbook Drafting

**Stage:** After `canonical/binding-generator-maintenance.md` is drafted.

**Review question:** Does the new playbook cover everything needed for actual maintenance? Version bumps, RSP edits, family-config.json edits, drift detection, new-family addition checklist all present and actionable?

**Agents to dispatch (in parallel):**

- One `Explore` agent: cross-check the new playbook against `family-config.json` schema for field-by-field coverage.
- One `general-purpose` agent: simulate a hypothetical maintenance task (e.g., "bump SDL2 to 2.x.y") and walk through the playbook to identify missing steps.

**Exit criteria:** maintenance playbook covers the version-bump procedures, family-config schema, RSP maintenance, postprocess pipeline maintenance, drift detection, new-family addition.

### Checkpoint 6 — Final State Validation

**Stage:** After all changes land, before commit.

**Review question:** Broken links across the touch set? Doc reading order coherent? Cross-references consistent across upstream docs (AGENTS.md, plan.md, release-strategy.md, ADR-004, phase-4) + spike docs?

**Agents to dispatch (in parallel):**

- One `Explore` agent: scan all touched docs for broken Markdown links + stale path references + line-number cross-refs that should have become section-name refs.
- One `general-purpose` agent: read the final canonical/ tree top-to-bottom (README → constitution → roadmap → testing-strategy → implementation-notes → maintenance → oracle-validation → satellites/) and report any contradiction or unclear reading flow.

**Exit criteria:** zero broken links; cross-references consistent; reading order coherent; ready for commit per AGENTS.md §"Approval Gate".

## Exit Gates (Aggregate Across All Stages)

- All 15 decisions reflected in the doc tree.
- Every retired file has a recorded unique-info-landing table entry; no kernel lost (Rule 1 satisfied).
- CppAst-independent patterns folded into ClangSharp+Roslyn-adapted docs (Rule 2 satisfied).
- Section-name cross-references replace line-number refs across all touched docs (Rule 3 satisfied).
- Constitution is pure principles (~350 lines target).
- Roadmap is layer-based forward milestones (M-numbering retired throughout the repo).
- All 6 multi-agent review checkpoints passed (consensus or explicit user override).
- Spike README accurately reflects current state.
- AGENTS.md L91 + plan.md Phase 4 + release-strategy.md + ADR-004 + phase-4 doc all consistent on post-spike state.
- `git diff --check` clean.
- Slopwatch clean (per AGENTS.md skill instructions).

## Out of Scope (Explicitly Deferred)

- **Formal ADR-004 amendment / ADR-005 supersession to ClangSharp** — deferred to post-Layer-2 closure per user message ("final decision after Layer 1/2").
- **Production flip** (committing `src/Janset.SDL2.*/Generated/` + retiring `external/sdl2-cs`) — separate work item (Roadmap Production Flip layer).
- **Restoring canonical/ contents back to `docs/binding-autogen/` at production flip** — that's a future re-relocation, not part of this consolidation.
- **Layer 2 typed public API design** — separate brainstorm + spec cycle.
- **Sunset Cake-impl code deletion** (`build/_build/Targets/GenerateBindings/`) — deferred to Production Flip.
- **Inline comment systematic refresh in sunset Cake impl** — those comments retire with the code at Production Flip.
- **SDL3 doc tree** — gated on PD-7 per AGENTS.md + release-strategy.

## References

### ADRs

- [ADR-002 — Target-centric build-host pattern](../../decisions/2026-05-05-target-centric-build-host.md) — the Cake-host fold inherits this pattern; the new ClangSharp+Roslyn implementation will need its own equivalent architectural ADR when production-flip lands.
- [ADR-003 — Contract-centric data layer](../../decisions/2026-05-12-build-host-data-layer.md) — binding-generation data contracts under `build/_build/Data/BindingGeneration/` extend this pattern.
- [ADR-004 — Binding Auto-Generation Toolchain](../../decisions/2026-05-14-binding-autogen-toolchain.md) — currently Reopened; this consolidation refreshes its status banner but defers formal amendment per user direction.

### Knowledge-Base

- [`docs/knowledge-base/extraction-guidelines.md`](../../knowledge-base/extraction-guidelines.md) — collaborator extraction discipline; applies to implementation-notes mechanism descriptions where they cite extracted collaborator classes.
- [`docs/knowledge-base/testing-guidelines.md`](../../knowledge-base/testing-guidelines.md) — canonical TUnit/MTP test infrastructure; testing-strategy purification defers to this for build-host test infrastructure.

### Canonical Workstream Docs (Current State Before This Consolidation)

- [`docs/binding-autogen/binding-generator-constitution.md`](../../binding-autogen/binding-generator-constitution.md) — ABI/API policy authority (Q3C target).
- [`docs/binding-autogen/binding-generator-roadmap.md`](../../binding-autogen/binding-generator-roadmap.md) — milestone plan (Q4D target).
- [`docs/binding-autogen/testing-strategy.md`](../../binding-autogen/testing-strategy.md) — testing layer model (Q5/Q9 target).

### Spike Top-Level Docs (Current State)

- [`spikes/binding-generators/docs/llm-handoff.md`](../../../spikes/binding-generators/docs/llm-handoff.md) — Q6 retire.
- [`spikes/binding-generators/docs/priority-c-closure-summary.md`](../../../spikes/binding-generators/docs/priority-c-closure-summary.md) — Q6 retire.
- [`spikes/binding-generators/docs/next-iteration-plan.md`](../../../spikes/binding-generators/docs/next-iteration-plan.md) — Q6 restructure.
- [`spikes/binding-generators/docs/satellite-expansion-roadmap.md`](../../../spikes/binding-generators/docs/satellite-expansion-roadmap.md) — Q6 fold-then-retire.
- [`spikes/binding-generators/docs/ppy-reference-analysis-2026-05-25.md`](../../../spikes/binding-generators/docs/ppy-reference-analysis-2026-05-25.md) — Q6 move to canonical/references/.

### Settled Constraints From This Repo

- AGENTS.md §"Settled Strategic Decisions" — `Binding autogen replaces SDL2-CS` row will be rewritten per Q14A.
- AGENTS.md §"Docs-First Workflow" — conflict resolution rule (code wins over docs for runtime; canonical docs win for strategic decisions).
- AGENTS.md §"Approval Gate" — explicit approval required before any commit; this consolidation produces a single commit (or series) per the approval gate discipline.

### User Memory (Standing Preferences Applied)

- *"Refactoring doc lifecycle: docs/refactoring/ mixes durable plan/ADR/checklist with temp per-slice design specs that delete after the slice ships"* — applies to items/ disposition (Q7).
- *"Preserve unique information... less historic, forward-looking"* — applies to all retirements (Rule 1).
- *"Plan-doc reference discipline — every plan/phase/refactor doc must cross-reference relevant ADRs + knowledge-base guidelines"* — this spec cross-references ADR-002/003/004 + extraction-guidelines + testing-guidelines.
- *"Follow explicit paths — when Deniz names a path/folder, use it exactly"* — applies to Q1 topology decision (user named `docs/binding-autogen/` and proposed move to spike; spec respects that even though my Q1 recommendation pushed back).
- *"No workarounds/shortcuts; no punting when solvable"* — applies to audit gates (Rule 1 strict).
- *"Single changeset via branch+squash"* — implementation plan should land as one squash commit per memory.
