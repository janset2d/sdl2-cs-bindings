using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Janset.CodeStyle.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class BlankLineBeforeBlockAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "JNS0002";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Add a blank line before a block statement",
        "Add a blank line before a block statement",
        "Style",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(Analyze,
            SyntaxKind.IfStatement, SyntaxKind.ForStatement, SyntaxKind.ForEachStatement, SyntaxKind.ForEachVariableStatement,
            SyntaxKind.WhileStatement, SyntaxKind.DoStatement, SyntaxKind.SwitchStatement, SyntaxKind.TryStatement,
            SyntaxKind.UsingStatement, SyntaxKind.LockStatement, SyntaxKind.FixedStatement);
    }

    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var statement = (StatementSyntax)context.Node;
        var followsStatement = statement.Parent switch
        {
            BlockSyntax block => block.Statements.IndexOf(statement) > 0,
            SwitchSectionSyntax section => section.Statements.IndexOf(statement) > 0,
            GlobalStatementSyntax global when global.Parent is CompilationUnitSyntax unit =>
                unit.Members.IndexOf(global) > 0 && unit.Members[unit.Members.IndexOf(global) - 1] is GlobalStatementSyntax,
            _ => false
        };

        if (!followsStatement || statement is UsingStatementSyntax { Statement: not BlockSyntax }
            or LockStatementSyntax { Statement: not BlockSyntax } or FixedStatementSyntax { Statement: not BlockSyntax })
        {
            return;
        }

        var first = statement.GetFirstToken();
        var previous = first.GetPreviousToken();
        var afterNewLine = false;

        foreach (var trivia in previous.TrailingTrivia.Concat(first.LeadingTrivia))
        {
            if (trivia.IsKind(SyntaxKind.EndOfLineTrivia))
            {
                if (afterNewLine)
                {
                    return;
                }

                afterNewLine = true;
            }
            else if (!trivia.IsKind(SyntaxKind.WhitespaceTrivia))
            {
                afterNewLine = false;
            }
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, first.GetLocation()));
    }
}
