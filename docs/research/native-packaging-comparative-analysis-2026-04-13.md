# Comparative Analysis: Native Library Packaging in Mature .NET Binding Projects

**Date:** 2026-04-13
**Status:** Research complete
**Purpose:** Inform architectural decision between pure dynamic and hybrid static + dynamic core packaging for Janset.SDL2
**Related:** [packaging-strategy-hybrid-static-2026-04-13.md](packaging-strategy-hybrid-static-2026-04-13.md), [packaging-strategy-pure-dynamic-2026-04-13.md](packaging-strategy-pure-dynamic-2026-04-13.md), [#75](https://github.com/janset2d/sdl2-cs-bindings/issues/75)

---

## 1. SkiaSharp

**Repository:** [github.com/mono/SkiaSharp](https://github.com/mono/SkiaSharp)
**Maintainer:** Microsoft (formerly Xamarin/Mono team)
**Latest version inspected:** 3.119.2

### Packaging shape

Three-tier platform split. Managed bindings in `SkiaSharp`, per-OS native assets in separate packages:

| Package | Size | Contents |
|---|---|---|
| `SkiaSharp.NativeAssets.Win32` | 73.25 MB | `runtimes/win-{x86,x64,arm64}/native/libSkiaSharp.dll` (+ PDBs) |
| `SkiaSharp.NativeAssets.Linux` | 52.64 MB | `runtimes/linux-{x64,x86,arm,arm64,riscv64,loongarch64,musl-*}/native/libSkiaSharp.so` |
| `SkiaSharp.NativeAssets.Linux.NoDependencies` | 51.64 MB | Same structure, fully self-contained (no fontconfig) |
| `SkiaSharp.NativeAssets.macOS` | — | `runtimes/osx-{x64,arm64}/native/libSkiaSharp.dylib` |
| `SkiaSharp.NativeAssets.iOS` | — | XCFramework |
| `SkiaSharp.NativeAssets.Android` | — | Per-ABI `libSkiaSharp.so` |

**Critical finding: ONE native binary per RID. No transitive DLLs.**

Verified from nupkg contents:

```
runtimes/win-arm64/native/libSkiaSharp.dll    10,408,992 bytes
runtimes/win-x64/native/libSkiaSharp.dll      11,611,680 bytes
runtimes/win-x86/native/libSkiaSharp.dll      10,101,792 bytes
```

No `libpng.dll`, no `zlib1.dll`, no `freetype.dll` — zero transitive DLLs.

### Transitive dep handling — everything statically baked

Build system: GN/Ninja (Google's Skia build system). Build script at `native/linux/build.cake`.

GN args explicitly disable system libraries:

```
skia_use_system_freetype2=false
skia_use_system_libpng=false
skia_use_system_libjpeg_turbo=false
skia_use_system_zlib=false
skia_use_system_expat=false
```

Linker flags include `-static-libstdc++` and `-static-libgcc`.

**THIRD-PARTY-NOTICES.txt** (139,775 bytes) in the nupkg confirms the following are statically baked:

- ANGLE, HarfBuzz, Skia, etc1, gif, **libpng**, DNG SDK, **expat**, **freetype**, ICU, imgui, jsoncpp, **libjpeg-turbo**, **libwebp**, libmicrohttpd, piex, sdl, sfntly, SPIR-V Headers, SPIR-V Tools, **zlib**

All permissive licenses (MIT, BSD, zlib).

### Symbol visibility approach

Uses GNU linker version scripts (`native/linux/libSkiaSharp/libSkiaSharp.map`):

```
libSkiaSharp {
    global:
        sk_*;
        gr_*;
        skottie_*;
        sksg_*;
        skresources_*;
    local:
        *;
};
```

Only SkiaSharp's own API symbols are exported. All transitive dep symbols (zlib's `deflate`, freetype's `FT_Init_FreeType`, etc.) are hidden. This prevents symbol pollution when multiple native libraries are loaded in the same process.

### NoDependencies variant

The `NoDependencies` Linux package excludes fontconfig (the only remaining system dependency beyond glibc/libm/libdl/libpthread). This variant exists specifically for Docker/Alpine/minimal container deployments where fontconfig is unavailable.

### HarfBuzzSharp — same pattern

HarfBuzzSharp follows the identical architecture. Single `libHarfBuzzSharp.dll` per RID (1.5-1.9 MB on Windows), no transitive deps. Version script: `hb_*` exported, everything else hidden.

### Custom build infrastructure

- Azure Pipelines (not GitHub Actions)
- Docker containers for Linux cross-compilation
- Architecture-specific Spectre mitigations (x64/x86: `-mretpoline`; arm/arm64: `-mharden-sls=all`)
- Alpine/musl builds with custom `__WORDSIZE` definitions

### Maintenance signals

Active, Microsoft-backed. 3.x line is mature. Per-OS split justified by binary sizes (10-18 MB per architecture, would exceed NuGet limits if all-in-one).

---

## 2. LibGit2Sharp

**Repository:** [github.com/libgit2/libgit2sharp.nativebinaries](https://github.com/libgit2/libgit2sharp.nativebinaries)
**Latest version inspected:** 2.0.323

### Packaging shape

Single native package containing ALL platforms (library is small enough):

```
LibGit2Sharp.NativeBinaries (8.97 MB)
└── runtimes/
    ├── win-x86/native/git2-3f4182d.dll          (1,397,760)
    ├── win-x64/native/git2-3f4182d.dll          (1,788,416)
    ├── win-arm64/native/git2-3f4182d.dll        (1,504,256)
    ├── osx-x64/native/libgit2-3f4182d.dylib     (1,270,816)
    ├── osx-arm64/native/libgit2-3f4182d.dylib   (1,214,152)
    ├── linux-x64/native/libgit2-3f4182d.so      (1,752,032)
    ├── linux-arm64/native/libgit2-3f4182d.so    (1,749,400)
    ├── linux-arm/native/libgit2-3f4182d.so      (1,172,124)
    ├── linux-ppc64le/native/libgit2-3f4182d.so  (2,380,184)
    ├── linux-musl-x64/native/libgit2-3f4182d.so (1,797,784)
    ├── linux-musl-arm64/native/libgit2-3f4182d.so
    └── linux-musl-arm/native/libgit2-3f4182d.so
```

**Critical finding: ONE binary per RID (named with commit hash suffix). No transitive DLLs.**

### Hash suffix scheme

Binary name: `git2-3f4182d.dll` (or `.so` / `.dylib`).

The `3f4182d` is the short SHA of the libgit2 commit the binary was built from. This solves:

1. **Version collision:** If two different NuGet packages bundle different libgit2 versions, they load different filenames — no overwrite.
2. **Debugging:** Binary filename encodes its exact source commit.
3. **Symlink avoidance:** No need for `libgit2.so.1.8` → `libgit2.so` symlink chains (NuGet doesn't support symlinks).

### Transitive dep handling — all baked

From `build.libgit2.sh`:

```
-DUSE_BUNDLED_ZLIB=ON        # zlib statically baked
-DUSE_SSH=exec               # no libssh2 at all; delegates to system SSH binary
-DUSE_HTTPS=OpenSSL-Dynamic  # only OpenSSL stays dynamic on Linux
-DUSE_HTTPS=Schannel         # Windows uses OS-native TLS, no mbedtls
```

So: zlib is static, SSH uses external process (no library), TLS uses OS-native (Schannel/SecureTransport) or dynamic OpenSSL. No libssh2, no mbedtls, no libcurl shipped.

### Custom build infrastructure

- PowerShell (`build.libgit2.ps1`) for Windows with Visual Studio
- Shell script (`build.libgit2.sh`) for Linux/macOS
- Docker containers (`Dockerfile.linux`, `Dockerfile.linux-musl`) for reproducible Linux builds
- NuGet package assembled via `nuget.exe Pack` with custom `.nuspec`

### Maintenance signals

Active. 11 RIDs covered including ppc64le. `.props` files for .NET Framework compatibility.

---

## 3. SQLitePCLRaw

**Repository:** [github.com/ericsink/SQLitePCL.raw](https://github.com/ericsink/SQLitePCL.raw)
**Latest version inspected:** 2.1.11 (lib), 3.0.2 (provider)

### Packaging shape — four-tier architecture

```
SQLitePCLRaw.bundle_e_sqlite3         (convenience meta-package)
├── SQLitePCLRaw.core                  (ISQLite3Provider interface)
├── SQLitePCLRaw.provider.e_sqlite3    (DllImport bridge to "e_sqlite3")
└── SQLitePCLRaw.lib.e_sqlite3         (native binaries)
```

The `lib` package is the native binary carrier. Verified nupkg contents:

```
SQLitePCLRaw.lib.e_sqlite3 (20.01 MB)
└── runtimes/
    ├── win-x86/native/e_sqlite3.dll           (1,453,568)
    ├── win-x64/native/e_sqlite3.dll           (1,795,072)
    ├── win-arm/native/e_sqlite3.dll           (1,282,560)
    ├── win-arm64/native/e_sqlite3.dll         (1,592,320)
    ├── osx-x64/native/libe_sqlite3.dylib      (1,591,680)
    ├── osx-arm64/native/libe_sqlite3.dylib    (1,554,624)
    ├── linux-x64/native/libe_sqlite3.so       (1,348,440)
    ├── linux-arm64/native/libe_sqlite3.so     (1,374,696)
    ├── linux-musl-x64/native/libe_sqlite3.so  (1,323,128)
    ├── linux-musl-arm64/native/libe_sqlite3.so
    ├── linux-s390x, linux-riscv64, linux-ppc64le, linux-mips64 ...
    ├── maccatalyst-{x64,arm64}/native/...
    └── browser-wasm/nativeassets/net{6,7,8,9}.0/e_sqlite3.a
```

**Critical finding: ONE binary per RID. No transitive DLLs.** SQLite is self-contained (no external deps beyond libc).

Covers **28 RIDs** including exotic ones (s390x, riscv64, mips64, ppc64le, armel, wasm).

### Plugin/provider architecture

The `ISQLite3Provider` interface allows swapping the native SQLite implementation:

- `e_sqlite3`: custom build of SQLite shipped with the package
- `sqlite3`: system-installed SQLite
- `winsqlite3`: Windows built-in SQLite (winsqlite3.dll)
- `sqlcipher`: encrypted SQLite variant
- Dynamic provider: runtime library selection

`Batteries_V2.Init()` pattern auto-configures the correct provider per platform.

### Bundle packages

- `bundle_e_sqlite3`: uses e_sqlite3 everywhere
- `bundle_green`: uses e_sqlite3 on most platforms, system SQLite on iOS
- `bundle_winsqlite3`: uses Windows built-in SQLite on Windows

### Maintenance signals

Mature, widely used (Microsoft.Data.Sqlite depends on it). 28 RIDs. Eric Sink maintains actively.

---

## 4. Magick.NET / Magick.Native

**Repository:** [github.com/dlemstra/Magick.NET](https://github.com/dlemstra/Magick.NET) + [github.com/dlemstra/Magick.Native](https://github.com/dlemstra/Magick.Native)
**Latest version inspected:** 14.11.1

### Packaging shape

**Most directly comparable to our problem domain** — many transitive deps (freetype, zlib, libpng, libtiff, etc.).

NuGet topology uses quantum-depth + architecture matrix:

```
Magick.NET.Core                        (managed interface)
Magick.NET-Q8-x64                      (managed bindings + native)
Magick.NET-Q8-x86
Magick.NET-Q8-AnyCPU                   (all architectures bundled)
Magick.NET-Q16-x64
Magick.NET-Q16-x86
Magick.NET-Q16-AnyCPU                  (93.04 MB)
Magick.NET-Q16-HDRI-x64
Magick.NET-Q16-HDRI-AnyCPU
... (18+ platform-specific packages)
```

Verified `Magick.NET-Q16-AnyCPU` nupkg native file listing:

```
runtimes/linux-arm64/native/Magick.Native-Q16-arm64.dll.so   (32,938,160)
runtimes/linux-musl-x64/native/Magick.Native-Q16-x64.dll.so  (37,879,472)
runtimes/linux-x64/native/Magick.Native-Q16-x64.dll.so       (37,950,448)
runtimes/osx-arm64/native/Magick.Native-Q16-arm64.dll.dylib  (27,943,272)
runtimes/osx-x64/native/Magick.Native-Q16-x64.dll.dylib      (35,421,544)
runtimes/win-arm64/native/Magick.Native-Q16-arm64.dll        (19,928,872)
runtimes/win-x64/native/Magick.Native-Q16-x64.dll            (23,002,568)
runtimes/win-x86/native/Magick.Native-Q16-x86.dll            (19,130,664)
```

**Critical finding: ONE massive binary per RID (~20-38 MB). ZERO transitive DLLs.**

### Transitive dep handling — EVERYTHING statically baked

From `Magick.Native/src/Magick.Native/CMakeLists.txt`, the link list is exclusively `.a` static archives:

```cmake
target_link_libraries(${LIBRARY_NAME}
    /tmp/ImageMagick/lib/libMagickWand-7.${QUANTUM_NAME}.a
    /tmp/ImageMagick/lib/libMagickCore-7.${QUANTUM_NAME}.a
    librsvg-2.a  libgdk_pixbuf-2.0.a  libcroco-0.6.a      # SVG
    libraqm.a                                                # text layout
    libpangocairo-1.0.a  libpango-1.0.a  libpangoft2-1.0.a # text rendering
    libcairo.a  libpixman-1.a                               # 2D rendering
    libfontconfig.a                                          # font discovery
    libharfbuzz.a                                            # text shaping
    libfribidi.a                                             # bidi text
    libfreetype.a                                            # font rendering
    libheif.a  libaom.a  libde265.a                         # HEIF/AVIF
    libjxl.a  libjxl_threads.a  libjxl_cms.a               # JPEG XL
    libbrotlienc.a  libbrotlidec.a  libbrotlicommon.a       # Brotli
    libhwy.a                                                 # SIMD
    libOpenEXR-3_4.a  ... libImath-3_2.a                    # OpenEXR
    libopenjph.a                                             # JPEG 2000 HT
    libraw_r.a                                               # RAW photos
    libtiff.a                                                # TIFF
    libwebpmux.a  libwebpdemux.a  libwebp.a  libsharpyuv.a # WebP
    libpng.a                                                 # PNG
    libturbojpeg.a                                           # JPEG
    libopenjp2.a                                             # JPEG 2000
    liblqr-1.a                                               # liquid rescale
    liblcms2.a                                               # color management
    libgio-2.0.a  libgobject-2.0.a  libglib-2.0.a  libffi.a  # GLib
    libxml2.a                                                # XML
    libzip.a                                                 # ZIP
    liblzma.a                                                # LZMA
    libbz2.a                                                 # bzip2
    libz.a                                                   # zlib
    libopenh264.a                                            # H.264
    -static-libstdc++  -static-libgcc                        # C++ runtime
)
```

**30+ dependencies statically linked into a single shared library.** This is the most aggressive static-baking approach in the entire .NET ecosystem.

ImageMagick itself is built with `--disable-shared --enable-static --enable-delegate-build`.

### Custom build infrastructure

- Separate repository (`dlemstra/Magick.Native`) for the native build
- Docker containers for Linux builds
- CMake for the native wrapper
- Autotools for ImageMagick itself
- GitHub Actions CI
- Per-platform settings files (e.g., `build/linux-x64/settings.sh`)

### LGPL handling

Not directly applicable — ImageMagick delegates are all permissive. But the pattern of "bake everything static into one binary" would need modification if LGPL libraries were involved.

### Maintenance signals

Very active (1,887 commits on Magick.Native). Supports 8 RIDs. The 93 MB AnyCPU package size is a known trade-off accepted by the community.

---

## 5. ppy/SDL3-CS (osu! framework)

**Repository:** [github.com/ppy/SDL3-CS](https://github.com/ppy/SDL3-CS)
**Latest version inspected:** 2026.320.0

### Packaging shape

**Most directly relevant comparison** — same domain (SDL bindings with satellites).

Separate packages per satellite library:

| Package | Size | Dependencies |
|---|---|---|
| `ppy.SDL3-CS` | 28.05 MB | None |
| `ppy.SDL3_image-CS` | 14.68 MB | ppy.SDL3-CS >= 2026.320.0 |
| `ppy.SDL3_mixer-CS` | 20.22 MB | ppy.SDL3-CS >= 2026.320.0 |
| `ppy.SDL3_ttf-CS` | — | ppy.SDL3-CS >= 2026.320.0 |

Verified nupkg contents for `ppy.SDL3-CS`:

```
runtimes/win-x64/native/SDL3.dll            (2,695,680)
runtimes/win-arm64/native/SDL3.dll           (2,540,032)
runtimes/win-x86/native/SDL3.dll             (2,282,496)
runtimes/osx-x64/native/libSDL3.dylib        (3,102,472)
runtimes/osx-arm64/native/libSDL3.dylib       (2,878,816)
runtimes/linux-x64/native/libSDL3.so          (4,191,872)
runtimes/linux-x86/native/libSDL3.so          (4,268,528)
runtimes/linux-arm64/native/libSDL3.so         (4,059,480)
runtimes/linux-arm/native/libSDL3.so           (3,039,156)
+ ios XCFramework + 4 android ABIs
```

**ONE binary per RID per package.**

Verified `ppy.SDL3_image-CS` — only `SDL3_image.dll/so/dylib`, no transitive deps:

```
runtimes/win-x64/native/SDL3_image.dll       (1,167,360)
runtimes/linux-x64/native/libSDL3_image.so    (2,002,584)
```

Verified `ppy.SDL3_mixer-CS` — only `SDL3_mixer.dll/so/dylib`:

```
runtimes/win-x64/native/SDL3_mixer.dll       (1,427,456)
runtimes/linux-x64/native/libSDL3_mixer.so    (2,239,672)
```

### Transitive dep handling — statically baked via vendored deps

**This is the smoking gun.** From `External/build.sh`:

```bash
# SDL3_ttf: vendored dependencies, statically linked
run_cmake SDL_ttf ... -DSDLTTF_VENDORED=ON

# SDL3_image: vendored dependencies, deps NOT shared (static), AVIF off
run_cmake SDL_image ... -DSDLIMAGE_AVIF=OFF -DSDLIMAGE_DEPS_SHARED=OFF -DSDLIMAGE_VENDORED=ON

# SDL3_mixer: vendored dependencies, deps NOT shared (static)
run_cmake SDL_mixer ... -DSDLMIXER_MP3_MPG123=OFF -DSDLMIXER_DEPS_SHARED=OFF -DSDLMIXER_VENDORED=ON
```

Key CMake flags:

- **`VENDORED=ON`**: Build and statically link bundled versions of all dependencies (from git submodules in `External/SDL_image/external/`, etc.)
- **`DEPS_SHARED=OFF`**: Do NOT dynamically load format libraries at runtime — link them statically instead

This means SDL3_image ships with libpng, libjpeg-turbo, libwebp, libtiff, zlib all statically baked into a single `SDL3_image.dll`. SDL3_mixer ships with libogg, libvorbis, libopus, libflac, etc. all baked in.

SDL3 core itself is built as a shared library (`-DSDL_SHARED=ON -DSDL_STATIC=OFF`), exactly matching our proposed hybrid model.

### LGPL handling

`-DSDLMIXER_MP3_MPG123=OFF` — they explicitly disable mpg123 (LGPL) and fall back to dr_mp3 (public domain, header-only). This avoids the LGPL question entirely by using a permissive alternative.

### Build infrastructure

- GitHub Actions with 13-platform matrix (win-{x64,x86,arm64}, linux-{x64,x86,arm64,arm}, osx-{x64,arm64}, android-{arm64,arm,x64,x86})
- Docker containers (`arm64v8/ubuntu:22.04`, `arm32v7/ubuntu:22.04`) for ARM Linux
- CMake + Ninja
- Git submodules for all SDL satellite libraries
- Native binaries built to `native/{platform}/` directories, uploaded as artifacts, then PR'd back to the repo

### Maintenance signals

Very active (osu! framework depends on it). Date-based versioning (2026.320.0). 28 MB core package covers 13 platform/architecture combos.

---

## 6. Alimer.Bindings.SDL

**Repository:** [github.com/amerkoleci/Alimer.Bindings.SDL](https://github.com/amerkoleci/Alimer.Bindings.SDL)
**Latest version inspected:** 3.9.8

### Packaging shape

Single package combining managed bindings AND native binaries:

```
Alimer.Bindings.SDL (5.58 MB)
├── lib/net8.0/Alimer.Bindings.SDL.dll     (553,472)
├── lib/net9.0/Alimer.Bindings.SDL.dll     (552,960)
├── runtimes/linux-x64/native/libSDL3.so   (4,000,856)
├── runtimes/osx/native/libSDL3.dylib      (5,079,904)
├── runtimes/win-arm64/native/SDL3.dll     (2,231,808)
└── runtimes/win-x64/native/SDL3.dll       (2,378,752)
```

**Critical finding: Only 4 RIDs covered (linux-x64, osx universal, win-arm64, win-x64).** No linux-arm64, no win-x86.

### Transitive dep handling

SDL3 core only — no satellite libraries (no SDL3_image, SDL3_mixer, SDL3_ttf). The single `libSDL3.so` is the SDL core dynamic library with no transitive deps (SDL3 itself has no external library dependencies on most platforms).

macOS uses a universal binary (`runtimes/osx/native/` rather than `runtimes/osx-x64/` and `runtimes/osx-arm64/`).

### Binding generation

Language composition: C (58.3%), C# (37.4%), C++ (3.9%). CMakeLists.txt uses FetchContent to pull SDL3 source from `libsdl-org/SDL` at a pinned tag. Build configuration: `SDL_SHARED=ON`, `SDL_STATIC=OFF`.

### Maintenance signals

221 commits, 38 stars. Minimal RID coverage compared to ppy/SDL3-CS. No satellite library support. More of a lightweight binding than a batteries-included distribution.

---

## 7. flibitijibibo/SDL2-CS

**Repository:** [github.com/flibitijibibo/SDL2-CS](https://github.com/flibitijibibo/SDL2-CS)

### Packaging shape

**Does NOT ship native binaries at all.** Pure managed wrapper only.

The project provides P/Invoke declarations for SDL2, SDL2_gfx, SDL2_image, SDL2_mixer, and SDL2_ttf. Users are expected to provide their own native SDL2 libraries (typically from SDL's official releases, or FNA's fnalibs).

No NuGet package. Consumed as a source project reference or directly compiled DLL.

### Model

"Bring your own natives." The binding project only covers the C# interop layer. This is the simplest possible approach but puts the entire native distribution burden on the consumer.

### Maintenance signals

Designed for FNA's use case. Last meaningful binding update was SDL2-specific. The project is feature-complete (mirrors the SDL2 C headers) but does not evolve its distribution model.

---

## 8. Microsoft.ML.OnnxRuntime

**Repository:** [github.com/microsoft/onnxruntime](https://github.com/microsoft/onnxruntime)
**Latest version inspected:** 1.24.4

### Packaging shape

Unified package with all platforms:

```
Microsoft.ML.OnnxRuntime (large, all platforms)
├── runtimes/win-x64/native/onnxruntime.dll                (14,203,464)
├── runtimes/win-x64/native/onnxruntime_providers_shared.dll (22,088)
├── runtimes/win-arm64/native/onnxruntime.dll              (14,215,752)
├── runtimes/win-arm64/native/onnxruntime_providers_shared.dll (21,528)
├── runtimes/linux-x64/native/libonnxruntime.so            (22,159,232)
├── runtimes/linux-x64/native/libonnxruntime_providers_shared.so (14,632)
├── runtimes/linux-arm64/native/libonnxruntime.so          (18,625,376)
├── runtimes/linux-arm64/native/libonnxruntime_providers_shared.so (198,792)
├── runtimes/osx-arm64/native/libonnxruntime.dylib         (35,451,632)
├── runtimes/android/native/onnxruntime.aar
└── runtimes/ios/native/onnxruntime.xcframework.zip
```

**Almost single binary** — `onnxruntime.dll` is the mega-binary with everything baked in. The only companion is `onnxruntime_providers_shared.dll` (22 KB on Windows, 14 KB on Linux x64), which is a tiny plugin interface binary.

### Transitive dep handling

ONNX Runtime statically links protobuf, Eigen, ONNX format parsing, and many other deps into the single `onnxruntime.dll`. The "providers_shared" binary is the only exception — it's a deliberate extension point for execution provider plugins (CUDA, DirectML, etc.), not a transitive dependency.

Separate packages exist for GPU variants:

- `Microsoft.ML.OnnxRuntime.Gpu` (CUDA)
- `Microsoft.ML.OnnxRuntime.Gpu.Windows` (platform-specific GPU split)
- `Microsoft.ML.OnnxRuntime.DirectML`

### Maintenance signals

Microsoft-backed, extremely active. The single-binary pattern is proven at massive scale.

---

## 9. HarfBuzzSharp

**Repository:** Part of [github.com/mono/SkiaSharp](https://github.com/mono/SkiaSharp)
**Latest version inspected:** 8.3.1.3

### Packaging shape

Follows the exact same pattern as SkiaSharp:

```
HarfBuzzSharp.NativeAssets.Win32 (68 MB with PDBs)
├── runtimes/win-x86/native/libHarfBuzzSharp.dll    (1,518,624)
├── runtimes/win-arm64/native/libHarfBuzzSharp.dll   (1,924,640)
└── runtimes/win-x64/native/libHarfBuzzSharp.dll     (1,816,088)
+ PDB files (~21 MB each)
```

Single binary per RID. No transitive deps. Version script exports only `hb_*` symbols.

---

## Synthesis

### Dominant pattern in the .NET ecosystem

**Every mature .NET native binding project with significant transitive dependencies uses the hybrid static + dynamic core pattern:**

| Project | Transitive deps | Ships separate transitive DLLs? | Approach |
|---|---|---|---|
| SkiaSharp | freetype, zlib, libpng, expat, ICU, libjpeg-turbo, libwebp | **No** | All statically baked |
| LibGit2Sharp | zlib | **No** | Bundled zlib, OS-native TLS |
| SQLitePCLRaw | none (SQLite is self-contained) | **No** | N/A — no transitive deps |
| Magick.NET | 30+ deps (freetype, zlib, libpng, libtiff, harfbuzz, cairo, glib, etc.) | **No** | All statically baked |
| ppy/SDL3-CS | libpng, libjpeg, zlib, libogg, libvorbis, libopus, freetype, harfbuzz | **No** | All vendored + statically baked |
| HarfBuzzSharp | ICU subset | **No** | All statically baked |
| OnnxRuntime | protobuf, Eigen, ONNX, many others | **No** (one tiny plugin DLL) | All statically baked |

**The score is 7-0. No mature .NET project ships transitive native dependencies as separate DLLs in their NuGet packages.**

### What no one does (risk patterns)

1. **Common.* shared native dependency packages** — The 26-package topology proposed in the pure dynamic option has zero precedent in the .NET ecosystem. No project distributes zlib, libpng, or freetype as standalone NuGet packages that other native packages depend on. This is an untested, high-maintenance pattern.

2. **RPATH/`$ORIGIN` tricks for NuGet distribution** — While technically valid on Linux/macOS, no NuGet-distributed project relies on RPATH for dependency isolation. MSBuild's native-copy behavior flattens everything into `bin/`, making RPATH irrelevant unless custom targets override the flattening.

3. **Selective static baking (Mechanism C)** — Partly static, partly dynamic hybrid where only collision-prone deps are baked. No project does this. They either go fully static for transitive deps or (in the rare SQLite case) have no transitive deps to worry about.

### Outliers and what justifies them

1. **flibitijibibo/SDL2-CS** ships no natives at all. Justified by FNA's distribution model where the game framework vendor (flibit) separately distributes "fnalibs" native packages. This works for a single-vendor ecosystem but not for a general-purpose NuGet binding.

2. **Alimer.Bindings.SDL** covers only 4 RIDs. Justified by being a lightweight project primarily serving the Alimer engine, not a general-purpose binding.

3. **OnnxRuntime** ships `onnxruntime_providers_shared.dll` as a second binary. This is a deliberate plugin interface, not a transitive dependency leak.

### Direct lessons for Janset.SDL2

1. **The hybrid static model is not novel — it is the established standard.** SkiaSharp, Magick.NET, ppy/SDL3-CS, and LibGit2Sharp all do it. Janset.SDL2 would be following the dominant pattern, not inventing one.

2. **ppy/SDL3-CS is the closest peer and they already solved this problem.** Their build script uses the exact same CMake flags (`VENDORED=ON`, `DEPS_SHARED=OFF`) that Janset.SDL2's hybrid model proposes via vcpkg custom triplets. The difference is vcpkg vs. vendored git submodules, but the architectural choice is identical.

3. **Symbol visibility via version scripts is mandatory.** SkiaSharp and HarfBuzzSharp demonstrate this with explicit `.map` files. The `C_VISIBILITY_PRESET hidden` + linker map approach proposed in Janset.SDL2's hybrid doc is exactly what the ecosystem leaders use.

4. **The NoDependencies variant pattern** (SkiaSharp Linux) is worth noting for future Docker/Alpine support, but is not needed at launch.

5. **LGPL avoidance by using permissive alternatives** (ppy uses dr_mp3 instead of mpg123) is a valid strategy. Janset.SDL2's opt-in Extras package for LGPL codecs is a more user-friendly approach that gives consumers the choice.

6. **Hash suffix in binary names** (LibGit2Sharp) is clever for avoiding version collisions but unnecessary when using the `runtimes/{rid}/native/` convention with NuGet's graph resolution handling versioning.

7. **Package count matters.** Successful projects minimize package count. SkiaSharp has ~6 native packages (per-OS split justified by 50+ MB sizes). Magick.NET has ~18 (justified by quantum-depth × architecture matrix). SQLitePCLRaw has 3 packages in the native pipeline. The 26-package topology in the pure dynamic option is an outlier with no precedent.

### Binary size context

| Project | Single native binary size (Linux x64) | Deps statically baked |
|---|---|---|
| SkiaSharp | 11.2 MB | ~15 deps |
| Magick.NET-Q16 | 38.0 MB | 30+ deps |
| ppy/SDL3_mixer | 2.2 MB | ~8 deps |
| ppy/SDL3_image | 2.0 MB | ~7 deps |
| LibGit2Sharp | 1.8 MB | zlib only |
| OnnxRuntime | 22.2 MB | many deps |

Janset.SDL2's estimated sizes per satellite (with static deps baked) align well with ppy/SDL3-CS sizes, as the dependency graph is comparable.

---

## Verdict Support

The ecosystem evidence overwhelmingly supports the **hybrid static + dynamic core** model:

- **Zero mature .NET projects** ship transitive native deps as separate DLLs in NuGet packages
- **All projects** with multiple transitive deps statically bake them into a single binary
- **The closest architectural peer** (ppy/SDL3-CS) already uses the exact pattern Janset.SDL2 proposes
- **Symbol visibility via version scripts** is standard practice
- **SDL core staying dynamic** while satellites statically bake deps is the proven model

The pure dynamic approach with Common.* packages would make Janset.SDL2 the first .NET project to attempt this topology at scale. That is a risk, not a feature.

---

## Sources

### NuGet Packages (inspected via direct nupkg download)

- [SkiaSharp.NativeAssets.Win32 3.119.2](https://www.nuget.org/packages/SkiaSharp.NativeAssets.Win32/)
- [SkiaSharp.NativeAssets.Linux 3.119.2](https://www.nuget.org/packages/SkiaSharp.NativeAssets.Linux/)
- [SkiaSharp.NativeAssets.Linux.NoDependencies 3.119.2](https://www.nuget.org/packages/SkiaSharp.NativeAssets.Linux.NoDependencies/)
- [LibGit2Sharp.NativeBinaries 2.0.323](https://www.nuget.org/packages/LibGit2Sharp.NativeBinaries)
- [SQLitePCLRaw.lib.e_sqlite3 2.1.11](https://www.nuget.org/packages/sqlitepclraw.lib.e_sqlite3/)
- [Alimer.Bindings.SDL 3.9.8](https://www.nuget.org/packages/Alimer.Bindings.SDL)
- [ppy.SDL3-CS 2026.320.0](https://www.nuget.org/packages/ppy.SDL3-CS)
- [ppy.SDL3_image-CS 2026.320.0](https://www.nuget.org/packages/ppy.SDL3_image-CS/2026.320.0)
- [ppy.SDL3_mixer-CS 2026.320.0](https://www.nuget.org/packages/ppy.SDL3_mixer-CS)
- [HarfBuzzSharp.NativeAssets.Win32 8.3.1.3](https://www.nuget.org/packages/HarfBuzzSharp.NativeAssets.Win32/)
- [Microsoft.ML.OnnxRuntime 1.24.4](https://www.nuget.org/packages/Microsoft.ML.OnnxRuntime)
- [Magick.NET-Q16-AnyCPU 14.11.1](https://www.nuget.org/packages/Magick.NET-Q16-AnyCPU)

### GitHub Repositories & Build Scripts

- [SkiaSharp build.cake (Linux native)](https://github.com/mono/SkiaSharp/blob/main/native/linux/build.cake) — GN args with `skia_use_system_*=false`
- [SkiaSharp libSkiaSharp.map](https://github.com/mono/SkiaSharp/blob/main/native/linux/libSkiaSharp/libSkiaSharp.map) — Version script: `sk_*`, `gr_*` exported, all else hidden
- [SkiaSharp libHarfBuzzSharp.map](https://github.com/mono/SkiaSharp/blob/main/native/linux/libHarfBuzzSharp/libHarfBuzzSharp.map) — Version script: `hb_*` exported
- [libgit2sharp.nativebinaries build.libgit2.sh](https://github.com/libgit2/libgit2sharp.nativebinaries/blob/master/build.libgit2.sh) — `USE_BUNDLED_ZLIB=ON`, `USE_SSH=exec`
- [ppy/SDL3-CS External/build.sh](https://github.com/ppy/SDL3-CS/blob/master/External/build.sh) — `SDLIMAGE_VENDORED=ON`, `SDLIMAGE_DEPS_SHARED=OFF`
- [ppy/SDL3-CS build.yml](https://github.com/ppy/SDL3-CS/blob/master/.github/workflows/build.yml) — 13-platform matrix
- [Magick.Native CMakeLists.txt](https://github.com/dlemstra/Magick.Native/blob/main/src/Magick.Native/CMakeLists.txt) — 30+ `.a` static archives linked
- [Magick.Native build.imagemagick.sh](https://github.com/dlemstra/Magick.Native/blob/main/build/shared/build.imagemagick.sh) — `--disable-shared --enable-static`
- [SQLitePCL.raw](https://github.com/ericsink/SQLitePCL.raw) — Provider/bundle architecture
- [Alimer.Bindings.SDL](https://github.com/amerkoleci/Alimer.Bindings.SDL) — FetchContent + SDL_SHARED=ON
- [flibitijibibo/SDL2-CS](https://github.com/flibitijibibo/SDL2-CS) — No natives shipped
- [SkiaSharp issue #2117](https://github.com/mono/SkiaSharp/issues/2117) — libHarfBuzzSharp symbol visibility problem

### Documentation & References

- [Build and Deployment | ppy/SDL3-CS | DeepWiki](https://deepwiki.com/ppy/SDL3-CS/3-build-and-deployment)
- [Linux Libraries | ppy/SDL3-CS | DeepWiki](https://deepwiki.com/ppy/SDL3-CS/4.3-linux-libraries)
- [Build System Overview | libsdl-org/SDL_image | DeepWiki](https://deepwiki.com/libsdl-org/SDL_image/4.1-windows-build) — SDLIMAGE_VENDORED and SDLIMAGE_DEPS_SHARED explained
- [Magick.Native Building.md](https://github.com/dlemstra/Magick.Native/blob/main/Building.md)
- [SQLitePCL.Batteries.Init](https://github.com/ericsink/SQLitePCL.raw/wiki/SQLitePCL.Batteries.Init)
