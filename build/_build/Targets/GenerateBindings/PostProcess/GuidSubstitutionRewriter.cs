using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Build.Targets.GenerateBindings.PostProcess;

/// <summary>
/// Substitutes SDL_GUID with the equally sized System.Guid. Guid's structured string representation
/// differs from SDL's raw hexadecimal convention. Removes the original struct and adds System only when needed.
/// </summary>
internal sealed class GuidSubstitutionRewriter : CSharpSyntaxRewriter
{
    private bool _needsSystemUsing;

    public bool AnyChanges { get; private set; }

    public void Reset()
    {
        _needsSystemUsing = false;
        AnyChanges = false;
    }

    public override SyntaxNode? VisitCompilationUnit(CompilationUnitSyntax node)
    {
        // Roslyn visits depth-first, so by the time we return to the compilation
        // unit `_needsSystemUsing` reflects every identifier substitution in the
        // subtree. Guard on the flag rather than `AnyChanges` so a file whose
        // only change is struct removal (no remaining Guid reference) doesn't
        // grow a stray using. With current SDL2 headers every file touched by
        // the rewriter also references Guid afterwards, but the flag keeps the
        // invariant honest if a future header ever decouples the two.
        var result = (CompilationUnitSyntax)base.VisitCompilationUnit(node)!;

        if (!_needsSystemUsing)
        {
            return result;
        }

        if (result.Usings.Any(u => u.Name?.ToString() == "System"))
        {
            return result;
        }

        var systemUsing = SyntaxFactory
            .UsingDirective(SyntaxFactory.IdentifierName("System"))
            .NormalizeWhitespace()
            .WithTrailingTrivia(SyntaxFactory.LineFeed);

        // Insert at index 0 rather than DllImport's AddUsings (which appends)
        // to preserve the conventional "System first" ordering in the resulting
        // file.
        return result.WithUsings(result.Usings.Insert(0, systemUsing));
    }

    public override SyntaxNode? VisitStructDeclaration(StructDeclarationSyntax node)
    {
        if (node.Identifier.ValueText == "SDL_GUID")
        {
            AnyChanges = true;
            // Returning null removes the declaration entirely. The struct
            // body's nested types (e.g. ClangSharp's _data_e__FixedBuffer
            // inline-array helper) go with it, which is what we want — the
            // wire format is fully covered by System.Guid's layout.
            return null;
        }

        return base.VisitStructDeclaration(node);
    }

    public override SyntaxNode? VisitIdentifierName(IdentifierNameSyntax node)
    {
        if (node.Identifier.ValueText == "SDL_GUID")
        {
            AnyChanges = true;
            _needsSystemUsing = true;
            return SyntaxFactory.IdentifierName("Guid").WithTriviaFrom(node);
        }

        return base.VisitIdentifierName(node);
    }
}
