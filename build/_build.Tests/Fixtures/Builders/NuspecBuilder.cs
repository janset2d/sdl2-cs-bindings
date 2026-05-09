using System.IO.Compression;
using System.Text;
using System.Xml.Linq;

namespace Build.Tests.Fixtures.Builders;

/// <summary>
/// Test helper for constructing minimal nuspec XML payloads and packaging them as
/// .nupkg ZIP archives. Mirrors the shape <c>dotnet pack</c> emits — root nuspec
/// entry plus dependency groups — without the full package contents.
/// </summary>
internal static class NuspecBuilder
{
    private static readonly XNamespace NuspecNs = "http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd";

    public static byte[] WithCrossFamilyDependency(
        string packageId,
        string version,
        string dependencyId,
        string currentRange)
    {
        var nuspec = new XDocument(
            new XDeclaration("1.0", "utf-8", null),
            new XElement(
                NuspecNs + "package",
                new XElement(
                    NuspecNs + "metadata",
                    new XElement(NuspecNs + "id", packageId),
                    new XElement(NuspecNs + "version", version),
                    new XElement(NuspecNs + "authors", "TestAuthor"),
                    new XElement(NuspecNs + "description", "test"),
                    new XElement(
                        NuspecNs + "dependencies",
                        new XElement(
                            NuspecNs + "group",
                            new XAttribute("targetFramework", "net10.0"),
                            new XElement(
                                NuspecNs + "dependency",
                                new XAttribute("id", dependencyId),
                                new XAttribute("version", currentRange)))))));

        return Encoding.UTF8.GetBytes(nuspec.Declaration + Environment.NewLine + nuspec.ToString(SaveOptions.DisableFormatting));
    }

    public static byte[] WithoutDependencies(string packageId, string version)
    {
        var nuspec = new XDocument(
            new XDeclaration("1.0", "utf-8", null),
            new XElement(
                NuspecNs + "package",
                new XElement(
                    NuspecNs + "metadata",
                    new XElement(NuspecNs + "id", packageId),
                    new XElement(NuspecNs + "version", version),
                    new XElement(NuspecNs + "authors", "TestAuthor"),
                    new XElement(NuspecNs + "description", "test"))));

        return Encoding.UTF8.GetBytes(nuspec.Declaration + Environment.NewLine + nuspec.ToString(SaveOptions.DisableFormatting));
    }

    public static byte[] AsNupkgZip(string nuspecEntryName, byte[] nuspecBytes)
    {
        using var memoryStream = new MemoryStream();
        using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = archive.CreateEntry(nuspecEntryName, CompressionLevel.Optimal);
            using var entryStream = entry.Open();
            entryStream.Write(nuspecBytes, 0, nuspecBytes.Length);
        }
        return memoryStream.ToArray();
    }
}
