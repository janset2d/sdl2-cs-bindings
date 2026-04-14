cmake_minimum_required(VERSION 3.20)
project(native-smoke LANGUAGES C)

# ---------------------------------------------------------------------------
# SDL2 core + all satellites via vcpkg
# ---------------------------------------------------------------------------
find_package(SDL2       CONFIG REQUIRED)
find_package(SDL2_image CONFIG REQUIRED)
find_package(SDL2_mixer CONFIG REQUIRED)
find_package(SDL2_ttf   CONFIG REQUIRED)
find_package(sdl2-gfx   CONFIG REQUIRED)
find_package(SDL2_net   CONFIG REQUIRED)

# ---------------------------------------------------------------------------
# Build options
# ---------------------------------------------------------------------------
option(SMOKE_INTERACTIVE "Enable interactive mode (real display + audio)" OFF)

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
    SDL2_net::SDL2_net
)

if(SMOKE_INTERACTIVE)
    target_compile_definitions(native-smoke PRIVATE SMOKE_INTERACTIVE=1)
endif()

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
