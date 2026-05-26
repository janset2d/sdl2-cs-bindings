# Iteration 2 — Config Surface Unification: Design Spec

**Date:** 2026-05-26  
**Status:** Draft / proposed — awaiting approval  
**Branch:** `spike/binding-autogen-sdl2-gfx`  
**Depends on:** Item 1 (per-library generation infrastructure) — closed  
**Unblocks:** Items 2–5 (satellite Layer 1 expansion)  
**Design label:** Moderate-plus unification with rich audit-preserving config

## 1. Problem Statement

Post-Item 1, the ClangSharp + Roslyn postprocess binding generator consumes configuration from approximately **25 sources** spread across Python code, C# code, external JSON files, RSP files, header-list text files, and csproj patterns. This dispersion grew incrementally through Priority A/B/C and Item 1 slices without deliberate classification. Three concrete risks:

1. **Drift between source types.** Family identity facts (`namespace`, `raw_class`, `include_subdir`) live in Python's `FAMILY_CONFIG` dict while the C# postprocess maintains parallel copies in `oracle.cs FamilyConfigs`, `Program.cs ResolveFamilyFromOutputDir`, `UniformOpaqueFamilyIdentity.cs`, and `UniformOpaqueOwnerMode.cs`. Same facts, 4 copies — guaranteed to drift.
2. **Discoverability for new agents.** Answering "where does family X config live?" requires inspecting 6+ files across two languages.
3. **Expansion tax.** Items 3–5 will add 3 more satellite families — each requires touching 5+ files. Centralizing now avoids multiplying the mess.

### 1.1 Non-Goals

- **Do not touch `build/manifest.json`.** That is Roadmap M7 production flip territory.
- **Family RSP files lose `--methodClassName` and `--libraryPath`.** These two lines are removed from family RSPs (`sdl2-core.rsp`, `sdl2-image.rsp`). The orchestrator derives them from config `raw_class` and `library_name`. Per-header RSPs and `base.rsp` are unchanged. This eliminates the only duplication between config and RSP content.
- **Do not change per-header RSP lookup logic.** ClangSharp discovers per-header `.rsp` by header name convention — unchanged.
- **Do not change csproj files or the `.slnx`.** Project structure is orthogonally template-able but not part of this pass.
- **Do not activate TTF/Mixer/GFX.** `selected_families("all")` stays `["core", "image"]`.
- **Do not add new features.** This is consolidation-only.
- **Config is seeded mechanically from current files/code**, not hand-copied. Implementation extracts existing data programmatically, then diff-reviews against known-good sources.

## 2. Verified Config Surface Inventory

Three-agent parallel sweep (2026-05-26) verified the 18-source roadmap inventory and discovered 7+ additional items. The canonical count is 25.

### 2.1 Current state (will be absorbed into new config)

| # | Source | Location | Disposition |
|---|--------|----------|-------------|
| 1 | Core required surface | `scope/sdl2-core-sdlh-required.json` | Absorb into `required_surface` |
| 2 | Opaque-handle roster | `clangsharp/policy/opaque-handle-roster.json` | Absorb into `opaque_handles` (full metadata preserved) |
| 3 | Flags-enum roster | `clangsharp/policy/flags-enum-roster.json` | Absorb into `flags_enums` (full metadata preserved) |
| 4 | Core header list | `scope/sdl2-core.headers.txt` | Absorb into `headers[]` (ordered objects) |
| 5 | Image header list | `scope/sdl2-image.headers.txt` | Absorb into `headers[]` (ordered objects) |
| 6 | `FAMILY_CONFIG` dict | `generate_bindings.py:373-409` | Merge into family sections |
| 7 | `PLATFORM_SENSITIVE_HEADERS` | `generate_bindings.py:516-525` | Merge into `platform_sensitive_headers` |
| 8 | `ALL_PLATFORM_MACROS` list | `generate_bindings.py:434-462` | Move to `global.all_platform_macros` |
| 9 | `SDL2_PLATFORM_VIEWS` | `generate_bindings.py:471-508` | Move to `global.platform_views` |
| 10 | `ClongDualDispatchRewriter.AffectedMethodNames` | `ClongDualDispatchRewriter.cs:28-39` | Move to per-family `clong_methods` |
| 11 | Family dir→ID mapping | `Program.cs:239-263` | Derive from config, eliminate hardcoding |
| 12 | Family dir→ID mapping (dup) | `UniformOpaqueFamilyIdentity.cs:26-33` | Derive from config, eliminate hardcoding |
| 13 | Namespace→ID mapping | `UniformOpaqueFamilyIdentity.cs:36-45` | Derive from config, eliminate hardcoding |
| 14 | Owner directory detection | `UniformOpaqueOwnerMode.cs:76` | Replace with config-driven `owner_mode` |
| 15 | `owner_mode_for_family()` | `generate_bindings.py:974-975` | Move to per-family `owner_mode` |
| 16 | Platform views (C# copy) | `PlatformDeltaPostProcessor.cs:10-31` | Derive from `global.platform_views` |
| 17 | `oracle.cs FamilyConfigs` | `oracle.cs:162-215` | Derive from config, eliminate parallel copy |
| 18 | `oracle.cs KnownFamilies` | `oracle.cs:220-227` | Derive from config families list |
| 19 | Postprocess default namespace fallback | `Program.cs:103,216-217` | Derive from config family section |

### 2.2 Not moving (stays as-is)

| Item | Location | Reason |
|------|----------|--------|
| `CODEGEN_CONFIG` (compat/modern flags) | `generate_bindings.py:420-423` | Internal mechanics, cross-cutting constant |
| `PRODUCTION_CODEGEN_PASSES` | `generate_bindings.py:425` | Codegen flow, not per-family fact |
| `selected_families("all")` | `generate_bindings.py:951-957` | CLI flow control, activation state |
| `postprocess_steps_for_codegen()` | `generate_bindings.py:989-1008` | Pipeline orchestration, business logic |
| `vcpkg_triplet` | `generate_bindings.py` (CLI arg) | Runtime environment, not versionable config |
| ClangSharp tool version | `.config/dotnet-tools.json` | Already a tool manifest |
| Per-header RSP **contents** | `rsp/per-header/*.rsp` | Filesystem data consumed by ClangSharp |
| `ClongDualDispatchRewriter` emit logic | `.cs` (CLong/CULong, RuntimeInformation dispatch) | Policy mechanism ("how"), not scope ("which") |
| C long native type classification | `.cs` (`"SDL_threadID"`, `"unsigned long"`, `"long"` semantics) | ABI knowledge — code owns what these mean and how they emit. Config owns only which methods are affected. See Constitution §"Configurable Scope Vs Policy Mechanism". |
| `FlagsAttributeRewriter` suffix rule | `.cs` (`EndsWith("Flags")`) | Policy mechanism, invariant across families |
| `GuidSubstitutionRewriter` substitution logic | `.cs` (`SDL_GUID` → `System.Guid`) | Policy mechanism, applied universally |
| `OpaqueHandleEmitRewriter` struct template | `.cs` (Pattern B shape, file header) | Policy mechanism, invariant across families |
| `StripVarargsRewriter` / `DllImportToLibraryImportRewriter` | `.cs` | Policy mechanism, invariant across families |
| Platform header shims | `shims/platform-headers/*.h` | Parse aids, not generation config |
| `vcpkg.json` + overlay triplets | Repo root | Native build input |
| csproj TFM patterns | `src/Janset.SDL2.*/` | Build infrastructure, separate templatization concern |
| `Directory.Build.props` / `.slnx` | Spike root | Build infrastructure |

## 3. Classification Table

| Category | Config owns (data — "which") | Code owns (mechanism — "how") |
|----------|----------------------------|------------------------------|
| **Family identity** | `namespace`, `raw_class`, `include_subdir`, `library_name`, `rsp`, `project_dir`, `headers[]` | How these drive ClangSharp invocations |
| **Opaque handles** | Full roster objects: `auto_detect_well_known[]`, `force_opaque_exceptions[]`, `excluded_candidates[]` (each with audit metadata); `owner_mode` | Pattern B template, auto-detect heuristic, 3-position rewrite, drift watchdog |
| **Flags enums** | Full roster objects: `allow_list[]` (each with audit metadata) | Suffix rule (`EndsWith("Flags")`), attribute emission, idempotency |
| **C long** | `clong_methods[]` (per-family — which methods are affected) | CLong/CULong emit, Compat RuntimeInformation dispatch, Win32/Unix64 cast expressions, native type name classification (`"SDL_threadID"`, `"unsigned long"`, `"long"`) |
| **Platform views** | View names, OS strings, define lists, cross-contamination macro inventory | Dedup algorithm, `[SupportedOSPlatform]` guard, `#if` wrapping |
| **Required surface** | `required_functions[]`, `required_constants[]` | SDL.h umbrella parser, C-to-C# type rendering |
| **Postprocess identity** | Family project directory naming, namespace mapping (derived from config) | Pipeline step order, rewriter dispatch, CLI mode routing |

## 4. Chosen Design: Single `family-config.json`

**File:** `spikes/binding-generators/clangsharp/config/family-config.json`

**Schema version:** `1.0`

**Format:** Pure JSON (no JSONC). Comments are not needed — audit metadata fields (`reason`, `source`, `notes`, `last_audited`) serve the same role and are tool-validatable. Both Python (`json.load`) and C# (`System.Text.Json`) read pure JSON with zero dependency overhead.

**Consumers:** Python `generate_bindings.py` + C# `postprocess/Program.cs` + C# `oracle.cs` — all read the same file, each picks its relevant sections.

### 4.1 Schema

*THIS SAMPLE IS ILLUSTRATIVE ONLY — DO NOT USE AS REFERENCE DATA.*
The schema below shows structure and field naming. Values are representative, not exact. Platform view defines, all_platform_macros, header lists, and roster entries shown here are NOT guaranteed to match current live data. The implementation MUST mechanically seed the config from live files/code (see §8.3 parity validation) and MUST run a multi-agent cross-check before claiming accuracy. If you are copying data from this sample into a config file, you are doing it wrong — use the mechanical seed script instead.

```json
{
  "schema_version": "1.0",
  "last_modified": "2026-05-26",

  "global": {
    "platform_views": [
      {
        "name": "WindowsDesktop",
        "supported_os": "windows",
        "defines": ["_WIN32", "WIN32"]
      },
      {
        "name": "Linux",
        "supported_os": "linux",
        "defines": ["__linux__", "__LINUX__", "SDL_VIDEO_DRIVER_X11", "SDL_VIDEO_DRIVER_WAYLAND"]
      },
      {
        "name": "MacOS",
        "supported_os": "macos",
        "defines": ["__APPLE__", "__MACOSX__", "TARGET_CPU_X86_64", "SDL_VIDEO_DRIVER_COCOA"]
      },
      {
        "name": "WinRT",
        "supported_os": "windows10.0.10240.0",
        "defines": ["_WIN32", "WIN32", "WINAPI_FAMILY=WINAPI_FAMILY_APP"]
      },
      {
        "name": "GDK",
        "supported_os": "windows",
        "defines": ["_WIN32", "WIN32", "__GDK__"]
      },
      {
        "name": "IOS",
        "supported_os": "ios",
        "defines": ["__APPLE__", "__IPHONEOS__", "TARGET_OS_IOS", "SDL_VIDEO_DRIVER_UIKIT"]
      },
      {
        "name": "Android",
        "supported_os": "android",
        "defines": ["__ANDROID__"]
      }
    ],
    "all_platform_macros": [
      "_WIN32", "WIN32", "WINAPI_FAMILY", "__WINRT__", "__GDK__",
      "__linux__", "__LINUX__", "SDL_VIDEO_DRIVER_X11", "SDL_VIDEO_DRIVER_WAYLAND",
      "__APPLE__", "__MACOSX__", "__IPHONEOS__", "TARGET_CPU_X86_64", "TARGET_OS_IOS",
      "__ANDROID__", "__arm__", "__aarch64__",
      "SDL_VIDEO_DRIVER_COCOA", "SDL_VIDEO_DRIVER_UIKIT"
    ],
    "base_rsp": "rsp/base.rsp"
  },

  "families": {
    "core": {
      "namespace": "SDL2",
      "library_version": "2.32.10",
      "raw_class": "SDLNative",
      "include_subdir": "SDL2",
      "library_name": "SDL2",
      "rsp": "rsp/sdl2-core.rsp",
      "project_dir": "Janset.SDL2.Core",
      "owner_mode": true,
      "headers": [
        { "order": 10, "name": "SDL_assert.h" },
        { "order": 20, "name": "SDL_atomic.h" },
        { "order": 30, "name": "SDL_audio.h" },
        { "order": 40, "name": "SDL_bits.h" },
        { "order": 50, "name": "SDL_blendmode.h" },
        { "order": 60, "name": "SDL_clipboard.h" },
        { "order": 70, "name": "SDL_cpuinfo.h" },
        { "order": 80, "name": "SDL_endian.h" },
        { "order": 90, "name": "SDL_error.h" },
        { "order": 100, "name": "SDL_events.h" },
        { "order": 110, "name": "SDL_filesystem.h" },
        { "order": 120, "name": "SDL_gamecontroller.h" },
        { "order": 130, "name": "SDL_gesture.h" },
        { "order": 140, "name": "SDL_haptic.h" },
        { "order": 150, "name": "SDL_hidapi.h" },
        { "order": 160, "name": "SDL_hints.h" },
        { "order": 170, "name": "SDL_joystick.h" },
        { "order": 180, "name": "SDL_keyboard.h" },
        { "order": 190, "name": "SDL_keycode.h" },
        { "order": 200, "name": "SDL_loadso.h" },
        { "order": 210, "name": "SDL_locale.h" },
        { "order": 220, "name": "SDL_log.h" },
        { "order": 230, "name": "SDL_main.h" },
        { "order": 240, "name": "SDL_messagebox.h" },
        { "order": 250, "name": "SDL_metal.h" },
        { "order": 260, "name": "SDL_misc.h" },
        { "order": 270, "name": "SDL_mouse.h" },
        { "order": 280, "name": "SDL_mutex.h" },
        { "order": 290, "name": "SDL_pixels.h" },
        { "order": 300, "name": "SDL_platform.h" },
        { "order": 310, "name": "SDL_power.h" },
        { "order": 320, "name": "SDL_rect.h" },
        { "order": 330, "name": "SDL_render.h" },
        { "order": 340, "name": "SDL_rwops.h" },
        { "order": 350, "name": "SDL_scancode.h" },
        { "order": 360, "name": "SDL_sensor.h" },
        { "order": 370, "name": "SDL_shape.h" },
        { "order": 380, "name": "SDL_stdinc.h" },
        { "order": 390, "name": "SDL_surface.h" },
        { "order": 400, "name": "SDL_system.h" },
        { "order": 410, "name": "SDL_thread.h" },
        { "order": 420, "name": "SDL_timer.h" },
        { "order": 430, "name": "SDL_touch.h" },
        { "order": 440, "name": "SDL_version.h" },
        { "order": 450, "name": "SDL_video.h" },
        { "order": 460, "name": "SDL_vulkan.h" }
      ],
      "platform_sensitive_headers": ["SDL_main.h", "SDL_system.h"],
      "required_surface": {
        "functions": [
          "SDL_Init", "SDL_InitSubSystem", "SDL_Quit",
          "SDL_QuitSubSystem", "SDL_WasInit"
        ],
        "constants": [
          "SDL_INIT_TIMER", "SDL_INIT_AUDIO", "SDL_INIT_VIDEO",
          "SDL_INIT_JOYSTICK", "SDL_INIT_HAPTIC",
          "SDL_INIT_GAMECONTROLLER", "SDL_INIT_EVENTS",
          "SDL_INIT_SENSOR", "SDL_INIT_NOPARACHUTE",
          "SDL_INIT_EVERYTHING"
        ]
      },
      "opaque_handles": {
        "last_audited": "2026-05-26",
        "source": "SDL2 release headers (vcpkg_installed/x64-windows-hybrid/include/SDL2/) + wiki.libsdl.org cross-validation + ClangSharp Modern output empty-struct verification",
        "auto_detect_well_known": [
          {
            "name": "SDL_Window",
            "header": "SDL_video.h",
            "header_decl": "typedef struct SDL_Window SDL_Window;",
            "wiki_url": "https://wiki.libsdl.org/SDL2/SDL_Window",
            "wiki_evidence": "The opaque type used to identify a window."
          },
          {
            "name": "SDL_Renderer",
            "header": "SDL_render.h",
            "header_decl": "typedef struct SDL_Renderer SDL_Renderer;",
            "wiki_url": "https://wiki.libsdl.org/SDL2/SDL_Renderer",
            "wiki_evidence": "A structure representing rendering state"
          },
          {
            "name": "SDL_Texture",
            "header": "SDL_render.h",
            "header_decl": "typedef struct SDL_Texture SDL_Texture;",
            "wiki_url": "https://wiki.libsdl.org/SDL2/SDL_Texture",
            "wiki_evidence": "An efficient driver-specific representation of pixel data"
          },
          {
            "name": "SDL_AudioStream",
            "header": "SDL_audio.h",
            "header_decl": "typedef struct _SDL_AudioStream SDL_AudioStream;",
            "wiki_url": "https://wiki.libsdl.org/SDL2/SDL_AudioStream",
            "wiki_evidence": "An opaque structure that buffers, converts, resamples, and generally streams audio data."
          },
          {
            "name": "SDL_GameController",
            "header": "SDL_gamecontroller.h",
            "header_decl": "typedef struct _SDL_GameController SDL_GameController;",
            "wiki_url": "https://wiki.libsdl.org/SDL2/SDL_GameController",
            "wiki_evidence": "not_found"
          },
          {
            "name": "SDL_Joystick",
            "header": "SDL_joystick.h",
            "header_decl": "typedef struct _SDL_Joystick SDL_Joystick;",
            "wiki_url": "https://wiki.libsdl.org/SDL2/SDL_Joystick",
            "wiki_evidence": "not_found"
          },
          {
            "name": "SDL_Haptic",
            "header": "SDL_haptic.h",
            "header_decl": "typedef struct _SDL_Haptic SDL_Haptic;",
            "wiki_url": "https://wiki.libsdl.org/SDL2/SDL_Haptic",
            "wiki_evidence": "not_found"
          },
          {
            "name": "SDL_Sensor",
            "header": "SDL_sensor.h",
            "header_decl": "typedef struct _SDL_Sensor SDL_Sensor;",
            "wiki_url": "https://wiki.libsdl.org/SDL2/SDL_Sensor",
            "wiki_evidence": "not_found"
          },
          {
            "name": "SDL_Cursor",
            "header": "SDL_mouse.h",
            "header_decl": "typedef struct SDL_Cursor SDL_Cursor;   /**< Implementation dependent */",
            "wiki_url": "https://wiki.libsdl.org/SDL2/SDL_Cursor",
            "wiki_evidence": "not_found"
          },
          {
            "name": "SDL_Thread",
            "header": "SDL_thread.h",
            "header_decl": "typedef struct SDL_Thread SDL_Thread;",
            "wiki_url": "https://wiki.libsdl.org/SDL2/SDL_Thread",
            "wiki_evidence": "not_found",
            "wiki_corroboration": "SDL_CreateThread documents its return value as 'an opaque pointer to the new thread object'."
          },
          {
            "name": "SDL_mutex",
            "header": "SDL_mutex.h",
            "header_decl": "typedef struct SDL_mutex SDL_mutex;",
            "wiki_url": "https://wiki.libsdl.org/SDL2/SDL_mutex",
            "wiki_evidence": "not_found"
          },
          {
            "name": "SDL_sem",
            "header": "SDL_mutex.h",
            "header_decl": "typedef struct SDL_semaphore SDL_sem;",
            "header_note": "Struct tag is SDL_semaphore but the typedef is SDL_sem.",
            "wiki_url": "https://wiki.libsdl.org/SDL2/SDL_sem",
            "wiki_evidence": "not_found"
          },
          {
            "name": "SDL_cond",
            "header": "SDL_mutex.h",
            "header_decl": "typedef struct SDL_cond SDL_cond;",
            "wiki_url": "https://wiki.libsdl.org/SDL2/SDL_cond",
            "wiki_evidence": "not_found"
          },
          {
            "name": "SDL_hid_device",
            "header": "SDL_hidapi.h",
            "header_decl": "typedef struct SDL_hid_device_ SDL_hid_device; /**< opaque hidapi structure */",
            "header_note": "Struct tag is SDL_hid_device_ (with trailing underscore) but the typedef is SDL_hid_device.",
            "wiki_url": "https://wiki.libsdl.org/SDL2/SDL_hid_device",
            "wiki_evidence": "not_found",
            "wiki_corroboration": "Header comment on the typedef itself reads '/**< opaque hidapi structure */'."
          }
        ],
        "force_opaque_exceptions": [
          {
            "name": "SDL_RWops",
            "header": "SDL_rwops.h",
            "reason": "Header declares full struct body with function-pointer fields plus subclass state. Field offsets unsafe to expose — actual layout depends on runtime subclass. Constitution mandates opaque emit."
          },
          {
            "name": "SDL_SysWMinfo",
            "header": "SDL_syswm.h",
            "reason": "Platform-dependent union via #if defined(SDL_VIDEO_DRIVER_*). Cross-platform binding cannot safely expose platform-conditional union members."
          },
          {
            "name": "SDL_SysWMmsg",
            "header": "SDL_syswm.h",
            "reason": "Platform-dependent union mirroring SDL_SysWMinfo layout variance. Same opaque rationale."
          }
        ],
        "excluded_candidates": [
          {
            "name": "SDL_BlitMap",
            "reason": "Field-scope only (SDL_Surface.map private field). No public API consumes or returns it. Header tags it as opaque + Private. SDL3 removed it entirely."
          },
          {
            "name": "SDL_iconv_t",
            "reason": "BCL-Replaceable Helper Exclusion Policy: SDL_iconv_* family excluded. No remaining function consumes this type."
          },
          {
            "name": "SDL_GLContext",
            "header": "SDL_video.h",
            "header_decl": "typedef void *SDL_GLContext;",
            "reason": "Pointer typedef (void*), not a struct type. Pattern B does not apply."
          },
          {
            "name": "SDL_MetalView",
            "header": "SDL_metal.h",
            "header_decl": "typedef void *SDL_MetalView;",
            "reason": "Pointer typedef (void*), not a struct type."
          },
          {
            "name": "SDL_TimerID",
            "header": "SDL_timer.h",
            "header_decl": "typedef int SDL_TimerID;",
            "reason": "Value-type alias for int. Not a handle."
          },
          {
            "name": "SDL_TLSID",
            "header": "SDL_thread.h",
            "header_decl": "typedef unsigned int SDL_TLSID;",
            "reason": "Value-type alias for unsigned int. Not a handle."
          },
          {
            "name": "SDL_threadID",
            "header": "SDL_thread.h",
            "header_decl": "typedef unsigned long SDL_threadID;",
            "reason": "Value-type alias for unsigned long. Handled separately by ClongDualDispatchRewriter."
          },
          {
            "name": "SDL_AudioDeviceID",
            "header": "SDL_audio.h",
            "header_decl": "typedef Uint32 SDL_AudioDeviceID;",
            "reason": "Value-type alias for Uint32. Not a handle."
          },
          {
            "name": "SDL_JoystickID",
            "header": "SDL_joystick.h",
            "header_decl": "typedef Sint32 SDL_JoystickID;",
            "reason": "Value-type alias for Sint32. Not a handle (numeric instance identifier)."
          },
          {
            "name": "SDL_SensorID",
            "header": "SDL_sensor.h",
            "header_decl": "typedef Sint32 SDL_SensorID;",
            "reason": "Value-type alias for Sint32. Not a handle."
          },
          {
            "name": "SDL_TouchID",
            "header": "SDL_touch.h",
            "header_decl": "typedef Sint64 SDL_TouchID;",
            "reason": "Value-type alias for Sint64. Not a handle."
          },
          {
            "name": "SDL_FingerID",
            "header": "SDL_touch.h",
            "header_decl": "typedef Sint64 SDL_FingerID;",
            "reason": "Value-type alias for Sint64. Not a handle."
          },
          {
            "name": "SDL_GestureID",
            "header": "SDL_gesture.h",
            "header_decl": "typedef Sint64 SDL_GestureID;",
            "reason": "Value-type alias for Sint64. Not a handle."
          }
        ]
      },
      "flags_enums": {
        "last_audited": "2026-05-26",
        "allow_list": [
          {
            "name": "SDL_Keymod",
            "reason": "Composed alias values (KMOD_CTRL = KMOD_LCTRL | KMOD_RCTRL). Suffix rule does not match."
          },
          {
            "name": "SDL_BlendMode",
            "reason": "Bitmask enum per SDL documentation. Suffix rule does not match."
          },
          {
            "name": "SDL_GLcontextFlag",
            "reason": "Bitmask values (SDL_GL_CONTEXT_DEBUG_FLAG = 0x0001, etc.). Suffix rule does not match."
          },
          {
            "name": "SDL_RendererFlip",
            "reason": "Bitmask values (SDL_FLIP_NONE/HORIZONTAL/VERTICAL). Suffix rule does not match."
          },
          {
            "name": "SDL_TextureModulate",
            "reason": "Bitmask values. Suffix rule does not match."
          }
        ]
      },
      "clong_methods": ["SDL_ThreadID", "SDL_GetThreadID"]
    },

    "image": {
      "namespace": "SDL2.Image",
      "library_version": "2.8.8",
      "raw_class": "SDL_imageNative",
      "include_subdir": "SDL2",
      "library_name": "SDL2_image",
      "rsp": "rsp/sdl2-image.rsp",
      "project_dir": "Janset.SDL2.Image",
      "owner_mode": false,
      "headers": [
        { "order": 10, "name": "SDL_image.h" }
      ],
      "platform_sensitive_headers": [],
      "required_surface": null,
      "opaque_handles": {
        "last_audited": "2026-05-26",
        "source": "SDL2_image release headers + ClangSharp Modern output verification; Image owns no local opaque handles at this audit date.",
        "auto_detect_well_known": [],
        "force_opaque_exceptions": [],
        "excluded_candidates": [
          {
            "name": "IMG_Animation",
            "header": "SDL_image.h",
            "header_decl": "typedef struct IMG_Animation { int w, h; int count; SDL_Surface **frames; int *delays; } IMG_Animation;",
            "reason": "SDL2_image-owned public full struct with exposed POD-like fields and explicit animation payload pointers. Not an opaque forward declaration."
          }
        ]
      },
      "flags_enums": {
        "last_audited": "2026-05-26",
        "allow_list": []
      },
      "clong_methods": []
    },

    "ttf": {
      "namespace": "SDL2.Ttf",
      "library_version": "2.24.0",
      "raw_class": "SDL_ttfNative",
      "include_subdir": "SDL2",
      "library_name": "SDL2_ttf",
      "rsp": "rsp/sdl2-ttf.rsp",
      "project_dir": "Janset.SDL2.Ttf",
      "owner_mode": true,
      "headers": [
        { "order": 10, "name": "SDL_ttf.h" }
      ],
      "platform_sensitive_headers": [],
      "required_surface": null,
      "opaque_handles": {
        "last_audited": "2026-05-26",
        "source": "SDL2_ttf release headers + wiki.libsdl.org cross-validation + ClangSharp Modern output empty-struct verification",
        "auto_detect_well_known": [
          {
            "name": "TTF_Font",
            "header": "SDL_ttf.h",
            "header_decl": "typedef struct TTF_Font TTF_Font;",
            "wiki_url": "https://wiki.libsdl.org/SDL2_ttf/TTF_Font",
            "wiki_evidence": "The internal structure containing font information. Remarks: Opaque data!",
            "header_note": "Satellite-owned Pattern B handle. Emitted into namespace SDL2.Ttf by uniform-opaque owner mode."
          }
        ],
        "force_opaque_exceptions": [],
        "excluded_candidates": []
      },
      "flags_enums": {
        "last_audited": "2026-05-26",
        "allow_list": []
      },
      "clong_methods": [
        "TTF_OpenFontIndex", "TTF_OpenFontIndexRW",
        "TTF_OpenFontIndexDPI", "TTF_OpenFontIndexDPIRW",
        "TTF_FontFaces"
      ]
    },

    "mixer": {
      "namespace": "SDL2.Mixer",
      "library_version": "2.8.1",
      "raw_class": "SDL_mixerNative",
      "include_subdir": "SDL2",
      "library_name": "SDL2_mixer",
      "rsp": "rsp/sdl2-mixer.rsp",
      "project_dir": "Janset.SDL2.Mixer",
      "owner_mode": true,
      "headers": [
        { "order": 10, "name": "SDL_mixer.h" }
      ],
      "platform_sensitive_headers": [],
      "required_surface": null,
      "opaque_handles": {
        "last_audited": "2026-05-26",
        "source": "SDL2_mixer release headers + wiki.libsdl.org cross-validation + ClangSharp Modern output empty-struct verification",
        "auto_detect_well_known": [
          {
            "name": "Mix_Music",
            "header": "SDL_mixer.h",
            "header_decl": "typedef struct Mix_Music Mix_Music;",
            "wiki_url": "https://wiki.libsdl.org/SDL2_mixer/Mix_Music",
            "wiki_evidence": "The internal format for a music chunk interpreted via codecs",
            "header_note": "Satellite-owned Pattern B handle. Emitted into namespace SDL2.Mixer by uniform-opaque owner mode."
          }
        ],
        "force_opaque_exceptions": [],
        "excluded_candidates": [
          {
            "name": "Mix_Chunk",
            "header": "SDL_mixer.h",
            "header_decl": "typedef struct Mix_Chunk { int allocated; Uint8 *abuf; Uint32 alen; Uint8 volume; } Mix_Chunk;",
            "reason": "SDL2_mixer-owned public full struct with simple exposed fields. Not an opaque forward declaration."
          }
        ]
      },
      "flags_enums": {
        "last_audited": "2026-05-26",
        "allow_list": []
      },
      "clong_methods": []
    },

    "gfx": {
      "namespace": "SDL2.Gfx",
      "library_version": "1.0.4",
      "raw_class": "SDL2_gfxNative",
      "include_subdir": "SDL2",
      "library_name": "SDL2_gfx",
      "rsp": "rsp/sdl2-gfx.rsp",
      "project_dir": "Janset.SDL2.Gfx",
      "owner_mode": false,
      "headers": [
        { "order": 10, "name": "SDL2_framerate.h" },
        { "order": 20, "name": "SDL2_gfxPrimitives.h" },
        { "order": 30, "name": "SDL2_imageFilter.h" },
        { "order": 40, "name": "SDL2_rotozoom.h" }
      ],
      "platform_sensitive_headers": [],
      "required_surface": null,
      "opaque_handles": {
        "last_audited": "2026-05-26",
        "source": "SDL2_gfx release headers + ClangSharp Modern output verification; GFX owns no local opaque handles at this audit date.",
        "auto_detect_well_known": [],
        "force_opaque_exceptions": [],
        "excluded_candidates": [
          {
            "name": "FPSmanager",
            "header": "SDL2_framerate.h",
            "header_decl": "typedef struct { Uint32 framecount; float rateticks; Uint32 baseticks; Uint32 lastticks; Uint32 rate; } FPSmanager;",
            "reason": "SDL2_gfx-owned public full POD struct used by pointer in framerate APIs. Not an opaque forward declaration."
          }
        ]
      },
      "flags_enums": {
        "last_audited": "2026-05-26",
        "allow_list": []
      },
      "clong_methods": []
    }
  }
}
```

### 4.2 Field reference — per-family section

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `namespace` | string | Yes | C# namespace for generated code (e.g. `"SDL2"`, `"SDL2.Image"`) |
| `library_version` | string | Yes | Upstream library version at last audit (e.g. `"2.32.10"`, `"2.8.8"`). Anchored to `manifest.json library_manifests[].vcpkg_version` per guardrail G54. Survives production flip as a per-family fact. |
| `raw_class` | string | Yes | Internal raw ABI container class name (e.g. `"SDLNative"`). Orchestrator derives `--methodClassName` ClangSharp arg from this. |
| `include_subdir` | string | Yes | vcpkg include subdirectory (e.g. `"SDL2"`). All SDL2 families currently share `"SDL2"`. |
| `library_name` | string | Yes | Native library name for DllImport/LibraryImport (e.g. `"SDL2"`, `"SDL2_image"`). Orchestrator derives `--libraryPath` ClangSharp arg from this. |
| `rsp` | string | Yes | Path to family-level `.rsp` file, relative to config file |
| `project_dir` | string | Yes | C# project directory name (e.g. `"Janset.SDL2.Core"`) — used to derive paths and resolve family identity in postprocess |
| `owner_mode` | boolean | Yes | Whether this family emits its own `Handles.g.cs` (see §4.5) |
| `headers` | object[] | Yes | Header files to parse for this family, each with `order` (int, used for sort) and `name` (string). Orders use gaps (10, 20, 30…) so insertion does not require renumbering. Duplicate order or duplicate name is a validation failure. |
| `platform_sensitive_headers` | string[] | Yes | Header names needing multi-OS platform-view parse (empty array if none) |
| `required_surface` | object \| null | No | Required functions/constants from excluded umbrella header. Null if N/A. |
| `required_surface.functions` | string[] | — | Function names (order-sensitive — must match the validation order in `validate_required_surface_names()`) |
| `required_surface.constants` | string[] | — | Constant names (order-sensitive) |
| `opaque_handles` | object | Yes | Pattern B handle policy with full audit metadata |
| `opaque_handles.last_audited` | string | Yes | ISO date of last audit |
| `opaque_handles.source` | string | Yes | Description of audit sources |
| `opaque_handles.auto_detect_well_known` | object[] | Yes | Handles expected from auto-detection (each with `name`, `header`, `header_decl`, `wiki_url`, `wiki_evidence`, optional `header_note` and `wiki_corroboration`) |
| `opaque_handles.force_opaque_exceptions` | object[] | Yes | Structs that must be Pattern B despite having bodies (each with `name`, `header`, `reason`) |
| `opaque_handles.excluded_candidates` | object[] | Yes | Types that look like handles but aren't (each with `name`, `reason`, optional `header` and `header_decl`) |
| `flags_enums` | object | Yes | `[Flags]` enum policy with audit metadata |
| `flags_enums.last_audited` | string | Yes | ISO date of last audit |
| `flags_enums.allow_list` | object[] | Yes | Enum names that get `[Flags]` despite not matching the `Flags` suffix (each with `name` and `reason`) |
| `clong_methods` | string[] | Yes | Method names affected by C `long`/`unsigned long` dual dispatch (empty array if none) |

### 4.3 Field reference — global section

| Field | Type | Description |
|-------|------|-------------|
| `platform_views` | object[] | 7 platform view definitions: `name`, `supported_os`, `defines[]` |
| `all_platform_macros` | string[] | All platform macro names for cross-contamination undefines |
| `base_rsp` | string | Path to cross-cutting base RSP, relative to config file |

### 4.4 Derivation rules

| Fact | Derived from | Convention |
|-------|-------------|------------|
| Generated output root | `project_dir` | `src/<project_dir>/Generated/` |
| Family ID string | JSON key in `families` | `"core"`, `"image"`, `"ttf"`, `"mixer"`, `"gfx"` |
| Family detection from output path | `project_dir` | Match `/<project_dir>/` in path |
| Family detection from namespace | `namespace` | Match namespace string |
| Handles namespace | `namespace` | Same as family namespace |
| Uniform-opaque `--owner-mode` flag | `owner_mode` | `true` → `"owner"`, `false` → `"consumer"` |
| Family-level RSP path | `rsp` | Resolved relative to config file |
| Header file include directory | `include_subdir` | `<vcpkg_installed>/<triplet>/include/<include_subdir>/` |
| ClangSharp `--file` argument | `headers[].name` | One `--file` per header, sorted by `order` |

### 4.5 Clarifications

**`owner_mode` semantics.** `owner_mode: true` means the family emits `Handles.g.cs` containing Pattern B structs for its owned opaque handles. `owner_mode: false` means the family only consumes handles (its generated `.g.cs` files have pointer→by-value rewrites applied, but no `Handles.g.cs` is emitted). The auto-detection heuristic still runs on all families regardless of mode — `owner_mode` gates only the file emission. A family with `owner_mode: false` that produces auto-detected handles is a config bug, caught by the drift watchdog.

**`required_surface` order sensitivity.** The `functions` and `constants` arrays are order-sensitive — they must match the order used by `validate_required_surface_names()` in `generate_bindings.py:193-208`, which compares them against `build/manifest.json`. The mechanical seed step preserves this order from the current `scope/sdl2-core-sdlh-required.json` file.

**Array ordering policy.** Not all arrays in config are order-sensitive. The following table is binding — reordering an order-significant array changes behavior and must not be done casually:

| Array | Order-sensitive? | Reason |
|-------|-----------------|--------|
| `headers[]` | **Yes** | Generator processes headers in order; `headers[].order` field is the explicit sort key |
| `required_surface.functions[]` | **Yes** | Matched against manifest validation order |
| `required_surface.constants[]` | **Yes** | Matched against manifest validation order |
| `platform_views[]` | **Yes** | Platform passes run in order; dedup behavior depends on view precedence |
| `all_platform_macros[]` | **No** | Undefine set — order of cross-contamination undefines is irrelevant |
| `clong_methods[]` | **No** | HashSet-like lookup; order within the array does not affect dispatch |
| `opaque_handles.auto_detect_well_known[]` | **No** | Checked by name; order does not affect drift watchdog |
| `opaque_handles.force_opaque_exceptions[]` | **No** | Checked by name |
| `opaque_handles.excluded_candidates[]` | **No** | Checked by name |
| `flags_enums.allow_list[]` | **No** | Checked by name |
| `platform_sensitive_headers[]` | **No** | Checked by name |
| `platform_views[].defines[]` | **No** | Full set applied per view; order irrelevant |

**Platform defines are exact argv strings.** Each entry in `platform_views[].defines[]` and `all_platform_macros[]` is a **verbatim** ClangSharp `--define-macro` / `--undefine-macro` argument string from the current `SDL2_PLATFORM_VIEWS` and `ALL_PLATFORM_MACROS` Python constants. This includes value-bearing entries like `_WIN32=1`, `WINAPI_FAMILY=WINAPI_FAMILY_APP`, `__WINDOWS__`, `__WINGDK__`, and `SDL_VIDEO_DRIVER_KMSDRM`. The parity validation compares exact strings, not bare names (see §8.3).

## 5. Rejected Alternatives

### 5.1 Minimal (keep layout, document + small helpers)

Rejected. Post-sweep evidence showed 4 copies of family directory mapping and 3 copies of namespace mapping in C# code alone. Documentation cannot fix maintainability bugs — the copies must be eliminated, not annotated.

### 5.2 Keep separate roster JSONs

Rejected. Two separate roster files plus `FAMILY_CONFIG` plus two scope `.txt` files created a discoverability tax (6+ files to inspect for "what does family X need?"). A single config file with clearly separated family sections is strictly more discoverable and no harder to audit — the family section is the unit of audit, and full audit metadata is preserved within it.

### 5.3 Flatten roster metadata into string arrays

Rejected during review (2026-05-26). The existing rosters carry `library_version`, `last_audited`, `source`, `header`, `reason`, `wiki_url`, `wiki_evidence`, and `notes` fields. Collapsing these into bare string arrays loses audit traceability. The unified config preserves the full object shape — each entry carries its own metadata. The tradeoff is a larger file, but the file is generated mechanically and diff-reviewed, not hand-edited.

### 5.4 Put `clong_type_names` in config

Rejected during review (2026-05-26). Native type name classification (`"SDL_threadID"`, `"unsigned long"`, `"long"`) is ABI knowledge — code must own what these mean and how they emit. A JSON edit of `clong_type_names` could silently change ABI behavior. Config owns `clong_methods[]` (which methods are affected); code owns the type classification and emit strategy. See Constitution §"Configurable Scope Vs Policy Mechanism".

## 6. File Ownership And Source-of-Truth Rules

### 6.1 RSP identity facts: config is authoritative

Family RSP files (`sdl2-core.rsp`, `sdl2-image.rsp`, and future TTF/Mixer/GFX) previously carried two identity facts that now belong in config:

| Fact | Old RSP key | Config field |
|------|-----------|-------------|
| Native library name | `--libraryPath SDL2` | `library_name: "SDL2"` |
| Raw ABI class name | `--methodClassName SDLNative` | `raw_class: "SDLNative"` |

**Iteration 2 decision:** Config is the single source of truth. The orchestrator reads `raw_class` and `library_name` from config and passes them to ClangSharp as `--methodClassName` and `--libraryPath`. The corresponding lines are removed from family RSP files. This eliminates the duplication entirely — no mirror, no drift risk.

### 6.2 Source-of-truth table

| Concept | Single source of truth |
|---------|----------------------|
| Family identity (`namespace`, `raw_class`, `library_name`, `project_dir`, `include_subdir`) | `family-config.json` |
| Family header inventory | `family-config.json` → family `headers[]` |
| Platform views | `family-config.json` → `global.platform_views` |
| Opaque handle roster | `family-config.json` → family `opaque_handles` |
| Flags enum allow-list | `family-config.json` → family `flags_enums` |
| C long method list | `family-config.json` → family `clong_methods` |
| C long type classification | Code (`ClongDualDispatchRewriter`) — ABI knowledge |
| Required umbrella surface | `family-config.json` → family `required_surface` |
| Owner/consumer mode | `family-config.json` → family `owner_mode` |
| RSP file paths | `family-config.json` → `global.base_rsp` + family `rsp` |
| Pattern B handle shape | Code (`OpaqueHandleEmitRewriter.BuildPatternBStructText`) |
| [Flags] suffix rule | Code (`FlagsAttributeRewriter`, `EndsWith("Flags")`) |
| SDL_GUID → Guid mechanism | Code (`GuidSubstitutionRewriter`) |
| C long emit strategy | Code (`ClongDualDispatchRewriter`) |

**Rule:** If a fact appears in both config and code after migration, the config copy is authoritative and the code copy is a bug. Config is loaded once at startup; derived lookups (family-by-directory, family-by-namespace) are built from config data, not from parallel switch statements.

## 7. Migration Boundaries

### 7.1 Files allowed to change

| File | Change |
|------|--------|
| `spikes/binding-generators/clangsharp/config/family-config.json` | **Created** — mechanically seeded from current files/code, diff-reviewed |
| `spikes/binding-generators/clangsharp/generate_bindings.py` | Load config via JSON; derive `--methodClassName` and `--libraryPath` ClangSharp args from config `raw_class` and `library_name`; remove `FAMILY_CONFIG`, `PLATFORM_SENSITIVE_HEADERS`, `ALL_PLATFORM_MACROS`, `SDL2_PLATFORM_VIEWS`, `owner_mode_for_family()`, `uniform_opaque_extra_args_for_family()` |
| `spikes/binding-generators/clangsharp/rsp/sdl2-core.rsp` | Remove `--libraryPath` (line 1) and `--methodClassName` (line 4) |
| `spikes/binding-generators/clangsharp/rsp/sdl2-image.rsp` | Remove `--libraryPath` (line 1) and `--methodClassName` (line 4) |
| `spikes/binding-generators/clangsharp/postprocess/Program.cs` | Remove `ResolveFamilyFromOutputDir` switch; derive family ID from config's `project_dir` field; remove default namespace fallback |
| `spikes/binding-generators/clangsharp/postprocess/UniformOpaqueFamilyIdentity.cs` | Remove `ResolveFromOutputPath` and `ResolveFromNamespace` hardcoded mappings; derive from config |
| `spikes/binding-generators/clangsharp/postprocess/UniformOpaqueOwnerMode.cs` | Remove `IsOwnerDirectoryByPath` substring check; use config `owner_mode` |
| `spikes/binding-generators/clangsharp/postprocess/OpaqueHandleEmitRewriter.cs` | Change roster path from `policy/opaque-handle-roster.json` to config; `owner_mode` → read from config section |
| `spikes/binding-generators/clangsharp/postprocess/ClongDualDispatchRewriter.cs` | Remove hardcoded `AffectedMethodNames` HashSet; read `clong_methods[]` from config per-family. Native type name classification (`"SDL_threadID"`, `"unsigned long"`, `"long"`) stays in code. |
| `spikes/binding-generators/clangsharp/postprocess/PlatformDeltaPostProcessor.cs` | Remove `PlatformOrder` array and `SupportedOsByPlatform` dictionary; derive from `global.platform_views` |
| `spikes/binding-generators/clangsharp/postprocess/FlagsAttributeRewriter.cs` | Config path change only (roster → config section); rewriter logic unchanged |
| `spikes/binding-generators/clangsharp/oracle.cs` | Remove `FamilyConfigs` static records and `KnownFamilies` array; derive from config families |
| `spikes/binding-generators/clangsharp/postprocess/PostProcessSelfTests.cs` | Update test assertions to use config-derived data instead of hardcoded counts; update config-path references |
| `spikes/binding-generators/clangsharp/tests/` | Update any test that references old file paths or hardcoded family data |

### 7.2 Files explicitly forbidden

- `build/manifest.json` — Roadmap M7 territory
- `vcpkg.json` — native build input
- `.config/dotnet-tools.json` — tool version pinning
- RSP files (except `--methodClassName` and `--libraryPath` removal per §7.1) — remaining content stays on disk
- `shims/platform-headers/*.h` — parse aids
- csproj files — separate templatization concern
- `.slnx` — build infrastructure

### 7.3 Files to delete after migration is verified

Deletion is gated on parity validation (see §8.4): the new config must match the old sources before old sources are deleted.

| File | Reason |
|------|--------|
| `scope/sdl2-core.headers.txt` | Absorbed into `headers[]` |
| `scope/sdl2-image.headers.txt` | Absorbed into `headers[]` |
| `scope/sdl2-core-sdlh-required.json` | Absorbed into `required_surface` |
| `policy/opaque-handle-roster.json` | Absorbed into `opaque_handles` |
| `policy/flags-enum-roster.json` | Absorbed into `flags_enums` |

## 8. Verification Gates

### 8.1 Determinism verification

Config becomes a determinism pin-set input. Verification gates after migration:

1. **Regen idempotency:** `--family all --execute` twice with identical config → second `git diff --ignore-cr-at-eol` is empty across all active families.
2. **Config-change idempotency:** Touching a non-functional config field (whitespace, reordering within arrays where order is not significant) does not change generated output.
3. **Full pin set unchanged:** If config, RSP files, postprocess code, orchestrator code, and native headers are all unchanged, regeneration produces byte-identical output.

### 8.2 Family isolation verification

1. **Targeted-family-only writes:** `--family core --execute` cleans/regenerates only `Janset.SDL2.Core/Generated/`. Other families' roots byte-untouched.
2. **Per-family equivalence:** `--family all` output ≡ sequential `--family core` + `--family image` (only active families).
3. **Config does not break per-family independence:** A field in family `core` does not affect generation of family `image` except through documented cross-family data flows (Core handle names pulled by satellite postprocess — this is a data pull from the same config file, not a generated-file cross-read).

### 8.3 Parity validation (before deleting old files)

Before any old config file is deleted, the implementation must:

1. Load `family-config.json` and extract header lists, rosters, required surface, platform views, and family identities.
2. Assert extracted headers match current `scope/*.headers.txt` content exactly (same names, same order).
3. Assert extracted opaque handle entries match `policy/opaque-handle-roster.json` entries (same names, same metadata).
4. Assert extracted flags enum entries match `policy/flags-enum-roster.json` entries.
5. Assert extracted required surface matches `scope/sdl2-core-sdlh-required.json`.
6. Assert extracted platform views match `generate_bindings.py` `SDL2_PLATFORM_VIEWS` and `ALL_PLATFORM_MACROS` verbatim.
7. Assert extracted family identities match `generate_bindings.py` `FAMILY_CONFIG` entries.
8. **Old files are only deleted after all parity assertions pass.**

### 8.4 Post-migration evidence

1. Solution build clean: `dotnet build .../Janset.SDL2.ClangSharpSpike.slnx -c Release` — 0 warnings, 0 errors.
2. Oracle report: `oracle.cs --family all --write-report` — Core/Image clean, TTF/Mixer/GFX missing as expected.
3. AbiTests: `792/792` across `net462`, `net8.0`, `net9.0`, `net10.0`.
4. Slopwatch: 0 issues.
5. Self-tests: Python `generate_bindings.py --self-test` passes with config-derived expected values; C# postprocess self-tests pass.
6. Header order validation: duplicate `order` or duplicate `name` within a family's `headers[]` is a hard failure.

## 9. Exit Criteria

Iteration 2 is closed when:

1. `family-config.json` exists at `clangsharp/config/family-config.json` with all 5 families fully populated, **mechanically seeded** from current files/code and diff-reviewed.
2. All 8 parity validation assertions pass (see §8.3).
3. Python `generate_bindings.py` loads all per-family and global config from this file; no hardcoded `FAMILY_CONFIG`, `PLATFORM_SENSITIVE_HEADERS`, `ALL_PLATFORM_MACROS`, or `SDL2_PLATFORM_VIEWS` remain.
4. C# postprocess derives all family identity (namespace, project_dir, owner_mode) from config; all hardcoded switch/if-chain family resolution is eliminated from `Program.cs`, `UniformOpaqueFamilyIdentity.cs`, and `UniformOpaqueOwnerMode.cs`.
5. C# `ClongDualDispatchRewriter.cs` reads `clong_methods[]` from config; no hardcoded `AffectedMethodNames` HashSet remains. Native type name classification stays in code per §3.
6. C# `oracle.cs` derives `FamilyConfigs` from config; no hardcoded per-family records remain.
7. `PlatformDeltaPostProcessor` derives platform views from `global.platform_views`; no hardcoded `PlatformOrder`/`SupportedOsByPlatform` remain.
8. All 5 retired files are deleted (headers.txt × 2, required.json, roster.json × 2) — only after parity validation passes.
9. All determinism and family-isolation gates pass (see §8.1, §8.2).
10. All post-migration evidence gates pass (see §8.4).
11. **Stale reference cleanup.** After deletion of old files, `rg` search across the spike directory for every retired path/name (`opaque-handle-roster.json`, `flags-enum-roster.json`, `sdl2-core.headers.txt`, `sdl2-image.headers.txt`, `sdl2-core-sdlh-required.json`) must return no active-policy references — only historical references explicitly marked as stale, or paths within this spec document (which records the migration). Spike README, `docs/llm-handoff.md`, `docs/next-iteration-plan.md`, and any other active spike docs must be updated to reference `config/family-config.json` instead.
12. Constitution §"Configurable Scope Vs Policy Mechanism" is updated to reflect the correct C long boundary (config owns `clong_methods[]` only; native type name classification stays in code). Constitution §"Determinism Inputs" is updated to replace old roster JSON paths with `config/family-config.json`.

## 10. Future Evolution: Spike → Production

### 10.1 Why This Design Consolidates Aggressively

The decision to consolidate into one config file is driven by the spike's endgame, not by spike-local aesthetics:

```
SPIKE PHASE (now)                    PRODUCTION PHASE (M7, post-spike)
┌────────────────────────┐           ┌──────────────────────────────────┐
│  generate_bindings.py  │           │  Unified Cake C# Target          │
│  (Python orchestrator) │  ──→      │  ├── ClangSharp tool wrapper    │
│                        │           │  ├── Postprocess rewriters      │
│  postprocess/*.cs      │           │  │   (same C# code, promoted)   │
│  (C# console app)      │           │  └── Reads manifest.json        │
│                        │           │                                  │
│  config/               │           │  build/manifest.json             │
│  family-config.json    │  ──→      │  (absorbs family-config.json)    │
│                        │           │                                  │
│  rsp/*.rsp             │  ──→      │  rsp/*.rsp (same files)         │
└────────────────────────┘           └──────────────────────────────────┘
```

**What this means for Iteration 2:**

- `family-config.json` is a **dry run** for the manifest schema. Its shape is designed so production flip (absorbing into `build/manifest.json`) is mechanical, not a re-architecture.
- Python code retires completely after the spike proves the pattern. The config is being centralized now so that when Cake takes over, there's exactly one source of truth to migrate — not 5 `.txt` files + 2 roster JSONs + a Python dict + 3 C# switch statements.
- Per-header RSP files survive the transition unchanged — ClangSharp consumes them regardless of whether it's called from Python or from a Cake C# tool.

### 10.2 What Retires When The Spike Closes

| Artifact | Fate |
|----------|------|
| `generate_bindings.py` | Retired — replaced by Cake C# target |
| `postprocess/Program.cs` (CLI shell) | Retired — rewriters promoted into Cake target, CLI dispatch replaced by Cake task orchestration |
| `config/family-config.json` | Absorbed into `build/manifest.json` under expanded schema |
| `scope/*.headers.txt` | Already retired by Iteration 2 (absorbed into config headers[]) |
| `policy/*.json` (roster files) | Already retired by Iteration 2 (absorbed into config) |
| Spike `src/` projects | Promoted to `src/SDL2.<Family>/` via `git mv` |

### 10.3 What Survives Into Production

| Artifact | Fate |
|----------|------|
| C# rewriter classes (`*Rewriter.cs`) | Candidate for promotion into Cake build host as target collaborators |
| RSP files (all tiers) | Same files, consumed by ClangSharp tool via Cake |
| Generated output layout (`Generated/{Compat,Modern}/`) | Same pattern under `src/Janset.SDL2.<Family>/` |
| Pattern B handle shape, C long policy, [Flags] policy, etc. | Same Constitution, same rewriter logic |
| Oracle / evidence reporter | Candidate for promotion as a Cake validation target |

### 10.4 Design Principle

> **Spike-local config that mirrors the manifest shape is intentional, not redundant.** It provides evidence for a future M7 manifest-design review. If/when M7 production flip is approved, the migration path is: validate spike output → approve manifest schema → delete spike config → enable Cake target that reads manifest. No re-classification needed at flip time. The exact Cake target shape, ClangSharp tool wrapper mechanics, and production manifest schema are deferred decisions requiring separate approval.

## 11. References

- Constitution §"Manifest Configuration Vs Code-Owned Policy" — authority for manifest-vs-code split
- Constitution §"Configurable Scope Vs Policy Mechanism" — authority for this design's config-vs-code boundary
- Constitution §"Generation Determinism Contract" — determinism invariants
- [satellite-expansion-roadmap.md](../satellite-expansion-roadmap.md) §"Iteration 2" — original problem statement and inventory
- [item-1-per-library-generation-spec.md](item-1-per-library-generation-spec.md) — Item 1 closure baseline
- [priority-c-closure-summary.md](../priority-c-closure-summary.md) — Priority C evidence baseline
- [next-iteration-plan.md](../next-iteration-plan.md) — active spike plan and review backlog
