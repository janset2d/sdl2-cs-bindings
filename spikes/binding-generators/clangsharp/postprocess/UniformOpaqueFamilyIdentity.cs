namespace Janset.SDL2.PostProcess;

internal static class UniformOpaqueFamilyIdentity
{
    public static string Resolve(string outputDir, string handlesNamespace)
    {
        var normalized = Path.GetFullPath(outputDir).Replace('\\', '/');
        var familyFromPath = ResolveFromOutputPath(normalized);
        if (familyFromPath is not null)
        {
            return familyFromPath;
        }

        var familyFromNamespace = ResolveFromNamespace(handlesNamespace);
        if (familyFromNamespace is not null)
        {
            return familyFromNamespace;
        }

        throw new InvalidOperationException(
            $"Could not resolve family from output directory: {outputDir}. " +
            "Expected path segment /Janset.SDL2.{Core,Image,Ttf,Mixer,Gfx}/ or --handles-namespace SDL2[.Image|.Ttf|.Mixer|.Gfx].");
    }

    private static string? ResolveFromOutputPath(string normalizedPath)
    {
        if (normalizedPath.Contains("/Janset.SDL2.Core/", StringComparison.OrdinalIgnoreCase)) return "core";
        if (normalizedPath.Contains("/Janset.SDL2.Image/", StringComparison.OrdinalIgnoreCase)) return "image";
        if (normalizedPath.Contains("/Janset.SDL2.Ttf/", StringComparison.OrdinalIgnoreCase)) return "ttf";
        if (normalizedPath.Contains("/Janset.SDL2.Mixer/", StringComparison.OrdinalIgnoreCase)) return "mixer";
        if (normalizedPath.Contains("/Janset.SDL2.Gfx/", StringComparison.OrdinalIgnoreCase)) return "gfx";
        return null;
    }

    private static string? ResolveFromNamespace(string handlesNamespace)
    {
        return handlesNamespace switch
        {
            "SDL2" => "core",
            "SDL2.Image" => "image",
            "SDL2.Ttf" => "ttf",
            "SDL2.Mixer" => "mixer",
            "SDL2.Gfx" => "gfx",
            _ => null,
        };
    }
}
