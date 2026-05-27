namespace Janset.SDL2.PostProcess.Config;

/// <summary>
/// Resolves the unified family-config.json by walking directory ancestors.
/// Replaces the per-roster ancestor walks previously duplicated in Program.cs
/// (ResolveOpaqueHandleRosterPath) and FlagsEnumRosterLoader (ResolveRosterPath).
/// </summary>
internal static class FamilyConfigLocator
{
    private static readonly string[] RelativeSegments =
        ["spikes", "binding-generators", "clangsharp", "config", "family-config.json"];

    public static string Resolve(string? startDir = null)
    {
        var dir = new DirectoryInfo(Path.GetFullPath(startDir ?? Directory.GetCurrentDirectory()));
        while (dir is not null)
        {
            var candidate = Path.Combine(new[] { dir.FullName }.Concat(RelativeSegments).ToArray());
            if (File.Exists(candidate))
            {
                return candidate;
            }
            dir = dir.Parent;
        }

        throw new FileNotFoundException(
            "Could not locate family-config.json by walking ancestors of " +
            $"'{startDir ?? Directory.GetCurrentDirectory()}'. " +
            "Expected at <repo>/spikes/binding-generators/clangsharp/config/family-config.json.");
    }
}
