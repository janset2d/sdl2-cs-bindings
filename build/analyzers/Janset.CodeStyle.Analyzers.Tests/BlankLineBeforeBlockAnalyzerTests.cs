using Build.Tests.Fixtures.CodeStyle;
using TUnit.Assertions.Enums;

namespace Janset.CodeStyle.Analyzers.Tests;

internal sealed class BlankLineBeforeBlockAnalyzerTests
{
    [Test]
    [MethodDataSource(typeof(BlankLineBeforeBlockData), nameof(BlankLineBeforeBlockData.Blocks))]
    public async Task BlankLineBeforeBlockAnalyzer_Should_Report_When_Block_Statement_Follows_A_Statement(string source, string expected)
    {
        var diagnostics = await AnalyzerCompilationFixture.AnalyzeAsync(source, new BlankLineBeforeBlockAnalyzer(), CancellationToken.None);

        await Assert.That(diagnostics).IsEquivalentTo(new[] { expected }, CollectionOrdering.Matching);
    }

    [Test]
    [Arguments(BlankLineBeforeBlockData.FirstStatement)]
    public async Task BlankLineBeforeBlockAnalyzer_Should_Not_Report_For_The_First_Statement_Of_A_Block(string source)
    {
        var diagnostics = await AnalyzerCompilationFixture.AnalyzeAsync(source, new BlankLineBeforeBlockAnalyzer(), CancellationToken.None);

        await Assert.That(diagnostics).IsEmpty();
    }

    [Test]
    public async Task BlankLineBeforeBlockAnalyzer_Should_Not_Report_For_Else_Catch_Finally()
    {
        var diagnostics = await AnalyzerCompilationFixture.AnalyzeAsync(
            BlankLineBeforeBlockData.Continuations, new BlankLineBeforeBlockAnalyzer(), CancellationToken.None);

        await Assert.That(diagnostics).IsEmpty();
    }

    [Test]
    public async Task BlankLineBeforeBlockAnalyzer_Should_Not_Report_When_A_Blank_Line_Precedes()
    {
        var diagnostics = await AnalyzerCompilationFixture.AnalyzeAsync(
            BlankLineBeforeBlockData.BlankLine, new BlankLineBeforeBlockAnalyzer(), CancellationToken.None);

        await Assert.That(diagnostics).IsEmpty();
    }

    [Test]
    public async Task BlankLineBeforeBlockAnalyzer_Should_Not_Report_For_A_Using_Declaration()
    {
        var diagnostics = await AnalyzerCompilationFixture.AnalyzeAsync(
            BlankLineBeforeBlockData.UsingDeclaration, new BlankLineBeforeBlockAnalyzer(), CancellationToken.None);

        await Assert.That(diagnostics).IsEmpty();
    }

    [Test]
    public async Task BlankLineBeforeBlockAnalyzer_Should_Not_Report_In_Generated_Code()
    {
        var diagnostics = await AnalyzerCompilationFixture.AnalyzeAsync(
            BlankLineBeforeBlockData.Generated, new BlankLineBeforeBlockAnalyzer(), CancellationToken.None);

        await Assert.That(diagnostics).IsEmpty();
    }
}
