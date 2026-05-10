using Build.Data.Versions;
using Build.Host;
using Build.Data.Manifest;
using Build.Targets.Package.Reporting;
using Build.Targets.Package.Services;
using Build.Validation.Versioning;
using Cake.Common.IO;
using Cake.Core;
using Cake.Core.IO;
using Cake.Frosting;
using Cake.Git;

namespace Build.Targets.Package;

/// <summary>
/// Cake target for packing managed + native NuGet packages per family. Owns top-level
/// orchestration directly: validates inputs, re-runs the G58 cross-family resolvability
/// check at pack time, resolves selected families in topological order, generates the
/// README mapping table, and delegates the per-family 3-phase flow (EnsureHarvestReady →
/// PrepareMetadata → PackAndValidate) to <see cref="PackageFamilyPacker"/>.
/// </summary>
[TaskName("Package")]
[TaskDescription("Packs managed/native families with explicit version propagation and post-pack nuspec assertions")]
public sealed class PackageTask : AsyncFrostingTask<BuildContext>
{
    private readonly IVersionFileRepository _versionFileRepository;
    private readonly IReadmeMappingTableGenerator _readmeMappingTableGenerator;
    private readonly ICrossFamilyDependencyResolvabilityValidator _crossFamilyDependencyResolvabilityValidator;
    private readonly PackageFamilyPacker _packer;
    private readonly PackageReporter _reporter;
    private readonly Func<ICakeContext, DirectoryPath, string> _resolveHeadCommitSha;

    public PackageTask(
        IVersionFileRepository versionFileRepository,
        IReadmeMappingTableGenerator readmeMappingTableGenerator,
        ICrossFamilyDependencyResolvabilityValidator crossFamilyDependencyResolvabilityValidator,
        PackageFamilyPacker packer,
        PackageReporter reporter,
        Func<ICakeContext, DirectoryPath, string>? resolveHeadCommitSha = null)
    {
        _versionFileRepository = versionFileRepository ?? throw new ArgumentNullException(nameof(versionFileRepository));
        _readmeMappingTableGenerator = readmeMappingTableGenerator ?? throw new ArgumentNullException(nameof(readmeMappingTableGenerator));
        _crossFamilyDependencyResolvabilityValidator = crossFamilyDependencyResolvabilityValidator ?? throw new ArgumentNullException(nameof(crossFamilyDependencyResolvabilityValidator));
        _packer = packer ?? throw new ArgumentNullException(nameof(packer));
        _reporter = reporter ?? throw new ArgumentNullException(nameof(reporter));

        // Cake.Git bypasses ICakeContext.FileSystem and hits System.IO directly via LibGit2Sharp's
        // native binary, which means unit tests against FakeFileSystem can't be served by the
        // default resolver. The optional ctor hook lets V2 scenario tests inject a stub lambda;
        // production uses the default GitLogTip-backed resolver. End-to-end runs against a real
        // repo (release.yml + tools.cs ci-sim) exercise the default, so default behavior stays
        // covered without a dedicated integration test.
        _resolveHeadCommitSha = resolveHeadCommitSha ?? DefaultResolveHeadCommitSha;
    }

    public override async Task RunAsync(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.VersionsFilePath is null)
        {
            throw new CakeException(
                "PackageTask requires --versions-file <path>. " +
                "Run --target ResolveVersions first to produce a versions.json " +
                "(e.g. --target ResolveVersions --version-source=manifest --suffix=local.<timestamp>), " +
                "then re-run with --versions-file artifacts/resolve-versions/versions.json.");
        }

        var versions = _versionFileRepository.Load(context.VersionsFilePath);

        if (versions.Count == 0)
        {
            throw new CakeException(
                "PackageTask requires a non-empty version mapping. " +
                "Re-run --target ResolveVersionsFromManifest or --target ResolveVersionsFromExplicit.");
        }

        // [G58] runs again here as a pack-stage guard, even if the caller skipped PreFlight.
        var crossFamilyValidation = _crossFamilyDependencyResolvabilityValidator.Validate(versions, context.Manifest);
        if (crossFamilyValidation.HasErrors)
        {
            _reporter.ReportCrossFamilyResolvabilityErrors(crossFamilyValidation);
            throw new CakeException(
                "Package task refused to proceed: cross-family dependency resolvability [G58] detected " +
                $"{crossFamilyValidation.Checks.Count(check => check.IsError)} unresolved dependency/dependencies. " +
                "Either include the missing families in --explicit-version, or (post-C feed-probe wiring) pass --feed <URL> to enable target-feed resolution.");
        }

        var families = ResolveSelectedFamilies(versions, context.Manifest);
        var headSha = _resolveHeadCommitSha(context, context.Paths.RepoRoot);

        // G57 generator: keep README mapping block aligned with manifest before pack validation.
        await _readmeMappingTableGenerator.UpdateAsync(CancellationToken.None);

        context.EnsureDirectoryExists(context.Paths.PackagesOutput);

        foreach (var family in families)
        {
            var version = versions.RequireVersion(new PackageFamilyId(family.Name)).ToNormalizedString();
            await _packer.PackAsync(family, version, headSha, context.BuildConfiguration, CancellationToken.None);
        }
    }

    private static IReadOnlyList<PackageFamilyConfig> ResolveSelectedFamilies(PackageFamilyVersionSet explicitVersions, ManifestConfig manifestConfig)
    {
        var selectedFamilies = new List<PackageFamilyConfig>(explicitVersions.Count);

        foreach (var entry in explicitVersions)
        {
            var requestedFamily = entry.Family.Value;
            var family = manifestConfig.PackageFamilies.SingleOrDefault(candidate =>
                string.Equals(candidate.Name, requestedFamily, StringComparison.OrdinalIgnoreCase));

            if (family is null)
            {
                throw new CakeException(
                    $"Package task received unknown family '{requestedFamily}'. Add it to build/manifest.json package_families[] or fix the CLI value.");
            }

            if (string.IsNullOrWhiteSpace(family.ManagedProject) || string.IsNullOrWhiteSpace(family.NativeProject))
            {
                throw new CakeException(
                    $"Package task cannot pack family '{family.Name}' yet because manifest.json does not declare both managed_project and native_project. This usually means the family is still a placeholder.");
            }

            selectedFamilies.Add(family);
        }

        if (!FamilyTopologyHelpers.TryOrderByDependencies(selectedFamilies, out var orderedFamilies, out var errorMessage))
        {
            throw new CakeException(errorMessage);
        }

        return orderedFamilies;
    }

    private static string DefaultResolveHeadCommitSha(ICakeContext context, DirectoryPath repoRoot)
    {
        var tip = context.GitLogTip(repoRoot);
        if (tip is null || string.IsNullOrWhiteSpace(tip.Sha))
        {
            throw new CakeException(
                $"Package task could not resolve git HEAD commit SHA via Cake.Frosting.Git GitLogTip at '{repoRoot.FullPath}'. " +
                "Ensure the repo root is a valid git checkout.");
        }

        return tip.Sha;
    }
}
