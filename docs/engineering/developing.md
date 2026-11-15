# Developing and Local Validation

Date: 2026-11-14

[tools.cs](../../tools.cs) owns local orchestration. [Quality gates](quality-gates.md) owns build/test
commands and completion evidence; [workflow](workflow.md#isolated-checkouts) owns checkout restrictions.
Run commands below from the repository root in a provisioned checkout. An isolated managed-only
checkout does not run setup, native builds or harvest merely to validate a managed edit.

## Prerequisites

Install the SDK selected by [global.json](../../global.json), Git with submodule support, and the
platform's C++ build tools. Executables that target an older framework than the SDK's own also need
that framework's runtime at its current patch level; `ExecutableTargetFrameworks` in
[Directory.Build.props](../../Directory.Build.props) owns the list. An IDE is optional.

| Platform | Native prerequisites |
|---|---|
| Windows | Visual Studio C++ Build Tools, including x64/x86 tools and a Windows SDK; ARM64 tools when targeting ARM64 |
| Linux | C/C++ compiler, CMake, pkg-config, ldd and SDL development dependencies; the [builder Dockerfile](../../docker/linux-builder.Dockerfile) owns the provisioned Linux build environment |
| macOS | Xcode Command Line Tools, autoconf, automake and libtool |

On Windows the host locates Visual Studio through VSWhere and sources `vcvarsall.bat` through
`IMsvcDevEnvironment`. Plain PowerShell works. An already configured developer shell takes the
`VCToolsInstallDir` fast path. If dumpbin cannot be found, repair the C++ workload and Visual Studio
Installer, then inspect `VCToolsInstallDir` and its host/target tool directory.

## Fresh clone and package-first setup

```bash
git clone --recursive https://github.com/janset2d/sdl2-cs-bindings.git
cd sdl2-cs-bindings
dotnet run --file tools.cs -- setup --source=local
```

Setup resolves manifest-derived local prerelease versions, runs preflight, installs the host hybrid
triplet, harvests, consolidates and packs. It then writes the local consumer override. It does **not**
run native or consumer smoke; use `ci-sim` for that evidence.

Outputs are `artifacts/packages/`, `artifacts/resolve-versions/versions.json` and the gitignored
`build/msbuild/Janset.Local.props`. The last file supplies the local feed and family versions through
[Janset.Smoke.props](../../build/msbuild/Janset.Smoke.props). IDE and command-line smoke restores use
the same packages. Samples, sandboxes and other consumer projects follow
[ADR-0001](../adr/0001-d3seg-and-package-first.md): no native ProjectReference injection, manual DLL
copying or system SDL substitute for consumer validation.

Setup cleans artifacts by default. `--no-clean` preserves existing artifacts when deliberately
iterating on setup. It does not establish that an old package matches new source.
`CleanArtifacts` is a helper inside tools.cs, not a Cake target.

### Validate a published internal wave

```pwsh
dotnet run --file tools.cs -- setup --source=remote-github
```

This downloads the latest published managed/native pair for every concrete manifest family from
GitHub Packages, without vcpkg, harvest or local packing. It prepares the same local feed and version
outputs as local setup. A managed/native version mismatch is refused, not accepted as a partial wave.
Use this route to test the published package as a consumer. Public-source availability is tracked in
the [roadmap](../ROADMAP.md), which owns current setup limitations.

### Authentication

tools.cs reads `GH_TOKEN`, then `GITHUB_TOKEN`; it does not query the GitHub CLI credential store.
GitHub Packages NuGet needs authentication even for public packages. Use a classic PAT with
`read:packages`; fine-grained PATs are not supported. Authorize SSO if the organization requires it.
Never commit tokens or paste them into logs. A private shell/session environment or credential
manager can provide the token. For an already authorized GitHub CLI session:

```pwsh
$env:GH_TOKEN = gh auth token
dotnet run --file tools.cs -- setup --source=remote-github
```

```bash
export GH_TOKEN="$(gh auth token)"
dotnet run --file tools.cs -- setup --source=remote-github
```

If needed, refresh the CLI's `read:packages` scope interactively before exporting its token.
CI maps its short-lived workflow token to `GH_TOKEN`; no persistent PAT is needed there.
Read access does not authorize publication, which remains reserved by workflow.

## Managed-only work

Build a binding project directly when no native execution is needed:

```pwsh
dotnet build src/SDL2.Core/SDL2.Core.csproj
```

Other managed projects are under [src](../../src/). Smoke projects remain in
[the solution](../../Janset.SDL2.sln); a whole-solution restore may fail with NU1101 until setup
creates a current local feed override. This is a consumer-feed prerequisite, not a reason to add
source references. [Binding maintenance](binding-generator-maintenance.md) owns regeneration;
never hand-edit generated output or update an upstream submodule merely to change a declaration.
Use `dotnet run --file tools.cs -- generate-bindings` for a read-only plan and add `--check` to
validate committed output. The maintenance guide owns generation, selection and evidence options.

## Host-RID CI rehearsal

```pwsh
dotnet run --file tools.cs -- ci-sim
```

tools.cs cleans artifacts, then runs the Cake steps in this order: ResolveVersionsFromManifest,
PreFlightCheck, EnsureVcpkgDependencies, Harvest, NativeSmoke, ConsolidateHarvest, Package,
PackageConsumerSmoke. It writes the consumer override again after smoke, so the IDE does not retain
versions that cleanup removed. Every stage in the summary must pass.

Logs live under `.logs/tools/`, grouped by platform, command and run ID. Each Cake step has a log;
the summary prints the failing path and duration. `-v` / `--verbose` also tees output to the console.
Inspect the first failing step rather than treating a later missing package as the root cause.

## Manual pipeline for diagnosis

Prefer setup or ci-sim. For a specific stage investigation, bootstrap
[vcpkg](../../external/vcpkg/) for the host (`bootstrap-vcpkg.bat` or `bootstrap-vcpkg.sh`), then
use the Cake forwarder. This example derives all family versions instead of copying version pins:

```pwsh
dotnet run --file tools.cs -- build --target ResolveVersionsFromManifest --suffix=local.check --versions-file artifacts/resolve-versions/versions.json
dotnet run --file tools.cs -- build --target PreFlightCheck --versions-file artifacts/resolve-versions/versions.json
dotnet run --file tools.cs -- build --target EnsureVcpkgDependencies --rid win-x64
dotnet run --file tools.cs -- build --target Harvest --rid win-x64
dotnet run --file tools.cs -- build --target NativeSmoke --rid win-x64
dotnet run --file tools.cs -- build --target ConsolidateHarvest
dotnet run --file tools.cs -- build --target Package --versions-file artifacts/resolve-versions/versions.json
dotnet run --file tools.cs -- build --target PackageConsumerSmoke --rid win-x64 --versions-file artifacts/resolve-versions/versions.json
```

Choose the host RID from the [manifest](../../build/manifest.json), replacing the Windows example.
Use both overlays for manual vcpkg installs under
[native maintenance](native-dependency-maintenance.md#install-and-caches). Harvest receipts and payloads
live under `artifacts/harvest_output/`; consolidation produces the cross-RID receipts and license union.
The native pack consumes staged payload through `NativePayloadSource`, never a copied source-tree
runtime directory. [Packaging canon](../architecture/packaging-and-release.md) owns scope and payload rules.

For explicit versions, resolve with the existing ResolveVersionsFromExplicit target and pass the
produced mapping to stages via `--versions-file`. Follow ADR-0001's upstream-major/minor anchor.
Do not invoke Package with version-provider flags instead of a resolved file.
After manual diagnosis, rerun setup when the IDE's local override needs refreshing.

Existing completed release runs also provide packed nupkg artifacts. They may populate a local
folder feed as packages; a raw harvest artifact alone is not consumer evidence. Dispatching a new
release merely to get artifacts requires Deniz's approval.

## TFM behavior

[PackageConsumer.Smoke.csproj](../../tests/smoke-tests/package-smoke/PackageConsumer.Smoke/PackageConsumer.Smoke.csproj) and
[build properties](../../Directory.Build.props) own the TFM sets. Package consumer smoke first compiles
the compatibility asset, then runs executable TFMs. Modern .NET targets run on all supported hosts.
.NET Framework runs natively on Windows; Linux skips it because Mono cannot host this test discovery
path; macOS runs it only when Mono is available. Record actual skips rather than calling them passes.

## Cross-OS validation loop

Changes to manifest schema, host composition, harvest/pack/smoke, runtime paths, platform branches or
triplets require Windows, Linux and macOS evidence. Host-RID rehearsal does not replace the full
supported-RID matrix. For managed-only and documentation changes, use the applicable checks in
[quality gates](quality-gates.md); do not start a native flow merely for a managed refactor.

1. Implement and run the appropriate local checks in the development checkout.
2. Commit and push the verified change on the work branch
   ([workflow](workflow.md#agent-autonomy)).
3. Bring each validation checkout to that exact commit without discarding unrelated local work.
   Initialize its required submodules and verify its environment, then run ci-sim there.
4. Capture summary, logs, commit and platform. Compare failures with the passing host. Fix in the
   development branch and repeat checks at the new commit; do not fix forward only on a validation host.
5. When all host rehearsals pass, the reserved release dispatch can prove the full matrix through
   [release.yml](../../.github/workflows/release.yml). Do not claim an unrun platform.

**WSL uses its own Linux filesystem checkout.** Never run dotnet, tools.cs or vcpkg on the Windows
mount from WSL. Cross-filesystem execution mixes permissions and causes submodule/build churn.
Noninteractive sessions must load the shell profile that defines their SDK environment; use
`zsh -lc` where zsh is that host's configured login shell. Private host inventory lives in the
companion repository, not in these generic recipes.

## Environment and failure triage

Read supported options from tools.cs and the target tree. `DOTNET_ROOT` and `PATH` must agree with
the SDK installation, particularly in WSL and noninteractive macOS shells. Native tools use the
manifest-selected hybrid triplet and the vcpkg submodule; avoid a different system vcpkg accidentally
winning PATH resolution.

Classify failures before changing code:

- **Environment:** missing SDK/CMake/compiler, wrong PATH or DOTNET_ROOT, uninitialized submodule,
  disk exhaustion or absent Linux MIDI data. Repair the host prerequisite.
- **Revision mismatch:** compare the actual commit with the expected one. Update safely; do not
  assume another checkout's uncommitted work is disposable.
- **Regression:** reproduce at the same commit with a correct environment. Report platform, stage,
  exact error and log path. Check filename case, system exclusions, tar/DLL differences, glibc symbol
  versions and macOS Mono availability where relevant.

For DllNotFoundException, inspect package restore, the selected RID, extraction/copy targets and
the loaded payload. Refresh setup if local package versions are stale; do not mask it by copying a DLL.
For build-host failures, confirm the pinned SDK and inspect diagnostic verbosity before cleaning.
