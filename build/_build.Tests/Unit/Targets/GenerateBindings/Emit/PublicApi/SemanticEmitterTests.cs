using Build.Targets.GenerateBindings.Emit;
using Build.Targets.GenerateBindings.Model;

namespace Build.Tests.Unit.Targets.GenerateBindings.Emit.PublicApi;

public sealed class SemanticEmitterTests
{
    [Test]
    public async Task Emit_Should_Produce_Typed_Handle_File_When_Model_Has_Handles()
    {
        var windowType = NativeTypeRef.OpaqueHandle("SDL_Window", "SDL_Window", "sdl2-core", "SDL_video.h");
        var model = new BindingModel(
            Views: [],
            Structs: [],
            Enums: [],
            Constants: [],
            Handles: [new BindingHandle("SDL_Window", windowType)],
            Callbacks: []);

        var fileSet = new BindingEmitter().Emit(model, new BindingEmissionOptions("SDL2", "SDL"));

        var handles = fileSet.Files.Single(file => file.RelativePath == "Types/Handles.g.cs");
        await Assert.That(handles.Content).Contains("#nullable enable");
        await Assert.That(handles.Content).Contains("[DebuggerDisplay(\"SDL_Window = {Value}\")]");
        await Assert.That(handles.Content).Contains("public readonly partial struct SDL_Window(nint value)");
        await Assert.That(handles.Content).Contains("public readonly nint Value = value;");
    }

    [Test]
    public async Task Emit_Should_Produce_Callback_File_When_Model_Has_Callbacks()
    {
        var model = new BindingModel(
            Views: [],
            Structs: [],
            Enums: [],
            Constants: [],
            Handles: [],
            Callbacks:
            [
                new BindingCallback(
                    "SDL_EventFilter",
                    NativeTypeRef.Primitive("int", "int", NativeAbiShape.Of("int", 4)),
                    [
                        new BindingParameter(NativeTypeRef.Primitive("void*", "nint", NativeAbiShape.Of("nint")), "userdata"),
                        new BindingParameter(NativeTypeRef.Primitive("SDL_Event*", "nint", NativeAbiShape.Of("nint")), "@event"),
                    ]),
            ]);

        var fileSet = new BindingEmitter().Emit(model, new BindingEmissionOptions("SDL2", "SDL"));

        var callbacks = fileSet.Files.Single(file => file.RelativePath == "Types/Callbacks.g.cs");
        await Assert.That(callbacks.Content).Contains("using System.Runtime.InteropServices;");
        await Assert.That(callbacks.Content).Contains("[UnmanagedFunctionPointer(CallingConvention.Cdecl)]");
        await Assert.That(callbacks.Content).Contains("public unsafe delegate int SDL_EventFilter(nint userdata, nint @event);");
    }

    [Test]
    public async Task Emit_Should_Guard_CULong_Callbacks_To_Modern_Tfms()
    {
        var model = new BindingModel(
            Views: [],
            Structs: [],
            Enums: [],
            Constants: [],
            Handles: [],
            Callbacks:
            [
                new BindingCallback(
                    "SDL_malloc_func",
                    NativeTypeRef.Primitive("void*", "nint", NativeAbiShape.Of("nint")),
                    [new BindingParameter(NativeTypeRef.Primitive("unsigned long", "CULong", NativeAbiShape.Of("CULong")), "size")]),
            ]);

        var fileSet = new BindingEmitter().Emit(model, new BindingEmissionOptions("SDL2", "SDL"));
        var callbacks = fileSet.Files.Single(file => file.RelativePath == "Types/Callbacks.g.cs").Content;

        await Assert.That(callbacks).Contains("#if NET6_0_OR_GREATER");
        await Assert.That(callbacks).Contains("public unsafe delegate nint SDL_malloc_func(CULong size);");
        await Assert.That(callbacks).Contains("#endif");
    }

    [Test]
    public async Task Emit_Should_Guard_CLong_Pointer_Callbacks_To_Modern_Tfms()
    {
        var element = NativeTypeRef.Primitive("long", "CLong", NativeAbiShape.Of("CLong"));
        var pointer = NativeTypeRef.Indirection(element, 1, "CLong*");
        var model = new BindingModel(
            Views: [],
            Structs: [],
            Enums: [],
            Constants: [],
            Handles: [],
            Callbacks:
            [
                new BindingCallback(
                    "SDL_long_callback",
                    NativeTypeRef.Primitive("int", "int", NativeAbiShape.Of("int", 4)),
                    [new BindingParameter(pointer, "value")]),
            ]);

        var fileSet = new BindingEmitter().Emit(model, new BindingEmissionOptions("SDL2", "SDL"));
        var callbacks = fileSet.Files.Single(file => file.RelativePath == "Types/Callbacks.g.cs").Content;

        await Assert.That(callbacks).Contains("#if NET6_0_OR_GREATER");
        await Assert.That(callbacks).Contains("public unsafe delegate int SDL_long_callback(CLong* value);");
        await Assert.That(callbacks).Contains("#endif");
    }
}
