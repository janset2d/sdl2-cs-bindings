# tests/scripts

Local witness scripts for Cake build-host validation. Single-file .NET 10 apps
([file-based apps][msdocs-file-based-apps]) — no `.csproj`, no build ceremony, just run.

`tests/scripts/global.json` was removed (2026-05-03). Scripts now use the repo-root
`global.json` (pinned to .NET 10.0.203). Run from any directory — the root SDK pin
is the single source of truth.

| Script | Purpose |
| --- | --- |
| [`smoke-witness.cs`](#smoke-witnesscs) | Black-box behavior witness — exercises the Cake target chain end-to-end via `dotnet run` from outside the host. Writes per-step logs and (with `--emit-baseline`) a deterministic JSON behavior signal. |
| [`verify-baselines.cs`](#verify-baselinescs) | Pre-merge gate helper — operationalizes the phase-x ADR-004 plan §2.1.5 fast/milestone loop cadence. Spawns `smoke-witness.cs` per loop entry, diffs emitted JSON against committed baselines, exits non-zero on mismatch. |

## `smoke-witness.cs`

Orchestrates the post-B2 flat Cake graph into two reproducible witness flows.

### Modes

| Mode | What it runs | Intent |
| --- | --- | --- |
| `local` (default) | `CleanArtifacts` → `SetupLocalDev --source=local` → `PackageConsumerSmoke` | Fast dev iterate. Validates the `SetupLocalDev` composition (vcpkg + harvest + pack + override) + end-to-end consumer smoke. |
| `remote` | `CleanArtifacts` → `SetupLocalDev --source=remote` → `PackageConsumerSmoke` | Test against the **last published wave** on GitHub Packages. Skips vcpkg/harvest/pack entirely — `RemoteArtifactSourceResolver` discovers + downloads the latest published nupkg per family, then the same consumer smoke validates them as an external consumer would. Requires `GH_TOKEN` env (see below). |
| `ci-sim` | `CleanArtifacts` → `ResolveVersions` → `PreFlightCheck` → `EnsureVcpkgDependencies` → `Harvest` → `NativeSmoke` → `ConsolidateHarvest` → `Package` → `PackageConsumerSmoke` | Mini CI/CD replay. Every stage invoked standalone with the `ResolveVersions`-emitted mapping, mirroring `release.yml`, then closes the loop with the same consumer smoke gate the pipeline uses after packaging. |

Logs land under `.logs/witness/<platform>-<mode>-<runId>/NN-<step>.log` (gitignored,
outside `artifacts/` so Cake's `CleanArtifacts` target cannot wipe them mid-run).
Each run clears its own subdir first in case the same second-level timestamp
collides.

> **Silent runs leave forensic logs too.** Since the P0-close session fix
> (smoke-witness.cs `InvokeProcessAsync` rewrite, 2026-05-02), the per-step
> log file is written regardless of `--verbose`. Verbose mode adds live
> console echo of stdout/stderr; silent mode just doesn't echo. Both modes
> now persist the per-step `NN-<step>.log` so a flaky failure leaves
> evidence on disk without requiring a verbose rerun. Phase-x [§14
> Adım 13](../../docs/phases/phase-x-build-host-modernization-2026-05-02.md#14-ad%C4%B1m-13-post-p2-follow-up-wave)
> design-notes for the rationale.

### Run

**Unix (Linux / macOS):**

```bash
# One-time: mark executable. Git preserves +x via .gitattributes-level intent,
# but on fresh clones checked out on Windows side you may need to re-run this.
chmod +x tests/scripts/smoke-witness.cs

# Run from repo root — root global.json pins .NET 10 for the whole repo.
./tests/scripts/smoke-witness.cs             # local mode (default)
./tests/scripts/smoke-witness.cs remote       # pull from GH Packages
./tests/scripts/smoke-witness.cs ci-sim       # mini CI simulation
```

**Windows (shebang is Unix-only at the OS level — use `dotnet run --file`):**

```pwsh
# Run from repo root. The --file flag is required because the repo root
# contains project files (build/_build/Build.csproj, Janset.SDL2.sln).
dotnet run --file tests/scripts/smoke-witness.cs           # local mode
dotnet run --file tests/scripts/smoke-witness.cs remote    # pull from GH Packages
dotnet run --file tests/scripts/smoke-witness.cs ci-sim    # mini CI simulation
```

The script resolves the repo root internally with `git rev-parse --show-toplevel`.
Both the shebang form (`./tests/scripts/smoke-witness.cs`) and the `dotnet run`
form work from the repo root — the root `global.json` now pins .NET 10.0.203 for
the whole repository, so there is no longer a need to `cd` into `tests/scripts/`
to pick up a directory-scoped SDK pin.

### Requirements

- **.NET 10 SDK** (`10.0.203` or later). File-based apps + the `#:package` /
  `#:property` directives are .NET 10 features; the repo-root `global.json`
  pins the SDK selection for the whole repository.
- **git** on `PATH` — used to resolve repo root + capture the short HEAD SHA
  into the summary panel.
- **vcpkg prerequisites** (only for `local` and `ci-sim`) — the same set the main
  build needs (`EnsureVcpkgDependencies` is invoked as a stage). See
  [`docs/playbook/cross-platform-smoke-validation.md`][playbook]. `remote` mode
  skips vcpkg entirely.
- **CMake + C/C++ toolchain** — only when `ci-sim` reaches `NativeSmoke`.
  `local` mode skips `NativeSmoke` entirely (by design — ADR-003 §6.4
  amendment: `SetupLocalDev` no longer composes `NativeSmoke`); `remote` mode
  has no native compilation step at all.
  - **Windows:** plain PowerShell works. The Cake host self-sources the MSVC
    environment via VSWhere + `vcvarsall.bat` (see `IMsvcDevEnvironment` in
    `build/_build/Infrastructure/Tools/Msvc/`, Slice CA). You need Visual
    Studio Build Tools 2022+ with the *Desktop development with C++* workload
    installed, but you do **not** need a Developer PowerShell for VS 2022;
    a regular shell is enough.
  - **Unix:** gcc / clang expected on `$PATH` via the usual package managers
    (`apt install build-essential`, `brew install cmake ninja`, …). The
    `IMsvcDevEnvironment` resolver is Windows-only and short-circuited at the
    `NativeSmokeTaskRunner` call site.
- **`GH_TOKEN` env var** (only for `remote` mode) — Classic PAT with
  `read:packages` scope. GitHub Packages NuGet feed always requires
  authentication, even for public packages (anonymous read is not supported
  on the NuGet/npm/Maven registries — only `ghcr.io` containers allow it).
  Setup recipes in [`docs/playbook/local-development.md`](../../docs/playbook/local-development.md#github-packages-auth---sourceremote).
  The resolver reads `GH_TOKEN`/`GITHUB_TOKEN` directly from the process
  environment; a stale or invalid stored `gh` CLI token is irrelevant when the
  env var is set.

### How `remote` mode threads versions into `PackageConsumerSmoke`

`SetupLocalDev --source=remote` discovers the latest published version per concrete family from `https://nuget.pkg.github.com/janset2d/index.json`, downloads managed + native nupkg pairs into `artifacts/packages/`, and writes both `Janset.Local.props` (consumer-side MSBuild override) and `artifacts/resolve-versions/versions.json` (Cake-pipeline-side mapping). The witness then runs `PackageConsumerSmoke --rid <host> --versions-file artifacts/resolve-versions/versions.json` exactly the same way `local` and `ci-sim` modes do — uniform `--versions-file` flag across all three witness paths.

If the feed has no published version for a family (or family-version invariant is violated — e.g. managed and native disagree), the resolver fails loud with operator-friendly remediation instead of silently producing an inconsistent feed.

### How `ci-sim` threads versions into `PackageConsumerSmoke`

Post-Slice-C, `PackageConsumerSmoke` takes its feed path and version mapping
from the stage request record — `--explicit-version family=semver` (repeated
per family) plus the default `artifacts/packages/` feed path. `ci-sim` reads
`artifacts/resolve-versions/versions.json` emitted by `ResolveVersions`,
filters to concrete families (those with both `managed_project` and
`native_project` in `build/manifest.json`), and passes `--explicit-version`
into the `PackageConsumerSmoke` step. No `Janset.Local.props` injection, no
local-dev-only helpers — the step is a faithful CI job simulation.

`PackageConsumerSmoke` auto-skips with a log hint when no `--explicit-version`
mapping is supplied (mirrors `PackageTask.ShouldRun` — same rationale).

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
- **`Failed to retrieve information about 'Janset.SDL2.Core' from remote source`**
  on `remote` mode → almost always auth scope. Inspect
  `02-SetupLocalDev--remote-.log`; if the underlying HTTP error is hidden, set
  `GH_TOKEN` to a freshly-created Classic PAT with `read:packages` scope.
  Token created without that scope returns a misleading "could not retrieve"
  rather than an explicit 401/403.
- **Shebang fails on Unix with "bad interpreter"** → the checkout picked up
  CRLF. `dos2unix tests/scripts/smoke-witness.cs` or re-clone after
  configuring `core.autocrlf=false`; `.gitattributes` pins LF for the
  scripts directory.
- **Shebang from repo root fails with a `dotnet-./tests/...` lookup** → this was a
  .NET 9 directory-scoped SDK artifact; resolved in .NET 10 with the unified root
  `global.json`. If you still see it, verify `dotnet --version` reports 10.0.203+.
  Fallback: `dotnet run --file tests/scripts/smoke-witness.cs <mode>` works on all platforms.
- **macOS skips `net462` with `mono binary not found in $PATH`** → expected on
  hosts without Mono. Install `brew install mono` if you need the macOS
  `net462` runtime slice; `net9.0` and `net8.0` still execute normally.
- **`Access to the path 'Microsoft.Testing.Platform.dll' is denied`** during
  `01-CleanArtifacts.log` (or any other early step) → Windows-only
  lingering-`testhost.exe` flake. The TUnit / Microsoft Testing Platform
  testhost stays alive in the per-user 10-minute build-server reuse window
  and holds an open file handle on `Microsoft.Testing.Platform.dll` from a
  prior `dotnet test` run. CleanArtifacts then trips on the locked file.
  Mitigation:

  ```pwsh
  dotnet build-server shutdown
  Get-Process dotnet, testhost -ErrorAction SilentlyContinue |
      Where-Object { ((Get-Date) - $_.StartTime).TotalMinutes -gt 1 -and $_.Id -ne $PID } |
      Stop-Process -Force -ErrorAction SilentlyContinue
  Start-Sleep -Seconds 3
  dotnet run --file tests/scripts/smoke-witness.cs local
  ```

  This is the same playbook entry as
  [`docs/playbook/cross-platform-smoke-validation.md` Lingering dotnet processes mitigation](../../docs/playbook/cross-platform-smoke-validation.md#lingering-dotnet-processes-mitigation),
  reproduced here for witness-loop ergonomics. Side-effect warning: the
  shutdown is per-user, so it terminates concurrent CLI build servers in
  other shells (their next build re-spawns; no work is lost).

## `verify-baselines.cs`

Operationalizes the [phase-x ADR-004 plan §2.1.5](../../docs/phases/phase-x-build-host-modernization-2026-05-02.md#215-loop-cadence--fast-vs-milestone) fast/milestone loop cadence and the [§12.3](../../docs/phases/phase-x-build-host-modernization-2026-05-02.md#123-pre-merge-checks) pre-merge checks. Runs `smoke-witness.cs` for each loop entry, emits a temp baseline, deserializes both the emitted and committed baselines via the same `JsonSerializer` options, and compares logical tuples (mode + host_rid + ordered `(label, exit)` step list + passed/failed). Mismatches exit non-zero — wave-rejection signal.

### Loops

| Loop | Entries | Cadence |
| --- | --- | --- |
| **Fast** (default) | `local` mode, host-matched (e.g. `smoke-witness-local-win-x64.json` on Windows) | Every wave commit boundary (developer's pre-merge ritual) |
| **Milestone** (`--milestone`) | Fast loop + Linux local (`linux-x64`) + macOS Intel local (`osx-x64`, opt-in) + Windows ci-sim (`win-x64`) — runtime gate skips entries this host cannot reproduce | P-wave close commits only (P0/P1/P2/P3/P4/P5 close) |

Cross-host verification is intentionally meaningless: a Windows host running `--milestone` will skip Linux and macOS entries with `SKIP (host)` instead of pretending to verify them. The intent is "what could *this* host produce?", not "which baselines does it own?".

### Run

```pwsh
# Run from repo root. --file flag required (repo root has project files).

# Fast loop — host-matched local baseline only
dotnet run --file tests/scripts/verify-baselines.cs

# Milestone loop — fast + every other-mode baseline this host can reproduce
dotnet run --file tests/scripts/verify-baselines.cs --milestone

# Debug: keep emitted temp baselines instead of cleaning them up
dotnet run --file tests/scripts/verify-baselines.cs --keep-tmp
```

```bash
# Unix shell form (Linux / macOS) — once chmod +x is applied
./tests/scripts/verify-baselines.cs
./tests/scripts/verify-baselines.cs --milestone
```

### Exit codes

- `0` — every non-skipped entry matched (skipped entries from missing baseline files or host-RID mismatch are tolerated, reported in the summary panel).
- `1` — at least one entry mismatched the committed baseline OR a smoke-witness spawn failed.

### Status semantics

| Status | Meaning |
| --- | --- |
| `MATCH` | Emitted JSON deserialized identical to committed baseline. Green. |
| `MISMATCH` | Logical tuple drift — step labels differ, exit codes differ, step count differs, mode/host_rid drifted, or passed/failed counters disagree. Detail column lists every individual problem. |
| `SKIP (host)` | This host cannot reproduce the entry's target RID (e.g. running on `win-x64`, entry targets `linux-x64`). Tolerated; not gating. |
| `SKIP (no baseline)` | Baseline file is not committed yet for this entry. Tolerated; emit a milestone-loop baseline first to lift the skip. |
| `SPAWN FAIL` | smoke-witness exited non-zero or did not emit a baseline file. Detail column carries the spawn exit code. |

### Design notes

- **No bash / PowerShell helper.** `verify-baselines.cs` is a file-based app for the same reason `smoke-witness.cs` is — one shell-flavor (none), one runtime (.NET 10 SDK pinned by repo-root `global.json`), cross-platform by construction. See [Microsoft Learn — File-based apps][msdocs-file-based-apps].
- **Spawn pattern matches smoke-witness.** Each entry spawns `dotnet run smoke-witness.cs <mode> --emit-baseline <tmp>` from this directory. Drains stdout/stderr to prevent deadlock; smoke-witness's own `.logs/witness/...` files retain the per-step log output for inspection if a step fails (now persisted in silent mode too — see the smoke-witness section above).
- **Logical equality, not byte equality.** Comparing files byte-for-byte is fragile across CRLF/LF or indentation drift. Comparing deserialized records with the same `JsonSerializerOptions` makes the gate correctness-shaped, not formatting-shaped.
- **Fast-loop / milestone-loop dedup.** `BuildEntries` walks the milestone-loop catalogue (`local linux-x64`, `local osx-x64`, `ci-sim win-x64`) and skips any entry whose `(mode, target_rid)` already matches the host-matched fast-loop slot. Without this, running `--milestone` on Linux double-runs `smoke-witness-local-linux-x64.json` (once as fast loop, once as milestone catalogue entry). Cosmetic but observable — fixed in the P2-warmup step (phase-x §14 Adım 1).
- **Per-host gating semantics.** A Windows host running `--milestone` will see Linux + macOS entries reported as `SKIP (host)` because cross-host verification is meaningless (a Windows host cannot reproduce a Linux byte-equal signal). The intent is "what could *this* host produce?", not "which baselines does it own?". A clean Windows `--milestone` therefore reports `MATCH (local win-x64)` + `SKIP (host)` × 2 (Linux + macOS) + `MATCH (ci-sim win-x64)`. Linux equivalent reports `MATCH (local linux-x64)` + `SKIP (host)` × 3.

[msdocs-file-based-apps]: https://learn.microsoft.com/en-us/dotnet/core/sdk/file-based-apps
[playbook]: ../../docs/playbook/cross-platform-smoke-validation.md
