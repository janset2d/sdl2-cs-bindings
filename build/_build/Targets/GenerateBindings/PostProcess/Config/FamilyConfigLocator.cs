using Build.Host.Paths;
using Cake.Core.IO;

namespace Build.Targets.GenerateBindings.PostProcess.Config;

/// <summary>
/// Resolves the unified family configuration from the host repository root.
/// </summary>
internal static class FamilyConfigLocator
{
    public static FilePath Resolve(IPathService paths)
    {
        return paths.RepoRoot.CombineWithFilePath("spikes/binding-generators/clangsharp/config/family-config.json");
    }
}
