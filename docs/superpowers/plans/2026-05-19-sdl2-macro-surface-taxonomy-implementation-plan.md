# SDL2 Macro Surface Taxonomy Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Extend the source-first macro pipeline so SDL2 public expression constants are generated safely, public helper macros are reported distinctly, non-.NET/internal macros are skipped with explicit reasons, and SDL2-CS/generated differences are auditable.

**Architecture:** Keep the current `GenerateBindings` macro lane: CppAst macro facts flow through `MacroCandidateCollector`, `MacroApiPolicy`, `MacroValueClassifier`, `MacroConstantMerger`, `MacroManualPolicyApplier`, `BindingModel`, and `parse-views.json`. Add one focused expression evaluator used by `MacroValueClassifier`, add report taxonomy for helper candidates, and keep helper method emission out of this plan.

**Tech Stack:** .NET 10, C# 14, Cake Frosting, CppAst, TUnit on Microsoft.Testing.Platform, `tools.cs generate-bindings`, generated binding compile-check, Slopwatch.

---

## Execution Guardrails

- Use TDD. Each production change starts with a failing test.
- Do not commit without Deniz's explicit approval. Commit steps below are approval checkpoints and proposed messages only.
- Use `apply_patch` for manual edits.
- Keep generated-output behavior conservative: emit only deterministic integer expressions; report the rest.
- Every new macro capability must have real `.h` fixture coverage.
- Do not add macro policy entries to `build\manifest.json` unless a real live override/exclude is required.
- Run Slopwatch after code/test/project changes:

```pwsh
slopwatch analyze --fail-on warning --exclude "artifacts/**,external/**,vcpkg_installed/**,**/bin/**,**/obj/**"
```

## File Structure

### Production files to create

- `build\_build\Targets\GenerateBindings\Translation\MacroIntegerExpressionEvaluator.cs`
  - Evaluates a safe subset of integer macro expressions: parentheses, integer literals with unsigned suffixes, known identifiers, whitelisted pure helper calls, additive/subtractive arithmetic, shift operators, and bitwise OR.
- `build\_build\Targets\GenerateBindings\Translation\MacroExpressionEvaluationContext.cs`
  - Carries previously evaluated constants and whitelisted function-like macro bodies for contextual expression resolution.

### Production files to modify

- `build\_build\Targets\GenerateBindings\Translation\MacroValueClassifier.cs`
  - Calls the expression evaluator after literal classification and emits computed integer constants with original-expression evidence.
- `build\_build\Targets\GenerateBindings\Translation\MacroApiPolicy.cs`
  - Distinguishes helper-candidate function-like macros and hardens non-API skip rules.
- `build\_build\Targets\GenerateBindings\Translation\BindingConstantTranslator.cs`
  - Reports helper candidates separately from unsupported macros.
- `build\_build\Targets\GenerateBindings\Model\MacroConstantReport.cs`
  - Adds `HelperCandidateCount`, `HelperDuplicateCoalescedCount`, plus macro form, taxonomy, original expression, and computed value evidence.
- `build\_build\Targets\GenerateBindings\Emitting\BindingParseViewReport.cs`
  - Adds `HelperCandidateCount` and `HelperDuplicateCoalescedCount` to JSON report DTOs.
- `build\_build\Targets\GenerateBindings\Emitting\CsCommandEmitter.cs`
  - Maps `HelperCandidateCount` into `parse-views.json`.

### Test files to create

- `build\_build.Tests\Fixtures\Data\GenerateBindings\MacroConstants\macro-expressions.h`
  - Header fixture for haptic flags, audio masks, button helper macros, window-position helper macros, and internal skip examples.
- `build\_build.Tests\Unit\Targets\GenerateBindings\Translation\MacroIntegerExpressionEvaluatorTests.cs`
  - Focused evaluator tests.
- `build\_build.Tests\Unit\Targets\GenerateBindings\Translation\MacroSurfaceAuditTests.cs`
  - Stable high-value audit expectations for generated/missing/skipped categories.

### Test files to modify

- `build\_build.Tests\Unit\Targets\GenerateBindings\Translation\SemanticHeaderFixtureTests.cs`
- `build\_build.Tests\Unit\Targets\GenerateBindings\Translation\MacroValueClassifierTests.cs`
- `build\_build.Tests\Unit\Targets\GenerateBindings\Translation\MacroApiPolicyTests.cs`
- `build\_build.Tests\Unit\Targets\GenerateBindings\Translation\BindingConstantTranslatorTests.cs`
- `build\_build.Tests\Unit\Targets\GenerateBindings\Emitting\BindingParseViewReportTests.cs`

### Documentation files to modify

- `docs\binding-autogen\binding-api-surface-strategy.md`
- `docs\playbook\binding-generator-maintenance.md`
- `docs\plan.md`

---

## Task 1: Header Fixture and RED Expression Tests

**Files:**
- Create: `build\_build.Tests\Fixtures\Data\GenerateBindings\MacroConstants\macro-expressions.h`
- Modify: `build\_build.Tests\Unit\Targets\GenerateBindings\Translation\SemanticHeaderFixtureTests.cs`
- Modify: `build\_build.Tests\Unit\Targets\GenerateBindings\Translation\MacroValueClassifierTests.cs`
- Modify: `build\_build.Tests\Unit\Targets\GenerateBindings\Translation\BindingConstantTranslatorTests.cs`

- [ ] **Step 1: Add the real header fixture**

Create `build\_build.Tests\Fixtures\Data\GenerateBindings\MacroConstants\macro-expressions.h`:

```c
#ifndef SDL_FIXTURE_MACRO_EXPRESSIONS_H_
#define SDL_FIXTURE_MACRO_EXPRESSIONS_H_

#define SDL_HAPTIC_CONSTANT (1u << 0)
#define SDL_HAPTIC_SINE (1u << 1)
#define SDL_HAPTIC_RAMP (1u << 6)
#define SDL_HAPTIC_PAUSE (1u << 15)

#define SDL_AUDIO_MASK_BITSIZE (0xFF)
#define SDL_AUDIO_MASK_DATATYPE (1 << 8)
#define SDL_AUDIO_MASK_ENDIAN (1 << 12)
#define SDL_AUDIO_MASK_SIGNED (1 << 15)

#define SDL_BUTTON_LEFT 1
#define SDL_BUTTON(X) (1u << ((X) - 1))
#define SDL_BUTTON_LMASK SDL_BUTTON(SDL_BUTTON_LEFT)

#define SDL_WINDOWPOS_UNDEFINED_MASK 0x1FFF0000u
#define SDL_WINDOWPOS_UNDEFINED_DISPLAY(X) (SDL_WINDOWPOS_UNDEFINED_MASK | (X))
#define SDL_WINDOWPOS_UNDEFINED SDL_WINDOWPOS_UNDEFINED_DISPLAY(0)

#define SDL_CACHELINE_SIZE 128
#define SDL_ASSERT_LEVEL 1
#define SDL_PRIs64 "I64d"
#define SDL_REVISION_NUMBER 0

#endif
```

- [ ] **Step 2: Add the fixture parse test**

Append this test to `SemanticHeaderFixtureTests`:

```csharp
[Test]
public async Task Fixtures_Should_Parse_Macro_Expression_Surface()
{
    var compilation = ParseFixture("GenerateBindings/MacroConstants/macro-expressions.h", parseMacros: true);

    var hapticRamp = compilation.Macros.Single(m => m.Name == "SDL_HAPTIC_RAMP");
    await Assert.That(hapticRamp.Parameters).IsEmpty();
    await Assert.That(hapticRamp.Value).Contains("1u");
    await Assert.That(hapticRamp.Value).Contains("6");

    var button = compilation.Macros.Single(m => m.Name == "SDL_BUTTON");
    await Assert.That(button.Parameters).IsEquivalentTo(["X"]);

    var buttonMask = compilation.Macros.Single(m => m.Name == "SDL_BUTTON_LMASK");
    await Assert.That(buttonMask.Parameters).IsEmpty();
    await Assert.That(buttonMask.Value).Contains("SDL_BUTTON");

    var assertLevel = compilation.Macros.Single(m => m.Name == "SDL_ASSERT_LEVEL");
    await Assert.That(assertLevel.Value).IsEqualTo("1");
}
```

- [ ] **Step 3: Add RED classifier tests for safe expressions**

Append these tests to `MacroValueClassifierTests`:

```csharp
[Test]
public async Task Classify_Should_Emit_Unsigned_Shift_Expression_As_Uint()
{
    var result = MacroValueClassifier.Classify(Candidate("SDL_HAPTIC_RAMP", "(1u << 6)"));

    await Assert.That(result.Constant).IsNotNull();
    await Assert.That(result.Constant!.Type.ManagedName).IsEqualTo("uint");
    await Assert.That(result.Constant.Value).IsEqualTo("64u");
    await Assert.That(result.Report.Disposition).IsEqualTo("emitted");
    await Assert.That(result.Report.Reason).IsEqualTo("safe integer macro expression");
}

[Test]
public async Task Classify_Should_Emit_Signed_Shift_Expression_As_Int_When_Value_Fits_Int()
{
    var result = MacroValueClassifier.Classify(Candidate("SDL_AUDIO_MASK_SIGNED", "(1 << 15)"));

    await Assert.That(result.Constant).IsNotNull();
    await Assert.That(result.Constant!.Type.ManagedName).IsEqualTo("int");
    await Assert.That(result.Constant.Value).IsEqualTo("32768");
}

[Test]
public async Task Classify_Should_Emit_Parenthesized_Hex_Literal_As_Uint()
{
    var result = MacroValueClassifier.Classify(Candidate("SDL_WINDOWPOS_UNDEFINED_MASK", "(0x1FFF0000u)"));

    await Assert.That(result.Constant).IsNotNull();
    await Assert.That(result.Constant!.Type.ManagedName).IsEqualTo("uint");
    await Assert.That(result.Constant.Value).IsEqualTo("0x1FFF0000u");
}
```

- [ ] **Step 4: Add RED translator test for haptic expression constants**

Append this test to `BindingConstantTranslatorTests`:

```csharp
[Test]
public async Task Translate_Should_Emit_Haptic_Shift_Macros_And_Include_Ramp()
{
    var compilation = new CppCompilation();
    compilation.Macros.Add(Macro("SDL_HAPTIC_CONSTANT", "(1u << 0)", "SDL_haptic.h"));
    compilation.Macros.Add(Macro("SDL_HAPTIC_RAMP", "(1u << 6)", "SDL_haptic.h"));
    compilation.Macros.Add(Macro("SDL_HAPTIC_PAUSE", "(1u << 15)", "SDL_haptic.h"));
    var config = BindingGenerationFixture.Sdl2CoreConfig();

    var result = BindingConstantTranslator.Translate([ParseResult("Neutral", compilation)], config);

    await Assert.That(result.Constants.Single(c => c.Name == "SDL_HAPTIC_CONSTANT").Value).IsEqualTo("1u");
    await Assert.That(result.Constants.Single(c => c.Name == "SDL_HAPTIC_RAMP").Value).IsEqualTo("64u");
    await Assert.That(result.Constants.Single(c => c.Name == "SDL_HAPTIC_PAUSE").Value).IsEqualTo("32768u");
    await Assert.That(result.Report.Entries.Single(e => e.Name == "SDL_HAPTIC_RAMP").Reason)
        .IsEqualTo("safe integer macro expression");
}
```

- [ ] **Step 5: Run the RED tests**

Run:

```pwsh
dotnet test --project build\_build.Tests\Build.Tests.csproj -c Release --framework net10.0 --treenode-filter "/*/*/*[name=~MacroValueClassifierTests or name=~BindingConstantTranslatorTests or name=~SemanticHeaderFixtureTests]"
```

Expected: the new classifier and translator tests fail because expression values are still reported as `unsupported macro expression`; the fixture parse test passes on Linux and is skipped on non-Linux by the existing `[LinuxOnly]` guard.

- [ ] **Step 6: Approval checkpoint**

Proposed commit message after Task 1 and Task 2 are green:

```text
test: cover SDL macro expression constants
```

Do not run `git commit` until Deniz explicitly approves.

---

## Task 2: Safe Integer Expression Evaluator

**Files:**
- Create: `build\_build\Targets\GenerateBindings\Translation\MacroIntegerExpressionEvaluator.cs`
- Create: `build\_build\Targets\GenerateBindings\Translation\MacroExpressionEvaluationContext.cs`
- Create: `build\_build.Tests\Unit\Targets\GenerateBindings\Translation\MacroIntegerExpressionEvaluatorTests.cs`
- Modify: `build\_build\Targets\GenerateBindings\Translation\MacroValueClassifier.cs`
- Modify: `build\_build.Tests\Unit\Targets\GenerateBindings\Translation\MacroValueClassifierTests.cs`
- Modify: `build\_build.Tests\Unit\Targets\GenerateBindings\Translation\BindingConstantTranslatorTests.cs`

- [ ] **Step 1: Write focused evaluator tests**

Create `MacroIntegerExpressionEvaluatorTests.cs`:

```csharp
using Build.Targets.GenerateBindings.Translation;

namespace Build.Tests.Unit.Targets.GenerateBindings.Translation;

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
    public async Task TryEvaluate_Should_Reject_Function_Call_Shaped_Expression()
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
    public async Task TryEvaluate_Should_Resolve_Known_Identifier()
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
    public async Task TryEvaluate_Should_Evaluate_Whitelisted_Helper_Call()
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
}
```

- [ ] **Step 2: Add expression context records**

Create `MacroExpressionEvaluationContext.cs`:

```csharp
namespace Build.Targets.GenerateBindings.Translation;

internal sealed record MacroExpressionEvaluationContext(
    IReadOnlyDictionary<string, MacroIntegerExpressionValue> Constants,
    IReadOnlyDictionary<string, MacroFunctionLikeMacro> Functions);

internal sealed record MacroFunctionLikeMacro(
    string Name,
    IReadOnlyList<string> Parameters,
    string Body);
```

- [ ] **Step 3: Implement the evaluator**

Create `MacroIntegerExpressionEvaluator.cs` with an internal parser that accepts only literals, known identifiers, whitelisted helper calls, parentheses, `+`, `-`, `<<`, and `|`. Keep it private to the macro translator namespace so it does not become a public build-host abstraction.

The public surface should be:

```csharp
using System.Globalization;

namespace Build.Targets.GenerateBindings.Translation;

internal sealed record MacroIntegerExpressionValue(
    ulong Value,
    bool IsUnsigned,
    string? OriginalLiteralText);

internal static class MacroIntegerExpressionEvaluator
{
    public static MacroIntegerExpressionValue? TryEvaluate(
        string expression,
        MacroExpressionEvaluationContext? context = null,
        IReadOnlyDictionary<string, MacroIntegerExpressionValue>? locals = null)
    {
        ArgumentNullException.ThrowIfNull(expression);

        var parser = new Parser(
            expression,
            context ?? new MacroExpressionEvaluationContext(
                new Dictionary<string, MacroIntegerExpressionValue>(StringComparer.Ordinal),
                new Dictionary<string, MacroFunctionLikeMacro>(StringComparer.Ordinal)),
            locals ?? new Dictionary<string, MacroIntegerExpressionValue>(StringComparer.Ordinal));
        var value = parser.ParseExpression();
        if (value is null || !parser.IsAtEnd)
            return null;

        return value;
    }
}
```

The nested `Parser` should implement these methods by responsibility. Additive/subtractive parsing is required because `SDL_BUTTON(X)` uses `((X) - 1)`.

```csharp
private sealed class Parser(
    string text,
    MacroExpressionEvaluationContext context,
    IReadOnlyDictionary<string, MacroIntegerExpressionValue> locals)
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

            left = new MacroIntegerExpressionValue(
                left.Value | right.Value,
                left.IsUnsigned || right.IsUnsigned,
                OriginalLiteralText: null);
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
            if (right is null || right.Value > 63)
                return null;

            left = new MacroIntegerExpressionValue(
                left.Value << (int)right.Value,
                left.IsUnsigned || right.IsUnsigned,
                OriginalLiteralText: null);
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
                if (right is null)
                    return null;

                left = new MacroIntegerExpressionValue(left.Value + right.Value, left.IsUnsigned || right.IsUnsigned, null);
                continue;
            }

            if (TryConsume('-'))
            {
                var right = ParsePrimary();
                if (right is null || right.Value > left.Value)
                    return null;

                left = new MacroIntegerExpressionValue(left.Value - right.Value, left.IsUnsigned || right.IsUnsigned, null);
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
}
```

Add helper methods in the same nested parser:

```csharp
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

        var unsigned = TryConsumeUnsignedSuffix();
        var literal = text[start.._position];
        var digits = literal.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            ? literal[2..].TrimEnd('u', 'U')
            : literal.TrimEnd('u', 'U');

        return ulong.TryParse(digits, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var value)
            ? new MacroIntegerExpressionValue(value, unsigned, literal)
            : null;
    }

    while (_position < text.Length && char.IsAsciiDigit(text[_position]))
        _position++;

    if (_position == start)
        return null;

    var isUnsigned = TryConsumeUnsignedSuffix();
    var decimalLiteral = text[start.._position];
    var decimalDigits = decimalLiteral.TrimEnd('u', 'U');
    return ulong.TryParse(decimalDigits, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed)
        ? new MacroIntegerExpressionValue(parsed, isUnsigned, decimalLiteral)
        : null;
}

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
```

Add identifier/function-call parsing in the same nested parser:

```csharp
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
        return locals.TryGetValue(name, out var local)
            ? local
            : context.Constants.TryGetValue(name, out var known)
                ? known
                : null;

    if (!context.Functions.TryGetValue(name, out var function))
        return null;

    var arguments = new List<MacroIntegerExpressionValue>();
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

    return MacroIntegerExpressionEvaluator.TryEvaluate(function.Body, context, callLocals);
}
```

- [ ] **Step 4: Wire evaluator into the classifier**

In `MacroValueClassifier.Classify`, after numeric literal classification and before the unsupported fallback, add:

```csharp
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
        Report(candidate, "emitted", "safe integer macro expression", managed, formatted));
}
```

Change the classifier signature to accept optional context:

```csharp
public static MacroValueClassification Classify(
    MacroConstantCandidate candidate,
    MacroExpressionEvaluationContext? context = null)
```

Add this private helper to `MacroValueClassifier`:

```csharp
private static string FormatEvaluatedValue(MacroIntegerExpressionValue value, string managedType)
{
    if (value.OriginalLiteralText is not null
        && (managedType == "uint" || managedType == "ulong")
        && value.OriginalLiteralText.Contains("0x", StringComparison.OrdinalIgnoreCase))
    {
        return managedType == "ulong"
            ? value.OriginalLiteralText.TrimEnd('u', 'U') + "UL"
            : value.OriginalLiteralText.EndsWith('u') || value.OriginalLiteralText.EndsWith('U')
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
```

- [ ] **Step 5: Run evaluator and classifier tests**

Run:

```pwsh
dotnet test --project build\_build.Tests\Build.Tests.csproj -c Release --framework net10.0 --treenode-filter "/*/*/*[name=~MacroIntegerExpressionEvaluatorTests or name=~MacroValueClassifierTests or name=~BindingConstantTranslatorTests]"
```

Expected: PASS for evaluator, classifier, and translator expression tests.

- [ ] **Step 6: Run real generation probe for haptic constants**

Run:

```pwsh
dotnet run --file tools.cs -- generate-bindings
```

Then probe:

```pwsh
Select-String -Path artifacts\generated-bindings-preview\sdl2-core\Constants.g.cs -Pattern "SDL_HAPTIC_CONSTANT|SDL_HAPTIC_RAMP|SDL_HAPTIC_PAUSE"
```

Expected: `Constants.g.cs` contains all three names. `SDL_HAPTIC_RAMP` must be present.

- [ ] **Step 7: Approval checkpoint**

Proposed commit message:

```text
feat: emit safe SDL macro expression constants
```

Do not run `git commit` until Deniz explicitly approves.

---

## Task 3: Contextual Expression Resolution in the Translator

**Files:**
- Modify: `build\_build\Targets\GenerateBindings\Translation\MacroApiPolicy.cs`
- Modify: `build\_build\Targets\GenerateBindings\Translation\BindingConstantTranslator.cs`
- Modify: `build\_build.Tests\Unit\Targets\GenerateBindings\Translation\MacroApiPolicyTests.cs`
- Modify: `build\_build.Tests\Unit\Targets\GenerateBindings\Translation\BindingConstantTranslatorTests.cs`

- [ ] **Step 1: Add helper-candidate disposition and tests**

Update `MacroApiDisposition`:

```csharp
internal enum MacroApiDisposition
{
    Candidate,
    HelperCandidate,
    NonApi,
    Unsupported,
}
```

In `MacroApiPolicy.Classify`, replace the current function-like branch with:

```csharp
if (candidate.IsFunctionLike)
{
    return IsPublicHelperMacro(candidate.Name)
        ? new MacroApiDecision(MacroApiDisposition.HelperCandidate, "public helper macro")
        : new MacroApiDecision(MacroApiDisposition.Unsupported, "function-like macro");
}
```

Add this helper:

```csharp
private static bool IsPublicHelperMacro(string name) =>
    name is "SDL_BUTTON"
        or "SDL_VERSION"
        or "SDL_VERSIONNUM"
        or "SDL_VERSION_ATLEAST"
        or "SDL_WINDOWPOS_UNDEFINED_DISPLAY"
        or "SDL_WINDOWPOS_CENTERED_DISPLAY"
        or "SDL_WINDOWPOS_ISUNDEFINED"
        or "SDL_WINDOWPOS_ISCENTERED"
        or "SDL_DEFINE_PIXELFOURCC"
        or "SDL_DEFINE_PIXELFORMAT"
        or "SDL_PIXELTYPE"
        or "SDL_PIXELORDER"
        or "SDL_PIXELLAYOUT"
        or "SDL_BITSPERPIXEL"
        or "SDL_BYTESPERPIXEL"
        or "SDL_ISPIXELFORMAT_INDEXED"
        or "SDL_ISPIXELFORMAT_PACKED"
        or "SDL_ISPIXELFORMAT_ARRAY"
        or "SDL_ISPIXELFORMAT_ALPHA"
        or "SDL_ISPIXELFORMAT_FOURCC";
```

Update the existing `MacroApiPolicyTests.Classify_Should_Report_Function_Like_Macro_As_Unsupported` test to use an unclassified helper-shaped name, not an internal/non-API name:

```csharp
var candidate = Candidate("SDL_UNCLASSIFIED_FUNCTION_MACRO", "(X)", "SDL_misc.h", parameters: ["X"]);
```

Append this public-helper test:

```csharp
[Test]
public async Task Classify_Should_Report_Public_FunctionLike_Macro_As_Helper_Candidate()
{
    var candidate = Candidate("SDL_VERSION_ATLEAST", "((SDL_COMPILEDVERSION >= SDL_VERSIONNUM(X, Y, Z)))", "SDL_version.h", parameters: ["X", "Y", "Z"]);

    var decision = MacroApiPolicy.Classify(candidate);

    await Assert.That(decision.Disposition).IsEqualTo(MacroApiDisposition.HelperCandidate);
    await Assert.That(decision.Reason).IsEqualTo("public helper macro");
}
```

- [ ] **Step 2: Add RED translator test for object-like helper invocations**

Append to `BindingConstantTranslatorTests`:

```csharp
[Test]
public async Task Translate_Should_Resolve_Object_Like_Macros_That_Invoke_Known_Helper_Macros()
{
    var compilation = new CppCompilation();
    compilation.Macros.Add(Macro("SDL_BUTTON_LEFT", "1", "SDL_mouse.h"));
    var button = Macro("SDL_BUTTON", "(1u << ((X) - 1))", "SDL_mouse.h");
    button.Parameters = ["X"];
    compilation.Macros.Add(button);
    compilation.Macros.Add(Macro("SDL_BUTTON_LMASK", "SDL_BUTTON(SDL_BUTTON_LEFT)", "SDL_mouse.h"));
    compilation.Macros.Add(Macro("SDL_WINDOWPOS_UNDEFINED_MASK", "0x1FFF0000u", "SDL_video.h"));
    var windowpos = Macro("SDL_WINDOWPOS_UNDEFINED_DISPLAY", "(SDL_WINDOWPOS_UNDEFINED_MASK | (X))", "SDL_video.h");
    windowpos.Parameters = ["X"];
    compilation.Macros.Add(windowpos);
    compilation.Macros.Add(Macro("SDL_WINDOWPOS_UNDEFINED", "SDL_WINDOWPOS_UNDEFINED_DISPLAY(0)", "SDL_video.h"));
    var config = BindingGenerationFixture.Sdl2CoreConfig();

    var result = BindingConstantTranslator.Translate([ParseResult("Neutral", compilation)], config);

    await Assert.That(result.Constants.Single(c => c.Name == "SDL_BUTTON_LMASK").Value).IsEqualTo("1u");
    await Assert.That(result.Constants.Single(c => c.Name == "SDL_WINDOWPOS_UNDEFINED").Value).IsEqualTo("536805376u");
    await Assert.That(result.Report.Entries.Single(e => e.Name == "SDL_BUTTON").Disposition).IsEqualTo("helper-candidate");
    await Assert.That(result.Report.Entries.Single(e => e.Name == "SDL_WINDOWPOS_UNDEFINED_DISPLAY").Disposition).IsEqualTo("helper-candidate");
}

[Test]
public async Task Translate_Should_Coalesce_Compatible_Helper_Macros_Across_Parse_Views()
{
    var neutral = new CppCompilation();
    var neutralButton = Macro("SDL_BUTTON", "(1u << ((X) - 1))", "SDL_mouse.h");
    neutralButton.Parameters = ["X"];
    neutral.Macros.Add(neutralButton);
    neutral.Macros.Add(Macro("SDL_BUTTON_LEFT", "1", "SDL_mouse.h"));
    neutral.Macros.Add(Macro("SDL_BUTTON_LMASK", "SDL_BUTTON(SDL_BUTTON_LEFT)", "SDL_mouse.h"));

    var linux = new CppCompilation();
    var linuxButton = Macro("SDL_BUTTON", "(1u << ((X) - 1))", "SDL_mouse.h");
    linuxButton.Parameters = ["X"];
    linux.Macros.Add(linuxButton);

    var config = BindingGenerationFixture.Sdl2CoreConfig();

    var result = BindingConstantTranslator.Translate(
        [ParseResult("Neutral", neutral), ParseResult("Linux", linux)],
        config);

    await Assert.That(result.Constants.Single(c => c.Name == "SDL_BUTTON_LMASK").Value).IsEqualTo("1u");
    await Assert.That(result.Report.Entries.Count(e => e.Name == "SDL_BUTTON" && e.Disposition == "helper-candidate")).IsEqualTo(2);
}
```

- [ ] **Step 3: Build expression context from raw macro candidates**

In `BindingConstantTranslator.Translate`, before classifying candidates, create:

```csharp
var helperFunctionResult = CoalesceHelperFunctions(rawCandidates
    .Where(candidate => candidate.IsFunctionLike)
    .Where(candidate => MacroApiPolicy.Classify(candidate).Disposition == MacroApiDisposition.HelperCandidate));
var helperFunctions = helperFunctionResult.Functions;
```

Add this local helper to `BindingConstantTranslator.Translate`:

```csharp
static MacroFunctionCoalesceResult CoalesceHelperFunctions(IEnumerable<MacroConstantCandidate> helpers)
{
    var result = new Dictionary<string, MacroFunctionLikeMacro>(StringComparer.Ordinal);
    var duplicateCoalescedCount = 0;
    foreach (var group in helpers.GroupBy(helper => helper.Name, StringComparer.Ordinal))
    {
        var first = group.First();
        foreach (var duplicate in group.Skip(1))
        {
            if (!first.Parameters.SequenceEqual(duplicate.Parameters)
                || !string.Equals(first.Value, duplicate.Value, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Incompatible helper macro definitions for '{first.Name}'. Existing value '{first.Value}'; new value '{duplicate.Value}'.");
            }

            duplicateCoalescedCount++;
        }

        result.Add(first.Name, new MacroFunctionLikeMacro(first.Name, first.Parameters, first.Value));
    }

    return new MacroFunctionCoalesceResult(result, duplicateCoalescedCount);
}
```

Add the local result record near the other translation result records:

```csharp
internal sealed record MacroFunctionCoalesceResult(
    IReadOnlyDictionary<string, MacroFunctionLikeMacro> Functions,
    int DuplicateCoalescedCount);
```

In the macro policy switch, add the new helper-candidate report branch using the current report-entry constructor:

```csharp
case MacroApiDisposition.HelperCandidate:
    policyEntries.Add(new MacroConstantReportEntry(
        candidate.Name, candidate.SourceHeader, candidate.ParseViewName,
        "helper-candidate", decision.Reason, null, null));
    break;
```

- [ ] **Step 4: Resolve candidates in phases**

Replace the single-pass `valueClassifications.Add(MacroValueClassifier.Classify(candidate));` shape with a candidate collection plus two-phase resolution loop. First, collect object-like candidate macros in the existing policy switch:

```csharp
var candidateMacros = new List<MacroConstantCandidate>();
```

In the `MacroApiDisposition.Candidate` branch:

```csharp
case MacroApiDisposition.Candidate:
    candidateMacros.Add(candidate);
    break;
```

After the policy loop completes, resolve collected candidates:

```csharp
var pendingCandidates = new List<MacroConstantCandidate>();
var knownConstants = new Dictionary<string, MacroIntegerExpressionValue>(StringComparer.Ordinal);

foreach (var candidate in candidateMacros)
{
    var context = new MacroExpressionEvaluationContext(knownConstants, helperFunctions);
    var classification = MacroValueClassifier.Classify(candidate, context);
    if (classification.Constant is not null)
    {
        valueClassifications.Add(classification);
        if (TryConvertConstantToKnownInteger(classification.Constant, out var known))
            knownConstants[classification.Constant.Name] = known;
        continue;
    }

    pendingCandidates.Add(candidate);
}

var madeProgress = true;
while (madeProgress && pendingCandidates.Count > 0)
{
    madeProgress = false;
    var nextPending = new List<MacroConstantCandidate>();
    foreach (var candidate in pendingCandidates)
    {
        var context = new MacroExpressionEvaluationContext(knownConstants, helperFunctions);
        var classification = MacroValueClassifier.Classify(candidate, context);
        if (classification.Constant is null)
        {
            nextPending.Add(candidate);
            continue;
        }

        madeProgress = true;
        valueClassifications.Add(classification);
        if (TryConvertConstantToKnownInteger(classification.Constant, out var known))
            knownConstants[classification.Constant.Name] = known;
    }

    pendingCandidates = nextPending;
}

foreach (var candidate in pendingCandidates)
{
    valueClassifications.Add(MacroValueClassifier.Classify(candidate, new MacroExpressionEvaluationContext(knownConstants, helperFunctions)));
}
```

Add a local helper:

```csharp
static bool TryConvertConstantToKnownInteger(BindingConstant constant, out MacroIntegerExpressionValue value)
{
    value = new MacroIntegerExpressionValue(0, false, null);
    var text = constant.Value.TrimEnd('u', 'U', 'l', 'L');
    var isUnsigned = constant.Type.ManagedName is "uint" or "ulong" || constant.Value.EndsWith('u') || constant.Value.EndsWith('U');
    if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
    {
        if (!ulong.TryParse(text[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var hex))
            return false;
        value = new MacroIntegerExpressionValue(hex, isUnsigned, constant.Value);
        return true;
    }

    if (!ulong.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed))
        return false;
    value = new MacroIntegerExpressionValue(parsed, isUnsigned, constant.Value);
    return true;
}
```

Add `using System.Globalization;` to `BindingConstantTranslator.cs`.

- [ ] **Step 5: Update existing mixed-report expectations**

In `BindingConstantTranslatorTests.Translate_Should_Set_Report_Counts_Correctly_For_Mixed_Input`, update the `SDL_BUTTON` comments and assertions:

```csharp
// SDL_BUTTON is a public function-like helper candidate, so 2 object-like macros reach value classification.
await Assert.That(result.Report.CandidateCount).IsEqualTo(2);
await Assert.That(result.Report.EmittedCount).IsEqualTo(2);
await Assert.That(result.Report.UnsupportedCount).IsEqualTo(0);
```

In `BindingConstantTranslatorTests.Translate_Should_Report_FunctionLike_Macro_As_Unsupported_And_Not_Emit_It`, rename the test to:

```csharp
public async Task Translate_Should_Report_Public_FunctionLike_Macro_As_Helper_Candidate_And_Not_Emit_It()
```

Update assertions:

```csharp
await Assert.That(entry!.Disposition).IsEqualTo("helper-candidate");
await Assert.That(result.Report.UnsupportedCount).IsEqualTo(0);
```

- [ ] **Step 6: Run contextual translator tests**

Run:

```pwsh
dotnet test --project build\_build.Tests\Build.Tests.csproj -c Release --framework net10.0 --treenode-filter "/*/*/*[name=~BindingConstantTranslatorTests or name=~MacroIntegerExpressionEvaluatorTests]"
```

Expected: PASS, including `SDL_BUTTON_LMASK` and `SDL_WINDOWPOS_UNDEFINED`.

- [ ] **Step 7: Approval checkpoint**

Proposed commit message:

```text
feat: resolve contextual SDL macro expressions
```

Do not run `git commit` until Deniz explicitly approves.

---

## Task 4: Macro Report Taxonomy Evidence

**Files:**
- Modify: `build\_build\Targets\GenerateBindings\Translation\BindingConstantTranslator.cs`
- Modify: `build\_build\Targets\GenerateBindings\Translation\MacroValueClassifier.cs`
- Modify: `build\_build\Targets\GenerateBindings\Model\MacroConstantReport.cs`
- Modify: `build\_build\Targets\GenerateBindings\Emitting\BindingParseViewReport.cs`
- Modify: `build\_build\Targets\GenerateBindings\Emitting\CsCommandEmitter.cs`
- Modify: `build\_build.Tests\Unit\Targets\GenerateBindings\Translation\BindingConstantTranslatorTests.cs`
- Modify: `build\_build.Tests\Unit\Targets\GenerateBindings\Emitting\BindingParseViewReportTests.cs`

- [ ] **Step 1: Add RED report taxonomy assertions**

Append to the contextual translator test from Task 3:

```csharp
var buttonMaskEntry = result.Report.Entries.Single(e => e.Name == "SDL_BUTTON_LMASK");
await Assert.That(buttonMaskEntry.MacroForm).IsEqualTo("object-like");
await Assert.That(buttonMaskEntry.Taxonomy).IsEqualTo("public-expression-constant");
await Assert.That(buttonMaskEntry.OriginalExpression).IsEqualTo("SDL_BUTTON(SDL_BUTTON_LEFT)");
await Assert.That(buttonMaskEntry.ComputedValue).IsEqualTo("1");

var helperEntry = result.Report.Entries.Single(e => e.Name == "SDL_BUTTON");
await Assert.That(helperEntry.MacroForm).IsEqualTo("function-like");
await Assert.That(helperEntry.Taxonomy).IsEqualTo("public-helper-candidate");
await Assert.That(result.Report.HelperDuplicateCoalescedCount).IsEqualTo(0);
```

In `Translate_Should_Coalesce_Compatible_Helper_Macros_Across_Parse_Views`, add:

```csharp
await Assert.That(result.Report.HelperDuplicateCoalescedCount).IsEqualTo(1);
```

- [ ] **Step 2: Extend macro report records**

Update `MacroConstantReport`:

```csharp
public sealed record MacroConstantReportEntry(
    string Name,
    string SourceHeader,
    string ParseViewName,
    string Disposition,
    string Reason,
    string? EmittedType,
    string? EmittedValue,
    string MacroForm,
    string Taxonomy,
    string? OriginalExpression,
    string? ComputedValue);
```

Update all `new MacroConstantReportEntry(...)` call sites to pass the new fields. Use these values:

```csharp
MacroForm: candidate.IsFunctionLike ? "function-like" : "object-like"
Taxonomy: "public-literal-constant" | "public-expression-constant" | "public-helper-candidate" | "non-api" | "unsupported" | "manual-policy"
OriginalExpression: candidate.Value
ComputedValue: expressionValue.Value.ToString(CultureInfo.InvariantCulture)
```

- [ ] **Step 3: Extend JSON report DTOs**

Update `BindingMacroConstantReportEntry`:

```csharp
internal sealed record BindingMacroConstantReportEntry(
    string Name,
    string SourceHeader,
    string ParseViewName,
    string Disposition,
    string Reason,
    string? EmittedType,
    string? EmittedValue,
    string MacroForm,
    string Taxonomy,
    string? OriginalExpression,
    string? ComputedValue);
```

- [ ] **Step 4: Add helper-candidate and helper-duplicate report counts**

Update `MacroConstantReport` and `BindingMacroConstantsReport` constructors to include:

```csharp
int HelperCandidateCount,
int HelperDuplicateCoalescedCount,
```

Update `MacroConstantReport.Empty` to pass `0` for both new counts.

Update `CsCommandEmitter.EmitReportJson` to pass:

```csharp
model.MacroReport.HelperCandidateCount,
model.MacroReport.HelperDuplicateCoalescedCount,
```

- [ ] **Step 5: Update helper candidate branch with taxonomy in the translator**

In `BindingConstantTranslator.Translate`, update the helper-candidate switch branch:

```csharp
case MacroApiDisposition.HelperCandidate:
    policyEntries.Add(new MacroConstantReportEntry(
        candidate.Name, candidate.SourceHeader, candidate.ParseViewName,
        "helper-candidate", decision.Reason, null, null,
        "function-like", "public-helper-candidate", candidate.Value, null));
    break;
```

Set the report count:

```csharp
HelperCandidateCount: allEntries.Count(e => e.Disposition == "helper-candidate"),
HelperDuplicateCoalescedCount: helperFunctionResult.DuplicateCoalescedCount,
```

- [ ] **Step 6: Update report round-trip tests**

In `BindingParseViewReportTests`, add `HelperCandidateCount: 1` and `HelperDuplicateCoalescedCount: 0` to the DTO construction and assert:

```csharp
await Assert.That(round.MacroConstants.HelperCandidateCount).IsEqualTo(1);
await Assert.That(round.MacroConstants.HelperDuplicateCoalescedCount).IsEqualTo(0);
```

Also update the fixture entry construction:

```csharp
new BindingMacroConstantReportEntry(
    Name: "SDL_INIT_TIMER",
    SourceHeader: "SDL.h",
    ParseViewName: "Neutral",
    Disposition: "included",
    Reason: "manual-include",
    EmittedType: "uint",
    EmittedValue: "0x00000001u",
    MacroForm: "manual",
    Taxonomy: "manual-policy",
    OriginalExpression: null,
    ComputedValue: null)
```

- [ ] **Step 7: Run focused tests**

Run:

```pwsh
dotnet test --project build\_build.Tests\Build.Tests.csproj -c Release --framework net10.0 --treenode-filter "/*/*/*[name=~MacroApiPolicyTests or name=~BindingConstantTranslatorTests or name=~BindingParseViewReportTests]"
```

Expected: PASS.

- [ ] **Step 8: Approval checkpoint**

Proposed commit message:

```text
feat: report SDL macro taxonomy evidence
```

Do not run `git commit` until Deniz explicitly approves.

---

## Task 5: Non-API Skip Policy Hardening

**Files:**
- Modify: `build\_build\Targets\GenerateBindings\Translation\MacroApiPolicy.cs`
- Modify: `build\_build.Tests\Unit\Targets\GenerateBindings\Translation\MacroApiPolicyTests.cs`
- Modify: `build\_build.Tests\Unit\Targets\GenerateBindings\Translation\BindingConstantTranslatorTests.cs`

- [ ] **Step 1: Add RED skip-policy tests**

Append to `MacroApiPolicyTests`:

```csharp
[Test]
public async Task Classify_Should_Report_Cacheline_Size_As_Non_Api()
{
    var decision = MacroApiPolicy.Classify(Candidate("SDL_CACHELINE_SIZE", "128", "SDL_cpuinfo.h"));

    await Assert.That(decision.Disposition).IsEqualTo(MacroApiDisposition.NonApi);
    await Assert.That(decision.Reason).IsEqualTo("C-only cache-line padding macro");
}

[Test]
public async Task Classify_Should_Report_Assert_Level_As_Non_Api()
{
    var decision = MacroApiPolicy.Classify(Candidate("SDL_ASSERT_LEVEL", "1", "SDL_assert.h"));

    await Assert.That(decision.Disposition).IsEqualTo(MacroApiDisposition.NonApi);
    await Assert.That(decision.Reason).IsEqualTo("SDL C assertion build-time macro");
}

[Test]
public async Task Classify_Should_Report_Printf_Format_Macro_As_Non_Api()
{
    var decision = MacroApiPolicy.Classify(Candidate("SDL_PRIu64", "\"I64u\"", "SDL_stdinc.h"));

    await Assert.That(decision.Disposition).IsEqualTo(MacroApiDisposition.NonApi);
    await Assert.That(decision.Reason).IsEqualTo("C printf format macro");
}

[Test]
public async Task Classify_Should_Report_Revision_Number_As_Non_Api()
{
    var decision = MacroApiPolicy.Classify(Candidate("SDL_REVISION_NUMBER", "0", "SDL_revision.h"));

    await Assert.That(decision.Disposition).IsEqualTo(MacroApiDisposition.NonApi);
    await Assert.That(decision.Reason).IsEqualTo("obsolete SDL revision macro");
}

[Test]
public async Task Classify_Should_Report_Compile_Time_Assert_As_Non_Api()
{
    var decision = MacroApiPolicy.Classify(Candidate("SDL_COMPILE_TIME_ASSERT", "typedef int SDL_compile_time_assert_##name[(x) * 2 - 1]", "SDL_stdinc.h", parameters: ["name", "x"]));

    await Assert.That(decision.Disposition).IsEqualTo(MacroApiDisposition.NonApi);
    await Assert.That(decision.Reason).IsEqualTo("C compile-time assertion macro");
}

[Test]
public async Task Classify_Should_Report_C_Cast_Helper_As_Non_Api()
{
    var decision = MacroApiPolicy.Classify(Candidate("SDL_reinterpret_cast", "reinterpret_cast<type>(expression)", "SDL_stdinc.h", parameters: ["type", "expression"]));

    await Assert.That(decision.Disposition).IsEqualTo(MacroApiDisposition.NonApi);
    await Assert.That(decision.Reason).IsEqualTo("C/C++ cast helper macro");
}

[Test]
public async Task Classify_Should_Report_Printf_Annotation_Macro_As_Non_Api()
{
    var decision = MacroApiPolicy.Classify(Candidate("SDL_PRINTF_VARARG_FUNC", "__attribute__((format(printf, fmtargnumber, firstvararg)))", "SDL_stdinc.h", parameters: ["fmtargnumber", "firstvararg"]));

    await Assert.That(decision.Disposition).IsEqualTo(MacroApiDisposition.NonApi);
    await Assert.That(decision.Reason).IsEqualTo("C printf annotation macro");
}
```

- [ ] **Step 2: Add policy helpers**

In `MacroApiPolicy.Classify`, after the `SDL_config*.h` branch and before the function-like branch, add:

```csharp
if (candidate.Name is "SDL_CACHELINE_SIZE")
    return new MacroApiDecision(MacroApiDisposition.NonApi, "C-only cache-line padding macro");

if (candidate.Name is "SDL_ASSERT_LEVEL")
    return new MacroApiDecision(MacroApiDisposition.NonApi, "SDL C assertion build-time macro");

if (candidate.Name is "SDL_PRINTF_VARARG_FUNC")
    return new MacroApiDecision(MacroApiDisposition.NonApi, "C printf annotation macro");

if (candidate.Name.StartsWith("SDL_PRI", StringComparison.Ordinal))
    return new MacroApiDecision(MacroApiDisposition.NonApi, "C printf format macro");

if (candidate.Name is "SDL_REVISION_NUMBER" or "SDL_REVISION")
    return new MacroApiDecision(MacroApiDisposition.NonApi, "obsolete SDL revision macro");

if (candidate.Name is "SDL_COMPILE_TIME_ASSERT")
    return new MacroApiDecision(MacroApiDisposition.NonApi, "C compile-time assertion macro");

if (candidate.Name is "SDL_reinterpret_cast" or "SDL_static_cast" or "SDL_const_cast")
    return new MacroApiDecision(MacroApiDisposition.NonApi, "C/C++ cast helper macro");

```

- [ ] **Step 3: Add translator evidence test**

Append to `BindingConstantTranslatorTests`:

```csharp
[Test]
public async Task Translate_Should_Skip_C_Only_And_Obsolete_Macros_With_Reasons()
{
    var compilation = new CppCompilation();
    compilation.Macros.Add(Macro("SDL_CACHELINE_SIZE", "128", "SDL_cpuinfo.h"));
    compilation.Macros.Add(Macro("SDL_ASSERT_LEVEL", "1", "SDL_assert.h"));
    compilation.Macros.Add(Macro("SDL_PRIu64", "\"I64u\"", "SDL_stdinc.h"));
    compilation.Macros.Add(Macro("SDL_REVISION_NUMBER", "0", "SDL_revision.h"));
    var config = BindingGenerationFixture.Sdl2CoreConfig();

    var result = BindingConstantTranslator.Translate([ParseResult("Neutral", compilation)], config);

    await Assert.That(result.Constants.Any(c => c.Name == "SDL_CACHELINE_SIZE")).IsFalse();
    await Assert.That(result.Report.Entries.Single(e => e.Name == "SDL_CACHELINE_SIZE").Reason)
        .IsEqualTo("C-only cache-line padding macro");
    await Assert.That(result.Report.Entries.Single(e => e.Name == "SDL_ASSERT_LEVEL").Reason)
        .IsEqualTo("SDL C assertion build-time macro");
    await Assert.That(result.Report.Entries.Single(e => e.Name == "SDL_PRIu64").Reason)
        .IsEqualTo("C printf format macro");
    await Assert.That(result.Report.Entries.Single(e => e.Name == "SDL_REVISION_NUMBER").Reason)
        .IsEqualTo("obsolete SDL revision macro");
}
```

- [ ] **Step 4: Run focused tests**

Run:

```pwsh
dotnet test --project build\_build.Tests\Build.Tests.csproj -c Release --framework net10.0 --treenode-filter "/*/*/*[name=~MacroApiPolicyTests or name=~BindingConstantTranslatorTests]"
```

Expected: PASS.

- [ ] **Step 5: Real generation skip probe**

Run:

```pwsh
dotnet run --file tools.cs -- generate-bindings
```

Probe `parse-views.json`:

```pwsh
Select-String -Path artifacts\generated-bindings-preview\sdl2-core\parse-views.json -Pattern "SDL_ASSERT_LEVEL|SDL_PRIu64|SDL_CACHELINE_SIZE|SDL_REVISION_NUMBER|SDL_PRINTF_VARARG_FUNC"
```

Expected: matching entries, if present in parsed input, have `Disposition` of `skipped` and explicit non-API reasons. None of these names appear in `Constants.g.cs`.

- [ ] **Step 6: Approval checkpoint**

Proposed commit message:

```text
fix: skip C-only SDL macro surface
```

Do not run `git commit` until Deniz explicitly approves.

---

## Task 6: Surface Audit and Documentation

**Files:**
- Create: `build\_build.Tests\Unit\Targets\GenerateBindings\Translation\MacroSurfaceAuditTests.cs`
- Modify: `docs\binding-autogen\binding-api-surface-strategy.md`
- Modify: `docs\playbook\binding-generator-maintenance.md`
- Modify: `docs\plan.md`

- [ ] **Step 1: Add audit expectation tests**

Create `MacroSurfaceAuditTests.cs`:

```csharp
using Build.Targets.GenerateBindings.Translation;

namespace Build.Tests.Unit.Targets.GenerateBindings.Translation;

public sealed class MacroSurfaceAuditTests
{
    [Test]
    public async Task PublicExpressionConstants_Should_Include_High_Value_Sdl2_Surface()
    {
        var expressions = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["SDL_HAPTIC_CONSTANT"] = "(1u << 0)",
            ["SDL_HAPTIC_RAMP"] = "(1u << 6)",
            ["SDL_HAPTIC_PAUSE"] = "(1u << 15)",
            ["SDL_AUDIO_MASK_BITSIZE"] = "(0xFF)",
            ["SDL_AUDIO_MASK_DATATYPE"] = "(1 << 8)",
            ["SDL_AUDIO_MASK_ENDIAN"] = "(1 << 12)",
            ["SDL_AUDIO_MASK_SIGNED"] = "(1 << 15)",
        };

        foreach (var expression in expressions)
        {
            var result = MacroValueClassifier.Classify(Candidate(expression.Key, expression.Value, "SDL_audit.h"));

            await Assert.That(result.Constant).IsNotNull();
            await Assert.That(result.Report.Disposition).IsEqualTo("emitted");
        }
    }

    [Test]
    public async Task HelperMacroBacklog_Should_Track_Public_FunctionLike_Surface()
    {
        var helperBacklog = new[]
        {
            "SDL_BUTTON",
            "SDL_VERSION",
            "SDL_VERSIONNUM",
            "SDL_VERSION_ATLEAST",
            "SDL_WINDOWPOS_UNDEFINED_DISPLAY",
            "SDL_WINDOWPOS_CENTERED_DISPLAY",
            "SDL_WINDOWPOS_ISUNDEFINED",
            "SDL_WINDOWPOS_ISCENTERED",
            "SDL_ISPIXELFORMAT_INDEXED",
            "SDL_ISPIXELFORMAT_PACKED",
            "SDL_ISPIXELFORMAT_ARRAY",
            "SDL_ISPIXELFORMAT_ALPHA",
            "SDL_ISPIXELFORMAT_FOURCC",
        };

        foreach (var helper in helperBacklog)
        {
            var decision = MacroApiPolicy.Classify(Candidate(helper, "(fixture)", "SDL_audit.h", parameters: ["X"]));

            await Assert.That(decision.Disposition).IsEqualTo(MacroApiDisposition.HelperCandidate);
        }
    }

    [Test]
    public async Task NonDotNetMacroSkips_Should_Track_Known_Internal_Surface()
    {
        var skipped = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["SDL_CACHELINE_SIZE"] = "SDL_cpuinfo.h",
            ["SDL_ASSERT_LEVEL"] = "SDL_assert.h",
            ["SDL_PRIu64"] = "SDL_stdinc.h",
            ["SDL_PRIs64"] = "SDL_stdinc.h",
            ["SDL_REVISION_NUMBER"] = "SDL_revision.h",
        };

        foreach (var macro in skipped)
        {
            var decision = MacroApiPolicy.Classify(Candidate(macro.Key, "1", macro.Value));

            await Assert.That(decision.Disposition).IsEqualTo(MacroApiDisposition.NonApi);
        }
    }

    private static MacroConstantCandidate Candidate(
        string name,
        string value,
        string header,
        IReadOnlyList<string>? parameters = null) =>
        new(name, value, parameters ?? [], [], $"C:/vcpkg/installed/x64-linux-hybrid/include/SDL2/{header}", header, "Neutral");
}
```

This class records high-value audit categories in executable form by exercising the same classifier/policy objects used by production translation. It is not a replacement for real generation probes; it prevents future work from losing the agreed categories while the helper emitter is still pending.

- [ ] **Step 2: Run full focused macro tests**

Run:

```pwsh
dotnet test --project build\_build.Tests\Build.Tests.csproj -c Release --framework net10.0 --treenode-filter "/*/*/*[name=~Macro]"
```

Expected: PASS.

- [ ] **Step 3: Run real generation and compile-check**

Run:

```pwsh
dotnet run --file tools.cs -- generate-bindings
dotnet build tests\binding-compile-check\SDL2.Core.CompileCheck.csproj -c Release
```

Expected: generation succeeds; compile-check succeeds across configured target frameworks.

- [ ] **Step 4: Run official-header and peer-surface audits**

Run this official-header audit after generation:

```pwsh
$officialHeaders = @(
  "vcpkg_installed\x64-windows-hybrid\include\SDL2\SDL_haptic.h",
  "vcpkg_installed\x64-windows-hybrid\include\SDL2\SDL_mouse.h",
  "vcpkg_installed\x64-windows-hybrid\include\SDL2\SDL_video.h",
  "vcpkg_installed\x64-windows-hybrid\include\SDL2\SDL_version.h",
  "vcpkg_installed\x64-windows-hybrid\include\SDL2\SDL_pixels.h",
  "vcpkg_installed\x64-windows-hybrid\include\SDL2\SDL_stdinc.h",
  "vcpkg_installed\x64-windows-hybrid\include\SDL2\SDL_assert.h",
  "vcpkg_installed\x64-windows-hybrid\include\SDL2\SDL_cpuinfo.h"
)

$officialMacroNames = Select-String -Path $officialHeaders -Pattern "^\s*#\s*define\s+(?<name>SDL_[A-Za-z0-9_]+)\b" |
  ForEach-Object { $_.Matches[0].Groups["name"].Value } |
  Sort-Object -Unique

$report = Get-Content artifacts\generated-bindings-preview\sdl2-core\parse-views.json -Raw | ConvertFrom-Json
$reportedMacroNames = $report.MacroConstants.Entries |
  ForEach-Object { $_.Name } |
  Sort-Object -Unique

"Official SDL macro names from audited headers that have no parse-report entry:"
Compare-Object -ReferenceObject $officialMacroNames -DifferenceObject $reportedMacroNames |
  Where-Object SideIndicator -eq "<=" |
  Select-Object -ExpandProperty InputObject |
  Select-Object -First 80
```

Then run this SDL2-CS peer audit:

```pwsh
$generatedConstants = Select-String -Path artifacts\generated-bindings-preview\sdl2-core\Constants.g.cs -Pattern "public\s+(?:const|static)\s+(?:[^\s]+\s+)?(?<name>SDL_[A-Za-z0-9_]+)" |
  ForEach-Object { $_.Matches[0].Groups["name"].Value } |
  Sort-Object -Unique

$sdl2csConstants = Select-String -Path external\sdl2-cs\src\SDL2.cs -Pattern "public\s+(?:const|static\s+readonly)\s+[^\s]+\s+(?<name>SDL_[A-Za-z0-9_]+)" |
  ForEach-Object { $_.Matches[0].Groups["name"].Value } |
  Sort-Object -Unique

$missingFromGenerated = Compare-Object -ReferenceObject $sdl2csConstants -DifferenceObject $generatedConstants |
  Where-Object SideIndicator -eq "<=" |
  Select-Object -ExpandProperty InputObject

$extraInGenerated = Compare-Object -ReferenceObject $sdl2csConstants -DifferenceObject $generatedConstants |
  Where-Object SideIndicator -eq "=>" |
  Select-Object -ExpandProperty InputObject

"Missing from generated:"
$missingFromGenerated | Select-Object -First 80
"Extra in generated:"
$extraInGenerated | Select-Object -First 80
```

Expected:

- Official header macros from the audited headers either appear in `parse-views.json` or are explained by parse-input scope; no public haptic/button/windowpos/version/pixel family should be invisible.
- `SDL_HAPTIC_RAMP` is not a failure if it appears as extra in generated; it is an SDL2-CS omission confirmed by official SDL2 headers.
- `SDL_ASSERT_LEVEL`, `SDL_PRI*`, `SDL_CACHELINE_SIZE`, and `SDL_REVISION_NUMBER` do not appear in generated constants.
- Any remaining missing haptic/button/windowpos/version/pixel helper family item is classified in notes as `implement now`, `helper-lane backlog`, `typed-shape backlog`, `documented skip`, `peer bug/stale reference`, or `needs official evidence`.
- Peer evidence beyond SDL2-CS, especially Silk.NET findings from the research/spec, is used to calibrate .NET shape in docs; it does not override official header evidence.
- Do not commit audit scratch output. If durable decisions are discovered, write them into the canonical docs in the next step.

- [ ] **Step 5: Update canonical docs**

In `docs\binding-autogen\binding-api-surface-strategy.md`, update the constants/macros section with these decisions:

```markdown
- object-like public expression macros such as `SDL_HAPTIC_*` are generated only when the expression evaluator can prove a deterministic integer value;
- function-like public macros such as `SDL_BUTTON`, `SDL_VERSION_ATLEAST`, and pixel-format predicates are reported as helper candidates and are emitted only by the later helper-method lane;
- C-only/build-time/internal macros such as `SDL_ASSERT_LEVEL`, `SDL_PRI*`, `SDL_CACHELINE_SIZE`, and `SDL_REVISION_NUMBER` are skipped with explicit report reasons;
- every new macro capability requires real embedded `.h` fixture coverage before it is considered complete.
```

In `docs\playbook\binding-generator-maintenance.md`, add a maintenance rule:

```markdown
When adding macro support, start with a real header fixture under `build\_build.Tests\Fixtures\Data\GenerateBindings\`. A constructed `CppMacro` test may cover edge cases, but it cannot be the only test for a new parser capability.
```

In `docs\plan.md`, record the result under the Phase 4 binding-autogen status:

```markdown
- SDL2 macro surface taxonomy now distinguishes expression constants, helper macro candidates, and non-.NET/internal skips; `SDL_HAPTIC_*` expression constants are generated from headers, including `SDL_HAPTIC_RAMP`.
```

- [ ] **Step 6: Run Slopwatch and diff check**

Run:

```pwsh
slopwatch analyze --fail-on warning --exclude "artifacts/**,external/**,vcpkg_installed/**,**/bin/**,**/obj/**"
git --no-pager diff --check
```

Expected: Slopwatch reports `0 issue(s) found`; `git diff --check` exits 0, allowing existing CRLF warnings if they appear without whitespace errors.

- [ ] **Step 7: Approval checkpoint**

Proposed commit message:

```text
docs: record SDL macro surface taxonomy
```

Do not run `git commit` until Deniz explicitly approves.

---

## Final Verification Gate

Run these commands before claiming implementation complete:

```pwsh
dotnet test --project build\_build.Tests\Build.Tests.csproj -c Release --framework net10.0
dotnet run --file tools.cs -- generate-bindings
dotnet build tests\binding-compile-check\SDL2.Core.CompileCheck.csproj -c Release
slopwatch analyze --fail-on warning --exclude "artifacts/**,external/**,vcpkg_installed/**,**/bin/**,**/obj/**"
git --no-pager diff --check
```

Expected:

- Build-host tests pass.
- `generate-bindings` succeeds.
- `Constants.g.cs` contains `SDL_HAPTIC_RAMP`.
- `parse-views.json` distinguishes emitted constants, helper candidates, unsupported macros, and skipped non-API macros.
- Compile-check succeeds for all configured target frameworks.
- Slopwatch reports no warning-or-higher issues.
- Diff check has no whitespace errors.
