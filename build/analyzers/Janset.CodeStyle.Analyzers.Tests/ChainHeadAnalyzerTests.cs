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

        await Assert.That(diagnostics)
            .IsEquivalentTo(ChainHeadData.ControlDiagnostics(ChainHeadData.SingleLine), CollectionOrdering.Matching);
    }

    [Test]
    public async Task ChainHeadAnalyzer_Should_Not_Report_When_Head_Is_A_Type_Qualified_Static_Call()
    {
        var diagnostics = await AnalyzerCompilationFixture.AnalyzeAsync(
            ChainHeadData.StaticHead, new ChainHeadAnalyzer(), CancellationToken.None);

        await Assert.That(diagnostics)
            .IsEquivalentTo(ChainHeadData.ControlDiagnostics(ChainHeadData.StaticHead), CollectionOrdering.Matching);
    }

    [Test]
    public async Task ChainHeadAnalyzer_Should_Not_Report_In_Generated_Code()
    {
        var diagnostics = await AnalyzerCompilationFixture.AnalyzeAsync(
            ChainHeadData.Generated, new ChainHeadAnalyzer(), CancellationToken.None, ChainHeadData.GeneratedControl);

        await Assert.That(diagnostics)
            .IsEquivalentTo(ChainHeadData.GeneratedDiagnostics, CollectionOrdering.Matching);
    }

    [Test]
    public async Task ChainHeadAnalyzer_Should_Report_When_Receiver_Is_An_Invocation()
    {
        var diagnostics = await AnalyzerCompilationFixture.AnalyzeAsync(
            ChainHeadData.WrappedInvocation, new ChainHeadAnalyzer(), CancellationToken.None);

        await Assert.That(diagnostics)
            .IsEquivalentTo(ChainHeadData.InvocationDiagnostics, CollectionOrdering.Matching);
    }

    [Test]
    public async Task ChainHeadAnalyzer_Should_Not_Report_When_Only_An_Argument_Wraps()
    {
        var diagnostics = await AnalyzerCompilationFixture.AnalyzeAsync(
            ChainHeadData.MultilineArgument, new ChainHeadAnalyzer(), CancellationToken.None);

        await Assert.That(diagnostics)
            .IsEquivalentTo(ChainHeadData.ControlDiagnostics(ChainHeadData.MultilineArgument), CollectionOrdering.Matching);
    }

    [Test]
    [MethodDataSource(typeof(ChainHeadData), nameof(ChainHeadData.WrappedLinks))]
    [MethodDataSource(typeof(ChainHeadData), nameof(ChainHeadData.Receivers))]
    public async Task ChainHeadAnalyzer_Should_Report_Through_Receiver_Links(string expression)
    {
        var diagnostics = await AnalyzerCompilationFixture.AnalyzeAsync(
            ChainHeadData.WithExpression(expression), new ChainHeadAnalyzer(), CancellationToken.None);

        await Assert.That(diagnostics)
            .IsEquivalentTo(ChainHeadData.ExpressionDiagnostics(expression), CollectionOrdering.Matching);
    }

    [Test]
    public async Task ChainHeadAnalyzer_Should_Not_Report_When_Type_Parameter_Is_A_Static_Head()
    {
        var diagnostics = await AnalyzerCompilationFixture.AnalyzeAsync(
            ChainHeadData.TypeParameterHead, new ChainHeadAnalyzer(), CancellationToken.None);

        await Assert.That(diagnostics)
            .IsEquivalentTo(ChainHeadData.ControlDiagnostics(ChainHeadData.TypeParameterHead), CollectionOrdering.Matching);
    }

    [Test]
    public async Task ChainHeadAnalyzer_Should_Report_Through_Conditional_Access()
    {
        var diagnostics = await AnalyzerCompilationFixture.AnalyzeAsync(
            ChainHeadData.ConditionalLink, new ChainHeadAnalyzer(), CancellationToken.None);

        await Assert.That(diagnostics)
            .IsEquivalentTo(ChainHeadData.ConditionalDiagnostics, CollectionOrdering.Matching);
    }

    [Test]
    public async Task ChainHeadAnalyzer_Should_Not_Report_When_Conditional_Calls_Wrap()
    {
        var diagnostics = await AnalyzerCompilationFixture.AnalyzeAsync(
            ChainHeadData.WrappedConditional, new ChainHeadAnalyzer(), CancellationToken.None);

        await Assert.That(diagnostics)
            .IsEquivalentTo(ChainHeadData.ControlDiagnostics(ChainHeadData.WrappedConditional), CollectionOrdering.Matching);
    }
}
