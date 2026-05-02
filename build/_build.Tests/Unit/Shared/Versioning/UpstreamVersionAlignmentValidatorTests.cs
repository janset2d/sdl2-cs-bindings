using Build.Shared.Versioning;
using Build.Tests.Fixtures;
using NuGet.Versioning;

namespace Build.Tests.Unit.Shared.Versioning;

/// <summary>
/// Post-B1 G54 validator consumes a resolved <see cref="IReadOnlyDictionary{TKey, TValue}"/>
/// mapping. Every entry is an explicit per-family assertion — strict-minor alignment applies
/// unconditionally. Pre-B1 skip cases (no family version, multi-family minor skip, invalid
/// family version literal) retire because the mapping shape rules them out by construction.
/// </summary>
public sealed class UpstreamVersionAlignmentValidatorTests
{
    [Test]
    public async Task Validate_Should_Return_No_Checks_When_Mapping_Is_Empty()
    {
        var manifest = ManifestFixture.CreateTestManifestConfig();
        var validator = new UpstreamVersionAlignmentValidator();

        var result = validator.Validate(manifest, new Dictionary<string, NuGetVersion>(StringComparer.OrdinalIgnoreCase));

        await Assert.That(result.IsSuccess()).IsTrue();
        await Assert.That(result.Validation.HasErrors).IsFalse();
        await Assert.That(result.Validation.Checks).IsEmpty();
    }

    [Test]
    public async Task Validate_Should_Return_Match_When_Single_Family_Major_And_Minor_Align()
    {
        var manifest = ManifestFixture.CreateTestManifestConfig();
        var validator = new UpstreamVersionAlignmentValidator();
        var mapping = new Dictionary<string, NuGetVersion>(StringComparer.OrdinalIgnoreCase)
        {
            ["sdl2-core"] = NuGetVersion.Parse("2.32.99"),
        };

        var result = validator.Validate(manifest, mapping);

        await Assert.That(result.IsSuccess()).IsTrue();
        await Assert.That(result.Validation.HasErrors).IsFalse();
        await Assert.That(result.Validation.Checks).HasSingleItem();
        await Assert.That(result.Validation.Checks[0].Status).IsEqualTo(UpstreamVersionAlignmentCheckStatus.Match);
    }

    [Test]
    public async Task Validate_Should_Return_Error_When_Minor_Differs_For_Single_Family()
    {
        var manifest = ManifestFixture.CreateTestManifestConfig();
        var validator = new UpstreamVersionAlignmentValidator();
        var mapping = new Dictionary<string, NuGetVersion>(StringComparer.OrdinalIgnoreCase)
        {
            ["sdl2-core"] = NuGetVersion.Parse("2.31.0"),
        };

        var result = validator.Validate(manifest, mapping);

        await Assert.That(result.IsError()).IsTrue();
        await Assert.That(result.Validation.HasErrors).IsTrue();
        await Assert.That(result.Validation.Checks).HasSingleItem();
        await Assert.That(result.Validation.Checks[0].Status).IsEqualTo(UpstreamVersionAlignmentCheckStatus.VersionMismatch);
        await Assert.That(result.Validation.Checks[0].ErrorMessage).Contains("minor");
    }

    [Test]
    public async Task Validate_Should_Apply_Strict_Minor_Alignment_To_Every_Multi_Family_Entry()
    {
        // Mapping carries per-family versions — no shared scalar, so strict-minor-alignment
        // applies to every entry unconditionally. sdl2-core upstream minor is 32;
        // sdl2-image upstream minor is 8. Passing 2.30.0 for both breaks BOTH entries.
        var manifest = ManifestFixture.CreateTestManifestConfig();
        var validator = new UpstreamVersionAlignmentValidator();
        var mapping = new Dictionary<string, NuGetVersion>(StringComparer.OrdinalIgnoreCase)
        {
            ["sdl2-core"] = NuGetVersion.Parse("2.30.0"),
            ["sdl2-image"] = NuGetVersion.Parse("2.30.0"),
        };

        var result = validator.Validate(manifest, mapping);

        await Assert.That(result.IsError()).IsTrue();
        await Assert.That(result.Validation.HasErrors).IsTrue();
        await Assert.That(result.Validation.Checks.Count).IsEqualTo(2);
        await Assert.That(result.Validation.Checks.All(check =>
            check.Status == UpstreamVersionAlignmentCheckStatus.VersionMismatch)).IsTrue();
    }

    [Test]
    public async Task Validate_Should_Return_Error_When_Major_Differs()
    {
        var manifest = ManifestFixture.CreateTestManifestConfig();
        var validator = new UpstreamVersionAlignmentValidator();
        var mapping = new Dictionary<string, NuGetVersion>(StringComparer.OrdinalIgnoreCase)
        {
            ["sdl2-core"] = NuGetVersion.Parse("3.0.0"),
        };

        var result = validator.Validate(manifest, mapping);

        await Assert.That(result.IsError()).IsTrue();
        await Assert.That(result.Validation.Checks).HasSingleItem();
        await Assert.That(result.Validation.Checks[0].Status).IsEqualTo(UpstreamVersionAlignmentCheckStatus.VersionMismatch);
        await Assert.That(result.Validation.Checks[0].ErrorMessage).Contains("major");
    }

    [Test]
    public async Task Validate_Should_Return_Error_When_Family_Is_Not_Defined_In_Manifest()
    {
        var manifest = ManifestFixture.CreateTestManifestConfig();
        var validator = new UpstreamVersionAlignmentValidator();
        var mapping = new Dictionary<string, NuGetVersion>(StringComparer.OrdinalIgnoreCase)
        {
            ["missing-family"] = NuGetVersion.Parse("2.32.0"),
        };

        var result = validator.Validate(manifest, mapping);

        await Assert.That(result.IsError()).IsTrue();
        await Assert.That(result.Validation.Checks).HasSingleItem();
        await Assert.That(result.Validation.Checks[0].Status).IsEqualTo(UpstreamVersionAlignmentCheckStatus.FamilyNotFound);
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
        var mapping = new Dictionary<string, NuGetVersion>(StringComparer.OrdinalIgnoreCase)
        {
            ["sdl2-core"] = NuGetVersion.Parse("2.32.0"),
        };

        var result = validator.Validate(manifest, mapping);

        await Assert.That(result.IsError()).IsTrue();
        await Assert.That(result.Validation.Checks).HasSingleItem();
        await Assert.That(result.Validation.Checks[0].Status).IsEqualTo(UpstreamVersionAlignmentCheckStatus.DuplicateFamilyName);
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
        var mapping = new Dictionary<string, NuGetVersion>(StringComparer.OrdinalIgnoreCase)
        {
            ["sdl2-core"] = NuGetVersion.Parse("2.32.0"),
        };

        var result = validator.Validate(manifest, mapping);

        await Assert.That(result.IsError()).IsTrue();
        await Assert.That(result.Validation.Checks).HasSingleItem();
        await Assert.That(result.Validation.Checks[0].Status).IsEqualTo(UpstreamVersionAlignmentCheckStatus.DuplicateLibraryName);
    }
}
