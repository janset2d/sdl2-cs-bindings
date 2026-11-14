using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Janset.SDL2.PostProcess;

// Transforms ClangSharp's [DllImport(...)] static extern method declarations into
// the .NET 7+ source-generated [LibraryImport(...)] + [UnmanagedCallConv(...)] +
// static partial form. The Modern codegen pass output is the input — Compat
// output stays untouched for legacy TFMs.
//
// Containing class is left as-is. ClangSharp already emits `public static unsafe
// partial class SDLNative` (partial keyword already present), so [LibraryImport]'s
// source generator can plug in.
internal sealed class DllImportToLibraryImportRewriter : CSharpSyntaxRewriter
{
    private bool _needsCompilerServicesUsing;

    public bool AnyChanges { get; private set; }

    public void Reset()
    {
        _needsCompilerServicesUsing = false;
        AnyChanges = false;
    }

    public override SyntaxNode? VisitCompilationUnit(CompilationUnitSyntax node)
    {
        var result = (CompilationUnitSyntax)base.VisitCompilationUnit(node)!;
        if (!_needsCompilerServicesUsing)
        {
            return result;
        }

        if (result.Usings.Any(u => u.Name?.ToString() == "System.Runtime.CompilerServices"))
        {
            return result;
        }

        var newUsing = SyntaxFactory
            .UsingDirective(SyntaxFactory.ParseName("System.Runtime.CompilerServices"))
            .NormalizeWhitespace()
            .WithTrailingTrivia(SyntaxFactory.LineFeed);

        return result.AddUsings(newUsing);
    }

    public override SyntaxNode? VisitMethodDeclaration(MethodDeclarationSyntax node)
    {
        var dllImportAttr = FindDllImportAttribute(node, out var hostingList);
        if (dllImportAttr is null || hostingList is null)
        {
            return node;
        }

        AnyChanges = true;

        // Partition the existing DllImport arguments:
        //   * keep: library name (no NameEquals) + EntryPoint
        //   * drop: ExactSpelling (LibraryImport default is true)
        //   * extract: CallingConvention → moves to [UnmanagedCallConv]
        AttributeArgumentSyntax? callingConvArg = null;
        var keptArgs = new List<AttributeArgumentSyntax>();
        foreach (var arg in dllImportAttr.ArgumentList?.Arguments ?? default)
        {
            var name = arg.NameEquals?.Name.Identifier.Text;
            switch (name)
            {
                case "CallingConvention":
                    callingConvArg = arg;
                    break;
                case "ExactSpelling":
                    break;
                default:
                    keptArgs.Add(arg);
                    break;
            }
        }

        var libraryImportAttr = SyntaxFactory.Attribute(
                SyntaxFactory.IdentifierName("LibraryImport"),
                SyntaxFactory.AttributeArgumentList(SyntaxFactory.SeparatedList(keptArgs)))
            .NormalizeWhitespace();

        // [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        // — array-creation syntax is the long-supported form; collection expressions
        // would need C# 12 emit which Roslyn handles but trips some legacy IDE views.
        AttributeListSyntax? unmanagedCallConvList = null;
        if (callingConvArg is not null
            && callingConvArg.Expression is MemberAccessExpressionSyntax mae
            && mae.Name.Identifier.Text == "Cdecl")
        {
            _needsCompilerServicesUsing = true;

            var callConvsArg = SyntaxFactory.AttributeArgument(
                    SyntaxFactory.ImplicitArrayCreationExpression(
                        SyntaxFactory.InitializerExpression(
                            SyntaxKind.ArrayInitializerExpression,
                            SyntaxFactory.SingletonSeparatedList<ExpressionSyntax>(
                                SyntaxFactory.TypeOfExpression(
                                    SyntaxFactory.IdentifierName("CallConvCdecl"))))))
                .WithNameEquals(SyntaxFactory.NameEquals("CallConvs"));

            unmanagedCallConvList = SyntaxFactory.AttributeList(
                    SyntaxFactory.SingletonSeparatedList(
                        SyntaxFactory.Attribute(
                            SyntaxFactory.IdentifierName("UnmanagedCallConv"),
                            SyntaxFactory.AttributeArgumentList(
                                SyntaxFactory.SingletonSeparatedList(callConvsArg)))))
                .NormalizeWhitespace();
        }

        // Rebuild the hosting attribute list with LibraryImport substituted in
        // place of DllImport (other attributes on the same list are preserved).
        var rewrittenHostList = hostingList.WithAttributes(
            SyntaxFactory.SeparatedList(
                hostingList.Attributes.Select(a => a == dllImportAttr ? libraryImportAttr : a)));

        var newAttrLists = new List<AttributeListSyntax>();
        foreach (var al in node.AttributeLists)
        {
            if (al == hostingList)
            {
                newAttrLists.Add(rewrittenHostList);
                if (unmanagedCallConvList is not null)
                {
                    // Match the host list's leading whitespace so the second attribute
                    // lines up under the first in the output, and end with a newline
                    // so the next attribute/method declaration starts on its own line.
                    newAttrLists.Add(unmanagedCallConvList
                        .WithLeadingTrivia(GetIndentationTrivia(rewrittenHostList))
                        .WithTrailingTrivia(SyntaxFactory.LineFeed));
                }
            }
            else
            {
                newAttrLists.Add(al);
            }
        }

        // Drop `extern` (LibraryImport source generator emits the body); add `partial`
        // with an explicit trailing space so it doesn't fuse with the return type.
        var modifiers = node.Modifiers
            .Where(m => !m.IsKind(SyntaxKind.ExternKeyword))
            .ToList();
        if (!modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword)))
        {
            modifiers.Add(SyntaxFactory.Token(SyntaxKind.PartialKeyword).WithTrailingTrivia(SyntaxFactory.Space));
        }

        return node
            .WithAttributeLists(SyntaxFactory.List(newAttrLists))
            .WithModifiers(SyntaxFactory.TokenList(modifiers));
    }

    private static SyntaxTriviaList GetIndentationTrivia(AttributeListSyntax attributeList)
    {
        var leadingText = attributeList.GetLeadingTrivia().ToFullString();
        var lastNewLine = leadingText.LastIndexOf('\n');
        var indentation = lastNewLine >= 0 ? leadingText[(lastNewLine + 1)..] : leadingText;
        if (string.IsNullOrWhiteSpace(indentation))
        {
            indentation = "        ";
        }

        return SyntaxFactory.TriviaList(SyntaxFactory.Whitespace(indentation));
    }

    private static AttributeSyntax? FindDllImportAttribute(
        MethodDeclarationSyntax method,
        out AttributeListSyntax? hostingList)
    {
        foreach (var al in method.AttributeLists)
        {
            foreach (var attr in al.Attributes)
            {
                var name = attr.Name.ToString();
                if (name is "DllImport" or "DllImportAttribute"
                    || name.EndsWith(".DllImport", StringComparison.Ordinal)
                    || name.EndsWith(".DllImportAttribute", StringComparison.Ordinal))
                {
                    hostingList = al;
                    return attr;
                }
            }
        }

        hostingList = null;
        return null;
    }
}
