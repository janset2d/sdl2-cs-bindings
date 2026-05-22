using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Janset.SDL2.PostProcess;

// Constitution §"C Variadics" L162-176: Stage 1 policy is fmt-only mapping.
// `__arglist` is explicitly rejected because it does not compose with the
// planned string/span overload tiers or with old TFM support. SDL variadic
// functions are emitted as fixed-prefix `byte* fmt` raw P/Invoke; the `...`
// tail is dropped at the binding boundary, callers pre-format the string.
//
// ClangSharp's default output emits the `__arglist` keyword for C-varargs
// methods. This rewriter strips that parameter so the resulting signature is
// the fmt-only fixed-prefix shape the constitution mandates. Applied to both
// Compat and Modern codegen output before the LibraryImport pass — keeps
// Microsoft.Interop.LibraryImportGenerator happy too (it does not support
// varargs either).
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
