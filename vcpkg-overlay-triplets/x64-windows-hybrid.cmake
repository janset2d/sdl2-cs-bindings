# Hybrid Static + Dynamic Core triplet for win-x64
#
# Base: x64-windows-release (stock vcpkg triplet).
# Invariants (library linkage, SDL-family override, build-type) live in
# _hybrid-common.cmake to keep the matrix of 7 hybrid triplets coherent.
# Issue: https://github.com/janset2d/sdl2-cs-bindings/issues/83

set(VCPKG_TARGET_ARCHITECTURE x64)
include(${CMAKE_CURRENT_LIST_DIR}/_hybrid-common.cmake)
