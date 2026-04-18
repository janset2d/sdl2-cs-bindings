# Hybrid Static + Dynamic Core triplet for linux-arm64.
# Base: arm64-linux-dynamic (community triplet). Common invariants live in _hybrid-common.cmake.

set(VCPKG_TARGET_ARCHITECTURE arm64)
set(VCPKG_CMAKE_SYSTEM_NAME Linux)
include(${CMAKE_CURRENT_LIST_DIR}/_hybrid-common.cmake)
