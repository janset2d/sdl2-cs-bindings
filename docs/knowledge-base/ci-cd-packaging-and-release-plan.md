# CI/CD Packaging and Release Plan

> **Policy reference:** Release lifecycle policy (package families, versioning model, release governance, dependency contracts, CI matrix shape, promotion path) is defined in [`release-lifecycle-direction.md`](release-lifecycle-direction.md). This document describes the **pipeline implementation** that enforces those policies.

## 1. Overview and Goals

- **What:** This document defines the comprehensive CI/CD pipeline for building, harvesting, packaging, and releasing the `Janset.SDL2.*` native and binding libraries for the `sdl2-cs-bindings` project. It builds upon the earlier Cake build planning work that has since been folded into the current documentation set.
- **Why:** To establish a reliable, controlled, transparent, and maintainable process for generating internal release candidates, promoting vetted packages to public NuGet feeds, managing versions consistently, and ensuring the quality and integrity of all distributed artifacts.
- **Key Objectives:**
  - **Internal Staging:** Implement a robust internal staging mechanism for all generated NuGet packages, allowing for testing and validation before any public release.
  - **Deliberate Public Promotion:** Ensure that publishing packages to public feeds (like NuGet.org) is always a conscious, manually triggered, and audited step.
  - **Clear Version Management:** Package versions are derived from family tags per the release lifecycle direction. `build/manifest.json` remains the single source of truth for native component target versions (vcpkg versions) and RID/triplet/strategy mappings.
  - **Optimized Builds:** Implement logic to only build and package libraries and RIDs that are new, have changed, or are explicitly forced, thus saving CI resources.
  - **Operational Robustness:** Design the pipeline to be resilient against common failure modes, with clear error reporting and recovery strategies.
  - **Visibility & Traceability:** Provide clear insight into the build, packaging, and release process through build fingerprints, logs, and status reporting (e.g., via GitHub Deployment Environments).

## 1.1 Reality Labels

This document intentionally mixes current implementation details and future CI/CD design. Read the labels literally:

- **Implemented today**: Exists in the repo and is wired into the current build flow.
- **Partially implemented**: Exists in code or workflow files, but is not fully wired or production-ready.
- **Planned / not present in repo**: Intentionally preserved design that does not yet exist as a working file or task.

Current repo reality on 2026-04-11:

- The `prepare-native-assets-*` workflows are the working cross-platform harvest path and now use explicit `--rid` plus the full SDL2 satellite harvest list (`SDL2`, `SDL2_image`, `SDL2_mixer`, `SDL2_ttf`, `SDL2_gfx`, `SDL2_net`); matrix-level validation for the expanded set is still pending.
- `release-candidate-pipeline.yml` exists, but it is still a placeholder workflow with dummy steps and artifacts.
- `PreFlightCheckTask`, `HarvestTask`, `ConsolidateHarvestTask`, and `CoverageCheckTask` are implemented in the Cake build host. `CoverageCheckTask` (ratchet gate) runs locally via `dotnet cake --target=Coverage-Check` against `build/coverage-baseline.json`; wiring it into a CI PreflightGate job alongside `PreFlightCheckTask` and the unit-test step is part of Stream C (see [`phase-2-adaptation-plan.md`](../phases/phase-2-adaptation-plan.md#stream-c-ci-modernization-after-stream-a)).
- `build/known-issues.json`, `PackageTask`, `PR-Version-Consistency-Check.yml`, and `Promote-To-Public.yml` are planned, not present as working repo artifacts.
- `PathService` contains harvest-staging helpers for distributed CI, but the active tasks and workflows still emit to `artifacts/harvest_output/`.
- Shared native dependency collision policy is resolved: Hybrid Static model eliminates transitive DLL collisions by static-baking deps into satellites.
- Config merge complete: `runtimes.json` and `system_artefacts.json` are now merged into `manifest.json` schema v2.
- Release lifecycle direction is locked: package families, tag-derived versioning, hybrid governance, dependency contracts, CI matrix shape, promotion path. See [`release-lifecycle-direction.md`](release-lifecycle-direction.md).

## 2. Guiding Principles & Core Tenets

The design of this CI/CD pipeline adheres to the following principles:

- **`manifest.json` as Single Source of Truth:** The `build/manifest.json` file is the definitive source for all `Janset.SDL2.*` package versions and for specifying the target versions of the underlying native components (e.g., the version of SDL2 that Vcpkg is expected to build, which is cross-referenced with `vcpkg.json`).
- **Internal Feed First (Staging):** All automated (e.g., tag-based, once implemented) and manually triggered release candidate builds will publish generated NuGet packages to an internal/private NuGet feed. This feed acts as a staging area for testing and validation. If an internal feed is not immediately available, the pipeline will support a "pack-only" mode to produce local `.nupkg` artifacts.
- **Manual Public Promotion:** The act of publishing packages to public feeds like NuGet.org will always be a separate, deliberate, and manually triggered step, distinct from the automated build and internal publish process.
- **Phased Implementation:** The pipeline will be developed and rolled out in phases, starting with a core manually triggered workflow and incrementally adding automation, advanced features, and more comprehensive checks.
- **Visibility & Traceability:** The pipeline will strive for maximum transparency through detailed logging, build fingerprints embedded in artifacts or metadata, and the use of GitHub features like Deployment Environments for status tracking.
- **Operational Resilience:** Proactive planning for error handling, recovery from common failures, health checks for the pipeline itself, and mechanisms for managing known issues (e.g., temporarily skipping a known-bad build combination).

## 3. Key Configuration Files & Artifacts

The pipeline relies on several key configuration files and generates specific artifacts:

- **`build/manifest.json`:**

  - **Role:** The primary driver for packaging. Defines each `Janset.SDL2.*` library (both native and binding), its target NuGet package version, the corresponding `vcpkg_name` of the native component, and the target `vcpkg_version` of that native component.
  - **Structure Example Snippet:**

    ```json
    {
      "name": "SDL2", // Corresponds to Cake --library argument
      "vcpkg_name": "sdl2",
      "vcpkg_version": "2.32.10", // Expected version from vcpkg.json
      "native_lib_name": "Janset.SDL2.Core.Native",
      "native_lib_version": "2.32.10.0", // NuGet package version (will be replaced by family version from tag)
      "core_lib": true,
      "primary_binaries": [...]
    }
    ```

  - Used by the `pre_flight_check` job to validate against `vcpkg.json` and by the `PackageTask` in Cake to determine the version for `dotnet pack`.

- **`vcpkg.json`:**

  - **Role:** Defines the exact versions of native libraries (e.g., SDL2, SDL2_image) that Vcpkg will build, primarily through its `overrides` section.
  - It is manually kept in sync with the `vcpkg_version` fields in `build/manifest.json` for consistency.

- **`build/manifest.json` `runtimes` section** (formerly `build/runtimes.json`): Defines supported RIDs and their mapping to vcpkg triplets, strategies, and CI runner configurations. Merged into manifest.json schema v2.
- **`build/manifest.json` `system_exclusions` section** (formerly `build/system_artefacts.json`): Lists OS-specific system library patterns to be excluded during harvesting. Merged into manifest.json schema v2.

- **`build/known-issues.json` (Planned / not present in repo):**

  - **Role:** A manually curated file listing specific `library/version/RID` combinations that are known to be problematic (e.g., due to an upstream bug or a temporary build environment issue).
  - **Purpose:** Allows the CI pipeline to gracefully skip these known-failing builds, saving resources and reducing noise, until the underlying issue is resolved.
  - **Structure Example:**

    ```json
    {
      "sdl2-image": {
        // vcpkg_name or manifest.json name
        "2.8.8": {
          // vcpkg_version from manifest.json
          "linux-arm64": "Tracking issue #123 - NEON optimization crash"
        }
      }
    }
    ```

- **`artifacts/harvest_output/{LibraryName}/harvest-manifest.json` (Generated Artifact - ✅ IMPLEMENTED):**
  - **What:** A JSON file generated by the `ConsolidateHarvestTask` that consolidates per-RID status files from individual harvest operations.
  - **Why:** Provides explicit metadata about the harvest results for that specific library across all processed RIDs, consumed by the subsequent `PackageTask` and useful for debugging and auditing.
  - **Current Implementation Status:** ✅ **COMPLETED** - The `ConsolidateHarvestTask` has been implemented and tested. It processes individual RID status files and generates consolidated harvest manifests and summaries.
  - **Actual Structure (Current Implementation):**

    ```json
    {
      "library_name": "SDL2",
      "generated_timestamp": "2025-05-31T21:56:33.6066345+00:00",
      "rids": [
        {
          "library_name": "SDL2",
          "rid": "win-x64",
          "triplet": "x64-windows-hybrid",
          "success": true,
          "error_message": null,
          "timestamp": "2025-05-31T21:55:43.3376331+00:00",
          "statistics": {
            "primary_files_count": 1,
            "runtime_files_count": 1,
            "license_files_count": 2,
            "deployed_packages_count": 2,
            "filtered_packages_count": 0,
            "deployment_strategy": "DirectCopy"
          }
        }
      ]
    }
    ```

  - **Complementary Files:** The task also generates `harvest-summary.json` with high-level success/failure statistics and maintains individual RID status files in `rid-status/` subdirectories.

- **Current Local Development vs Future CI Architecture:**
  - **⚠️ ARCHITECTURAL LIMITATION:** The current implementation works perfectly for local single-machine development but requires significant changes for distributed CI operations across multiple OS runners.
  - **Current Structure:** Single `artifacts/harvest_output/` directory with all libraries and RIDs consolidated in one location.
  - **Future CI Requirement:** Separate staging paths for individual RID uploads vs. final consolidated output to support CI matrix jobs running on different operating systems.

## 4. Core CI/CD Workflows

### A. `Release-Candidate-Pipeline.yml` (The "Mega Workflow")

**Status**: Partially implemented. The file exists in `.github/workflows/`, but the current workflow body is still placeholder logic and does not execute the end-to-end design described below.

- **What:** The primary workflow responsible for validating the configuration, building native assets via Vcpkg, harvesting these assets, packaging them into `.nupkg` files, and publishing them to an internal NuGet feed (or staging them locally).
- **Why:** This consolidated workflow (as opposed to multiple smaller, chained workflows) aims for better visibility into the end-to-end process, easier debugging, and more robust control over the release candidate generation.
- **Filename:** `.github/workflows/release-candidate-pipeline.yml`

- **Trigger (Phase 1 - Initial Implementation):**
  - Manual `workflow_dispatch` only.
- **Trigger (Phase 2 - Adding Automation):**

  - `on: push: tags: [ 'v*', 'rc-*', 'build-candidate-*' ]` (Specific tag patterns to be finalized). The workflow will also retain its `workflow_dispatch` trigger.

- **Inputs (for `workflow_dispatch`):**

  - `target_destination`:
    - Description: 'Target for the generated NuGet packages.'
    - Type: `choice`
    - Default: `internal-feed`
    - Options: [`internal-feed`, `pack-only`]
  - `force_build_strategy`:
    - Description: 'Strategy for forcing library builds, overriding default change detection.'
    - Type: `choice`
    - Default: `auto-detect`
    - Options:
      - `auto-detect`: Default. Only build/package libraries with versions in `manifest.json` not yet in `target_destination` (and not in `known-issues.json`).
      - `force-buildable`: Force rebuild/repackage of all libraries in `manifest.json` that are _not_ listed in `known-issues.json` for the target RIDs.
      - `force-everything`: Force rebuild/repackage of _all_ libraries in `manifest.json`, even if listed in `known-issues.json` (useful for diagnostics or verifying fixes).
  - `force_push_packages`:
    - Description: 'If true, attempt to re-push/overwrite packages in the internal feed (if the feed allows).'
    - Type: `boolean`
    - Default: `false`

- **Concurrency:**

  - `group: ${{ github.workflow }}-${{ github.event_name == 'push' && github.ref || 'manual' }}`
  - `cancel-in-progress: ${{ github.event_name == 'workflow_dispatch' }}` (Evaluates to `true` for manual dispatches, `false` for tag pushes).

- **Jobs:**

  1. **`pre_flight_check` Job:**

      - **What:** Validates configurations, determines the build plan (which libraries/RIDs to process), and provides an early summary.
      - **Why:** Prevents wasted CI resources on invalid configurations and gives quick feedback before long-running builds.
      - **How:**
        - `runs-on: ubuntu-latest`
        - `outputs`: `build_matrix` (JSON string), `build_plan_summary` (string).
        - **Steps:**
          1. `actions/checkout@v4`: With `ref: ${{ github.event_name == 'push' && github.ref || github.sha }}`.
          2. **Read Configurations:** Load `build/manifest.json`, `vcpkg.json`, `build/runtimes.json`, and `build/known-issues.json` (if it exists).
          3. **Validate Versions:** For each library in `manifest.json`, compare its declared `vcpkg_version` against the version specified for the corresponding component in `vcpkg.json` (under `overrides`). If they do not match, **fail the workflow**.
          4. **Determine Build Matrix:**
              - Start with all libraries defined in `manifest.json` and all RIDs from `runtimes.json`.
              - Apply filtering based on `inputs.force_build_strategy`:
                - If `auto-detect`: Query `inputs.target_destination` (internal NuGet feed via an API call - _add caching for this query in Phase 2_) to identify library/versions already published. Exclude these. Also, exclude combinations found in `known-issues.json`.
                - If `force-buildable`: Exclude combinations found in `known-issues.json`.
                - If `force-everything`: Include all combinations (no filtering beyond what's in manifest/runtimes).
              - The final output is a matrix (e.g., JSON array of objects: `[{rid: "win-x64", triplet: "x64-windows-hybrid", strategy: "hybrid-static", runner: "windows-latest"}, ...]`). Per the release lifecycle direction, the matrix is RID-based (build axis), not library×RID.
          5. **Generate Summary:** Create a human-readable summary of the build plan (libraries, RIDs, total count) and set it as an output for GitHub Step Summary.
          6. If the generated build matrix is empty and `inputs.force_build_strategy` was `auto-detect`, the job can complete successfully, indicating nothing needs to be built. Subsequent jobs should handle this gracefully (e.g., using `if: needs.pre_flight_check.outputs.build_matrix != '[]'`).

  2. **`build_harvest_matrix` Job:**

      - **`needs: pre_flight_check`**
      - **`if: needs.pre_flight_check.outputs.build_matrix != '[]'`** (or similar check for non-empty matrix)
      - **`strategy: matrix: ${{ fromJson(needs.pre_flight_check.outputs.build_matrix) }}`**
      - **What:** Dynamically runs the Vcpkg build and Cake `Harvest` task for each item (library/RID combination) in the matrix generated by `pre_flight_check`.
      - **Why:** This is the core compilation and artifact collection stage. The detailed internal workings of the harvesting task itself, including how it discovers dependencies, handles platform differences, and collects native binaries and licenses, are documented in the **[Native Binary Harvesting Process](./harvesting-process.md)**.
      - **How (per matrix job):**
        1. `actions/checkout@v4`: With `ref: ${{ github.event_name == 'push' && github.ref || github.sha }}` and `submodules: recursive`.
        2. Setup .NET, Vcpkg (using existing custom action), etc., based on `matrix.rid` and `matrix.triplet`.
        3. **Run Cake Harvest:**
            - `./build/_build/Build --target Harvest --library ${{ matrix.library }} --rid ${{ matrix.rid }} --triplet ${{ matrix.triplet }} --package-version ${{ matrix.version }} --repo-root "$(pwd)"` (adjust args as needed for Cake script).
            - The Cake `HarvestTask` must generate `artifacts/harvest_output/${{ matrix.library }}/harvest-manifest.json` containing metadata about the harvest for _this specific library instance_ (including the RIDs it attempted for this library, which ones succeeded/failed for this library, etc.).
        4. **Upload Harvested Library Artifact:**
            - `uses: actions/upload-artifact@v4`
            - `name: harvest-output-${{ matrix.library }}-${{ matrix.rid }}` (or a more consolidated name if preferred, but per-RID might be good for partial success).
            - `path: artifacts/harvest_output/${{ matrix.library }}/` (or more specific to the RID if necessary).
            - _Alternative for `path` if a single `harvest_output` dir is used: the whole `artifacts/harvest_output` is uploaded once by a dependent job._ For now, assume the matrix job uploads its specific library's output. If `build_harvest_matrix` uploads a single consolidated artifact at the end, this step changes. Given current harvest structure, individual library harvest outputs are distinct. Let's assume each matrix job uploads its own `artifacts/harvest_output/${{ matrix.library }}`.
  3. **`consolidate_harvest_artifacts` Job (Architectural Change Required for CI):**

      - **`needs: build_harvest_matrix`** (runs after all matrix jobs, even if some failed, if `fail-fast: false` is used on matrix)
      - **Current Status:** ⚠️ **ARCHITECTURE NEEDS REDESIGN** - The current `ConsolidateHarvestTask` works for local development but the CI workflow architecture described here needs significant changes.
      - **Current Implementation:** The `ConsolidateHarvestTask` is fully implemented and functional for local single-machine development, processing RID status files into consolidated manifests.
      - **Issue:** The current artifact upload/download approach assumes each CI matrix job can upload `artifacts/harvest_output/${{ matrix.library }}/` but this won't work across different OS runners since the path structure overlaps.
      - **Required Change:** Need separate **staging** vs **consolidated** output paths:
        - **Staging Path:** `artifacts/harvest_staging/{library}/{rid}/` for individual CI job uploads
        - **Consolidated Path:** `artifacts/harvest_output/{library}/` for final consolidated results
      - **Future Architecture:**
        1. Each matrix job uploads to `harvest-staging-{library}-{rid}` artifacts
        2. This job downloads all staging artifacts and reorganizes into proper `harvest_output` structure
        3. Runs `ConsolidateHarvestTask` on the reorganized data
        4. Uploads final consolidated `harvest_output` artifact

  4. **`package_and_publish_internal` Job:**
      - **`needs: consolidate_harvest_artifacts`**
      - **What:** Takes the consolidated harvested artifacts, packages them into `.nupkg` files, and (conditionally) pushes them to the internal NuGet feed.
      - **Why:** Creates the actual consumable NuGet packages for internal use or staging.
      - **How:**
        1. `actions/checkout@v4`: (minimal, just for Cake script if needed, or run Cake as a downloaded artifact).
        2. Setup .NET.
        3. `actions/download-artifact@v4`: Download `final-harvest-output`.
        4. **Iterate & Package:** For each library defined in `build/manifest.json` that was intended for build (can cross-reference with `pre_flight_check` output or by presence in downloaded `final-harvest-output`):
            - Run Cake `Package` task:
              - Pass path to the library's harvested files within `final-harvest-output`.
              - Task determines the family version from the family tag (see release-lifecycle-direction.md §3).
              - Task reads the library-specific `final-harvest-output/{LibraryName}/harvest-manifest.json` to confirm successful RIDs.
              - Stages files correctly for `dotnet pack` (including `native.tar.gz` and `.targets` file for `buildTransitive/` in native packages).
              - Calls `dotnet pack` for the native package.
              - Calls `dotnet pack` for the corresponding binding package.
        5. **Publish (Conditional):**
            - If `inputs.target_destination == 'internal-feed'`:
              - Script to find all generated `.nupkg` files.
              - `dotnet nuget push *.nupkg --source {INTERNAL_FEED_URL} --api-key {INTERNAL_FEED_KEY}` (respect `inputs.force_push_packages` if feed allows/requires specific flags for overwrite).
        6. **Upload NuGet Packages as Artifact:**
            - `uses: actions/upload-artifact@v4`
            - `name: nuget-packages`
            - `path: path/to/generated/*.nupkg`
        7. **Update GitHub Deployment Environments (Phase 1):**
            - For each package successfully pushed to `internal-feed`, create or update a GitHub Deployment. Environment name could be `Internal-Feed/${PackageId}/${PackageVersion}`. Mark as `success` or `failure`. URL could point to the internal feed package URL.

### B. `PR-Version-Consistency-Check.yml`

**Status**: Planned / not present in repo.

- **What:** A lightweight, non-blocking CI check that runs on Pull Requests to provide early feedback on version alignment between `manifest.json` and `vcpkg.json`.
- **Why:** Helps developers catch inconsistencies before they attempt to trigger the main release candidate pipeline.
- **Filename:** `.github/workflows/pr-version-consistency-check.yml`
- **Trigger:** `on: pull_request` (targeting branches like `master`, `develop`).
- **Job:**
  1. **`check_versions` Job:**
      - `runs-on: ubuntu-latest`
      - **Steps:**
        1. `actions/checkout@v4`.
        2. Script (bash or C# tool) to parse `build/manifest.json` (specifically `vcpkg_version` fields) and `vcpkg.json` (specifically `overrides` versions).
        3. If any inconsistencies are found for common components, output them using `echo "::warning file={path_to_manifest},title=Version Mismatch::{component_name} in manifest.json (v{m_ver}) vs vcpkg.json (v{v_ver})"`.
        4. The job itself should always succeed, allowing the PR to proceed, but the warnings will be visible.

### C. `Promote-To-Public.yml` (Targeted for Phase 3)

**Status**: Planned / not present in repo.

- **What:** A manually triggered workflow to promote vetted NuGet packages from the internal feed to the public NuGet.org.
- **Why:** Ensures that public releases are a deliberate, audited, and controlled process.
- **Filename:** `.github/workflows/promote-to-public.yml`
- **Trigger:** `workflow_dispatch`.
- **Inputs (Example):**
  - `package_id_to_promote`: (string, e.g., `Janset.SDL2.Core.Native`)
  - `package_version_to_promote`: (string, e.g., `2.32.4.0`)
  - _Or, an input to specify "promote all latest validated from manifest version X.Y.Z."_
- **Job:**
  1. **`promote_package` Job:**
      - **Steps:**
        1. Script to download the specified `.nupkg` from the internal feed.
        2. `dotnet nuget push {downloaded_package.nupkg} --source https://api.nuget.org/v3/index.json --api-key {NUGET_ORG_API_KEY_SECRET}`.
        3. Optionally, create a GitHub Release for the corresponding tag, attaching assets.
        4. Update public-facing deployment statuses or notifications.

## 5. Phased Implementation Details

The rollout of this comprehensive CI/CD pipeline will occur in distinct phases:

- **Phase 1: Core Manually Triggered `Release-Candidate-Pipeline`**

  - **Focus:** Establish a reliable, manually triggered workflow (`Release-Candidate-Pipeline.yml`) that can:
    - Perform pre-flight checks (version consistency, `known-issues.json` filtering, "already published" check against internal feed for `auto-detect`).
    - Implement the three-tier `force_build_strategy`.
    - Execute the `build_harvest_matrix` job.
    - Generate basic `harvest-manifest.json` for each library artifact (V1 content: library name, package version, timestamp, commit hash, successful/failed RIDs for that harvest).
    - Implement the `consolidate_harvest_artifacts` job.
    - Execute the `package_and_publish_internal` job, supporting `pack-only` and `internal-feed` (if available) destinations.
    - Utilize GitHub Deployment Environments for basic status tracking of internal pushes.
  - Implement the `PR-Version-Consistency-Check.yml` workflow (warning only).
  - Refine Cake `HarvestTask` and `PackageTask` as needed.

- **Phase 2: Enhancements & Tag-Based Automation**

  - **Focus:** Add automation layers and improve metadata/traceability.
  - Add tag-based triggers (`on: push: tags: [...]`) to `Release-Candidate-Pipeline.yml`.
  - Refine `concurrency` settings for mixed manual/tag triggers.
  - Enhance `harvest-manifest.json` to include the full `build_fingerprint` (workflow run ID, actor, trigger type, specific Vcpkg commit, manifest hash, etc.).
  - Implement NuGet query caching in the `pre_flight_check` job.
  - If GitHub Deployment Environments prove limiting for status tracking, implement the JSON/Markdown status page workaround (Claude's suggestion).

- **Phase 3: Public Promotion & Long-Term Maintenance**
  - **Focus:** Implement the public release mechanism and long-term operational health features.
  - Develop and test the `Promote-To-Public.yml` workflow.
  - Formalize and document "Oops" recovery procedures (package yanking from internal/public, artifact backup strategy during `package_and_publish_internal`).
  - Design and implement the "Maintenance Mode" / Health Check workflow (scheduled checks for feed health, stale packages, `known-issues.json` validity, recovery time benchmark).

## 6. Cake Task Responsibilities (Current Implementation Status)

- **`HarvestTask` (Cake - ✅ FUNCTIONAL):**

  - **Implemented today**

  - **Current Status:** ✅ **IMPLEMENTED** - The `HarvestTask` is functional for individual RID harvesting and generates per-RID status files.
  - Inputs today: Library name(s) and an optional RID. The triplet is derived from `runtimes.json` via the resolved RID.
  - Planned future inputs in CI design: explicit triplet/package-version orchestration from the release workflow.
  - Responsibilities:
    - Runs Vcpkg install for the given triplet (if not already done by CI setup).
    - Performs the actual binary harvesting logic (dependency walking, license gathering, symlink-preserving `tar.gz` creation for Linux/macOS).
    - **Generates per-RID status files** in `artifacts/harvest_output/{LibraryName}/rid-status/{RID}.json` containing detailed harvest results for that specific RID.
    - **Current Implementation:** Creates individual RID status files that are later consolidated by `ConsolidateHarvestTask`.

- **`ConsolidateHarvestTask` (Cake - ✅ COMPLETED):**

  - **Implemented today**

  - **Current Status:** ✅ **FULLY IMPLEMENTED AND TESTED** - Successfully consolidates per-RID harvest status files into unified manifests.
  - Inputs: Processes all libraries found in the harvest output directory.
  - Responsibilities:
    - Reads individual RID status files from `artifacts/harvest_output/{LibraryName}/rid-status/` directories.
    - Consolidates them into unified `harvest-manifest.json` and `harvest-summary.json` files.
    - Handles both successful and failed harvest results with detailed statistics.
    - Generates comprehensive metadata suitable for downstream packaging tasks.
  - **Tested Results:** Successfully processes SDL2 (success), SDL2_image (success), and SDL2_mixer (failure due to missing vcpkg package).

- **`PackageTask` (Cake - Planned / not present in repo):**
  - **Current Status:** ⚠️ **NOT IMPLEMENTED** - No `PackageTask` exists in the current build host.
  - This section describes the planned task contract, not an existing implementation.
  - Expected Responsibilities:
    1. Determines the **family version** for the target package family. Per the release lifecycle direction, the family version is derived from the family tag (e.g., `sdl2-core-1.2.0` → version `1.2.0`). Both managed and native packages within a family share the same version. Family identifier convention is `sdl<major>-<role>` (canonical, see [`release-lifecycle-direction.md`](release-lifecycle-direction.md) §1). See [`release-lifecycle-direction.md`](release-lifecycle-direction.md) §3.
    2. Reads the `harvest_output/{LibraryName}/harvest-manifest.json` to identify successfully harvested RIDs and their output paths.
    3. **For Native Package (e.g., `Janset.SDL2.Core.Native`):**
        - Creates a temporary staging directory.
        - For each successful RID in `harvest-manifest.json`:
          - Copies/links the native content (e.g., `win-x64/native/*.dll` or `linux-x64/native/native.tar.gz`) into `staging/runtimes/{rid}/native/`.
        - Copies the `buildTransitive/{PackageId}.targets` file (for tar.gz extraction) into `staging/buildTransitive/`.
        - Calls `dotnet pack` with the family version.
    4. **For Binding Package (e.g., `Janset.SDL2.Core`):**
        - Calls `dotnet pack` with the same family version. The managed package declares a within-family exact pin (`=`) dependency on its native package at the family version.

This detailed plan should provide a strong foundation.

## 7. Implementation Status Summary

### ✅ Completed Components

#### Harvest Consolidation (Phase 1 - Core Functionality)

- ✅ **`ConsolidateHarvestTask`** - Fully implemented and tested
  - Processes individual RID status files from `rid-status/` directories
  - Generates consolidated `harvest-manifest.json` and `harvest-summary.json` files
  - Handles success/failure states with detailed statistics
  - Successfully tested with SDL2 (success), SDL2_image (success), SDL2_mixer (failure)

#### Current File Structure (Local Development)

- ✅ **Harvest Output Organization** - Working structure implemented:

  ```text
  artifacts/harvest_output/
  ├── SDL2/
  │   ├── harvest-manifest.json    (consolidated results)
  │   ├── harvest-summary.json     (high-level statistics)
  │   ├── rid-status/              (individual RID status files)
  │   ├── licenses/                (collected license files)
  │   └── runtimes/                (harvested binaries by RID)
  ├── SDL2_image/ (similar structure)
  └── SDL2_mixer/ (similar structure)
  ```

#### Core Build Tasks

- ✅ **`HarvestTask`** - Functional for individual RID operations
- ✅ **Path management** - `PathService.HarvestOutput` correctly implemented

### ⚠️ Architectural Changes Required for CI

#### Critical Issue: Single-Machine vs Distributed CI

- **Problem:** Current implementation assumes single machine can access all RID outputs
- **Current:** All libraries/RIDs write to shared `artifacts/harvest_output/` directory
- **CI Requirement:** Multiple OS runners need separate staging areas before consolidation

#### Required Path Architecture Changes

```text
Current (Local Dev):     artifacts/harvest_output/{library}/
Future CI Staging:       artifacts/harvest_staging/{library}/{rid}/
Future CI Consolidated:  artifacts/harvest_output/{library}/
```

#### Required BuildPaths Updates

- Add `HarvestStaging` property for per-RID CI uploads
- Modify `HarvestTask` to support staging vs consolidated output modes
- Update `ConsolidateHarvestTask` to process staging directory structure

### 🔄 Next Steps for CI Implementation

#### Phase 1 Completion

1. **Update BuildPaths class:**
   - Add `HarvestStaging` path property
   - Add methods for staging path generation per library/RID

2. **Modify HarvestTask:**
   - Add flag/parameter for CI staging mode
   - Output to staging path when in CI mode
   - Maintain current behavior for local development

3. **Update CI Workflow Design:**
   - Matrix jobs upload individual `harvest-staging-{library}-{rid}` artifacts
   - Consolidation job downloads all, reorganizes, runs `ConsolidateHarvestTask`
   - Upload final consolidated `harvest-output` artifact

4. **Update PackageTask:**
   - Modify to consume new `harvest-manifest.json` format
   - Process actual RID structure from consolidated results

#### Phase 2 & 3 (Future)

- Implement full CI workflows as documented
- Add tag-based automation triggers
- Implement public promotion workflows

### 📝 Documentation Accuracy Status

This document has been updated to reflect:

- ✅ **Correct harvest output structure** (actual JSON format)
- ✅ **Current implementation status** of each component
- ✅ **Architectural limitations** for distributed CI
- ✅ **Required changes** for CI implementation
- ✅ **Clear distinction** between local development (working) vs CI requirements (needs changes)

The core harvest consolidation functionality is complete and working for local development. The primary remaining work is adapting the path structure and workflow for distributed CI operations across multiple operating systems.
