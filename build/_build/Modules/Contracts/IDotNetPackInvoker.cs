using Build.Modules.Packaging.Results;
using Cake.Core.IO;

namespace Build.Modules.Contracts;

public interface IDotNetPackInvoker
{
    /// <summary>
    /// Invokes <c>dotnet pack</c> for <paramref name="projectPath"/>. Returns a typed
    /// <see cref="DotNetPackResult"/> capturing either success (artifacts written to
    /// <c>artifacts/packages</c>) or a <see cref="DotNetPackError"/> describing the underlying
    /// Cake/MSBuild failure. Exceptions from the underlying invocation are wrapped instead
    /// of surfacing as raw <c>CakeException</c>s.
    /// </summary>
    DotNetPackResult Pack(FilePath projectPath, DotNetPackInvocation invocation, bool noRestore, bool noBuild);
}

/// <summary>
/// Parameters that travel together across every <c>dotnet pack</c> invocation
/// driven by <see cref="IDotNetPackInvoker"/>. Each pack invocation sets the
/// MSBuild <c>Version</c> global to the family version and optionally threads
/// <c>NativePayloadSource</c> through to the native csproj's content include
/// patterns (see <c>src/native/Directory.Build.props</c>).
/// </summary>
public sealed record DotNetPackInvocation(string Configuration, string Version, DirectoryPath? NativePayloadSource);
