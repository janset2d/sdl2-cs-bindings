using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Janset.SDL2.PostProcess;

// Roslyn-level emit for C `long` and `unsigned long` raw ABI signatures, mode-aware.
//
// Two channels: structural SDL_ThreadID/SDL_GetThreadID family (Constitution §"C `long`"
// Priority C closure R2) AND satellite C `long` surface (SDL_ttf's TTF_OpenFontIndex*,
// TTF_FontFaces - dormant until Item 4 activates TTF).
//
// Mutation strategy: true Roslyn node-level via VisitClassDeclaration returning a
// mutated Members list (one matched method expands to 1 dispatcher + 2 helper
// DllImports on Compat; one [LibraryImport] partial on Modern). SyntaxFactory builds
// attribute lists, parameter lists, body blocks, then preserves source member
// boundary trivia so generated output remains deterministic and readable.
//
// See Constitution §"C `long` And `unsigned long`" Priority C hybrid strategy.
internal sealed class ClongDualDispatchRewriter : CSharpSyntaxRewriter
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
        // Dormant until Item 4 activates TTF in selected_families("all").
        // Constitution §"C `long` And `unsigned long`" (TTF satellite C `long` surface clause).
        "TTF_OpenFontIndex",
        "TTF_OpenFontIndexRW",
        "TTF_OpenFontIndexDPI",
        "TTF_OpenFontIndexDPIRW",
        "TTF_FontFaces",
    };

    private readonly Mode _mode;

    public ClongDualDispatchRewriter(Mode mode)
    {
        _mode = mode;
    }

    public bool AnyChanges { get; private set; }

    public void Reset()
    {
        AnyChanges = false;
    }

    // Inspect the input directory's path segments and choose the emit mode.
    // Mirrors PlatformDeltaPostProcessor.GetPlatformName - defensive throw if
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
            $"clong-dispatch: input directory must contain a 'Compat' or 'Modern' path segment to select emit mode: {inputDir}");
    }

    public override SyntaxNode? VisitClassDeclaration(ClassDeclarationSyntax node)
    {
        var newMembers = new List<MemberDeclarationSyntax>();
        var changed = false;

        foreach (var member in node.Members)
        {
            if (member is MethodDeclarationSyntax method && IsAffectedMethod(method))
            {
                var replacement = _mode == Mode.Modern
                    ? BuildModernMembers(method)
                    : BuildCompatMembers(method);
                newMembers.AddRange(AttachReplacementTrivia(method, replacement));
                changed = true;
            }
            else
            {
                newMembers.Add(member);
            }
        }

        if (!changed)
        {
            return base.VisitClassDeclaration(node);
        }

        AnyChanges = true;
        return node.WithMembers(SyntaxFactory.List(newMembers));
    }

    private static bool IsAffectedMethod(MethodDeclarationSyntax method)
    {
        if (!AffectedMethodNames.Contains(method.Identifier.ValueText))
        {
            return false;
        }

        var returnNativeType = ExtractReturnNativeTypeName(method);
        if (returnNativeType is "SDL_threadID" or "unsigned long" or "long")
        {
            return true;
        }

        return method.ParameterList.Parameters.Any(HasClongAnnotation);
    }

    private static IEnumerable<MemberDeclarationSyntax> BuildModernMembers(MethodDeclarationSyntax method)
    {
        var libPath = ExtractLibraryPath(method) ?? "SDL2";
        var name = method.Identifier.ValueText;
        var returnNativeType = ExtractReturnNativeTypeName(method);
        var clongReturnType = GetClongManagedTypeName(returnNativeType);

        var newParams = method.ParameterList.Parameters.Select(p =>
            GetNativeTypeName(p) is { } nativeType && GetClongManagedTypeName(nativeType) is { } parameterType
                ? p.WithType(SyntaxFactory.IdentifierName(parameterType))
                : p);

        var returnType = clongReturnType is null
            ? method.ReturnType
            : SyntaxFactory.IdentifierName(clongReturnType);

        var attributes = SyntaxFactory.List(new[]
        {
            SyntaxFactory.AttributeList(SyntaxFactory.SingletonSeparatedList(
                SyntaxFactory.Attribute(SyntaxFactory.ParseName("LibraryImport"))
                    .WithArgumentList(SyntaxFactory.ParseAttributeArgumentList(
                        $"(\"{libPath}\", EntryPoint = \"{name}\")")))),
            SyntaxFactory.AttributeList(SyntaxFactory.SingletonSeparatedList(
                SyntaxFactory.Attribute(SyntaxFactory.ParseName("UnmanagedCallConv"))
                    .WithArgumentList(SyntaxFactory.ParseAttributeArgumentList(
                        "(CallConvs = new[] { typeof(CallConvCdecl) })")))),
        });

        if (returnNativeType is { Length: > 0 })
        {
            attributes = attributes.Add(SyntaxFactory.AttributeList(
                    SyntaxFactory.SingletonSeparatedList(
                        SyntaxFactory.Attribute(SyntaxFactory.ParseName("NativeTypeName"))
                            .WithArgumentList(SyntaxFactory.ParseAttributeArgumentList($"(\"{returnNativeType}\")"))))
                .WithTarget(SyntaxFactory.AttributeTargetSpecifier(SyntaxFactory.Token(SyntaxKind.ReturnKeyword))));
        }

        var modifiers = method.Modifiers.Any(SyntaxKind.PartialKeyword)
            ? method.Modifiers
            : method.Modifiers.Add(SyntaxFactory.Token(SyntaxKind.PartialKeyword));
        modifiers = SyntaxFactory.TokenList(modifiers.Where(m => !m.IsKind(SyntaxKind.ExternKeyword)));

        yield return (MemberDeclarationSyntax)SyntaxFactory.MethodDeclaration(returnType, name)
            .WithAttributeLists(attributes)
            .WithModifiers(modifiers)
            .WithParameterList(SyntaxFactory.ParameterList(SyntaxFactory.SeparatedList(newParams)))
            .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken))
            .NormalizeWhitespace();
    }

    private static IEnumerable<MemberDeclarationSyntax> BuildCompatMembers(MethodDeclarationSyntax method)
    {
        var libPath = ExtractLibraryPath(method) ?? "SDL2";
        var name = method.Identifier.ValueText;
        var returnNativeType = ExtractReturnNativeTypeName(method);
        var returnIsUnsignedLong = returnNativeType is "SDL_threadID" or "unsigned long";
        var returnIsClong = returnIsUnsignedLong || returnNativeType is "long";
        var dispatcherReturnType = returnIsClong
            ? SyntaxFactory.IdentifierName(returnIsUnsignedLong ? "ulong" : "long")
            : method.ReturnType;
        var winReturnType = returnIsClong
            ? returnIsUnsignedLong ? "uint" : "int"
            : method.ReturnType.ToString();
        var unixReturnType = returnIsClong ? "nint" : method.ReturnType.ToString();

        ParameterListSyntax BuildHelperParams(string signedLongType, string unsignedLongType) =>
            SyntaxFactory.ParameterList(SyntaxFactory.SeparatedList(
                method.ParameterList.Parameters.Select(p => GetNativeTypeName(p) switch
                {
                    "SDL_threadID" or "unsigned long" => p.WithType(SyntaxFactory.IdentifierName(unsignedLongType)),
                    "long" => p.WithType(SyntaxFactory.IdentifierName(signedLongType)),
                    _ => p,
                })));

        var winArgList = BuildCallArgumentList("int", "uint");
        var unixArgList = BuildCallArgumentList("nint", "nint");
        var unixCall = returnIsClong
            ? $"return ({dispatcherReturnType}){name}_Unix64({unixArgList});"
            : $"return {name}_Unix64({unixArgList});";
        var dispatcherBody = SyntaxFactory.ParseStatement($$"""
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    return {{name}}_Win32({{winArgList}});
                {{unixCall}}
            }
            """);

        var dispatcherModifiers = SyntaxFactory.TokenList(method.Modifiers.Where(m =>
            !m.IsKind(SyntaxKind.ExternKeyword) &&
            !m.IsKind(SyntaxKind.PartialKeyword)));
        var dispatcher = SyntaxFactory.MethodDeclaration(dispatcherReturnType, name)
            .WithAttributeLists(BuildReturnAttributeList(returnNativeType))
            .WithModifiers(dispatcherModifiers)
            .WithParameterList(method.ParameterList)
            .WithBody((BlockSyntax)dispatcherBody);

        yield return (MemberDeclarationSyntax)dispatcher.NormalizeWhitespace();
        yield return BuildHelperDllImport("Win32", winReturnType, BuildHelperParams("int", "uint"));
        yield return BuildHelperDllImport("Unix64", unixReturnType, BuildHelperParams("nint", "nint"));

        string BuildCallArgumentList(string signedLongType, string unsignedLongType) =>
            string.Join(", ", method.ParameterList.Parameters.Select(p => GetNativeTypeName(p) switch
            {
                "SDL_threadID" or "unsigned long" => $"({unsignedLongType}){p.Identifier.ValueText}",
                "long" => $"({signedLongType}){p.Identifier.ValueText}",
                _ => p.Identifier.ValueText,
            }));

        MemberDeclarationSyntax BuildHelperDllImport(string suffix, string ridReturnType, ParameterListSyntax parameterList)
        {
            var dllImportAttr = SyntaxFactory.AttributeList(SyntaxFactory.SingletonSeparatedList(
                SyntaxFactory.Attribute(SyntaxFactory.ParseName("DllImport"))
                    .WithArgumentList(SyntaxFactory.ParseAttributeArgumentList(
                        $"(\"{libPath}\", EntryPoint = \"{name}\", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)"))));

            return (MemberDeclarationSyntax)SyntaxFactory.MethodDeclaration(SyntaxFactory.ParseTypeName(ridReturnType), $"{name}_{suffix}")
                .WithAttributeLists(SyntaxFactory.SingletonList(dllImportAttr))
                .WithModifiers(SyntaxFactory.TokenList(
                    SyntaxFactory.Token(SyntaxKind.PrivateKeyword),
                    SyntaxFactory.Token(SyntaxKind.StaticKeyword),
                    SyntaxFactory.Token(SyntaxKind.ExternKeyword)))
                .WithParameterList(parameterList)
                .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken))
                .NormalizeWhitespace();
        }
    }

    private static SyntaxList<AttributeListSyntax> BuildReturnAttributeList(string? returnNativeType)
    {
        if (returnNativeType is not { Length: > 0 })
        {
            return default;
        }

        return SyntaxFactory.SingletonList(SyntaxFactory.AttributeList(
                SyntaxFactory.SingletonSeparatedList(
                    SyntaxFactory.Attribute(SyntaxFactory.ParseName("NativeTypeName"))
                        .WithArgumentList(SyntaxFactory.ParseAttributeArgumentList($"(\"{returnNativeType}\")"))))
            .WithTarget(SyntaxFactory.AttributeTargetSpecifier(SyntaxFactory.Token(SyntaxKind.ReturnKeyword))));
    }

    private static IEnumerable<MemberDeclarationSyntax> AttachReplacementTrivia(
        MethodDeclarationSyntax originalMethod,
        IEnumerable<MemberDeclarationSyntax> replacementMembers)
    {
        var members = replacementMembers.ToList();
        var indentation = ExtractMemberIndentation(originalMethod);

        for (var i = 0; i < members.Count; i++)
        {
            var member = IndentMemberLines(members[i], indentation);
            var leadingTrivia = i == 0
                ? originalMethod.GetLeadingTrivia()
                : SyntaxFactory.TriviaList(
                    SyntaxFactory.EndOfLine("\n"),
                    SyntaxFactory.EndOfLine("\n"),
                    SyntaxFactory.Whitespace(indentation));

            if (i == members.Count - 1)
            {
                member = member.WithTrailingTrivia(originalMethod.GetTrailingTrivia());
            }

            yield return member.WithLeadingTrivia(leadingTrivia);
        }
    }

    private static string ExtractMemberIndentation(MethodDeclarationSyntax method)
    {
        var leadingText = method.GetLeadingTrivia().ToFullString();
        var lastNewline = leadingText.LastIndexOf('\n');
        var indentation = lastNewline >= 0 ? leadingText[(lastNewline + 1)..] : leadingText;
        return indentation.All(char.IsWhiteSpace) && indentation.Length > 0 ? indentation : "        ";
    }

    private static MemberDeclarationSyntax IndentMemberLines(MemberDeclarationSyntax member, string indentation)
    {
        return (MemberDeclarationSyntax)new MemberLineIndentationRewriter(indentation).Visit(member)!;
    }

    private sealed class MemberLineIndentationRewriter(string indentation) : CSharpSyntaxRewriter
    {
        public override SyntaxToken VisitToken(SyntaxToken token)
        {
            return token
                .WithLeadingTrivia(IndentAfterLineFeeds(token.LeadingTrivia))
                .WithTrailingTrivia(IndentAfterLineFeeds(token.TrailingTrivia));
        }

        private SyntaxTriviaList IndentAfterLineFeeds(SyntaxTriviaList trivia)
        {
            if (!trivia.Any(t => t.IsKind(SyntaxKind.EndOfLineTrivia)))
            {
                return trivia;
            }

            var adjusted = new List<SyntaxTrivia>(trivia.Count + 2);
            foreach (var item in trivia)
            {
                adjusted.Add(item);
                if (item.IsKind(SyntaxKind.EndOfLineTrivia))
                {
                    adjusted.Add(SyntaxFactory.Whitespace(indentation));
                }
            }

            return SyntaxFactory.TriviaList(adjusted);
        }
    }

    private static bool HasClongAnnotation(ParameterSyntax param) =>
        GetNativeTypeName(param) is "long" or "unsigned long" or "SDL_threadID";

    private static string? GetNativeTypeName(ParameterSyntax param)
    {
        foreach (var al in param.AttributeLists)
        {
            foreach (var attr in al.Attributes)
            {
                if (attr.Name.ToString() != "NativeTypeName")
                {
                    continue;
                }

                var arg = attr.ArgumentList?.Arguments.FirstOrDefault();
                if (arg?.Expression is LiteralExpressionSyntax lit)
                {
                    return lit.Token.ValueText;
                }
            }
        }

        return null;
    }

    private static string? GetClongManagedTypeName(string? nativeType) => nativeType switch
    {
        "SDL_threadID" or "unsigned long" => "CULong",
        "long" => "CLong",
        _ => null,
    };

    private static string? ExtractReturnNativeTypeName(MethodDeclarationSyntax method)
    {
        foreach (var al in method.AttributeLists)
        {
            if (al.Target?.Identifier.ValueText != "return")
            {
                continue;
            }

            foreach (var attr in al.Attributes)
            {
                if (attr.Name.ToString() != "NativeTypeName")
                {
                    continue;
                }

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
}
