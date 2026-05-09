using Cake.Core.Diagnostics;
using Cake.Frosting;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;

namespace Build.Tests.Fixtures;

public sealed class TargetTestHostV2<TTask> where TTask : class, IFrostingTask
{
    private readonly FakeCakeWorldV2 _world;
    private readonly List<Action<IServiceCollection>> _registrations = [];
    private Build.Shared.Manifest.ManifestConfig? _manifest;

    public TargetTestHostV2(FakeCakeWorldV2 world)
    {
        _world = world ?? throw new ArgumentNullException(nameof(world));
    }

    public TargetTestHostV2<TTask> WithServices(Action<IServiceCollection> register)
    {
        ArgumentNullException.ThrowIfNull(register);
        _registrations.Add(register);
        return this;
    }

    public TargetTestHostV2<TTask> WithManifest(Build.Shared.Manifest.ManifestConfig manifest)
    {
        _manifest = manifest ?? throw new ArgumentNullException(nameof(manifest));
        return this;
    }

    public async Task<TargetRunResultV2> RunAsync()
    {
        var services = new ServiceCollection();

        // Cake primitives from the fake world
        services.AddSingleton(_world.CakeContext);
        services.AddSingleton<ICakeLog>(_world.Log);
        services.AddSingleton(_world.CakeContext.Environment);
        services.AddSingleton(_world.CakeContext.FileSystem);
        services.AddSingleton(_world.CakeContext.Globber);
        services.AddSingleton(_world.CakeContext.Arguments);
        services.AddSingleton(_world.CakeContext.Configuration);

        // V2 BuildContext + its sub-parts
        var buildContext = _world.CreateBuildContext(_manifest);
        services.AddSingleton(buildContext);
        services.AddSingleton(buildContext.Manifest);
        services.AddSingleton(buildContext.Runtime);
        services.AddSingleton(buildContext.Paths);
        // Configurations aggregate + DumpbinConfiguration retired in S14. Surviving sub-records
        // (P9-territory + composition-root consumers) are constructed inline here; tests that need
        // non-default values override via WithServices.
        services.AddSingleton(new Build.Host.Configuration.VcpkgConfiguration([], _world.Rid));
        services.AddSingleton(new Build.Host.Configuration.PackageBuildConfiguration(_world.FamilyVersions));
        services.AddSingleton(new Build.Host.Configuration.RepositoryConfiguration(_world.RepoRoot));
        services.AddSingleton(new Build.Host.Configuration.DotNetBuildConfiguration("Release"));

        // IAnsiConsole from the fake world (Spectre.Console.Testing.TestConsole)
        services.AddSingleton<IAnsiConsole>(_world.AnsiConsole);

        // Target-specific registrations (e.g. AddInfoFeature)
        foreach (var register in _registrations)
        {
            register(services);
        }

        using var provider = services.BuildServiceProvider();
        var task = ActivatorUtilities.CreateInstance<TTask>(provider);

        try
        {
            // IFrostingTask.RunAsync works for both sync FrostingTask<T> and
            // async AsyncFrostingTask<T>. BuildContext implements ICakeContext
            // through FrostingContext → CakeContext.
            await task.RunAsync(buildContext);
            return new TargetRunResultV2(true, null, _world.Log);
        }
#pragma warning disable CA1031
        // Test harness intentionally captures all exception types so scenario
        // tests can assert on failure modes without losing the log.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            return new TargetRunResultV2(false, ex, _world.Log);
        }
    }
}

public sealed record TargetRunResultV2(
    bool Success,
    Exception? Exception,
    TestLogV2 Log
);
