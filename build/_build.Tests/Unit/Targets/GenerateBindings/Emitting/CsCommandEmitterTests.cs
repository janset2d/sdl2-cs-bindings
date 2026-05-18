using Build.Targets.GenerateBindings.Emitting;
using Build.Targets.GenerateBindings.Model;
using System.Runtime.InteropServices;

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
    public async Task Emit_Should_Use_Core_Namespace_For_Command_Files()
    {
        var model = BindingModelData.TwoViewsNeutralPlusLinux();

        var fileSet = CsCommandEmitter.Emit(model);

        foreach (var file in CommandFiles(fileSet))
        {
            await Assert.That(file.Content).Contains("namespace Janset.SDL2.Core;");
        }
    }

    [Test]
    public async Task Emit_Should_Declare_Stable_Raw_Abi_Class_For_Command_Files()
    {
        var model = BindingModelData.TwoViewsNeutralPlusLinux();

        var fileSet = CsCommandEmitter.Emit(model);

        foreach (var file in CommandFiles(fileSet))
        {
            await Assert.That(file.Content).Contains("internal static unsafe partial class SDL2Native");
        }
    }

    [Test]
    public async Task Emit_Should_Not_Emit_View_Specific_Command_Class_Names()
    {
        var model = BindingModelData.TwoViewsNeutralPlusLinux();

        var fileSet = CsCommandEmitter.Emit(model);

        foreach (var file in CommandFiles(fileSet))
        {
            await Assert.That(file.Content).DoesNotContain("Sdl2_Neutral");
            await Assert.That(file.Content).DoesNotContain("Sdl2_Linux");
            await Assert.That(file.Content).DoesNotContain("class Sdl2_");
        }
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
    public async Task Emit_Should_Declare_LibName_Constant_Once_Per_Raw_Abi_Partial_Class()
    {
        var model = BindingModelData.TwoViewsNeutralPlusLinux();

        var fileSet = CsCommandEmitter.Emit(model);

        var libNameDeclarationCount = CommandFiles(fileSet)
            .Sum(file => CountOccurrences(file.Content, "private const string LibName = \"SDL2\";"));

        await Assert.That(libNameDeclarationCount).IsEqualTo(1);
    }

    [Test]
    public async Task Emit_Should_Declare_LibName_Constant_In_Neutral_View_When_View_Order_Changes()
    {
        var model = BindingModelData.LinuxBeforeNeutral();

        var fileSet = CsCommandEmitter.Emit(model);

        var linuxFile = fileSet.Files.Single(f => f.RelativePath == "Platform/Linux/Commands.g.cs");
        var neutralFile = fileSet.Files.Single(f => f.RelativePath == "Platform/Neutral/Commands.g.cs");
        await Assert.That(linuxFile.Content).DoesNotContain("private const string LibName = \"SDL2\";");
        await Assert.That(neutralFile.Content).Contains("private const string LibName = \"SDL2\";");
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

    [Test]
    public async Task Emit_Should_Produce_Structs_File_When_Model_Has_Structs()
    {
        var model = BindingModelData.ModelWithStructs();

        var fileSet = CsCommandEmitter.Emit(model);

        var structsFile = fileSet.Files.Single(f => f.RelativePath == "Types/Structs.g.cs");
        await Assert.That(structsFile.Content).Contains("namespace Janset.SDL2.Core;");
        await Assert.That(structsFile.Content).Contains("[StructLayout(LayoutKind.Sequential)]");
        await Assert.That(structsFile.Content).Contains("public unsafe partial struct SDL_CustomBytes");
        await Assert.That(structsFile.Content).Contains("public fixed byte data[16];");
        await Assert.That(structsFile.Content).Contains("public partial struct SDL_GameControllerButtonBind");
        await Assert.That(structsFile.Content).Contains("public SDL_GameControllerButtonBind_value @value;");
        await Assert.That(structsFile.Content).Contains("[StructLayout(LayoutKind.Explicit, Size = 8)]");
        await Assert.That(structsFile.Content).Contains("public partial struct SDL_GameControllerButtonBind_value");
        await Assert.That(structsFile.Content).Contains("[FieldOffset(0)]");
        await Assert.That(structsFile.Content).Contains("public int button;");
        await Assert.That(structsFile.Content).Contains("public int axis;");
    }

    [Test]
    public async Task Emit_Should_Mark_Struct_Unsafe_When_It_Has_Pointer_Fields()
    {
        var model = new BindingModel(
            Views: [],
            Structs:
            [
                new BindingStruct(
                    Name: "SDL_PointerFields",
                    Fields:
                    [
                        new BindingStructField("name", BindingTypeRef.Of("sbyte*"), FieldOffset: null),
                    ],
                    Layout: LayoutKind.Sequential,
                    ExplicitSize: null),
            ],
            Enums: [],
            Constants: [],
            Handles: [],
            Callbacks: []);

        var fileSet = CsCommandEmitter.Emit(model);

        var structsFile = fileSet.Files.Single(f => f.RelativePath == "Types/Structs.g.cs");
        await Assert.That(structsFile.Content).Contains("public unsafe partial struct SDL_PointerFields");
        await Assert.That(structsFile.Content).Contains("public sbyte* name;");
    }

    [Test]
    public async Task Emit_Should_Use_InlineArray_For_Fixed_Array_Elements_That_CSharp_Fixed_Buffers_Do_Not_Support()
    {
        var model = new BindingModel(
            Views: [],
            Structs:
            [
                new BindingStruct(
                    Name: "SDL_ColorScheme",
                    Fields:
                    [
                        new BindingStructField("colors", BindingTypeRef.Of("SDL_MessageBoxColor"), FieldOffset: null, FixedBufferLength: 5),
                    ],
                    Layout: LayoutKind.Sequential,
                    ExplicitSize: null),
            ],
            Enums: [],
            Constants: [],
            Handles: [],
            Callbacks: []);

        var fileSet = CsCommandEmitter.Emit(model);

        var structsFile = fileSet.Files.Single(f => f.RelativePath == "Types/Structs.g.cs");
        await Assert.That(structsFile.Content).Contains("using System.Runtime.CompilerServices;");
        await Assert.That(structsFile.Content).Contains("public SDL_ColorScheme_colors colors;");
        await Assert.That(structsFile.Content).Contains("[InlineArray(5)]");
        await Assert.That(structsFile.Content).Contains("public partial struct SDL_ColorScheme_colors");
        await Assert.That(structsFile.Content).Contains("private SDL_MessageBoxColor _element0;");
        await Assert.That(structsFile.Content).DoesNotContain("fixed SDL_MessageBoxColor colors[5];");
    }

    private static IEnumerable<GeneratedFile> CommandFiles(GeneratedFileSet fileSet)
    {
        return fileSet.Files.Where(f => f.RelativePath.EndsWith("Commands.g.cs", StringComparison.Ordinal));
    }

    private static int CountOccurrences(string text, string value)
    {
        var count = 0;
        var startIndex = 0;
        while (true)
        {
            var index = text.IndexOf(value, startIndex, StringComparison.Ordinal);
            if (index < 0)
            {
                return count;
            }

            count++;
            startIndex = index + value.Length;
        }
    }
}
