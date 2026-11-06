namespace Build.Host.Runtime;

/// <summary>
/// Build-host-local OS family enum, decoupled from <c>Cake.Core.PlatformFamily</c>.
/// The <c>Build.Runtime</c> root concept carries no Cake dependencies — the build
/// host's own runtime vocabulary lives here. Values intentionally mirror the Cake
/// enum's three concrete platform names so existing string-comparison callsites
/// (<c>PlatformFamily.ToString()</c> producing "Windows" / "Linux" / "OSX") keep working.
/// </summary>
/// <remarks>
/// Build-host code that talks to <see cref="IRuntimeProfile.Family"/> uses this local
/// enum. Cake-native tooling and extension code continues to consume
/// <c>Cake.Core.PlatformFamily</c> directly via <c>ICakePlatform.Family</c>.
/// </remarks>
public enum RuntimeFamily
{
    Windows,
    Linux,
    OSX,
}
