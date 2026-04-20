# Playbook: Cross-Platform Smoke Validation

> How to verify that the Cake build host, harvest pipeline, native libraries, and package-consumer path work correctly across the supported local hosts after a refactor or significant change.

**Last validated:** 2026-04-17 post-smoke-expansion.
**Result (2026-04-17):** Two validation layers are currently true at the same time:

- The historical Phase 2a proof slice remains green on the three original hybrid-static hosts for `sdl2-core` + `sdl2-image` (`win-x64`, `linux-x64`, `osx-x64`).
- The newer expanded Windows-host slice is now green end to end for `sdl2-core`, `sdl2-image`, `sdl2-mixer`, `sdl2-ttf`, and `sdl2-gfx` on `win-x64`: native-smoke passes with the widened codec/render coverage (`28 passed, 0 failed`), Harvest reports `1 primary / 0 runtime` for every SDL2 satellite, dumpbin confirms the harvested satellites depend only on `SDL2.dll` plus CRT/system DLLs, and PostFlight is green for `net9.0`, `net8.0`, and `net462` against a local package family version (`1.3.0-validation.win64.1` during the latest run).

**Scope note:** PA-2 moved the remaining four rows (`win-arm64`, `win-x86`, `linux-arm64`, `osx-arm64`) onto hybrid overlay triplets, but those rows are still not validated end to end here. The expanded package-consumer smoke scope is code-complete, but only the Windows `win-x64` host slice has been re-verified with that wider family set so far.

## When to Run This

- After any build-host refactor (Cake tasks, DI wiring, service boundaries)
- After manifest.json schema changes
- After vcpkg baseline or overlay triplet changes
- After CI workflow command surface changes
- Before declaring a multi-session refactor "done"
- When a new stream (C, D, F) lands changes that touch cross-platform behavior

## Recommended Big-Smoke Rules

- Treat `artifacts/` as a **single-run disposable workspace** for this playbook. If you want a trustworthy answer to "everything still works", start from an empty artifact tree instead of reusing old `harvest_output/`, `packages/`, or consumer-smoke caches.
- Do **not** define success as "every Cake target was invoked once." Some targets are lifecycle gates (`PreFlightCheck`, `EnsureVcpkgDependencies`, `Harvest`, `ConsolidateHarvest`, `Package`, `PackageConsumerSmoke`, `PostFlight`), some are diagnostics (`Dumpbin-Dependents`, `Ldd-Dependents`, `Otool-Analyze`), and some are local-only safeguards (`Coverage-Check`). The big smoke should exercise the lifecycle gates on every platform and the diagnostics where they add evidence.
- `SetupLocalDev --source=local` is a useful **developer-convenience umbrella** because it prepares the local feed after `EnsureVcpkgDependencies -> Harvest -> ConsolidateHarvest`, but it does **not** replace an explicit `PreFlightCheck` gate in this playbook. Keep PreFlight as a standalone fail-fast step.
- Use a **fresh version suffix per run** and never mix package families from multiple smoke attempts in `artifacts/packages/`. Even with orchestrator-supplied version properties, a dirty local feed is how you accidentally end up debugging ghosts.
- Record results per platform as a bundle: command log, harvested dependency inspection, native-smoke output, and final package-consumer result. If one platform goes red, you want evidence, not vibes.

## Smoke Matrix

This matrix is a living validation surface that grows as new Cake tasks and streams land. Each checkpoint has a stream origin so you know when it was introduced and whether it applies to the current codebase.

### Active Checkpoints

These are validated today and should pass on all 3 platforms.

| # | Checkpoint | Stream | What It Proves | Expected Output |
| --- | --- | --- | --- | --- |
| A | Build-host unit tests | Baseline | Refactored code logic is correct | 337 passed, 0 failed on the current branch (2026-04-19 local validation) |
| B | Cake restore + build (Release) | Baseline | Build host compiles clean on all platforms | 0 warnings, 0 errors (usually implied by A — tests build the same assemblies) |
| C | Cake `--tree` | Baseline | Task dependency graph is intact | `PostFlight → PackageConsumerSmoke → Package → PreFlightCheck`; `ConsolidateHarvest → Harvest → Info` |
| D | PreFlightCheck | Baseline + A-risky + S1 | manifest.json ↔ vcpkg.json consistency + strategy coherence + post-S1 csproj pack contract (G4/G6/G7/G17/G18) | 6/6 versions, 7/7 strategies, 6/6 families × 10/10 csprojs all green |
| D1 | EnsureVcpkgDependencies | Baseline | vcpkg bootstrap scripts + manifest install work for the current triplet, with overlay triplets/ports applied | bootstrap only when needed, install exits 0, triplet/overlay paths logged |
| E | Harvest (scoped to slice under test) | Baseline | Binary closure walk + deployment works per-platform | per-library `1 primary, 0 runtime, DirectCopy/Archive` green, rid-status JSON generated |
| F | ConsolidateHarvest | Baseline | Per-RID merge produces manifest + summary | `harvest-manifest.json` + `harvest-summary.json` per library |
| G | Native smoke (C++) | Baseline | Hybrid-built natives load and initialize at runtime | Current expanded Windows harness: `28 passed, 0 failed, Result: ALL PASS` |
| J | PackageTask | D-local (post-S1) | Family-aware pack produces valid `.nupkg` per library (managed + native + .snupkg) + post-pack validator suite (G21–G23, G25–G27, G47, G48) passes on every produced package | 3 `.nupkg` files per family at `--family-version`; post-pack validator 0 violations |
| K | PackageConsumerSmoke | D-local (post-S1, expanded on Windows) | `PackageReference` restore from local feed + consumer-side `buildTransitive` target fires + runtime smoke succeeds for the concrete package-consumer set (`sdl2-core`, `sdl2-image`, `sdl2-mixer`, `sdl2-ttf`, `sdl2-gfx`) + Unix symlink chain preserved | per-TFM TUnit pass; current Windows host expectation is 12 passing tests on `net9.0`/`net8.0` and 11 passing tests on `net462`; netstandard2.0 compile-sanity passes |

**Scope caveat for J and K (2026-04-17):** The current code path for `PackageConsumerSmoke` requires the concrete five-family smoke scope (`sdl2-core`, `sdl2-image`, `sdl2-mixer`, `sdl2-ttf`, `sdl2-gfx`). That widened scope is re-validated on `win-x64`. Linux and macOS still retain the older proof-slice evidence for `sdl2-core` + `sdl2-image`; rerunning the expanded scope there is still Phase 2b work, as is end-to-end validation for the newly hybridized rows (`win-arm64`, `win-x86`, `linux-arm64`, `osx-arm64`).

### Planned Checkpoints

These will be added as their parent streams land. Add the command reference and "what to look for" details when promoting from planned to active.

| # | Checkpoint | Stream | What It Will Prove | Promotion Criteria |
| --- | --- | --- | --- | --- |
| H | GenerateMatrixTask | C | Dynamic CI matrix produces correct 7-RID JSON from manifest | Task implemented + validated against hardcoded YAML |
| I | PreFlightCheck as gate (expanded) | C | Version resolution, package-family integrity, unit tests as gate | Stream C PreFlight expansion landed |
| L | `SetupLocalDev --source=remote` | F | Remote artifact-source feed prep populates the local cache and writes `Janset.Smoke.local.props` correctly | `RemoteArtifactSourceResolver` implemented + authenticated feed download validated |
| M | J/K extended to remaining 4 hybrid-static RIDs | 2b | PackageTask + PackageConsumerSmoke green for `win-arm64`, `win-x86`, `linux-arm64`, and `osx-arm64` now that the overlay triplets (`x86-windows-hybrid`, `arm64-windows-hybrid`, `arm64-linux-hybrid`, `arm64-osx-hybrid`) exist | PA-1 decision landed + PA-2 overlay triplets merged + at least one newly-covered RID harvested and consumer-smoked on its native runner |

**Exit criteria:** All **active** checkpoints green on all 3 platforms. Any failure must be classified as environment issue vs code regression before proceeding. When promoting a planned checkpoint to active, update this table and add its command reference below.

## Platform Access

| Platform | Access Method | Repo Path | Triplet |
| --- | --- | --- | --- |
| Windows | Local (current machine) | `E:\repos\my-projects\janset2d\sdl2-cs-bindings` | `x64-windows-hybrid` |
| Linux | WSL from Windows | `/home/deniz/repos/sdl2-cs-bindings` | `x64-linux-hybrid` |
| macOS Intel | SSH: `Armut@192.168.50.205` | `/Users/armut/repos/sdl2-cs-bindings` | `x64-osx-hybrid` |

Keep all 3 repos on the same commit before running the matrix. Verify with `git log --oneline -1` on each.

## Per-Platform Environment Setup

### Windows

No special setup. Developer PowerShell recommended for native-smoke (provides MSVC environment), but Cake commands work from any terminal.

### WSL / Linux

dotnet is installed at `~/.dotnet/` but is **not in the default PATH**. Both `PATH` and `DOTNET_ROOT` must be set:

```bash
export PATH="$HOME/.dotnet:$PATH"
export DOTNET_ROOT="$HOME/.dotnet"
```

**Why `DOTNET_ROOT`?** TUnit uses Microsoft Testing Platform which produces a native apphost. The apphost resolves .NET runtime via `DOTNET_ROOT`, not `PATH`. Without it, tests build but fail at execution with `Failed to resolve libhostfxr.so`.

Consider adding these exports to `~/.bashrc` or `~/.profile` to avoid repeating them.

**native-smoke MIDI decoder prereq:** SDL_mixer's bundled internal Timidity only supports **GUS `.pat` patches** (not SF2) and only registers the `MIDI` decoder when it finds a GUS-format config at init. On Debian/Ubuntu install `freepats` (`sudo apt install -y freepats`) — that drops GUS patches + `/etc/timidity/freepats.cfg`, which SDL_mixer's bundled Timidity auto-searches via its `TIMIDITY_CFG_FREEPATS` fallback path. The alternative `timidity` apt package installs `/etc/timidity/timidity.cfg` pointing at FluidR3_GM.sf2 (a `%font` SF2 directive) — bundled Timidity does NOT parse SF2 binaries, so `timidity` alone does not register the decoder. Without `freepats` installed, `Mix decoder: MIDI` will report "decoder missing" — a clear signal rather than a silent skip. This is also an **end-user concern**: Janset ships the bundled Timidity code (Artistic License) but does not ship GUS patches (GPL); consumers on Linux who want MIDI install their own patches the same way. Packaging strategy for the end-user UX (doc-only vs opt-in `.Soundfonts` meta-package) is tracked in [phase-2-adaptation-plan.md PD-14](../phases/phase-2-adaptation-plan.md#pending-decisions) and will be resolved before the first public Mixer-family release.

### macOS (SSH)

Non-interactive SSH shells don't source `~/.zprofile` or `~/.zshrc`, so tools installed via Homebrew and the .NET SDK are invisible:

```bash
export PATH="/usr/local/share/dotnet:/usr/local/bin:$PATH"
export DOTNET_ROOT=/usr/local/share/dotnet
```

- dotnet: `/usr/local/share/dotnet/dotnet` (Intel Mac path)
- cmake + ninja: `/usr/local/bin/` (Homebrew)
- Apple Silicon Macs would use `/opt/homebrew/bin` instead of `/usr/local/bin`

## Command Reference

All commands assume you are at the repo root. The environment exports from the section above are assumed to be active on WSL and macOS.

### Local Multi-RID Sequencing Caveat

The repo-local `vcpkg_installed/` tree does **not** behave like a permanent multi-triplet cache in this workflow. In local manifest mode, the most recently installed triplet is the one you should assume is present under `vcpkg_installed/`.

- Do not assume `x64-windows-hybrid`, `x86-windows-hybrid`, and `arm64-windows-hybrid` will all remain materialized side by side after repeated local `vcpkg install --triplet ...` runs.
- For real local validation of more than one RID, use a staged loop: `install triplet -> native-smoke / dumpbin -> Harvest -> next triplet install -> Harvest -> ConsolidateHarvest -> Package/PostFlight`.
- CI is unaffected because each RID job installs exactly one triplet in isolation.

### Clean-Slate Artifact Rule

For a real "is everything still OK?" answer, wipe the generated artifacts before each platform run. Reusing `artifacts/packages/` or stale `harvest-manifest.json` files is how you get a fake green.

Suggested wipe scope:

- `artifacts/harvest_output/`
- `artifacts/packages/`
- `artifacts/package-consumer-smoke/`
- `artifacts/test-results/smoke/` (or whatever run-log folder you use)
- `tests/smoke-tests/native-smoke/build/` for the current platform preset

Example cleanup commands:

```powershell
# Windows
Remove-Item .\artifacts\harvest_output -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item .\artifacts\packages -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item .\artifacts\package-consumer-smoke -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item .\artifacts\test-results\smoke -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item .\tests\smoke-tests\native-smoke\build\win-x64 -Recurse -Force -ErrorAction SilentlyContinue
```

```bash
# Linux / macOS
rm -rf artifacts/harvest_output
rm -rf artifacts/packages
rm -rf artifacts/package-consumer-smoke
rm -rf artifacts/test-results/smoke
rm -rf tests/smoke-tests/native-smoke/build/linux-x64
rm -rf tests/smoke-tests/native-smoke/build/osx-x64
```

If you need the previous run for comparison, archive it somewhere outside `artifacts/` first.

### A. Build-Host Unit Tests

```bash
dotnet test build/_build.Tests/Build.Tests.csproj --no-restore
```

### B. Cake Restore + Build (Release)

```bash
dotnet restore build/_build/Build.csproj --use-lock-file
dotnet build build/_build/Build.csproj --configuration Release --no-restore
```

### C. Cake Task Tree

```bash
dotnet run --project build/_build/Build.csproj -- --tree
```

> **Note:** The flag is `--tree`, not `--showtree`. The latter does not exist and will fail.

### D. PreFlightCheck

```bash
# Windows (repo-root auto-detected via git):
dotnet run --project build/_build/Build.csproj -- --target PreFlightCheck --rid win-x64

# Linux / macOS (explicit repo-root recommended):
dotnet run --project build/_build/Build.csproj -- --target PreFlightCheck --rid linux-x64 --repo-root "$(pwd)"
```

### D1. EnsureVcpkgDependencies

This is the explicit bootstrap + install gate. `Harvest` depends on it already, but a big smoke should call it out as its own checkpoint so bootstrap/install failures are not confused with harvest regressions.

```bash
# Windows:
dotnet run --project build/_build/Build.csproj -- --target EnsureVcpkgDependencies --rid win-x64

# Linux:
dotnet run --project build/_build/Build.csproj -- --target EnsureVcpkgDependencies --rid linux-x64 --repo-root "$(pwd)"

# macOS:
dotnet run --project build/_build/Build.csproj -- --target EnsureVcpkgDependencies --rid osx-x64 --repo-root "$(pwd)"
```

**What to look for:**

- missing `vcpkg` gets bootstrapped successfully
- install uses the expected triplet plus overlay triplets/ports
- command exits 0 before you spend time debugging higher-level Cake stages

### E. Harvest (Full Satellite Set)

CI workflows use the Release binary directly. Local smoke should match:

```bash
# Windows:
./build/_build/bin/Release/net9.0/Build.exe --target Harvest \
  --library SDL2 --library SDL2_image --library SDL2_mixer \
  --library SDL2_ttf --library SDL2_gfx --library SDL2_net \
  --rid win-x64

# Linux:
./build/_build/bin/Release/net9.0/Build --target Harvest \
  --library SDL2 --library SDL2_image --library SDL2_mixer \
  --library SDL2_ttf --library SDL2_gfx --library SDL2_net \
  --rid linux-x64 --repo-root "$(pwd)"

# macOS:
./build/_build/bin/Release/net9.0/Build --target Harvest \
  --library SDL2 --library SDL2_image --library SDL2_mixer \
  --library SDL2_ttf --library SDL2_gfx --library SDL2_net \
  --rid osx-x64 --repo-root "$(pwd)"
```

**What to look for:**

- Each library shows "1 primary, 0 runtime" (hybrid-static: all transitive deps baked in)
- Windows: DirectCopy deployment
- Linux/macOS: Archive deployment (tar.gz preserving symlinks)
- No `CakeException` or error status in any library block

### F. ConsolidateHarvest

```bash
# Windows:
./build/_build/bin/Release/net9.0/Build.exe --target ConsolidateHarvest --rid win-x64

# Linux:
./build/_build/bin/Release/net9.0/Build --target ConsolidateHarvest --repo-root "$(pwd)"

# macOS:
./build/_build/bin/Release/net9.0/Build --target ConsolidateHarvest --repo-root "$(pwd)"
```

**What to look for:** `harvest-manifest.json` + `harvest-summary.json` generated under each library in `artifacts/harvest_output/`.

### F1. Harvested Artifact Dependency Inspection

`Harvest` already consumes dumpbin / ldd / otool through the runtime scanners, but the big smoke should still do a **manual artifact-side spot check** on the harvested payload. This is the easiest way to prove that the thing we are about to pack is actually the thing we think we built.

Use at least one representative binary per satellite (`SDL2`, `SDL2_image`, `SDL2_mixer`, `SDL2_ttf`, `SDL2_gfx`, and when ready `SDL2_net`). On Windows inspect the harvested DLL directly. On Linux/macOS extract the harvested `native.tar.gz` to a temp folder first, then run `ldd` / `otool -L` on the extracted shared library.

```powershell
# Windows example: inspect harvested SDL2_image.dll
dumpbin /dependents .\artifacts\harvest_output\SDL2_image\runtimes\win-x64\native\SDL2_image.dll

# Optional Cake diagnostic wrapper (pass --dll explicitly)
dotnet run --project .\build\_build\Build.csproj -- --target Dumpbin-Dependents --dll .\artifacts\harvest_output\SDL2_image\runtimes\win-x64\native\SDL2_image.dll
```

```bash
# Linux example: extract harvested archive, then inspect libSDL2_image*.so
rm -rf artifacts/temp/ldd/linux-x64
mkdir -p artifacts/temp/ldd/linux-x64
tar -xzf artifacts/harvest_output/SDL2_image/runtimes/linux-x64/native/native.tar.gz -C artifacts/temp/ldd/linux-x64
ldd "$(find artifacts/temp/ldd/linux-x64 -name 'libSDL2_image*.so*' | head -n 1)"

# Optional Cake diagnostic wrapper (pass --dll explicitly)
dotnet run --project build/_build/Build.csproj -- --target Ldd-Dependents --dll "$(find artifacts/temp/ldd/linux-x64 -name 'libSDL2_image*.so*' | head -n 1)" --rid linux-x64 --repo-root "$(pwd)"
```

```bash
# macOS example: extract harvested archive, then inspect libSDL2_image*.dylib
rm -rf artifacts/temp/otool/osx-x64
mkdir -p artifacts/temp/otool/osx-x64
tar -xzf artifacts/harvest_output/SDL2_image/runtimes/osx-x64/native/native.tar.gz -C artifacts/temp/otool/osx-x64
otool -L "$(find artifacts/temp/otool/osx-x64 -name 'libSDL2_image*.dylib' | head -n 1)"

# Optional Cake diagnostic wrapper (Otool can analyze one or more --dll values)
dotnet run --project build/_build/Build.csproj -- --target Otool-Analyze --dll "$(find artifacts/temp/otool/osx-x64 -name 'libSDL2_image*.dylib' | head -n 1)" --rid osx-x64 --repo-root "$(pwd)"
```

**What to look for:**

- Windows satellites depend on `SDL2.dll` plus CRT / system DLLs, not codec/transitive DLLs like `zlib1.dll`, `libpng16.dll`, `jpeg62.dll`, `libwebp.dll`, `freetype.dll`, `ogg.dll`, `vorbis*.dll`, etc.
- Linux/macOS satellites depend on the SDL2 core shared library and OS/system libraries/frameworks, not unexpected third-party shared objects that should have been baked in.
- The manual inspection agrees with what Harvest reported (`1 primary, 0 runtime`). If the scanner says green but raw tool output shows leaked deps, trust the raw tool and investigate.
- For `Dumpbin-Dependents` and `Ldd-Dependents`, pass `--dll` explicitly. Their current help text is looser than their real behavior.

### G. Native Smoke Test

See [tests/smoke-tests/native-smoke/README.md](../../tests/smoke-tests/native-smoke/README.md) for full details. Quick reference:

```bash
# Windows (from tests/smoke-tests/native-smoke/):
cmd.exe //c ".\build.bat"

# Linux:
cd tests/smoke-tests/native-smoke
cmake --preset linux-x64
cmake --build build/linux-x64
./build/linux-x64/native-smoke

# macOS:
cd tests/smoke-tests/native-smoke
cmake --preset osx-x64
cmake --build build/osx-x64
./build/osx-x64/native-smoke
```

**What to look for:** `Passed: 13, Failed: 0, Result: ALL PASS`

**Expanded Windows note:** older logs and docs may still mention `13/13`. The current widened Windows harness now covers PNG/JPEG/WebP/TIFF/AVIF loading, FLAC/MIDI/WavPack/Opus/OGG/MP3/MOD decoder discovery, `TTF_Init`, `SDL2_gfx` drawing, and `SDLNet_Init`, so the expected success shape on the expanded harness is `28 passed, 0 failed`.

### J. Package (family-aware pack + post-pack validator)

`Package` packs one or more families at a single `--family-version`. Requires Harvest + ConsolidateHarvest to have produced `harvest-manifest.json` for each library in scope first; the task fails fast if the harvest output is missing or empty.

```bash
# Single-family:
dotnet run --project build/_build/Build.csproj -- \
  --target Package --family sdl2-core \
  --family-version 2.32.0-smoke.1

# Multi-family (topological order respected, one --family-version applies to all):
dotnet run --project build/_build/Build.csproj -- \
  --target Package --family sdl2-core --family sdl2-image \
  --family-version 2.32.0-smoke.1

# Linux / macOS: add --repo-root "$(pwd)" as in the other targets.
```

> **D-3seg caveat (2026-04-18).** The single-`--family-version` flag is a pre-D-3seg orchestration pattern that applies the same version string to every family in the pack invocation. Under [ADR-001](../decisions/2026-04-18-versioning-d3seg.md), each family's version should anchor its own upstream Major.Minor (Core = `2.32.x`, Image = `2.8.x`, Gfx = `1.0.x`). G54 will reject a `sdl2-image` pack whose `--family-version` starts with `2.32.` because SDL2_image upstream is `2.8.x`. Today the playbook's multi-family example works only when every family happens to share an UpstreamMajor.UpstreamMinor line (currently only `sdl2-core`), OR when bootstrapping with `SetupLocalDev` (V5), which auto-generates per-family versions. Proper per-family `--family-version` override is tracked as a V5 `PackageTaskRunner` refinement.

**What to look for:**

- Three `.nupkg` files per family in `artifacts/packages/`: `Janset.SDL2.<Role>.<version>.nupkg` (managed), `Janset.SDL2.<Role>.<version>.snupkg` (symbols), `Janset.SDL2.<Role>.Native.<version>.nupkg` (native).
- Cake log emits `Packed family '<family>': <native.nupkg>, <managed.nupkg>, <symbols.snupkg>` once per family.
- Post-pack validator runs silently on success; any G21–G23, G25–G27, G47, or G48 violation is logged with its guardrail prefix and fails the task.
- Native nupkg layout check (optional, but useful after any `src/native/Directory.Build.props` change): `unzip -l artifacts/packages/Janset.SDL2.Core.Native.<version>.nupkg` should show `buildTransitive/Janset.SDL2.Core.Native.targets`, `buildTransitive/Janset.SDL2.Native.Common.targets`, and per-RID payload as either `runtimes/<rid>/native/*.dll` (Windows) or `runtimes/<rid>/native/Janset.SDL2.Core.Native.tar.gz` (Unix).

### K. PackageConsumerSmoke (consumer restore + runtime SDL_Init)

`PackageConsumerSmoke` runs the per-TFM TUnit smoke against the local feed produced by `Package`, plus a netstandard2.0 compile-sanity pass. The full `PostFlight` target chains PreFlight → Package → PackageConsumerSmoke in one invocation, which is the normal way to exercise J + K together:

```bash
# Windows:
dotnet run --project build/_build/Build.csproj -- \
  --target PostFlight --family sdl2-core --family sdl2-image \
  --family-version 2.32.0-smoke.1

# Linux / macOS: add --repo-root "$(pwd)".
```

**What to look for:**

- `Running dotnet compile-sanity netstandard2.0 consumer` passes first (compile-only sanity against the Compile.NetStandard consumer).
- One `Running dotnet test package-smoke (<tfm>)` line per executable TFM resolved from `PackageConsumer.Smoke.csproj`'s inherited `$(ExecutableTargetFrameworks)` — typically `net9.0`, `net8.0`, `net462`.
- `Failed: 0` for each TFM. On the current expanded Windows scope, passing tests include native asset landing for `core/image/mixer/ttf/gfx`, `SDL_Init_Cycle_Succeeds`, PNG fixture load, mixer decoder-surface validation, `TTF_Init`, a headless `SDL2_gfx` render path, and linked-version major checks. The Unix symlink assertion still applies on modern Unix TFMs only (`#if NET6_0_OR_GREATER`).
- net462 runtime coverage depends on the host's Mono availability:
  - **Windows**: always runs — .NET Framework ships natively, no Mono required.
  - **Linux**: always skipped with `Skipping package-smoke for TFM 'net462'` — Mono 6.12 cannot host the TUnit + Microsoft Testing Platform discovery stack (MissingMethodException at test discovery).
  - **macOS**: skipped unless a `mono` binary is discoverable on `$PATH`. macOS has **no built-in .NET Framework runtime**, and recent GitHub runner images regressed: `macos-14` shipped Mono 6.12 but `macos-15` (the current `macos-latest` default) removed it. To enable net462 runtime coverage locally or in CI, install classic Mono via `brew install mono` or the [mono-project.com MDK pkg](https://www.mono-project.com/download/stable/). Compile-time coverage of net462 runs regardless via `Microsoft.NETFramework.ReferenceAssemblies`; only the runtime TUnit slice is gated on Mono.
- If the task fails with `Access to the path 'Microsoft.Testing.Platform.dll' is denied` on Windows, see the "Lingering dotnet processes mitigation" subsection below — this is a pre-existing testhost-lock flake, not a regression in J or K.

## Output Artifacts to Verify

| Artifact | Location | What to Check |
| --- | --- | --- |
| RID status files | `artifacts/harvest_output/{Library}/rid-status/{rid}.json` | One per library, status = success |
| Native payload (Windows) | `artifacts/harvest_output/{Library}/runtimes/{rid}/native/*.dll` | DLLs present |
| Native payload (Unix) | `artifacts/harvest_output/{Library}/runtimes/{rid}/native/native.tar.gz` | Archive present, symlinks preserved |
| Harvest manifest | `artifacts/harvest_output/{Library}/harvest-manifest.json` | Generated after consolidation |
| Harvest summary | `artifacts/harvest_output/{Library}/harvest-summary.json` | Human-readable summary present |
| Managed package | `artifacts/packages/Janset.SDL2.{Role}.{version}.nupkg` | Produced by `Package`; nuspec has bare-min-range dependency on `Janset.SDL2.{Role}.Native` and `include="All"` on the within-family native dep |
| Managed symbols | `artifacts/packages/Janset.SDL2.{Role}.{version}.snupkg` | Produced by `Package`; contains `.nuspec` + `.pdb` entries |
| Native package | `artifacts/packages/Janset.SDL2.{Role}.Native.{version}.nupkg` | Produced by `Package`; ships `buildTransitive/Janset.SDL2.{Role}.Native.targets` (thin wrapper) + `buildTransitive/Janset.SDL2.Native.Common.targets` (shared extractor) plus per-RID payload (DLLs on Windows, `$(PackageId).tar.gz` on Unix) |

## Known Gotchas

| Issue | Platform | Root Cause | Fix |
| --- | --- | --- | --- |
| `dotnet: command not found` | WSL, macOS SSH | dotnet not in default non-interactive PATH | Set `PATH` explicitly (see environment setup above) |
| `Failed to resolve libhostfxr.so` | WSL | TUnit apphost needs `DOTNET_ROOT` | `export DOTNET_ROOT=$HOME/.dotnet` |
| `cmake: command not found` | macOS SSH | Homebrew not in non-interactive PATH | Add `/usr/local/bin` to PATH |
| `--showtree` fails | All | Flag does not exist | Use `--tree` instead |
| Linux Harvest is ~20× slower | WSL/Linux | ldd scanning is inherently slower than dumpbin | Expected behavior, not a regression |
| `vswhere.exe not found` warning | Windows | Git Bash doesn't load VS environment | Cosmetic — build.bat handles it via VsDevCmd.bat fallback |
| macOS SDK version mismatch | macOS | Multiple SDKs installed (8/9/10) | global.json pins to 9.0.x — verify with `dotnet --version` from repo root |
| Lingering `dotnet` processes after `PostFlight` / `PackageConsumerSmoke` | All | MSBuild worker nodes (`/nodemode:1 /nodeReuse:true`), VBCSCompiler (Roslyn), and testhost (Microsoft Testing Platform) stay alive ~10 min for reuse. On Windows they hold file handles on `Microsoft.Testing.Platform.dll` and break the next run's `bin/` cleanup. On macOS / Linux the same processes accumulate RAM (~100 MB each). | Automatic mitigation + manual fallback documented below under [Lingering dotnet processes mitigation](#lingering-dotnet-processes-mitigation). |

### Lingering dotnet processes mitigation

`PackageConsumerSmokeRunner` runs `dotnet build-server shutdown` on entry, again before each executable TFM slice, and passes `--disable-build-servers -p:UseSharedCompilation=false -nodeReuse:false` to every `dotnet build/test` invocation. That keeps the normal case clean: new runs do not spawn detachable MSBuild / Roslyn / Razor server children and the later `net462` slice is less likely to inherit bad state from earlier `net8.0` / `net9.0` runs.

**Shutdown call count per PostFlight run.** One shutdown fires on entry; one fires before **each** executable TFM slice. Concrete totals:

- Windows default (net9.0 + net8.0 + net462): **4 shutdowns** per PostFlight invocation.
- Linux default (net9.0 + net8.0; net462 skipped for Mono/TUnit incompatibility): **3 shutdowns**.
- macOS with `mono` on `$PATH` (net9.0 + net8.0 + net462): **4 shutdowns**. macOS without Mono (net9.0 + net8.0; net462 auto-skipped): **3 shutdowns**.

**Side-effect warning — `dotnet build-server shutdown` is scoped per-user, not per-project.** It also terminates CLI build servers owned by any other concurrent shell running `dotnet build` / `dotnet watch` / `dotnet test` (those processes re-spawn their servers on the next build; work is not lost, but there is a small warm-cache hit). The command does NOT touch Visual Studio, Rider, or VS Code / C# DevKit language-service MSBuild nodes — those run under different hosts and use separate IPC channels. If you are mid-way through a long parallel CLI build in another terminal, defer the `PostFlight` run until it finishes.

**Manual fallback — if something still wedges** (usually a prior run that was killed mid-flight and left `testhost.exe` holding `Microsoft.Testing.Platform.dll`):

```powershell
# Kill all CLI-launched build servers (same action the runner performs):
dotnet build-server shutdown

# Kill dotnet processes older than MSBuild's 10-minute reuse timeout (PowerShell):
Get-Process dotnet | Where-Object { ((Get-Date) - $_.StartTime).TotalMinutes -gt 15 } | Stop-Process -Force
```

On macOS / Linux the equivalent is `pkill -f /nodemode:1` for MSBuild worker nodes, or use the same `dotnet build-server shutdown` which is cross-platform.

**Tree-scoped kill (experiment).** A more precise fix — killing only the processes spawned by the runner itself via `Process.Kill(entireProcessTree: true)` — is tracked as a roadmap experiment (GitHub issue, labels `area:build-system`, `type:experiment`). That refactor would eliminate the side-effect warning above but requires giving up Cake's `IProcess` abstraction for the smoke runner's `dotnet` invocations; deferred until the side-effect actually bites someone's parallel workflow.

## Failure Triage

When a checkpoint fails, classify before reacting:

1. **Environment issue** — dotnet not in PATH, missing tools, stale build output. Fix the environment, re-run.
2. **Stale repo** — platforms not on the same commit. `git pull` and re-run.
3. **Code regression** — same commit, clean environment, still fails. This is what the smoke matrix is designed to catch. Report with platform, checkpoint, exact error.

Do not conflate environment issues with code regressions. The majority of cross-platform "failures" in practice are category 1 or 2.

## Extending This Matrix

When a new Cake task or validation step lands:

1. **Add a row** to the Active Checkpoints table (move from Planned if it was already listed, or add fresh).
2. **Add a command reference section** (letter-keyed, e.g., `### H. GenerateMatrixTask`) with per-platform commands and "what to look for".
3. **Add output artifacts** to the Output Artifacts table if the checkpoint produces verifiable files.
4. **Add known gotchas** if the checkpoint has platform-specific quirks.
5. **Update the "Last validated" header** with the date and result after running the expanded matrix.

The matrix should always reflect the current Cake task surface. If a task is removed or renamed, remove or update its checkpoint too.

## Relationship to Local Dev (ADR-001)

Under [ADR-001](../decisions/2026-04-18-versioning-d3seg.md), the local dev flow and the smoke matrix converge on the same consumer contract: every consumer-facing csproj (smoke, examples, sandbox) restores Janset packages from a **local folder feed** via `PackageReference`. The only variation is how the feed is populated:

| Flow | Feed source | Consumer contract | Driver |
| --- | --- | --- | --- |
| `SetupLocalDev --source=local` (Phase 2a, lands V5) | Repo pack → `artifacts/packages` | PackageReference + exact-pinned smoke override | `build/msbuild/Janset.Smoke.local.props` generated by task |
| `SetupLocalDev --source=remote` (Phase 2b) | Internal feed download → local cache | Same | Same override file, written from remote-fetched versions |
| This smoke matrix (manual A–K walkthrough) | Whatever the operator staged | Same | CLI `-p:` flags or local.props |

Once `SetupLocalDev` ships, IDE-opened smoke csprojs restore without Cake in the loop (the `Janset.Smoke.local.props` conditional import picks up the per-developer feed + version values). See [local-development.md](local-development.md) for the full Quick Start flow.

## Relationship to CI

This local smoke matrix validates the **same command surface** that CI workflows use. The key differences:

- CI sets up its own environment (PATH, SDK, vcpkg bootstrap) per job
- CI uses matrix jobs across RIDs; local smoke runs one RID per platform
- CI uploads artifacts; local smoke checks artifacts exist locally
- CI will gain PreFlightCheck as a gate (Stream C); local smoke already runs it manually

If local smoke passes but CI fails, the issue is almost certainly in CI environment setup, not in the build host code.

## Authoring New Smoke / Example Consumer Projects

Smoke, example, and sandbox consumer projects that exercise the local-feed `.nupkg` surface share a single MSBuild foundation. New consumers inherit feeds, version-property conventions, and guard logic by dropping into the existing hierarchy — no ad-hoc `LocalPackageFeed` / version-property plumbing per project.

### Layout

```text
build/msbuild/
  Janset.Smoke.props      ← Shared properties (LocalPackageFeed, per-family version properties, SDL3 placeholders, smoke analyzer posture)
  Janset.Smoke.targets    ← Shared guard targets (JNSMK001: LocalPackageFeed required; JNSMK002+: per-family version input required when referenced)

tests/smoke-tests/
  Directory.Build.props   ← Imports root Directory.Build.props + Janset.Smoke.props
  Directory.Build.targets ← Imports root Directory.Build.targets + Janset.Smoke.targets
  package-smoke/
    PackageConsumer.Smoke/   ← TUnit runtime smoke (executable TFMs)
    Compile.NetStandard/     ← netstandard2.0 compile-only sanity
```

The same pattern can extend to `examples/Directory.Build.props` and `sandbox/Directory.Build.props` once those trees grow consumer projects.

### Authoring Contract

Per consumer csproj:

- Inherit the hierarchy by living under `tests/smoke-tests/**` (or a sibling tree that imports the same shared props/targets).
- Declare the SDL generation + family roles you want to validate via **exactly one** of two property lists:

    ```xml
    <PropertyGroup>
      <JansetSmokeSdl2Families>Core;Image;Mixer;Ttf;Gfx</JansetSmokeSdl2Families>
    </PropertyGroup>
    ```

    Or, once SDL3 ships:

    ```xml
    <PropertyGroup>
      <JansetSmokeSdl3Families>Core;Image</JansetSmokeSdl3Families>
    </PropertyGroup>
    ```

    `Janset.Smoke.targets` translates the list into exact-pinned `<PackageReference>` entries (bracket notation `[x]` = exact-version NuGet constraint). The smoke harness validates the exact package set the orchestrator produced, not any compatible sibling in the feed.

- Do **not** redeclare `LocalPackageFeed`, `RestoreAdditionalProjectSources`, version defaults, individual per-family `<PackageReference>` entries, or guard targets — all of these live in the shared files.

- Non-Janset references (TUnit, PolySharp, etc.) stay in the csproj as normal `<PackageReference>` items; the auto-generation only covers `Janset.SDL<Major>.<Role>` packages.

Naming convention (authoritative in `FamilyIdentifierConventions`):

- `sdl<major>-<role>` family identifier → `Janset.SDL<Major>.<Role>` managed package + `Janset.SDL<Major>.<Role>.Native` native package + `JansetSdl<Major><Role>PackageVersion` version property.
- The family-list role tokens are the same `<Role>` values (case-sensitive): `Core`, `Image`, `Mixer`, `Ttf`, `Gfx`, `Net`.

### Authoring Guards (MSBuild, fire before Restore / Build)

| Code | Fires when | Purpose |
| --- | --- | --- |
| `JNSMK001` | `$(LocalPackageFeed)` is empty | Refuses direct `dotnet build/test` against a smoke csproj — feed must come from Cake. |
| `JNSMK002` – `JNSMK007` | An SDL2 family is referenced but its `-p:JansetSdl2<Role>PackageVersion=…` is still the sentinel | Orchestrator forgot to inject the version for a family the csproj declared. |
| `JNSMK008` | Both `JansetSmokeSdl2Families` and `JansetSmokeSdl3Families` are non-empty | SDL2 and SDL3 are mutually exclusive in one smoke csproj — pulls parallel native payloads, always an authoring mistake. |
| `JNSMK009` | Neither `JansetSmokeSdl2Families` nor `JansetSmokeSdl3Families` is set | A smoke csproj without an SDL generation validates nothing; fire loud so silent no-op smoke is impossible. |
| `JNSMK101` – `JNSMK106` | An SDL3 family is referenced but its `-p:JansetSdl3<Role>PackageVersion=…` is still the sentinel | SDL3 mirror of 002-007; ready for when SDL3 packages ship. |

### SDL3 Drop-in Readiness

`Janset.Smoke.props` already declares `JansetSdl3CorePackageVersion`, `JansetSdl3ImagePackageVersion`, etc. (all defaulted to the sentinel). When SDL3 ships, authoring an SDL3 smoke consumer is a one-line property change: set `<JansetSmokeSdl3Families>Core;Image</JansetSmokeSdl3Families>` (instead of the SDL2 list). The `JNSMK101`–`JNSMK106` guards activate automatically, and the ItemGroup expansion in `Janset.Smoke.targets` emits the matching `Janset.SDL3.*` `<PackageReference>` entries with exact-pin bracket notation.

### Invocation Contract (via Cake)

Consumer csprojs require orchestrator-supplied inputs — direct `dotnet build`/`dotnet test` invocations deliberately fail at `JNSMK001` so contributors can't accidentally bypass the guard. The runner (`PackageConsumerSmokeRunner`) derives its smoke scope from `manifest.json` package_families (filtered by `managed_project` + `native_project` non-null) and injects `-p:LocalPackageFeed=<absolute path>` plus one `-p:JansetSdl<Major><Role>PackageVersion=<semver>` per concrete family into every `dotnet build/test` call.

That means:

- Adding a new concrete family to `manifest.json` (non-null `managed_project` + `native_project`) automatically expands the smoke scope — no runner edit.
- Graduating a placeholder family (e.g., `sdl2-net` today) requires only filling in the two `*_project` fields; the smoke runner picks it up and the shared props already declare the version property.
- Adding a new smoke consumer csproj under `tests/smoke-tests/**` only requires declaring the desired `PackageReference`s; feed and versions are inherited.

## Consumer Invocation Contract (Checkpoint K)

`PackageConsumerSmoke` invokes the consumer project with an explicit `-r <rid>` on `restore`, `build`, and `run`. That is a deliberate choice and the contract it validates is narrower than "what an arbitrary external consumer sees."

What `-r <rid>` exercises today:

- Runtime-specific restore resolves the `.Native` package's `runtimes/{rid}/native/` subtree
- SDK-level file copy places the native binaries directly under `bin/Release/net9.0/<rid>/`
- `dotnet run --no-build --no-restore -r <rid>` executes the pre-built binary with runtime assets already present in the output folder
- P/Invoke loader succeeds because the DLL is sitting next to the managed assembly

What `-r <rid>` does NOT exercise:

- The default framework-dependent consumer path (no `<RuntimeIdentifier>` set), where runtime asset resolution goes through MSBuild's `runtimetargets` evaluation and the host runtime's native library resolver (not the build-time file copy)
- Multi-RID publish scenarios (`dotnet publish -r linux-x64 --self-contained`)
- Consumer projects that set `<RuntimeIdentifiers>` (plural) for multi-target output
- Any form of NuGet client version drift (restore works with modern SDK; older NuGet clients might not honour the runtime subtree layout identically)

**Consequence for D-local:** checkpoint K today proves "runtime assets LAND in bin/ via build-time file copy on win-x64." It does NOT prove "runtime assets LOAD via the default framework-dependent consumer flow across RIDs." Both are legitimate Package Validation Mode truths per [`research/execution-model-strategy-2026-04-13.md §7.2`](../research/execution-model-strategy-2026-04-13.md) but they exercise different subsystems.

**When to revisit:** before K is promoted to active on all three platforms, decide whether the smoke should also run a second invocation **without** `-r <rid>` to cover the framework-dependent resolver path. The decision lives in [`phases/phase-2-adaptation-plan.md`](../phases/phase-2-adaptation-plan.md) Pending Decisions (PD-10).

## PA-2 Per-Triplet Witness Invocations

PA-2 moved four previously-stock runtime rows onto hybrid overlay triplets (2026-04-18). The mechanism landed, but no behavioral evidence exists yet on the new triplets. Before Stream C consumes them as peers of the validated x64 row, we need at least one end-to-end PostFlight run per new row on its native runner.

Each invocation packs the concrete five-family smoke scope, exercises harvest → consolidate → pack → consumer-smoke, and records pass/fail against this playbook. Record the result in the "Last validated" header at the top of this document as each triplet passes; any failure triage into (a) upstream vcpkg port issue, (b) overlay-triplet tuning needed, or (c) vcpkg feature-flag degradation — file a `docs/research/` note before re-attempting.

Trigger each run via workflow-dispatch on the matching GitHub runner:

| # | RID | Triplet | Runner (workflow dispatch) | Notes |
| --- | --- | --- | --- | --- |
| 1 | `win-x86` | `x86-windows-hybrid` | `windows-latest` (cross-arch from x64 host) | 32-bit MSVC calling conventions; first hybrid-static zlib/libpng/libjpeg-turbo bake on x86. |
| 2 | `win-arm64` | `arm64-windows-hybrid` | `windows-latest` (cross-arch from x64 host) | Watch for vcpkg port gaps on windows-arm64 — particularly `timidity` and `wavpack` — that can silently degrade the SDL2_mixer codec set. |
| 3 | `linux-arm64` | `arm64-linux-hybrid` | `ubuntu-24.04-arm` (native arm64) | Community triplet base + `ubuntu:24.04` container; newer glibc vs x64's `ubuntu:20.04`. Watch for `-fvisibility=hidden` interactions with freetype / libwebp symbol versioning. |
| 4 | `osx-arm64` | `arm64-osx-hybrid` | `macos-latest` (Apple Silicon) | First arm64-osx run through the hybrid bake. Watch for MachO layout / `VCPKG_OSX_ARCHITECTURES=arm64` / dyld cache surprises. |

Per-RID command (adapt the runner via workflow input; body identical):

```bash
# Harvest + ConsolidateHarvest + Package + PackageConsumerSmoke for the full
# concrete family scope on the target runner. Replace <rid> with the RID from
# the table above. --family-version can be any unused suffix (e.g., 'pa2-witness.1').
dotnet run --project build/_build/Build.csproj -- \
  --target PostFlight \
  --family sdl2-core --family sdl2-image \
  --family sdl2-mixer --family sdl2-ttf --family sdl2-gfx \
  --family-version 2.32.0-pa2-witness.<rid>.1 \
  --rid <rid>
```

**Acceptance per witness:**

- PreFlight: 6/6 versions, 7/7 strategy coherence, G49 core-identity, csproj contract all green.
- Harvest: each selected library reports `primary=1, runtime=0` and emits `rid-status/<rid>.json` with `success=true`.
- Consolidate: `harvest-manifest.json` + `harvest-summary.json` + `licenses/_consolidated/` produced.
- Package: `Janset.SDL2.<Role>.nupkg`, `.snupkg`, and `.Native.<version>.nupkg` produced per family with post-pack validator (G21–G23, G25–G27, G46, G47, G48) = 0 violations.
- PackageConsumerSmoke: netstandard2.0 compile-sanity pass + per-TFM TUnit pass for executable TFMs (net9.0 / net8.0 on every runner; net462 on Windows always, on macOS only when `mono` is on `$PATH` — else auto-skipped; always skipped on Linux). See [Consumer Invocation Contract](#consumer-invocation-contract-checkpoint-k) for the per-TFM matrix rationale.

Any failure that isn't an obvious environment issue triggers a research note at `docs/research/pa2-witness-<rid>-<date>.md` before re-attempting. Do not paper over behavioral failures with workarounds in the overlay files — file the research note first, diagnose root cause, then either fix or retreat.
