using System.Globalization;
using System.Text.RegularExpressions;
using Build.Data.BindingGeneration.Models;
using Build.Targets.GenerateBindings.Model;

namespace Build.Targets.GenerateBindings.Translation;

internal sealed record MacroValueClassification(BindingConstant? Constant, MacroConstantReportEntry Report);

internal static partial class MacroValueClassifier
{
    public static MacroValueClassification Classify(
        MacroConstantCandidate candidate,
        MacroExpressionEvaluationContext? context = null)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        var value = candidate.Value.Trim();
        if (IsStringLiteral(value))
        {
            return Emitted(candidate, new BindingConstant(
                candidate.Name,
                new NativeTypeRef(
                    "const char[]",
                    "ReadOnlySpan<byte>",
                    NativeTypeKind.SubstitutedManagedType,
                    PointerDepth: 0,
                    OwningFamilyId: null,
                    SourceHeader: candidate.SourceHeader,
                    AbiShape: NativeAbiShape.Of("ReadOnlySpan<byte>", IntPtr.Size * 2, isBlittable: false),
                    ElementType: null,
                    Diagnostics: []),
                value + "u8",
                ConstantKind.Literal));
        }

        if (TryFormatCharacterLiteral(value, out var characterValue))
        {
            return Emitted(candidate, new BindingConstant(
                candidate.Name,
                NativeTypeRef.Primitive("int", "int", NativeAbiShape.Of("int", 4), candidate.SourceHeader),
                characterValue,
                ConstantKind.Literal));
        }

        if (TryClassifyNumericLiteral(value, out var nativeType, out var managedType))
        {
            var abiSize = managedType == "ulong" ? 8 : 4;
            return Emitted(candidate, new BindingConstant(
                candidate.Name,
                NativeTypeRef.Primitive(nativeType, managedType, NativeAbiShape.Of(managedType, abiSize), candidate.SourceHeader),
                value,
                ConstantKind.Literal));
        }

        if (MacroIntegerExpressionEvaluator.TryEvaluate(value, context) is { } expressionValue)
        {
            var managed = expressionValue.IsUnsigned || expressionValue.Value > int.MaxValue
                ? "uint"
                : "int";
            if (expressionValue.Value > uint.MaxValue)
                managed = "ulong";

            var native = managed switch
            {
                "int" => "int",
                "uint" => "unsigned int",
                _ => "unsigned long long",
            };
            var size = managed == "ulong" ? 8 : 4;
            var formatted = FormatEvaluatedValue(expressionValue, managed);

            return new MacroValueClassification(
                new BindingConstant(
                    candidate.Name,
                    NativeTypeRef.Primitive(native, managed, NativeAbiShape.Of(managed, size), candidate.SourceHeader),
                    formatted,
                    ConstantKind.Computed),
                Report(
                    candidate,
                    "emitted",
                    "safe integer macro expression",
                    "public-expression-constant",
                    managed,
                    formatted,
                    expressionValue.Value.ToString(CultureInfo.InvariantCulture)));
        }

        return new MacroValueClassification(
            null,
            Report(candidate, "unsupported", "unsupported macro expression", "unsupported", null, null, null));
    }

    private static MacroValueClassification Emitted(MacroConstantCandidate candidate, BindingConstant constant) =>
        new(constant, Report(
            candidate,
            "emitted",
            "safe macro value",
            "public-literal-constant",
            constant.Type.ManagedName,
            constant.Value,
            null));

    private static MacroConstantReportEntry Report(
        MacroConstantCandidate candidate,
        string disposition,
        string reason,
        string taxonomy,
        string? emittedType,
        string? emittedValue,
        string? computedValue) =>
        new(
            candidate.Name,
            candidate.SourceHeader,
            candidate.ParseViewName,
            disposition,
            reason,
            emittedType,
            emittedValue,
            MacroForm(candidate),
            taxonomy,
            candidate.Value,
            computedValue);

    private static string MacroForm(MacroConstantCandidate candidate) =>
        candidate.IsFunctionLike ? "function-like" : "object-like";

    private static bool IsStringLiteral(string value) =>
        value.Length >= 2 && value[0] == '"' && value[^1] == '"';

    private static bool TryFormatCharacterLiteral(string value, out string formatted)
    {
        formatted = string.Empty;
        if (value.Length < 3 || value[0] != '\'' || value[^1] != '\'')
            return false;

        var body = value[1..^1];
        var numericValue = body switch
        {
            "\\0" => 0,
            "\\033" => 27,
            "\\x1B" or "\\x1b" => 27,
            { Length: 1 } => body[0],
            _ => -1,
        };

        if (numericValue < 0)
            return false;

        formatted = numericValue.ToString(CultureInfo.InvariantCulture);
        return true;
    }

    private static bool TryClassifyNumericLiteral(string value, out string nativeType, out string managedType)
    {
        nativeType = string.Empty;
        managedType = string.Empty;

        if (!NumericLiteralRegex().IsMatch(value))
            return false;

        if (value.Contains('l', StringComparison.OrdinalIgnoreCase))
            return false;

        var unsigned = value.EndsWith('u') || value.EndsWith('U');
        var digits = unsigned ? value[..^1] : value;
        var isHex = digits.StartsWith("0x", StringComparison.OrdinalIgnoreCase);
        var parseText = isHex ? digits[2..] : digits.TrimStart('-');
        var style = isHex ? NumberStyles.HexNumber : NumberStyles.None;
        if (!ulong.TryParse(parseText, style, CultureInfo.InvariantCulture, out var parsed))
            return false;

        if (!unsigned && digits.StartsWith('-') && parsed <= (ulong)int.MaxValue + 1UL)
        {
            nativeType = "int";
            managedType = "int";
            return true;
        }

        if (!unsigned && parsed <= int.MaxValue)
        {
            nativeType = "int";
            managedType = "int";
            return true;
        }

        if (parsed <= uint.MaxValue)
        {
            nativeType = "unsigned int";
            managedType = "uint";
            return true;
        }

        nativeType = "unsigned long long";
        managedType = "ulong";
        return true;
    }

    private static string FormatEvaluatedValue(MacroIntegerExpressionValue value, string managedType)
    {
        if (value.OriginalLiteralText is not null
            && (managedType == "uint" || managedType == "ulong")
            && value.OriginalLiteralText.Contains("0x", StringComparison.OrdinalIgnoreCase))
        {
            if (managedType == "ulong")
                return value.OriginalLiteralText.TrimEnd('u', 'U') + "UL";

            return value.OriginalLiteralText.EndsWith('u') || value.OriginalLiteralText.EndsWith('U')
                ? value.OriginalLiteralText
                : value.OriginalLiteralText + "u";
        }

        return managedType switch
        {
            "uint" => value.Value.ToString(CultureInfo.InvariantCulture) + "u",
            "ulong" => value.Value.ToString(CultureInfo.InvariantCulture) + "UL",
            _ => value.Value.ToString(CultureInfo.InvariantCulture),
        };
    }

    [GeneratedRegex(@"^-?(?:0x[0-9A-Fa-f]+|[0-9]+)(?:[uU])?$", RegexOptions.CultureInvariant | RegexOptions.NonBacktracking | RegexOptions.ExplicitCapture)]
    private static partial Regex NumericLiteralRegex();
}
