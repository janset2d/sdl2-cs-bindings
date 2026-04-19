using Build.Domain.Packaging.Models;
using OneOf;
using OneOf.Monads;
using OneOf.Types;

namespace Build.Domain.Packaging.Results;

/// <summary>
/// Result monad for <c>IPackageFamilySelector.Select</c>:
/// <list type="bullet">
///   <item><term>Success</term><description><see cref="PackageFamilySelection"/> — topologically ordered family list</description></item>
///   <item><term>Error</term><description><see cref="PackageFamilySelectionError"/> — unknown / placeholder / cyclic family inputs</description></item>
/// </list>
/// </summary>
public sealed class PackageFamilySelectionResult(OneOf<Error<PackageFamilySelectionError>, Success<PackageFamilySelection>> result)
    : Result<PackageFamilySelectionError, PackageFamilySelection>(result)
{
    public static implicit operator PackageFamilySelectionResult(PackageFamilySelectionError error) => new(new Error<PackageFamilySelectionError>(error));
    public static implicit operator PackageFamilySelectionResult(PackageFamilySelection selection) => new(new Success<PackageFamilySelection>(selection));

    public static explicit operator PackageFamilySelectionError(PackageFamilySelectionResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return result.AsT0.Value;
    }

    public static explicit operator PackageFamilySelection(PackageFamilySelectionResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return result.AsT1.Value;
    }

    public static PackageFamilySelectionResult FromPackageFamilySelectionError(PackageFamilySelectionError error) => error;
    public static PackageFamilySelectionResult FromPackageFamilySelection(PackageFamilySelection selection) => selection;

    public static PackageFamilySelectionError ToPackageFamilySelectionError(PackageFamilySelectionResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return result.AsT0.Value;
    }

    public static PackageFamilySelection ToPackageFamilySelection(PackageFamilySelectionResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return result.AsT1.Value;
    }

    public PackageFamilySelection Selection => SuccessValue();

    public PackageFamilySelectionError PackageFamilySelectionError => AsT0.Value;
}
