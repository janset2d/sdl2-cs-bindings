# Janset overlay port for sdl2-gfx
#
# Changes from upstream (vcpkg baseline 0b88aacd, sdl2-gfx 1.0.4#11):
#
# 1. Added 003-fix-unix-visibility.patch — teaches SDL2_gfx headers to export
#    their public API on GCC/Clang 4+. Upstream only defines
#    __declspec(dllexport) for MSVC and falls back to bare `extern` everywhere
#    else, which combined with our hybrid triplet's -fvisibility=hidden hides
#    every SDL2_gfx public symbol on Linux and macOS. That breaks both the
#    native-smoke link step and any C# P/Invoke at runtime.
#
# The patch aligns SDL2_gfx with how the rest of the SDL family handles exports
# (SDL_image, SDL_mixer, SDL_ttf, SDL_net all use SDL's DECLSPEC which already
# emits visibility("default") on GCC/Clang).
#
# Upstream is effectively abandoned (SDL2_gfx 1.0.4 shipped 2018, no releases
# since). No upstream issue to track.
#
# See: docs/playbook/cross-platform-smoke-validation.md — Known Gotchas

set(VERSION 1.0.4)

vcpkg_download_distfile(ARCHIVE
    URLS "http://www.ferzkopp.net/Software/SDL2_gfx/SDL2_gfx-${VERSION}.zip"
    FILENAME "SDL2_gfx-${VERSION}.zip"
    SHA512 213b481469ba2161bd8558a7a5427b129420193b1c3895923d515f69f87991ed2c99bbc44349c60b4bcbb7d7d2255c1f15ee8a3523c26502070cfaacccaa5242
)

vcpkg_extract_source_archive(
    SOURCE_PATH
    ARCHIVE ${ARCHIVE}
    SOURCE_BASE "${VERSION}"
    PATCHES
        001-lrint-arm64.patch
        002-use-the-lrintf-intrinsic.patch
        003-fix-unix-visibility.patch
)

file(COPY "${CMAKE_CURRENT_LIST_DIR}/CMakeLists.txt" DESTINATION "${SOURCE_PATH}")

vcpkg_cmake_configure(
    SOURCE_PATH "${SOURCE_PATH}"
    OPTIONS_DEBUG -DSDL_GFX_SKIP_HEADERS=1
)

vcpkg_cmake_install()
vcpkg_cmake_config_fixup()

# Handle copyright
file(INSTALL "${SOURCE_PATH}/COPYING" DESTINATION "${CURRENT_PACKAGES_DIR}/share/${PORT}" RENAME copyright)

vcpkg_copy_pdbs()
