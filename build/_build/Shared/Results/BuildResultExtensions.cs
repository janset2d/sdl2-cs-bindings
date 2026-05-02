using OneOf.Monads;

namespace Build.Shared.Results;

public static class BuildResultExtensions
{
    public static Result<TError, TSuccess> ToResult<TError, TSuccess>(this Result<TError, TSuccess> self)
    {
        ArgumentNullException.ThrowIfNull(self);
        return self;
    }

    public static void OnError<TError, TSuccess>(this Result<TError, TSuccess> result, Action<TError> errorHandler)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(errorHandler);

        if (result.IsError())
        {
            errorHandler(result.ErrorValue());
        }
    }
}
