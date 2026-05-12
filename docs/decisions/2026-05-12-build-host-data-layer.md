# ADR-003: Contract-Centric Build Host Data Layer

- **Status:** Accepted
- **Date:** 2026-05-12

## 1. Context

ADR-002 moved the build host to a target-centric architecture, but one follow-up refinement became clear during and after the migration: persisted contracts and tool-read contracts were still too easy to scatter across root concepts such as `Manifest`, `Versioning`, `Packaging`, `Harvesting`, and repository folders.

That shape created three problems:

- maintainers had to jump across multiple folders to answer a simple ownership question such as "who owns `versions.json`?";
- folders that mostly held file contracts could look like behavior layers;
- new readers and writers could drift toward whichever target first needed them instead of converging on a clear contract boundary.

The production code has now converged on a contract-centric `Data/` layer. This ADR records that decision.

## 2. Decision

The build host uses a **contract-centric `Data/` layer** for persisted or tool-read contracts.

### 2.1 What `Data/` owns

`Data/` is the home for build-host data contracts that are persisted, packaged, or read through a stable external/tool boundary.

That includes:

- JSON schema models for files the build host owns or consumes;
- repositories, readers, and writers for those contracts;
- serialization and deserialization logic;
- small data-specific error types for expected read/write failures;
- registration through a single `AddData()` composition method.

### 2.2 What `Data/` does not own

`Data/` is not a generic enterprise data-access layer.

It does **not** own:

- Cake target orchestration;
- validation policy;
- package mutation policy;
- artifact deployment workflows;
- diagnostic scanner behavior;
- staging and swap workflows;
- arbitrary helpers with no contract boundary.

### 2.3 Boundary rule

Repositories and readers sit at the data boundary, not deep inside business flow.

Preferred flow:

```text
Task loads data through Data/<Contract>/ adapter
  -> task validates target input
  -> task passes data object to services and validators
  -> services and validators perform policy, transformation, or checks
```

Avoid hidden data loads inside unrelated services or validators when the caller can load the contract explicitly.

### 2.4 Current cohorts

The current `Data/` layer is organized by contract family:

- `Data/Manifest/`
- `Data/Versions/`
- `Data/ProjectMetadata/`
- `Data/Harvest/`
- `Data/NativePackageMetadata/`

Each cohort owns the contract models and the adapter that loads or writes that contract.

### 2.5 Interface and testing rule

Data-boundary adapters are an explicit interface-friendly exception to the usual "concrete by default" rule.

These adapters are important task collaborators and stable seams around file or tool-backed boundaries. They should ship with paired tests that cover:

- constructor and argument validation;
- sociable round-trip or realistic fake-filesystem behavior.

### 2.6 Host boundary

`Host/` stays separate from `Data/`.

`Host/` owns:

- path resolution;
- runtime profile information;
- CLI parsing and named invocation state;
- Cake helper extensions.

`Host/` should not expose loaded manifest or version state as ambient singletons.

## 3. Consequences

Positive:

- ownership of persistent contracts becomes obvious;
- file shape and file adapter stay together;
- target code reads more clearly because IO boundaries are explicit;
- former root concepts stop pretending to be broad behavior layers when they are really contract homes.

Tradeoffs:

- `Data/` must stay disciplined so it does not become a new dumping ground;
- some formerly separate concepts lose top-level visibility because contract ownership matters more than broad category labels.

## 4. References

- [`2026-05-05-target-centric-build-host.md`](2026-05-05-target-centric-build-host.md)
- [`../refactoring/data-layer-refactor-plan.md`](../refactoring/data-layer-refactor-plan.md)
- [`../../build/_build/Data/ServiceCollectionExtensions.cs`](../../build/_build/Data/ServiceCollectionExtensions.cs)
- [`../../build/_build/Program.cs`](../../build/_build/Program.cs)
