using System.Text;
using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Janset.SDL2.PostProcess;

/// <summary>
/// Slice C-B uniform Pattern B opaque handle emit.
///
/// Two input channels (both sourced from the canonical roster JSON at
/// <c>spikes/binding-generators/clangsharp/policy/opaque-handle-roster.json</c>):
///   1. Auto-detect: roster-listed empty `public partial struct X { }`
///      declarations whose name appears as `X*` at any raw ABI signature
///      position. Syntactic discovery cross-checks the roster; mismatches
///      surface as a stderr warning (non-fatal).
///   2. Force-opaque: Constitution-bound allow-list (SDL_RWops, SDL_SysWMinfo,
///      SDL_SysWMmsg) — full struct body removed and replaced with Pattern B.
///
/// Architecture (two-phase discovery + emit):
///
///   Phase 1 (rewriter pass, this class):
///     * REMOVE every `partial struct SDL_X { ... }` whose name is in the
///       combined handle set. ClangSharp emits forward declarations of
///       cross-referenced handle types in multiple .g.cs files (e.g.,
///       SDL_SysWMmsg appears in both SDL_events.g.cs and SDL_syswm.g.cs);
///       removing all of them prevents partial-class member collision when
///       the consolidated Handles.g.cs declares the canonical body.
///     * REWRITE pointer references SDL_X* -> SDL_X at method parameter,
///       method return-type, struct field-type, and function-pointer
///       parameter/return positions (single-pointer only; double-pointer
///       SDL_X** and `out SDL_X` are preserved by the structural pattern
///       match). Field-position rewrite is ABI-safe because Pattern B carries
///       a single `nint` field whose layout is bit-identical to a pointer at
///       the corresponding C field offset.
///
///   Phase 2 (orchestrator pass, Program.cs):
///     * In owner mode (handle definitions live here, e.g. Janset.SDL2.Core),
///       write a single consolidated `Handles.g.cs` file containing ONE
///       Pattern B declaration per handle in alphabetical order.
///     * In consumer mode (e.g. Janset.SDL2.Image — references Core handles
///       via ProjectReference; namespace nesting `SDL2.Image` -> `SDL2`
///       makes the unqualified names resolve), skip the file write — Image
///       does not redeclare Core's handle types.
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
///       public override bool Equals(object obj) => obj is X other &amp;&amp; Equals(other);
///       public override int GetHashCode() => Value.GetHashCode();
///       public static bool operator ==(X left, X right) => left.Equals(right);
///       public static bool operator !=(X left, X right) => !left.Equals(right);
///       public static explicit operator nint(X value) => value.Value;
///       public static explicit operator X(nint value) => new(value);
///   }
///
/// Note: `Equals(object obj)` (no `?` annotation) — generated files compile
/// under &lt;Nullable&gt;enable&lt;/Nullable&gt; without `#nullable enable` directives,
/// so the nullable-reference annotation would trigger CS8669. The pattern
/// `obj is X other` already null-safe-shorts when obj is null.
///
/// Reference rewrite: every X* in raw ABI signatures rewrites to X by-value
/// at method parameter, method return-type, struct field-type, and nested
/// function-pointer parameter/return positions. Double-pointer X** and
/// `out X` positions are preserved.
/// </summary>
internal sealed class OpaqueHandleEmitRewriter : CSharpSyntaxRewriter
{
    private readonly HashSet<string> _handleNames;

    public OpaqueHandleEmitRewriter(HashSet<string> handleNames)
    {
        _handleNames = new HashSet<string>(handleNames, StringComparer.Ordinal);
    }

    public bool AnyChanges { get; private set; }

    public void Reset() => AnyChanges = false;

    /// <summary>
    /// Remove any `partial struct X { ... }` whose name is in the handle set.
    /// The consolidated body for the handle lives in the per-directory
    /// Handles.g.cs written by the orchestrator pass.
    /// </summary>
    public override SyntaxNode? VisitStructDeclaration(StructDeclarationSyntax node)
    {
        if (_handleNames.Contains(node.Identifier.ValueText))
        {
            AnyChanges = true;
            return null;
        }

        return base.VisitStructDeclaration(node);
    }

    /// <summary>
    /// Rewrite single-pointer parameter `X*` -> by-value `X` when X is
    /// a known handle. Preserves attribute lists and trivia.
    /// </summary>
    public override SyntaxNode? VisitParameter(ParameterSyntax node)
    {
        if (node.Type is PointerTypeSyntax ptr &&
            ptr.ElementType is IdentifierNameSyntax id &&
            _handleNames.Contains(id.Identifier.ValueText))
        {
            AnyChanges = true;
            return node.WithType(id.WithTriviaFrom(ptr));
        }
        return base.VisitParameter(node);
    }

    /// <summary>
    /// Rewrite single-pointer return type `X*` -> by-value `X` when X
    /// is a known handle.
    /// </summary>
    public override SyntaxNode? VisitMethodDeclaration(MethodDeclarationSyntax node)
    {
        if (node.ReturnType is PointerTypeSyntax ptr &&
            ptr.ElementType is IdentifierNameSyntax id &&
            _handleNames.Contains(id.Identifier.ValueText))
        {
            AnyChanges = true;
            node = node.WithReturnType(id.WithTriviaFrom(ptr));
        }
        return base.VisitMethodDeclaration(node);
    }

    /// <summary>
    /// Rewrite single-pointer struct field type `X* field` -> by-value
    /// `X field` when X is a known handle. Pattern B struct's single-nint
    /// layout is bit-identical to a pointer at the corresponding C field offset,
    /// so this is a pure C# API ergonomics improvement (caller avoids explicit
    /// dereference) with zero ABI change. Double-pointer `SDL_X**` fields are
    /// preserved because their `ElementType` is `PointerTypeSyntax`, not
    /// `IdentifierNameSyntax`, and fail the structural match.
    /// </summary>
    public override SyntaxNode? VisitFieldDeclaration(FieldDeclarationSyntax node)
    {
        var declaration = node.Declaration;
        if (declaration.Type is PointerTypeSyntax ptr &&
            ptr.ElementType is IdentifierNameSyntax id &&
            _handleNames.Contains(id.Identifier.ValueText))
        {
            AnyChanges = true;
            var newType = id.WithTriviaFrom(ptr);
            return node.WithDeclaration(declaration.WithType(newType));
        }
        return base.VisitFieldDeclaration(node);
    }

    /// <summary>
    /// Rewrite single-pointer callback slots `delegate*<X*, ...>` ->
    /// `delegate*<X, ...>` when X is a known handle. Function pointer
    /// parameters include the return type as the final list item, so this covers
    /// callback parameters and callback return values with one structural rule.
    /// </summary>
    public override SyntaxNode? VisitFunctionPointerParameter(FunctionPointerParameterSyntax node)
    {
        if (node.Type is PointerTypeSyntax ptr &&
            ptr.ElementType is IdentifierNameSyntax id &&
            _handleNames.Contains(id.Identifier.ValueText))
        {
            AnyChanges = true;
            return node.WithType(id.WithTriviaFrom(ptr));
        }

        return base.VisitFunctionPointerParameter(node);
    }

    /// <summary>
    /// Syntactic auto-detect: scan every <c>.g.cs</c> file under <paramref name="inputDir"/>
    /// for (a) names declared as empty <c>public partial struct X { }</c> and
    /// (b) names used as pointer type <c>X*</c> at any raw ABI signature position
    /// (method parameter type or return type). The auto-detect roster equals the
    /// intersection of (a) and (b). Used as a drift watchdog against the roster JSON.
    /// </summary>
    public static HashSet<string> DiscoverAutoDetectedHandles(string inputDir)
    {
        var emptyStructs = new HashSet<string>(StringComparer.Ordinal);
        var pointerUses = new HashSet<string>(StringComparer.Ordinal);

        foreach (var file in Directory.EnumerateFiles(inputDir, "*.g.cs", SearchOption.AllDirectories))
        {
            var source = File.ReadAllText(file);
            var root = CSharpSyntaxTree.ParseText(source).GetCompilationUnitRoot();

            // Empty structs that later appear as pointer types are opaque candidates.
            foreach (var sd in root.DescendantNodes().OfType<StructDeclarationSyntax>())
            {
                if (sd.Members.Count == 0)
                {
                    emptyStructs.Add(sd.Identifier.ValueText);
                }
            }

            // Pointer uses in method parameter and return type positions
            foreach (var method in root.DescendantNodes().OfType<MethodDeclarationSyntax>())
            {
                if (method.ReturnType is PointerTypeSyntax retPtr &&
                    retPtr.ElementType is IdentifierNameSyntax retId)
                {
                    pointerUses.Add(retId.Identifier.ValueText);
                }

                foreach (var param in method.ParameterList.Parameters)
                {
                    if (param.Type is PointerTypeSyntax paramPtr &&
                        paramPtr.ElementType is IdentifierNameSyntax paramId)
                    {
                        pointerUses.Add(paramId.Identifier.ValueText);
                    }
                }

                foreach (var param in method.DescendantNodes().OfType<FunctionPointerParameterSyntax>())
                {
                    if (param.Type is PointerTypeSyntax paramPtr &&
                        paramPtr.ElementType is IdentifierNameSyntax paramId)
                    {
                        pointerUses.Add(paramId.Identifier.ValueText);
                    }
                }
            }
        }

        var result = new HashSet<string>(StringComparer.Ordinal);
        foreach (var name in emptyStructs)
        {
            if (pointerUses.Contains(name))
            {
                result.Add(name);
            }
        }
        return result;
    }

    /// <summary>
    /// Parse the canonical family-keyed opaque-handle roster JSON. Returns the
    /// <c>auto_detect_well_known</c> and <c>force_opaque_exceptions</c> name sets
    /// for the requested family. Satellite families also pull Core's handle names
    /// as data so their pointer references can rewrite by value without reading
    /// Core generated files.
    /// </summary>
    public static (HashSet<string> AutoDetect, HashSet<string> ForceOpaque) LoadRoster(
        string rosterPath,
        string family,
        bool includeCoreHandles = true)
    {
        if (!File.Exists(rosterPath))
        {
            throw new FileNotFoundException(
                $"Opaque-handle roster not found at: {rosterPath}. " +
                "Expected at <repo>/spikes/binding-generators/clangsharp/policy/opaque-handle-roster.json.",
                rosterPath);
        }

        using var doc = JsonDocument.Parse(File.ReadAllText(rosterPath));
        var root = doc.RootElement;
        var families = root.GetProperty("families");

        var autoDetect = new HashSet<string>(StringComparer.Ordinal);
        var forceOpaque = new HashSet<string>(StringComparer.Ordinal);

        AppendFamilyEntries(families.GetProperty(family), autoDetect, forceOpaque);

        if (includeCoreHandles && !family.Equals("core", StringComparison.Ordinal))
        {
            AppendFamilyEntries(families.GetProperty("core"), autoDetect, forceOpaque);
        }

        return (autoDetect, forceOpaque);
    }

    public static (HashSet<string> AutoDetect, HashSet<string> ForceOpaque) LoadFamilyOwnedRoster(
        string rosterPath,
        string family)
    {
        return LoadRoster(rosterPath, family, includeCoreHandles: false);
    }

    private static void AppendFamilyEntries(
        JsonElement familyEntry,
        HashSet<string> autoDetect,
        HashSet<string> forceOpaque)
    {
        foreach (var entry in familyEntry.GetProperty("auto_detect_well_known").EnumerateArray())
        {
            autoDetect.Add(entry.GetProperty("name").GetString()!);
        }

        foreach (var entry in familyEntry.GetProperty("force_opaque_exceptions").EnumerateArray())
        {
            forceOpaque.Add(entry.GetProperty("name").GetString()!);
        }
    }

    /// <summary>
    /// Compare the syntactic discovery set against the family's own
    /// <c>auto_detect_well_known</c> roster section while allowing pulled handle
    /// names that are used as rewrite data. On mismatch, write a single stderr
    /// warning describing the drift. Non-fatal — drift surfaces as a build-time
    /// warning per Constitution §"Opaque Handles".
    ///
    /// Skipped when syntactic discovery is empty: such directories are consumers
    /// (e.g. Janset.SDL2.Image) that reference handles declared in another project
    /// rather than defining them. Reporting "in roster but not code" against a
    /// consumer directory would surface every roster entry as a false-positive drift.
    /// </summary>
    public static void ReportDrift(
        HashSet<string> syntacticDetect,
        HashSet<string> requiredAutoDetect,
        HashSet<string> permittedHandleNames,
        string family)
    {
        if (syntacticDetect.Count == 0)
        {
            return;
        }

        var inCodeNotRoster = syntacticDetect.Except(permittedHandleNames, StringComparer.Ordinal).ToList();
        var inRosterNotCode = requiredAutoDetect.Except(syntacticDetect, StringComparer.Ordinal).ToList();

        if (inCodeNotRoster.Count > 0 || inRosterNotCode.Count > 0)
        {
            Console.Error.WriteLine($"uniform-opaque: WARNING - auto-detect roster drift detected for family '{family}'.");
            if (inCodeNotRoster.Count > 0)
            {
                Console.Error.WriteLine($"  In code but not roster: {string.Join(", ", inCodeNotRoster.OrderBy(s => s, StringComparer.Ordinal))}");
            }
            if (inRosterNotCode.Count > 0)
            {
                Console.Error.WriteLine($"  In roster but not code: {string.Join(", ", inRosterNotCode.OrderBy(s => s, StringComparer.Ordinal))}");
            }
            Console.Error.WriteLine($"  Resolution: audit spikes/binding-generators/clangsharp/policy/opaque-handle-roster.json families.{family} section and update either the JSON or the auto-detect logic to converge.");
        }
    }

    /// <summary>
    /// Build the contents of the consolidated <c>Handles.g.cs</c> file. Emits one
    /// Pattern B struct declaration per handle inside the requested namespace, sorted
    /// alphabetically for deterministic output. Modern and Compat output is
    /// byte-identical because Pattern B is nint-based (no CULong, no
    /// mode-specific intrinsics); the csproj routes the two copies to disjoint
    /// TFM sets via conditional <c>&lt;Compile Include&gt;</c>.
    /// </summary>
    public static string BuildHandlesFileContent(IEnumerable<string> handleNamesSorted, string namespaceName)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated>");
        sb.AppendLine("// This file is generated by the Janset.SDL2 spike postprocess (uniform-opaque mode).");
        sb.AppendLine("// One file per Generated/{Modern,Compat} directory; contents are byte-identical.");
        sb.AppendLine("// Edits will be lost on the next regeneration.");
        sb.AppendLine("// </auto-generated>");
        sb.AppendLine();
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Runtime.InteropServices;");
        sb.AppendLine();
        sb.AppendLine($"namespace {namespaceName}");
        sb.AppendLine("{");

        var first = true;
        foreach (var name in handleNamesSorted)
        {
            if (!first)
            {
                sb.AppendLine();
            }
            sb.AppendLine(BuildPatternBStructText(name));
            first = false;
        }

        sb.AppendLine("}");
        return sb.ToString();
    }

    /// <summary>
    /// Render one Pattern B struct declaration as a string, indented for placement
    /// inside the requested namespace block.
    /// </summary>
    private static string BuildPatternBStructText(string name)
    {
        return $@"    [StructLayout(LayoutKind.Sequential)]
    public readonly partial struct {name} : IEquatable<{name}>
    {{
        public {name}(nint value) {{ Value = value; }}
        public nint Value {{ get; }}
        public bool IsNull => Value == 0;
        public bool IsNotNull => Value != 0;
        public static {name} Null => default;
        public nint DangerousGetHandle() => Value;
        public bool Equals({name} other) => Value == other.Value;
        public override bool Equals(object obj) => obj is {name} other && Equals(other);
        public override int GetHashCode() => Value.GetHashCode();
        public static bool operator ==({name} left, {name} right) => left.Equals(right);
        public static bool operator !=({name} left, {name} right) => !left.Equals(right);
        public static explicit operator nint({name} value) => value.Value;
        public static explicit operator {name}(nint value) => new(value);
    }}";
    }
}
