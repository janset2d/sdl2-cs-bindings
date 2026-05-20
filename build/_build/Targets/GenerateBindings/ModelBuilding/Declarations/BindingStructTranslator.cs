using System.Runtime.InteropServices;
using Build.Targets.GenerateBindings.Model;
using Build.Targets.GenerateBindings.ModelBuilding.Types;
using Build.Targets.GenerateBindings.Parse;
using CppAst;

namespace Build.Targets.GenerateBindings.ModelBuilding.Declarations;

internal sealed class BindingStructTranslator
{
    private readonly BindableDeclarationPolicy _declarationPolicy;
    private readonly StructFieldTranslator _fieldTranslator;

    public BindingStructTranslator(BindableDeclarationPolicy declarationPolicy, NativeTypeClassifier typeClassifier)
    {
        _declarationPolicy = declarationPolicy ?? throw new ArgumentNullException(nameof(declarationPolicy));
        ArgumentNullException.ThrowIfNull(typeClassifier);
        _fieldTranslator = new StructFieldTranslator(typeClassifier);
    }

    public List<BindingStruct> Extract(IReadOnlyList<CppAstParseResult> parseResults)
    {
        ArgumentNullException.ThrowIfNull(parseResults);

        var classes = parseResults
            .SelectMany(result => result.Compilations)
            .SelectMany(compilation => compilation.Classes);
        return ExtractClasses(classes);
    }

    public List<BindingStruct> Extract(NativeDeclarationCatalog declarationCatalog)
    {
        ArgumentNullException.ThrowIfNull(declarationCatalog);

        return ExtractClasses(declarationCatalog.Classes);
    }

    private List<BindingStruct> ExtractClasses(IEnumerable<CppClass> classes)
    {
        var seen = new HashSet<(string SourceFile, string Name)>();
        var structs = new List<BindingStruct>();

        foreach (var cls in classes)
        {
            if (!_declarationPolicy.IsBindableStruct(cls))
            {
                continue;
            }

            var key = (cls.SourceFile ?? string.Empty, cls.Name);
            if (!seen.Add(key))
            {
                continue;
            }

            structs.AddRange(TranslateStruct(cls, cls.Name));
        }

        return structs
            .OrderBy(structure => structure.Name, StringComparer.Ordinal)
            .ToList();
    }

    private IReadOnlyList<BindingStruct> TranslateStruct(CppClass cls, string structName, string? inheritedSourceHeader = null)
    {
        var generatedStructs = new List<BindingStruct>();
        var layout = cls.ClassKind == CppClassKind.Union ? LayoutKind.Explicit : LayoutKind.Sequential;
        int? explicitSize = layout == LayoutKind.Explicit ? cls.SizeOf : null;
        var ownSourceHeader = cls.SourceFile.ToSourceHeaderName();
        var sourceHeader = string.IsNullOrWhiteSpace(ownSourceHeader) ? inheritedSourceHeader : ownSourceHeader;
        var fields = cls.Fields
            .Select(field => _fieldTranslator.Translate(
                field,
                structName,
                layout,
                (anonymousClass, generatedName) => generatedStructs.AddRange(TranslateStruct(anonymousClass, generatedName, sourceHeader)),
                sourceHeader))
            .ToList();

        return
        [
            new BindingStruct(structName, fields, layout, explicitSize),
            .. generatedStructs,
        ];
    }
}
