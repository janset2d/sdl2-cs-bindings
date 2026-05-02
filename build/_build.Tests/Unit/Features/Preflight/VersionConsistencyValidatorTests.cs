using Build.Features.Preflight;
using Build.Shared.Manifest;
using Build.Tests.Fixtures;
using Cake.Core.IO;

namespace Build.Tests.Unit.Features.Preflight;

public sealed class VersionConsistencyValidatorTests
{
    [Test]
    public async Task Validate_Should_Return_Success_When_Manifest_And_Overrides_Are_Aligned()
    {
        var manifest = CreateManifestConfig("2.32.10", 0);
        var vcpkgManifest = CreateVcpkgManifest("2.32.10", 0);

        var result = VersionConsistencyValidator.Validate(
            manifest,
            vcpkgManifest,
            new FilePath("/repo/build/manifest.json"),
            new FilePath("/repo/vcpkg.json"));

        await Assert.That(result.IsSuccess()).IsTrue();
        await Assert.That(result.Validation.HasErrors).IsFalse();
    }

    [Test]
    public async Task Validate_Should_Return_Error_When_Override_Version_Does_Not_Match_Manifest()
    {
        var manifest = CreateManifestConfig("2.32.10", 0);
        var vcpkgManifest = CreateVcpkgManifest("2.31.0", 0);

        var result = VersionConsistencyValidator.Validate(
            manifest,
            vcpkgManifest,
            new FilePath("/repo/build/manifest.json"),
            new FilePath("/repo/vcpkg.json"));

        await Assert.That(result.IsError()).IsTrue();
        await Assert.That(result.VersionConsistencyError).IsTypeOf<VersionConsistencyError>();
        await Assert.That(result.Validation.HasErrors).IsTrue();
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
