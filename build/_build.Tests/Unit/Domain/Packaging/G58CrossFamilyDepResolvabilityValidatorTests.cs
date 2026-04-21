using Build.Domain.Packaging;
using Build.Domain.Packaging.Models;
using Build.Tests.Fixtures;
using NuGet.Versioning;

namespace Build.Tests.Unit.Domain.Packaging;

/// <summary>
/// G58 scope-contains coverage. Feed-probe path is Phase 2b future work and not exercised
/// here (see <c>G58CrossFamilyCheckStatus.OnFeed</c> / <c>FeedProbeFailed</c> reserved states).
/// </summary>
public sealed class G58CrossFamilyDepResolvabilityValidatorTests
{
    [Test]
    public async Task Validate_Should_Return_InScope_When_Dependency_Is_In_Mapping()
    {
        // sdl2-image depends on sdl2-core per ManifestFixture default. When both are in scope,
        // G58 records an InScope check and reports no errors.
        var manifest = ManifestFixture.CreateTestManifestConfig();
        var mapping = new Dictionary<string, NuGetVersion>(StringComparer.OrdinalIgnoreCase)
        {
            ["sdl2-core"] = NuGetVersion.Parse("2.32.0-local.test"),
            ["sdl2-image"] = NuGetVersion.Parse("2.8.0-local.test"),
        };

        var validator = new G58CrossFamilyDepResolvabilityValidator();
        var validation = validator.Validate(mapping, manifest);

        await Assert.That(validation.HasErrors).IsFalse();
        await Assert.That(validation.Checks.Count).IsEqualTo(1);
        var check = validation.Checks[0];
        await Assert.That(check.Status).IsEqualTo(G58CrossFamilyCheckStatus.InScope);
        await Assert.That(check.DependentFamily).IsEqualTo("sdl2-image");
        await Assert.That(check.DependencyFamily).IsEqualTo("sdl2-core");
    }

    [Test]
    public async Task Validate_Should_Return_Missing_When_Dependency_Is_Not_In_Mapping()
    {
        // Satellite-only release: sdl2-image in scope, sdl2-core missing. G58 must flag it.
        var manifest = ManifestFixture.CreateTestManifestConfig();
        var mapping = new Dictionary<string, NuGetVersion>(StringComparer.OrdinalIgnoreCase)
        {
            ["sdl2-image"] = NuGetVersion.Parse("2.8.1-hotfix.1"),
        };

        var validator = new G58CrossFamilyDepResolvabilityValidator();
        var validation = validator.Validate(mapping, manifest);

        await Assert.That(validation.HasErrors).IsTrue();
        await Assert.That(validation.Checks.Count).IsEqualTo(1);
        var check = validation.Checks[0];
        await Assert.That(check.Status).IsEqualTo(G58CrossFamilyCheckStatus.Missing);
        await Assert.That(check.DependentFamily).IsEqualTo("sdl2-image");
        await Assert.That(check.DependencyFamily).IsEqualTo("sdl2-core");
        await Assert.That(check.ErrorMessage).IsNotNull();
        await Assert.That(check.ErrorMessage!).Contains("sdl2-core");
    }

    [Test]
    public async Task Validate_Should_Return_Empty_Checks_When_Family_Has_No_Dependencies()
    {
        // sdl2-core has empty depends_on. Core-only releases produce no G58 checks.
        var manifest = ManifestFixture.CreateTestManifestConfig();
        var mapping = new Dictionary<string, NuGetVersion>(StringComparer.OrdinalIgnoreCase)
        {
            ["sdl2-core"] = NuGetVersion.Parse("2.32.0-local.test"),
        };

        var validator = new G58CrossFamilyDepResolvabilityValidator();
        var validation = validator.Validate(mapping, manifest);

        await Assert.That(validation.HasErrors).IsFalse();
        await Assert.That(validation.Checks.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Validate_Should_Flag_Missing_When_Mapping_Contains_Family_Not_In_Manifest()
    {
        // A family id in the mapping that has no package_families[] entry is a manifest-coherence
        // failure that G58 surfaces via a Missing check keyed against the stray family itself,
        // not silently dropped.
        var manifest = ManifestFixture.CreateTestManifestConfig();
        var mapping = new Dictionary<string, NuGetVersion>(StringComparer.OrdinalIgnoreCase)
        {
            ["sdl-ghost"] = NuGetVersion.Parse("1.0.0"),
        };

        var validator = new G58CrossFamilyDepResolvabilityValidator();
        var validation = validator.Validate(mapping, manifest);

        await Assert.That(validation.HasErrors).IsTrue();
        await Assert.That(validation.Checks.Count).IsEqualTo(1);
        var check = validation.Checks[0];
        await Assert.That(check.Status).IsEqualTo(G58CrossFamilyCheckStatus.Missing);
        await Assert.That(check.DependentFamily).IsEqualTo("sdl-ghost");
    }
}
