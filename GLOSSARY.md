# Janset.SDL2

Domain language for the SDL binding and native-package families that underpin Janset2D.

## Language

**Hybrid triplet**:
A native build profile that keeps SDL libraries dynamic while incorporating their non-system dependencies statically.
_Avoid_: Strategy mode, all-dynamic profile.

**Harvest**:
The collection of a native library's deliverable binaries, dependency closure and license attribution for packaging.
_Avoid_: DLL copy, native download.

**Binary closure**:
The complete dependency set reachable from a selected native binary, distinguished from libraries supplied by the operating system.
_Avoid_: Expected-dependency list, direct-dependency list.

**Satellite library**:
An SDL companion library for a focused capability such as images, audio mixing, fonts, graphics helpers or networking.
_Avoid_: Core library, plugin.

**Package family**:
A release unit containing one managed binding package and its native package, sharing one family version.
_Avoid_: Package train, independently versioned managed/native pair.

**Family identifier**:
The canonical identity of a package family within an SDL major-version line.
_Avoid_: Package ID, library filename.

**Core family**:
The package family for the central SDL library of an SDL major-version line.
_Avoid_: All SDL packages, satellite family.

**Satellite family**:
A non-core package family that depends on the core family of the same SDL major-version line.
_Avoid_: Independent SDL implementation.

**Family version**:
The single release version shared by a family's managed and native packages, anchored to its upstream major/minor line.
_Avoid_: Native version, binding-only version.

**Targeted release**:
A release selecting specific package families without releasing every family.
_Avoid_: Full-train release, single-package release.

**Full-train release**:
A coordinated release of the complete selected ecosystem of package families for a cross-cutting change.
_Avoid_: Targeted release, brand version.

**Binding tier**:
The public, C-named, ABI-faithful typed binding surface of a family.
_Avoid_: Layer 1, Layer 2, raw layer, L1, L2.

**Friendly tier**:
The ergonomic overloads that adapt managed text, buffers and call shapes to the binding surface.
_Avoid_: Layer 3, L3, wrapper layer.

**Per-member ABI demotion**:
Hiding only a member's native import when its stable public shape needs a managed adaptation.
_Avoid_: Separate raw layer, blanket forwarding projection.

**Package-first consumer contract**:
The agreement that consumer validation exercises packaged bindings and natives, regardless of where those packages were prepared.
_Avoid_: Source mode, native project injection.

**Linux-canonical output**:
Generated binding output derived from the agreed Linux header environment and used as the common cross-platform artifact.
_Avoid_: Host-shaped output, Windows-canonical output.

**Canonical header bundle**:
The pinned native header set that defines the common input for reproducible binding generation.
_Avoid_: System headers, latest upstream headers.

**Builder base**:
The native build environment whose compatibility floor constrains the libraries delivered to consumers.
_Avoid_: Generator runtime, CI runner version.
