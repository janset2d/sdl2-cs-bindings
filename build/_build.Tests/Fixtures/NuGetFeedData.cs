using System.Globalization;
using System.IO.Compression;
using Build.Targets.PublishStaging.Services;
using Cake.Core;
using Cake.Core.IO;
using Cake.Testing;
using NSubstitute;

namespace Build.Tests.Fixtures;

internal static class NuGetFeedData
{
    public const string AuthToken = "ignored";

    public static NuGetProtocolFeedClient CreateRealClient()
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

    public static async Task<string> WritePackageWithSymbolsAsync(string sourceDir)
    {
        var packagePath = await WriteMinimalNupkgAsync(sourceDir, "Test.Pkg", "1.0.0");
        var symbolPath = System.IO.Path.ChangeExtension(packagePath, ".symbols.nupkg");
        File.Copy(packagePath, symbolPath);

        await using (var archive = await ZipFile.OpenAsync(symbolPath, ZipArchiveMode.Update))
        {
            archive.CreateEntry("symbols.pdb");
        }

        File.Copy(symbolPath, System.IO.Path.ChangeExtension(packagePath, ".snupkg"));
        return packagePath;
    }

    public static async Task<string> WriteMinimalNupkgAsync(string feedDir, string packageId, string version)
    {
        var nupkgPath = System.IO.Path.Combine(feedDir, $"{packageId}.{version}.nupkg");
        await using var fs = File.Create(nupkgPath);
        await using var archive = new ZipArchive(fs, ZipArchiveMode.Create);

        var nuspecEntry = archive.CreateEntry($"{packageId}.nuspec");
        await using var nuspecStream = await nuspecEntry.OpenAsync();
        await using var writer = new StreamWriter(nuspecStream);
        await writer.WriteAsync(string.Format(
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
