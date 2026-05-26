using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Janset.SDL2.PostProcess;

internal sealed class FlagsAttributeRewriter : CSharpSyntaxRewriter
{
    private readonly HashSet<string> _allowList;

    public FlagsAttributeRewriter(HashSet<string> allowList)
    {
        _allowList = new HashSet<string>(allowList, StringComparer.Ordinal);
    }

    public bool AnyChanges { get; private set; }

    public void Reset() => AnyChanges = false;

    public override SyntaxNode? VisitEnumDeclaration(EnumDeclarationSyntax node)
    {
        if (AlreadyHasFlagsAttribute(node))
        {
            return node;
        }

        var enumName = node.Identifier.ValueText;
        if (!enumName.EndsWith("Flags", StringComparison.Ordinal) && !_allowList.Contains(enumName))
        {
            return node;
        }

        AnyChanges = true;
        var leadingTrivia = node.GetLeadingTrivia();
        return node
            .WithLeadingTrivia(ExtractIndentation(leadingTrivia))
            .AddAttributeLists(BuildFlagsAttributeList(leadingTrivia));
    }

    private static bool AlreadyHasFlagsAttribute(EnumDeclarationSyntax node) =>
        node.AttributeLists
            .SelectMany(list => list.Attributes)
            .Any(attribute => attribute.Name.ToString() is "Flags" or "FlagsAttribute" or "System.Flags" or "System.FlagsAttribute");

    private static AttributeListSyntax BuildFlagsAttributeList(SyntaxTriviaList leadingTrivia)
    {
        var attribute = SyntaxFactory.Attribute(SyntaxFactory.ParseName("System.Flags"));
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
