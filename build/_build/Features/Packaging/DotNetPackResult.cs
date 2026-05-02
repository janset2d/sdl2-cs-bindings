using Build.Shared.Results;
using OneOf;
using OneOf.Monads;
using OneOf.Types;

namespace Build.Features.Packaging;

/// <summary>
/// Result monad for <c>IDotNetPackInvoker.Pack</c>:
/// <list type="bullet">
///   <item><term>Success</term><description><see cref="Unit"/> — pack completed, artifacts are in <c>artifacts/packages</c></description></item>
///   <item><term>Error</term><description><see cref="DotNetPackError"/> — underlying <c>dotnet pack</c> failed</description></item>
/// </list>
/// </summary>
public sealed class DotNetPackResult(OneOf<Error<DotNetPackError>, Success<Unit>> result) : Result<DotNetPackError, Unit>(result)
{
    public static implicit operator DotNetPackResult(DotNetPackError error) => new(new Error<DotNetPackError>(error));
    public static implicit operator DotNetPackResult(Unit unit) => new(new Success<Unit>(unit));

    public static explicit operator DotNetPackError(DotNetPackResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return result.AsT0.Value;
    }

    public static explicit operator Unit(DotNetPackResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return result.AsT1.Value;
    }

    public static DotNetPackResult FromDotNetPackError(DotNetPackError error) => error;
    public static DotNetPackResult FromUnit(Unit unit) => unit;

    public static DotNetPackError ToDotNetPackError(DotNetPackResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return result.AsT0.Value;
    }

    public static Unit ToUnit(DotNetPackResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return result.AsT1.Value;
    }

    public static DotNetPackResult ToSuccess() => Unit.Value;

    public DotNetPackError DotNetPackError => AsT0.Value;
}
