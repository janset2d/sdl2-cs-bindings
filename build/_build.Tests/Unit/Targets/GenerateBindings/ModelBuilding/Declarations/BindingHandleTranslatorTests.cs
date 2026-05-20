using Build.Data.BindingGeneration.Models;
using Build.Targets.GenerateBindings.Model;
using Build.Targets.GenerateBindings.ModelBuilding.Declarations;
using Build.Targets.GenerateBindings.ModelBuilding.Types;
using Build.Tests.Fixtures;
using CppAst;

namespace Build.Tests.Unit.Targets.GenerateBindings.ModelBuilding.Declarations;

public sealed class BindingHandleTranslatorTests
{
    private static readonly BindingGenerationConfig DefaultConfig = BindingGenerationFixture.Sdl2CoreConfig();

    [Test]
    public async Task Extract_Should_Create_Handles_From_Opaque_Owned_Structs()
    {
        var translator = CreateTranslator();
        var window = new CppClass("SDL_Window")
        {
            ClassKind = CppClassKind.Struct,
            IsDefinition = false,
            Span = SdlHeaderSpan("SDL_video.h"),
        };
        var rect = new CppClass("SDL_Rect")
        {
            ClassKind = CppClassKind.Struct,
            IsDefinition = true,
            Span = SdlHeaderSpan("SDL_rect.h"),
        };
        rect.Fields.Add(new CppField(CppPrimitiveType.Int, "x"));
        var external = new CppClass("VkInstance_T")
        {
            ClassKind = CppClassKind.Struct,
            IsDefinition = false,
            Span = SdlHeaderSpan("SDL_vulkan.h"),
        };
        var nonSdlHeader = new CppClass("SDL_Pretender")
        {
            ClassKind = CppClassKind.Struct,
            IsDefinition = false,
            Span = HeaderSpan("C:/vendor/include/not-sdl/SDL_video.h"),
        };

        var handles = translator.Extract(new NativeDeclarationCatalog([], [window, rect, external, nonSdlHeader], [], []));

        var handle = await Assert.That(handles).HasSingleItem();
        await Assert.That(handle.Name).IsEqualTo("SDL_Window");
        await Assert.That(handle.Type.Kind).IsEqualTo(NativeTypeKind.OpaqueHandle);
        await Assert.That(handle.Type.SourceHeader).IsEqualTo("SDL_video.h");
        await Assert.That(handle.Type.OwningFamilyId).IsEqualTo("sdl2-core");
    }

    [Test]
    public async Task Extract_Should_Create_Handle_From_Private_Tag_Typedef()
    {
        var translator = CreateTranslator();
        var privateTag = new CppClass("_SDL_GameController")
        {
            ClassKind = CppClassKind.Struct,
            IsDefinition = false,
            Span = SdlHeaderSpan("SDL_gamecontroller.h"),
        };
        var gameController = new CppTypedef("SDL_GameController", privateTag)
        {
            Span = SdlHeaderSpan("SDL_gamecontroller.h"),
        };

        var handles = translator.Extract(new NativeDeclarationCatalog([], [], [], [gameController]));

        var handle = await Assert.That(handles).HasSingleItem();
        await Assert.That(handle.Name).IsEqualTo("SDL_GameController");
        await Assert.That(handle.Type.Kind).IsEqualTo(NativeTypeKind.OpaqueHandle);
        await Assert.That(handle.Type.NativeName).IsEqualTo("SDL_GameController");
        await Assert.That(handle.Type.ManagedName).IsEqualTo("SDL_GameController");
    }

    [Test]
    public async Task Extract_Should_Prefer_Public_Typedef_Name_Over_Opaque_Struct_Tag()
    {
        var translator = CreateTranslator();
        var hidTag = new CppClass("SDL_hid_device_")
        {
            ClassKind = CppClassKind.Struct,
            IsDefinition = false,
            Span = SdlHeaderSpan("SDL_hidapi.h"),
        };
        var hidTypedef = new CppTypedef("SDL_hid_device", hidTag)
        {
            Span = SdlHeaderSpan("SDL_hidapi.h"),
        };
        var semaphoreTag = new CppClass("SDL_semaphore")
        {
            ClassKind = CppClassKind.Struct,
            IsDefinition = false,
            Span = SdlHeaderSpan("SDL_mutex.h"),
        };
        var semaphoreTypedef = new CppTypedef("SDL_sem", semaphoreTag)
        {
            Span = SdlHeaderSpan("SDL_mutex.h"),
        };

        var handles = translator.Extract(new NativeDeclarationCatalog([], [hidTag, semaphoreTag], [], [hidTypedef, semaphoreTypedef]));

        await Assert.That(handles.Select(handle => handle.Name).ToArray())
            .IsEquivalentTo(["SDL_hid_device", "SDL_sem"]);
    }

    [Test]
    public async Task Extract_Should_Exclude_Deferred_Handle_Declarations()
    {
        var translator = CreateTranslator();
        var sysWmInfo = new CppClass("SDL_SysWMinfo")
        {
            ClassKind = CppClassKind.Struct,
            IsDefinition = false,
            Span = SdlHeaderSpan("SDL_syswm.h"),
        };
        var window = new CppClass("SDL_Window")
        {
            ClassKind = CppClassKind.Struct,
            IsDefinition = false,
            Span = SdlHeaderSpan("SDL_video.h"),
        };

        var handles = translator.Extract(new NativeDeclarationCatalog([], [sysWmInfo, window], [], []));

        var handle = await Assert.That(handles).HasSingleItem();
        await Assert.That(handle.Name).IsEqualTo("SDL_Window");
    }

    private static BindingHandleTranslator CreateTranslator(BindingGenerationConfig? config = null)
    {
        config ??= DefaultConfig;
        return new(CreatePolicy(config), new NativeTypeClassifier(NativeTypeClassificationContext.FromConfig(config)));
    }

    private static BindableDeclarationPolicy CreatePolicy(BindingGenerationConfig config) =>
        new(config, new KnownUnsupportedDeclarationPolicy(config));

    private static CppSourceSpan SdlHeaderSpan(string headerName) =>
        HeaderSpan($"C:/vcpkg/installed/x64-linux-hybrid/include/SDL2/{headerName}");

    private static CppSourceSpan HeaderSpan(string sourceFile) =>
        new(
            new CppSourceLocation(sourceFile, 0, 1, 1),
            new CppSourceLocation(sourceFile, 1, 1, 2));
}
