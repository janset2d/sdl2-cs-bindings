# ppy/SDL3-CS Reference Analysis — 2026-05-25

**Status:** Research artifact. Findings feed the satellite-family expansion and inform Layer 2/3 design decisions. Not policy by itself — the Constitution is the policy authority.

**Source:** Local clone at `spikes/binding-generators/references/ppy-SDL3-CS/` (SDL3-CS commit `0d32669` + submodules).

---

## 1. Satellite Generation Architecture — The Core Mechanism

### The Problem

When generating bindings for a satellite library like SDL3_image, you need to resolve types from the core library (`SDL_Surface*`, `SDL_Renderer*`, etc.) without regenerating the core binding surface. If every satellite invocation re-emitted core type definitions, you'd get duplicate `public partial struct SDL_Surface` across multiple assemblies, which breaks C# compilation.

### ppy's Solution: `--file` Scoping + Shared Include Dirs + ProjectReference

ppy uses a **single** `generate_bindings.py` orchestrator that generates all four families (SDL3, SDL3_image, SDL3_ttf, SDL3_mixer) in one flat script. The mechanism:

```
                    ┌──────────────────────────────────┐
                    │     generate_bindings.py          │
                    │     (single orchestrator)          │
                    └──────────┬───────────────────────┘
                               │
          ┌────────────────────┼────────────────────────┐
          │                    │                        │
          ▼                    ▼                        ▼
   ┌──────────────┐   ┌──────────────┐   ┌──────────────────┐
   │ ClangSharp   │   │ ClangSharp   │   │ ClangSharp       │
   │ --file       │   │ --file       │   │ --file           │
   │ SDL_surface.h│   │ SDL_image.h  │   │ SDL_ttf.h        │
   │ --include-dir│   │ --include-dir│   │ --include-dir    │
   │  +SDL/       │   │  +SDL/       │   │  +SDL/           │
   │  +SDL_image/ │   │  +SDL_image/ │   │  +SDL_ttf/       │
   │  +SDL_ttf/   │   │  +SDL_ttf/   │   │  +SDL_mixer/     │
   │  +SDL_mixer/ │   │  +SDL_mixer/ │   │                  │
   └──────┬───────┘   └──────┬───────┘   └──────┬───────────┘
          │                  │                  │
          ▼                  ▼                  ▼
   SDL_surface.g.cs    SDL_image.g.cs     SDL_ttf.g.cs
   (defines SDL_       (uses SDL_Surface*, (uses SDL_Surface*,
    Surface struct)     doesn't redefine)   doesn't redefine)
```

**Three critical rules:**

1. **`--include-directory` for ALL libraries on EVERY invocation.** ClangSharp needs to see every type definition to resolve them. All four `External/SDL*/include` paths are passed to every single `--file` invocation.

2. **`--file` limits EMISSION to one header.** ClangSharp's `--file` parameter says "this is the translation unit — only generate C# for declarations in THIS file." Transitive `#include`'d types are resolved for correctness but NOT emitted as duplicate C# declarations.

3. **`ProjectReference` resolves types at C# compile time.** Satellites don't regenerate core types because they `ProjectReference` the core assembly:
   ```xml
   <ProjectReference Include="..\SDL3-CS\SDL3-CS.csproj"/>
   ```

**Verification via grep:** `SDL_Surface` is defined exactly once across all 65 `.g.cs` files — in `SDL3-CS/SDL3/ClangSharp/SDL_surface.g.cs`. Satellites reference it by name only. Zero occurrences of `public partial struct SDL_` in any satellite project.

### What Our Spike Already Has

Our current `spikes/binding-generators/clangsharp/` already implements this pattern for Core + Image:

- `generate_bindings.py` has `FAMILY_CONFIG` dict with per-family identity
- `rsp/sdl2-core.rsp` and `rsp/sdl2-image.rsp` are family-scope RSP files
- `Janset.SDL2.Image.csproj` has `<ProjectReference Include="../Janset.SDL2.Core/..."/>`
- `--owner-mode consumer` in the uniform-opaque postprocess suppresses `Handles.g.cs` emit in Image
- Pattern B handles live in `namespace SDL2` (Core), visible to `namespace SDL2.Image` (Image)

**What's missing for TTF/Mixer/GFX:**

- Header include directories for TTF/Mixer/GFX libraries (currently only SDL2 + SDL2_image are in scope)
- Family RSP files for each new family
- Csproj projects for each new family
- `FAMILY_CONFIG` entries for each new family

### ppy's Flat Header List Pattern

All 54 headers (core + 3 satellites) are one flat Python list:

```python
headers = [
    "SDL3/SDL.h",           # umbrella — special
    "SDL3/SDL_audio.h",     # core
    "SDL3/SDL_blendmode.h", # core
    ...
    "SDL3_image/SDL_image.h", # satellite
    "SDL3_ttf/SDL_ttf.h",     # satellite
    "SDL3_mixer/SDL_mixer.h", # satellite
]
```

You can filter to a single header: `python generate_bindings.py SDL3_ttf/SDL_ttf.h`. This means regenerating just one satellite doesn't touch core `.g.cs` files.

### Namespace Strategy

ppy puts everything in a single `namespace SDL`. All 54 headers emit into that namespace regardless of which family they belong to. Satellite method classes (e.g., `SDL3_image`) live in the same namespace as core types (`SDL_Surface`), so unqualified name resolution works without `using` directives.

For SDL2, we follow a similar pattern: `namespace SDL2` for Core, and satellite namespaces that nest inside it (`SDL2.Image`, `SDL2.Mixer`, etc.). C# namespace nesting means `SDL2.Image` can reference `SDL2.SDL_Window` unqualified — the compiler walks up the namespace chain.

---

## 2. Companion Class Analysis — Layer Mapping

### The Three-Source Model

ppy/SDL3-CS uses three sources of C# code that compile into each assembly:

| Source | Author | What it contains | Our layer |
|---|---|---|---|
| **Generated `.g.cs`** | ClangSharp | Raw `public static extern [DllImport]`, structs, enums, macro constants | **Layer 1** (but PUBLIC in ppy) |
| **Manual companion `.cs`** | Hand-written | Typedef enums, function-like macros, friendly wrappers, struct partial extensions | **Layer 2 + Layer 3** |
| **Roslyn source generator** | Auto at build | `string?`/`Utf8String` overloads for `const char*`-returning functions | **Layer 3** |

### Key Architectural Difference: Layer 1 Visibility

| Aspect | ppy/SDL3-CS | Our Constitution |
|---|---|---|
| Raw P/Invoke visibility | **PUBLIC** | **INTERNAL** |
| Layer 2 existence | No systematic Layer 2. Typed enums and struct extensions live in companion files ad-hoc | Layer 2 is a **generated** systematic projection with no extern declarations |
| Companion files | ~27 files, mix of Layer 2 and Layer 3 content | We generate Layer 2; companion files are the anti-pattern Deniz rejected |

ppy puts raw P/Invoke as the user-facing API and compensates with manual companion files. Our design inverts this: internal raw ABI is generated, public typed API is the systematic projection. This is cleaner but requires more upfront generator work for Layer 2.

### Companion File Content Categories

#### Category 1: Typedef Enums (`[Typedef]` → Layer 2)

Manual typed enums that replace raw integer types in function signatures. Examples:

```csharp
// SDL_video.cs
[Typedef] public enum SDL_DisplayID : UInt32 { }
[Typedef] public enum SDL_WindowID : UInt32 { }
[Flags] [Typedef] public enum SDL_WindowFlags : UInt64 { /* ~30 members */ }
```

The `[Typedef]` attribute (compile-time conditional: `[Conditional("NEVER")]`) is a regex marker for the Python orchestrator. At generation time, the orchestrator scans all companion `.cs` files, extracts typedef names, and adds global `--remap SDL_DisplayID=SDL_DisplayID` entries. This tells ClangSharp: "use the manual enum everywhere you see this type in function signatures."

**Why this matters for us:** We should auto-detect `typedef enum` vs `typedef uint32_t SDL_X` patterns rather than requiring manual `[Typedef]` companion files for hundreds of type IDs. ppy's `SDL_keycode.cs` has 260 manual enum members — that's high-maintenance. The Constitution (L102) says enums are code-owned policy. Our generator should auto-classify and emit the right shape.

#### Category 2: Function-Like Macros (`[Macro]` → Layer 3)

C function-like macros reimplemented in C#. Examples:

```csharp
// SDL_version.cs
[Macro] public static int SDL_VERSIONNUM(int major, int minor, int patch)
    => major * 1000000 + minor * 1000 + patch;

// SDL_pixels.cs
[Macro] public static uint SDL_DEFINE_PIXELFOURCC(byte A, byte B, byte C, byte D)
    => FOURCC(A, B, C, D);
```

The `[Macro]` attribute is NOT scanned by ppy's Python orchestrator (unlike `[Constant]` and `[Typedef]`). It appears to be documentation-only. The macro methods coexist alongside generated methods in the same `partial class`.

**Why this matters for us:** Constitution (L456) says "Function-like public macros are helper candidates, not constants." Our `macro_constants.overrides` manifest field and the `required_constants` lane already handle the simple case. Auto-generation of macro helpers where the expression is evaluable is the preferred path per the "no magic companion" philosophy.

#### Category 3: Struct Partial Extensions (Layer 2/3 hybrid)

```csharp
// SDL_events.cs
public partial struct SDL_CommonEvent
{
    public SDL_EventType Type => (SDL_EventType)type;
}

public partial struct SDL_TextInputEvent
{
    public string? GetText() => SDL3.PtrToStringUTF8(text);
}
```

These extend ClangSharp-generated structs with typed property accessors and string helpers. The generated struct has a raw `uint type` field; the companion adds a typed `SDL_EventType Type` property.

**Why this matters for us:** If our Layer 2 knows that `uint type` in `SDL_CommonEvent` maps to `SDL_EventType`, we can emit the typed property directly in the Layer 2 struct projection rather than relying on manual partial extensions.

#### Category 4: Array-Returning Friendly Wrappers (Layer 3)

```csharp
// SDL_video.cs
[MustDisposeResource]
public static SDLArray<SDL_DisplayID> SDL_GetDisplays()
{
    int count;
    var displays = SDL_GetDisplays(&count); // calls raw
    return new SDLArray<SDL_DisplayID>(displays, count);
}
```

The `SDLArray<T>`, `SDLOpaquePointerArray<T>`, etc. are infrastructure types (104 lines in `SDLArray.cs`) that wrap raw pointer+count returns with `IDisposable` and auto-`SDL_free` on dispose.

**Why this matters for us:** This is a good Layer 3 pattern worth adopting. The wrapper types themselves are infrastructure (not per-function ceremony), and the friendly methods are straightforward projections. We'd generate these rather than hand-write them.

#### Category 5: String Marshalling Infrastructure (Layer 3)

```csharp
// SDL3.cs (root file, ~29 lines)
public static string? PtrToStringUTF8(byte* ptr, bool free = false) { ... }

// Utf8String.cs (~57 lines)
public ref struct Utf8String
{
    // implicit conversion from string? and ReadOnlySpan<byte>
    // GetPinnableReference() for C# fixed statement
}
```

Zero-alloc UTF-8 string type with implicit conversion from `string?` / `ReadOnlySpan<byte>`. The Roslyn source generator uses this: methods with `const char*` parameters get overloads accepting `Utf8String` instead of `byte*`.

### ppy's Roslyn Source Generator — Layer 3 Mechanism

**Location:** `SDL3-CS.SourceGeneration/FriendlyOverloadGenerator.cs` (192 lines)

Purely syntactic transform, SDL-semantics-free:

1. Finds methods with return type `byte*` + name starting with `Unsafe_` → generates `string?`-returning overload via `PtrToStringUTF8()`
2. Finds parameters with `[NativeTypeName("const char *")]` → generates overload accepting `Utf8String` with `fixed` pinning

The `Unsafe_` prefix convention is a contract between the Python orchestrator (which remaps `const char*`-returning functions to `Unsafe_X`) and the Roslyn generator (which looks for `Unsafe_`-prefixed methods). Source generation runs at consumer BUILD time, not at generator time.

**Why this matters for us:** Our Constitution + roadmap use committed `.g.cs` files, NOT Roslyn source generators. We'd implement equivalent transforms as postprocess rewriter steps (like our existing `libraryimport`, `guid-substitute`, etc.). Same concept, different runtime.

---

## 3. What We Should Adapt vs Do Differently

| Aspect | ppy approach | Our adaptation | Rationale |
|---|---|---|---|
| **Single orchestrator** | One script, all families | Keep single `generate_bindings.py`, extend `FAMILY_CONFIG` | Works, we already have the pattern |
| **`--include-directory` for all** | All lib headers visible to all invocations | Add TTF/Mixer/GFX include dirs to `base_command` | ClangSharp needs to resolve transitive types |
| **`--file` per header** | One ClangSharp invocation per header | Same — we already do this | Prevents duplicate type emission |
| **ProjectReference chain** | Satellites → Core | Same — we already do this for Image | Standard .NET, zero friction |
| **Flat header list** | All 54 headers in one Python list | Keep our `scope/*.headers.txt` pattern | Our scope files are cleaner than inline lists |
| **Per-header RSP** | Optional `.rsp` overrides | Already implemented via `per_header_rsp_path()` | Priority C infrastructure |
| **Shared namespace** | Everything in `namespace SDL` | Core in `namespace SDL2`, satellites in nested namespaces | C# namespace nesting gives same unqualified resolution |
| **Manual `[Typedef]` enums** | 260+ lines of hand-written enum members | Auto-detect typedef enums from ClangSharp output | Avoid manual maintenance hell |
| **Manual `[Macro]` methods** | Hand-written C# for C macros | Auto-generate where expression evaluable | "No magic companion" philosophy |
| **Struct partial extensions** | Manual typed properties on generated structs | Emit typed properties in Layer 2 struct projection | Systematic > ad-hoc |
| **Array-friendly wrappers** | `SDLArray<T>` with `IDisposable` | Generate friendly wrappers in Layer 3 | Good pattern, adopt infrastructure types |
| **Roslyn source generator** | Consumer-build-time SG | Commit-time postprocess rewriter | Constitution + roadmap require committed `.g.cs` |
| **Raw ABI visibility** | **PUBLIC** raw P/Invoke | **INTERNAL** raw ABI, Layer 2 public typed projection | Constitution L34-50 — settled decision |

---

## 4. Satellite Family Header Inventories

### SDL2_ttf

| Header | Key types | Notable risks |
|---|---|---|
| `SDL_ttf.h` | `TTF_Font*` (opaque), `TTF_TextLayout` enum, `TTF_*` functions | **C `long` surface**: `TTF_OpenFontIndex*`, `TTF_OpenFontDPI`, `TTF_FontFaces` (Constitution L264 calls this out explicitly). Reuses SDL2.Core hybrid CLong/dual-dispatch pattern from Slice C-A. |

TTF is a single-header library. The C `long` risk is the main one — and it's already scoped in the Constitution. The opaque handle for `TTF_Font` follows the same Pattern B shape as `SDL_Window`, but since TTF is a separate assembly, it needs its own `Handles.g.cs` in owner mode.

### SDL2_mixer

| Header | Key types | Notable risks |
|---|---|---|
| `SDL_mixer.h` | `Mix_Music*` (opaque), `Mix_Chunk` (transparent struct), `Mix_Fading`/`Mix_MusicType` enums, callback types | **Callback delegates**: `Mix_EffectFunc_t`, `Mix_EffectDone_t`, `Mix_MusicFinished`, `Mix_EachSoundFont` — managed delegate lifecycle. **Chunk struct** has fixed-size array: `Mix_Chunk.allocated` with union field. |

Mixer is a single-header library with moderate complexity. The key risk is callback delegates — C function pointers that require managed delegate marshalling. Also `Mix_Chunk` has a union field (`abuf`) which may need explicit layout.

### SDL2_gfx

| Header | Key types | Notable risks |
|---|---|---|
| `SDL2_framerate.h` | `FPSmanager` struct | Per-header export macros |
| `SDL2_gfxPrimitives.h` | Drawing functions | Per-header export macros |
| `SDL2_imageFilter.h` | Filter functions | Per-header export macros |
| `SDL2_rotozoom.h` | `SDL2_gfx` namespace functions | Per-header export macros |

GFX is the most unusual satellite. Constitution (L82) calls it out: "SDL2_gfx uses per-header `SDL2_*_SCOPE` export macros and a mixed naming surface." Unlike SDL2_image/ttf/mixer which use `extern DECLSPEC`, GFX uses:

```c
// SDL2_gfxPrimitives.h
#define SDL2_GFXPRIMITIVES_SCOPE extern DECLSPEC
extern SDL2_GFXPRIMITIVES_SCOPE int pixelColor(SDL_Renderer* renderer, ...);
```

ClangSharp may not recognize these custom export macros as function declarations. The approach options:
- Define the scope macros in `--define-macro` so ClangSharp sees them as `extern DECLSPEC`
- Parse the headers with explicit function lists backed by binary symbol evidence

GFX also has mixed naming: some functions use `SDL_` prefix, others use bare names like `pixelColor`. The function surface may need manual curation.

---

## 5. Recommended Approach for Satellite Expansion

### Phase 1: Infrastructure mirror (low risk)

1. Create `scope/sdl2-ttf.headers.txt`, `scope/sdl2-mixer.headers.txt`, `scope/sdl2-gfx.headers.txt`
2. Create `rsp/sdl2-ttf.rsp`, `rsp/sdl2-mixer.rsp`, `rsp/sdl2-gfx.rsp` — family identity + scope macros for GFX
3. Create `src/Janset.SDL2.Ttf/`, `src/Janset.SDL2.Mixer/`, `src/Janset.SDL2.Gfx/` — csproj + `Support/DisableRuntimeMarshalling.cs`, mirroring Image pattern
4. Add family config to `generate_bindings.py` FAMILY_CONFIG
5. Add include directories for all three libraries to ClangSharp base command
6. Add projects to solution

### Phase 2: Header deep-dive (per family)

Each family gets a detailed analysis agent that:
- Reads the actual installed headers from `vcpkg_installed/<triplet>/include/SDL2/`
- Identifies function surface, type inventory, opaque handles, callback types
- Flags ABI risks: C `long`, `wchar_t`, unions, fixed arrays, platform-conditioned layouts
- Proposes RSP exclude/remap entries
- Identifies symbols needing special postprocess treatment

### Phase 3: Generation + validation

1. Run full-scope generation with all 5 families
2. Multi-TFM compile check (5 families × 5 TFMs)
3. Oracle validation per family
4. AbiTests expansion per family
5. Slice-specific risk closure (TTF C `long` proof, GFX export macro resolution, Mixer callback smoke)

### Family Ordering

Recommended: **TTF first** (single header, known C `long` risk with existing hybrid pattern), then **Mixer** (single header, callback complexity), then **GFX** (multi-header, export macro risk — most unusual).

---

## 6. Cross-References

- [`binding-generator-constitution.md`](../../../docs/binding-autogen/binding-generator-constitution.md) — Layer Contract, C `long` policy, Opaque Handles, Foreign Type Boundary Policy
- [`next-iteration-plan.md`](next-iteration-plan.md) — active slice plan
- [`priority-c-closure-summary.md`](priority-c-closure-summary.md) — authoritative Priority C closure record
- [`../../../docs/binding-autogen/binding-generator-roadmap.md`](../../../docs/binding-autogen/binding-generator-roadmap.md) — milestone plan
- `spikes/binding-generators/references/ppy-SDL3-CS/` — reference clone (gitignored)
