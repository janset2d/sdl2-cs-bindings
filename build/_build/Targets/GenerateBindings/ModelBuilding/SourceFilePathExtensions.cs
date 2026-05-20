using Cake.Core.IO;

namespace Build.Targets.GenerateBindings.ModelBuilding;

internal static class SourceFilePathExtensions
{
    public static string ToSourceHeaderName(this string? sourceFile) =>
        string.IsNullOrWhiteSpace(sourceFile)
            ? string.Empty
            : new FilePath(sourceFile).GetFilename().FullPath;
}
