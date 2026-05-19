# Playbook: Binding Generator Maintenance

**Status:** In progress — Stage 1 SDL2.Core generator is still landing.
**Last updated:** 2026-05-19

This playbook covers maintenance work that touches generated bindings, platform macro catalogs, CppAst/libclang versions, and the native hybrid-static inputs those bindings depend on.

It is intentionally conservative: generated bindings are public API, and the generator is tied to vcpkg-built native payloads.

## When To Use This

Use this playbook when:

- SDL headers change through a vcpkg baseline or SDL version bump.
- `PlatformCatalog` or parse-view macro definitions need review.
- CppAst / libclang / libClangSharp versions change.
- A new SDL satellite enters generation scope.
- SDL3 planning starts.
- Overlay triplets or ports change in a way that may affect exported symbols or headers.

## Current Stage 1 Contract

Stage 1 is SDL2.Core only.

Current production scope:

- Generator home: `build/_build/Targets/GenerateBindings/`
- Header input services: `build/_build/Targets/GenerateBindings/HeaderSet/`
- Parse catalog: `build/_build/Targets/GenerateBindings/Parsing/PlatformCatalog.cs`
- Persisted stamp contract: `build/_build/Data/BindingGeneration/GeneratedStamp.cs`
- Stamp repository: `build/_build/Data/BindingGeneration/GeneratedStampRepository.cs`

Current parse-view model:

- Neutral
- WindowsDesktop
- WinRT
- GDK
- Linux
- MacOS
- IOS
- Android

Backends such as X11, Wayland, KMSDRM, Cocoa, UIKit, Windows video, WinRT video, and Android video are bundled into their OS parse views for Stage 1. DirectFB, Vivante, MIR, and OS/2 remain explicit Stage 1 exclusions unless a real consumer need appears.

## Trio Pinning

The CppAst + libclang.runtime + libClangSharp.runtime trio moves as a coordinated set per [ADR-004](../decisions/2026-05-14-binding-autogen-toolchain.md). Mismatches surface as runtime stack overflows during AST visit (see [`research/binding-autogen-spike-findings.md`](../binding-autogen/research/binding-autogen-spike-findings.md) §7.2 friction #5).

| CppAst | libclang.runtime.* | libClangSharp.runtime.* | Status |
|---|---|---|---|
| 0.24.0 (2025-11-20) | 20.1.2 | 20.1.2 | Stage 1 pinned |
| 0.25+ | TBD | TBD | candidates — see "Post-Stage-1 Trio Revalidation" below |

`BindingGenerationRunner` asserts the resolved libclang version against this table at task entry and fails closed with an actionable diagnostic on mismatch (defensive against future package-restore drift or accidental floating-version regression).

Bump procedure:

1. Update all three versions in `Directory.Packages.props`.
2. Run `dotnet restore --force-evaluate` to refresh `packages.lock.json`.
3. Run `tools.cs generate-bindings` and inspect output for AST regressions.
4. Update the version assertion constant in `BindingGenerationRunner.cs` (`ExpectedLibclangMajorMinor`).
5. Update this table.
6. Update ADR-004 status notes if a major libclang version is involved.

## Post-Stage-1 Trio Revalidation

After the unified plan ships (Phases 1–3G) and the regeneration baseline is stable, attempt to bump the trio to absolute-latest available versions on NuGet. Maintainer comfort note (Deniz, 2026-05-15): "20.1.x içime sinmedi ama şimdilik concern'ümüz olmamalı". Trigger criteria and rationale: historical reference in [`../superpowers/specs/superseded/2026-05-15-binding-generator-local-output-loop-design.md`](../superpowers/specs/superseded/2026-05-15-binding-generator-local-output-loop-design.md) §10.1.

## Local Generation Loop

```pwsh
dotnet run --file tools.cs -- generate-bindings
dotnet run --file tools.cs -- generate-bindings --rebuild-image   # force docker layer cache invalidation
dotnet run --file tools.cs -- generate-bindings --no-cache        # skip vcpkg binary cache volume
```

Output lands at `artifacts/generated-bindings-preview/sdl2-core/` (gitignored). vcpkg binary cache lives in a named Docker volume; nuke it via `docker volume rm janset-vcpkg-cache` if a fresh full vcpkg install is needed.

Production-location flag-flip to `src/SDL2.<Family>/Generated/` is a Stage 1 Task 7 concern, not a maintenance operation.

## Synthetic Headers Maintenance

`build/_build/Targets/GenerateBindings/SyntheticHeaders/` ships **8 platform-specific stub headers** (5 mandatory + 3 defensive) used by the parser's `SystemIncludeFolders` because NuGet `libclang.runtime.linux-x64` ships no clang resource directory and the Linux container has no Windows / Apple / WinRT SDKs. **Pure type stand-ins — no business logic.** Full directory listing + per-stub purpose lives in the directory's `README.md`.

Maintenance triggers and procedure:

1. **SDL2 minor / patch release lands in vcpkg pin (e.g., 2.32.10 → 2.32.11+ or 2.34.x).** After a successful `tools.cs generate-bindings` run on the new pin, scan the SDL2 headers for new platform-specific `#include` directives:

   ```pwsh
   grep -rhn -E '^\s*#\s*include\s*<[A-Za-z_][A-Za-z0-9_.]*>' vcpkg_installed/x64-windows-hybrid/include/SDL2/*.h `
   | grep -oE '<[A-Za-z_][A-Za-z0-9_.]*>' | sort -u
   ```

   Compare against the existing stub set. If a new platform header (e.g., a hypothetical `<wintoast.h>` or `<TargetConditionalsExtra.h>`) appears AND it is referenced under a parse view we enable, add a stub. If it only appears under a driver macro we never set (`SDL_VIDEO_DRIVER_VITA` etc.), leave it alone — exclusion list / unset macro keeps it inert.

2. **New parse view added to `PlatformCatalog`.** If a new view enables a driver macro we previously left unset (DirectFB, Vivante, OS/2, etc.), the relevant defensive stub becomes load-bearing: verify its content covers the symbols SDL_syswm.h's union references for that driver.

3. **SDL3 binding scope arrives.** Discard SDL2 stubs and start fresh — SDL3 uses different platform macros (`SDL_PLATFORM_*`) and a different SysWM topology. SDL2 stubs do not carry over.

4. **Stub content shape.** Empty `#pragma once` is correct for headers whose only purpose is to make the `#include` line succeed. Typedef stand-ins (`typedef void* HWND;`, `typedef struct _IInspectable IInspectable;`) are appropriate when SDL declares struct fields or function parameters using the type name. Never add business logic, function bodies, or values to a stub — they ship as parser input only and are never compiled into binaries.

5. **Retirement criteria.** All stubs retire if (and only if) the project escalates from single-host Linux-canonical parsing to a multi-runner cross-OS pipeline (`bottlenoselabs/SDL3-cs` pattern documented in [`../binding-autogen/binding-autogen-strategy-brief.md`](../binding-autogen/binding-autogen-strategy-brief.md) §"True multi-OS extraction stays an escalation path"). Until then, treat the stub set as durable maintenance surface — not a workaround.

Peer baseline: ppy/SDL3-CS ships exactly **one** stub (`process.h`, ~80 bytes) for the non-Windows host case. We ship more because Stage 1 covers eight parse views vs. ppy's narrower platform matrix; we are still well below any "ad-hoc stub explosion" threshold (Silk.NET's contrast is to apt-install clang and resolve `/usr/lib/clang/<ver>/include` programmatically — no stubs, real headers).

## Header Set Resolver Exclusions Maintenance

`HeaderSetResolver.ExcludedHeaders` filters the `*.h` glob over the SDL2 vcpkg include directory. Once the unified plan Phase 2 lands the manifest-driven config, these categories live in `manifest.library_manifests[].binding_generation.header_set.excluded_headers` + `excluded_header_prefixes`. Current categories (see [`../superpowers/specs/superseded/2026-05-15-binding-generator-local-output-loop-design.md`](../superpowers/specs/superseded/2026-05-15-binding-generator-local-output-loop-design.md) §8.7 for the original full table):

- **Umbrella** — `SDL.h`. Pulls every other SDL2 header transitively.
- **Scaffolding** — `begin_code.h`, `close_code.h`. Pragma-pack pseudo-headers; `close_code.h` `#error`s standalone.
- **Satellite umbrellas** — `SDL_image.h`, `SDL_mixer.h`, `SDL_net.h`, `SDL_ttf.h`. Stage 1 is core-only; satellites have their own generation slices.
- **Satellite prefix** — `SDL2_*` (SDL2_gfx family).
- **Graphics-API convenience wrappers** — `SDL_opengl.h`, `SDL_opengles.h`, `SDL_opengles2.h`, `SDL_egl.h`. **Zero `extern DECLSPEC`** each — they only re-export system GL/EGL headers for users who want raw graphics-API calls alongside SDL. Excluding them sidesteps Apple OpenGLES + Windows windows.h transitive pulls.
- **GL / GLES sub-headers** — `SDL_opengl_glext.h`, `SDL_opengles2_*.h`. Sub-headers requiring umbrella's type setup; defensive even after umbrella exclusion.
- **Test scaffolding** — `SDL_test*` (prefix match). SDLTest_* helpers, not public binding surface.

Maintenance triggers and procedure:

1. **vcpkg pin bump / new SDL2 release.** Run:

   ```pwsh
   ls vcpkg_installed/x64-windows-hybrid/include/SDL2/ | sort
   ```

   Diff against the previous run. If a new header file appears:

   - Does it have `extern DECLSPEC` declarations? If zero, it is a convenience wrapper — add to the "Graphics-API convenience wrappers" category in `ExcludedHeaders`.
   - Does it look like a sub-header (depends on another header's macro state)? Try parsing it standalone via the generator's smoke; if it fails with "unknown type name" errors, add to the "GL/GLES sub-headers" category.
   - Otherwise it is real binding surface — leave unexcluded; expect new functions to surface in the next generation run.

2. **New satellite enters scope.** Add the satellite's umbrella header (`SDL_<library>.h`) to "Satellite umbrellas" before that satellite has its own generation slice; otherwise the satellite's `IMG_*`/`Mix_*`/etc. functions will leak into SDL2.Core's output.

3. **Test parity.** Every exclusion category should be exercised by `HeaderSetResolverTests` — keep `ResolveSdl2CoreHeaders_Should_Exclude_Non_Core_Headers` in sync.

## Macro Constants Maintenance

Macro constants are source-first. The generator collects object-like `SDL_*` macros from parsed SDL2.Core public headers, classifies non-API/header-control macros, emits safe string and numeric constants, and writes macro evidence into `parse-views.json`.

`SDL_config*.h` macros are source-visible but not public API. They describe SDL's build-time feature toggles for the parser host / configured target and must stay classified as non-API report evidence rather than emitted constants.

When adding macro parser or evaluator support, start with a real embedded header fixture under `build/_build.Tests/Fixtures/Data/GenerateBindings/`. Constructed `CppMacro` tests may cover policy branches, edge cases, and regression minimization, but they cannot be the only test for a new parser capability.

Use `binding_generation.required_constants` only for constants that are not visible through the per-header parse loop, such as current `SDL_INIT_*` values from excluded `SDL.h`. Use `binding_generation.macro_constants.excluded` only when a generated source-visible macro is intentionally not public binding API. Use `binding_generation.macro_constants.overrides` only when the source-visible value needs an explicit managed shape.

Unused manual includes, excludes, and overrides fail by default. If a stale-tolerant entry is necessary, it must carry `allow_stale: true` and a reason that explains why the entry remains in the manifest.

## Parser Options Audit Cadence

The per-family `parse_defines` + `clang_args` entries in `build/manifest.json library_manifests[].binding_generation` carry workarounds for SDL header / libclang interaction bugs. They are not free — each entry is a documented mechanism, and adding without justification or keeping after the underlying SDL behaviour changes both create drift. Before the unified plan Phase 2 lands, these values live as `BaseDefines` + `BaseAdditionalArguments` lists in `CppAstParseRunner.cs`; after Phase 2, they live in the manifest and the audit cadence below operates against the manifest entries.

Run this audit at every trio bump (CppAst / libclang.runtime / libClangSharp.runtime) and at every SDL2 minor release:

1. **Re-read each entry's rationale** (in `parse-time configuration surface (per family)` section below). Rationale documents the SDL header line + the symptom + the mechanism. If a rationale references SDL_stdinc.h line numbers, verify those lines still match the current SDL2 pin.
2. **Try removing one entry at a time, smoke, observe.** If the parse succeeds without the entry, retire it (delete from manifest + this playbook). If it fails with the symptom the rationale documents, keep it.
3. **Compare against ppy/SDL3-CS `SDL3-CS/SDL3/*.rsp` files.** Their per-header RSP files document equivalent workarounds for SDL3. Cross-check whether SDL2's stable equivalents still apply or have been superseded.

Document any change to this set with the symptom that motivates it. Bare-flag additions are not acceptable.

### Parse-time configuration surface (per family)

JSON in `build/manifest.json` cannot host paragraph-length rationale per entry. The rationale lives here, per-family. When a value changes in the manifest, the corresponding rationale below updates in the same slice.

#### sdl2-core

Verified working at SDL2 2.32.10 + CppAst 0.24.0 + libclang 20.1.2.

**`parse_defines`:**

- **`SDL_DECLSPEC=`** — Suppresses the platform-specific export attributes (`__declspec(dllexport)` on Windows, `__attribute__((visibility("default")))` on Linux) so CppAst sees plain function declarations. Without this, libclang parses the attribute syntax and CppAst's function records pick up annotations that don't translate to our emit shape.

- **`SDL_DISABLE_IMMINTRIN_H=1`**, **`SDL_DISABLE_MMINTRIN_H=1`**, **`SDL_DISABLE_XMMINTRIN_H=1`**, **`SDL_DISABLE_EMMINTRIN_H=1`**, **`SDL_DISABLE_PMMINTRIN_H=1`**, **`SDL_DISABLE_MM3DNOW_H=1`**, **`SDL_DISABLE_LSX_H=1`**, **`SDL_DISABLE_LASX_H=1`**, **`SDL_DISABLE_ARM_NEON_H=1`** — Short-circuit `SDL_cpuinfo.h:118-133`'s intrinsic-header includes (immintrin/mmintrin/xmmintrin/emmintrin/pmmintrin/mm3dnow/lsx/lasx/arm_neon). Without these defines, `SDL_cpuinfo.h` pulls GCC's intrinsic headers (via the Dockerfile's CPATH), whose `extern __inline` declarations of `_mm_pause` / `_mm_getcsr` / `__rdtsc` / `_mm_clflush` / `_mm_{l,m,s}fence` collide with libclang's internal builtin-function table and break the parse with `error: definition of builtin function ...`. The disable macros are the documented SDL2 escape hatch for binding generators — see SDL_cpuinfo.h lines 118-133 in any SDL2 release. Peer evidence: amerkoleci's Alimer.Bindings.SDL uses the SDL3 equivalents (SDL_PLATFORM_ANDROID/IOS/WINRT) for the same purpose.

**`clang_args`:**

- **`-fdeclspec`** — Enables `__declspec` parsing in C mode. `libegl-dev`'s `/usr/include/EGL/egl.h` declares its functions with `EGLAPI` which expands to `__declspec(dllimport/export)` on a Windows target — and SDL_egl.h is pulled by SDL_video.h under SDL_VIDEO_DRIVER_WINDOWS. Without `-fdeclspec`, clang's default C mode rejects every EGL function declaration in Windows-flavoured views. The flag is narrower than `-fms-extensions` (which enables the full MS dialect).

- **`-U__has_builtin`** — Forces `SDL_stdinc.h:127-131` `#ifdef __has_builtin` to false, which makes `_SDL_HAS_BUILTIN(x)` always expand to 0. That short-circuits the `#if _SDL_HAS_BUILTIN(__builtin_{mul,add}_overflow)` blocks at SDL_stdinc.h:822 and 853, so `_SDL_size_mul_overflow_builtin` and `_SDL_size_add_overflow_builtin` SDL_FORCE_INLINE helpers never enter the AST. Defense-in-depth complement to the translator's `CppFunctionFlags.Inline` filter; kills 2 of the 16 known SDL_FORCE_INLINE leaks at parse time before the AST filter ever sees them. Peer reference: ppy/SDL3-CS `SDL_stdinc.rsp` uses the exact same flag.

  Side-effect audit: three SDL2 sites reference `_SDL_HAS_BUILTIN` beyond the two SDL_stdinc.h declaration gates above — `SDL_assert.h:54` (selects `__builtin_debugtrap` for `SDL_TriggerBreakpoint` impl — macro body, no declaration impact), `SDL_endian.h:134/136/138` (selects `__builtin_bswap{16,32,64}` for `SDL_Swap*` SDL_FORCE_INLINE bodies — body content, helpers are already inline-filtered by the AST filter). No public-API declaration is affected by undefining the macro.

**Non-config baseline (set by CppAstParseRunner, not in manifest):**

- **`TargetSystem = "linux"`** — Overrides CppAst's `"windows"` ctor default (`CppParserOptions.cs`). Without this, libclang's triple becomes `x86_64-pc-windows-` even on a Linux host and SDL_stdinc.h:357 activates its `_MSC_VER` branch (`#include <sal.h>`), which fails inside the container. The `TargetSystem` field is a structural property of the parser, not a per-family knob — it stays in code.

**`required_functions`:**

5 hand-curated declarations recover the base API functions that survive only in `SDL.h` (`SDL_Init`, `SDL_InitSubSystem`, `SDL_QuitSubSystem`, `SDL_WasInit`, `SDL_Quit`). `SDL.h` is excluded from the per-header parse loop because it's the umbrella header — including it would collapse all ~50 SDL2 headers into one libclang translation unit, re-importing the intrinsic-header-collision and platform-conditioned-path-failure problems documented in `docs/binding-autogen/research/binding-autogen-spike-findings.md` §11 friction #7. Per-header parsing isolates failures: a parse error in any single header doesn't poison the rest of the surface. The 5 base functions cannot be reached through any non-`SDL.h` declaration site, so they're injected hand-curated and merged into the Neutral view by `CppAstToBindingModel.Translate`. Peer evidence: amerkoleci/Alimer.Bindings.SDL (CppAst SDL3) and ppy/SDL3-CS (ClangSharp SDL3) both exclude SDL3.h with the same rationale — this architectural choice transfers Stage 3 unchanged.

**`required_constants`:**

10 hand-curated declarations recover the `SDL_INIT_*` macros declared exclusively in `SDL.h` and not reachable through any other header (9 literals: `SDL_INIT_TIMER` / `SDL_INIT_AUDIO` / `SDL_INIT_VIDEO` / `SDL_INIT_JOYSTICK` / `SDL_INIT_HAPTIC` / `SDL_INIT_GAMECONTROLLER` / `SDL_INIT_EVENTS` / `SDL_INIT_SENSOR` / `SDL_INIT_NOPARACHUTE`; 1 computed compound: `SDL_INIT_EVERYTHING` = bitwise-OR of the individual flags except `NOPARACHUTE`). Same mechanism as `required_functions`: SDL.h umbrella exclusion + Neutral-view merge at translation time. The `kind` discriminator splits the emit shape — `Literal` constants emit as `public const <type> NAME = <value>;` (compile-time literal expression required by C#); `Computed` constants emit as `public static readonly <type> NAME = <value_expr>;` because C# `const` rejects non-literal expressions, and the compound `SDL_INIT_EVERYTHING` references other identifiers rather than embedding their values.

**Drift posture (2026-05-17).** SDL2 has been officially in maintenance mode since SDL 2.28.0 (June 2023) — bug-fix releases only, no new public-API additions. The `SDL_INIT_*` set has not changed since SDL 2.0.0 (2013), and the bug-fix release cadence has slowed sharply (SDL 2.32.10 shipped 2025-09-01; no 2.x release in the 8 months since at the time of writing). Drift risk for this hand-curated list is therefore essentially zero for the maintenance lifetime of SDL2. When a vcpkg SDL2 bump lands (rare), the per-bump diff-review step (the upstream-version-bump checklist below) catches any new entries before they ship missing. If drift detection becomes a real concern in practice, the natural extension is an `IBindingFamilyValidator` that text-greps `vcpkg-installed/.../SDL2/SDL.h` for `^#define SDL_INIT_` patterns and warns when manifest entries are missing — Option C in the unified-plan Phase 3B finding note. Stage 1 stays on Option A (manifest-only); Option C lands if and when needed.

#### sdl2-image / sdl2-mixer / sdl2-ttf / sdl2-gfx / sdl2-net

Satellite per-family rationales fill in at Stage 2 when each satellite's `binding_generation.enabled` flips to `true`. Most satellites can reuse `sdl2-core`'s `SDL_DECLSPEC=` and the GCC intrinsic-disable family; satellite-specific entries (e.g. `IMG_DISABLE_*` if SDL2_image grows analogous escape hatches in a future release) land here as they are introduced. The `required_constants` slot is available for satellite umbrella-only macros (e.g. `IMG_INIT_PNG` / `IMG_INIT_JPG` / `IMG_INIT_TIF` / `IMG_INIT_WEBP` declared in `SDL_image.h`, which is also excluded from the per-header loop) — same Literal/Computed split.

## Dynapi Manifest Cross-Check

SDL2 ships `src/dynapi/SDL2.exports` (Watcom-format text manifest, autogenerated by `gendynapi.pl`) listing every public export. Cross-platform single source of truth — Linux/macOS/Windows binaries all export the same symbol set per SDL2's dynapi design. The file lives inside vcpkg's `buildtrees/sdl2/src/<sha>.clean/src/dynapi/SDL2.exports` after `vcpkg install sdl2` completes a real source build.

**Coverage limit — name-only, not signature.** The Watcom DEF-file format records only `++'_NAME'.'SDL2.dll'.'NAME'` entries — function names exporting from the dynamic library, with no argument types, no parameter order, no return-shape. The dynapi cross-check therefore proves "the emit's symbol set ⊆ the SDL2 dynamic library's public exports" but cannot prove "the emit's parameter widths and return-type bytes match the C ABI." Stage 1 Task 3.5's 2026-05-16 production smoke exposed this gap concretely: the validator gate stayed green for 866 name-correct emits while several `MapTypedef`/`MapPrimitive` paths emitted wire-format-wrong primitive widths (e.g. `SDL_AudioFormat` typedef'd to `Uint16` emitting as `IntPtr`; Linux `long` mapped to 32-bit `int` instead of 64-bit). The Stage 2 Pack-stage `BindingSymbolExistenceValidator` inherits the same name-only limitation because binary symbol tables (PE/ELF/Mach-O dynamic exports via `dumpbin` / `nm` / `otool`) are byname dispatch maps — C signatures are not present in those tables. The natural place to catch wire-format ABI mismatch is the per-RID `consumer-smoke` matrix already in `release.yml`, by calling selected functions against the native library and asserting marshalling round-trips. Tracked as an outstanding gap in the binding-autogen strategy brief §"Symbol-existence validation guardrail" and the Stage 1 plan §"Post-Implementation Review Findings 2026-05-16 / Validator wire-format coverage gap".

**vcpkg behaviour to keep in mind:** vcpkg's binary cache stores only the compiled install payload — `installed/<triplet>/<port>/` contents. On a binary-cache hit vcpkg unpacks the cached archive directly into `installed/` and **skips source extraction entirely**, so `buildtrees/sdl2/src/` is empty. The dynapi manifest is reachable on a real (cache-miss) build and survives across subsequent runs only if `buildtrees/sdl2/src/` is preserved independently. Reach mechanisms:

- **Local container (binding-generator image):** vcpkg install runs at image build time as a dedicated Docker layer; the resulting `buildtrees/sdl2/src/` is baked into the image. Docker layer cache invalidates the layer when `vcpkg.json` / overlay tree / vcpkg submodule commit changes — matches vcpkg's own ABI key discipline.
- **CI (regenerate-bindings.yml — PSTH-J / future):** `.github/actions/vcpkg-setup/action.yml` multi-path `actions/cache@v5` covers both the binary cache directory and `external/vcpkg/buildtrees/sdl2/src/` (PSTH-I hardening). Same key already invalidates on `vcpkg.json` + overlays + submodule commit changes.
- **Host dev machine:** `tools.cs setup` (or any `vcpkg install sdl2` flow) populates `external/vcpkg/buildtrees/sdl2/src/` once; subsequent invocations preserve it. The same path is what `DynapiManifestRepository.LoadAsync` globs.

**Scope:** dynapi is SDL2-only. SDL2_image / SDL2_mixer / SDL2_ttf / SDL2_gfx / SDL2_net do not produce equivalent textual export manifests — their public surface is the public header (`extern DECLSPEC` declarations). Stage 2 satellite generators must consume a different validator strategy (header-derived expected-export sets, or harvested-binary symbol extraction via `BinaryClosureWalker`). The validator surface is therefore configurable per family from Stage 2 onward; the Stage 1 `IDynapiManifestRepository` + `IBindingPublicApiCoherenceValidator` pair is hard-wired to sdl2-core.

The generator runs a post-emit validator against this manifest:

- **Generator emit ∩ manifest** — correct public API entries (expected vast majority).
- **Generator emit \ manifest** — false-positive (sızıntı; inline helper, internal symbol, header parsing mishap). Fail-closed at Stage 1 GenerateBindings task.
- **Manifest \ generator emit** — false-negative (under-emit; header exclusion too aggressive, parse view missing a platform). Fail-closed.

The validator is **reused across three stages** (defense-in-depth):

| Stage | Trigger | Severity | Behaviour |
| --- | --- | --- | --- |
| Stage 1 `GenerateBindings` task | After `PreviewEmitter.Emit` | Fail-closed on false-positives; log false-negatives as warnings | Surfaces filter regressions and over-aggressive exclusions before they ship in `.generated-stamp` |
| Stage 2 `PreFlightCheck` task | Before native build work | Fail-closed in both directions | Catches stamp drift before vcpkg / harvest pipelines spin up |
| Stage 2 Pack stage `BindingSymbolExistenceValidator` | After Harvest, before Package | Fail-closed against the actual per-RID native binary (`dumpbin /exports` / `nm -D` / `nm -gU`) | Catches the `-fvisibility=hidden` / missing-`DECLSPEC` class of bug that ships natives missing symbols the bindings expect |

Maintenance triggers and procedure:

1. **vcpkg pin bump.** After regen, diff the new `buildtrees/sdl2/src/<sha>.clean/src/dynapi/SDL2.exports` against the previous pin's manifest. New `++'_X'.'SDL2.dll'.'X'` entries = new public surface to expect; removed entries = downstream surface change. The binding-generator container layer that runs `vcpkg install` re-runs automatically when `vcpkg.json` updates (Docker layer cache invalidation); CI multi-path cache (PSTH-I) re-fetches buildtrees on the same trigger.
2. **Validator drift.** If a manifest update produces false-positive flags that turn out to be legitimate new public API, the AST inline filter or header exclusion list is over-restrictive — investigate before silencing.
3. **Watcom-disabled lines.** The parser recognises `# ++'_NAME'.'SDL2.dll'.'NAME'` lines as ACTIVE exports — the Watcom DEF-file build path disables those entries, but the modern SDL2 dynamic library still exports them. If an entry needs to be excluded from validation, the fix lives in `Sdl2CoreGenerationConfig.ExcludedFunctionNames`, not in the parser.
4. **SDL3 transition.** SDL3 uses `gendynapi.py --dump`'s `sdl.json` (JSON, not Watcom .exports). Replace the manifest parser when SDL3 binding scope opens — the cross-check pattern stays the same.

Peer evidence: ppy/SDL3-CS `generate_bindings.py::check_generated_functions` runs the same kind of post-emit cross-check against SDL3's `sdl.json`. No peer uses raw `nm`/`dumpbin`/`otool` for the public-API source of truth — SDL itself ships the textual manifest, and binary tools are reserved for the Pack-stage check where they catch a different (visibility) bug class.

## Platform Macro Catalog Maintenance

The platform catalog is an explicit maintenance surface. This is normal for SDL binding generators: peer projects such as ppy/SDL3-CS also keep header and platform generation lists in code/scripts rather than deriving every platform pass automatically.

That does not make the list "set and forget." On every relevant SDL header update:

1. Inspect platform-related headers:
   - SDL2: `SDL_platform.h`, `SDL_config.h`, `SDL_stdinc.h`, `SDL_system.h`, `SDL_main.h`, `SDL_syswm.h`
   - SDL3: `SDL_platform_defines.h`, `SDL_platform.h`, `SDL_init.h`, `SDL_system.h`, and any header with `SDL_PLATFORM_*` conditionals
2. Search for platform and backend conditionals:
   - SDL2-style: `_WIN32`, `__APPLE__`, `__MACOSX__`, `__IPHONEOS__`, `__ANDROID__`, `linux`, `__linux__`, `SDL_VIDEO_DRIVER_*`
   - SDL3-style: `SDL_PLATFORM_*`
3. Compare found macros with `PlatformCatalog.AllPlatformMacros`.
4. Add or remove parse views only when public declarations or emitted attribution change.
5. Update `PlatformCatalogTests` with the intended parse-view set.
6. Regenerate bindings and review the generated diff.

Do not copy the SDL2 macro list into SDL3. SDL3 has a different platform macro contract and must get its own catalog when Phase 5 starts.

## Header Set And Stamp Maintenance

Header discovery and fingerprinting are target-local generation services, not Data-layer repositories. They read the vcpkg-installed input tree for one generation run.

The `.generated-stamp` is different: it is a persisted build-host contract and belongs under `Data/BindingGeneration/`.

When header inputs change:

- The header fingerprint should change.
- `.generated-stamp` should update.
- PreFlight coherence validation should eventually fail if generated output is stale.

The stamp must not contain wall-clock timestamps. It should record reproducible state: generator/toolchain versions, vcpkg state, manifest library version, header fingerprint, header count, and parse views.

## Hybrid-Static / Overlay Coupling

Binding maintenance is coupled to native build maintenance. When updating overlays or vcpkg state, also ask whether the binding surface changed.

Use `docs/playbook/overlay-management.md` for the overlay procedure, then apply these binding-specific checks:

- If an overlay port changes SDL feature flags, confirm affected public headers and exported symbols.
- If a hybrid triplet changes compiler flags, confirm symbol visibility assumptions still hold.
- If Linux/macOS visibility flags or version scripts change, check whether generated entry points still match exported symbols.
- If a satellite starts or stops exposing a function due to build options, the generated binding surface and future symbol-existence validation must reflect that.
- If a vcpkg baseline changes upstream port patches without changing SDL upstream version, still regenerate or validate `.generated-stamp`; header patches can change ABI-relevant declarations.

## SDL2 vs SDL3 Maintenance

SDL2 is relatively stable; SDL3 is not.

For SDL2:

- Patch updates usually require regeneration plus diff review.
- `SDL2_gfx` is effectively frozen but still needs export/symbol validation because it is third-party.
- `SDL_syswm.h` typed unions remain Stage 2 scope.

For SDL3:

- Treat platform macros as a new catalog, not an extension of SDL2.
- Expect `SDL_PLATFORM_*` usage.
- Re-evaluate legacy TFMs before copying SDL2's `netstandard2.0` / `net462` obligations.
- Re-evaluate bool, IO, and handle rules before emitting public API.

## Maintenance Checklist

Use this checklist when reviewing a generator or SDL update:

```markdown
- [ ] Relevant SDL platform headers inspected.
- [ ] New or removed platform/backend macros compared with PlatformCatalog.
- [ ] SDL2 and SDL3 macro models kept separate.
- [ ] Header-set fingerprint behavior still matches intended inputs.
- [ ] .generated-stamp updated without wall-clock fields.
- [ ] Overlay triplet/port changes reviewed for header/export impact.
- [ ] Generated source diff reviewed for public API changes.
- [ ] Build host tests passed.
- [ ] Package-first smoke path identified for the affected family.
- [ ] Docs updated if maintenance procedure changed.
- [ ] Trio version table reviewed for CppAst bump candidacy.
- [ ] Manual `tools.cs generate-bindings` smoke after maintenance change.
- [ ] SyntheticHeaders/ reviewed against new platform-specific #include references in SDL headers.
- [ ] HeaderSetResolver.ExcludedHeaders reviewed for new convenience wrappers, sub-headers, or satellite umbrellas.
- [ ] Parser BaseDefines + BaseAdditionalArguments audited; obsolete entries retired with documented justification.
- [ ] Dynapi manifest (SDL2.exports) refreshed for the new pin; post-emit cross-check validator output reviewed.
```

## Related Docs

- `docs/binding-autogen/binding-autogen-strategy-brief.md`
- `docs/superpowers/specs/2026-05-16-binding-generator-unified-design.md` (active unified design spec)
- `docs/superpowers/plans/2026-05-17-binding-generator-unified-plan.md` (active unified implementation plan)
- `docs/superpowers/specs/superseded/` + `docs/superpowers/plans/superseded/` (historical 2026-05-14/05-15 specs+plans)
- `docs/playbook/overlay-management.md`
- `docs/playbook/vcpkg-update.md`
- `docs/decisions/2026-05-05-target-centric-build-host.md`
- `docs/decisions/2026-05-12-build-host-data-layer.md`
- `docs/decisions/2026-05-14-binding-autogen-toolchain.md`
