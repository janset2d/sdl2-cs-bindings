# CI/CD Packaging and Release Plan

> **2026-04-25 rewrite.** This document is a live-state pointer + carry-forward backlog index. The pre-Slice-E-follow-up-pass design (484 lines describing the retired `Release-Candidate-Pipeline.yml` mega-workflow + the retired `prepare-native-assets-*.yml` family) is preserved at [`docs/_archive/ci-cd-packaging-and-release-plan-2026-04-25.md`](../_archive/ci-cd-packaging-and-release-plan-2026-04-25.md). Substantive content moved to its canonical home; this file now points readers there + tracks unique forward-looking ideas with no other home.

## Where the canonical content lives now

| Topic | Canonical home |
| --- | --- |
| Pipeline stage ownership, version-source providers, matrix re-entry, stage-owned validation | [ADR-003 Release Lifecycle Orchestration](../decisions/2026-04-20-release-lifecycle-orchestration.md) |
| Release governance, package families, tag-derived versioning, family lifecycle | [release-lifecycle-direction.md](release-lifecycle-direction.md) |
| Live `release.yml` job graph + per-job contracts + composite-action usage | [`.github/workflows/release.yml`](../../.github/workflows/release.yml) (header comments carry slice trail Slice A → Slice E follow-up pass + inline P4e hygiene table) |
| Builder image + GHCR multi-arch flow + retention | [`.github/workflows/build-linux-container.yml`](../../.github/workflows/build-linux-container.yml) + [`docker/linux-builder.Dockerfile`](../../docker/linux-builder.Dockerfile) |
| Composite actions: `vcpkg-setup` (cache identity + bootstrap + install), `nuget-cache` (cross-OS workspace cache), `platform-build-prereqs` (macOS brew; Linux/Windows no-ops) | [`.github/actions/`](../../.github/actions/) |
| Cross-platform validation contract (A-K checkpoints + clean-slate rule + per-stage commands) | [cross-platform-smoke-validation.md](../playbook/cross-platform-smoke-validation.md) |
| Cake task contracts + per-stage runners + DDD layering | [cake-build-architecture.md](cake-build-architecture.md) |
| Guardrail catalog (G21–G58) + stage-owned validation map | [release-guardrails.md](release-guardrails.md) |

## Live state snapshot (as of 2026-04-25, master `d190b5b`)

`release.yml` runs the following 10-job topology against the post-Slice-E-follow-up-pass shape. Detailed job bodies live in the workflow file; this table is a navigation index, not a duplicate spec.

| Job | Stage | Notes |
| --- | --- | --- |
| `build-cake-host` | Build + Test Cake Host | Single-runner FDD publish (`-p:UseAppHost=false`); coverage ratchet via `Coverage-Check` against `build/coverage-baseline.json`; uploads `cake-host` artifact every consumer downloads |
| `resolve-versions` | ADR-003 §3.1 entrypoint | `ManifestVersionProvider` (`workflow_dispatch` `manifest-derived`), `GitTagVersionProvider` (tag pushes), `ExplicitVersionProvider` (`workflow_dispatch` `explicit`); emits `versions.json` artifact |
| `preflight` | Pre-matrix fail-fast | Version-aware by ADR-003 §2.3 contract; G54 + G58 (defense-in-depth mirror) + structural validators; consumes `versions.json` via `--versions-file` |
| `generate-matrix` | Dynamic matrix | Reads `manifest.runtimes[]`; emits same matrix JSON for `harvest` + `consumer-smoke` (symmetric per ADR-003 §3.4) |
| `harvest` (matrix, 7 RIDs) | Per-RID harvest + native-smoke inline | Uses `vcpkg-setup` + `platform-build-prereqs` composites; Linux RIDs run on `ghcr.io/janset2d/sdl2-bindings-linux-builder:focal-latest` |
| `consolidate-harvest` | Aggregation | Single runner; merges per-RID status into `harvest-manifest.json` + `harvest-summary.json` |
| `pack` | Per-family pack | Consumes `versions.json` via `--versions-file`; G21–G58 post-pack guardrails run inside `PackageOutputValidator` |
| `consumer-smoke` (matrix, 7 RIDs) | Matrix re-entry | Per-RID restore + per-TFM TUnit (`net9.0`/`net8.0`/`net462`); `IDotNetRuntimeEnvironment` bootstraps win-x86 runtime via Cake (no inline YAML PowerShell); `IPackageConsumerSmokeRunner` enforces `--explicit-version` mandatory |
| `publish-staging` | Phase-2b stub | Gated `if: false`; `PublishStagingTask` Cake target throws `NotImplementedException` until Phase 2b lands real feed transfer |
| `publish-public` | Phase-2b stub | Gated `if: false`; `PublishPublicTask` Cake target same shape |

**Triggers**: tag push (`sdl2-*-*.*.*` targeted family release; `sdl3-*-*.*.*` future SDL3 line; `train-*` full-train) + `workflow_dispatch` (mode=`manifest-derived`/`explicit` + optional `explicit-versions` inline list).

**Builder image**: `build-linux-container.yml` builds amd64 + arm64 native runners → `docker buildx imagetools create` merges into a single multi-arch manifest list → publishes 2 tags (`focal-<yyyymmdd>-<sha>` immutable + `focal-latest` mutable pointer). Triggers: `workflow_dispatch` (with `push: false` dry-run input) + monthly cron 1st 03:00 UTC. Retention: `delete-only-untagged-versions: true` + 5 most recent.

## Carry-forward backlog (Phase 2b/3 candidates)

These ideas come from the archived design and have no other canonical home. They are interesting for Phase 2b/3 but **not active** today; cross-referenced from [`phase-2-adaptation-plan.md`](../phases/phase-2-adaptation-plan.md) Pending Decisions so the residue stays visible.

- **`build/known-issues.json` opt-out mechanism**: per-`{library, vcpkg_version, RID}` skip directive for known-failing combinations (upstream port bugs, environment regressions). Allows the matrix to keep moving without disabling whole RIDs. Cake `PreFlightCheck` reads + warns; `Harvest` matrix filter respects it.
- **`force_build_strategy` 3-tier enum** (`auto-detect` / `force-buildable` / `force-everything`): operator-controlled rebuild scope override exposed via `workflow_dispatch` input. `auto-detect` queries the target feed and skips already-published; `force-buildable` rebuilds everything except `known-issues.json` entries; `force-everything` ignores both filters (diagnostic / fix-verification mode).
- **`Promote-To-Public.yml` separate workflow**: explicit promotion step from staging feed (GitHub Packages) to nuget.org. Manual `workflow_dispatch` with package-id + version inputs; downloads validated nupkg from staging, re-pushes to public feed, optionally creates GitHub Release with assets. Target shape lands when `publish-staging` runs real packages in Phase 2b.
- **`PR-Version-Consistency-Check.yml`**: lightweight PR-time warning workflow that parses `manifest.json` (`vcpkg_version`) vs `vcpkg.json` (`overrides[]`) and emits step-summary `::warning::` annotations on drift. Always succeeds (doesn't block PR), surfaces inconsistencies before operator triggers a release pipeline. Cheap insurance against silent version drift slipping past code review.

## When to update

- Job graph changes in `release.yml` → update the live-state snapshot table.
- Trigger surface evolves (new tag pattern, new dispatch input) → update the snapshot + cross-reference ADR-003 §3.4.
- Backlog item graduates to active → move from "Carry-forward backlog" into the appropriate doc (`phase-2-adaptation-plan.md` Pending Decisions for staged work, dedicated playbook for runtime concerns) and update the snapshot table.
- Composite action surface evolves → update the canonical-content-locations table.

For the design intent + retired mega-workflow rationale, follow the archive link at the top.
