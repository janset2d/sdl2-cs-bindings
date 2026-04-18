# Hybrid Static + Dynamic Core triplet for osx-arm64.
# Base: arm64-osx-dynamic (community triplet). Common invariants live in _hybrid-common.cmake.

set(VCPKG_TARGET_ARCHITECTURE arm64)
set(VCPKG_CMAKE_SYSTEM_NAME Darwin)
set(VCPKG_OSX_ARCHITECTURES arm64)
include(${CMAKE_CURRENT_LIST_DIR}/_hybrid-common.cmake)
