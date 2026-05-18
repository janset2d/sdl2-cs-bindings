using Build.Data.BindingGeneration.Models;
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

        var fileSet = Emit(model);

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

        var fileSet = Emit(model);

        var neutralFile = fileSet.Files.Single(f => f.RelativePath == "Platform/Neutral/Commands.g.cs");
        await Assert.That(neutralFile.Content).Contains("[DllImport(LibName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]");
        await Assert.That(neutralFile.Content).Contains("internal static extern int SDL_Init(uint flags);");
        await Assert.That(neutralFile.Content).Contains("internal static extern void SDL_Quit();");
    }

    [Test]
    public async Task Emit_Should_Use_Configured_Namespace_For_Command_Files()
    {
        var model = BindingModelData.TwoViewsNeutralPlusLinux();

        var fileSet = Emit(model);

        foreach (var file in CommandFiles(fileSet))
        {
            await Assert.That(file.Content).Contains("namespace SDL2;");
            await Assert.That(file.Content).DoesNotContain("namespace Janset.SDL2.Core;");
        }
    }

    [Test]
    public async Task Emit_Should_Declare_Configured_Raw_Abi_Class_For_Command_Files()
    {
        var model = BindingModelData.TwoViewsNeutralPlusLinux();

        var fileSet = Emit(model);

        foreach (var file in CommandFiles(fileSet))
        {
            await Assert.That(file.Content).Contains("internal static unsafe partial class SDLNative");
            await Assert.That(file.Content).DoesNotContain("internal static unsafe partial class SDL2Native");
        }
    }

    [Test]
    public async Task Emit_Should_Not_Emit_View_Specific_Command_Class_Names()
    {
        var model = BindingModelData.TwoViewsNeutralPlusLinux();

        var fileSet = Emit(model);

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

        var fileSet = Emit(model);

        var linuxFile = fileSet.Files.Single(f => f.RelativePath == "Platform/Linux/Commands.g.cs");
        await Assert.That(linuxFile.Content).Contains("[SupportedOSPlatform(\"linux\")]");
    }

    [Test]
    public async Task Emit_Should_Not_Annotate_Neutral_View_With_SupportedOSPlatform()
    {
        var model = BindingModelData.TwoViewsNeutralPlusLinux();

        var fileSet = Emit(model);

        var neutralFile = fileSet.Files.Single(f => f.RelativePath == "Platform/Neutral/Commands.g.cs");
        await Assert.That(neutralFile.Content).DoesNotContain("SupportedOSPlatform");
    }

    [Test]
    public async Task Emit_Should_Declare_LibName_Constant_Once_Per_Raw_Abi_Partial_Class()
    {
        var model = BindingModelData.TwoViewsNeutralPlusLinux();

        var fileSet = Emit(model);

        var libNameDeclarationCount = CommandFiles(fileSet)
            .Sum(file => CountOccurrences(file.Content, "private const string LibName = \"SDL2\";"));

        await Assert.That(libNameDeclarationCount).IsEqualTo(1);
    }

    [Test]
    public async Task Emit_Should_Declare_LibName_Constant_In_Neutral_View_When_View_Order_Changes()
    {
        var model = BindingModelData.LinuxBeforeNeutral();

        var fileSet = Emit(model);

        var linuxFile = fileSet.Files.Single(f => f.RelativePath == "Platform/Linux/Commands.g.cs");
        var neutralFile = fileSet.Files.Single(f => f.RelativePath == "Platform/Neutral/Commands.g.cs");
        await Assert.That(linuxFile.Content).DoesNotContain("private const string LibName = \"SDL2\";");
        await Assert.That(neutralFile.Content).Contains("private const string LibName = \"SDL2\";");
    }

    [Test]
    public async Task Emit_Should_Be_Deterministic_Across_Invocations()
    {
        var model = BindingModelData.TwoViewsNeutralPlusLinux();

        var first = Emit(model);
        var second = Emit(model);

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

        var fileSet = Emit(model);

        var file = fileSet.Files.Single(f => f.RelativePath == "Platform/Neutral/Commands.g.cs");
        await Assert.That(file.Content).Contains("internal static extern uint SDL_GetTicks();");
    }

    [Test]
    public async Task Emit_Should_Produce_Only_Report_For_Empty_Model()
    {
        var model = BindingModelData.EmptyModel();

        var fileSet = Emit(model);

        await Assert.That(fileSet.Files.Count).IsEqualTo(1);
        await Assert.That(fileSet.Files[0].RelativePath).IsEqualTo("parse-views.json");
    }

    [Test]
    public async Task Emit_Should_Produce_Structs_File_When_Model_Has_Structs()
    {
        var model = BindingModelData.ModelWithStructs();

        var fileSet = Emit(model);

        var structsFile = fileSet.Files.Single(f => f.RelativePath == "Types/Structs.g.cs");
        await Assert.That(structsFile.Content).Contains("namespace SDL2;");
        await Assert.That(structsFile.Content).DoesNotContain("namespace Janset.SDL2.Core;");
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

        var fileSet = Emit(model);

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

        var fileSet = Emit(model);

        var structsFile = fileSet.Files.Single(f => f.RelativePath == "Types/Structs.g.cs");
        await Assert.That(structsFile.Content).Contains("using System.Runtime.CompilerServices;");
        await Assert.That(structsFile.Content).Contains("public SDL_ColorScheme_colors colors;");
        await Assert.That(structsFile.Content).Contains("[InlineArray(5)]");
        await Assert.That(structsFile.Content).Contains("public partial struct SDL_ColorScheme_colors");
        await Assert.That(structsFile.Content).Contains("private SDL_MessageBoxColor _element0;");
        await Assert.That(structsFile.Content).DoesNotContain("fixed SDL_MessageBoxColor colors[5];");
    }

    [Test]
    public async Task Emit_Should_Produce_Constants_File_When_Model_Has_Constants()
    {
        var model = new BindingModel(
            Views: [],
            Structs: [],
            Enums: [],
            Constants:
            [
                new BindingConstant("SDL_INIT_TIMER", BindingTypeRef.Of("uint"), "0x00000001u", ConstantKind.Literal),
                new BindingConstant("SDL_INIT_EVERYTHING", BindingTypeRef.Of("uint"), "SDL_INIT_TIMER | SDL_INIT_AUDIO", ConstantKind.Computed),
                new BindingConstant("SDL_HINT_RENDER_DRIVER", BindingTypeRef.Of("ReadOnlySpan<byte>"), "\"SDL_RENDER_DRIVER\"u8", ConstantKind.Literal),
            ],
            Handles: [],
            Callbacks: []);

        var fileSet = Emit(model);

        var constantsFile = fileSet.Files.Single(f => f.RelativePath == "Constants.g.cs");
        await Assert.That(constantsFile.Content).Contains("using System;");
        await Assert.That(constantsFile.Content).Contains("namespace SDL2;");
        await Assert.That(constantsFile.Content).Contains("public static partial class SDL");
        await Assert.That(constantsFile.Content).Contains("public const uint SDL_INIT_TIMER = 0x00000001u;");
        await Assert.That(constantsFile.Content).Contains("public const uint SDL_INIT_EVERYTHING = SDL_INIT_TIMER | SDL_INIT_AUDIO;");
        await Assert.That(constantsFile.Content).Contains("public static ReadOnlySpan<byte> SDL_HINT_RENDER_DRIVER => \"SDL_RENDER_DRIVER\"u8;");
        await Assert.That(constantsFile.Content).DoesNotContain("public const string SDL_HINT_RENDER_DRIVER");
    }

    [Test]
    public async Task Emit_Should_Produce_Enums_File_When_Model_Has_Enums()
    {
        var model = new BindingModel(
            Views: [],
            Structs: [],
            Enums:
            [
                new BindingEnumeration(
                    Name: "SDL_WindowFlags",
                    UnderlyingType: BindingTypeRef.Of("uint"),
                    Members:
                    [
                        new BindingEnumMember("SDL_WINDOW_FULLSCREEN", "0x00000001u"),
                        new BindingEnumMember("SDL_WINDOW_OPENGL", "0x00000002u"),
                        new BindingEnumMember("SDL_WINDOW_MOUSE_GRABBED", "0x00000100u"),
                        new BindingEnumMember("SDL_WINDOW_FULLSCREEN_DESKTOP", "SDL_WINDOW_FULLSCREEN | 0x00001000u"),
                        new BindingEnumMember("SDL_WINDOW_INPUT_GRABBED", "SDL_WINDOW_MOUSE_GRABBED"),
                    ],
                    IsFlags: true),
            ],
            Constants: [],
            Handles: [],
            Callbacks: []);

        var fileSet = Emit(model);

        var enumsFile = fileSet.Files.Single(f => f.RelativePath == "Types/Enums.g.cs");
        await Assert.That(enumsFile.Content).Contains("using System;");
        await Assert.That(enumsFile.Content).Contains("namespace SDL2;");
        await Assert.That(enumsFile.Content).Contains("[Flags]");
        await Assert.That(enumsFile.Content).Contains("public enum SDL_WindowFlags : uint");
        await Assert.That(enumsFile.Content).Contains("SDL_WINDOW_FULLSCREEN = 0x00000001u,");
        await Assert.That(enumsFile.Content).Contains("SDL_WINDOW_FULLSCREEN_DESKTOP = SDL_WINDOW_FULLSCREEN | 0x00001000u,");
        await Assert.That(enumsFile.Content).Contains("SDL_WINDOW_INPUT_GRABBED = SDL_WINDOW_MOUSE_GRABBED,");
    }

    private static IEnumerable<GeneratedFile> CommandFiles(GeneratedFileSet fileSet)
    {
        return fileSet.Files.Where(f => f.RelativePath.EndsWith("Commands.g.cs", StringComparison.Ordinal));
    }

    private static GeneratedFileSet Emit(BindingModel model) =>
        CsCommandEmitter.Emit(model, new BindingEmissionOptions("SDL2", "SDL"));

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
