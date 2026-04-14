# Hybrid Static + Dynamic Core triplet for win-x64
#
# Default: all ports build as STATIC libraries (.lib archives).
# Override: SDL family ports build as DYNAMIC libraries (.dll).
#
# Effect: transitive dependencies (zlib, libpng, libjpeg, freetype, etc.)
# are static archives that get baked into the satellite DLLs at link time.
# SDL2 core and satellite DLLs remain dynamic shared libraries.
#
# See: docs/research/packaging-strategy-hybrid-static-2026-04-13.md
# Issue: https://github.com/janset2d/sdl2-cs-bindings/issues/83

set(VCPKG_TARGET_ARCHITECTURE x64)
set(VCPKG_CRT_LINKAGE dynamic)
set(VCPKG_LIBRARY_LINKAGE static)
set(VCPKG_BUILD_TYPE release)

# SDL family: these are the DLLs we ship — they must remain dynamic
if(PORT MATCHES "^(sdl2|sdl2-image|sdl2-mixer|sdl2-ttf|sdl2-gfx|sdl2-net)$")
    set(VCPKG_LIBRARY_LINKAGE dynamic)
endif()
