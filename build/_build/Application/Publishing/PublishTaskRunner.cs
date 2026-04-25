using System.Diagnostics.CodeAnalysis;
using Build.Context;
using Build.Domain.Publishing.Models;
using Cake.Core.Diagnostics;

namespace Build.Application.Publishing;

/// <summary>
/// Publish-stage runner.
/// <para>
/// The current implementation is an intentional stub: it logs a clear error and
/// throws <see cref="NotImplementedException"/> because feed-push logic is not wired yet.
/// </para>
/// <para>
/// Keeping the runner in place preserves the <c>PublishStaging</c> and
/// <c>PublishPublic</c> target surface so workflow and DI wiring can be exercised
/// without pretending that package publication already works.
/// </para>
/// </summary>
public sealed class PublishTaskRunner(ICakeLog log)
{
    private readonly ICakeLog _log = log ?? throw new ArgumentNullException(nameof(log));

    [SuppressMessage(
        "Design",
        "MA0025:Implement the functionality",
        Justification = "This runner is an intentional stub. NotImplementedException keeps the failure mode explicit until feed-push logic is added.")]
    public Task RunAsync(BuildContext context, PublishRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        _log.Error("PublishTaskRunner was invoked, but publish logic is not implemented yet. The workflow keeps publish jobs disabled until this runner gains a real feed-push implementation.");
        throw new NotImplementedException(
            "PublishTaskRunner is a stub. The release pipeline's publish stages are " +
            "present for wiring and target discovery, but the actual NuGet push, " +
            "authentication, and deduplication logic has not been implemented yet.");
    }
}
