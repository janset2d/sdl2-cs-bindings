using Build.Targets.GenerateBindings.Parse;
using CppAst;

namespace Build.Targets.GenerateBindings.ModelBuilding.Declarations;

/// <summary>
/// Builds a <see cref="NativeDeclarationCatalog"/> from a set of
/// <see cref="CppAstParseResult"/> instances, deduplicating declarations by
/// source file and name. Functions are filtered through
/// <see cref="BindableDeclarationPolicy.IsBindableFunction"/>; classes,
/// enums, and typedefs are collected from all compilations.
/// </summary>
internal sealed class NativeDeclarationCatalogBuilder
{
    private readonly BindableDeclarationPolicy _declarationPolicy;

    public NativeDeclarationCatalogBuilder(BindableDeclarationPolicy declarationPolicy)
    {
        _declarationPolicy = declarationPolicy ?? throw new ArgumentNullException(nameof(declarationPolicy));
    }

    public NativeDeclarationCatalog Build(IReadOnlyList<CppAstParseResult> parseResults)
    {
        ArgumentNullException.ThrowIfNull(parseResults);

        var seenFunctions = new HashSet<(string, string)>();
        var seenClasses = new HashSet<(string, string)>();
        var seenEnums = new HashSet<(string, string)>();
        var seenTypedefs = new HashSet<(string, string)>();
        var functions = new List<CppFunction>();
        var classes = new List<CppClass>();
        var enums = new List<CppEnum>();
        var typedefs = new List<CppTypedef>();

        foreach (var result in parseResults)
        {
            foreach (var compilation in result.Compilations)
            {
                foreach (var fn in compilation.Functions)
                {
                    if (!_declarationPolicy.IsBindableFunction(fn))
                        continue;

                    var key = (fn.SourceFile ?? string.Empty, fn.Name);
                    if (seenFunctions.Add(key))
                        functions.Add(fn);
                }

                foreach (var cls in compilation.Classes)
                {
                    var key = (cls.SourceFile ?? string.Empty, cls.Name);
                    if (seenClasses.Add(key))
                        classes.Add(cls);
                }

                foreach (var enumeration in compilation.Enums)
                {
                    var key = (enumeration.SourceFile ?? string.Empty, enumeration.Name);
                    if (seenEnums.Add(key))
                        enums.Add(enumeration);
                }

                foreach (var typedef in compilation.Typedefs)
                {
                    var key = (typedef.SourceFile ?? string.Empty, typedef.Name);
                    if (seenTypedefs.Add(key))
                        typedefs.Add(typedef);
                }
            }
        }

        return new NativeDeclarationCatalog(
            SortBySourceAndName(functions, function => function.Name),
            SortBySourceAndName(classes, cls => cls.Name),
            SortBySourceAndName(enums, enumeration => enumeration.Name),
            SortBySourceAndName(typedefs, typedef => typedef.Name));
    }

    private static List<T> SortBySourceAndName<T>(IEnumerable<T> declarations, Func<T, string> getName)
        where T : CppElement =>
        declarations
            .OrderBy(declaration => declaration.SourceFile ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(getName, StringComparer.Ordinal)
            .ToList();
}
