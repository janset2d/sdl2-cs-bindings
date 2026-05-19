using Build.Data.BindingGeneration.Models;
using Build.Targets.GenerateBindings.Emitting;
using Build.Targets.GenerateBindings.Model;
using Build.Tests.Fixtures;
using System.Runtime.InteropServices;
using System.Text.Json;

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
        await Assert.That(linuxFile.Content).Contains("#if NET5_0_OR_GREATER");
        await Assert.That(linuxFile.Content).Contains("[SupportedOSPlatform(\"linux\")]");
        await Assert.That(linuxFile.Content).Contains("#endif");
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
    public async Task Emit_Should_Guard_CLong_Functions_To_Modern_Tfms()
    {
        var model = new BindingModel(
            [new BindingParseView(
                "Neutral",
                null,
                [new BindingFunction(
                    "SDL_lround",
                    NativeTypeRef.Primitive("long", "CLong", NativeAbiShape.Of("CLong")),
                    [new BindingParameter(NativeTypeRef.Primitive("double", "double", NativeAbiShape.Of("double", 8)), "x")],
                    "SDL_stdinc.h")])]);

        var fileSet = CsCommandEmitter.Emit(model, new BindingEmissionOptions("SDL2", "SDL"));
        var commands = fileSet.Files.Single(file => file.RelativePath == "Platform/Neutral/Commands.g.cs").Content;

        await Assert.That(commands).Contains("#if NET6_0_OR_GREATER");
        await Assert.That(commands).Contains("internal static extern CLong SDL_lround(double x);");
        await Assert.That(commands).Contains("#endif");
    }

    [Test]
    public async Task Emit_Should_Guard_CLong_Pointer_Functions_To_Modern_Tfms()
    {
        var cLongPointer = NativeTypeRef.Indirection(
            NativeTypeRef.Primitive("long", "CLong", NativeAbiShape.Of("CLong")),
            indirectionDepth: 1,
            managedName: "CLong*");
        var model = new BindingModel(
            [new BindingParseView(
                "Neutral",
                null,
                [new BindingFunction(
                    "SDL_read_long",
                    NativeTypeRef.Primitive("int", "int", NativeAbiShape.Of("int", 4)),
                    [new BindingParameter(cLongPointer, "value")],
                    "SDL_stdinc.h")])]);

        var fileSet = CsCommandEmitter.Emit(model, new BindingEmissionOptions("SDL2", "SDL"));
        var commands = fileSet.Files.Single(file => file.RelativePath == "Platform/Neutral/Commands.g.cs").Content;

        await Assert.That(commands).Contains("#if NET6_0_OR_GREATER");
        await Assert.That(commands).Contains("internal static extern int SDL_read_long(CLong* value);");
        await Assert.That(commands).Contains("#endif");
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
    public async Task Emit_Should_Write_Rich_Parse_View_Audit_Report()
    {
        var model = BindingModelData.ModelWithRichParseViewEvidence();

        var fileSet = Emit(model);

        using var document = JsonDocument.Parse(fileSet.Files.Single(f => f.RelativePath == "parse-views.json").Content);
        var root = document.RootElement;
        await Assert.That(root.GetProperty("SchemaVersion").GetInt32()).IsEqualTo(1);

        var categories = root.GetProperty("Categories");
        await Assert.That(categories.GetProperty("ViewCount").GetInt32()).IsEqualTo(2);
        await Assert.That(categories.GetProperty("FunctionCount").GetInt32()).IsEqualTo(3);
        await Assert.That(categories.GetProperty("StructCount").GetInt32()).IsEqualTo(1);
        await Assert.That(categories.GetProperty("EnumCount").GetInt32()).IsEqualTo(1);
        await Assert.That(categories.GetProperty("ConstantCount").GetInt32()).IsEqualTo(1);
        await Assert.That(categories.GetProperty("HandleCount").GetInt32()).IsEqualTo(1);
        await Assert.That(categories.GetProperty("CallbackCount").GetInt32()).IsEqualTo(1);
        await Assert.That(JsonStrings(categories.GetProperty("Structs"))).IsEquivalentTo(["SDL_Rect"]);
        await Assert.That(JsonStrings(categories.GetProperty("Enums"))).IsEquivalentTo(["SDL_EventType"]);
        await Assert.That(JsonStrings(categories.GetProperty("Constants"))).IsEquivalentTo(["SDL_INIT_TIMER"]);
        await Assert.That(JsonStrings(categories.GetProperty("Handles"))).IsEquivalentTo(["SDL_Window"]);
        await Assert.That(JsonStrings(categories.GetProperty("Callbacks"))).IsEquivalentTo(["SDL_AudioCallback"]);

        await Assert.That(JsonStrings(root.GetProperty("EmittedFiles"))).IsEquivalentTo(
        [
            "Platform/Neutral/Commands.g.cs",
            "Platform/Linux/Commands.g.cs",
            "Constants.g.cs",
            "Types/Enums.g.cs",
            "Types/Handles.g.cs",
            "Types/Structs.g.cs",
            "Types/Callbacks.g.cs",
            "parse-views.json",
        ]);

        var linux = root.GetProperty("Views").EnumerateArray()
            .Single(view => view.GetProperty("Name").GetString() == "Linux");
        await Assert.That(linux.GetProperty("PlatformConditionKind").GetString()).IsEqualTo("OperatingSystem");
        await Assert.That(linux.GetProperty("SupportedOSPlatform").GetString()).IsEqualTo("linux");
        await Assert.That(JsonStrings(linux.GetProperty("Defines"))).IsEquivalentTo(["SDL_VIDEO_DRIVER_X11=1"]);
        await Assert.That(JsonStrings(linux.GetProperty("Undefines"))).IsEquivalentTo(["__WIN32__"]);
        await Assert.That(linux.GetProperty("FunctionCount").GetInt32()).IsEqualTo(1);

        var function = linux.GetProperty("Functions").EnumerateArray().Single();
        await Assert.That(function.GetProperty("Name").GetString()).IsEqualTo("SDL_LinuxSetThreadPriority");
        await Assert.That(function.GetProperty("SourceHeader").GetString()).IsEqualTo("SDL_system.h");
        await Assert.That(function.GetProperty("ReturnType").GetString()).IsEqualTo("int");
        var parameters = function.GetProperty("Parameters").EnumerateArray().ToArray();
        await Assert.That(parameters[0].GetProperty("Name").GetString()).IsEqualTo("threadID");
        await Assert.That(parameters[0].GetProperty("Type").GetString()).IsEqualTo("long");
        await Assert.That(parameters[1].GetProperty("Name").GetString()).IsEqualTo("priority");
        await Assert.That(parameters[1].GetProperty("Type").GetString()).IsEqualTo("int");

        var macroConstants = root.GetProperty("MacroConstants");
        await Assert.That(macroConstants.GetProperty("ParsedCount").GetInt32()).IsEqualTo(1);
        await Assert.That(macroConstants.GetProperty("EmittedCount").GetInt32()).IsEqualTo(1);
        await Assert.That(macroConstants.GetProperty("HelperCandidateCount").GetInt32()).IsEqualTo(0);
        await Assert.That(macroConstants.GetProperty("HelperDuplicateCoalescedCount").GetInt32()).IsEqualTo(0);
        var macroEntries = macroConstants.GetProperty("Entries").EnumerateArray().ToArray();
        await Assert.That(macroEntries.Length).IsEqualTo(1);
        var timerEntry = macroEntries[0];
        await Assert.That(timerEntry.GetProperty("Name").GetString()).IsEqualTo("SDL_INIT_TIMER");
        await Assert.That(timerEntry.GetProperty("Disposition").GetString()).IsEqualTo("included");
        await Assert.That(timerEntry.GetProperty("Reason").GetString()).IsEqualTo("manual-include");
        await Assert.That(timerEntry.GetProperty("EmittedType").GetString()).IsEqualTo("uint");
        await Assert.That(timerEntry.GetProperty("EmittedValue").GetString()).IsEqualTo("0x00000001u");
        await Assert.That(timerEntry.GetProperty("MacroForm").GetString()).IsEqualTo("manual");
        await Assert.That(timerEntry.GetProperty("Taxonomy").GetString()).IsEqualTo("manual-policy");
        await Assert.That(timerEntry.GetProperty("OriginalExpression").ValueKind).IsEqualTo(JsonValueKind.Null);
        await Assert.That(timerEntry.GetProperty("ComputedValue").ValueKind).IsEqualTo(JsonValueKind.Null);
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
                        new BindingStructField("name", NativeTypeRef.Indirection(
                            NativeTypeRef.Primitive("signed char", "sbyte", NativeAbiShape.Of("sbyte")),
                            indirectionDepth: 1,
                            managedName: "sbyte*"), FieldOffset: null),
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
    public async Task Emit_Should_Use_TfmSafe_Wrapper_For_Fixed_Array_Elements_That_CSharp_Fixed_Buffers_Do_Not_Support()
    {
        var model = new BindingModel(
            Views: [],
            Structs:
            [
                new BindingStruct(
                    Name: "SDL_ColorScheme",
                    Fields:
                    [
                        new BindingStructField("colors", BindingGenerationFixture.NativePrimitive("SDL_MessageBoxColor", "SDL_MessageBoxColor"), FieldOffset: null, FixedBufferLength: 5),
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
        await Assert.That(structsFile.Content).Contains("public SDL_ColorScheme_colors colors;");
        await Assert.That(structsFile.Content).Contains("[StructLayout(LayoutKind.Sequential)]");
        await Assert.That(structsFile.Content).Contains("public partial struct SDL_ColorScheme_colors");
        await Assert.That(structsFile.Content).Contains("public SDL_MessageBoxColor Element0;");
        await Assert.That(structsFile.Content).Contains("public SDL_MessageBoxColor Element4;");
        await Assert.That(structsFile.Content).DoesNotContain("using System.Runtime.CompilerServices;");
        await Assert.That(structsFile.Content).DoesNotContain("[InlineArray(5)]");
        await Assert.That(structsFile.Content).DoesNotContain("fixed SDL_MessageBoxColor colors[5];");
    }

    [Test]
    public async Task Emit_Should_Mark_FixedArray_Wrapper_Unsafe_When_ElementType_Is_Pointer()
    {
        var pointerType = NativeTypeRef.Indirection(
            NativeTypeRef.Primitive("signed char", "sbyte", NativeAbiShape.Of("sbyte")),
            indirectionDepth: 1,
            managedName: "sbyte*");
        var model = new BindingModel(
            Views: [],
            Structs:
            [
                new BindingStruct(
                    Name: "SDL_PointerTable",
                    Fields:
                    [
                        new BindingStructField("entries", pointerType, FieldOffset: null, FixedBufferLength: 2),
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
        await Assert.That(structsFile.Content).Contains("public SDL_PointerTable_entries entries;");
        await Assert.That(structsFile.Content).Contains("public partial struct SDL_PointerTable\n");
        await Assert.That(structsFile.Content).DoesNotContain("public unsafe partial struct SDL_PointerTable\n");
        await Assert.That(structsFile.Content).Contains("public unsafe partial struct SDL_PointerTable_entries");
        await Assert.That(structsFile.Content).Contains("public sbyte* Element0;");
        await Assert.That(structsFile.Content).Contains("public sbyte* Element1;");
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
                new BindingConstant("SDL_INIT_TIMER", BindingGenerationFixture.NativeUInt(), "0x00000001u", ConstantKind.Literal),
                new BindingConstant("SDL_INIT_EVERYTHING", BindingGenerationFixture.NativeUInt(), "SDL_INIT_TIMER | SDL_INIT_AUDIO", ConstantKind.Computed),
                new BindingConstant("SDL_HINT_RENDER_DRIVER", BindingGenerationFixture.NativePrimitive("ReadOnlySpan<byte>", "ReadOnlySpan<byte>"), "\"SDL_RENDER_DRIVER\"u8", ConstantKind.Literal),
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
                    UnderlyingType: BindingGenerationFixture.NativeUInt(),
                    IsFlags: true,
                    Members:
                    [
                        new BindingEnumMember("SDL_WINDOW_FULLSCREEN", "0x00000001u"),
                        new BindingEnumMember("SDL_WINDOW_OPENGL", "0x00000002u"),
                        new BindingEnumMember("SDL_WINDOW_MOUSE_GRABBED", "0x00000100u"),
                        new BindingEnumMember("SDL_WINDOW_FULLSCREEN_DESKTOP", "SDL_WINDOW_FULLSCREEN | 0x00001000u"),
                        new BindingEnumMember("SDL_WINDOW_INPUT_GRABBED", "SDL_WINDOW_MOUSE_GRABBED"),
                    ]),
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

    private static string[] JsonStrings(JsonElement array)
    {
        return [.. array.EnumerateArray().Select(element => element.GetString() ?? string.Empty)];
    }
}
