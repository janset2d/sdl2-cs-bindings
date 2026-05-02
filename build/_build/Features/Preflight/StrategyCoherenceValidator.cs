using System.Collections.Immutable;
using Build.Shared.Manifest;
using Build.Shared.Strategy;

namespace Build.Features.Preflight;

public sealed class StrategyCoherenceValidator(IStrategyResolver strategyResolver)
{
    private readonly IStrategyResolver _strategyResolver = strategyResolver ?? throw new ArgumentNullException(nameof(strategyResolver));

    public StrategyCoherenceResult Validate(IImmutableList<RuntimeInfo> runtimes)
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

        var validation = new StrategyCoherenceValidation(checks);

        return validation.HasErrors
            ? StrategyCoherenceResult.Fail(validation)
            : StrategyCoherenceResult.Pass(validation);
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
