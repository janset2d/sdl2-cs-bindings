using System.Collections.Immutable;
using Build.Data.BindingGeneration.Models;
using Build.Targets.GenerateBindings.Model;
using Build.Targets.GenerateBindings.ModelBuilding.Declarations;
using Build.Targets.GenerateBindings.ModelBuilding.Types;
using Build.Tests.Fixtures;
using CppAst;

namespace Build.Tests.Unit.Targets.GenerateBindings.ModelBuilding.Declarations;

public sealed class BindingCallbackTranslatorTests
{
    private static readonly BindingGenerationConfig DefaultConfig = BindingGenerationFixture.Sdl2CoreConfig();

    [Test]
    public async Task Extract_Should_Create_Callback_From_Function_Pointer_Typedef()
    {
        var translator = CreateTranslator();
        var functionType = new CppFunctionType(CppPrimitiveType.Void);
        functionType.Parameters.Add(new CppParameter(new CppPointerType(CppPrimitiveType.Void), "userdata"));
        functionType.Parameters.Add(new CppParameter(new CppPointerType(new CppTypedef("Uint8", CppPrimitiveType.UnsignedChar)), "stream"));
        functionType.Parameters.Add(new CppParameter(CppPrimitiveType.Int, "len"));
        var callback = new CppTypedef("SDL_AudioCallback", new CppPointerType(functionType))
        {
            Span = SdlHeaderSpan("SDL_audio.h"),
        };
        var external = new CppTypedef("VkCallback", new CppPointerType(functionType))
        {
            Span = SdlHeaderSpan("SDL_vulkan.h"),
        };
        var nonSdlHeader = new CppTypedef("SDL_PretenderCallback", new CppPointerType(functionType))
        {
            Span = HeaderSpan("C:/vendor/include/not-sdl/SDL_audio.h"),
        };

        var callbacks = translator.Extract([callback, external, nonSdlHeader, new CppTypedef("Uint8", CppPrimitiveType.UnsignedChar)]);

        var bindingCallback = await Assert.That(callbacks).HasSingleItem();
        await Assert.That(bindingCallback.Name).IsEqualTo("SDL_AudioCallback");
        await Assert.That(bindingCallback.ReturnType.ManagedName).IsEqualTo("void");
        await Assert.That(string.Join(',', bindingCallback.Parameters.Select(parameter => parameter.Name)))
            .IsEqualTo("userdata,stream,len");
        await Assert.That(string.Join(',', bindingCallback.Parameters.Select(parameter => parameter.Type.ManagedName)))
            .IsEqualTo("nint,byte*,int");
    }

    [Test]
    public async Task Extract_Should_Exclude_Deferred_Callback_Typedefs()
    {
        var config = DefaultConfig with
        {
            DeferredDeclarations = ImmutableDictionary<string, DeferredDeclarationConfig>.Empty
                .Add("SDL_AudioCallback", new DeferredDeclarationConfig
                {
                    Category = "test-defer",
                    Reason = "exercised by callback translator test.",
                }),
        };
        var translator = CreateTranslator(config);
        var functionType = new CppFunctionType(CppPrimitiveType.Void);
        var deferredCallback = new CppTypedef("SDL_AudioCallback", new CppPointerType(functionType))
        {
            Span = SdlHeaderSpan("SDL_audio.h"),
        };
        var eventFilter = new CppTypedef("SDL_EventFilter", new CppPointerType(functionType))
        {
            Span = SdlHeaderSpan("SDL_events.h"),
        };

        var callbacks = translator.Extract([deferredCallback, eventFilter]);

        var callback = await Assert.That(callbacks).HasSingleItem();
        await Assert.That(callback.Name).IsEqualTo("SDL_EventFilter");
    }

    private static BindingCallbackTranslator CreateTranslator(BindingGenerationConfig? config = null)
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
