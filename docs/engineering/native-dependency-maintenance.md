# Native Dependency Maintenance

Date: 2026-11-06

[Native build](../architecture/native-build.md) owns linkage, codecs, Unix identity and builder policy.
This guide owns dependency and overlay procedures. Run native work in a provisioned checkout under
[workflow](workflow.md#isolated-checkouts). Run commands from the repository root.

## Sources of truth

Read the baseline, dependencies, features and overrides from [vcpkg.json](../../vcpkg.json).
Read library metadata, concrete families and RID/triplet mappings from the
[manifest](../../build/manifest.json). Read port differences from the
[overlay registry](../../vcpkg-overlay-ports/README.md) and triplet mechanics from
[the triplet directory](../../vcpkg-overlay-triplets/). Do not maintain a second version or runtime table.
Family versioning belongs to [packaging and release](../architecture/packaging-and-release.md#versioning-and-labels).

## Install and caches

Pass both overlay directories to manual vcpkg invocations. Choose the triplet from the manifest:

```bash
./external/vcpkg/vcpkg install --triplet x64-windows-hybrid \
  --overlay-triplets=./vcpkg-overlay-triplets --overlay-ports=./vcpkg-overlay-ports
```

The [vcpkg setup action](../../.github/actions/vcpkg-setup/action.yml) owns CI cache path, key and
restore-key order. Its key includes the manifest, overlay trees, triplet and vcpkg submodule commit.
Ordinary local installs use vcpkg's OS-default user cache. To reproduce a CI cache issue, set
`VCPKG_DEFAULT_BINARY_CACHE` explicitly to the chosen cache directory. Keep cache paths consistent
with [.gitignore](../../.gitignore) when changing CI cache configuration.

## Baseline and version update

Use this single procedure for a security fix, upstream library release or vcpkg port update.

1. Read the current pin and desired versions. Inspect available ports with `vcpkg search sdl2` or
   `vcpkg search sdl3`; inspect a port's history with `vcpkg x-history <port>`.
2. Fetch the approved vcpkg commit in [the submodule](../../external/vcpkg/) and check it out.
   Record `git -C external/vcpkg rev-parse HEAD` as `builtin-baseline` in the root vcpkg manifest.
3. Update root overrides and matching `library_manifests[].vcpkg_version` / `vcpkg_port_version`
   together. Those fields preserve exact upstream patch and port metadata; family patch is independent.
4. Compare every overlay directory with its upstream port at that commit. Sync unchanged files
   byte-identically. Reapply intentional portfile and feature changes. Update overlay version,
   port-version, source hash and CMake options to match upstream. G60 rejects overlay version drift;
   a successful install alone is not proof of version coherence.
5. If source changed, regenerate patches from pristine source with the
   [patch procedure](../../vcpkg-overlay-ports/README.md#creating-or-updating-a-patch).
   Test `git apply --check` against that source. Never hand-write patch hunks.
6. Compare the hybrid triplets with stock triplets. Preserve the dynamic SDL override, Unix visibility,
   position-independent code and platform identity settings. Never change the builder floor as part
   of an update.
7. Reinstall for each affected triplet with both overlays. If an investigation needs a clean rebuild,
   remove only the affected port's build/package cache and installed triplet after reviewing the paths.
8. Resolve versions and run preflight:

   ```pwsh
   dotnet run --file tools.cs -- build --target ResolveVersionsFromManifest --suffix=local.check --versions-file artifacts/resolve-versions/versions.json
   dotnet run --file tools.cs -- build --target PreFlightCheck --versions-file artifacts/resolve-versions/versions.json
   ```

9. Run the generator and classify the complete diff under
   [binding maintenance](binding-generator-maintenance.md). An upstream patch may change headers;
   minor releases may add functions. Do not infer compatibility from the version label alone.
10. Run harvest and native smoke, then the package-first consumer loop in
     [developing](developing.md). Inspect receipts under `artifacts/harvest_output/` and perform the
     checks below on every affected OS/architecture. For diagnostic harvest output, pass
     `--verbosity Diagnostic` through the build forwarder.
     The `Package` target regenerates the README mapping block before pack validation; review that diff.
11. Update the overlay registry's rationale and source references, consumer documentation if format
    coverage changed, and this procedure if mechanics changed. Commit submodule pin, manifests,
    overlays, regenerated outputs and documentation together under [workflow](workflow.md).

On version lookup failure, confirm the override exists at the new baseline. On build failure,
inspect the upstream portfile and its changed dependencies before changing system exclusions.
On preflight failure, reconcile the named manifest, overlay or project; do not bypass the check.

## Add a triplet

1. Start from the closest stock triplet in the vcpkg submodule's triplet directories.
2. Add the architecture/system settings in [overlay triplets](../../vcpkg-overlay-triplets/) and include
   [the shared fragment](../../vcpkg-overlay-triplets/_hybrid-common.cmake). Keep build logic out of triplets.
3. Add the manifest runtime mapping and update the [triplet README](../../vcpkg-overlay-triplets/README.md).
4. Install with both overlays. Verify closure, payload and public exports below on the actual target.

## Add or retire an overlay port

Copy the entire upstream port directory into [overlay ports](../../vcpkg-overlay-ports/).
Make targeted changes and explain each locally. Keep every unchanged patch, usage file and manifest
byte-identical to upstream. The [overlay README](../../vcpkg-overlay-ports/README.md) is the canonical
registry of why an overlay exists and what differs; update it in the same change.

If upstream removes the need for an overlay, delete that overlay and validate the upstream port with
the same native and package checks. The setup action conditionally supplies overlay flags; remove a
type's flag only if no overlays of that type remain and the action is updated consistently.
Upstream prefers external codec dependencies, so bundled-codec changes may not be upstreamable.

## Add a satellite family

1. Add the port, intended features and version override in the root vcpkg manifest. Follow the codec
   policy in [native build](../architecture/native-build.md#codecs-and-attribution).
2. Add its library metadata and primary-binary patterns to the build manifest. Add a concrete family
   with managed/native project paths, library reference, change paths and the same-major core dependency.
   Extend the shared hybrid port matcher when a new SDL port needs dynamic linkage.
3. Create managed and native projects following existing siblings under [src](../../src/) and
   [native](../../src/native/). Use the shared native packaging properties and consumer targets.
   The extraction/copy targets are required package assets, not an optional .NET Framework feature.
4. Add header/config coverage and **run the generator** through
   [binding maintenance](binding-generator-maintenance.md). Do not source declarations from an
   unrelated community file or write P/Invokes by hand. Review all output against ABI policy.
5. Add the projects to [the solution](../../Janset.SDL2.sln). Follow the baseline procedure above for
   version resolution, preflight and the package-first loop, including mapping-block regeneration.
6. Install using hybrid triplets and both overlays. Harvest the library on each supported target,
   check the receipt, add native and package consumer smoke coverage, and validate dependency bounds.
7. Update consumer installation guidance and current coverage in the operational-state document.
   If a convenience metapackage exists, update its dependencies too.

## Platform checks

Every hybrid build must satisfy the payload and dynamic-closure checks. Export checks distinguish
required public API from unintended dependency symbols; a successful compile is not runtime evidence.

| Check | Windows | Linux | macOS |
|---|---|---|---|
| Payload | SDL-family DLLs only | SDL-family SOs and identity symlinks | SDL-family dylibs and identity symlinks |
| Dynamic closure | `dumpbin /dependents` | `ldd` | `otool -L` |
| Public exports | `dumpbin /exports` | `nm -D` or `readelf -Ws` | `nm -gU` |

Satellites may reference SDL core and manifest-excluded OS libraries. Codec libraries such as zlib,
libpng and FreeType must not appear as separate dynamic dependencies. For example:

```pwsh
dumpbin /dependents vcpkg_installed/x64-windows-hybrid/bin/SDL2_image.dll
```

```bash
ldd vcpkg_installed/x64-linux-hybrid/lib/libSDL2_image.so
otool -L vcpkg_installed/x64-osx-hybrid/lib/libSDL2_image.dylib
nm -D vcpkg_installed/x64-linux-hybrid/lib/libSDL2_image.so
nm -gU vcpkg_installed/x64-osx-hybrid/lib/libSDL2_image.dylib
```

Verify public satellite entry points remain visible, particularly gfx after patch updates. Inspect
unexpected `deflate`, `png_read`, `FT_`, `WebP_` and `op_` exports separately from closure leaks.
[Native build](../architecture/native-build.md#unix-identity-and-visibility) owns the platform risk
distinction. Preserve Unix symlink chains and core install-name/rpath references through packaging.

For review, record upstream byte comparisons, pristine-source patch checks, preflight results,
install/harvest evidence, payload/closure/export results, generated-output review and the affected
package consumer matrix. [Quality gates](quality-gates.md) owns completion requirements.
