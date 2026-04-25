using Build.Application.Publishing;
using Build.Context;
using Build.Domain.Publishing.Models;
using Cake.Core.Diagnostics;
using Cake.Core.IO;
using Cake.Frosting;

namespace Build.Tasks.Publishing;

/// <summary>
/// ADR-003 §3.2 public-feed promotion stage. Phase-2b stub — delegates to
/// <see cref="PublishTaskRunner"/> which throws <see cref="NotImplementedException"/>.
/// The CI <c>publish-public</c> job in <c>release.yml</c> is gated <c>if: false</c>
/// (and depends on <c>publish-staging</c>) until Phase 2b lands the real promotion
/// logic; this task exists so the <c>--target=PublishPublic</c> CLI surface compiles
/// and is discoverable today.
/// </summary>
[TaskName("PublishPublic")]
[TaskDescription("Stub — Phase 2b will promote staging-validated artifacts to the public feed (nuget.org).")]
public sealed class PublishPublicTask(PublishTaskRunner runner, ICakeLog log) : AsyncFrostingTask<BuildContext>
{
    private readonly PublishTaskRunner _runner = runner ?? throw new ArgumentNullException(nameof(runner));
    private readonly ICakeLog _log = log ?? throw new ArgumentNullException(nameof(log));

    public override Task RunAsync(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        _log.Warning("PublishPublic is a Phase-2b stub. Invocation will throw NotImplementedException — the real public-feed promoter is not yet wired.");

        var request = new PublishRequest(
            PackagesDir: new DirectoryPath("."),
            FeedUrl: string.Empty,
            AuthToken: string.Empty);

        return _runner.RunAsync(context, request);
    }
}
