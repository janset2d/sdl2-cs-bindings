# Priority C Semantic-ABI Completion — Closure Summary

**Date:** 2026-05-24
**Status:** Closed — six known semantic-ABI risks resolved + foreign-type boundary policy + Pattern B uniform handle emit.
**Branch:** `spike/binding-autogen-sdl2-gfx`
**Closure commit chain:** Slice C-A (`adb64d0`), Slice C-B (`45fdab6`), Slice C-C (`bb638f9` + follow-up closure work), DisableRuntimeMarshalling Constitution policy (`d0016de`), this closure summary.

## Original Six Risks

| # | Risk | Constitution authority | Resolution | Evidence |
|---|------|------------------------|------------|----------|
| R1 | shared `wchar_t*` → `ushort*` (silently wrong on POSIX 32-bit `wchar_t`; Linux/macOS buffer corruption) | §"wchar_t" L300-323 | `rsp/base.rsp` literal-space `--remap` entries (`wchar_t *=nint` + `const wchar_t *=nint`); old no-space entry removed | Oracle: 0 `platform-sensitive-wchar` findings |
| R2 | C `long`/`unsigned long` → `int`/`uint` (Linux/macOS LP64 truncated return) | §"C `long` And `unsigned long`" L246-283 + §"BCL-Replaceable Helper Exclusion Policy" L169-208 | Hybrid: `rsp/per-header/SDL_stdinc.rsp` `--exclude` for BCL-replaceable helpers (`SDL_lround`/`SDL_lroundf`/`SDL_ltoa`/`SDL_ultoa`/`SDL_strtol`/`SDL_strtoul`); current `ClongDualDispatchRewriter` mode-aware emit: Modern → `[LibraryImport]` + `CULong`, Compat → managed wrapper with `RuntimeInformation.IsOSPlatform` dispatch between `[DllImport] uint` (Win32) and `[DllImport] nint` (Unix64), normalized to `ulong` | Oracle: 0 `platform-sensitive-long` findings; AbiTests `SDL_ThreadID` non-zero on Win net10/net9/net8/net462 (4/4 TFMs); Linux x64 net10 docker pass |
| R3 | `SDL_RWops` full layout including platform-conditioned `_hidden_e__Union` (non-Windows: wrong layout, satellite-spreading) | §"Structs And Unions" L416-420 + §"Opaque Handles" Implementation mechanism L352-363 | `OpaqueHandleEmitRewriter` force-opaque allow-list (driven by `policy/opaque-handle-roster.json` `force_opaque_exceptions`); body stripped, Pattern B emit in `Handles.g.cs` | Oracle: 0 `deferred-layout-sdl-rwops` findings; `Generated/Modern/Handles.g.cs:103` shape `readonly partial struct SDL_RWops : IEquatable<SDL_RWops>` |
| R4 | `SDL_SysWMinfo` Windows-shaped partial layout (non-Windows: false ABI) | §"Structs And Unions" L422-425 | Same force-opaque allow-list entry; body stripped, Pattern B emit | Oracle: 0 `deferred-layout-sdl-syswminfo` findings; `Handles.g.cs:157` |
| R5 | `SDL_SysWMmsg` Windows-shaped partial layout (non-Windows: false ABI) | §"Structs And Unions" L422-425 | Same force-opaque allow-list entry; body stripped, Pattern B emit | Oracle: 0 `deferred-layout-sdl-syswmmsg` findings; `Handles.g.cs:175` |
| R6 | Opaque-handle tag leak (`SDL_hid_device_` vs canonical `SDL_hid_device`; `SDL_semaphore` vs canonical `SDL_sem`) | §"Opaque Handles" L325-340 | Per-header RSP canonicalization: `SDL_hid_device_=SDL_hid_device` in `rsp/per-header/SDL_hidapi.rsp`; `SDL_semaphore=SDL_sem` in `rsp/per-header/SDL_mutex.rsp`; cross-header repeats for `_SDL_Joystick` audit confirmed | Oracle: 0 `duplicate-tag-typedef` findings |

## Additional Resolutions

- **SDL_GUID → System.Guid substitution.** `GuidSubstitutionRewriter` walks `[NativeTypeName("SDL_GUID")]` annotations and rewrites the managed type to `System.Guid` (commit `bb638f9`, Slice C-C). The policy is captured in the Constitution §"Current SDL2.Core ABI Status" and the substitution rationale in `GuidSubstitutionRewriter.cs`.
- **Foreign Type Boundary Policy.** Constitution §"Foreign Type Boundary Policy" (L365-400) codifies the SDL2-CS-aligned IntPtr-at-foreign-boundary pattern for non-SDL-owned types: Vulkan / D3D / GDK active via per-header RSP `--remap`, Win32 / Android JNI / C stdlib already covered by base/sdl2-core RSP; Linux X11/Wayland/KMSDRM + macOS Cocoa + iOS UIKit + WinRT deferred behind future `SDL_syswm.h` multi-OS pass activation.
- **BCL-Replaceable Helper Exclusion Policy.** Constitution §"BCL-Replaceable Helper Exclusion Policy" (L169-208) codifies the three-condition rule (BCL equivalent exists; SDL2-CS does not expose; no transitive SDL dependency). Standing exclusions: SDL_lround/SDL_lroundf/SDL_ltoa/SDL_ultoa/SDL_strtol/SDL_strtoul + SDL_iconv_* family.
- **Pattern B uniform 3-position rewrite.** `OpaqueHandleEmitRewriter` rewrites single-pointer `SDL_X*` → by-value `SDL_X` at all three raw-ABI positions: method parameter, method return, and **struct field**. Double-pointer (`X**`) and `out X` positions preserved. ABI invariant holds because a Pattern B struct is exactly one `nint` field — bit-identical to a pointer at the corresponding C struct field offset.
- **DisableRuntimeMarshalling cross-assembly Pattern B contract.** `[assembly: DisableRuntimeMarshalling]` lands in `Janset.SDL2.Core` and `Janset.SDL2.Image` under `Support/DisableRuntimeMarshalling.cs` (NET7_0_OR_GREATER gated, commit `4b87037`). Resolves SYSLIB1051 conservative path on the LibraryImport source generator for satellite-by-value consumption of Core Pattern B handles. Policy codified in Constitution §"Opaque Handles" Implementation mechanism cross-assembly clause (commit `d0016de`).
- **Handles.g.cs canonical home.** 17 handles consolidated in `Generated/Modern/Handles.g.cs` and `Generated/Compat/Handles.g.cs` (byte-identical) in `Janset.SDL2.Core` (owner mode); `Janset.SDL2.Image` (consumer mode) references them via `ProjectReference` + namespace nesting (`SDL2.Image` → `SDL2` resolves Core handle names unqualified). 14 auto-detected (SDL_AudioStream, SDL_Cursor, SDL_GameController, SDL_Haptic, SDL_Joystick, SDL_Renderer, SDL_Sensor, SDL_Texture, SDL_Thread, SDL_Window, SDL_cond, SDL_hid_device, SDL_mutex, SDL_sem) + 3 force-opaque (SDL_RWops, SDL_SysWMinfo, SDL_SysWMmsg).

## Architecture Snapshot

- **Spike home:** `spikes/binding-generators/clangsharp/`. ClangSharp + Roslyn postprocess prototype (ADR-004 toolchain re-evaluation, active comparison branch).
- **Current 7-step postprocess pipeline** (`postprocess/Program.cs`): `platform-delta` → `strip-varargs` → `libraryimport` (Modern only) → `flags-detect` → `guid-substitute` → `clong-dispatch` → `uniform-opaque`.
- **Per-header RSP organization** (ppy/SDL3-CS pattern): `rsp/base.rsp` (cross-cutting remaps) + `rsp/sdl2-core.rsp` (family-scope) + `rsp/per-header/<header>.rsp` (per-header excludes/remaps).
- **Roster policy data** at `clangsharp/policy/opaque-handle-roster.json` — version-keyed on `sdl2_version` 2.32.10, three-source triangulated (SDL2 headers + wiki.libsdl.org + ClangSharp Modern output), carries `auto_detect_well_known` (14) + `force_opaque_exceptions` (3) + `excluded_candidates` (11 with rationale).
- **5 TFM matrix:** net10.0, net9.0, net8.0, netstandard2.0, net462. Csproj `<Compile Include Condition>` routes `Generated/Compat/**/*.cs` → legacy TFMs (netstandard2.0 + net462), `Generated/Modern/**/*.cs` → modern TFMs (net8.0+). AbiTests targets net10.0/net9.0/net8.0/net462 (4 TFMs; netstandard2.0 is a library-only TFM).
- **AbiTests harness** at `clangsharp/tests/abi-tests/` proves runtime ABI correctness on the real `Janset.SDL2.Core` assembly: `SDL_ThreadID` returns non-zero bit pattern, validating both Modern (`CULong` + `LibraryImport`) and Compat (managed wrapper + `RuntimeInformation` dispatch + Win32 `[DllImport]`) emit paths against the platform-resolved native SDL2 library.

## Verification Evidence (2026-05-24 closure run)

### Oracle (Step B.2)

```
dotnet run --file spikes/binding-generators/clangsharp/oracle.cs -- --family sdl2-core --family sdl2-image --write-report
```

| Category | Findings |
|---|---:|
| `platform-sensitive-wchar` | 0 |
| `platform-sensitive-long` | 0 |
| `deferred-layout-sdl-rwops` | 0 |
| `deferred-layout-sdl-syswminfo` | 0 |
| `deferred-layout-sdl-syswmmsg` | 0 |
| `duplicate-tag-typedef` | 0 |

Hard Bug section absent from `spikes/binding-generators/output/reports/oracle-evidence-clangsharp.md`. Evidence Gaps absent for both families. ClangSharp Modern: sdl2-core 929 functions / 413 constants / 218 types; sdl2-image 59 functions / 4 constants / 3 types. ClangSharp Compat: sdl2-core 933 functions / 413 constants / 204 types (Compat has +4 functions because legacy `[DllImport]` retains the synthesised dual-dispatch DllImports for `SDL_ThreadID` / `SDL_GetThreadID`); sdl2-image identical (Pattern B field-rewrite is shape-only, no count delta).

### Multi-TFM compile (Step B.3)

| Project | TFM count | Warnings | Errors |
|---|---:|---:|---:|
| `Janset.SDL2.Core` | 5 (net10/net9/net8/netstandard2.0/net462) | 0 | 0 |
| `Janset.SDL2.Image` | 5 (net10/net9/net8/netstandard2.0/net462) | 0 | 0 |
| `AbiTests` | 4 (net10/net9/net8/net462) | 0 | 0 |

### AbiTests runtime smoke (Steps B.4 + B.5)

| Platform | TFM | Result | Duration |
|---|---|---|---|
| Windows x64 | net10.0 | 1/1 passed | 608 ms |
| Windows x64 | net9.0 | 1/1 passed | 876 ms |
| Windows x64 | net8.0 | 1/1 passed | 890 ms |
| Windows x64 | net462 | 1/1 passed | 1.6 s |
| Linux x64 (focal docker) | net10.0 | 1/1 passed | 320 ms |

All TFM/platform pairs confirm `SDL_ThreadID` returns a non-zero OS thread identifier — validates both Modern (`CULong` + `LibraryImport`) and Compat (managed wrapper + `RuntimeInformation.IsOSPlatform` dispatch + Win32 `[DllImport]`) emit paths. Linux native lib resolution via `vcpkg_installed/x64-linux-hybrid/lib/libSDL2-2.0.so.0` symlink-aware dispatch.

Post-review note (2026-05-25): AbiTests now also call `SDL_GetThreadID(SDL_Thread.Null)` and assert it matches `SDL_ThreadID()` on the host path. The closure-run table above remains historical; full 7-RID runtime proof remains a production CI gate.

### Slopwatch (Step B.6)

```
slopwatch analyze --fail-on warning --exclude "artifacts/**,external/**,vcpkg_installed/**,spikes/binding-generators/references/**,**/bin/**,**/obj/**"
```

`Scan complete: 0 issue(s) found`.

### Regen idempotency (Step B.1)

```
python spikes/binding-generators/clangsharp/generate_bindings.py --family all --execute --vcpkg-triplet x64-windows-hybrid --use-platform-header-shims
```

Output diff with `--ignore-cr-at-eol`: empty (pipeline is byte-stable across regens; only CRLF-write noise appears in working tree and reverts cleanly). Multi-OS `SDL_system.h` platform-pass parse warnings are pre-existing and unrelated to Priority C scope (declspec parse error on iOS/macOS/Android views; ABI surface is sourced from the Windows-canonical neutral pass).

## Next Steps

- **Plan Task 18.** Update spike handoff doc (`llm-handoff.md`, `next-iteration-plan.md`, `README` at `spikes/binding-generators/clangsharp/`) with the Priority C closure outcome so the next session has the right starting context.
- **Plan Task 19.** Final code review + finishing branch decision (Deniz push-approval gate per AGENTS.md §Approval Gate).
- **Post-Priority-C horizon.** Layer 2 typed low-level public API slice. The Layer 1 raw ABI surface is now stable enough that Layer 2 can be projected onto it (typed `SDL2.SDL` methods calling internal `SDLNative`) without churn on the handle shape, scalar width, or foreign-type boundary.

## References

- **Constitution:** [`docs/binding-autogen/binding-generator-constitution.md`](../../../docs/binding-autogen/binding-generator-constitution.md) — §"Opaque Handles", §"C `long` And `unsigned long`", §"wchar_t", §"BCL-Replaceable Helper Exclusion Policy", §"Structs And Unions", §"Foreign Type Boundary Policy".
- **Durable policy:** [`docs/binding-autogen/binding-generator-constitution.md`](../../../docs/binding-autogen/binding-generator-constitution.md) — Pattern B, C `long` hybrid, `wchar_t*` opaque, SDL_GUID substitution, BCL helper exclusion, and Foreign Type Boundary Policy.
- **Active follow-up plan:** [`next-iteration-plan.md`](next-iteration-plan.md) — Layer 2 next scope plus the 2026-05-25 review follow-up backlog.
- **Research:** [`docs/research/semantic-abi-type-classification-research.md`](../../../docs/research/semantic-abi-type-classification-research.md) (2026-05-22) — semantic ABI classification backing for the six risks.
- **Oracle evidence:** [`spikes/binding-generators/output/reports/oracle-evidence-clangsharp.md`](../output/reports/oracle-evidence-clangsharp.md) — current run, 2026-05-24.
- **Roster JSON:** [`spikes/binding-generators/clangsharp/policy/opaque-handle-roster.json`](../clangsharp/policy/opaque-handle-roster.json) — single source of truth for the 14 auto-detect + 3 force-opaque + 11 excluded-candidate enumeration.
- **Handles.g.cs:** [`Generated/Modern/Handles.g.cs`](../clangsharp/src/Janset.SDL2.Core/Generated/Modern/Handles.g.cs) / [`Generated/Compat/Handles.g.cs`](../clangsharp/src/Janset.SDL2.Core/Generated/Compat/Handles.g.cs) — 17 Pattern B handle structs, byte-identical across emit modes.
