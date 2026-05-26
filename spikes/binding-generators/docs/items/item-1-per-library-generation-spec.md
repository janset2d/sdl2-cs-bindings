# Item 1 — Per-Library Generation Infrastructure (Spec)

**Date:** 2026-05-25
**Status:** Closed 2026-05-26 — durable design decisions and exit criteria for Roadmap Item 1. Approval gate per [`AGENTS.md`](../../../../AGENTS.md) §"Approval Gate" applies before any future code change.
**Branch:** `spike/binding-autogen-sdl2-gfx`
**Parent roadmap:** [`satellite-expansion-roadmap.md`](../satellite-expansion-roadmap.md) §Item 1.
**Successor plan:** [`item-1-per-library-generation-plan.md`](item-1-per-library-generation-plan.md) — ordered execution slices + exit evidence.

---

## 1. Goal

Make `generate_bindings.py` + the 7-step postprocess pipeline + `oracle.cs` per-family invocation deterministic and family-agnostic, so that Items 3–5 (GFX/TTF/Mixer) can plug in by adding data only (production header lists, RSPs, csprojs) rather than by editing pipeline logic.

The Item 1 deliverable is **infrastructure**, not new family output. Successful exit leaves Core + Image generation byte-identical (CRLF aside) to current HEAD except for the audited `[Flags]` additions required by §5.4, while:

- `FAMILY_CONFIG`, the `--family` CLI choice list, `PLATFORM_SENSITIVE_HEADERS`, the `stats` dict, and `write_report` all enumerate families dynamically.
- `OpaqueHandleEmitRewriter` discovers satellite-owned handles (`TTF_Font`, `Mix_Music`) without a name-prefix gate and emits `Handles.g.cs` into the family's namespace.
- `ThreadIdDualDispatchRewriter` is renamed `ClongDualDispatchRewriter`, expanded to handle parameter-position C `long` (TTF surface), and uses true Roslyn node-level mutation.
- A new 7th postprocess step adds `[Flags]` to bitmask enums via name-suffix detection plus a family-keyed allow-list (Item 2 then verifies on `IMG_InitFlags`).
- `oracle.cs FamilyConfigs` carries `Sdl2Ttf`, `Sdl2Mixer`, `Sdl2Gfx` entries; the family-parameterized `RawAbiChecks` engine ingests them without code changes.

GFX/TTF/Mixer remain **dormant in `--family all`** until their expansion items (Items 3/4/5) activate them.

---

## 2. Authority Order

Per the Constitution Authority Order — when sources disagree, use:

1. Pinned SDL public headers from `vcpkg_installed/x64-windows-hybrid/include/SDL2/`.
2. Generator implementation + tests in `spikes/binding-generators/clangsharp/`.
3. [`binding-generator-constitution.md`](../../../../docs/binding-autogen/binding-generator-constitution.md) for intended policy.
4. [`ADR-004`](../../../../docs/decisions/2026-05-14-binding-autogen-toolchain.md) for the recorded 2026-05-14 toolchain reasoning (Reopened — active comparison under this spike).
5. [`AGENTS.md`](../../../../AGENTS.md) for operating rules.
6. [`satellite-expansion-roadmap.md`](../satellite-expansion-roadmap.md) for sequencing.
7. This spec for Item 1-scoped decisions.
8. [`item-1-per-library-generation-plan.md`](item-1-per-library-generation-plan.md) for execution detail (this is the temporary per-slice surface, not policy).

This spec **does not reopen** settled decisions from §"Settled Strategic Decisions" in `AGENTS.md`, the Layer Contract in the Constitution, the Pattern B opaque-handle policy, the Priority C closure record, or the Foreign Type Boundary Policy. Item 1 is infrastructure for the existing contract.

---

## 3. Non-Goals

- **No Layer 2 typed public API or Layer 3 friendly overloads.** Both defer to Roadmap M5/M6.
- **No production source flip into `src/Janset.SDL2.*/`.** Roadmap M7.
- **No new `.generated-stamp` reproducibility metadata.** M7.
- **No new family **output** (TTF/Mixer/GFX `.g.cs` files).** Items 3/4/5. Item 1 only adds the data structures + pipeline hooks they will activate.
- **No 7-RID runtime ABI matrix expansion.** Production CI gate.
- **No SDL2_net.** Out of scope for this expansion wave.
- **No `compare_oracle.py` evolution.** The script retires (Cake-hosted CppAst comparison approach is sunset per `satellite-expansion-roadmap.md` §Cross-Cutting); `oracle.cs` is the active evidence reporter.
- **No companion `[Constant]`/`[Typedef]` regex feedback loop (ppy Slice 5 parity).** Roadmap §Slice 5 stays deferred.
- **No function-like public macro helper completion.** SDL helper macros such as `SDL_MIXER_VERSION(X)` / `SDL_MIXER_VERSION_ATLEAST(X,Y,Z)` are not Layer 1 raw ABI and are not constants. S1-2 only keeps ClangSharp warning-only exits diagnostic and report-visible; approved helper methods require a later explicit companion-helper policy or manual implementation.
- **No generated no-op file cleanup.** Some current Core outputs are intentionally zero-byte because ClangSharp exits 0 after finding no Layer 1 declarations, or because a platform view has no remaining declarations after neutral-symbol exclusion. S1-2 reports these as no-op generated outputs; missing outputs and non-accepted empty outputs remain failures.

---

## 4. Current-State Baseline (Evidence)

Verified against the source tree at branch `spike/binding-autogen-sdl2-gfx` HEAD `07e5a4b`.

### 4.1 Orchestrator (`generate_bindings.py`)

| Concern | Location | State |
|---|---|---|
| `FAMILY_CONFIG` | L327-344 | Two entries: `core`, `image`. |
| `selected_families("all")` | L776-779 | Hardcoded `["core", "image"]`. |
| `--family` CLI choices | L1054 | `["core", "image", "all"]`. |
| `PLATFORM_SENSITIVE_HEADERS` | L449-455 | `core: [SDL_main.h, SDL_system.h]`, `image: []`. |
| `stats` dict initialization | L1078-1081 | Hardcoded core+image keys. Per-family runs for unknown families would KeyError. |
| `write_report` family loop | L825 | Hardcoded `["core", "image"]`. |
| Owner-mode wiring | L1267 | `"owner" if family == "core" else "consumer"`. |
| RSP three-tier loader | `extend_rsp_arguments` L83-108 | Per-header lookup uses `<basename>.rsp`; precedence base → family → per-header. |
| Three-tier exit codes | `generation_exit_code` L787-798 | 2 = ClangSharp failure, 3 = postprocess failure, 4 = empty output, 0 = clean. |

### 4.2 Postprocess pipeline (`postprocess/`)

| Step | Mode key | Owner | Status for Item 1 |
|---|---|---|---|
| 1 | `platform-delta` | `PlatformDeltaPostProcessor` | Untouched. |
| 2 | `strip-varargs` | `StripVarargsRewriter` | Untouched. |
| 3 | `libraryimport` | `DllImportToLibraryImportRewriter` | Untouched. |
| 4 | `flags-detect` | `FlagsAttributeRewriter` | **New** — Item 1 deliverable. |
| 5 | `guid-substitute` | `GuidSubstitutionRewriter` | Untouched. |
| 6 | `clong-dispatch` | `ClongDualDispatchRewriter` | **Rename + refactor + extend.** |
| 7 | `uniform-opaque` | `OpaqueHandleEmitRewriter` + `UniformOpaqueOwnerMode` | **Drop SDL_ prefix gate; parameterize namespace.** |

### 4.3 OpaqueHandleEmitRewriter shape (pre-Item 1 baseline)

- Constructor accepts `HashSet<string> handleNames` (`OpaqueHandleEmitRewriter.cs:80-83`). The rewriter itself is already name-agnostic for the **rewrite** step — `_handleNames` drives `VisitStructDeclaration`, `VisitParameter`, `VisitMethodDeclaration`, `VisitFieldDeclaration`, `VisitFunctionPointerParameter` uniformly.
- The two gates that prevent satellite-owned handles today:
  1. **`DiscoverAutoDetectedHandles`** (L186-247) gates by `StartsWith("SDL_", StringComparison.Ordinal)` at L200, L211, L220, L230. `TTF_Font`/`Mix_Music` are not detected.
  2. **`BuildHandlesFileContent`** (L324-352) hardcodes `namespace SDL2` at L336. Satellite `Handles.g.cs` (TTF/Mixer in owner mode) would land in the wrong namespace.
- Baseline before Item 1 used a Core-only roster JSON. Item 1 S1-4 migrates `policy/opaque-handle-roster.json` to family-keyed schema 2.0: satellite-owned handles are explicitly rostered under their family sections, while Core force-opaque exceptions (`SDL_RWops`/`SDL_SysWMinfo`/`SDL_SysWMmsg`) remain under `families.core` and are pulled data-only by satellite postprocess runs.

### 4.4 ThreadIdDualDispatchRewriter shape (pre-Item 1 baseline)

- Inherits from `CSharpSyntaxRewriter` — Roslyn-based for **detection** (visits `MethodDeclarationSyntax`, reads `[return: NativeTypeName(...)]` via `AttributeListSyntax`).
- **Mutation is text-level**, not syntax-tree-level. `VisitMethodDeclaration` (L121-148) stages `(TextSpan, string)` pairs into `_pending` and returns the node unchanged; `VisitCompilationUnit` (L151-172) does StringBuilder `Remove`/`Insert` on the full source text, then re-parses with `CSharpSyntaxTree.ParseText`.
- Why the original choice was text-level: emitting **one method that becomes 1 dispatcher + 2 private DllImports** (Compat path) is awkward to return from `Visit<Foo>` (each visit returns a single node).
- Hardcoded surface:
  - `AffectedMethodNames` L76-80: `{ SDL_ThreadID, SDL_GetThreadID }`.
  - Sensor: only return-type native type names (L128-132). Parameter positions are not inspected.

### 4.5 Oracle (`oracle.cs`)

- `FamilyConfigs` (L160-183): two static `FamilyConfig` records — `Sdl2Core`, `Sdl2Image`.
- `OracleRunner.KnownFamilies` (L187-191): array containing the two records.
- `RawAbiChecks.Run(config, evidence, required)` (L1146+): **already family-parameterized**. Only `sdl2-core` gets the manifest required-surface check (L1164); every other check is generic. Adding families = data change to `FamilyConfigs` + `KnownFamilies` + (optionally) `BuildFamilyReport` constants for non-Core dynapi/cake-preview/sdl2-cs paths.

### 4.6 Reference satellite shape (`Janset.SDL2.Image/`)

```
Janset.SDL2.Image/
├── Janset.SDL2.Image.csproj          5 TFMs, EnableDefaultCompileItems=false,
│                                     RootNamespace=SDL2, ProjectReference→Core,
│                                     Compile Include split Compat/Modern by TFM,
│                                     System.Memory PackageReference for legacy
├── Generated/
│   ├── Compat/SDL_image.g.cs
│   └── Modern/SDL_image.g.cs
└── Support/
    ├── DisableRuntimeMarshalling.cs  [assembly:] under #if NET7_0_OR_GREATER
    └── NativeTypeNameAttribute.cs
```

This is the template every new family mirrors. Item 1 does **not** create new family projects (defers to Items 3/4/5); it only documents the template.

### 4.7 Cross-cutting status

- `spikes/binding-generators/scope/`: production per-family header lists (`sdl2-core.headers.txt`, `sdl2-image.headers.txt`, and future satellite lists). `sdl2-core-sdlh-required.json` exists and is consumed by execute-mode `--family core` generation for required-surface validation. **Do not retire** — it's load-bearing, not stale.
- `spikes/binding-generators/clangsharp/policy/`: roster JSON only. Per-family policy JSONs are **not** assumed needed; deep-dive of TTF/Mixer/GFX headers shows no force-opaque allow-list needed beyond Core's.
- `spikes/binding-generators/clangsharp/shims/platform-headers/`: 3 files (`endian.h`, `AvailabilityMacros.h`, `TargetConditionals.h`). Untouched.
- `spikes/binding-generators/clangsharp/compare_oracle.py`: **Retire.** Sunset per roadmap §Cross-Cutting; `oracle.cs` supersedes it.

---

## 5. Decisions

### 5.1 Dormant `all` activation set (matches roadmap Item 1 success criterion 5)

`selected_families("all")` stays `["core", "image"]` after Item 1 ships. New families are reachable per-family via `--family ttf|mixer|gfx` (CLI choices extended), but `all` does not regenerate them.

**Why:** Item 1's success gate is "no regression on existing Core + Image output." If `all` activated GFX/TTF/Mixer immediately, Item 1 verification would require their full Layer 1 to also pass — that work belongs to Items 3/4/5 and would inflate Item 1's blast radius.

**How:** The roadmap's success criterion #5 is binding: "GFX, TTF, Mixer FAMILY_CONFIG entries exist in `generate_bindings.py` but are NOT yet wired in `selected_families('all')` — they're dormant until their respective expansion items activate them."

**Activation contract:** Items 3/4/5 each include, as part of their slice, a single-line change to `selected_families("all")` adding their family identifier. That activation is gated by the same item's exit evidence (multi-TFM build clean, oracle clean) — not a separate slice.

### 5.1.1 CLI unit amendment — complete family artifact

`generate_bindings.py` operates at the complete family artifact level. The public generation controls are `--family`, `--execute`, `--vcpkg-triplet`, and `--use-platform-header-shims`; `Compat` and `Modern` are internal backends that always run together in production order. Execute mode cleans the selected family's `Generated/` root before generation; dry-run prints the compat + modern ClangSharp commands without deleting output.

Bootstrap/full header-subset selection is retired bring-up/debug surface. Each family has one production header list in `FAMILY_CONFIG[family]["headers"]`, and required SDL.h surface validation runs for execute-mode Core generation because there is no longer a bootstrap/full distinction.

### 5.2 Family-blind opaque auto-detection

`OpaqueHandleEmitRewriter.DiscoverAutoDetectedHandles` drops the `StartsWith("SDL_")` gate (4 occurrences: L200, L211, L220, L230). Detection becomes purely structural: a name is an auto-detect candidate iff (1) it appears as an empty `public partial struct X { }` declaration somewhere in the input tree **and** (2) it appears as `X*` at any raw ABI signature position (method parameter, method return, struct field, function-pointer parameter/return) in the same input tree.

`BuildHandlesFileContent` takes the family namespace as a parameter (currently hardcoded `"SDL2"` at L336). The caller (`UniformOpaqueOwnerMode.EmitConsolidatedHandlesFileIfOwner`) resolves the namespace from the orchestrator's per-family identity — passed in via a new CLI flag (`--handles-namespace SDL2.Ttf`) on the `uniform-opaque` postprocess invocation.

**Why:** Pattern B's invariant is structural (single `nint` field, ABI-equivalent to a pointer). The `SDL_` prefix gate was always a heuristic that happened to work because Core had the only opaque-handle surface. Satellite handles (`TTF_Font`, `Mix_Music`) carry no SDL prefix but satisfy the same structural test. Removing the prefix gate is a **policy alignment**, not a policy change — the Constitution §"Opaque Handles" Auto-detect criterion already describes the structural rule without referencing a prefix.

**How (mechanism):**
- `DiscoverAutoDetectedHandles(string inputDir)` returns the intersection of (empty-struct names) and (pointer-use names). Names are matched by `Identifier.ValueText` only; no prefix filter.
- The roster JSON (`policy/opaque-handle-roster.json`) **migrates from Core-only flat schema to family-keyed schema 2.0** under a top-level `families` object. Each family entry (`core`, `image`, `ttf`, `mixer`, `gfx`) carries its own `library_version`, `last_audited`, `auto_detect_well_known`, `force_opaque_exceptions`, and `excluded_candidates` lists. Satellite-owned handles (`TTF_Font`, `Mix_Music`) are listed explicitly in their family's `auto_detect_well_known` — same disciplinary surface as Core's 14 entries. **No asymmetry between Core and satellites.**
- Drift watchdog (`ReportDrift`) runs per-family. Each owner directory (`Janset.SDL2.{Core,Ttf,Mixer}/Generated/<Codegen>/`) is checked against its own family's roster section; divergence emits a build-time warning. Consumer directories (`Janset.SDL2.{Image,Gfx}/Generated/<Codegen>/`) skip drift reporting because they declare no local handle types (existing `syntacticDetect.Count == 0` short-circuit naturally covers this case). `LoadRoster` signature updates to take a `string family` parameter and load only that family's section.
- Cross-family `force_opaque_exceptions` (`SDL_RWops`, `SDL_SysWMinfo`, `SDL_SysWMmsg`) live exclusively in `families.core.force_opaque_exceptions`. Satellite consumers resolve them by-value via ProjectReference + nested namespace; satellite `force_opaque_exceptions` lists start empty.
- `BuildHandlesFileContent(IEnumerable<string> handleNamesSorted, string namespaceName)` signature change: replace the hardcoded `namespace SDL2` with `namespace {namespaceName}`. Existing callers pass `"SDL2"` explicitly.

**What:**
- Core `Handles.g.cs`: unchanged contents (still emits 17 handles into `namespace SDL2`).
- TTF `Handles.g.cs` (owner mode): emits `TTF_Font` (and any future satellite-owned handles discovered structurally) into `namespace SDL2.Ttf`.
- Mixer `Handles.g.cs` (owner mode): emits `Mix_Music` into `namespace SDL2.Mixer`.
- GFX (consumer mode, no satellite-owned handles): no `Handles.g.cs` written.
- Image (consumer mode, no satellite-owned handles): no `Handles.g.cs` written (current behavior).

**Cross-assembly contract:** Each owner-mode family csproj must carry `Support/DisableRuntimeMarshalling.cs` with the `[assembly: DisableRuntimeMarshalling]` attribute under `#if NET7_0_OR_GREATER`. Mirrors the existing Core + Image pattern. Constitution §"Opaque Handles" Implementation mechanism cross-assembly clause is binding.

**Peer divergence note.** ppy/SDL3-CS and Silk.NET 2.X keep satellite handles as empty `partial struct X` used via `X*` pointer (Sdl3-CS `SDL3_ttf-CS/SDL3_ttf/ClangSharp/SDL_ttf.g.cs:31` `public partial struct TTF_Font`; Silk.NET `src/Windowing/Silk.NET.SDL/Structs/Window.gen.cs` `public unsafe partial struct Window {}`); SDL2-CS uses `IntPtr` with `/* IntPtr refers to a TTF_Font* */` comments. **Zero peers** use typed Pattern B by-value handles for satellites. Janset's Pattern B is a deliberate divergence anchored in Constitution §"Opaque Handles" Decision 1 (ABI-equivalent typed handle + compile-time type safety) and Layer 2 typed-API ergonomics (M5 will project methods like `SDL_ttf.OpenFont(string)` returning typed `TTF_Font` with no caller-visible wrapping). The `--handles-namespace` CLI plumbing introduced in this section is uniquely needed because Janset uses nested namespaces (`SDL2.Ttf`, `SDL2.Mixer`); peers using a single flat namespace (`SDL`, `Silk.NET.SDL`) don't have this concern.

### 5.3 ClongDualDispatchRewriter — true Roslyn node-level mutation

Rename `threadid-dispatch` → `clong-dispatch` (postprocess mode key, file name, class name). Expand `AffectedMethodNames` to include the 5 TTF symbols (`TTF_OpenFontIndex`, `TTF_OpenFontIndexRW`, `TTF_OpenFontIndexDPI`, `TTF_OpenFontIndexDPIRW`, `TTF_FontFaces`) **as data only** — they stay dormant until Item 4 activates TTF generation. Add parameter-position rewrite support via **true Roslyn syntax mutation**.

**Why true syntax mutation now (changing the existing text-substitution approach):**

The current text-substitution approach has two costs:
1. **Brittle whitespace handling.** `ExtractLeadingIndentation` (L293-299) computes indentation from raw trivia; any change in upstream trivia shape breaks the rewriter silently. `StripParamTypes` (L228-237) implements an ad-hoc C param parser via string splits.
2. **Doesn't compose with parameter mutation.** The current path renders entire methods as strings. Adding parameter-position support to that path means rendering 4+ method variants per matched name with full `[LibraryImport]` / `[DllImport]` attribute blocks, parameter declarations, body blocks — each is a small string template that drifts independently from any upstream attribute shape change (e.g., a future `[UnmanagedCallConv]` tweak in `DllImportToLibraryImportRewriter`).

True Roslyn node-level mutation builds the replacement members via `SyntaxFactory` calls, which inherits Roslyn's normalization (indentation, attribute formatting, parameter list spacing) automatically and composes cleanly with future attribute-shape changes.

**How:**

Move the mutation point from `VisitMethodDeclaration` (which can only return one node) to `VisitClassDeclaration` (which mutates `ClassDeclarationSyntax.Members`):

```csharp
public override SyntaxNode? VisitClassDeclaration(ClassDeclarationSyntax node)
{
    var newMembers = new List<MemberDeclarationSyntax>();
    foreach (var member in node.Members)
    {
        if (member is MethodDeclarationSyntax method && IsAffectedSymbol(method))
        {
            newMembers.AddRange(_mode == Mode.Modern
                ? BuildModernMembers(method)            // returns 1 partial method
                : BuildCompatMembers(method));          // returns 1 dispatcher + 2 private DllImports
            AnyChanges = true;
        }
        else
        {
            newMembers.Add(member);
        }
    }
    return node.WithMembers(SyntaxFactory.List(newMembers));
}
```

`BuildModernMembers` and `BuildCompatMembers` construct `MethodDeclarationSyntax` nodes via `SyntaxFactory.MethodDeclaration(...)`, attaching attribute lists built via `SyntaxFactory.AttributeList(...)`. Parameter lists are built via `SyntaxFactory.ParameterList(...)` using the input method's parameters, with the C `long` parameter type rewritten:
- Modern: `long index` → `CLong index` (parameter type swap only).
- Compat: dispatcher keeps original `ulong/long index` (depending on signed vs unsigned), helper DllImports get per-RID parameter types (Win32: `uint`/`int`, Unix64: `nint`).

**Discriminating signed vs unsigned C `long`:** the sensor inspects the parameter's `[NativeTypeName("...")]` annotation. TTF index params are signed `long`; `SDL_threadID`/`SDL_ThreadID` returns are `unsigned long` (via the `SDL_threadID` typedef). The rewriter handles both by reading the native type name string.

**What lands in Item 1 vs Item 4:**
- **Item 1 ships:** the renamed mode key (`clong-dispatch`), the Roslyn-node-level mutation refactor, expanded `AffectedMethodNames` data, parameter-position rewrite logic. **Tested against Core's existing `SDL_ThreadID`/`SDL_GetThreadID` surface** — output must be byte-identical to the current text-substitution emit (modulo Roslyn's normalized formatting).
- **Item 4 activates:** TTF symbols enter the rewriter's affected set automatically because they're already in `AffectedMethodNames`. No code change in the rewriter itself.

**Risk:** Byte-identical output between text-substitution and Roslyn `SyntaxFactory` formatting is non-trivial. If Roslyn's default formatting produces a small whitespace delta (e.g., different blank-line behavior around attribute lists), it's acceptable as a one-time formatting churn — but must be a single regen-and-commit, not a moving target. The plan doc carries the exit evidence step that confirms byte-equivalence or records the formatting delta.

**Peer evidence.** alimer-bindings-sdl validates the Modern path: its Generated `Commands.cs` ships `public static partial CLong SDL_wcstol(char* str, char** endp, int @base)` and `public static partial CULong SDL_strtoul(byte* str, byte** endp, int @base)` — `CLong`/`CULong` emitted directly via Roslyn `SyntaxFactory` at the P/Invoke signature. alimer targets net8+ only and so doesn't carry a legacy TFM fallback; the `MapCLongToIntPtr` toggle in `CsCodeGeneratorOptions.cs:14` exists for downstream consumers who want the IntPtr coercion but is off by default. ppy/SDL3-CS and SDL2-CS both ship the **Constitution-rejected anti-pattern** (`[NativeTypeName("unsigned long")] uint value` in ppy's `SDL3-CS/SDL3/ClangSharp/SDL_stdinc.g.cs:349`; `int index` in SDL2-CS's `SDL2_ttf.cs:122`) — silently wrong on Linux LP64. The Compat path remains **uniquely Janset**: no peer targets `netstandard2.0`/`net462` while preserving C `long` semantic correctness. Microsoft's documented [`RuntimeInformation.IsOSPlatform` dispatch pattern](https://learn.microsoft.com/en-us/dotnet/standard/native-interop/best-practices) is the only precedent, already proven in Slice C-A's `ThreadIdDualDispatchRewriter`.

### 5.4 `[Flags]` detection — new 7th postprocess step

Add a 7th postprocess step `flags-detect` running a `FlagsAttributeRewriter` that walks `EnumDeclarationSyntax` and adds `[Flags]` when the enum's name matches one of two structural conditions. **The rewriter does not analyze enum values; it operates purely on names.**

**Detection rule (alimer-style — name-suffix + family-keyed allow-list):**

Add `[Flags]` to an enum iff **either** of:

- **(a) Suffix rule:** the enum's name ends with `Flags` (case-sensitive — captures naming conventions across families: `SDL_RendererFlags`, `IMG_InitFlags`, `MIX_InitFlags`, `TTF_FontStyleFlags`).
- **(b) Allow-list rule:** the enum's name appears in the family-keyed allow-list at `policy/flags-enum-roster.json` for the family whose output is being processed.

Already-`[Flags]`-decorated enums are passed through unchanged.

**Why this approach (power-of-two heuristic rejected):**

A power-of-two value heuristic was the initial proposal but is rejected for three reasons:

1. **Constitution friction.** Constitution §"Enums" L448 (pre-edit) explicitly says: "Do not invent `[Flags]` solely because values are powers of two if the SDL concept is not a bitmask." A value-pattern heuristic does exactly that — its safety relies on "audit confirms no false positives in this SDL2 release," which is fragile across upstream changes.
2. **`SDL_bool` false-positive risk.** `SDL_bool` declares `SDL_FALSE = 0` and `SDL_TRUE = 1`. Under a naive power-of-two check, both values qualify (`0` and `1` are both 0-or-power-of-two). A simple heuristic decorates `SDL_bool` as `[Flags]`, breaking Constitution §"Scalar Type Translation" `SDL_bool` int-backed contract. Strengthening with "≥2 distinct non-zero values" fixes this specific case but doesn't address future false-positive shapes.
3. **No peer validation.** Survey of ppy/SDL3-CS, SDL2-CS, alimer-bindings-sdl, Silk.NET 2.X shows: **only alimer auto-detects**, and alimer uses precisely this name-suffix + hardcoded allow-list mechanism (`CsCodeGenerator.Enum.cs:256-264`: `cppEnum.Name.EndsWith("Flags") || cppEnum.Name == "SDL_Keymod" || cppEnum.Name == "SDL_BlendMode" || ...`). ppy/SDL2-CS use manual companion-file annotations; Silk.NET 2.X drops `[Flags]` entirely. Power-of-two value matching is unique to no peer.

The name-suffix + roster approach is peer-validated, Constitution-safe (name convention = "API docs prove bitmask semantics" per the revised §"Enums" policy), and predictable: a new SDL release cannot silently produce `[Flags]` on an unaudited enum.

**`policy/flags-enum-roster.json` schema (Item 1 deliverable, S1-6):**

```json
{
  "schema_version": "2.0",
  "last_audited": "2026-05-25",
  "families": {
    "core": {
      "library_version": "2.32.10",
      "allow_list": [
        {"name": "SDL_Keymod", "header": "SDL_keycode.h",
         "reason": "Composite-alias bitmask (KMOD_CTRL = KMOD_LCTRL | KMOD_RCTRL); Constitution §\"Enums\" Stage 1 flag."},
        {"name": "SDL_BlendMode", "header": "SDL_blendmode.h",
         "reason": "Bitmask via SDL_BLENDMODE_* values; aligned with alimer hardcoded list."},
        {"name": "SDL_GLcontextFlag", "header": "SDL_video.h",
         "reason": "Pure power-of-two bitmask but caught by allow-list for predictability; Constitution Stage 1 flag."},
        {"name": "SDL_RendererFlip", "header": "SDL_render.h",
         "reason": "Bitwise composable flip flags; Constitution Stage 1 flag."},
        {"name": "SDL_TextureModulate", "header": "SDL_render.h",
         "reason": "Bitmask via SDL_TEXTUREMODULATE_NONE/COLOR/ALPHA — composable color+alpha modulation."}
      ]
    },
    "image": { "library_version": "2.8.8", "allow_list": [] },
    "ttf":   { "library_version": "2.24.0", "allow_list": [] },
    "mixer": { "library_version": "2.8.1", "allow_list": [] },
    "gfx":   { "library_version": "1.0.4", "allow_list": [] }
  }
}
```

**Symmetry with opaque-handle roster:** identical schema discipline — family-keyed under top-level `families`, per-family `library_version` + `last_audited`, per-family name list. No asymmetry between the two roster surfaces.

**What:**
- Item 1 ships the rewriter + the new pipeline step + the roster JSON.
- Item 2 verifies: regenerating Image produces `IMG_InitFlags` with `[Flags]` in both `Generated/Compat/SDL_image.g.cs` and `Generated/Modern/SDL_image.g.cs` via the suffix rule. No additional code change.
- Core regen gains `[Flags]` on every Core enum qualified by §5.4: `SDL_MessageBoxFlags`, `SDL_MessageBoxButtonFlags`, `SDL_RendererFlags`, and `SDL_WindowFlags` via suffix rule; `SDL_Keymod`, `SDL_BlendMode`, `SDL_GLcontextFlag`, `SDL_RendererFlip`, and `SDL_TextureModulate` via allow-list. `SDL_bool` does NOT gain `[Flags]` (neither suffix nor in allow-list). See §6 success criterion #1 carve-out.

**Pipeline order:** `flags-detect` runs after `libraryimport` and before `guid-substitute` — i.e., the new pipeline order becomes:

```
platform-delta → strip-varargs → libraryimport → flags-detect → guid-substitute → clong-dispatch → uniform-opaque
```

`flags-detect` is enum-only and doesn't compose with the others; ordering choice is for readability, not correctness.

### 5.5 Owner-mode resolution (no functional change — documented for clarity)

`generate_bindings.py:1267` already passes `--owner-mode owner|consumer` explicitly. The spec confirms:

```python
owner_mode = "owner" if family in ("core", "ttf", "mixer") else "consumer"
```

GFX and Image are consumer (no satellite-owned handles). TTF and Mixer become owners when their expansion items activate them. The substring-based fallback in `UniformOpaqueOwnerMode.IsOwnerDirectoryByPath` remains as a safety net only — explicit `--owner-mode` is the contract.

Each owner-mode family must additionally pass `--handles-namespace SDL2.<Family>` (new flag introduced in §5.2) so `BuildHandlesFileContent` writes the correct namespace into the family's `Handles.g.cs`.

### 5.6 Oracle multi-family extension (data-only)

`oracle.cs`:

```csharp
// FamilyConfigs (L160-183)
public static readonly FamilyConfig Sdl2Ttf = new(
    "sdl2-ttf",
    "SDL2 TTF",
    "SDL2.Ttf",
    "SDL_ttfNative",
    null,                                                            // no Cake preview
    "spikes/binding-generators/clangsharp/src/Janset.SDL2.Ttf/Generated/Compat",
    "spikes/binding-generators/clangsharp/src/Janset.SDL2.Ttf/Generated/Modern",
    "external/sdl2-cs/src/SDL2_ttf.cs",
    false);                                                          // no Sdl2Dynapi

public static readonly FamilyConfig Sdl2Mixer = new(
    "sdl2-mixer", "SDL2 Mixer", "SDL2.Mixer", "SDL_mixerNative", null,
    "spikes/binding-generators/clangsharp/src/Janset.SDL2.Mixer/Generated/Compat",
    "spikes/binding-generators/clangsharp/src/Janset.SDL2.Mixer/Generated/Modern",
    "external/sdl2-cs/src/SDL2_mixer.cs", false);

public static readonly FamilyConfig Sdl2Gfx = new(
    "sdl2-gfx", "SDL2 GFX", "SDL2.Gfx", "SDL2_gfxNative", null,
    "spikes/binding-generators/clangsharp/src/Janset.SDL2.Gfx/Generated/Compat",
    "spikes/binding-generators/clangsharp/src/Janset.SDL2.Gfx/Generated/Modern",
    "external/sdl2-cs/src/SDL2_gfx.cs", false);
```

`OracleRunner.KnownFamilies` (L187-191) extended to the 5-entry array.

**No new RawAbiChecks logic.** The engine is already family-parameterized; the only check that's Core-specific (`required-function-missing`/`required-constant-missing` at L1164) explicitly gates on `config.FamilyId.Equals("sdl2-core", ...)` and stays Core-only by design (manifest required surface is Core-only).

**Exit evidence per family:** for satellites whose generation directories don't yet exist (TTF/Mixer/GFX before Items 3/4/5 ship), `CSharpEvidenceLoader.LoadPath` returns `SourceStatus.Missing` rather than failing — the existing `LoadOptionalCSharpEvidence` path already handles this for `CakePreviewRelativePath` and `Sdl2CsRelativePath`. Item 1's oracle exit gate is "0 findings across Priority C categories **for sdl2-core + sdl2-image**" — same as current HEAD. TTF/Mixer/GFX generated rows appear in the report as missing/zero, while SDL2-CS rows load existing peer evidence where the source exists.

### 5.7 Per-family header lists/RSP/csproj/Support — defer creation to Items 3/4/5

Item 1 does **not** create:
- `scope/sdl2-ttf.headers.txt` (Item 4).
- `scope/sdl2-mixer.headers.txt` (Item 5).
- `scope/sdl2-gfx.headers.txt` (Item 3).
- `rsp/sdl2-ttf.rsp`, `rsp/sdl2-mixer.rsp`, `rsp/sdl2-gfx.rsp`.
- `src/Janset.SDL2.{Ttf,Mixer,Gfx}/Janset.SDL2.{Ttf,Mixer,Gfx}.csproj`.
- `src/Janset.SDL2.{Ttf,Mixer,Gfx}/Support/DisableRuntimeMarshalling.cs`.

**Why:** Item 1 is infrastructure for these families' arrival; the data files belong to the items that bring the families to life. Adding empty stubs invites drift between stub and final shape.

**Consequence:** Items 3/4/5 each carry the corresponding header list + RSP + csproj + Support creation as part of their slice. The Item 1 plan doc only ensures `FAMILY_CONFIG` entries reference paths that **will** exist by the time the corresponding expansion item activates `selected_families("all")`.

---

## 6. Success Criteria

| # | Criterion | Verification |
|---|---|---|
| 1 | `generate_bindings.py --family all --execute --vcpkg-triplet x64-windows-hybrid --use-platform-header-shims` produces Core + Image `.g.cs` output byte-identical (CRLF aside) to current HEAD **EXCEPT for `[Flags]` attribute additions on enums newly qualified by §5.4**. Core suffix-rule additions: `SDL_MessageBoxFlags`, `SDL_MessageBoxButtonFlags`, `SDL_RendererFlags`, `SDL_WindowFlags`. Core allow-list additions: `SDL_Keymod`, `SDL_BlendMode`, `SDL_GLcontextFlag`, `SDL_RendererFlip`, `SDL_TextureModulate`. Image suffix-rule addition: `IMG_InitFlags`. This is intentional Constitution §"Enums" alignment, not a regression. The exact `[Flags]`-addition delta is enumerated in the S1-6 slice commit. | `git diff --ignore-cr-at-eol` shows only `[Flags]` attribute additions on the enumerated set; no other content changes. |
| 2 | **Family-isolation determinism.** Three properties hold simultaneously: (a) `generate_bindings.py --family core --execute --vcpkg-triplet x64-windows-hybrid --use-platform-header-shims` and `generate_bindings.py --family image --execute --vcpkg-triplet x64-windows-hybrid --use-platform-header-shims` run sequentially produce identical output to `--family all`; (b) Image-only execute regenerates ONLY `spikes/binding-generators/clangsharp/src/Janset.SDL2.Image/Generated/` — Core's `Generated/` directory and `Handles.g.cs` remain byte-untouched (`git status` shows no Core changes after Image-only run); (c) symmetric guarantee for Core-only execute — Image's `Generated/` remains byte-untouched. | Sequential regen + `git diff --ignore-cr-at-eol` empty; per-family isolation check: `git status --short spikes/binding-generators/clangsharp/src/Janset.SDL2.{Core,Image}/Generated/` after each per-family run shows changes only in the targeted family's directory. |
| 3 | Multi-TFM build clean: `dotnet build spikes/binding-generators/clangsharp/Janset.SDL2.ClangSharpSpike.slnx -c Release` — 0 warnings / 0 errors across all 5 TFMs for Core + Image. | Build log. |
| 4 | `dotnet run --file spikes/binding-generators/clangsharp/oracle.cs -- --family sdl2-core --family sdl2-image --family sdl2-ttf --family sdl2-mixer --family sdl2-gfx --write-report` produces a 5-family evidence report; **0 findings** across Priority C categories for sdl2-core + sdl2-image (no regression). TTF/Mixer/GFX generated rows show `SourceStatus.Missing` with zero counts (expected — not yet generated); SDL2-CS rows load existing peer evidence where the source exists. | Report at `spikes/binding-generators/output/reports/oracle-evidence-clangsharp.md`. |
| 5 | `FAMILY_CONFIG` entries exist for `ttf`, `mixer`, `gfx` and the `--family` CLI accepts them. `selected_families("all")` returns `["core", "image"]` only. Running `python generate_bindings.py --family ttf` exits with `generation_exit_code` 2 or 4 (missing RSP/headers/project support) — not a Python KeyError or unhandled exception. | Self-test + manual smoke. |
| 6 | `OpaqueHandleEmitRewriter.DiscoverAutoDetectedHandles` returns `TTF_Font` and `Mix_Music` when run against synthetic test input containing empty `partial struct TTF_Font {}` + `TTF_Font*` use; returns the existing 14 SDL_* names when run against Core. | Postprocess self-test fixture. |
| 7 | `ClongDualDispatchRewriter` produces output for Core's `SDL_ThreadID`/`SDL_GetThreadID` byte-identical to current `ThreadIdDualDispatchRewriter` output (modulo Roslyn `SyntaxFactory` formatting normalization, which must be a single one-time churn committed alongside the refactor). Parameter-position rewrite is exercised by a synthetic test input with a `[NativeTypeName("long")] long index` parameter. | Postprocess self-test + Core regen diff. |
| 8 | `FlagsAttributeRewriter` adds `[Flags]` to `IMG_InitFlags` in both Generated/Compat/SDL_image.g.cs and Generated/Modern/SDL_image.g.cs after regen. (Item 2 success criterion #1 — Item 1 ships the rewriter; Item 2 ships the regen and the bug closure.) | Image regen diff after Item 1 + Item 2 land. |
| 9 | Slopwatch clean: `slopwatch analyze --fail-on warning --exclude "artifacts/**,external/**,vcpkg_installed/**,spikes/binding-generators/references/**,**/bin/**,**/obj/**"` reports 0 issues. | Slopwatch report. |
| 10 | `compare_oracle.py` removed; no active code/build/user-command references remain. Item 1 removal notes may mention the filename until the temporary execution plan retires after S1-7. | Grep returns 0 matches in code/build file types and no active README/prompt command references; remaining markdown matches are Item 1 removal notes only. |

---

## 7. Cross-Cutting Standardization

Item 1 lands these alongside the pipeline changes:

| Action | Why |
|---|---|
| Remove `spikes/binding-generators/clangsharp/compare_oracle.py`. | Sunset per roadmap §Cross-Cutting; `oracle.cs` is the active reporter. |
| **Keep** `spikes/binding-generators/scope/sdl2-core-sdlh-required.json`. | Load-bearing for `generate_bindings.py:554` required-surface validation. Roadmap's "if unused" note is incorrect — this spec corrects it. |
| Remove `comparison-report-template.md` if it exists and is unreferenced. | Sunset Cake-preview comparison artifacts alongside `compare_oracle.py`; keep only if a live caller is found during plan execution. |
| Confirm `policy/opaque-handle-roster.json` is family-keyed schema 2.0. | Satellite-owned handles are explicitly rostered under their family sections; Core force-opaque exceptions remain in `families.core` for data-only cross-family pull. |
| Confirm `shims/platform-headers/` is untouched. | No new platform views in Item 1. |

---

## 8. Assumption Resolutions (vs roadmap §Item 1 "Current Understanding")

| Roadmap assumption | Spec resolution |
|---|---|
| "no logic changes needed beyond data entries" for `FAMILY_CONFIG` | **False.** `stats` dict initialization (L1078-1081) and `write_report` family loop (L825) must move from hardcoded `["core", "image"]` to `selected`-driven enumeration. Small but real logic touches. |
| "no new include directories are needed" | **Confirmed.** All satellite headers live flat under `vcpkg_installed/<triplet>/include/SDL2/`. Per-family header analyses (TTF/Mixer/GFX) confirm. |
| "no cross-family postprocess coupling" | **Confirmed.** All 6 (now 7) postprocess steps operate on one family's `Generated/{Compat,Modern}/` tree. |
| "OpaqueHandleEmitRewriter auto-detection without prefix matching is sufficient" | **Confirmed.** Structural test (empty struct ∩ pointer-use) discovers `TTF_Font` and `Mix_Music` correctly per `ppy-reference-analysis-2026-05-25.md` §4. |
| "ClongDualDispatchRewriter — name-based matching is the right selection mechanism" | **Confirmed.** Name-based `AffectedMethodNames` is correct; the structural sensor (return + parameter native type names) protects against accidental matches. |
| "Compat dual-DllImport pattern works identically for parameter positions as it does for return positions" | **Conditionally confirmed.** Mechanism works; the spec calls for true Roslyn node-level mutation (vs current text substitution) so the parameter-rewrite path inherits Roslyn formatting normalization rather than re-implementing it via string templates. |
| "power-of-two detection is sufficient for SDL2's flag enums" | **Rejected.** Constitution §"Enums" is the authority: detection is name-suffix plus family-keyed allow-list only. Power-of-two value heuristics are intentionally avoided because `SDL_bool` can false-positive and peer evidence favors the alimer-style suffix/allow-list model. |

---

## 9. Risks

| Risk | Severity | Mitigation |
|---|---|---|
| Roslyn `SyntaxFactory` formatting differs from current text-substitution output → one-time formatting churn on Core regen | Low | Single regen-and-commit alongside the refactor; plan doc carries the diff inspection step. Acceptable churn because it's confined to one file pattern. |
| `[Flags]` roster misses a future bitmask enum that lacks the `Flags` suffix | Low | The rewriter is intentionally value-pattern-blind per Constitution §"Enums". Add audited enum names to the family-keyed allow-list when header/docs evidence proves bitmask semantics. |
| Satellite-owned handle in a future SDL2 release breaks structural auto-detect (e.g., struct gets fields added upstream) | Low | Drift report already exists for the Core roster; extend `ReportDrift` semantics to also warn when an owner-mode satellite directory produces zero auto-detected handles unexpectedly. Deferred to Items 4/5 as part of their exit gates. |
| `selected`-driven `stats` and `write_report` enumeration silently breaks parity with current hardcoded loops | Low | Success criterion #1 (byte-identical output) catches any behavior delta. |
| `compare_oracle.py` removal breaks a hidden caller | Low | `grep -r "compare_oracle"` confirms zero non-historical references before removal. |

---

## 10. References

### 10.1 Authority

- [`AGENTS.md`](../../../../AGENTS.md) — operating rules, approval gate, settled strategic decisions.
- [`docs/binding-autogen/binding-generator-constitution.md`](../../../../docs/binding-autogen/binding-generator-constitution.md) — canonical policy. Item 1 lands five Constitution edits in the same change set:
  - §"Opaque Handles" Auto-detect criterion (generalized from `SDL_X` to `X`, family-blind statement).
  - §"Opaque Handles" Canonical roster (migrated to family-keyed schema 2.0, drift watchdog per-family, **cross-family handle name resolution** rule for Pattern B by-value uniformity).
  - §"Enums" `[Flags]` auto-decoration policy (REWRITTEN — name-suffix + family-keyed allow-list; power-of-two heuristic explicitly rejected; `SDL_bool` false-positive case codified).
  - §"Current SDL2.Core ABI Status" point 7 (designation-vs-emission drift fix).
  - **NEW** §"Generation Determinism Contract" (the new section binding all determinism invariants: pin-set inputs, family isolation, dependency direction, native header resolution scope, pure-inputs discipline, per-slice verification contract). Item 1's success criteria are gated by this section.
- [`docs/decisions/2026-05-14-binding-autogen-toolchain.md`](../../../../docs/decisions/2026-05-14-binding-autogen-toolchain.md) (ADR-004, Reopened).

### 10.2 Parent docs

- [`satellite-expansion-roadmap.md`](../satellite-expansion-roadmap.md) — Item 1 master.
- [`priority-c-closure-summary.md`](../priority-c-closure-summary.md) — Layer 1 evidence baseline (Core + Image, 2026-05-24).
- [`next-iteration-plan.md`](../next-iteration-plan.md) — spike active plan + Review Follow-up Backlog.
- [`ppy-reference-analysis-2026-05-25.md`](../ppy-reference-analysis-2026-05-25.md) — ppy/SDL3-CS satellite architecture analysis.

### 10.3 Header analyses (per-family deep-dives, drive Items 3/4/5 but inform Item 1 success criteria)

- [`satellites/sdl2-image-cross-check.md`](../satellites/sdl2-image-cross-check.md) — `IMG_InitFlags` bug §5; cross-family `[Flags]` risk §7.
- [`satellites/sdl2-ttf-header-analysis.md`](../satellites/sdl2-ttf-header-analysis.md) — C `long` surface; satellite-owned `TTF_Font`; no-SDLCALL functions.
- [`satellites/sdl2-mixer-header-analysis.md`](../satellites/sdl2-mixer-header-analysis.md) — callback typedefs; satellite-owned `Mix_Music`; `MIX_InitFlags` `[Flags]` gap.
- [`satellites/sdl2-gfx-header-analysis.md`](../satellites/sdl2-gfx-header-analysis.md) — per-header export macros; mixed naming; no satellite-owned handles.

### 10.4 Code anchors

- `spikes/binding-generators/clangsharp/generate_bindings.py` — `FAMILY_CONFIG` L327-344, `selected_families` L776-779, `PLATFORM_SENSITIVE_HEADERS` L449-455, `stats` init L1078-1081, `write_report` loop L825, owner-mode wiring L1267, `extend_rsp_arguments` L83-108, `generation_exit_code` L787-798.
- `spikes/binding-generators/clangsharp/postprocess/Program.cs` — pipeline dispatch L49-160; `uniform-opaque` mode L126-157.
- `spikes/binding-generators/clangsharp/postprocess/OpaqueHandleEmitRewriter.cs` — `DiscoverAutoDetectedHandles` L186-247; `BuildHandlesFileContent` L324-352; `_handleNames`-driven rewrite L94-177.
- `spikes/binding-generators/clangsharp/postprocess/ClongDualDispatchRewriter.cs` — `AffectedMethodNames`; Roslyn node-level C `long` / `unsigned long` mutation for Core and dormant TTF surfaces.
- `spikes/binding-generators/clangsharp/postprocess/UniformOpaqueOwnerMode.cs` — owner-mode CLI resolution + `EmitConsolidatedHandlesFileIfOwner`.
- `spikes/binding-generators/clangsharp/oracle.cs` — `FamilyConfigs` L160-183; `OracleRunner.KnownFamilies` L187-191; `RawAbiChecks.Run` L1146+.
- `spikes/binding-generators/clangsharp/policy/opaque-handle-roster.json` — family-keyed schema 2.0 Pattern B auto-detect + force-opaque + excluded-candidates lists.
- `spikes/binding-generators/clangsharp/rsp/base.rsp` — cross-cutting remaps + SDL_bool=int.
- `spikes/binding-generators/clangsharp/src/Janset.SDL2.Image/` — reference satellite shape (csproj 5-TFM split, Generated/{Compat,Modern}/, Support/DisableRuntimeMarshalling.cs).

### 10.5 Knowledge base

- [`docs/knowledge-base/extraction-guidelines.md`](../../../../docs/knowledge-base/extraction-guidelines.md) — private-method / collaborator / interface decisions.
- [`docs/knowledge-base/testing-guidelines.md`](../../../../docs/knowledge-base/testing-guidelines.md) — TUnit + characterization test policy (applies to AbiTests when Items 3/4/5 add per-family runtime smoke).

---

## 11. Out of Scope (cross-reference roadmap §What This Roadmap Does NOT Cover)

- Layer 2 typed public API (Roadmap M5).
- Layer 3 friendly overloads (M6).
- Production source flip into `src/Janset.SDL2.*/` (M7).
- `.generated-stamp` reproducibility metadata (M7).
- 7-RID CI matrix for AbiTests (production CI gate).
- SDL2_net.
- Alimer-style CppAst comparison evidence (ADR-004 alternative path).
- Package smoke against real native packages (production gate).
- Companion file `[Constant]`/`[Typedef]` regex feedback loop (ppy Slice 5 parity — roadmap §Slice 5 deferred).
- `gendynapi` validation pass against `sdl.json` (ppy Slice 5 parity — deferred).
