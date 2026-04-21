# Unix Smoke Runbook — Linux + macOS witness

> Operational step-by-step witness runbook for a single Unix host (WSL for
> `linux-x64`, Intel macOS over SSH for `osx-x64`). Platform differences are
> parameterised via `$PLATFORM` / `$RID` / `$PRESET` env vars declared in §1.
>
> This is the **companion** to [`cross-platform-smoke-validation.md`](cross-platform-smoke-validation.md) — that file is the reference playbook (how to
> verify, A–K matrix, scope rationale); this file is the concrete witness
> script (copy-paste commands, tee logs, send them back).
>
> Supersedes `TEMP-wsl-smoke-commands.md` + `TEMP-macos-smoke-commands.md`
> (retired 2026-04-21 during Slice DA Cake-first alignment).

## Cake-first rule

Every command in §2 onward invokes a Cake target via
`dotnet run --project build/_build/Build.csproj …` or the compiled
`build/_build/bin/Release/net9.0/Build` entrypoint.

The **one allowed exception** is §3 (build-host bootstrap). The Cake host
cannot run its own unit tests or compile its own csproj from inside Cake —
that would be Cake-over-Cake, a chicken-and-egg loop. `dotnet restore` / `dotnet
test` / `dotnet build` against `build/_build.Tests/*.csproj` + `build/_build/Build.csproj`
are the pre-Cake bootstrap.

If you find yourself reaching for `rm -rf artifacts/…`, raw `tar -xzf`, raw
`cmake --preset` outside §3, or `dotnet test` on a smoke csproj — stop. That
work already has a Cake target; see `cross-platform-smoke-validation.md`
§Command Reference for the mapping.

## 0. Per-platform environment setup

Do this once per session before anything else. The non-interactive SSH shell
(macOS) and WSL default shell do not source `~/.zprofile` / `~/.bashrc`, so
`dotnet` and tooling PATHs must be exported explicitly.

### Linux (WSL)

```bash
cd /path/to/sdl2-cs-bindings

export PATH="$HOME/.dotnet:$PATH"
export DOTNET_ROOT="$HOME/.dotnet"
export DOTNET_CLI_TELEMETRY_OPTOUT=1

git pull --ff-only
git log --oneline -1
```

Expected HEAD: `<sha>` (tell the requester the current `feat/adr003-impl` tip
before running).

### macOS Intel (SSH)

```bash
cd /Users/armut/repos/sdl2-cs-bindings

export PATH="/usr/local/share/dotnet:/usr/local/bin:$PATH"
export DOTNET_ROOT="/usr/local/share/dotnet"
export DOTNET_CLI_TELEMETRY_OPTOUT=1

git pull --ff-only
git log --oneline -1
```

Expected HEAD: `<sha>` (same as Linux; keep hosts on the same commit).

### Tool sanity (optional — only if unsure)

```bash
command -v dotnet
command -v cmake
command -v ninja || true
command -v pkg-config
# Linux only
command -v ldd 2>/dev/null || true
# macOS only
command -v otool 2>/dev/null || true
command -v mono 2>/dev/null || true
```

Missing bits: see `cross-platform-smoke-validation.md` §Per-Platform Environment
Setup (apt / brew package lists + `freepats` MIDI prereq on Linux).

## 1. Platform variables

Set exactly one of the two blocks:

```bash
# Linux
PLATFORM=linux
RID=linux-x64
PRESET=linux-x64

# macOS Intel
PLATFORM=macos
RID=osx-x64
PRESET=osx-x64
```

> macOS Apple Silicon (`osx-arm64`) is a PA-2 row. Do **not** run this runbook
> there — see `cross-platform-smoke-validation.md` §PA-2 Per-Triplet Witness
> Invocations for the Phase 2b path.

## 2. Clean artifacts + log folder

```bash
# Pre-clean BEFORE the log folder exists, so we wipe any prior run's logs too.
dotnet run --project build/_build/Build.csproj -c Release -- \
  --target CleanArtifacts --repo-root "$PWD"

RUN_ID="$(date -u +%Y%m%dT%H%M%SZ)"
LOG_DIR="$PWD/artifacts/test-results/smoke/$PLATFORM-$RUN_ID"
mkdir -p "$LOG_DIR"

{
  echo "RUN_ID=$RUN_ID"
  echo "PWD=$PWD"
  echo "PLATFORM=$PLATFORM RID=$RID PRESET=$PRESET"
  date -u
  uname -a
  if [ "$PLATFORM" = "macos" ]; then sw_vers; fi
  git log --oneline -1
  dotnet --info
  if [ "$PLATFORM" = "macos" ] && command -v mono >/dev/null; then mono --version; fi
} | tee "$LOG_DIR/00-environment.log"
```

> `CleanArtifacts` wipes `artifacts/{harvest_output,packages,package-consumer-smoke,test-results/smoke,harvest-staging,temp/inspect,matrix}` + `tests/smoke-tests/native-smoke/build/`. `vcpkg_installed/` is intentionally preserved — cold rebuild is expensive.

## 3. Build-host bootstrap (pre-Cake exception)

These four invocations are the **only** non-Cake commands allowed in this
runbook. Cake-over-Cake is a chicken-and-egg loop.

```bash
dotnet restore build/_build.Tests/Build.Tests.csproj --use-lock-file \
  | tee "$LOG_DIR/10-restore-build-tests.log"

dotnet test build/_build.Tests/Build.Tests.csproj --no-restore \
  | tee "$LOG_DIR/11-build-host-tests.log"

dotnet restore build/_build/Build.csproj --use-lock-file \
  | tee "$LOG_DIR/12-restore-build-host.log"

dotnet build build/_build/Build.csproj --configuration Release --no-restore \
  | tee "$LOG_DIR/13-build-host-release.log"
```

Expected test count: 390/390 green (as of Slice D WIP `1ef3a82` — update
as new slices land).

## 4. Cake pipeline — PreFlight → EnsureVcpkg → Harvest → NativeSmoke → Consolidate

Each target is invoked explicitly for maximum signal isolation (the Cake
`[IsDependentOn]` chain would run them all transitively via a single target,
but per-target logs make triage easier).

```bash
dotnet run --project build/_build/Build.csproj -c Release -- --tree \
  | tee "$LOG_DIR/14-cake-tree.log"

dotnet run --project build/_build/Build.csproj -c Release -- \
  --target PreFlightCheck --rid "$RID" --repo-root "$PWD" \
  | tee "$LOG_DIR/15-preflight.log"

dotnet run --project build/_build/Build.csproj -c Release -- \
  --target EnsureVcpkgDependencies --rid "$RID" --repo-root "$PWD" \
  | tee "$LOG_DIR/16-ensure-vcpkg.log"

./build/_build/bin/Release/net9.0/Build \
  --target Harvest --rid "$RID" --repo-root "$PWD" \
  | tee "$LOG_DIR/20-harvest.log"

./build/_build/bin/Release/net9.0/Build \
  --target NativeSmoke --rid "$RID" --repo-root "$PWD" \
  | tee "$LOG_DIR/25-native-smoke.log"

./build/_build/bin/Release/net9.0/Build \
  --target ConsolidateHarvest --repo-root "$PWD" \
  | tee "$LOG_DIR/30-consolidate.log"
```

**What to look for (per step):**

- **PreFlight**: `6/6 versions`, `7/7 strategies`, `6/6 families × 10/10 csprojs` all green. No `CakeException`.
- **EnsureVcpkg**: bootstrap exits 0 only if `vcpkg` is missing; install uses `$RID`'s triplet + overlay triplets/ports.
- **Harvest**: each library reports `1 primary, 0 runtime` (hybrid-static baseline); Unix emits `native.tar.gz` (symlinks preserved); `rid-status/$RID.json` emitted per library.
- **NativeSmoke**: `Passed: N, Failed: 0, Result: ALL PASS`. Current expanded harness: `28 passed` on Windows; Linux/macOS harness is the same C source + same `CMakePresets.json` entry, so parity is expected.
- **Consolidate**: per-library `harvest-manifest.json` + `harvest-summary.json` + `licenses/_consolidated/` present.

## 5. Cake-first dependency inspection

```bash
./build/_build/bin/Release/net9.0/Build \
  --target Inspect-HarvestedDependencies --rid "$RID" --repo-root "$PWD" \
  | tee "$LOG_DIR/35-inspect-deps.log"
```

**What to look for:**

- Unix RIDs: the runner extracts `native.tar.gz` via the Cake `TarExtract`
  alias into `artifacts/temp/inspect/$RID/<lib>/` preserving SONAME symlinks,
  then invokes `ldd` (Linux) / `otool -L` (macOS) on the resolved primary
  binary per library (pattern-matched against `LibraryManifest.PrimaryBinaries[<os>].Patterns`).
- Per-library log block shows `[lib-name] Primary binary resolved: <path>` +
  the scanner output. SDL2 satellites should depend on the SDL2 core shared
  library + OS/system libraries only; no stray third-party codec `.so` / `.dylib`
  entries.
- If the scanner reports green but a manual `tar -xzf + ldd` on the archive
  disagrees, trust the raw tool. Inspect-HarvestedDependencies is a
  diagnostic; Harvest's internal scanner (during the harvest stage) is the
  one that gates deployment.

## 6. Cake-first local feed + consumer smoke

```bash
dotnet run --project build/_build/Build.csproj -c Release -- \
  --target SetupLocalDev --source=local --rid "$RID" --repo-root "$PWD" \
  | tee "$LOG_DIR/40-setup-local-dev.log"

cat build/msbuild/Janset.Smoke.local.props | tee "$LOG_DIR/41-smoke-local-props.log"
ls -1 artifacts/packages | tee "$LOG_DIR/42-packages.log"

./build/_build/bin/Release/net9.0/Build \
  --target PackageConsumerSmoke --rid "$RID" --repo-root "$PWD" \
  | tee "$LOG_DIR/50-package-consumer-smoke.log"
```

**What to look for:**

- `SetupLocalDev`: composes PreFlight → Harvest → NativeSmoke → Consolidate →
  Package internally via the `LocalArtifactSourceResolver`, writes per-family
  D-3seg versions (`sdl2-core 2.32.0-local.<ts>`, `sdl2-gfx 1.0.0-local.<ts>`,
  `sdl2-image/mixer 2.8.0-local.<ts>`, `sdl2-ttf 2.24.0-local.<ts>`), produces
  15 nupkgs in `artifacts/packages/`, and writes
  `build/msbuild/Janset.Smoke.local.props` referencing them.
- `PackageConsumerSmoke`: internally loops executable TFMs resolved from
  `PackageConsumer.Smoke.csproj`'s `$(ExecutableTargetFrameworks)`. Per-platform
  net462 policy:
  - Linux: net462 runtime slice auto-skipped (`Skipping package-smoke for TFM 'net462'`) — Mono 6.12 cannot host TUnit + Microsoft Testing Platform discovery.
  - macOS: net462 runtime slice runs iff `mono` is on `$PATH`; otherwise auto-skipped (same mechanism). `brew install mono` or the [mono-project.com MDK pkg](https://www.mono-project.com/download/stable/) if you want the slice covered.
- Compile-only netstandard2.0 sanity pass precedes the TFM loop; that one
  runs unconditionally.
- `Failed: 0` per TFM. The TUnit suite covers native asset landing for
  `core/image/mixer/ttf/gfx`, `SDL_Init_Cycle_Succeeds`, PNG fixture load,
  mixer decoder surface, `TTF_Init`, `SDL2_gfx` render, linked-version checks,
  Unix symlink assertion (on NET6+).

## 7. Ancillary — Unix symlink output check

Redundant with the NET6+ symlink assertion baked into the consumer smoke, but
useful when debugging extraction issues:

```bash
find tests/smoke-tests/package-smoke/PackageConsumer.Smoke/bin/Release/net9.0/$RID \
  -print | grep 'libSDL2' | sort | tee "$LOG_DIR/60-net9-symlinks.log"

find tests/smoke-tests/package-smoke/PackageConsumer.Smoke/bin/Release/net8.0/$RID \
  -print | grep 'libSDL2' | sort | tee "$LOG_DIR/61-net8-symlinks.log"

if [ "$PLATFORM" = "macos" ] && command -v mono >/dev/null; then
  find tests/smoke-tests/package-smoke/PackageConsumer.Smoke/bin/Release/net462/$RID \
    -print | grep 'libSDL2' | sort | tee "$LOG_DIR/62-net462-symlinks.log"
fi
```

## 8. Quick summary

```bash
{
  echo '===== QUICK SUMMARY ====='
  echo "HEAD: $(git rev-parse --short HEAD)"
  echo "PLATFORM=$PLATFORM RID=$RID"
  echo
  echo '--- build-host tests ---'
  grep -E 'Test summary|failed:|succeeded:|total:' "$LOG_DIR/11-build-host-tests.log" || true
  echo
  echo '--- preflight last 5 lines ---'
  tail -5 "$LOG_DIR/15-preflight.log" || true
  echo
  echo '--- native-smoke ---'
  grep -E 'Passed:|Failed:|Result:' "$LOG_DIR/25-native-smoke.log" || true
  echo
  echo '--- inspect-deps per-lib counts ---'
  grep -E 'Primary binary resolved|deps\):' "$LOG_DIR/35-inspect-deps.log" || true
  echo
  echo '--- setup-local-dev ---'
  grep -E 'SetupLocalDev completed|Packed family|Failed' "$LOG_DIR/40-setup-local-dev.log" || true
  echo
  echo '--- consumer smoke ---'
  grep -E 'Test summary|failed:|succeeded:|total:|Skipping package-smoke' "$LOG_DIR/50-package-consumer-smoke.log" || true
} | tee "$LOG_DIR/99-quick-summary.log"
```

## 9. What to send back

At minimum, paste the contents of (or upload) these files:

- `$LOG_DIR/00-environment.log`
- `$LOG_DIR/11-build-host-tests.log`
- `$LOG_DIR/15-preflight.log`
- `$LOG_DIR/20-harvest.log`
- `$LOG_DIR/25-native-smoke.log`
- `$LOG_DIR/30-consolidate.log`
- `$LOG_DIR/35-inspect-deps.log`
- `$LOG_DIR/40-setup-local-dev.log`
- `$LOG_DIR/50-package-consumer-smoke.log`
- `$LOG_DIR/99-quick-summary.log`

If anything fails mid-way, send the failing log plus the last successful step
identifier (e.g., "§5 failed, §4 was green"). Do not paper over failures by
skipping steps — flag them, classify per §Failure triage below, and stop.

## Failure triage

When a step fails, classify before reacting:

1. **Environment** — `dotnet` / `cmake` / tool missing, PATH not exported,
   `DOTNET_ROOT` absent, submodule not initialised, `freepats` missing on
   Linux (Mix decoder: MIDI). Fix host, re-run from §0.
2. **Stale repo** — `git log --oneline -1` doesn't match expected HEAD.
   `git pull`, re-run from §2 (CleanArtifacts forward).
3. **Code regression** — same commit, clean environment, still fails. Report
   with platform, step identifier, exact error text, and the failing log
   filename. This is what the runbook exists to catch.

Do not conflate (1) / (2) with (3). The majority of "it failed" reports in
practice are category 1 or 2.
