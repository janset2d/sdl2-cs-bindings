# Binding-Autogen Doc Consolidation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Execute the binding-autogen documentation consolidation per the [2026-05-28 design spec](../specs/2026-05-28-binding-autogen-doc-consolidation-design.md).

**Architecture:** 9 phases over a 51-file change set with 6 multi-agent review checkpoints. Audit-first (no mutation until unique-info kernels have confirmed destinations); move atomically; purify content; write new docs; restructure forward-tracking; update upstream refs; retire files; refresh spike README; validate + commit.

**Tech Stack:** git (mv, rm, diff, log --follow, blame -C), markdownlint, Slopwatch (`slopwatch analyze --fail-on warning --exclude "artifacts/**,external/**,vcpkg_installed/**,spikes/binding-generators/references/**,**/bin/**,**/obj/**"`), parallel agent dispatch (Explore + general-purpose subagents). No compiler / test runner — doc-only consolidation.

**Spec references:** [Design spec §"Three Standing Rules"](../specs/2026-05-28-binding-autogen-doc-consolidation-design.md) (Rule 1 audit gate, Rule 2 CppAst-independent fold, Rule 3 section-name cross-refs), §"Critical Stage Review Checkpoints" (6 checkpoints), §"Target Doc Topology" (final tree).

**Commit cadence:** WIP commit after each phase per user memory "single changeset via branch+squash" — landing commit at end uses `git merge --squash` from this branch. No `--no-verify`; no force-push.

---

## File Structure

Files created during execution (alongside the moves/edits enumerated in spec §"File-Level Change Inventory"):

| Path | Purpose |
| --- | --- |
| `spikes/binding-generators/docs/canonical/binding-generator-implementation-notes.md` | NEW — sibling to purified constitution; mechanism + evidence + classification labels + hardcoding rules + cross-cutting iteration-2 facts |
| `spikes/binding-generators/docs/canonical/binding-generator-maintenance.md` | NEW — ClangSharp+Roslyn-adapted maintenance playbook (replaces sunset version) |
| `spikes/binding-generators/docs/canonical/references/` | NEW subfolder — peer reference analyses |
| `spikes/binding-generators/docs/canonical/satellites/` | NEW subfolder — per-family + cross-family satellite analyses |
| `docs/superpowers/audits/2026-05-28-binding-autogen-audit-landing-tables.md` | NEW — Phase 0 output (audit gate Rule 1 evidence) |

Files moved (atomic `git mv` to preserve history):

- `docs/binding-autogen/README.md` → `spikes/binding-generators/docs/canonical/README.md`
- `docs/binding-autogen/binding-generator-constitution.md` → `spikes/binding-generators/docs/canonical/binding-generator-constitution.md`
- `docs/binding-autogen/binding-generator-roadmap.md` → `spikes/binding-generators/docs/canonical/binding-generator-roadmap.md`
- `docs/binding-autogen/testing-strategy.md` → `spikes/binding-generators/docs/canonical/testing-strategy.md`
- `docs/playbook/binding-output-oracle-validation.md` → `spikes/binding-generators/docs/canonical/binding-output-oracle-validation.md`
- `spikes/binding-generators/docs/ppy-reference-analysis-2026-05-25.md` → `spikes/binding-generators/docs/canonical/references/ppy-reference-analysis-2026-05-25.md`
- `spikes/binding-generators/docs/satellites/sdl2-image-cross-check.md` → `spikes/binding-generators/docs/canonical/satellites/sdl2-image-header-analysis.md` (rename)
- Other 6 satellites/ files → `spikes/binding-generators/docs/canonical/satellites/` (same filename)

Files retired:

- `docs/parking-lot/binding-autogen-cake-implementation/` (4 files + folder)
- `docs/playbook/binding-generator-maintenance.md` (sunset)
- 6 spike top-level docs (after fold)
- 10 spike `items/` files
- 3 spike `testing/` files (after fold)
- 3 `.github/prompts/` priming files
- 2 `output/reports/` legacy files

Files modified in place:

- `spikes/binding-generators/README.md`
- `AGENTS.md` (L91 paragraph)
- `docs/plan.md` (Phase 4 section)
- `docs/release-strategy.md` (refs + stages)
- `docs/decisions/2026-05-14-binding-autogen-toolchain.md` (status + paths)
- `docs/phases/phase-4-binding-autogen.md` (purify)

---

## Phase 0 — Audit (No Mutation)

**Goal:** Produce per-file unique-info-landing tables for every retiree before any `git rm` or `git mv`. Output is a single audit doc.

**Output file:** `docs/superpowers/audits/2026-05-28-binding-autogen-audit-landing-tables.md`

**Audit template per file:**

```text
### File: <path>

**Sections audited:**
- <section heading>: <unique-info kernel description> → <destination doc + section>
- <section heading>: duplicate of <other doc + section> → no fold needed
- <section heading>: sunset-specific / no durable kernel → no fold needed

**Verdict:** ready to git rm | additional fold needed
```

### Task 0.1: Bootstrap audit doc

**Files:**

- Create: `docs/superpowers/audits/2026-05-28-binding-autogen-audit-landing-tables.md`

- [ ] **Step 1: Create the audit file skeleton**

Write to `docs/superpowers/audits/2026-05-28-binding-autogen-audit-landing-tables.md`:

```markdown
# Binding-Autogen Doc Consolidation — Audit Landing Tables

**Date:** 2026-05-28
**Status:** In progress
**Scope:** Phase 0 evidence for the [doc consolidation design spec](../specs/2026-05-28-binding-autogen-doc-consolidation-design.md) Rule 1 audit gate.

## Purpose

Per-file unique-info-landing tables for every retiree. No `git rm` until each retired file has a verdict + destination map for its unique kernels.

## Audit Format

Each entry follows:

\`\`\`text
### File: <path>

**Sections audited:**
- <section heading>: <unique kernel description> → <destination doc + section>
- <section heading>: duplicate of <other doc + section> → no fold needed
- <section heading>: sunset-specific / no durable kernel → no fold needed

**Verdict:** ready to git rm | additional fold needed
\`\`\`

## Audit Entries

(Entries added per Task 0.2–0.9 below.)
```

- [ ] **Step 2: Commit the audit skeleton**

```pwsh
git add docs/superpowers/audits/2026-05-28-binding-autogen-audit-landing-tables.md
git commit -m "docs(audit): bootstrap Phase 0 audit landing tables for binding-autogen doc consolidation

Per Phase 0 of docs/superpowers/specs/2026-05-28-binding-autogen-doc-consolidation-design.md.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

### Task 0.2: Audit `docs/parking-lot/binding-autogen-cake-implementation/` (4 files)

**Files (read-only):**

- `docs/parking-lot/binding-autogen-cake-implementation/README.md`
- `docs/parking-lot/binding-autogen-cake-implementation/milestone-1-safety-harness-baseline.md`
- `docs/parking-lot/binding-autogen-cake-implementation/milestone-2-behavior-preserving-topology-refactor.md`
- `docs/parking-lot/binding-autogen-cake-implementation/milestone-3-profile-boundary.md`

**Files modified:**

- `docs/superpowers/audits/2026-05-28-binding-autogen-audit-landing-tables.md` (append entries)

- [ ] **Step 1: Read parking-lot README in full**

Use Read tool on `docs/parking-lot/binding-autogen-cake-implementation/README.md`. Identify the 3 preservation reasons (git continuity, toolchain-neutral intent, M4-M10 cross-references) and verify they no longer apply post-consolidation (per spec §Q10 reasoning).

- [ ] **Step 2: Read M1 plan in full**

Use Read tool on `milestone-1-safety-harness-baseline.md`. Per spec §"Rule 2 CppAst-Independent Fold", look specifically for:
- Safety-harness pattern (snapshot-first / embedded `.h` fixtures / compile-check gates / RED/GREEN discipline) — toolchain-neutral, fold into `testing-strategy.md`.
- Cake-impl-specific test infrastructure decisions — sunset, no fold.
- Any other unique kernel.

- [ ] **Step 3: Read M2 plan in full**

Use Read tool on `milestone-2-behavior-preserving-topology-refactor.md`. Look for:
- Parser/model/emitter separation principle — toolchain-neutral; verify already in constitution §"Generator Engine And SDL Policy" + verify implementation-notes outline covers it.
- Cake-impl folder layout (`Targets/GenerateBindings/<concept>/`) — sunset, no fold.

- [ ] **Step 4: Read M3 plan in full (user-emphasized scrutiny)**

Use Read tool on `milestone-3-profile-boundary.md`. Per spec §Q10 emphasis ("Ama bak iyi audit edelim CppAst bağımsız burada işimize yaracak feature'lar olabilir, mutlaka onları alalım adapte edelim, blindly retire etmek yok kesinlikle"), scrutinize carefully:
- Engine-owned concepts list → cross-check constitution coverage.
- Manifest-owned facts list → cross-check constitution §"Manifest Configuration Vs Code-Owned Policy" coverage.
- Code-owned policy list → cross-check constitution coverage.
- Profile direction (`sdl2-core` / `sdl2-satellite` / `sdl2-gfx` / `sdl3-core` / `sdl3-satellite`) → cross-check Family Identity table coverage.
- M2 review findings ("TypeMappingPolicy mixes generic primitive-width mapping..." / "LegacyBindingTypeRefBridge..." / "ExternalNativeTypePolicy is foreign-type policy..." / "PlatformCatalog.AllPlatformMacros is SDL2.Core-specific...") → identify general-principle survivors → fold into constitution Foreign Type Boundary Policy + implementation-notes.
- Satellite reconnaissance findings (Image: 59 declarations, Mixer: 97, Ttf: 85, Gfx headers split) → cross-check `spikes/binding-generators/docs/satellites/sdl2-*-header-analysis.md` for completeness; fold gaps into appropriate satellite analysis.

- [ ] **Step 5: Append parking-lot audit entries**

Append to `docs/superpowers/audits/2026-05-28-binding-autogen-audit-landing-tables.md`:

```markdown
## Parking Lot (docs/parking-lot/binding-autogen-cake-implementation/)

### File: docs/parking-lot/binding-autogen-cake-implementation/README.md

**Sections audited:**
- Header + 3 preservation reasons: <verdict on each per spec §Q10>
- Archived plans table: pointer-only, retires with the parking-lot folder

**Verdict:** ready to git rm

### File: docs/parking-lot/binding-autogen-cake-implementation/milestone-1-safety-harness-baseline.md

**Sections audited:**
<per Step 2 findings — list each unique kernel + destination>

**Verdict:** ready to git rm | additional fold needed (specify)

### File: docs/parking-lot/binding-autogen-cake-implementation/milestone-2-behavior-preserving-topology-refactor.md

**Sections audited:**
<per Step 3 findings>

**Verdict:** ready to git rm | additional fold needed (specify)

### File: docs/parking-lot/binding-autogen-cake-implementation/milestone-3-profile-boundary.md

**Sections audited:**
<per Step 4 findings — extra scrutiny per user Q10 emphasis>

**Verdict:** ready to git rm | additional fold needed (specify)
```

Replace `<...>` placeholders with actual findings from Steps 1–4. **Do not leave any `<...>` placeholders in the committed file.**

### Task 0.3: Audit `docs/binding-autogen/` (4 files)

**Files (read-only):**

- `docs/binding-autogen/README.md`
- `docs/binding-autogen/binding-generator-constitution.md`
- `docs/binding-autogen/binding-generator-roadmap.md`
- `docs/binding-autogen/testing-strategy.md`

**Note:** These files **move** to `canonical/` (not retire). Audit identifies content that gets **cut during purification** (Phase 2), not content that needs an external destination. Audit produces a "what stays / what cuts" map per file.

**Files modified:**

- `docs/superpowers/audits/2026-05-28-binding-autogen-audit-landing-tables.md` (append entries)

- [ ] **Step 1: Audit constitution Q3C aggressive purification scope**

Use Read tool on `docs/binding-autogen/binding-generator-constitution.md` (645 lines; read in two passes via offset/limit if needed). Per spec §Q3C, identify:
- **Stays in constitution:** Layer Contract, scalar mapping, opaque handles concept (Pattern B principle, force-opaque allow-list rule, cross-assembly DisableRuntimeMarshalling rule), foreign-type boundary principle, structs/unions rules, enum policy, macro policy, platform views, evidence gates, BCL-Replaceable Helper Exclusion Policy, C variadics, Maintenance Rule.
- **Cuts to implementation-notes:** Generation Determinism Contract enumerated pin set + verification commands + family isolation specifics; "Implementation mechanism (ClangSharp + Roslyn postprocess)" subsections under Opaque Handles; Current SDL2.Core ABI Status closure-record content; Foreign Type Boundary Policy disposition table column; Known Stage 1 flag enums data; Generator Home path enumeration.
- **Pure cuts:** Status banners ("Toolchain re-evaluation 2026-05-23"); implementation-specific paragraphs at section heads.

- [ ] **Step 2: Audit roadmap Q4D layer-based restructure scope**

Use Read tool on `docs/binding-autogen/binding-generator-roadmap.md`. Identify:
- **Collapses to "What's Done":** M0 / M1 / M2 / M3 (subsumed by spike) / M4 (closed by spike).
- **Becomes layer-based forward milestones:** M5 → Layer 2 Typed Public API; M6 → Layer 3 Friendly Overloads; M7 → Production Flip; M8 → SysWM Layout + Satellite Sweep close; M9 → Smoke Expansion; M10 → SDL3 Extension.
- **Cuts entirely:** Canonical References And Research Baseline table (covered by canonical README); Operating Rules wall (already in AGENTS.md + constitution); Continuous Maintenance list (folds into maintenance playbook).

- [ ] **Step 3: Audit testing-strategy Q5/Q9 fold scope**

Use Read tool on `docs/binding-autogen/testing-strategy.md` (645 lines). Identify:
- **Preserved anchors** (per spec §Q5): Layer Model table, Upstream Adoption Policy, SDL2 Core + Satellite Research Summaries, Initial Coverage Matrix, fixture policy, flakiness rules, research-sources URL list.
- **Cuts:** Status banners; sunset Cake paths (`build/_build.Tests/...`, `tests/binding-compile-check/SDL2.Core.CompileCheck.csproj`); Refactor Gate Mapping M-numbering (rewrite to layer-based).
- **Fold targets** (per spec §Q9): incoming content from `spike docs/testing/raw-abi-upstream-testing-spec.md` (Categories table merges with Layer Model; Coverage Strategy + ABI risk families merge with Generated Raw ABI Runtime Tests section; Upstream Feasibility Summary merges with SDL2 Core Research Summary); incoming from `raw-abi-upstream-testing-plan.md` (multi-agent slice tables → new "Raw ABI Upstream Port Backlog" subsection under Implementation Backlog); incoming from `raw-abi-current-inventory.md` (Known Generated Binding Blockers → deferred-coverage notes).

- [ ] **Step 4: Audit README docs/binding-autogen/ purification scope**

Use Read tool on `docs/binding-autogen/README.md`. Identify:
- Folder Layout table → update target paths.
- Reading Order table → update target paths + add new sibling docs.
- Current Decision Posture → refresh to post-spike state.
- Related Canonical Docs → path updates.
- Sunset-specific banner content → cut.

- [ ] **Step 5: Append docs/binding-autogen/ audit entries**

Append per the same template as Task 0.2 Step 5. Verdict per file: **ready to git mv (purification scope recorded for Phase 2 execution)**.

### Task 0.4: Audit spike top-level docs (6 retiree files)

**Files (read-only):**

- `spikes/binding-generators/docs/llm-handoff.md`
- `spikes/binding-generators/docs/priority-c-closure-summary.md`
- `spikes/binding-generators/docs/generator-spike-goals.md`
- `spikes/binding-generators/docs/oracle-evidence-design.md`
- `spikes/binding-generators/docs/oracle-evidence-implementation-plan.md`
- `spikes/binding-generators/docs/satellite-expansion-roadmap.md`

**Files modified:**

- `docs/superpowers/audits/2026-05-28-binding-autogen-audit-landing-tables.md` (append entries)

- [ ] **Step 1: Audit llm-handoff.md**

Per spec §Q6: working preferences already in user memory; code map auto-discoverable; slice progress overlaps with satellite-expansion-roadmap; anti-patterns already in user memory; cross-refs table → spike README refresh absorbs it. **Verdict: ready to git rm — empty unique kernel after spec §Q6 audit.**

- [ ] **Step 2: Audit priority-c-closure-summary.md**

Per spec §Q6: durable policy (Pattern B, C `long` hybrid, `wchar_t*` opaque, force-opaque allow-list, foreign-type boundary, BCL-replaceable helper rule, cross-assembly `DisableRuntimeMarshalling`) all in constitution; commit chain + verification table reconstructible from `git log --follow`. **Verdict: ready to git rm — empty unique kernel.**

- [ ] **Step 3: Audit generator-spike-goals.md**

Per spec §Q6 retire-after-fold: hardcoding rules section (good config-owned facts vs bad patterns) → fold into `implementation-notes.md` per spec content outline §11. Decision evidence already in `output/reports/iteration-2-comparison.md` + ADR-004. Methodology rules already in constitution. **Verdict: ready to git rm after fold — fold hardcoding rules into implementation-notes Phase 3.1.**

- [ ] **Step 4: Audit oracle-evidence-design.md**

Per spec §Q6: design intent implemented in `oracle.cs`. **Classification labels** (`Hard Bug` / `Likely Bug` / `Accepted Deferral` / `Compatibility Risk` / `Evidence Missing` / `Out Of Scope`) → fold into `implementation-notes.md` content outline §10. Source Hierarchy / Structured Model / Roslyn Extraction Policy / Report Shape / Initial Raw ABI Checks → mostly covered by oracle-validation playbook. **Verdict: ready to git rm after fold — fold classification labels into implementation-notes Phase 3.1.**

- [ ] **Step 5: Audit oracle-evidence-implementation-plan.md**

Per spec §Q6: tasks completed; `oracle.cs` is the canonical artifact. Pure execution scaffolding. **Verdict: ready to git rm — empty unique kernel.**

- [ ] **Step 6: Audit satellite-expansion-roadmap.md (after fold)**

Per spec §Q6 retire-after-fold:
- **Closure facts for Items 1-5 + Iteration 2** → `git log --follow` is the archive.
- **Cross-cutting facts:** family-config.json structure, postprocess pipeline order, oracle structure → fold into `implementation-notes.md` content outline §§12-13.
- **Forward backlog** ("What This Roadmap Does NOT Cover" + "Review Follow-up Backlog" sections) → fold into `next-iteration-plan.md` via Phase 4 restructure.
- **Satellite reconnaissance findings** (Image/Mixer/Ttf/Gfx header counts) → cross-check against `canonical/satellites/*-header-analysis.md` for completeness; fold any gaps into the appropriate per-family analysis.

**Verdict: ready to git rm after fold — fold cross-cutting into implementation-notes (Phase 3.1) + forward backlog into next-iteration-plan (Phase 4) + reconnaissance gap fill into satellites (Phase 2.6).**

- [ ] **Step 7: Append spike top-level audit entries**

Append entries per the template, replacing all `<...>` with actual findings.

### Task 0.5: Audit spike `items/` folder (10 files)

**Files (read-only):**

- `spikes/binding-generators/docs/items/item-1-per-library-generation-{plan,spec}.md`
- `spikes/binding-generators/docs/items/iteration-2-config-surface-unification-{plan,spec}.md`
- `spikes/binding-generators/docs/items/item-3-sdl2-gfx-layer-1-{plan,spec}.md`
- `spikes/binding-generators/docs/items/item-4-sdl2-ttf-layer-1-{plan,spec}.md`
- `spikes/binding-generators/docs/items/item-5-sdl2-mixer-layer-1-{plan,spec}.md`

**Files modified:**

- `docs/superpowers/audits/2026-05-28-binding-autogen-audit-landing-tables.md` (append entries)

- [ ] **Step 1: Audit all 5 plan files**

Use Read tool on each. Per spec §Q7: plans are execution scaffolding (task lists, RED/GREEN, verification commands). **Verdict per plan: ready to git rm — empty unique kernel.** Single batch verdict acceptable.

- [ ] **Step 2: Audit item-1-per-library-generation-spec.md**

Per spec §Q7 specific concern: contains design rationale for `OpaqueHandleEmitRewriter` auto-detect criterion, owner/consumer mode design, postprocess pipeline ordering. Cross-check `implementation-notes.md` content outline §§3, 13 for coverage. **Verdict: ready to git rm after fold — confirm fold targets.**

- [ ] **Step 3: Audit iteration-2-config-surface-unification-spec.md**

Per spec §Q7 specific concern: 18-source pre-unification config inventory + classification against Constitution §"Manifest Configuration Vs Code-Owned Policy" → fold into `implementation-notes.md` content outline §12 ("Config Surface Evolution"). **Verdict: ready to git rm after fold.**

- [ ] **Step 4: Audit item-3-sdl2-gfx-layer-1-spec.md**

Cross-check `canonical/satellites/sdl2-gfx-header-analysis.md` for any unique design decisions not captured (`SDL2_*_SCOPE` export macros, `M_PI` duplicate handling, mixed naming `pixelColor` / `SDL_imageFilter*` / `SDL_initFramerate`). **Verdict: ready to git rm after fold if any gap found.**

- [ ] **Step 5: Audit item-4-sdl2-ttf-layer-1-spec.md**

Cross-check `canonical/satellites/sdl2-ttf-header-analysis.md` for coverage of: `TTF_Font` opaque handle, C `long` surface (5 symbols), no-SDLCALL Cdecl handling, deprecated exclusions, error macro aliases. Also check `clong-dispatch` design rationale → cross-check implementation-notes §4. **Verdict: ready to git rm after fold.**

- [ ] **Step 6: Audit item-5-sdl2-mixer-layer-1-spec.md**

Cross-check `canonical/satellites/sdl2-mixer-header-analysis.md` for coverage of: `Mix_Music` opaque, `Mix_Chunk` transparent struct, 6 callback typedefs, `MIX_InitFlags` decoration, error macro exclusions, version macro handling. **Verdict: ready to git rm after fold.**

- [ ] **Step 7: Append items/ audit entries**

Append entries per template. All 5 plans share single batch verdict.

### Task 0.6: Audit spike `testing/` folder (3 files)

**Files (read-only):**

- `spikes/binding-generators/docs/testing/raw-abi-current-inventory.md`
- `spikes/binding-generators/docs/testing/raw-abi-upstream-testing-spec.md`
- `spikes/binding-generators/docs/testing/raw-abi-upstream-testing-plan.md`

**Files modified:**

- `docs/superpowers/audits/2026-05-28-binding-autogen-audit-landing-tables.md` (append entries)

- [ ] **Step 1: Audit raw-abi-current-inventory.md**

Per spec §Q9: point-in-time test counts (712 tests, 178 per TFM) volatile and disposable. Per-file deferred-blockers list → fold into testing-strategy deferred-coverage section. **Verdict: ready to git rm after fold.**

- [ ] **Step 2: Audit raw-abi-upstream-testing-spec.md**

Per spec §Q9: durable policy with overlap to testing-strategy. Categories table, Coverage Strategy targets, ABI risk families, Upstream Feasibility Summary, high-value P0/P1 testautomation_*.c source list → fold into purified testing-strategy via Phase 2.3. **Verdict: ready to git rm after fold.**

- [ ] **Step 3: Audit raw-abi-upstream-testing-plan.md**

Per spec §Q9: multi-agent slice tables (Phase 2-5) → fold into testing-strategy "Raw ABI Upstream Port Backlog" subsection (under Implementation Backlog). **Verdict: ready to git rm after fold.**

- [ ] **Step 4: Append testing/ audit entries**

Append entries per template.

### Task 0.7: Audit playbooks (1 retiree)

**Files (read-only):**

- `docs/playbook/binding-generator-maintenance.md` (sunset; retire)
- `docs/playbook/binding-output-oracle-validation.md` (moves to canonical/; purification audit)

**Files modified:**

- `docs/superpowers/audits/2026-05-28-binding-autogen-audit-landing-tables.md` (append entries)

- [ ] **Step 1: Audit binding-generator-maintenance.md (sunset)**

Per spec §Q11 + Q11 expansion: identify CppAst-independent maintenance patterns:
- Trio pinning concept → adapt for ClangSharp + libclang + Microsoft.CodeAnalysis in NEW maintenance playbook.
- Parse-options audit discipline → adapt to RSP-file review in NEW playbook.
- Dynapi cross-check workflow → adapt to oracle.cs cross-check in NEW playbook.
- Satellite enablement checklist → adapt to family-config.json + RSP + csproj checklist in NEW playbook.
- Profile boundary maintenance → adapt.
- Local generation loop commands → replace with ClangSharp commands.

**Verdict: ready to git rm after the NEW playbook is written (Phase 3.2) and absorbs the CppAst-independent patterns.**

- [ ] **Step 2: Audit binding-output-oracle-validation.md (move + purify)**

Per spec §Q11: toolchain-neutral methodology. Cuts during purification (Phase 2.5):
- Path/command references using `tools.cs generate-bindings` → toolchain-neutral language.
- Evidence-input rows pointing at sunset `parse-views.json` → point at `oracle-evidence-clangsharp.md`.
- Existing Evidence Inputs table: review each row's path; update sunset references.

**Verdict: ready to git mv to canonical/ (purification scope recorded for Phase 2.5).**

- [ ] **Step 3: Append playbook audit entries**

Append entries per template.

### Task 0.8: Audit `.github/prompts/` (3 retirees)

**Files (read-only):**

- `.github/prompts/binding-generator-spike-priority-c-handoff.prompt.md`
- `.github/prompts/satellite-expansion-research-cross-check.prompt.md`
- `.github/prompts/item-4-closure-item-5-mixer-handoff.prompt.md`

**Files modified:**

- `docs/superpowers/audits/2026-05-28-binding-autogen-audit-landing-tables.md` (append entries)

- [ ] **Step 1: Spot-check the 3 priming prompts**

Use Read tool on each. Per spec §Q15 amendment: spike-specific priming prompts retire because their work (Priority C closure, satellite research, items 4-5 handoff) is done. **Verdict per prompt: ready to git rm — empty unique kernel.**

- [ ] **Step 2: Append .github/prompts/ audit entries**

Append entries per template. Single batch verdict acceptable.

### Task 0.9: Audit `output/reports/` legacy (2 retirees)

**Files (read-only):**

- `spikes/binding-generators/output/reports/oracle-comparison-clangsharp.md`
- `spikes/binding-generators/output/reports/oracle-comparison-alimer.md`

**Files modified:**

- `docs/superpowers/audits/2026-05-28-binding-autogen-audit-landing-tables.md` (append entries)

- [ ] **Step 1: Spot-check both legacy reports**

Use Read tool on each. Per spec §Q15e + current spike README L115-116: both flagged "legacy, not regenerated". Superseded by `oracle-evidence-clangsharp.md` (active oracle.cs output) + `iteration-2-comparison.md` (comparison evidence). **Verdict per report: ready to git rm — empty unique kernel.**

- [ ] **Step 2: Append output/reports/ audit entries**

Append entries per template.

### Task 0.10: Audit unread prompt + clangsharp/README

**Files (read-only):**

- `.github/prompts/general-deep-dive-code-reviewer.prompt.md` (verify keep)
- `.github/prompts/session-pickup-template.prompt.md` (verify keep)
- `spikes/binding-generators/clangsharp/README.md` (needs path-refresh audit)

**Files modified:**

- `docs/superpowers/audits/2026-05-28-binding-autogen-audit-landing-tables.md` (append entries)

- [ ] **Step 1: Verify general templates are genuinely generic**

Read both `.github/prompts/general-deep-dive-code-reviewer.prompt.md` and `session-pickup-template.prompt.md`. Confirm they have no binding-autogen-specific content. **Verdict: keep as-is (no edit).**

- [ ] **Step 2: Audit clangsharp/README.md path refs**

Read `spikes/binding-generators/clangsharp/README.md`. Identify all references to:
- `docs/binding-autogen/...` paths (need rewrite to `docs/canonical/...` relative paths)
- Constitution / roadmap / testing-strategy line numbers (need section-name refs per Rule 3)
- Retired doc references (any pointing at llm-handoff, priority-c-closure-summary, etc.)

Record findings as path-refresh scope for Phase 7.2.

- [ ] **Step 3: Append final audit entries**

Append entries per template.

### Task 0.11: Commit audit doc

- [ ] **Step 1: Verify the audit file has no placeholders**

```pwsh
Select-String -Path docs/superpowers/audits/2026-05-28-binding-autogen-audit-landing-tables.md -Pattern '<\.\.\.>|TBD|TODO'
```

Expected: no matches.

- [ ] **Step 2: Run markdownlint on the audit file**

```pwsh
# If markdownlint-cli is available; otherwise rely on IDE diagnostics
```

Expected: no errors.

- [ ] **Step 3: Commit audit completion**

```pwsh
git add docs/superpowers/audits/2026-05-28-binding-autogen-audit-landing-tables.md
git commit -m "docs(audit): complete Phase 0 unique-info-landing tables for binding-autogen consolidation

All retirees + move-with-purify files audited per spec Rule 1 audit gate.
Each retiree has confirmed unique-info kernels mapped to destination docs;
no kernel without destination. Cross-checked against canonical/satellites/
analyses for completeness; gaps recorded for Phase 2.6 fold.

CppAst-independent fold targets recorded per Rule 2:
- M1 safety-harness pattern -> testing-strategy
- M2 parser/model/emitter separation -> verified in constitution
- M3 engine/policy boundary + M2 review findings -> constitution + implementation-notes
- Sunset playbook trio-pinning concept -> adapt in new ClangSharp maintenance playbook
- generator-spike-goals hardcoding rules -> implementation-notes section 11
- oracle-evidence-design classification labels -> implementation-notes section 10
- iteration-2-spec 18-source config inventory -> implementation-notes section 12
- satellite-expansion-roadmap cross-cutting facts -> implementation-notes sections 12-13
- satellite-expansion-roadmap forward backlog -> next-iteration-plan via Phase 4 restructure

Phase 0 complete; ready for Multi-Agent Review Checkpoint 1.

Refs docs/superpowers/specs/2026-05-28-binding-autogen-doc-consolidation-design.md.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

### Task 0.12: Multi-Agent Review Checkpoint 1 (Post-Audit)

**Per spec §"Checkpoint 1 — Post-Audit"**: dispatch 2 parallel agents.

- [ ] **Step 1: Dispatch Explore agent — durable-pattern search**

Use Agent tool with `subagent_type: "Explore"`:

```text
Prompt: Search the following retired files for sections matching durable-pattern keywords (algorithms, decision tables, rule definitions, fixtures, evidence tables) that should survive but might be missed by the audit landing table.

Files to scan (all read-only):
- docs/parking-lot/binding-autogen-cake-implementation/milestone-1-safety-harness-baseline.md
- docs/parking-lot/binding-autogen-cake-implementation/milestone-2-behavior-preserving-topology-refactor.md
- docs/parking-lot/binding-autogen-cake-implementation/milestone-3-profile-boundary.md
- spikes/binding-generators/docs/llm-handoff.md
- spikes/binding-generators/docs/priority-c-closure-summary.md
- spikes/binding-generators/docs/generator-spike-goals.md
- spikes/binding-generators/docs/oracle-evidence-design.md
- spikes/binding-generators/docs/satellite-expansion-roadmap.md
- spikes/binding-generators/docs/items/iteration-2-config-surface-unification-spec.md
- spikes/binding-generators/docs/items/item-1-per-library-generation-spec.md
- spikes/binding-generators/docs/testing/raw-abi-upstream-testing-spec.md
- spikes/binding-generators/docs/testing/raw-abi-upstream-testing-plan.md
- docs/playbook/binding-generator-maintenance.md

Compare findings against the audit landing table at:
- docs/superpowers/audits/2026-05-28-binding-autogen-audit-landing-tables.md

Report any unique-info kernel found in a retiree that does NOT have a destination in the audit landing table. Limit to specific section-level findings; do not flag whole-file duplicates.

Search breadth: very thorough.

Report under 400 words.
```

- [ ] **Step 2: Dispatch general-purpose agent — destination coverage cross-check**

Use Agent tool with `subagent_type: "general-purpose"`:

```text
Prompt: Cross-check the destination docs against the audit landing table for landing-place coverage.

Read:
1. docs/superpowers/audits/2026-05-28-binding-autogen-audit-landing-tables.md (the audit table)
2. docs/superpowers/specs/2026-05-28-binding-autogen-doc-consolidation-design.md sections "New Documents — Content Outlines" (implementation-notes outline + maintenance outline)
3. docs/binding-autogen/binding-generator-constitution.md (current state, pre-purification)
4. docs/binding-autogen/binding-generator-roadmap.md (current state, pre-restructure)
5. docs/binding-autogen/testing-strategy.md (current state, pre-purification)

For each unique-info kernel in the audit table with a destination listed (e.g., "Constitution Foreign Type Boundary Policy" or "implementation-notes section 12"), verify:
- The destination section exists OR is part of the implementation-notes/maintenance content outline.
- The destination scope can absorb this kernel without semantic mismatch.

Flag any destination that does NOT have capacity for its assigned kernel.

Report under 400 words.
```

- [ ] **Step 3: Consolidate review findings**

Read both agent reports. Document review consensus or explicit conflicts. If conflicts: add follow-up audit entries to address gaps before Phase 1.

- [ ] **Step 4: Apply audit corrections if any**

If reviewers found unique-info gaps, append new entries or revise existing entries in the audit landing table. Re-commit if changes made.

- [ ] **Step 5: Exit gate**

Confirm: zero unique-info kernels without destination. Phase 0 complete; Phase 1 can begin.

---

## Phase 1 — Move Canonical Docs

**Goal:** Atomic `git mv` of 13 files into `spikes/binding-generators/docs/canonical/` tree. History preserved via `git log --follow`. No content edits (purification happens in Phase 2).

### Task 1.1: Create canonical/ subfolder structure

**Files:**

- Create directory: `spikes/binding-generators/docs/canonical/`
- Create directory: `spikes/binding-generators/docs/canonical/references/`
- Create directory: `spikes/binding-generators/docs/canonical/satellites/`

- [ ] **Step 1: Verify the canonical/ tree does not yet exist**

```pwsh
Test-Path 'spikes/binding-generators/docs/canonical'
```

Expected: `False`.

- [ ] **Step 2: Create the canonical/ tree placeholder structure**

Git tracks files, not folders. The folders will exist implicitly when their first file lands. No explicit `mkdir` needed; just verify the path will exist after `git mv` in Task 1.2.

### Task 1.2: Move docs/binding-autogen/ → canonical/

**Files (move):**

- `docs/binding-autogen/README.md` → `spikes/binding-generators/docs/canonical/README.md`
- `docs/binding-autogen/binding-generator-constitution.md` → `spikes/binding-generators/docs/canonical/binding-generator-constitution.md`
- `docs/binding-autogen/binding-generator-roadmap.md` → `spikes/binding-generators/docs/canonical/binding-generator-roadmap.md`
- `docs/binding-autogen/testing-strategy.md` → `spikes/binding-generators/docs/canonical/testing-strategy.md`

- [ ] **Step 1: git mv each file**

```pwsh
git mv docs/binding-autogen/README.md spikes/binding-generators/docs/canonical/README.md
git mv docs/binding-autogen/binding-generator-constitution.md spikes/binding-generators/docs/canonical/binding-generator-constitution.md
git mv docs/binding-autogen/binding-generator-roadmap.md spikes/binding-generators/docs/canonical/binding-generator-roadmap.md
git mv docs/binding-autogen/testing-strategy.md spikes/binding-generators/docs/canonical/testing-strategy.md
```

- [ ] **Step 2: Verify history preserved**

```pwsh
git log --follow -1 spikes/binding-generators/docs/canonical/binding-generator-constitution.md
```

Expected: commit history of the original file shown (not just the rename commit).

- [ ] **Step 3: Verify docs/binding-autogen/ folder is empty**

```pwsh
Get-ChildItem docs/binding-autogen/
```

Expected: empty result (folder will be implicitly removed at commit since git doesn't track empty folders).

### Task 1.3: Move docs/playbook/binding-output-oracle-validation.md → canonical/

- [ ] **Step 1: git mv**

```pwsh
git mv docs/playbook/binding-output-oracle-validation.md spikes/binding-generators/docs/canonical/binding-output-oracle-validation.md
```

- [ ] **Step 2: Verify**

```pwsh
git log --follow -1 spikes/binding-generators/docs/canonical/binding-output-oracle-validation.md
```

### Task 1.4: Move satellites/ → canonical/satellites/ (with rename)

**Files (move + 1 rename):**

- `spikes/binding-generators/docs/satellites/sdl2-image-cross-check.md` → `spikes/binding-generators/docs/canonical/satellites/sdl2-image-header-analysis.md` (rename)
- 6 other `satellites/*.md` files → `canonical/satellites/` (same filename)

- [ ] **Step 1: git mv all 7 satellite files**

```pwsh
git mv spikes/binding-generators/docs/satellites/sdl2-image-cross-check.md spikes/binding-generators/docs/canonical/satellites/sdl2-image-header-analysis.md
git mv spikes/binding-generators/docs/satellites/sdl2-mixer-header-analysis.md spikes/binding-generators/docs/canonical/satellites/sdl2-mixer-header-analysis.md
git mv spikes/binding-generators/docs/satellites/sdl2-ttf-header-analysis.md spikes/binding-generators/docs/canonical/satellites/sdl2-ttf-header-analysis.md
git mv spikes/binding-generators/docs/satellites/sdl2-gfx-header-analysis.md spikes/binding-generators/docs/canonical/satellites/sdl2-gfx-header-analysis.md
git mv spikes/binding-generators/docs/satellites/sdl2-satellite-error-function-consolidation.md spikes/binding-generators/docs/canonical/satellites/sdl2-satellite-error-function-consolidation.md
git mv spikes/binding-generators/docs/satellites/sdl2-endian-platform-macro-consolidation.md spikes/binding-generators/docs/canonical/satellites/sdl2-endian-platform-macro-consolidation.md
git mv spikes/binding-generators/docs/satellites/sdl2-function-like-macro-consolidation.md spikes/binding-generators/docs/canonical/satellites/sdl2-function-like-macro-consolidation.md
```

- [ ] **Step 2: Verify history preserved for the renamed file**

```pwsh
git log --follow -1 spikes/binding-generators/docs/canonical/satellites/sdl2-image-header-analysis.md
```

Expected: commit history of `sdl2-image-cross-check.md` shown.

- [ ] **Step 3: Verify satellites/ folder is empty**

```pwsh
Get-ChildItem spikes/binding-generators/docs/satellites/
```

Expected: empty result.

### Task 1.5: Move ppy-reference-analysis → canonical/references/

- [ ] **Step 1: git mv**

```pwsh
git mv spikes/binding-generators/docs/ppy-reference-analysis-2026-05-25.md spikes/binding-generators/docs/canonical/references/ppy-reference-analysis-2026-05-25.md
```

- [ ] **Step 2: Verify**

```pwsh
git log --follow -1 spikes/binding-generators/docs/canonical/references/ppy-reference-analysis-2026-05-25.md
```

### Task 1.6: Commit Phase 1 (atomic moves done)

- [ ] **Step 1: Review staged changes**

```pwsh
git status
```

Expected: ~13 files shown as renamed (R), plus the 4 implicit folder removals manifest in untracked-removed status.

- [ ] **Step 2: Commit Phase 1**

```pwsh
git commit -m "docs(binding-autogen): Phase 1 - move canonical docs into spike canonical/

Atomic git mv of 13 files into spikes/binding-generators/docs/canonical/ tree
preserves git log --follow history. No content edits in this phase; purification
happens in Phase 2.

Moves:
- docs/binding-autogen/ (4 files) -> canonical/ (README, constitution, roadmap, testing-strategy)
- docs/playbook/binding-output-oracle-validation.md -> canonical/
- spike docs/ppy-reference-analysis-2026-05-25.md -> canonical/references/
- spike docs/satellites/ (7 files) -> canonical/satellites/ (with sdl2-image-cross-check -> sdl2-image-header-analysis rename)

Folder removals (implicit): docs/binding-autogen/, spike docs/satellites/.
docs/playbook/ retains local-development.md (non-binding-autogen).

Refs docs/superpowers/specs/2026-05-28-binding-autogen-doc-consolidation-design.md Phase 1.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

- [ ] **Step 3: Verify clean tree**

```pwsh
git status
```

Expected: clean working tree.

---

## Phase 2 — Purify Content

**Goal:** Apply Q3C aggressive constitution purification, Q4D roadmap restructure, Q5/Q9 testing-strategy purification + testing/ fold, README purification, oracle-validation playbook path refresh, satellites/ index refresh. All edits happen in the newly-moved `canonical/` tree.

**Source for what-to-cut-and-what-to-keep:** the audit landing tables produced in Phase 0 + the design spec §"New Documents — Content Outlines" + §"Settled Decisions" Q3/Q4/Q5/Q9.

### Task 2.1: Constitution aggressive purification (Q3C)

**Files modified:**

- `spikes/binding-generators/docs/canonical/binding-generator-constitution.md`

- [ ] **Step 1: Re-read the audit table entry for the constitution**

Use Read tool on the audit landing table; locate the constitution entry. Confirm the "stays / cuts to implementation-notes / pure cuts" split before editing.

- [ ] **Step 2: Strip Status banner + Toolchain re-evaluation banner**

Edit `binding-generator-constitution.md`. Remove the top `> **Status (...)** / **Toolchain re-evaluation (...)** banner block. Replace with a single durable header:

```markdown
> **Status:** Canonical binding-generator constitution. Pure principles — toolchain-neutral ABI/API policy. Mechanism details and current implementation evidence live in [`binding-generator-implementation-notes.md`](binding-generator-implementation-notes.md).
```

- [ ] **Step 3: Strip Generator Home implementation paths**

Remove paragraphs naming `build/_build/Targets/GenerateBindings/` paths under §"Generator Home". Keep the principle: "the production implementation must satisfy these contracts" + the bullet list of durable contracts (committed `.g.cs`, `.generated-stamp`, validators reachable from build host, persisted data contracts, local invocation via `tools.cs`, Linux-canonical parsing). Cut path enumerations.

- [ ] **Step 4: Strip Generation Determinism Contract implementation detail**

Under §"Generation Determinism Contract", keep the durable invariants (Determinism Inputs principle, Family Isolation 3 properties, Dependency Direction forward-runtime / build-time ProjectReference distinction, Native Header Resolution Scope principle, Pure-Inputs Discipline). **Cut:** enumerated 8-item pin set with named files (`family-config.json`, `generate_bindings.py`, `oracle.cs`, postprocess/*.cs); Verification Contract specific commands. Reference instead: "Implementation pin set and verification commands: see [implementation-notes §"Generation Determinism Implementation"](binding-generator-implementation-notes.md)".

- [ ] **Step 5: Strip Opaque Handles "Implementation mechanism" subsections**

Under §"Opaque Handles": keep "Why / How / What" principle + Pattern B blittable-struct contract + force-opaque allow-list rule + cross-assembly DisableRuntimeMarshalling rule + Cross-family handle name resolution principle. **Cut:** "Implementation mechanism (ClangSharp + Roslyn postprocess)" paragraph naming `OpaqueHandleEmitRewriter` + auto-detect criterion implementation + "Force-opaque allow-list delegation" implementation detail + canonical roster machine-readable enumeration paragraph (the per-family roster table). Reference instead: "Implementation mechanism: see [implementation-notes §"Opaque Handles Implementation Mechanism"](binding-generator-implementation-notes.md)".

- [ ] **Step 6: Strip C `long` Priority C hybrid strategy implementation paragraphs**

Under §"C `long` And `unsigned long`" → "Priority C hybrid strategy": keep WHY/HOW/WHAT principle + dropped convenience helper rule + dual-dispatch `SDL_threadID` family decision. **Cut:** specific RSP `--exclude` enumeration + `Generated/Modern/` vs `Generated/Compat/` path detail + commit chain references. Reference instead: "Implementation mechanism: see [implementation-notes §"C \`long\` Hybrid Implementation"](binding-generator-implementation-notes.md)".

- [ ] **Step 7: Strip `wchar_t` Priority C mechanism paragraphs**

Under §"wchar_t" → "Priority C mechanism": keep WHY/HOW/WHAT principle (opaque `nint` at raw ABI is ABI-correct). **Cut:** "ClangSharp-style implementations use RSP-level..." + "CppAst-style implementations use the type classifier..." paragraphs. Reference instead: implementation-notes.

- [ ] **Step 8: Strip Foreign Type Boundary Policy disposition table column**

Under §"Foreign Type Boundary Policy": keep "Why / How / What" principle + the categorical table (Vulkan / Direct3D / GDK / Win32 / Android JNI / C stdlib / Linux X11 / etc.). **Cut:** the "Disposition" column ("Active — `rsp/per-header/...`" / "Already handled — `rsp/...`" / "Deferred"). Reference instead: "Per-category implementation disposition: see [implementation-notes §"Foreign Type Boundary Implementation"](binding-generator-implementation-notes.md)".

- [ ] **Step 9: Strip Current SDL2.Core ABI Status closure-record content**

Under §"Current SDL2.Core ABI Status": keep the durable resolved-categories list (1-7 enumeration) as policy outcomes. **Cut:** the introductory paragraph naming commit chains (`d0016de`, `e62bf92`, etc.) + the "sunset Cake-hosted CppAst implementation" historical phrasing + "Known Stage 1 flag enums" sub-list (data, not policy — moves to implementation-notes §"Known Flag Enums Data"). Replace with: "Resolved or intentionally quarantined categories (toolchain-neutral policy outcomes; current implementation evidence in [implementation-notes](binding-generator-implementation-notes.md)):"

- [ ] **Step 10: Apply Authority Order cleanup**

Under §"Authority Order": cleanup the list to remove implementation-status references. Keep the durable hierarchy (1. pinned headers, 2. native binary exports, 3. current generator implementation, 4. dynapi manifests, 5. this constitution, 6. roadmap, 7. ADR-004 historical, 8. peer bindings + research as evidence). Cut the "(currently Reopened — see spike under...)" parenthetical and similar status callouts. Add a single durable footer line: "Implementation-mechanism authority (item 3) is captured in [implementation-notes](binding-generator-implementation-notes.md)."

- [ ] **Step 11: Apply Configurable Scope Vs Policy Mechanism cleanup**

Under §"Manifest Configuration Vs Code-Owned Policy" → "Configurable Scope Vs Policy Mechanism": keep the principle (config = which / what; code = how). **Cut:** example field names from `family-config.json` (`clong_methods`, `flags_enums.allow_list`, `opaque_handles.force_opaque_exceptions`) — these are config artifact names, not principles. Reference instead: implementation-notes §"Config Surface Evolution" for current config field enumeration.

- [ ] **Step 12: Run markdownlint on the purified constitution**

Use IDE diagnostics or `markdownlint-cli` if available. Fix any warnings inline (table separator spacing, list-around-blank-lines, code-block language tags).

- [ ] **Step 13: Verify line count target (~350 lines)**

```pwsh
(Get-Content spikes/binding-generators/docs/canonical/binding-generator-constitution.md).Count
```

Expected: ~350 lines (target per spec §Q3C; ±50 acceptable). If significantly over, identify additional cuts before commit.

- [ ] **Step 14: WIP commit constitution purification**

```pwsh
git add spikes/binding-generators/docs/canonical/binding-generator-constitution.md
git commit -m "docs(binding-autogen): Q3C constitution aggressive purification

Pure principles only - mechanism + evidence move to forthcoming
binding-generator-implementation-notes.md sibling per spec Phase 2.1.

Cuts:
- Status + Toolchain re-evaluation banners
- Generator Home path enumeration
- Generation Determinism Contract enumerated pin set + verification commands
- Opaque Handles Implementation mechanism subsections (rewriter names, roster table)
- C long Priority C implementation paragraphs (RSP --exclude detail, commit chains)
- wchar_t Priority C ClangSharp-style + CppAst-style implementation paragraphs
- Foreign Type Boundary Policy disposition column
- Current SDL2.Core ABI Status closure-record framing
- Configurable Scope Vs Policy Mechanism example field-name enumeration
- Authority Order status-callout parentheticals

Stays: Layer Contract, scalar mapping, Why/How/What principle for every policy section,
Pattern B contract, BCL-Replaceable Helper Exclusion Policy, structs/unions rules,
enum/macro/platform policies, evidence gates, Maintenance Rule.

Refs spec Q3C + audit landing table.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

### Task 2.2: Roadmap layer-based restructure (Q4D)

**Files modified:**

- `spikes/binding-generators/docs/canonical/binding-generator-roadmap.md`

- [ ] **Step 1: Strip Status + Toolchain re-evaluation banners**

Replace the top banner block with a single durable header:

```markdown
> **Status:** Canonical binding-generator roadmap. Layer-based forward milestones aligned with constitution Layer Contract. Sequence: Layer 1 raw ABI (done) → Layer 2 typed public API → Layer 3 friendly overloads → Production Flip → SysWM Layout + Satellite Sweep close → Smoke Expansion → SDL3 Extension.
```

- [ ] **Step 2: Replace M0–M4 sections with a single "What's Done" section**

Locate sections M0 (Plan/Research Consolidation), M1 (Safety Harness), M2 (Topology Refactor), M3 (Profile Boundary), M4 (Raw ABI Projection). Delete all five sections. Replace with one new section:

```markdown
## What's Done — SDL2 Layer 1 Raw ABI Closure (2026-05-28)

Layer 1 raw ABI generation closed for SDL2 Core, Image, GFX, TTF, and Mixer
families on the active spike implementation. Full evidence:

- Generator runs under [`spikes/binding-generators/clangsharp/`](../../clangsharp/).
- Multi-TFM compile clean across `net10.0`, `net9.0`, `net8.0`, `netstandard2.0`, `net462`.
- Five-family oracle report at [`output/reports/oracle-evidence-clangsharp.md`](../../output/reports/oracle-evidence-clangsharp.md).
- Toolchain decision evidence at [`output/reports/iteration-2-comparison.md`](../../output/reports/iteration-2-comparison.md).
- RSP fix history at [`output/reports/clangsharp-failure-buckets.md`](../../output/reports/clangsharp-failure-buckets.md).
- Per-TFM ABI runtime smoke at `spikes/binding-generators/clangsharp/tests/abi-tests/`.

Closure includes Priority C semantic-ABI risks (`wchar_t*` opaque, C `long`
hybrid, `SDL_RWops` / `SDL_SysWMinfo` / `SDL_SysWMmsg` Pattern B quarantine,
tag/typedef canonicalization, `SDL_GUID` substitution); Foreign Type Boundary
Policy + BCL-Replaceable Helper Exclusion Policy + Cross-Assembly Pattern B
contract via `[assembly: DisableRuntimeMarshalling]`.

Mechanism and implementation evidence: [`binding-generator-implementation-notes.md`](binding-generator-implementation-notes.md).
Maintenance procedures: [`binding-generator-maintenance.md`](binding-generator-maintenance.md).

`git log --follow` is the archive for slice-by-slice closure detail.
```

- [ ] **Step 3: Rename M5 → "Layer 2 Typed Public API Projection"**

Section heading: `## Layer 2 — Typed Public API Projection`. Body content stays mostly intact (scope + exit evidence + non-goals) but with implementation references cleaned per Rule 3 (section-name refs instead of line numbers). Strip M-numbering references.

- [ ] **Step 4: Rename M6 → "Layer 3 Friendly Overload Projection"**

Section heading: `## Layer 3 — Friendly Overload Projection`. Same treatment as Step 3.

- [ ] **Step 5: Rename M7 → "Production Flip"**

Section heading: `## Production Flip — SDL2.Core Reproducibility`. Same treatment. Body keeps `.generated-stamp` contract, committed `.g.cs`, stale-output validation.

- [ ] **Step 6: Rename M8 → "SysWM Layout And Satellite Sweep Close"**

Section heading: `## SysWM Layout And Satellite Sweep Close`. Body acknowledges satellite Layer 1 closure done; remaining work = SysWM typed-union layout + remaining `external/sdl2-cs` production-use retirement + Pack-stage symbol-existence validator + SDL2_net addition.

- [ ] **Step 7: Rename M9 → "Smoke And Asset-Backed Testing Expansion"**

Section heading: `## Smoke And Asset-Backed Testing Expansion`. Body unchanged in substance; defers to testing-strategy implementation backlog.

- [ ] **Step 8: Rename M10 → "SDL3 Extension"**

Section heading: `## SDL3 Extension`. Body unchanged in substance; gated on PD-7 per release-strategy.

- [ ] **Step 9: Cut Canonical References And Research Baseline table**

The table duplicates canonical/README content. Delete entire section.

- [ ] **Step 10: Cut Operating Rules wall**

Already covered by AGENTS.md + constitution principles. Delete entire section.

- [ ] **Step 11: Cut Continuous Maintenance list**

Folds into new maintenance playbook. Delete entire section.

- [ ] **Step 12: Cut Retired Active References section**

One-time consolidation note from 2026-05-23. No longer relevant.

- [ ] **Step 13: Run markdownlint + verify line count target (~250 lines)**

```pwsh
(Get-Content spikes/binding-generators/docs/canonical/binding-generator-roadmap.md).Count
```

Expected: ~250 lines (target per spec §Q4D).

- [ ] **Step 14: WIP commit roadmap restructure**

```pwsh
git add spikes/binding-generators/docs/canonical/binding-generator-roadmap.md
git commit -m "docs(binding-autogen): Q4D roadmap layer-based restructure

Collapsed M0-M4 into single 'What's Done' section recording SDL2 Layer 1
raw ABI closure across 5 families. Forward milestones renamed to layer-based:
Layer 2 Typed Public API / Layer 3 Friendly Overloads / Production Flip /
SysWM Layout + Satellite Sweep Close / Smoke Expansion / SDL3 Extension.

Cut: Canonical References table (duplicates canonical README), Operating Rules
wall (covered by AGENTS.md + constitution), Continuous Maintenance list (folds
into maintenance playbook), Retired Active References section (one-time note).

M-numbering retires throughout this doc; upstream cross-references updated in Phase 5.

Refs spec Q4D + audit landing table.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

### Task 2.3: Testing-strategy purification + testing/ fold (Q5/Q9)

**Files modified:**

- `spikes/binding-generators/docs/canonical/testing-strategy.md`

**Files read (for fold-in):**

- `spikes/binding-generators/docs/testing/raw-abi-upstream-testing-spec.md`
- `spikes/binding-generators/docs/testing/raw-abi-upstream-testing-plan.md`
- `spikes/binding-generators/docs/testing/raw-abi-current-inventory.md`

- [ ] **Step 1: Strip Status banner + Current Baseline section**

Replace top status banner with durable header:

```markdown
> **Status:** Canonical testing strategy for generated SDL bindings. Layer model, fixture policy, upstream adoption rules, coverage strategy, and implementation backlog (smoke + raw ABI upstream port).
```

Cut entire "Current Baseline" section (sunset Cake refs + spike-state snapshot).

- [ ] **Step 2: Strip sunset paths from Build-Host Unit Tests section**

Replace `build/_build.Tests/Unit/Targets/GenerateBindings/` paths with toolchain-neutral language ("build-host unit-test project for the binding generator"). Remove `JANSET_VERIFY_GENERATED_PREVIEW=1` env-var convention (sunset).

- [ ] **Step 3: Strip sunset paths from Binding Compile Checks section**

`tests/binding-compile-check/SDL2.Core.CompileCheck.csproj` references → toolchain-neutral.

- [ ] **Step 4: Fold testing/raw-abi-upstream-testing-spec.md content**

Read the spec file in full. Identify which sections merge where:

- **Categories table** (`AbiSmoke` / `AbiUpstreamPort` / `AbiHeaderCoverage` / etc.) → merge into the Layer Model table OR add as a new "Spike Test Categories" subsection.
- **Coverage Strategy + ABI risk families** → merge into the "Generated Raw ABI Runtime Tests" section's existing scope/policy paragraphs.
- **Upstream Feasibility Summary** (260-280 strong from 310 enabled) + **High-value P0/P1 sources list** → merge into "SDL2 Core Research Summary" section, expanding with the explicit list.
- **Hard Boundaries / Ground Rules / TUnit And Parallelism / Multi-Agent Execution / Test Project Layout / Verification Commands** → consolidate into new section "Raw ABI Test Suite Ground Rules" (the durable ones).

After merging, the source file `testing/raw-abi-upstream-testing-spec.md` retires in Phase 6.

- [ ] **Step 5: Fold testing/raw-abi-upstream-testing-plan.md content**

Read the plan in full. Identify the multi-agent slice tables (Phase 2 P0 wave / Phase 3 P1 wave / Phase 4 P2 wave / Phase 5 Mechanical Header Coverage). Add a new subsection under "Implementation Backlog" called "Raw ABI Upstream Port Backlog" containing the slice tables verbatim (or condensed if appropriate).

After merging, the source file `testing/raw-abi-upstream-testing-plan.md` retires in Phase 6.

- [ ] **Step 6: Fold testing/raw-abi-current-inventory.md deferred-coverage notes**

Read the inventory. The point-in-time test counts (712 tests, 178 per TFM) are volatile — do not fold (retire with file). The "Known Generated Binding Blockers" list (SDL_syswm, thread creation macros, function-like SDL macros, `SDL_GetErrorMsg`) folds into the testing-strategy "Generated Raw ABI Runtime Tests" deferred-blockers paragraphs.

- [ ] **Step 7: Rewrite Refactor Gate Mapping table to layer-based names**

Current table uses M1–M10. Rewrite to:

```markdown
| Forward Milestone | Primary Gate | Secondary Gate |
| --- | --- | --- |
| Layer 2 Typed Public API | API snapshot, compile-check | Package smoke minimal generated calls |
| Layer 3 Friendly Overloads | Pattern tests, compile-check | Package smoke string/path/resource pairs |
| Production Flip | Project build, stamp tests | Package-first smoke |
| SysWM Layout + Satellite Sweep | Family compile/package smoke | Duplicate core-type guard, symbol existence |
| Smoke Expansion | NativeSmoke + PackageConsumerSmoke | Fixture provenance review |
| SDL3 Extension | SDL3 compile/package smoke | SDL3-specific policy review |
```

- [ ] **Step 8: Run markdownlint + verify line count target (~300 lines)**

Per spec §Q5/Q9 — target ~300 lines with all folded content. May land higher (~400-450) given fold-in from 3 testing/ files; acceptable if all folds preserved.

- [ ] **Step 9: WIP commit testing-strategy purification**

```pwsh
git add spikes/binding-generators/docs/canonical/testing-strategy.md
git commit -m "docs(binding-autogen): Q5/Q9 testing-strategy purification + testing/ fold

Aggressive purification (Q5) with testing/ folder folded in (Q9):
- Sunset Cake paths (build/_build.Tests, tests/binding-compile-check) -> toolchain-neutral
- Refactor Gate Mapping rewritten layer-based (retire M-numbering)
- Folded testing/raw-abi-upstream-testing-spec.md: Categories table, Coverage Strategy,
  ABI risk families, Upstream Feasibility Summary + P0/P1 source list, Test Categories
  ground rules
- Folded testing/raw-abi-upstream-testing-plan.md: multi-agent slice tables ->
  new 'Raw ABI Upstream Port Backlog' subsection under Implementation Backlog
- Folded testing/raw-abi-current-inventory.md: Known Generated Binding Blockers
  -> deferred-coverage notes; point-in-time test counts dropped (volatile)

Preserved anchors per spec Q5: Layer Model, Upstream Adoption Policy,
SDL2 Core + Satellite Research Summaries, Initial Coverage Matrix,
fixture policy, flakiness rules, research-sources URL list.

testing/ folder retires entirely in Phase 6.

Refs spec Q5/Q9 + audit landing table.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

### Task 2.4: canonical/README purification

**Files modified:**

- `spikes/binding-generators/docs/canonical/README.md`

- [ ] **Step 1: Strip Status banner + Toolchain re-evaluation banner**

Replace with durable header:

```markdown
> **Status:** Canonical binding-generator workstream index. Pure-principles policy + mechanism + maintenance + testing + satellite analyses for the active spike implementation under `spikes/binding-generators/clangsharp/`. Restores to `docs/binding-autogen/` at Production Flip (see roadmap §"Production Flip").
```

- [ ] **Step 2: Rewrite Folder Layout table**

Update the table to reflect the new `canonical/` tree:

```markdown
| Path | Purpose |
| --- | --- |
| [`README.md`](README.md) | Workstream index and reading order. |
| [`binding-generator-constitution.md`](binding-generator-constitution.md) | Canonical ABI/API policy: internal raw ABI, public typed low-level API, friendly overloads, manifest config vs code-owned policy, evidence gates. |
| [`binding-generator-roadmap.md`](binding-generator-roadmap.md) | Canonical roadmap: SDL2 Layer 1 closure record + Layer 2 / Layer 3 / Production Flip / SysWM + Satellite Sweep / Smoke Expansion / SDL3 Extension. |
| [`testing-strategy.md`](testing-strategy.md) | Canonical testing strategy: layer model, fixture policy, raw ABI runtime tests, smoke expansion backlog, raw ABI upstream port backlog. |
| [`binding-generator-implementation-notes.md`](binding-generator-implementation-notes.md) | Mechanism + evidence: postprocess rewriter design, oracle classification labels, hardcoding rules, config surface evolution, postprocess pipeline order. |
| [`binding-generator-maintenance.md`](binding-generator-maintenance.md) | Maintenance playbook: version-bump procedures, RSP file maintenance, family-config.json schema, postprocess pipeline maintenance, drift detection, new-family addition checklist. |
| [`binding-output-oracle-validation.md`](binding-output-oracle-validation.md) | Multi-oracle review workflow for validating generated output. |
| [`satellites/`](satellites/) | Per-family header analyses + cross-family consolidation analyses. |
| [`references/`](references/) | Peer reference analyses (ppy/SDL3-CS, etc.). |
```

- [ ] **Step 3: Rewrite Reading Order table**

```markdown
| # | Document | Purpose |
| --- | --- | --- |
| 1 | [`binding-generator-constitution.md`](binding-generator-constitution.md) | Read first. ABI/API law and evidence gates. |
| 2 | [`binding-generator-roadmap.md`](binding-generator-roadmap.md) | Read second. Layer-based forward sequencing. |
| 3 | [`testing-strategy.md`](testing-strategy.md) | Read before changing tests, snapshots, smoke tests, fixtures. |
| 4 | [`binding-generator-implementation-notes.md`](binding-generator-implementation-notes.md) | Read for mechanism rationale (rewriter design, hardcoding rules, classification labels). |
| 5 | [`binding-generator-maintenance.md`](binding-generator-maintenance.md) | Read for operational procedures (version bumps, RSP edits, family-config schema). |
| 6 | [`binding-output-oracle-validation.md`](binding-output-oracle-validation.md) | Read before promoting output toward production source. |
| 7 | [`satellites/`](satellites/) | Read when designing Layer 2/3 projection for a specific SDL2 family. |
| 8 | [`references/ppy-reference-analysis-2026-05-25.md`](references/ppy-reference-analysis-2026-05-25.md) | Read for peer reference (orchestrator architecture, companion class layer mapping). |
```

- [ ] **Step 4: Rewrite Current Decision Posture section**

Replace the toolchain re-evaluation framing with post-spike state:

```markdown
## Current Decision Posture

- **Toolchain:** ClangSharp + Roslyn postprocess is the active implementation under [`spikes/binding-generators/clangsharp/`](../../clangsharp/) and the direction-of-record for Layer 2+ work. Formal ADR-004 amendment deferred to post-Layer-2 closure.
- **Output contract** per Layer Contract in the constitution: internal raw ABI externs + public typed low-level API + friendly overloads. Internal raw container blocks public package API exposure. Public raw `IntPtr` externs not part of v1 preview.
- **Family identity** is manifest-driven; namespace + public class per Constitution Family Identity table.
- **Generator is build infrastructure** — committed `.g.cs` source, per-family `.generated-stamp` with reproducibility metadata at Production Flip, validators reachable from PreFlight and Pack stages.
- **Linux-canonical generation** for ABI correctness across the 7-RID surface.
- **SDL3** is gated on PD-7 (SDL2 real-public-release) per release-strategy.
```

- [ ] **Step 5: Cut historical research / temporary-notes paragraphs**

Cut any paragraphs describing "Historical research and task-by-task plans were intentionally removed..." or similar one-time consolidation framing.

- [ ] **Step 6: Rewrite Related Canonical Docs section**

```markdown
## Related Canonical Docs

- [`../../../../docs/phases/phase-4-binding-autogen.md`](../../../../docs/phases/phase-4-binding-autogen.md) — Phase 4 design brief.
- [`../../../../docs/release-strategy.md`](../../../../docs/release-strategy.md) — AST-first sequencing and v1.0 release strategy.
- [`../../../../docs/plan.md`](../../../../docs/plan.md) — tactical roadmap.
- [`../../../../docs/decisions/2026-05-05-target-centric-build-host.md`](../../../../docs/decisions/2026-05-05-target-centric-build-host.md) — ADR-002 target-centric build-host pattern.
- [`../../../../docs/decisions/2026-05-12-build-host-data-layer.md`](../../../../docs/decisions/2026-05-12-build-host-data-layer.md) — ADR-003 contract-centric data layer pattern.
- [`../../../../docs/decisions/2026-05-14-binding-autogen-toolchain.md`](../../../../docs/decisions/2026-05-14-binding-autogen-toolchain.md) — ADR-004 binding-autogen toolchain (status: Reopened; amendment deferred).
- [`../../../../docs/knowledge-base/extraction-guidelines.md`](../../../../docs/knowledge-base/extraction-guidelines.md) — collaborator extraction discipline.
- [`../../../../docs/knowledge-base/testing-guidelines.md`](../../../../docs/knowledge-base/testing-guidelines.md) — TUnit/MTP test infrastructure.
- [`../../../../AGENTS.md`](../../../../AGENTS.md) — operating rules and settled project decisions.
```

- [ ] **Step 7: Run markdownlint**

- [ ] **Step 8: WIP commit README purification**

```pwsh
git add spikes/binding-generators/docs/canonical/README.md
git commit -m "docs(binding-autogen): canonical README purification + post-spike state refresh

Updated Folder Layout + Reading Order tables for new canonical/ tree
(implementation-notes + maintenance siblings, references/ and satellites/
subfolders). Current Decision Posture rewritten to post-spike state
(ClangSharp + Roslyn postprocess direction-of-record; formal ADR-004
amendment deferred to post-Layer-2). Sunset banners removed.

Refs spec Phase 2.4 + audit landing table.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

### Task 2.5: Oracle-validation playbook purification

**Files modified:**

- `spikes/binding-generators/docs/canonical/binding-output-oracle-validation.md`

- [ ] **Step 1: Strip Status banner**

Replace with durable header:

```markdown
> **Status:** Canonical multi-oracle review workflow. Toolchain-neutral methodology complementing the automated `oracle.cs` evidence extractor.
```

- [ ] **Step 2: Replace `tools.cs generate-bindings` references**

Search the doc for `tools.cs generate-bindings`. Replace with toolchain-neutral language ("the binding generator command" or "regeneration"). The actual command is documented in the maintenance playbook (Phase 3.2).

- [ ] **Step 3: Update Existing Evidence Inputs table**

Replace `artifacts/generated-bindings-preview/sdl2-core/**/*.g.cs` row (sunset Cake-generated path) with `spikes/binding-generators/clangsharp/src/Janset.SDL2.*/Generated/**/*.g.cs` (active spike path). Replace `parse-views.json` row with reference to `oracle-evidence-clangsharp.md`.

- [ ] **Step 4: Reference oracle.cs as the active automated lane**

Add a short subsection acknowledging `oracle.cs` (at `spikes/binding-generators/clangsharp/oracle.cs`) as the active narrow-fact extractor. This playbook covers the broader review-led workflow.

- [ ] **Step 5: Run markdownlint**

- [ ] **Step 6: WIP commit oracle-validation purification**

```pwsh
git add spikes/binding-generators/docs/canonical/binding-output-oracle-validation.md
git commit -m "docs(binding-autogen): oracle-validation playbook purification

Path/command refresh:
- tools.cs generate-bindings refs -> toolchain-neutral language
- artifacts/generated-bindings-preview/sdl2-core (sunset Cake) -> spike src/Janset.SDL2.*/Generated
- parse-views.json -> oracle-evidence-clangsharp.md

Added pointer to oracle.cs as the active automated narrow-fact extractor;
this playbook covers the broader review-led workflow that complements it.

Refs spec Phase 2.5 + audit landing table.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

### Task 2.6: Satellites/ index refresh + reconnaissance gap fill

**Files modified:**

- All 7 files under `spikes/binding-generators/docs/canonical/satellites/` (cross-reference updates only)
- Possibly fold satellite reconnaissance gaps from `satellite-expansion-roadmap.md` audit (Phase 0 Task 0.4 Step 6 output)

- [ ] **Step 1: Cross-reference refresh in each satellite analysis**

For each file under `canonical/satellites/`:

```text
- sdl2-image-header-analysis.md (renamed from sdl2-image-cross-check.md)
- sdl2-mixer-header-analysis.md
- sdl2-ttf-header-analysis.md
- sdl2-gfx-header-analysis.md
- sdl2-satellite-error-function-consolidation.md
- sdl2-endian-platform-macro-consolidation.md
- sdl2-function-like-macro-consolidation.md
```

Apply Rule 3 (section-name cross-references): grep each file for `Constitution L\d+` patterns; replace with section-name refs (e.g., `Constitution §"C \`long\` And \`unsigned long\`"`). Grep for `docs/binding-autogen/` paths; replace with relative paths to new `canonical/` location.

- [ ] **Step 2: Fold satellite reconnaissance gaps (if any from Phase 0 Task 0.4 Step 6)**

Per Phase 0 audit: if any satellite reconnaissance findings from `satellite-expansion-roadmap.md` were not already present in the corresponding `canonical/satellites/sdl2-*-header-analysis.md`, fold them in now.

- [ ] **Step 3: Run markdownlint on each satellite file**

- [ ] **Step 4: WIP commit satellites refresh**

```pwsh
git add spikes/binding-generators/docs/canonical/satellites/
git commit -m "docs(binding-autogen): satellites/ cross-reference refresh + reconnaissance gap fill

Applied Rule 3 (section-name cross-references) across all 7 satellite analyses;
folded any reconnaissance gaps from satellite-expansion-roadmap.md (per Phase 0
audit Step 6).

Refs spec Phase 2.6 + audit landing table.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

---

## Phase 3 — Write New Docs

**Goal:** Write `binding-generator-implementation-notes.md` + `binding-generator-maintenance.md` per spec §"New Documents — Content Outlines". Both docs absorb audit-fold targets from Phase 0.

### Task 3.1: Write `binding-generator-implementation-notes.md`

**Files created:**

- `spikes/binding-generators/docs/canonical/binding-generator-implementation-notes.md`

**Source material for content:**

- Constitution sections cut in Task 2.1 (Generator Home paths, Determinism Contract pin set, Opaque Handles Implementation mechanism, C `long` mechanism, wchar_t mechanism, Foreign Type Boundary disposition, Configurable Scope examples).
- Audit fold targets: generator-spike-goals hardcoding rules, oracle-evidence-design classification labels, iteration-2-spec 18-source config inventory, satellite-expansion-roadmap cross-cutting facts (postprocess pipeline order, family-config.json structure).
- `spikes/binding-generators/clangsharp/postprocess/` source code (cite class names, not line numbers).
- `spikes/binding-generators/clangsharp/config/family-config.json` (cite schema, not full content).

- [ ] **Step 1: Create file with frontmatter + Purpose section**

Write the file at `spikes/binding-generators/docs/canonical/binding-generator-implementation-notes.md`:

```markdown
# Binding Generator Implementation Notes

> **Status:** Canonical companion to [`binding-generator-constitution.md`](binding-generator-constitution.md). Policy lives in the constitution (the WHY and WHAT); this document captures mechanism (HOW) + evidence + classification labels + hardcoding rules + cross-cutting iteration facts for the active spike implementation under [`spikes/binding-generators/clangsharp/`](../../clangsharp/).

## Purpose

The constitution is toolchain-neutral law. This document is toolchain-aware mechanism. When the constitution says "Pattern B handles emit as `readonly partial struct X(nint value)`", this document explains how `OpaqueHandleEmitRewriter` realizes that policy across two input channels (auto-detect + force-opaque allow-list) and rewrites references at three raw-ABI positions.

When the implementation changes (e.g., a new postprocess rewriter, a config schema evolution), update this document in the same change. When the policy changes (e.g., a new bool wire shape for SDL3), update the constitution.

## 1. Generation Determinism Implementation

[Section content folded from constitution Determinism Contract cuts in Task 2.1. Captures the 8-item pin set, verification commands, family isolation specifics naming family-config.json + generate_bindings.py + oracle.cs + postprocess.]

## 2. Postprocess Pipeline Order

[7-step pipeline rationale (platform-delta -> strip-varargs -> libraryimport Modern only -> flags-detect -> guid-substitute -> clong-dispatch -> uniform-opaque). Folded from satellite-expansion-roadmap cross-cutting facts.]

## 3. Opaque Handles Implementation Mechanism

[OpaqueHandleEmitRewriter design, auto-detect criterion (empty partial struct + pointer use), family-keyed roster integration, cross-family handle name resolution data pull, drift watchdog. Folded from constitution Opaque Handles Implementation mechanism cuts + item-1-spec audit fold.]

## 4. C `long` Hybrid Implementation

[ClongDualDispatchRewriter design, per-header RSP exclude pattern for SDL_stdinc convenience helpers, hybrid emit shape: Compat dual-dispatch via RuntimeInformation vs Modern CULong + LibraryImport. Folded from constitution C long Priority C cuts.]

## 5. wchar_t Implementation Mechanism

[RSP-level --remap literal-space pattern in base.rsp (wchar_t *=nint and const wchar_t *=nint), WcharStarToNintRewriter fallback. Folded from constitution wchar_t Priority C mechanism cuts.]

## 6. SDL_GUID Substitution

[GuidSubstitutionRewriter design - walks NativeTypeName("SDL_GUID") annotations, rewrites managed type to System.Guid. Folded from constitution.]

## 7. Foreign Type Boundary Implementation

[Per-header RSP --remap pattern for foreign-type allow-list (Vulkan, Direct3D, GDK, Win32, JNI, FILE, va_list). Active vs Deferred dispositions per active platform-view passes. Folded from constitution Foreign Type Boundary disposition column cut.]

## 8. `[Flags]` Detection Mechanism

[FlagsAttributeRewriter suffix-rule + family-keyed allow-list, power-of-two heuristic rejected. Folded from constitution Enums policy + item-1-spec audit fold.]

## 9. Cross-Assembly Pattern B Contract

[`[assembly: DisableRuntimeMarshalling]` mechanism, NET7_0_OR_GREATER gating, SYSLIB1051 resolution. Folded from constitution cross-assembly clause.]

## 10. Oracle Classification Labels

Six classification labels used in `oracle-evidence-clangsharp.md`:

- **Hard Bug** — contradicts pinned headers, native exports, or the binding-generator constitution.
- **Likely Bug** — strong evidence, but one source is missing or incomplete.
- **Accepted Deferral** — intentional policy deferral with a documented source.
- **Compatibility Risk** — mismatch with SDL2-CS that may affect migration but is not target truth.
- **Evidence Missing** — input source unavailable locally or not wired yet.
- **Out Of Scope** — source is not valid for this family or category.

[Folded from oracle-evidence-design.md audit fold.]

## 11. Hardcoding Rules

[Good code-owned policy vs good config-owned facts vs bad patterns. Folded from generator-spike-goals.md audit fold.]

## 12. Config Surface Evolution

[18-source pre-unification config inventory + classification against Constitution §"Manifest Configuration Vs Code-Owned Policy" + post-unification single source (family-config.json). Folded from iteration-2-config-surface-unification-spec audit fold.]

## 13. Postprocess Pipeline Mechanism Detail

[Detailed mechanism per rewriter class (cross-link to sections 3-9). Folded from satellite-expansion-roadmap cross-cutting facts + per-rewriter design notes from items/*-spec audit folds.]

## References

- [`binding-generator-constitution.md`](binding-generator-constitution.md) — policy authority for every rule this doc implements.
- [`binding-generator-maintenance.md`](binding-generator-maintenance.md) — operational procedures for maintaining the implementation described here.
- [`../../../../docs/decisions/2026-05-14-binding-autogen-toolchain.md`](../../../../docs/decisions/2026-05-14-binding-autogen-toolchain.md) — ADR-004 toolchain decision.
- [`../../../../docs/knowledge-base/extraction-guidelines.md`](../../../../docs/knowledge-base/extraction-guidelines.md) — collaborator extraction discipline.
- `spikes/binding-generators/clangsharp/postprocess/` — source code for the rewriter classes referenced throughout.
- `spikes/binding-generators/clangsharp/config/family-config.json` — schema for the configuration surface.
```

The bracketed `[Section content folded from ...]` placeholders are intentional in this initial scaffold; **fill each section with actual content in subsequent steps**, drawing from the source material listed at the top of Task 3.1.

- [ ] **Step 2: Fill Section 1 (Generation Determinism Implementation)**

Open `binding-generator-implementation-notes.md`. Replace the `[Section content folded from ...]` placeholder in section 1 with actual content from constitution Determinism Contract cuts (Task 2.1 Step 4) + the verification contract specifics. Reference family-config.json schema (don't paste full schema).

- [ ] **Step 3: Fill Section 2 (Postprocess Pipeline Order)**

Replace placeholder with 7-step pipeline rationale: each step's purpose, when it runs (Modern vs Compat), what concept it owns. Source: satellite-expansion-roadmap.md cross-cutting facts (audit fold).

- [ ] **Step 4: Fill Section 3 (Opaque Handles Implementation Mechanism)**

Replace placeholder with `OpaqueHandleEmitRewriter` mechanism. Cover: auto-detect criterion, family-keyed roster integration (cite family-config.json `opaque_handles.{auto_detect_well_known, force_opaque_exceptions, excluded_candidates}` schema), cross-family handle name resolution data pull, drift watchdog per-family scope.

- [ ] **Step 5: Fill Section 4 (C `long` Hybrid Implementation)**

Replace placeholder with `ClongDualDispatchRewriter` mechanism. Cover: per-header RSP exclude for SDL_stdinc helpers, Compat dual-dispatch via `RuntimeInformation.IsOSPlatform(OSPlatform.Windows)`, Modern `CULong` + `LibraryImport`, csproj `<Compile Include>` conditional gating (Compat → netstandard2.0/net462, Modern → net8.0+).

- [ ] **Step 6: Fill Section 5 (wchar_t Implementation Mechanism)**

Replace placeholder with RSP `--remap` literal-space pattern. Document the two entries in base.rsp (`wchar_t *=nint`, `const wchar_t *=nint`). Mention `WcharStarToNintRewriter` as Roslyn postprocess fallback.

- [ ] **Step 7: Fill Section 6 (SDL_GUID Substitution)**

Replace placeholder with `GuidSubstitutionRewriter` mechanism. Walks `[NativeTypeName("SDL_GUID")]` annotations; rewrites managed type to `System.Guid` (16-byte wire-identical).

- [ ] **Step 8: Fill Section 7 (Foreign Type Boundary Implementation)**

Replace placeholder with the disposition table cut from constitution (Active / Deferred categorization with per-header RSP locations). Folded as-is or as a condensed table.

- [ ] **Step 9: Fill Section 8 (`[Flags]` Detection Mechanism)**

Replace placeholder with `FlagsAttributeRewriter` mechanism: suffix-rule (`EndsWith("Flags")` case-sensitive) + family-keyed allow-list (cite `family-config.json` `flags_enums.allow_list`); power-of-two heuristic rejection rationale.

- [ ] **Step 10: Fill Section 9 (Cross-Assembly Pattern B Contract)**

Replace placeholder with `[assembly: DisableRuntimeMarshalling]` mechanism, NET7_0_OR_GREATER gating, SYSLIB1051 resolution rationale.

- [ ] **Step 11: Fill Section 11 (Hardcoding Rules)**

Replace placeholder with content from `generator-spike-goals.md` "Hardcoding Rules (unchanged — these survived the spike intact)" section: good code-owned policy list / good config-owned facts list / bad patterns list. Adapt language to be ClangSharp-aware where the original said "TypeMappingPolicy" (Cake-impl class name).

- [ ] **Step 12: Fill Section 12 (Config Surface Evolution)**

Replace placeholder with 18-source pre-unification config inventory (from `iteration-2-config-surface-unification-spec.md` audit fold). Document classification against Constitution §"Manifest Configuration Vs Code-Owned Policy". Note post-unification single source: `family-config.json` schema 1.0.

- [ ] **Step 13: Fill Section 13 (Postprocess Pipeline Mechanism Detail)**

Replace placeholder with per-rewriter design notes from items/*-spec audit folds. Cross-link to sections 3-9 above. Document scope-bounded discipline (each rewriter targets one concept).

- [ ] **Step 14: Run markdownlint + verify no `[Section content folded from ...]` placeholders remain**

```pwsh
Select-String -Path spikes/binding-generators/docs/canonical/binding-generator-implementation-notes.md -Pattern '\[Section content folded'
```

Expected: no matches.

- [ ] **Step 15: WIP commit implementation-notes draft**

```pwsh
git add spikes/binding-generators/docs/canonical/binding-generator-implementation-notes.md
git commit -m "docs(binding-autogen): write binding-generator-implementation-notes.md from audit folds

NEW sibling doc to the purified constitution. Captures mechanism + evidence:
- Sections 1-9: implementation mechanism for Determinism Contract, Postprocess
  Pipeline, Opaque Handles, C long, wchar_t, SDL_GUID, Foreign Type Boundary,
  [Flags] detection, Cross-Assembly Pattern B contract
- Section 10: Oracle Classification Labels (folded from oracle-evidence-design)
- Section 11: Hardcoding Rules (folded from generator-spike-goals)
- Section 12: Config Surface Evolution (folded from iteration-2-config-surface-unification-spec)
- Section 13: Postprocess Pipeline Mechanism Detail (folded from items/*-spec + satellite-expansion-roadmap)

Cross-links constitution sections by name (Rule 3) and cites postprocess class
names + family-config.json schema (not line numbers).

Refs spec Phase 3.1 + audit landing tables (multiple sources folded).

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

### Task 3.2: Write `binding-generator-maintenance.md` (NEW ClangSharp+Roslyn-adapted)

**Files created:**

- `spikes/binding-generators/docs/canonical/binding-generator-maintenance.md`

**Source material:**

- Spec §"New Documents — Content Outlines" content outline for maintenance playbook (11 sections).
- Sunset playbook (`docs/playbook/binding-generator-maintenance.md`) audit fold — toolchain-independent maintenance patterns adapted for ClangSharp.
- `family-config.json` schema (already shown in conversation).
- `spikes/binding-generators/clangsharp/generate_bindings.py` (orchestrator).
- `spikes/binding-generators/clangsharp/postprocess/Program.cs` (CLI dispatch — 7 conceptual steps).
- `spikes/binding-generators/Directory.Build.props` (spike-local build config).
- AGENTS.md Slopwatch exclude string + common commands.

- [ ] **Step 1: Create file with frontmatter + Purpose section**

Write to `spikes/binding-generators/docs/canonical/binding-generator-maintenance.md`:

```markdown
# Binding Generator Maintenance Playbook

> **Status:** Operational procedures for maintaining the active ClangSharp + Roslyn postprocess binding generator. Cross-references mechanism rationale in [`binding-generator-implementation-notes.md`](binding-generator-implementation-notes.md) and policy in [`binding-generator-constitution.md`](binding-generator-constitution.md).

## 1. Purpose And When To Use

[Coverage scope: when this playbook is the right reference, and what kinds of maintenance it covers.]

## 2. Toolchain Version Pinning

[ClangSharp tool + libclang.runtime + Microsoft.CodeAnalysis + oracle.cs package directive. Coherent-set pinning discipline.]

## 3. Configuration Surface Map

[family-config.json schema field-by-field documentation.]

## 4. RSP File Maintenance

[Three-tier RSP organization, when to add an entry, type-remap discipline.]

## 5. Postprocess Pipeline Maintenance

[7-step order, when to add a new rewriter, scope-bounded discipline.]

## 6. Version-Bump Procedures

[SDL2 version bump / ClangSharp tool version bump / Microsoft.CodeAnalysis postprocess version bump / vcpkg triplet bump.]

## 7. Adding A New SDL Satellite Family

[Checklist for new family addition.]

## 8. Drift Detection Between Versions

[Diff commands, oracle report comparison, AbiTests pass counts.]

## 9. Linux-Canonical Generation

[Docker container procedure; --use-platform-header-shims local-only.]

## 10. Build-Host Configuration

[spikes/binding-generators/Directory.Build.props isolation contract; references to root build files.]

## 11. References

[Cross-link to constitution, implementation-notes, ADR-004, family-config.json, generate_bindings.py.]
```

The bracketed `[...]` placeholders are intentional scaffold; **fill each section with actual content in subsequent steps**.

- [ ] **Step 2: Fill Section 1 (Purpose And When To Use)**

Replace placeholder with content covering: SDL version bumps (`library_version` field per family), vcpkg baseline updates, ClangSharp tool version bumps, Microsoft.CodeAnalysis postprocess version bumps, new satellite family addition, SDL3 planning, overlay/triplet changes. Distinguish maintenance from operational dev work.

- [ ] **Step 3: Fill Section 2 (Toolchain Version Pinning)**

Replace placeholder. Document the coherent-set pinning discipline:

- ClangSharp tool: pinned via `dotnet-tools.json` (cite the file path; do not paste version).
- `libclang.runtime.*`: pinned transitively via ClangSharp tool — record current major-version line (20.x).
- `Microsoft.CodeAnalysis.CSharp`: pinned via `VersionOverride` in `spikes/binding-generators/clangsharp/postprocess/Janset.SDL2.PostProcess.csproj`.
- `oracle.cs` `#:package` directive: pin `Microsoft.CodeAnalysis.CSharp@4.12.0` (or current).

Bump procedure: update all coherent-set members together; never bump one without coordinated revalidation.

- [ ] **Step 4: Fill Section 3 (Configuration Surface Map)**

Replace placeholder. Document `family-config.json` schema field-by-field:

```markdown
- `global.platform_views[]` — 7 SDL2 platform views (Windows desktop, WinRT, GDK, Linux, macOS, iOS, Android) with `name`, `supported_os`, `defines[]`.
- `global.all_platform_macros[]` — cross-contamination undefine set (28 macros).
- `global.base_rsp` — cross-cutting RSP file path.
- `families.<family>.namespace` — managed namespace.
- `families.<family>.library_version` — pinned SDL family version; **bump this on SDL version updates**.
- `families.<family>.raw_class` — internal raw ABI class name.
- `families.<family>.include_subdir` — vcpkg include subdir.
- `families.<family>.library_name` — native library import name.
- `families.<family>.rsp` — family RSP file path.
- `families.<family>.project_dir` — managed csproj folder.
- `families.<family>.owner_mode` — true if family owns its own Pattern B handles emit.
- `families.<family>.headers[]` — ordered list of header filenames.
- `families.<family>.platform_sensitive_headers[]` — headers needing platform-view passes.
- `families.<family>.required_surface` — required functions + constants (Core only).
- `families.<family>.opaque_handles.{auto_detect_well_known, force_opaque_exceptions, excluded_candidates}` — Pattern B handle roster; three-source triangulated audit method (release headers + wiki + ClangSharp Modern output empty-struct verification).
- `families.<family>.flags_enums.allow_list` — `[Flags]` allow-list for composite-alias enums.
- `families.<family>.clong_methods[]` — symbols using hybrid CLong/dual-dispatch emit.
```

Audit cadence: `opaque_handles.last_audited` + `flags_enums.last_audited` updated on every SDL version bump.

- [ ] **Step 5: Fill Section 4 (RSP File Maintenance)**

Replace placeholder. Document:

- Three-tier organization: `base.rsp` (cross-cutting) + `sdl2-<family>.rsp` (family identity + family-scope exclusions) + `per-header/<header>.rsp` (per-header overrides).
- When to add an entry: type-remap (when a foreign type or platform-sensitive type needs `--remap`), exclude (when a symbol should not emit), define-macro (when scope macros need recognition).
- Type-remap discipline: literal-space `--remap` entries match libclang byte-exact (`wchar_t *=nint` includes the space; `const wchar_t *=nint` is separate).
- Additive-only semantics: ClangSharp rejects duplicate keys; per-header RSP entries cannot override family RSP entries on the same key.

- [ ] **Step 6: Fill Section 5 (Postprocess Pipeline Maintenance)**

Replace placeholder. Document:

- 7-step order (`platform-delta` → `strip-varargs` → `libraryimport` Modern only → `flags-detect` → `guid-substitute` → `clong-dispatch` → `uniform-opaque`).
- When to add a new rewriter: when a new policy mechanism is needed (not covered by RSP); scope-bounded (one concept per rewriter).
- Compat vs Modern: `libraryimport` is Modern-only; all others operate on both.
- Self-test: each rewriter has self-tests in the postprocess project; run after changes via `dotnet run --project spikes/binding-generators/clangsharp/postprocess/Janset.SDL2.PostProcess.csproj -c Release -- self-test`.

- [ ] **Step 7: Fill Section 6 (Version-Bump Procedures)**

Replace placeholder. Four procedures, each as a numbered checklist:

```markdown
### 6.1 SDL2 Version Bump (e.g., 2.32.10 → 2.x.y)

1. Update vcpkg port to new version (separate work item; outside this playbook).
2. Update `family-config.json` `families.<family>.library_version` for affected families.
3. Regenerate: `python spikes/binding-generators/clangsharp/generate_bindings.py --family all --execute --vcpkg-triplet x64-windows-hybrid --use-platform-header-shims`.
4. Diff: `git diff --ignore-cr-at-eol spikes/binding-generators/clangsharp/src/Janset.SDL2.*/Generated/`.
5. Run oracle: `dotnet run --file spikes/binding-generators/clangsharp/oracle.cs -- --family sdl2-core --family sdl2-image --family sdl2-ttf --family sdl2-mixer --family sdl2-gfx --write-report`.
6. Compare oracle deltas vs prior `oracle-evidence-clangsharp.md`.
7. Update `family-config.json` `opaque_handles.last_audited` + `flags_enums.last_audited` dates.
8. Run AbiTests: `dotnet test --project spikes/binding-generators/clangsharp/tests/abi-tests/AbiTests.csproj -c Release`.
9. Run Slopwatch: `slopwatch analyze --fail-on warning --exclude "artifacts/**,external/**,vcpkg_installed/**,spikes/binding-generators/references/**,**/bin/**,**/obj/**"`.
10. Commit with version-bump-shaped message.

### 6.2 ClangSharp Tool Version Bump

[Numbered checklist analogous to 6.1.]

### 6.3 Microsoft.CodeAnalysis Postprocess Version Bump

[Numbered checklist.]

### 6.4 Vcpkg Triplet Bump

[Numbered checklist.]
```

Fill each sub-procedure with similar detail.

- [ ] **Step 8: Fill Section 7 (Adding A New SDL Satellite Family)**

Replace placeholder with new-family addition checklist:

```markdown
1. Manifest entry: add to `build/manifest.json` `package_families[]` + `library_manifests[]`.
2. `family-config.json` family entry with all required fields (see Section 3).
3. Family RSP: `spikes/binding-generators/clangsharp/rsp/sdl2-<family>.rsp` with family identity + family-scope exclusions.
4. Per-header RSP overlays: if any header needs overrides, `rsp/per-header/<header>.rsp`.
5. Managed csproj: `spikes/binding-generators/clangsharp/src/Janset.SDL2.<Family>/Janset.SDL2.<Family>.csproj` with `<ProjectReference>` to Core + multi-TFM compile-include conditions.
6. `Support/DisableRuntimeMarshalling.cs` in the new csproj.
7. Opaque handle audit (3-source triangulation: release headers + wiki + ClangSharp Modern empty-struct verification).
8. Flags enum audit.
9. clong_methods audit.
10. Oracle activation: extend `oracle.cs` `FamilyConfigs` if not already family-parameterized.
11. AbiTests expansion: add family-specific tests under `tests/abi-tests/`.
12. Slopwatch + multi-TFM build verification.
```

- [ ] **Step 9: Fill Section 8 (Drift Detection Between Versions)**

Replace placeholder. Document:

- `git diff --ignore-cr-at-eol` after regeneration to detect output drift.
- Oracle report diff (before/after `oracle-evidence-clangsharp.md`) for ABI-policy compliance.
- AbiTests pass counts: expected total stays stable; new failures investigate.
- Dynapi coherence percentage: current 98.1% for SDL2.Core; track per-bump.
- Slopwatch: 0 issues required.

- [ ] **Step 10: Fill Section 9 (Linux-Canonical Generation)**

Replace placeholder. Document:

- Production-evidence target: native Linux generation via `docker/binding-generator.Dockerfile`.
- Local Windows iteration: `--use-platform-header-shims` flag adds `clangsharp/shims/platform-headers/` (synthetic `endian.h`, `AvailabilityMacros.h`, `TargetConditionals.h`) — local-iteration only, not production substitute.
- Docker command pattern (cite the form from spike README L186-188).

- [ ] **Step 11: Fill Section 10 (Build-Host Configuration)**

Replace placeholder. Document:

- `spikes/binding-generators/Directory.Build.props` keeps spike projects isolated from production multi-TFM inheritance.
- References to root `Directory.Build.props`, `Directory.Packages.props`, `global.json` from spike isolation discipline.
- When the spike retires (at Production Flip), this isolation goes away — production code lives under `src/Janset.SDL2.*/` with full repo build conventions.

- [ ] **Step 12: Fill Section 11 (References)**

Replace placeholder:

```markdown
- [`binding-generator-constitution.md`](binding-generator-constitution.md) — policy authority.
- [`binding-generator-implementation-notes.md`](binding-generator-implementation-notes.md) — mechanism + classification labels + hardcoding rules.
- [`binding-generator-roadmap.md`](binding-generator-roadmap.md) — forward layer-based milestones.
- [`../../../../docs/decisions/2026-05-14-binding-autogen-toolchain.md`](../../../../docs/decisions/2026-05-14-binding-autogen-toolchain.md) — ADR-004.
- `spikes/binding-generators/clangsharp/config/family-config.json` — unified family configuration schema.
- `spikes/binding-generators/clangsharp/generate_bindings.py` — Python orchestrator.
- `spikes/binding-generators/clangsharp/postprocess/Janset.SDL2.PostProcess.csproj` — postprocess C# project.
- `spikes/binding-generators/clangsharp/oracle.cs` — Roslyn evidence reporter.
- `spikes/binding-generators/Directory.Build.props` — spike-local build isolation.
- `docker/binding-generator.Dockerfile` — production Linux generation container.
```

- [ ] **Step 13: Run markdownlint + verify no `[...]` placeholders remain**

```pwsh
Select-String -Path spikes/binding-generators/docs/canonical/binding-generator-maintenance.md -Pattern '^\[.*\]$'
```

Expected: no matches (placeholders all filled).

- [ ] **Step 14: WIP commit new maintenance playbook**

```pwsh
git add spikes/binding-generators/docs/canonical/binding-generator-maintenance.md
git commit -m "docs(binding-autogen): write new ClangSharp+Roslyn-adapted maintenance playbook

NEW playbook replacing the sunset Cake/CppAst-targeted version. 11 sections:
1. Purpose / When To Use
2. Toolchain Version Pinning (ClangSharp + libclang + Microsoft.CodeAnalysis + oracle.cs)
3. Configuration Surface Map (family-config.json schema field-by-field)
4. RSP File Maintenance (three-tier organization)
5. Postprocess Pipeline Maintenance (7-step order)
6. Version-Bump Procedures (SDL2 / ClangSharp tool / postprocess / vcpkg triplet)
7. Adding A New SDL Satellite Family (12-step checklist)
8. Drift Detection Between Versions
9. Linux-Canonical Generation (docker container + shim flag)
10. Build-Host Configuration (Directory.Build.props isolation)
11. References

Toolchain-independent maintenance patterns from the sunset playbook (trio pinning
concept, parse-options audit, dynapi cross-check workflow, satellite enablement
checklist) adapted to ClangSharp + Roslyn shape per spec Q11 expansion.

Sunset binding-generator-maintenance.md retires in Phase 6 after this playbook lands.

Refs spec Phase 3.2 + audit landing table (sunset playbook fold).

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

### Task 3.3: Multi-Agent Review Checkpoint 2 (Post-Constitution + Implementation-Notes)

**Per spec §"Checkpoint 2 — Post-Constitution Purification + Implementation-Notes Drafting".**

- [ ] **Step 1: Dispatch Explore agent — purified constitution coverage check**

Use Agent tool with `subagent_type: "Explore"`:

```text
Prompt: Compare the purified constitution against the pre-purification version (via git history) for missing policy content.

Files:
- Current (purified): spikes/binding-generators/docs/canonical/binding-generator-constitution.md
- Pre-purification: same file, two commits earlier (use `git show HEAD~3:docs/binding-autogen/binding-generator-constitution.md` or similar)

For each major section in the pre-purification version, verify the purified version still contains the durable principle. Flag any policy content that appears to have been cut without landing in implementation-notes.

Cross-check the implementation-notes draft at:
- spikes/binding-generators/docs/canonical/binding-generator-implementation-notes.md

to confirm each cut item has a corresponding section in implementation-notes.

Report findings under 400 words; list specifics with section names.
```

- [ ] **Step 2: Dispatch general-purpose agent — policy-vs-mechanism boundary check**

Use Agent tool with `subagent_type: "general-purpose"`:

```text
Prompt: Read the implementation-notes draft and flag any content that is actually POLICY (should be in constitution) vs MECHANISM (correctly placed here).

File:
- spikes/binding-generators/docs/canonical/binding-generator-implementation-notes.md

For each of the 13 sections, classify the content as:
- Mechanism (how the policy is realized)
- Policy (what the rule actually is)
- Evidence (artifacts, classification labels, etc.)

Flag any section where policy content has leaked from constitution to implementation-notes. Report under 400 words; cite section numbers.
```

- [ ] **Step 3: Consolidate review findings**

Read both reports. If reviewers found policy-content leakage or missing cuts, edit the constitution + implementation-notes inline.

- [ ] **Step 4: Apply corrections if any**

If corrections made, amend the relevant commit (`git commit --amend`) OR create a follow-up commit per AGENTS.md "never amend after pre-commit hook failure" rule. Prefer follow-up commit.

- [ ] **Step 5: Exit gate**

Confirm: policy-vs-mechanism boundary defensible; no durable policy lost. Checkpoint 2 passes.

### Task 3.4: Multi-Agent Review Checkpoint 5 (Post-Maintenance-Playbook)

**Per spec §"Checkpoint 5 — Post-New-Maintenance-Playbook Drafting".**

- [ ] **Step 1: Dispatch Explore agent — family-config.json schema coverage**

Use Agent tool with `subagent_type: "Explore"`:

```text
Prompt: Cross-check the new maintenance playbook against family-config.json for field-by-field coverage.

Files:
- Playbook: spikes/binding-generators/docs/canonical/binding-generator-maintenance.md
- Schema: spikes/binding-generators/clangsharp/config/family-config.json

For each field in the schema, verify the playbook documents:
- What it does
- When to edit it
- What invariants it must satisfy

Report any uncovered or thinly-covered field. Under 400 words.
```

- [ ] **Step 2: Dispatch general-purpose agent — simulate version-bump scenario**

Use Agent tool with `subagent_type: "general-purpose"`:

```text
Prompt: Simulate a hypothetical maintenance task and identify missing steps in the playbook.

Scenario: "Bump SDL2 from 2.32.10 to 2.33.0 (hypothetical new release)."

Walk through the playbook step-by-step at:
- spikes/binding-generators/docs/canonical/binding-generator-maintenance.md

following Section 6.1 (SDL2 Version Bump Procedure). For each step, verify:
- The command/instruction is concrete and actionable.
- Expected output is described or reasonably inferable.
- Failure modes (what to do if step fails) are addressed where non-trivial.

Flag any step that an unfamiliar agent could not execute without external context. Under 400 words.
```

- [ ] **Step 3: Consolidate findings + apply corrections if any**

- [ ] **Step 4: Exit gate**

Confirm: maintenance playbook covers version bumps + family-config schema + RSP + postprocess pipeline + drift detection + new-family addition. Checkpoint 5 passes.

---

## Phase 4 — Restructure `next-iteration-plan.md`

**Goal:** Cut historical slice progress; absorb forward backlog from `satellite-expansion-roadmap.md`; restructure around Layer 2 active iteration. After this phase, `satellite-expansion-roadmap.md` is ready for retirement (Phase 6).

### Task 4.1: Cut historical content from `next-iteration-plan.md`

**Files modified:**

- `spikes/binding-generators/docs/next-iteration-plan.md`

- [ ] **Step 1: Cut Slice progress table historical rows**

Slices 1-5, Priority A/B/C, Item 1, Iteration 2, Items 2-5 are all closed. Cut all `✅ done` / `✅ closed` rows from the Slice progress table. Keep forward rows (Layer 2 — typed public API row + Layer 3 friendly overloads row + future rows).

- [ ] **Step 2: Cut Current Evidence Snapshot section (2026-05-28)**

This section is a point-in-time snapshot. The durable facts (5 families closed, multi-TFM clean, oracle 0 findings) move to the new roadmap "What's Done" section in Task 2.2. Cut from next-iteration-plan.

- [ ] **Step 3: Cut Oracle Repair Queue section**

All A/B/C priority oracle gaps closed per the doc itself. Cut entire section.

- [ ] **Step 4: Cut Detailed Slices section (Slices 1-6+)**

Slices 1-5 closed; Slice 6 is Layer 2/3 forward work (covered by the roadmap's Layer 2/3 forward milestones now). Cut all closed slice detail. If anything from Slice 5 (deferred ppy orchestrator feature parity) remains forward-relevant, fold into the Review Follow-up Backlog.

- [ ] **Step 5: Cut Open Questions section**

Most questions are stale (resolved by Layer 1 closure). Cut entire section.

- [ ] **Step 6: Cut Cross-references section duplicates**

Cross-references to closure summaries that retire (priority-c-closure-summary) — cut. References that remain (constitution, roadmap) — keep with path updates to canonical/.

### Task 4.2: Absorb forward backlog from `satellite-expansion-roadmap.md`

**Files modified:**

- `spikes/binding-generators/docs/next-iteration-plan.md`

**Files read (for fold):**

- `spikes/binding-generators/docs/satellite-expansion-roadmap.md`

- [ ] **Step 1: Read satellite-expansion-roadmap.md forward backlog sections**

Two sections feed forward: "What This Roadmap Does NOT Cover" + "Review Follow-up Backlog — 2026-05-25" (handoff blockers + production flip gates + Layer 2/Layer 3 follow-ups + accepted tradeoffs).

- [ ] **Step 2: Fold forward backlog into next-iteration-plan**

Add a new section in `next-iteration-plan.md` called "Forward Backlog" with three subsections:

```markdown
## Forward Backlog

### Production Flip Gates

[Items required before generator becomes production source. Folded from satellite-expansion-roadmap "Production Flip Gates" subsection.]

### Layer 2 / Layer 3 Follow-ups

[Items belonging to Layer 2 typed public API + Layer 3 friendly overloads work. Folded from satellite-expansion-roadmap "Layer 2 / Layer 3 Follow-ups" subsection.]

### Accepted Tradeoffs / No Action

[Items deliberately accepted as-is. Folded from satellite-expansion-roadmap "Accepted Tradeoffs / No Action" subsection.]
```

Fill each subsection with the actual content from satellite-expansion-roadmap (the tables with Item / Why / Status columns).

### Task 4.3: Restructure around Layer 2 active iteration

**Files modified:**

- `spikes/binding-generators/docs/next-iteration-plan.md`

- [ ] **Step 1: Rewrite Status banner + top-of-doc framing**

Replace with:

```markdown
# Spike Next-Iteration Plan — Layer 2 Typed Public API

**Date:** 2026-05-28
**Status:** Active spike plan for Layer 2 typed public API work. SDL2 Layer 1 raw ABI closed across Core / Image / GFX / TTF / Mixer; canonical roadmap §"Layer 2 — Typed Public API Projection" is the milestone scope.

## Direction

Layer 2 takes the stable Layer 1 raw ABI surface and projects a public typed low-level API across the 5 SDL2 families. Public methods on the manifest-driven public class (e.g., `SDL2.SDL`) call the internal raw ABI class. Typed handles, enums, structs, callbacks, and constants remain public generated types per Constitution §"Layer Contract".

See [`canonical/binding-generator-roadmap.md`](canonical/binding-generator-roadmap.md) §"Layer 2 — Typed Public API Projection" for full scope, exit evidence, and non-goals.
```

- [ ] **Step 2: Add Active Iteration Tracking section**

Add a section that becomes the day-to-day forward tracker (replacing the old Slice progress + Detailed Slices structure):

```markdown
## Active Iteration

(To be filled by Layer 2 brainstorm/spec/plan cycle. This section tracks the current iteration's active work items.)
```

- [ ] **Step 3: Move/keep section ordering**

Final structure of `next-iteration-plan.md` after restructure:

```text
# Spike Next-Iteration Plan — Layer 2 Typed Public API
- Date / Status
- Direction
- Active Iteration (placeholder for future Layer 2 brainstorm output)
- Forward Backlog
  - Production Flip Gates
  - Layer 2 / Layer 3 Follow-ups
  - Accepted Tradeoffs / No Action
- Cross-References (path-updated for canonical/ location)
```

- [ ] **Step 4: Run markdownlint**

- [ ] **Step 5: WIP commit next-iteration-plan restructure**

```pwsh
git add spikes/binding-generators/docs/next-iteration-plan.md
git commit -m "docs(binding-autogen): restructure next-iteration-plan around Layer 2 + forward backlog absorption

Cut historical slice progress (Slices 1-5, Priority A/B/C, Items 1-5, Iteration 2
all closed - git log is the archive). Cut closed Oracle Repair Queue section,
Current Evidence Snapshot point-in-time, Detailed Slices closed-slice detail,
Open Questions (stale).

Absorbed forward backlog from satellite-expansion-roadmap.md:
- Production Flip Gates subsection
- Layer 2 / Layer 3 Follow-ups subsection
- Accepted Tradeoffs / No Action subsection

Added Direction (Layer 2 typed public API scope) + Active Iteration placeholder
for future Layer 2 brainstorm/spec/plan output.

satellite-expansion-roadmap.md ready for retirement (Phase 6).

Refs spec Phase 4 + Phase 0 audit Step 6 (forward backlog fold target).

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

### Task 4.4: Multi-Agent Review Checkpoint 4 (Post-Roadmap + Testing-Strategy + Next-Iteration-Plan)

**Per spec §"Checkpoint 4".**

- [ ] **Step 1: Dispatch Explore agent — roadmap forward completeness**

Use Agent tool with `subagent_type: "Explore"`:

```text
Prompt: Cross-check the restructured roadmap forward milestones against constitution Layer Contract + production-flip prerequisites + SDL3 gating (PD-7) for completeness.

Files:
- Roadmap: spikes/binding-generators/docs/canonical/binding-generator-roadmap.md
- Constitution Layer Contract: spikes/binding-generators/docs/canonical/binding-generator-constitution.md
- Release strategy SDL3 gating: docs/release-strategy.md
- Next-iteration plan: spikes/binding-generators/docs/next-iteration-plan.md

Verify:
- Each Layer 2/3 forward milestone has scope + exit evidence + non-goals.
- Production Flip milestone covers committed .g.cs, .generated-stamp, retire external/sdl2-cs.
- SysWM Layout milestone covers SDL_SysWMinfo/SDL_SysWMmsg typed-union work.
- SDL3 Extension milestone references PD-7 gating.

Cross-check next-iteration-plan Forward Backlog to ensure no forward work item is duplicated between roadmap milestones and backlog (or both should reference the same item).

Report under 400 words.
```

- [ ] **Step 2: Dispatch general-purpose agent — testing-strategy anchors preserved**

Use Agent tool with `subagent_type: "general-purpose"`:

```text
Prompt: Verify the testing-strategy fold preserved every anchor identified in spec Q5/Q9.

File:
- spikes/binding-generators/docs/canonical/testing-strategy.md

Verify the following content is present and intact:

1. Layer Model table.
2. Upstream Adoption Policy.
3. SDL2 Core Research Summary (upstream test tree audit).
4. SDL2 Satellite Research Summary.
5. Initial Coverage Matrix.
6. Implementation Backlog (smoke fixture Slices 1-9).
7. NEW: Raw ABI Upstream Port Backlog subsection (multi-agent slice tables for Rect, RWops, Surface, Pixels, GUID, Platform, Timer, Hints+Events, Audio, Video+Render, Math, Keyboard, Mouse, Stdlib, Main+Subsystems, Log, Virtual Joystick, Atomic+Mutex+Thread, Clipboard, Filesystem+Locale+Power+LoadSO, YUV+Iconv+Geometry).
8. Categories table (AbiSmoke / AbiUpstreamPort / AbiHeaderCoverage / AbiMechanical / AbiPure / AbiAssets / AbiGlobalState / AbiDummyDriver / AbiCapabilityGated / SDL.<Subsystem>).
9. Coverage Strategy targets (P0 70-100 / P1 80-130 / P2 20-40 / default 260-280 / aspirational 300+).
10. ABI risk families list.
11. Upstream Feasibility Summary (260-280 strong from 310 enabled upstream + high-value P0/P1 testautomation_*.c source list).
12. Known Generated Binding Blockers (SDL_syswm, thread creation, function-like macros).
13. Refactor Gate Mapping (rewritten layer-based, not M-numbering).
14. Research Sources URL list.

Flag any missing anchor. Under 400 words.
```

- [ ] **Step 3: Consolidate findings + apply corrections if any**

- [ ] **Step 4: Exit gate**

Confirm: roadmap forward direction validated; all testing anchors preserved; next-iteration-plan reflects current Layer 2 state. Checkpoint 4 passes.

---

## Phase 5 — Update Upstream References (Q14A.α)

**Goal:** Apply path updates + status refreshes + content rewrites to upstream docs that reference the binding-autogen tree. Section-name cross-references (Rule 3) replace line-number refs.

### Task 5.1: AGENTS.md L91 paragraph rewrite

**Files modified:**

- `AGENTS.md`

- [ ] **Step 1: Read AGENTS.md L91 current content**

Use Read tool on `AGENTS.md` lines ~88-93 (the "Binding autogen replaces SDL2-CS" row in the Settled Strategic Decisions table).

- [ ] **Step 2: Rewrite the L91 paragraph**

Replace the current paragraph (currently mentions "Toolchain selection under re-evaluation 2026-05-23: ADR-004 (Reopened) recorded the 2026-05-14 CppAst direction; the active selection happens in [`spikes/binding-generators/`](spikes/binding-generators/) between ClangSharp + Roslyn postprocess and Alimer-style single-pass CppAst...") with:

```text
Phase 4 replaces SDL2-CS imports with AST-generated bindings. Active implementation: **ClangSharp + Roslyn postprocess** under [`spikes/binding-generators/clangsharp/`](spikes/binding-generators/clangsharp/) with canonical policy at [`spikes/binding-generators/docs/canonical/`](spikes/binding-generators/docs/canonical/). SDL2 Layer 1 raw ABI closed 2026-05-28 across Core / Image / GFX / TTF / Mixer; Layer 2 typed public API + Layer 3 friendly overloads + production flip remain. ADR-004 status is **Reopened** pending formal amendment after Layer 2 closure. Generated API shape (internal raw ABI + public typed low-level API + friendly overloads) is policy in [`canonical/binding-generator-constitution.md`](spikes/binding-generators/docs/canonical/binding-generator-constitution.md). SDL2-CS compatibility is best-effort, not the design target.
```

- [ ] **Step 3: WIP commit AGENTS.md update**

```pwsh
git add AGENTS.md
git commit -m "docs(agents): update L91 Binding autogen Settled Strategic Decision

Refresh the paragraph to reflect post-spike state per spec Q14A.alpha:
- Active implementation: ClangSharp + Roslyn postprocess (formal toolchain
  commitment deferred to post-Layer-2 per Q13A)
- Canonical policy location: spikes/binding-generators/docs/canonical/
- SDL2 Layer 1 raw ABI closed 2026-05-28 across 5 families
- ADR-004 Reopened pending formal amendment

Neutral on formal toolchain commitment (Q13A); direction lean captured
implicitly via spike-path reference.

Refs spec Q14A.alpha + Q13A.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

### Task 5.2: `docs/plan.md` Phase 4 section refresh

**Files modified:**

- `docs/plan.md` (Phase 4 section L59-70)

- [ ] **Step 1: Read current Phase 4 section**

Use Read tool on `docs/plan.md` lines 59-72.

- [ ] **Step 2: Update path references (L62-63)**

Replace:

```text
Binding generator constitution: [binding-autogen/binding-generator-constitution.md](binding-autogen/binding-generator-constitution.md) ...
Binding generator roadmap: [binding-autogen/binding-generator-roadmap.md](binding-autogen/binding-generator-roadmap.md) ...
```

With:

```text
Binding generator constitution: [../spikes/binding-generators/docs/canonical/binding-generator-constitution.md](../spikes/binding-generators/docs/canonical/binding-generator-constitution.md) (canonical ABI/API/translation contract and manifest config-vs-policy rules).
Binding generator roadmap: [../spikes/binding-generators/docs/canonical/binding-generator-roadmap.md](../spikes/binding-generators/docs/canonical/binding-generator-roadmap.md) (layer-based forward milestones: Layer 2 typed public API, Layer 3 friendly overloads, Production Flip, SysWM Layout + Satellite Sweep Close, Smoke Expansion, SDL3 Extension).
Binding generator implementation notes: [../spikes/binding-generators/docs/canonical/binding-generator-implementation-notes.md](../spikes/binding-generators/docs/canonical/binding-generator-implementation-notes.md) (mechanism + classification labels + hardcoding rules).
Binding generator maintenance: [../spikes/binding-generators/docs/canonical/binding-generator-maintenance.md](../spikes/binding-generators/docs/canonical/binding-generator-maintenance.md) (ClangSharp + Roslyn postprocess maintenance playbook).
```

- [ ] **Step 3: Rewrite Status banner (L65)**

Replace the existing status banner with:

```text
**Status (2026-05-28):** SDL2 Layer 1 raw ABI closed across Core / Image / GFX / TTF / Mixer on the active ClangSharp + Roslyn postprocess implementation at `spikes/binding-generators/clangsharp/`. Layer 2 typed public API is next. Multi-TFM compile clean (5 TFMs); five-family oracle report clean across Priority C categories. Toolchain selection effectively decided pending formal ADR-004 amendment after Layer 2 closure. `SDL_syswm.h` typed-union layout remains under the SysWM Layout milestone. SDL3 binding generation gated on PD-7.
```

- [ ] **Step 4: Refresh Stage 1/2 work item text (L67-68)**

Keep checkboxes OPEN. Refresh text:

```text
- [ ] **Stage 1 — SDL2.Core generated-core readiness**: Layer 1 raw ABI closed (current state); Layer 2 typed public API + production flip retiring `external/sdl2-cs/src/SDL2.cs` remain. ([#69](https://github.com/janset2d/sdl2-cs-bindings/issues/69))
- [ ] **Stage 2 — SDL_syswm full union + SDL2 family sweep close + sdl2-cs retire**: SysWM Layout typed-union layout + remaining `external/sdl2-cs` production-use retirement + Pack-stage symbol-existence validator + SDL2_net addition. Satellite Layer 1 already closed for Image / GFX / TTF / Mixer. ([#70](https://github.com/janset2d/sdl2-cs-bindings/issues/70))
```

- [ ] **Step 5: WIP commit plan.md Phase 4 refresh**

```pwsh
git add docs/plan.md
git commit -m "docs(plan): refresh Phase 4 section for post-spike state

Path updates: docs/binding-autogen/ -> spikes/binding-generators/docs/canonical/.
Added new canonical sibling refs (implementation-notes + maintenance).
Status banner rewritten to reflect SDL2 Layer 1 closure across 5 families;
Layer 2 typed public API next. Stage 1/2 work-item text refreshed; checkboxes
stay open (production flip + Layer 2 + SysWM remain).

Refs spec Q14A.alpha + Phase 5.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

### Task 5.3: `docs/release-strategy.md` updates

**Files modified:**

- `docs/release-strategy.md`

- [ ] **Step 1: Read release-strategy.md sections L37-38 + L143-148**

Use Read tool.

- [ ] **Step 2: Update Cross-Reference list paths (L143-148)**

Replace:

```text
- [`binding-autogen/`](binding-autogen/) - binding generator workstream index, constitution, and roadmap
```

With:

```text
- [`../spikes/binding-generators/docs/canonical/`](../spikes/binding-generators/docs/canonical/) — binding generator workstream canonical docs (constitution, roadmap, testing strategy, implementation notes, maintenance playbook, oracle validation, per-family satellite analyses)
```

Update any other path refs to `docs/binding-autogen/` similarly.

- [ ] **Step 3: Minor cleanups in Stage 1/2 table (L37-38)**

Drop "via the toolchain selected by the spike" wording — toolchain selection effectively decided. Keep Stage 1/2 substance.

- [ ] **Step 4: WIP commit release-strategy.md update**

```pwsh
git add docs/release-strategy.md
git commit -m "docs(release-strategy): path refresh + Stage 1/2 minor cleanups

Cross-Reference list paths: binding-autogen/ -> spikes/binding-generators/docs/canonical/.
Stage 1/2 table: dropped 'via the toolchain selected by the spike' phrasing
(selection effectively decided; formal ADR-004 amendment deferred per Q13A).

Refs spec Q14A.alpha + Phase 5.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

### Task 5.4: ADR-004 status + path refresh (Q13A)

**Files modified:**

- `docs/decisions/2026-05-14-binding-autogen-toolchain.md`

- [ ] **Step 1: Update Status line**

Replace current `- **Status:** Reopened (re-evaluation underway 2026-05-23)` with:

```text
- **Status:** Reopened (re-evaluation underway since 2026-05-23; Layer 1 closure on ClangSharp + Roslyn postprocess spike completed 2026-05-28 for SDL2 Core/Image/GFX/TTF/Mixer; formal amendment deferred pending Layer 2 closure)
```

- [ ] **Step 2: Update Re-evaluation note banner**

Keep the banner; refresh the spike path reference + tighten wording. Replace:

```text
> The binding-autogen toolchain decision is under active re-evaluation in [`spikes/binding-generators/`](../../spikes/binding-generators/). Both **ClangSharp + Roslyn postprocess** and **Alimer-style single-pass CppAst** are being measured against the same evidence matrix...
```

With:

```text
> The binding-autogen toolchain decision is under active re-evaluation in [`spikes/binding-generators/`](../../spikes/binding-generators/). The active spike implementation is **ClangSharp + Roslyn postprocess** with canonical policy at [`spikes/binding-generators/docs/canonical/`](../../spikes/binding-generators/docs/canonical/) and Layer 1 raw ABI closure evidence at [`spikes/binding-generators/output/reports/`](../../spikes/binding-generators/output/reports/). Either successor toolchain implies replacing the sunset Cake implementation under `build/_build/Targets/GenerateBindings/`. This ADR remains historical evidence of the 2026-05-14 reasoning; formal amendment is deferred to post-Layer-2 closure.
```

- [ ] **Step 3: Update References section paths (Section 7)**

Replace `binding-autogen/` paths with `spikes/binding-generators/docs/canonical/` paths in the References list. Sections 1-6 (original CppAst rationale) untouched.

- [ ] **Step 4: WIP commit ADR-004 refresh**

```pwsh
git add docs/decisions/2026-05-14-binding-autogen-toolchain.md
git commit -m "docs(adr-004): status + path refresh per spec Q13A

Status banner updated to reflect Layer 1 closure on the ClangSharp + Roslyn
postprocess spike across 5 SDL2 families (2026-05-28); formal amendment
deferred pending Layer 2 closure. Re-evaluation note tightened with current
evidence location; sections 1-6 (original 2026-05-14 CppAst rationale) untouched
as historical record. References section path updates: binding-autogen/ ->
spikes/binding-generators/docs/canonical/.

Neutral on formal toolchain commitment; direction-lean captured implicitly
via spike-path reference.

Refs spec Q13A + Phase 5.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

### Task 5.5: `docs/phases/phase-4-binding-autogen.md` purify (Q12)

**Files modified:**

- `docs/phases/phase-4-binding-autogen.md`

- [ ] **Step 1: Strip Status banners + sunset Cake-impl paragraphs**

Strip the entire "Status (2026-05-23) / Toolchain re-evaluation (2026-05-23) ..." banner block at top. Keep a single Status line for the phase.

- [ ] **Step 2: Strip implementation-specific subsections from "What Phase 4 Delivers"**

Cut the paragraphs naming "Generator home", "Toolchain (sunset Cake impl)", "Local invocation (sunset Cake impl)", "Parse strategy", "Coherence guardrails" — these implementation specifics live in implementation-notes + maintenance playbook now. Keep the high-level "What Phase 4 Delivers" introductory paragraph.

- [ ] **Step 3: Refresh Canonical Docs table paths**

Update paths:

```text
| [`../../spikes/binding-generators/docs/canonical/binding-generator-constitution.md`](../../spikes/binding-generators/docs/canonical/binding-generator-constitution.md) | ... |
| [`../../spikes/binding-generators/docs/canonical/binding-generator-roadmap.md`](../../spikes/binding-generators/docs/canonical/binding-generator-roadmap.md) | ... |
| [`../../spikes/binding-generators/docs/canonical/binding-generator-implementation-notes.md`](../../spikes/binding-generators/docs/canonical/binding-generator-implementation-notes.md) | mechanism + classification labels |
| [`../../spikes/binding-generators/docs/canonical/binding-generator-maintenance.md`](../../spikes/binding-generators/docs/canonical/binding-generator-maintenance.md) | ClangSharp + Roslyn postprocess maintenance playbook |
| [`../decisions/2026-05-14-binding-autogen-toolchain.md`](../decisions/2026-05-14-binding-autogen-toolchain.md) | ADR-004 (Reopened) |
| [`../../spikes/binding-generators/`](../../spikes/binding-generators/) | Active spike |
```

- [ ] **Step 4: Simplify Stage Sequencing table**

Keep Stage 1 / Stage 2 / Stage 3 scope-axis sequencing (SDL2.Core → SDL2 satellites + SysWM → SDL3). Drop layer-specific implementation details from the rows; defer to roadmap layer-based milestones for implementation tracking.

- [ ] **Step 5: Refresh Cross-Reference section paths**

Update binding-autogen/ paths to canonical/.

- [ ] **Step 6: Verify target line count (~30-35 lines)**

```pwsh
(Get-Content docs/phases/phase-4-binding-autogen.md).Count
```

Expected: ~30-35 lines.

- [ ] **Step 7: WIP commit phase-4 purify**

```pwsh
git add docs/phases/phase-4-binding-autogen.md
git commit -m "docs(phases): purify phase-4-binding-autogen in place

Stripped sunset Cake-impl paragraphs (Generator home / Toolchain / Local
invocation / Parse strategy / Coherence guardrails) - those live in
implementation-notes + maintenance now. Status banner simplified to a single
line. Canonical Docs table paths updated to canonical/ subfolder. Stage 1/2/3
scope sequencing kept (orthogonal to layer-based roadmap milestones).
Target ~30-35 lines after cuts.

Refs spec Q12 + Phase 5.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

---

## Phase 6 — Retire Files

**Goal:** `git rm` all retirees per Phase 0 audit verdicts. Phase 6.2 multi-agent review checkpoint 3 runs after parking-lot retirement specifically (user-emphasized in Q10).

### Task 6.1: Retire parking-lot

**Files retired:**

- `docs/parking-lot/binding-autogen-cake-implementation/README.md`
- `docs/parking-lot/binding-autogen-cake-implementation/milestone-1-safety-harness-baseline.md`
- `docs/parking-lot/binding-autogen-cake-implementation/milestone-2-behavior-preserving-topology-refactor.md`
- `docs/parking-lot/binding-autogen-cake-implementation/milestone-3-profile-boundary.md`

- [ ] **Step 1: Confirm audit verdicts**

Re-read the parking-lot audit entries in `docs/superpowers/audits/2026-05-28-binding-autogen-audit-landing-tables.md`. Confirm each file has verdict "ready to git rm".

- [ ] **Step 2: git rm each file**

```pwsh
git rm docs/parking-lot/binding-autogen-cake-implementation/README.md
git rm docs/parking-lot/binding-autogen-cake-implementation/milestone-1-safety-harness-baseline.md
git rm docs/parking-lot/binding-autogen-cake-implementation/milestone-2-behavior-preserving-topology-refactor.md
git rm docs/parking-lot/binding-autogen-cake-implementation/milestone-3-profile-boundary.md
```

- [ ] **Step 3: Verify the parking-lot/binding-autogen-cake-implementation/ folder is implicitly removed**

```pwsh
Test-Path docs/parking-lot/binding-autogen-cake-implementation/
```

Expected: `False` after `git rm` (empty folder removes itself in git tree).

- [ ] **Step 4: WIP commit parking-lot retirement**

```pwsh
git add -u docs/parking-lot/binding-autogen-cake-implementation/
git commit -m "docs(binding-autogen): retire parking-lot/binding-autogen-cake-implementation per spec Q10

All 4 archived plans + parking-lot README retired after Phase 0 audit confirmed
unique-info kernels landed in destination docs per Rule 1 + Rule 2 (CppAst-
independent fold):
- M1 safety-harness pattern -> testing-strategy
- M2 parser/model/emitter separation -> verified in constitution
- M3 engine/policy boundary + M2 review findings -> constitution + implementation-notes
- Satellite reconnaissance findings -> verified in canonical/satellites/

git log --follow + git blame -C preserve history across retirement.

Refs spec Q10 + audit landing table.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

### Task 6.2: Multi-Agent Review Checkpoint 3 (Post-Parking-Lot Retirement)

**Per spec §"Checkpoint 3 — Post-Parking-Lot Audit (User-Emphasized in Q10)".**

- [ ] **Step 1: Dispatch Explore agent — CppAst-independent feature extraction verification**

Use Agent tool with `subagent_type: "Explore"`:

```text
Prompt: Verify that all CppAst-independent features from the now-retired M1/M2/M3 plans have a documented landing place in the destination docs.

Files (now retired but recoverable via `git show HEAD~N:...`):
- docs/parking-lot/binding-autogen-cake-implementation/milestone-1-safety-harness-baseline.md
- docs/parking-lot/binding-autogen-cake-implementation/milestone-2-behavior-preserving-topology-refactor.md
- docs/parking-lot/binding-autogen-cake-implementation/milestone-3-profile-boundary.md

Destination docs to verify:
- spikes/binding-generators/docs/canonical/binding-generator-constitution.md
- spikes/binding-generators/docs/canonical/binding-generator-implementation-notes.md
- spikes/binding-generators/docs/canonical/testing-strategy.md
- spikes/binding-generators/docs/canonical/satellites/*.md
- spikes/binding-generators/docs/canonical/binding-generator-maintenance.md

For each CppAst-independent feature/pattern in M1/M2/M3 (use `git show` to read the retired files), verify it has a corresponding landing place. Specifically check:
- Safety-harness pattern (M1) -> testing-strategy
- Parser/model/emitter separation principle (M2) -> constitution + implementation-notes
- Engine-owned concepts list (M3) -> constitution
- Manifest-owned facts list (M3) -> constitution
- Code-owned policy list (M3) -> constitution
- M2 review findings (foreign-type policy naming, platform macro hygiene, etc.) -> constitution + implementation-notes
- Satellite reconnaissance findings (M3) -> canonical/satellites/

Flag any feature without a documented landing place. Under 400 words.
```

- [ ] **Step 2: Dispatch general-purpose agent — new maintenance playbook adequately adapted sunset patterns**

Use Agent tool with `subagent_type: "general-purpose"`:

```text
Prompt: Verify the new ClangSharp + Roslyn maintenance playbook adequately adapted toolchain-independent maintenance patterns from the sunset Cake playbook.

Files:
- New playbook: spikes/binding-generators/docs/canonical/binding-generator-maintenance.md
- Sunset playbook (recoverable via `git show HEAD~N:docs/playbook/binding-generator-maintenance.md`)

Verify the new playbook covers (with ClangSharp + Roslyn shape):
- Trio pinning concept (sunset: CppAst + libclang + libClangSharp; new: ClangSharp tool + libclang.runtime + Microsoft.CodeAnalysis + oracle.cs)
- Parse-options audit discipline (sunset: CppAst parser options; new: RSP file review)
- Dynapi cross-check workflow (sunset: dynapi vs Cake oracle; new: dynapi vs oracle.cs report)
- Satellite enablement checklist (sunset: manifest binding_generation + Cake target; new: family-config.json + Janset.SDL2.<Family> csproj)
- Profile boundary maintenance (sunset: M3 profile design; new: family-config.json profile_id implicit + code-owned policy boundary per constitution)

Flag any sunset pattern that did not get adapted to the new playbook. Under 400 words.
```

- [ ] **Step 3: Consolidate findings + apply corrections if any**

If reviewers found gaps, edit the destination docs inline + create a follow-up commit.

- [ ] **Step 4: Exit gate**

Confirm: every CppAst-independent feature has a documented landing place; user-emphasized "no blind retirement" gate satisfied (Q10). Checkpoint 3 passes.

### Task 6.3: Retire sunset maintenance playbook

**Files retired:**

- `docs/playbook/binding-generator-maintenance.md`

- [ ] **Step 1: Confirm new playbook in canonical/ is complete**

Verify `spikes/binding-generators/docs/canonical/binding-generator-maintenance.md` exists with no placeholders (per Phase 3.2 Step 13).

- [ ] **Step 2: git rm sunset playbook**

```pwsh
git rm docs/playbook/binding-generator-maintenance.md
```

- [ ] **Step 3: WIP commit**

```pwsh
git commit -m "docs(playbook): retire sunset binding-generator-maintenance

Replaced by spikes/binding-generators/docs/canonical/binding-generator-maintenance.md
(ClangSharp + Roslyn postprocess adapted). docs/playbook/ retains
local-development.md (non-binding-autogen).

Refs spec Q11 + audit landing table.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

### Task 6.4: Retire 6 spike top-level docs

**Files retired:**

- `spikes/binding-generators/docs/llm-handoff.md`
- `spikes/binding-generators/docs/priority-c-closure-summary.md`
- `spikes/binding-generators/docs/generator-spike-goals.md`
- `spikes/binding-generators/docs/oracle-evidence-design.md`
- `spikes/binding-generators/docs/oracle-evidence-implementation-plan.md`
- `spikes/binding-generators/docs/satellite-expansion-roadmap.md`

- [ ] **Step 1: Confirm folds are complete**

Verify Phase 4.2 (forward backlog absorbed) + Phase 3.1 (hardcoding rules, classification labels, cross-cutting facts folded into implementation-notes) are committed.

- [ ] **Step 2: git rm the 6 files**

```pwsh
git rm spikes/binding-generators/docs/llm-handoff.md
git rm spikes/binding-generators/docs/priority-c-closure-summary.md
git rm spikes/binding-generators/docs/generator-spike-goals.md
git rm spikes/binding-generators/docs/oracle-evidence-design.md
git rm spikes/binding-generators/docs/oracle-evidence-implementation-plan.md
git rm spikes/binding-generators/docs/satellite-expansion-roadmap.md
```

- [ ] **Step 3: WIP commit**

```pwsh
git commit -m "docs(binding-autogen): retire 6 spike top-level docs per spec Q6

Retired (audit verdicts in docs/superpowers/audits/2026-05-28-binding-autogen-audit-landing-tables.md):
- llm-handoff.md (session-handoff state; working preferences in user memory; cross-refs absorbed into spike README refresh)
- priority-c-closure-summary.md (durable policy in constitution; closure evidence in git history)
- generator-spike-goals.md (hardcoding rules folded into implementation-notes section 11)
- oracle-evidence-design.md (classification labels folded into implementation-notes section 10)
- oracle-evidence-implementation-plan.md (tasks completed; oracle.cs is the canonical artifact)
- satellite-expansion-roadmap.md (cross-cutting facts folded into implementation-notes sections 12-13; forward backlog folded into next-iteration-plan; closure facts -> git history)

Refs spec Q6 + audit landing table.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

### Task 6.5: Retire 10 spike items/ files

**Files retired:**

- All 10 files under `spikes/binding-generators/docs/items/`

- [ ] **Step 1: Confirm folds for specs are complete**

Per Phase 0 audit Task 0.5 verdicts: 5 plans empty-kernel; 5 specs audit-fold-delete. Verify any fold targets identified for specs were absorbed in Phase 2 / Phase 3.

- [ ] **Step 2: git rm all 10 files**

```pwsh
git rm spikes/binding-generators/docs/items/item-1-per-library-generation-plan.md
git rm spikes/binding-generators/docs/items/item-1-per-library-generation-spec.md
git rm spikes/binding-generators/docs/items/iteration-2-config-surface-unification-plan.md
git rm spikes/binding-generators/docs/items/iteration-2-config-surface-unification-spec.md
git rm spikes/binding-generators/docs/items/item-3-sdl2-gfx-layer-1-plan.md
git rm spikes/binding-generators/docs/items/item-3-sdl2-gfx-layer-1-spec.md
git rm spikes/binding-generators/docs/items/item-4-sdl2-ttf-layer-1-plan.md
git rm spikes/binding-generators/docs/items/item-4-sdl2-ttf-layer-1-spec.md
git rm spikes/binding-generators/docs/items/item-5-sdl2-mixer-layer-1-plan.md
git rm spikes/binding-generators/docs/items/item-5-sdl2-mixer-layer-1-spec.md
```

- [ ] **Step 3: WIP commit**

```pwsh
git commit -m "docs(binding-autogen): retire items/ folder per spec Q7

All 5 plans + 5 specs retired:
- Plans (execution scaffolding, empty unique-info kernel): item-1, item-3, item-4, item-5, iteration-2
- Specs (audit-fold-delete; unique kernels folded per Phase 0 audit):
  - item-1-spec: OpaqueHandleEmitRewriter design -> implementation-notes section 3
  - iteration-2-spec: 18-source config inventory -> implementation-notes section 12
  - item-3/4/5-spec: family-specific design decisions cross-checked against canonical/satellites/*-header-analysis.md

items/ folder implicitly removed.

Refs spec Q7 + audit landing table.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

### Task 6.6: Retire 3 spike testing/ files (after fold from Phase 2.3)

**Files retired:**

- `spikes/binding-generators/docs/testing/raw-abi-current-inventory.md`
- `spikes/binding-generators/docs/testing/raw-abi-upstream-testing-spec.md`
- `spikes/binding-generators/docs/testing/raw-abi-upstream-testing-plan.md`

- [ ] **Step 1: Confirm Phase 2.3 fold is complete**

Verify testing-strategy.md absorbed Categories table + Coverage Strategy + ABI risk families + Upstream Feasibility Summary + multi-agent slice tables + Known Generated Binding Blockers.

- [ ] **Step 2: git rm the 3 files**

```pwsh
git rm spikes/binding-generators/docs/testing/raw-abi-current-inventory.md
git rm spikes/binding-generators/docs/testing/raw-abi-upstream-testing-spec.md
git rm spikes/binding-generators/docs/testing/raw-abi-upstream-testing-plan.md
```

- [ ] **Step 3: WIP commit**

```pwsh
git commit -m "docs(binding-autogen): retire spike testing/ folder per spec Q9

All 3 files retired after Phase 2.3 folded their durable content into the
purified canonical/testing-strategy.md:
- raw-abi-upstream-testing-spec.md: Categories table, Coverage Strategy + ABI risk
  families, Upstream Feasibility Summary + P0/P1 source list, ground rules
- raw-abi-upstream-testing-plan.md: multi-agent slice tables -> testing-strategy
  Implementation Backlog 'Raw ABI Upstream Port Backlog' subsection
- raw-abi-current-inventory.md: Known Generated Binding Blockers -> testing-strategy
  deferred-coverage notes; point-in-time test counts dropped (volatile)

testing/ folder implicitly removed.

Refs spec Q9 + audit landing table.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

### Task 6.7: Retire 3 `.github/prompts/` priming files

**Files retired:**

- `.github/prompts/binding-generator-spike-priority-c-handoff.prompt.md`
- `.github/prompts/satellite-expansion-research-cross-check.prompt.md`
- `.github/prompts/item-4-closure-item-5-mixer-handoff.prompt.md`

- [ ] **Step 1: git rm the 3 files**

```pwsh
git rm .github/prompts/binding-generator-spike-priority-c-handoff.prompt.md
git rm .github/prompts/satellite-expansion-research-cross-check.prompt.md
git rm .github/prompts/item-4-closure-item-5-mixer-handoff.prompt.md
```

- [ ] **Step 2: WIP commit**

```pwsh
git commit -m "docs(prompts): retire spike-specific priming prompts per spec Q15 amendment

3 priming prompts retired (spike-specific; Priority C closure + satellite
research + items 4-5 handoff all complete):
- binding-generator-spike-priority-c-handoff.prompt.md
- satellite-expansion-research-cross-check.prompt.md
- item-4-closure-item-5-mixer-handoff.prompt.md

.github/prompts/ retains generic templates (general-deep-dive-code-reviewer,
session-pickup-template).

Refs spec Q15 amendment + audit landing table.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

### Task 6.8: Retire 2 legacy output/reports/ files

**Files retired:**

- `spikes/binding-generators/output/reports/oracle-comparison-clangsharp.md`
- `spikes/binding-generators/output/reports/oracle-comparison-alimer.md`

- [ ] **Step 1: git rm both files**

```pwsh
git rm spikes/binding-generators/output/reports/oracle-comparison-clangsharp.md
git rm spikes/binding-generators/output/reports/oracle-comparison-alimer.md
```

- [ ] **Step 2: WIP commit**

```pwsh
git commit -m "docs(reports): retire 2 legacy oracle-comparison reports per spec Q15e

Both reports flagged 'legacy, not regenerated' in the current spike README
(L115-116). Superseded by:
- oracle-evidence-clangsharp.md (active oracle.cs output)
- iteration-2-comparison.md (toolchain decision evidence)

Remaining output/reports/ keepers: iteration-2-comparison.md,
clangsharp-failure-buckets.md, clangsharp-production.md,
oracle-evidence-clangsharp.md, alimer-full.md.

Refs spec Q15e + audit landing table.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

---

## Phase 7 — Refresh Spike README + Auxiliary

**Goal:** Major refresh of `spikes/binding-generators/README.md` to reflect new canonical/ tree. Audit + update `spikes/binding-generators/clangsharp/README.md` per Phase 0 Task 0.10.

### Task 7.1: Refresh spike-level README

**Files modified:**

- `spikes/binding-generators/README.md`

- [ ] **Step 1: Rewrite Status section (L5-11)**

Replace the existing Status section with:

```markdown
## Status — 2026-05-28

- **SDL2 Layer 1 raw ABI: CLOSED across Core / Image / GFX / TTF / Mixer** on the active ClangSharp + Roslyn postprocess implementation under `clangsharp/`. Multi-TFM compile clean across 5 TFMs. Five-family oracle report at `output/reports/oracle-evidence-clangsharp.md`.
- **Canonical policy + roadmap + testing strategy + maintenance + implementation notes** moved into `docs/canonical/` during the spike → production-flip transition. Restores to `docs/binding-autogen/` at Production Flip per roadmap §"Production Flip".
- **Next forward scope:** Layer 2 typed public API. See `docs/canonical/binding-generator-roadmap.md` §"Layer 2 — Typed Public API Projection" + `docs/next-iteration-plan.md` for active iteration tracking.
- **Branch:** `spike/binding-autogen-sdl2-gfx`. Push gate pending Deniz approval per AGENTS.md §Approval Gate.
```

- [ ] **Step 2: Refresh Read first table**

Rewrite the table to reflect the new canonical/ tree. Cut retired-doc rows; add new canonical/ doc rows; update paths.

```markdown
| Doc | Why |
| --- | --- |
| **`docs/canonical/README.md`** | **Canonical workstream index.** Read first to navigate the canonical/ tree. |
| **`docs/canonical/binding-generator-constitution.md`** | ABI/API/translation policy (pure principles). |
| **`docs/canonical/binding-generator-roadmap.md`** | Layer-based forward milestones. |
| **`docs/canonical/testing-strategy.md`** | Testing layer model + smoke + raw ABI upstream port backlog. |
| **`docs/canonical/binding-generator-implementation-notes.md`** | Mechanism + classification labels + hardcoding rules. |
| **`docs/canonical/binding-generator-maintenance.md`** | Version-bump procedures + family-config.json schema + RSP maintenance. |
| **`docs/canonical/binding-output-oracle-validation.md`** | Multi-oracle review workflow. |
| **`docs/canonical/satellites/`** | Per-family + cross-family satellite analyses. |
| **`docs/canonical/references/ppy-reference-analysis-2026-05-25.md`** | ppy/SDL3-CS reference analysis. |
| `docs/next-iteration-plan.md` | Active spike plan (Layer 2 iteration + forward backlog). |
| `docs/reference-clones.md` | Local clone commands for ppy + Alimer reference projects. |
| `output/reports/iteration-2-comparison.md` | Decision evidence (function counts, dynapi coherence, multi-TFM trajectory). |
| `output/reports/oracle-evidence-clangsharp.md` | Active family-aware raw ABI evidence. |
| `output/reports/clangsharp-failure-buckets.md` | 8 RSP-fix iteration history (useful when adding new exclude rules). |
```

- [ ] **Step 3: Refresh Decision recorded — 2026-05-21 section**

Keep as historical record but tighten path refs. The Alimer-scaffold-retained note stays.

- [ ] **Step 4: Refresh Layout section tree**

Update the tree to reflect new canonical/ subtree + removed docs:

```text
spikes/binding-generators/
├── README.md                                        # this file
├── BindingGeneratorSpikes.slnx
├── Directory.Build.props
├── docs/
│   ├── canonical/
│   │   ├── README.md
│   │   ├── binding-generator-constitution.md
│   │   ├── binding-generator-roadmap.md
│   │   ├── testing-strategy.md
│   │   ├── binding-generator-implementation-notes.md
│   │   ├── binding-generator-maintenance.md
│   │   ├── binding-output-oracle-validation.md
│   │   ├── satellites/        # 7 per-family + cross-family analyses
│   │   └── references/        # ppy-reference-analysis-2026-05-25.md
│   ├── next-iteration-plan.md
│   └── reference-clones.md
├── clangsharp/                 # ACTIVE prototype
├── alimer-style/               # RETAINED reference
├── output/reports/             # evidence artifacts (oracle, comparison, failure buckets)
└── references/                 # GITIGNORED — local clones (ppy-SDL3-CS, alimer-bindings-sdl)
```

- [ ] **Step 5: Refresh Quick commands section**

Update the oracle.cs command to use 5-family form (already in current README L136). Keep other commands.

- [ ] **Step 6: Refresh Working rules section**

Update spike-related path references; cut sunset Cake-impl callbacks.

- [ ] **Step 7: Refresh "How a new agent should pick this up" section**

Replace references to retired docs (llm-handoff, priority-c-closure-summary) with canonical/README pointer. Reorder reading sequence:

```markdown
1. `docs/canonical/README.md` — canonical workstream index and reading order.
2. `docs/canonical/binding-generator-constitution.md` — ABI/API policy.
3. `docs/canonical/binding-generator-roadmap.md` — layer-based forward milestones.
4. `docs/canonical/binding-generator-implementation-notes.md` — mechanism rationale.
5. `docs/canonical/binding-generator-maintenance.md` — operational procedures.
6. `docs/next-iteration-plan.md` — active iteration + forward backlog.
7. `output/reports/iteration-2-comparison.md` + `oracle-evidence-clangsharp.md` — evidence.
8. `references/ppy-SDL3-CS/SDL3-CS/generate_bindings.py` (gitignored clone) — north-star orchestrator.
```

- [ ] **Step 8: Remove stale `.github/prompts/binding-generator-spike-handoff.prompt.md` reference**

That reference (L41) is stale even before this consolidation. Remove from any new reading-order list.

- [ ] **Step 9: Run markdownlint**

- [ ] **Step 10: WIP commit spike README refresh**

```pwsh
git add spikes/binding-generators/README.md
git commit -m "docs(spike): refresh README for canonical/ tree + post-spike state

Major refresh per spec Phase 7.1:
- Status section: SDL2 Layer 1 closure across 5 families (was: Item 1 + Iteration 2 closure)
- Read first table: rewritten for canonical/ tree (cut retired llm-handoff /
  priority-c-closure-summary / generator-spike-goals / oracle-evidence-design /
  oracle-evidence-implementation-plan rows; added implementation-notes,
  maintenance, oracle-validation, satellites/, references/)
- Layout section: reflects canonical/ subtree + removed docs
- How a new agent picks this up: updated reading order pointing at canonical/README
- Decision recorded 2026-05-21 section: kept as historical anchor with path refresh

Refs spec Phase 7.1 + Phase 0 audit Task 0.10.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

### Task 7.2: Audit + update `spikes/binding-generators/clangsharp/README.md`

**Files modified:**

- `spikes/binding-generators/clangsharp/README.md`

- [ ] **Step 1: Re-read the audit table entry for clangsharp/README.md**

Per Phase 0 Task 0.10 Step 2, the audit identified path-refresh scope for this file.

- [ ] **Step 2: Apply path updates**

Replace `docs/binding-autogen/` references with `docs/canonical/` (relative path adjusted for nesting depth).

- [ ] **Step 3: Apply Rule 3 — section-name cross-references**

grep for `Constitution L\d+` patterns; replace with section-name refs.

- [ ] **Step 4: Remove references to retired docs**

If any references to `llm-handoff`, `priority-c-closure-summary`, `generator-spike-goals`, `oracle-evidence-design`, `oracle-evidence-implementation-plan` exist, remove or replace with canonical/ refs.

- [ ] **Step 5: WIP commit clangsharp/README.md refresh**

```pwsh
git add spikes/binding-generators/clangsharp/README.md
git commit -m "docs(clangsharp): path + section-name cross-reference refresh

Per spec Phase 7.2 + Rule 3:
- docs/binding-autogen/ refs -> docs/canonical/ (with relative-path adjustments)
- Line-number cross-refs -> section-name refs
- References to retired docs (llm-handoff, priority-c-closure-summary, etc.)
  removed or replaced with canonical/ pointers

Refs spec Phase 7.2 + audit Task 0.10.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

---

## Phase 8 — Inline Comment Spot-Check

**Goal:** Apply Rule 3 (section-name cross-references) to inline code comments in `spikes/binding-generators/clangsharp/postprocess/*.cs`. Sunset Cake-impl comments in `build/_build/Targets/GenerateBindings/*.cs` left for retirement-with-the-code.

### Task 8.1: grep + update postprocess inline comments

**Files modified:**

- `spikes/binding-generators/clangsharp/postprocess/*.cs` (grep first to identify candidates)

- [ ] **Step 1: grep for binding-autogen path refs in postprocess source**

```pwsh
Select-String -Path 'spikes/binding-generators/clangsharp/postprocess/*.cs' -Pattern 'docs/binding-autogen|Constitution L\d+|priority-c-closure-summary|llm-handoff|generator-spike-goals'
```

- [ ] **Step 2: Update each match per Rule 3**

For each grep hit:

- Replace `docs/binding-autogen/...` paths with `spikes/binding-generators/docs/canonical/...` paths (or remove the path ref if the comment can stand without it, per AGENTS.md §"Code comments must be self-contained").
- Replace `Constitution L\d+` with section-name refs.
- Remove references to retired docs.

- [ ] **Step 3: Verify no stale refs remain**

Re-run the grep from Step 1. Expected: no matches.

- [ ] **Step 4: WIP commit inline comment refresh**

```pwsh
git add spikes/binding-generators/clangsharp/postprocess/
git commit -m "docs(postprocess): inline comment cross-reference refresh per Rule 3

Apply spec Phase 8 + Rule 3:
- docs/binding-autogen/ path refs -> docs/canonical/
- Constitution line-number refs -> section-name refs
- References to retired docs (llm-handoff, priority-c-closure-summary, etc.) removed

AGENTS.md L152 'Code comments must be self-contained' discipline preserved -
some path refs removed entirely where the comment stood without them.

Sunset Cake-impl comments in build/_build/Targets/GenerateBindings/*.cs left
intentionally untouched; they retire with the code at Production Flip.

Refs spec Phase 8 + Rule 3.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

---

## Phase 9 — Validate + Final Commit

**Goal:** Final-state validation (multi-agent review checkpoint 6), broken-link scan, Slopwatch, commit summary.

### Task 9.1: Multi-Agent Review Checkpoint 6 (Final State Validation)

**Per spec §"Checkpoint 6".**

- [ ] **Step 1: Dispatch Explore agent — broken-link + stale-path scan**

Use Agent tool with `subagent_type: "Explore"`:

```text
Prompt: Scan all touched docs for broken Markdown links + stale path references + remaining line-number cross-refs.

Files to scan (all):
- spikes/binding-generators/docs/canonical/*.md
- spikes/binding-generators/docs/canonical/satellites/*.md
- spikes/binding-generators/docs/canonical/references/*.md
- spikes/binding-generators/docs/next-iteration-plan.md
- spikes/binding-generators/docs/reference-clones.md
- spikes/binding-generators/README.md
- spikes/binding-generators/clangsharp/README.md
- AGENTS.md
- docs/plan.md
- docs/release-strategy.md
- docs/decisions/2026-05-14-binding-autogen-toolchain.md
- docs/phases/phase-4-binding-autogen.md

Check for:
- Markdown links to files that no longer exist (retired in Phase 6).
- Path references using docs/binding-autogen/ (should all be canonical/ now).
- Cross-refs using Constitution L\d+ pattern (should be section names per Rule 3).
- References to retired spike top-level docs (llm-handoff, priority-c-closure-summary, generator-spike-goals, oracle-evidence-design, oracle-evidence-implementation-plan, satellite-expansion-roadmap).
- References to retired parking-lot files.
- References to retired playbook (sunset binding-generator-maintenance).
- References to retired prompt files.

Report all findings with file + line. Under 600 words.

Search breadth: very thorough.
```

- [ ] **Step 2: Dispatch general-purpose agent — canonical/ reading-order coherence**

Use Agent tool with `subagent_type: "general-purpose"`:

```text
Prompt: Read the final canonical/ tree top-to-bottom and report any contradiction or unclear reading flow.

Reading order:
1. spikes/binding-generators/docs/canonical/README.md
2. spikes/binding-generators/docs/canonical/binding-generator-constitution.md
3. spikes/binding-generators/docs/canonical/binding-generator-roadmap.md
4. spikes/binding-generators/docs/canonical/testing-strategy.md
5. spikes/binding-generators/docs/canonical/binding-generator-implementation-notes.md
6. spikes/binding-generators/docs/canonical/binding-generator-maintenance.md
7. spikes/binding-generators/docs/canonical/binding-output-oracle-validation.md
8. (Skim) spikes/binding-generators/docs/canonical/satellites/sdl2-image-header-analysis.md as representative satellite analysis.

Verify:
- Reading order is coherent (each doc builds on prior; no unexplained references).
- Policy in constitution + mechanism in implementation-notes split is clean (no policy in impl-notes, no mechanism in constitution).
- Maintenance playbook is actionable (a fresh maintenance task can be executed from it).
- Cross-references between docs use section names (not line numbers).
- No contradictions between policy in constitution + mechanism in implementation-notes + procedures in maintenance.

Report findings under 600 words.
```

- [ ] **Step 3: Consolidate findings**

- [ ] **Step 4: Apply corrections (if any)**

For each finding, create a targeted fix commit.

- [ ] **Step 5: Exit gate**

Confirm: zero broken links; cross-references consistent; reading order coherent. Checkpoint 6 passes.

### Task 9.2: Run validation commands

- [ ] **Step 1: Verify whitespace cleanliness**

```pwsh
git diff --check
```

Expected: no output (no whitespace errors).

- [ ] **Step 2: Run Slopwatch (per AGENTS.md instructions)**

```pwsh
slopwatch analyze --fail-on warning --exclude "artifacts/**,external/**,vcpkg_installed/**,spikes/binding-generators/references/**,**/bin/**,**/obj/**"
```

Expected: `Scan complete: 0 issue(s) found`. If new files added that weren't in the baseline, may need `slopwatch init -f --exclude "..."` to refresh baseline (per AGENTS.md L295).

- [ ] **Step 3: Verify all retirees are gone**

```pwsh
$retirees = @(
  "docs/parking-lot/binding-autogen-cake-implementation",
  "docs/binding-autogen",
  "docs/playbook/binding-generator-maintenance.md",
  "spikes/binding-generators/docs/llm-handoff.md",
  "spikes/binding-generators/docs/priority-c-closure-summary.md",
  "spikes/binding-generators/docs/generator-spike-goals.md",
  "spikes/binding-generators/docs/oracle-evidence-design.md",
  "spikes/binding-generators/docs/oracle-evidence-implementation-plan.md",
  "spikes/binding-generators/docs/satellite-expansion-roadmap.md",
  "spikes/binding-generators/docs/items",
  "spikes/binding-generators/docs/testing",
  "spikes/binding-generators/docs/satellites",
  ".github/prompts/binding-generator-spike-priority-c-handoff.prompt.md",
  ".github/prompts/satellite-expansion-research-cross-check.prompt.md",
  ".github/prompts/item-4-closure-item-5-mixer-handoff.prompt.md",
  "spikes/binding-generators/output/reports/oracle-comparison-clangsharp.md",
  "spikes/binding-generators/output/reports/oracle-comparison-alimer.md"
)
$retirees | ForEach-Object { if (Test-Path $_) { Write-Host "STILL EXISTS: $_" } }
```

Expected: no output (all retirees gone).

- [ ] **Step 4: Verify canonical/ tree complete**

```pwsh
$canonical = @(
  "spikes/binding-generators/docs/canonical/README.md",
  "spikes/binding-generators/docs/canonical/binding-generator-constitution.md",
  "spikes/binding-generators/docs/canonical/binding-generator-roadmap.md",
  "spikes/binding-generators/docs/canonical/testing-strategy.md",
  "spikes/binding-generators/docs/canonical/binding-generator-implementation-notes.md",
  "spikes/binding-generators/docs/canonical/binding-generator-maintenance.md",
  "spikes/binding-generators/docs/canonical/binding-output-oracle-validation.md",
  "spikes/binding-generators/docs/canonical/references/ppy-reference-analysis-2026-05-25.md",
  "spikes/binding-generators/docs/canonical/satellites/sdl2-image-header-analysis.md",
  "spikes/binding-generators/docs/canonical/satellites/sdl2-mixer-header-analysis.md",
  "spikes/binding-generators/docs/canonical/satellites/sdl2-ttf-header-analysis.md",
  "spikes/binding-generators/docs/canonical/satellites/sdl2-gfx-header-analysis.md",
  "spikes/binding-generators/docs/canonical/satellites/sdl2-satellite-error-function-consolidation.md",
  "spikes/binding-generators/docs/canonical/satellites/sdl2-endian-platform-macro-consolidation.md",
  "spikes/binding-generators/docs/canonical/satellites/sdl2-function-like-macro-consolidation.md"
)
$canonical | ForEach-Object { if (-not (Test-Path $_)) { Write-Host "MISSING: $_" } }
```

Expected: no output (all canonical files present).

### Task 9.3: Present commit summary + await Deniz approval

Per AGENTS.md §"Approval Gate" — explicit approval required before push.

- [ ] **Step 1: Generate commit summary**

```pwsh
git log --oneline origin/spike/binding-autogen-sdl2-gfx..HEAD
```

Expected: list of all WIP commits from this consolidation (plan/Phase commits + audit + Phase 1-8 commits).

- [ ] **Step 2: Generate change summary**

```pwsh
git diff --stat origin/spike/binding-autogen-sdl2-gfx..HEAD -- '*.md'
```

Expected: ~51 file changes summary.

- [ ] **Step 3: Present summary to user with options**

Two finish options per memory ("single changeset via branch+squash"):

**Option A (recommended):** Squash all WIP commits into a single landing commit on the branch via `git merge --squash` from a working branch into the canonical work branch, then push as one commit.

**Option B:** Keep WIP commits as-is and push the series.

Present both options + recommended commit message for Option A. Wait for Deniz approval before any `git push`.

- [ ] **Step 4: Apply Deniz's choice**

If Option A: execute `git merge --squash` flow per AGENTS.md discipline + the chosen commit message.
If Option B: push the series as-is.

- [ ] **Step 5: Final commit (if Option A) or push (Option B)**

Per Deniz approval. Then `git status` clean.

---

## Self-Review (Plan Author Check Before Handoff)

Run this checklist before declaring the plan complete:

**1. Spec coverage check:** Walk through the [design spec](../specs/2026-05-28-binding-autogen-doc-consolidation-design.md) §"Settled Decisions" Q1–Q15 + Amendment. For each decision, can you point to a task in this plan that implements it?

- Q1 (topology) → Phase 1 (moves into spike canonical/)
- Q2 (canonical/ subfolder) → Phase 1 + Phase 2.4 README + Phase 7.1 spike README refresh
- Q3 (constitution purification) → Task 2.1
- Q4 (roadmap restructure) → Task 2.2
- Q5/Q9 (testing-strategy purification + testing/ fold) → Task 2.3 + Task 6.6
- Q6 (spike top-level disposition) → Task 6.4 + Task 4.2 (satellite-expansion-roadmap fold)
- Q7 (items/ disposition) → Task 6.5
- Q8 (satellites/ move + rename) → Task 1.4 + Task 2.6
- Q10 (parking-lot audit-and-retire) → Phase 0 audit + Task 6.1 + Checkpoint 3
- Q11 (playbooks) → Task 6.3 retire + Task 3.2 new playbook + Task 2.5 oracle-validation
- Q12 (phase-4 purify) → Task 5.5
- Q13 (ADR-004) → Task 5.4
- Q14 (cross-cutting refs) → Task 5.1 / 5.2 / 5.3 / 5.4 / 5.5
- Q15 (spike-level structural) → Task 7.1 + Task 6.7 (prompts) + Task 6.8 (legacy reports)
- Amendment (multi-agent reviews) → Checkpoints 1-6 in Tasks 0.12 / 3.3 / 3.4 / 4.4 / 6.2 / 9.1

**2. Placeholder scan:** Search this plan for red flags (TBD / TODO / "implement later" / "add appropriate error handling" / "similar to Task N"). Replace any found with concrete content.

**3. Type consistency:** Verify file paths and section names are consistent across tasks (e.g., `binding-generator-implementation-notes.md` spelled the same in Phase 3, Phase 7, Phase 8).

**4. Audit gate consistency:** Every retirement task references the audit table entry produced in Phase 0. Confirm.

**5. Multi-agent review prompts:** Each checkpoint's Agent dispatch prompts are self-contained (no hidden context assumptions). Confirm.

---

## References

- [Design spec](../specs/2026-05-28-binding-autogen-doc-consolidation-design.md) — full decision rationale and topology design.
- [ADR-002 Target-centric build-host](../../decisions/2026-05-05-target-centric-build-host.md).
- [ADR-003 Contract-centric data layer](../../decisions/2026-05-12-build-host-data-layer.md).
- [ADR-004 Binding-autogen toolchain](../../decisions/2026-05-14-binding-autogen-toolchain.md) — Reopened pending Layer 2 closure.
- [Knowledge-base extraction guidelines](../../knowledge-base/extraction-guidelines.md).
- [Knowledge-base testing guidelines](../../knowledge-base/testing-guidelines.md).
- [AGENTS.md](../../../AGENTS.md) — operating rules and approval gates.
