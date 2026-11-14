using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Build.Targets.GenerateBindings.PostProcess;

internal sealed class PlatformDeltaFileRewriter(
    ISet<string> seenDeclarations,
    string supportedOs) : CSharpSyntaxRewriter
{
    public bool AnyChanges { get; private set; }

    public bool AddedPlatformAttributes { get; private set; }

    public int RemovedDeclarations { get; private set; }

    public override SyntaxNode? VisitDelegateDeclaration(DelegateDeclarationSyntax node)
    {
        return KeepOrRemove(DeclarationKey.Type(node.Identifier.Text), node, node.Identifier.Text);
    }

    public override SyntaxNode? VisitEnumDeclaration(EnumDeclarationSyntax node)
    {
        return KeepOrRemove(DeclarationKey.Type(node.Identifier.Text), node, node.Identifier.Text);
    }

    public override SyntaxNode? VisitStructDeclaration(StructDeclarationSyntax node)
    {
        return KeepOrRemove(DeclarationKey.Type(node.Identifier.Text), node, node.Identifier.Text);
    }

    public override SyntaxNode? VisitClassDeclaration(ClassDeclarationSyntax node)
    {
        var rewritten = (ClassDeclarationSyntax)base.VisitClassDeclaration(node)!;
        return rewritten;
    }

    public override SyntaxNode? VisitMethodDeclaration(MethodDeclarationSyntax node)
    {
        var key = DeclarationKey.Method(node.Identifier.Text);

        if (!seenDeclarations.Add(key))
        {
            AnyChanges = true;
            RemovedDeclarations++;
            return null;
        }

        if (HasPlatformAttribute(node))
        {
            return node;
        }

        AnyChanges = true;
        AddedPlatformAttributes = true;
        return node.WithAttributeLists(node.AttributeLists.Insert(0, CreatePlatformAttribute(node)));
    }

    public override SyntaxNode? VisitFieldDeclaration(FieldDeclarationSyntax node)
    {
        var variables = node.Declaration.Variables;

        if (variables.Count != 1)
        {
            return node;
        }

        return KeepOrRemove(DeclarationKey.Field(node, variables[0].Identifier.Text), node, variables[0].Identifier.Text);
    }

    private SyntaxNode? KeepOrRemove<TNode>(string key, TNode node, string name)
        where TNode : SyntaxNode
    {
        _ = name;

        if (seenDeclarations.Add(key))
        {
            return node;
        }

        AnyChanges = true;
        RemovedDeclarations++;
        return null;
    }

    private AttributeListSyntax CreatePlatformAttribute(MethodDeclarationSyntax method)
    {
        var attribute = SyntaxFactory.Attribute(
            SyntaxFactory.IdentifierName("SupportedOSPlatform"),
            SyntaxFactory.AttributeArgumentList(
                SyntaxFactory.SingletonSeparatedList(
                    SyntaxFactory.AttributeArgument(
                        SyntaxFactory.LiteralExpression(
                            SyntaxKind.StringLiteralExpression,
                            SyntaxFactory.Literal(supportedOs))))));

        return SyntaxFactory.AttributeList(SyntaxFactory.SingletonSeparatedList(attribute))
            .NormalizeWhitespace()
            .WithLeadingTrivia(method.GetLeadingTrivia())
            .WithTrailingTrivia(SyntaxFactory.LineFeed);
    }

    private static bool HasPlatformAttribute(MethodDeclarationSyntax method)
    {
        return method.AttributeLists
            .SelectMany(list => list.Attributes)
            .Any(attr => attr.Name.ToString().Contains("SupportedOSPlatform", StringComparison.Ordinal));
    }
}
