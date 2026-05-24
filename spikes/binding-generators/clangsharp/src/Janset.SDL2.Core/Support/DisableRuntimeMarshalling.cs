// Slice C-B Pattern B handle support: opt the assembly into the modern
// blittable-only P/Invoke marshalling model.
//
// Pattern B per Constitution §"Opaque Handles" emits opaque handles as
// `readonly partial struct X(nint value) : IEquatable<X>` with explicit
// [StructLayout(LayoutKind.Sequential)]. Within the same assembly the
// LibraryImport source generator can prove blittability from the source
// declaration, so methods using the handle by value compile without any
// assembly-wide opt-in. Cross-assembly consumers (Janset.SDL2.Image and any
// future satellite using its own [LibraryImport] surface) hit SYSLIB1051
// because the generator looks at the handle through metadata and falls into
// the conservative "user-defined struct => requires runtime marshalling
// opt-in" path. Microsoft's documented resolution is
// [assembly: DisableRuntimeMarshalling]; this is also the precedent in
// Alimer.Bindings.SDL (peer reference checked into the spike at
// spikes/binding-generators/references/alimer-bindings-sdl).
//
// The attribute lives in System.Runtime.CompilerServices and is only
// available on net7+. The #if guard restricts it to TFMs that consume the
// Modern (LibraryImport) emit. Legacy TFMs (netstandard2.0, net462) use
// the Compat (DllImport) emit, which performs runtime marshalling itself
// and neither needs nor supports the attribute.
//
// Safety: the assembly does not rely on auto-marshalling. Strings are passed
// as `byte*` (UTF-8 caller-allocated buffers), bools are `SDL_bool` enums,
// and all aggregate parameters are either pointers (`SDL_Surface*`) or
// blittable structs ([StructLayout(LayoutKind.Sequential)] with primitive
// fields). Disabling runtime marshalling therefore has no semantic effect
// on any existing signature.

#if NET7_0_OR_GREATER
[assembly: System.Runtime.CompilerServices.DisableRuntimeMarshalling]
#endif
