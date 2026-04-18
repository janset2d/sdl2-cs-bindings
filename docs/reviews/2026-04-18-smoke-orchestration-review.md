# Review - Smoke Orchestration Deep Dive

**Date:** 2026-04-18
**Status:** Ongoing
**Mode:** Read-only review
**Validation performed:**

- Targeted code and docs inspection of the shared smoke MSBuild contract, smoke consumer csprojs, and `PackageConsumerSmokeRunner`
- Direct `dotnet build tests/smoke-tests/package-smoke/PackageConsumer.Smoke/PackageConsumer.Smoke.csproj -c Release --nologo` -> failed with `JNSMK001`
- Direct `dotnet build ... -p:LocalPackageFeed=<artifacts/packages>` -> failed with `JNSMK002`
- Generated NuGet restore spec inspection showed smoke `VersionOverride` values materialized as `[1.3.0-validation.win64.1, )`, not exact pins
- Controlled `dotnet restore` against `Compile.NetStandard.csproj` with mixed family versions -> failed with `NU1605`, proving lower-bound range semantics are live, not theoretical

## Scope And Assumptions

This pass focused on the smoke orchestration surface rather than the broader package or build-host system:

- `build/msbuild/Janset.Smoke.props`
- `build/msbuild/Janset.Smoke.targets`
- `tests/smoke-tests/package-smoke/PackageConsumer.Smoke/PackageConsumer.Smoke.csproj`
- `tests/smoke-tests/package-smoke/Compile.NetStandard/Compile.NetStandard.csproj`
- `build/_build/Modules/Packaging/PackageConsumerSmokeRunner.cs`
- the canonical smoke docs in `docs/playbook/cross-platform-smoke-validation.md`

The review target here is the internal validation harness. This is intentionally narrower than the repo's published package dependency policy. `plan.md` currently and explicitly says shipped package dependencies are minimum-range under the S1 decision; this note is about whether the internal smoke harness is deterministic enough to prove it validated the package set produced by the current run.

## Findings

### [High] The smoke harness does not actually pin the orchestrator-selected package set

- **Location:** [Janset.Smoke.props](../../build/msbuild/Janset.Smoke.props#L29), [Janset.Smoke.props](../../build/msbuild/Janset.Smoke.props#L32), [Janset.Smoke.props](../../build/msbuild/Janset.Smoke.props#L37), [PackageConsumer.Smoke.csproj](../../tests/smoke-tests/package-smoke/PackageConsumer.Smoke/PackageConsumer.Smoke.csproj#L17), [PackageConsumer.Smoke.csproj](../../tests/smoke-tests/package-smoke/PackageConsumer.Smoke/PackageConsumer.Smoke.csproj#L25), [Compile.NetStandard.csproj](../../tests/smoke-tests/package-smoke/Compile.NetStandard/Compile.NetStandard.csproj#L17), [Compile.NetStandard.csproj](../../tests/smoke-tests/package-smoke/Compile.NetStandard/Compile.NetStandard.csproj#L22), [PackageConsumerSmokeRunner.cs](../../build/_build/Modules/Packaging/PackageConsumerSmokeRunner.cs#L306), [PackageConsumerSmokeRunner.cs](../../build/_build/Modules/Packaging/PackageConsumerSmokeRunner.cs#L316), [plan.md](../plan.md#L45)
- **Evidence type:** Observed in code / observed in generated restore metadata / observed in executable validation
- **Confidence:** High
- **The reality:** the runner injects one concrete version per family, and the smoke csprojs consume those values through `VersionOverride`. But in the current shape, NuGet materializes those references as lower-bound ranges (`[x, )`), not exact pins. That matches the repo's S1 minimum-range policy for shipped packages, but these smoke projects are internal probes with `IsPackable=false`, not pack inputs. In this session the generated `.nuget.dgspec.json` files showed `[1.3.0-validation.win64.1, )`, and a controlled restore against `Compile.NetStandard` failed with `NU1605` because NuGet treated the direct requests as `>= 1.3.0-smoke.5` while a transitive dependency required `>= 1.3.0-validation.win64.1`.
- **Why it matters:** the smoke harness is supposed to validate the package set produced by the current run. Today it can instead validate any compatible sibling set that happens to be available in the local feed, or fail on mixed-version downgrade behavior that has nothing to do with the exact package set the runner selected. With the current `artifacts/packages` feed already containing many parallel prerelease variants, this is not hypothetical.
- **Recommended fix:** if the intent is exact current-run validation, make the smoke-only `PackageReference`s exact-versioned, for example with bracket notation in the smoke csprojs only. That does not reopen the S1 published-package decision because these projects are internal probes and are not packed. If exact current-run validation is not the intent, the playbook should say so explicitly, because the current behavior is materially weaker than "validate the local packages just produced."
- **Tradeoff:** strict smoke pinning makes messy local feeds fail earlier, but that is the correct behavior for an orchestrator-owned validation harness.

### [Medium] Direct-invocation guards are real, but there is still no automated blackbox coverage for the actual MSBuild and restore contract

- **Location:** [Janset.Smoke.targets](../../build/msbuild/Janset.Smoke.targets#L17), [Janset.Smoke.targets](../../build/msbuild/Janset.Smoke.targets#L22), [Janset.Smoke.targets](../../build/msbuild/Janset.Smoke.targets#L32), [FamilyIdentifierConventionsTests.cs](../../build/_build.Tests/Unit/Modules/Preflight/FamilyIdentifierConventionsTests.cs#L43), [SmokeScopeComparatorTests.cs](../../build/_build.Tests/Unit/Modules/Packaging/SmokeScopeComparatorTests.cs#L8)
- **Evidence type:** Observed in code / observed in test inventory / observed in executable validation
- **Confidence:** High
- **The reality:** the shared guard layer works as documented: direct invocation without `LocalPackageFeed` fails at `JNSMK001`, and supplying only the feed moves the failure to `JNSMK002`. That part is fine. The gap is that Build.Tests only defends naming and scope-comparison logic around this contract; it does not execute a real smoke project through `dotnet build` or `dotnet restore` and assert the guard codes or the post-guard restore semantics.
- **Why it matters:** this is exactly the kind of surface where a green unit suite can lie by omission. Property-name drift, guard-condition drift, or restore-shape regressions can land without any automated test noticing, and the first person to discover the break is the contributor or CI job invoking the real smoke path.
- **Recommended fix:** add one or two small blackbox tests that run a smoke csproj in a temporary workspace or feed and assert `JNSMK001` and `JNSMK002`. If the harness is tightened to exact internal pinning, add one assertion against the generated restore spec so the test proves the reference shape is `[x]` rather than `[x, )`.
- **Tradeoff:** blackbox tests are slower and slightly more operationally fussy, but this is a user-facing safety boundary and not the kind of contract unit tests can honestly cover alone.

### [Medium] Smoke completeness still stops at the `-r <rid>` path, which is a narrower contract than real default consumers

- **Location:** [PackageConsumerSmokeRunner.cs](../../build/_build/Modules/Packaging/PackageConsumerSmokeRunner.cs#L287), [PackageConsumerSmokeRunner.cs](../../build/_build/Modules/Packaging/PackageConsumerSmokeRunner.cs#L296), [cross-platform-smoke-validation.md](../playbook/cross-platform-smoke-validation.md#L390), [cross-platform-smoke-validation.md](../playbook/cross-platform-smoke-validation.md#L409), [phase-2-adaptation-plan.md](../phases/phase-2-adaptation-plan.md#L566)
- **Evidence type:** Observed in code and docs
- **Confidence:** High
- **The reality:** the runner always executes the runtime smoke with `-r <rid>`. The playbook and PD-10 already explain what that validates: runtime-specific restore and SDK-level copy of native assets into `bin/`. They also explicitly note what it does not validate: the default framework-dependent resolver path that loads native assets from the NuGet cache without an explicit runtime identifier.
- **Why it matters:** even after the version-selection issue above is fixed, green smoke would still only prove the `-r <rid>` subset unless PD-10 is resolved. A regression in the default consumer path can still hide behind a green local-feed smoke run.
- **Recommended fix:** resolve PD-10 explicitly before treating this surface as complete consumer validation. Either keep the current behavior and document it as a deliberate D-local subset, or add a second no-`-r` invocation path so the smoke runner covers both the copy-to-bin and framework-dependent resolver flows.
- **Tradeoff:** a second invocation path increases runtime and platform complexity, but it closes a real behavioral gap rather than adding decorative coverage.

## Broader Systemic Observations

- The direct-invocation contract is better than it first looked. `JNSMK001` and `JNSMK002` are not imaginary docs-only guardrails; they do fire in the expected order.
- The most important smoke defect is not guard absence. It is that once the guard layer passes, the restore shape is looser than the runner's intent.
- This review does not challenge the S1 decision recorded in [plan.md](../plan.md#L45). Published package dependencies can remain minimum-range while the internal smoke harness becomes stricter and more deterministic.

## Open Questions / Confidence Limiters

- Is the intended contract "validate the exact package set produced by this run" or "validate any compatible set currently present in `artifacts/packages`"?
- Should PD-10 be resolved during Phase 2b, or is the current `-r <rid>` subset considered sufficient for the local smoke role?

## What Was Not Verified

- I did not run the full `PostFlight` task end-to-end in this review pass.
- I did not run live Linux or macOS smoke commands from this machine.
- I did not build a temporary prototype showing bracketed exact smoke references; the recommendation is based on the observed current restore shape, not on a new implementation attempt.

## Short Summary

The smoke guard layer is real and correctly ordered, but the smoke harness is still not deterministic enough to prove it validated the exact package set chosen by the orchestrator. The remaining second-order caveat is already documented in PD-10: even a green smoke run currently proves only the `-r <rid>` consumer path.
