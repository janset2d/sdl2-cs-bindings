# Raw ABI Current Test Inventory

**Date:** 2026-05-25
**Scope:** Current ClangSharp spike `AbiTests.csproj` state after infrastructure normalization and the P0 pure/RWops expansion.

## Baseline Verification

Run:

```pwsh
dotnet test --project spikes/binding-generators/clangsharp/tests/abi-tests/AbiTests.csproj -c Release
```

Result:

- `total: 576`
- `failed: 0`
- `succeeded: 576`
- `skipped: 0`
- executable TFMs: `net10.0`, `net9.0`, `net8.0`, `net462`
- unique tests discovered per executable TFM: `144`
- explicit `net462` check: `dotnet test --project spikes/binding-generators/clangsharp/tests/abi-tests/AbiTests.csproj -c Release --framework net462` → `144/144` passed

## Current Files

| File | Discovered tests per TFM | Current role | Next action |
| --- | ---: | --- | --- |
| `ThreadIdAbiTests.cs` | 2 | Host/thread smoke for Modern/Compat dispatch. | Keep as `AbiSmoke`. |
| `Upstream/Pure/RectAbiTests.cs` | 18 | Deterministic integer/float rect upstream ports with const-input guard checks. | Add parameter-negative cases when null-pointer coverage policy is ready. |
| `Upstream/Pure/GuidAbiTests.cs` | 11 | GUID parse/string variants with raw SDL byte-order checks. | Keep; expand joystick GUID aliases if needed. |
| `Upstream/Pure/TimerAbiTests.cs` | 2 | Performance counter/frequency smoke. | Move or mark as global-state when adding delay/timer callback cases. |
| `Upstream/Pure/PlatformAbiTests.cs` | 3 | Platform/version/error string behavior. | Split error-state keyed parallelism; expand platform/endian cases. |
| `Upstream/Pure/PixelsAbiTests.cs` | 89 | Pixel format names/allocation, palette allocation, and gamma ramp upstream ports. | Add color mapping/conversion cases after deciding breadth vs generated macro/header guardrails. |
| `Upstream/Assets/RwopsAbiTests.cs` | 11 | Memory/file/const-memory/endian/alloc RWops upstream ports with temp assets. | `SDL_RWFromFP` remains deferred; add any missing negative close/error cases only if generator exposes needed surface. |
| `Upstream/Assets/SurfaceAbiTests.cs` | 1 | BMP load/save smoke. | Expand enabled software-surface and BMP cases. |
| `Upstream/Assets/WavAbiTests.cs` | 1 | WAV load/free smoke. | Keep; expand with audio conversion/load cases where stable. |
| `Upstream/GlobalState/HintsAbiTests.cs` | 1 | Custom hint round-trip. | Expand upstream hints and callbacks. |
| `Upstream/GlobalState/EventsAbiTests.cs` | 2 | Event queue and event watch callback smoke. | Expand event queue/filter/watch coverage. |
| `Upstream/DummyDrivers/AudioAbiTests.cs` | 1 | Dummy audio open/close smoke. | Keep dummy-driver category; normalize driver probes and parallel key. |
| `Upstream/DummyDrivers/VideoAbiTests.cs` | 1 | Dummy window lifecycle smoke. | Keep; expand dummy-safe video subset. |
| `Upstream/DummyDrivers/RenderAbiTests.cs` | 1 | Dummy/software renderer lifecycle smoke. | Keep; expand enabled software-render subset after helper normalization. |

## Current Infrastructure

| File | Responsibility | Next action |
| --- | --- | --- |
| `Infrastructure/Classification/AbiCategories.cs` | Central category constants. | Added during normalization. |
| `Infrastructure/Classification/AbiParallelKeys.cs` | Central keyed non-parallel group names. | Added during normalization. |
| `Infrastructure/Classification/UpstreamSdlTestAttribute.cs` | Upstream source/function traceability. | Keep. |
| `Infrastructure/Diagnostics/SdlAssert.cs` | SDL return/bool assertion helpers. | Keep; add richer diagnostics where useful. |
| `Infrastructure/Diagnostics/SdlError.cs` | `SDL_GetError` / `SDL_ClearError`. | Keep; use keyed parallelism for error-state tests. |
| `Infrastructure/Text/SdlUtf8.cs` | UTF-8 pin/decode helper. | Keep; consider byte-span helpers only when tests need them. |
| `Infrastructure/Assets/AbiTempDirectory.cs` | Per-test temp directory. | Keep. |
| `Infrastructure/Assets/UpstreamSdlAsset.cs` | Copied asset lookup. | Keep; rename only during production promotion if needed. |
| `Infrastructure/Runtime/Scopes/SdlSubsystemScope.cs` | SDL subsystem init/quit scope. | Keep; use in global-state tests. |
| `Infrastructure/Runtime/Scopes/SdlEnvironmentScope.cs` | Environment variable restore scope. | Keep; use only in keyed non-parallel tests. |
| `Infrastructure/Runtime/Scopes/SdlHintScope.cs` | SDL hint restore scope. | Keep; use only in keyed non-parallel tests. |
| `Infrastructure/Runtime/SdlRuntimeProbe.cs` | Runtime facts and driver discovery. | Added during normalization. |
| `Infrastructure/Runtime/Requirements/RequiresVideoDummyDriverAttribute.cs` | TUnit skip for dummy video. | Delegates to `SdlRuntimeProbe`. |
| `Infrastructure/Runtime/Requirements/RequiresAudioDummyDriverAttribute.cs` | TUnit skip for dummy audio. | Delegates to `SdlRuntimeProbe`. |
| `Infrastructure/Callbacks/SdlCallbackBridge.Modern.cs` | Modern event-watch callback helper. | Added during normalization. |
| `Infrastructure/Callbacks/SdlCallbackBridge.Compat.cs` | Compat event-watch callback helper. | Added during normalization. |
| `Infrastructure/Macros/SdlMacro.cs` | Test-local equivalents for used SDL function-like macros. | Added during normalization. |

## Known Normalization Work

- `AbiCategories` replaced category string literals with constants.
- `AbiParallelKeys` replaced broad `[NotInParallel]` with keyed constraints where global state is involved.
- `SdlRuntimeProbe` centralizes runtime facts and capability checks.
- Callback bridge helpers isolate Modern vs Compat event-watch callback mechanics.
- `SdlMacro` helpers exist only for SDL function-like macros used by current tests.
- Hints/events moved to `Upstream/GlobalState`; dummy-driver folders now only hold dummy video/audio/render tests.

## Known Generated Binding Blockers

- `SDL_syswm` upstream automation remains deferred until `SDL_SysWMinfo` / `SDL_SysWMmsg` typed union generation and `SDL_GetWindowWMInfo` are available.
- Thread creation runtime tests remain deferred until `SDL_CreateThread` macro/REAL entrypoint handling is intentionally supported.
- Function-like SDL macros need generated or test-local helper policy before direct upstream ports are clean.
