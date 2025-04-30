#!/bin/bash

# Check if required arguments are provided
if [ $# -ne 2 ]; then
    echo "Usage: $0 <VCPKG_DIR> <ARTIFACT_BASE_DIR>"
    echo "Example: $0 /home/user/vcpkg/installed/x64-linux-dynamic /home/user/sdl-artifacts"
    exit 1
fi

# Set directories from arguments
VCPKG_DIR="$1"
ARTIFACT_BASE_DIR="$2"
SUBDIR="runtimes/linux-x64/native"

# Function to collect vcpkg-provided dependencies using ldd
collect_deps() {
    ldd "$1" | grep "$VCPKG_DIR" | awk '{print $3}' | sort -u
}

# Function to recursively collect all dependencies
collect_all_deps() {
    local lib="$1"
    local deps
    deps=$(collect_deps "$lib")
    for dep in $deps; do
        if [ ! -L "$dep" ]; then  # Skip symlinks to avoid infinite loops
            collect_all_deps "$dep"
        fi
    done
    echo "$deps"
}

# Function to find all related files (including symlinks) for a library
find_related_files() {
    local lib="$1"
    local base_name
    base_name=$(basename "$lib" | sed 's/\.so.*//')
    find "$VCPKG_DIR/lib/" -name "${base_name}.so*" -o -name "${base_name}-*.so*"
}

# Function to map library filename to vcpkg package name
map_lib_to_package() {
    local lib="$1"
    local base_name
    base_name=$(basename "$lib" | sed 's/\.so.*//' | sed 's/^lib//')
    case "$base_name" in
        SDL2) echo "sdl2" ;;
        SDL2_image) echo "sdl2-image" ;;
        SDL2_gfx) echo "sdl2-gfx" ;;
        SDL2_mixer) echo "sdl2-mixer" ;;
        SDL2_ttf) echo "sdl2-ttf" ;;
        jpeg*) echo "libjpeg-turbo" ;;
        png*) echo "libpng" ;;
        webp*) echo "libwebp" ;;
        avif) echo "libavif" ;;
        yuv) echo "libyuv" ;;
        lzma) echo "liblzma" ;;
        sharpyuv) echo "libwebp" ;; # License covered by libwebp
        FLAC) echo "libflac" ;;
        asound) echo "alsa" ;;
        glib-2.0) echo "glib" ;;
        modplug) echo "libmodplug" ;;
        ogg) echo "libogg" ;;
        vorbis) echo "libvorbis" ;;
        vorbisfile) echo "libvorbis" ;; # Same package as libvorbis
        pcre2-8) echo "pcre2" ;;
        brotlicommon|brotlidec) echo "brotli" ;;
        bz2) echo "bzip2" ;;
        fluidsynth) echo "fluidsynth" ;;
        mpg123) echo "mpg123" ;;
        opus) echo "opus" ;;
        opusfile) echo "opusfile" ;;
        wavpack) echo "wavpack" ;;
        freetype) echo "freetype" ;;
        tiff) echo "tiff" ;;
        wayland*) echo "wayland-client" ;;
        z) echo "zlib" ;;
        *) echo "$base_name" ;;
    esac
}

# Function to perform sanity check for overlapping dependencies
sanity_check_overlaps() {
    echo "Performing sanity check for overlapping dependencies..."
    declare -A file_to_dirs
    declare -A file_to_hashes

    for dir in "$ARTIFACT_BASE_DIR"/*/runtimes/linux-x64/native; do
        if [ -d "$dir" ]; then
            lib_name=$(basename "$(dirname "$(dirname "$(dirname "$dir")")")")
            for file in "$dir"/*.so*; do
                if [ -f "$file" ]; then
                    file_name=$(basename "$file")
                    file_to_dirs["$file_name"]="${file_to_dirs["$file_name"]} $lib_name"
                    hash=$(sha256sum "$file" | awk '{print $1}')
                    file_to_hashes["$file_name:$lib_name"]="$hash"
                fi
            done
        fi
    done

    overlaps_found=0
    for file in "${!file_to_dirs[@]}"; do
        dirs="${file_to_dirs[$file]}"
        dir_count=$(echo "$dirs" | wc -w)
        if [ "$dir_count" -gt 1 ]; then
            overlaps_found=1
            echo "Overlap detected: $file appears in: $dirs"
            first_hash=""
            identical=true
            for dir in $dirs; do
                current_hash="${file_to_hashes[$file_name:$dir]}"
                if [ -z "$first_hash" ]; then
                    first_hash="$current_hash"
                elif [ "$current_hash" != "$first_hash" ]; then
                    identical=false
                    echo "WARNING: $file has different contents in $dir (hash: $current_hash) vs previous (hash: $first_hash)"
                fi
            done
            if $identical; then
                echo "OK: $file is identical across all directories (hash: $first_hash)"
            fi
        fi
    done

    if [ $overlaps_found -eq 0 ]; then
        echo "No overlapping dependencies found."
    else
        echo "Sanity check complete. Review overlaps above."
    fi
}

# Main SDL2 library base names
MAIN_LIB_NAMES="libSDL2 libSDL2_image libSDL2_gfx libSDL2_mixer libSDL2_ttf"

# Track copied files to avoid duplicates
declare -A COPIED_FILES

# Bundle per main library
for lib_name in $MAIN_LIB_NAMES; do
    ARTIFACT_DIR="$ARTIFACT_BASE_DIR/$lib_name/$SUBDIR"
    mkdir -p "$ARTIFACT_DIR"
    mkdir -p "$ARTIFACT_DIR/licenses"

    # Reset LICENSE_PACKAGES for each library
    unset LICENSE_PACKAGES
    declare -A LICENSE_PACKAGES=()

    # Find all files related to the main library
    MAIN_LIB_FILES=$(find "$VCPKG_DIR/lib/" -name "${lib_name}.so*" -o -name "${lib_name}-*.so*")

    # Copy all related files for the main library
    for file in $MAIN_LIB_FILES; do
        if [ -z "${COPIED_FILES[$file]}" ]; then
            cp -a "$file" "$ARTIFACT_DIR/"
            COPIED_FILES[$file]=1
        fi
    done

    # Add main library package to licenses
    main_package=$(map_lib_to_package "$lib_name.so")
    if [ -f "$VCPKG_DIR/share/$main_package/copyright" ]; then
        LICENSE_PACKAGES["$main_package"]=1
    else
        echo "Warning: No license found for main package $main_package"
    fi

    # Collect and copy vcpkg-provided dependencies for one representative file
    REPRESENTATIVE_FILE=$(find "$VCPKG_DIR/lib/" -name "${lib_name}-*.so*" -not -type l | head -n 1)
    if [ -n "$REPRESENTATIVE_FILE" ]; then
        DEPS=$(collect_all_deps "$REPRESENTATIVE_FILE")
        for dep in $DEPS; do
            DEP_RELATED_FILES=$(find_related_files "$dep")
            for dep_file in $DEP_RELATED_FILES; do
                dep_base_name=$(basename "$dep_file" | sed 's/\.so.*//')
                skip=false
                for main_lib in $MAIN_LIB_NAMES; do
                    if [ "$dep_base_name" = "$main_lib" ]; then
                        skip=true
                        break
                    fi
                done
                if [ "$skip" = false ] && [ -z "${COPIED_FILES[$dep_file]}" ]; then
                    cp -a "$dep_file" "$ARTIFACT_DIR/"
                    COPIED_FILES[$dep_file]=1
                    dep_package=$(map_lib_to_package "$dep_file")
                    if [ -f "$VCPKG_DIR/share/$dep_package/copyright" ]; then
                        LICENSE_PACKAGES["$dep_package"]=1
                    else
                        echo "Warning: No license found for dependency package $dep_package (from $dep_file)"
                    fi
                fi
            done
        done
        # Add sdl2 license for all satellite libraries (except libSDL2 itself)
        if [ "$lib_name" != "libSDL2" ] && [ -f "$VCPKG_DIR/share/sdl2/copyright" ]; then
            LICENSE_PACKAGES["sdl2"]=1
        fi
    fi

    # Copy license files for the main library and its dependencies
    for pkg in "${!LICENSE_PACKAGES[@]}"; do
        cp "$VCPKG_DIR/share/$pkg/copyright" "$ARTIFACT_DIR/licenses/$pkg.txt" 2>/dev/null
    done

    echo "Packaged $lib_name. Artifacts are in $ARTIFACT_DIR/"
    echo "Libraries:"
    ls "$ARTIFACT_DIR"/*.so* 2>/dev/null || echo "No libraries found"
    echo "Licenses:"
    ls "$ARTIFACT_DIR/licenses"/*.txt 2>/dev/null || echo "No licenses found"
done

# Perform sanity check for overlapping dependencies
sanity_check_overlaps

echo "Packaging and sanity check complete."