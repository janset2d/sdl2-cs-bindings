# Versioning Simplification — May 4, 2026

> **Status:** Plan v3 — principle-driven, options split (revised 2026-05-04, not yet implemented)
> **Session:** Targeted refactoring — remove git-tag versioning, remove MinVer entirely, lock the version-resolution boundary on a single principle, split CLI options by consumer

---

## 0. Foundational principle (the simplification spine)

All other phases descend from one rule:

> **Stage tasks (PreFlight, Package, PackageConsumerSmoke, PublishStaging) read versions from `--versions-file` only. `--explicit-version` and `--explicit-versions` are ResolveVersions inputs only.**

Operationally:

| Configuration record | Populated from | Consumed by |
| --- | --- | --- |
| `VersioningConfiguration.ExplicitVersions` (NEW property) | `--explicit-version` (repeated) ∪ `--explicit-versions` (comma-sep) | `ResolveVersionsPipeline` only, when `--version-source=explicit`. |
| `VersioningConfiguration.{VersionSource, Suffix, Scope}` | `--version-source`, `--suffix`, `--scope` (unchanged) | `ResolveVersionsPipeline` |
| `PackageBuildConfiguration.FamilyVersionMapping` (renamed from `ExplicitVersions`) | `--versions-file` only | Stage targets: PreFlight, Package, PackageConsumerSmoke, PublishStaging. |

**Mutual exclusivity is between `--explicit-version` and `--explicit-versions`** (operator chooses the input shape). `--versions-file` is independent — it feeds a different pipeline stage. The current `--versions-file` ↔ `--explicit-version` mutex check in `Program.cs` is conceptually wrong and is removed.

**Stage tasks fail loud at task entry, not soft-skip.** When a stage target is invoked without `--versions-file` (and therefore `FamilyVersionMapping` is empty), the task throws `CakeException` with an actionable message pointing at `--target ResolveVersions` or the CI artifact-flow. The current `ShouldRun() → false` soft-skip pattern in `PackageTask` / `PackageConsumerSmokeTask` / `PublishStagingTask` is removed. Rationale: the misuse is structural, not configurational; the pipeline must refuse to run rather than emit a green log line that hides the problem. Hard-fail at task entry was preferred over making `--versions-file` mandatory at CLI binding time because the same root command also drives `ResolveVersions` (which writes versions.json, never reads one) — a global mandatory option would block the very target that produces the file.

**Scope is encoded in versions.json contents.** The mapping's key set defines pack/preflight/smoke/publish scope. Operators select families upstream by passing `--scope` to ResolveVersions (manifest mode) or by listing only the desired families in `--explicit-version*` (explicit mode). Stages neither filter nor expand the mapping they receive. Single-family vs full-set selection capability is unchanged — the encoding moves from CLI flags at the stage to JSON contents.

**Operator local-dev ergonomics: 2-command flow is the only supported direct path.** The previously-documented `--target Package --explicit-version foo=...` shortcut is removed (it was the only stage-target operator shortcut that survived earlier refactors). Replacement, documented in `CLAUDE.md` and `docs/playbook/local-development.md`:

```pwsh
dotnet run --project build/_build -- --target ResolveVersions --version-source=explicit --explicit-version sdl2-core=2.32.0-local.1
dotnet run --project build/_build -- --target Package --versions-file artifacts/resolve-versions/versions.json
```

`tools.cs ci-sim` already follows this shape. A future ergonomic wrapper (e.g. `tools pack --explicit-version …`) is **out of scope** for this plan; revisit in a follow-up if the 2-command friction proves real.

**MinVer is eradicated, not retired-in-place.** Every reference — `<PackageReference>`, `<PackageVersion>`, `<MinVerTagPrefix>` properties, `MinVerSkip` MSBuild flags, `FamilyIdentifierConventions.MinVerTagPrefix()`, `CsprojPackContractCheckKind.MinVerTagPrefixMatchesManifest`, the G4 guardrail row, ADR-001 §2.3, every `docs/` mention — disappears in this change. Partial MinVer presence is worse than none. The only acceptable surviving artefacts are archived dated snapshots (`docs/_archive/*`, `docs/research/*-2026-04-*.md`) which are intentionally frozen historical record.

### 0.1 Cross-cutting principle: each task validates its own options

> **Going forward, repo-wide rule (not just versioning): every Cake task is responsible for validating the configuration / options it consumes. Configuration records and CLI options remain nullable-by-default; the producing CLI binding does not enforce per-target requiredness.**

Why this is the only viable seam:

- The CLI shape is a flat root command; Cake.Frosting reads `--target X` and dispatches on its side. System.CommandLine has no knowledge of which Cake target will run — it sees one global option set. Setting `Option<T>.IsRequired = true` would block ALL targets from invoking, including ones that don't consume the option (e.g., `--versions-file IsRequired = true` would also block `--target ResolveVersions`, which is the producer of versions.json).
- DI / configuration construction happens once at startup, before Cake selects a target. The same `BuildContext` and config records are shared across every target the invocation runs. Therefore option/config validation cannot be globally enforced at the DI boundary either.
- The first moment we know which target is running and which configuration subset matters is **inside the task's own `RunAsync` (or `ShouldRun`)**. That is the single correct enforcement seam.

What this means in code:

- Stage tasks that need `FamilyVersionMapping` throw `CakeException` at `RunAsync` entry with an actionable message when the mapping is empty. No silent skip.
- ResolveVersions throws when its required axes (`--version-source`; `--suffix` for manifest mode; `ExplicitVersions` non-empty for explicit mode) are missing. Already partially in place.
- New tasks added to the codebase follow the same pattern. Option/config records stay nullable; the task is the validator.

A centralized Cake `BuildLifetime`-based validator was considered and rejected: it would centralize the rule list at the cost of making each task no longer self-describing about its preconditions. The 4-line guard in each stage task is preferred over the indirection.

### 0.2 CLI option organization mirrors the consumer boundary

Option classes are split so their **file names** state who consumes them:

| Option class | File | Consumed by |
| --- | --- | --- |
| `ResolveVersionsOptions` (renamed from `VersioningOptions`) | `Host/Cli/Options/ResolveVersionsOptions.cs` | `ResolveVersionsPipeline` only |
| `StageVersionsOptions` (new) | `Host/Cli/Options/StageVersionsOptions.cs` | Stage tasks (Package, PreFlight, ConsumerSmoke, PublishStaging) |

`ResolveVersionsOptions` holds: `VersionSourceOption`, `VersionSuffixOption`, `VersionScopeOption`, `ExplicitVersionOption`, `ExplicitVersionsOption` (new). `StageVersionsOptions` holds: `VersionsFileOption`. The split does **not** add CLI-level enforcement (per §0.1 it cannot) — it adds **structural clarity**: a code review catching `ExplicitVersionOption` in the wrong file is one bad commit; the help text scope markers (Phase 2.3.1) reinforce the same boundary at the user-facing surface. New stage-input flags go to `StageVersionsOptions`; new ResolveVersions axes go to `ResolveVersionsOptions`.

---

## 1. Why

### 1.1 Git-tag versioning doesn't work

The repo built a three-provider version resolution system (`ManifestVersionProvider`, `ExplicitVersionProvider`, `GitTagVersionProvider`) with two tag-based release shapes (targeted `sdl2-core-2.32.0` and meta-tag `train-*`). In practice:

- **GitHub Actions triggers one workflow run per pushed tag.** A train release needing N family tags + 1 meta-tag produces N+1 runs — only the `train-*` run is the actual release; the N family-tag-triggered runs are noise that functionally fail.
- **The trigger mechanism is under reconsideration** per `docs/knowledge-base/release-lifecycle-direction.md`.
- **Real releases will use `workflow_dispatch` with explicit versions**, not git tags. PD-7 (public NuGet publish) is operator-driven.

**Decision:** Drop `GitTagVersionProvider`, `GitTagScope`, `Cake.Frosting.Git`'s tag-discovery use-cases (the package itself stays for `GitLogTip` HEAD-SHA stamping in `PackagePipeline`), and all tag-trigger infrastructure. Keep only `--version-source=manifest` (CI/local dev) and `--version-source=explicit` (operator release).

### 1.2 Configuration boundary confusion

Two configuration objects have today blurred responsibilities:

| Config object | What it holds today | Who reads it today |
| --- | --- | --- |
| `PackageBuildConfiguration.ExplicitVersions` | Family→version mapping from `--explicit-version` or `--versions-file` | `ResolveVersionsPipeline` (explicit mode) AND `PackageTask` / `PackageConsumerSmokeTask` / `PreFlightCheckTask` / `PublishStagingTask` |
| `VersioningConfiguration` | `--version-source`, `--suffix`, `--scope` | `ResolveVersionsPipeline` only |

This is the boundary the foundational principle re-cuts cleanly. The `Program.cs` mutual-exclusivity check between `--versions-file` and `--explicit-version` is a symptom of the same confusion — it treated stage-target input and ResolveVersions input as competing for the same role.

### 1.3 release.yml bash script

The `resolve-versions` job parses comma-separated `EXPLICIT_VERSIONS` env var with a 15-line bash script (IFS split, whitespace trim, `--explicit-version` arg building). This parsing belongs in Cake (C#), not in YAML bash. The plan introduces a `--explicit-versions` (plural) CLI option accepting a single comma-separated string; release.yml passes `inputs.explicit-versions` directly with no bash interpolation.

### 1.4 MinVer is dead weight

MinVer was added for git-tag-driven automatic versioning. With:

- `dotnet pack -p:Version=<explicit>` always supplied by `PackageTask` (via Cake `DotNetPackInvocation.Version`),
- `--version-source=git-tag` and `--version-source=meta-tag` removed,
- G54 (upstream version alignment) enforced in `ExplicitVersionProvider` and `PreflightPipeline` independent of MinVer,

MinVer's `<MinVerTagPrefix>` property on 10 csproj files, the `<PackageReference>` block in `src/Directory.Build.props`, the G4 guardrail (`MinVerTagPrefixMatchesManifest`), the `MinVerSkip=true` MSBuild flag in `DotNetPackInvoker.BuildMSBuildSettings`, `FamilyIdentifierConventions.MinVerTagPrefix()`, and ~16 `docs/` references all carry zero current value. Every one of them is removed.

### 1.5 `on: push: tags:` trigger

With git-tag versioning removed, the release workflow's tag-push trigger (`sdl2-*-*.*.*`, `sdl3-*-*.*.*`, `train-*`) serves no purpose. All releases are `workflow_dispatch`-driven. The `publish-staging` job's `if:` condition collapses to the workflow_dispatch branch.

---

## 2. What

### 2.1 Summary of changes

| Category | Removed | Added | Modified |
| --- | --- | --- | --- |
| Production `.cs` files | 3 files (~350 lines): `GitTagVersionProvider.cs`, `GitTagScope.cs`, `FamilyIdentifierConventions.MinVerTagPrefix()` | `ExplicitVersionParser.ParseCommaSeparated`, `VersioningConfiguration.ExplicitVersions` property | 7 files (~140 lines): `ResolveVersionsPipeline`, `PackageBuildConfiguration`, `Program.cs`, `VersioningOptions`, `PackageTask`, `PackageConsumerSmokeTask`, `PreFlightCheckTask`, `PublishStagingTask`, `Versioning/ServiceCollectionExtensions`, `CsprojPackContractValidator`, `CsprojPackContractModels`, `ICsprojPackContractValidator`, `DotNetPackInvoker`, `PreflightReporter` |
| Test `.cs` files | 2 files (`GitTagVersionProviderTests`, `TempGitRepo`) + 2 methods in `ResolveVersionsPipelineTests` + 1 method + helpers in `CsprojPackContractValidatorTests` | `ParseCommaSeparated_*` tests, fail-loud assertions for stage tasks | 4 files (rename/new fixtures) |
| `.csproj` / `.props` | 10× `<MinVerTagPrefix>`, `<PackageReference Include="MinVer">` block, `<MinVerDefaultPreReleaseIdentifiers>`, `<MinVerMinimumMajorMinor>` (and possibly the entire `src/Directory.Build.props` if it collapses to an Import-only file) | — | — |
| `release.yml` | `on: push: tags:`, git-tag/meta-tag steps, bash-parser script, publish-staging tag-push branch | `--explicit-versions` usage | `resolve-versions` job simplified |
| `Directory.Packages.props` | `MinVer` `PackageVersion` entry | — | `Cake.Frosting.Git` comment re-anchored to surviving G55 use |
| Docs | "Amendment" archeology (none accumulates) | One in-place rewrite per affected doc | ADR-001, release-guardrails, release-lifecycle-direction, cake-build-architecture, plan, onboarding, 4× playbooks, 2× phase docs, CLAUDE.md |

### 2.2 What stays

| Component | Why |
| --- | --- |
| `--scope` CLI option | Filters families for `ResolveVersions --version-source=manifest` |
| `--versions-file` CLI option | Stage-target input contract |
| `Features/Packaging/FamilyTopologyHelpers` | Used by `PackagePipeline` for topological pack ordering; not git-tag-specific |
| `Cake.Frosting.Git` NuGet package | Surviving consumer: `PackagePipeline.DefaultResolveHeadCommitSha` uses `GitLogTip` for G55 metadata stamping. (LibGit2Sharp x64-only constraint persists; not addressed by this plan.) |
| `IUpstreamVersionAlignmentValidator` (G54) | Enforced inside `ExplicitVersionProvider` and `PreflightPipeline`; independent of any version-source provider |
| `IPackageVersionProvider` interface | Two production implementations remain: `ManifestVersionProvider`, `ExplicitVersionProvider` |
| `ManifestVersionProvider` | Produces `<UpstreamMajor>.<UpstreamMinor>.0-<suffix>` versions for `ResolveVersions --version-source=manifest` |
| `ExplicitVersionProvider` | Validates operator-supplied versions against G54; consumed by both ResolveVersions (explicit mode) and stage-target DI (reading `FamilyVersionMapping`) |
| `ExplicitVersionParser.ParseCliEntries(IEnumerable<string>)` | Existing repeated-entry parser; extended (not replaced) by `ParseCommaSeparated` |
| `tools.cs` | Already uses only `--version-source=manifest` and the 2-command resolve→stage flow; no changes needed |

---

## 3. How (data flow under the principle)

```text
┌──────────────────────────────────────────────────────────────────────────┐
│                       Program.cs (composition root)                       │
│                                                                          │
│  --version-source ───────────┐                                           │
│  --suffix ───────────────────┤                                           │
│  --scope ────────────────────┤──→ VersioningConfiguration                │
│  --explicit-version ─────────┤      .VersionSource                       │
│  --explicit-versions ────────┘      .Suffix / .Scope                     │
│  ┌─ mutex: --explicit-version  XOR --explicit-versions ─┐                │
│                                     .ExplicitVersions                    │
│                                                                          │
│  --versions-file ──────────────→ PackageBuildConfiguration               │
│                                     .FamilyVersionMapping                │
└────────────────────────┬─────────────────────────────────────────────────┘
                         │
              ┌──────────┼──────────────────────────────┐
              ▼          ▼                              ▼
      ┌──────────────┐  ┌──────────────────┐  ┌──────────────────────────┐
      │ ResolveVersions│ │ PreFlight/Package /│ │ Stage tasks fail loud at  │
      │  Pipeline     │ │ ConsumerSmoke /  │ │ task entry when            │
      │               │ │ PublishStaging   │ │ FamilyVersionMapping is    │
      │  reads:       │ │                  │ │ empty (no soft-skip).      │
      │  Versioning-  │ │  reads:          │ │                            │
      │  Configuration│ │  PackageBuild-   │ │ Actionable message points  │
      │               │ │  Configuration   │ │ at --target ResolveVersions│
      │  writes:      │ │  .FamilyVersion- │ │ for direct invocation, or  │
      │  versions.json│ │  Mapping         │ │ the CI artifact for jobs.  │
      └──────────────┘  └──────────────────┘  └──────────────────────────┘
```

### 3.1 Key design decisions

1. **`--explicit-version` (repeated) and `--explicit-versions` (comma-sep) are mutually exclusive.** Both feed `VersioningConfiguration.ExplicitVersions`. The comma-sep form exists solely to eliminate bash parsing in release.yml; both shapes are otherwise equivalent.
2. **`--versions-file` is independent of `--explicit-version*`.** Different pipeline stages, different configurations. No mutex.
3. **Stage targets never see `--explicit-version*` directly.** Operator input flows through ResolveVersions → versions.json → stage targets (or, for direct dev runs, the operator hand-writes a versions.json, which is rare enough not to optimize for).
4. **`ExplicitVersionParser.ParseCommaSeparated(string?)`** splits on `,`, trims whitespace from each segment, skips empties, delegates to `ParseCliEntries`. Null/whitespace input returns empty mapping (mirrors `ParseCliEntries([])`).
5. **No CLI-mandatory `--versions-file`.** The same root command serves `ResolveVersions` (which writes versions.json, never reads one). Mandatory-at-CLI would block the producer of the file. Stage-task-level fail-loud is the right enforcement seam.

---

## 4. Implementation plan

### Phase 1 — `ExplicitVersionParser` expansion (bottom-up, no dependencies)

| Step | File | Action |
| --- | --- | --- |
| 1.1 | `Features/Versioning/ExplicitVersionParser.cs` | Add `ParseCommaSeparated(string? commaSeparated)`: split on `,`, trim each, skip empties, delegate to `ParseCliEntries`. Null/whitespace input → empty mapping. |

### Phase 2 — Configuration records update

| Step | File | Action |
| --- | --- | --- |
| 2.1 | `Host/Configuration/VersioningConfiguration.cs` | Add 4th ctor param `IReadOnlyDictionary<string, NuGetVersion>? explicitVersions`, add `ExplicitVersions` property. Doc comment: principle-aware (sole consumer is `ResolveVersionsPipeline`). Drop `git-tag` / `meta-tag` from `--version-source` allowed-values list. |
| 2.2 | `Host/Configuration/PackageBuildConfiguration.cs` | Rename property `ExplicitVersions` → `FamilyVersionMapping`. **Rewrite** XML doc comment in place (no archeology). New shape: "Resolved family→version mapping consumed by stage targets (PreFlight, Package, PackageConsumerSmoke, PublishStaging). Populated only from `--versions-file`." |
| 2.3a | `Host/Cli/Options/VersioningOptions.cs` → `Host/Cli/Options/ResolveVersionsOptions.cs` | **Rename file + class** to `ResolveVersionsOptions`. Keeps: `VersionSourceOption`, `VersionSuffixOption`, `VersionScopeOption`, `ExplicitVersionOption`. **Removes:** `VersionsFileOption` (moves to `StageVersionsOptions` per 2.3b). **Adds:** `ExplicitVersionsOption` (`--explicit-versions`, single comma-separated string). Class-level XML doc rewritten to reflect §0.2 scope: "ResolveVersions-only options. Stage targets read `Host/Cli/Options/StageVersionsOptions.cs`." |
| 2.3b | `Host/Cli/Options/StageVersionsOptions.cs` (new file) | Create with `VersionsFileOption` declaration. Class-level XML doc: "Stage-target version input options (PreFlight, Package, PackageConsumerSmoke, PublishStaging). ResolveVersions-only options live in `ResolveVersionsOptions.cs`." |
| 2.3c | both `ResolveVersionsOptions.cs` + `StageVersionsOptions.cs` | All five option descriptions rewritten per §0.1/§0.2 principle (see 2.3.1 below). |
| 2.4 | `Program.cs` `ParsedArguments` record | Append `string? ExplicitVersions` at the **end** of the positional record. Existing callsites (notably `ProgramCompositionRootTests.CreateParsedArguments`) grow by one trailing `null` arg. |

**2.3.1 — option description rewrites (per §0.1 / §0.2):**

- `--explicit-version` (in `ResolveVersionsOptions`): append "Consumed only by `--target ResolveVersions`. Stage targets read `--versions-file`."
- `--explicit-versions` (in `ResolveVersionsOptions`, NEW): same scope marker as `--explicit-version`.
- `--version-source` (in `ResolveVersionsOptions`): drop `git-tag` / `meta-tag`; allowed values become `manifest | explicit`.
- `--versions-file` (in `StageVersionsOptions`): rewrite description to "Stage-target input. Path to flat `{family: semver}` JSON. ResolveVersions does not consume this flag." (no "merged with" language)
- `--scope` (in `ResolveVersionsOptions`): clarify it filters ResolveVersions output only.

### Phase 3 — Composition root rewiring

| Step | File | Action |
| --- | --- | --- |
| 3.1 | `Program.cs` `ConfigureBuildServices` | Replace the existing `--versions-file`/`--explicit-version` mutex with `--explicit-version`/`--explicit-versions` mutex. Build `VersioningConfiguration` with the explicit mapping (preferring plural `ExplicitVersions` when supplied; otherwise `ExplicitVersion` repeated; otherwise empty). Build `PackageBuildConfiguration` only from `--versions-file` (empty when not supplied — stages will fail-loud at task entry). |
| 3.2 | `Program.cs` root option registration | Update `using` to reflect new option-class names. Register the five `ResolveVersionsOptions.*` options + the single `StageVersionsOptions.VersionsFileOption`. Order so the principle reads top-to-bottom: ResolveVersions options block first, stage options block second. |

### Phase 4 — `ResolveVersions` pipeline update

| Step | File | Action |
| --- | --- | --- |
| 4.1 | `Features/Versioning/ResolveVersionsPipeline.cs` | Remove `PackageBuildConfiguration` ctor param. Read explicit versions from `VersioningConfiguration.ExplicitVersions`. Remove `ResolveFromGitTagAsync`, `ResolveFromMetaTagAsync`, `EmptyRequestedScope` field, and the orphaned scope-narrowing comment block at the call site. Strip every `git-tag` / `meta-tag` mention from error messages and the class XML doc. |
| 4.2 | `Features/Versioning/ResolveVersionsTask.cs` | No change (already pipeline-only ctor). |

### Phase 5 — Stage consumers: rename + fail-loud

| Step | File | Action |
| --- | --- | --- |
| 5.1 | `Features/Packaging/PackageTask.cs` | Replace `_packageBuildConfiguration.ExplicitVersions` reads with `.FamilyVersionMapping`. **Replace `ShouldRun() → false` soft-skip with hard-fail at `RunAsync` entry**: throw `CakeException` when `FamilyVersionMapping.Count == 0`. Message: "PackageTask requires `--versions-file <path>`. Run `--target ResolveVersions` first to produce a versions.json (e.g. `--target ResolveVersions --version-source=manifest --suffix=local.<timestamp>`), then re-run with `--versions-file artifacts/resolve-versions/versions.json`." Drop `ICakeLog` ctor injection if it was used only by the removed soft-skip log. |
| 5.2 | `Features/Packaging/PackageConsumerSmokeTask.cs` | Same rename + fail-loud + log-injection cleanup. |
| 5.3 | `Features/Preflight/PreFlightCheckTask.cs` | Rename. Add explicit empty-mapping fail-loud guard at `RunAsync` entry for symmetry (it currently builds a request unconditionally, which would surface as a less-actionable error downstream). |
| 5.4 | `Features/Publishing/PublishStagingTask.cs` | **Newly added to plan** (was missed in v1). Rename + fail-loud. Replace the existing `--explicit-version` mention in the soft-skip log with the canonical `--versions-file` direction. |
| 5.5 | `Features/Versioning/ServiceCollectionExtensions.cs` | `IPackageVersionProvider` registration reads `FamilyVersionMapping`. Comment rewritten in place: "ResolveVersions handles every release shape upstream of stages: manifest+suffix and explicit dispatch. Downstream stages consume the resolved mapping via `--versions-file`." (no `git-tag` / `meta-tag` / `targeted family-tag push` / `meta-tag train push` archeology) |
| 5.6 | `Features/Packaging/PackRequest.cs` | Field/parameter rename if needed (current shape is `IReadOnlyDictionary<string, NuGetVersion> Versions` — likely no change beyond inbound reference site updates). |

### Phase 6 — Git-tag removal

| Step | File | Action |
| --- | --- | --- |
| 6.1 | `Features/Versioning/GitTagVersionProvider.cs` | **DELETE** (~260 lines). |
| 6.2 | `Features/Versioning/GitTagScope.cs` | **DELETE** (~30 lines). |
| 6.3 | `Features/Versioning/ResolveVersionsPipeline.cs` | Verification step (already covered by Phase 4.1): `EmptyRequestedScope` field removed, scope-narrowing comment block removed, no orphaned references. |
| 6.4 | `Host/Configuration/VersioningConfiguration.cs` | XML doc comment scrub: drop `git-tag`, `meta-tag` from version-source enumeration. (Already covered by Phase 2.1.) |
| 6.5 | `Features/Versioning/ServiceCollectionExtensions.cs` | Comment scrub. (Already covered by Phase 5.5.) |
| 6.6 | `Directory.Packages.props` | Re-anchor `Cake.Frosting.Git` comment to its surviving purpose: G55 HEAD-SHA stamping in `PackagePipeline.DefaultResolveHeadCommitSha`. The package itself stays. Note that LibGit2Sharp is x64-only — the constraint persists for jobs that hit `GitLogTip` (Pack runs on x64 today; arm64 is unaffected). |

### Phase 7 — Test updates

| Step | File | Action |
| --- | --- | --- |
| 7.1 | `Integration/Versioning/GitTagVersionProviderTests.cs` | **DELETE** (~175 lines). |
| 7.2 | `Fixtures/TempGitRepo.cs` | **DELETE** (~100 lines). Verify no other consumer before deletion. |
| 7.3 | `Unit/Features/Versioning/ResolveVersionsPipelineTests.cs` | Remove `RunAsync_Should_Throw_When_GitTag_Source_Has_No_Scope` and `RunAsync_Should_Throw_When_GitTag_Source_Has_Multiple_Scope_Entries`. Update `CreateRunner` helper: drop `PackageBuildConfiguration` param, accept `IReadOnlyDictionary<string, NuGetVersion>? explicitVersions` and pass through to `VersioningConfiguration`. |
| 7.4 | `Unit/Features/Versioning/ExplicitVersionParserTests.cs` | Add tests: `ParseCommaSeparated_Should_Parse_Single_Entry`, `ParseCommaSeparated_Should_Parse_Multiple_Entries`, `ParseCommaSeparated_Should_Trim_Whitespace`, `ParseCommaSeparated_Should_Skip_Empty_Segments`, `ParseCommaSeparated_Should_Throw_On_Invalid_SemVer`, `ParseCommaSeparated_Should_Return_Empty_When_Null_Or_Whitespace`. |
| 7.5 | All tests referencing `PackageBuildConfiguration.ExplicitVersions` | Rename to `FamilyVersionMapping`. Affected so far: `PackageConsumerSmokeTaskTests` and any others surfaced by compile. |
| 7.6 | New fail-loud tests | Add `RunAsync_Should_Throw_When_VersionsFile_Mapping_Is_Empty` (and analogue) for `PackageTaskTests`, `PackageConsumerSmokeTaskTests`, `PreFlightCheckTaskTests`, `PublishStagingTaskTests`. |
| 7.7 | `Unit/CompositionRoot/ProgramCompositionRootTests.CreateParsedArguments` | Append trailing `ExplicitVersions: null` argument. |

### Phase 8 — MinVer total eradication

**Principle: nothing in the repo references MinVer after this phase — code, csproj, lock files, doc-comments, ADRs, knowledge-base, playbooks. No archaeology. Phase 8 is intentionally a single bracket; partial MinVer presence is worse than none.**

Code:

| Step | File | Action |
| --- | --- | --- |
| 8.1 | `Directory.Packages.props` | Remove `<PackageVersion Include="MinVer" Version="7.0.0" />`. |
| 8.2 | `src/Directory.Build.props` | Remove `<MinVerDefaultPreReleaseIdentifiers>`, `<MinVerMinimumMajorMinor>`, `<PackageReference Include="MinVer">` block. If the file collapses to a single `<Import>` element, **delete it entirely** — `src/**/*.csproj` will inherit from the repo-root `Directory.Build.props` directly. |
| 8.3 | `src/SDL2.Core/SDL2.Core.csproj` | Remove `<MinVerTagPrefix>sdl2-core-</MinVerTagPrefix>`. |
| 8.4 | `src/SDL2.Image/SDL2.Image.csproj` | Same pattern. |
| 8.5 | `src/SDL2.Mixer/SDL2.Mixer.csproj` | Same. |
| 8.6 | `src/SDL2.Ttf/SDL2.Ttf.csproj` | Same. |
| 8.7 | `src/SDL2.Gfx/SDL2.Gfx.csproj` | Same. |
| 8.8 | `src/native/SDL2.Core.Native/SDL2.Core.Native.csproj` | Same. |
| 8.9 | `src/native/SDL2.Image.Native/SDL2.Image.Native.csproj` | Same. |
| 8.10 | `src/native/SDL2.Mixer.Native/SDL2.Mixer.Native.csproj` | Same. |
| 8.11 | `src/native/SDL2.Ttf.Native/SDL2.Ttf.Native.csproj` | Same. |
| 8.12 | `src/native/SDL2.Gfx.Native/SDL2.Gfx.Native.csproj` | Same. |
| 8.13 | `Features/Preflight/FamilyIdentifierConventions.cs` | Remove `MinVerTagPrefix(string)` static method. |
| 8.14 | `Features/Preflight/CsprojPackContractValidator.cs` | Remove `CheckMinVerTagPrefix` method. Drop the two `checks.Add(CheckMinVerTagPrefix(...))` invocations (managed-csproj and native-csproj paths). |
| 8.15 | `Features/Preflight/CsprojPackContractModels.cs` | Remove `MinVerTagPrefixMatchesManifest` enum value. Update enum-level XML doc to drop the G1/G2/G3/G5/G8 historical markers (already-retired guardrails) — they cluster with G4 and confuse new readers. |
| 8.16 | `Features/Preflight/ICsprojPackContractValidator.cs` | Class-level XML doc currently lists "G1-G3, G4 (MinVer), G5, G6, G7, G8". Rewrite to list only the surviving guardrails: G6 (PackageId), G7 (Native ProjectReference), G17 (depends_on cross-reference), G18 (library_ref cross-reference). |
| 8.17 | `Integrations/DotNet/DotNetPackInvoker.cs` | Lines 57-63 (the `MinVerSkip=true` MSBuild property setting and surrounding comment): the property is dead (no MinVer in the build), its `Cake has already resolved the family version (MinVer-derived from git tag, or from --family-version override)` comment is wrong on three counts (no MinVer, no git tag, no `--family-version`). Remove the property assignment in `BuildMSBuildSettings` AND the comment block. |
| 8.18 | `Features/Preflight/PreflightReporter.cs` | Line 204: replace "Review the canonical pack-contract rules (G1-G8, G17, G18) for details." with "Review the canonical pack-contract rules (G6, G7, G17, G18) for details." |

Tests:

| Step | File | Action |
| --- | --- | --- |
| 8.19 | `Unit/Features/Preflight/CsprojPackContractValidatorTests.cs` | Multi-step (see 8.19.1 below). |

**8.19.1 — `CsprojPackContractValidatorTests` step-by-step:**

1. **Delete** `Validate_Should_Fail_When_MinVerTagPrefix_Drifts_From_Manifest` test method.
2. From `ManagedCsproj` and `NativeCsproj` helper methods: drop the `tagPrefix` parameter and remove `<MinVerTagPrefix>` lines from the embedded csproj template strings.
3. Update all helper invocations to drop the second argument.
4. From the inline csproj string literals inside test bodies (`Validate_Should_Fail_When_Managed_PackageId_Drifts_From_Canonical_Convention`, `Validate_Should_Fail_When_Native_ProjectReference_Path_Does_Not_Match_Manifest`, `Validate_Should_Fail_When_Native_PackageId_Drifts_From_Canonical_Convention`): drop the `<MinVerTagPrefix>...</MinVerTagPrefix>` lines.

Note: `Unit/Features/Preflight/FamilyIdentifierConventionsTests.cs` has **no** existing MinVer tests (only `ManagedPackageId`, `NativePackageId`, `VersionPropertyName`). No-op for that file.

### Phase 9 — `release.yml` update

| Step | File | Action |
| --- | --- | --- |
| 9.1 | `.github/workflows/release.yml` `on:` | Remove the `push: tags:` block entirely. Keep only `workflow_dispatch`. |
| 9.2 | `resolve-versions` job | Remove "Resolve targeted family tag versions" and "Resolve coordinated train tag versions" steps. Replace the bash-based "Resolve explicit dispatch versions" step with a single line: `--explicit-versions "${{ inputs.explicit-versions }}"`. No bash IFS / trim / arg-build. |
| 9.3 | `publish-staging` job `if:` | Drop the `github.event_name == 'push'` clause. New condition: `if: ${{ github.event_name == 'workflow_dispatch' && inputs.publish-staging == true }}`. |
| 9.4 | Top-of-file flow comment | Update wording — remove "release tags publish to the internal feed after trigger-aware ResolveVersions routing emits tag-derived versions.json". Replace with the workflow_dispatch-only narrative. |

### Phase 10 — Build & verify

| Step | Action |
| --- | --- |
| 10.1 | `dotnet build build/_build/Build.csproj` |
| 10.2 | `dotnet test --project build/_build.Tests/Build.Tests.csproj -c Release --framework net10.0` |
| 10.3 | `dotnet run --file tools.cs -- ci-sim` (full local pipeline rehearsal) |
| 10.4 | Manual review of changed files |

### Phase 11 — Documentation rewrites (in place — no amendments)

**Operating rule for this phase:** rewrite canonical content **in place**. No "Amendment 2026-05-XX" paragraphs, no `~~strikethrough~~` archeology, no preserved-but-superseded sections. Old text is replaced by new text. The only carve-out is when removing context creates ambiguity for the new reader — in that case, write a short replacement paragraph that tells the new story without referencing the old one.

**ADR-001** (`docs/decisions/2026-04-18-versioning-d3seg.md`):

| Step | Section | Action |
| --- | --- | --- |
| 11.1 | §2.3 "MinVer role clarification" | **Rewrite in place** as "Version resolution providers". New content: enumerate the two surviving providers (`ManifestVersionProvider`, `ExplicitVersionProvider`), their inputs (manifest `vcpkg_version` + suffix; operator `--explicit-version*`), their consumers (ResolveVersions exclusively for the explicit provider; ResolveVersions + DI-registered stage seam for the manifest provider via the resolved versions.json), and the foundational principle. |
| 11.2 | §1.3 "Decision precedents reviewed" table | Rows A, B, D-4seg, D-3seg mention MinVer in rationale columns. Rewrite the rationale columns to drop MinVer references and re-anchor on the underlying deciding factors (NuGet 4-segment surprise, vcpkg patch-bump cascade, etc.). |
| 11.3 | §2.1 "Versioning (D-3seg)" | Drop the "Keeps MinVer compatible" clause; SemVer-2.0 / NuGet rationale stands on its own. |
| 11.4 | §3.2 "Why D-3seg over B" | "MinVer-native" bullet → rewrite as "no 4-part SemVer for NuGet consumers". |
| 11.5 | §5 "Non-goals" | Delete the bullet "Reopen MinVer as the tag-derivation tool choice. It stays." |
| 11.6 | §8.3 External references | Remove the MinVer documentation link. |
| 11.7 | §6 Precedent survey table | LibGit2Sharp row "4-segment approx-upstream-tracked" — rewrite the relevance column without the implicit MinVer reference. |

**`docs/knowledge-base/release-guardrails.md`:**

| Step | Action |
| --- | --- |
| 11.8 | G4 row — set status to "Retired 2026-05-04 (MinVer removed)". The row stays for guardrail-ID continuity (G4 is a stable historical reference); the `Validator` column becomes `—`. |

**`docs/knowledge-base/release-lifecycle-direction.md`:**

| Step | Action |
| --- | --- |
| 11.9 | Any §referring to MinVer or git-tag-driven release — rewrite the surrounding paragraphs to describe the workflow_dispatch-only model. |

**`docs/knowledge-base/cake-build-architecture.md`:**

| Step | Action |
| --- | --- |
| 11.10 | Rewrite mentions of `GitTagVersionProvider`, the four-source provider pattern, and MinVer to the two-source model and the principle. |

**`docs/decisions/2026-04-20-release-lifecycle-orchestration.md`:**

| Step | Action |
| --- | --- |
| 11.11 | §describing the four version sources (manifest / explicit / git-tag / meta-tag) — rewrite in place to two sources. |
| 11.12 | §describing release triggers — rewrite in place to workflow_dispatch only. |
| 11.13 | Trigger × Provider × Scope axis matrix — collapses substantially; rewrite with the smaller cross-product. |

**`docs/plan.md`:**

| Step | Action |
| --- | --- |
| 11.14 | Current-status / roadmap mentions of git-tag, meta-tag, MinVer, four-source provider — rewrite in place. Reflect Phase 8's MinVer eradication and the principle. |

**`docs/onboarding.md`:**

| Step | Action |
| --- | --- |
| 11.15 | Native-build pipeline diagram and surrounding prose mentioning MinVer or git-tag triggers — rewrite in place. |

**`docs/playbook/local-development.md`:**

| Step | Action |
| --- | --- |
| 11.16 | Replace any `--target Package --explicit-version …` shortcut with the documented 2-command flow (ResolveVersions → Package via `--versions-file`). State the foundational principle near the top of the doc. |

**`docs/playbook/ci-troubleshooting.md`:**

| Step | Action |
| --- | --- |
| 11.17 | Any reference to git-tag-triggered failures or MinVer fallback versions — rewrite or delete. |

**`docs/playbook/cross-platform-smoke-validation.md`:**

| Step | Action |
| --- | --- |
| 11.18 | Same triage as 11.17. |

**`docs/playbook/adding-new-library.md`:**

| Step | Action |
| --- | --- |
| 11.19 | New-family checklist drops the `<MinVerTagPrefix>` step. |

**`docs/playbook/vcpkg-update.md`:**

| Step | Action |
| --- | --- |
| 11.20 | Version-bump section mentions MinVer in passing — rewrite. |

**`docs/phases/phase-2-release-cycle-orchestration-implementation-plan.md`:**

| Step | Action |
| --- | --- |
| 11.21 | Active-phase doc mentions of git-tag/meta-tag/MinVer — rewrite in place. |

**`docs/phases/phase-2-adaptation-plan.md`:**

| Step | Action |
| --- | --- |
| 11.22 | Same triage as 11.21. |

**`CLAUDE.md`** (repo root):

| Step | Action |
| --- | --- |
| 11.23 | "Pipeline targets (run individually for debugging)" code block — replace `--target Package --explicit-version sdl2-core=2.32.0-local.1 ...` with the 2-command flow. "Versioning notes" section — rewrite the `--explicit-version` bullet to: "`--explicit-version` / `--explicit-versions` are ResolveVersions inputs only; mutually exclusive with each other. Stage targets read `--versions-file`. See `docs/playbook/local-development.md` for the canonical 2-command flow." |

**Out of scope for this phase (intentionally frozen):**

- `docs/_archive/*` — archived snapshots stay frozen.
- `docs/research/*-2026-04-*.md` — dated research snapshots stay frozen.
- This research doc itself (`docs/research/2026-05-04-versioning-simplification-plan.md`) — treated as temp per session decision; no implementation footer required.

### Phase 12 — CLI surface validation

| Step | Action |
| --- | --- |
| 12.1 | `dotnet run --project build/_build -- --help` — confirm option descriptions match the principle (consumer-scope markers visible in help text for `--explicit-version`, `--explicit-versions`, `--versions-file`). |
| 12.2 | Confirm both explicit-input shapes work (see 12.2.1 below). Both should write the same versions.json shape. |
| 12.3 | `dotnet run --project build/_build -- --target Package` (no `--versions-file`) — confirm fail-loud with the actionable message (not a silent skip). Repeat for PreFlightCheck, PackageConsumerSmoke, PublishStaging. |
| 12.4 | `dotnet run --project build/_build -- --target ResolveVersions --version-source=explicit --explicit-version a=1.0 --explicit-versions "b=2.0"` — confirm mutex error from `Program.cs`. |

**12.2.1 — explicit-input shape parity:**

```pwsh
dotnet run --project build/_build -- --target ResolveVersions --version-source=explicit --explicit-version sdl2-core=2.32.0-test.smoke
dotnet run --project build/_build -- --target ResolveVersions --version-source=explicit --explicit-versions "sdl2-core=2.32.0-test.smoke,sdl2-image=2.8.0-test.smoke"
```

---

## 5. Files NOT changed (no impact)

| File | Reason |
| --- | --- |
| `Features/Packaging/PackagePipeline.cs` | Uses `GitLogTip` for HEAD SHA (G55); stays. |
| `Features/Packaging/FamilyTopologyHelpers.cs` | Used by `PackagePipeline` for topological pack ordering. |
| `Features/Packaging/SatelliteUpperBoundValidator.cs` | Unrelated. |
| `Features/Preflight/PreflightPipeline.cs` | Consumes `PackageBuildConfiguration` indirectly via DI; rename-only via Phase 5.3. |
| `Features/Preflight/StrategyCoherenceValidator.cs` | Unrelated. |
| `Features/Preflight/VersionConsistencyValidator.cs` | Unrelated to version source. |
| `Features/Preflight/CoreLibraryIdentityValidator.cs` | Unrelated. |
| `Shared/Versioning/*` | `IUpstreamVersionAlignmentValidator` + impl stay. |
| `Shared/Manifest/*` | Schema models stay. |
| `Integrations/Vcpkg/*` | Stay. |
| `Integrations/NuGet/*` | Stay. |
| `Integrations/DependencyAnalysis/*` | Stay. |
| `Integrations/Msvc/*`, `Integrations/DotNet/DotNetRuntimeEnvironment.*`, `Integrations/DotNet/ProjectMetadataReader.*` | Stay. (`DotNetPackInvoker.cs` is modified — see Phase 8.17.) |
| `Tools/*` | All Cake.Frosting `Tool<T>` wrappers stay. |
| `Host/Paths/*`, `Host/Cake/*`, `Host/BuildContext.cs`, `Host/Enums.cs` | Stay. |
| `Features/Harvesting/*`, `Features/Coverage/*`, `Features/Vcpkg/*`, `Features/Info/*`, `Features/Ci/*`, `Features/Diagnostics/*`, `Features/DependencyAnalysis/*`, `Features/Maintenance/*` | Stay. |
| `Features/Publishing/PublishPipeline.cs` | Stays (Phase 5.4 only touches `PublishStagingTask.cs`). |
| `tools.cs` | Already uses only `--version-source=manifest` and the resolve→stage 2-command flow. |
| `docs/_archive/*` | Frozen historical snapshots. |
| `docs/research/*-2026-04-*.md` | Frozen dated research. |

---

## 6. Open follow-ups (out of scope)

These are explicitly **not** addressed by this plan; capture them as future tickets if they prove material:

- **Local-dev wrapper in `tools.cs`.** The 2-command resolve→stage flow is documented but not ergonomically optimized. If repeated friction surfaces, a `tools pack --explicit-version foo=1.0.0` convenience wrapper that orchestrates the chain is a candidate. Out of scope here because it's UX surface, not boundary-correctness.
- **`Cake.Frosting.Git` arm64 extension.** LibGit2Sharp x64-only constraint means jobs that hit `GitLogTip` (Pack today) cannot run on arm64 runners. Pre-existing, not introduced by this plan.
- **Meta-package versioning scheme** (`Janset.SDL2` umbrella). Tracked under PD-7; this plan does not touch it.
- **Public NuGet promotion (`publish-public` job).** Stub remains disabled; PD-7 work.
