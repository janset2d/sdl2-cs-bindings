using Build.Targets.GenerateBindings.Emitting;

namespace Build.Tests.Unit.Targets.GenerateBindings.Emitting;

public sealed class CsCommandEmitterTests
{
    [Test]
    public async Task Emit_Should_Produce_One_File_Per_Parse_View_Plus_Report()
    {
        var model = BindingModelData.TwoViewsNeutralPlusLinux();

        var fileSet = CsCommandEmitter.Emit(model);

        var relativePaths = fileSet.Files.Select(f => f.RelativePath).ToArray();
        await Assert.That(relativePaths).IsEquivalentTo(
        [
            "Platform/Neutral/Commands.g.cs",
            "Platform/Linux/Commands.g.cs",
            "parse-views.json",
        ]);
    }

    [Test]
    public async Task Emit_Should_Produce_DllImport_Stub_Per_Function()
    {
        var model = BindingModelData.TwoViewsNeutralPlusLinux();

        var fileSet = CsCommandEmitter.Emit(model);

        var neutralFile = fileSet.Files.Single(f => f.RelativePath == "Platform/Neutral/Commands.g.cs");
        await Assert.That(neutralFile.Content).Contains("[DllImport(LibName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]");
        await Assert.That(neutralFile.Content).Contains("internal static extern int SDL_Init(uint flags);");
        await Assert.That(neutralFile.Content).Contains("internal static extern void SDL_Quit();");
    }

    [Test]
    public async Task Emit_Should_Declare_View_Class_As_Unsafe()
    {
        var model = BindingModelData.TwoViewsNeutralPlusLinux();

        var fileSet = CsCommandEmitter.Emit(model);

        var neutralFile = fileSet.Files.Single(f => f.RelativePath == "Platform/Neutral/Commands.g.cs");
        await Assert.That(neutralFile.Content).Contains("internal static unsafe partial class Sdl2_Neutral");
    }

    [Test]
    public async Task Emit_Should_Annotate_Platform_Views_With_SupportedOSPlatform()
    {
        var model = BindingModelData.TwoViewsNeutralPlusLinux();

        var fileSet = CsCommandEmitter.Emit(model);

        var linuxFile = fileSet.Files.Single(f => f.RelativePath == "Platform/Linux/Commands.g.cs");
        await Assert.That(linuxFile.Content).Contains("[SupportedOSPlatform(\"linux\")]");
    }

    [Test]
    public async Task Emit_Should_Not_Annotate_Neutral_View_With_SupportedOSPlatform()
    {
        var model = BindingModelData.TwoViewsNeutralPlusLinux();

        var fileSet = CsCommandEmitter.Emit(model);

        var neutralFile = fileSet.Files.Single(f => f.RelativePath == "Platform/Neutral/Commands.g.cs");
        await Assert.That(neutralFile.Content).DoesNotContain("SupportedOSPlatform");
    }

    [Test]
    public async Task Emit_Should_Include_LibName_Constant_In_Each_View_File()
    {
        var model = BindingModelData.TwoViewsNeutralPlusLinux();

        var fileSet = CsCommandEmitter.Emit(model);

        foreach (var file in fileSet.Files.Where(f => f.RelativePath.EndsWith(".g.cs", StringComparison.Ordinal)))
        {
            await Assert.That(file.Content).Contains("private const string LibName = \"SDL2\";");
        }
    }

    [Test]
    public async Task Emit_Should_Be_Deterministic_Across_Invocations()
    {
        var model = BindingModelData.TwoViewsNeutralPlusLinux();

        var first = CsCommandEmitter.Emit(model);
        var second = CsCommandEmitter.Emit(model);

        for (var i = 0; i < first.Files.Count; i++)
        {
            await Assert.That(second.Files[i].RelativePath).IsEqualTo(first.Files[i].RelativePath);
            await Assert.That(second.Files[i].Content).IsEqualTo(first.Files[i].Content);
        }
    }

    [Test]
    public async Task Emit_Should_Handle_Function_With_No_Parameters()
    {
        var model = BindingModelData.SingleNeutralEmptyParameterFunction();

        var fileSet = CsCommandEmitter.Emit(model);

        var file = fileSet.Files.Single(f => f.RelativePath == "Platform/Neutral/Commands.g.cs");
        await Assert.That(file.Content).Contains("internal static extern uint SDL_GetTicks();");
    }

    [Test]
    public async Task Emit_Should_Produce_Only_Report_For_Empty_Model()
    {
        var model = BindingModelData.EmptyModel();

        var fileSet = CsCommandEmitter.Emit(model);

        await Assert.That(fileSet.Files.Count).IsEqualTo(1);
        await Assert.That(fileSet.Files[0].RelativePath).IsEqualTo("parse-views.json");
    }
}
