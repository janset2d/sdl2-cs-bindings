# Item 3: SDL2_gfx Layer 1 Raw ABI — Implementation Plan

> **For agentic workers:** Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Activate SDL2_gfx as the third family in the ClangSharp + Roslyn postprocess binding generator with full Layer 1 raw ABI output.

**Architecture:** Add GFX to the existing family-keyed pipeline — populate config, create family RSP, create C# project from Image template, wire into `selected_families("all")`. Zero postprocess changes; existing 7-step pipeline is GFX-safe.

**Tech Stack:** Python 3 (orchestrator), C# 14 / .NET 10 (postprocess), ClangSharp, JSON config

---

## File Map

| File | Action | Responsibility |
|------|--------|---------------|
| `config/family-config.json` | Modify | Populate `families.gfx.headers[]` with 4 headers |
| `rsp/sdl2-gfx.rsp` | Create | Family-level ClangSharp directives (export macro insurance + M_PI exclude) |
| `src/Janset.SDL2.Gfx/Janset.SDL2.Gfx.csproj` | Create | Multi-TFM project, ProjectReference→Core, Compat/Modern TFM dispatch |
| `src/Janset.SDL2.Gfx/Support/DisableRuntimeMarshalling.cs` | Create | Cross-assembly Pattern B contract (copy from Image) |
| `generate_bindings.py` | Modify | Add `"gfx"` to `selected_families("all")`; update self-test |
| `Janset.SDL2.ClangSharpSpike.slnx` | Modify | Add GFX project to solution |
| *(none)* | — | `oracle.cs` already has `Sdl2Gfx` in `FamilyConfigs` + `KnownFamilies` |

---

### Task 1: Populate GFX header list in config

**Files:**

- Modify: `spikes/binding-generators/clangsharp/config/family-config.json`

The GFX family section is at the end of `families` (after mixer), lines 579-616. Its `headers` field is currently `[]`.

Locate this block within `"gfx": { ... }`:

```json
      "headers": [],
```

Replace with:

```json
      "headers": [
        { "order": 10, "name": "SDL2_framerate.h" },
        { "order": 20, "name": "SDL2_gfxPrimitives.h" },
        { "order": 30, "name": "SDL2_imageFilter.h" },
        { "order": 40, "name": "SDL2_rotozoom.h" }
      ],
```

**Validation:** `python -c "import json; json.load(open('spikes/binding-generators/clangsharp/config/family-config.json'))"` exits 0.

---

### Task 2: Create family RSP

**Files:**

- Create: `spikes/binding-generators/clangsharp/rsp/sdl2-gfx.rsp`

Create the file with the following content — no `--libraryPath` or `--methodClassName` (those come from config per Iteration 2 RSP-identity retirement):

```rsp
# SDL2_gfx uses per-header export macros instead of extern DECLSPEC.
# Insurance: explicitly define each scope macro to extern so ClangSharp
# resolves function declarations regardless of host preprocessor state.
--define-macro
SDL2_GFXPRIMITIVES_SCOPE=extern
SDL2_ROTOZOOM_SCOPE=extern
SDL2_FRAMERATE_SCOPE=extern
SDL2_IMAGEFILTER_SCOPE=extern

# M_PI appears conditionally in SDL2_gfxPrimitives.h and SDL2_rotozoom.h
# (both guarded by #ifndef M_PI). Host compiler pre-defines it on Windows
# so ClangSharp never emits it there; cross-platform determinism requires
# an explicit exclude. BCL provides System.Math.PI.
--exclude
M_PI
```

**Validation:** File exists at the path, non-empty, contains 4 `--define-macro` lines and 1 `--exclude` line.

---

### Task 3: Create C# project

**Files:**

- Create: `spikes/binding-generators/clangsharp/src/Janset.SDL2.Gfx/Janset.SDL2.Gfx.csproj`
- Create: `spikes/binding-generators/clangsharp/src/Janset.SDL2.Gfx/Support/DisableRuntimeMarshalling.cs`

**Step 3a: Create the directory structure**

```pwsh
New-Item -ItemType Directory -Path spikes/binding-generators/clangsharp/src/Janset.SDL2.Gfx/Support -Force
New-Item -ItemType Directory -Path spikes/binding-generators/clangsharp/src/Janset.SDL2.Gfx/Generated/Compat -Force
New-Item -ItemType Directory -Path spikes/binding-generators/clangsharp/src/Janset.SDL2.Gfx/Generated/Modern -Force
```

**Step 3b: Create the csproj** (copy Image's with name changes)

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

  <!-- Core-owned SDL types (SDL_Surface*, SDL_Renderer*, etc.) come from
       Janset.SDL2.Core so satellite output does not redeclare them. -->
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

**Step 3c: Create `Support/DisableRuntimeMarshalling.cs`** (identical to Image's)

```csharp
#if NET7_0_OR_GREATER
[assembly: System.Runtime.CompilerServices.DisableRuntimeMarshalling]
#endif
```

**Validation:** `dotnet restore spikes/binding-generators/clangsharp/src/Janset.SDL2.Gfx/Janset.SDL2.Gfx.csproj` succeeds.

---

### Task 4: Add GFX to slnx

**Files:**

- Modify: `spikes/binding-generators/clangsharp/Janset.SDL2.ClangSharpSpike.slnx`

In the `/src/` folder, add the GFX project line after Image:

Current `/src/` folder (lines 5-8):

```xml
  <Folder Name="/src/">
    <Project Path="src/Janset.SDL2.Core/Janset.SDL2.Core.csproj" />
    <Project Path="src/Janset.SDL2.Image/Janset.SDL2.Image.csproj" />
  </Folder>
```

Replace with:

```xml
  <Folder Name="/src/">
    <Project Path="src/Janset.SDL2.Core/Janset.SDL2.Core.csproj" />
    <Project Path="src/Janset.SDL2.Image/Janset.SDL2.Image.csproj" />
    <Project Path="src/Janset.SDL2.Gfx/Janset.SDL2.Gfx.csproj" />
  </Folder>
```

---

### Task 5: Activate GFX in orchestrator

**Files:**

- Modify: `spikes/binding-generators/clangsharp/generate_bindings.py`

**Step 5a: Add `"gfx"` to `selected_families("all")`**

At line 881-882, change:

```python
    if family == "all":
        return ["core", "image"]
```

To:

```python
    if family == "all":
        return ["core", "image", "gfx"]
```

**Step 5b: Update self-test expectation**

At line 1578-1579, change:

```python
    if selected_families("all") != ["core", "image"]:
        failures.append(f"selected_families('all') must stay dormant as ['core', 'image']; got {selected_families('all')!r}")
```

To:

```python
    if selected_families("all") != ["core", "image", "gfx"]:
        failures.append(f"selected_families('all') must be ['core', 'image', 'gfx']; got {selected_families('all')!r}")
```

**Validation:** `python spikes/binding-generators/clangsharp/generate_bindings.py --self-test` passes (deferred until after generation — needs GFX headers processed first).

---

### Task 6: Run full generation

- [ ] **Step 6a: Regenerate all three families**

```pwsh
python spikes/binding-generators/clangsharp/generate_bindings.py --family all --execute --vcpkg-triplet x64-windows-hybrid --use-platform-header-shims
```

Expected: exits 0. Console output shows `core` (51 headers), `image` (1 header), `gfx` (4 headers). `Generated/` roots populated for all three.

- [ ] **Step 6b: Verify Core/Image determinism (no regression)**

```pwsh
git diff --ignore-cr-at-eol -- spikes/binding-generators/clangsharp/src/Janset.SDL2.Core/Generated/ spikes/binding-generators/clangsharp/src/Janset.SDL2.Image/Generated/
```

Expected: empty diff (Core and Image output byte-identical to pre-Item 3 baseline).

- [ ] **Step 6c: Verify GFX output exists**

```pwsh
Get-ChildItem -Recurse -Filter *.g.cs spikes/binding-generators/clangsharp/src/Janset.SDL2.Gfx/Generated/
```

Expected: 4+ `.g.cs` files (one per header, per codegen), each non-empty. `Handles.g.cs` NOT present (consumer mode).

---

### Task 7: Build verification

- [ ] **Step 7a: Full solution build**

```pwsh
dotnet build spikes/binding-generators/clangsharp/Janset.SDL2.ClangSharpSpike.slnx -c Release
```

Expected: 0 warnings, 0 errors across postprocess + Core (5 TFMs) + Image (5 TFMs) + GFX (5 TFMs).

- [ ] **Step 7b: GFX-specific build**

```pwsh
dotnet build spikes/binding-generators/clangsharp/src/Janset.SDL2.Gfx/Janset.SDL2.Gfx.csproj -c Release
```

Expected: 0/0.

---

### Task 8: Oracle evidence

- [ ] **Step 8a: Run five-family oracle**

```pwsh
dotnet run --file spikes/binding-generators/clangsharp/oracle.cs -- --family sdl2-core --family sdl2-image --family sdl2-gfx --family sdl2-ttf --family sdl2-mixer --write-report
```

Expected: exit 0. GFX row now shows generated function/constant/type counts (previously "missing"). Core + Image 0 new findings.

---

### Task 9: Runtime smoke + quality gates

- [ ] **Step 9a: AbiTests**

```pwsh
dotnet test --project spikes/binding-generators/clangsharp/tests/abi-tests/AbiTests.csproj -c Release
```

Expected: `792/792` across `net462`, `net8.0`, `net9.0`, `net10.0`.

- [ ] **Step 9b: Slopwatch**

```pwsh
slopwatch analyze --fail-on warning --exclude "artifacts/**,external/**,vcpkg_installed/**,spikes/binding-generators/references/**,**/bin/**,**/obj/**"
```

Expected: `0 issue(s) found`.

- [ ] **Step 9c: Python self-test**

```pwsh
python spikes/binding-generators/clangsharp/generate_bindings.py --self-test
```

Expected: PASS (exit 0).

- [ ] **Step 9d: C# postprocess self-test**

```pwsh
dotnet run --project spikes/binding-generators/clangsharp/postprocess/Janset.SDL2.PostProcess.csproj -c Release -- --self-test
```

Expected: PASS (exit 0).

---

### Task 10: Commit

```bash
git add spikes/binding-generators/clangsharp/config/family-config.json
git add spikes/binding-generators/clangsharp/rsp/sdl2-gfx.rsp
git add spikes/binding-generators/clangsharp/src/Janset.SDL2.Gfx/
git add spikes/binding-generators/clangsharp/Janset.SDL2.ClangSharpSpike.slnx
git add spikes/binding-generators/clangsharp/generate_bindings.py
git add spikes/binding-generators/clangsharp/src/Janset.SDL2.Gfx/Generated/
git commit -m "feat(bindings): add SDL2_gfx Layer 1 raw ABI generation"
```

Task 10 requires Deniz approval per AGENTS.md §Before Any Commit.
