using Build.Host.Cake;
using Build.Host.Paths;
using Cake.Core;
using NuGet.Versioning;

namespace Build.Features.Packaging;

internal static class VersionsFileWriter
{
    public static async Task WriteAsync(
        ICakeContext cakeContext,
        IPathService pathService,
        IReadOnlyDictionary<string, NuGetVersion> versions)
    {
        ArgumentNullException.ThrowIfNull(cakeContext);
        ArgumentNullException.ThrowIfNull(pathService);
        ArgumentNullException.ThrowIfNull(versions);

        var serializable = new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (family, version) in versions)
        {
            serializable[family] = version.ToNormalizedString();
        }

        var outputFile = pathService.GetResolveVersionsOutputFile();
        await cakeContext.WriteJsonAsync(outputFile, serializable);
    }
}
