namespace Build.Host.Runtime;

public interface IRuntimeProfile
{
    public string Rid { get; }

    public string Triplet { get; }

    /// <summary>
    /// Build-host-local OS family for this runtime profile, decoupled from Cake's
    /// <c>PlatformFamily</c>. The runtime concept carries no Cake dependencies; Cake-tier
    /// code such as tools and Cake extensions reads <c>ICakePlatform.Family</c> directly.
    /// </summary>
    public RuntimeFamily Family { get; }

    /// <summary>
    /// Whether the binary at <paramref name="fileName"/> matches one of the OS-family
    /// system-DLL / shared-object exclusion patterns defined in
    /// <c>manifest.json system_exclusions</c>. Callers that hold a Cake <c>FilePath</c>
    /// should pass <c>path.GetFilename().FullPath</c> here — this method takes a plain
    /// file name to keep the runtime-profile surface Cake-decoupled.
    /// </summary>
    public bool IsSystemFile(string fileName);
}
