# Phased Plan for `sdl2-cs-bindings` Project (Revision 11)

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
    * Create `src/`, `src/native/`, `build/`, `samples/`, `tests/`, `config/`. Place `*.Native.*` projects in `src/native/`.
4. **Central Build Configuration (`Directory.Build.props`):**
    * Create at root. Define common properties (TargetFrameworks, LangVersion, Nullable, Authors, Company, Repo info, PackageLicenseFile, **VersionPrefix**,
        **VersionSuffix**, SourceLink, Common Analyzer Settings, CPM flag, etc.). Ensure `<Platforms>` is not defined globally.
5. **Native Build Configuration (`src/native/Directory.Build.props`):**
    * Create in `src/native/`. Import `../../Directory.Build.props`.
    * Define common properties for content-only native packages: `<NoBuild>true</NoBuild>`, `<IncludeBuildOutput>false</IncludeBuildOutput>`,
        `<SuppressDependenciesWhenPacking>true</SuppressDependenciesWhenPacking>`, `<TargetFramework>netstandard2.0</TargetFramework>`.
    * Override inherited properties irrelevant for native packages: `<IncludeSymbols>false</IncludeSymbols>`, `<RunAnalyzersDuringBuild>false</RunAnalyzersDuringBuild>`,
        `<PublishRepositoryUrl>false</PublishRepositoryUrl>`, etc.
    * Define common `<ItemGroup>`s for packaging content from `runtimes/`, `build/`, `buildTransitive/`, `licenses/` using appropriate `PackagePath` logic (e.g., `runtimes\%(RecursiveDir)` or `runtimes\%(Link)`).
6. **Central Package Versions (`Directory.Packages.props`):**
    * Create at root. Enable CPM (`<ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>`).
    * Define `<PackageVersion>` for all external dependencies (analyzers, SourceLink).
    * Define `<PackageVersion>` for all **internal** packages (`Janset.SDL2.Core`, `Janset.SDL2.Native.Core`, etc.). Use specific versions for native packages (e.g., "2.30.3") and MSBuild properties (e.g., `$(SDLBindingVersion)`) for binding packages.
7. **Build Targets File (`Directory.Build.targets`):** Create at root (initially empty).
8. **SDK Version Pinning (`global.json`):**
    * Create at root to pin .NET SDK (e.g., 9.x).
    * `dotnet new globaljson --sdk-version <your-chosen-sdk-version>`
9. **Native Version & Dependency Tracking (`config/versions.json`):**
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
10. **Nuke Build Setup:**
    * Install/Update Nuke global tool.
    * Run `nuke :setup` in the repo root.
11. **Root README:** Update with repo description and goals.
12. **Initial Commit:** Commit foundational structure.

---

**Phase 1: Core Packages (`Native.Core` & `Core`) (Days 2-5)**

* **Phase 1a: `Janset.SDL2.Native.Core` Package**
    1. **Create Project:** `src/native/SDL2.Core.Native/SDL2.Core.Native.csproj`.
    2. **Configure Project:** Set only `<PackageId>` and `<Description>`. Version defined in `Directory.Packages.props`. Inherits content-only structure and content rules from `src/native/Directory.Build.props`.
    3. **Manual Native Asset Handling:** Create `runtimes/{RID}/native/` folders, copy/rename real `SDL2` native libs. Exclude system libs.
    4. **Include Native Assets:** Handled by common rules in `src/native/Directory.Build.props`.
    5. **.NET Framework & Common Targets:** Create `src/native/SDL2.Core.Native/buildTransitive/Janset.SDL2.Native.Core.targets`. Add MSBuild copy logic (conditioned on TFM). Include via common rules.
    6. **Include Licenses/Notices:** Create `src/native/SDL2.Core.Native/licenses/`, copy SDL2 `LICENSE.txt`, create `THIRD-PARTY-NOTICES.txt`. Include via common rules.
    7. **Pack:** `dotnet pack` (Nuke will eventually override version with `/p:Version=...`).

* **Phase 1b: `Janset.SDL2.Core` Package**
    1. **Create & Configure Project:** `src/SDL2.Core/SDL2.Core.csproj`. Set `<PackageId>`, `<Description>`, `<RootNamespace>`. Version defined in `Directory.Packages.props` via `$(SDLBindingVersion)`.
    2. **Link SDL-CS Files:** Link core `*.cs` files from submodule.
    3. **Add Project Reference:** Add `<ProjectReference Include="..\native\SDL2.Core.Native\SDL2.Core.Native.csproj" PrivateAssets="All"/>`. `PrivateAssets="All"` prevents SDK pack from incorrectly bundling native assets or promoting transitive dependencies directly. Standard SDK pack automatically converts this to the correct `<PackageReference>` using the version from `Directory.Packages.props`.
    4. **Include SDL2-CS License:** Create `src/SDL2.Core/licenses/`, copy SDL2-CS license, add `<ItemGroup>` to `.csproj` to include it (`<Content Include="licenses\..." Pack="true" PackagePath="licenses\" />`).
    5. **Pack:** `dotnet pack`. Verify dependency in `.nuspec` points to the correct `Janset.SDL2.Core.Native` version from `Directory.Packages.props`.
    6. **Testing:** Create `samples/CoreTest`. Add `Janset.SDL2.Core`. Test `SDL_Init/Quit`.
    7. **Commit:** Commit working Core packages.

---

**Phase 2: Image Packages (`Native.Image` & `Image`) (Days 5-7)**

* **Phase 2a: `Janset.SDL2.Native.Image` Package**
    1. **Create & Configure Project:** `src/native/SDL2.Image.Native/SDL2.Image.Native.csproj`. Set `<PackageId>`, `<Description>`. Version from `Directory.Packages.props`. Inherits content-only structure.
    2. **Manual Native Asset Handling:** Create `runtimes/`. Copy/rename `SDL2_image` lib & unique deps (referencing `versions.json`). Exclude `SDL2` & system libs.
    3. **Include Native Assets:** Handled by common rules.
    4. **.NET Framework Targets:** Create `src/native/SDL2.Image.Native/build/net462/Janset.SDL2.Native.Image.targets`. Add MSBuild copy logic for `SDL2_image.dll` and unique deps. Include via common rules.
    5. **Include Licenses/Notices:** Create `src/native/SDL2.Image.Native/licenses/`, copy licenses, create/update notices. Include via common rules.
    6. **Pack:** `dotnet pack`.

* **Phase 2b: `Janset.SDL2.Image` Package**
    1. **Create & Configure Project:** `src/SDL2.Image/SDL2.Image.csproj`. Set `<PackageId>`, `<Description>`. Version inherited.
    2. **Link SDL_image.cs:** Link binding file.
    3. **Add Project References:** Add `<ProjectReference Include="..\SDL2.Core\SDL2.Core.csproj" />` and `<ProjectReference Include="..\native\SDL2.Image.Native\SDL2.Image.Native.csproj" />`. Standard SDK pack converts these to `<PackageReference>` items using versions from `Directory.Packages.props`.
    4. **Pack:** `dotnet pack`. Verify dependencies in `.nuspec` correctly reference `Janset.SDL2.Core` and `Janset.SDL2.Image.Native`.
    5. **Testing:** Create/update `samples/ImageTest`. Add `Janset.SDL2.Image`. Test image loading.
    6. **Commit:** Commit working Image packages.

---

**Phase 3: TTF, Mixer, GFX Packages (Days 7-10)**

1. **Repeat Phase 2 Structure:** Create `.Native.Satellite` and `.Satellite` packages. Use content-only structure for native.
2. **Native Assets:** Bundle main satellite lib and unique runtime dependencies (informed by `versions.json`). Include via common rules.
3. **Dependencies:** Add `<ProjectReference Include="..\SDL2.Core\SDL2.Core.csproj" />` and `<ProjectReference Include="..\native\SDL2.*.Native\SDL2.*.Native.csproj" />`. Standard SDK pack converts these.
4. **.NET Framework Targets:** Create and include necessary `.targets` files in `build/net462/` for each `.Native.Satellite` package.
5. **Build, Pack, Test:** Verify functionality.
6. **Commit:** Commit each working set.

---

**Phase 4: Testing, Samples & Nuke Build Refinement (Days 10-13)**

1. **Unit Tests:** Set up test projects (`tests/SDL2.Core.Tests`, etc.). Write basic P/Invoke tests.
2. **Headless Integration Tests:** Design and implement configurable headless tests. Add to `tests/`.
3. **Integration/Sample Tests:** Refine samples.
4. **Nuke Build Targets:** Refine Nuke targets (`Clean`, `Compile`, `Test`, `Pack`). Ensure `Pack` target passes `/p:Version=...` to `dotnet pack` calls, overriding base versions defined via CPM/props.
5. **Commit:** Commit testing and Nuke build improvements.
6. **Native AOT Testing:** Test native AOT assemblies.

---

**Phase 5: Documentation & Final Polish (Days 13-15)**

1. **Package READMEs:** Write `README.md` for each `src/` project. Configure `<PackageReadmeFile>`.
2. **Root README Update:**
    * Enhance main `README.md` with project overview, how to build, contribution guidelines, and links to the individual packages.
    * **Document dependency structure:** Explain that satellite packages (Image, TTF, etc.) automatically bring in `Janset.SDL2.Core` (and subsequently `Janset.SDL2.Core.Native`) as transitive dependencies.
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
