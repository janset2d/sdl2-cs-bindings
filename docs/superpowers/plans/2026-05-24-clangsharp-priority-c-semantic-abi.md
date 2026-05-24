# Priority C Semantic-ABI Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Close the six Priority C semantic-ABI risks on the ClangSharp + Roslyn postprocess spike, transitioning the spike from "near-ABI-compatible Layer 1" to **"ABI-compatible Layer 1"** per [design spec](../specs/2026-05-24-clangsharp-priority-c-semantic-abi-design.md).

**Architecture:** Three sequential slices on the spike under `spikes/binding-generators/`:
- **C-C** — Per-header RSP infrastructure + tag/typedef canonicalization (R6) + SDL_GUID substitution.
- **C-A** — Scalars: wchar_t RSP fix (R1) + SDL_stdinc drops (R2 helpers) + ThreadIdDualDispatchRewriter (R2 structural).
- **C-B** — Uniform Pattern B opaque handle struct emit (R3/R4/R5 + ~12 existing empty-struct opaques upgrade) with by-value raw signature rewrites.

Each slice's mechanism mixes ClangSharp RSP-level fixes (preferred per "RSP-first → postprocess-fallback" principle) with Roslyn-based `CSharpSyntaxRewriter` postprocess transforms. Existing postprocess project pattern at `spikes/binding-generators/clangsharp/postprocess/` is the reference (DllImportToLibraryImportRewriter, StripVarargsRewriter, PlatformDeltaPostProcessor).

**Tech Stack:** ClangSharp PInvokeGenerator (NuGet CLI), Python orchestrator (`generate_bindings.py`), Microsoft.CodeAnalysis.CSharp Syntax/Rewriter API, .NET 10 SDK, vcpkg (x64-windows-hybrid triplet for local Windows-host iteration), Docker (CI/native parse pass).

---

## File Structure

**Create:**

- `spikes/binding-generators/clangsharp/rsp/per-header/` — new directory
- `spikes/binding-generators/clangsharp/rsp/per-header/SDL_hidapi.rsp` — R6 tag remap
- `spikes/binding-generators/clangsharp/rsp/per-header/SDL_mutex.rsp` — R6 tag remap
- `spikes/binding-generators/clangsharp/rsp/per-header/SDL_stdinc.rsp` — R2 stdinc excludes
- `spikes/binding-generators/clangsharp/postprocess/GuidSubstitutionRewriter.cs` — SDL_GUID → System.Guid
- `spikes/binding-generators/clangsharp/postprocess/ThreadIdDualDispatchRewriter.cs` — R2 structural dual-emit
- `spikes/binding-generators/clangsharp/postprocess/OpaqueHandleEmitRewriter.cs` — Slice C-B main rewriter
- `tests/smoke-tests/abi-tests/AbiTests.csproj` + `tests/smoke-tests/abi-tests/ThreadIdAbiTests.cs` — per-RID smoke test for SDL_threadID

**Modify:**

- `spikes/binding-generators/clangsharp/rsp/base.rsp` — fix wchar_t remap spelling (line 17-20)
- `spikes/binding-generators/clangsharp/generate_bindings.py` — add per-header RSP lookup in `run_clangsharp`
- `spikes/binding-generators/clangsharp/postprocess/Program.cs` — wire new rewriter modes (guid-substitute, threadid-dispatch, uniform-opaque)
- `spikes/binding-generators/clangsharp/oracle.cs` — add duplicate tag/typedef detection lane

**Verification commands (used throughout):**

```pwsh
# Full regenerate (Windows-host local iteration)
python spikes/binding-generators/clangsharp/generate_bindings.py --scope full --codegen both --execute --clean-output --vcpkg-triplet x64-windows-hybrid --use-platform-header-shims

# Multi-TFM compile-check (5 TFMs)
dotnet build spikes/binding-generators/clangsharp/src/Janset.SDL2.Image/Janset.SDL2.Image.csproj -c Release

# Oracle evidence report
dotnet run --file spikes/binding-generators/clangsharp/oracle.cs -- --family sdl2-core --family sdl2-image --write-report

# Run a specific postprocess mode in place (post-regenerate)
dotnet run --project spikes/binding-generators/clangsharp/postprocess/Janset.SDL2.PostProcess.csproj -c Release -- <mode> spikes/binding-generators/clangsharp/src/Janset.SDL2.Core/Generated/Modern
```

---

## Phase 1 — Slice C-C: Handle Canonicalization + SDL_GUID

Purpose: smallest slice; unblocks Slice C-B's auto-detect channel by removing tag duplicates.

### Task 1: Per-Header RSP Infrastructure

**Files:**
- Modify: `spikes/binding-generators/clangsharp/generate_bindings.py` (orchestrator: add `--additional` per-header RSP lookup in `run_clangsharp`)
- Create: `spikes/binding-generators/clangsharp/rsp/per-header/` (empty directory marker)

- [ ] **Step 1.1: Locate the `run_clangsharp` function in the orchestrator**

Run: `grep -n "def run_clangsharp" spikes/binding-generators/clangsharp/generate_bindings.py`

Expected: one match around lines 310-340 area. Read the function and its caller to understand where the ClangSharp argument list is assembled.

- [ ] **Step 1.2: Identify the per-header file basename in the orchestrator**

Within the per-header invocation loop, each header has a `header_name` like `SDL_audio.h`. The corresponding per-header RSP basename would be the header name without `.h` extension: `SDL_audio.rsp`.

- [ ] **Step 1.3: Add per-header RSP lookup helper**

Add (or extend if a helper already exists) a function like this near the top of `generate_bindings.py`:

```python
def per_header_rsp_path(repo: pathlib.Path, header_name: str) -> pathlib.Path | None:
    """Return path to per-header RSP if it exists, else None.

    header_name is the SDL header filename like 'SDL_audio.h'.
    Looks under spikes/binding-generators/clangsharp/rsp/per-header/<basename>.rsp.
    """
    basename = pathlib.Path(header_name).stem  # 'SDL_audio.h' -> 'SDL_audio'
    candidate = (
        repo / "spikes" / "binding-generators" / "clangsharp"
        / "rsp" / "per-header" / f"{basename}.rsp"
    )
    return candidate if candidate.is_file() else None
```

- [ ] **Step 1.4: Wire the helper into the ClangSharp invocation**

Within `run_clangsharp` (or wherever the ClangSharp argument list is built per header), after the existing family RSP path is added with `--additional`, add the per-header RSP if it exists:

```python
# After: command.extend(["--additional", str(family_rsp_path)])
per_header = per_header_rsp_path(repo, header_name)
if per_header is not None:
    command.extend(["--additional", str(per_header)])
```

Adjust the exact insertion point to match the existing code's idiom — search for an existing `command.extend(["--additional"` line to find the right placement.

- [ ] **Step 1.5: Create the per-header directory marker**

```bash
mkdir -p spikes/binding-generators/clangsharp/rsp/per-header
touch spikes/binding-generators/clangsharp/rsp/per-header/.gitkeep
```

- [ ] **Step 1.6: Smoke-test the orchestrator change (no per-header RSP exists yet, so behavior should be identical)**

Run: `python spikes/binding-generators/clangsharp/generate_bindings.py --scope full --codegen both --execute --clean-output --vcpkg-triplet x64-windows-hybrid --use-platform-header-shims`

Expected: regeneration completes without errors. No new per-header RSP files exist yet, so output is byte-identical to pre-change baseline. Generated `spikes/binding-generators/output/reports/clangsharp-full.md` shows the same per-header summary lines.

- [ ] **Step 1.7: Verify multi-TFM compile is still clean**

Run: `dotnet build spikes/binding-generators/clangsharp/src/Janset.SDL2.Image/Janset.SDL2.Image.csproj -c Release`

Expected: 0 errors, 0 warnings across `net462`, `netstandard2.0`, `net8.0`, `net9.0`, `net10.0`.

- [ ] **Step 1.8: Commit**

```bash
git add spikes/binding-generators/clangsharp/generate_bindings.py spikes/binding-generators/clangsharp/rsp/per-header/.gitkeep
git commit -m "$(cat <<'EOF'
feat(binding-spike): per-header RSP lookup in generate_bindings orchestrator

Add rsp/per-header/<basename>.rsp lookup in run_clangsharp so each header
invocation can load symbol-local concerns close to the header they affect
(ppy/SDL3-CS pattern). Cross-cutting policy stays in base.rsp; family
identity stays in family RSP; per-header concerns route into the new
per-header/ tier.

No behavior change yet — no per-header RSP files exist. Subsequent tasks
add the actual per-header entries (R6 tag remaps, R2 stdinc excludes).

Refs: docs/superpowers/specs/2026-05-24-clangsharp-priority-c-semantic-abi-design.md
Decision 4.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

### Task 2: R6 SDL_hid_device_ Tag Remap

**Files:**
- Create: `spikes/binding-generators/clangsharp/rsp/per-header/SDL_hidapi.rsp`

- [ ] **Step 2.1: Verify the current duplicate-tag state**

Run: `grep -n "SDL_hid_device_" spikes/binding-generators/clangsharp/src/Janset.SDL2.Core/Generated/Modern/SDL_hidapi.g.cs`

Expected: ~13 matches in `SDL_hidapi.g.cs` — both the empty struct declaration (`public partial struct SDL_hid_device_ { }`) and parameter/return-type references like `SDL_hid_device_* dev`.

- [ ] **Step 2.2: Create the per-header RSP**

```bash
cat > spikes/binding-generators/clangsharp/rsp/per-header/SDL_hidapi.rsp <<'EOF'
# Per-header overrides for SDL_hidapi.h.
#
# R6 tag/typedef canonicalization (Constitution L262-277): rename the
# private C struct tag `SDL_hid_device_` to the public typedef name
# `SDL_hid_device` everywhere (struct declaration + every reference).
# ClangSharp byte-exact remap follows ppy/SDL3-CS pattern at
# references/ppy-SDL3-CS/SDL3-CS/SDL3/SDL_hidapi.rsp:5-6.

--remap
SDL_hid_device_=SDL_hid_device
EOF
```

- [ ] **Step 2.3: Regenerate**

Run: `python spikes/binding-generators/clangsharp/generate_bindings.py --scope full --codegen both --execute --clean-output --vcpkg-triplet x64-windows-hybrid --use-platform-header-shims`

Expected: regeneration completes; per-header RSP loaded (Task 1 wiring).

- [ ] **Step 2.4: Verify the rename took effect**

Run:
```bash
grep -n "SDL_hid_device_" spikes/binding-generators/clangsharp/src/Janset.SDL2.Core/Generated/Modern/SDL_hidapi.g.cs
grep -cn "SDL_hid_device\b" spikes/binding-generators/clangsharp/src/Janset.SDL2.Core/Generated/Modern/SDL_hidapi.g.cs
```

Expected:
- First grep: 0 matches (the underscored tag is gone).
- Second grep: positive count (the canonical typedef is everywhere references used to be).

If the rename didn't take effect, re-read the RSP file and confirm Task 1's per-header lookup is wired correctly.

- [ ] **Step 2.5: Verify multi-TFM compile**

Run: `dotnet build spikes/binding-generators/clangsharp/src/Janset.SDL2.Image/Janset.SDL2.Image.csproj -c Release`

Expected: 0 errors, 0 warnings across all 5 TFMs.

- [ ] **Step 2.6: Commit**

```bash
git add spikes/binding-generators/clangsharp/rsp/per-header/SDL_hidapi.rsp spikes/binding-generators/clangsharp/src/
git commit -m "$(cat <<'EOF'
feat(binding-spike): canonicalize SDL_hid_device tag via per-header RSP

R6 fix: single --remap line in rsp/per-header/SDL_hidapi.rsp renames the
private struct tag SDL_hid_device_ to the public typedef name
SDL_hid_device everywhere (struct declaration + every parameter/return
reference). ppy/SDL3-CS verified pattern.

Refs: docs/superpowers/specs/2026-05-24-clangsharp-priority-c-semantic-abi-design.md
Slice C-C R6.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

### Task 3: R6 SDL_semaphore Tag Remap

**Files:**
- Create: `spikes/binding-generators/clangsharp/rsp/per-header/SDL_mutex.rsp`

- [ ] **Step 3.1: Verify the current duplicate-tag state**

Run: `grep -n "SDL_semaphore" spikes/binding-generators/clangsharp/src/Janset.SDL2.Core/Generated/Modern/SDL_mutex.g.cs`

Expected: ~7 matches — empty struct declaration plus parameter/return references like `SDL_semaphore* sem` with `[NativeTypeName("SDL_sem *")]` annotations.

- [ ] **Step 3.2: Create the per-header RSP**

```bash
cat > spikes/binding-generators/clangsharp/rsp/per-header/SDL_mutex.rsp <<'EOF'
# Per-header overrides for SDL_mutex.h.
#
# R6 tag/typedef canonicalization (Constitution L262-277): rename the
# private C struct tag `SDL_semaphore` to the public typedef name
# `SDL_sem` everywhere.

--remap
SDL_semaphore=SDL_sem
EOF
```

- [ ] **Step 3.3: Regenerate**

Run: `python spikes/binding-generators/clangsharp/generate_bindings.py --scope full --codegen both --execute --clean-output --vcpkg-triplet x64-windows-hybrid --use-platform-header-shims`

- [ ] **Step 3.4: Verify the rename took effect**

Run:
```bash
grep -n "SDL_semaphore" spikes/binding-generators/clangsharp/src/Janset.SDL2.Core/Generated/Modern/SDL_mutex.g.cs
grep -cn "SDL_sem\b" spikes/binding-generators/clangsharp/src/Janset.SDL2.Core/Generated/Modern/SDL_mutex.g.cs
```

Expected: first grep returns 0; second grep shows positive count.

- [ ] **Step 3.5: Verify multi-TFM compile**

Run: `dotnet build spikes/binding-generators/clangsharp/src/Janset.SDL2.Image/Janset.SDL2.Image.csproj -c Release`

Expected: 0 errors, 0 warnings.

- [ ] **Step 3.6: Commit**

```bash
git add spikes/binding-generators/clangsharp/rsp/per-header/SDL_mutex.rsp spikes/binding-generators/clangsharp/src/
git commit -m "$(cat <<'EOF'
feat(binding-spike): canonicalize SDL_sem typedef via per-header RSP

R6 fix: --remap SDL_semaphore=SDL_sem in rsp/per-header/SDL_mutex.rsp
renames the private tag everywhere to the public typedef name.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

### Task 4: Oracle Duplicate Tag/Typedef Detection Lane

**Files:**
- Modify: `spikes/binding-generators/clangsharp/oracle.cs` (add new check)

- [ ] **Step 4.1: Locate the oracle's check list**

Run: `grep -n "Compatibility Risk\|Hard Bug" spikes/binding-generators/clangsharp/oracle.cs | head -20`

Expected: matches showing where existing check categories (platform-sensitive-wchar, deferred-layout-*, etc.) are defined. Read the surrounding code to understand the check pattern.

- [ ] **Step 4.2: Add a "duplicate tag/typedef" check function**

Add a new check method following the same pattern as the existing scalar/layout checks. The check walks all `partial struct` declarations in the generated output and, for each one, searches for `[NativeTypeName("X *")]` annotations whose `X` differs from the declared struct name (after stripping `_` suffix or `_T` suffix). Report each such pair.

Suggested signature (matching existing style — adapt to actual oracle.cs idioms):

```csharp
private static IReadOnlyList<Finding> CheckDuplicateTagTypedef(
    IReadOnlyList<GeneratedSymbol> structs,
    IReadOnlyList<GeneratedSymbol> references)
{
    var findings = new List<Finding>();
    foreach (var s in structs.Where(static x => x.IsEmptyPartialStruct))
    {
        // Look for NativeTypeName annotations referencing this struct
        // with a DIFFERENT canonical name (typedef vs tag)
        foreach (var r in references.Where(x => x.NativeTypeName.Contains(s.Name)))
        {
            var trimmedAnnotation = r.NativeTypeName
                .Replace("const ", "")
                .Replace(" *", "")
                .Replace("*", "")
                .Trim();
            if (!string.Equals(trimmedAnnotation, s.Name, StringComparison.Ordinal))
            {
                findings.Add(new Finding(
                    Category: "duplicate-tag-typedef",
                    Symbol: s.Name,
                    Message: $"Struct '{s.Name}' is referenced via [NativeTypeName(\"{r.NativeTypeName}\")] — tag vs typedef mismatch suggests canonicalization needed.",
                    Path: r.SourceFile,
                    Severity: Severity.Compatibility));
                break; // one finding per struct
            }
        }
    }
    return findings;
}
```

Wire the new check into the family-aware report writer (`--family sdl2-core --family sdl2-image`) so its findings appear in the same Compatibility Risk section as existing scalar checks.

- [ ] **Step 4.3: Run the oracle and verify the new check works**

Run: `dotnet run --file spikes/binding-generators/clangsharp/oracle.cs -- --family sdl2-core --family sdl2-image --write-report`

Expected: report shows a new "Compatibility Risk → duplicate-tag-typedef" section. After Tasks 2 and 3, this section should show **0 findings** (the two known pairs are already resolved).

- [ ] **Step 4.4: Validate by temporarily reverting Task 2's RSP and regenerating**

This is a self-test of the new oracle check. Steps:

```bash
git stash push -- spikes/binding-generators/clangsharp/rsp/per-header/SDL_hidapi.rsp
python spikes/binding-generators/clangsharp/generate_bindings.py --scope full --codegen both --execute --clean-output --vcpkg-triplet x64-windows-hybrid --use-platform-header-shims
dotnet run --file spikes/binding-generators/clangsharp/oracle.cs -- --family sdl2-core --family sdl2-image --write-report
```

Expected: oracle now reports 1 duplicate-tag-typedef finding (`SDL_hid_device_` ↔ `SDL_hid_device`).

Restore Task 2's RSP:

```bash
git stash pop
python spikes/binding-generators/clangsharp/generate_bindings.py --scope full --codegen both --execute --clean-output --vcpkg-triplet x64-windows-hybrid --use-platform-header-shims
```

- [ ] **Step 4.5: Commit**

```bash
git add spikes/binding-generators/clangsharp/oracle.cs spikes/binding-generators/output/
git commit -m "$(cat <<'EOF'
feat(binding-spike): oracle detection for duplicate tag/typedef pairs

Walk all empty partial struct declarations; for each, search NativeTypeName
annotations referencing it with a different canonical name (typedef vs
tag). Report as Compatibility Risk → duplicate-tag-typedef.

Self-test verified: temporarily reverting SDL_hidapi.rsp reproduces a
finding; restoring it returns to 0.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

### Task 5: GuidSubstitutionRewriter

**Files:**
- Create: `spikes/binding-generators/clangsharp/postprocess/GuidSubstitutionRewriter.cs`
- Modify: `spikes/binding-generators/clangsharp/postprocess/Program.cs` (wire `guid-substitute` mode)
- Modify: `spikes/binding-generators/clangsharp/generate_bindings.py` (invoke the new postprocess mode)

- [ ] **Step 5.1: Verify the current SDL_GUID emission state**

Run: `grep -n "SDL_GUID" spikes/binding-generators/clangsharp/src/Janset.SDL2.Core/Generated/Modern/SDL_joystick.g.cs | head -10`

Expected: matches showing both the struct declaration (`public partial struct SDL_GUID { fixed byte data[16]; }` or `public unsafe partial struct SDL_GUID { ... }`) and references in function signatures.

- [ ] **Step 5.2: Create the rewriter file**

```csharp
// spikes/binding-generators/clangsharp/postprocess/GuidSubstitutionRewriter.cs

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Janset.SDL2.PostProcess;

/// <summary>
/// Substitute SDL_GUID with System.Guid throughout generated output.
///
/// C SDL_GUID is `typedef struct { Uint8 data[16]; } SDL_GUID;` (16 raw bytes
/// for joystick/gamecontroller identification). System.Guid is also 16 bytes
/// with structured Data1/Data2/Data3/Data4 fields. Wire size is bit-identical;
/// the substitution is ABI-correct but caller-side .ToString() renders MS
/// GUID notation rather than SDL's raw-hex convention (Constitution-level
/// trade-off accepted; Cake's SdlNativeTypeSubstitutionPolicy does the same).
///
/// Mechanism:
///   1. Remove `partial struct SDL_GUID { ... }` declaration entirely.
///   2. Rewrite every reference (parameter/return/field type) `SDL_GUID` → `Guid`.
///   3. Ensure `using System;` is present in any file that now uses `Guid`.
/// </summary>
internal sealed class GuidSubstitutionRewriter : CSharpSyntaxRewriter
{
    public bool AnyChanges { get; private set; }

    public void Reset() => AnyChanges = false;

    public override SyntaxNode? VisitStructDeclaration(StructDeclarationSyntax node)
    {
        if (node.Identifier.ValueText == "SDL_GUID")
        {
            AnyChanges = true;
            // Return null to remove the declaration entirely.
            return null;
        }

        return base.VisitStructDeclaration(node);
    }

    public override SyntaxNode? VisitIdentifierName(IdentifierNameSyntax node)
    {
        if (node.Identifier.ValueText == "SDL_GUID")
        {
            AnyChanges = true;
            return SyntaxFactory.IdentifierName("Guid")
                .WithTriviaFrom(node);
        }

        return base.VisitIdentifierName(node);
    }

    /// <summary>
    /// After Visit completes, ensure the compilation unit has `using System;`
    /// when any Guid reference was introduced. Caller should invoke this on
    /// the rewritten root after Visit.
    /// </summary>
    public static CompilationUnitSyntax EnsureSystemUsing(CompilationUnitSyntax root)
    {
        var hasSystemUsing = root.Usings.Any(u =>
            u.Name?.ToString() == "System");

        if (hasSystemUsing)
        {
            return root;
        }

        var systemUsing = SyntaxFactory.UsingDirective(
            SyntaxFactory.IdentifierName("System"));

        return root.WithUsings(root.Usings.Insert(0, systemUsing));
    }
}
```

- [ ] **Step 5.3: Wire the new mode into `Program.cs`**

Modify `spikes/binding-generators/clangsharp/postprocess/Program.cs` to recognize `guid-substitute` as a mode. Add to the mode validation tuple and add a switch case:

```csharp
// In the args validation:
if (args.Length < 2 || args[0] is not
    ("strip-varargs" or "libraryimport" or "platform-delta" or "guid-substitute"))
{
    // ... existing error path
}

// In the rewriter switch:
case "guid-substitute":
{
    var r = new GuidSubstitutionRewriter();
    rewriter = r;
    hasChanges = () => r.AnyChanges;
    resetRewriter = r.Reset;
    break;
}
```

Also modify the file-write step to call `GuidSubstitutionRewriter.EnsureSystemUsing` when the mode is `guid-substitute` and changes were applied:

```csharp
// After: var rewritten = (CompilationUnitSyntax)rewriter.Visit(root)!;
if (mode == "guid-substitute" && hasChanges())
{
    rewritten = GuidSubstitutionRewriter.EnsureSystemUsing(rewritten);
}
```

- [ ] **Step 5.4: Build the postprocess project**

Run: `dotnet build spikes/binding-generators/clangsharp/postprocess/Janset.SDL2.PostProcess.csproj -c Release`

Expected: 0 errors, 0 warnings. The new rewriter compiles cleanly.

- [ ] **Step 5.5: Run the rewriter against the spike output (manual one-shot)**

Run:
```bash
dotnet run --project spikes/binding-generators/clangsharp/postprocess/Janset.SDL2.PostProcess.csproj -c Release -- guid-substitute spikes/binding-generators/clangsharp/src/Janset.SDL2.Core/Generated/Compat
dotnet run --project spikes/binding-generators/clangsharp/postprocess/Janset.SDL2.PostProcess.csproj -c Release -- guid-substitute spikes/binding-generators/clangsharp/src/Janset.SDL2.Core/Generated/Modern
```

Expected: "X files scanned, Y files transformed" output. Y > 0 (at least `SDL_joystick.g.cs` was transformed).

- [ ] **Step 5.6: Verify the substitution**

Run:
```bash
grep -n "partial struct SDL_GUID" spikes/binding-generators/clangsharp/src/Janset.SDL2.Core/Generated/Modern/SDL_joystick.g.cs
grep -n "\bGuid\b" spikes/binding-generators/clangsharp/src/Janset.SDL2.Core/Generated/Modern/SDL_joystick.g.cs | head -5
grep -n "using System;" spikes/binding-generators/clangsharp/src/Janset.SDL2.Core/Generated/Modern/SDL_joystick.g.cs
```

Expected:
- First grep: 0 matches (struct gone).
- Second grep: positive count (references now say `Guid`).
- Third grep: 1 match (using statement present).

- [ ] **Step 5.7: Verify multi-TFM compile**

Run: `dotnet build spikes/binding-generators/clangsharp/src/Janset.SDL2.Image/Janset.SDL2.Image.csproj -c Release`

Expected: 0 errors, 0 warnings.

- [ ] **Step 5.8: Wire the new mode into the orchestrator pipeline**

The orchestrator runs `libraryimport`, `strip-varargs`, `platform-delta` postprocess passes after generation. Add `guid-substitute` to that pipeline so subsequent regenerations don't lose the substitution.

In `spikes/binding-generators/clangsharp/generate_bindings.py`, find where the existing postprocess invocations happen (search for `libraryimport` or `platform-delta`). Add a new invocation step for `guid-substitute`. Apply to BOTH Compat and Modern output directories.

Pattern (adjust to actual code):

```python
def run_postprocess(repo: pathlib.Path, output_dir: pathlib.Path, mode: str) -> None:
    """Invoke the postprocess project in a given mode against output_dir."""
    project = repo / "spikes" / "binding-generators" / "clangsharp" / "postprocess" / "Janset.SDL2.PostProcess.csproj"
    subprocess.run(
        ["dotnet", "run", "--project", str(project), "-c", "Release", "--",
         mode, str(output_dir)],
        check=True,
    )

# Add to the pipeline (after existing postprocess steps):
run_postprocess(repo, compat_output_dir, "guid-substitute")
run_postprocess(repo, modern_output_dir, "guid-substitute")
```

If a helper like `run_postprocess` already exists, use it; otherwise add it.

- [ ] **Step 5.9: Verify the full regeneration round-trip**

Run: `python spikes/binding-generators/clangsharp/generate_bindings.py --scope full --codegen both --execute --clean-output --vcpkg-triplet x64-windows-hybrid --use-platform-header-shims`

Expected: regeneration succeeds; new `guid-substitute` step runs; final output has no `SDL_GUID` struct and `Guid` references resolve.

Re-run the grep checks from Step 5.6.

- [ ] **Step 5.10: Commit**

```bash
git add spikes/binding-generators/clangsharp/postprocess/ spikes/binding-generators/clangsharp/generate_bindings.py spikes/binding-generators/clangsharp/src/
git commit -m "$(cat <<'EOF'
feat(binding-spike): GuidSubstitutionRewriter for SDL_GUID -> System.Guid

New CSharpSyntaxRewriter removes the partial struct SDL_GUID declaration
(C: 16 raw bytes via Uint8 data[16]) and substitutes every reference
with System.Guid (also 16 bytes, structured Data1/2/3/4 layout). Wire
size bit-identical; trade-off accepted per Cake SdlNativeTypeSubstitutionPolicy
precedent. Adds `using System;` automatically where needed.

Wired into the orchestrator pipeline after libraryimport/platform-delta
so subsequent regenerations preserve the substitution.

Refs: docs/superpowers/specs/2026-05-24-clangsharp-priority-c-semantic-abi-design.md
Slice C-C SDL_GUID.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

### Task 6: Apply R6 Canonicalization + Foreign-Type Boundary Policy

**Background:** Task 4's oracle duplicate-tag-typedef lane surfaced 10 candidate pairs (beyond the 2 in Tasks 2-3). 2026-05-24 brainstorm + 6 parallel research probes (see `docs/research/semantic-abi-type-classification-research.md` Appendix B) classified them into two groups:

1. **6 SDL-owned pairs** — apply R6 canonicalization (tag → public typedef) per Decision 1 of the design spec.
2. **4 foreign-boundary pairs (Vulkan + GDK)** — apply **Decision 5 Foreign Type Boundary Policy** (foreign types emit as `IntPtr` / `nint`, not Pattern B wrappers, for zero-friction interop with `Silk.NET.Vulkan`, `Vortice.Windows`, etc.). Plus 3 Direct3D COM types from `SDL_system.h` Windows pass that surfaced in the foreign-type survey but weren't in the oracle finding (they have no tag/typedef pair — just empty stub structs leaking).

The Constitution gained a new §"Foreign Type Boundary Policy" section with the canonical allow-list; the design spec's Decision 5 carries the slice-scoped specifics.

**Files:**
- Create: `spikes/binding-generators/clangsharp/rsp/per-header/SDL_audio.rsp` (R6: `_SDL_AudioStream` → `SDL_AudioStream`)
- Create: `spikes/binding-generators/clangsharp/rsp/per-header/SDL_gamecontroller.rsp` (R6: `_SDL_GameController` → `SDL_GameController`)
- Create: `spikes/binding-generators/clangsharp/rsp/per-header/SDL_haptic.rsp` (R6: `_SDL_Haptic` → `SDL_Haptic`)
- Create: `spikes/binding-generators/clangsharp/rsp/per-header/SDL_joystick.rsp` (R6: `_SDL_Joystick` → `SDL_Joystick`)
- Create: `spikes/binding-generators/clangsharp/rsp/per-header/SDL_sensor.rsp` (R6: `_SDL_Sensor` → `SDL_Sensor`)
- Create: `spikes/binding-generators/clangsharp/rsp/per-header/SDL_vulkan.rsp` (Decision 5: `VkInstance` / `VkSurfaceKHR *` → IntPtr; exclude tag structs)
- Create: `spikes/binding-generators/clangsharp/rsp/per-header/SDL_system.rsp` (Decision 5: Direct3D COM → IntPtr; GDK `XTaskQueueObject` / `XUser` → IntPtr; exclude tag structs)
- Modify: `spikes/binding-generators/clangsharp/rsp/per-header/SDL_stdinc.rsp` already exists from Task 9 plan; if Task 6 runs first, create the file with the `_SDL_iconv_t` remap only and let Task 9 append `--exclude` entries later. (Alternatively, defer `SDL_iconv_t` to Task 9 and combine.)

- [ ] **Step 6.1: Run the oracle to confirm starting state**

Run: `dotnet run --file spikes/binding-generators/clangsharp/oracle.cs -- --family sdl2-core --family sdl2-image --write-report`

Read `spikes/binding-generators/output/reports/oracle-evidence-clangsharp.md` Compatibility Risk → duplicate-tag-typedef section.

Expected: 10 findings / pairs reported (post-Task-5 state) — 6 SDL pairs + 4 foreign pairs.

- [ ] **Step 6.2: Apply the 6 SDL R6 canonicalizations**

For each SDL-owned pair below, create the per-header RSP file mirroring `SDL_hidapi.rsp` / `SDL_mutex.rsp` structure (comment block citing Constitution L262-277 + ppy precedent + `--remap` line).

| Pair | RSP file |
| --- | --- |
| `_SDL_AudioStream=SDL_AudioStream` | `SDL_audio.rsp` |
| `_SDL_GameController=SDL_GameController` | `SDL_gamecontroller.rsp` |
| `_SDL_Haptic=SDL_Haptic` | `SDL_haptic.rsp` |
| `_SDL_Joystick=SDL_Joystick` | `SDL_joystick.rsp` |
| `_SDL_Sensor=SDL_Sensor` | `SDL_sensor.rsp` |
| `_SDL_iconv_t=SDL_iconv_t` | `SDL_stdinc.rsp` (new file; Task 9 will later append `--exclude` for the C-long helpers) |

Each RSP file structure (use `SDL_hidapi.rsp` as the template, swap the symbol):

```
# Per-header overrides for <header>.h.
#
# R6 tag/typedef canonicalization (Constitution L262-277): rename the
# private C struct tag `_SDL_X` to the public typedef name `SDL_X`
# everywhere (struct declaration + every reference).

--remap
_SDL_X=SDL_X
```

- [ ] **Step 6.3: Apply Decision 5 — foreign-type IntPtr-emit for Vulkan**

Create `spikes/binding-generators/clangsharp/rsp/per-header/SDL_vulkan.rsp`:

```
# Per-header overrides for SDL_vulkan.h.
#
# Decision 5 (Foreign Type Boundary Policy) per
# docs/superpowers/specs/2026-05-24-clangsharp-priority-c-semantic-abi-design.md
# and Constitution §"Foreign Type Boundary Policy":
# VkInstance / VkSurfaceKHR are owned by the user's external Vulkan binding
# (Silk.NET.Vulkan, Vortice.Vulkan, TerraFX). SDL2 references them at
# SDL_Vulkan_CreateSurface parameter sites only (SDL_vulkan.h:187-188).
# Emit as IntPtr / IntPtr* so users pass handle values from external Vulkan
# bindings directly without explicit-conversion friction.
#
# Precedent: SDL2-CS uses IntPtr for VkInstance and `out ulong` for
# VkSurfaceKHR (external/sdl2-cs/src/SDL2.cs:2452-2456).

--remap
VkInstance=IntPtr
"VkSurfaceKHR *"=IntPtr*

--exclude
VkInstance_T
VkSurfaceKHR_T
```

Note: ClangSharp `--remap` is byte-exact textual lookup. `VkInstance` typedef appears as plain `VkInstance instance` in `SDL_vulkan.h:187` (the typedef already buries the `*`), so the bare-name remap is correct. `VkSurfaceKHR *surface` (the C out-param at `:188`) needs the quoted-with-space form for byte-exact match.

- [ ] **Step 6.4: Apply Decision 5 — foreign-type IntPtr-emit for Direct3D + GDK in SDL_system.h**

Create `spikes/binding-generators/clangsharp/rsp/per-header/SDL_system.rsp`:

```
# Per-header overrides for SDL_system.h.
#
# Decision 5 (Foreign Type Boundary Policy): SDL_system.h surfaces three
# Direct3D COM interfaces under the Windows pass and two GDK handle types
# under the GDK pass. All are foreign-boundary types owned by external
# .NET bindings (Vortice.Direct3D{9,11,12} for D3D; future XGameRuntime
# binding for GDK). Emit as IntPtr at parameter/return positions for
# friction-free interop.
#
# Precedent: SDL2-CS uses IntPtr for IDirect3DDevice9 / ID3D11Device with
# `// Refers to an IDirect3DDevice9*` annotation (SDL2.cs:8747-8748).
# SDL2-CS omits SDL_GDKGetTaskQueue / SDL_GDKGetDefaultUser entirely; we
# emit them with IntPtr per Decision 5.

--remap
IDirect3DDevice9*=nint
ID3D11Device*=nint
ID3D12Device*=nint
XTaskQueueObject*=nint
XUser*=nint

--exclude
IDirect3DDevice9
ID3D11Device
ID3D12Device
XTaskQueueObject
XUser
```

- [ ] **Step 6.5: Regenerate**

Run: `python spikes/binding-generators/clangsharp/generate_bindings.py --scope full --codegen both --execute --clean-output --vcpkg-triplet x64-windows-hybrid --use-platform-header-shims`

Expected: regeneration completes; new per-header RSPs loaded for each affected header.

- [ ] **Step 6.6: Verify R6 canonicalizations took effect**

For each SDL pair, grep for the tag absence in both Compat and Modern outputs:

```bash
for tag in _SDL_AudioStream _SDL_GameController _SDL_Haptic _SDL_Joystick _SDL_Sensor _SDL_iconv_t; do
    count=$(grep -rn "$tag\b" spikes/binding-generators/clangsharp/src/Janset.SDL2.Core/Generated/ | wc -l)
    if [ "$count" -ne 0 ]; then
        echo "WARN: $tag still has $count references"
    fi
done
```

Expected: 0 matches each.

- [ ] **Step 6.7: Verify Decision 5 foreign-type IntPtr emit**

```bash
grep -n "VkInstance_T\|VkSurfaceKHR_T" spikes/binding-generators/clangsharp/src/Janset.SDL2.Core/Generated/Modern/SDL_vulkan.g.cs
grep -n "IntPtr\b" spikes/binding-generators/clangsharp/src/Janset.SDL2.Core/Generated/Modern/SDL_vulkan.g.cs | head -5
grep -rn "IDirect3DDevice9\|ID3D11Device\|ID3D12Device" spikes/binding-generators/clangsharp/src/Janset.SDL2.Core/Generated/Modern/Platforms/WindowsDesktop/ 2>/dev/null
grep -rn "XTaskQueueObject\|XUser\b" spikes/binding-generators/clangsharp/src/Janset.SDL2.Core/Generated/Modern/Platforms/GDK/ 2>/dev/null
```

Expected:
- `VkInstance_T` / `VkSurfaceKHR_T` struct declarations gone (tag structs excluded)
- `IntPtr` references appear in SDL_vulkan.g.cs parameter positions
- Direct3D struct declarations gone from WindowsDesktop platform output
- GDK tag struct declarations gone from GDK platform output

- [ ] **Step 6.8: Re-run oracle, confirm 0 duplicate-tag-typedef findings**

Run: `dotnet run --file spikes/binding-generators/clangsharp/oracle.cs -- --family sdl2-core --family sdl2-image --write-report`

Expected: report shows 0 `duplicate-tag-typedef` findings under Compatibility Risk. All 10 pairs from Step 6.1 are now resolved — 6 via canonicalization, 4 via tag-exclusion under Decision 5.

- [ ] **Step 6.9: Verify multi-TFM compile**

Run: `dotnet build spikes/binding-generators/clangsharp/src/Janset.SDL2.Image/Janset.SDL2.Image.csproj -c Release`

Expected: 0 errors, 0 warnings across all 5 TFMs.

- [ ] **Step 6.10: Slopwatch**

Run: `slopwatch analyze --fail-on warning --exclude "artifacts/**,external/**,vcpkg_installed/**,spikes/binding-generators/references/**,**/bin/**,**/obj/**"`

Expected: 0 issues.

- [ ] **Step 6.11: Commit**

CRLF-only churn on platform-view files should be excluded as in earlier tasks.

```bash
git add spikes/binding-generators/clangsharp/rsp/per-header/ spikes/binding-generators/clangsharp/src/ spikes/binding-generators/output/
git commit -m "$(cat <<'EOF'
feat(binding-spike): R6 canonicalization + Decision 5 foreign-type IntPtr-emit

Task 6 closes the 10 duplicate-tag-typedef findings from Task 4's oracle
discovery:

- 6 SDL-owned pairs canonicalized via R6 (--remap TAG=TYPEDEF):
  _SDL_AudioStream / _SDL_GameController / _SDL_Haptic / _SDL_Joystick /
  _SDL_Sensor / _SDL_iconv_t -> SDL_AudioStream / SDL_GameController /
  SDL_Haptic / SDL_Joystick / SDL_Sensor / SDL_iconv_t.

- 4 foreign-boundary pairs handled via Decision 5 Foreign Type Boundary
  Policy (emit as IntPtr / nint, exclude tag structs):
  VkInstance_T / VkSurfaceKHR_T (Vulkan, SDL_vulkan.rsp)
  XTaskQueueObject / XUser (GDK, SDL_system.rsp)
  Plus 3 Direct3D COM interfaces surfaced by the foreign-type survey:
  IDirect3DDevice9 / ID3D11Device / ID3D12Device (SDL_system.rsp).

User's interop friction concern preserved: foreign types stay as IntPtr
at SDL boundary so Silk.NET.Vulkan / Vortice.Windows / etc. consumers
pass values without explicit-conversion ceremony. SDL2-CS pragmatic
precedent (IntPtr + comment-annotated provenance) applies.

Oracle duplicate-tag-typedef findings: 10 -> 0. Multi-TFM compile clean
across 5 TFMs.

Refs:
- docs/binding-autogen/binding-generator-constitution.md
  §"Foreign Type Boundary Policy"
- docs/superpowers/specs/2026-05-24-clangsharp-priority-c-semantic-abi-design.md
  Decision 5
- docs/research/semantic-abi-type-classification-research.md
  Appendix B (Findings 11-18, 2026-05-24 surveys)

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

If no additional pairs were found, skip this commit and note in the audit log.

---

### Task 7: Slice C-C Exit Gate

- [ ] **Step 7.1: Full regenerate**

Run: `python spikes/binding-generators/clangsharp/generate_bindings.py --scope full --codegen both --execute --clean-output --vcpkg-triplet x64-windows-hybrid --use-platform-header-shims`

Expected: completes cleanly; postprocess pipeline includes new `guid-substitute` step.

- [ ] **Step 7.2: Oracle gate check**

Run: `dotnet run --file spikes/binding-generators/clangsharp/oracle.cs -- --family sdl2-core --family sdl2-image --write-report`

Verify in the report:
- `duplicate-tag-typedef`: **0 findings**.
- No remaining `SDL_hid_device_` or `SDL_semaphore` declarations: confirmed by grep in Tasks 2/3.
- No remaining `partial struct SDL_GUID`: confirmed by grep in Task 5.

- [ ] **Step 7.3: Multi-TFM compile gate**

Run:
```bash
dotnet build spikes/binding-generators/clangsharp/src/Janset.SDL2.Core/Janset.SDL2.Core.csproj -c Release
dotnet build spikes/binding-generators/clangsharp/src/Janset.SDL2.Image/Janset.SDL2.Image.csproj -c Release
```

Expected: 0 errors, 0 warnings across all 5 TFMs for both projects.

- [ ] **Step 7.4: Slopwatch hygiene check**

Run: `slopwatch analyze --fail-on warning --exclude "artifacts/**,external/**,vcpkg_installed/**,spikes/binding-generators/references/**,**/bin/**,**/obj/**"`

Expected: 0 issues. (Per AGENTS.md guidance, the spike references clone exclusion is required.)

- [ ] **Step 7.5: Commit (only if regeneration produced changes not already committed)**

If Tasks 2-6 left uncommitted regenerated content:
```bash
git add spikes/binding-generators/clangsharp/src/ spikes/binding-generators/output/
git commit -m "feat(binding-spike): Slice C-C exit gate — handle canonicalization + SDL_GUID closed

Oracle duplicate-tag-typedef: 0 findings.
Multi-TFM compile clean across 5 TFMs.
Slopwatch clean.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

Otherwise note Slice C-C complete.

---

## Phase 2 — Slice C-A: Scalars

### Task 8: Fix base.rsp wchar_t Remap

**Files:**
- Modify: `spikes/binding-generators/clangsharp/rsp/base.rsp` (lines 17-20)

- [ ] **Step 8.1: Verify the current dead-letter state**

Run: `grep -n "wchar_t" spikes/binding-generators/clangsharp/src/Janset.SDL2.Core/Generated/Modern/SDL_hidapi.g.cs | head -10`

Expected: matches showing `[NativeTypeName("wchar_t *")] public ushort*` — the existing `wchar_t*=nint` remap is not firing.

- [ ] **Step 8.2: Edit base.rsp**

Replace the dead `wchar_t*=nint` with space-containing variants on their own lines. Open `spikes/binding-generators/clangsharp/rsp/base.rsp` and change:

```
--remap
void*=nint
char=byte
wchar_t*=nint
```

to:

```
--remap
void*=nint
char=byte
wchar_t *=nint
const wchar_t *=nint
```

The space-containing variants match libclang's actual type spelling (research Finding 7 — ClangSharp `--remap` is byte-exact textual lookup; field types are rendered with space). Note the **unquoted** form: ClangSharp's RSP parser (System.CommandLine) treats each non-empty line as one argv element verbatim, so surrounding quotes (`"wchar_t *"=nint`) get included literally in the key and break the match. ppy SDL3-CS's argv-passing pattern (`--remap "wchar_t *=IntPtr"`) is shell-level quoting protecting the space across argv boundaries, not RSP file syntax.

- [ ] **Step 8.3: Regenerate**

Run: `python spikes/binding-generators/clangsharp/generate_bindings.py --scope full --codegen both --execute --clean-output --vcpkg-triplet x64-windows-hybrid --use-platform-header-shims`

- [ ] **Step 8.4: Verify the remap took effect**

Run:
```bash
grep -n "ushort\* serial_number\|ushort\* manufacturer_string\|ushort\* product_string" spikes/binding-generators/clangsharp/src/Janset.SDL2.Core/Generated/Modern/SDL_hidapi.g.cs
grep -n "nint serial_number\|nint manufacturer_string\|nint product_string" spikes/binding-generators/clangsharp/src/Janset.SDL2.Core/Generated/Modern/SDL_hidapi.g.cs
grep -cn "\[NativeTypeName(\"wchar_t \*\")\]" spikes/binding-generators/clangsharp/src/Janset.SDL2.Core/Generated/Modern/SDL_hidapi.g.cs
```

Expected:
- First grep (old shape): 0 matches.
- Second grep (new shape): 3 matches.
- Third grep: annotations preserved on the nint fields.

If first grep still shows matches, the remap didn't fire. Possible causes:
1. Surrounding quotes accidentally included in the line — System.CommandLine takes each RSP line as one argv element verbatim; remove the quotes (use plain `wchar_t *=nint`).
2. The field is via a typedef chain that bypasses pointer remap — fallback: also add `wchar_t=byte` pointee remap.
3. If RSP path completely doesn't work, proceed to Task 8.5 (postprocess fallback rewriter).

Implementation note (2026-05-24): the originally documented quoted form (`"wchar_t *"=nint`) and the escaped-space form (`wchar_t\ *=nint`) both failed in this environment (ClangSharp 17.0.1, libclang 17.0.4). The working syntax is unquoted with a literal space, one entry per line. ppy/SDL3-CS does not use an RSP file for this remap (it argv-passes `--remap` directly), which is why their `"wchar_t *=IntPtr"` form works for them and does not translate directly to our RSP-based config.

- [ ] **Step 8.5 (CONDITIONAL — only if Step 8.4 RSP fix failed): Create WcharStarToNintRewriter fallback**

Create `spikes/binding-generators/clangsharp/postprocess/WcharStarToNintRewriter.cs`:

```csharp
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Janset.SDL2.PostProcess;

/// <summary>
/// Fallback: rewrite ushort* to nint where the adjacent NativeTypeName
/// annotation declares wchar_t * (or const wchar_t *). Used when RSP
/// --remap does not reach the case.
/// </summary>
internal sealed class WcharStarToNintRewriter : CSharpSyntaxRewriter
{
    public bool AnyChanges { get; private set; }
    public void Reset() => AnyChanges = false;

    public override SyntaxNode? VisitFieldDeclaration(FieldDeclarationSyntax node)
    {
        if (HasWcharNativeTypeName(node.AttributeLists))
        {
            var newDeclaration = node.Declaration.WithType(
                SyntaxFactory.IdentifierName("nint")
                    .WithTriviaFrom(node.Declaration.Type));
            AnyChanges = true;
            return node.WithDeclaration(newDeclaration);
        }
        return base.VisitFieldDeclaration(node);
    }

    public override SyntaxNode? VisitParameter(ParameterSyntax node)
    {
        if (HasWcharNativeTypeName(node.AttributeLists) && node.Type is not null)
        {
            AnyChanges = true;
            return node.WithType(SyntaxFactory.IdentifierName("nint")
                .WithTriviaFrom(node.Type));
        }
        return base.VisitParameter(node);
    }

    public override SyntaxNode? VisitMethodDeclaration(MethodDeclarationSyntax node)
    {
        // Check return-type [return: NativeTypeName(...)]
        var returnAttrs = node.AttributeLists.Where(al =>
            al.Target?.Identifier.ValueText == "return");

        if (returnAttrs.Any(al => HasWcharAttr(al.Attributes)))
        {
            AnyChanges = true;
            node = node.WithReturnType(SyntaxFactory.IdentifierName("nint")
                .WithTriviaFrom(node.ReturnType));
        }
        return base.VisitMethodDeclaration(node);
    }

    private static bool HasWcharNativeTypeName(SyntaxList<AttributeListSyntax> lists)
        => lists.Any(al => HasWcharAttr(al.Attributes));

    private static bool HasWcharAttr(SeparatedSyntaxList<AttributeSyntax> attrs)
        => attrs.Any(a =>
            a.Name.ToString() == "NativeTypeName" &&
            a.ArgumentList?.Arguments.Any(arg =>
                arg.Expression is LiteralExpressionSyntax lit &&
                (lit.Token.ValueText == "wchar_t *" ||
                 lit.Token.ValueText == "const wchar_t *")) == true);
}
```

Wire into `Program.cs` mode list (`wchar-fallback`) and orchestrator pipeline. Apply after regeneration. Verify expected behavior with grep checks from Step 8.4.

- [ ] **Step 8.6: Verify multi-TFM compile**

Run: `dotnet build spikes/binding-generators/clangsharp/src/Janset.SDL2.Image/Janset.SDL2.Image.csproj -c Release`

Expected: 0 errors, 0 warnings.

- [ ] **Step 8.7: Commit**

```bash
git add spikes/binding-generators/clangsharp/rsp/base.rsp spikes/binding-generators/clangsharp/src/
# If fallback rewriter was needed:
git add spikes/binding-generators/clangsharp/postprocess/WcharStarToNintRewriter.cs spikes/binding-generators/clangsharp/postprocess/Program.cs spikes/binding-generators/clangsharp/generate_bindings.py
git commit -m "$(cat <<'EOF'
feat(binding-spike): fix shared wchar_t* opaque mapping at raw ABI (R1)

Replace dead-letter `wchar_t*=nint` (no-space) with quoted, space-
containing variants matching libclang's actual type spelling:
  "wchar_t *"=nint
  "const wchar_t *"=nint

ClangSharp --remap is byte-exact textual lookup (research Finding 7);
the original entry never fired because libclang renders struct field
types with a space. ppy/SDL3-CS uses the same quoted-with-space pattern.

Shared wchar_t* (HIDAPI fields, SDL_wcs* functions, ~15 symbols) now
emit as nint at raw ABI. Opaque nint IS the ABI-correct mapping (not a
Layer 3 friendly-layer repair); no portable C# primitive maps wchar_t
correctly across 7 target RIDs.

Refs: docs/superpowers/specs/2026-05-24-clangsharp-priority-c-semantic-abi-design.md
Slice C-A R1, Constitution §"wchar_t" Priority C mechanism.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

### Task 9: SDL_stdinc.rsp Excludes (R2 Convenience Helpers Drop)

**Files:**
- Create: `spikes/binding-generators/clangsharp/rsp/per-header/SDL_stdinc.rsp`

- [ ] **Step 9.1: Verify the current emission state**

Run: `grep -n "SDL_lround\|SDL_lroundf\|SDL_ltoa\|SDL_ultoa\|SDL_strtol\|SDL_strtoul" spikes/binding-generators/clangsharp/src/Janset.SDL2.Core/Generated/Modern/SDL_stdinc.g.cs | head -10`

Expected: matches showing the six convenience helpers currently emitted with `int`/`uint` returns (wrong on Unix LP64 per research Finding 8).

- [ ] **Step 9.2: Create the per-header RSP**

```bash
cat > spikes/binding-generators/clangsharp/rsp/per-header/SDL_stdinc.rsp <<'EOF'
# Per-header overrides for SDL_stdinc.h.
#
# R2 C long convenience helpers — drop all-TFM per SDL2-CS precedent.
# SDL provides these for platforms with incomplete libc; .NET callers
# have BCL equivalents:
#   SDL_lround/SDL_lroundf -> Math.Round / MathF.Round
#   SDL_ltoa/SDL_ultoa     -> value.ToString()
#   SDL_strtol/SDL_strtoul -> long.Parse / ulong.Parse
# SDL2 wiki does not document them as user-facing API.
# Constitution L223-228 high-risk C `long` symbols, Priority C
# disposition: deferred all-TFM.

--exclude
SDL_lround
SDL_lroundf
SDL_ltoa
SDL_ultoa
SDL_strtol
SDL_strtoul
EOF
```

- [ ] **Step 9.3: Regenerate**

Run: `python spikes/binding-generators/clangsharp/generate_bindings.py --scope full --codegen both --execute --clean-output --vcpkg-triplet x64-windows-hybrid --use-platform-header-shims`

- [ ] **Step 9.4: Verify the excludes took effect**

Run: `grep -n "SDL_lround\|SDL_lroundf\|SDL_ltoa\|SDL_ultoa\|SDL_strtol\|SDL_strtoul" spikes/binding-generators/clangsharp/src/Janset.SDL2.Core/Generated/Modern/SDL_stdinc.g.cs`

Expected: 0 matches.

Also verify the symbols are absent from `Compat/SDL_stdinc.g.cs`:

Run: `grep -n "SDL_lround\|SDL_strtol" spikes/binding-generators/clangsharp/src/Janset.SDL2.Core/Generated/Compat/SDL_stdinc.g.cs`

Expected: 0 matches.

- [ ] **Step 9.5: Verify multi-TFM compile**

Run: `dotnet build spikes/binding-generators/clangsharp/src/Janset.SDL2.Image/Janset.SDL2.Image.csproj -c Release`

Expected: 0 errors, 0 warnings.

- [ ] **Step 9.6: Commit**

```bash
git add spikes/binding-generators/clangsharp/rsp/per-header/SDL_stdinc.rsp spikes/binding-generators/clangsharp/src/
git commit -m "$(cat <<'EOF'
feat(binding-spike): drop SDL_stdinc C long convenience helpers (R2)

Six symbols (SDL_lround, SDL_lroundf, SDL_ltoa, SDL_ultoa, SDL_strtol,
SDL_strtoul) excluded via rsp/per-header/SDL_stdinc.rsp. BCL equivalents
exist (Math.Round, long.Parse, ToString); SDL2-CS has shipped this
posture for 10+ years; SDL2 wiki does not document them as user-facing
API. Constitution L223-228 Priority C disposition: deferred all-TFM.

Refs: docs/superpowers/specs/2026-05-24-clangsharp-priority-c-semantic-abi-design.md
Slice C-A R2 (convenience helpers), Constitution §"C `long` And `unsigned long`"
Priority C hybrid strategy.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

### Task 10: ThreadIdDualDispatchRewriter (R2 Structural)

**Files:**
- Create: `spikes/binding-generators/clangsharp/postprocess/ThreadIdDualDispatchRewriter.cs`
- Modify: `spikes/binding-generators/clangsharp/postprocess/Program.cs` (wire `threadid-dispatch` mode)
- Modify: `spikes/binding-generators/clangsharp/generate_bindings.py` (invoke new postprocess mode)

- [ ] **Step 10.1: Verify the current threadID emission state**

Run: `grep -n "SDL_ThreadID\|SDL_GetThreadID\|SDL_threadID" spikes/binding-generators/clangsharp/src/Janset.SDL2.Core/Generated/Modern/SDL_thread.g.cs | head -10`

Expected: matches showing methods with `[return: NativeTypeName("SDL_threadID")]` and managed return type `uint` (wrong on Unix LP64).

- [ ] **Step 10.2: Create the rewriter (mode-aware Compat/Modern emit)**

The rewriter is mode-aware: it inspects the input directory's path segment (`Compat` or `Modern`) and emits the single TFM-appropriate form per output. The csproj's conditional `<Compile Include>` items route `Generated/Compat/**/*.cs` to `netstandard2.0` + `net462` only and `Generated/Modern/**/*.cs` to `net6+` only, so no `#if NET6_0_OR_GREATER` directives are needed in the emitted code (each file already compiles under exactly one TFM range). This mirrors the existing `libraryimport` postprocess pattern (Compat keeps `[DllImport]`; Modern is rewritten to `[LibraryImport]`) and `PlatformDeltaPostProcessor`'s path-based mode detection.

```csharp
// spikes/binding-generators/clangsharp/postprocess/ThreadIdDualDispatchRewriter.cs

using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Janset.SDL2.PostProcess;

/// <summary>
/// R2 structural-symbol hybrid emit for SDL_threadID family, mode-aware.
///
/// Sensor: methods whose return is marked [return: NativeTypeName("SDL_threadID")]
/// or [return: NativeTypeName("unsigned long")] AND the method name matches
/// SDL_(ThreadID|GetThreadID).
///
/// Modern emit (single form, requires net6+):
///   [LibraryImport(LibName, EntryPoint = "SDL_ThreadID")]
///   [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
///   [return: NativeTypeName("SDL_threadID")]
///   public static partial CULong SDL_ThreadID();
///
/// Compat emit (single form, netstandard2.0/net462 only):
///   [return: NativeTypeName("SDL_threadID")]
///   public static ulong SDL_ThreadID()
///   {
///       if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
///           return SDL_ThreadID_Win32();
///       return (ulong)SDL_ThreadID_Unix64();
///   }
///   [DllImport(LibName, EntryPoint = "SDL_ThreadID", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
///   private static extern uint SDL_ThreadID_Win32();
///   [DllImport(LibName, EntryPoint = "SDL_ThreadID", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
///   private static extern nint SDL_ThreadID_Unix64();
///
/// Microsoft's documented dual-DllImport pattern for cross-platform C long
/// on legacy TFMs that lack CLong/CULong. Mode is detected from the input
/// directory's `Compat` / `Modern` path segment (mirrors PlatformDeltaPostProcessor);
/// missing both throws so a mis-pointed CLI fails loudly.
/// </summary>
internal sealed class ThreadIdDualDispatchRewriter : CSharpSyntaxRewriter
{
    internal enum Mode { Compat, Modern }

    public static Mode DetectMode(string inputDir)
    {
        // Walk path segments right-to-left; return Compat or Modern when
        // matched. Throw if neither is present so a mis-pointed CLI fails
        // loudly rather than silently emitting the wrong shape.
    }

    public ThreadIdDualDispatchRewriter(Mode mode) { _mode = mode; }

    public bool AnyChanges { get; private set; }
    public void Reset() { /* clear AnyChanges + pending substitutions */ }

    // Detect SDL_(ThreadID|GetThreadID) methods with
    // [return: NativeTypeName("SDL_threadID")] or ("unsigned long"). For each
    // hit, stage a text-level substitution targeting node.FullSpan with a
    // pre-rendered replacement block.
    public override SyntaxNode? VisitMethodDeclaration(MethodDeclarationSyntax node) { /* ... */ }

    // Apply staged substitutions in reverse order against the source text,
    // then re-parse so ToFullString() emits the verbatim replacement.
    // (Roslyn's tree mutation can't emit multiple sibling members cleanly in
    // one pass; text-level substitution sidesteps that. The original
    // implementation hit `#if` strip-on-parse issues — no longer relevant
    // now that we emit a single branch, but the text approach is retained
    // for cleanly inserting the Compat case's 3 members per source method.)
    public override SyntaxNode? VisitCompilationUnit(CompilationUnitSyntax node) { /* ... */ }

    // Modern: single [LibraryImport] + CULong form (no #if).
    private static string BuildModernReplacement(...) { /* ... */ }

    // Compat: single managed wrapper + 2 private [DllImport] form (no #if).
    private static string BuildCompatReplacement(...) { /* ... */ }
}
```

Full implementation lives at `spikes/binding-generators/clangsharp/postprocess/ThreadIdDualDispatchRewriter.cs`.

- [ ] **Step 10.3: Wire into Program.cs**

Add `threadid-dispatch` to the mode list and switch in `Program.cs` (same pattern as Task 5 Step 5.3). The switch case calls `ThreadIdDualDispatchRewriter.DetectMode(inputDir)` to choose Compat vs Modern from the input directory's path segment, then passes the mode to the rewriter's constructor. Logs the selected mode for traceability (mirrors `PlatformDeltaPostProcessor`'s pattern). No new CLI flag.

- [ ] **Step 10.4: Build the postprocess project**

Run: `dotnet build spikes/binding-generators/clangsharp/postprocess/Janset.SDL2.PostProcess.csproj -c Release`

Expected: 0 errors. Resolve any compilation issues in the rewriter inline.

- [ ] **Step 10.5: Apply the rewriter manually as a one-shot to both output trees**

Run:
```bash
dotnet run --project spikes/binding-generators/clangsharp/postprocess/Janset.SDL2.PostProcess.csproj -c Release -- threadid-dispatch spikes/binding-generators/clangsharp/src/Janset.SDL2.Core/Generated/Compat
dotnet run --project spikes/binding-generators/clangsharp/postprocess/Janset.SDL2.PostProcess.csproj -c Release -- threadid-dispatch spikes/binding-generators/clangsharp/src/Janset.SDL2.Core/Generated/Modern
```

Expected: SDL_thread.g.cs in both directories transformed.

- [ ] **Step 10.6: Verify each output has a single TFM-appropriate branch and no `#if` directives**

Run:
```bash
grep -n "#if\|SDL_ThreadID_Win32\|SDL_ThreadID_Unix64\|CULong SDL_ThreadID" spikes/binding-generators/clangsharp/src/Janset.SDL2.Core/Generated/Modern/SDL_thread.g.cs
grep -n "#if\|SDL_ThreadID_Win32\|SDL_ThreadID_Unix64\|CULong SDL_ThreadID" spikes/binding-generators/clangsharp/src/Janset.SDL2.Core/Generated/Compat/SDL_thread.g.cs
```

Expected:
- Modern: single `[LibraryImport]` + `public static partial CULong SDL_ThreadID()` (and the same for `SDL_GetThreadID`). Zero `#if` matches. Zero `_Win32` / `_Unix64` matches.
- Compat: single `public static ulong SDL_ThreadID()` managed wrapper + private `_Win32` / `_Unix64` `[DllImport]` pair (and the same for `SDL_GetThreadID`). Zero `#if` matches. Zero `CULong` matches.

TFM gating is handled by the csproj's conditional `<Compile Include>` (Compat tree → `netstandard2.0` + `net462`; Modern tree → `net6+`), so each file only compiles under one TFM range and `#if` directives would be dead code.

- [ ] **Step 10.7: Verify multi-TFM compile (critical gate — each branch must compile under its targeted TFMs)**

Run: `dotnet build spikes/binding-generators/clangsharp/src/Janset.SDL2.Image/Janset.SDL2.Image.csproj -c Release`

Expected: 0 errors, 0 warnings. Modern (net8/net9/net10) and legacy (netstandard2.0/net462) TFMs both build.

If errors:
- Modern TFM error around `CULong`: ensure `using System.Runtime.InteropServices;` is in the file (the libraryimport postprocess already inserts it; the threadid-dispatch emit relies on that).
- Legacy TFM error around `RuntimeInformation` / `OSPlatform`: ensure `using System.Runtime.InteropServices;` is in the Compat output (ClangSharp emits it for files that already contain DllImport, which SDL_thread.g.cs does).
- Legacy TFM error around `RuntimeInformation` on net462 only: the `System.Runtime.InteropServices.RuntimeInformation` OOB package is gated in the Core csproj's `net462` `ItemGroup` — verify the conditional is intact.

- [ ] **Step 10.8: Wire into the orchestrator pipeline**

Following the same pattern as `guid-substitute` (Task 5 Step 5.8), add a `threadid-dispatch` pipeline step in `generate_bindings.py` for both Compat and Modern output directories.

- [ ] **Step 10.9: Verify the full regeneration round-trip**

Run: `python spikes/binding-generators/clangsharp/generate_bindings.py --scope full --codegen both --execute --clean-output --vcpkg-triplet x64-windows-hybrid --use-platform-header-shims`

Then re-run the grep checks from Step 10.6 and the compile-check from Step 10.7.

- [ ] **Step 10.10: Commit**

```bash
git add spikes/binding-generators/clangsharp/postprocess/ spikes/binding-generators/clangsharp/generate_bindings.py spikes/binding-generators/clangsharp/src/
git commit -m "$(cat <<'EOF'
feat(binding-spike): ThreadIdDualDispatchRewriter for SDL_ThreadID family

R2 structural-symbol hybrid emit, mode-aware. SDL_ThreadID / SDL_GetThreadID
are structural (different ID space from Thread.CurrentThread.ManagedThreadId)
and cannot be dropped. The csproj routes Generated/Compat to
netstandard2.0+net462 and Generated/Modern to net6+ via conditional
<Compile Include>, so the rewriter emits a single branch per output (no
#if directives, mirroring the libraryimport postprocess split):

- Modern output (single branch): [LibraryImport] + CULong return.
- Compat output (single branch): managed ulong wrapper +
  RuntimeInformation.IsOSPlatform dispatch + 2 private [DllImport] with
  uint (Windows LLP64) / nint (Unix LP64) returns — Microsoft's
  documented cross-platform C-long pattern.

Mode detected from the input directory's Compat/Modern path segment
(mirrors PlatformDeltaPostProcessor); no Program.cs CLI flag change.

Sensor: [return: NativeTypeName("SDL_threadID")] on SDL_(ThreadID|GetThreadID)
method names. Wired into orchestrator postprocess pipeline.

Refs: docs/superpowers/specs/2026-05-24-clangsharp-priority-c-semantic-abi-design.md
Slice C-A R2 (structural), Constitution §"C `long`" Priority C hybrid strategy.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

### Task 11: Per-TFM ABI Smoke Test for SDL_threadID (Runtime Evidence via InternalsVisibleTo)

**Scope upgrade (2026-05-24):** original plan placeholdered the test with `Assert.That(true).IsTrue()` and deferred the real SDL call to Layer 2. Per the "no workarounds, no shortcuts" hard rule we upgrade now: expose Layer 1 `SDLNative` to the test assembly via `InternalsVisibleTo` and exercise `SDL_ThreadID()` for real. The host-side dispatch path (Win Compat → Win32 dual-DllImport; Win Modern → CULong+LibraryImport) is verified at `dotnet test` time. Unix64 nint path stays on the CI RID matrix.

**Location upgrade (2026-05-24):** the project lives next to the spike at `spikes/binding-generators/clangsharp/tests/abi-tests/`, NOT under `tests/smoke-tests/`. The smoke-tests hierarchy enforces a package-consumer contract (`build/msbuild/Janset.Smoke.props` requires `LocalPackageFeed` + `JansetSmokeSdl2Families` declarations) that conflicts with a `ProjectReference`-based design. Hosting under the spike preserves the "test lives with the code it tests" principle and lets the test graduate to a production location when the spike itself graduates.

**MSBuild inheritance correction (2026-05-24):** `spikes/binding-generators/Directory.Build.props` exists (LangVersion=preview, Nullable=enable, ImplicitUsings=enable, AllowUnsafeBlocks=true, TreatWarningsAsErrors=true, CPM=true) but does NOT `<Import>` the repo-root `Directory.Build.props`. MSBuild's nearest-ancestor lookup stops there, so `$(ExecutableTargetFrameworks)` from the repo root never reaches the spike's projects. The sibling projects (`Janset.SDL2.Core.csproj`, `Janset.SDL2.Image.csproj`) **hardcode** their TFM lists in response. The AbiTests csproj follows the same convention: hardcode `net10.0;net9.0;net8.0;net462` literally (drops netstandard2.0 because the test project is executable).

**Files:**
- Modify: `spikes/binding-generators/clangsharp/src/Janset.SDL2.Core/Janset.SDL2.Core.csproj` (add `<InternalsVisibleTo>` item)
- Create: `spikes/binding-generators/clangsharp/tests/abi-tests/AbiTests.csproj`
- Create: `spikes/binding-generators/clangsharp/tests/abi-tests/ThreadIdAbiTests.cs`

- [ ] **Step 11.1: Inspect existing smoke-test conventions (reference only — do NOT inherit the smoke contract)**

Read `tests/smoke-tests/package-smoke/PackageConsumer.Smoke/PackageConsumer.Smoke.csproj` + `PackageSmokeTests.cs` for TUnit + apphost + PolySharp conventions.

**Do NOT host the AbiTests project under `tests/smoke-tests/`** — that hierarchy's `Directory.Build.props` forces `JansetSmokeSdl2Families` + `LocalPackageFeed` declarations (errors `JNSMK001` / `JNSMK009`) which conflict with the ProjectReference-only design here. The AbiTests project lives at `spikes/binding-generators/clangsharp/tests/abi-tests/` and inherits only the repo-root `Directory.Build.props`.

Expected conventions to mirror (from PackageConsumer.Smoke):
- `$(ExecutableTargetFrameworks)` = `net10.0;net9.0;net8.0;net462` (root `Directory.Build.props`; drops netstandard2.0 — not executable).
- TUnit + Microsoft Testing Platform apphost pattern → `OutputType=Exe`, `IsTestProject=true`.
- PolySharp is required for net462 (modern-attribute polyfills for TUnit's source-generated bootstrap).
- net462 ItemGroup needs `System.Memory` + `System.Runtime.CompilerServices.Unsafe`.
- Native-touching tests use `[NotInParallel]`.

Do not redefine CPM-managed package versions. CPM entries for TUnit, PolySharp, System.Memory, System.Runtime.CompilerServices.Unsafe already exist in `Directory.Packages.props`.

- [ ] **Step 11.2: Add `InternalsVisibleTo` to `Janset.SDL2.Core.csproj`**

Add (or extend) an `ItemGroup` in `spikes/binding-generators/clangsharp/src/Janset.SDL2.Core/Janset.SDL2.Core.csproj`:

```xml
<ItemGroup>
  <InternalsVisibleTo Include="Janset.SDL2.AbiTests" />
</ItemGroup>
```

This exposes the internal `SDLNative` class (Layer 1 raw ABI container) to the AbiTests assembly only. The assembly name must match exactly — the AbiTests project sets `<AssemblyName>Janset.SDL2.AbiTests</AssemblyName>` in Step 11.3.

Constitution Layer Contract (Layer 1 = internal raw ABI) is preserved: `SDLNative` stays internal; only this specific test assembly gets access.

- [ ] **Step 11.3: Create `AbiTests.csproj`**

`spikes/binding-generators/clangsharp/tests/abi-tests/AbiTests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <!--
    Per-TFM ABI smoke for the ClangSharp spike's rewriter output. References
    the spike's Janset.SDL2.Core ProjectReference directly (not the published
    NuGet) and exercises Layer 1 internals via InternalsVisibleTo. Build +
    test evidence per executable TFM verifies the ThreadIdDualDispatchRewriter
    produces valid code for both Compat (net462) and Modern (net8+) outputs.
  -->
  <PropertyGroup>
    <!--
      TFMs hardcoded per spike sibling convention (Core/Image both hardcode).
      The spike's Directory.Build.props does not import the repo-root one, so
      $(ExecutableTargetFrameworks) is empty here. Dropped netstandard2.0
      because this is an executable test project.
    -->
    <TargetFrameworks>net10.0;net9.0;net8.0;net462</TargetFrameworks>
    <OutputType>Exe</OutputType>
    <IsTestProject>true</IsTestProject>
    <IsPackable>false</IsPackable>

    <AssemblyName>Janset.SDL2.AbiTests</AssemblyName>
    <RootNamespace>Janset.SDL2.AbiTests</RootNamespace>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\Janset.SDL2.Core\Janset.SDL2.Core.csproj" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="TUnit" />
    <PackageReference Include="PolySharp" PrivateAssets="all" IncludeAssets="runtime;build;native;contentfiles;analyzers;buildtransitive" />
  </ItemGroup>

  <ItemGroup Condition="'$(TargetFramework)' == 'net462'">
    <PackageReference Include="System.Memory" />
    <PackageReference Include="System.Runtime.CompilerServices.Unsafe" />
  </ItemGroup>

  <!--
    Copy SDL2 native binary from the repo-root vcpkg_installed dir into the
    test output so the runtime loader can resolve SDL2.dll. Host triplet
    (x64-windows-hybrid) only — CI RID matrix overrides this for other RIDs.
    Path is relative to spikes/binding-generators/clangsharp/tests/abi-tests/.
  -->
  <ItemGroup>
    <None Include="..\..\..\..\..\vcpkg_installed\x64-windows-hybrid\bin\SDL2.dll"
          CopyToOutputDirectory="PreserveNewest"
          Visible="false" />
  </ItemGroup>
</Project>
```

The canonical host-triplet path is `vcpkg_installed/x64-windows-hybrid/bin/SDL2.dll` at the repo root (confirmed during plan revision; populated by `external/vcpkg/vcpkg install --triplet x64-windows-hybrid --overlay-triplets=vcpkg-overlay-triplets`). If the file doesn't exist on a fresh checkout, the implementer must run vcpkg install first per `docs/playbook/local-development.md` (Step 2 "Install Native Dependencies via vcpkg"). STOP and report BLOCKED if the file genuinely doesn't materialize.

If CPM is missing entries for `TUnit`, `PolySharp`, `System.Memory`, or `System.Runtime.CompilerServices.Unsafe`, do NOT add them to `Directory.Packages.props` unilaterally — STOP and report BLOCKED so the orchestrator can confirm with the user. (PackageConsumer.Smoke already references these, so the entries almost certainly exist; just don't invent versions.)

- [ ] **Step 11.4: Create `ThreadIdAbiTests.cs`**

`spikes/binding-generators/clangsharp/tests/abi-tests/ThreadIdAbiTests.cs`:

```csharp
using SDL2;

namespace Janset.SDL2.AbiTests;

/// <summary>
/// Layer 1 raw ABI smoke for the SDL_ThreadID family. Exercises the
/// ThreadIdDualDispatchRewriter's per-mode output:
///   - Modern TFMs (net8+): CULong return via [LibraryImport].
///   - Compat TFM (net462): managed ulong wrapper + RuntimeInformation
///     dispatch + dual private [DllImport] (Win32 uint / Unix64 nint).
///
/// On Windows host, both modes dispatch through the 32-bit Win32 branch.
/// The Unix64 nint path is exercised by the CI per-RID matrix. The build
/// itself (per-TFM compile of this project against Janset.SDL2.Core) is
/// also evidence — if the rewriter's output were invalid on net462 where
/// CULong does not exist, the build would fail.
/// </summary>
[NotInParallel]
public sealed class ThreadIdAbiTests
{
    [Test]
    [Category("AbiSmoke")]
    public async Task SDL_ThreadID_Returns_NonZero_On_Host_Platform()
    {
#if NET6_0_OR_GREATER
        ulong threadId = (ulong)SDLNative.SDL_ThreadID().Value;
#else
        ulong threadId = SDLNative.SDL_ThreadID();
#endif

        await Assert.That(threadId).IsNotEqualTo(0UL);
    }
}
```

Notes:
- The `#if NET6_0_OR_GREATER` directive is in the **test code**, not the rewriter output. The test project compiles per-TFM and adapts to the surface that Janset.SDL2.Core exposes on each TFM (CULong on net6+, ulong on legacy). This is the correct place for a `#if` — the test bridges two valid Layer 1 surfaces. Contrast Task 10's removed `#if` which was inside generated code that the csproj already file-routes per-TFM.
- The `using SDL2;` brings the `SDLNative` internal type into scope (allowed by InternalsVisibleTo).
- `[NotInParallel]` because the test calls into a native runtime singleton.

- [ ] **Step 11.5: Build the AbiTests project (per-TFM compile evidence)**

Run: `dotnet build spikes/binding-generators/clangsharp/tests/abi-tests/AbiTests.csproj -c Release`

Expected: 0 errors and 0 warnings across `net10.0;net9.0;net8.0;net462`. This verifies:
1. The spike's Janset.SDL2.Core (with rewriter output) compiles in a multi-TFM consumer.
2. `InternalsVisibleTo` correctly exposes `SDLNative` to the test assembly.
3. PolySharp polyfills are sufficient for net462 TUnit bootstrap.

- [ ] **Step 11.6: Run the smoke (runtime evidence on host)**

Run on Windows host:

```bash
dotnet test spikes/binding-generators/clangsharp/tests/abi-tests/AbiTests.csproj -c Release --framework net10.0
dotnet test spikes/binding-generators/clangsharp/tests/abi-tests/AbiTests.csproj -c Release --framework net8.0
dotnet test spikes/binding-generators/clangsharp/tests/abi-tests/AbiTests.csproj -c Release --framework net462
```

Expected: 1/1 test passes per TFM. The net10/net8 invocations exercise the CULong + LibraryImport path. The net462 invocation exercises the managed `ulong` wrapper + RuntimeInformation dispatch + Win32 32-bit `uint` DllImport path.

If `SDL_ThreadID()` returns 0 on any TFM, STOP and report BLOCKED. A returned 0 means the dispatch or marshalling is broken — Layer 1 must report a non-zero current thread ID per SDL2 semantics.

If `DllNotFoundException` fires, the vcpkg native lookup in Step 11.3 failed — STOP and report BLOCKED with the resolved path.

- [ ] **Step 11.7: Add a CI matrix note**

Append to `spikes/binding-generators/README.md` (or wherever the spike's CI guidance lives) a short note:

> The `spikes/binding-generators/clangsharp/tests/abi-tests` project exercises Layer 1 `SDLNative.SDL_ThreadID()` runtime evidence per executable TFM (net462, net8.0, net9.0, net10.0). Host-side this covers Win32 32-bit `uint` and CULong+LibraryImport paths. Unix64 (Linux x64/arm64, macOS x64/arm64) `nint` returns must be exercised on the CI per-RID matrix by overriding the SDL2 native source path.

- [ ] **Step 11.8: Commit**

```bash
git add spikes/binding-generators/clangsharp/src/Janset.SDL2.Core/Janset.SDL2.Core.csproj \
        spikes/binding-generators/clangsharp/tests/abi-tests/ \
        spikes/binding-generators/README.md
git commit -m "$(cat <<'EOF'
test(binding-spike): per-TFM ABI runtime smoke for SDL_threadID dispatch

New spikes/binding-generators/clangsharp/tests/abi-tests/AbiTests.csproj
targets executable TFMs (net462, net8.0, net9.0, net10.0) and references
Janset.SDL2.Core via ProjectReference. Layer 1 raw ABI access enabled via
`<InternalsVisibleTo Include="Janset.SDL2.AbiTests" />` on Core. Project
lives next to the spike (not under tests/smoke-tests/) because that
hierarchy enforces a package-consumer contract incompatible with the
ProjectReference-only design here.

ThreadIdAbiTests.SDL_ThreadID_Returns_NonZero_On_Host_Platform exercises:
  - net8+ (Modern output): CULong return via [LibraryImport].
  - net462 (Compat output): managed ulong wrapper + RuntimeInformation
    dispatch + Win32 32-bit `uint` [DllImport].

Build evidence on all 4 executable TFMs + runtime evidence on Windows
host. Unix64 nint path stays on the CI per-RID matrix per
spikes/binding-generators/README.md note.

Constitution L220 evidence: per-RID ABI shape verified at runtime where
the host RID is exercisable; the matrix completes when CI runs the
remaining RIDs.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

### Task 12: Slice C-A Exit Gate

- [ ] **Step 12.1: Full regenerate**

Run: `python spikes/binding-generators/clangsharp/generate_bindings.py --scope full --codegen both --execute --clean-output --vcpkg-triplet x64-windows-hybrid --use-platform-header-shims`

- [ ] **Step 12.2: Oracle gate check**

Run: `dotnet run --file spikes/binding-generators/clangsharp/oracle.cs -- --family sdl2-core --family sdl2-image --write-report`

Verify in the report:
- `platform-sensitive-wchar`: **0 findings**.
- `platform-sensitive-long`: **0 findings**.

- [ ] **Step 12.3: Multi-TFM compile gate**

Run:
```bash
dotnet build spikes/binding-generators/clangsharp/src/Janset.SDL2.Image/Janset.SDL2.Image.csproj -c Release
dotnet build spikes/binding-generators/clangsharp/tests/abi-tests/AbiTests.csproj -c Release
```

Expected: 0 errors, 0 warnings. Janset.SDL2.Image targets all 5 TFMs (net10/net9/net8/netstandard2.0/net462); AbiTests targets 4 executable TFMs (net10/net9/net8/net462).

- [ ] **Step 12.4: Slopwatch check**

Run: `slopwatch analyze --fail-on warning --exclude "artifacts/**,external/**,vcpkg_installed/**,spikes/binding-generators/references/**,**/bin/**,**/obj/**"`

Expected: 0 issues.

- [ ] **Step 12.5: Commit (only if regeneration produced changes not already committed)**

```bash
git add spikes/binding-generators/clangsharp/src/ spikes/binding-generators/output/
git commit -m "feat(binding-spike): Slice C-A exit gate — scalars closed

Oracle platform-sensitive-wchar: 0 findings.
Oracle platform-sensitive-long: 0 findings.
Multi-TFM compile clean across 5 TFMs (Janset.SDL2.Image + AbiTests).
Slopwatch clean.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

Otherwise note Slice C-A complete.

---

## Phase 3 — Slice C-B: Uniform Pattern B Opaque Handle Struct Emit

Largest slice; **must run after Slice C-C** (auto-detect channel requires canonical names from R6).

### Task 13: OpaqueHandleEmitRewriter Scaffolding

**Files:**
- Create: `spikes/binding-generators/clangsharp/postprocess/OpaqueHandleEmitRewriter.cs`
- Modify: `spikes/binding-generators/clangsharp/postprocess/Program.cs` (wire `uniform-opaque` mode)

- [ ] **Step 13.1: Verify the current empty-struct opaque inventory**

Run:
```bash
grep -rn "public partial struct SDL_\w\+\s*$" spikes/binding-generators/clangsharp/src/Janset.SDL2.Core/Generated/Modern/ | grep -v "_\|InlineArray\|union\|Struct\b" | head -20
```

Expected: ~12 empty struct declarations matching auto-detect criteria (SDL_Window, SDL_Renderer, SDL_Texture, etc.).

- [ ] **Step 13.2: Create the rewriter scaffold**

```csharp
// spikes/binding-generators/clangsharp/postprocess/OpaqueHandleEmitRewriter.cs

using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Janset.SDL2.PostProcess;

/// <summary>
/// Slice C-B uniform Pattern B opaque handle emit.
///
/// Two input channels:
///   1. Auto-detect: empty `public partial struct X { }` declarations whose
///      name appears in [NativeTypeName("X *")] annotations somewhere.
///   2. Force-opaque: Constitution-bound allow-list (SDL_RWops, SDL_SysWMinfo,
///      SDL_SysWMmsg) — struct body cleared and replaced with Pattern B.
///
/// Pattern B shape per design Decision 1:
///   [StructLayout(LayoutKind.Sequential)]
///   public readonly partial struct X : IEquatable<X>
///   {
///       public X(nint value) { Value = value; }
///       public nint Value { get; }
///       public bool IsNull => Value == 0;
///       public bool IsNotNull => Value != 0;
///       public static X Null => default;
///       public nint DangerousGetHandle() => Value;
///       public bool Equals(X other) => Value == other.Value;
///       public override bool Equals(object? obj) => obj is X other && Equals(other);
///       public override int GetHashCode() => Value.GetHashCode();
///       public static bool operator ==(X left, X right) => left.Equals(right);
///       public static bool operator !=(X left, X right) => !left.Equals(right);
///       public static explicit operator nint(X value) => value.Value;
///       public static explicit operator X(nint value) => new(value);
///   }
///
/// Reference rewrite: every X* in raw ABI signatures rewrites to X by-value.
/// Double-pointer X** and `out X` positions are preserved.
/// </summary>
internal sealed class OpaqueHandleEmitRewriter : CSharpSyntaxRewriter
{
    // Force-opaque allow-list per Constitution L293-302
    private static readonly HashSet<string> ForceOpaqueNames = new(StringComparer.Ordinal)
    {
        "SDL_RWops",
        "SDL_SysWMinfo",
        "SDL_SysWMmsg",
    };

    private readonly HashSet<string> _knownHandles;

    public OpaqueHandleEmitRewriter(HashSet<string> autoDetectedHandles)
    {
        _knownHandles = new HashSet<string>(autoDetectedHandles, StringComparer.Ordinal);
        foreach (var f in ForceOpaqueNames)
        {
            _knownHandles.Add(f);
        }
    }

    public bool AnyChanges { get; private set; }

    public void Reset() => AnyChanges = false;

    public override SyntaxNode? VisitStructDeclaration(StructDeclarationSyntax node)
    {
        var name = node.Identifier.ValueText;
        if (!_knownHandles.Contains(name))
        {
            return base.VisitStructDeclaration(node);
        }

        AnyChanges = true;
        return BuildPatternBStruct(name, preserveTrivia: node.GetLeadingTrivia());
    }

    /// <summary>
    /// Construct a complete Pattern B typed handle struct declaration.
    /// </summary>
    private static StructDeclarationSyntax BuildPatternBStruct(
        string name, SyntaxTriviaList preserveTrivia)
    {
        var text = $@"[global::System.Runtime.InteropServices.StructLayout(global::System.Runtime.InteropServices.LayoutKind.Sequential)]
public readonly partial struct {name} : global::System.IEquatable<{name}>
{{
    public {name}(nint value) {{ Value = value; }}
    public nint Value {{ get; }}
    public bool IsNull => Value == 0;
    public bool IsNotNull => Value != 0;
    public static {name} Null => default;
    public nint DangerousGetHandle() => Value;
    public bool Equals({name} other) => Value == other.Value;
    public override bool Equals(object? obj) => obj is {name} other && Equals(other);
    public override int GetHashCode() => Value.GetHashCode();
    public static bool operator ==({name} left, {name} right) => left.Equals(right);
    public static bool operator !=({name} left, {name} right) => !left.Equals(right);
    public static explicit operator nint({name} value) => value.Value;
    public static explicit operator {name}(nint value) => new(value);
}}";

        var parsed = SyntaxFactory.ParseCompilationUnit(text);
        var structDecl = (StructDeclarationSyntax)parsed.Members[0];
        return structDecl.WithLeadingTrivia(preserveTrivia);
    }

    /// <summary>
    /// Rewrite pointer references to known handles as by-value.
    /// `SDL_Window*` parameter -> `SDL_Window` (preserves attribute lists).
    /// </summary>
    public override SyntaxNode? VisitParameter(ParameterSyntax node)
    {
        if (node.Type is PointerTypeSyntax ptr &&
            ptr.ElementType is IdentifierNameSyntax id &&
            _knownHandles.Contains(id.Identifier.ValueText))
        {
            AnyChanges = true;
            return node.WithType(id.WithTriviaFrom(ptr));
        }
        return base.VisitParameter(node);
    }

    public override SyntaxNode? VisitMethodDeclaration(MethodDeclarationSyntax node)
    {
        if (node.ReturnType is PointerTypeSyntax ptr &&
            ptr.ElementType is IdentifierNameSyntax id &&
            _knownHandles.Contains(id.Identifier.ValueText))
        {
            AnyChanges = true;
            node = node.WithReturnType(id.WithTriviaFrom(ptr));
        }
        return base.VisitMethodDeclaration(node);
    }
}
```

- [ ] **Step 13.3: Auto-detect helper (separate from the rewriter — pre-scan input)**

Add a static helper method that scans the input directory and returns the auto-detected handle set:

```csharp
// Add to OpaqueHandleEmitRewriter.cs

public static HashSet<string> DiscoverAutoDetectedHandles(string inputDir)
{
    var allFiles = Directory.EnumerateFiles(inputDir, "*.g.cs", SearchOption.AllDirectories);
    var emptyStructs = new HashSet<string>(StringComparer.Ordinal);
    var referencedAsPointer = new HashSet<string>(StringComparer.Ordinal);

    foreach (var file in allFiles)
    {
        var source = File.ReadAllText(file);
        var tree = CSharpSyntaxTree.ParseText(source);
        var root = tree.GetCompilationUnitRoot();

        // Collect empty struct names
        foreach (var sd in root.DescendantNodes().OfType<StructDeclarationSyntax>())
        {
            if (sd.Members.Count == 0 &&
                sd.Identifier.ValueText.StartsWith("SDL_", StringComparison.Ordinal))
            {
                emptyStructs.Add(sd.Identifier.ValueText);
            }
        }

        // Collect [NativeTypeName("X *")] annotations
        foreach (var attr in root.DescendantNodes().OfType<AttributeSyntax>())
        {
            if (attr.Name.ToString() != "NativeTypeName") continue;
            var firstArg = attr.ArgumentList?.Arguments.FirstOrDefault();
            if (firstArg?.Expression is LiteralExpressionSyntax lit)
            {
                var raw = lit.Token.ValueText;
                // Match "X *" or "X*" or "const X *"
                var stripped = raw.Replace("const ", "").Replace("*", "").Trim();
                if (stripped.StartsWith("SDL_", StringComparison.Ordinal) &&
                    raw.Contains('*'))
                {
                    referencedAsPointer.Add(stripped);
                }
            }
        }
    }

    var result = new HashSet<string>(StringComparer.Ordinal);
    foreach (var name in emptyStructs)
    {
        if (referencedAsPointer.Contains(name))
        {
            result.Add(name);
        }
    }
    return result;
}
```

- [ ] **Step 13.4: Wire `uniform-opaque` mode into Program.cs**

Modify `Program.cs` mode validation and switch. For this rewriter, the construction is parameterized by the discovered set, so the wiring differs slightly:

```csharp
case "uniform-opaque":
{
    var discovered = OpaqueHandleEmitRewriter.DiscoverAutoDetectedHandles(inputDir);
    Console.WriteLine($"uniform-opaque: discovered {discovered.Count} auto-detect handles + 3 force-opaque");
    var r = new OpaqueHandleEmitRewriter(discovered);
    rewriter = r;
    hasChanges = () => r.AnyChanges;
    resetRewriter = r.Reset;
    break;
}
```

Also add `"uniform-opaque"` to the args validation tuple.

- [ ] **Step 13.5: Build the postprocess project**

Run: `dotnet build spikes/binding-generators/clangsharp/postprocess/Janset.SDL2.PostProcess.csproj -c Release`

Expected: 0 errors, 0 warnings.

- [ ] **Step 13.6: Commit (scaffolding only — no spike output rewrite yet)**

```bash
git add spikes/binding-generators/clangsharp/postprocess/OpaqueHandleEmitRewriter.cs spikes/binding-generators/clangsharp/postprocess/Program.cs
git commit -m "$(cat <<'EOF'
feat(binding-spike): OpaqueHandleEmitRewriter scaffolding (Slice C-B Task 13)

New rewriter for uniform Pattern B opaque handle emit. Auto-detect channel
scans empty `partial struct SDL_X { }` declarations referenced via
[NativeTypeName("X *")] elsewhere; force-opaque channel applies to the
Constitution-bound allow-list (SDL_RWops, SDL_SysWMinfo, SDL_SysWMmsg).

Pattern B shape per design Decision 1:
  readonly partial struct X(nint value) : IEquatable<X>
  with explicit StructLayout(Sequential), get-only Value property,
  IsNull/IsNotNull/Null sentinels, DangerousGetHandle, full equality,
  and explicit operator nint only (no implicit per research Finding 3).

Reference rewrite: X* in raw signatures -> X by-value at single-pointer
positions; double-pointer and `out` positions preserved.

This commit adds scaffolding only. Task 14 applies it.

Refs: docs/superpowers/specs/2026-05-24-clangsharp-priority-c-semantic-abi-design.md
Slice C-B, Constitution §"Opaque Handles" Public typed handle struct shape.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

### Task 14: Apply Pattern B to Auto-Detected Handles

- [ ] **Step 14.0: Revise rewriter to syntactic auto-detect + roster JSON load**

Before running the rewriter on output, revise `OpaqueHandleEmitRewriter` to match the policy realization documented in Constitution §"Opaque Handles" → Implementation mechanism and Spec Decision 1 → Postprocess realization (both updated in the doc-only commit that landed the roster JSON):

- **`DiscoverAutoDetectedHandles(inputDir)` becomes syntactic.** Walk every `.g.cs` file under `inputDir` for one TFM view; collect (a) the set of names `N` declared as empty `public partial struct SDL_X { }` and (b) the set of names `P` used as pointer type `SDL_X*` at any raw ABI signature position (method parameter type or return type). Auto-detect roster = `N ∩ P`. The earlier `[NativeTypeName("X *")]` cross-reference is **dropped** — ClangSharp omits `NativeTypeName` when the C and C# names match (the common case for opaque handles), so the annotation heuristic under-detected.
- **Load the canonical roster.** Read `spikes/binding-generators/clangsharp/policy/opaque-handle-roster.json` at startup. Resolve the path from `inputDir` upward to the repo root (convention: `<repo>/spikes/binding-generators/clangsharp/policy/opaque-handle-roster.json`), or accept an explicit `--roster <path>` argument from `Program.cs`. Parse the `auto_detect_well_known` and `force_opaque_exceptions` arrays into `HashSet<string>` instances.
- **Drift handling (warning, not failure).** Compute the symmetric difference between the syntactic discovery set and the roster's `auto_detect_well_known` set. On mismatch, emit a single stderr line of the form `uniform-opaque: drift! In code but not roster: [<names>]; In roster but not code: [<names>]` and continue with the union (or the syntactic set, whichever is safer for Pattern B emit — document the choice in source). Do NOT fail the run. SDL2 upstream additions thereby surface visibly without blocking regeneration.
- **Force-opaque list is roster-driven.** Remove the hard-coded `ForceOpaqueNames` `HashSet<string>` literal from `OpaqueHandleEmitRewriter.cs`. The force-opaque allow-list is loaded from the roster JSON's `force_opaque_exceptions` array. Constitution §"Opaque Handles" prose remains authoritative for *why* each force-opaque type is listed; the JSON carries the *what*.
- **Program.cs wiring.** The `uniform-opaque` mode in `postprocess/Program.cs` resolves the roster JSON path (default: convention path from `inputDir`) and passes both the path and the input directory into the rewriter. No new CLI surface required for the default case; an optional `--roster <path>` argument may be added for testing if needed.

After this step the rewriter is policy-aligned. Subsequent steps run it against the spike output.

- [ ] **Step 14.1: One-shot run on Modern output**

Run: `dotnet run --project spikes/binding-generators/clangsharp/postprocess/Janset.SDL2.PostProcess.csproj -c Release -- uniform-opaque spikes/binding-generators/clangsharp/src/Janset.SDL2.Core/Generated/Modern`

Expected: stdout reports `uniform-opaque: discovered 15 syntactic handles + 3 force-opaque (roster cross-check: 0 drift entries)`. (The 15 number matches the roster's `auto_detect_well_known` length; the 3 number matches `force_opaque_exceptions`. Drift count 0 in the nominal case — any nonzero count surfaces as a `drift!` stderr line and requires investigation before proceeding.) Multiple files transformed.

- [ ] **Step 14.2: Verify Pattern B emit across multiple roster handles**

Run: `head -30 spikes/binding-generators/clangsharp/src/Janset.SDL2.Core/Generated/Modern/SDL_video.g.cs`

Expected: see `public readonly partial struct SDL_Window : IEquatable<SDL_Window>` with the full Pattern B shape, replacing the old empty struct.

Then verify at least three more roster handles emit Pattern B (spread across distinct headers to confirm uniform application):

```bash
for handle in SDL_Renderer SDL_AudioStream SDL_GameController SDL_mutex; do
    grep -rn "public readonly partial struct $handle\b" spikes/binding-generators/clangsharp/src/Janset.SDL2.Core/Generated/Modern/ || echo "MISSING: $handle"
done
```

Expected: each handle declared exactly once with the Pattern B `: IEquatable<X>` signature. No `MISSING` lines.

- [ ] **Step 14.3: Verify reference rewrites in signatures**

Run: `grep -n "SDL_Window\*\|SDL_Window " spikes/binding-generators/clangsharp/src/Janset.SDL2.Core/Generated/Modern/SDL_video.g.cs | head -10`

Expected: no `SDL_Window*` parameters/returns remain (they're now `SDL_Window` by-value); double-pointer cases `SDL_Window**` preserved.

Spot-check another roster handle's reference rewrite to confirm uniformity:

Run: `grep -n "SDL_Renderer\*\|SDL_Renderer " spikes/binding-generators/clangsharp/src/Janset.SDL2.Core/Generated/Modern/SDL_render.g.cs | head -10`

Expected: same pattern — single-pointer references rewritten to by-value; double-pointer preserved.

- [ ] **Step 14.4: Compile-check Modern TFMs**

Run: `dotnet build spikes/binding-generators/clangsharp/src/Janset.SDL2.Core/Janset.SDL2.Core.csproj -c Release --framework net10.0`

Expected: 0 errors. Some warnings about `using System.Runtime.InteropServices;` may appear if not already present — add via rewriter or post-step.

- [ ] **Step 14.5: One-shot run on Compat output**

Run: `dotnet run --project spikes/binding-generators/clangsharp/postprocess/Janset.SDL2.PostProcess.csproj -c Release -- uniform-opaque spikes/binding-generators/clangsharp/src/Janset.SDL2.Core/Generated/Compat`

- [ ] **Step 14.6: Compile-check legacy TFMs (critical Pattern B verification)**

Run: `dotnet build spikes/binding-generators/clangsharp/src/Janset.SDL2.Core/Janset.SDL2.Core.csproj -c Release --framework netstandard2.0`

Then: `dotnet build spikes/binding-generators/clangsharp/src/Janset.SDL2.Core/Janset.SDL2.Core.csproj -c Release --framework net462`

Expected: 0 errors. This is the multi-TFM Pattern B verdict gate — if legacy TFMs compile with `readonly partial struct X(nint value)`, Pattern B works.

If errors:
- `nint` keyword not recognized on legacy: bump `<LangVersion>12</LangVersion>` in the spike's csproj (should already be set; verify).
- Primary-constructor warnings: the explicit `public X(nint value) { Value = value; }` form should be safe.

- [ ] **Step 14.7: Commit**

```bash
git add spikes/binding-generators/clangsharp/postprocess/ spikes/binding-generators/clangsharp/src/
git commit -m "$(cat <<'EOF'
feat(binding-spike): apply Pattern B uniform opaque handle emit (Task 14)

OpaqueHandleEmitRewriter revised to syntactic auto-detect (empty
public partial struct + SDL_X* pointer-use intersection over raw ABI
signatures) and roster-driven force-opaque allow-list. Canonical roster
loaded from spikes/binding-generators/clangsharp/policy/
opaque-handle-roster.json (sdl2 2.32.10); roster cross-check emits a
warning on drift but does not fail the run.

Rewriter applied to auto-detected handles (15 names per roster:
SDL_Window, SDL_Renderer, SDL_Texture, SDL_AudioStream,
SDL_GameController, SDL_Joystick, SDL_Haptic, SDL_Sensor, SDL_Cursor,
SDL_Thread, SDL_mutex, SDL_sem, SDL_cond, SDL_hid_device, SDL_BlitMap)
plus force-opaque allow-list (SDL_RWops, SDL_SysWMinfo, SDL_SysWMmsg).
Raw signature references rewritten to by-value at every single-pointer
occurrence.

Critical multi-TFM gate satisfied: netstandard2.0 + net462 + net8/9/10
all compile with `readonly partial struct X(nint value)` shape. Pattern B
multi-TFM verdict verified.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

### Task 15: Wire OpaqueHandleEmitRewriter Into Orchestrator Pipeline

- [ ] **Step 15.1: Add postprocess step to `generate_bindings.py`**

Following Task 5 Step 5.8 / Task 10 Step 10.8 pattern, invoke `uniform-opaque` after the other postprocess steps for both Compat and Modern output directories.

```python
# After existing postprocess steps:
run_postprocess(repo, compat_output_dir, "uniform-opaque")
run_postprocess(repo, modern_output_dir, "uniform-opaque")
```

- [ ] **Step 15.2: Verify full regeneration round-trip**

Run: `python spikes/binding-generators/clangsharp/generate_bindings.py --scope full --codegen both --execute --clean-output --vcpkg-triplet x64-windows-hybrid --use-platform-header-shims`

Expected: regeneration succeeds; uniform-opaque pipeline step runs; final output has typed handle structs in place of empty structs and force-opaque structs.

- [ ] **Step 15.3: Verify a force-opaque struct (was full-layout, now Pattern B)**

Run: `head -40 spikes/binding-generators/clangsharp/src/Janset.SDL2.Core/Generated/Modern/SDL_rwops.g.cs`

Expected: see `public readonly partial struct SDL_RWops : IEquatable<SDL_RWops>` Pattern B shape — the old `hidden` union and function-pointer fields are gone.

Run: `grep -n "_hidden_e__Union\|delegate\*" spikes/binding-generators/clangsharp/src/Janset.SDL2.Core/Generated/Modern/SDL_rwops.g.cs`

Expected: 0 matches (the platform-conditioned union and function-pointer slots are removed).

- [ ] **Step 15.4: Verify reference rewrites in satellite (SDL2.Image)**

Run: `grep -n "SDL_RWops" spikes/binding-generators/clangsharp/src/Janset.SDL2.Image/Generated/Modern/SDL_image.g.cs | head -10`

Expected: signatures use `SDL_RWops` by-value (e.g., `IMG_Load_RW(SDL_RWops src, ...)`); no `SDL_RWops*` pointer parameters remain.

- [ ] **Step 15.5: Compile-check both projects across all TFMs**

Run:
```bash
dotnet build spikes/binding-generators/clangsharp/src/Janset.SDL2.Core/Janset.SDL2.Core.csproj -c Release
dotnet build spikes/binding-generators/clangsharp/src/Janset.SDL2.Image/Janset.SDL2.Image.csproj -c Release
dotnet build tests/smoke-tests/abi-tests/AbiTests.csproj -c Release
```

Expected: 0 errors, 0 warnings across all 5 TFMs in all three projects.

- [ ] **Step 15.6: Commit**

```bash
git add spikes/binding-generators/clangsharp/generate_bindings.py spikes/binding-generators/clangsharp/src/
git commit -m "$(cat <<'EOF'
feat(binding-spike): wire OpaqueHandleEmitRewriter into orchestrator pipeline

uniform-opaque postprocess step runs after libraryimport/platform-delta/
guid-substitute/threadid-dispatch on both Compat and Modern output trees.

Round-trip verified: full regeneration produces Pattern B typed handle
structs for all ~15 expected names (12 auto-detected + 3 force-opaque).
Force-opaque structs (SDL_RWops, SDL_SysWMinfo, SDL_SysWMmsg) lose their
platform-conditioned unions and function-pointer fields entirely;
references rewrite to by-value across both Core and Image satellites.

Multi-TFM compile clean across 5 TFMs in Janset.SDL2.Core,
Janset.SDL2.Image, and tests/smoke-tests/abi-tests.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

### Task 16: Slice C-B Exit Gate

- [ ] **Step 16.1: Full regenerate (verify deterministic)**

Run: `python spikes/binding-generators/clangsharp/generate_bindings.py --scope full --codegen both --execute --clean-output --vcpkg-triplet x64-windows-hybrid --use-platform-header-shims`

Run again immediately:

`python spikes/binding-generators/clangsharp/generate_bindings.py --scope full --codegen both --execute --clean-output --vcpkg-triplet x64-windows-hybrid --use-platform-header-shims`

Run: `git status spikes/binding-generators/clangsharp/src/ --short`

Expected: no diff between the two regenerations (deterministic emit).

- [ ] **Step 16.2: Oracle gate check**

Run: `dotnet run --file spikes/binding-generators/clangsharp/oracle.cs -- --family sdl2-core --family sdl2-image --write-report`

Verify in the report:
- `deferred-layout-sdl-rwops`: **0 findings**.
- `deferred-layout-sdl-syswminfo`: **0 findings**.
- `deferred-layout-sdl-syswmmsg`: **0 findings**.

- [ ] **Step 16.3: Typed handle inventory check**

Run:
```bash
expected_handles=("SDL_Window" "SDL_Renderer" "SDL_Texture" "SDL_AudioStream" "SDL_Cursor" "SDL_Joystick" "SDL_GameController" "SDL_hid_device" "SDL_mutex" "SDL_cond" "SDL_sem" "SDL_Thread" "SDL_RWops" "SDL_SysWMinfo" "SDL_SysWMmsg")
for h in "${expected_handles[@]}"; do
    count=$(grep -rn "public readonly partial struct $h\b" spikes/binding-generators/clangsharp/src/Janset.SDL2.Core/Generated/Modern/ | wc -l)
    if [ "$count" -ne 1 ]; then
        echo "MISSING or DUPLICATE: $h (count=$count)"
    fi
done
echo "Inventory check done."
```

Expected: every expected handle has exactly one declaration. No "MISSING" or "DUPLICATE" lines printed.

- [ ] **Step 16.4: Multi-TFM compile gate (final critical)**

Run:
```bash
dotnet build spikes/binding-generators/clangsharp/src/Janset.SDL2.Core/Janset.SDL2.Core.csproj -c Release
dotnet build spikes/binding-generators/clangsharp/src/Janset.SDL2.Image/Janset.SDL2.Image.csproj -c Release
dotnet build tests/smoke-tests/abi-tests/AbiTests.csproj -c Release
```

Expected: 0 errors, 0 warnings across all 5 TFMs in all three projects.

- [ ] **Step 16.5: Slopwatch check**

Run: `slopwatch analyze --fail-on warning --exclude "artifacts/**,external/**,vcpkg_installed/**,spikes/binding-generators/references/**,**/bin/**,**/obj/**"`

Expected: 0 issues.

- [ ] **Step 16.6: Commit (only if regeneration produced changes not already committed)**

```bash
git add spikes/binding-generators/clangsharp/src/ spikes/binding-generators/output/
git commit -m "feat(binding-spike): Slice C-B exit gate — uniform Pattern B opaque handles closed

Oracle deferred-layout-sdl-rwops/syswminfo/syswmmsg: 0 findings each.
Typed handle inventory: all 15 expected names emitted as Pattern B.
Reference rewrite: single-pointer X* -> X by-value everywhere; double-
pointer and `out` positions preserved.
Multi-TFM compile clean across 5 TFMs (Core + Image + AbiTests).
Slopwatch clean.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

---

## Phase 4 — Priority C Overall Closure

### Task 17: Final Regeneration + Constitution Evidence Gate Verification

- [ ] **Step 17.1: Full regeneration from clean state**

Run: `python spikes/binding-generators/clangsharp/generate_bindings.py --scope full --codegen both --execute --clean-output --vcpkg-triplet x64-windows-hybrid --use-platform-header-shims`

- [ ] **Step 17.2: Oracle clean — zero Priority C findings**

Run: `dotnet run --file spikes/binding-generators/clangsharp/oracle.cs -- --family sdl2-core --family sdl2-image --write-report`

Open `spikes/binding-generators/output/reports/oracle-evidence-clangsharp.md` and verify the "Raw ABI Constitution Checks" section shows:
- Compatibility Risk → `platform-sensitive-wchar`: 0 findings.
- Compatibility Risk → `platform-sensitive-long`: 0 findings.
- Compatibility Risk → `duplicate-tag-typedef`: 0 findings.
- Hard Bug → `deferred-layout-sdl-rwops`: 0 findings.
- Hard Bug → `deferred-layout-sdl-syswminfo`: 0 findings.
- Hard Bug → `deferred-layout-sdl-syswmmsg`: 0 findings.

- [ ] **Step 17.3: Constitution evidence gate (L383-394) checklist**

Manually verify (or by automated checks where possible):
- [ ] Generated preview compiles across `net10.0`, `net9.0`, `net8.0`, `netstandard2.0`, `net462` — covered by Step 17.4.
- [ ] No public raw ABI class or effectively public raw extern leak — covered by earlier A-slice (commit `92b893b`); re-verify by grepping for `public.*\bSDLNative\b` in generated output: expect only `internal` containers.
- [ ] Function-name set matches dynapi/export evidence after accepted exclusions — already verified by existing oracle dynapi coherence (~98%).
- [ ] High-risk type translations have fixture coverage: C `long`, `wchar_t`, SDL2 `SDL_bool`, `size_t`, callbacks — partially via the new ABI tests project (Task 11); broader coverage is a follow-up.
- [ ] Public struct layouts have size/offset proof where layout is platform-conditioned — N/A (force-opaque structs disclose no layout).
- [ ] Macro report explains emitted, skipped, unsupported, helper-candidate entries — pre-existing.
- [ ] Oracle validation records any accepted deltas and remaining blockers — covered by Step 17.2.

- [ ] **Step 17.4: Full multi-TFM compile**

Run:
```bash
dotnet build spikes/binding-generators/clangsharp/src/Janset.SDL2.Core/Janset.SDL2.Core.csproj -c Release
dotnet build spikes/binding-generators/clangsharp/src/Janset.SDL2.Image/Janset.SDL2.Image.csproj -c Release
dotnet build tests/smoke-tests/abi-tests/AbiTests.csproj -c Release
```

Expected: 0 errors, 0 warnings across all 5 TFMs in all three projects.

- [ ] **Step 17.5: Slopwatch check**

Run: `slopwatch analyze --fail-on warning --exclude "artifacts/**,external/**,vcpkg_installed/**,spikes/binding-generators/references/**,**/bin/**,**/obj/**"`

Expected: 0 issues.

- [ ] **Step 17.6: Commit (if any final regeneration changes)**

```bash
git add spikes/binding-generators/clangsharp/src/ spikes/binding-generators/output/
git commit -m "feat(binding-spike): Priority C overall exit — ABI-compatible Layer 1

All six Priority C semantic-ABI risks closed:
  R1 wchar_t* opaque (Slice C-A Task 8)
  R2 C long hybrid (Slice C-A Tasks 9, 10)
  R3 SDL_RWops force-opaque typed handle (Slice C-B Task 15)
  R4 SDL_SysWMinfo force-opaque typed handle (Slice C-B Task 15)
  R5 SDL_SysWMmsg force-opaque typed handle (Slice C-B Task 15)
  R6 tag/typedef canonicalization (Slice C-C Tasks 2, 3, 6)
  + SDL_GUID -> System.Guid substitution (Slice C-C Task 5)

Oracle Priority C category findings: 0 across all six.
Multi-TFM compile clean across 5 TFMs.
Typed handle inventory: 15 expected names emit as Pattern B.

Spike state: ABI-compatible Layer 1. Layer 2 design (public typed
low-level methods on SDL2.SDL calling internal raw SDLNative) is the
next slice; typed handle structs already in place for reuse.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

---

### Task 18: Update Spike Handoff With Priority C Closure

**Files:**
- Modify: `spikes/binding-generators/docs/llm-handoff.md`

- [ ] **Step 18.1: Read the current Priority C section**

Run: `grep -n "Priority C" spikes/binding-generators/docs/llm-handoff.md | head -10`

Read the section around the "Oracle report repair queue status" table (~line 280).

- [ ] **Step 18.2: Update the Priority C status**

Edit the table to mark Priority C as **Done**, citing the relevant commit hash (from Task 17 Step 17.6 or whichever was the final closing commit):

```markdown
| C | Deferred layouts and platform-sensitive scalar mappings (SDL_RWops, SDL_SysWMinfo, SDL_SysWMmsg, wchar_t, C `long`, tag/typedef canonicalization, SDL_GUID) | **Done** (commit `<final-commit-hash>`). Three slices C-C → C-A → C-B closed; spike state advanced to "ABI-compatible Layer 1". Layer 2 typed public method projection is the next slice. |
```

Also update the spike state line at the top of llm-handoff.md (if present) from "near-ABI-compatible Layer 1" to "ABI-compatible Layer 1".

- [ ] **Step 18.3: Update the next-iteration-plan if present**

Run: `grep -n "C-slice\|Priority C" spikes/binding-generators/docs/next-iteration-plan.md`

If the file references the design spec, update the status from "design landed" to "implementation landed". If the slice plan referenced future work, mark the relevant items complete.

- [ ] **Step 18.4: Commit**

```bash
git add spikes/binding-generators/docs/llm-handoff.md spikes/binding-generators/docs/next-iteration-plan.md
git commit -m "$(cat <<'EOF'
docs(binding-spike): mark Priority C closed in spike handoff

All three Slice C phases (C-C → C-A → C-B) shipped; spike state advances
from "near-ABI-compatible Layer 1" to "ABI-compatible Layer 1". Oracle
Priority C category findings: 0. Layer 2 public typed method projection
is the next slice; typed handle structs already in place for reuse.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

### Task 19: Push to Remote (Optional)

- [ ] **Step 19.1: Verify branch state**

Run: `git status && git log --oneline -10`

Expected: working tree clean; recent commits include all Priority C implementation work.

- [ ] **Step 19.2: Push (only after user explicit approval)**

Per AGENTS.md "Approval Gate", do NOT push to remote without user confirmation. Surface the commit list and ask:

> "Priority C implementation complete locally. N new commits ready to push to `origin/spike/binding-autogen-sdl2-gfx`. Want me to push, or stage them for review first?"

If approved:

```bash
git push origin HEAD
```

If not, surface to user and stop.

---

## Out-of-Scope Follow-ups (Track for Later)

These are surfaced in the design spec; do not implement here:

1. **Alimer-style CppAst comparison evidence** — required before ADR-004 amendment.
2. **Layer 2 public method projection** — `SDL2.SDL` / `SDL_image` public class methods calling `SDLNative` internal raw. Next slice after Priority C.
3. **Layer 3 friendly overloads** — `string`/`Span<T>`/`out T`/platform-aware `wchar_t` decoder.
4. **`oracle.cs` dynapi unique-vs-occurrence reporting** — research Finding 5; reporting polish.
5. **`implicit operator nint` final API review** — pre-public-API-snapshot decision.
6. **Per-RID native ABI smoke test infrastructure expansion** beyond SDL_threadID — broader CLong-using symbol coverage as a hardening item.
7. **ClangSharp `--with-attribute` migration** for `[SupportedOSPlatform]` — replace `PlatformDeltaPostProcessor` with RSP-level attribution.
