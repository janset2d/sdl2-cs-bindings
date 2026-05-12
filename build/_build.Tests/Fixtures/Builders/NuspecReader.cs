using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using Cake.Core.IO;

namespace Build.Tests.Fixtures.Builders;

internal static class NuspecReader
{
    public static string? GetDependencyVersion(FakeCakeWorld world, FilePath nupkgPath, string dependencyId)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(nupkgPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(dependencyId);

        using var stream = world.FileSystem.GetFile(nupkgPath).Open(FileMode.Open, FileAccess.Read, FileShare.Read);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false);

        var nuspecEntry = archive.Entries.SingleOrDefault(entry =>
            entry.FullName.EndsWith(".nuspec", StringComparison.OrdinalIgnoreCase) &&
            !entry.FullName.StartsWith("package/", StringComparison.OrdinalIgnoreCase));

        if (nuspecEntry is null)
        {
            return null;
        }

        using var reader = new StreamReader(nuspecEntry.Open(), Encoding.UTF8);
        var content = reader.ReadToEnd();
        var document = XDocument.Parse(content);
        var ns = document.Root!.Name.Namespace;

        var dependency = document
            .Descendants(ns + "dependency")
            .SingleOrDefault(element =>
                string.Equals((string?)element.Attribute("id"), dependencyId, StringComparison.OrdinalIgnoreCase));

        return (string?)dependency?.Attribute("version");
    }
}
