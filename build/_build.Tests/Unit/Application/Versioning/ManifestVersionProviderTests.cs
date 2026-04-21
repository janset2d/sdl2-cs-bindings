using Build.Application.Versioning;
using Build.Tests.Fixtures;
using Cake.Core;

namespace Build.Tests.Unit.Application.Versioning;

/// <summary>
/// ManifestVersionProvider derives per-family versions from manifest upstream major/minor +
/// caller-supplied suffix. Output shape is <c>&lt;Major&gt;.&lt;Minor&gt;.0-&lt;suffix&gt;</c>.
/// Tests use the real test manifest fixture (sdl2-core at SDL2 2.32.10, sdl2-image at
/// SDL2_image 2.8.8) so expected outputs track the fixture's upstream values.
/// </summary>
public sealed class ManifestVersionProviderTests
{
    [Test]
    public async Task ResolveAsync_Should_Compose_UpstreamMajorMinor_Zero_Suffix_Per_Family()
    {
        var manifest = ManifestFixture.CreateTestManifestConfig();
        var provider = new ManifestVersionProvider(manifest, "local.20260421T143022");

        var resolved = await provider.ResolveAsync(new HashSet<string>(StringComparer.OrdinalIgnoreCase), CancellationToken.None);

        await Assert.That(resolved.Count).IsEqualTo(2);
        await Assert.That(resolved["sdl2-core"].ToNormalizedString()).IsEqualTo("2.32.0-local.20260421T143022");
        await Assert.That(resolved["sdl2-image"].ToNormalizedString()).IsEqualTo("2.8.0-local.20260421T143022");
    }

    [Test]
    public async Task ResolveAsync_Should_Return_Only_Requested_Families_When_Scope_Is_Subset()
    {
        var manifest = ManifestFixture.CreateTestManifestConfig();
        var provider = new ManifestVersionProvider(manifest, "ci.12345");
        var scope = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "sdl2-core" };

        var resolved = await provider.ResolveAsync(scope, CancellationToken.None);

        await Assert.That(resolved.Count).IsEqualTo(1);
        await Assert.That(resolved["sdl2-core"].ToNormalizedString()).IsEqualTo("2.32.0-ci.12345");
        await Assert.That(resolved.ContainsKey("sdl2-image")).IsFalse();
    }

    [Test]
    public async Task ResolveAsync_Should_Throw_When_Requested_Family_Is_Missing_From_Manifest()
    {
        var manifest = ManifestFixture.CreateTestManifestConfig();
        var provider = new ManifestVersionProvider(manifest, "ci.12345");
        var scope = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "sdl2-core", "sdl2-made-up" };

        var exception = await Assert.ThrowsAsync<CakeException>(() =>
            provider.ResolveAsync(scope, CancellationToken.None));

        await Assert.That(exception!.Message).Contains("sdl2-made-up");
        await Assert.That(exception.Message).Contains("package_families");
    }

    [Test]
    public void Constructor_Should_Reject_Empty_Suffix()
    {
        var manifest = ManifestFixture.CreateTestManifestConfig();

        Assert.Throws<ArgumentException>(() => new ManifestVersionProvider(manifest, string.Empty));
        Assert.Throws<ArgumentException>(() => new ManifestVersionProvider(manifest, "   "));
    }

    [Test]
    public async Task ResolveAsync_Should_Throw_When_Suffix_Produces_Invalid_NuGet_SemVer()
    {
        // SemVer prerelease identifier allows ASCII alphanumerics, hyphens, and dot separators.
        // Whitespace-stripped but otherwise character-preserving; "bad_suffix" contains an
        // underscore which NuGet rejects in prerelease. Provider surfaces that deterministically.
        var manifest = ManifestFixture.CreateTestManifestConfig();
        var provider = new ManifestVersionProvider(manifest, "bad_suffix");

        var exception = await Assert.ThrowsAsync<CakeException>(() =>
            provider.ResolveAsync(new HashSet<string>(StringComparer.OrdinalIgnoreCase), CancellationToken.None));

        await Assert.That(exception!.Message).Contains("bad_suffix");
        await Assert.That(exception.Message).Contains("prerelease");
    }
}
