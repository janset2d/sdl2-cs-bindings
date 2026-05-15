# Playbook: Binding Generator Maintenance

**Status:** In progress — Stage 1 SDL2.Core generator is still landing.
**Last updated:** 2026-05-15

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
```

## Related Docs

- `docs/binding-autogen/binding-autogen-strategy-brief.md`
- `docs/superpowers/specs/2026-05-14-binding-generator-architecture-design.md`
- `docs/superpowers/plans/2026-05-14-sdl2-core-binding-generator-stage-1.md`
- `docs/playbook/overlay-management.md`
- `docs/playbook/vcpkg-update.md`
- `docs/decisions/2026-05-05-target-centric-build-host.md`
- `docs/decisions/2026-05-12-build-host-data-layer.md`
- `docs/decisions/2026-05-14-binding-autogen-toolchain.md`
