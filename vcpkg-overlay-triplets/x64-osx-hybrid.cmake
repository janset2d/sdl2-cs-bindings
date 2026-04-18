# Hybrid Static + Dynamic Core triplet for osx-x64.
# Base: x64-osx-dynamic (community triplet). Common invariants (including
# -fvisibility=hidden) live in _hybrid-common.cmake.

set(VCPKG_TARGET_ARCHITECTURE x64)
set(VCPKG_CMAKE_SYSTEM_NAME Darwin)
set(VCPKG_OSX_ARCHITECTURES x86_64)
include(${CMAKE_CURRENT_LIST_DIR}/_hybrid-common.cmake)
