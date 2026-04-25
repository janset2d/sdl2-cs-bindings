using System.IO;
using System.Xml.Linq;
using Build.Application.Packaging;
using Build.Context;
using Build.Context.Configs;
using Build.Context.Models;
using Build.Domain.Preflight;
using Build.Tests.Fixtures;
using Cake.Core;
using Cake.Core.IO;
using NuGet.Versioning;

namespace Build.Tests.Unit.Application.Packaging;

/// <summary>
/// Tests for <see cref="LocalArtifactSourceResolver"/>.
/// This resolver verifies the packaged feed for a supplied version mapping and stamps
/// <c>Janset.Local.props</c> for IDE direct-restore consumers. Pipeline composition stays
/// with <see cref="SetupLocalDevTaskRunner"/> and is covered separately.
/// </summary>
public sealed class LocalArtifactSourceResolverTests
{
    [Test]
    public async Task PrepareFeedAsync_Should_Throw_When_Versions_Mapping_Is_Empty()
    {
        var manifest = ManifestFixture.CreateTestManifestConfig();
        var repo = new FakeRepoBuilder(FakeRepoPlatform.Windows)
            .WithManifest(manifest)
            .BuildContextWithHandles();

        var resolver = new LocalArtifactSourceResolver(
            repo.CakeContext,
            repo.CakeContext.Log,
            repo.Paths,
            manifest);

        var thrown = await Assert.That(() =>
                resolver.PrepareFeedAsync(repo.BuildContext, new Dictionary<string, NuGetVersion>(StringComparer.OrdinalIgnoreCase)))
            .Throws<CakeException>();

        await Assert.That(thrown!.Message).Contains("empty version mapping");
    }

    [Test]
    public async Task PrepareFeedAsync_Should_Throw_When_Feed_Directory_Is_Missing()
    {
        var manifest = ManifestFixture.CreateTestManifestConfig();
        var repo = new FakeRepoBuilder(FakeRepoPlatform.Windows)
            .WithManifest(manifest)
            .BuildContextWithHandles();

        var resolver = new LocalArtifactSourceResolver(
            repo.CakeContext,
            repo.CakeContext.Log,
            repo.Paths,
            manifest);

        var versions = new Dictionary<string, NuGetVersion>(StringComparer.OrdinalIgnoreCase)
        {
            ["sdl2-core"] = NuGetVersion.Parse("2.32.0-local.20260421T000000"),
        };

        var thrown = await Assert.That(() => resolver.PrepareFeedAsync(repo.BuildContext, versions))
            .Throws<CakeException>();

        await Assert.That(thrown!.Message).Contains("does not exist");
        await Assert.That(thrown.Message).Contains("SetupLocalDev --source=local");
    }

    [Test]
    public async Task PrepareFeedAsync_Should_Throw_When_Family_Is_Unknown()
    {
        var manifest = ManifestFixture.CreateTestManifestConfig();
        var repo = new FakeRepoBuilder(FakeRepoPlatform.Windows)
            .WithManifest(manifest)
            .BuildContextWithHandles();

        // Seed the feed directory so the empty-feed precondition does not swallow the
        // unknown-family check.
        SeedEmptyFeed(repo);

        var resolver = new LocalArtifactSourceResolver(
            repo.CakeContext,
            repo.CakeContext.Log,
            repo.Paths,
            manifest);

        var versions = new Dictionary<string, NuGetVersion>(StringComparer.OrdinalIgnoreCase)
        {
            ["ghost-family"] = NuGetVersion.Parse("1.0.0"),
        };

        var thrown = await Assert.That(() => resolver.PrepareFeedAsync(repo.BuildContext, versions))
            .Throws<CakeException>();

        await Assert.That(thrown!.Message).Contains("unknown family 'ghost-family'");
    }

    [Test]
    public async Task PrepareFeedAsync_Should_Throw_When_Managed_Nupkg_Is_Missing()
    {
        var manifest = ManifestFixture.CreateTestManifestConfig();
        var repo = new FakeRepoBuilder(FakeRepoPlatform.Windows)
            .WithManifest(manifest)
            .BuildContextWithHandles();

        SeedEmptyFeed(repo);

        var resolver = new LocalArtifactSourceResolver(
            repo.CakeContext,
            repo.CakeContext.Log,
            repo.Paths,
            manifest);

        var versions = new Dictionary<string, NuGetVersion>(StringComparer.OrdinalIgnoreCase)
        {
            ["sdl2-core"] = NuGetVersion.Parse("2.32.0-local.20260421T000000"),
        };

        var thrown = await Assert.That(() => resolver.PrepareFeedAsync(repo.BuildContext, versions))
            .Throws<CakeException>();

        await Assert.That(thrown!.Message).Contains("expected package");
        await Assert.That(thrown.Message).Contains("was not found");
    }

    [Test]
    public async Task PrepareFeedAsync_And_WriteConsumerOverrideAsync_Should_Succeed_When_All_Packages_Exist()
    {
        var manifest = ManifestFixture.CreateTestManifestConfig();
        var concreteFamilies = manifest.PackageFamilies
            .Where(family => !string.IsNullOrWhiteSpace(family.ManagedProject) && !string.IsNullOrWhiteSpace(family.NativeProject))
            .OrderBy(family => family.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var repo = new FakeRepoBuilder(FakeRepoPlatform.Windows)
            .WithManifest(manifest)
            .BuildContextWithHandles();

        var versions = concreteFamilies.ToDictionary(
            family => family.Name,
            family => NuGetVersion.Parse($"{FakeUpstreamMajor(manifest, family.LibraryRef)}.{FakeUpstreamMinor(manifest, family.LibraryRef)}.0-local.20260421T000000"),
            StringComparer.OrdinalIgnoreCase);

        foreach (var family in concreteFamilies)
        {
            var version = versions[family.Name].ToNormalizedString();
            SeedPackage(repo, FamilyIdentifierConventions.ManagedPackageId(family.Name), version);
            SeedPackage(repo, FamilyIdentifierConventions.NativePackageId(family.Name), version);
        }

        var resolver = new LocalArtifactSourceResolver(
            repo.CakeContext,
            repo.CakeContext.Log,
            repo.Paths,
            manifest);

        await resolver.PrepareFeedAsync(repo.BuildContext, versions);
        await resolver.WriteConsumerOverrideAsync(repo.BuildContext, versions);

        var propsPath = repo.Paths.GetLocalPropsFile();
        var file = repo.FileSystem.GetFile(propsPath);
        await Assert.That(file.Exists).IsTrue();

        string xml;
        using (var stream = file.OpenRead())
        using (var reader = new StreamReader(stream))
        {
            xml = await reader.ReadToEndAsync();
        }

        var document = XDocument.Parse(xml);
        var propertyGroup = document.Root?.Element("PropertyGroup");
        await Assert.That(propertyGroup).IsNotNull();

        var localFeed = propertyGroup!.Element("LocalPackageFeed")?.Value;
        await Assert.That(localFeed).IsEqualTo(repo.Paths.PackagesOutput.FullPath);

        foreach (var family in concreteFamilies)
        {
            var propertyName = FamilyIdentifierConventions.VersionPropertyName(family.Name);
            var propertyValue = propertyGroup.Element(propertyName)?.Value;
            await Assert.That(propertyValue).IsEqualTo(versions[family.Name].ToNormalizedString());
        }
    }

    private static int FakeUpstreamMajor(ManifestConfig manifest, string libraryRef)
    {
        var library = manifest.LibraryManifests.Single(candidate =>
            string.Equals(candidate.Name, libraryRef, StringComparison.OrdinalIgnoreCase));
        return NuGetVersion.Parse(library.VcpkgVersion).Major;
    }

    private static int FakeUpstreamMinor(ManifestConfig manifest, string libraryRef)
    {
        var library = manifest.LibraryManifests.Single(candidate =>
            string.Equals(candidate.Name, libraryRef, StringComparison.OrdinalIgnoreCase));
        return NuGetVersion.Parse(library.VcpkgVersion).Minor;
    }

    private static void SeedEmptyFeed(FakeRepoHandles repo)
    {
        var feedDir = repo.FileSystem.GetDirectory(repo.Paths.PackagesOutput);
        if (!feedDir.Exists)
        {
            feedDir.Create();
        }
    }

    private static void SeedPackage(FakeRepoHandles repo, string packageId, string version)
    {
        var packagePath = repo.Paths.GetPackageOutputFile(packageId, version);
        var directory = repo.FileSystem.GetDirectory(packagePath.GetDirectory());
        if (!directory.Exists)
        {
            directory.Create();
        }

        var file = repo.FileSystem.GetFile(packagePath);
        using var stream = file.OpenWrite();
        using var writer = new StreamWriter(stream);
        writer.Write("nupkg-stub");
    }
}
