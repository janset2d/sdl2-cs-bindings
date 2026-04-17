using Build.Modules.Packaging.Models;
using OneOf;
using OneOf.Monads;
using OneOf.Types;

namespace Build.Modules.Packaging.Results;

/// <summary>
/// Result monad for <c>IPackageVersionResolver.Resolve</c>:
/// <list type="bullet">
///   <item><term>Success</term><description><see cref="PackageVersion"/> — normalized NuGet SemVer</description></item>
///   <item><term>Error</term><description><see cref="PackageVersionResolutionError"/> — missing or invalid input</description></item>
/// </list>
/// </summary>
public sealed class PackageVersionResolutionResult(OneOf<Error<PackageVersionResolutionError>, Success<PackageVersion>> result)
    : Result<PackageVersionResolutionError, PackageVersion>(result)
{
    public static implicit operator PackageVersionResolutionResult(PackageVersionResolutionError error) => new(new Error<PackageVersionResolutionError>(error));
    public static implicit operator PackageVersionResolutionResult(PackageVersion version) => new(new Success<PackageVersion>(version));

    public static explicit operator PackageVersionResolutionError(PackageVersionResolutionResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return result.AsT0.Value;
    }

    public static explicit operator PackageVersion(PackageVersionResolutionResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return result.AsT1.Value;
    }

    public static PackageVersionResolutionResult FromPackageVersionResolutionError(PackageVersionResolutionError error) => error;
    public static PackageVersionResolutionResult FromPackageVersion(PackageVersion version) => version;

    public static PackageVersionResolutionError ToPackageVersionResolutionError(PackageVersionResolutionResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return result.AsT0.Value;
    }

    public static PackageVersion ToPackageVersion(PackageVersionResolutionResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return result.AsT1.Value;
    }

    public PackageVersion PackageVersion => SuccessValue();

    public PackageVersionResolutionError PackageVersionResolutionError => AsT0.Value;
}
