using System.Diagnostics.CodeAnalysis;
using Build.Host;
using Build.Host.Configuration;
using Cake.Common;
using Cake.Core;
using Cake.Frosting;

namespace Build.Features.Publishing;

[TaskName("PublishStaging")]
[TaskDescription("Pushes packed nupkgs to the GitHub Packages staging feed.")]
[SuppressMessage("Minor Code Smell", "S1075:URIs should not be hardcoded",
    Justification = "Internal feed URL is part of the release-lifecycle contract, not operator-tunable.")]
public sealed class PublishStagingTask(
    PublishPipeline runner,
    PackageBuildConfiguration packageBuildConfiguration,
    ICakeContext cakeContext) : AsyncFrostingTask<BuildContext>
{
    private const string GitHubPackagesFeedUrl = "https://nuget.pkg.github.com/janset2d/index.json";

    private static readonly string[] AuthEnvVarChain = ["GH_TOKEN", "GITHUB_TOKEN"];

    private readonly PublishPipeline _runner = runner ?? throw new ArgumentNullException(nameof(runner));
    private readonly PackageBuildConfiguration _packageBuildConfiguration = packageBuildConfiguration ?? throw new ArgumentNullException(nameof(packageBuildConfiguration));
    private readonly ICakeContext _cakeContext = cakeContext ?? throw new ArgumentNullException(nameof(cakeContext));

    public override Task RunAsync(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (_packageBuildConfiguration.FamilyVersions.Count == 0)
        {
            throw new CakeException(
                "PublishStaging requires --versions-file <path>. " +
                "Run --target ResolveVersions first to produce a versions.json, " +
                "then re-run with --versions-file artifacts/resolve-versions/versions.json.");
        }

        var authToken = ResolveAuthToken();

        var request = new PublishRequest(
            FeedUrl: GitHubPackagesFeedUrl,
            AuthToken: authToken,
            Versions: _packageBuildConfiguration.FamilyVersions);

        return _runner.RunAsync(request);
    }

    private string ResolveAuthToken()
    {
        foreach (var envVar in AuthEnvVarChain)
        {
            var value = _cakeContext.EnvironmentVariable(envVar);
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        throw new CakeException(
            $"PublishStaging requires a GitHub Packages auth token. Set one of: {string.Join(", ", AuthEnvVarChain)}. " +
            "CI: release.yml maps ${{ secrets.GITHUB_TOKEN }} into GH_TOKEN automatically. " +
            "Local escape hatch: 'gh auth token' produces a usable value (PAT with write:packages scope works too).");
    }
}
