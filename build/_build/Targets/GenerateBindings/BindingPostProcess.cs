using Build.Data.BindingGeneration.Models;
using Build.Targets.GenerateBindings.PostProcess;
using Build.Targets.GenerateBindings.PostProcess.Config;
using Cake.Core;
using Cake.Core.IO;

namespace Build.Targets.GenerateBindings;

internal static class BindingPostProcess
{
    public static IEnumerable<(string Mode, string Codegen, BindingGenerationConfig Family)> Invocations(IReadOnlyList<BindingGenerationConfig> families)
    {
        string[] modes =
            ["platform-delta", "strip-varargs", "libraryimport", "flags-detect", "guid-substitute", "clong-dispatch", "uniform-opaque", "struct-layout", "public-typed"];

        foreach (var mode in modes)
        {
            foreach (var codegen in GenerationPlan.Codegens)
            {
                if ((mode is "libraryimport" or "public-typed") && codegen != "Modern")
                {
                    continue;
                }

                foreach (var family in families)
                {
                    if (mode != "public-typed" || family.FamilyId == "sdl2-core")
                    {
                        yield return (mode, codegen, family);
                    }
                }
            }
        }
    }

    public static async Task ApplyAsync(ICakeContext context, FamilyConfig config, BindingGenerationConfig profile, DirectoryPath directory, string mode)
    {
        var files = new GeneratedSyntaxFiles(context);

        if (!files.DirectoryExists(directory))
        {
            return;
        }

        var family = UniformOpaqueFamilyIdentity.Resolve(directory.FullPath, profile.ManagedNamespace, config);

        switch (mode)
        {
            case "platform-delta":
                await new PlatformDeltaPostProcessor(config.PlatformViews()).ProcessAsync(files, directory, directory);
                break;
            case "strip-varargs":
                var strip = new StripVarargsRewriter();
                await files.RewriteAsync(directory, directory, strip, () => strip.AnyChanges, strip.Reset);
                break;
            case "libraryimport":
                var imports = new DllImportToLibraryImportRewriter();
                await files.RewriteAsync(directory, directory, imports, () => imports.AnyChanges, imports.Reset);
                break;
            case "flags-detect":
                var flags = new FlagsAttributeRewriter(config.FlagsAllowList(family));
                await files.RewriteAsync(directory, directory, flags, () => flags.AnyChanges, flags.Reset);
                break;
            case "guid-substitute":
                var guid = new GuidSubstitutionRewriter();
                await files.RewriteAsync(directory, directory, guid, () => guid.AnyChanges, guid.Reset);
                break;
            case "clong-dispatch":
                var clong = new ClongDualDispatchRewriter(ClongDualDispatchRewriter.DetectMode(directory.FullPath), config.ClongMethods(family));
                await files.RewriteAsync(directory, directory, clong, () => clong.AnyChanges, clong.Reset);
                break;
            case "uniform-opaque":
                await UniformOpaqueHandles.ApplyAsync(context, directory, directory, config, profile.OwnerMode, profile.ManagedNamespace);
                break;
            case "struct-layout":
                var layout = new StructLayoutSequentialRewriter();
                await files.RewriteAsync(directory, directory, layout, () => layout.AnyChanges, layout.Reset);
                break;
            case "public-typed":
                await PublicTypedMethodProjection.WriteAsync(files, directory, family, config);
                break;
            default:
                throw new InvalidOperationException($"Unknown postprocess mode '{mode}'.");
        }
    }
}
