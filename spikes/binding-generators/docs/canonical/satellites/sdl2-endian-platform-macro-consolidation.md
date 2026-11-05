# SDL2 Endian and Platform-Computed Macro Consolidation

**Date:** 2026-05-28
**Status:** Research artifact. Feeds macro policy, Layer 2 public typed API, and future platform-generation design.
**Scope:** SDL2 Core plus satellites where object-like macros depend on byte order, platform selection, or other preprocessor-computed state. This is separate from function-like macro helpers and satellite error-function redirects.

---

## 1. Core Finding

ClangSharp value-macro generation evaluates macros through the current generation host. It preserves the original macro spelling only in `[NativeTypeName("#define ...")]`; the generated C# initializer is the evaluated value from the parse environment.

On Windows-local `x64-windows-hybrid` generation, this means little-endian and Windows-conditioned macros become concrete constants:

```csharp
[NativeTypeName("#define MIX_DEFAULT_FORMAT AUDIO_S16SYS")]
public const int MIX_DEFAULT_FORMAT = 0x8010;
```

This is correct for today's supported RIDs because all current targets are little-endian: `win-x64`, `win-x86`, `win-arm64`, `linux-x64`, `linux-arm64`, `osx-x64`, and `osx-arm64`. It is still source-semantic drift: the C headers describe platform/endian-dependent aliases, while Layer 1 currently records one evaluated host view.

---

## 2. Header and Generated Output Scan

### 2.1 Audio `*SYS` Macros

`SDL_audio.h` selects system-endian audio formats from `SDL_BYTEORDER`:

```c
#if SDL_BYTEORDER == SDL_LIL_ENDIAN
#define AUDIO_U16SYS    AUDIO_U16LSB
#define AUDIO_S16SYS    AUDIO_S16LSB
#define AUDIO_S32SYS    AUDIO_S32LSB
#define AUDIO_F32SYS    AUDIO_F32LSB
#else
#define AUDIO_U16SYS    AUDIO_U16MSB
#define AUDIO_S16SYS    AUDIO_S16MSB
#define AUDIO_S32SYS    AUDIO_S32MSB
#define AUDIO_F32SYS    AUDIO_F32MSB
#endif
```

Generated Core output currently emits little-endian constants:

```csharp
[NativeTypeName("#define AUDIO_S16SYS AUDIO_S16LSB")]
public const int AUDIO_S16SYS = 0x8010;
```

Evidence:

- `vcpkg_installed/x64-windows-hybrid/include/SDL2/SDL_audio.h:123-132`
- `spikes/binding-generators/clangsharp/src/Janset.SDL2.Core/Generated/Compat/SDL_audio.g.cs`
- `spikes/binding-generators/clangsharp/src/Janset.SDL2.Core/Generated/Modern/SDL_audio.g.cs`

### 2.2 Mixer `MIX_DEFAULT_FORMAT`

`SDL_mixer.h` defines:

```c
#define MIX_DEFAULT_FORMAT      AUDIO_S16SYS
```

Generated Mixer output emits the evaluated little-endian value:

```csharp
[NativeTypeName("#define MIX_DEFAULT_FORMAT AUDIO_S16SYS")]
public const int MIX_DEFAULT_FORMAT = 0x8010;
```

Evidence:

- `vcpkg_installed/x64-windows-hybrid/include/SDL2/SDL_mixer.h:224`
- `spikes/binding-generators/clangsharp/src/Janset.SDL2.Mixer/Generated/Compat/SDL_mixer.g.cs`
- `spikes/binding-generators/clangsharp/src/Janset.SDL2.Mixer/Generated/Modern/SDL_mixer.g.cs`

### 2.3 Endian Constants

`SDL_endian.h` derives `SDL_BYTEORDER` and `SDL_FLOATWORDORDER` from compiler/platform macros. Windows-local generation emits:

```csharp
[NativeTypeName("#define SDL_BYTEORDER SDL_LIL_ENDIAN")]
public const int SDL_BYTEORDER = 1234;

[NativeTypeName("#define SDL_FLOATWORDORDER SDL_BYTEORDER")]
public const int SDL_FLOATWORDORDER = 1234;
```

Evidence:

- `vcpkg_installed/x64-windows-hybrid/include/SDL2/SDL_endian.h:58-117`
- `spikes/binding-generators/clangsharp/src/Janset.SDL2.Core/Generated/Compat/SDL_endian.g.cs`
- `spikes/binding-generators/clangsharp/src/Janset.SDL2.Core/Generated/Modern/SDL_endian.g.cs`

### 2.4 Pixel Format `*32` Aliases

`SDL_pixels.h` maps `SDL_PIXELFORMAT_RGBA32`, `ARGB32`, `BGRA32`, and `ABGR32` differently on big-endian vs little-endian systems. Windows-local generation captures the little-endian branch.

Evidence:

- `vcpkg_installed/x64-windows-hybrid/include/SDL2/SDL_pixels.h:284-301`
- `spikes/binding-generators/clangsharp/src/Janset.SDL2.Core/Generated/Compat/SDL_pixels.g.cs`
- `spikes/binding-generators/clangsharp/src/Janset.SDL2.Core/Generated/Modern/SDL_pixels.g.cs`

### 2.5 Platform Macros

`SDL_platform.h` emits platform identity macros based on the parse environment. Windows-local generation emits Windows macros such as `__WINDOWS__` and `__WIN32__`.

Evidence:

- `vcpkg_installed/x64-windows-hybrid/include/SDL2/SDL_platform.h:152-201`
- `spikes/binding-generators/clangsharp/src/Janset.SDL2.Core/Generated/Compat/SDL_platform.g.cs`
- `spikes/binding-generators/clangsharp/src/Janset.SDL2.Core/Generated/Modern/SDL_platform.g.cs`

The endian cases are low risk for current supported RIDs. The platform macro cases are more likely to confuse consumers if surfaced as runtime truth on Linux/macOS.

---

## 3. Peer Comparison

### 3.1 SDL2-CS

SDL2-CS hand-authors runtime-endian values:

```csharp
public static readonly ushort MIX_DEFAULT_FORMAT =
    BitConverter.IsLittleEndian ? SDL.AUDIO_S16LSB : SDL.AUDIO_S16MSB;
```

It also defines `AUDIO_*SYS` in Core using `BitConverter.IsLittleEndian`.

Evidence:

- `external/sdl2-cs/src/SDL2.cs`
- `external/sdl2-cs/src/SDL2_mixer.cs:61-64`

### 3.2 ppy/SDL3-CS

ppy SDL3-CS also hand-authors runtime-endian audio aliases. Its Mixer default format aliases the Core audio default instead of hard-coding a ClangSharp-evaluated integer.

Evidence:

- `spikes/binding-generators/references/ppy-SDL3-CS/SDL3-CS/SDL3/SDL_audio.cs`
- `spikes/binding-generators/references/ppy-SDL3-CS/SDL3_mixer-CS/SDL3_mixer/SDL_mixer.cs`

### 3.3 Silk.NET

No useful local or web Silk.NET SDL2_mixer precedent was found for `MIX_DEFAULT_FORMAT`. Available Silk.NET SDL references are core-oriented and do not provide a satellite Mixer comparison.

### 3.4 Direct ClangSharp-Style Peers

Direct ClangSharp-generated SDL bindings tend to emit evaluated constants. That matches current Layer 1 behavior, but it does not preserve runtime-endian source semantics.

---

## 4. ClangSharp Capability Check

ClangSharp 17.0.4 does not appear to provide a macro-expression-preservation option for generated C# initializers.

Observed capabilities:

- `generate-macro-bindings` emits value-like macros only.
- `--define-macro`, `--undefine-macro`, and `--additional` can change the parse environment.
- `--exclude` can suppress selected macro bindings.
- `--with-type` and `--with-attribute` can alter generated declarations.
- No discovered option preserves source macro expressions as C# aliases or runtime conditionals.

Likely root cause: ClangSharp macro handling reparses macro bodies as generated C/C++ declarations and emits the resulting `VarDecl`, so aliases and conditional macros are already evaluated before C# emission. The original spelling survives only in `[NativeTypeName]`.

---

## 5. Recommended Treatment

### 5.1 Layer 1 Raw ABI

Keep current evaluated constants as Layer 1 raw evidence for the current RID set. Do not block Item 5 on replacing `MIX_DEFAULT_FORMAT`; all supported RIDs are little-endian today.

Do not add piecemeal RSP excludes for these macros. Excluding object-like constants would hide useful raw evidence and repeat the `MIX_MAJOR_VERSION` mistake in a different costume.

### 5.2 Macro Policy / Postprocess Follow-up

Create a dedicated macro policy pass or report classifier for object-like macros whose `[NativeTypeName]` contains platform/endian aliases:

- Audio system-endian aliases: `AUDIO_U16SYS`, `AUDIO_S16SYS`, `AUDIO_S32SYS`, `AUDIO_F32SYS`.
- Mixer default format: `MIX_DEFAULT_FORMAT`.
- Endian constants: `SDL_BYTEORDER`, `SDL_FLOATWORDORDER`.
- Pixel aliases: `SDL_PIXELFORMAT_*32`.
- Platform identity macros from `SDL_platform.h`.

The policy should decide which generated constants remain accepted host-evaluated values and which receive public runtime-aware aliases in Layer 2.

### 5.3 Layer 2 Public API

For user-facing APIs, prefer SDL2-CS / ppy-style runtime-endian aliases where semantics matter:

```csharp
public static readonly ushort MIX_DEFAULT_FORMAT =
    BitConverter.IsLittleEndian ? AUDIO_S16LSB : AUDIO_S16MSB;
```

Equivalent treatment may be needed for `AUDIO_*SYS` and `SDL_PIXELFORMAT_*32`. Platform identity macros should probably not be projected as runtime platform truth without a deliberate policy.

---

## 6. Open Decisions

| Decision | Options | Recommendation |
| --- | --- | --- |
| Raw Layer 1 constants | Keep evaluated constants vs exclude computed macros | Keep evaluated constants and report the source expression. |
| Runtime-endian aliases | Layer 2 public properties vs generated postprocess replacement | Prefer Layer 2 aliases/properties so raw output remains auditable. |
| Big-endian support | Declare unsupported vs generate per-target constants | Current roadmap can declare all supported RIDs little-endian; revisit only if a big-endian RID is added. |
| Platform macros | Emit as raw constants vs suppress from public projection | Do not treat Windows-local generated platform macros as runtime truth on consumers. |
| Automation | Hardcoded list vs expression classifier over `[NativeTypeName]` | Start with a small explicit classifier; avoid magic global rewrites. |

---

## 7. References

- `vcpkg_installed/x64-windows-hybrid/include/SDL2/SDL_audio.h:123-132` — `AUDIO_*SYS` endian mapping.
- `vcpkg_installed/x64-windows-hybrid/include/SDL2/SDL_mixer.h:224` — `MIX_DEFAULT_FORMAT` alias.
- `vcpkg_installed/x64-windows-hybrid/include/SDL2/SDL_endian.h:58-117` — endian macro derivation.
- `vcpkg_installed/x64-windows-hybrid/include/SDL2/SDL_pixels.h:284-301` — endian-conditioned pixel aliases.
- `vcpkg_installed/x64-windows-hybrid/include/SDL2/SDL_platform.h:152-201` — platform identity macros.
- `external/sdl2-cs/src/SDL2.cs` — SDL2-CS runtime-endian audio aliases.
- `external/sdl2-cs/src/SDL2_mixer.cs:61-64` — SDL2-CS `MIX_DEFAULT_FORMAT` runtime-endian expression.
- `spikes/binding-generators/references/ppy-SDL3-CS/SDL3-CS/SDL3/SDL_audio.cs` — ppy runtime-endian audio aliases.
- `spikes/binding-generators/references/ppy-SDL3-CS/SDL3_mixer-CS/SDL3_mixer/SDL_mixer.cs` — ppy Mixer default format alias.
- [`sdl2-function-like-macro-consolidation.md`](sdl2-function-like-macro-consolidation.md) — function-like macro helper taxonomy.
- [`sdl2-satellite-error-function-consolidation.md`](sdl2-satellite-error-function-consolidation.md) — satellite error macro redirect policy.
