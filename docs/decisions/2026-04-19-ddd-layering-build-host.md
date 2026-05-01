# ADR-002: DDD Layered Architecture for the Cake Build-Host

- **Status:** Superseded by [ADR-004 (Cake-Native Feature-Oriented Build-Host Architecture)](2026-05-02-cake-native-feature-architecture.md) on 2026-05-02
- **Date:** 2026-04-19
- **Last reviewed:** 2026-04-30
- **Deciders:** Deniz İrgin (primary), collaborative synthesis during 2026-04-19 Packaging maintainability review
- **Supersedes:** No prior ADR
- **Orthogonal to:** [ADR-001: D-3seg Versioning](2026-04-18-versioning-d3seg.md) — that ADR locks external contracts (version strings, consumer contract, source profile interface); this ADR locked the internal architecture that enforced those contracts at adoption.

> **This ADR is no longer authoritative.** The internal build-host architecture is governed by [ADR-004](2026-05-02-cake-native-feature-architecture.md). The §2.3 three-criteria interface rule and the §2.3.1 delegate-hook amendment carry forward into ADR-004 §2.9 / §2.9.1 verbatim. The content below is preserved as the historical record of the layered shape that ran the build host between 2026-04-19 and the ADR-004 migration.

---

## Historical Implementation State (2026-04-19 → 2026-05-02)

The Cake build host followed the ADR-002 shape: `Tasks/`, `Application/`, `Domain/`, `Infrastructure/`, and `Context/` were the live top-level boundaries under `build/_build/`. `Infrastructure/Tools/` hosted the repo-authored Cake `Tool<T>` wrappers, and `build/_build.Tests/Unit/CompositionRoot/LayerDependencyTests.cs` enforced the layer direction rules.

The target pattern was also live: task classes were thin Cake adapters and Application-layer runners owned orchestration. Some interfaces remained for practical seams or existing test structure; new interfaces had to satisfy §2.3.

## 1. Context

### 1.1 What was broken

A 2026-04-19 maintainability audit of `build/_build/Modules/Packaging/` surfaced structural drift that post-ADR-001 delivery accelerated:

1. **Packaging module file count exploded to 34** against Harvesting's 19. The driver was not domain complexity alone; each new concern (G54–G57 validators, `janset-native-metadata.json` generator, README mapping table generator, three `IArtifactSourceResolver` profiles) landed as its own `ISomething` + `Something` + `SomethingValidator` + `Models/Something.cs` quartet.
2. **Interface bloat was cargo-cult, not seam discipline.** At least seven single-method interfaces in Packaging (`IPackageVersionResolver`, `IDotNetPackInvoker`, `IProjectMetadataReader`, `IPackageFamilySelector`, `INativePackageMetadataGenerator`, `IReadmeMappingTableGenerator`, `IPackageConsumerSmokeRunner`) had exactly one implementation and zero mocks in tests. They existed because "DI needs an interface" — not because of any replaceable seam. Contrast with Harvesting's `IBinaryClosureWalker` (5 test mocks) and `IArtifactDeployer` (5 mocks), which are genuine seams.
3. **Sub-validators were `new`-ed inside the orchestrator.** `PackageOutputValidator` (1,038 lines) composed `NativePackageMetadataValidator`, `ReadmeMappingTableValidator`, and `SatelliteUpperBoundValidator` by constructing them directly instead of receiving them via DI. This hybrid — "orchestrator with sub-validator interfaces but manual composition" — is the worst of both worlds: the abstraction tax is paid without the testability or substitutability benefit.
4. **Concerns from different architectural layers co-habited the same folder.** Domain models (`PackageVersion`, `NativePackageMetadata`, `ArtifactProfile`), use-case orchestrators (`PackageTaskRunner`, `LocalArtifactSourceResolver`), Cake CLI wrappers (`DotNetPackInvoker`), and I/O helpers (ZIP reading, JSON generation) all sat at the same directory depth with no shape signal.
5. **Cross-cutting infrastructure existed but was not formalized.** `IPathService` (memory-locked reuse pattern) and JSON helpers were present and correctly reused, but there was no **"Infrastructure layer"** shape communicating to contributors "this is where cross-module technical concerns live; reuse before you reinvent."
6. **Application-layer orchestration drift.** `LocalArtifactSourceResolver` injected `IPackageTaskRunner` and triggered `Pack` from inside feed preparation — a borderline violation of the steering checkpoint "do not orchestrate Cake task graph by DI-injecting task runners into one another." Cake dependency mapping, not DI chaining, should sequence `Harvest → ConsolidateHarvest → Package → SetupLocalDev`.

### 1.2 The strategic observation

Packaging is not pathologically over-designed. The domain genuinely carries six concerns (versioning, family orchestration, cross-referenced metadata, dependency contract enforcement, artifact source profile, consumer smoke). What is pathological is flattening those six concerns into one directory with no layer discipline. The shape made it cheap to add an interface and a file; it made it expensive to read the code or change behavior.

Harvesting's 19-file shape is frequently cited in this repo as the "golden reference." It is not golden because it is small — it is golden because its domain is small (walk dependencies → plan deployment → deploy artifacts). Demanding Packaging match Harvesting file-for-file is a category error. The correct ask is: **adopt the same clarity of layering at a scale proportional to the domain's actual complexity.**

### 1.3 Decision precedents reviewed

Before converging on layered DDD with the exceptions documented below:

| Option | Shape | Outcome |
|---|---|---|
| **Keep as-is** (flat `Modules/*`) | Every module is one directory; interfaces alongside implementations; models + orchestrators + infra mixed | Rejected: confirmed drift trajectory; Phase 2b will worsen it |
| **Per-module DDD** (`Modules/Packaging/Domain/`, `Modules/Packaging/Application/`, `Modules/Packaging/Infrastructure/`) | DDD inside each module | Rejected: duplicates infrastructure per module; defeats cross-module reuse of PathService-style helpers |
| **Top-level DDD with module sub-folders** (`Application/Packaging/`, `Domain/Packaging/`, `Infrastructure/Paths/`) | Layers are top-level; modules are sub-concerns inside each layer | **Accepted** |
| **Hex / onion** (ports & adapters with explicit ring numbering) | Domain → Application → Ports → Adapters | Rejected: overkill for a build-host CLI; the Tasks/ layer already functions as adapters to Cake, ports would add indirection without payoff |

---

## 2. Decision

### 2.1 Four-layer model

The `build/_build` project adopts a four-layer architecture:

```
build/_build/
├── Tasks/             ← Presentation (Cake-native; EXCEPTION, see §2.5)
├── Application/       ← Use-case orchestrators (TaskRunners, Resolvers, SmokeRunner)
├── Domain/            ← Models, value objects, domain services, result types
├── Infrastructure/    ← PathService, JSON IO, process wrappers, filesystem, ZIP, Tool<T> wrappers
├── Context/           ← BuildContext (Cake task boundary; locked by steering)
└── Program.cs         ← DI wiring + CLI options
```

Each non-exception layer is a top-level directory. Modules (Packaging / Harvesting / Preflight / Coverage / Strategy) appear as sub-folders **inside the layer they belong to**, not as peer top-level concepts.

### 2.2 Layer discipline rules

Enforced by code review and contributor convention. No roslyn analyzer attempted in this ADR.

1. **Domain**
   - No dependencies on `ICakeContext`, `IPathService`, `ILogger`, file system, process invocation, or any other outer-layer concern.
   - Contains: records, value objects, `OneOf`/Result wrappers, domain invariants expressed in code.
   - Pure: a Domain type must be constructable in a unit test with no IoC container, no `Mock<>`.

2. **Application**
   - May depend on Domain and Infrastructure.
   - May inject other Application-layer services (service-to-service composition is permitted and expected — that's how orchestrators compose).
   - Must NOT be directly reachable from another **Cake task class** via constructor injection; only Tasks construct Application orchestrators. This matches the original steering checkpoint ("do not orchestrate Cake task graph by DI-injecting task classes into another task") — the prohibition targets task-class-to-task-class injection, not service-to-service composition.
   - Use-case orchestration is the only responsibility: receive intent, coordinate Domain + Infrastructure, return a Result.

3. **Infrastructure**
   - External-system adapters: filesystem, process launch, JSON/YAML/XML IO, ZIP reading, HTTP, git shell-outs, Cake CLI wrappers.
   - **Reuse is mandatory.** If `IPathService` or an existing JSON helper covers the need, a new helper is not written. Expanding an existing Infrastructure type is preferred over introducing a module-specific duplicate.
   - May depend on nothing except Domain (to return domain types from adapters).

4. **Presentation (Tasks/)**
   - Exception layer: see §2.5.
   - Cake task classes. Preferred shape: depend on Application orchestrators for behavior and carry only DTO / result payloads across the boundary.
   - Direct Task → Domain / Infrastructure interface injection is temporarily tolerated by the current architecture catchnet for existing fat-task holdovers, but that allowance is transitional debt and must not be used as precedent for new work.
   - Cake dependency mapping (`IsDependentOn`, `IsDependeeOf`) lives here.

### 2.3 Interface discipline

An interface earns its existence only if it formalizes a real runtime seam. The primary qualifying criteria are below; test substitution is supporting evidence, not a standalone entitlement.

1. **Multiple implementations exist today** — polymorphic dispatch by profile, platform, or strategy. Examples: `IRuntimeScanner` (3 platform impls: Windows dumpbin, Linux ldd, macOS otool), `IDependencyPolicyValidator` (2 impls: HybridStatic, PureDynamic), `IArtifactSourceResolver` (3 profiles per ADR-001 §2.7).

2. **It is the boundary of an independent axis of change** — even with one implementation and no mocks today, the interface is load-bearing when the contract is part of the Ubiquitous Language commitment and the implementation is expected to evolve for reasons unrelated to its callers (e.g. an infrastructure adapter with a stable contract but a changing backing store; a domain-service seam where the contract formalizes an architectural boundary). The test here is narrative-level, not speculative: the seam must name a real, recognized axis on which the implementation will move without rippling into callers. "Someone might want to substitute this one day" does not qualify.

**Supporting evidence, not a criterion on its own:** tests mock it. Dedicated substitutes are useful evidence that a seam is already paying rent, but mockability alone does not justify introducing or retaining an interface. If a type exists only to make an interaction-style unit test convenient, prefer concrete construction, fixtures, or a coarser application boundary.

Criterion 2 is the guard against over-pruning. Some single-implementation types legitimately serve as architectural boundaries and should keep their interface even before a second implementation materializes. When applying criterion 2, the ADR-writer / reviewer must be able to state the axis of change in one sentence. Examples where criterion 2 alone could justify a seam: a future "package introspection" boundary that today has one impl (file-system-backed) but clearly belongs in a contract because a feed-backed impl is roadmapped; an `IArtifactSourceResolver` whose contract is explicitly tied to ADR-001 artifact source profiles.

Counter-examples under this rule typically include small packaging / preflight helper seams such as `IPackageVersionResolver`, `IDotNetPackInvoker`, `IProjectMetadataReader`, `IPackageFamilySelector`, `IPackageConsumerSmokeRunner`, `INativePackageMetadataGenerator`, `IReadmeMappingTableGenerator`, and one-implementation validator / reporter interfaces that exist mainly to support interaction-style task tests. If they still have one implementation, no strong axis of change, and are kept alive only because tests substitute them, they are review targets rather than precedent.

The `Modules/Contracts/` directory retires in Wave 4. Surviving interfaces live in the layer that owns them (typically Application, occasionally Domain for domain-service seams, Infrastructure for adapter contracts).

#### 2.3.1 Test hooks for non-mockable third-party boundaries (amendment, 2026-04-25)

Some third-party Cake addins bypass `ICakeContext.FileSystem` entirely — Cake.Frosting.Git is the canonical example: its aliases reach LibGit2Sharp's native binary, which calls `System.IO.Directory.Exists` directly and ignores `FakeFileSystem`-backed test fixtures. Wrapping these surfaces behind a one-call interface (e.g., `IGitCommandRunner` for a single `GitLogTip` invocation) fails §2.3 criterion 1 (no second implementation today) and criterion 2 (no independent axis of change — the seam exists only to substitute a non-mockable native call).

A constrained alternative is permitted: an **optional ctor-injected delegate parameter** with a default that wraps the production alias. Example shape:

```csharp
public sealed class PackageTaskRunner(
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
4. **Documented in the runner.** A comment at the delegate parameter site explains why the standard interface route is unavailable and points at the third-party constraint (e.g., "Cake.Frosting.Git / LibGit2Sharp bypasses ICakeContext.FileSystem; see ADR-002 §2.3.1").

Counter-rule: if a second non-mockable boundary surfaces and the delegate-hook pattern starts looking like a habit (≥3 callsites, or the helpers cluster around a recognizable seam), promote to a real interface or a `Build.Tests/Fixtures/` extension that handles the substitute fully on the test side. The delegate-hook pattern is a contained workaround, not a precedent.

### 2.4 Folder shape — concrete example (Packaging, Wave 1 target)

```
build/_build/
├── Application/
│   └── Packaging/
│       ├── PackageTaskRunner.cs
│       ├── PackageConsumerSmokeRunner.cs
│       ├── LocalArtifactSourceResolver.cs
│       ├── RemoteArtifactSourceResolver.cs
│       └── UnsupportedArtifactSourceResolver.cs      (ReleasePublic pending)
├── Domain/
│   └── Packaging/
│       ├── PackageVersion.cs
│       ├── PackageFamilySelection.cs
│       ├── PackageArtifacts.cs
│       ├── ProjectMetadata.cs
│       ├── ArtifactProfile.cs
│       ├── NativePackageMetadata.cs                  (model + generator + validator — §2.3 no interface)
│       ├── ReadmeMappingTable.cs                     (model + generator + validator)
│       ├── PackageValidation.cs
│       ├── PackageVersionResolver.cs                 (domain service, concrete)
│       ├── PackageFamilySelector.cs                  (domain service, concrete)
│       ├── PackageOutputValidator.cs                 (domain service, concrete; IPackageOutputValidator kept — real seam)
│       ├── SatelliteUpperBoundValidator.cs           (static helper)
│       └── Results/                                  (OneOf-style error + result types)
└── Infrastructure/
    ├── Paths/                                        (IPathService + impl hoisted from Modules/)
    ├── Json/                                         (ToJson / FromJson helpers)
    └── DotNet/
        └── DotNetPackInvoker.cs                      (Cake CLI wrapper)
```

Harvesting, Preflight, Coverage follow the same shape in later waves.

### 2.5 Tasks/ exception — Cake-native presentation stays flat

`build/_build/Tasks/` is deliberately not collapsed into `Presentation/Packaging/*Task.cs`. Reasons:

1. **Cake idiom.** Cake Frosting task classes are the public CLI surface. Contributors searching "where is target X implemented?" expect a flat `Tasks/` tree by convention.
2. **Dependency map readability.** `IsDependentOn(nameof(HarvestTask))` calls are easier to audit when tasks are co-located.
3. **Steering alignment.** Steering checkpoints #2 (no task-to-task DI) and #3 (Cake dependency mapping owns orchestration) treat Tasks/ as a first-class concept. Renaming it to Presentation/ would obscure that.

Tasks/ internal organization (by concern: `Harvest/`, `Packaging/`, `Preflight/`, etc.) is unchanged.

### 2.6 Tools/ placement — Cake-native wrappers are Infrastructure

`build/_build/Infrastructure/Tools/` contains Cake Frosting tool wrappers for external CLIs that Cake does not natively expose: vcpkg, dumpbin, ldd, otool. Each wrapper follows the canonical Cake Frosting convention (documented in [cake-frosting-build-expertise.md](../reference/cake-frosting-build-expertise.md) §3.1):

- `*Tool.cs` inherits `Cake.Core.Tooling.Tool<TSettings>` (tool resolution, argument building, process execution).
- `*Aliases.cs` provides `ICakeContext` extension methods decorated with `[CakeMethodAlias]` (the Cake DSL gateway).
- `*Settings.cs` inherits `Cake.Core.Tooling.ToolSettings` (typed configuration).
- Optional `*Runner.cs` for multi-command tools.

This IS Infrastructure in DDD terms — external-system adapters with typed configuration and a composition boundary. The `Tool / Aliases / Settings / Runner` filename convention is a Cake idiom and is preserved verbatim. Placement:

```
Infrastructure/
└── Tools/
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
    └── Otool/
```

Rationale:

- **Single home for external-system adapters.** `Infrastructure/Tools/` co-locates everything that is a typed wrapper around an external process. Cross-cutting helpers without a Cake Tool base (PathService, JSON IO) live alongside at `Infrastructure/Paths/`, `Infrastructure/Json/`.
- **Cake convention preserved.** The per-tool sub-folder (`Vcpkg/`, `Dumpbin/`, etc.) and the `Tool/Aliases/Settings/Runner` filename triad remain exactly as the Cake Frosting documentation describes. Contributors writing a new tool wrapper follow the pattern without learning a repo-local deviation.
- **DDD coherence.** "Tools" is a Cake naming convention, not a separate architectural layer. Inside DDD, this is `Infrastructure` — the adapter tier. The naming is preserved inside the layer, not exalted to peer status.

Cake built-in aliases (`DotNetPack`, `DotNetBuild`, `MSBuild`, `CleanDirectory`, etc.) that already ship in `Cake.Common` are consumed directly from Application-layer code via `ICakeContext`; they do not get re-wrapped. `Infrastructure/Tools/` is only for wrappers the repo authored itself.

### 2.7 Test project structure — mirrors production layering

`build/_build.Tests/` mirrors `build/_build/` at the folder level. The mirroring rule is unchanged from current practice; the ADR extends it to cover the new DDD layers.

```
build/_build.Tests/
├── Unit/                                (mirrors production structure)
│   ├── Tasks/                           (mirrors build/_build/Tasks/)
│   │   ├── Harvest/
│   │   ├── Packaging/
│   │   ├── Preflight/
│   │   └── Coverage/
│   ├── Application/                     (mirrors build/_build/Application/)
│   │   ├── Packaging/
│   │   ├── Harvesting/
│   │   ├── Preflight/
│   │   └── Coverage/
│   ├── Domain/                          (mirrors build/_build/Domain/)
│   │   ├── Packaging/
│   │   ├── Harvesting/
│   │   ├── Preflight/
│   │   ├── Coverage/
│   │   ├── Strategy/
│   │   └── RuntimeProfile/
│   ├── Infrastructure/                  (mirrors build/_build/Infrastructure/)
│   │   ├── Paths/
│   │   ├── Json/
│   │   ├── DotNet/
│   │   └── Tools/
│   │       ├── Vcpkg/
│   │       ├── Dumpbin/
│   │       ├── Ldd/
│   │       └── Otool/
│   ├── Context/                         (mirrors build/_build/Context/)
│   └── CompositionRoot/                 (mirrors build/_build/CompositionRoot/)
├── Integration/                         (scenario-based; populated as needed)
│   └── <scenario-folders>                 e.g. SetupLocalDev/, PackagingPipeline/, PreflightGate/
├── Characterization/                    (unchanged — contract snapshot tests)
│   └── ConfigContract/
└── Fixtures/                            (unchanged — shared test infrastructure)
    ├── Data/
    └── Seeders/
```

**Unit tests** mirror production folder by folder. A test file at `Unit/Domain/Packaging/PackageOutputValidatorTests.cs` asserts the contract of `Domain/Packaging/PackageOutputValidator.cs`. Navigation from test to code is mechanical.

**Integration tests** do not mirror production folders — they cross layer boundaries by nature. Integration tests are organized by **scenario** (the user-visible flow under test), not by module. Examples: `Integration/SetupLocalDev/` for the end-to-end `SetupLocalDev --source=local` flow; `Integration/PackagingPipeline/` for Harvest → ConsolidateHarvest → Package → Validate. No `Integration/` sub-folders are created speculatively; each arrives with the first test that justifies it.

**Characterization tests** (contract snapshots, e.g. manifest deserialization) remain at `Characterization/`. They are not unit tests (they cover serialization contracts), not integration tests (no cross-layer orchestration). The existing folder stays.

**Fixtures** (shared test builders, seeders, fake infrastructure) remain at `Fixtures/`. They are cross-cutting test infrastructure and do not mirror any production layer.

Wave-level convention: when a production file moves under DDD layering, its unit test file moves in the same commit. The mirror invariant is preserved at every commit boundary, never lagged.

### 2.8 Layer discipline enforcement — lightweight architecture tests

This ADR declines a Roslyn analyzer but mandates a small suite of namespace-level tests as a drift catchnet. A Roslyn analyzer is overkill for a four-layer internal build-host project; a handful of reflection-based tests catch 95% of realistic drift at <1% of the maintenance cost.

Three invariants hold at every commit from the close of Wave 1 onward:

1. **Domain has no outward dependencies.** No type in `Build.Domain.*` references `Build.Application.*`, `Build.Infrastructure.*`, `Build.Tasks.*`, or `Cake.*` (Domain purity rule from §2.2).
2. **Infrastructure does not reach into Application.** No type in `Build.Infrastructure.*` references `Build.Application.*` or `Build.Tasks.*`. Infrastructure may return Domain types but does not call use-cases.
3. **Target invariant: Tasks depend on Application for behavior; DTO references to Domain value objects are permitted.** No type in `Build.Tasks.*` references a Domain **service** at a layer root (e.g. `Build.Domain.Harvesting.HarvestJsonContract`) or an Infrastructure **service** at a layer root (e.g. `Build.Infrastructure.DotNet.DotNetPackInvoker`). Tasks MAY reference Domain and Infrastructure value objects / DTOs / result types living under `.Models.*` or `.Results.*` sub-namespaces (e.g. `Build.Domain.Harvesting.Models.DeploymentStatistics`), because those types are the data currency that flows through task output (Spectre rendering, JSON serialization, status-file generation, etc.). Behavior stays in its layer; value objects flow freely — this is canonical DDD. The exception for `Build.Context` (BuildContext binding at the task boundary) is unchanged.

   **Current enforcement note:** `LayerDependencyTests` is temporarily looser than the target invariant above. It still allows Task → Domain / Infrastructure interface references, plus `Infrastructure.Tools.*` Cake wrappers, so existing Harvest / PreFlight / dependency-analysis tasks can remain green while runner extraction and boundary cleanup continue. That broader allowance is transitional debt, not precedent for new work.

**Implementation location:** `build/_build.Tests/Unit/CompositionRoot/LayerDependencyTests.cs` (single file). Reflection over the loaded `Build` assembly using `Type.GetReferencedTypes()` pattern, or `NetArchTest.Rules` / `Mono.Cecil` if a package dependency is acceptable — the specific tooling is not locked by this ADR. TUnit (repo's existing test framework) carries the assertions.

**Failure mode:** a layer-rule violation fails the test suite, which gates any commit or CI run. Violations must be fixed before the commit lands, not triaged later.

**Scope guardrails:**

- These tests check **direction of dependency between layers**, not internal shape of each layer (no "no static classes," no "no public members"). Scope is narrow on purpose.
- Namespace patterns only; no attribute-based marking required on production types.
- The test file itself lives under `Unit/CompositionRoot/` because it tests the overall composition of the system, not any single module.

**When to run:** every `dotnet test build/_build.Tests` run. There is no separate CI job; the invariants ride alongside the rest of the unit suite.

**Sunsetting:** if and when the repo grows to warrant a Roslyn analyzer, the three invariants above define its rule set. The test file retires when the analyzer ships.

---

## 3. Rationale

### 3.1 Why layered DDD over flat modules

- **Readability proportional to domain size.** A reader looking at `Domain/Packaging/NativePackageMetadata.cs` learns more from the path than from a filename alone. The layer label answers "is this a model or an orchestrator?" without opening the file.
- **Change impact visibility.** A PR that touches only `Domain/*` is a pure domain change; touching `Infrastructure/*` implies an external-system contract change; touching `Application/*` is orchestration refactoring. Git diff scopes communicate intent.
- **Phase 2b headroom.** Stream D-ci (CI integration), PD-7 (full-train orchestration), PD-8 (manual escape hatch) will each add classes. Dropping them into the correct layer is mechanical; dropping them into flat `Modules/Packaging/` accelerates the drift documented in §1.1.

### 3.2 Why top-level layers, not per-module DDD

- **Infrastructure is cross-cutting.** `PathService` serves Harvesting, Packaging, Preflight, Coverage. Duplicating `Modules/*/Infrastructure/Paths/` would defeat the reuse that memory already locks ("Always use PathService").
- **Module boundaries are soft.** Packaging consumes Harvesting output (ConsolidatedHarvest). A shared Domain layer makes this a normal type reference across sub-folders, not a cross-module contract gymnastics exercise.

### 3.3 Why interface discipline (delete single-impl/no-mock interfaces)

- **Abstraction without substitutability is tax.** Navigation cost + mental overhead + DI wiring + file count all increase; zero-cost occurs only if a second implementation or a test mock eventually materializes.
- **Tests provide evidence, not entitlement.** A mock can confirm that a seam is paying rent, but an interface that exists only because an interaction-style unit test substitutes it is usually a sign that the caller boundary is too granular.
- **Surviving interfaces are self-documenting.** `IArtifactSourceResolver` surviving means "yes, 3 profiles are coming (ADR-001 §2.7)." `IRuntimeScanner` surviving means "yes, 3 platform implementations exist." The seam earns its cost.

### 3.4 Why Tasks/ exception

Consistency is a maintainability heuristic, not a law. Cake task files are the one place in this repo where framework convention wins over layer purity. Anyone reading `HarvestTask : FrostingTask<BuildContext>` is already in Cake-native space; the `Tasks/` folder reinforces that mental context.

---

## 4. Consequences

### 4.1 Positive

- Contributors can locate code by layer intent without guessing module internals.
- Interface bloom halts: reviewers can cite this ADR when asking "why does this new interface have one impl and no mocks?"
- Cross-module infrastructure reuse becomes the default path, not a memory-gated convention.
- Phase 2b additions land in named slots, not at the bottom of a growing flat folder.
- Domain logic is unit-testable without Cake / filesystem / process concerns (Domain purity rule).
- Application-layer orchestration rule blocks the `Resolver → Runner` DI anti-pattern structurally, not only by code review.

### 4.2 Negative / trade-offs accepted

- **Git blame churn.** File moves disrupt line-level blame. Mitigation: `git mv` preserves move detection at the file level; blame archaeology via `git log --follow` remains intact.
- **Test constructor rewiring.** Tests instantiating or mocking retired interfaces need updates. Mitigation: waves are scoped; test updates happen in the same commit as the production change.
- **Learning curve for new contributors.** A four-layer map is more to absorb than "everything lives in Modules/". Mitigation: `AGENTS.md` and `docs/onboarding.md` updates in Wave 1 closure.
- **Wave sequencing risk.** Between Wave 1 (Packaging) and Wave 2 (Harvesting), the repo has mixed-shape modules. Mitigation: each wave is self-contained, tests green per wave, no half-migrations committed.

---

## 5. Non-goals

This ADR does NOT:

- Introduce a Roslyn analyzer to enforce layer dependency direction (future consideration if drift re-emerges).
- Change Cake task names, dependency mapping, or CLI target surface.
- Change `manifest.json` contract, guardrail G-numbering, or any external-consumer-facing behavior.
- Define a new DI container or composition-root framework; existing `Microsoft.Extensions.DependencyInjection` usage continues.
- Move `build/_build.Tests/` test project layout (tests follow implementation structure naturally — mirror updates happen inside each wave).
- Touch `external/sdl2-cs` or any submodule.
- Reopen interface decisions for ADR-001-locked seams (`IArtifactSourceResolver`, `IPackagingStrategy`, etc.).

---

## 6. Implementation State

ADR-002 is implemented as the build-host baseline rather than an active wave plan.

- The four-layer folder split is live under `build/_build/`.
- `Infrastructure/Tools/` is the final home for repo-authored Cake `Tool<T>` wrappers.
- `Program.cs` is the composition root for DI and CLI option wiring.
- Packaging, Harvesting, Preflight, Coverage, Versioning, SetupLocalDev, Publishing, and smoke orchestration now live behind Application-layer runners or resolvers.
- `LayerDependencyTests` enforces Domain no outward dependencies, Infrastructure no Application dependencies, and the intended Task-layer shape.
- Harvest and ConsolidateHarvest runner extraction has landed; task classes should remain presentation adapters.

Remaining cleanup is case-by-case: single-implementation interfaces that survived previous refactors should be retained only when they satisfy §2.3.

---

## 7. References

### 7.1 Repo-internal

- [ADR-001: D-3seg Versioning](2026-04-18-versioning-d3seg.md) — locks external contracts enforced by this internal architecture.
- [release-lifecycle-direction.md](../knowledge-base/release-lifecycle-direction.md) — release governance context.
- [cake-build-architecture.md](../knowledge-base/cake-build-architecture.md) — current build-host architecture guide.
- [AGENTS.md](../../AGENTS.md) — contributor mental model.
- [harvesting-process.md](../knowledge-base/harvesting-process.md) — Harvest reference shape that informed Wave 2 target.
- [cake-frosting-build-expertise.md](../reference/cake-frosting-build-expertise.md) — Cake Frosting tooling conventions that locked §2.6 Tools/ placement decision.

### 7.2 External / inspirational

- Domain-Driven Design, Eric Evans (2003) — layered architecture chapter.
- Implementing Domain-Driven Design, Vaughn Vernon (2013) — bounded context + infrastructure chapter.
- Cake Frosting documentation — task lifecycle and dependency mapping semantics.

---

## 8. Change log

| Date | Change | Editor |
|---|---|---|
| 2026-04-19 | Initial draft and adoption | Deniz İrgin + LLM synthesis session |
