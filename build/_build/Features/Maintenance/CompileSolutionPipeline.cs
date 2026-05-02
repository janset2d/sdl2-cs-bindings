using Build.Host.Configuration;
using Build.Host.Paths;
using Cake.Common.Tools.DotNet;
using Cake.Common.Tools.DotNet.Build;
using Cake.Core;
using Cake.Core.Diagnostics;

namespace Build.Features.Maintenance;

/// <summary>
/// Thin wrapper around <c>dotnet build Janset.SDL2.sln</c>. Replaces the WSL-playbook §8 solution-build step.
/// </summary>
public sealed class CompileSolutionPipeline(
    ICakeContext cakeContext,
    ICakeLog log,
    IPathService pathService,
    DotNetBuildConfiguration buildConfiguration)
{
    private readonly ICakeContext _cakeContext = cakeContext ?? throw new ArgumentNullException(nameof(cakeContext));
    private readonly ICakeLog _log = log ?? throw new ArgumentNullException(nameof(log));
    private readonly IPathService _pathService = pathService ?? throw new ArgumentNullException(nameof(pathService));
    private readonly DotNetBuildConfiguration _buildConfiguration = buildConfiguration ?? throw new ArgumentNullException(nameof(buildConfiguration));

    public Task RunAsync()
    {
        var solution = _pathService.SolutionFile;
        var settings = new DotNetBuildSettings
        {
            Configuration = _buildConfiguration.Configuration,
            NoLogo = true,
        };

        _log.Information("Building solution '{0}' (Configuration={1}).", solution.FullPath, _buildConfiguration.Configuration);
        _cakeContext.DotNetBuild(solution.FullPath, settings);

        return Task.CompletedTask;
    }
}
