using System.Globalization;
using System.IO;
using System.IO.Compression;
using Build.Infrastructure.DotNet;
using Build.Tests.Fixtures;
using Cake.Core;
using Cake.Core.IO;
using Cake.Testing;
using NSubstitute;
using NuGet.Versioning;

namespace Build.Tests.Integration.Packaging;

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
        WriteMinimalNupkg(feed.Path, "Test.Pkg", "1.0.0");
        WriteMinimalNupkg(feed.Path, "Test.Pkg", "2.0.0-preview.1");
        WriteMinimalNupkg(feed.Path, "Test.Pkg", "1.5.0");

        var client = CreateRealClient();

        var resolved = await client.GetLatestVersionAsync(feed.Path, "ignored", "Test.Pkg", includePrerelease: true);

        await Assert.That(resolved).IsNotNull();
        await Assert.That(resolved!.ToNormalizedString()).IsEqualTo("2.0.0-preview.1");
    }

    [Test]
    public async Task GetLatestVersionAsync_Should_Skip_Prereleases_When_IncludePrerelease_False()
    {
        using var feed = new TempDirectory("janset-temp-feed-");
        WriteMinimalNupkg(feed.Path, "Test.Pkg", "1.0.0");
        WriteMinimalNupkg(feed.Path, "Test.Pkg", "2.0.0-preview.1");

        var client = CreateRealClient();

        var resolved = await client.GetLatestVersionAsync(feed.Path, "ignored", "Test.Pkg", includePrerelease: false);

        await Assert.That(resolved).IsNotNull();
        await Assert.That(resolved!.ToNormalizedString()).IsEqualTo("1.0.0");
    }

    [Test]
    public async Task GetLatestVersionAsync_Should_Return_Null_When_Package_Not_In_Feed()
    {
        using var feed = new TempDirectory("janset-temp-feed-");
        WriteMinimalNupkg(feed.Path, "Test.Pkg", "1.0.0");

        var client = CreateRealClient();

        var resolved = await client.GetLatestVersionAsync(feed.Path, "ignored", "Missing.Pkg", includePrerelease: true);

        await Assert.That(resolved).IsNull();
    }

    [Test]
    public async Task DownloadAsync_Should_Write_Nupkg_To_Target_Directory_Through_CakeContext_FileSystem()
    {
        using var feed = new TempDirectory("janset-temp-feed-");
        var sourcePath = WriteMinimalNupkg(feed.Path, "Test.Pkg", "1.0.0");
        var sourceBytes = await File.ReadAllBytesAsync(sourcePath);

        using var targetDir = new TempDirectory("janset-temp-target-");
        var client = CreateRealClient();

        var resolved = await client.DownloadAsync(
            feed.Path,
            "ignored",
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
        var sourcePath = WriteMinimalNupkg(feed.Path, "Test.Pkg", "1.0.0");
        var sourceBytes = await File.ReadAllBytesAsync(sourcePath);

        using var targetDir = new TempDirectory("janset-temp-target-");
        var stalePath = System.IO.Path.Combine(targetDir.Path, "Test.Pkg.1.0.0.nupkg");
        await File.WriteAllBytesAsync(stalePath, new byte[sourceBytes.Length + 4096]);

        var client = CreateRealClient();

        var resolved = await client.DownloadAsync(
            feed.Path,
            "ignored",
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

        var sourcePath = WriteMinimalNupkg(sourceDir.Path, "Test.Pkg", "1.0.0");
        var sourceBytes = await File.ReadAllBytesAsync(sourcePath);

        var client = CreateRealClient();

        await client.PushAsync(feed.Path, "ignored", new FilePath(sourcePath));

        // Local-feed layout (v2 flat vs v3 hierarchical) is NuGet.Protocol's call;
        // the contract we care about is "the .nupkg landed somewhere under the feed
        // root with byte-identical content".
        var pushed = Directory.GetFiles(feed.Path, "*.nupkg", SearchOption.AllDirectories);
        await Assert.That(pushed.Length).IsEqualTo(1);

        var pushedBytes = await File.ReadAllBytesAsync(pushed[0]);
        await Assert.That(pushedBytes.Length).IsEqualTo(sourceBytes.Length);
    }

    [Test]
    public async Task PushAsync_Should_Throw_When_Source_Nupkg_Missing()
    {
        using var feed = new TempDirectory("janset-temp-feed-");
        var client = CreateRealClient();

        await Assert.That(async () =>
                await client.PushAsync(feed.Path, "ignored", new FilePath(System.IO.Path.Combine(feed.Path, "Missing.Pkg.1.0.0.nupkg"))))
            .Throws<Exception>();
    }

    private static NuGetProtocolFeedClient CreateRealClient()
    {
        var environment = OperatingSystem.IsWindows()
            ? FakeEnvironment.CreateWindowsEnvironment()
            : FakeEnvironment.CreateUnixEnvironment();

        var context = Substitute.For<ICakeContext>();
        context.FileSystem.Returns(new FileSystem());
        context.Log.Returns(new FakeLog());
        context.Environment.Returns(environment);

        return new NuGetProtocolFeedClient(context, new FakeLog());
    }

    private static string WriteMinimalNupkg(string feedDir, string packageId, string version)
    {
        var nupkgPath = System.IO.Path.Combine(feedDir, $"{packageId}.{version}.nupkg");
        using var fs = File.Create(nupkgPath);
        using var archive = new ZipArchive(fs, ZipArchiveMode.Create);

        var nuspecEntry = archive.CreateEntry($"{packageId}.nuspec");
        using var nuspecStream = nuspecEntry.Open();
        using var writer = new StreamWriter(nuspecStream);
        writer.Write(string.Format(
            CultureInfo.InvariantCulture,
            "<?xml version=\"1.0\" encoding=\"utf-8\"?>" +
            "<package xmlns=\"http://schemas.microsoft.com/packaging/2010/07/nuspec.xsd\">" +
            "<metadata>" +
            "<id>{0}</id>" +
            "<version>{1}</version>" +
            "<authors>test</authors>" +
            "<description>test</description>" +
            "</metadata>" +
            "</package>",
            packageId,
            version));

        return nupkgPath;
    }
}
