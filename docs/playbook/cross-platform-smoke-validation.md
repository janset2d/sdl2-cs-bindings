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

## Smoke Matrix

This matrix is a living validation surface that grows as new Cake tasks and streams land. Each checkpoint has a stream origin so you know when it was introduced and whether it applies to the current codebase.

### Active Checkpoints

These are validated today and should pass on all 3 platforms.

| # | Checkpoint | Stream | What It Proves | Expected Output |
| --- | --- | --- | --- | --- |
| A | Build-host unit tests | Baseline | Refactored code logic is correct | 273 passed, 0 failed (247 → 256 A-risky → 270 S1 → 273 post-S1 buildTransitive G47/G48 tests) |
| B | Cake restore + build (Release) | Baseline | Build host compiles clean on all platforms | 0 warnings, 0 errors (usually implied by A — tests build the same assemblies) |
| C | Cake `--tree` | Baseline | Task dependency graph is intact | `PostFlight → PackageConsumerSmoke → Package → PreFlightCheck`; `ConsolidateHarvest → Harvest → Info` |
| D | PreFlightCheck | Baseline + A-risky + S1 | manifest.json ↔ vcpkg.json consistency + strategy coherence + post-S1 csproj pack contract (G4/G6/G7/G17/G18) | 6/6 versions, 7/7 strategies, 6/6 families × 10/10 csprojs all green |
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
| L | Source-Mode-Prepare | F | `--source=local` stages natives correctly per-platform | Task implemented + Directory.Build.targets wired |
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
  --family-version 1.3.0-smoke.1

# Multi-family (topological order respected, one --family-version applies to all):
dotnet run --project build/_build/Build.csproj -- \
  --target Package --family sdl2-core --family sdl2-image \
  --family-version 1.3.0-smoke.1

# Linux / macOS: add --repo-root "$(pwd)" as in the other targets.
```

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
  --family-version 1.3.0-smoke.1

# Linux / macOS: add --repo-root "$(pwd)".
```

**What to look for:**

- `Running dotnet compile-sanity netstandard2.0 consumer` passes first (compile-only sanity against the Compile.NetStandard consumer).
- One `Running dotnet test package-smoke (<tfm>)` line per executable TFM resolved from `PackageConsumer.Smoke.csproj`'s inherited `$(ExecutableTargetFrameworks)` — typically `net9.0`, `net8.0`, `net462`.
- `Failed: 0` for each TFM. On the current expanded Windows scope, passing tests include native asset landing for `core/image/mixer/ttf/gfx`, `SDL_Init_Cycle_Succeeds`, PNG fixture load, mixer decoder-surface validation, `TTF_Init`, a headless `SDL2_gfx` render path, and linked-version major checks. The Unix symlink assertion still applies on modern Unix TFMs only (`#if NET6_0_OR_GREATER`).
- net462 on Linux is **skipped** with a `Skipping package-smoke for TFM 'net462'` warning — Mono 6.12 cannot host the TUnit + Microsoft Testing Platform discovery stack, so runtime coverage there is intentionally absent. macOS Homebrew Mono is not skipped.
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
- macOS default (net9.0 + net8.0 + net462 via Homebrew Mono): **4 shutdowns**.

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
- Reference every managed package you consume using the shared version property, e.g.:

    ```xml
    <PackageReference Include="Janset.SDL2.Core" VersionOverride="$(JansetSdl2CorePackageVersion)" />
    <PackageReference Include="Janset.SDL2.Image" VersionOverride="$(JansetSdl2ImagePackageVersion)" />
    ```

- Do **not** redeclare `LocalPackageFeed`, `RestoreAdditionalProjectSources`, version defaults, or guard targets — all of these live in the shared files.

Naming convention (authoritative in `FamilyIdentifierConventions`):

- `sdl<major>-<role>` family identifier → `Janset.SDL<Major>.<Role>` managed package + `Janset.SDL<Major>.<Role>.Native` native package + `JansetSdl<Major><Role>PackageVersion` version property.
- Example: `sdl2-mixer` → `Janset.SDL2.Mixer` / `Janset.SDL2.Mixer.Native` / `JansetSdl2MixerPackageVersion`.

### SDL3 Drop-in Readiness

`Janset.Smoke.props` already declares `JansetSdl3CorePackageVersion`, `JansetSdl3ImagePackageVersion`, etc. (all defaulted to the sentinel). When SDL3 ships, authoring an SDL3 consumer is a single-csproj change: add `<PackageReference Include="Janset.SDL3.Core" VersionOverride="$(JansetSdl3CorePackageVersion)" />` and the existing guard (`JNSMK101`) picks up missing version input without editing the shared files.

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
  --family-version 1.3.0-pa2-witness.<rid>.1 \
  --rid <rid>
```

**Acceptance per witness:**

- PreFlight: 6/6 versions, 7/7 strategy coherence, G49 core-identity, csproj contract all green.
- Harvest: each selected library reports `primary=1, runtime=0` and emits `rid-status/<rid>.json` with `success=true`.
- Consolidate: `harvest-manifest.json` + `harvest-summary.json` + `licenses/_consolidated/` produced.
- Package: `Janset.SDL2.<Role>.nupkg`, `.snupkg`, and `.Native.<version>.nupkg` produced per family with post-pack validator (G21–G23, G25–G27, G46, G47, G48) = 0 violations.
- PackageConsumerSmoke: netstandard2.0 compile-sanity pass + per-TFM TUnit pass for executable TFMs (net9.0 / net8.0 on every runner; net462 on Windows + macOS; net462 skipped on Linux).

Any failure that isn't an obvious environment issue triggers a research note at `docs/research/pa2-witness-<rid>-<date>.md` before re-attempting. Do not paper over behavioral failures with workarounds in the overlay files — file the research note first, diagnose root cause, then either fix or retreat.
