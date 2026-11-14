using System.Globalization;
using System.Text;
using Cake.Core.Diagnostics;
using Cake.Core.IO;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Build.Targets.GenerateBindings.PostProcess;

/// <summary>
/// Emits single-nint opaque handles from configured auto-detect and force-opaque names.
/// Removes all matching struct declarations before the owner emits one consolidated Handles.g.cs,
/// preventing collisions from repeated forward declarations. Consumers retain Core-owned types
/// through parent-namespace resolution and do not emit local copies.
/// Rewrites single pointers in parameters, returns, fields and callbacks to by-value handles.
/// Double pointers and out positions are preserved. The single-nint layout remains pointer-sized.
/// The emitted Equals(object obj) deliberately has no nullable annotation: generated files have
/// no nullable directive, and adding the annotation would produce CS8669.
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
    /// Rewrite single-pointer callback slots <c>delegate*&lt;X*, ...&gt;</c> to
    /// <c>delegate*&lt;X, ...&gt;</c> when X is a known handle. Function pointer
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
    public static async Task<HashSet<string>> DiscoverAutoDetectedHandlesAsync(GeneratedSyntaxFiles files, DirectoryPath inputDir)
    {
        var emptyStructs = new HashSet<string>(StringComparer.Ordinal);
        var pointerUses = new HashSet<string>(StringComparer.Ordinal);

        foreach (var file in files.Enumerate(inputDir))
        {
            var source = await files.ReadAsync(file.Path);
            var root = CSharpSyntaxTree.ParseText(source, CSharpParseOptions.Default).GetCompilationUnitRoot();

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

        foreach (var name in emptyStructs.Where(pointerUses.Contains))
        {
            result.Add(name);
        }

        return result;
    }

    /// <summary>
    /// Compare the syntactic discovery set against the family's own
    /// <c>auto_detect_well_known</c> roster section while allowing pulled handle
    /// names that are used as rewrite data. On mismatch, write a single stderr
    /// warning describing the drift. Non-fatal — drift surfaces as a build-time
    /// warning for the caller to audit.
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
        string family,
        ICakeLog log)
    {
        if (syntacticDetect.Count == 0)
        {
            return;
        }

        var inCodeNotRoster = syntacticDetect.Except(permittedHandleNames, StringComparer.Ordinal).ToList();
        var inRosterNotCode = requiredAutoDetect.Except(syntacticDetect, StringComparer.Ordinal).ToList();

        if (inCodeNotRoster.Count > 0 || inRosterNotCode.Count > 0)
        {
            log.Warning($"uniform-opaque: WARNING - auto-detect roster drift detected for family '{family}'.");

            if (inCodeNotRoster.Count > 0)
            {
                log.Warning($"  In code but not roster: {string.Join(", ", inCodeNotRoster.OrderBy(s => s, StringComparer.Ordinal))}");
            }

            if (inRosterNotCode.Count > 0)
            {
                log.Warning($"  In roster but not code: {string.Join(", ", inRosterNotCode.OrderBy(s => s, StringComparer.Ordinal))}");
            }

            log.Warning($"  Resolution: audit build/manifest.json library_manifests[].binding_generation opaque_handles for family '{family}' " +
                "and update either the JSON or the auto-detect logic to converge.");
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
        sb.AppendLine(CultureInfo.InvariantCulture, $"namespace {namespaceName}");
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
