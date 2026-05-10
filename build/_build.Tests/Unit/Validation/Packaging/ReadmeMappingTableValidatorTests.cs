using Build.Targets.Package.Models;
using Build.Tests.Fixtures;
using Build.Validation.Packaging;
using Cake.Core.IO;

namespace Build.Tests.Unit.Validation.Packaging;

/// <summary>
/// Solitary unit tests for <see cref="ReadmeMappingTableValidator"/> — exercise the
/// validator directly against a Cake <c>FakeFileSystem</c> without routing through
/// <see cref="PackageOutputValidator"/>. The sociable side (consumer wires the real
/// instance) is covered by <c>PackageOutputValidatorTests</c>.
/// </summary>
public sealed class ReadmeMappingTableValidatorTests
{
    [Test]
    public async Task Validate_Should_Return_Null_When_Block_Matches_Generator_Output()
    {
        var manifest = ManifestFixture.CreateTestManifestConfig();
        var world = FakeCakeWorldV2.CreateWindows();
        var readmePath = world.RepoRoot.CombineWithFilePath("README.md");
        world.WithTextFile(readmePath, ReadmeMappingTableBlock.BuildBlock(manifest));
        var family = manifest.PackageFamilies[0];

        var validator = new ReadmeMappingTableValidator(world.FileSystem);

        var result = validator.Validate(family, readmePath, manifest);

        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task Validate_Should_Fail_When_README_Does_Not_Exist()
    {
        var manifest = ManifestFixture.CreateTestManifestConfig();
        var world = FakeCakeWorldV2.CreateWindows();
        var readmePath = world.RepoRoot.CombineWithFilePath("README.md");
        // No README seeded.
        var family = manifest.PackageFamilies[0];

        var validator = new ReadmeMappingTableValidator(world.FileSystem);

        var result = validator.Validate(family, readmePath, manifest);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Code).IsEqualTo("G57");
        await Assert.That(result.Message).Contains("does not exist");
    }

    [Test]
    public async Task Validate_Should_Fail_When_Markers_Are_Missing()
    {
        var manifest = ManifestFixture.CreateTestManifestConfig();
        var world = FakeCakeWorldV2.CreateWindows();
        var readmePath = world.RepoRoot.CombineWithFilePath("README.md");
        world.WithTextFile(readmePath, "# Project\n\nNo mapping block here.\n");
        var family = manifest.PackageFamilies[0];

        var validator = new ReadmeMappingTableValidator(world.FileSystem);

        var result = validator.Validate(family, readmePath, manifest);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Code).IsEqualTo("G57");
        await Assert.That(result.Message).Contains("missing mapping table markers");
    }

    [Test]
    public async Task Validate_Should_Fail_When_Block_Is_Stale()
    {
        var manifest = ManifestFixture.CreateTestManifestConfig();
        var world = FakeCakeWorldV2.CreateWindows();
        var readmePath = world.RepoRoot.CombineWithFilePath("README.md");
        // Readme has the markers but with a stale (different) inner block.
        var staleBlock = $"{ReadmeMappingTableBlock.StartMarker}\n| stale | row |\n{ReadmeMappingTableBlock.EndMarker}";
        world.WithTextFile(readmePath, staleBlock);
        var family = manifest.PackageFamilies[0];

        var validator = new ReadmeMappingTableValidator(world.FileSystem);

        var result = validator.Validate(family, readmePath, manifest);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Code).IsEqualTo("G57");
        await Assert.That(result.Message).Contains("stale");
    }

    [Test]
    public async Task Validate_Should_Match_When_Block_Uses_CRLF_Line_Endings()
    {
        var manifest = ManifestFixture.CreateTestManifestConfig();
        var world = FakeCakeWorldV2.CreateWindows();
        var readmePath = world.RepoRoot.CombineWithFilePath("README.md");
        var crlfBlock = ReadmeMappingTableBlock.BuildBlock(manifest).Replace("\n", "\r\n", StringComparison.Ordinal);
        world.WithTextFile(readmePath, crlfBlock);
        var family = manifest.PackageFamilies[0];

        var validator = new ReadmeMappingTableValidator(world.FileSystem);

        var result = validator.Validate(family, readmePath, manifest);

        await Assert.That(result).IsNull();
    }
}
