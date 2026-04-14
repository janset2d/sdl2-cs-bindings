using Build.Context.Models;
using Build.Modules.Strategy.Models;

namespace Build.Modules.Strategy;

/// <summary>
/// Resolves and validates the packaging strategy for a given runtime entry.
/// <para>
/// The <see cref="RuntimeInfo.Strategy"/> field in manifest.json is the authority.
/// The triplet name is used as a coherence check — if the triplet contains <c>-hybrid</c>,
/// the strategy must be <c>hybrid-static</c>; if it contains <c>-dynamic</c>, the strategy
/// must be <c>pure-dynamic</c>. Triplets matching neither known pattern are rejected.
/// </para>
/// </summary>
public static class StrategyResolver
{
    /// <summary>
    /// Resolves the packaging model from a runtime entry, validating strategy↔triplet coherence.
    /// </summary>
    /// <param name="runtime">The runtime entry from manifest.json.</param>
    /// <returns>The resolved <see cref="PackagingModel"/>.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the strategy field is missing, unknown, or inconsistent with the triplet name.
    /// </exception>
    public static PackagingModel Resolve(RuntimeInfo runtime)
    {
        ArgumentNullException.ThrowIfNull(runtime);

        if (string.IsNullOrEmpty(runtime.Strategy))
        {
            throw new InvalidOperationException(
                $"Runtime '{runtime.Rid}' has no strategy field. " +
                "manifest.json schema v2 requires an explicit strategy per runtime entry.");
        }

        var declared = runtime.Strategy switch
        {
            "hybrid-static" => PackagingModel.HybridStatic,
            "pure-dynamic" => PackagingModel.PureDynamic,
            _ => throw new InvalidOperationException(
                $"Unknown strategy '{runtime.Strategy}' for RID {runtime.Rid}. " +
                "Valid values: 'hybrid-static', 'pure-dynamic'."),
        };

        // Coherence check: triplet must match a known pattern
        var triplet = runtime.Triplet;
        var isHybridTriplet = triplet.Contains("-hybrid", StringComparison.OrdinalIgnoreCase);
        var isDynamicTriplet = triplet.Contains("-dynamic", StringComparison.OrdinalIgnoreCase);

        if (!isHybridTriplet && !isDynamicTriplet)
        {
            // Stock triplets (e.g., arm64-windows, x86-windows) are treated as pure-dynamic
            // but only if the declared strategy agrees
            if (declared != PackagingModel.PureDynamic)
            {
                throw new InvalidOperationException(
                    $"Triplet '{triplet}' has no recognized strategy suffix (-hybrid or -dynamic) " +
                    $"but manifest declares '{runtime.Strategy}' for RID {runtime.Rid}. " +
                    "Stock triplets can only be used with 'pure-dynamic' strategy.");
            }

            return declared;
        }

        var expectedFromTriplet = isHybridTriplet
            ? PackagingModel.HybridStatic
            : PackagingModel.PureDynamic;

        if (expectedFromTriplet != declared)
        {
            throw new InvalidOperationException(
                $"Triplet '{triplet}' implies {expectedFromTriplet} but manifest declares '{runtime.Strategy}' for RID {runtime.Rid}. " +
                "Fix the strategy field in manifest.json or use the correct triplet.");
        }

        return declared;
    }
}
