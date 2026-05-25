# Raw ABI Current Test Inventory

**Date:** 2026-05-25
**Scope:** Current ClangSharp spike `AbiTests.csproj` state after infrastructure normalization, the P0 pure/RWops expansion, SDL2 Core hints/events global-state coverage expansion, and dummy audio/video/software-render coverage expansion.

## Baseline Verification

Run:

```pwsh
dotnet test --project spikes/binding-generators/clangsharp/tests/abi-tests/AbiTests.csproj -c Release
```

Result:

- `total: 692`
- `failed: 0`
- `succeeded: 692`
- `skipped: 0`
- executable TFMs: `net10.0`, `net9.0`, `net8.0`, `net462`
- unique tests discovered per executable TFM: `173`
- explicit `net462` coverage is included in the multi-target run above: `173/173` passed

## Current Files

| File | Discovered tests per TFM | Current role | Next action |
| --- | ---: | --- | --- |
| `ThreadIdAbiTests.cs` | 2 | Host/thread smoke for Modern/Compat dispatch. | Keep as `AbiSmoke`. |
| `Upstream/Pure/RectAbiTests.cs` | 18 | Deterministic integer/float rect upstream ports with const-input guard checks. | Add parameter-negative cases when null-pointer coverage policy is ready. |
| `Upstream/Pure/GuidAbiTests.cs` | 11 | GUID parse/string variants with raw SDL byte-order checks. | Keep; expand joystick GUID aliases if needed. |
| `Upstream/Pure/TimerAbiTests.cs` | 2 | Performance counter/frequency smoke. | Move or mark as global-state when adding delay/timer callback cases. |
| `Upstream/Pure/PlatformAbiTests.cs` | 8 | Faithful platform/version/revision/error upstream ports plus separate exact platform/version generated-header consistency coverage. | Deferred: `SDL_VERSION` upstream macro port until an actual generated/accessibly exposed macro equivalent exists; endian/swap, CPU feature probes, power-info, `SDL_GetErrorMsg`, and function-like version macro helpers (`SDL_VERSIONNUM`, `SDL_VERSION_ATLEAST`) until their generator/test-local macro policy is explicit and upstream coverage is portable. |
| `Upstream/Pure/PixelsAbiTests.cs` | 89 | Pixel format names/allocation, palette allocation, and gamma ramp upstream ports. | Add color mapping/conversion cases after deciding breadth vs generated macro/header guardrails. |
| `Upstream/Assets/RwopsAbiTests.cs` | 11 | Memory/file/const-memory/endian/alloc RWops upstream ports with temp assets. | `SDL_RWFromFP` remains deferred; add any missing negative close/error cases only if generator exposes needed surface. |
| `Upstream/Assets/SurfaceAbiTests.cs` | 9 | BMP save/reload, missing-load failure, surface conversion/header smoke, overflow/pitch checks, and blend-none header smoke. | Deferred: upstream fixture-comparison blit/conversion cases, upstream-disabled blend modes, and 32-bit-only overflow path until a real 32-bit/native lane exists. |
| `Upstream/Assets/WavAbiTests.cs` | 1 | WAV load/free smoke. | Keep; expand with audio conversion/load cases where stable. |
| `Upstream/GlobalState/HintsAbiTests.cs` | 3 | Mixed coverage: one custom hint round-trip smoke plus upstream environment/default/override/reset and hint callback reset/delete ports. | Deferred: full `_HintsEnum` sweep until generated string-like macro null-termination/header-coverage policy is explicit. |
| `Upstream/GlobalState/EventsAbiTests.cs` | 3 | Event queue, NULL-userdata watch, and userdata watch upstream ports with delete verification. | Keep event queue/filter/watch coverage headless and keyed by global event state. |
| `Upstream/DummyDrivers/AudioAbiTests.cs` | 5 | Dummy audio upstream ports for driver init/quit, open/close, global status, device status, and lock/unlock. | Deferred: callback pause/unpause timing and conversion/resample cases until stable callback/capability policy exists for all TFMs. |
| `Upstream/DummyDrivers/VideoAbiTests.cs` | 6 | Dummy video upstream ports for window lifecycle, flags, ID lookup, pixel format, size, and window-surface/renderer interaction. | Deferred: display-mode, brightness/gamma, position, min/max-size sweep, window-data, centered-on-display, syswm, and WM/event-sensitive cases that are not dummy-portable or need broader generated surface. |
| `Upstream/DummyDrivers/RenderAbiTests.cs` | 5 | Dummy/software render coverage for render-driver enumeration, renderer lifecycle/clear, primitive draw calls, texture query/copy, and texture color modulation. | Deferred: upstream image-comparison blit suites and disabled blend/alpha suites until asset/reference comparison policy is explicit for raw ABI dummy runs. |

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
- `SDL_GetErrorMsg` is generated in the raw ABI, but local SDL2 upstream test sources do not exercise it; keep it deferred rather than inventing behavior outside upstream evidence.
