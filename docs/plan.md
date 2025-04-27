# Phased Plan for `sdl2-cs-bindings` Project (Revision 1)

**Goal:** Create a set of modular, cross-platform NuGet packages for SDL2 C# bindings (based on SDL-CS), bundling necessary native libraries built via vcpkg.

**Repository:** `janset2d/sdl2-cs-bindings`
**NuGet Prefix:** `Janset.SDL2.*`
**License (Bindings):** MIT
**License (Dependencies):** zlib (SDL, SDL-CS), others as needed (libpng, freetype, etc.)
**Target Frameworks:** `net9.0`, `net8.0`, `netstandard2.0`, `net462`
**Build Tool:** Nuke Build (`nuke.build`)

---

**Phase 0: Foundation & Repository Setup (Day 1-2)**

1.  **Create GitHub Repository:**
    * Create the public repository `janset2d/sdl2-cs-bindings`.
    * Initialize with a `README.md`, `.gitignore` (standard .NET template), and an `MIT` license file (for your binding code).
2.  **Add SDL-CS Submodule:**
    * Add the `SDL-CS/SDL2-CS` repository as a Git submodule in `external/SDL2-CS`.
    * `git submodule add https://github.com/SDL-CS/SDL2-CS.git external/SDL2-CS`
    * `git commit -m "Add SDL2-CS submodule"`
3.  **Basic Folder Structure:**
    * Create top-level folders: `src/`, `build/`, `samples/`, `tests/`.
4.  **Central Build Configuration (`Directory.Build.props`):**
    * Create `Directory.Build.props` at the root.
    * Define common properties:
        * `<TargetFrameworks>net9.0;net8.0;netstandard2.0;net462</TargetFrameworks>`
        * `<LangVersion>latest</LangVersion>`
        * `<ImplicitUsings>enable</ImplicitUsings>` (or disable if preferred)
        * `<Nullable>enable</Nullable>`
        * `<Authors>Your Name/Org</Authors>`
        * `<Company>janset2d</Company>`
        * `<RepositoryUrl>https://github.com/janset2d/sdl2-cs-bindings</RepositoryUrl>`
        * `<PackageProjectUrl>https://github.com/janset2d/sdl2-cs-bindings</PackageProjectUrl>`
        * `<PackageLicenseExpression>MIT</PackageLicenseExpression>`
        * `<VersionPrefix>0.1.0</VersionPrefix>` (adjust as needed)
        * `<CheckEolTargetFramework>false</CheckEolTargetFramework>` (to suppress warnings for net462)
    * Configure SourceLink properties (e.g., `<PublishRepositoryUrl>true</PublishRepositoryUrl>`, `<EmbedUntrackedSources>true</EmbedUntrackedSources>`, `<IncludeSymbols>true</IncludeSymbols>`, `<SymbolPackageFormat>snupkg</SymbolPackageFormat>`).
5.  **Build Targets File:**
    * Create `Directory.Build.targets` at the root (can be mostly empty for now, automation targets will go here later).
6.  **SDK Version Pinning:**
    * Create `global.json` at the root to pin the .NET SDK version (use a version supporting .NET 9).
    * `dotnet new globaljson --sdk-version <your-chosen-sdk-version>`
7.  **Nuke Build Setup:**
    * Install/Update Nuke global tool: `dotnet tool install Nuke.GlobalTool --global`
    * Navigate to the repository root in the terminal.
    * Run `nuke :setup` and follow prompts to set up the build project (likely in the `build/` folder).
8.  **Root README:**
    * Update the root `README.md` with the chosen repository description and initial project goals.
9.  **Initial Commit:** Commit this foundational structure including the Nuke build project.

---

**Phase 1: `Janset.SDL2.Core` Package (Days 2-4)**

1.  **Create Core Project:**
    * Create the directory `src/SDL2.Core/`.
    * Inside it, create `SDL2.Core.csproj` (e.g., `dotnet new classlib -n SDL2.Core`). Ensure it inherits TargetFrameworks from `Directory.Build.props`.
2.  **Configure Core Project:**
    * Edit `SDL2.Core.csproj`.
    * Add `<PackageId>Janset.SDL2.Core</PackageId>`.
    * Add `<Description>Core C# bindings for SDL2 (based on SDL-CS).</Description>`.
    * Optionally add `<RootNamespace>SDL2</RootNamespace>`.
3.  **Link SDL-CS Files:**
    * In `SDL2.Core.csproj`, add `<ItemGroup>` to link the necessary core `*.cs` files from `../../external/SDL2-CS/src/` using `<Compile Include="..." Link="..."/>`.
4.  **Manual Native Asset Handling (Initial):**
    * Create the folder structure within `src/SDL2.Core/`: `runtimes/win-x64/native/`, `runtimes/win-x86/native/`, `runtimes/linux-x64/native/`.
    * Manually locate, copy the **real** file, and rename (Linux) the necessary core `SDL2` native libraries from your vcpkg builds into the corresponding `runtimes/.../native/` folders.
5.  **Include Native Assets in Package:**
    * In `SDL2.Core.csproj`, add the `<ItemGroup>` for `<Content Include="runtimes\**\*.dll;runtimes\**\*.so" ... />`.
6.  **Include Dependency Licenses & Notices:**
    * Create a `licenses/` folder within `src/SDL2.Core/`.
    * Manually copy the `LICENSE.txt` (or equivalent) files from the original SDL2 source and the SDL2-CS submodule into `src/SDL2.Core/licenses/`.
    * Manually create a `THIRD-PARTY-NOTICES.txt` file in `src/SDL2.Core/licenses/` summarizing the included dependencies (SDL2, SDL2-CS) and their licenses (zlib).
    * Add `<ItemGroup>` in `SDL2.Core.csproj` to include these files in the package:
        ```xml
        <ItemGroup>
          <Content Include="licenses\THIRD-PARTY-NOTICES.txt" Pack="true" PackagePath="licenses\" />
          <Content Include="licenses\SDL2_LICENSE.txt" Pack="true" PackagePath="licenses\" />
          <Content Include="licenses\SDL2-CS-LICENSE.txt" Pack="true" PackagePath="licenses\" />
        </ItemGroup>
        ```
7.  **Build & Pack (Manual):**
    * Build the project: `dotnet build src/SDL2.Core/SDL2.Core.csproj`.
    * Pack the project: `dotnet pack src/SDL2.Core/SDL2.Core.csproj`. Verify the `Janset.SDL2.Core.*.nupkg` is created.
8.  **Initial Testing:**
    * Create a temporary console application (`samples/CoreTest`).
    * Add the generated `Janset.SDL2.Core` package as a local source.
    * Write minimal code to call `SDL.SDL_Init(0)` and `SDL.SDL_Quit()`.
    * Test on target platforms.
9.  **Commit:** Commit the working `SDL2.Core` package.

---

**Phase 2: `Janset.SDL2.Image` Package (Days 4-6)**

1.  **Create & Configure Image Project:** As before (`SDL2.Image.csproj`, `<PackageId>`, `<Description>`). Add `<ProjectReference Include="../SDL2.Core/SDL2.Core.csproj" />`.
2.  **Link SDL_image.cs:** Link the binding file.
3.  **Manual Native Asset Handling:**
    * Create `runtimes/.../native/` folders.
    * Copy & rename `SDL2_image.dll` / `libSDL2_image.so` (real file).
    * Identify and copy/rename the **unique direct native dependencies** (e.g., `libpng`, `libjpeg-turbo`, `zlib`, etc.). **Exclude core SDL2 library.**
4.  **Include Native Assets:** Add `<Content Include... />` group.
5.  **Include Dependency Licenses & Notices:**
    * Create `licenses/` folder.
    * Copy license files for SDL2_image and its unique dependencies (libpng, etc.).
    * Manually create/update `THIRD-PARTY-NOTICES.txt` for this package, listing SDL2_image, libpng, etc., and their licenses.
    * Add `<Content Include="licenses\..." ... />` items to include them.
6.  **Configure NuGet Dependency:** Set up conditional `ProjectReference` / `PackageReference` for `Janset.SDL2.Core` using `$(IsPacking)`.
7.  **Build & Pack (Manual):** Build and pack (`dotnet pack /p:IsPacking=true`). Verify package contents and `.nuspec` dependency.
8.  **Testing:** Create/update sample (`samples/ImageTest`) referencing Core and Image packages. Test image loading.
9.  **Commit:** Commit the working `SDL2.Image` package.

---

**Phase 3: `Janset.SDL2.TTF`, `Janset.SDL2.Mixer`, `Janset.SDL2.GFX` Packages (Days 6-9)**

1.  **Repeat Phase 2:** Follow the steps from Phase 2 for `TTF`, `Mixer`, `GFX`.
2.  Identify and bundle only the **unique direct native dependencies** for each (e.g., `freetype` for TTF; `FLAC`, `mpg123`, `opusfile`, `vorbisfile`, etc. for Mixer).
3.  Create/update and include the respective license files and `THIRD-PARTY-NOTICES.txt` for each package.
4.  Set up conditional dependencies on `Janset.SDL2.Core`.
5.  Build, pack, test basic functionality for each.
6.  **Commit:** Commit each working package.

---

**Phase 4: Testing, Samples & Nuke Build Refinement (Days 9-11)**

1.  **Unit Tests:** Set up test projects (`tests/SDL2.Core.Tests`, etc.) referencing `src` projects. Write basic P/Invoke tests.
2.  **Integration/Sample Tests:** Refine samples, add run instructions.
3.  **Nuke Build Targets:**
    * In the Nuke build project (`build/Build.cs`), define initial targets:
        * `Clean`: Deletes build artifacts.
        * `Compile`: Builds all projects in the `src/` directory.
        * `Test`: Runs all tests in the `tests/` directory.
        * `Pack`: Packs all projects in `src/` (`/p:IsPacking=true`), outputting NuGet packages to an `artifacts/` directory.
    * Ensure Nuke targets work correctly.
4.  **Commit:** Commit testing and Nuke build improvements.

---

**Phase 5: Documentation & Final Polish (Days 11-13)**

1.  **Package READMEs:** Write `README.md` for each `src/` project. Configure `<PackageReadmeFile>` in `.csproj` files.
2.  **Root README Update:** Enhance main `README.md`.
3.  **API Documentation (Optional):** Consider setting up DocFX.
4.  **Review NuGet Metadata:** Double-check all package metadata.
5.  **Review Licenses/Notices:** Manually verify the content and inclusion of all `LICENSE` and `THIRD-PARTY-NOTICES.txt` files in the packages.
6.  **SourceLink/Symbols:** Verify SourceLink/symbols are working.
7.  **Commit:** Commit documentation and final polishing.

---

**Phase 6: Automation of vcpkg & Native Assets (Deferred - Future Phase)**

1.  **Automated vcpkg Builds:** Set up CI (GitHub Actions) or local Nuke targets to bootstrap vcpkg and run the necessary `vcpkg install` commands for all target platforms/configurations.
2.  **Automated Asset Copying (`Directory.Build.targets` / Nuke):**
    * Implement custom MSBuild targets or Nuke targets to automate copying/renaming native assets from vcpkg output directories to project `runtimes` folders before packing.
    * Logic needs to identify the correct source (vcpkg package dir) and destination (project runtimes dir) and select only the required files (main lib + unique deps) for each package.
3.  **Automated `THIRD-PARTY-NOTICES.txt` Generation (Nuke):**
    * Create a Nuke target that runs before `Pack`.
    * This target needs to:
        * Identify the direct and transitive dependencies for each package being packed (perhaps by parsing `project.assets.json` or using `dotnet list package --include-transitive`).
        * Gather the license files (copied manually in earlier phases or potentially located automatically).
        * Aggregate the license information into the `THIRD-PARTY-NOTICES.txt` file for each project.
    * Tools like `dotnet-project-licenses` might be adaptable or serve as inspiration for this Nuke target.
4.  **Integration:** Ensure the Nuke build orchestrates vcpkg builds, asset copying, notice generation, compilation, testing, and packing correctly.
5.  **Testing:** Thoroughly test the fully automated process.

---
