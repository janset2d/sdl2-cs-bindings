using Build.Tests.Fixtures.CodeStyle;
using TUnit.Assertions.Enums;

namespace Janset.CodeStyle.Analyzers.Tests;

internal sealed class BlankLineBeforeBlockAnalyzerTests
{
    [Test]
    [MethodDataSource(typeof(BlankLineBeforeBlockData), nameof(BlankLineBeforeBlockData.Blocks))]
    [MethodDataSource(typeof(BlankLineBeforeBlockData), nameof(BlankLineBeforeBlockData.BoundaryBlocks))]
    public async Task BlankLineBeforeBlockAnalyzer_Should_Report_When_Block_Statement_Follows_A_Statement(string source, string expected)
    {
        var diagnostics = await AnalyzerCompilationFixture.AnalyzeAsync(source, new BlankLineBeforeBlockAnalyzer(), CancellationToken.None);

        await Assert.That(diagnostics).IsEquivalentTo(new[] { expected }, CollectionOrdering.Matching);
    }

    [Test]
    public async Task BlankLineBeforeBlockAnalyzer_Should_Not_Report_For_The_First_Statement_Of_A_Block()
    {
        var source = BlankLineBeforeBlockData.WithControl(BlankLineBeforeBlockData.FirstStatement);
        var diagnostics = await AnalyzerCompilationFixture.AnalyzeAsync(source, new BlankLineBeforeBlockAnalyzer(), CancellationToken.None);

        await Assert.That(diagnostics).IsEquivalentTo(BlankLineBeforeBlockData.ControlDiagnostics(source), CollectionOrdering.Matching);
    }

    [Test]
    public async Task BlankLineBeforeBlockAnalyzer_Should_Not_Report_For_Else_Catch_Finally()
    {
        var source = BlankLineBeforeBlockData.WithControl(BlankLineBeforeBlockData.Continuations);
        var diagnostics = await AnalyzerCompilationFixture.AnalyzeAsync(source, new BlankLineBeforeBlockAnalyzer(), CancellationToken.None);

        await Assert.That(diagnostics).IsEquivalentTo(BlankLineBeforeBlockData.ControlDiagnostics(source), CollectionOrdering.Matching);
    }

    [Test]
    public async Task BlankLineBeforeBlockAnalyzer_Should_Not_Report_When_A_Blank_Line_Precedes()
    {
        var source = BlankLineBeforeBlockData.WithControl(BlankLineBeforeBlockData.BlankLine);
        var diagnostics = await AnalyzerCompilationFixture.AnalyzeAsync(source, new BlankLineBeforeBlockAnalyzer(), CancellationToken.None);

        await Assert.That(diagnostics).IsEquivalentTo(BlankLineBeforeBlockData.ControlDiagnostics(source), CollectionOrdering.Matching);
    }

    [Test]
    public async Task BlankLineBeforeBlockAnalyzer_Should_Not_Report_For_A_Using_Declaration()
    {
        var source = BlankLineBeforeBlockData.WithControl(BlankLineBeforeBlockData.UsingDeclaration);
        var diagnostics = await AnalyzerCompilationFixture.AnalyzeAsync(source, new BlankLineBeforeBlockAnalyzer(), CancellationToken.None);

        await Assert.That(diagnostics).IsEquivalentTo(BlankLineBeforeBlockData.ControlDiagnostics(source), CollectionOrdering.Matching);
    }

    [Test]
    public async Task BlankLineBeforeBlockAnalyzer_Should_Not_Report_In_Generated_Code()
    {
        var diagnostics = await AnalyzerCompilationFixture.AnalyzeAsync(
            BlankLineBeforeBlockData.Generated, new BlankLineBeforeBlockAnalyzer(), CancellationToken.None, BlankLineBeforeBlockData.PositiveControl);

        await Assert.That(diagnostics)
            .IsEquivalentTo(BlankLineBeforeBlockData.ControlDiagnostics(BlankLineBeforeBlockData.PositiveControl), CollectionOrdering.Matching);
    }

    [Test]
    [MethodDataSource(typeof(BlankLineBeforeBlockData), nameof(BlankLineBeforeBlockData.BoundaryExemptions))]
    public async Task BlankLineBeforeBlockAnalyzer_Should_Report_Only_The_Control_For_Exempt_Boundaries(string exempt)
    {
        var source = BlankLineBeforeBlockData.WithControl(exempt);
        var diagnostics = await AnalyzerCompilationFixture.AnalyzeAsync(source, new BlankLineBeforeBlockAnalyzer(), CancellationToken.None);

        await Assert.That(diagnostics).IsEquivalentTo(BlankLineBeforeBlockData.ControlDiagnostics(source), CollectionOrdering.Matching);
    }
}
