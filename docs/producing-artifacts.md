# Roadmap for Producing Native Artifacts with vcpkg

This roadmap outlines the steps to create a reliable pipeline for building and packaging native artifacts using vcpkg. The artifacts will target `runtimes/win-x64`, `runtimes/linux-x64`, `runtimes/linux-arm64`, `runtimes/win-arm64`, and `runtimes/osx`, with an initial focus on x64 architectures. The process emphasizes creating reliable scripts first, followed by automation and optimization using GitHub Actions and caching mechanisms. License files will be included in the artifact folders.

---

## Phase 1: Focus on x64 Architectures

### 1. **Linux (x64)**
   - **Build Environment**:
     - Use a Docker container based on Ubuntu 18.04 for compatibility with older glibc versions (e.g., glibc 2.27).
     - Install build tools (e.g., `g++`, `cmake`, `git`) and dependencies in the container.
   - **vcpkg Setup**:
     - Clone the vcpkg repository and bootstrap it inside the container.
     - Install required libraries (e.g., SDL2) using `vcpkg install sdl2:x64-linux-dynamic`.
   - **Dependency Identification**:
     - Use `ldd` to list shared library dependencies of the built binaries.
     - Filter out system libraries and retain only vcpkg-provided `.so` files.
   - **Artifact Packaging**:
     - Copy the identified `.so` files to `runtimes/linux-x64/native`.
     - Include license files from vcpkg’s `share` directory in the artifact folder.
   - **Scripting**:
     - Develop a shell script to automate the above steps, ensuring reliability and repeatability.

### 2. **Windows (x64)**
   - **Build Environment**:
     - Use a Windows environment with Visual Studio tools and CMake installed.
   - **vcpkg Setup**:
     - Clone and bootstrap vcpkg.
     - Install required libraries using `vcpkg install sdl2:x64-windows-dynamic`.
   - **Dependency Identification**:
     - Use `dumpbin /dependents` or a similar tool to identify required `.dll` files.
     - Exclude system libraries (e.g., `kernel32.dll`).
   - **Artifact Packaging**:
     - Copy the necessary `.dll` files to `runtimes/win-x64/native`.
     - Include license files in the artifact folder.
   - **Scripting**:
     - Create a batch or PowerShell script to automate the process.

### 3. **macOS (x64)**
   - **Build Environment**:
     - Use a macOS environment with CMake and other tools installed (e.g., via Homebrew).
   - **vcpkg Setup**:
     - Clone and bootstrap vcpkg.
     - Install required libraries using `vcpkg install sdl2:x64-osx-dynamic`.
   - **Dependency Identification**:
     - Use `otool -L` to identify required `.dylib` files.
     - Filter out system libraries (e.g., `/usr/lib/libSystem.B.dylib`).
   - **Artifact Packaging**:
     - Copy the necessary `.dylib` files to `runtimes/osx/native`.
     - Include license files in the artifact folder.
   - **Scripting**:
     - Develop a shell script for macOS to automate the build and packaging.

---

## Phase 2: Extend to ARM64 Architectures

### 1. **Linux (ARM64)**
   - **Build Environment**:
     - Use a Docker container with Ubuntu 18.04 for ARM64, potentially requiring cross-compilation or an ARM64 runner.
   - **vcpkg Setup**:
     - Install libraries targeting `arm64-linux-dynamic`.
   - **Artifact Packaging**:
     - Package `.so` files into `runtimes/linux-arm64/native` with licenses.

### 2. **Windows (ARM64)**
   - **Build Environment**:
     - Use a Windows environment with ARM64 support or cross-compilation tools.
   - **vcpkg Setup**:
     - Install libraries targeting `arm64-windows-dynamic`.
   - **Artifact Packaging**:
     - Package `.dll` files into `runtimes/win-arm64/native` with licenses.

---

## Phase 3: Optimize the Pipeline

- **Caching**:
  - Cache vcpkg’s build artifacts to avoid recompilation across builds.
  - For Linux, leverage Docker layer caching by creating a custom Ubuntu 18.04 image with vcpkg pre-installed.
- **Automation**:
  - Integrate the scripts into GitHub Actions workflows using available runners (Windows, macOS) and Docker for Linux.
- **Parallelization**:
  - Use GitHub Actions’ matrix strategy to build for multiple platforms concurrently.

---

## Key Notes

- **Script Focus**: In Phase 1, prioritize creating robust, platform-specific scripts that can later be integrated into GitHub Actions.
- **Docker for Linux**: Use Ubuntu 18.04 in a Docker container to target older glibc versions, as GitHub’s minimum Ubuntu runner is 20.04.
- **Licenses**: Ensure all artifact folders include relevant license files from vcpkg.

This roadmap starts with Linux x64 scripting and progresses through x64 platforms before extending to ARM64 and optimization.