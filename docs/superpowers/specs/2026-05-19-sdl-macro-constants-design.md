# SDL Macro Constants Design

> Status: Draft for review.
> Scope: Stage 1 SDL2.Core binding-generator macro constant auto-generation.

## Problem Statement

The SDL2.Core binding generator currently parses macros, but generated constants still come primarily from `build\manifest.json` `binding_generation.required_constants`. That is why a readiness probe such as `SDL_HINT_RENDER_DRIVER` only appeared after being manually added to the manifest.

That approach is not durable enough for the SDL macro surface. SDL headers contain many public object-like macros: hint names, init flags, button masks, special values, aliases, limits, and version/platform-related constants. Hand-curating those in the manifest would recreate the maintenance shape of SDL2-CS: simple today, stale tomorrow, and very easy to typo.

The generator needs to become source-first for macro constants while preserving explicit manual control for exceptional cases.

## Goals

- Auto-generate public SDL2.Core macro constants from parsed SDL public headers.
- Treat all object-like `SDL_*` macros as candidates, not only a narrow `SDL_HINT_*` allowlist.
- Keep function-like macros out of the constant API and report them explicitly.
- Keep `build\manifest.json` manual constants as include/exclude/override escape hatches, not as the primary source of truth.
- Detect stale manual includes, excludes, and overrides by default.
- Detect incompatible duplicate macro definitions across parse views.
- Preserve enough evidence in `parse-views.json` to explain why each relevant macro was emitted, skipped, overridden, or rejected.
- Preserve the current modern low-level API shape: UTF-8 span properties for string-like constants and `const` values for safe numeric constants.

## Non-Goals

- Do not introduce a second binding generator or ClangSharp side-path for this slice.
- Do not emit function-like macros such as `SDL_FOURCC`, `SDL_VERSIONNUM`, or `SDL_SCANCODE_TO_KEYCODE` as constants.
- Do not manually copy the SDL2-CS `SDL_hints.h` region into the manifest.
- Do not flip `src\SDL2.Core` to generated sources as part of this design.
- Do not design the later friendly API layer beyond preserving the current low-level constant shape.

## Peer Evidence

| Peer | Macro constant shape | Lesson for Janset.SDL2 |
| --- | --- | --- |
| SDL2-CS | Hand-authored `const string SDL_HINT_*` constants. | Useful compatibility reference, but not a durable generation strategy. It is UTF-16-oriented and manually maintained. |
| ppy/SDL3-CS | ClangSharp-generated `ReadOnlySpan<byte>` UTF-8 constants with native metadata and manual excludes. | Confirms the modern output shape and the need for generated source traceability. |
| Alimer.Bindings.SDL | CppAst-based custom constant generator emitting `ReadOnlySpan<byte>`. | Closest architectural peer, but observed silent string-value typos show that collection without validation is not enough. |
| bottlenoselabs/SDL3-cs | Config-heavy extraction with allow/block names and multi-platform extraction. | Reinforces explicit block/override policy and platform-aware extraction, even though its output shape is not our target. |

The common thread is that modern SDL bindings expose macro constants broadly, but durable generators need policy and drift checks around the extraction.

## Design Decisions

### Source-First CppAst Collection

Add a new semantic translator, tentatively `BindingConstantTranslator`, wired from `CppAstToBindingModel`. It consumes macro data from the existing `CppAstParseResult` and per-view `CppCompilation.Macros`.

The translator does not run another parser and does not add a ClangSharp-style second pass. The current CppAst semantic pipeline remains the single source of parsed native facts.

### Candidate Scope

All object-like macros whose names start with `SDL_` and originate from owned SDL2.Core public parse inputs enter the macro classification pipeline.

"All object-like `SDL_*` macros" means all such macros are considered and reported. It does not mean every preprocessor control define becomes public API. Include guards, empty macros, private/internal header mechanics, and platform/compiler control defines are classified as non-API and reported with a skip reason.

Function-like macros are not constant candidates. They are reported separately so they do not disappear silently.

### Constant Classification

The translator classifies each candidate into one of these categories:

- **String literal**: emits as a UTF-8 span property.
- **Numeric literal**: emits as a `public const` integral value when the type is unambiguous.
- **Character literal**: emits as a numeric constant when the value is deterministic.
- **Alias**: references another generated macro constant when the alias target is safe and available.
- **Safe expression**: emits a compile-safe C# expression or computed numeric value when all referenced tokens are understood.
- **Unsupported expression**: is deferred with diagnostics instead of emitting suspicious C#.

Classification must preserve native identity separately from emitted C# text. Emitters render `BindingConstant`; they do not re-parse raw macro tokens.

### Emission Shape

String-like constants keep the current canonical low-level shape:

```csharp
public static ReadOnlySpan<byte> SDL_HINT_RENDER_DRIVER => "SDL_RENDER_DRIVER"u8;
```

Numeric constants emit as `public const` with the selected integral type.

The design intentionally does not generate parallel `const string` aliases. String ergonomics belong in function overloads and higher-level APIs, not by doubling the low-level constant surface.

### Manifest Manual Controls

`binding_generation.required_constants` remains available, but its role changes from primary source to manual include/seed path. The design also needs explicit exclude and override concepts, whether implemented by extending the existing configuration shape or by adding a new macro policy section.

Manual controls follow these rules:

- **Include/seed** adds a constant that the parser cannot see or that must be forced for a known reason.
- **Exclude** removes a generated macro from public output.
- **Override** replaces generated metadata such as managed type, value, expression, or documentation notes.
- Unused includes, excludes, and overrides are hard errors by default.
- An explicit manifest escape hatch may downgrade known stale entries to warnings, but it must be named, documented, and consumed; silence is not allowed.
- Manual override conflicts with source facts are hard errors unless the override explicitly documents and owns the conflict.

### Duplicate and Cross-View Rules

Macro facts are collected per parse view and merged deterministically by name.

- Compatible same-name definitions coalesce into one generated constant.
- Incompatible same-name definitions are hard errors with source-view evidence.
- Same-value aliases are allowed and recorded.
- Platform-specific or parse-view-specific macros are not silently dropped. They are either emitted with coherent metadata when safe or reported as unsupported/deferred with a reason.

This gives the broad collection strategy guardrails without shrinking back to a fragile allowlist.

### Reporting

`parse-views.json` should include macro audit data alongside the existing parse-view and semantic inventory data.

At minimum, it should report:

- Macro summary counts: parsed, candidates, emitted, skipped, excluded, overridden, duplicate-coalesced, unsupported, and conflicts.
- Per emitted macro: name, classified kind, emitted type/value shape, source header/view evidence, and alias/override metadata when applicable.
- Per skipped or unsupported macro: name, source evidence, and skip/defer reason.
- Per manual policy entry: whether it was consumed and how.

The report should be useful for answering "why do we have this constant?" and "why do we not have that constant?" without opening the generator internals.

## Component Shape

Expected production collaborators:

- `BindingConstantTranslator`: orchestrates macro collection, classification, merge, manual policy application, and `BindingConstant` output.
- `MacroCandidateCollector`: extracts raw macro records from CppAst parse results and source views.
- `MacroApiPolicy`: decides candidate, non-API, unsupported, and hard-block classifications.
- `MacroValueClassifier`: classifies string, numeric, character, alias, safe expression, and unsupported expression values.
- `MacroConstantMerger`: coalesces parse-view definitions and reports duplicate conflicts.
- `MacroManualPolicyApplier`: applies manifest include/exclude/override rules and validates stale manual entries.
- `MacroReportModel` or equivalent parse-view report records: carries audit evidence into `parse-views.json`.

These collaborators are named for behavior rather than generic pipeline roles. They should remain internal to the `GenerateBindings` target unless a second real consumer appears.

## Testing Strategy

Use TDD with embedded `.h` fixtures before production logic:

- CppAst fixture tests for object-like string, numeric, character, alias, expression, empty, include-guard, platform-control, and function-like macros.
- Translator tests proving all candidate categories map to `BindingConstant` or diagnostics.
- Duplicate tests for compatible and incompatible same-name definitions across parse views.
- Manifest policy tests for include, exclude, override, stale entries, and hard-error behavior.
- Emitter tests for UTF-8 span string constants and numeric `const` output.
- Report tests for macro summary counts and representative emitted/skipped/manual evidence.
- Real generation readiness probe proving multiple SDL hint constants are generated from headers without manifest entries.
- Compile-check remains the final generated-output gate across all library target frameworks.

## Risks and Mitigations

| Risk | Mitigation |
| --- | --- |
| Broad `SDL_*` collection emits header mechanics as public API. | Classify include guards, empty macros, platform/compiler controls, and private mechanics as non-API with report evidence. |
| CppAst macro values are token strings and may not be fully typed. | Use conservative value classification; emit only safe literals/expressions and report unsupported cases. |
| Manual override entries become stale. | Hard-error unused manual policy entries by default. |
| Cross-view definitions differ by platform. | Merge by name with compatibility checks; unsupported platform variance is reported rather than hidden. |
| Generated string values contain subtle typos. | Source-first extraction plus value-shape tests and parse report evidence prevent manual copy/paste drift. |
| API surface grows unexpectedly. | Broad candidates are intentional, but compile-check, report review, and non-API classification keep the surface explainable. |

## Success Criteria

- `SDL_HINT_RENDER_DRIVER` and other `SDL_HINT_*` values are generated from parsed headers without manifest-required entries.
- `required_constants` no longer needs to carry normal SDL header constants just to make readiness probes pass.
- Function-like macros are visible in diagnostics/reporting and absent from public constant output.
- Stale manual includes/excludes/overrides fail by default.
- Duplicate macro conflicts include enough source evidence to fix the policy or header classification.
- Generated constants compile across `LibraryTargetFrameworks`.
- `parse-views.json` can explain emitted, skipped, overridden, and unsupported macro constants.
