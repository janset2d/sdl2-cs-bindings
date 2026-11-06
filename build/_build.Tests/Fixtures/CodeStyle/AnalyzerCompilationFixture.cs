using System.Globalization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Build.Tests.Fixtures.CodeStyle;

internal static class AnalyzerCompilationFixture
{
    public static async Task<string[]> AnalyzeAsync(string source, DiagnosticAnalyzer analyzer, CancellationToken cancellationToken, string? additionalSource = null)
    {
        var tree = CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.CSharp14), path: "Sample.cs", cancellationToken: cancellationToken);
        var root = await tree.GetRootAsync(cancellationToken);
        var outputKind = root.ChildNodes().OfType<GlobalStatementSyntax>().Any()
            ? OutputKind.ConsoleApplication
            : OutputKind.DynamicallyLinkedLibrary;
        SyntaxTree[] trees = additionalSource is null
            ? [tree]
            : [tree, CSharpSyntaxTree.ParseText(additionalSource, new CSharpParseOptions(LanguageVersion.CSharp14), path: "Control.cs", cancellationToken: cancellationToken)];
        var compilation = CSharpCompilation.Create(
            "Sample",
            trees,
            [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)],
            new CSharpCompilationOptions(outputKind, allowUnsafe: true));
        var errors = compilation
            .GetDiagnostics(cancellationToken)
            .Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToArray();

        if (errors.Length > 0)
        {
            throw new InvalidOperationException(string.Join('\n', errors.Select(static diagnostic => diagnostic.ToString())));
        }

        var diagnostics = await compilation
            .WithAnalyzers([analyzer])
            .GetAnalyzerDiagnosticsAsync(cancellationToken);

        return [.. diagnostics
            .OrderBy(static diagnostic => diagnostic.Location.SourceSpan.Start)
            .ThenBy(static diagnostic => diagnostic.Id, StringComparer.Ordinal)
            .Select(static diagnostic => string.Create(CultureInfo.InvariantCulture,
                $"{diagnostic.Id}|{diagnostic.Location.SourceSpan.Start}|{diagnostic.GetMessage(CultureInfo.InvariantCulture)}"))];
    }
}
