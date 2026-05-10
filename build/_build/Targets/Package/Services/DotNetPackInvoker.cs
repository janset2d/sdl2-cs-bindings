using Build.Host.Paths;
using Build.Results;
using Build.Targets.Package.Models;
using Cake.Common.Tools.DotNet;
using Cake.Common.Tools.DotNet.MSBuild;
using Cake.Common.Tools.DotNet.Pack;
using Cake.Core;
using Cake.Core.Diagnostics;
using Cake.Core.IO;

namespace Build.Targets.Package.Services;

public interface IDotNetPackInvoker
{
    /// <summary>
    /// Invokes <c>dotnet pack</c> for <paramref name="projectPath"/>. Returns a typed
    /// <see cref="Result{TValue,TError}"/> where success carries <see cref="Unit"/> (artifacts
    /// written to <c>artifacts/packages</c>) and failure carries a <see cref="DotNetPackError"/>
    /// describing the underlying Cake/MSBuild failure. Exceptions from the underlying
    /// invocation are wrapped instead of surfacing as raw <c>CakeException</c>s.
    /// </summary>
    Result<Unit, DotNetPackError> Pack(FilePath projectPath, DotNetPackInvocation invocation, bool noRestore, bool noBuild);
}

/// <summary>
/// Parameters that travel together across every <c>dotnet pack</c> invocation
/// driven by <see cref="IDotNetPackInvoker"/>. Each pack invocation sets the
/// MSBuild <c>Version</c> global to the family version and optionally threads
/// <c>NativePayloadSource</c> through to the native csproj's content include
/// patterns (see <c>src/native/Directory.Build.props</c>).
/// </summary>
public sealed record DotNetPackInvocation(string Configuration, string Version, DirectoryPath? NativePayloadSource);

public sealed class DotNetPackInvoker(ICakeContext cakeContext, ICakeLog log, IPathService pathService) : IDotNetPackInvoker
{
    private const string NativePayloadSourceProperty = "NativePayloadSource";

    private readonly ICakeContext _cakeContext = cakeContext ?? throw new ArgumentNullException(nameof(cakeContext));
    private readonly ICakeLog _log = log ?? throw new ArgumentNullException(nameof(log));
    private readonly IPathService _pathService = pathService ?? throw new ArgumentNullException(nameof(pathService));

    /// <inheritdoc />
    public Result<Unit, DotNetPackError> Pack(FilePath projectPath, DotNetPackInvocation invocation, bool noRestore, bool noBuild)
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
            return Result<Unit, DotNetPackError>.Failure(
                new DotNetPackError($"dotnet pack failed for '{projectPath.GetFilename().FullPath}' at version {invocation.Version}: {ex.Message}", projectPath.FullPath, ex));
        }

        return Result<Unit, DotNetPackError>.Success(Unit.Value);
    }

    private static DotNetMSBuildSettings BuildMSBuildSettings(DotNetPackInvocation invocation)
    {
        // Version is resolved upstream by PackageTask from --versions-file (the resolved
        // PackageFamilyVersionSet) and threaded through DotNetPackInvocation as $(Version)
        // in every pack invocation.
        var settings = new DotNetMSBuildSettings
        {
            Version = invocation.Version,
        };

        if (invocation.NativePayloadSource is not null)
        {
            settings.WithProperty(NativePayloadSourceProperty, invocation.NativePayloadSource.FullPath);
        }

        return settings;
    }
}
