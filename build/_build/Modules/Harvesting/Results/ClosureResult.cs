using System.Diagnostics.CodeAnalysis;
using Build.Context.Models;
using Build.Modules.Harvesting.Models;
using Cake.Core.Diagnostics;
using OneOf;
using OneOf.Monads;
using OneOf.Types;

namespace Build.Modules.Harvesting.Results;

/// <summary>
/// Result monad variant of <see cref="ClosureResult"/>:
/// <list type="bullet">
///   <item><term>Success</term><description><see cref="BinaryClosure"/></description></item>
///   <item><term>Error</term><description><see cref="HarvestingError"/></description></item>
/// </list>
/// </summary>
public sealed class ClosureResult(OneOf<Error<HarvestingError>, Success<BinaryClosure>> result) : Result<HarvestingError, BinaryClosure>(result)
{
    public static implicit operator ClosureResult(HarvestingError error) => new(new Error<HarvestingError>(error));
    public static implicit operator ClosureResult(BinaryClosure closure) => new(new Success<BinaryClosure>(closure));

    public static explicit operator HarvestingError(ClosureResult _)
    {
        ArgumentNullException.ThrowIfNull(_);

        return _.AsT0.Value;
    }

    public static explicit operator BinaryClosure(ClosureResult _)
    {
        ArgumentNullException.ThrowIfNull(_);

        return _.AsT1.Value;
    }

    public static ClosureResult FromHarvestingError(HarvestingError error) => error;
    public static ClosureResult FromBinaryClosure(BinaryClosure closure) => closure;

    public static HarvestingError ToHarvestingError(ClosureResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return result.AsT0.Value;
    }

    public static BinaryClosure ToBinaryClosure(ClosureResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return result.AsT1.Value;
    }

    public BinaryClosure Closure => SuccessValue();
    public ClosureError AsClosureError() => (ClosureError)AsT0.Value;
}

public static class ClosureResultExtensions
{
    public static Result<HarvestingError, BinaryClosure> ToResult(this ClosureResult self) => self;

    [SuppressMessage("Usage", "VSTHRD003:Avoid awaiting foreign Tasks")]
    public static async Task<Result<HarvestingError, BinaryClosure>> ToResult(this Task<ClosureResult> self)
    {
        ArgumentNullException.ThrowIfNull(self);

        var cr = await self.ConfigureAwait(false);
        return cr;
    }

    public static bool LogError(this ClosureResult r, ICakeLog log, LibraryManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(r);
        ArgumentNullException.ThrowIfNull(log);
        ArgumentNullException.ThrowIfNull(manifest);

        if (!r.IsError())
        {
            return false;
        }

        var err = r.AsClosureError();

        switch (err)
        {
            case ClosureNotFound:
                log.Error("Library '{0}' not found in manifest. Skipping.", manifest.Name);
                break;
            default:
                log.Warning("Binary closure could not be resolved for '{0}'. Skipping.", manifest.Name);
                log.Verbose("Binary closure failed: {0}", err.Message);
                if (err.Exception != null)
                {
                    log.Verbose("Details: {0}", err.Exception);
                }

                break;
        }

        return true;
    }
}
