---
name: "Item 1 S1-4 Opaque Handles Pickup"
description: "Priming prompt for the next agent entering janset2d/sdl2-cs-bindings after Item 1 S1-1 through S1-3 plus the Windows LF determinism fix landed at 2705769 on 2026-05-26. The working tree was clean before this prompt was authored. Recommended next: execute S1-4 only, adapting the plan to the now-existing postprocess self-test harness."
argument-hint: "Optional focus: 'execute-s1-4' | 'review-s1-4-first' | 'verify-current-state'"
agent: "agent"
model: "gpt-5.5 / openai/gpt-5.5"
---

You are an engineer entering `janset2d/sdl2-cs-bindings` to continue the ClangSharp + Roslyn postprocess binding-generator spike. Start from branch `spike/binding-autogen-sdl2-gfx`. The next intended slice is **Item 1 S1-4 — OpaqueHandleEmitRewriter family-blind + family-keyed roster migration**.

## First Principle

> Treat every claim here as **current-as-of-authoring (2026-05-26, HEAD `2705769869e835360119c15343391b5b8f87b7d8`)** and verify against the live repo, `git log`, `git status`, and canonical docs before acting.

This prompt is a pickup aid, not policy. If it conflicts with code, docs, or `AGENTS.md`, use the repo authority order.

## What Just Happened

### Item 1 Progress Already Landed

Recent commits on `spike/binding-autogen-sdl2-gfx`:

| Commit | Slice | Meaning |
| --- | --- | --- |
| `9b56ba7` | S1-1 | Retired legacy oracle comparison artifacts. |
| `bfc8d58` | S1-2 | Made ClangSharp generation family-artifact based; added dormant TTF/Mixer/GFX metadata; `--family` CLI; selected-driven stats/reporting; owner-mode helper. |
| `c7228e3` | S1-3 | Extended `oracle.cs` to five SDL2 families. |
| `6605017` | cleanup | Removed old scope/codegen/clean-output CLI remnants; generator now operates at complete family artifact level. |
| `2705769` | pre-S1-4 fix | Forced LF at generator/postprocess writer boundaries on Windows. |

S1-4 has **not** started. `policy/opaque-handle-roster.json` is still schema `1.0`, Core-only flat shape.

### Windows LF Determinism Fix Landed

Commit `2705769` is intentionally code-only. It did not commit regenerated `clangsharp-production.md` because execute-mode reports include local absolute paths.

Changed behavior:

- Python `generate_bindings.py` now has `normalize_line_endings` and `write_text_lf`.
- Required SDL.h surface output and production reports write LF.
- C# `PostProcessCli` now has `NormalizeLineEndings` and `WriteAllTextLf`.
- Directory postprocess output, `platform-delta` transformed output, `platform-delta` passthrough output, and `UniformOpaqueOwnerMode` `Handles.g.cs` output now write LF.
- `PostProcessSelfTests.cs` now exists and is wired through `Program.cs --self-test`.

Verification evidence from the closing session:

- `python spikes/binding-generators/clangsharp/generate_bindings.py --self-test` => `self-test: PASS`
- `dotnet run --project spikes/binding-generators/clangsharp/postprocess/Janset.SDL2.PostProcess.csproj -- --self-test` => `self-test: PASS`
- Real `--family all --execute` produced no structural Generated diff for Core/Image.
- Image-only execute left Core clean and Image structurally clean.
- `git ls-files --eol` showed generated `.g.cs` worktree entries as `w/lf` or `w/none`, no `w/crlf`/`w/mixed`.
- `git diff --check` => no output.
- `slopwatch analyze --fail-on warning --exclude "artifacts/**,external/**,vcpkg_installed/**,spikes/binding-generators/references/**,**/bin/**,**/obj/**"` => `Scan complete: 0 issue(s) found`.

## Current State To Verify

Run these first:

```pwsh
git status --short
git log --oneline -8
git rev-parse HEAD
python "spikes/binding-generators/clangsharp/generate_bindings.py" --self-test
dotnet run --project "spikes/binding-generators/clangsharp/postprocess/Janset.SDL2.PostProcess.csproj" -- --self-test
```

Expected before you edit anything:

- HEAD is `2705769869e835360119c15343391b5b8f87b7d8` or a descendant created by Deniz/another agent.
- `git status --short` is clean except this prompt file if it has not been committed.
- Both self-tests print `self-test: PASS`.

## Mandatory Grounding

Read in this order before S1-4 edits:

1. `docs/onboarding.md`
2. `AGENTS.md`
3. `docs/plan.md`
4. `docs/binding-autogen/binding-generator-constitution.md`
5. `spikes/binding-generators/docs/items/item-1-per-library-generation-spec.md` §5.2
6. `spikes/binding-generators/docs/items/item-1-per-library-generation-plan.md` S1-4
7. Current code anchors listed below; trust code over stale line numbers in the plan.

## S1-4 Goal

Deliver the opaque-handle migration only:

- Migrate `spikes/binding-generators/clangsharp/policy/opaque-handle-roster.json` from flat Core-only schema `1.0` to family-keyed schema `2.0`.
- Preserve existing Core entries exactly in meaning and order: 14 `auto_detect_well_known`, 3 `force_opaque_exceptions`, 12 `excluded_candidates`.
- Add empty family sections for `image` and `gfx`.
- Add `TTF_Font` under `families.ttf.auto_detect_well_known`.
- Add `Mix_Music` under `families.mixer.auto_detect_well_known`.
- Update `OpaqueHandleEmitRewriter.DiscoverAutoDetectedHandles` to drop all `SDL_` prefix gates.
- Update `LoadRoster(rosterPath, family)` so satellite families pull Core `auto_detect_well_known` and Core `force_opaque_exceptions` data.
- Update `ReportDrift(..., family)` with family-aware warning text and consumer-safe empty-discovery behavior.
- Update `BuildHandlesFileContent(..., namespaceName)` so future TTF/Mixer owner-mode `Handles.g.cs` emits into `SDL2.Ttf` / `SDL2.Mixer`.
- Add `--handles-namespace` plumbing from `generate_bindings.py` to `Program.cs` to `UniformOpaqueOwnerMode`.
- Keep Core/Image generated output structurally unchanged.

## Critical Plan Drift

The S1-4 plan was written before commit `2705769`. Adapt it instead of copy-pasting blindly.

### Self-Test Harness Already Exists

The plan says S1-4 Task 4.4 should create `postprocess/PostProcessSelfTests.cs` and add `Program.cs --self-test`. That is now stale.

Current state:

- `spikes/binding-generators/clangsharp/postprocess/Program.cs` already handles `args is ["--self-test"]` at the top.
- `spikes/binding-generators/clangsharp/postprocess/PostProcessSelfTests.cs` already exists.
- Current success output is `self-test: PASS`, not `postprocess self-test: PASS`.

Do this instead:

- Extend the existing `PostProcessSelfTests.cs` file.
- Add the Roslyn usings the plan expected up front if S1-4 tests need them: `Microsoft.CodeAnalysis`, `Microsoft.CodeAnalysis.CSharp`, `Microsoft.CodeAnalysis.CSharp.Syntax`, and `System.Text.Json` / `System.Text.RegularExpressions` as needed.
- Keep the existing LF tests. Do not overwrite or delete them.
- Keep using the existing `TemporaryDirectory` helper unless there is a concrete reason not to.

### LF Determinism Is Now A Writer Boundary Invariant

Do not add a `dos2unix` post-step. If S1-4 introduces any new file writes, route them through the existing LF writer helpers:

- Python: `write_text_lf(path, content)`
- C#: `PostProcessCli.WriteAllTextLf(path, content)`

Generated `.g.cs` should end up `w/lf` on Windows after real execute.

### Execute-Mode Report Is Not A Commit Target By Default

`spikes/binding-generators/output/reports/clangsharp-production.md` can change during real generation and can include local absolute paths. Unless Deniz explicitly asks to commit it, restore it before the code commit.

## Current Code Anchors

Use `grep`/`read` to refresh line numbers, but these are current shapes after `2705769`:

- `spikes/binding-generators/clangsharp/policy/opaque-handle-roster.json`: schema `1.0`, flat Core-only fields.
- `OpaqueHandleEmitRewriter.cs`:
- `DiscoverAutoDetectedHandles` still has 4 `StartsWith("SDL_", StringComparison.Ordinal)` filters.
- `LoadRoster(string rosterPath)` still reads root `auto_detect_well_known` and `force_opaque_exceptions`.
- `ReportDrift(HashSet<string>, HashSet<string>)` is not family-aware.
- `BuildHandlesFileContent(IEnumerable<string>)` still hardcodes `namespace SDL2`.
- `Program.cs`:
- usage still lists `threadid-dispatch` and `uniform-opaque` without `--handles-namespace`.
- `uniform-opaque` still calls `LoadRoster(rosterPath)` and `ReportDrift(..., ...)`.
- `UniformOpaqueOwnerMode.cs`:
- `EmitConsolidatedHandlesFileIfOwner(...)` still lacks `namespaceName`.
- `BuildHandlesFileContent(sortedNames)` call already writes via `PostProcessCli.WriteAllTextLf` from LF fix.
- `generate_bindings.py`:
- `owner_mode_for_family(family)` already returns owner for `core`, `ttf`, `mixer`; consumer for `image`, `gfx`.
- `uniform-opaque` invocation still passes only `--owner-mode`, not `--handles-namespace`.

## S1-4 Execution Shape

Recommended implementation rhythm:

1. Verify clean state and self-tests.
2. Take a temporary copy of `policy/opaque-handle-roster.json` for preserving Core entries. Do not commit the backup.
3. Migrate roster JSON to schema `2.0`; validate JSON parse immediately.
4. Add/extend self-tests that fail against the current one-arg `LoadRoster` and prefix-gated discovery.
5. Update `LoadRoster(rosterPath, family)` and add cross-family Core pull for non-Core families.
6. Drop the 4 `SDL_` prefix filters in `DiscoverAutoDetectedHandles`.
7. Parameterize `BuildHandlesFileContent` with `namespaceName`.
8. Add `ReportDrift(..., family)`.
9. Add `uniformOpaqueHandlesNamespace` at the outer `Program.cs` scope, not inside the switch case.
10. Parse `--handles-namespace` in the `uniform-opaque` case.
11. Resolve family from output path and use it for `LoadRoster` / `ReportDrift`.
12. Pass `--handles-namespace FAMILY_CONFIG[family]["namespace"]` from `generate_bindings.py`.
13. Run full S1-4 verification before committing.

Do not run real `generate_bindings.py --execute` between steps 3 and 11. During that window the JSON schema and loader/call-sites are intentionally inconsistent or only partially family-aware.

## Non-Negotiable Warnings

- **Do not run `generate_bindings.py --family all --execute` between roster schema migration and family-aware loader completion.** Legacy `LoadRoster` will crash on schema `2.0`.
- **Do not treat intermediate `LoadRoster(..., "core")` as a valid D.5 pass.** It can accidentally preserve Image today without proving cross-family pull.
- **Do not commit generated Core/Image drift for S1-4.** This slice should be structurally byte-equivalent for current generated output.
- **Do not commit temporary roster backups.** If you create `policy/.gitignore` only for the backup, delete it if it becomes empty.
- **Do not lose the LF self-tests.** They are now part of the Windows determinism safety net.
- **Do not start S1-5 in the same commit.** S1-5 is the `threadid-dispatch` → `clong-dispatch` rename/refactor and has a separate risk profile.

## Verification Checklist For S1-4

At minimum, collect fresh evidence after implementation:

```pwsh
python "spikes/binding-generators/clangsharp/generate_bindings.py" --self-test
dotnet run --project "spikes/binding-generators/clangsharp/postprocess/Janset.SDL2.PostProcess.csproj" -- --self-test
dotnet build "spikes/binding-generators/clangsharp/postprocess/Janset.SDL2.PostProcess.csproj" -c Release
python "spikes/binding-generators/clangsharp/generate_bindings.py" --family all --execute --vcpkg-triplet x64-windows-hybrid --use-platform-header-shims
git diff --ignore-cr-at-eol -- "spikes/binding-generators/clangsharp/src/Janset.SDL2.Core/Generated/" "spikes/binding-generators/clangsharp/src/Janset.SDL2.Image/Generated/"
python "spikes/binding-generators/clangsharp/generate_bindings.py" --family image --execute --vcpkg-triplet x64-windows-hybrid --use-platform-header-shims
git status --short "spikes/binding-generators/clangsharp/src/Janset.SDL2.Core/Generated/"
git diff --ignore-cr-at-eol -- "spikes/binding-generators/clangsharp/src/Janset.SDL2.Image/Generated/"
git diff --check
slopwatch analyze --fail-on warning --exclude "artifacts/**,external/**,vcpkg_installed/**,spikes/binding-generators/references/**,**/bin/**,**/obj/**"
```

Also inspect EOL state after execute:

```pwsh
git ls-files --eol -- "spikes/binding-generators/clangsharp/src/Janset.SDL2.Core/Generated/**/*.g.cs" "spikes/binding-generators/clangsharp/src/Janset.SDL2.Image/Generated/**/*.g.cs"
```

Expected: generated files are `w/lf` or `w/none`; no `w/crlf` or `w/mixed`.

## Commit Shape

The original plan suggests two commits for S1-4:

1. Roster schema migration.
2. Family-aware opaque-handle postprocess/orchestrator changes.

That shape still makes sense if verification stays clean. Before committing, present the summary and proposed commit messages to Deniz and ask for approval per `AGENTS.md`.

Suggested messages:

```text
refactor(spike): migrate opaque handle roster to family-keyed schema (S1-4a)
feat(spike): make opaque handle postprocess family-aware (S1-4b)
```

If `clangsharp-production.md` changes only because execute ran, restore it unless Deniz explicitly chooses to include report evidence.

## Locked Policy Recap

- `--family all` remains `core,image` after Item 1; TTF/Mixer/GFX stay dormant until their own items.
- Satellite handles must be Pattern B-compatible by value once owned; Image/GFX consume Core handles through roster data and ProjectReference, not by reading Core `.g.cs` during postprocess.
- `SDL_bool` must never become `[Flags]`; that belongs to S1-6, not S1-4.
- Approval gate is real: no commits without explicit `go / apply / proceed / başla / yap`.
- Smallest correct change wins. No broad refactor while inside S1-4.

## Final Steering Note

S1-4 is a schema-and-plumbing slice with one sharp edge: the loader and roster must cross the river together. Keep the working tree coherent, prove Core/Image do not drift, and preserve the new LF determinism guardrails. After S1-4 lands, S1-5 can tackle the `clong-dispatch` refactor without carrying opaque-handle ambiguity forward.
