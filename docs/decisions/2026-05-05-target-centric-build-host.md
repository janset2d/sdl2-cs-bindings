# ADR-002: Target-Centric Cake Build Host Architecture

- **Status:** Accepted
- **Date:** 2026-05-05
- **Revised:** 2026-05-12
- **Notes:** This cleaned-up version preserves the normative decisions still active in code. Migration sequencing, slice-by-slice execution notes, and review history were deliberately removed from the ADR and belong in historical records or git history.

## 1. Context

The Cake Frosting build host is the production pipeline for native harvesting, packaging, validation, consumer smoke testing, and publishing.

Before the refactor, the build host had several structural problems:

- thin task classes delegated immediately to large `*Pipeline` classes;
- orchestration, validation, reporting, file writes, and policy were mixed in generic workflow types;
- `Host/Configuration` hid option parsing far away from the target that used the values;
- `Features`, `Shared`, and `Integrations` did not match how maintainers actually navigate the build, which starts from a Cake target name;
- some abstractions existed because they were anticipated, not because the current build domain needed them.

The build host has since converged on a simpler shape that is easier to navigate and reason about. This ADR documents that target state.

## 2. Decision

The build host uses a **target-centric, Cake-native architecture**.

### 2.1 Primary navigation unit

Cake targets are the primary navigation unit.

- Executable targets live under `Targets/<CakeTargetName>/`.
- A maintainer should be able to start from `tools.cs`, CI workflow target names, or `--target <Name>` and reach the implementation directly.
- The current top-level support structure around targets is intentionally small: `Data/`, `Validation/`, `Tools/`, `Host/`, and `Results/`.

Folder names and namespaces should optimize for that navigation path, not for generic layering aesthetics.

### 2.2 Task classes own orchestration

Task classes are allowed to be meaningful.

They should:

- read the `BuildContext` properties they need;
- validate target-specific inputs at the task boundary;
- orchestrate the build story at a high level;
- translate expected failures into Cake logging and `CakeException`.

Requests are **earned**, not mandatory. Use a request DTO when collaborators need a stable input contract; skip the ceremony for simple, inline, or marker-like behavior.

The mandatory `*Pipeline` pattern is retired. Do not replace it with equivalent generic wrappers such as `Runner`, `Operation`, `Processor`, or `Executor` unless the type is a real build-domain concept.

### 2.3 Narrow host state

`BuildContext` is ambient invocation state, not a service locator.

It may expose:

- Cake/Frosting context needed by tasks and tools;
- repository and artifact paths;
- runtime profile information;
- named, readonly CLI-derived properties.

It should not expose raw option dictionaries or preloaded target-specific state.

`Host/Configuration` is retired. Targets read and validate their own inputs at the task boundary.

`tools.cs` remains the human-facing orchestration surface and must stay standalone. It may parse `build/manifest.json` minimally when needed, but it must not depend on `build/_build` internals.

### 2.4 Shared code promotion

Code stays target-local by default.

Promote code out of a target only when it represents a real shared concept. In the current build host, those concepts are mainly:

- `Data/<Contract>/` for persisted or tool-read contracts;
- `Validation/` for cross-cutting build rules;
- `Results/` for typed result/report primitives;
- `Tools/` for Cake `Tool<TSettings>` wrappers;
- a narrow `Host/` surface for paths, runtime profile, Cake helpers, and CLI parsing.

There is no catch-all `Shared` or `Common` bucket. `Features/`, `Shared/`, and `Integrations/` are retired layers and must not be reintroduced.

### 2.5 Interfaces and dependency injection

Constructor injection remains useful, but interfaces are not automatic.

Use an interface when at least one of these is true:

- there are multiple production implementations;
- the dependency is a costly or process-backed seam;
- the dependency expresses an independent axis of change;
- the contract matters enough that tasks and tests should depend on it explicitly.

Concrete types remain the default for small helpers, one-off extracted classes, and pure policies.

Two cohorts intentionally lean on interface-shaped contracts more heavily than the default:

- cross-cutting validators;
- data-boundary adapters such as repositories/readers/writers.

DI registration should stay close to the code being registered through focused `IServiceCollection` extensions. Cake task classes are discovered from `[TaskName]` metadata and should not be explicitly registered in DI.

### 2.6 Cake nativeness

Cake is the host language of the build host, not a thin process-launching layer underneath generic C# architecture.

For build-host IO, process execution, paths, logging, environment access, and tool invocation, prefer:

1. existing Cake aliases, built-ins, addins, or plugins;
2. project `Tool<TSettings>` wrappers;
3. named adapters only when the boundary is not a CLI boundary.

Raw BCL IO or raw process invocation is an exception that must earn itself.

Pure policies, algorithms, and value objects should stay Cake-free when practical.

### 2.7 Testing model

The build-host test project is first-class infrastructure.

The architectural testing taxonomy is:

- **Unit** for pure collaborators, policies, validators, and small algorithms;
- **Scenario** for in-process task orchestration with a fake Cake world;
- **Integration** for mission-critical external boundaries only.

Cake `FakeFileSystem` is the default fake filesystem. V2 test infrastructure is the default. Operational details live in the testing guidelines knowledge-base document, not in this ADR.

### 2.8 Retired abstractions

The following abstractions are retired by this architecture:

- mandatory `*Pipeline` classes;
- `Host/Configuration` aggregate objects;
- broad `Features/`, `Shared/`, and `Integrations/` layers;
- raw version-dictionary boundaries where a named value type or set is warranted;
- build-host strategy abstractions for hybrid-static packaging;
- OneOf-style result hierarchies for expected failures;
- architecture dependency tests as design police.

## 3. Consequences

Positive:

- navigation follows the Cake target names maintainers already know;
- task classes read like build stories instead of pass-through shells;
- extracted collaborators have to earn domain names instead of hiding behind generic wrappers;
- Cake-native IO and tooling boundaries stay visible;
- the build host is easier to review locally because orchestration, data access, and validation surfaces are explicit.

Tradeoffs:

- some task classes are intentionally longer because orchestration belongs there;
- maintainers must exercise judgment when deciding whether a behavior belongs target-local or in a shared concept;
- without architecture tests acting as police, documentation and review discipline matter more.

## 4. References

- [`2026-05-12-build-host-data-layer.md`](2026-05-12-build-host-data-layer.md)
- [`../knowledge-base/extraction-guidelines.md`](../knowledge-base/extraction-guidelines.md)
- [`../knowledge-base/testing-guidelines.md`](../knowledge-base/testing-guidelines.md)
- [`../knowledge-base/release-guardrails.md`](../knowledge-base/release-guardrails.md)
- [`../../AGENTS.md`](../../AGENTS.md)
- [`../../tools.cs`](../../tools.cs)
