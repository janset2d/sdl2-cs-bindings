using Build.Tests.Fixtures;
using Build.Validation.Models;
using Build.Validation.Vcpkg;
using Cake.Core.IO;

namespace Build.Tests.Unit.Validation.Vcpkg;

/// <summary>
/// G60 validator cross-checks every overlay port's vcpkg.json version + port-version
/// against the upstream port's vcpkg.json at the current vcpkg submodule pin. Filesystem
/// is seeded via <see cref="FakeCakeWorld"/>; overlay and upstream roots are arbitrary
/// directories the test plants JSON into.
/// </summary>
public sealed class OverlayPortVersionCoherenceValidatorTests
{
    private const string OverlayRootRelative = "vcpkg-overlay-ports";
    private const string UpstreamRootRelative = "external/vcpkg/ports";

    [Test]
    public async Task Validate_Should_Return_No_Checks_When_Overlay_Root_Missing()
    {
        var world = FakeCakeWorld.CreateWindows();
        var (validator, overlayRoot, upstreamRoot) = Arrange(world);

        var result = validator.Validate(overlayRoot, upstreamRoot);

        await Assert.That(result.HasErrors).IsFalse();
        await Assert.That(result.Checks).IsEmpty();
    }

    [Test]
    public async Task Validate_Should_Report_Match_When_Overlay_Version_Matches_Upstream()
    {
        var world = FakeCakeWorld.CreateWindows();
        SeedPortPair(world, "sdl2-mixer", overlayVersion: "2.8.1", overlayPortVersion: 2, upstreamVersion: "2.8.1", upstreamPortVersion: 2);
        var (validator, overlayRoot, upstreamRoot) = Arrange(world);

        var result = validator.Validate(overlayRoot, upstreamRoot);

        await Assert.That(result.HasErrors).IsFalse();
        var check = result.Checks.Single();
        await Assert.That(check.PortName).IsEqualTo("sdl2-mixer");
        await Assert.That(check.Status).IsEqualTo(OverlayPortVersionCheckStatus.Match);
    }

    [Test]
    public async Task Validate_Should_Report_VersionDrift_When_Overlay_Version_Differs()
    {
        var world = FakeCakeWorld.CreateWindows();
        SeedPortPair(world, "sdl2-mixer", overlayVersion: "2.8.0", overlayPortVersion: 2, upstreamVersion: "2.8.1", upstreamPortVersion: 2);
        var (validator, overlayRoot, upstreamRoot) = Arrange(world);

        var result = validator.Validate(overlayRoot, upstreamRoot);

        await Assert.That(result.HasErrors).IsTrue();
        var check = result.Checks.Single();
        await Assert.That(check.Status).IsEqualTo(OverlayPortVersionCheckStatus.VersionDrift);
        await Assert.That(check.OverlayVersion).IsEqualTo("2.8.0");
        await Assert.That(check.UpstreamVersion).IsEqualTo("2.8.1");
        await Assert.That(check.ErrorMessage).Contains("[G60]");
    }

    [Test]
    public async Task Validate_Should_Report_VersionDrift_When_PortVersion_Differs()
    {
        var world = FakeCakeWorld.CreateWindows();
        SeedPortPair(world, "sdl2-gfx", overlayVersion: "1.0.4", overlayPortVersion: 10, upstreamVersion: "1.0.4", upstreamPortVersion: 11);
        var (validator, overlayRoot, upstreamRoot) = Arrange(world);

        var result = validator.Validate(overlayRoot, upstreamRoot);

        await Assert.That(result.HasErrors).IsTrue();
        var check = result.Checks.Single();
        await Assert.That(check.Status).IsEqualTo(OverlayPortVersionCheckStatus.VersionDrift);
        await Assert.That(check.OverlayPortVersion).IsEqualTo(10);
        await Assert.That(check.UpstreamPortVersion).IsEqualTo(11);
    }

    [Test]
    public async Task Validate_Should_Treat_Missing_PortVersion_As_Zero_Both_Sides()
    {
        // Default port-version is implicit zero per vcpkg semantics. An overlay
        // that omits the field should still match an upstream that omits it.
        var world = FakeCakeWorld.CreateWindows();
        SeedPort(world, OverlayRootRelative, "mpg123", version: "1.33.4", portVersion: null);
        SeedPort(world, UpstreamRootRelative, "mpg123", version: "1.33.4", portVersion: null);
        var (validator, overlayRoot, upstreamRoot) = Arrange(world);

        var result = validator.Validate(overlayRoot, upstreamRoot);

        var check = result.Checks.Single();
        await Assert.That(check.Status).IsEqualTo(OverlayPortVersionCheckStatus.Match);
        await Assert.That(check.OverlayPortVersion).IsEqualTo(0);
        await Assert.That(check.UpstreamPortVersion).IsEqualTo(0);
    }

    [Test]
    public async Task Validate_Should_Report_OverlayManifestMissing_When_Overlay_Dir_Has_No_VcpkgJson()
    {
        var world = FakeCakeWorld.CreateWindows();
        // Create overlay dir but no vcpkg.json — also seed upstream so we'd otherwise reach compare.
        world.WithTextFile($"{OverlayRootRelative}/orphan/portfile.cmake", "# overlay portfile");
        SeedPort(world, UpstreamRootRelative, "orphan", version: "1.0.0", portVersion: 0);
        var (validator, overlayRoot, upstreamRoot) = Arrange(world);

        var result = validator.Validate(overlayRoot, upstreamRoot);

        await Assert.That(result.HasErrors).IsTrue();
        var check = result.Checks.Single();
        await Assert.That(check.Status).IsEqualTo(OverlayPortVersionCheckStatus.OverlayManifestMissing);
    }

    [Test]
    public async Task Validate_Should_Report_UpstreamPortMissing_When_Upstream_Counterpart_Absent()
    {
        var world = FakeCakeWorld.CreateWindows();
        SeedPort(world, OverlayRootRelative, "janset-only", version: "1.0.0", portVersion: 0);
        var (validator, overlayRoot, upstreamRoot) = Arrange(world);

        var result = validator.Validate(overlayRoot, upstreamRoot);

        await Assert.That(result.HasErrors).IsTrue();
        var check = result.Checks.Single();
        await Assert.That(check.Status).IsEqualTo(OverlayPortVersionCheckStatus.UpstreamPortMissing);
    }

    [Test]
    public async Task Validate_Should_Validate_Each_Overlay_Independently()
    {
        var world = FakeCakeWorld.CreateWindows();
        SeedPortPair(world, "sdl2-mixer", overlayVersion: "2.8.1", overlayPortVersion: 2, upstreamVersion: "2.8.1", upstreamPortVersion: 2);
        SeedPortPair(world, "sdl2-gfx", overlayVersion: "1.0.3", overlayPortVersion: 11, upstreamVersion: "1.0.4", upstreamPortVersion: 11);
        SeedPort(world, OverlayRootRelative, "mpg123", version: "1.33.4", portVersion: 0);
        SeedPort(world, UpstreamRootRelative, "mpg123", version: "1.33.4", portVersion: 0);
        var (validator, overlayRoot, upstreamRoot) = Arrange(world);

        var result = validator.Validate(overlayRoot, upstreamRoot);

        await Assert.That(result.Checks.Count).IsEqualTo(3);
        await Assert.That(result.Checks.Single(c => c.PortName == "sdl2-mixer").Status).IsEqualTo(OverlayPortVersionCheckStatus.Match);
        await Assert.That(result.Checks.Single(c => c.PortName == "sdl2-gfx").Status).IsEqualTo(OverlayPortVersionCheckStatus.VersionDrift);
        await Assert.That(result.Checks.Single(c => c.PortName == "mpg123").Status).IsEqualTo(OverlayPortVersionCheckStatus.Match);
        await Assert.That(result.HasErrors).IsTrue();
    }

    private static (OverlayPortVersionCoherenceValidator Validator, DirectoryPath OverlayRoot, DirectoryPath UpstreamRoot) Arrange(FakeCakeWorld world)
    {
        var manifestRepo = new Build.Data.Manifest.VcpkgManifestRepository(world.CakeContext);
        var validator = new OverlayPortVersionCoherenceValidator(world.CakeContext, manifestRepo);
        var overlayRoot = world.RepoRoot.Combine(OverlayRootRelative);
        var upstreamRoot = world.RepoRoot.Combine(UpstreamRootRelative);
        return (validator, overlayRoot, upstreamRoot);
    }

    private static void SeedPortPair(FakeCakeWorld world, string portName, string overlayVersion, int? overlayPortVersion, string upstreamVersion, int? upstreamPortVersion)
    {
        SeedPort(world, OverlayRootRelative, portName, overlayVersion, overlayPortVersion);
        SeedPort(world, UpstreamRootRelative, portName, upstreamVersion, upstreamPortVersion);
    }

    private static void SeedPort(FakeCakeWorld world, string rootRelative, string portName, string version, int? portVersion)
    {
        var path = $"{rootRelative}/{portName}/vcpkg.json";
        var json = portVersion is null
            ? $$"""
                {
                  "name": "{{portName}}",
                  "version": "{{version}}"
                }
                """
            : $$"""
                {
                  "name": "{{portName}}",
                  "version": "{{version}}",
                  "port-version": {{portVersion}}
                }
                """;
        world.WithTextFile(path, json);
    }
}
