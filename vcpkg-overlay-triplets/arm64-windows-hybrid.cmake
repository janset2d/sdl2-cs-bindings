# Hybrid Static + Dynamic Core triplet for win-arm64.
# Common invariants live in _hybrid-common.cmake.

set(VCPKG_TARGET_ARCHITECTURE arm64)
include(${CMAKE_CURRENT_LIST_DIR}/_hybrid-common.cmake)
