# Phase X - Lightweight .NET 10 / C# 14 Update

- **Date:** 2026-05-03
- **Status:** PLANNED / awaiting explicit implementation approval
- **Maintainer:** Deniz Irgin (@denizirgin)
- **Scope:** Lightweight SDK, TFM, C# language, CI runtime, script-scope, test-fixture, lock-file, and live-doc update
- **Gate:** Implementation changes require explicit approval per [`AGENTS.md`](../../AGENTS.md). This document is planning-only.
- **Related prior plan:** [`phase-x-modernization-2026-04-20.md`](phase-x-modernization-2026-04-20.md) captured a broad modernization wave. This plan extracts only the small, near-term .NET 10 / C# 14 slice and leaves the larger package, analyzer, Result-pattern, System.CommandLine, NativeAOT, and vcpkg-baseline work parked.

## 1. Executive Summary

Move the repository's default SDK/runtime posture from .NET 9 / C# 13 to .NET 10 / C# 14 while preserving the existing multi-target support contract.

The intended end state:

| Axis | Current | Target |
| --- | --- | --- |
| Root SDK pin | `9.0.200` | `10.0.203` |
| `$(LatestDotNet)` | `net9.0` | `net10.0` |
| Library TFMs | `net9.0;net8.0;netstandard2.0;net462` | `net10.0;net9.0;net8.0;netstandard2.0;net462` |
| Executable TFMs | `net9.0;net8.0;net462` | `net10.0;net9.0;net8.0;net462` |
| C# language | `13.0` | `14.0` |
| Cake build host | tracks `$(LatestDotNet)` | `net10.0` |
| Cake test project | tracks `$(LatestDotNet)` | `net10.0` |
| File-based witness scripts | separate `tests/scripts/global.json` pin | root SDK pin owns them |
| Consumer smoke CI runtime channels | root SDK + extra `8.0.x` | root SDK + extra `9.0.x` + `8.0.x` |

The update is intentionally not a broad modernization. It should not change native packaging strategy, release topology, vcpkg baseline, public package identity, D-3seg versioning, Cake target names, or the ADR-004 build-host architecture.

## 2. Goals

1. Make .NET 10 the default repo SDK and default modern TFM.
2. Keep .NET 9 in the supported library and executable target matrix.
3. Keep .NET 8, `netstandard2.0`, and `net462` support unchanged.
4. Move Cake Frosting host and build-host tests to `net10.0` through the existing `$(LatestDotNet)` indirection.
5. Move the repo-wide C# language version to `14.0`.
6. Remove the temporary `tests/scripts` SDK pin now that the root SDK is .NET 10.
7. Preserve deterministic CI setup for hosted runners and the custom Linux container.
8. Update hardcoded `net9.0` test fixtures so tests assert the new current matrix instead of stale current-state literals.
9. Regenerate lock files through restore, not by hand.
10. Validate with build-host tests and both `smoke-witness.cs local` and `smoke-witness.cs ci-sim`.

## 3. Non-Goals

- No System.CommandLine 2.x GA migration.
- No OneOf.Monads / Result-pattern migration.
- No analyzer-suite reshuffle beyond the minimal SDK-aligned NetAnalyzers bump needed for .NET 10 consistency.
- No broad third-party package upgrade wave.
- No vcpkg baseline or overlay-port update.
- No Docker image change to bake in .NET.
- No CI job topology redesign.
- No Cake target rename or ADR-004 structural refactor.
- No mass rewrite of archived or historical docs. Historical evidence stays historical unless it is actively misleading in a live workflow section.

## 4. Design Decisions

### 4.1 SDK Pin

Use root `global.json` as the single SDK pin:

```json
{
  "sdk": {
    "version": "10.0.203",
    "rollForward": "latestFeature",
    "allowPrerelease": false
  },
  "test": {
    "runner": "Microsoft.Testing.Platform"
  }
}
```

`rollForward=latestFeature` stays. The repo already uses SDK feature-band roll-forward; the lightweight update only changes the baseline major line.

### 4.2 Target Framework Policy

`$(LatestDotNet)` remains the central switch. The important detail is that adding .NET 10 must not silently drop .NET 9.

Target shape:

```xml
<LatestDotNet>net10.0</LatestDotNet>
<LibraryTargetFrameworks>$(LatestDotNet);net9.0;net8.0;netstandard2.0;net462</LibraryTargetFrameworks>
<ExecutableTargetFrameworks>$(LatestDotNet);net9.0;net8.0;net462</ExecutableTargetFrameworks>
```

Cake host and Cake tests already use `$(LatestDotNet)`, so they move to `net10.0` without direct project-file edits.

### 4.3 C# 14

Set repo-wide:

```xml
<LangVersion>14.0</LangVersion>
```

Known C# 14 risk areas to keep in mind during review:

- `field` contextual keyword inside property accessors.
- contextual `extension` / `scoped` / `partial` identifier behavior.
- overload-resolution changes involving spans.
- `Enumerable.Reverse` vs in-place array/span reverse behavior on older TFMs.

The earlier modernization research found no current call sites that trigger these traps, but the validation pass should still treat new compiler/analyzer diagnostics as real findings.

### 4.4 Analyzer Posture

Keep the strict analyzer posture:

- `TreatWarningsAsErrors=true`
- `AnalysisMode=All`
- `AnalysisLevel=latest`
- `Features=strict`

Minimal package alignment:

- Move `Microsoft.CodeAnalysis.NetAnalyzers` from `9.0.0` to the current stable `10.0.x` line during implementation.
- Do not use this wave to upgrade Meziantou, Roslynator, SonarAnalyzer, System.CommandLine, TUnit, or other packages unless validation proves it is required.

### 4.5 CI Runtime Setup

Keep `actions/setup-dotnet@v5` in release jobs that invoke `dotnet`.

Reasoning:

- Downstream jobs run the framework-dependent Cake host as `dotnet ./cake-host/Build.dll`; after this update, that requires a .NET 10 runtime.
- GitHub-hosted runners may have .NET preinstalled, but that image contents contract is not the repo's versioning contract.
- The custom Linux builder container deliberately does not include a .NET SDK/runtime; `setup-dotnet` is required there.

Only targeted CI edit:

- In `consumer-smoke`, keep `global-json-file: global.json` for .NET 10.
- Add extra runtime/SDK channels for lower executable TFMs:

```yaml
dotnet-version: |
  9.0.x
  8.0.x
```

Do not add an ad-hoc runtime-only install script in this lightweight wave. It would trade a small setup-time win for extra CI surface area.

### 4.6 `tests/scripts` Scope Cleanup

Remove `tests/scripts/global.json` because root `global.json` becomes .NET 10.

Remove `tests/scripts/.gitattributes` because root `.gitattributes` already enforces LF for all text files:

```gitattributes
* text=auto eol=lf
```

This is not a statement that LF no longer matters. The shebang scripts still need LF on Unix; the root attributes file already provides that guarantee.

## 5. Work Plan

### L0 - Preflight Snapshot

- [ ] Run `git status --short` or inspect source-control state.
- [ ] Preserve unrelated user changes, especially existing `src/*/packages.lock.json` drift.
- [ ] Confirm `dotnet --list-sdks` includes `10.0.203` or a compatible `10.0.2xx` SDK.
- [ ] Capture the current hardcoded `.NET 9` hit list with generated folders excluded.
- [ ] Confirm root `.gitattributes` still enforces LF before deleting `tests/scripts/.gitattributes`.

### L1 - Core SDK / TFM / Language Patch

Files:

- `global.json`
- `Directory.Build.props`

Tasks:

- [ ] Change root SDK version to `10.0.203`.
- [ ] Keep `rollForward=latestFeature` and `allowPrerelease=false`.
- [ ] Change `LatestDotNet` to `net10.0`.
- [ ] Add `net9.0` explicitly after `$(LatestDotNet)` in both TFM lists.
- [ ] Change `LangVersion` to `14.0`.
- [ ] Leave `TargetFrameworks=$(LibraryTargetFrameworks)` behavior unchanged.
- [ ] Leave AOT/trim analyzer conditions unchanged for modern TFMs.

Expected result:

- Build host and build-host tests target `net10.0` automatically.
- `src` libraries target five TFMs: `net10.0`, `net9.0`, `net8.0`, `netstandard2.0`, `net462`.
- executable/smoke projects target four TFMs: `net10.0`, `net9.0`, `net8.0`, `net462`.

### L2 - Minimal Package Alignment

Files:

- `Directory.Packages.props`

Tasks:

- [ ] Bump `Microsoft.CodeAnalysis.NetAnalyzers` from `9.0.0` to a stable SDK-aligned `10.0.x` version.
- [ ] Keep `Cake.Frosting` and `Cake.Testing` at `6.1.0` unless restore/build proves otherwise.
- [ ] Keep TUnit at `1.33.0` in this wave unless .NET 10 validation proves an incompatibility.
- [ ] Do not upgrade System.CommandLine or remove `System.CommandLine.NamingConventionBinder` here.

Expected result:

- Analyzer package line no longer lags the SDK major.
- The diff stays focused on the .NET 10/C# 14 update.

### L3 - Hardcoded `net9.0` Source/Test Fixture Updates

Files known to need attention:

- `build/_build.Tests/Unit/Integrations/DotNet/DotNetRuntimeEnvironmentTests.cs`
- `build/_build.Tests/Unit/Features/Packaging/PackageOutputValidatorTests.cs`
- `build/_build.Tests/Unit/Features/Packaging/PackagePipelineTests.cs`
- `build/_build.Tests/Unit/Features/Packaging/SmokeScopeComparatorTests.cs`
- `src/Directory.Build.props`

Tasks:

- [ ] Add `net10.0` to runtime-channel tests and expected channel output (`8.0`, `9.0`, `10.0`).
- [ ] Keep `net9.0` fixtures when the intent is backwards-compatibility coverage.
- [ ] Replace `net9.0` with `net10.0` only when the test means "current latest TFM".
- [ ] Update package-output expectations to include `net10.0` framework groups while retaining `net9.0`.
- [ ] Verify pack-output path expectations such as `lib/net9.0/...` before flipping them to `lib/net10.0/...`.
- [ ] Update generic smoke-scope XML fixture TFMs where they model the current default project shape.
- [ ] Update comments in `src/Directory.Build.props` that mention `Microsoft.NET.ILLink.Tasks 9.0.x`; prefer version-neutral wording if possible.
- [ ] Run another focused search after edits for `net9.0`, `9.0.200`, and `LangVersion>13.0` outside generated and historical folders.

Do not blindly replace every `net9.0`. The target matrix intentionally keeps .NET 9.

### L4 - Release Workflow Runtime Update

File:

- `.github/workflows/release.yml`

Tasks:

- [ ] Keep `actions/setup-dotnet@v5` on jobs that invoke `dotnet`.
- [ ] Keep `global-json-file: global.json` as the .NET 10 source of truth.
- [ ] In `consumer-smoke`, update the comment from `net9.0;net8.0;net462` to `net10.0;net9.0;net8.0;net462`.
- [ ] In `consumer-smoke`, change extra `dotnet-version` entries to `9.0.x` and `8.0.x`.
- [ ] Do not remove `setup-dotnet` from custom Linux container jobs.
- [ ] Do not bake .NET into `docker/linux-builder.Dockerfile` in this wave.

Expected result:

- Every release job has the .NET 10 runtime needed for the framework-dependent Cake host.
- Consumer smoke can execute `net10.0`, `net9.0`, and `net8.0` rows.
- `net462` remains handled by existing platform-specific runner logic.

### L5 - File-Based Script Cleanup

Files:

- `tests/scripts/global.json`
- `tests/scripts/.gitattributes`
- `tests/scripts/README.md`

Tasks:

- [ ] Delete `tests/scripts/global.json`.
- [ ] Delete `tests/scripts/.gitattributes` because root `.gitattributes` already forces LF.
- [ ] Update `tests/scripts/README.md` to say scripts use the root .NET 10 SDK pin.
- [ ] Remove stale wording that says root remains .NET 9 while script scope is .NET 10.
- [ ] Keep launch examples for `dotnet run smoke-witness.cs local`, `dotnet run smoke-witness.cs ci-sim`, and direct Unix shebang execution if currently documented.

### L6 - Live Documentation Update

Live docs to update:

- `AGENTS.md`
- `CLAUDE.md`
- `README.md`
- `docs/onboarding.md`
- `docs/plan.md`
- `docs/knowledge-base/release-guardrails.md`
- `docs/knowledge-base/ci-cd-packaging-and-release-plan.md`
- `docs/playbook/cross-platform-smoke-validation.md`
- `docs/playbook/unix-smoke-runbook.md`

Tasks:

- [ ] Update current target framework lists to include `net10.0` and retain `net9.0`.
- [ ] Update current technology table entries from `.NET 9.0 / C# 13` to `.NET 10.0 / C# 14`.
- [ ] Update build-host binary paths from `bin/Release/net9.0` to `bin/Release/net10.0` in live operational instructions.
- [ ] Update current consumer smoke expectations to mention `net10.0`, `net9.0`, `net8.0`, and platform-gated `net462`.
- [ ] Replace live references to `tests/scripts/global.json` with root `global.json` ownership.
- [ ] Avoid rewriting archived evidence records unless they are inside a current workflow instruction section.

Historical docs and archived research can keep old values when they describe old commits. If a historical document is linked from a live playbook and currently reads as operational instruction, add a short "current state changed" note instead of erasing history.

### L7 - Restore and Lock Files

Expected lock files:

- `build/_build/packages.lock.json`
- `build/_build.Tests/packages.lock.json`
- `src/**/packages.lock.json`
- any smoke/test lock files affected by inherited TFM expansion

Tasks:

- [ ] Run restore with .NET 10 SDK.
- [ ] Let lock files update mechanically.
- [ ] Review lock-file diffs for expected `net10.0` target groups and SDK/analyzer package changes.
- [ ] Do not hand-edit lock files.
- [ ] Preserve unrelated lock-file changes that predate this wave unless Deniz explicitly asks to revert them.

Expected result:

- `net10.0` groups appear where inherited TFM policy requires them.
- `net9.0` groups remain for supported rows.
- Existing `net8.0`, `netstandard2.0`, and `net462` rows remain.

### L8 - Validation

Minimum local validation:

```pwsh
dotnet --info
dotnet restore Janset.SDL2.sln
dotnet build build/_build/Build.csproj -c Release --nologo
dotnet test build/_build.Tests/Build.Tests.csproj -c Release --nologo
dotnet build src/SDL2.Core/SDL2.Core.csproj -c Release --nologo
Push-Location tests/scripts
dotnet run smoke-witness.cs local
dotnet run smoke-witness.cs ci-sim
Pop-Location
```

Validation expectations:

- Build host compiles as `net10.0`.
- Build-host tests pass on `net10.0`.
- At least one library project compiles across the full library TFM matrix.
- `smoke-witness.cs local` passes.
- `smoke-witness.cs ci-sim` passes on the local Windows environment, assuming native prerequisites are available.
- If `ci-sim` fails because of environment prerequisites rather than code, capture the exact blocker and rerun from the correct Developer PowerShell / CI-sim-ready shell before declaring failure.

Optional follow-up validation:

- Run full solution build after `SetupLocalDev` refreshes `build/msbuild/Janset.Local.props` if the solution restore/build path needs native props.
- Trigger `release.yml` manually once local validation is green.

## 6. Risk Register

| Risk | Likelihood | Impact | Mitigation |
| --- | --- | --- | --- |
| New C# 14 compiler/analyzer diagnostics break `TreatWarningsAsErrors` | Medium | Medium | Fix real issues; avoid broad suppressions. Keep package upgrades minimal. |
| `net9.0` is accidentally removed from the supported matrix | Medium | High | Explicitly add `net9.0` after `$(LatestDotNet)` in both TFM lists; validate generated package nuspec groups. |
| Consumer smoke lacks .NET 9 runtime after root SDK moves to .NET 10 | High without YAML edit | High | Add `9.0.x` alongside existing `8.0.x` in consumer-smoke `setup-dotnet`. |
| Custom Linux container cannot run Cake host if `setup-dotnet` is removed | High if optimized too aggressively | High | Keep `setup-dotnet` in all release jobs that invoke `dotnet`. |
| Lock-file churn obscures meaningful changes | Medium | Medium | Regenerate through restore and review by target group/package. Do not mix broad package upgrades into this wave. |
| Historical docs become noisy if mass-updated | Medium | Low | Update live operational docs; leave archived evidence alone unless it misleads current workflow. |
| `tests/scripts/.gitattributes` deletion regresses Unix shebang line endings | Low | Medium | Root `.gitattributes` already enforces LF for text files; verify before deletion. |
| `smoke-witness.cs ci-sim` fails because host lacks native prerequisites | Medium | Medium | Treat as environment blocker only after confirming the code build/test path is green and rerunning in the expected shell. |

## 7. Exit Criteria

- [ ] Root SDK pin is `10.0.203`.
- [ ] Repo-wide `LangVersion` is `14.0`.
- [ ] `LatestDotNet` is `net10.0`.
- [ ] Libraries target `net10.0;net9.0;net8.0;netstandard2.0;net462`.
- [ ] Executables target `net10.0;net9.0;net8.0;net462`.
- [ ] Cake host and Cake tests build as `net10.0` without direct project-file TFM edits.
- [ ] `tests/scripts/global.json` is removed.
- [ ] `tests/scripts/.gitattributes` is removed only after confirming root LF policy covers it.
- [ ] `release.yml` keeps deterministic `setup-dotnet` and installs extra `9.0.x` + `8.0.x` for consumer smoke.
- [ ] Hardcoded-current `net9.0` test fixtures are updated; compatibility `net9.0` fixtures remain.
- [ ] Lock files are regenerated and reviewed.
- [ ] Live docs reflect .NET 10 / C# 14 and the expanded TFM matrix.
- [ ] `dotnet test build/_build.Tests/Build.Tests.csproj -c Release --nologo` passes.
- [ ] `tests/scripts/smoke-witness.cs local` passes.
- [ ] `tests/scripts/smoke-witness.cs ci-sim` passes or has a documented, reproducible environment blocker.

## 8. Rollback Plan

Rollback should be a normal revert of the implementation commit or commit series.

If the wave is split:

1. Revert CI/runtime edits first only if CI is blocked and local code changes are still under investigation.
2. Revert SDK/TFM/language edits and regenerated lock files together.
3. Restore `tests/scripts/global.json` and `tests/scripts/.gitattributes` together if root SDK reverts to .NET 9.
4. Revert doc updates last, so historical notes about the failed attempt can be preserved if useful.

Do not partially revert only lock files after the SDK/TFM change; that produces a misleading restore state.

## 9. Implementation Notes For The Next Agent Pass

- Use `apply_patch` for source/doc edits and deletions.
- Do not edit project files directly unless the approved scope explicitly includes them. This lightweight plan should not need `.csproj` edits because `$(LatestDotNet)` already drives the relevant projects.
- Do not hand-edit lock files.
- Keep unrelated package-lock drift intact.
- Run focused searches after edits with generated folders excluded.
- Treat `net9.0` as both a legacy literal to update in "current latest" contexts and an intentionally retained supported TFM.
- Keep the CI setup conservative. The custom Linux container has no .NET; hosted runner preinstalls are not the repo contract.

## 10. Change Log

| Date | Change |
| --- | --- |
| 2026-05-03 | Initial detailed plan for the lightweight .NET 10 / C# 14 update. |
