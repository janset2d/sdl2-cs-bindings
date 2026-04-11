# Research: Native Library Packaging Patterns in .NET

**Date**: 2026-04-11
**Context**: Evaluating whether our `.Native` package split is overcomplicated or best practice.

## Conclusion

**Our approach is best practice.** Separate `.Native` packages following the `runtimes/{rid}/native/` convention is the dominant pattern used by all major .NET projects with native dependencies.

## Industry Survey

### SkiaSharp — Three-Tier Platform Split

```
SkiaSharp                          (managed bindings)
├── SkiaSharp.NativeAssets.Win32   (win-x86, win-x64, win-arm64)
├── SkiaSharp.NativeAssets.Linux   (linux-x64, linux-arm64, etc.)
├── SkiaSharp.NativeAssets.macOS   (osx-x64, osx-arm64)
├── SkiaSharp.NativeAssets.iOS
├── SkiaSharp.NativeAssets.Android
└── SkiaSharp.NativeAssets.WebAssembly
```

- Per-OS native packages, not per-RID
- Ships single unversioned `.so` file (custom soname to avoid symlink issues)
- Two Linux variants: standard and `NoDependencies` (statically linked for Docker/Alpine)

### LibGit2Sharp — Single Native Package

```
LibGit2Sharp                       (managed bindings)
└── LibGit2Sharp.NativeBinaries    (all platforms in one package)
```

- All RIDs in a single native package (native library is small)
- Uses git-commit-hash suffix in filename (`libgit2-381caf5.so`) to avoid symlink issues
- `.props` file for .NET Framework compatibility

### SQLitePCLRaw — Four-Tier Plugin Architecture

```
SQLitePCLRaw.bundle_e_sqlite3     (meta/convenience)
├── SQLitePCLRaw.core              (interface layer)
├── SQLitePCLRaw.provider.e_sqlite3 (DllImport bridge)
└── SourceGear.sqlite3             (native binaries per RID)
```

- Most sophisticated approach: allows swapping native implementations
- Plugin architecture via `ISQLite3Provider` interface
- `Batteries_V2.Init()` pattern for auto-configuration

### Our Approach — Per-Library Native Split

```
Janset.SDL2.Core                   (managed bindings)
└── Janset.SDL2.Core.Native        (native binaries, all RIDs)
```

- Each SDL satellite library gets its own binding + native pair
- Similar to LibGit2Sharp's simplicity but modular like SkiaSharp
- Users reference `Janset.SDL2.Core`, `.Native` is pulled transitively

## Key .NET Runtime Behavior

### How `runtimes/{rid}/native/` Works

1. **RID-specific publish** (`dotnet publish -r linux-x64`): SDK copies matching `runtimes/{rid}/native/` files to output.
2. **Framework-dependent** (no RID): Creates `.deps.json` with `runtimeTargets`. Runtime probes the matching RID directory.
3. **Library name resolution**: `[DllImport("SDL2")]` on Linux tries: `SDL2.so`, `libSDL2.so`, `SDL2`, `libSDL2` automatically.

### .NET 8+ RID Changes

The portable RID asset list replaced the deep RID fallback graph. Packages must use specific RIDs (`linux-x64`) rather than generic parents (`linux`, `unix`). This is relevant for our packaging.

## NuGet Size Constraints

- NuGet.org maximum package size: **250 MB**
- Our estimate per `.Native` package: 5-15 MB per library across 7 RIDs
- Well within limits — no need for per-OS split (unlike SkiaSharp which has much larger binaries)

## Recommendation for Janset.SDL2

1. **Keep per-library split** (not per-OS like SkiaSharp) — our native binaries are small enough
2. **Add `Janset.SDL2` meta-package** for convenience
3. **Ensure managed packages depend on `.Native` transitively** — users never reference `.Native` directly
4. **`buildTransitive` targets** for .NET Framework compatibility (already implemented for Core.Native)
5. **Consider `NativeLibrary.SetDllImportResolver`** for advanced scenarios (net5.0+)

## Sources

- [Microsoft: Including native libraries in .NET packages](https://learn.microsoft.com/en-us/nuget/create-packages/native-files-in-net-packages)
- [Microsoft: Native library loading](https://learn.microsoft.com/en-us/dotnet/standard/native-interop/native-library-loading)
- [NuGet symlink support issue #10734](https://github.com/NuGet/Home/issues/10734)
- [SkiaSharp project structure](https://github.com/mono/SkiaSharp)
- [LibGit2Sharp.NativeBinaries](https://github.com/libgit2/libgit2sharp.nativebinaries)
- [SQLitePCL.raw](https://github.com/ericsink/SQLitePCL.raw)
