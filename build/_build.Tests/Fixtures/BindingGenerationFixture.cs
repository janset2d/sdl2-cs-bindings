using Build.Data.BindingGeneration.Models;
using Build.Data.Manifest.Models;

namespace Build.Tests.Fixtures;

/// <summary>Typed family policy builders for focused validator and resolver tests.</summary>
public static class BindingGenerationFixture
{
    public static void SeedGenerationInputs(FakeCakeWorld world, ManifestConfig manifest)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(manifest);
        var settings = manifest.BindingGeneration;
        world
            .WithTextFile(settings.BaseRsp, "/* base response */")
            .WithTextFile(settings.PerHeaderRspDir + "/fixture.rsp", "/* response */")
            .WithTextFile(settings.PlatformHeaderShimsDir + "/fixture.h", "/* shim */");

        foreach (var library in manifest.LibraryManifests.Where(library => library.BindingGeneration.Enabled))
        {
            world
                .WithTextFile(library.BindingGeneration.Rsp, "/* family response */")
                .WithTextFile(library.BindingGeneration.GenerationProject, "<Project />");
        }
    }

    public static BindingGenerationConfig Sdl2CoreConfig(
        bool enabled = true,
        bool withDynapi = true,
        IReadOnlyList<RequiredFunctionConfig>? requiredFunctions = null,
        IReadOnlyList<RequiredConstantConfig>? requiredConstants = null) =>
        ManifestFixture.RealCoreLibrary.BindingGeneration with
        {
            FamilyId = "sdl2-core",
            Enabled = enabled,
            RequiredFunctions = requiredFunctions is null ? [] : [.. requiredFunctions],
            RequiredConstants = requiredConstants is null ? [] : [.. requiredConstants],
            Dynapi = withDynapi ? new DynapiConfig() : null,
        };

    public static BindingGenerationConfig Sdl2ImagePlaceholderConfig() =>
        ManifestFixture.RealSatelliteLibrary.BindingGeneration with { FamilyId = "sdl2-image", Enabled = false };

    public static BindingGenerationConfig CreateWithDefaultExcludedFunctions(BindingGenerationConfig source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return new BindingGenerationConfig
        {
            FamilyId = source.FamilyId,
            Enabled = source.Enabled,
            ManagedNamespace = source.ManagedNamespace,
            PrimaryClassName = source.PrimaryClassName,
            OwnedPrefixes = source.OwnedPrefixes,
            GenerationProject = source.GenerationProject,
            Rsp = source.Rsp,
            OwnerMode = source.OwnerMode,
            PlatformSensitiveHeaders = source.PlatformSensitiveHeaders,
            OpaqueHandles = source.OpaqueHandles,
            FlagsEnums = source.FlagsEnums,
            ClongMethods = source.ClongMethods,
            RequiredFunctions = source.RequiredFunctions,
            RequiredConstants = source.RequiredConstants,
            Validators = source.Validators,
            HeaderSet = source.HeaderSet,
            Dynapi = source.Dynapi,
        };
    }

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
