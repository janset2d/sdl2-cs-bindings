using Build.Results;

namespace Build.Tests.Unit.Results;

/// <summary>
/// Tests for <see cref="Result{T, TError}"/> — boring typed result with throwing
/// accessors, factory methods, and TryGet helpers.
/// </summary>
public sealed class ResultTests
{
    [Test]
    public async Task Success_Should_Set_IsSuccess_True_And_Expose_Value()
    {
        var result = Result<int, string>.Success(42);

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value).IsEqualTo(42);
    }

    [Test]
    public async Task Success_Should_Throw_When_Error_Accessed()
    {
        var result = Result<int, string>.Success(42);

        await Assert.That(() => result.Error).Throws<InvalidOperationException>();
    }

    [Test]
    public async Task Failure_Should_Set_IsSuccess_False_And_Expose_Error()
    {
        var result = Result<int, string>.Failure("something broke");

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).IsEqualTo("something broke");
    }

    [Test]
    public async Task Failure_Should_Throw_When_Value_Accessed()
    {
        var result = Result<int, string>.Failure("err");

        await Assert.That(() => result.Value).Throws<InvalidOperationException>();
    }

    [Test]
    public async Task IsFailure_Should_Mirror_Negation_Of_IsSuccess()
    {
        var ok = Result<int, string>.Success(1);
        var bad = Result<int, string>.Failure("err");

        await Assert.That(ok.IsFailure).IsFalse();
        await Assert.That(bad.IsFailure).IsTrue();
    }

    [Test]
    public async Task TryGetValue_Should_Return_True_And_Value_When_Success()
    {
        var result = Result<int, string>.Success(42);

        var ok = result.TryGetValue(out var value);

        await Assert.That(ok).IsTrue();
        await Assert.That(value).IsEqualTo(42);
    }

    [Test]
    public async Task TryGetValue_Should_Return_False_When_Failure()
    {
        var result = Result<int, string>.Failure("err");

        var ok = result.TryGetValue(out var value);

        await Assert.That(ok).IsFalse();
        await Assert.That(value).IsEqualTo(0);
    }

    [Test]
    public async Task TryGetError_Should_Return_True_And_Error_When_Failure()
    {
        var result = Result<int, string>.Failure("err");

        var ok = result.TryGetError(out var error);

        await Assert.That(ok).IsTrue();
        await Assert.That(error).IsEqualTo("err");
    }

    [Test]
    public async Task TryGetError_Should_Return_False_When_Success()
    {
        var result = Result<int, string>.Success(42);

        var ok = result.TryGetError(out var error);

        await Assert.That(ok).IsFalse();
        await Assert.That(error).IsNull();
    }

    [Test]
    public async Task Equals_Should_Be_True_When_Both_Success_With_Same_Value()
    {
        var a = Result<int, string>.Success(42);
        var b = Result<int, string>.Success(42);

        await Assert.That(a.Equals(b)).IsTrue();
        await Assert.That(a == b).IsTrue();
    }

    [Test]
    public async Task Default_Should_Be_Failure_With_Default_Error()
    {
        var result = default(Result<int, string>);

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.IsFailure).IsTrue();
        var ok = result.TryGetError(out var error);
        await Assert.That(ok).IsTrue();
        await Assert.That(error).IsNull();
    }
}
