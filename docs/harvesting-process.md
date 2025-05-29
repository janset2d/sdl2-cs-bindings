# Native Binary Harvesting Process

## 1. Introduction

The native binary harvesting process is a critical part of the `sdl2-cs-bindings` build system. Its primary purpose is to automatically identify, collect, and package all necessary native binaries (e.g., `.dll`, `.so`, `.dylib` files for SDL2 and its satellite libraries), their transitive dependencies, and associated license files from a Vcpkg-managed installation.

The overall goal is to produce clean, self-contained artifacts for each supported library and Runtime Identifier (RID). These artifacts are then used to create NuGet packages (e.g., `Janset.SDL2.Native.Core`) and are uploaded as assets to GitHub Releases. This ensures that consuming applications have all required native components to run correctly on different platforms.

## 2. Orchestration: The `HarvestTask`

The entire harvesting process is orchestrated by the `HarvestTask.cs`, a Cake Frosting task. It performs the following high-level steps:

1. **Initialization**:

    - Cleans the main harvest output directory (`artifacts/harvest_output/`).
    - Reads the `build/manifest.json` to get a list of all defined libraries (e.g., SDL2, SDL2_image).
    - Allows for selective harvesting if specific library names are passed via the `--library` command-line argument. If no libraries are specified, it processes all libraries from the manifest.

2. **Per-Library Processing**: For each library targeted for harvest:

    - **Binary Closure Building**: Invokes the `IBinaryClosureWalker` service to recursively find all required binary files (primary binaries of the current library and all their native dependencies).
    - **Artifact Planning**: Passes the resulting closure to the `IArtifactPlanner` service. This service determines exactly how and where each file (binaries, licenses) should be packaged, creating a `DeploymentPlan`.
    - **Artifact Deployment**: Executes the `DeploymentPlan` using the `IArtifactDeployer` service, which performs the actual file copying and archiving operations.
    - **Reporting**: Displays a summary of the harvested files and packages using Spectre.Console.

3. **Completion**: Reports overall success or failure.

## 3. Core Configuration Files

The harvesting process is heavily driven by a set of JSON configuration files:

- **`build/manifest.json`**:

  - Defines each library to be processed (e.g., "SDL2", "SDL2_image", "SDL2_mixer").
  - Specifies the corresponding `vcpkg_name` and version information.
  - Declares one library as the `core_lib` (typically "SDL2"). This is used by the `ArtifactPlanner` to avoid packaging core library files with add-on libraries.
  - Crucially, contains `primary_binaries` patterns for each operating system (`Windows`, `Linux`, `OSX`). These patterns (e.g., `SDL2.dll`, `libSDL2*`, `libSDL2*.dylib`) are used to identify the main distributable files for each library.

- **`build/runtimes.json`**:

  - Lists all supported Runtime Identifiers (RIDs) like `win-x64`, `linux-arm64`, `osx-x64`.
  - Maps each RID to its corresponding Vcpkg `triplet` (e.g., `x64-windows-release`, `arm64-linux-dynamic`).
  - Contains information about CI runners and container images for each RID.

- **`build/system_artefacts.json`**:
  - Defines patterns for system-level libraries and DLLs for each platform (`windows`, `linux`, `osx`).
  - Examples: `kernel32.dll`, `libc.so*`, `libSystem.B.dylib`.
  - These files are common OS components and should _not_ be packaged with the application. The `RuntimeProfile` uses these patterns to filter them out.

## 4. Key Services and Components

The `HarvestTask` relies on several specialized services to perform its work:

### 4.1. `RuntimeProfile`

- **Purpose**: Provides context about the current platform and helps in filtering system-specific files.
- **Functionality**:
  - Determines the `PlatformFamily` (Windows, Linux, or OSX) based on the current RID (obtained from `BuildContext`, which gets it from `runtimes.json` via command-line arguments or inferred environment).
  - Loads the appropriate list of system library exclusion patterns from `system_artefacts.json` for the current platform.
  - Provides the `IsSystemFile(FilePath path)` method, which checks if a given file path matches any of the system library patterns. This is used extensively by the `BinaryClosureWalker` to exclude OS-provided files.

### 4.2. `IPackageInfoProvider` (Implemented by `VcpkgCliProvider`)

- **Purpose**: Abstract the retrieval of package information from Vcpkg.
- **`VcpkgCliProvider` Functionality**:
  - Uses the `vcpkg x-package-info --x-json --x-installed <package_name>:<triplet>` command.
  - Parses the JSON output to extract:
    - `OwnedFiles`: A list of all files installed by the specified Vcpkg package, relative to the Vcpkg installation root. These are converted to absolute paths.
    - `DeclaredDependencies`: A list of other Vcpkg packages that the specified package depends on.

### 4.3. `IBinaryClosureWalker` (Implemented by `BinaryClosureWalker`)

- **Purpose**: To discover and build a complete list (a "closure") of all native binary files required for a given library to function on the target platform. This includes the library's own primary binaries and all their transitive native dependencies.
- **Process**:
  1. **Get Root Package Info**: Uses `IPackageInfoProvider` to get details for the main Vcpkg package of the library being harvested (e.g., `sdl2` for the `SDL2` library manifest).
  2. **Resolve Primary Binaries**:
      - Identifies the "primary binaries" of the current library using the OS-specific patterns defined in `manifest.json` (e.g., `SDL2.dll`).
      - It matches these patterns against the `OwnedFiles` list from the root package info.
      - The `IsBinary()` method is used here to ensure only actual binary files are considered, based on file extension and conventional paths (e.g., in `vcpkg_installed/{triplet}/bin` for Windows DLLs, or `vcpkg_installed/{triplet}/lib` for Linux/macOS shared objects, and excluding `debug/lib`).
  3. **Iterative Dependency Discovery (Vcpkg Packages)**:
      - It traverses the `DeclaredDependencies` graph obtained from `IPackageInfoProvider`, starting with the root package.
      - For each Vcpkg package encountered, it retrieves its `OwnedFiles` and adds any binaries found (that are not system files) to a dictionary of `BinaryNode` objects. This ensures all binaries from relevant Vcpkg packages are considered.
  4. **Iterative Dependency Discovery (Runtime Analysis)**:
      - It takes all unique binaries collected so far and puts them in a queue.
      - For each binary in the queue:
        - It invokes the platform-specific `IRuntimeScanner` (see below) to get its direct runtime dependencies.
        - For each dependency found by the scanner:
          - If the dependency is not a system file (checked via `RuntimeProfile.IsSystemFile()`) and not already processed, it's added to the `BinaryNode` dictionary and enqueued for further scanning.
          - It attempts to infer the Vcpkg package name owning the dependency based on its path (looking for `vcpkg_installed/{triplet}/(bin|lib|share)/<package_name>/`). If it can't be inferred, it's marked as "Unknown".
  5. **Result**: Returns a `ClosureResult` containing the set of primary file paths, a collection of all `BinaryNode` objects (each representing a unique binary with its path, owning Vcpkg package, and origin package), and a list of all Vcpkg packages encountered.

### 4.4. `IRuntimeScanner` (Platform-Specific Dependency Scanners)

This interface defines a contract for platform-specific tools that can inspect a binary file and list its direct runtime dependencies.

- **`WindowsDumpbinScanner`**:

  - Uses Microsoft's `dumpbin.exe /dependents` tool.
  - Parses the output to extract the names of dependent DLLs.
  - Resolves these DLL names to full paths, typically assuming they are in the same directory as the binary being scanned or in system paths (though the focus is on finding local/Vcpkg-provided ones).

- **`LinuxLddScanner`**:

  - Uses the `ldd` (List Dynamic Dependencies) command-line utility.
  - Parses the `ldd` output, which typically shows `library_name => /path/to/library.so`.
  - Extracts the full paths to the dependent shared object (`.so`) files.

- **`MacOtoolScanner`**:
  - Uses Apple's `otool -L` command (Object File Display Tool with Load commands option).
  - Parses the output to find lines listing dependent dynamic libraries (`.dylib`).
  - Includes logic to resolve paths that start with `@rpath/`, `@loader_path/`, or `@executable_path/` relative to the binary being scanned or known library locations.

### 4.5. `IArtifactPlanner` (Implemented by `ArtifactPlanner`)

- **Purpose**: Takes the `BinaryClosure` (the complete list of needed files) and creates a `DeploymentPlan`, which is a list of actions specifying how each file should be packaged.
- **Process**:
  1. **Initialization**: Sets up output base paths for native files (`{output_root}/{LibraryName}/runtimes/{rid}/native/`) and license files (`{output_root}/{LibraryName}/licenses/`).
  2. **Iterate Through Closure Nodes**: For each `BinaryNode` (each file) in the closure:
      - **Core Library Filtering**: If the current library being processed is _not_ the `core_lib` (as defined in `manifest.json`), and the binary node's `OriginPackage` or `OwnerPackageName` is the `core_lib`'s Vcpkg name, then this binary is skipped. This prevents, for example, `SDL2.dll` from being included in the `SDL2_image` native package, as it's assumed the user will also reference the `SDL2.Core.Native` package.
      - **Platform-Specific Packaging Strategy**:
        - **Windows**: Creates a `FileCopyAction` to copy the binary directly to the `nativeOutput` directory.
        - **Linux/macOS**: Adds the binary to a list of `ArchivedItemDetails` to be included in a `.tar.gz` archive. This is done to preserve the symlink structure crucial for these platforms.
      - Keeps track of all Vcpkg packages whose files are being included (`copiedPackages`).
  3. **Process License Files**: For every package in `copiedPackages`:
      - Uses `IPackageInfoProvider` to get its details again.
      - Finds any `copyright` files within its `OwnedFiles` (typically in a `share/{package_name}/` directory).
      - Creates `FileCopyAction`s to copy these license files to the `licenseOutput` directory, namespaced by the package name (e.g., `licenses/{package_name}/copyright`).
  4. **Plan Archive Creation (Linux/macOS)**: If not on Windows and there are items to archive:
      - Creates an `ArchiveCreationAction`. This action includes:
        - The target path for the archive (e.g., `nativeOutput/native.tar.gz`).
        - The base directory for `tar` (the Vcpkg installed lib directory, e.g., `vcpkg_installed/{triplet}/lib`).
        - The list of `ArchivedItemDetails`.
  5. **Generate Statistics**: Collects information about primary files, runtime files, license files, deployed packages, and filtered packages to create `DeploymentStatistics` for the summary report.
  6. **Result**: Returns a `DeploymentPlan` containing all `DeploymentAction`s (file copies, archive creation) and the `DeploymentStatistics`.

### 4.6. `IArtifactDeployer` (Implemented by `ArtifactDeployer`)

- **Purpose**: Executes the `DeploymentPlan` created by the `ArtifactPlanner`.
- **Process**: Iterates through each `DeploymentAction` in the plan:
  - **`FileCopyAction`**:
    - Ensures the target directory exists.
    - Copies the source file to the target path using `context.CopyFile()`.
  - **`ArchiveCreationAction` (for Linux/macOS)**:
    - Ensures the target archive directory exists.
    - Creates a temporary text file listing the relative paths of all binaries to be included in the archive. These paths are relative to the `BaseDirectory` specified in the action (which is typically the Vcpkg `lib` or `bin` directory for the triplet).
    - Executes the `tar` command-line utility:
      - `tar -czf {archive_path} -T {temp_file_list_path}`
      - Sets the `WorkingDirectory` for the `tar` process to the `BaseDirectory`. This is crucial for `tar` to correctly resolve relative paths and embed symlinks with their correct (relative) targets within the archive.
    - Deletes the temporary file list.
- **Result**: Returns a `CopierResult` indicating success or failure.

## 5. Platform-Specific Differences & NuGet Packaging Considerations

The harvesting process adapts to different operating systems and build tooling (specifically NuGet) in several key ways:

- **Dependency Discovery Tools**:
  - **Windows**: `dumpbin.exe`
  - **Linux**: `ldd`
  - **macOS**: `otool -L`
- **Binary File Types & Naming**:
  - **Windows**: Dynamic Link Libraries (`.dll`)
  - **Linux**: Shared Objects (`.so`)
  - **macOS**: Dynamic Libraries (`.dylib`)
- **Symlink Handling & Packaging (The NuGet Workaround)**:
  - **Linux/macOS**: These platforms heavily rely on symbolic links for versioning and managing shared libraries (e.g., `libSDL2.so -> libSDL2-2.0.so.0`, `libSDL2-2.0.so.0 -> libSDL2-2.0.so.0.xx.y`). NuGet itself does not natively support packaging or restoring these symbolic links directly. This limitation is discussed in GitHub issues such as [NuGet/Home#12136](https://github.com/NuGet/Home/issues/12136) and the related feature request [NuGet/Home#10734](https://github.com/NuGet/Home/issues/10734).
    To overcome this, the harvesting and packaging strategy for these platforms involves:
    1. **Archiving**: During the harvest, all required native binaries and their preserved symlinks are packaged into a single `native.tar.gz` archive using the `tar` utility. The `tar` command is executed from an appropriate base directory (e.g., `vcpkg_installed/{triplet}/lib`) to ensure symlink targets are stored correctly as relative paths within the archive.
    2. **NuGet Package Content**: This `native.tar.gz` file is placed within the `runtimes/{rid}/native/` directory of the NuGet package.
    3. **Extraction via MSBuild Targets**: A custom MSBuild `.targets` file is included in the `build/` folder of the NuGet package. This targets file contains logic that hooks into the consuming project's build process. When the project is built on a Linux or macOS system, the targets file executes commands (e.g., `tar -xzf`) to extract the `native.tar.gz` archive into the build output directory (e.g., `$(OutDir)`), thereby restoring the symlinks in the location where the application expects to find them.
  - **Windows**: DLLs are typically self-contained, and their deployment does not usually involve symlinks in the same way as Linux/macOS shared libraries. Therefore, for Windows, direct file copying of DLLs into the `runtimes/{rid}/native/` structure within the NuGet package is sufficient.
- **System Library Identification**: The patterns in `system_artefacts.json` are tailored for each OS to correctly identify and exclude common system libraries that should not be redistributed.
- **Primary Binary Patterns**: The `primary_binaries` patterns in `manifest.json` are defined per-OS to match platform-specific naming conventions (e.g., `SDL2.dll` vs. `libSDL2*`).

## 6. Harvesting Output Structure

After a successful harvest for a library (e.g., `SDL2_image`) and a specific RID (e.g., `linux-x64`), the output is organized under `artifacts/harvest_output/` as follows:

```text
artifacts/harvest_output/
└── {LibraryName}/                     (e.g., SDL2_image)
    ├── licenses/
    │   ├── {owning_vcpkg_package_1}/
    │   │   └── copyright
    │   └── {owning_vcpkg_package_2}/
    │       └── copyright
    └── runtimes/
        └── {rid}/                     (e.g., linux-x64)
            └── native/
                ├── (Windows RIDs):
                │   ├── binary1.dll
                │   ├── binary2.dll
                │   └── ...
                └── (Linux/macOS RIDs):
                    └── native.tar.gz  (Contains binaries and preserved symlinks, intended for NuGet packaging)
```

- **`licenses/`**: Contains a subdirectory for each Vcpkg package from which files were harvested. Each subdirectory holds the `copyright` file for that package.
- **`runtimes/{rid}/native/`**:
  - For **Windows RIDs**, this directory contains all the harvested `.dll` files directly.
  - For **Linux and macOS RIDs**, this directory contains a single `native.tar.gz` archive. This archive is specifically created to work around NuGet's lack of symlink support and is intended to be bundled into the resulting NuGet package. An accompanying MSBuild `.targets` file in the NuGet package (located in the package's `build/` directory) will handle its extraction during the consumer's build process.

This structured output is then ready to be consumed by subsequent packaging tasks to create the `Janset.SDL2.Native.*` NuGet packages.
