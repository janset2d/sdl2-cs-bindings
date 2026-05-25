---
name: "Item 1 Per-Library Generation — Spec + Plan Handoff"
description: "Priming prompt for the next agent entering janset2d/sdl2-cs-bindings to execute Item 1 (Per-Library Generation Infrastructure) after the spec + execution plan were authored and reviewed-by-three-agents at branch HEAD 07e5a4b on 2026-05-25. Spec is durable (473 lines, 7 design decisions §5.1–§5.7); execution plan is TDD-disciplined (4218 lines, 8 slices S1-0 through S1-7, fully task-decomposed with exact code blocks + expected outputs + per-slice determinism contract verification); Constitution carries 5 uncommitted policy edits in the working tree. Branch is on the same HEAD as Priority C closure (the Item 1 doc tour produced only working-tree edits, no commits). Recommended next: execute S1-0 (commit the doc artifacts) then S1-1 through S1-7 as approval-gated slices."
argument-hint: "Optional focus area: 'execute-s1-0' | 'execute-full-item-1' | 'review-plan-again' | 'switch-to-iteration-2' | custom"
agent: "agent"
model: "Claude Opus 4.7 (1M context)"
---

You are an engineer entering `janset2d/sdl2-cs-bindings` to continue work on the **ClangSharp + Roslyn postprocess binding-generator spike** at `spikes/binding-generators/clangsharp/`. The previous session authored, reviewed (by 3 parallel agents), and revised the **Item 1 Per-Library Generation Infrastructure** spec + execution plan that enables future satellite expansion (Items 2-5: SDL_image bug fix, SDL2_gfx, SDL2_ttf, SDL2_mixer). The work is **doc-only** at this point — no code has been touched. Branch `spike/binding-autogen-sdl2-gfx` HEAD is still `07e5a4b` (same as Priority C closure); the working tree carries 5 Constitution edits (`M`) + a new Iteration 2 roadmap section (`??`) + the durable Item 1 spec + the TDD-disciplined execution plan (`??`). Push gate from Priority C closure (37 commits ahead of origin) still pends; Item 1 has not begun execution.

## First Principle

> Treat every claim here as **current-as-of-authoring (2026-05-25 — Item 1 spec+plan landed)** and verify against the live repo, `git log`, and canonical docs before acting. The Item 1 spec at `spikes/binding-generators/docs/items/item-1-per-library-generation-spec.md` is the authoritative durable design record; the plan at `spikes/binding-generators/docs/items/item-1-per-library-generation-plan.md` is the per-slice execution record (deletes after S1-7 ships per refactoring-doc lifecycle). This prompt cites both but does not duplicate their content.

## What Just Happened

### Iteration scope

The session produced one deliverable: a fully-specified Item 1 with an executable plan. No code was touched. The branch HEAD did not advance from Priority C closure (`07e5a4b`). All artifacts live in the working tree awaiting commit.

### Spec authored — `spikes/binding-generators/docs/items/item-1-per-library-generation-spec.md`

**473 lines.** Durable design decisions for Item 1. Eleven sections; the seven Decision sections (§5.1–§5.7) are the binding contract:

| §   | Decision | Summary |
|-----|---|---|
| 5.1 | Dormant `all` scope | `selected_families("all")` stays `["core", "image"]` after Item 1; new families reachable per-family via CLI but not in `all`. Items 3/4/5 each add one entry on their slice. |
| 5.2 | Family-blind opaque auto-detection + family-keyed roster | Drop `StartsWith("SDL_")` gate in `DiscoverAutoDetectedHandles`; migrate `policy/opaque-handle-roster.json` to family-keyed schema 2.0 (top-level `families` object); `LoadRoster(rosterPath, family)` cross-family-pulls Core's auto-detect + force-opaque sets for satellite consumers; `BuildHandlesFileContent` takes `namespaceName` parameter. |
| 5.3 | ClongDualDispatchRewriter Roslyn refactor | Rename `threadid-dispatch` → `clong-dispatch`; switch mutation from text-substitution to true Roslyn `SyntaxFactory` via `VisitClassDeclaration`; expand `AffectedMethodNames` with 5 TTF C `long` symbols (dormant); add parameter-position rewrite. |
| 5.4 | `[Flags]` detection — alimer-style | New 7th postprocess step `flags-detect`; name-suffix (`EndsWith("Flags")`) + family-keyed allow-list at `policy/flags-enum-roster.json`. Power-of-two value heuristic explicitly **rejected** (Constitution friction, `SDL_bool` false-positive, no peer validation — only alimer auto-detects with same approach). |
| 5.5 | Owner-mode resolution | `family in ("core", "ttf", "mixer")` → owner; image/gfx → consumer. TTF/Mixer dormant until activated. |
| 5.6 | Oracle multi-family extension | Data-only: 3 new `FamilyConfig` records (Sdl2Ttf, Sdl2Mixer, Sdl2Gfx) + `KnownFamilies` array extension. |
| 5.7 | Per-family scope/RSP/csproj defer | Item 1 does NOT create these files; Items 3/4/5 own creation. |

§6 success criteria (10 criteria, #1 has explicit `[Flags]` additions carve-out per §5.4); §7 cross-cutting standardization; §10 references (Constitution + AGENTS.md + roadmap cross-refs).

**Spec is shared on a secret gist for Deniz review**: https://gist.github.com/Blind-Striker/db01d172abe623c42f611e4744e249f1

### Constitution edits authored (uncommitted in working tree)

**5 sections changed in `docs/binding-autogen/binding-generator-constitution.md`**:

1. **§"Opaque Handles" Auto-detect criterion** — generalize prose from `SDL_X` to `X` (family-blind structural test); explicit family-blind statement covering `TTF_Font` / `Mix_Music`.
2. **§"Opaque Handles" Canonical roster** — migrate prose for family-keyed schema 2.0; drift watchdog runs per-owner-family; consumer directories short-circuit.
3. **§"Opaque Handles" Cross-family handle name resolution** (NEW subsection) — codify `LoadRoster` cross-family pull contract: satellites see Core's `auto_detect_well_known` ∪ `force_opaque_exceptions` for Pattern B by-value uniformity. Data-only pull; no cross-directory `.g.cs` read at execution.
4. **§"Enums" auto-decoration policy** — REWRITE alimer-style; explicit rejection of bit-value heuristics; SDL_bool false-positive case codified; Stage 1 Core flag enum list (5).
5. **§"Current SDL2.Core ABI Status" point 7** — fix designation-vs-emission drift (Stage 1 bitmask enums designated but emission delivered by Item 1's flags-detect step).
6. **NEW §"Generation Determinism Contract"** — top-level section binding all determinism invariants: pin set (8 inputs), family isolation (3 properties), dependency direction (gen→postprocess forward; build-time Core←Image ProjectReference), native header resolution scope, pure-inputs discipline (no machine-paths / timestamps / network), per-slice verification contract (idempotency, family isolation, per-family equivalence, cross-family handle pull).

Constitution grew from 542 → 623 lines (+81). **All 5 edits sit uncommitted (`M` status)**; S1-0 is the first execution slice and lands them.

### Roadmap edits authored (untracked in working tree)

**3 changes in `spikes/binding-generators/docs/satellite-expansion-roadmap.md`** (the roadmap itself was created in this branch and is `??` untracked):

- **E1**: §Cross-Cutting → `sdl2-core-sdlh-required.json` keep note ("load-bearing for generate_bindings.py:554"); stale comparison-report template conditional removal.
- **E2**: §Item 1 → `[Flags]` direction — replace power-of-two heuristic with alimer-style name-suffix + family-keyed roster allow-list.
- **E3**: §Cross-Cutting → `policy/` paragraph — opaque-handle-roster family-keyed (not Core-only); new flags-enum-roster.json with same disipline.

**Iteration 2 placeholder added** (new section between Item 1 and Item 2): **Config Surface Unification**. Problem statement + 18-source inventory (9 external + 9 code-internal) + binding constraints (manifest.json untouched; Constitution determinism preserved; ships before Item 2). **No solution proposed.** Detail spec + plan iteration deferred to its own approval gate after Item 1 ships.

Roadmap §Implementation Order & Dependencies graph updated to show Iteration 1 (Item 1) → Iteration 2 (config) → Items 2-5.

### Execution plan authored — `spikes/binding-generators/docs/items/item-1-per-library-generation-plan.md`

**4218 lines.** TDD-disciplined per-slice execution plan. 8 slices:

| Slice | Goal | Approx. tasks |
|---|---|---|
| S1-0 | Land doc artifacts — 3 commits (Constitution / roadmap / spec+plan) | 5 tasks (verification + 3 commits + sanity check) |
| S1-1 | Cross-cutting cleanup (retire the legacy Python oracle-comparison script; verify load-bearing files) | 7 tasks |
| S1-2 | Orchestrator family-agnostic refactor (FAMILY_CONFIG entries; selected-driven stats/write_report; --family CLI; owner-mode wiring) | 16 tasks |
| S1-3 | Oracle multi-family extension (data-only `FamilyConfig` additions) | 10 tasks |
| S1-4 | OpaqueHandleEmitRewriter family-blind + roster family-keyed migration + cross-family handle pull | 16 tasks |
| S1-5 | ClongDualDispatchRewriter Roslyn node-level mutation + rename + TTF symbols dormant + param-position | 18 tasks (Tasks 5.1-5.4 are pre-TDD rename prelude; Task 5.5 onward is strict TDD) |
| S1-6 | FlagsAttributeRewriter alimer-style + new `policy/flags-enum-roster.json` + Core regen `[Flags]` audit + Image `IMG_InitFlags` delivery (Item 2 #1 side-effect) | 12 tasks |
| S1-7 | Item 1 exit verification (no code change — full regen + multi-TFM + oracle + slopwatch + closure record) | 11 tasks |

Plan-level discipline (in the plan's "How to read this plan" preamble):

- **Bite-sized steps (2-5 min each)** — exact code blocks, exact commands, expected output. No placeholders.
- **TDD shape per code change**: failing test → run, verify fails → implement → run, verify passes → commit.
- **`Common: Determinism Contract Verification`** (defined once near the top) — D.1 idempotency (true two-run snapshot diff), D.2 Core-only isolation, D.3 Image-only isolation, D.4 per-family equivalence to `--family all`, D.5 cross-family handle pull preservation (Modern AND Compat trees; anti-check pointer-form greps). Referenced from every code-touching slice (S1-2 through S1-6).
- **`Common: Slopwatch`** (defined once) — `slopwatch analyze` per slice; baseline rebuild on slices that delete/rename source files (S1-1, S1-5).
- **Plan-doc reference discipline** — every slice cross-references the spec § and the Constitution § it must honor; code anchors are file paths + line numbers (verified at authoring time).

### Three-agent parallel review (post-revision validation)

After the plan was revised to TDD discipline, **3 paralel general-purpose agents** were dispatched with identical brief: onboard (Constitution + AGENTS.md + roadmap + spec + plan + 4 satellite header analyses), review, report ambiguities. They returned **9 distinct CRITICAL findings** (2 triple-converged, 7 unique-but-real) and **12 MAJOR findings** (some converged across 2+ agents). **All 21 findings were applied to the plan in a final fix pass** before this handoff. Key categories:

| Headline fix | Convergence |
|---|---|
| PostProcessSelfTests.cs `Microsoft.CodeAnalysis.*` usings pre-loaded in Task 4.4.2 | A+B+C triple |
| Common D.1 idempotency rewritten as true two-run snapshot diff (not HEAD diff) | A+B converged |
| Task 2.13 `read_scope` friendly error converted from manual probe to TDD red→green | A+B+C triple |
| `.sln` → `.slnx` filename fix (Tasks 1.5.1 + 7.4.1) | B unique |
| **NEW S1-0 slice** inserted to commit the 5 Constitution edits before any code slice (audit trail) | C unique |
| BuildModernMembers `partial` modifier duplication guard (post-libraryimport already adds partial) | A unique |
| S1-4 Tasks 4.5.3 → 4.10.2 intermediate-state "do NOT regen between" warning | A unique |
| S1-4 Tasks 4.2 → 4.5 JSON-vs-LoadRoster crash window warning | C unique |
| Constitution stale line references (L246-283, L264, L443-451) replaced with §-name only | B+C converged |
| ParseAttributeArgumentList dry-run validation step added to S1-5 | A+B converged |
| `uniformOpaqueHandlesNamespace` variable declaration explicit (declare at L85 before switch, NOT inside case) | B unique |
| Image family empty allow_list + suffix wiring test (Test 15) | C unique |
| Slopwatch `dotnet tool install -g slopwatch` prerequisite added to Common: Slopwatch | A unique |
| S1-5 vs S1-6 flags-detect mode-key atomic pair (allowlist + case land together in S1-6, not split) | A unique |
| D.5 cross-family pull check extended to BOTH Compat and Modern + anti-check pointer-form greps | C unique |

The plan grew from 3388 → 4218 lines (+830, +24%) in the fix pass.

## Onboarding Snapshot

### Architecture map (mostly unchanged from Priority C handoff; Item 1 ADDS roster/policy + 7th postprocess step)

```
spikes/binding-generators/clangsharp/
├── generate_bindings.py             # Python orchestrator
├── oracle.cs                        # Roslyn raw ABI evidence reporter
├── rsp/                             # Three-tier RSP (base + family + per-header)
├── policy/
│   ├── opaque-handle-roster.json    # Pattern B handle policy — schema 1.0 today; S1-4 migrates to schema 2.0 family-keyed
│   └── flags-enum-roster.json       # NEW — created in S1-6 (family-keyed allow-list)
├── postprocess/
│   ├── Program.cs                   # 6-mode dispatch today; S1-6 adds 7th (flags-detect)
│   ├── OpaqueHandleEmitRewriter.cs  # S1-4 drops SDL_ prefix gate + family-keyed LoadRoster + BuildHandlesFileContent namespace param
│   ├── ThreadIdDualDispatchRewriter.cs  # S1-5 renames to ClongDualDispatchRewriter.cs + Roslyn refactor
│   ├── FlagsAttributeRewriter.cs    # NEW — created in S1-6
│   ├── FlagsEnumRosterLoader.cs     # NEW — created in S1-6
│   ├── PostProcessSelfTests.cs      # NEW — created in S1-4 Task 4.4.2 (TDD harness)
│   └── ... (DllImportToLibraryImport, GuidSubstitution, PlatformDelta, StripVarargs, UniformOpaqueOwnerMode unchanged)
├── shims/platform-headers/          # Untouched
├── tests/abi-tests/                 # Used in S1-5 verification (AbiTests revalidation post-Roslyn-refactor)
└── src/
    ├── Janset.SDL2.Core/            # 5 TFMs; Generated/ Compat+Modern unchanged in scope, but S1-6 adds [Flags] to 5 enums
    └── Janset.SDL2.Image/           # 5 TFMs; consumer mode; S1-6 adds [Flags] to IMG_InitFlags
```

### Constitution policy index (post-Item 1 edits — currently in working tree, lands in S1-0)

- §"Generation Determinism Contract" — **NEW top-level section.** Pin set inputs, family isolation, dependency direction, native header resolution scope, pure-inputs discipline, verification contract.
- §"Opaque Handles" — extended for family-blind auto-detect, family-keyed roster schema 2.0, cross-family handle name resolution.
- §"Enums" — REWRITTEN auto-decoration policy (alimer-style; power-of-two heuristic rejected).
- §"Current SDL2.Core ABI Status" point 7 — designation-vs-emission drift fixed.
- (All other sections unchanged from Priority C closure state.)

### Standing invariants from Priority C (unchanged — see priority-c handoff for the full list)

- Same-named tag+typedef SDL2 handles require Roslyn postprocess (Pattern B emit).
- Pattern B operators are explicit-only; deliberate escape via `DangerousGetHandle()`.
- Cross-OS container mount forbidden (COPY-into-image only).
- RSP literal-space form (not shell-quoted) for `wchar_t *=nint` etc.
- Spike output is committed; regeneration must be idempotent (true two-run diff in Item 1's D.1).
- No commit without explicit "go / yap / apply / proceed / başla" approval.
- Plan-doc reference discipline: cross-reference ADRs + Constitution sections.
- `git mv` FIRST during file renames (preserves history).

### Item 1-specific invariants (new — internalize before executing)

- **Cross-family handle name resolution is a binding contract.** Satellite `LoadRoster(rosterPath, family)` MUST pull both Core's `auto_detect_well_known` AND `force_opaque_exceptions` when `family != "core"`. Without this, Image's regen emits `SDL_Renderer*` (pointer) instead of `SDL_Renderer` (by-value) — D.5 verification catches this.
- **`SDL_bool` MUST NOT gain `[Flags]`.** S1-6 Test 10 is the headline assertion validating Option B (alimer-style) rejection of value-pattern heuristics. The plan's `FlagsAttributeRewriter` is value-pattern-blind — only name-suffix `EndsWith("Flags")` + roster allow-list.
- **S1-0 must commit Constitution + roadmap + spec+plan BEFORE any code slice.** The audit trail requires Constitution edits to be tied to a real commit so subsequent slices' Constitution references resolve to a committed file.
- **S1-4 Tasks 4.2 → 4.10 are non-divisible.** Do NOT run `generate_bindings.py --execute` between these tasks; intermediate state would either crash (JSON schema vs LoadRoster mismatch) or silently break cross-family pull. The Common D-block verification runs only at Task 4.14, after all rewriter changes land.
- **S1-5 Tasks 5.1-5.4 are pre-TDD rename prelude.** TDD red→green discipline starts at Task 5.5 (failing test for parameter-position rewrite). Pre-TDD prelude is documented in the slice header.
- **S1-6 success criterion #1 carve-out is intentional.** Core regen will gain `[Flags]` on 5 enums (SDL_RendererFlags via suffix; SDL_Keymod/BlendMode/GLcontextFlag/RendererFlip/TextureModulate via allow-list); Image gains `[Flags]` on IMG_InitFlags via suffix. The committed regen output **changes** in S1-6; this is Constitution alignment, not regression.

## Current State You Should Assume Until Verified

- **Branch HEAD**: `07e5a4b` — `test(clangsharp): add SDL2 ABI-critical coverage`. Same as Priority C closure; Item 1 doc tour produced ZERO commits. 37 commits ahead of `origin/spike/binding-autogen-sdl2-gfx` from Priority C closure (Item 1 has not added to that count yet).
- **Worktree expectation**: 1 modified + 2 untracked:
  ```
   M docs/binding-autogen/binding-generator-constitution.md
  ?? spikes/binding-generators/docs/items/
  ?? spikes/binding-generators/docs/satellite-expansion-roadmap.md
  ```
  Constitution diff: ~80+ inserts / ~10-15 deletes (5 edits). `items/` is two new files (spec ~473 lines, plan ~4218 lines). Roadmap is a new file (~393 lines).
- **Oracle baseline**: unchanged from Priority C closure (0 findings across all Priority C categories). Item 1 has not regenerated.
- **AbiTests**: unchanged from Priority C closure (Windows x64 4/4 + Linux x64 net10 docker 1/1 pass).
- **Push gate**: still pending from Priority C closure. Item 1 does NOT bypass — `başla` approval required per AGENTS.md.
- **Spec gist (Item 1 spec only)**: https://gist.github.com/Blind-Striker/db01d172abe623c42f611e4744e249f1 — secret gist, plan NOT on gist (4200+ lines, local-only).

Verify with:

```bash
git log --oneline -1
git status --short
wc -l docs/binding-autogen/binding-generator-constitution.md \
      spikes/binding-generators/docs/items/item-1-per-library-generation-spec.md \
      spikes/binding-generators/docs/items/item-1-per-library-generation-plan.md \
      spikes/binding-generators/docs/satellite-expansion-roadmap.md
# Expected approximate: Constitution 623, spec 473, plan 4218, roadmap 393.

# Confirm no Item 1 commits exist yet (HEAD same as Priority C)
git log --oneline 07e5a4b..HEAD
# Expected: empty (HEAD is 07e5a4b itself).
```

## Recommended Next Step

Three options. **Talk to Deniz before committing to which one.** Default and recommended path is option 1; options 2 and 3 are exits.

### 1. Execute Item 1 — start with S1-0 (RECOMMENDED — multi-session arc)

**What**: Begin Item 1 execution. The plan is fully task-decomposed with exact code blocks; a careful engineer can ship S1-0 in one short session and proceed slice-by-slice with Deniz approval gates between commits.

**Pre-flight**:
- Read AGENTS.md §"Approval Gate" — every code-touching slice waits for explicit `başla` / `yap` / `proceed` / `go` / `apply` before commit.
- Read the spec end-to-end (`spikes/binding-generators/docs/items/item-1-per-library-generation-spec.md`, 473 lines). The 7 Decision sections §5.1-§5.7 are the binding contract. Spec is durable; do NOT modify during execution unless Deniz explicitly reopens.
- Read the plan slice-by-slice (`item-1-per-library-generation-plan.md`). Start with the "How to read this plan" preamble, then "Per-slice common requirements" (Common: Determinism Contract Verification + Common: Slopwatch — referenced from every code-touching slice), then S1-0.
- Verify slopwatch is installed: `slopwatch --version`. If absent: `dotnet tool install -g slopwatch`.

**S1-0 specifically** (single-session, ~30-45 min including approval rounds):
- 3 commits in order: Constitution edits (S1-0a) → roadmap (S1-0b) → spec+plan (S1-0c).
- Each commit's message body enumerates the changes per the plan's Step 0.2.2 / Step 0.3.2 / Step 0.4.2 heredocs.
- Exit evidence: working tree clean for `docs/` + `spikes/binding-generators/docs/`; `git log --oneline -5` shows the 3 S1-0 commits.

**After S1-0**: pause for Deniz approval. The next slice S1-1 is a small file-removal slice (retire the legacy Python oracle-comparison script) — another tight loop. Subsequent slices (S1-2 through S1-6) are larger and may span multiple sessions each.

**Acceptance criteria for "Item 1 closed"** (S1-7 exit gate, defined in the plan):
- `--family all` byte-identical to HEAD EXCEPT audited `[Flags]` additions (carve-out enumerated in plan §6 #1).
- Per-family equivalence + family isolation + cross-family handle pull all verified (Common D.1-D.5).
- Multi-TFM build clean (5 TFMs × 4 projects).
- Oracle 5-family report; 0 findings on Core+Image; TTF/Mixer/GFX expected `SourceStatus.Missing`.
- AbiTests PASS across Windows x64 TFMs.
- Slopwatch 0 issues.
- Doc closure record landed in `next-iteration-plan.md` + roadmap; plan doc deleted (spec stays).

### 2. Re-review the plan with fresh eyes (single session, optional — only if Deniz feels the plan is still ambiguous)

**What**: The 3-agent parallel review applied 21 fixes. If Deniz wants additional perspective before execution, re-dispatch a 4th agent — or read the plan personally and flag remaining concerns.

**Pre-flight**: Read this prompt's §"What Just Happened" → "Three-agent parallel review" table; the 21 fixes already applied are listed. Anything not on that list is either a true open issue or out of Item 1 scope.

**Touch points**: Plan or spec edits only; no code touch. If a real gap surfaces, apply it the same way the prior fix pass did.

### 3. Switch to Iteration 2 (Config Surface Unification) ahead of Item 1 execution

**What**: Roadmap places Iteration 2 between Item 1 and Item 2 (per `spikes/binding-generators/docs/satellite-expansion-roadmap.md` §Iteration 2). The placeholder has problem statement + 18-source inventory but **no solution**. Iteration 2's own spec + plan iteration would precede Item 1 execution if Deniz wants the config surface unified first.

**Pre-flight**: Read the Iteration 2 placeholder in the roadmap. The "What this iteration does NOT decide" section enumerates 6 deferred design questions. The "Binding constraints" section is firm: `build/manifest.json` is untouched in Iteration 2; Constitution determinism preserved; ships before Item 2.

**Cost**: Iteration 2's spec + plan iteration would be its own multi-session effort. Deniz approved sequencing as Item 1 → Iteration 2 → Items 2-5; deviating reopens that decision.

## Mandatory Grounding (read in this order)

1. `AGENTS.md` — canonical agent contract. §"Approval Gate" + bilingual TR/EN policy + skill catalog Tier 1.
2. `CLAUDE.md` — relay to AGENTS.md.
3. **`spikes/binding-generators/docs/items/item-1-per-library-generation-spec.md`** — Item 1 durable design (473 lines). The 7 Decision sections §5.1-§5.7 are binding. **Read end-to-end before any execution.**
4. **`spikes/binding-generators/docs/items/item-1-per-library-generation-plan.md`** — Item 1 execution plan (4218 lines). Read sequentially: header + "How to read this plan" + "Slice ordering rationale" + "Per-slice common requirements" + S1-0 first. Subsequent slices read on-demand when about to execute them.
5. `docs/binding-autogen/binding-generator-constitution.md` — canonical policy. **Note**: 5 Item 1 edits are in working tree (uncommitted). Read both the committed sections and the uncommitted diffs (`git diff docs/binding-autogen/binding-generator-constitution.md`). Critical sections for Item 1: §"Generation Determinism Contract" (NEW), §"Opaque Handles" + cross-family handle name resolution subsection, §"Enums" auto-decoration policy, §"C `long` And `unsigned long`" Priority C hybrid strategy.
6. `spikes/binding-generators/docs/satellite-expansion-roadmap.md` — parent roadmap. §Item 1 (scope this plan implements) + §Iteration 2 (Config Surface Unification placeholder).
7. `spikes/binding-generators/docs/priority-c-closure-summary.md` — prior wave closure; **read for prior-art context** on roster JSON, postprocess pipeline architecture, AbiTests harness.
8. `spikes/binding-generators/docs/ppy-reference-analysis-2026-05-25.md` — ppy/SDL3-CS satellite architecture reference (Item 1's §5.2 cross-family pull design is informed by ppy's `--include-directory` + `--file` semantics).
9. Per-family header analyses (only when executing the corresponding satellite Item; not needed for Item 1):
   - `spikes/binding-generators/docs/satellites/sdl2-image-cross-check.md`
   - `spikes/binding-generators/docs/satellites/sdl2-ttf-header-analysis.md`
   - `spikes/binding-generators/docs/satellites/sdl2-mixer-header-analysis.md`
   - `spikes/binding-generators/docs/satellites/sdl2-gfx-header-analysis.md`
10. `.github/prompts/binding-generator-spike-priority-c-handoff.prompt.md` — prior handoff with the standing architecture map + invariants Item 1 builds on.

Code to inspect (in order of importance for Item 1 execution):

1. `spikes/binding-generators/clangsharp/postprocess/OpaqueHandleEmitRewriter.cs` — S1-4 target. Read all 379 lines. Lines 186-247 (`DiscoverAutoDetectedHandles` — drops SDL_ prefix gate), 253-279 (`LoadRoster` — gets family parameter), 291-313 (`ReportDrift` — gets family parameter), 324-352 (`BuildHandlesFileContent` — gets namespace parameter).
2. `spikes/binding-generators/clangsharp/postprocess/ThreadIdDualDispatchRewriter.cs` — S1-5 target. Renamed to ClongDualDispatchRewriter; switched from text-substitution to Roslyn SyntaxFactory.
3. `spikes/binding-generators/clangsharp/postprocess/Program.cs` — extension target. S1-4 adds family resolution; S1-5 renames mode key; S1-6 adds 7th case.
4. `spikes/binding-generators/clangsharp/postprocess/UniformOpaqueOwnerMode.cs` — S1-4 adds `namespaceName` parameter.
5. `spikes/binding-generators/clangsharp/generate_bindings.py` — S1-2 target (family-agnostic refactor). Read whole file ~1300 lines; pay attention to FAMILY_CONFIG L327-344, PLATFORM_SENSITIVE_HEADERS L449-455, selected_families L776-779, stats init L1078-1081, owner-mode wiring L1267, and run_self_tests L858-1047 (where the new TDD assertions land in S1-2 and S1-4).
6. `spikes/binding-generators/clangsharp/oracle.cs` — S1-3 target (data-only FamilyConfigs extension). Lines 160-191.
7. `spikes/binding-generators/clangsharp/policy/opaque-handle-roster.json` — S1-4 migrates to family-keyed schema 2.0. The migration must byte-preserve the existing 14+3+12 Core entries.

## Locked Policy Recap

These are most-likely-to-be-tempting-to-violate during Item 1 execution:

- **No commit without explicit Deniz approval** (`başla` / `yap` / `proceed` / `go` / `apply`). AGENTS.md §"Approval Gate" hard rule. Holds per slice — S1-0 ships 3 commits, but each needs Deniz's go.
- **Plan is the contract, not a suggestion.** Bite-sized step format (failing test → run, fails → implement → run, passes → commit) is mandatory. Skipping the failing-test step violates the plan's preamble TDD policy. If a step is genuinely impossible to write a failing test for (rare — see Task 2.13 for an example of how to do it correctly), surface the divergence; do not silently skip.
- **Constitution authority binds**. Pattern B shape, cross-family handle pull semantic, family-keyed roster schema, alimer-style flags detection — these are Constitution policy. Item 1 implements them; do not propose alternatives mid-execution.
- **Spec §6 #1 carve-out is the ONLY allowed Core+Image output change.** S1-6 adds `[Flags]` to 6 enums (5 Core + 1 Image). Any other diff in Core or Image's Generated/ during Item 1 execution is a regression — investigate before commit.
- **Cross-family handle pull MUST hold across all slices.** D.5 verification in Common D-block is the gate. If Image's regen emits `SDL_Renderer*` (pointer) instead of `SDL_Renderer` (by-value), the cross-family pull broke — reject the slice.
- **`SDL_bool` does NOT gain `[Flags]`.** Headline assertion in S1-6 Test 10. If the rewriter ever decorates SDL_bool, the heuristic regressed to value-pattern matching.
- **`git mv` FIRST** during S1-5 file rename. Memory `git mv during migrations`.
- **Slopwatch per code-touching slice** with baseline rebuild on file deletes/renames (S1-1, S1-5).
- **Determinism contract per code-touching slice.** All 5 D-steps (D.1-D.5) execute before commit. D.1 is now a **true two-run snapshot diff** (not HEAD diff) — capture snapshot between two regens.
- **No bind-mount Windows-host repo into Linux container** for Linux x64 validation (Priority C closure constraint; relevant if Linux docker validation comes up in S1-7).
- **Pattern B `Equals(object obj)` no nullable annotation** in generated `.g.cs` files (existing convention; relevant if S1-4 touches BuildHandlesFileContent's output format).
- **No CPM (Directory.Packages.props) edits without orchestrator approval.**
- **Subagent dispatch hygiene**: same as Priority C. If Item 1 execution surfaces ambiguity, brief Deniz; do not guess.

## Final Steering Note

Item 1 is the foundation Items 2-5 (and Iteration 2) build on. The spec + plan + Constitution edits + roadmap edits sit in the working tree, fully reviewed, ready to commit. The next session's hardest call is **discipline, not technical** — execute the plan as written, slice by slice, with Deniz approval between each commit. Resist the urge to "simplify" or "combine" slices; the granularity is deliberate (per-slice approval gate matches Deniz's pacing preference, and the bite-sized steps are designed for cold-start agents).

The plan's Common: Determinism Contract Verification block is the safety net — if every code-touching slice passes D.1-D.5, the Item 1 success criteria fall out naturally at S1-7. Do not skip D-block steps to save time; they catch the kinds of regressions (cross-family pull breakage, idempotency drift, family isolation leak) that compound across slices and are expensive to debug later.

37 commits still await Deniz's push approval from Priority C closure. Item 1 will likely add 12-15 more commits across its 8 slices. The push gate is a milestone decision, not a per-slice one. Hold the line; the plan is the contract.
