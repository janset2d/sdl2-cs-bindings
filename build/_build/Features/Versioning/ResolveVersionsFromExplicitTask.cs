using Build.Host;
using Build.Shared.Manifest;
using Build.Shared.Versioning;
using Cake.Core;
using Cake.Frosting;
using NuGet.Versioning;

namespace Build.Features.Versioning;

/// <summary>
/// Resolves per-family versions from operator-supplied <c>--explicit-version</c> (repeated)
/// or <c>--explicit-versions</c> (comma-separated) input, validates them against
/// <c>manifest.json library_manifests[].vcpkg_version</c> (G54 upstream alignment), then
/// writes <c>artifacts/resolve-versions/versions.json</c>.
/// <para>
/// Inputs read from <see cref="BuildContext.ParsedArguments"/>:
/// <c>ExplicitVersion</c> XOR <c>ExplicitVersions</c> (mutually exclusive). Self-validates
/// at task entry per the per-task validation principle. The operator-supplied mapping IS
/// the scope — there is no <c>--scope</c> filter on this task.
/// </para>
/// </summary>
[TaskName("ResolveVersionsFromExplicit")]
[TaskDescription("Resolves per-family versions from --explicit-version / --explicit-versions; emits artifacts/resolve-versions/versions.json")]
public sealed class ResolveVersionsFromExplicitTask(
    ManifestConfig manifestConfig,
    IUpstreamVersionAlignmentValidator upstreamVersionAlignmentValidator,
    VersionsJsonWriter writer) : AsyncFrostingTask<BuildContext>
{
    private readonly ManifestConfig _manifestConfig = manifestConfig ?? throw new ArgumentNullException(nameof(manifestConfig));

    private readonly IUpstreamVersionAlignmentValidator _upstreamVersionAlignmentValidator =
        upstreamVersionAlignmentValidator ?? throw new ArgumentNullException(nameof(upstreamVersionAlignmentValidator));

    private readonly VersionsJsonWriter _writer = writer ?? throw new ArgumentNullException(nameof(writer));

    public override async Task RunAsync(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(context.ParsedArguments);

        var mapping = ResolveOperatorMapping(context.ParsedArguments);

        EnforceUpstreamVersionAlignment(mapping);

        await _writer.WriteAsync(mapping);
    }

    private static IReadOnlyDictionary<string, NuGetVersion> ResolveOperatorMapping(ParsedArguments args)
    {
        var hasExplicitVersion = args.ExplicitVersion.Any(e => !string.IsNullOrWhiteSpace(e));
        var hasExplicitVersions = !string.IsNullOrWhiteSpace(args.ExplicitVersions);

        if (hasExplicitVersion && hasExplicitVersions)
        {
            throw new CakeException(
                "ResolveVersionsFromExplicit: --explicit-version and --explicit-versions are mutually exclusive. " +
                "Use one shape or the other (repeated CLI entries OR a single comma-separated string).");
        }

        if (!hasExplicitVersion && !hasExplicitVersions)
        {
            throw new CakeException(
                "ResolveVersionsFromExplicit requires at least one --explicit-version <family>=<semver> entry " +
                "or --explicit-versions \"<family1>=<semver1>,<family2>=<semver2>,...\" so it can emit versions.json.");
        }

        try
        {
            return hasExplicitVersion
                ? ExplicitVersionParser.ParseCliEntries(args.ExplicitVersion)
                : ExplicitVersionParser.ParseCommaSeparated(args.ExplicitVersions);
        }
        catch (ArgumentException ex)
        {
            throw new CakeException($"ResolveVersionsFromExplicit could not parse operator input: {ex.Message}", ex);
        }
    }

    private void EnforceUpstreamVersionAlignment(IReadOnlyDictionary<string, NuGetVersion> mapping)
    {
        var validationResult = _upstreamVersionAlignmentValidator.Validate(_manifestConfig, mapping);
        if (!validationResult.IsError())
        {
            return;
        }

        var errors = validationResult.Validation.Checks
            .Where(check => check.IsError && !string.IsNullOrWhiteSpace(check.ErrorMessage))
            .Select(check => check.ErrorMessage!);

        throw new CakeException(
            "ResolveVersionsFromExplicit G54 (upstream version alignment) rejected one or more entries:" +
            Environment.NewLine +
            "  - " + string.Join(Environment.NewLine + "  - ", errors));
    }
}
