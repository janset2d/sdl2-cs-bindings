using Build.Shared.Manifest;

namespace Build.Shared.Strategy;

/// <summary>
/// Resolves and validates the packaging strategy for a given runtime entry.
/// <para>
/// The <see cref="RuntimeInfo.Strategy"/> field in manifest.json is the authority.
/// The triplet name is used as a coherence check — if the triplet contains <c>-hybrid</c>,
/// the strategy must be <c>hybrid-static</c>; if it contains <c>-dynamic</c>, the strategy
/// must be <c>pure-dynamic</c>. Triplets matching neither known pattern are rejected.
/// </para>
/// </summary>
public sealed class StrategyResolver : IStrategyResolver
{
    /// <summary>
    /// Resolves the packaging model from a runtime entry, validating strategy↔triplet coherence.
    /// </summary>
    /// <param name="runtime">The runtime entry from manifest.json.</param>
    /// <returns>The resolved <see cref="PackagingModel"/>.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the strategy field is missing, unknown, or inconsistent with the triplet name.
    /// </exception>
    public StrategyResolutionResult Resolve(RuntimeInfo runtime)
    {
        ArgumentNullException.ThrowIfNull(runtime);

        if (string.IsNullOrEmpty(runtime.Strategy))
        {
            return new StrategyResolutionError(
                $"Runtime '{runtime.Rid}' has no strategy field. " +
                "manifest.json schema v2 requires an explicit strategy per runtime entry.");
        }

        PackagingModel declaredModel;
        switch (runtime.Strategy)
        {
            case "hybrid-static":
                declaredModel = PackagingModel.HybridStatic;
                break;
            case "pure-dynamic":
                declaredModel = PackagingModel.PureDynamic;
                break;
            default:
                return new StrategyResolutionError(
                    $"Unknown strategy '{runtime.Strategy}' for RID {runtime.Rid}. " +
                    "Valid values: 'hybrid-static', 'pure-dynamic'.");
        }

        // Coherence check: triplet must match a known pattern
        var triplet = runtime.Triplet;
        var isHybridTriplet = triplet.Contains("-hybrid", StringComparison.OrdinalIgnoreCase);
        var isDynamicTriplet = triplet.Contains("-dynamic", StringComparison.OrdinalIgnoreCase);

        if (!isHybridTriplet && !isDynamicTriplet)
        {
            // Stock triplets (e.g., arm64-windows, x86-windows) are treated as pure-dynamic
            // but only if the declared strategy agrees
            if (declaredModel != PackagingModel.PureDynamic)
            {
                return new StrategyResolutionError(
                    $"Triplet '{triplet}' has no recognized strategy suffix (-hybrid or -dynamic) " +
                    $"but manifest declares '{runtime.Strategy}' for RID {runtime.Rid}. " +
                    "Stock triplets can only be used with 'pure-dynamic' strategy.");
            }

            return declaredModel;
        }

        var expectedFromTriplet = isHybridTriplet
            ? PackagingModel.HybridStatic
            : PackagingModel.PureDynamic;

        if (expectedFromTriplet != declaredModel)
        {
            return new StrategyResolutionError(
                $"Triplet '{triplet}' implies {expectedFromTriplet} but manifest declares '{runtime.Strategy}' for RID {runtime.Rid}. " +
                "Fix the strategy field in manifest.json or use the correct triplet.");
        }

        return declaredModel;
    }
}
