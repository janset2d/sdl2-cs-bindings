# General Deep-Dive Code Review — Full Repo Audit

- **Date:** 2026-04-18
- **Reviewer:** Claude Opus 4.7 (1M context)
- **Prompt:** [`.github/prompts/general-deep-dive-code-reviewer.prompt.md`](../../.github/prompts/general-deep-dive-code-reviewer.prompt.md)
- **Independence note:** Per user instruction, no other docs under `docs/reviews/` were read; findings here are independent of the other dated reviews and may overlap.

---

## A. Scope And Assumptions

**Reviewed:** Full repo at the working-tree state of `master` (head `eececd8`). Subsystems mapped end-to-end: Cake build host (`build/_build`, ~5,500 LoC), TUnit suite (`build/_build.Tests`, 324 tests), packaging layer (`build/manifest.json`, `src/native/**`, `_shared/Janset.SDL2.Native.Common.targets`), CI workflows (`.github/workflows/**`), smoke tests (`tests/smoke-tests/**`), root MSBuild policy (`Directory.Build.props/.targets`, `Directory.Packages.props`).

**Mode:** Read-only review with proposed fixes (not implemented). User authorized non-destructive command execution and proposed-fix output.

**Verification performed:**

- Test suite: `dotnet test build/_build.Tests/Build.Tests.csproj` — **324 / 324 passed (TUnit, net9.0, win-x64)**, matches the count in memory.
- Cross-checked `release-guardrails.md` to verify guardrail-letter references in code comments.
- Cross-checked working-tree `docs/README.md` diff vs actual `docs/reviews/` directory contents.
- Read all five `src/native/*.Native/*.csproj`, both per-family `buildTransitive` shape and the shared `Janset.SDL2.Native.Common.targets`.

**Not verified:** Cake harvest end-to-end (no vcpkg install), `PackageConsumerSmoke` task on real packages, cross-platform behavior (Linux/macOS), CI workflow execution. See §E.

**Material assumptions:**

- Memory-stored facts about Strategy Layer being skeletal-by-design (HybridStaticValidator only) and PA-2 being landed but witness runs pending were treated as claims to verify, not premises. They held against the code.
- Off-limits: `docs/reviews/` (not read), per the user's explicit instruction.

---

## B. Findings

### [High] H1 — `release-candidate-pipeline.yml` is a non-functional stub committed to the default branch with stale references

- **Location:** [`.github/workflows/release-candidate-pipeline.yml`](../../.github/workflows/release-candidate-pipeline.yml) (267 lines, all jobs)
- **Evidence type:** Observed in code
- **Confidence:** High
- **The reality:** The workflow is `workflow_dispatch`-only, and every step that does meaningful work is a placeholder (`# Placeholder for actual script/tool to perform validation`). It writes `dummy.txt` files into a fake `harvest_output/`, emits `"dummy native package"` strings into fake `.nupkg` files (lines 248–251), and the `Determine Build Matrix` step hardcodes a stale version `"2.32.4.0"` (line 70) — the real SDL2 version in `build/manifest.json:186` is `2.32.10`. The `Read Configurations & Validate Versions` step echoes that it reads `build/runtimes.json` (line 47) — that file was merged into `manifest.json` schema v2 long ago and no longer exists. Worst case, an operator dispatches it expecting a release-candidate build and gets a successful CI run that publishes nothing meaningful while looking green.
- **Why it matters:** This is the canonical "stub committed as if real." It pollutes the release-pipeline mental model, breaks new-contributor reading flow (the file is named like the actual release entry point), and references retired schema artifacts. It also uses `actions/upload-artifact@v7` and `actions/download-artifact@v8` for the "dummy" output, which means there is real workflow infrastructure surrounding fake logic — the surface area looks operational.
- **Recommended fix (preferred):** Either:
  - **(A)** Replace the body with a single failing step: `run: echo "Stream D-ci not implemented. See docs/phases/phase-2-adaptation-plan.md." && exit 1`. Drop the `runtimes.json` reference and stale version. This makes "stub" loud and eliminates the chance of someone running it.
  - **(B)** If you genuinely want the scaffolding for incremental Stream D-ci work, add a top-level guard (`if: vars.RELEASE_PIPELINE_ENABLED == 'true'`) and a banner job that always runs and fails unless a maintainer-only repo variable is set, and fix the `runtimes.json` / `2.32.4.0` references inline.
- **Tradeoff:** None — the workflow has no current consumers (memory + plan.md confirm Stream D-ci unimplemented).

---

### [High] H2 — CI matrix hardcodes RIDs / triplets / library list with no guard against `manifest.json` drift

- **Location:** [`.github/workflows/prepare-native-assets-main.yml`](../../.github/workflows/prepare-native-assets-main.yml) (lines 19, 27, 49, 79–86); platform workflows pass hardcoded `--library` lists, e.g. [`prepare-native-assets-windows.yml:61-66`](../../.github/workflows/prepare-native-assets-windows.yml#L61-L66) (`--library SDL2 --library SDL2_image --library SDL2_mixer --library SDL2_ttf --library SDL2_gfx --library SDL2_net`)
- **Evidence type:** Observed in code
- **Confidence:** High
- **The reality:** Three workflow files duplicate the (RID, triplet, runner, container_image) tuple list that already lives in `build/manifest.json:9-17`. The `--library` argument list is duplicated against `build/manifest.json:182-267`. PA-1 closure (memory + plan.md) explicitly chose to keep Stream C "RID-only" and let strategy be a runtime-row attribute, but no automated check enforces that the workflow matrix and the manifest stay in sync. Currently they happen to agree (verified by direct comparison).
- **Why it matters:** This is the exact drift class the docs warn about ("Cake as single orchestration surface", "Dynamic matrix generated from manifest.json", `plan.md:46`). The next time someone adds a RID to `manifest.json`, they have to remember to touch three YAML files, or the matrix silently underbuilds. It also contradicts the canonical decision row in `plan.md`: *"CI matrix: 7 RID jobs, not library×RID — Matrix generated dynamically from manifest.json."*
- **Recommended fix (preferred):** Add a small `pre_flight` job to `prepare-native-assets-main.yml` that runs before the platform jobs and either:
  - **(A)** Generates the matrix as JSON (`jq` against `build/manifest.json`) and feeds it to the platform jobs via `needs.<job>.outputs.matrix` — closes the drift class entirely.
  - **(B)** Cheaper: parses `build/manifest.json` and asserts every `runtimes[].rid` and every `library_manifests[].name` appears in the static matrix; fails fast on drift. Keeps the YAML readable while putting a guardrail on the duplication.
- **Tradeoff:** Option A removes the readable YAML matrix block (you lose at-a-glance "what RIDs do we ship?"). Option B keeps that, at the cost of still maintaining the duplication. Either way, this matches `Stream C: PreFlightCheck as CI gate, dynamic matrix generation` which is the next active milestone in the plan.

---

### [Medium] M1 — Stale guardrail reference in `HarvestTask` post-harvest invariant comment

- **Location:** [`build/_build/Tasks/Harvest/HarvestTask.cs:253-267`](../../build/_build/Tasks/Harvest/HarvestTask.cs#L253-L267)
- **Evidence type:** Observed in code; cross-referenced against `docs/knowledge-base/release-guardrails.md`
- **Confidence:** High
- **The reality:** The comment opens with `// G1 post-harvest invariant: a successful harvest must have produced at least one primary binary.` But `release-guardrails.md:25` says: *"S1 2026-04-17: Guardrails G1 (PrivateAssets="all"), G2, G3, G5, G8 RETIRED."* The same canonical doc (`release-guardrails.md:66`) names the post-harvest assertion **G50**. The thrown `CakeException` message also omits the guardrail tag entirely (`HarvestTask.cs:261-267`), so even an operator reading the failure text loses the cross-link to the canonical guardrail registry.
- **Why it matters:** A maintainer chasing "what's G1?" lands on a retired-guardrail block in `release-guardrails.md`, finds no match, and either assumes the comment is a bug (it is) or that the doc is out of date (it isn't). This is the exact failure mode the prompt's documentation lens calls out (XML/markdown lying about current behavior). The check itself is correct — only the label is stale.
- **Recommended fix:** Rename comment label and tag the error message. Concrete diff:

```csharp
        // G50 post-harvest invariant: a successful harvest must have produced at least one
        // primary binary. Defence-in-depth for the case where the walker and planner each
        // returned OK-shaped results but the resolved primary set ended up empty (silent
        // feature-flag degradation, partial vcpkg install, etc.). BinaryClosureWalker is
        // the primary guard, but this post-check ensures no downstream consumer ingests a
        // rid-status=true with zero primaries.
        if (statistics.PrimaryFiles.Count == 0)
        {
            var message =
                $"G50: Harvest produced zero primary binaries for '{manifest.Name}' on {_runtimeProfile.Rid} " +
                $"(triplet '{_runtimeProfile.Triplet}'). Closure walker and planner returned success but " +
                "no primary files were deployed. Inspect vcpkg install output and manifest primary_binaries patterns.";
            ...
        }
```

- **Tradeoff:** None.

---

### [Medium] M2 — `PackageOutputValidator` XML doc names retired guardrail letters and omits the live G51 check

- **Location:** [`build/_build/Modules/Packaging/PackageOutputValidator.cs`](../../build/_build/Modules/Packaging/PackageOutputValidator.cs) — class summary `lines 15-36`, method summary on `EvaluateNativePackageLayoutAsync` `lines 733-737`
- **Evidence type:** Observed in code
- **Confidence:** High
- **The reality:** Two separate doc-vs-code drifts in the same file:
  1. The method-level XML summary on `EvaluateNativePackageLayoutAsync` (line 733-737) says: *"G28 + G29 — native package layout checks."* Neither G28 nor G29 exists in `release-guardrails.md`. The actual invariants enforced inside the method emit `G47` (`line 832`) and `G48` (`line 860`) error messages, and a third invariant — `G51` (`EvaluateLicensePayloadPresence`, lines 779–797) — is also called from this same method (line 767) but is missing from the doc.
  2. The class-level XML summary (lines 19-31) enumerates only G21, G22, G23, G25, G26, G27, G47, G48 — `G51` is absent despite being live and emitting a hard error on missing license payload.
- **Why it matters:** These XML docs are the contract description for the most reliability-critical class in the packaging path (8 active guardrails enforce post-pack invariants). A maintainer modifying validation logic reads them first. A `///` summary that points at retired guardrail letters is worse than no comment at all — it actively misleads.
- **Recommended fix:** Two doc rewrites (no behavioral change). Concrete:

**Class summary (replace lines 22–31):**

```csharp
/// <list type="bullet">
///   <item><description>G21 — all family dependencies (within AND cross) emitted as bare minimum range `x.y.z`.</description></item>
///   <item><description>G22 — all TFM dependency groups agree.</description></item>
///   <item><description>G23 — managed and native packages emit identical <c>&lt;version&gt;</c> elements (primary within-family coherence check).</description></item>
///   <item><description>G25 — managed symbol package (.snupkg) is present and valid.</description></item>
///   <item><description>G26 — nuspec <c>&lt;repository&gt;</c> commit matches expected SHA.</description></item>
///   <item><description>G27 — nuspec metadata (id, authors, license, icon) matches project metadata.</description></item>
///   <item><description>G47 — native package ships the consumer-side buildTransitive contract (per-family wrapper + shared common.targets).</description></item>
///   <item><description>G48 — every <c>runtimes/&lt;rid&gt;/native/</c> subtree in the native package has the correct payload shape (DLLs on Windows, <c>$(PackageId).tar.gz</c> on Unix).</description></item>
///   <item><description>G51 — native package contains at least one entry under <c>licenses/</c> (last line of defence against missing third-party attribution if upstream Harvest invalidation is bypassed).</description></item>
/// </list>
```

**Method summary (replace lines 733–737 on `EvaluateNativePackageLayoutAsync`):**

```csharp
    /// <summary>
    /// G47 + G48 + G51 — native package layout checks. Opens the native .nupkg once and
    /// inspects entries for the buildTransitive contract (G47), the per-RID payload shape
    /// (G48), and license payload presence (G51). All three concerns live on the native
    /// package only.
    /// </summary>
```

- **Tradeoff:** None.

---

### [Medium] M3 — Working-tree change to `docs/README.md` indexes 5 review docs but the directory contains 6

- **Location:** [`docs/README.md` lines 81–88 (working-tree diff)](../README.md); [`docs/reviews/`](../reviews/)
- **Evidence type:** Observed in code (working-tree only — change is unstaged)
- **Confidence:** High
- **The reality:** The unstaged change adds a "Reviews (Dated Assessments)" table with five rows but the directory actually contains six files (the missing one is `2026-04-18-build-tests-deep-dive-codex.md`, the largest of the build-tests pair). After this review lands, the count will be 7 (this file is review #7) and the `2026-04-18-general-deep-dive-review-claude-opus.md` row will need to be added too.
- **Why it matters:** AGENTS.md (`§Docs-First Workflow → Change Hygiene`) calls docs first-class artifacts. An incomplete index row drifts on day one — the rest will rot.
- **Recommended fix:** Add the missing row to the working-tree edit before committing it, plus the row for this file. Suggested format (descriptions are placeholders; reviewer-of-record knows the actual scopes):

```markdown
| [2026-04-18-build-tests-deep-dive-codex.md](reviews/2026-04-18-build-tests-deep-dive-codex.md) | Companion deep dive on the `build/_build.Tests` suite (Codex)  | 2026-04-18 |
| [2026-04-18-general-deep-dive-review-claude-opus.md](reviews/2026-04-18-general-deep-dive-review-claude-opus.md) | Repository-wide deep-dive review (Claude Opus 4.7) — packaging guardrails, CI drift, doc-vs-code coherence | 2026-04-18 |
```

- **Tradeoff:** None.

---

### [Medium] M4 — GitHub-provided actions pinned only to major-version tags, not commit SHAs

- **Location:** [`.github/workflows/`](../../.github/workflows/) (every workflow); composite action `[.github/actions/vcpkg-setup/action.yml](../../.github/actions/vcpkg-setup/action.yml)`
- **Evidence type:** Observed in code
- **Confidence:** High
- **The reality:** Every external action uses a major-version tag: `actions/checkout@v6`, `actions/setup-dotnet@v5`, `actions/cache@v5`, `actions/upload-artifact@v7`, `actions/download-artifact@v8`. None are pinned to commit SHAs.
- **Why it matters:** This repo ships **native binaries** built from source through CI and will (per `plan.md`) push to GitHub Packages and NuGet.org. Major-tag pinning gives an upstream maintainer (or attacker who compromises an action repo) a pre-release window to ship new code that runs in the build path. SLSA / GitHub's own hardening guide and most NuGet-publishing OSS projects (e.g. Microsoft.Extensions, Roslyn) pin to SHAs for this reason.
- **Recommended fix:** Replace each `@vN` with `@<full-40-char-sha> # vN.M.K`. Add `dependabot.yml` with `package-ecosystem: github-actions` so Dependabot keeps the SHAs current. Low-effort, durable.
- **Tradeoff:** Slightly noisier diffs when actions update; fully justified by the supply-chain posture appropriate for a native-binary publisher.

---

### [Medium] M5 — Indexed reviews live under `docs/reviews/` but `docs/README.md` doesn't yet describe a creation/maintenance policy

- **Location:** [`docs/README.md`](../README.md) (working-tree change introduces the section)
- **Evidence type:** Observed in code
- **Confidence:** Medium
- **The reality:** The new "Reviews" section adds five (soon to be more) dated assessments without a one-line policy on when these are created, who authors them, what triggers a re-review, or whether they age out / are archived. The other doc tables in the same file (e.g. "Phases", "Knowledge Base", "Playbook") all describe self-evident purposes.
- **Why it matters:** Six concurrent reviews of the same repo on the same day with no policy will accumulate. In six months, it'll be unclear which is the current truth and which is historical context. The repo already had this exact pattern bite it on the strategy-layer / exact-pin / S1 docs (memory's "strategy honesty" memory exists because earlier docs over-claimed; reviews could trigger the same).
- **Recommended fix:** Add a one-paragraph policy block above the table — something like:

```markdown
### Reviews (Dated Assessments)

> **Policy:** External code reviews are stored here as dated, authored snapshots. They reflect the
> reviewer's read at one point in time and are NOT canonical status — `plan.md` and the active phase
> doc remain the source of truth. Reviews that are fully addressed should reference the resolution
> commit/PR; reviews older than ~3 months should be moved to an `archive/` subfolder if not still actionable.

| Document | Scope | Date | Status |
| --- | --- | --- | --- |
| ... |
```

The "Status" column gives a place to track whether each review is open / resolved / archived. Alternative: drop the per-file entries entirely and just point at the directory — but that loses the description column.

- **Tradeoff:** None significant.

---

### [Low] L1 — Commented-out dead line in `LddTask`

- **Location:** [`build/_build/Tasks/Dependency/LddTask.cs:24`](../../build/_build/Tasks/Dependency/LddTask.cs#L24)
- **Evidence type:** Observed in code
- **Confidence:** High
- **The reality:** Line 24 is `// var rawOutput = await Task.Run(() => context.Ldd(settings)).ConfigureAwait(false);` — superseded by line 25 (`context.LddDependencies()`). No git annotations, no TODO context.
- **Why it matters:** Trivial. Adds no value, adds future confusion ("was this needed?"). The kind of line that survives ten refactors and accumulates "what if we need it back?" superstition.
- **Recommended fix:** Delete line 24.
- **Tradeoff:** None.

---

### [Low] L2 — `tests/Sandbox/` is committed but isn't a test, isn't run by CI, and hardcodes an absolute Visual Studio install path

- **Location:** [`tests/Sandbox/Sandbox.csproj`](../../tests/Sandbox/Sandbox.csproj), [`tests/Sandbox/Program.cs`](../../tests/Sandbox/Program.cs)
- **Evidence type:** Observed in code
- **Confidence:** High
- **The reality:** Console app under `tests/` that probes `C:\Program Files (x86)\Microsoft Visual Studio\Installer\vswhere.exe` to invoke `vcvarsall.bat` then `dumpbin.exe`. Not referenced by `build/_build` or any test runner. Naming places it in the test tree even though `Tests/` semantics don't apply.
- **Why it matters:** Two minor issues compounded: (a) the absolute path will not exist on Linux/macOS or on Windows installs that landed in `D:\` / non-default paths — and there's no fallback. (b) The presence under `tests/` falsely suggests it's a test surface and inflates the test count discussion (it's why test-count audits diverge — the suite is 324, not "324 + Sandbox + smoke").
- **Recommended fix:** Either (a) move it to `tools/sandbox/` or `artifacts/dev-tools/` with a one-line `README.md` explaining its scope, or (b) delete it if `dumpbin` invocation is now better served by `Tools/Dumpbin/` in the build host (which it appears to be — the build host has a real `WindowsDumpbinScanner`). Path A is lower-effort; path B is cleaner if the sandbox has been superseded.
- **Tradeoff:** Low payoff but low cost.

---

### [Low] L3 — `release-candidate-pipeline.yml` placeholder text still references retired `runtimes.json`

- **Location:** [`.github/workflows/release-candidate-pipeline.yml:47`](../../.github/workflows/release-candidate-pipeline.yml#L47)
- **Evidence type:** Observed in code; cross-referenced against `AGENTS.md §Configuration File Relationships`
- **Confidence:** High
- **The reality:** Echoed message in the placeholder step says: `Reading build/manifest.json, vcpkg.json, build/runtimes.json...`. AGENTS.md (line 224) explicitly says: *"Legacy `runtimes.json` and `system_artefacts.json` files may still exist in history or older notes, but they are no longer the source of truth."* `runtimes.json` no longer exists in the working tree. Subsumed by H1 but worth its own bullet because it's a one-line fix and the same pattern likely lurks in other placeholder text.
- **Recommended fix:** When fixing H1, drop the `runtimes.json` reference. If the workflow stays as a stub (option A in H1), the fix collapses to the single-line failing step.
- **Tradeoff:** None.

---

### [Note] N1 — `HarvestTask.cs` is 617 lines; tracked by issue #87

- **Location:** [`build/_build/Tasks/Harvest/HarvestTask.cs`](../../build/_build/Tasks/Harvest/HarvestTask.cs)
- **Evidence type:** Observed in code; cross-checked `plan.md`
- **Confidence:** High
- **The reality:** Task class mixes orchestration (RunAsync, ResolveLibrariesToHarvest), per-RID setup (PrepareLibraryOutputForCurrentRid, InvalidateCrossRidReceipts), the harvest pipeline itself (ExecuteHarvestPipelineAsync), error handling (HandleKnownHarvestFailureAsync, HandleOperationalHarvestFailureAsync), result rendering (DisplaySummaryPanel, DisplayPrimaryFilesTable, etc.), and status-file emission (GenerateRidStatusFileAsync, GenerateErrorRidStatusFileAsync). Comments at lines 122–134 and 164–180 acknowledge this and explain the current call-site contract.
- **Why it matters:** Already known and tracked: `plan.md "Phase 2a — Active" → Extract HarvestPipeline service from HarvestTask (#87)`. Calling it out here for completeness, not as a new finding.
- **Recommended fix:** No action — accept the existing #87 backlog item. When it's eventually addressed, the rendering helpers (`Display*`, `Format*`, `ToLocationText`, `DescribeDeploymentStrategy`) are the most cleanly separable seam — they're pure functions over `DeploymentStatistics` and could move into a `HarvestSummaryPresenter` without touching the pipeline.

---

### [Note] N2 — `PackageOutputValidator` is 968 lines with three `[SuppressMessage("MA0051")]` justifications

- **Location:** [`build/_build/Modules/Packaging/PackageOutputValidator.cs`](../../build/_build/Modules/Packaging/PackageOutputValidator.cs) — suppressions at lines 250, 381, 509
- **Evidence type:** Observed in code
- **Confidence:** Medium
- **The reality:** Single class enforces 9 active guardrails (G21, G22, G23, G25, G26, G27, G47, G48, G51) via accumulating `List<PackageValidationCheck>`. Three internal methods carry `[SuppressMessage("Design", "MA0051:Method is too long", Justification = "...guardrail traceability...")]`. Result-collection pattern (lines 100–105) means operators see all failures, not first-throw — that's good. The class is the most fact-dense module in the build host.
- **Why it matters:** This class is doing a lot, and per the prompt's "started clean, ended patched" lens, it's worth a deliberate decision. Splitting per-guardrail (one validator per `G*`) would scatter the result-accumulation pattern across 9 classes and add coordination cost. Splitting by concern (metadata vs dependencies vs payload-shape) preserves the result pattern but reduces in-file co-location of the guardrail comments. The current shape is internally consistent; the suppressions are justified.
- **Recommended fix:** Don't split now. Watch closely if a 10th guardrail lands — the marginal cost of each new check is small but the read cost of the file is already high. If a refactor happens, prefer concern-based splits (metadata-checks / dependency-checks / payload-shape-checks) over per-guardrail splits.
- **Tradeoff:** Splitting would obscure the result-pattern reading flow. Keeping is the better tradeoff today.

---

### [Note] N3 — Strategy layer matches its documented "honest state"

- **Location:** [`build/_build/Modules/Strategy/`](../../build/_build/Modules/Strategy/)
- **Evidence type:** Observed in code
- **Confidence:** High
- **The reality:** Direct verification against `plan.md`'s "Strategy State Audit" caveat:
  - `IPackagingStrategy` is a string-compare helper (`HybridStaticStrategy.IsCoreLibrary` line 22 of 22) — matches docs.
  - `HybridStaticValidator` (lines 21–65) has real logic — closure walking, system-file filtering, primary-file filtering, ValidationMode-aware error/warning emission. Matches docs.
  - `PureDynamicValidator` is a deliberate pass-through (lines 13–25). Matches docs.
  - `StrategyResolver` (lines 17–86) does the real coherence work — strategy field validation, triplet↔strategy match, stock-triplet rejection. Matches docs.
- **Why it matters:** The "strategy_layer_honest_state" memory exists because earlier docs over-claimed the layer. The current code/docs alignment is tight. Worth recording so the next reviewer doesn't relitigate it.
- **Recommended fix:** None — keep monitoring as the layer fills out.

---

## C. Broader Systemic Observations

These don't hit a single file but are worth capturing.

1. **Native csproj DRY pattern is exemplary.** Every `src/native/SDL2.*.Native/*.csproj` has a 5-line body; all policy lives in `src/native/Directory.Build.props`. Adding a 6th satellite (e.g. SDL2.Net.Native) is structurally a 5-line file plus a `manifest.json` row plus a per-family `buildTransitive/Janset.SDL2.Net.Native.targets` wrapper. This is the right ambition level for the rest of the repo.

2. **`Janset.SDL2.Native.Common.targets` is the highest-quality file in the repo.** Comment lines 1–60 explain *why* item-batching beats properties (with the actual production failure mode they observed: "when PackageConsumer.Smoke references both Core and Image, Image's wrapper imports first, Core's last, so Core wins and Image's libSDL2_image.* files never land"). Every reviewer-prompt criterion for "rationale capture" is met. Hold this up as the local style for build-system rationale comments.

3. **The result-collection pattern in PreFlight + PackageOutputValidator is consistent and the right shape.** Both validators evaluate every check, accumulate, then return Pass/Fail with the full set. Operators see the whole compliance picture in one run. Recommend extending this pattern (don't fork into first-throw-wins variants in future modules).

4. **CI workflow architecture is *almost* there.** Composite action `vcpkg-setup` is well-factored with overlay-aware cache busting (`action.yml:35-51`). The cache key includes both the vcpkg submodule commit and the hash of overlay triplets/ports — the right invalidation surface. The remaining gap is the matrix-vs-manifest binding (H2). Once H2 is solved, the CI shape will be cleanly defended.

5. **Stream F (Source Mode native visibility) is fully docs-only.** Confirmed — `JansetSdl2SourceMode` appears 0 times in code. Docs (`docs/research/source-mode-native-visibility-2026-04-15.md`, `docs/phases/phase-2-adaptation-plan.md`) carry the design and the proven mechanism. Plan.md `[ ] Stream F` accurately marks it unimplemented. Honest state — no drift.

---

## D. Open Questions / Confidence Limiters

1. **Cross-platform packaging end-to-end was not exercised.** All packaging assertions on Linux/macOS RIDs are inferred from code reading + the existing test suite (which uses `Cake.Testing` fakes for I/O). The live paths in `Janset.SDL2.Native.Common.targets` (e.g. `tar -xzf` invocation, `_JansetSdlEffectiveRid` resolution chain, .NETFramework AnyCPU win-x64 default) were not run. PA-2 witness runs from memory cover the 4 newly-mapped RIDs and are pending — until they land, the per-RID layout assertions in `PackageOutputValidator` are evidence about *intent*, not *behavior*.
2. **`PackageConsumerSmoke` task was not invoked.** It exists, has 477 lines of supporting runner code, and 13 smoke tests in `PackageSmokeTests.cs`. I couldn't validate that the consumer-restore path actually loads native payloads via `buildTransitive` on a real local feed.
3. **Build-host suite ran on Windows only.** `dotnet test` was invoked on win-x64. The `[Test]` selectors are not platform-conditional, but the `MacOtoolScannerTests` / `LinuxLddScannerTests` exercise process-execution mocks; their real-process behavior on the target OS is not verified.

---

## E. What Was Not Verified

- vcpkg install / harvest end-to-end on any RID
- Real `dotnet pack` of any `.Native` family from Cake (only the validator code path was read)
- `release-candidate-pipeline.yml` execution (it is a stub — finding H1)
- `prepare-native-assets-*.yml` execution (no CI run triggered)
- `tests/smoke-tests/native-smoke/` (CMake / C harness — read README only)
- Cross-platform behavior in any package-time MSBuild target
- `docs/reviews/` other reports (read prohibited per user instruction)
- Any docs file beyond AGENTS.md, plan.md, release-guardrails.md (the latter only for guardrail-letter cross-reference)

---

## F. Brief Summary

Two **High** items: a fully-stub workflow shipped on the default branch (H1) and a CI matrix duplication of `manifest.json` with no drift guard (H2). Both have low-effort fixes and align with the next planned milestone (Stream C / Stream D-ci).

Five **Medium** items are doc-vs-code drifts (M1, M2), an incomplete in-flight working-tree edit to `docs/README.md` (M3, M5), and supply-chain hygiene on action versions (M4). All have concrete one-shot fixes.

The build host **is in good structural shape**: the test suite is 324/324 green, the strategy-layer code matches its documented honest state, the native packaging layer is well-DRYed, and the consumer-side `Janset.SDL2.Native.Common.targets` is the gold-standard rationale-capture file in the repo. The cleanup items here are about documentation accuracy and CI coupling discipline — not about engineering risk in the Phase 2a proof slice.
