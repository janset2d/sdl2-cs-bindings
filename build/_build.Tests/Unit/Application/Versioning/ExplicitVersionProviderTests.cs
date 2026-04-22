using Build.Application.Versioning;
using Build.Domain.Preflight;
using Build.Tests.Fixtures;
using Cake.Core;
using NuGet.Versioning;

namespace Build.Tests.Unit.Application.Versioning;

/// <summary>
/// Slice A provider seam. ExplicitVersionProvider holds an operator-supplied
/// {family → version} mapping and exposes it through <see cref="IPackageVersionProvider"/>
/// after G54 upstream-alignment validation. Tests use the real
/// <see cref="UpstreamVersionAlignmentValidator"/> (pure-logic, stateless) rather than a
/// mock — we care about the integration path provider-to-G54, not the mock.
/// </summary>
public sealed class ExplicitVersionProviderTests
{
    [Test]
    public async Task ResolveAsync_Should_Return_Full_Mapping_When_RequestedScope_Is_Empty()
    {
        var manifest = ManifestFixture.CreateTestManifestConfig();
        var validator = new UpstreamVersionAlignmentValidator();
        var operatorMapping = new Dictionary<string, NuGetVersion>(StringComparer.OrdinalIgnoreCase)
        {
            ["sdl2-core"] = NuGetVersion.Parse("2.32.0-test.1"),
            ["sdl2-image"] = NuGetVersion.Parse("2.8.0-test.1"),
        };
        var provider = new ExplicitVersionProvider(manifest, validator, operatorMapping);

        var resolved = await provider.ResolveAsync(new HashSet<string>(StringComparer.OrdinalIgnoreCase), CancellationToken.None);

        await Assert.That(resolved.Count).IsEqualTo(2);
        await Assert.That(resolved["sdl2-core"].ToNormalizedString()).IsEqualTo("2.32.0-test.1");
        await Assert.That(resolved["sdl2-image"].ToNormalizedString()).IsEqualTo("2.8.0-test.1");
    }

    [Test]
    public async Task ResolveAsync_Should_Return_Filtered_Mapping_When_RequestedScope_Is_Subset()
    {
        var manifest = ManifestFixture.CreateTestManifestConfig();
        var validator = new UpstreamVersionAlignmentValidator();
        var operatorMapping = new Dictionary<string, NuGetVersion>(StringComparer.OrdinalIgnoreCase)
        {
            ["sdl2-core"] = NuGetVersion.Parse("2.32.0-test.1"),
            ["sdl2-image"] = NuGetVersion.Parse("2.8.0-test.1"),
        };
        var provider = new ExplicitVersionProvider(manifest, validator, operatorMapping);

        var scope = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "sdl2-core" };
        var resolved = await provider.ResolveAsync(scope, CancellationToken.None);

        await Assert.That(resolved.Count).IsEqualTo(1);
        await Assert.That(resolved.ContainsKey("sdl2-core")).IsTrue();
        await Assert.That(resolved.ContainsKey("sdl2-image")).IsFalse();
    }

    [Test]
    public async Task ResolveAsync_Should_Reject_Mapping_With_Invalid_Major_Version()
    {
        // sdl2-core upstream is 2.32.x in the fixture manifest. A "3.x" family-version fails G54
        // major alignment for every mapping entry (single or multi) and should surface as
        // CakeException with the validator's per-check error message folded in.
        var manifest = ManifestFixture.CreateTestManifestConfig();
        var validator = new UpstreamVersionAlignmentValidator();
        var operatorMapping = new Dictionary<string, NuGetVersion>(StringComparer.OrdinalIgnoreCase)
        {
            ["sdl2-core"] = NuGetVersion.Parse("3.0.0"),
        };
        var provider = new ExplicitVersionProvider(manifest, validator, operatorMapping);

        var exception = await Assert.ThrowsAsync<CakeException>(() =>
            provider.ResolveAsync(new HashSet<string>(StringComparer.OrdinalIgnoreCase), CancellationToken.None));

        await Assert.That(exception!.Message).Contains("G54");
        await Assert.That(exception!.Message).Contains("sdl2-core");
        await Assert.That(exception!.Message).Contains("major");
    }

    [Test]
    public async Task ResolveAsync_Should_Throw_When_Constructed_With_Empty_Mapping()
    {
        var manifest = ManifestFixture.CreateTestManifestConfig();
        var validator = new UpstreamVersionAlignmentValidator();
        var emptyMapping = new Dictionary<string, NuGetVersion>(StringComparer.OrdinalIgnoreCase);
        var provider = new ExplicitVersionProvider(manifest, validator, emptyMapping);

        var exception = await Assert.ThrowsAsync<CakeException>(() =>
            provider.ResolveAsync(new HashSet<string>(StringComparer.OrdinalIgnoreCase), CancellationToken.None));

        await Assert.That(exception!.Message).Contains("--explicit-version");
    }

    [Test]
    public async Task ResolveAsync_Should_Throw_When_RequestedScope_Asks_For_Missing_Family()
    {
        var manifest = ManifestFixture.CreateTestManifestConfig();
        var validator = new UpstreamVersionAlignmentValidator();
        var operatorMapping = new Dictionary<string, NuGetVersion>(StringComparer.OrdinalIgnoreCase)
        {
            ["sdl2-core"] = NuGetVersion.Parse("2.32.0-test.1"),
        };
        var provider = new ExplicitVersionProvider(manifest, validator, operatorMapping);

        var scope = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "sdl2-core", "sdl2-image" };

        var exception = await Assert.ThrowsAsync<CakeException>(() =>
            provider.ResolveAsync(scope, CancellationToken.None));

        await Assert.That(exception!.Message).Contains("sdl2-image");
    }
}
