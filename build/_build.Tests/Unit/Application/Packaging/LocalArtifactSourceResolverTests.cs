using System.Xml.Linq;
using Build.Application.Packaging;
using Build.Context.Configs;
using Build.Context.Models;
using Build.Domain.Preflight;
using Build.Tests.Fixtures;
using Cake.Core;
using Cake.Core.IO;
using NSubstitute;
using NuGet.Versioning;

namespace Build.Tests.Unit.Application.Packaging;

public sealed class LocalArtifactSourceResolverTests
{
    [Test]
    public async Task WriteConsumerOverrideAsync_Should_Throw_When_PrepareFeed_Not_Run()
    {
        var manifest = ManifestFixture.CreateTestManifestConfig();
        var repo = new FakeRepoBuilder(FakeRepoPlatform.Windows)
            .WithManifest(manifest)
            .BuildContextWithHandles();

        var packageTaskRunner = Substitute.For<IPackageTaskRunner>();
        var resolver = CreateResolver(repo, manifest, packageTaskRunner);

        var exception = await Assert.That(() => resolver.WriteConsumerOverrideAsync(repo.BuildContext)).Throws<CakeException>();
        await Assert.That(exception!.Message).Contains("Run PrepareFeedAsync first");
    }

    [Test]
    public async Task PrepareFeedAsync_Should_Pack_Concrete_Families_And_Write_Local_Props()
    {
        var manifest = ManifestFixture.CreateTestManifestConfig();
        var concreteFamilies = manifest.PackageFamilies
            .Where(family => !string.IsNullOrWhiteSpace(family.ManagedProject) && !string.IsNullOrWhiteSpace(family.NativeProject))
            .OrderBy(family => family.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var repo = new FakeRepoBuilder(FakeRepoPlatform.Windows)
            .WithManifest(manifest)
            .BuildContextWithHandles();

        var packageTaskRunner = Substitute.For<IPackageTaskRunner>();
        packageTaskRunner
            .RunAsync(Arg.Any<PackageBuildConfiguration>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                // Post-B1.7 resolver: single multi-family pack call with the full ManifestVersionProvider
                // mapping. Mock seeds a nupkg per mapping entry so the resolver's EnsurePackageExists
                // post-assertion is satisfied.
                var packageConfiguration = callInfo.ArgAt<PackageBuildConfiguration>(0);
                foreach (var (familyName, familyVersion) in packageConfiguration.ExplicitVersions)
                {
                    var version = familyVersion.ToNormalizedString();
                    var family = concreteFamilies.Single(candidate => string.Equals(candidate.Name, familyName, StringComparison.OrdinalIgnoreCase));
                    SeedPackage(repo, FamilyIdentifierConventions.ManagedPackageId(family.Name), version);
                    SeedPackage(repo, FamilyIdentifierConventions.NativePackageId(family.Name), version);
                }

                return Task.CompletedTask;
            });

        var resolver = CreateResolver(repo, manifest, packageTaskRunner);

        await resolver.PrepareFeedAsync(repo.BuildContext);
        await resolver.WriteConsumerOverrideAsync(repo.BuildContext);

        // Resolver now packs all families in one invocation (ADR-003 §2.4 resolve-once + single
        // Pack call). Pre-B1.7 the test asserted `.Received(concreteFamilies.Count)`; post-B1.7
        // it's a single call carrying the full mapping.
        await packageTaskRunner.Received(1)
            .RunAsync(
                Arg.Is<PackageBuildConfiguration>(config => config.ExplicitVersions.Count == concreteFamilies.Count),
                Arg.Any<CancellationToken>());

        var propsPath = repo.Paths.GetSmokeLocalPropsFile();
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

        var observedTokens = new HashSet<string>(StringComparer.Ordinal);

        foreach (var family in concreteFamilies)
        {
            var propertyName = FamilyIdentifierConventions.VersionPropertyName(family.Name);
            var propertyValue = propertyGroup.Element(propertyName)?.Value;

            await Assert.That(propertyValue).IsNotNull();
            await Assert.That(propertyValue!).Contains("-local.");

            var library = manifest.LibraryManifests.Single(candidate => string.Equals(candidate.Name, family.LibraryRef, StringComparison.OrdinalIgnoreCase));
            var upstreamParsed = NuGetVersion.TryParse(library.VcpkgVersion, out var upstreamVersion);
            await Assert.That(upstreamParsed).IsTrue();

            var expectedPrefix = $"{upstreamVersion!.Major}.{upstreamVersion.Minor}.0-local.";
            await Assert.That(propertyValue).StartsWith(expectedPrefix);

            var token = propertyValue[expectedPrefix.Length..];
            observedTokens.Add(token);
        }

        await Assert.That(observedTokens.Count).IsEqualTo(1);
    }

    private static LocalArtifactSourceResolver CreateResolver(
        FakeRepoHandles repo,
        ManifestConfig manifest,
        IPackageTaskRunner packageTaskRunner)
    {
        return new LocalArtifactSourceResolver(
            repo.CakeContext,
            repo.CakeContext.Log,
            repo.Paths,
            manifest,
            packageTaskRunner);
    }

    private static void SeedPackage(FakeRepoHandles repo, string packageId, string version)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packageId);
        ArgumentException.ThrowIfNullOrWhiteSpace(version);

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
