# Phased Plan for `sdl2-cs-bindings` Project (Revision 3)

**Goal:** Create a set of modular, cross-platform NuGet packages for SDL2 C# bindings (based on SDL-CS). Separate packages will provide managed bindings and native binaries. Native binaries will be built via vcpkg.

**Repository:** `janset2d/sdl2-cs-bindings`
**NuGet Prefix:** `Janset.SDL2.*` (e.g., `Janset.SDL2.Core`, `Janset.SDL2.Native.Core`)
**License (Bindings):** MIT
**License (Dependencies):** zlib (SDL, SDL-CS), others as needed.
**Target Frameworks (Bindings):** `net9.0`, `net8.0`, `netstandard2.0`, `net462`
**Initial Target RIDs (Native):** `win-x64`, `win-x86`, `win-arm64`, `linux-x64`
**Build Tool:** Nuke Build (`nuke.build`)

---

**Phase 0: Foundation & Repository Setup (Day 1-2)**

1.  **Create GitHub Repository:**
    * Create public repository `janset2d/sdl2-cs-bindings`.
    * Initialize with `README.md`, `.gitignore` (.NET template), `LICENSE` (MIT).
2.  **Add SDL-CS Submodule:**
    * Add `git@github.com:flibitijibibo/SDL2-CS.git` as a submodule in `external/SDL2-CS`.
    * `git commit -m "Add SDL2-CS submodule"`
3.  **Basic Folder Structure:**
    * Create `src/`, `build/`, `samples/`, `tests/`, `config/`.
4.  **Central Build Configuration (`Directory.Build.props`):**
    * Create at root. Define common properties (TargetFrameworks, LangVersion, Nullable, Authors, Company, Repo info, PackageLicenseFile, VersionPrefix `0.1.0`, SourceLink, etc.). Use the previously reviewed version.
5.  **Central Package Versions (`Directory.Packages.props`):**
    * Create at root. Define versions for analyzers, SourceLink using `<PackageVersion>`.
6.  **Build Targets File (`Directory.Build.targets`):**
    * Create at root (initially empty).
7.  **SDK Version Pinning (`global.json`):**
    * Create at root to pin .NET SDK (e.g., 9.x).
    * `dotnet new globaljson --sdk-version <your-chosen-sdk-version>`
8.  **Native Version Tracking (`config/native-versions.json`):**
    * Create this file to define target versions for native libraries. Automation will use this later (Phase 6). Manual reference for now.
    * Example content:
      ```json
      {
        "SDL2": "2.30.3", // Example version
        "SDL2_image": "2.8.2", // Example version
        "SDL2_ttf": "2.22.0", // Example version
        "SDL2_mixer": "2.8.0", // Example version
        "SDL2_gfx": "1.0.4", // Example version
        "//": "Add versions for dependencies like libpng, freetype etc. as needed"
      }
      ```
9.  **Nuke Build Setup:**
    * Install/Update Nuke global tool.
    * Run `nuke :setup` in the repo root.
10. **Root README:** Update with repo description and goals.
11. **Initial Commit:** Commit foundational structure.

---

**Phase 1: Core Packages (`Native.Core` & `Core`) (Days 2-5)**

* **Phase 1a: `Janset.SDL2.Native.Core` Package**
    1.  **Create Project:** `src/Janset.SDL2.Native.Core/Janset.SDL2.Native.Core.csproj`.
    2.  **Configure Project:** Set `<PackageId>`, `<Description>`, `<Version>` (from `native-versions.json`).
    3.  **Manual Native Asset Handling:** Create `runtimes/`, copy/rename real `SDL2` native libs for initial RIDs. Exclude system libs.
    4.  **Include Native Assets:** Add `<Content Include="runtimes\**\*.dll;runtimes\**\*.so" ... />`.
    5.  **.NET Framework Targets:** Create and include `build/net462/Janset.SDL2.Native.Core.targets` with copy logic.
    6.  **Include Licenses/Notices:** Create `licenses/`, copy SDL2 `LICENSE.txt`, create `THIRD-PARTY-NOTICES.txt`. Include in package.
    7.  **Build & Pack:** `dotnet pack`.

* **Phase 1b: `Janset.SDL2.Core` Package**
    1.  **Create & Configure Project:** `src/SDL2.Core/SDL2.Core.csproj`. Set `<PackageId>`, `<Description>`, `<RootNamespace>`, inherit `$(VersionPrefix)`.
    2.  **Link SDL-CS Files:** Link core `*.cs` files from submodule.
    3.  **Add NuGet Dependency:** Add `<PackageReference Include="Janset.SDL2.Native.Core" Version="$({NativeCoreVersion})" />`.
    4.  **Include SDL2-CS License:** Create `licenses/`, copy SDL2-CS license, include in package.
    5.  **Build & Pack:** `dotnet pack`. Verify dependency.
    6.  **Testing:** Create `samples/CoreTest`. Add `Janset.SDL2.Core`. Test `SDL_Init/Quit`.
    7.  **Commit:** Commit working Core packages.

---

**Phase 2: Image Packages (`Native.Image` & `Image`) (Days 5-7)**

* **Phase 2a: `Janset.SDL2.Native.Image` Package**
    1.  **Create & Configure Project:** `Janset.SDL2.Native.Image.csproj`. Set `<PackageId>`, `<Description>`, `<Version>` (from `native-versions.json`).
    2.  **Manual Native Asset Handling:** Create `runtimes/`. Copy/rename `SDL2_image` lib. Copy/rename unique runtime dependencies (libpng, etc.). Exclude `SDL2` & system libs.
    3.  **Include Native Assets:** Add `<Content Include... />`.
    4.  **.NET Framework Targets:** Create and include `build/net462/Janset.SDL2.Native.Image.targets`.
    5.  **Include Licenses/Notices:** Copy licenses for SDL2_image and dependencies. Create/update `THIRD-PARTY-NOTICES.txt`. Include.
    6.  **Build & Pack:** `dotnet pack`.

* **Phase 2b: `Janset.SDL2.Image` Package**
    1.  **Create & Configure Project:** `SDL2.Image.csproj`. Set `<PackageId>`, `<Description>`, inherit `$(VersionPrefix)`.
    2.  **Link SDL_image.cs:** Link binding file.
    3.  **Add Dependencies:** Conditional `ProjectReference`/`PackageReference` to `Janset.SDL2.Core` (with PrivateAssets). Standard `PackageReference` to `Janset.SDL2.Native.Image`.
    4.  **Build & Pack:** Build, then `dotnet pack /p:IsPacking=true`. Verify dependencies.
    5.  **Testing:** Create/update `samples/ImageTest`. Add `Janset.SDL2.Image`. Test image loading.
    6.  **Commit:** Commit working Image packages.

---

**Phase 3: TTF, Mixer, GFX Packages (Days 7-10)**

1.  **Repeat Phase 2 Structure:** Create `*.Native.Satellite` and `*.Satellite` packages for TTF, Mixer, GFX.
2.  **Native Assets:** Bundle only main satellite lib and unique runtime dependencies in `*.Native.Satellite`.
3.  **Dependencies:** Ensure `*.Satellite` depends conditionally on `Janset.SDL2.Core` (PrivateAssets) and directly on `*.Native.Satellite`.
4.  **Build, Pack, Test:** Verify basic functionality.
5.  **Commit:** Commit each working set.

---

**Phase 4: Testing, Samples & Nuke Build Refinement (Days 10-13)**

1.  **Unit Tests:** Set up test projects (`tests/SDL2.Core.Tests`, etc.). Write basic P/Invoke tests.
2.  **Headless Integration Tests:**
    * Design and implement tests that call a wide range of binding APIs (Core, Image, TTF, Mixer, GFX) without requiring a graphical display or interactive input (e.g., load/save images, render text to surface, mix audio buffers, check properties).
    * Add these tests to the `tests/` structure.
    * Make tests configurable (e.g., via environment variables or build parameters) to run in full interactive mode (for manual testing) or headless mode (for CI).
3.  **Integration/Sample Tests:** Refine samples, add run instructions.
4.  **Nuke Build Targets:** Refine Nuke targets (`Clean`, `Compile`, `Test`, `Pack`). Ensure `Test` target can run headless tests. Ensure `Pack` uses `/p:IsPacking=true`. Define properties for native package versions from `native-versions.json`.
5.  **Commit:** Commit testing (including headless) and Nuke build improvements.

---

**Phase 5: Documentation & Final Polish (Days 13-15)**

1.  **Package READMEs:** Write `README.md` for each `src/` project. Configure `<PackageReadmeFile>`.
2.  **Root README Update:** Enhance main `README.md`.
3.  **API Documentation (Optional):** Consider DocFX.
4.  **Review NuGet Metadata:** Double-check all package metadata.
5.  **Review Licenses/Notices:** Manually verify content and inclusion of all `LICENSE` and `THIRD-PARTY-NOTICES.txt` files.
6.  **SourceLink/Symbols:** Verify SourceLink/symbols.
7.  **Commit:** Commit documentation and final polishing.

---

**Phase 6: Core Automation (vcpkg & Native Assets) (Deferred - Future Phase)**

1.  **Automated vcpkg Builds:** Set up CI/Nuke targets to bootstrap pinned vcpkg, potentially update ports based on `native-versions.json`, and run `vcpkg install` for all needed packages/features/triplets.
2.  **Automated Asset Identification (Linkage Analysis):** Implement Nuke target using `ldd`/`dumpbin` to identify runtime dependencies, filtering against System Library Exclusion List and Core SDL2.
3.  **Automated Asset Copying:** Implement Nuke/MSBuild target logic to copy identified primary and dependency native libs to staging `runtimes/` folders, renaming and running `patchelf` on Linux `.so` files.
4.  **Automated `THIRD-PARTY-NOTICES.txt` Generation:** Implement Nuke target to gather licenses and generate notices for `*.Native.*` packages.
5.  **Integration & Testing:** Ensure Nuke orchestrates the full flow (vcpkg -> analysis -> copy -> notices -> compile -> test -> pack). Thoroughly test automation.

---

**Phase 7: Advanced Automation & Maintenance (Deferred - Future Phase)**

1.  **SDL Release Monitoring:**
    * Implement a scheduled GitHub Action (or external monitor).
    * Action checks the official `libsdl-org/SDL` repository (e.g., via GitHub API for releases/tags).
    * If a new release version is found that's newer than the version in `config/native-versions.json`:
        * Action automatically updates `config/native-versions.json` with the new SDL version.
        * Action creates a Pull Request titled "Bump SDL2 version to X.Y.Z" with the changes to `native-versions.json`.
        * (Optionally: Trigger the Phase 6 CI build on the PR branch to test the new version).
2.  **SDL-CS Commit Monitoring:**
    * Implement a similar scheduled GitHub Action (or external monitor).
    * Action checks the `SDL-CS/SDL2-CS` repository for new commits on its main branch since the commit currently used by the submodule (`git submodule status`).
    * If new commits are found:
        * Action creates a Pull Request titled "Update SDL-CS submodule to latest commit" which runs `git submodule update --remote external/SDL2-CS`.
        * (Optionally: Trigger a CI build on the PR branch to test compatibility).
3.  **Dependency Update Automation (Optional):** Consider tools like Dependabot for updating C# dependencies (`Directory.Packages.props`) or potentially even vcpkg baseline updates if using manifests heavily.

---
