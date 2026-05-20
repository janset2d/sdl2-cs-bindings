using Build.Tests.Fixtures;
using CppAst;

namespace Build.Tests.Unit.Targets.GenerateBindings.Parse;

[LinuxOnly]
public sealed class CppAstFixtureMatrixTests
{
    private static readonly Lazy<CppCompilation> Matrix = new(ParseMatrixHeader);

    [Test]
    public async Task Parse_Should_Model_Unnamed_Parameters_With_Empty_Names()
    {
        var function = Function("SDL_ReportAssertion");

        await Assert.That(function.Parameters.Select(p => p.Name).ToArray())
            .IsEquivalentTo(["", "", "", ""]);
    }

    [Test]
    public async Task Parse_Should_Model_SDL2_Bool_As_Int_Sized_Enum_Typedef()
    {
        var enumType = Enum("SDL_bool");

        await Assert.That(enumType.SizeOf).IsEqualTo(sizeof(int));
        await Assert.That(enumType.Items.Select(item => item.Name).ToArray())
            .IsEquivalentTo(["SDL_FALSE", "SDL_TRUE"]);
    }

    [Test]
    public async Task Parse_Should_Model_C_Variadic_Function_With_Flag_Not_Ellipsis_Parameter()
    {
        var function = Function("SDL_Log");

        await Assert.That(function.Flags.HasFlag(CppFunctionFlags.Variadic)).IsTrue();
        await Assert.That(function.Parameters.Count).IsEqualTo(1);
        await Assert.That(function.Parameters[0].Name).IsEqualTo("fmt");
    }

    [Test]
    public async Task Parse_Should_Model_Explicit_VaList_As_Typedef_Parameter()
    {
        var ap = Function("SDL_LogMessageV").Parameters.Single(p => p.Name == "ap");
        var typedef = await AssertType<CppTypedef>(ap.Type);

        await Assert.That(typedef.Name).IsEqualTo("va_list");
    }

    [Test]
    public async Task Parse_Should_Model_FILE_As_Pointer_To_Typedef()
    {
        var fp = Function("SDL_RWFromFP").Parameters.Single(p => p.Name == "fp");
        var pointer = await AssertType<CppPointerType>(fp.Type);
        var typedef = await AssertType<CppTypedef>(pointer.ElementType);

        await Assert.That(typedef.Name).IsEqualTo("FILE");
    }

    [Test]
    public async Task Parse_Should_Model_SDL_Opaque_Handle_As_Forward_Declared_Struct()
    {
        var window = Class("SDL_Window");

        await Assert.That(window.ClassKind).IsEqualTo(CppClassKind.Struct);
        await Assert.That(window.IsDefinition).IsFalse();
        await Assert.That(window.Fields.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Parse_Should_Model_SDL_GUID_As_Defined_POD_Struct()
    {
        var guid = Class("SDL_GUID");

        await Assert.That(guid.ClassKind).IsEqualTo(CppClassKind.Struct);
        await Assert.That(guid.IsDefinition).IsTrue();
        await Assert.That(guid.Fields.Single().Name).IsEqualTo("data");
    }

    [Test]
    public async Task Parse_Should_Model_ButtonBind_As_Union()
    {
        var bind = Class("SDL_GameControllerButtonBind");

        await Assert.That(bind.ClassKind).IsEqualTo(CppClassKind.Union);
        await Assert.That(bind.IsDefinition).IsTrue();
        await Assert.That(bind.Fields.Select(field => field.Name).ToArray())
            .IsEquivalentTo(["button", "axis"]);
    }

    [Test]
    public async Task Parse_Should_Model_Vulkan_Handles_As_Distinct_Dispatchable_And_NonDispatchable_Shapes()
    {
        var function = Function("SDL_Vulkan_CreateSurface");
        var instance = function.Parameters.Single(p => p.Name == "instance");
        var surface = function.Parameters.Single(p => p.Name == "surface");

        var instanceTypedef = await AssertType<CppTypedef>(instance.Type);
        var surfacePointer = await AssertType<CppPointerType>(surface.Type);
        var surfaceTypedef = await AssertType<CppTypedef>(surfacePointer.ElementType);

        await Assert.That(instanceTypedef.Name).IsEqualTo("VkInstance");
        await Assert.That(surfaceTypedef.Name).IsEqualTo("VkSurfaceKHR");
    }

    [Test]
    public async Task Parse_Should_Model_GDK_Handles_As_Pointers_To_Explicit_Platform_Typedefs()
    {
        var userHandle = Function("SDL_GDKGetDefaultUser").Parameters.Single(p => p.Name == "outUserHandle");
        var taskQueue = Function("SDL_GDKGetTaskQueue").Parameters.Single(p => p.Name == "outTaskQueue");

        await Assert.That(PointerTypedefName(userHandle.Type)).IsEqualTo("XUserHandle");
        await Assert.That(PointerTypedefName(taskQueue.Type)).IsEqualTo("XTaskQueueHandle");
    }

    private static string PointerTypedefName(CppType type)
    {
        var pointer = type as CppPointerType
            ?? throw new InvalidOperationException($"Expected pointer type, got '{type.GetType().Name}'.");
        var typedef = pointer.ElementType as CppTypedef
            ?? throw new InvalidOperationException($"Expected typedef pointee, got '{pointer.ElementType.GetType().Name}'.");
        return typedef.Name;
    }

    private static CppFunction Function(string name) =>
        Matrix.Value.Functions.Single(function => string.Equals(function.Name, name, StringComparison.Ordinal));

    private static CppClass Class(string name) =>
        Matrix.Value.Classes.Single(type => string.Equals(type.Name, name, StringComparison.Ordinal));

    private static CppEnum Enum(string name) =>
        Matrix.Value.Enums.Single(type => string.Equals(type.Name, name, StringComparison.Ordinal));

    private static async Task<T> AssertType<T>(CppType type)
        where T : CppType
    {
        await Assert.That(type).IsTypeOf<T>();
        return (T)type;
    }

    private static CppCompilation ParseMatrixHeader()
    {
        var directory = Path.Combine(Path.GetTempPath(), "janset-cppast-fixtures", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var header = Path.Combine(directory, "fixture-matrix.h");
        try
        {
            File.WriteAllText(header, FixtureLoader.Load("GenerateBindings/cppast-fixture-matrix.h"));

            var options = new CppParserOptions
            {
                ParserKind = CppParserKind.C,
                TargetSystem = "linux",
                ParseMacros = false,
            };

            var compilation = CppParser.ParseFile(header, options);
            if (compilation.HasErrors)
            {
                throw new InvalidOperationException(compilation.Diagnostics.ToString());
            }

            return compilation;
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
