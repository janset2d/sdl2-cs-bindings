# vcpkg Overlay Triplets

This directory contains custom vcpkg triplets that implement the **Hybrid Static + Dynamic Core** packaging strategy.

**How it works:** vcpkg's `--overlay-triplets` flag tells vcpkg to look here first when resolving a triplet name. If the triplet exists here, it overrides any stock or community triplet with the same name.

```bash
vcpkg install sdl2 sdl2-image --triplet x64-windows-hybrid \
    --overlay-triplets=./vcpkg-overlay-triplets
```

## The Hybrid Model

The hybrid triplet inverts vcpkg's default linkage:

| Port kind | Default triplet (dynamic) | Hybrid triplet |
| --- | --- | --- |
| Transitive deps (zlib, libpng, freetype, etc.) | Dynamic (.dll/.so) | **Static** (.lib/.a) |
| SDL family (sdl2, sdl2-image, sdl2-mixer, etc.) | Dynamic (.dll/.so) | **Dynamic** (.dll/.so) |

When a dynamic satellite (SDL2_image.dll) links against static transitive deps (zlib.lib, libpng16.lib), the dep code is **baked into** the satellite binary. No separate `zlib1.dll` or `libpng16.dll` exists in the output.

This eliminates the DLL collision problem documented in [#75](https://github.com/janset2d/sdl2-cs-bindings/issues/75).

## Active Triplets

### x64-windows-hybrid

- **Base:** `x64-windows-release` (stock vcpkg triplet)
- **Difference:** `VCPKG_LIBRARY_LINKAGE=static` as default, SDL family overridden to `dynamic`
- **RID:** `win-x64`
- **Tracking issue:** [#83](https://github.com/janset2d/sdl2-cs-bindings/issues/83)

## Planned Triplets (Phase 2b)

| Triplet | RID | Base |
| --- | --- | --- |
| `x86-windows-hybrid` | win-x86 | `x86-windows` |
| `arm64-windows-hybrid` | win-arm64 | `arm64-windows` |
| `x64-linux-hybrid` | linux-x64 | `x64-linux-dynamic` + `-fvisibility=hidden` |
| `arm64-linux-hybrid` | linux-arm64 | `arm64-linux-dynamic` + `-fvisibility=hidden` + `-fPIC` |
| `x64-osx-hybrid` | osx-x64 | `x64-osx-dynamic` + `-fvisibility=hidden` |
| `arm64-osx-hybrid` | osx-arm64 | `arm64-osx-dynamic` + `-fvisibility=hidden` |

Linux/macOS triplets will add `-fvisibility=hidden` to `VCPKG_C_FLAGS` and `VCPKG_CXX_FLAGS` to prevent transitive symbol leakage. Windows does not need this — PE format is export-opt-in by default.

## Maintenance Rules

### On vcpkg baseline bump

Triplets are configuration files, not code. They rarely need changes. Check:

1. Port names haven't changed (unlikely — would break the entire vcpkg ecosystem)
2. New SDL family ports added → update the `PORT MATCHES` regex
3. New triplet variables introduced → evaluate if they affect our use case

### On new SDL satellite

If a new SDL library is added (e.g. `sdl3`, `sdl3-image`):

1. Add the port name to the `PORT MATCHES` regex in every hybrid triplet
2. Run `vcpkg install` to validate the new port builds correctly under the hybrid model

## Related

- **Why hybrid?** [docs/research/packaging-strategy-hybrid-static-2026-04-13.md](../docs/research/packaging-strategy-hybrid-static-2026-04-13.md)
- **Ecosystem evidence:** [docs/research/native-packaging-comparative-analysis-2026-04-13.md](../docs/research/native-packaging-comparative-analysis-2026-04-13.md)
- **Overlay ports:** [vcpkg-overlay-ports/README.md](../vcpkg-overlay-ports/README.md)
