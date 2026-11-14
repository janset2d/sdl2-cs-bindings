using Build.Targets.GenerateBindings.Oracle;

namespace Build.Tests.Unit.Targets.GenerateBindings.Oracle;

public sealed class FamilyConfigsTests
{
    [Test]
    [Arguments("sdl2-ttf", "SDL2.Ttf", "SDL_ttfNative", "external/sdl2-cs/src/SDL2_ttf.cs")]
    [Arguments("sdl2-mixer", "SDL2.Mixer", "SDL_mixerNative", "external/sdl2-cs/src/SDL2_mixer.cs")]
    [Arguments("sdl2-gfx", "SDL2.Gfx", "SDL2_gfxNative", "external/sdl2-cs/src/SDL2_gfx.cs")]
    public async Task FamilyConfigs_Should_Preserve_Satellite_Identities_Without_Core_Dynapi(
        string familyId, string expectedNamespace, string rawClass, string peerPath)
    {
        var family = familyId switch
        {
            "sdl2-ttf" => FamilyConfigs.Sdl2Ttf,
            "sdl2-mixer" => FamilyConfigs.Sdl2Mixer,
            "sdl2-gfx" => FamilyConfigs.Sdl2Gfx,
            _ => throw new ArgumentOutOfRangeException(nameof(familyId))
        };

        await Assert.That(family.FamilyId).IsEqualTo(familyId);
        await Assert.That(family.ExpectedNamespace).IsEqualTo(expectedNamespace);
        await Assert.That(family.ExpectedRawClassName).IsEqualTo(rawClass);
        await Assert.That(family.Sdl2CsRelativePath).IsEqualTo(peerPath);
        await Assert.That(family.UsesSdl2Dynapi).IsFalse();
    }
}
