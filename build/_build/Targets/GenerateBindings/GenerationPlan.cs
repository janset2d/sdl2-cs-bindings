using Build.Data.BindingGeneration.Models;
using Build.Host;
using Build.Tools.ClangSharp;
using Cake.Common.IO;
using Cake.Core;
using Cake.Core.IO;

namespace Build.Targets.GenerateBindings;

internal sealed class GenerationPlan(BuildContext context, BindingGenerationSettings settings, IReadOnlyList<BindingGenerationConfig> families)
{
    public static IReadOnlyList<string> Codegens { get; } = ["Compat", "Modern"];
    public IReadOnlyList<BindingGenerationConfig> Families { get; } = families;
    public BindingGenerationSettings Settings { get; } = settings;
    public DirectoryPath ShimRoot { get; } = context.Paths.RepoRoot.Combine(settings.PlatformHeaderShimsDir);

    public static DirectoryPath StagedRoot(DirectoryPath staging, BindingGenerationConfig family)
        => staging.Combine(new FilePath(family.GenerationProject).GetDirectory().GetDirectoryName()).Combine("Generated");

    public static string[] Headers(BindingGenerationConfig family)
        => [.. (family.HeaderSet ?? throw new CakeException($"Family '{family.FamilyId}' has no header_set.")).Headers
            .OrderBy(header => header.Order)
            .Select(header => header.Name)];

    public DirectoryPath IncludeDirectory(BindingGenerationConfig family)
        => context.Paths.GetVcpkgInstalledTripletDir(context.Runtime.Triplet).Combine(family.HeaderSet!.IncludeDir);

    public ClangSharpSettings Invocation(BindingGenerationConfig family, string codegen, string header, DirectoryPath generatedRoot,
        PlatformViewConfig? view = null, IReadOnlyList<string>? neutralSymbols = null)
    {
        var sensitive = family.PlatformSensitiveHeaders.Contains(header, StringComparer.Ordinal);
        var includes = new List<DirectoryPath> { IncludeDirectory(family) };

        if (context.GenerateBindingsPlatformHeaderShims && (sensitive || view is not null))
        {
            includes.Add(ShimRoot);
        }

        var defined = (view?.Defines ?? []).Select(value => value.Split('=', 2)[0]).ToHashSet(StringComparer.Ordinal);
        IReadOnlyList<string> undefines = sensitive ? Settings.AllPlatformMacros : [];

        if (view is not null)
        {
            undefines = [.. Settings.AllPlatformMacros.Where(macro => !defined.Contains(macro))];
        }

        var output = generatedRoot.Combine(codegen);

        if (view is not null)
        {
            output = output.Combine("Platforms").Combine(view.Name);
        }

        return new ClangSharpSettings
        {
            WorkingDirectory = context.Paths.RepoRoot,
            Config = [codegen == "Compat" ? "compatible-codegen" : "latest-codegen", "windows-types", "generate-macro-bindings"],
            ResponseFiles = ResponseFiles(family, header),
            MethodClassName = family.RawClassName,
            LibraryPath = family.LibraryName,
            Namespace = family.ManagedNamespace,
            AccessSpecifier = family.RawClassName + "=Internal",
            IncludeDirectories = includes,
            File = IncludeDirectory(family).CombineWithFilePath(header),
            Output = output.CombineWithFilePath(new FilePath(header).GetFilenameWithoutExtension().FullPath + ".g.cs"),
            Defines = view?.Defines ?? [],
            Undefines = undefines,
            Exclusions = neutralSymbols ?? [],
        };
    }

    public IReadOnlyList<FilePath> ResponseFiles(BindingGenerationConfig family, string header)
    {
        if (header.Contains('/', StringComparison.Ordinal) || header.Contains('\\', StringComparison.Ordinal))
        {
            throw new CakeException($"Header must be a bare basename: '{header}'.");
        }

        var responseFiles = new List<FilePath>
        {
            context.Paths.RepoRoot.CombineWithFilePath(Settings.BaseRsp),
            context.Paths.RepoRoot.CombineWithFilePath(family.Rsp),
        };
        var perHeader = context.Paths.RepoRoot
            .Combine(Settings.PerHeaderRspDir)
            .CombineWithFilePath(new FilePath(header).GetFilenameWithoutExtension().FullPath + ".rsp");

        if (context.FileExists(perHeader))
        {
            responseFiles.Add(perHeader);
        }

        return responseFiles;
    }

    public IEnumerable<string> Describe()
    {
        yield return $"Generation plan: RID {context.RuntimeIdentifier}; triplet {context.Runtime.Triplet}; shims {context.GenerateBindingsPlatformHeaderShims}";

        foreach (var family in Families)
        {
            yield return $"Plan family: {family.FamilyId}; headers: {string.Join(", ", Headers(family))}";
            yield return $"Include directory: {IncludeDirectory(family).FullPath}; output: {family.GeneratedRoot}";

            if (family.PlatformSensitiveHeaders.Count > 0)
            {
                yield return $"Views: {string.Join(", ", Settings.PlatformViews.Select(view => view.Name))}; exclusions await raw neutral output";
            }
        }

        foreach (var codegen in Codegens)
        {
            foreach (var family in Families)
            {
                foreach (var header in Headers(family))
                {
                    yield return "dotnet " + ClangSharpTool.RenderArguments(Invocation(family, codegen, header, context.Paths.RepoRoot.Combine(family.GeneratedRoot))).Render();
                }
            }
        }
    }
}
