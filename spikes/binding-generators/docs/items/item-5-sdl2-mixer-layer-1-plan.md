# Item 5: SDL2_mixer Layer 1 Raw ABI Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Status:** Closed 2026-05-28. Checkboxes are preserved as execution history and marked complete.

**Goal:** Activate SDL2_mixer as the fifth family in the ClangSharp + Roslyn postprocess binding generator with Layer 1 raw ABI output.

**Architecture:** Use the existing family-keyed pipeline. Activate one header in `family-config.json`, add a Mixer family RSP, create the Mixer spike project from the TTF/GFX/Image pattern, generate output, then verify callback signatures, `Mix_Music` owner-mode handle output, `Mix_Chunk`, enum shape, oracle evidence, regressions, and peer visual checks. Runtime callback/audio smoke stays deferred.

**Tech Stack:** Python 3 orchestrator, ClangSharp, C# 14 / .NET 10 Roslyn postprocess, multi-TFM spike class libraries.

---

## File Map

| File | Action | Responsibility |
|------|--------|----------------|
| `spikes/binding-generators/clangsharp/config/family-config.json` | Modify | Populate `families.mixer.headers[]` with `SDL_mixer.h` |
| `spikes/binding-generators/clangsharp/rsp/sdl2-mixer.rsp` | Create | Exclude Mixer error macros |
| `spikes/binding-generators/clangsharp/src/Janset.SDL2.Mixer/Janset.SDL2.Mixer.csproj` | Create | Multi-TFM Mixer spike project referencing Core |
| `spikes/binding-generators/clangsharp/src/Janset.SDL2.Mixer/Support/DisableRuntimeMarshalling.cs` | Create | Cross-assembly Pattern B contract |
| `spikes/binding-generators/clangsharp/src/Janset.SDL2.Mixer/Support/NativeTypeNameAttribute.cs` | Create | Per-assembly ClangSharp native type attribute |
| `spikes/binding-generators/clangsharp/Janset.SDL2.ClangSharpSpike.slnx` | Modify | Add Mixer project |
| `spikes/binding-generators/clangsharp/generate_bindings.py` | Modify | Add Mixer to aggregate `all` generation and self-test |
| `spikes/binding-generators/clangsharp/oracle.cs` | Verify/minimal modify only if needed | Confirm Mixer paths produce real evidence row |
| `spikes/binding-generators/docs/items/item-5-sdl2-mixer-layer-1-spec.md` | Modify only if implementation evidence changes design | Keep spec aligned with discovered facts |
| `spikes/binding-generators/docs/satellite-expansion-roadmap.md` | Modify at closure | Mark Item 5 closed and preserve callback smoke deferral |
| `spikes/binding-generators/docs/next-iteration-plan.md` | Modify at closure | Mark Item 5 closed and point to Layer 2 planning |
| `spikes/binding-generators/docs/llm-handoff.md` | Modify at closure | Record final Item 5 evidence for the next agent |

---

### Task 1: Activate Mixer Header In Config

**Files:**

- Modify: `spikes/binding-generators/clangsharp/config/family-config.json`

- [x] **Step 1: Replace dormant Mixer header list**

Find the `"mixer"` family block and replace:

```json
      "headers": [],
```

with:

```json
      "headers": [
        { "order": 10, "name": "SDL_mixer.h" }
      ],
```

- [x] **Step 2: Validate JSON parses**

Run:

```pwsh
python -c "import json; json.load(open('spikes/binding-generators/clangsharp/config/family-config.json', encoding='utf-8'))"
```

Expected: exit 0, no output.

---

### Task 2: Add Mixer Family RSP

**Files:**

- Create: `spikes/binding-generators/clangsharp/rsp/sdl2-mixer.rsp`

- [x] **Step 1: Create RSP file**

Create the file with exactly this content:

```rsp
# Mix_SetError / Mix_GetError / Mix_ClearError / Mix_OutOfMemory are
# macro shortcuts that point at SDL core error helpers. ClangSharp can't
# cross the family methodClassName boundary, so emit these as part of a
# later SDL2_mixer public helper layer rather than as raw bindings.
--exclude
Mix_SetError
Mix_GetError
Mix_ClearError
Mix_OutOfMemory
```

- [x] **Step 2: Confirm no per-header RSP exists**

Run:

```pwsh
Test-Path -LiteralPath "spikes/binding-generators/clangsharp/rsp/per-header/SDL_mixer.rsp"
```

Expected: `False`. If callback, macro, or callconv verification later fails, add a corrective per-header RSP as a new reviewed task instead of preemptively creating it.

---

### Task 3: Create Mixer Project And Support Files

**Files:**

- Create: `spikes/binding-generators/clangsharp/src/Janset.SDL2.Mixer/Janset.SDL2.Mixer.csproj`
- Create: `spikes/binding-generators/clangsharp/src/Janset.SDL2.Mixer/Support/DisableRuntimeMarshalling.cs`
- Create: `spikes/binding-generators/clangsharp/src/Janset.SDL2.Mixer/Support/NativeTypeNameAttribute.cs`

- [x] **Step 1: Create directories**

Run:

```pwsh
Test-Path -LiteralPath "spikes/binding-generators/clangsharp/src"; New-Item -ItemType Directory -Path "spikes/binding-generators/clangsharp/src/Janset.SDL2.Mixer/Support" -Force; New-Item -ItemType Directory -Path "spikes/binding-generators/clangsharp/src/Janset.SDL2.Mixer/Generated/Compat" -Force; New-Item -ItemType Directory -Path "spikes/binding-generators/clangsharp/src/Janset.SDL2.Mixer/Generated/Modern" -Force
```

Expected: first command prints `True`; directories exist.

- [x] **Step 2: Create csproj**

Create `Janset.SDL2.Mixer.csproj` with exactly:

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

  <!-- Core-owned SDL types (SDL_RWops*, SDL_version, SDL_bool, audio typedefs,
       etc.) come from Janset.SDL2.Core so satellite output does not redeclare them. -->
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

- [x] **Step 3: Create DisableRuntimeMarshalling support file**

Create `Support/DisableRuntimeMarshalling.cs` with exactly:

```csharp
#if NET7_0_OR_GREATER
[assembly: System.Runtime.CompilerServices.DisableRuntimeMarshalling]
#endif
```

- [x] **Step 4: Create NativeTypeNameAttribute support file**

Create `Support/NativeTypeNameAttribute.cs` with exactly:

```csharp
// <auto-generated/>

using System;
using System.Diagnostics;

namespace SDL2.Mixer;

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

### Task 4: Add Mixer To Spike Solution

**Files:**

- Modify: `spikes/binding-generators/clangsharp/Janset.SDL2.ClangSharpSpike.slnx`

- [x] **Step 1: Add project entry**

Change the `/src/` folder from:

```xml
  <Folder Name="/src/">
    <Project Path="src/Janset.SDL2.Core/Janset.SDL2.Core.csproj" />
    <Project Path="src/Janset.SDL2.Image/Janset.SDL2.Image.csproj" />
    <Project Path="src/Janset.SDL2.Gfx/Janset.SDL2.Gfx.csproj" />
    <Project Path="src/Janset.SDL2.Ttf/Janset.SDL2.Ttf.csproj" />
  </Folder>
```

to:

```xml
  <Folder Name="/src/">
    <Project Path="src/Janset.SDL2.Core/Janset.SDL2.Core.csproj" />
    <Project Path="src/Janset.SDL2.Image/Janset.SDL2.Image.csproj" />
    <Project Path="src/Janset.SDL2.Gfx/Janset.SDL2.Gfx.csproj" />
    <Project Path="src/Janset.SDL2.Ttf/Janset.SDL2.Ttf.csproj" />
    <Project Path="src/Janset.SDL2.Mixer/Janset.SDL2.Mixer.csproj" />
  </Folder>
```

---

### Task 5: Generate Mixer Explicitly

**Files:**

- Generated: `spikes/binding-generators/clangsharp/src/Janset.SDL2.Mixer/Generated/**`

- [x] **Step 1: Run explicit Mixer generation**

Run:

```pwsh
python spikes/binding-generators/clangsharp/generate_bindings.py --family mixer --execute --vcpkg-triplet x64-windows-hybrid --use-platform-header-shims
```

Expected: exit 0. Output includes Mixer generation and postprocess stages.

- [x] **Step 2: Verify generated files exist**

Run:

```pwsh
Test-Path -LiteralPath "spikes/binding-generators/clangsharp/src/Janset.SDL2.Mixer/Generated/Compat/SDL_mixer.g.cs"; Test-Path -LiteralPath "spikes/binding-generators/clangsharp/src/Janset.SDL2.Mixer/Generated/Modern/SDL_mixer.g.cs"; Test-Path -LiteralPath "spikes/binding-generators/clangsharp/src/Janset.SDL2.Mixer/Generated/Compat/Handles.g.cs"; Test-Path -LiteralPath "spikes/binding-generators/clangsharp/src/Janset.SDL2.Mixer/Generated/Modern/Handles.g.cs"
```

Expected: four `True` values.

---

### Task 6: Verify Generated ABI Shape Before Aggregate Activation

**Files:**

- Inspect: `spikes/binding-generators/clangsharp/src/Janset.SDL2.Mixer/Generated/Compat/SDL_mixer.g.cs`
- Inspect: `spikes/binding-generators/clangsharp/src/Janset.SDL2.Mixer/Generated/Modern/SDL_mixer.g.cs`
- Inspect: `spikes/binding-generators/clangsharp/src/Janset.SDL2.Mixer/Generated/{Compat,Modern}/Handles.g.cs`

- [x] **Step 1: Check required callback symbols are present**

Run:

```pwsh
rg "Mix_MixCallback|Mix_MusicFinishedCallback|Mix_ChannelFinishedCallback|Mix_EffectFunc_t|Mix_EffectDone_t|Mix_EachSoundFontCallback|Mix_SetPostMix|Mix_HookMusic|Mix_HookMusicFinished|Mix_ChannelFinished|Mix_RegisterEffect|Mix_UnregisterEffect|Mix_EachSoundFont" spikes/binding-generators/clangsharp/src/Janset.SDL2.Mixer/Generated
```

Expected: finds all six callback typedef names and all seven callback-consuming functions.

- [x] **Step 2: Check excluded error macro symbols are absent**

Run:

```pwsh
rg "Mix_SetError|Mix_GetError|Mix_ClearError|Mix_OutOfMemory" spikes/binding-generators/clangsharp/src/Janset.SDL2.Mixer/Generated
```

Expected: no raw generated declarations or constants for excluded error macro symbols.

- [x] **Step 3: Check Compat callback Cdecl output**

Run:

```pwsh
rg "UnmanagedFunctionPointer\(CallingConvention\.Cdecl\)|public (unsafe )?delegate|Mix_MixCallback|Mix_MusicFinishedCallback|Mix_ChannelFinishedCallback|Mix_EffectFunc_t|Mix_EffectDone_t|Mix_EachSoundFontCallback" spikes/binding-generators/clangsharp/src/Janset.SDL2.Mixer/Generated/Compat/SDL_mixer.g.cs
```

Expected: Compat output contains delegate declarations for the six callback typedefs and each callback delegate is associated with `[UnmanagedFunctionPointer(CallingConvention.Cdecl)]`.

- [x] **Step 4: Check Modern callback function-pointer output**

Run:

```pwsh
rg "delegate\* unmanaged\[Cdecl\]|Mix_SetPostMix|Mix_HookMusic|Mix_HookMusicFinished|Mix_ChannelFinished|Mix_RegisterEffect|Mix_UnregisterEffect|Mix_EachSoundFont" spikes/binding-generators/clangsharp/src/Janset.SDL2.Mixer/Generated/Modern/SDL_mixer.g.cs
```

Expected: Modern callback-consuming function declarations use `delegate* unmanaged[Cdecl]<...>` callback parameters. `Mix_RegisterEffect` has two callback-typed parameters.

- [x] **Step 5: Check owner-mode handle output**

Run:

```pwsh
rg "namespace SDL2\.Mixer|public readonly partial struct Mix_Music" spikes/binding-generators/clangsharp/src/Janset.SDL2.Mixer/Generated/*/Handles.g.cs
```

Expected: both Compat and Modern handles files contain namespace `SDL2.Mixer` and `Mix_Music` Pattern B declarations.

- [x] **Step 6: Check `Mix_Chunk` transparent struct output**

Run:

```pwsh
rg "struct Mix_Chunk|allocated|abuf|alen|volume" spikes/binding-generators/clangsharp/src/Janset.SDL2.Mixer/Generated/*/SDL_mixer.g.cs
```

Expected: `Mix_Chunk` is a real struct containing the four expected fields; it is not emitted in `Handles.g.cs`.

- [x] **Step 7: Check enum flags output**

Run:

```pwsh
rg "Flags|enum MIX_InitFlags|enum Mix_Fading|enum Mix_MusicType" spikes/binding-generators/clangsharp/src/Janset.SDL2.Mixer/Generated
```

Expected: `MIX_InitFlags` has `[System.Flags]`; `Mix_Fading` and `Mix_MusicType` do not.

---

### Task 7: Build Mixer Before Aggregate Activation

**Files:**

- Build: `spikes/binding-generators/clangsharp/src/Janset.SDL2.Mixer/Janset.SDL2.Mixer.csproj`

- [x] **Step 1: Build Mixer project**

Run:

```pwsh
dotnet build spikes/binding-generators/clangsharp/src/Janset.SDL2.Mixer/Janset.SDL2.Mixer.csproj -c Release
```

Expected: 0 warnings, 0 errors across all five TFMs.

---

### Task 8: Activate Mixer In Aggregate Generation

**Files:**

- Modify: `spikes/binding-generators/clangsharp/generate_bindings.py`

- [x] **Step 1: Update selected families**

Change:

```python
    if family == "all":
        return ["core", "image", "gfx", "ttf"]
```

to:

```python
    if family == "all":
        return ["core", "image", "gfx", "ttf", "mixer"]
```

- [x] **Step 2: Update self-test expectation**

Find the self-test assertion that expects `selected_families("all")` to equal `['core', 'image', 'gfx', 'ttf']` and update it to `['core', 'image', 'gfx', 'ttf', 'mixer']`. Keep SDL2_net and SDL3 dormant.

---

### Task 9: Run Aggregate Generation And Regression Diff

**Files:**

- Generated: Core/Image/GFX/TTF/Mixer `Generated/**`

- [x] **Step 1: Regenerate all active families**

Run:

```pwsh
python spikes/binding-generators/clangsharp/generate_bindings.py --family all --execute --vcpkg-triplet x64-windows-hybrid --use-platform-header-shims
```

Expected: exit 0. Active families are Core, Image, GFX, TTF, and Mixer only.

- [x] **Step 2: Verify existing family determinism**

Run:

```pwsh
git diff --ignore-cr-at-eol -- spikes/binding-generators/clangsharp/src/Janset.SDL2.Core/Generated/ spikes/binding-generators/clangsharp/src/Janset.SDL2.Image/Generated/ spikes/binding-generators/clangsharp/src/Janset.SDL2.Gfx/Generated/ spikes/binding-generators/clangsharp/src/Janset.SDL2.Ttf/Generated/
```

Expected: empty diff for Core/Image/GFX/TTF generated output.

---

### Task 10: Build And Oracle Evidence

**Files:**

- Verify: `spikes/binding-generators/clangsharp/oracle.cs`

- [x] **Step 1: Build full spike solution**

Run:

```pwsh
dotnet build spikes/binding-generators/clangsharp/Janset.SDL2.ClangSharpSpike.slnx -c Release
```

Expected: 0 warnings, 0 errors.

- [x] **Step 2: Run five-family oracle**

Run:

```pwsh
dotnet run --file spikes/binding-generators/clangsharp/oracle.cs -- --family sdl2-core --family sdl2-image --family sdl2-gfx --family sdl2-ttf --family sdl2-mixer --write-report
```

Expected: exit 0. Mixer has generated evidence and is no longer reported as missing/dormant.

- [x] **Step 3: Correct oracle only if evidence path is wrong**

If the oracle reports Mixer as missing despite generated output existing, inspect `FamilyConfigs.Sdl2Mixer` and Mixer path handling in `oracle.cs`; correct only stale path or evidence mapping data. Do not refactor oracle logic in Item 5.

---

### Task 11: Self-Tests And Slopwatch

- [x] **Step 1: Python self-test**

Run:

```pwsh
python spikes/binding-generators/clangsharp/generate_bindings.py --self-test
```

Expected: PASS / exit 0.

- [x] **Step 2: C# postprocess self-test**

Run:

```pwsh
dotnet run --project spikes/binding-generators/clangsharp/postprocess/Janset.SDL2.PostProcess.csproj -c Release -- --self-test
```

Expected: PASS / exit 0.

- [x] **Step 3: Slopwatch**

Run:

```pwsh
slopwatch analyze --fail-on warning --exclude "artifacts/**,external/**,vcpkg_installed/**,spikes/binding-generators/references/**,**/bin/**,**/obj/**"
```

Expected: 0 issues.

---

### Task 12: Final Peer Visual Checks

**Files:**

- Compare: `external/sdl2-cs/src/SDL2_mixer.cs`
- Compare: `spikes/binding-generators/references/ppy-SDL3-CS/SDL3_mixer-CS/SDL3_mixer/ClangSharp/SDL_mixer.g.cs`
- Inspect: `spikes/binding-generators/clangsharp/src/Janset.SDL2.Mixer/Generated/**`

- [x] **Step 1: SDL2-CS visual check**

Compare generated Mixer output against `external/sdl2-cs/src/SDL2_mixer.cs` for:

- `Mix_Music` representation: expected Janset typed Pattern B vs SDL2-CS `IntPtr` comments.
- `Mix_Chunk` representation: expected transparent generated struct with pointer field vs SDL2-CS `MIX_Chunk` with `IntPtr abuf`.
- Callback typedefs and callback-consuming functions: expected Cdecl delegate/function-pointer ABI shape.
- `[Flags]`: expected `MIX_InitFlags` decorated; `Mix_Fading` and `Mix_MusicType` undecorated.
- Error macros: expected absent in Janset raw output; SDL2-CS redirects `Mix_GetError`, `Mix_SetError`, and `Mix_ClearError` manually.
- Stale peer constants: SDL2-CS targets older Mixer constants; Janset follows pinned SDL2_mixer 2.8.1.

- [x] **Step 2: ppy/SDL3-CS visual check**

Use ppy/SDL3-CS only to compare ClangSharp callback output shape. Record that SDL3_mixer is not an SDL2_mixer API oracle because SDL3_mixer's API is substantially different.

- [x] **Step 3: Silk.NET visual check**

Search for a useful Silk.NET SDL2_mixer peer surface. If found, compare handle representation, callback treatment, and macro/helper treatment. If not found, record: `No useful Silk.NET SDL2_mixer peer surface found for Item 5 visual comparison.`

- [x] **Step 4: Record notes**

Add concise notes to the implementation summary or handoff docs. Do not alter generated output based solely on peer divergence unless it reveals an ABI correctness issue.

---

### Task 13: Documentation Closure

**Files:**

- Modify: `spikes/binding-generators/docs/satellite-expansion-roadmap.md`
- Modify: `spikes/binding-generators/docs/next-iteration-plan.md`
- Modify: `spikes/binding-generators/docs/llm-handoff.md`
- Modify only if implementation evidence changes design: `spikes/binding-generators/docs/items/item-5-sdl2-mixer-layer-1-spec.md`

- [x] **Step 1: Update roadmap status**

After all gates pass, mark Item 5 as closed with the date and summarize evidence counts. Preserve the deferred callback lifetime/rooting follow-up as a separate non-closure item.

- [x] **Step 2: Update next-iteration plan**

Move Item 5 from queued/active to closed and identify Layer 2 public typed low-level API planning as the next forward scope. Keep the Mixer callback lifetime/rooting policy in the Layer 2 / Layer 3 follow-up table.

- [x] **Step 3: Update LLM handoff**

Record final Item 5 evidence, known tradeoffs, callback-runtime-smoke deferral, and next recommended action.

---

### Task 14: Commit Preparation Only

- [x] **Step 1: Inspect status and diff**

Run:

```pwsh
git status --short
git diff --stat
git diff --check
```

Expected: only intended Item 5 files changed plus any pre-existing user edits that were deliberately left untouched; no whitespace errors.

- [x] **Step 2: Ask Deniz before commit**

Present the change summary and proposed commit message. Do not commit without explicit approval.

Proposed commit message after implementation:

```text
feat(bindings): add SDL2_mixer Layer 1 raw ABI generation
```
