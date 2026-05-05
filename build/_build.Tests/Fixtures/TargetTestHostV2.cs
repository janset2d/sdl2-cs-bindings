#pragma warning disable CA1031

using System.Runtime.ExceptionServices;
using Build.Host;
using Cake.Frosting;
using Microsoft.Extensions.DependencyInjection;

namespace Build.Tests.Fixtures;

public sealed class TargetTestHostV2<TTask> where TTask : AsyncFrostingTask<BuildContext>
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
        services.AddSingleton<Cake.Core.Diagnostics.ICakeLog>(_world.Log);
        services.AddSingleton(_world.CakeContext.Environment);
        services.AddSingleton(_world.CakeContext.FileSystem);
        services.AddSingleton(_world.CakeContext.Globber);
        services.AddSingleton(_world.CakeContext.Arguments);
        services.AddSingleton(_world.CakeContext.Configuration);

        // Legacy compatibility: BuildContext + its sub-parts
        var buildContext = _world.ToLegacyBuildContext(_manifest);
        services.AddSingleton(buildContext);
        services.AddSingleton(buildContext.Manifest);
        services.AddSingleton(buildContext.Runtime);
        services.AddSingleton(buildContext.Paths);
        services.AddSingleton(buildContext.Options);
        services.AddSingleton(buildContext.Options.Vcpkg);
        services.AddSingleton(buildContext.Options.Package);
        services.AddSingleton(buildContext.Options.Repository);
        services.AddSingleton(buildContext.Options.DotNet);
        services.AddSingleton(buildContext.Options.Dumpbin);

        // The task class itself
        services.AddSingleton<TTask>();

        // Target-specific registrations (e.g. AddInfoFeature)
        foreach (var register in _registrations)
        {
            register(services);
        }

        using var provider = services.BuildServiceProvider();
        var task = provider.GetRequiredService<TTask>();

        try
        {
            await task.RunAsync(buildContext);
            return new TargetRunResultV2(true, null, _world.Log);
        }
        catch (Exception ex)
        {
            var captured = ExceptionDispatchInfo.Capture(ex);
            return new TargetRunResultV2(false, captured.SourceException, _world.Log);
        }
    }
}

public sealed record TargetRunResultV2(
    bool Success,
    Exception? Exception,
    TestLogV2 Log
);
