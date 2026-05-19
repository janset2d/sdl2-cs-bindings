# SDL2 Macro Surface Taxonomy Design

> Status: Draft for review.
> Scope: SDL2.Core binding-generator macro surface after source-first object-like macro constants.

## Problem Statement

The generator now collects source-visible object-like SDL macros and emits safe literal constants, which fixed the first SDL hint gap. The next gap is broader: official SDL2 headers expose public API through several macro shapes, not only string hints and simple literals.

Examples include haptic effect flags such as `SDL_HAPTIC_CONSTANT`, mouse button masks such as `SDL_BUTTON_LMASK`, window-position sentinels such as `SDL_WINDOWPOS_CENTERED`, version helpers such as `SDL_VERSION_ATLEAST`, and pixel-format helper predicates. SDL2-CS contains many of these, but it is manually maintained and outdated; it even misses official public macros such as `SDL_HAPTIC_RAMP`. The binding generator needs a taxonomy that treats SDL headers as the authority, uses peer bindings as calibration, and keeps non-.NET or build-time macros out of the public API with explicit evidence.

## Goals

- Classify SDL2.Core macro surface into public constants, public helper macros, public-but-deprecated macros, and non-public/build-time macros.
- Add safe expression support for object-like public macro constants without emitting raw C preprocessor text.
- Keep function-like macros out of `Constants.g.cs`; emit them later through a separate helper-method lane when they are public API.
- Make every new macro capability prove itself with a real embedded `.h` fixture, not only constructed `CppMacro` objects.
- Use official SDL2 headers and wiki/API docs as the source of truth; use SDL2-CS, Silk.NET, ppy/SDL3-CS, and other peers only as shape and migration evidence.
- Preserve manual include/exclude/override policy, duplicate detection, and parse-report evidence from the source-first macro pipeline.
- Run a bidirectional generated-vs-SDL2-CS audit as an alarm system, not as an authority.

## Non-Goals

- Do not copy SDL2-CS macro regions into the manifest or generated code.
- Do not expose C-only build configuration, compiler, printf-format, or include-guard macros.
- Do not implement helper macros in the same lane as constants.
- Do not require exact SDL2-CS compatibility when official SDL2 evidence or modern C# API shape disagrees.
- Do not flip `src\SDL2.Core` from SDL2-CS imports to generated sources as part of this slice.
- Do not decide the final friendly high-level API wrapper layer here.

## Source-of-Truth Hierarchy

1. **Official SDL2 headers in `vcpkg_installed\...\include\SDL2`** decide whether a macro exists and how it is defined.
2. **Official SDL wiki/API documentation and header comments** decide whether the macro is public API, deprecated public API, or C-only/internal.
3. **Peer bindings** calibrate .NET shape, migration impact, and likely consumer expectations.
4. **SDL2-CS** is useful compatibility evidence, but never the deciding authority.
5. **Current generated output and `parse-views.json`** provide local evidence for what the generator sees, emits, skips, or marks unsupported.

## Considered Approaches

### Recommended: Taxonomy-first staged implementation

Define a macro taxonomy first, then implement expression constants, helper macros, and skip hardening as separate TDD slices.

This keeps `SDL_HAPTIC_*` from becoming a one-off parser patch and gives us a durable place to classify future SDL2/SDL3 macro families. It also makes false positives visible: `SDL_DISABLE` and `SDL_ENABLE` are public despite looking generic, while `SDL_ASSERT_LEVEL` is non-public despite being a simple numeric macro.

### Alternative: Expression evaluator only

Add support for `(1u << n)` and simple `|` expressions, then regenerate.

This is fast, but too narrow. It fixes haptic flags and button masks while leaving helper macros, public/deprecated distinctions, and internal skip policy as ad hoc follow-up work. It risks growing the public surface accidentally because every newly parsable macro looks safe.

### Alternative: SDL2-CS compatibility mirror

Compare against SDL2-CS and generate whatever SDL2-CS exposes.

This improves migration familiarity but bakes in stale manual decisions. SDL2-CS is missing `SDL_HAPTIC_RAMP`, contains known helper bugs such as the `SDL_ISPIXELFORMAT_FOURCC` predicate, and predates parts of the current SDL2 surface. It should guide audits, not drive generation.

## Macro Taxonomy

### Public object-like literal constants

These are source-visible object-like macros with literal string, numeric, character, or alias values. They continue through the existing constant pipeline.

Examples:

- `SDL_HINT_RENDER_DRIVER`
- `SDL_HINT_RENDER_VSYNC`
- `SDL_DISABLE`
- `SDL_ENABLE`
- `SDL_QUERY`
- `SDL_IGNORE`

Expected output:

- String keys emit canonical UTF-8 span properties.
- Numeric and character values emit `public const` when the managed type is unambiguous.
- Aliases emit only when the target is also generated and the C# expression is compile-safe.

### Public object-like expression constants

These are object-like public macros whose values are deterministic compile-time expressions, but not simple literals.

Examples:

- `SDL_HAPTIC_CONSTANT` through `SDL_HAPTIC_PAUSE`
- `SDL_BUTTON_LMASK` through `SDL_BUTTON_X2MASK`
- `SDL_WINDOWPOS_UNDEFINED`
- `SDL_WINDOWPOS_CENTERED`
- `SDL_AUDIO_MASK_BITSIZE`
- `SDL_AUDIO_MASK_DATATYPE`
- `SDL_AUDIO_MASK_ENDIAN`
- `SDL_AUDIO_MASK_SIGNED`

The expression lane should initially support:

- integer literals with C suffixes: `1u`, `1U`, `0x1FFF0000u`, `0xffffffffU`;
- unary parentheses;
- shifts: `1u << 6`, `1 << 15`;
- bitwise OR over safe operands;
- aliases to previously classified safe constants;
- deterministic casts only when the target managed type is explicitly known.

The lane must reject expressions with function calls, platform conditionals, unknown identifiers, `sizeof`, string concatenation, token pasting, or any side-effect-shaped C preprocessor behavior.

Expected output may be a numeric computed value or a C# compile-time expression. The model must preserve the original source expression and source evidence even when output uses a computed value.

### Public function-like helper macros

These macros are not constants and must not be emitted through `Constants.g.cs`. They are public helper APIs and need a separate static-helper emitter.

Examples:

- `SDL_BUTTON(X)`
- `SDL_VERSIONNUM(X, Y, Z)`
- `SDL_VERSION_ATLEAST(X, Y, Z)`
- `SDL_WINDOWPOS_UNDEFINED_DISPLAY(X)`
- `SDL_WINDOWPOS_CENTERED_DISPLAY(X)`
- `SDL_WINDOWPOS_ISUNDEFINED(X)`
- `SDL_WINDOWPOS_ISCENTERED(X)`
- `SDL_ISPIXELFORMAT_INDEXED(format)`
- `SDL_ISPIXELFORMAT_PACKED(format)`
- `SDL_ISPIXELFORMAT_ARRAY(format)`
- `SDL_ISPIXELFORMAT_ALPHA(format)`
- `SDL_ISPIXELFORMAT_FOURCC(format)`

The helper lane should produce normal public static C# methods or properties on the generated public class, with tests that compare against official macro semantics. Helper generation should not begin until expression constants are stable enough to support their dependencies.

### Public deprecated or borderline macros

These macros are source-visible and may be documented, but official SDL guidance discourages direct use.

Examples:

- `SDL_COMPILEDVERSION`
- `SDL_VERSIONNUM`
- `SDL_REVISION`
- `SDL_REVISION_NUMBER`

Rules:

- Public deprecated helper macros should emit `[Obsolete]` when official SDL marks them deprecated and the API remains useful for migration.
- `SDL_REVISION_NUMBER` should be skipped because official SDL marks the corresponding runtime function obsolete and modern SDL returns zero.
- `SDL_REVISION` should default to skip unless a real consumer need appears, because it is header-time build metadata and can differ from the linked runtime SDL library.

### Non-public, build-time, C-only, and internal macros

These macros are visible to the parser but should not become public .NET API.

Examples:

- `SDL_config*.h` build toggles and `HAVE_*` macros
- `SIZEOF_VOIDP`
- include guards
- `SDL_ASSERT_LEVEL`
- `SDL_CACHELINE_SIZE`
- `SDL_PRI*` printf-format fragments
- `SDL_COMPILE_TIME_ASSERT`
- `SDL_DUMMY_ENUM`
- compiler/static-analysis controls such as `SDL_DISABLE_ANALYZE_MACROS`
- C cast helper macros such as `SDL_reinterpret_cast`

Each skip must have an explicit report reason. The reason should identify the class of non-API behavior, not merely say "unsupported".

## Typed Shape Decisions

The first implementation should keep raw constant emission conservative, but the taxonomy must leave room for typed public shape where that is clearly better API.

- Haptic effect/support bits are public flags. They can initially emit as constants to unblock coverage, but the preferred long-term public shape is a `[Flags]` enum or a named flags type. `SDL_HAPTIC_RAMP` must be included because official SDL2 headers define it.
- Mouse button masks are public flags. `const uint` is acceptable initially; a later `[Flags]` enum can improve ergonomics.
- Window-position sentinels are passed to APIs typed as `int`, but the C macro literals use unsigned suffixes. The implementation must choose deliberately and document the decision; either `int` for call-site ergonomics or `uint` for source literal fidelity can be defended.
- Pixel formats should primarily come from `SDL_PixelFormatEnum`, not from regenerating `SDL_DEFINE_PIXELFORMAT` as static readonly fields. Function-like pixel helpers remain a separate helper-method concern.

## Reporting Requirements

`parse-views.json` should remain the answer key for macro decisions.

For each relevant macro, report:

- name;
- source header and parse view evidence;
- macro form: object-like or function-like;
- taxonomy category;
- emitted shape or skip/defer reason;
- original expression text when available;
- computed value when expression evaluation succeeds;
- manual include/exclude/override consumption when applicable;
- duplicate/coalescing evidence across parse views.

The report must distinguish "not public API" from "public but not implemented yet" from "unsupported expression".

## Embedded Header Fixture Rule

Every macro capability added after this spec must include a real `.h` fixture under `build\_build.Tests\Fixtures\Data\GenerateBindings\`.

Minimum coverage per capability:

1. A header fixture containing representative C preprocessor syntax.
2. A CppAst parse/fixture test proving the parser exposes the macro shape as expected.
3. A policy or classifier unit test proving the generator classifies it correctly.
4. A translator/report test proving it emits, skips, or defers with the expected evidence.
5. A generated-output compile check when the capability changes emitted C#.

Constructed object tests are still useful for edge cases, but they do not replace header fixtures.

## Bidirectional Audit Requirements

Run two audits before calling the macro surface stable:

1. **Missing public surface audit:** symbols present in official SDL2 headers or SDL2-CS/Silk.NET peer surfaces but absent from generated output.
2. **Excess public surface audit:** generated symbols that peers skip or official evidence classifies as build-time, C-only, internal, deprecated-obsolete, or non-.NET-useful.

Audit output should classify each finding as:

- implement now;
- helper-lane backlog;
- typed-shape backlog;
- documented skip;
- peer bug/stale reference;
- needs official evidence.

SDL2-CS comparison failures are not automatically generator bugs. Official SDL2 evidence decides.

## Success Criteria

- `SDL_HAPTIC_*` public expression constants, including `SDL_HAPTIC_RAMP`, are no longer reported only as unsupported expressions.
- Public simple and expression constants have real `.h` fixture coverage.
- Function-like public macros are reported as helper candidates instead of disappearing or being misclassified as constants.
- Non-public macros such as `SDL_ASSERT_LEVEL`, `SDL_PRI*`, `SDL_CACHELINE_SIZE`, and config/build toggles are skipped with explicit reasons.
- A bidirectional audit can explain major SDL2-CS/generated differences without manual guesswork.
- Real `generate-bindings` and binding compile-check still pass after each implementation slice.

