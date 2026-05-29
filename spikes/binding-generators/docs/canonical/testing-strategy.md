# Binding Generator Testing Strategy

## Purpose

Janset.SDL testing has multiple jobs that must stay separate:

- prove the vcpkg-built native feature set works;
- prove `.Native` package runtime payloads land and load correctly;
- prove managed binding ABI calls are wired correctly;
- prove public wrapper and friendly overload behavior;
- exercise real asset workflows for file/image/audio/font APIs;
- provide manual SDL apps for visual/audio/input confidence outside CI.

The strategy is not to blindly clone every upstream SDL test. The strategy is to curate deterministic tests that catch Janset.SDL-specific risks: native dependency closure, NuGet asset layout, ABI wire correctness, resource lifetime, string/path marshalling, and wrapper behavior. For generated raw ABI coverage, this still means porting most upstream SDL2 automation cases that are headless-safe, while adding Janset-owned header guardrails for generated surface breadth.

## Current Baseline

The repository already has the right broad shape:

- a build-host unit-test project for the binding generator validates generator behavior without native runtime dependency.
- a binding-compile-check project compiles generated preview source across library target frameworks.
- `tests/smoke-tests/native-smoke` is a C/CMake harness that validates the native SDL2 stack directly.
- `tests/smoke-tests/package-smoke/PackageConsumer.Smoke` is a TUnit consumer project that restores real packages from a local feed and validates native asset landing plus representative managed calls.

The main gap is depth and layering, not existence. Current smoke coverage is mostly package-load/init oriented. It should grow into curated, real-asset, headless functional coverage while generator refactors stay protected by fast fixture and snapshot tests.

Root `assets/` is for repository documentation and NuGet package identity assets. Smoke fixtures should live under a test-owned root such as `tests/smoke-tests/assets/`.

## Layer Model

| Layer | Validates | Should Be Headless? | Uses Real Assets? |
| --- | --- | --- | --- |
| Build-host unit tests | Generator and build policy | Yes | Synthetic fixtures mostly |
| Binding compile checks | Generated source compiles and has expected TFM shape | Yes | No |
| Generated raw ABI runtime tests | Generated raw binding ABI correctness, ownership, callbacks, layouts, and upstream behavior | Yes by default | Yes for file/media APIs |
| Mechanical header guardrails | Symbol, constant, layout, macro, and callback surface breadth | Yes | No |
| NativeSmoke | Native feature set and dependency closure | Yes by default | Yes |
| PackageConsumerSmoke | NuGet assets, native load, ABI calls, wrappers | Yes by default | Yes |
| Asset-backed headless functional smoke | Real file/media workflows without hardware | Yes | Yes |
| Manual diagnostics | Human-visible SDL behavior | No | Yes |
| Samples | Consumer education and scenario demos | Optional | Yes when useful |

## Build-Host Unit Tests

Purpose:

- validate manifest/config adapters, header resolution, parse views, semantic translation, validators, and emitters;
- pin generator behavior during refactors;
- keep normal feedback fast and native-free.

Recommended emphasis:

- prefer small embedded `.h` fixtures for parser/model behavior;
- use constructed objects only for narrow policy branches;
- snapshot complex generated output only after a baseline has been reviewed;
- do not require Docker/libclang/vcpkg for ordinary refactor unit tests.

### GenerateBindings Snapshot Conventions

The build-host unit-test project owns Verify snapshots for the generator safety harness. Tests that own snapshot inputs live near the generator concept they cover (emission, translation, generated-preview, end-to-end scenarios).

Rules:

- commit `*.verified.*` baselines;
- never commit `*.received.*` files;
- keep generated source snapshots line-ending normalized to `\n`;
- keep ordinary build-host test runs native-free;
- run host-restricted semantic fixture snapshots on the required host (or inside an exact-SDK Linux container) when approving baselines;
- gate generated-preview snapshot execution behind an opt-in env-var convention so normal test runs never depend on Docker or generated artifacts;
- for filtered runs that intentionally select only skipped tests on Windows, pass MTP `--ignore-exit-code 8`; full-suite runs still pass normally because non-skipped tests execute.

High-value characterization fixtures:

- C `long` / `unsigned long` and `CLong` / `CULong` guard behavior;
- SDL2 `SDL_bool` int-backed mapping;
- `wchar_t*` opaque pointer handling;
- duplicate opaque typedef/tag canonicalization;
- callbacks and function-pointer fields;
- fixed arrays and anonymous nested union/struct shapes;
- macro taxonomy, helper candidates, manual excludes/overrides, and stale tolerance;
- required functions/constants injected from excluded umbrella headers;
- platform-only declarations and Neutral subtraction.

### Refactor Safety Harness Pattern

Generator refactors land behind a layered safety harness. The harness is a refactor gate, not just snapshot housekeeping. Any successor implementation reproducing the binding generator must satisfy this pattern before topology changes:

- **Snapshot the deterministic generated file set** as a RED gate before any topology change. The snapshot pins the relative path, ordinal ordering, and content of every generated file the engine would write for a representative model. Failing snapshots block the refactor until the change is either reviewed or reverted.
- **Snapshot the orchestration output** (the fake-task-host equivalent: log lines, emitted file names, exit ordering) so that workflow-level refactors do not silently drop steps or reorder side effects. Pair with substitute parser/runner collaborators to keep these tests native-free.
- **Use small embedded `.h` fixtures** as the primary parser/model evidence. The parser/model layer must be driven by AST shape, not by name allowlists. Each policy seam (platform-conditioned declarations, manual macro include/exclude/override, function-like macro classification, opaque typedef/tag canonicalization, callback and function-pointer fields) gets a targeted fixture so policy regressions surface as snapshot diffs.
- **Opt-in real-preview inventory hashes** keep the end-to-end generated output honest without requiring Docker/libclang for ordinary refactor unit tests. Hash the per-family generated artifact tree and commit the inventory snapshot. The opt-in env-var gates execution so default test runs stay native-free; the gated lane runs after a real generator invocation produces fresh output.
- **Source-control hygiene**: commit `.verified.*` baselines; never commit `.received.*` files; add `.gitignore` rule for `*.received.*`; add `.gitattributes` `text eol=lf` for `*.verified.*` so cross-OS contributors do not churn line endings.

The four snapshot families (deterministic file-set, orchestration, embedded-fixture semantic model, opt-in real-preview hash inventory) compose into the refactor gate. Topology changes must hold all four invariant before merging.

## Binding Compile Checks

Purpose:

- prove generated C# compiles across supported library TFMs;
- catch missing generated files, bad syntax, duplicate declarations, bad conditional compilation, and public/raw layering accidents;
- provide fast feedback before runtime smoke.

Limits:

- compile checks do not prove ABI wire correctness;
- compile checks do not prove native asset landing;
- compile checks do not prove wrapper ownership or runtime lifetime behavior.

Future additions:

- synthetic generated-source samples for raw ABI backend split;
- public API snapshots once public low-level methods exist;
- checks that no public `[DllImport]` / `[LibraryImport]` declarations leak.

## Generated Raw ABI Runtime Tests

Current spike home:

- `spikes/binding-generators/clangsharp/tests/abi-tests/AbiTests.csproj`

Future production home after generator promotion:

- a root test project under `tests/` with a production name chosen during promotion.

Purpose:

- prove generated raw `SDLNative` calls marshal correctly against real native payloads;
- catch wrong integer widths, struct layouts, explicit-layout union shapes, callback signatures, UTF-8 string handling, path marshalling, and ownership mistakes;
- port most upstream SDL2 `testautomation_*.c` cases that are deterministic and headless-safe;
- add Janset-owned header-based tests where upstream coverage does not cover generated binding risks.

Ground rules:

- keep the spike in one test project until promotion;
- build reusable infrastructure before broad subsystem ports;
- keep default runs headless-safe;
- use dummy video/audio drivers and software renderers only behind explicit capability probes;
- use keyed TUnit non-parallel groups for SDL process-global state rather than serializing the whole assembly;
- defer real display, real speaker playback, hardware input, haptic, GL/GLES/Vulkan, syswm, and other manual/backend-sensitive tests unless a capability-gated lane is deliberately added;
- use TUnit/MTP `--treenode-filter` for category runs, not stale VSTest `--filter` examples.

### Coverage Strategy

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

Coverage targets after the spike feasibility review:

- P0 raw ABI spine: `70-100` stable tests;
- P1 headless breadth: additional `80-130` stable tests;
- P2 capability-gated/environment-sensitive: additional `20-40` tests;
- Default stable headless suite: approximately `260-280` tests before production promotion;
- Aspirational total with gated/backend-sensitive lanes: `300+` runtime tests;
- Mechanical header guardrails should pursue as-close-to-full generated surface coverage as practical for symbols, constants, layouts, callbacks, and macro policy.

Do not promise `300+` always-running headless tests until backend-sensitive SDL areas are proven stable across CI hosts. The suite may reach `300+` total through capability-gated tests and scheduled/full validation lanes.

### Test Categories

Use central category constants once infrastructure is in place. Categories are a different taxonomy from the layer model above: layers describe *where* a test runs in the gate hierarchy; categories describe *what kind* of behavior a test exercises within the runtime suite.

| Category | Meaning |
| --- | --- |
| `AbiSmoke` | Small host smoke that should always run (e.g., thread-ID dispatch checks). |
| `AbiUpstreamPort` | Behavior ported from upstream SDL2 tests. |
| `AbiHeaderCoverage` | Janset-owned header-based runtime or mechanical coverage. |
| `AbiMechanical` | Symbol/layout/constant/macro surface guardrails. |
| `AbiPure` | Headless tests that do not require real assets, dummy drivers, or SDL global state. |
| `AbiAssets` | Headless tests using copied real assets or temp files. |
| `AbiGlobalState` | Tests mutating SDL process-global state. |
| `AbiDummyDriver` | Headless tests requiring dummy video/audio or software renderer. |
| `AbiCapabilityGated` | Tests that run only when a probed runtime capability exists. |
| `SDL.<Subsystem>` | Subsystem tag such as `SDL.Rect`, `SDL.RWops`, `SDL.Video`, `SDL.Audio`. |

### High-value Upstream Sources

P0/P1 deterministic, headless-safe `testautomation_*.c` sources to port:

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
- `testautomation_joystick.c` virtual-joystick subset when available

### Known Generated-Surface Blockers

Tests requiring generator changes before they can be ported cleanly:

- `SDL_syswm` upstream automation remains deferred until `SDL_SysWMinfo` / `SDL_SysWMmsg` typed union generation and `SDL_GetWindowWMInfo` are available.
- Thread creation tests wait for explicit handling of `SDL_CreateThread` macro/REAL entrypoints.
- Common function-like SDL macros need either generated helpers or test-local helper policy before direct upstream ports are clean.
- `SDL_GetErrorMsg` is generated in the raw ABI, but local SDL2 upstream test sources do not exercise it; keep it deferred rather than inventing behavior outside upstream evidence.

## Raw ABI Test Suite Ground Rules

These rules apply specifically to the spike-local runtime ABI suite. They are the operating contract under which the suite scales toward the coverage targets above.

### Hard Boundaries

- Use one test project during the spike. Do not create separate test projects per SDL subsystem, stage, platform, or manual scenario.
- Do not move this suite into root `tests/` during the spike. Production naming and topology happen during generator promotion.
- Do not consume managed `Janset.SDL2.Core` packages in this suite. Keep the spike `ProjectReference` to the in-tree managed project.
- Resolve the native library through `Janset.SDL2.Core.Native` from the local feed.
- Treat any upstream-SDL clone under `references/` as a gitignored reference, not a runtime test dependency.
- Copy only selected stable assets into the slice when a test needs them, with provenance and license text.
- Manual diagnostics are out of scope until raw ABI headless infrastructure and coverage are stable.
- Broad subsystem porting starts only after reusable test infrastructure is in place and existing tests are normalized onto it.

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

### Upstream Adoption Metadata

Use upstream SDL2 tests as behavior oracles and traceability anchors. Port most `testautomation_*.c` cases that satisfy the headless-safe rule.

Every upstream-derived test carries source metadata, e.g.:

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

Before another broad subsystem port, stabilize reusable infrastructure and retrofit existing tests onto it. At minimum the suite needs:

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

### Test Project Layout

The spike keeps one project and splits by folders plus TUnit categories:

```text
spikes/binding-generators/clangsharp/tests/abi-tests/
├── AbiTests.csproj
├── ThreadIdAbiTests.cs
├── Infrastructure/
│   ├── Assets/
│   ├── Callbacks/
│   ├── Classification/
│   ├── Diagnostics/
│   ├── Macros/
│   ├── Runtime/
│   │   ├── Requirements/
│   │   └── Scopes/
│   └── Text/
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

Folder boundaries are navigation only. The assembly remains the spike test assembly, and runtime tests call `SDLNative` directly.

### Verification Commands

Default verification for the spike project:

```pwsh
dotnet test --project spikes/binding-generators/clangsharp/tests/abi-tests/AbiTests.csproj -c Release
slopwatch analyze --fail-on warning --exclude "artifacts/**,external/**,vcpkg_installed/**,spikes/binding-generators/references/**,**/bin/**,**/obj/**"
git diff --check
```

Category-specific runs should use TUnit/MTP tree filters after the exact expression is verified for this project. Do not document unverified filter commands.

## Mechanical Header Guardrails

Purpose:

- complement runtime tests with breadth checks across the generated raw surface;
- prevent a false sense of coverage from hand-authored runtime tests alone;
- make header-by-header omissions visible before packaging or release validation.

Recommended guardrails:

- generated extern declarations versus native exports where feasible;
- enum, flag, and macro constant snapshots by header;
- struct size, field offset, fixed-buffer, and explicit-layout union checks;
- callback signature and TFM-conditional ABI bridge checks;
- macro policy classification: emitted helper, test helper, unsupported, or intentionally skipped.

These guardrails belong near the generated raw ABI runtime suite during the spike and can move to the production test topology with it after generator promotion.

## NativeSmoke

Current home:

- `tests/smoke-tests/native-smoke`

Purpose:

- first line of defense for vcpkg-built native artifacts;
- prove the LGPL-free/hybrid-static native feature set works before `.Native` packages become the consumer-facing contract;
- validate native libraries directly, before C# binding behavior enters the picture.

Current good coverage:

- SDL init with dummy audio/video hints;
- SDL_image init bits for PNG, JPEG, WebP, TIFF, and AVIF;
- one PNG load;
- SDL_mixer init bits and decoder names;
- dummy audio driver registration and `Mix_OpenAudio`;
- SDL_ttf init;
- SDL_gfx primitive draw return code;
- optional SDL_net init.

Recommended expansion:

- test-owned tiny image/audio/font fixtures;
- SDL2 core BMP/WAV/RWops real-file checks;
- SDL_image real-file loads for mandatory formats;
- SDL_mixer real-file loads for agreed LGPL-free codecs;
- SDL_ttf real font open and render-to-surface;
- SDL_gfx pixel-change assertion;
- SDL_net local-only probes when stable.

NativeSmoke should not validate C# API shape, friendly overloads, real window hardware, real speaker output, physical input devices, or external network availability.

## PackageConsumerSmoke

Current home:

- `tests/smoke-tests/package-smoke/PackageConsumer.Smoke`

Purpose:

- validate the package-first consumer contract end to end;
- restore real managed and native packages from a local feed;
- prove native assets copy/extract into consumer output;
- resolve P/Invoke entry points;
- call representative generated APIs against native libraries;
- validate wrapper/friendly overload behavior where present.

This layer becomes especially important after generated bindings land. Export validators can prove a symbol exists, but only runtime calls through managed bindings can catch wrong integer widths, struct layout problems, string encoding bugs, delegate lifetime issues, and ownership mistakes.

Recommended expansion:

- real asset-backed tests;
- path/string marshalling through file APIs;
- surface/font/audio/image resource lifetime pairs;
- representative raw ABI plus public wrapper/friendly overload calls;
- per-family smoke projects once partial-scope smoke support lands.

High-value examples:

- `SDL_Init` / `SDL_Quit`;
- `SDL_GetError` / `SDL_SetError` string behavior;
- create/free surface;
- `SDL_RWFromFile` path and lifetime;
- one deterministic callback path after generated callbacks are promoted;
- BMP/PNG/JPG/WebP/TIFF/AVIF file loads as applicable;
- `IMG_Init`, `IMG_Load`, decoder list;
- `Mix_OpenAudio` with dummy audio, `Mix_LoadWAV`, decoder list;
- `TTF_OpenFont`, render to surface;
- SDL2_gfx primitive draw into a software renderer.

## Asset-Backed Headless Functional Smoke

This is a test style used by NativeSmoke and PackageConsumerSmoke, not necessarily a separate runner.

Rules:

- use real files for APIs whose normal contract is file/media based;
- keep fixtures tiny and deterministic;
- prefer generated fixtures with documented generation commands;
- commit fixture outputs so CI does not need media tooling;
- track licenses for imported fixtures;
- use strict pixel checks only for lossless formats;
- use dimensions, metadata, or tolerance for lossy formats;
- keep fixtures under test-owned folders, not root `assets/`.

Recommended fixture root:

```text
tests/smoke-tests/assets/
  README.md
  core/
  image/
  mixer/
  ttf/
```

Initial fixture candidates:

- `core/tiny.bmp`
- `core/tone.wav`
- `core/utf8.txt`
- `image/tiny.png`
- `image/tiny.jpg`
- `image/tiny.webp`
- `image/tiny.tif`
- `image/tiny.avif`
- optional `image/tiny.qoi`
- `mixer/tone.wav`
- codec-specific mixer files only after the mandatory codec set is final
- `ttf/test-font.ttf` plus license

## Manual Diagnostic Apps

Manual apps validate user-visible SDL behavior that CI should not gate.

Candidate homes:

- `samples/`
- `manual-tests/`
- both, if samples and internal release diagnostics need different expectations

Candidate apps:

- window and renderer lifecycle;
- image viewer;
- audio playback;
- mixer playback;
- font rendering;
- sprite/FPS stress;
- keyboard/mouse/controller inspector;
- haptic tester;
- optional GL/Vulkan experiments later;
- optional local SDL_net diagnostics.

Rules:

- keep them small and direct;
- do not block CI;
- document expected manual observations;
- prefer package references when validating released shape.

## Upstream Adoption Policy

Use upstream SDL tests as an oracle for behavior and risk, not as a direct import plan.

Adopt directly only when:

- the behavior maps cleanly to managed bindings;
- the test can be headless and deterministic;
- the fixture/license story is clean;
- the test catches a meaningful binding/package risk.

Prefer local curated ports for:

- SDL2 `testautomation` logic that maps to binding/package risks;
- SDL_image format load checks;
- SDL_mixer load/decoder checks;
- SDL_ttf render checks;
- SDL_gfx software-render pixel mutation;
- SDL_net local-only probes.

Keep upstream sample-style programs as manual diagnostic inspiration and future sample app inspiration.

### SDL2 Core Research Summary

The upstream SDL2 core `test/` tree is mixed:

- one substantial automated harness: `testautomation`;
- several standalone noninteractive CTest entries;
- many manual/sample/diagnostic programs that open windows, play audio, inspect input devices, or need real graphics/audio infrastructure;
- small real assets such as BMP, WAV, DAT, HEX, and UTF-8 text files.

Upstream automation feasibility inventory:

- approximately `318` declared `testautomation_*.c` cases;
- approximately `310` enabled upstream cases;
- approximately `260-280` strong headless/raw ABI candidates;
- approximately `20-35` additional backend-sensitive but possible candidates;
- approximately `35-55` cases to skip, defer, or treat as future manual/hardware diagnostics.

Good managed-binding candidates:

- init/quit and subsystem state tests;
- `SDL_GetError` / `SDL_SetError`;
- hints;
- timer basics;
- platform/version/endian/CPU queries;
- rect/math/pixel constants and value behavior;
- `SDL_RWops` against real files;
- surface creation, conversion, BMP load/save;
- software renderer tests under dummy video.

Good native-smoke candidates:

- dummy driver availability;
- audio/video/timer init;
- file-backed `SDL_LoadBMP` and `SDL_LoadWAV`;
- cheap thread/timer/platform probes.

Manual/sample candidates:

- keyboard/mouse/controller watch programs;
- haptic/rumble;
- real audio playback;
- GL/GLES/Vulkan;
- window manager, IME, drag/drop, URL, and display tests.

### SDL2 Satellite Research Summary

SDL_image has the strongest automated upstream model. Its `testimage` runner validates decoder availability, `IMG_Init` flags, format detection, load through convenience and RWops APIs, dimensions, optional save/load round trips, and pixel comparisons with tolerance for lossy formats.

SDL_mixer, SDL_ttf, and SDL_net mostly provide sample programs rather than stable headless test harnesses:

- SDL_mixer `playwave.c` and `playmus.c` are useful examples, but mandatory CI should prefer load/metadata checks over audible playback loops.
- SDL_ttf `showfont.c` and `glfont.c` are useful manual references; automated tests should use a small licensed font and render to a surface.
- SDL_net `showinterfaces`, `chat`, and `chatd` are sample references; mandatory CI should avoid external network and prefer local-only probes.
- SDL2_gfx is third-party; deterministic software rendering and pixel mutation are the right local test shape.

## Initial Coverage Matrix

| Family | NativeSmoke | PackageConsumerSmoke | Manual Diagnostics |
| --- | --- | --- | --- |
| SDL2 Core | Init dummy audio/video/timer; BMP/WAV/RWops load | Init/quit; version; error string; RWops/file path; surface create/free | Window lifecycle; keyboard/mouse; renderer clear |
| SDL2_image | Init required codecs; load tiny PNG/JPG/WebP/TIFF/AVIF as configured | Load same fixtures through C# APIs; assert dimensions and selected pixels | Image viewer |
| SDL2_mixer | Init codecs; dummy audio; decoder list; load tiny audio/music fixtures | Open dummy audio; load WAV plus selected codec files; decoder assertions | Audio/music playback |
| SDL2_ttf | Init; open test font; render text to surface | Open font path; render text; assert non-empty surface | Font render demo |
| SDL2_gfx | Draw primitive; assert pixel changed | Draw primitive via binding; assert return and pixel changed | Primitive/FPS demo |
| SDL2_net | Init; local address/interface probe | Local loopback after binding exists | Chat/showinterfaces app |

## Implementation Backlog

### Slice 1: Dedicated Smoke Fixture Root

Create `tests/smoke-tests/assets/README.md` plus `core/`, `image/`, `mixer/`, and `ttf/`. The README states generation, provenance, and license policy. Existing smoke projects should be able to copy from the new location.

### Slice 2: Tiny PNG Replacement

Replace the current large smoke PNG dependency with a tiny deterministic PNG fixture. NativeSmoke and PackageConsumerSmoke should load it and assert dimensions or selected pixels.

### Slice 3: SDL2 Core File Fixtures

Add `core/tiny.bmp`, `core/tone.wav`, and `core/utf8.txt`. Cover `SDL_LoadBMP`, `SDL_LoadWAV`, `SDL_RWFromFile`, path marshalling, and at least one create/free or open/close pair.

### Slice 4: SDL_image Required Format Set

Validate the actual mandatory image codec set, not just `IMG_Init` bits. Current feature hints point to PNG, JPEG, WebP, TIFF, and AVIF. Promote QOI only after confirming it is intended mandatory coverage.

### Slice 5: SDL_ttf Real Font Render

Use a small clean-license font. Cover `TTF_Init`, `TTF_OpenFont`, render-to-surface, surface validity, `TTF_CloseFont`, and `TTF_Quit`.

### Slice 6: SDL_gfx Pixel Assertions

Create a known software-render target, draw a primitive, and assert at least one expected pixel changed.

### Slice 7: SDL_mixer Real Audio/Music Fixtures

Start with WAV. Add FLAC/OGG/MP3/Opus/WavPack/MOD only after codec policy is final. MIDI waits for the Timidity/native MIDI packaging decision.

### Slice 8: Per-Family Package Smoke Split

Split package-consumer smoke by family once targeted package scopes are ready:

- `PackageConsumer.Core.Smoke`
- `PackageConsumer.Image.Smoke`
- `PackageConsumer.Mixer.Smoke`
- `PackageConsumer.Ttf.Smoke`
- `PackageConsumer.Gfx.Smoke`
- future `PackageConsumer.Net.Smoke`

The runner invokes only smoke projects whose families are in release scope.

### Slice 9: Manual Diagnostic Apps

Add human-visible diagnostics outside CI gates: window, image viewer, audio player, mixer player, font viewer, sprite stress, input inspector, and optional local network demo.

### Raw ABI Upstream Port Backlog

Multi-agent slice waves for the spike-local runtime ABI suite, dispatched only after the infrastructure-first prerequisites in the "Raw ABI Test Suite Ground Rules" section are satisfied. Each agent receives the exact upstream source file, generated binding file, destination test file, category rules, and verification command; agents do not edit shared infrastructure while working on independent subsystem slices.

#### Phase 2 — P0 ABI Spine Wave

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

#### Phase 3 — P1 Headless Breadth Wave

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

#### Phase 4 — P2 Capability-Gated Wave

| Area | Source | Rule |
| --- | --- | --- |
| Clipboard | `testautomation_clipboard.c` | Capability-gated per OS/session; no default failure when clipboard backend is absent. |
| Filesystem/Locale/Power/LoadSO | mini tests | Smoke-level assertions only; avoid exact environment values. |
| YUV/Iconv/Geometry | mini tests and assets | Port pure conversion or asset-backed paths only. |
| Additional Video/Mouse Focus | automation subsets | Add only after dummy or CI display behavior is proven stable. |

#### Phase 5 — Mechanical Header Coverage

This track may run in parallel with P0/P1 only if it does not require shared infrastructure changes.

| Guardrail | Scope |
| --- | --- |
| Symbol coverage | Generated extern declarations versus native exports where feasible. |
| Constant coverage | Enum, flag, and macro constants by header. |
| Layout coverage | Struct size, field offsets, fixed buffers, explicit-layout unions. |
| Callback coverage | Generated callback signatures and TFM-specific ABI bridge shape. |
| Macro policy coverage | Function-like macros classified as emitted helper, test helper, unsupported, or intentionally skipped. |

After each wave, the coordinator reviews agent diffs for category/parallel/cleanup consistency, runs full `dotnet test`, runs slopwatch, and updates inventory tracking with counts and deferrals.

## Refactor Gate Mapping

Use the lightest gate that answers the question for the current roadmap stage.

| Roadmap Stage | Primary Gate | Secondary Gate |
| --- | --- | --- |
| Layer 1 (Done) | Build-host unit/scenario tests, snapshots, oracle | Compile-check across TFMs |
| Layer 2 — Typed Public API Projection | API snapshot, compile-check | Package smoke minimal generated calls |
| Layer 3 — Friendly Overload Projection | Pattern tests, compile-check | Package smoke string/path/resource pairs |
| Production Flip | Project build, stamp tests | Package-first smoke |
| SysWM Layout And Satellite Sweep Close | Family compile/package smoke | Duplicate core-type guard, symbol existence |
| Smoke And Asset-Backed Testing Expansion | NativeSmoke and PackageConsumerSmoke | Fixture provenance review |
| SDL3 Extension | SDL3 compile/package smoke | SDL3-specific policy review |

## Flakiness And Scope Rules

- Do not conflate generator correctness, C# compile correctness, native feature availability, NuGet package layout, and runtime ABI smoke.
- Do not make libclang/vcpkg/native setup mandatory for normal refactor unit tests.
- Do not port upstream SDL wholesale.
- Do not require real display/audio/input/network in CI gates.
- Do not use strict pixel assertions for lossy image formats.
- Do not use large branding assets as test fixtures.
- Do not make mixer MIDI or codec-specific fixtures hard gates until codec policy settles.
- Do not block generator architecture refactors on deep smoke expansion; use smoke expansion as roadmap-stage validation and later hardening.

## Research Sources

Upstream SDL sources:

- SDL2 core test tree: https://github.com/libsdl-org/SDL/tree/SDL2/test
- SDL2 core test CMake wiring: https://github.com/libsdl-org/SDL/blob/SDL2/test/CMakeLists.txt
- SDL2 core test automation runner: https://github.com/libsdl-org/SDL/blob/SDL2/test/testautomation.c
- SDL2 core automation suite registry: https://github.com/libsdl-org/SDL/blob/SDL2/test/testautomation_suites.h
- SDL2 core test license note: https://github.com/libsdl-org/SDL/blob/SDL2/test/COPYING
- SDL_image 2.8 test tree: https://github.com/libsdl-org/SDL_image/tree/release-2.8.x/test
- SDL_image 2.8 test CMake wiring: https://github.com/libsdl-org/SDL_image/blob/release-2.8.x/test/CMakeLists.txt
- SDL_image 2.8 test runner: https://github.com/libsdl-org/SDL_image/blob/release-2.8.x/test/main.c
- SDL_image examples: https://github.com/libsdl-org/SDL_image/tree/release-2.8.x/examples
- SDL_mixer 2.8 samples: https://github.com/libsdl-org/SDL_mixer/blob/release-2.8.x/playwave.c and https://github.com/libsdl-org/SDL_mixer/blob/release-2.8.x/playmus.c
- SDL_ttf 2.24 samples: https://github.com/libsdl-org/SDL_ttf/blob/release-2.24.x/showfont.c and https://github.com/libsdl-org/SDL_ttf/blob/release-2.24.x/glfont.c
- SDL_net SDL2 examples: https://github.com/libsdl-org/SDL_net/tree/SDL2/examples

.NET / NuGet interop sources:

- Native library loading in .NET: https://learn.microsoft.com/en-us/dotnet/standard/native-interop/native-library-loading
- Native files in NuGet packages: https://learn.microsoft.com/en-us/nuget/create-packages/native-files-in-net-packages

Comparable .NET binding projects:

- SkiaSharp repository and tests: https://github.com/mono/SkiaSharp and https://github.com/mono/SkiaSharp/tree/main/tests
- SkiaSharp test content assets: https://github.com/mono/SkiaSharp/tree/main/tests/Content
- LibGit2Sharp tests and native load app: https://github.com/libgit2/libgit2sharp/tree/master/LibGit2Sharp.Tests and https://github.com/libgit2/libgit2sharp/tree/master/NativeLibraryLoadTestApp
- Vortice.Windows tests and samples: https://github.com/amerkoleci/Vortice.Windows/tree/main/tests and https://github.com/amerkoleci/Vortice.Windows/tree/main/samples
- Silk.NET repository: https://github.com/dotnet/Silk.NET
