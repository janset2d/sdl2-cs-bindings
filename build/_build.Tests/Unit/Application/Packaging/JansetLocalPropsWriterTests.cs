using System.IO;
using System.Xml.Linq;
using Build.Application.Packaging;
using Build.Tests.Fixtures;
using Cake.Core.IO;
using NuGet.Versioning;

namespace Build.Tests.Unit.Application.Packaging;

/// <summary>
/// Pins the byte-equivalence contract of <see cref="JansetLocalPropsWriter"/>: both
/// <see cref="LocalArtifactSourceResolver"/> and <see cref="RemoteArtifactSourceResolver"/>
/// stamp <c>Janset.Local.props</c> through this single helper, so the helper's output
/// shape becomes the de-facto IDE-direct-restore contract.
/// </summary>
public sealed class JansetLocalPropsWriterTests
{
    [Test]
    public async Task BuildContent_Should_Emit_LocalPackageFeed_And_Per_Family_Properties()
    {
        var feedPath = new DirectoryPath("/repo/artifacts/packages");
        var versions = new Dictionary<string, NuGetVersion>(StringComparer.OrdinalIgnoreCase)
        {
            ["sdl2-core"] = NuGetVersion.Parse("2.32.0-preview.1"),
            ["sdl2-image"] = NuGetVersion.Parse("2.8.0-preview.1"),
        };

        var xml = JansetLocalPropsWriter.BuildContent(feedPath, versions);

        var document = XDocument.Parse(xml);
        var propertyGroup = document.Root?.Element("PropertyGroup");
        await Assert.That(propertyGroup).IsNotNull();

        await Assert.That(propertyGroup!.Element("LocalPackageFeed")?.Value).IsEqualTo(feedPath.FullPath);
        await Assert.That(propertyGroup.Element("JansetSdl2CorePackageVersion")?.Value).IsEqualTo("2.32.0-preview.1");
        await Assert.That(propertyGroup.Element("JansetSdl2ImagePackageVersion")?.Value).IsEqualTo("2.8.0-preview.1");
    }

    [Test]
    public async Task BuildContent_Should_Order_Families_By_Identifier_For_Deterministic_Output()
    {
        var feedPath = new DirectoryPath("/repo/artifacts/packages");

        // Deliberately disordered input mapping; output must still be stable-ordered
        // so two byte-identical-input runs produce byte-identical files (matters for
        // Janset.Local.props diffability, IDE restore-cache invariance).
        var versions = new Dictionary<string, NuGetVersion>(StringComparer.OrdinalIgnoreCase)
        {
            ["sdl2-mixer"] = NuGetVersion.Parse("2.8.0-preview.1"),
            ["sdl2-core"] = NuGetVersion.Parse("2.32.0-preview.1"),
            ["sdl2-image"] = NuGetVersion.Parse("2.8.0-preview.1"),
        };

        var xml = JansetLocalPropsWriter.BuildContent(feedPath, versions);

        var coreIndex = xml.IndexOf("JansetSdl2CorePackageVersion", StringComparison.Ordinal);
        var imageIndex = xml.IndexOf("JansetSdl2ImagePackageVersion", StringComparison.Ordinal);
        var mixerIndex = xml.IndexOf("JansetSdl2MixerPackageVersion", StringComparison.Ordinal);

        await Assert.That(coreIndex).IsGreaterThan(0);
        await Assert.That(imageIndex).IsGreaterThan(coreIndex);
        await Assert.That(mixerIndex).IsGreaterThan(imageIndex);
    }

    [Test]
    public async Task WriteAsync_Should_Write_File_Through_CakeContext_When_Directory_Missing()
    {
        var repo = new FakeRepoBuilder(FakeRepoPlatform.Windows)
            .BuildContextWithHandles();

        var versions = new Dictionary<string, NuGetVersion>(StringComparer.OrdinalIgnoreCase)
        {
            ["sdl2-core"] = NuGetVersion.Parse("2.32.0-preview.1"),
        };

        // Parent build/msbuild directory does not exist on the FakeFileSystem yet;
        // WriteAsync must EnsureDirectoryExists before writing.
        await JansetLocalPropsWriter.WriteAsync(
            repo.CakeContext,
            repo.Paths,
            repo.Paths.PackagesOutput,
            versions);

        var propsPath = repo.Paths.GetLocalPropsFile();
        var file = repo.FileSystem.GetFile(propsPath);
        await Assert.That(file.Exists).IsTrue();

        string actual;
        using (var stream = file.OpenRead())
        using (var reader = new StreamReader(stream))
        {
            actual = await reader.ReadToEndAsync();
        }

        await Assert.That(actual).Contains("LocalPackageFeed");
        await Assert.That(actual).Contains("JansetSdl2CorePackageVersion");
        await Assert.That(actual).Contains("2.32.0-preview.1");
    }
}
