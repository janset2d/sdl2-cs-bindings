using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Janset.SDL2.PostProcess;

// Substitute SDL_GUID with System.Guid throughout generated output.
//
// C SDL_GUID is `typedef struct { Uint8 data[16]; } SDL_GUID;` (16 raw bytes
// for joystick/gamecontroller identification, not UUID-compliant). C# System.Guid
// is also 16 bytes with structured Data1/Data2/Data3/Data4 fields (UUID
// convention). Wire size is bit-identical, so P/Invoke marshalling is correct,
// but caller-side .ToString() on a Guid returned from SDL renders Microsoft
// GUID notation rather than SDL's raw-hex-bytes convention. This is a
// Constitution-level trade-off accepted at the binding boundary; Cake's
// SdlNativeTypeSubstitutionPolicy applies the same substitution and pins
// `SDL_GUID → System.Guid` as the canonical repo policy (Workstream README
// Current Decision Posture).
//
// Mechanism:
//   1. Remove `partial struct SDL_GUID { ... }` declaration entirely.
//   2. Rewrite every reference (parameter/return/field type) `SDL_GUID` -> `Guid`.
//   3. VisitCompilationUnit ensures `using System;` is present in any file
//      where step 2 actually substituted an identifier, so `Guid` resolves
//      without fully-qualified spellings cluttering signatures. Mirrors
//      DllImportToLibraryImportRewriter's `_needsCompilerServicesUsing` pattern
//      so all rewriters handle using-insertion internally rather than via
//      mode-specific special-cases in Program.cs.
//
// Refs: Constitution §"Current SDL2.Core ABI Status" and Priority C closure
// summary §"Additional Resolutions".
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
        // file. Matches the original output from when this lived in Program.cs.
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
