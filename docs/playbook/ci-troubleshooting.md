# Playbook: CI/CD Troubleshooting

> Common CI failures and how to diagnose them.

## Workflow Overview

```text
prepare-native-assets-main.yml  (orchestrator, manual trigger)
├── prepare-native-assets-windows.yml   (x64, x86, arm64)
├── prepare-native-assets-linux.yml     (x64 in ubuntu:20.04, arm64 in ubuntu:24.04)
└── prepare-native-assets-macos.yml     (x64 on macos-15-intel, arm64 on macos-latest)
```

Each platform workflow:

1. Checkout repo + submodules
2. Run `vcpkg-setup` composite action (bootstrap, cache, install)
3. Setup .NET SDK
4. Build Cake Frosting host
5. Run Harvest task
6. Upload artifacts

## Common Failures

### vcpkg Install Fails

**Symptoms**: `vcpkg install` exits non-zero, build logs show compilation errors

**Diagnosis**:

1. Check which triplet failed (the workflow logs the triplet)
2. Look for the specific port that failed in the vcpkg build log
3. Common causes:
   - Missing system dependencies (especially on Linux containers)
   - Network issues downloading sources
   - Triplet-specific build incompatibilities

**Fixes**:

- **Linux missing deps**: Check the `apt-get install` step in `prepare-native-assets-linux.yml`. SDL2 requires X11, Wayland, ALSA, and other dev packages. If vcpkg adds new features, new deps may be needed.
- **Cache corruption**: Delete the GitHub Actions cache for the affected triplet and re-run.
- **vcpkg port bug**: Check [vcpkg issues](https://github.com/microsoft/vcpkg/issues) for known problems with the specific port version.

### vcpkg Cache Miss

**Symptoms**: Build takes 15-30 minutes instead of 2-3 minutes

**Diagnosis**: Check the `vcpkg-setup` action's cache restore step. The cache key is:

```text
vcpkg-bin-{os}-{triplet}-{vcpkg.json hash}-{vcpkg submodule commit}
```

If any of these change, the cache misses. This is expected after:

- vcpkg baseline updates
- vcpkg.json changes (new features, new dependencies)
- vcpkg submodule updates

**Fix**: First run after a change will be slow. Subsequent runs will use the new cache.

### Cake Build Fails

**Symptoms**: `dotnet build` or `dotnet run` in `build/_build/` fails

**Diagnosis**: Check the error message. Common causes:

- .NET SDK version mismatch (check `global.json`)
- NuGet package restore failure (network issues or feed problems)
- Code compilation errors (if build code was changed)

**Fix**: Ensure CI uses the correct .NET SDK version from `global.json`.

### Harvest Produces Empty Results

**Symptoms**: Harvest completes but RID status shows no binaries collected

**Diagnosis**:

1. Check that vcpkg actually built the library (look for installed files in vcpkg output)
2. Check the binary patterns in `manifest.json` — do they match actual filenames?
3. Check the system_artefacts.json whitelist — is the library being excluded?
4. Run with `--verbosity Diagnostic` to see detailed scanning output

**Fix**: Usually a pattern mismatch in `manifest.json` or a new system library that needs whitelisting.

### Artifact Upload Fails

**Symptoms**: Build succeeds but artifact upload step fails

**Diagnosis**: Check GitHub Actions storage limits and artifact naming. Common issues:

- Artifact name conflicts between matrix jobs
- Total artifact size exceeding limits
- Path too long (Windows-specific)

**Fix**: Ensure each matrix job produces uniquely-named artifacts.

### Linux Container Issues

**Symptoms**: Failures specific to Linux workflows

**Common issues**:

- **Git safe directory**: The container's workspace may not be in git's safe directory list. The workflow should run `git config --global --add safe.directory $(pwd)`.
- **Missing locales**: Some tools need UTF-8 locale. Set `LANG=C.UTF-8` or install `locales` package.
- **ARM64 emulation**: `ubuntu-24.04-arm` runners are real ARM64 hardware, not emulated. If the runner is unavailable, the job queues indefinitely.

### autoconf version too old (ubuntu:20.04)

**Symptoms**: gperf (or other autotools-based ports) fail with `Autoconf version 2.70 or higher is required` during `autoreconf`.

**Root cause**: ubuntu:20.04 ships autoconf 2.69. Newer vcpkg baselines pull in ports (e.g. gperf 3.3 via harfbuzz) that require autoconf >= 2.70.

**Fix**: The Linux workflow installs autoconf 2.72 from source after the apt packages. This is a build-time tool only — it does not affect the shipped binaries or glibc target. See #77 and upstream microsoft/vcpkg#48169.

### mpg123 arm64 build failure (NEON64 + fixed-point math)

**Symptoms**: mpg123 fails on arm64-linux with `#error "Bad decoder choice together with fixed point math!"`.

**Root cause**: The upstream vcpkg port's FPU detection incorrectly reports no FPU on arm64 Linux containers, enabling `REAL_IS_FIXED` which conflicts with `OPT_NEON64`.

**Fix**: A vcpkg overlay port at `vcpkg-overlay-ports/mpg123/` patches the FPU detection to force `HAVE_FPU=1` on arm64 Linux. The `vcpkg-setup` action passes `--overlay-ports` to vcpkg automatically. Remove this overlay when upstream fixes land. See #78 and upstream microsoft/vcpkg#40709.

**Maintenance**: This overlay requires re-validation on every vcpkg baseline bump. The dependency chain is `sdl2-mixer` → `mpg123`. See `vcpkg-overlay-ports/README.md` for the full maintenance protocol.

### macOS-Specific Issues

**Symptoms**: Failures only on macOS workflows

**Common issues**:

- **Xcode version**: Different macOS runner images have different Xcode versions. Check `macos-15-intel` vs `macos-latest` default Xcode.
- **Homebrew packages**: If `autoconf`/`automake`/`libtool` are needed, ensure they're installed in the workflow.
- **Universal binaries**: `otool` may show different results for x86_64 vs arm64 slices in universal binaries.

## Debugging Tips

### Re-run with Debug Logging

```yaml
# Add to workflow env
env:
  ACTIONS_STEP_DEBUG: true
```

Or re-run with "Enable debug logging" checkbox in GitHub Actions UI.

### Run Locally to Reproduce

```bash
# Match the CI environment as closely as possible
cd build/_build
dotnet run -- --target Harvest --library SDL2 --rid {your-rid} --verbosity Diagnostic
```

### Check vcpkg Build Logs

vcpkg build logs are in:

```text
external/vcpkg/buildtrees/{port-name}/build-{triplet}-out.log
external/vcpkg/buildtrees/{port-name}/build-{triplet}-err.log
```

### Pre-flight Check

Before debugging complex issues, run the pre-flight check:

```bash
cd build/_build
dotnet run -- --target PreFlightCheck
```

This validates that `manifest.json` and `vcpkg.json` are consistent.
