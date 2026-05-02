using System.Xml.Linq;
using Build.Features.Preflight;
using Build.Host.Cake;
using Build.Host.Paths;
using Cake.Common.IO;
using Cake.Core;
using Cake.Core.IO;
using NuGet.Versioning;

namespace Build.Features.Packaging;

internal static class JansetLocalPropsWriter
{
    public static async Task WriteAsync(
        ICakeContext cakeContext,
        IPathService pathService,
        DirectoryPath localFeedPath,
        IReadOnlyDictionary<string, NuGetVersion> versions)
    {
        ArgumentNullException.ThrowIfNull(cakeContext);
        ArgumentNullException.ThrowIfNull(pathService);
        ArgumentNullException.ThrowIfNull(localFeedPath);
        ArgumentNullException.ThrowIfNull(versions);

        var propsFile = pathService.GetLocalPropsFile();
        cakeContext.EnsureDirectoryExists(propsFile.GetDirectory());

        var xml = BuildContent(localFeedPath, versions);
        await cakeContext.WriteAllTextAsync(propsFile, xml);
    }

    public static string BuildContent(
        DirectoryPath localFeedPath,
        IReadOnlyDictionary<string, NuGetVersion> familyVersions)
    {
        ArgumentNullException.ThrowIfNull(localFeedPath);
        ArgumentNullException.ThrowIfNull(familyVersions);

        var propertyGroup = new XElement("PropertyGroup",
            new XElement("LocalPackageFeed", localFeedPath.FullPath));

        foreach (var pair in familyVersions.OrderBy(p => p.Key, StringComparer.OrdinalIgnoreCase))
        {
            var propertyName = FamilyIdentifierConventions.VersionPropertyName(pair.Key);
            propertyGroup.Add(new XElement(propertyName, pair.Value.ToNormalizedString()));
        }

        var document = new XDocument(
            new XDeclaration("1.0", "utf-8", null),
            new XElement("Project", propertyGroup));

        return string.Concat(document.Declaration?.ToString(), "\n", document.ToString());
    }
}
