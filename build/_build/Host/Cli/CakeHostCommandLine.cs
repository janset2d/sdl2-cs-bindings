using System.CommandLine;
using System.CommandLine.Help;
using Build.Host.Cli.Options;

namespace Build.Host.Cli;

internal static class CakeHostCommandLine
{
    public static RootCommand CreateRootCommand(Func<ParseResult, CancellationToken, Task<int>> action)
    {
        var root = new RootCommand("Cake build for janset2d/sdl2-cs-bindings");
        var helpOption = root.Options.OfType<HelpOption>().Single();
        var versionOption = root.Options.OfType<VersionOption>().Single();
        root.Options.Remove(helpOption);
        root.Options.Remove(versionOption);

        root.Options.Add(CakeOptions.DescriptionOption);
        root.Options.Add(CakeOptions.DryRunOption);
        root.Options.Add(CakeOptions.ExclusiveOption);
        root.Options.Add(CakeOptions.HelpOption);
        root.Options.Add(CakeOptions.InfoOption);
        root.Options.Add(CakeOptions.TargetOption);
        root.Options.Add(CakeOptions.TreeOption);
        root.Options.Add(CakeOptions.VerbosityOption);
        root.Options.Add(CakeOptions.VersionOption);
        root.Options.Add(CakeOptions.WorkingPathOption);
        root.Options.Add(RepositoryOptions.RepoRootOption);
        root.Options.Add(DotNetOptions.ConfigOption);
        root.Options.Add(VcpkgOptions.VcpkgDirOption);
        root.Options.Add(VcpkgOptions.VcpkgInstalledDirOption);
        root.Options.Add(VcpkgOptions.LibraryOption);
        root.Options.Add(VcpkgOptions.RidOption);
        root.Options.Add(ResolveVersionsFromManifestOptions.VersionSuffixOption);
        root.Options.Add(ResolveVersionsFromManifestOptions.VersionScopeOption);
        root.Options.Add(ResolveVersionsFromExplicitOptions.ExplicitVersionOption);
        root.Options.Add(ResolveVersionsFromExplicitOptions.ExplicitVersionsOption);
        root.Options.Add(StageVersionsOptions.VersionsFileOption);
        root.Options.Add(DumpbinOptions.DllOption);
        root.Options.Add(GenerateBindingsOptions.EvidenceOption);
        root.Options.Add(versionOption);
        root.Options.Add(helpOption);
        root.SetAction(action);
        return root;
    }

    public static ParsedArguments BindParsedArguments(ParseResult parseResult) => new(
        RepoRoot: parseResult.GetValue(RepositoryOptions.RepoRootOption),
        Config: parseResult.GetValue(DotNetOptions.ConfigOption)!,
        VcpkgDir: parseResult.GetValue(VcpkgOptions.VcpkgDirOption),
        VcpkgInstalledDir: parseResult.GetValue(VcpkgOptions.VcpkgInstalledDirOption),
        Library: parseResult.GetValue(VcpkgOptions.LibraryOption)!,
        Rid: parseResult.GetValue(VcpkgOptions.RidOption),
        Dll: parseResult.GetValue(DumpbinOptions.DllOption)!,
        Suffix: parseResult.GetValue(ResolveVersionsFromManifestOptions.VersionSuffixOption),
        Scope: parseResult.GetValue(ResolveVersionsFromManifestOptions.VersionScopeOption)!,
        ExplicitVersion: parseResult.GetValue(ResolveVersionsFromExplicitOptions.ExplicitVersionOption)!,
        ExplicitVersions: parseResult.GetValue(ResolveVersionsFromExplicitOptions.ExplicitVersionsOption),
        VersionsFile: parseResult.GetValue(StageVersionsOptions.VersionsFileOption),
        Evidence: parseResult.GetValue(GenerateBindingsOptions.EvidenceOption));
}
