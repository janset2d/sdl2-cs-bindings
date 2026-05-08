using System.Collections.Immutable;
using Build.Host.Paths;
using Build.Results;
using Build.Shared.Manifest;
using Cake.Common.IO;
using Cake.Core;

namespace Build.Features.Preflight;

/// <summary>
/// Validates that every runtime entry in <c>manifest.json</c> uses a hybrid overlay triplet:
/// the triplet name ends with <c>-hybrid</c> AND a corresponding overlay <c>.cmake</c> file
/// exists at <c>vcpkg-overlay-triplets/{triplet}.cmake</c>.
/// </summary>
/// <remarks>
/// G16 (release-guardrails) — replaces the strategy↔triplet coherence check retired in S11.
/// The hybrid-static packaging model is encoded in the overlay triplet itself; this validator
/// catches manifest typos (wrong suffix) and missing overlay drops (declared triplet has no
/// backing <c>.cmake</c> file).
/// </remarks>
public sealed class HybridStaticOverlayValidator(ICakeContext cakeContext, IPathService pathService)
{
    private readonly ICakeContext _cakeContext = cakeContext ?? throw new ArgumentNullException(nameof(cakeContext));
    private readonly IPathService _pathService = pathService ?? throw new ArgumentNullException(nameof(pathService));

    public ValidationReport Validate(IImmutableList<RuntimeInfo> runtimes)
    {
        ArgumentNullException.ThrowIfNull(runtimes);

        if (runtimes.Count == 0)
        {
            return ValidationReport.Empty;
        }

        var checks = new List<ValidationCheck>();
        var overlayDir = _pathService.VcpkgOverlayTripletsDir;

        foreach (var runtime in runtimes)
        {
            if (!runtime.Triplet.EndsWith("-hybrid", StringComparison.OrdinalIgnoreCase))
            {
                checks.Add(new ValidationCheck(
                    Name: "Hybrid overlay triplet name",
                    Severity: ValidationSeverity.Error,
                    Message: $"Runtime '{runtime.Rid}' uses triplet '{runtime.Triplet}' which does not end with '-hybrid'. The hybrid-static packaging model requires hybrid overlay triplets only.",
                    Code: "G16"));
                continue;
            }

            var overlayFile = overlayDir.CombineWithFilePath($"{runtime.Triplet}.cmake");
            if (!_cakeContext.FileExists(overlayFile))
            {
                checks.Add(new ValidationCheck(
                    Name: "Hybrid overlay triplet file existence",
                    Severity: ValidationSeverity.Error,
                    Message: $"Runtime '{runtime.Rid}' declares triplet '{runtime.Triplet}' but overlay file '{overlayFile.FullPath}' does not exist. Drop the runtime from manifest.json or add the overlay.",
                    Code: "G16"));
            }
        }

        return new ValidationReport(checks);
    }
}
