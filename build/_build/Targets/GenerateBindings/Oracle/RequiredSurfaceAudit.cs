using System.Globalization;
using System.Numerics;
using Build.Data.BindingGeneration.Models;
using Build.Results;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Build.Targets.GenerateBindings.Oracle;

internal sealed record RequiredSurfaceAudit(IReadOnlyList<RequiredSurfaceResult> Results)
{
    public ValidationReport Findings => new(Results
        .Where(row => row.Status != "present and equal")
        .Select(row => new ValidationCheck(row.Symbol, ValidationSeverity.Warning, $"{row.Backend}: {row.Status}; {row.Evidence}")));

    public static RequiredSurfaceAudit Run(BindingGenerationConfig config, SourceEvidence compat, SourceEvidence modern)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(compat);
        ArgumentNullException.ThrowIfNull(modern);
        var results = new List<RequiredSurfaceResult>();

        foreach (var function in config.RequiredFunctions)
        {
            results.Add(AuditFunction(function, "Compat", compat));
            results.Add(AuditFunction(function, "Modern", modern));
        }

        foreach (var constant in config.RequiredConstants)
        {
            results.Add(AuditConstant(constant, "Compat", compat));
            results.Add(AuditConstant(constant, "Modern", modern));
        }

        return new RequiredSurfaceAudit(results);
    }

    private static RequiredSurfaceResult AuditFunction(RequiredFunctionConfig required, string backend, SourceEvidence source)
    {
        if (source.Status != SourceStatus.Present)
        {
            return new RequiredSurfaceResult(required.Name, backend, "Evidence Missing", "generated backend unavailable");
        }

        var declarations = source.Evidence.Functions
            .Where(function => function.IsNativeImportDeclaration && function.NativeEntryPoint == required.Name)
            .ToArray();

        if (declarations.Length == 0)
        {
            return new RequiredSurfaceResult(required.Name, backend, "not emitted", "no declaration with this native entry point");
        }

        var missing = declarations.Any(function => function.ReturnType.Length == 0 || function.Parameters.Any(parameter => parameter.Type.Length == 0));
        var equal = declarations.All(function => NormalizeType(function.ReturnType) == NormalizeType(required.ReturnType)
            && function.Parameters
                .Select(parameter => NormalizeType(parameter.Type))
                .SequenceEqual(required.Parameters.Select(parameter => NormalizeType(parameter.Type)), StringComparer.Ordinal));
        var expected = $"expected return: {required.ReturnType}; parameters: ({string.Join(", ", required.Parameters.Select(parameter => parameter.Type))})";
        var observed = string.Join("; ", declarations.Select(function =>
            $"managed return: {TypeEvidence(function.ReturnType, function.ReturnNativeTypeName)}; " +
            $"parameters: ({string.Join(", ", function.Parameters.Select(parameter => TypeEvidence(parameter.Type, parameter.NativeTypeName)))})"));
        return new RequiredSurfaceResult(required.Name, backend, Status(missing, equal), $"{expected}; {observed}");
    }

    private static RequiredSurfaceResult AuditConstant(RequiredConstantConfig required, string backend, SourceEvidence source)
    {
        if (source.Status != SourceStatus.Present)
        {
            return new RequiredSurfaceResult(required.Name, backend, "Evidence Missing", "generated backend unavailable");
        }

        var declarations = source.Evidence.Constants.Where(constant => constant.Name == required.Name).ToArray();

        if (declarations.Length == 0)
        {
            return new RequiredSurfaceResult(required.Name, backend, "not emitted", "no generated constant");
        }

        var missing = declarations.Any(constant => constant.Initializer is null);
        var equal = declarations.All(constant => constant.Initializer is { } value
            && ValuesEqual(required.Value, value)
            && Kind(value) == required.Kind);
        var observed = string.Join("; ", declarations.Select(constant =>
            $"managed type: {TypeEvidence(constant.ManagedType, constant.NativeTypeName)}; value: {constant.Initializer ?? "Evidence Missing"}; " +
            $"kind: {Kind(constant.Initializer)?.ToString() ?? "Evidence Missing"}"));
        return new RequiredSurfaceResult(required.Name, backend, Status(missing, equal), $"expected value: {required.Value}; kind: {required.Kind}; {observed}");
    }

    private static string Status(bool missing, bool equal)
    {
        if (missing)
        {
            return "Evidence Missing";
        }

        return equal ? "present and equal" : "present and different";
    }

    private static ConstantKind? Kind(string? text)
    {
        if (text is null)
        {
            return null;
        }

        return IsNumericLiteral(SyntaxFactory.ParseExpression(text)) ? ConstantKind.Literal : ConstantKind.Computed;
    }

    private static bool IsNumericLiteral(ExpressionSyntax expression) => expression switch
    {
        ParenthesizedExpressionSyntax parentheses => IsNumericLiteral(parentheses.Expression),
        PrefixUnaryExpressionSyntax unary when unary.IsKind(SyntaxKind.UnaryPlusExpression) || unary.IsKind(SyntaxKind.UnaryMinusExpression)
            => IsNumericLiteral(unary.Operand),
        LiteralExpressionSyntax literal => literal.IsKind(SyntaxKind.NumericLiteralExpression),
        _ => false
    };

    private static string TypeEvidence(string managed, string native) => native.Length == 0 ? managed : $"{managed} [NativeTypeName: {native}]";

    private static string NormalizeType(string text) => SyntaxFactory.ParseTypeName(text).NormalizeWhitespace().ToFullString();

    private static bool ValuesEqual(string expected, string actual)
    {
        var expectedExpression = SyntaxFactory.ParseExpression(expected);
        var actualExpression = SyntaxFactory.ParseExpression(actual);
        return TryInteger(expectedExpression, out var expectedValue) && TryInteger(actualExpression, out var actualValue)
            ? expectedValue == actualValue
            : expectedExpression.NormalizeWhitespace().ToFullString() == actualExpression.NormalizeWhitespace().ToFullString();
    }

    private static bool TryInteger(ExpressionSyntax expression, out BigInteger value)
    {
        if (expression is ParenthesizedExpressionSyntax parentheses)
        {
            return TryInteger(parentheses.Expression, out value);
        }

        if (expression is PrefixUnaryExpressionSyntax unary && (unary.IsKind(SyntaxKind.UnaryPlusExpression) || unary.IsKind(SyntaxKind.UnaryMinusExpression)))
        {
            var parsed = TryInteger(unary.Operand, out value);
            value = unary.IsKind(SyntaxKind.UnaryMinusExpression) ? -value : value;
            return parsed;
        }

        value = default;

        if (expression is not LiteralExpressionSyntax literal || !literal.IsKind(SyntaxKind.NumericLiteralExpression))
        {
            return false;
        }

        var text = literal.Token.Text.TrimEnd('u', 'U', 'l', 'L');
        return text.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            ? BigInteger.TryParse("0" + text[2..], NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out value)
            : BigInteger.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value);
    }
}
