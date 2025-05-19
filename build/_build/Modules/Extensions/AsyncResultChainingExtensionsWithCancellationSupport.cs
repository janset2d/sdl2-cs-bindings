// Result‑based async chaining helpers converted from OneOf.Chaining
// ---------------------------------------------------------------
// Every extension operates on Task<Result<TError, TSuccess>> and short‑circuits on the
// first Error (propagating it unchanged).  All awaits use ConfigureAwait(false)
// to avoid capturing a synchronization context (satisfying VSTHRD analyzers).
// ---------------------------------------------------------------
#pragma warning disable VSTHRD003 // foreign-task awaits are intentional and safe here
#pragma warning disable IDE0130 // Namespace "OneOf.Monads" does not match folder structure, expected "Build.Modules.Extensions"

namespace OneOf.Monads;

/// <summary>
/// Cancellation-aware async chaining helpers for <see cref="Result{TError,TSuccess}"/>.
/// Each method takes or returns <c>Task&lt;Result&lt;TError,TSuccess&gt;&gt;</c> short-circuiting on the first
/// <c>Error</c> value and honouring a <see cref="CancellationToken"/>.
/// </summary>
public static class AsyncResultChainingExtensionsWithCancellationSupport
{
    /// <summary>
    /// Chains an asynchronous operation onto a prior result, propagating the first <c>Error</c> and
    /// observing cancellation.
    /// </summary>
    /// <typeparam name="TError">Error type carried by the <see cref="Result{TError,TSuccess}"/>.</typeparam>
    /// <typeparam name="TSuccess">Success payload type.</typeparam>
    /// <param name="previousJob">The preceding asynchronous result.</param>
    /// <param name="nextJob">A function executed only if <paramref name="previousJob"/> completed successfully.</param>
    /// <param name="ct">A token to observe for cancellation.</param>
    /// <param name="throwOnCancellation">If <see langword="true"/>, throws when <paramref name="ct"/> is canceled; otherwise exits gracefully.</param>
    /// <returns>A new asynchronous result encapsulating either the next success or any propagated error.</returns>
    public static async Task<Result<TError, TSuccess>> Then<TError, TSuccess>(
        this Task<Result<TError, TSuccess>> previousJob,
        Func<TSuccess, CancellationToken, Task<Result<TError, TSuccess>>> nextJob,
        CancellationToken ct, bool throwOnCancellation = true)
    {
        ArgumentNullException.ThrowIfNull(previousJob);
        ArgumentNullException.ThrowIfNull(nextJob);
        if (throwOnCancellation)
        {
            ct.ThrowIfCancellationRequested();
        }

        var first = await previousJob.ConfigureAwait(false);
        if (first.IsError())
        {
            return Result<TError, TSuccess>.Error(first.ErrorValue());
        }

        return await nextJob(first.SuccessValue(), ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Chains <paramref name="nextJob"/> and, if it fails, invokes <paramref name="onFailure"/> for compensation.
    /// </summary>
    /// <typeparam name="TError">Error type carried by the <see cref="Result{TError,TSuccess}"/>.</typeparam>
    /// <typeparam name="TSuccess">Success payload type.</typeparam>
    /// <param name="previousJob">The preceding asynchronous result.</param>
    /// <param name="nextJob">Executed if <paramref name="previousJob"/> succeeded.</param>
    /// <param name="onFailure">Cleanup invoked when <paramref name="nextJob"/> returns an <c>Error</c>.</param>
    /// <param name="ct">Cancellation-token observed during both delegates.</param>
    /// <param name="throwOnCancellation">When <see langword="true"/>, throws on cancellation.</param>
    /// <returns>The first <c>Error</c> encountered or the final successful state.</returns>
    public static async Task<Result<TError, TSuccess>> Then<TError, TSuccess>(
        this Task<Result<TError, TSuccess>> previousJob,
        Func<TSuccess, CancellationToken, Task<Result<TError, TSuccess>>> nextJob,
        Func<TSuccess, TError, CancellationToken, Task<Result<TError, TSuccess>>> onFailure,
        CancellationToken ct, bool throwOnCancellation = true)
    {
        ArgumentNullException.ThrowIfNull(previousJob);
        ArgumentNullException.ThrowIfNull(nextJob);
        ArgumentNullException.ThrowIfNull(onFailure);
        if (throwOnCancellation)
        {
            ct.ThrowIfCancellationRequested();
        }

        var first = await previousJob.ConfigureAwait(false);
        if (first.IsError())
        {
            return Result<TError, TSuccess>.Error(first.ErrorValue());
        }

        var ctx = first.SuccessValue();
        var second = await nextJob(ctx, ct).ConfigureAwait(false);
        if (second.IsSuccess())
        {
            return second;
        }

        return await onFailure(ctx, second.ErrorValue(), ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Conditionally executes <paramref name="nextJob"/> based on <paramref name="condition"/>.
    /// </summary>
    /// <typeparam name="TError">Error type.</typeparam>
    /// <typeparam name="TSuccess">Success type.</typeparam>
    /// <param name="previousJob">The prior result.</param>
    /// <param name="condition">Predicate evaluated on the success payload.</param>
    /// <param name="nextJob">Operation executed when <paramref name="condition"/> is <see langword="true"/>.</param>
    /// <param name="ct">Cancellation‑token.</param>
    /// <param name="throwOnCancellation">Whether to throw on cancellation.</param>
    /// <param name="onFailure">Optional compensating action on failure.</param>
    /// <returns>The original success, the propagated error, or the outcome of <paramref name="nextJob"/>.</returns>
    public static async Task<Result<TError, TSuccess>> IfThen<TError, TSuccess>(
        this Task<Result<TError, TSuccess>> previousJob,
        Func<TSuccess, CancellationToken, bool> condition,
        Func<TSuccess, CancellationToken, Task<Result<TError, TSuccess>>> nextJob,
        CancellationToken ct, bool throwOnCancellation = true,
        Func<TSuccess, TError, CancellationToken, Task<Result<TError, TSuccess>>>? onFailure = null)
    {
        ArgumentNullException.ThrowIfNull(previousJob);
        ArgumentNullException.ThrowIfNull(condition);
        ArgumentNullException.ThrowIfNull(nextJob);
        if (throwOnCancellation)
        {
            ct.ThrowIfCancellationRequested();
        }

        var first = await previousJob.ConfigureAwait(false);
        if (first.IsError())
        {
            return Result<TError, TSuccess>.Error(first.ErrorValue());
        }

        var ctx = first.SuccessValue();
        if (!condition(ctx, ct))
        {
            return ctx;
        }

        var second = await nextJob(ctx, ct).ConfigureAwait(false);
        if (second.IsSuccess() || onFailure is null)
        {
            return second;
        }

        return await onFailure(ctx, second.ErrorValue(), ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Executes multiple tasks in parallel and returns once the <em>first</em> completes, canceling the rest.
    /// </summary>
    /// <typeparam name="TError">Error type.</typeparam>
    /// <typeparam name="TSuccess">Success type.</typeparam>
    /// <param name="previousJob">The prior result.</param>
    /// <param name="ct">Cancellation‑token linked to spawned tasks.</param>
    /// <param name="throwOnCancellation">Whether to throw when <paramref name="ct"/> is canceled.</param>
    /// <param name="tasks">Task factory delegates executed in parallel.</param>
    /// <returns>The first completed task’s result, or a propagated error.</returns>
    public static async Task<Result<TError, TSuccess>> ThenWaitForFirst<TError, TSuccess>(
        this Task<Result<TError, TSuccess>> previousJob,
        CancellationToken ct, bool throwOnCancellation = true,
        params Func<TSuccess, CancellationToken, Task<Result<TError, TSuccess>>>[] tasks)
    {
        ArgumentNullException.ThrowIfNull(previousJob);
        if (tasks is null || tasks.Length == 0)
        {
            throw new ArgumentException("At least one task delegate is required.", nameof(tasks));
        }

        if (throwOnCancellation)
        {
            ct.ThrowIfCancellationRequested();
        }

        var first = await previousJob.ConfigureAwait(false);
        if (first.IsError())
        {
            return first;
        }

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var ctx = first.SuccessValue();
        var running = tasks.Select(t => t(ctx, linkedCts.Token));
        var winner = await (await Task.WhenAny(running).ConfigureAwait(false)).ConfigureAwait(false);
        await linkedCts.CancelAsync();
        return winner;
    }

    /// <summary>
    /// Runs tasks in parallel and merges outcomes using a default strategy where the first error wins.
    /// </summary>
    /// <typeparam name="TError">Error type.</typeparam>
    /// <typeparam name="TSuccess">Success type.</typeparam>
    /// <param name="previousJob">The prior result.</param>
    /// <param name="ct">Cancellation‑token.</param>
    /// <param name="throwOnCancellation">Whether to throw on cancellation.</param>
    /// <param name="tasks">Task factory delegates executed in parallel.</param>
    /// <returns>A merged <see cref="Result{TError,TSuccess}"/>.</returns>
    public static async Task<Result<TError, TSuccess>> ThenWaitForAll<TError, TSuccess>(
        this Task<Result<TError, TSuccess>> previousJob,
        CancellationToken ct, bool throwOnCancellation = true,
        params Func<TSuccess, CancellationToken, Task<Result<TError, TSuccess>>>[] tasks)
    {
        ArgumentNullException.ThrowIfNull(previousJob);
        if (tasks is null || tasks.Length == 0)
        {
            throw new ArgumentException("At least one task delegate is required.", nameof(tasks));
        }

        return await previousJob.ThenWaitForAll(DefaultMerge, ct, throwOnCancellation, tasks).ConfigureAwait(false);

        static Result<TError, TSuccess> DefaultMerge(TSuccess ctx, CancellationToken _, List<Result<TError, TSuccess>> results)
            => results.FirstOrDefault(r => r.IsError()) ?? ctx;
    }

    /// <summary>
    /// Runs tasks in parallel and merges outcomes via a caller‑supplied <paramref name="merge"/> delegate.
    /// </summary>
    /// <typeparam name="TError">Error type.</typeparam>
    /// <typeparam name="TSuccess">Success type.</typeparam>
    /// <param name="previousJob">The prior result.</param>
    /// <param name="merge">Function combining the context value and aggregated task results.</param>
    /// <param name="ct">Cancellation‑token.</param>
    /// <param name="throwOnCancellation">Whether to throw on cancellation.</param>
    /// <param name="tasks">Task factory delegates executed in parallel.</param>
    /// <returns>The merged <see cref="Result{TError,TSuccess}"/> according to <paramref name="merge"/>.</returns>
    public static async Task<Result<TError, TSuccess>> ThenWaitForAll<TError, TSuccess>(
        this Task<Result<TError, TSuccess>> previousJob,
        Func<TSuccess, CancellationToken, List<Result<TError, TSuccess>>, Result<TError, TSuccess>> merge,
        CancellationToken ct, bool throwOnCancellation = true,
        params Func<TSuccess, CancellationToken, Task<Result<TError, TSuccess>>>[] tasks)
    {
        ArgumentNullException.ThrowIfNull(previousJob);
        ArgumentNullException.ThrowIfNull(merge);
        if (tasks is null || tasks.Length == 0)
        {
            throw new ArgumentException("At least one task delegate is required.", nameof(tasks));
        }

        if (throwOnCancellation)
        {
            ct.ThrowIfCancellationRequested();
        }

        var first = await previousJob.ConfigureAwait(false);
        if (first.IsError())
        {
            return first;
        }

        var ctx = first.SuccessValue();
        var results = new List<Result<TError, TSuccess>>();
        await Task.WhenAll(tasks.Select(async t => results.Add(await t(ctx, ct).ConfigureAwait(false)))).ConfigureAwait(false);
        return merge(ctx, ct, results);
    }
}
