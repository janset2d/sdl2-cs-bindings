using Build.Data.Manifest.Models;
using Build.Host;
using Cake.Frosting;
using Microsoft.Extensions.DependencyInjection;

namespace Build.Tests.Fixtures;

public sealed class TargetTestHost<TTask> where TTask : class, IFrostingTask
{
    private readonly FakeCakeWorld _world;
    private readonly List<Action<IServiceCollection>> _registrations = [];
    private ManifestConfig? _manifest;

    public TargetTestHost(FakeCakeWorld world)
    {
        _world = world ?? throw new ArgumentNullException(nameof(world));
    }

    public TargetTestHost<TTask> WithServices(Action<IServiceCollection> register)
    {
        ArgumentNullException.ThrowIfNull(register);
        _registrations.Add(register);
        return this;
    }

    public TargetTestHost<TTask> WithManifest(ManifestConfig manifest)
    {
        _manifest = manifest ?? throw new ArgumentNullException(nameof(manifest));
        return this;
    }

    public async Task<TargetRunResult> RunAsync()
    {
        if (_manifest is not null)
        {
            _world.WithManifestObject(_manifest);
        }

        var services = new ServiceCollection()
            .AddFakeCakeWorld(_world);

        // Target-specific registrations supplied by the test.
        foreach (var register in _registrations)
        {
            register(services);
        }

        using var provider = services.BuildServiceProvider();
        var buildContext = provider.GetRequiredService<BuildContext>();
        var task = ActivatorUtilities.CreateInstance<TTask>(provider);

        try
        {
            // IFrostingTask.RunAsync works for both sync FrostingTask<T> and
            // async AsyncFrostingTask<T>. BuildContext implements ICakeContext
            // through FrostingContext → CakeContext.
            await task.RunAsync(buildContext);
            return new TargetRunResult(true, null, _world.Log);
        }
#pragma warning disable CA1031
        // Test harness intentionally captures all exception types so scenario
        // tests can assert on failure modes without losing the log.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            return new TargetRunResult(false, ex, _world.Log);
        }
    }
}

public sealed record TargetRunResult(
    bool Success,
    Exception? Exception,
    TestLog Log
);
