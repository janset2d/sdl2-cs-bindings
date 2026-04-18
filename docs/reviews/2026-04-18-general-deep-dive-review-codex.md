# Review — General Deep Dive (Session Baseline)

**Date:** 2026-04-18
**Status:** Ongoing
**Mode:** Read-only review
**Validation performed:**
- `dotnet test build/_build.Tests/Build.Tests.csproj -c Release --nologo` -> passed (`324` passed, `0` failed)
- `dotnet build Janset.SDL2.sln -c Release --nologo` -> failed during smoke-project restore (`NU1603`, `NU1605`)
- `dotnet build src/SDL2.Core/SDL2.Core.csproj -c Release --nologo` -> passed

## A. Scope And Assumptions

This note records findings from this session only. Review scope in this baseline pass:

- `Janset.SDL2.sln`
- `build/msbuild/Janset.Smoke.props`
- `build/msbuild/Janset.Smoke.targets`
- `build/_build/Program.cs`
- `tests/smoke-tests/package-smoke/*`
- `README.md`
- `docs/playbook/local-development.md`

No working-tree diff was available, so this is a live-state audit of the current repository.

## B. Findings First

### [High] The main solution is no longer a valid default build entrypoint

- **Location:** [Janset.SDL2.sln](../../Janset.SDL2.sln#L61), [Janset.SDL2.sln](../../Janset.SDL2.sln#L63), [tests/smoke-tests/package-smoke/PackageConsumer.Smoke/PackageConsumer.Smoke.csproj](../../tests/smoke-tests/package-smoke/PackageConsumer.Smoke/PackageConsumer.Smoke.csproj#L25), [README.md](../../README.md#L105), [docs/playbook/local-development.md](../playbook/local-development.md#L31)
- **Evidence type:** Observed in code / observed in executable validation
- **Confidence:** High
- **The reality:** the main solution includes smoke projects that are designed to run only under Cake orchestration with injected package-feed and version properties, but the repo still presents `dotnet build Janset.SDL2.sln` as a normal developer command. In this session, solution build failed while direct managed-project build succeeded.
- **Why it matters:** the repo's default "does it build?" path is broken. That is both a contributor experience defect and a reliability signal problem.
- **Recommended fix:** remove orchestrator-only smoke projects from `Janset.SDL2.sln`, or otherwise exclude them from normal solution restore/build. Keep them in a dedicated smoke solution or execute them only through Cake.
- **Tradeoff:** slightly less convenience in one solution view, but a real baseline build contract.

### [High] The smoke sentinel contract is weaker than advertised and can restore stale package versions

- **Location:** [build/msbuild/Janset.Smoke.props](../../build/msbuild/Janset.Smoke.props#L27), [build/msbuild/Janset.Smoke.props](../../build/msbuild/Janset.Smoke.props#L37), [build/msbuild/Janset.Smoke.targets](../../build/msbuild/Janset.Smoke.targets#L15), [build/msbuild/Janset.Smoke.targets](../../build/msbuild/Janset.Smoke.targets#L31), [tests/smoke-tests/package-smoke/PackageConsumer.Smoke/PackageConsumer.Smoke.csproj](../../tests/smoke-tests/package-smoke/PackageConsumer.Smoke/PackageConsumer.Smoke.csproj#L25)
- **Evidence type:** Observed in code / observed in executable validation
- **Confidence:** High
- **The reality:** the default smoke version value is `0.0.0-smoke-sentinel`, and the smoke projects use that value via `VersionOverride`. That is a minimum version range, not an exact pin. When the MSBuild guard does not intercept early enough, NuGet can resolve arbitrary higher versions from available feeds or caches. In this session it resolved mixed local versions instead of failing cleanly on the sentinel.
- **Why it matters:** the smoke path is supposed to validate the exact local-feed packages produced by the current run. Instead it can drift onto stale artifacts and produce misleading restore failures or false confidence.
- **Recommended fix:** make the smoke package references exact, not minimum-range. Example shape:

```xml
<PackageReference Include="Janset.SDL2.Core" VersionOverride="[$(JansetSdl2CorePackageVersion)]" />
```

Add one blackbox test that proves direct `dotnet build/test` on a smoke project fails with the intended guard contract rather than opportunistic NuGet resolution.
- **Tradeoff:** direct manual restore on smoke projects fails earlier and harder. That is the correct behavior for orchestrator-only probes.

### [Medium] Repo-root fallback is incorrect when `git rev-parse` is unavailable

- **Location:** [build/_build/Program.cs](../../build/_build/Program.cs#L252), [build/_build/Program.cs](../../build/_build/Program.cs#L256), [build/_build.Tests/Unit/CompositionRoot/ProgramCompositionRootTests.cs](../../build/_build.Tests/Unit/CompositionRoot/ProgramCompositionRootTests.cs#L123)
- **Evidence type:** Observed in code / observed in tests / observed in executable validation
- **Confidence:** High
- **The reality:** `DetermineRepoRootAsync` falls back to `AppContext.BaseDirectory` with two parent hops. In this repo layout, that resolves to `build/_build/bin`, not repository root. Current composition-root tests cover only the explicit `--repo-root` path.
- **Why it matters:** in environments where `git` is missing, blocked, or launched from an unexpected working directory, the build host can silently resolve the wrong root and fail with misleading downstream path errors.
- **Recommended fix:** replace fixed parent hopping with marker-based upward discovery from both current working directory and `AppContext.BaseDirectory` using repo markers such as `build/manifest.json`, `vcpkg.json`, and `Janset.SDL2.sln`. Add tests for the git-failure fallback path.
- **Tradeoff:** a little more startup logic for much safer failure behavior.

### [Medium] Contributor-facing docs materially drift from current packaging and build reality

- **Location:** [README.md](../../README.md#L54), [README.md](../../README.md#L105), [docs/playbook/local-development.md](../playbook/local-development.md#L31), [docs/playbook/local-development.md](../playbook/local-development.md#L77), [docs/playbook/local-development.md](../playbook/local-development.md#L124), [docs/plan.md](../plan.md#L29), [docs/plan.md](../plan.md#L49), [docs/plan.md](../plan.md#L186)
- **Evidence type:** Observed in code / observed in docs / observed in executable validation
- **Confidence:** High
- **The reality:** `README.md` and `local-development.md` still tell contributors to run `dotnet build Janset.SDL2.sln`, still show retired triplets such as `x64-windows-release` and `x64-linux-dynamic`, still say "Until the PackageTask is implemented", and still describe `SDL2_mixer` as using `mpg123` and `FluidSynth`, which conflicts with the locked LGPL-free codec decision in `plan.md`.
- **Why it matters:** these are not cosmetic nits. They send contributors into retired workflows and describe the wrong runtime/license surface.
- **Recommended fix:** update the docs in the same change as the solution/smoke-contract fix. Exact rewrites:

```md
Replace README's SDL2_mixer feature bullet with:
- **SDL2_mixer**: permissive-only codec stack: minimp3, drflac, stb_vorbis, libmodplug, Timidity/Native MIDI
```

```md
Replace the managed-build quick-start snippet in README.md and docs/playbook/local-development.md with:
dotnet test build/_build.Tests/Build.Tests.csproj -c Release
dotnet build src/SDL2.Core/SDL2.Core.csproj -c Release
```

```md
Replace the retired triplets in docs/playbook/local-development.md with:
./external/vcpkg/vcpkg install --triplet x64-windows-hybrid
./external/vcpkg/vcpkg install --triplet x64-linux-hybrid
./external/vcpkg/vcpkg install --triplet arm64-osx-hybrid
```

```md
Replace "Until the PackageTask is implemented" with:
PackageTask exists; manual copy steps are legacy fallback only and should not be presented as the primary packaging path.
```
- **Tradeoff:** docs churn only.

## C. Broader Systemic Observations

- The build-host unit suite is healthy enough to catch many local regressions, but the repo is still missing blackbox protection around contributor entrypoints.
- The highest-risk failures in this session came from orchestration contracts and docs drift, not from the core managed projects themselves.

## D. Open Questions / Confidence Limiters

- Should the main solution remain a contributor-facing surface at all, or should the repo explicitly move to task-specific build entrypoints only?
- Should smoke projects be treated as fully private build probes, or should they also support a deterministic direct invocation mode?

## E. What Was Not Verified

- End-to-end `Package`, `PackageConsumerSmoke`, or `PostFlight` execution in this session
- Linux/macOS runtime behavior from fresh local execution
- CI workflow execution

## F. Brief Summary

The core managed project build is fine; the default repo entrypoints are not. The biggest live issues in this session are a broken main-solution build contract, a smoke version sentinel that behaves like a range instead of an exact pin, an incorrect repo-root fallback, and contributor docs that still describe retired packaging/build flows.
