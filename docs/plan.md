# Phased Plan for `sdl2-cs-bindings` Project (Revision 2)

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
    1.  **Create Project:** `src/Janset.SDL2.Native.Core/Janset.SDL2.Native.Core.csproj` (standard classlib, can be minimal as it only packs content).
    2.  **Configure Project:** Set `<PackageId>Janset.SDL2.Native.Core</PackageId>`, `<Description>Native SDL2 libraries built via vcpkg.</Description>`. Set `<Version>` based on the SDL2 version in `native-versions.json` (e.g., `2.30.3`).
    3.  **Manual Native Asset Handling:**
        * Create `runtimes/{RID}/native/` folders for initial RIDs (`win-x64`, `win-x86`, `win-arm64`, `linux-x64`).
        * Manually copy the **real** `SDL2.dll` / `libSDL2.so` files from corresponding vcpkg builds into these folders. Rename Linux `.so` file. **Exclude** system libs like `libsystemd`, etc.
    4.  **Include Native Assets:** Add `<ItemGroup><Content Include="runtimes\**\*.dll;runtimes\**\*.so" Pack="true" PackagePath="runtimes\%(RecursiveDir)native\" /></ItemGroup>`.
    5.  **.NET Framework Targets:**
        * Create `build/net462/Janset.SDL2.Native.Core.targets` file.
        * Add MSBuild logic (using `<Copy>` task and `$(Platform)` condition) to copy the correct `SDL2.dll` (x86 or x64) from the package's `runtimes/win-*/native/` folder to the consuming project's output directory (`$(TargetDir)`).
        * Include this targets file in the package: `<ItemGroup><Content Include="build\**\*.targets" Pack="true" PackagePath="build\" /></ItemGroup>`.
    6.  **Include Licenses/Notices:** Create `licenses/` folder, copy SDL2 `LICENSE.txt`. Create basic `THIRD-PARTY-NOTICES.txt` listing SDL2 (zlib). Include in package via `<Content ... PackagePath="licenses\" />`.
    7.  **Build & Pack:** `dotnet pack src/Janset.SDL2.Native.Core/Janset.SDL2.Native.Core.csproj`.

* **Phase 1b: `Janset.SDL2.Core` Package**
    1.  **Create & Configure Project:** `src/SDL2.Core/SDL2.Core.csproj`. Set `<PackageId>Janset.SDL2.Core</PackageId>`, `<Description>`, `<RootNamespace>`. Inherit `$(VersionPrefix)` from `Directory.Build.props`.
    2.  **Link SDL-CS Files:** Link core `*.cs` files from submodule.
    3.  **Add NuGet Dependency:** Add `<PackageReference Include="Janset.SDL2.Native.Core" Version="$({NativeCoreVersion})" />` (Define `NativeCoreVersion` in `Directory.Build.props` based on `native-versions.json`, e.g., `2.30.3`).
    4.  **Include SDL2-CS License:** Create `licenses/` folder. Copy the SDL2-CS license file from `external/SDL2-CS/LICENSE.txt`. Add `<ItemGroup><Content Include="licenses\SDL2-CS-LICENSE.txt" Pack="true" PackagePath="licenses\" /></ItemGroup>` to include it.
    5.  **Build & Pack:** `dotnet pack src/SDL2.Core/SDL2.Core.csproj`. Verify dependency on `Janset.SDL2.Native.Core` in `.nuspec`.
    6.  **Testing:** Create `samples/CoreTest`. Add *only* `Janset.SDL2.Core` package from local source. Test `SDL_Init/Quit` on target platforms.
    7.  **Commit:** Commit working Core packages.

---

**Phase 2: Image Packages (`Native.Image` & `Image`) (Days 5-7)**

* **Phase 2a: `Janset.SDL2.Native.Image` Package**
    1.  **Create & Configure Project:** `Janset.SDL2.Native.Image.csproj`. Set `<PackageId>`, `<Description>`. Set `<Version>` based on SDL2_image version (e.g., `2.8.2`).
    2.  **Manual Native Asset Handling:** Create `runtimes/{RID}/native/`. Copy/rename `SDL2_image.dll`/`libSDL2_image.so`. Identify (manually for now) and copy/rename unique runtime dependencies (e.g., `libpng`, `libjpeg-turbo`, `zlib`, `libwebp`, `libavif`, `libyuv`, `libtiff`, `liblzma`). **Exclude** core `SDL2` and system libs.
    3.  **Include Native Assets:** Add `<Content Include... />`.
    4.  **.NET Framework Targets:** Create `build/net462/Janset.SDL2.Native.Image.targets` with copy logic for `SDL2_image.dll` and its unique dependencies based on `$(Platform)`. Include in package.
    5.  **Include Licenses/Notices:** Copy licenses for SDL2_image and dependencies (libpng, etc.). Create/update `THIRD-PARTY-NOTICES.txt`. Include in package.
    6.  **Build & Pack:** `dotnet pack`.

* **Phase 2b: `Janset.SDL2.Image` Package**
    1.  **Create & Configure Project:** `SDL2.Image.csproj`. Set `<PackageId>`, `<Description>`. Inherit `$(VersionPrefix)`.
    2.  **Link SDL_image.cs:** Link binding file.
    3.  **Add Dependencies:**
        * Conditional `ProjectReference` / `PackageReference` to `Janset.SDL2.Core` using `$(IsPacking)`. Use `<PrivateAssets>native;build</PrivateAssets>` on the `PackageReference`.
        * Standard `PackageReference` to `Janset.SDL2.Native.Image` (using a property like `$(NativeImageVersion)`).
    4.  **Build & Pack:** Build, then `dotnet pack /p:IsPacking=true`. Verify dependencies in `.nuspec`.
    5.  **Testing:** Create/update `samples/ImageTest`. Add `Janset.SDL2.Image` (which pulls Core + Natives). Test image loading.
    6.  **Commit:** Commit working Image packages.

---

**Phase 3: TTF, Mixer, GFX Packages (Days 7-10)**

1.  **Repeat Phase 2 Structure:** Create `*.Native.Satellite` and `*.Satellite` packages for TTF, Mixer, GFX.
2.  **Native Assets:** Bundle only the main satellite library and its unique, non-SDL2, non-system runtime dependencies in each `*.Native.Satellite` package.
3.  **Dependencies:** Ensure `*.Satellite` packages depend conditionally on `Janset.SDL2.Core` (with PrivateAssets) and directly on their corresponding `*.Native.Satellite` package.
4.  **Build, Pack, Test:** Verify basic functionality for each.
5.  **Commit:** Commit each working set of packages.

---

**Phase 4: Testing, Samples & Nuke Build Refinement (Days 10-12)**

1.  **Unit Tests:** Set up test projects (`tests/SDL2.Core.Tests`, etc.). Write basic P/Invoke tests.
2.  **Integration/Sample Tests:** Refine samples, add run instructions.
3.  **Nuke Build Targets:** Refine Nuke targets (`Clean`, `Compile`, `Test`, `Pack`). Ensure `Pack` uses `/p:IsPacking=true` and handles dependencies correctly. Define properties for native package versions based on `native-versions.json`.
4.  **Commit:** Commit testing and Nuke build improvements.

---

**Phase 5: Documentation & Final Polish (Days 12-14)**

1.  **Package READMEs:** Write `README.md` for each `src/` project. Configure `<PackageReadmeFile>`.
2.  **Root README Update:** Enhance main `README.md`.
3.  **API Documentation (Optional):** Consider DocFX.
4.  **Review NuGet Metadata:** Double-check all package metadata.
5.  **Review Licenses/Notices:** Manually verify content and inclusion of all `LICENSE` and `THIRD-PARTY-NOTICES.txt` files.
6.  **SourceLink/Symbols:** Verify SourceLink/symbols.
7.  **Commit:** Commit documentation and final polishing.

---

**Phase 6: Automation of vcpkg & Native Assets (Deferred - Future Phase)**

1.  **Automated vcpkg Builds:** Set up CI/Nuke targets to bootstrap pinned vcpkg, potentially update ports based on `native-versions.json`, and run `vcpkg install` for all needed packages/features/triplets.
2.  **Automated Asset Identification (Linkage Analysis):**
    * Implement Nuke target (runs after vcpkg build, before packing `*.Native.*` packages).
    * For each primary native library (SDL2, SDL2_image, etc.):
        * Execute `ldd` (Linux) or `dumpbin /dependents` (Windows).
        * Parse output to get runtime dependencies.
        * Filter list against a curated **System Library Exclusion List** (e.g., `libc.so.6`, `kernel32.dll`) and the **Core SDL2 library**.
3.  **Automated Asset Copying:**
    * Implement Nuke/MSBuild target logic.
    * For each `*.Native.*` package:
        * Copy its primary library (SDL2, SDL2_image, etc.) from vcpkg output to staging `runtimes/{RID}/native/`.
        * For dependencies identified via Linkage Analysis (and not excluded): Locate the real file in the corresponding vcpkg package directory, copy to staging `runtimes/{RID}/native/`, renaming as needed (handle Linux symlinks, use expected `DllImport` names).
    * Run `patchelf --set-rpath '$ORIGIN'` on all staged Linux `.so` files.
4.  **Automated `THIRD-PARTY-NOTICES.txt` Generation:**
    * Implement Nuke target (runs before `Pack`).
    * Identify all bundled native dependencies (from linkage analysis/copy step).
    * Gather corresponding license files (need a reliable way to locate them, perhaps from vcpkg package share dirs).
    * Generate `THIRD-PARTY-NOTICES.txt` for each `*.Native.*` package.
5.  **Integration:** Ensure Nuke orchestrates vcpkg -> linkage analysis -> asset copying -> notice generation -> compilation -> testing -> packing.
6.  **Testing:** Thoroughly test the fully automated process.

---
