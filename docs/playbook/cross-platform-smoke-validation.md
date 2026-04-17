# Playbook: Cross-Platform Smoke Validation

> How to verify that the Cake build host, harvest pipeline, and native libraries work correctly across all 3 local platforms after a refactor or significant change.

**Last validated:** 2026-04-16 at A-risky closure. **Note (2026-04-17):** A-risky's exact-pin csproj shape (Mechanism 3) was retired by S1 adoption. The smoke validation procedures described below are unaffected — they exercise the harvest + pack + consumer path, not the dependency-shape invariants. Current baseline post-S1: MinVer + family rename + PreFlight `CsprojPackContractValidator` (post-S1 subset: G4 MinVerTagPrefix, G6 PackageId, G7 native project path, G17/G18 cross-section). Re-validation should happen after Phase 3 code changes (csproj cleanup + validator simplification + PackageTaskRunner collapse) land. See [phase-2-adaptation-plan.md "S1 Adoption Record"](../phases/phase-2-adaptation-plan.md).
**Result (at 2026-04-16 validation):** Checkpoints A-D green on all 3 platforms (Windows local, WSL Linux, macOS Intel SSH). 256/256 build-host tests, 0 warnings / 0 errors on Release build, PreFlight 6 families × 10 csprojs × 8 invariants all green per platform. E/F/G unchanged scope (harvest pipeline not touched by A-risky) — re-run when Stream D-local or later lands runtime-affecting changes.

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
| A | Build-host unit tests | Baseline | Refactored code logic is correct | 256 passed, 0 failed (count grows with coverage; 247 → 256 after A-risky added 9 `CsprojPackContractValidator` tests) |
| B | Cake restore + build (Release) | Baseline | Build host compiles clean on all platforms | 0 warnings, 0 errors |
| C | Cake `--tree` | Baseline | Task dependency graph is intact | ConsolidateHarvest→Harvest→Info chain visible |
| D | PreFlightCheck | Baseline + A-risky | manifest.json ↔ vcpkg.json consistency + strategy coherence + csproj pack contract (G1-G8 + G17-G18) | 6/6 versions, 7/7 strategies, 6/6 families × 10/10 csprojs all green |
| E | Harvest (6 satellites) | Baseline | Binary closure walk + deployment works per-platform | 6/6 succeeded, rid-status JSON generated |
| F | ConsolidateHarvest | Baseline | Per-RID merge produces manifest + summary | harvest-manifest.json + harvest-summary.json per library |
| G | Native smoke (C++) | Baseline | Hybrid-built natives load and initialize at runtime | 13/13 PASS, all codecs functional |

### Planned Checkpoints

These will be added as their parent streams land. Add the command reference and "what to look for" details when promoting from planned to active.

| # | Checkpoint | Stream | What It Will Prove | Promotion Criteria |
| --- | --- | --- | --- | --- |
| H | GenerateMatrixTask | C | Dynamic CI matrix produces correct 7-RID JSON from manifest | Task implemented + validated against hardcoded YAML |
| I | PreFlightCheck as gate (expanded) | C | Version resolution, package-family integrity, unit tests as gate | Stream C PreFlight expansion landed |
| J | PackageTask | D-local | Family-aware pack produces valid .nupkg per library | Task implemented and locally validated on the Phase 2a `sdl2-core` + `sdl2-image` win-x64 slice; promote to active after 3-platform validation |
| K | Package-consumer smoke | D-local | PackageReference restore + native binary load + SDL_Init from .nupkg | Dedicated consumer project + Cake smoke target exist and pass on the Phase 2a win-x64 slice; promote to active after 3-platform validation |
| L | Source-Mode-Prepare | F | `--source=local` stages natives correctly per-platform | Task implemented + Directory.Build.targets wired |

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

## Output Artifacts to Verify

| Artifact | Location | What to Check |
| --- | --- | --- |
| RID status files | `artifacts/harvest_output/{Library}/rid-status/{rid}.json` | One per library, status = success |
| Native payload (Windows) | `artifacts/harvest_output/{Library}/runtimes/{rid}/native/*.dll` | DLLs present |
| Native payload (Unix) | `artifacts/harvest_output/{Library}/runtimes/{rid}/native/native.tar.gz` | Archive present, symlinks preserved |
| Harvest manifest | `artifacts/harvest_output/{Library}/harvest-manifest.json` | Generated after consolidation |
| Harvest summary | `artifacts/harvest_output/{Library}/harvest-summary.json` | Human-readable summary present |

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
