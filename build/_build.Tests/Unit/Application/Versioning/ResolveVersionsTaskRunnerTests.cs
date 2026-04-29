using System.Text.Json;
using Build.Application.Versioning;
using Build.Context.Configs;
using Build.Context.Models;
using Build.Domain.Preflight;
using Build.Tests.Fixtures;
using Cake.Core;
using NuGet.Versioning;

namespace Build.Tests.Unit.Application.Versioning;

/// <summary>
/// Tests for <see cref="ResolveVersionsTaskRunner"/>.
/// These cases exercise manifest-based resolution and verify that the runner writes the
/// expected flat JSON mapping through the FakeRepoBuilder-backed filesystem.
/// </summary>
public sealed class ResolveVersionsTaskRunnerTests
{
    [Test]
    public async Task RunAsync_Should_Write_Canonical_Json_For_Manifest_Source_Happy_Path()
    {
        var manifest = ManifestFixture.CreateTestManifestConfig();
        var repo = new FakeRepoBuilder(FakeRepoPlatform.Windows).WithManifest(manifest).BuildContextWithHandles();

        var versioning = new VersioningConfiguration(
            versionSource: "manifest",
            suffix: "test.smoke",
            scope: []);

        var runner = CreateRunner(repo, manifest, versioning);

        await runner.RunAsync();

        var outputFile = repo.Paths.GetResolveVersionsOutputFile();
        var writtenFile = repo.FileSystem.GetFile(outputFile);
        await Assert.That(writtenFile.Exists).IsTrue();

        string content;
        using (var stream = writtenFile.Open(FileMode.Open, FileAccess.Read, FileShare.Read))
        using (var reader = new StreamReader(stream))
        {
            content = await reader.ReadToEndAsync();
        }

        var deserialized = JsonSerializer.Deserialize<SortedDictionary<string, string>>(content);
        await Assert.That(deserialized).IsNotNull();
        await Assert.That(deserialized!.ContainsKey("sdl2-core")).IsTrue();
        await Assert.That(deserialized["sdl2-core"]).IsEqualTo("2.32.0-test.smoke");
        await Assert.That(deserialized["sdl2-image"]).IsEqualTo("2.8.0-test.smoke");
        await Assert.That(deserialized.Count).IsEqualTo(2);
    }

    [Test]
    public async Task RunAsync_Should_Honor_Scope_Filter_When_Supplied()
    {
        var manifest = ManifestFixture.CreateTestManifestConfig();
        var repo = new FakeRepoBuilder(FakeRepoPlatform.Windows).WithManifest(manifest).BuildContextWithHandles();

        var versioning = new VersioningConfiguration(
            versionSource: "manifest",
            suffix: "ci.12345",
            scope: ["sdl2-core"]);

        var runner = CreateRunner(repo, manifest, versioning);

        await runner.RunAsync();

        var outputFile = repo.Paths.GetResolveVersionsOutputFile();
        var writtenFile = repo.FileSystem.GetFile(outputFile);

        string content;
        using (var stream = writtenFile.Open(FileMode.Open, FileAccess.Read, FileShare.Read))
        using (var reader = new StreamReader(stream))
        {
            content = await reader.ReadToEndAsync();
        }

        var deserialized = JsonSerializer.Deserialize<SortedDictionary<string, string>>(content);
        await Assert.That(deserialized!.Count).IsEqualTo(1);
        await Assert.That(deserialized.ContainsKey("sdl2-core")).IsTrue();
        await Assert.That(deserialized.ContainsKey("sdl2-image")).IsFalse();
    }

    [Test]
    public async Task RunAsync_Should_Write_Canonical_Json_For_Explicit_Source_Happy_Path()
    {
        var manifest = ManifestFixture.CreateTestManifestConfig();
        var repo = new FakeRepoBuilder(FakeRepoPlatform.Windows).WithManifest(manifest).BuildContextWithHandles();

        var explicitVersions = new Dictionary<string, NuGetVersion>(StringComparer.OrdinalIgnoreCase)
        {
            ["sdl2-image"] = NuGetVersion.Parse("2.8.0-rc.2"),
            ["sdl2-core"] = NuGetVersion.Parse("2.32.0-rc.1"),
        };

        var versioning = new VersioningConfiguration(
            versionSource: "explicit",
            suffix: null,
            scope: []);

        var runner = CreateRunner(repo, manifest, versioning, explicitVersions);

        await runner.RunAsync();

        var outputFile = repo.Paths.GetResolveVersionsOutputFile();
        var writtenFile = repo.FileSystem.GetFile(outputFile);

        string content;
        using (var stream = writtenFile.Open(FileMode.Open, FileAccess.Read, FileShare.Read))
        using (var reader = new StreamReader(stream))
        {
            content = await reader.ReadToEndAsync();
        }

        var deserialized = JsonSerializer.Deserialize<SortedDictionary<string, string>>(content);
        await Assert.That(deserialized).IsNotNull();
        await Assert.That(deserialized!.Count).IsEqualTo(2);
        await Assert.That(deserialized.Keys.ToArray()).IsEquivalentTo(["sdl2-core", "sdl2-image"]);
        await Assert.That(deserialized["sdl2-core"]).IsEqualTo("2.32.0-rc.1");
        await Assert.That(deserialized["sdl2-image"]).IsEqualTo("2.8.0-rc.2");
    }

    [Test]
    public async Task RunAsync_Should_Honor_Scope_Filter_For_Explicit_Source_When_Supplied()
    {
        var manifest = ManifestFixture.CreateTestManifestConfig();
        var repo = new FakeRepoBuilder(FakeRepoPlatform.Windows).WithManifest(manifest).BuildContextWithHandles();

        var explicitVersions = new Dictionary<string, NuGetVersion>(StringComparer.OrdinalIgnoreCase)
        {
            ["sdl2-core"] = NuGetVersion.Parse("2.32.0-rc.1"),
            ["sdl2-image"] = NuGetVersion.Parse("2.8.0-rc.2"),
        };

        var versioning = new VersioningConfiguration(
            versionSource: "explicit",
            suffix: null,
            scope: ["sdl2-core"]);

        var runner = CreateRunner(repo, manifest, versioning, explicitVersions);

        await runner.RunAsync();

        var outputFile = repo.Paths.GetResolveVersionsOutputFile();
        var writtenFile = repo.FileSystem.GetFile(outputFile);

        string content;
        using (var stream = writtenFile.Open(FileMode.Open, FileAccess.Read, FileShare.Read))
        using (var reader = new StreamReader(stream))
        {
            content = await reader.ReadToEndAsync();
        }

        var deserialized = JsonSerializer.Deserialize<SortedDictionary<string, string>>(content);
        await Assert.That(deserialized!.Count).IsEqualTo(1);
        await Assert.That(deserialized.ContainsKey("sdl2-core")).IsTrue();
        await Assert.That(deserialized.ContainsKey("sdl2-image")).IsFalse();
    }

    [Test]
    public async Task RunAsync_Should_Throw_When_Explicit_Source_Has_No_ExplicitVersions()
    {
        var manifest = ManifestFixture.CreateTestManifestConfig();
        var repo = new FakeRepoBuilder(FakeRepoPlatform.Windows).WithManifest(manifest).BuildContextWithHandles();

        var versioning = new VersioningConfiguration(
            versionSource: "explicit",
            suffix: null,
            scope: []);

        var runner = CreateRunner(repo, manifest, versioning);

        var exception = await Assert.ThrowsAsync<CakeException>(() => runner.RunAsync());
        await Assert.That(exception!.Message).Contains("--explicit-version");
    }

    [Test]
    public async Task RunAsync_Should_Throw_When_VersionSource_Is_Missing()
    {
        var manifest = ManifestFixture.CreateTestManifestConfig();
        var repo = new FakeRepoBuilder(FakeRepoPlatform.Windows).WithManifest(manifest).BuildContextWithHandles();

        var versioning = new VersioningConfiguration(
            versionSource: null,
            suffix: "test",
            scope: []);

        var runner = CreateRunner(repo, manifest, versioning);

        var exception = await Assert.ThrowsAsync<CakeException>(() => runner.RunAsync());
        await Assert.That(exception!.Message).Contains("--version-source");
    }

    [Test]
    public async Task RunAsync_Should_Throw_When_Manifest_Source_Has_No_Suffix()
    {
        var manifest = ManifestFixture.CreateTestManifestConfig();
        var repo = new FakeRepoBuilder(FakeRepoPlatform.Windows).WithManifest(manifest).BuildContextWithHandles();

        var versioning = new VersioningConfiguration(
            versionSource: "manifest",
            suffix: null,
            scope: []);

        var runner = CreateRunner(repo, manifest, versioning);

        var exception = await Assert.ThrowsAsync<CakeException>(() => runner.RunAsync());
        await Assert.That(exception!.Message).Contains("--suffix");
    }

    [Test]
    public async Task RunAsync_Should_Throw_When_GitTag_Source_Has_No_Scope()
    {
        // Post-C.5: git-tag source requires exactly one --scope <family> entry. Empty scope
        // is the classic "forgot the family argument" misuse; it must fail loud before we
        // reach the GitTagVersionProvider construction.
        var manifest = ManifestFixture.CreateTestManifestConfig();
        var repo = new FakeRepoBuilder(FakeRepoPlatform.Windows).WithManifest(manifest).BuildContextWithHandles();

        var versioning = new VersioningConfiguration(
            versionSource: "git-tag",
            suffix: null,
            scope: []);

        var runner = CreateRunner(repo, manifest, versioning);

        var exception = await Assert.ThrowsAsync<CakeException>(() => runner.RunAsync());
        await Assert.That(exception!.Message).Contains("exactly one --scope");
    }

    [Test]
    public async Task RunAsync_Should_Throw_When_GitTag_Source_Has_Multiple_Scope_Entries()
    {
        // Targeted release is single-family by construction. Multi-family requests must
        // go through --version-source=meta-tag (GitTagScope.Train), not git-tag.
        var manifest = ManifestFixture.CreateTestManifestConfig();
        var repo = new FakeRepoBuilder(FakeRepoPlatform.Windows).WithManifest(manifest).BuildContextWithHandles();

        var versioning = new VersioningConfiguration(
            versionSource: "git-tag",
            suffix: null,
            scope: ["sdl2-core", "sdl2-image"]);

        var runner = CreateRunner(repo, manifest, versioning);

        var exception = await Assert.ThrowsAsync<CakeException>(() => runner.RunAsync());
        await Assert.That(exception!.Message).Contains("meta-tag");
    }

    [Test]
    public async Task RunAsync_Should_Throw_For_Unknown_Source()
    {
        var manifest = ManifestFixture.CreateTestManifestConfig();
        var repo = new FakeRepoBuilder(FakeRepoPlatform.Windows).WithManifest(manifest).BuildContextWithHandles();

        var versioning = new VersioningConfiguration(
            versionSource: "nonsense",
            suffix: null,
            scope: []);

        var runner = CreateRunner(repo, manifest, versioning);

        var exception = await Assert.ThrowsAsync<CakeException>(() => runner.RunAsync());
        await Assert.That(exception!.Message).Contains("nonsense");
    }

    private static ResolveVersionsTaskRunner CreateRunner(
        FakeRepoHandles repo,
        ManifestConfig manifest,
        VersioningConfiguration versioning,
        IReadOnlyDictionary<string, NuGetVersion>? explicitVersions = null) =>
        new(
            repo.CakeContext,
            repo.CakeContext.Log,
            repo.Paths,
            manifest,
            new PackageBuildConfiguration(explicitVersions ?? new Dictionary<string, NuGetVersion>(StringComparer.OrdinalIgnoreCase)),
            versioning,
            new UpstreamVersionAlignmentValidator());
}
