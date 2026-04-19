using Build.Domain.Preflight;

namespace Build.Tests.Unit.Domain.Preflight;

public class FamilyIdentifierConventionsTests
{
    [Test]
    [Arguments("sdl2-core", "Janset.SDL2.Core")]
    [Arguments("sdl2-image", "Janset.SDL2.Image")]
    [Arguments("sdl2-mixer", "Janset.SDL2.Mixer")]
    [Arguments("sdl3-core", "Janset.SDL3.Core")]
    public async Task ManagedPackageId_Matches_Canonical_Format(string familyIdentifier, string expected)
    {
        var actual = FamilyIdentifierConventions.ManagedPackageId(familyIdentifier);
        await Assert.That(actual).IsEqualTo(expected);
    }

    [Test]
    [Arguments("sdl2-core", "Janset.SDL2.Core.Native")]
    [Arguments("sdl2-image", "Janset.SDL2.Image.Native")]
    [Arguments("sdl3-gfx", "Janset.SDL3.Gfx.Native")]
    public async Task NativePackageId_Matches_Canonical_Format(string familyIdentifier, string expected)
    {
        var actual = FamilyIdentifierConventions.NativePackageId(familyIdentifier);
        await Assert.That(actual).IsEqualTo(expected);
    }

    [Test]
    [Arguments("sdl2-core", "JansetSdl2CorePackageVersion")]
    [Arguments("sdl2-image", "JansetSdl2ImagePackageVersion")]
    [Arguments("sdl2-mixer", "JansetSdl2MixerPackageVersion")]
    [Arguments("sdl2-ttf", "JansetSdl2TtfPackageVersion")]
    [Arguments("sdl2-gfx", "JansetSdl2GfxPackageVersion")]
    [Arguments("sdl2-net", "JansetSdl2NetPackageVersion")]
    [Arguments("sdl3-core", "JansetSdl3CorePackageVersion")]
    public async Task VersionPropertyName_Matches_Canonical_Format(string familyIdentifier, string expected)
    {
        var actual = FamilyIdentifierConventions.VersionPropertyName(familyIdentifier);
        await Assert.That(actual).IsEqualTo(expected);
    }

    [Test]
    public async Task VersionPropertyName_Aligns_With_Janset_Smoke_Props_Declarations()
    {
        // The shared build/msbuild/Janset.Smoke.props declares version properties using this
        // exact convention; the smoke runner injects -p:<name>=<version>. If this test
        // drifts, the MSBuild guards won't match the runner's injected properties.
        var expectations = new (string family, string property)[]
        {
            ("sdl2-core", "JansetSdl2CorePackageVersion"),
            ("sdl2-image", "JansetSdl2ImagePackageVersion"),
            ("sdl2-mixer", "JansetSdl2MixerPackageVersion"),
            ("sdl2-ttf", "JansetSdl2TtfPackageVersion"),
            ("sdl2-gfx", "JansetSdl2GfxPackageVersion"),
            ("sdl2-net", "JansetSdl2NetPackageVersion"),
        };

        foreach (var (family, property) in expectations)
        {
            var actual = FamilyIdentifierConventions.VersionPropertyName(family);
            await Assert.That(actual).IsEqualTo(property);
        }
    }
}
