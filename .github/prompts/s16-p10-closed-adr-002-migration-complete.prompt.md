S16 P10 closed — ADR-002 target-centric migration complete

---
name: "S16 P10 closure handover — ADR-002 migration complete; build-host now fully target-centric"
description: "Priming prompt for the next agent picking up after S16 P10 ADR-002 closure. Single atomic slice retired all pre-ADR scaffolding (ArchitectureTests, HarvestJsonContract, IVcpkgManifestReader, Features/, Integrations/, Shared/, Host/Configuration/, Characterization/), promoted Build.Shared.* to root namespaces, redistributed Build.Integrations.* (scanners → new Build.DependencyAnalysis/ root concept; NuGet client → PublishStaging/Services/; .NET runtime env → ConsumerSmoke/Services/; project metadata reader → root Build.Packaging/), migrated Features/{Ci,Vcpkg} to Targets/{GenerateMatrix,EnsureVcpkgDependencies}/ with Pipelines retired (ADR §5) and bodies inlined into task RunAsync, retired RepositoryConfiguration (PathService ctor takes DirectoryPath repoRoot directly), relocated Characterization/ManifestDeserializationTests to Unit/Manifest/RealManifestContractTests, plus two amendments (SatelliteUpperBoundValidator → IFoo pattern, HybridStaticLeakValidator → Cake-native FilePath.GetFilename). Slice committed atomically; tests 596 → 587 (-9 net), build 0W/0E, slopwatch 0, ci-sim 8/8 PASS in ~187s on Windows. ADR-002 done."
argument-hint: "Verify HEAD on master is the P10 closure commit (commit message starts 'refactor: close ADR-002 with P10 unified cleanup (S16)'); working tree clean. Strategic next-step candidates: PD-7 (PublishPublic real impl + nuget.org Trusted Publishing) is the highest-value Phase 2b tail item; V1 test infrastructure deprecation (TestHostFixture + FakeRepoBuilder retire) is a clean standalone slice candidate before PD-7 to keep new test surface V2-only."
agent: "agent"
model: "Claude Opus 4.7 (1M context)"
---

You are an engineer entering the janset2d/sdl2-cs-bindings repository **at S16 P10 slice closure**. ADR-002 target-centric build-host migration is complete; this is the architectural watershed of the refactor stream.

## First Principle

> Treat every claim here as **current-as-of-handover (`2026-05-10`)** and verify against the live repo, git log, branch state, gates (build + tests + slopwatch + ci-sim), and canonical docs before acting.

## Status verification (do this first)

```pwsh
git status
git log -3 --oneline
git branch --show-current
```

Expected:
- Branch: `master` (the P10 slice was atomic-committed and merged)
- HEAD: commit message starts `refactor: close ADR-002 with P10 unified cleanup (S16)`
- Working tree clean (the slice's design + plan + S15 priming-prompt scratch all deleted in the same commit per `docs/refactoring/README.md` lifecycle)

```pwsh
dotnet build build/_build/Build.csproj -c Release
dotnet test --project build/_build.Tests/Build.Tests.csproj -c Release --framework net10.0
slopwatch analyze --fail-on warning --exclude "artifacts/**,external/**,vcpkg_installed/**,**/bin/**,**/obj/**"
```

Expected:
- Build: 0 W / 0 E
- Tests: **587 PASS** (596 baseline at S15 P9 close → 587 net delta of -9 across the P10 slice)
- Slopwatch: **0 issues** (baseline rebuilt at slice close)

```pwsh
dotnet run --file tools.cs -- ci-sim
```

Expected: **8/8 PASS in ~165–190s** on Windows. Last verified run took 186.7s; S15 P9 baseline was 168.9s. Variance within ±20s is acceptable; investigate anything beyond.

If any expectation fails — STOP and diagnose; don't pick a new slice on a broken baseline.

## What S16 / P10 closed

Single atomic slice retired all pre-ADR scaffolding. **ADR-002 migration is complete.**

Surface delta (172 production + test files; 562+ insertions / 1089- deletions; net subtractive):

- **Retired layers:** `ArchitectureTests.cs`, `HarvestJsonContract.cs`, `IVcpkgManifestReader` + `VcpkgManifestReader`, entire `Features/`, `Integrations/`, `Shared/`, `Host/Configuration/`, `Characterization/` folders.
- **Namespace promotions:** `Build.Shared.{Manifest,Runtime,Results,Packaging,Harvesting}` → root concepts; ~221 production + test `using` updates handled via Rider mass-rename.
- **Integration redistributions:**
  - `IRuntimeScanner` + 3 OS scanners → new root `Build.DependencyAnalysis/` (host-platform abstraction, not a Harvest-domain detail — Deniz mid-slice correction vs the original spec which had them target-local).
  - `INuGetFeedClient` → `Targets/PublishStaging/Services/`.
  - `IDotNetRuntimeEnvironment` → `Targets/PackageConsumerSmoke/Services/`.
  - `IProjectMetadataReader` → root `Build.Packaging/` (cross-target shared by Package + ConsumerSmoke).
  - `VcpkgBootstrapTool` registration → `Tools/AddToolWrappers()`.
- **Features dissolution:**
  - `Features/Ci/` → `Targets/GenerateMatrix/` (`MatrixOutput` → `Models/`; pipeline retired).
  - `Features/Vcpkg/` → `Targets/EnsureVcpkgDependencies/` (pipeline retired).
- **Vcpkg absorb:** `VcpkgManifestRepository.Load()` inlines parse via `_context.FileSystem.GetFile().OpenRead()` + `CakeJsonExtensions.DeserializeJson<VcpkgManifest>`; `JsonException` → `CakeException` for Cake-native fail surface (behavior change vs prior `ArgumentException`).
- **JSON consolidation:** `HarvestJsonContract` redundant naming policy was already shadowed by per-property `[JsonPropertyName]` attributes; deleted entirely. All callers route through `CakeJsonExtensions.DefaultJsonOptions` (or drop the explicit options arg since `WriteJsonAsync`'s default is the same).
- **`Host/Configuration/RepositoryConfiguration` retired:** `PathService` ctor takes `DirectoryPath repoRoot` directly; `AddHostBuildingBlocks(parsedArgs, repoRoot)`.
- **Two architectural amendments (surfaced during execution):**
  1. `SatelliteUpperBoundValidator` static class promoted to IFoo pattern (S12 amendment compliance — every validator under root `Validation/` ships with `IFoo` interface + `AddSingleton<IFoo, Foo>()` registration).
  2. `HybridStaticLeakValidator` `System.IO.Path` / `IoPath` alias replaced with Cake-native `new FilePath(node.Path).GetFilename().FullPath` (ADR-002 §9 Cake-nativeness).
- **Empty ServiceCollection rule (Deniz instruction):** `AddCiFeature()`, `AddVcpkgFeature()`, `AddPublishPublic()` `ServiceCollectionExtensions.cs` files deleted entirely (only `return services;` bodies). Cake auto-discovers their tasks via `[TaskName]`.
- **Test relocations:**
  - `Tests/Unit/Shared/` retired; 3 files relocated to `Unit/{Manifest,Runtime}/`.
  - `Characterization/ManifestDeserializationTests` → `Unit/Manifest/RealManifestContractTests`.
  - `GenerateMatrixPipelineTests` → `Scenarios/GenerateMatrix/GenerateMatrixTaskScenarioTests` with V2 fixture (`FakeCakeWorldV2` + `TargetTestHostV2<GenerateMatrixTask>`).
  - `VcpkgManifestRepositoryRoundTripTests` rewritten as a true round-trip via `FakeCakeWorldV2.WithTextFile` + real JSON deserialize (no NSubstitute reader); two new vakaları cover invalid-JSON and null-deserialize `CakeException` paths.
- **Multi-agent review absorption:** all 6 BLOCKs from the 5-reviewer parallel pass absorbed in-slice (PublishPublic empty SC retire, Tests/Unit/Shared/ retire, GenerateMatrixTaskTests V2 migration, plan.md ACTIVE→CLOSED header, refactor-plan §11 P10 closure block, 4 stale doc files); 3 SUGGESTs absorbed (vcpkg fixture extract, review-checklist phrasing flip, refactor-plan stale Shared/Harvesting reference); 4 NIT clusters absorbed (4 stale doc-comments in production code, ServiceCollectionExtensionsSmokeTests stale rationale, Program.cs:91 stale AddIntegrations comment); 1 SUGGEST deferred (S1 — DependencyAnalysis placement deviation vs spec, already documented in plan.md closure note + AGENTS.md retired-layers table).

**Test count delta (596 → 587, -9 net):**
- −5 ArchitectureTests methods
- −2 retired AddCiFeature/AddVcpkgFeature smoke tests
- −1 retired AddPublishPublic smoke test
- −2 VcpkgManifestReader tests
- −1 ctor null-check (reader param removed)
- +2 new VcpkgRepository CakeException vakaları

## Inline corrections from S16 execution (worth carrying forward)

These are the lessons the slice surfaced; honor them when authoring future slices:

1. **`IRuntimeScanner` is host-platform abstraction, not target-domain detail.** Single-consumer ≠ target-local automatically; if the type abstracts a host-platform invariant (per-RID dispatch closure), it earns a root concept home. The spec had it target-local; Deniz corrected mid-slice to a new `Build.DependencyAnalysis/` root concept. Rule for future placement decisions: check whether the contract abstracts host-platform vs target-domain logic; per-platform dispatch always = host-platform abstraction.
2. **Empty ServiceCollectionExtensions.cs files retire entirely.** If `AddXTarget()` ends up with only `return services;`, delete the file + drop the call from `Program.cs`. Cake auto-discovers tasks via `[TaskName]`. Default rule going forward.
3. **HarvestJsonContract redundancy was shadowed by attributes.** When a serializer's `PropertyNamingPolicy` is shadowed by per-property `[JsonPropertyName]` attributes on every model member, the policy is dead code regardless of how authoritative the surrounding type looks. Audit before adding new options instances.
4. **Test-side namespaces must mirror production-side renames.** Renaming production `Build.Shared.Foo` → `Build.Foo` requires also renaming `Tests.Unit.Shared.Foo` → `Tests.Unit.Foo`. The reviewer caught 3 test files left in `Tests/Unit/Shared/` after the production-side promotion.
5. **`git mv` discipline + Rider mass-rename pairing.** For multi-file namespace promotions: I (the agent) `git mv` the files + update top-level namespace declarations; Deniz Rider-rewrites the `using` sites across the rest of the codebase in a single Find/Replace pass. ~123 files of `using` updates land in seconds. Don't try to script the cross-file edits manually.
6. **Multi-agent review tier discipline:** convergent BLOCKs (multiple reviewers agree) are highest priority; cluster-level SUGGESTs decided by operator; single-reviewer NITs default-defer. Present full review surface before absorption.
7. **`commit do not push` standing rule** holds across slices.

## Strategic position (where we are)

ADR-002 migration is the architectural watershed. The build host now matches the canonical target-centric layout end-to-end. The remaining work splits into three streams:

### Stream 1 — Phase 2b public-release tail

| Item | Priority | Notes |
|---|---|---|
| **PD-7 — PublishPublicTask real impl + nuget.org Trusted Publishing OIDC** | High | Highest-value next-step; PublishPublicTask currently a stub. Will likely require `IGitHubAuthTokenResolver` extraction (deferred from S15 P9). |
| PD-8 — Release recovery playbook + Pack/Smoke/Push-Family helpers | Medium | Operator runbook + family-scoped Cake helpers. |
| PD-3 — `dotnet-affected` integration ADR | Low | Decision-only; spike already done. |
| Pipeline scope-assumption gaps (Gap #2 G58 feed-probe, Gap #3 ConsumerSmoke partial-scope, Gap #4 trigger rework) | Medium | Each its own small-medium slice. |

### Stream 2 — Test infrastructure consolidation

V1 test surface still alive: `TestHostFixture` (V1) consumed by 10 smoke tests in `ServiceCollectionExtensionsSmokeTests.cs`; `FakeRepoBuilder` still in a few `Unit/` tests. V2 (`FakeCakeWorldV2` + `TargetTestHostV2`) is the standard for everything else.

**Standalone V1-deprecation slice (S17 P11 candidate):**
- Add `FakeCakeWorldV2.AsServiceCollection()` (or analogous helper) for DI-graph smoke tests.
- Migrate `ServiceCollectionExtensionsSmokeTests.cs` (10 tests) to V2.
- Audit remaining `FakeRepoBuilder` consumers; migrate or document why kept.
- Delete `TestHostFixture.cs` + `FakeRepoBuilder.cs` + V1-specific helpers.
- Slopwatch re-baseline + ci-sim verify.

**Recommendation (Deniz preference at S16 close):** standalone slice **before** PD-7 — keeps PD-7's new test surface V2-only without V1 distraction. ~1 day focused work, ~15-20 file touches, low risk.

### Stream 3 — Native pipeline scaling

| Item | Priority | Notes |
|---|---|---|
| SDL2_net bindings + native project + manifest re-entry | High | [#58](https://github.com/janset2d/sdl2-cs-bindings/issues/58) |
| SDL2_mixer LGPL-free codec parity audit | Medium | Cross-RID codec coverage |
| Linux symbol-visibility (`.map` files) | Low | Per-satellite |
| PD-14 — Linux MIDI packaging strategy | Decision | Pre-first-public-Mixer-release |
| PD-15 — SDL2_gfx Unix symbol-export regression guard | Small | One-off |

### Stream 4 — Phase 4 binding generator (CppAst)

Not started. Future strategic initiative; replaces SDL2-CS imports.

### Parking-lot tail (low-effort polish)

- Cancellation token plumbing across `PublishStagingTask` + `PackageConsumerSmokeTask` (S15 P9 follow-up).
- `DotNetSmokeRunner.RunCompileSanity` + `RunSmokeForTfm` collapse to single `Run(...)`.
- `IGitHubAuthTokenResolver` extraction (will be naturally addressed by PD-7).
- `PackageConsumerSmokeTask` god-service smell (10 ctor deps + 100 LOC RunAsync) — re-extraction candidates deferred from S15.
- `VcpkgCliProviderTests` V2 migration (V2 lacks tool-stdout injection equivalent).
- 6 stale doc-comments in production cleaned in S16; if more surface, same pattern.

## References

Canonical docs in reading order for the next session:

- `docs/onboarding.md` (project overview, repo layout glossary)
- `AGENTS.md` (operating rules + post-P10 build-host reference pattern + retired-layers table)
- `docs/plan.md` (current status; S16 P10 closure note in active-phase paragraph; roadmap)
- `docs/decisions/2026-05-05-target-centric-build-host.md` (ADR-002 — migration is complete)
- `docs/refactoring/target-centric-build-host-refactor-plan.md` §11 (P10 closure block at top of section)
- `docs/refactoring/target-centric-build-host-review-checklist.md` (post-P10 do-not-reintroduce phrasing)
- `docs/refactoring/testing-guidelines.md` (V2 fixtures default; V1 deprecation candidate)
- `docs/parking-lot.md` (cleaned of P10 closures; remaining tail relevant for parking-lot review)

## Sequencing notes (carry-forward)

1. **`commit do not push` standing rule** until Deniz explicitly authorizes push.
2. **Single atomic commit per slice** per S12-S16 precedent.
3. **Scratch deletion at slice close** — design + plan + priming-prompt scratch never enter git history per `docs/refactoring/README.md` lifecycle.
4. **Multi-agent review at slice closure** dispatches 5 reviewers in parallel via `Agent` tool; tier discipline strict (BLOCK = must fix, SUGGEST = should fix or defer with rationale, NIT = polish or parking-lot defer). Present full review surface to operator BEFORE applying absorption.
5. **`git mv` discipline** + Rider mass-rename pairing for namespace work.
6. **Touch a test → use V2 fixtures** (`feedback_test_infra_v2_default.md`) — broader than the now-closed ADR-002 migration rule.

## Communication style (Deniz preferences)

- Talkative + practical, sometimes clever humor.
- Innovative but prioritize what works.
- Challenge decisions when needed; explain reasoning. **Avoid yes-person behavior.**
- Bilingual: Turkish + English interchangeably.
- **Skip micro-checkpoints during execution** — batch routine work, stop only at significant boundaries.
- After a slice commits, default to "what's next?" not "let me wrap up".
- **NEVER write priming prompts unilaterally** — this handover IS user-requested ("hazırla next session için"), so it's not unilateral.
- **`git mv` discipline** is mandatory per `feedback_git_mv_during_migrations.md`.

## Memory check (auto-memory at `C:\Users\deniz\.claude\projects\E--repos-my-projects-janset2d-sdl2-cs-bindings\memory\`)

Notable for the post-S16 era:

- `feedback_skip_micro_checkpoints.md` — batch routine work; stop at significant boundaries.
- `feedback_test_infra_v2_default.md` — touch a test → use V2 (the V1 deprecation candidate slice would be the natural close of this rule).
- `feedback_git_mv_during_migrations.md` — `git mv` FIRST before content edits; preserves history.
- `feedback_wsl_macos_zsh.md` — drive WSL via `wsl -- zsh -lc`; macOS via `ssh ... 'zsh -lc'`. Bash doesn't load DOTNET_ROOT.
- `feedback_session_pacing.md` — after a slice commits, default to "what's next?" not "let me wrap up". Never write priming prompts unilaterally.
- `feedback_rider_for_mass_renames.md` — propose rename, let Deniz Rider-rename instead of scripting cross-file edits.
- `project_refactoring_doc_lifecycle.md` — `docs/refactoring/` mixes durable plan/ADR/checklist with temp per-slice design specs that delete after the slice ships.

## Useful commands

```pwsh
# Build host
dotnet build build/_build/Build.csproj

# Tests (TUnit on MTP) — note: NO --filter support; runs all
dotnet test --project build/_build.Tests/Build.Tests.csproj -c Release --framework net10.0

# Slopwatch (baseline rebuilt at S16 close; analyze should show 0)
slopwatch analyze --fail-on warning --exclude "artifacts/**,external/**,vcpkg_installed/**,**/bin/**,**/obj/**"
# Re-baseline only if needed (do NOT re-run unless slopwatch reports new noise):
# slopwatch init -f --exclude "artifacts/**,external/**,vcpkg_installed/**,**/bin/**,**/obj/**"

# Local CI rehearsal (~3min on Windows post-S16)
dotnet run --file tools.cs -- ci-sim

# Cake target discovery
dotnet run --project build/_build -- --tree
dotnet run --project build/_build -- --target Info

# Cross-platform (post-push verification)
wsl -- zsh -lc 'cd /home/deniz/repos/sdl2-cs-bindings && git fetch origin && git reset --hard origin/master && git submodule update --init --recursive && dotnet run --file tools.cs -- ci-sim'
ssh Armut@192.168.50.178 'zsh -lc "cd /Users/armut/repos/sdl2-cs-bindings && git fetch origin && git reset --hard origin/master && git submodule update --init --recursive && dotnet run --file tools.cs -- ci-sim"'

# GitHub release pipeline (post-push)
GH_TOKEN= gh run list --limit 5
GH_TOKEN= gh run watch <runId> --interval 30 --exit-status
```

## Skills checklist

When starting:

1. Invoke `superpowers:using-superpowers` — sets up skill discovery (loaded at session start automatically).
2. **Don't pick the next slice unilaterally.** Verify baseline, then ask Deniz which stream/item to open. Strategic options: V1 test deprecation (S17 P11 candidate), PD-7 PublishPublic real impl, SDL2_net bindings, or parking-lot polish.
3. Before any new slice → `superpowers:brainstorming` for design alignment, then `superpowers:writing-plans` for executable plan, then user approval, then `superpowers:executing-plans` (or `subagent-driven-development` if scope warrants).

Skills you'll likely need:

- `superpowers:verification-before-completion` — at every gate.
- `superpowers:brainstorming` — when next slice scope is opened.
- `superpowers:writing-plans` — once scope is locked.
- `superpowers:executing-plans` — for inline execution.

## Final steering note

**ADR-002 migration is complete.** The architectural ratchet that drove S02–S16 has clicked closed. Future build-host work happens within the established target-centric topology, not against pre-ADR scaffolding.

**Don't repeat work already done.** S16 P10 was atomic and merged; the ADR migration is closed. Strategic next-step depends on Deniz's priority signal: PD-7 (highest-value but bigger surface), V1 deprecation (clean infra slice), SDL2_net (native pipeline scaling), or parking-lot polish.

**Slice scratch lifecycle** — `docs/refactoring/2026-05-10-p10-*-design.md` + `-plan.md` + this priming prompt all delete from working tree once the next slice's commit lands per `docs/refactoring/README.md`. This priming prompt is the new working-tree scratch for the next session; delete it on next slice close.

---

**Quick start for next agent (TL;DR):**

1. `git status` + `git log -3 --oneline` + `git branch --show-current` → confirm baseline matches expected (master @ P10 closure commit, working tree clean).
2. Run gates → confirm 587 PASS, 0/0 build, 0 slopwatch issues.
3. Optionally re-run ci-sim → expect 8/8 PASS in ~165–190s.
4. Don't pick next slice unilaterally — Deniz steers between V1 deprecation, PD-7, SDL2_net, or parking-lot.
5. Once scope locked → brainstorm → plan → executing-plans flow.

Welcome aboard. ADR-002 closed; new horizons. İyi commit'ler!
