# ClangSharp SDL.h Required Surface Design

**Date:** 2026-05-22
**Status:** Accepted design; implementation plan pending.
**Scope:** Spike-local ClangSharp prototype under `spikes/binding-generators/clangsharp/`.

## Goal

Fix oracle repair queue item B for the ClangSharp spike: recover the required SDL2 Core initialization surface declared only in the excluded umbrella header `SDL.h`, without parsing `SDL.h` as a normal ClangSharp generation unit and without using `build/manifest.json` as the generation source.

The recovered surface is intentionally narrow:

- functions: `SDL_Init`, `SDL_InitSubSystem`, `SDL_QuitSubSystem`, `SDL_WasInit`, `SDL_Quit`;
- constants: `SDL_INIT_TIMER`, `SDL_INIT_AUDIO`, `SDL_INIT_VIDEO`, `SDL_INIT_JOYSTICK`, `SDL_INIT_HAPTIC`, `SDL_INIT_GAMECONTROLLER`, `SDL_INIT_EVENTS`, `SDL_INIT_SENSOR`, `SDL_INIT_NOPARACHUTE`, `SDL_INIT_EVERYTHING`.

## Context

The A-slice made generated raw ABI containers internal and moved SDL2_image output to `SDL2.Image`. The remaining high-signal Core gap is the SDL2 initialization surface.

SDL2 differs from ppy's SDL3 reference here. ppy avoids the SDL3 umbrella `SDL.h`, but SDL3 has a leaf `SDL_init.h` that contains the init/quit functions and init flag constants. SDL2 has no equivalent leaf header; the five init/quit declarations and ten `SDL_INIT_*` macros live directly in the umbrella `SDL.h`.

The repository intentionally excludes SDL2 `SDL.h` from normal generation because it includes the full SDL2 header set and would collapse the per-header parse model into one translation unit. That reintroduces duplicated declarations, platform-conditioned branches, and intrinsic-header failures that per-header parsing avoids. The B-slice should therefore recover only the required `SDL.h` surface, not broaden the parsed header set.

## Decision

Use a spike-local curated required-surface file as the generator's allowlist, then extract the constrained signatures and values from the installed `SDL.h` and validate against the manifest-required surface.

Recommended file:

```text
spikes/binding-generators/scope/sdl2-core-sdlh-required.json
```

The file lists the owning family, source header, functions, and constants. It decides what the spike is allowed to recover. The installed `SDL.h` decides the signatures and macro values for those allowed names. `build/manifest.json` remains validation and oracle evidence, not generation input.

This gives the spike a local experiment boundary while avoiding two quiet truths. If the curated file, installed `SDL.h`, and manifest-required surface disagree, the B-slice should fail closed or produce an explicit hard oracle finding.

## Non-Goals

- Do not parse or generate the full SDL2 `SDL.h` umbrella header.
- Do not generate APIs from headers included by `SDL.h`.
- Do not add public typed wrappers or friendly overloads.
- Do not add a public `SDL_InitFlags` enum yet; that belongs to the typed public layer.
- Do not touch production generator code, production `src/` output, package projects, CI, vcpkg config, or manifest schema.
- Do not add satellite required-symbol recovery unless a later red test proves a satellite has an umbrella-only declaration gap.

## Design

### Spike-Local Required Surface

Create a small JSON allowlist for SDL2 Core `SDL.h` recovery. The generator reads only this file for B-slice emission.

Expected logical content:

```json
{
  "family": "sdl2-core",
  "header": "SDL.h",
  "functions": [
    "SDL_Init",
    "SDL_InitSubSystem",
    "SDL_QuitSubSystem",
    "SDL_WasInit",
    "SDL_Quit"
  ],
  "constants": [
    "SDL_INIT_TIMER",
    "SDL_INIT_AUDIO",
    "SDL_INIT_VIDEO",
    "SDL_INIT_JOYSTICK",
    "SDL_INIT_HAPTIC",
    "SDL_INIT_GAMECONTROLLER",
    "SDL_INIT_EVENTS",
    "SDL_INIT_SENSOR",
    "SDL_INIT_NOPARACHUTE",
    "SDL_INIT_EVERYTHING"
  ]
}
```

The JSON is intentionally name-based. The implementation should parse the installed `SDL.h` for those names only and use the parsed declaration signatures / macro values when emitting C#. The important rule is that the output set is driven by the spike-local allowlist, not by a broad `SDL.h` generation pass.

### Generated Output

Emit a generated file for both backends:

```text
spikes/binding-generators/clangsharp/src/Janset.SDL2.Core/Generated/Compat/SDL_required.g.cs
spikes/binding-generators/clangsharp/src/Janset.SDL2.Core/Generated/Modern/SDL_required.g.cs
```

Both files should initially use DllImport-style declarations so the existing Modern postprocess can convert the Modern file to `[LibraryImport]` automatically.

Output shape:

```csharp
using System.Runtime.InteropServices;

namespace SDL2
{
    internal static unsafe partial class SDLNative
    {
        public const uint SDL_INIT_TIMER = 0x00000001u;
        public const uint SDL_INIT_EVERYTHING = SDL_INIT_TIMER | /* ... */;

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int SDL_Init(uint flags);
    }
}
```

After `libraryimport`, the Modern file should use `[LibraryImport]`, `[UnmanagedCallConv]`, and `partial` methods like the existing Modern generated files.

Constants remain inside the internal raw ABI container for this spike slice. Public typed constants or flags belong to the later public typed API layer.

### Validation

Validation should prove four things:

1. The spike-local allowlist names exist in the installed `vcpkg_installed/<triplet>/include/SDL2/SDL.h`.
2. The installed `SDL.h` does not contain additional direct public functions or `SDL_INIT_*` macros outside the allowlist.
3. The spike-local allowlist matches the manifest-required SDL.h surface, but the manifest is not used for generation.
4. The generated output contains all required functions/constants and no extra recovered `SDL.h` symbols.

The current oracle already reports missing manifest-required functions/constants. B-slice should extend or reuse that evidence so the regenerated report shows:

- no `required-function-missing` findings for the five init/quit functions;
- no `required-constant-missing` findings for the ten `SDL_INIT_*` constants;
- remaining C-slice findings, such as deferred layouts and platform-sensitive scalar risks, still visible.

### Satellite Posture

Do not generalize SDL2 Core's `SDL.h` recovery to satellites.

Current header reconnaissance shows:

- SDL2_image is covered by `SDL_image.h`; `IMG_InitFlags` and `IMG_Init` already generate from that header.
- SDL2_mixer, SDL2_ttf, and SDL2_net are primarily single owned-header profile work.
- SDL2_gfx needs multi-header scope and custom export-token handling, not umbrella recovery.

If a future satellite red test proves a declaration exists only in an intentionally excluded umbrella header, add a family-specific required-surface file then. Until that evidence exists, B-slice remains SDL2 Core only.

## Error Handling

- If the allowlist references a symbol absent from installed `SDL.h`, fail before emitting output.
- If installed `SDL.h` has an extra direct public function or `SDL_INIT_*` macro not listed in the allowlist, fail or report a hard oracle finding.
- If manifest-required SDL.h surface differs from the spike-local allowlist, fail closed. The manifest is not generation input, but drift between the two should not be quiet.
- If the Modern postprocess does not convert `SDL_required.g.cs` methods to `[LibraryImport]`, treat that as a B-slice failure rather than hand-writing Modern-specific imports.

## Testing And Evidence

Implementation should use test-first changes around the generator/oracle behavior where practical.

Required verification commands:

- `python spikes/binding-generators/clangsharp/generate_bindings.py --scope full --codegen both --execute --clean-output --vcpkg-triplet x64-windows-hybrid --use-platform-header-shims`
- `dotnet run --file spikes/binding-generators/clangsharp/oracle.cs -- --self-test`
- `dotnet run --file spikes/binding-generators/clangsharp/oracle.cs -- --family sdl2-core --family sdl2-image --write-report`
- `dotnet build spikes/binding-generators/clangsharp/src/Janset.SDL2.Core/Janset.SDL2.Core.csproj -c Release`
- `dotnet build spikes/binding-generators/clangsharp/src/Janset.SDL2.Image/Janset.SDL2.Image.csproj -c Release`
- `git diff --check`
- `slopwatch analyze --fail-on warning --exclude "artifacts/**,external/**,vcpkg_installed/**,spikes/binding-generators/references/**,**/bin/**,**/obj/**"`

Exit evidence from the regenerated oracle report:

- required SDL.h functions: 0 missing;
- required `SDL_INIT_*` constants: 0 missing;
- A-slice findings remain absent;
- C-slice findings remain visible and explicitly deferred.

## Risks

- A curated file can drift from SDL.h if validation is weak. Make parity validation fail closed.
- Generating constants inside the internal raw ABI container is not the final public API shape. Keep this explicit so the typed API layer can later expose a nicer flags model.
- Parsing full `SDL.h` for convenience would undo the per-header generation model. The allowlist must constrain both symbol selection and output.
- A synthetic file may bypass ClangSharp formatting conventions. Keep its style close to generated output and let existing postprocess phases handle Modern backend conversion.
