using System.Collections.Immutable;
using Build.Context.Models;
using Build.Domain.Preflight;
using Build.Domain.Preflight.Results;
using Build.Domain.Strategy;

namespace Build.Tests.Unit.Domain.Preflight;

public sealed class StrategyCoherenceValidatorTests
{
    [Test]
    public async Task Validate_Should_Return_Success_When_Runtime_Strategy_And_Triplet_Are_Coherent()
    {
        var validator = new StrategyCoherenceValidator(new StrategyResolver());
        var runtimes = ImmutableList.Create(
            new RuntimeInfo
            {
                Rid = "win-x64",
                Triplet = "x64-windows-hybrid",
                Strategy = "hybrid-static",
                Runner = "windows-latest",
                ContainerImage = null,
            });

        var result = validator.Validate(runtimes);

        await Assert.That(result.IsSuccess()).IsTrue();
        await Assert.That(result.Validation.HasErrors).IsFalse();
    }

    [Test]
    public async Task Validate_Should_Return_Error_When_Runtime_Strategy_And_Triplet_Are_Incoherent()
    {
        var validator = new StrategyCoherenceValidator(new StrategyResolver());
        var runtimes = ImmutableList.Create(
            new RuntimeInfo
            {
                Rid = "win-x64",
                Triplet = "x64-windows-hybrid",
                Strategy = "pure-dynamic",
                Runner = "windows-latest",
                ContainerImage = null,
            });

        var result = validator.Validate(runtimes);

        await Assert.That(result.IsError()).IsTrue();
        await Assert.That(result.StrategyCoherenceError).IsTypeOf<StrategyCoherenceError>();
        await Assert.That(result.Validation.HasErrors).IsTrue();
    }
}