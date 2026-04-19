using Build.Application.Packaging;
using Build.Context;
using Build.Tasks.Harvest;
using Cake.Core.Diagnostics;
using Cake.Frosting;

namespace Build.Tasks.Packaging;

[TaskName("SetupLocalDev")]
[TaskDescription("Prepares local package feed and writes build/msbuild/Janset.Smoke.local.props for IDE-ready smoke restore/build")]
[IsDependentOn(typeof(ConsolidateHarvestTask))]
public sealed class SetupLocalDevTask(
    IArtifactSourceResolver artifactSourceResolver,
    ICakeLog log) : AsyncFrostingTask<BuildContext>
{
    private readonly IArtifactSourceResolver _artifactSourceResolver = artifactSourceResolver ?? throw new ArgumentNullException(nameof(artifactSourceResolver));
    private readonly ICakeLog _log = log ?? throw new ArgumentNullException(nameof(log));

    public override async Task RunAsync(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        _log.Information("SetupLocalDev started with source profile '{0}'.", _artifactSourceResolver.Profile);

        await _artifactSourceResolver.PrepareFeedAsync(context);
        await _artifactSourceResolver.WriteConsumerOverrideAsync(context);

        _log.Information("SetupLocalDev completed. Smoke local override and package feed are ready.");
    }
}
