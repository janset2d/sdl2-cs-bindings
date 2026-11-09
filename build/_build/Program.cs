using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.NamingConventionBinder;
using System.Runtime.InteropServices;
using Build;
using Build.Host;
using Build.Host.Cli.Options;
using Cake.Frosting;
using Microsoft.Extensions.DependencyInjection;

var root = new RootCommand("Cake build for janset2d/sdl2-cs-bindings");

root.AddOption(CakeOptions.DescriptionOption);
root.AddOption(CakeOptions.DryRunOption);
root.AddOption(CakeOptions.ExclusiveOption);
root.AddOption(CakeOptions.HelpOption);
root.AddOption(CakeOptions.InfoOption);
root.AddOption(CakeOptions.TargetOption);
root.AddOption(CakeOptions.TreeOption);
root.AddOption(CakeOptions.VerbosityOption);
root.AddOption(CakeOptions.VersionOption);
root.AddOption(CakeOptions.WorkingPathOption);

root.AddOption(RepositoryOptions.RepoRootOption);

root.AddOption(DotNetOptions.ConfigOption);

root.AddOption(VcpkgOptions.VcpkgDirOption);
root.AddOption(VcpkgOptions.VcpkgInstalledDirOption);
root.AddOption(VcpkgOptions.LibraryOption);
root.AddOption(VcpkgOptions.RidOption);

root.AddOption(ResolveVersionsFromManifestOptions.VersionSuffixOption);
root.AddOption(ResolveVersionsFromManifestOptions.VersionScopeOption);

root.AddOption(ResolveVersionsFromExplicitOptions.ExplicitVersionOption);
root.AddOption(ResolveVersionsFromExplicitOptions.ExplicitVersionsOption);

root.AddOption(StageVersionsOptions.VersionsFileOption);

root.AddOption(DumpbinOptions.DllOption);

root.Handler = CommandHandler.Create<InvocationContext, ParsedArguments>(RunCakeHostAsync);
return await root.InvokeAsync(args);

static async Task<int> RunCakeHostAsync(InvocationContext context, ParsedArguments parsedArgs)
{
    var repoRootPath = await EntryPoint.DetermineRepoRootAsync(parsedArgs.RepoRoot);
    var initialCakeArgs = context.ParseResult.Tokens.Select(t => t.Value).ToArray();
    var effectiveCakeArgs = EntryPoint.GetEffectiveCakeArguments(initialCakeArgs, repoRootPath, context);

    using var cts = new CancellationTokenSource();

    // A second Ctrl+C falls through to the OS handler.
    void OnCancelKeyPress(object? sender, ConsoleCancelEventArgs e)
    {
        if (cts.IsCancellationRequested)
        {
            return;
        }

        e.Cancel = true;
        cts.Cancel();
    }

    Console.CancelKeyPress += OnCancelKeyPress;

    using var sigTermRegistration = PosixSignalRegistration.Create(PosixSignal.SIGTERM, _ => cts.Cancel());

    try
    {
        return new CakeHost()
            .UseContext<BuildContext>()
            .ConfigureServices(services =>
            {
                EntryPoint.ConfigureBuildServices(services, parsedArgs, repoRootPath);
                // The generic overload cannot register a value type.
                services.AddSingleton(typeof(CancellationToken), cts.Token);
            })
            .Run(effectiveCakeArgs);
    }
    finally
    {
        Console.CancelKeyPress -= OnCancelKeyPress;
    }
}
