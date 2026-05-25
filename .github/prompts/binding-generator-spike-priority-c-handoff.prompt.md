---
name: "Spike Handoff — Janset.SDL2 ClangSharp Binding Generator (Priority C closed)"
description: "Priming prompt for the next LLM/agent entering spikes/binding-generators/clangsharp/ after Priority C semantic-ABI completion landed at 09957a7 on 2026-05-24. Six known platform-sensitive ABI risks closed across three slices (C-A scalars, C-B Pattern B opaque handles, C-C handle canonicalization) plus Foreign Type Boundary Policy, BCL-Replaceable Helper Exclusion Policy, and Cross-Assembly Pattern B contract codified in the Constitution. 37 commits ahead of origin, push gate pending. Recommended next: Layer 2 typed low-level public API slice."
argument-hint: "Optional focus area: 'layer-2' | 'alimer-cppast-comparison' | 'ci-rid-matrix' | 'review-only' | custom"
agent: "agent"
model: "Claude Opus 4.7 (1M context)"
---

You are an engineer entering `janset2d/sdl2-cs-bindings` to continue work on the **ClangSharp + Roslyn postprocess binding-generator spike** at `spikes/binding-generators/clangsharp/`. The previous session closed **Priority C semantic-ABI completion** — all six known platform-sensitive ABI risks resolved, three cross-cutting policies codified in the Constitution, and the Layer 1 raw ABI surface is now stable enough to project Layer 2 onto. The branch `spike/binding-autogen-sdl2-gfx` is **37 commits ahead of origin**, working tree clean, push gate pending Deniz's explicit approval per AGENTS.md.

## First Principle

> Treat every claim here as **current-as-of-authoring (2026-05-24 — Priority C closure)** and verify against the live repo, `git log`, and canonical docs before acting. The closure summary doc at `spikes/binding-generators/docs/priority-c-closure-summary.md` is the authoritative record; this prompt cites it but does not duplicate its evidence tables.

## What Just Happened

### The toolchain context

ADR-004 (binding-autogen toolchain selection) was **Reopened** on 2026-05-23. The earlier Cake-hosted CppAst implementation under `build/_build/Targets/GenerateBindings/` is in **sunset**. The active prototype is the ClangSharp + Microsoft.CodeAnalysis (Roslyn) postprocess spike under `spikes/binding-generators/clangsharp/`. Constitution policy is **toolchain-neutral** and binds whichever implementation ships. An Alimer-style single-pass CppAst comparison evidence pass is a separate slice that must precede any ADR-004 amendment.

### Slice C-A — Scalar ABI risks (R1 wchar_t opaque + R2 C long hybrid)

**Why**: Two scalar widths drifted from native ABI on Linux/macOS:
- `wchar_t*` parameters were emitted as `ushort*` (Windows-shaped) — buffer corruption on Linux/macOS where `wchar_t` is 32-bit.
- C `long`/`unsigned long` was emitted as `int`/`uint` — truncated return on Linux/macOS LP64 (`unsigned long` is 64-bit).

**How**:
- **R1**: `rsp/base.rsp` adds `wchar_t *=nint` + `const wchar_t *=nint` remap (literal-space form, NOT shell-quoted — ClangSharp RSP parsing uses `System.CommandLine` which treats quoted text as part of the key). Caller treats wchar_t as opaque pointer; downstream consumers pick the right decoder per host platform.
- **R2**: Hybrid strategy resolved into two categories:
  - **Convenience helpers** (SDL_lround, SDL_lroundf, SDL_ltoa, SDL_ultoa, SDL_strtol, SDL_strtoul) excluded entirely via `rsp/per-header/SDL_stdinc.rsp` `--exclude`. BCL provides `Math.Round`, `long.Parse`, `ToString()`, `ulong.Parse` with better ergonomics. SDL2-CS (reference binding) doesn't expose these either.
  - **Structural symbols** (`SDL_threadID` typedef + `SDL_ThreadID`/`SDL_GetThreadID` functions) handled via `ThreadIdDualDispatchRewriter` (Roslyn postprocess). Mode-aware emit:
    - **Modern TFM** (net8+): `[LibraryImport("SDL2")]` + `CULong` return + `[UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]`.
    - **Compat TFM** (netstandard2.0/net462): managed `ulong` wrapper that dispatches via `RuntimeInformation.IsOSPlatform(OSPlatform.Windows)` between two private `[DllImport]` (`uint` for Win32, `nint` for Unix64 LP64).
  - The csproj file-routes `Generated/Compat/**/*.cs` to legacy TFMs and `Generated/Modern/**/*.cs` to net8+ via conditional `<Compile Include>` — so the rewriter emits ONE branch per mode (no `#if` directives in generated code).

**Decision elevations to Constitution policy**: §"BCL-Replaceable Helper Exclusion Policy" (new section between Function Surface and C Variadics). Rule: a symbol is excluded when (1) BCL equivalent has equal-or-better ergonomics, (2) SDL2-CS skips it, (3) no SDL2 symbol transitively depends on it. Same policy bound `SDL_iconv_*` family later in Slice C-B drift resolution.

**Exit gate**: `45fdab6`-prior commits (Slice C-A landed at `adb64d0`). Oracle `platform-sensitive-wchar` + `platform-sensitive-long`: 0 findings.

### Slice C-B — Pattern B uniform opaque handle struct emit (R3-R5 force-opaque + Pattern B 3-position)

**Why**: Three SDL2 structs have full header bodies but platform-conditioned layouts that are unsafe to expose:
- `SDL_RWops` — function-pointer subclass state (FileRWops, MemoryRWops, ...).
- `SDL_SysWMinfo` — platform-conditioned union over X11/Wayland/Cocoa/Win32/etc.
- `SDL_SysWMmsg` — same platform-conditioned union.

Plus the broader pattern: SDL2 has **17 opaque handle types** (`SDL_Window`, `SDL_Renderer`, `SDL_Texture`, ...) that ClangSharp emits as empty `partial struct X { }`. Raw pointer usage (`SDL_Window*` parameter/return) leaks `unsafe` context to callers.

**How**:
- **Pattern B shape** per Constitution §"Opaque Handles":
  ```csharp
  [StructLayout(LayoutKind.Sequential)]
  public readonly partial struct SDL_Window : IEquatable<SDL_Window>
  {
      public SDL_Window(nint value) { Value = value; }
      public nint Value { get; }
      public bool IsNull => Value == 0;
      public bool IsNotNull => Value != 0;
      public static SDL_Window Null => default;
      public nint DangerousGetHandle() => Value;
      // Equals, GetHashCode, ==, !=, explicit operator nint, explicit operator SDL_Window
  }
  ```
  Single `nint` field → struct size = pointer size → ABI bit-identical to a raw pointer.
  **Explicit-only operators** (no implicit `nint → X` or `X → nint`) — research Finding 3 binding; deliberate-escape uses `DangerousGetHandle()`.

- **`OpaqueHandleEmitRewriter`** (`spikes/binding-generators/clangsharp/postprocess/OpaqueHandleEmitRewriter.cs`) two-phase architecture:
  1. **Discovery pass**: walks every `.g.cs`, identifies partial struct declarations matching the roster-loaded handle name set.
  2. **Emit pass**:
     - REMOVES matched partial struct declarations via `RemoveNode`
     - APPLIES single-pointer `SDL_X*` → `SDL_X` by-value rewrite at THREE positions: method parameter, method return, **struct field** (`VisitParameter`, `VisitMethodDeclaration`, `VisitFieldDeclaration`). Double-pointer `SDL_X**` and `out SDL_X` positions preserved by structural match.
     - In **owner mode** (Janset.SDL2.Core), Program.cs writes a new `Handles.g.cs` with ONE Pattern B struct per handle (alphabetically sorted). In **consumer mode** (Janset.SDL2.Image), no Handles.g.cs — Image references Core via ProjectReference + shared `namespace SDL2`.
     - Owner-mode selection via explicit `--owner-mode {owner|consumer}` CLI flag from the orchestrator (NOT substring matching — pre-push refactor `09957a7`).

- **Roster JSON** (`spikes/binding-generators/clangsharp/policy/opaque-handle-roster.json`) is the **canonical policy authority**:
  - `auto_detect_well_known`: 14 handles, three-source triangulated (SDL2 headers + wiki.libsdl.org + ClangSharp Modern empty-struct emit). Each entry has `name`, `header`, `wiki_url`, `wiki_evidence` fields.
  - `force_opaque_exceptions`: 3 entries (SDL_RWops, SDL_SysWMinfo, SDL_SysWMmsg) with `name`, `header`, `reason`, `wiki_url`.
  - `excluded_candidates`: 11 documented exclusions (pointer typedefs like SDL_GLContext, value-type aliases like SDL_TimerID, BCL-replaceable SDL_iconv_t, field-scope-only SDL_BlitMap).
  - `sdl2_version`-keyed (`2.32.10`). Re-audited per upstream SDL2 release.
  - Rewriter cross-checks syntactic discovery against the roster; drift fires a **build-time warning** (not failure) so upstream SDL2 additions get visibility without forcing immediate Constitution patches.

- **Cross-assembly Pattern B contract** (`[assembly: DisableRuntimeMarshalling]`, NET7_0_OR_GREATER gated). Applied to both `Janset.SDL2.Core` and `Janset.SDL2.Image` via `Support/DisableRuntimeMarshalling.cs`. Resolves SYSLIB1051 source-generator conservative path on net8+ when satellite assembly P/Invoke uses Pattern B by value from a referenced assembly. Codified as Constitution §"Opaque Handles" Implementation mechanism sub-clause. Peer-validated by Alimer.Bindings.SDL.

**Decision elevations to Constitution policy**: §"Opaque Handles" Implementation mechanism (extended for Pattern B 3-position rewrite + roster JSON + cross-assembly DisableRuntimeMarshalling); §"Foreign Type Boundary Policy" (Vulkan/D3D/GDK → nint at raw ABI parameter positions for zero-friction interop with Silk.NET/Vortice/etc.; X11/Wayland/Apple/WinRT deferred until SDL_syswm.h platform pass).

**Critical learning surfaced mid-task**:
1. **ClangSharp `[NativeTypeName("X *")]` annotation is NOT emitted when C and C# names match** — SDL2's `typedef struct SDL_Window SDL_Window;` pattern (same-name tag+typedef) is the common case for opaque handles. The plan's original cross-reference auto-detect logic under-detected (1 found vs 12 expected). Resolution: switched to **syntactic discovery** (empty SDL_* struct + pointer use at method param/return positions).
2. **ClangSharp emits forward declarations of cross-referenced struct types in multiple .g.cs files** (e.g., `SDL_SysWMmsg` in both `SDL_events.g.cs` and `SDL_syswm.g.cs`). The first attempt applied Pattern B body to ALL partials → 150+ C# partial-struct member collision errors. Resolution: **consolidated `Handles.g.cs` canonical home** pattern (TerraFX-precedent — `Handles.cs` as single source). All partial struct declarations of handle names removed from their original .g.cs; Pattern B emitted once in `Handles.g.cs`.
3. **SDL_iconv_t drift discovery** during apply: ClangSharp emits empty struct + uses `SDL_iconv_t*` at signature positions, but the C type `typedef struct _SDL_iconv_t *SDL_iconv_t` is structurally a pointer-typedef. Triangulated decision: **exclude the entire SDL_iconv_* family** via `SDL_stdinc.rsp` (System.Text.Encoding is BCL equivalent; SDL2-CS + Silk.NET skip the family).
4. **SDL_BlitMap drift discovery**: header tags it `/* this is an opaque type. */` and SDL3 removed it entirely (`void* reserved`). Resolution: exclude struct + remap field type to `nint` via new `SDL_surface.rsp`. SDL_Surface.map field becomes `nint`.

**Exit gate**: `45fdab6`. Oracle `deferred-layout-sdl-rwops` + `deferred-layout-sdl-syswminfo` + `deferred-layout-sdl-syswmmsg`: 0 findings. Hard Bug section REMOVED from oracle report entirely. 17/17 handle inventory check passes.

### Slice C-C — Handle canonicalization + SDL_GUID substitute (R6 + ad-hoc)

**Why**: ClangSharp leaks SDL2's internal tag names (`SDL_hid_device_` with trailing underscore, `SDL_semaphore` struct tag aliased to `SDL_sem` typedef). API-shape noise, not ABI failure, but oracle's duplicate-tag-typedef detector flagged 10 findings.

**How**:
- **Per-header RSP organization** (ppy/SDL3-CS pattern): `rsp/base.rsp` + `rsp/<family>.rsp` + `rsp/per-header/<header>.rsp`. The orchestrator auto-discovers per-header RSPs via `extend_rsp_arguments(...)` helper.
- **R6 canonicalizations** (one per affected header):
  - `SDL_hidapi.rsp`: `--remap SDL_hid_device_=SDL_hid_device`
  - `SDL_mutex.rsp`: `--remap SDL_semaphore=SDL_sem`
  - Cross-header repeats for `_SDL_Joystick` in `SDL_haptic.rsp` + `SDL_gamecontroller.rsp` (ClangSharp `--remap` is per-header byte-exact)
- **SDL_GUID substitute** via `GuidSubstitutionRewriter` (Roslyn postprocess): replaces `SDL_GUID` struct emit with `System.Guid` type alias. Wires using directive via `VisitCompilationUnit` (canonical pattern; see `DllImportToLibraryImportRewriter`).
- **Foreign Type Boundary Policy** (Slice C-C extension; commit `cfe4937`): explicit allow-list at `SDL_vulkan.rsp` (VkInstance/VkSurfaceKHR → nint) + `SDL_system.rsp` (D3D9/11/12, XTaskQueueObject, XUser → nint). Peer-validated against SDL2-CS / Silk.NET / Vortice consumer pattern.

**Exit gate**: Slice C-C closed mid-session (commit `247aac4` predecessor + R6 work). Oracle `duplicate-tag-typedef-*`: 0 findings.

### Cross-cutting: orchestrator pipeline + AbiTests

- **Six-step postprocess pipeline** in `generate_bindings.py`: `platform-delta` → `strip-varargs` → `libraryimport` (Modern only) → `guid-substitute` → `threadid-dispatch` → `uniform-opaque`. Single command produces deterministic Layer 1 raw ABI surface.
- **AbiTests project** (`spikes/binding-generators/clangsharp/tests/abi-tests/`): per-TFM ABI runtime smoke. `InternalsVisibleTo` bridge from Core exposes `SDLNative` to test assembly. Calls `SDLNative.SDL_ThreadID()` and asserts non-zero. Native SDL2 lib OS-dispatched in csproj (Windows: `vcpkg_installed/x64-windows-hybrid/bin/SDL2.dll`; Linux: `vcpkg_installed/x64-linux-hybrid/lib/libSDL2-2.0.so.0`).
- **Linux x64 runtime evidence** via docker command override on the existing `janset-binding-generator:focal-latest` image (built from `docker/binding-generator.Dockerfile`, derives from `ghcr.io/janset2d/sdl2-bindings-linux-builder:focal-latest`). Cross-OS bind-mount of Windows-host repo forbidden — image bakes repo via COPY at Layer F.

## Onboarding Snapshot

### Architecture map

```
spikes/binding-generators/clangsharp/
├── generate_bindings.py             # Python orchestrator; 6-step postprocess pipeline
├── oracle.cs                        # Roslyn-based raw ABI evidence reporter
├── rsp/
│   ├── base.rsp                     # Cross-cutting: --remap wchar_t* → nint, --define-macro, --additional
│   ├── sdl2-core.rsp                # Family identity + family-wide excludes
│   ├── sdl2-image.rsp               # Family identity + family-wide excludes
│   └── per-header/
│       ├── SDL_hidapi.rsp           # R6: SDL_hid_device_ → SDL_hid_device
│       ├── SDL_mutex.rsp            # R6: SDL_semaphore → SDL_sem
│       ├── SDL_stdinc.rsp           # BCL exclusion: SDL_lround/SDL_strtol/SDL_iconv_*
│       ├── SDL_surface.rsp          # SDL_BlitMap exclude + map field → nint
│       ├── SDL_vulkan.rsp           # Foreign type: VkInstance/VkSurfaceKHR → nint
│       ├── SDL_system.rsp           # Foreign type: D3D9/11/12, XTaskQueue → nint
│       └── ... (others — see directory listing)
├── policy/
│   └── opaque-handle-roster.json    # Canonical Pattern B handle policy (14+3+11)
├── postprocess/
│   ├── Program.cs                   # CLI dispatch: 6 modes + uniform-opaque flag parsing
│   ├── OpaqueHandleEmitRewriter.cs  # Two-phase: discovery + emit; Pattern B + pointer rewrites
│   ├── ThreadIdDualDispatchRewriter.cs  # Mode-aware Compat/Modern emit
│   ├── DllImportToLibraryImportRewriter.cs  # Mode-aware [LibraryImport] (Modern only)
│   ├── GuidSubstitutionRewriter.cs  # SDL_GUID → System.Guid
│   ├── PlatformDeltaPostProcessor.cs  # Multi-OS platform-conditional emit
│   ├── StripVarargsRewriter.cs      # __arglist removal
│   ├── UniformOpaqueOwnerMode.cs    # --owner-mode flag parsing + Handles.g.cs writer
│   └── Janset.SDL2.PostProcess.csproj
├── shims/platform-headers/          # Windows-local synthetic platform parse shims
├── tests/abi-tests/
│   ├── AbiTests.csproj              # TUnit; net10/9/8/462; SDL2 native OS-dispatched
│   └── ThreadIdAbiTests.cs          # SDL_ThreadID non-zero assertion
└── src/
    ├── Janset.SDL2.Core/            # 5 TFMs; InternalsVisibleTo AbiTests; DisableRuntimeMarshalling
    │   ├── Janset.SDL2.Core.csproj
    │   ├── Support/DisableRuntimeMarshalling.cs    # NET7_0_OR_GREATER gated
    │   └── Generated/
    │       ├── Compat/              # netstandard2.0 + net462 routed here
    │       │   ├── Handles.g.cs     # 17 Pattern B structs (owner mode canonical)
    │       │   └── ... (per-header .g.cs minus handle struct decls)
    │       └── Modern/              # net8+/net9+/net10+ routed here
    │           ├── Handles.g.cs     # Byte-identical to Compat
    │           └── ... (per-header .g.cs)
    └── Janset.SDL2.Image/           # 5 TFMs; ProjectReference Core; DisableRuntimeMarshalling
        ├── Janset.SDL2.Image.csproj
        ├── Support/DisableRuntimeMarshalling.cs
        └── Generated/{Compat,Modern}/  # No Handles.g.cs (consumer mode); imports Core's namespace SDL2
```

### Constitution policy index (current)

- §"Layer Contract" — three layers (Internal Raw ABI / Public Typed Low-Level / Public Friendly Overload). Spike currently produces Layer 1 only.
- §"Function Surface" — what enters the binding.
- §"BCL-Replaceable Helper Exclusion Policy" — three-condition rule + standing exclusions (C-long helpers + SDL_iconv).
- §"C Variadics" — __arglist policy.
- §"Scalar Type Translation" — wchar_t opaque + C long hybrid + SDL_bool enum + SDL_GUID → System.Guid.
- §"Opaque Handles" — Pattern B spec + Implementation mechanism (auto-detect criterion + roster JSON + cross-assembly DisableRuntimeMarshalling + 3-position rewrite).
- §"Foreign Type Boundary Policy" — Vulkan/D3D/GDK active; X11/Wayland/Apple/WinRT deferred.
- §"Structs And Unions" — force-opaque allow-list (SDL_RWops, SDL_SysWMinfo, SDL_SysWMmsg).
- §"Evidence Gates" — what must verify clean before a slice closes.

### Standing invariants (do not violate)

- **Same-named tag+typedef pattern** (`typedef struct SDL_Window SDL_Window;`) is the common opaque-handle shape in SDL2. ClangSharp's `--with-transparent-struct` mechanism DOES NOT WORK for this — `VisitRecordDecl` has no skip guard for transparent-flagged names; duplicate emit. **Roslyn postprocess is mandatory** for this shape (Agent C research verified). Do not propose ClangSharp-only solutions.
- **Pattern B operators**: explicit-only. No implicit `nint → X` or `X → nint`. Deliberate escape via `DangerousGetHandle()`.
- **Pattern B `Equals(object obj)` no nullable annotation** in generated `.g.cs` (CS8669 fix; no `#nullable enable` directive in any generated file — ClangSharp convention). The spec example at Decision 1 HOW L65-95 shows `Equals(object?)` with a footnote explaining the emit-form drift.
- **Cross-OS container mount forbidden**: never bind-mount Windows-host repo into a Linux container running dotnet/vcpkg. Use COPY-into-image + isolated cache volumes instead (binding-generator.Dockerfile baked pattern).
- **Plan vs. RSP quoting**: RSP files use unquoted-with-literal-space form (`wchar_t *=nint`, `const wchar_t *=nint`). argv-direct invocations use shell quotes. The plan/spec/Constitution/research record this distinction.
- **Spike output is committed** (Layer 1 raw ABI surface). Regeneration is reproducible (idempotent except for CRLF noise). Do not propose Roslyn source generators (Constitution L41 + Roadmap M7 require committed `.g.cs` files with `.generated-stamp`).

## Current State You Should Assume Until Verified

- **Branch HEAD**: `09957a7` — `refactor(binding-spike): explicit --owner-mode CLI flag for uniform-opaque`. 37 commits ahead of `origin/spike/binding-autogen-sdl2-gfx`.
- **Worktree expectation**: clean.
- **Oracle Priority C findings**: 0 across `platform-sensitive-wchar`, `platform-sensitive-long`, `deferred-layout-*`, `duplicate-tag-typedef` categories. Hard Bug section absent.
- **Multi-TFM compile**: 0 warnings / 0 errors across `Janset.SDL2.Core` (5 TFMs), `Janset.SDL2.Image` (5 TFMs), `AbiTests` (4 TFMs).
- **AbiTests runtime**: Windows x64 net10/9/8/462 all 1/1 passed; Linux x64 net10 docker 1/1 passed. Other RIDs (linux-arm64, osx-x64, osx-arm64) deferred to CI per-RID matrix.
- **Slopwatch**: 0 issues with the prescribed exclude list.
- **Push gate**: pending Deniz approval. Per AGENTS.md "Approval Gate", do NOT push to remote without explicit go.

Verify with:
```bash
git log --oneline master..HEAD | head
git status
dotnet build spikes/binding-generators/clangsharp/src/Janset.SDL2.Core/Janset.SDL2.Core.csproj -c Release
dotnet build spikes/binding-generators/clangsharp/src/Janset.SDL2.Image/Janset.SDL2.Image.csproj -c Release
dotnet build spikes/binding-generators/clangsharp/tests/abi-tests/AbiTests.csproj -c Release
dotnet test --project spikes/binding-generators/clangsharp/tests/abi-tests/AbiTests.csproj -c Release --framework net10.0
```

## Recommended Next Step

Three options. **Talk to Deniz before committing to which one.**

### 1. Layer 2 — Typed Low-Level Public API (Recommended, multi-session arc)

**What**: Project Layer 2 onto the now-stable Layer 1 raw ABI surface. Public `SDL2.SDL` / `SDL_image.IMG` static classes with methods that call `SDLNative` internal raw, exposing typed handles, `nint`, spans, pointers, and unsafe overloads where SDL requires them. Layer 2 reuses Pattern B handle types directly (no Layer 2 work for handle types themselves).

**Pre-flight**:
- Read Constitution §"Layer Contract" L34-50 (the public-typed-low-level row).
- Read the closure summary at `spikes/binding-generators/docs/priority-c-closure-summary.md` to confirm Layer 1 surface.
- Read `spikes/binding-generators/docs/next-iteration-plan.md` for any Layer 2 sketch.
- Verify current AbiTests covers the most-fragile dispatch (SDL_ThreadID); Layer 2 may extend it for symbols exercising the typed-handle round-trip.

**Touch points (high-level)**:
- New rewriter or codegen step: `Layer2PublicMethodEmitter` (Roslyn). Reads internal `SDLNative` partial class, emits matching public methods on `SDL2.SDL` that call them by value. Translation table: `byte*` strings stay `byte*` (or get span overloads in Layer 3), Pattern B handles stay by-value, primitives untouched.
- Decision points to brainstorm with Deniz BEFORE coding:
  - Should Layer 2 live in a new `Layer2.g.cs` per family (Modern + Compat), or be additional `.g.cs` files mirroring per-header organization? Handles.g.cs canonical-home precedent suggests the former.
  - How does Layer 2 handle the `byte*` → `string` boundary? Pure projection (`byte*` passthrough) for Layer 2; full friendly overloads (`string`/`Span<T>`/`out T`) deferred to Layer 3.
  - Does Layer 2 need new postprocess pipeline step, or is it part of `uniform-opaque`'s second sweep? Probably new step.

**Acceptance criteria**: `Janset.SDL2.Core` exposes a typed public surface that calls `SDLNative.*` internally; no public extern declaration leaks; AbiTests + a new Layer 2 test exercise both Layer 1 and Layer 2 paths; multi-TFM clean.

### 2. Alimer-style CppAst comparison evidence (single-session, ADR-blocker)

**What**: Repeat the spike work with an Alimer-style single-pass CppAst toolchain to provide evidence for ADR-004 amendment. ClangSharp + Roslyn is the active prototype; the comparison must produce equivalent ABI-correct output via CppAst to validate the toolchain decision.

**Pre-flight**:
- Read `docs/decisions/2026-05-14-binding-autogen-toolchain.md` (ADR-004 — currently Reopened).
- Read `spikes/binding-generators/output/reports/iteration-2-comparison.md` for the existing comparison framework.
- Read the Cake-hosted CppAst implementation at `build/_build/Targets/GenerateBindings/` (sunset reference — patterns may transfer).

**Touch points**: New `spikes/binding-generators/alimer-style/` subtree (already scaffolded per spike README). Likely a single-pass C# console app that walks CppAst output and emits Layer 1 raw ABI matching the ClangSharp spike's output (oracle comparison passes).

**Acceptance criteria**: CppAst-based output matches ClangSharp+Roslyn output 1:1 by oracle category; ADR-004 amendment proposal ready.

### 3. CI per-RID matrix expansion for AbiTests (lightweight, well-scoped)

**What**: AbiTests currently has runtime evidence on Windows x64 (4 TFMs) + Linux x64 net10 docker. CI matrix should extend to linux-arm64, osx-x64, osx-arm64 to complete the 7-RID coverage.

**Pre-flight**:
- Read `.github/workflows/regenerate-bindings.yml` for the existing per-RID matrix.
- Read `build/manifest.json` `runtimes[]` for the RID/triplet/container_image mapping.
- Verify the binding-generator container is publishable (currently local-build only; ADR or workflow change needed to push to GHCR).

**Touch points**: New CI workflow file or extension of existing one that runs AbiTests across the 7-RID matrix using either real runners (Windows + macOS) or containerized invocations (Linux).

**Acceptance criteria**: AbiTests pass on all 7 RIDs in CI; closure summary doc updated with full matrix evidence.

## Mandatory Grounding (read in this order)

1. `AGENTS.md` — canonical agent contract; "Approval Gate" + bilingual policy.
2. `CLAUDE.md` — relay to AGENTS.md (sanity).
3. **`spikes/binding-generators/docs/priority-c-closure-summary.md`** — authoritative Priority C closure record; six-risks resolution table + evidence + architecture snapshot. **Read this before anything else spike-related.**
4. `docs/binding-autogen/binding-generator-constitution.md` — canonical policy. Sections: Layer Contract (L34-50), BCL-Replaceable Helper Exclusion Policy, Function Surface, Scalar Type Translation, Opaque Handles, Foreign Type Boundary Policy, Structs And Unions.
5. `docs/research/semantic-abi-type-classification-research.md` — research evidence backing Priority C decisions; Appendix A findings 6-10 (typed handle ABI verification, ClangSharp --remap, C long peer survey, wchar_t cross-library survey, opaque struct patterns); Appendix B findings 11-18 (foreign-type boundary survey).
6. `spikes/binding-generators/README.md` — spike-level status + quick-start.
7. `spikes/binding-generators/docs/llm-handoff.md` — LLM-to-LLM handoff doc (cross-references the closure summary).
8. `spikes/binding-generators/docs/next-iteration-plan.md` — slice plan with Priority C closed, Layer 2 next, and the 2026-05-25 review follow-up backlog.
9. `spikes/binding-generators/clangsharp/policy/opaque-handle-roster.json` — canonical policy data (read end-to-end; the `excluded_candidates` rationale explains many edge cases).
10. `spikes/binding-generators/output/reports/oracle-evidence-clangsharp.md` — current oracle baseline (0 findings across Priority C categories).
11. `docs/decisions/2026-05-14-binding-autogen-toolchain.md` — ADR-004 (Reopened).

Code to inspect (in order of importance for Layer 2 work):

1. `spikes/binding-generators/clangsharp/postprocess/OpaqueHandleEmitRewriter.cs` + `UniformOpaqueOwnerMode.cs` + `Program.cs` (uniform-opaque mode) — the canonical "two-phase rewriter + owner-mode flag + Handles.g.cs writer" pattern Layer 2 might mirror.
2. `spikes/binding-generators/clangsharp/postprocess/ThreadIdDualDispatchRewriter.cs` — canonical mode-aware Compat/Modern emit pattern.
3. `spikes/binding-generators/clangsharp/postprocess/DllImportToLibraryImportRewriter.cs` — Modern-only emit + `VisitCompilationUnit` using-injection pattern.
4. `spikes/binding-generators/clangsharp/postprocess/GuidSubstitutionRewriter.cs` — simple type-substitution + using-insertion pattern.
5. `spikes/binding-generators/clangsharp/generate_bindings.py` — the 6-step pipeline; see `extend_rsp_arguments` helper, `run_postprocess`, and the `uniform-opaque` step with `--owner-mode` wiring.
6. `spikes/binding-generators/clangsharp/src/Janset.SDL2.Core/Janset.SDL2.Core.csproj` — file-routed Compat/Modern via `<Compile Include Condition>`; InternalsVisibleTo to AbiTests.
7. `spikes/binding-generators/clangsharp/src/Janset.SDL2.Core/Generated/Modern/Handles.g.cs` — canonical Pattern B output (17 structs).
8. `spikes/binding-generators/clangsharp/tests/abi-tests/AbiTests.csproj` + `ThreadIdAbiTests.cs` — InternalsVisibleTo bridge + OS-dispatched native lib pattern.

## Locked Policy Recap

These are most-likely-to-be-tempting-to-violate during forward work:

- **No commit without explicit user "go / yap / apply / proceed / başla"**. AGENTS.md Approval Gate. Holds for branch creation, force-push, dependency bumps, and remote push.
- **Constitution authority binds toolchain-neutrally**. Pattern B shape, opaque handle policy, foreign type boundary — these are spike-output-agnostic. Do not propose a Layer 2 that deviates from them.
- **Documents are first-class citizens**. Any policy change lands in Constitution + Spec + Plan + Closure Summary + Roster JSON in the same commit (or in a strictly ordered policy-first → code-second sequence).
- **"No workarounds, no shortcuts"** — never hack code/tests to silence errors. If a fix feels like a workaround, surface it.
- **No bind-mount Windows-host repo into Linux container running dotnet/vcpkg**. COPY-into-image only.
- **`git mv` FIRST during migrations** (preserves `git log --follow` / `git blame -C` history).
- **Same-named tag+typedef SDL2 handles require Roslyn postprocess** (ClangSharp native flags don't work for this shape).
- **Pattern B `Equals(object obj)` no nullable annotation in generated files** (CS8669 convention).
- **Spike output is committed; regeneration must be idempotent** (CRLF-only diff acceptable; substantive diff means a rewriter bug).
- **No CPM (Directory.Packages.props) edits without orchestrator approval**.
- **Subagent dispatch hygiene**: every subagent reads official .NET / ClangSharp docs as needed; loads `superpowers:verification-before-completion`; reports BLOCKED on ambiguity rather than guessing.
- **Bilingual TR/EN communication, millennial dev tone** (Deniz preference; doesn't constrain code).

## Final Steering Note

Priority C closure is a real milestone — the spike now has a complete, runtime-evidenced Layer 1 raw ABI surface with policy machinery (roster JSON, BCL exclusion rule, foreign-type allow-list, cross-assembly Pattern B contract) that future SDL2 versions can extend without re-deriving from scratch. The next session's hardest call isn't technical; it's **scope discipline**. Layer 2 is the natural continuation but it touches public API design — every method projection decision has API-shape consequences. Brainstorm with Deniz before designing the projection scheme. If anything in this prompt contradicts what `git log` / the closure summary / the Constitution actually says, **trust the live repo and update this prompt**.

37 commits await Deniz's push approval. Until that approves, all work continues local-only. Hold the line.
