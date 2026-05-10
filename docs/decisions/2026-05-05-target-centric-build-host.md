# ADR-002: Target-Centric Cake Build Host Architecture

- **Status:** Accepted
- **Date:** 2026-05-05

## 1. Context

The Cake Frosting build host is the production pipeline for native harvesting, packaging, validation, consumer smoke testing, and publishing. It works, but the current shape has accumulated architecture that is harder to read than the build flow itself:

- Thin task classes often delegate immediately to large `*Pipeline` classes.
- Large pipeline classes mix orchestration, validation, reporting, file writes, and policy.
- `Host/Configuration` centralizes option parsing into configuration objects that are far away from the target that uses them.
- `Features`, `Shared`, and `Integrations` do not always map cleanly to how maintainers navigate the build: from a Cake target name in `tools.cs`, `release.yml`, or `--target`.
- Some abstractions exist because they were generated or anticipated, not because the current build domain needs them.

This ADR defines the target architecture for the next build-host refactor. It is intentionally a target-state decision: the migration may temporarily leave old and new shapes side by side, but each migrated target must move toward this model.

## 2. Decision

The build host will move to a **target-centric, Cake-native architecture**.

Cake targets are the primary navigation unit. The source layout, class names, and test scenarios should make it obvious how a command in `tools.cs`, `.github/workflows/release.yml`, or `dotnet run --project build/_build -- --target X` reaches the code that implements target `X`.

The refactor will proceed incrementally target by target. Mixed architecture is acceptable during the migration, but a migrated target should not merely rename folders. It should retire the local pipeline/configuration/interface/private-method smells that motivated the migration.

## 3. Vocabulary

| Term | Meaning |
| --- | --- |
| Cake target | The user-visible target name, for example `Package`, `Harvest`, or `PreFlightCheck`. |
| Task class | The Frosting task implementation, for example `PackageTask : AsyncFrostingTask<BuildContext>`. |
| Target module | The source folder for one Cake target, for example `Targets/Package/`. |
| Request | An immutable input contract built by a task class and passed to collaborators. It does not replace the Cake task method signature. |

Cake remains the host model. Task classes still use `RunAsync(BuildContext context)` or the equivalent Frosting entrypoint.

## 4. Target module layout

Executable targets live under `Targets/<CakeTargetName>/`.

Example:

```text
build/_build/
  Targets/
    Package/
      PackageTask.cs
      Requests/
        PackageRequest.cs
      Services/
        PackageFamilyPacker.cs
      Validation/
        HarvestReadinessValidator.cs
      Reporting/
        PackageReporter.cs
```

Subfolders are not mandatory. A small target may have only `<TargetName>Task.cs`. Introduce standard subfolders when a category has enough weight to improve navigation. As a practical trigger, the second file in the same category is usually enough reason to create the standard subfolder.

Preferred subfolder vocabulary:

- `Requests`
- `Services`
- `Validation`
- `Reporting`
- `Models`

Avoid creating empty symmetry. A target gets the folders it earns.

## 5. Task classes own orchestration

Task classes are allowed to be meaningful. They should:

- read the `BuildContext` properties they need;
- validate target-specific inputs at task entry;
- build a target-specific request for non-trivial executable target behavior when collaborators need an input contract;
- orchestrate the target's high-level flow;
- translate expected failures into Cake logging and `CakeException`.

Simple, no-op, default, marker, or fully-inline targets are exempt from request ceremony. Extraction is earned when code represents a named behavior, policy, algorithm, IO adapter, reusable concept, or independently testable unit.

The mandatory `*Pipeline` pattern is retired. Do not replace it with an equivalent generic wrapper such as `Runner`, `Operation`, or `Executor` unless the type is a real build-domain concept.

Do not split large work into internal Cake pseudo-targets solely to reduce task size. Cake targets should model user-visible lifecycle stages; internal steps should be named collaborators unless they are independently meaningful from `tools.cs`, CI, or the command line.

## 6. BuildContext and configuration

`BuildContext` is ambient invocation state, not a service locator.

It may expose:

- Cake/Frosting context needed by tasks and tools;
- repository/build paths;
- selected runtime information;
- readonly, named convenience properties for parsed CLI arguments.

It should not expose raw parsed argument dictionaries. Adding a new CLI option should require adding a named property, making the invocation surface discoverable.

`Host/Configuration` is retired as a pattern. The refactor should delete the aggregate configuration object and individual configuration classes. Each target reads and validates the options it needs at the task boundary.

File-backed source-of-truth state belongs behind named repositories, for example:

- `ManifestRepository`
- `VersionFileRepository`

These are practical file adapters, not a signal to turn the build host into a DDD application.

`FrostingLifetime<TContext>` is allowed only for true host lifecycle setup/teardown. It should not preload manifest, version, or other target-specific state by default; target state remains explicit at the task boundary.

## 7. Shared code policy

There is no catch-all `Shared` or `Common` dumping ground in the target architecture.

Cross-target code is elevated only when it represents a real named concept, such as:

- `Manifest`
- `Runtime`
- `Packaging`
- `Versioning`
- `Results`
- file-backed repositories

Do not create a shared mirror folder for every target folder. If code changes together with one target, keep it with that target until reuse is real.

Reuse means a second real consumer exists, or a second consumer is being created in the same migration slice or phase. Hypothetical future reuse is not enough.

`Integrations/` is not a default target-state layer. External adapters should be moved to the consuming target, a named cross-target concept, or `Tools/` when they are Cake `Tool<TSettings>` wrappers.

## 8. Interfaces and dependency injection

Constructor injection remains useful, but interfaces are not automatic.

Use an interface when at least one of these is true:

- there are multiple production implementations;
- the dependency represents an independent axis of change;
- the dependency is a high-cost or process/tool boundary that needs a stable test seam;
- the dependency is an important task collaborator whose contract is clearer than the concrete type.

Small pure helpers, simple policies, and one-off extracted classes should usually stay concrete. Avoid ceremonial `IFoo`/`Foo` pairs.

> **S12 amendment (2026-05-08), broadened in S13 (2026-05-09):** `Validation/` is an explicit exception. **All build-host validators under root `Validation/`** ship with `IFoo` interfaces and `AddSingleton<IFoo, Foo>()` registrations for uniform DI shape. Originally seven validators in S12 (manifest invariants, version consistency, core identity, csproj pack contract, upstream alignment, cross-family resolvability, hybrid-static overlay); expanded to eleven in S13 with `HarvestReadinessValidator`, `PackageOutputValidator`, `NativePackageMetadataValidator`, and `ReadmeMappingTableValidator`. Justified by ADR-002 §8 last two bullets (validators are independent-axis-of-change collaborators; their contracts matter to PreFlightCheckTask + ResolveVersionsFromExplicit + PackageTask + PackageFamilyPacker consumers). Bonus: interface implementations cannot be marked static, so `CA1822`/`S2325` analyzers are silently satisfied with zero `[SuppressMessage]` attributes. The exception remains bounded to root `Validation/` — broader build-host code continues to follow the default "concrete unless interface earned" rule.

> **S14 amendment (2026-05-09):** `Repositories/` is a parallel explicit exception alongside `Validation/`. **All build-host repositories live under root `Build.Repositories`** with `IFoo` interfaces and `AddSingleton<IFoo, Foo>()` registrations through `AddRepositories()`. Four repositories at S14 close: `IManifestRepository`, `IVcpkgManifestRepository`, `IVersionFileRepository`, `IHarvestStatusRepository` (relocated from `Targets/Harvest/Services/` to root `Repositories/` as part of P8). Each repository ships with **paired test classes in a single file**: `<X>RepositoryUnitTests` (mock-based — constructor + argument validation + cancellation contracts; no Cake extension chain mocking) and `<X>RepositoryRoundTripTests` (sociable — real `ICakeContext` + `Cake.Testing.FakeFileSystem` + real serialization, asserts on byte-on-disk parity). Justified by the same independent-axis-of-change + important-task-collaborator-contract rationale as `Validation/`; repositories are file-backed adapters whose contracts task classes and downstream stages depend on. Going forward all new repositories must follow this layout: root namespace, interface-bound, paired test classes.

DI registration should stay close to the code being registered through focused `IServiceCollection` extension methods. Keep `Program.cs` as composition/root parsing, not business logic.

Cake task classes are discovered from `[TaskName]` metadata and should not be explicitly registered in DI. Register collaborators, repositories, tools, and options; let Cake construct the task from the service provider.

Prefer idiomatic modern C# where it clarifies the build domain: immutable records, value objects, and pattern matching are welcome; over-abstracted functional cosplay is not.

## 9. Cake nativeness

Cake is the host language of the build host, not just a process runner underneath generic C# orchestration.

The Frosting target graph and task model are the architectural backbone. For build-host IO, process execution, paths, logging, environment access, and tool invocation, Cake-native abstractions are required by default:

- Cake/Frosting tasks and target dependencies for user-visible build lifecycle;
- Cake aliases, addins, and built-in abstractions when they already solve the problem;
- `ICakeContext`, `ICakeLog`, `IFileSystem`, `FilePath`, `DirectoryPath`, and related Cake types for build-host IO boundaries;
- `Tool<TSettings>` wrappers for external CLI tools when no suitable Cake addin or alias exists.

External interactions follow this preference order:

1. Use an existing Cake alias, built-in abstraction, or Cake addin/plugin.
2. Use an existing project `Tool<TSettings>` wrapper.
3. Add a new focused `Tool<TSettings>` wrapper for an external CLI.
4. Use a named library/API adapter when the integration is not a CLI boundary.
5. Use raw BCL IO or raw process invocation only as a justified exception in the migration slice review.

Pure policies, algorithms, and value objects should stay Cake-free when practical. Do not contort IO/tooling code to remove useful Cake types, and do not leak Cake types into pure code when simple domain values would do.

## 10. Code/document boundary and guardrail naming

Logic-bearing code must be understandable without jumping to documentation.

Comments in `.cs`, `.csproj`, `.props`, `.targets`, workflow YAML, and local orchestration scripts should explain the local logic or external constraint directly. They should not point readers to internal or external documentation as a substitute for explaining why the code behaves the way it does. Documentation may reference code; code should not outsource its meaning to documentation.

Guardrail IDs such as `G58` are documentation/reporting identifiers, not primary code names.

Types, files, methods, and test classes should use explicit build-domain names that describe the behavior being enforced. A guardrail ID may appear as report metadata, log evidence, test data, or documentation mapping, but the code identity should still be readable without knowing the release-guardrails document.

## 11. Results, validation, and logging

Retire OneOf-style result hierarchies for expected build failures.

> **S15 amendment (2026-05-10):** OneOf retirement story closes in P9. The last surviving package-info OneOf result collapsed to the repo's standard `Result<T,TError>` shape; the package-info implementation later settled under the Cake-native `Build.Tools.Vcpkg` alias/tool surface. The `OneOf`, `OneOf.SourceGenerator`, and `OneOf.Monads` NuGet packages are removed entirely from `Directory.Packages.props` and `build/_build/Build.csproj`. `Shared/Results/BuildResultExtensions.cs` (zero post-relocation consumers) retired in the same slice.

Use simple typed results:

- `Result<T, TError>` for expected operation success/failure;
- `ValidationReport` / `ValidationCheck` for multi-check validation output with warnings and errors.

Task/reporting boundaries own user-facing expected-error logging and translation to `CakeException`. Services may log progress or diagnostics, but should not both log an expected error and throw/return the same error for another layer to log again.

Avoid duplicate "log and throw" noise.

## 12. Testing model

The build-host test project is first-class infrastructure.

> **Operational detail:** [`testing-guidelines.md`](../refactoring/testing-guidelines.md) carries the day-to-day rules — test data policy (embedded fixtures vs centralized inline), V2 vs V1 infrastructure, filesystem seeding, scenario test structure, and anti-patterns. This section defines the architecture; the guidelines document defines the practice.

The target taxonomy is:

| Category | Purpose |
| --- | --- |
| Unit | Exercise collaborators, policies, validators, and small algorithms directly. |
| Scenario | Exercise real task orchestration in-process with a fake Cake world. |
| Integration | Exercise mission-critical external boundaries only when needed. |
| Fixtures/Data | Hold reusable fake repository data and input files. |

Scenario tests are build-host E2E tests inside the process: real task orchestration, fake Cake filesystem/log/context, and fake tool outputs where possible.

Integration tests are narrower and reserved for boundaries such as real process execution, real `dotnet`, real archive tools, or real filesystem behavior when the fake Cake world cannot prove the contract.

TUnit creates a new test class instance per test. Prefer composable builders and fixtures over a large abstract `TestBase`. Use constructors for simple per-test setup, hooks for async setup/cleanup, and `ClassDataSource` only for expensive shared resources.

Cake `FakeFileSystem` is the standard fake filesystem for build-host unit and scenario tests. Avoid `System.IO` inside the Cake project; use real filesystem APIs only where external integration boundaries make them necessary.

`Characterization` tests may remain as a temporary safety net during the refactor. Valuable long-lived tests should graduate into `Scenarios` after the behavior stabilizes.

Migrated target tests use the V2 fake Cake world directly. `ToLegacyBuildContext` is a temporary bridge for unmigrated tests, not a compatibility surface to extend for migrated targets.

Architecture dependency tests are not the guardrail for this refactor. The guardrails are this ADR, the refactor phase plan, `AGENTS.md`, and a review checklist.

## 13. `tools.cs` contract

`tools.cs` is the canonical human-facing local orchestration surface.

`tools.cs` remains standalone. It should not reference `build/_build` internals. When it needs package-family or manifest knowledge, `build/manifest.json` is the source of truth; minimal local parsing is acceptable and should be protected by contract tests or smoke flows rather than shared by importing the Cake host.

Direct Cake invocations remain valid for CI debugging, target discovery, and low-level diagnostics, but day-to-day local workflows should be expressed through `tools.cs`.

The refactor must preserve the command contracts used by:

- `tools.cs`;
- `.github/workflows/release.yml`;
- composite GitHub Actions under `.github/actions/`;
- documented local-development playbooks.

If a target name, artifact contract, or command surface changes, update all of those call sites in the same migration step.

## 14. Retired abstractions

The following abstractions are retired by this target architecture:

| Abstraction | Decision |
| --- | --- |
| Mandatory `*Pipeline` classes | Retire. Task classes own orchestration; named collaborators own extracted behavior. |
| `Host/Configuration` aggregate | Retire. `BuildContext` exposes named CLI properties; tasks validate what they use. |
| Raw version dictionaries on boundaries | Replace with typed concepts such as `PackageFamilyId` and a family version set. `NuGetVersion` itself is fine. |
| `strategy: "hybrid-static"` manifest field and strategy resolvers/factories | Retire. Hybrid-static is a documented invariant, not a configurable strategy. |
| Cake-owned coverage gate/ratchet | Retire. Coverage may return later as an optional non-blocking signal, not a build-host target concern. |
| Architecture dependency tests | Retire. Replace with written guardrails and review discipline. |

## 15. Consequences

Positive:

- Navigation follows the thing maintainers already know: the Cake target name.
- Build-host IO and process boundaries use Cake-native APIs instead of ad-hoc BCL/process wrappers.
- Code names describe behavior instead of requiring readers to decode guardrail IDs.
- Comments explain local logic instead of sending readers to documentation.
- Task classes become readable build stories instead of pass-through shells.
- Extraction becomes intentional instead of ceremonial.
- Test scenarios can describe behavior at the same level as build targets.
- `tools.cs` remains the local north star while Cake stays the CI build engine.

Tradeoffs:

- The migration will temporarily contain both old and new architecture.
- Some task classes will be longer than before, intentionally, because orchestration belongs there.
- Contributors must check Cake built-ins/addins before introducing new wrappers or raw process calls.
- Existing G-number-named code and doc-link comments need migration cleanup.
- Without architecture tests, review discipline and documentation must carry more weight.
- Deleting abstractions may require carefully sequenced updates to `release.yml`, `tools.cs`, docs, tests, and manifest validation.

## 16. Migration rule

Migrate incrementally.

For each target:

1. Add or preserve characterization/scenario coverage for current behavior.
2. Move the target into `Targets/<CakeTargetName>/`.
3. Let the task class own orchestration.
4. Extract only named collaborators that earn their existence.
5. Use Cake-native abstractions for IO/process/path/logging/tooling boundaries.
6. Replace guardrail-ID-first names with explicit domain names when touching guardrail code.
7. Replace doc-reference comments with self-contained logic explanations.
8. Remove local pipeline/configuration/strategy/coverage-era leftovers.
9. Verify the target through unit/scenario tests and the relevant `tools.cs` or CI command contract.

Do not perform a repo-wide rename that leaves the old design intact under new folder names.

## 17. References

- [`AGENTS.md`](../../AGENTS.md) - build-host reference pattern and agent operating rules
- [`tools.cs`](../../tools.cs) - local orchestration surface
- [`.github/workflows/release.yml`](../../.github/workflows/release.yml) - CI target orchestration
- [`docs/refactoring/target-centric-build-host-refactor-plan.md`](../refactoring/target-centric-build-host-refactor-plan.md) - detailed execution plan and consolidated rationale
- [`docs/refactoring/target-centric-build-host-review-checklist.md`](../refactoring/target-centric-build-host-review-checklist.md) - review checklist for migration slices
