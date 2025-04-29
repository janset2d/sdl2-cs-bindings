# Phased Plan for `sdl2-cs-bindings` Project (Revision 9)

**Goal:** Create a set of modular, cross-platform NuGet packages for SDL2 C# bindings (based on SDL-CS). Separate packages will provide managed bindings and native binaries. Native
binaries will be built via vcpkg. Provide native binary archives via GitHub Releases.

**Repository:** `janset2d/sdl2-cs-bindings`
**NuGet Prefix:** `Janset.SDL2.*` (e.g., `Janset.SDL2.Core`, `Janset.SDL2.Native.Core`)
**License (Bindings):** MIT
**License (Dependencies):** zlib (SDL, SDL-CS), others as needed.
**Target Frameworks (Bindings):** `net9.0`, `net8.0`, `netstandard2.0`, `net462`
**Initial Target RIDs (Native):** `win-x64`, `win-x86`, `win-arm64`, `linux-x64`
**Build Tool:** Nuke Build (`nuke.build`)

---

**Phase 0: Foundation & Repository Setup (Day 1-2)**

1. **Create GitHub Repository:**
    * Create public repository `janset2d/sdl2-cs-bindings`.
    * Initialize with `README.md`, `.gitignore` (.NET template), `LICENSE` (MIT).
2. **Add SDL-CS Submodule:**
    * Add `git@github.com:flibitijibibo/SDL2-CS.git` as a submodule in `external/SDL2-CS`.
    * `git commit -m "Add SDL2-CS submodule"`
3. **Basic Folder Structure:**
    * Create `src/`, `build/`, `samples/`, `tests/`, `config/`. Keep `*.Native.*` projects side-by-side with binding projects in `src/`.
4. **Central Build Configuration (`Directory.Build.props`):**
    * Create at root. Define common properties (TargetFrameworks, LangVersion, Nullable, Authors, Company, Repo info, PackageLicenseFile, VersionPrefix `0.1.0`, SourceLink, etc.).
      Ensure `<Platforms>` is not defined globally. Use the reviewed version (Rev 2) which removed `<PackageLicenseExpression>` and `<GeneratePackageOnBuild>`.
5. **Central Package Versions (`Directory.Packages.props`):**
    * Create at root. Define versions for analyzers, SourceLink using `<PackageVersion>`.
6. **Build Targets File (`Directory.Build.targets`):**
    * Create at root. This file can later import a common `.targets` file for native asset copying for .NET Framework.
7. **SDK Version Pinning (`global.json`):**
    * Create at root to pin .NET SDK (e.g., 9.x).
    * `dotnet new globaljson --sdk-version <your-chosen-sdk-version>`
8. **Native Version & Dependency Tracking (`config/versions.json`):**
    * Create this file. Define target versions for native libraries (`SDL2`, `SDL2_image`, etc.).
    * **Enhancement:** Also list the expected *native runtime dependency* filenames (e.g., `libpng16.so`, `zlib1.dll`) and potentially their versions/vcpkg triplets for *each*
      `*.Native.*` package per target RID. This provides crucial input for Phase 6 automation (Linkage Analysis verification & Asset Copying). Manual definition initially.
    * Example structure:
      ```json
      {
        "Janset.SDL2.Native.Core": {
          "Version": "2.30.3", // Corresponds to native SDL2 version
          "Dependencies": {
             // Core SDL2 might have minimal direct bundled deps
          }
        },
        "Janset.SDL2.Native.Image": {
          "Version": "2.8.2", // Corresponds to native SDL2_image version
          "Dependencies": {
            "win-x64": ["libpng16.dll", "jpeg62.dll", "libwebp.dll", "..."], // Expected DLL names
            "linux-x64": ["libpng16.so", "libjpeg.so.62", "libwebp.so.7", "..."] // Expected SO names
            // Optionally add expected versions/triplets for these deps
          }
        }
        // ... other native packages
      }
      ```
9. **Nuke Build Setup:**
    * Install/Update Nuke global tool.
    * Run `nuke :setup` in the repo root.
10. **Root README:** Update with repo description and goals.
11. **Initial Commit:** Commit foundational structure.

---

**Phase 1: Core Packages (`Native.Core` & `Core`) (Days 2-5)**

* **Phase 1a: `Janset.SDL2.Native.Core` Package**
    1. **Create Project:** `src/Janset.SDL2.Native.Core/Janset.SDL2.Native.Core.csproj`.
    2. **Configure Project:** Set `<PackageId>`, `<Description>`. Set `<Version>` based on `versions.json`. Use the **content-only package structure** (`<NoBuild>true</NoBuild>`,
       `<IncludeBuildOutput>false</IncludeBuildOutput>`, `<SuppressDependenciesWhenPacking>true</SuppressDependenciesWhenPacking>`, inert `<TargetFramework>`,
       `<NoWarn>NU5128</NoWarn>`).
    3. **Manual Native Asset Handling:** Create `runtimes/`, copy/rename real `SDL2` native libs for initial RIDs. Exclude system libs.
    4. **Include Native Assets:** Add `<ItemGroup><Content Include="runtimes\**\*" Pack="true" PackagePath="runtimes\%(RecursiveDir)" /></ItemGroup>`.
    5. **.NET Framework & Common Targets:** Create `buildTransitive/Janset.SDL2.Native.Core.targets`. Add MSBuild copy logic (initially targeting `net462` via condition, but place
       file in `buildTransitive/` for universal inclusion). Include in package:
       `<ItemGroup><Content Include="buildTransitive\**\*.targets" Pack="true" PackagePath="buildTransitive\" /></ItemGroup>`.
    6. **Include Licenses/Notices:** Create `licenses/`, copy SDL2 `LICENSE.txt`, create `THIRD-PARTY-NOTICES.txt`. Include in package via
       `<Content ... PackagePath="licenses\" />`.
    7. **Build & Pack:** `dotnet pack`.

* **Phase 1b: `Janset.SDL2.Core` Package**
    1. **Create & Configure Project:** `src/SDL2.Core/SDL2.Core.csproj`. Set `<PackageId>`, `<Description>`, `<RootNamespace>`, inherit `$(VersionPrefix)`.
    2. **Link SDL-CS Files:** Link core `*.cs` files from submodule.
    3. **Add NuGet Dependency:** Add `<PackageReference Include="Janset.SDL2.Native.Core" Version="$({NativeCoreVersion})" />` (Define `NativeCoreVersion` based on
       `versions.json`).
    4. **Include SDL2-CS License:** Create `licenses/`, copy SDL2-CS license, include in package.
    5. **Build & Pack:** `dotnet pack`. Verify dependency.
    6. **Testing:** Create `samples/CoreTest`. Add `Janset.SDL2.Core`. Test `SDL_Init/Quit`.
    7. **Commit:** Commit working Core packages.

---

**Phase 2: Image Packages (`Native.Image` & `Image`) (Days 5-7)**

* **Phase 2a: `Janset.SDL2.Native.Image` Package**
    1. **Create & Configure Project:** `Janset.SDL2.Native.Image.csproj`. Set `<PackageId>`, `<Description>`, `<Version>` (from `versions.json`). Use content-only structure.
    2. **Manual Native Asset Handling:** Create `runtimes/`. Copy/rename `SDL2_image` lib. Copy/rename unique runtime dependencies (referencing `versions.json`). Exclude `SDL2` &
       system libs.
    3. **Include Native Assets:** Add `<Content Include="runtimes\**\*" ... PackagePath="runtimes\%(RecursiveDir)" />`.
    4. **.NET Framework & Common Targets:** Create and include `buildTransitive/Janset.SDL2.Native.Image.targets` (or a uniquely named file, e.g.,
       `buildTransitive/Janset.SDL2.Native.Image.Copy.targets`). Add MSBuild copy logic (initially targeting `net462`).
    5. **Include Licenses/Notices:** Copy licenses for SDL2_image and dependencies. Create/update `THIRD-PARTY-NOTICES.txt`. Include.
    6. **Build & Pack:** `dotnet pack`.

* **Phase 2b: `Janset.SDL2.Image` Package**
    1. **Create & Configure Project:** `SDL2.Image.csproj`. Set `<PackageId>`, `<Description>`, inherit `$(VersionPrefix)`.
    2. **Link SDL_image.cs:** Link binding file.
    3. **Add Dependencies:**
        * Conditional `ProjectReference`/`PackageReference` to `Janset.SDL2.Core` using `$(IsPacking)`. **(No PrivateAssets)**
        * Standard `PackageReference` to `Janset.SDL2.Native.Image` (using `$(NativeImageVersion)`).
    4. **Build & Pack:** Build, then `dotnet pack /p:IsPacking=true`. Verify dependencies.
    5. **Testing:** Create/update `samples/ImageTest`. Add `Janset.SDL2.Image`. Test image loading.
    6. **Commit:** Commit working Image packages.

---

**Phase 3: TTF, Mixer, GFX Packages (Days 7-10)**

1. **Repeat Phase 2 Structure:** Create `*.Native.Satellite` and `*.Satellite` packages. Use content-only structure for `*.Native.*`.
2. **Native Assets:** Bundle only main satellite lib and unique runtime dependencies (informed by `versions.json`) in `*.Native.Satellite`.
3. **Dependencies:** Ensure `*.Satellite` depends conditionally on `Janset.SDL2.Core` **(No PrivateAssets)** and directly on `*.Native.Satellite`.
4. **.NET Framework & Common Targets:** Create and include necessary `.targets` files in `buildTransitive/` for each `*.Native.Satellite` package.
5. **Build, Pack, Test:** Verify basic functionality.
6. **Commit:** Commit each working set.

---

**Phase 4: Testing, Samples & Nuke Build Refinement (Days 10-13)**

1. **Unit Tests:** Set up test projects (`tests/SDL2.Core.Tests`, etc.). Write basic P/Invoke tests.
2. **Headless Integration Tests:** Design and implement configurable headless tests. Add to `tests/`.
3. **Integration/Sample Tests:** Refine samples.
4. **Nuke Build Targets:** Refine Nuke targets (`Clean`, `Compile`, `Test`, `Pack`). Ensure `Test` runs headless tests. Ensure `Pack` uses `/p:IsPacking=true`. Define version
   properties from `versions.json`.
5. **Commit:** Commit testing and Nuke build improvements.
6. **Native AOT Testing:** Test native AOT assemblies.

---

**Phase 5: Documentation & Final Polish (Days 13-15)**

1. **Package READMEs:** Write `README.md` for each `src/` project. Configure `<PackageReadmeFile>`.
2. **Root README Update:**
    * Enhance main `README.md` with project overview, how to build, contribution guidelines, and links to the individual packages.
    * **Document dependency structure:** Explain that satellite packages (Image, TTF, etc.) automatically bring in `Janset.SDL2.Core` (and its native counterpart) as a transitive
      dependency.
3. **API Documentation (Optional):** Consider DocFX.
4. **Review NuGet Metadata:** Double-check.
5. **Review Licenses/Notices:** Manually verify.
6. **SourceLink/Symbols:** Verify.
7. **Commit:** Commit documentation and final polishing.

---

**Phase 6: Core Automation (vcpkg & Native Assets) (Deferred - Future Phase)**

1. **Automated vcpkg Builds:** Set up CI/Nuke targets to bootstrap pinned vcpkg, use `versions.json` to potentially update ports/select features, run `vcpkg install`.
2. **Automated Asset Identification (Linkage Analysis):** Implement Nuke target using `ldd`/`dumpbin`, filtering against System Library Exclusion List and Core SDL2. Use dependency
   lists in `versions.json` as a cross-reference/guide.
3. **Automated Asset Copying:** Implement Nuke/MSBuild target logic. Copy identified libs to staging `runtimes/` folders, renaming and running `patchelf`. Use correct
   `PackagePath`.
4. **Automated `THIRD-PARTY-NOTICES.txt` Generation:** Implement Nuke target.
5. **Integration & Testing:** Ensure Nuke orchestrates the full flow. Test automation.

---

**Phase 7: Advanced Automation & Maintenance (Deferred - Future Phase)**

1. **SDL Release Monitoring:** Implement GitHub Action to check `libsdl-org/SDL` releases and create PRs to update `versions.json`.
2. **SDL-CS Commit Monitoring:** Implement GitHub Action to check `SDL-CS/SDL2-CS` commits and create PRs to update submodule.
3. **GitHub Release Artifacts:** Add Nuke target (`CreateReleaseArchives`). Identify staged native libs, create platform archives (`.zip`, `.tar.gz`), include licenses/notices.
   Integrate with GitHub Actions to create Releases and upload assets on tag push.
4. **Dependency Update Automation (Optional):** Consider Dependabot.

---
