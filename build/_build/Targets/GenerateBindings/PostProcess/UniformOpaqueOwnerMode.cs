using Cake.Core.IO;

namespace Build.Targets.GenerateBindings.PostProcess;

internal static class UniformOpaqueOwnerMode
{
    public static async Task EmitConsolidatedHandlesFileIfOwnerAsync(
        GeneratedSyntaxFiles files,
        DirectoryPath outputDirectory,
        HashSet<string> handleNames,
        bool isOwner,
        string namespaceName)
    {
        if (!isOwner)
        {
            return;
        }

        var sortedNames = handleNames.OrderBy(s => s, StringComparer.Ordinal).ToList();
        var content = OpaqueHandleEmitRewriter.BuildHandlesFileContent(sortedNames, namespaceName);
        await files.WriteAllTextLfAsync(outputDirectory.CombineWithFilePath("Handles.g.cs"), content);
    }
}
