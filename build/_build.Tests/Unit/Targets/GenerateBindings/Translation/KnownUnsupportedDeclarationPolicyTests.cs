using System.Collections.Immutable;
using Build.Data.BindingGeneration.Models;
using Build.Targets.GenerateBindings.Translation;
using Build.Tests.Fixtures;
using CppAst;

namespace Build.Tests.Unit.Targets.GenerateBindings.Translation;

/// <summary>
/// Manifest-driven declaration-deferral policy. C-variadic functions are
/// intentionally NOT filtered here; SDL2-CS / Alimer.Bindings.SDL peer patterns
/// emit variadic functions fmt-only and expect managed-side pre-format. A
/// friendly-overload wrapper makes the pre-format expectation explicit.
/// </summary>
public sealed class KnownUnsupportedDeclarationPolicyTests
{
    [Test]
    public void Constructor_Should_Throw_When_Config_Is_Null()
    {
        Assert.Throws<ArgumentNullException>(() => new KnownUnsupportedDeclarationPolicy(null!));
    }

    [Test]
    public async Task IsUnsupported_Function_Should_Return_False_When_Manifest_Has_No_Matching_Deferred_Entry()
    {
        // Fixture config carries the live SDL2.Core deferred declarations; an
        // unrelated SDL function should not hit the deferred path.
        var policy = new KnownUnsupportedDeclarationPolicy(BindingGenerationFixture.Sdl2CoreConfig());
        var function = new CppFunction("SDL_CreateWindow");

        var unsupported = policy.IsUnsupported(function, out var reason);

        await Assert.That(unsupported).IsFalse();
        await Assert.That(reason).IsEqualTo(string.Empty);
    }

    [Test]
    public async Task IsUnsupported_Function_Should_Return_True_When_Name_Matches_Manifest_Deferred_Entry()
    {
        // Construct a config with SDL_Init marked deferred — this is the wire
        // shape the SDL_SysWMinfo / SDL_SysWMmsg manifest entries take, but for
        // a function-shaped declaration we use a function-named entry to
        // exercise the function overload of IsUnsupported.
        var config = BindingGenerationFixture.Sdl2CoreConfig() with
        {
            DeferredDeclarations = ImmutableDictionary<string, DeferredDeclarationConfig>.Empty
                .Add("SDL_Init", new DeferredDeclarationConfig
                {
                    Category = "test-defer",
                    Reason = "exercised by KnownUnsupportedDeclarationPolicyTests",
                }),
        };
        var policy = new KnownUnsupportedDeclarationPolicy(config);
        var function = new CppFunction("SDL_Init");

        var unsupported = policy.IsUnsupported(function, out var reason);

        await Assert.That(unsupported).IsTrue();
        await Assert.That(reason).Contains("test-defer");
        await Assert.That(reason).Contains("exercised by KnownUnsupportedDeclarationPolicyTests");
    }

    [Test]
    public void IsUnsupported_Function_Should_Throw_When_Function_Is_Null()
    {
        var policy = new KnownUnsupportedDeclarationPolicy(BindingGenerationFixture.Sdl2CoreConfig());

        Assert.Throws<ArgumentNullException>(() => policy.IsUnsupported((CppFunction)null!, out _));
    }

    [Test]
    public async Task IsUnsupported_Name_Should_Match_Manifest_Deferred_Entry()
    {
        // The string-overload path drives type / struct / enum deferral checks
        // — e.g. SDL_SysWMinfo deferred until typed-union shape is proven. Pins the
        // contract that BOTH overloads consult the same dictionary.
        var policy = new KnownUnsupportedDeclarationPolicy(BindingGenerationFixture.Sdl2CoreConfig() with
        {
            DeferredDeclarations = ImmutableDictionary<string, DeferredDeclarationConfig>.Empty
                .Add("SDL_SysWMinfo", new DeferredDeclarationConfig
                {
                    Category = "deferred-to-typed-union",
                    Reason = "SDL_syswm typed-union layout deferred.",
                }),
        });

        var unsupported = policy.IsUnsupported("SDL_SysWMinfo", out var reason);

        await Assert.That(unsupported).IsTrue();
        await Assert.That(reason).Contains("deferred-to-typed-union");
    }

    [Test]
    public async Task IsUnsupported_Name_Should_Return_False_For_Unknown_Declaration()
    {
        var policy = new KnownUnsupportedDeclarationPolicy(BindingGenerationFixture.Sdl2CoreConfig());

        var unsupported = policy.IsUnsupported("SDL_NoSuchType", out var reason);

        await Assert.That(unsupported).IsFalse();
        await Assert.That(reason).IsEqualTo(string.Empty);
    }

    [Test]
    public async Task IsUnsupported_Function_Should_NOT_Filter_Variadic_Functions()
    {
        // Per 2026-05-17 peer-evidence review (SDL2-CS / Alimer / ppy/SDL3-CS),
        // variadic functions are emitted as fmt-only raw P/Invoke — NOT filtered
        // by this policy. A follow-up SDL2-CS-style `string fmtAndArglist`
        // friendly-overload wrapper makes that explicit. This test pins the
        // policy so a future regression that re-adds variadic filtering surfaces
        // immediately.
        var policy = new KnownUnsupportedDeclarationPolicy(BindingGenerationFixture.Sdl2CoreConfig());
        var variadic = new CppFunction("SDL_Log") { Flags = CppFunctionFlags.Variadic };

        var unsupported = policy.IsUnsupported(variadic, out _);

        await Assert.That(unsupported).IsFalse();
    }

    [Test]
    public async Task IsUnsupported_Function_Should_Defer_Explicit_VaList_Parameter()
    {
        var policy = new KnownUnsupportedDeclarationPolicy(BindingGenerationFixture.Sdl2CoreConfig());
        var function = new CppFunction("SDL_LogMessageV");
        function.Parameters.Add(new CppParameter(
            new CppTypedef("va_list", new CppPointerType(new CppClass("__va_list_tag"))),
            "ap"));

        var unsupported = policy.IsUnsupported(function, out var reason);

        await Assert.That(unsupported).IsTrue();
        await Assert.That(reason).Contains("va_list");
    }

    [Test]
    public async Task IsUnsupported_Function_Should_Defer_FILE_Pointer_Parameter()
    {
        var policy = new KnownUnsupportedDeclarationPolicy(BindingGenerationFixture.Sdl2CoreConfig());
        var function = new CppFunction("SDL_RWFromFP");
        function.Parameters.Add(new CppParameter(
            new CppPointerType(new CppTypedef("FILE", new CppClass("_IO_FILE"))),
            "fp"));

        var unsupported = policy.IsUnsupported(function, out var reason);

        await Assert.That(unsupported).IsTrue();
        await Assert.That(reason).Contains("FILE");
    }
}
