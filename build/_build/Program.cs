using Build;
using Build.Host;
using Build.Host.Cli;
using Cake.Frosting;
using Microsoft.Extensions.DependencyInjection;

return await CakeHostCommandLine
    .CreateRootCommand((parseResult, cancellationToken) => EntryPoint.RunCakeHostAsync(parseResult, RunCake, cancellationToken))
    .Parse(args)
    .InvokeAsync();

static int RunCake(string[] args, Action<IServiceCollection> configureServices)
{
    return new CakeHost()
        .UseContext<BuildContext>()
        .ConfigureServices(configureServices)
        .Run(args);
}
