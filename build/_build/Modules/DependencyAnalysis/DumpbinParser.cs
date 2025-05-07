namespace Build.Modules.DependencyAnalysis;

public static class DumpbinParser
{
    public static IReadOnlyList<string> ExtractDependentDlls(IEnumerable<string> lines)
    {
        const string startMarker = "Image has the following dependencies:";
        const string endMarker = "Summary";
        const string dllSuffix = ".dll";

        return
        [
            .. lines
                .SkipWhile(line => !line.Contains(startMarker, StringComparison.OrdinalIgnoreCase))
                .Skip(1) // Skip the marker line itself
                .TakeWhile(line => !line.Contains(endMarker, StringComparison.OrdinalIgnoreCase))
                .Select(line => line.Trim())
                .Where(line => !string.IsNullOrEmpty(line) && line.EndsWith(dllSuffix, StringComparison.OrdinalIgnoreCase)),
        ];
    }
}