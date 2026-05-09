using Build.Results;
using Build.Shared.Packaging;
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
