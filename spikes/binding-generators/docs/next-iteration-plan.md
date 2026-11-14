# Spike Follow-up Ledger

**Date:** 2026-11-14
**Status:** Current tooling guidance and open follow-ups, with historical foundation evidence retained below.

## Current tooling and direction

`dotnet run --file tools.cs -- generate-bindings` is the local entry to the `GenerateBindings` Cake
target: a read-only plan by default, `--execute` for staged complete-family generation, `--check` for
committed generation stamps and `--evidence` for the hosted oracle. Postprocess and oracle run in the
host; the spike orchestrator and standalone CLI shells are retired. The manifest owns generation
profiles, and RSPs and shims live beside it. Stamps are committed and release/pack validate freshness
without regenerating. [Maintenance](../../../docs/engineering/binding-generator-maintenance.md)
owns commands; [generation canon](../../../docs/architecture/binding-generation.md) owns the contract.

The [roadmap](../../../docs/ROADMAP.md) owns next work: the ClangSharp upgrade, binding surface
collapse, Linux-canonical generation and generator image, then source promotion. The open items below
remain review inputs; [ABI policy](../../../docs/architecture/binding-abi-policy.md) supersedes the
historical layer model and boolean policy recorded in the foundation evidence.

## Historical foundation direction

The following direction and implementation record describe the 2026-05 foundation, not current policy.

Layer 2 takes the stable Layer 1 raw ABI surface and projects a public typed low-level API across the five SDL2 families (Core, Image, GFX, TTF, Mixer). Public methods on the manifest-driven public class (for example `SDL2.SDL`) call the internal raw ABI class. Typed handles, enums, structs, callbacks, and constants remain public generated types per Constitution §"Layer Contract".

See [Binding promotion gates](../../../docs/architecture/binding-generation.md#promotion-evidence-gates) for current scope and exit evidence.

### Historical foundation iteration

Layer 2 typed public API — **Core foundation implementation** (2026-05). The design was complete; that slice activated SDL2.Core public typed output only, with satellite activation and deeper friendly APIs left to later slices. Artifacts produced in that cycle:

- **Phase A peer mapping:** [`canonical/references/2026-05-29-layer-2-peer-mapping.md`](canonical/references/2026-05-29-layer-2-peer-mapping.md) — ppy / Alimer / Silk / SDL2-CS mapped to our L1/L2/L3 under Litmus A.
- **Cross-family requirements matrix:** [`canonical/references/2026-05-30-layer-2-cross-family-requirements.md`](canonical/references/2026-05-30-layer-2-cross-family-requirements.md) — per-pass × per-family + the satellite-only exceptions a Core-first design forgets (library-agnostic invariants).
- **Design evidence:** internal research, 2026-05-29, "Binding Autogen - Layer 2 Typed Public API Projection (Design Spec)" — the original typed/friendly projection design; current decisions are owned by [binding ABI policy](../../../docs/architecture/binding-abi-policy.md) and its ADRs.
- **Implementation evidence:** internal research, 2026-05-31, "Layer 2 Core Foundation Implementation Plan" — Core L2 foundation (family-blind projection + universal StructLayout + clong both-paths + public-API snapshot).
- **Expert reviews (×2, 2026-05-30 + follow-up ×4, 2026-05-31):** initial reviews (unbiased P/Invoke + context-full peer-benchmarked) validated the architecture and caught legacy-TFM compile blockers. The 2026-05-31 parallel review added required plan fixes before implementation: callback type-shape coverage beyond direct `delegate*` params (`delegate*`, `delegate* *`, callback returns), guarded platform-attribute usings, public macro/value member classification (ordinary consts + string-like `ReadOnlySpan<byte>` properties + endian const suppression), Core-only `public-typed` orchestrator activation, stronger attribute filtering, public-API policy assertions, and a small public-L2 smoke through the existing ABI test project. Design call remains: **single public surface** — uniform signatures + `#if`-pragma bodies + verbatim attribute-trivia copy, NOT a TFM-split.

- **Forward-scope ledger (everything beyond the foundation plan — single resume anchor):** [`canonical/references/2026-05-30-layer-2-design-and-deferred-scope.md`](canonical/references/2026-05-30-layer-2-design-and-deferred-scope.md) — ratified design decisions, deferred slices + activation sequence, Layer 3 scope, Constitution policy reconciliation, and deferred review findings, each pointing to its authoritative source.

**Execution gates of that slice:** preserve callback direct/pointer/return compile fixtures, public macro/member classification, no public endian-const freeze, Core-only public output, existing `abi-tests` public-L2 smoke, Modern/Compat projectable-member parity review, and Constitution/spec reconciliation in the Layer 2 changeset. Function-like macro helpers, runtime/endian aliases, satellite public output, and callback lifetime/rooting policy remain deferred should-fixes or later slices.

## Forward Backlog

The forward backlog absorbs durable follow-ups distilled from the Layer 1 expansion wave's reviewer reports (2026-05-25) and the Iteration 2 and Item 5 specs (Phase 0.5 audit folds). Items below do not reopen settled Layer 1 design decisions.

### Historical handoff blockers

Completed handoff evidence from 2026-05; retained as history, not current work.

| Item | Why it matters | Status |
| --- | --- | --- |
| Fail ClangSharp generation when any invocation fails | The retired orchestrator could report success after a ClangSharp failure, leaving stale output. | Fixed 2026-05-25; the hosted generator retains failure classification and exit-code tests. Current run diagnostics are untracked, not a committed generation report. |
| Rewrite Pattern B handles inside callback function-pointer signatures, or lower raw callback slots to `nint` deliberately | Modern `SDL_SetWindowHitTest` exposed `delegate* unmanaged[Cdecl]<SDL_Window*, ...>` after `SDL_Window*` method parameters were rewritten to by-value Pattern B handles. That is pointer-sized at the wire level but semantically invites pointer-to-wrapper confusion. | Fixed 2026-05-25: `OpaqueHandleEmitRewriter` now rewrites function-pointer parameter/return slots; Modern `SDL_SetWindowHitTest` emits `delegate* unmanaged[Cdecl]<SDL_Window, ...>`. |
| Remove broken `docs/superpowers/...` references from durable docs and code comments | The Priority C design content lives inline in the Constitution and closure docs; links to deleted/absent `docs/superpowers` files make future agents chase phantom authority. | Fixed 2026-05-25 for durable docs and code comments. Temporary review reports under `docs/temp/` intentionally keep their original reviewer text. |
| Correct evidence wording around runtime ABI coverage | The closure evidence covers ABI-family smoke, not full 7-RID proof. | Fixed 2026-05-25: AbiTests now include `SDL_GetThreadID(SDL_Thread.Null)` host coverage; docs say "ABI-family smoke now; 7-RID proof before production flip". |

### Production Flip Gates

These are not required to keep iterating on Layer 2, but they must be closed or consciously deferred before the generator becomes production source.

| Item | Why it matters | Target / evidence |
| --- | --- | --- |
| 7-RID runtime ABI matrix for retained C `long` symbols | The product contract is all supported RIDs, not only local Windows/Linux x64. Include `SDL_ThreadID` and `SDL_GetThreadID`; prefer a width/sign-sensitive assertion or native sentinel where practical. | `clangsharp/tests/abi-tests`; per-RID CI integration. |
| `SDL_GUID` ABI/value roundtrip smoke | `System.Guid` is sequential and 16 bytes on current .NET, but SDL GUID byte ordering and string semantics are user-visible. | Add `SDL_GUIDToString` / `SDL_GUIDFromString` roundtrip test and keep the Constitution note clear that `Guid.ToString()` is not SDL raw hex rendering. |
| Cross-assembly Pattern B runtime smoke through SDL_image | Compile proves `[DisableRuntimeMarshalling]` accepts Core-owned Pattern B handles in Image signatures, but one runtime satellite call would strengthen evidence. | `IMG_Init` / a low-risk Image function under AbiTests or a future package smoke. |
| Postprocess standalone/idempotency hardening | Several rewriters are correct for the current full pipeline but depend on ordering or source-text details. | Keep `ClongDualDispatchRewriter` self-contained for usings, replace `PlatformDeltaPostProcessor` source-text using insertion, and add a run-twice idempotency harness. |
| Validate SDL2 version in config against the active manifest | The former separate generator config duplicated the library version. | Resolved: the typed generation profile reads the existing library version from [the manifest](../../../build/manifest.json); no duplicate version or mismatch override remains. |
| Correct RSP precedence docs and probe duplicate-key behavior | Keyed `--remap` / `--with-type` entries are additive; ClangSharp rejects duplicate keys. | Resolved in the host's RSP composition and tests; [RSP maintenance](../../../docs/engineering/binding-generator-maintenance.md#rsp-maintenance) owns the rule. |
| Improve evidence report UX | Oracle and generation reports should show failure/check counts directly instead of requiring inference from prose. | Render watched raw ABI checks as explicit `0 finding(s)` rows; derive generated-file counts from actual output; include failure count as a top-level field. |
| Audit Windows pointer-sized callback typedefs | `SDL_SetWindowsMessageHook` currently emits `uint, ulong, long` callback parameters; `WPARAM` / `LPARAM` are pointer-sized and win-x86 is in scope. | Add a platform-width sensor/follow-up before production flip. |
| Emit explicit enum backing for ABI-sensitive enums | Generated enums rely on the default `int` backing where the native backing is known. `SDL_bool` is not an enum under current policy. | Consider a general explicit-backing policy for generated enums. [ADR-0007](../../../docs/adr/0007-sdl-bool-abi-struct.md) and [ABI policy](../../../docs/architecture/binding-abi-policy.md#sdl_bool) own the `SDL_bool` struct; the [roadmap](../../../docs/ROADMAP.md) binding surface collapse implements it. |
| Emit explicit `[StructLayout(LayoutKind.Sequential)]` on all generated data structs | ClangSharp emits `LayoutKind.Explicit` for unions but omits `LayoutKind.Sequential` for sequential data structs (`FPSmanager`, `SDL_Color`, `SDL_AudioSpec`, `SDL_Rect`, ...). C# default for structs is `Sequential` in practice and `[assembly: DisableRuntimeMarshalling]` prevents runtime reordering, but ECMA-335 treats layout without `[StructLayout]` as unspecified. Explicit `[StructLayout(LayoutKind.Sequential)]` is the interop hygiene posture taken by ppy, SDL2-CS, and Microsoft's P/Invoke best practices. | Resolved: the host postprocess runs `StructLayoutSequentialRewriter` (`struct-layout`) for both backends, a universal cross-family policy with no per-family config. Evidenced by `FPSmanager` in GFX Item 3 (2026-05-27). |
| Audit endian/platform-computed macro emission | ClangSharp evaluates value-like macros through the generation host. On Windows-local generation, `AUDIO_*SYS`, `MIX_DEFAULT_FORMAT`, `SDL_BYTEORDER`, `SDL_FLOATWORDORDER`, and `SDL_PIXELFORMAT_*32` collapse to little-endian constants; `SDL_platform.h` also emits Windows platform macros. Current supported RIDs are little-endian, but this is source-semantic drift and could become wrong for future big-endian targets or public docs. | Macro policy/postprocess or Layer 2 helper strategy. See [`canonical/satellites/sdl2-endian-platform-macro-consolidation.md`](canonical/satellites/sdl2-endian-platform-macro-consolidation.md). |
| Package smoke against real native packages | Compile + AbiTests prove the spike layout, but production must validate against the real `Janset.SDL2.<Family>.Native` packages once published. | Production CI gate; runs after production source flip and native package publish. |
| Spike-to-production transition: retire spike-local config | The separate configuration was a proving ground for the manifest shape. | Resolved: [the manifest](../../../build/manifest.json) owns typed generation settings and family profiles; postprocess consumes them. Generation orchestration is hosted; source promotion remains open. |
| Spike-to-production transition: retire orchestrator + CLI shells | Spike-local entry points duplicated build orchestration. | Resolved: the Cake target owns generation, postprocess and oracle evidence; `tools.cs generate-bindings` is the local route. Rewriters and tests live in the host, RSPs and shims beside the manifest. |
| Spike-to-production transition: promote spike `src/` to production `src/` | The spike workspace at `spikes/binding-generators/clangsharp/src/Janset.SDL2.<Family>/` is the production-shape candidate. At production flip, the projects move via `git mv` into the production tree, preserving history. | `git mv` from spike `src/` to repo `src/`; `Generated/{Compat,Modern}/` layout, Pattern B handle shape, C `long` policy, and `[Flags]` policy carry over unchanged. |
| Spike-to-production transition: `.generated-stamp` reproducibility metadata | Complete family output needs deterministic input/output provenance. | Resolved: generation commits family stamps; release and pack check them without regeneration. [Stamp contract](../../../docs/architecture/binding-generation.md#generation-stamp-contract) owns fields and limits. |

### Layer 2 / Layer 3 Follow-ups

Items belonging to Layer 2 typed public API + Layer 3 friendly overloads work.

| Item | Why it matters | Target / evidence |
| --- | --- | --- |
| Document Stage 1 `SDL_RWops` and `SDL_SysWM*` limitations in consumer-facing docs | Pattern B quarantine is the safe Stage 1 choice, but users need to know custom RWops and native window-manager info are deferred. | Preview docs / release notes / Layer 2 API docs. |
| Add HIDAPI wide-string decoders | Raw `wchar_t* -> nint` is ABI-honest, but consumers need platform-aware decoding helpers to read HID strings safely. | Layer 3 helper: Windows UTF-16, POSIX UTF-32 transcode. |
| Plan Stage 2 typed layout or helper strategy for `SDL_RWops` | Opaque Stage 1 blocks custom managed-backed RWops setup. | Either verified per-platform layout or a managed/native helper such as an `RWops` builder. |
| Guard future `SDL_GetWindowWMInfo` activation | `SDL_SysWMinfo` is not a pointer-like opaque object for that API; it requires caller-allocated concrete layout. | No `SDL_GetWindowWMInfo` emission unless the layout is verified per platform or projected through a deliberate platform-specific wrapper. |
| Mixer callback lifetime/rooting policy and deterministic callback smoke (4-element deferred policy) | SDL2_mixer callback registrations (`Mix_SetPostMix`, `Mix_HookMusic`, `Mix_HookMusicFinished`, `Mix_ChannelFinished`, `Mix_RegisterEffect`, `Mix_UnregisterEffect`, `Mix_EachSoundFont`) can outlive the immediate call, so Layer 1 only proves raw callback signatures. The deferred policy must define four elements: (1) Compat delegate rooting — how Compat `[UnmanagedFunctionPointer(CallingConvention.Cdecl)]` delegates stay rooted for the full native registration lifetime; (2) Modern `delegate* unmanaged[Cdecl]` authoring rules — how Modern callbacks are authored (including `[UnmanagedCallersOnly]` discipline) for the function-pointer surface; (3) dummy-audio runtime smoke setup — how callback roundtrip is proven without depending on real audio hardware; (4) callback cleanup/unregistration policy — how persistent callbacks are torn down deterministically when their managed owner releases them. | Layer 2/Layer 3 callback policy plus follow-up AbiTests or package smoke after generated callbacks are promoted. |
| TTF runtime font-asset smoke | Layer 1 closure does not require runtime ABI smoke; the four follow-up candidates are `TTF_Init`/`TTF_WasInit`/`TTF_Quit` lifecycle, `TTF_OpenFont`/`TTF_CloseFont` with a small redistributable `.ttf` test asset, `TTF_OpenFontIndex(..., 0)` parameter-position C `long` smoke, and `TTF_FontFaces` return-position C `long` smoke. Prerequisite: redistributable font asset and native SDL2_ttf dependency copy strategy. | Layer 2/Layer 3 AbiTests once font-asset strategy lands. |
| Runtime-endian public aliases for audio/pixel defaults | `MIX_DEFAULT_FORMAT` currently emits as `0x8010` because `AUDIO_S16SYS` was evaluated on a little-endian generator host. This is correct for all current RIDs but less portable than SDL2-CS/ppy-style runtime expressions. | Consider public typed API aliases/properties for `AUDIO_*SYS` / `MIX_DEFAULT_FORMAT`, plus equivalent policy for `SDL_PIXELFORMAT_*32`. See [`canonical/satellites/sdl2-endian-platform-macro-consolidation.md`](canonical/satellites/sdl2-endian-platform-macro-consolidation.md). |
| ppy orchestrator feature parity (deferred from Slice 5) | Per-header `.rsp` lookup landed during Priority C. Remaining ppy-pattern parity: companion-file manual-symbol exclusion feedback regex (`[Constant]` / `[Typedef]` markers) and full dynapi validation pass. Not on the critical path for Layer 2 expansion. | Layer 2/Layer 3 orchestrator enhancement once Layer 2 typed API patterns reveal what companion-helper feedback is required. |
| Function-like SDL macros (helper-class policy) | `TTF_VERSION(X)`, `TTF_VERSION_ATLEAST(X,Y,Z)`, `SDL_MIXER_VERSION(X)`, `MIX_VERSION(X)`, `SDL_MIXER_VERSION_ATLEAST(X,Y,Z)`, and similar function-like macros are not Layer 1 raw ABI output. Generation report classifies them as helper candidates. | Layer 2 / Layer 3 helper strategy; see [`canonical/satellites/sdl2-function-like-macro-consolidation.md`](canonical/satellites/sdl2-function-like-macro-consolidation.md). |
| Drift-watchdog extension for owner-mode satellites | Current drift report covers Core-owned handles; satellite-owned-handle owner-mode (TTF / Mixer) should extend the watchdog to detect zero-handle scenarios that imply a roster/code drift. | `OpaqueHandleEmitRewriter` drift report extension. |
| `SDL_bool` bool affordance | Every peer (Silk / SDL2-CS / ppy) gives consumers a bool path; `if (x == SDL_bool.SDL_TRUE)` is verbose (expert review, 2026-05-30). | Superseded: [ADR-0007](../../../docs/adr/0007-sdl-bool-abi-struct.md) makes `SDL_bool` an ABI-exact struct with implicit `bool` conversion; the [roadmap](../../../docs/ROADMAP.md) binding surface collapse implements it. |

### Accepted Tradeoffs / No Action

Items deliberately accepted as-is.

| Item | Decision |
| --- | --- |
| Explicit-only Pattern B handle conversion | Keep. This is deliberate type safety; implicit conversion can be added later if preview feedback shows real friction. |
| `VkSurfaceKHR` mapped through `nint` at the foreign boundary | Keep. This is the correct SDL/Vulkan boundary shape for the current raw signature. |
| `SDL_RWops`, `SDL_SysWMinfo`, `SDL_SysWMmsg` Pattern B quarantine | Keep for Stage 1. It avoids false cross-platform layouts. |
| `CallConvCdecl` on POSIX | Keep. It is the portable source-generated P/Invoke spelling for platform C ABI; add docs only if future reviewers keep tripping over the name. |
| SDL2_net bindings | Out of scope for the Layer 1 expansion wave. Reconsider if/when there is concrete user demand and a maintained native package. |
| Alimer-style comparison evidence | Historical evidence in `output/reports/iteration-2-comparison.md`; the scaffold is removed, not retained as a fallback implementation. |

## Cross-References

- [`binding-abi-policy.md`](../../../docs/architecture/binding-abi-policy.md) — policy authority.
- [Binding promotion gates](../../../docs/architecture/binding-generation.md#promotion-evidence-gates) — current generated-source evidence requirements.
- [`binding-generation.md`](../../../docs/architecture/binding-generation.md) — mechanism details and classification labels.
- [`binding-generator-maintenance.md`](../../../docs/engineering/binding-generator-maintenance.md) — maintenance procedures.
- [`binding-output-review.md`](../../../docs/engineering/binding-output-review.md) — multi-oracle review workflow.
- [`canonical/satellites/`](canonical/satellites/) — per-family + cross-family satellite analyses.
- [Oracle evidence collection](../../../docs/engineering/binding-output-review.md#tools-and-commands) — current command; the former tracked report is archived in the private companion repository.
- [`../output/reports/iteration-2-comparison.md`](../output/reports/iteration-2-comparison.md) — toolchain decision evidence.
- [`../../../docs/adr/0004-binding-autogen-toolchain.md`](../../../docs/adr/0004-binding-autogen-toolchain.md) — ADR-004 (Reopened).
