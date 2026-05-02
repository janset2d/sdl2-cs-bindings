using System.Collections.Immutable;
using Build.Tests.Fixtures;

namespace Build.Tests.Unit.Shared.Manifest;

public class ManifestConfigCoreLibraryTests
{
    [Test]
    public async Task CoreLibrary_Returns_The_Single_Core_Library_Entry()
    {
        var manifest = ManifestFixture.CreateTestManifestConfig();

        var core = manifest.CoreLibrary;

        await Assert.That(core.IsCoreLib).IsTrue();
        await Assert.That(core.VcpkgName).IsEqualTo("sdl2");
        await Assert.That(core.Name).IsEqualTo("SDL2");
    }

    [Test]
    public async Task CoreLibrary_Throws_When_No_Core_Library_Declared()
    {
        var baseline = ManifestFixture.CreateTestManifestConfig();
        var nonCoreOnly = baseline with
        {
            LibraryManifests = ImmutableList.Create(ManifestFixture.CreateTestSatelliteLibrary()),
        };

        await Assert.That(() => nonCoreOnly.CoreLibrary).Throws<InvalidOperationException>();
    }

    [Test]
    public async Task CoreLibrary_Throws_When_Multiple_Core_Libraries_Declared()
    {
        var baseline = ManifestFixture.CreateTestManifestConfig();
        var duplicated = baseline with
        {
            LibraryManifests = ImmutableList.Create(
                ManifestFixture.CreateTestCoreLibrary(name: "SDL2", vcpkgName: "sdl2"),
                ManifestFixture.CreateTestCoreLibrary(name: "SDL3", vcpkgName: "sdl3")),
        };

        await Assert.That(() => duplicated.CoreLibrary).Throws<InvalidOperationException>();
    }

    [Test]
    public async Task CoreLibrary_Does_Not_Enforce_Agreement_With_PackagingConfig_CoreLibrary()
    {
        // The computed property is intentionally permissive: it resolves solely via IsCoreLib=true.
        // Cross-field drift against PackagingConfig.CoreLibrary is the domain of PreFlight's G49
        // guardrail, which emits an operator-facing error rather than a startup crash.
        var baseline = ManifestFixture.CreateTestManifestConfig();
        var drifted = baseline with
        {
            PackagingConfig = baseline.PackagingConfig with { CoreLibrary = "sdl3" },
        };

        var core = drifted.CoreLibrary;

        await Assert.That(core.VcpkgName).IsEqualTo("sdl2");
        await Assert.That(drifted.PackagingConfig.CoreLibrary).IsEqualTo("sdl3");
    }
}
