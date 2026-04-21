using Cake.Core.IO;
using NuGet.Versioning;

namespace Build.Domain.Packaging.Models;

/// <summary>
/// ADR-003 §3.2 per-stage request driving the stateless <c>PackageConsumerSmokeRunner</c>.
/// Carries everything the runner needs to execute a single matrix entry (RID + resolved
/// version mapping + feed directory). The runner reads no DI-scoped version state; this
/// shape is what enables the ADR-003 §3.4 consumer-smoke matrix re-entry — each RID runner
/// invokes the same target with its per-RID request and the same mapping.
/// </summary>
/// <param name="Rid">Target RID the smoke csproj restores + runs against (e.g.,
/// <c>win-x64</c>). Drives <c>dotnet test -r &lt;rid&gt;</c>.</param>
/// <param name="Versions">Case-insensitive per-family mapping. Smoke csproj's
/// <c>PackageReference</c> entries resolve against these exact versions via
/// <c>-p:Janset&lt;Major&gt;&lt;Role&gt;PackageVersion=&lt;semver&gt;</c> MSBuild overrides.</param>
/// <param name="FeedPath">Local folder feed containing the <c>.nupkg</c> set. Default origin
/// is <c>IPathService.PackagesOutput</c> (<c>artifacts/packages</c>); CLI flag
/// <c>--feed-path</c> overrides when the caller needs to point at a workspace-local CI
/// download directory.</param>
public sealed record PackageConsumerSmokeRequest(
    string Rid,
    IReadOnlyDictionary<string, NuGetVersion> Versions,
    DirectoryPath FeedPath);
