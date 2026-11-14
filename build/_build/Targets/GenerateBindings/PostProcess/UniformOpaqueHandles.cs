using Build.Targets.GenerateBindings.PostProcess.Config;
using Cake.Core;
using Cake.Core.IO;

namespace Build.Targets.GenerateBindings.PostProcess;

internal static class UniformOpaqueHandles
{
    public static async Task ApplyAsync(
        ICakeContext context,
        DirectoryPath inputDirectory,
        DirectoryPath outputDirectory,
        FamilyConfig config,
        bool? ownerMode,
        string handlesNamespace)
    {
        var files = new GeneratedSyntaxFiles(context);
        var family = UniformOpaqueFamilyIdentity.Resolve(outputDirectory.FullPath, handlesNamespace, config);
        var (autoDetect, forceOpaque) = config.OpaqueHandles(family, includeCoreHandles: true);
        var (ownedAutoDetect, ownedForceOpaque) = config.OpaqueHandles(family, includeCoreHandles: false);
        var handleNames = new HashSet<string>(autoDetect, StringComparer.Ordinal);
        handleNames.UnionWith(forceOpaque);
        var discovered = await OpaqueHandleEmitRewriter.DiscoverAutoDetectedHandlesAsync(files, inputDirectory);
        OpaqueHandleEmitRewriter.ReportDrift(discovered, ownedAutoDetect, handleNames, family, context.Log);
        var rewriter = new OpaqueHandleEmitRewriter(handleNames);
        await files.RewriteAsync(inputDirectory, outputDirectory, rewriter, () => rewriter.AnyChanges, rewriter.Reset);
        ownedAutoDetect.UnionWith(ownedForceOpaque);
        await UniformOpaqueOwnerMode.EmitConsolidatedHandlesFileIfOwnerAsync(
            files, outputDirectory, ownedAutoDetect, ownerMode ?? config.OwnerMode(family), handlesNamespace);
    }
}
