using System.Text.Json;
using Build.Features.Versioning;
using Build.Tests.Fixtures;
using NuGet.Versioning;

namespace Build.Tests.Unit.Features.Versioning;

/// <summary>
/// Unit tests for <see cref="VersionsJsonWriter"/>. The writer produces the canonical
/// flat JSON shape consumed by stage targets via <c>--versions-file</c>. Both
/// <c>ResolveVersionsFromManifestTask</c> and <c>ResolveVersionsFromExplicitTask</c>
/// inject this writer; tests here cover the shared output contract directly.
/// </summary>
public sealed class VersionsJsonWriterTests
{
    [Test]
    public async Task WriteAsync_Should_Sort_Keys_OrdinalIgnoreCase()
    {
        // Input order should not affect output — keys sorted case-insensitively.
        var repo = new FakeRepoBuilder(FakeRepoPlatform.Windows).BuildContextWithHandles();
        var writer = new VersionsJsonWriter(repo.CakeContext, repo.Paths, repo.CakeContext.Log);
        var mapping = new Dictionary<string, NuGetVersion>(StringComparer.OrdinalIgnoreCase)
        {
            ["sdl2-image"] = NuGetVersion.Parse("2.8.0"),
            ["sdl2-core"] = NuGetVersion.Parse("2.32.0"),
            ["sdl2-mixer"] = NuGetVersion.Parse("2.8.0"),
        };

        await writer.WriteAsync(mapping);

        var deserialized = await ReadAsync(repo);
        await Assert.That(deserialized.Keys.ToArray()).IsEquivalentTo(["sdl2-core", "sdl2-image", "sdl2-mixer"]);
    }

    [Test]
    public async Task WriteAsync_Should_Use_NuGet_Normalized_Version_Strings()
    {
        // NuGetVersion normalization strips leading zeros, normalizes prerelease, etc.
        // Round-trip through writer must preserve the normalized form so consumers can
        // parse without surprises.
        var repo = new FakeRepoBuilder(FakeRepoPlatform.Windows).BuildContextWithHandles();
        var writer = new VersionsJsonWriter(repo.CakeContext, repo.Paths, repo.CakeContext.Log);
        var version = NuGetVersion.Parse("2.32.0-rc.1+build.42");
        var mapping = new Dictionary<string, NuGetVersion>(StringComparer.OrdinalIgnoreCase)
        {
            ["sdl2-core"] = version,
        };

        await writer.WriteAsync(mapping);

        var deserialized = await ReadAsync(repo);
        await Assert.That(deserialized["sdl2-core"]).IsEqualTo(version.ToNormalizedString());
    }

    [Test]
    public async Task WriteAsync_Should_Create_Output_Directory_When_Missing()
    {
        // FakeFileSystem starts empty — writer must create the artifacts/resolve-versions
        // directory before writing. Tests previously relied on this implicitly via
        // ResolveVersionsPipeline; preserved here at the writer layer.
        var repo = new FakeRepoBuilder(FakeRepoPlatform.Windows).BuildContextWithHandles();
        var writer = new VersionsJsonWriter(repo.CakeContext, repo.Paths, repo.CakeContext.Log);
        var mapping = new Dictionary<string, NuGetVersion>(StringComparer.OrdinalIgnoreCase)
        {
            ["sdl2-core"] = NuGetVersion.Parse("2.32.0"),
        };

        await writer.WriteAsync(mapping);

        var outputFile = repo.Paths.GetResolveVersionsOutputFile();
        var writtenFile = repo.FileSystem.GetFile(outputFile);
        await Assert.That(writtenFile.Exists).IsTrue();
    }

    [Test]
    public async Task WriteAsync_Should_Throw_OperationCanceledException_When_Cancelled()
    {
        // Cancellation propagates — defensive guarantee for callers that pass a token
        // through (manifest/explicit tasks today pass default; future Cake.Frosting
        // upgrade may surface a real ct).
        var repo = new FakeRepoBuilder(FakeRepoPlatform.Windows).BuildContextWithHandles();
        var writer = new VersionsJsonWriter(repo.CakeContext, repo.Paths, repo.CakeContext.Log);
        var mapping = new Dictionary<string, NuGetVersion>(StringComparer.OrdinalIgnoreCase)
        {
            ["sdl2-core"] = NuGetVersion.Parse("2.32.0"),
        };
        var cancelled = new CancellationToken(canceled: true);

        await Assert.ThrowsAsync<OperationCanceledException>(() => writer.WriteAsync(mapping, cancelled));
    }

    private static async Task<SortedDictionary<string, string>> ReadAsync(FakeRepoHandles repo)
    {
        var outputFile = repo.Paths.GetResolveVersionsOutputFile();
        var writtenFile = repo.FileSystem.GetFile(outputFile);

        string content;
        using (var stream = writtenFile.Open(FileMode.Open, FileAccess.Read, FileShare.Read))
        using (var reader = new StreamReader(stream))
        {
            content = await reader.ReadToEndAsync();
        }

        return JsonSerializer.Deserialize<SortedDictionary<string, string>>(content)!;
    }
}
