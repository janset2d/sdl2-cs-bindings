using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Build.Targets.GenerateBindings.PostProcess;

internal sealed class DeclarationCollector(ISet<string> seenDeclarations) : CSharpSyntaxWalker
{
    public override void VisitDelegateDeclaration(DelegateDeclarationSyntax node)
    {
        seenDeclarations.Add(DeclarationKey.Type(node.Identifier.Text));
        base.VisitDelegateDeclaration(node);
    }

    public override void VisitEnumDeclaration(EnumDeclarationSyntax node)
    {
        seenDeclarations.Add(DeclarationKey.Type(node.Identifier.Text));
        base.VisitEnumDeclaration(node);
    }

    public override void VisitStructDeclaration(StructDeclarationSyntax node)
    {
        seenDeclarations.Add(DeclarationKey.Type(node.Identifier.Text));
        base.VisitStructDeclaration(node);
    }

    public override void VisitMethodDeclaration(MethodDeclarationSyntax node)
    {
        seenDeclarations.Add(DeclarationKey.Method(node.Identifier.Text));
        base.VisitMethodDeclaration(node);
    }

    public override void VisitFieldDeclaration(FieldDeclarationSyntax node)
    {
        foreach (var variable in node.Declaration.Variables)
        {
            seenDeclarations.Add(DeclarationKey.Field(node, variable.Identifier.Text));
        }

        base.VisitFieldDeclaration(node);
    }
}
