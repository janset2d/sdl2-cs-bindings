using Build.Targets.GenerateBindings.ModelBuilding.Macros;

namespace Build.Tests.Unit.Targets.GenerateBindings.ModelBuilding.Macros;

public sealed class MacroIntegerExpressionEvaluatorTests
{
    [Test]
    public async Task TryEvaluate_Should_Evaluate_Unsigned_Shift()
    {
        var evaluated = MacroIntegerExpressionEvaluator.TryEvaluate("(1u << 6)");

        await Assert.That(evaluated).IsNotNull();
        await Assert.That(evaluated!.Value).IsEqualTo(64UL);
        await Assert.That(evaluated.IsUnsigned).IsTrue();
    }

    [Test]
    public async Task TryEvaluate_Should_Evaluate_Bitwise_Or()
    {
        var evaluated = MacroIntegerExpressionEvaluator.TryEvaluate("(1u << 0) | (1u << 6)");

        await Assert.That(evaluated).IsNotNull();
        await Assert.That(evaluated!.Value).IsEqualTo(65UL);
        await Assert.That(evaluated.IsUnsigned).IsTrue();
    }

    [Test]
    public async Task TryEvaluate_Should_Evaluate_Parenthesized_Hex_Literal()
    {
        var evaluated = MacroIntegerExpressionEvaluator.TryEvaluate("(0x1FFF0000u)");

        await Assert.That(evaluated).IsNotNull();
        await Assert.That(evaluated!.Value).IsEqualTo(0x1FFF0000UL);
        await Assert.That(evaluated.IsUnsigned).IsTrue();
        await Assert.That(evaluated.OriginalLiteralText).IsEqualTo("0x1FFF0000u");
    }

    [Test]
    public async Task TryEvaluate_Should_Reject_Helper_Call_Without_Context()
    {
        var evaluated = MacroIntegerExpressionEvaluator.TryEvaluate("SDL_BUTTON(SDL_BUTTON_LEFT)");

        await Assert.That(evaluated).IsNull();
    }

    [Test]
    public async Task TryEvaluate_Should_Reject_Unknown_Identifier()
    {
        var evaluated = MacroIntegerExpressionEvaluator.TryEvaluate("SDL_UNKNOWN_FLAG | 1u");

        await Assert.That(evaluated).IsNull();
    }

    [Test]
    public async Task TryEvaluate_Should_Resolve_Known_Identifier_With_Context()
    {
        var context = new MacroExpressionEvaluationContext(
            new Dictionary<string, MacroIntegerExpressionValue>(StringComparer.Ordinal)
            {
                ["SDL_BUTTON_LEFT"] = new(1, IsUnsigned: false, OriginalLiteralText: "1"),
            },
            new Dictionary<string, MacroFunctionLikeMacro>(StringComparer.Ordinal));

        var evaluated = MacroIntegerExpressionEvaluator.TryEvaluate("SDL_BUTTON_LEFT", context);

        await Assert.That(evaluated).IsNotNull();
        await Assert.That(evaluated!.Value).IsEqualTo(1UL);
    }

    [Test]
    public async Task TryEvaluate_Should_Evaluate_Whitelisted_Helper_Call_With_Context()
    {
        var context = new MacroExpressionEvaluationContext(
            new Dictionary<string, MacroIntegerExpressionValue>(StringComparer.Ordinal)
            {
                ["SDL_BUTTON_LEFT"] = new(1, IsUnsigned: false, OriginalLiteralText: "1"),
            },
            new Dictionary<string, MacroFunctionLikeMacro>(StringComparer.Ordinal)
            {
                ["SDL_BUTTON"] = new("SDL_BUTTON", ["X"], "(1u << ((X) - 1))"),
            });

        var evaluated = MacroIntegerExpressionEvaluator.TryEvaluate("SDL_BUTTON(SDL_BUTTON_LEFT)", context);

        await Assert.That(evaluated).IsNotNull();
        await Assert.That(evaluated!.Value).IsEqualTo(1UL);
        await Assert.That(evaluated.IsUnsigned).IsTrue();
    }

    [Test]
    public async Task TryEvaluate_Should_Reject_Invalid_Shift_Count()
    {
        var evaluated = MacroIntegerExpressionEvaluator.TryEvaluate("1u << 64");

        await Assert.That(evaluated).IsNull();
    }

    [Test]
    public async Task TryEvaluate_Should_Reject_Shift_Count_Outside_32Bit_Integer_Width()
    {
        var evaluated = MacroIntegerExpressionEvaluator.TryEvaluate("1u << 40");

        await Assert.That(evaluated).IsNull();
    }

    [Test]
    public async Task TryEvaluate_Should_Reject_Signed_Addition_Overflow()
    {
        var evaluated = MacroIntegerExpressionEvaluator.TryEvaluate("2147483647 + 1");

        await Assert.That(evaluated).IsNull();
    }

    [Test]
    public async Task TryEvaluate_Should_Reject_Unsigned_Addition_Overflow()
    {
        var evaluated = MacroIntegerExpressionEvaluator.TryEvaluate("0xffffffffu + 1u");

        await Assert.That(evaluated).IsNull();
    }

    [Test]
    public async Task TryEvaluate_Should_Reject_Negative_Subtraction()
    {
        var evaluated = MacroIntegerExpressionEvaluator.TryEvaluate("1u - 2u");

        await Assert.That(evaluated).IsNull();
    }

    [Test]
    public async Task TryEvaluate_Should_Reject_Helper_Call_With_Wrong_Arity()
    {
        var context = new MacroExpressionEvaluationContext(
            new Dictionary<string, MacroIntegerExpressionValue>(StringComparer.Ordinal),
            new Dictionary<string, MacroFunctionLikeMacro>(StringComparer.Ordinal)
            {
                ["SDL_BUTTON"] = new("SDL_BUTTON", ["X"], "(1u << ((X) - 1))"),
            });

        var evaluated = MacroIntegerExpressionEvaluator.TryEvaluate("SDL_BUTTON(1, 2)", context);

        await Assert.That(evaluated).IsNull();
    }

    [Test]
    public async Task TryEvaluate_Should_Reject_Unsupported_Operators()
    {
        var evaluated = MacroIntegerExpressionEvaluator.TryEvaluate("1u * 2u");

        await Assert.That(evaluated).IsNull();
    }

    [Test]
    public async Task TryEvaluate_Should_Reject_Leftover_Tokens()
    {
        var evaluated = MacroIntegerExpressionEvaluator.TryEvaluate("1u 2u");

        await Assert.That(evaluated).IsNull();
    }
}
