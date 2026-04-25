using Build.Context.Models;
using Build.Domain.Packaging;
using Build.Domain.Preflight;
using Build.Domain.Versioning;
using Cake.Core;
using Cake.Core.IO;
using Cake.Git;
using NuGet.Versioning;

namespace Build.Application.Versioning;

/// <summary>
/// <see cref="IPackageVersionProvider"/> that derives per-family versions from git tags
/// pointing at HEAD.
/// Tag format: <c>{manifest.package_families[].tag_prefix}-{semver}</c>
/// (for example, <c>sdl2-image-2.8.0</c>). Backed by Cake.Frosting.Git aliases
/// over LibGit2Sharp.
/// </summary>
/// <remarks>
/// Cake.Frosting.Git bypasses <c>ICakeContext.FileSystem</c>, so fake-filesystem unit tests
/// cannot exercise this provider. Coverage therefore lives in integration tests that create
/// an ephemeral LibGit2Sharp repository on disk.
/// </remarks>
public sealed class GitTagVersionProvider(
    ManifestConfig manifestConfig,
    IUpstreamVersionAlignmentValidator upstreamVersionAlignmentValidator,
    ICakeContext cakeContext,
    DirectoryPath repoRoot,
    GitTagScope scope) : IPackageVersionProvider
{
    private readonly ManifestConfig _manifestConfig = manifestConfig ?? throw new ArgumentNullException(nameof(manifestConfig));
    private readonly IUpstreamVersionAlignmentValidator _upstreamVersionAlignmentValidator = upstreamVersionAlignmentValidator ?? throw new ArgumentNullException(nameof(upstreamVersionAlignmentValidator));
    private readonly ICakeContext _cakeContext = cakeContext ?? throw new ArgumentNullException(nameof(cakeContext));
    private readonly DirectoryPath _repoRoot = repoRoot ?? throw new ArgumentNullException(nameof(repoRoot));
    private readonly GitTagScope _scope = scope ?? throw new ArgumentNullException(nameof(scope));

    public Task<IReadOnlyDictionary<string, NuGetVersion>> ResolveAsync(
        IReadOnlySet<string> requestedScope,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(requestedScope);
        cancellationToken.ThrowIfCancellationRequested();

        var headSha = ResolveHeadCommitSha();
        var tagsAtHead = CollectTagsAtHead(headSha);

        var providerFamilies = ResolveProviderFamilies();
        var effectiveFamilies = FilterByRequestedScope(providerFamilies, requestedScope);

        var mapping = new Dictionary<string, NuGetVersion>(effectiveFamilies.Count, StringComparer.OrdinalIgnoreCase);
        foreach (var family in effectiveFamilies)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var version = ResolveVersionForFamily(family, tagsAtHead, headSha);
            mapping[family.Name] = version;
        }

        EnforceG54Alignment(mapping);

        if (_scope is GitTagScope.Train && effectiveFamilies.Count > 1)
        {
            return Task.FromResult<IReadOnlyDictionary<string, NuGetVersion>>(OrderTopologically(effectiveFamilies, mapping));
        }

        return Task.FromResult<IReadOnlyDictionary<string, NuGetVersion>>(mapping);
    }

    private string ResolveHeadCommitSha()
    {
        var tip = _cakeContext.GitLogTip(_repoRoot);
        if (tip is null || string.IsNullOrWhiteSpace(tip.Sha))
        {
            throw new CakeException(
                $"GitTagVersionProvider could not resolve HEAD commit SHA at '{_repoRoot.FullPath}'. " +
                "Ensure the repo root points at a valid git checkout.");
        }

        return tip.Sha;
    }

    private List<GitTagAtHead> CollectTagsAtHead(string headSha)
    {
        var tags = _cakeContext.GitTags(_repoRoot, loadTargets: true);
        var matches = new List<GitTagAtHead>();
        foreach (var tag in tags)
        {
            if (tag.Target is not null && string.Equals(tag.Target.Sha, headSha, StringComparison.OrdinalIgnoreCase))
            {
                matches.Add(new GitTagAtHead(tag.FriendlyName, tag.Target.Sha));
            }
        }

        return matches;
    }

    private List<PackageFamilyConfig> ResolveProviderFamilies()
    {
        return _scope switch
        {
            GitTagScope.Targeted targeted => [ResolveFamilyByIdOrThrow(targeted.FamilyId)],
            GitTagScope.Train => [.. _manifestConfig.PackageFamilies
                .Where(family => !string.IsNullOrWhiteSpace(family.ManagedProject) && !string.IsNullOrWhiteSpace(family.NativeProject))],
            _ => throw new CakeException($"GitTagVersionProvider received unrecognized scope '{_scope.GetType().Name}'."),
        };
    }

    private PackageFamilyConfig ResolveFamilyByIdOrThrow(string familyId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(familyId);

        var match = _manifestConfig.PackageFamilies.SingleOrDefault(candidate =>
            string.Equals(candidate.Name, familyId, StringComparison.OrdinalIgnoreCase));

        if (match is null)
        {
            throw new CakeException(
                $"GitTagVersionProvider (Single scope) references family '{familyId}' " +
                "that is not present in manifest.json package_families[].");
        }

        return match;
    }

    private static List<PackageFamilyConfig> FilterByRequestedScope(
        IReadOnlyList<PackageFamilyConfig> providerFamilies,
        IReadOnlySet<string> requestedScope)
    {
        if (requestedScope.Count == 0)
        {
            return [.. providerFamilies];
        }

        var providerByName = providerFamilies.ToDictionary(
            family => family.Name,
            StringComparer.OrdinalIgnoreCase);

        var missing = requestedScope
            .Where(name => !providerByName.ContainsKey(name))
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (missing.Count > 0)
        {
            throw new CakeException(
                "GitTagVersionProvider cannot satisfy requested scope entries outside provider coverage: " +
                string.Join(", ", missing) +
                ". Narrow the --scope filter or widen the GitTagScope configuration.");
        }

        return [.. providerFamilies.Where(family => requestedScope.Contains(family.Name))];
    }

    private static NuGetVersion ResolveVersionForFamily(PackageFamilyConfig family, IReadOnlyList<GitTagAtHead> tagsAtHead, string headSha)
    {
        var tagPrefix = string.IsNullOrWhiteSpace(family.TagPrefix) ? family.Name : family.TagPrefix;
        var expectedPrefix = tagPrefix + "-";

        var candidates = tagsAtHead
            .Where(tag => tag.FriendlyName.StartsWith(expectedPrefix, StringComparison.Ordinal))
            .ToList();

        if (candidates.Count == 0)
        {
            throw new CakeException(
                $"GitTagVersionProvider found no '{expectedPrefix}<semver>' tag at HEAD ({headSha[..Math.Min(7, headSha.Length)]}) for family '{family.Name}'. " +
                "Ensure the expected release tag is pushed and checked out before invoking ResolveVersions with --version-source=git-tag or meta-tag.");
        }

        if (candidates.Count > 1)
        {
            throw new CakeException(
                $"GitTagVersionProvider found multiple '{expectedPrefix}<semver>' tags at HEAD for family '{family.Name}': " +
                string.Join(", ", candidates.Select(candidate => candidate.FriendlyName).OrderBy(name => name, StringComparer.Ordinal)) +
                ". Exactly one family tag per HEAD commit is expected.");
        }

        var versionLiteral = candidates[0].FriendlyName[expectedPrefix.Length..];
        if (!NuGetVersion.TryParse(versionLiteral, out var version))
        {
            throw new CakeException(
                $"GitTagVersionProvider could not parse '{versionLiteral}' as a NuGet semantic version " +
                $"from tag '{candidates[0].FriendlyName}' for family '{family.Name}'.");
        }

        return version;
    }

    private void EnforceG54Alignment(IReadOnlyDictionary<string, NuGetVersion> mapping)
    {
        var validation = _upstreamVersionAlignmentValidator.Validate(_manifestConfig, mapping);
        if (!validation.IsError())
        {
            return;
        }

        var errors = validation.Validation.Checks
            .Where(check => check.IsError && !string.IsNullOrWhiteSpace(check.ErrorMessage))
            .Select(check => check.ErrorMessage!);

        throw new CakeException(
            "GitTagVersionProvider G54 (upstream version alignment) rejected one or more entries:" +
            Environment.NewLine +
            "  - " + string.Join(Environment.NewLine + "  - ", errors));
    }

    private static Dictionary<string, NuGetVersion> OrderTopologically(
        IReadOnlyList<PackageFamilyConfig> families,
        Dictionary<string, NuGetVersion> mapping)
    {
        if (!FamilyTopologyHelpers.TryOrderByDependencies(families, out var ordered, out var errorMessage))
        {
            throw new CakeException(errorMessage);
        }

        return ordered
            .Select(family => family.Name)
            .ToDictionary(name => name, name => mapping[name], StringComparer.OrdinalIgnoreCase);
    }

    private sealed record GitTagAtHead(string FriendlyName, string TargetSha);
}
