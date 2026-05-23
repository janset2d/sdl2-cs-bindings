# ClangSharp Semantic ABI Implementation Design

**Date:** 2026-05-23
**Status:** Accepted design; implementation plan pending.
**Scope:** Spike-local ClangSharp prototype under `spikes/binding-generators/clangsharp/`.

## Goal

Implement the C-track semantic ABI corrections for the ClangSharp spike while making the ownership boundary explicit: prefer ClangSharp/RSP/header-profile solutions, use generator-side semantic policy or synthetic generation when ClangSharp cannot express the required shape, and reserve Roslyn postprocess for syntax/backend cleanup or a narrow fallback bridge.

This track implements the research findings from [`docs/research/semantic-abi-type-classification-research.md`](../../research/semantic-abi-type-classification-research.md) in the ClangSharp spike. It does not replace the CppAst production generator decision in ADR-004. CppAst production code and the binding-generator constitution remain the policy baseline and comparison oracle.

C2 prototypes Stage-2-shaped SysWM layout proof inside the ClangSharp spike only. It does not change the production CppAst Stage 1 deferral or Stage 2 sequencing. Any production adoption of C2 mechanics requires a separate CppAst generator design/update under the binding-generator constitution and roadmap.

The desired end state is not just "the generated project builds." The generated ClangSharp output must stop presenting known ABI hazards as successful bindings, and the oracle must explain which hazards are fixed, intentionally deferred, or still blocked by ClangSharp capability limits.

## Context

The A-slice fixed effective raw ABI visibility by keeping generated raw containers internal. The B-slice recovered the SDL2 `SDL.h` required initialization surface through a spike-local allowlist and synthetic generation path.

The remaining C-track findings are semantic ABI classification problems:

- C `long` / `unsigned long` are not pointer-sized and must not become `nint` / `nuint` or platform-wrong `int` / `uint`.
- Shared `wchar_t*` is not portable as `ushort*`; Unix-like target platforms use 4-byte `wchar_t`.
- `SDL_RWops` must not expose a false full layout unless platform-conditioned layout proof exists.
- `SDL_SysWMinfo` and `SDL_SysWMmsg` need full platform layout work in this track, not a quiet deferral.
- Opaque handles must project canonical SDL typedef concepts, not parser private C tag names such as `SDL_semaphore` or `SDL_hid_device_`.
- Dynapi evidence is name coverage only and should distinguish total entries, unique names, duplicates, exclusions, deferrals, missing names, and extras.

The key design point is layer ownership. A semantic ABI fact should not be hidden as an accidental string rewrite in Roslyn. Roslyn can be used, but only when driven by explicit semantic markers or policy.

## Decision

Use a ClangSharp-first semantic bridge:

1. Research what ClangSharp can express through RSP options, remaps, type overrides, header shims, and small probe headers.
2. Record the result as a capability matrix before choosing fallback mechanics.
3. Put SDL semantic policy in the spike orchestration layer when ClangSharp cannot express it directly.
4. Use Roslyn postprocess only for backend syntax transforms, platform cleanup, or a tested marker-based fallback for opaque structs.
5. Make the oracle the final evidence layer for false-success ABI shapes.

The implementation should be incremental. Each slice must preserve known A/B fixes and leave the report more honest than before.

## Non-Goals

- Do not rewrite the production CppAst generator in this track.
- Do not change production `src/` generated output, package projects, build system, vcpkg config, CI, or manifest schema.
- Do not change the Phase 4 roadmap just because the ClangSharp spike explores Stage-2-shaped work.
- Do not expose public raw extern classes as the user-facing API.
- Do not turn Roslyn postprocess into a broad semantic repair layer.
- Do not emit full struct or union layouts without size/offset evidence.
- Do not introduce a casual downlevel `CLong` / `CULong` polyfill.
- Do not treat this spec as permission to commit; commits still require the repo approval gate.

## Layer Ownership

| Layer | Examples | Allowed C-track responsibilities |
| --- | --- | --- |
| ClangSharp input | `rsp/*.rsp`, header scope, platform header shims, parse defines | First place to solve type remaps, excludes, and platform parse shape. |
| ClangSharp orchestration | `generate_bindings.py`, synthetic files, output replacement | Holds spike-local semantic ABI catalog and explicit synthetic generation when ClangSharp cannot express honest output. |
| Roslyn postprocess | `strip-varargs`, `libraryimport`, `platform-delta`, possible marker pass | Syntax/backend cleanup or marker-driven fallback only. No blind semantic guessing. |
| Oracle/evidence | `oracle.cs`, evidence reports | Classifies fixed, false-success, deferred, excluded, duplicate, and residual hazards. |

The default rule is: if the decision requires native semantic knowledge, it belongs in the ClangSharp input/orchestration path or a named semantic catalog. Roslyn may apply the decision, but it should not silently invent the decision.

## Implementation Slices

| Slice | Goal | Preferred layer | Fallback layer | Acceptance summary |
| --- | --- | --- | --- | --- |
| C0 | Research ClangSharp/RSP/postprocess capabilities and add oracle taxonomy | ClangSharp probes + oracle | None | Capability matrix, mechanism table, and taxonomy explain what can be solved where. |
| C1 | Quarantine or opaque `SDL_RWops` honestly | ClangSharp exclude/remap | synthetic opaque file or marker pass | No false full `SDL_RWops` layout; references still compile. |
| C2a | Classify emitted SysWM platform views and current false-success output | oracle + SysWM catalog | None | Unproven SysWM views are explicit and no longer look proven by host-shaped output. |
| C2b | Add native SysWM size/alignment/offset probe infrastructure | native probe + report | None | Probe evidence can be produced for supported target runtime families. |
| C2c | Synthesize proven `SDL_SysWMinfo` / `SDL_SysWMmsg` layouts | generator-side platform layout synthesis | no real fallback beyond syntax cleanup | Layout has full size/alignment/offset proof for emitted supported views. |
| C2d | Reconsider `SDL_GetWindowWMInfo` | generator/oracle | keep excluded/deferred | Function is emitted only after `SDL_SysWMinfo*` layout is honest. |
| C3 | Correct C `long` / `unsigned long` ABI mapping | ClangSharp remap to `CLong` / `CULong` | targeted rewrite keyed by `NativeTypeName` and probe results | No shared C `long` ABI signature maps to platform-wrong managed types. |
| C4 | Correct shared `wchar_t*` ABI mapping | stronger ClangSharp remap | targeted pointer rewrite | Shared `wchar_t*` becomes opaque pointer shape; Windows-only Unicode APIs are separately classified. |
| C5 | Canonicalize opaque handles | ClangSharp canonicalization/remap | semantic policy map + synthetic handles or marker pass | Canonical SDL typedef names win over parser private tags. |
| C6 | Improve dynapi evidence | oracle/report | None | Report separates total occurrences, unique names, duplicates, missing, extra, exclusions, and deferrals. |

C2 must not block C3-C6. If C2a or C2b shows that full proof requires unavailable SDK/toolchain evidence, C2 may remain an explicit deferred finding while the scalar, wide-character, opaque-handle, and dynapi slices proceed. C6 may also be implemented immediately after C0, or partially inside C0, because evidence/reporting improvements help every later slice.

## C0 Capability Research

C0 is mandatory before implementation mechanics are locked. It should answer these questions with small, reproducible probes and current generated SDL output comparisons.

Known ClangSharp limitations should be named before probing:

- `--with-type` is enum-focused in ClangSharp documentation; do not expect it to solve C `long` / `unsigned long` until a probe proves otherwise.
- `--remap` may work for named declarations, typedef-like spellings, or exact pointer spelling cases, but it must be treated as unreliable until proven for builtins, `const`, typedef chains, fields, callbacks, and `T**`.
- `--with-transparent-struct` is not an opaque raw ABI policy by itself.
- `--with-attribute` is a plausible marker-injection candidate for known generated declarations and should be probed before native header annotations are treated as the main marker path.
- Generated native metadata attributes, such as `NativeTypeName`, are source-time evidence for postprocess/oracle only. They are not public API contract and must not be required at runtime.

| Question | Probe expectation | Possible outcome |
| --- | --- | --- |
| Can ClangSharp map C `long` / `unsigned long` to `CLong` / `CULong`? | Minimal header with params, returns, typedefs, callbacks, fields. | Use RSP if reliable; otherwise targeted semantic rewrite. |
| Why does current `wchar_t*=nint` not remove `ushort*` output? | Minimal header for `wchar_t*`, `const wchar_t*`, `wchar_t**`, `const wchar_t**`, fields, params, returns. | Strengthen remap or rewrite by source-time native type evidence. |
| Can incomplete/opaque structs be represented as opaque pointer scalars? | Header with `typedef struct Foo Foo; Foo*`, `Foo**`, fields, returns, delegates. | Prefer ClangSharp remap if pointer-depth safe; otherwise policy/rewrite. |
| Can a marker survive or be injected into ClangSharp output? | Test `--with-attribute`, `generate-cpp-attributes`, and `annotate`-style native metadata separately. | Use source-time marker if reliable; otherwise inject marker from policy catalog after generation. |
| Can Roslyn rewrite opaque pointer-depth safely? | Test `T*`, `T**`, `const T*`, fields, returns, parameters, delegates/function pointers. | Allow marker pass only if pointer-depth cases are covered. |
| Can SysWM be solved from ClangSharp platform output? | Compare `SDL_syswm.h` arms, generated output, and native size/offset probes. | Preserve ClangSharp output if proven; otherwise synthesize platform layout. |

C0 scalar and wide-character probes must run both `windows-types` and `unix-types`. The capability matrix must record whether any RSP fix is mode-dependent.

C0 output should update the oracle/reporting layer so later slices can prove that a finding disappeared for the right reason. It should not hide findings simply because they compile.

C0 exit artifact must include a per-hazard mechanism table with these columns:

| Column | Meaning |
| --- | --- |
| Hazard | The ABI issue, such as C `long`, `wchar_t*`, `SDL_RWops`, SysWM, or opaque handles. |
| Preferred mechanism tested | ClangSharp option, header shim, generator catalog, synthetic file, Roslyn marker pass, or deferral. |
| Result | What the probe proved or disproved. |
| Selected implementation layer | The layer C1-C6 must use unless later evidence invalidates it. |
| Fallback if selected layer fails | The next allowed path. |
| Required tests/evidence | Self-tests, generated build, native probe, oracle report, or postprocess tests. |
| Deferral condition | The exact condition under which the hazard remains reported instead of fake-fixed. |

C1-C6 may not choose a Roslyn fallback unless C0 records why ClangSharp input/orchestration cannot express the shape safely.

## Semantic ABI Catalog

The spike needs a small semantic catalog, initially close to `generate_bindings.py`, rather than a full production model layer. The catalog records facts that are already known from SDL headers and the production generator policy:

- opaque handles and their canonical public SDL typedef names;
- foreign opaque structs such as `FILE`-like concepts;
- concrete-but-quarantined layouts such as `SDL_RWops`;
- platform-conditioned layouts such as `SDL_SysWMinfo` and `SDL_SysWMmsg`;
- platform-sensitive scalar families such as C `long` and `unsigned long`;
- platform-sensitive wide-character pointers;
- accepted exclusions and deferrals.

This catalog is allowed to drive synthetic output or targeted rewrites. It must remain explicit and small. It should not become a second manifest language.

The ClangSharp semantic catalog is spike-local evidence/orchestration data. It is not a production policy source, not a replacement for `build/manifest.json`, and not a new public generator configuration format. Any durable rule discovered here must be promoted separately into the CppAst generator policy/constitution path.

## Opaque Struct Marker Fallback

If ClangSharp cannot produce honest opaque handle shape directly, a marker-based Roslyn pass is acceptable as a last-resort bridge.

The marker should be intermediate/internal, not public API:

```csharp
[NativeOpaqueType(canonicalName: "SDL_sem", nativeTagName: "SDL_semaphore")]
internal partial struct SDL_semaphore
{
}
```

The attribute is only a marker. It does not change P/Invoke or marshalling semantics. The tested pass must perform the actual shape work:

- rewrite raw ABI `SDL_semaphore*` to `nint`;
- rewrite raw ABI `SDL_semaphore**` to the selected opaque out-pointer shape, such as `nint*`, when proven;
- rewrite fields, parameters, returns, and delegate/function-pointer signatures consistently;
- emit or route the canonical public handle concept, such as `SDL_sem`, rather than leaking `SDL_semaphore`;
- avoid leaving the marker attribute in the final public API surface.

This fallback must be driven by the semantic ABI catalog. It must not scan for empty structs and guess.

Probe native annotation preservation, generated C++ attributes, and ClangSharp `--with-attribute` separately. If native annotation does not survive reliably, inject `NativeOpaqueType` from the semantic ABI catalog in a dedicated source-time pass after ClangSharp generation and platform cleanup, before pointer-shape rewriting and before `libraryimport`.

Verification must fail if:

- `NativeOpaqueTypeAttribute` appears in any final generated public API file;
- marker-bearing parser tag types are public;
- canonical handle output depends on the marker attribute at runtime;
- parser-private tag names such as `SDL_semaphore` or `SDL_hid_device_` appear as selected public handle concepts.

## `SDL_RWops` Design

`SDL_RWops` is public SDL header surface, but the current ClangSharp output emits a full layout whose `hidden` union is platform-conditioned. That is a false-success risk, especially because SDL_image, SDL_mixer, and SDL_ttf consume `SDL_RWops*` heavily.

C1 should first test whether ClangSharp can keep `SDL_RWops` opaque while preserving all pointer references. If it cannot, C1 should suppress or replace the false full layout and emit an honest opaque shape for raw ABI references.

Acceptance requirements:

- generated output must not expose a false full public `SDL_RWops` layout;
- generated output must not expose `SDL_RWops` public fields, nested `hidden` union types, or function-pointer field surface unless full layout proof is intentionally reopened;
- functions and structs that reference `SDL_RWops*` must still compile through an honest pointer representation;
- the selected opaque representation must be consistent across Core and satellites: either an internal empty marker pointer or `nint`, with pointer-depth tests for `SDL_RWops*`, `const SDL_RWops*`, `SDL_RWops**`, fields, callbacks, and function pointers;
- the oracle must stop reporting `SDL_RWops` as a false-success layout;
- if full layout remains deferred, the report must say that explicitly.

## `SDL_syswm` Design

C2 intentionally differs from the Stage 1 production deferral: this ClangSharp track should solve full `SDL_SysWMinfo` and `SDL_SysWMmsg` layout for the emitted supported platform views when proof is available. It is a spike evidence slice, not a production roadmap change.

The current ClangSharp spike does not route `SDL_syswm.h` through the platform-sensitive parse path, and the current generated SysWM file is host-shaped output. C2 must first add `SDL_syswm.h` to a platform-sensitive parse/evidence path or synthesize it from a semantic SysWM catalog. Neutral or Windows-host output is not acceptable SysWM evidence.

The implementation should not rely on Roslyn to patch random fields into ClangSharp output. It also must not rely on current `PlatformDeltaPostProcessor` duplicate pruning for SysWM: de-duping declarations by name would let one platform layout win and remove later platform layouts. SysWM layout output needs a dedicated synthesis step backed by evidence:

- enumerate the `SDL_syswm.h` arms exposed by the current ClangSharp platform profiles;
- decide which views are emitted by the spike and which are outside this repository's runtime RID scope;
- for repository target OS families, collect native `sizeof`, alignment, and key `offsetof` evidence;
- record `sizeof` and alignment for each nested arm struct and `offsetof` for every emitted arm field;
- for non-target SDL platform views, either obtain equivalent compile-time evidence with the correct SDK/toolchain or do not emit that view as a proven layout;
- generate explicit struct/union layout only for views with proof;
- emit explicit `StructLayout(..., Size = nativeSize[, Pack = provenPack])` where required, and verify every union arm `FieldOffset(0)`.

Required target runtime families are Windows desktop, Linux, and macOS. Other SDL platform arms that appear in headers, such as WinRT, UIKit, Android, KMSDRM, or dummy padding paths, must be classified in the report. They may be emitted only when C0/C2 can prove their layout under the relevant view.

SDL enum constants and SysWM layout arms have different proof requirements. The enum may preserve SDL source constants without layout proof. Struct/union arms are different: WinRT, UIKit/iOS, Android, DirectFB, Vivante, OS/2, MIR, Haiku, RISCOS, and KMSDRM arms must not be emitted into Windows desktop/Linux/macOS RID output unless their native layout was proven under that exact parse view and SDK/toolchain. The report must distinguish "enum value preserved" from "layout arm emitted and proven."

C2 must define a single ownership rule for `SDL_SysWMmsg`. `SDL_events.g.cs` may emit only a forward-compatible empty partial stub when no SysWM layout file is emitted, or the SysWM generator owns the complete partial type. Synthetic opaque structs must remain `partial` and must not conflict with the events forward declaration.

Acceptance requirements:

- `SDL_SysWMinfo` and `SDL_SysWMmsg` are no longer reported as unproven false-success layouts for emitted supported views;
- size/offset evidence is recorded in the oracle report or an adjacent generated evidence report;
- unsupported or unproven views are explicit, not silently generated as if proven;
- `SDL_GetWindowWMInfo` can be reconsidered only after the pointed-to layout is honest.

Windows desktop proof must cover `win-x86`, `win-x64`, and `win-arm64`, including `HWND`, `HDC`, `HINSTANCE`, `UINT`, `WPARAM`, and `LPARAM` field offsets and total struct sizes. Linux proof must cover `linux-x64` and `linux-arm64`; macOS proof must cover `osx-x64` and `osx-arm64`.

## C `long` / `unsigned long` Design

C3 should first prove whether ClangSharp can emit `System.Runtime.InteropServices.CLong` and `CULong` directly for C `long` and `unsigned long`. If that fails, use a targeted semantic rewrite keyed by native type evidence, not by managed type spelling alone.

Modern raw ABI should use `CLong` / `CULong` where available. `CLong` / `CULong` availability is a .NET 6+ concern; `LibraryImport` availability is a separate .NET 7+ backend concern. Do not conflate the two.

Downlevel `net462` and `netstandard2.0` should not receive fake portable signatures unless an exact per-platform strategy is designed and proven. Compat C `long` is not an automatic deferral: C3 must inventory return, parameter, field, callback/delegate, and typedef-alias occurrences, then prototype exact manual code generation when needed. Function returns/parameters may be solved with separate exact Windows and Unix private externs plus a non-extern wrapper following Microsoft's downlevel C `long` guidance. Struct fields, callbacks, delegates, and typedef aliases require separate proof because one RID-agnostic managed assembly may not be able to represent both LLP64 and LP64 layout truth with a single struct shape. A same-source portable `CLong` / `CULong` polyfill remains forbidden.

Acceptance requirements:

- known SDL stdinc functions such as `SDL_ltoa`, `SDL_ultoa`, `SDL_strtol`, `SDL_strtoul`, `SDL_lround`, and `SDL_lroundf` no longer map C `long` / `unsigned long` to platform-wrong managed types;
- any TFM guards, platform-specific wrappers, or manual generated Compat entrypoints are explicit;
- oracle evidence distinguishes fixed mappings from exact Compat strategy blockers.

## `wchar_t*` Design

C4 should correct shared `wchar_t*` output. Current generated output still contains `ushort*` for cross-platform SDL stdinc and HIDAPI surfaces even though `base.rsp` contains `wchar_t*=nint`. C0 must determine why that remap is insufficient.

Shared raw ABI should use opaque pointer representation. Windows-only Unicode APIs, such as WinRT-specific UTF-16 paths, may be classified separately because their platform contract differs.

Acceptance requirements:

- shared SDL stdinc and HIDAPI `wchar_t*` fields, returns, and parameters no longer emit as `ushort*`;
- Windows-only `const wchar_t*` APIs are not accidentally broken by the shared rule;
- oracle evidence reports platform-sensitive wide-character hazards as fixed or explicitly classified.

## Opaque Handles Design

C5 should stop leaking parser private tag names as canonical API concepts. Examples include `SDL_hid_device_` versus `SDL_hid_device`, and `SDL_semaphore` versus `SDL_sem`.

The preferred output split is:

- raw ABI uses internal, honest pointer scalar shapes for opaque native handles, such as `nint`, `void*`, or typed unsafe pointers according to the selected raw ABI policy;
- public typed layer, if generated in this track, uses canonical readonly `nint` handle structs;
- implicit conversion to `nint` is not introduced in this track unless separately approved.

Canonical readonly `nint` handle structs are public typed-layer values. They should cross into raw ABI through explicit unwrap/wrap code generated outside the extern declaration. Do not emit custom handle structs directly in `[DllImport]` / `[LibraryImport]` signatures unless native ABI probes prove the struct ABI is identical for parameters and returns on every supported RID.

The C-track may not need to build the full public typed API layer, but it must at least avoid making ClangSharp parser tags look like the selected public shape.

Acceptance requirements:

- canonical SDL typedef names are known in the semantic catalog;
- raw signatures avoid parser-tag pointer soup where the selected fallback is opaque `nint`;
- marker-based fallback, if used, covers pointer-depth and delegate/function-pointer cases with tests;
- final public output does not depend on internal marker attributes.

## Dynapi Evidence Design

C6 is report polish, not code generation. It should clarify dynapi/export evidence so count mismatches are actionable.

The report should distinguish:

- total dynapi/export entries;
- unique dynapi/export names;
- duplicate names;
- generated missing names;
- generated extra names;
- accepted exclusions;
- accepted deferrals.

Dynapi remains name evidence only. It does not prove parameter order, scalar width, struct layout, enum backing type, or ownership semantics.

## Data Flow

```text
SDL headers + RSP/platform profile
        -> ClangSharp generated raw files
        -> C0 capability checks
        -> semantic ABI catalog
        -> targeted synthetic generation or rewrite only where needed
        -> Compat + Modern generated output
        -> oracle evidence report
```

The Modern `LibraryImport` pass may continue to run as syntax/backend cleanup. The semantic ABI catalog must feed both Compat and Modern consistently so backend choice does not change native ABI truth.

Backend conversion must preserve SDL calling convention semantics. `LibraryImport` uses `[UnmanagedCallConv]`; `DllImport` must either use matching `CallingConvention` or rely on `[UnmanagedCallConv]` only when that is known to have the intended effect. Compat and Modern output must be compared for equivalent effective calling convention.

## Error Handling

- If ClangSharp cannot express a shape honestly, record the capability failure and move to the designed fallback.
- If a fallback cannot be tested for all relevant pointer-depth or layout cases, keep the declaration deferred or fail the slice.
- If SysWM layout proof is missing for an emitted view, do not emit a fake full layout for that view.
- If C `long` or `wchar_t*` mapping remains unsafe for a TFM/platform combination, report it explicitly.
- If an oracle finding disappears without a corresponding fix, treat that as a reporting regression.

## Testing And Evidence

Required evidence will vary by slice, but the C-track should use this stack:

- small capability probes for ClangSharp/RSP behavior;
- Python self-tests for `generate_bindings.py` policy and synthetic output;
- Roslyn/postprocess tests if a marker rewrite pass is introduced;
- oracle self-tests for taxonomy and report shape;
- generated project builds for Compat and Modern output;
- native layout or data-model probes for C `long`, `wchar_t`, `SDL_RWops`, and SysWM where relevant;
- `git diff --check`;
- Slopwatch after C# edits.

Every C-slice verification must preserve A/B regression gates:

- `raw-abi-public-class`: 0 findings for Core/Image;
- `raw-abi-public-import`: 0 effective public leaks for Core/Image;
- `family-namespace-drift`: 0 findings for Image;
- required SDL.h functions/constants: 0 missing for the B-slice surface.

C2 native layout evidence must include:

- pinned SDL header version and exact preprocessor defines used for each SysWM parse view;
- native `sizeof` and alignment for `SDL_version`, `SDL_SYSWM_TYPE`, `SDL_SysWMmsg`, and `SDL_SysWMinfo`;
- native `offsetof` for `version`, `subsystem`, `msg`, and `info`;
- native `sizeof`, alignment, and field offsets for every emitted `SDL_SysWMmsg.msg.*` arm;
- native `sizeof`, alignment, and field offsets for every emitted `SDL_SysWMinfo.info.*` arm;
- verification that `SDL_SysWMinfo.info` has 64-byte storage and generated total size matches native size;
- generated C# `StructLayout` evidence, including `Explicit`, `Sequential`, `Size`, optional proven `Pack`, and every `FieldOffset`;
- platform guard evidence for every emitted platform-specific file or declaration;
- report entries for every unproven or non-target arm;
- duplicate/merge report for `SDL_SysWMmsg` ownership between `SDL_events.g.cs` and `SDL_syswm.g.cs`;
- oracle check proving `SDL_GetWindowWMInfo` is emitted only after `SDL_SysWMinfo*` layout is honest.

Expected verification commands include:

```pwsh
python spikes/binding-generators/clangsharp/generate_bindings.py --self-test
dotnet run --file spikes/binding-generators/clangsharp/oracle.cs -- --self-test
dotnet run --file spikes/binding-generators/clangsharp/oracle.cs -- --family sdl2-core --family sdl2-image --write-report
dotnet build spikes/binding-generators/clangsharp/src/Janset.SDL2.Core/Janset.SDL2.Core.csproj -c Release
dotnet build spikes/binding-generators/clangsharp/src/Janset.SDL2.Image/Janset.SDL2.Image.csproj -c Release
git diff --check
slopwatch analyze --fail-on warning --exclude "artifacts/**,external/**,vcpkg_installed/**,spikes/binding-generators/references/**,**/bin/**,**/obj/**"
```

Native probe commands and exact generated report paths should be finalized in the implementation plan after C0 confirms the mechanics.

## Risks

- ClangSharp may not support enough semantic remapping for opaque handles. The marker fallback handles this, but it must be tested carefully.
- Roslyn pointer-depth rewriting can become fragile if it is not driven by a semantic catalog.
- Full SysWM layout may require platform SDK evidence beyond the current Windows-hosted development environment.
- Downlevel C `long` support can easily become a fake compatibility story. Exact manual Compat code generation must be researched before accepting a result; if a declaration category cannot be represented honestly, keep it as an explicit blocker rather than pretending.
- `wchar_t*` handling must not damage Windows-only UTF-16 APIs while fixing shared Unix-sensitive APIs.
- This spike could drift toward a second production generator. Keep it focused on evidence and C-track correctness.

## Approval Gate Before Implementation

After this spec is reviewed, the next step is an implementation plan using the `writing-plans` workflow. Implementation should not begin from this design alone.
