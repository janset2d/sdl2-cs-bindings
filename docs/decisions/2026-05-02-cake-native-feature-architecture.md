# ADR-004: Cake-Native Feature-Oriented Build-Host Architecture

- **Status:** Accepted
- **Date:** 2026-05-02
- **Last reviewed:** 2026-05-02
- **Deciders:** Deniz İrgin (primary), collaborative critique synthesis (2026-05-01 — first reviewer pass + second reviewer's critique + first reviewer's revision)
- **Supersedes:** [ADR-002 (DDD Layered Architecture for the Cake Build-Host)](2026-04-19-ddd-layering-build-host.md)
- **Succeeds:** ADR-002 governs no live code after this ADR's wave-2 close; its three-criteria interface rule (§2.3) is preserved verbatim under §2.9, including the §2.3.1 delegate-hook amendment.
- **Orthogonal to:** [ADR-001 (D-3seg Versioning + Package-First + Artifact Source Profile)](2026-04-18-versioning-d3seg.md) — external contracts are unaffected.
- **Refines:** [ADR-003 (Release Lifecycle Orchestration)](2026-04-20-release-lifecycle-orchestration.md) — ADR-003's release-lifecycle invariants (provider/scope/version axes, stage-owned validation, matrix re-entry, G54/G58 placement) are unchanged. Only ADR-003's internal-layout references shift from ADR-002's `Application/Domain/Infrastructure` shape to ADR-004's `Features/Shared/Tools/Integrations/Host` shape.

---

## 1. Context

### 1.1 Why ADR-002 is being superseded

ADR-002 (2026-04-19) chose horizontal layering — `Application` / `Domain` / `Infrastructure` with `Tasks/` as the Cake-native presentation exception — to halt the flat `Modules/*` drift that preceded it. ADR-002 succeeded at its immediate goal: it formalized cross-module reuse of `IPathService`, locked layer-direction tests, and gave Phase 2b a coherent shape to land into.

Six weeks of Phase 2 implementation surfaced a different failure mode: **the layered shape is wrong-routed for a build host's cognitive map.**

A 2026-05-01 critique pass (preserved at [`docs/reviews/code-review-conversation.txt`](../reviews/code-review-conversation.txt) and [`docs/reviews/conversation-2.txt`](../reviews/conversation-2.txt)) catalogued the symptoms:

1. **Cognitive routing is wrong-shaped.** Contributors do not ask "where is the domain model?" — they ask "where does Package skip?", "what does Harvest copy?", "how is the local feed prepared?". DDD layering forces a three-folder traversal to read one operational behavior.
2. **`Domain/` carries 93 files.** `Domain/Packaging/` (24) and `Domain/Preflight/` (20) alone exceed `Tasks/` (20). This is not domain complexity — it is misclassification. Validators that orchestrate behavior, generators that produce side effects, reporters with I/O were placed under `Domain/` because "they have models adjacent to them." Pure value objects, orchestrating services, and behavioral validators all share one folder.
3. **Task + TaskRunner duality is semantic noise.** Cake Frosting's `[TaskName]`-decorated `FrostingTask<BuildContext>` is already the public adapter. Adding `*TaskRunner` next to `*Task` for every operation duplicates the abstraction. New contributors ask "is the task the runner or the runner the task?".
4. **`Context/` folder mixes five concerns.** `BuildContext.cs` + `Configs/` + `Models/` + `Options/` + `CakeExtensions.cs` + `Enums.cs` all under one heading. Options are CLI surface, Models are manifest contract, Configs are derived state — none are "context" in the Cake-native sense.
5. **Single-implementation interfaces accumulated despite ADR-002 §2.3 discipline.** `IPackageTaskRunner`, `IPackageConsumerSmokeRunner`, `IBinaryClosureWalker`, `IArtifactPlanner`, `IArtifactDeployer` each have one production implementation and primarily exist as test mock seams. ADR-002 §2.3 said test substitution alone was insufficient justification, but enforcement was case-by-case and inconsistent.
6. **Sub-validators composed via `new` inside orchestrators.** `PackageOutputValidator` constructs `NativePackageMetadataValidator`, `ReadmeMappingTableValidator`, `SatelliteUpperBoundValidator` directly — the same §1.1.3 problem ADR-002 itself flagged but the layered shape did not solve.

### 1.2 The strategic observation

A build host is not a domain application. It is a release machine — a directed acyclic graph of operations, each producing artifacts consumed by the next. The natural mental model is the **operational journey**, not the **domain model**.

ADR-002 was the right call for the first refactor cycle. But the layered shape has reached its ceiling: every Phase-2b additional operation (`PublishStaging`, `GenerateMatrix`, `ResolveVersions`, `PreflightReporter`, three `IArtifactSourceResolver` profiles) accelerated the file-count trajectory ADR-002 was supposed to halt — because the layered shape inadvertently rewards adding a fourth file (model + interface + impl + validator) per concern instead of co-locating them by operation.

> **Motto for the new shape:** *Features own behavior. LocalDev owns orchestration. Shared owns vocabulary. Tools run commands. Host runs Cake.*

### 1.3 Decision precedents reviewed

| Option | Shape | Outcome |
|---|---|---|
| **Keep ADR-002 unchanged** | `Application` / `Domain` / `Infrastructure` + `Tasks/` exception | Rejected — drift continues; cognitive routing mismatch persists |
| **DDD with stricter §2.3 review** | Same shape, prune single-impl interfaces aggressively | Rejected — addresses the symptom (interface bloat) without addressing the cause (horizontal misrouting) |
| **Hexagonal / Adapters-and-Ports** | Domain core, ports, adapters rings | Rejected (twice): once by ADR-002 §1.3, again by 2026-05-01 critique — hexagonal jargon over-fits a build host |
| **Cake-native vertical slices with "Adapters" bucket** | `Tasks` / `Steps` / `Flows` / `Shared` / `Adapters` / `Host` (six top-level) | Rejected — second-reviewer critique flagged "Adapters" as hexagonal-jargon repackaging; "Steps" mandatory recreates Task+Runner duality under a new name |
| **Cake-native feature-oriented with size-triggered Pipeline + Tools/Integrations split** | `Host` / `Features` / `Shared` / `Tools` / `Integrations` (five top-level) | **Accepted** |

---

## 2. Decision

### 2.1 Top-level shape

```
build/_build/
├── Host/             ← Cake/Frosting runtime, CLI parsing, BuildContext, composition root, paths, Cake extensions
├── Features/         ← operational vertical slices (Harvesting, Packaging, Preflight, Versioning, Publishing, LocalDev, ...)
├── Shared/           ← build-domain vocabulary (manifest models, runtime models, package family conventions, results)
├── Tools/            ← Cake `Tool<TSettings>` wrappers ONLY (vcpkg, dumpbin, ldd, otool, tar, msvc, cmake, native-smoke)
├── Integrations/     ← non-Cake-Tool external adapters (NuGet protocol client, dotnet pack invoker, project metadata reader, coverage XML readers, vcpkg manifest reader)
└── Program.cs        ← entry point + CLI invocation
```

Five top-level folders. Each answers exactly one navigational question:

| Folder | Question it answers |
|---|---|
| `Host/` | How does the Cake host bootstrap and compose? |
| `Features/` | Where is target X implemented? |
| `Shared/` | What vocabulary do features speak to each other? |
| `Tools/` | Which Cake-native external CLI wrappers exist? |
| `Integrations/` | Which non-Cake external system clients exist? |

### 2.2 Host/

```
Host/
├── BuildContext.cs                   ← Cake/Frosting invocation context (data + ambient API only; §2.11)
├── Cli/
│   ├── BuildCli.cs
│   ├── ParsedArguments.cs
│   └── Options/                      ← raw CLI option definitions (operator-input surface)
│       ├── CakeOptions.cs
│       ├── DotNetOptions.cs
│       ├── DumpbinOptions.cs
│       ├── PackageOptions.cs
│       ├── RepositoryOptions.cs
│       ├── VcpkgOptions.cs
│       └── VersioningOptions.cs
├── Configuration/                    ← derived/normalized build-time configuration
│   ├── DotNetBuildConfiguration.cs
│   ├── DumpbinConfiguration.cs
│   ├── PackageBuildConfiguration.cs
│   ├── RepositoryConfiguration.cs
│   ├── VcpkgConfiguration.cs
│   └── VersioningConfiguration.cs
├── Composition/
│   └── CompositionRoot.cs            ← orchestrates feature service registrations
├── Cake/                             ← Cake-extension surface (split by concern, not a kitchen drawer)
│   ├── CakeJsonExtensions.cs
│   ├── CakePlatformExtensions.cs
│   └── CakeFileSystemExtensions.cs
└── Paths/
    └── PathService.cs                ← single-file pass-through in P1; fluent grouping deferred to P3+
```

**Rationale:**

- **BuildContext is a Cake/Frosting runtime concern, not a domain concern.** It belongs at the Host root — adjacent to Program.cs and CLI parsing, not under any domain heading.
- **Options vs Configuration split.** Options are operator-input surface (raw CLI parse). Configuration is derived/normalized state used at runtime. The current `Context/Options/` + `Context/Configs/` ambiguity becomes explicit.
- **CakeExtensions split by concern.** Three files (`Json`, `Platform`, `FileSystem`) prevent the "throw it in CakeExtensions.cs" kitchen-drawer pattern.
- **PathService stays at Host level (not Tools, not Shared, not Integrations).** Path layout describes repo and artifact topology, which is host runtime knowledge — not external infrastructure.

### 2.3 Features/

Each feature folder is one **operational vertical slice**. It owns:

- The Cake `Task` class (public adapter)
- A `Pipeline` class — only when Task body would exceed ~200 LOC; not mandatory (§2.4)
- Feature-specific request / result records
- Feature-specific validators, generators, reporters
- Feature-specific error types
- A `ServiceCollectionExtensions.cs` exposing `AddXFeature(this IServiceCollection)` (§2.12)

**Reference layout (Packaging, the largest feature):**

```
Features/Packaging/
├── PackageTask.cs                      ← Cake adapter
├── PackagePipeline.cs                  ← orchestration (was PackageTaskRunner, ~556 LOC)
├── PackageConsumerSmokeTask.cs
├── PackageConsumerSmokePipeline.cs     ← was PackageConsumerSmokeRunner (~688 LOC)
├── PackageOutputValidator.cs
├── NativePackageMetadata.cs            ← model + generator + validator co-located (no `new`-from-orchestrator anti-pattern)
├── ReadmeMappingTable.cs
├── G58CrossFamilyValidator.cs
├── SatelliteUpperBoundValidator.cs
├── FamilyTopologyHelpers.cs
├── ArtifactSourceResolvers/            ← Local / Remote (§2.15 retires Unsupported)
├── PackRequest.cs
├── PackagingError.cs
└── ServiceCollectionExtensions.cs      ← services.AddPackagingFeature()
```

**Reference layout (LocalDev, the only orchestration feature):**

```text
Features/LocalDev/
├── SetupLocalDevTask.cs                ← Cake adapter
├── SetupLocalDevFlow.cs                ← multi-feature compose (Preflight → Vcpkg → Harvest → ConsolidateHarvest → Package → ArtifactSourceResolver)
├── SetupLocalDevRequest.cs
└── ServiceCollectionExtensions.cs      ← services.AddLocalDevFeature()
```

`Features/LocalDev/` is the **designated orchestration feature** — the only feature folder that may reference sibling feature pipelines (see §2.13 invariant #4). It exists because `SetupLocalDev` semantically is not a packaging operation; it is local-developer-experience orchestration that terminates with feed preparation. Co-locating it inside `Features/Packaging/` would force `Packaging → {Preflight, Vcpkg, Harvesting}` cross-feature dependencies that violate the architecture-test invariant.

**Feature roster (target migration map):**

| Feature folder | Holds |
|---|---|
| `Features/Info/` | InfoTask |
| `Features/Maintenance/` | CleanArtifactsTask, CompileSolutionTask |
| `Features/Ci/` | GenerateMatrixTask |
| `Features/Versioning/` | ResolveVersionsTask + Manifest/GitTag/Explicit version providers |
| `Features/Preflight/` | PreflightTask + PreflightReporter + per-guardrail validators (G14/G15/G54/G58, etc.) |
| `Features/Vcpkg/` | EnsureVcpkgDependenciesTask |
| `Features/Harvesting/` | HarvestTask + HarvestPipeline + BinaryClosureWalker + ArtifactPlanner + ArtifactDeployer + NativeSmokeTask + ConsolidateHarvestTask |
| `Features/Packaging/` | PackageTask + PackagePipeline + PackageConsumerSmokeTask + PackageConsumerSmokePipeline + post-pack validators + native-package metadata generators + Local/Remote ArtifactSourceResolvers |
| `Features/LocalDev/` | SetupLocalDevTask + SetupLocalDevFlow — **designated orchestration feature** (§2.13 rule #4 allowlist); the only feature permitted to reference sibling feature pipelines |
| `Features/Publishing/` | PublishStagingTask + PublishPublicTask |
| `Features/Diagnostics/` | InspectHarvestedDependenciesTask |
| `Features/DependencyAnalysis/` | OtoolAnalyzeTask + DependentsTask + LddTask |
| `Features/Coverage/` | CoverageCheckTask + CoverageThresholdValidator |

### 2.4 Pipeline class — size-triggered, never convention-triggered

`Pipeline` classes are **extracted by readability, with size as a smell threshold rather than a hard rule.** The default progression:

| Task body size | Default shape |
|---|---|
| 0–50 LOC | Logic in `Task.RunAsync` directly |
| 50–200 LOC | Private methods inside the Task |
| 200+ LOC | Extract to `XPipeline.cs` in the same feature folder; Task delegates |

**The size column is a smell threshold, not a hard rule.** A 250-line linear pipeline that reads top-to-bottom as a single concern can stay in the Task. A 120-line orchestration that interleaves three concerns might already deserve a Pipeline. The author defends the choice; the threshold flags when the burden of defense rises.

**Pipeline ≠ TaskRunner.** A Pipeline:

- Has no `I*` interface unless §2.9 criteria justify it (most do not)
- Reads top-to-bottom as a named-step orchestration table, not as a free-form async method
- Lives in the same feature folder as its Task — never a cross-folder lookup
- Consumes a feature-specific `Request` DTO produced by the Task — see §2.11 for the BuildContext boundary rule

**Reference shape (target architecture):**

```csharp
[TaskName("Package")]
public sealed class PackageTask(
    PackagePipeline pipeline,
    PackageBuildConfiguration config) : AsyncFrostingTask<BuildContext>
{
    public override bool ShouldRun(BuildContext context)
        => config.ExplicitVersions.Count > 0;

    public override Task RunAsync(BuildContext context)
        => pipeline.RunAsync(PackRequest.From(context, config));
}

internal sealed record PackRequest(
    IReadOnlyDictionary<string, NuGetVersion> Versions,
    DirectoryPath RepoRoot,
    DirectoryPath PackagesOutput)
{
    public static PackRequest From(BuildContext context, PackageBuildConfiguration config)
        => new(config.ExplicitVersions, context.Paths.RepoRoot, context.Paths.PackagesOutput);
}

internal sealed class PackagePipeline(
    ICakeLog log,
    PackageOutputValidator outputValidator,
    NativePackageMetadataGenerator metadataGenerator
    /* ...other deps... */)
{
    public async Task RunAsync(PackRequest request, CancellationToken ct = default)
    {
        var families = ResolveSelectedFamilies(request);
        EnsureHarvestReady(request, families);
        await GenerateNativeMetadataAsync(request, families, ct);
        await PackNativeAsync(request, families, ct);
        await PackManagedAsync(request, families, ct);
        NormalizeDependencyRanges(request, families);
        ValidatePackageOutput(request, families);
    }
}
```

The Pipeline reads as orchestration; details live in named private methods or per-concern services within the same feature folder. **Note:** during P1/P2 migration, existing Cake-heavy pipelines may transitionally retain `RunAsync(BuildContext, TRequest, ...)` signatures while their internals are extracted (§2.11 migration exception). The reference shape above is the post-P4 target.

### 2.5 Flow class — multi-feature composition only

`Flow` classes are reserved for **multi-feature composition** — a single Cake Task that internally orchestrates pipelines belonging to **different** features.

The repo has exactly one such case today: `SetupLocalDev` runs `Preflight → EnsureVcpkg → Harvest → ConsolidateHarvest → Package → ArtifactSourceResolver`. Each step is a pipeline in a different feature.

| Convention | Detail |
|---|---|
| File name | `XFlow.cs` (e.g., `SetupLocalDevFlow.cs`) |
| Class | `internal sealed class XFlow` |
| Location | A **designated orchestration feature folder**. Today the only one is `Features/LocalDev/`. Sibling features must not cross-reference each other; orchestration features are the explicit allowlist exception in §2.13 invariant #4. |

**Adding a second orchestration feature is by exception, not convention.** If a new multi-feature compose surfaces (none anticipated today), the choice is between:

1. Folding it into `Features/LocalDev/` if the orchestration is local-developer-experience-shaped.
2. Adding a new orchestration feature folder and **explicitly extending the §2.13 invariant #4 allowlist** in the same wave. Implicit "this feature happens to call others" drift is the anti-pattern the rule is designed to prevent.

**A Pipeline that internally composes its own sub-steps without crossing feature boundaries stays a Pipeline.** Renaming a Pipeline to a Flow because it has many steps is the anti-pattern.

### 2.6 Shared/

`Shared/` is a **strict vocabulary layer**. It contains **no Cake dependencies**, no filesystem implementations, no process invocations, no external clients. The "no Cake" rule is an architecture-test invariant (§2.13 invariant #1) with a single bounded P1 migration exception for legacy `RuntimeProfile`-style types that transitionally carry Cake `PlatformFamily` references; that exception closes at P2.

```text
Shared/
├── Manifest/         ← ManifestConfig, RuntimeConfig, VcpkgManifest models
├── Runtime/          ← RuntimeProfile, RID/triplet vocabulary
├── Versions/         ← package version mapping types
├── PackageFamilies/  ← FamilyIdentifierConventions, family identity helpers, SmokeScopeComparator
├── Strategy/         ← HybridStaticStrategy, PureDynamicStrategy + IPackagingStrategy / IDependencyPolicyValidator (genuine multi-impl seams per §2.9); Strategy-local result types stay here, not in Shared/Results/
└── Results/          ← cross-feature result/error primitives only — see §2.6.1 admission criteria
```

**Allowed:**

- Pure value objects, records, enums
- Pure validators / comparators with no I/O
- Result / error types shared by 2+ features (subject to §2.6.1)
- Domain-level interfaces with multiple genuine implementations

**Not allowed:**

- Cake extensions (those live at `Host/Cake/`)
- Path layout types (those live at `Host/Paths/`)
- CLI tool wrappers (those live at `Tools/`)
- Process execution (Tools or Integrations, depending on Cake-Tool fitness)
- HTTP / NuGet protocol clients (Integrations)
- Feature-specific validators (those live in their feature)
- `Utils/`, `Helpers/`, `Common/` buckets — banned

> **Rule of thumb:** If a type would land in `Shared/` only because two features happen to use it but it has external dependencies, promote it to `Tools/` or `Integrations/` instead. Shared is vocabulary, not plumbing.

#### 2.6.1 Shared/Results — admission criteria

`Shared/Results/` is the highest-drift-risk folder under `Shared/`. Without explicit admission criteria, it becomes the new home of every `*Error` and `*Result` type — at which point `Domain/` has been recreated under a different name. The criteria below preserve vertical-slice cohesion.

A type lives in `Shared/Results/` only when **all three** hold:

1. **Used by ≥2 features in production code.** Single-feature consumption disqualifies; the type lives in its feature folder. A second consumer must already exist — not "might exist later".
2. **Generic semantics.** `BuildError` is a vocabulary primitive with no feature-specific failure modes. `PackagingError`, `HarvestingError`, `PreflightError` carry feature-specific semantics — they don't qualify regardless of how many features import them. The shape of the type, not its consumer count, determines admission.
3. **No feature-specific extension surface.** `BuildResultExtensions.MatchOrThrow` is generic chaining glue. `PackageValidationResult.IsRedFlag()` is feature-specific extension surface — disqualified.

**Promotion rule (reactive, not proactive):** when a feature-local result type starts being consumed by a second feature, the choice is between:

- **Preferred:** introduce a new `Shared/` vocabulary primitive that both features map to/from. Each feature keeps its own error semantics; Shared carries only the common subset.
- **Acceptable:** generalize the type and promote it to `Shared/Results/`. Only when the semantics genuinely match across consumers.
- **Banned:** moving a feature-local type to `Shared/Results/` defensively "in case another feature might use it later". This is the path back to a horizontal layer.

**Anti-pattern (do not do this):** A new `*Error` or `*Result` type is added to `Shared/Results/` because the author "wasn't sure where it belongs." The default location is the feature folder. If genuinely unclear, leave it feature-local; promote later when a second consumer materializes.

**Banned bucket names inside `Shared/Results/`:** `Helpers`, `Utils`, `Common`, `Misc`, `Generic`. If a result primitive is hard to name, it doesn't belong in Shared.

**Strategy domain exception.** `Shared/Strategy/` carries its own result types (`StrategyResolutionResult`, `ValidationResult`, `ValidationError`) inside the Strategy sub-folder — these are vocabulary primitives that belong with the Strategy interfaces and implementations, not in `Shared/Results/`. The pattern: **a Shared sub-domain may co-locate its own result types when those types are vocabulary primitives consumed by that sub-domain's interfaces.** This is not a license to scatter results across `Shared/`; it is a single structured exception for genuine multi-impl seams.

**Today's expected admissions to `Shared/Results/`:** `BuildError`, `BuildResultExtensions`, plus async chaining helpers if added. Everything else stays feature-local. Specifically: `PackagingError`, `HarvestingError`, `PreflightError`, `CoverageError`, all `*ValidationResult` and feature-specific `*Result` types live in their respective `Features/<X>/` folders.

### 2.7 Tools/

`Tools/` contains **Cake `Tool<TSettings>` wrappers only**. The Cake Frosting filename convention is preserved verbatim: `XTool.cs` + `XAliases.cs` + `XSettings.cs` + optional `XRunner.cs` (Cake-native multi-command tool name — exception to the build-host runner ban in §2.10).

```
Tools/
├── Vcpkg/
│   ├── VcpkgTool.cs
│   ├── VcpkgInstallTool.cs
│   ├── VcpkgPackageInfoTool.cs
│   ├── VcpkgBootstrapTool.cs
│   ├── VcpkgAliases.cs
│   └── Settings/
│       ├── VcpkgSettings.cs
│       ├── VcpkgInstallSettings.cs
│       └── VcpkgPackageInfoSettings.cs
├── Dumpbin/
├── Ldd/
├── Otool/
├── Tar/
├── NativeSmoke/
└── CMake/
```

**Strict rule.** A type that does not inherit `Cake.Core.Tooling.Tool<TSettings>` does not belong here. NuGet protocol clients, XML readers, dotnet pack CLI invokers, and process-based environment resolvers (e.g., `MsvcDevEnvironment` calling `vcvarsall.bat`) go to `Integrations/`.

### 2.8 Integrations/

`Integrations/` contains **non-Cake-Tool external adapters** — typed wrappers around external systems that do not fit the Cake `Tool<T>` model.

```
Integrations/
├── DotNet/
│   ├── DotNetPackInvoker.cs            ← wraps `dotnet pack` via process invocation, but isn't a Cake Tool<T>
│   ├── ProjectMetadataReader.cs        ← MSBuild evaluation
│   └── DotNetRuntimeEnvironment.cs     ← runtime install resolver for win-x86 child-test bootstrap
├── NuGet/
│   └── NuGetProtocolFeedClient.cs      ← HTTP-level NuGet protocol client
├── Vcpkg/
│   ├── VcpkgCliProvider.cs             ← non-Tool process invoker backing IPackageInfoProvider
│   └── VcpkgManifestReader.cs          ← JSON manifest reader
├── Coverage/
│   ├── CoberturaReader.cs              ← Cobertura XML parser
│   └── CoverageBaselineReader.cs       ← baseline JSON reader
└── Msvc/
    └── MsvcDevEnvironment.cs           ← invokes vcvarsall.bat to populate MSVC env vars; not a Cake Tool<T>
```

**Naming distinction.** `Tools/Vcpkg/VcpkgTool.cs` is the Cake-native CLI wrapper consumed via Cake aliases. `Integrations/Vcpkg/VcpkgCliProvider.cs` is the non-Cake process invoker that exposes typed package info to features. Both can coexist for the same external system; the distinction is the integration model, not the system.

### 2.9 Interface discipline

ADR-002 §2.3's three-criteria rule is preserved with one refinement: **test mock substitution is supporting evidence only, never the sole justification.**

An interface earns its existence only if it satisfies one of:

1. **Multiple production implementations exist today.** Polymorphic dispatch by profile, platform, or strategy. Examples: `IRuntimeScanner` (3 OS impls), `IPackagingStrategy` (2 strategy impls), `IDependencyPolicyValidator` (2 strategy impls), `IArtifactSourceResolver` (Local / Remote profiles per ADR-001 §2.7).

2. **The interface formalizes an independent axis of change.** Even with one implementation today, the contract is part of an architectural seam recognized by the project. The reviewer must be able to state the axis in one sentence (e.g., "feed protocol could swap from NuGet HTTP to local filesystem"). Speculation does not qualify.

3. **High-cost test seam (transitional).** An existing single-implementation interface that backs many test mock substitutions where rewriting tests would dwarf the migration scope. **This is transitional debt** — the interface stays for the migration but is a P3 review target.

**Migration discipline:** interfaces are **not removed during structural migration (P1/P2)**. They are review targets in a dedicated wave (P3). Removal proceeds only when test rewrite cost is bounded.

**Probable P3 review targets** (no decision today):

- `IBinaryClosureWalker`, `IArtifactPlanner`, `IArtifactDeployer` — single-impl, real test mock seams (~6 callsites each)
- `IPackageTaskRunner`, `IPackageConsumerSmokeRunner` — single-impl, real test mock seams + composition-root smoke tests
- `IPackageVersionProvider` — single direct-impl after ADR-003 (`ExplicitVersionProvider`); the other two providers (`Manifest`, `GitTag`) are constructed inline from `ResolveVersionsTaskRunner`. Criterion-2 case is borderline.

**Probable retention candidates** (genuine §2.9 seams):

- `IRuntimeScanner` (criterion 1: 3 OS impls)
- `IPackagingStrategy`, `IDependencyPolicyValidator` (criterion 1: 2 impls each)
- `IArtifactSourceResolver` (criterion 1: 2 active profiles + ADR-001 §2.7 ReleasePublic landing in Phase 2b)
- `INuGetFeedClient`, `IDotNetPackInvoker`, `IProjectMetadataReader`, `IVcpkgManifestReader`, `IMsvcDevEnvironment` (criterion 2: external boundary contracts)

#### 2.9.1 Delegate-hook pattern for non-mockable third-party boundaries

(Preserved verbatim from ADR-002 §2.3.1, 2026-04-25.)

Some third-party Cake addins bypass `ICakeContext.FileSystem` entirely — `Cake.Frosting.Git` is the canonical example: its aliases reach LibGit2Sharp's native binary, which calls `System.IO.Directory.Exists` directly and ignores `FakeFileSystem`-backed test fixtures. Wrapping these surfaces behind a one-call interface fails §2.9 criterion 1 (no second implementation) and criterion 2 (no independent axis of change — the seam exists only to substitute a non-mockable native call).

A constrained alternative is permitted: an **optional ctor-injected delegate parameter** with a default that wraps the production alias.

```csharp
public sealed class PackagePipeline(
    /* ...other deps... */,
    Func<ICakeContext, DirectoryPath, string>? resolveHeadCommitSha = null)
{
    private readonly Func<ICakeContext, DirectoryPath, string> _resolveHeadCommitSha
        = resolveHeadCommitSha ?? DefaultResolveHeadCommitSha;
}
```

Permitted under the following invariants:

1. **Default delegate exercises the production path** unconditionally — no mode flag, no env-var sniff. Production callers never pass the parameter.
2. **Optional parameter only.** The seam stays invisible to consumers who do not need substitution; it does not enter the public contract.
3. **Bounded to the call site.** The pattern does not propagate beyond the specific non-mockable surface — sibling code uses interfaces (criteria 1/2) or fixture-backed integration tests (e.g., `Repository.Init()` in `Path.GetTempPath()` for a real ephemeral git repo) when the third-party surface admits them.
4. **Documented in the consumer.** A comment at the delegate parameter site explains why the standard interface route is unavailable and points at the third-party constraint.

**Counter-rule:** if a second non-mockable boundary surfaces and the delegate-hook pattern starts looking like a habit (≥3 callsites, or the helpers cluster around a recognizable seam), promote to a real interface or a `Build.Tests/Fixtures/` extension that handles the substitute fully on the test side. The delegate-hook pattern is a contained workaround, not a precedent.

### 2.10 Terminology lock

| Term | Meaning | Scope |
|---|---|---|
| **Task** | Cake Frosting public target adapter (`[TaskName]`-decorated `FrostingTask<BuildContext>` subclass) | Build-host vocabulary |
| **Pipeline** | Large target implementation extracted from a Task body when it exceeds ~200 LOC | Build-host vocabulary |
| **Flow** | Multi-feature composition: one Cake Task that orchestrates pipelines from different features (only `SetupLocalDev` today) | Build-host vocabulary |
| **Tool** | Cake `Tool<TSettings>` wrapper around an external CLI | Cake-native (matches `Tools/`) |
| **Integration** | Non-Cake-Tool external system adapter | Build-host vocabulary (matches `Integrations/`) |
| **Feature** | Operational vertical slice owning one or more related Cake targets | Build-host vocabulary (matches `Features/`) |
| **Shared** | Build-domain vocabulary with no external dependencies | Build-host vocabulary (matches `Shared/`) |
| **Host** | Cake/Frosting runtime, CLI parsing, composition root, paths, Cake extensions | Build-host vocabulary (matches `Host/`) |
| **Runner** | **Banned for build-host concepts.** Allowed only inside the Cake `Tool<T>` filename triad as the canonical Cake-native multi-command tool name (e.g., `Tools/Vcpkg/VcpkgRunner.cs` if a multi-command vcpkg wrapper warrants it) | Cake-native exception only |
| **Adapter** | **Banned in folder names and class-name suffixes.** Hexagonal-architecture connotation. Use `Tool` / `Integration` / `Pipeline` / `Validator` / `Reader` / `Client` / `Provider` / `Generator` instead | Build-host vocabulary |
| **Service** | Reusable internal capability registered in DI; not a class-name suffix convention | Internal usage |

### 2.11 BuildContext discipline

`BuildContext` extends `Cake.Frosting.FrostingContext` and carries **invocation state and ambient Cake API only**. It is not a service locator.

**Allowed members:**

```csharp
public sealed class BuildContext : FrostingContext
{
    public BuildPaths Paths { get; }              // Host/Paths
    public RuntimeProfile Runtime { get; }        // Shared/Runtime
    public ManifestConfig Manifest { get; }       // Shared/Manifest (data only — no behavior)
    public BuildOptions Options { get; }          // Host/Configuration aggregate (Vcpkg, Package, Versioning, Repository, DotNet, Diagnostics)
}
```

**Forbidden:**

- `context.GetService<T>()`, `context.Services.GetRequiredService<T>()`
- Properties exposing pipelines, validators, tools, or integrations (`context.PackagePipeline`, `context.NuGetClient`)
- Methods that orchestrate behavior (`context.ExecutePackage()`)

**Rule of thumb:** BuildContext answers "what is this run's state?". DI answers "what capabilities exist?". Request objects answer "what does this target want?".

**ManifestConfig on BuildContext is data, not service.** `context.Manifest.Runtimes` is fine. `context.Manifest.ResolveConcreteFamilies()` is **not** — that behavior lives in `Shared/PackageFamilies/` extension methods or `Features/<X>/` helpers, not on the data carrier.

#### 2.11.1 BuildContext boundary rule

BuildContext is a powerful object — it carries Cake ambient API, paths, runtime profile, manifest, and options. Passing it deeper than necessary turns "no service locator" into "ambient everything," which is the same problem under a thinner disguise.

The boundary by layer:

| Layer | BuildContext access |
|---|---|
| **Cake `Task`** | Reads `BuildContext`. Translates state + normalized configuration into a feature-specific `Request` DTO and passes that Request to a Pipeline or Flow. |
| **Pipeline / Flow** | **Should** consume the `Request` directly: `RunAsync(TRequest request, CancellationToken ct = default)`. Should not receive `BuildContext`. **Migration exception (P1/P2):** existing Cake-heavy pipelines may transitionally retain `RunAsync(BuildContext, TRequest, ...)` during structural extraction. **A Pipeline that receives `BuildContext` must not pass it deeper** to validators, generators, or pure services. The exception closes at P4 close. |
| **Tools / Integrations** | May receive narrower Cake abstractions (`ICakeContext`, `ICakeLog`, `IFileSystem`) when required by Cake-native execution. **They never receive `BuildContext`.** A Cake `Tool<T>` wrapper that takes `ICakeContext` is fine; one that takes `BuildContext` is the leak. |
| **Pure services / validators / generators / planners / readers** | Receive **explicit inputs only** — no `BuildContext`, no `ICakeContext`. If a validator needs paths, it takes `BuildPaths` (or specific `FilePath`/`DirectoryPath` arguments). If it needs runtime info, it takes `RuntimeProfile`. The constructor signature reads as a precise capability shape, not as ambient state. |

#### 2.11.2 Request DTO conventions

Each public Cake target defines its own `Request` record. **No generic `BuildRequest` aggregate** — that is `BuildContext` cosplay.

**Examples:**

```csharp
internal sealed record PackRequest(
    IReadOnlyDictionary<string, NuGetVersion> Versions,
    DirectoryPath RepoRoot,
    DirectoryPath PackagesOutput);

internal sealed record HarvestRequest(
    string Rid,
    string Triplet,
    IReadOnlyList<string> Libraries);

internal sealed record PackageConsumerSmokeRequest(
    string Rid,
    IReadOnlyDictionary<string, NuGetVersion> Versions,
    DirectoryPath FeedPath);

internal sealed record PreflightRequest(
    IReadOnlyDictionary<string, NuGetVersion> Versions);

internal sealed record SetupLocalDevRequest(
    string Source,
    string Rid,
    IReadOnlyList<string> Libraries);
```

**A Request carries:** target's explicit intent + CLI-derived normalized values + scope/version/rid/source values for this run + required input/output paths.

**A Request does not carry:** `ICakeContext`, `IServiceProvider`, pipeline / validator / tool instances, the entire `BuildOptions` "just in case", the entire `BuildContext`.

#### 2.11.3 Path consumption — Request vs BuildPaths

Two valid patterns, choose by intent:

- **Path is part of the target's invocation contract** → put it on the Request. Example: `PackageConsumerSmokeRequest.FeedPath` — the smoke target's intent depends on which feed it is reading from.
- **Path is generic repo/artifact layout knowledge** → inject `BuildPaths` (or a sub-grouped path service after P4) into the Pipeline / service. Example: `paths.GetHarvestLibraryManifestFile(library, rid)` — repo layout, not invocation intent.

The dividing line: if changing the path changes what the target is doing, it is intent (Request). If the path is an implementation detail of how the target reads its own outputs, it is layout (BuildPaths).

### 2.12 DI registration shape — one extension method per feature

Each feature exposes its services via an extension method on `IServiceCollection`:

```csharp
// Features/Packaging/ServiceCollectionExtensions.cs
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPackagingFeature(this IServiceCollection services)
    {
        services.AddSingleton<PackagePipeline>();
        services.AddSingleton<PackageConsumerSmokePipeline>();
        services.AddSingleton<PackageOutputValidator>();
        services.AddSingleton<NativePackageMetadataGenerator>();
        services.AddSingleton<ReadmeMappingTableGenerator>();
        services.AddSingleton<G58CrossFamilyValidator>();
        services.AddSingleton<LocalArtifactSourceResolver>();
        services.AddSingleton<RemoteArtifactSourceResolver>();
        services.AddSingleton<ArtifactSourceResolverFactory>();
        // ...
        return services;
    }
}
```

`Host/Composition/CompositionRoot.cs` (or directly `Program.cs` while small) chains feature registrations:

```csharp
services
    .AddHostBuildingBlocks()
    .AddSharedVocabulary()
    .AddToolWrappers()
    .AddIntegrations()
    .AddInfoFeature()
    .AddVersioningFeature()
    .AddPreflightFeature()
    .AddVcpkgFeature()
    .AddHarvestingFeature()
    .AddPackagingFeature()
    .AddPublishingFeature()
    .AddDiagnosticsFeature()
    .AddDependencyAnalysisFeature()
    .AddCoverageFeature()
    .AddLocalDevFeature();        // registered last — depends on sibling feature pipelines (§2.13 rule #4 allowlist)
```

The chain doubles as an architectural index — every feature folder has a corresponding `Add*Feature` line.

**Rules:**

- DI registers **capabilities**, not CLI options. Per-run state (parsed CLI arguments) flows through `BuildContext.Options`, not DI.
- **No reflection-based assembly scanning.** Explicit registration only.
- `BuildOptions` aggregate is built at composition time from parsed CLI args and registered as a singleton.

### 2.13 Architecture tests — direction-of-dependency invariants

`LayerDependencyTests.cs` is renamed via `git mv` to `ArchitectureTests.cs` and its invariants are rewritten **in the same P2 wave**. P1 carries the legacy invariants unchanged; P2 lands the new shape with file rename and rule rewrite atomically (no parallel test files, no P5 tail).

The new file lives at `build/_build.Tests/Unit/CompositionRoot/ArchitectureTests.cs`. Invariants:

1. **Shared has no outward dependencies on the build host or on Cake.** `Build.Shared.*` may reference pure-domain libraries only (`NuGet.Versioning`, `OneOf`, etc.) — not `Cake.*` framework types, not `Build.Host.*`, not `Build.Features.*`, not `Build.Tools.*`, not `Build.Integrations.*`. **Migration exception (P1 only):** existing `Shared/Runtime/RuntimeProfile` and similar types that transitionally hold Cake `PlatformFamily` references are tolerated until the P2 wave replaces them with build-host-local enums or vocabulary types. The exception closes at P2 close — `ArchitectureTests` invariant #1 enforces no-Cake from that point.
2. **Tools have no Feature dependencies.** `Build.Tools.*` may depend on Cake framework + `Build.Shared.*` only.
3. **Integrations have no Feature dependencies.** `Build.Integrations.*` may depend on Cake framework + `Build.Shared.*` only.
4. **Features do not cross-reference each other in code, except from designated orchestration features.** `Build.Features.X.*` may not reference types in `Build.Features.Y.*`. Cross-feature data sharing flows through `Build.Shared.*` (e.g., `Shared.Manifest.HarvestManifest` consumed by `Features/Packaging/`). **Orchestration-feature exception:** `Build.Features.LocalDev.*` may reference sibling feature pipelines and tasks for the express purpose of multi-feature composition. The allowlist is **explicit and singular** — today only `LocalDev` qualifies. Adding a second orchestration feature requires extending this rule with the same explicit allowlist semantics in the same wave; implicit "this feature happens to call others" drift is the anti-pattern this rule blocks.
5. **Host is free.** `Build.Host.*` may reference any layer (it is the composition site — Program.cs, CompositionRoot, BuildContext bind everything).

Violations fail the test suite, gating commit/CI runs at the same boundary `LayerDependencyTests` did.

### 2.14 Naming cleanup

Cake target names normalize to plain PascalCase. **Rename criterion:** a target is renamed only when its current shape violates plain PascalCase — hyphen-separated segments (`Coverage-Check`, `Inspect-HarvestedDependencies`) or inner mixed-case where an internal letter is unexpectedly capitalized (`PreFlightCheck`'s inner `F`) are the only triggers. PascalCase names with semantic prefixes (`PackageConsumerSmoke`, `EnsureVcpkgDependencies`, `ConsolidateHarvest`, `ResolveVersions`, `NativeSmoke`, `OtoolAnalyze`, `Dependents`, `Ldd`, `GenerateMatrix`, `SetupLocalDev`, `PublishStaging`, `PublishPublic`, `Harvest`, `Package`, `Info`, `CleanArtifacts`, `CompileSolution`) stay because the prefix carries contextual scope and the shape is already well-formed. The criterion is mechanical, not aesthetic — debating whether `PackageConsumerSmoke` *could* read better is out of scope; the rule answers only "does the current shape parse as plain PascalCase?".

| Old | New |
|---|---|
| `PreFlightCheck` | `Preflight` |
| `Coverage-Check` | `CoverageCheck` |
| `Inspect-HarvestedDependencies` | `InspectHarvestedDependencies` |
| `PackageConsumerSmoke` | **Unchanged** — `Package` prefix preserves contextual scope ("which consumer? package consumer") and avoids breaking smoke-witness + release.yml callers without semantic gain |

**No backwards-compatibility aliases.** `release.yml`, `smoke-witness.cs`, and `tests/scripts/*.cs` update atomically with the Cake target rename **in the same migration wave commit**. Splitting into separate commits would break CI between commits — both old and new target names would be wrong simultaneously across a non-atomic rename.

### 2.15 ArtifactSourceResolver scope narrowing

`UnsupportedArtifactSourceResolver` (currently maps `release` / `release-public` to runtime failure) is retired. The CLI `--source` option accepts only `local` and `remote` until Phase 2b PD-7 lands public-feed promotion. Out-of-range values fail at CLI parse, not at resolver invocation. When `release` lands, the resolver returns to ADR-001 §2.7 shape.

---

## 3. Rationale

### 3.1 Why vertical slices over horizontal layers

- **One folder reads one operation.** `Features/Packaging/` contains the Cake target, the orchestration, the validators, the generators, and the DI wiring. No three-layer traversal to read package behavior.
- **Add a satellite, edit one feature folder.** Phase 3 adds SDL2_net. Under ADR-002 the scope touched `Domain/Packaging/`, `Application/Packaging/`, `Infrastructure/Tools/`, plus four sub-folders inside `Domain/`. Under ADR-004 it touches `Features/Packaging/` (manifest entry, family ID validator if needed) and `vcpkg.json`. Done.
- **Diff scope communicates intent.** A PR touching only `Features/Harvesting/` is a harvest change. A PR touching `Shared/Manifest/` is a contract change. A PR touching `Tools/` is a CLI wrapper change.

### 3.2 Why "Tools" + "Integrations" instead of "Adapters"

Hexagonal terminology imports a layered abstraction model the build host does not need. `Tools` matches Cake's own vocabulary (`Tool<TSettings>`) and is precise (only `Tool<T>` wrappers); `Integrations` describes external system access without committing to ports-and-adapters semantics. Two narrow buckets are clearer than one broad `Adapters` bucket that drifts.

The 2026-05-01 second-reviewer critique was decisive on this point: renaming `Infrastructure` to `Adapters` would solve the file-count symptom while keeping the conceptual cosplay. Splitting on Cake-Tool fitness keeps the discipline visible.

### 3.3 Why Pipeline is size-triggered

Mandatory `Step` (or `Pipeline`) extraction recreates the Task+TaskRunner duality under a new name. A 30-line `CleanArtifactsTask` does not need a `CleanArtifactsPipeline`. A 600-line `PackageConsumerSmokeTask` does. The size trigger is the only honest signal of whether extraction adds value.

### 3.4 Why interface review is deferred to P3

P1/P2 (folder migration + Pipeline rename + ServiceCollectionExtensions per feature) is **structural**. P3 (interface pruning per §2.9) is **API-shape**. Mixing them in one wave produces unreviewable diffs and inflated test rewrite scope.

P1/P2 runs with green tests using existing seams (mock-based interaction tests survive). P3 evaluates each surviving `I*` against §2.9 criteria with bounded test-rewrite scope per item.

### 3.5 Why PathService split is also deferred

The current `IPathService` has 50+ members consumed at hundreds of callsites. Splitting into `BuildPaths.Harvest`, `.Packages`, `.Smoke` etc. is desirable but is an **API refactor** on top of a folder refactor. Done together, they produce one giant unreviewable diff. Done separately, each is bounded.

### 3.6 Why "Features" prefix folder instead of flat top-level operation folders

A flat top-level (`_build/Harvest/`, `_build/Package/`, `_build/Tools/`, `_build/Host/`, `_build/Shared/`) would mean 16+ peer folders. The `Features/` prefix preserves a clear infrastructure-vs-operation visual boundary at the top level: 5 top-level folders, with operations grouped under `Features/`. The `feature` term clashes superficially with vcpkg's `features[]` codec selectors, but the contexts are unambiguous (manifest field vs build-host directory).

---

## 4. Consequences

### 4.1 Positive

- New contributors locate target behavior in one folder.
- Phase 2b additions (PublishStaging hardening, RemoteArtifactSourceResolver tail, family/train orchestration) land in feature slots, not in a growing layered tree.
- Cross-feature data contracts force `Shared/` discipline (architecture test #4 in §2.13).
- `Tools/` vs `Integrations/` split prevents Cake-tool conventions from drifting into HTTP/protocol clients.
- BuildContext discipline closes the service-locator drift door before it opens.
- DI composition reads as feature roster — `services.Add*Feature()` chain doubles as architectural index.
- Sub-validator co-location (`NativePackageMetadata.cs` model + generator + validator together) closes the ADR-002 §1.1.3 anti-pattern that horizontal layering did not solve.

### 4.2 Negative / trade-offs accepted

- **Migration churn.** `git mv` preserves blame but folder reorganization disrupts cross-PR reviews during the migration window.
- **Test constructor rewiring.** Each Pipeline rename updates its Task's test fixture and any direct test references. Mitigation: per-feature waves with green tests at every wave boundary.
- **Architecture test rewrite.** `LayerDependencyTests` invariants become `ArchitectureTests` invariants. The catchnet purpose persists; the rules change shape.
- **Mid-migration mixed shape.** Between waves, the repo carries half-old / half-new layout. Mitigation: each wave is self-contained; no half-migrated commits.
- **`feature` term clash with vcpkg codec selectors.** Accepted — context disambiguates (manifest JSON field vs build-host directory).

### 4.3 Out of scope (preserved unchanged)

- Cake target dependency mapping (`IsDependentOn`/`IsDependeeOf`) — semantics unchanged.
- ADR-001 D-3seg versioning, package-first consumer contract, ArtifactProfile semantics.
- ADR-003 release lifecycle invariants: provider/scope/version axes, stage-owned validation, matrix re-entry, G54/G58 placement. ADR-003's internal-layout references shift from ADR-002's `Application/Domain/Infrastructure` to ADR-004's `Features/Shared/Tools/Integrations/Host` — content unchanged, location updated.
- `manifest.json` schema v2.1 — no changes.
- `release.yml` 10-job topology — no changes other than target-name updates from §2.14.
- `smoke-witness.cs` — behavior lock; updated only for renamed target names.
- Pack guardrails (G14/G15/G16/G46/G54/G58) — preserved verbatim, relocated to `Features/Preflight/` and `Features/Packaging/`.

---

## 5. Non-goals

This ADR does NOT:

- Reduce the public Cake target surface or change CLI semantics (other than `--source` narrowing in §2.15).
- Change `manifest.json` contract or guardrail G-numbering.
- Introduce a Roslyn analyzer to enforce architecture rules — `ArchitectureTests` covers the same ground at lower cost.
- Move tests outside `build/_build.Tests/`.
- Change `external/sdl2-cs` or any submodule.
- Reopen ADR-001 or ADR-003 invariants.
- Delete the `LayerDependencyTests.cs` history. The file is renamed via `git mv` to `ArchitectureTests.cs` inside the P2 wave (file rename + invariant rewrite atomic, see §2.13); blame is preserved.

---

## 6. Implementation phases

Implementation is governed by a separate refactor plan at [`docs/phases/phase-x-build-host-modernization-2026-05-02.md`](../phases/phase-x-build-host-modernization-2026-05-02.md). Summary:

| Phase | Scope | Risk | ADR section |
|---|---|---|---|
| **P0** | Safety baseline (smoke-witness known-good, target rename inventory, test count snapshot, LayerDependencyTests baseline, public target surface freeze) | None | — |
| **P1** | Folder migration: Host split, Features re-grouping, Tools/Integrations separation, Shared narrowing | Medium (file moves) | §2.1–§2.8 |
| **P2** | Terminology migration: `*TaskRunner` → `*Pipeline` rename, `ServiceCollectionExtensions` per feature, `BuildOptions` aggregate, `BuildContext` slimming, `LayerDependencyTests.cs` → `ArchitectureTests.cs` (file rename via `git mv` + invariant rewrite, atomic in-wave) | Medium (rename + DI shape) | §2.4, §2.5, §2.10, §2.11, §2.12, §2.13 |
| **P3** | Interface review wave: §2.9 criteria applied to surviving `I*` types, test rewrite scope-bounded | High (test impact) | §2.9 |
| **P4** | API-surface refactors (deferred): Pipeline `RunAsync(BuildContext, TRequest)` → `RunAsync(TRequest)` cut-over (closes §2.11.1 migration exception), PathService fluent split, large-Pipeline internal refactor (PackageConsumerSmokePipeline, HarvestPipeline) | Low–Medium (per-item) | §2.11 |
| **P5** | Naming cleanup tail + `UnsupportedArtifactSourceResolver` retirement | Low | §2.14, §2.15 |

P0 must complete before P1. P1 + P2 land per-feature with green tests at every wave boundary. P3 starts only after P2 closes — no interface pruning during structural migration.

---

## 7. References

### 7.1 Repo-internal

- [ADR-001 (D-3seg + Artifact Source Profile)](2026-04-18-versioning-d3seg.md) — external contracts unaffected.
- [ADR-002 (DDD Layered Architecture)](2026-04-19-ddd-layering-build-host.md) — superseded by this ADR.
- [ADR-003 (Release Lifecycle Orchestration)](2026-04-20-release-lifecycle-orchestration.md) — invariants unchanged; internal-layout references updated to ADR-004 shape.
- [`docs/reviews/code-review-conversation.txt`](../reviews/code-review-conversation.txt) — 2026-05-01 critique pass (first reviewer).
- [`docs/reviews/conversation-2.txt`](../reviews/conversation-2.txt) — 2026-05-01 critique extension (second reviewer + first reviewer's revision).
- `docs/phases/phase-x-build-host-modernization-2026-05-02.md` — refactor plan (next deliverable).

### 7.2 External / inspirational

- Vertical Slice Architecture (Jimmy Bogard) — feature-cohesion principle.
- Cake Frosting documentation — `Tool/Aliases/Settings/Runner` filename triad preserved at `Tools/`.

---

## 8. Change log

| Date | Change | Editor |
| --- | --- | --- |
| 2026-05-02 | Initial draft and adoption | Deniz İrgin + 2026-05-01 collaborative critique synthesis |
| 2026-05-02 | Same-day batch revision — pre-finalization comments folded in (`docs/reviews/mycomments.txt`): SetupLocalDev → `Features/LocalDev/` (§2.3, §2.5); architecture rule #4 orchestration-feature exception (§2.13); Pipeline LOC threshold reframed as smell signal (§2.4); `BuildContext` boundary rule sharpened — Pipelines target `RunAsync(TRequest)`, services take explicit inputs, Tools take narrow Cake abstractions (§2.11.1–§2.11.3); `Shared/Results` admission criteria + Strategy-domain co-location exception (§2.6.1); `LayerDependencyTests` rename consolidated to P2 wave (§2.13, §6); `Features/Common/` renamed to `Features/Info/`; `AddLocalDevFeature()` added to composition root chain (§2.12); `Shared/` no-Cake invariant strengthened with bounded P1 migration exception for legacy `RuntimeProfile` Cake `PlatformFamily` references (§2.6, §2.13); Request DTO conventions wording de-Turkified (§2.11.2); `MsvcDevEnvironment` relocated from `Tools/` to `Integrations/Msvc/` — not a Cake `Tool<T>`, calls `vcvarsall.bat` (§2.7, §2.8); atomic-wave commit clarification (§2.14); `§1.2` motto added | Deniz İrgin |
| 2026-05-02 | P0-kickoff session refinement: §2.14 rename criterion paragraph (plain-PascalCase trigger rule — hyphen segments + inner mixed-case as the only renames; PascalCase names with semantic prefixes retained) | Deniz İrgin (+ P0-kickoff session refactor) |
| 2026-05-02 | P0 + P1 + P2 migration waves closed on `master` (commits `b18002f`, `651ac2f`, `e602b6c`, `b6de515`, `3ab2e68`). The ADR-002 layered shape (`Tasks/Application/Domain/Infrastructure/Context`) is fully retired in production code; the ADR-004 5-folder shape (`Host/Features/Shared/Tools/Integrations`) is live with 13 feature folders, per-feature `ServiceCollectionExtensions` × 13, BuildContext slimmed to 4 properties (`Paths`, `Runtime`, `Manifest`, `Options`) per §2.11, BuildOptions aggregate per §2.11.1, `*TaskRunner` → `*Pipeline` rename per §2.10, `SetupLocalDevTaskRunner` → `SetupLocalDevFlow` per §2.5, and `LayerDependencyTests` → `ArchitectureTests` rewrite per §2.13 (5 invariants; 3 currently skipped with explicit P3 deadline tracking per [phase-x §14 Adım 13](../phases/phase-x-build-host-modernization-2026-05-02.md#14-ad%C4%B1m-13-post-p2-follow-up-wave)). `Shared/` no-Cake invariant closed — `Build.Shared.Runtime.RuntimeFamily` build-host-local enum replaces `Cake.Core.PlatformFamily`, `IRuntimeProfile.IsSystemFile(string)` replaces `(FilePath)` per the §2.6 P1 transitional exception closure rule. Pre-P3 gate: Adım 13 (post-P2 follow-up wave) closes the remaining cross-tier violations (BinaryClosure / HarvestManifest / Coverage&Packaging result types into `Shared/<X>/`) and lands the deferred `ServiceCollectionExtensions` smokes + `cake-build-architecture.md` doc rewrite. **No ADR-004 invariants change in this entry — only realisation status.** | Deniz İrgin (+ P2-close session sweep) |
