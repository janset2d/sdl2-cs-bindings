using Build.Application.Publishing;
using Build.Context;
using Build.Domain.Publishing.Models;
using Cake.Core.Diagnostics;
using Cake.Core.IO;
using Cake.Frosting;

namespace Build.Tasks.Publishing;

/// <summary>
/// ADR-003 §3.2 staging publish stage. Phase-2b stub — delegates to
/// <see cref="PublishTaskRunner"/> which throws <see cref="NotImplementedException"/>.
/// The CI <c>publish-staging</c> job in <c>release.yml</c> is gated <c>if: false</c>
/// until Phase 2b lands the real feed transfer logic; this task exists so the
/// <c>--target=PublishStaging</c> CLI surface compiles and is discoverable today.
/// </summary>
[TaskName("PublishStaging")]
[TaskDescription("Stub — Phase 2b will push the packed nupkg set to the staging feed (GitHub Packages).")]
public sealed class PublishStagingTask(PublishTaskRunner runner, ICakeLog log) : AsyncFrostingTask<BuildContext>
{
    private readonly PublishTaskRunner _runner = runner ?? throw new ArgumentNullException(nameof(runner));
    private readonly ICakeLog _log = log ?? throw new ArgumentNullException(nameof(log));

    public override Task RunAsync(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        _log.Warning("PublishStaging is a Phase-2b stub. Invocation will throw NotImplementedException — the real staging publisher is not yet wired.");

        var request = new PublishRequest(
            PackagesDir: new DirectoryPath("."),
            FeedUrl: string.Empty,
            AuthToken: string.Empty);

        return _runner.RunAsync(context, request);
    }
}
