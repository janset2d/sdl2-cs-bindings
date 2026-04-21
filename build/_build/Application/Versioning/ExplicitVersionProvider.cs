using Build.Context.Configs;
using Build.Context.Models;
using Build.Domain.Preflight;
using Cake.Core;
using NuGet.Versioning;

namespace Build.Application.Versioning;

/// <summary>
/// <see cref="IPackageVersionProvider"/> backed by an operator-supplied mapping. This is the
/// sole provider that stage tasks see (PreFlight / Pack / PackageConsumerSmoke). Every entry
/// is validated against <see cref="ManifestConfig"/> via
/// <see cref="IUpstreamVersionAlignmentValidator"/> (G54) before the mapping is returned, so
/// an invalid operator input fails fast at provider entry rather than downstream.
/// <para>
/// Slice A note: the G54 validator currently accepts a scalar
/// <see cref="PackageBuildConfiguration"/> (Families + FamilyVersion) and is invoked per
/// mapping entry with a synthetic single-family config. Slice B1 rewrites the validator to
/// accept the mapping directly; this per-entry loop collapses at that point.
/// </para>
/// </summary>
public sealed class ExplicitVersionProvider(
    ManifestConfig manifestConfig,
    IUpstreamVersionAlignmentValidator upstreamVersionAlignmentValidator,
    IReadOnlyDictionary<string, NuGetVersion> operatorSuppliedMapping) : IPackageVersionProvider
{
    private readonly ManifestConfig _manifestConfig = manifestConfig ?? throw new ArgumentNullException(nameof(manifestConfig));
    private readonly IUpstreamVersionAlignmentValidator _upstreamVersionAlignmentValidator = upstreamVersionAlignmentValidator ?? throw new ArgumentNullException(nameof(upstreamVersionAlignmentValidator));
    private readonly Dictionary<string, NuGetVersion> _operatorSuppliedMapping = NormalizeMapping(operatorSuppliedMapping ?? throw new ArgumentNullException(nameof(operatorSuppliedMapping)));

    public Task<IReadOnlyDictionary<string, NuGetVersion>> ResolveAsync(
        IReadOnlySet<string> requestedScope,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(requestedScope);
        cancellationToken.ThrowIfCancellationRequested();

        if (_operatorSuppliedMapping.Count == 0)
        {
            throw new CakeException(
                "ExplicitVersionProvider has no operator-supplied versions. Supply at least one " +
                "--explicit-version <family>=<semver> (repeated) when invoking a stage target, or " +
                "invoke the ResolveVersions target against manifest or git-tag source to populate " +
                "the mapping upstream.");
        }

        var missingInMapping = requestedScope
            .Where(scopeKey => !_operatorSuppliedMapping.ContainsKey(scopeKey))
            .ToList();

        if (missingInMapping.Count > 0)
        {
            var missingList = string.Join(", ", missingInMapping.OrderBy(name => name, StringComparer.OrdinalIgnoreCase));
            throw new CakeException(
                "ExplicitVersionProvider cannot satisfy requested scope: " + missingList +
                ". Add a matching --explicit-version entry for each requested family, or remove the " +
                "missing families from the invoking target's scope.");
        }

        var filteredMapping = requestedScope.Count == 0
            ? _operatorSuppliedMapping
            : BuildFilteredMapping(_operatorSuppliedMapping, requestedScope);

        var validationErrors = CollectUpstreamAlignmentErrors(filteredMapping);
        if (validationErrors.Count > 0)
        {
            throw new CakeException(
                "ExplicitVersionProvider G54 (upstream version alignment) rejected one or more entries:" +
                Environment.NewLine +
                "  - " + string.Join(Environment.NewLine + "  - ", validationErrors));
        }

        return Task.FromResult<IReadOnlyDictionary<string, NuGetVersion>>(filteredMapping);
    }

    private static Dictionary<string, NuGetVersion> NormalizeMapping(
        IReadOnlyDictionary<string, NuGetVersion> source)
    {
        var normalized = new Dictionary<string, NuGetVersion>(source.Count, StringComparer.OrdinalIgnoreCase);
        foreach (var (family, version) in source)
        {
            if (string.IsNullOrWhiteSpace(family))
            {
                throw new ArgumentException("Family identifier cannot be null, empty, or whitespace.", nameof(source));
            }

            ArgumentNullException.ThrowIfNull(version);

            if (!normalized.TryAdd(family, version))
            {
                throw new ArgumentException(
                    $"Duplicate family identifier '{family}' in --explicit-version mapping (matched case-insensitively).",
                    nameof(source));
            }
        }

        return normalized;
    }

    private static Dictionary<string, NuGetVersion> BuildFilteredMapping(
        Dictionary<string, NuGetVersion> source,
        IReadOnlySet<string> requestedScope)
    {
        var filtered = new Dictionary<string, NuGetVersion>(requestedScope.Count, StringComparer.OrdinalIgnoreCase);
        foreach (var scopeKey in requestedScope)
        {
            filtered[scopeKey] = source[scopeKey];
        }

        return filtered;
    }

    private List<string> CollectUpstreamAlignmentErrors(IReadOnlyDictionary<string, NuGetVersion> mapping)
    {
        var errors = new List<string>();
        foreach (var (family, version) in mapping)
        {
            // The current G54 validator accepts a scalar PackageBuildConfiguration; per-entry
            // invocation with a synthetic single-family config is the Slice A shape. Slice B1
            // rewrites the validator to accept the mapping directly and this loop collapses.
            var syntheticConfig = new PackageBuildConfiguration([family], version.ToNormalizedString());
            var result = _upstreamVersionAlignmentValidator.Validate(_manifestConfig, syntheticConfig);

            if (!result.IsError())
            {
                continue;
            }

            errors.AddRange(result.Validation.Checks
                .Where(check => !string.IsNullOrWhiteSpace(check.ErrorMessage))
                .Select(check => check.ErrorMessage!));
        }

        return errors;
    }
}
