using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Build.Targets.GenerateBindings.PostProcess;

internal static class DeclarationKey
{
    public static string Type(string name) => $"type:{name}";

    public static string Method(string name) => $"method:{name}";

    public static string Field(SyntaxNode node, string name)
    {
        var declaringType = string.Join(
            '.',
            node
                .Ancestors()
                .OfType<BaseTypeDeclarationSyntax>()
                .Reverse()
                .Select(type => type.Identifier.Text));

        return $"field:{declaringType}:{name}";
    }
}
