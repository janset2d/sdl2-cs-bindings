# Binding Generator Testing Strategy

> **Status (2026-05-20):** Canonical testing strategy for generated SDL bindings. This document promotes the temporary testing research from `docs/binding-autogen/temp/testing/` into active docs. Update it when test layer boundaries, fixture policy, or smoke expansion sequencing changes.

## Purpose

Janset.SDL testing has multiple jobs that must stay separate:

- prove the vcpkg-built native feature set works;
- prove `.Native` package runtime payloads land and load correctly;
- prove managed binding ABI calls are wired correctly;
- prove public wrapper and friendly overload behavior;
- exercise real asset workflows for file/image/audio/font APIs;
- provide manual SDL apps for visual/audio/input confidence outside CI.

The strategy is not to port all upstream SDL tests. The strategy is to curate deterministic tests that catch Janset.SDL-specific risks: native dependency closure, NuGet asset layout, ABI wire correctness, resource lifetime, string/path marshalling, and wrapper behavior.

## Current Baseline

The repository already has the right broad shape:

- `build/_build.Tests` validates build-host and generator behavior without native runtime dependency.
- `tests/binding-compile-check/SDL2.Core.CompileCheck.csproj` compiles generated preview source across library target frameworks.
- `tests/smoke-tests/native-smoke` is a C/CMake harness that validates the native SDL2 stack directly.
- `tests/smoke-tests/package-smoke/PackageConsumer.Smoke` is a TUnit consumer project that restores real packages from a local feed and validates native asset landing plus representative managed calls.

The main gap is depth and layering, not existence. Current smoke coverage is mostly package-load/init oriented. It should grow into curated, real-asset, headless functional coverage while generator refactors stay protected by fast fixture and snapshot tests.

Root `assets/` is for repository documentation and NuGet package identity assets. Smoke fixtures should live under a test-owned root such as `tests/smoke-tests/assets/`.

## Layer Model

| Layer | Validates | Should Be Headless? | Uses Real Assets? |
| --- | --- | --- | --- |
| Build-host unit tests | Cake/generator/build policy | Yes | Synthetic fixtures mostly |
| Binding compile checks | Generated source compiles and has expected TFM shape | Yes | No |
| NativeSmoke | Native feature set and dependency closure | Yes by default | Yes |
| PackageConsumerSmoke | NuGet assets, native load, ABI calls, wrappers | Yes by default | Yes |
| Asset-backed headless functional smoke | Real file/media workflows without hardware | Yes | Yes |
| Manual diagnostics | Human-visible SDL behavior | No | Yes |
| Samples | Consumer education and scenario demos | Optional | Yes when useful |

## Build-Host Unit Tests

Current home:

- `build/_build.Tests/Unit/Targets/GenerateBindings/`
- `build/_build.Tests/Scenarios/GenerateBindings/`
- fixture headers under `build/_build.Tests/Fixtures/Data/GenerateBindings/`

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

Milestone 1 added Verify snapshots for the generator safety harness.

Snapshot baselines live here:

```text
build/_build.Tests/Unit/Targets/GenerateBindings/Snapshots/
```

Tests that own snapshot inputs live near the generator concept they cover, including:

```text
build/_build.Tests/Unit/Targets/GenerateBindings/Emitting/
build/_build.Tests/Unit/Targets/GenerateBindings/Translation/
build/_build.Tests/Unit/Targets/GenerateBindings/GeneratedPreview/
build/_build.Tests/Scenarios/GenerateBindings/
```

Rules:

- commit `*.verified.*` baselines;
- never commit `*.received.*` files;
- keep generated source snapshots line-ending normalized to `\n`;
- keep ordinary build-host test runs native-free except existing Linux-only CppAst fixture tests;
- run Linux-only semantic fixture snapshots on Linux or inside an exact-SDK Linux container when approving baselines;
- run generated-preview snapshots only after `dotnet run --file tools.cs -- generate-bindings` has produced `artifacts/generated-bindings-preview/sdl2-core/`;
- gate generated-preview snapshot execution with `JANSET_VERIFY_GENERATED_PREVIEW=1` so normal test runs never depend on Docker or generated artifacts;
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

## Binding Compile Checks

Current home:

- `tests/binding-compile-check/SDL2.Core.CompileCheck.csproj`

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

## Refactor Gate Mapping

Use the lightest gate that answers the question for the current milestone.

| Roadmap Milestone | Primary Gate | Secondary Gate |
| --- | --- | --- |
| M1 Safety Harness | Build-host unit/scenario tests, snapshots | Compile-check if preview exists |
| M2 Topology Refactor | Snapshot no-drift, unit/scenario tests | `generate-bindings` once at milestone end |
| M3 Profiles | Profile/unit fixtures, output no-drift | Satellite placeholder scenarios |
| M4 Raw Backends | Emitter tests, compile-check all TFMs | Snapshot intentional backend diff |
| M5 Public Low-Level | API snapshot, compile-check | Package smoke minimal generated calls |
| M6 Friendly Overloads | Pattern tests, compile-check | Package smoke string/path/resource pairs |
| M7 Production Flip | Project build, stamp tests | Package-first smoke |
| M8 Satellites | Family compile/package smoke | Duplicate core-type guard, symbol existence |
| M9 Smoke Expansion | NativeSmoke and PackageConsumerSmoke | Fixture provenance review |
| M10 SDL3 | SDL3 compile/package smoke | SDL3-specific policy review |

## Flakiness And Scope Rules

- Do not conflate generator correctness, C# compile correctness, native feature availability, NuGet package layout, and runtime ABI smoke.
- Do not make libclang/vcpkg/native setup mandatory for normal refactor unit tests.
- Do not port upstream SDL wholesale.
- Do not require real display/audio/input/network in CI gates.
- Do not use strict pixel assertions for lossy image formats.
- Do not use large branding assets as test fixtures.
- Do not make mixer MIDI or codec-specific fixtures hard gates until codec policy settles.
- Do not block generator architecture refactors on deep smoke expansion; use smoke expansion as milestone validation and later hardening.

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
