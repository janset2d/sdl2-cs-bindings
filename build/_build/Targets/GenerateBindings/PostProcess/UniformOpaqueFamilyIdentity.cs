using Janset.SDL2.PostProcess.Config;

namespace Janset.SDL2.PostProcess;

internal static class UniformOpaqueFamilyIdentity
{
    public static string Resolve(string outputDir, string handlesNamespace, FamilyConfig config)
    {
        var normalized = Path.GetFullPath(outputDir).Replace('\\', '/');
        foreach (var (projectDir, family) in config.ProjectDirToFamily())
        {
            if (normalized.Contains($"/{projectDir}/", StringComparison.OrdinalIgnoreCase))
            {
                return family;
            }
        }

        if (config.NamespaceToFamily().TryGetValue(handlesNamespace, out var byNamespace))
        {
            return byNamespace;
        }

        throw new InvalidOperationException(
            $"Could not resolve family from output directory: {outputDir} or namespace '{handlesNamespace}'. " +
            "Expected a config project_dir path segment or a configured handles namespace.");
    }
}
