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

        var handles = new Dictionary<string, BindingHandle>(StringComparer.Ordinal);

        foreach (var handle in ExtractClasses(catalog.Classes).Concat(ExtractTypedefs(catalog.Typedefs)))
        {
            handles.TryAdd(handle.Name, handle);
        }

        return handles.Values
            .OrderBy(handle => handle.Name, StringComparer.Ordinal)
            .ToList();
    }

    private IEnumerable<BindingHandle> ExtractClasses(IEnumerable<CppClass> classes) =>
        classes
            .Where(_declarationPolicy.IsBindableOwnedType)
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
}
