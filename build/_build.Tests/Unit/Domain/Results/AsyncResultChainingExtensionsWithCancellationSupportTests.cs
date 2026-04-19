#pragma warning disable VSTHRD003

using Build.Domain.Results;
using OneOf.Monads;

namespace Build.Tests.Unit.Domain.Results;

public sealed class AsyncResultChainingExtensionsWithCancellationSupportTests
{
    [Test]
    public async Task Then_Should_Throw_When_Cancellation_Is_Requested_And_ThrowOnCancellation_Is_True()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var previous = Task.FromResult(Result<string, int>.Success(1));

        await Assert.That(async () =>
        {
            _ = await previous.Then(
                (value, _) => Task.FromResult(Result<string, int>.Success(value + 1)),
                cts.Token,
                throwOnCancellation: true);
        }).Throws<OperationCanceledException>();
    }

    [Test]
    public async Task Then_Should_Continue_When_Canceled_But_ThrowOnCancellation_Is_False()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var nextCalls = 0;
        var previous = Task.FromResult(Result<string, int>.Success(3));

        var result = await previous.Then(
            (value, _) =>
            {
                nextCalls++;
                return Task.FromResult(Result<string, int>.Success(value + 1));
            },
            cts.Token,
            throwOnCancellation: false);

        await Assert.That(nextCalls).IsEqualTo(1);
        await Assert.That(result.IsSuccess()).IsTrue();
        await Assert.That(result.SuccessValue()).IsEqualTo(4);
    }

    [Test]
    public async Task ThenWaitForAll_Should_ShortCircuit_When_Previous_Result_Is_Error()
    {
        var taskCalls = 0;

        var result = await Task.FromResult(Result<string, int>.Error("previous-error"))
            .ThenWaitForAll(
                CancellationToken.None,
                throwOnCancellation: true,
                (_, _) =>
                {
                    taskCalls++;
                    return Task.FromResult(Result<string, int>.Success(1));
                });

        await Assert.That(taskCalls).IsEqualTo(0);
        await Assert.That(result.IsError()).IsTrue();
        await Assert.That(result.ErrorValue()).IsEqualTo("previous-error");
    }

    [Test]
    public async Task IfThen_Should_Skip_NextJob_When_Condition_Is_False()
    {
        var nextCalls = 0;

        var result = await Task.FromResult(Result<string, int>.Success(7))
            .IfThen(
                (_, _) => false,
                (value, _) =>
                {
                    nextCalls++;
                    return Task.FromResult(Result<string, int>.Success(value + 1));
                },
                CancellationToken.None);

        await Assert.That(nextCalls).IsEqualTo(0);
        await Assert.That(result.IsSuccess()).IsTrue();
        await Assert.That(result.SuccessValue()).IsEqualTo(7);
    }
}
