using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Build.Targets.GenerateBindings.PostProcess;

/// <summary>Strips ClangSharp's varargs parameter, retaining the fixed prefix for caller-formatted strings.</summary>
internal sealed class StripVarargsRewriter : CSharpSyntaxRewriter
{
    public bool AnyChanges { get; private set; }

    public void Reset() => AnyChanges = false;

    public override SyntaxNode? VisitMethodDeclaration(MethodDeclarationSyntax node)
    {
        var parameters = node.ParameterList.Parameters;
        var argListIndex = -1;

        for (var i = 0; i < parameters.Count; i++)
        {
            var parameter = parameters[i];

            if (parameter.Identifier.IsKind(SyntaxKind.ArgListKeyword)
                || parameter.Identifier.Text == "__arglist")
            {
                argListIndex = i;
                break;
            }
        }

        if (argListIndex < 0)
        {
            return node;
        }

        AnyChanges = true;

        // Drop the __arglist parameter. SeparatedSyntaxList's RemoveAt handles
        // the surrounding comma trivia so the remaining parameter list stays
        // syntactically valid.
        var stripped = parameters.RemoveAt(argListIndex);
        var newParameterList = node.ParameterList.WithParameters(stripped);
        return node.WithParameterList(newParameterList);
    }
}
