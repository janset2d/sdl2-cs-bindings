# Binding Generator Constitution

> **Status (2026-05-23):** Canonical binding-generator constitution. This document is the source of truth for generated SDL binding surface decisions: internal ABI, public API layering, C-to-C# translation, manifest responsibilities, and evidence gates. Code and pinned SDL public headers remain the north star; update this document in the same change when a generator rule changes.
>
> **Toolchain re-evaluation (2026-05-23):** The binding-autogen toolchain decision recorded in [ADR-004](../decisions/2026-05-14-binding-autogen-toolchain.md) is under active re-evaluation in [`spikes/binding-generators/`](../../spikes/binding-generators/). The **policy** in this document is binding regardless of which toolchain ships; **implementation-specific language** below (Cake paths, "CppAst engine" references, `tools.cs generate-bindings` invocation) describes the sunset Cake-hosted implementation under `build/_build/Targets/GenerateBindings/` and will be replaced when the spike concludes. Either path (ClangSharp + Roslyn postprocess or Alimer-style single-pass CppAst) implies a new implementation; the current Cake pipeline is in sunset regardless.

## Purpose

The generator must answer two questions without mixing them:

1. Which SDL declarations belong in a generated binding family?
2. How does each native declaration become ABI-correct, stable C#?

The internal ABI policy below is the durable contract for any generator implementation. Earlier work under a Cake-hosted `GenerateBindings` target (CppAst-based, ADR-004) implemented this contract end-to-end for SDL2.Core: manifest-driven family configuration, semantic type classification, source-first macro collection, generated handles/enums/structs/callbacks/constants, internal raw ABI command emission, dynapi name validation, and fixture-backed tests for the ABI-sensitive SDL2.Core blockers found on 2026-05-19. That implementation is in sunset pending the toolchain re-evaluation under `spikes/binding-generators/`; the policy resolutions catalogued in §"Current SDL2.Core ABI Status" remain binding requirements any compliant successor implementation must honor.

The remaining Stage 1 work is public surface completion and production flip on whichever implementation the spike selects, not re-proving the internal ABI constitution from scratch.

## Authority Order

Use this document for intended binding-generator policy, but verify current
behavior against the implementation and tests before making behavior claims. When
sources disagree, use this order:

1. Pinned SDL public headers from the exact vcpkg version.
2. Actual packaged native binary exports.
3. Current generator implementation and tests for actual behavior — during toolchain re-evaluation that means the spike under `spikes/binding-generators/`; the sunset Cake-hosted implementation under `build/_build/Targets/GenerateBindings/` remains historical evidence.
4. SDL dynapi manifests for function-name coverage only.
5. This constitution for intended policy.
6. `docs/binding-autogen/binding-generator-roadmap.md` for future work sequencing.
7. ADR-004 for the recorded 2026-05-14 CppAst toolchain reasoning (currently Reopened — see spike under `spikes/binding-generators/`).
8. Peer bindings and old research as evidence only.

Historical plans, spike reports, and superpowers specs are not policy. If they disagree with this document, this document wins. If the implementation disagrees with this document, treat it as code/docs drift to investigate, not as permission to ignore either source.

## Layer Contract

Generated bindings use three layers:

1. **Internal raw ABI layer**: generated, unsafe where needed, exact ABI, `internal`, and the only layer with `[DllImport]` or `[LibraryImport]`.
2. **Public typed low-level layer**: generated handles, enums, structs, callbacks, constants, and thin public methods that call the internal raw layer without exposing extern declarations.
3. **Public friendly overload layer**: generated conveniences for `string`, `ReadOnlySpan<byte>`, `Span<T>`, `ReadOnlySpan<T>`, `out`, `ref`, bool conversions, and explicit preformatted variadic helpers.

### Internal Raw ABI: Why / How / What

**Why:** Generated native imports are volatile implementation detail, not the user-facing compatibility contract. Keeping them internal lets the generator fix C `long`, `wchar_t`, bool wire shape, struct layout, platform attribution, and `DllImport` / `LibraryImport` backend choices without turning every correction into a public breaking change. Public raw bindings are a valid product choice for TerraFX/Silk.NET-style raw catalogs; this project is a stable SDL platform layer with typed low-level APIs and friendly overloads on top.

**How:** The raw ABI container type is internal. Generated members may remain lexically `public` when produced by upstream emitters, but they are not effectively public API when their containing type is internal. Public low-level APIs call the internal raw container and expose honest typed handles, `nint` values, spans, pointers, and unsafe overloads where SDL requires them.

**What:** The main package exposes a typed, low-allocation SDL API plus friendly overloads. It does not expose generated `[DllImport]` / `[LibraryImport]` classes as the blessed user-facing API. Escape hatches belong in typed handles, `DangerousGetHandle()` / `nint`, span/pointer overloads, and deliberately unsafe APIs. If demand appears later, a separate raw package or raw namespace can be designed with an explicit different compatibility promise.

Rules:

- Public raw `IntPtr` externs are not part of v1 preview.
- SDL2-CS compatibility is best-effort. It is an oracle, not the API target.
- Raw ABI mistakes are still bugs even though the raw layer is internal.
- A declaration that cannot be represented honestly is deferred with evidence. Do not emit a success-shaped lie.
- Typed handles expose native pointer values through `nint`; that does not make `nint` the answer for every native scalar.
- Raw ABI backends may split into `DllImport` and `LibraryImport` generated files, but both consume the same semantic/projection truth. File-level TFM guards are preferred over per-function conditional sprawl.
- Compatibility packages such as `System.Memory` are acceptable for public/friendly APIs on `netstandard2.0` and `net462` when package smoke proves the consumer contract. They must not be used to fake ABI primitives whose platform shape is not portable.

## Generator Home

The generator is build infrastructure, not a standalone product project. Whichever toolchain the active spike (`spikes/binding-generators/`) selects, the production implementation must satisfy these contracts:

- Produce committed `.g.cs` source consumed by managed family csprojs. Generation never runs in consumer builds.
- Reproducible output anchored by a per-family `.generated-stamp` with no wall-clock fields.
- Cross-cutting validators reachable from the build host's PreFlight and Pack stages.
- Persisted binding-generation data contracts reachable from the build host's Data layer.
- Local invocation routed through `tools.cs` so day-to-day developers do not handle generator orchestration directly.
- Linux-canonical parsing for ABI correctness across the 7-RID surface unless the spike proves a different model is equivalent.

The sunset Cake-hosted CppAst implementation lived at `build/_build/Targets/GenerateBindings/` with tests under `build/_build.Tests/Unit/Targets/GenerateBindings/`, validators under `build/_build/Validation/BindingGeneration/`, data contracts under `build/_build/Data/BindingGeneration/`, and local invocation through `dotnet run --file tools.cs -- generate-bindings` inside a pinned Linux builder container. Those paths remain in the tree as historical reference but are not the path-of-record while the spike runs.

## Generator Engine And SDL Policy

The generator should separate parser/ABI mechanics from SDL family policy without pretending to be a general binding-generator product.

Rules:

- The core pipeline is an ABI engine for parsed declarations, native type classification, platform parse-view merge, raw ABI projection, and deterministic file-set emission. ClangSharp orchestrator + Roslyn postprocess and CppAst single-pass emitter are both viable engine shapes; the active spike under `spikes/binding-generators/` is selecting between them.
- SDL-specific decisions live behind named policy/profile concepts: owned prefixes, core-owned type references, SDL2 versus SDL3 bool shape, known opaque structs, string-like macro handling, SysWM layout, and satellite-to-core reference rules.
- Keep those concepts target-local under `Targets/GenerateBindings/` until a second real generator target exists. Do not promote them to root `Shared` or standalone `src/` projects for aesthetic symmetry.
- M2 may expose an `SdlPolicy` seam while preserving SDL2.Core output. M3 owns the real profile/config boundary for SDL2 core, SDL2 satellites, SDL3 core, and SDL3 satellites.
- Generic model-building or emission code must not accumulate ad hoc `/SDL2/`, `SDL_`, `SDL2`, or library-name checks once a named policy/profile seam exists. Add a policy collaborator instead.
- Satellite profiles must be designed against real installed headers before generation is enabled. SDL2.Image / Mixer / Ttf mostly use SDL's `extern DECLSPEC` convention, while SDL2_gfx uses per-header `SDL2_*_SCOPE` export macros and a mixed naming surface; this is profile policy, not a reason to special-case generic CppAst processing.
- `profile_id` is a manifest routing key into code-owned profile policy. It is not a behavior switch that lets JSON redefine ABI rules.

## Generation Determinism Contract

The generator pipeline (ClangSharp/CppAst engine + Roslyn postprocess + per-family orchestration) is a **deterministic function** from a pinned input set to committed `.g.cs` output. Determinism is not a quality-of-life property; it is policy. The following invariants are binding regardless of which toolchain ships.

### Determinism Inputs (the pin set)

Every output byte is determined by these inputs alone. Output reproducibility means: same pin set → byte-identical output (CRLF aside on Windows).

1. **Vcpkg-installed native headers**, pinned via `build/manifest.json library_manifests[].vcpkg_version` per family (SDL2 Core 2.32.10, SDL2_image 2.8.8, SDL2_ttf 2.24.0, SDL2_mixer 2.8.1, SDL2_gfx 1.0.4 at the audit date above). Manifest is the single source of truth for which native headers exist.
2. **Vcpkg triplet** (e.g. `x64-windows-hybrid`). CI matrix pins one triplet per RID.
3. **Generation engine version** — ClangSharp tool version (`dotnet-tools.json`) when the spike selects ClangSharp + Roslyn postprocess; equivalent CppAst version anchor when the spike selects the CppAst single-pass alternative.
4. **RSP files** (cross-cutting `rsp/base.rsp` + family `rsp/sdl2-<family>.rsp` + per-header `rsp/per-header/<header>.rsp`). All versionable text in git.
5. **Production per-family header lists** — one complete header set per active family, currently represented by `FAMILY_CONFIG[family]["headers"]` in the ClangSharp spike. Bootstrap/header-subset lists are not production inputs.
6. **Config file** — `config/family-config.json` (per-family identity, opaque handles, flags enums, C long method lists, platform views, header inventories, required surface). All auditable single sources of truth for generator and postprocess data input.
7. **Postprocess code** — the rewriter implementations under `postprocess/` (or equivalent under the selected toolchain).
8. **Orchestrator code** — `generate_bindings.py` (`FAMILY_CONFIG`, `PLATFORM_SENSITIVE_HEADERS`, `selected_families`, pipeline order).

If none of these change, regeneration produces byte-identical output. The determinism unit is the complete selected family artifact: that family's `Generated/` root, including Compat and Modern backend projections plus every postprocess output written under that root. Wall-clock fields, machine identifiers, build timestamps, or environment-derived values are **forbidden** in any committed `.g.cs` or in the per-family `.generated-stamp` (deferred to Roadmap M7 production flip).

Compat and Modern are internal backend projections of the same family artifact. Production generation always runs them together in order, not as separate CLI generation units. `libraryimport` is a Modern-only postprocess implementation detail inside that complete-family run. Production generation must not write partial bootstrap/header-subset output into committed `Generated/` roots.

### Family Isolation

The pipeline guarantees three simultaneous properties:

1. **Targeted-family-only writes.** `--family X --execute` cleans and regenerates **only** `Janset.SDL2.<X>/Generated/`. Other families' directories are byte-untouched (`git status` reports zero changes outside the targeted family). Execute-mode cleanup deletes only the selected families' Generated trees, never others.
2. **Per-family equivalence.** Running each family individually produces the same `.g.cs` output as running `--family all` (modulo the `selected_families("all")` activation set; dormant families remain dormant on both paths).
3. **Independent postprocess execution.** Each family's postprocess pipeline executes against that family's own Generated tree only. It never reads cross-family `.g.cs` files. The only cross-family data flow at postprocess execution time is the roster JSON pull described under §"Opaque Handles" Cross-family handle name resolution — a **data-only** pull from the single roster file, not a cross-directory file read.

### Dependency Direction

Two distinct dependencies, not to be conflated:

- **Generation → postprocess (forward, runtime).** Generation writes `.g.cs`; postprocess reads `.g.cs`. Generation is independent of postprocess; postprocess depends on generation's output. Raw generation output is itself deterministic and idempotent — postprocess transforms it but does not feed back into generation.
- **Build-time Core ← Image (ProjectReference).** Image's compiled `.dll` resolves Core-owned type names (`SDL_Renderer`, `SDL_RWops`, ...) against Core's `Handles.g.cs` via ProjectReference. This is a **C# compile-time** dependency; the generator pipeline never reads cross-family `.g.cs` files at generation or postprocess execution time. Core can be regenerated before, after, or independently of Image — the generation pipeline imposes no execution-time ordering. The only ordering constraint is on the **consumer build** of the produced packages, not on their generation.

A satellite's regeneration consumes Core's `binding_generation.required_constants` / `required_functions` only at the manifest level (configuration shared via `build/manifest.json`); it does not consume Core's generated `.g.cs` artifacts.

### Native Header Resolution Scope

The generation engine's `--include-directory` (ClangSharp) or equivalent (CppAst) exposes the entire SDL2 native header tree to every per-family parse invocation. The `--file` flag scopes **emit** to one header at a time; `#include`'d type declarations from other headers are **resolved for correctness but never emitted** in the family's output. Concretely:

- A satellite's `IMG_LoadTexture(SDL_Renderer*, ...)` parse resolves `SDL_Renderer` via Core's `SDL_render.h` (visible through `--include-directory`) but emits the signature only into the satellite's family-owned `.g.cs` file.
- Across the entire generated output for all five families, every public C type is defined exactly **once** (in its owning family's `.g.cs`); satellite `.g.cs` files reference Core types by name and rely on ProjectReference + nested namespace resolution at C# compile time.
- Duplicate emission of a Core type across families is a **generator bug**, not a coexistence pattern.

### Pure-Inputs Discipline

Generation is not allowed to consume environment-derived inputs beyond the pin set above. Specifically:

- **No machine-local paths** in committed output (paths under `--include-directory` are only used for parse; they do not leak into emitted attribute arguments).
- **No timestamps**, build dates, machine names, user names, or CI run identifiers in committed `.g.cs` content.
- **No conditional behavior on host OS** at generation time except via the explicit platform-view pass (`PLATFORM_SENSITIVE_HEADERS` + ClangSharp `--define-macro`/`--undefine-macro` semantics). Synthetic platform header shims (`shims/platform-headers/`) are spike-only iteration aids for Windows-local generation; production generation runs against native platform headers via the binding-generator docker container or per-RID CI.
- **No network access** at generation time. All inputs must be local files reproducible from the pin set.

### Verification Contract

Every Item-1+ slice that touches generation or postprocess MUST verify the determinism contract in its exit evidence:

- **Complete-family idempotency:** regenerate the selected complete family artifact twice with identical inputs; second `git diff --ignore-cr-at-eol` is empty across the family's whole `Generated/` root, including Compat, Modern, and postprocess output.
- **Family isolation:** `--family <one> --execute` cleans/regenerates only that family's complete `Generated/` root and leaves other families' roots byte-untouched (`git status` per family directory).
- **Per-family equivalence:** `--family all` byte-equivalent to the union of per-family complete-artifact runs (modulo dormant set).
- **Cross-family handle pull preserved:** satellite output continues to rewrite Core-owned pointer types to by-value (e.g. `SDL_Renderer*` → `SDL_Renderer` in `Janset.SDL2.Image/Generated/`).

Slices that change the determinism contract itself (e.g. add a new pin-set input, change family isolation semantics) must update **this section** in the same change set, with rationale and the new verification step. Drift between the contract and the implementation is treated as a hard bug.

## Manifest Configuration Vs Code-Owned Policy

`build/manifest.json library_manifests[].binding_generation` is per-family configuration. It is not a hidden policy language.

Manifest owns reviewable facts that vary by family:

- `enabled`
- `profile_id`
- `managed_namespace`
- `primary_class_name`
- `platform_catalog`
- `owned_prefixes`
- `parse_defines`
- `clang_args`
- `header_set`
- `excluded_functions`
- `required_functions`
- `required_constants`
- `macro_constants.excluded`
- `macro_constants.overrides`
- `deferred_declarations`
- `validators`
- `dynapi`

M3 may add `export_macro_names` as declaration-visibility token inventory when tests prove it earns its place. The field lists family-specific macro names; code-owned declaration visibility strategy decides how those names affect parsing, macro suppression, and export evidence.

Raw ABI class name, native import name, and satellite core-reference identity are derived first from existing manifest facts (`primary_class_name`, `library_manifests[].name`, and `package_families[].depends_on`). Promote them into explicit manifest fields only when a RED test proves the convention is insufficient for a real family.

Generator code owns ABI/API policy:

- Scalar width mapping.
- SDL2 vs SDL3 bool wire shape.
- Opaque-handle detection.
- Pointer, array, callback, function-pointer, and userdata classification.
- UTF-8 string and span overload behavior.
- C variadic handling.
- Struct/union layout policy.
- Macro taxonomy and safe expression evaluation.
- Platform parse-view merge and attribution rules.
- Public API layering.

Exception rule:

- Manifest exceptions must name the declaration, category, source/reason, and rationale.
- Silent JSON knobs that change ABI behavior are forbidden.
- Move behavior into the manifest only when it genuinely varies by family or needs an explicit per-family override.
- If every family must obey the same rule, keep it in code and tests.
- A manifest fact may route to a code-owned profile or named exception. It must not encode scalar width, bool wire shape, pointer classification, callback handling, variadic behavior, macro taxonomy, platform merge, or struct/union layout.

### Configurable Scope Vs Policy Mechanism

The binding generator has two kinds of configuration, separated by the question each answers:

- **Config owns the application scope** — answers **"which"** and **"what"**. Which families exist, which headers belong to them, which methods are affected by C `long` dispatch, which enum names get `[Flags]`, which families own opaque handles. These are per-family facts that change when a family is added, an SDL version introduces new types, or an audit expands coverage. They belong in auditable config files, not buried in code. Config is data; it must not encode *how* the policy operates.

- **Code owns the policy mechanism** — answers **"how"**. How a C `long` return gets emitted as `CULong` + `LibraryImport` on Modern versus `RuntimeInformation` dispatch on Compat. How the suffix rule and allow-list combine to decorate `[Flags]`. How Pattern B handle structs are templated from `nint` fields. How `SDL_GUID` maps to `System.Guid`. These are invariant across families — adding a new family does not change the mechanism. They belong in code and tests; they must not be driven by JSON boolean flags that silently alter ABI behavior.

Rules:

- Per-family data that answers "which symbols / which families" goes in config (e.g. `clong_methods`, `flags_enums.allow_list`, `opaque_handles.force_opaque_exceptions`).
- Cross-family constants that are mechanically derived from SDL2 source headers (platform view definitions, platform macro enumeration) are config data, not code.
- Code that transforms generated `.g.cs` output (rewriter implementations, pipeline orchestration) is policy mechanism and stays in code.
- Config is reviewable data read by both the generator orchestrator and the postprocess toolchain. Both consumers read the same file for their relevant sections — no per-consumer parallel copies of the same fact.
- Before moving a fact into config, ask: "does this vary by family, or could it vary by family?" If yes, it belongs in config. If the answer is "no, this is the same for every family forever" and it concerns *how* a transform operates, it stays in code.
- When in doubt, prefer config for "which" facts that would need updating if a new satellite library were added to the generator scope.

## Family Identity

Generated C# identity is family-owned. The public namespace and primary public class are manifest-driven today; the internal raw ABI class and native import library are intended family identity seams and may be code-derived until M3 profile/config work promotes them explicitly.

| Family | Namespace | Public class | Internal raw ABI class |
| --- | --- | --- | --- |
| SDL2 core | `SDL2` | `SDL` | `SDLNative` |
| SDL2_image | `SDL2.Image` | `SDL_image` | `SDL_imageNative` |
| SDL2_mixer | `SDL2.Mixer` | `SDL_mixer` | `SDL_mixerNative` |
| SDL2_ttf | `SDL2.Ttf` | `SDL_ttf` | `SDL_ttfNative` |
| SDL2_gfx | `SDL2.Gfx` | `SDL2_gfx` | `SDL2_gfxNative` |

Parse views such as `Neutral`, `WindowsDesktop`, `Linux`, and `MacOS` are parser metadata and file attribution. They do not become public class names.

## Function Surface

Function inclusion is header-first and validated by available native evidence.

Rules:

- A generated function must trace to a pinned public header declaration or a required manifest declaration from an intentionally excluded umbrella header.
- A generated SDL2.Core function name must match dynapi/export evidence unless explicitly excluded or deferred.
- Dynapi validates names only. It does not prove parameter order, scalar width, struct layout, enum backing type, or ownership semantics.
- SDL2 satellite function names are validated by a family-specific evidence source because satellites do not ship SDL2.Core's dynapi manifest. For Image/Mixer/Ttf this starts from public `extern DECLSPEC` declarations; for SDL2_gfx it starts from the `SDL2_GFXPRIMITIVES_SCOPE`, `SDL2_IMAGEFILTER_SCOPE`, `SDL2_ROTOZOOM_SCOPE`, and `SDL2_FRAMERATE_SCOPE` declaration macros plus harvested binary symbol evidence when available.
- Platform-specific declarations stay in the family raw ABI class and receive platform attribution.
- `SDL_main`, `SDL_DYNAPI_entry`, startup glue, and dynapi internals are not ordinary public binding functions.

Current accepted deferrals:

- `FILE`, `_IO_FILE`, `va_list`, and `__va_list_tag` APIs are deferred unless a portable mapping is deliberately designed.
- `SDL_RWFromFP`, `SDL_LogMessageV`, `SDL_vsnprintf`, `SDL_vsscanf`, and `SDL_vasprintf` are Stage 1 accepted deferrals.

## BCL-Replaceable Helper Exclusion Policy

SDL2 ships convenience helpers that duplicate functionality already in the
.NET Base Class Library (BCL). SDL provides these because libsdl targets
platforms with incomplete or missing libc primitives; .NET runtimes always
carry the BCL, so re-binding these helpers is ceremony with **negative**
ergonomic payoff (caller learns a second API to do what BCL already does,
plus an extra P/Invoke hop).

**Rule.** A symbol or function family is excluded from the Janset.SDL2 surface
when **all three** conditions hold:

1. A direct BCL equivalent exists with equal or better ergonomics
   (`System.Math.Round` for `SDL_lround`; `long.Parse` for `SDL_strtol`;
   `System.Text.Encoding` for `SDL_iconv_*`).
2. SDL2-CS (the reference binding) does not expose the symbol/family.
3. No other SDL2 symbol transitively depends on the excluded symbol
   (verified by grep across `vcpkg_installed/<triplet>/include/SDL2/*.h`).

**Mechanism.** `--exclude <symbol>` in the relevant per-header RSP
(`spikes/binding-generators/clangsharp/rsp/per-header/<header>.rsp`).
The RSP comment block above the exclude block records the rule's three
conditions and the BCL equivalent.

**Standing exclusions (Priority C):**

- `SDL_lround`, `SDL_lroundf`, `SDL_ltoa`, `SDL_ultoa`, `SDL_strtol`,
  `SDL_strtoul` (SDL_stdinc.h) — Decision 2 Step 9. BCL: `Math.Round`,
  `long.Parse`, `ToString()`, `ulong.Parse`.
- `SDL_iconv_open`, `SDL_iconv_close`, `SDL_iconv`, `SDL_iconv_string`
  (SDL_stdinc.h) — Slice C-B drift resolution. BCL: `System.Text.Encoding`
  family. Also implicitly excludes `SDL_iconv_t` opaque type at call-site
  scope (no remaining function references it).

**Non-rule.** Excluding `--exclude SDL_X` does NOT cascade through dependent
typedefs automatically. The `SDL_iconv_t` type stays declared (empty struct)
unless explicitly excluded too — but with no remaining function consuming it,
the empty struct is harmless residue. Document the residue in the roster
JSON's `excluded_candidates` field for audit-trail.

## C Variadics

C ellipsis functions are not exactly representable in portable C# P/Invoke.

Stage 1 policy is deliberate fmt-only mapping:

- Non-`va_list` SDL variadic functions may be emitted internally only as fixed-prefix, fmt-only calls.
- The public/friendly API must make the preformatted-string contract explicit.
- These imports must not be documented or reported as exact C varargs ABI declarations.
- `__arglist` is rejected for this repo because it does not compose with the planned string/span overload tiers and old TFM support.
- `va_list` variants remain deferred.

Examples:

- `SDL_Log(byte* fmt)` means "log this already formatted UTF-8 string," not "forward arbitrary C varargs."
- `SDL_snprintf(byte* text, nuint maxlen, byte* fmt)` and `SDL_sscanf(byte* text, byte* fmt)` require explicit public wrapper policy before promotion beyond internal raw usage.

Required evidence:

- parse/report output distinguishes fmt-only mapped variadics from exact functions;
- friendly wrappers make formatting behavior visible;
- tests prevent accidental treatment of `...` as ordinary dropped parameters.

## Scalar Type Translation

Exact-width SDL typedefs map by width.

| Native type | Managed raw concept |
| --- | --- |
| `Sint8` / `Uint8` | `sbyte` / `byte` |
| `Sint16` / `Uint16` | `short` / `ushort` |
| `Sint32` / `Uint32` | `int` / `uint` |
| `Sint64` / `Uint64` | `long` / `ulong` |
| `size_t` | `nuint` |
| `ptrdiff_t` | `nint` |
| pointer values / `void* userdata` | `nint`, `void*`, or typed pointer by layer |

### C `long` And `unsigned long`

C `long` is platform-sensitive, not pointer-sized.

Facts:

- Windows LLP64: `long` and `unsigned long` are 32-bit on x86, x64, and arm64.
- Unix LP64 target RIDs in this repo: `long` and `unsigned long` are 64-bit on x64 and arm64.
- `nint`/`nuint` are wrong for Windows x64/arm64 C `long` because they become 64-bit where C `long` is 32-bit.
- `System.Runtime.InteropServices.CLong` and `CULong` model this correctly on modern TFMs but are unavailable to all current target frameworks.

Contract:

- Do not map C `long` or `unsigned long` directly to `nint` / `nuint` in shared generated signatures.
- Internal raw ABI uses `CLong` / `CULong` where available and guards these members to `NET6_0_OR_GREATER` until a downlevel exact strategy exists.
- Public typed wrappers may normalize values to stable managed shapes such as `long` / `ulong`, with Windows range checks for input parameters when needed.
- Typedefs over C `long`, such as `SDL_threadID`, inherit this policy unless a stronger SDL semantic type is introduced.
- Do not introduce a casual downlevel `CLong` / `CULong` NuGet polyfill. A same-named portable struct backed by `IntPtr`, `int`, or `long` would be wrong for at least one of Windows LLP64 or Unix LP64. Any downlevel strategy must prove exact per-platform ABI shape before removing guards.
- SDL_ttf already exposes raw C `long` in `TTF_OpenFontIndex*` and `TTF_FontFaces`; satellite profiles must reuse the same C `long` policy rather than treating it as an SDL2.Core-only edge case.

Priority C hybrid strategy:

**Why:** SDL2's `long`-using API splits into two categories. SDL_stdinc convenience helpers (`SDL_lround`, `SDL_lroundf`, `SDL_ltoa`, `SDL_ultoa`, `SDL_strtol`, `SDL_strtoul`) have direct BCL equivalents (`Math.Round`, `long.Parse`, `ToString()`); SDL2-CS dropped them entirely 10+ years ago without consumer impact, and the SDL2 wiki does not document them as user-facing API. Structural symbols (`SDL_threadID` typedef plus `SDL_ThreadID` / `SDL_GetThreadID` functions) identify OS threads; `System.Threading.Thread.ManagedThreadId` is not equivalent (different ID space).

**How:**

- Convenience helpers (SDL_stdinc family) are excluded from raw ABI emission on every TFM via per-header RSP `--exclude`; consumers use the BCL equivalents.
- Structural thread API symbols use the hybrid emit, mode-split across the two generated trees: `Generated/Modern/**/*.cs` (compiled for `net6.0+` via csproj `<Compile Include>` conditional) gets `[LibraryImport]` with `CLong` / `CULong` return type; `Generated/Compat/**/*.cs` (compiled for `netstandard2.0` / `net462` only) gets a managed wrapper with `RuntimeInformation.IsOSPlatform(OSPlatform.Windows)` dispatching between `[DllImport]` with `uint` return (Windows: C `unsigned long` = 32-bit) and `[DllImport]` with `nint` return (Unix LP64: C `unsigned long` = 64-bit), normalized to `ulong` at the caller surface — Microsoft's [documented cross-platform `long` dispatch pattern](https://learn.microsoft.com/en-us/dotnet/standard/native-interop/best-practices). Each output emits the single TFM-appropriate form (no `#if NET6_0_OR_GREATER` directives); TFM gating happens via the csproj's conditional `<Compile Include>` items, mirroring the existing `libraryimport` postprocess split (Compat keeps `[DllImport]`; Modern is rewritten to `[LibraryImport]`).
- The "any downlevel strategy must prove exact per-platform ABI shape" requirement above is satisfied before production source promotion by a per-RID runtime ABI smoke test calling each retained symbol and asserting non-zero bit-pattern on each of the 7 supported RIDs. The Spike C local evidence covers Windows x64 + Linux x64 ABI-family smoke; remaining RID proof joins through the production CI matrix.

**What:** Six SDL_stdinc convenience symbols deferred all-TFM. Three SDL_thread symbols (the `SDL_ThreadID` / `SDL_GetThreadID` functions and the `SDL_threadID` typedef they return) preserved on every TFM via the hybrid emit. Satellite C `long` surface (SDL_ttf `TTF_OpenFontIndex*` / `TTF_FontFaces`) reuses this same hybrid pattern when those satellites enter generation.

High-risk SDL2.Core symbols (Priority C disposition):

- `SDL_lround`, `SDL_lroundf` — **deferred all-TFM** (BCL equivalent: `Math.Round`)
- `SDL_ltoa`, `SDL_ultoa` — **deferred all-TFM** (BCL equivalent: `value.ToString()`)
- `SDL_strtol`, `SDL_strtoul` — **deferred all-TFM** (BCL equivalent: `long.Parse` / `ulong.Parse`)
- `SDL_threadID`, `SDL_ThreadID`, `SDL_GetThreadID` — **kept on every TFM** via hybrid CLong + dual-dispatch emit

### `SDL_bool`

SDL2 and SDL3 bool-like values are separate policies.

| Family | Native shape | Raw wire type | Friendly shape |
| --- | --- | --- | --- |
| SDL2 | `typedef enum { SDL_FALSE = 0, SDL_TRUE = 1 } SDL_bool` or `typedef int SDL_bool` fallback | `int` | `bool` conversion uses `!= 0` |
| SDL3 | C bool-like 1-byte value | `byte` or byte-backed wrapper | `bool` conversion |

Contract:

- SDL2 `SDL_bool` public enum must be int-backed.
- Mapping SDL2 `SDL_bool` to `byte` is ABI-wrong.
- SDL3 bool policy must not be copied from SDL2.

### `wchar_t`

`wchar_t` is platform-sensitive.

Facts:

- Windows `wchar_t` is 2 bytes.
- Unix-like target RIDs generally use 4-byte `wchar_t`.
- C# `char*` is always 2-byte UTF-16 code units.
- C# `int*` is always 4-byte elements.

Contract:

- Do not map `wchar_t*` to `int*` or `char*` in shared cross-platform generated public structs or signatures.
- Low-level raw shape uses opaque pointer representation unless a platform-specific helper is generated.
- Friendly APIs may decode wide strings only through platform-aware helpers with tests.

Priority C mechanism:

**Why:** Opaque `nint` at the raw ABI layer is the ABI-correct mapping for shared `wchar_t*`, not a Layer 3 friendly-wrapper repair of a "broken" ABI. No portable C# primitive maps correctly across the target RID set; Microsoft's BCL has no portable `wchar_t` story (`[MarshalAs(UnmanagedType.LPWStr)]` and `CharSet.Unicode` are hardcoded 16-bit even on Linux, mismatching POSIX's 32-bit `wchar_t`). Peer evidence: Silk.NET's `wchar_t -> char` is silently wrong on POSIX (anti-pattern); SDL2-CS dropped the entire `SDL_hid_*` API rather than bind it incorrectly; ppy SDL3-CS uses opaque `IntPtr` raw. Opaque pointer is the only ABI-honest mapping at Layer 1.

**How:** ClangSharp-style implementations use RSP-level `--remap` with space-containing variants for libclang byte-exact match (`wchar_t *=nint`, `const wchar_t *=nint` — unquoted, one per line, because System.CommandLine treats each RSP line as one argv element verbatim); CppAst-style implementations use the type classifier's `wchar_t* -> nint` rule plus a wide-string policy override for HIDAPI field/parameter cases. Postprocess `WcharStarToNintRewriter` is available as fallback when the type-system path does not reach a specific case. Windows-only API surface like `SDL_WinRTGetFSPathUNICODE` retains its existing `[SupportedOSPlatform("windows")]` attribution.

**What:** Shared `wchar_t*` (HIDAPI fields, `SDL_wcs*` functions, ~15 SDL2.Core symbols) emit as `nint` at the raw ABI layer. Layer 3 friendly wrappers (later slice) provide platform-aware decoders (`Marshal.PtrToStringUni` on Windows; UTF-32 transcode on POSIX) — those are ergonomics, not ABI repair.

## Opaque Handles

SDL-owned opaque handles are public readonly value types wrapping `nint`.

Rules:

- Emit one public handle type per SDL public typedef concept.
- Prefer the public typedef name over the underlying C struct tag.
- Do not emit duplicate public handles for `typedef struct tag Name;` or `typedef struct tag* Name;` patterns.
- The underlying C struct tag is parser evidence, not automatically public .NET API.
- Handle types expose a native value escape hatch for advanced consumers; that is not a license to expose public raw externs.

Resolved examples:

- `SDL_hid_device_` must not be emitted alongside canonical `SDL_hid_device`.
- `SDL_semaphore` must not be emitted alongside canonical `SDL_sem`.

Public typed handle struct shape:

**Why:** A `readonly partial struct X(nint value)` with a single pointer-sized field is ABI-equivalent to passing a bare `IntPtr` at the P/Invoke boundary — verified against the SysV x64, AAPCS64, MSVC ARM64, and x86 ABI specs (a single-integer-field composite is classified identically to that integer in every calling convention this repo targets). It is blittable; `LibraryImport` source-generated marshalling supports it without `[MarshalAs]`; `DllImport` legacy marshaller uses the blittable fast path. Cake's `RawAbiCommandEmitter`, Alimer.Bindings.SDL, TerraFX.Interop.Windows, and Silk.NET (non-readonly variant) all emit P/Invoke signatures using typed-handle-by-value. The "use IntPtr only at the P/Invoke boundary" advice in older interop literature predates modern .NET features and never reflected an ABI constraint.

**How:** Each opaque handle emits as a `public readonly partial struct X(nint value) : IEquatable<X>` with explicit `[StructLayout(LayoutKind.Sequential)]`, get-only `Value` property, `IsNull` / `IsNotNull` / `Null` sentinels, `DangerousGetHandle()` escape hatch, full equality contract (`Equals` / `GetHashCode` / `==` / `!=`), and **explicit** `operator nint` / `operator X` only — no implicit operator. Implicit conversion weakens the type safety the struct provides; the deliberate-escape case uses `DangerousGetHandle()`.

The struct is lexically `public` (so Layer 2 public methods can use it in their signatures) but appears in `internal` raw ABI signatures inside the internal `SDLNative` / family raw container — visibility-wise public, effectively internal API because the containing raw class is internal (Layer Contract §"Internal Raw ABI: Why / How / What"). Raw signatures pass by value: `internal static partial SDL_Window SDL_CreateWindow(...)`, not `internal static partial SDL_Window* SDL_CreateWindow(...)`. Single-pointer references rewrite to by-value at all three raw-ABI positions — method parameter, method return, and struct field; double-pointer (`X**`) and `out X` parameter positions are preserved as-is.

**What:** Every SDL opaque concept emits one typed handle struct of this shape. Both auto-detected empty-body opaques (14 names — canonical enumeration lives in `spikes/binding-generators/clangsharp/policy/opaque-handle-roster.json` `auto_detect_well_known` field: SDL_Window, SDL_Renderer, SDL_Texture, SDL_AudioStream, SDL_GameController, SDL_Joystick, SDL_Haptic, SDL_Sensor, SDL_Cursor, SDL_Thread, SDL_mutex, SDL_sem, SDL_cond, SDL_hid_device) and force-opaque types from a Constitution-bound allow-list (SDL_RWops, SDL_SysWMinfo, SDL_SysWMmsg per §"Structs And Unions") use the same shape uniformly. The Layer 2 public typed low-level slice that follows reuses these handle types in its public method projection — no Layer 2 work for the handle types themselves.

**Implementation mechanism (ClangSharp + Roslyn postprocess):** The `OpaqueHandleEmitRewriter` ([`spikes/binding-generators/clangsharp/postprocess/OpaqueHandleEmitRewriter.cs`](../../spikes/binding-generators/clangsharp/postprocess/OpaqueHandleEmitRewriter.cs), invoked via the `uniform-opaque` postprocess mode) realizes this policy across two input channels — auto-detect and a force-opaque allow-list — both feeding the same Pattern B template `BuildPatternBStruct(name)` and emitting the struct shape verbatim per the **How** clause above. The rewriter also rewrites single-pointer references to handle types throughout the raw ABI surface — method parameter positions, method return positions, and **struct field positions** — to by-value. Double-pointer `X**` and `out X` positions are preserved as-is. The ABI invariant holds because Pattern B structs carry exactly one `nint` field; their layout is bit-identical to a pointer at the corresponding position. Field-position rewrite improves API ergonomics (caller avoids explicit dereferencing) without changing native C struct layout: an `SDL_SysWMmsg* msg` C field reads as a `SDL_SysWMmsg msg` C# field with the same 8/4-byte slot.

**Auto-detect criterion (syntactic).** A type is auto-detected as a Pattern B candidate iff (1) its generated declaration is an empty `public partial struct X { }` (no body members) **and** (2) the same name `X` appears as a pointer type (`X*`) in at least one raw ABI signature position — parameter type or return type — anywhere in the generated output for the same TFM view. Detection is purely syntactic over the post-ClangSharp output: it does not depend on `[NativeTypeName("X *")]` annotations, because ClangSharp omits `NativeTypeName` when the C tag/typedef name matches the emitted C# name (the common case for opaque handles such as `SDL_Window` or satellite-owned handles such as `TTF_Font` and `Mix_Music`). The intersection of the two sets — empty-struct declarations and pointer-use sites — is the canonical auto-detect roster. **The criterion is family-blind:** SDL2.Core handles (`SDL_*` prefix) and satellite-owned handles (`TTF_Font`, `Mix_Music`) satisfy the same structural test; the rewriter does not gate on a name prefix.

**Canonical roster (machine-readable).** The family-keyed roster lives within `family-config.json` at `config/family-config.json`. It is the single source of truth for every family's `auto_detect_well_known`, `force_opaque_exceptions`, and `excluded_candidates` lists. The schema treats each family as a peer entry; no family is privileged at the policy layer:

- The roster is family-keyed under a top-level `families` object with one entry per family (`core`, `image`, `ttf`, `mixer`, `gfx`). Each entry carries the family's `library_version` (`2.32.10` for Core, `2.8.8` for Image, `2.24.0` for TTF, `2.8.1` for Mixer, `1.0.4` for GFX), `last_audited` date, and three name lists (`auto_detect_well_known`, `force_opaque_exceptions`, `excluded_candidates`).
- Audit method per family is **three-source triangulation**: family-pinned release headers (forward-decl evidence), wiki / project documentation pages (opacity phrasing where present), and ClangSharp Modern output (empty-struct emit). All three sources must agree before a name enters `auto_detect_well_known`; wiki evidence may be `not_found` when sources 1 and 3 agree (corroboration from create-function pages or header comments accepted where recorded).
- Constitution prose (this section) explains policy and criteria; the JSON file carries the enumerated names per family. Both surfaces must move together for any roster change.
- Drift between the rewriter's syntactic discovery and each family's roster section surfaces as a build-time **warning**, not a failure: each family's owner directory (`Janset.SDL2.{Core,Ttf,Mixer}/Generated/<Codegen>/`) is checked against its own roster section. Consumer directories (`Janset.SDL2.{Image,Gfx}/Generated/<Codegen>/`) skip drift reporting because they declare no local handle types. Satellite-owned handles such as `TTF_Font` and `Mix_Music` enter their family's `auto_detect_well_known` list and participate in the same drift-watchdog discipline as Core's 14 entries. **No asymmetry**: every family follows the same disciplines (audit triangulation, drift warning, roster surface).

**Cross-family force-opaque scope.** The Stage 1 force-opaque names enumerated in §"Structs And Unions" (`SDL_RWops`, `SDL_SysWMinfo`, `SDL_SysWMmsg`) live exclusively in the **Core** family's `force_opaque_exceptions` list. Satellite consumers reference them by-value via ProjectReference + nested-namespace resolution (`SDL2.Image` → `SDL2` resolves Core handle names unqualified); satellite `force_opaque_exceptions` lists start empty and are only populated if a satellite ships its own platform-dependent struct whose body is unsafe to expose (none do as of the audit dates above).

**Cross-family handle name resolution (rewriter input set).** A satellite's `uniform-opaque` postprocess pass must know which names are handle types — not just satellite-owned ones (`TTF_Font`, `Mix_Music`) but also Core-owned ones (`SDL_Renderer`, `SDL_Texture`, `SDL_RWops`, ...) that the satellite consumes by-value at its `[LibraryImport]` surface. The roster loader implements this with a single contract:

- **Core's** loader returns Core's own `auto_detect_well_known` ∪ Core's `force_opaque_exceptions`.
- **Each satellite's** loader returns the satellite's own `auto_detect_well_known` ∪ the satellite's `force_opaque_exceptions` ∪ **Core's `auto_detect_well_known`** ∪ **Core's `force_opaque_exceptions`**.

This preserves Pattern B's uniform by-value semantic at every raw ABI position regardless of which family owns the handle. The pull is **data-only**: satellite postprocess execution still does not read cross-family `.g.cs` files. **Drift watchdog stays per-family** — `ReportDrift` only compares syntactic discovery against the family's own `auto_detect_well_known` section, not the cross-family pull.

**Force-opaque allow-list delegation.** The three Stage 1 force-opaque names (`SDL_RWops`, `SDL_SysWMinfo`, `SDL_SysWMmsg`) and the rationale documented in §"Structs And Unions" (header body declared but unsafe to expose — function-pointer subclass state for `SDL_RWops`; platform-conditioned `#if defined(SDL_VIDEO_DRIVER_*)` union for the two `SDL_SysWM*`) remain authoritative as policy prose. The machine-readable enumeration of those same names lives at `families.core.force_opaque_exceptions` in the roster JSON. Changes to either prose or JSON must update both sides in the same commit.

**Cross-assembly Pattern B contract (`[assembly: DisableRuntimeMarshalling]`).** The modern `[LibraryImport]` source generator (net7+) emits SYSLIB1051 when a satellite assembly's P/Invoke surface uses a Pattern B handle struct by value that is defined in a *referenced* assembly. The struct is blittable by construction (`readonly partial struct X { nint Value; }` — single pointer-sized field, layout bit-identical to a raw pointer per Decision 1 above), but the source generator inspects cross-assembly types through metadata rather than source declarations and falls back to a conservative "user-defined struct requires runtime marshalling opt-in" path. The documented Microsoft resolution per the [P/Invoke source generator design](https://learn.microsoft.com/en-us/dotnet/standard/native-interop/pinvoke-source-generation) and the peer convention in [Alimer.Bindings.SDL](https://github.com/amerkoleci/Alimer.Bindings) is to apply `[assembly: DisableRuntimeMarshalling]` to every assembly that exposes `[LibraryImport]` declarations consuming Pattern B handles. The attribute (1) suppresses SYSLIB1051 for blittable cross-assembly Pattern B, (2) guarantees the whole assembly uses only blittable types at every P/Invoke position, and (3) is conditional on `NET7_0_OR_GREATER` because the attribute itself doesn't exist on legacy TFMs (and the legacy `[DllImport]` backend used by the Compat tree doesn't trip the source-gen diagnostic).

Janset.SDL2's P/Invoke surface is independently blittable-clean by design: strings emit as `byte*` (not `string`), booleans use the `SDL_bool` enum (not `bool`), and aggregate parameters are either pointers or Pattern B structs — there is no auto-marshalled type at any signature position. The attribute is opt-in formalization of an existing invariant, not a behavior shift. The single observable change is that adding a non-blittable type to any P/Invoke signature now fails at build time with a clear diagnostic, instead of silently triggering runtime marshalling. This is the desired posture for a Layer 1 raw ABI surface. The attribute lands in each generation-emitting assembly (`Janset.SDL2.Core`, `Janset.SDL2.Image`, and every future satellite that ships its own `[LibraryImport]` surface) under `Support/DisableRuntimeMarshalling.cs` with the `#if NET7_0_OR_GREATER` guard.

## Foreign Type Boundary Policy

SDL2 references types owned by **external native libraries** (Vulkan, Direct3D / DXGI, Microsoft GDK, Linux X11 / Wayland / KMSDRM, macOS Cocoa / UIKit / Metal, etc.) at parameter positions in its public API. Consumers obtain these from dedicated .NET bindings (`Silk.NET.Vulkan`, `Vortice.Windows`, `Silk.NET.OpenGL`, etc.). The Janset.SDL2 binding must let those external typed handles cross the SDL boundary without explicit-conversion friction.

**Why:** Wrapping foreign types in our own typed handle structs (Pattern B per §"Opaque Handles") forces users to write `new VkInstance(silkInstance.Handle)` at every cross-binding call site. SDL2-CS's pragmatic precedent — `IntPtr` / `nint` at foreign parameter positions — ships zero-friction interop with any .NET binding that exposes a pointer-sized handle. The §"Opaque Handles" typed-handle policy is binding for **SDL-owned** types only; foreign types are not ours to own, rename, or wrap. The active spike's research probes (2026-05-24) confirmed peer convergence on the IntPtr-at-foreign-boundary pattern: SDL2-CS uses `IntPtr` for `VkInstance` / `HWND` / `IDirect3DDevice9` / `JNIEnv*`; ppy/SDL3-CS keeps raw tag-pointers (counter-evidence but workable); Silk.NET / Vortice / TerraFX consumers all expose `.Handle` accessors returning `IntPtr` / `nint`. Distinguishing SDL-owned from foreign types is a manual policy decision — no automatic mechanism exists.

**How:** Foreign types at SDL parameter positions emit as opaque `nint` / `IntPtr`. The `[NativeTypeName("VkInstance")]` annotation (or equivalent for the toolchain) preserves provenance for documentation and downstream postprocess sensors. Mechanism varies by generator toolchain:

- **ClangSharp-style:** per-header RSP `--remap` with byte-exact textual match for the typedef / pointer spelling, plus `--exclude` to suppress the parser tag struct emission.
- **CppAst-style:** equivalent type-classifier rule plus a foreign-type allow-list policy (Cake's `ExternalNativeTypePolicy` precedent).

The allow-list lives in per-header configuration close to the header that references the foreign type, with comments citing the SDL header source line of the typedef and the upstream owner library (Vulkan / Win32 / GDK / etc.).

**What:** The Priority C survey identifies these foreign-type categories in SDL2 public API. The first three are active in the current spike scope; the rest are deferred until their corresponding `SDL_syswm.h` platform-view passes are activated.

| Category | Foreign types in SDL2 public API | Location | Disposition |
| --- | --- | --- | --- |
| Vulkan | `VkInstance` (= `VkInstance_T *`), `VkSurfaceKHR` (= `VkSurfaceKHR_T *` on 64-bit) | `SDL_vulkan.h:52-53,187-188` | **Active** — `rsp/per-header/SDL_vulkan.rsp` |
| Direct3D COM | `IDirect3DDevice9 *`, `ID3D11Device *`, `ID3D12Device *` | `SDL_system.h:77-127` (Windows pass) | **Active** — `rsp/per-header/SDL_system.rsp` (Windows-conditioned) |
| Microsoft GDK | `XTaskQueueHandle` (= `XTaskQueueObject *`), `XUserHandle` (= `XUser *`) | `SDL_system.h:599-630` (GDK pass) | **Active** — `rsp/per-header/SDL_system.rsp` (GDK-conditioned) |
| Win32 handles | `HWND`, `HDC`, `HINSTANCE` | `SDL_syswm.h:165,235-237` | **Already handled** — `rsp/sdl2-core.rsp:22-24` remap `HWND__* / HDC__* / HINSTANCE__* = nint` |
| Android JNI | `JNIEnv *`, `jobject` | `SDL_system.h:258-294` (pre-erased to `void*` by SDL) | **Already handled** — `rsp/base.rsp:18` `void*=nint` covers |
| C stdlib | `FILE *`, `va_list` | `SDL_rwops.h`, `SDL_log.h`, `SDL_stdinc.h` | **Already handled** — `rsp/sdl2-core.rsp:21,31-38` (FILE* remap + variadic-deferral excludes) |
| Linux X11 | `Display *`, `Window`, `XEvent *` | `SDL_syswm.h:173,249-250` | **Deferred** — `SDL_syswm.h` not yet in multi-OS pass |
| Linux Wayland | `wl_display *`, `wl_surface *`, `wl_egl_window *`, `xdg_*` | `SDL_syswm.h:295-302` | **Deferred** — same |
| Linux KMSDRM | `gbm_device *` | `SDL_syswm.h:342` | **Deferred** — same |
| macOS Cocoa | `NSWindow *` | `SDL_syswm.h:266-271` (Apple pass) | **Deferred** — Apple multi-OS pass not enabled |
| iOS UIKit | `UIWindow *`, `UIViewController *` | `SDL_syswm.h:280-289` (iOS pass) | **Deferred** — same |
| Metal | `void *` (already opaque upstream) | `SDL_render.h:1890-1911`, `SDL_metal.h` | **Already handled** — `void*=nint` covers; SDL deliberately exposes `CAMetalLayer*` / `MTLCommandEncoder` as `void*` |
| WinRT | `IInspectable *` | `SDL_syswm.h:243` (WinRT pass) | **Deferred** — gated behind `SDL_GetWindowWMInfo` which is currently excluded |
| OpenGL / EGL / GLES | `EGL*`, `GL*` (Khronos types) | `SDL_opengl*.h`, `SDL_egl.h` | **Not in scope** — entire header set excluded from parse via `scope/sdl2-core.headers.txt`. `SDL_GLContext` is SDL-owned (`typedef void *` in `SDL_video.h:221`), not foreign |
| DirectFB / Mir / Vivante / OS/2 | various | `SDL_syswm.h` | **Excluded** — Constitution L367 Stage 1 exclusions; never emitted |

Constitution L161-164's accepted deferrals (`SDL_RWFromFP`, `SDL_LogMessageV`, `SDL_vsnprintf`, `SDL_vsscanf`, `SDL_vasprintf`, `FILE`, `_IO_FILE`, `va_list`, `__va_list_tag`) cover the C variadic / file-pointer surface independently of this foreign-type allow-list.

When future slices activate additional platform passes (`SDL_syswm.h` Linux variant, Apple variant, WinRT variant), the corresponding deferred rows above transition to **active** and earn their own per-header RSP entries. The allow-list is grown deliberately; no auto-detection sweep silently expands it.

## Structs And Unions

Structs and unions are public only when the emitted layout is honest.

Rules:

- POD structs emit `[StructLayout(LayoutKind.Sequential)]` when every field is translated correctly.
- Unions emit `[StructLayout(LayoutKind.Explicit)]` with verified `FieldOffset` and size.
- Anonymous nested unions are translated from AST shape, not from name allowlists.
- Fixed primitive arrays use C# fixed buffers.
- Fixed arrays of non-fixed-buffer-compatible elements use deterministic generated wrapper structs, not `[InlineArray]`, so output remains safe across the supported TFM matrix.
- Platform-conditioned structs require per-platform size/offset proof or conservative layout that is correct for every emitted view.
- Do not under-size public struct storage to make compile-check pass.

`SDL_RWops` contract:

- `SDL_RWops` is public SDL header surface, but its `hidden` union is platform-conditioned.
- Stage 1 keeps `SDL_RWops` opaque/quarantined rather than exposing a false full layout. The quarantine emits as a typed handle struct following the public Opaque Handles shape above (`readonly partial struct SDL_RWops(nint value)`), referenced by value in raw ABI signatures rather than as `SDL_RWops*` pointer.
- Full typed layout requires later platform-specific size/offset proof.

`SDL_syswm.h` contract:

- Full typed `SDL_SysWMinfo` / `SDL_SysWMmsg` union layout remains Stage 2.
- Stage 1 may keep those declarations deferred or opaque as documented. Stage 1 quarantine emits both as typed handle structs following the public Opaque Handles shape, referenced by value in raw ABI signatures.

Function-pointer fields:

- Raw struct fields may use `nint` for function-pointer slots to preserve blittability across old TFMs.
- Separate typed callback declarations preserve callback identity.
- Friendly helper APIs may later bridge managed delegates to function pointers with explicit lifetime rules.

## Enums

Enum translation must preserve the native concept and managed ergonomics.

Rules:

- Use explicit C# underlying types.
- C enums default to `int` unless header evidence or existing strategy proves a different storage concept.
- SDL2 `SDL_bool` is int-backed.
- Keep alias/composed enum values when they are part of public SDL source compatibility.

`[Flags]` auto-decoration policy:

- A postprocess step adds `[Flags]` to an enum iff **(a)** the enum's name ends with the `Flags` suffix (case-sensitive — catches naming conventions across Core and satellites: `SDL_RendererFlags`, `IMG_InitFlags`, `MIX_InitFlags`, `TTF_FontStyleFlags`, etc.), **or (b)** the enum's name appears in the family-keyed allow-list within `family-config.json`. The allow-list follows the same family-keyed schema discipline as the opaque-handle roster, audited per SDL2/satellite release.
- **Heuristics over bit values alone are rejected.** Enums whose values happen to be powers of two are not automatically decorated — `SDL_bool` (`SDL_FALSE = 0`, `SDL_TRUE = 1`) would otherwise false-positive and break the int-backed bool contract above.
- Composed alias values (`KMOD_CTRL = KMOD_LCTRL | KMOD_RCTRL`) are preserved as enum members; the bitmask semantics flow from the allow-list entry, not from value analysis. The auto-decoration policy can decorate `SDL_Keymod` because the family's allow-list lists it, not because the postprocess parses the OR expression.

Known Stage 1 flag enums:

- **SDL2.Core** (decorated by `Flags` suffix): `SDL_MessageBoxFlags`, `SDL_MessageBoxButtonFlags`, `SDL_RendererFlags`, `SDL_WindowFlags`.
- **SDL2.Core** (decorated by allow-list match): `SDL_Keymod`, `SDL_BlendMode`, `SDL_GLcontextFlag`, `SDL_RendererFlip`, `SDL_TextureModulate`.
- **SDL2.Image** (decorated by `Flags` suffix): `IMG_InitFlags`.
- **SDL2.Mixer** (decorated by `Flags` suffix): `MIX_InitFlags`.
- **SDL2.Ttf**, **SDL2.Gfx**: no Stage 1 flag enums known at audit date.

## Constants And Macros

Macro translation is source-first and conservative.

Rules:

- Source-visible object-like `SDL_*` macros from public owned headers enter the macro pipeline.
- `binding_generation.required_constants` is a manual seed/include path, not the primary source for normal header macros.
- Literal numeric/string/character macros may emit when the managed type and value are deterministic.
- Deterministic object-like integer expressions may emit only when the evaluator can prove the value from safe syntax.
- Function-like public SDL macros are Layer 2 / friendly companion-helper candidates. They are not Layer 1 raw ABI declarations and must not be emitted as constants.
- Approved function-like macro helpers are manually authored or generated from an explicit companion-helper policy. ClangSharp does not emit them.
- Unknown function-like macros are reported and skipped. During the ClangSharp Layer 1 spike, the warning-only non-zero-exit classifier is temporary diagnostic glue: accepted macro names must stay visible in generation reports so the public-surface gap is reviewable rather than silently swallowed.
- C-only, build-time, compiler, include-guard, printf annotation, format, assertion, revision, cast-helper, and platform-control macros are skipped with explicit reasons.
- String-like SDL macro keys emit as canonical `ReadOnlySpan<byte>` UTF-8 literal properties. Do not duplicate every key as both `const string` and UTF-8 span.
- Runtime-sized or runtime-dependent macros must not emit as fake constants.

Current macro helper-candidate lane:

- `SDL_BUTTON`
- `SDL_VERSION`
- `SDL_VERSIONNUM`
- `SDL_VERSION_ATLEAST`
- `SDL_WINDOWPOS_*` helpers
- `SDL_DEFINE_PIXEL*` helpers
- `SDL_PIXELTYPE`, `SDL_PIXELORDER`, `SDL_PIXELLAYOUT`
- `SDL_BITSPERPIXEL`, `SDL_BYTESPERPIXEL`
- `SDL_ISPIXELFORMAT_*`

Version macro note:

- SDL2 `SDL_VERSIONNUM(X,Y,Z)` is `X * 1000 + Y * 100 + Z`.
- For SDL2 2.32.10, `SDL_COMPILEDVERSION` is `5210`, not `2032010`.

## Platform Views

Platform parsing uses controlled preprocessor views.

Rules:

- Platform views are parser metadata and file attribution, not public class identity.
- One family has one public class and one internal raw ABI class split across partial files.
- Preprocessor-macro switching is the Stage 1 approach; no parse-view-specific public classes.
- DirectFB, Vivante, MIR, and OS/2 remain Stage 1 exclusions unless explicitly reopened.
- Platform-specific functions receive platform attributes and are validated against the view that exposed them.
- Neutral functions are emitted once; platform views emit only platform-only functions after deduplication.
- Generation fails rather than guessing if the same function appears in multiple views with incompatible signatures.

Current SDL2.Core views:

- Neutral
- WindowsDesktop
- WinRT
- GDK
- Linux
- MacOS
- IOS
- Android

## Evidence Gates

No generated preview should be promoted toward production source unless these gates pass or have documented deferrals:

- Generated preview compiles across `net10.0`, `net9.0`, `net8.0`, `netstandard2.0`, and `net462`.
- No public raw ABI class or effectively public raw extern leak. Lexically public generated members inside an internal raw container are acceptable because the containing type blocks public API exposure.
- Function-name set matches dynapi/export evidence after accepted exclusions.
- High-risk type translations have fixture coverage: C `long`, `wchar_t`, SDL2 `SDL_bool`, `size_t`, callbacks, and pointer types.
- Public struct layouts have size/offset proof where layout is platform-conditioned.
- Macro report explains emitted, skipped, unsupported, helper-candidate, overridden, and stale-tolerant entries.
- Oracle validation records any accepted deltas and remaining blockers.
- Package-consumer smoke calls representative generated APIs against packaged natives before public release.

## Current SDL2.Core ABI Status

The 2026-05-19 P0 translation blockers were addressed at the policy level and proven feasible by the sunset Cake-hosted CppAst implementation under fixture-backed generator tests. Whichever toolchain the active spike selects, the policy resolutions below remain binding. The spike's ClangSharp + postprocess output currently re-proves raw ABI visibility, SDL.h required surface, dynapi coherence at ~98%, and the Priority C semantic-ABI closure: C `long` hybrid strategy, shared `wchar_t*` opaque, SDL_RWops / SDL_SysWMinfo / SDL_SysWMmsg as typed handle structs, tag/typedef canonicalization, and SDL_GUID substitution. The implementation lives under `spikes/binding-generators/`; the toolchain-neutral policy decisions are captured directly in this constitution via the WHY/HOW/WHAT subsections in §"C `long`", §"wchar_t", and §"Opaque Handles".

Resolved or intentionally quarantined categories:

1. Duplicate public opaque handles now canonicalize to public typedef names such as `SDL_hid_device` and `SDL_sem`.
2. C `long` / `unsigned long` raw command imports and callback delegates now use `CLong` / `CULong` and are emitted only for `NET6_0_OR_GREATER`; downlevel TFMs avoid false raw signatures.
3. `SDL_RWops` is Stage 1 opaque/quarantined rather than a public full-layout struct.
4. `wchar_t*` and CppAst-erased HID wide-string pointers map to opaque `nint` storage rather than false `int*` / `char*` signatures.
5. `SDL_WINAPI_FAMILY_PHONE` is classified as a platform-control macro and is not emitted as public API.
6. SDL2 `SDL_bool` is int-backed.
7. Known bitmask enums qualified by the `Flags` suffix or family allow-list are designated for `[Flags]` decoration per §"Enums" auto-decoration policy. Emission is delivered by Item 1's `flags-detect` postprocess step (see [`spikes/binding-generators/docs/items/item-1-per-library-generation-spec.md`](../../spikes/binding-generators/docs/items/item-1-per-library-generation-spec.md) §5.4 and the Roadmap §Item 1).

Variadic fmt-only imports are not a P0 ABI blocker when clearly documented and reported as mapped variadics, but they remain a policy/reporting cleanup item before production flip.

## Maintenance Rule

When a generator rule changes, update this constitution in the same change unless the change is purely mechanical. If code and this document disagree, inspect the code and pinned headers, fix the docs or code, and add tests for the disputed behavior.
