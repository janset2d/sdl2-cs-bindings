using System.Text.Json;
using Build.Data.BindingGeneration;
using Build.Data.BindingGeneration.Models;
using Build.Data.Manifest;
using Build.Targets.GenerateBindings.Oracle;

namespace Build.Tests.Fixtures;

internal static class OracleFamilyFixture
{
    public static BindingGenerationConfig RequiredProfile => JsonSerializer.Deserialize<BindingGenerationConfig>(
        FixtureLoader.Load("BindingGeneration/Oracle/required-profile.json"))!;

    public static FamilyConfig Sdl2Core => Config(Profile("sdl2-core"));
    public static FamilyConfig Sdl2Image => Config(Profile("sdl2-image"));
    public static FamilyConfig Sdl2Ttf => Config(Profile("sdl2-ttf"));
    public static FamilyConfig Sdl2Mixer => Config(Profile("sdl2-mixer"));
    public static FamilyConfig Sdl2Gfx => Config(Profile("sdl2-gfx"));

    public static FamilyConfig Config(BindingGenerationConfig profile)
        => FamilyConfig.FromProfile(profile, FakeCakeWorld.CreateLinux().CreateBuildContext().Paths);

    public static BindingGenerationConfig Profile(string familyId)
    {
        var world = FakeCakeWorld.CreateLinux().WithManifestObject(ManifestFixture.RealManifest);
        return Repository(world).Load(familyId).Value;
    }

    public static BindingGenerationConfigRepository Repository(FakeCakeWorld world)
        => new(new ManifestRepository(world.CakeContext, world.RepoRoot.CombineWithFilePath("build/manifest.json")));
}
