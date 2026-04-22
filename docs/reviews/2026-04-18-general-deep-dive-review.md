# Review — General Deep Dive (Round 1)

**Date:** 2026-04-18
**Status:** Ongoing
**Mode:** Read-only review
**Validation performed:** targeted code/docs inspection plus `dotnet test build/_build.Tests/Build.Tests.csproj -c Release --nologo` (324 passed, 0 failed)

## Scope And Assumptions

This round reviewed repository-wide high-risk areas with focus on build-host reliability, packaging guardrails, cross-platform behavior, and docs/code drift:

- `build/_build/Program.cs`
- `build/_build/Modules/Packaging/*`
- `build/_build/Modules/Preflight/*`
- `build/_build/Tasks/*`
- `tests/smoke-tests/package-smoke/*`
- `external/sdl2-cs/src/*` (read-only boundary)
- canonical docs in `docs/plan.md` and `docs/knowledge-base/*`

No working-tree diff was available, so this is a live-state audit.

## Findings

### [High] Repo-root fallback resolves to build output path when git resolution fails

- **Location:** [build/_build/Program.cs](../../build/_build/Program.cs#L253), [build/_build/Program.cs](../../build/_build/Program.cs#L256), [build/_build.Tests/Unit/CompositionRoot/ProgramCompositionRootTests.cs](../../build/_build.Tests/Unit/CompositionRoot/ProgramCompositionRootTests.cs#L123), [build/_build.Tests/Fixtures/WorkspaceFiles.cs](../../build/_build.Tests/Fixtures/WorkspaceFiles.cs#L46)
- **Evidence type:** Observed in code / observed in tests / observed in executable validation
- **Confidence:** High
- **The reality:** `DetermineRepoRootAsync` falls back to `AppContext.BaseDirectory` with only two parent hops. In this repo layout that resolves to `build/_build/bin`, not repository root. Composition-root tests only verify the explicit `--repo-root` happy path and do not verify git-failure fallback.
- **Why it matters:** in git-less or git-failure environments, tasks can run against a non-repo working directory and fail with misleading path errors or operate on unintended directories.
- **Recommended fix:** replace fixed-depth fallback with marker-based upward discovery (`.git`, `vcpkg.json`, `build/manifest.json`) from current working directory and `AppContext.BaseDirectory`; add negative-path tests for git-failure fallback.
- **Tradeoff:** slightly more startup logic and tests for much safer behavior.

### [High] G48 unix payload check allows mixed-invalid runtime payloads

- **Location:** [build/_build/Modules/Packaging/PackageOutputValidator.cs](../../build/_build/Modules/Packaging/PackageOutputValidator.cs#L870), [build/_build/Modules/Packaging/PackageOutputValidator.cs](../../build/_build/Modules/Packaging/PackageOutputValidator.cs#L876), [build/_build/Modules/Packaging/PackageOutputValidator.cs](../../build/_build/Modules/Packaging/PackageOutputValidator.cs#L879), [build/_build/Modules/Packaging/PackageOutputValidator.cs](../../build/_build/Modules/Packaging/PackageOutputValidator.cs#L905)
- **Evidence type:** Observed in code
- **Confidence:** High
- **The reality:** unix validation only requires one correctly named tarball; it does not fail if extra payload files exist under `runtimes/<rid>/native/`.
- **Why it matters:** malformed native payloads can pass a release guardrail intended to enforce a strict package shape.
- **Recommended fix:** make unix check exclusive: require exactly one entry in `runtimes/<rid>/native/` and that entry must be `<PackageId>.tar.gz`; add tests for tarball+DLL and tarball+extra-file cases.
- **Tradeoff:** stricter validation may fail currently tolerated but invalid artifacts.

### [High] Known broken mixer/ttf linked-version wrappers remain publicly shipped and are intentionally skipped in managed smoke

- **Location:** [external/sdl2-cs/src/SDL2_mixer.cs](../../external/sdl2-cs/src/SDL2_mixer.cs#L148), [external/sdl2-cs/src/SDL2_ttf.cs](../../external/sdl2-cs/src/SDL2_ttf.cs#L77), [src/SDL2.Mixer/SDL2.Mixer.csproj](../../src/SDL2.Mixer/SDL2.Mixer.csproj#L10), [src/SDL2.Ttf/SDL2.Ttf.csproj](../../src/SDL2.Ttf/SDL2.Ttf.csproj#L10), [tests/smoke-tests/package-smoke/PackageConsumer.Smoke/PackageSmokeTests.cs](../../tests/smoke-tests/package-smoke/PackageConsumer.Smoke/PackageSmokeTests.cs#L230), [docs/plan.md](../plan.md#L345)
- **Evidence type:** Observed in code / observed in tests / observed in docs
- **Confidence:** High
- **The reality:** incorrect `EntryPoint` declarations are linked into shipped managed packages; managed smoke explicitly avoids those wrappers.
- **Why it matters:** consumers calling these public APIs hit `EntryPointNotFoundException` at runtime.
- **Recommended fix:** preserve submodule boundary, but provide repo-local safe wrapper surface (or explicit fail-fast with actionable message) until generator replacement; add characterization tests for current behavior contract.
- **Tradeoff:** temporary compatibility shim maintenance versus ongoing runtime footgun.

### [Medium] PreFlight native ProjectReference path check is case-insensitive on all platforms

- **Location:** [build/_build/Modules/Preflight/CsprojPackContractValidator.cs](../../build/_build/Modules/Preflight/CsprojPackContractValidator.cs#L283)
- **Evidence type:** Observed in code
- **Confidence:** Medium
- **The reality:** `PathsEqual` uses `StringComparison.OrdinalIgnoreCase` unconditionally.
- **Why it matters:** on Linux/macOS this can mask casing drift in `ProjectReference` paths that would fail in real builds on case-sensitive file systems.
- **Recommended fix:** use OS-aware comparison (`Ordinal` on Linux/macOS, `OrdinalIgnoreCase` on Windows) and add cross-platform test coverage for case mismatch behavior.
- **Tradeoff:** stricter check can reveal existing latent path-casing issues.

### [Medium] Guardrail/docs drift: stale G28/G29 labels and stale test-count telemetry

- **Location:** [build/_build/Modules/Packaging/PackageOutputValidator.cs](../../build/_build/Modules/Packaging/PackageOutputValidator.cs#L734), [build/_build.Tests/Unit/Modules/Packaging/PackageOutputValidatorTests.cs](../../build/_build.Tests/Unit/Modules/Packaging/PackageOutputValidatorTests.cs#L248), [docs/plan.md](../plan.md#L172), [docs/plan.md](../plan.md#L178), [docs/plan.md](../plan.md#L197), [docs/plan.md](../plan.md#L332)
- **Evidence type:** Observed in code and docs
- **Confidence:** High
- **The reality:** comments still refer to retired guard IDs (`G28/G29`) while enum/docs use `G47/G48/G51`; plan test-count statements are behind current run (324 passing).
- **Why it matters:** drift weakens incident triage and trust in canonical status docs.
- **Recommended fix:** normalize labels in comments/tests, and refresh plan test counts with date-stamped values.
- **Tradeoff:** docs/comment churn only.

### [Low] StrategyResolver XML docs still claim unknown-suffix triplets are rejected outright

- **Location:** [build/_build/Modules/Strategy/StrategyResolver.cs](../../build/_build/Modules/Strategy/StrategyResolver.cs#L14), [build/_build/Modules/Strategy/StrategyResolver.cs](../../build/_build/Modules/Strategy/StrategyResolver.cs#L58), [build/_build.Tests/Unit/Modules/Strategy/StrategyResolutionTests.cs](../../build/_build.Tests/Unit/Modules/Strategy/StrategyResolutionTests.cs#L101)
- **Evidence type:** Observed in code / observed in tests
- **Confidence:** High
- **The reality:** implementation accepts stock triplets without `-hybrid/-dynamic` when strategy is `pure-dynamic`; XML summary says such triplets are rejected.
- **Why it matters:** this is an API contract documentation mismatch for a key strategy boundary.
- **Recommended fix:** update XML summary to match runtime behavior.
- **Tradeoff:** none.

## Broader Systemic Observations

- Build-host architectural seams are generally strong and test-backed.
- The recurring risk theme is drift between intended guardrail strictness and what validators actually enforce.

## Open Questions / Confidence Limiters

- Should git-less fallback be a supported build-host mode, or should failure to resolve repo root become explicit/terminal?
- For the known broken wrappers, is a temporary managed compatibility shim acceptable before generator migration?

## What Was Not Verified

- End-to-end packaging + smoke across all 7 RIDs in this round.
- CI workflow runtime behavior and release pipeline execution.
- Native smoke runtime on Linux/macOS from this session.

## Short Summary

Highest-priority fixes are repo-root fallback hardening and strict unix payload exclusivity in G48. Public wrapper breakage is known but still active on shipped API surface. Guardrail/comment/doc drift should be cleaned to keep operations and audits trustworthy.

## Round 2 Addendum (CI/Workflow Slice)

### [Medium] Runner source-of-truth drift between manifest and workflow matrix

- **Location:** [build/manifest.json](../../build/manifest.json#L14), [.github/workflows/prepare-native-assets-main.yml](../../.github/workflows/prepare-native-assets-main.yml#L57), [docs/onboarding.md](../onboarding.md#L173), [docs/plan.md](../plan.md#L196)
- **Evidence type:** Observed in code and docs
- **Confidence:** High
- **The reality:** `build/manifest.json` declares `linux-arm64` runner as `ubuntu-24.04-arm`, while the orchestrator workflow hard-codes `ubuntu-24.04-arm` in its matrix include block.
- **Why it matters:** this directly violates the documented “manifest as single source of truth” contract and increases drift risk while Stream C dynamic matrix migration is still pending.
- **Recommended fix:** either (A) align hard-coded workflow values to manifest immediately and add a lightweight CI guard that diffs workflow matrix runners against manifest runtime rows, or (B) prioritize Stream C extraction so matrix values are generated directly from manifest.
- **Tradeoff:** A is fast and incremental; B removes the class of drift entirely but is larger scope.

### [Low] Windows reusable workflow job name is incorrect and degrades observability

- **Location:** [.github/workflows/prepare-native-assets-windows.yml](../../.github/workflows/prepare-native-assets-windows.yml#L24)
- **Evidence type:** Observed in code
- **Confidence:** High
- **The reality:** Windows job is named `Build Linux (...)`.
- **Why it matters:** operator dashboards and run triage become misleading, especially during cross-platform failure investigations.
- **Recommended fix:** rename to `Build Windows (...)`.
- **Tradeoff:** none.

### [Low] Windows harvest workflow relies on implicit repo-root detection while Linux/macOS pass it explicitly

- **Location:** [.github/workflows/prepare-native-assets-linux.yml](../../.github/workflows/prepare-native-assets-linux.yml#L107), [.github/workflows/prepare-native-assets-macos.yml](../../.github/workflows/prepare-native-assets-macos.yml#L71), [.github/workflows/prepare-native-assets-windows.yml](../../.github/workflows/prepare-native-assets-windows.yml#L65)
- **Evidence type:** Observed in code
- **Confidence:** High
- **The reality:** Linux/macOS workflows invoke Cake with `--repo-root "$(pwd)"`; Windows does not.
- **Why it matters:** platform behavior differs for the same task contract, and Windows currently depends on the `Program.cs` fallback path and git resolution behavior that is already a known reliability concern.
- **Recommended fix:** pass `--repo-root` on Windows for consistency with Linux/macOS.
- **Tradeoff:** none.

### [Note] Unexpected docs/README.md change review: no defect found

- **Location:** [docs/README.md](../README.md#L81)
- **Evidence type:** Observed in code
- **Confidence:** High
- **The reality:** the change adds a `Reviews (Dated Assessments)` index section and links both existing review docs under `docs/reviews/`.
- **Why it matters:** it improves discoverability and follows this repo’s documentation index pattern.
- **Recommended action:** keep as-is.

### [Low] PreFlight vcpkg reader has no negative-path unit coverage and mixed error-shaping surface

- **Location:** [build/_build/Modules/Preflight/VcpkgManifestReader.cs](../../build/_build/Modules/Preflight/VcpkgManifestReader.cs#L17), [build/_build/Modules/Preflight/VcpkgManifestReader.cs](../../build/_build/Modules/Preflight/VcpkgManifestReader.cs#L21), [build/_build.Tests/Unit/Modules/Preflight/VcpkgManifestReaderTests.cs](../../build/_build.Tests/Unit/Modules/Preflight/VcpkgManifestReaderTests.cs#L11), [build/_build.Tests/Unit/Modules/Preflight/VcpkgManifestReaderTests.cs](../../build/_build.Tests/Unit/Modules/Preflight/VcpkgManifestReaderTests.cs#L30)
- **Evidence type:** Observed in code / observed in tests
- **Confidence:** High
- **The reality:** unit tests cover only success cases. `Parse` wraps invalid JSON into `ArgumentException`, while `ParseFile` can also surface raw file I/O exceptions from `OpenRead`.
- **Why it matters:** error behavior for the same component is harder to reason about and currently under-tested in failure paths used by PreFlight.
- **Recommended fix:** add tests for invalid JSON and missing file paths; decide whether `ParseFile` should normalize missing-file errors to the same shaped exception contract as parse failures.
- **Tradeoff:** adding normalization may hide precise I/O exception types unless wrapped with inner exceptions.

## Round 3 Addendum (Runtime/MSBuild Target Slice)

### [High] Compile-sanity consumer gate is currently red under orchestrator-equivalent invocation

- **Location:** [tests/smoke-tests/package-smoke/Compile.NetStandard/Probe.cs](../../tests/smoke-tests/package-smoke/Compile.NetStandard/Probe.cs#L18), [build/msbuild/Janset.Smoke.props](../../build/msbuild/Janset.Smoke.props#L59), [build/msbuild/Janset.Smoke.props](../../build/msbuild/Janset.Smoke.props#L60), [build/_build/Modules/Packaging/PackageConsumerSmokeRunner.cs](../../build/_build/Modules/Packaging/PackageConsumerSmokeRunner.cs#L92), [build/_build/Modules/Packaging/PackageConsumerSmokeRunner.cs](../../build/_build/Modules/Packaging/PackageConsumerSmokeRunner.cs#L272), [build/_build/Modules/Packaging/PackageConsumerSmokeRunner.cs](../../build/_build/Modules/Packaging/PackageConsumerSmokeRunner.cs#L284)
- **Evidence type:** Observed in code / observed in command validation
- **Confidence:** High
- **The reality:** compile-sanity currently declares an intentionally-unused probe field (`_surface`) while warnings are treated as errors. The active smoke analyzer relaxations do not suppress `S1144` or `CA1823`, so the compile-sanity project fails before runtime smoke starts.
- **Observed command result:**
	- `dotnet build tests/smoke-tests/package-smoke/Compile.NetStandard/Compile.NetStandard.csproj -c Release --disable-build-servers -p:UseSharedCompilation=false -nodeReuse:false -p:LocalPackageFeed=".../artifacts/packages" -p:RestorePackagesPath=".../artifacts/package-consumer-smoke/packages-cache" -p:JansetSdl2CorePackageVersion=1.3.0-validation.win64.1 -p:JansetSdl2ImagePackageVersion=1.3.0-validation.win64.1 -p:JansetSdl2MixerPackageVersion=1.3.0-validation.win64.1 -p:JansetSdl2TtfPackageVersion=1.3.0-validation.win64.1 -p:JansetSdl2GfxPackageVersion=1.3.0-validation.win64.1`
	- Fails with: `S1144 Remove the unused private field '_surface'` and `CA1823 Unused field '_surface'`.
	- In the same package/version context, runtime smoke itself is green on `net9.0/win-x64` (`dotnet test ...PackageConsumer.Smoke.csproj -f net9.0 -r win-x64 ...` reported `12/12` passing). This isolates the break to compile-sanity gate behavior, not package-loader/runtime basics.
- **Why it matters:** this is the exact invocation shape the runner uses for the compile-only netstandard2.0 guardrail. In concrete 5-family smoke scope, the smoke chain is blocked at compile-sanity.
- **Recommended fix:** keep probe intent but make it analyzer-clean (for example, move the type touch into a called method/module initializer) or add narrow, file-local suppressions with rationale for this compile-only probe.
- **Tradeoff:** suppressions are low effort but weaker than making the probe structurally used.

### [Medium] Shared native consumer target has asymmetric missing-payload diagnostics (build vs publish/.NETFramework)

- **Location:** [src/native/_shared/Janset.SDL2.Native.Common.targets](../../src/native/_shared/Janset.SDL2.Native.Common.targets#L120), [src/native/_shared/Janset.SDL2.Native.Common.targets](../../src/native/_shared/Janset.SDL2.Native.Common.targets#L140), [src/native/_shared/Janset.SDL2.Native.Common.targets](../../src/native/_shared/Janset.SDL2.Native.Common.targets#L149), [src/native/_shared/Janset.SDL2.Native.Common.targets](../../src/native/_shared/Janset.SDL2.Native.Common.targets#L166), [src/native/_shared/Janset.SDL2.Native.Common.targets](../../src/native/_shared/Janset.SDL2.Native.Common.targets#L179)
- **Evidence type:** Observed in code
- **Confidence:** High
- **The reality:** build-time Unix extraction emits `JANSET_SDL_EXTRACT_MISSING` when a tarball is absent, but the publish extraction path has no equivalent warning/error and the `.NETFramework` Windows copy path also silently no-ops when no DLLs match.
- **Why it matters:** publish and net462 outputs can miss native payload without an explicit, local failure signal, leaving detection to runtime `DllNotFoundException`.
- **Recommended fix:** add equivalent warning/error diagnostics for publish and framework-copy missing payload conditions (or fail fast in smoke contexts).
- **Tradeoff:** stricter diagnostics may surface latent package-shape issues immediately in existing consumer builds.

### [Low] Smoke restore is not hermetic to the local package feed

- **Location:** [build/msbuild/Janset.Smoke.props](../../build/msbuild/Janset.Smoke.props#L24), [nuget.config](../../nuget.config#L4), [nuget.config](../../nuget.config#L6)
- **Evidence type:** Observed in code / observed in command validation
- **Confidence:** High
- **The reality:** smoke config appends the local package feed via `RestoreAdditionalProjectSources` instead of replacing sources. Effective restore source set includes local feed plus `nuget.org` (and SDK offline library packs).
- **Observed command result:** restoring with isolated package cache (`-p:RestorePackagesPath=.../artifacts/temp/smoke-review-cache`) produced `obj/project.assets.json` with sources including `.../artifacts/packages` and `https://api.nuget.org/v3/index.json`.
- **Why it matters:** smoke remains reproducible for local Janset packages, but not fully hermetic: external source availability and ordering can still influence parts of restore behavior.
- **Recommended fix:** introduce an optional hermetic mode for smoke (`RestoreSources=$(LocalPackageFeed)` + explicit policy for analyzer dependencies), or document current mixed-source behavior as intentional non-hermetic design.
- **Tradeoff:** strict hermetic mode requires additional work to handle analyzer/tooling package resolution cleanly.

### [Low] Smoke guard targets lack direct blackbox coverage

- **Location:** [build/msbuild/Janset.Smoke.targets](../../build/msbuild/Janset.Smoke.targets#L17), [build/msbuild/Janset.Smoke.targets](../../build/msbuild/Janset.Smoke.targets#L22), [build/msbuild/Janset.Smoke.targets](../../build/msbuild/Janset.Smoke.targets#L32), [build/_build.Tests/Unit/Modules/Packaging/SmokeScopeComparatorTests.cs](../../build/_build.Tests/Unit/Modules/Packaging/SmokeScopeComparatorTests.cs#L26)
- **Evidence type:** Observed in code / observed in test inventory
- **Confidence:** High
- **The reality:** the MSBuild guard surface (`JNSMK001+`) is active in shared smoke targets, but current build-host tests cover scope-comparison logic and package-output checks rather than direct MSBuild guard execution.
- **Why it matters:** regressions in guard conditions (for example, property-name drift or PackageReference identity matching changes) can pass unit tests yet silently weaken the direct-invocation protection contract.
- **Recommended fix:** add one or two blackbox tests that run `dotnet build` against smoke projects in controlled temp feeds and assert the expected guard codes/messages (`JNSMK001`, `JNSMK002`) on missing inputs.
- **Tradeoff:** blackbox tests are slower and need robust output assertions, but they defend a user-facing safety boundary that pure unit tests cannot.

## Round 4 Addendum (Harvesting / Closure Slice)

### [High] Runtime-discovered dependency owner inference can degrade into filename tokens, breaking package-level policy and license attribution

- **Location:** [build/_build/Modules/Harvesting/BinaryClosureWalker.cs](../../build/_build/Modules/Harvesting/BinaryClosureWalker.cs#L110), [build/_build/Modules/Harvesting/BinaryClosureWalker.cs](../../build/_build/Modules/Harvesting/BinaryClosureWalker.cs#L111), [build/_build/Modules/Harvesting/BinaryClosureWalker.cs](../../build/_build/Modules/Harvesting/BinaryClosureWalker.cs#L195), [build/_build/Modules/Harvesting/BinaryClosureWalker.cs](../../build/_build/Modules/Harvesting/BinaryClosureWalker.cs#L206), [build/_build/Modules/Harvesting/ArtifactPlanner.cs](../../build/_build/Modules/Harvesting/ArtifactPlanner.cs#L63), [build/_build/Modules/Harvesting/ArtifactPlanner.cs](../../build/_build/Modules/Harvesting/ArtifactPlanner.cs#L81), [build/_build/Modules/Harvesting/ArtifactPlanner.cs](../../build/_build/Modules/Harvesting/ArtifactPlanner.cs#L87), [build/_build.Tests/Unit/Modules/Harvesting/ClosureBuildingTests.cs](../../build/_build.Tests/Unit/Modules/Harvesting/ClosureBuildingTests.cs#L154), [build/_build.Tests/Unit/Modules/Harvesting/ClosureBuildingTests.cs](../../build/_build.Tests/Unit/Modules/Harvesting/ClosureBuildingTests.cs#L174)
- **Evidence type:** Observed in code / observed in tests
- **Confidence:** High
- **The reality:** runtime-scan dependencies use `TryInferPackageNameFromPath` for owner attribution. The helper returns `segments[vcpkgIndex + 3]`; for typical `.../vcpkg_installed/<triplet>/bin/<file>` and `.../lib/<file>` paths this segment is the binary filename, not a package name. That owner token then flows into planner filtering and package-info lookups.
- **Why it matters:** any dependency discovered only at runtime-scan level can be attributed to a non-package token (for example, `SDL2.dll`), which weakens package-level filtering and can skip package license lookup paths.
- **Recommended fix:** replace segment-index inference with ownership resolution against `IPackageInfoProvider` data (or emit explicit `Unknown` plus strict-mode failure) and add tests that assert `OwnerPackage` correctness for runtime-discovered nodes.
- **Tradeoff:** stronger attribution may require extra lookup work and caching, but removes silent compliance drift.

### [Medium] Closure walk treats missing dependency metadata as non-fatal and can return success with a partial package graph

- **Location:** [build/_build/Modules/Harvesting/BinaryClosureWalker.cs](../../build/_build/Modules/Harvesting/BinaryClosureWalker.cs#L64), [build/_build/Modules/Harvesting/BinaryClosureWalker.cs](../../build/_build/Modules/Harvesting/BinaryClosureWalker.cs#L116)
- **Evidence type:** Observed in code
- **Confidence:** High
- **The reality:** when dependency package info lookup fails during queue traversal, the walker logs a warning and continues, then can still return a success closure.
- **Why it matters:** the harvest can be marked successful while metadata-driven package closure is incomplete, which weakens the trust boundary for downstream planning and license accounting.
- **Recommended fix:** introduce strict failure for unresolved non-system dependencies (or at minimum surface a typed degraded-closure result consumed as failure by `HarvestTask` in release validation mode).
- **Tradeoff:** stricter behavior may turn currently tolerated environment drift into explicit failures, but that is desirable for release-grade harvesting.

### [Low] Consolidated manifest RID ordering is source-enumeration order, not canonicalized

- **Location:** [build/_build/Tasks/Harvest/ConsolidateHarvestTask.cs](../../build/_build/Tasks/Harvest/ConsolidateHarvestTask.cs#L195), [build/_build/Tasks/Harvest/ConsolidateHarvestTask.cs](../../build/_build/Tasks/Harvest/ConsolidateHarvestTask.cs#L206), [build/_build/Tasks/Harvest/ConsolidateHarvestTask.cs](../../build/_build/Tasks/Harvest/ConsolidateHarvestTask.cs#L475)
- **Evidence type:** Observed in code
- **Confidence:** High
- **The reality:** `rid-status/*.json` files are loaded and emitted into `harvest-manifest.json` in file-enumeration order.
- **Why it matters:** output ordering can vary by filesystem/provider behavior, creating avoidable receipt churn and harder diffs for operators.
- **Recommended fix:** sort RID statuses deterministically (for example by RID then timestamp) before manifest materialization.
- **Tradeoff:** none beyond a tiny ordering change in generated manifests.

### [Validation Note]

- Harvesting slice validation command executed: `dotnet test build/_build.Tests/Build.Tests.csproj -c Release --nologo --filter "FullyQualifiedName~Unit.Tasks.Harvest|FullyQualifiedName~Unit.Modules.Harvesting"`.
- Result: test run completed green (reported `324/324` passing in this environment).

## Round 5 Addendum (Strategy / Triplet Contract Slice)

### [Medium] Strategy coherence accepts any suffix-less triplet as pure-dynamic, so malformed triplet names can pass PreFlight and fail late

- **Location:** [build/_build/Modules/Strategy/StrategyResolver.cs](../../build/_build/Modules/Strategy/StrategyResolver.cs#L58), [build/_build/Modules/Strategy/StrategyResolver.cs](../../build/_build/Modules/Strategy/StrategyResolver.cs#L67), [build/_build/Modules/Strategy/StrategyResolver.cs](../../build/_build/Modules/Strategy/StrategyResolver.cs#L70), [build/_build/Modules/Preflight/StrategyCoherenceValidator.cs](../../build/_build/Modules/Preflight/StrategyCoherenceValidator.cs#L26), [build/_build/Modules/Preflight/StrategyCoherenceValidator.cs](../../build/_build/Modules/Preflight/StrategyCoherenceValidator.cs#L28)
- **Evidence type:** Observed in code
- **Confidence:** High
- **The reality:** if a triplet contains neither `-hybrid` nor `-dynamic`, resolver treats it as a stock triplet and passes whenever strategy is `pure-dynamic`. There is no existence/allow-list validation at this layer.
- **Why it matters:** typo or malformed manifest triplets can clear strategy coherence and fail later during vcpkg execution, shifting errors from fast preflight to late operational phases.
- **Recommended fix:** add a second-stage triplet validity guard in preflight for suffix-less triplets (allow-list or vcpkg-query based), while preserving current strategy-model coherence check.
- **Tradeoff:** introduces a stricter preflight gate that may require keeping allow-list/query logic aligned with supported triplets.

### [Low] RID matching is case-sensitive in composition root and can throw opaque startup failures on casing drift

- **Location:** [build/_build/Program.cs](../../build/_build/Program.cs#L98), [build/_build/Program.cs](../../build/_build/Program.cs#L136)
- **Evidence type:** Observed in code
- **Confidence:** High
- **The reality:** runtime lookup uses `StringComparison.Ordinal` for RID matching in both runtime-profile creation and packaging-strategy resolution.
- **Why it matters:** a casing variant supplied through CLI/config can produce generic `Single`/not-found failures instead of deterministic, actionable RID diagnostics.
- **Recommended fix:** use case-insensitive matching (`OrdinalIgnoreCase`) plus explicit error messaging that includes the requested RID and known manifest RIDs.
- **Tradeoff:** none functionally; only tighter operator ergonomics.

### [Low] Hybrid-triplet behavioral invariants are documented centrally but not protected by automated contract tests

- **Location:** [vcpkg-overlay-triplets/_hybrid-common.cmake](../../vcpkg-overlay-triplets/_hybrid-common.cmake#L23), [vcpkg-overlay-triplets/_hybrid-common.cmake](../../vcpkg-overlay-triplets/_hybrid-common.cmake#L28), [vcpkg-overlay-triplets/_hybrid-common.cmake](../../vcpkg-overlay-triplets/_hybrid-common.cmake#L38), [build/_build.Tests/Unit/Modules/Preflight/StrategyCoherenceValidatorTests.cs](../../build/_build.Tests/Unit/Modules/Preflight/StrategyCoherenceValidatorTests.cs#L12), [build/_build.Tests/Unit/Modules/Preflight/StrategyCoherenceValidatorTests.cs](../../build/_build.Tests/Unit/Modules/Preflight/StrategyCoherenceValidatorTests.cs#L32)
- **Evidence type:** Observed in code / observed in tests
- **Confidence:** High
- **The reality:** strategy tests currently validate resolver/coherence behavior, but do not assert `_hybrid-common.cmake` invariants (default static linkage, SDL-family dynamic override, Linux RPATH fixup).
- **Why it matters:** accidental triplet/CMake drift can violate hybrid model assumptions while strategy tests and preflight still report green.
- **Recommended fix:** add a lightweight static contract test suite that parses overlay triplet files and asserts required invariants per platform.
- **Tradeoff:** introduces test maintenance when intentional triplet policy changes occur.

### [Validation Note]

- Strategy slice validation command executed: `dotnet test build/_build.Tests/Build.Tests.csproj -c Release --nologo --filter "FullyQualifiedName~Modules.Strategy|FullyQualifiedName~StrategyCoherenceValidator"`.
- Result: test run completed green (reported `324/324` passing in this environment).

## Round 6 Addendum (Build Context / Option Binding Slice)

### [High] `--dll` input contract is inconsistent with dependency tasks and causes immediate runtime crashes

- **Location:** [build/_build/Context/Options/DumpbinOptions.cs](../../build/_build/Context/Options/DumpbinOptions.cs#L7), [build/_build/Context/Options/DumpbinOptions.cs](../../build/_build/Context/Options/DumpbinOptions.cs#L10), [build/_build/Tasks/Dependency/DependentsTask.cs](../../build/_build/Tasks/Dependency/DependentsTask.cs#L16), [build/_build/Tasks/Dependency/LddTask.cs](../../build/_build/Tasks/Dependency/LddTask.cs#L16), [build/_build/Tasks/Dependency/OtoolAnalyzeTask.cs](../../build/_build/Tasks/Dependency/OtoolAnalyzeTask.cs#L24)
- **Evidence type:** Observed in code / observed in command validation
- **Confidence:** High
- **The reality:** CLI description says `--dll` is optional and that tasks can fall back to analyzing current-directory binaries. But `Dumpbin-Dependents` and `Ldd-Dependents` index `DllToDump[0]` unconditionally, so invoking those targets without `--dll` throws `IndexOutOfRangeException`.
- **Observed command result:**
	- `dotnet run --project build/_build -- --target Dumpbin-Dependents` -> fails with `Index was out of range. Must be non-negative and less than the size of the collection. (Parameter 'index')`.
	- `dotnet run --project build/_build -- --target Ldd-Dependents` -> fails with the same exception.
- **Why it matters:** this is a direct operator-facing crash on a documented command path; failures are non-actionable and look like internal bugs.
- **Recommended fix:** align contract and behavior. Either make `--dll` mandatory for `Dumpbin-Dependents`/`Ldd-Dependents` with explicit validation + clear error text, or implement real fallback discovery (the behavior currently described in option help).
- **Tradeoff:** stricter validation changes current invocation expectations but improves reliability and diagnostics.

### [Medium] Explicit invalid `--repo-root` is silently ignored instead of failing fast

- **Location:** [build/_build/Program.cs](../../build/_build/Program.cs#L216), [build/_build/Program.cs](../../build/_build/Program.cs#L221), [build/_build/Program.cs](../../build/_build/Program.cs#L243), [build/_build.Tests/Unit/CompositionRoot/ProgramCompositionRootTests.cs](../../build/_build.Tests/Unit/CompositionRoot/ProgramCompositionRootTests.cs#L123)
- **Evidence type:** Observed in code / observed in tests / observed in command validation
- **Confidence:** High
- **The reality:** `DetermineRepoRootAsync` accepts `--repo-root` only when `DirectoryInfo.Exists == true`; when caller passes a non-existent explicit path, code silently falls back to git discovery instead of treating user input as invalid.
- **Observed command result:** `dotnet run --project build/_build -- --target Info --repo-root Z:\definitely-not-existing-path-12345` still succeeds and uses git-derived repo root.
- **Why it matters:** explicit operator intent is discarded; path typos can run against a different repository root than requested.
- **Recommended fix:** if `--repo-root` is present but non-existent, fail immediately with a clear `CakeException` message; keep git fallback only for omitted argument.
- **Tradeoff:** stricter input validation can break scripts that currently rely on silent fallback, but it makes behavior deterministic.

### [Medium] Invalid explicit `--vcpkg-dir` and `--vcpkg-installed-dir` paths are treated as "not specified" and silently downgraded

- **Location:** [build/_build/Modules/PathService.cs](../../build/_build/Modules/PathService.cs#L26), [build/_build/Modules/PathService.cs](../../build/_build/Modules/PathService.cs#L34), [build/_build/Modules/PathService.cs](../../build/_build/Modules/PathService.cs#L40), [build/_build/Modules/PathService.cs](../../build/_build/Modules/PathService.cs#L47)
- **Evidence type:** Observed in code / observed in command validation
- **Confidence:** High
- **The reality:** when explicit directory arguments are supplied but do not exist, `PathService` falls back to repo-default paths and logs warnings that say "not specified" rather than "invalid explicit path".
- **Observed command result:** `dotnet run --project build/_build -- --target Info --vcpkg-dir Z:\definitely-not-existing-path-12345 --vcpkg-installed-dir Z:\definitely-not-existing-path-67890` succeeds and falls back to default repo paths with "not specified" warnings.
- **Why it matters:** invalid operator input is hidden; builds may continue against unintended vcpkg state and complicate incident triage.
- **Recommended fix:** detect explicit-but-invalid paths separately and fail fast (or at minimum emit distinct hard warnings that include provided invalid path and fallback decision).
- **Tradeoff:** fail-fast is operationally safer but less permissive for ad-hoc local runs.

### [Low] Negative-path coverage is missing for key option-binding failure contracts

- **Location:** [build/_build.Tests/Unit/CompositionRoot/ProgramCompositionRootTests.cs](../../build/_build.Tests/Unit/CompositionRoot/ProgramCompositionRootTests.cs#L123), [build/_build.Tests/Unit/CompositionRoot/ProgramCompositionRootTests.cs](../../build/_build.Tests/Unit/CompositionRoot/ProgramCompositionRootTests.cs#L135), [build/_build.Tests/Unit/CompositionRoot/ProgramCompositionRootTests.cs](../../build/_build.Tests/Unit/CompositionRoot/ProgramCompositionRootTests.cs#L187)
- **Evidence type:** Observed in tests
- **Confidence:** High
- **The reality:** composition-root tests cover helper logic and strategy DI resolution but do not assert invalid explicit path behavior (`--repo-root`, `--vcpkg-dir`) or missing `--dll` handling for dependency targets.
- **Why it matters:** the exact failure modes above are currently regression-prone because they are not codified as executable contracts.
- **Recommended fix:** add targeted negative-path tests for (1) invalid explicit repo root, (2) invalid explicit vcpkg paths, and (3) dependency task behavior when `DllToDump` is empty.
- **Tradeoff:** minimal additional test maintenance for significantly better operator-contract stability.

### [Validation Note]

- Surface 3 targeted test command executed: `dotnet test build/_build.Tests/Build.Tests.csproj -c Release --nologo --filter "FullyQualifiedName~CompositionRoot|FullyQualifiedName~Unit.Context"`.
- Result: test run completed green (reported `324/324` passing in this environment).
- Additional command-level behavior validation executed:
	- `dotnet run --project build/_build -- --target Info --repo-root Z:\definitely-not-existing-path-12345`
	- `dotnet run --project build/_build -- --target Info --vcpkg-dir Z:\definitely-not-existing-path-12345 --vcpkg-installed-dir Z:\definitely-not-existing-path-67890`
	- `dotnet run --project build/_build -- --target Dumpbin-Dependents`
	- `dotnet run --project build/_build -- --target Ldd-Dependents`

## Round 7 Addendum (Canonical Docs Consistency / Operational Drift Slice)

### [High] Phase-canonical docs still describe PackageTask and Phase 2 priorities as pre-implementation, conflicting with canonical plan reality

- **Location:** [docs/phases/README.md](../phases/README.md#L21), [docs/phases/README.md](../phases/README.md#L23), [docs/phases/README.md](../phases/README.md#L24), [docs/phases/phase-2-cicd-packaging.md](../phases/phase-2-cicd-packaging.md#L42), [docs/phases/phase-2-cicd-packaging.md](../phases/phase-2-cicd-packaging.md#L44), [docs/phases/phase-2-cicd-packaging.md](../phases/phase-2-cicd-packaging.md#L191), [docs/plan.md](../plan.md#L197)
- **Evidence type:** Observed in docs
- **Confidence:** High
- **The reality:** phase-level docs still prioritize "implement PackageTask" and "make RC pipeline functional" as if PackageTask were missing, while `plan.md` records PackageTask/consumer-smoke proof-slice as landed and now frames remaining work under Stream C / 2b expansion.
- **Why it matters:** these are canonical navigation docs. New work can be directed toward already-landed items instead of current risk areas, creating execution churn.
- **Recommended fix:** align `phases/README.md` and `phase-2-cicd-packaging.md` with `plan.md` current state (proof-slice landed, remaining 7-RID matrix and CI-gate generalization pending).
- **Tradeoff:** docs-only churn; no runtime impact.

### [High] Managed-only local quick-start currently fails on the documented main-solution build path

- **Location:** [docs/playbook/local-development.md](../playbook/local-development.md#L21), [docs/playbook/local-development.md](../playbook/local-development.md#L31), [docs/playbook/local-development.md](../playbook/local-development.md#L32), [docs/playbook/local-development.md](../playbook/local-development.md#L35), [tests/smoke-tests/package-smoke/PackageConsumer.Smoke/PackageConsumer.Smoke.csproj](../../tests/smoke-tests/package-smoke/PackageConsumer.Smoke/PackageConsumer.Smoke.csproj#L1), [tests/smoke-tests/package-smoke/Compile.NetStandard/Compile.NetStandard.csproj](../../tests/smoke-tests/package-smoke/Compile.NetStandard/Compile.NetStandard.csproj#L1)
- **Evidence type:** Observed in docs / observed in command validation
- **Confidence:** High
- **The reality:** the playbook says managed-only contributors can run `dotnet restore/build Janset.SDL2.sln` and compile managed code fine. In this environment, `dotnet build Janset.SDL2.sln -c Release --nologo` fails with smoke-project restore errors (`NU1603`, `NU1605`) before the promised managed-only happy path.
- **Observed command result:** solution build failed with 12 restore errors from smoke projects, including sentinel-version resolution and package downgrade failures.
- **Why it matters:** this is the first-run contributor path; failure at step 1 undermines docs trust and onboarding velocity.
- **Recommended fix:** either (A) remove smoke projects from default solution build path for managed-only docs flow, or (B) update quick-start to use stable managed/build-host commands that are green today.
- **Tradeoff:** A changes solution composition; B keeps composition but requires clearer command split in docs.

### [Medium] Single-source-of-truth config policy drifts in release pipeline scaffold (legacy file reference + dummy artifact flow)

- **Location:** [docs/plan.md](../plan.md#L36), [.github/workflows/release-candidate-pipeline.yml](../../.github/workflows/release-candidate-pipeline.yml#L47), [.github/workflows/release-candidate-pipeline.yml](../../.github/workflows/release-candidate-pipeline.yml#L134), [.github/workflows/release-candidate-pipeline.yml](../../.github/workflows/release-candidate-pipeline.yml#L250)
- **Evidence type:** Observed in code and docs
- **Confidence:** High
- **The reality:** canonical policy says merged `manifest.json` is the single source of truth, but RC workflow scaffold still echoes `build/runtimes.json` and produces placeholder/dummy harvest/package outputs.
- **Why it matters:** when this scaffold is resumed, it carries a legacy config assumption and fake-output patterns that can leak into real implementation.
- **Recommended fix:** remove legacy `runtimes.json` reference now and replace placeholder blocks with explicit TODO stubs that cannot be mistaken for production logic.
- **Tradeoff:** minor workflow cleanup now reduces future implementation ambiguity.

### [Medium] linux-arm64 runner mapping remains split between manifest authority and workflow/docs values

- **Location:** [build/manifest.json](../../build/manifest.json#L14), [.github/workflows/prepare-native-assets-main.yml](../../.github/workflows/prepare-native-assets-main.yml#L57), [docs/onboarding.md](../onboarding.md#L187)
- **Evidence type:** Observed in code and docs
- **Confidence:** High
- **The reality:** `manifest.json` runtime row uses `ubuntu-24.04-arm` while orchestrator workflow and onboarding table still use `ubuntu-24.04-arm`.
- **Why it matters:** this preserves a known authority split in operational targeting. Any automation that assumes manifest authority can diverge from actual workflow execution.
- **Recommended fix:** choose one authoritative runner label for linux-arm64 and align manifest + orchestrator + onboarding in one change.
- **Tradeoff:** none beyond synchronized docs/workflow update.

### [Medium] Onboarding repository topology points to a non-existent test path

- **Location:** [docs/onboarding.md](../onboarding.md#L127), [build/_build.Tests](../../build/_build.Tests)
- **Evidence type:** Observed in docs / observed in repository structure
- **Confidence:** High
- **The reality:** onboarding tree lists `tests/Build.Tests/`, but build-host tests live under `build/_build.Tests/`.
- **Why it matters:** onboarding is the primary entry doc; wrong path guidance slows navigation and confuses contributors mapping docs to code.
- **Recommended fix:** update onboarding repository layout block to match actual build-host test location.
- **Tradeoff:** docs-only fix.

### [Low] Canonical status telemetry is internally inconsistent and stale for build-host test counts

- **Location:** [docs/onboarding.md](../onboarding.md#L215), [docs/plan.md](../plan.md#L172), [docs/plan.md](../plan.md#L178), [docs/plan.md](../plan.md#L197), [docs/plan.md](../plan.md#L332)
- **Evidence type:** Observed in docs / observed in command validation
- **Confidence:** High
- **The reality:** canonical docs concurrently state `241`, `247`, and `273` passing build-host tests, while current targeted run reports `324` passing.
- **Why it matters:** conflicting telemetry weakens canonical-doc trust during release/readiness discussions.
- **Recommended fix:** normalize counts to a single dated statement (or switch to trend-style wording that avoids hardcoding unstable totals).
- **Tradeoff:** none.

### [Validation Note]

- Docs-surface operational validation command executed: `dotnet build Janset.SDL2.sln -c Release --nologo`.
- Result: failed with smoke-project restore errors (`NU1603`, `NU1605`) under the documented managed-only flow.
- Additional drift checks executed:
	- verified missing path expectation `tests/Build.Tests/**` (no files) vs actual `build/_build.Tests/**`.
	- verified legacy triplet file absence for `x64-windows-release` under both `vcpkg-overlay-triplets/` and `external/vcpkg/triplets/`.
