# Release Lifecycle Strategy Research - 2026-04-14 (gpt-codex)

- Date: 2026-04-14
- Author: GitHub Copilot (GPT-5.3-Codex)
- Status: Independent research synthesis
- Scope: Release lifecycle governance for an inter-dependent .NET native-library monorepo
- Repository: janset2d/sdl2-cs-bindings

## Why This Exists

This document captures my own independent research synthesis, separate from tracker audit work. The goal is to answer: "How should this monorepo run release lifecycle governance after strategy lock-in, with minimal ceremony and high reliability?"

## Method

1. Repository evidence review

- Canonical docs and phase docs
- Build/packaging architecture docs
- Existing research papers in docs/research
- Issue tracker body/comment evidence for strategy and release flow

1. Ecosystem pattern review (external)

- Azure SDK release and dependent-package policy
- dotnet Arcade dependency-flow model
- dotnet/runtime and dotnet/aspnetcore dependency graph structure (Version.Details.xml)
- Nx release/affected model and .NET plugin status
- NuGet Central Package Management guidance
- SDL ecosystem peer reference (ppy/SDL3-CS)

1. Synthesis rules

- Keep repo decisions that are already locked
- Avoid introducing governance that exceeds current team/automation maturity
- Prefer manifest/Cake authority over additional control planes

## Current Baseline (from repo evidence)

1. Packaging direction is already locked

- Hybrid Static + Dynamic Core is treated as settled.
- Pure Dynamic is treated as rejected.
- Triplet = strategy is treated as settled.

1. Release lifecycle implementation is not yet complete

- PackageTask and full release-candidate flow are still incomplete.
- Strategy primitives are present, but runtime wiring is still pending.
- CI matrix orchestration still has hardcoded/drift-prone surfaces.

1. Tracker posture

- Core strategy discussion appears in #75, #83, #85.
- Legacy release-workflow issues were superseded.
- Active execution issues now focus on PackageTask, staging/consolidation, prerelease publishing.

## Ecosystem Patterns That Matter Here

1. Two dominant release-governance styles in .NET monorepos

- Independent/targeted package releases with strict policy controls (Azure SDK style)
- Fixed train/shared cadence with centralized dependency channels (Arcade-style ecosystems)

1. Most successful ecosystems do not rely on one single mechanism

- They combine explicit policy, dependency metadata, and CI orchestration.
- They distinguish "what changed" from "what must be released together".

1. Nx value is real but bounded for this repo context

- Affected/caching can improve CI efficiency.
- Nx .NET plugin maturity is still not the strongest foundation for release governance authority.
- Governance should remain in repo-native sources (manifest + Cake + issue policy), with Nx optional as acceleration.

1. NuGet dependency hygiene matters as much as pipeline shape

- Central version policy reduces drift but can hide transitive pinning side effects.
- Inter-dependent native/managed packages need explicit dependency contract choices, not accidental defaults.

## Options Considered

### Option A - Full train-only releases

Definition:

- Any meaningful change triggers coordinated release of all families.

Pros:

- Simpler external story.
- Coherency is naturally strong.

Cons:

- Over-release churn for small changes.
- Slower cycle time and more publishing overhead.
- Higher operational cost for a small team.

### Option B - Pure targeted independent releases

Definition:

- Release only changed family/package set, always.

Pros:

- Fast feedback and low publish blast radius.
- Efficient for frequent small changes.

Cons:

- Requires strict dependency and compatibility governance.
- Can drift if cross-family changes are under-detected.

### Option C - Hybrid governance (recommended)

Definition:

- Targeted release by default.
- Mandatory full-train release for milestone/cadence or high-impact changes.

Pros:

- Keeps speed without losing ecosystem coherency.
- Aligns with current repo maturity and active work items.
- Avoids over-automation early while keeping a clear path to scale.

Cons:

- Requires clear promotion rules.
- Needs explicit trigger matrix for when full-train is forced.

## Recommended Operating Model

1. Authority model

- Manifest + Cake remain source of truth for build/release policy.
- CI workflows are execution surfaces, not policy sources.
- Optional orchestration tools (including Nx) must consume this policy, not replace it.

1. Release unit model

- Family-locked release unit: managed + corresponding native package move together.
- Inter-family dependencies remain explicit and policy-driven.

1. Default path

- Targeted release for isolated family-level changes.

1. Forced full-train triggers

- Core packaging policy changes.
- Runtime/triplet matrix changes.
- Shared dependency baseline or toolchain changes.
- Build-host strategy wiring/coherence behavior changes.
- Any change affecting validation guardrails.

1. Cadence guardrail

- Even with targeted default, run scheduled full-train validation/release checkpoints.
- Suggested baseline: milestone-driven, with optional monthly/quarterly hardening train.

1. Dependency contract baseline

- Managed package depends on its own native package with strong coupling semantics.
- Satellite-to-core dependency policy should be explicit and documented per family.

1. Pipeline shape

- Preflight: policy coherence and manifest integrity checks.
- Build/harvest: matrix generated from manifest data, not hardcoded YAML.
- Consolidation: distributed staging then deterministic merge.
- Package + consumer smoke tests: release gate, not optional post-step.
- Publish: staged promotion path with clear rollback/audit trail.

## What This Means For Near-Term Repo Work

1. Do not reopen base packaging strategy by default.

- Focus on release-lifecycle mechanics and governance finalization.

1. Prioritize lifecycle policy concretization in docs and issues.

- Resolve open lifecycle questions into explicit rules.
- Align acceptance criteria in active issues with chosen release model.

1. Keep implementation sequencing practical.

- Finish strategy runtime wiring and validator integration.
- Complete PackageTask and staging/consolidation path.
- Add reliable package-consumer smoke gates before broad publish automation.

## Open Decisions To Confirm Explicitly

1. Versioning operation

- Tag-prefix flow vs manual project-version flow for family releases.

1. Dependency coupling strictness

- Exact vs minimum semantics for key package links.

1. Promotion cadence

- Milestone-only full-train vs milestone + periodic train.

1. Release metadata policy

- Minimum required metadata for prerelease and stable promotion.

## Risks If Left Ambiguous

1. Governance drift

- Different workflows encode different release rules.

1. Accidental coupling regressions

- Targeted release publishes without full impact visibility.

1. Matrix drift

- Manifest and CI diverge, causing inconsistent platform coverage.

1. Tooling overreach too early

- Extra orchestration layer added before base policy is stable.

## Final Recommendation

Adopt and formalize a hybrid release governance model:

- Targeted releases by default
- Forced full-train on high-impact changes
- Scheduled full-train checkpoints for coherence
- Manifest/Cake as policy authority
- CI and optional orchestration as execution accelerators

This gives the best balance of delivery speed, correctness, and maintainability for the current repo maturity.

## Verdict

Base packaging strategy should remain closed; lifecycle governance should move to a targeted-default plus forced-train model with manifest/Cake authority and explicit promotion gates. - gpt-codex
