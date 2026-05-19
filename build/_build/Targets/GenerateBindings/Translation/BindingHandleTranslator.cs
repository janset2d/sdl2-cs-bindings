using Build.Targets.GenerateBindings.Model;
using CppAst;

namespace Build.Targets.GenerateBindings.Translation;

internal sealed class BindingHandleTranslator
{
    private readonly BindableDeclarationPolicy _declarationPolicy;
    private readonly NativeTypeClassifier _typeClassifier;

    public BindingHandleTranslator(BindableDeclarationPolicy declarationPolicy, NativeTypeClassifier typeClassifier)
    {
        _declarationPolicy = declarationPolicy ?? throw new ArgumentNullException(nameof(declarationPolicy));
        _typeClassifier = typeClassifier ?? throw new ArgumentNullException(nameof(typeClassifier));
    }

    public IReadOnlyList<BindingHandle> Extract(NativeDeclarationCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);

        var aliasedOpaqueTags = FindAliasedOpaqueTags(catalog.Typedefs);
        var handles = new Dictionary<string, BindingHandle>(StringComparer.Ordinal);

        foreach (var handle in ExtractClasses(catalog.Classes, aliasedOpaqueTags).Concat(ExtractTypedefs(catalog.Typedefs)))
        {
            handles.TryAdd(handle.Name, handle);
        }

        return handles.Values
            .OrderBy(handle => handle.Name, StringComparer.Ordinal)
            .ToList();
    }

    private HashSet<string> FindAliasedOpaqueTags(IEnumerable<CppTypedef> typedefs)
    {
        var tags = new HashSet<string>(StringComparer.Ordinal);

        foreach (var typedef in typedefs.Where(_declarationPolicy.IsBindableOwnedType))
        {
            if (UnwrapQualified(typedef.ElementType) is CppClass cls && IsOpaqueClass(cls))
            {
                tags.Add(cls.Name);
            }
        }

        return tags;
    }

    private IEnumerable<BindingHandle> ExtractClasses(IEnumerable<CppClass> classes, HashSet<string> aliasedOpaqueTags) =>
        classes
            .Where(cls => !aliasedOpaqueTags.Contains(cls.Name) && _declarationPolicy.IsBindableOwnedType(cls))
            .Select(cls => _typeClassifier.Classify(cls, Path.GetFileName(cls.SourceFile ?? string.Empty)))
            .Select(TryCreateHandle)
            .Where(handle => handle is not null)
            .Select(handle => handle!);

    private IEnumerable<BindingHandle> ExtractTypedefs(IEnumerable<CppTypedef> typedefs) =>
        typedefs
            .Where(_declarationPolicy.IsBindableOwnedType)
            .Select(typedef => _typeClassifier.Classify(typedef, Path.GetFileName(typedef.SourceFile ?? string.Empty)))
            .Select(TryCreateHandle)
            .Where(handle => handle is not null)
            .Select(handle => handle!);

    private static BindingHandle? TryCreateHandle(NativeTypeRef type) =>
        type.Kind == NativeTypeKind.OpaqueHandle && type.OwningFamilyId is not null
            ? new BindingHandle(type.ManagedName, type)
            : null;

    private static bool IsOpaqueClass(CppClass cls) =>
        !cls.IsDefinition || (cls.SizeOf == 0 && cls.Fields.Count == 0);

    private static CppType UnwrapQualified(CppType type)
    {
        while (type is CppQualifiedType qualified)
        {
            type = qualified.ElementType;
        }

        return type;
    }
}
