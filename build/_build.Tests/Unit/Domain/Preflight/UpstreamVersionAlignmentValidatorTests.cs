using Build.Context.Configs;
using Build.Domain.Preflight;
using Build.Domain.Preflight.Models;
using Build.Tests.Fixtures;

namespace Build.Tests.Unit.Domain.Preflight;

public sealed class UpstreamVersionAlignmentValidatorTests
{
    [Test]
    public async Task Validate_Should_Skip_When_FamilyVersion_Is_Not_Provided()
    {
        var manifest = ManifestFixture.CreateTestManifestConfig();
        var validator = new UpstreamVersionAlignmentValidator();
        var configuration = new PackageBuildConfiguration(["sdl2-core"], null);

        var result = validator.Validate(manifest, configuration);

        await Assert.That(result.IsSuccess()).IsTrue();
        await Assert.That(result.Validation.HasErrors).IsFalse();
        await Assert.That(result.Validation.Checks).HasSingleItem();
        await Assert.That(result.Validation.Checks[0].Status).IsEqualTo(UpstreamVersionAlignmentCheckStatus.SkippedNoFamilyVersion);
    }

    [Test]
    public async Task Validate_Should_Return_Match_For_Single_Family_When_Major_And_Minor_Align()
    {
        var manifest = ManifestFixture.CreateTestManifestConfig();
        var validator = new UpstreamVersionAlignmentValidator();
        var configuration = new PackageBuildConfiguration(["sdl2-core"], "2.32.99");

        var result = validator.Validate(manifest, configuration);

        await Assert.That(result.IsSuccess()).IsTrue();
        await Assert.That(result.Validation.HasErrors).IsFalse();
        await Assert.That(result.Validation.Checks).HasSingleItem();
        await Assert.That(result.Validation.Checks[0].Status).IsEqualTo(UpstreamVersionAlignmentCheckStatus.Match);
    }

    [Test]
    public async Task Validate_Should_Return_Error_For_Single_Family_When_Minors_Differ()
    {
        var manifest = ManifestFixture.CreateTestManifestConfig();
        var validator = new UpstreamVersionAlignmentValidator();
        var configuration = new PackageBuildConfiguration(["sdl2-core"], "2.31.0");

        var result = validator.Validate(manifest, configuration);

        await Assert.That(result.IsError()).IsTrue();
        await Assert.That(result.Validation.HasErrors).IsTrue();
        await Assert.That(result.Validation.Checks).HasSingleItem();
        await Assert.That(result.Validation.Checks[0].Status).IsEqualTo(UpstreamVersionAlignmentCheckStatus.VersionMismatch);
        await Assert.That(result.Validation.Checks[0].ErrorMessage).Contains("minor");
    }

    [Test]
    public async Task Validate_Should_Skip_Minor_Alignment_For_MultiFamily_Packs_When_Major_Aligns()
    {
        var manifest = ManifestFixture.CreateTestManifestConfig();
        var validator = new UpstreamVersionAlignmentValidator();
        var configuration = new PackageBuildConfiguration(["sdl2-core", "sdl2-image"], "2.30.0");

        var result = validator.Validate(manifest, configuration);

        await Assert.That(result.IsSuccess()).IsTrue();
        await Assert.That(result.Validation.HasErrors).IsFalse();
        await Assert.That(result.Validation.Checks.Count).IsEqualTo(2);
        await Assert.That(result.Validation.Checks.All(check =>
            check.Status == UpstreamVersionAlignmentCheckStatus.SkippedMinorAlignmentForMultiFamilyPack)).IsTrue();
    }

    [Test]
    public async Task Validate_Should_Return_Error_When_Major_Differs_Even_For_MultiFamily_Packs()
    {
        var manifest = ManifestFixture.CreateTestManifestConfig();
        var validator = new UpstreamVersionAlignmentValidator();
        var configuration = new PackageBuildConfiguration(["sdl2-core", "sdl2-image"], "3.0.0");

        var result = validator.Validate(manifest, configuration);

        await Assert.That(result.IsError()).IsTrue();
        await Assert.That(result.Validation.HasErrors).IsTrue();
        await Assert.That(result.Validation.Checks.Count).IsEqualTo(2);
        await Assert.That(result.Validation.Checks.All(check =>
            check.Status == UpstreamVersionAlignmentCheckStatus.VersionMismatch)).IsTrue();
    }

    [Test]
    public async Task Validate_Should_Return_Error_When_Family_Is_Not_Defined_In_Manifest()
    {
        var manifest = ManifestFixture.CreateTestManifestConfig();
        var validator = new UpstreamVersionAlignmentValidator();
        var configuration = new PackageBuildConfiguration(["missing-family"], "2.32.0");

        var result = validator.Validate(manifest, configuration);

        await Assert.That(result.IsError()).IsTrue();
        await Assert.That(result.Validation.HasErrors).IsTrue();
        await Assert.That(result.Validation.Checks).HasSingleItem();
        await Assert.That(result.Validation.Checks[0].Status).IsEqualTo(UpstreamVersionAlignmentCheckStatus.FamilyNotFound);
    }

    [Test]
    public async Task Validate_Should_Return_Error_When_FamilyVersion_Is_Invalid()
    {
        var manifest = ManifestFixture.CreateTestManifestConfig();
        var validator = new UpstreamVersionAlignmentValidator();
        var configuration = new PackageBuildConfiguration(["sdl2-core"], "not-semver");

        var result = validator.Validate(manifest, configuration);

        await Assert.That(result.IsError()).IsTrue();
        await Assert.That(result.Validation.HasErrors).IsTrue();
        await Assert.That(result.Validation.Checks).HasSingleItem();
        await Assert.That(result.Validation.Checks[0].Status).IsEqualTo(UpstreamVersionAlignmentCheckStatus.InvalidFamilyVersion);
    }
}
