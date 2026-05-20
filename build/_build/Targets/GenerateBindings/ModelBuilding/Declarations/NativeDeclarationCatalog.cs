using CppAst;

namespace Build.Targets.GenerateBindings.ModelBuilding.Declarations;

/// <summary>
/// Flattened, deduplicated view of all native declarations collected from a
/// set of CppAst parse results. Serves as a shared lookup surface for
/// translators that need to resolve cross-declaration relationships (e.g.
/// handle vs struct discrimination when a pointee is declared in a different
/// header than the pointer site).
/// </summary>
internal sealed record NativeDeclarationCatalog(
    IReadOnlyList<CppFunction> Functions,
    IReadOnlyList<CppClass> Classes,
    IReadOnlyList<CppEnum> Enums,
    IReadOnlyList<CppTypedef> Typedefs);
