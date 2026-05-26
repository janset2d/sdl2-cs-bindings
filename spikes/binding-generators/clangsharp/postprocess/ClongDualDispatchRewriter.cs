using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Janset.SDL2.PostProcess;

// R2 structural-symbol hybrid emit for the SDL_threadID family, mode-aware.
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
// Mode-aware emit. The project's csproj routes Generated/Compat to
// netstandard2.0 + net462 only, and Generated/Modern to net6+ only via
// conditional <Compile Include>. Each output file therefore only ever
// compiles under one TFM range. We emit a single branch matching the
// destination — no `#if` directives are needed, mirroring the existing
// `libraryimport` postprocess pattern (Compat keeps [DllImport]; Modern
// gets [LibraryImport]).
//
// Modern emit (single form, requires net6+):
//
//   [LibraryImport("SDL2", EntryPoint = "SDL_ThreadID")]
//   [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
//   [return: NativeTypeName("SDL_threadID")]
//   public static partial CULong SDL_ThreadID();
//
// Compat emit (single form, legacy TFMs only): managed wrapper +
// RuntimeInformation.IsOSPlatform dispatch between two private DllImports
// returning uint (Windows LLP64: C unsigned long = 32-bit) and nint
// (Unix LP64: C unsigned long = 64-bit). Microsoft's documented
// cross-platform C-long pattern.
//
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
//
// Mode is auto-detected from the input directory path (mirrors
// PlatformDeltaPostProcessor's pattern); a path segment of `Compat`
// selects Compat, `Modern` selects Modern. Missing both throws.
//
// Mechanism: the rewriter cannot rely on Roslyn's syntax-tree mutation to
// inject the replacement attribute lists + extra member declarations cleanly
// in one pass (multiple members per source method), so we stay with the
// source-text substitution approach — collect detected methods during the
// syntax walk, then rewrite at the source-text level in VisitCompilationUnit
// by computing each method's text span and substituting a verbatim raw block.
//
// Refs: Constitution §"C `long` And `unsigned long`" Priority C hybrid
// strategy and Priority C closure summary R2.
internal sealed class ThreadIdDualDispatchRewriter : CSharpSyntaxRewriter
{
    internal enum Mode
    {
        Compat,
        Modern,
    }

    private static readonly HashSet<string> AffectedMethodNames = new(StringComparer.Ordinal)
    {
        "SDL_ThreadID",
        "SDL_GetThreadID",
    };

    private readonly Mode _mode;
    private readonly List<(TextSpan FullSpan, string Replacement)> _pending = new();

    public ThreadIdDualDispatchRewriter(Mode mode)
    {
        _mode = mode;
    }

    public bool AnyChanges { get; private set; }

    public void Reset()
    {
        AnyChanges = false;
        _pending.Clear();
    }

    // Inspect the input directory's path segments and choose the emit mode.
    // Mirrors PlatformDeltaPostProcessor.GetPlatformName — defensive throw if
    // neither segment is present so a mis-pointed CLI fails loudly rather
    // than silently producing the wrong shape.
    public static Mode DetectMode(string inputDir)
    {
        var parts = inputDir.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        for (var i = parts.Length - 1; i >= 0; i--)
        {
            if (string.Equals(parts[i], "Compat", StringComparison.Ordinal))
            {
                return Mode.Compat;
            }
            if (string.Equals(parts[i], "Modern", StringComparison.Ordinal))
            {
                return Mode.Modern;
            }
        }

        throw new InvalidOperationException(
            $"threadid-dispatch: input directory must contain a 'Compat' or 'Modern' path segment to select emit mode: {inputDir}");
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
        var replacement = _mode == Mode.Modern
            ? BuildModernReplacement(name, libPath, accessModifier, returnNativeType, paramList, indent)
            : BuildCompatReplacement(name, libPath, accessModifier, returnNativeType, paramList, indent);

        // Stage a text-level substitution. We use FullSpan (which includes
        // leading trivia: blank line + indentation) so the replacement controls
        // its own preamble whitespace.
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

    private static string BuildModernReplacement(
        string name,
        string libPath,
        string accessModifier,
        string returnNativeType,
        string paramList,
        string indent)
    {
        // Single LibraryImport + CULong form. Mirrors the post-libraryimport
        // output shape so the file remains visually consistent with the
        // surrounding members. `using System.Runtime.InteropServices;` is
        // already present in every Modern output (DllImportToLibraryImportRewriter
        // ensures it), so unqualified names work without extra using insertion.
        var sb = new StringBuilder();
        sb.AppendLine();
        sb.Append(indent).AppendLine($"[LibraryImport(\"{libPath}\", EntryPoint = \"{name}\")]");
        sb.Append(indent).AppendLine("[UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]");
        sb.Append(indent).AppendLine($"[return: NativeTypeName(\"{returnNativeType}\")]");
        sb.Append(indent).Append($"{accessModifier} static partial CULong {name}{paramList};").AppendLine();
        return sb.ToString();
    }

    private static string BuildCompatReplacement(
        string name,
        string libPath,
        string accessModifier,
        string returnNativeType,
        string paramList,
        string indent)
    {
        // Managed dispatch wrapper + 2 private DllImports. RuntimeInformation /
        // OSPlatform / DllImport / CallingConvention all live under
        // System.Runtime.InteropServices — the Compat outputs already `using`
        // that namespace (ClangSharp emits it), so unqualified spellings work.
        var sb = new StringBuilder();
        sb.AppendLine();
        sb.Append(indent).AppendLine($"[return: NativeTypeName(\"{returnNativeType}\")]");
        sb.Append(indent).AppendLine($"{accessModifier} static ulong {name}{paramList}");
        sb.Append(indent).AppendLine("{");
        sb.Append(indent).AppendLine("    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))");
        sb.Append(indent).AppendLine($"        return {name}_Win32{StripParamTypes(paramList)};");
        sb.Append(indent).AppendLine($"    return (ulong){name}_Unix64{StripParamTypes(paramList)};");
        sb.Append(indent).AppendLine("}");
        sb.AppendLine();
        sb.Append(indent).AppendLine($"[DllImport(\"{libPath}\", EntryPoint = \"{name}\", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]");
        sb.Append(indent).AppendLine($"private static extern uint {name}_Win32{paramList};");
        sb.AppendLine();
        sb.Append(indent).AppendLine($"[DllImport(\"{libPath}\", EntryPoint = \"{name}\", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]");
        sb.Append(indent).Append($"private static extern nint {name}_Unix64{paramList};").AppendLine();
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
