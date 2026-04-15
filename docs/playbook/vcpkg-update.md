# Playbook: Updating vcpkg Baseline and Library Versions

> How to update the vcpkg baseline to get newer library versions.

## When to Update

- SDL2 or satellite libraries have security fixes or important bugfixes
- vcpkg ports have been updated with new features or build fixes
- Starting a new development phase and want current versions

## Current State

Check current versions in `vcpkg.json` overrides and compare with vcpkg registry:

```bash
# See what you have pinned
cat vcpkg.json | grep -A3 '"overrides"'

# Check latest available in vcpkg
./external/vcpkg/vcpkg search sdl2
./external/vcpkg/vcpkg search sdl3
```

## Step-by-Step Update

### Step 1: Update vcpkg Submodule

```bash
cd external/vcpkg
git fetch origin
git checkout master
git pull origin master
cd ../..
```

### Step 2: Get New Baseline Commit Hash

The baseline is the vcpkg commit that determines which port versions are available:

```bash
cd external/vcpkg
git rev-parse HEAD
# Example output: abc123def456...
cd ../..
```

### Step 3: Update vcpkg.json

Update the `builtin-baseline` field:

```json
{
  "builtin-baseline": "NEW_COMMIT_HASH_HERE"
}
```

### Step 4: Check Available Versions

With the new baseline, check what versions are available:

```bash
./external/vcpkg/vcpkg search sdl2 --x-json
```

### Step 5: Update Version Overrides

Update each library's override to the desired version:

```json
"overrides": [
  {
    "name": "sdl2",
    "version": "2.32.10",
    "port-version": 0
  }
]
```

### Step 6: Update manifest.json

Keep `build/manifest.json` in sync with vcpkg.json:

- Update `vcpkg_version` fields
- Update `vcpkg_port_version` fields
- Update `native_lib_version` fields

### Step 7: Validate

```bash
# Run pre-flight check (validates manifest.json ↔ vcpkg.json consistency + runtime strategy coherence)
cd build/_build
dotnet run -- --target PreFlightCheck

# Test a build for one platform
./external/vcpkg/vcpkg install --triplet x64-windows-release

# Test harvest for one RID
dotnet run -- --target Harvest --library SDL2 --rid win-x64
```

### Step 8: Commit

```bash
git add external/vcpkg vcpkg.json build/manifest.json
git commit -m "chore: update vcpkg baseline to $(cd external/vcpkg && git rev-parse --short HEAD) — SDL2 2.32.10"
```

## Troubleshooting

### "Version not found" after baseline update

The version you specified in overrides might not exist at the new baseline. Check:

```bash
./external/vcpkg/vcpkg x-history sdl2
```

### Build fails after update

New library versions may introduce new dependencies or change behavior:

1. Check the vcpkg port's changelog: `external/vcpkg/ports/sdl2/portfile.cmake`
2. Run harvest with diagnostic verbosity: `--verbosity Diagnostic`
3. Compare `system_artefacts.json` — new system dependencies may need whitelisting

### Pre-flight check fails

Versions in `manifest.json` don't match `vcpkg.json`. Update manifest.json to match.

## Version Compatibility Notes

- **SDL2 patch versions** (e.g., 2.32.4 → 2.32.10): Generally safe, API-compatible
- **SDL2 minor versions** (e.g., 2.30.x → 2.32.x): May add new functions, review binding coverage
- **Satellite library updates**: Usually API-compatible within a major version
- **SDL2_gfx**: Frozen at 1.0.4, will never update
