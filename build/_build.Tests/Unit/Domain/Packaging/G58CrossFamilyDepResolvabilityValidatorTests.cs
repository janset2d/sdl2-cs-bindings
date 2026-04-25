using Build.Context.Models;
using Build.Domain.Packaging;
using Build.Domain.Packaging.Models;
using Build.Tests.Fixtures;
using NuGet.Versioning;

namespace Build.Tests.Unit.Domain.Packaging;

/// <summary>
/// Coverage for the scope-contains path of G58.
/// These tests do not exercise the optional external-feed probe states.
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

    [Test]
    public async Task Validate_Should_Return_InScope_For_Chain_DependsOn_When_All_In_Mapping()
    {
        // Three-family chain: sdl2-ttf → sdl2-core (chain via DependsOn).
        // sdl2-image also → sdl2-core. All three in scope, no errors expected.
        var manifest = CreateThreeFamilyManifest();
        var mapping = new Dictionary<string, NuGetVersion>(StringComparer.OrdinalIgnoreCase)
        {
            ["sdl2-core"] = NuGetVersion.Parse("2.32.0-local.test"),
            ["sdl2-image"] = NuGetVersion.Parse("2.8.0-local.test"),
            ["sdl2-ttf"] = NuGetVersion.Parse("2.24.0-local.test"),
        };

        var validator = new G58CrossFamilyDepResolvabilityValidator();
        var validation = validator.Validate(mapping, manifest);

        await Assert.That(validation.HasErrors).IsFalse();
        // sdl2-image → sdl2-core + sdl2-ttf → sdl2-core = 2 checks
        await Assert.That(validation.Checks.Count).IsEqualTo(2);
        foreach (var check in validation.Checks)
        {
            await Assert.That(check.Status).IsEqualTo(G58CrossFamilyCheckStatus.InScope);
            await Assert.That(check.DependencyFamily).IsEqualTo("sdl2-core");
        }
    }

    [Test]
    public async Task Validate_Should_Flag_Multiple_Missing_When_Concurrent_Satellites_Lack_Core()
    {
        // Two satellites (sdl2-image + sdl2-ttf) both depend on sdl2-core, but core is
        // missing from the mapping. G58 must flag both as Missing — one check per satellite.
        var manifest = CreateThreeFamilyManifest();
        var mapping = new Dictionary<string, NuGetVersion>(StringComparer.OrdinalIgnoreCase)
        {
            ["sdl2-image"] = NuGetVersion.Parse("2.8.0-local.test"),
            ["sdl2-ttf"] = NuGetVersion.Parse("2.24.0-local.test"),
        };

        var validator = new G58CrossFamilyDepResolvabilityValidator();
        var validation = validator.Validate(mapping, manifest);

        await Assert.That(validation.HasErrors).IsTrue();
        await Assert.That(validation.Checks.Count).IsEqualTo(2);

        var missingChecks = validation.Checks.Where(c => c.Status == G58CrossFamilyCheckStatus.Missing).ToList();
        await Assert.That(missingChecks.Count).IsEqualTo(2);

        var dependentFamilies = missingChecks.Select(c => c.DependentFamily).OrderBy(f => f, StringComparer.OrdinalIgnoreCase).ToList();
        await Assert.That(dependentFamilies[0]).IsEqualTo("sdl2-image");
        await Assert.That(dependentFamilies[1]).IsEqualTo("sdl2-ttf");

        foreach (var check in missingChecks)
        {
            await Assert.That(check.DependencyFamily).IsEqualTo("sdl2-core");
            await Assert.That(check.ErrorMessage).IsNotNull();
        }
    }

    private static ManifestConfig CreateThreeFamilyManifest()
    {
        var baseManifest = ManifestFixture.CreateTestManifestConfig();
        return baseManifest with
        {
            PackageFamilies =
            [
                .. baseManifest.PackageFamilies,
                new PackageFamilyConfig
                {
                    Name = "sdl2-ttf",
                    TagPrefix = "sdl2-ttf",
                    ManagedProject = "src/SDL2.Ttf/SDL2.Ttf.csproj",
                    NativeProject = "src/native/SDL2.Ttf.Native/SDL2.Ttf.Native.csproj",
                    LibraryRef = "SDL2_ttf",
                    DependsOn = ["sdl2-core"],
                    ChangePaths = ["src/SDL2.Ttf/**"],
                },
            ],
            LibraryManifests =
            [
                .. baseManifest.LibraryManifests,
                new LibraryManifest
                {
                    Name = "SDL2_ttf",
                    VcpkgName = "sdl2-ttf",
                    VcpkgVersion = "2.24.0",
                    VcpkgPortVersion = 0,
                    NativeLibName = "SDL2.Ttf.Native",
                    IsCoreLib = false,
                    PrimaryBinaries =
                    [
                        new PrimaryBinary { Os = "Windows", Patterns = ["SDL2_ttf.dll"] },
                        new PrimaryBinary { Os = "Linux", Patterns = ["libSDL2_ttf*"] },
                        new PrimaryBinary { Os = "OSX", Patterns = ["libSDL2_ttf*.dylib"] },
                    ],
                },
            ],
        };
    }
}
