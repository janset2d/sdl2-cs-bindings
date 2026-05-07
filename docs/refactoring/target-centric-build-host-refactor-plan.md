# Target-Centric Build Host Refactoring Plan

- **Status:** Accepted planning baseline
- **Decision:** [ADR-002: Target-Centric Cake Build Host Architecture](../decisions/2026-05-05-target-centric-build-host.md)
- **Scope:** `build/_build`, `build/_build.Tests`, build-host docs, and command-contract call sites in `tools.cs`, `.github/workflows/release.yml`, and `.github/actions/` when a target contract changes
- **Non-goal:** New SDL bindings, new native packaging features, public publish implementation, broad product roadmap changes

This document consolidates the design discussion behind ADR-002 so the reasoning does not depend on transient chat history. The ADR is the concise decision record; this plan is the detailed execution map.

## 1. North star

The build host should read like the Cake target graph it implements.

When a maintainer sees `--target Package` in `tools.cs`, `release.yml`, or a local command, they should be able to navigate directly to the target module and read the build story without first decoding a feature/pipeline/configuration abstraction stack.

The preferred failure mode is slightly longer, honest task orchestration over tiny pass-through task classes hiding behavior in oversized private-method or pipeline soup.

## 2. Why this refactor exists

The current build host works, but the architecture is not carrying its weight anymore.

### Pain points

| Area | Current pain | Desired direction |
| --- | --- | --- |
| Task readability | Many task classes immediately delegate to `*Pipeline`. | Task classes own high-level orchestration. |
| Pipeline classes | Large pipeline files mix orchestration, validation, policy, reporting, and IO. | Extract named collaborators only when they represent real behavior or seams. |
| `Features` naming | `Features/<X>` does not match how Cake users navigate the build. | `Targets/<CakeTargetName>` is the primary layout. |
| `Shared` / `Common` gravity | Shared folders can become dumping grounds or artificial architecture layers. | Elevate only real named concepts: `Manifest`, `Runtime`, `Versioning`, `Packaging`, `Results`, repositories. |
| `Host/Configuration` | Configuration objects live far from the target using them. | `BuildContext` exposes named CLI properties; each task validates what it consumes. |
| Private method soup | Private methods are used as folding regions for behavior that wants a name. | Private methods are fine for local mechanics; extract business/build policy and independently testable algorithms. |
| Interface ceremony | Interface-per-class creates noise and fake abstractions. | Hybrid interface policy: use interfaces for important seams, not every helper. |
| Strategy abstraction | `strategy: "hybrid-static"` is treated as configurable despite being a project invariant. | Remove manifest/code strategy polymorphism; document hybrid-static as invariant. |
| Coverage gate | Cake-owned coverage ratchet is not core build-host behavior. | Retire the Cake target; coverage may return later as optional signal. |
| Architecture tests | Dependency/layer tests enforce old architecture rather than good design. | Replace with ADR, plan, `AGENTS.md`, and review checklist. |

## 3. Research signals retained

### Cake Frosting

Cake Frosting documentation frames tasks as units of work:

- `[TaskName]` maps a class to the target name.
- `[IsDependentOn]` expresses target graph edges.
- `BuildContext` is the extension point for custom invocation state.
- Constructor injection is available but optional.

This supports a target-first architecture. Cake does not require mandatory pipeline wrappers or an external service layer for every target.

Additional Cake-specific conclusions:

- Cake is the host language of the build host, not a thin process runner under generic C# orchestration.
- `FrostingLifetime<TContext>` may be useful for true host lifecycle setup/teardown, but it is not the default home for manifest/version preload. Target-owned state should stay explicit at the task boundary.
- The Cake target graph should model user-visible lifecycle stages. Do not split one large target into internal pseudo-targets such as `HarvestPlan` / `HarvestExecute` just to reduce LOC.
- For build-host IO, process execution, paths, logging, environment access, and tool invocation, Cake-native abstractions are the default.
- Before wrapping an external tool or writing a custom adapter, check whether a Cake alias, addin/plugin, or existing Cake abstraction already covers the job. If not using one, document why in the slice review.

### Existing good examples

The current versioning tasks are already close to the desired shape:

- `ResolveVersionsFromManifestTask`
- `ResolveVersionsFromExplicitTask`

They read and validate target input close to the task, perform direct orchestration, and write output without a mandatory pipeline wrapper.

Use them as migration examples, not as perfect final code.

### Current hotspots

Observed hotspots from the current codebase:

| File | Why it matters |
| --- | --- |
| `build/_build/Features/Harvesting/HarvestPipeline.cs` | Large mixed orchestration/reporting/status/validation pipeline. |
| `build/_build/Features/Packaging/PackagePipeline.cs` | Large packaging boss fight: family selection, versions, pack invocation, validation, dependency ranges. |
| `build/_build/Features/Packaging/PackageConsumerSmokePipeline.cs` | Large consumer smoke orchestration with platform/TFM behavior. |
| `build/_build/Features/Preflight/PreflightPipeline.cs` | Cross-cutting validation pipeline that includes strategy-era logic. |
| `build/_build/Host/Configuration/` | Centralized configuration pattern marked for retirement. |
| `build/_build/Shared/Strategy/` | Strategy abstraction marked for retirement. |
| `build/_build/Features/Coverage/` | Coverage target marked for retirement. |
| `build/_build.Tests/Unit/CompositionRoot/ArchitectureTests.cs` | Architecture tests marked for deletion/replacement by written guardrails. |

### TUnit

TUnit creates a new test class instance per test. Therefore:

- use constructors for simple per-test setup;
- use hooks for async setup/cleanup;
- use `ClassDataSource` for expensive shared resources only;
- do not blindly create a large xUnit-style `TestBase`.

### External test infrastructure inspiration

The `homeruntech/dotnet-backend-monorepo-tool` test suite inspired:

- fixture data folders;
- fluent fake repo builders;
- scenario context objects;
- test logging helpers;
- behavior-level scenario tests.

Adopt the useful ideas, but keep this repo Cake-native. Do not introduce a second filesystem abstraction.

## 4. Target inventory

Current Cake target surface found via `[TaskName]`:

| Target | Current area | Migration notes |
| --- | --- | --- |
| `Info` | `Features/Info` | Low-risk early migration. Useful for target layout proof. |
| `GenerateMatrix` | `Features/Ci` | CI contract target; preserve output shape exactly. |
| `EnsureVcpkgDependencies` | `Features/Vcpkg` | Tool/process-adjacent; keep Cake abstractions acceptable. |
| `ResolveVersionsFromManifest` | `Features/Versioning` | Already close to target-owned orchestration. Early migration candidate. |
| `ResolveVersionsFromExplicit` | `Features/Versioning` | Already close to target-owned orchestration. Early migration candidate. |
| `PreFlightCheck` | `Features/Preflight` | Medium/high risk due cross-cutting validations and strategy retirement. |
| `Harvest` | `Features/Harvesting` | High risk boss fight; per-RID artifact/status/native behavior. |
| `NativeSmoke` | `Features/Harvesting` | Coupled to Harvest stage and external native harness. |
| `ConsolidateHarvest` | `Features/Harvesting` | Medium/high risk; artifact merge and manifest summary behavior. |
| `Package` | `Features/Packaging` | High risk boss fight; pack invocation, versions, dependency ranges, validation. |
| `PackageConsumerSmoke` | `Features/Packaging` | High risk; platform/TFM/package restore/runtime behavior. |
| `PublishStaging` | `Features/Publishing` | Mission-critical, but can migrate after package contracts stabilize. |
| `PublishPublic` | `Features/Publishing` | Stubbed; keep behavior unchanged unless Phase 2b PD-7 explicitly starts. |
| `Dumpbin-Dependents` | `Features/DependencyAnalysis` | Diagnostic target. Folder can keep hyphen; namespace cannot. |
| `Ldd-Dependents` | `Features/DependencyAnalysis` | Diagnostic target. Folder can keep hyphen; namespace cannot. |
| `Otool-Analyze` | `Features/DependencyAnalysis` | Diagnostic target. Folder can keep hyphen; namespace cannot. |
| `Inspect-HarvestedDependencies` | `Features/Diagnostics` | Diagnostic target. Preserve command usability. |
| `Coverage-Check` | `Features/Coverage` | Retire as Cake target. Remove command references and baseline gate. |

For hyphenated target names, the folder may match the Cake target string for navigation, for example `Targets/Otool-Analyze/`, while namespaces use valid C# names such as `Build.Targets.OtoolAnalyze`.

## 5. Final architecture rules

### 5.1 Target modules

Default:

```text
build/_build/
  Targets/
    <CakeTargetName>/
      <CakeTargetNameWithoutInvalidCSharpChars>Task.cs
```

Use subfolders only when they improve navigation:

```text
Targets/
  Package/
    PackageTask.cs
    Requests/
      PackageRequest.cs
    Services/
      PackageFamilyPacker.cs
      DependencyRangeNormalizer.cs
    Validation/
      HarvestReadinessValidator.cs
      PackageOutputValidator.cs
    Reporting/
      PackageReporter.cs
    Models/
      PackagePlan.cs
```

Do not create empty symmetry. A target gets the folders it earns.

Practical trigger: when a second file in the same category appears, introduce the standard subfolder for that category. One `PackageReporter` can live beside the task; `PackageReporter` plus `PackageSummaryWriter` earns `Reporting/`.

### 5.2 Task anatomy

A healthy task class:

1. reads named `BuildContext` properties;
2. validates task input at the task boundary;
3. builds a `<Target>Request` for non-trivial executable target behavior when collaborators need a stable input contract;
4. orchestrates the build story;
5. delegates named behavior to collaborators;
6. logs expected user-facing failures once;
7. throws `CakeException` at the task boundary for expected fatal failures.

Simple, no-op, default, marker, or fully-inline targets are exempt from request ceremony.

Cake task classes are discovered by Cake Frosting from `[TaskName]` metadata and should not be explicitly registered in DI. Register the task's collaborators, repositories, tools, and options; let Cake construct the task from the service provider. V2 tests follow the same rule by creating the task from the provider without adding the task type as a service.

### 5.3 Extraction rule

For the full private-method decision tree and collaborator design guidance, see [extraction-guidelines.md](extraction-guidelines.md). The summary:

Keep code in the task when it is:

- orchestration narrative;
- short and target-specific;
- unlikely to be reused;
- not useful to test independently;
- clearer inline than behind a fake abstraction.

Extract code when it is:

- build policy;
- branching-heavy algorithm;
- reusable behavior;
- dependency-heavy IO/tool adapter;
- independently testable validation;
- a concept that deserves a name in the build domain.

Bad extraction target names:

- `Helper`
- `Manager`
- `Processor`
- `Handler`
- `Runner` without a concrete build-domain noun
- `Pipeline` as a generic wrapper

Good extraction target names:

- `VersionFileRepository`
- `ManifestRepository`
- `DependencyRangeNormalizer`
- `HarvestReadinessValidator`
- `PackageReporter`
- `HarvestStatusRepository`
- `PackageFamilyPacker`

### 5.4 Interface rule

Do not create `IFoo` because `Foo` exists.

Create an interface when:

- multiple production implementations exist;
- the seam is expensive or process/tool-backed;
- the contract is important enough that tests and tasks should depend on it;
- the interface expresses a real axis of change.

Small pure helpers stay concrete.

### 5.5 Cake nativeness rule

Cake-native is a hard rule for build-host IO, process execution, paths, logging, environment access, and tool invocation.

Prefer, in order:

1. existing Cake aliases, built-in abstractions, or Cake addins/plugins;
2. existing project `Tool<TSettings>` wrappers;
3. new focused `Tool<TSettings>` wrappers for external CLIs;
4. named library/API adapters when the integration is not a CLI boundary;
5. raw BCL IO or raw process invocation only as a justified exception in slice review.

Cake types are expected in IO/tooling build code:

- `ICakeContext`
- `ICakeLog`
- `IFileSystem`
- `FilePath`
- `DirectoryPath`
- `Tool<TSettings>` and typed tool settings

Pure policy code should prefer Cake-free value types and records. This is not an exception to Cake nativeness; it is the correct boundary.

The rule is not "remove Cake"; the rule is "use Cake where the build host touches the build environment, and keep pure concepts pure."

### 5.6 Cake target graph rule

Cake targets are user-visible lifecycle stages, not internal private methods with attributes.

Do not split large work into internal pseudo-target chains merely to reduce task class size. Prefer one user-visible target plus named collaborators. Split into multiple Cake targets only when each target is independently meaningful from `tools.cs`, CI, or the command line.

### 5.7 Code/document boundary

Logic-bearing code should stand on its own.

Comments in `.cs`, `.csproj`, `.props`, `.targets`, workflow YAML, and local orchestration scripts must explain the local logic or external constraint directly. Do not use comments that send readers to internal or external documentation instead of explaining the behavior in place.

Docs may reference code. Code should not outsource its meaning to docs.

### 5.8 Guardrail naming

Release guardrail IDs such as `G58` are documentation/reporting identifiers, not primary code names.

Use behavior-first names for types, files, methods, and test classes. Guardrail IDs may appear as report metadata, log evidence, test data, or documentation mappings, but a maintainer reading code should not need `release-guardrails.md` open to understand what a class does.

Example migration direction:

| Current smell | Preferred code identity |
| --- | --- |
| `G58CrossFamilyDepResolvabilityValidator` | `CrossFamilyDependencyResolvabilityValidator` or a more precise behavior name |
| `IG58CrossFamilyDepResolvabilityValidator` | `ICrossFamilyDependencyResolvabilityValidator` only if the interface is justified |
| `G58CrossFamilyCheckStatus` | `CrossFamilyDependencyCheckStatus` |

## 6. Shared concept map

The target architecture does not have a catch-all `Shared`.

Candidate top-level concepts:

| Concept | Owns | Does not own |
| --- | --- | --- |
| `Manifest` | Manifest models, manifest loading, manifest validation helpers that are not target-specific. | Target-specific preflight orchestration. |
| `Runtime` | RID/triplet/platform identity, runtime selection, platform predicates. | Per-target runtime behavior. |
| `Versioning` | `PackageFamilyId`, version file read/write, version set models, D-3seg helpers. | `ResolveVersions*` task orchestration. |
| `Packaging` | Package-domain models reused by multiple targets. | `PackageTask` orchestration if only used there. |
| `Results` | Minimal `Result<T,TError>`, validation report/check primitives. | Target-specific error catalogs unless reused. |
| `Repositories` or named repository folders | File-backed source-of-truth adapters. | Generic in-memory services or fake DDD ceremony. |

Migration rule: promote only when reuse is real or imminent in the same phase. Otherwise keep code with the target.

"Imminent" means a second real consumer is being created in the same migration slice or phase. It does not mean "we can imagine a future consumer someday."

### Integrations dissolution policy

`Integrations/` is not a default target-state layer.

Current integration adapters should be dissolved by destination:

| Adapter kind | Destination |
| --- | --- |
| Cake `Tool<TSettings>` wrappers | `Tools/` |
| Single-target adapters | The consuming `Targets/<CakeTargetName>/Services/` folder |
| Cross-target domain adapters | A named concept such as `Packaging`, `Versioning`, `Runtime`, `Manifest`, `NuGet`, or `Vcpkg` |
| Raw process wrappers where Cake/addins solve the same problem | Replace with the Cake-native abstraction |
| Coverage-only adapters | Delete with the coverage gate |

Examples:

- `DotNetPackInvoker` starts target-local under `Targets/Package/Services/` unless another target needs it.
- `ProjectMetadataReader` should move to a named concept only if it is reused outside Package/PreFlight packaging validation.
- `VcpkgBootstrapTool` belongs with Vcpkg tooling if it remains a Cake tool boundary.
- runtime dependency scanners should become a named native dependency scanning concept only if Harvest and diagnostic targets both consume them.

## 7. BuildContext and configuration plan

### Current problem

`Program.cs` and `Host/Configuration` currently centralize too much target-specific knowledge.

The refactor should remove:

- `Configurations`
- `VcpkgConfiguration`
- `PackageBuildConfiguration`
- `RepositoryConfiguration`
- `DotNetBuildConfiguration`
- `DumpbinConfiguration`
- raw `ParsedArguments` exposure from normal task code

### Target shape

`BuildContext` should expose named readonly properties such as:

- `RuntimeIdentifier`
- `TargetRid`
- `VersionsFilePath`
- `VersionSuffix`
- `ExplicitVersion`
- `PackageScope`
- `Configuration`
- `Verbosity` or existing Cake logging settings when needed

Exact property names should follow the existing CLI option names but avoid leaking parser dictionaries.

When a target migrates to `Targets/`, it reads named `BuildContext` properties rather than `BuildContext.ParsedArguments`. If a migration slice removes the last task consumer of a raw parser value, remove that value from the public `BuildContext` surface in the same slice. `ParsedArguments` remains a composition-root binding input, not a task-facing API.

### File-backed state

Move file-backed state behind repositories:

- `ManifestRepository`
  - loads `build/manifest.json`;
  - returns typed manifest model;
  - owns path-specific loading details.
- `VersionFileRepository`
  - reads/writes `artifacts/resolve-versions/versions.json` or caller-specified path;
  - returns `PackageFamilyVersionSet`;
  - does not expose `IReadOnlyDictionary<string, NuGetVersion>` on task/service boundaries.

### Version identity

Introduce:

- `PackageFamilyId`
  - string-backed value object;
  - not an enum;
  - validates empty/whitespace;
  - keeps manifest-driven extensibility.
- `PackageFamilyVersionSet`
  - wraps family/version pairs;
  - offers lookup by `PackageFamilyId`;
  - exposes intentional methods such as `RequireVersion(familyId)` or `TryGetVersion(familyId)`;
  - avoids raw dictionary-shaped APIs at boundaries.

`NuGetVersion` itself is acceptable.

### Runtime identity

`RuntimeId` is an open implementation-pressure decision under the `Runtime` concept.

Default: if migrated boundaries continue passing raw RID strings across target/service boundaries, introduce a string-backed `RuntimeId` value object similar in spirit to `PackageFamilyId`. Do not add it only for symmetry if existing `RuntimeProfile`/runtime models already provide a clear typed boundary.

## 8. Results, validation, logging

### Operation result

Use simple typed results for expected operation failures.

Conceptual shape:

```csharp
public readonly record struct Result<T, TError>
{
    public bool IsSuccess { get; }
    public T Value { get; }
    public TError Error { get; }
}
```

Do not design a universal monad shrine. Keep the API boring and readable.

### Validation report

Use separate validation types for multi-check output:

- `ValidationReport`
- `ValidationCheck`
- `ValidationSeverity`

Validation reports can carry warnings and errors. Operation results should not grow warning bags by default.

### Logging boundary

Rules:

- Task/reporting boundary logs expected fatal errors.
- Services may log progress/details when useful.
- Services should not log an expected error and also return/throw it for another layer to log.
- Unexpected exceptions should flow naturally unless the target can add meaningful context.

## 9. Testing architecture

> **Operational companion:** [`testing-guidelines.md`](testing-guidelines.md) — canonical reference for test data policy, V2/V1 infrastructure, filesystem seeding, scenario test structure, and anti-patterns. This section defines the architecture; the guidelines document defines the day-to-day rules.

### Taxonomy

| Folder | Meaning |
| --- | --- |
| `Unit/` | Direct tests for services, validators, pure policies, small algorithms. |
| `Scenarios/` | In-process target behavior tests using real task orchestration and fake Cake world. |
| `Integration/` | Narrow tests for external process/tool/filesystem boundaries. |
| `Characterization/` | Temporary safety net for legacy behavior during migration. |
| `Fixtures/Data/` | Reusable fake repo files and embedded/fixture inputs. |
| `Fixtures/` | Builders, fake Cake context, logger capture, test harnesses. |

### Scenario boundary

Scenario tests should execute the real task class and as much production composition as practical, but with:

- Cake `FakeFileSystem`;
- fake Cake environment;
- fake Cake log or log capture;
- fake tool/process outputs unless the test is explicitly integration-level;
- realistic manifest/project/harvest/package fixture data.

This is build-host-in-process E2E.

### Integration boundary

Integration tests are reserved for mission-critical edges:

- actual `dotnet` invocation;
- archive extraction behavior;
- native tool wrapper behavior;
- filesystem behavior that Cake `FakeFileSystem` cannot model;
- end-to-end smoke where process boundaries are the point.

Even integration tests may still use fake filesystem around unrelated pieces. "Integration" does not mean "make everything slow and flaky for vibes."

### Test infrastructure direction

Prefer composable test helpers:

- `FakeCakeWorld`
- `FakeRepoBuilder`
- `TargetTestHost`
- `TestLog`
- fixture seeders under `Fixtures/Seeders`

Avoid a giant abstract `TestBase`. Introduce a base class only if it becomes a narrow scenario DSL with a real domain name.

### TUnit rules

- New test class instance per test.
- Constructor setup is fine for simple state.
- `[Before(Test)]` / `[After(Test)]` for async per-test lifecycle.
- `ClassDataSource` only for expensive shared resources.
- Always `await` assertions.
- Keep the existing naming convention: `<MethodName>_Should_<Verb>_<optional When/If/Given>`.

### Filesystem rule

Cake project code and build-host tests should standardize on Cake filesystem abstractions.

Do not introduce `System.IO.Abstractions`. Do not use `System.IO` in the Cake project except at explicit external integration boundaries where Cake cannot represent the contract.

## 10. Workflow and branch strategy

Implementation should happen on an isolated local branch/worktree, not directly on `master`.

Preferred workflow:

1. Create local branch/worktree from clean `master`.
2. Do not push the refactor branch during iterative local work unless explicitly requested.
3. Commit at meaningful phase checkpoints, after presenting summary and proposed commit message.
4. Merge to `master` only after the full refactor is accepted.

`tools.cs` remains the local north star:

- local setup and validation flows should go through `tools.cs`;
- direct Cake invocation is valid for CI debugging and target discovery;
- scenario/unit tests can call task classes directly because they are testing build-host internals, not the human command surface.
- `tools.cs` remains standalone and should not reference `build/_build` internals.
- when `tools.cs` needs package-family or manifest knowledge, `build/manifest.json` is the source of truth; minimal local parsing is acceptable and should be protected by contract tests or smoke flows.

## 11. Phase plan

### P0 - Documentation and guardrails

Goal: make the decision durable before touching build behavior.

Tasks:

1. Add ADR-002.
2. Add this refactoring plan.
3. Add target-centric review checklist.
4. Update `AGENTS.md` so future agents do not follow the retired feature/pipeline guidance.
5. Link refactoring docs from `docs/README.md`.
6. Confirm no broken internal links.
7. Fold reviewed external-agent feedback into this plan only after technical evaluation.

Exit criteria:

- ADR, plan, checklist, and agent guidance agree.
- Old temporary review notes are either committed or consolidated here.
- No implementation code has changed.

### P1 - Baseline and inventory

Goal: establish a trustworthy baseline before moving code.

Tasks:

1. Create isolated local branch/worktree.
2. Run target discovery:
   - `dotnet run --file tools.cs -- build --tree`
   - `dotnet run --file tools.cs -- build --target Info`
3. Run build-host tests:
   - `dotnet test --project build/_build.Tests/Build.Tests.csproj -c Release --framework net10.0`
4. Capture current target list from `[TaskName]`.
5. Capture current command-contract call sites:
   - `tools.cs`
   - `.github/workflows/release.yml`
   - `.github/actions/*`
   - local-development playbooks
6. Identify tests that will break because they assert old architecture.
7. Capture `tools.cs` package-family and manifest helper duplication and decide which drift checks belong in P2/P3.
8. Invoke at least one existing primary-constructor/DI Frosting task during baseline, such as a version-resolution target, so task construction assumptions are proven before migration.
9. Inventory logic-bearing comments that point to docs instead of explaining behavior.
10. Inventory G-number-first type/file/test names and assign behavior-first replacement names.

Exit criteria:

- Baseline is known.
- Failing baseline, if any, is documented before refactor work starts.
- No target migration has started.

### P2 - Foundation and test infrastructure bootstrap

Goal: make behavior-preserving refactoring safe without building throwaway test infrastructure on top of retired configuration shapes.

Candidate files:

- `build/_build/Versioning/PackageFamilyId.cs`
- `build/_build/Versioning/PackageFamilyVersionSet.cs`
- `build/_build/Results/Result.cs`
- `build/_build/Results/ValidationReport.cs`
- `build/_build.Tests/Fixtures/FakeCakeWorld.cs`
- `build/_build.Tests/Fixtures/TargetTestHost.cs`
- `build/_build.Tests/Fixtures/TestLog.cs`
- `build/_build.Tests/Fixtures/FakeRepoBuilder.cs`
- `build/_build.Tests/Fixtures/Data/`
- `build/_build.Tests/Scenarios/`

Tasks:

1. Add the smallest foundation primitives needed by the test harness first:
   - `PackageFamilyId`
   - `PackageFamilyVersionSet`
   - minimal result/validation primitives if the first scenario needs them
2. Add value object and version set tests before implementation.
3. Keep existing `FakeRepoBuilder` useful, but decouple it from `Host/Configuration` as BuildContext changes.
4. Add `TargetTestHost` that can:
   - create fake Cake context;
   - register production services where practical;
   - override tool/process seams;
   - instantiate and run real task classes.
5. Add log capture helper for scenario assertions.
6. Add fixture data organization under `Fixtures/Data`.
7. Add one low-risk scenario test for an existing simple target before migrating it.

Exit criteria:

- New test harness code does not depend on soon-to-be-deleted `Host/Configuration` shapes except through compatibility shims.
- Scenario test harness exists.
- At least one real task can be exercised in-process with fake Cake world.
- Test infrastructure does not use a giant `TestBase`.
- Cake `FakeFileSystem` is the default filesystem in unit/scenario tests.

### P2a polish — Re-evaluated and re-scoped for P3 (2026-05-06)

The original P2a review proposed host composition parity via production `AddHostBuildingBlocks` plus fake overrides. That design assumed migrated targets would keep one foot in legacy — injecting `IRuntimeProfile`, `IPathService`, and `Configurations` sub-records resolved from the production DI pipeline.

That assumption was rejected during P3 design (2026-05-06). A migrated target reads simple named `BuildContext` properties (`RuntimeIdentifier`, `Configuration`, `VersionsFilePath`). It does not inject `IRuntimeProfile`, `IPathService`, or `Configurations`. Calling `AddHostBuildingBlocks` in the test host would register production services the task never touches, only to immediately replace half of them with fakes — ceremony, not safety.

**Revised P3 items:**

1. **`FakeCakeWorldV2` platform factories.** Three static factories — `CreateWindows()`, `CreateLinux()`, `CreateOsx()` — that set up the correct `FakeEnvironment` + a minimal valid manifest with matching `RuntimeInfo` + populated `SystemExclusions`. These are the "I just need a world" path. Everything is overridable via the existing fluent API (`WithRid`, `WithManifestFile`, `WithProcessResult`, etc.).

2. **Embedded JSON fixture files under `Fixtures/Data/`.** Inspired by the homeruntech/dotnet-backend-monorepo-tool pattern. Canonical manifest and version-set fixtures live as embedded resources under `Fixtures/Data/`, loaded via `Assembly.GetManifestResourceStream()`, and fed into `FakeCakeWorldV2` via `WithManifestFile`/`WithTextFile`. No runtime file I/O in tests, fully Cake-native at the test boundary. Pre-built fixtures for `win-x64`, `linux-x64`, `osx-x64` at minimum.

3. **`ToLegacyBuildContext` stays frozen.** The shim is a temporary compatibility bridge for unmigrated tests. It receives zero new features, zero fluent configuration for `Configurations` sub-records, and zero investment. It retires in P5 along with `Host/Configuration` and `Configurations`. No shim behavior tests — testing the shim is testing infrastructure we're deleting.

**The rule:** When a target migrates to `Targets/`, its tests use V2 infra exclusively. `TargetTestHostV2` builds `BuildContext` directly from `FakeCakeWorldV2` properties — no `AddHostBuildingBlocks`, no `Configurations`, no shim. `ToLegacyBuildContext` is not a compatibility support surface for migrated targets. Do not add V1 fixture features or shim behavior to keep old target tests alive. Migrated tests assert against the target's ADR-002 shape; unmigrated targets continue using the shim until their migration slice.

### P3 - Foundation completion and BuildContext transition ✅ (completed 2026-05-06)

Goal: finish the shared concepts needed by multiple target migrations without creating a new dumping ground. Prove the pattern by migrating `InfoTask` — the simplest target, already on V2 infra — to `Targets/Info/` with named `BuildContext` properties.

Candidate concepts:

- `IManifestRepository` / `ManifestRepository`
- `IVersionFileRepository` / `VersionFileRepository`
- `PackageFamilyId` (already shipped in P2b)
- `PackageFamilyVersionSet` (already shipped in P2b)
- `Result<T,TError>` (already shipped in P2b)
- `ValidationReport` / `ValidationCheck` (already shipped in P2b)

#### Repository design

Both repositories are practical file adapters (ADR-002 §6), not DDD repositories. Interfaces are justified (ADR-002 §8): file-backed seams that need test doubles.

```csharp
public interface IManifestRepository
{
    ManifestConfig Load();
}

public interface IVersionFileRepository
{
    PackageFamilyVersionSet Load();
    Task SaveAsync(PackageFamilyVersionSet versions);
}
```

Both take `ICakeContext` + `FilePath` via constructor injection. All JSON I/O routes through the existing `CakeJsonExtensions` (`ToJson<T>`, `WriteJsonAsync<T>`) — Cake-native per ADR-002 §9. The `[JsonConverter]` on `PackageFamilyVersionSet` is honored transparently because `CakeJsonExtensions` uses `System.Text.Json` internally. Zero new JSON code.

`ManifestRepository.Load()` deserializes `build/manifest.json` into the existing `ManifestConfig` model. `VersionFileRepository.Load()` deserializes `versions.json` into `PackageFamilyVersionSet`; `SaveAsync()` serializes it back out via `WriteJsonAsync`. The flat `{family-id: semver}` wire shape matches the existing `VersionsJsonWriter` output exactly.

Errors surface as `CakeException` (inherited from `CakeJsonExtensions` for missing files, bad JSON, null deserialization). No new error types.

#### FakeCakeWorldV2 platform factories

Three static factories that Just Work for the common case:

```csharp
public static FakeCakeWorldV2 CreateWindows(string? repoRoot = null);
public static FakeCakeWorldV2 CreateLinux(string? repoRoot = null);
public static FakeCakeWorldV2 CreateOsx(string? repoRoot = null);
```

Each factory sets up the correct `FakeEnvironment` (Windows/Unix), a minimal valid `ManifestConfig` on disk with a matching `RuntimeInfo` and populated `SystemExclusions`, and sensible default process results. Everything is overridable via the existing fluent API. The manifest is seeded as an embedded JSON fixture from `Fixtures/Data/`.

#### Embedded JSON fixtures (Fixtures/Data/)

Inspired by the homeruntech/dotnet-backend-monorepo-tool pattern. Canonical fixture files live as embedded resources under `build/_build.Tests/Fixtures/Data/`, organized by domain:

```text
Fixtures/Data/
  Manifest/
    manifest-win-x64.json
    manifest-linux-x64.json
    manifest-osx-x64.json
    manifest-minimal.json
    manifest-invalid-empty-runtimes.json
  Versions/
    versions-valid.json
    versions-empty.json
    versions-invalid-semver.json
```

Loaded via `Assembly.GetManifestResourceStream()` and fed into `FakeCakeWorldV2` via `WithManifestFile`/`WithTextFile`. No runtime file I/O in tests, fully Cake-native at the test boundary.

#### IAnsiConsole injection

`FakeCakeWorldV2` exposes a `Spectre.Console.Testing.TestConsole` instance per world:

```csharp
public TestConsole AnsiConsole { get; } = new();
```

`TargetTestHostV2.RunAsync()` registers it as `IAnsiConsole` alongside other Cake primitives. This unblocks scenario tests that need to assert on console output (InfoTask, future targets). `TestConsole` ships with `NoopExclusivityMode` — fully parallel-safe, zero shared state between test instances.

#### P3 task list

1. Add embedded JSON fixture infrastructure (`ResourceLoader` or equivalent) + `Fixtures/Data/` directory.
2. Add `FakeCakeWorldV2.CreateWindows/CreateLinux/CreateOsx` platform factories.
3. Add `IManifestRepository` / `ManifestRepository` + unit tests (uses `FakeCakeWorldV2.CakeContext` + `CakeJsonExtensions`).
4. Add `IVersionFileRepository` / `VersionFileRepository` + unit tests.
5. Introduce `BuildContext` named properties incrementally:
   - `RuntimeIdentifier` (string, from `ICakeEnvironment.Platform.Rid()`)
   - `Configuration` (string, from `--config` CLI option)
   - `VersionsFilePath` (FilePath, from `--versions-file` or default)
6. Migrate `InfoTask` to `Targets/Info/InfoTask.cs` with `IAnsiConsole` injection as the proof-of-pattern migration.
7. Keep `PathService` / `Host` path construction under `Host` unless implementation pressure reveals a better named concept.
8. Do not delete `Host/Configuration` until all consumers have moved.

Exit criteria:

- New target code can avoid raw parsed args and raw version dictionaries.
- Migrated `InfoTask` lives under `Targets/Info/` with named `BuildContext` properties.
- Existing unmigrated targets still run.
- Foundation code has unit tests and avoids premature broad abstractions.
- `TestConsole` is available in `FakeCakeWorldV2` for console-output assertions.
- No new code was added to `ToLegacyBuildContext` or `Host/Configuration`.

### P4 - Low-risk target migrations

Goal: prove the target module layout before touching boss fights.

Completed in this phase:

1. `Info`
2. `ResolveVersionsFromManifest`
3. `ResolveVersionsFromExplicit`
4. `versions.json` path contract resolved — `--versions-file` universal, `PathService` hardcoded directory removed
5. `CleanArtifacts` **retired** (not migrated — local hygiene belongs in `tools.cs`, not Cake)
6. `testing-guidelines.md` extracted as canonical test reference (embedded fixtures, V2/V1 rules, filesystem seeding, anti-patterns)
7. `CompileSolution` **retired** (not migrated — zero Cake callers; bare `dotnet build Janset.SDL2.sln` covers the use case)

Remaining suggested order: diagnostic targets only.

- `Dumpbin-Dependents`
- `Ldd-Dependents`
- `Otool-Analyze`
- `Inspect-HarvestedDependencies`

Tasks per target:

1. Add or preserve behavior tests.
2. Move to `Targets/<CakeTargetName>/`.
3. Keep task orchestration in the task class.
4. Extract only named collaborators that earn it.
5. Update DI registration.
6. Update tests/namespaces.
7. Verify command contract through direct Cake target or `tools.cs` where applicable.

**Info target — IAnsiConsole injection.** `InfoPipeline` is the only build-host class using Spectre.Console interactive widgets (`Status().StartAsync()`). The static `AnsiConsole` facade prevents parallel scenario testing and couples to a global console. During the `InfoTask` migration:

1. Add `Spectre.Console.Testing` package (`Spectre.Console.Testing.TestConsole`) for test-side console isolation. Each `TestConsole` ships with `NoopExclusivityMode` — zero-throw, fully parallel-safe.
2. Register `IAnsiConsole` in production `Program.cs`: `services.AddSingleton<IAnsiConsole>(AnsiConsole.Console)`.
3. Constructor-inject `IAnsiConsole` into `InfoPipeline`. Replace every static `AnsiConsole.X` call (`Write`, `MarkupLine`, `Status().StartAsync()`) with `_console.X`.
4. Audit and migrate all remaining static `AnsiConsole` call sites in the build host (`OtoolAnalyzePipeline.Write/MarkupLine`, `HarvestPipeline.Write`, `Program.cs MarkupLine`) to `IAnsiConsole` injection for consistency. These are non-interactive calls that do not race today but should use the injected console for testability.
5. `FakeCakeWorldV2` exposes a `Spectre.Console.Testing.TestConsole` instance per world: `public TestConsole AnsiConsole { get; } = new();`.
6. `TargetTestHostV2.RunAsync` registers it as `IAnsiConsole` alongside other Cake primitives: `services.AddSingleton<IAnsiConsole>(_world.AnsiConsole)`.
7. This unblocks the `InfoTask` failure-path scenario test (dotnet non-zero exit code), full log/output assertions, and the two deferred `InfoTask_Scenarios` tests.

**ResolveVersions targets — repository-backed version output.** `ResolveVersionsFromManifest` and `ResolveVersionsFromExplicit` now live under `Targets/`, consume named `BuildContext` properties, and write `PackageFamilyVersionSet` through `IVersionFileRepository`. `VersionsJsonWriter` and the old `Features/Versioning` service registration are gone. `ExplicitVersionParser` lives under the named `Versioning` concept and returns typed version sets. The explicit target keeps input-shape validation separate from parser invocation, and G54 validation has a typed overload for `PackageFamilyVersionSet`; the dictionary overload remains only for unmigrated stage consumers.

Exit criteria:

- Multiple low-risk targets use final layout.
- Migration pattern is boring and repeatable.
- No repo-wide rename has hidden old architecture under new names.
- All `AnsiConsole` static calls in the build host use `IAnsiConsole` injection.
- `InfoTask` failure-path scenario test passes.
- ResolveVersions targets use `PackageFamilyVersionSet` and repositories instead of raw version dictionaries and `VersionsJsonWriter`.
- `versions.json` path contract resolved: `--versions-file` universal, `PathService` hardcoded path removed.
- `CleanArtifacts` retired from Cake (local hygiene in `tools.cs`).
- `CompileSolution` retired from Cake (zero callers; bare `dotnet build Janset.SDL2.sln` covers the use case).
- `testing-guidelines.md` canonical test reference extracted and cross-referenced from ADR, plan, checklist, and AGENTS.md.

#### Post-P4 research task: `versions.json` path contract ✅ (resolved 2026-05-07)

Decision: `--versions-file` is universal — both ResolveVersions writers AND stage-task readers use it. `PathService.ResolveVersionsOutputDirectory` / `GetResolveVersionsOutputFile()` removed. `IVersionFileRepository` takes path at method-call time, not constructor injection. `BuildContext.VersionsFilePath` is `FilePath?` with no fallback; every task validates at entry (defense in depth). `CleanArtifacts` retired from Cake — artifact cleanup is local-dev hygiene owned by `tools.cs`, not a build pipeline stage. See commit `e893600` → current `master`.

### P5 - Retire coverage and strategy-era abstractions

Goal: remove abstractions explicitly rejected by ADR-002.

This phase intentionally happens after low-risk target migrations prove the new layout, but before PreFlight/Package/Harvest boss fights. Strategy-era code is concentrated around those boss fights, so retiring it before them reduces mixed-architecture drag without making it the first refactor risk.

Coverage tasks:

1. Remove `Coverage-Check` target.
2. Remove coverage baseline gate from release workflow.
3. Remove coverage-specific build-host code and tests.
4. Keep test result publishing/reporting behavior that is independent of coverage.

Strategy tasks:

1. Remove `strategy` from `build/manifest.json`.
2. Update manifest models.
3. Remove strategy resolver/factory/polymorphism code.
4. Remove strategy coherence validators.
5. Update preflight guardrails to validate the real invariant: all runtimes use the supported hybrid-static triplet model.
6. Update tests that assert old strategy behavior.
7. Rename guardrail-ID-first code touched during this phase to behavior-first names.

Exit criteria:

- No Cake target named `Coverage-Check`.
- No manifest field named `strategy`.
- Hybrid-static remains documented as an invariant.
- Preflight still catches actual manifest/runtime mistakes.

### P6 - PreFlightCheck migration

Goal: convert cross-cutting validation without recreating a mega-pipeline.

Candidate target module:

```text
Targets/
  PreFlightCheck/
    PreFlightCheckTask.cs
    Requests/
      PreFlightCheckRequest.cs
    Validation/
      ManifestVcpkgConsistencyValidator.cs
      CoreLibraryIdentityValidator.cs
      UpstreamVersionAlignmentValidator.cs
      PackageFamilyScopeValidator.cs
      ProjectPackContractValidator.cs
    Reporting/
      PreflightReporter.cs
```

Tasks:

1. Preserve existing G-numbered guardrail behavior where still valid.
2. Remove strategy-specific validation.
3. Use `ValidationReport` / `ValidationCheck` for multi-check output.
4. Use behavior-first validator names; guardrail IDs remain report/documentation metadata.
5. Keep task orchestration readable:
   - load manifest/version state;
   - run validators;
   - report warnings/errors;
   - throw once if fatal.
6. Add scenario coverage for success and representative failure cases.
7. Add (or strengthen) a manifest lowercase invariant validator: every `manifest.package_families[].name` must match `^sdl[0-9]+-[a-z][a-z0-9-]*$`. P2b's `PackageFamilyId` uses ordinal-exact equality; the manifest is canonical-lowercase by convention, but a hand-edited mixed-case entry (`SDL2-Core`) would silently bypass legacy ignore-case lookups and then break `PackageFamilyId` lookups in P3+/P4+ consumers. PreFlight is the canonical home for this contract.

Exit criteria:

- `PreFlightCheckTask` tells the validation story directly.
- Validators are named by the rule they enforce.
- No generic `PreflightPipeline` remains.
- Manifest lowercase invariant is enforced on every `package_families[].name`.

### P7 - Package migration

Goal: decompose the package boss fight without losing behavior.

Candidate target module:

```text
Targets/
  Package/
    PackageTask.cs
    Requests/
      PackageRequest.cs
    Services/
      PackageFamilySelector.cs
      PackageFamilyPacker.cs
      DependencyRangeNormalizer.cs
      DotNetPackInvoker.cs
    Validation/
      HarvestReadinessValidator.cs
      PackageOutputValidator.cs
      CrossFamilyResolvableValidator.cs
    Reporting/
      PackageReporter.cs
    Models/
      PackagePlan.cs
```

Placement rule:

- If `DotNetPackInvoker` is only used by Package, move it under `Targets/Package/Services`.
- If it becomes a cross-target adapter, place it under a named `DotNet` or `Packaging` concept, not a vague `Integrations` bucket.

Behavior to preserve:

- D-3seg version mapping.
- family-lock between managed/native packages.
- within-family minimum dependency range.
- cross-family lower/upper dependency range.
- G58 cross-family scope resolvability.
- harvest output readiness checks.
- nuspec/package payload validation.
- package output paths expected by `tools.cs` and CI.

Scenario tests to add before major surgery:

- package fails clearly when required harvest output is missing;
- package fails clearly when versions file lacks a required family;
- package uses the correct family version for managed/native pair;
- cross-family dependency ranges are normalized correctly;
- selected package scope does not silently omit required dependencies.

Exit criteria:

- `PackageTask` owns orchestration.
- `PackagePipeline` is deleted.
- Raw version dictionaries are not task/service boundaries.
- Packaging behavior is covered by unit + scenario tests.

### P8 - Harvest, NativeSmoke, and ConsolidateHarvest migration

Goal: split harvesting behavior into readable target stories while preserving per-RID artifact contracts.

This does not mean splitting Harvest into internal Cake pseudo-targets. `Harvest`, `NativeSmoke`, and `ConsolidateHarvest` are already user-visible lifecycle stages; internal planning/execution/validation steps should become collaborators.

Candidate target modules:

```text
Targets/
  Harvest/
  NativeSmoke/
  ConsolidateHarvest/
```

Harvest behavior to preserve:

- RID validation;
- vcpkg installed binary discovery;
- closure walk via platform tools;
- system library exclusion;
- hybrid-static leak checks;
- native payload copy layout;
- license attribution layout;
- per-RID harvest status persistence;
- artifact paths expected by CI.

Potential collaborators:

- `HarvestRequest`
- `HarvestPlanner`
- `BinaryClosureWalker` (existing concept likely remains)
- `HarvestStatusRepository`
- `HarvestReporter`
- `LicenseCollector`
- `DeploymentPlan`

Consolidate behavior to preserve:

- per-RID artifact merge;
- stage-replace behavior;
- `harvest-manifest.json`;
- `harvest-summary.json`;
- consolidated license layout.

NativeSmoke behavior to preserve:

- same-RID execution immediately after Harvest in CI;
- local harness behavior;
- platform-specific prerequisites/tool discovery.

Exit criteria:

- `HarvestPipeline`, `NativeSmokePipeline`, and `ConsolidateHarvestPipeline` are gone or reduced to named concepts if a name is still genuinely accurate.
- Artifact contracts consumed by `release.yml`, Package, and ConsumerSmoke are unchanged unless deliberately migrated in the same step.
- Scenario coverage exists for planning/status/reporting behavior.
- Integration coverage remains only where external native/tool boundaries require it.

### P9 - PackageConsumerSmoke and publishing migration

Goal: make consumer validation and publishing target modules readable without changing release behavior.

PackageConsumerSmoke behavior to preserve:

- local folder feed restore;
- package-first consumer contract;
- RID-specific native loading path;
- executable TFM selection;
- TUnit/MTP invocation;
- platform-specific TFM skip behavior;
- GitHub Actions test-result environment passthrough.

Publishing behavior to preserve:

- GitHub Packages staging push;
- package pair behavior;
- dry-run / missing-token failure behavior if present;
- `PublishPublic` remains stubbed until PD-7 starts.

Exit criteria:

- `PackageConsumerSmokeTask` tells the smoke story directly.
- publishing targets are target modules.
- no public publish behavior is accidentally implemented as part of the refactor.

### P10 - Final cleanup

Goal: remove old architecture residue.

Tasks:

1. Delete `Features/` after all targets migrate.
2. Delete `Host/Configuration/`.
3. Delete old `Shared/Strategy/` and coverage result types.
4. Replace broad `Shared/` with named concepts or delete what is no longer needed.
5. Delete architecture dependency tests.
6. Promote valuable `Characterization` tests into `Scenarios`.
7. Delete obsolete characterization tests that only protected old implementation shape.
8. Update:
   - `AGENTS.md`;
   - `docs/onboarding.md` if needed;
   - `docs/plan.md`;
   - `docs/playbook/local-development.md`;
   - `docs/playbook/local-validation.md`;
   - release guardrails docs if G-number ownership changes.

Exit criteria:

- New build-host code follows ADR-002.
- No old folder/concept remains solely for compatibility.
- Documentation and tests describe the new architecture, not the old migration state.

## 12. Validation gates

Use the smallest useful validation per phase, then widen.

Common commands:

```pwsh
dotnet run --file tools.cs -- build --tree
dotnet run --file tools.cs -- build --target Info
dotnet test --project build/_build.Tests/Build.Tests.csproj -c Release --framework net10.0
```

Target-specific validation:

| Change type | Minimum validation |
| --- | --- |
| Pure helper/value object | Targeted unit tests. |
| Task migration | Unit tests + relevant scenario tests + direct target invocation. |
| `tools.cs` command contract | Relevant `tools.cs` flow or a narrow smoke path. |
| `release.yml` contract | Static call-site review + target command validation. |
| Manifest schema change | Characterization/scenario tests + preflight validation. |
| Package/Harvest/ConsumerSmoke change | Scenario tests first, then targeted local command flow. |

Full native matrix validation is not required after every micro-step, but any change to artifact layout, native closure behavior, package payload shape, or consumer smoke restore/runtime behavior must get a wider validation pass before merge.

## 13. Review protocol for each migration slice

Before changing a target:

1. Name the Cake target and command-contract call sites.
2. State whether behavior should change. Default: no.
3. Identify tests that prove current behavior.
4. Identify abstractions being retired.
5. Identify any docs that must move with the change.

During review:

1. Does the task tell the orchestration story?
2. Did we extract named behavior, or did we create a new generic wrapper?
3. Did we avoid raw dictionaries and parser dictionaries on boundaries?
4. Did we avoid fake interfaces?
5. Did we log expected failures once?
6. Did we use Cake-native abstractions for IO/process/path/logging/tooling boundaries?
7. Did we keep Cake types out of pure policies where simple domain values would do?
8. Did we preserve `tools.cs` and CI command contracts?
9. Did we avoid new warning suppressions unless they are local, justified, and documented?
10. Did we check existing Cake aliases/addins/plugins before adding new tool wrappers or raw process calls?
11. Did comments explain local behavior instead of linking to docs?
12. Did code names describe behavior instead of relying on guardrail IDs?

After the slice:

1. Run the agreed validation.
2. Update docs if behavior/topology changed.
3. Present summary and proposed commit message before committing.

## 14. Open decisions to resolve during implementation

These are intentionally deferred until code pressure gives better information.

| Question | Default |
| --- | --- |
| Exact top-level folder names for named concepts | Use `Manifest`, `Runtime`, `Versioning`, `Packaging`, `Results` only when code exists. |
| Whether `DotNetPackInvoker` is target-local or shared | Keep target-local unless another target uses it. |
| Exact `BuildContext` property names | Mirror CLI names, but expose typed/named properties. |
| Whether `RuntimeId` is needed | Add a string-backed value object if raw RID strings keep crossing target/service boundaries. |
| Whether a scenario base class earns its keep | Default no; allow a narrow domain-specific scenario DSL later. |
| How fast `Characterization` disappears | Keep during migration, promote valuable tests to `Scenarios`, delete implementation-shape tests at the end. |
| Whether named-concept folders stay root-level long-term | ADR §7 places `Manifest`, `Runtime`, `Versioning`, `Packaging`, `Results`, and repositories as root-level siblings of `Targets/`. P2b ships `Versioning/` and `Results/` at root accordingly. **Revisit after P10**: with all named concepts and repositories live, reassess whether the resulting root-level fan-out (`Targets/`, `Tools/`, plus 5–7 named concepts) reads cleanly or warrants an ADR-002 amendment introducing a parent grouping. No relocation should be made mid-refactor — folder churn during target migrations is more expensive than a single post-refactor reorganization slice. |

## 15. Anti-goals

Do not:

- perform a repo-wide rename from `Features` to `Targets` without improving design;
- move all code into new `Shared`/`Common` folders;
- replace `Pipeline` with `Runner`/`Operation`/`Processor` doing the same thing;
- split a large target into internal Cake pseudo-targets merely to reduce LOC;
- make every service an interface;
- make `BuildContext` a service locator;
- preload manifest/version target state in `FrostingLifetime` by default;
- make `tools.cs` reference `build/_build` internals;
- use raw BCL IO/process APIs where Cake aliases/addins/abstractions solve the same problem;
- leave comments that point to docs instead of explaining local logic;
- name production or test code primarily after guardrail IDs such as `G58`;
- add `System.IO.Abstractions`;
- add broad `#pragma warning disable` suppressions as cleanup shortcuts;
- treat coverage percentage as a substitute for scenario coverage;
- use architecture tests to enforce taste;
- implement `PublishPublic` as part of this refactor;
- change native artifact contracts accidentally.

## 16. Definition of done

The refactor is done when:

1. Build-host target modules live under `Targets/`.
2. `tools.cs` remains the canonical local command surface.
3. `Host/Configuration` is gone.
4. Mandatory `*Pipeline` pattern is gone.
5. Strategy abstraction and manifest `strategy` field are gone.
6. Cake-owned `Coverage-Check` gate is gone.
7. Architecture dependency tests are gone.
8. Unit/scenario/integration taxonomy is in place.
9. Package/Harvest/ConsumerSmoke contracts still work.
10. Build-host IO/process/path/logging/tooling boundaries are Cake-native by default.
11. Logic-bearing comments are self-contained and do not link to docs as a substitute for explanation.
12. Guardrail-related code uses behavior-first names; guardrail IDs remain metadata/reporting labels.
13. `Integrations/` has either disappeared or every remaining adapter has a named, justified destination.
14. Docs and agent guidance describe the new architecture, not the old one.
