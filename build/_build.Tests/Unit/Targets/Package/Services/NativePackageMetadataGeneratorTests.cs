using Build.Data.NativePackageMetadata;
using Build.Data.Manifest.Models;
using Build.Host.Paths;
using Build.Targets.Package.Services;
using Build.Tests.Fixtures;
using Cake.Core.IO;
using NSubstitute;

namespace Build.Tests.Unit.Targets.Package.Services;

public sealed class NativePackageMetadataGeneratorTests
{
    [Test]
    public async Task GenerateAsync_Should_Write_Metadata_To_Native_Metadata_Path()
    {
        var manifest = ManifestFixture.CreateTestManifestConfig();
        var family = manifest.PackageFamilies.Single(candidate => candidate.Name == "sdl2-core");
        var metadataPath = new FilePath("artifacts/harvest_output/sdl2/janset-native-metadata.json");
        var paths = Substitute.For<IPathService>();
        paths.GetHarvestLibraryNativeMetadataFile(family.LibraryRef).Returns(metadataPath);
        var repository = Substitute.For<INativePackageMetadataRepository>();
        var generator = new NativePackageMetadataGenerator(paths, repository);

        await generator.GenerateAsync(manifest, family, "2.32.0", "0123456789abcdef0123456789abcdef01234567");

        await repository.Received(1).WriteAsync(
            metadataPath,
            Arg.Is<NativePackageMetadataDocument>(metadata =>
                metadata.JansetFamilyVersion == "2.32.0" &&
                metadata.FamilyIdentifier == "sdl2-core" &&
                metadata.UpstreamLibrary == "sdl2" &&
                metadata.BuildCommit == "0123456789abcdef0123456789abcdef01234567" &&
                metadata.TripletSet.Count > 0),
            Arg.Any<CancellationToken>());
    }
}
