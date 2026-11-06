using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Janset.CodeStyle.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ChainHeadAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "JNS0001";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Wrap the first call of a wrapped call chain",
        "Wrap the first call of a wrapped call chain",
        "Style",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.InvocationExpression);
    }

    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        if (invocation.Parent is MemberAccessExpressionSyntax { Parent: InvocationExpressionSyntax })
        {
            return;
        }

        var calls = new List<MemberAccessExpressionSyntax>();

        while (invocation.Expression is MemberAccessExpressionSyntax access)
        {
            calls.Add(access);

            if (access.Expression is not InvocationExpressionSyntax previous)
            {
                break;
            }

            invocation = previous;
        }

        if (calls.Count < 2)
        {
            return;
        }

        calls.Reverse();
        var first = calls[0];
        var firstLine = first.OperatorToken.GetLocation().GetLineSpan().StartLinePosition.Line;
        var lastLine = calls[calls.Count - 1].OperatorToken.GetLocation().GetLineSpan().StartLinePosition.Line;

        if (firstLine == lastLine)
        {
            return;
        }

        if (context.SemanticModel.GetSymbolInfo(first.Expression, context.CancellationToken).Symbol is INamedTypeSymbol)
        {
            first = calls[1];
        }

        var receiverLine = first.Expression.GetLocation().GetLineSpan().EndLinePosition.Line;
        var callLine = first.OperatorToken.GetLocation().GetLineSpan().StartLinePosition.Line;

        if (receiverLine == callLine)
        {
            context.ReportDiagnostic(Diagnostic.Create(Rule, first.OperatorToken.GetLocation()));
        }
    }
}
