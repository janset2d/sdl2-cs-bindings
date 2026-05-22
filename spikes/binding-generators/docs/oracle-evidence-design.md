# ClangSharp Oracle Evidence Design

**Date:** 2026-05-22
**Status:** Draft approved for planning
**Scope:** Spike-local validation for `spikes/binding-generators/clangsharp/`

## Goal

Build a spike-local oracle/evidence loop that keeps the ClangSharp raw ABI output honest while preserving the ppy-style Python generation path.

The first implementation target is a .NET 10 file-based app at `spikes/binding-generators/clangsharp/oracle.cs`. It replaces the fragile C# regex extraction in `compare_oracle.py` with Roslyn-based structured evidence, then reports family-first raw ABI gaps across ClangSharp output, Cake preview output, SDL2-CS compatibility declarations, and SDL2 dynapi exports.

## Non-Goals

- Do not change `generate_bindings.py` away from the ppy-style orchestration model.
- Do not implement public typed low-level APIs.
- Do not implement friendly overloads.
- Do not implement ppy manual companion feedback in this slice.
- Do not wire spike validation into production Cake targets, CI, package projects, `build/manifest.json`, or project files.
- Do not make a final ClangSharp-vs-CppAst production recommendation from this evidence alone.

## Context

The ClangSharp spike currently proves that Core and Image generated output can build across the five target TFMs, but build success is not enough. Recent read-only audit found several raw ABI and evidence gaps:

- Raw ABI classes and methods currently leak as public declarations.
- `SDL.h` required functions and `SDL_INIT_*` constants are missing from the spike output.
- `SDL_RWops` full layout is emitted despite the Stage 1 opaque/quarantine policy.
- `SDL_SysWMinfo` and `SDL_SysWMmsg` full typed union layouts are emitted despite the Stage 2 deferral.
- C `long` and `unsigned long` are emitted with Windows-local shapes in shared output.
- `wchar_t*` is emitted as Windows-local `ushort*` in shared output.
- SDL2_image output currently uses `namespace SDL2` instead of the constitution target `SDL2.Image`.
- `compare_oracle.py` is Core-only, regex-based, and misses modern C# syntax shapes such as `[LibraryImport]` methods and expression-bodied UTF-8 span constants.

The oracle needs to become the dashboard for this raw ABI stabilization loop, not merely a one-off count comparison.

## Architecture Decision

Use a C# file-based app for oracle/evidence validation:

```text
spikes/binding-generators/clangsharp/oracle.cs
```

Command shape:

```pwsh
dotnet run --file spikes/binding-generators/clangsharp/oracle.cs -- --family sdl2-core --family sdl2-image --write-report
```

Rationale:

- Generated binding output is C#; Roslyn is the right parser.
- File-based apps avoid adding or modifying `.csproj` / `.sln` files during the spike.
- `tools.cs` already validates the repo's .NET 10 file-based app workflow.
- `generate_bindings.py` remains the ppy-style generation orchestrator.
- The oracle can later migrate to production Cake/.NET validation if the spike graduates.

## Evidence Sources

The oracle must label each source by authority and scope. Absence of evidence must not silently become failure.

| Source | Families | Authority | Scope |
| --- | --- | --- | --- |
| ClangSharp Compat output | Core, Image | Candidate spike output | Legacy raw ABI shape for `netstandard2.0` / `net462` |
| ClangSharp Modern output | Core, Image | Candidate spike output | Modern raw ABI shape for `net8.0+` |
| Cake generated preview | Core only today | Current production generator preview | Prior generated evidence, not independent upstream truth |
| SDL2 dynapi exports | Core only | Runtime symbol-name evidence | Function names only, not signatures or layout |
| SDL2-CS | Core, Image, later satellites | Compatibility signal | Legacy coverage/reference, not target API truth |
| Native exports | Core, Image, later satellites | Runtime symbol-name evidence | Per-RID exported names; initially report as available or missing |
| Pinned headers | All families | Native declaration source | Authoritative source, but platform/macro context must be handled explicitly |

## Structured Model

The app should extract a normalized model before rendering Markdown. The model should be serializable to JSON so future tests and reports can consume the same evidence.

### Source Model

Each source has:

- `sourceId`: stable identifier such as `clangsharp-modern`, `clangsharp-compat`, `cake-preview`, `sdl2-cs`, `dynapi`.
- `family`: `sdl2-core`, `sdl2-image`, and later other SDL2 families.
- `authority`: `candidate-output`, `generator-preview`, `runtime-symbol-name`, `compatibility-signal`, `header-source`.
- `path`: repo-relative input path.
- `status`: `available`, `missing`, or `not-wired`.

### Function Model

Each function/import declaration should include:

- Managed method name.
- Native entry point, using `EntryPoint` when present and method name fallback otherwise.
- Native library name, including const-backed `DllImport(LibName)` resolution where feasible.
- Containing namespace and type.
- Accessibility.
- Import kind: `DllImport`, `LibraryImport`, or none.
- Return type text.
- Parameter type/name list.
- `NativeTypeName` evidence on return and parameters.
- Platform attributes.
- Preprocessor guard evidence where detectable.
- Source file path.

### Type Model

The initial type model should include:

- Enums and explicit underlying type when present.
- Structs, nested structs, and field list.
- Opaque struct candidates.
- `[StructLayout]` and `[FieldOffset]` evidence.
- Function pointer fields and parameters.
- Constants from `const`, `static readonly`, and expression-bodied UTF-8 span properties.

## Roslyn Extraction Policy

Use Roslyn syntax as the default extractor and semantic resolution only where it earns its cost.

Syntax extraction should handle:

- Block and file-scoped namespaces.
- Class, struct, enum, field, property, method, and delegate declarations.
- `DllImport`, `LibraryImport`, `SupportedOSPlatform`, `UnmanagedCallConv`, `StructLayout`, `FieldOffset`, and `NativeTypeName` attributes.
- `const` fields, `static readonly` fields, and expression-bodied `ReadOnlySpan<byte>` properties.
- Nested declarations.
- Preprocessor directive trivia around platform attributes.

Semantic resolution should be used for:

- `DllImport(LibName)` and similar const-backed attribute values.
- Attribute identity normalization where syntax names are ambiguous.
- Constant values where syntax text is not enough.

Preprocessor profiles should be explicit:

- Compat profile for legacy output.
- Modern profile with `NET5_0_OR_GREATER` active when checking platform attributes.

## Report Shape

The primary Markdown report should be:

```text
spikes/binding-generators/output/reports/oracle-evidence-clangsharp.md
```

The report should be family-first:

```text
# ClangSharp Oracle Evidence

## Inputs
| Source | Family | Path | Authority | Scope | Status |

## Family: sdl2-core
### Surface Counts
### Raw ABI Constitution Checks
### Function Evidence Matrix
### Accepted Deferrals
### Evidence Gaps

## Family: sdl2-image
### Surface Counts
### Raw ABI Constitution Checks
### Function Evidence Matrix
### Accepted Deferrals
### Evidence Gaps
```

Classification labels:

- `Hard Bug`: contradicts pinned headers, native exports, or the binding-generator constitution.
- `Likely Bug`: strong evidence, but one source is missing or incomplete.
- `Accepted Deferral`: intentional policy deferral with a documented source.
- `Compatibility Risk`: mismatch with SDL2-CS that may affect migration but is not target truth.
- `Evidence Missing`: input source unavailable locally or not wired yet.
- `Out Of Scope`: source is not valid for this family or category.

## Initial Raw ABI Checks

The first implementation should include focused checks that explain the current high-risk gaps.

### Public Raw ABI Leak

Flag public raw ABI classes and public raw import methods.

Expected examples today:

- `SDLNative` is public in ClangSharp Core output.
- `SDL_imageNative` is public in ClangSharp Image output.

### Missing SDL.h Required Surface

For `sdl2-core`, compare generated functions/constants against `build/manifest.json` `required_functions` and `required_constants` for `SDL.h`.

Expected examples today:

- `SDL_Init`
- `SDL_InitSubSystem`
- `SDL_QuitSubSystem`
- `SDL_WasInit`
- `SDL_Quit`
- `SDL_INIT_*` constants

### Deferred Layout Violations

Flag full public layout emission for declarations the constitution defers or quarantines.

Expected examples today:

- `SDL_RWops` full layout.
- `SDL_SysWMinfo` full typed union layout.
- `SDL_SysWMmsg` full typed union layout.

### Platform-Sensitive Scalar Risks

Flag raw signatures using Windows-local C `long` or `wchar_t` shapes in shared output.

Expected examples today:

- C `long` as `int`.
- `unsigned long` as `uint`.
- `wchar_t*` as `ushort*`.

### Family Identity Drift

Flag generated namespace/class identity mismatches against the binding-generator constitution.

Expected example today:

- SDL2_image generated output uses `namespace SDL2` instead of `SDL2.Image`.

## Relationship To ppy

The ppy-style constraint applies to generation, not oracle implementation.

Keep these ppy-aligned in `generate_bindings.py`:

- Header-by-header generation.
- RSP policy layering.
- Future per-header `.rsp` lookup.
- Future validation feedback into generation where it belongs.
- Future manual symbol / typedef feedback only when companion code exists and earns its place.

Do not force oracle validation into Python just for aesthetic parity. Oracle validation is about understanding generated C# syntax and should use Roslyn.

## Later Hooks

These should be documented but not implemented in the first oracle slice:

- ppy manual `[Constant]` feedback.
- ppy `[Typedef]` feedback.
- `Unsafe_` string-return remap.
- Public typed method projection.
- Friendly string/span/ref/out overloads.
- Native export extraction through `dumpbin`, `nm`, or `otool`.

## Verification Plan

For the oracle slice, verification should include:

1. Run the file-based app against current ClangSharp output.
2. Confirm the report lists current known raw ABI gaps instead of hiding them behind build success.
3. Run `dotnet run --file spikes/binding-generators/clangsharp/oracle.cs -- --family sdl2-core --family sdl2-image --write-report`.
4. Run `git diff --check`.
5. If code changes are made, run Slopwatch with the repo-specific excludes from `AGENTS.md`.

## Open Design Questions

1. Should the first implementation emit both JSON and Markdown, or Markdown only with JSON added once tests exist?
2. Should `compare_oracle.py` be deleted immediately, or retained as a legacy report until `oracle.cs` reaches equivalent coverage?
3. Should native export discovery be a report-only lane first, or should missing local native export evidence fail the oracle command?

Current recommendation:

- Emit Markdown first and keep the in-memory model structured enough to add JSON without redesign.
- Retain `compare_oracle.py` until `oracle.cs` report proves equal-or-better coverage.
- Treat native exports as non-fatal evidence availability in the first slice.
