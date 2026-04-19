---
name: "S3 Post-ADR-001 Continuation"
description: "Use when resuming forward work on janset2d/sdl2-cs-bindings after the 2026-04-18 ADR-001 landing (D-3seg versioning + package-first consumer contract + Artifact Source Profile abstraction) and the C2/C3 critical-finding closures. Anchors the agent on ADR-001 as the authoritative decision record, the consolidated review index as the tactical backlog, and the remaining V4–V7 implementation waves."
argument-hint: "Optional focus area (e.g. V4 validators, V5 SetupLocalDev, V6 historical/memory, V7 tactical C1/C4, PA-2 witness, Stream C)"
agent: "agent"
model: "GPT-5 (copilot)"
---

You are entering `janset2d/sdl2-cs-bindings` after the 2026-04-18 ADR-001 landing. The ADR-001 diff adopted D-3seg versioning, retired Source Mode in favour of a package-first consumer contract, introduced the Artifact Source Profile abstraction, closed two critical review findings (C2 smoke exact-pin + family-list contract; C3 net462 AnyCPU OSArchitecture fallback), consolidated seven independent code reviews into a single tracked index, and aligned every canonical policy doc + 12 moderate-amend docs + the ADR impact checklist. The repo is **not** in a mid-refactor state; the versioning + consumer-contract decisions are locked. Your job is forward work on waves V4 → V7 of the ADR-001 impact checklist, and your first obligation is to ground yourself in the new canon before you touch anything.

The previous prompt in this slot was [`s2-post-h1-continuation.prompt.md`](s2-post-h1-continuation.prompt.md) — it covered the H1 license-integrity landing + PA-2 mechanism + review meta-pass that led to ADR-001. Its work is done. Do not re-run it unless the user explicitly asks.

## Mandatory Onboarding Before You Touch Anything

These are non-negotiable. The rest of the prompt assumes you have completed them.

### 1. Read the memory index

Before you read any code, inspect the auto-memory index at `C:\Users\deniz\.claude\projects\e--repos-my-projects-janset2d-sdl2-cs-bindings\memory\MEMORY.md` and load the relevant entries. At minimum, internalize:

- **No Scope Creep On Critical Findings** — critical fix scope is strictly the finding, no bundled adjacent cleanup. The user will interrupt a fix that pulls in "while we're here" work.
- **Verify API Claims Before Asserting** — never reject a design option based on unverified third-party API claims. WebSearch / repo-read first.
- **Always Use PathService** — every on-disk path in the Cake build host goes through `IPathService` accessors; no `Combine` chains, no hardcoded relative-path arrays.
- **Honest Progress Narration** — narrate findings, propose, wait for acknowledgement before commit / push / close.
- **Test Fixture Feedback** — fixtures load real JSON via seeders; no static duplicate data.
- **Holistic Thinking** — no waterfall between CI / Cake / csproj / manifest; design across all layers simultaneously.
- **Strong Release Guardrails** — defence-in-depth across PreFlight / MSBuild / post-pack / CI; new invariants land WITH guardrails in the same diff.

Then load, at minimum, these project-state memory files before reading any code:

- `project_s1_post_validation_state.md` — post-H1 state baseline (will be superseded by a new entry once V6 lands; until then, cross-check against ADR-001 for anything that shifted).
- `project_strategy_layer_honest_state.md` — the strategy seam reality check. `INativeAcquisitionStrategy` is now retired by ADR-001 (see ADR §2.7).
- `packaging_strategy_decisions.md` — hybrid-static, LGPL-free, buildTransitive contract, Mono-per-platform, dev env notes.
- `release_lifecycle_direction_2026_04_15.md` — **historical**. Post-ADR-001 direction is authoritative via the decision record itself; expect this memory to be superseded in V6.

If a memory entry contradicts this prompt, the memory entry wins unless the contradiction is explicitly about D-3seg, package-first, or the artifact source profile abstraction — in those three cases, ADR-001 wins.

### 2. Read ADR-001 end to end

[`docs/decisions/2026-04-18-versioning-d3seg.md`](../../docs/decisions/2026-04-18-versioning-d3seg.md) is the authoritative decision record for everything versioning-, consumer-contract-, and feed-prep-related. Not a skim — an actual read. Pay specific attention to:

- §2.1 D-3seg format + examples (and why 4-segment / build counter / upstream-exact-patch variants were rejected).
- §2.4 cross-family dependency contract upper bound `< (UpstreamMajor + 1).0.0`.
- §2.5 the machine-readable `janset-native-metadata.json` + human-readable README mapping table — both are required, neither alone is sufficient.
- §2.6 the package-first consumer contract and the **tek cümle principle**: *"All consumer-facing validation paths use packages; local vs remote changes only how the local feed is prepared."*
- §2.7 `IArtifactSourceResolver` + `ArtifactProfile { Local, RemoteInternal, ReleasePublic }`. Local resolver is Phase 2a; Remote/Release are contract-only stubs until Phase 2b.
- §2.8 `SetupLocalDev` Cake task + `Janset.Smoke.local.props` consumer override mechanism.
- §2.9 new guardrails G54–G57 (PreFlight UpstreamMajor.Minor coherence, post-pack native metadata file, post-pack satellite upper bound, post-pack README mapping table currency).
- §5 Non-goals (read twice — these are the things NOT to reintroduce).
- §7 Impact Checklist — your live tracking surface. V1–V3 ticked; V4–V7 pending.

If you cannot, without opening the ADR, describe (a) why `2.32.10.1` and `2.32.10` variants were rejected in favour of `2.32.0`, (b) where `janset-native-metadata.json` lives (inside which nupkg, at what path, generated by what), (c) what `< (UpstreamMajor + 1).0.0` means for a satellite on SDL2_image 2.8.x, (d) why Source Mode was retired and what replaces it for smoke / example / sandbox consumers — then you haven't done the reading yet. Go back.

### 3. Read the consolidated review index

[`docs/reviews/2026-04-18-consolidated-review-index.md`](../../docs/reviews/2026-04-18-consolidated-review-index.md) is the single entry point to the seven 2026-04-18 code reviews. It de-duplicates ~50 raw findings into 39 tracked items (C1-C4 critical, H4-H10 high, M11-M25 medium, L26-L39 low, N1-N7 notes), carries per-item spot-verification state + source-review cross-references, and is updated in-place as waves land.

Status at handoff time (2026-04-18):

| ID | Short name | Status |
| --- | --- | --- |
| **C2** | smoke exact-pin + family-list contract | **Resolved** (V5-era C2 landed with the ADR-001 diff) |
| **C3** | net462 AnyCPU OSArchitecture fallback | **Resolved** (ADR-001 diff) |
| **C1** | `Janset.SDL2.sln` restore red (smoke projects) | **OPEN** — V7 scope |
| **C4** | `Dumpbin-Dependents`/`Ldd-Dependents` crash without `--dll` | **OPEN** — V7 scope |
| **H4** | repo-root fallback → `build/_build/bin` when git unavailable | **OPEN** — V7 scope |
| **H5** | G48 unix payload non-exclusive | **OPEN** — V7 scope |
| **H6** | G48 completeness vs manifest RID set | **OPEN** — V7 scope (design alignment required; ADR §2.9 defers strict enforcement to Phase 2b) |
| **H7** | release-candidate-pipeline.yml stub | **OPEN** — V7 Stream C bundle |
| **H8** | CI matrix duplicates manifest | **OPEN** — V7 Stream C bundle |
| **H9** | Compile.NetStandard `_surface` S1144/CA1823 | **Meta-verified** — V7 scope (suppression or module-initializer use-site) |
| **H10** | BinaryClosureWalker filename-token owner | **Meta-verified** — V7 scope |
| **M11** | stale G1/G28/G29 labels | **OPEN** — V7 scope |
| **M12** | linux-arm64 runner drift | **OPEN** — V7 Stream C bundle |
| **M13** | invalid explicit `--repo-root` / `--vcpkg-dir` silent fallback | **OPEN** — V7 scope |
| **M14** | Native.Common.targets publish/net462 copy missing diagnostic | **OPEN** — V7 scope |
| **M15** | contributor docs drift | **Partially resolved** — root `README.md` mpg123/FluidSynth wording deferred to V7 |
| **M16** | plan.md self-contradiction | **Resolved** (V2 canon align) |
| **M17–M25** | mid-severity hygiene / missing coverage | **OPEN** — V7 scope |
| **L26–L37** | low-severity cleanup | **OPEN** — V7 scope, batchable |
| **L38** | docs/README.md review row-count | **Resolved** (V3 sweep) |
| **L39** | docs/reviews/ retention policy | **Resolved** (V3 sweep) |
| **N1-N7** | notes / no action | — |

Before proposing code changes, cross-check the finding ID against this table — the status column is authoritative.

### 4. Internalize the post-ADR-001 guardrail set

The full registry lives at [`docs/knowledge-base/release-guardrails.md`](../../docs/knowledge-base/release-guardrails.md). Current active guardrails relevant to forward work:

**H1-era (still active):**

- **G49** — manifest core-library identity coherence (PreFlight).
- **G50** — post-harvest primary-count ≥ 1 (HarvestTask post-deploy).
- **G51** — native nupkg ships ≥ 1 `licenses/` entry (post-pack).
- **G52** — pack pre-gate checks `runtimes/` AND `licenses/_consolidated/` specifically (not parent `licenses/`).
- **G53** — ConsolidateHarvest staged-replace invariant.

**ADR-001-era (NEW — implementation lands in V4):**

- **G54** — family tag UpstreamMajor.UpstreamMinor ≡ `manifest.json library_manifests[].vcpkg_version` Major.Minor (PreFlight).
- **G55** — native nupkg ships `janset-native-metadata.json` with schema-valid content matching vcpkg-resolved upstream + git SHA (post-pack).
- **G56** — satellite cross-family nuspec dependency declares explicit upper bound `< (UpstreamMajor + 1).0.0` (post-pack).
- **G57** — README mapping table block between `<!-- JANSET:MAPPING-TABLE-START -->` / `<!-- JANSET:MAPPING-TABLE-END -->` markers matches manifest (post-pack).

**Deferred to Phase 2b (documented but NOT a Phase 2a deliverable):**

- Strict release-must-bump-patch enforcement (API diff / native-hash-based). Phase 2a scope is file-existence overwrite-attempt guard only. See ADR §2.9.

Before proposing a change that touches compliance surface (versioning, dependency shape, pack gate, consumer override), you must know which guardrail owns which failure mode. If you are about to add a new invariant, it lands WITH a guardrail entry in the same diff (strong-guardrails feedback rule).

### 5. Know the current active work-stream map

Consult [`docs/plan.md` Strategic Decisions](../../docs/plan.md) and [`docs/phases/phase-2-adaptation-plan.md` Stream F + Pending Decisions](../../docs/phases/phase-2-adaptation-plan.md) for the post-ADR-001 state, and [ADR-001 §7 Impact Checklist](../../docs/decisions/2026-04-18-versioning-d3seg.md) for the wave-by-wave implementation status.

- **V1 ADR landing — DONE.**
- **V2 canon docs align — DONE** (6 files: release-lifecycle-direction, release-guardrails, plan, phase-2-adaptation-plan, execution-model-strategy, source-mode-native-visibility).
- **V3 moderate docs sweep — DONE** (12 files including playbooks, research notes, onboarding, phase-2-cicd-packaging, docs/README.md; closes M16, L38, L39, partially M15).
- **V4 manifest + model cleanup + validators + metadata — NEXT.** Remove `native_lib_version` from manifest schema + model + fixtures + seeders; implement `UpstreamVersionAlignmentValidator` (G54), `NativePackageMetadataValidator` (G55), `SatelliteUpperBoundValidator` (G56), `ReadmeMappingTableValidator` (G57) with test coverage; pack-time `janset-native-metadata.json` generator; pack-time README mapping-table generator with HTML comment markers; wire G54–G57 into `PackageOutputValidator`'s result-accumulation pattern.
- **V5 profile abstraction + SetupLocalDev.** `IArtifactSourceResolver` + `ArtifactProfile` enum + `LocalArtifactSourceResolver` (concrete impl) + `RemoteInternalArtifactSourceResolver` / `ReleaseArtifactSourceResolver` (contract-only stubs). `SetupLocalDev` Cake task with `--source=local` fully wired. `Janset.Smoke.local.props` conditional import + .gitignore. Playbook Local Dev section expanded.
- **V6 historical markers + memory sidecar.** Retired/SUPERSEDED markers on `exact-pin-spike-and-nugetizer-eval-2026-04-16.md`, `release-lifecycle-patterns-2026-04-14-claude-opus.md`, `release-lifecycle-strategy-research-2026-04-14-gpt-codex.md`, `release-strategy-history-audit-2026-04-14-gpt-codex.md`. Memory updates: `packaging_strategy_decisions`, retire `release_lifecycle_direction_2026_04_15`, create `versioning_d3seg_decision_2026_04_18`.
- **V7 tactical backlog.** C1 sln cleanup; C4 `--dll` contract fix; H4-H10 hardening (repo-root fallback, G48 tightening, Compile.NetStandard analyzer, owner inference, etc.); Stream C bundle (H7 release-candidate-pipeline rewrite + H8 CI matrix generator + M11 G1/G28/G29 label fixes + M12 linux-arm64 runner alignment); remaining M/L findings in review index order.

Open issues that belong in the next agent's mental model:

- **#87** — HarvestPipeline extraction from HarvestTask (type:cleanup, deferred).
- **#88** — Experiment: `Process.Kill(entireProcessTree: true)` for `PackageConsumerSmokeRunner` build-server hygiene.
- **#89** — Research: consumer package-upgrade testing (N → N+1 symlink churn, stale file behaviour).
- **PA-2 witness runs** — four workflow-dispatch PostFlight invocations on `ubuntu-24.04-arm` (linux-arm64), `macos-latest` (osx-arm64), `windows-latest` (win-arm64 + win-x86). Strictly a button-press task, not a code change; blocks Stream C's behavioural claim. Per-RID commands + acceptance criteria in [`docs/playbook/cross-platform-smoke-validation.md` "PA-2 Per-Triplet Witness Invocations"](../../docs/playbook/cross-platform-smoke-validation.md).
- **SDL2-CS upstream PR** — two confirmed `EntryPointNotFoundException` defects tracked in `docs/knowledge-base/cake-build-architecture.md` "SDL2-CS Submodule Boundary". Low-cost community contribution; retired naturally when the AST binding generator replaces SDL2-CS.
- **PD-7 full-train orchestration research** and **PD-8 manual escape hatch research** — both amended by ADR-001 (see their respective addenda) but scope otherwise unchanged. Open research sessions separately.

## Hard Scope

You are entering a locked-decision state for ADR-001. Do **not** treat this as a revisit.

- Do **not** re-open the D-3seg versioning decision. `<UpstreamMajor>.<UpstreamMinor>.<FamilyPatch>` with no build segment is the canonical format. If you find a concrete mechanical defect, raise it as a finding; do not re-pitch alternatives A/B/C/D-4seg.
- Do **not** resurrect Source Mode as a consumer contract. ProjectReference-chain Content injection is retired. The `SetupLocalDev --source=local|remote` pair is the only sanctioned feed-prep abstraction. If the binding-debug fast-loop use case comes up, respond with "separate opt-in throwaway harness", not "reintroduce Source Mode".
- Do **not** add a `native_lib_version` field back to the manifest schema. It is an orphan that V4 will remove. If you find code that reads it before V4 lands, raise it as a finding first.
- Do **not** modify `ManifestConfig.CoreLibrary` semantics, the `ConsolidationState` receipt shape, or the `_consolidated/` layout without a specific breaking defect. These remain H1 load-bearing.
- Do **not** touch the SDL2-CS submodule working tree. The boundary is documented; fix repo-local code, not the submodule.
- Do **not** compose `.Combine("segment").Combine("segment")` chains or hardcoded relative-path arrays. Add a `PathService` accessor.
- Do **not** change `--family-version`'s acceptance of multiple families at a single version as a breaking change; the per-family version override is tracked as a V5 orchestration refinement, not a breaking semantic shift.
- Do **not** declare PA-2 witnesses complete based on Windows-only runs. Witnesses require each row's native runner.

Your scope is one concrete wave or one concrete finding at a time, anchored in the ADR-001 Impact Checklist.

## What You Should Do

### 1. Pick a scoped target and justify it

Candidates, in rough priority order (confirm with the user before executing):

1. **Wave V4.** Manifest schema cleanup + 4 new validators + native metadata generator + README mapping-table generator. Ticks G54–G57 in the guardrail registry and closes the code-side of ADR-001. Largest implementation chunk remaining.
2. **Wave V5.** Profile abstraction interface + `LocalArtifactSourceResolver` + `SetupLocalDev` Cake task + `Janset.Smoke.local.props` consumer override. Delivers the IDE-open-and-restore flow that the ADR promises. Depends on V4 for the metadata generator but the two can run in parallel if the developer accepts the ordering complexity.
3. **PA-2 witness runs.** Four workflow-dispatch invocations + result capture. User drives the button press; agent prepares capture doc template + triages results. Unblocks Stream C.
4. **Wave V7 Stream C bundle.** H7 release-candidate-pipeline rewrite + H8 CI matrix generator + M11 stale G1/G28/G29 labels + M12 linux-arm64 runner alignment. Natural cluster; lands after PA-2 witnesses so the real runner labels are known.
5. **Wave V7 C1 + C4.** Critical operator-facing fixes (sln restore red, `--dll` crash). Small isolated diffs.
6. **Wave V6.** Historical markers on superseded research + memory sidecar update. Lower priority than V4/V5 but keeps the docs-graph honest.
7. **Wave V7 remaining M/L.** Finer-grained. Batch by theme.

State your target up front, justify why it is the right next step given the open blockers and user priorities, and get user confirmation before executing.

### 2. Follow repository discipline

- Every new module follows the Harvesting shape: thin task + narrow services + typed Results with the full `OneOf.Monads` surface.
- Every on-disk path goes through `IPathService`. Zero tolerance for `.Combine` chains or hardcoded relative-path arrays in production code (tests may use `.WithTextFile("relative/path", …)` via `FakeRepoBuilder`, but that itself is a path-input API — not a production layout statement).
- Every new behavioural claim is backed by a test in `build/_build.Tests/` or a reproducible Cake invocation. Fixtures use the seeder pattern.
- Every new invariant lands with a guardrail entry in `docs/knowledge-base/release-guardrails.md` — not as a follow-up commit.
- Every version string used as an example in docs / tests / scripts follows D-3seg (`<UpstreamMajor>.<UpstreamMinor>.<FamilyPatch>[-suffix]`) with UpstreamMajor.Minor anchored to the relevant family's `vcpkg_version`. No `1.2.0`, no `2.32.10.1`.
- If you touch native payload or consumer-side `.targets` logic, update [`docs/playbook/cross-platform-smoke-validation.md`](../../docs/playbook/cross-platform-smoke-validation.md) and re-run the matrix on all three platforms.
- Commits are conventional; destructive git operations require explicit user confirmation.
- User explicitly chose "no PR workflow; push to master" for this repo — commit cadence is the user's call, not yours.

### 3. Verify on three platforms when it matters

Platform access is documented in [`docs/playbook/cross-platform-smoke-validation.md` "Platform Access"](../../docs/playbook/cross-platform-smoke-validation.md). Use it. Do not claim cross-platform success from Windows-only runs. If you change anything that lives inside a `.nupkg`'s `buildTransitive/` folder or `janset-native-metadata.json` (once G55 is live), a live repack + restore on all three platforms is the bar before declaring it done.

### 4. Keep the ADR impact checklist honest

Every item you land updates its checkbox in [ADR-001 §7 Impact Checklist](../../docs/decisions/2026-04-18-versioning-d3seg.md). Same with the consolidated review index status column for findings you resolve. These are living docs; treat them as first-class artifacts.

If you discover a gap between ADR-001 and code that the ADR didn't anticipate, either amend the ADR with a dated change-log entry (§9 Change log in ADR-001) or open a new ADR in `docs/decisions/` for a distinct cross-cutting decision. Don't let the misalignment rot silently.

### 5. Keep the memory honest

If you make a mistake that repeats a documented pattern (e.g., hardcoded paths, unverified API claims, scope creep on critical findings, narrating-less-than-expected, declaring-cross-platform-done-from-Windows) — cite the specific memory rule in your acknowledgement so the pattern gets reinforced. When V6 lands, memory entries will be updated; until then, cross-check against ADR-001 for anything touching versioning / consumer contract / feed-prep.

## Output Contract

When you start working, respond in this order:

### 1. Onboarding confirmation

Five short statements proving you completed §1–§5 onboarding:

- One sentence summarizing the D-3seg format and why it's 3 segments instead of 4.
- One sentence naming what's in `janset-native-metadata.json` and which guardrail asserts it.
- One sentence summarizing the package-first consumer contract's single-sentence principle.
- One sentence naming the current active blocker (PA-2 witnesses for Stream C, or whatever the user directed) and which review-index findings are currently OPEN.
- One sentence naming the scoped target you are picking for this session.

### 2. Target justification

Two or three sentences on why the target you picked is the right next step given the open blockers and user priorities. If the user already steered you to a target via the prompt argument, acknowledge it and confirm alignment instead.

### 3. Plan

A short, concrete step list for executing the target. Include verification gates (tests, platform runs, doc updates, ADR-impact-checklist ticks, review-index status updates). If any step requires design alignment with the user, flag it explicitly — do not execute design decisions without confirmation.

### 4. Execution

Execute the plan. Pause for user confirmation before:

- Anything destructive (git reset, force push, deleting tracked files).
- Any action visible outside the repo (PR creation, issue close, public feed push, upstream contribution).
- Any decision that reverses a prior Strategic Decision in `plan.md` or contradicts a memory feedback rule or contradicts ADR-001 §2 or §5.
- Any "while we're here" adjacent cleanup inside a critical-finding fix (scope creep rule).
- Any design decision for PD-7, PD-8, or Phase 2b scope (those require their own research session, not inline mid-wave choices).

## Style Requirements

- Be direct. Do not pad with meta-commentary about what you're about to do.
- Prefer evidence over vibes. When you say "the code does X," cite the file and line or quote the symbol.
- Match response length to task complexity. Onboarding confirmation is five sentences, not five paragraphs.
- Do not repeat content from this prompt back at the user unless they ask you to.
- If a plan assumption turns out to be wrong mid-execution, stop, surface the mismatch, and propose a re-scope. Do not paper over it.
- When the user pushes back ("are you sure?", "validate that"), assume the claim is suspect and re-verify immediately — do not double down defensively.
- Version strings in docs, tests, scripts, and commit messages follow D-3seg. If you catch yourself typing `1.2.0` or `2.32.10.1` for a Janset family version, that's the rule-of-thumb signal that something is off — stop and re-anchor on ADR §2.1.
