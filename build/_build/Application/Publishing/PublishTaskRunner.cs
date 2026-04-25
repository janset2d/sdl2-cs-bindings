using System.Diagnostics.CodeAnalysis;
using Build.Context;
using Build.Domain.Publishing.Models;
using Cake.Core.Diagnostics;

namespace Build.Application.Publishing;

/// <summary>
/// ADR-003 §3.2 Publish stage runner. Phase-2b stub — body throws
/// <see cref="NotImplementedException"/> with a pointer to the planned implementation.
/// <para>
/// The Slice E P6 scaffolding lands the Cake-side surface so the
/// <c>publish-staging</c> / <c>publish-public</c> jobs in <c>release.yml</c> have
/// matching <c>--target=PublishStaging</c> / <c>--target=PublishPublic</c> entries
/// to dispatch against. The real feed transfer logic (NuGet push,
/// authentication, deduplication) arrives with Phase 2b's first prerelease
/// publication wave.
/// </para>
/// <para>
/// No interface is introduced at this stub stage per AGENTS.md interface discipline
/// (single implementation, single caller surface today). When Phase 2b wires real
/// transfer logic, an <c>IPublishTaskRunner</c> seam may be extracted if multiple
/// implementations or test seams emerge — until then a concrete class registered
/// directly in the composition root is the smaller change.
/// </para>
/// </summary>
public sealed class PublishTaskRunner(ICakeLog log)
{
    private readonly ICakeLog _log = log ?? throw new ArgumentNullException(nameof(log));

    [SuppressMessage(
        "Design",
        "MA0025:Implement the functionality",
        Justification = "Phase-2b stub: Slice E P6 lands the Cake-side scaffolding so target wiring + DI registration can be verified end-to-end while the real publish pipeline is not yet implemented. NotImplementedException is the semantically correct exception here — the operation IS planned, just not implemented in this pass — so NotSupportedException would mislead operators looking at the failure.")]
    public Task RunAsync(BuildContext context, PublishRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        _log.Error("PublishTaskRunner reached but Phase 2b real implementation has not landed. The 'publish-staging' / 'publish-public' release.yml jobs are gated `if: false` until then.");
        throw new NotImplementedException(
            "PublishTaskRunner is a Phase-2b stub. The release pipeline's publish stages " +
            "(staging → public feed promotion) are not implemented in this pass. The Cake " +
            "scaffolding lands in Slice E P6 so target wiring + DI registration can be " +
            "verified end-to-end; the real NuGet push / auth / deduplication logic arrives " +
            "with the first Phase 2b prerelease publication.");
    }
}
