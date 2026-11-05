// Slice C-B Pattern B handle support: opt the assembly into the modern
// blittable-only P/Invoke marshalling model.
//
// Pattern B handles owned by Janset.SDL2.Core appear by value in
// Janset.SDL2.Image's [LibraryImport] surface (e.g. IMG_LoadTexture_RW
// takes SDL_RWops and returns SDL_Texture). The LibraryImport source
// generator looks at cross-assembly Pattern B structs through metadata
// and conservatively requires SYSLIB1051's documented opt-in via
// [assembly: DisableRuntimeMarshalling]. This file applies it.
//
// Rationale (full): see the matching file in
// spikes/binding-generators/clangsharp/src/Janset.SDL2.Core/Support/DisableRuntimeMarshalling.cs.
// Safety: Image P/Invokes use `byte*` for strings, no `bool`, no `string`,
// no auto-marshalled aggregates. Disabling runtime marshalling has no
// semantic effect on the existing signatures.
//
// Legacy TFMs (netstandard2.0, net462) use the Compat (DllImport) emit
// and neither need nor support the attribute; the #if guard restricts
// the application to net7+ consumers of the Modern (LibraryImport) emit.

#if NET7_0_OR_GREATER
[assembly: System.Runtime.CompilerServices.DisableRuntimeMarshalling]
#endif
