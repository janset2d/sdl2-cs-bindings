# Playbook: Local Validation with `tools.cs`

> Reference for the `tools.cs` repo-root file-based .NET 10 app — the canonical dev orchestration entry point. Companion to [`local-development.md`](local-development.md) (which covers fresh-clone setup, env vars, and the manual full-build fallback).

## When to Run What

| Goal | Command |
| --- | --- |
| Verify a build-host change end-to-end on host RID (mini CI replay) | `dotnet run --file tools.cs -- ci-sim` |
| Refresh the local feed so IDE-driven smoke restore stays in sync | `dotnet run --file tools.cs -- setup --source=local` |
| Validate that a CI publish landed correctly (consumer's-eye view) | `dotnet run --file tools.cs -- setup --source=remote-github` |
| Pass a Cake target through directly (debugging / target discovery) | `dotnet run --file tools.cs -- build --target <Name> [args...]` |

## `tools ci-sim` — 9-step CI replay

Single command, 9 sequential Cake targets on the host RID, with per-step logs:

1. `CleanArtifacts`
2. `ResolveVersionsFromManifest` (per-concrete-family `--scope`, `--suffix=local.<timestamp>`)
3. `PreFlightCheck`
4. `EnsureVcpkgDependencies`
5. `Harvest`
6. `NativeSmoke`
7. `ConsolidateHarvest`
8. `Package`
9. `PackageConsumerSmoke`

After `PackageConsumerSmoke` finishes, `Janset.Local.props` is rewritten so the IDE-driven smoke restore stays in sync with the freshly-packed nupkgs (`ci-sim`'s `CleanArtifacts` step wipes the previous wave; without this rewrite the props would dangle at stale versions).

Logs land in `.logs/tools/<platform>-ci-sim-<runId>/<NN>-<TargetName>.log` (one file per step). A Spectre summary table prints at the end with per-step status + duration.

Add `-v` / `--verbose` to tee each step's stdout/stderr to the console while still writing the log file.

## `tools setup` — feed bootstrap for IDE-driven smoke

Three sources, all converging on the same outputs (`artifacts/packages/` + `build/msbuild/Janset.Local.props` + `artifacts/resolve-versions/versions.json`):

| Source | What it does |
| --- | --- |
| `--source=local` (default) | Full local pack: CleanArtifacts → ResolveVersionsFromManifest → PreFlightCheck → EnsureVcpkgDependencies → Harvest → ConsolidateHarvest → Package, then write `Janset.Local.props`. **NativeSmoke is not in this chain** — use `tools ci-sim` for the full CI replay. |
| `--source=remote-github` | Skip vcpkg / Harvest / Pack entirely. Discover latest published version per concrete family on `https://nuget.pkg.github.com/janset2d/index.json`, download managed + native nupkg pairs (wipe-on-prepare), write `Janset.Local.props` + `versions.json` for parity with Local. Requires `GH_TOKEN` / `GITHUB_TOKEN` env (Classic PAT with `read:packages` scope; fine-grained PATs are unsupported by GH Packages NuGet). |
| `--source=remote-nuget` | Stub. Returns "not implemented" until Phase 2b PD-7 wires public-feed promotion. |

`--no-clean` skips the leading `CleanArtifacts` step (useful when iterating on a single concern without re-harvesting).

## `tools build` — Cake passthrough

Forwards all subsequent args directly to `dotnet run --project build/_build -- ...` with stdout/stderr piped live to the console. Use for ad-hoc target invocation:

```bash
dotnet run --file tools.cs -- build --tree
dotnet run --file tools.cs -- build --target Info
dotnet run --file tools.cs -- build --target Inspect-HarvestedDependencies --rid linux-x64
dotnet run --file tools.cs -- build --target Coverage-Check
```

## Per-TFM Smoke Behavior

`PackageConsumerSmoke` loops executable TFMs from `PackageConsumer.Smoke.csproj`'s `$(ExecutableTargetFrameworks)` (`net10.0;net9.0;net8.0;net462`):

- **net10 / net9 / net8** run unconditionally on all platforms.
- **net462** runs:
  - On **Windows** — natively.
  - On **Linux** — auto-skipped (`Skipping package-smoke for TFM 'net462'`), since Mono cannot host TUnit + Microsoft Testing Platform discovery.
  - On **macOS** — runs only if `mono` is on `$PATH`; otherwise auto-skipped.

A compile-only `netstandard2.0` sanity pass precedes the TFM loop unconditionally.

## Reading Logs

`.logs/tools/<platform>-<subcommand>-<runId>/` contains one file per step. When a step fails:

- The Spectre summary table prints the failing log path on red.
- Tail the failing log: `tail -50 .logs/tools/.../NN-FailingStep.log`.

## Failure Triage

When a step fails, classify before reacting:

1. **Environment** — `dotnet` / `cmake` / tool missing, PATH not exported, `DOTNET_ROOT` absent, submodule not initialised, `freepats` missing on Linux. Fix host, re-run. See [`local-development.md`](local-development.md) for prerequisites and env-var recipes.
2. **Stale repo** — `git log --oneline -1` doesn't match the expected HEAD. `git pull`, re-run.
3. **Code regression** — same commit, clean environment, still fails. Surface with platform + step identifier + exact error text + log filename. This is what `tools ci-sim` exists to catch.

Most "it failed" reports in practice are category 1 or 2.

## Cross-Reference

- [`local-development.md`](local-development.md) — fresh-clone setup, manual full-build fallback, env vars, troubleshooting.
- [`overlay-management.md`](overlay-management.md) — vcpkg overlay triplets + ports.
- [`vcpkg-update.md`](vcpkg-update.md) — bumping the vcpkg baseline.
- [`adding-new-library.md`](adding-new-library.md) — adding a new SDL satellite.
- [`../knowledge-base/release-guardrails.md`](../knowledge-base/release-guardrails.md) — what each pipeline stage validates.
- `tools.cs` — the canonical implementation.
- `tests/smoke-tests/native-smoke/README.md` — C++ NativeSmoke harness detail.
