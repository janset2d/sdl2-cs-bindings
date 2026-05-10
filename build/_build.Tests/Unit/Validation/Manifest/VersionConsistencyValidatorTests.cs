using Build.Data.Manifest;
using Build.Tests.Fixtures;
using Build.Validation.Manifest;

namespace Build.Tests.Unit.Validation.Manifest;

public sealed class VersionConsistencyValidatorTests
{
    [Test]
    public async Task Validate_Should_Return_Validation_With_No_Errors_When_Manifest_And_Overrides_Are_Aligned()
    {
        var manifest = CreateManifestConfig("2.32.10", 0);
        var vcpkgManifest = CreateVcpkgManifest("2.32.10", 0);

        var validation = new VersionConsistencyValidator().Validate(manifest, vcpkgManifest);

        await Assert.That(validation.HasErrors).IsFalse();
    }

    [Test]
    public async Task Validate_Should_Return_Validation_With_Errors_When_Override_Version_Does_Not_Match_Manifest()
    {
        var manifest = CreateManifestConfig("2.32.10", 0);
        var vcpkgManifest = CreateVcpkgManifest("2.31.0", 0);

        var validation = new VersionConsistencyValidator().Validate(manifest, vcpkgManifest);

        await Assert.That(validation.HasErrors).IsTrue();
    }

    private static ManifestConfig CreateManifestConfig(string version, int portVersion)
    {
        var manifest = ManifestFixture.CreateTestManifestConfig();

        return manifest with
        {
            PackageFamilies = [manifest.PackageFamilies.Single(family => string.Equals(family.Name, "sdl2-core", StringComparison.OrdinalIgnoreCase))],
            LibraryManifests =
            [
                ManifestFixture.CreateTestCoreLibrary() with
                {
                    VcpkgVersion = version,
                    VcpkgPortVersion = portVersion,
                },
            ],
        };
    }

    private static VcpkgManifest CreateVcpkgManifest(string version, int portVersion)
    {
        return new VcpkgManifest
        {
            Overrides =
            [
                new VcpkgOverride
                {
                    Name = "sdl2",
                    Version = version,
                    PortVersion = portVersion,
                },
            ],
        };
    }
}
