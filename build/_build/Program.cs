using System.CommandLine;
using Build;
using Build.Host;
using Build.Host.Cli;
using Cake.Frosting;

return await CakeHostCommandLine
    .CreateRootCommand(RunCakeHostAsync)
    .Parse(args)
    .InvokeAsync();

static async Task<int> RunCakeHostAsync(ParseResult parseResult, CancellationToken cancellationToken)
{
    var parsedArgs = CakeHostCommandLine.BindParsedArguments(parseResult);
    var repoRootPath = await EntryPoint.DetermineRepoRootAsync(parsedArgs.RepoRoot);
    var initialCakeArgs = parseResult.Tokens.Select(token => token.Value).ToArray();
    var effectiveCakeArgs = EntryPoint.GetEffectiveCakeArguments(initialCakeArgs, repoRootPath, parseResult);

    return await Task.Run(() => new CakeHost()
        .UseContext<BuildContext>()
        .ConfigureServices(services => EntryPoint.ConfigureHostServices(services, parsedArgs, repoRootPath, cancellationToken))
        .Run(effectiveCakeArgs), cancellationToken);
}
