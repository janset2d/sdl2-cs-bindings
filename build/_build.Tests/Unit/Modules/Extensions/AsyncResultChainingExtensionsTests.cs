#pragma warning disable VSTHRD003

using OneOf.Monads;

namespace Build.Tests.Unit.Modules.Extensions;

public sealed class AsyncResultChainingExtensionsTests
{
    [Test]
    public async Task Then_Should_ShortCircuit_When_Previous_Result_Is_Error()
    {
        var nextCalls = 0;

        var result = await Task.FromResult(Result<string, int>.Error("first-error"))
            .Then(value =>
            {
                nextCalls++;
                return Task.FromResult(Result<string, int>.Success(value + 1));
            });

        await Assert.That(nextCalls).IsEqualTo(0);
        await Assert.That(result.IsError()).IsTrue();
        await Assert.That(result.ErrorValue()).IsEqualTo("first-error");
    }

    [Test]
    public async Task ThenWaitForAll_Should_Return_First_Error_With_Default_Merge()
    {
        var result = await Task.FromResult(Result<string, int>.Success(10))
            .ThenWaitForAll(
                _ => Task.FromResult(Result<string, int>.Success(11)),
                _ => Task.FromResult(Result<string, int>.Error("merge-error")),
                _ => Task.FromResult(Result<string, int>.Success(12)));

        await Assert.That(result.IsError()).IsTrue();
        await Assert.That(result.ErrorValue()).IsEqualTo("merge-error");
    }

    [Test]
    public async Task ThenWaitForFirst_Should_Return_First_Completed_Task_Result()
    {
        var slowTask = new TaskCompletionSource<Result<string, int>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var fastTask = new TaskCompletionSource<Result<string, int>>(TaskCreationOptions.RunContinuationsAsynchronously);

        var pending = Task.FromResult(Result<string, int>.Success(5))
            .ThenWaitForFirst(
                _ => slowTask.Task,
                _ => fastTask.Task);

        fastTask.SetResult(Result<string, int>.Success(99));

        var result = await pending;

        await Assert.That(result.IsSuccess()).IsTrue();
        await Assert.That(result.SuccessValue()).IsEqualTo(99);
    }

    [Test]
    public async Task BindAsync_Should_Propagate_Error_Without_Calling_Binder()
    {
        var binderCalls = 0;

        var result = await Task.FromResult(Result<string, int>.Error("bind-error"))
            .BindAsync(value =>
            {
                binderCalls++;
                return Task.FromResult(Result<string, int>.Success(value + 1));
            });

        await Assert.That(binderCalls).IsEqualTo(0);
        await Assert.That(result.IsError()).IsTrue();
        await Assert.That(result.ErrorValue()).IsEqualTo("bind-error");
    }
}
