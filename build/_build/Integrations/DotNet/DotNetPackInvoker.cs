using Build.Host.Paths;
using Build.Shared.Packaging;
using Cake.Common.Tools.DotNet;
using Cake.Common.Tools.DotNet.MSBuild;
using Cake.Common.Tools.DotNet.Pack;
using Cake.Core;
using Cake.Core.Diagnostics;
using Cake.Core.IO;

namespace Build.Integrations.DotNet;

public sealed class DotNetPackInvoker(ICakeContext cakeContext, ICakeLog log, IPathService pathService) : IDotNetPackInvoker
{
    private const string NativePayloadSourceProperty = "NativePayloadSource";
    private const string MinVerSkipProperty = "MinVerSkip";
    private const string MinVerSkipTrue = "true";

    private readonly ICakeContext _cakeContext = cakeContext ?? throw new ArgumentNullException(nameof(cakeContext));
    private readonly ICakeLog _log = log ?? throw new ArgumentNullException(nameof(log));
    private readonly IPathService _pathService = pathService ?? throw new ArgumentNullException(nameof(pathService));

    public DotNetPackResult Pack(FilePath projectPath, DotNetPackInvocation invocation, bool noRestore, bool noBuild)
    {
        ArgumentNullException.ThrowIfNull(projectPath);
        ArgumentNullException.ThrowIfNull(invocation);

        var settings = new DotNetPackSettings
        {
            Configuration = invocation.Configuration,
            OutputDirectory = _pathService.PackagesOutput,
            NoRestore = noRestore,
            NoBuild = noBuild,
            MSBuildSettings = BuildMSBuildSettings(invocation),
        };

        _log.Information(
            "Running dotnet pack '{0}' at {1} (noRestore={2}, noBuild={3})",
            projectPath.GetFilename().FullPath,
            invocation.Version,
            noRestore,
            noBuild);

        // Cake's DotNetPack surfaces build/pack failures as CakeException. Convert to the
        // Result surface so the runner can aggregate errors alongside the other services.
        try
        {
            _cakeContext.DotNetPack(projectPath.FullPath, settings);
        }
        catch (CakeException ex)
        {
            return new DotNetPackError($"dotnet pack failed for '{projectPath.GetFilename().FullPath}' at version {invocation.Version}: {ex.Message}", projectPath.FullPath, ex);
        }

        return DotNetPackResult.ToSuccess();
    }

    private static DotNetMSBuildSettings BuildMSBuildSettings(DotNetPackInvocation invocation)
    {
        // MinVerSkip=true: Cake has already resolved the family version (MinVer-derived from
        // git tag, or from --family-version override). Setting this property prevents MinVer's
        // own target from re-reading git tags and overwriting $(Version) with its fallback
        // (e.g., 0.0.0-alpha.0.N when no matching tag exists). $(Version) supplied below wins.
        var settings = new DotNetMSBuildSettings
        {
            Version = invocation.Version,
        };

        settings.WithProperty(MinVerSkipProperty, MinVerSkipTrue);

        if (invocation.NativePayloadSource is not null)
        {
            settings.WithProperty(NativePayloadSourceProperty, invocation.NativePayloadSource.FullPath);
        }

        return settings;
    }
}
