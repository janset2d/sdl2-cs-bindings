using Build.Host;
using Build.Repositories;
using Build.Shared.Manifest;
using Build.Validation.Versioning;
using Build.Versioning;
using Cake.Core;
using Cake.Frosting;

namespace Build.Targets.ResolveVersionsFromExplicit;

[TaskName("ResolveVersionsFromExplicit")]
[TaskDescription("Resolves per-family versions from --explicit-version / --explicit-versions; writes versions.json to --versions-file path")]
public sealed class ResolveVersionsFromExplicitTask(
    IManifestRepository manifestRepository,
    IVersionFileRepository versionFileRepository,
    IUpstreamVersionAlignmentValidator upstreamVersionAlignmentValidator) : AsyncFrostingTask<BuildContext>
{
    private readonly IManifestRepository _manifestRepository = manifestRepository ?? throw new ArgumentNullException(nameof(manifestRepository));
    private readonly IVersionFileRepository _versionFileRepository = versionFileRepository ?? throw new ArgumentNullException(nameof(versionFileRepository));
    private readonly IUpstreamVersionAlignmentValidator _upstreamVersionAlignmentValidator = upstreamVersionAlignmentValidator ?? throw new ArgumentNullException(nameof(upstreamVersionAlignmentValidator));

    public override async Task RunAsync(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.VersionsFilePath is null)
        {
            throw new CakeException(
                "ResolveVersionsFromExplicit requires --versions-file <path>. " +
                "Example: --versions-file artifacts/resolve-versions/versions.json");
        }

        var input = ResolveOperatorInput(context);
        var versions = ParseOperatorMapping(input);
        var manifest = _manifestRepository.Load();

        EnforceUpstreamVersionAlignment(manifest, versions);

        await _versionFileRepository.SaveAsync(context.VersionsFilePath, versions);
    }

    private static ExplicitVersionInput ResolveOperatorInput(BuildContext context)
    {
        var hasExplicitVersion = context.ExplicitVersionEntries.Any(static entry => !string.IsNullOrWhiteSpace(entry));
        var hasExplicitVersions = !string.IsNullOrWhiteSpace(context.ExplicitVersions);

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

        return hasExplicitVersion
            ? new ExplicitVersionInput(ExplicitVersionInputKind.RepeatedEntries, context.ExplicitVersionEntries, CommaSeparated: null)
            : new ExplicitVersionInput(ExplicitVersionInputKind.CommaSeparated, Entries: [], context.ExplicitVersions);
    }

    private static PackageFamilyVersionSet ParseOperatorMapping(ExplicitVersionInput input)
    {
        try
        {
            return input.Kind switch
            {
                ExplicitVersionInputKind.RepeatedEntries => ExplicitVersionParser.ParseCliEntries(input.Entries),
                ExplicitVersionInputKind.CommaSeparated => ExplicitVersionParser.ParseCommaSeparated(input.CommaSeparated),
                _ => throw new CakeException("ResolveVersionsFromExplicit received an unsupported explicit version input shape."),
            };
        }
        catch (ArgumentException ex)
        {
            throw new CakeException($"ResolveVersionsFromExplicit could not parse operator input: {ex.Message}", ex);
        }
    }

    private void EnforceUpstreamVersionAlignment(ManifestConfig manifest, PackageFamilyVersionSet versions)
    {
        var validation = _upstreamVersionAlignmentValidator.Validate(manifest, versions);
        if (!validation.HasErrors)
        {
            return;
        }

        var errors = validation.Checks
            .Where(check => check.IsError && !string.IsNullOrWhiteSpace(check.ErrorMessage))
            .Select(check => check.ErrorMessage!);

        throw new CakeException(
            "ResolveVersionsFromExplicit upstream version alignment [G54] rejected one or more entries:" +
            Environment.NewLine +
            "  - " + string.Join(Environment.NewLine + "  - ", errors));
    }

    private enum ExplicitVersionInputKind
    {
        RepeatedEntries,
        CommaSeparated,
    }

    private sealed record ExplicitVersionInput(ExplicitVersionInputKind Kind, IReadOnlyList<string> Entries, string? CommaSeparated);
}
