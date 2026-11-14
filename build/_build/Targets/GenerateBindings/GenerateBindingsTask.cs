using Build.Data.BindingGeneration;
using Build.Data.BindingGeneration.Models;
using Build.Host;
using Build.Targets.GenerateBindings.Oracle;
using Cake.Common.Diagnostics;
using Cake.Core;
using Cake.Frosting;

namespace Build.Targets.GenerateBindings;

[TaskName("GenerateBindings")]
[TaskDescription("Writes non-mutating binding-generation evidence")]
public sealed class GenerateBindingsTask(IBindingGenerationConfigRepository configurations, IDynapiManifestRepository dynapiRepository) : AsyncFrostingTask<BuildContext>
{
    public override async Task RunAsync(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!context.GenerateBindingsEvidence)
        {
            throw new CakeException("GenerateBindings supports only --evidence at this revision");
        }

        var requested = context.ResolveVersionsScope.ToHashSet(StringComparer.Ordinal);
        var enabled = configurations.EnumerateEnabledFamilies();
        var profiles = new Dictionary<string, BindingGenerationConfig>(StringComparer.Ordinal);

        IEnumerable<string> selection = requested.Count == 0 ? enabled : requested;

        foreach (var family in selection)
        {
            var result = configurations.Load(family);

            if (result.TryGetError(out var error))
            {
                throw new CakeException(error.Reason);
            }

            profiles.Add(family, result.Value);
        }

        var selected = enabled.Where(profiles.ContainsKey).Select(family => profiles[family]).ToArray();
        var collector = new OracleEvidenceCollector(context, context.Paths, dynapiRepository);
        var report = await collector.BuildReportAsync(selected, context.CancellationToken);
        await collector.WriteReportAsync(report);

        foreach (var family in report.Families)
        {
            context.Information("Evidence family: {0}", family.Config.FamilyId);
            context.Information("Raw ABI findings: {0}; ownership warnings: {1}; required-surface findings: {2}",
                family.RawAbiChecks.Count, family.Ownership?.Count ?? 0, family.RequiredAudit?.Findings.Count ?? 0);
        }

        context.Information("Report: artifacts/binding-generation/oracle-evidence.md");
    }
}
