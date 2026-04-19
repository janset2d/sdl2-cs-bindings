# Temporary macOS Smoke Commands

Temporary runbook for the macOS Intel witness after commit `870beb3`.

This runbook targets the repo's current validated macOS proof host shape: `osx-x64` on Intel macOS. If the machine is Apple Silicon, stop and tell me before running this because that is a different RID/triplet path.

Use this from the macOS clone of the repo, usually over SSH. The commands assume you are at the macOS repo root after `cd /Users/armut/repos/sdl2-cs-bindings`.

If a command fails, stop there and send me:

- the failing command
- the matching log file from `artifacts/test-results/smoke/macos-<timestamp>/`
- the last 80 lines of that log

## 0. Enter Repo And Set Environment

Adjust the `cd` line if the macOS clone lives somewhere else.

```bash
cd /Users/armut/repos/sdl2-cs-bindings

export PATH="/usr/local/share/dotnet:/usr/local/bin:$PATH"
export DOTNET_ROOT="/usr/local/share/dotnet"
export DOTNET_CLI_TELEMETRY_OPTOUT=1

git pull --ff-only
git log --oneline -1
```

Expected HEAD: `870beb3`

## 1. Optional Tool Sanity

Only use this block if you are not sure the machine already has the required macOS build tools.

```bash
command -v dotnet
command -v cmake
command -v ninja || true
command -v brew
command -v otool
command -v mono
xcode-select -p
```

If something essential is missing, fix the host first:

```bash
xcode-select --install || true
brew install cmake ninja autoconf automake libtool pkg-config mono
```

## 2. Clean Previous Artifacts And Create Log Folder

```bash
rm -rf artifacts/harvest_output
rm -rf artifacts/packages
rm -rf artifacts/package-consumer-smoke
rm -rf artifacts/test-results/smoke
rm -rf tests/smoke-tests/native-smoke/build/osx-x64
rm -rf artifacts/temp/otool/osx-x64

RUN_ID="$(date -u +%Y%m%dT%H%M%SZ)"
LOG_DIR="$PWD/artifacts/test-results/smoke/macos-$RUN_ID"
mkdir -p "$LOG_DIR"

{
  echo "RUN_ID=$RUN_ID"
  echo "PWD=$PWD"
  date -u
  uname -a
  sw_vers
  git log --oneline -1
  dotnet --info
  mono --version
} | tee "$LOG_DIR/00-environment.log"
```

## 3. Build-Host Validation

```bash
dotnet restore build/_build.Tests/Build.Tests.csproj --use-lock-file | tee "$LOG_DIR/10-restore-build-tests.log"
dotnet test build/_build.Tests/Build.Tests.csproj --no-restore | tee "$LOG_DIR/11-build-host-tests.log"

dotnet restore build/_build/Build.csproj --use-lock-file | tee "$LOG_DIR/12-restore-build-host.log"
dotnet build build/_build/Build.csproj --configuration Release --no-restore | tee "$LOG_DIR/13-build-host-release.log"
dotnet run --project build/_build/Build.csproj -c Release -- --tree | tee "$LOG_DIR/14-cake-tree.log"

dotnet run --project build/_build/Build.csproj -c Release -- --target PreFlightCheck --rid osx-x64 --repo-root "$PWD" | tee "$LOG_DIR/15-preflight.log"
dotnet run --project build/_build/Build.csproj -c Release -- --target EnsureVcpkgDependencies --rid osx-x64 --repo-root "$PWD" | tee "$LOG_DIR/16-ensure-vcpkg.log"
```

## 4. Harvest And Consolidate

```bash
./build/_build/bin/Release/net9.0/Build --target Harvest --library SDL2 --library SDL2_image --library SDL2_mixer --library SDL2_ttf --library SDL2_gfx --library SDL2_net --rid osx-x64 --repo-root "$PWD" | tee "$LOG_DIR/20-harvest.log"

./build/_build/bin/Release/net9.0/Build --target ConsolidateHarvest --repo-root "$PWD" | tee "$LOG_DIR/21-consolidate-harvest.log"
```

## 5. Manual `otool -L` Inspection Of Harvested Artifacts

This spot-check should show SDL satellites depending on the SDL core shared library plus Apple/system frameworks, not stray third-party dylibs that should have been baked in.

```bash
inspect_otool() {
  local lib="$1"
  local pattern="$2"
  local dest="$PWD/artifacts/temp/otool/osx-x64/$lib"
  rm -rf "$dest"
  mkdir -p "$dest"
  tar -xzf "artifacts/harvest_output/$lib/runtimes/osx-x64/native/native.tar.gz" -C "$dest"

  echo "===== $lib =====" | tee -a "$LOG_DIR/30-otool.log"
  find "$dest" -print | sort | tee -a "$LOG_DIR/30-otool.log"

  local candidate
  candidate="$(find "$dest" -name "$pattern" | sort | head -n 1)"
  echo "candidate=$candidate" | tee -a "$LOG_DIR/30-otool.log"
  otool -L "$candidate" | tee -a "$LOG_DIR/30-otool.log"
  echo | tee -a "$LOG_DIR/30-otool.log"
}

inspect_otool SDL2 'libSDL2*.dylib'
inspect_otool SDL2_image 'libSDL2_image*.dylib'
inspect_otool SDL2_mixer 'libSDL2_mixer*.dylib'
inspect_otool SDL2_ttf 'libSDL2_ttf*.dylib'
inspect_otool SDL2_gfx 'libSDL2_gfx*.dylib'
inspect_otool SDL2_net 'libSDL2_net*.dylib'
```

## 6. Native Smoke (C++)

```bash
pushd tests/smoke-tests/native-smoke
cmake --preset osx-x64 | tee "$LOG_DIR/40-native-smoke-configure.log"
cmake --build build/osx-x64 | tee "$LOG_DIR/41-native-smoke-build.log"
./build/osx-x64/native-smoke | tee "$LOG_DIR/42-native-smoke-run.log"
popd
```

## 7. Local Package Feed Bootstrap

Use `SetupLocalDev` here, not a manual multi-family `Package` call. The repo's D-3seg rules make a single shared `--family-version` invalid across `Core/Image/Mixer/Ttf/Gfx`.

```bash
dotnet run --project build/_build/Build.csproj -c Release -- --target SetupLocalDev --source=local --rid osx-x64 --repo-root "$PWD" | tee "$LOG_DIR/50-setup-local-dev.log"

cat build/msbuild/Janset.Smoke.local.props | tee "$LOG_DIR/51-smoke-local-props.log"
ls -1 artifacts/packages | tee "$LOG_DIR/52-packages.log"
```

## 8. Managed Consumer Validation

macOS note: `net462` package smoke only runs if a `mono` binary is present in `$PATH` — macOS has no built-in .NET Framework runtime. Install classic Mono via `brew install mono` or the [mono-project.com MDK pkg](https://www.mono-project.com/download/stable/) to enable the runtime slice. Without Mono the per-TFM runner auto-skips net462 with a `Skipping package-smoke for TFM 'net462'` warning (same mechanism as the Linux skip), so this block is safe to run either way — it will just report one fewer TFM covered.

```bash
dotnet build Janset.SDL2.sln -c Release | tee "$LOG_DIR/60-solution-build.log"

dotnet build-server shutdown | tee "$LOG_DIR/61-build-server-shutdown-before-compile.log"
dotnet build tests/smoke-tests/package-smoke/Compile.NetStandard/Compile.NetStandard.csproj -c Release --disable-build-servers -p:UseSharedCompilation=false -nodeReuse:false | tee "$LOG_DIR/62-compile-netstandard.log"

dotnet build-server shutdown | tee "$LOG_DIR/63-build-server-shutdown-before-net9.log"
dotnet test tests/smoke-tests/package-smoke/PackageConsumer.Smoke/PackageConsumer.Smoke.csproj -c Release -f net9.0 -r osx-x64 --disable-build-servers -p:UseSharedCompilation=false -nodeReuse:false | tee "$LOG_DIR/64-package-smoke-net9.log"

dotnet build-server shutdown | tee "$LOG_DIR/65-build-server-shutdown-before-net8.log"
dotnet test tests/smoke-tests/package-smoke/PackageConsumer.Smoke/PackageConsumer.Smoke.csproj -c Release -f net8.0 -r osx-x64 --disable-build-servers -p:UseSharedCompilation=false -nodeReuse:false | tee "$LOG_DIR/66-package-smoke-net8.log"

dotnet build-server shutdown | tee "$LOG_DIR/67-build-server-shutdown-before-net462.log"
dotnet test tests/smoke-tests/package-smoke/PackageConsumer.Smoke/PackageConsumer.Smoke.csproj -c Release -f net462 -r osx-x64 --disable-build-servers -p:UseSharedCompilation=false -nodeReuse:false | tee "$LOG_DIR/68-package-smoke-net462.log"
```

## 9. Unix Symlink Output Check In Consumer Bins

This is extra evidence for the Unix package path. The package smoke tests should already catch this, but the file listing is useful when debugging extraction issues.

```bash
find tests/smoke-tests/package-smoke/PackageConsumer.Smoke/bin/Release/net9.0/osx-x64 -print | grep 'libSDL2' | sort | tee "$LOG_DIR/70-net9-symlinks.log"

find tests/smoke-tests/package-smoke/PackageConsumer.Smoke/bin/Release/net8.0/osx-x64 -print | grep 'libSDL2' | sort | tee "$LOG_DIR/71-net8-symlinks.log"

find tests/smoke-tests/package-smoke/PackageConsumer.Smoke/bin/Release/net462/osx-x64 -print | grep 'libSDL2' | sort | tee "$LOG_DIR/72-net462-symlinks.log"
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
  echo
  echo '--- package smoke net462 ---'
  grep -E 'Test summary|failed:|succeeded:' "$LOG_DIR/68-package-smoke-net462.log" || true
} | tee "$LOG_DIR/99-quick-summary.log"
```

## 11. What To Send Me Back

At minimum, send me these files or their contents:

- `artifacts/test-results/smoke/macos-<timestamp>/15-preflight.log`
- `artifacts/test-results/smoke/macos-<timestamp>/20-harvest.log`
- `artifacts/test-results/smoke/macos-<timestamp>/30-otool.log`
- `artifacts/test-results/smoke/macos-<timestamp>/42-native-smoke-run.log`
- `artifacts/test-results/smoke/macos-<timestamp>/50-setup-local-dev.log`
- `artifacts/test-results/smoke/macos-<timestamp>/62-compile-netstandard.log`
- `artifacts/test-results/smoke/macos-<timestamp>/64-package-smoke-net9.log`
- `artifacts/test-results/smoke/macos-<timestamp>/66-package-smoke-net8.log`
- `artifacts/test-results/smoke/macos-<timestamp>/68-package-smoke-net462.log`
- `artifacts/test-results/smoke/macos-<timestamp>/99-quick-summary.log`

If anything fails before the end, just send the failing log and the last successful step.