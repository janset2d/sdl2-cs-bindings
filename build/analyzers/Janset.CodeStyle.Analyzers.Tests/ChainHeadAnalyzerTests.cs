using Build.Tests.Fixtures.CodeStyle;
using TUnit.Assertions.Enums;

namespace Janset.CodeStyle.Analyzers.Tests;

internal sealed class ChainHeadAnalyzerTests
{
    [Test]
    public async Task ChainHeadAnalyzer_Should_Report_When_First_Call_Stays_On_A_Wrapped_Head()
    {
        var diagnostics = await AnalyzerCompilationFixture.AnalyzeAsync(
            ChainHeadData.WrappedInstance, new ChainHeadAnalyzer(), CancellationToken.None);

        await Assert.That(diagnostics)
            .IsEquivalentTo(ChainHeadData.WrappedDiagnostics, CollectionOrdering.Matching);
    }

    [Test]
    public async Task ChainHeadAnalyzer_Should_Not_Report_When_Chain_Fits_One_Line()
    {
        var diagnostics = await AnalyzerCompilationFixture.AnalyzeAsync(
            ChainHeadData.SingleLine, new ChainHeadAnalyzer(), CancellationToken.None);

        await Assert.That(diagnostics).IsEmpty();
    }

    [Test]
    public async Task ChainHeadAnalyzer_Should_Not_Report_When_Head_Is_A_Type_Qualified_Static_Call()
    {
        var diagnostics = await AnalyzerCompilationFixture.AnalyzeAsync(
            ChainHeadData.StaticHead, new ChainHeadAnalyzer(), CancellationToken.None);

        await Assert.That(diagnostics).IsEmpty();
    }

    [Test]
    public async Task ChainHeadAnalyzer_Should_Not_Report_In_Generated_Code()
    {
        var diagnostics = await AnalyzerCompilationFixture.AnalyzeAsync(
            ChainHeadData.Generated, new ChainHeadAnalyzer(), CancellationToken.None);

        await Assert.That(diagnostics).IsEmpty();
    }

    [Test]
    public async Task ChainHeadAnalyzer_Should_Report_When_Receiver_Is_An_Invocation()
    {
        var diagnostics = await AnalyzerCompilationFixture.AnalyzeAsync(
            ChainHeadData.WrappedInvocation, new ChainHeadAnalyzer(), CancellationToken.None);

        await Assert.That(diagnostics)
            .IsEquivalentTo(ChainHeadData.InvocationDiagnostics, CollectionOrdering.Matching);
    }
}
