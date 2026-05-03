# Playbook: Cross-Platform Smoke Validation

> How to verify that the Cake build host, harvest pipeline, native libraries, and package-consumer path work correctly across the supported local hosts after a refactor or significant change.

**Last validated:** 2026-04-29 remote-feed host witness at `4afdd1d` against CI-published wave `release.yml` run 24962876812; prior all-7-RID CI confirmation remains run 24938451364 on master `8ec85c5`.
**Result (2026-04-29):** PD-5 exit criterion #4 is fully closed; the remote-feed bootstrap (then `SetupLocalDev --source=remote`, now `tools setup --source=remote-github` after the Phase Y retirement of the Cake-side `SetupLocalDev` target) works on all 3 maintainer host platforms against the same GitHub Packages wave:

- **CI (all 7 RIDs)**: `release.yml` run 24938451364 green on master `8ec85c5` — Pack ✓ + ConsumerSmoke ✓ across `win-{x64,x86,arm64}` / `linux-{x64,arm64}` / `osx-{x64,arm64}`. Build host strict-mode lock-file gate held; ConsumerSmoke per-TFM TUnit (`net9.0` + `net8.0`) green on every RID; `net462` runtime green on Windows runners, correctly skipped on Linux + macOS per the platform Mono / TUnit-incompat gates in `PackageConsumerSmokeRunner.ShouldSkipTfm`.
- **Remote-feed host witness (Windows + WSL Linux + macOS Intel)**: all 3 platforms pulled the same CI-published wave from run 24962876812 (`sdl2-core=2.32.0-ci.24962876812.1`, `sdl2-gfx=1.0.0-ci.24962876812.1`, `sdl2-image=2.8.0-ci.24962876812.1`, `sdl2-mixer=2.8.0-ci.24962876812.1`, `sdl2-ttf=2.24.0-ci.24962876812.1`). Windows `win-x64`: 3/3 PASS in 78.6s, ConsumerSmoke 35/35. WSL Linux `linux-x64`: 3/3 PASS in 71.0s, ConsumerSmoke 24/24 with `net462` correctly skipped. macOS Intel `osx-x64`: 3/3 PASS in 119.5s, ConsumerSmoke 24/24 with `net462` skipped because Mono is not installed.
- **Local A-K walkthrough (Windows + WSL Linux)**: every checkpoint A→K green on master `d190b5b`. Windows `win-x64`: build-host 460/460, native-smoke 29/29, harvest 1-primary/0-runtime per library, `Inspect-HarvestedDependencies` confirmed no leaked codec/font deps, the local-dev feed bootstrap (then `SetupLocalDev --source=local`, now `tools setup --source=local`) produced 15 nupkgs in 41.8s, `PackageConsumerSmoke` 12 + 12 + 11 = 35/35 PASS across `net9.0`/`net8.0`/`net462`. WSL Linux: same shape, MIDI decoder fonctional via builder image's `freepats` package, `PackageConsumerSmoke` 12 + 12 = 24/24 PASS (`net462` auto-skip per Mono+TUnit incompat).
- **Builder image**: `ghcr.io/janset2d/sdl2-bindings-linux-builder:focal-latest` (multi-arch amd64+arm64) consumed by `release.yml` Linux jobs; bakes the full apt preamble (autotools + SDL2 backends + `freepats` MIDI patches + `libicu*` + autoconf 2.72) + GCC 11 PPA + Kitware CMake. Builder workflow `build-linux-container.yml` runs monthly (cron 1st of each month 03:00 UTC) absorbing upstream security-patch drift.

**Cake-first invocation contract (Slice DA, 2026-04-21).** Every checkpoint below invokes a Cake target directly. Pre-Slice-D raw shell blocks (`rm -rf artifacts/…`, `tar -xzf native.tar.gz`, `cmake --preset <rid>`, manual per-TFM `dotnet test`) have dedicated Cake targets after Slice D:

- Artifact wipe → `--target CleanArtifacts`
- Tarball extraction + ldd/otool/dumpbin → `--target Inspect-HarvestedDependencies --rid <rid>`
- CMake configure + build + native-smoke invocation → `--target NativeSmoke --rid <rid>`
- Per-TFM consumer smoke loop → `--target PackageConsumerSmoke --rid <rid>`
- Full local-dev feed bootstrap (pack + local.props) → `tools setup --source=local` (repo-root file-based app — see [local-development.md](local-development.md))

The only allowed non-Cake invocations in the big-smoke flow are checkpoints A and B (build-host unit tests + Cake-host csproj build). Cake cannot compile or test itself — that bootstrap exception is explicit. See `unix-smoke-runbook.md` for the concrete per-platform witness script.

**Scope note:** all 7 manifest runtime rows (`win-{x64,x86,arm64}` / `linux-{x64,arm64}` / `osx-{x64,arm64}`) green via CI matrix re-entry. The per-RID matrix re-entry shape from ADR-003 §3.4 + §3.5 is live in `release.yml` (`generate-matrix` → `harvest` matrix → `pack` → `consumer-smoke` matrix re-entry consuming the same `manifest.runtimes[]` source). **PA-2 behavioral validation closed 2026-04-26** via `release.yml` run 24938451364 on master `8ec85c5` — the four newly-hybridized rows (`win-arm64`, `win-x86`, `linux-arm64`, `osx-arm64`) cleared the full pipeline on their native runners with zero-failure per-TFM TUnit (see [§PA-2 Per-Triplet Witness Invocations](#pa-2-per-triplet-witness-invocations) for the closure record). The local walkthrough below exercises a **single host RID per platform** (Windows + WSL Linux + macOS); CI dispatch is the full-matrix surface. PD-10 (consumer smoke `-r <rid>` vs default resolver path) deferred to Phase 2b.

## When to Run This

- After any build-host refactor (Cake tasks, DI wiring, service boundaries)
- After manifest.json schema changes
- After vcpkg baseline or overlay triplet changes
- After CI workflow command surface changes
- Before declaring a multi-session refactor "done"
- When a new stream (C, D, F) lands changes that touch cross-platform behavior

## Recommended Big-Smoke Rules

- Treat `artifacts/` as a **single-run disposable workspace** for this playbook. If you want a trustworthy answer to "everything still works", start from an empty artifact tree instead of reusing old `harvest_output/`, `packages/`, or consumer-smoke caches.
- Do **not** define success as "every Cake target was invoked once." Some targets are lifecycle gates (`PreFlightCheck`, `EnsureVcpkgDependencies`, `Harvest`, `NativeSmoke`, `ConsolidateHarvest`, `Package`, `PackageConsumerSmoke`), some are diagnostics (`Inspect-HarvestedDependencies`, `Dumpbin-Dependents`, `Ldd-Dependents`, `Otool-Analyze`), some are utility (`CleanArtifacts`, `CompileSolution`, `GenerateMatrix`), and some are local-only safeguards (`Coverage-Check`). The big smoke should exercise the lifecycle gates on every platform and the diagnostics where they add evidence.
- `tools setup --source=local` is a useful **developer-convenience umbrella** that orchestrates the full local-dev feed bootstrap (CleanArtifacts → ResolveVersions → PreFlight → EnsureVcpkgDependencies → Harvest → ConsolidateHarvest → Package + writes `Janset.Local.props`), but the explicit `PreFlightCheck` gate in this playbook is still kept as a standalone fail-fast step.
- Use a **fresh version suffix per run** and never mix package families from multiple smoke attempts in `artifacts/packages/`. Even with orchestrator-supplied version properties, a dirty local feed is how you accidentally end up debugging ghosts.
- Record results per platform as a bundle: command log, harvested dependency inspection, native-smoke output, and final package-consumer result. If one platform goes red, you want evidence, not vibes.

## Smoke Matrix

This matrix is a living validation surface that grows as new Cake tasks and streams land. Each checkpoint has a stream origin so you know when it was introduced and whether it applies to the current codebase.

### Active Checkpoints

These are validated today and should pass on all 3 platforms.

| # | Checkpoint | Stream | What It Proves | Expected Output |
| --- | --- | --- | --- | --- |
| A | Build-host unit tests (**bootstrap exception**) | Baseline | Refactored code logic is correct | 460 passed, 0 failed on master `d190b5b` (Slice E follow-up pass P7 walkthrough, 2026-04-25) |
| B | Cake restore + build (Release) (**bootstrap exception**) | Baseline | Build host compiles clean on all platforms | 0 warnings, 0 errors (usually implied by A — tests build the same assemblies) |
| C | Cake `--tree` | Baseline | Task dependency graph is flat (Slice B2) | every stage task standalone — `CleanArtifacts`, `CompileSolution`, `ConsolidateHarvest`, `EnsureVcpkgDependencies`, `GenerateMatrix`, `Harvest`, `Info`, `Inspect-HarvestedDependencies`, `NativeSmoke`, `Package`, `PackageConsumerSmoke`, `PreFlightCheck`, `PublishPublic`, `PublishStaging`, `ResolveVersions` + diagnostic targets (`Coverage-Check`, `Dumpbin-Dependents`, `Ldd-Dependents`, `Otool-Analyze`). No `PostFlight`, no `SetupLocalDev` (Phase Y moved dev orchestration to repo-root `tools.cs`). |
| D | PreFlightCheck | Baseline + A-risky + S1 | manifest.json ↔ vcpkg.json consistency + strategy coherence + post-S1 csproj pack contract (G4/G6/G7/G17/G18) | 6/6 versions, 7/7 strategies, 6/6 families × 10/10 csprojs all green |
| D1 | EnsureVcpkgDependencies | Baseline | vcpkg bootstrap scripts + manifest install work for the current triplet, with overlay triplets/ports applied | bootstrap only when needed, install exits 0, triplet/overlay paths logged |
| E | Harvest | Baseline | Binary closure walk + deployment works per-platform; default scope is the full manifest library set | per-library `1 primary, 0 runtime, DirectCopy/Archive` green, rid-status JSON generated |
| F | ConsolidateHarvest | Baseline | Per-RID merge produces manifest + summary | `harvest-manifest.json` + `harvest-summary.json` per library |
| F1 | Inspect-HarvestedDependencies | **D** | Platform-aware artifact-side spot-check: Unix RIDs extract `native.tar.gz` via the repo-local tar wrapper into `artifacts/temp/inspect/<rid>/<lib>/` preserving SONAME symlinks, Windows RIDs read `runtimes/<rid>/native/` directly, then invokes the platform scanner (Dumpbin/Ldd/Otool) on each library's primary binary | per-library `Primary binary resolved` line + dep scanner output; no stray third-party codec DLLs / `.so` / `.dylib` entries beyond the SDL core shared library + OS/system libs |
| G | NativeSmoke (C/C++ harness via Cake) | **D** | Hybrid-built natives load and initialize at runtime; Cake target wraps CMake configure/build + native-smoke executable invocation via Cake.CMake + `NativeSmokeRunnerTool` | `29 passed, 0 failed, Result: ALL PASS` on the current expanded harness |
| J | Package (family-aware pack + post-pack validator) | D-local (post-S1), flag-updated B1 | Per-family pack produces valid `.nupkg` per library (managed + native + .snupkg) + post-pack validator suite (G21–G23, G25–G27, G47, G48) passes on every produced package | 3 `.nupkg` files per family at the `--explicit-version` mapping; post-pack validator 0 violations |
| K | PackageConsumerSmoke | D-local (post-S1, expanded on Windows) | `PackageReference` restore from local feed + consumer-side `buildTransitive` target fires + runtime smoke succeeds for the concrete package-consumer set (`sdl2-core`, `sdl2-image`, `sdl2-mixer`, `sdl2-ttf`, `sdl2-gfx`) + Unix symlink chain preserved | per-TFM TUnit pass; current Windows host expectation is 12 passing tests on `net10.0`/`net9.0`/`net8.0` and 11 passing tests on `net462`; netstandard2.0 compile-sanity passes |
| L | `tools setup --source=remote-github` | F | Remote artifact-source feed prep populates the local cache + writes `Janset.Local.props` + `versions.json` correctly; consumer smoke green against pulled feed | **Closed 2026-04-29** (then under the now-retired `SetupLocalDev --source=remote` Cake target) against CI run 24962876812: Windows `win-x64` 3/3 PASS, ConsumerSmoke 35/35; WSL Linux `linux-x64` 3/3 PASS, ConsumerSmoke 24/24 (`net462` skipped); macOS Intel `osx-x64` 3/3 PASS, ConsumerSmoke 24/24 (`net462` skipped because Mono absent). Phase Y (2026-05-03) moved this surface to `tools setup --source=remote-github`. |

**Scope caveat for J and K (2026-04-17):** The current code path for `PackageConsumerSmoke` requires the concrete five-family smoke scope (`sdl2-core`, `sdl2-image`, `sdl2-mixer`, `sdl2-ttf`, `sdl2-gfx`). That widened scope is re-validated on `win-x64`. Linux and macOS still retain the older proof-slice evidence for `sdl2-core` + `sdl2-image`; rerunning the expanded scope there is still Phase 2b work, as is end-to-end validation for the newly hybridized rows (`win-arm64`, `win-x86`, `linux-arm64`, `osx-arm64`).

### Planned Checkpoints

These will be added as their parent streams land. Add the command reference and "what to look for" details when promoting from planned to active.

| # | Checkpoint | Stream | What It Will Prove | Promotion Criteria |
| --- | --- | --- | --- | --- |
| I | PreFlightCheck as a CI gate | B1 (**landed**) | Version resolution, package-family integrity, unit tests as gate — present in `release.yml` as a first-class job since Slice B1 | Already active in CI; covered locally by checkpoint D |
| M | J/K extended to remaining 4 hybrid-static RIDs | 2b | PackageTask + PackageConsumerSmoke green for `win-arm64`, `win-x86`, `linux-arm64`, and `osx-arm64` now that the overlay triplets (`x86-windows-hybrid`, `arm64-windows-hybrid`, `arm64-linux-hybrid`, `arm64-osx-hybrid`) exist | PA-1 decision landed + PA-2 overlay triplets merged + at least one newly-covered RID harvested and consumer-smoked on its native runner |
| N | CleanArtifacts | **D** (landed) | Every ephemeral artifact subtree wipes cleanly without side-effects on `vcpkg_installed/` | `dotnet run --project build/_build/Build.csproj -- --target CleanArtifacts` exits 0 with no prior run's files remaining under the eight configured roots |
| O | GenerateMatrix | **D** (landed) | `artifacts/matrix/runtimes.json` in GitHub-Actions `include[]` shape, symmetric 7-RID from `manifest.runtimes[]` | `--target GenerateMatrix` emits the JSON; the `include` array cardinality equals `manifest.runtimes[]` length (currently 7) |
| P | CompileSolution | **D** (landed) | Thin `DotNetBuild` wrapper on `Janset.SDL2.sln` with the active BuildContext configuration | `dotnet run --project build/_build/Build.csproj -- --target CompileSolution` completes with 0 errors (implied by A/B on most flows; kept for explicit solution-build witness) |

**Exit criteria:** All **active** checkpoints green on all 3 platforms. Any failure must be classified as environment issue vs code regression before proceeding. When promoting a planned checkpoint to active, update this table and add its command reference below.

## Platform Access

| Platform | Access Method | Repo Path | Triplet |
| --- | --- | --- | --- |
| Windows | Local (current machine) | `E:\repos\my-projects\janset2d\sdl2-cs-bindings` | `x64-windows-hybrid` |
| Linux | WSL from Windows | `/home/deniz/repos/sdl2-cs-bindings` | `x64-linux-hybrid` |
| macOS Intel | SSH: `Armut@192.168.50.178` | `/Users/armut/repos/sdl2-cs-bindings` | `x64-osx-hybrid` |

Keep all 3 repos on the same commit before running the matrix. Verify with `git log --oneline -1` on each.

## Per-Platform Environment Setup

### Windows

No special setup. Since Slice CA (2026-04-21) the Cake host self-sources the MSVC environment via `IMsvcDevEnvironment` (VSWhere → `vcvarsall.bat <host-arch>` → env-delta merge) whenever `NativeSmoke` runs, so **plain PowerShell is enough** — Developer PowerShell for VS 2022 is no longer required. Prereq: Visual Studio Build Tools 2022+ with the *Desktop development with C++* workload installed (the `Microsoft.VisualStudio.Component.VC.Tools.x86.x64` component VSWhere keys on).

### WSL / Linux

dotnet is installed at `~/.dotnet/` but is **not in the default PATH**. Both `PATH` and `DOTNET_ROOT` must be set:

```bash
export PATH="$HOME/.dotnet:$PATH"
export DOTNET_ROOT="$HOME/.dotnet"
```

**Why `DOTNET_ROOT`?** TUnit uses Microsoft Testing Platform which produces a native apphost. The apphost resolves .NET runtime via `DOTNET_ROOT`, not `PATH`. Without it, tests build but fail at execution with `Failed to resolve libhostfxr.so`.

**Use zsh on WSL.** The repo's WSL Ubuntu setup runs zsh as the default login shell — persist these exports in `~/.zshrc`, not `~/.bashrc`. For Windows-side harnesses driving WSL non-interactively, use `wsl zsh -c "setopt no_nomatch; source ~/.zshrc; …"` rather than `wsl zsh -lic '…'`; the `setopt` guard is required because zsh's default `nomatch` chokes on the unquoted `(x86)` token in the WSL-inherited Windows PATH (`Program Files (x86)/...`) the moment a login shell expands it. bash silently passes the literal, which is why the bash form has historically worked but isn't the canonical invocation here. Cake child processes are unaffected — they don't glob-expand `$PATH`.

**WSL `dotnet pack` PATH gotcha:** WSL's `appendWindowsPath=true` default prepends Windows PATH entries, so `/mnt/c/Program Files/dotnet` ends up ahead of `/home/<user>/.dotnet` after the prepend above. The Cake host itself starts on the Linux dotnet (full-path or absolute resolution), but `dotnet pack` invoked internally by `IDotNetPackInvoker` resolves through naked PATH lookup and picks Windows dotnet — `MSBuild.dll` then fails with `MSB1001: Unknown switch` against Linux paths (`/home/<user>/...`). D-G checkpoints (PreFlight, Harvest, NativeSmoke, …) are unaffected because they invoke vcpkg / scanners / cmake through Cake's `IPathService` abstractions rather than naked PATH. **For checkpoints J (Package) and K (PackageConsumerSmoke), set a Linux-only PATH explicitly:**

```bash
export PATH="$HOME/.dotnet:/usr/local/sbin:/usr/local/bin:/usr/sbin:/usr/bin:/sbin:/bin"
export DOTNET_ROOT="$HOME/.dotnet"
```

This drops `/mnt/c/...` entries entirely; WSL Ubuntu has Linux-native git, cmake, gcc, dotnet (`~/.dotnet/`), so the resulting environment is fully self-contained on the Linux side. Without this override, `tools setup --source=local` fails at the first `dotnet pack '...Native.csproj'` invocation.

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
- For real local validation of more than one RID, use a staged loop driven by Cake targets per RID: `CleanArtifacts` → explicit pack chain for `<rid-A>` (`PreFlightCheck` → `EnsureVcpkgDependencies --rid <rid-A>` → `Harvest --rid <rid-A>` → `ConsolidateHarvest` → `Package --explicit-version ...`) → `Inspect-HarvestedDependencies --rid <rid-A>` → `CleanArtifacts` → repeat for `<rid-B>`. The `tools setup --source=local` umbrella is host-RID only; cross-RID validation is an explicit-target chain. A fresh `CleanArtifacts` between RIDs prevents cross-RID state leakage.
- CI is unaffected because each RID job installs exactly one triplet in isolation.

### Clean-Slate Artifact Rule

For a real "is everything still OK?" answer, wipe the generated artifacts before each platform run. Reusing `artifacts/packages/` or stale `harvest-manifest.json` files is how you get a fake green.

**Primary path (Cake-first)** — one command, all platforms:

```bash
# All platforms (bash / pwsh alike accept this shape):
dotnet run --project build/_build/Build.csproj -c Release -- --target CleanArtifacts
```

`CleanArtifacts` wipes:

- `artifacts/harvest_output/`
- `artifacts/packages/`
- `artifacts/package-consumer-smoke/`
- `artifacts/test-results/smoke/`
- `artifacts/harvest-staging/`
- `artifacts/temp/inspect/`
- `artifacts/matrix/`
- `tests/smoke-tests/native-smoke/build/` (all presets)

`vcpkg_installed/` is **intentionally preserved** — cold-rebuild cost is too high to discard on every smoke run.

**Raw shell fallback (debug-only)** — use this when CleanArtifacts is itself the suspected regression, or when you want to wipe only a subset:

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
rm -rf artifacts/harvest_output artifacts/packages artifacts/package-consumer-smoke
rm -rf artifacts/test-results/smoke artifacts/harvest-staging artifacts/temp/inspect artifacts/matrix
rm -rf tests/smoke-tests/native-smoke/build
```

If you need the previous run for comparison, archive it somewhere outside `artifacts/` first.

### A. Build-Host Unit Tests (pre-Cake bootstrap exception)

The Cake host cannot run its own unit tests from inside Cake — that's a chicken-and-egg loop. `dotnet test` against `build/_build.Tests/Build.Tests.csproj` is a bootstrap-only invocation. Every other smoke step below invokes a Cake target.

```bash
dotnet test --project build/_build.Tests/Build.Tests.csproj --no-restore --framework net10.0
```

### B. Cake Restore + Build (Release) (pre-Cake bootstrap exception)

Same rationale as A — Cake cannot compile its own csproj. After B, every subsequent step invokes a Cake target (either via `dotnet run --project build/_build/Build.csproj -- --target <X>` or the compiled `./build/_build/bin/Release/net10.0/Build[.exe]` entrypoint).

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
./build/_build/bin/Release/net10.0/Build.exe --target Harvest \
  --library SDL2 --library SDL2_image --library SDL2_mixer \
  --library SDL2_ttf --library SDL2_gfx --library SDL2_net \
  --rid win-x64

# Linux:
./build/_build/bin/Release/net10.0/Build --target Harvest \
  --library SDL2 --library SDL2_image --library SDL2_mixer \
  --library SDL2_ttf --library SDL2_gfx --library SDL2_net \
  --rid linux-x64 --repo-root "$(pwd)"

# macOS:
./build/_build/bin/Release/net10.0/Build --target Harvest \
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
./build/_build/bin/Release/net10.0/Build.exe --target ConsolidateHarvest --rid win-x64

# Linux:
./build/_build/bin/Release/net10.0/Build --target ConsolidateHarvest --repo-root "$(pwd)"

# macOS:
./build/_build/bin/Release/net10.0/Build --target ConsolidateHarvest --repo-root "$(pwd)"
```

**What to look for:** `harvest-manifest.json` + `harvest-summary.json` generated under each library in `artifacts/harvest_output/`.

### F1. Harvested Artifact Dependency Inspection

`Harvest` already consumes dumpbin / ldd / otool through the runtime scanners, but the big smoke should still do an artifact-side spot check on the harvested payload — the easiest way to prove that the thing we are about to pack is actually the thing we think we built. Slice D added a dedicated Cake target that runs the full scan end-to-end per RID.

**Primary path (Cake-first):**

```bash
# Windows / Linux / macOS — platform is resolved from the --rid value.
dotnet run --project build/_build/Build.csproj -c Release -- \
  --target Inspect-HarvestedDependencies --rid <rid> --repo-root "$(pwd)"
```

`Inspect-HarvestedDependencies` per-library:

- **Unix RIDs (`linux-*`, `osx-*`)**: extracts `artifacts/harvest_output/<lib>/runtimes/<rid>/native/native.tar.gz` via the repo-local `Infrastructure/Tools/Tar/` wrapper (preserves SONAME symlinks / permissions / xattrs natively — matches what MSBuild does consumer-side) into `artifacts/temp/inspect/<rid>/<lib>/`, then invokes the platform scanner alias (`Ldd-Dependents` / `Otool-Analyze`) on the resolved primary binary.
- **Windows RIDs (`win-*`)**: reads `artifacts/harvest_output/<lib>/runtimes/<rid>/native/` directly (no extraction needed — Windows ships primaries uncompressed), then invokes `Dumpbin-Dependents`.
- Primary binary is resolved via `LibraryManifest.PrimaryBinaries[<os>].Patterns` glob match (shortest-path tie-break). If the pattern does not match anything under the inspected directory, the task fails fast with the pattern list in the error message.

**Raw shell fallback (advanced-debug only)** — use this if `Inspect-HarvestedDependencies` is itself the suspected regression, or when you need to run a scanner against a file the runner does not resolve to:

```powershell
# Windows — inspect any DLL directly
dumpbin /dependents .\artifacts\harvest_output\SDL2_image\runtimes\win-x64\native\SDL2_image.dll

# Optional direct Cake alias wrapper
dotnet run --project .\build\_build\Build.csproj -- --target Dumpbin-Dependents --dll .\artifacts\harvest_output\SDL2_image\runtimes\win-x64\native\SDL2_image.dll
```

```bash
# Linux — manual tar + ldd against a specific library
rm -rf artifacts/temp/ldd/linux-x64 && mkdir -p artifacts/temp/ldd/linux-x64
tar -xzf artifacts/harvest_output/SDL2_image/runtimes/linux-x64/native/native.tar.gz -C artifacts/temp/ldd/linux-x64
ldd "$(find artifacts/temp/ldd/linux-x64 -name 'libSDL2_image*.so*' | head -n 1)"

# Optional direct Cake alias wrapper
dotnet run --project build/_build/Build.csproj -- --target Ldd-Dependents --dll "$(find artifacts/temp/ldd/linux-x64 -name 'libSDL2_image*.so*' | head -n 1)" --rid linux-x64 --repo-root "$(pwd)"
```

```bash
# macOS — manual tar + otool against a specific library
rm -rf artifacts/temp/otool/osx-x64 && mkdir -p artifacts/temp/otool/osx-x64
tar -xzf artifacts/harvest_output/SDL2_image/runtimes/osx-x64/native/native.tar.gz -C artifacts/temp/otool/osx-x64
otool -L "$(find artifacts/temp/otool/osx-x64 -name 'libSDL2_image*.dylib' | head -n 1)"

# Optional direct Cake alias wrapper
dotnet run --project build/_build/Build.csproj -- --target Otool-Analyze --dll "$(find artifacts/temp/otool/osx-x64 -name 'libSDL2_image*.dylib' | head -n 1)" --rid osx-x64 --repo-root "$(pwd)"
```

**What to look for:**

- Windows satellites depend on `SDL2.dll` plus CRT / system DLLs, not codec/transitive DLLs like `zlib1.dll`, `libpng16.dll`, `jpeg62.dll`, `libwebp.dll`, `freetype.dll`, `ogg.dll`, `vorbis*.dll`, etc.
- Linux/macOS satellites depend on the SDL2 core shared library and OS/system libraries/frameworks, not unexpected third-party shared objects that should have been baked in.
- The Inspect scan agrees with what Harvest reported (`1 primary, 0 runtime`). If Inspect says green but the raw shell fallback shows leaked deps, trust the raw tool and file a scanner-regression note (this gap is exactly why the raw fallback exists).
- For the direct `Dumpbin-Dependents` and `Ldd-Dependents` aliases, pass `--dll` explicitly. Their help text is looser than their actual behavior.

### G. NativeSmoke (C/C++ harness via Cake)

Slice D added a dedicated Cake target that wraps CMake configure + build + native-smoke executable invocation. See [tests/smoke-tests/native-smoke/README.md](../../tests/smoke-tests/native-smoke/README.md) for the underlying harness details.

**Primary path (Cake-first):**

```bash
# Windows / Linux / macOS — preset is resolved from --rid.
dotnet run --project build/_build/Build.csproj -c Release -- \
  --target NativeSmoke --rid <rid> --repo-root "$(pwd)"
```

The runner: preconditions the harvest output exists for the target RID → `cmake --preset <rid>` via Cake.CMake → `cmake --build --preset <rid>` → invokes the built `native-smoke[.exe]` via the `NativeSmokeRunnerTool` wrapper → captures stdout/stderr/exit-code into the Cake log. Non-zero exit fails the task.

**Raw shell fallback (advanced-debug only)** — use when NativeSmoke itself is the suspected regression:

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

**What to look for:** `Passed: 29, Failed: 0, Result: ALL PASS` on the expanded harness (covers PNG/JPEG/WebP/TIFF/AVIF loading, FLAC/MIDI/WavPack/Opus/OGG/MP3/MOD decoder discovery, SDL dummy-audio registration + `Mix_OpenAudio`, `TTF_Init`, `SDL2_gfx` drawing, `SDLNet_Init`). Older logs may still mention `13/13` — outdated.

**PA-2 RID coverage (closed 2026-04-26).** `tests/smoke-tests/native-smoke/CMakePresets.json` ships configure + build presets for every `manifest.runtimes[]` row (Release + Debug per RID = 14 buildPresets, since Slice E follow-up P8.4). `NativeSmoke --rid <rid>` therefore configures + builds + runs the C harness on every PA-2 row on its native runner — verified by `release.yml` run 24938451364 (Harvest + NativeSmoke green on all 7 RIDs). The runner has no hard-coded allow-list; the preset file is the source of truth (ADR-003 direction, 2026-04-21).

### J. Package (family-aware pack + post-pack validator)

`Package` packs one or more families at an operator-supplied version mapping (`--explicit-version <family>=<semver>`, repeatable). Slice B1 retired the legacy `--family` / `--family-version` flags in favor of the per-family explicit-version mapping that honors D-3seg upstream lines per-family (ADR-001).

`Package` requires Harvest + NativeSmoke + ConsolidateHarvest to have produced `harvest-manifest.json` for each library in scope first; the task fails fast if the harvest output is missing or empty.

**Primary path (Cake-first, per-family D-3seg versions):**

```bash
# Single-family (Core, UpstreamMajor.Minor = 2.32):
dotnet run --project build/_build/Build.csproj -- \
  --target Package \
  --explicit-version sdl2-core=2.32.0-smoke.1

# Multi-family with per-family D-3seg upstream lines (topological order respected):
dotnet run --project build/_build/Build.csproj -- \
  --target Package \
  --explicit-version sdl2-core=2.32.0-smoke.1 \
  --explicit-version sdl2-image=2.8.0-smoke.1 \
  --explicit-version sdl2-mixer=2.8.0-smoke.1 \
  --explicit-version sdl2-ttf=2.24.0-smoke.1 \
  --explicit-version sdl2-gfx=1.0.0-smoke.1

# Linux / macOS: add --repo-root "$(pwd)" as in the other targets.
```

> **Natural local-dev alternative.** For a full local feed bootstrap, prefer `tools setup --source=local` (see §K). The script internally drives Cake's `ResolveVersions` target with a `local.<timestamp>` suffix and then composes PreFlight → Harvest → ConsolidateHarvest → Package in one invocation — no `--explicit-version` required. Use explicit `Package` with `--explicit-version` only when you need a specific version string (e.g., for PA-2 witness records, for repro of a specific reported-failure CI run, or for exercising the pack surface in isolation).

**What to look for:**

- Three `.nupkg` files per family in `artifacts/packages/`: `Janset.SDL2.<Role>.<version>.nupkg` (managed), `Janset.SDL2.<Role>.<version>.snupkg` (symbols), `Janset.SDL2.<Role>.Native.<version>.nupkg` (native).
- Cake log emits `Packed family '<family>': <native.nupkg>, <managed.nupkg>, <symbols.snupkg>` once per family.
- Post-pack validator runs silently on success; any G21–G23, G25–G27, G47, or G48 violation is logged with its guardrail prefix and fails the task.
- Native nupkg layout check (optional, but useful after any `src/native/Directory.Build.props` change): `unzip -l artifacts/packages/Janset.SDL2.Core.Native.<version>.nupkg` should show `buildTransitive/Janset.SDL2.Core.Native.targets`, `buildTransitive/Janset.SDL2.Native.Common.targets`, and per-RID payload as either `runtimes/<rid>/native/*.dll` (Windows) or `runtimes/<rid>/native/Janset.SDL2.Core.Native.tar.gz` (Unix).

### K. PackageConsumerSmoke (consumer restore + runtime SDL_Init)

`PackageConsumerSmoke` runs the per-TFM TUnit smoke against the local feed produced by `Package` (or by `tools setup --source=local`), plus a netstandard2.0 compile-sanity pass. Two flows exist; pick based on what you want to exercise.

**Flow 1 (primary for big smoke) — `tools setup`:**

`tools setup --source=local` composes ResolveVersions → PreFlightCheck → EnsureVcpkgDependencies → Harvest → ConsolidateHarvest → Package + writes `build/msbuild/Janset.Local.props` in a single invocation with per-family D-3seg versions auto-derived from the manifest (suffix `local.<timestamp>`). `NativeSmoke` is **not** part of this chain (CMake + platform C/C++ toolchain prereq is orthogonal to feed materialisation; native smoke runs as its own standalone target or via the CI harvest matrix). The script always uses the host RID. Pair this invocation with a direct Cake `PackageConsumerSmoke` afterwards for end-to-end witness:

```bash
# Windows / Linux / macOS:
dotnet run --file tools.cs -- setup --source=local
# (or `./tools.cs setup --source=local` on Unix after `chmod +x tools.cs`)

dotnet run --project build/_build/Build.csproj -- \
  --target PackageConsumerSmoke

# Optional sibling — verify native payloads load at OS level:
dotnet run --project build/_build/Build.csproj -- \
  --target NativeSmoke
```

For a single-shot run that also includes `NativeSmoke` and `PackageConsumerSmoke` (full 9-step CI replay on the host RID), use the dedicated subcommand:

```bash
dotnet run --file tools.cs -- ci-sim
# Add --verbose to tee each step's output to the console; per-step logs land
# under .logs/tools/<platform>-ci-sim-<runid>/.
```

**Flow 2 (targeted, version-pinned) — explicit `Package` + `PackageConsumerSmoke`:**

Use when you need a specific version mapping (PA-2 witness, CI-run repro, pack-surface-only exercise):

```bash
# Windows:
dotnet run --project build/_build/Build.csproj -- \
  --target Package \
  --explicit-version sdl2-core=2.32.0-smoke.1 \
  --explicit-version sdl2-image=2.8.0-smoke.1

dotnet run --project build/_build/Build.csproj -- \
  --target PackageConsumerSmoke --rid win-x64

# Linux / macOS: add --repo-root "$(pwd)" to both.
```

> `PostFlight` retired in Slice B2 (2026-04-21). The Cake-side `SetupLocalDev` target itself was retired in Phase Y (2026-05-03); its multi-feature composition now lives in repo-root `tools.cs` (Flow 1). `--family` / `--family-version` retired in Slice B1 — use repeated `--explicit-version <family>=<semver>` entries (or `--versions-file <path>`) instead.

**What to look for:**

- `Running dotnet compile-sanity netstandard2.0 consumer` passes first (compile-only sanity against the Compile.NetStandard consumer).
- One `Running dotnet test package-smoke (<tfm>)` line per executable TFM resolved from `PackageConsumer.Smoke.csproj`'s inherited `$(ExecutableTargetFrameworks)` — typically `net10.0`, `net9.0`, `net8.0`, `net462`.
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
| macOS SDK version mismatch | macOS | Multiple SDKs installed (8/9/10/11) | global.json pins to 10.0.x — verify with `dotnet --version` from repo root |
| Lingering `dotnet` processes after `PostFlight` / `PackageConsumerSmoke` | All | MSBuild worker nodes (`/nodemode:1 /nodeReuse:true`), VBCSCompiler (Roslyn), and testhost (Microsoft Testing Platform) stay alive ~10 min for reuse. On Windows they hold file handles on `Microsoft.Testing.Platform.dll` and break the next run's `bin/` cleanup. On macOS / Linux the same processes accumulate RAM (~100 MB each). | Automatic mitigation + manual fallback documented below under [Lingering dotnet processes mitigation](#lingering-dotnet-processes-mitigation). |
| `$PWD` points at `/mnt/...` or the Windows path inside a WSL invocation (or at the SSH client's working directory on a macOS SSH invocation) despite a successful `cd` | WSL invoked via Windows `wsl -c '...'`, macOS invoked via non-interactive `ssh <host> '...'` | WSLENV + non-interactive SSH env inheritance: the caller shell's `PWD` leaks into the child shell, and some `bash` command-substitution paths (`$(pwd)`, subshells) read the stale value before `cd` has re-exported it. The runbook's documented `--repo-root "$PWD"` shape assumes an **interactive** WSL terminal / SSH session where POSIX `cd` updates `$PWD` reliably. | Only affects cross-shell harnesses (Windows bash → `wsl -c`, remote tooling → `ssh … '…'`). Human operators running the runbook inside an interactive WSL terminal or an interactive SSH session do not see this. If you do hit it, bind an absolute path first (`REPO=/home/deniz/repos/sdl2-cs-bindings` on Linux or `REPO=/Users/armut/repos/sdl2-cs-bindings` on macOS), then pass `--repo-root "$REPO"` everywhere the runbook says `--repo-root "$PWD"`. See [PWD env leakage in non-interactive cross-shell invocations](#pwd-env-leakage-in-non-interactive-cross-shell-invocations) below. |

### PWD env leakage in non-interactive cross-shell invocations

Not a code regression. Documented here so anyone driving the witness runbook from a scripted cross-shell harness knows the failure mode at sight.

**Symptom.** A command that resolved `--repo-root "$PWD"` (or any shell variable that chases `PWD`) picks up a path from the **caller** shell rather than the WSL / macOS shell we `cd`'d into. On WSL, the leaked path is typically `/mnt/<drive>/<project-path>` — the WSLENV mapping of the Windows caller's `PWD`. On macOS SSH, it is whatever the SSH client exported as `PWD` before invocation. Cake then resolves `Paths.RepoRoot` against that wrong directory, which cascades into every subsequent path the build host touches (artifacts root, harvest output, native-smoke build dirs, …).

**Root cause.** Non-interactive shells inherit environment variables from the caller via WSLENV (`wsl -c '...'` from Windows) or standard SSH env forwarding (`ssh <host> '...'`). `bash`'s `cd` builtin does update `$PWD` in the current shell after the `cd`, but some command-substitution forms (`$(pwd)`, subshells that fork before the next statement, `bash -c '…'` chains) re-read the stale inherited `$PWD` before it is overwritten. The `pwd` builtin and `realpath .` hit the kernel's CWD instead, so those report the correct path — which is what makes the divergence confusing (`$PWD` and `$(pwd)` disagree). The failure does not reproduce in an interactive WSL terminal or an interactive SSH shell because those shells start a fresh login env without inheriting Windows' `PWD`.

**When the runbook's `--repo-root "$PWD"` is safe.**

- Interactive WSL terminal opened inside the clone.
- Interactive SSH session to the macOS host followed by `cd` into the clone.
- Any bash invocation where the caller shell has already `cd`'d into the clone and stays within the same process group.

**When to replace `"$PWD"` with an absolute `REPO` binding.**

- Driving the runbook from a Windows harness via `wsl -c '<script>'` or `wsl -d <distro> -- bash -c '<script>'`.
- Driving the runbook from a remote orchestrator via `ssh <host> '<script>'`.
- Any CI context where the script might be invoked by a non-interactive parent that already exported `PWD`.

**Pattern.** Prepend the per-host absolute path and use it consistently:

```bash
# WSL (Linux)
REPO=/home/deniz/repos/sdl2-cs-bindings

# macOS Intel (SSH)
REPO=/Users/armut/repos/sdl2-cs-bindings

cd "$REPO" || exit 1
# …
dotnet run --project "$REPO/build/_build/Build.csproj" -c Release -- \
  --target PreFlightCheck --rid <rid> --repo-root "$REPO"
```

No build-host code change is required; this is purely an operator / harness input-hygiene note. Cake's `DetermineRepoRootAsync` already honors `--repo-root` as an absolute override and will use whichever path the caller supplies.

### Lingering dotnet processes mitigation

`PackageConsumerSmokeRunner` runs `dotnet build-server shutdown` on entry, again before each executable TFM slice, passes `--disable-build-servers -p:UseSharedCompilation=false -nodeReuse:false` to `dotnet build` invocations, and passes only `-p:UseSharedCompilation=false` to `dotnet test` invocations. .NET 10's Microsoft.Testing.Platform `dotnet test` command rejects the old CLI-level build-server flags and forwards them to the test application, so the shutdown calls are the cross-TFM cleanup mechanism for test slices. That keeps the normal case clean: new runs do not inherit detachable MSBuild / Roslyn / Razor server children and the later `net462` slice is less likely to inherit bad state from earlier `net10.0` / `net9.0` / `net8.0` runs.

**Shutdown call count per PackageConsumerSmoke run.** One shutdown fires on entry; one fires before **each** executable TFM slice. Concrete totals:

- Windows default (net10.0 + net9.0 + net8.0 + net462): **5 shutdowns** per PackageConsumerSmoke invocation.
- Linux default (net10.0 + net9.0 + net8.0; net462 skipped for Mono/TUnit incompatibility): **4 shutdowns**.
- macOS with `mono` on `$PATH` (net10.0 + net9.0 + net8.0 + net462): **5 shutdowns**. macOS without Mono (net10.0 + net9.0 + net8.0; net462 auto-skipped): **4 shutdowns**.

**Side-effect warning — `dotnet build-server shutdown` is scoped per-user, not per-project.** It also terminates CLI build servers owned by any other concurrent shell running `dotnet build` / `dotnet watch` / `dotnet test` (those processes re-spawn their servers on the next build; work is not lost, but there is a small warm-cache hit). The command does NOT touch Visual Studio, Rider, or VS Code / C# DevKit language-service MSBuild nodes — those run under different hosts and use separate IPC channels. If you are mid-way through a long parallel CLI build in another terminal, defer the `PackageConsumerSmoke` run until it finishes.

**Manual fallback — if something still wedges** (usually a prior run that was killed mid-flight and left `testhost.exe` holding `Microsoft.Testing.Platform.dll`):

```powershell
# Kill all CLI-launched build servers (same action the runner performs):
dotnet build-server shutdown

# Kill BOTH dotnet (MSBuild workers, VBCSCompiler) and testhost.exe processes.
# testhost.exe is the TUnit / Microsoft Testing Platform host that survives
# the build-server shutdown's 10-minute reuse window and holds open file
# handles on Microsoft.Testing.Platform.dll — that handle is what trips
# CleanArtifacts in the very next run. Targeting both names is required.
Get-Process dotnet, testhost -ErrorAction SilentlyContinue |
    Where-Object { ((Get-Date) - $_.StartTime).TotalMinutes -gt 15 -and $_.Id -ne $PID } |
    Stop-Process -Force -ErrorAction SilentlyContinue
```

On macOS / Linux the equivalent is `pkill -f /nodemode:1` for MSBuild worker nodes, or use the same `dotnet build-server shutdown` which is cross-platform.

**Tree-scoped kill (experiment).** A more precise fix — killing only the processes spawned by the runner itself via `Process.Kill(entireProcessTree: true)` — is tracked as a roadmap experiment (GitHub issue, labels `area:build-system`, `type:experiment`). That refactor would eliminate the side-effect warning above but requires giving up Cake's `IProcess` abstraction for the smoke runner's `dotnet` invocations; deferred until the side-effect actually bites someone's parallel workflow.

**Pre-`tools ci-sim` ritual (Phase X observation, 2026-05-02; carried forward).** During
the Phase X build-host migration the test loop ran `dotnet test` followed by a witness
script repeatedly, and the witness's `01-CleanArtifacts` step tripped on the
`Microsoft.Testing.Platform.dll` lock the previous `dotnet test` left behind — observed
twice during P2 (Adım 6 and Adım 7 fast-loop gates). The same lock surface still applies
to `tools ci-sim` (whose first step is `CleanArtifacts`); the reliable recipe before each
invocation in a tight test-then-verify loop is:

```powershell
dotnet build-server shutdown 2>&1 | Out-Null
Get-Process dotnet, testhost -ErrorAction SilentlyContinue |
    Where-Object { ((Get-Date) - $_.StartTime).TotalMinutes -gt 1 -and $_.Id -ne $PID } |
    Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Seconds 3
dotnet run --file tools.cs -- ci-sim
```

(The `1-minute` age threshold is intentionally tighter than the `15-minute`
manual-fallback default above — by the time you're running the second
`tools ci-sim`, the lingering `testhost.exe` is at most a few minutes old
and the conservative cutoff would miss it.) Phase Y (2026-05-03) retired
the `tests/scripts/smoke-witness.cs` + `verify-baselines.cs` machinery; the
operative reproduction surface is now `tools ci-sim` directly.

## Host Liveness Pre-flight (added 2026-05-02 from Phase X session)

The macOS Intel host at `Armut@192.168.50.178` is on a private LAN and
auto-sleeps when idle. Before driving a remote SSH run from a script — for
example a `tools ci-sim` invocation routed at the macOS host as part of a
multi-platform witness sweep — probe liveness with a fast `BatchMode`
connect attempt:

```powershell
ssh -o ConnectTimeout=5 -o BatchMode=yes Armut@192.168.50.178 'echo MAC_AWAKE'
```

- Exit `0` + literal `MAC_AWAKE` echoed back → host is reachable, key auth
  succeeded, proceed.
- Exit `255` + `Connection timed out` → host is asleep (or off LAN). Skip
  the macOS run; phase-x §10.5 makes macOS Intel coverage non-gating
  precisely for this case. The witness flow continues with Win + WSL Linux.
- Exit `255` + `Permission denied (publickey,…)` → host reachable but key
  auth needs setup. Resolve before retrying.
- Anything else → treat as a real connectivity issue, do not retry blindly.

The `BatchMode=yes` flag suppresses any interactive prompts (password,
host-key fingerprint confirmation) so a sleeping or mis-keyed host fails
fast instead of hanging on an unanswerable prompt — important when the
probe runs from a script that expects a deterministic exit code.

WSL Linux is always reachable from the same Windows box (it shares the
process tree), so no liveness probe is needed for the Linux clone — the
`wsl zsh -c '…'` invocation either succeeds immediately or surfaces a
distro-not-installed error that's obviously fatal.

> **WSL invocation gotcha (already documented under [PWD env leakage in
> non-interactive cross-shell invocations](#pwd-env-leakage-in-non-interactive-cross-shell-invocations)).**
> When driving WSL from a Windows PowerShell harness via `wsl zsh -c '…'`,
> bind absolute paths up front (`cd /home/deniz/repos/sdl2-cs-bindings && …`)
> rather than expanding shell variables — variable interpolation through the
> WSLENV bridge can drop or stale-out the value before zsh sees it.

## Failure Triage

When a checkpoint fails, classify before reacting:

1. **Environment issue** — dotnet not in PATH, missing tools, stale build output. Fix the environment, re-run.
2. **Stale repo** — platforms not on the same commit. `git pull` and re-run.
3. **Code regression** — same commit, clean environment, still fails. This is what the smoke matrix is designed to catch. Report with platform, checkpoint, exact error.
4. **Lingering process flake** — `Microsoft.Testing.Platform.dll` access denied during `01-CleanArtifacts` (Windows only). Not a regression; see [Lingering dotnet processes mitigation](#lingering-dotnet-processes-mitigation) for the build-server shutdown ritual. Run the ritual, retry once. If it persists past one retry, escalate to category 1/3.

Do not conflate environment issues with code regressions. The majority of cross-platform "failures" in practice are category 1, 2, or 4.

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
| `tools setup --source=local` | Repo pack → `artifacts/packages` | PackageReference + exact-pinned smoke override | `build/msbuild/Janset.Local.props` written by the script |
| `tools setup --source=remote-github` | Internal feed download → local cache | Same | Same override file + `versions.json`, written from remote-discovered versions |
| `tools setup --source=remote-nuget` | Stub pending Phase 2b PD-7 (public NuGet promotion) | Same | n/a — exits with "not yet implemented" |
| This smoke matrix (manual A–K walkthrough) | Whatever the operator staged | Same | CLI `-p:` flags or local.props |

After `tools setup` runs, IDE-opened smoke csprojs restore without Cake in the loop (the `Janset.Local.props` conditional import picks up the per-developer feed + version values). See [local-development.md](local-development.md) for the full Quick Start flow.

## Relationship to CI

This local smoke matrix validates the **same command surface** that CI workflows use. The key differences:

- CI sets up its own environment (PATH, SDK, vcpkg bootstrap) per job.
- CI uses matrix jobs across RIDs; local smoke runs one RID per platform.
- CI uploads artifacts; local smoke checks artifacts exist locally.
- `PreFlightCheck` has been a first-class CI gate in `release.yml` since Slice B1 (`resolve-versions → preflight` job chain); local smoke runs it as checkpoint D.
- `GenerateMatrix` + `Harvest` (7-RID matrix, with `NativeSmoke` running inline within each harvest job after the Slice E P4c collapse) + `ConsolidateHarvest` (aggregation) live in `release.yml` against the post-Slice-E-follow-up-pass topology. Apt prereqs + vcpkg-setup composite + submodule recursion + NuGet cache + Cake compile-once discipline are absorbed via the `nuget-cache` + `vcpkg-setup` + `platform-build-prereqs` composites + the GHCR `linux-builder:focal-latest` image. The retired `prepare-native-assets-*.yml` family was deleted 2026-04-25 (P8.1).

If local smoke passes but CI fails, the issue is almost certainly in CI environment setup (or an unabsorbed Slice-E gap), not in the build host code.

### Stage-Owned Validation Mapping (ADR-003 alignment)

Under [ADR-003 §3.5 + §4](../decisions/2026-04-20-release-lifecycle-orchestration.md), every validation belongs to exactly one pipeline stage. The manual A–K checkpoint walk-through in this playbook maps onto those stages as follows:

| ADR-003 stage | Playbook checkpoints | Stage-owned guardrails exercised |
| --- | --- | --- |
| **PreFlight** | D (PreFlightCheck) | G4, G6, G7, G14, G15, G16, G17, G18, G49, G54 |
| **EnsureVcpkgDependencies** (supporting, not release-path) | D1 | — (vcpkg install preflight) |
| **Harvest** (per-RID matrix target) | E (Harvest full manifest scope) | G19 (hybrid leak), G50 (primary ≥ 1) |
| **ConsolidateHarvest** (aggregation) | F | G53 (staged-replace invariant) |
| **NativeSmoke** (per-RID) — extracted stage, Cake target landed Slice D via Cake.CMake + `NativeSmokeRunnerTool` | G (NativeSmoke Cake target wraps C/C++ harness) | — (no G-series yet; runtime fail on `Passed:/Failed:/Result:` output + non-zero exit code is the fail surface) |
| **Pack** (single-runner) | J (Package) | G13, G21, G22, G23, G25, G26, G27, G46, G47, G48, G51, G52, G55, G56, G57, G58 |
| **PackageConsumerSmoke** (per-RID matrix **re-entry** under ADR-003) | K (PackageConsumerSmoke) | — (TUnit behavioral smoke; PD-10 scope) |
| **Coverage** (ratchet gate, CI pre-matrix) | A + B + Coverage-Check | G36 |
| **Diagnostics (Slice D additions, off release-path)** | F1 (Inspect-HarvestedDependencies), N (CleanArtifacts), O (GenerateMatrix), P (CompileSolution) | — (operator / CI-plumbing surface; no guardrails) |

The playbook checkpoints are **local-host substitutes** for the CI stage sequence. When the ADR-003 `release.yml` lands, the per-stage CI jobs invoke the same Cake target surface this playbook exercises manually. The key promotion is checkpoint K: today it runs on a single host RID; under ADR-003 the CI matrix re-enters K on all 7 RIDs, which catches the per-RID consumer paths (Windows P/Invoke search order, macOS dyld two-level namespace, arm64 runtime resolver) that a single-host smoke cannot.

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
- SDK-level file copy places the native binaries directly under `bin/Release/net10.0/<rid>/`
- `dotnet run --no-build --no-restore -r <rid>` executes the pre-built binary with runtime assets already present in the output folder
- P/Invoke loader succeeds because the DLL is sitting next to the managed assembly

What `-r <rid>` does NOT exercise:

- The default framework-dependent consumer path (no `<RuntimeIdentifier>` set), where runtime asset resolution goes through MSBuild's `runtimetargets` evaluation and the host runtime's native library resolver (not the build-time file copy)
- Multi-RID publish scenarios (`dotnet publish -r linux-x64 --self-contained`)
- Consumer projects that set `<RuntimeIdentifiers>` (plural) for multi-target output
- Any form of NuGet client version drift (restore works with modern SDK; older NuGet clients might not honour the runtime subtree layout identically)

**Consequence for D-local:** checkpoint K today proves "runtime assets LAND in bin/ via build-time file copy on win-x64." It does NOT prove "runtime assets LOAD via the default framework-dependent consumer flow across RIDs." Both are legitimate Package Validation Mode truths per [`research/execution-model-strategy-2026-04-13.md §7.2`](../research/execution-model-strategy-2026-04-13.md) but they exercise different subsystems.

**When to revisit:** before K is promoted to active on all three platforms, decide whether the smoke should also run a second invocation **without** `-r <rid>` to cover the framework-dependent resolver path. The decision lives in [`phases/phase-2-adaptation-plan.md`](../phases/phase-2-adaptation-plan.md) Pending Decisions (PD-10).

## .NETFramework AnyCPU Consumer Guideline

`Janset.SDL2.*.Native` packages support .NETFramework SDK-style consumers (net4x TFMs) on Windows via the `_JansetSdlNativeCopyDllsForFrameworkWindows` target. **Canonical reference for native asset placement contract: [`src/native/_shared/Janset.SDL2.Native.Common.targets`](../../src/native/_shared/Janset.SDL2.Native.Common.targets) — the in-file comment block carries the layout + arch resolution rationale; documentation here is a pointer, not a separate source of truth.**

### Default behavior (zero csproj configuration)

For modern SDK-style net4x projects with default `Prefer32Bit=false`, the apphost runs at host OSArchitecture (typically x64). The consumer-side `.targets` resolves the copy RID from `Platform` / `Prefer32Bit` / `OSArchitecture` (deliberately NOT from `RuntimeIdentifier` — SDK auto-x86 inference for AnyCPU + native package presence makes that signal indistinguishable from user-explicit `-r win-x86`):

| Consumer state | Selected native RID |
| --- | --- |
| `Platform=x64` / `x86` / `ARM64` | `win-x64` / `win-x86` / `win-arm64` (Priority 1) |
| `AnyCPU` + `Prefer32Bit=true` | `win-x86` (Priority 2 — 32-bit-required intent honoured) |
| `AnyCPU` + `Prefer32Bit` unset/false | host `OSArchitecture` (Priority 3 — canonical AnyCPU-runs-at-host-arch; overrides SDK auto-x86 inference) |

The `JANSET_SDL_ANYCPU_ARCH_ASSUMPTION` warning fires on the Priority-3 fallback, advising consumers to set `<RuntimeIdentifier>` or `<Platform>` explicitly when the deployed process arch differs from host (e.g., `Prefer32Bit=true` builds, `32BIT_REQUIRED` EXEs, x64 binaries on arm64 Windows under emulation).

### Opt-in to win-x86 native shipping

For AnyCPU consumers that genuinely need 32-bit native shipping while running on an x64 host, set one of:

```xml
<!-- Option A: Prefer32Bit (standard .NETFramework AnyCPU 32-bit-pref) -->
<PropertyGroup>
  <Prefer32Bit>true</Prefer32Bit>
</PropertyGroup>

<!-- Option B: Explicit Platform target -->
<PropertyGroup>
  <PlatformTarget>x86</PlatformTarget>
</PropertyGroup>

<!-- Option C: Explicit RuntimeIdentifier (still respected as Priority 1 input via $(Platform) mapping) -->
<PropertyGroup>
  <RuntimeIdentifier>win-x86</RuntimeIdentifier>
</PropertyGroup>
```

### Why RuntimeIdentifier alone is unreliable

When SDK-style net4x detects a `.Native` package's per-RID payload, it auto-sets `PlatformTarget=x86` + `RuntimeIdentifier=win-x86` regardless of the actual deployment target — and user-explicit `-r win-x86` produces the **identical** MSBuild property state. Because the two scenarios are indistinguishable, the consumer-side copy target ignores `RuntimeIdentifier` and reads the orthogonal `Platform` / `Prefer32Bit` / `OSArchitecture` surface instead.

### Smoke runner alignment

`PackageConsumerSmokeRunner.AppendNet4xPlatformArgument` ([`build/_build/Application/Packaging/PackageConsumerSmokeRunner.cs`](../../build/_build/Application/Packaging/PackageConsumerSmokeRunner.cs)) forwards `-p:Platform=<arch>` alongside `-r <rid>` for any `net4*` TFM (`win-x64 → x64`, `win-x86 → x86`, `win-arm64 → ARM64`), so the smoke runner's explicit RID intent maps to Priority 1 with no ambiguity. No-op for non-`net4*` TFMs and non-Windows RIDs.

## PA-2 Per-Triplet Witness Invocations

PA-2 moved four previously-stock runtime rows onto hybrid overlay triplets (2026-04-18). **PA-2 behavioral validation closed 2026-04-26** via `release.yml` run 24938451364 on master `8ec85c5`. This section retains the per-RID command reference for ad-hoc local-host witness reproductions (e.g., diagnosing a future regression) and for the manual escape hatch (PD-8) when CI is broken; it is **no longer a phase gate**.

**Closure record (2026-04-26):** `workflow_dispatch` on master `8ec85c5` with default `mode=manifest-derived` produced `release.yml` run 24938451364 — all 10 jobs green; the four PA-2 RIDs cleared every stage's owned validators on their native runners. Per-TFM TUnit `PackageConsumerSmoke` results: **win-arm64** net9.0/net8.0/net462 = 12+12+11 = **35/35**; **win-x86** net9.0/net8.0/net462 = 12+12+11 = **35/35**; **linux-arm64** net9.0/net8.0 = 12+12 = **24/24** (net462 auto-skipped per Linux Mono+TUnit incompat gate); **osx-arm64** net9.0/net8.0 = 12+12 = **24/24** (net462 auto-skipped per macOS no-mono gate). `Harvest + NativeSmoke` green on every RID, so the 29-test C harness built and executed under each PA-2 RID's native (or near-native via WoW64 for `win-x86`) runtime. Suffix shape: `ci.24938451364.1` (the `release.yml` `--suffix=ci.${{ github.run_id }}.${{ github.run_attempt }}` default — the prior `pa2.<run-id>` wording in this section was fictional; `ExplicitVersionProvider` covers any custom suffix shape an operator might want, e.g. `--explicit-version sdl2-core=2.32.0-pa2-witness.1` for a manually-labeled witness run).

**ADR-003 + Slice B1/D CLI migration note (2026-04-21).** The legacy `--family` / `--family-version` flags on `PostFlight` / `Package` retired in Slice B1. Local-host PA-2 reproductions now run through the post-B1 CLI: either the `tools setup --source=local` umbrella (for full local-feed + consumer-smoke end-to-end on the host RID) or an explicit `Package` → `PackageConsumerSmoke` pair with `--explicit-version <family>=<semver>` per family (or `--versions-file`). CI runs flow through `release.yml` `workflow_dispatch` with `mode=manifest-derived` (the closure shape, suffix `ci.<run-id>.<attempt>`) or `mode=explicit` (operator-supplied per-family versions including any custom suffix).

Each invocation packs the concrete five-family smoke scope, exercises harvest → consolidate → pack → consumer-smoke, and records pass/fail against this playbook. Record the result in the "Last validated" header at the top of this document as each triplet passes; any failure triage into (a) upstream vcpkg port issue, (b) overlay-triplet tuning needed, or (c) vcpkg feature-flag degradation — file a `docs/research/` note before re-attempting.

Trigger each run via workflow-dispatch on the matching GitHub runner:

| # | RID | Triplet | Runner (workflow dispatch) | Notes |
| --- | --- | --- | --- | --- |
| 1 | `win-x86` | `x86-windows-hybrid` | `windows-latest` (cross-arch from x64 host) | 32-bit MSVC calling conventions; first hybrid-static zlib/libpng/libjpeg-turbo bake on x86. |
| 2 | `win-arm64` | `arm64-windows-hybrid` | `windows-latest` (cross-arch from x64 host) | Watch for vcpkg port gaps on windows-arm64 — particularly `timidity` and `wavpack` — that can silently degrade the SDL2_mixer codec set. |
| 3 | `linux-arm64` | `arm64-linux-hybrid` | `ubuntu-24.04-arm` (native arm64) | Community triplet base + `ubuntu:24.04` container; newer glibc vs x64's `ubuntu:20.04`. Watch for `-fvisibility=hidden` interactions with freetype / libwebp symbol versioning. |
| 4 | `osx-arm64` | `arm64-osx-hybrid` | `macos-latest` (Apple Silicon) | First arm64-osx run through the hybrid bake. Watch for MachO layout / `VCPKG_OSX_ARCHITECTURES=arm64` / dyld cache surprises. |

Per-RID command (adapt the runner via workflow input; body identical).

**Primary path — host-RID `tools setup` umbrella** (recommended when the PA-2 RID matches the host: auto-derives per-family D-3seg versions, runs the full local pipeline, writes `Janset.Local.props`, then `PackageConsumerSmoke` covers consumer restore + runtime):

```bash
# Run on the matching native runner — `tools setup` always uses the host RID.
dotnet run --file tools.cs -- setup --source=local

dotnet run --project build/_build/Build.csproj -c Release -- \
  --target PackageConsumerSmoke
```

**Alternative — explicit per-family version mapping** (use when you need a specific version string, a non-host RID via cross-build, or a custom suffix like `pa2-witness.<rid>.1`):

```bash
dotnet run --project build/_build/Build.csproj -c Release -- \
  --target Package --rid <rid> \
  --explicit-version sdl2-core=2.32.0-pa2-witness.<rid>.1 \
  --explicit-version sdl2-image=2.8.0-pa2-witness.<rid>.1 \
  --explicit-version sdl2-mixer=2.8.0-pa2-witness.<rid>.1 \
  --explicit-version sdl2-ttf=2.24.0-pa2-witness.<rid>.1 \
  --explicit-version sdl2-gfx=1.0.0-pa2-witness.<rid>.1

dotnet run --project build/_build/Build.csproj -c Release -- \
  --target PackageConsumerSmoke --rid <rid>
```

> **NativeSmoke on PA-2 RIDs.** `tests/smoke-tests/native-smoke/CMakePresets.json` ships configure + build presets for every `manifest.runtimes[]` row (14 buildPresets total, Release + Debug per RID, since Slice E follow-up P8.4). `NativeSmoke` is not part of the `tools setup` chain — invoke it explicitly when you want native-level coverage on a local PA-2 reproduction (`--target NativeSmoke --rid <pa2-rid>`), or run `tools ci-sim` for the full 9-step host-RID replay that includes `NativeSmoke`. The Cake host does not hard-code a RID allow-list; the preset file is the source of truth.

**Acceptance per witness:**

- PreFlight: 6/6 versions, 7/7 strategy coherence, G49 core-identity, csproj contract all green.
- Harvest: each selected library reports `primary=1, runtime=0` and emits `rid-status/<rid>.json` with `success=true`.
- Consolidate: `harvest-manifest.json` + `harvest-summary.json` + `licenses/_consolidated/` produced.
- Package: `Janset.SDL2.<Role>.nupkg`, `.snupkg`, and `.Native.<version>.nupkg` produced per family with post-pack validator (G21–G23, G25–G27, G46, G47, G48) = 0 violations.
- PackageConsumerSmoke: netstandard2.0 compile-sanity pass + per-TFM TUnit pass for executable TFMs (net9.0 / net8.0 on every runner; net462 on Windows always, on macOS only when `mono` is on `$PATH` — else auto-skipped; always skipped on Linux). See [Consumer Invocation Contract](#consumer-invocation-contract-checkpoint-k) for the per-TFM matrix rationale.

Any failure that isn't an obvious environment issue triggers a research note at `docs/research/pa2-witness-<rid>-<date>.md` before re-attempting. Do not paper over behavioral failures with workarounds in the overlay files — file the research note first, diagnose root cause, then either fix or retreat.
