# Hybrid Static + Dynamic Core triplet for osx-x64
#
# Base: x64-osx-dynamic (community triplet)
# Default: all ports build as STATIC libraries (.a archives)
# Override: SDL family ports build as DYNAMIC libraries (.dylib)
#
# Symbol visibility: -fvisibility=hidden prevents transitive dep symbols
# from leaking through satellite .dylib files.
#
# See: docs/research/packaging-strategy-hybrid-static-2026-04-13.md
# See: docs/playbook/overlay-management.md (macOS platform notes)

set(VCPKG_TARGET_ARCHITECTURE x64)
set(VCPKG_CRT_LINKAGE dynamic)
set(VCPKG_LIBRARY_LINKAGE static)
set(VCPKG_CMAKE_SYSTEM_NAME Darwin)
set(VCPKG_OSX_ARCHITECTURES x86_64)

# Symbol visibility: hide transitive dep symbols from satellite .dylib exports
set(VCPKG_C_FLAGS "-fvisibility=hidden")
set(VCPKG_CXX_FLAGS "-fvisibility=hidden -fvisibility-inlines-hidden")

# SDL family: these are the .dylib files we ship — they must remain dynamic
if(PORT MATCHES "^(sdl2|sdl2-image|sdl2-mixer|sdl2-ttf|sdl2-gfx|sdl2-net)$")
    set(VCPKG_LIBRARY_LINKAGE dynamic)
endif()
