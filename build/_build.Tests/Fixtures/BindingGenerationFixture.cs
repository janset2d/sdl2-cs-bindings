using System.Collections.Immutable;
using Build.Data.BindingGeneration.Models;

namespace Build.Tests.Fixtures;

/// <summary>
/// Lightweight builders for <see cref="BindingGenerationConfig"/> used by validator tests. Keep
/// surface narrow — fixture grows when validator coverage grows.
/// </summary>
public static class BindingGenerationFixture
{
    /// <summary>
    /// Test config mirroring the SDL2.Core block in <c>build/manifest.json</c>. All
    /// fields match the live manifest values so any test resolved through this fixture
    /// exercises the same parse-defines / clang-args / header-set surface that
    /// production uses. Optional knobs override individual concerns for narrow tests.
    /// </summary>
    public static BindingGenerationConfig Sdl2CoreConfig(
        bool enabled = true,
        bool withDynapi = true,
        IReadOnlyList<RequiredFunctionConfig>? requiredFunctions = null,
        IReadOnlyList<RequiredConstantConfig>? requiredConstants = null) =>
        new()
        {
            FamilyId = "sdl2-core",
            Enabled = enabled,
            ManagedNamespace = "SDL2",
            PrimaryClassName = "SDL",
            PlatformCatalogId = "sdl2-core",
            OwnedPrefixes = ["SDL_", "SDLK_", "SDL_HINT_", "SDL_INIT_"],
            ParseDefines =
            [
                "SDL_DECLSPEC=",
                "SDL_DISABLE_IMMINTRIN_H=1",
                "SDL_DISABLE_MMINTRIN_H=1",
                "SDL_DISABLE_XMMINTRIN_H=1",
                "SDL_DISABLE_EMMINTRIN_H=1",
                "SDL_DISABLE_PMMINTRIN_H=1",
                "SDL_DISABLE_MM3DNOW_H=1",
                "SDL_DISABLE_LSX_H=1",
                "SDL_DISABLE_LASX_H=1",
                "SDL_DISABLE_ARM_NEON_H=1",
            ],
            ClangArgs = ["-fdeclspec", "-U__has_builtin"],
            HeaderSet = new HeaderSetConfig
            {
                IncludeDirGlob = "include/SDL2",
                HeaderGlob = "*.h",
                ExcludedHeaders =
                [
                    "SDL.h",
                    "begin_code.h",
                    "close_code.h",
                    "SDL_opengl.h",
                    "SDL_opengl_glext.h",
                    "SDL_opengles.h",
                    "SDL_opengles2.h",
                    "SDL_opengles2_gl2.h",
                    "SDL_opengles2_gl2ext.h",
                    "SDL_opengles2_gl2platform.h",
                    "SDL_opengles2_khrplatform.h",
                    "SDL_egl.h",
                    "SDL_image.h",
                    "SDL_mixer.h",
                    "SDL_net.h",
                    "SDL_ttf.h",
                ],
                ExcludedHeaderPrefixes = ["SDL_test", "SDL2_"],
            },
            ExcludedFunctions = [with(StringComparer.Ordinal), "SDL_main", "SDL_DYNAPI_entry"],
            RequiredFunctions = requiredFunctions is null ? [] : [.. requiredFunctions],
            RequiredConstants = requiredConstants is null ? [] : [.. requiredConstants],
            MacroConstants = new MacroConstantPolicyConfig(),
            DeferredDeclarations = Sdl2CoreDeferredDeclarations,
            Validators = ImmutableDictionary<string, bool>.Empty
                .Add("dynapi-coherence", true)
                .Add("neutral-view-non-empty", true)
                .Add("required-functions-emitted", true)
                .Add("semantic-type-consistency", true),
            Dynapi = withDynapi ? new DynapiConfig { ExportsGlob = "buildtrees/sdl2/src/*/src/dynapi/SDL2.exports" } : null,
        };

    public static BindingGenerationConfig Sdl2ImagePlaceholderConfig() =>
        new()
        {
            FamilyId = "sdl2-image",
            Enabled = false,
            ManagedNamespace = "SDL2.Image",
            PrimaryClassName = "SDL_image",
            PlatformCatalogId = "sdl2-image",
            OwnedPrefixes = ["IMG_"],
        };

    public static BindingGenerationConfig CreateWithDefaultExcludedFunctions(BindingGenerationConfig source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return new BindingGenerationConfig
        {
            FamilyId = source.FamilyId,
            Enabled = source.Enabled,
            ManagedNamespace = source.ManagedNamespace,
            PrimaryClassName = source.PrimaryClassName,
            PlatformCatalogId = source.PlatformCatalogId,
            OwnedPrefixes = source.OwnedPrefixes,
            ParseDefines = source.ParseDefines,
            ClangArgs = source.ClangArgs,
            RequiredFunctions = source.RequiredFunctions,
            RequiredConstants = source.RequiredConstants,
            MacroConstants = source.MacroConstants,
            DeferredDeclarations = source.DeferredDeclarations,
            Validators = source.Validators,
            HeaderSet = source.HeaderSet,
            Dynapi = source.Dynapi,
        };
    }

    private static ImmutableDictionary<string, DeferredDeclarationConfig> Sdl2CoreDeferredDeclarations { get; } =
        ImmutableDictionary<string, DeferredDeclarationConfig>.Empty
            .Add("SDL_SysWMinfo", new DeferredDeclarationConfig
            {
                Category = "deferred-to-stage-2",
                Reason = "SDL_syswm typed-union layout requires platform-handle forward declarations.",
            })
            .Add("SDL_SysWMmsg", new DeferredDeclarationConfig
            {
                Category = "deferred-to-stage-2",
                Reason = "Same as SDL_SysWMinfo.",
            })
            .Add("SDL_DUMMY_ENUM", new DeferredDeclarationConfig
            {
                Category = "internal-sdl-sentinel",
                Reason = "SDL compile-time enum-size sentinel, not public API.",
            });

    public static RequiredFunctionConfig RequiredFunction(string name, string sourceHeader = "SDL.h") =>
        new()
        {
            Name = name,
            ReturnType = "void",
            SourceHeader = sourceHeader,
            Parameters = [],
        };

    public static RequiredConstantConfig RequiredConstant(
        string name,
        string type = "uint",
        string value = "0x00000001u",
        string sourceHeader = "SDL.h",
        ConstantKind kind = ConstantKind.Literal) =>
        new()
        {
            Name = name,
            Type = type,
            Value = value,
            SourceHeader = sourceHeader,
            Kind = kind,
        };

}
