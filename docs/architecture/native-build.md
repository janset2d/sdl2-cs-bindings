# Native Build

Date: 2026-11-06

## Sources and package boundary

Build native libraries from source through [vcpkg](../../external/vcpkg/). Do not substitute downloaded
binary bundles. [vcpkg.json](../../vcpkg.json) owns dependencies, features, overrides and the baseline;
the [manifest](../../build/manifest.json) owns library metadata and the RID/triplet/runner matrix.
Every manifest runtime is built and validated; the RID matrix is not reduced to unblock a release.
The managed package brings its family's separate `.Native` package transitively.
[Packaging and release](packaging-and-release.md) owns the package contract.
Evidence: internal research, 2026-04-11, "Research: Native Library Packaging Patterns in .NET".

## Hybrid-static model

Each SDL library remains dynamic. Its non-system transitive dependencies are linked statically into it.
Satellites may depend dynamically on SDL core and permitted OS libraries, not separate codec DLLs or SOs.
This keeps the native payload small without embedding a separate SDL core in every satellite.

The [hybrid triplets](../../vcpkg-overlay-triplets/) encode the model in their names.
Their [shared fragment](../../vcpkg-overlay-triplets/_hybrid-common.cmake) owns linkage and visibility
settings: default static linkage, dynamic CRT, and a dynamic override for SDL ports.
Unix builds hide dependency symbols; Linux also fixes ELF rpaths.
Windows triplets build only the release configuration.
Runtime rows have no strategy field; there is no strategy CLI switch.

Harvest derives the binary closure from actual binaries and vcpkg metadata. It does not use a
hand-maintained expected-dependency list. [BinaryClosureWalker](../../build/_build/Targets/Harvest/Services/BinaryClosureWalker.cs)
walks dependencies through dumpbin, ldd or otool. The manifest owns system exclusions.
[HybridStaticLeakValidator](../../build/_build/Validation/Harvesting/HybridStaticLeakValidator.cs)
rejects non-system transitive shared-library leaks.

## Codecs and attribution

Use the LGPL-free codec stack. The [mixer overlay registry](../../vcpkg-overlay-ports/README.md#sdl2-mixer)
owns backend choices, licenses and differences from upstream. The overlay enables bundled permissive
alternatives that the stock port disables, and removes the LGPL backends.
Evidence: internal research, 2026-04-14, "LGPL-Free Codec Migration — SDL2_mixer";
internal research, 2026-04-13, "SDL2 Native Dependency License Inventory".

Native MIDI uses the OS on Windows and macOS. Linux Timidity needs a configuration and GUS patches
on the consuming machine. Use MP3 or OGG when that user-side MIDI setup is not appropriate.
Harvest collects license attribution; consolidation and package validation must not drop it.

## Unix identity and visibility

Unix native payloads travel as per-package tar.gz archives. ZIP-based NuGet packages cannot preserve
the symlink chains that SONAME and dylib identities need. The shared
[consumer targets](../../src/native/_shared/Janset.SDL2.Native.Common.targets) extract the archive at
consumer build time. A package-specific tarball name prevents collisions when NuGet flattens native assets.
macOS install names use `@rpath`; Linux symlink chains retain the versioned identity.
Evidence: internal research, 2026-04-22, "Unix Native Identity Strategy — vcpkg, SDL2 Family, Overlay Ports, and the Single-File Question".

`-fvisibility=hidden` reduces exports from static dependencies. Explicit default-visibility annotations
can still expose them. Windows PE exports are opt-in; macOS two-level namespaces isolate symbol lookup;
Linux's flat ELF scope can interpose symbols from the first loaded library.
The [gfx overlay registry](../../vcpkg-overlay-ports/README.md#sdl2-gfx) owns its public-export patch.
Evidence: internal research, 2026-04-14, "Symbol Visibility in Hybrid Static Packaging — Analysis & Strategy".

## Builder and validation boundaries

[ADR-0010](../adr/0010-linux-builder-glibc-floor.md) owns the immutable Linux builder floor.
Generator runtime requirements are separate from consumer-native requirements.
Linux static dependencies linked into shared objects require position-independent code; vcpkg supplies it.

The [C++ native smoke harness](../../tests/smoke-tests/native-smoke/) uses CMake and vcpkg to prove
OS-level loading and initialization without managed interop. It remains IDE-debuggable.
Package consumer smoke then checks the actual NuGet restore, native extraction and P/Invoke path.
[Maintenance](../engineering/native-dependency-maintenance.md) owns update procedures and platform checks;
[quality gates](../engineering/quality-gates.md) owns completion evidence.
