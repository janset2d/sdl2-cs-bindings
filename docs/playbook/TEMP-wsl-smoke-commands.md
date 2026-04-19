# Temporary WSL Smoke Commands

Temporary runbook for the Linux witness after commit `870beb3`.

Use this from the WSL clone of the repo, not from PowerShell. The commands assume you are at the WSL repo root after `cd /path/to/sdl2-cs-bindings`.

If a command fails, stop there and send me:

- the failing command
- the matching log file from `artifacts/test-results/smoke/wsl-<timestamp>/`
- the last 80 lines of that log

## 0. Enter Repo And Set Environment

Adjust the `cd` line if your WSL clone lives somewhere else.

```bash
cd /path/to/sdl2-cs-bindings

export PATH="$HOME/.dotnet:$PATH"
export DOTNET_ROOT="$HOME/.dotnet"
export DOTNET_CLI_TELEMETRY_OPTOUT=1

git pull --ff-only
git log --oneline -1
```

Expected HEAD: `870beb3`

## 1. Optional Tool Sanity

Only use this block if you are not sure the machine already has the required Linux build tools.

```bash
command -v dotnet
command -v cmake
command -v ninja || true
command -v pkg-config
command -v ldd
```

If one of the essentials is missing, install the basic toolchain first:

```bash
sudo apt update
sudo apt install -y build-essential cmake ninja-build pkg-config tar unzip zip
```

## 2. Clean Previous Artifacts And Create Log Folder

```bash
rm -rf artifacts/harvest_output
rm -rf artifacts/packages
rm -rf artifacts/package-consumer-smoke
rm -rf artifacts/test-results/smoke
rm -rf tests/smoke-tests/native-smoke/build/linux-x64
rm -rf artifacts/temp/ldd/linux-x64

RUN_ID="$(date -u +%Y%m%dT%H%M%SZ)"
LOG_DIR="$PWD/artifacts/test-results/smoke/wsl-$RUN_ID"
mkdir -p "$LOG_DIR"

{
  echo "RUN_ID=$RUN_ID"
  echo "PWD=$PWD"
  date -u
  uname -a
  git log --oneline -1
  dotnet --info
} | tee "$LOG_DIR/00-environment.log"
```

## 3. Build-Host Validation

```bash
dotnet restore build/_build.Tests/Build.Tests.csproj --use-lock-file | tee "$LOG_DIR/10-restore-build-tests.log"
dotnet test build/_build.Tests/Build.Tests.csproj --no-restore | tee "$LOG_DIR/11-build-host-tests.log"

dotnet restore build/_build/Build.csproj --use-lock-file | tee "$LOG_DIR/12-restore-build-host.log"
dotnet build build/_build/Build.csproj --configuration Release --no-restore | tee "$LOG_DIR/13-build-host-release.log"
dotnet run --project build/_build/Build.csproj -c Release -- --tree | tee "$LOG_DIR/14-cake-tree.log"

dotnet run --project build/_build/Build.csproj -c Release -- --target PreFlightCheck --rid linux-x64 --repo-root "$PWD" | tee "$LOG_DIR/15-preflight.log"
dotnet run --project build/_build/Build.csproj -c Release -- --target EnsureVcpkgDependencies --rid linux-x64 --repo-root "$PWD" | tee "$LOG_DIR/16-ensure-vcpkg.log"
```

## 4. Harvest And Consolidate

```bash
./build/_build/bin/Release/net9.0/Build --target Harvest --library SDL2 --library SDL2_image --library SDL2_mixer --library SDL2_ttf --library SDL2_gfx --library SDL2_net --rid linux-x64 --repo-root "$PWD" | tee "$LOG_DIR/20-harvest.log"

./build/_build/bin/Release/net9.0/Build --target ConsolidateHarvest --repo-root "$PWD" | tee "$LOG_DIR/21-consolidate-harvest.log"
```

## 5. Manual `ldd` Inspection Of Harvested Artifacts

This spot-check should show SDL satellites depending on the SDL core shared library plus system libs, not stray third-party codec `.so` files.

```bash
inspect_ldd() {
  local lib="$1"
  local pattern="$2"
  local dest="$PWD/artifacts/temp/ldd/linux-x64/$lib"
  rm -rf "$dest"
  mkdir -p "$dest"
  tar -xzf "artifacts/harvest_output/$lib/runtimes/linux-x64/native/native.tar.gz" -C "$dest"

  echo "===== $lib =====" | tee -a "$LOG_DIR/30-ldd.log"
  find "$dest" -printf '%y %p -> %l\n' | sort | tee -a "$LOG_DIR/30-ldd.log"

  local candidate
  candidate="$(find "$dest" -name "$pattern" | sort | head -n 1)"
  echo "candidate=$candidate" | tee -a "$LOG_DIR/30-ldd.log"
  ldd "$candidate" | tee -a "$LOG_DIR/30-ldd.log"
  echo | tee -a "$LOG_DIR/30-ldd.log"
}

inspect_ldd SDL2 'libSDL2*.so*'
inspect_ldd SDL2_image 'libSDL2_image*.so*'
inspect_ldd SDL2_mixer 'libSDL2_mixer*.so*'
inspect_ldd SDL2_ttf 'libSDL2_ttf*.so*'
inspect_ldd SDL2_gfx 'libSDL2_gfx*.so*'
inspect_ldd SDL2_net 'libSDL2_net*.so*'
```

## 6. Native Smoke (C++)

```bash
pushd tests/smoke-tests/native-smoke
cmake --preset linux-x64 | tee "$LOG_DIR/40-native-smoke-configure.log"
cmake --build build/linux-x64 | tee "$LOG_DIR/41-native-smoke-build.log"
./build/linux-x64/native-smoke | tee "$LOG_DIR/42-native-smoke-run.log"
popd
```

## 7. Local Package Feed Bootstrap

Use `SetupLocalDev` here, not a manual multi-family `Package` call. The repo's D-3seg rules make a single shared `--family-version` invalid across `Core/Image/Mixer/Ttf/Gfx`.

```bash
dotnet run --project build/_build/Build.csproj -c Release -- --target SetupLocalDev --source=local --rid linux-x64 --repo-root "$PWD" | tee "$LOG_DIR/50-setup-local-dev.log"

cat build/msbuild/Janset.Smoke.local.props | tee "$LOG_DIR/51-smoke-local-props.log"
ls -1 artifacts/packages | tee "$LOG_DIR/52-packages.log"
```

## 8. Managed Consumer Validation

Linux note: `net462` runtime smoke is intentionally skipped on Linux in the repo playbook because Mono 6.12 cannot host TUnit + Microsoft Testing Platform discovery. The solution build still covers the `net462` compile surface.

```bash
dotnet build Janset.SDL2.sln -c Release | tee "$LOG_DIR/60-solution-build.log"

dotnet build-server shutdown | tee "$LOG_DIR/61-build-server-shutdown-before-compile.log"
dotnet build tests/smoke-tests/package-smoke/Compile.NetStandard/Compile.NetStandard.csproj -c Release --disable-build-servers -p:UseSharedCompilation=false -nodeReuse:false | tee "$LOG_DIR/62-compile-netstandard.log"

dotnet build-server shutdown | tee "$LOG_DIR/63-build-server-shutdown-before-net9.log"
dotnet test tests/smoke-tests/package-smoke/PackageConsumer.Smoke/PackageConsumer.Smoke.csproj -c Release -f net9.0 -r linux-x64 --disable-build-servers -p:UseSharedCompilation=false -nodeReuse:false | tee "$LOG_DIR/64-package-smoke-net9.log"

dotnet build-server shutdown | tee "$LOG_DIR/65-build-server-shutdown-before-net8.log"
dotnet test tests/smoke-tests/package-smoke/PackageConsumer.Smoke/PackageConsumer.Smoke.csproj -c Release -f net8.0 -r linux-x64 --disable-build-servers -p:UseSharedCompilation=false -nodeReuse:false | tee "$LOG_DIR/66-package-smoke-net8.log"

printf '%s\n' 'net462 runtime smoke is intentionally skipped on Linux per docs/playbook/cross-platform-smoke-validation.md.' \
  | tee "$LOG_DIR/67-net462-skip-note.log"
```

## 9. Unix Symlink Output Check In Consumer Bins

This is extra evidence for the Unix package path. The package smoke tests should already catch this, but the file listing is useful when debugging extraction issues.

```bash
find tests/smoke-tests/package-smoke/PackageConsumer.Smoke/bin/Release/net9.0/linux-x64 -printf '%y %p -> %l\n' | grep 'libSDL2' | sort | tee "$LOG_DIR/70-net9-symlinks.log"

find tests/smoke-tests/package-smoke/PackageConsumer.Smoke/bin/Release/net8.0/linux-x64 -printf '%y %p -> %l\n' | grep 'libSDL2' | sort | tee "$LOG_DIR/71-net8-symlinks.log"
```

## 10. Quick Summary To Send Back

Run this last and send me the summary plus any failing full log.

```bash
{
  echo '===== QUICK SUMMARY ====='
  echo "HEAD: $(git rev-parse --short HEAD)"
  echo
  echo '--- build host tests ---'
  grep -E 'Test summary|failed:|succeeded:' "$LOG_DIR/11-build-host-tests.log" || true
  echo
  echo '--- native smoke ---'
  grep -E 'Passed:|Failed:|Result:' "$LOG_DIR/42-native-smoke-run.log" || true
  echo
  echo '--- package smoke net9 ---'
  grep -E 'Test summary|failed:|succeeded:' "$LOG_DIR/64-package-smoke-net9.log" || true
  echo
  echo '--- package smoke net8 ---'
  grep -E 'Test summary|failed:|succeeded:' "$LOG_DIR/66-package-smoke-net8.log" || true
} | tee "$LOG_DIR/99-quick-summary.log"
```

## 11. What To Send Me Back

At minimum, send me these files or their contents:

- `artifacts/test-results/smoke/wsl-<timestamp>/15-preflight.log`
- `artifacts/test-results/smoke/wsl-<timestamp>/20-harvest.log`
- `artifacts/test-results/smoke/wsl-<timestamp>/30-ldd.log`
- `artifacts/test-results/smoke/wsl-<timestamp>/42-native-smoke-run.log`
- `artifacts/test-results/smoke/wsl-<timestamp>/50-setup-local-dev.log`
- `artifacts/test-results/smoke/wsl-<timestamp>/62-compile-netstandard.log`
- `artifacts/test-results/smoke/wsl-<timestamp>/64-package-smoke-net9.log`
- `artifacts/test-results/smoke/wsl-<timestamp>/66-package-smoke-net8.log`
- `artifacts/test-results/smoke/wsl-<timestamp>/99-quick-summary.log`

If anything fails before the end, just send the failing log and the last successful step.