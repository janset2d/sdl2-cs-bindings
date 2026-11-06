# Janset.SDL2 / Janset.SDL3

Modular C# bindings for SDL and its satellite libraries, bundled with Windows, Linux and macOS
native libraries built from source through vcpkg. The packages are an independent foundation for
Janset2D, not a game engine or a high-level game-object wrapper.

## Status

**Under development; the first public NuGet preview has not shipped.** Internal package waves are
used for validation. Production bindings still use SDL2-CS while generated replacements are being
prepared. SDL3, SDL2.Net and convenience metapackages are planned, not available packages.
See the [roadmap](https://github.com/janset2d/sdl2-cs-bindings/blob/master/docs/ROADMAP.md) for current
coverage and release work. Do not treat an internal wave as a stable public API.

## Why these bindings?

- **Bindings and natives together.** Reference the managed family package; its `.Native` dependency
  supplies the corresponding native binaries. You do not need to find a separate SDL binary bundle.
- **Source-built native stack.** Pinned vcpkg inputs and explicit features make native builds auditable.
  SDL libraries stay dynamic; non-system dependencies are incorporated statically into satellites.
- **Unix identity preserved.** Package-specific tar.gz archives and consumer build targets preserve
  Linux/macOS symlink identities instead of losing them in NuGet's ZIP format.
- **Choose the capabilities you need.** Core, image, mixer, font and graphics packages are separate
  families, each with coherent managed/native versions.

The product is C#/.NET bindings and packaging, not cross-language bindings or a tutorial framework.
Samples are separate work. Low-level SDL semantics and resource ownership remain explicit.

## Packages

| Managed package | Capability |
|---|---|
| Janset.SDL2.Core | Windowing, input, events, rendering and core SDL APIs |
| Janset.SDL2.Image | Image decoding, including PNG, JPEG, WebP, TIFF and AVIF |
| Janset.SDL2.Mixer | Audio mixing and the LGPL-free codec stack |
| Janset.SDL2.Ttf | Font rendering and HarfBuzz text shaping |
| Janset.SDL2.Gfx | Graphics primitives and rotozoom |

These are the implemented package families, not a claim that they are published on nuget.org.
SDL2.Net will add networking. Planned SDL3 coverage is Core, Image, Mixer and Ttf; it will coexist
with SDL2 rather than replacing the SDL2 package identities.

## Platform support

The native build matrix covers seven desktop RIDs:

| Platform | Native RIDs |
|---|---|
| Windows | win-x64, win-x86, win-arm64 |
| Linux | linux-x64, linux-arm64 |
| macOS | osx-x64, osx-arm64 |

**Linux requires glibc 2.31 or newer.** musl-based systems such as Alpine are not covered by these
Linux assets. The host must also provide the normal OS libraries required by SDL; bundled codecs
do not remove all operating-system dependencies.

The [manifest](https://github.com/janset2d/sdl2-cs-bindings/blob/master/build/manifest.json) owns the
native matrix and exact upstream versions. Managed framework targets are defined in
[build properties](https://github.com/janset2d/sdl2-cs-bindings/blob/master/Directory.Build.props).
Use package metadata for the framework assets in a specific published wave.

## Getting started

Once the public preview is published, select the managed family packages you need:

```bash
dotnet add package Janset.SDL2.Core --prerelease
dotnet add package Janset.SDL2.Image --prerelease
```

Native dependencies are pulled transitively; do not add a separate `.Native` reference for ordinary
use. Use a coherent release wave for related families. There is no all-in-one metapackage to install
yet. Until public previews exist, developer dogfooding uses the local/internal package procedure in
the [development guide](https://github.com/janset2d/sdl2-cs-bindings/blob/master/docs/engineering/developing.md).

Mixer intentionally excludes LGPL backends such as mpg123, libxmp and FluidSynth. MIDI uses native
OS support on Windows/macOS; Linux Timidity needs configuration and patch data on the consuming
machine. MP3 or OGG avoids that MIDI-specific setup. The
[codec registry](https://github.com/janset2d/sdl2-cs-bindings/blob/master/vcpkg-overlay-ports/README.md#sdl2-mixer)
owns backend and license details.

## Licensing and acknowledgements

Repository-authored code is [MIT-licensed](https://github.com/janset2d/sdl2-cs-bindings/blob/master/LICENSE).
SDL, satellite libraries and bundled dependencies retain their own licenses; native packages include
third-party attribution. Keep those notices when redistributing. An LGPL-free codec choice does not
make every component MIT-licensed.

Thanks to Ethan Lee and SDL2-CS for the transitional managed bindings, the SDL maintainers, Microsoft
vcpkg, Cake Frosting, and the SkiaSharp/LibGit2Sharp packaging precedents.

Contributors: start with [AGENTS.md](https://github.com/janset2d/sdl2-cs-bindings/blob/master/AGENTS.md)
and the [development guide](https://github.com/janset2d/sdl2-cs-bindings/blob/master/docs/engineering/developing.md).

## Version mapping

Family versions track the upstream major/minor line with a repository-owned patch counter. The
generated table records exact upstream patch and port metadata; it is not a publication inventory.

<!-- JANSET:MAPPING-TABLE-START -->
| Family | Version | Upstream | vcpkg Port |
| --- | --- | --- | --- |
| Janset.SDL2.Core | 2.32.0 | SDL 2.32.10 | 0 |
| Janset.SDL2.Image | 2.8.0 | SDL2_image 2.8.8 | 2 |
| Janset.SDL2.Mixer | 2.8.0 | SDL2_mixer 2.8.1 | 2 |
| Janset.SDL2.Ttf | 2.24.0 | SDL2_ttf 2.24.0 | 0 |
| Janset.SDL2.Gfx | 1.0.0 | SDL2_gfx 1.0.4 | 11 |
<!-- JANSET:MAPPING-TABLE-END -->
