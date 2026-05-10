using Build.Results;

namespace Build.Tools.Vcpkg;

public sealed class VcpkgPackageInfoError : BuildError
{
    public VcpkgPackageInfoError(string message, Exception? exception = null)
        : base(message, exception)
    {
    }
}
