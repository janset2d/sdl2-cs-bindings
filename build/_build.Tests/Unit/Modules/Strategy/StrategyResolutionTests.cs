using Build.Context.Models;
using Build.Modules.Strategy;
using Build.Modules.Strategy.Models;

namespace Build.Tests.Unit.Modules.Strategy;

public sealed class StrategyResolutionTests
{
    [Test]
    [Arguments("x64-windows-hybrid", "hybrid-static", PackagingModel.HybridStatic)]
    [Arguments("x64-linux-hybrid", "hybrid-static", PackagingModel.HybridStatic)]
    [Arguments("x64-osx-hybrid", "hybrid-static", PackagingModel.HybridStatic)]
    [Arguments("arm64-windows", "pure-dynamic", PackagingModel.PureDynamic)]
    [Arguments("arm64-linux-dynamic", "pure-dynamic", PackagingModel.PureDynamic)]
    [Arguments("arm64-osx-dynamic", "pure-dynamic", PackagingModel.PureDynamic)]
    public async Task ResolveStrategy_Should_Return_Correct_Model_When_Triplet_And_Field_Are_Aligned(
        string triplet, string strategyField, PackagingModel expected)
    {
        var runtime = new RuntimeInfo
        {
            Rid = "test-rid",
            Triplet = triplet,
            Strategy = strategyField,
            Runner = "test-runner",
        };

        var result = StrategyResolver.Resolve(runtime);

        await Assert.That(result).IsEqualTo(expected);
    }

    [Test]
    public async Task ResolveStrategy_Should_Throw_When_Hybrid_Triplet_Has_PureDynamic_Strategy()
    {
        var runtime = new RuntimeInfo
        {
            Rid = "win-x64",
            Triplet = "x64-windows-hybrid",
            Strategy = "pure-dynamic",
            Runner = "windows-latest",
        };

        await Assert.That(() => StrategyResolver.Resolve(runtime))
            .Throws<InvalidOperationException>();
    }

    [Test]
    public async Task ResolveStrategy_Should_Throw_When_NonHybrid_Triplet_Has_HybridStatic_Strategy()
    {
        var runtime = new RuntimeInfo
        {
            Rid = "win-arm64",
            Triplet = "arm64-windows",
            Strategy = "hybrid-static",
            Runner = "windows-latest",
        };

        await Assert.That(() => StrategyResolver.Resolve(runtime))
            .Throws<InvalidOperationException>();
    }

    [Test]
    public async Task ResolveStrategy_Should_Throw_When_Strategy_Field_Is_Unknown()
    {
        var runtime = new RuntimeInfo
        {
            Rid = "win-x64",
            Triplet = "x64-windows-hybrid",
            Strategy = "unknown-strategy",
            Runner = "windows-latest",
        };

        await Assert.That(() => StrategyResolver.Resolve(runtime))
            .Throws<InvalidOperationException>();
    }

    [Test]
    public async Task ResolveStrategy_Should_Throw_When_Strategy_Field_Is_Null()
    {
        var runtime = new RuntimeInfo
        {
            Rid = "win-x64",
            Triplet = "x64-windows-hybrid",
            Strategy = null,
            Runner = "windows-latest",
        };

        await Assert.That(() => StrategyResolver.Resolve(runtime))
            .Throws<InvalidOperationException>();
    }

    [Test]
    [Arguments("arm64-windows", "pure-dynamic")]
    [Arguments("x86-windows", "pure-dynamic")]
    public async Task ResolveStrategy_Should_Accept_Stock_Triplets_With_PureDynamic(string triplet, string strategyField)
    {
        var runtime = new RuntimeInfo
        {
            Rid = "test-rid",
            Triplet = triplet,
            Strategy = strategyField,
            Runner = "test-runner",
        };

        var result = StrategyResolver.Resolve(runtime);

        await Assert.That(result).IsEqualTo(PackagingModel.PureDynamic);
    }

    [Test]
    public async Task ResolveStrategy_Should_Throw_When_Stock_Triplet_Has_HybridStatic_Strategy()
    {
        var runtime = new RuntimeInfo
        {
            Rid = "win-arm64",
            Triplet = "arm64-windows",
            Strategy = "hybrid-static",
            Runner = "windows-latest",
        };

        await Assert.That(() => StrategyResolver.Resolve(runtime))
            .Throws<InvalidOperationException>();
    }
}
