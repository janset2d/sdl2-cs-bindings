using System.Collections.Immutable;
using Build.Features.Preflight;
using Build.Tests.Fixtures;

namespace Build.Tests.Unit.Features.Preflight;

public class CoreLibraryIdentityValidatorTests
{
    [Test]
    public async Task Validate_Returns_Match_When_Fields_Agree()
    {
        var manifest = ManifestFixture.CreateTestManifestConfig();
        var validator = new CoreLibraryIdentityValidator();

        var result = validator.Validate(manifest);

        await Assert.That(result.IsSuccess()).IsTrue();
        await Assert.That(result.Validation.Check.Status).IsEqualTo(CoreLibraryIdentityCheckStatus.Match);
        await Assert.That(result.Validation.Check.ManifestCoreVcpkgName).IsEqualTo("sdl2");
        await Assert.That(result.Validation.Check.PackagingConfigCoreLibrary).IsEqualTo("sdl2");
        await Assert.That(result.Validation.HasErrors).IsFalse();
    }

    [Test]
    public async Task Validate_Is_Case_Insensitive_For_Field_Agreement()
    {
        var manifest = ManifestFixture.CreateTestManifestConfig();
        var caseDrifted = manifest with
        {
            PackagingConfig = manifest.PackagingConfig with { CoreLibrary = "SDL2" },
        };
        var validator = new CoreLibraryIdentityValidator();

        var result = validator.Validate(caseDrifted);

        await Assert.That(result.IsSuccess()).IsTrue();
        await Assert.That(result.Validation.Check.Status).IsEqualTo(CoreLibraryIdentityCheckStatus.Match);
    }

    [Test]
    public async Task Validate_Returns_Mismatch_When_PackagingConfig_CoreLibrary_Diverges()
    {
        var manifest = ManifestFixture.CreateTestManifestConfig();
        var drifted = manifest with
        {
            PackagingConfig = manifest.PackagingConfig with { CoreLibrary = "sdl3" },
        };
        var validator = new CoreLibraryIdentityValidator();

        var result = validator.Validate(drifted);

        await Assert.That(result.IsError()).IsTrue();
        await Assert.That(result.Validation.Check.Status).IsEqualTo(CoreLibraryIdentityCheckStatus.PackagingConfigCoreLibraryMismatch);
        await Assert.That(result.Validation.Check.ManifestCoreVcpkgName).IsEqualTo("sdl2");
        await Assert.That(result.Validation.Check.PackagingConfigCoreLibrary).IsEqualTo("sdl3");
        await Assert.That(result.CoreLibraryIdentityError.Message).Contains("sdl2");
        await Assert.That(result.CoreLibraryIdentityError.Message).Contains("sdl3");
    }

    [Test]
    public async Task Validate_Returns_InvalidCount_When_No_Core_Library_Declared()
    {
        var manifest = ManifestFixture.CreateTestManifestConfig();
        var nonCoreOnly = manifest with
        {
            LibraryManifests = ImmutableList.Create(ManifestFixture.CreateTestSatelliteLibrary()),
        };
        var validator = new CoreLibraryIdentityValidator();

        var result = validator.Validate(nonCoreOnly);

        await Assert.That(result.IsError()).IsTrue();
        await Assert.That(result.Validation.Check.Status).IsEqualTo(CoreLibraryIdentityCheckStatus.InvalidCoreLibraryManifestCount);
        await Assert.That(result.Validation.Check.CoreLibraryManifestCount).IsEqualTo(0);
    }

    [Test]
    public async Task Validate_Returns_InvalidCount_When_Multiple_Core_Libraries_Declared()
    {
        var manifest = ManifestFixture.CreateTestManifestConfig();
        var duplicated = manifest with
        {
            LibraryManifests = ImmutableList.Create(
                ManifestFixture.CreateTestCoreLibrary(name: "SDL2", vcpkgName: "sdl2"),
                ManifestFixture.CreateTestCoreLibrary(name: "SDL3", vcpkgName: "sdl3")),
        };
        var validator = new CoreLibraryIdentityValidator();

        var result = validator.Validate(duplicated);

        await Assert.That(result.IsError()).IsTrue();
        await Assert.That(result.Validation.Check.Status).IsEqualTo(CoreLibraryIdentityCheckStatus.InvalidCoreLibraryManifestCount);
        await Assert.That(result.Validation.Check.CoreLibraryManifestCount).IsEqualTo(2);
        await Assert.That(result.CoreLibraryIdentityError.Message).Contains("sdl2");
        await Assert.That(result.CoreLibraryIdentityError.Message).Contains("sdl3");
    }
}
