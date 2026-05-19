using System.Globalization;

namespace Build.Targets.GenerateBindings.Translation;

internal sealed record MacroIntegerExpressionValue(
    ulong Value,
    bool IsUnsigned,
    string? OriginalLiteralText);

internal static class MacroIntegerExpressionEvaluator
{
    private const int MaxFunctionExpansionDepth = 32;
    private const ulong MaxSignedIntegerValue = int.MaxValue;
    private const ulong MaxUnsignedIntegerValue = uint.MaxValue;
    private const ulong MaxShiftCountForSupportedIntegerWidth = 31;

    public static MacroIntegerExpressionValue? TryEvaluate(
        string expression,
        MacroExpressionEvaluationContext? context = null,
        IReadOnlyDictionary<string, MacroIntegerExpressionValue>? locals = null)
    {
        ArgumentNullException.ThrowIfNull(expression);

        return TryEvaluate(expression, context, locals, functionExpansionDepth: 0);
    }

    private static MacroIntegerExpressionValue? TryEvaluate(
        string expression,
        MacroExpressionEvaluationContext? context,
        IReadOnlyDictionary<string, MacroIntegerExpressionValue>? locals,
        int functionExpansionDepth)
    {
        if (functionExpansionDepth > MaxFunctionExpansionDepth)
            return null;

        var parser = new Parser(
            expression,
            context ?? EmptyContext,
            locals ?? EmptyLocals,
            functionExpansionDepth);
        var value = parser.ParseExpression();
        return value is not null && parser.IsAtEnd ? value : null;
    }

    private static readonly MacroExpressionEvaluationContext EmptyContext = new(
        new Dictionary<string, MacroIntegerExpressionValue>(StringComparer.Ordinal),
        new Dictionary<string, MacroFunctionLikeMacro>(StringComparer.Ordinal));

    private static readonly IReadOnlyDictionary<string, MacroIntegerExpressionValue> EmptyLocals =
        new Dictionary<string, MacroIntegerExpressionValue>(StringComparer.Ordinal);

    private sealed class Parser(
        string text,
        MacroExpressionEvaluationContext context,
        IReadOnlyDictionary<string, MacroIntegerExpressionValue> locals,
        int functionExpansionDepth)
    {
        private int _position;

        public bool IsAtEnd
        {
            get
            {
                SkipWhitespace();
                return _position == text.Length;
            }
        }

        public MacroIntegerExpressionValue? ParseExpression() => ParseOr();

        private MacroIntegerExpressionValue? ParseOr()
        {
            var left = ParseShift();
            if (left is null)
                return null;

            while (true)
            {
                SkipWhitespace();
                if (!TryConsume('|'))
                    return left;

                var right = ParseShift();
                if (right is null)
                    return null;

                var resultIsUnsigned = left.IsUnsigned || right.IsUnsigned;
                var value = left.Value | right.Value;
                left = CreateComputedValue(value, resultIsUnsigned);
                if (left is null)
                    return null;
            }
        }

        private MacroIntegerExpressionValue? ParseShift()
        {
            var left = ParseAdditive();
            if (left is null)
                return null;

            while (true)
            {
                SkipWhitespace();
                if (!TryConsume("<<"))
                    return left;

                var right = ParseAdditive();
                if (right is null || right.Value > MaxShiftCountForSupportedIntegerWidth)
                    return null;

                var value = left.Value << (int)right.Value;
                left = CreateComputedValue(value, left.IsUnsigned);
                if (left is null)
                    return null;
            }
        }

        private MacroIntegerExpressionValue? ParseAdditive()
        {
            var left = ParsePrimary();
            if (left is null)
                return null;

            while (true)
            {
                SkipWhitespace();
                if (TryConsume('+'))
                {
                    var right = ParsePrimary();
                if (right is null || WouldAddOverflow(left.Value, right.Value))
                    return null;

                var resultIsUnsigned = left.IsUnsigned || right.IsUnsigned;
                left = CreateComputedValue(left.Value + right.Value, resultIsUnsigned);
                if (left is null)
                    return null;
                continue;
            }

                if (TryConsume('-'))
                {
                    var right = ParsePrimary();
                    if (right is null || right.Value > left.Value)
                        return null;

                var resultIsUnsigned = left.IsUnsigned || right.IsUnsigned;
                left = CreateComputedValue(left.Value - right.Value, resultIsUnsigned);
                if (left is null)
                    return null;
                continue;
            }

                return left;
            }
        }

        private MacroIntegerExpressionValue? ParsePrimary()
        {
            SkipWhitespace();
            if (TryConsume('('))
            {
                var inner = ParseExpression();
                SkipWhitespace();
                return inner is not null && TryConsume(')') ? inner : null;
            }

            return ParseIntegerLiteral() ?? ParseIdentifierOrFunctionCall();
        }

        private MacroIntegerExpressionValue? ParseIntegerLiteral()
        {
            SkipWhitespace();
            var start = _position;

            if (TryConsume("0x") || TryConsume("0X"))
            {
                while (_position < text.Length && Uri.IsHexDigit(text[_position]))
                    _position++;

                if (_position == start + 2)
                    return null;

                _ = TryConsumeUnsignedSuffix();
                var literal = text[start.._position];
                var digits = literal[2..].TrimEnd('u', 'U');
                return ulong.TryParse(digits, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var value)
                    ? CreateLiteralValue(value, literal.EndsWith('u') || literal.EndsWith('U'), literal)
                    : null;
            }

            while (_position < text.Length && char.IsAsciiDigit(text[_position]))
                _position++;

            if (_position == start)
                return null;

            _ = TryConsumeUnsignedSuffix();
            var decimalLiteral = text[start.._position];
            var decimalDigits = decimalLiteral.TrimEnd('u', 'U');
            return ulong.TryParse(decimalDigits, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed)
                ? CreateLiteralValue(parsed, decimalLiteral.EndsWith('u') || decimalLiteral.EndsWith('U'), decimalLiteral)
                : null;
        }

        private MacroIntegerExpressionValue? ParseIdentifierOrFunctionCall()
        {
            SkipWhitespace();
            var start = _position;
            if (_position >= text.Length || !(char.IsAsciiLetter(text[_position]) || text[_position] == '_'))
                return null;

            _position++;
            while (_position < text.Length && (char.IsAsciiLetterOrDigit(text[_position]) || text[_position] == '_'))
                _position++;

            var name = text[start.._position];
            SkipWhitespace();
            if (!TryConsume('('))
            {
                if (locals.TryGetValue(name, out var local))
                    return local;

                return context.Constants.TryGetValue(name, out var known) ? known : null;
            }

            if (!context.Functions.TryGetValue(name, out var function))
                return null;

            var arguments = new List<MacroIntegerExpressionValue>();
            SkipWhitespace();
            if (!TryConsume(')'))
            {
                while (true)
                {
                    var argument = ParseExpression();
                    if (argument is null)
                        return null;

                    arguments.Add(argument);

                    SkipWhitespace();
                    if (TryConsume(')'))
                        break;

                    if (!TryConsume(','))
                        return null;
                }
            }

            if (arguments.Count != function.Parameters.Count)
                return null;

            var callLocals = new Dictionary<string, MacroIntegerExpressionValue>(StringComparer.Ordinal);
            for (var index = 0; index < function.Parameters.Count; index++)
                callLocals[function.Parameters[index]] = arguments[index];

            return TryEvaluate(function.Body, context, callLocals, functionExpansionDepth + 1);
        }

        private static bool WouldAddOverflow(ulong left, ulong right) =>
            ulong.MaxValue - left < right;

        private static MacroIntegerExpressionValue? CreateLiteralValue(ulong value, bool isUnsigned, string literal) =>
            IsInSupportedIntegerRange(value, isUnsigned)
                ? new MacroIntegerExpressionValue(value, isUnsigned, literal)
                : null;

        private static MacroIntegerExpressionValue? CreateComputedValue(ulong value, bool isUnsigned) =>
            IsInSupportedIntegerRange(value, isUnsigned)
                ? new MacroIntegerExpressionValue(value, isUnsigned, OriginalLiteralText: null)
                : null;

        private static bool IsInSupportedIntegerRange(ulong value, bool isUnsigned) =>
            isUnsigned ? value <= MaxUnsignedIntegerValue : value <= MaxSignedIntegerValue;

        private bool TryConsumeUnsignedSuffix()
        {
            if (_position >= text.Length || text[_position] is not ('u' or 'U'))
                return false;

            _position++;
            return true;
        }

        private bool TryConsume(char value)
        {
            if (_position >= text.Length || text[_position] != value)
                return false;

            _position++;
            return true;
        }

        private bool TryConsume(string value)
        {
            if (!text.AsSpan(_position).StartsWith(value, StringComparison.Ordinal))
                return false;

            _position += value.Length;
            return true;
        }

        private void SkipWhitespace()
        {
            while (_position < text.Length && char.IsWhiteSpace(text[_position]))
                _position++;
        }
    }
}
