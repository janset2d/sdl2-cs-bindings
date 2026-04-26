using System.IO;
using System.Text.Json;
using Build.Application.Packaging;
using Build.Tests.Fixtures;
using Cake.Core.IO;
using NuGet.Versioning;

namespace Build.Tests.Unit.Application.Packaging;

public sealed class VersionsFileWriterTests
{
    private static readonly string[] ExpectedFamilyOrder = ["sdl2-core", "sdl2-image", "sdl2-mixer"];

    [Test]
    public async Task WriteAsync_Should_Emit_Sorted_Family_Id_Keyed_Json()
    {
        var repo = new FakeRepoBuilder(FakeRepoPlatform.Windows).BuildContextWithHandles();

        // Disordered input → output must still be alphabetical-by-family for deterministic
        // diffability (matches the Cake-side resolve-versions output shape).
        var versions = new Dictionary<string, NuGetVersion>(StringComparer.OrdinalIgnoreCase)
        {
            ["sdl2-mixer"] = NuGetVersion.Parse("2.8.0-ci.1"),
            ["sdl2-core"] = NuGetVersion.Parse("2.32.0-ci.1"),
            ["sdl2-image"] = NuGetVersion.Parse("2.8.0-ci.1"),
        };

        await VersionsFileWriter.WriteAsync(repo.CakeContext, repo.Paths, versions);

        var outputFile = repo.Paths.GetResolveVersionsOutputFile();
        var file = repo.FileSystem.GetFile(outputFile);
        await Assert.That(file.Exists).IsTrue();

        string json;
        using (var stream = file.OpenRead())
        using (var reader = new StreamReader(stream))
        {
            json = await reader.ReadToEndAsync();
        }

        using var doc = JsonDocument.Parse(json);
        var props = doc.RootElement.EnumerateObject().Select(p => p.Name).ToArray();
        await Assert.That(props).IsEquivalentTo(ExpectedFamilyOrder);

        await Assert.That(doc.RootElement.GetProperty("sdl2-core").GetString()).IsEqualTo("2.32.0-ci.1");
        await Assert.That(doc.RootElement.GetProperty("sdl2-image").GetString()).IsEqualTo("2.8.0-ci.1");
        await Assert.That(doc.RootElement.GetProperty("sdl2-mixer").GetString()).IsEqualTo("2.8.0-ci.1");
    }

    [Test]
    public async Task WriteAsync_Should_Use_Normalized_Version_Strings()
    {
        var repo = new FakeRepoBuilder(FakeRepoPlatform.Windows).BuildContextWithHandles();

        // SemVer 2 input with build metadata; ToNormalizedString strips build metadata
        // and lowercases prerelease labels, which is the shape downstream Cake stages
        // consume via --versions-file.
        var versions = new Dictionary<string, NuGetVersion>(StringComparer.OrdinalIgnoreCase)
        {
            ["sdl2-core"] = NuGetVersion.Parse("2.32.0-CI.1+build.42"),
        };

        await VersionsFileWriter.WriteAsync(repo.CakeContext, repo.Paths, versions);

        var file = repo.FileSystem.GetFile(repo.Paths.GetResolveVersionsOutputFile());
        string json;
        using (var stream = file.OpenRead())
        using (var reader = new StreamReader(stream))
        {
            json = await reader.ReadToEndAsync();
        }

        using var doc = JsonDocument.Parse(json);
        await Assert.That(doc.RootElement.GetProperty("sdl2-core").GetString()).IsEqualTo("2.32.0-CI.1");
    }
}
