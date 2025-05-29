# **Cake Frosting Build Plan: janset2d/sdl2-cs-bindings**

## **1\. Introduction**

> **Note:** This document outlines the initial architectural and build plan for the `janset2d/sdl2-cs-bindings` project. While many foundational aspects regarding the Cake Frosting project structure and core harvesting logic remain relevant, the strategies for CI/CD, versioning, packaging, and release management have evolved. For the most current and detailed information on these topics, please refer to **[CI/CD Packaging and Release Plan](./ci-cd-packaging-and-release-plan.md)**. For a deep dive into the mechanics of the native binary harvesting process itself, see the **[Native Binary Harvesting Process](./harvesting-process.md)**.

This document consolidates the discussions, architectural decisions, policy choices, and the final implementation plan for creating a robust, cross-platform build system using Cake Frosting for the janset2d/sdl2-cs-bindings project. It serves as a blueprint for development, capturing the consensus reached through collaborative planning.

## **2\. Project Goals**

The primary objectives for this build system are:

* **Modular NuGet Packages:** Produce two sets of packages under the Janset.SDL2.\* prefix:
  * Managed C\# bindings (Janset.SDL2.Core, Janset.SDL2.Image, etc.) targeting net9.0, net8.0, netstandard2.0, net462.
  * Native binaries (Janset.SDL2.Native.Core, Janset.SDL2.Native.Image, etc.) targeting multiple Runtime Identifiers (RIDs), initially win-x64, win-x86, win-arm64, linux-x64, with plans for macOS and ARM64 expansion.
* **Vcpkg Sourcing:** Utilize Vcpkg (managed via Git submodule) to build and source the required native SDL libraries and their dependencies with specific features enabled per target platform/triplet.
* **Native Harvesting & Packaging:** Automate the extraction of required binaries (DLLs/SOs, including symbolic links on Linux), transitive dependencies, and license files from the Vcpkg installation directory. Package these correctly into the Janset.SDL2.Native.\* NuGet packages.
  * For Windows and macOS RIDs, this will utilize the standard `runtimes/{rid}/native` structure.
  * For Linux RIDs, to ensure the integrity of the shared library ecosystem, the following approach (based on preserving symbolic links) will be used:
    * Harvested libraries and their associated symbolic links will be packaged into an archive (e.g., a `.zip` file created with symlink preservation options, or a `.tar.gz` archive) within the NuGet package (e.g., located at `build/native/<linux-rid>/payload.archive`).
    * An MSBuild `.targets` file, bundled within the NuGet package (e.g., `build/<PackageId>.targets`), will contain logic to extract this archive into the consuming project's build output directory (e.g., `$(OutDir)`) during the build process on Linux. This ensures that symbolic links are correctly restored in the environment where the application will run.
* **Sanity Checking:** Implement checks to validate the harvested artifacts, potentially including checks for overlapping files with differing content across different packages.
* **GitHub Releases:** Create and upload archives of the harvested native binaries for each target RID as assets attached to GitHub Releases. For Linux RIDs, these archives (e.g., `.tar.gz` or `.zip` created with symlink preservation flags like `zip -y`) must faithfully preserve the complete symbolic link structure of the shared libraries.
* **Managed Bindings Pipeline:** Implement a separate but related pipeline to version, build, and pack the managed binding NuGet packages, ensuring they reference the correct versions of the corresponding native packages.
* **Selective Builds:** Enable the build process (especially Vcpkg install and native harvesting) to be triggered for specific SDL libraries (e.g., only sdl2 and sdl2-image) based on manual input or automated detection (e.g., manifest changes).
* **CI/CD Integration:** Fully integrate the build process with GitHub Actions, utilizing matrix strategies for multi-RID builds and automating testing, packaging, and release workflows. The detailed strategy for this integration is now documented in [CI/CD Packaging and Release Plan](./ci-cd-packaging-and-release-plan.md).

## **3\. Core Architectural Decisions**

The build system will be implemented using Cake Frosting, following these architectural principles:

* **Project Structure:** A layered structure within the `build/_build` directory:
    * `Context/`: Contains the lean `BuildContext`, configuration POCOs (`VcpkgSettings`, `VersionManifest`), and a `PathService`.
    * `Tasks/`: Contains task definitions acting as "workflow glue," orchestrating calls to modules. Grouped logically (e.g., `DotNet/`, `Vcpkg/`, `Packaging/`).
    * `Modules/`: Contains reusable, Cake-agnostic domain logic (e.g., dependency scanning, Vcpkg harvesting) as pure C# classes/interfaces. Designed for testability.
    * `Tools/`: Contains thin wrappers (`Tool<T>` derived classes and aliases) for external CLI tools like `dumpbin`, `ldd`, `otool`, `vcpkg`.
* **Lean BuildContext:** The `BuildContext` will primarily hold configuration values (passed via DI from parsed arguments and `config/versions.json`) and provide access to the `PathService` and potentially intermediate build state (like harvested file lists).
* **Path Management (PathService):** A dedicated `PathService` (injected into `BuildContext`) will handle all path construction logic, providing semantic methods (e.g., `Paths.VcpkgInstalledBin(triplet)`, `Paths.ArtifactNative(lib, rid)`). It will internally use Cake's path types and methods. It will use `git rev-parse --show-toplevel` via `context.StartProcess` to reliably locate the repository root.
* **Vcpkg Interaction (Manifest Mode):**
    * A `vcpkg.json` file at the repository root will define dependencies, desired features (with platform qualifiers), and exact version overrides for the core SDL libraries. A `builtin-baseline` will manage transitive dependency versions.
    * The `VcpkgInstallTask` in Cake will simply invoke `vcpkg install --triplet <triplet>`, relying on Vcpkg to read the root `vcpkg.json`.
    * The `VcpkgTool` wrapper will ensure commands are run from the repository root.
* **Domain Modules (Modules/):** Core logic like dependency scanning (`IDependencyScanner` with `WindowsDumpbinScanner`, `LinuxLddScanner`, `MacOtoolScanner` implementations) and artifact harvesting (`VcpkgHarvesterService`) will reside in this layer. The harvester will use tools like `vcpkg owns` or `vcpkg x-package-info` to identify package ownership for license gathering.
* **Task Orchestration (Tasks/):** Tasks will be kept lean, orchestrating calls to modules and tools.
* **Dependency Injection (DI):** Cake's `ConfigureServices` will be used to register configuration objects (`VcpkgSettings`, `VersionManifest` read from `config/versions.json`) and potentially core services (like `IDependencyScanner` implementations, `VcpkgHarvesterService`) as singletons.
* **Argument Parsing:** `System.CommandLine` will be used in `Program.cs` to handle arguments (`--rid`, `--library`, `--use-overrides`). Parsed arguments will be captured in POCOs and registered via DI.
* **Tool Wrappers (Tools/):** External tools (`dumpbin`, `ldd`, `otool`, `vcpkg`) will be wrapped using the `Tool<T>` pattern.

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

> **Note:** Some policies, particularly around versioning and release automation, have been refined. See [CI/CD Packaging and Release Plan](./ci-cd-packaging-and-release-plan.md) for the latest details.

The following policies have been agreed upon:

1.  **Versioning & Dependencies:**
    * **Native Version Control:** The **`vcpkg.json` manifest file (at repo root) is the source of truth for which native library versions Vcpkg installs**, using `overrides` and a `builtin-baseline`.
    * **NuGet Package Versioning:** The **`build/manifest.json` file is the single source of truth for the semantic versions used when packing all `Janset.SDL2.*` NuGet packages (both native and bindings).** It also specifies the target version of the underlying native components (e.g., the Vcpkg version of SDL2 to be used for a given package version). This approach ensures clear control over package releases. Manual updates to `vcpkg.json` (for Vcpkg-built library versions) and `build/manifest.json` (for package versions and targeted Vcpkg component versions) are made in the same PR for consistency. For more details, see the [CI/CD Packaging and Release Plan](./ci-cd-packaging-and-release-plan.md).
    * **Managed Bindings:** Use a **Coupled Patch** strategy. Managed packages (`Janset.SDL2.*`) get at least a patch bump when their corresponding native package updates. The `bindingsPatch` number in `build/manifest.json` can track this. Managed `.csproj` files use `<ProjectReference>` to native projects; `dotnet pack` converts these using the versions defined in `build/manifest.json` (injected via `/p:Version=`).
2.  **Vcpkg Management:** Vcpkg will be managed as a **Git submodule** (`external/vcpkg`). The build **does not** need a `SubmoduleInitTask`; CI (`actions/checkout@v4` with `submodules: recursive`) and local developers (`git submodule update --init --recursive`) are responsible for initialization. **GitHub Actions caching** will be implemented, keyed on the triplet, `vcpkg.json` hash, and Vcpkg ports hash.
3.  **Platform Support:** Initial RIDs: `win-x64`, `win-x86`, `win-arm64`, `linux-x64`, `osx-arm64`. Architecture designed for future `osx-x64` support.
4.  **Selective Build Trigger:** Vcpkg installs all dependencies from the manifest per triplet. The CI/CD pipeline, specifically the `Release-Candidate-Pipeline` workflow, dynamically determines which libraries require building and packaging based on changes, `manifest.json` versions, and the status of packages in the target NuGet feed. See [CI/CD Packaging and Release Plan](./ci-cd-packaging-and-release-plan.md).
5.  **License Compliance:** **Basic approach:** The `HarvestTask` will use `vcpkg owns` or similar to identify the package owning each harvested binary, then copy the corresponding `copyright` file from the Vcpkg `share/` directory into the artifact's `licenses/` folder. SBOM generation is deferred.
6.  **Release Artifacts:**
    * **NuGet:** Separate native packages (`Janset.SDL2.Native.*`) per library, and corresponding managed binding packages (`Janset.SDL2.*`).
    * **GitHub Releases:** Native binary archives may still be attached to GitHub Releases, but the primary distribution for consumption is NuGet. The CI/CD pipeline detailed in [CI/CD Packaging and Release Plan](./ci-cd-packaging-and-release-plan.md) handles the creation and publishing of NuGet packages.
    * **Tagging:** Tagging strategies will be used to trigger specific release candidate builds and promotions as defined in the [CI/CD Packaging and Release Plan](./ci-cd-packaging-and-release-plan.md).
7.  **Failure Policy (CI):** **Strict.** Failure of any requested component task (e.g., `Harvest-SDL2_image-win-x64`) will cause that specific task and its corresponding CI matrix job to fail. The overall workflow run will be marked as failed if *any* job fails (`strategy.fail-fast: false` in matrix).
8.  **Native Overrides (External):** Support for using pre-built binaries instead of Vcpkg will be implemented via a flag (`--use-overrides`). Overrides **take precedence** when active. A configurable path (`--overridesPath`) can be used. Storage planned for **AWS S3**, but implementation is **deferred**.
9.  **Symbol Files:** **Publish symbols (`.snupkg`) for managed bindings**. Native symbol handling is **deferred**.
10. **Testing:** Initial reliance on **manual integration testing** and **headless smoke tests** (Phase 4.5). Formal unit testing (`build.Tests`) is planned for the future.
11. **Code Formatting:** Enforced using `dotnet format` and existing configuration.

## **5\. Detailed Phased Implementation Plan**

> **Note:** The phases outlined below describe the initial development of the Cake build scripts and core harvesting logic. The CI/CD integration, packaging, and release automation aspects (originally covered in Phases 5 and 6) have evolved significantly. For the current, detailed plan regarding these areas, please refer to **[CI/CD Packaging and Release Plan](./ci-cd-packaging-and-release-plan.md)**, which includes its own phased implementation for the release pipeline.

The implementation will follow these incremental phases for the build system development:

* **Phase 0.5: "Hello Cake" CI Smoke Test**
* **Phase 1: Cake Project - Core Setup**
* **Phase 2: Cake Project - Core Harvesting Logic (Single Target - Windows)**
  * Goal: Prove end-to-end harvesting for one library on one platform.
  * Tasks:
    * Implement Tools/Dumpbin/ wrapper (DumpbinTool, alias).
    * Implement Modules/DependencyAnalysis/IDependencyScanner and WindowsDumpbinScanner.
    * Note: Future macOS implementation will likely use `otool -L`.
    * Implement core Modules/VcpkgHarvester/VcpkgHarvester.cs logic (file finding, dependency walking via scanner, file copying, basic license copying for Windows).
      * Use `vcpkg x-package-info --x-json` early to resolve known feature-driven dynamic dependencies (e.g., libmodplug -> libxmp) instead of hardcoded mapping.
    * Create a granular Harvest task (e.g., Harvest-SDL2-win-x64) using the harvester.
    * **Output:** Generate a manifest file (artifacts/harvest-SDL2-win-x64.json) listing harvested files for decoupling.
  * > **Detailed Mechanics:** The detailed internal workings of the harvesting logic, including dependency scanning, artifact planning, and deployment strategies, are further elaborated in the **[Native Binary Harvesting Process](./harvesting-process.md)** document.
* **Phase 3: Cake Project - Expansion & Refinement**
* **Phase 4: Cake Project - Packaging & Validation (Initial Local Logic)**
* **Phase 4.5: Local "Dry-Run" Publish & Smoke Test**

* **Phases 5 & 6 (Original Scope: CI/CD Integration, Release Workflow):**
  * **Current Status:** The initial concepts for GitHub Actions integration and release workflows have been superseded by a more comprehensive, multi-workflow strategy detailed in the **[CI/CD Packaging and Release Plan](./ci-cd-packaging-and-release-plan.md)**.
  * **Key Evolved Workflows (see new plan for details):**
    *   `Release-Candidate-Pipeline.yml`: The core workflow for validating, building, harvesting, packaging, and publishing to an internal feed.
    *   `PR-Version-Consistency-Check.yml`: A lightweight check for PRs to ensure version alignment.
    *   `Promote-To-Public.yml`: A manual workflow for promoting packages from the internal feed to NuGet.org.
  * **Details:** The new plan covers dynamic determination of what to build, robust version checking, staged releases (internal then public), manual overrides, and more advanced operational considerations.

* **(Later Phases - Build System Specific):**
  * Native override implementation (S3 download, potentially tested with LocalStack).
  * Comprehensive unit testing (build.Tests).
  * Automated bindingsPatch bumping.
  * SBOM generation.
  * Native symbol handling.
  * Code-signing/notarization (especially for Windows/macOS).
  * Robust Dynamic Dependency Handling: Replace/augment manual mapping in VcpkgHarvester by parsing Vcpkg package metadata (vcpkg x-package-info) to automatically identify runtime dependencies associated with installed features.
  * **Contribution Guidelines:** Create a `CONTRIBUTING.md` document detailing development setup, PR process, versioning policy enforcement (e.g., `version.json` updates), and secret management for forks.

## **6\. Implementation Insights & Critical Knowledge Transfer**

### **6.1 Overview**

This section documents critical insights, lessons learned, and architectural decisions discovered during the Linux implementation phase. It serves as a knowledge transfer guide for future developers, particularly those implementing macOS support or extending the system.

### **6.2 Core Architecture Insights**

#### **6.2.1 Scanner Responsibility Separation (CRITICAL)**

**Key Learning**: The `IRuntimeScanner` implementations must be **pure dependency discoverers**, not decision makers.

**What We Learned**:
- Initially, `LinuxLddScanner` was filtering system libraries and virtual DSOs
- This created **inconsistent behavior** between `WindowsDumpbinScanner` (no filtering) and `LinuxLddScanner` (heavy filtering)
- **Correct Design**: All scanners return raw dependency lists; `BinaryClosureWalker` makes all filtering decisions via `RuntimeProfile.IsSystemFile()`

**Implementation Pattern**:
```csharp
// ✅ CORRECT: Scanner returns everything it finds
foreach (var (libName, libPath) in dependencies)
{
    var filePath = new FilePath(libPath);
    if (_context.FileExists(filePath))
    {
        result.Add(filePath);  // Return ALL real dependencies
    }
}

// ✅ CORRECT: BinaryClosureWalker filters using centralized logic
foreach (var dep in deps)
{
    if (_profile.IsSystemFile(dep) || nodesDict.ContainsKey(dep))
    {
        continue;  // Centralized filtering
    }
    // Process dependency...
}
```

**For macOS Implementation**: Follow this exact pattern. `MacOtoolScanner` should return all dependencies found by `otool -L`, and let `BinaryClosureWalker` handle system library filtering.

#### **6.2.2 System Library Configuration Strategy**

**Key Learning**: Use `system_artefacts.json` as the single source of truth for system library patterns.

**Critical Patterns**:
```json
{
  "linux": {
    "system_libraries": [
      "linux-vdso.so.*",        // Virtual DSO (Linux-specific)
      "ld-linux-*.so.*",        // Dynamic linker (architecture-agnostic)
      "libc.so.*",              // Core system libraries (version-agnostic)
      "libm.so.*",
      "libpthread.so.*"
    ]
  }
}
```

**For macOS**: You'll need to add macOS system library patterns:
```json
{
  "osx": {
    "system_libraries": [
      "/usr/lib/libSystem.B.dylib",
      "/usr/lib/libc++.1.dylib",
      "/System/Library/Frameworks/*.framework/*",
      "/usr/lib/system/*"
    ]
  }
}
```

**Pro Tip**: Use star patterns (`*`) instead of hardcoded versions. This makes the system resilient to OS updates.

### **6.3 Platform-Specific Implementation Patterns**

#### **6.3.1 Binary Discovery & Symlink Handling**

**Critical Insight**: Unix systems (Linux/macOS) have fundamentally different binary organization than Windows.

**Linux Symlink Chain Example**:
```
libSDL2.so → libSDL2-2.0.so → libSDL2-2.0.so.0 → libSDL2-2.0.so.0.3200.4 (real file)
```

**Key Implementation Points**:

1. **Binary Detection Must Be Platform-Aware**:
```csharp
private bool IsBinary(FilePath f)
{
    var ext = f.GetExtension();
    return _profile.OsFamily switch
    {
        "Windows" => string.Equals(ext, ".dll", StringComparison.OrdinalIgnoreCase),
        "Linux" => string.Equals(ext, ".so", StringComparison.OrdinalIgnoreCase) ||
                  f.GetFilename().FullPath.Contains(".so.", StringComparison.OrdinalIgnoreCase),
        "OSX" => string.Equals(ext, ".dylib", StringComparison.OrdinalIgnoreCase),
        _ => false
    };
}
```

2. **Primary Binary Resolution for Unix**:
```csharp
// Find the real file (not a symlink) in the chain
private async Task<FilePath?> ResolveUnixPrimaryBinaryAsync(PackageInfo pkgInfo, string expectedName)
{
    // Look for all files matching the base name pattern
    var baseNameWithoutExt = expectedName.Replace(".so", "", StringComparison.OrdinalIgnoreCase);
    var candidates = pkgInfo.OwnedFiles
        .Where(f => IsBinary(f) &&
                   f.GetFilename().FullPath.StartsWith(baseNameWithoutExt, StringComparison.OrdinalIgnoreCase))
        .ToList();

    // Find the real file (longest name is usually the versioned real file)
    foreach (var candidate in candidates.OrderByDescending(f => f.GetFilename().FullPath.Length))
    {
        if (_ctx.FileExists(candidate) && !await IsSymlinkAsync(candidate))
        {
            return candidate;
        }
    }
    return null;
}
```

**For macOS**: Similar pattern, but `.dylib` files may have different versioning schemes. Research macOS dylib naming conventions.

#### **6.3.2 Symlink-Aware File Copying (CRITICAL)**

**Key Learning**: NuGet doesn't support symlinks, but Unix applications expect them. We must preserve symlinks during copying.

**Implementation Strategy**:
```csharp
public async Task<CopyResult> CopyFileAsync(FilePath source, FilePath destination)
{
    if (_profile.OsFamily != "Windows")
    {
        // Check if source is a symlink
        var sourceInfo = new FileInfo(source.FullPath);
        if (sourceInfo.LinkTarget != null)
        {
            // Preserve symlink
            var targetPath = Path.Combine(destination.GetDirectory().FullPath, source.GetFilename().FullPath);
            File.CreateSymbolicLink(targetPath, sourceInfo.LinkTarget);
            return CopyResult.Success();
        }
    }

    // Regular file copy for Windows or real files
    _context.CopyFile(source, destination);
    return CopyResult.Success();
}
```

**For macOS**: Same approach should work. Test thoroughly with macOS dylib symlinks.

### **6.4 Tool Integration Patterns**

#### **6.4.1 Platform Tool Wrapper Design**

**Pattern Established**:
```csharp
// Tool wrapper structure
public sealed class LinuxLddScanner : IRuntimeScanner
{
    private readonly ICakeContext _context;
    private readonly ICakeLog _log;

    public LinuxLddScanner(ICakeContext context)  // Simple constructor
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _log = context.Log;
    }

    public async Task<IReadOnlySet<FilePath>> ScanAsync(FilePath binary, CancellationToken ct = default)
    {
        // Use existing tool wrapper (LddRunner via aliases)
        var settings = new LddSettings(binary);
        var dependencies = await Task.Run(() => _context.LddDependencies(settings), ct);

        // Process results...
    }
}
```

**For macOS**: Create `MacOtoolScanner` following this exact pattern:
```csharp
public sealed class MacOtoolScanner : IRuntimeScanner
{
    private readonly ICakeContext _context;
    private readonly ICakeLog _log;

    public MacOtoolScanner(ICakeContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _log = context.Log;
    }

    public async Task<IReadOnlySet<FilePath>> ScanAsync(FilePath binary, CancellationToken ct = default)
    {
        var settings = new OtoolSettings(binary);
        var dependencies = await Task.Run(() => _context.OtoolDependencies(settings), ct);
        // Process otool -L output...
    }
}
```

#### **6.4.2 DI Registration Pattern**

**Established Pattern**:
```csharp
services.AddSingleton<IRuntimeScanner>(provider =>
{
    var env = provider.GetRequiredService<ICakeEnvironment>();
    var context = provider.GetRequiredService<ICakeContext>();
    var log = provider.GetRequiredService<ICakeLog>();

    var currentRid = env.Platform.Rid();
    return currentRid switch
    {
        Rids.WinX64 or Rids.WinX86 or Rids.WinArm64 => new WindowsDumpbinScanner(context),
        Rids.LinuxX64 or Rids.LinuxArm64 => new LinuxLddScanner(context),
        Rids.OsxX64 or Rids.OsxArm64 => new MacOtoolScanner(context),  // Add this
        _ => throw new NotSupportedException($"Unsupported OS for IRuntimeScanner: {currentRid}"),
    };
});
```

### **6.5 Critical Implementation Details**

#### **6.5.1 Package Inference Simplification**

**Key Learning**: Avoid complex package name inference logic. Keep it simple.

**What We Learned**:
- Initially tried complex library name parsing with magic strings
- **Better approach**: Use simple directory structure when available, fallback to "Unknown"

```csharp
private static string? TryInferPackageNameFromPath(FilePath p)
{
    // .../vcpkg_installed/<triplet>/(bin|lib|share)/<package>/...
    var segments = p.Segments;
    var vcpkgIndex = Array.FindIndex(segments, s => s.Equals("vcpkg_installed", StringComparison.OrdinalIgnoreCase));

    if (vcpkgIndex < 0 || vcpkgIndex + 3 >= segments.Length)
        return null;

    // Use directory structure when available
    return segments[vcpkgIndex + 3];
}
```

**For macOS**: Same logic should work. Don't overcomplicate it.

#### **6.5.2 Error Handling & Logging Strategy**

**Pattern Established**:
```csharp
try
{
    // Main logic
}
catch (OperationCanceledException)
{
    throw;  // Always re-throw cancellation
}
catch (SpecificException ex)
{
    _log.Error("Specific error context: {0}", ex.Message);
    return EmptyResult;  // Graceful degradation
}
```

**Key Points**:
- Always handle `OperationCanceledException` specially
- Use specific exception types when possible
- Provide meaningful error context in logs
- Prefer graceful degradation over hard failures

### **6.6 Testing & Validation Strategies**

#### **6.6.1 Symlink Validation**

**Critical Test**:
```bash
# After harvest, verify symlinks are preserved
ls -la artifacts/harvest_output/SDL2_image/runtimes/linux-x64/native/libSDL2_image*

# Should show:
# lrwxrwxrwx ... libSDL2_image.so -> libSDL2_image-2.0.so.0
# lrwxrwxrwx ... libSDL2_image-2.0.so -> libSDL2_image-2.0.so.0
# -rwxrwxrwx ... libSDL2_image-2.0.so.0.800.8 (real file)
```

**For macOS**: Implement similar validation for `.dylib` symlinks.

#### **6.6.2 Dependency Count Validation**

**Key Insight**: After refactoring, dependency counts should increase because scanners now return everything.

**Before refactor**: `LDD scan of libSDL2_image-2.0.so found 12 dependencies`
**After refactor**: `LDD scan of libSDL2_image-2.0.so found 17 dependencies`

This increase is **expected and correct** - the scanner now reports all dependencies, and `BinaryClosureWalker` filters appropriately.

### **6.7 macOS-Specific Implementation Guidance**

#### **6.7.1 Tool Requirements**

**You'll need to implement**:
1. `Tools/Otool/OtoolRunner.cs` - Wrapper for `otool -L`
2. `Tools/Otool/OtoolSettings.cs` - Settings class
3. `Tools/Otool/OtoolAliases.cs` - Cake aliases
4. `Modules/DependencyAnalysis/MacOtoolScanner.cs` - Scanner implementation

#### **6.7.2 Expected `otool -L` Output Format**

```bash
$ otool -L /path/to/library.dylib
/path/to/library.dylib:
    /usr/lib/libSystem.B.dylib (compatibility version 1.0.0, current version 1311.0.0)
    /usr/lib/libc++.1.dylib (compatibility version 1.0.0, current version 905.6.0)
    @rpath/libSDL2-2.0.0.dylib (compatibility version 1.0.0, current version 1.0.0)
```

**Key Points**:
- Parse lines that start with whitespace
- Handle `@rpath`, `@loader_path`, `@executable_path` prefixes
- Extract actual file paths for dependency resolution

#### **6.7.3 macOS System Library Patterns**

**Research needed**:
- System frameworks: `/System/Library/Frameworks/`
- System libraries: `/usr/lib/`
- Core libraries: `libSystem.B.dylib`, `libc++.1.dylib`

#### **6.7.4 macOS Binary Naming Conventions**

**Research needed**:
- How are versioned dylibs named? (e.g., `libSDL2-2.0.0.dylib`)
- What symlink patterns exist?
- How does vcpkg organize macOS binaries?

### **6.8 Performance & Optimization Notes**

#### **6.8.1 Caching Opportunities**

**Identified**:
- `IsSymlinkAsync` calls could be cached per file
- Package info queries could be memoized
- Dependency scan results could be cached by binary hash

#### **6.8.2 Parallel Processing Potential**

**Current**: Sequential dependency scanning
**Future**: Could parallelize scanning of independent binaries

### **6.9 Known Issues & Workarounds**

#### **6.9.1 Duplicate File Warnings**

**Issue**: `IO error copying libavif.so: The file already exists`
**Cause**: Both debug and release versions of libraries processed
**Status**: Expected behavior, not a bug
**Solution**: Implement smarter deduplication if needed

#### **6.9.2 Missing Package Info**

**Issue**: `Package info not found for dependency alsa, continuing.`
**Cause**: Some system packages not installed via vcpkg
**Status**: Expected for system dependencies
**Solution**: Graceful degradation already implemented

### **6.10 Future Enhancement Opportunities**

1. **Archive Support**: Implement ZIP/TAR.GZ packaging for Linux symlinks
2. **RPATH Manipulation**: Use `patchelf` to flatten dependency trees
3. **Parallel Scanning**: Speed up dependency resolution
4. **Smart Caching**: Cache expensive operations
5. **Better Package Inference**: Use vcpkg metadata more effectively

### **6.11 Critical Success Metrics**

**For macOS implementation, ensure**:
1. ✅ All dylib dependencies discovered correctly
2. ✅ System libraries filtered appropriately
3. ✅ Symlinks preserved in output
4. ✅ Primary binary resolution works for dylib chains
5. ✅ Integration with existing `BinaryClosureWalker` seamless
6. ✅ No regression in Windows/Linux functionality

## **7\. Conclusion**

This plan outlines a robust, maintainable, and scalable approach for the janset2d/sdl2-cs-bindings build system using Cake Frosting, focusing on the foundational architecture and harvesting logic. By following the defined architecture, policies, and phased implementation for the build scripts, the project aims to achieve its goals efficiently. The implementation insights in Section 6 provide critical knowledge for extending the system to additional platforms and maintaining code quality.

**For the comprehensive strategy regarding CI/CD, automated packaging, version management, and the release lifecycle, please refer to the current and authoritative [CI/CD Packaging and Release Plan](./ci-cd-packaging-and-release-plan.md).** The next step is to implement Phase 1 of the plan detailed in that document.
