using System.Collections.Immutable;
using Build.Tests.Fixtures;
using Build.Validation.Manifest;
using Build.Validation.Models;

namespace Build.Tests.Unit.Validation.Manifest;

public sealed class CoreLibraryIdentityValidatorTests
{
    [Test]
    public async Task Validate_Should_Return_Match_When_Fields_Agree()
    {
        var manifest = ManifestFixture.CreateTestManifestConfig();

        var validation = new CoreLibraryIdentityValidator().Validate(manifest);

        await Assert.That(validation.HasErrors).IsFalse();
        await Assert.That(validation.Check.Status).IsEqualTo(CoreLibraryIdentityCheckStatus.Match);
        await Assert.That(validation.Check.ManifestCoreVcpkgName).IsEqualTo("sdl2");
        await Assert.That(validation.Check.PackagingConfigCoreLibrary).IsEqualTo("sdl2");
    }

    [Test]
    public async Task Validate_Should_Be_Case_Insensitive_For_Field_Agreement()
    {
        var manifest = ManifestFixture.CreateTestManifestConfig();
        var caseDrifted = manifest with
        {
            PackagingConfig = manifest.PackagingConfig with { CoreLibrary = "SDL2" },
        };

        var validation = new CoreLibraryIdentityValidator().Validate(caseDrifted);

        await Assert.That(validation.HasErrors).IsFalse();
        await Assert.That(validation.Check.Status).IsEqualTo(CoreLibraryIdentityCheckStatus.Match);
    }

    [Test]
    public async Task Validate_Should_Return_Mismatch_When_PackagingConfig_CoreLibrary_Diverges()
    {
        var manifest = ManifestFixture.CreateTestManifestConfig();
        var drifted = manifest with
        {
            PackagingConfig = manifest.PackagingConfig with { CoreLibrary = "sdl3" },
        };

        var validation = new CoreLibraryIdentityValidator().Validate(drifted);

        await Assert.That(validation.HasErrors).IsTrue();
        await Assert.That(validation.Check.Status).IsEqualTo(CoreLibraryIdentityCheckStatus.PackagingConfigCoreLibraryMismatch);
        await Assert.That(validation.Check.ManifestCoreVcpkgName).IsEqualTo("sdl2");
        await Assert.That(validation.Check.PackagingConfigCoreLibrary).IsEqualTo("sdl3");
        await Assert.That(validation.Check.ErrorMessage!).Contains("sdl2");
        await Assert.That(validation.Check.ErrorMessage!).Contains("sdl3");
    }

    [Test]
    public async Task Validate_Should_Return_InvalidCount_When_No_Core_Library_Declared()
    {
        var manifest = ManifestFixture.CreateTestManifestConfig();
        var nonCoreOnly = manifest with
        {
            LibraryManifests = ImmutableList.Create(ManifestFixture.CreateTestSatelliteLibrary()),
        };

        var validation = new CoreLibraryIdentityValidator().Validate(nonCoreOnly);

        await Assert.That(validation.HasErrors).IsTrue();
        await Assert.That(validation.Check.Status).IsEqualTo(CoreLibraryIdentityCheckStatus.InvalidCoreLibraryManifestCount);
        await Assert.That(validation.Check.CoreLibraryManifestCount).IsEqualTo(0);
    }

    [Test]
    public async Task Validate_Should_Return_InvalidCount_When_Multiple_Core_Libraries_Declared()
    {
        var manifest = ManifestFixture.CreateTestManifestConfig();
        var duplicated = manifest with
        {
            LibraryManifests = ImmutableList.Create(
                ManifestFixture.CreateTestCoreLibrary(name: "SDL2", vcpkgName: "sdl2"),
                ManifestFixture.CreateTestCoreLibrary(name: "SDL3", vcpkgName: "sdl3")),
        };

        var validation = new CoreLibraryIdentityValidator().Validate(duplicated);

        await Assert.That(validation.HasErrors).IsTrue();
        await Assert.That(validation.Check.Status).IsEqualTo(CoreLibraryIdentityCheckStatus.InvalidCoreLibraryManifestCount);
        await Assert.That(validation.Check.CoreLibraryManifestCount).IsEqualTo(2);
        await Assert.That(validation.Check.ErrorMessage!).Contains("sdl2");
        await Assert.That(validation.Check.ErrorMessage!).Contains("sdl3");
    }
}
