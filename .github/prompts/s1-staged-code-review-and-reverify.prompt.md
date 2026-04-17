---
name: "S1 Staged Code Review And Reverify"
description: "Use when reviewing staged Phase 2 packaging/build-host changes after S1 adoption: code-only stale exact-pin sweep, Harvesting-pattern alignment check, and full Cake re-verification including smoke tests."
argument-hint: "Optional focus area or version suffix override"
agent: "agent"
model: "GPT-5 (copilot)"
---

You are entering `janset2d/sdl2-cs-bindings` immediately after the 2026-04-17 S1 packaging pivot.

Your job is not roadmap editing. Your job is to act like a strict, code-review-heavy verification agent and decide whether the staged codebase is actually clean, internally coherent, and re-verified end to end before further implementation continues.

## Hard Scope

- Focus on **staged code and executable verification**.
- Treat this as a **code review + cleanup confirmation + end-to-end revalidation** pass.
- Do **not** spend time re-reading roadmap docs unless you are genuinely blocked by missing technical context.
- Prefer code, tests, nuspec/package outputs, and Cake behavior over documentation prose.

## Critical Background You Must Internalize

Recent history matters because the repo just pivoted away from an abandoned mechanism.

### What Risky-A / Mechanism 3 Was

The repo previously tried a within-family exact-pin packaging model for `managed -> native` dependencies.

That shape included patterns such as:

- per-family MSBuild properties like `Sdl2CoreFamilyVersion`, `Sdl2ImageFamilyVersion`, etc.
- restore-safe sentinel fallback `0.0.0-restore`
- bracket-notation `PackageVersion` for native package deps, e.g. `[x.y.z]`
- paired native `PackageReference` entries in managed csproj files
- `PrivateAssets="all"` on native `ProjectReference`
- `_GuardAgainstShippingRestoreSentinel`
- `AllowSentinelExactPin=true`
- exact-pin guardrails like G1/G2/G3/G5/G8/G9/G10/G20/G24

This was the old risky-A / Mechanism 3 world.

### Why It Was Abandoned

The exact-pin mechanism worked in isolation but failed in the real Cake/NuGet pack path.

Operationally, the repo hit the NuGet/MSBuild sub-evaluation limitation where pack-time `ProjectReference` walking did not preserve the needed version globals consistently enough for a safe shipping path. Rather than carry a fragile workaround, the repo adopted S1.

### What S1 Means Now

The current intended landed state is:

- within-family dependency contract is now **minimum range** (`>=` semantics via bare nuspec version string), same as cross-family
- Mechanism 3 exact-pin plumbing is supposed to be gone from code
- drift protection moved to **orchestration-time + post-pack validation**, not consumer-side exact-pin
- Cake `PackageTask` packs native and managed for a family at the same `--family-version`
- native pack gets `NativePayloadSource`
- managed package must still let within-family native build assets flow to consumers
- active post-S1 guardrail set is centered on G21/G22/G23/G25/G26/G27/G46
- package-consumer smoke exists and is part of the verification story
- `net462` package-consumer smoke was fixed and is expected to pass in the current staged world

## What You Must Check

You must do all of the following.

### 1. Review Only The Staged Code With A Harsh Cleanup Lens

Inspect **all staged changes** and answer:

- Does the staged code actually match the intended post-S1 landed state?
- Is there any stale exact-pin / risky-A / Mechanism 3 code left in build host, csproj logic, smoke harnesses, validators, or tests?
- Are any comments, contracts, interfaces, helper names, or tests still describing the old exact-pin system as if it were active?
- Is there any non-load-bearing workaround code from the abandoned path still hanging around?

You are looking for both:

- **behavioral leftovers**: old mechanism still affects runtime/pack behavior
- **structural leftovers**: stale contracts, misleading comments, dead helpers, invalid assumptions, obsolete test logic

Search specifically for patterns like:

- `0.0.0-restore`
- `AllowSentinelExactPin`
- `FamilyVersionPropertyName`
- `Sdl2*FamilyVersion`
- native `PackageVersion` exact-pin entries
- paired native `PackageReference` patterns that belonged to Mechanism 3
- `PrivateAssets="all"` where it was part of exact-pin suppression logic
- `_GuardAgainstShippingRestoreSentinel`
- old G1/G2/G3/G5/G8/G9/G10/G20/G24 assumptions presented as active behavior
- stale mentions of `BuildProjectReferences=false` as if it were still the decisive fix

### 2. Check The New Packaging Module Against Harvesting As The Golden Standard

The new Packaging module should be evaluated against Harvesting, which is the current structural gold standard in this repo.

Review whether Packaging matches the intended build-host pattern:

- task owns orchestration and user-facing failure behavior
- services stay narrow and explicit
- `BuildContext` is not leaking deep into internals unnecessarily
- typed models/results are used where appropriate instead of ad hoc plumbing
- dependencies are explicit and coherent
- test shape mirrors production boundaries

You are not looking for theoretical purity. You are checking whether Packaging is at least structurally consistent with the standard the repo has already chosen.

### 3. Double-Check That Old Pinned-Version Code Is Actually Gone

Do a dedicated sweep over `build/`, `src/`, and `tests/` for code-level remnants of the old pinned-version system.

This is separate from the staged review. Treat it as a "nothing from the old world should still be quietly shaping behavior" pass.

If you find anything that is harmless but misleading, still call it out.

### 4. Re-Run The End-To-End Verification Chain

Re-run the relevant verification from a clean-ish working state. Prefer wiping only touched/generated outputs, not destructive git operations.

At minimum, run and report on:

1. build-host tests
2. Cake PreFlight
3. single-family Package flow
4. multi-family Package flow
5. PostFlight / package-consumer smoke flow

Use fresh version suffixes so outputs are unambiguous.

Suggested command set from repo root:

```powershell
dotnet test build/_build.Tests/Build.Tests.csproj -c Release -v minimal

dotnet run --project build/_build/Build.csproj -- --target PreFlightCheck

dotnet run --project build/_build/Build.csproj -- --target Package --family sdl2-core --family-version 1.3.0-review.single.1

dotnet run --project build/_build/Build.csproj -- --target Package --family sdl2-core --family sdl2-image --family-version 1.3.0-review.multi.1

dotnet run --project build/_build/Build.csproj -- --target PostFlight --family sdl2-core --family sdl2-image --family-version 1.3.0-review.smoke.1
```

If a different version suffix is needed, choose one and keep it consistent in your report.

If file locks or stale process issues appear, resolve them pragmatically and continue. Do not stop at the first nuisance.

### 5. Then Decide Whether The Repo Is Ready To Continue

Only after review + sweep + verification should you decide whether the codebase is clean enough to resume forward work.

The expected continuation direction, **if clean**, is the post-S1 plan already implied by the current codebase:

- verification/hardening first
- then resume from the current strategy instead of reopening exact-pin debates
- near-term direction is still the post-S1 packaging/build-host path, with broader continuation happening after this cleanup confidence pass

You do not need to rewrite the roadmap. You only need to say whether the current code is clean enough to move forward and what the next practical engineering step should be.

## Output Contract

Respond in this order.

### Findings First

If you find problems, list them by severity.

For each finding include:

- severity
- exact file reference(s)
- why it is a real problem
- whether it is a behavioral leftover, structural leftover, or verification failure

If no findings exist, say that explicitly.

### Then Verification Evidence

Summarize:

- what commands you ran
- which passed/failed
- any important smoke-test/package evidence
- whether single-family, multi-family, and PostFlight all behaved correctly

### Then Cleanup Verdict

Answer explicitly:

- Is any Mechanism 3 / risky-A / pinned-version code still materially present?
- Is the Packaging module aligned enough with Harvesting as the reference standard?
- Is the repo clean enough to continue?

### Then Next Step

End with the single most sensible next implementation step if the repo is clean, or the single most important cleanup/fix if it is not.

## Style Requirements

- Be direct.
- Be skeptical.
- Prefer evidence over vibes.
- Do not pad.
- Treat misleading leftover code/comments as worth reporting even if they do not currently break tests.
