# Native Smoke Test

Cross-platform C/CMake project that validates SDL2 hybrid-static native libraries at **runtime**. Tests that all 6 satellites and their baked-in codecs actually work — not just that the binaries exist.

## What It Tests

| Satellite | Test | What it proves |
| --- | --- | --- |
| **SDL2 Core** | `SDL_Init(AUDIO\|TIMER)` | Core library loads and initializes |
| **SDL2_image** | `IMG_Init(PNG\|JPG\|WebP\|TIFF\|AVIF)` | All image codecs baked in and functional |
| **SDL2_mixer** | `Mix_Init(OGG\|Opus\|MP3\|MOD)` | minimp3, libvorbis, opusfile, libmodplug all baked and working |
| **SDL2_ttf** | `TTF_Init()` | FreeType + HarfBuzz baked and working |
| **SDL2_gfx** | Link test + render (interactive) | Library loads, gfx primitives available |
| **SDL2_net** | `SDLNet_Init()` | Network library initializes |

## Two Modes

| Mode | Preset suffix | Tests | What it does | Use case |
| --- | --- | --- | --- | --- |
| **Headless** | (none) | 13 | Dummy audio/video drivers, no display needed | CI, automated validation |
| **Interactive** | `-interactive` | 16 | Real display + audio, opens a window, renders SDL2_gfx circle | Local debugging, F5 in IDE |

## Prerequisites

All platforms need the vcpkg hybrid build completed first — the `vcpkg_installed/x64-{platform}-hybrid/` directory must exist with built libraries. See `vcpkg-overlay-triplets/README.md` for how to run the hybrid build.

### Windows

- **Visual Studio 2022/2025** with C++ desktop workload (provides MSVC compiler)
- **CMake 3.20+**: `winget install Kitware.CMake`
- **Ninja**: `winget install Ninja-build.Ninja`

> **Note:** CMake and vcpkg do NOT conflict. vcpkg downloads and uses its own internal cmake via absolute path (`external/vcpkg/downloads/tools/cmake-*/`). Your global cmake installation is only used for this smoke test project, never for vcpkg port builds.

### Linux

- GCC or Clang: `sudo apt install build-essential`
- CMake: `sudo apt install cmake`
- Ninja: `sudo apt install ninja-build`

### macOS

- Xcode Command Line Tools: `xcode-select --install`
- CMake: `brew install cmake`
- Ninja: `brew install ninja`

## Build & Run

### Windows — Developer PowerShell (recommended)

Open **"Developer PowerShell for VS"** from Start menu (this loads MSVC environment), then:

```powershell
cd tests\smoke-tests\native-smoke
cmake --preset win-x64
cmake --build build\win-x64
.\build\win-x64\native-smoke.exe
```

### Windows — Developer Command Prompt

```cmd
cd tests\smoke-tests\native-smoke
cmake --preset win-x64
cmake --build build\win-x64
build\win-x64\native-smoke.exe
```

### Windows — Git Bash (Claude Code, terminal)

Git Bash doesn't have VS environment loaded. Use the included batch file:

```bash
cmd.exe //c build.bat
```

`build.bat` loads VS environment via `VsDevCmd.bat` automatically, then configures + builds + runs.

### Linux

```bash
cd tests/smoke-tests/native-smoke
cmake --preset linux-x64
cmake --build build/linux-x64
./build/linux-x64/native-smoke
```

### macOS

```bash
cd tests/smoke-tests/native-smoke
cmake --preset osx-x64
cmake --build build/osx-x64
./build/osx-x64/native-smoke
```

## Interactive Mode

Use the `*-interactive` presets to open a real window and test rendering:

```bash
# Configure + build
cmake --preset win-x64-interactive
cmake --build build/win-x64-interactive

# Run — opens a window with SDL2_gfx circle + text, auto-closes after 3s
.\build\win-x64-interactive\native-smoke.exe
```

Interactive mode runs all headless tests first (13 tests), then additionally:

- Creates an 800x600 SDL window
- Renders a circle using SDL2_gfx primitives
- Displays "Janset.SDL2 Hybrid Static - All satellites OK"
- Auto-closes after 3 seconds

## IDE Setup

### Visual Studio 2022/2025

1. File → Open → Folder → select `tests/smoke-tests/native-smoke/`
2. VS auto-detects `CMakePresets.json` and shows presets in toolbar dropdown
3. Select preset (e.g., "Windows x64 (Hybrid)" or "Windows x64 (Interactive)")
4. Build: Ctrl+Shift+B
5. Debug: F5 (set breakpoints in main.c, full MSVC debugging)

### VS Code + CMake Tools

1. Open `tests/smoke-tests/native-smoke/` folder in VS Code
2. Install the **"CMake Tools"** extension (ms-vscode.cmake-tools)
3. CMakePresets.json auto-detected — select configure preset from status bar
4. Build: Ctrl+Shift+B or CMake sidebar
5. Debug: F5 (configure a `cppdbg` launch config, or use CMake Tools' debug button)

### CLion 2026.1+ / JetBrains

CLion has native CMake support and reads CMakePresets.json directly. Setup requires one-time toolchain configuration on Windows:

**First-time setup (Windows):**

1. Open `tests/smoke-tests/native-smoke/` folder in CLion
2. Settings → Build, Execution, Deployment → **Toolchains**:
   - **Make "Visual Studio" the default toolchain** (move it above MinGW)
   - CLion bundles MinGW which uses GCC — our vcpkg libraries are MSVC-compiled, ABI incompatible with MinGW
   - Visual Studio toolchain should auto-detect VS install path, amd64 architecture, MSVC compiler
3. Settings → Build, Execution, Deployment → **CMake**:
   - **Delete or disable the "Debug" profile** — this is CLion's auto-generated default that doesn't use our presets
   - The CMakePresets.json profiles should appear automatically: "Windows x64 (Hybrid)", "Windows x64 (Interactive)", etc.
   - Enable the profiles you want to use
4. Click OK, CLion reloads CMake

**First-time setup (Linux/macOS):**

1. Open `tests/smoke-tests/native-smoke/` folder in CLion
2. Toolchains: default system toolchain (GCC/Clang) works out of the box
3. CMake: select the appropriate preset (e.g., "Linux x64 (Hybrid)")

**Building and running:**

- Select preset from the CMake tab or toolbar dropdown
- Build: Ctrl+F9 or Build menu
- Run: Shift+F10
- Debug: Shift+F9 (LLDB on Windows/macOS, GDB on Linux — breakpoints, stepping, watch all work)

**Known issues:**

- `CMAKE_TOOLCHAIN_FILE` "unused variable" warning — cosmetic, vcpkg toolchain works correctly despite the warning
- Linux/macOS presets show as unavailable on Windows (expected — they have platform conditions)
- CLion uses its own bundled CMake+Ninja, not system-installed ones — this is fine

### IntelliJ IDEA

IntelliJ IDEA does **not** include C/C++ support. CLion is a separate JetBrains product. If you have the JetBrains All Products Pack, use CLion. Otherwise, VS Code + CMake Tools is the best free cross-platform alternative.

## Expected Output

### Headless mode (13 tests)

```
Janset.SDL2 Native Smoke Test
Mode: HEADLESS (dummy drivers, CI-safe)

=== SDL2 Core ===
  [PASS] SDL_Init(AUDIO|TIMER)
  SDL version: 2.32.10

=== SDL2_image ===
  [PASS] IMG_Init: PNG
  [PASS] IMG_Init: JPEG
  [PASS] IMG_Init: WebP
  [PASS] IMG_Init: TIFF
  [PASS] IMG_Init: AVIF
  SDL_image version: 2.8.8

=== SDL2_mixer ===
  [PASS] Mix_Init: OGG Vorbis
  [PASS] Mix_Init: Opus
  [PASS] Mix_Init: MP3
  [PASS] Mix_Init: MOD
  SDL_mixer version: 2.8.1

=== SDL2_ttf ===
  [PASS] TTF_Init (FreeType + HarfBuzz)
  SDL_ttf version: 2.24.0

=== SDL2_gfx ===
  [PASS] SDL2_gfx linked (primitives available)

=== SDL2_net ===
  [PASS] SDLNet_Init
  SDL_net version: 2.2.0

=== Summary ===
  Passed: 13
  Failed: 0
  Result: ALL PASS
```

### Interactive mode (16 tests)

Same as above, plus:

```
=== Interactive Mode ===
  [PASS] SDL_CreateWindow
  [PASS] SDL_CreateRenderer
  Window open — press any key or wait 3 seconds...
  [PASS] Interactive window lifecycle

=== Summary ===
  Passed: 16
  Failed: 0
  Result: ALL PASS
```

Exit code = number of failed tests. `0` = all pass.

## How It Works

This project does **not** rebuild SDL2 libraries. It consumes the already-built vcpkg packages via `VCPKG_MANIFEST_MODE=OFF` + `VCPKG_INSTALLED_DIR` pointing to the repo's `vcpkg_installed/` directory. The `CMakePresets.json` handles all the path wiring — no manual `-D` flags needed in any IDE.

The hybrid overlay triplets ensure that each satellite `.dll`/`.so`/`.dylib` has its transitive deps (zlib, libpng, freetype, etc.) statically baked in. This test validates that baking works correctly at runtime — not just at link time.

## Troubleshooting

### "Could not find SDL2" / find_package fails

- Make sure you've run the vcpkg hybrid build first (`vcpkg install --triplet x64-windows-hybrid --overlay-triplets=./vcpkg-overlay-triplets --overlay-ports=./vcpkg-overlay-ports` from repo root)
- Check that `vcpkg_installed/x64-windows-hybrid/share/SDL2/SDL2Config.cmake` exists
- In IDE: verify the correct preset is selected (not the default "Debug" profile)

### CLion: "No CMAKE_C_COMPILER could be found"

- CLion is using MinGW (default). Switch to Visual Studio toolchain:
  - Settings → Toolchains → move "Visual Studio" above "MinGW"
- Our vcpkg libraries are MSVC-compiled — MinGW (GCC) has ABI incompatibility

### CLion: "Debug" profile fails but preset profiles work

- Delete or disable the "Debug" profile in Settings → CMake
- It's CLion's auto-generated default that doesn't use CMakePresets.json

### "CMAKE_TOOLCHAIN_FILE unused variable" warning

- Cosmetic warning, safe to ignore. The vcpkg toolchain does its work during the `project()` call, then CMake reports it as "unused" even though it has already been processed.

### Interactive mode: no audio

- Expected in current version. Interactive mode validates video (window + rendering) and codec initialization. Actual audio playback testing requires loading audio files, which is planned for future iterations.
