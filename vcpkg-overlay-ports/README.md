# vcpkg Overlay Ports

This directory contains local overrides for vcpkg ports that have upstream bugs affecting our build matrix. Overlays are vcpkg's official mechanism for patching ports without forking the vcpkg repo.

**How it works:** The `vcpkg-setup` GitHub Action conditionally passes `--overlay-ports` pointing to this directory (only if the directory exists). When vcpkg encounters a port name that exists here, it uses our version instead of the upstream port.

## Active Overlays

### mpg123

- **Why:** arm64 Linux FPU detection bug — container environments incorrectly report no FPU, causing `REAL_IS_FIXED` + `OPT_NEON64` compile conflict.
- **Upstream issue:** microsoft/vcpkg#40709
- **Tracking issue:** #78
- **Dependency chain:** `sdl2-mixer` (feature: mpg123) → `mpg123`
- **Based on upstream version:** 1.33.4 (vcpkg baseline `0b88aacd`)
- **Files changed from upstream:** Only `have-fpu.diff` (FPU detection patch). All other files (`vcpkg.json`, `portfile.cmake`, `pkgconfig.diff`) are identical copies of the upstream port.

## How Patches Work in vcpkg

vcpkg extracts source tarballs and applies patches using `git apply`. This means:

- Patch files **must** be in unified diff format (standard `git diff` output)
- **Never write patch files by hand** — they will almost certainly have encoding, whitespace, or line count issues that cause `error: corrupt patch`
- On Windows, `git diff` can sometimes output UTF-16 — patches must be UTF-8

### Creating or Updating a Patch

```bash
# 1. Find the pristine source tarball (vcpkg caches downloads)
ls external/vcpkg/downloads/{library}-{version}.tar.*

# 2. Extract to a temp directory
mkdir -p /tmp/{library}-patch
cp external/vcpkg/downloads/{library}-{version}.tar.bz2 /tmp/{library}-patch/
cd /tmp/{library}-patch
tar xjf {library}-{version}.tar.bz2
cd {library}-{version}

# 3. Initialize a git repo on the pristine source
git init && git add -A && git commit -m "pristine"

# 4. Make your changes (edit files directly)

# 5. Generate the patch
git diff > /path/to/vcpkg-overlay-ports/{library}/your-patch.diff

# 6. Validate: reset and test apply
git checkout -- .
git apply --check /path/to/vcpkg-overlay-ports/{library}/your-patch.diff
```

**Important:** If the upstream port already has patches (like `have-fpu.diff` or `pkgconfig.diff`), your overlay patch replaces the upstream one entirely. Generate your patch against the **unpatched** source, including both the upstream fix and your additional changes.

## Maintenance Rules

### On vcpkg baseline bump

1. Compare each overlay against the new upstream port: `diff -r vcpkg-overlay-ports/{port}/ external/vcpkg/ports/{port}/`
2. If `vcpkg.json` or `portfile.cmake` changed upstream, re-sync the overlay copies
3. If the source version changed, re-generate patches against the new pristine source using the workflow above
4. Run `git apply --check` against the new source tarball before pushing

### On upstream fix

When the upstream issue is resolved in a vcpkg commit included in our baseline:

1. Delete the overlay directory
2. Test that `vcpkg install` works without the overlay
3. If no overlays remain, the `--overlay-ports` flag is automatically skipped (conditional in `vcpkg-setup` action)

### On dependency version change

If the parent library (e.g. `sdl2-mixer`) bumps its dependency version (e.g. mpg123 1.33.4 → 1.34.x), the overlay's SHA512 in `portfile.cmake` and all patches may need updating against the new source.

## Overlay Port Checklist

Use this when adding or updating an overlay:

- [ ] All unchanged files are byte-identical to upstream (`diff` against `external/vcpkg/ports/{port}/`)
- [ ] Patches generated via `git diff` from pristine source (never hand-written)
- [ ] `git apply --check` passes against the pristine source tarball
- [ ] This README updated with: why, upstream issue link, tracking issue, dependency chain, base version
- [ ] CI passes on all affected platforms

Keep this README current — it is the canonical registry of why each overlay exists.
