using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Janset.SDL2.PostProcess;

// R2 structural-symbol hybrid emit for the SDL_threadID family.
//
// SDL_ThreadID / SDL_GetThreadID return the OS-level thread identifier (C
// `unsigned long`, typedef'd to SDL_threadID). It is structurally different
// from System.Threading.Thread.ManagedThreadId — they live in different ID
// spaces — and so cannot be dropped from the binding surface on legacy TFMs.
//
// Sensor: methods with [return: NativeTypeName("SDL_threadID")] or
// [return: NativeTypeName("unsigned long")] whose identifier is on the allow
// list (SDL_ThreadID / SDL_GetThreadID).
//
// Emit (replaces the single P/Invoke method with a TFM-conditional block of
// members inside the same SDLNative partial class):
//
//   #if NET6_0_OR_GREATER
//   [LibraryImport("SDL2", EntryPoint = "SDL_ThreadID")]
//   [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
//   [return: NativeTypeName("SDL_threadID")]
//   public static partial CULong SDL_ThreadID();
//   #endif
//
//   #if !NET6_0_OR_GREATER
//   [return: NativeTypeName("SDL_threadID")]
//   public static ulong SDL_ThreadID()
//   {
//       if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
//           return SDL_ThreadID_Win32();
//       return (ulong)SDL_ThreadID_Unix64();
//   }
//
//   [DllImport("SDL2", EntryPoint = "SDL_ThreadID", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
//   private static extern uint SDL_ThreadID_Win32();
//
//   [DllImport("SDL2", EntryPoint = "SDL_ThreadID", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
//   private static extern nint SDL_ThreadID_Unix64();
//   #endif
//
// Mechanism: the rewriter cannot use Roslyn's syntax-tree mutation to inject
// the multi-branch block because the CSharp parser strips one of the #if
// branches during parse — only the currently-active preprocessor branch
// survives. Instead we collect detected methods during the syntax walk, then
// rewrite at the source-text level in VisitCompilationUnit by computing each
// method's text span and substituting the verbatim raw block. The compilation
// unit returned is a re-parse of the substituted text, so ToFullString() emits
// both branches as literal text.
//
// Refs: docs/superpowers/specs/2026-05-24-clangsharp-priority-c-semantic-abi-design.md
// Decision 2 — C `long` Hybrid Strategy; Constitution §"C `long` And `unsigned long`"
// Priority C hybrid strategy.
internal sealed class ThreadIdDualDispatchRewriter : CSharpSyntaxRewriter
{
    private static readonly HashSet<string> AffectedMethodNames = new(StringComparer.Ordinal)
    {
        "SDL_ThreadID",
        "SDL_GetThreadID",
    };

    private readonly List<(TextSpan FullSpan, string Replacement)> _pending = new();

    public bool AnyChanges { get; private set; }

    public void Reset()
    {
        AnyChanges = false;
        _pending.Clear();
    }

    public override SyntaxNode? VisitMethodDeclaration(MethodDeclarationSyntax node)
    {
        if (!AffectedMethodNames.Contains(node.Identifier.ValueText))
        {
            return node;
        }

        var returnNativeType = ExtractReturnNativeTypeName(node);
        if (returnNativeType is not ("SDL_threadID" or "unsigned long"))
        {
            return node;
        }

        var libPath = ExtractLibraryPath(node) ?? "SDL2";
        var accessModifier = ExtractAccessModifier(node);
        var name = node.Identifier.ValueText;
        var paramList = node.ParameterList.ToString();
        var indent = ExtractLeadingIndentation(node);
        var replacement = BuildReplacementMembersBlock(
            name, libPath, accessModifier, returnNativeType, paramList, indent);

        // Stage a text-level substitution. We use FullSpan (which includes
        // leading trivia: blank line + indentation) so the replacement controls
        // its own preamble whitespace and the #if directive lines stay at
        // column 0 instead of inheriting the original method's indentation.
        _pending.Add((node.FullSpan, replacement));
        AnyChanges = true;
        return node;
    }

    public override SyntaxNode? VisitCompilationUnit(CompilationUnitSyntax node)
    {
        // Visit first so VisitMethodDeclaration populates _pending.
        var visited = (CompilationUnitSyntax)base.VisitCompilationUnit(node)!;
        if (_pending.Count == 0)
        {
            return visited;
        }

        var source = node.SyntaxTree.GetText();
        // Apply substitutions in reverse order so earlier spans aren't
        // invalidated by edits at later offsets.
        var sb = new StringBuilder(source.ToString());
        foreach (var (span, replacement) in _pending.OrderByDescending(p => p.FullSpan.Start))
        {
            sb.Remove(span.Start, span.Length);
            sb.Insert(span.Start, replacement);
        }

        var rewrittenTree = CSharpSyntaxTree.ParseText(sb.ToString());
        return rewrittenTree.GetCompilationUnitRoot();
    }

    private static string BuildReplacementMembersBlock(
        string name,
        string libPath,
        string accessModifier,
        string returnNativeType,
        string paramList,
        string indent)
    {
        // Preamble: blank line + indent — matches the leading trivia the
        // original method declaration would have had after the previous member,
        // since we replace via FullSpan (which swallowed the original trivia).
        var sb = new StringBuilder();
        sb.AppendLine();
        sb.AppendLine("#if NET6_0_OR_GREATER");
        sb.Append(indent).AppendLine($"[global::System.Runtime.InteropServices.LibraryImport(\"{libPath}\", EntryPoint = \"{name}\")]");
        sb.Append(indent).AppendLine("[global::System.Runtime.InteropServices.UnmanagedCallConv(CallConvs = new[] { typeof(global::System.Runtime.CompilerServices.CallConvCdecl) })]");
        sb.Append(indent).AppendLine($"[return: NativeTypeName(\"{returnNativeType}\")]");
        sb.Append(indent).AppendLine($"{accessModifier} static partial global::System.Runtime.InteropServices.CULong {name}{paramList};");
        sb.AppendLine("#endif");
        sb.AppendLine("#if !NET6_0_OR_GREATER");
        sb.Append(indent).AppendLine($"[return: NativeTypeName(\"{returnNativeType}\")]");
        sb.Append(indent).AppendLine($"{accessModifier} static ulong {name}{paramList}");
        sb.Append(indent).AppendLine("{");
        sb.Append(indent).AppendLine("    if (global::System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(global::System.Runtime.InteropServices.OSPlatform.Windows))");
        sb.Append(indent).AppendLine($"        return {name}_Win32{StripParamTypes(paramList)};");
        sb.Append(indent).AppendLine($"    return (ulong){name}_Unix64{StripParamTypes(paramList)};");
        sb.Append(indent).AppendLine("}");
        sb.AppendLine();
        sb.Append(indent).AppendLine($"[global::System.Runtime.InteropServices.DllImport(\"{libPath}\", EntryPoint = \"{name}\", CallingConvention = global::System.Runtime.InteropServices.CallingConvention.Cdecl, ExactSpelling = true)]");
        sb.Append(indent).AppendLine($"private static extern uint {name}_Win32{paramList};");
        sb.AppendLine();
        sb.Append(indent).AppendLine($"[global::System.Runtime.InteropServices.DllImport(\"{libPath}\", EntryPoint = \"{name}\", CallingConvention = global::System.Runtime.InteropServices.CallingConvention.Cdecl, ExactSpelling = true)]");
        sb.Append(indent).AppendLine($"private static extern nint {name}_Unix64{paramList};");
        sb.AppendLine("#endif");
        return sb.ToString();
    }

    // Strip type names from a parameter list so it can be reused as an argument list.
    // e.g., "(SDL_Thread* thread)" -> "(thread)"; "()" stays "()".
    private static string StripParamTypes(string paramList)
    {
        var inner = paramList.Trim('(', ')').Trim();
        if (string.IsNullOrEmpty(inner)) return "()";

        var args = inner.Split(',')
            .Select(p => p.Trim().Split(' ', '*').Last().TrimStart('@'))
            .Where(s => !string.IsNullOrWhiteSpace(s));
        return $"({string.Join(", ", args)})";
    }

    private static string? ExtractReturnNativeTypeName(MethodDeclarationSyntax method)
    {
        foreach (var al in method.AttributeLists)
        {
            if (al.Target?.Identifier.ValueText != "return") continue;
            foreach (var attr in al.Attributes)
            {
                if (attr.Name.ToString() != "NativeTypeName") continue;
                var arg = attr.ArgumentList?.Arguments.FirstOrDefault();
                if (arg?.Expression is LiteralExpressionSyntax lit)
                {
                    return lit.Token.ValueText;
                }
            }
        }
        return null;
    }

    private static string? ExtractLibraryPath(MethodDeclarationSyntax method)
    {
        foreach (var al in method.AttributeLists)
        {
            foreach (var attr in al.Attributes)
            {
                var name = attr.Name.ToString();
                if (name is "DllImport" or "LibraryImport")
                {
                    var firstArg = attr.ArgumentList?.Arguments.FirstOrDefault();
                    if (firstArg?.Expression is LiteralExpressionSyntax lit)
                    {
                        return lit.Token.ValueText;
                    }
                }
            }
        }
        return null;
    }

    // Preserve the original method's access modifier (public/internal) so the
    // rewritten emit matches the rest of the SDLNative container's surface.
    private static string ExtractAccessModifier(MethodDeclarationSyntax method)
    {
        foreach (var modifier in method.Modifiers)
        {
            if (modifier.IsKind(SyntaxKind.PublicKeyword)) return "public";
            if (modifier.IsKind(SyntaxKind.InternalKeyword)) return "internal";
            if (modifier.IsKind(SyntaxKind.PrivateKeyword)) return "private";
            if (modifier.IsKind(SyntaxKind.ProtectedKeyword)) return "protected";
        }
        return "public";
    }

    // Pull the indentation prefix from the original method's leading trivia
    // so the rendered replacement lines up with surrounding members.
    private static string ExtractLeadingIndentation(MethodDeclarationSyntax method)
    {
        var leadingText = method.GetLeadingTrivia().ToFullString();
        var lastNewline = leadingText.LastIndexOfAny(['\n', '\r']);
        var indent = lastNewline >= 0 ? leadingText[(lastNewline + 1)..] : leadingText;
        return indent.All(char.IsWhiteSpace) && indent.Length > 0 ? indent : "        ";
    }
}
