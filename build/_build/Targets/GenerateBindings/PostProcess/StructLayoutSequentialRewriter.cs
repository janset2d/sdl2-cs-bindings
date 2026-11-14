using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Janset.SDL2.PostProcess;

internal sealed class StructLayoutSequentialRewriter : CSharpSyntaxRewriter
{
    private readonly HashSet<string> _structsWithLayout = new(StringComparer.Ordinal);
    private bool _needsInteropUsing;

    public bool AnyChanges { get; private set; }

    public void Reset()
    {
        _structsWithLayout.Clear();
        _needsInteropUsing = false;
        AnyChanges = false;
    }

    public override SyntaxNode? VisitCompilationUnit(CompilationUnitSyntax node)
    {
        foreach (var structDeclaration in node.DescendantNodes().OfType<StructDeclarationSyntax>())
        {
            if (HasStructLayout(structDeclaration))
            {
                _structsWithLayout.Add(GetStructKey(structDeclaration));
            }
        }

        var result = (CompilationUnitSyntax)base.VisitCompilationUnit(node)!;
        if (!_needsInteropUsing || HasInteropUsing(result))
        {
            return result;
        }

        var newUsing = SyntaxFactory
            .UsingDirective(SyntaxFactory.ParseName("System.Runtime.InteropServices"))
            .NormalizeWhitespace()
            .WithTrailingTrivia(SyntaxFactory.LineFeed);

        return result.AddUsings(newUsing);
    }

    public override SyntaxNode? VisitStructDeclaration(StructDeclarationSyntax node)
    {
        var result = (StructDeclarationSyntax)base.VisitStructDeclaration(node)!;
        if (HasStructLayout(result) || _structsWithLayout.Contains(GetStructKey(result)))
        {
            return result;
        }

        AnyChanges = true;
        _needsInteropUsing = true;
        var leadingTrivia = result.GetLeadingTrivia();
        return result
            .WithLeadingTrivia(ExtractIndentation(leadingTrivia))
            .AddAttributeLists(BuildStructLayoutAttributeList(leadingTrivia));
    }

    private static bool HasStructLayout(StructDeclarationSyntax node) =>
        node.AttributeLists
            .SelectMany(list => list.Attributes)
            .Any(attribute => IsStructLayoutName(attribute.Name.ToString()));

    private static bool IsStructLayoutName(string name) =>
        name is "StructLayout" or "StructLayoutAttribute"
        || name.EndsWith(".StructLayout", StringComparison.Ordinal)
        || name.EndsWith(".StructLayoutAttribute", StringComparison.Ordinal);

    private static bool HasInteropUsing(CompilationUnitSyntax node) =>
        node.Usings.Any(u => u.Name?.ToString() == "System.Runtime.InteropServices");

    private static string GetStructKey(StructDeclarationSyntax node)
    {
        var parts = new List<string>();
        foreach (var ancestor in node.Ancestors().Reverse())
        {
            switch (ancestor)
            {
                case BaseNamespaceDeclarationSyntax namespaceDeclaration:
                    parts.Add(namespaceDeclaration.Name.ToString());
                    break;
                case TypeDeclarationSyntax typeDeclaration:
                    parts.Add(typeDeclaration.Identifier.ValueText);
                    break;
            }
        }

        parts.Add(node.Identifier.ValueText);
        return string.Join('.', parts);
    }

    private static AttributeListSyntax BuildStructLayoutAttributeList(SyntaxTriviaList leadingTrivia)
    {
        var argument = SyntaxFactory.AttributeArgument(SyntaxFactory.ParseExpression("LayoutKind.Sequential"));
        var attribute = SyntaxFactory.Attribute(
            SyntaxFactory.IdentifierName("StructLayout"),
            SyntaxFactory.AttributeArgumentList(SyntaxFactory.SingletonSeparatedList(argument)));

        return SyntaxFactory.AttributeList(SyntaxFactory.SingletonSeparatedList(attribute))
            .WithLeadingTrivia(leadingTrivia)
            .WithTrailingTrivia(SyntaxFactory.EndOfLine("\n"));
    }

    private static SyntaxTriviaList ExtractIndentation(SyntaxTriviaList leadingTrivia)
    {
        for (var i = leadingTrivia.Count - 1; i >= 0; i--)
        {
            if (leadingTrivia[i].IsKind(SyntaxKind.WhitespaceTrivia))
            {
                return SyntaxFactory.TriviaList(leadingTrivia[i]);
            }

            if (leadingTrivia[i].IsKind(SyntaxKind.EndOfLineTrivia))
            {
                return default;
            }
        }

        return default;
    }
}
