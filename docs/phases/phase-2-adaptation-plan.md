# Phase 2 Adaptation Plan — Active Execution Ledger

**Status:** IN PROGRESS — core CI/CD + packaging surface landed; Phase 2b tail = nuget.org promotion, release-recovery playbook, and the four scope-assumption gaps surfaced in 2026-05-01 rehearsals.

## Purpose

This document tracks the remaining work for Phase 2 (CI/CD & Packaging). It does not re-derive policy or roadmap — those live in [`plan.md`](../plan.md) and (when ADRs land) under [`docs/decisions/`](../decisions/).

For canonical guardrail surface, see [`release-guardrails.md`](../knowledge-base/release-guardrails.md).

## Phase 2b Tail (open work)

### PD-7 — Public NuGet promotion

- **What landed:** `GitTagVersionProvider` supports targeted/family-tag and meta-tag/train modes; `release.yml` `publish-staging` is live for the GitHub Packages internal feed; trigger-aware `ResolveVersions` routing operational.
- **What's open:** `PublishPublicTask` real implementation, nuget.org Trusted Publishing OIDC wiring, first prerelease publication ([#63](https://github.com/janset2d/sdl2-cs-bindings/issues/63)).
- **Adjacency:** the four rehearsal gaps below are PD-7 adjacent — at least gaps #2 and #3 must close before the public-promotion path is operationally complete.

### PD-8 — Release recovery / manual escape hatch

- **Direction selected:** operator runs the same pipeline via `ExplicitVersionProvider` (`--explicit-version family=semver`); no separate release path.
- **What's open:** `playbook/release-recovery.md` doesn't exist yet; needs operator-executable step lists, API key provisioning policy, and audit-trail invariants. Cake `Pack-Family` / `Smoke-Family` / `Push-Family` helpers either implemented or explicitly deferred.

### PD-3 — `dotnet-affected` integration

- **Status:** scope-reduced to a 2a feasibility spike; full implementation deferred to 2b.
- **What's open:** ADR-style note committing to NuGet library vs CLI wrapper after the spike. Not blocking other Phase 2b work.

### Pipeline scope-assumption gaps (2026-05-01 rehearsal evidence)

The trigger-aware `ResolveVersions` routing was rehearsed end-to-end across four CI runs on master `0ffaa7a`. Three gaps remain open; the fourth (resolve-time scope filter) was fixed in `437edff`.

| # | Stage | Gap | Status |
| --- | --- | --- | --- |
| 1 | `Resolve Versions` `--scope` filter | Provider rejected full-tag scope values like `sdl2-image-2.8.0` | **Fixed** in `437edff` |
| 2 | `PreFlight` + `Pack` G58 cross-family resolvability | Scope-contains check only; satellite without core in same scope blocks at G58 even when core is published on the target feed | **Open** — feed-probe is the deferred relaxation surface |
| 3 | `PackageConsumerSmoke` runner | `EnsureSelectionSupportsCurrentSmokeScope` enforces "all manifest-concrete families OR none" against the explicit-version mapping; partial scope rejected before any restore | **Open** — partial-scope smoke not yet supported |
| 4 | `release.yml` tag-trigger fan-out | `on.push.tags` fires one workflow run per pushed tag. Train release requires N+1 atomic tags → N+1 separate workflow runs, of which only the `train-*` run is the desired release | **Open** — trigger mechanism under reconsideration |

**Operational consequence today.** The only release shapes that pass the full pipeline include every concrete family in the resolved scope: train tag, multi-family explicit dispatch with all 5 families, or manifest-derived dispatch. Targeted single-family or arbitrary-subset releases block at gap #2 (satellites) or gap #3 (any partial scope including core-only). The "core first, then satellites independently" policy is operationally enforced as "all 5 together per release wave" until both gaps close.

**Candidate directions (open, decision required, all PD-7 adjacent).**

- For #3, current leaning is **per-library / per-family smoke csprojs** instead of the single multi-family csproj. Each family carries its own smoke project; the runner invokes only the smoke projects whose families are in scope. Same direction may apply to NativeSmoke. Tradeoffs to evaluate: file count vs scope isolation; manifest-csproj drift catchnet shape; whether per-family smoke covers the cross-family interaction surface that the current 5-family csproj exercises.
- For #2, the deferred slice queries the **publish target feed** for the satellite's invocation (GH Packages staging for tag-push, nuget.org for the future PD-7 path). NuGet semver ordering matters: `2.32.0-ci.<run-id>` does NOT satisfy `>= 2.32.0` since prerelease versions come before their base release.
- For #4, two candidate replacements: (a) **manual `workflow_dispatch` as canonical**, with tags becoming audit-trail-only records created after a successful release; (b) **GitHub Releases as trigger source**, with the release body encoding the family-version mapping and `release: published` firing one workflow run regardless of how many tags the release object creates. Both preserve the existing governance policy while breaking the per-tag fan-out dependency.

## Open Pending Decisions

| # | Decision | Resolution moment |
| --- | --- | --- |
| **PD-3** | dotnet-affected integration: NuGet library vs CLI wrapper via `Cake.Process` | Phase 2b spike + ADR note |
| **PD-7** | Full-train release orchestration mechanism + public NuGet promotion path | First nuget.org prerelease |
| **PD-8** | Release recovery / manual escape hatch | `playbook/release-recovery.md` written |
| **PD-10** | `PackageConsumerSmoke` `-r <rid>` vs framework-dependent resolver coverage | When K checkpoint promoted on all 3 platforms |
| **PD-14** | Linux end-user MIDI packaging strategy under LGPL-free policy | Pre-first-public-Mixer-release |
| **PD-15** | SDL2_gfx Unix symbol-export regression guard | Next CI hardening pass or first vcpkg baseline bump touching sdl2-gfx |
| **PD-16** | Shared native dependency duplicate policy (dormant under hybrid-static; reactivates if pure-dynamic reintroduced) | Conditional on pure-dynamic reintroduction |

Closed PDs: PD-1, PD-2, PD-4, PD-5, PD-6, PD-9, PD-11, PD-12, PD-13.

## Cross-Reference

- **Roadmap + status:** [`plan.md`](../plan.md)
- **Pipeline + guardrails:** `.github/workflows/release.yml`, `build/_build/Targets/`, [`release-guardrails.md`](../knowledge-base/release-guardrails.md)
- **Local dev:** [`local-development.md`](../playbook/local-development.md)
