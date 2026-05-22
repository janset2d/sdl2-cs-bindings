# ClangSharp Full-Scope Failure Buckets — SDL2.Core + SDL2_image

**Date:** 2026-05-21
**Inputs:**

- `output/reports/clangsharp-full.md` (21 failed headers, exit codes 1–21, plus two crashes)
- `output/clangsharp/Generated/core/*.g.cs` (partial outputs from the failed run)
- `artifacts/generated-bindings-preview/sdl2-core/` (Cake CppAst oracle, known-good shape)
- `build/_build/Targets/GenerateBindings/ModelBuilding/` (the policy that produced the oracle)
- `build/manifest.json` library_manifests[0].binding_generation (parse defines, deferrals)
- `spikes/binding-generators/references/ppy-SDL3-CS/SDL3-CS/SDL3/*.rsp`
- `spikes/binding-generators/references/ppy-SDL3-CS/SDL3-CS/SDL3/*.cs` (companion shape)

## Headline Correction (after reading every failed `.g.cs`)

**Exit codes from the failure report are misleading.** ClangSharp's exit code counts skipped macros and attribute warnings, not C# compile errors. Reading all 21 failed generated files and grepping for actual C#-invalid patterns shows:

| Class | Count | Headers |
| --- | ---: | --- |
| **True compile-blockers** (invalid C# syntax in output) | **4** | `SDL_assert`, `SDL_endian`, `SDL_cpuinfo`, `SDL_stdinc` |
| **Warning-only fails** (output is valid C#; exit code reflects skipped macros / `__attribute__` annotations) | **17** | `SDL_atomic`, `SDL_audio`, `SDL_error`, `SDL_events`, `SDL_gamecontroller`, `SDL_image`, `SDL_keycode`, `SDL_mouse`, `SDL_mutex`, `SDL_pixels`, `SDL_quit`, `SDL_shape`, `SDL_surface`, `SDL_thread`, `SDL_timer`, `SDL_version`, `SDL_video` |

Grep pattern used (all instances ⇒ syntactically invalid C#):

```text
public partial struct __AnonymousRecord  ← only as type body, not as attribute string
, swapper = new                          ← struct decl mid-expression
=\s*;\s*$                                ← empty rvalue
=\s*\(0\s*,\s*0\)\s*;                    ← C comma operator misinterpreted
extern \w+ __builtin_                    ← compiler intrinsic extern-declared
```

Files that hit those patterns: only `SDL_assert.g.cs`, `SDL_endian.g.cs`, `SDL_cpuinfo.g.cs`, `SDL_stdinc.g.cs`.

**Plus two files the failure report missed** (ClangSharp exit 0 but invalid C# output): `SDL_rect.g.cs` and `SDL_bits.g.cs` both emit `using static SDL2.int;` — a side-effect of `--remap SDL_bool=int` in `base.rsp:36`. The rename treats `int` as a SDL2-namespaced static class, then propagates it into every `using static` directive ClangSharp adds for cross-references to the (renamed) `SDL_bool` enum.

## Actual Build Trajectory (2026-05-21 compile test)

Verified the predictions above by running `dotnet build` on the spike output csproj across four quarantine configurations:

| Run | Quarantined files | Compile errors | What the errors prove |
| --- | --- | ---: | --- |
| Build 1 | 4 broken-syntax files (`SDL_assert`, `SDL_endian`, `SDL_cpuinfo`, `SDL_stdinc`) | **6** | All 6 errors are `using static SDL2.int;` in `SDL_rect.g.cs` and `SDL_bits.g.cs`. Confirms the `SDL_bool=int` remap side-effect; pattern was missed by the initial grep because `using static` doesn't match `= ;`. |
| Build 2 | + `SDL_rect.g.cs`, `SDL_bits.g.cs` | **161** | Quarantine artifact only: removing `SDL_rect.g.cs` removed `SDL_Rect`/`SDL_FPoint`/`SDL_Point` type definitions, cascading into `SDL_surface`, `SDL_video`, `SDL_render`, etc. as `CS0246` and `CS8500` (`SDL_Surface*` can't take pointer to managed type because its `clip_rect` field is unresolved). Not real generation breakage. |
| Build 3 | 4 broken-syntax files only; `SDL_rect.g.cs` restored with the bad `using static` line manually deleted | **14** | The real signal. Errors fall into exactly the three buckets the report predicted: (a) **8x** `CS0266 uint → int` in `SDL_pixels.g.cs:122-129` (FOURCC enum members overflow default int — fixed by `--with-type SDL_PixelFormatEnum=uint`); (b) **4x** `CS0246 HWND__/HDC__/HINSTANCE__` in `SDL_syswm.g.cs` (manifest-deferred `SDL_SysWMinfo`/`SDL_SysWMmsg`); (c) **2x** `CS0246 _iobuf` in `SDL_rwops.g.cs:77,109` (constitution-deferred `SDL_RWFromFP`). |
| Build 4 | + `SDL_syswm.g.cs`, `SDL_rwops.g.cs` + manual `: uint` patch on `SDL_pixels` enum | **55** | Quarantine artifact only: removing `SDL_rwops.g.cs` cascaded `SDL_RWops` references in `SDL_image`, `SDL_gesture`, `SDL_surface`. Not real breakage — the proper fix is to `--exclude SDL_RWFromFP` and let `SDL_RWops` itself stay generated. |

Net signal: **Build 3's 14 errors are the only artifact-free measurement.** Every one of them maps directly to an entry in the report's bucket table.

## Validated RSP Delta (sufficient to make SDL2.Core compile-clean)

Confirmed by Build 3's error trace, in priority order:

1. **`base.rsp`** (single edit, replaces the broken `--remap SDL_bool=int`):
   ```text
   --additional
   --undefine-macro=__has_builtin
   -fdeclspec
   --with-type
   SDL_bool=int
   ```
   This fix alone resolves: `SDL_rect.g.cs` + `SDL_bits.g.cs` (using-static directive), `SDL_stdinc.g.cs` (the `public enum @int` mess), `SDL_endian.g.cs` (`__builtin_bswap32`-style intrinsics), `SDL_cpuinfo.g.cs` (`_m_prefetch` intrinsic body), and parts of `SDL_atomic.g.cs`. Single most impactful change.

2. **`SDL_pixels.rsp`** (single new RSP):
   ```text
   --with-type
   SDL_PixelFormatEnum=uint
   ```
   Note SDL2 has `SDL_PixelFormatEnum` (enum tag) **separate** from `SDL_PixelFormat` (struct). ppy SDL3 patches the latter; we patch the former. Resolves Build 3's 8 uint→int cast errors.

3. **`SDL_assert.rsp`** (single new RSP):
   ```text
   --exclude
   SDL_FUNCTION SDL_FILE SDL_LINE SDL_NULL_WHILE_LOOP_CONDITION SDL_ASSERT_LEVEL
   SDL_TriggerBreakpoint SDL_AssertBreakpoint SDL_assert SDL_assert_release
   SDL_assert_paranoid SDL_assert_always SDL_disabled_assert SDL_enabled_assert
   __debugbreak
   ```

4. **`SDL_stdinc.rsp`** (start from ppy template + SDL2-specific additions):
   ```text
   --define-macro
   SDL_SLOW_MEMCPY SDL_SLOW_MEMMOVE SDL_SLOW_MEMSET SDL_DISABLE_ALLOCA
   --exclude
   SDL_size_mul_check_overflow SDL_size_add_check_overflow SDL_SIZE_MAX
   SDL_PRIs64 SDL_PRIu64 SDL_PRIx64 SDL_PRIX64
   SDL_memset4 _SDL_size_mul_overflow_builtin _SDL_size_add_overflow_builtin
   SDL_vsnprintf SDL_vsscanf SDL_vasprintf
   __builtin_mul_overflow __builtin_add_overflow
   ```

5. **`SDL_endian.rsp`** (defensive — base RSP `__has_builtin` undef may obviate this):
   ```text
   --exclude
   SDL_Swap16 SDL_Swap32 SDL_Swap64 SDL_SwapFloat
   SDL_SwapLE16 SDL_SwapLE32 SDL_SwapLE64
   SDL_SwapBE16 SDL_SwapBE32 SDL_SwapBE64
   _m_prefetch __builtin_prefetch __builtin_bswap16 __builtin_bswap32 __builtin_bswap64
   ```

6. **`SDL_bits.rsp`**:
   ```text
   --exclude
   SDL_MostSignificantBitIndex32 SDL_HasExactlyOneBitSet32 _BitScanReverse
   ```

7. **Cross-cutting deferrals** (add to family RSP `sdl2-core.rsp`):
   ```text
   --exclude
   SDL_SysWMinfo SDL_SysWMmsg SDL_GetWindowWMInfo
   SDL_RWFromFP
   SDL_DUMMY_ENUM
   SDL_LogMessageV
   ```

Total: 1 base RSP edit + 6 new per-header RSPs + 1 cross-cutting `--exclude` block. **No companion `.cs` required to compile.** Companion files become value-adds for public API parity (SDL_pixels macros, SDL_FOURCC helper, etc.) but are not blockers.

Files with `[NativeTypeName("__AnonymousRecord_...")]` and `[NativeTypeName("... __attribute__((cdecl))")]` (joystick, rwops, syswm, gamecontroller) carry the anonymous record/attribute names only as **attribute string content** — these are valid C# annotations, not type references.

This collapses the cost of the ppy-style SDL2.Core adaptation dramatically. The four real blockers are below; the seventeen warning-only fails go straight to the "verify by compile" pile.

## Three-Bucket Model

| Bucket | Meaning | ppy mechanism | Cake oracle mechanism |
| --- | --- | --- | --- |
| **RSP-only** | Parser config or symbol exclusion fixes the failure. No public-API loss because the symbol was never going to be a real public API (compiler magic, inline body, broken expansion). | per-header `.rsp` with `--exclude` / `--with-type` / `--additional --undefine-macro` | `MacroApiPolicy` (Macros/MacroApiPolicy.cs:21-50) classifies as `NonApi`/`Unsupported`; declaration translators just skip |
| **RSP + companion** | RSP fixes parse, but a real public-API helper (function-like macro, runtime-dependent constant, typedef-strengthened type) needs to be hand-written. | RSP excludes the symbol if ClangSharp also emits it; companion `.cs` writes the public form with `[Macro]` / `[Constant]` / `[Typedef]` markers; `generate_bindings.py` regex-feeds them back as `--exclude` / `--remap` | `MacroApiPolicy.PublicHelperMacroNames` (Macros/MacroApiPolicy.cs:53-75) + `MacroIntegerExpressionEvaluator` for computed constants + `BindingEnumTranslator` for int-backed enums |
| **Quarantine** | Stage 1 deferral, no companion intended yet. | RSP `--exclude` and that's it | `manifest.binding_generation.deferred_declarations` + `KnownUnsupportedDeclarationPolicy` (Declarations/KnownUnsupportedDeclarationPolicy.cs:26-79) |

## Cross-Cutting Base RSP Deltas

Single biggest gap in the current spike: `base.rsp` is missing the `__has_builtin` neutralization. Cake's `manifest.json:201` carries `clang_args: ["-fdeclspec", "-U__has_builtin"]`, ppy's `SDL_stdinc.rsp` carries the same. Without it, SDL_endian.h, SDL_stdinc.h, SDL_atomic.h all flow into the compiler-intrinsic detection branch and emit `__builtin_bswap32` / `__builtin_add_overflow` style nonsense.

Proposed additions to `spikes/binding-generators/clangsharp-style/rsp/base.rsp`:

```text
--additional
--undefine-macro=__has_builtin
-fdeclspec
```

Proposed **change** to the existing remap block (the current `SDL_bool=int` is the cause of `public enum @int { SDL_FALSE = 0, SDL_TRUE = 1 }` in `Generated/core/SDL_stdinc.g.cs:6-10`):

```text
# Remove from --remap (renames the type; produces enum @int):
#   SDL_bool=int
# Add to --with-type (sets underlying type; keeps the name):
--with-type
SDL_bool=int
```

Cake oracle for this exact decision: `build/_build/Targets/GenerateBindings/ModelBuilding/Types/TypeMappingPolicy.cs:57` puts `SDL_bool` in the explicit-typedef map and `BindingEnumTranslator.cs:42-44` forces int underlying via classification, **never** renaming. Result in oracle output: `artifacts/generated-bindings-preview/sdl2-core/Types/Enums.g.cs:1217-1221` emits the correct `public enum SDL_bool : int { SDL_FALSE = 0, SDL_TRUE = 1, }`.

## Per-Header Bucket Table

Headers ordered by failure severity. "Cake oracle" column points to the file where Cake's correct output for this header's content already lives.

| # | Header | Exit | Bucket | Root cause | RSP delta | Companion need | Cake oracle |
| --- | --- | --: | --- | --- | --- | --- | --- |
| 1 | `SDL_assert.h` | -1 (crash → 5 emitted-error symbols) | RSP-only | `SDL_FUNCTION = __FUNCTION__` expands to empty (`byte* SDL_FUNCTION = ;`), `SDL_FILE` expands to absolute Windows path leak, `SDL_NULL_WHILE_LOOP_CONDITION = (0, 0)` is C comma operator. See `Generated/core/SDL_assert.g.cs:66-77`. | `--exclude SDL_FUNCTION SDL_FILE SDL_LINE SDL_NULL_WHILE_LOOP_CONDITION SDL_ASSERT_LEVEL SDL_TriggerBreakpoint SDL_AssertBreakpoint SDL_assert SDL_assert_release SDL_assert_paranoid SDL_assert_always SDL_disabled_assert SDL_enabled_assert __debugbreak` | None — Cake `MacroApiPolicy.cs:97-107` classifies all of these `NonApi`. | `MacroApiPolicy.cs:97-99` (SDL_ASSERT_LEVEL, SDL_NULL_WHILE_LOOP_CONDITION as NonApi) |
| 2 | `SDL_stdinc.h` | -3 (unhandled exception) | **RSP + companion** | Multiple: (a) `SDL_bool=int` remap produces `enum @int` (`Generated/core/SDL_stdinc.g.cs:6-10`); (b) inline `SDL_memset4` body unrepresentable; (c) `__has_builtin` macros expand into compiler intrinsics; (d) varargs `SDL_iconv`, `SDL_qsort`, `SDL_bsearch` ok but `SDL_vsnprintf`/`SDL_vsscanf`/`SDL_vasprintf`/`SDL_LogMessageV` need exclusion; (e) `SDL_size_mul_check_overflow`/`SDL_size_add_check_overflow` differ Windows vs Unix. | `--with-type SDL_bool=int` (move from remap); `--additional --undefine-macro=__has_builtin` (already in cross-cutting); `--define-macro SDL_SLOW_MEMCPY SDL_SLOW_MEMMOVE SDL_SLOW_MEMSET SDL_DISABLE_ALLOCA` (from ppy `SDL_stdinc.rsp:5-9`); `--exclude SDL_size_mul_check_overflow SDL_size_add_check_overflow SDL_SIZE_MAX SDL_PRIs64 SDL_PRIu64 SDL_PRIx64 SDL_PRIX64 SDL_memset4 SDL_vsnprintf SDL_vsscanf SDL_vasprintf` | `SDL_FOURCC(A,B,C,D)` as `[Macro]` (used by SDL_PixelFormat enum members as bit-packed FOURCC — see `Generated/core/SDL_pixels.g.cs:122-129`). Optional `[Typedef] public enum SDL_DUMMY_ENUM` quarantine. | `manifest.json:254-257` defers `SDL_DUMMY_ENUM`; `MacroApiPolicy.PublicHelperMacroNames` does not list `SDL_FOURCC` but `MacroIntegerExpressionEvaluator` evaluates it inline for FOURCC pixel formats |
| 3 | `SDL_endian.h` | 18 | RSP-only | Inline `SDL_Swap16/32/64/SwapFloat` bodies port C struct-in-function syntax that isn't valid C# (`Generated/core/SDL_endian.g.cs:7-44`); `__builtin_bswap32` and `__builtin_prefetch` extern-declared as if they were SDL runtime symbols. | Base RSP `--additional --undefine-macro=__has_builtin` already covers most; add `--exclude SDL_Swap16 SDL_Swap32 SDL_Swap64 SDL_SwapFloat SDL_SwapLE16 SDL_SwapLE32 SDL_SwapLE64 SDL_SwapBE16 SDL_SwapBE32 SDL_SwapBE64 _m_prefetch __builtin_prefetch __builtin_bswap16 __builtin_bswap32 __builtin_bswap64` | None — SDL_BYTEORDER/LIL/BIG_ENDIAN constants already emit correctly. | `Constants.g.cs` already has `SDL_BIG_ENDIAN`, `SDL_BYTEORDER`, `SDL_LIL_ENDIAN` (file lines confirmed via Constants.g.cs:37-59) |
| 4 | `SDL_pixels.h` | 13 | **RSP + companion** | Multiple: (a) `SDL_PixelFormatEnum` (enum tag, NOT `SDL_PixelFormat`!) underlying type defaults to int but values overflow (`(32 << 8)` etc.); (b) `SDL_PIXELFORMAT_RGBA32 = SDL_PIXELFORMAT_ABGR8888` hardcoded little-endian (wrong on big-endian); (c) function-like macros `SDL_DEFINE_PIXELFOURCC`, `SDL_PIXELTYPE`, `SDL_PIXELORDER`, `SDL_PIXELLAYOUT`, `SDL_BITSPERPIXEL`, `SDL_BYTESPERPIXEL`, `SDL_ISPIXELFORMAT_*` need helper representation. **SDL2 vs SDL3:** ppy `SDL_pixels.rsp` does `SDL_PixelFormat=uint` because SDL3 collapsed enum and typedef; SDL2 has `SDL_PixelFormat` as a **struct** (`Generated/core/SDL_pixels.g.cs:159-221`) — do NOT remap it. | `--with-type SDL_PixelFormatEnum=uint` (NOT SDL_PixelFormat); `--with-type SDL_Colorspace=uint` (if header exposes it in SDL2); `--exclude SDL_PIXELFORMAT_RGBA32 SDL_PIXELFORMAT_ARGB32 SDL_PIXELFORMAT_BGRA32 SDL_PIXELFORMAT_ABGR32 SDL_PIXELFORMAT_RGBX32 SDL_PIXELFORMAT_XRGB32 SDL_PIXELFORMAT_BGRX32 SDL_PIXELFORMAT_XBGR32 SDL_DEFINE_PIXELFOURCC SDL_DEFINE_PIXELFORMAT SDL_PIXELFLAG SDL_PIXELTYPE SDL_PIXELORDER SDL_PIXELLAYOUT SDL_BITSPERPIXEL SDL_BYTESPERPIXEL SDL_ISPIXELFORMAT_INDEXED SDL_ISPIXELFORMAT_PACKED SDL_ISPIXELFORMAT_ARRAY SDL_ISPIXELFORMAT_ALPHA SDL_ISPIXELFORMAT_FOURCC` | All ten `SDL_*32` runtime-endian constants as `[Constant] public static readonly` using `BitConverter.IsLittleEndian` ternary (ppy companion `SDL_pixels.cs:149-171` is the template); helper macros from `MacroApiPolicy.PublicHelperMacroNames` (Macros/MacroApiPolicy.cs:62-74) as `[Macro]` methods. Largest companion in the suite. | `MacroApiPolicy.cs:62-74` lists all the helper-candidate macro names; `MacroIntegerExpressionEvaluator` computes the bit-packed format constants from C definitions |
| 5 | `SDL_mutex.h` | 21 | RSP-only | High error count likely from inline `SDL_LockMutex`, `SDL_UnlockMutex` if SDL2 inlines them, plus `SDL_MUTEX_TIMEDOUT`, opaque-handle chain. Need to read the partial g.cs to confirm specifics — out of scope for this report. | `--exclude SDL_LockMutex SDL_UnlockMutex` if they appear; otherwise probably `--exclude` for the inline body symbols only. Verify by reading `Generated/core/SDL_mutex.g.cs` before finalizing. | None expected — mutex API is straight P/Invoke. | `TypeMappingPolicy` opaque handle path emits `SDL_mutex*` → `IntPtr` correctly when not blocked by inline bodies |
| 6 | `SDL_audio.h` | 8 | RSP-only (companion optional) | `SDL_AudioFormat` is `typedef Uint16`; ClangSharp may default int. ppy `SDL_audio.rsp` does `--with-type SDL_AudioFormat=uint` for SDL3 but SDL2 typedef is **Uint16**. SDL_AudioCVT has `__attribute__((packed))` that ClangSharp may mishandle. | `--with-type SDL_AudioFormat=ushort` (SDL2-specific — Uint16, not uint!); `--exclude` any inline format helpers. | Optional `SDL_AUDIO_BITSIZE(x)`, `SDL_AUDIO_ISFLOAT(x)`, etc. function-like macros as `[Macro]` later. | `TypeMappingPolicy.MapTypedef` chain-resolves Uint16→ushort, confirmed in oracle |
| 7 | `SDL_video.h` | 4 | RSP-only | `SDL_WINDOWPOS_UNDEFINED_DISPLAY(X)`, `SDL_WINDOWPOS_CENTERED_DISPLAY(X)`, `SDL_WINDOWPOS_ISUNDEFINED(X)`, `SDL_WINDOWPOS_ISCENTERED(X)` function-like macros. | `--exclude SDL_WINDOWPOS_UNDEFINED_DISPLAY SDL_WINDOWPOS_CENTERED_DISPLAY SDL_WINDOWPOS_ISUNDEFINED SDL_WINDOWPOS_ISCENTERED` | Optional later: all four as `[Macro]` (Cake puts them in `PublicHelperMacroNames` Macros/MacroApiPolicy.cs:59-61). | `MacroApiPolicy.cs:59-61` |
| 8 | `SDL_cpuinfo.h` | 4 | RSP-only | `SDL_CACHELINE_SIZE` is C-only padding macro (Cake `MacroApiPolicy.cs:97` marks NonApi); inline `SDL_HasMMX`-style helpers if present; possibly intrinsic-detection paths. | `--exclude SDL_CACHELINE_SIZE` plus any inline detection helpers. | None. | `MacroApiPolicy.cs:97` |
| 9 | `SDL_error.h` | 3 | RSP-only | `SDL_OutOfMemory()`, `SDL_Unsupported()`, `SDL_InvalidParamError(p)` are macros expanding to `SDL_SetError(fmt, ...)` with `__arglist`. Cake skips them as unsupported function-like macros. ppy writes them in companion (`SDL_error.cs:8-21`). | `--exclude SDL_OutOfMemory SDL_Unsupported SDL_InvalidParamError` | Optional later: copy ppy `SDL_error.cs` shape. | `MacroApiPolicy.cs:34-39` (function-like macros not in helper list → Unsupported) |
| 10 | `SDL_version.h` | 3 | RSP-only | `SDL_VERSION(x)`, `SDL_VERSIONNUM(X,Y,Z)`, `SDL_VERSION_ATLEAST(X,Y,Z)`, `SDL_COMPILEDVERSION` function-like macros. | `--exclude SDL_VERSION SDL_VERSIONNUM SDL_VERSION_ATLEAST SDL_COMPILEDVERSION` | Optional later: all four as `[Macro]` (Cake `PublicHelperMacroNames` lists them). | `MacroApiPolicy.cs:56-58` |
| 11 | `SDL_surface.h` | 3 | RSP-only | `SDL_MUSTLOCK(S)` function-like macro; `SDL_LoadBMP(file)` and `SDL_SaveBMP(surface, file)` are macros that call the `_RW` variants with `SDL_RWFromFile`. | `--exclude SDL_MUSTLOCK SDL_LoadBMP SDL_SaveBMP` | Optional `[Macro] SDL_MUSTLOCK` later. | `MacroApiPolicy.cs:34-39` classifies these as Unsupported function-like; the `_RW` variants are the public API |
| 12 | `SDL_thread.h` | 2 | RSP-only | `SDL_CreateThread`/`SDL_CreateThreadWithStackSize` Windows-side macros that route through `SDL_CreateThread_runtime` with `_beginthreadex`. C `long`-typed `SDL_threadID` (Cake handles via `CLong` policy, ClangSharp default falls through). | `--exclude SDL_CreateThread SDL_CreateThreadWithStackSize` (the macro forms); `--with-type SDL_threadID=long` as defensive default until C `long` policy is wired. | `[Typedef] public enum SDL_threadID : long` (Stage 1 placeholder; constitution L213 calls SDL2 `SDL_threadID` C `long`). Net stretch. | `TypeMappingPolicy.MapPrimitive` L153-154 maps C `long` → `CLong`; constitution §"C `long` And `unsigned long`" |
| 13 | `SDL_image.h` (SDL2_image) | 2 | RSP-only | `IMG_Load(file)`, `IMG_Load_RW(src, freesrc, type)` macro variants; `IMG_LinkedVersion` returns version struct. | `--exclude IMG_Load IMG_SaveJPG IMG_SavePNG IMG_LoadTyped_RW` (preserve `_RW` form raw; macro shortcuts excluded) | Optional later: `[Macro] IMG_Load(string file)` overload helper. | N/A (SDL2_image not yet enabled in Cake — `manifest.json:283` has `enabled: false`) |
| 14 | `SDL_events.h` | 1 | RSP-only | Likely a single `SDL_GetEventState`/`SDL_QuitRequested`-equivalent macro, or an anonymous-union field in SDL_Event. Need to verify by reading the generated file. | TBD (read `Generated/core/SDL_events.g.cs` first) | None expected. | `Structs.g.cs` already emits SDL_Event union correctly in Cake oracle |
| 15 | `SDL_gamecontroller.h` | 1 | RSP-only | Likely one inline detection helper or one function-like macro. Need to verify. | TBD | None. | — |
| 16 | `SDL_keycode.h` | 1 | RSP-only | Likely `SDLK_*` definitions that route through `SDL_SCANCODE_TO_KEYCODE(X)` function-like macro. | `--exclude SDL_SCANCODE_TO_KEYCODE` | None — SDLK_* enum-value emission stays. | `BindingEnumTranslator.FormatValue` evaluates the SDL_SCANCODE_TO_KEYCODE expression for each member |
| 17 | `SDL_mouse.h` | 1 | RSP-only | `SDL_BUTTON(X)` function-like macro alone; the dependent `SDL_BUTTON_LMASK` etc. **already emit correctly** (`Generated/core/SDL_mouse.g.cs:104-117`). | `--exclude SDL_BUTTON` | Optional `[Macro] SDL_BUTTON(int x)` later. | `MacroApiPolicy.cs:55` lists SDL_BUTTON as helper-candidate |
| 18 | `SDL_quit.h` | 1 (file is 0 bytes) | RSP-only | `SDL_QuitRequested()` macro that calls `SDL_PumpEvents()` + `SDL_PeepEvents(...)` — unrepresentable as constant macro. | `--exclude SDL_QuitRequested` | Optional `[Macro] SDL_QuitRequested()` helper later. | `MacroApiPolicy.cs:34-39` |
| 19 | `SDL_shape.h` | 1 | RSP-only | `SDL_SHAPEMODEALPHA(mode)` macro. | `--exclude SDL_SHAPEMODEALPHA` | None. | `MacroApiPolicy.cs:34-39` |
| 20 | `SDL_timer.h` | 1 | RSP-only | Generated output is **clean** (`Generated/core/SDL_timer.g.cs`) — exit 1 likely benign warning. `SDL_TICKS_PASSED(A,B)` macro is the only function-like candidate. | `--exclude SDL_TICKS_PASSED` | None. | — |
| 21 | `SDL_atomic.h` | 6 | RSP-only | Inline `SDL_AtomicGet`, `SDL_AtomicSet`, `SDL_AtomicCAS`, `SDL_AtomicLock`, `SDL_AtomicUnlock` bodies; `SDL_CompilerBarrier` macro. | Mostly `--additional --undefine-macro=__has_builtin` (base) plus `--exclude SDL_AtomicGet SDL_AtomicSet SDL_AtomicCAS SDL_AtomicGetPtr SDL_AtomicSetPtr SDL_AtomicCASPtr SDL_AtomicLock SDL_AtomicUnlock SDL_AtomicTryLock SDL_CompilerBarrier SDL_MemoryBarrierAcquireFunction SDL_MemoryBarrierReleaseFunction` | None — atomic API isn't part of the public typed surface in Cake oracle either. | Cake `KnownUnsupportedDeclarationPolicy` / manifest deferral path (some are caught by `BindableDeclarationPolicy` not in this read) |

## Quarantine List (Stage 1 deferral)

These come from `manifest.json:245-258` + constitution §"Current accepted deferrals":

```text
--exclude
SDL_SysWMinfo SDL_SysWMmsg
SDL_DUMMY_ENUM
SDL_RWFromFP
SDL_LogMessageV SDL_vsnprintf SDL_vsscanf SDL_vasprintf
```

`SDL_RWops` itself stays parsed but emitted opaque per constitution L286-289. ClangSharp will emit a struct; we'll need a follow-up `--exclude SDL_RWops` if the struct shape leaks.

## Estimated Cost — ppy-Style SDL2.Core (Revised)

After the audit, the real cost shape is:

| Category | Files | Notes |
| --- | ---: | --- |
| **Base RSP delta** (required) | 1 | `--additional --undefine-macro=__has_builtin -fdeclspec` + replace `--remap SDL_bool=int` with `--with-type SDL_bool=int`. Alone this likely fixes `SDL_endian`, `SDL_cpuinfo`, large parts of `SDL_stdinc`. |
| **Per-header RSP** (required for the 4 true blockers) | 4 | `SDL_assert.rsp` (exclude FUNCTION/FILE/LINE/NULL_WHILE_LOOP_CONDITION + the 5 assert-family macros), `SDL_stdinc.rsp` (ppy template + memset/varargs excludes + `SDL_size_*_overflow` excludes), `SDL_endian.rsp` (exclude inline swap helpers + `__builtin_*`), `SDL_cpuinfo.rsp` (exclude `_m_prefetch` + `__builtin_prefetch` + `SDL_CACHELINE_SIZE`). |
| **Companion `.cs`** (mandatory for public-API completeness, not for compile) | **1 mandatory + ~6 optional** | Mandatory: `SDL_pixels.cs` (10 endian-aware constants + 14 helper macros — ppy `SDL_pixels.cs` is the template). Optional ergonomy: `SDL_stdinc.cs` (SDL_FOURCC + SDLBool-equivalent if we want bool conversion), `SDL_version.cs` (4 version helpers), `SDL_mouse.cs` (SDL_BUTTON), `SDL_error.cs` (3 macros), `SDL_quit.cs` (SDL_QuitRequested), `SDL_surface.cs` (SDL_MUSTLOCK). None of these are needed to compile. |
| `generate_bindings.py` enhancements | 1 | Add ppy-style `[Constant]` → `--exclude` and `[Typedef]` → `--remap` regex feedback. Needed only when first companion lands. |
| Quarantine RSP entries | (folded into base) | `--exclude SDL_SysWMinfo SDL_SysWMmsg SDL_DUMMY_ENUM SDL_RWFromFP SDL_LogMessageV SDL_vsnprintf SDL_vsscanf SDL_vasprintf` |

**Minimum to make SDL2.Core compile**: 1 base RSP edit + 4 per-header RSP files. No companion required for compile-clean output.

**Minimum to match the Cake oracle's public API**: above + 1 mandatory companion (`SDL_pixels.cs`).

Compare with Cake `build/_build/Targets/GenerateBindings/ModelBuilding/` which currently runs ~30 policy files to produce the same result.

## Quick Compile-Test Strategy (Proposed)

Before doing more bucket archaeology, this is the fastest way to validate the report's thesis:

1. Temporarily move the 4 broken-syntax files out of the spike output compile path:
   ```pwsh
   New-Item -ItemType Directory -Force spikes/binding-generators/output/clangsharp/Generated/_quarantined-for-testing
   Move-Item spikes/binding-generators/output/clangsharp/Generated/core/SDL_assert.g.cs `
             spikes/binding-generators/output/clangsharp/Generated/core/SDL_endian.g.cs `
             spikes/binding-generators/output/clangsharp/Generated/core/SDL_cpuinfo.g.cs `
             spikes/binding-generators/output/clangsharp/Generated/core/SDL_stdinc.g.cs `
             spikes/binding-generators/output/clangsharp/Generated/_quarantined-for-testing/
   ```
2. Build:
   ```pwsh
   dotnet build spikes/binding-generators/output/clangsharp/Janset.Sdl2.ClangSharpSpike.Output.csproj -c Release
   ```
3. Outcomes:
   - **Build succeeds**: thesis confirmed. Remaining work is exactly the table above (1 base RSP + 4 per-header RSPs).
   - **Build fails**: the new errors are the *real* signal — cross-file references, duplicate symbols, missing types. Bucket those next.
4. Restore the four files when the run is done; the RSP fixes will regenerate them clean.

This costs ~5 minutes and replaces speculation with evidence.

## Multi-TFM Footnote (2026-05-21 verification)

The "0 errors" results above are for `net10.0` only. A follow-up build with `<TargetFrameworks>net10.0;net9.0;net8.0;netstandard2.0;net462</TargetFrameworks>` produces **474 errors** on the ClangSharp output, concentrated in `netstandard2.0` and `net462`. The constructs are not signature-level — they are baked into struct definitions and member initializers:

- `[InlineArray(N)]` / `InlineArrayAttribute` (C# 12, net8+ only).
- `ReadOnlySpan<byte>` UTF-8 literal returns (no netstandard2.0/net462 polyfill in the spike).
- `delegate* unmanaged[Cdecl]<...>` field types in structs (net5+ language feature, no fallback).

Gating these with `#if` requires either dropping the API on old TFMs or emitting alternate shapes in an `#else` branch the spike does not produce. ppy SDL3-CS targets modern TFMs only; if SDL2 multi-TFM is binding (constitution §"Evidence Gates" L379), this approach needs an additional post-processing pass or output-mode split (`DllImport.g.cs` vs `LibraryImport.g.cs` with file-level guards, per constitution §"Layer Contract" L48).

See `iteration-2-comparison.md` §Multi-TFM Compatibility for the side-by-side comparison against Alimer-style (12 errors, single-line emitter fix).

## Decision-Quality Signal

What this report proves:

- **Real compile-blockers are 4, not 21**. ppy's adaptation effort for SDL2.Core is much smaller than the raw failure count implies.
- **0 of 21 require manual P/Invoke emission**. Companion files are pure public-layer (macros, runtime constants, ergonomic types). Confirms the "ClangSharp ABI is cheap, public layer is owned" thesis from the previous session.
- **Single biggest gap in the current spike**: `__has_builtin` is not undefined. That one parser flag alone is responsible for `SDL_endian`, `SDL_cpuinfo`, and the `_SDL_size_*_overflow_builtin` portions of `SDL_stdinc`.
- **SDL_bool remap is the second biggest gap**. `--remap SDL_bool=int` (rename) is the wrong knob; `--with-type SDL_bool=int` (underlying type) is the right one — the same shape Cake's `TypeMappingPolicy.cs:57` enforces in code.

## What This Does Not Decide

This report does not pick ppy vs Alimer. It only quantifies the ppy adaptation cost for SDL2.Core's known failures. Comparable Alimer-side bucketing should follow when `output/reports/alimer-full.md` exists. Both need to land before the spike charter (`docs/generator-spike-goals.md`) allows a toolchain decision.
