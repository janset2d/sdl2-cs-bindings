using System.Text.Json;
using Build.Application.Versioning;
using Build.Context.Configs;
using Build.Domain.Preflight;
using Build.Tests.Fixtures;
using Cake.Core;

namespace Build.Tests.Unit.Application.Versioning;

/// <summary>
/// <see cref="ResolveVersionsTaskRunner"/> is the build-host CI entrypoint for ADR-003
/// version resolution. Slice B1 implements manifest source only; other sources error with
/// clear "lands in later slice" messages. Tests exercise the dispatch surface + the manifest
/// happy path; file-write assertion uses the FakeRepoBuilder in-memory filesystem.
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

        var runner = new ResolveVersionsTaskRunner(repo.CakeContext, repo.CakeContext.Log, repo.Paths, manifest, versioning, new UpstreamVersionAlignmentValidator());

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

        var runner = new ResolveVersionsTaskRunner(repo.CakeContext, repo.CakeContext.Log, repo.Paths, manifest, versioning, new UpstreamVersionAlignmentValidator());

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
    public async Task RunAsync_Should_Throw_When_VersionSource_Is_Missing()
    {
        var manifest = ManifestFixture.CreateTestManifestConfig();
        var repo = new FakeRepoBuilder(FakeRepoPlatform.Windows).WithManifest(manifest).BuildContextWithHandles();

        var versioning = new VersioningConfiguration(
            versionSource: null,
            suffix: "test",
            scope: []);

        var runner = new ResolveVersionsTaskRunner(repo.CakeContext, repo.CakeContext.Log, repo.Paths, manifest, versioning, new UpstreamVersionAlignmentValidator());

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

        var runner = new ResolveVersionsTaskRunner(repo.CakeContext, repo.CakeContext.Log, repo.Paths, manifest, versioning, new UpstreamVersionAlignmentValidator());

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

        var runner = new ResolveVersionsTaskRunner(repo.CakeContext, repo.CakeContext.Log, repo.Paths, manifest, versioning, new UpstreamVersionAlignmentValidator());

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

        var runner = new ResolveVersionsTaskRunner(repo.CakeContext, repo.CakeContext.Log, repo.Paths, manifest, versioning, new UpstreamVersionAlignmentValidator());

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

        var runner = new ResolveVersionsTaskRunner(repo.CakeContext, repo.CakeContext.Log, repo.Paths, manifest, versioning, new UpstreamVersionAlignmentValidator());

        var exception = await Assert.ThrowsAsync<CakeException>(() => runner.RunAsync());
        await Assert.That(exception!.Message).Contains("nonsense");
    }
}
