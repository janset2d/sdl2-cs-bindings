# Spike Next-Iteration Plan — Layer 2 Typed Public API

**Date:** 2026-05-28
**Status:** Active spike plan for Layer 2 typed public API work. SDL2 Layer 1 raw ABI closed across Core / Image / GFX / TTF / Mixer; the canonical roadmap §"Layer 2 — Typed Public API Projection" is the milestone scope.

## Direction

Layer 2 takes the stable Layer 1 raw ABI surface and projects a public typed low-level API across the five SDL2 families (Core, Image, GFX, TTF, Mixer). Public methods on the manifest-driven public class (for example `SDL2.SDL`) call the internal raw ABI class. Typed handles, enums, structs, callbacks, and constants remain public generated types per Constitution §"Layer Contract".

See [`canonical/binding-generator-roadmap.md`](canonical/binding-generator-roadmap.md) §"Layer 2 — Typed Public API Projection" for full scope, exit evidence, and non-goals.

## Active Iteration

To be filled by the Layer 2 brainstorm/spec/plan cycle. This section tracks the current iteration's active work items.

## Forward Backlog

The forward backlog absorbs durable follow-ups distilled from the Layer 1 expansion wave's reviewer reports (2026-05-25) and the Iteration 2 and Item 5 specs (Phase 0.5 audit folds). Items below do not reopen settled Layer 1 design decisions.

### Handoff Blockers

Items that must be cleared before merge / production-flip.

| Item | Why it matters | Status |
| --- | --- | --- |
| Fail ClangSharp generation when any invocation fails | `generate_bindings.py` records ClangSharp failures but can still return success, so stale generated files can masquerade as a good run. | Fixed 2026-05-25: generation exit-code policy now returns 2 when any ClangSharp invocation fails; self-test covers the policy. The committed generation report refreshes on the next clean regeneration. |
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
| Validate SDL2 version in config against the active manifest | The config carries per-family `library_version`, but the postprocess does not enforce that it matches `build/manifest.json`. | Compare family `library_version` in `config/family-config.json` to `build/manifest.json` before owner-mode `uniform-opaque`; allow an explicit spike-only mismatch override if needed. |
| Correct RSP precedence docs and probe duplicate-key behavior | Current comments imply keyed `--remap` / `--with-type` entries can be overridden by later RSP tiers, but ClangSharp rejects duplicate keys. | `generate_bindings.py` comments/self-test and `rsp/per-header/README.md`; describe keyed entries as additive-only unless proven otherwise. |
| Improve evidence report UX | Oracle and generation reports should show failure/check counts directly instead of requiring inference from prose. | Render watched raw ABI checks as explicit `0 finding(s)` rows; derive generated-file counts from actual output; include failure count as a top-level field. |
| Audit Windows pointer-sized callback typedefs | `SDL_SetWindowsMessageHook` currently emits `uint, ulong, long` callback parameters; `WPARAM` / `LPARAM` are pointer-sized and win-x86 is in scope. | Add a platform-width sensor/follow-up before production flip. |
| Emit explicit enum backing for ABI-sensitive enums | `SDL_bool` is currently ABI-correct because C# enum default backing is `int`, but the Constitution says it must be int-backed. | Emit `public enum SDL_bool : int`; consider a general explicit-backing policy for generated enums where native backing is known. |
| Emit explicit `[StructLayout(LayoutKind.Sequential)]` on all generated data structs | ClangSharp emits `LayoutKind.Explicit` for unions but omits `LayoutKind.Sequential` for sequential data structs (`FPSmanager`, `SDL_Color`, `SDL_AudioSpec`, `SDL_Rect`, ...). C# default for structs is `Sequential` in practice and `[assembly: DisableRuntimeMarshalling]` prevents runtime reordering, but ECMA-335 treats layout without `[StructLayout]` as unspecified. Explicit `[StructLayout(LayoutKind.Sequential)]` is the interop hygiene posture taken by ppy, SDL2-CS, and Microsoft's P/Invoke best practices. | Roslyn postprocess rewriter: walk every `*StructDeclarationSyntax` without an existing `[StructLayout]` attribute and prepend `[StructLayout(LayoutKind.Sequential)]` + ensure `using System.Runtime.InteropServices;`. Universal cross-family policy — no per-family config. Evidenced by `FPSmanager` in GFX Item 3 (2026-05-27). |
| Audit endian/platform-computed macro emission | ClangSharp evaluates value-like macros through the generation host. On Windows-local generation, `AUDIO_*SYS`, `MIX_DEFAULT_FORMAT`, `SDL_BYTEORDER`, `SDL_FLOATWORDORDER`, and `SDL_PIXELFORMAT_*32` collapse to little-endian constants; `SDL_platform.h` also emits Windows platform macros. Current supported RIDs are little-endian, but this is source-semantic drift and could become wrong for future big-endian targets or public docs. | Macro policy/postprocess or Layer 2 helper strategy. See [`canonical/satellites/sdl2-endian-platform-macro-consolidation.md`](canonical/satellites/sdl2-endian-platform-macro-consolidation.md). |
| Package smoke against real native packages | Compile + AbiTests prove the spike layout, but production must validate against the real `Janset.SDL2.<Family>.Native` packages once published. | Production CI gate; runs after production source flip and native package publish. |
| Spike-to-production transition: retire spike-local config | `clangsharp/config/family-config.json` is a deliberate spike-local mirror of the eventual manifest shape — a dry run for the production-flip manifest-absorption migration. At production flip, the spike config retires and `build/manifest.json` becomes the single source of truth. | Spike retires at production flip: (a) validate spike output as production candidate, (b) approve expanded `build/manifest.json` schema, (c) delete `config/family-config.json`, (d) enable the production target reading manifest. |
| Spike-to-production transition: retire orchestrator + CLI shells | `generate_bindings.py` and `postprocess/Program.cs` CLI wrappers are spike-local orchestration. At production flip, the orchestration moves into the production generation target; rewriter classes (`OpaqueHandleEmitRewriter`, `ClongDualDispatchRewriter`, `FlagsAttributeRewriter`, `GuidSubstitutionRewriter`, `PlatformDeltaPostProcessor`, `StripVarargsRewriter`, `DllImportToLibraryImportRewriter`) are promotion candidates. | Spike retires at production flip; rewriter classes promoted into the production codegen pipeline; RSP files survive unchanged. |
| Spike-to-production transition: promote spike `src/` to production `src/` | The spike workspace at `spikes/binding-generators/clangsharp/src/Janset.SDL2.<Family>/` is the production-shape candidate. At production flip, the projects move via `git mv` into the production tree, preserving history. | `git mv` from spike `src/` to repo `src/`; `Generated/{Compat,Modern}/` layout, Pattern B handle shape, C `long` policy, and `[Flags]` policy carry over unchanged. |
| Spike-to-production transition: `.generated-stamp` reproducibility metadata | Production source must carry a reproducibility stamp recording the generation inputs (pin set, RSP digest, postprocess pipeline order). The spike does not emit this stamp. | Add `.generated-stamp` emission to the production generation target; production flip gate. |

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

### Accepted Tradeoffs / No Action

Items deliberately accepted as-is.

| Item | Decision |
| --- | --- |
| Explicit-only Pattern B handle conversion | Keep. This is deliberate type safety; implicit conversion can be added later if preview feedback shows real friction. |
| `VkSurfaceKHR` mapped through `nint` at the foreign boundary | Keep. This is the correct SDL/Vulkan boundary shape for the current raw signature. |
| `SDL_RWops`, `SDL_SysWMinfo`, `SDL_SysWMmsg` Pattern B quarantine | Keep for Stage 1. It avoids false cross-platform layouts. |
| `CallConvCdecl` on POSIX | Keep. It is the portable source-generated P/Invoke spelling for platform C ABI; add docs only if future reviewers keep tripping over the name. |
| SDL2_net bindings | Out of scope for the Layer 1 expansion wave. Reconsider if/when there is concrete user demand and a maintained native package. |
| Alimer-style CppAst comparison evidence | Out of scope as a separate slice per ADR-004. Reopened ADR may revisit if Layer 2 work surfaces a real CppAst advantage; otherwise the ClangSharp toolchain stands. |

## Cross-References

- [`canonical/binding-generator-constitution.md`](canonical/binding-generator-constitution.md) — policy authority.
- [`canonical/binding-generator-roadmap.md`](canonical/binding-generator-roadmap.md) — layer-based forward milestones; §"Layer 2 — Typed Public API Projection" is the active milestone scope.
- [`canonical/binding-generator-implementation-notes.md`](canonical/binding-generator-implementation-notes.md) — mechanism details and classification labels.
- [`canonical/binding-generator-maintenance.md`](canonical/binding-generator-maintenance.md) — maintenance procedures.
- [`canonical/binding-output-oracle-validation.md`](canonical/binding-output-oracle-validation.md) — multi-oracle review workflow.
- [`canonical/satellites/`](canonical/satellites/) — per-family + cross-family satellite analyses.
- [`../output/reports/oracle-evidence-clangsharp.md`](../output/reports/oracle-evidence-clangsharp.md) — current oracle evidence.
- [`../output/reports/iteration-2-comparison.md`](../output/reports/iteration-2-comparison.md) — toolchain decision evidence.
- [`../../../docs/decisions/2026-05-14-binding-autogen-toolchain.md`](../../../docs/decisions/2026-05-14-binding-autogen-toolchain.md) — ADR-004 (Reopened).
