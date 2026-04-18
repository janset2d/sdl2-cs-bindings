cmake_minimum_required(VERSION 3.20)
project(native-smoke LANGUAGES C)

# ---------------------------------------------------------------------------
# Build options
# ---------------------------------------------------------------------------
option(SMOKE_INTERACTIVE "Enable interactive mode (real display + audio)" OFF)
# sdl2-net is in native scope today but remains a managed-package placeholder.
# Give CI / contributors a safe off-switch for triplets where the upstream vcpkg
# port is flaky so a net-only failure does not block the rest of the native smoke.
option(SMOKE_INCLUDE_NET "Include SDL2_net in the native smoke harness" ON)

# ---------------------------------------------------------------------------
# SDL2 core + all satellites via vcpkg
# ---------------------------------------------------------------------------
find_package(SDL2       CONFIG REQUIRED)
find_package(SDL2_image CONFIG REQUIRED)
find_package(SDL2_mixer CONFIG REQUIRED)
find_package(SDL2_ttf   CONFIG REQUIRED)
find_package(sdl2-gfx   CONFIG REQUIRED)
if(SMOKE_INCLUDE_NET)
    find_package(SDL2_net CONFIG REQUIRED)
endif()

# ---------------------------------------------------------------------------
# Executable
# ---------------------------------------------------------------------------
add_executable(native-smoke main.c)

target_link_libraries(native-smoke PRIVATE
    $<TARGET_NAME_IF_EXISTS:SDL2::SDL2main>
    SDL2::SDL2
    SDL2_image::SDL2_image
    SDL2_mixer::SDL2_mixer
    SDL2_ttf::SDL2_ttf
    SDL2::SDL2_gfx
)

if(SMOKE_INCLUDE_NET)
    target_link_libraries(native-smoke PRIVATE SDL2_net::SDL2_net)
    target_compile_definitions(native-smoke PRIVATE SMOKE_INCLUDE_NET=1)
endif()

if(SMOKE_INTERACTIVE)
    target_compile_definitions(native-smoke PRIVATE SMOKE_INTERACTIVE=1)
endif()

set(SMOKE_PNG_ASSET "${CMAKE_CURRENT_SOURCE_DIR}/../../../assets/janset2d-sdl-min.png")
configure_file("${SMOKE_PNG_ASSET}" "${CMAKE_CURRENT_BINARY_DIR}/janset2d-sdl-min.png" COPYONLY)

# ---------------------------------------------------------------------------
# Copy SDL2 DLLs next to the executable (Windows only)
# vcpkg hybrid triplet builds release-only DLLs. On Windows the dynamic
# linker searches the executable's directory first, so we need them there.
# ---------------------------------------------------------------------------
if(WIN32)
    set(_SDL_BIN_DIR "${VCPKG_INSTALLED_DIR}/${VCPKG_TARGET_TRIPLET}/bin")
    file(GLOB _SDL_DLLS "${_SDL_BIN_DIR}/SDL2*.dll")
    foreach(_dll ${_SDL_DLLS})
        add_custom_command(TARGET native-smoke POST_BUILD
            COMMAND ${CMAKE_COMMAND} -E copy_if_different "${_dll}" "$<TARGET_FILE_DIR:native-smoke>"
            COMMENT "Copying ${_dll}")
    endforeach()
endif()
