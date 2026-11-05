# Binding Generator Grand Roadmap

> **Status:** Canonical binding-generator roadmap. Layer-based forward milestones aligned with constitution Layer Contract. Sequence: Layer 1 raw ABI (done) → Layer 2 typed public API → Layer 3 friendly overloads → Production Flip → SysWM Layout + Satellite Sweep close → Smoke Expansion → SDL3 Extension.

## Goal

Land a durable generator foundation for:

- SDL2.Core generated public source;
- SDL2 satellites: Image, Mixer, Ttf, Gfx, and later Net;
- future SDL3 core and satellites;
- multi-TFM raw ABI backends;
- public typed low-level APIs;
- friendly string/span/ref/out overloads;
- package-first compile, smoke, oracle, and snapshot evidence.

The target is not a general-purpose binding-generator product. The target is a pragmatic SDL binding generator with clean seams, explicit family policy, and enough tests to refactor without gambling.

## What's Done — SDL2 Layer 1 Raw ABI Closure (2026-05-28)

Layer 1 raw ABI generation closed for SDL2 Core, Image, GFX, TTF, and Mixer
families on the active spike implementation. Full evidence:

- Generator runs under [`spikes/binding-generators/clangsharp/`](../../clangsharp/).
- Multi-TFM compile clean across `net10.0`, `net9.0`, `net8.0`, `netstandard2.0`, `net462`.
- Five-family oracle report at [`output/reports/oracle-evidence-clangsharp.md`](../../output/reports/oracle-evidence-clangsharp.md).
- Toolchain decision evidence at [`output/reports/iteration-2-comparison.md`](../../output/reports/iteration-2-comparison.md).
- RSP fix history at [`output/reports/clangsharp-failure-buckets.md`](../../output/reports/clangsharp-failure-buckets.md).
- Per-TFM ABI runtime smoke at `spikes/binding-generators/clangsharp/tests/abi-tests/`.

Closure includes Priority C semantic-ABI risks (`wchar_t*` opaque, C `long`
hybrid, `SDL_RWops` / `SDL_SysWMinfo` / `SDL_SysWMmsg` Pattern B quarantine,
tag/typedef canonicalization, `SDL_GUID` substitution); Foreign Type Boundary
Policy + BCL-Replaceable Helper Exclusion Policy + Cross-Assembly Pattern B
contract via `[assembly: DisableRuntimeMarshalling]`.

Mechanism and implementation evidence: [`binding-generator-implementation-notes.md`](binding-generator-implementation-notes.md).
Maintenance procedures: [`binding-generator-maintenance.md`](binding-generator-maintenance.md).

`git log --follow` is the archive for slice-by-slice closure detail.

## Layer 2 — Typed Public API Projection

**Goal:** Generate public low-level methods over the internal raw ABI while keeping extern declarations internal.

**References:** Constitution §"Layer Contract", API design extend-only guidance, PublicApiGenerator/Verify API snapshot pattern.

**Scope:**

- Add public methods on the manifest-driven public class, such as `SDL2.SDL`.
- Public methods call the internal raw ABI class.
- Typed handles, enums, structs, callbacks, and constants remain public generated types.
- Unsafe pointer signatures remain available when that is the honest low-level C shape.
- SDL2 bool-like raw `int` values convert to `bool` only when a rule proves the public method is predicate-like.
- Platform-only methods carry the same platform attribution as the raw ABI member.
- Introduce public API snapshot review before first public preview.

**Exit evidence:**

- Emitter tests prove public methods call internal raw methods.
- Compile-check passes across all library TFMs.
- Public API snapshot exists and is reviewed.
- Source inspection or automated check proves no public raw ABI container or effectively public raw extern leaks.

**Non-goals:**

- No SDL2-CS compatibility freeze.
- No `SafeHandle` / `IDisposable` owner wrappers.
- No callback lifetime helper layer beyond preserving low-level callback identity.

## Layer 3 — Friendly Overload Projection

**Goal:** Add ergonomic overloads through explicit, tested projection rules rather than ad hoc emitter special cases.

**References:** Constitution §"Layer Contract", Alimer overload evidence, `System.Memory` compatibility package posture.

**Baseline overload patterns:**

- `string` caller input encoded to null-terminated UTF-8.
- `ReadOnlySpan<byte>` for pre-encoded UTF-8.
- `ReadOnlySpan<T>` for counted input buffers when SDL does not retain the pointer.
- `Span<T>` for counted output buffers when SDL writes within caller-provided bounds.
- `out T` for required single-element output pointers.
- `ref T` for required single-element in/out pointers.
- Explicit fmt-only helpers for accepted variadic logging/formatting calls.

**Compatibility posture:**

- Use `System.Memory` or similar package dependencies for `netstandard2.0` / `net462` when the dependency is deliberate and package-smoke validated.
- Prefer stack allocation for small UTF-8 buffers and pooled arrays for larger hot-path buffers when the implementation pattern becomes performance-sensitive.
- Do not infer ownership or lifetime from pointer shape alone.

**Exit evidence:**

- RED/GREEN tests for every overload pattern.
- Compile-check passes across all library TFMs.
- Package-consumer smoke exercises representative string/path/resource pairs.
- Generated docs/reporting make allocation behavior and fmt-only variadic behavior visible.

**Non-goals:**

- No giant handwritten wrapper layer.
- No automatic lifetime-safe callback wrapper until callback pinning/lifetime policy is designed.
- No owner wrapper layer such as `SdlWindow : IDisposable` in this milestone.

## Production Flip — SDL2.Core Reproducibility

**Goal:** Move SDL2.Core from preview artifacts to committed production generated source and retire SDL2-CS production use for Core.

**References:** package-first release strategy, generated stamp contract.

**Scope:**

- Generate into `src/SDL2.Core/Generated/`.
- Commit generated `.g.cs` files.
- Remove the SDL2.Core production compile include for `external/sdl2-cs/src/SDL2.cs`.
- Keep `external/sdl2-cs` only as a reference oracle until remaining production uses retire.
- Add `.generated-stamp` with generator/toolchain version, vcpkg state, SDL library version, header-set fingerprint, header count, and parse views.
- Ensure the stamp has no wall-clock fields.
- Add stale-generated-output validation before expensive native/package work.
- Stale-output diagnostics name the family, stale field, and remediation command/workflow.
- Require regeneration from the same inputs to be diff-clean.
- Generated output uses stable LF line endings and deterministic ordering.

**Exit evidence:**

- `dotnet build src/SDL2.Core/SDL2.Core.csproj` succeeds.
- Compile-check is repointed or retired only if project build fully replaces its value.
- Package-first smoke passes from local package feed and covers at least init, quit, error retrieval, environment-permitting window create/destroy, and one deterministic callback path.
- No production source path uses `external/sdl2-cs/src/SDL2.cs` for SDL2.Core.

## SysWM Layout And Satellite Sweep Close

**Goal:** Close remaining SDL2 satellite production work: typed `SDL_SysWMinfo` / `SDL_SysWMmsg` projection, retire remaining `external/sdl2-cs` production usage, ship Pack-stage symbol-existence validator, and add SDL2_net.

Satellite Layer 1 raw ABI already closed for Image, GFX, TTF, and Mixer per the spike's 2026-05-28 closure (see What's Done). The remaining work in this layer scope is the typed-Layer-2 / friendly-Layer-3 / Production-Flip pass over those satellites plus the SysWM typed-union design, plus SDL2_net addition once its package family lands in `build/manifest.json`.

**References:** SDL_image upstream test model, satellite smoke strategy, package family manifest topology.

**Scope:**

- Implement full typed `SDL_SysWMinfo` and `SDL_SysWMmsg` only with platform layout proof.
- Add minimal forward-declaration stubs for platform handle types used by SysWM unions.
- Run Layer 2 + Layer 3 + Production Flip for each SDL2 satellite:
  - SDL2.Image;
  - SDL2.Mixer;
  - SDL2.Ttf;
  - SDL2.Gfx;
  - SDL2.Net after its package family enters `build/manifest.json`.
- Each satellite's `binding_generation` config moves from placeholder to full config in the same slice that enables Layer 2 typed projection for the family.
- Satellite outputs emit satellite-owned functions and types only.
- Core-owned `SDL_*` structs, handles, enums, callbacks, and constants are referenced from SDL2.Core, never redeclared.
- Add duplicate core-type guard with errors naming the satellite family, offending type, source header, and expected core-owned reference.
- Add symbol-existence validation after Harvest and before Package using platform-appropriate export tooling.
- Split package smoke per family when partial-scope smoke support lands.

**Exit evidence:**

- Every SDL2 managed family builds from generated source through Layer 2 + Layer 3.
- Full 7-RID native smoke and package-consumer smoke pass for generated SDL2 families.
- First public SDL2 preview is AST-generated, package-first, and not shaped by SDL2-CS public API inertia.
- `external/sdl2-cs` removed from all SDL2 production compile paths.

## Smoke And Asset-Backed Testing Expansion

**Goal:** Turn the testing strategy into durable CI/release confidence without importing upstream SDL wholesale.

**References:** [`testing-strategy.md`](testing-strategy.md), upstream SDL2 core tests, SDL_image test runner, SDL_mixer/SDL_ttf/SDL_net samples, SkiaSharp/LibGit2Sharp fixture patterns.

**Scope:**

- Create `tests/smoke-tests/assets/` with fixture policy and provenance.
- Replace root branding image usage in smoke tests with tiny generated fixtures.
- Add SDL2 core BMP/WAV/RWops real-file checks.
- Expand SDL_image load checks for mandatory image formats: PNG, JPEG, WebP, TIFF, AVIF, and optional QOI only if promoted.
- Add SDL_ttf real font open/render checks with a clean-license tiny font.
- Add SDL_gfx pixel mutation assertions.
- Add SDL_mixer real-file load checks after the LGPL-free codec contract and MIDI/Timidity story are settled.
- Keep manual diagnostic apps outside CI gates.

**Exit evidence:**

- CI smoke remains headless and deterministic.
- Smoke fixtures are tiny, committed, generated or explicitly licensed, and test-owned.
- Package-consumer smoke validates representative generated API calls, not only native load/init.

## SDL3 Extension

**Goal:** Add SDL3 as a real second consumer after SDL2 public-release progress and native packaging support exist.

**Gated on PD-7 completion** per [release-strategy](../../../../docs/release-strategy.md) §"Sequencing" Stage 3. SDL3 generation does not begin until SDL2 real-public-release (PD-7) ships; SDL3 vcpkg port + overlay triplet + transitive dependency closure work is its own substantial scope and must not block SDL2 v1.0 stable.

**References:** ADR-004, ppy/SDL3-CS, Alimer SDL3 evidence, SDL3-specific upstream headers and `sdl.json`.

**Scope:**

- Add SDL3 package families and native build support first.
- Introduce SDL3 generation only when SDL3 becomes a real second consumer.
- Promote shared generator code out of SDL2 target-local folders only when ADR-002 reuse criteria are met.
- Encode SDL3-specific ABI rules instead of copying SDL2 behavior:
  - 1-byte bool-like values;
  - `SDL_IOStream` replacing `SDL_RWops`;
  - SDL3 platform macro model;
  - SDL3 namespace and native library identity.
- Re-evaluate SDL3 TFM support instead of blindly copying SDL2's `netstandard2.0` / `net462` obligations.

**Exit evidence:**

- SDL3 Core, Image, Mixer, and Ttf generated output compiles and packages through the internal feed.
- SDL3 package-consumer smoke proves load and minimal calls per generated family across the supported RID/TFM matrix.
- SDL3-specific ABI decisions are captured in the constitution or a companion ADR before any public SDL3 preview.
