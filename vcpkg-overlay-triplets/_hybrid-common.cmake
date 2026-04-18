# Shared hybrid-static + dynamic-SDL-family fragment for all hybrid overlay triplets.
#
# Each per-triplet file (x64-windows-hybrid, arm64-linux-hybrid, ...) sets arch /
# CMake system name / OSX architectures, then includes this file. All behavioral
# invariants for the hybrid packaging strategy live here so a new SDL port (e.g.
# sdl3-image) lands in one place rather than being duplicated across 7 triplet files.
#
# Invariants (see docs/research/packaging-strategy-hybrid-static-2026-04-13.md):
#   - Default library linkage: static. Transitive deps (zlib, libpng, libjpeg, etc.)
#     become .lib/.a archives that link directly into satellite DLLs/so/dylibs.
#   - CRT linkage: dynamic (preserves ucrtbase / libc interop expectations).
#   - SDL family linkage override: DYNAMIC so we ship SDL2.dll / libSDL2.so and the
#     satellite shared libraries that depend on it. Core SDL2 is the only external
#     dynamic dependency a satellite is allowed to carry (enforced by
#     HybridStaticValidator in the build host).
#   - Unix-only (Linux/Darwin): -fvisibility=hidden to prevent transitive-dep symbol
#     leakage through satellite export tables.
#   - Linux-only: VCPKG_FIXUP_ELF_RPATH so the satellite .so files resolve libSDL2.so
#     at runtime via the packaged rpath layout.
#   - Windows-only: VCPKG_BUILD_TYPE=release (skips debug builds we don't ship).

set(VCPKG_CRT_LINKAGE dynamic)
set(VCPKG_LIBRARY_LINKAGE static)

# SDL family: these are the dynamic shared libraries we ship. Extend this regex when
# a new SDL port enters scope (e.g. SDL3 bring-up) so the hybrid model covers it
# automatically across every triplet.
if(PORT MATCHES "^(sdl2|sdl2-image|sdl2-mixer|sdl2-ttf|sdl2-gfx|sdl2-net)$")
    set(VCPKG_LIBRARY_LINKAGE dynamic)
endif()

if(VCPKG_CMAKE_SYSTEM_NAME STREQUAL "Linux" OR VCPKG_CMAKE_SYSTEM_NAME STREQUAL "Darwin")
    # Hide transitive-dep symbols from the dynamic satellite's export table.
    set(VCPKG_C_FLAGS "-fvisibility=hidden")
    set(VCPKG_CXX_FLAGS "-fvisibility=hidden -fvisibility-inlines-hidden")

    if(VCPKG_CMAKE_SYSTEM_NAME STREQUAL "Linux")
        set(VCPKG_FIXUP_ELF_RPATH ON)
    endif()
else()
    # Windows triplets ship only release-configured artifacts.
    set(VCPKG_BUILD_TYPE release)
endif()
