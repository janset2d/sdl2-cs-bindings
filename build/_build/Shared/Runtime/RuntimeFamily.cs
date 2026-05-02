namespace Build.Shared.Runtime;

/// <summary>
/// Build-host-local OS family enum, decoupled from <c>Cake.Core.PlatformFamily</c>.
/// Per ADR-004 §2.6 the <c>Shared/</c> layer carries no Cake dependencies — the build
/// host's own runtime vocabulary lives here. Values intentionally mirror the Cake
/// enum's three concrete platform names so existing string-comparison callsites
/// (<c>PlatformFamily.ToString()</c> producing "Windows" / "Linux" / "OSX") keep working.
/// </summary>
/// <remarks>
/// Tools, Integrations, and Cake extension code (e.g. <c>Tools/Vcpkg/VcpkgTool</c>,
/// <c>Features/Harvesting/ArtifactPlanner</c>, <c>Host/Cake/CakePlatformExtensions</c>)
/// continue to consume <c>Cake.Core.PlatformFamily</c> directly via
/// <c>ICakePlatform.Family</c> — that's the Cake-native side of the boundary. Pure
/// Shared / Features code that talks to <see cref="IRuntimeProfile.Family"/> uses this
/// local enum.
/// </remarks>
public enum RuntimeFamily
{
    Windows,
    Linux,
    OSX,
}
