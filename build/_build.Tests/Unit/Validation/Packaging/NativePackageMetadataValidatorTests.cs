using System.Collections.Immutable;
using System.IO.Compression;
using System.Text.Json;
using Build.Features.Packaging;
using Build.Shared.Manifest;
using Build.Tests.Fixtures;
using Build.Validation.Packaging;
using Cake.Core.IO;

namespace Build.Tests.Unit.Validation.Packaging;

/// <summary>
/// Solitary unit tests for <see cref="NativePackageMetadataValidator"/> — exercise the
/// validator directly against a Cake <c>FakeFileSystem</c> (the standard V2 boundary fake)
/// without routing through <see cref="PackageOutputValidator"/>. The sociable side
/// (consumer wires the real instance) is covered by
/// <c>PackageOutputValidatorTests.Validate_Should_*</c>.
/// </summary>
public sealed class NativePackageMetadataValidatorTests
{
    private const string ExpectedFamilyVersion = "2.32.0";
    private const string ExpectedCommit = "0123456789abcdef0123456789abcdef01234567";
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    [Test]
    public async Task ValidateAsync_Should_Return_Null_When_Metadata_Matches_Manifest()
    {
        var world = FakeCakeWorldV2.CreateWindows();
        var manifest = ManifestFixture.CreateTestManifestConfig();
        var family = manifest.PackageFamilies.Single(f => f.Name == "sdl2-core");
        var packagePath = world.RepoRoot.CombineWithFilePath("artifacts/packages/Janset.SDL2.Core.Native.2.32.0.nupkg");
        SeedNupkg(world, packagePath, ConsistentMetadata(manifest, family, ExpectedFamilyVersion, ExpectedCommit));

        var validator = new NativePackageMetadataValidator(world.FileSystem);

        var result = await validator.ValidateAsync(family, packagePath, ExpectedFamilyVersion, ExpectedCommit, manifest);

        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task ValidateAsync_Should_Fail_When_Native_Package_Is_Missing()
    {
        var world = FakeCakeWorldV2.CreateWindows();
        var manifest = ManifestFixture.CreateTestManifestConfig();
        var family = manifest.PackageFamilies.Single(f => f.Name == "sdl2-core");
        var packagePath = world.RepoRoot.CombineWithFilePath("artifacts/packages/Janset.SDL2.Core.Native.2.32.0.nupkg");
        // No nupkg seeded.

        var validator = new NativePackageMetadataValidator(world.FileSystem);

        var result = await validator.ValidateAsync(family, packagePath, ExpectedFamilyVersion, ExpectedCommit, manifest);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Code).IsEqualTo("G55");
        await Assert.That(result.Message).Contains("metadata file cannot be validated");
    }

    [Test]
    public async Task ValidateAsync_Should_Fail_When_Metadata_Entry_Missing_From_Nupkg()
    {
        var world = FakeCakeWorldV2.CreateWindows();
        var manifest = ManifestFixture.CreateTestManifestConfig();
        var family = manifest.PackageFamilies.Single(f => f.Name == "sdl2-core");
        var packagePath = world.RepoRoot.CombineWithFilePath("artifacts/packages/Janset.SDL2.Core.Native.2.32.0.nupkg");
        SeedNupkgWithEntries(world, packagePath, [("package.nuspec", "<package />")]);
        // No janset-native-metadata.json.

        var validator = new NativePackageMetadataValidator(world.FileSystem);

        var result = await validator.ValidateAsync(family, packagePath, ExpectedFamilyVersion, ExpectedCommit, manifest);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Code).IsEqualTo("G55");
        await Assert.That(result.Message).Contains("does not contain root metadata file");
    }

    [Test]
    public async Task ValidateAsync_Should_Fail_When_Metadata_Json_Is_Invalid()
    {
        var world = FakeCakeWorldV2.CreateWindows();
        var manifest = ManifestFixture.CreateTestManifestConfig();
        var family = manifest.PackageFamilies.Single(f => f.Name == "sdl2-core");
        var packagePath = world.RepoRoot.CombineWithFilePath("artifacts/packages/Janset.SDL2.Core.Native.2.32.0.nupkg");
        SeedNupkgWithEntries(world, packagePath, [("janset-native-metadata.json", "{not valid json")]);

        var validator = new NativePackageMetadataValidator(world.FileSystem);

        var result = await validator.ValidateAsync(family, packagePath, ExpectedFamilyVersion, ExpectedCommit, manifest);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Code).IsEqualTo("G55");
        await Assert.That(result.Message).Contains("not valid JSON");
    }

    [Test]
    public async Task ValidateAsync_Should_Fail_When_Family_Version_Mismatches_Expected()
    {
        var world = FakeCakeWorldV2.CreateWindows();
        var manifest = ManifestFixture.CreateTestManifestConfig();
        var family = manifest.PackageFamilies.Single(f => f.Name == "sdl2-core");
        var packagePath = world.RepoRoot.CombineWithFilePath("artifacts/packages/Janset.SDL2.Core.Native.2.32.0.nupkg");
        var driftedMetadata = ConsistentMetadata(manifest, family, "9.9.9", ExpectedCommit);
        SeedNupkg(world, packagePath, driftedMetadata);

        var validator = new NativePackageMetadataValidator(world.FileSystem);

        var result = await validator.ValidateAsync(family, packagePath, ExpectedFamilyVersion, ExpectedCommit, manifest);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Code).IsEqualTo("G55");
        await Assert.That(result.Message).Contains("janset_family_version expected '2.32.0' actual '9.9.9'");
    }

    [Test]
    public async Task ValidateAsync_Should_Fail_When_Build_Commit_Mismatches_Expected()
    {
        var world = FakeCakeWorldV2.CreateWindows();
        var manifest = ManifestFixture.CreateTestManifestConfig();
        var family = manifest.PackageFamilies.Single(f => f.Name == "sdl2-core");
        var packagePath = world.RepoRoot.CombineWithFilePath("artifacts/packages/Janset.SDL2.Core.Native.2.32.0.nupkg");
        var driftedMetadata = ConsistentMetadata(manifest, family, ExpectedFamilyVersion, "deadbeefdeadbeefdeadbeefdeadbeefdeadbeef");
        SeedNupkg(world, packagePath, driftedMetadata);

        var validator = new NativePackageMetadataValidator(world.FileSystem);

        var result = await validator.ValidateAsync(family, packagePath, ExpectedFamilyVersion, ExpectedCommit, manifest);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Message).Contains("build_commit expected");
    }

    [Test]
    public async Task ValidateAsync_Should_Aggregate_Multiple_Mismatches_In_One_Check()
    {
        var world = FakeCakeWorldV2.CreateWindows();
        var manifest = ManifestFixture.CreateTestManifestConfig();
        var family = manifest.PackageFamilies.Single(f => f.Name == "sdl2-core");
        var packagePath = world.RepoRoot.CombineWithFilePath("artifacts/packages/Janset.SDL2.Core.Native.2.32.0.nupkg");
        var library = manifest.LibraryManifests.Single(l => string.Equals(l.Name, family.LibraryRef, StringComparison.OrdinalIgnoreCase));
        var driftedMetadata = new NativePackageMetadata
        {
            JansetFamilyVersion = "9.9.9",                       // mismatch
            FamilyIdentifier = "sdl2-bogus",                     // mismatch
            UpstreamLibrary = "wrong-vcpkg-name",                // mismatch
            UpstreamVersion = library.VcpkgVersion,
            VcpkgPortVersion = library.VcpkgPortVersion,
            TripletSet = manifest.Runtimes.Select(r => r.Triplet).ToList(),
            BuildCommit = ExpectedCommit,
        };
        SeedNupkg(world, packagePath, driftedMetadata);

        var validator = new NativePackageMetadataValidator(world.FileSystem);

        var result = await validator.ValidateAsync(family, packagePath, ExpectedFamilyVersion, ExpectedCommit, manifest);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Message).Contains("janset_family_version");
        await Assert.That(result.Message).Contains("family_identifier");
        await Assert.That(result.Message).Contains("upstream_library");
    }

    private static NativePackageMetadata ConsistentMetadata(
        ManifestConfig manifest,
        PackageFamilyConfig family,
        string familyVersion,
        string commit)
    {
        var library = manifest.LibraryManifests.Single(l => string.Equals(l.Name, family.LibraryRef, StringComparison.OrdinalIgnoreCase));
        return new NativePackageMetadata
        {
            JansetFamilyVersion = familyVersion,
            FamilyIdentifier = family.Name,
            UpstreamLibrary = library.VcpkgName,
            UpstreamVersion = library.VcpkgVersion,
            VcpkgPortVersion = library.VcpkgPortVersion,
            TripletSet = manifest.Runtimes
                .Select(r => r.Triplet)
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(t => t, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            BuildCommit = commit,
        };
    }

    private static void SeedNupkg(FakeCakeWorldV2 world, FilePath nupkgPath, NativePackageMetadata metadata)
    {
        SeedNupkgWithEntries(world, nupkgPath,
        [
            ("janset-native-metadata.json", JsonSerializer.Serialize(metadata, JsonOptions)),
            ("package.nuspec", "<package />"),
        ]);
    }

    private static void SeedNupkgWithEntries(FakeCakeWorldV2 world, FilePath nupkgPath, ImmutableArray<(string Entry, string Content)> entries)
    {
        var dir = world.FileSystem.GetDirectory(nupkgPath.GetDirectory());
        if (!dir.Exists)
        {
            dir.Create();
        }

        var file = world.FileSystem.GetFile(nupkgPath);
        using var stream = file.Open(FileMode.Create, FileAccess.Write, FileShare.None);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: false);

        foreach (var (name, content) in entries)
        {
            var entry = archive.CreateEntry(name);
            using var writer = new StreamWriter(entry.Open());
            writer.Write(content);
        }
    }
}
