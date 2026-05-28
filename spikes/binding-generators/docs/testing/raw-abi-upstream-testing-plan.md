# Raw ABI High-Coverage Testing Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use `subagent-driven-development` for multi-agent implementation waves after infrastructure is stabilized. Until then, execute infrastructure tasks serially. Steps use checkbox (`- [ ]`) syntax for tracking. Do not create new test projects. Do not add manual diagnostics. Do not commit without Deniz's explicit approval.

**Goal:** Turn the existing ClangSharp `AbiTests.csproj` into a reusable, high-coverage SDL2 raw ABI test lab that can support `260-280` stable headless tests and `300+` total runtime tests with gated lanes.

**Architecture:** Keep one spike-local TUnit project as the execution boundary. Build reusable infrastructure first, normalize the current tests onto that infrastructure, then port upstream/header coverage by independent SDL header or subsystem slices using multi-agent waves.

**Tech Stack:** .NET 10 SDK, C# preview, TUnit, Microsoft.Testing.Platform, unsafe raw ABI calls, `Janset.SDL2.Core.Native` local-feed package, upstream SDL2 `SDL2` branch cloned under gitignored `spikes/binding-generators/references/SDL`.

---

## Execution Ground Rules

- Manual diagnostics are stopped for this slice. Do not add `Manual/`, manual skip attributes, real-window visual checks, real speaker playback, haptic, GL/GLES/Vulkan, or hardware input tests.
- Keep `spikes/binding-generators/clangsharp/tests/abi-tests/AbiTests.csproj` as the single spike project. Rename/topology cleanup happens during production promotion, not now.
- Before broad subsystem ports, harden infrastructure and retrofit current tests.
- Default automated tests must be headless-safe. Backend-sensitive tests require explicit skip/capability attributes.
- Upstream `testautomation_*.c` is the primary oracle, but Janset-owned header tests are equally valid when upstream does not cover generated binding risks.
- Multi-agent waves start only after shared infrastructure is stable. Agents should not edit shared infrastructure while working on independent subsystem slices.
- Category runs must use verified TUnit/MTP `--treenode-filter` expressions. Do not use stale VSTest `--filter` examples.

## Files And Responsibilities

| Path | Responsibility |
| --- | --- |
| `spikes/binding-generators/docs/testing/raw-abi-upstream-testing-spec.md` | Ground rules, coverage targets, and scope boundaries. |
| `spikes/binding-generators/docs/testing/raw-abi-upstream-testing-plan.md` | Implementation order and multi-agent wave plan. |
| `spikes/binding-generators/clangsharp/tests/abi-tests/AbiTests.csproj` | Single spike test project; native package resolution; asset copy items. |
| `spikes/binding-generators/clangsharp/tests/abi-tests/Infrastructure/Classification/` | Category constants, parallel-key constants, and upstream traceability metadata. |
| `spikes/binding-generators/clangsharp/tests/abi-tests/Infrastructure/Diagnostics/` | SDL assertion and error helpers. |
| `spikes/binding-generators/clangsharp/tests/abi-tests/Infrastructure/Text/` | UTF-8 pin/decode helpers. |
| `spikes/binding-generators/clangsharp/tests/abi-tests/Infrastructure/Assets/` | Test asset lookup and per-test temp directories. |
| `spikes/binding-generators/clangsharp/tests/abi-tests/Infrastructure/Runtime/` | Runtime facts, capability probes, skip requirements, and SDL global-state scopes. |
| `spikes/binding-generators/clangsharp/tests/abi-tests/Infrastructure/Callbacks/` | Modern/Compat callback lifetime and function-pointer helpers. |
| `spikes/binding-generators/clangsharp/tests/abi-tests/Infrastructure/Macros/` | Stable C# equivalents for SDL function-like macros needed by ports. |
| `spikes/binding-generators/clangsharp/tests/abi-tests/Mechanical/` | Symbol/layout/constant/header guardrails. |
| `spikes/binding-generators/clangsharp/tests/abi-tests/Upstream/Pure/` | Pure upstream and header-based runtime tests. |
| `spikes/binding-generators/clangsharp/tests/abi-tests/Upstream/Assets/` | Asset-backed and temp-file runtime tests. |
| `spikes/binding-generators/clangsharp/tests/abi-tests/Upstream/DummyDrivers/` | Dummy video/audio/software-renderer runtime tests. |
| `spikes/binding-generators/clangsharp/tests/abi-tests/Upstream/GlobalState/` | SDL init/quit, hints, events, error state, and subsystem lifecycle tests. |
| `spikes/binding-generators/clangsharp/tests/abi-tests/Upstream/CapabilityGated/` | Backend-sensitive tests with explicit capability skips. |

## Phase 0: Stabilize Current Slice

### Task 0.1: Freeze Manual Scope

**Files:**

- Modify: `spikes/binding-generators/docs/testing/raw-abi-upstream-testing-spec.md`
- Modify: `spikes/binding-generators/docs/testing/raw-abi-upstream-testing-plan.md`

- [ ] Remove Stage D/manual diagnostics from the active slice plan.
- [ ] State that manual diagnostics are deferred until after raw ABI headless infrastructure and coverage are stable.
- [ ] Verify no active task asks an agent to add `Manual/` or `RequiresManualDiagnosticsAttribute`.

### Task 0.2: Capture Current Test Inventory

**Files:**

- Create: `spikes/binding-generators/docs/testing/raw-abi-current-inventory.md`

- [ ] List current test files under `Infrastructure/`, `Upstream/Pure/`, `Upstream/Assets/`, and `Upstream/DummyDrivers/`.
- [ ] Record current unique test count and multi-TFM execution count from a fresh `dotnet test` run.
- [ ] Mark each current test as keep, refactor-to-infra, expand, or replace.
- [ ] Record any known generated binding blocker found during current tests.

### Task 0.3: Verify Baseline Before Refactor

Run:

```pwsh
dotnet test --project spikes/binding-generators/clangsharp/tests/abi-tests/AbiTests.csproj -c Release
```

Expected: existing automated tests pass or failures are recorded in `raw-abi-current-inventory.md` before any refactor.

## Phase 1: Build Reusable Test Infrastructure

### Task 1.1: Add Category And Parallel-Key Constants

**Files:**

- Create: `spikes/binding-generators/clangsharp/tests/abi-tests/Infrastructure/Classification/AbiCategories.cs`
- Create: `spikes/binding-generators/clangsharp/tests/abi-tests/Infrastructure/Classification/AbiParallelKeys.cs`
- Modify: existing tests under `spikes/binding-generators/clangsharp/tests/abi-tests/`

- [ ] Add `const string` category names matching the spec.
- [ ] Add keyed non-parallel names for `SDL.Global`, `SDL.Video`, `SDL.Audio`, `SDL.Events`, `SDL.Hints`, and `SDL.Error`.
- [ ] Replace string-literal categories in existing tests with constants where TUnit attribute constraints allow constants.
- [ ] Replace broad `[NotInParallel]` usage with keyed constraints where the test mutates only one SDL global-state area.

### Task 1.2: Add Runtime Probe And Capability Skips

**Files:**

- Create: `spikes/binding-generators/clangsharp/tests/abi-tests/Infrastructure/Runtime/SdlRuntimeProbe.cs`
- Modify: `RequiresVideoDummyDriverAttribute.cs`
- Modify: `RequiresAudioDummyDriverAttribute.cs`

- [ ] Centralize SDL platform, driver list, OS, process architecture, and runtime facts.
- [ ] Make skip attributes depend on `SdlRuntimeProbe` instead of duplicating driver enumeration.
- [ ] Add clear skip messages that include the missing driver or capability name.
- [ ] Keep probes side-effect-free except read-only SDL driver enumeration.

### Task 1.3: Add Callback Bridge Helpers

**Files:**

- Create: `spikes/binding-generators/clangsharp/tests/abi-tests/Infrastructure/Callbacks/SdlCallbackBridge.Modern.cs`
- Create: `spikes/binding-generators/clangsharp/tests/abi-tests/Infrastructure/Callbacks/SdlCallbackBridge.Compat.cs`
- Modify: existing callback tests under `Upstream/DummyDrivers/EventsAbiTests.cs`

- [ ] Hide Modern `delegate* unmanaged[Cdecl]` versus Compat `IntPtr` delegate plumbing behind small helpers.
- [ ] Ensure Compat delegates stay rooted for the full native callback registration lifetime.
- [ ] Keep callback helper APIs specific to actual SDL callback shapes used by tests; do not build a generic callback framework.

### Task 1.4: Add Macro Helper Surface

**Files:**

- Create: `spikes/binding-generators/clangsharp/tests/abi-tests/Infrastructure/Macros/SdlMacro.cs`

- [ ] Add only helpers required by current or next planned ports: BMP load/save macro equivalents, blit macro equivalent, centered-window position helper, endian swap helpers, and checked `size_t` overflow helpers.
- [ ] Each helper must cite the SDL macro it models in a local code comment.
- [ ] Do not add helpers for macros that no test uses.

### Task 1.5: Normalize Existing Tests

**Files:**

- Modify: `ThreadIdAbiTests.cs`
- Modify: `Upstream/Pure/*.cs`
- Modify: `Upstream/Assets/*.cs`
- Modify: `Upstream/DummyDrivers/*.cs`

- [ ] Apply category constants, parallel keys, runtime probes, callback bridge helpers, and macro helpers to current tests.
- [ ] Split tests into `Pure`, `Assets`, `DummyDrivers`, or `GlobalState` when current placement no longer matches the spec.
- [ ] Keep assertions behavior-first and include `SdlError.Current` through `SdlAssert` for SDL return-code failures.
- [ ] Verify with full `dotnet test` after normalization.

## Phase 2: P0 ABI Spine Multi-Agent Wave

After Phase 1 passes, dispatch independent agents by header/subsystem. Each agent receives the exact upstream source file, generated binding file, destination test file, category rules, and verification command.

| Agent Slice | Upstream Source | Destination | Target |
| --- | --- | --- | --- |
| Rect | `test/testautomation_rect.c` | `Upstream/Pure/RectAbiTests.cs` | Port most/all 36 cases. |
| RWops | `test/testautomation_rwops.c` | `Upstream/Assets/RwopsAbiTests.cs` | Port all 10 automation cases plus temp-file hygiene. |
| Surface | `test/testautomation_surface.c` | `Upstream/Assets/SurfaceAbiTests.cs` | Port enabled software-surface/BMP cases; defer upstream-disabled blend cases. |
| Pixels | `test/testautomation_pixels.c` | `Upstream/Pure/PixelsAbiTests.cs` | Port all automation cases and add header-owned format metadata cases. |
| GUID | `test/testautomation_guid.c` | `Upstream/Pure/GuidAbiTests.cs` | Port all cases and variants needed for byte/string ABI confidence. |
| Platform/Error/Version | `test/testautomation_platform.c`, `testver.c`, `testerror.c` | `Upstream/Pure/PlatformAbiTests.cs` | Port stable platform/version/error/endian behavior. |
| Timer | `test/testautomation_timer.c` | `Upstream/GlobalState/TimerAbiTests.cs` | Port counter/frequency/delay/timer callback with conservative timing policy. |
| Hints/Events | `test/testautomation_hints.c`, `test/testautomation_events.c` | `Upstream/GlobalState/` | Port all deterministic hint/event/callback cases. |
| Audio Spine | `test/testautomation_audio.c`, `loopwave.c` | `Upstream/DummyDrivers/AudioAbiTests.cs`, `Upstream/Assets/WavAbiTests.cs` | Port dummy-safe open/close, conversion, and WAV load/free cases. |
| Video/Render Spine | `test/testautomation_video.c`, `test/testautomation_render.c` | `Upstream/DummyDrivers/` | Port dummy-safe window lifecycle and enabled software renderer cases. |

Each agent reports:

- upstream cases ported;
- upstream cases skipped with reason;
- generated binding blockers;
- new helper needs that were not part of the locked infrastructure;
- verification command and result.

Coordinator duties after each wave:

- review agent diffs for category/parallel/cleanup consistency;
- run full `dotnet test`;
- run slopwatch;
- update the inventory doc with counts and deferrals.

## Phase 3: P1 Headless Breadth Multi-Agent Wave

Dispatch after P0 is stable.

| Agent Slice | Upstream Source | Destination | Target |
| --- | --- | --- | --- |
| Math | `test/testautomation_math.c` | `Upstream/Pure/MathAbiTests.cs` | Port representative or full math matrix with clear floating-point tolerance policy. |
| Keyboard | `test/testautomation_keyboard.c` | `Upstream/DummyDrivers/KeyboardAbiTests.cs` | Port scancode/key-name/mod-state cases; gate text-input/focus if needed. |
| Mouse | `test/testautomation_mouse.c` | `Upstream/DummyDrivers/MouseAbiTests.cs` | Port cursor/state cases; defer focus/relative-mode cases unless dummy-stable. |
| Stdlib | `test/testautomation_stdlib.c` | `Upstream/GlobalState/StdlibAbiTests.cs` | Port environment/string/memory wrappers with cleanup. |
| Main/Subsystems | `test/testautomation_main.c`, `test/testautomation_subsystems.c` | `Upstream/GlobalState/SubsystemAbiTests.cs` | Port serialized lifecycle checks. |
| Log | `test/testautomation_log.c` | `Upstream/GlobalState/LogAbiTests.cs` | Port log priority/output function behavior with callback bridge. |
| Virtual Joystick | `test/testautomation_joystick.c` | `Upstream/CapabilityGated/JoystickAbiTests.cs` | Port virtual joystick path when SDL build supports it. |
| Atomic/Mutex/Thread Adjacent | mini tests such as `testatomic.c`, `testlock.c`, `testsem.c` | `Upstream/Pure/` or `Upstream/GlobalState/` | Port deterministic non-torture cases; defer thread creation until generator support exists. |

## Phase 4: P2 Capability-Gated Wave

Dispatch only after P0/P1 are stable.

| Area | Source | Rule |
| --- | --- | --- |
| Clipboard | `testautomation_clipboard.c` | Capability-gated per OS/session; no default failure when clipboard backend is absent. |
| Filesystem/Locale/Power/LoadSO | mini tests | Smoke-level assertions only; avoid exact environment values. |
| YUV/Iconv/Geometry | mini tests and assets | Port pure conversion or asset-backed paths only. |
| Additional Video/Mouse Focus | automation subsets | Add only after dummy or CI display behavior is proven stable. |

## Phase 5: Mechanical Header Coverage

This track may run in parallel with P0/P1 only if it does not require shared infrastructure changes.

| Guardrail | Scope |
| --- | --- |
| Symbol coverage | Generated extern declarations versus native exports where feasible. |
| Constant coverage | Enum, flag, and macro constants by header. |
| Layout coverage | Struct size, field offsets, fixed buffers, explicit-layout unions. |
| Callback coverage | Generated callback signatures and TFM-specific ABI bridge shape. |
| Macro policy coverage | Function-like macros classified as emitted helper, test helper, unsupported, or intentionally skipped. |

## Verification

Full verification after every infrastructure task and after each agent wave:

```pwsh
dotnet test --project spikes/binding-generators/clangsharp/tests/abi-tests/AbiTests.csproj -c Release
slopwatch analyze --fail-on warning --exclude "artifacts/**,external/**,vcpkg_installed/**,spikes/binding-generators/references/**,**/bin/**,**/obj/**"
git diff --check
```

Expected:

- automated tests pass across executable TFMs;
- skipped tests have explicit capability reasons;
- slopwatch reports zero issues;
- `git diff --check` reports no whitespace errors other than pre-existing line-ending normalization warnings already called out in status.

## Commit Gate

Before any commit, present:

- changed file summary;
- verification evidence;
- proposed commit message.

Do not commit until Deniz explicitly approves.
