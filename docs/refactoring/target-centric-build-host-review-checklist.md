# Target-Centric Build Host Review Checklist

Use this checklist for each ADR-002 migration slice.

## 1. Scope

- [ ] The Cake target being changed is named explicitly.
- [ ] `tools.cs` call sites are identified.
- [ ] `.github/workflows/release.yml` and `.github/actions/` call sites are identified if relevant.
- [ ] The expected behavior change is stated. Default is "no behavior change".
- [ ] Artifact paths and command contracts are preserved or intentionally migrated.
- [ ] `tools.cs` remains standalone; no build-host internals were imported into it.

## 2. Target module shape

- [ ] Code lives under `Targets/<CakeTargetName>/` for migrated targets.
- [ ] Folder names optimize navigation from Cake target names.
- [ ] Hyphenated target folders may keep hyphens; namespaces use valid C# identifiers.
- [ ] Subfolders exist only when they improve navigation.
- [ ] A second file in the same category uses the standard subfolder for that category.
- [ ] No empty symmetry folders were created.

## 3. Task orchestration

- [ ] The task class tells the high-level build story.
- [ ] The task reads named `BuildContext` properties instead of raw parser dictionaries.
- [ ] Migrated tasks do not read `BuildContext.ParsedArguments`.
- [ ] Raw parser values are removed from public `BuildContext` when the migration slice removes their last task consumer.
- [ ] Target input validation happens at the task boundary.
- [ ] Non-trivial executable target behavior has a `<Target>Request` when collaborators need a stable input contract.
- [ ] Trivial/no-op/default/fully-inline targets did not receive request ceremony.
- [ ] Large work was not split into internal Cake pseudo-targets solely to reduce LOC.
- [ ] Expected fatal failures are translated to `CakeException` at the task/reporting boundary.

## 4. Extraction quality

- [ ] Extraction decisions follow the decision tree in [extraction-guidelines.md](extraction-guidelines.md).
- [ ] Extracted classes have build-domain names.
- [ ] No generic `Helper`, `Manager`, `Processor`, `Handler`, `Runner`, or replacement `Pipeline` was added without a real domain meaning.
- [ ] Private methods are local mechanics, not hidden business/build policy.
- [ ] Private methods do not form call chains (private calling private calling private).
- [ ] Private method names avoid vague folding-region names such as `Process`, `Handle`, `Do`, `Prepare`, `Execute`, and `Manage` unless the domain noun makes the meaning concrete.
- [ ] No extracted class received a ceremonial `IFoo` interface without meeting the ADR-002 interface rule.
- [ ] No enterprise cosplay: one-liners did not become classes with interfaces and DI registrations.
- [ ] Branching-heavy algorithms and reusable policies are separately testable.
- [ ] Code that changes with one target stayed with that target.

## 5. Interfaces and DI

- [ ] Interfaces are justified by multiple implementations, expensive seams, independent change axes, or important task collaborator contracts.
- [ ] No ceremonial `IFoo`/`Foo` pairs were added.
- [ ] Small pure helpers remain concrete.
- [ ] DI registration lives near the code being registered.
- [ ] Cake task classes are not explicitly registered in DI; only their collaborators are registered.
- [ ] `Program.cs` remains composition/root parsing, not target business logic.

## 6. Shared concepts

- [ ] No catch-all `Shared` or `Common` bucket was introduced.
- [ ] Cross-target code was promoted only to a named concept such as `Manifest`, `Runtime`, `Versioning`, `Packaging`, `Results`, or a repository.
- [ ] "Imminent reuse" means a second real consumer exists in the same migration slice or phase, not hypothetical future reuse.
- [ ] File-backed state uses repository naming where appropriate.
- [ ] Version APIs avoid raw `IReadOnlyDictionary<string, NuGetVersion>` boundaries.
- [ ] Package family identity uses manifest-driven value objects, not enums.

## 7. Code/document boundary and naming

- [ ] Comments in `.cs`, `.csproj`, `.props`, `.targets`, workflow YAML, and local orchestration scripts explain local logic directly.
- [ ] Comments do not point to internal or external docs as a substitute for explaining behavior.
- [ ] Phase numbers, ADR references, and migration timing live in canonical docs, not logic-bearing comments.
- [ ] Types, files, methods, and test classes use behavior-first names.
- [ ] Guardrail IDs such as `G58` are report/log/test-data/docs metadata only, not primary code identity.
- [ ] Guardrail-related messages include both readable behavior and any needed guardrail ID.

## 8. Cake nativeness and filesystem

- [ ] Build-host IO/process/path/logging/environment/tooling boundaries use Cake-native abstractions by default.
- [ ] IO/tooling code may use Cake abstractions directly.
- [ ] Pure policy/algorithm code avoids Cake types where practical.
- [ ] Existing Cake aliases, addins/plugins, or Cake abstractions were checked before adding new tool wrappers or raw process calls.
- [ ] Raw BCL IO or raw process invocation has an explicit slice-review justification.
- [ ] Build-host tests use Cake `FakeFileSystem` by default.
- [ ] No `System.IO.Abstractions` was introduced.
- [ ] Direct `System.IO` usage is limited to explicit external integration boundaries.

## 9. Results, validation, and logging

- [ ] Expected operation failures use simple typed results/errors.
- [ ] Multi-check validations use validation report/check shapes.
- [ ] Services do not both log and return/throw the same expected error for another layer to log.
- [ ] User-facing failure output is clear and logged once.
- [ ] Unexpected exceptions are not swallowed by broad catch blocks.
- [ ] New warning suppressions are local, justified, and documented; broad `#pragma warning disable` cleanup shortcuts were not added.

## 10. Testing

> **Operational detail:** [`testing-guidelines.md`](testing-guidelines.md) — test data policy, V2/V1 infra rules, filesystem seeding, scenario structure, anti-patterns.

- [ ] Unit tests cover pure policies, validators, and small algorithms directly.
- [ ] Scenario tests exercise real task orchestration with fake Cake world for migrated target behavior.
- [ ] Integration tests are limited to mission-critical external boundaries.
- [ ] No large abstract `TestBase` was introduced by default.
- [ ] TUnit assertions are awaited.
- [ ] Test names follow `<MethodName>_Should_<Verb>_<optional When/If/Given>`.
- [ ] Characterization tests are kept only as temporary safety net or promoted to `Scenarios`.

## 11. Retired abstractions

- [ ] No new mandatory `*Pipeline` class was introduced.
- [ ] `Host/Configuration` usage decreased or stayed unchanged only for unmigrated code.
- [ ] Strategy abstraction usage decreased when touching manifest/runtime/preflight/packaging code.
- [ ] Coverage gate code was not expanded.
- [ ] Architecture dependency tests were not updated to enforce new taste rules.
- [ ] `Integrations/` usage decreased or a remaining adapter has a named, justified destination.
- [ ] `FrostingLifetime` was not used to hide manifest/version target state preload.

## 12. Documentation and validation

- [ ] ADR-002 remains accurate after the change.
- [ ] Refactoring plan is updated if the migration sequence changes.
- [ ] `AGENTS.md` is updated when agent-facing rules change.
- [ ] Playbooks are updated when local commands change.
- [ ] The agreed validation command was run for the slice.
- [ ] Commit summary and proposed commit message were presented before committing.

## 13. V2 test infrastructure

- [ ] New and migrated tests use V2 infra (`FakeCakeWorldV2`, `TestLogV2`, `TargetTestHostV2<TTask>`).
- [ ] Process commands are configured via `WithProcessResult(...)` or the explicit `WithDefaultProcessResult(...)` API.
- [ ] Unconfigured process commands fail fast — no silent "exit code 0" default.
- [ ] Tool paths are configured via `WithToolPath(...)` or the explicit `WithDefaultToolPath(...)` API.
- [ ] Scenario tests assert on `ProcessInvocations` when process behavior matters.
- [ ] `TargetTestHostV2<TTask>` constraint uses `IFrostingTask` — both sync and async tasks are supported.
- [ ] `ToLegacyBuildContext` was not extended to support migrated target tests.
- [ ] Migrated target tests do not use V1 fixture features or shim behavior to keep old tests alive.
- [ ] V2 target tests instantiate task classes from the service provider without registering the task type as a service.
- [ ] V1 fixtures (`FakeRepoBuilder`, `TestHostFixture`) are not extended — only unmigrated code uses them.

### Test migration rule

When a production target is migrated to `Targets/<CakeTargetName>/`, its existing tests under `Unit/Features/<OldFeature>/` must migrate to V2 test infrastructure in the same migration slice. New unit tests go under `Unit/Targets/<CakeTargetName>/`, new scenarios under `Scenarios/<CakeTargetName>/`. `ToLegacyBuildContext` is not a compatibility support surface for migrated targets.

## 14. Non-actions (do not reopen without explicit approval)

- [ ] `FakeRepoPlatformV2.Unix` was not split into Linux and macOS.
- [ ] `HasMessageExact(...)` was not added to `TestLogV2` unnecessarily.
- [ ] No reflection-based shim usage tracker or architecture-police test was added.
- [ ] Production `InfoPipeline` / Spectre.Console output was not refactored outside the P4 `IAnsiConsole` injection slice.
- [ ] `System.IO.Abstractions` was not introduced.
- [ ] Giant abstract `TestBase` was not introduced.
- [ ] `Spectre.Console.Testing.TestConsole` was not used outside `FakeCakeWorldV2`.
