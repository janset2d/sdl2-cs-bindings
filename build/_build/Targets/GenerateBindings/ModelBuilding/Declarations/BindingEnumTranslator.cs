using System.Globalization;
using Build.Targets.GenerateBindings.Model;
using Build.Targets.GenerateBindings.ModelBuilding.Types;
using CppAst;

namespace Build.Targets.GenerateBindings.ModelBuilding.Declarations;

internal sealed class BindingEnumTranslator
{
    private static readonly HashSet<string> KnownFlagsEnumNames = new(StringComparer.Ordinal)
    {
        "SDL_Keymod",
        "SDL_GLcontextFlag",
        "SDL_RendererFlip",
    };

    private readonly BindableDeclarationPolicy _declarationPolicy;
    private readonly NativeTypeClassifier _typeClassifier;

    public BindingEnumTranslator(BindableDeclarationPolicy declarationPolicy, NativeTypeClassifier typeClassifier)
    {
        _declarationPolicy = declarationPolicy ?? throw new ArgumentNullException(nameof(declarationPolicy));
        _typeClassifier = typeClassifier ?? throw new ArgumentNullException(nameof(typeClassifier));
    }

    public IReadOnlyList<BindingEnumeration> Extract(IReadOnlyList<CppEnum> enumerations)
    {
        ArgumentNullException.ThrowIfNull(enumerations);

        return enumerations
            .Where(IsBindableEnum)
            .OrderBy(enumeration => enumeration.Name, StringComparer.Ordinal)
            .Select(Translate)
            .ToList();
    }

    private bool IsBindableEnum(CppEnum enumeration) =>
        _declarationPolicy.IsBindableEnum(enumeration);

    private BindingEnumeration Translate(CppEnum enumeration)
    {
        var sourceHeader = enumeration.SourceFile.ToSourceHeaderName();
        var underlyingType = string.Equals(enumeration.Name, "SDL_bool", StringComparison.Ordinal)
            ? NativeTypeRef.Primitive("int", "int", NativeAbiShape.Of("int", 4), sourceHeader)
            : _typeClassifier.Classify(enumeration.IntegerType ?? CppPrimitiveType.Int, sourceHeader);
        var memberNames = enumeration.Items
            .Select(item => item.Name)
            .ToHashSet(StringComparer.Ordinal);
        var members = enumeration.Items
            .Select(item => new BindingEnumMember(item.Name, FormatValue(item, memberNames)))
            .ToList();

        return new BindingEnumeration(enumeration.Name, underlyingType, IsFlagsEnum(enumeration), members);
    }

    private static bool IsFlagsEnum(CppEnum enumeration) =>
        enumeration.Name.EndsWith("Flags", StringComparison.Ordinal)
        || KnownFlagsEnumNames.Contains(enumeration.Name);

    private static string FormatValue(CppEnumItem item, HashSet<string> memberNames)
    {
        var expression = item.ValueExpression;
        if (expression is null)
        {
            return FormatNumericValue(item);
        }

        var referencedIdentifiers = new HashSet<string>(StringComparer.Ordinal);
        CollectIdentifiers(expression, referencedIdentifiers);
        if (referencedIdentifiers.Any(identifier => !memberNames.Contains(identifier)))
        {
            return FormatNumericValue(item);
        }

        var formatted = FormatExpression(expression);
        return string.IsNullOrWhiteSpace(formatted)
            ? FormatNumericValue(item)
            : formatted;
    }

    private static string FormatNumericValue(CppEnumItem item) =>
        item.Value.ToString(CultureInfo.InvariantCulture);

    private static string FormatExpression(CppExpression expression) =>
        expression switch
        {
            CppBinaryExpression binary when binary.Arguments is [var left, var right] && !string.IsNullOrWhiteSpace(binary.Operator) =>
                $"{FormatExpression(left)} {binary.Operator} {FormatExpression(right)}",
            CppUnaryExpression unary when unary.Arguments is [var argument] && !string.IsNullOrWhiteSpace(unary.Operator) =>
                unary.Operator + FormatExpression(argument),
            CppParenExpression paren when paren.Arguments is [var argument] =>
                $"({FormatExpression(argument)})",
            CppLiteralExpression literal => literal.Value,
            CppRawExpression raw => raw.Text,
            _ => expression.ToString() ?? string.Empty,
        };

    private static void CollectIdentifiers(CppExpression expression, ISet<string> identifiers)
    {
        switch (expression)
        {
            case CppRawExpression raw:
                AddIdentifiers(raw.Text, identifiers);
                break;
            case CppLiteralExpression literal when literal.Kind == CppExpressionKind.DeclRef:
                identifiers.Add(literal.Value);
                break;
        }

        foreach (var argument in expression.Arguments ?? [])
        {
            CollectIdentifiers(argument, identifiers);
        }
    }

    private static void AddIdentifiers(string text, ISet<string> identifiers)
    {
        var index = 0;
        while (index < text.Length)
        {
            if (!IsIdentifierStart(text[index]))
            {
                index++;
                continue;
            }

            var start = index++;
            while (index < text.Length && IsIdentifierPart(text[index]))
            {
                index++;
            }

            identifiers.Add(text[start..index]);
        }
    }

    private static bool IsIdentifierStart(char value) =>
        value is '_' || char.IsAsciiLetter(value);

    private static bool IsIdentifierPart(char value) =>
        IsIdentifierStart(value) || char.IsAsciiDigit(value);
}
