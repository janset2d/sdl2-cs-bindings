using Build.Results;

namespace Build.Vcpkg;

public sealed class PackageInfoError : BuildError
{
    public PackageInfoError(string message, Exception? exception = null)
        : base(message, exception)
    {
    }
}
