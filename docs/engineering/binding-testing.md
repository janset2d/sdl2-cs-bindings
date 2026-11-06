# Binding Testing

Date: 2026-11-06

Keep generator correctness, compilation, native features, package layout, runtime ABI and friendly
behavior separate. Each needs evidence at its own boundary. Curate tests for binding/package risks;
do not import an upstream test suite wholesale. [ABI policy](../architecture/binding-abi-policy.md)
owns the intended surface; [quality gates](quality-gates.md) owns repository completion commands.

## Validation boundaries

| Boundary | Proof | Main home |
|---|---|---|
| Build-host units | Header/config/stamp contracts, output safeguards, validator integrity, LF and dynapi policies without native runtime | [Build.Tests](../../build/_build.Tests/) |
| Generated compilation | Syntax, files, duplicates, conditional backend shape and library TFMs | [Generated family projects](../../spikes/binding-generators/clangsharp/src/) |
| API review | Actual consumer surface and reviewed baseline | [ApiSnapshot](../../spikes/binding-generators/clangsharp/tests/api-snapshot/) |
| ABI runtime | Widths, layouts, pointers, ownership, strings and callbacks against real natives | [AbiTests](../../spikes/binding-generators/clangsharp/tests/abi-tests/) |
| Mechanical breadth | Names/exports, enum/flag/macro values, layouts and callback signatures | ABI suite and generator self-tests |
| Native smoke | Native dependency closure, codecs and native initialization without C# interop | [Native harness](../../tests/smoke-tests/native-smoke/) |
| Package consumer smoke | PackageReference restore, runtime landing/extraction, P/Invoke and friendly calls | [Package consumer](../../tests/smoke-tests/package-smoke/PackageConsumer.Smoke/) |
| Manual diagnostics | Visible display/audio/input/backend behavior | Explicit non-CI apps with documented observations |

Compilation does not prove ABI, package landing or lifetime. Export existence does not prove wire
shape. Test totals come from the run, not a copied baseline count. Record all actual skips.
[Developing](developing.md) owns feed preparation and cross-OS rehearsal.

## Unit and snapshot discipline

Use [testing style](testing-style.md) for build-host fixtures and filesystem boundaries. Ordinary
unit/refactor feedback stays native-free: no mandatory Docker, libclang or vcpkg. Retained host
contracts have direct tests; their dormant state is not proof that production consumes them.

Commit reviewed .verified baselines, never .received output. Normalize generated snapshots to LF.
Keep API snapshots independent of runtime ABI runs. A topology refactor needs a red safety gate
for the deterministic file set (relative path, ordinal order, content) and orchestration effects
(logs, emitted names and ordering), using substitute expensive boundaries where possible.
Small header fixtures drive parser/model policy: C long signed/unsigned inputs and returns,
boolean storage, wchar pointers, typedef/tag canonicalization, callbacks, fixed arrays/anonymous
unions, macro taxonomy/staleness, required umbrella declarations and platform subtraction.
Real generated-tree inventory hashes supplement these fixtures; they do not make the default
unit run require native setup. Add an opt-in lane only through an explicitly designed contract.

## ABI-suite boundary

Keep one runtime test project during the spike. It references the in-tree generated managed Core
project and restores the matching native package from the local feed. This is generator evidence,
not an alternative to the package-first consumer contract. Do not split by subsystem or move the
suite into production test topology before promotion. Upstream clones are reference-only and never
runtime test dependencies. Copy selected assets with provenance and license.

Default tests are deterministic and headless. Pure scalar/struct/string cases, tiny files, isolated
temp files and capability-probed dummy video/audio/software rendering can qualify. Real display,
speakers, hardware input/haptics/sensors, external network and real graphics backends do not.
Clipboard/focus/backend-sensitive tests need an explicitly proven capability lane, not a default
failure on a host without the feature. Reusable skip attributes make absence visible; early return
does not count as passing evidence.

Use native create/free and open/close pairs; verify out/ref data, UTF-8 paths, event unions, scalar
widths, flags and Modern/Compat calls. Callback tests prove calling convention, parameter shape,
rooting/registration lifetime and cleanup. Use deterministic state assertions rather than timing
as the oracle. A randomized race does not replace a controlled case.

### Infrastructure, categories and parallelism

Read exact category names from
[AbiCategories](../../spikes/binding-generators/clangsharp/tests/abi-tests/Infrastructure/Classification/AbiCategories.cs)
and scheduling keys from
[AbiParallelKeys](../../spikes/binding-generators/clangsharp/tests/abi-tests/Infrastructure/Classification/AbiParallelKeys.cs).
Category describes behavior;
validation boundary describes where the proof runs. Subsystem tags are a separate dimension.

Pure tests may run in parallel. Tests mutating shared SDL process state use the applicable shared
key; related hint/subsystem state must not be split into falsely independent groups. Do not serialize
the assembly to hide ordering bugs. Use the existing first-use/runtime/scopes helpers and deterministic
cleanup. Parameterized matrices suit math, rects, pixels, strings and constants; ownership, callbacks
and multi-call workflows often need bespoke tests.

Before broad ports, use the shared
[infrastructure](../../spikes/binding-generators/clangsharp/tests/abi-tests/Infrastructure/): assertions,
UTF-8/error helpers, asset catalog/temp directories, runtime probes, scopes, callback bridge and
driver skip attributes. Header/subsystem workers may run independently only when they need no shared
infrastructure edits. Their brief names exact upstream/generated inputs, destination, category/key
policy and verify command; the coordinator integrates and runs the full affected suite.

### Upstream traceability and actual layout

Every upstream-derived test records its source file and case using UpstreamSdlTest metadata;
the C# name describes behavior idiomatically. Port headless-safe behavior only when it maps to a
managed risk and its fixture/license story is clean. Add header-owned coverage where upstream
automation does not exercise generated names, widths, layouts, callback shapes or macro helpers.

The tracked runtime suite layout is:

```text
abi-tests/
  AbiTests.csproj
  PublicLayerAbiTests.cs
  ThreadIdAbiTests.cs
  Infrastructure/
    Assets/
    Classification/
    SdlAssert.cs
    SdlHelpers.cs
    Sdl/
      Callbacks/
      Requirements/
      Scopes/
      SdlError.cs
      SdlFirstUseInitialization.cs
      SdlMacro.cs
      SdlRuntimeProbe.cs
      SdlUtf8.cs
  Upstream/
    Assets/
    DummyDrivers/
    GlobalState/
    Pure/
  TestAssets/sdl2-upstream/
```

Folders are navigation, not separate assemblies. Read actual TFMs and references from
[AbiTests.csproj](../../spikes/binding-generators/clangsharp/tests/abi-tests/AbiTests.csproj).
Run the MTP project form:

```pwsh
dotnet test --project spikes/binding-generators/clangsharp/tests/abi-tests/AbiTests.csproj -c Release
```

Focused runs use verified TUnit `--treenode-filter` expressions, not VSTest `--filter`.
[Maintenance](binding-generator-maintenance.md) owns self-tests, generation and baked-image runtime
commands. [Quality gates](quality-gates.md) owns slopwatch and diff checks.

## Native and package smoke

Native smoke checks vcpkg-built features before managed bindings enter the picture. Consumer smoke
restores actual packages and exercises extraction/copy, entry points, signatures and friendly calls.
Keep both headless. Decoder availability and init bits are useful but not a substitute for loading
the intended formats through real files.

| Family | Representative proof |
|---|---|
| Core | Init/quit, version/error, UTF-8 RWops path, BMP/WAV load, surface create/free |
| Image | Intended decoder/init bits and tiny PNG/JPEG/WebP/TIFF/AVIF loads; dimensions and appropriate pixel checks |
| Mixer | Decoder/init behavior, dummy audio open/close, WAV and agreed codec loads; no audible playback requirement |
| Ttf | Clean-license tiny font open, render to nonempty surface, close/free |
| Gfx | Primitive call plus actual software-target pixel mutation |
| Net | Local-only init/loopback when bindings and packaging support it; no external network dependency |

These are test-design targets, not a claim that every case is implemented. The
[roadmap](../ROADMAP.md) owns depth/fixture gaps and capability work. Package scope is governed by
[packaging canon](../architecture/packaging-and-release.md#release-scope-requirements),
not by creating private smoke arrangements for individual changes.

## Asset policy and manual diagnostics

Keep fixtures tiny, deterministic and test-owned. Prefer generated files with reproducible commands,
but commit outputs so CI does not need media tools. Imported fixtures retain source ref, license
and provenance. Root branding assets are not smoke fixtures. Use per-test temp directories with
cleanup for writes. Lossless formats can use exact pixels; lossy formats need dimensions/metadata
or justified tolerance. Codec/MIDI-specific hard gates require a settled native runtime contract.

Manual apps are small, direct and nonblocking for CI. Document expected observations for window,
renderer, image/font viewing, audio/mixer playback, sprite/FPS stress, input/haptic inspection and
optional graphics or local-network experiments. Use packages when validating released shape.
Samples teach consumers; manual diagnostics gather human evidence. Do not confuse either with
automated integrity gates.

## Promotion/refactor evidence

Use the lightest gate that answers the risk, without dropping another boundary's protection:
generator/self-test and header fixtures for translation; compile/API snapshots for binding shape;
pattern tests plus string/path/resource smoke for friendly APIs; project build/stamp tests and
package-first smoke for production promotion; platform layout, duplicate Core-type and symbol proof
for satellites/SysWM; native/package smoke plus provenance for fixture expansion; independent SDL3
policy and runtime proof for that family.

[Generation](../architecture/binding-generation.md#promotion-evidence-gates) owns the complete promotion
gate list. [Output review](binding-output-review.md) owns upstream/peer reconciliation. Do not block a
native-free refactor on unrelated deep-smoke expansion, or declare a public release proven by compile
success alone.
