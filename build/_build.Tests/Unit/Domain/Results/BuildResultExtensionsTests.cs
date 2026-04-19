using Build.Domain.Results;
using Build.Domain.Strategy.Models;
using Build.Domain.Strategy.Results;

namespace Build.Tests.Unit.Domain.Results;

public sealed class BuildResultExtensionsTests
{
    [Test]
    public async Task OnError_Should_Invoke_Handler_When_Result_Is_Error()
    {
        var calls = 0;
        ValidationError? handledError = null;
        var result = ValidationResult.Fail([], "boom");

        result.OnError(error =>
        {
            calls++;
            handledError = error;
        });

        await Assert.That(calls).IsEqualTo(1);
        await Assert.That(handledError).IsNotNull();
        await Assert.That(handledError!.Message).IsEqualTo("boom");
    }

    [Test]
    public async Task OnError_Should_Not_Invoke_Handler_When_Result_Is_Success()
    {
        var calls = 0;
        var result = ValidationResult.Pass(ValidationMode.Strict);

        result.OnError(_ => calls++);

        await Assert.That(calls).IsEqualTo(0);
    }

    [Test]
    public async Task ToResult_Should_Return_Generic_Result_When_Wrapper_Is_Converted()
    {
        var result = ValidationResult.Pass(ValidationMode.Strict).ToResult();

        await Assert.That(result.IsSuccess()).IsTrue();
        await Assert.That(result.SuccessValue().Mode).IsEqualTo(ValidationMode.Strict);
    }
}
