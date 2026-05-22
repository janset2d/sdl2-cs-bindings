# ClangSharp Raw ABI Visibility Design

**Date:** 2026-05-22
**Status:** Accepted design; implementation plan lives in [`../plans/2026-05-22-clangsharp-raw-abi-visibility.md`](../plans/2026-05-22-clangsharp-raw-abi-visibility.md).
**Scope:** Spike-local ClangSharp prototype under `spikes/binding-generators/clangsharp/`.

## Goal

Fix oracle repair queue item A for the ClangSharp spike:

- Raw ABI containers must not be externally visible.
- Raw native imports must not be effectively public package API.
- SDL2_image generated output must use the expected family namespace `SDL2.Image`, while SDL2 Core remains `SDL2`.

This slice cleans the generated API shape before adding missing `SDL.h` initialization symbols or tackling platform-sensitive layout/scalar issues.

## Context

The current family-aware oracle report shows these A-priority findings:

- SDL2 Core leaks public `SDLNative` raw ABI containers. Its public raw import methods are public API leaks only because the container is public.
- SDL2 Image leaks public `SDL_imageNative` raw ABI containers. Its public raw import methods are public API leaks only because the container is public.
- SDL2 Image generated code lands in namespace `SDL2`, but the expected namespace is `SDL2.Image`.

The constitution requires the raw ABI layer to be internal and reserves public API for the later typed low-level and friendly layers. The canonical WHY/HOW/WHAT rationale now lives in [`../../binding-autogen/binding-generator-constitution.md`](../../binding-autogen/binding-generator-constitution.md). The spike currently builds because public raw output is easy for ClangSharp to emit, but that shape is not the target package contract.

## Why / How / What

**Why:** Generated native imports are volatile implementation detail, not the package compatibility contract. Keeping them internal lets the generator fix C `long`, `wchar_t`, bool, struct layout, platform attribution, and `DllImport` / `LibraryImport` backend choices without turning every correction into a public breaking change. Public raw bindings are valid for TerraFX/Silk.NET-style raw catalog packages; this project is aiming at a stable, low-level SDL platform layer with friendly APIs on top.

**How:** Hide the generated raw ABI container type. ClangSharp can do this directly with `--with-access-specifier SDLNative=Internal` and `--with-access-specifier SDL_imageNative=Internal`. Generated methods may remain lexically `public static extern` inside an internal container; C# containing-type accessibility prevents them from becoming public package API. Do not add a Roslyn visibility postprocess unless ClangSharp cannot express the container-level rule.

**What:** The public surface is a typed, low-allocation SDL API plus friendly overloads. Escape hatches belong in typed handles, `nint` / `DangerousGetHandle()`-style access, span/pointer overloads, and deliberately unsafe APIs. The generated `[DllImport]` / `[LibraryImport]` classes are not the blessed user-facing API. A separate raw package or raw namespace can be designed later only with an explicit different compatibility promise.

## Non-Goals

- Do not add the missing `SDL_Init` / `SDL_INIT_*` surface. That is repair queue item B.
- Do not fix `SDL_RWops`, `SDL_SysWMinfo`, `SDL_SysWMmsg`, `wchar_t`, or C `long`. That is repair queue item C.
- Do not introduce the public typed low-level API or friendly overloads.
- Do not change production Cake generator code, manifests, package projects, CI, or `src/` package output.
- Do not use broad `--with-access-specifier *=Internal`; this slice targets raw ABI containers, not every generated enum/struct/constant.
- Do not add or keep a Roslyn visibility postprocess for this slice.

## Design

### Family Namespace Ownership

Move namespace identity out of the shared ClangSharp base RSP and into family-specific generation policy.

SDL2 Core continues to emit namespace `SDL2`. SDL2 Image emits namespace `SDL2.Image`. Because Image generated signatures reference core-owned types such as `SDL_Surface`, `SDL_Texture`, `SDL_Renderer`, and `SDL_RWops`, Image output also needs a `using SDL2;` path or equivalent ClangSharp `--with-using` / postprocess support. The Image-local support attribute must follow the Image namespace so `[NativeTypeName]` resolves in generated Image files.

`NativeTypeNameAttribute` remains internal and assembly-local. It is ClangSharp diagnostic metadata that preserves native spelling such as `const char *`, `Uint32`, or macro source text; it is not runtime marshalling behavior and should not become a public Core API just so satellites can reuse it. Each generated family may carry the same small internal support attribute in its own namespace.

Preferred implementation order:

1. Use explicit family command options first: `--namespace SDL2` for Core, `--namespace SDL2.Image` for Image, and a family-specific `using SDL2` mechanism for Image if ClangSharp supports it cleanly.
2. If repeated `--namespace` options or wildcard `--with-using` are ambiguous, move namespace/using construction into `generate_bindings.py` so each family command is explicit and inspectable.
3. Use a small Roslyn postprocess only if ClangSharp cannot produce the required namespace/import shape reliably.

### Raw ABI Visibility

Generated raw ABI classes become internal. Raw import methods inside those internal classes may remain lexically public if ClangSharp emits them that way; they are not effectively public API because their containing class is internal. The rest of this slice must not redesign generated type visibility.

Preferred implementation order:

1. Add `--with-access-specifier SDLNative=Internal` for Core generation.
2. Add `--with-access-specifier SDL_imageNative=Internal` for Image generation.
3. Update the oracle to check effective visibility: raw import methods are leaks only when the raw ABI container is public.

Probe result: ClangSharp exact class access works; wildcard method access does not provide the narrow rule we want. Container-level internal access is enough for the public API boundary and avoids a magic visibility postprocess.

### Pipeline Placement

No new postprocess phase is added. The existing order stays:

```text
platform-delta -> strip-varargs -> libraryimport
```

## Error Handling

- If namespace correction would make Image fail to resolve Core-owned types, the implementation fails the build rather than adding duplicated Core type declarations to Image.
- If ClangSharp access-specifier output changes non-raw public types unexpectedly, reject that path rather than adding a broad wildcard access rule.
- Oracle findings remain report-only; this slice uses the report as exit evidence rather than making oracle failure fatal.

## Testing And Evidence

Implementation should use the existing oracle self-test for changed evidence semantics and deterministic regeneration for ClangSharp output. No code-level rewriter test is needed because this design does not add a rewriter.

Required verification commands:

- `python spikes/binding-generators/clangsharp/generate_bindings.py --scope full --codegen both --execute --clean-output --vcpkg-triplet x64-windows-hybrid --use-platform-header-shims`
- `dotnet run --file spikes/binding-generators/clangsharp/oracle.cs -- --family sdl2-core --family sdl2-image --write-report`
- `dotnet build spikes/binding-generators/clangsharp/src/Janset.SDL2.Image/Janset.SDL2.Image.csproj -c Release`
- `git diff --check`
- `slopwatch analyze --fail-on warning --exclude "artifacts/**,external/**,vcpkg_installed/**,spikes/binding-generators/references/**,**/bin/**,**/obj/**"`

Exit evidence from the regenerated oracle report:

- `raw-abi-public-class`: 0 findings for Core and Image.
- `raw-abi-public-import`: 0 effective public leaks for Core and Image. Lexical `public` methods inside an internal raw class are acceptable.
- `family-namespace-drift`: 0 findings for Image.
- Existing B and C findings may remain and should be called out as intentionally deferred.

## Risks

- Moving Image to `SDL2.Image` can break references to Core-owned types unless `using SDL2;` or equivalent is added.
- Making every declaration internal would hide generated enums/structs/constants and blur this slice with later public API design. Avoid broad wildcard access changes.
- The fix reduces raw public callability before typed wrappers exist. That is correct for the package contract, but it means spike consumers should not treat raw externs as the public API.
- Escape hatches must be designed deliberately in the public typed layer; otherwise advanced users may feel locked out and copy raw declarations themselves.
