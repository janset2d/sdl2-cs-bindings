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
//   3. Caller (Program.cs) ensures `using System;` is present in any file the
//      rewriter touched via EnsureSystemUsing, so `Guid` resolves without
//      fully-qualified spellings cluttering signatures.
//
// Refs: docs/superpowers/specs/2026-05-24-clangsharp-priority-c-semantic-abi-design.md
// Slice C-C SDL_GUID; docs/binding-autogen/README.md Current Decision Posture.
internal sealed class GuidSubstitutionRewriter : CSharpSyntaxRewriter
{
    public bool AnyChanges { get; private set; }

    public void Reset() => AnyChanges = false;

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
            return SyntaxFactory.IdentifierName("Guid").WithTriviaFrom(node);
        }

        return base.VisitIdentifierName(node);
    }

    // Inserts `using System;` at the top of the compilation unit when absent.
    // Caller (Program.cs) invokes this on the rewritten root after Visit when
    // any change was applied, so files that now reference Guid pick up the
    // System namespace without forcing every untouched file to grow a using
    // it doesn't need.
    public static CompilationUnitSyntax EnsureSystemUsing(CompilationUnitSyntax root)
    {
        var hasSystemUsing = root.Usings.Any(u => u.Name?.ToString() == "System");
        if (hasSystemUsing)
        {
            return root;
        }

        var systemUsing = SyntaxFactory
            .UsingDirective(SyntaxFactory.IdentifierName("System"))
            .NormalizeWhitespace()
            .WithTrailingTrivia(SyntaxFactory.LineFeed);

        return root.WithUsings(root.Usings.Insert(0, systemUsing));
    }
}
