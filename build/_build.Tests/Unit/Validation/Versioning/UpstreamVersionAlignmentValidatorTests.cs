using Build.Tests.Fixtures;
using Build.Validation.Models;
using Build.Validation.Versioning;
using Build.Versioning;
using NuGet.Versioning;

namespace Build.Tests.Unit.Validation.Versioning;

/// <summary>
/// G54 validator consumes a resolved <see cref="PackageFamilyVersionSet"/>. Every entry
/// is an explicit per-family assertion — strict-minor alignment applies unconditionally.
/// </summary>
public sealed class UpstreamVersionAlignmentValidatorTests
{
    [Test]
    public async Task Validate_Should_Return_No_Checks_When_Mapping_Is_Empty()
    {
        var manifest = ManifestFixture.CreateTestManifestConfig();
        var validator = new UpstreamVersionAlignmentValidator();

        var result = validator.Validate(manifest, PackageFamilyVersionSet.Empty);

        await Assert.That(result.HasErrors).IsFalse();
        await Assert.That(result.Checks).IsEmpty();
    }

    [Test]
    public async Task Validate_Should_Pass_When_Multi_Family_Aligns_With_Manifest()
    {
        var manifest = ManifestFixture.CreateTestManifestConfig();
        var versions = new PackageFamilyVersionSet(
        [
            new PackageFamilyVersion(new PackageFamilyId("sdl2-core"), NuGetVersion.Parse("2.32.0")),
            new PackageFamilyVersion(new PackageFamilyId("sdl2-image"), NuGetVersion.Parse("2.8.0")),
        ]);
        var validator = new UpstreamVersionAlignmentValidator();

        var result = validator.Validate(manifest, versions);

        await Assert.That(result.HasErrors).IsFalse();
    }

    [Test]
    public async Task Validate_Should_Return_Match_When_Single_Family_Major_And_Minor_Align()
    {
        var manifest = ManifestFixture.CreateTestManifestConfig();
        var validator = new UpstreamVersionAlignmentValidator();
        var versions = new PackageFamilyVersionSet(
        [
            new PackageFamilyVersion(new PackageFamilyId("sdl2-core"), NuGetVersion.Parse("2.32.99")),
        ]);

        var result = validator.Validate(manifest, versions);

        await Assert.That(result.HasErrors).IsFalse();
        await Assert.That(result.Checks).HasSingleItem();
        await Assert.That(result.Checks[0].Status).IsEqualTo(UpstreamVersionAlignmentCheckStatus.Match);
    }

    [Test]
    public async Task Validate_Should_Return_Error_When_Minor_Differs_For_Single_Family()
    {
        var manifest = ManifestFixture.CreateTestManifestConfig();
        var validator = new UpstreamVersionAlignmentValidator();
        var versions = new PackageFamilyVersionSet(
        [
            new PackageFamilyVersion(new PackageFamilyId("sdl2-core"), NuGetVersion.Parse("2.31.0")),
        ]);

        var result = validator.Validate(manifest, versions);

        await Assert.That(result.HasErrors).IsTrue();
        await Assert.That(result.Checks).HasSingleItem();
        await Assert.That(result.Checks[0].Status).IsEqualTo(UpstreamVersionAlignmentCheckStatus.VersionMismatch);
        await Assert.That(result.Checks[0].ErrorMessage).Contains("minor");
    }

    [Test]
    public async Task Validate_Should_Apply_Strict_Minor_Alignment_To_Every_Multi_Family_Entry()
    {
        // Mapping carries per-family versions — no shared scalar, so strict-minor-alignment
        // applies to every entry unconditionally. sdl2-core upstream minor is 32;
        // sdl2-image upstream minor is 8. Passing 2.30.0 for both breaks BOTH entries.
        var manifest = ManifestFixture.CreateTestManifestConfig();
        var validator = new UpstreamVersionAlignmentValidator();
        var versions = new PackageFamilyVersionSet(
        [
            new PackageFamilyVersion(new PackageFamilyId("sdl2-core"), NuGetVersion.Parse("2.30.0")),
            new PackageFamilyVersion(new PackageFamilyId("sdl2-image"), NuGetVersion.Parse("2.30.0")),
        ]);

        var result = validator.Validate(manifest, versions);

        await Assert.That(result.HasErrors).IsTrue();
        await Assert.That(result.Checks.Count).IsEqualTo(2);
        await Assert.That(result.Checks.All(check =>
            check.Status == UpstreamVersionAlignmentCheckStatus.VersionMismatch)).IsTrue();
    }

    [Test]
    public async Task Validate_Should_Return_Error_When_Major_Differs()
    {
        var manifest = ManifestFixture.CreateTestManifestConfig();
        var validator = new UpstreamVersionAlignmentValidator();
        var versions = new PackageFamilyVersionSet(
        [
            new PackageFamilyVersion(new PackageFamilyId("sdl2-core"), NuGetVersion.Parse("3.0.0")),
        ]);

        var result = validator.Validate(manifest, versions);

        await Assert.That(result.HasErrors).IsTrue();
        await Assert.That(result.Checks).HasSingleItem();
        await Assert.That(result.Checks[0].Status).IsEqualTo(UpstreamVersionAlignmentCheckStatus.VersionMismatch);
        await Assert.That(result.Checks[0].ErrorMessage).Contains("major");
    }

    [Test]
    public async Task Validate_Should_Return_Error_When_Family_Is_Not_Defined_In_Manifest()
    {
        var manifest = ManifestFixture.CreateTestManifestConfig();
        var validator = new UpstreamVersionAlignmentValidator();
        var versions = new PackageFamilyVersionSet(
        [
            new PackageFamilyVersion(new PackageFamilyId("missing-family"), NuGetVersion.Parse("2.32.0")),
        ]);

        var result = validator.Validate(manifest, versions);

        await Assert.That(result.HasErrors).IsTrue();
        await Assert.That(result.Checks).HasSingleItem();
        await Assert.That(result.Checks[0].Status).IsEqualTo(UpstreamVersionAlignmentCheckStatus.FamilyNotFound);
    }

    [Test]
    public async Task Validate_Should_Return_Typed_Error_When_Manifest_Has_Duplicate_Family_Names()
    {
        var baseManifest = ManifestFixture.CreateTestManifestConfig();
        var manifest = baseManifest with
        {
            PackageFamilies = baseManifest.PackageFamilies.Add(baseManifest.PackageFamilies[0] with
            {
                TagPrefix = "sdl2-core-duplicate",
            }),
        };

        var validator = new UpstreamVersionAlignmentValidator();
        var versions = new PackageFamilyVersionSet(
        [
            new PackageFamilyVersion(new PackageFamilyId("sdl2-core"), NuGetVersion.Parse("2.32.0")),
        ]);

        var result = validator.Validate(manifest, versions);

        await Assert.That(result.HasErrors).IsTrue();
        await Assert.That(result.Checks).HasSingleItem();
        await Assert.That(result.Checks[0].Status).IsEqualTo(UpstreamVersionAlignmentCheckStatus.DuplicateFamilyName);
        // Manifest-shape errors short-circuit per-family validation; CheckedFamilies must reflect that
        // no family was actually evaluated (else the reporter prints a misleading "evaluated" count).
        await Assert.That(result.CheckedFamilies).IsEqualTo(0);
    }

    [Test]
    public async Task Validate_Should_Return_Typed_Error_When_Manifest_Has_Duplicate_Library_Names()
    {
        var baseManifest = ManifestFixture.CreateTestManifestConfig();
        var manifest = baseManifest with
        {
            LibraryManifests = baseManifest.LibraryManifests.Add(baseManifest.LibraryManifests[0] with
            {
                VcpkgName = "sdl2-duplicate",
                IsCoreLib = false,
            }),
        };

        var validator = new UpstreamVersionAlignmentValidator();
        var versions = new PackageFamilyVersionSet(
        [
            new PackageFamilyVersion(new PackageFamilyId("sdl2-core"), NuGetVersion.Parse("2.32.0")),
        ]);

        var result = validator.Validate(manifest, versions);

        await Assert.That(result.HasErrors).IsTrue();
        await Assert.That(result.Checks).HasSingleItem();
        await Assert.That(result.Checks[0].Status).IsEqualTo(UpstreamVersionAlignmentCheckStatus.DuplicateLibraryName);
    }
}
