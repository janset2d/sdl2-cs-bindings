# Janset.SDL2 - Modular C# Bindings for SDL2 & Friends

**Modern, modular, and robust C# bindings for SDL2 and its satellite libraries (SDL_image, SDL_ttf, SDL_mixer, SDL2_gfx).**

This project provides comprehensive C# bindings, heavily based on the excellent [SDL-CS](https://github.com/flibitijibibo/SDL2-CS) project, and bundles pre-compiled native libraries built consistently via [Vcpkg](https://github.com/microsoft/vcpkg). Our goal is to offer an easy-to-use, flexible, and reliable way for .NET developers to integrate SDL2 functionalities into their cross-platform applications.

[![Build Status](https://github.com/janset/sdl2-cs-bindings/actions/workflows/release-candidate-pipeline.yml/badge.svg?branch=master)](https://github.com/janset/sdl2-cs-bindings/actions/workflows/release-candidate-pipeline.yml) <!-- Replace with your actual workflow badge once active -->
[![NuGet (Core)](https://img.shields.io/nuget/v/Janset.SDL2.Core.svg)](https://www.nuget.org/packages/Janset.SDL2.Core/) <!-- Example, update once published -->
[![NuGet (Native Core)](https://img.shields.io/nuget/v/Janset.SDL2.Core.Native.svg)](https://www.nuget.org/packages/Janset.SDL2.Core.Native/) <!-- Example, update once published -->

## 🌱 Motivation & Vision

The primary motivation behind Janset.SDL2 was to create a robust set of SDL2 bindings to serve as the foundation for **Janset2D**, a new cross-platform 2D game framework (named after my daughter, Janset). While these bindings are integral to Janset2D (which will also be open-sourced soon), they are designed to be a standalone, contribution that can benefit any .NET developer looking to leverage the power of SDL2.

## ✨ Key Features

* **Comprehensive Bindings:** Covers SDL2, SDL_image, SDL_mixer, SDL_ttf, and SDL2_gfx.
* **Modular Design:**
  * Separate NuGet packages for each library (e.g., `Janset.SDL2.Core`, `Janset.SDL2.Image`).
  * Separate native library packages (e.g., `Janset.SDL2.Core.Native`, `Janset.SDL2.Image.Native`).
  * Include only what you need, keeping your application lean.
* **Cross-Platform Native Binaries:**
  * Pre-compiled native libraries for:
    * Windows (x64, x86, ARM64)
    * Linux (x64, ARM64)
    * macOS (x64, ARM64 - Apple Silicon)
  * Built reliably using **Vcpkg** with a defined set of features (see `vcpkg.json`).
* **Automatic Native Library Handling:**
  * Native packages correctly place binaries in `runtimes/{rid}/native/`.
  * For Linux and macOS, symbolic links are preserved within a `native.tar.gz` archive, which is automatically extracted at build time by the consuming project via included MSBuild targets. This ensures correct behavior of shared libraries on these platforms.
* **Modern .NET:** Targets a wide range of .NET runtimes (details TBD upon first release).
* **Actively Developed:** With a focus on robust CI/CD for reliable packaging and releases.

## 🚀 Project Status

**Actively Under Development.**

* Core bindings are functional.
* Cross-platform native library harvesting (including Windows, Linux, and macOS) is implemented.
* A comprehensive CI/CD pipeline for automated testing, packaging, and internal releases is currently being finalized.
* The first official pre-release packages are expected soon.

We are excited to make these bindings available to the .NET community and welcome feedback!

## 📦 Getting Started (Intended Usage)

Once packages are published, you'll typically add them to your .NET project like so:

```bash
# For the core SDL2 bindings
dotnet add package Janset.SDL2.Core

# For the corresponding native SDL2 libraries
dotnet add package Janset.SDL2.Core.Native
```

Similarly for other libraries:

```bash
dotnet add package Janset.SDL2.Image
dotnet add package Janset.SDL2.Image.Native

dotnet add package Janset.SDL2.Mixer
dotnet add package Janset.SDL2.Mixer.Native
# ...and so on for TTF and Gfx.
```

The native packages will automatically ensure the correct native binaries are copied to your build output for the target runtime.

## 🛠️ Building from Source (For Contributors / Advanced Users)

If you wish to build the libraries yourself:

1. Clone this repository: `git clone https://github.com/janset/sdl2-cs-bindings.git --recursive` (ensure submodules are initialized for Vcpkg).
2. Ensure you have the [.NET SDK (currently 9.0.x)](https://dotnet.microsoft.com/download) installed.
3. Vcpkg will be bootstrapped by the build process.
4. For detailed build, harvesting, and packaging instructions, please refer to our documentation:
    * **[Foundational Project & Build Plan](./docs/cake-build-plan.md):** The original blueprint detailing project goals, the Cake-based build system architecture, native dependency strategies, and initial phased feature implementation.
    * **[Native Binary Harvesting Process](./docs/harvesting-process.md):** Deep dive into how native libraries are collected.
    * **[CI/CD Packaging and Release Plan](./docs/ci-cd-packaging-and-release-plan.md):** Detailed plan for our automated build, packaging, and release workflows.

## 🗺️ Roadmap (High-Level)

* **H2 2025:** <!-- Adjusted quarter based on typical development pace -->
  * Finalize and stabilize Phase 1 of the `Release-Candidate-Pipeline` (automated internal packaging).
  * Publish initial pre-release versions of all packages to an internal feed for testing.
  * Publish first official public pre-release/release to NuGet.org.
* **Future:**
  * Implement Phase 2 & 3 enhancements for the CI/CD pipeline (tag-based automation, public promotion workflow, maintenance checks).
  * Comprehensive smoke tests and example projects.
  * Ongoing updates to SDL library versions.
  * Community feedback incorporation.

## 📚 Documentation

For more in-depth information, please explore the `docs/` directory:

* **[CI/CD Packaging and Release Plan](./docs/ci-cd-packaging-and-release-plan.md):** The primary document detailing our automated build, packaging, and release strategy.
* **[Native Binary Harvesting Process](./docs/harvesting-process.md):** Explains the mechanics of how native SDL2 libraries and their dependencies are collected.
* **[Foundational Project & Build Plan](./docs/cake-build-plan.md):** The original blueprint detailing project goals, the Cake-based build system architecture, native dependency strategies, and initial phased feature implementation.
* **[Architectural Review](./docs/architectural-review.md):** Provides an overview of the system's architecture, strengths, and areas for improvement (including macOS support status).
* **[Architectural Review: Core Harvesting Components](./docs/architectural-review-core-components.md):** A detailed look at the internal design of the harvesting logic and potential refactorings in the future.

## 🙏 Acknowledgements

* This project is heavily based on and inspired by [SDL-CS](https://github.com/flibitijibibo/SDL2-CS) by Ethan Lee.
* Native libraries are built using [Vcpkg](https://github.com/microsoft/vcpkg).
* Build automation powered by [Cake Frosting](https://cakebuild.net/).

## 🤝 Contributing

(Contributions will be welcome once the initial release and CI/CD pipeline are stabilized. A `CONTRIBUTING.md` will be added at that time.)

## 📜 License

This project is licensed under the **MIT License**. See the [LICENSE](./LICENSE) file for details. <!-- Assuming zlib, please confirm and add LICENSE file -->
