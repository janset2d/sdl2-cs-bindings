using System.Runtime.InteropServices;
using Build.Targets.GenerateBindings.Model;
using Build.Targets.GenerateBindings.Parsing;
using CppAst;

namespace Build.Targets.GenerateBindings.Translation;

internal sealed class BindingStructTranslator
{
    private readonly BindableDeclarationPolicy _declarationPolicy;

    public BindingStructTranslator(BindableDeclarationPolicy declarationPolicy)
    {
        _declarationPolicy = declarationPolicy ?? throw new ArgumentNullException(nameof(declarationPolicy));
    }

    public List<BindingStruct> Extract(IReadOnlyList<CppAstParseResult> parseResults)
    {
        ArgumentNullException.ThrowIfNull(parseResults);

        var seen = new HashSet<(string SourceFile, string Name)>();
        var structs = new List<BindingStruct>();

        foreach (var result in parseResults)
        {
            foreach (var compilation in result.Compilations)
            {
                foreach (var cls in compilation.Classes)
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
            }
        }

        return structs
            .OrderBy(structure => structure.Name, StringComparer.Ordinal)
            .ToList();
    }

    private static IReadOnlyList<BindingStruct> TranslateStruct(CppClass cls, string structName)
    {
        var generatedStructs = new List<BindingStruct>();
        var layout = cls.ClassKind == CppClassKind.Union ? LayoutKind.Explicit : LayoutKind.Sequential;
        int? explicitSize = layout == LayoutKind.Explicit ? cls.SizeOf : null;
        var fields = cls.Fields
            .Select(field => StructFieldTranslator.Translate(
                field,
                structName,
                layout,
                (anonymousClass, generatedName) => generatedStructs.AddRange(TranslateStruct(anonymousClass, generatedName))))
            .ToList();

        return
        [
            new BindingStruct(structName, fields, layout, explicitSize),
            .. generatedStructs,
        ];
    }
}
