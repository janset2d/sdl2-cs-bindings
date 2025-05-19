using Build.Modules.Harvesting.Models;
using OneOf;
using OneOf.Monads;
using OneOf.Types;

namespace Build.Modules.Harvesting.Results;

public sealed class PackageInfoResult(OneOf<Error<PackageInfoError>, Success<PackageInfo>> result) : Result<PackageInfoError, PackageInfo>(result)
{
    public static implicit operator PackageInfoResult(PackageInfoError error) => new(new Error<PackageInfoError>(error));
    public static implicit operator PackageInfoResult(PackageInfo packageInfo) => new(new Success<PackageInfo>(packageInfo));

    public static explicit operator PackageInfoError(PackageInfoResult _)
    {
        ArgumentNullException.ThrowIfNull(_);

        return _.AsT0.Value;
    }

    public static explicit operator PackageInfo(PackageInfoResult _)
    {
        ArgumentNullException.ThrowIfNull(_);

        return _.AsT1.Value;
    }

    public static PackageInfoResult FromPackageInfoError(PackageInfoError error) => error;
    public static PackageInfoResult FromPackageInfo(PackageInfo packageInfo) => packageInfo;

    public static PackageInfoError ToPackageInfoError(PackageInfoResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return result.AsT0.Value;
    }

    public static PackageInfo ToPackageInfo(PackageInfoResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return result.AsT1.Value;
    }

    public PackageInfo PackageInfo => AsT1.Value;
}
