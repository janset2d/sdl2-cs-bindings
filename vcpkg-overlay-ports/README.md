# vcpkg Overlay Ports

This directory contains local overrides for vcpkg ports that have upstream bugs affecting our build matrix. Overlays are vcpkg's official mechanism for patching ports without forking the vcpkg repo.

**How it works:** The `vcpkg-setup` GitHub Action passes `--overlay-ports` pointing to this directory. When vcpkg encounters a port name that exists here, it uses our version instead of the upstream port.

## Active Overlays

### mpg123

- **Why:** arm64 Linux FPU detection bug — container environments incorrectly report no FPU, causing `REAL_IS_FIXED` + `OPT_NEON64` compile conflict.
- **Upstream issue:** microsoft/vcpkg#40709
- **Tracking issue:** #78
- **Dependency chain:** `sdl2-mixer` (feature: mpg123) → `mpg123`
- **Based on upstream version:** 1.33.4 (vcpkg baseline `0b88aacd`)

### Maintenance Rules

1. **On vcpkg baseline bump:** Verify each overlay still applies cleanly. Compare `vcpkg-overlay-ports/{port}/` against `external/vcpkg/ports/{port}/` — if the upstream portfile.cmake, vcpkg.json, or patches changed, the overlay must be re-synced.
2. **On upstream fix:** When the upstream issue is resolved in a vcpkg commit that our baseline includes, delete the overlay directory and remove the `--overlay-ports` flag if no overlays remain.
3. **On dependency version change:** If the parent library (e.g. sdl2-mixer) bumps its mpg123 dependency version, the overlay's SHA512 and patches may need updating.

Keep this README current — it is the canonical registry of why each overlay exists.
