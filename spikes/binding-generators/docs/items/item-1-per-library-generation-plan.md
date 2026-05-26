# Item 1 — Per-Library Generation Infrastructure (Execution Plan)

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:subagent-driven-development` (recommended) or `superpowers:executing-plans` to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make `generate_bindings.py` + the 7-step postprocess pipeline + `oracle.cs` per-family invocation deterministic and family-agnostic, so Items 3–5 can plug in by adding data only (production header lists, RSPs, csprojs) rather than by editing pipeline logic.

**Architecture:** Seven commit-sized slices: cross-cutting cleanup → orchestrator family-agnostic → oracle multi-family → opaque rewriter family-blind + roster migration → ClongDualDispatchRewriter Roslyn refactor → FlagsAttributeRewriter alimer-style + new flags-detect step → final exit verification. Each slice is independently testable; no slice ships output regression on Core + Image (modulo audited `[Flags]` additions per spec §6 success criterion #1 carve-out).

**Tech Stack:** ClangSharp source generator + Roslyn (Microsoft.CodeAnalysis.CSharp 4.12) postprocess; Python 3 orchestrator (`generate_bindings.py`); TUnit + Microsoft.Testing.Platform for AbiTests; JSON roster files (family-keyed schema 2.0); 5-TFM matrix (net10.0, net9.0, net8.0, netstandard2.0, net462) for Core + Image; 4-TFM (net10/9/8/462) for AbiTests.

**Date:** 2026-05-25
**Status:** Per-slice execution plan. Temporary — delete after Item 1 ships (matches refactoring-doc lifecycle).
**Branch:** `spike/binding-autogen-sdl2-gfx`
**Spec:** [`item-1-per-library-generation-spec.md`](item-1-per-library-generation-spec.md) — durable decisions.
**Parent roadmap:** [`../satellite-expansion-roadmap.md`](../satellite-expansion-roadmap.md) §Item 1.
**Approval gate:** [`AGENTS.md`](../../../../AGENTS.md) §"Approval Gate" — every slice that touches code waits for explicit `başla/yap/proceed/go/apply`.

---

## How to read this plan

- **Slices are commit-sized.** Each has a clear exit gate; do not stack slices into one commit. Each slice is its own approval-gated unit.
- **TDD shape per code change.** Each task that changes behavior follows: (1) write failing test → (2) run test, verify it fails with the expected error → (3) write minimal implementation → (4) run test, verify it passes → (5) commit. Verification-only tasks (sanity build, grep checks, slopwatch) skip steps 3–4.
- **Bite-sized steps (2–5 min each).** Every step shows exact code, exact command, and expected output. No placeholders.
- **No slice ships output regression on Core + Image.** Every code-touching slice ends with the **Determinism Contract Verification** + **Slopwatch** common tasks (defined below) before commit. Exception: audited `[Flags]` additions in S1-6 per spec §6 criterion #1 carve-out.
- **Constitution authority.** Every slice cross-references the Constitution sections it must honor (§"Generation Determinism Contract", §"Opaque Handles", §"Enums", etc.). Implementations cannot drift from policy.
- **Plan-doc reference discipline.** Each slice cross-references the spec section that holds the decision and the code anchors that change.
- **TaskCreate per slice.** When execution begins, the operating agent creates a `TaskCreate` per slice and marks it `in_progress`/`completed` along the way. Batch routine task-completions silently per Deniz's pacing memory; stop only at significant boundaries (slice exit, blocker, ambiguity).

---

## Slice ordering rationale

```
S1-0  Land doc artifacts                (Constitution + spec + plan + roadmap edits from doc tour — currently uncommitted)
  ↓
S1-1  Cross-cutting cleanup            (doc + file removal — least risk, fully reversible)
  ↓
S1-2  Orchestrator family-agnostic     (data + small logic — affects per-family-CLI surface only)
  ↓
S1-3  Oracle multi-family              (data-only — affects evidence reports only)
  ↓
S1-4  Opaque rewriter family-blind     (postprocess refactor — affects rewriter discovery only)
  ↓
S1-5  ClongDualDispatchRewriter        (postprocess refactor — biggest behavioral change)
  ↓
S1-6  FlagsAttributeRewriter           (new postprocess step + pipeline order)
  ↓
S1-7  Final exit verification          (no code change — full regen + multi-TFM + oracle + slopwatch)
```

S1-4 and S1-5 are independent of each other but both must precede S1-7. They're sequenced 4→5 so the bigger refactor (S1-5) lands against a known-clean opaque rewriter. S1-6 is independent of S1-4/5; it could run earlier, but sequencing it after the rewriter changes keeps regen evidence ordered by complexity.

---

## Per-slice common requirements (referenced by every code-touching slice)

Every code-touching slice (S1-2 through S1-6) ends with these mandatory verification tasks before the commit step. They are defined once here and referenced from each slice as `Common: Determinism Contract Verification` and `Common: Slopwatch` to avoid 5-fold duplication.

### Common: Determinism Contract Verification

**Constitution authority:** §"Generation Determinism Contract" §Verification Contract.

**Ordering rationale.** D.1 (idempotency) requires two consecutive regen runs compared TO EACH OTHER (not to HEAD — code-touching slices have expected drift from HEAD by design). Execute mode always cleans the selected family/families' `Generated/` roots before generating. D.4 restores the `--family all` baseline (since D.2+D.3 leave Core/Image in non-`all` state) and reverifies per-family equivalence. D.5 runs AFTER D.4 has the `--family all` baseline in place. **Execute steps in numerical order.**

- [ ] **Step D.1: Idempotency — true two-run diff**
  Run regeneration TWICE, snapshot between runs, diff the two outputs against each other:

  ```pwsh
  # First regen
  python spikes/binding-generators/clangsharp/generate_bindings.py --family all --execute --vcpkg-triplet x64-windows-hybrid --use-platform-header-shims

  # Snapshot
  $snap = Join-Path $env:TEMP "item1-idempotency-$(Get-Date -Format yyyyMMddHHmmss)"
  Copy-Item -Recurse "spikes/binding-generators/clangsharp/src/Janset.SDL2.Core/Generated/"  "$snap/Core/"
  Copy-Item -Recurse "spikes/binding-generators/clangsharp/src/Janset.SDL2.Image/Generated/" "$snap/Image/"

  # Second regen
  python spikes/binding-generators/clangsharp/generate_bindings.py --family all --execute --vcpkg-triplet x64-windows-hybrid --use-platform-header-shims

  # Diff snapshot vs current regen (NOT against HEAD)
  git diff --ignore-cr-at-eol --no-index "$snap/Core/"  "spikes/binding-generators/clangsharp/src/Janset.SDL2.Core/Generated/"  | Measure-Object -Line
  git diff --ignore-cr-at-eol --no-index "$snap/Image/" "spikes/binding-generators/clangsharp/src/Janset.SDL2.Image/Generated/" | Measure-Object -Line

  Remove-Item -Recurse -Force $snap
  ```

  Expected: both `Measure-Object -Line` outputs are 0. Two consecutive regens with identical inputs MUST produce byte-identical output. Non-zero → generator has nondeterministic input (wall-clock timestamps, machine identifiers, etc.) — Constitution §"Generation Determinism Contract" §Pure-Inputs Discipline violation. Investigate before commit.

- [ ] **Step D.2: Family isolation — Core-only run**
  Verify `--family core --execute` does NOT modify Image's Generated/:

  ```pwsh
  python spikes/binding-generators/clangsharp/generate_bindings.py --family core --execute --vcpkg-triplet x64-windows-hybrid --use-platform-header-shims
  git status --short spikes/binding-generators/clangsharp/src/Janset.SDL2.Image/Generated/
  ```

  Expected: 0 lines output (Image untouched). If any output, isolation is broken — investigate.

- [ ] **Step D.3: Family isolation — Image-only run**
  Verify `--family image --execute` does NOT modify Core's Generated/:

  ```pwsh
  python spikes/binding-generators/clangsharp/generate_bindings.py --family image --execute --vcpkg-triplet x64-windows-hybrid --use-platform-header-shims
  git status --short spikes/binding-generators/clangsharp/src/Janset.SDL2.Core/Generated/
  ```

  Expected: 0 lines output (Core untouched).

- [ ] **Step D.4: Per-family equivalence to --family all**
  After D.2 + D.3 leave Core/Image in non-`all` state, restore the baseline and verify per-family runs match it:

  ```pwsh
  # Restore --family all baseline
  python spikes/binding-generators/clangsharp/generate_bindings.py --family all --execute --vcpkg-triplet x64-windows-hybrid --use-platform-header-shims

  # Snapshot baseline
  $allSnap = Join-Path $env:TEMP "item1-family-all-$(Get-Date -Format yyyyMMddHHmmss)"
  Copy-Item -Recurse "spikes/binding-generators/clangsharp/src/Janset.SDL2.Core/Generated/"  "$allSnap/Core/"
  Copy-Item -Recurse "spikes/binding-generators/clangsharp/src/Janset.SDL2.Image/Generated/" "$allSnap/Image/"

  # Per-family sequential
  python spikes/binding-generators/clangsharp/generate_bindings.py --family core --execute --vcpkg-triplet x64-windows-hybrid --use-platform-header-shims
  python spikes/binding-generators/clangsharp/generate_bindings.py --family image --execute --vcpkg-triplet x64-windows-hybrid --use-platform-header-shims

  # Diff baseline vs per-family
  git diff --ignore-cr-at-eol --no-index "$allSnap/Core/"  "spikes/binding-generators/clangsharp/src/Janset.SDL2.Core/Generated/"  | Measure-Object -Line
  git diff --ignore-cr-at-eol --no-index "$allSnap/Image/" "spikes/binding-generators/clangsharp/src/Janset.SDL2.Image/Generated/" | Measure-Object -Line

  # Restore --family all baseline for D.5
  python spikes/binding-generators/clangsharp/generate_bindings.py --family all --execute --vcpkg-triplet x64-windows-hybrid --use-platform-header-shims

  Remove-Item -Recurse -Force $allSnap
  ```

  Expected: both `Measure-Object -Line` outputs are 0. Per-family runs and `--family all` produce identical bytes modulo CRLF.

- [ ] **Step D.5: Cross-family handle pull preservation — Compat AND Modern**
  Image's generated output must contain Core handle types **by-value** at every raw ABI signature position in BOTH codegen trees:

  ```pwsh
  # Modern tree — by-value form must be present
  Select-String -Path "spikes/binding-generators/clangsharp/src/Janset.SDL2.Image/Generated/Modern/SDL_image.g.cs" -Pattern "SDL_Renderer renderer" -SimpleMatch
  Select-String -Path "spikes/binding-generators/clangsharp/src/Janset.SDL2.Image/Generated/Modern/SDL_image.g.cs" -Pattern "SDL_RWops src" -SimpleMatch

  # Compat tree — symmetric check (cross-family pull must hold for legacy DllImport surface)
  Select-String -Path "spikes/binding-generators/clangsharp/src/Janset.SDL2.Image/Generated/Compat/SDL_image.g.cs" -Pattern "SDL_Renderer renderer" -SimpleMatch
  Select-String -Path "spikes/binding-generators/clangsharp/src/Janset.SDL2.Image/Generated/Compat/SDL_image.g.cs" -Pattern "SDL_RWops src" -SimpleMatch

  # Anti-check: pointer-form MUST NOT appear (would mean cross-family pull broke)
  Select-String -Path "spikes/binding-generators/clangsharp/src/Janset.SDL2.Image/Generated/" -Pattern "SDL_Renderer\* " -Recurse
  Select-String -Path "spikes/binding-generators/clangsharp/src/Janset.SDL2.Image/Generated/" -Pattern "SDL_RWops\* " -Recurse
  ```

  Expected: ≥1 match per by-value grep across both Compat AND Modern (4 matches total minimum). The two anti-check pointer-form greps MUST return 0 matches. If any pointer-form match appears, the cross-family handle pull in `LoadRoster` is broken — investigate per Constitution §"Opaque Handles" Cross-family handle name resolution.

### Common: Slopwatch anti-slop gate

**AGENTS.md authority:** Tier 1 mandatory `dotnet-skills:slopwatch`.

**Prerequisite:** Slopwatch must be installed globally. If `slopwatch` is not on PATH, install before the first slice runs:

```pwsh
dotnet tool install -g slopwatch
```

Verify with `slopwatch --version`. Expected: any version string. If install fails, see `dotnet-skills:slopwatch` documentation per AGENTS.md Tier 1.

- [ ] **Step S.1: Run slopwatch**

  ```pwsh
  slopwatch analyze --fail-on warning --exclude "artifacts/**,external/**,vcpkg_installed/**,spikes/binding-generators/references/**,**/bin/**,**/obj/**"
  ```

  Expected: `Scan complete: 0 issue(s) found`. If any issue: investigate and resolve before commit.

- [ ] **Step S.2: If files deleted in this slice, rebuild slopwatch baseline**
  Required only when a slice deletes source files (e.g. S1-1 removes `compare_oracle.py`; S1-5 renames `ThreadIdDualDispatchRewriter.cs`). Stale baseline entries point at deleted paths.

  ```pwsh
  slopwatch init -f --exclude "artifacts/**,external/**,vcpkg_installed/**,spikes/binding-generators/references/**,**/bin/**,**/obj/**"
  ```

  Expected: baseline regenerated; subsequent `slopwatch analyze` returns 0 issues.

---

## S1-0 — Land doc artifacts (Constitution + spec + plan + roadmap)

**Goal:** Land the 5 Constitution edits and 3 new/modified doc files from the Item 1 doc tour that currently sit uncommitted in the working tree. These commits MUST precede any code slice so that the audit trail ties Constitution policy edits to the slice that depends on them, and so a fresh clone of the branch starts from a coherent state.

**Spec sections:** §10.1 (References — enumerates the 5 Constitution edits expected to land in the Item 1 change set).
**Constitution sections:** none (this slice LANDS the Constitution edits; it does not reference policy).

**Rationale:** Constitution `docs/binding-autogen/binding-generator-constitution.md` is currently in `M` (uncommitted modified) state with 5 edits: (1) §"Opaque Handles" Auto-detect criterion family-blind generalization, (2) §"Opaque Handles" Canonical roster family-keyed migration prose, (3) §"Opaque Handles" Cross-family handle name resolution NEW subsection, (4) §"Enums" auto-decoration policy rewrite, (5) §"Current SDL2.Core ABI Status" point 7 designation-vs-emission fix, (6) NEW §"Generation Determinism Contract" top-level section. The roadmap and spec docs also have uncommitted edits. Each code slice (S1-2 onwards) references these sections as binding policy; if they remain uncommitted the audit chain is broken and a fresh clone reverts the references to invalid state.

### Task 0.1: Inventory uncommitted doc edits

**Files:** none modified (inventory only).

- [ ] **Step 0.1.1: List uncommitted doc changes**

  ```pwsh
  git status --short docs/ spikes/binding-generators/docs/
  ```

  Expected output (order may vary):

  ```
   M docs/binding-autogen/binding-generator-constitution.md
  ?? spikes/binding-generators/docs/items/
  ?? spikes/binding-generators/docs/satellite-expansion-roadmap.md
  ```

  (The `items/` directory is untracked because it contains the new spec + plan files; the roadmap is untracked because it was created in this branch.)

- [ ] **Step 0.1.2: Confirm Constitution diff scope**

  ```pwsh
  git diff --stat docs/binding-autogen/binding-generator-constitution.md
  ```

  Expected: a single file with ~80+ insertions and ~10-15 deletions reflecting the 5 documented sections. If the diff is significantly different, investigate before committing — the tree may have drifted.

### Task 0.2: Commit Constitution edits

**Files:**

- Stage + commit: `docs/binding-autogen/binding-generator-constitution.md`

- [ ] **Step 0.2.1: Stage Constitution**

  ```pwsh
  git add docs/binding-autogen/binding-generator-constitution.md
  ```

- [ ] **Step 0.2.2: Commit with explicit section enumeration**

  ```bash
  git commit -m "$(cat <<'EOF'
  docs(constitution): Item 1 policy edits — family-keyed opaque + [Flags] + determinism contract (S1-0a)

  Five Constitution edits supporting Item 1's per-library generation infrastructure:

  1. §"Opaque Handles" Auto-detect criterion — generalize prose from SDL_X to X
     (family-blind structural test); explicit family-blind statement covering
     satellite-owned handles (TTF_Font, Mix_Music).
  2. §"Opaque Handles" Canonical roster — migrate prose for family-keyed schema
     2.0 (top-level families object; per-family library_version + lists);
     drift watchdog runs per-owner-family.
  3. §"Opaque Handles" Cross-family handle name resolution (NEW subsection) —
     codify the LoadRoster cross-family pull contract: satellites see Core's
     auto_detect_well_known + force_opaque_exceptions for Pattern B by-value
     uniformity.
  4. §"Enums" auto-decoration policy — REWRITE alimer-style (name suffix +
     family-keyed allow-list); explicit rejection of bit-value heuristics
     (SDL_bool false-positive risk codified).
  5. §"Current SDL2.Core ABI Status" point 7 — fix designation-vs-emission
     drift (Stage 1 bitmask enums designated but emission delivered by Item 1's
     flags-detect step).
  6. NEW §"Generation Determinism Contract" — top-level section binding all
     determinism invariants: pin set inputs, family isolation, dependency
     direction, native header resolution scope, pure-inputs discipline,
     per-slice verification contract.

  All edits are policy refinements; settled Layer 1 decisions are preserved.

  Refs: spec §10.1 Authority, Roadmap §Item 1.
  EOF
  )"
  ```

- [ ] **Step 0.2.3: Verify commit landed**

  ```pwsh
  git log -1 --oneline docs/binding-autogen/binding-generator-constitution.md
  ```

  Expected: latest commit subject `docs(constitution): Item 1 policy edits — family-keyed opaque + [Flags] + determinism contract (S1-0a)`.

### Task 0.3: Commit roadmap update

**Files:**

- Stage + commit: `spikes/binding-generators/docs/satellite-expansion-roadmap.md`

- [ ] **Step 0.3.1: Stage roadmap**

  ```pwsh
  git add spikes/binding-generators/docs/satellite-expansion-roadmap.md
  ```

- [ ] **Step 0.3.2: Commit**

  ```bash
  git commit -m "$(cat <<'EOF'
  docs(spike): satellite expansion roadmap + Iteration 2 placeholder (S1-0b)

  Roadmap drives Item 1-5 satellite expansion; Iteration 2 (Config Surface
  Unification) inserted as immediate-post-Item-1 placeholder with problem +
  18-source inventory + binding constraints. Cross-Cutting + Item 1 §[Flags]
  + policy/roster subsections updated to reflect family-keyed schema 2.0 +
  alimer-style flags detection direction.

  Refs: Constitution §"Generation Determinism Contract", §"Manifest
  Configuration Vs Code-Owned Policy".
  EOF
  )"
  ```

### Task 0.4: Commit Item 1 spec + plan (this file)

**Files:**

- Stage + commit: `spikes/binding-generators/docs/items/item-1-per-library-generation-spec.md` + `item-1-per-library-generation-plan.md`

- [ ] **Step 0.4.1: Stage spec + plan**

  ```pwsh
  git add spikes/binding-generators/docs/items/
  ```

- [ ] **Step 0.4.2: Commit**

  ```bash
  git commit -m "$(cat <<'EOF'
  docs(spike): Item 1 spec + execution plan (S1-0c)

  Durable spec (item-1-per-library-generation-spec.md) carries the 7 design
  decisions (§5.1–§5.7); execution plan (item-1-per-library-generation-plan.md)
  carries the 8 slices (S1-0 through S1-7) with TDD-disciplined bite-sized
  tasks. Plan doc deletes after Item 1 ships per refactoring-doc lifecycle;
  spec stays as durable reference.

  Refs: Roadmap §Item 1, Constitution §"Generation Determinism Contract".
  EOF
  )"
  ```

### Task 0.5: Sanity check — no leftover uncommitted doc changes

- [ ] **Step 0.5.1: Verify clean working tree (docs)**

  ```pwsh
  git status --short docs/ spikes/binding-generators/docs/
  ```

  Expected: 0 lines (all doc artifacts committed).

- [ ] **Step 0.5.2: Verify commit chain**

  ```pwsh
  git log --oneline -5
  ```

  Expected: latest 3 commits are the S1-0a/S1-0b/S1-0c commits (in reverse order — c, b, a).

**Slice exit evidence:**

- ✓ Constitution edits committed (S1-0a).
- ✓ Roadmap committed (S1-0b).
- ✓ Spec + plan committed (S1-0c).
- ✓ Working tree clean for docs/ + spikes/binding-generators/docs/.
- ✓ Audit chain: subsequent slices' Constitution references resolve to a committed file.

**Note:** This slice has no code changes, no Determinism Contract verification (no generation/postprocess touched), and no Slopwatch (no .cs / .py edits). Plan/spec/Constitution policy alignment is the slice's deliverable.

**Commit message shape:** Three small commits (S1-0a Constitution, S1-0b roadmap, S1-0c spec+plan). Approval gate per `AGENTS.md` §"Approval Gate" — documentation-only edits are exception-allowed but Deniz approves the slice as a whole.

---

## S1-1 — Cross-cutting standardization

**Goal:** Retire sunset `compare_oracle.py`. Verify `sdl2-core-sdlh-required.json` is load-bearing and stays. Confirm `comparison-report-template.md` removal if present. **No determinism verification needed** — this slice does not touch generation or postprocess code.

**Spec sections:** §7 (Cross-Cutting Standardization), §4.7 (Cross-cutting status baseline).
**Constitution sections:** none directly (file housekeeping); §"Generator Home" implicitly anchored.
**Roadmap sections:** §Cross-Cutting (the `compare_oracle.py` and `sdl2-core-sdlh-required.json` notes; both already corrected during the Item 1 doc tour — this slice executes the file removal).

**Code anchors:**

- `spikes/binding-generators/clangsharp/compare_oracle.py` — to be removed.
- `spikes/binding-generators/scope/sdl2-core-sdlh-required.json` — load-bearing per `generate_bindings.py:554`; stays.

### Task 1.1: Verify compare_oracle.py has no live references

**Files:** none modified (verification only).

- [ ] **Step 1.1.1: Grep for `compare_oracle` across the repo**

  Use the Grep tool with parameters:
  - `pattern`: `compare_oracle`
  - `path`: repository root
  - `output_mode`: `files_with_matches`

  Expected results — these are the **only acceptable** match locations:
  - `spikes/binding-generators/docs/items/item-1-per-library-generation-plan.md` (this plan)
  - `spikes/binding-generators/docs/items/item-1-per-library-generation-spec.md` (spec doc — §7, §4.7 mention retire)
  - `spikes/binding-generators/docs/satellite-expansion-roadmap.md` (roadmap §Cross-Cutting)
  - `spikes/binding-generators/docs/next-iteration-plan.md` (historical reference to the script)

  **Reject if** any `.py`, `.cs`, `.rsp`, `.json`, `.cake`, `.targets`, or `.csproj` file references `compare_oracle`. If any such reference exists, STOP this slice and investigate — the file is still in active use.

- [ ] **Step 1.1.2: Record verification count**
  Note the grep match count in your TaskUpdate progress note. Acceptable count: 4–5 matches (all in docs).

### Task 1.2: Remove compare_oracle.py via git rm

**Files:**

- Delete: `spikes/binding-generators/clangsharp/compare_oracle.py`

- [ ] **Step 1.2.1: Remove the file via git rm (preserves history)**

  ```pwsh
  git rm spikes/binding-generators/clangsharp/compare_oracle.py
  ```

  Expected output:

  ```
  rm 'spikes/binding-generators/clangsharp/compare_oracle.py'
  ```

- [ ] **Step 1.2.2: Verify the deletion is staged**

  ```pwsh
  git status --short spikes/binding-generators/clangsharp/compare_oracle.py
  ```

  Expected output:

  ```
  D  spikes/binding-generators/clangsharp/compare_oracle.py
  ```

### Task 1.3: Verify sdl2-core-sdlh-required.json is load-bearing and stays

**Files:** none modified (verification only).

- [ ] **Step 1.3.1: Grep for the file's consumer**

  Use the Grep tool with parameters:
  - `pattern`: `sdl2-core-sdlh-required`
  - `path`: `spikes/binding-generators/clangsharp/`
  - `output_mode`: `content`
  - `-n`: true

  Expected: at least one match in `generate_bindings.py` referencing the file (specifically in `generate_required_sdlh_surface` and `should_validate_required_sdlh_surface`).

- [ ] **Step 1.3.2: Confirm the file is present and not staged for deletion**

  ```pwsh
  Test-Path spikes/binding-generators/scope/sdl2-core-sdlh-required.json
  git status --short spikes/binding-generators/scope/sdl2-core-sdlh-required.json
  ```

  Expected:
  - `Test-Path` returns `True`.
  - `git status --short` returns empty (no staged changes).

### Task 1.4: Check + conditionally remove comparison-report-template.md

**Files:**

- Conditional delete: `spikes/binding-generators/scope/comparison-report-template.md` (only if present AND unreferenced).

- [ ] **Step 1.4.1: Check existence**

  ```pwsh
  Test-Path spikes/binding-generators/scope/comparison-report-template.md
  ```

  - If `False`: skip Steps 1.4.2 and 1.4.3.
  - If `True`: proceed.

- [ ] **Step 1.4.2: Verify no live references**

  Use the Grep tool:
  - `pattern`: `comparison-report-template`
  - `path`: repository root
  - `output_mode`: `files_with_matches`

  Expected acceptable matches: this plan, spec, roadmap docs only. Reject removal if any code/RSP/JSON references the file.

- [ ] **Step 1.4.3: Remove if unreferenced**

  ```pwsh
  git rm spikes/binding-generators/scope/comparison-report-template.md
  ```

### Task 1.5: Sanity build to confirm nothing broke

**Files:** none modified (verification only).

- [ ] **Step 1.5.1: Build the spike solution across all 5 TFMs**

  ```pwsh
  dotnet build spikes/binding-generators/clangsharp/Janset.SDL2.ClangSharpSpike.slnx -c Release
  ```

  Expected last lines of output:

  ```
  Build succeeded.
      0 Warning(s)
      0 Error(s)
  ```

  Reject any warning or error; do not proceed to commit.

### Task 1.6: Common: Slopwatch anti-slop gate

See "Per-slice common requirements" §Common: Slopwatch.

- [ ] **Step 1.6.1: Run slopwatch baseline rebuild** (required — this slice deletes a file)

  ```pwsh
  slopwatch init -f --exclude "artifacts/**,external/**,vcpkg_installed/**,spikes/binding-generators/references/**,**/bin/**,**/obj/**"
  ```

  Expected: baseline regenerates without errors.

- [ ] **Step 1.6.2: Run slopwatch analyze**

  ```pwsh
  slopwatch analyze --fail-on warning --exclude "artifacts/**,external/**,vcpkg_installed/**,spikes/binding-generators/references/**,**/bin/**,**/obj/**"
  ```

  Expected: `Scan complete: 0 issue(s) found`.

### Task 1.7: Commit

- [ ] **Step 1.7.1: Stage and review changes**

  ```pwsh
  git status --short
  ```

  Expected staged:

  ```
  D  spikes/binding-generators/clangsharp/compare_oracle.py
  ```

  (Plus `comparison-report-template.md` deletion if Task 1.4 fired.)

- [ ] **Step 1.7.2: Commit with heredoc message**

  ```bash
  git commit -m "$(cat <<'EOF'
  cleanup(spike): retire compare_oracle.py (S1-1)

  - Remove sunset compare_oracle.py; oracle.cs is the active evidence reporter
    per roadmap §Cross-Cutting.
  - sdl2-core-sdlh-required.json kept — load-bearing for
    generate_bindings.py:554 required-surface validation.

  Refs: spec §7, roadmap §Cross-Cutting, ADR-004 (Reopened).
  EOF
  )"
  ```

- [ ] **Step 1.7.3: Verify commit landed**

  ```pwsh
  git log -1 --oneline
  ```

  Expected: latest commit subject is `cleanup(spike): retire compare_oracle.py (S1-1)`.

**Slice exit evidence:**

- ✓ `compare_oracle.py` removed (verified via `git status` and `git log`).
- ✓ `sdl2-core-sdlh-required.json` confirmed load-bearing; remains in tree.
- ✓ Sanity build: 0 warnings, 0 errors across 5 TFMs.
- ✓ Slopwatch baseline rebuilt; analyze clean.
- ✓ Commit landed with conventional message + slice reference.

**Note:** Determinism Contract Verification is **not required** for S1-1 — no generation or postprocess code touched. The sanity build (Task 1.5) provides equivalent assurance for this file-removal-only slice.

---

## S1-2 — Orchestrator family-agnostic refactor

**Goal:** `generate_bindings.py` enumerates families dynamically from `selected_families()` everywhere — `stats`, `write_report`, `--family` choices. New families (`ttf`/`mixer`/`gfx`) are reachable per-family via CLI but stay dormant in `--family all`. Self-tests cover the new family error paths.

**Spec sections:** §5.1 (Dormant `all`), §5.5 (Owner-mode), §6 success criteria #5.
**Constitution sections:** §"Generation Determinism Contract" §Family Isolation (per-family equivalence to `--family all` must hold).
**Roadmap sections:** §Item 1 success criterion 5.

**Code anchors:**

- `generate_bindings.py:327-344` — `FAMILY_CONFIG`.
- `generate_bindings.py:449-455` — `PLATFORM_SENSITIVE_HEADERS`.
- `generate_bindings.py:776-779` — `selected_families`.
- `generate_bindings.py:825` — `write_report` hardcoded loop.
- `generate_bindings.py:1054` — `--family` argparse choices.
- `generate_bindings.py:1078-1081` — `stats` init dict literal.
- `generate_bindings.py:1267` — owner-mode wiring.
- `generate_bindings.py:858-1047` — `run_self_tests()` function.

### Task 2.1: Add failing self-test for ttf/mixer/gfx FAMILY_CONFIG lookups

**Files:**

- Modify: `spikes/binding-generators/clangsharp/generate_bindings.py` (extend `run_self_tests` function around L858)

- [ ] **Step 2.1.1: Add new test assertions before the `if failures:` block (around L1041)**

  Append this block immediately before the trailing `if failures:` check in `run_self_tests`:

  ```python
      # New families (ttf/mixer/gfx) must be present in FAMILY_CONFIG even when dormant
      # in selected_families("all"). Per Item 1 spec §5.1, they're CLI-reachable per-family.
      for family in ("ttf", "mixer", "gfx"):
          if family not in FAMILY_CONFIG:
              failures.append(f"FAMILY_CONFIG missing entry for new family '{family}'")
              continue
          entry = FAMILY_CONFIG[family]
          for key in ("namespace", "raw_class", "rsp", "headers", "library_dir"):
              if key not in entry:
                  failures.append(f"FAMILY_CONFIG[{family!r}] missing key {key!r}")
          if family not in PLATFORM_SENSITIVE_HEADERS:
              failures.append(f"PLATFORM_SENSITIVE_HEADERS missing entry for '{family}'")
          elif PLATFORM_SENSITIVE_HEADERS[family] != []:
              failures.append(
                  f"PLATFORM_SENSITIVE_HEADERS[{family!r}] expected empty list, got {PLATFORM_SENSITIVE_HEADERS[family]!r}"
              )

      # selected_families("all") must remain dormant per Item 1 spec §5.1 — only ["core", "image"]
      if selected_families("all") != ["core", "image"]:
          failures.append(
              f"selected_families('all') must stay ['core', 'image'] per Item 1 dormant policy; got {selected_families('all')!r}"
          )

      # New families must be valid --family CLI choices (orchestrator argparse accepts them)
      argparser_choices = ("core", "image", "ttf", "mixer", "gfx", "all")
      # Verified by direct probe: production header-list resolution should not crash for ttf
      try:
          production_header_list_file_name("ttf")
      except KeyError as exc:
          failures.append(f"production_header_list_file_name('ttf') raised KeyError: {exc}")

      # Owner-mode wiring: ttf/mixer should be owner; image/gfx consumer
      def _owner_mode(family: str) -> str:
          return "owner" if family in ("core", "ttf", "mixer") else "consumer"
      for family, expected in (("core", "owner"), ("ttf", "owner"), ("mixer", "owner"),
                                ("image", "consumer"), ("gfx", "consumer")):
          actual = _owner_mode(family)
          if actual != expected:
              failures.append(f"owner-mode wiring for {family!r}: expected {expected!r}, got {actual!r}")
  ```

- [ ] **Step 2.1.2: Run self-test, verify it FAILS with expected errors**

  ```pwsh
  python spikes/binding-generators/clangsharp/generate_bindings.py --self-test
  ```

  Expected output includes lines like:

  ```
  self-test: FAIL: FAMILY_CONFIG missing entry for new family 'ttf'
  self-test: FAIL: FAMILY_CONFIG missing entry for new family 'mixer'
  self-test: FAIL: FAMILY_CONFIG missing entry for new family 'gfx'
  ```

  And exit code 1.

### Task 2.2: Add ttf entry to FAMILY_CONFIG

**Files:**

- Modify: `spikes/binding-generators/clangsharp/generate_bindings.py:327-344`

- [ ] **Step 2.2.1: Add ttf entry**

  Inside `FAMILY_CONFIG` dict (after the `image` entry), add:

  ```python
      "ttf": {
          "namespace": "SDL2.Ttf",
          "raw_class": "SDL_ttfNative",
          "rsp": "sdl2-ttf.rsp",
          "headers": "sdl2-ttf.headers.txt",
          "library_dir": "Janset.SDL2.Ttf",
      },
  ```

  Note: the referenced `sdl2-ttf.rsp` and header list do not yet exist — Item 4 creates them. Item 1 only adds the FAMILY_CONFIG metadata.

### Task 2.3: Add mixer entry to FAMILY_CONFIG

**Files:**

- Modify: `spikes/binding-generators/clangsharp/generate_bindings.py:327-344`

- [ ] **Step 2.3.1: Add mixer entry**

  Inside `FAMILY_CONFIG`, after the `ttf` entry, add:

  ```python
      "mixer": {
          "namespace": "SDL2.Mixer",
          "raw_class": "SDL_mixerNative",
          "rsp": "sdl2-mixer.rsp",
          "headers": "sdl2-mixer.headers.txt",
          "library_dir": "Janset.SDL2.Mixer",
      },
  ```

### Task 2.4: Add gfx entry to FAMILY_CONFIG

**Files:**

- Modify: `spikes/binding-generators/clangsharp/generate_bindings.py:327-344`

- [ ] **Step 2.4.1: Add gfx entry**

  Inside `FAMILY_CONFIG`, after the `mixer` entry, add:

  ```python
      "gfx": {
          "namespace": "SDL2.Gfx",
          "raw_class": "SDL2_gfxNative",
          "rsp": "sdl2-gfx.rsp",
          "headers": "sdl2-gfx.headers.txt",
          "library_dir": "Janset.SDL2.Gfx",
      },
  ```

### Task 2.5: Extend PLATFORM_SENSITIVE_HEADERS

**Files:**

- Modify: `spikes/binding-generators/clangsharp/generate_bindings.py:449-455`

- [ ] **Step 2.5.1: Add ttf/mixer/gfx empty lists**

  Replace the current `PLATFORM_SENSITIVE_HEADERS` block (L449-455):

  ```python
  PLATFORM_SENSITIVE_HEADERS: dict[str, list[str]] = {
      "core": [
          "SDL_main.h",
          "SDL_system.h",
      ],
      "image": [],
      "ttf": [],
      "mixer": [],
      "gfx": [],
  }
  ```

### Task 2.6: Extend --family argparse choices

**Files:**

- Modify: `spikes/binding-generators/clangsharp/generate_bindings.py:1054`

- [ ] **Step 2.6.1: Extend choices list**

  At L1054, replace:

  ```python
      parser.add_argument("--family", choices=["core", "image", "all"], default="all")
  ```

  with:

  ```python
      parser.add_argument(
          "--family",
          choices=["core", "image", "ttf", "mixer", "gfx", "all"],
          default="all",
      )
  ```

### Task 2.7: Annotate dormant selected_families("all") with activation comment

**Files:**

- Modify: `spikes/binding-generators/clangsharp/generate_bindings.py:776-779`

- [ ] **Step 2.7.1: Add comment + verify behavior unchanged**

  Replace L776-779:

  ```python
  def selected_families(family: str) -> list[str]:
      if family == "all":
          return ["core", "image"]
      return [family]
  ```

  with:

  ```python
  def selected_families(family: str) -> list[str]:
      # "all" stays dormant per Item 1 spec §5.1 — only core + image are active.
      # Items 3/4/5 each activate their family by appending one entry to this list
      # as part of their exit criteria (gfx -> Item 3, ttf -> Item 4, mixer -> Item 5).
      if family == "all":
          return ["core", "image"]
      return [family]
  ```

  No behavior change — comment-only.

### Task 2.8: Refactor stats dict initialization to be dynamic (TDD)

**Files:**

- Modify: `spikes/binding-generators/clangsharp/generate_bindings.py` — extend `run_self_tests` + refactor stats init at L1078-1081.

The stats refactor is a behavior change that the spec §6 #1 byte-identical check covers at integration level — but the plan preamble's TDD discipline mandates a unit-level red→green pair specific to this refactor. The criterion #1 check is a backstop, not the TDD test.

- [ ] **Step 2.8.1: Add failing test for dynamic stats comprehension**

  Append to `run_self_tests`:

  ```python
      # Task 2.8: stats dict must be selected-driven. Comprehension-based init must
      # produce per-family entries for whatever selected_families() returns, not the
      # hardcoded ["core", "image"] of the prior implementation.
      def build_stats(selected: list[str]) -> dict[str, dict[str, int]]:
          return {family: {"headers": 0, "commands": 0, "generated_files": 0} for family in selected}

      expected_core_only = {"core": {"headers": 0, "commands": 0, "generated_files": 0}}
      if build_stats(["core"]) != expected_core_only:
          failures.append("stats comprehension for ['core'] did not produce expected shape")

      expected_all = {
          "core":  {"headers": 0, "commands": 0, "generated_files": 0},
          "image": {"headers": 0, "commands": 0, "generated_files": 0},
      }
      if build_stats(["core", "image"]) != expected_all:
          failures.append("stats comprehension for ['core', 'image'] did not produce expected shape")

      expected_ttf_only = {"ttf": {"headers": 0, "commands": 0, "generated_files": 0}}
      if build_stats(["ttf"]) != expected_ttf_only:
          failures.append("stats comprehension for ['ttf'] did not produce expected shape — selected-driven init regressed")
  ```

  The local `build_stats` helper mirrors the production comprehension expected at L1078-1081 after the refactor. This test fails as long as the production code hasn't been refactored to a comprehension (it asserts the SHAPE the refactor produces).

  Wait — this test passes even without the production refactor, because `build_stats` is a self-contained local function. To make it a true RED test, also assert that the production stats dict (after `main()` runs) has the expected family-keyed shape. Since calling `main()` from a self-test is expensive, use an alternate red check: assert that `inspect.getsource(main)` contains the comprehension pattern instead of the literal dict.

  Replace the test block above with:

  ```python
      # Task 2.8: stats dict must use a selected-driven comprehension at runtime.
      # Source-level check: the literal hardcoded {"core": ..., "image": ...} pattern
      # must be GONE from main(); the comprehension {family: ... for family in selected}
      # must be present.
      import inspect
      main_source = inspect.getsource(main)
      if '{"core": {"headers": 0' in main_source or '"image": {"headers": 0,' in main_source.split("\n")[0:200].__repr__():
          # Heuristic: hardcoded literal still present in main()
          if "for family in selected}" not in main_source:
              failures.append(
                  "Task 2.8 stats refactor incomplete: hardcoded {\"core\": ..., \"image\": ...} "
                  "literal still in main() and comprehension not yet introduced."
              )
      if "for family in selected}" not in main_source:
          failures.append(
              "Task 2.8 stats refactor incomplete: comprehension `{family: ... for family in selected}` not found in main()."
          )
  ```

- [ ] **Step 2.8.2: Run self-test, verify it FAILS**

  ```pwsh
  python spikes/binding-generators/clangsharp/generate_bindings.py --self-test
  ```

  Expected failure:

  ```
  self-test: FAIL: Task 2.8 stats refactor incomplete: comprehension `{family: ... for family in selected}` not found in main().
  ```

- [ ] **Step 2.8.3: Apply the refactor**

  Currently `generate_bindings.py:1078-1081`:

  ```python
      stats = {
          "core": {"headers": 0, "commands": 0, "generated_files": 0},
          "image": {"headers": 0, "commands": 0, "generated_files": 0},
      }
  ```

  The `selected` variable is computed at L1083 (`selected = selected_families(args.family)`). Stats init must come AFTER `selected` is known. Move `selected` computation up, then refactor:

  ```python
      selected = selected_families(args.family)
      stats = {family: {"headers": 0, "commands": 0, "generated_files": 0} for family in selected}
  ```

  Delete the original `selected = selected_families(args.family)` line at L1083 (now moved up).

- [ ] **Step 2.8.4: Run self-test, verify it PASSES**

  ```pwsh
  python spikes/binding-generators/clangsharp/generate_bindings.py --self-test
  ```

  Expected: `self-test: PASS`. The source-level check now finds the comprehension and the hardcoded literal is gone.

### Task 2.9: Refactor write_report family loop

**Files:**

- Modify: `spikes/binding-generators/clangsharp/generate_bindings.py:801-855`

- [ ] **Step 2.9.1: Replace hardcoded `["core", "image"]` loop with `selected`-driven iteration**

  At L825, replace:

  ```python
      for family in ["core", "image"]:
          family_stats = stats[family]
          lines.append(
              f"| {family} | {family_stats['headers']} | {family_stats['commands']} | {family_stats['generated_files']} |"
          )
  ```

  with:

  ```python
      for family in selected:
          family_stats = stats[family]
          lines.append(
              f"| {family} | {family_stats['headers']} | {family_stats['commands']} | {family_stats['generated_files']} |"
          )
  ```

  The `write_report` function already accepts `selected` as a parameter (L806); the body just needs to use it instead of the hardcoded list. No signature change required.

### Task 2.10: Update owner-mode wiring

**Files:**

- Modify: `spikes/binding-generators/clangsharp/generate_bindings.py:1267`

- [ ] **Step 2.10.1: Extend owner family list to include ttf and mixer**

  At L1267, replace:

  ```python
                  owner_mode = "owner" if family == "core" else "consumer"
  ```

  with:

  ```python
                  owner_mode = "owner" if family in ("core", "ttf", "mixer") else "consumer"
  ```

  **No effect yet** — ttf/mixer are not in `selected_families("all")` so this code path doesn't fire. The spec records the contract so Items 4/5 don't have to touch this line.

### Task 2.11: Run self-test, verify it now PASSES

**Files:** none modified (verification only).

- [ ] **Step 2.11.1: Run the self-test**

  ```pwsh
  python spikes/binding-generators/clangsharp/generate_bindings.py --self-test
  ```

  Expected output:

  ```
  self-test: PASS
  ```

  Exit code 0.

### Task 2.12: Verify --family all output is byte-identical to current HEAD

**Files:** none modified (verification only).

- [ ] **Step 2.12.1: Run full regen**

  ```pwsh
  python spikes/binding-generators/clangsharp/generate_bindings.py --family all --execute --vcpkg-triplet x64-windows-hybrid --use-platform-header-shims
  ```

  Expected: exits 0, writes `clangsharp-production.md` report.

- [ ] **Step 2.12.2: Diff against HEAD**

  ```pwsh
  git diff --ignore-cr-at-eol spikes/binding-generators/clangsharp/src/Janset.SDL2.Core/Generated/ spikes/binding-generators/clangsharp/src/Janset.SDL2.Image/Generated/
  ```

  Expected: empty diff. If any structural change (not CRLF), STOP — orchestrator refactor introduced a regression; investigate.

### Task 2.13: `--family ttf` error path (TDD — failing test → friendly error → passing test)

**Files:**

- Modify: `spikes/binding-generators/clangsharp/generate_bindings.py` (`run_self_tests` + `read_header_list`)

This task delivers spec §6 success criterion #5: "`--family ttf` exits with `generation_exit_code` 2 or 4 (missing RSP/headers/project support) — not a Python KeyError or unhandled exception." Per the plan preamble's TDD discipline, this is a failing-test → implement → passing-test sequence (not a manual probe).

- [ ] **Step 2.13.1: Add failing test asserting the friendly error message**

  Append to `run_self_tests` (after the existing assertions added by Task 2.1):

  ```python
      # Spec §6 #5: --family ttf must exit with a clear error pointing at the missing
      # production header list, NOT a KeyError or generic FileNotFoundError. Probes read_header_list
      # directly to keep the test in-process (avoids subprocess overhead).
      try:
          read_header_list(scope_root / "sdl2-ttf.headers.txt")
          failures.append("read_header_list did not raise for missing sdl2-ttf.headers.txt")
      except FileNotFoundError as exc:
          message = str(exc)
          if "For new families" not in message:
              failures.append(
                  f"read_header_list FileNotFoundError lacks 'For new families' hint; got: {message}"
              )
          if "spikes/binding-generators/scope" not in message.replace("\\", "/"):
              failures.append(
                  f"read_header_list FileNotFoundError lacks scope directory hint; got: {message}"
              )
      except KeyError as exc:
          failures.append(f"read_header_list raised KeyError instead of friendly FileNotFoundError: {exc}")
      except Exception as exc:
          failures.append(f"read_header_list raised unexpected exception type {type(exc).__name__}: {exc}")
  ```

  Where `scope_root` is the variable already in scope inside `run_self_tests` (resolved from `find_repository_root() / "spikes" / "binding-generators" / "scope"`). If `scope_root` is not yet in scope, derive it at the top of the new block:

  ```python
      scope_root = find_repository_root() / "spikes" / "binding-generators" / "scope"
  ```

- [ ] **Step 2.13.2: Run self-test, verify it FAILS**

  ```pwsh
  python spikes/binding-generators/clangsharp/generate_bindings.py --self-test
  ```

  Expected failure line:

  ```
  self-test: FAIL: read_header_list FileNotFoundError lacks 'For new families' hint; got: [Errno 2] No such file or directory: '...sdl2-ttf.headers.txt'
  ```

  This is the bare BCL `FileNotFoundError` message — clear about WHAT is missing but not WHY a fresh agent would expect it.

- [ ] **Step 2.13.3: Implement the friendly error in `read_header_list`**

  At `generate_bindings.py:119-126`, replace `read_header_list`:

  ```python
  def read_header_list(header_list_file: pathlib.Path) -> list[str]:
      if not header_list_file.is_file():
          raise FileNotFoundError(
              f"Header list file not found: {header_list_file}. "
              f"For new families, create the header list file in spikes/binding-generators/scope/ "
              f"(see Items 3/4/5 in spikes/binding-generators/docs/satellite-expansion-roadmap.md "
              f"for examples)."
          )
      headers: list[str] = []
      for line in header_list_file.read_text(encoding="utf-8").splitlines():
          stripped = line.strip()
          if not stripped or stripped.startswith("#"):
              continue
          headers.append(stripped)
      return headers
  ```

- [ ] **Step 2.13.4: Run self-test, verify it PASSES**

  ```pwsh
  python spikes/binding-generators/clangsharp/generate_bindings.py --self-test
  ```

  Expected: `self-test: PASS`. The test from Step 2.13.1 now asserts both substrings present in the FileNotFoundError message.

- [ ] **Step 2.13.5: End-to-end probe (verification only)**

  ```pwsh
  python spikes/binding-generators/clangsharp/generate_bindings.py --family ttf 2>&1
  ```

  Expected exit code: non-zero. Expected combined output contains: `Header list file not found:` AND `For new families` AND `spikes/binding-generators/scope/`. Spec §6 #5 satisfied.

### Task 2.14: Common: Determinism Contract Verification

See "Per-slice common requirements" §Common: Determinism Contract Verification — execute all 5 steps (D.1 through D.5).

Note: D.5 cross-family handle pull check should still pass — no change to opaque rewriter in this slice. If D.5 fails, the previous postprocess pipeline had latent dependency on hardcoded family iteration; investigate.

### Task 2.15: Common: Slopwatch

See "Per-slice common requirements" §Common: Slopwatch. No file deletions in this slice — baseline rebuild not required; run analyze only.

### Task 2.16: Commit

- [ ] **Step 2.16.1: Stage changes**

  ```pwsh
  git status --short spikes/binding-generators/clangsharp/generate_bindings.py
  ```

  Expected: `M  spikes/binding-generators/clangsharp/generate_bindings.py` (single file modified).

- [ ] **Step 2.16.2: Commit**

  ```bash
  git commit -m "$(cat <<'EOF'
  refactor(spike): make generate_bindings.py family-agnostic (S1-2)

  - Add ttf/mixer/gfx entries to FAMILY_CONFIG (dormant in --family all per
    Item 1 spec §5.1).
  - Extend PLATFORM_SENSITIVE_HEADERS, --family CLI choices.
  - Refactor stats dict + write_report loop to enumerate selected families
    dynamically instead of hardcoding [core, image].
  - Owner-mode wiring: family in (core, ttf, mixer) -> owner (Items 4/5 inert
    until they activate their family in selected_families("all")).
  - Self-tests cover new family entries + dormant policy + --family ttf error
    path (clear FileNotFoundError, not KeyError).

  Determinism contract preserved: --family all output byte-identical to HEAD.
  Refs: spec §5.1, §5.5, §6 #5; Constitution §"Generation Determinism Contract".
  EOF
  )"
  ```

**Slice exit evidence:**

- ✓ Self-test PASS.
- ✓ `--family all` byte-identical to HEAD (D.1, D.4).
- ✓ Family isolation preserved (D.2, D.3).
- ✓ Cross-family handle pull still works (D.5).
- ✓ `--family ttf` exits with clear FileNotFoundError.
- ✓ Slopwatch clean.
- ✓ Single commit.

---

## S1-3 — Oracle multi-family extension

**Goal:** `oracle.cs` accepts all 5 family IDs and produces a 5-family evidence report. No new `RawAbiChecks` logic — purely data + array extension. TTF/Mixer/GFX rows show `SourceStatus.Missing` until their families are generated by Items 3/4/5.

**Spec sections:** §5.6 (Oracle multi-family extension).
**Constitution sections:** §"Generation Determinism Contract" §Family Isolation (oracle reports per-family without coupling).
**Code anchors:**

- `oracle.cs:160-183` — `FamilyConfigs` static records.
- `oracle.cs:187-191` — `OracleRunner.KnownFamilies` array.

### Task 3.1: Add failing self-test for the 3 new family records

**Files:**

- Modify: `spikes/binding-generators/clangsharp/oracle.cs` (extend `SelfTests.Run` around L337)

- [ ] **Step 3.1.1: Add new test assertions**

  Inside `SelfTests.Run`, after the existing `imageEvidence` assertion block (around L416), append:

  ```csharp
          // New families (ttf/mixer/gfx) must be present in FamilyConfigs + KnownFamilies
          // per Item 1 spec §5.6. OracleOptions.Parse accepts any --family string (it doesn't
          // validate against KnownFamilies), so the dispatch check below (OracleRunner.Run)
          // is what proves the family ID resolves correctly. A bare parse-error check would
          // pass even for nonsense IDs and add no signal.
          var newFamilyIds = new[] { "sdl2-ttf", "sdl2-mixer", "sdl2-gfx" };
          foreach (var id in newFamilyIds)
          {
              // Dispatch check: family ID must resolve in OracleRunner.KnownFamilies array.
              // This is the meaningful assertion — the parse step is a no-op for IDs.
              var dispatchOptions = OracleOptions.Parse(["--family", id]);
              if (dispatchOptions.ParseError is not null)
              {
                  failures.Add($"oracle --family {id} unexpectedly produced a parse error: {dispatchOptions.ParseError}");
                  continue;
              }
              // Verify the ID is in KnownFamilies via reflection (KnownFamilies is private).
              var knownFamiliesField = typeof(OracleRunner).GetField("KnownFamilies",
                  System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
              if (knownFamiliesField?.GetValue(null) is FamilyConfig[] knownFamilies)
              {
                  if (!knownFamilies.Any(c => c.FamilyId == id))
                  {
                      failures.Add($"OracleRunner.KnownFamilies missing {id} entry");
                  }
              }
              else
              {
                  failures.Add("OracleRunner.KnownFamilies reflection probe failed");
              }
          }

          // Expected namespace / raw class identity per Constitution §"Family Identity"
          var expectedIdentity = new (string FamilyId, string Namespace, string RawClass)[]
          {
              ("sdl2-ttf",   "SDL2.Ttf",   "SDL_ttfNative"),
              ("sdl2-mixer", "SDL2.Mixer", "SDL_mixerNative"),
              ("sdl2-gfx",   "SDL2.Gfx",   "SDL2_gfxNative"),
          };
          foreach (var (familyId, expectedNs, expectedRaw) in expectedIdentity)
          {
              var config = typeof(FamilyConfigs)
                  .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
                  .Select(f => f.GetValue(null) as FamilyConfig)
                  .FirstOrDefault(c => c != null && c.FamilyId == familyId);
              if (config is null)
              {
                  failures.Add($"FamilyConfigs missing static record for {familyId}");
                  continue;
              }
              if (config.ExpectedNamespace != expectedNs)
                  failures.Add($"FamilyConfigs.{familyId} namespace: expected {expectedNs}, got {config.ExpectedNamespace}");
              if (config.ExpectedRawClassName != expectedRaw)
                  failures.Add($"FamilyConfigs.{familyId} raw class: expected {expectedRaw}, got {config.ExpectedRawClassName}");
              if (config.UsesSdl2Dynapi)
                  failures.Add($"FamilyConfigs.{familyId} should NOT consume sdl2 dynapi (satellite)");
          }
  ```

- [ ] **Step 3.1.2: Run self-test, verify it FAILS**

  ```pwsh
  dotnet run --file spikes/binding-generators/clangsharp/oracle.cs -- --self-test
  ```

  Expected:

  ```
  self-test: FAIL
  - FamilyConfigs missing static record for sdl2-ttf
  - FamilyConfigs missing static record for sdl2-mixer
  - FamilyConfigs missing static record for sdl2-gfx
  ```

  Exit code 1.

### Task 3.2: Add Sdl2Ttf FamilyConfig record

**Files:**

- Modify: `spikes/binding-generators/clangsharp/oracle.cs:160-183` (`FamilyConfigs` static class)

- [ ] **Step 3.2.1: Add Sdl2Ttf static field**

  Inside `internal static class FamilyConfigs`, after the existing `Sdl2Image` field (L173-182), append:

  ```csharp
      public static readonly FamilyConfig Sdl2Ttf = new(
          "sdl2-ttf",
          "SDL2 TTF",
          "SDL2.Ttf",
          "SDL_ttfNative",
          null,                                                              // no Cake preview
          "spikes/binding-generators/clangsharp/src/Janset.SDL2.Ttf/Generated/Compat",
          "spikes/binding-generators/clangsharp/src/Janset.SDL2.Ttf/Generated/Modern",
          "external/sdl2-cs/src/SDL2_ttf.cs",
          false);                                                             // no Sdl2Dynapi
  ```

### Task 3.3: Add Sdl2Mixer FamilyConfig record

**Files:**

- Modify: `spikes/binding-generators/clangsharp/oracle.cs:160-183`

- [ ] **Step 3.3.1: Add Sdl2Mixer static field**

  After `Sdl2Ttf`, append:

  ```csharp
      public static readonly FamilyConfig Sdl2Mixer = new(
          "sdl2-mixer",
          "SDL2 Mixer",
          "SDL2.Mixer",
          "SDL_mixerNative",
          null,
          "spikes/binding-generators/clangsharp/src/Janset.SDL2.Mixer/Generated/Compat",
          "spikes/binding-generators/clangsharp/src/Janset.SDL2.Mixer/Generated/Modern",
          "external/sdl2-cs/src/SDL2_mixer.cs",
          false);
  ```

### Task 3.4: Add Sdl2Gfx FamilyConfig record

**Files:**

- Modify: `spikes/binding-generators/clangsharp/oracle.cs:160-183`

- [ ] **Step 3.4.1: Add Sdl2Gfx static field**

  After `Sdl2Mixer`, append:

  ```csharp
      public static readonly FamilyConfig Sdl2Gfx = new(
          "sdl2-gfx",
          "SDL2 GFX",
          "SDL2.Gfx",
          "SDL2_gfxNative",
          null,
          "spikes/binding-generators/clangsharp/src/Janset.SDL2.Gfx/Generated/Compat",
          "spikes/binding-generators/clangsharp/src/Janset.SDL2.Gfx/Generated/Modern",
          "external/sdl2-cs/src/SDL2_gfx.cs",
          false);
  ```

### Task 3.5: Extend KnownFamilies array

**Files:**

- Modify: `spikes/binding-generators/clangsharp/oracle.cs:187-191` (`OracleRunner.KnownFamilies`)

- [ ] **Step 3.5.1: Add new families to the array**

  Replace:

  ```csharp
      private static readonly FamilyConfig[] KnownFamilies =
      [
          FamilyConfigs.Sdl2Core,
          FamilyConfigs.Sdl2Image
      ];
  ```

  with:

  ```csharp
      private static readonly FamilyConfig[] KnownFamilies =
      [
          FamilyConfigs.Sdl2Core,
          FamilyConfigs.Sdl2Image,
          FamilyConfigs.Sdl2Ttf,
          FamilyConfigs.Sdl2Mixer,
          FamilyConfigs.Sdl2Gfx,
      ];
  ```

### Task 3.6: Run self-test, verify it now PASSES

- [ ] **Step 3.6.1: Run the self-test**

  ```pwsh
  dotnet run --file spikes/binding-generators/clangsharp/oracle.cs -- --self-test
  ```

  Expected:

  ```
  self-test: PASS
  ```

  Exit code 0.

### Task 3.7: Produce 5-family evidence report

**Files:** none modified (verification only).

- [ ] **Step 3.7.1: Run oracle for all 5 families with --write-report**

  ```pwsh
  dotnet run --file spikes/binding-generators/clangsharp/oracle.cs -- --family sdl2-core --family sdl2-image --family sdl2-ttf --family sdl2-mixer --family sdl2-gfx --write-report
  ```

  Expected: report written to `spikes/binding-generators/output/reports/oracle-evidence-clangsharp.md`.

- [ ] **Step 3.7.2: Verify report contents — Core + Image rows**

  Open the report file. Verify:
  - sdl2-core section: 0 findings under §"Raw ABI Constitution Checks" for `platform-sensitive-wchar`, `platform-sensitive-long`, `deferred-layout-sdl-rwops`, `deferred-layout-sdl-syswminfo`, `deferred-layout-sdl-syswmmsg`, `duplicate-tag-typedef`. No Hard Bug section.
  - sdl2-image section: same 0 findings.

- [ ] **Step 3.7.3: Verify report contents — TTF/Mixer/GFX rows**

  Verify:
  - sdl2-ttf, sdl2-mixer, sdl2-gfx sections present.
  - ClangSharp Compat + ClangSharp Modern sources show `Status: missing`, `Functions: 0`, `Constants: 0`, `Types: 0`.
  - SDL2-CS source rows load existing peer evidence where the source exists, including `external/sdl2-cs/src/SDL2_gfx.cs`.
  - No unhandled-exception or `OutOfScope` entries.
  - No `family-namespace-drift` findings.

### Task 3.8: Common: Determinism Contract Verification

See "Per-slice common requirements" §Common: Determinism Contract Verification — execute D.1 through D.5. Generated `.g.cs` output untouched by this slice, so the contract verification is a sanity check, not a meaningful regression test for this slice's changes.

### Task 3.9: Common: Slopwatch

See "Per-slice common requirements" §Common: Slopwatch. No file deletions in this slice — analyze only, baseline rebuild not required.

### Task 3.10: Commit

- [ ] **Step 3.10.1: Stage and review**

  ```pwsh
  git status --short spikes/binding-generators/clangsharp/oracle.cs
  ```

  Expected: `M  spikes/binding-generators/clangsharp/oracle.cs`.

  Also expect a modified report file:

  ```pwsh
  git status --short spikes/binding-generators/output/reports/oracle-evidence-clangsharp.md
  ```

  Expected: `M` for the report file (5-family rows added).

- [ ] **Step 3.10.2: Commit**

  ```bash
  git commit -m "$(cat <<'EOF'
  feat(spike): extend oracle.cs FamilyConfigs to 5-family report (S1-3)

  - Add Sdl2Ttf, Sdl2Mixer, Sdl2Gfx FamilyConfig static records.
  - Extend OracleRunner.KnownFamilies to 5 entries.
  - Satellite rows show SourceStatus.Missing for ClangSharp Compat/Modern
    paths (no generation yet — pending Items 3/4/5).
  - Self-tests cover the 3 new family IDs, namespace/raw-class identity,
    and Sdl2Dynapi=false invariant.
  - Core + Image: 0 findings across all Priority C categories (regression-free).

  Refs: spec §5.6, Constitution §"Family Identity".
  EOF
  )"
  ```

**Slice exit evidence:**

- ✓ Self-test PASS with 3 new family-identity assertions.
- ✓ 5-family report produced; Core + Image clean; TTF/Mixer/GFX SourceStatus.Missing as expected.
- ✓ No regression on Priority C category findings.
- ✓ Slopwatch clean.
- ✓ Single commit.

---

## S1-4 — OpaqueHandleEmitRewriter family-blind + roster family-keyed migration

**Goal:** Migrate `policy/opaque-handle-roster.json` from Core-only flat schema → family-keyed schema 2.0. `DiscoverAutoDetectedHandles` drops the `StartsWith("SDL_")` filter. `BuildHandlesFileContent` accepts a namespace parameter. `LoadRoster` and `ReportDrift` take a family parameter; `LoadRoster` cross-family-pulls Core entries for satellite consumers. New `--handles-namespace` CLI flag plumbs family namespace through `Program.cs` → `UniformOpaqueOwnerMode` → `BuildHandlesFileContent`. Core regen produces byte-identical `Handles.g.cs` (Core's existing 14 + 3 + 12 entries preserved verbatim in the migration; satellite consumer-mode unchanged).

**Spec sections:** §5.2 (Family-blind opaque auto-detection — family-keyed roster).
**Constitution sections:** §"Opaque Handles" Auto-detect criterion + Canonical roster + Cross-family handle name resolution + §"Generation Determinism Contract" (byte preservation of Core entries; cross-family handle pull contract).

**Code anchors:**

- `OpaqueHandleEmitRewriter.cs:200, 211, 220, 230` — 4 prefix filters (one in empty-struct scan, three in pointer-use scans).
- `OpaqueHandleEmitRewriter.cs:253-279` — `LoadRoster` current Core-only signature.
- `OpaqueHandleEmitRewriter.cs:291-313` — `ReportDrift` current implementation.
- `OpaqueHandleEmitRewriter.cs:324-352` — `BuildHandlesFileContent` hardcoded `namespace SDL2`.
- `Program.cs:126-157` — `uniform-opaque` mode case (calls `LoadRoster`, `DiscoverAutoDetectedHandles`, `ReportDrift`).
- `UniformOpaqueOwnerMode.cs:86-110` — `EmitConsolidatedHandlesFileIfOwner` signature.
- `generate_bindings.py:1267-1271` — owner-mode invocation `extra_args`.
- `policy/opaque-handle-roster.json` — current Core-only flat schema (needs migration to schema 2.0).

**Code anchors:**

- `OpaqueHandleEmitRewriter.cs:186-247` — `DiscoverAutoDetectedHandles` with 4 `StartsWith("SDL_")` filters at L200, L211, L220, L230.
- `OpaqueHandleEmitRewriter.cs:253-279` — `LoadRoster` (current Core-only flat-schema signature).
- `OpaqueHandleEmitRewriter.cs:291-313` — `ReportDrift` (current implementation).
- `OpaqueHandleEmitRewriter.cs:324-377` — `BuildHandlesFileContent` (hardcoded `namespace SDL2` at L336).
- `Program.cs:126-157` — `uniform-opaque` mode case (calls `LoadRoster`, `DiscoverAutoDetectedHandles`, `ReportDrift`).
- `UniformOpaqueOwnerMode.cs:86-110` — `EmitConsolidatedHandlesFileIfOwner` (needs `namespaceName` param).
- `generate_bindings.py:1263-1274` — owner-mode invocation `extra_args` (needs `--handles-namespace`).
- `policy/opaque-handle-roster.json` — current Core-only flat schema (needs migration to schema 2.0).

### Task 4.1: Take a backup of the current roster JSON (byte-preservation reference)

**Files:**

- Create (temp, not committed): `spikes/binding-generators/clangsharp/policy/opaque-handle-roster.v1.backup.json`

- [ ] **Step 4.1.1: Copy current roster as backup**

  ```pwsh
  Copy-Item -LiteralPath "spikes/binding-generators/clangsharp/policy/opaque-handle-roster.json" -Destination "spikes/binding-generators/clangsharp/policy/opaque-handle-roster.v1.backup.json"
  ```

  Expected: file copied. **This backup is NOT committed** — it serves only as a byte-preservation reference for Task 4.3.

- [ ] **Step 4.1.2: Add the backup file to .gitignore for the duration of this slice**

  ```pwsh
  Add-Content -Path "spikes/binding-generators/clangsharp/policy/.gitignore" -Value "opaque-handle-roster.v1.backup.json"
  ```

  (Create `.gitignore` in `policy/` if it does not exist.)

### Task 4.2: Migrate roster JSON to family-keyed schema 2.0

**Files:**

- Modify: `spikes/binding-generators/clangsharp/policy/opaque-handle-roster.json`

**⚠ Sequencing warning.** Between Task 4.2 (JSON migrated to schema 2.0) and Task 4.5 (LoadRoster updated to parse schema 2.0), the legacy `LoadRoster` reads `root.GetProperty("auto_detect_well_known")` at the file root — but the migrated JSON has that field under `root.families.core` instead. **Do NOT run `python generate_bindings.py --family all --execute` between Task 4.2 and Task 4.5** — any postprocess invocation during this window crashes with `KeyNotFoundException`. Treat Tasks 4.2 → 4.5 as a single non-divisible working-tree edit (two commits per Task 4.16, but no `--execute` runs in between).

- [ ] **Step 4.2.1: Migrate the schema**

  The current roster has top-level fields: `schema_version`, `sdl2_version`, `source`, `last_audited`, `audit_method`, `notes`, `auto_detect_well_known`, `force_opaque_exceptions`, `excluded_candidates`. Transform into the family-keyed schema 2.0 below.

  Replace the entire file content with:

  ```json
  {
    "schema_version": "2.0",
    "last_audited": "2026-05-25",
    "audit_method": "Three-source triangulation per family: family-pinned release headers (forward-decl evidence), wiki / project documentation pages (opacity phrasing where present), and ClangSharp Modern output (empty-struct emit). All three sources must agree before a name enters auto_detect_well_known; wiki evidence may be 'not_found' when sources 1 and 3 agree.",
    "families": {
      "core": {
        "library_version": "2.32.10",
        "source": "SDL2 release headers (vcpkg_installed/x64-windows-hybrid/include/SDL2/) + wiki.libsdl.org cross-validation + ClangSharp Modern output empty-struct verification",
        "auto_detect_well_known": [ <PASTE the existing 14 entries from the v1 backup verbatim, byte-preserved> ],
        "force_opaque_exceptions": [ <PASTE the existing 3 entries verbatim> ],
        "excluded_candidates": [ <PASTE the existing 12 entries verbatim> ]
      },
      "image": {
        "library_version": "2.8.8",
        "auto_detect_well_known": [],
        "force_opaque_exceptions": [],
        "excluded_candidates": []
      },
      "ttf": {
        "library_version": "2.24.0",
        "auto_detect_well_known": [
          {
            "name": "TTF_Font",
            "header": "SDL_ttf.h",
            "header_decl": "typedef struct TTF_Font TTF_Font;",
            "wiki_url": "https://wiki.libsdl.org/SDL2_ttf/TTF_Font",
            "wiki_evidence": "Opaque structure representing an open TTF font. (Consult the page during execution and paste the exact phrasing; record 'not_found' if the page does not exist at the audit date.)",
            "header_note": "Satellite-owned Pattern B handle. Emitted into namespace SDL2.Ttf by uniform-opaque owner-mode."
          }
        ],
        "force_opaque_exceptions": [],
        "excluded_candidates": []
      },
      "mixer": {
        "library_version": "2.8.1",
        "auto_detect_well_known": [
          {
            "name": "Mix_Music",
            "header": "SDL_mixer.h",
            "header_decl": "typedef struct Mix_Music Mix_Music;",
            "wiki_url": "https://wiki.libsdl.org/SDL2_mixer/Mix_Music",
            "wiki_evidence": "Opaque type. (Consult page during execution; record 'not_found' if 404.)",
            "header_note": "Satellite-owned Pattern B handle. Emitted into namespace SDL2.Mixer by uniform-opaque owner-mode."
          }
        ],
        "force_opaque_exceptions": [],
        "excluded_candidates": []
      },
      "gfx": {
        "library_version": "1.0.4",
        "auto_detect_well_known": [],
        "force_opaque_exceptions": [],
        "excluded_candidates": []
      }
    }
  }
  ```

  **Critical:** the `<PASTE ... verbatim>` placeholders MUST be replaced with byte-identical copies from the v1 backup. JSON indentation and key ordering inside each entry must match the backup. This preserves Core regen byte-equivalence (since the rewriter sees the same handle names in the same order).

- [ ] **Step 4.2.2: Validate the migrated JSON**

  ```pwsh
  python -c "import json; json.load(open('spikes/binding-generators/clangsharp/policy/opaque-handle-roster.json'))"
  ```

  Expected: no output (valid JSON parses successfully). If `json.JSONDecodeError`: fix syntax, repeat.

### Task 4.3: Verify Core entries are byte-preserved in migration

**Files:** none modified (verification only).

- [ ] **Step 4.3.1: Extract Core entries from v2 schema**

  ```pwsh
  python -c "
  import json
  v2 = json.load(open('spikes/binding-generators/clangsharp/policy/opaque-handle-roster.json'))
  core = v2['families']['core']
  print(json.dumps(core['auto_detect_well_known'], indent=2))
  "
  ```

  Expected output: 14 entries matching the v1 backup's `auto_detect_well_known` byte-for-byte.

- [ ] **Step 4.3.2: Extract from v1 backup**

  ```pwsh
  python -c "
  import json
  v1 = json.load(open('spikes/binding-generators/clangsharp/policy/opaque-handle-roster.v1.backup.json'))
  print(json.dumps(v1['auto_detect_well_known'], indent=2))
  "
  ```

  Compare the two outputs character-by-character. They MUST match. If any difference: migration introduced byte-skew; revert and re-migrate carefully.

  Repeat for `force_opaque_exceptions` (3 entries) and `excluded_candidates` (12 entries).

### Task 4.4: Add failing test for LoadRoster family parameter

**Files:**

- Modify: `spikes/binding-generators/clangsharp/postprocess/Program.cs` OR create a dedicated test fixture file under `postprocess/Tests/`.

The postprocess project has no current self-test harness for `OpaqueHandleEmitRewriter`. Two options:

**Option A (preferred):** add a `--self-test` CLI mode to `Program.cs` that runs in-process assertions and exits with code 0/1. This mirrors `generate_bindings.py --self-test`.

**Option B:** add a TUnit test project under `postprocess/Janset.SDL2.PostProcess.Tests.csproj`. Higher ceremony; defer to a separate slice if blocked.

Use Option A.

- [ ] **Step 4.4.1: Add `--self-test` mode to `Program.cs`**

  **Insertion point:** immediately after the using statements + leading comment block (currently ending around L47, before the existing usage guard at L49 `if (args.Length < 2 || args[0] is not (...))`). The `--self-test` check MUST come BEFORE this usage guard — otherwise the single-arg `--self-test` invocation trips the `args.Length < 2` branch, prints the usage banner, and returns 1 before the self-test can run.

  Add:

  ```csharp
  if (args.Length == 1 && args[0] == "--self-test")
  {
      return PostProcessSelfTests.Run();
  }
  ```

  Then the existing usage guard at L49 continues as-is — `--self-test` falls through it because it has already returned.

- [ ] **Step 4.4.2: Create `postprocess/PostProcessSelfTests.cs`**

  New file. **Critical:** include the full Roslyn using set up-front. Later slices (S1-5 Task 5.5 and S1-6 Task 6.2) append fixtures that use `CSharpSyntaxTree.ParseText(...)`, `(CompilationUnitSyntax)`, `CSharpSyntaxRewriter`, and `Regex.Matches(...)` — none of which are covered by `ImplicitUsings=enable` in the postprocess csproj. Pre-loading the usings prevents misleading "unknown type" compile errors masquerading as TDD red phase.

  ```csharp
  using System.Text.Json;
  using System.Text.RegularExpressions;
  using Janset.SDL2.PostProcess;
  using Microsoft.CodeAnalysis;
  using Microsoft.CodeAnalysis.CSharp;
  using Microsoft.CodeAnalysis.CSharp.Syntax;

  internal static class PostProcessSelfTests
  {
      public static int Run()
      {
          var failures = new List<string>();
          var rosterPath = ResolveRosterPath();

          // Test 1: LoadRoster("core") returns 14 auto-detect + 3 force-opaque
          var (coreAuto, coreForce) = OpaqueHandleEmitRewriter.LoadRoster(rosterPath, "core");
          if (coreAuto.Count != 14)
              failures.Add($"LoadRoster('core') auto-detect count: expected 14, got {coreAuto.Count}");
          if (coreForce.Count != 3)
              failures.Add($"LoadRoster('core') force-opaque count: expected 3, got {coreForce.Count}");
          if (!coreAuto.Contains("SDL_Window"))
              failures.Add("LoadRoster('core') missing SDL_Window");
          if (!coreForce.Contains("SDL_RWops"))
              failures.Add("LoadRoster('core') missing SDL_RWops");

          // Test 2: LoadRoster("image") cross-family pull — must contain Core's 14 + 3
          var (imageAuto, imageForce) = OpaqueHandleEmitRewriter.LoadRoster(rosterPath, "image");
          if (!imageAuto.Contains("SDL_Renderer"))
              failures.Add("LoadRoster('image') cross-family pull missing SDL_Renderer");
          if (!imageForce.Contains("SDL_RWops"))
              failures.Add("LoadRoster('image') cross-family pull missing SDL_RWops");

          // Test 3: LoadRoster("ttf") contains Core handles AND TTF_Font
          var (ttfAuto, ttfForce) = OpaqueHandleEmitRewriter.LoadRoster(rosterPath, "ttf");
          if (!ttfAuto.Contains("TTF_Font"))
              failures.Add("LoadRoster('ttf') missing TTF_Font");
          if (!ttfAuto.Contains("SDL_Renderer"))
              failures.Add("LoadRoster('ttf') cross-family pull missing SDL_Renderer");

          // Test 4: LoadRoster("mixer") contains Mix_Music
          var (mixerAuto, _) = OpaqueHandleEmitRewriter.LoadRoster(rosterPath, "mixer");
          if (!mixerAuto.Contains("Mix_Music"))
              failures.Add("LoadRoster('mixer') missing Mix_Music");

          if (failures.Count > 0)
          {
              Console.Error.WriteLine("postprocess self-test: FAIL");
              foreach (var f in failures) Console.Error.WriteLine($"  - {f}");
              return 1;
          }
          Console.WriteLine("postprocess self-test: PASS");
          return 0;
      }

      private static string ResolveRosterPath()
      {
          var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
          while (dir != null)
          {
              var candidate = Path.Combine(dir.FullName, "spikes", "binding-generators", "clangsharp", "policy", "opaque-handle-roster.json");
              if (File.Exists(candidate)) return candidate;
              dir = dir.Parent;
          }
          throw new FileNotFoundException("Could not locate opaque-handle-roster.json by walking ancestors.");
      }
  }
  ```

- [ ] **Step 4.4.3: Run the self-test, verify it FAILS with expected errors**

  ```pwsh
  dotnet run --project spikes/binding-generators/clangsharp/postprocess/Janset.SDL2.PostProcess.csproj -c Release -- --self-test
  ```

  Expected: build error (since `LoadRoster(rosterPath, family)` 2-arg signature doesn't exist yet — current sig is 1-arg). Compile error message resembles:

  ```
  error CS1501: No overload for method 'LoadRoster' takes 2 arguments
  ```

  This proves the test is in place and asserts on the new signature.

### Task 4.5: Update LoadRoster signature for family parameter

**Files:**

- Modify: `spikes/binding-generators/clangsharp/postprocess/OpaqueHandleEmitRewriter.cs:253-279`

- [ ] **Step 4.5.1: Replace LoadRoster method**

  Replace the current `LoadRoster` (L253-279) with the family-keyed version implementing cross-family pull:

  ```csharp
  /// <summary>
  /// Parse the canonical family-keyed opaque-handle roster JSON. Returns the
  /// <c>auto_detect_well_known</c> and <c>force_opaque_exceptions</c> name sets
  /// for the requested family.
  ///
  /// **Cross-family handle name resolution (Constitution §"Opaque Handles"):** when
  /// <paramref name="family"/> is not "core", additionally pull Core's
  /// <c>auto_detect_well_known</c> and <c>force_opaque_exceptions</c> into the loaded
  /// sets. Pattern B's uniform by-value semantic at every raw ABI position is
  /// preserved regardless of which family owns the handle. The pull is data-only;
  /// satellite postprocess execution does not read Core's .g.cs files.
  /// </summary>
  public static (HashSet<string> AutoDetect, HashSet<string> ForceOpaque) LoadRoster(string rosterPath, string family)
  {
      if (!File.Exists(rosterPath))
      {
          throw new FileNotFoundException(
              $"Opaque-handle roster not found at: {rosterPath}. " +
              "Expected at <repo>/spikes/binding-generators/clangsharp/policy/opaque-handle-roster.json.",
              rosterPath);
      }

      using var doc = JsonDocument.Parse(File.ReadAllText(rosterPath));
      var root = doc.RootElement;
      var families = root.GetProperty("families");

      var autoDetect = new HashSet<string>(StringComparer.Ordinal);
      var forceOpaque = new HashSet<string>(StringComparer.Ordinal);

      AppendFamilyEntries(families.GetProperty(family), autoDetect, forceOpaque);

      // Cross-family resolution: satellites also see Core's handle name set.
      // See Constitution §"Opaque Handles" Cross-family handle name resolution.
      if (!family.Equals("core", StringComparison.Ordinal))
      {
          AppendFamilyEntries(families.GetProperty("core"), autoDetect, forceOpaque);
      }

      return (autoDetect, forceOpaque);
  }

  private static void AppendFamilyEntries(
      JsonElement familyEntry,
      HashSet<string> autoDetect,
      HashSet<string> forceOpaque)
  {
      foreach (var entry in familyEntry.GetProperty("auto_detect_well_known").EnumerateArray())
      {
          autoDetect.Add(entry.GetProperty("name").GetString()!);
      }
      foreach (var entry in familyEntry.GetProperty("force_opaque_exceptions").EnumerateArray())
      {
          forceOpaque.Add(entry.GetProperty("name").GetString()!);
      }
  }
  ```

- [ ] **Step 4.5.2: Build the postprocess project**

  ```pwsh
  dotnet build spikes/binding-generators/clangsharp/postprocess/Janset.SDL2.PostProcess.csproj -c Release
  ```

  Expected: 0 errors. There may be compile errors at the call site in `Program.cs:130` (`LoadRoster(rosterPath)` — old 1-arg call). Step 4.5.3 fixes the call site.

- [ ] **Step 4.5.3: Update Program.cs call site temporarily for build**

  At `Program.cs:130`, the current line is:

  ```csharp
          var (rosterAutoDetect, rosterForceOpaque) = OpaqueHandleEmitRewriter.LoadRoster(rosterPath);
  ```

  Replace with a temporary call passing `"core"` literally (will be replaced with family-detection logic in Task 4.10):

  ```csharp
          // TEMP (Tasks 4.5–4.10 transitional): hardcoded family. Replaced by
          // family-detection from output directory in Task 4.10.2.
          var (rosterAutoDetect, rosterForceOpaque) = OpaqueHandleEmitRewriter.LoadRoster(rosterPath, "core");
  ```

  Rebuild: `dotnet build spikes/binding-generators/clangsharp/postprocess/Janset.SDL2.PostProcess.csproj -c Release`. Expected: clean.

  **⚠ Intermediate state warning — DO NOT regen between Task 4.5 and Task 4.10.** Tasks 4.6 through 4.9 incrementally land the family-blind prefix gate drop, namespace parameterization, ReportDrift signature change, and EmitConsolidatedHandlesFileIfOwner update. During this window:
  - The `uniform-opaque` postprocess is hardcoded to `"core"` family (this Step 4.5.3 temp).
  - If invoked against Image's output, it loads Core's roster instead of pulling cross-family — Image still rewrites `SDL_Renderer*` → `SDL_Renderer` because Core's auto-detect list contains the name; this happens to work for the current Core+Image surface.
  - BUT the LoadRoster contract (cross-family pull when `family != "core"`) is not yet exercised, and D.5 in this intermediate state would falsely PASS without actually testing the pull logic.

  Do NOT run any `generate_bindings.py --family all --execute` invocation or any D.1–D.5 Determinism Contract Verification between Task 4.5 and Task 4.10. Treat Tasks 4.5 → 4.10 as a single working-tree edit. The Common: Determinism Contract Verification at Task 4.14 runs only after Task 4.10 (family resolution) lands.

- [ ] **Step 4.5.4: Run the self-test, verify Tests 1–4 PASS**

  ```pwsh
  dotnet run --project spikes/binding-generators/clangsharp/postprocess/Janset.SDL2.PostProcess.csproj -c Release -- --self-test
  ```

  **Awareness:** Tests 1–4 (from Task 4.4.2) PASS at this point. Tests 5–7 are NOT yet added — they land in Tasks 4.6 (Test 5: TTF_Font discovery), 4.7 (Test 6: BuildHandlesFileContent namespace). Do NOT interpret this PASS as "all tests pass"; it means "the family-keyed LoadRoster signature change works for the 4 tests we have today." The complete failing-test path continues in Tasks 4.6 → 4.8.

  Expected:

  ```
  postprocess self-test: PASS
  ```

### Task 4.6: Drop SDL_ prefix gate in DiscoverAutoDetectedHandles

**Files:**

- Modify: `spikes/binding-generators/clangsharp/postprocess/OpaqueHandleEmitRewriter.cs:186-247`

- [ ] **Step 4.6.1: Add failing test for satellite-owned handle discovery**

  Inside `PostProcessSelfTests.Run` (created in Task 4.4), before the existing failure block, append:

  ```csharp
          // Test 5: DiscoverAutoDetectedHandles must find satellite-owned handle (no SDL_ prefix)
          var tmpDir = Path.Combine(Path.GetTempPath(), "ophe-fixture-" + Guid.NewGuid().ToString("N"));
          Directory.CreateDirectory(tmpDir);
          try
          {
              File.WriteAllText(Path.Combine(tmpDir, "SDL_ttf.g.cs"), """
  namespace SDL2.Ttf
  {
      public partial struct TTF_Font
      {
      }

      internal static unsafe partial class SDL_ttfNative
      {
          public static partial TTF_Font* TTF_OpenFont(byte* file, int ptsize);
      }
  }
  """);
              var discovered = OpaqueHandleEmitRewriter.DiscoverAutoDetectedHandles(tmpDir);
              if (!discovered.Contains("TTF_Font"))
                  failures.Add("DiscoverAutoDetectedHandles missing TTF_Font (no SDL_ prefix — family-blind regression)");
          }
          finally { Directory.Delete(tmpDir, recursive: true); }
  ```

- [ ] **Step 4.6.2: Run self-test, verify TTF_Font test FAILS**

  ```pwsh
  dotnet run --project spikes/binding-generators/clangsharp/postprocess/Janset.SDL2.PostProcess.csproj -c Release -- --self-test
  ```

  Expected failure line: `DiscoverAutoDetectedHandles missing TTF_Font (no SDL_ prefix — family-blind regression)`.

- [ ] **Step 4.6.3: Remove the 4 prefix filters**

  In `OpaqueHandleEmitRewriter.cs`, remove the `StartsWith("SDL_", StringComparison.Ordinal)` clause from these 4 locations:

  - L199-200 (empty-struct scan):

    ```csharp
    if (sd.Members.Count == 0 &&
        sd.Identifier.ValueText.StartsWith("SDL_", StringComparison.Ordinal))
    ```

    becomes:

    ```csharp
    if (sd.Members.Count == 0)
    ```

  - L209-211 (return-type pointer-use scan):

    ```csharp
    if (method.ReturnType is PointerTypeSyntax retPtr &&
        retPtr.ElementType is IdentifierNameSyntax retId &&
        retId.Identifier.ValueText.StartsWith("SDL_", StringComparison.Ordinal))
    ```

    becomes:

    ```csharp
    if (method.ReturnType is PointerTypeSyntax retPtr &&
        retPtr.ElementType is IdentifierNameSyntax retId)
    ```

  - L218-220 (parameter-type pointer-use scan):

    ```csharp
    if (param.Type is PointerTypeSyntax paramPtr &&
        paramPtr.ElementType is IdentifierNameSyntax paramId &&
        paramId.Identifier.ValueText.StartsWith("SDL_", StringComparison.Ordinal))
    ```

    becomes:

    ```csharp
    if (param.Type is PointerTypeSyntax paramPtr &&
        paramPtr.ElementType is IdentifierNameSyntax paramId)
    ```

  - L228-230 (function-pointer parameter scan): same pattern, remove the `StartsWith` clause.

- [ ] **Step 4.6.4: Run self-test, verify it PASSES**

  ```pwsh
  dotnet run --project spikes/binding-generators/clangsharp/postprocess/Janset.SDL2.PostProcess.csproj -c Release -- --self-test
  ```

  Expected: `postprocess self-test: PASS`.

### Task 4.7: Parameterize BuildHandlesFileContent namespace

**Files:**

- Modify: `spikes/binding-generators/clangsharp/postprocess/OpaqueHandleEmitRewriter.cs:324-352`

- [ ] **Step 4.7.1: Add failing test for namespaceName parameter**

  Append to `PostProcessSelfTests.Run`:

  ```csharp
          // Test 6: BuildHandlesFileContent emits the requested namespace
          var content = OpaqueHandleEmitRewriter.BuildHandlesFileContent(new[] { "TTF_Font" }, "SDL2.Ttf");
          if (!content.Contains("namespace SDL2.Ttf"))
              failures.Add("BuildHandlesFileContent did not emit 'namespace SDL2.Ttf' for namespaceName argument");
          if (!content.Contains("public readonly partial struct TTF_Font"))
              failures.Add("BuildHandlesFileContent did not emit Pattern B struct for TTF_Font");
  ```

- [ ] **Step 4.7.2: Run, verify build FAILS (signature mismatch)**

  ```pwsh
  dotnet run --project spikes/binding-generators/clangsharp/postprocess/Janset.SDL2.PostProcess.csproj -c Release -- --self-test
  ```

  Expected: compile error — current `BuildHandlesFileContent` takes only 1 argument.

- [ ] **Step 4.7.3: Update the method signature**

  At `OpaqueHandleEmitRewriter.cs:324`, change:

  ```csharp
  public static string BuildHandlesFileContent(IEnumerable<string> handleNamesSorted)
  ```

  to:

  ```csharp
  public static string BuildHandlesFileContent(IEnumerable<string> handleNamesSorted, string namespaceName)
  ```

  Inside the method, at L336, change:

  ```csharp
  sb.AppendLine("namespace SDL2");
  ```

  to:

  ```csharp
  sb.AppendLine($"namespace {namespaceName}");
  ```

  Update existing callers (if any) to pass `"SDL2"` explicitly for now — Task 4.9 will plumb the family-aware namespace.

- [ ] **Step 4.7.4: Update UniformOpaqueOwnerMode.EmitConsolidatedHandlesFileIfOwner caller temporarily**

  In `UniformOpaqueOwnerMode.cs:101`, update the call site:

  ```csharp
  var content = OpaqueHandleEmitRewriter.BuildHandlesFileContent(sortedNames);
  ```

  becomes (temporarily — Task 4.9 will plumb the real namespace):

  ```csharp
  var content = OpaqueHandleEmitRewriter.BuildHandlesFileContent(sortedNames, "SDL2");
  ```

- [ ] **Step 4.7.5: Run self-test, verify PASS**

  ```pwsh
  dotnet run --project spikes/binding-generators/clangsharp/postprocess/Janset.SDL2.PostProcess.csproj -c Release -- --self-test
  ```

  Expected: `postprocess self-test: PASS`.

### Task 4.8: Update ReportDrift signature for family parameter

**Files:**

- Modify: `spikes/binding-generators/clangsharp/postprocess/OpaqueHandleEmitRewriter.cs:291-313`

- [ ] **Step 4.8.1: Update signature + warning text**

  Replace the current `ReportDrift` (L291-313) with:

  ```csharp
  /// <summary>
  /// Compare the syntactic discovery set against the family's <c>auto_detect_well_known</c>
  /// roster section. On mismatch, write a single stderr warning describing the drift,
  /// citing the family name. Non-fatal — drift surfaces as a build-time warning per
  /// Constitution §"Opaque Handles".
  /// </summary>
  public static void ReportDrift(HashSet<string> syntacticDetect, HashSet<string> rosterAutoDetect, string family)
  {
      if (syntacticDetect.Count == 0)
      {
          // Consumer directories (Image, GFX) have no local empty-struct emission;
          // skip drift reporting to avoid false-positive "in roster but not code" against pulled Core entries.
          return;
      }

      var inCodeNotRoster = syntacticDetect.Except(rosterAutoDetect, StringComparer.Ordinal).ToList();
      var inRosterNotCode = rosterAutoDetect.Except(syntacticDetect, StringComparer.Ordinal).ToList();

      if (inCodeNotRoster.Count > 0 || inRosterNotCode.Count > 0)
      {
          Console.Error.WriteLine($"uniform-opaque: WARNING - auto-detect roster drift detected for family '{family}'.");
          if (inCodeNotRoster.Count > 0)
          {
              Console.Error.WriteLine($"  In code but not roster: {string.Join(", ", inCodeNotRoster.OrderBy(s => s, StringComparer.Ordinal))}");
          }
          if (inRosterNotCode.Count > 0)
          {
              Console.Error.WriteLine($"  In roster but not code: {string.Join(", ", inRosterNotCode.OrderBy(s => s, StringComparer.Ordinal))}");
          }
          Console.Error.WriteLine($"  Resolution: audit spikes/binding-generators/clangsharp/policy/opaque-handle-roster.json families.{family} section and update either the JSON or the auto-detect logic to converge.");
      }
  }
  ```

- [ ] **Step 4.8.2: Update Program.cs call site**

  At `Program.cs:135`, the current call is:

  ```csharp
          OpaqueHandleEmitRewriter.ReportDrift(syntacticDetect, rosterAutoDetect);
  ```

  Replace with a temporary call using `"core"` (will be replaced by family-detection in Task 4.11):

  ```csharp
          // TEMP: hardcoded family — replaced by family-detection in Task 4.11
          OpaqueHandleEmitRewriter.ReportDrift(syntacticDetect, rosterAutoDetect, "core");
  ```

- [ ] **Step 4.8.3: Build, verify clean**

  ```pwsh
  dotnet build spikes/binding-generators/clangsharp/postprocess/Janset.SDL2.PostProcess.csproj -c Release
  ```

  Expected: 0 errors.

### Task 4.9: Update UniformOpaqueOwnerMode.EmitConsolidatedHandlesFileIfOwner signature

**Files:**

- Modify: `spikes/binding-generators/clangsharp/postprocess/UniformOpaqueOwnerMode.cs:86-110`

- [ ] **Step 4.9.1: Add `string namespaceName` parameter**

  Replace the method signature:

  ```csharp
  public static void EmitConsolidatedHandlesFileIfOwner(
      string mode,
      string outputDirectory,
      HashSet<string>? handleNames,
      bool? isOwner)
  ```

  with:

  ```csharp
  public static void EmitConsolidatedHandlesFileIfOwner(
      string mode,
      string outputDirectory,
      HashSet<string>? handleNames,
      bool? isOwner,
      string namespaceName)
  ```

  Inside the method body, update the `BuildHandlesFileContent` call to pass `namespaceName`:

  ```csharp
  var content = OpaqueHandleEmitRewriter.BuildHandlesFileContent(sortedNames, namespaceName);
  ```

  (Replaces the temporary `"SDL2"` literal from Task 4.7.4.)

- [ ] **Step 4.9.2: Update Program.cs call site at the bottom**

  At `Program.cs:203`, the current call is:

  ```csharp
  UniformOpaqueOwnerMode.EmitConsolidatedHandlesFileIfOwner(mode, outputDir, uniformOpaqueHandleNames, uniformOpaqueIsOwner);
  ```

  Replace with:

  ```csharp
  UniformOpaqueOwnerMode.EmitConsolidatedHandlesFileIfOwner(
      mode, outputDir, uniformOpaqueHandleNames, uniformOpaqueIsOwner,
      uniformOpaqueHandlesNamespace ?? "SDL2");
  ```

  **Variable declaration location — critical:** `uniformOpaqueHandlesNamespace` MUST be declared BEFORE the `switch (mode)` statement at the same scope level as `uniformOpaqueHandleNames` (currently at `Program.cs:85`) and `uniformOpaqueIsOwner` (currently at `Program.cs:89`). The `EmitConsolidatedHandlesFileIfOwner` call at `Program.cs:203` is OUTSIDE the switch, so the variable must be in the enclosing scope to be reachable. Declaring it inside the `uniform-opaque` case body scopes it incorrectly and produces a "variable does not exist in current context" compile error at the L203 call site.

  Add immediately after the `uniformOpaqueIsOwner` declaration:

  ```csharp
  // Resolved namespace for satellite-owned Handles.g.cs emission per Item 1 S1-4.
  // Defaults to "SDL2" when --handles-namespace is absent (preserves Core behavior).
  string? uniformOpaqueHandlesNamespace = null;
  ```

  Inside the `uniform-opaque` case body, Task 4.10.1 parses the `--handles-namespace` CLI flag and assigns to this variable.

### Task 4.10: Add --handles-namespace CLI flag to Program.cs

**Files:**

- Modify: `spikes/binding-generators/clangsharp/postprocess/Program.cs` (uniform-opaque case at L126)

- [ ] **Step 4.10.1: Parse the flag**

  Inside the `uniform-opaque` case in `Program.cs`, after the `uniformOpaqueIsOwner = UniformOpaqueOwnerMode.Resolve(args, outputDir);` line, add:

  ```csharp
          // Parse --handles-namespace flag (optional; defaults to SDL2 for backward compat with Core).
          for (int i = 0; i < args.Length - 1; i++)
          {
              if (args[i] == "--handles-namespace")
              {
                  uniformOpaqueHandlesNamespace = args[i + 1];
                  break;
              }
          }
  ```

- [ ] **Step 4.10.2: Detect family from output directory + pass to LoadRoster/ReportDrift**

  Replace the temporary `"core"` literal in the `LoadRoster` and `ReportDrift` calls with family detection:

  ```csharp
          var family = ResolveFamilyFromOutputDir(outputDir);
          var (rosterAutoDetect, rosterForceOpaque) = OpaqueHandleEmitRewriter.LoadRoster(rosterPath, family);

          // Syntactic discovery acts as a watchdog against the roster (the policy authority).
          var syntacticDetect = OpaqueHandleEmitRewriter.DiscoverAutoDetectedHandles(inputDir);
          OpaqueHandleEmitRewriter.ReportDrift(syntacticDetect, rosterAutoDetect, family);
  ```

  Add the helper at the bottom of `Program.cs` (alongside `ResolveOpaqueHandleRosterPath`):

  ```csharp
  static string ResolveFamilyFromOutputDir(string outputDir)
  {
      var normalized = outputDir.Replace('\\', '/');
      if (normalized.Contains("/Janset.SDL2.Core/", StringComparison.OrdinalIgnoreCase)) return "core";
      if (normalized.Contains("/Janset.SDL2.Image/", StringComparison.OrdinalIgnoreCase)) return "image";
      if (normalized.Contains("/Janset.SDL2.Ttf/", StringComparison.OrdinalIgnoreCase)) return "ttf";
      if (normalized.Contains("/Janset.SDL2.Mixer/", StringComparison.OrdinalIgnoreCase)) return "mixer";
      if (normalized.Contains("/Janset.SDL2.Gfx/", StringComparison.OrdinalIgnoreCase)) return "gfx";
      throw new InvalidOperationException(
          $"Could not resolve family from output directory: {outputDir}. " +
          "Expected path segment /Janset.SDL2.{Core,Image,Ttf,Mixer,Gfx}/.");
  }
  ```

- [ ] **Step 4.10.3: Build, run self-test**

  ```pwsh
  dotnet build spikes/binding-generators/clangsharp/postprocess/Janset.SDL2.PostProcess.csproj -c Release
  dotnet run --project spikes/binding-generators/clangsharp/postprocess/Janset.SDL2.PostProcess.csproj -c Release -- --self-test
  ```

  Expected: `postprocess self-test: PASS`.

### Task 4.11: Update generate_bindings.py owner-mode invocation

**Files:**

- Modify: `spikes/binding-generators/clangsharp/generate_bindings.py:1263-1274`

- [ ] **Step 4.11.1: Add --handles-namespace to extra_args**

  At `generate_bindings.py:1267-1271`, the current owner-mode invocation is:

  ```python
                  owner_mode = "owner" if family in ("core", "ttf", "mixer") else "consumer"
                  exit_code = run_postprocess(
                      repo, family, spike_root, "uniform-opaque", codegen,
                      extra_args=["--owner-mode", owner_mode],
                  )
  ```

  Extend `extra_args` to also include `--handles-namespace`:

  ```python
                  owner_mode = "owner" if family in ("core", "ttf", "mixer") else "consumer"
                  handles_namespace = FAMILY_CONFIG[family]["namespace"]
                  exit_code = run_postprocess(
                      repo, family, spike_root, "uniform-opaque", codegen,
                      extra_args=[
                          "--owner-mode", owner_mode,
                          "--handles-namespace", handles_namespace,
                      ],
                  )
  ```

  For `core`, `handles_namespace == "SDL2"` (matches current hardcoded value — Core regen unchanged). For `image`, `"SDL2.Image"` (consumer mode — does not write `Handles.g.cs`, but flag is passed for consistency).

### Task 4.12: Core regen byte-equivalence verification

**Files:** none modified (verification only).

- [ ] **Step 4.12.1: Regenerate --family all**

  ```pwsh
  python spikes/binding-generators/clangsharp/generate_bindings.py --family all --execute --vcpkg-triplet x64-windows-hybrid --use-platform-header-shims
  ```

  Expected: exits 0, writes `clangsharp-production.md`.

- [ ] **Step 4.12.2: Diff Core Generated/ against HEAD**

  ```pwsh
  git diff --ignore-cr-at-eol spikes/binding-generators/clangsharp/src/Janset.SDL2.Core/Generated/
  ```

  Expected: empty (Core's `Handles.g.cs` is unchanged because the migrated roster preserves the 14 + 3 names byte-for-byte and `--handles-namespace SDL2` matches the previous hardcoded value).

- [ ] **Step 4.12.3: Diff Image Generated/ against HEAD**

  ```pwsh
  git diff --ignore-cr-at-eol spikes/binding-generators/clangsharp/src/Janset.SDL2.Image/Generated/
  ```

  Expected: empty (Image is consumer mode; no `Handles.g.cs` emitted; rewriter rewrites Core handles by-value as before because cross-family pull preserves the same handle name set).

### Task 4.13: Cleanup roster backup

**Files:**

- Delete: `spikes/binding-generators/clangsharp/policy/opaque-handle-roster.v1.backup.json`
- Modify: `spikes/binding-generators/clangsharp/policy/.gitignore` (remove the backup line)

- [ ] **Step 4.13.1: Remove the backup file**

  ```pwsh
  Remove-Item -LiteralPath "spikes/binding-generators/clangsharp/policy/opaque-handle-roster.v1.backup.json"
  ```

- [ ] **Step 4.13.2: Remove .gitignore line**
  Edit `spikes/binding-generators/clangsharp/policy/.gitignore` and delete the line `opaque-handle-roster.v1.backup.json`. If the `.gitignore` becomes empty, delete it entirely.

### Task 4.14: Common: Determinism Contract Verification

See "Per-slice common requirements" §Common: Determinism Contract Verification — execute D.1 through D.5.

**Critical:** D.5 (cross-family handle pull preservation) is the headline test for this slice. If D.5 fails, the cross-family pull logic in `LoadRoster` (Task 4.5) is broken — Image's output would emit `SDL_Renderer*` instead of by-value `SDL_Renderer`. Reject the slice; debug.

### Task 4.15: Common: Slopwatch

See "Per-slice common requirements" §Common: Slopwatch. No file deletions in this slice (backup file is gitignored, not committed) — analyze only, baseline rebuild not required.

### Task 4.16: Commit

Two-commit shape for clarity:

- [ ] **Step 4.16.1: Commit the roster migration**

  ```pwsh
  git status --short spikes/binding-generators/clangsharp/policy/opaque-handle-roster.json
  ```

  Expected: `M  spikes/binding-generators/clangsharp/policy/opaque-handle-roster.json`.

  ```bash
  git add spikes/binding-generators/clangsharp/policy/opaque-handle-roster.json
  git commit -m "$(cat <<'EOF'
  refactor(spike): migrate opaque-handle-roster.json to family-keyed schema 2.0 (S1-4a)

  - Wrap existing Core entries (14 + 3 + 12) under families.core verbatim.
  - Add families.{image,ttf,mixer,gfx} with library_version + lists.
  - TTF_Font entry under families.ttf.auto_detect_well_known.
  - Mix_Music entry under families.mixer.auto_detect_well_known.
  - schema_version 1.0 -> 2.0; bump audit date.

  Byte-preservation: Core entries are byte-identical to v1 backup; Core regen
  produces identical Handles.g.cs.

  Refs: spec §5.2, Constitution §"Opaque Handles" Canonical roster.
  EOF
  )"
  ```

- [ ] **Step 4.16.2: Commit the rewriter + orchestrator changes**

  ```pwsh
  git status --short
  ```

  Expected modified:
  - `spikes/binding-generators/clangsharp/postprocess/OpaqueHandleEmitRewriter.cs`
  - `spikes/binding-generators/clangsharp/postprocess/UniformOpaqueOwnerMode.cs`
  - `spikes/binding-generators/clangsharp/postprocess/Program.cs`
  - `spikes/binding-generators/clangsharp/postprocess/PostProcessSelfTests.cs` (new)
  - `spikes/binding-generators/clangsharp/generate_bindings.py`

  ```bash
  git add spikes/binding-generators/clangsharp/postprocess/ spikes/binding-generators/clangsharp/generate_bindings.py
  git commit -m "$(cat <<'EOF'
  refactor(spike): OpaqueHandleEmitRewriter family-blind + cross-family pull (S1-4b)

  - Drop StartsWith("SDL_") gate in DiscoverAutoDetectedHandles (4 sites);
    detection is now purely structural per Constitution §"Opaque Handles"
    Auto-detect criterion (family-blind).
  - LoadRoster(rosterPath, family): family-keyed parse + cross-family pull —
    satellites also see Core's auto_detect_well_known + force_opaque_exceptions
    so satellite rewriter emits Core handles by-value uniformly (Pattern B
    invariant; Constitution §"Generation Determinism Contract"
    Cross-family handle name resolution).
  - ReportDrift(syntacticDetect, rosterAutoDetect, family): drift watchdog
    cites family name; per-family meaningful, consumer dirs short-circuit.
  - BuildHandlesFileContent(handleNames, namespaceName): parameterized.
  - UniformOpaqueOwnerMode.EmitConsolidatedHandlesFileIfOwner: accepts
    namespaceName + plumbs to BuildHandlesFileContent.
  - Program.cs: parses --handles-namespace CLI flag; resolves family from
    output directory path.
  - generate_bindings.py: owner-mode invocation passes
    --handles-namespace FAMILY_CONFIG[family]["namespace"].
  - New postprocess self-test harness (--self-test) covers LoadRoster
    family parameter, cross-family pull, DiscoverAutoDetectedHandles
    family-blind discovery, BuildHandlesFileContent namespace emission.

  Byte-equivalence: Core + Image regen byte-identical to HEAD. Cross-family
  handle pull preserved (Image still emits SDL_Renderer/SDL_RWops by-value).

  Refs: spec §5.2, Constitution §"Opaque Handles" + §"Generation Determinism Contract".
  EOF
  )"
  ```

**Slice exit evidence:**

- ✓ Roster JSON migrated to family-keyed schema 2.0; Core entries byte-preserved.
- ✓ TTF_Font + Mix_Music listed in their families' auto_detect_well_known.
- ✓ Postprocess self-test PASS (LoadRoster, cross-family pull, DiscoverAutoDetectedHandles family-blind, BuildHandlesFileContent namespace).
- ✓ Determinism Contract D.1–D.5 all pass.
- ✓ Multi-TFM build clean for postprocess project.
- ✓ Slopwatch clean.
- ✓ Two commits (roster migration; rewriter+orchestrator changes).

---

## S1-5 — ClongDualDispatchRewriter — Roslyn node-level mutation

**Goal:** Rename `threadid-dispatch` → `clong-dispatch` (mode key, file, class). Move mutation from text-substitution to true Roslyn syntax mutation via `VisitClassDeclaration` returning a class with a mutated `Members` list built by `SyntaxFactory`. Expand `AffectedMethodNames` to include the 5 TTF C `long` symbols (dormant — TTF doesn't run yet). Add parameter-position rewrite logic. Core regen produces output structurally equivalent to current; one-time formatting churn acceptable iff committed atomically. AbiTests revalidation is **critical** — confirms refactor preserves runtime behavior.

**Spec sections:** §5.3 (ClongDualDispatchRewriter — true Roslyn node-level mutation).
**Constitution sections:** §"C `long` And `unsigned long`" (Priority C hybrid CLong/CULong policy — Why/How/What subsections; TTF satellite C `long` surface clause); §"Generation Determinism Contract" (Core regen byte-equivalence; AbiTests runtime smoke).

**Code anchors:**

- `spikes/binding-generators/clangsharp/postprocess/ThreadIdDualDispatchRewriter.cs` (entire file — to be renamed + refactored).
- `spikes/binding-generators/clangsharp/postprocess/Program.cs:7-11, 27-31, 49, 116-125` — mode key references (`"threadid-dispatch"`).
- `spikes/binding-generators/clangsharp/generate_bindings.py:1239-1245` — postprocess invocation.
- `spikes/binding-generators/clangsharp/generate_bindings.py:1230-1238` — postprocess pipeline comment block.

**Memory disciplines:** `git mv during migrations` — rename via `git mv` FIRST before any content edit; preserves `git log --follow` / `git blame -C` history.

**Slice TDD ordering note.** Tasks 5.1–5.4 are a **pre-TDD rename prelude** — they propagate the `ThreadIdDualDispatchRewriter` → `ClongDualDispatchRewriter` name change across the file, Program.cs mode key, and generate_bindings.py invocation. These four tasks change **identifiers only**, not behavior. The TDD red→green discipline proper begins at Task 5.5 (failing test for parameter-position rewrite), where the new behavior — Roslyn node-level mutation + parameter-position rewriting — is added test-first. This split reflects the genuine tension between memory `git mv during migrations` (rename first) and the plan's TDD policy (test first); for a rename-then-behavior-change refactor, rename-first is correct because the test in Task 5.5 must reference the new class name to assert the new behavior. Behavior changes (Tasks 5.5 onward) follow strict TDD.

### Task 5.1: git mv ThreadIdDualDispatchRewriter.cs → ClongDualDispatchRewriter.cs

**Files:**

- Rename: `spikes/binding-generators/clangsharp/postprocess/ThreadIdDualDispatchRewriter.cs` → `spikes/binding-generators/clangsharp/postprocess/ClongDualDispatchRewriter.cs`

- [ ] **Step 5.1.1: git mv the file (history-preserving)**

  ```pwsh
  git mv spikes/binding-generators/clangsharp/postprocess/ThreadIdDualDispatchRewriter.cs spikes/binding-generators/clangsharp/postprocess/ClongDualDispatchRewriter.cs
  ```

  Expected: no output (success).

- [ ] **Step 5.1.2: Verify the rename is staged**

  ```pwsh
  git status --short spikes/binding-generators/clangsharp/postprocess/
  ```

  Expected:

  ```
  R  spikes/binding-generators/clangsharp/postprocess/ThreadIdDualDispatchRewriter.cs -> spikes/binding-generators/clangsharp/postprocess/ClongDualDispatchRewriter.cs
  ```

### Task 5.2: Rename class inside the file

**Files:**

- Modify: `spikes/binding-generators/clangsharp/postprocess/ClongDualDispatchRewriter.cs`

- [ ] **Step 5.2.1: Replace class name**

  In the renamed file, change all occurrences of `ThreadIdDualDispatchRewriter` to `ClongDualDispatchRewriter`:
  - Class declaration: `internal sealed class ThreadIdDualDispatchRewriter` → `internal sealed class ClongDualDispatchRewriter`
  - Constructor: `public ThreadIdDualDispatchRewriter(Mode mode)` → `public ClongDualDispatchRewriter(Mode mode)`
  - Top-of-file XML/comment references — update to reflect that this is now a generalized C `long` rewriter (not just SDL_threadID).

  Also update the top-of-file summary block (L9-67) to reflect the broader scope:

  ```
  // Roslyn-level emit for C `long` and `unsigned long` raw ABI signatures, mode-aware.
  //
  // Two channels: structural SDL_ThreadID/SDL_GetThreadID family (Constitution §"C `long`"
  // Priority C closure R2) AND satellite C `long` surface (SDL_ttf's TTF_OpenFontIndex*,
  // TTF_FontFaces — dormant until Item 4 activates TTF).
  //
  // Mutation strategy: true Roslyn node-level via VisitClassDeclaration returning a
  // mutated Members list (one matched method expands to 1 dispatcher + 2 helper
  // DllImports on Compat; one [LibraryImport] partial on Modern). SyntaxFactory builds
  // attribute lists, parameter lists, body blocks — inherits Roslyn normalization
  // automatically (no manual indentation / trivia management).
  //
  // See Constitution §"C `long` And `unsigned long`" Priority C hybrid strategy.
  ```

- [ ] **Step 5.2.2: Build the project**

  ```pwsh
  dotnet build spikes/binding-generators/clangsharp/postprocess/Janset.SDL2.PostProcess.csproj -c Release
  ```

  Expected: compile errors at `Program.cs:116-125` (still references `ThreadIdDualDispatchRewriter` and `"threadid-dispatch"`). Step 5.3 fixes that.

### Task 5.3: Update Program.cs mode key + class references

**Files:**

- Modify: `spikes/binding-generators/clangsharp/postprocess/Program.cs`

- [ ] **Step 5.3.1: Update mode key string in the allowlist check (L49)**

  Replace:

  ```csharp
  if (args.Length < 2 || args[0] is not ("strip-varargs" or "libraryimport" or "platform-delta" or "guid-substitute" or "threadid-dispatch" or "uniform-opaque"))
  ```

  with:

  ```csharp
  if (args.Length < 2 || args[0] is not ("strip-varargs" or "libraryimport" or "platform-delta" or "guid-substitute" or "clong-dispatch" or "uniform-opaque"))
  ```

  **Note:** `flags-detect` is intentionally NOT added here. S1-5 owns only the `clong-dispatch` rename; S1-6 atomically adds BOTH the `flags-detect` allowlist entry AND the case block (avoids the "allowlist accepts but case throws" dangling state between slices). See S1-6 Task 6.5 for the matched pair.

- [ ] **Step 5.3.2: Update usage banner (L51)**

  Replace `"threadid-dispatch"` with `"clong-dispatch"`. **Do NOT** add `"flags-detect"` to the banner — that lands in S1-6 Task 6.5 alongside the case body.

- [ ] **Step 5.3.3: Update the switch case (L116-125)**

  Replace:

  ```csharp
      case "threadid-dispatch":
      {
          var threadIdMode = ThreadIdDualDispatchRewriter.DetectMode(inputDir);
          Console.WriteLine($"threadid-dispatch: mode={threadIdMode}");
          var r = new ThreadIdDualDispatchRewriter(threadIdMode);
          ...
      }
  ```

  with:

  ```csharp
      case "clong-dispatch":
      {
          var clongMode = ClongDualDispatchRewriter.DetectMode(inputDir);
          Console.WriteLine($"clong-dispatch: mode={clongMode}");
          var r = new ClongDualDispatchRewriter(clongMode);
          rewriter = r;
          hasChanges = () => r.AnyChanges;
          resetRewriter = r.Reset;
          break;
      }
  ```

- [ ] **Step 5.3.4: Update the comment header (L26-35) referencing the rewriter**

  Replace the `// threadid-dispatch :` block with:

  ```
  // clong-dispatch    : Roslyn node-level emit for C `long` / `unsigned long` raw ABI
  //                     signatures. Covers SDL_ThreadID family (Core) and TTF C `long`
  //                     surface (dormant until Item 4 activates TTF in --family all).
  //                     Modern emits [LibraryImport] + CLong/CULong; Compat emits a
  //                     managed wrapper + RuntimeInformation.IsOSPlatform dispatch to
  //                     per-RID [DllImport] helpers (uint on Win, nint on Unix64).
  //                     Mode is auto-detected from inputDir path (Compat / Modern).
  ```

- [ ] **Step 5.3.5: Build, expect clean**

  ```pwsh
  dotnet build spikes/binding-generators/clangsharp/postprocess/Janset.SDL2.PostProcess.csproj -c Release
  ```

  Expected: 0 errors.

### Task 5.4: Update generate_bindings.py mode key + pipeline comments

**Files:**

- Modify: `spikes/binding-generators/clangsharp/generate_bindings.py:1230-1245`

- [ ] **Step 5.4.1: Update the postprocess comment block (L1230-1238)**

  Replace the `# Slice C-A R2 structural SDL_threadID hybrid dispatch:` block with:

  ```python
      # Slice C-A R2 + Item 1 S1-5: Roslyn node-level C `long` / unsigned long hybrid
      # dispatch. Covers SDL_ThreadID family (Core, active) and TTF C `long` surface
      # (TTF_OpenFontIndex*, TTF_FontFaces — dormant until Item 4 activates TTF in
      # --family all). Modern emits [LibraryImport] + CLong/CULong; Compat emits
      # managed wrapper + RuntimeInformation.IsOSPlatform dispatch to per-RID
      # [DllImport] helpers (uint on Win, nint on Unix64). Applied to both Compat and
      # Modern after guid-substitute so the rewrite sees the final attribute shape
      # across both codegen trees.
  ```

- [ ] **Step 5.4.2: Update the run_postprocess call (L1239-1245)**

  Replace:

  ```python
              print("--- postprocess: threadid-dispatch (all codegens) ---")
              for codegen in codegen_passes:
                  for family in selected:
                      exit_code = run_postprocess(repo, family, spike_root, "threadid-dispatch", codegen)
                      if exit_code != 0:
                          postprocess_failures += 1
                          print(f"WARNING: threadid-dispatch postprocess for {family}/{codegen} returned exit {exit_code}")
  ```

  with:

  ```python
              print("--- postprocess: clong-dispatch (all codegens) ---")
              for codegen in codegen_passes:
                  for family in selected:
                      exit_code = run_postprocess(repo, family, spike_root, "clong-dispatch", codegen)
                      if exit_code != 0:
                          postprocess_failures += 1
                          print(f"WARNING: clong-dispatch postprocess for {family}/{codegen} returned exit {exit_code}")
  ```

- [ ] **Step 5.4.3: Update run_postprocess docstring mode list (L706-712)**

  In `run_postprocess` function docstring, replace `"threadid-dispatch"` with `"clong-dispatch"`.

- [ ] **Step 5.4.4: Verify no remaining references**

  Use the Grep tool:
  - Pattern: `threadid-dispatch`
  - Path: `spikes/`
  - Output: `files_with_matches`

  Expected: zero matches in `.cs` / `.py` files. Acceptable matches in this plan doc + spec + roadmap + historical commits only.

### Task 5.5: Add failing test for parameter-position rewrite

**Files:**

- Modify: `spikes/binding-generators/clangsharp/postprocess/PostProcessSelfTests.cs` (created in S1-4 Task 4.4)

- [ ] **Step 5.5.1: Add a synthetic test exercising `long index` parameter**

  Append to `PostProcessSelfTests.Run`:

  ```csharp
          // Test 7: ClongDualDispatchRewriter rewrites `long index` parameter to CLong on Modern
          // and to per-RID dispatch on Compat. Synthetic fixture mimics TTF_OpenFontIndex's signature.
          var clongFixture = """
  using System.Runtime.CompilerServices;
  using System.Runtime.InteropServices;

  namespace SDL2.Ttf
  {
      internal static unsafe partial class SDL_ttfNative
      {
          [DllImport("SDL2_ttf", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
          public static extern TTF_Font TTF_OpenFontIndex(byte* file, int ptsize, [NativeTypeName("long")] long index);
      }
  }
  """;
          var modernRewriter = new ClongDualDispatchRewriter(ClongDualDispatchRewriter.Mode.Modern);
          var modernTree = CSharpSyntaxTree.ParseText(clongFixture);
          var modernRoot = (CompilationUnitSyntax)modernRewriter.Visit(modernTree.GetCompilationUnitRoot())!;
          var modernOutput = modernRoot.ToFullString();
          if (!modernOutput.Contains("CLong index"))
              failures.Add("ClongDualDispatchRewriter Modern: did not rewrite `long index` parameter to `CLong index`");
          if (!modernOutput.Contains("[LibraryImport"))
              failures.Add("ClongDualDispatchRewriter Modern: did not emit [LibraryImport] attribute");

          var compatRewriter = new ClongDualDispatchRewriter(ClongDualDispatchRewriter.Mode.Compat);
          var compatTree = CSharpSyntaxTree.ParseText(clongFixture);
          var compatRoot = (CompilationUnitSyntax)compatRewriter.Visit(compatTree.GetCompilationUnitRoot())!;
          var compatOutput = compatRoot.ToFullString();
          if (!compatOutput.Contains("RuntimeInformation.IsOSPlatform"))
              failures.Add("ClongDualDispatchRewriter Compat: dispatcher missing RuntimeInformation.IsOSPlatform branch");
          if (!compatOutput.Contains("_Win32"))
              failures.Add("ClongDualDispatchRewriter Compat: Win32 helper DllImport not emitted");
          if (!compatOutput.Contains("_Unix64"))
              failures.Add("ClongDualDispatchRewriter Compat: Unix64 helper DllImport not emitted");
  ```

- [ ] **Step 5.5.2: Run self-test, verify FAILS**

  Note: `TTF_OpenFontIndex` is not yet in `AffectedMethodNames` (added in Task 5.10), and the current rewriter only handles return-position. The test should fail on at least one assertion.

  ```pwsh
  dotnet run --project spikes/binding-generators/clangsharp/postprocess/Janset.SDL2.PostProcess.csproj -c Release -- --self-test
  ```

  Expected failures: `ClongDualDispatchRewriter Modern: did not rewrite ...`, `Compat dispatcher missing ...`, etc.

### Task 5.6: Add VisitClassDeclaration override (Roslyn node-level mutation skeleton)

**Files:**

- Modify: `spikes/binding-generators/clangsharp/postprocess/ClongDualDispatchRewriter.cs`

- [ ] **Step 5.6.1: Add VisitClassDeclaration override**

  Inside the class body (after existing constructor + properties), add:

  ```csharp
  public override SyntaxNode? VisitClassDeclaration(ClassDeclarationSyntax node)
  {
      var newMembers = new List<MemberDeclarationSyntax>();
      var changed = false;

      foreach (var member in node.Members)
      {
          if (member is MethodDeclarationSyntax method && IsAffectedMethod(method))
          {
              IEnumerable<MemberDeclarationSyntax> replacement = _mode == Mode.Modern
                  ? BuildModernMembers(method)
                  : BuildCompatMembers(method);
              newMembers.AddRange(replacement);
              changed = true;
          }
          else
          {
              newMembers.Add(member);
          }
      }

      if (!changed)
      {
          return base.VisitClassDeclaration(node);
      }

      AnyChanges = true;
      return node.WithMembers(SyntaxFactory.List(newMembers));
  }

  private bool IsAffectedMethod(MethodDeclarationSyntax method)
  {
      if (!AffectedMethodNames.Contains(method.Identifier.ValueText)) return false;
      var returnNativeType = ExtractReturnNativeTypeName(method);
      if (returnNativeType is "SDL_threadID" or "unsigned long" or "long") return true;
      // Parameter-position sensor — fires for TTF_OpenFontIndex*'s `long index`, etc.
      return method.ParameterList.Parameters.Any(p => HasClongAnnotation(p));
  }

  private static bool HasClongAnnotation(ParameterSyntax param)
  {
      foreach (var al in param.AttributeLists)
      {
          foreach (var attr in al.Attributes)
          {
              if (attr.Name.ToString() != "NativeTypeName") continue;
              var arg = attr.ArgumentList?.Arguments.FirstOrDefault();
              if (arg?.Expression is LiteralExpressionSyntax lit)
              {
                  var v = lit.Token.ValueText;
                  if (v is "long" or "unsigned long" or "SDL_threadID") return true;
              }
          }
      }
      return false;
  }
  ```

  These additions do NOT yet remove the old `VisitMethodDeclaration` + `_pending` + `VisitCompilationUnit` text-substitution path; both paths coexist temporarily for incremental migration. Tasks 5.7–5.9 build the SyntaxFactory output then Task 5.9 removes the old text path.

### Task 5.7: Implement BuildModernMembers via SyntaxFactory

**Files:**

- Modify: `spikes/binding-generators/clangsharp/postprocess/ClongDualDispatchRewriter.cs`

- [ ] **Step 5.7.1: Add BuildModernMembers method**

  Inside the class, add:

  ```csharp
  private IEnumerable<MemberDeclarationSyntax> BuildModernMembers(MethodDeclarationSyntax method)
  {
      var libPath = ExtractLibraryPath(method) ?? "SDL2";
      var name = method.Identifier.ValueText;
      var returnNativeType = ExtractReturnNativeTypeName(method);
      var isUnsignedLong = returnNativeType is "SDL_threadID" or "unsigned long"
          || method.ParameterList.Parameters.Any(p => GetNativeTypeName(p) == "unsigned long");
      var clongType = isUnsignedLong ? "CULong" : "CLong";

      // Build new parameter list: rewrite any [NativeTypeName("long"|"unsigned long")] parameter type
      // to CLong/CULong; pass others through unchanged.
      var newParams = method.ParameterList.Parameters.Select(p =>
          HasClongAnnotation(p)
              ? p.WithType(SyntaxFactory.IdentifierName(clongType))
              : p);

      // Determine return type: CLong/CULong if return native type was C long; otherwise preserve.
      var returnType = (returnNativeType is "SDL_threadID" or "unsigned long" or "long")
          ? SyntaxFactory.IdentifierName(clongType)
          : (TypeSyntax)method.ReturnType;

      var attributes = SyntaxFactory.List(new[]
      {
          SyntaxFactory.AttributeList(SyntaxFactory.SingletonSeparatedList(
              SyntaxFactory.Attribute(SyntaxFactory.ParseName("LibraryImport"))
                  .WithArgumentList(SyntaxFactory.ParseAttributeArgumentList(
                      $"(\"{libPath}\", EntryPoint = \"{name}\")")))),
          SyntaxFactory.AttributeList(SyntaxFactory.SingletonSeparatedList(
              SyntaxFactory.Attribute(SyntaxFactory.ParseName("UnmanagedCallConv"))
                  .WithArgumentList(SyntaxFactory.ParseAttributeArgumentList(
                      "(CallConvs = new[] { typeof(CallConvCdecl) })")))),
      });

      if (returnNativeType is { Length: > 0 })
      {
          attributes = attributes.Add(SyntaxFactory.AttributeList(
              SyntaxFactory.SingletonSeparatedList(
                  SyntaxFactory.Attribute(SyntaxFactory.ParseName("NativeTypeName"))
                      .WithArgumentList(SyntaxFactory.ParseAttributeArgumentList($"(\"{returnNativeType}\")"))))
              .WithTarget(SyntaxFactory.AttributeTargetSpecifier(SyntaxFactory.Token(SyntaxKind.ReturnKeyword))));
      }

      // CRITICAL: clong-dispatch runs AFTER libraryimport, which already added `partial`
      // to every [LibraryImport]-decorated method. SyntaxTokenList.Add does not dedup,
      // so blindly calling `.Add(PartialKeyword)` would emit `internal static partial partial`
      // and break compilation. Guard the addition.
      var modifiers = method.Modifiers.Any(SyntaxKind.PartialKeyword)
          ? method.Modifiers
          : method.Modifiers.Add(SyntaxFactory.Token(SyntaxKind.PartialKeyword));

      // Strip `extern` from the original method (LibraryImport partials cannot be extern).
      modifiers = SyntaxFactory.TokenList(modifiers.Where(m => !m.IsKind(SyntaxKind.ExternKeyword)));

      var newMethod = SyntaxFactory.MethodDeclaration(returnType, name)
          .WithAttributeLists(attributes)
          .WithModifiers(modifiers)
          .WithParameterList(SyntaxFactory.ParameterList(SyntaxFactory.SeparatedList(newParams)))
          .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken));

      yield return newMethod;
  }
  ```

  Helper: add `GetNativeTypeName(ParameterSyntax)` returning the `NativeTypeName` literal text or `null`.

  **Equivalent guard required in `BuildCompatMembers`** — the Compat dispatcher must NOT carry `partial` (it has a body), and the helper DllImports must NOT carry `partial` (legacy `[DllImport]` extern shape). Both are filtered explicitly in Task 5.8's per-RID helper construction (which builds its own modifier token list from scratch — see Step 5.8.1). The dispatcher inherits `method.Modifiers` minus `extern` and minus `partial`:

  ```csharp
  // In BuildCompatMembers, when constructing the dispatcher's modifiers:
  var dispatcherModifiers = SyntaxFactory.TokenList(
      method.Modifiers.Where(m =>
          !m.IsKind(SyntaxKind.ExternKeyword) &&
          !m.IsKind(SyntaxKind.PartialKeyword)));
  ```

- [ ] **Step 5.7.2: Dry-run validate `SyntaxFactory.ParseAttributeArgumentList` calls**

  The plan's `BuildModernMembers` invokes `SyntaxFactory.ParseAttributeArgumentList` with two non-trivial argument strings:

  ```csharp
  SyntaxFactory.ParseAttributeArgumentList($"(\"{libPath}\", EntryPoint = \"{name}\")")
  SyntaxFactory.ParseAttributeArgumentList("(CallConvs = new[] { typeof(CallConvCdecl) })")
  ```

  `ParseAttributeArgumentList` expects an argument-list shape including the leading `(` and trailing `)` and DOES handle `typeof(...)` + array initializers in attribute argument position. **But this is not free** — failures here surface only at rewriter runtime, not at compile time. Before relying on these in the rewriter, validate via a one-off scratch run.

  Add a dry-run check at the top of `PostProcessSelfTests.Run` (before any rewriter tests):

  ```csharp
          // Dry-run: validate SyntaxFactory.ParseAttributeArgumentList for the
          // two non-trivial argument strings used by ClongDualDispatchRewriter.BuildModernMembers.
          // If parsing fails, the rewriter will throw at runtime — catch it here instead.
          try
          {
              var libraryImportArgs = SyntaxFactory.ParseAttributeArgumentList("(\"SDL2\", EntryPoint = \"SDL_ThreadID\")");
              if (libraryImportArgs.Arguments.Count != 2)
                  failures.Add($"ParseAttributeArgumentList(LibraryImport) expected 2 args, got {libraryImportArgs.Arguments.Count}");

              var callConvArgs = SyntaxFactory.ParseAttributeArgumentList("(CallConvs = new[] { typeof(CallConvCdecl) })");
              if (callConvArgs.Arguments.Count != 1)
                  failures.Add($"ParseAttributeArgumentList(UnmanagedCallConv) expected 1 arg, got {callConvArgs.Arguments.Count}");

              // Verify the typeof expression survived parsing
              if (!callConvArgs.ToFullString().Contains("typeof(CallConvCdecl)"))
                  failures.Add($"ParseAttributeArgumentList stripped typeof(CallConvCdecl); got: {callConvArgs.ToFullString()}");
          }
          catch (Exception exc)
          {
              failures.Add($"SyntaxFactory.ParseAttributeArgumentList dry-run threw: {exc.GetType().Name}: {exc.Message}");
          }
  ```

  If this dry-run fails, the rewriter is broken — switch to explicit `AttributeArgumentList` construction via `SyntaxFactory.AttributeArgumentList(SyntaxFactory.SeparatedList(...))` and individual `SyntaxFactory.AttributeArgument(...)` calls. The dry-run catches this before the production regen call.

### Task 5.8: Implement BuildCompatMembers via SyntaxFactory

**Files:**

- Modify: `spikes/binding-generators/clangsharp/postprocess/ClongDualDispatchRewriter.cs`

- [ ] **Step 5.8.1: Add BuildCompatMembers method**

  Inside the class, add:

  ```csharp
  private IEnumerable<MemberDeclarationSyntax> BuildCompatMembers(MethodDeclarationSyntax method)
  {
      var libPath = ExtractLibraryPath(method) ?? "SDL2";
      var name = method.Identifier.ValueText;
      var returnNativeType = ExtractReturnNativeTypeName(method);
      var isUnsignedLong = returnNativeType is "SDL_threadID" or "unsigned long"
          || method.ParameterList.Parameters.Any(p => GetNativeTypeName(p) == "unsigned long");

      // Helper builds a parameter list with C `long` parameters mapped to a per-RID type
      // (`uint`/`int` for Win32, `nint` for Unix64). Preserves other parameters.
      ParameterListSyntax BuildHelperParams(string clongRidType) =>
          SyntaxFactory.ParameterList(SyntaxFactory.SeparatedList(
              method.ParameterList.Parameters.Select(p =>
                  HasClongAnnotation(p)
                      ? p.WithType(SyntaxFactory.IdentifierName(clongRidType))
                      : p)));

      // Dispatcher: returns ulong (unsigned) or long (signed), calls Win32 helper on Windows, Unix64 elsewhere.
      var managedReturnType = isUnsignedLong ? "ulong" : "long";
      var winRidType = isUnsignedLong ? "uint" : "int";
      var unixRidType = "nint";

      var argList = string.Join(", ", method.ParameterList.Parameters.Select(p => p.Identifier.ValueText));
      var dispatcherBody = SyntaxFactory.ParseStatement($$"""
          {
              if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                  return {{name}}_Win32({{argList}});
              return ({{managedReturnType}}){{name}}_Unix64({{argList}});
          }
          """);

      var dispatcher = SyntaxFactory.MethodDeclaration(SyntaxFactory.IdentifierName(managedReturnType), name)
          .WithAttributeLists(returnNativeType is { Length: > 0 }
              ? SyntaxFactory.List(new[] { SyntaxFactory.AttributeList(SyntaxFactory.SingletonSeparatedList(
                  SyntaxFactory.Attribute(SyntaxFactory.ParseName("NativeTypeName"))
                      .WithArgumentList(SyntaxFactory.ParseAttributeArgumentList($"(\"{returnNativeType}\")"))))
                  .WithTarget(SyntaxFactory.AttributeTargetSpecifier(SyntaxFactory.Token(SyntaxKind.ReturnKeyword))) })
              : default)
          .WithModifiers(method.Modifiers)
          .WithParameterList(method.ParameterList)
          .WithBody((BlockSyntax)dispatcherBody);

      // Two private helper DllImports: Win32 (uint/int return + uint/int param) and Unix64 (nint).
      MemberDeclarationSyntax BuildHelperDllImport(string suffix, string ridReturnType, string ridParamType)
      {
          var dllImportAttr = SyntaxFactory.AttributeList(SyntaxFactory.SingletonSeparatedList(
              SyntaxFactory.Attribute(SyntaxFactory.ParseName("DllImport"))
                  .WithArgumentList(SyntaxFactory.ParseAttributeArgumentList(
                      $"(\"{libPath}\", EntryPoint = \"{name}\", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)"))));

          return SyntaxFactory.MethodDeclaration(SyntaxFactory.IdentifierName(ridReturnType), $"{name}_{suffix}")
              .WithAttributeLists(SyntaxFactory.SingletonList(dllImportAttr))
              .WithModifiers(SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.PrivateKeyword), SyntaxFactory.Token(SyntaxKind.StaticKeyword), SyntaxFactory.Token(SyntaxKind.ExternKeyword)))
              .WithParameterList(BuildHelperParams(ridParamType))
              .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken));
      }

      yield return dispatcher;
      yield return BuildHelperDllImport("Win32", winRidType, winRidType);
      yield return BuildHelperDllImport("Unix64", unixRidType, unixRidType);
  }
  ```

### Task 5.9: Remove old text-substitution path

**Files:**

- Modify: `spikes/binding-generators/clangsharp/postprocess/ClongDualDispatchRewriter.cs`

- [ ] **Step 5.9.1: Delete deprecated code**

  Remove the following members from the class:
  - `private readonly List<(TextSpan FullSpan, string Replacement)> _pending = new();`
  - Inside `Reset()`: remove `_pending.Clear();`
  - `public override SyntaxNode? VisitMethodDeclaration(...)` (the old text-substitution staging method)
  - `public override SyntaxNode? VisitCompilationUnit(...)` (the StringBuilder remove/insert path)
  - `private static string BuildModernReplacement(...)` (text template renderer)
  - `private static string BuildCompatReplacement(...)` (text template renderer)
  - `private static string StripParamTypes(string paramList)`
  - `private static string ExtractLeadingIndentation(MethodDeclarationSyntax method)`

  Keep:
  - `ExtractReturnNativeTypeName(MethodDeclarationSyntax method)` — still used by `IsAffectedMethod` and `BuildModernMembers`/`BuildCompatMembers`.
  - `ExtractLibraryPath(MethodDeclarationSyntax method)` — same.
  - `ExtractAccessModifier(MethodDeclarationSyntax method)` — only if still used; otherwise remove.

- [ ] **Step 5.9.2: Build, verify clean**

  ```pwsh
  dotnet build spikes/binding-generators/clangsharp/postprocess/Janset.SDL2.PostProcess.csproj -c Release
  ```

  Expected: 0 errors. If any usage of the removed methods remains, the compiler points to them — clean up.

### Task 5.10: Expand AffectedMethodNames with TTF symbols (data-only, dormant)

**Files:**

- Modify: `spikes/binding-generators/clangsharp/postprocess/ClongDualDispatchRewriter.cs`

- [ ] **Step 5.10.1: Add 5 TTF symbols to AffectedMethodNames**

  Replace:

  ```csharp
  private static readonly HashSet<string> AffectedMethodNames = new(StringComparer.Ordinal)
  {
      "SDL_ThreadID",
      "SDL_GetThreadID",
  };
  ```

  with:

  ```csharp
  private static readonly HashSet<string> AffectedMethodNames = new(StringComparer.Ordinal)
  {
      "SDL_ThreadID",
      "SDL_GetThreadID",
      // Dormant until Item 4 activates TTF in selected_families("all").
      // Constitution §"C `long` And `unsigned long`" (TTF satellite C `long` surface clause).
      "TTF_OpenFontIndex",
      "TTF_OpenFontIndexRW",
      "TTF_OpenFontIndexDPI",
      "TTF_OpenFontIndexDPIRW",
      "TTF_FontFaces",
  };
  ```

### Task 5.11: Run self-test, verify it PASSES

- [ ] **Step 5.11.1: Run self-test**

  ```pwsh
  dotnet run --project spikes/binding-generators/clangsharp/postprocess/Janset.SDL2.PostProcess.csproj -c Release -- --self-test
  ```

  Expected: `postprocess self-test: PASS`. The Task 5.5 synthetic fixture for `long index` parameter rewriting now passes both Modern and Compat assertions.

### Task 5.12: Core regen + structural diff inspection

**Files:** none modified (verification only).

- [ ] **Step 5.12.1: Regenerate --family all**

  ```pwsh
  python spikes/binding-generators/clangsharp/generate_bindings.py --family all --execute --vcpkg-triplet x64-windows-hybrid --use-platform-header-shims
  ```

  Expected: exits 0.

- [ ] **Step 5.12.2: Diff Core Generated/ against HEAD — structural inspection**

  ```pwsh
  git diff --ignore-cr-at-eol spikes/binding-generators/clangsharp/src/Janset.SDL2.Core/Generated/ | Out-String
  ```

  Inspect manually. Two acceptable outcomes:
  - **Empty diff** — Roslyn `SyntaxFactory` output happens to match the previous text-substitution output byte-for-byte. Ideal.
  - **Pure formatting diff** — `SDL_ThreadID` / `SDL_GetThreadID` method bodies are structurally equivalent (same attributes, same parameter types, same body branches) but differ in whitespace (blank line placement, attribute trivia, indentation). Acceptable as a one-time formatting churn.

  **Reject** if any diff is structural (different attributes, different parameter types, different method bodies, missing/extra members). Treat as a bug; do not commit; debug `BuildModernMembers` / `BuildCompatMembers`.

- [ ] **Step 5.12.3: Decide commit strategy based on diff**

  - Empty diff: proceed to Task 5.14 (single commit covers everything).
  - Pure formatting diff: proceed; the Core regen output will be committed as a second commit in Task 5.18 (formatting-only).

### Task 5.13: AbiTests revalidation

**Files:** none modified (verification only).

- [ ] **Step 5.13.1: Run AbiTests across all TFMs**

  ```pwsh
  dotnet test spikes/binding-generators/clangsharp/tests/abi-tests/ -c Release
  ```

  Expected: all tests pass across `net10.0`, `net9.0`, `net8.0`, `net462`. Test names: `SDL_ThreadID_Should_Return_NonZero_When_Invoked`, `SDL_GetThreadID_Should_Match_SDL_ThreadID_When_Same_Thread` (or equivalent — confirm during execution).

  **Critical:** this confirms the Roslyn refactor preserved runtime behavior. If any test fails, the new `BuildModernMembers` / `BuildCompatMembers` output is semantically broken — investigate and fix before commit.

### Task 5.14: Common: Determinism Contract Verification

See "Per-slice common requirements" §Common: Determinism Contract Verification — execute D.1 through D.5. D.4 (per-family equivalence) is the headline check — must hold even with the rewriter refactor.

### Task 5.15: Common: Slopwatch (with baseline rebuild)

See "Per-slice common requirements" §Common: Slopwatch.

- [ ] **Step 5.15.1: Rebuild slopwatch baseline (file rename in this slice)**

  ```pwsh
  slopwatch init -f --exclude "artifacts/**,external/**,vcpkg_installed/**,spikes/binding-generators/references/**,**/bin/**,**/obj/**"
  ```

- [ ] **Step 5.15.2: Run slopwatch analyze**

  ```pwsh
  slopwatch analyze --fail-on warning --exclude "artifacts/**,external/**,vcpkg_installed/**,spikes/binding-generators/references/**,**/bin/**,**/obj/**"
  ```

  Expected: `Scan complete: 0 issue(s) found`.

### Task 5.16: Verify no leftover `threadid-dispatch` references

- [ ] **Step 5.16.1: Repo-wide grep**

  Use the Grep tool:
  - Pattern: `threadid-dispatch`
  - Path: repository root

  Expected acceptable matches: this plan doc, spec, roadmap, historical commits. Reject if any `.py`, `.cs`, `.rsp` file references it.

### Task 5.17: Commit (refactor + rename)

- [ ] **Step 5.17.1: Stage**

  ```pwsh
  git status --short
  ```

  Expected staged:

  ```
  R  spikes/binding-generators/clangsharp/postprocess/ThreadIdDualDispatchRewriter.cs -> spikes/binding-generators/clangsharp/postprocess/ClongDualDispatchRewriter.cs
  M  spikes/binding-generators/clangsharp/postprocess/Program.cs
  M  spikes/binding-generators/clangsharp/postprocess/PostProcessSelfTests.cs
  M  spikes/binding-generators/clangsharp/generate_bindings.py
  ```

- [ ] **Step 5.17.2: Commit**

  ```bash
  git commit -m "$(cat <<'EOF'
  refactor(spike): ClongDualDispatchRewriter — Roslyn node-level mutation (S1-5)

  - git mv ThreadIdDualDispatchRewriter.cs -> ClongDualDispatchRewriter.cs (history-preserving).
  - Switch mutation strategy from text-substitution (StringBuilder Remove/Insert
    on full source) to true Roslyn SyntaxFactory: VisitClassDeclaration returns
    a class with mutated Members list; BuildModernMembers/BuildCompatMembers
    construct nodes via SyntaxFactory.MethodDeclaration / AttributeList /
    ParameterList. Inherits Roslyn whitespace/trivia normalization.
  - Mode key threadid-dispatch -> clong-dispatch (Program.cs + generate_bindings.py).
  - Expand AffectedMethodNames with 5 TTF C `long` symbols (dormant — TTF
    inactive in selected_families("all") until Item 4).
  - Parameter-position sensor: HasClongAnnotation walks ParameterSyntax
    [NativeTypeName("long"|"unsigned long")] annotations. TTF_OpenFontIndex*'s
    `long index` parameter is now rewritten on Modern (CLong) and Compat
    (per-RID dispatcher + helper DllImports).
  - Self-test fixture (PostProcessSelfTests Test 7) covers synthetic
    [NativeTypeName("long")] long index parameter — Modern emits CLong,
    Compat emits dispatcher + Win32(int)/Unix64(nint) DllImports.

  Verification:
  - Core regen: structural equivalence (formatting diff acceptable as
    one-time churn; see follow-up commit if any).
  - AbiTests: SDL_ThreadID/SDL_GetThreadID PASS across net10/9/8/462.
  - Determinism Contract D.1-D.5 all PASS.
  - Slopwatch: 0 issues (baseline rebuilt after rename).

  Refs: spec §5.3, Constitution §"C `long` And `unsigned long`".
  EOF
  )"
  ```

### Task 5.18: Commit formatting churn (conditional)

Only if Task 5.12.2 reported a pure formatting diff in Core Generated/.

- [ ] **Step 5.18.1: Stage Core Generated/ changes**

  ```pwsh
  git add spikes/binding-generators/clangsharp/src/Janset.SDL2.Core/Generated/
  ```

- [ ] **Step 5.18.2: Commit formatting churn separately**

  ```bash
  git commit -m "$(cat <<'EOF'
  chore(spike): regenerate Core SDL_ThreadID/SDL_GetThreadID — Roslyn formatting (S1-5)

  One-time formatting churn from ClongDualDispatchRewriter migration to Roslyn
  SyntaxFactory output (S1-5 prior commit). Structural equivalence verified by
  AbiTests + Determinism Contract D.1-D.5. No behavioral change.

  Refs: S1-5 commit refactor(spike): ClongDualDispatchRewriter ...
  EOF
  )"
  ```

**Slice exit evidence:**

- ✓ File renamed via `git mv`; history preserved.
- ✓ Mode key `threadid-dispatch` → `clong-dispatch` propagated (Program.cs, generate_bindings.py, comments).
- ✓ Rewriter uses Roslyn `SyntaxFactory` for member construction; old text-substitution path removed.
- ✓ `AffectedMethodNames` extended with 5 TTF symbols (dormant).
- ✓ Parameter-position rewrite verified by synthetic self-test.
- ✓ AbiTests PASS across Windows TFMs (net10/9/8/462).
- ✓ Determinism Contract D.1–D.5 all PASS.
- ✓ Slopwatch clean (baseline rebuilt post-rename).
- ✓ Zero `threadid-dispatch` references remain in code.
- ✓ 1–2 commits (refactor + optional formatting churn).

---

## S1-6 — FlagsAttributeRewriter (new 7th pipeline step)

**Goal:** New `FlagsAttributeRewriter` adds `[Flags]` to enums by **name-suffix + family-keyed allow-list** (alimer-style; spec §5.4). New `flags-detect` pipeline step lands between `libraryimport` and `guid-substitute`. New `policy/flags-enum-roster.json` carries the family-keyed allow-list. Core regen gains `[Flags]` on every Core enum qualified by §5.4: `SDL_MessageBoxFlags`, `SDL_MessageBoxButtonFlags`, `SDL_RendererFlags`, and `SDL_WindowFlags` via suffix rule; `SDL_Keymod`, `SDL_BlendMode`, `SDL_GLcontextFlag`, `SDL_RendererFlip`, and `SDL_TextureModulate` via allow-list. Image regen gains `[Flags]` on `IMG_InitFlags` (delivers Item 2 success criterion #1 as side effect). **`SDL_bool` MUST NOT gain `[Flags]`** — this is the headline test that validates Option B rejection of value-pattern heuristics.

**Spec sections:** §5.4 (`[Flags]` detection — new 7th postprocess step), §6 success criterion #1 (carve-out for `[Flags]` additions).
**Constitution sections:** §"Enums" (auto-decoration policy: name-suffix + allow-list; heuristics over bit values alone rejected); §"Generation Determinism Contract" §Determinism Inputs (roster JSON is single source of truth for postprocess data input).

**Code anchors:**

- New file: `spikes/binding-generators/clangsharp/policy/flags-enum-roster.json`.
- New file: `spikes/binding-generators/clangsharp/postprocess/FlagsAttributeRewriter.cs`.
- New file: `spikes/binding-generators/clangsharp/postprocess/FlagsEnumRosterLoader.cs` (helper).
- Modify: `spikes/binding-generators/clangsharp/postprocess/Program.cs` (already extended with `"flags-detect"` mode in S1-5 Task 5.3.1; S1-6 adds the actual case block).
- Modify: `spikes/binding-generators/clangsharp/generate_bindings.py:1206-1212` — pipeline order (insert `flags-detect` after `libraryimport`).

### Task 6.1: Create policy/flags-enum-roster.json

**Files:**

- Create: `spikes/binding-generators/clangsharp/policy/flags-enum-roster.json`

- [ ] **Step 6.1.1: Write the family-keyed allow-list JSON**

  File content:

  ```json
  {
    "schema_version": "2.0",
    "last_audited": "2026-05-25",
    "audit_method": "Name-suffix + manual allow-list per Constitution §\"Enums\" auto-decoration policy. Power-of-two value heuristic rejected (Constitution L448 friction; SDL_bool false-positive; no peer validation — only alimer-bindings-sdl auto-detects, and uses identical name-suffix + allow-list approach).",
    "families": {
      "core": {
        "library_version": "2.32.10",
        "allow_list": [
          {"name": "SDL_Keymod", "header": "SDL_keycode.h",
           "reason": "Composite-alias bitmask (KMOD_CTRL = KMOD_LCTRL | KMOD_RCTRL); Constitution Stage 1 flag. Caught by allow-list because composite OR values would defeat any value-pattern detector."},
          {"name": "SDL_BlendMode", "header": "SDL_blendmode.h",
           "reason": "Bitmask via SDL_BLENDMODE_* values; aligned with alimer-bindings-sdl precedent (CsCodeGenerator.Enum.cs:264 isBitmask hardcoded list)."},
          {"name": "SDL_GLcontextFlag", "header": "SDL_video.h",
           "reason": "Pure power-of-two bitmask but caught by allow-list for predictability; Constitution Stage 1 flag."},
          {"name": "SDL_RendererFlip", "header": "SDL_render.h",
           "reason": "Bitwise composable flip flags (HORIZONTAL | VERTICAL); Constitution Stage 1 flag."},
          {"name": "SDL_TextureModulate", "header": "SDL_render.h",
           "reason": "Bitmask via SDL_TEXTUREMODULATE_NONE/COLOR/ALPHA — composable color+alpha modulation."}
        ]
      },
      "image": { "library_version": "2.8.8", "allow_list": [] },
      "ttf":   { "library_version": "2.24.0", "allow_list": [] },
      "mixer": { "library_version": "2.8.1", "allow_list": [] },
      "gfx":   { "library_version": "1.0.4", "allow_list": [] }
    }
  }
  ```

  **Symmetry note:** schema identical to `opaque-handle-roster.json` family-keyed shape (top-level `schema_version`, `last_audited`, `audit_method`, then `families` map with per-family `library_version` + name list).

  Note: `IMG_InitFlags` and `MIX_InitFlags` are **not** listed — they are caught by the `Flags` suffix rule (no roster entry needed).

- [ ] **Step 6.1.2: Validate JSON**

  ```pwsh
  python -c "import json; json.load(open('spikes/binding-generators/clangsharp/policy/flags-enum-roster.json'))"
  ```

  Expected: no output (valid JSON).

### Task 6.2: Add failing tests for FlagsAttributeRewriter

**Files:**

- Modify: `spikes/binding-generators/clangsharp/postprocess/PostProcessSelfTests.cs`

- [ ] **Step 6.2.1: Append 7 detection-rule test fixtures**

  Append to `PostProcessSelfTests.Run`:

  ```csharp
          // Tests 8-14: FlagsAttributeRewriter detection rules per spec §5.4 (Option B alimer-style).

          static (string Input, string EnumName, bool ExpectFlags, string Description) MakeEnumFixture(
              string enumName, string members)
          {
              var input = $$"""
  namespace SDL2
  {
      public enum {{enumName}}
      {
  {{members}}
      }
  }
  """;
              return (input, enumName, true, "");
          }

          var coreAllowList = FlagsEnumRosterLoader.LoadForFamily(
              FlagsEnumRosterLoader.ResolveRosterPath(),
              "core");

          // Test 8: IMG_InitFlags via suffix rule (no allow-list entry, but EndsWith("Flags") fires)
          AssertFlagsDecoration(
              "IMG_InitFlags",
              "        IMG_INIT_JPG  = 0x00000001,\n        IMG_INIT_PNG  = 0x00000002,",
              new HashSet<string>(StringComparer.Ordinal), // empty allow-list (Image family)
              expectFlags: true,
              testId: "Test 8 (suffix: IMG_InitFlags)",
              failures);

          // Test 9: SDL_Keymod via allow-list rule (no suffix match; loaded from roster)
          AssertFlagsDecoration(
              "SDL_Keymod",
              "        KMOD_NONE = 0x0000,\n        KMOD_LSHIFT = 0x0001,\n        KMOD_CTRL = KMOD_LCTRL | KMOD_RCTRL,",
              coreAllowList,
              expectFlags: true,
              testId: "Test 9 (allow-list: SDL_Keymod composite)",
              failures);

          // Test 10: SDL_bool — CRITICAL. Must NOT gain [Flags]. Values 0+1; no suffix; not in allow-list.
          AssertFlagsDecoration(
              "SDL_bool",
              "        SDL_FALSE = 0,\n        SDL_TRUE = 1,",
              coreAllowList,
              expectFlags: false,
              testId: "Test 10 (blocked: SDL_bool — Option B safety)",
              failures);

          // Test 11: SDL_HitTestResult — sequential int, no suffix, not in allow-list. Must NOT gain [Flags].
          AssertFlagsDecoration(
              "SDL_HitTestResult",
              "        SDL_HITTEST_NORMAL,\n        SDL_HITTEST_DRAGGABLE,",
              coreAllowList,
              expectFlags: false,
              testId: "Test 11 (blocked: SDL_HitTestResult sequential int)",
              failures);

          // Test 12: Already-[Flags] enum — idempotent (rewriter does not double-decorate)
          var prefixedInput = """
  namespace SDL2
  {
      [System.Flags]
      public enum SDL_RendererFlags
      {
          SDL_RENDERER_SOFTWARE = 0x1,
      }
  }
  """;
          var prefixedRewriter = new FlagsAttributeRewriter(coreAllowList);
          var prefixedRoot = (CompilationUnitSyntax)prefixedRewriter.Visit(CSharpSyntaxTree.ParseText(prefixedInput).GetCompilationUnitRoot())!;
          var prefixedOutput = prefixedRoot.ToFullString();
          var flagsCount = System.Text.RegularExpressions.Regex.Matches(prefixedOutput, @"\[(?:System\.)?Flags(?:Attribute)?\]").Count;
          if (flagsCount != 1)
              failures.Add($"Test 12 (idempotent): expected exactly 1 [Flags] attribute, got {flagsCount}");

          // Test 13: SDL_RendererFlags via suffix (alimer-precedent: also in allow-list but suffix fires first)
          AssertFlagsDecoration(
              "SDL_RendererFlags",
              "        SDL_RENDERER_SOFTWARE = 0x1,\n        SDL_RENDERER_ACCELERATED = 0x2,",
              coreAllowList,
              expectFlags: true,
              testId: "Test 13 (suffix: SDL_RendererFlags)",
              failures);

          // Test 14: Suffix rule case-sensitivity — "flags" (lowercase) does NOT trigger
          AssertFlagsDecoration(
              "SDL_someflags",
              "        VALUE_A = 0x1,\n        VALUE_B = 0x2,",
              coreAllowList,
              expectFlags: false,
              testId: "Test 14 (suffix case-sensitivity: lowercase 'flags' does not trigger)",
              failures);

          // Test 15: Cross-family wiring — Image family loads EMPTY allow_list, but
          // IMG_InitFlags still gains [Flags] via the suffix rule. This is the integration
          // assertion that actually proves Item 2 delivery (IMG_InitFlags [Flags] addition)
          // via the family wiring, not just the rewriter's name match. Verifies:
          // (a) FlagsEnumRosterLoader.LoadForFamily(rosterPath, "image") returns empty.
          // (b) FlagsAttributeRewriter with empty allow-list + suffix rule still catches IMG_InitFlags.
          var imageAllowList = FlagsEnumRosterLoader.LoadForFamily(
              FlagsEnumRosterLoader.ResolveRosterPath(),
              "image");
          if (imageAllowList.Count != 0)
          {
              failures.Add($"Test 15 setup: families.image.allow_list expected empty, got {imageAllowList.Count} entries");
          }
          AssertFlagsDecoration(
              "IMG_InitFlags",
              "        IMG_INIT_JPG  = 0x00000001,\n        IMG_INIT_PNG  = 0x00000002,",
              imageAllowList,   // empty — proves suffix rule fires independently of allow-list
              expectFlags: true,
              testId: "Test 15 (cross-family wiring: Image empty allow-list + IMG_InitFlags suffix → [Flags])",
              failures);
  ```

  And add the helper `AssertFlagsDecoration` near the bottom of the `PostProcessSelfTests` class:

  ```csharp
  private static void AssertFlagsDecoration(
      string enumName,
      string members,
      HashSet<string> allowList,
      bool expectFlags,
      string testId,
      List<string> failures)
  {
      var input = $$"""
  namespace SDL2
  {
      public enum {{enumName}}
      {
  {{members}}
      }
  }
  """;
      var rewriter = new FlagsAttributeRewriter(allowList);
      var root = (CompilationUnitSyntax)rewriter.Visit(CSharpSyntaxTree.ParseText(input).GetCompilationUnitRoot())!;
      var output = root.ToFullString();
      var hasFlags = output.Contains("[Flags]") || output.Contains("[System.Flags]") || output.Contains("[FlagsAttribute]");
      if (hasFlags != expectFlags)
      {
          failures.Add($"{testId}: expected [Flags]={expectFlags} on {enumName}, got {hasFlags}. Output:\n{output}");
      }
  }
  ```

- [ ] **Step 6.2.2: Run self-test, verify it FAILS (build error)**

  ```pwsh
  dotnet run --project spikes/binding-generators/clangsharp/postprocess/Janset.SDL2.PostProcess.csproj -c Release -- --self-test
  ```

  Expected: compile errors — `FlagsAttributeRewriter` and `FlagsEnumRosterLoader` types do not yet exist. This validates that the tests assert on the new API surface.

### Task 6.3: Create FlagsEnumRosterLoader.cs

**Files:**

- Create: `spikes/binding-generators/clangsharp/postprocess/FlagsEnumRosterLoader.cs`

- [ ] **Step 6.3.1: Write the loader**

  File content:

  ```csharp
  using System.Text.Json;

  namespace Janset.SDL2.PostProcess;

  /// <summary>
  /// Loads per-family `[Flags]` allow-lists from
  /// <c>spikes/binding-generators/clangsharp/policy/flags-enum-roster.json</c>.
  /// Mirrors the family-keyed schema discipline of <see cref="OpaqueHandleEmitRewriter.LoadRoster"/>.
  /// </summary>
  internal static class FlagsEnumRosterLoader
  {
      public static HashSet<string> LoadForFamily(string rosterPath, string family)
      {
          if (!File.Exists(rosterPath))
          {
              throw new FileNotFoundException(
                  $"Flags-enum roster not found at: {rosterPath}. " +
                  "Expected at <repo>/spikes/binding-generators/clangsharp/policy/flags-enum-roster.json.",
                  rosterPath);
          }

          using var doc = JsonDocument.Parse(File.ReadAllText(rosterPath));
          var familyEntry = doc.RootElement.GetProperty("families").GetProperty(family);

          var allowList = new HashSet<string>(StringComparer.Ordinal);
          foreach (var entry in familyEntry.GetProperty("allow_list").EnumerateArray())
          {
              allowList.Add(entry.GetProperty("name").GetString()!);
          }
          return allowList;
      }

      public static string ResolveRosterPath()
      {
          var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
          while (dir != null)
          {
              var candidate = Path.Combine(dir.FullName, "spikes", "binding-generators", "clangsharp", "policy", "flags-enum-roster.json");
              if (File.Exists(candidate)) return candidate;
              dir = dir.Parent;
          }
          throw new FileNotFoundException(
              "Could not locate flags-enum-roster.json by walking ancestors. " +
              "Expected at <repo>/spikes/binding-generators/clangsharp/policy/flags-enum-roster.json.");
      }
  }
  ```

### Task 6.4: Create FlagsAttributeRewriter.cs

**Files:**

- Create: `spikes/binding-generators/clangsharp/postprocess/FlagsAttributeRewriter.cs`

- [ ] **Step 6.4.1: Write the rewriter**

  File content:

  ```csharp
  using Microsoft.CodeAnalysis;
  using Microsoft.CodeAnalysis.CSharp;
  using Microsoft.CodeAnalysis.CSharp.Syntax;

  namespace Janset.SDL2.PostProcess;

  /// <summary>
  /// Adds <c>[Flags]</c> to enum declarations matching either:
  ///   (a) name suffix rule — <c>EndsWith("Flags", StringComparison.Ordinal)</c>, or
  ///   (b) family-keyed allow-list rule — name appears in the family's section of
  ///       <c>policy/flags-enum-roster.json</c>.
  ///
  /// Value-pattern blind. Constitution §"Enums" auto-decoration policy explicitly
  /// rejects heuristics over bit values alone (SDL_bool false-positive risk).
  /// Composed alias values (KMOD_CTRL = KMOD_LCTRL | KMOD_RCTRL) are preserved
  /// as enum members; bitmask semantics flow from the allow-list entry, not value
  /// analysis.
  ///
  /// Peer validation: alimer-bindings-sdl uses the same name-suffix + hardcoded
  /// allow-list approach (CsCodeGenerator.Enum.cs:256-264). ppy/SDL2-CS use manual
  /// companion-file [Flags] annotation. Silk.NET 2.X dropped [Flags] entirely.
  /// </summary>
  internal sealed class FlagsAttributeRewriter : CSharpSyntaxRewriter
  {
      private readonly HashSet<string> _allowList;

      public FlagsAttributeRewriter(HashSet<string> allowList)
      {
          _allowList = new HashSet<string>(allowList, StringComparer.Ordinal);
      }

      public bool AnyChanges { get; private set; }

      public void Reset() => AnyChanges = false;

      public override SyntaxNode? VisitEnumDeclaration(EnumDeclarationSyntax node)
      {
          if (AlreadyHasFlagsAttribute(node)) return node;

          var name = node.Identifier.ValueText;
          var qualifies =
              name.EndsWith("Flags", StringComparison.Ordinal) ||
              _allowList.Contains(name);

          if (!qualifies) return node;

          AnyChanges = true;
          return node.AddAttributeLists(BuildFlagsAttributeList(node));
      }

      private static bool AlreadyHasFlagsAttribute(EnumDeclarationSyntax node) =>
          node.AttributeLists
              .SelectMany(al => al.Attributes)
              .Any(attr => attr.Name.ToString() is "Flags" or "FlagsAttribute" or "System.Flags" or "System.FlagsAttribute");

      private static AttributeListSyntax BuildFlagsAttributeList(EnumDeclarationSyntax target)
      {
          var attribute = SyntaxFactory.Attribute(SyntaxFactory.ParseName("Flags"));
          var list = SyntaxFactory.AttributeList(SyntaxFactory.SingletonSeparatedList(attribute));
          // Inherit the enum's leading trivia to preserve indentation/blank lines.
          return list.WithLeadingTrivia(target.GetLeadingTrivia())
                     .WithTrailingTrivia(SyntaxFactory.EndOfLine(Environment.NewLine));
      }
  }
  ```

### Task 6.5: Add flags-detect case to Program.cs

**Files:**

- Modify: `spikes/binding-generators/clangsharp/postprocess/Program.cs`

**Atomic wiring.** S1-5 intentionally did NOT add `flags-detect` to the allowlist. This slice adds the allowlist entry AND the case body in the same edit pair (Steps 6.5.1 + 6.5.2) so no intermediate state exists where the postprocess accepts `flags-detect` but throws in the default case.

- [ ] **Step 6.5.1: Add `flags-detect` to the mode allowlist + usage banner**

  At `Program.cs:49`, replace the allowlist check:

  ```csharp
  if (args.Length < 2 || args[0] is not ("strip-varargs" or "libraryimport" or "platform-delta" or "guid-substitute" or "clong-dispatch" or "uniform-opaque"))
  ```

  with:

  ```csharp
  if (args.Length < 2 || args[0] is not ("strip-varargs" or "libraryimport" or "platform-delta" or "guid-substitute" or "clong-dispatch" or "flags-detect" or "uniform-opaque"))
  ```

  At `Program.cs:51`, update the usage banner string to include `"flags-detect"` in the mode list.

- [ ] **Step 6.5.2: Add the case block (atomic with Step 6.5.1)**

  After the `case "uniform-opaque":` block in the switch (around L126), add:

  ```csharp
      case "flags-detect":
      {
          var rosterPath = FlagsEnumRosterLoader.ResolveRosterPath();
          var family = ResolveFamilyFromOutputDir(outputDir);
          var allowList = FlagsEnumRosterLoader.LoadForFamily(rosterPath, family);
          Console.WriteLine($"flags-detect: family={family}, allow-list size={allowList.Count}");
          var r = new FlagsAttributeRewriter(allowList);
          rewriter = r;
          hasChanges = () => r.AnyChanges;
          resetRewriter = r.Reset;
          break;
      }
  ```

  Note: `ResolveFamilyFromOutputDir` was added in S1-4 Task 4.10.2. It is reused here.

  Steps 6.5.1 and 6.5.2 MUST land in the same working-tree edit (one logical change). Do NOT build between them — Step 6.5.1 alone leaves a state where the allowlist accepts `flags-detect` but the switch's default arm throws.

- [ ] **Step 6.5.3: Update top-of-file comment block (L27-46)**

  Insert between the `guid-substitute` and `clong-dispatch` comment lines:

  ```
  // flags-detect      : Item 1 S1-6 — adds [Flags] to enum declarations matching
  //                     name-suffix rule (EndsWith("Flags")) OR family-keyed
  //                     allow-list in policy/flags-enum-roster.json. Value-pattern
  //                     blind per Constitution §"Enums" auto-decoration policy
  //                     (heuristics over bit values alone explicitly rejected).
  ```

### Task 6.6: Wire flags-detect into generate_bindings.py pipeline

**Files:**

- Modify: `spikes/binding-generators/clangsharp/generate_bindings.py:1206-1228`

- [ ] **Step 6.6.1: Insert flags-detect step after libraryimport**

  After the `libraryimport` block (currently L1206-1212), and before the `guid-substitute` block (L1221-1228), insert:

  ```python
      # Item 1 S1-6 [Flags] auto-decoration: alimer-style name-suffix + family-keyed
      # roster allow-list per Constitution §"Enums" auto-decoration policy. Applied to
      # both Compat and Modern because [Flags] is enum metadata, not codegen-specific.
      # Position: after libraryimport so the rewriter sees final attribute shape; before
      # guid-substitute for readability (no functional dependency).
      if args.execute:
          print("--- postprocess: flags-detect (all codegens) ---")
          for codegen in codegen_passes:
              for family in selected:
                  exit_code = run_postprocess(repo, family, spike_root, "flags-detect", codegen)
                  if exit_code != 0:
                      postprocess_failures += 1
                      print(f"WARNING: flags-detect postprocess for {family}/{codegen} returned exit {exit_code}")
  ```

- [ ] **Step 6.6.2: Update `run_postprocess` docstring mode list (L706-712)**

  Add `flags-detect` to the mode list in the docstring.

### Task 6.7: Build + run self-test, verify all 7 tests PASS

- [ ] **Step 6.7.1: Build the postprocess project**

  ```pwsh
  dotnet build spikes/binding-generators/clangsharp/postprocess/Janset.SDL2.PostProcess.csproj -c Release
  ```

  Expected: 0 errors.

- [ ] **Step 6.7.2: Run self-test**

  ```pwsh
  dotnet run --project spikes/binding-generators/clangsharp/postprocess/Janset.SDL2.PostProcess.csproj -c Release -- --self-test
  ```

  Expected: `postprocess self-test: PASS`.

  **Critical:** Test 10 (SDL_bool blocked) must PASS. This is the headline test that validates Option B rejection of value-pattern heuristics. If Test 10 fails, the heuristic is incorrectly decorating SDL_bool — investigate.

### Task 6.8: Core regen with flags-detect — audited [Flags] additions

**Files:** none modified (verification only).

- [ ] **Step 6.8.1: Regenerate --family all**

  ```pwsh
  python spikes/binding-generators/clangsharp/generate_bindings.py --family all --execute --vcpkg-triplet x64-windows-hybrid --use-platform-header-shims
  ```

  Expected: exits 0.

- [ ] **Step 6.8.2: Inspect Core diff — expected [Flags] additions**

  ```pwsh
  git diff --ignore-cr-at-eol spikes/binding-generators/clangsharp/src/Janset.SDL2.Core/Generated/ | Select-String -Pattern "^\+.*\[(System\.)?Flags\]" | ForEach-Object { $_.Line }
  ```

  Expected output: exactly the `[Flags]` additions on these Core enums (each appears in both Compat and Modern trees, so 18 lines total):
  - `SDL_MessageBoxFlags` (suffix rule) — `SDL_messagebox.g.cs`
  - `SDL_MessageBoxButtonFlags` (suffix rule) — `SDL_messagebox.g.cs`
  - `SDL_RendererFlags` (suffix rule) — `SDL_render.g.cs`
  - `SDL_WindowFlags` (suffix rule) — `SDL_video.g.cs`
  - `SDL_Keymod` (allow-list) — `SDL_keycode.g.cs`
  - `SDL_BlendMode` (allow-list) — `SDL_blendmode.g.cs`
  - `SDL_GLcontextFlag` (allow-list) — `SDL_video.g.cs`
  - `SDL_RendererFlip` (allow-list) — `SDL_render.g.cs`
  - `SDL_TextureModulate` (allow-list) — `SDL_render.g.cs`

- [ ] **Step 6.8.3: Verify SDL_bool does NOT gain [Flags]**

  ```pwsh
  git diff --ignore-cr-at-eol spikes/binding-generators/clangsharp/src/Janset.SDL2.Core/Generated/ | Select-String -Pattern "SDL_bool" -Context 3,3
  ```

  Expected: 0 matches OR matches only show context lines without any `+[Flags]` / `+[System.Flags]` line on or adjacent to `SDL_bool`. If either appears near `SDL_bool` — **CRITICAL BUG**, the heuristic incorrectly decorated SDL_bool. Investigate immediately.

- [ ] **Step 6.8.4: Confirm no other content changes**

  ```pwsh
  git diff --ignore-cr-at-eol spikes/binding-generators/clangsharp/src/Janset.SDL2.Core/Generated/ | Select-String -Pattern "^\+" | Where-Object { $_ -notmatch "\[(System\.)?Flags\]" -and $_ -notmatch "^\+\+\+" }
  ```

  Expected: 0 lines (no other added content). If any: the rewriter is touching unintended areas — investigate.

### Task 6.9: Image regen — IMG_InitFlags gains [Flags] (delivers Item 2)

- [ ] **Step 6.9.1: Inspect Image diff**

  ```pwsh
  git diff --ignore-cr-at-eol spikes/binding-generators/clangsharp/src/Janset.SDL2.Image/Generated/
  ```

  Expected: exactly 2 `+[Flags]` / `+[System.Flags]` additions on `IMG_InitFlags` (one in `Generated/Compat/SDL_image.g.cs`, one in `Generated/Modern/SDL_image.g.cs`). No other content changes.

  This delivers Item 2 success criterion #1 (`IMG_InitFlags` emits with `[Flags]` in both Compat and Modern) as a side effect of Item 1.

### Task 6.10: Common: Determinism Contract Verification

See "Per-slice common requirements" §Common: Determinism Contract Verification — execute D.1 through D.5.

**Note:** D.1 (idempotency) compares against post-S1-6 state, not against pre-S1-6. The `[Flags]` additions are intentional output changes per spec §6 success criterion #1 carve-out. D.1 verifies that running regen twice in a row produces identical output (no further drift).

### Task 6.11: Common: Slopwatch

See "Per-slice common requirements" §Common: Slopwatch. No file deletions in this slice — analyze only, baseline rebuild not required (two new files added, but slopwatch picks them up on next analyze).

### Task 6.12: Commit (two-commit shape)

- [ ] **Step 6.12.1: Commit the new postprocess step + roster**

  ```pwsh
  git add spikes/binding-generators/clangsharp/policy/flags-enum-roster.json spikes/binding-generators/clangsharp/postprocess/FlagsAttributeRewriter.cs spikes/binding-generators/clangsharp/postprocess/FlagsEnumRosterLoader.cs spikes/binding-generators/clangsharp/postprocess/Program.cs spikes/binding-generators/clangsharp/postprocess/PostProcessSelfTests.cs spikes/binding-generators/clangsharp/generate_bindings.py
  git commit -m "$(cat <<'EOF'
  feat(spike): add FlagsAttributeRewriter + flags-detect postprocess step (S1-6a)

  - New family-keyed allow-list at policy/flags-enum-roster.json (schema 2.0
    mirroring opaque-handle-roster.json discipline). Core entries: SDL_Keymod,
    SDL_BlendMode, SDL_GLcontextFlag, SDL_RendererFlip, SDL_TextureModulate.
    Satellites: empty allow-list (suffix rule handles IMG_InitFlags etc.).
  - FlagsAttributeRewriter applies name-suffix rule (EndsWith("Flags")) OR
    family allow-list lookup. Value-pattern blind — Constitution §"Enums"
    auto-decoration policy explicitly rejects heuristics over bit values.
  - FlagsEnumRosterLoader: per-family allow-list parse, ancestor-walk roster
    resolution mirroring OpaqueHandleEmitRewriter.LoadRoster shape.
  - Program.cs: case "flags-detect" + comment block + family resolution.
  - generate_bindings.py: inserts flags-detect after libraryimport, before
    guid-substitute (applies to both Compat and Modern).
  - PostProcessSelfTests (Tests 8-14): suffix rule, allow-list rule, SDL_bool
    blocked (Option B safety — headline test), SDL_HitTestResult blocked,
    idempotent, case-sensitivity.

  Refs: spec §5.4, Constitution §"Enums" auto-decoration policy.
  EOF
  )"
  ```

- [ ] **Step 6.12.2: Commit the audited regen [Flags] additions**

  ```pwsh
  git add spikes/binding-generators/clangsharp/src/Janset.SDL2.Core/Generated/ spikes/binding-generators/clangsharp/src/Janset.SDL2.Image/Generated/
  git commit -m "$(cat <<'EOF'
  chore(spike): regenerate Core+Image after flags-detect (S1-6b)

  Audited [Flags] attribute additions per spec §6 success criterion #1 carve-out:

  Core (9 enums; both Compat + Modern trees):
  - SDL_MessageBoxFlags (suffix rule)
  - SDL_MessageBoxButtonFlags (suffix rule)
  - SDL_RendererFlags (suffix rule)
  - SDL_WindowFlags (suffix rule)
  - SDL_Keymod (allow-list — composite-alias bitmask)
  - SDL_BlendMode (allow-list — alimer-precedent)
  - SDL_GLcontextFlag (allow-list — Stage 1 flag)
  - SDL_RendererFlip (allow-list — Stage 1 flag)
  - SDL_TextureModulate (allow-list — bitmask via NONE/COLOR/ALPHA)

  Image (1 enum; both trees):
  - IMG_InitFlags (suffix rule) — delivers Item 2 success criterion #1.

  SDL_bool verified NOT to gain [Flags] (Option B safety test PASS — value-pattern
  heuristic correctly rejected; only suffix + allow-list catch fires).

  No other content changes. Constitution §"Generation Determinism Contract"
  D.1-D.5 all PASS.

  Refs: spec §5.4 + §6 #1 carve-out, Constitution §"Enums" auto-decoration policy + §"Current
  SDL2.Core ABI Status" point 7 (now satisfied: designation -> emission).
  EOF
  )"
  ```

**Slice exit evidence:**

- ✓ `policy/flags-enum-roster.json` valid family-keyed schema 2.0.
- ✓ `FlagsAttributeRewriter.cs` + `FlagsEnumRosterLoader.cs` created.
- ✓ Program.cs `case "flags-detect"` wired; generate_bindings.py pipeline updated.
- ✓ 8 self-test fixtures PASS (suffix, allow-list, SDL_bool blocked, sequential blocked, idempotent, trivia preservation, case-sensitivity, image empty allow-list).
- ✓ Core regen adds `[Flags]` to exactly 9 enums (4 suffix + 5 allow-list); no other content changes.
- ✓ Image regen adds `[Flags]` to `IMG_InitFlags` (delivers Item 2 criterion #1).
- ✓ `SDL_bool` did NOT gain `[Flags]` (Option B safety verified).
- ✓ Determinism Contract D.1–D.5 PASS.
- ✓ Slopwatch clean.
- ✓ Two commits (rewriter+pipeline; regen with [Flags] additions).

---

## S1-7 — Item 1 exit verification

**Goal:** No code change. Full end-to-end regen + oracle + multi-TFM + slopwatch + final diff inspection. Confirms the spec's success criteria 1–10 (with #1 carve-out per §6).

**Spec sections:** §6 (Success Criteria).
**Constitution sections:** §"Generation Determinism Contract" §Verification Contract (this slice executes the contract end-to-end as Item 1's gate).

### Task 7.1: Clean regen with --family all

- [ ] **Step 7.1.1: Clean output + regenerate all families**

  ```pwsh
  python spikes/binding-generators/clangsharp/generate_bindings.py --family all --execute --vcpkg-triplet x64-windows-hybrid --use-platform-header-shims
  ```

  Expected: exits 0; writes `clangsharp-production.md` report; deletes and regenerates `Janset.SDL2.Core/Generated/` and `Janset.SDL2.Image/Generated/` from scratch.

- [ ] **Step 7.1.2: Capture pre-comparison snapshot**

  ```pwsh
  git status --short
  ```

  Expected: changes only within `spikes/binding-generators/clangsharp/src/Janset.SDL2.{Core,Image}/Generated/` (Core/Image regen) and possibly the `output/reports/` report file. No changes to other family directories (TTF/Mixer/GFX directories should not exist at all yet — Items 3/4/5 create them).

### Task 7.2: Per-family regen parity check

- [ ] **Step 7.2.1: Stash --family all output**

  ```pwsh
  git stash push spikes/binding-generators/clangsharp/src/Janset.SDL2.Core/Generated/ spikes/binding-generators/clangsharp/src/Janset.SDL2.Image/Generated/
  ```

- [ ] **Step 7.2.2: Run per-family sequential**

  ```pwsh
  python spikes/binding-generators/clangsharp/generate_bindings.py --family core --execute --vcpkg-triplet x64-windows-hybrid --use-platform-header-shims
  python spikes/binding-generators/clangsharp/generate_bindings.py --family image --execute --vcpkg-triplet x64-windows-hybrid --use-platform-header-shims
  ```

- [ ] **Step 7.2.3: Compare per-family output to stashed --family all**

  ```pwsh
  git stash show -p | git apply --check
  ```

  Expected: clean apply check (no conflicts). Then:

  ```pwsh
  git diff --ignore-cr-at-eol spikes/binding-generators/clangsharp/src/Janset.SDL2.Core/Generated/ spikes/binding-generators/clangsharp/src/Janset.SDL2.Image/Generated/
  ```

  Expected: empty diff between per-family run and stashed `--family all` run. Drop the stash:

  ```pwsh
  git stash drop
  ```

### Task 7.3: Family isolation verification

- [ ] **Step 7.3.1: --family core does NOT touch Image**

  ```pwsh
  python spikes/binding-generators/clangsharp/generate_bindings.py --family core --execute --vcpkg-triplet x64-windows-hybrid --use-platform-header-shims
  git status --short spikes/binding-generators/clangsharp/src/Janset.SDL2.Image/Generated/
  ```

  Expected: 0 lines (Image untouched).

- [ ] **Step 7.3.2: --family image does NOT touch Core**

  ```pwsh
  python spikes/binding-generators/clangsharp/generate_bindings.py --family image --execute --vcpkg-triplet x64-windows-hybrid --use-platform-header-shims
  git status --short spikes/binding-generators/clangsharp/src/Janset.SDL2.Core/Generated/
  ```

  Expected: 0 lines (Core untouched).

- [ ] **Step 7.3.3: Re-run --family all to restore the committed regen state**

  ```pwsh
  python spikes/binding-generators/clangsharp/generate_bindings.py --family all --execute --vcpkg-triplet x64-windows-hybrid --use-platform-header-shims
  ```

### Task 7.4: Multi-TFM build across the full spike solution

- [ ] **Step 7.4.1: Build the spike solution**

  ```pwsh
  dotnet build spikes/binding-generators/clangsharp/Janset.SDL2.ClangSharpSpike.slnx -c Release
  ```

  Expected last lines:

  ```
  Build succeeded.
      0 Warning(s)
      0 Error(s)
  ```

  This covers all 5 TFMs (net10.0, net9.0, net8.0, netstandard2.0, net462) × (Janset.SDL2.Core + Janset.SDL2.Image + AbiTests + postprocess project).

### Task 7.5: Oracle 5-family report

- [ ] **Step 7.5.1: Run oracle with all 5 families + write-report**

  ```pwsh
  dotnet run --file spikes/binding-generators/clangsharp/oracle.cs -- --family sdl2-core --family sdl2-image --family sdl2-ttf --family sdl2-mixer --family sdl2-gfx --write-report
  ```

  Expected: report written to `spikes/binding-generators/output/reports/oracle-evidence-clangsharp.md`.

- [ ] **Step 7.5.2: Verify Core + Image have 0 findings across Priority C categories**

  Open the report. Verify under each family's §"Raw ABI Constitution Checks":
  - `platform-sensitive-wchar`: 0 findings
  - `platform-sensitive-long`: 0 findings
  - `deferred-layout-sdl-rwops`: 0 findings
  - `deferred-layout-sdl-syswminfo`: 0 findings
  - `deferred-layout-sdl-syswmmsg`: 0 findings
  - `duplicate-tag-typedef`: 0 findings
  - No Hard Bug section.

- [ ] **Step 7.5.3: Verify TTF/Mixer/GFX show Missing**

  Verify their sections list ClangSharp Compat/Modern as `Status: missing`, all counts = 0. No `family-namespace-drift`, no `OutOfScope`, no unhandled-exception entries.

### Task 7.6: AbiTests runtime smoke

- [ ] **Step 7.6.1: Run AbiTests across all 4 TFMs (Windows x64)**

  ```pwsh
  dotnet test spikes/binding-generators/clangsharp/tests/abi-tests/ -c Release
  ```

  Expected: all tests pass across `net10.0`, `net9.0`, `net8.0`, `net462`. The `SDL_ThreadID` + `SDL_GetThreadID` smoke tests confirm:
  - Modern path: `[LibraryImport] + CULong` Roslyn-emitted shape works at runtime.
  - Compat path: dispatcher + per-RID `[DllImport]` helpers work at runtime.

  Linux x64 docker pass is a **production gate**, not required for Item 1 exit. The `SDL_ThreadID` Linux x64 docker smoke is part of Priority C closure history; if needed, run:

  ```pwsh
  # OPTIONAL — Linux x64 docker validation (production gate)
  docker run --rm -v "${PWD}:/work" -w /work mcr.microsoft.com/dotnet/sdk:10.0 dotnet test spikes/binding-generators/clangsharp/tests/abi-tests/ -c Release -f net10.0
  ```

### Task 7.7: Slopwatch final clean

- [ ] **Step 7.7.1: Run slopwatch analyze**

  ```pwsh
  slopwatch analyze --fail-on warning --exclude "artifacts/**,external/**,vcpkg_installed/**,spikes/binding-generators/references/**,**/bin/**,**/obj/**"
  ```

  Expected: `Scan complete: 0 issue(s) found`.

### Task 7.8: Repo hygiene grep

- [ ] **Step 7.8.1: Verify no leftover compare_oracle references**

  Use the Grep tool with pattern `compare_oracle`, path repository root.

  Expected acceptable matches: this plan doc, spec, roadmap, historical commits. **Zero** matches in code/RSP/JSON.

- [ ] **Step 7.8.2: Verify no leftover threadid-dispatch references**

  Use the Grep tool with pattern `threadid-dispatch`, path repository root.

  Expected acceptable matches: docs only. **Zero** matches in code/RSP/JSON.

- [ ] **Step 7.8.3: Verify no leftover ThreadIdDualDispatchRewriter class references**

  Use the Grep tool with pattern `ThreadIdDualDispatchRewriter`, path repository root.

  Expected: docs only. **Zero** matches in `.cs` files.

### Task 7.9: Doc updates — closure record

**Files:**

- Modify: `spikes/binding-generators/docs/next-iteration-plan.md` (add Item 1 closed row).
- Modify: `spikes/binding-generators/docs/satellite-expansion-roadmap.md` §Implementation Order & Dependencies (mark Item 1 closed).

- [ ] **Step 7.9.1: Update next-iteration-plan.md slice progress table**

  Add a new row at the bottom of the Slice progress table (after the existing Slice 7 entry):

  ```markdown
  | Item 1 — Per-Library Generation Infrastructure | ✅ closed (date) | All 7 slices (S1-1 through S1-7) shipped. Constitution edits landed (5 sections); roster JSONs migrated to family-keyed schema 2.0; ClongDualDispatchRewriter Roslyn-mutated; FlagsAttributeRewriter alimer-style; oracle 5-family. Determinism Contract D.1–D.5 PASS. AbiTests PASS. Slopwatch clean. |
  ```

  Replace `(date)` with the actual closure date (commit date of S1-7).

- [ ] **Step 7.9.2: Update roadmap §Implementation Order & Dependencies**

  In `satellite-expansion-roadmap.md` §Implementation Order & Dependencies, change `Iteration 1: Item 1 (per-library infra)` to `Iteration 1: Item 1 (per-library infra) ✅ closed`. Items 2/3/4/5 are unblocked.

### Task 7.10: Plan archive / delete

Per `feedback_refactoring_doc_lifecycle` memory: per-slice temp docs delete after the slice ships. Two options:

**Option A — Delete:** the durable spec stays at `spikes/binding-generators/docs/items/item-1-per-library-generation-spec.md` as the historical reference; the per-slice plan is no longer load-bearing.

**Option B — Archive:** move to `spikes/binding-generators/docs/items/archive/`.

Choose Option A (matches memory `project_refactoring_doc_lifecycle`).

- [ ] **Step 7.10.1: Delete the plan via git rm**

  ```pwsh
  git rm spikes/binding-generators/docs/items/item-1-per-library-generation-plan.md
  ```

  Note: the spec doc remains. The closure record in `next-iteration-plan.md` + the roadmap is the durable historical reference.

### Task 7.11: Final exit-evidence checklist commit

- [ ] **Step 7.11.1: Stage doc updates + plan deletion**

  ```pwsh
  git status --short
  ```

  Expected:

  ```
  M  spikes/binding-generators/docs/next-iteration-plan.md
  M  spikes/binding-generators/docs/satellite-expansion-roadmap.md
  D  spikes/binding-generators/docs/items/item-1-per-library-generation-plan.md
  ```

- [ ] **Step 7.11.2: Final commit with success-criteria evidence table**

  ```bash
  git commit -m "$(cat <<'EOF'
  docs(spike): close Item 1 per-library generation infrastructure (S1-7)

  All 7 slices (S1-1 through S1-7) shipped. Spec §6 success criteria evidence:

  #1  --family all byte-identical EXCEPT audited [Flags] additions per §5.4
        (SDL_MessageBoxFlags, SDL_MessageBoxButtonFlags, SDL_RendererFlags,
         SDL_WindowFlags, SDL_Keymod, SDL_BlendMode, SDL_GLcontextFlag,
         SDL_RendererFlip, SDL_TextureModulate, IMG_InitFlags) — PASS
  #2  --family core + --family image sequential == --family all  — PASS
  #3  Multi-TFM build clean (5 TFMs × 4 projects)                — PASS
  #4  Oracle 5-family report; 0 findings on core+image           — PASS
  #5  FAMILY_CONFIG ttf/mixer/gfx exist + dormant in all          — PASS
  #6  DiscoverAutoDetectedHandles family-blind (TTF_Font/Mix_Music) — PASS
  #7  ClongDualDispatchRewriter Core regen + AbiTests PASS         — PASS
  #8  IMG_InitFlags emits [Flags] (Item 2 delivered via S1-6)      — PASS
  #9  Slopwatch clean                                              — PASS
  #10 compare_oracle.py removed; zero non-doc references          — PASS

  Family isolation: --family core does not touch Image's Generated/; --family
  image does not touch Core's. Cross-family handle pull preserved: Image's
  SDL_image.g.cs emits SDL_Renderer / SDL_RWops by-value via Core's roster
  entries pulled into the family-keyed loader.

  Constitution edits landed (5 sections): §"Opaque Handles" Auto-detect criterion
  + Canonical roster + Cross-family handle name resolution; §"Enums" auto-decoration
  policy; §"Current SDL2.Core ABI Status" point 7; NEW §"Generation Determinism
  Contract".

  Item 1 plan doc deleted per refactoring-doc lifecycle (spec doc remains as
  durable reference; closure record lives in next-iteration-plan.md + roadmap).

  Items 2/3/4/5 unblocked.

  Refs: spec §6, Constitution §"Generation Determinism Contract" §Verification Contract.
  EOF
  )"
  ```

**Slice exit evidence:**

- ✓ All 7 slices closed.
- ✓ Determinism Contract D.1–D.5 PASS end-to-end.
- ✓ Multi-TFM build clean across 5 TFMs × 4 projects.
- ✓ Oracle 5-family report clean on Core + Image; TTF/Mixer/GFX expected Missing.
- ✓ AbiTests PASS across Windows TFMs.
- ✓ Slopwatch clean.
- ✓ Repo hygiene: no leftover sunset/renamed references.
- ✓ Doc closure record landed in next-iteration-plan + roadmap.
- ✓ Plan doc deleted per refactoring-doc lifecycle.
- ✓ Items 2/3/4/5 unblocked.

**Exit evidence (record in commit/PR description):**

```
| Success criterion | Verification | Status |
|---|---|---|
| #1 --family all byte-identical to HEAD (EXCEPT audited [Flags] additions per §5.4) | git diff --ignore-cr-at-eol shows ONLY [Flags] attribute additions on SDL_MessageBoxFlags / SDL_MessageBoxButtonFlags / SDL_RendererFlags / SDL_WindowFlags / SDL_Keymod / SDL_BlendMode / SDL_GLcontextFlag / SDL_RendererFlip / SDL_TextureModulate / IMG_InitFlags; no other content changes | ☐ |
| #2 --family core + --family image == --family all | sequential regen diff empty | ☐ |
| #3 Multi-TFM build clean | dotnet build → 0/0 across 5 TFMs | ☐ |
| #4 Oracle 5-family report, 0 findings on core+image | report attached | ☐ |
| #5 FAMILY_CONFIG ttf/mixer/gfx exist + dormant in all | generate_bindings.py --self-test PASS | ☐ |
| #6 DiscoverAutoDetectedHandles finds TTF_Font/Mix_Music in synthetic | postprocess fixture | ☐ |
| #7 ClongDualDispatchRewriter byte-equivalent on Core | regen diff empty + AbiTests | ☐ |
| #8 IMG_InitFlags emits [Flags] (Item 2 verification surface) | Image diff shows attribute addition | ☐ |
| #9 Slopwatch clean | 0 issues | ☐ |
| #10 compare_oracle.py removed | git grep returns 0 non-historical | ☐ |
```

**Commit message shape:** `docs(spike): close Item 1 per-library generation infrastructure; archive execution plan`.

---

## After Item 1 ships

- Items 2/3/4/5 unblock per roadmap §Implementation Order & Dependencies.
- Item 2 is the quickest validation — regenerate Image, confirm `IMG_InitFlags` carries `[Flags]`. (Already half-verified in S1-6 if the diff lands clean.)
- Item 3 (GFX) is the next forward expansion — lowest risk, validates the multi-family pipeline.
- This plan doc deletes (per the refactoring-doc lifecycle memory). The durable spec stays; the durable closure record updates to [`../next-iteration-plan.md`](../next-iteration-plan.md) and the roadmap.

---

## Approval gate checklist (for each slice commit)

Per [`AGENTS.md`](../../../../AGENTS.md) §"Approval Gate":

- [ ] Deniz has said `başla` / `yap` / `proceed` / `go` / `apply` for the scope this commit covers.
- [ ] Commit message follows repo convention (`<type>(<scope>): <subject>`); references the slice ID (`S1-N`).
- [ ] No production code touched (this is spike scope; `src/Janset.SDL2.Core` proper is untouched).
- [ ] No CI/CD pipeline changes.
- [ ] No `.csproj` / `.sln` / `Directory.Build.props` / `vcpkg.json` / `build/manifest.json` changes (Item 1 stays within `spikes/binding-generators/`).
- [ ] Approval pinged again before pushing if the slice exceeds the approved scope.
