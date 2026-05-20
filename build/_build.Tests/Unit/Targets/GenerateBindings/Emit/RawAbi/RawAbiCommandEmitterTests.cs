using Build.Targets.GenerateBindings.Emit;
using Build.Targets.GenerateBindings.Emit.RawAbi;
using Build.Targets.GenerateBindings.Model;
using Build.Tests.Unit.Targets.GenerateBindings.Emit;

namespace Build.Tests.Unit.Targets.GenerateBindings.Emit.RawAbi;

public sealed class RawAbiCommandEmitterTests
{
    [Test]
    public async Task Emit_Should_Produce_DllImport_Stub_Per_Function()
    {
        var view = BindingModelData.TwoViewsNeutralPlusLinux().Views.Single(v => v.Name == "Neutral");

        var file = Emit(view, includeLibName: true);

        await Assert.That(file.Content).Contains("[DllImport(LibName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]");
        await Assert.That(file.Content).Contains("internal static extern int SDL_Init(uint flags);");
        await Assert.That(file.Content).Contains("internal static extern void SDL_Quit();");
    }

    [Test]
    public async Task Emit_Should_Use_Configured_Namespace_For_Command_Files()
    {
        var view = BindingModelData.TwoViewsNeutralPlusLinux().Views.Single(v => v.Name == "Neutral");

        var file = Emit(view);

        await Assert.That(file.Content).Contains("namespace SDL2;");
        await Assert.That(file.Content).DoesNotContain("namespace Janset.SDL2.Core;");
    }

    [Test]
    public async Task Emit_Should_Declare_Configured_Raw_Abi_Class_For_Command_Files()
    {
        var view = BindingModelData.TwoViewsNeutralPlusLinux().Views.Single(v => v.Name == "Neutral");

        var file = Emit(view);
        await Assert.That(file.Content).Contains("internal static unsafe partial class SDLNative");
        await Assert.That(file.Content).DoesNotContain("internal static unsafe partial class SDL2Native");
    }

    [Test]
    public async Task Emit_Should_Not_Emit_View_Specific_Command_Class_Names()
    {
        var view = BindingModelData.TwoViewsNeutralPlusLinux().Views.Single(v => v.Name == "Linux");

        var file = Emit(view);
        await Assert.That(file.Content).DoesNotContain("Sdl2_Neutral");
        await Assert.That(file.Content).DoesNotContain("Sdl2_Linux");
        await Assert.That(file.Content).DoesNotContain("class Sdl2_");
    }

    [Test]
    public async Task Emit_Should_Annotate_Platform_Views_With_SupportedOSPlatform()
    {
        var view = BindingModelData.TwoViewsNeutralPlusLinux().Views.Single(v => v.Name == "Linux");

        var file = Emit(view);
        await Assert.That(file.Content).Contains("#if NET5_0_OR_GREATER");
        await Assert.That(file.Content).Contains("[SupportedOSPlatform(\"linux\")]");
        await Assert.That(file.Content).Contains("#endif");
    }

    [Test]
    public async Task Emit_Should_Not_Annotate_Neutral_View_With_SupportedOSPlatform()
    {
        var view = BindingModelData.TwoViewsNeutralPlusLinux().Views.Single(v => v.Name == "Neutral");

        var file = Emit(view);
        await Assert.That(file.Content).DoesNotContain("SupportedOSPlatform");
    }

    [Test]
    public async Task Emit_Should_Handle_Function_With_No_Parameters()
    {
        var view = BindingModelData.SingleNeutralEmptyParameterFunction().Views.Single();

        var file = Emit(view, includeLibName: true);
        await Assert.That(file.Content).Contains("internal static extern uint SDL_GetTicks();");
    }

    [Test]
    public async Task Emit_Should_Guard_CLong_Functions_To_Modern_Tfms()
    {
        var view = new BindingParseView(
            "Neutral",
            null,
            [new BindingFunction(
                "SDL_lround",
                NativeTypeRef.Primitive("long", "CLong", NativeAbiShape.Of("CLong")),
                [new BindingParameter(NativeTypeRef.Primitive("double", "double", NativeAbiShape.Of("double", 8)), "x")],
                "SDL_stdinc.h")]);

        var file = Emit(view, includeLibName: true);

        await Assert.That(file.Content).Contains("#if NET6_0_OR_GREATER");
        await Assert.That(file.Content).Contains("internal static extern CLong SDL_lround(double x);");
        await Assert.That(file.Content).Contains("#endif");
    }

    [Test]
    public async Task Emit_Should_Guard_CLong_Pointer_Functions_To_Modern_Tfms()
    {
        var cLongPointer = NativeTypeRef.Indirection(
            NativeTypeRef.Primitive("long", "CLong", NativeAbiShape.Of("CLong")),
            indirectionDepth: 1,
            managedName: "CLong*");
        var view = new BindingParseView(
            "Neutral",
            null,
            [new BindingFunction(
                "SDL_read_long",
                NativeTypeRef.Primitive("int", "int", NativeAbiShape.Of("int", 4)),
                [new BindingParameter(cLongPointer, "value")],
                "SDL_stdinc.h")]);

        var file = Emit(view, includeLibName: true);

        await Assert.That(file.Content).Contains("#if NET6_0_OR_GREATER");
        await Assert.That(file.Content).Contains("internal static extern int SDL_read_long(CLong* value);");
        await Assert.That(file.Content).Contains("#endif");
    }

    private static GeneratedFile Emit(BindingParseView view, bool includeLibName = false) =>
        RawAbiCommandEmitter.Emit(view, new BindingEmissionOptions("SDL2", "SDL"), includeLibName);
}
