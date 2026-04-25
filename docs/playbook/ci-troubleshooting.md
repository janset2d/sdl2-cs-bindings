# Playbook: CI/CD Troubleshooting

> Common CI failures on `release.yml` + `build-linux-container.yml` and how to diagnose them. Pre-Slice-E-follow-up-pass version (covering the retired `prepare-native-assets-*.yml` family) is preserved at [`docs/_archive/ci-troubleshooting-2026-04-25.md`](../_archive/ci-troubleshooting-2026-04-25.md) — kept readable for archaeological "why does the overlay port for mpg123 exist?" type questions.

## Workflow Overview (post-Slice-E-follow-up-pass)

Two workflow files cover the build + release pipeline:

```text
release.yml                          (10-job pipeline; tag-push + workflow_dispatch triggers)
├── build-cake-host                  (single-runner FDD publish; Coverage-Check ratchet gate)
├── resolve-versions                 (ADR-003 version-source provider entrypoint)
├── preflight                        (single-runner fail-fast; G54 + G58 + structural validators)
├── generate-matrix                  (dynamic 7-RID matrix from manifest.runtimes[])
├── harvest (matrix, 7 RIDs)         (per-RID vcpkg-setup + harvest + native-smoke inline)
├── consolidate-harvest              (aggregation; merges per-RID status JSON)
├── pack                             (per-family pack + post-pack G21–G58 validators)
├── consumer-smoke (matrix, 7 RIDs)  (matrix re-entry; per-TFM TUnit smoke + restore)
├── publish-staging                  (gated `if: false`; Phase-2b stub)
└── publish-public                   (gated `if: false`; Phase-2b stub)

build-linux-container.yml            (multi-arch GHCR builder image; workflow_dispatch + monthly cron)
├── build (matrix: amd64, arm64)     (per-arch native build via docker/build-push-action; push-by-digest)
├── merge                            (docker buildx imagetools create → focal-<yyyymmdd>-<sha> + focal-latest)
└── retention                        (delete-only-untagged-versions; keeps 5 most recent)
```

Composite actions live under `.github/actions/`:

- **`vcpkg-setup`**: container digest resolution (mutable tag → immutable digest for cache-key) + bootstrap + install.
- **`nuget-cache`**: cross-OS workspace `NUGET_PACKAGES` + `actions/cache@v5` keyed on `hashFiles('**/packages.lock.json', '**/Directory.Packages.props', '**/*.csproj')`.
- **`platform-build-prereqs`**: macOS brew per-formula idempotent install; Linux + Windows no-ops (Linux baked into builder image, Windows self-sourced via `IMsvcDevEnvironment`).

## Common Failures

### `cake-host` artifact download fails

**Symptoms**: Consumer job step "Download cake-host artifact" fails with "artifact not found".

**Diagnosis**: `build-cake-host` job either failed or published the artifact under a different name. Check the upstream job's "Upload cake-host artifact" step.

**Fixes**:

- If `build-cake-host` failed: fix that job first (most common upstream causes are `Coverage-Check` ratchet + `Build.Tests` test failure).
- Run-scoped retention: `cake-host` is uploaded with `retention-days: 1`. If you re-run a downstream job in isolation after >24h, the artifact is gone. Solution: re-run the entire workflow (or `build-cake-host` first), then the dependent job.

### `Coverage-Check` ratchet gate fails

**Symptoms**: `build-cake-host` job's "Coverage-Check" step exits non-zero with `coverage X.Y% < floor Z.W%`.

**Diagnosis**: Either coverage genuinely regressed below the baseline floor, or the `coverage.cobertura.xml` path mismatched.

**Fixes**:

- Genuine regression: add tests, or (carefully) adjust the floor in `build/coverage-baseline.json` if the new code surface justifies a temporary dip — explain in the commit body.
- Path mismatch: the `dotnet test --coverage --coverage-output` step writes to `${{ github.workspace }}/artifacts/test-results/build-tests/coverage.cobertura.xml` (absolute path). The `CoverageCheckTaskRunner.DefaultCoverageRelativePath` resolves the same location relative to repo root. If the `.cobertura.xml` doesn't appear, check the test step output for `Microsoft.Testing.Platform` errors.

### vcpkg install fails (port-specific)

**Symptoms**: `vcpkg-setup` step's `vcpkg install` exits non-zero; build log shows compilation errors for a specific port.

**Diagnosis**:

1. Note which **triplet** failed (logged at the top of `vcpkg-setup` Install step).
2. Identify the **port** that failed (look for `Building <port>:<triplet>` followed by errors).
3. Cross-check the vcpkg cache key — was it a fresh build (cold cache) or restored?

**Known port-specific issues**:


- **autoconf < 2.70 on focal/Ubuntu 20.04**: gperf 3.3 (transitive via harfbuzz) requires autoconf ≥ 2.70. The Linux builder image (`docker/linux-builder.Dockerfile`) builds autoconf 2.72 from source on top of focal's apt 2.69 to satisfy this. If a vcpkg port introduces a new autoconf-dependent transitive, autoconf 2.72 should still cover it; if a port requires autoconf > 2.72 in the future, bump the Dockerfile source-build URL and SHA256 pin in lockstep. See [#77](https://github.com/janset2d/sdl2-cs-bindings/issues/77).
- **mpg123 arm64 NEON64 + fixed-point math conflict**: vcpkg's upstream `mpg123` port FPU detection incorrectly reports `HAVE_FPU=0` on arm64 Linux containers, enabling `REAL_IS_FIXED` which conflicts with `OPT_NEON64`. Workaround: a vcpkg overlay port at [`vcpkg-overlay-ports/mpg123/`](../../vcpkg-overlay-ports/) patches the FPU detection to force `HAVE_FPU=1` on arm64 Linux. The `vcpkg-setup` composite passes `--overlay-ports` automatically. **Re-validate on every vcpkg baseline bump**; remove when upstream fixes land. See [#78](https://github.com/janset2d/sdl2-cs-bindings/issues/78) + [microsoft/vcpkg#40709](https://github.com/microsoft/vcpkg/issues/40709). Maintenance protocol: [`vcpkg-overlay-ports/README.md`](../../vcpkg-overlay-ports/).
- **Linux missing dev headers**: builder image bakes the canonical SDL2 dependency set (X11 + Wayland + ALSA + freepats for MIDI + libicu + autotools). If a vcpkg feature flag introduces a new system dep, update [`docker/linux-builder.Dockerfile`](../../docker/linux-builder.Dockerfile) and re-run `build-linux-container.yml` to publish a new `focal-<yyyymmdd>-<sha>` tag. The mutable `focal-latest` pointer retags onto the new digest after `imagetools create` runs.

**General fixes**:

- **Cache corruption**: GHCR + `actions/cache` invalidation differ. For `actions/cache` (vcpkg binary cache), delete the cache entry from the workflow's Caches UI and re-run. For GHCR (builder image), rebuild via `build-linux-container.yml` — the `focal-latest` pointer retags once the new image is pushed.
- **Upstream vcpkg port bug**: cross-reference [microsoft/vcpkg issues](https://github.com/microsoft/vcpkg/issues) for known port-version-specific problems. If an overlay port fixes it locally, follow the mpg123 pattern (patch + re-validate on baseline bumps).

### vcpkg cache miss (full cold rebuild)

**Symptoms**: A vcpkg-using job (`harvest` matrix entry) takes 15-30 minutes instead of the warm-cache 2-5 minutes.

**Diagnosis**: Check the `vcpkg-setup` step's "Restore NuGet cache (Unix)" / "(Windows)" log line — `Cache hit for: <key>` vs `Cache restored from key:` (restore-key fallback) vs no-hit cold build.

The cache key shape (`.github/actions/vcpkg-setup/action.yml`):

```text
vcpkg-bin-{platform-identity}-{triplet}-{hashFiles(vcpkg.json, vcpkg-overlay-triplets/**, vcpkg-overlay-ports/**)}-{vcpkg-submodule-commit}
```

Where `{platform-identity}` is:
- **Host jobs** (Windows, macOS): the runner label as-is (e.g., `windows-2025`, `macos-15-intel`).
- **Container jobs** (Linux): the GHCR image's per-platform child manifest digest, resolved inline by `vcpkg-setup` from the mutable `:focal-latest` tag (matched via `runner.os` + `runner.arch`).

Triggers for cache miss (legitimate cold rebuild expected):
- Vcpkg baseline / submodule commit bump.
- `vcpkg.json` change (new feature flag, new dependency, version override).
- Overlay triplet / port edit.
- GHCR builder image refresh (new digest behind `focal-latest`).
- Runner image label change (operator updated `manifest.runtimes[].runner`).

**Fix**: First run after a legitimate change is slow; subsequent runs hit the new key.

### `RestoreLockedMode` strict-mode failure (NU1004 / NU1005 / NU1009)

**Symptoms**: `build-cake-host` job's `dotnet test build/_build.Tests/Build.Tests.csproj` fails with `NU1004: The package references have changed` or `NU1009: implicitly referenced packages` during restore.

**Diagnosis**: P5 (Slice E follow-up) introduced strict lock-file mode on the build-host csprojs only. Strict mode is gated on `$(GITHUB_ACTIONS)='true'` in `build/_build/Build.csproj` + `build/_build.Tests/Build.Tests.csproj`. `src/**` csprojs use lenient mode (lock files committed for diff visibility, but drift regenerates rather than fails) precisely because SDK-implicit packages (`Microsoft.NET.ILLink.Tasks` per .NET runtime patch, `Microsoft.NETFramework.ReferenceAssemblies` per host OS) drift past the local SDK on every monthly Microsoft cadence.

**Fixes**:

- **Build-host strict failure**: legitimate lock-file drift on bounded package surface. Run `dotnet restore build/_build.Tests/Build.Tests.csproj --force-evaluate` locally, commit the regenerated `packages.lock.json` files. Build-host package surface is `Cake.Frosting + OneOf + NuGet.Versioning + ...` — none patch-driven, so genuine drift is rare and usually intentional (CPM bump).
- **NEVER copy strict mode to `src/`**: a previous attempt failed with NU1004 + NU1009 chains because Linux SDK auto-implicit-defines `Microsoft.NETFramework.ReferenceAssemblies` for every csproj at root level CPM. Lenient mode in `src/Directory.Build.props` is the correct discipline. See [memory: Lock-File Strict-Mode Scope Discipline](../../) for the full rationale.

### `consumer-smoke` matrix entry fails on net4x runtime

**Symptoms**: `consumer-smoke (win-x86)` or similar net4x-targeting matrix entry fails with `Failed to resolve apphost` / `BadImageFormatException` / `DllNotFoundException`.

**Diagnosis**: net4x AnyCPU + native-package presence triggers SDK auto-x86 RuntimeIdentifier inference, which is indistinguishable from user-explicit `-r win-x86`. Cake's `IPackageConsumerSmokeRunner.AppendNet4xPlatformArgument` forwards `-p:Platform=<arch>` for net4* TFMs alongside `-r <rid>` so the consumer-side `_JansetSdlNativeCopyDllsForFrameworkWindows` target reads `Platform`/`Prefer32Bit`/`OSArchitecture` (deliberately ignoring `RuntimeIdentifier`).

**Fixes**:

- If smoke is genuinely failing (regression): trace via the consumer-side targets in [`src/native/_shared/Janset.SDL2.Native.Common.targets`](../../src/native/_shared/Janset.SDL2.Native.Common.targets) (in-file comments are the canonical reference for net4x copy contract).
- For end-user-side failures see the [Consumer Guideline section in cross-platform-smoke-validation.md](cross-platform-smoke-validation.md#netframework-anycpu-consumer-guideline).

### `consumer-smoke` matrix entry fails on `dotnet pack` PATH (WSL / Linux)

**Symptoms**: Local witness on WSL hits `MSB1001: Unknown switch` during `dotnet pack` of `src/native/SDL2.*.Native/`. Doesn't reproduce on Windows, doesn't reproduce in CI Linux runners (only WSL).

**Diagnosis**: WSL inherits Windows PATH (`appendWindowsPath=true` default), `/mnt/c/Program Files/dotnet/dotnet` ends up ahead of `/home/$USER/.dotnet/dotnet`. Cake host starts on Linux dotnet (full-path resolution) but child `dotnet pack` resolves through naked PATH lookup → picks Windows dotnet → MSBuild Windows can't parse `/home/...` Linux paths.

**Fix**: Set Linux-only PATH explicitly before any J/K checkpoint invocation. See [cross-platform-smoke-validation.md §"WSL / Linux"](cross-platform-smoke-validation.md#wsl--linux). CI Linux is unaffected — `/mnt/c/...` doesn't exist there, only Linux paths in PATH.

### Builder image `focal-latest` 404 / multi-arch missing manifest

**Symptoms**: `harvest` Linux matrix entry fails at `actions/checkout@v6` with `Error response from daemon: pull access denied` or `manifest unknown`. Or `docker pull` of `focal-latest` returns only one arch (e.g., amd64 missing or arm64 missing).

**Diagnosis**: Two known causes:
- **Untagged version pruned by retention**: the `retention` job's `actions/delete-package-versions` previously had `delete-only-untagged-versions: false`, which swept platform-specific runtime manifests that GHCR reported as "untagged" (they were members of the multi-arch image index, not standalone-tagged). Post-2026-04-22 fix: `delete-only-untagged-versions: true` constrains deletes to genuinely-orphaned versions.
- **SBOM/provenance attestation noise**: `docker/build-push-action@v6` with attestations enabled produces 2 manifests per arch (runtime + attestation). Tagless attestation manifests look like "untagged versions" to retention tooling. Workflow disables them: `provenance: false` + `sbom: false`.

**Fixes**:

- Re-run `build-linux-container.yml` with `workflow_dispatch` to rebuild + re-merge the multi-arch image. The `focal-latest` pointer retags onto the new digest via `imagetools create`.
- If the retention configuration was reverted, check `delete-only-untagged-versions: true` is still set.
- For deeper investigation: `docker buildx imagetools inspect ghcr.io/janset2d/sdl2-bindings-linux-builder:focal-latest` shows the current manifest list and per-arch digests.

### `harvest` produces empty results / NativeSmoke MIDI decoder missing on Linux

**Symptoms**: `harvest` matrix entry runs but RID status JSON shows `primary_files_count: 0` for a library, or `NativeSmoke` reports `Mix decoder: MIDI` as "decoder missing" on Linux.

**Diagnosis**:

1. **Empty harvest**: check `manifest.json` `library_manifests[].primary_binaries[].patterns` — do they match actual filenames in `vcpkg_installed/<triplet>/`? Run `--target Inspect-HarvestedDependencies --rid <rid>` locally to see scanner output. Most common cause: a vcpkg port renames a binary across baseline bumps.
2. **MIDI decoder missing on Linux**: SDL_mixer's bundled internal Timidity only supports GUS `.pat` patches and only registers the `MIDI` decoder when it finds a GUS-format config at init. The Linux builder image installs `freepats` exactly for this reason (drops GUS patches at `/usr/share/midi/freepats/` + `/etc/timidity/freepats.cfg`). If the `freepats` apt install ever drops out of `docker/linux-builder.Dockerfile`, MIDI decoder discovery silently fails.

**Fixes**:

- Empty harvest from pattern mismatch: update `manifest.json` `primary_binaries[].patterns` to match the new vcpkg output filename.
- Empty harvest from new system dep: add the dep to `manifest.json` `system_exclusions.<os>` (whitelist) so the scanner doesn't try to bundle it.
- MIDI decoder missing: re-check `freepats` in [`docker/linux-builder.Dockerfile`](../../docker/linux-builder.Dockerfile); rebuild the builder image if it dropped out.

### macOS-specific issues

**Common surfaces**:

- **Xcode version drift**: `macos-15-intel` (osx-x64 runner) vs `macos-26` (osx-arm64 runner — current `macos-latest`) ship different Xcode versions. CMake / clang behavior can diverge on toolchain features.
- **Mono availability for net462**: net462 runtime smoke on macOS depends on a `mono` binary in `$PATH`. `macos-14` shipped Mono 6.12 preinstalled; `macos-15` removed it. Cake's `IPackageConsumerSmokeRunner.IsMonoAvailableOnPath()` probes for this and gates net462 runtime execution accordingly. To enable net462 runtime coverage: install `brew install mono` or the [mono-project.com MDK pkg](https://www.mono-project.com/download/stable/) on the runner. Compile-time net462 coverage runs unconditionally (via `Microsoft.NETFramework.ReferenceAssemblies` SDK-implicit ref).
- **Homebrew formula drift**: `platform-build-prereqs` composite is idempotent per-formula (`brew list --formula <name>` check before install) but assumes `pkg-config + autoconf + automake + libtool` are install-targets. New vcpkg formulas added at port build-time would surface as install-failures here; bump the composite formula list in lockstep with vcpkg port additions.
- **otool universal binary slices**: `otool -L` on a universal `.dylib` shows different `LC_LOAD_DYLIB` per arch. The harvest scanner (`MacOtoolScanner`) handles this; if you're running `otool` manually for debugging, pick the arch slice explicitly via `lipo -extract <arch>` first.

## Debugging Tips

### Re-run with debug logging

GitHub Actions UI: re-run with the "Enable debug logging" checkbox. Or set `ACTIONS_RUNNER_DEBUG: true` in workflow `env:` for persistent debug output. The Cake host honours `ACTIONS_RUNNER_DEBUG` and forces `--verbosity diagnostic` automatically (see `Program.cs` `GetEffectiveCakeArguments`).

### Run locally to reproduce

For a CI matrix entry that reproduces locally (host-RID matches the failing matrix RID), follow [cross-platform-smoke-validation.md](cross-platform-smoke-validation.md) A-K checkpoint sequence. The Cake target invocation contract is identical between CI and local (CI just adds artifact upload/download wrapping).

For non-host RIDs (e.g., reproducing a `linux-arm64` failure on a Windows dev box) the only path is `workflow_dispatch` on `release.yml` with the matrix narrowed via `manifest.runtimes[]` edit — there is no local cross-RID emulation flow today.

### Check vcpkg build logs

vcpkg per-port build logs land at:

```text
external/vcpkg/buildtrees/<port-name>/build-<triplet>-out.log
external/vcpkg/buildtrees/<port-name>/build-<triplet>-err.log
```

In CI, these aren't uploaded as artifacts by default. If a port build is consistently failing in CI but not locally, add an artifact upload step to `release.yml` `harvest` job conditional on failure (`if: failure()`) for the buildtrees subtree.

### Pre-flight check (local)

Before a CI dispatch, run `--target PreFlightCheck --rid <rid>` locally to validate `manifest.json` ↔ `vcpkg.json` consistency + strategy coherence + per-csproj pack contract (G4/G6/G7/G17/G18) + G54 upstream version alignment + G58 cross-family resolvability.
