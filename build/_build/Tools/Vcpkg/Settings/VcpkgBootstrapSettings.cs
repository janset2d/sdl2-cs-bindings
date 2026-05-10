using Cake.Core.IO;

namespace Build.Tools.Vcpkg.Settings;

public sealed class VcpkgBootstrapSettings
{
    public required DirectoryPath VcpkgRoot { get; init; }

    public required FilePath WindowsScript { get; init; }

    public required FilePath UnixScript { get; init; }
}
