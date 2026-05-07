# CompileSolution Retirement Implementation Plan (Slice S08)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Retire the `CompileSolution` Cake target. Delete the task, pipeline, DI registration, V1 tests, and Maintenance feature folder. Update the refactor plan and `docs/plan.md` to reflect the retirement and drift-correct the §4 inventory (S07 left a stale `CleanArtifacts` row).

**Architecture:** Atomic single-commit slice mirroring S07's `CleanArtifacts` retirement (commit `7179aaf`). No new code, no new tests — pure deletion + doc updates. All working-tree changes accumulate; verification gate runs once at the end; commit happens only after Deniz approves the proposed commit message.

**Tech Stack:** .NET 10, C# 14, Cake Frosting 6.1, TUnit on Microsoft.Testing.Platform, Slopwatch, PowerShell.

**Spec:** [`docs/refactoring/2026-05-07-compilesolution-retirement-design.md`](2026-05-07-compilesolution-retirement-design.md)

**Approval gate (AGENTS.md):** This slice modifies production code, the build system, and DI composition. Explicit `go / apply / proceed / başla / yap` is required from Deniz before commit. The plan walks the working-tree changes; Task 7 holds at the approval gate before committing.

---

## File Map

### Deleted

| Path | Reason |
| --- | --- |
| `build/_build/Features/Maintenance/CompileSolutionTask.cs` | Retired Cake task |
| `build/_build/Features/Maintenance/CompileSolutionPipeline.cs` | Retired pipeline (zero remaining consumers) |
| `build/_build/Features/Maintenance/ServiceCollectionExtensions.cs` | Retired DI registration (only registered the pipeline) |
| `build/_build/Features/Maintenance/` (folder) | Last resident gone |
| `build/_build.Tests/Unit/Features/Maintenance/CompileSolutionPipelineTests.cs` | V1 ctor null-guard tests for retired type |
| `build/_build.Tests/Unit/Features/Maintenance/` (folder) | Last resident gone |
| `docs/refactoring/2026-05-07-compilesolution-retirement-design.md` | Per the docs/refactoring temp-doc lifecycle (Deniz confirms in Task 7) |
| `docs/refactoring/2026-05-07-compilesolution-retirement-plan.md` | Per the docs/refactoring temp-doc lifecycle (this file — Deniz confirms in Task 7) |

### Modified

| Path | Change |
| --- | --- |
| `build/_build/Program.cs` | Remove `using Build.Features.Maintenance;` import + `.AddMaintenanceFeature()` line in composition root |
| `build/_build.Tests/Unit/CompositionRoot/ServiceCollectionExtensionsSmokeTests.cs` | Remove `using Build.Features.Maintenance;` import + `AddMaintenanceFeature_Should_Register_All_Pipeline_And_Validator_Types` test method |
| `docs/refactoring/target-centric-build-host-refactor-plan.md` | Drop §4 rows for `CleanArtifacts` (drift correction) and `CompileSolution`; add P4 completed item; drop P4 remaining item; add P4 exit criterion |
| `docs/plan.md` | Update Phase X status sentence (line ~17) and Phase X — Build-Host Modernization paragraph (line ~75) |

---

## Task 1: Delete production code

**Files:**
- Delete: `build/_build/Features/Maintenance/CompileSolutionTask.cs`
- Delete: `build/_build/Features/Maintenance/CompileSolutionPipeline.cs`
- Delete: `build/_build/Features/Maintenance/ServiceCollectionExtensions.cs`
- Delete: `build/_build/Features/Maintenance/` directory (must be empty after the three file deletes)

- [ ] **Step 1: Delete the three Maintenance feature files**

```pwsh
Remove-Item build/_build/Features/Maintenance/CompileSolutionTask.cs
Remove-Item build/_build/Features/Maintenance/CompileSolutionPipeline.cs
Remove-Item build/_build/Features/Maintenance/ServiceCollectionExtensions.cs
```

- [ ] **Step 2: Confirm folder is empty, then remove it**

```pwsh
Get-ChildItem build/_build/Features/Maintenance -Force
# Expect: empty (or only auto-restore/.vs metadata, none of which is tracked)
Remove-Item build/_build/Features/Maintenance -Force
```

- [ ] **Step 3: Verify deletion**

```pwsh
Test-Path build/_build/Features/Maintenance
# Expect: False
git status --short | Select-String "Features/Maintenance"
# Expect: three "D " lines (deleted) for the three .cs files
```

No commit yet — working-tree changes accumulate through Task 6.

---

## Task 2: Remove Maintenance from production composition root

**Files:**
- Modify: `build/_build/Program.cs` (line 13: remove using import; line 130: remove `.AddMaintenanceFeature()` call)

The build host registers feature DI groupings via fluent extension calls in `Program.cs`. With Maintenance retired, both the `using` import and the registration call must go. Without removal, the build fails (`AddMaintenanceFeature` no longer exists) and `Build.Features.Maintenance` becomes a dead namespace import.

- [ ] **Step 1: Remove the using import on line 13**

In `build/_build/Program.cs`, find the line:

```csharp
using Build.Features.Maintenance;
```

and delete it. The surrounding using block has Ci, Coverage, DependencyAnalysis, Diagnostics, Harvesting, Maintenance, Packaging, Preflight, Publishing, Vcpkg — alphabetically ordered; removing Maintenance preserves the ordering.

- [ ] **Step 2: Remove the AddMaintenanceFeature() registration**

In `build/_build/Program.cs` around line 130, the composition root reads:

```csharp
services
    .AddHostBuildingBlocks(parsedArgs)
    .AddRepositories()
    .AddIntegrations()
    .AddToolWrappers()
    .AddMaintenanceFeature()
    .AddCiFeature()
    .AddCoverageFeature()
    .AddVcpkgFeature()
    .AddDiagnosticsFeature()
    .AddDependencyAnalysisFeature()
    .AddPreflightFeature()
    .AddHarvestingFeature()
    .AddPublishingFeature()
    .AddPackagingFeature();
```

Remove the `.AddMaintenanceFeature()` line entirely. The chain becomes:

```csharp
services
    .AddHostBuildingBlocks(parsedArgs)
    .AddRepositories()
    .AddIntegrations()
    .AddToolWrappers()
    .AddCiFeature()
    .AddCoverageFeature()
    .AddVcpkgFeature()
    .AddDiagnosticsFeature()
    .AddDependencyAnalysisFeature()
    .AddPreflightFeature()
    .AddHarvestingFeature()
    .AddPublishingFeature()
    .AddPackagingFeature();
```

- [ ] **Step 3: Confirm build host still compiles**

```pwsh
dotnet build build/_build/Build.csproj --nologo
# Expect: Build succeeded. 0 Warning(s), 0 Error(s)
```

If the build fails with `'IServiceCollection' does not contain a definition for 'AddMaintenanceFeature'`, the composition root edit is incomplete. If it fails with `The type or namespace name 'Maintenance' does not exist in the namespace 'Build.Features'`, the using import was not removed.

---

## Task 3: Delete V1 test file and smoke-test method

**Files:**
- Delete: `build/_build.Tests/Unit/Features/Maintenance/CompileSolutionPipelineTests.cs`
- Delete: `build/_build.Tests/Unit/Features/Maintenance/` directory
- Modify: `build/_build.Tests/Unit/CompositionRoot/ServiceCollectionExtensionsSmokeTests.cs` (remove using import and one test method)

- [ ] **Step 1: Delete the V1 test file and folder**

```pwsh
Remove-Item build/_build.Tests/Unit/Features/Maintenance/CompileSolutionPipelineTests.cs
Get-ChildItem build/_build.Tests/Unit/Features/Maintenance -Force
# Expect: empty
Remove-Item build/_build.Tests/Unit/Features/Maintenance -Force
```

- [ ] **Step 2: Remove the using import from the smoke-test file**

In `build/_build.Tests/Unit/CompositionRoot/ServiceCollectionExtensionsSmokeTests.cs`, line 8:

```csharp
using Build.Features.Maintenance;
```

Delete it. The remaining usings stay alphabetically ordered.

- [ ] **Step 3: Remove the AddMaintenanceFeature smoke-test method**

In the same file, lines 33–37 contain:

```csharp
    [Test]
    public async Task AddMaintenanceFeature_Should_Register_All_Pipeline_And_Validator_Types()
    {
        await AssertAllRegisteredTypesResolve(services => services.AddMaintenanceFeature());
    }
```

Delete those five lines plus the trailing blank line. The next test (`AddCiFeature_Should_Register_All_Pipeline_And_Validator_Types`) becomes the first method in the class. Other tests remain unchanged.

- [ ] **Step 4: Confirm tests still compile**

```pwsh
dotnet build build/_build.Tests/Build.Tests.csproj --nologo
# Expect: Build succeeded. 0 Warning(s), 0 Error(s)
```

If compilation fails with `'IServiceCollection' does not contain a definition for 'AddMaintenanceFeature'` or `'CompileSolutionPipeline' could not be found`, the previous edits are incomplete.

---

## Task 4: Update the refactor plan

**Files:**
- Modify: `docs/refactoring/target-centric-build-host-refactor-plan.md` (§4 inventory, §11 P4 completed list, §11 P4 remaining suggested order, §11 P4 exit criteria)

- [ ] **Step 1: §4 inventory — drop both retired-from-Cake rows**

Find the inventory table around lines 109–130. Two consecutive rows (lines 112 and 113):

```text
| `CleanArtifacts` | `Features/Maintenance` | Low-risk maintenance target. |
| `CompileSolution` | `Features/Maintenance` | Low-risk, but touches process/build invocation. |
```

Delete both rows. The `Info` row immediately above and the `GenerateMatrix` row immediately below stay; the table now skips from `Info` straight to `GenerateMatrix`. This drops both the S07-retired `CleanArtifacts` row (drift correction; S07 missed it) and the S08-retired `CompileSolution` row.

- [ ] **Step 2: §11 P4 "Completed in this phase" — append item 7**

Find around line 740. The current list ends at item 6. Append a seventh item directly after item 6:

```text
7. `CompileSolution` **retired** (not migrated — zero Cake callers; bare `dotnet build Janset.SDL2.sln` covers the use case)
```

- [ ] **Step 3: §11 P4 "Remaining suggested order" — drop CompileSolution and flatten**

Find around lines 749–756. Currently:

```text
Remaining suggested order:

1. `CompileSolution`
2. diagnostic targets:
   - `Dumpbin-Dependents`
   - `Ldd-Dependents`
   - `Otool-Analyze`
   - `Inspect-HarvestedDependencies`
```

Replace with the flatter form (only diagnostic targets remain):

```text
Remaining suggested order: diagnostic targets only.

- `Dumpbin-Dependents`
- `Ldd-Dependents`
- `Otool-Analyze`
- `Inspect-HarvestedDependencies`
```

- [ ] **Step 4: §11 P4 "Exit criteria" — add CompileSolution bullet**

Find around lines 780–790. The exit criteria list currently ends with the `testing-guidelines.md` bullet. Insert a new bullet after the `CleanArtifacts retired from Cake (local hygiene in tools.cs).` line and before the `testing-guidelines.md canonical test reference...` line:

```text
- `CompileSolution` retired from Cake (zero callers; bare `dotnet build Janset.SDL2.sln` covers the use case).
```

- [ ] **Step 5: Visual sanity check**

```pwsh
# Verify the §4 table is well-formed
Get-Content docs/refactoring/target-centric-build-host-refactor-plan.md | Select-String -Pattern "^\|" -Context 0 | Select-Object -First 25
# Expect: pipe-rows for the inventory table; no rows containing "CleanArtifacts" or "CompileSolution"
Get-Content docs/refactoring/target-centric-build-host-refactor-plan.md | Select-String -Pattern "CompileSolution"
# Expect: only the new "retired" mentions in §11 (completed list + exit criterion); no inventory or remaining-order hits
Get-Content docs/refactoring/target-centric-build-host-refactor-plan.md | Select-String -Pattern "CleanArtifacts"
# Expect: only the existing S07 mention in §11 completed list + exit criterion + the post-P4 research note; no inventory hit
```

---

## Task 5: Update docs/plan.md

**Files:**
- Modify: `docs/plan.md` (line ~17: Phase X status sentence; line ~75: Phase X — Build-Host Modernization paragraph)

- [ ] **Step 1: Line ~17 — update Phase X status sentence**

Find the sentence ending `... 578 tests, 0 failures. Remaining P4 candidates: \`CompileSolution\` and diagnostic targets.`

Replace with:

```text
... 573 tests, 0 failures. Remaining P4 candidates: diagnostic targets.
```

(Test count drops from 578 to 573; CompileSolution removed from the candidate list.)

Also update the prior clause that lists S07 retirements to mention S08:

Find: `\`CleanArtifacts\` retired from Cake (local hygiene owned by \`tools.cs\`).`

Replace with: `\`CleanArtifacts\` and \`CompileSolution\` retired from Cake (CleanArtifacts: local hygiene owned by \`tools.cs\`; CompileSolution: zero callers, bare \`dotnet build\` covers the use case).`

- [ ] **Step 2: Line ~75 — update Phase X — Build-Host Modernization paragraph**

Find: `\`CleanArtifacts\` retired from Cake; \`testing-guidelines.md\` extracted.`

Replace with: `\`CleanArtifacts\` and \`CompileSolution\` retired from Cake; \`testing-guidelines.md\` extracted.`

- [ ] **Step 3: Visual sanity check**

```pwsh
Get-Content docs/plan.md | Select-String -Pattern "CompileSolution"
# Expect: two hits — one in line ~17 (new "retired" sentence), one in line ~75 (new "retired" mention)
# No "Remaining P4 candidates: CompileSolution..." hit anywhere
Get-Content docs/plan.md | Select-String -Pattern "578 tests"
# Expect: zero hits (replaced with 573)
```

---

## Task 6: Run the verification gate

**Files:** No edits. Read-only verification.

The slice ships only when all four checks pass.

- [ ] **Step 1: Build host compiles cleanly**

```pwsh
dotnet build build/_build/Build.csproj
```

Expected output (last lines):

```text
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

- [ ] **Step 2: Build-host test suite passes**

```pwsh
dotnet test --project build/_build.Tests/Build.Tests.csproj -c Release --framework net10.0
```

Expected: every test passes. Total count should be **573** (down from 578: 4 ctor null-guard tests + 1 smoke test method removed).

If the count is anything other than 573, investigate:
- 574+ → a test was missed in Task 3 deletion or the smoke-test edit
- 572 or fewer → a test outside the planned scope was inadvertently affected

- [ ] **Step 3: Cake target tree no longer lists CompileSolution**

```pwsh
dotnet run --file tools.cs -- build --tree
```

Expected: target tree output that does NOT contain `CompileSolution`. Quick filter:

```pwsh
dotnet run --file tools.cs -- build --tree 2>&1 | Select-String -Pattern "CompileSolution"
# Expect: no matches
```

- [ ] **Step 4: Slopwatch baseline gate passes**

```pwsh
slopwatch analyze --fail-on warning --exclude "artifacts/**,external/**,vcpkg_installed/**,**/bin/**,**/obj/**" --baseline .slopwatch\baseline.json
```

Expected: `0 issue(s) found.`

If slopwatch reports stale baseline entries pointing to `Features/Maintenance/*` or `CompileSolution*` (because they no longer exist), regenerate the baseline:

```pwsh
slopwatch analyze --fail-on warning --exclude "artifacts/**,external/**,vcpkg_installed/**,**/bin/**,**/obj/**" --create-baseline
```

Then re-run the analyze command above to confirm `0 issue(s) found.`

If slopwatch reports new violations (entries not in baseline), STOP — investigate before proceeding. Do not regenerate baseline to mask new violations.

---

## Task 7: Approval gate, commit summary, and commit

**Files:**
- Maybe delete: `docs/refactoring/2026-05-07-compilesolution-retirement-design.md` (Deniz confirms)
- Maybe delete: `docs/refactoring/2026-05-07-compilesolution-retirement-plan.md` (this file — Deniz confirms)

Per AGENTS.md "Before Any Commit: Present a summary of changes and a proposed commit message; ask for approval first."

- [ ] **Step 1: Confirm all working-tree changes**

```pwsh
git status --short
```

Expected lines (order may vary):

```text
 M build/_build/Program.cs
 M build/_build.Tests/Unit/CompositionRoot/ServiceCollectionExtensionsSmokeTests.cs
 D build/_build/Features/Maintenance/CompileSolutionPipeline.cs
 D build/_build/Features/Maintenance/CompileSolutionTask.cs
 D build/_build/Features/Maintenance/ServiceCollectionExtensions.cs
 D build/_build.Tests/Unit/Features/Maintenance/CompileSolutionPipelineTests.cs
 M docs/plan.md
 M docs/refactoring/target-centric-build-host-refactor-plan.md
?? docs/refactoring/2026-05-07-compilesolution-retirement-design.md
?? docs/refactoring/2026-05-07-compilesolution-retirement-plan.md
```

If anything outside this list appears, investigate before proceeding.

- [ ] **Step 2: Ask Deniz about temp doc deletion**

Per the user's auto-memory: "Refactoring doc lifecycle — `docs/refactoring/` mixes durable plan/ADR/checklist with temp per-slice design specs that delete after the slice ships."

Two interpretations:

| Path | What happens to spec + plan files |
| --- | --- |
| A | Both deleted (never committed). Slice commit only contains code/test/canonical-doc changes. |
| B | Both committed in this slice and removed in a follow-up. |
| C | Both committed in this slice and removed in the same commit (added then deleted — confusing in `git log` but technically fine). |

Recommend **A** — the temp docs stay as working-tree scratch and never enter history. Cleanest. Ask Deniz which they prefer before staging.

- [ ] **Step 3: Walk the review checklist**

Open `docs/refactoring/target-centric-build-host-review-checklist.md` and confirm relevant items pass:

- §1 Scope — `CompileSolution` named explicitly; `tools.cs` / `release.yml` / `.github/actions/` checked (all zero); `tools.cs` not modified.
- §11 Retired abstractions — no new `*Pipeline` introduced; `Host/Configuration` usage unchanged for migrated code (DotNetBuildConfiguration retains its other consumers); strategy/coverage/architecture-tests untouched; FrostingLifetime untouched.
- §12 Documentation and validation — refactor plan + AGENTS.md / plan.md updates align with the change; verification command was run; commit summary will be presented.

The other sections (§2–§10, §13–§14) are largely no-ops for a pure-deletion slice but verify nothing was accidentally extended (no V1 fixture additions, no shim extensions, no architecture-police tests added).

- [ ] **Step 4: Present commit summary and proposed commit message to Deniz**

Working-tree changes summary:

- Production: `Features/Maintenance/` deleted (3 files + folder); `Program.cs` composition root cleaned of `AddMaintenanceFeature` registration and import.
- Tests: `Unit/Features/Maintenance/` deleted (1 file + folder); `ServiceCollectionExtensionsSmokeTests` cleaned of the Maintenance smoke method and import.
- Docs: refactor plan §4 inventory (drift-corrects `CleanArtifacts` row + drops `CompileSolution` row); refactor plan §11 P4 completed list (item 7), remaining order (flattens to diagnostics-only), and exit criteria (added bullet); `docs/plan.md` Phase X status (line 17) and Build-Host Modernization paragraph (line 75).

Test delta: 578 → 573 (−5).

Proposed commit message:

```text
refactor: retire CompileSolution from Cake

Zero callers (release.yml, tools.cs, .github/actions, no
[IsDependentOn] references). Mirrors the CleanArtifacts retirement
in commit 7179aaf. Bare `dotnet build Janset.SDL2.sln` replaces it.

- delete Features/Maintenance/{CompileSolutionTask, CompileSolutionPipeline, ServiceCollectionExtensions}.cs
- delete Features/Maintenance/ folder (last resident gone)
- remove .AddMaintenanceFeature() and using from Program.cs
- delete Unit/Features/Maintenance/CompileSolutionPipelineTests.cs
- remove AddMaintenanceFeature smoke method from ServiceCollectionExtensionsSmokeTests
- drift-correct refactor plan §4 inventory (drop CleanArtifacts + CompileSolution rows)
- update refactor plan §11 P4 (completed list, remaining order, exit criteria)
- update docs/plan.md Phase X status and Build-Host Modernization paragraph

Test count: 578 → 573.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
```

Wait for Deniz's `go / apply / proceed / başla / yap` before staging or committing. If Deniz requests message edits, apply them and re-present.

- [ ] **Step 5: Stage the changes Deniz approved**

If Deniz chose option A (temp docs not committed), explicitly omit them:

```pwsh
git add build/_build/Program.cs `
        build/_build.Tests/Unit/CompositionRoot/ServiceCollectionExtensionsSmokeTests.cs `
        build/_build/Features/Maintenance/CompileSolutionPipeline.cs `
        build/_build/Features/Maintenance/CompileSolutionTask.cs `
        build/_build/Features/Maintenance/ServiceCollectionExtensions.cs `
        build/_build.Tests/Unit/Features/Maintenance/CompileSolutionPipelineTests.cs `
        docs/plan.md `
        docs/refactoring/target-centric-build-host-refactor-plan.md
git status --short
# Expect: 8 staged paths; the two ?? temp design/plan files remain unstaged.
```

If Deniz chose option B or C, adjust the staging accordingly.

- [ ] **Step 6: Commit using HEREDOC for clean formatting**

Per AGENTS.md: never `--amend` (a NEW commit always); never `--no-verify`; always include the `Co-Authored-By` trailer.

```bash
git commit -m "$(cat <<'EOF'
refactor: retire CompileSolution from Cake

Zero callers (release.yml, tools.cs, .github/actions, no
[IsDependentOn] references). Mirrors the CleanArtifacts retirement
in commit 7179aaf. Bare `dotnet build Janset.SDL2.sln` replaces it.

- delete Features/Maintenance/{CompileSolutionTask, CompileSolutionPipeline, ServiceCollectionExtensions}.cs
- delete Features/Maintenance/ folder (last resident gone)
- remove .AddMaintenanceFeature() and using from Program.cs
- delete Unit/Features/Maintenance/CompileSolutionPipelineTests.cs
- remove AddMaintenanceFeature smoke method from ServiceCollectionExtensionsSmokeTests
- drift-correct refactor plan §4 inventory (drop CleanArtifacts + CompileSolution rows)
- update refactor plan §11 P4 (completed list, remaining order, exit criteria)
- update docs/plan.md Phase X status and Build-Host Modernization paragraph

Test count: 578 → 573.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

- [ ] **Step 7: Verify the commit landed cleanly**

```pwsh
git log -1 --oneline
# Expect: <hash> refactor: retire CompileSolution from Cake
git status --short
# Expect: clean (no staged or unstaged tracked changes); only the two ?? temp doc files remain if option A
```

- [ ] **Step 8: Clean up temp docs (if option A and Deniz confirms)**

```pwsh
Remove-Item docs/refactoring/2026-05-07-compilesolution-retirement-design.md
Remove-Item docs/refactoring/2026-05-07-compilesolution-retirement-plan.md
git status --short
# Expect: clean working tree
```

If option B was chosen, the cleanup happens in a follow-up commit (out of scope for this slice).

- [ ] **Step 9: Final witness**

```pwsh
git log -3 --oneline
# Expect: top of log shows the new retirement commit followed by 7179aaf and prior history
```

Slice S08 ships when this output looks right. Do NOT push to remote unless Deniz explicitly asks.

---

## Self-review checklist (writer-side, completed before handoff)

- [x] **Spec coverage:** Every section of `2026-05-07-compilesolution-retirement-design.md` has a Task. §1 Context → motivates Task 1–3. §2 Caller audit → motivates the slice itself. §3 Decision → encoded in Task 1–7. §4.1 Production deletions → Task 1–2. §4.2 Test deletions → Task 3. §4.3 Documentation updates → Task 4–5. §4.4 Out-of-scope → respected throughout. §5 Verification gate → Task 6. §6 Approval gate → Task 7. §7 Forward-looking impact → reflected in §11 doc updates. §8 References → preserved.

- [x] **Placeholder scan:** No "TBD" / "TODO" / "implement later". Every code/edit step shows the exact text or filenames. Every command shows expected output.

- [x] **Type/identifier consistency:** `AddMaintenanceFeature` spelled identically across Tasks 2, 3, 7 and the commit message. `CompileSolutionPipeline` / `CompileSolutionTask` spelled identically. Test count `578 → 573` consistent across the spec, plan, and commit message.

- [x] **No new tests, no new code claim honored:** Tasks 1–5 are pure deletion + doc updates. No new test files, no new production code. The smoke-test method removal is the only test edit; no test logic added.

- [x] **AGENTS.md alignment:** Approval gate at Task 7 Step 4. HEREDOC commit message format. `Co-Authored-By` trailer. No `--amend`, no `--no-verify`, no `--force`. Master-direct commit explicitly noted as acceptable per S07 precedent. Slopwatch in verification gate.

- [x] **Atomic single-commit slice:** Tasks 1–6 accumulate working-tree changes without committing; Task 7 commits once after Deniz approval. Mirrors S07 (`7179aaf`) shape.
