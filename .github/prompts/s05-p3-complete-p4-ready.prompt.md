---
name: "S05 ADR-002 target-centric refactor — P3 foundation complete, P4 ready"
description: "Priming prompt for the next agent entering janset2d/sdl2-cs-bindings after P3 (repositories + BuildContext + InfoTask migration) shipped on 2026-05-06. IManifestRepository, IVersionFileRepository, FakeCakeWorldV2 platform factories, embedded JSON fixtures, IAnsiConsole injection, and the first migrated target (InfoTask under Targets/Info/) are live with 577 tests passing. P4 (low-risk target migrations: ResolveVersions, CleanArtifacts, CompileSolution, diagnostic targets) is the recommended next step."
argument-hint: "Confirm P4 target migration order (ResolveVersions* first), or propose alternative ordering"
agent: "agent"
model: "Claude Opus 4.7 (1M context)"
---

You are an engineer entering the janset2d/sdl2-cs-bindings repository after P3 foundation completion shipped. The ADR-002 target-centric build-host refactor is progressing — P0 through P3 are complete, the V2 test harness has platform factories (`CreateWindows/CreateLinux/CreateOsx`), `IAnsiConsole` injection is wired in both production and test DI, two file-backed repositories are live, and the first target migration (`InfoTask` → `Targets/Info/`) proves the target-module pattern end-to-end. No other production code has moved from `Features/` to `Targets/` yet — that starts now.

## First Principle

> Treat every claim here as **current-as-of-authoring (`2026-05-06` — `s05-p3-complete`)** and verify against the live repo, git log, and canonical docs before acting.

## What Just Happened

### P3 — Foundation completion

Documented in `docs/refactoring/target-centric-build-host-refactor-plan.md` §P3. Per-slice design spec and implementation plan were temporary and deleted once the slice shipped; durable insights were promoted to the canonical refactor plan §§P2a-polish and P3.

| File | Owns |
|---|---|
| `build/_build/Repositories/IManifestRepository.cs` | Interface — `ManifestConfig Load()` |
| `build/_build/Repositories/ManifestRepository.cs` | Sealed class — Cake-native via `CakeJsonExtensions.ToJson<T>`, takes `ICakeContext` + `FilePath` |
| `build/_build/Repositories/IVersionFileRepository.cs` | Interface — `PackageFamilyVersionSet Load()` + `Task SaveAsync(PackageFamilyVersionSet)` |
| `build/_build/Repositories/VersionFileRepository.cs` | Sealed class — Cake-native via `CakeJsonExtensions.ToJson<T>` / `WriteJsonAsync<T>`, takes `ICakeContext` + `FilePath` |
| `build/_build/Targets/Info/InfoTask.cs` | First migrated target — `InfoPipeline` deleted, orchestration inlined, `IAnsiConsole` constructor-injected, reads `context.RuntimeIdentifier` |
| `build/_build/Host/BuildContext.cs` | Three additive named properties: `RuntimeIdentifier`, `BuildConfiguration`, `VersionsFilePath` |
| `build/_build/Program.cs` | `IAnsiConsole` + `InfoTask` registered directly; `AddInfoFeature()` removed |
| `build/_build.Tests/Fixtures/FakeCakeWorldV2.cs` | `CreateWindows()`, `CreateLinux()`, `CreateOsx()` static factories + `TestConsole AnsiConsole` property |
| `build/_build.Tests/Fixtures/FixtureLoader.cs` | Embedded resource loader — `Assembly.GetManifestResourceStream()` |
| `build/_build.Tests/Fixtures/TargetTestHostV2.cs` | `IAnsiConsole` registered from `_world.AnsiConsole` |
| `build/_build.Tests/Fixtures/Data/Manifest/` | 4 embedded JSON manifest fixtures (win-x64, linux-x64, osx-x64, minimal) |
| `build/_build.Tests/Fixtures/Data/Versions/` | 2 embedded JSON version fixtures (valid, empty) |

Deleted: `build/_build/Features/Info/InfoTask.cs`, `InfoPipeline.cs`, `ServiceCollectionExtensions.cs`. The `Features/Info/` directory is gone.

Test files: `build/_build.Tests/Unit/Repositories/{ManifestRepository,VersionFileRepository}Tests.cs` (8 tests, pure unit via `FakeCakeWorldV2`), `build/_build.Tests/Unit/Fixtures/FakeCakeWorldV2PlatformFactoriesTests.cs` (9 tests), `build/_build.Tests/Scenarios/Info/InfoTask_Scenarios.cs` (2 tests — existing + non-zero exit code assertion via `TestConsole.Output`). 17 new tests total. 577 tests, 0 failures.

### Slice notes (durable)

- **`IManifestRepository` and `IVersionFileRepository` have zero production consumers.** They are shipped ready for P4. `ResolveVersionsFromManifestTask` and `ResolveVersionsFromExplicitTask` are the natural first consumers — they're already close to target-owned orchestration and would retire `VersionsJsonWriter` in the process.
- **Repositories are Cake-native (ADR-002 §9).** Both take `ICakeContext` + `FilePath`, delegate to `CakeJsonExtensions` for all JSON I/O. No raw `System.Text.Json` at repository boundaries. The `[JsonConverter]` on `PackageFamilyVersionSet` is honored transparently.
- **Interfaces are justified (ADR-002 §8).** File-backed adapters that need test seams. Tests substitute the fake `ICakeContext` backed by `FakeFileSystem` with seeded fixture content.
- **`BuildContext` named properties are additive.** `RuntimeIdentifier` → `Runtime.Rid`, `BuildConfiguration` → `ParsedArguments.Config`, `VersionsFilePath` → `ParsedArguments.VersionsFile ?? Paths.GetResolveVersionsOutputFile()`. No legacy surface removed — unmigrated code still uses `Options.Vcpkg`, `Options.Package`, etc.
- **`BuildConfiguration` not `Configuration`.** The name `Configuration` was taken by the inherited `CakeContextAdapter.Configuration` — renamed to `BuildConfiguration` to avoid CS0114.
- **No legacy investment.** `ToLegacyBuildContext` received zero changes. `Host/Configuration` was untouched. The shim stays frozen and retires in P5.
- **`FakeCakeWorldV2` platform factories seed a minimal valid manifest.** Each factory loads an embedded JSON fixture with matching `RuntimeInfo` + populated `SystemExclusions`, sets default process results, and allows full fluent-API override. `CreateOsx` uses `FakeRepoPlatformV2.Unix` internally (Cake.Testing has no separate macOS factory) — platform-specific behavior comes from the seeded manifest.
- **`Spectre.Console` bumped from 0.49.1 → 0.54.0.** Required for Cake.Frosting 6.1 compatibility (`Spectre.Console >= 0.54.0` transitive dependency).
- **`CA1031` suppression carried over.** `InfoTask`'s `#pragma warning disable CA1031` is from the deleted `InfoPipeline` — not new slop.

## Onboarding Snapshot

| Concern | State |
|---|---|
| Stack | .NET 10 / C# 14, Cake Frosting 6.1, vcpkg, TUnit, GitHub Actions |
| RID coverage | 7 targets: `win-{x64,x86,arm64}`, `linux-{x64,arm64}`, `osx-{x64,arm64}` |
| Build host | `build/_build/` — Cake Frosting, `Features/` layout migrating to `Targets/` |
| Dev orchestration | `tools.cs` — .NET 10 file-based app, Spectre.Console + CliWrap, 3 commands (build/setup/ci-sim) |
| CI | `.github/workflows/release.yml` — 9-stage pipeline |
| Testing | TUnit + MTP, 577 tests, 0 failures |
| Test infra (active) | `FakeCakeWorldV2` (with platform factories), `TestLogV2`, `TargetTestHostV2<TTask>` (with `IAnsiConsole`), `FixtureLoader` |
| Test infra (legacy) | `FakeRepoBuilder`, `TestHostFixture`, `FakeCakeToolContextBuilder` (retire P5) |
| Named concepts (active) | `Build.Repositories.{IManifestRepository, ManifestRepository, IVersionFileRepository, VersionFileRepository}`, `Build.Versioning.{PackageFamilyId, PackageFamilyVersion, PackageFamilyVersionSet}`, `Build.Results.{Result<T,TError>, ValidationSeverity, ValidationCheck, ValidationReport}` |
| Migrated targets | `Targets/Info/InfoTask.cs` (1 of 20) |
| NuGet family pattern | Per-library managed+native packages, D-3seg versioning |
| Packaging strategy | Hybrid-static (invariant, not configurable) |
| Local feed | `build/msbuild/Janset.Local.props` generated by `tools.cs setup` |

## Current State You Should Assume Until Verified

- **Master HEAD:** `79155eb` — docs update + cleanup post-P3
- **Working tree:** clean (verify with `git status`)
- **Tests:** 577 total, 0 failed, 0 skipped
- **Pre-flight:** `dotnet run --file tools.cs -- build --tree` shows 20 Cake targets (unchanged)
- **Build-host code:** `Features/` still holds 19 unmigrated targets. `Targets/Info/` is the sole migrated target. `Repositories/`, `Versioning/`, `Results/` are the three ADR-002 named-concept folders at root level.
- **Phase:** P0 → P1 → P2a → P2b → P3 complete. P4 (low-risk target migrations) ready.
- **`VersionsJsonWriter`** at `build/_build/Features/Versioning/VersionsJsonWriter.cs` still alive — consumed by `ResolveVersionsFromManifestTask` and `ResolveVersionsFromExplicitTask`. Retires when those targets migrate in P4.
- **`Features/Info/`** directory is gone — fully migrated. The `ServiceCollectionExtensionsSmokeTests` removed its `AddInfoFeature_Should` test.

## Recommended Next Step

**P4 — Low-risk target migrations.**

The refactor plan (§P4) prescribes migrating 8 low-risk targets to prove the `Targets/` module layout before touching the boss fights (Package, Harvest, PreFlight). Suggested order (smallest risk first):

1. **`ResolveVersionsFromManifest`** — already close to target-owned orchestration. Consumes `IVersionFileRepository.SaveAsync()` instead of `VersionsJsonWriter`. First production consumer of `PackageFamilyVersionSet`. Retires `VersionsJsonWriter`.
2. **`ResolveVersionsFromExplicit`** — same pattern as FromManifest. Parses `--explicit-version*` CLI args.
3. **`CleanArtifacts`** — low-risk maintenance target, simple file cleanup.
4. **`CompileSolution`** — process/build invocation, slightly more integration-heavy.
5-8. **Diagnostic targets** — `Dumpbin-Dependents`, `Ldd-Dependents`, `Otool-Analyze`, `Inspect-HarvestedDependencies`. These are diagnostic tools with hyphenated folder names (folders keep hyphens, namespaces don't).

Each target follows the proven pattern: move to `Targets/<CakeTargetName>/`, inline or extract named collaborators, update DI registration (inline `services.AddSingleton<TTask>()` instead of `AddXFeature()`), delete old `Features/` files, update tests to V2 infra, verify CI command contracts.

Talk to Deniz before committing to ordering. The `target-centric-build-host-refactor-plan.md` §P4 task list is the canonical phase plan; this is just the suggested execution sequence.

## Mandatory Grounding (read in this order)

1. `docs/onboarding.md` — project overview, repo layout, glossary
2. `AGENTS.md` — operating rules, approval gate, settled decisions, Build-Host Reference Pattern
3. `CLAUDE.md` — quick pointers, common commands
4. `docs/plan.md` — current status (P0-P3 complete), roadmap
5. `docs/refactoring/target-centric-build-host-refactor-plan.md` — full execution plan (especially §P4 low-risk target migrations, §P2a-polish re-evaluated items, §7 BuildContext/configuration plan)
6. `docs/decisions/2026-05-05-target-centric-build-host.md` — ADR-002 (north star)
7. `docs/refactoring/target-centric-build-host-review-checklist.md` — per-slice review checklist
8. `docs/refactoring/extraction-guidelines.md` — private-method decision tree and collaborator design rules
9. `build/_build/Repositories/{IManifestRepository, ManifestRepository, IVersionFileRepository, VersionFileRepository}.cs` — P3 repositories (first consumers in P4)
10. `build/_build/Versioning/{PackageFamilyId, PackageFamilyVersionSet}.cs` — P2b foundation primitives (consumed by VersionFileRepository)
11. `build/_build/Features/Versioning/VersionsJsonWriter.cs` — retiring in P4, replaced by `IVersionFileRepository.SaveAsync()`
12. `build/_build/Features/Versioning/{ResolveVersionsFromManifestTask, ResolveVersionsFromExplicitTask}.cs` — first P4 migration candidates
13. `build/_build/Targets/Info/InfoTask.cs` — golden example of a migrated target
14. `.github/workflows/release.yml` — CI pipeline contract
15. `tools.cs` — local command contracts

## Locked Policy Recap

Curated from AGENTS.md and ADR-002 — the rules most likely to matter for P4 work:

- **No code changes without explicit "go / apply / proceed / başla / yap"** from Deniz. Docs-only edits are exempt.
- **Before any commit**: present summary + proposed commit message, ask for approval.
- **Master-direct commits are the default.** Do not branch unless there is a concrete reason.
- **ADR-002 migration slices**: `brainstorming` → `writing-plans` → user approval → `executing-plans` → review checklist → summary + commit message → approval → commit.
- **`tools.cs` is the canonical local command surface**. Direct Cake invocation is valid for CI debugging and target discovery.
- **`BuildContext` is ambient invocation state**, not a service locator. Exposes named readonly CLI properties.
- **`Tools/` is Cake `Tool<TSettings>` wrappers ONLY.**
- **No catch-all `Shared` or `Common`.** Cross-target code promotes to named concepts only.
- **`*Pipeline` as a mandatory pattern is dead.** Task classes own orchestration.
- **`Host/Configuration` is dead.** Tasks read named `BuildContext` properties. Existing `Configurations` stays alive until all consumers migrate.
- **Interface rule**: Only for multiple impls, expensive seams, independent change axes, or important task collaborator contracts.
- **Cake nativeness**: Build-host IO/process/path/logging/tooling boundaries use Cake-native abstractions by default. `CakeJsonExtensions` is the project's JSON surface.
- **Cake `FakeFileSystem`** is the default in unit/scenario tests. No `System.IO.Abstractions`.
- **Test naming**: `<MethodName>_Should_<Verb>_<optional When/If/Given>`.
- **Test migration rule**: When a target migrates to `Targets/`, its tests migrate to V2 infra in the same slice.
- **V2 test infra default for everything we touch.** New unit tests for pure types (no Cake): TUnit-only, no fixtures needed. New tests touching any Cake primitive: V2.
- **Phase numbers and migration timing belong in canonical docs, not `.cs` comments.**
- **No doc references in code.**
- **`ToLegacyBuildContext` receives zero changes.** It's a temporary bridge, not a featured API.
- **No legacy investment.** Migrated targets do not inject `Configurations`, `IRuntimeProfile`, or `IPathService`. They read named `BuildContext` properties.

### Active foundation (use these in new code)

- `IManifestRepository` / `ManifestRepository(ICakeContext, FilePath)` — `Load()` returns `ManifestConfig`.
- `IVersionFileRepository` / `VersionFileRepository(ICakeContext, FilePath)` — `Load()` returns `PackageFamilyVersionSet`, `SaveAsync()` persists it. Wire shape matches existing `versions.json`.
- `FakeCakeWorldV2.CreateWindows/CreateLinux/CreateOsx()` — platform factories with seeded manifest + default process results.
- `FixtureLoader.Load("Manifest/manifest-win-x64.json")` — embedded resource loader.
- `PackageFamilyId`, `PackageFamilyVersionSet`, `Result<T,TError>`, `ValidationReport` — P2b primitives.
- `FakeCakeWorldV2.AnsiConsole` — `TestConsole` instance for output assertions.

### Common commands

```pwsh
# Full test suite
dotnet test --project build/_build.Tests/Build.Tests.csproj -c Release --framework net10.0

# Cake target discovery
dotnet run --file tools.cs -- build --tree
dotnet run --file tools.cs -- build --target Info
```

## Final Steering Note

P0 through P3 are in the books — docs, guardrails, baseline, V2 test infra, foundation primitives, repositories, and the first migrated target. 577 tests, 0 failures. `InfoTask` under `Targets/Info/` is the working proof that the ADR-002 pattern produces simpler, more testable code: `InfoPipeline` is deleted, orchestration is inline, `IAnsiConsole` is injected, and scenario tests assert on real console output.

P4 is the volume play: 8 low-risk targets, one pattern repeated. `ResolveVersionsFromManifest` is the ideal first target — it's already close to the desired shape, it consumes the P3 repositories directly (`IVersionFileRepository.SaveAsync()` replaces `VersionsJsonWriter`), and it proves `PackageFamilyVersionSet` as a first-class boundary type. Each subsequent target gets easier as the pattern solidifies.

The repositories are ready. The test harness is ready. The pattern is proven. Run the playbook.
