# Review — Packaging Consumer Path Deep Dive

**Date:** 2026-04-18
**Status:** Ongoing
**Mode:** Read-only review
**Validation performed:** code and docs inspection only; no build or smoke commands were executed in this review pass

## Scope And Assumptions

Reviewed the active Phase 2 packaging and consumer-delivery path centered on:

- `build/_build/Modules/Packaging/PackageTaskRunner.cs`
- `build/_build/Modules/Packaging/PackageOutputValidator.cs`
- `build/_build/Modules/Packaging/PackageConsumerSmokeRunner.cs`
- `src/native/_shared/Janset.SDL2.Native.Common.targets`
- the related package-smoke csproj and canonical docs

There was no working-tree diff to anchor the review against, so this pass treats the current live implementation as the review target.

## Findings

### [High] .NET Framework AnyCPU fallback silently hard-codes `win-x64`

- **Location:** [`src/native/_shared/Janset.SDL2.Native.Common.targets`](../../src/native/_shared/Janset.SDL2.Native.Common.targets#L79), [`src/native/_shared/Janset.SDL2.Native.Common.targets`](../../src/native/_shared/Janset.SDL2.Native.Common.targets#L177), [`build/_build/Modules/Packaging/PackageConsumerSmokeRunner.cs`](../../build/_build/Modules/Packaging/PackageConsumerSmokeRunner.cs#L296)
- **Evidence type:** Observed in code
- **Confidence:** High
- **The reality:** when `RuntimeIdentifier` is unset and the target is `.NETFramework`, the shared consumer target falls through to `win-x64` unconditionally. The same target file still frames this path as the generic AnyCPU fallback, while the current smoke runner forces `-r <rid>` and only proves the `win-x64` branch on Windows.
- **Why it matters:** this is an x64-biased fallback presented as architecture-neutral support. A net462 consumer that does not declare `Platform` or `RuntimeIdentifier` can receive the wrong native payload and fail at runtime on non-x64 hosts or 32-bit execution paths.
- **Recommended fix:** stop treating unset AnyCPU as safely inferable. Either fail fast for `.NETFramework` when both `Platform` and `RuntimeIdentifier` are unset, or implement real architecture-aware fallback and validate it with x86 and arm64 consumer runs.
- **Tradeoff:** a fail-fast contract is stricter for consumers, but it is materially safer than copying the wrong DLL set.

### [Medium] G48 validates payload shape for shipped RIDs, not completeness against the configured runtime matrix

- **Location:** [`build/_build/Modules/Packaging/PackageTaskRunner.cs`](../../build/_build/Modules/Packaging/PackageTaskRunner.cs#L183), [`build/_build/Modules/Packaging/PackageTaskRunner.cs`](../../build/_build/Modules/Packaging/PackageTaskRunner.cs#L191), [`build/_build/Modules/Packaging/PackageOutputValidator.cs`](../../build/_build/Modules/Packaging/PackageOutputValidator.cs#L841), [`build/_build/Modules/Packaging/PackageOutputValidator.cs`](../../build/_build/Modules/Packaging/PackageOutputValidator.cs#L851)
- **Evidence type:** Observed in code
- **Confidence:** High
- **The reality:** `PackageTaskRunner` only requires a non-zero successful RID set and logs whichever successful RIDs exist. `PackageOutputValidator` then discovers `runtimes/<rid>/native/` roots that are actually present in the `.nupkg` and validates shape for those roots only. It never compares the package contents against `manifest.json` runtimes or any explicit expected RID set.
- **Why it matters:** a native package can silently omit configured RIDs and still pass G48. That is a release-guardrail gap once the repo expects fat `.Native` packages to cover the declared runtime matrix rather than a proof-slice subset.
- **Recommended fix:** introduce an explicit expected-RID contract for pack validation. If D-local proof-slice runs are intentionally partial, pass the allowed RID set explicitly; if a release run expects all manifest rows, assert all of them are present before the package goes green.
- **Tradeoff:** wiring G48 directly to all manifest rows today would break the intentional proof-slice workflow. The fix needs an explicit scope contract, not a hard-coded 7-RID assumption.

### [Low] `plan.md` still contradicts itself about shipping-graph `buildTransitive` support

- **Location:** [`docs/plan.md`](../plan.md#L49), [`docs/plan.md`](../plan.md#L197), [`src/native/_shared/Janset.SDL2.Native.Common.targets`](../../src/native/_shared/Janset.SDL2.Native.Common.targets#L1)
- **Evidence type:** Observed in code and docs
- **Confidence:** High
- **The reality:** one section of `plan.md` still says repo-wide `buildTransitive/*.targets` and Unix untar support are not implemented and that only `Janset.SDL2.Core.Native.targets` exists. Later in the same file, the plan correctly states that the shared `Janset.SDL2.Native.Common.targets` consumer contract was added and verified on the original 3-RID slice.
- **Why it matters:** `plan.md` is supposed to be canonical status. This contradiction makes already-landed work look partial in one part of the document and complete in another.
- **Recommended fix:** update the stale strategic-decision row so it reflects the landed shared-target implementation and leaves only the remaining behavioral validation gaps as future work.
- **Tradeoff:** none.

## Broader Systemic Observations

- The repo is already unusually honest about the strategy-layer gap. [`docs/phases/phase-2-adaptation-plan.md`](../phases/phase-2-adaptation-plan.md#L63) and [`docs/knowledge-base/cake-build-architecture.md`](../knowledge-base/cake-build-architecture.md#L23) already document that `INativeAcquisitionStrategy` is still a design ghost, `IPayloadLayoutPolicy` is still deferred, and pure-dynamic is a dormant fallback.
- The consumer-smoke blind spot around always running with `-r <rid>` is already tracked as PD-10 in [`docs/phases/phase-2-adaptation-plan.md`](../phases/phase-2-adaptation-plan.md#L566). That concern remains real, but it is known and explicitly documented rather than newly discovered in this pass.
- I did not find behavior-level tests for the shared `Janset.SDL2.Native.Common.targets` logic itself in `build/_build.Tests/`. The extra non-proof-slice RID references I found in unit tests live under runtime-profile, strategy, and harvest-state coverage rather than packaging or consumer-target behavior. The current direct automated checks in this area are package-entry presence checks and smoke-level execution of the RID-specific path.

## Open Questions / Confidence Limiters

- Is the `.NETFramework` unset-architecture fallback intentionally x64-only, or is the intent still true AnyCPU friendliness?
- Should post-pack validation treat D-local proof-slice packaging and release-mode packaging as different completeness contracts, or is the intended long-term direction to make package completeness uniform and move proof-slice scoping elsewhere?

## What Was Not Verified

- No build, test, pack, or smoke commands were run as part of this review note.
- No win-x86 or win-arm64 consumer execution path was observed directly.
- No attempt was made to validate the package-consumer path outside the current repo documentation and code.

## Short Summary

The packaging spine is mostly coherent, but the consumer edge still has a real correctness problem and a weaker-than-advertised guardrail story. The most concrete defect is the `.NETFramework` fallback that silently picks `win-x64`; the most important validation gap is that G48 checks payload shape for present RIDs, not completeness against the intended runtime set.