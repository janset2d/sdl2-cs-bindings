# tests/scripts

Local witness scripts for Cake build-host validation. Single-file .NET 10 apps
([file-based apps][msdocs-file-based-apps]) — no `.csproj`, no build ceremony, just run.

## `smoke-witness.cs`

Orchestrates the post-B2 flat Cake graph into two reproducible witness flows.

### Modes

| Mode | What it runs | Intent |
| --- | --- | --- |
| `local` (default) | `CleanArtifacts` → `SetupLocalDev` → `PackageConsumerSmoke` | Fast dev iterate. Validates the `SetupLocalDev` composition + end-to-end consumer smoke. |
| `ci-sim` | `CleanArtifacts` → `ResolveVersions` → `PreFlightCheck` → `EnsureVcpkgDependencies` → `Harvest` → `NativeSmoke` → `ConsolidateHarvest` → `Package` | Mini CI/CD replay. Every stage invoked standalone with the `ResolveVersions`-emitted mapping, mirroring `release.yml`. Proves the flat graph works when each stage is independently reachable. `PackageConsumerSmoke` is deliberately skipped — see note below. |

Logs land under `.logs/witness/<platform>-<mode>-<runId>/NN-<step>.log` (gitignored,
outside `artifacts/` so Cake's `CleanArtifacts` target cannot wipe them mid-run).
Each run clears its own subdir first in case the same second-level timestamp
collides.

### Run

**Unix (Linux / macOS):**

```bash
# One-time: mark executable. Git preserves +x via .gitattributes-level intent,
# but on fresh clones checked out on Windows side you may need to re-run this.
chmod +x tests/scripts/smoke-witness.cs

./tests/scripts/smoke-witness.cs             # local mode (default)
./tests/scripts/smoke-witness.cs ci-sim       # mini CI simulation
```

**Windows (shebang is Unix-only at the OS level — use `dotnet run`):**

```pwsh
dotnet run tests/scripts/smoke-witness.cs           # local mode
dotnet run tests/scripts/smoke-witness.cs ci-sim    # mini CI simulation
```

Either invocation works from the repo root; `git rev-parse --show-toplevel`
resolves the repo root internally.

### Requirements

- **.NET 10 SDK** (`10.0.100` or later). File-based apps + the `#:package` /
  `#:property` directives are .NET 10 features; `tests/scripts/global.json` pins
  the SDK selection for this directory scope so the repo-root `global.json`
  (net9-pinned for the build-host) does not interfere.
- **git** on `PATH` — used to resolve repo root + capture the short HEAD SHA
  into the summary panel.
- **vcpkg prerequisites** — the same set the main build needs (`EnsureVcpkgDependencies`
  is invoked as a stage). See [`docs/playbook/cross-platform-smoke-validation.md`][playbook].
- **CMake + C/C++ toolchain** (MSVC Developer shell on Windows / gcc / clang
  on Unix) — only when `ci-sim` reaches `NativeSmoke`. `local` mode skips
  `NativeSmoke` entirely (by design — ADR-003 §6.4 amendment: `SetupLocalDev`
  no longer composes `NativeSmoke`).

  **Windows note:** run `ci-sim` from a **Developer PowerShell for VS 2022** (or
  any shell where `vcvarsall.bat x64` has been sourced) — otherwise `cl.exe`
  is not on `PATH`, CMake cannot resolve `CMAKE_C_COMPILER`, and `NativeSmoke`
  halts the run. That failure is mini-CI-sim correctly surfacing a real env
  gap (CI's `vcpkg-setup` composite resolves it; your local shell does not),
  not a script bug. For fast iteration without a Developer shell, run
  `./smoke-witness.cs local`.

### Why `ci-sim` skips `PackageConsumerSmoke`

`PackageConsumerSmoke` today reads `build/msbuild/Janset.Smoke.local.props`
via MSBuild. That file is stamped **only** by `SetupLocalDev` through
`IArtifactSourceResolver.WriteConsumerOverrideAsync`. Running consumer smoke
after a standalone `Package` rolls `JNSMK001` ("Smoke projects require
LocalPackageFeed") because the props stub is missing.

Mini CI simulation refuses to inject local-dev-only helpers (stamping props
inside the script would make it a non-simulation). Per ADR-003 §6.5 / Slice C:
`PackageConsumerSmokeRequest(Rid, Versions, FeedPath)` will parameterise the
runner so `--feed-path` + `--explicit-version` carry the same contract the
props stub carries today — the natural CI flow then works end-to-end without
props. Until Slice C lands, run `./smoke-witness.cs local` for smoke
coverage.

### Design notes

- **Each stage = separate `Process.Start`.** No in-process Cake invocation.
  Mirrors CI exactly: every job runs its own Cake process with its own args.
- **`ResolveVersions` is the single version source** (ADR-003 §2.4 resolve-once
  invariant). The script reads the emitted JSON
  (`artifacts/resolve-versions/versions.json`) and feeds the mapping to every
  downstream stage via repeated `--explicit-version family=semver` flags.
- **Stdout + stderr captured to file**, not teed to TTY, so Spectre UI stays
  clean. Inspect the `NN-step.log` files for raw Cake output.
- **Exit code propagation:** first failing stage halts the run and returns
  its exit code (unified to `1` for non-zero). Summary panel shows which
  stage failed + log path.
- **Directory.Build.props + Directory.Packages.props inherit** from the repo
  root — the script intentionally does not isolate its MSBuild config from
  the surrounding project (Deniz direction 2026-04-21: inheritance "zararı
  yok" / no harm).

### Troubleshooting

- **`Can't find file-based app support`** → .NET 10 SDK missing. Install
  `10.0.100+` and re-run. `dotnet --list-sdks` should show a `10.x` entry.
- **`ResolveVersions JSON missing`** after `ci-sim` starts → the
  `ResolveVersionsTaskRunner` writes to
  `artifacts/resolve-versions/versions.json`; inspect the `02-ResolveVersions.log`
  for path/error details.
- **`JNSMK001` on `local` mode** → `SetupLocalDev` did not stamp the props
  file. Inspect the `02-SetupLocalDev.log` for resolver errors (usually
  missing feed directory or unknown family).
- **Shebang fails on Unix with "bad interpreter"** → the checkout picked up
  CRLF. `dos2unix tests/scripts/smoke-witness.cs` or re-clone after
  configuring `core.autocrlf=false`; `.gitattributes` pins LF for the
  scripts directory.

[msdocs-file-based-apps]: https://learn.microsoft.com/en-us/dotnet/core/sdk/file-based-apps
[playbook]: ../../docs/playbook/cross-platform-smoke-validation.md
