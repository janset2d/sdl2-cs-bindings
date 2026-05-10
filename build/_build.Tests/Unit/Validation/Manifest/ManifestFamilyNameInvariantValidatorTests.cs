using System.Collections.Immutable;
using Build.Manifest;
using Build.Tests.Fixtures;
using Build.Validation.Manifest;

namespace Build.Tests.Unit.Validation.Manifest;

public sealed class ManifestFamilyNameInvariantValidatorTests
{
    [Test]
    public async Task Validate_Should_Return_Empty_Report_When_All_Names_Match_Canonical_Pattern()
    {
        var manifest = ManifestFixture.CreateTestManifestConfig();
        var validator = new ManifestFamilyNameInvariantValidator();

        var report = validator.Validate(manifest);

        await Assert.That(report.IsValid).IsTrue();
        await Assert.That(report.Errors.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Validate_Should_Return_Error_When_Name_Has_Uppercase()
    {
        var manifest = WithExtraFamily(ManifestFixture.CreateTestManifestConfig(), "SDL2-Test");
        var validator = new ManifestFamilyNameInvariantValidator();

        var report = validator.Validate(manifest);

        await Assert.That(report.IsValid).IsFalse();
        await Assert.That(report.Errors.Count).IsEqualTo(1);
        await Assert.That(report.Errors[0].Message).Contains("SDL2-Test");
        await Assert.That(report.Errors[0].Code).IsEqualTo("G59");
    }

    [Test]
    public async Task Validate_Should_Return_Error_When_Name_Is_Underscore_Delimited()
    {
        var manifest = WithExtraFamily(ManifestFixture.CreateTestManifestConfig(), "sdl2_core");
        var validator = new ManifestFamilyNameInvariantValidator();

        var report = validator.Validate(manifest);

        await Assert.That(report.IsValid).IsFalse();
        await Assert.That(report.Errors[0].Message).Contains("sdl2_core");
    }

    [Test]
    public async Task Validate_Should_Return_Error_When_Name_Is_Empty()
    {
        var manifest = WithExtraFamily(ManifestFixture.CreateTestManifestConfig(), "");
        var validator = new ManifestFamilyNameInvariantValidator();

        var report = validator.Validate(manifest);

        await Assert.That(report.IsValid).IsFalse();
        await Assert.That(report.Errors[0].Message).Contains("empty name");
    }

    [Test]
    public async Task Validate_Should_Return_Error_When_Major_Is_Not_Digits()
    {
        var manifest = WithExtraFamily(ManifestFixture.CreateTestManifestConfig(), "sdlx-core");
        var validator = new ManifestFamilyNameInvariantValidator();

        var report = validator.Validate(manifest);

        await Assert.That(report.IsValid).IsFalse();
        await Assert.That(report.Errors[0].Message).Contains("sdlx-core");
    }

    [Test]
    public async Task Validate_Should_Return_Multiple_Errors_For_Multiple_Violations()
    {
        var manifest = WithExtraFamily(
            WithExtraFamily(ManifestFixture.CreateTestManifestConfig(), "SDL2-Foo"),
            "sdl2_bar");
        var validator = new ManifestFamilyNameInvariantValidator();

        var report = validator.Validate(manifest);

        await Assert.That(report.Errors.Count).IsEqualTo(2);
    }

    private static ManifestConfig WithExtraFamily(ManifestConfig manifest, string name)
    {
        var families = manifest.PackageFamilies.Add(new PackageFamilyConfig
        {
            Name = name,
            TagPrefix = name,
            ManagedProject = null,
            NativeProject = null,
            LibraryRef = "sdl2",
            DependsOn = ImmutableList<string>.Empty,
            ChangePaths = ImmutableList<string>.Empty,
        });
        return manifest with { PackageFamilies = families };
    }
}
