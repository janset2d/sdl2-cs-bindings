# Item 4: SDL2_ttf Layer 1 Raw ABI Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Activate SDL2_ttf as the fourth family in the ClangSharp + Roslyn postprocess binding generator with Layer 1 raw ABI output.

**Architecture:** Use the existing family-keyed pipeline. Activate one header in `family-config.json`, add a TTF family RSP, create the TTF spike project from the GFX/Image pattern, generate output, then verify ABI shape, oracle evidence, regressions, and peer visual checks.

**Tech Stack:** Python 3 orchestrator, ClangSharp, C# 14 / .NET 10 Roslyn postprocess, multi-TFM spike class libraries.

---

## File Map

| File | Action | Responsibility |
|------|--------|----------------|
| `spikes/binding-generators/clangsharp/config/family-config.json` | Modify | Populate `families.ttf.headers[]` with `SDL_ttf.h` |
| `spikes/binding-generators/clangsharp/rsp/sdl2-ttf.rsp` | Create | Exclude TTF error macros and deprecated functions |
| `spikes/binding-generators/clangsharp/src/Janset.SDL2.Ttf/Janset.SDL2.Ttf.csproj` | Create | Multi-TFM TTF spike project referencing Core |
| `spikes/binding-generators/clangsharp/src/Janset.SDL2.Ttf/Support/DisableRuntimeMarshalling.cs` | Create | Cross-assembly Pattern B contract |
| `spikes/binding-generators/clangsharp/src/Janset.SDL2.Ttf/Support/NativeTypeNameAttribute.cs` | Create | Per-assembly ClangSharp native type attribute |
| `spikes/binding-generators/clangsharp/Janset.SDL2.ClangSharpSpike.slnx` | Modify | Add TTF project |
| `spikes/binding-generators/clangsharp/generate_bindings.py` | Modify | Add TTF to aggregate `all` generation and self-test |
| `spikes/binding-generators/clangsharp/oracle.cs` | Verify/minimal modify only if needed | Confirm TTF paths produce real evidence row |
| `spikes/binding-generators/docs/items/item-4-sdl2-ttf-layer-1-spec.md` | Modify only if implementation evidence changes design | Keep spec aligned with discovered facts |

---

### Task 1: Activate TTF Header In Config

**Files:**

- Modify: `spikes/binding-generators/clangsharp/config/family-config.json`

- [ ] **Step 1: Replace dormant TTF header list**

Find the `"ttf"` family block and replace:

```json
      "headers": [],
```

with:

```json
      "headers": [
        { "order": 10, "name": "SDL_ttf.h" }
      ],
```

- [ ] **Step 2: Validate JSON parses**

Run:

```pwsh
python -c "import json; json.load(open('spikes/binding-generators/clangsharp/config/family-config.json', encoding='utf-8'))"
```

Expected: exit 0, no output.

---

### Task 2: Add TTF Family RSP

**Files:**

- Create: `spikes/binding-generators/clangsharp/rsp/sdl2-ttf.rsp`

- [ ] **Step 1: Create RSP file**

Create the file with exactly this content:

```rsp
# TTF_SetError / TTF_GetError are macro shortcuts that point at SDL_SetError
# / SDL_GetError in the core class. ClangSharp can't cross the family
# methodClassName boundary, so emit them as part of the SDL2_ttf friendly
# wrapper layer later rather than as raw bindings.
#
# The three deprecated functions are excluded from Layer 1 rather than emitted
# as raw imports. TTF_GetFontKerningSize also lacks SDLCALL, while
# TTF_SetDirection / TTF_SetScript are superseded by per-font APIs.
--exclude
TTF_SetError
TTF_GetError
TTF_GetFontKerningSize
TTF_SetDirection
TTF_SetScript
```

- [ ] **Step 2: Confirm no per-header RSP exists**

Run:

```pwsh
Test-Path -LiteralPath "spikes/binding-generators/clangsharp/rsp/per-header/SDL_ttf.rsp"
```

Expected: `False`. If generated callconv verification later fails, add the per-header RSP as a corrective task instead of preemptively creating it.

---

### Task 3: Create TTF Project And Support Files

**Files:**

- Create: `spikes/binding-generators/clangsharp/src/Janset.SDL2.Ttf/Janset.SDL2.Ttf.csproj`
- Create: `spikes/binding-generators/clangsharp/src/Janset.SDL2.Ttf/Support/DisableRuntimeMarshalling.cs`
- Create: `spikes/binding-generators/clangsharp/src/Janset.SDL2.Ttf/Support/NativeTypeNameAttribute.cs`

- [ ] **Step 1: Create directories**

Run:

```pwsh
Test-Path -LiteralPath "spikes/binding-generators/clangsharp/src"; New-Item -ItemType Directory -Path "spikes/binding-generators/clangsharp/src/Janset.SDL2.Ttf/Support" -Force; New-Item -ItemType Directory -Path "spikes/binding-generators/clangsharp/src/Janset.SDL2.Ttf/Generated/Compat" -Force; New-Item -ItemType Directory -Path "spikes/binding-generators/clangsharp/src/Janset.SDL2.Ttf/Generated/Modern" -Force
```

Expected: first command prints `True`; directories exist.

- [ ] **Step 2: Create csproj**

Create `Janset.SDL2.Ttf.csproj` with exactly:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFrameworks>net10.0;net9.0;net8.0;netstandard2.0;net462</TargetFrameworks>
    <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
    <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
    <LangVersion>preview</LangVersion>
    <Nullable>enable</Nullable>
    <RootNamespace>SDL2</RootNamespace>
  </PropertyGroup>

  <!-- Core-owned SDL types (SDL_Surface*, SDL_RWops*, SDL_Color, etc.) come
       from Janset.SDL2.Core so satellite output does not redeclare them. -->
  <ItemGroup>
    <ProjectReference Include="../Janset.SDL2.Core/Janset.SDL2.Core.csproj" />
  </ItemGroup>

  <!-- TFM-split generated source (matches Janset.SDL2.Core layout). -->
  <ItemGroup Condition="'$(TargetFramework)' == 'netstandard2.0' Or '$(TargetFramework)' == 'net462'">
    <Compile Include="Generated/Compat/**/*.cs" />
    <Compile Include="Support/**/*.cs" />
  </ItemGroup>
  <ItemGroup Condition="'$(TargetFramework)' != 'netstandard2.0' And '$(TargetFramework)' != 'net462'">
    <Compile Include="Generated/Modern/**/*.cs" />
    <Compile Include="Support/**/*.cs" />
  </ItemGroup>

  <!-- System.Memory polyfill for ReadOnlySpan<T> on legacy TFMs (Constitution L46). -->
  <ItemGroup Condition="'$(TargetFramework)' == 'netstandard2.0' Or '$(TargetFramework)' == 'net462'">
    <PackageReference Include="System.Memory" />
  </ItemGroup>

</Project>
```

- [ ] **Step 3: Create DisableRuntimeMarshalling support file**

Create `Support/DisableRuntimeMarshalling.cs` with exactly:

```csharp
#if NET7_0_OR_GREATER
[assembly: System.Runtime.CompilerServices.DisableRuntimeMarshalling]
#endif
```

- [ ] **Step 4: Create NativeTypeNameAttribute support file**

Create `Support/NativeTypeNameAttribute.cs` with exactly:

```csharp
// <auto-generated/>

using System;
using System.Diagnostics;

namespace SDL2.Ttf;

/// <summary>Defines the type of a member as it was used in the native signature.</summary>
[AttributeUsage(AttributeTargets.Struct | AttributeTargets.Enum | AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter | AttributeTargets.ReturnValue, AllowMultiple = false, Inherited = true)]
[Conditional("DEBUG")]
internal sealed partial class NativeTypeNameAttribute(string name) : Attribute
{
    /// <summary>Gets the name of the type that was used in the native signature.</summary>
    public string Name { get; } = name;
}
```

---

### Task 4: Add TTF To Spike Solution

**Files:**

- Modify: `spikes/binding-generators/clangsharp/Janset.SDL2.ClangSharpSpike.slnx`

- [ ] **Step 1: Add project entry**

Change the `/src/` folder from:

```xml
  <Folder Name="/src/">
    <Project Path="src/Janset.SDL2.Core/Janset.SDL2.Core.csproj" />
    <Project Path="src/Janset.SDL2.Image/Janset.SDL2.Image.csproj" />
    <Project Path="src/Janset.SDL2.Gfx/Janset.SDL2.Gfx.csproj" />
  </Folder>
```

to:

```xml
  <Folder Name="/src/">
    <Project Path="src/Janset.SDL2.Core/Janset.SDL2.Core.csproj" />
    <Project Path="src/Janset.SDL2.Image/Janset.SDL2.Image.csproj" />
    <Project Path="src/Janset.SDL2.Gfx/Janset.SDL2.Gfx.csproj" />
    <Project Path="src/Janset.SDL2.Ttf/Janset.SDL2.Ttf.csproj" />
  </Folder>
```

---

### Task 5: Generate TTF Explicitly

**Files:**

- Generated: `spikes/binding-generators/clangsharp/src/Janset.SDL2.Ttf/Generated/**`

- [ ] **Step 1: Run explicit TTF generation**

Run:

```pwsh
python spikes/binding-generators/clangsharp/generate_bindings.py --family ttf --execute --vcpkg-triplet x64-windows-hybrid --use-platform-header-shims
```

Expected: exit 0. Output includes TTF generation and postprocess stages.

- [ ] **Step 2: Verify generated files exist**

Run:

```pwsh
Test-Path -LiteralPath "spikes/binding-generators/clangsharp/src/Janset.SDL2.Ttf/Generated/Compat/SDL_ttf.g.cs"; Test-Path -LiteralPath "spikes/binding-generators/clangsharp/src/Janset.SDL2.Ttf/Generated/Modern/SDL_ttf.g.cs"; Test-Path -LiteralPath "spikes/binding-generators/clangsharp/src/Janset.SDL2.Ttf/Generated/Compat/Handles.g.cs"; Test-Path -LiteralPath "spikes/binding-generators/clangsharp/src/Janset.SDL2.Ttf/Generated/Modern/Handles.g.cs"
```

Expected: four `True` values.

---

### Task 6: Verify Generated ABI Shape Before Aggregate Activation

**Files:**

- Inspect: `spikes/binding-generators/clangsharp/src/Janset.SDL2.Ttf/Generated/Compat/SDL_ttf.g.cs`
- Inspect: `spikes/binding-generators/clangsharp/src/Janset.SDL2.Ttf/Generated/Modern/SDL_ttf.g.cs`
- Inspect: `spikes/binding-generators/clangsharp/src/Janset.SDL2.Ttf/Generated/{Compat,Modern}/Handles.g.cs`

- [ ] **Step 1: Check required symbols are present/absent**

Run:

```pwsh
rg "TTF_GetFontKerningSizeGlyphs|TTF_GetFontKerningSizeGlyphs32|TTF_SetFontSDF|TTF_GetFontSDF" spikes/binding-generators/clangsharp/src/Janset.SDL2.Ttf/Generated
rg "TTF_SetError|TTF_GetError|TTF_GetFontKerningSize\(|TTF_SetDirection|TTF_SetScript" spikes/binding-generators/clangsharp/src/Janset.SDL2.Ttf/Generated
```

Expected: first command finds the four non-deprecated no-SDLCALL functions; second command finds no raw generated declarations for excluded symbols.

- [ ] **Step 2: Check no-SDLCALL Cdecl output**

Run:

```pwsh
rg "TTF_GetFontKerningSizeGlyphs|TTF_GetFontKerningSizeGlyphs32|TTF_SetFontSDF|TTF_GetFontSDF|CallingConvention\.Cdecl|CallConvCdecl" spikes/binding-generators/clangsharp/src/Janset.SDL2.Ttf/Generated
```

Expected: Compat declarations are near `CallingConvention.Cdecl`; Modern declarations are near `CallConvCdecl`. If not, stop and add a corrective per-header RSP with `--with-callconv` for the affected symbols.

- [ ] **Step 3: Check C long output**

Run:

```pwsh
rg "TTF_OpenFontIndex|TTF_OpenFontIndexRW|TTF_OpenFontIndexDPI|TTF_OpenFontIndexDPIRW|TTF_FontFaces|CLong|_Win32|_Unix64" spikes/binding-generators/clangsharp/src/Janset.SDL2.Ttf/Generated
```

Expected: Modern output uses `CLong`; Compat output contains dispatcher/helper shapes with `_Win32` and `_Unix64` for affected methods.

- [ ] **Step 4: Check owner-mode handle output**

Run:

```pwsh
rg "namespace SDL2\.Ttf|public readonly partial struct TTF_Font" spikes/binding-generators/clangsharp/src/Janset.SDL2.Ttf/Generated/*/Handles.g.cs
```

Expected: both Compat and Modern handles files contain namespace `SDL2.Ttf` and `TTF_Font` Pattern B declarations.

---

### Task 7: Build TTF Before Aggregate Activation

**Files:**

- Build: `spikes/binding-generators/clangsharp/src/Janset.SDL2.Ttf/Janset.SDL2.Ttf.csproj`

- [ ] **Step 1: Build TTF project**

Run:

```pwsh
dotnet build spikes/binding-generators/clangsharp/src/Janset.SDL2.Ttf/Janset.SDL2.Ttf.csproj -c Release
```

Expected: 0 warnings, 0 errors across all five TFMs.

---

### Task 8: Activate TTF In Aggregate Generation

**Files:**

- Modify: `spikes/binding-generators/clangsharp/generate_bindings.py`

- [ ] **Step 1: Update selected families**

Change:

```python
    if family == "all":
        return ["core", "image", "gfx"]
```

to:

```python
    if family == "all":
        return ["core", "image", "gfx", "ttf"]
```

- [ ] **Step 2: Update self-test expectation**

Find the self-test assertion that expects `selected_families("all")` to equal `['core', 'image', 'gfx']` and update it to `['core', 'image', 'gfx', 'ttf']`. Keep Mixer dormant.

---

### Task 9: Run Aggregate Generation And Regression Diff

**Files:**

- Generated: Core/Image/GFX/TTF `Generated/**`

- [ ] **Step 1: Regenerate all active families**

Run:

```pwsh
python spikes/binding-generators/clangsharp/generate_bindings.py --family all --execute --vcpkg-triplet x64-windows-hybrid --use-platform-header-shims
```

Expected: exit 0. Active families are Core, Image, GFX, and TTF only.

- [ ] **Step 2: Verify existing family determinism**

Run:

```pwsh
git diff --ignore-cr-at-eol -- spikes/binding-generators/clangsharp/src/Janset.SDL2.Core/Generated/ spikes/binding-generators/clangsharp/src/Janset.SDL2.Image/Generated/ spikes/binding-generators/clangsharp/src/Janset.SDL2.Gfx/Generated/
```

Expected: empty diff for Core/Image/GFX generated output.

---

### Task 10: Build And Oracle Evidence

**Files:**

- Verify: `spikes/binding-generators/clangsharp/oracle.cs`

- [ ] **Step 1: Build full spike solution**

Run:

```pwsh
dotnet build spikes/binding-generators/clangsharp/Janset.SDL2.ClangSharpSpike.slnx -c Release
```

Expected: 0 warnings, 0 errors.

- [ ] **Step 2: Run five-family oracle**

Run:

```pwsh
dotnet run --file spikes/binding-generators/clangsharp/oracle.cs -- --family sdl2-core --family sdl2-image --family sdl2-gfx --family sdl2-ttf --family sdl2-mixer --write-report
```

Expected: exit 0. TTF has generated evidence; Mixer remains dormant/missing.

- [ ] **Step 3: Correct oracle only if evidence path is wrong**

If the oracle reports TTF as missing despite generated output existing, inspect `FamilyConfigs.Sdl2Ttf` in `oracle.cs` and correct only stale path data. Do not refactor oracle logic in Item 4.

---

### Task 11: Self-Tests And Slopwatch

- [ ] **Step 1: Python self-test**

Run:

```pwsh
python spikes/binding-generators/clangsharp/generate_bindings.py --self-test
```

Expected: PASS / exit 0.

- [ ] **Step 2: C# postprocess self-test**

Run:

```pwsh
dotnet run --project spikes/binding-generators/clangsharp/postprocess/Janset.SDL2.PostProcess.csproj -c Release -- --self-test
```

Expected: PASS / exit 0.

- [ ] **Step 3: Slopwatch**

Run:

```pwsh
slopwatch analyze --fail-on warning --exclude "artifacts/**,external/**,vcpkg_installed/**,spikes/binding-generators/references/**,**/bin/**,**/obj/**"
```

Expected: 0 issues.

---

### Task 12: Final Peer Visual Checks

**Files:**

- Compare: `external/sdl2-cs/src/SDL2_ttf.cs`
- Compare if available: Silk.NET SDL/TTF source found through local references or GitHub search
- Inspect: `spikes/binding-generators/clangsharp/src/Janset.SDL2.Ttf/Generated/**`

- [ ] **Step 1: SDL2-CS visual check**

Compare generated TTF output against `external/sdl2-cs/src/SDL2_ttf.cs` for:

- `TTF_Font` representation: expected Janset typed Pattern B vs SDL2-CS `IntPtr`.
- C `long`: expected Janset `CLong`/dual-dispatch vs SDL2-CS `long`/`IntPtr` shortcut.
- No-SDLCALL functions: expected `TTF_GetFontKerningSizeGlyphs`, `TTF_GetFontKerningSizeGlyphs32`, `TTF_SetFontSDF`, `TTF_GetFontSDF` present if generated.
- Deprecated functions: expected absent in Janset raw output.
- Error macros: expected absent in Janset raw output; SDL2-CS redirects them manually.

- [ ] **Step 2: Silk.NET visual check**

Search for a useful Silk.NET SDL_ttf peer surface. If found, compare handle representation, C `long`, no-SDLCALL, and macro/helper treatment. If not found, record: `No useful Silk.NET SDL_ttf peer surface found for Item 4 visual comparison.`

- [ ] **Step 3: Record notes**

Add concise notes to the implementation summary or handoff docs. Do not alter generated output based solely on peer divergence unless it reveals an ABI correctness issue.

---

### Task 13: Documentation Closure

**Files:**

- Modify: `spikes/binding-generators/docs/satellite-expansion-roadmap.md`
- Modify: `spikes/binding-generators/docs/next-iteration-plan.md`
- Modify: `spikes/binding-generators/docs/llm-handoff.md`

- [ ] **Step 1: Update roadmap status**

After all gates pass, mark Item 4 as closed with the date and summarize evidence counts.

- [ ] **Step 2: Update next-iteration plan**

Move Item 4 from queued/active to closed and identify Item 5 Mixer as next.

- [ ] **Step 3: Update LLM handoff**

Record final Item 4 evidence, known tradeoffs, and next recommended action.

---

### Task 14: Commit Preparation Only

- [ ] **Step 1: Inspect status and diff**

Run:

```pwsh
git status --short
git diff --stat
git diff --check
```

Expected: only intended Item 4 files changed; no whitespace errors.

- [ ] **Step 2: Ask Deniz before commit**

Present the change summary and proposed commit message. Do not commit without explicit approval.

Proposed commit message after implementation:

```text
feat(bindings): add SDL2_ttf Layer 1 raw ABI generation
```
