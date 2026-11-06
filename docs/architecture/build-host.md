# Build Host Architecture

Date: 2026-11-06

The [Cake host](../../build/_build/) is target-centric with a contract-centric data layer.
[The target-centric decision](../decisions/2026-05-05-target-centric-build-host.md) and
[the data-layer decision](../decisions/2026-05-12-build-host-data-layer.md) record its rationale.

## Cake host and local orchestration

The Cake host is the CI production pipeline for native harvesting, packaging and validation.
Day-to-day orchestration lives in [tools.cs](../../tools.cs): setup, `ci-sim` and Cake passthrough.
Direct Cake calls are for CI debugging and target discovery. [Quality gates](../engineering/quality-gates.md)
owns commands; [workflow](../engineering/workflow.md#isolated-checkouts) owns checkout restrictions.

## Layout

| Concept | Rule |
|---|---|
| [Targets](../../build/_build/Targets/) | Primary navigation unit; folders use the PascalCase task name with hyphens removed (`Dumpbin-Dependents` → [`DumpbinDependents/`](../../build/_build/Targets/DumpbinDependents/)) |
| Task class | Owns orchestration, input validation, request construction, expected-error reporting and `CakeException` translation |
| Request DTO | Immutable collaborator input when useful; never replaces `RunAsync(BuildContext context)` |
| Target collaborators | Extract named behaviour when complexity, reuse, testing, dependencies or change reasons justify it |
| [Data](../../build/_build/Data/) | Shared contract-owned repositories and adapters |
| [Validation](../../build/_build/Validation/), [Results](../../build/_build/Results/) | Shared validation and typed outcomes |
| [Host](../../build/_build/Host/) | Narrow runtime and host concerns |
| [Tools](../../build/_build/Tools/) | Cake `Tool<TSettings>` wrappers and the sealed `VcpkgBootstrapTool`; CMake uses the `Cake.CMake` addin |

New code starts target-local or in one of these named root concepts. There is no catch-all shared
layer, integration layer or host-configuration layer.
[`GenerateBindings/`](../../build/_build/Targets/GenerateBindings/) holds dormant tested salvage with no task.

## Rules

- Tasks are not pass-through shells. Simple targets keep behaviour inline; larger targets use named collaborators.
- Non-trivial executable targets use a target-named request when collaborators need stable input.
  Trivial, no-op, default and fully inline targets need no request DTO.
- Cake targets model user-visible lifecycle. Do not create pseudo-target chains merely to reduce line counts.
- Use named collaborators rather than default `Pipeline`, `Runner`, `Operation` or `Processor` wrappers.
- `BuildContext` exposes named readonly CLI properties. File-backed state flows through contract-owned
  adapters such as `ManifestRepository` and `VersionFileRepository`.
- Code leaves a target only for a named shared concept with a second real consumer in the same change.
- Keep interfaces for multiple implementations, independent change axes, expensive seams or important
  task contracts. Avoid ceremonial single-implementation interface/class pairs.
- At build boundaries use Cake aliases and addins first, then `Tool<TSettings>`, then named API adapters.
  Raw BCL IO or raw process invocation requires a stated review justification.
- Pure policies, algorithms and value objects stay Cake-free when domain values suffice.
- Guardrail IDs belong in reports, logs, test data and documentation mappings. Code names describe behaviour.
- `FrostingLifetime` handles true host setup and teardown, not hidden target manifest or version state.
- [tools.cs](../../tools.cs) stays standalone. It may parse the [manifest](../../build/manifest.json)
  itself but references no build-host internals.
- Prefer immutable records, value objects and pattern matching when they clarify the domain.
- Check existing Cake aliases, addins and abstractions before wrapping an external tool. State why they do not fit.
- Warning suppressions are a last resort, local and justified; broad warning-disabling pragmas are not a default.
- Expected failures use `Result<T, TError>`. Multi-check validation uses `ValidationReport` and `ValidationCheck`.
  Avoid OneOf-style result hierarchies.
- Enforce design through review and current canon, not architecture-police tests.

[Extraction guidelines](../knowledge-base/extraction-guidelines.md) own private-method, collaborator and
interface decisions. [Testing guidelines](../knowledge-base/testing-guidelines.md) own fixture data,
filesystem rules and test taxonomy. `ResolveVersionsFromManifestTask` and `ResolveVersionsFromExplicitTask`
show task-owned orchestration without generic pipelines.

## Configuration files

[vcpkg.json](../../vcpkg.json) defines native dependencies and features. The [manifest](../../build/manifest.json)
owns packaging configuration, runtime/triplet/runner mappings, package families, system exclusions and
library metadata. Read its schema version from the file. `PreFlightCheck` validates the cross-file contract.
Runtime entries have no strategy field; triplet names encode the hybrid-static model.

## Native packaging pipeline

The [release workflow](../../.github/workflows/release.yml) expands the runtime matrix from the manifest.

1. **Harvest**, per RID: `BinaryClosureWalker` resolves primary binaries and transitive dependencies
   through dumpbin, ldd or otool, applies manifest system exclusions and checks hybrid-static leak-freeness.
   It stages the native payload and license attribution. **NativeSmoke** immediately tests that RID's payload.
2. **ConsolidateHarvest** merges runtime outputs into the harvest manifest, summary and consolidated licenses
   using stage-replace.
3. **Package** packs each family at its resolved version and validates nuspec shape, payload, licenses and
   cross-family resolvability.
4. **PackageConsumerSmoke** restores the packages from a local folder feed and proves P/Invoke loading,
   dyld and Unix symlink extraction. Executable TFMs come from `$(ExecutableTargetFrameworks)` in
   [build properties](../../Directory.Build.props).
5. **PublishStaging** pushes managed/native pairs to GitHub Packages with NuGet.Protocol.

`PreFlightCheck` runs before the matrix. `PublishPublic` is a stub.
Discover current targets with `--tree`; do not maintain a second target list.
[Release guardrails](../knowledge-base/release-guardrails.md) own the stage validation map and guardrail registry.
