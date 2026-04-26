using Build.Context;
using Build.Domain.Paths;
using Cake.Core;
using NuGet.Versioning;

namespace Build.Application.Packaging;

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
