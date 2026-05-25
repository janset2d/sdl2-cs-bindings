# Raw ABI Upstream Testing Spec

**Date:** 2026-05-25
**Status:** Slice-local design for the ClangSharp spike. Revised after feasibility review to target high-coverage, headless-safe raw ABI testing before manual diagnostics.

## Purpose

Expand the ClangSharp spike's `AbiTests` from a small smoke suite into a high-coverage SDL2 raw ABI conformance suite. The suite validates Layer 1 `SDLNative` calls against real `Janset.SDL2.Core.Native` payloads while the generated API is still inside the spike.

The suite uses upstream SDL2 automation tests as the primary behavioral oracle, but it is not a blind SDL_test clone. It ports most upstream automation cases that are deterministic, headless-safe, and meaningful for managed binding risks. It also adds Janset-owned header-based tests where upstream coverage is thin or where generated binding risks need direct assertions.

This is not a production test topology change. The work stays under `spikes/binding-generators/clangsharp/` until the generator is promoted. The project name and path remain unchanged for now; production naming can be cleaned up during promotion.

## Hard Boundaries

- Use one test project during the spike: `spikes/binding-generators/clangsharp/tests/abi-tests/AbiTests.csproj`.
- Do not create separate test projects per SDL subsystem, stage, platform, or manual scenario during this spike.
- Do not move this suite into root `tests/` during the spike.
- Do not consume managed `Janset.SDL2.Core` packages in this suite. Keep the spike `ProjectReference` to `clangsharp/src/Janset.SDL2.Core/Janset.SDL2.Core.csproj`.
- Resolve the native library through `Janset.SDL2.Core.Native` from the local feed.
- Treat `spikes/binding-generators/references/SDL` as a gitignored upstream reference clone, not a runtime test dependency.
- Copy only selected stable assets into the slice when a test needs them, with provenance and license text.
- Manual diagnostics are out of scope for this phase. Do not add `Manual/`, `RequiresManualDiagnosticsAttribute`, real-window visual diagnostics, real speaker playback, haptic, GL/GLES/Vulkan, or hardware-input tests in this slice.
- Broad subsystem porting starts only after reusable test infrastructure is in place and the currently written tests are normalized onto it.

## Ground Rules

### Headless-Safe Default

Default `dotnet test` runs only automated, headless-safe tests. A test is default-safe when it is deterministic across CI hosts and does not require physical hardware, visible windows, real speakers, external network, desktop session clipboard behavior, or a specific real graphics stack.

Allowed default-safe classes:

- pure scalar/struct/string APIs;
- asset-backed file APIs using committed tiny fixtures;
- temp-file workflows isolated under per-test temp directories;
- dummy video/audio driver workflows when a skip attribute proves driver availability;
- software renderer workflows on dummy video when validated as stable;
- capability-gated tests that report clear TUnit skips when the runtime capability is absent.

Deferred or non-default classes:

- real display/focus/gamma/brightness assertions;
- hardware joystick/gamecontroller, haptic, sensor, and hotplug behavior;
- real speaker playback;
- GL/GLES/Vulkan/Metal backend diagnostics;
- `SDL_syswm` until typed union generation is intentionally supported;
- `SDL_CreateThread` family until thread-creation macro/REAL entrypoint handling is intentionally supported;
- OS clipboard and primary-selection tests unless capability-gated and proven stable per OS family.

### Upstream Adoption

Use upstream SDL2 tests as behavior oracles and traceability anchors. Port most `testautomation_*.c` cases that satisfy the headless-safe rule.

Every upstream-derived test carries source metadata:

```csharp
[UpstreamSdlTest("test/testautomation_rect.c", "rect_testIntersectRect")]
```

The C# method name stays idiomatic and describes the behavior. The metadata preserves the upstream source without forcing C naming into the test API.

### Janset Header-Based Coverage

Upstream coverage is not the only source of truth. Add Janset-owned tests per SDL header when needed to cover generated binding risks that upstream does not exercise directly:

- exported symbol presence versus generated declarations;
- struct size, field offset, explicit-layout union shape, and opaque-handle policy;
- enum, flag, and macro constant values;
- function-like SDL macros that require C# helper equivalents;
- UTF-8 span macro constants;
- callback signatures and lifetime bridging across Modern and Compat TFMs;
- platform-width-sensitive types such as `size_t`, `Sint64`, `nint`, `CLong`, and `CULong`.

Mechanical full-surface guardrails should eventually cover the generated raw surface more broadly than hand-authored runtime tests. Runtime tests prove representative behavior and high-risk ABI paths; mechanical checks prove breadth.

### Infrastructure First

Before another broad subsystem port, stabilize reusable infrastructure and retrofit the existing tests onto it. At minimum the suite needs:

- `AbiCategories` constants for category names;
- `AbiParallelKeys` constants for keyed non-parallel groups;
- `SdlAssert`, `SdlError`, and `SdlUtf8` helpers;
- `AbiTempDirectory` and asset catalog helpers;
- `SdlSubsystemScope`, `SdlEnvironmentScope`, and `SdlHintScope`;
- `SdlRuntimeProbe` for native runtime facts and driver/capability discovery;
- reusable skip attributes for dummy video, dummy audio, OS/platform, capability, and backend-sensitive tests;
- callback lifetime helpers that bridge Modern `delegate* unmanaged` and Compat `IntPtr` delegate patterns;
- small C# equivalents for stable SDL function-like macros needed by upstream ports.

### TUnit And Parallelism

- Pure ABI tests may run in parallel.
- Tests mutating SDL process-global state must use keyed non-parallel groups such as `SDL.Global`, `SDL.Video`, `SDL.Audio`, `SDL.Events`, `SDL.Hints`, or `SDL.Error`.
- Do not make the whole assembly non-parallel. That would hide ordering bugs and slow the suite unnecessarily.
- Prefer reusable skip attributes over inline early returns so skipped capabilities are visible in reports.
- Use data-driven TUnit tests for repetitive matrices such as math, rect, pixel formats, strings, enum values, and layout cases.
- Keep bespoke tests for ownership/lifetime, callbacks, complex cleanup, and multi-call workflows.
- Use MTP/TUnit `--treenode-filter` for category runs. Do not document old VSTest `--filter` for this project.

### Multi-Agent Execution

After infrastructure stabilization, use multi-agent execution by SDL header or subsystem. Agents may work in parallel only when their tasks do not require shared infrastructure edits.

Agent task boundaries:

- one agent per header/subsystem slice;
- each agent receives the exact upstream source file(s), generated binding file(s), local target path, category/parallel policy, and verification command;
- agents add or edit only their assigned test files unless a coordinator explicitly opens an infrastructure change;
- each agent reports upstream cases ported, skipped, deferred, and any generated binding blocker;
- coordinator runs full verification and resolves cross-slice consistency.

## Test Project Layout

The spike keeps one project and splits by folders plus TUnit categories:

```text
spikes/binding-generators/clangsharp/tests/abi-tests/
├── AbiTests.csproj
├── ThreadIdAbiTests.cs
├── Infrastructure/
│   ├── Assets/
│   │   ├── AbiTempDirectory.cs
│   │   └── UpstreamSdlAsset.cs
│   ├── Callbacks/
│   │   ├── SdlCallbackBridge.Compat.cs
│   │   └── SdlCallbackBridge.Modern.cs
│   ├── Classification/
│   │   ├── AbiCategories.cs
│   │   ├── AbiParallelKeys.cs
│   │   └── UpstreamSdlTestAttribute.cs
│   ├── Diagnostics/
│   │   ├── SdlAssert.cs
│   │   └── SdlError.cs
│   ├── Macros/
│   │   └── SdlMacro.cs
│   ├── Runtime/
│   │   ├── Requirements/
│   │   │   ├── RequiresAudioDummyDriverAttribute.cs
│   │   │   └── RequiresVideoDummyDriverAttribute.cs
│   │   ├── Scopes/
│   │   │   ├── SdlEnvironmentScope.cs
│   │   │   ├── SdlHintScope.cs
│   │   │   └── SdlSubsystemScope.cs
│   │   └── SdlRuntimeProbe.cs
│   └── Text/
│       └── SdlUtf8.cs
├── Mechanical/
│   ├── Symbols/
│   ├── Layout/
│   └── Constants/
├── Upstream/
│   ├── Pure/
│   ├── Assets/
│   ├── DummyDrivers/
│   ├── GlobalState/
│   └── CapabilityGated/
└── TestAssets/
    └── sdl2-upstream/
```

Folder boundaries are navigation only. The assembly remains `Janset.SDL2.AbiTests`, and runtime tests call `SDLNative` directly.

## Categories

Use constants from `AbiCategories` once infrastructure is in place.

| Category | Meaning |
| --- | --- |
| `AbiSmoke` | Small host smoke that should always run. Existing thread-ID tests stay here. |
| `AbiUpstreamPort` | Behavior ported from upstream SDL2 tests. |
| `AbiHeaderCoverage` | Janset-owned header-based runtime or mechanical coverage. |
| `AbiMechanical` | Symbol/layout/constant/macro surface guardrails. |
| `AbiPure` | Headless tests that do not require real assets, dummy drivers, or SDL global state. |
| `AbiAssets` | Headless tests using copied real assets or temp files. |
| `AbiGlobalState` | Tests mutating SDL process-global state. |
| `AbiDummyDriver` | Headless tests requiring dummy video/audio or software renderer. |
| `AbiCapabilityGated` | Tests that run only when a probed runtime capability exists. |
| `SDL.<Subsystem>` | Subsystem tag such as `SDL.Rect`, `SDL.RWops`, `SDL.Video`, `SDL.Audio`. |

## Coverage Strategy

Runtime tests target ABI risk families:

- pointer ownership and create/free pairs;
- by-ref structs and out parameters;
- UTF-8 string input/output;
- callbacks and delegate/function-pointer signatures;
- event unions and explicit-layout structs;
- `SDL_bool`, enum, flag, and scalar return values;
- `size_t`, `Sint64`, `nint`, `CLong`, and `CULong` width-sensitive APIs;
- Modern `LibraryImport` and Compat `DllImport` behavior across TFMs;
- path/file marshalling through real assets and temp files;
- SDL global-state lifecycle through disciplined scopes.

Targets after feasibility review:

- P0 raw ABI spine: `70-100` stable tests.
- P1 headless breadth: additional `80-130` stable tests.
- P2 capability-gated/environment-sensitive: additional `20-40` tests.
- Default stable headless suite: approximately `260-280` tests.
- Aspirational total with gated lanes: `300+` runtime tests.
- Mechanical guardrails: as close to full generated raw surface coverage as practical for symbols, constants, layouts, callbacks, and macro policy.

Do not promise `300+` always-running headless tests until backend-sensitive SDL areas are proven stable across CI hosts. The suite may reach `300+` total through capability-gated tests and scheduled/full validation lanes.

## Upstream Feasibility Summary

The SDL2 upstream automation inventory is approximately:

- `318` declared `testautomation_*.c` cases;
- `310` enabled upstream cases;
- `260-280` strong headless/raw ABI candidates;
- `20-35` additional backend-sensitive but possible candidates;
- `35-55` cases that should be skipped, deferred, or treated as future manual/hardware diagnostics.

High-value P0/P1 sources:

- `testautomation_rect.c`
- `testautomation_rwops.c`
- `testautomation_surface.c`
- `testautomation_pixels.c`
- `testautomation_guid.c`
- `testautomation_platform.c`
- `testautomation_timer.c`
- `testautomation_hints.c`
- `testautomation_events.c`
- `testautomation_audio.c`
- `testautomation_video.c` dummy-safe subset
- `testautomation_render.c` enabled software-render subset
- `testautomation_keyboard.c`
- `testautomation_mouse.c` dummy-safe subset
- `testautomation_math.c`
- `testautomation_stdlib.c`
- `testautomation_main.c`
- `testautomation_subsystems.c`
- `testautomation_joystick.c` virtual joystick subset when available

Known blockers or deferrals:

- `testautomation_syswm.c` waits for `SDL_SysWMinfo` / `SDL_SysWMmsg` typed union generation and `SDL_GetWindowWMInfo`.
- Thread creation tests wait for explicit handling of `SDL_CreateThread` macro/REAL entrypoints.
- OS clipboard/primary selection is backend-sensitive and should be capability-gated after dedicated review.
- Real display/focus/gamma/brightness and interactive input behavior are not default-safe.

## Verification Commands

Default verification for the spike project:

```pwsh
dotnet test --project spikes/binding-generators/clangsharp/tests/abi-tests/AbiTests.csproj -c Release
slopwatch analyze --fail-on warning --exclude "artifacts/**,external/**,vcpkg_installed/**,spikes/binding-generators/references/**,**/bin/**,**/obj/**"
git diff --check
```

Category-specific runs should use TUnit/MTP tree filters after the exact expression is verified for this project. Do not document unverified filter commands.
