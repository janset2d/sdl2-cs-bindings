using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Janset.SDL2.PostProcess;

/// <summary>
/// Slice C-B uniform Pattern B opaque handle emit.
///
/// Two input channels:
///   1. Auto-detect: empty `public partial struct X { }` declarations whose
///      name appears in [NativeTypeName("X *")] annotations somewhere.
///   2. Force-opaque: Constitution-bound allow-list (SDL_RWops, SDL_SysWMinfo,
///      SDL_SysWMmsg) — struct body cleared and replaced with Pattern B.
///
/// Pattern B shape per design Decision 1:
///   [StructLayout(LayoutKind.Sequential)]
///   public readonly partial struct X : IEquatable&lt;X&gt;
///   {
///       public X(nint value) { Value = value; }
///       public nint Value { get; }
///       public bool IsNull => Value == 0;
///       public bool IsNotNull => Value != 0;
///       public static X Null => default;
///       public nint DangerousGetHandle() => Value;
///       public bool Equals(X other) => Value == other.Value;
///       public override bool Equals(object? obj) => obj is X other &amp;&amp; Equals(other);
///       public override int GetHashCode() => Value.GetHashCode();
///       public static bool operator ==(X left, X right) => left.Equals(right);
///       public static bool operator !=(X left, X right) => !left.Equals(right);
///       public static explicit operator nint(X value) => value.Value;
///       public static explicit operator X(nint value) => new(value);
///   }
///
/// Reference rewrite: every X* in raw ABI signatures rewrites to X by-value.
/// Double-pointer X** and `out X` positions are preserved.
/// </summary>
internal sealed class OpaqueHandleEmitRewriter : CSharpSyntaxRewriter
{
    // Force-opaque allow-list per Constitution L293-302
    private static readonly HashSet<string> ForceOpaqueNames = new(StringComparer.Ordinal)
    {
        "SDL_RWops",
        "SDL_SysWMinfo",
        "SDL_SysWMmsg",
    };

    private readonly HashSet<string> _knownHandles;

    public OpaqueHandleEmitRewriter(HashSet<string> autoDetectedHandles)
    {
        _knownHandles = new HashSet<string>(autoDetectedHandles, StringComparer.Ordinal);
        foreach (var f in ForceOpaqueNames)
        {
            _knownHandles.Add(f);
        }
    }

    public bool AnyChanges { get; private set; }

    public void Reset() => AnyChanges = false;

    public override SyntaxNode? VisitStructDeclaration(StructDeclarationSyntax node)
    {
        var name = node.Identifier.ValueText;
        if (!_knownHandles.Contains(name))
        {
            return base.VisitStructDeclaration(node);
        }

        AnyChanges = true;
        return BuildPatternBStruct(name, preserveTrivia: node.GetLeadingTrivia());
    }

    /// <summary>
    /// Construct a complete Pattern B typed handle struct declaration.
    /// </summary>
    private static StructDeclarationSyntax BuildPatternBStruct(
        string name, SyntaxTriviaList preserveTrivia)
    {
        var text = $@"[global::System.Runtime.InteropServices.StructLayout(global::System.Runtime.InteropServices.LayoutKind.Sequential)]
public readonly partial struct {name} : global::System.IEquatable<{name}>
{{
    public {name}(nint value) {{ Value = value; }}
    public nint Value {{ get; }}
    public bool IsNull => Value == 0;
    public bool IsNotNull => Value != 0;
    public static {name} Null => default;
    public nint DangerousGetHandle() => Value;
    public bool Equals({name} other) => Value == other.Value;
    public override bool Equals(object? obj) => obj is {name} other && Equals(other);
    public override int GetHashCode() => Value.GetHashCode();
    public static bool operator ==({name} left, {name} right) => left.Equals(right);
    public static bool operator !=({name} left, {name} right) => !left.Equals(right);
    public static explicit operator nint({name} value) => value.Value;
    public static explicit operator {name}(nint value) => new(value);
}}";

        var parsed = SyntaxFactory.ParseCompilationUnit(text);
        var structDecl = (StructDeclarationSyntax)parsed.Members[0];
        return structDecl.WithLeadingTrivia(preserveTrivia);
    }

    /// <summary>
    /// Rewrite pointer references to known handles as by-value.
    /// `SDL_Window*` parameter -> `SDL_Window` (preserves attribute lists).
    /// </summary>
    public override SyntaxNode? VisitParameter(ParameterSyntax node)
    {
        if (node.Type is PointerTypeSyntax ptr &&
            ptr.ElementType is IdentifierNameSyntax id &&
            _knownHandles.Contains(id.Identifier.ValueText))
        {
            AnyChanges = true;
            return node.WithType(id.WithTriviaFrom(ptr));
        }
        return base.VisitParameter(node);
    }

    public override SyntaxNode? VisitMethodDeclaration(MethodDeclarationSyntax node)
    {
        if (node.ReturnType is PointerTypeSyntax ptr &&
            ptr.ElementType is IdentifierNameSyntax id &&
            _knownHandles.Contains(id.Identifier.ValueText))
        {
            AnyChanges = true;
            node = node.WithReturnType(id.WithTriviaFrom(ptr));
        }
        return base.VisitMethodDeclaration(node);
    }

    /// <summary>
    /// Scan the input directory for empty `public partial struct SDL_X { }`
    /// declarations whose name also appears in a [NativeTypeName("X *")]
    /// annotation somewhere. Result is the auto-detect channel input for the
    /// rewriter constructor.
    /// </summary>
    public static HashSet<string> DiscoverAutoDetectedHandles(string inputDir)
    {
        var allFiles = Directory.EnumerateFiles(inputDir, "*.g.cs", SearchOption.AllDirectories);
        var emptyStructs = new HashSet<string>(StringComparer.Ordinal);
        var referencedAsPointer = new HashSet<string>(StringComparer.Ordinal);

        foreach (var file in allFiles)
        {
            var source = File.ReadAllText(file);
            var tree = CSharpSyntaxTree.ParseText(source);
            var root = tree.GetCompilationUnitRoot();

            // Collect empty struct names.
            foreach (var sd in root.DescendantNodes().OfType<StructDeclarationSyntax>())
            {
                if (sd.Members.Count == 0 &&
                    sd.Identifier.ValueText.StartsWith("SDL_", StringComparison.Ordinal))
                {
                    emptyStructs.Add(sd.Identifier.ValueText);
                }
            }

            // Collect [NativeTypeName("X *")] annotations referencing SDL_* as pointer.
            foreach (var attr in root.DescendantNodes().OfType<AttributeSyntax>())
            {
                if (attr.Name.ToString() != "NativeTypeName") continue;
                var firstArg = attr.ArgumentList?.Arguments.FirstOrDefault();
                if (firstArg?.Expression is LiteralExpressionSyntax lit)
                {
                    var raw = lit.Token.ValueText;
                    // Match "X *" or "X*" or "const X *".
                    var stripped = raw.Replace("const ", "").Replace("*", "").Trim();
                    if (stripped.StartsWith("SDL_", StringComparison.Ordinal) &&
                        raw.Contains('*'))
                    {
                        referencedAsPointer.Add(stripped);
                    }
                }
            }
        }

        var result = new HashSet<string>(StringComparer.Ordinal);
        foreach (var name in emptyStructs)
        {
            if (referencedAsPointer.Contains(name))
            {
                result.Add(name);
            }
        }
        return result;
    }
}
