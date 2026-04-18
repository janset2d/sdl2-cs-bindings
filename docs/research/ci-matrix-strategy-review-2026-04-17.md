# CI Matrix Strategy Review — PA-1

**Date:** 2026-04-17
**Status:** Proposed recommendation for PA-1 closure
**Scope:** Stream C matrix shape only
**Related:** [docs/phases/phase-2-adaptation-plan.md](../phases/phase-2-adaptation-plan.md), [docs/knowledge-base/release-lifecycle-direction.md](../knowledge-base/release-lifecycle-direction.md), [docs/playbook/cross-platform-smoke-validation.md](../playbook/cross-platform-smoke-validation.md)

## Decision Summary

**Recommendation:** keep the CI build matrix **RID-only** for Stream C.

PA-1 should close by reaffirming the current policy in [release-lifecycle-direction.md](../knowledge-base/release-lifecycle-direction.md#5-ci-matrix-model): one matrix job per runtime entry in `manifest.json`, no extra `strategy` axis, no parity job in the default CI path. Strategy coverage should be improved by:

1. finishing PA-2 (overlay hybrid triplets for the remaining 4 RIDs), and
2. explicitly deciding whether any RID remains pure-dynamic after PA-2.

If a pure-dynamic fallback remains after PA-2, it should still run as a **single RID job**, not as a second axis. Any future cross-strategy comparison should be a targeted Phase 2b validation workflow, not the baseline Stream C matrix.

## Why This Review Exists

PA-1 is open because the repo has two truths that need to be reconciled before Stream C:

- Canonical policy already says the CI build matrix is RID-only, 7 jobs, with per-library harvest inside each job.
- The adaptation plan still leaves room to revisit whether CI should add a `strategy` axis or a parity-style validation job before matrix generation and workflow migration are implemented.

That ambiguity is now expensive because Stream C needs a concrete output shape for `GenerateMatrixTask`, and the current orchestrator workflow is already stale relative to `manifest.json`.

## Current Repo Facts

### 1. Strategy does not dispatch enough behavior to justify a default `strategy × RID` matrix

Today the strategy layer differentiates only two runtime behaviors:

1. which `IDependencyPolicyValidator` instance DI resolves during harvest, and
2. whether PreFlight's declarative strategy coherence check passes.

This is narrower than a typical strategy-driven pipeline:

- `IPackagingStrategy` is effectively an `IsCoreLibrary(string vcpkgName)` helper.
- `HybridStaticValidator` has real behavior: fail or warn on transitive dependency leaks.
- `PureDynamicValidator` is an intentional pass-through.
- Packaging, package validation, consumer smoke, and deployer flow do not branch on strategy today.

Operational implication: adding a `strategy` axis to CI would mostly duplicate the expensive build/install work while exercising only a very small amount of extra behavior.

### 2. The proof slice that already passed is hybrid-only

The 2026-04-17 three-platform validation covered:

- `win-x64`
- `linux-x64`
- `osx-x64`

All three are hybrid-static RIDs. The four remaining runtime entries are still declared pure-dynamic in `manifest.json` today. The current post-S1 proof slice therefore validates the packaging and consumer spine on one strategy only.

This is a real gap, but it is not evidence that the matrix shape should change. It is evidence that PA-2 and the remaining strategy-allocation decision still need to happen.

### 3. The current workflow drift is about source-of-truth shape, not about missing a strategy axis

`build/manifest.json` is the source of truth for runtimes and strategy allocation, but `.github/workflows/prepare-native-assets-main.yml` still hardcodes pre-alignment triplets such as:

- `x64-windows-release`
- `x64-linux-dynamic`
- `x64-osx-dynamic`

That drift argues for dynamic matrix generation from `manifest.json`. It does **not** argue for introducing a second CI axis. The repo's current pain is stale orchestration, not lack of cross-product coverage.

### 4. PA-2 is the real mechanism gap

Only three overlay hybrid triplets exist today:

- `x64-windows-hybrid`
- `x64-linux-hybrid`
- `x64-osx-hybrid`

The remaining four RIDs still use stock triplets. Until PA-2 lands, a 7-job matrix cannot exercise hybrid validation everywhere even if Stream C is perfect. That is a mechanism gap, not a matrix-shape gap.

## Options Considered

### Option A — Keep RID-only matrix

#### Option A Shape

- One CI job per runtime entry in `manifest.json`
- `GenerateMatrixTask` emits 7 runtime rows
- Strategy is a property on each row, not an axis
- Harvest remains the secondary per-library axis inside each job

#### Option A Pros

- Matches already-locked policy in `release-lifecycle-direction.md`
- Matches the actual build cost model: one vcpkg install per triplet, all libraries inside the job
- Minimizes CI cost and cache churn
- Keeps `GenerateMatrixTask` trivial and manifest-driven
- Lets PA-2 solve the actual coverage gap by expanding hybrid overlays to the remaining RIDs
- Avoids inventing a second artifact contract before Stream C even lands

#### Option A Cons

- If one or more RIDs intentionally stays pure-dynamic after PA-2, the pure-dynamic path still gets only single-path validation
- Does not provide automatic cross-strategy comparison on the same platform

#### Option A Assessment

Best fit for the codebase as it exists now.

### Option B — Add `strategy × RID` parity axis

#### Option B Shape

- Multiply each RID row by one or more strategy variants
- Same platform potentially runs hybrid and pure-dynamic jobs side by side

#### Option B Pros

- Maximum explicit parity coverage if the repo truly intends to support both strategies as first-class long-term outputs
- Surfaces strategy-specific regressions earlier

#### Option B Cons

- Roughly doubles CI cost for parity-covered rows
- Duplicates the most expensive step: vcpkg install/build per strategy-specific triplet
- Needs additional output naming, artifact routing, and matrix-generation rules before Stream C can even start
- Current code does not have enough strategy-dependent downstream behavior to justify this default cost
- Pure-dynamic validation is currently a no-op, so half of the extra matrix would mostly prove that the repo can do extra work

#### Option B Assessment

Overbuilt for the current repo state. This option makes sense only if the project explicitly commits to keeping both hybrid and pure-dynamic as equally supported release behaviors.

### Option C — Add a parity job / cross-strategy validation job

#### Option C Shape

- Keep RID-only build matrix
- Add one extra job that re-validates a built artifact under the alternate strategy expectation, or re-runs a selected subset under both strategy assumptions

#### Option C Pros

- Cheaper than a full `strategy × RID` expansion
- Keeps the main matrix compact

#### Option C Cons

- Introduces a new artifact contract before Stream C has even replaced the stale hardcoded matrix
- Risk of testing a synthetic scenario that does not match shipped behavior
- Still weak value today because packaging and consumer smoke are strategy-agnostic, while pure-dynamic validation remains pass-through
- Harder to explain and maintain than the RID-only baseline

#### Option C Assessment

More complexity than signal in the current codebase. If parity evidence becomes necessary later, it should arrive as a deliberately scoped Phase 2b workflow, not as a prerequisite for Stream C.

## Decision Criteria

The matrix model should optimize for the repo's current reality, not an imagined future architecture. The criteria that matter now are:

1. **Source-of-truth alignment** — `manifest.json` must be the only authority for runtime rows.
2. **Signal per CI minute** — extra jobs should only exist when they exercise meaningful extra behavior.
3. **Compatibility with PA-2** — the chosen shape must not pre-judge the outcome of the remaining hybrid-overlay expansion.
4. **Low migration risk for Stream C** — `GenerateMatrixTask` and workflow migration should reduce drift first, not introduce a new contract surface.
5. **Honesty about strategy state** — do not design CI around a dispatcher model the code does not currently implement.

Option A is the only choice that scores well on all five.

## Recommended Resolution

Close PA-1 with this explicit statement:

> Stream C keeps a RID-only matrix. `GenerateMatrixTask` emits one row per `manifest.runtimes[]` entry, carrying `rid`, `triplet`, `strategy`, `runner`, and `container_image` as row metadata. Strategy remains metadata, not a top-level axis. Cross-strategy parity is not part of the default Phase 2a/Stream C CI surface.

And pair that closure with two follow-on rules:

1. **PA-2 remains mandatory.** The four remaining RIDs need hybrid overlay triplets unless the repo explicitly decides to keep some of them pure-dynamic.
2. **Any surviving pure-dynamic RIDs need an explicit behavioral contract before release.** That contract can still be exercised inside a RID-only matrix row. It does not require a second CI axis.

## Consequences for Stream C

If this recommendation is accepted, Stream C should implement the following shape:

1. `GenerateMatrixTask` reads `manifest.runtimes[]` and emits the runtime rows directly.
2. GitHub Actions replaces hardcoded matrices in `.github/workflows/prepare-native-assets-main.yml` with `fromJson()` output.
3. Affected-family filtering stays inside the job at the harvest axis, as already specified in the adaptation plan.
4. Strategy-specific branching remains limited to runtime metadata and validator resolution unless later code changes add a real downstream strategy seam.

## What Would Reopen This Decision

PA-1 should only be revisited if one of these becomes true:

1. Packaging starts emitting materially different nupkg layouts for hybrid and pure-dynamic RIDs.
2. Consumer smoke needs to prove both strategies as first-class supported release behaviors on the same RID.
3. The repo intentionally decides to keep a durable hybrid + pure-dynamic dual-support policy across the same architecture family.

None of those are true today.

## Recommended Next Step After PA-1

Proceed directly to PA-2.

That is the mechanism work that makes the RID-only policy actually valuable: once overlay triplets cover the remaining RIDs, the same 7-row manifest-driven matrix stops being a partial hybrid proof and becomes the real coverage surface.
