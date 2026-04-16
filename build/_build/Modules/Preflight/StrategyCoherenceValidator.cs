using System.Collections.Immutable;
using Build.Context.Models;
using Build.Modules.Contracts;
using Build.Modules.Preflight.Models;
using Build.Modules.Strategy.Results;

namespace Build.Modules.Preflight;

public sealed class StrategyCoherenceValidator(IStrategyResolver strategyResolver) : IStrategyCoherenceValidator
{
    private readonly IStrategyResolver _strategyResolver = strategyResolver ?? throw new ArgumentNullException(nameof(strategyResolver));

    public StrategyCoherenceValidation Validate(IImmutableList<RuntimeInfo> runtimes)
    {
        ArgumentNullException.ThrowIfNull(runtimes);

        if (runtimes.Count == 0)
        {
            throw new InvalidOperationException(
                "manifest.json requires a non-empty runtimes section for strategy coherence validation.");
        }

        var checks = new List<RuntimeStrategyCheck>(runtimes.Count);

        foreach (var runtime in runtimes)
        {
            var resolution = _strategyResolver.Resolve(runtime);
            checks.Add(ToRuntimeStrategyCheck(runtime, resolution));
        }

        return new StrategyCoherenceValidation(checks);
    }

    private static RuntimeStrategyCheck ToRuntimeStrategyCheck(RuntimeInfo runtime, StrategyResolutionResult resolution)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(resolution);

        return resolution.IsSuccess()
            ? new RuntimeStrategyCheck(runtime.Rid, runtime.Triplet, runtime.Strategy, IsValid: true, resolution.ResolvedModel.ToString(), ErrorMessage: null)
            : new RuntimeStrategyCheck(runtime.Rid, runtime.Triplet, runtime.Strategy, IsValid: false, ResolvedModel: null, ErrorMessage: resolution.ResolutionError.Message);
    }
}
