// Result‑based async chaining helpers converted from OneOf.Chaining
// ---------------------------------------------------------------
// Every extension operates on Task<Result<TError, TSuccess>> and short‑circuits on the
// first Error (propagating it unchanged).  All awaits use ConfigureAwait(false)
// to avoid capturing a synchronization context (satisfying VSTHRD003 analyzers).
// ---------------------------------------------------------------
#pragma warning disable VSTHRD003 // foreign-task awaits are intentional and safe here
#pragma warning disable IDE0130 // Namespace "OneOf.Monads" does not match folder structure, expected "Build.Modules.Extensions"

namespace OneOf.Monads;

/// <summary>
/// Fluent, English-like async chains for <see cref="Result{TError,TSuccess}"/>.
/// All helpers short-circuit on the first <c>Error</c> value.
/// <para>
/// <b>Note</b> – Only method declarations are present; implementation bodies were
/// trimmed for documentation brevity.
/// </para>
/// </summary>
public static class AsyncResultChainingExtensions
{
    /// <summary>
    /// Chains an asynchronous operation onto a prior successful result.
    /// </summary>
    /// <typeparam name="TError">Error type carried by the <see cref="Result{TError,TSuccess}"/>.</typeparam>
    /// <typeparam name="TSuccess">Success payload type.</typeparam>
    /// <param name="previousJob">An asynchronous <see cref="Result{TError,TSuccess}"/>.</param>
    /// <param name="nextJob">Executed only when <paramref name="previousJob"/> succeeds.</param>
    /// <returns>The propagated <c>Error</c> or the <paramref name="nextJob"/> outcome.</returns>
    public static async Task<Result<TError, TSuccess>> Then<TError, TSuccess>(
        this Task<Result<TError, TSuccess>> previousJob,
        Func<TSuccess, Task<Result<TError, TSuccess>>> nextJob)
    {
        ArgumentNullException.ThrowIfNull(previousJob);
        ArgumentNullException.ThrowIfNull(nextJob);

        var first = await previousJob.ConfigureAwait(false);
        if (first.IsError())
        {
            return Result<TError, TSuccess>.Error(first.ErrorValue());
        }

        return await nextJob(first.SuccessValue()).ConfigureAwait(false);
    }

    /// <summary>
    /// Chains <paramref name="nextJob"/> and, upon its error result, executes <paramref name="onFailure"/>.
    /// </summary>
    /// <typeparam name="TError">Error type.</typeparam>
    /// <typeparam name="TSuccess">Success type.</typeparam>
    /// <param name="previousJob">The prior asynchronous result.</param>
    /// <param name="nextJob">Executed if <paramref name="previousJob"/> succeeded.</param>
    /// <param name="onFailure">Compensation logic when <paramref name="nextJob"/> fails.</param>
    /// <returns>The first <c>Error</c> or the final successful state.</returns>
    public static async Task<Result<TError, TSuccess>> Then<TError, TSuccess>(
        this Task<Result<TError, TSuccess>> previousJob,
        Func<TSuccess, Task<Result<TError, TSuccess>>> nextJob,
        Func<TSuccess, TError, Task<Result<TError, TSuccess>>> onFailure)
    {
        ArgumentNullException.ThrowIfNull(previousJob);
        ArgumentNullException.ThrowIfNull(nextJob);
        ArgumentNullException.ThrowIfNull(onFailure);

        var first = await previousJob.ConfigureAwait(false);
        if (first.IsError())
        {
            return Result<TError, TSuccess>.Error(first.ErrorValue());
        }

        var currentSuccess = first.SuccessValue();
        var second = await nextJob(currentSuccess).ConfigureAwait(false);
        if (second.IsSuccess())
        {
            return second;
        }

        return await onFailure(currentSuccess, second.ErrorValue()).ConfigureAwait(false);
    }

    /// <summary>
    /// Conditionally invokes <paramref name="nextJob"/> when <paramref name="condition"/> is <see langword="true"/>.
    /// </summary>
    /// <typeparam name="TError">Error type.</typeparam>
    /// <typeparam name="TSuccess">Success type.</typeparam>
    /// <param name="previousJob">The prior result.</param>
    /// <param name="condition">Predicate applied to the success payload.</param>
    /// <param name="nextJob">Operation executed when <paramref name="condition"/> passes.</param>
    /// <param name="onFailure">Optional compensation on failure.</param>
    /// <returns>The unchanged success, the propagated error, or the outcome of <paramref name="nextJob"/>.</returns>
    public static async Task<Result<TError, TSuccess>> IfThen<TError, TSuccess>(
        this Task<Result<TError, TSuccess>> previousJob,
        Func<TSuccess, bool> condition,
        Func<TSuccess, Task<Result<TError, TSuccess>>> nextJob,
        Func<TSuccess, TError, Task<Result<TError, TSuccess>>>? onFailure = null)
    {
        ArgumentNullException.ThrowIfNull(previousJob);
        ArgumentNullException.ThrowIfNull(condition);
        ArgumentNullException.ThrowIfNull(nextJob);

        var first = await previousJob.ConfigureAwait(false);
        if (first.IsError())
        {
            return Result<TError, TSuccess>.Error(first.ErrorValue());
        }

        var currentSuccess = first.SuccessValue();
        if (!condition(currentSuccess))
        {
            return currentSuccess; // skipped branch
        }

        var second = await nextJob(currentSuccess).ConfigureAwait(false);
        if (second.IsSuccess() || onFailure is null)
        {
            return second;
        }

        return await onFailure(currentSuccess, second.ErrorValue()).ConfigureAwait(false);
    }

    /// <summary>
    /// Loops over a collection derived from the success payload and applies <paramref name="taskForEach"/> to each item.
    /// Stops at the first error, optionally invoking <paramref name="onFailure"/>.
    /// </summary>
    /// <typeparam name="TError">Error type.</typeparam>
    /// <typeparam name="TSuccess">Success type.</typeparam>
    /// <typeparam name="TItem">Item type to iterate.</typeparam>
    /// <param name="previousJob">The prior result.</param>
    /// <param name="items">Selector producing a list of items.</param>
    /// <param name="taskForEach">Async operation executed per item.</param>
    /// <param name="onFailure">Optional compensation when an item execution fails.</param>
    /// <returns>The original success, a propagated error, or the last successful state.</returns>
    public static async Task<Result<TError, TSuccess>> ThenForEach<TError, TSuccess, TItem>(
        this Task<Result<TError, TSuccess>> previousJob,
        Func<TSuccess, IEnumerable<TItem>> items,
        Func<TSuccess, TItem, Task<Result<TError, TSuccess>>> taskForEach,
        Func<TSuccess, TError, Task<Result<TError, TSuccess>>>? onFailure = null)
    {
        ArgumentNullException.ThrowIfNull(previousJob);
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(taskForEach);

        var start = await previousJob.ConfigureAwait(false);
        if (start.IsError())
        {
            return Result<TError, TSuccess>.Error(start.ErrorValue());
        }

        var ctx = start.SuccessValue();
        foreach (var item in items(ctx))
        {
            var res = await taskForEach(ctx, item).ConfigureAwait(false);
            if (!res.IsError())
            {
                continue;
            }

            if (onFailure is null)
            {
                return res;
            }

            return await onFailure(ctx, res.ErrorValue()).ConfigureAwait(false);
        }
        return ctx;
    }

    /// <summary>
    /// Projects the success payload into a new type while preserving the error channel.
    /// </summary>
    /// <typeparam name="TError">Error type.</typeparam>
    /// <typeparam name="TSuccess">Original success type.</typeparam>
    /// <typeparam name="TResult">Projected success type.</typeparam>
    /// <param name="previousJob">The prior result.</param>
    /// <param name="projector">Transformation applied to the success payload.</param>
    /// <returns>A <see cref="Result{TError,TResult}"/> containing the projected success or the original error.</returns>
    public static async Task<Result<TError, TResult>> ToResult<TError, TSuccess, TResult>(
        this Task<Result<TError, TSuccess>> previousJob,
        Func<TSuccess, TResult> projector)
    {
        ArgumentNullException.ThrowIfNull(previousJob);
        ArgumentNullException.ThrowIfNull(projector);

        var r = await previousJob.ConfigureAwait(false);
        return r.IsSuccess()
            ? Result<TError, TResult>.Success(projector(r.SuccessValue()))
            : Result<TError, TResult>.Error(r.ErrorValue());
    }

    /// <summary>
    /// Executes several tasks in parallel and merges their outcomes using <paramref name="merge"/>.
    /// </summary>
    /// <typeparam name="TError">Error type.</typeparam>
    /// <typeparam name="TSuccess">Success type.</typeparam>
    /// <param name="previousJob">The prior result.</param>
    /// <param name="merge">User‑supplied merge strategy.</param>
    /// <param name="tasks">Task factory delegates executed in parallel.</param>
    /// <returns>A merged <see cref="Result{TError,TSuccess}"/> according to <paramref name="merge"/>.</returns>
    public static async Task<Result<TError, TSuccess>> ThenWaitForAll<TError, TSuccess>(
        this Task<Result<TError, TSuccess>> previousJob,
        Func<TSuccess, List<Result<TError, TSuccess>>, Result<TError, TSuccess>> merge,
        params Func<TSuccess, Task<Result<TError, TSuccess>>>[] tasks)
    {
        ArgumentNullException.ThrowIfNull(previousJob);
        ArgumentNullException.ThrowIfNull(merge);
        if (tasks is null || tasks.Length == 0)
        {
            throw new ArgumentException("At least one task delegate is required.", nameof(tasks));
        }

        var first = await previousJob.ConfigureAwait(false);
        if (first.IsError())
        {
            return first;
        }

        var ctx = first.SuccessValue();
        var taskArray = tasks.Select(t => t(ctx)).ToArray();
        var resultsArray = await Task.WhenAll(taskArray).ConfigureAwait(false);
        return merge(ctx, [.. resultsArray]);
    }

    /// <summary>
    /// Executes several tasks in parallel using a default merge strategy (first error wins; otherwise original success).
    /// </summary>
    /// <typeparam name="TError">Error type.</typeparam>
    /// <typeparam name="TSuccess">Success type.</typeparam>
    /// <param name="previousJob">The prior result.</param>
    /// <param name="tasks">Task factory delegates executed in parallel.</param>
    /// <returns>A merged <see cref="Result{TError,TSuccess}"/>.</returns>
    public static async Task<Result<TError, TSuccess>> ThenWaitForAll<TError, TSuccess>(
        this Task<Result<TError, TSuccess>> previousJob,
        params Func<TSuccess, Task<Result<TError, TSuccess>>>[] tasks)
    {
        ArgumentNullException.ThrowIfNull(previousJob);
        if (tasks is null || tasks.Length == 0)
        {
            throw new ArgumentException("At least one task delegate is required.", nameof(tasks));
        }

        return await previousJob.ThenWaitForAll(DefaultMerge, tasks).ConfigureAwait(false);

        static Result<TError, TSuccess> DefaultMerge(TSuccess ctx, List<Result<TError, TSuccess>> list)
            => list.FirstOrDefault(r => r.IsError()) ?? ctx;
    }

    /// <summary>
    /// Executes tasks in parallel and returns the result of the first to complete.
    /// </summary>
    /// <typeparam name="TError">Error type.</typeparam>
    /// <typeparam name="TSuccess">Success type.</typeparam>
    /// <param name="previousJob">The prior result.</param>
    /// <param name="tasks">Task factory delegates executed in parallel.</param>
    /// <returns>The first completed task’s <see cref="Result{TError,TSuccess}"/> or a propagated error.</returns>
    public static async Task<Result<TError, TSuccess>> ThenWaitForFirst<TError, TSuccess>(
        this Task<Result<TError, TSuccess>> previousJob,
        params Func<TSuccess, Task<Result<TError, TSuccess>>>[] tasks)
    {
        ArgumentNullException.ThrowIfNull(previousJob);
        if (tasks is null || tasks.Length == 0)
        {
            throw new ArgumentException("At least one task delegate is required.", nameof(tasks));
        }

        var first = await previousJob.ConfigureAwait(false);
        if (first.IsError())
        {
            return first;
        }

        var ctx = first.SuccessValue();
        var taskArray = tasks.Select(t => t(ctx)).ToArray();
        var winner = await await Task.WhenAny(taskArray).ConfigureAwait(false);
        return winner;
    }
}
