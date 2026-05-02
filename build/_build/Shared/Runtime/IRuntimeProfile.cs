namespace Build.Shared.Runtime;

public interface IRuntimeProfile
{
    string Rid { get; }

    string Triplet { get; }

    /// <summary>
    /// Build-host-local OS family for this runtime profile, decoupled from Cake's
    /// <c>PlatformFamily</c>. Per ADR-004 §2.6 the Shared layer carries no Cake deps.
    /// Cake-tier code (Tools, Cake extensions) reads <c>ICakePlatform.Family</c> directly.
    /// </summary>
    RuntimeFamily Family { get; }

    /// <summary>
    /// Whether the binary at <paramref name="fileName"/> matches one of the OS-family
    /// system-DLL / shared-object exclusion patterns defined in
    /// <c>manifest.json system_exclusions</c>. Callers that hold a Cake <c>FilePath</c>
    /// should pass <c>path.GetFilename().FullPath</c> here — this method takes a plain
    /// file name to keep the Shared/ surface Cake-decoupled.
    /// </summary>
    bool IsSystemFile(string fileName);
}
