# Research: Linux/macOS Symlink Handling for NuGet Native Packages

**Date**: 2026-04-11
**Context**: NuGet cannot preserve Unix symlinks. This document explains the problem, surveys solutions, and justifies our tar.gz approach.

## The Problem

Linux shared libraries use a **symlink chain** convention:

```text
libSDL2.so                    → symlink to libSDL2-2.0.so.0
libSDL2-2.0.so.0              → symlink to libSDL2-2.0.so.0.3200.4  (SONAME)
libSDL2-2.0.so.0.3200.4       ← actual binary file
```

When another library (e.g., `libSDL2_image.so`) is compiled, the linker records the **SONAME** (`libSDL2-2.0.so.0`) as the dependency — not the full version or the unversioned name. At runtime, the dynamic linker resolves this chain.

**NuGet's `.nupkg` format is a ZIP file. ZIP does not support symbolic links.**

When `nuget pack` or `dotnet pack` encounters symlinks:

- It **dereferences** them, copying the actual file content for each link
- This results in 3x size bloat (3 copies of the same binary)
- More critically, **dependent libraries expect the SONAME to exist as a filename**

This is tracked as:

- [NuGet/Home#10734](https://github.com/NuGet/Home/issues/10734) — Open since April 2021, Priority 2/Backlog
- [NuGet/Home#12136](https://github.com/NuGet/Home/issues/12136) — Closed as external

**There is no fix on the NuGet roadmap.**

## Solutions Evaluated

### Option A: Ship Only Unversioned Name (SkiaSharp Approach)

**How**: Compile with custom SONAME, or rename the binary to `libSDL2.so` at harvest time.
**Used by**: SkiaSharp (`libSkiaSharp.so`)

**Why we rejected this**:

1. **vcpkg doesn't produce unversioned .so files**. The build outputs the standard versioned chain.
2. **Renaming the primary binary is not enough**: Every transitive dependency also uses versioned SONAMEs. For example, `libSDL2_image.so` internally references `libpng16.so.16`, not `libpng16.so`. If we rename `libpng16.so.16` to `libpng16.so`, the runtime linker won't find it.
3. **Editing ELF SONAME metadata** (via `patchelf`) is technically possible but makes the entire dependency chain fragile. One missed binary and runtime linking fails silently.
4. **SkiaSharp can do this because they control the build**: They compile Skia from source with a custom SONAME. We use vcpkg, which produces standard Linux-convention outputs.

### Option B: Hash-Based Unique Names (LibGit2Sharp Approach)

**How**: Ship a single file with a unique name like `libgit2-381caf5.so`. DllImport uses this exact name.
**Used by**: LibGit2Sharp

**Why we rejected this**:

1. **Only works for a single library**: LibGit2Sharp ships one native file. We ship 5-30+ files per package (SDL2 + all transitive dependencies).
2. **Transitive dependencies still use versioned SONAMEs**: Even if we rename `libSDL2.so` to `libSDL2-abc123.so`, its dependency `libpulse.so.0` (or any vcpkg-built dep) still expects its standard name.
3. **DllImport name must be a constant**: Cannot use runtime-computed hash names with traditional `[DllImport]`.

### Option C: tar.gz Archive (Our Approach)

**How**: Package Unix natives as `native.tar.gz` inside the NuGet package. Use `buildTransitive` MSBuild targets to extract the archive at build time, recreating the symlink structure.

**How it works**:

1. During harvest, Cake creates a tar.gz archive preserving symlinks
2. The `.nupkg` contains `runtimes/{rid}/native.tar.gz` instead of loose files
3. A `.targets` file in `buildTransitive/` runs `tar -xzf` during the build
4. Symlinks are recreated in the consuming project's output directory

**Pros**:

- Preserves exact symlink chain as vcpkg produces it
- No binary editing, no renaming, no fragile post-processing
- Standard tar format — supported everywhere
- MSBuild integration is straightforward

**Cons**:

- Extra build step for consumers (tar extraction)
- Requires tar on the consumer's system (available on all target platforms)
- .NET runtime can't directly probe `runtimes/{rid}/native/` (files are inside tar)
- Slightly more complex `.targets` file

### Option D: Publish All Versions as Separate Files (Naive Approach)

**How**: Let NuGet dereference symlinks, shipping 3 copies of each binary.

**Why we rejected this**:

- 3x package size
- Runtime linker may not find the right file if the SONAME doesn't match the actual filename
- Wastes bandwidth and disk space

## Our Implementation

The tar.gz approach was chosen as the best balance of correctness and practicality.

### Harvest Side (Cake Frosting)

- `IArtifactDeployer` detects Unix platforms and creates tar.gz archives
- Archives preserve symlink metadata (GNU tar format)
- A separate status file records what's inside the archive

### Consumer Side (MSBuild Targets)

- `buildTransitive/{PackageId}.targets` contains an MSBuild target
- Runs after build, before the output is finalized
- Extracts `native.tar.gz` to the output directory
- Preserves symlinks on Linux/macOS
- No-ops on Windows (Windows packages use direct file copy)

### Future Considerations

If NuGet ever adds symlink support (unlikely near-term given the issue's priority), we can simplify by shipping loose files. The tar.gz approach is our stable workaround until then.

## Sources

- [NuGet/Home#10734 — Symlink support in nupkg](https://github.com/NuGet/Home/issues/10734)
- [NuGet/Home#12136 — Unable to install Linux shared libraries with symlinks](https://github.com/NuGet/Home/issues/12136)
- [dotnet/sdk#33845 — .NET Native Library Packaging](https://github.com/dotnet/sdk/issues/33845)
- [SkiaSharp symlink workaround](https://github.com/mono/SkiaSharp/issues/1252)
- [patchelf — ELF binary editor](https://github.com/NixOS/patchelf)
