using Build.Data.BindingGeneration;
using Build.Data.BindingGeneration.Models;
using Build.Results;
using Build.Targets.GenerateBindings.Model;

namespace Build.Tests.Fixtures;

/// <summary>
/// Lightweight builders for <see cref="BindingGenerationConfig"/> +
/// <see cref="BindingModel"/> shapes used by validator tests. Keep
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
        IReadOnlyList<RequiredFunctionConfig>? requiredFunctions = null) =>
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
            ExcludedFunctions = ["SDL_main", "SDL_DYNAPI_entry"],
            RequiredFunctions = requiredFunctions is null ? [] : [.. requiredFunctions],
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

    public static RequiredFunctionConfig RequiredFunction(string name, string sourceHeader = "SDL.h") =>
        new()
        {
            Name = name,
            ReturnType = "void",
            SourceHeader = sourceHeader,
            Parameters = [],
        };

    public static BindingModel ModelWithNeutralFunctions(params string[] functionNames) =>
        new([
            new BindingParseView(
                Name: "Neutral",
                SupportedOsPlatform: null,
                Functions: [.. functionNames.Select(n => new BindingFunction(n, "void", [], "SDL_video.h"))]),
        ]);

    public static BindingModel ModelWithoutNeutralView() =>
        new([
            new BindingParseView(
                Name: "WindowsDesktop",
                SupportedOsPlatform: "windows",
                Functions: [new BindingFunction("SDL_RegisterApp", "int", [], "SDL_main.h")]),
        ]);

    public static BindingModel ModelWithMultipleViews(
        IReadOnlyList<string> neutralFunctionNames,
        IReadOnlyList<string>? windowsFunctionNames = null,
        IReadOnlyList<string>? linuxFunctionNames = null) =>
        new([
            new BindingParseView("Neutral", null,
                [.. neutralFunctionNames.Select(n => new BindingFunction(n, "void", [], "SDL_video.h"))]),
            new BindingParseView("WindowsDesktop", "windows",
                [.. (windowsFunctionNames ?? []).Select(n => new BindingFunction(n, "void", [], "SDL_system.h"))]),
            new BindingParseView("Linux", "linux",
                [.. (linuxFunctionNames ?? []).Select(n => new BindingFunction(n, "void", [], "SDL_system.h"))]),
        ]);
}

/// <summary>
/// Synchronously-returning fake <see cref="IDynapiManifestRepository"/> for
/// validator tests. Avoids the round-trip-test ceremony of seeding a real
/// vcpkg buildtree fake filesystem — validators only care about the
/// resolved <see cref="DynapiManifest"/> payload.
/// </summary>
public sealed class FakeDynapiManifestRepository(IReadOnlySet<string>? publicSymbols, ManifestResolutionError? error = null) : IDynapiManifestRepository
{
    private readonly IReadOnlySet<string> _publicSymbols = publicSymbols ?? new HashSet<string>(StringComparer.Ordinal);
    private readonly ManifestResolutionError? _error = error;

    public Task<Result<DynapiManifest, ManifestResolutionError>> LoadAsync(CancellationToken ct = default)
    {
        if (_error is not null)
        {
            return Task.FromResult(Result<DynapiManifest, ManifestResolutionError>.Failure(_error));
        }

        var manifest = new DynapiManifest(
            PublicSymbols: _publicSymbols,
            Origin: DynapiManifestOrigin.VcpkgBuildtree,
            SourcePath: "/fake/SDL2.exports");
        return Task.FromResult(Result<DynapiManifest, ManifestResolutionError>.Success(manifest));
    }
}
