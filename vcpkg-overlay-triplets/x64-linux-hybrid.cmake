# Hybrid Static + Dynamic Core triplet for linux-x64.
# Base: x64-linux-dynamic (community triplet). Common invariants (including
# -fvisibility=hidden and VCPKG_FIXUP_ELF_RPATH) live in _hybrid-common.cmake.

set(VCPKG_TARGET_ARCHITECTURE x64)
set(VCPKG_CMAKE_SYSTEM_NAME Linux)
include(${CMAKE_CURRENT_LIST_DIR}/_hybrid-common.cmake)
