using Build.Targets.PublishStaging.Services;
using Build.Tests.Fixtures;
using Cake.Core;
using Cake.Core.IO;
using NuGet.Versioning;
using TUnit.Assertions.Enums;
using static Build.Tests.Fixtures.NuGetFeedData;

namespace Build.Tests.Integration.PublishStaging;

/// <summary>
/// Drives <see cref="NuGetProtocolFeedClient"/> against a real local folder NuGet feed.
/// NuGet.Protocol's discovery side bypasses <c>ICakeContext.FileSystem</c> and reads
/// directly from disk; the download write side stays Cake-native, so the test wires a
/// real <see cref="FileSystem"/> into a substitute <see cref="ICakeContext"/>.
/// </summary>
public sealed class NuGetProtocolFeedClientTests
{
    [Test]
    public async Task GetLatestVersionAsync_Should_Return_Highest_Version_When_IncludePrerelease_True()
    {
        using var feed = new TempDirectory("janset-temp-feed-");
        await WriteMinimalNupkgAsync(feed.Path, "Test.Pkg", "1.0.0");
        await WriteMinimalNupkgAsync(feed.Path, "Test.Pkg", "2.0.0-preview.1");
        await WriteMinimalNupkgAsync(feed.Path, "Test.Pkg", "1.5.0");

        var client = CreateRealClient();

        var resolved = await client.GetLatestVersionAsync(feed.Path, AuthToken, "Test.Pkg", includePrerelease: true);

        await Assert.That(resolved).IsNotNull();
        await Assert.That(resolved!.ToNormalizedString()).IsEqualTo("2.0.0-preview.1");
    }

    [Test]
    public async Task GetLatestVersionAsync_Should_Skip_Prereleases_When_IncludePrerelease_False()
    {
        using var feed = new TempDirectory("janset-temp-feed-");
        await WriteMinimalNupkgAsync(feed.Path, "Test.Pkg", "1.0.0");
        await WriteMinimalNupkgAsync(feed.Path, "Test.Pkg", "2.0.0-preview.1");

        var client = CreateRealClient();

        var resolved = await client.GetLatestVersionAsync(feed.Path, AuthToken, "Test.Pkg", includePrerelease: false);

        await Assert.That(resolved).IsNotNull();
        await Assert.That(resolved!.ToNormalizedString()).IsEqualTo("1.0.0");
    }

    [Test]
    public async Task GetLatestVersionAsync_Should_Return_Null_When_Package_Not_In_Feed()
    {
        using var feed = new TempDirectory("janset-temp-feed-");
        await WriteMinimalNupkgAsync(feed.Path, "Test.Pkg", "1.0.0");

        var client = CreateRealClient();

        var resolved = await client.GetLatestVersionAsync(feed.Path, AuthToken, "Missing.Pkg", includePrerelease: true);

        await Assert.That(resolved).IsNull();
    }

    [Test]
    public async Task DownloadAsync_Should_Write_Nupkg_To_Target_Directory_Through_CakeContext_FileSystem()
    {
        using var feed = new TempDirectory("janset-temp-feed-");
        var sourcePath = await WriteMinimalNupkgAsync(feed.Path, "Test.Pkg", "1.0.0");
        var sourceBytes = await File.ReadAllBytesAsync(sourcePath);

        using var targetDir = new TempDirectory("janset-temp-target-");
        var client = CreateRealClient();

        var resolved = await client.DownloadAsync(
            feed.Path,
            AuthToken,
            "Test.Pkg",
            NuGetVersion.Parse("1.0.0"),
            new DirectoryPath(targetDir.Path));

        await Assert.That(resolved.GetFilename().FullPath).IsEqualTo("Test.Pkg.1.0.0.nupkg");
        await Assert.That(File.Exists(resolved.FullPath)).IsTrue();

        var downloadedBytes = await File.ReadAllBytesAsync(resolved.FullPath);
        await Assert.That(downloadedBytes.Length).IsEqualTo(sourceBytes.Length);
    }

    [Test]
    public async Task DownloadAsync_Should_Truncate_Stale_Partial_File_From_Prior_Failed_Download()
    {
        using var feed = new TempDirectory("janset-temp-feed-");
        var sourcePath = await WriteMinimalNupkgAsync(feed.Path, "Test.Pkg", "1.0.0");
        var sourceBytes = await File.ReadAllBytesAsync(sourcePath);

        using var targetDir = new TempDirectory("janset-temp-target-");
        var stalePath = System.IO.Path.Combine(targetDir.Path, "Test.Pkg.1.0.0.nupkg");
        await File.WriteAllBytesAsync(stalePath, new byte[sourceBytes.Length + 4096]);

        var client = CreateRealClient();

        var resolved = await client.DownloadAsync(
            feed.Path,
            AuthToken,
            "Test.Pkg",
            NuGetVersion.Parse("1.0.0"),
            new DirectoryPath(targetDir.Path));

        var downloadedBytes = await File.ReadAllBytesAsync(resolved.FullPath);
        await Assert.That(downloadedBytes.Length).IsEqualTo(sourceBytes.Length);
    }

    [Test]
    public async Task PushAsync_Should_Copy_Nupkg_To_Local_Folder_Feed()
    {
        using var feed = new TempDirectory("janset-temp-feed-");
        using var sourceDir = new TempDirectory("janset-temp-source-");

        var sourcePath = await WriteMinimalNupkgAsync(sourceDir.Path, "Test.Pkg", "1.0.0");
        var sourceBytes = await File.ReadAllBytesAsync(sourcePath);

        var client = CreateRealClient();

        await client.PushAsync(feed.Path, AuthToken, new FilePath(sourcePath));

        // Local-feed layout (v2 flat vs v3 hierarchical) is NuGet.Protocol's call;
        // the contract we care about is "the .nupkg landed somewhere under the feed
        // root with byte-identical content".
        var pushed = Directory.GetFiles(feed.Path, "*.nupkg", SearchOption.AllDirectories);
        await Assert.That(pushed.Length).IsEqualTo(1);

        var pushedBytes = await File.ReadAllBytesAsync(pushed[0]);
        await Assert.That(pushedBytes.Length).IsEqualTo(sourceBytes.Length);
    }

    [Test]
    public async Task PushAsync_Should_Publish_Only_Package_When_Symbol_Packages_Are_Beside_It()
    {
        using var feed = new TempDirectory("janset-temp-feed-");
        using var sourceDir = new TempDirectory("janset-temp-source-");
        var sourcePath = await WritePackageWithSymbolsAsync(sourceDir.Path);
        var sourceBytes = await File.ReadAllBytesAsync(sourcePath);
        var client = CreateRealClient();

        await client.PushAsync(feed.Path, AuthToken, new FilePath(sourcePath));

        var pushed = Directory.GetFiles(feed.Path, "*.nupkg", SearchOption.AllDirectories);
        await Assert.That(pushed.Length).IsEqualTo(1);
        var pushedBytes = await File.ReadAllBytesAsync(pushed[0]);
        await Assert.That(pushedBytes).IsEquivalentTo(sourceBytes, CollectionOrdering.Matching);
    }

    [Test]
    public async Task PushAsync_Should_Throw_When_Source_Nupkg_Missing()
    {
        using var feed = new TempDirectory("janset-temp-feed-");
        var client = CreateRealClient();

        await Assert.That(async () =>
                await client.PushAsync(feed.Path, AuthToken, new FilePath(System.IO.Path.Combine(feed.Path, "Missing.Pkg.1.0.0.nupkg"))))
            .Throws<Exception>();
    }
}
