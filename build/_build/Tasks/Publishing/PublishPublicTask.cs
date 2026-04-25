using Build.Application.Publishing;
using Build.Context;
using Build.Domain.Publishing.Models;
using Cake.Core.Diagnostics;
using Cake.Core.IO;
using Cake.Frosting;

namespace Build.Tasks.Publishing;

/// <summary>
/// Public-feed promotion task.
/// Delegates to <see cref="PublishTaskRunner"/>, which currently logs and throws
/// <see cref="NotImplementedException"/> until promotion logic is implemented.
/// </summary>
[TaskName("PublishPublic")]
[TaskDescription("Stub — will promote staging-validated artifacts to the public feed when publish logic is implemented.")]
public sealed class PublishPublicTask(PublishTaskRunner runner, ICakeLog log) : AsyncFrostingTask<BuildContext>
{
    private readonly PublishTaskRunner _runner = runner ?? throw new ArgumentNullException(nameof(runner));
    private readonly ICakeLog _log = log ?? throw new ArgumentNullException(nameof(log));

    public override Task RunAsync(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        _log.Warning("PublishPublic is a stub. Invocation will throw NotImplementedException until a real public-feed promoter is implemented.");

        var request = new PublishRequest(
            PackagesDir: new DirectoryPath("."),
            FeedUrl: string.Empty,
            AuthToken: string.Empty);

        return _runner.RunAsync(context, request);
    }
}
