# **Cake Frosting Build Plan: janset2d/sdl2-cs-bindings**

## **1\. Introduction**

This document consolidates the discussions, architectural decisions, policy choices, and the final implementation plan for creating a robust, cross-platform build system using Cake Frosting for the janset2d/sdl2-cs-bindings project. It serves as a blueprint for development, capturing the consensus reached through collaborative planning.

## **2\. Project Goals**

The primary objectives for this build system are:

* **Modular NuGet Packages:** Produce two sets of packages under the Janset.SDL2.\* prefix:
  * Managed C\# bindings (Janset.SDL2.Core, Janset.SDL2.Image, etc.) targeting net9.0, net8.0, netstandard2.0, net462.
  * Native binaries (Janset.SDL2.Native.Core, Janset.SDL2.Native.Image, etc.) targeting multiple Runtime Identifiers (RIDs), initially win-x64, win-x86, win-arm64, linux-x64, with plans for macOS and ARM64 expansion.
* **Vcpkg Sourcing:** Utilize Vcpkg (managed via Git submodule) to build and source the required native SDL libraries and their dependencies with specific features enabled per target platform/triplet.
* **Native Harvesting & Packaging:** Automate the extraction of required binaries (DLLs/SOs), transitive dependencies, and license files from the Vcpkg installation directory. Package these correctly into the Janset.SDL2.Native.\* NuGet packages using the runtimes/{rid}/native structure.
* **Sanity Checking:** Implement checks to validate the harvested artifacts, potentially including checks for overlapping files with differing content across different packages.
* **GitHub Releases:** Create and upload archives (ZIP/TAR) of the harvested native binaries for each target RID as assets attached to GitHub Releases.
* **Managed Bindings Pipeline:** Implement a separate but related pipeline to version, build, and pack the managed binding NuGet packages, ensuring they reference the correct versions of the corresponding native packages.
* **Selective Builds:** Enable the build process (especially Vcpkg install and native harvesting) to be triggered for specific SDL libraries (e.g., only sdl2 and sdl2-image) based on manual input or automated detection (e.g., manifest changes).
* **CI/CD Integration:** Fully integrate the build process with GitHub Actions, utilizing matrix strategies for multi-RID builds and automating testing, packaging, and release workflows.

## **3\. Core Architectural Decisions**

The build system will be implemented using Cake Frosting, following these architectural principles:

* **Project Structure:** A layered structure within the build/ directory:
  * BuildContext/: Contains the lean BuildContext and a PathService for centralized, semantic path construction.
  * Tasks/: Contains task definitions acting as "workflow glue," orchestrating calls to modules. Grouped logically (e.g., DotNet/, Vcpkg/, Packaging/).
  * Modules/: Contains reusable, Cake-agnostic domain logic (e.g., dependency scanning, Vcpkg harvesting) as pure C\# classes/interfaces. Designed for testability.
  * Tools/: Contains thin wrappers (Tool\<T\> derived classes and aliases) for external CLI tools like dumpbin.
* **Lean** BuildContext**:** The BuildContext will primarily hold configuration values (passed via DI) and provide access to the PathService and potentially intermediate build state (like harvested file lists). It avoids holding complex logic or numerous path properties directly.
* **Path Management (**PathService**):** A dedicated PathService (injected into BuildContext) will handle all path construction logic, providing semantic methods (e.g., Paths.VcpkgInstallBin(triplet), Paths.ArtifactNative(lib, rid)). It will internally use Cake's path types (DirectoryPath, FilePath) and methods (Combine, CombineWithFilePath) for robustness and cross-platform compatibility. It will use git rev-parse \--show-toplevel via context.StartProcess to reliably locate the repository root for constructing paths relative to it (e.g., for the Vcpkg submodule).
* **Domain Modules (**Modules/**):** Core logic like dependency scanning (IDependencyScanner with WindowsDumpbinScanner, LinuxLddScanner implementations) and artifact harvesting (VcpkgHarvester) will reside in this layer, free from Cake dependencies, enabling unit testing.

### Dependency Resolution Strategy

The system employs a hybrid, three-tier approach for discovering and resolving native dependencies:

1. **Recursive Runtime Analysis (Primary Source)**
   - Use platform-specific tools (dumpbin/ldd/otool) through IDependencyScanner implementations
   - Recursively analyze each direct dependency to build a complete dependency tree
   - Filter out system libraries that don't need to be distributed
   - This captures what binaries actually need at runtime

2. **Package Metadata Analysis (Supplementary)**
   - Query vcpkg for package metadata using `x-package-info --x-json --x-installed`
   - Use this comprehensive metadata to identify:
     * All direct dependencies (including those from enabled features)
     * Package version information (useful for versioning and auditing)
     * Features enabled on the package
     * Complete list of files owned by the package (for harvesting and validation)
   - Recursively analyze these additional dependencies
   - Cross-reference with runtime dependencies to ensure completeness

3. **Manual Overrides (Safety Net)**
   - Maintain a list of known edge cases (controlled via configuration)
   - Apply these overrides when specific libraries are detected
   - Enables handling of special cases that automated methods can miss

4. **Final Merge Process**
   - Combine dependencies from all three sources
   - Eliminate duplicates
   - Create a flattened dependency list for harvesting

This comprehensive approach ensures all required dependencies are captured, even those that might be missed by any single method.

* **Task Orchestration (**Tasks/**):** Tasks will be kept lean, primarily responsible for:
  * Defining dependencies (\[IsDependentOn\]).
  * Selecting and invoking appropriate domain modules based on context (e.g., platform, arguments).
  * Handling exceptions thrown by modules according to the defined failure policy.
  * Logging progress and status using context.Log, context.Warning, context.Error.
* **Dependency Injection (DI):** Cake's ConfigureServices will be used to register configuration objects (BuildSettings, VcpkgSettings, VersionManifest) and potentially core services (like IDependencyScanner implementations, VcpkgHarvester) as singletons. These will be injected into the BuildContext and potentially tasks.
* **Argument Parsing:** System.CommandLine will be used in Program.cs to handle potentially complex arguments (selective library builds, RIDs, flags like \--use-overrides). Parsed arguments will be captured in POCOs (like BuildSettings) and registered via DI for use in BuildContext and tasks. Cake's context.Argument may be used for simpler, built-in parameters like \--target, \--verbosity.
* **Tool Wrappers (**Tools/**):** External tools like dumpbin will be wrapped using the Tool\<T\> pattern, encapsulating tool path resolution (PATH \-\> Smart Fallback \-\> Override) and argument building logic. Specific aliases will provide a clean interface for tasks.

### Cake Frosting Project Structure (`build/`)

This document outlines the target directory structure for the Cake Frosting build project located within the `build/` folder of the `janset2d/sdl2-cs-bindings` repository.

```text
build/
│
├── Build.csproj                  # Main C# project file for the build host. References Cake.Frosting, other addins/libs.
├── Program.cs                    # Application entry point. Configures CakeHost, sets up DI, parses args (System.CommandLine).
│
├── BuildContext/                 # Contains classes related to the build context.
│   ├── BuildContext.cs           # The main FrostingContext implementation. Lean, holds injected config/services.
│   ├── PathService.cs            # Helper service (DI) for constructing canonical paths (Vcpkg, artifacts, etc.).
│   ├── BuildSettings.cs          # POCO for core build configuration (injected via DI).
│   ├── VcpkgSettings.cs          # POCO for Vcpkg-specific configuration (injected via DI).
│   └── VersionManifest.cs        # POCO representing version.json (injected via DI).
│
├── Tasks/                        # Contains all Cake task definitions ([TaskName]). Acts as workflow glue.
│   ├── Common/                   # Tasks applicable across different workflows (optional grouping).
│   │   ├── CleanTask.cs
│   │   └── InfoTask.cs           # Example: Prints environment info (Phase 0.5).
│   ├── DotNet/                   # Tasks related to building/packaging the .NET binding projects (optional).
│   │   ├── BuildBindingsTask.cs
│   │   └── PackBindingsTask.cs
│   ├── Vcpkg/                    # Tasks related to Vcpkg interaction and native harvesting.
│   │   ├── SubmoduleInitTask.cs
│   │   ├── VcpkgBootstrapTask.cs
│   │   ├── VcpkgInstallTask.cs     # Installs requested packages/features.
│   │   ├── HarvestTask.cs          # Parameterized task to harvest natives for a specific library/RID.
│   │   └── SanityCheckTask.cs      # Validates harvested artifacts.
│   └── Packaging/                # Tasks related to creating final packages/releases.
│       ├── ComputeVersionTask.cs   # Reads version.json, calculates final versions.
│       ├── PackNativeTask.cs       # Creates the Janset.SDL2.Native.* NuGet packages.
│       └── PublishLocalTask.cs     # Task for Phase 4.5 dry-run publish.
│
├── Modules/                      # Reusable, Cake-agnostic domain logic (pure C#). Testable.
│   ├── DependencyAnalysis/       # Logic for analyzing native dependencies.
│   │   ├── IDependencyScanner.cs   # Interface for platform-specific scanners.
│   │   ├── WindowsDumpbinScanner.cs # Implementation using DumpbinTool.
│   │   └── LinuxLddScanner.cs      # Implementation using LddTool.
│   │   └── MacOtoolScanner.cs      # Implementation using OtoolTool.
│   ├── VcpkgHarvester/           # Core logic for extracting files/licenses from Vcpkg.
│   │   └── VcpkgHarvester.cs     # Uses IDependencyScanner, PathService, VcpkgTool etc.
│   └── VcpkgSupport/             # Helpers related to Vcpkg (optional grouping).
│       └── TripletService.cs       # Generates Vcpkg triplet strings from RID/features.
│
├── Tools/                        # Wrappers for external command-line tools.
│   ├── Dumpbin/                  # Example for dumpbin.
│   │   ├── DumpbinTool.cs        # Tool<T> implementation for dumpbin.exe.
│   │   ├── DumpbinSettings.cs    # Settings classes for different dumpbin operations.
│   │   └── DumpbinAliases.cs     # Extension methods (aliases) for ICakeContext.
│   ├── Ldd/                      # Wrapper for ldd.
│   │   ├── LddTool.cs
│   │   ├── LddSettings.cs
│   │   └── LddAliases.cs
│   ├── Vcpkg/                    # Wrapper for vcpkg.
│   │   ├── VcpkgTool.cs
│   │   ├── VcpkgSettings.cs      # Settings for bootstrap, install etc.
│   │   └── VcpkgAliases.cs
│   ├── Otool/                    # Wrapper for otool (macOS).
│   │   ├── OtoolTool.cs
│   │   ├── OtoolSettings.cs
│   │   └── OtoolAliases.cs
│
├── Properties/
│   └── launchSettings.json       # For local debugging profiles (optional).
│
└── (Other files like .editorconfig, global using directives, etc.)
```

### Explanation of Top-Level Folders

* **BuildContext:** Defines the state and configuration available to tasks, keeping the main BuildContext.cs lean by using helper services like PathService and injected configuration POCOs.
* **Tasks:** Contains the workflow definitions. Each class typically represents one build step (e.g., cleaning, compiling, harvesting, packaging). Tasks orchestrate calls to Modules/ and Tools/.
* **Modules:** Holds the core "business logic" of the build (e.g., how to analyze dependencies, how to gather files from Vcpkg). These classes should not depend on Cake APIs directly, making them unit

## **4\. Key Policy Decisions**

The following policies have been agreed upon:

1. **Versioning & Dependencies:** **Coupled Patch** strategy. Managed binding packages (Janset.SDL2.\*) get at least a patch bump when their corresponding native package (Janset.SDL2.Native.\*) updates. A single authoritative version.json file in the repo root will define native library versions and the shared bindingsPatch number. The managed binding .csproj files will use SDK-style \<ProjectReference\>s to the native projects, which dotnet pack converts to \<PackageReference\>s. The version will be injected via $(Version) (e.g., \<ProjectReference Include="..." Version="$(Version)" /\>), with a fallback mechanism (e.g., \<Version Condition="'$(Version)' \== ''"\>0.0.1-local\</Version\>) defined in the binding .csproj files for local development outside the Cake build.
2. **Vcpkg Management:** Vcpkg will be managed as a **Git submodule**. The build will ensure it's initialized and updated. **GitHub Actions caching** (keyed on Vcpkg SHA, triplet, package list, caching buildtrees and packages) will be implemented to optimize build times.
3. **Platform Support:** Initial RIDs: win-x64, win-x86, win-arm64, linux-x64, osx-arm64. Architecture will be designed with **future osx-x64 support** in mind.
4. **Selective Build Trigger:** Support for **both manual** (\--library LibName) **and manifest-based** (version.json changes) triggers will be implemented.
5. **License Compliance:** **Basic approach:** Copy Vcpkg copyright files into artifact licenses/ folders. SBOM generation is deferred.
6. **Release Artifacts:**
   * **NuGet:** Separate native packages (Janset.SDL2.Native.\*) per library.
   * **GitHub Releases:** **Separate archives per library per RID** (e.g., Janset.SDL2.Native.Core-win-x64-vX.Y.Z.P.zip).
   * **Tagging:** **Per-Library Tags** (e.g., Janset.SDL2.Core-vX.Y.Z.P) will be used. Tags **without pre-release labels** (e.g., \-beta, \-preview) will trigger the release workflow.
7. **Failure Policy (CI):** **Strict.** Failure of any requested component task (e.g., Harvest-SDL2\_image-win-x64) will cause that specific task and its corresponding CI matrix job to fail. The overall workflow run will be marked as failed if *any* job fails (strategy.fail-fast: false in matrix).
8. **Native Overrides:** Support will be implemented via a flag (\--use-overrides). Overrides **take precedence** when the flag is active. A configurable path (e.g., via `\--overridesPath /abs/path`) can override the default location (repo-root/overrides/{rid}); `PathService` will expose methods to resolve the correct path. For *local testing* of the override *logic* (before S3 integration is built), a canonical folder like repo-root/overrides/{rid}/ (added to .gitignore) can be used. Storage of override binaries for CI usage is planned for **AWS S3**, but the implementation of this feature (including S3 download logic, potentially tested with LocalStack) is **deferred** to a later phase.
9. **Symbol Files:** **Publish symbols (**.snupkg**) for managed bindings**. Native symbol harvesting/publishing is **deferred** (low priority).
10. **Testing:** Initial reliance on **manual integration testing**. A formal **unit test project (**build.Tests**)** for the Modules/ layer is planned for the future.
11. **Code Formatting:** Code style for the build/ project will be enforced using standard .NET formatting tools (dotnet format) leveraging the existing .editorconfig, Directory.Build.props, and Directory.packages.props files.

## **5\. Detailed Phased Implementation Plan**

The implementation will follow these incremental phases:

* **Phase 0.5: "Hello Cake" CI Smoke Test**
  * Goal: Basic CI setup validation.
  * Tasks: Create a minimal GitHub Actions workflow that checks out the code (including submodule placeholder) and runs a simple Cake target (e.g., Info) to print environment details and confirm SDK availability.
* **Phase 1: Cake Project \- Core Setup**
  * Goal: Establish the foundational project structure and configuration flow.
  * Tasks:
    * Create build/ project with BuildContext/, Tasks/, Modules/, Tools/ folders.
    * Define initial BuildSettings, VcpkgSettings POCOs (consider array properties like LibrariesToBuild\[\] early).
    * Set up ConfigureServices in Program.cs to register settings (initially hardcoded or basic args).
    * Implement BuildContext with DI constructor.
    * Implement PathService with core path methods using Cake types and reliable repo root finding.
    * Add Submodule-Init task (git submodule update \--init \--recursive).
    * Add Vcpkg-Bootstrap task (runs ./bootstrap-vcpkg).
    * Establish code formatting standards (dotnet format, .editorconfig).
* **Phase 2: Cake Project \- Core Harvesting Logic (Single Target \- Windows)**
  * Goal: Prove end-to-end harvesting for one library on one platform.
  * Tasks:
    * Implement Tools/Dumpbin/ wrapper (DumpbinTool, alias).
    * Implement Modules/DependencyAnalysis/IDependencyScanner and WindowsDumpbinScanner.
    * Note: Future macOS implementation will likely use `otool -L`.
    * Implement core Modules/VcpkgHarvester/VcpkgHarvester.cs logic (file finding, dependency walking via scanner, file copying, basic license copying for Windows).
      * Use `vcpkg x-package-info --x-json` early to resolve known feature-driven dynamic dependencies (e.g., libmodplug -> libxmp) instead of hardcoded mapping.
    * Create a granular Harvest task (e.g., Harvest-SDL2-win-x64) using the harvester.
    * **Output:** Generate a manifest file (artifacts/harvest-SDL2-win-x64.json) listing harvested files for decoupling.
* **Phase 3: Cake Project \- Expansion & Refinement**
  * Goal: Add Linux support, parameterize tasks, integrate Vcpkg install.
  * Tasks:
    * Implement Modules/DependencyAnalysis/LinuxLddScanner (using a dedicated LddTool wrapper).
    * Refactor Harvest task(s) to be parameterized (accept library name, RID).
    * Implement Vcpkg package installation logic (using a dedicated VcpkgTool wrapper for operations like install, bootstrap).
    * Implement Vcpkg configuration, including:
      * Defining required features per library/RID (e.g., in `VcpkgSettings` or `vcpkg-features.json`) like `sdl2-mixer[opusfile,wavpack]:x64-windows-release`.
      * Using `VcpkgTool` to pass these features during `vcpkg install`.
    * Implement TripletService (or similar) to generate Vcpkg triplet strings reliably (e.g., win-x64 -> x64-windows-release, linux-x64 -> x64-linux-dynamic, osx-arm64 -> arm64-osx-dynamic). Document the RID <-> Triplet mapping.
    * **RID-Triplet Mapping:**
      | RID        | Vcpkg Triplet       |
      |------------|---------------------|
      | win-x64    | x64-windows-release |
      | win-x86    | x86-windows         |
      | win-arm64  | arm64-windows       |
      | linux-x64  | x64-linux-dynamic   |
      | linux-arm64| arm64-linux-dynamic |
      | osx-arm64  | arm64-osx-dynamic   |
      | osx-x64    | x64-osx-dynamic     |
    * Implement version.json reading and Compute-Version task. Register VersionManifest via DI.
* **Phase 4: Cake Project \- Packaging & Validation**
  * Goal: Create NuGet packages and perform sanity checks.
  * Tasks:
    * Implement native NuGet packaging task (PackNative) using a temporary staging project (NativePack.csproj) and dotnet pack, applying the computed version via /p:Version=.
    * Implement the SanityCheck task (reading harvested file lists/manifests from BuildContext or files).
    * (Optional) Implement tasks for building/packaging managed bindings using \<ProjectReference\> and version injection.
* **Phase 4.5: Local "Dry-Run" Publish & Smoke Test**
  * Goal: Validate packaging locally before public release.
  * Tasks:
    * Create a publish-local Cake target that pushes generated NuGet packages to a local NuGet feed, potentially run via a BaGet Docker container.
    * Add steps to verify packages (dotnet nuget verify).
    * Implement **headless SDL console application(s)** that reference the locally packed NuGet(s) and test core functionalities (e.g., image loading, audio playback with various formats) to catch packaging/RID/dependency issues. Provide an option for windowed execution for manual debugging.
    * Note: These test apps will reside under `/smoke/` (e.g., `/smoke/SDLTest/`) and be excluded from packing via `<EnableDefaultItems>false</EnableDefaultItems>` or similar in their `.csproj`.
    * Ensure tests cover specific features/dependencies: e.g., SDL_mixer (MOD, Opus, WavPack playback), SDL_image (PNG loading).
* **Phase 5: GitHub Actions \- Build & Test Integration**
  * Goal: Automate the build process in CI.
  * Tasks:
    * Set up the full GitHub Actions matrix workflow.
    * Integrate Vcpkg caching using actions/cache@v4 (caching buildtrees, packages, etc., keyed appropriately, e.g., "vcpkg-${{ hashFiles('vcpkg.json', '.git/modules/vcpkg/HEAD') }}-${{ matrix.triplet }}").
    * Add steps to execute the parameterized Cake build targets within the matrix jobs.
    * **Ensure Linux jobs run within an `ubuntu:18.04` Docker container** for glibc compatibility.
    * Add steps to **run the headless smoke test application(s)** (from Phase 4.5) using the generated Linux artifacts within Docker containers for target distributions (e.g., `centos:7`, `debian:buster`, `ubuntu:20.04`) to validate runtime compatibility.
    * Document the minimum required glibc version based on testing.
    * Ensure strategy.fail-fast: false is set and job failure reflects Cake task failure.
    * (Optional) Add a lightweight security/license scan step (e.g., using Syft) if desired.
    * Example: `syft packages dir:artifacts -o spdx-json` (potentially fail build on forbidden licenses).
    * Add subsequent step to parse SPDX JSON (e.g., using `jq`) and enforce license policy (e.g., fail on GPL).
* **Phase 6: GitHub Actions \- Release Workflow**
  * Goal: Automate tagging, NuGet publishing, and GitHub Releases.
  * Tasks:
    * Create a separate release workflow triggered by tags matching the per-library convention (Janset.\*-v\*) without pre-release labels.
    * Implement logic/script ("what changed?") to determine which libraries/versions were built successfully based on build artifacts or manifests (potentially reading version.json diff).
    * Add steps to create per-library Git tags based on computed versions.
    * Add steps to push NuGet packages (dotnet nuget push), using `NUGET_API_KEY` from GitHub Secrets.
    * Add steps to create/update GitHub Releases (using `GH_TOKEN` from Secrets) and upload corresponding native binary archives.
    * Note: `NUGET_API_KEY` and `GH_TOKEN` should be configured as repository-level secrets to allow forks to run CI/release steps (without actual publishing).
* **(Later Phases):**
  * Native override implementation (S3 download, potentially tested with LocalStack).
  * Comprehensive unit testing (build.Tests).
  * Automated bindingsPatch bumping.
  * SBOM generation.
  * Native symbol handling.
  * Code-signing/notarization (especially for Windows/macOS).
  * Robust Dynamic Dependency Handling: Replace/augment manual mapping in VcpkgHarvester by parsing Vcpkg package metadata (vcpkg x-package-info) to automatically identify runtime dependencies associated with installed features.
  * **Contribution Guidelines:** Create a `CONTRIBUTING.md` document detailing development setup, PR process, versioning policy enforcement (e.g., `version.json` updates), and secret management for forks.

## **6\. Conclusion**

This plan outlines a robust, maintainable, and scalable approach for the janset2d/sdl2-cs-bindings build system using Cake Frosting. By following the defined architecture, policies, and phased implementation, the project aims to achieve its goals efficiently while incorporating best practices for build automation and CI/CD. The next step is to begin implementation starting with Phase 0.5/Phase 1\.
