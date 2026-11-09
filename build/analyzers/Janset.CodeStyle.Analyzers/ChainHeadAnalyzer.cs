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

        var head = GetAccess(invocation);

        if (head is null || PreviousCall(head.Value.Receiver) is null)
        {
            return;
        }

        if (HasFollowingCall(invocation))
        {
            return;
        }

        var calls = new List<(ExpressionSyntax Receiver, SyntaxToken Dot)>();

        while (GetAccess(invocation) is { } access)
        {
            calls.Add(access);

            var previous = PreviousCall(access.Receiver);

            if (previous is null)
            {
                break;
            }

            invocation = previous;
        }

        if (calls.Count < 2)
        {
            return;
        }

        if (!calls.Any(static call => StartsLine(call.Dot)))
        {
            return;
        }

        var first = calls[calls.Count - 1];

        if (context.SemanticModel.GetSymbolInfo(first.Receiver, context.CancellationToken).Symbol is ITypeSymbol)
        {
            first = calls[calls.Count - 2];
        }

        var receiverLine = first.Receiver.GetLocation().GetLineSpan().EndLinePosition.Line;
        var callLine = first.Dot.GetLocation().GetLineSpan().StartLinePosition.Line;

        if (receiverLine == callLine)
        {
            context.ReportDiagnostic(Diagnostic.Create(Rule, first.Dot.GetLocation()));
        }
    }

    private static (ExpressionSyntax Receiver, SyntaxToken Dot)? GetAccess(InvocationExpressionSyntax invocation)
    {
        if (invocation.Expression is MemberAccessExpressionSyntax access)
        {
            return (access.Expression, access.OperatorToken);
        }

        if (invocation.Expression is MemberBindingExpressionSyntax)
        {
            var conditional = invocation.Ancestors().OfType<ConditionalAccessExpressionSyntax>()
                .FirstOrDefault(candidate => candidate.WhenNotNull.Span.Contains(invocation.Expression.Span));

            if (conditional is not null)
            {
                return (conditional.Expression, conditional.OperatorToken);
            }
        }

        return null;
    }

    private static InvocationExpressionSyntax? PreviousCall(ExpressionSyntax expression)
    {
        while (expression is not InvocationExpressionSyntax)
        {
            var previous = PreviousLink(expression);

            if (previous is null)
            {
                return null;
            }

            expression = previous;
        }

        return (InvocationExpressionSyntax)expression;
    }

    private static ExpressionSyntax? PreviousLink(ExpressionSyntax expression) => expression switch
    {
        MemberAccessExpressionSyntax access => access.Expression,
        ElementAccessExpressionSyntax access => access.Expression,
        PostfixUnaryExpressionSyntax postfix when postfix.IsKind(SyntaxKind.SuppressNullableWarningExpression) => postfix.Operand,
        ConditionalAccessExpressionSyntax conditional => conditional.WhenNotNull,
        _ => null,
    };

    private static bool HasFollowingCall(ExpressionSyntax expression)
    {
        while (expression.Parent is ExpressionSyntax parent)
        {
            if (parent is InvocationExpressionSyntax call && call.Expression == expression)
            {
                return true;
            }

            if (parent is ConditionalAccessExpressionSyntax conditional && conditional.Expression == expression)
            {
                if (PreviousCall(conditional.WhenNotNull) is not null)
                {
                    return true;
                }

                expression = parent;
                continue;
            }

            if (PreviousLink(parent) != expression)
            {
                return false;
            }

            expression = parent;
        }

        return false;
    }

    private static bool StartsLine(SyntaxToken token)
    {
        var indentation = 0;
        for (var index = token.LeadingTrivia.Count - 1; index >= 0 && token.LeadingTrivia[index].IsKind(SyntaxKind.WhitespaceTrivia); index--)
        {
            indentation += token.LeadingTrivia[index].Span.Length;
        }

        return token.GetLocation().GetLineSpan().StartLinePosition.Character == indentation;
    }
}
