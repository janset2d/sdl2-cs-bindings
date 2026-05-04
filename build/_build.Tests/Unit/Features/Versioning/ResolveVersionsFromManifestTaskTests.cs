using System.Text.Json;
using Build.Features.Versioning;
using Build.Tests.Fixtures;
using Cake.Core;

namespace Build.Tests.Unit.Features.Versioning;

/// <summary>
/// Integration-style tests for <see cref="ResolveVersionsFromManifestTask"/>.
/// Each test sets up <see cref="ParsedArguments"/> via <see cref="FakeRepoBuilder"/>,
/// invokes the task, and asserts on the produced versions.json or thrown exception.
/// Scenarios are ports of the retired <c>ManifestVersionProviderTests</c> +
/// <c>ResolveVersionsPipelineTests</c> manifest-mode entries (plan v4 §4.5).
/// </summary>
public sealed class ResolveVersionsFromManifestTaskTests
{
    [Test]
    public async Task RunAsync_Should_Write_Versions_With_UpstreamMajorMinor_Zero_Suffix_Per_Family()
    {
        // Ports: ManifestVersionProviderTests.ResolveAsync_Should_Compose_UpstreamMajorMinor_Zero_Suffix_Per_Family
        // + ResolveVersionsPipelineTests.RunAsync_Should_Write_Canonical_Json_For_Manifest_Source_Happy_Path
        var manifest = ManifestFixture.CreateTestManifestConfig();
        var repo = new FakeRepoBuilder(FakeRepoPlatform.Windows)
            .WithManifest(manifest)
            .WithSuffix("local.20260421T143022")
            .BuildContextWithHandles();

        var task = new ResolveVersionsFromManifestTask(manifest, CreateWriter(repo));

        await task.RunAsync(repo.BuildContext);

        var deserialized = await ReadVersionsJsonAsync(repo);
        await Assert.That(deserialized.Count).IsEqualTo(2);
        await Assert.That(deserialized["sdl2-core"]).IsEqualTo("2.32.0-local.20260421T143022");
        await Assert.That(deserialized["sdl2-image"]).IsEqualTo("2.8.0-local.20260421T143022");
    }

    [Test]
    public async Task RunAsync_Should_Write_Only_Scoped_Families_When_Scope_Is_Subset()
    {
        // Ports: ManifestVersionProviderTests.ResolveAsync_Should_Return_Only_Requested_Families_When_Scope_Is_Subset
        // + ResolveVersionsPipelineTests.RunAsync_Should_Honor_Scope_Filter_When_Supplied
        var manifest = ManifestFixture.CreateTestManifestConfig();
        var repo = new FakeRepoBuilder(FakeRepoPlatform.Windows)
            .WithManifest(manifest)
            .WithSuffix("ci.12345")
            .WithScope("sdl2-core")
            .BuildContextWithHandles();

        var task = new ResolveVersionsFromManifestTask(manifest, CreateWriter(repo));

        await task.RunAsync(repo.BuildContext);

        var deserialized = await ReadVersionsJsonAsync(repo);
        await Assert.That(deserialized.Count).IsEqualTo(1);
        await Assert.That(deserialized["sdl2-core"]).IsEqualTo("2.32.0-ci.12345");
        await Assert.That(deserialized.ContainsKey("sdl2-image")).IsFalse();
    }

    [Test]
    public async Task RunAsync_Should_Throw_When_Scoped_Family_Is_Missing_From_Manifest()
    {
        // Ports: ManifestVersionProviderTests.ResolveAsync_Should_Throw_When_Requested_Family_Is_Missing_From_Manifest
        var manifest = ManifestFixture.CreateTestManifestConfig();
        var repo = new FakeRepoBuilder(FakeRepoPlatform.Windows)
            .WithManifest(manifest)
            .WithSuffix("ci.12345")
            .WithScope("sdl2-core", "sdl2-made-up")
            .BuildContextWithHandles();

        var task = new ResolveVersionsFromManifestTask(manifest, CreateWriter(repo));

        var exception = await Assert.ThrowsAsync<CakeException>(() => task.RunAsync(repo.BuildContext));
        await Assert.That(exception!.Message).Contains("sdl2-made-up");
        await Assert.That(exception.Message).Contains("package_families");
    }

    [Test]
    public async Task RunAsync_Should_Throw_When_Suffix_Produces_Invalid_NuGet_SemVer()
    {
        // Ports: ManifestVersionProviderTests.ResolveAsync_Should_Throw_When_Suffix_Produces_Invalid_NuGet_SemVer
        // SemVer prerelease identifier disallows underscore — surfaces deterministically.
        var manifest = ManifestFixture.CreateTestManifestConfig();
        var repo = new FakeRepoBuilder(FakeRepoPlatform.Windows)
            .WithManifest(manifest)
            .WithSuffix("bad_suffix")
            .BuildContextWithHandles();

        var task = new ResolveVersionsFromManifestTask(manifest, CreateWriter(repo));

        var exception = await Assert.ThrowsAsync<CakeException>(() => task.RunAsync(repo.BuildContext));
        await Assert.That(exception!.Message).Contains("bad_suffix");
        await Assert.That(exception.Message).Contains("prerelease");
    }

    [Test]
    public async Task RunAsync_Should_Throw_When_Suffix_Is_Missing()
    {
        // Ports: ResolveVersionsPipelineTests.RunAsync_Should_Throw_When_Manifest_Source_Has_No_Suffix
        // + ManifestVersionProviderTests.Constructor_Should_Reject_Empty_Suffix (now task-level)
        var manifest = ManifestFixture.CreateTestManifestConfig();
        var repo = new FakeRepoBuilder(FakeRepoPlatform.Windows)
            .WithManifest(manifest)
            .BuildContextWithHandles();

        var task = new ResolveVersionsFromManifestTask(manifest, CreateWriter(repo));

        var exception = await Assert.ThrowsAsync<CakeException>(() => task.RunAsync(repo.BuildContext));
        await Assert.That(exception!.Message).Contains("--suffix");
    }

    [Test]
    public async Task RunAsync_Should_Throw_When_Suffix_Is_Whitespace()
    {
        // Companion to the missing-suffix scenario; whitespace-only must also fail loud.
        // Original ManifestVersionProvider's NormalizeSuffix rejected both null AND whitespace
        // (Constructor_Should_Reject_Empty_Suffix). Preserve both branches at task level.
        var manifest = ManifestFixture.CreateTestManifestConfig();
        var repo = new FakeRepoBuilder(FakeRepoPlatform.Windows)
            .WithManifest(manifest)
            .WithSuffix("   ")
            .BuildContextWithHandles();

        var task = new ResolveVersionsFromManifestTask(manifest, CreateWriter(repo));

        var exception = await Assert.ThrowsAsync<CakeException>(() => task.RunAsync(repo.BuildContext));
        await Assert.That(exception!.Message).Contains("--suffix");
    }

    private static VersionsJsonWriter CreateWriter(FakeRepoHandles repo) =>
        new(repo.CakeContext, repo.Paths, repo.CakeContext.Log);

    private static async Task<SortedDictionary<string, string>> ReadVersionsJsonAsync(FakeRepoHandles repo)
    {
        var outputFile = repo.Paths.GetResolveVersionsOutputFile();
        var writtenFile = repo.FileSystem.GetFile(outputFile);
        await Assert.That(writtenFile.Exists).IsTrue();

        string content;
        await using (var stream = writtenFile.Open(FileMode.Open, FileAccess.Read, FileShare.Read))
        using (var reader = new StreamReader(stream))
        {
            content = await reader.ReadToEndAsync();
        }

        var deserialized = JsonSerializer.Deserialize<SortedDictionary<string, string>>(content);
        await Assert.That(deserialized).IsNotNull();
        return deserialized!;
    }
}
