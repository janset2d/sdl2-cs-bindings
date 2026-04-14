# Janset overlay port for sdl2-mixer
#
# Changes from upstream (vcpkg baseline 0b88aacd, sdl2-mixer 2.8.1#2):
#
# 1. LGPL features removed: mpg123, fluidsynth, libflac (vcpkg.json)
# 2. LGPL dependency removed: libxmp from libmodplug feature (vcpkg.json)
# 3. Bundled alternatives enabled: minimp3 (MP3), drflac (FLAC) (portfile.cmake)
# 4. Native MIDI enabled on Windows/macOS (portfile.cmake)
# 5. MOD support via libmodplug only, libxmp disabled (portfile.cmake)
#
# Bundled codecs are header-only libraries embedded in SDL2_mixer source:
#   - minimp3 (CC0 Public Domain) — src/codecs/minimp3/minimp3.h
#   - drflac (Unlicense/MIT-0)    — src/codecs/dr_libs/dr_flac.h
#   - stb_vorbis (MIT/Unlicense)  — src/codecs/stb_vorbis/stb_vorbis.h
#   - Timidity (Artistic License) — src/codecs/timidity/*.c
#
# See: docs/research/license-inventory-2026-04-13.md
# Issue: https://github.com/janset2d/sdl2-cs-bindings/issues/84

vcpkg_from_github(
    OUT_SOURCE_PATH SOURCE_PATH
    REPO libsdl-org/SDL_mixer
    REF "release-${VERSION}"
    SHA512 653ec1f0af0b749b9ed0acd3bfcaa40e1e1ecf34af3127eb74019502ef42a551de226daef4cc89e6a51715f013e0ba0b1e48ae17d6aeee931271f2d10e82058a
    PATCHES
        fix-pkg-prefix.patch
)

vcpkg_check_features(
    OUT_FEATURE_OPTIONS FEATURE_OPTIONS
    FEATURES
        libmodplug SDL2MIXER_MOD
        libmodplug SDL2MIXER_MOD_MODPLUG
        timidity SDL2MIXER_MIDI_TIMIDITY
        wavpack SDL2MIXER_WAVPACK
        wavpack SDL2MIXER_WAVPACK_DSD
        opusfile SDL2MIXER_OPUS
)

# MIDI is enabled if timidity feature is present
if("timidity" IN_LIST FEATURES)
    list(APPEND FEATURE_OPTIONS "-DSDL2MIXER_MIDI=ON")
else()
    # Even without timidity feature, enable MIDI on platforms with native support
    if(VCPKG_TARGET_IS_WINDOWS OR VCPKG_TARGET_IS_OSX)
        list(APPEND FEATURE_OPTIONS "-DSDL2MIXER_MIDI=ON")
    else()
        list(APPEND FEATURE_OPTIONS "-DSDL2MIXER_MIDI=OFF")
    endif()
endif()

string(COMPARE EQUAL "${VCPKG_LIBRARY_LINKAGE}" "dynamic" BUILD_SHARED)

vcpkg_cmake_configure(
    SOURCE_PATH "${SOURCE_PATH}"
    OPTIONS
        ${FEATURE_OPTIONS}
        -DSDL2MIXER_VENDORED=OFF
        -DSDL2MIXER_SAMPLES=OFF
        -DSDL2MIXER_DEPS_SHARED=OFF
        -DSDL2MIXER_OPUS_SHARED=OFF
        -DSDL2MIXER_VORBIS_VORBISFILE_SHARED=OFF
        -DSDL2MIXER_VORBIS="VORBISFILE"
        # --- Janset: enable bundled alternatives (stock portfile disables these) ---
        -DSDL2MIXER_MP3=ON
        -DSDL2MIXER_FLAC=ON
        -DSDL2MIXER_FLAC_DRFLAC=ON
        -DSDL2MIXER_MIDI_NATIVE=ON
        # --- Janset: disable LGPL backends ---
        -DSDL2MIXER_MP3_MPG123=OFF
        -DSDL2MIXER_MIDI_FLUIDSYNTH=OFF
        -DSDL2MIXER_MOD_XMP=OFF
        -DSDL2MIXER_MOD_XMP_SHARED=OFF
    MAYBE_UNUSED_VARIABLES
        SDL2MIXER_MOD_XMP_SHARED
)

vcpkg_cmake_install()
vcpkg_copy_pdbs()
vcpkg_cmake_config_fixup(
    PACKAGE_NAME "SDL2_mixer"
    CONFIG_PATH "lib/cmake/SDL2_mixer"
)
vcpkg_fixup_pkgconfig()

set(debug_libname "SDL2_mixerd")
if(VCPKG_LIBRARY_LINKAGE STREQUAL "static" AND VCPKG_TARGET_IS_WINDOWS AND NOT VCPKG_TARGET_IS_MINGW)
    vcpkg_replace_string("${CURRENT_PACKAGES_DIR}/lib/pkgconfig/SDL2_mixer.pc" "-lSDL2_mixer" "-lSDL2_mixer-static")
    set(debug_libname "SDL2_mixer-staticd")
endif()

if(NOT VCPKG_BUILD_TYPE)
    vcpkg_replace_string("${CURRENT_PACKAGES_DIR}/debug/lib/pkgconfig/SDL2_mixer.pc" "-lSDL2_mixer" "-l${debug_libname}")
endif()

file(REMOVE_RECURSE "${CURRENT_PACKAGES_DIR}/debug/include")
file(REMOVE_RECURSE "${CURRENT_PACKAGES_DIR}/debug/share")

file(INSTALL "${CMAKE_CURRENT_LIST_DIR}/usage" DESTINATION "${CURRENT_PACKAGES_DIR}/share/${PORT}")
vcpkg_install_copyright(FILE_LIST "${SOURCE_PATH}/LICENSE.txt")
