---
name: "S04 ADR-002 target-centric refactor — P2b foundation primitives complete, P3 ready"
description: "Priming prompt for the next agent entering janset2d/sdl2-cs-bindings after P2b foundation primitives shipped on 2026-05-05. PackageFamilyId, PackageFamilyVersionSet (with versions.json-compatible JsonConverter), Result<T,TError>, and the ValidationReport family are live with 560 tests passing. P3 (BuildContext transition + repositories + deferred P2a polish) is the recommended next step."
argument-hint: "Confirm P3 BuildContext transition + ManifestRepository / VersionFileRepository, or propose alternative ordering"
agent: "agent"
model: "Claude Opus 4.7 (1M context)"
---

You are an engineer entering the janset2d/sdl2-cs-bindings repository after P2b foundation primitives shipped. The ADR-002 target-centric build-host refactor is in progress — documentation and guardrails are set, the V2 test harness is operational, the first scenario test proves the DI + task execution stack works, and the four foundation primitives that every subsequent migration slice will consume are now live with full unit-test coverage. No production code under `build/_build/Features/` has moved to `Targets/` yet.

## First Principle

> Treat every claim here as **current-as-of-authoring (`2026-05-05` — `s04-p2b-complete`)** and verify against the live repo, git log, and canonical docs before acting.

## What Just Happened

### P2b — Foundation primitives

Documented in `docs/refactoring/target-centric-build-host-refactor-plan.md` §P2b (the per-slice design spec and implementation plan were temporary and were deleted once the slice shipped — durable insights were promoted to the canonical refactor plan §P6 and §14).

| File | Owns |
|---|---|
| `build/_build/Versioning/PackageFamilyId.cs` | `PackageFamilyId` — sealed record, format-agnostic, ordinal-exact equality, manifest-driven extensibility |
| `build/_build/Versioning/PackageFamilyVersionSet.cs` | `PackageFamilyVersion` (record struct, iteration unit), `PackageFamilyVersionSet` (sealed record collection: sorted, throw-on-dup, value equality over sorted contents), `PackageFamilyVersionSetJsonConverter` (internal sealed STJ converter) |
| `build/_build/Results/Result.cs` | `Result<T, TError>` — readonly record struct, throwing accessors, `Success`/`Failure` factories, `TryGetValue`/`TryGetError` helpers |
| `build/_build/Results/ValidationReport.cs` | `ValidationSeverity` (Warning/Error), `ValidationCheck` (sealed record with non-empty validation), `ValidationReport` (sealed class: `Empty`, `Combine`, `IsValid`/`HasWarnings`/`Errors`/`Warnings` projections) |

Test files: `build/_build.Tests/Unit/Versioning/{PackageFamilyIdTests, PackageFamilyVersionSetTests}.cs` and `build/_build.Tests/Unit/Results/{ResultTests, ValidationReportTests}.cs`. 52 new TUnit tests, 0 V2 fixture usage (pure value objects), 100% pass.

### Slice notes (durable, in case the implementation context isn't loaded)

- **`PackageFamilyVersionSet` JSON wire format** matches the existing `versions.json` shape (flat `{family-id: normalized-semver-string}`). The `[JsonConverter]` attribute is honored end-to-end because `Build.Host.Cake.CakeJsonExtensions` is project-internal `System.Text.Json` (no Newtonsoft compatibility shims). P3's `VersionFileRepository` will get correct serialization for free.
- **`PackageFamilyId` ordinal-exact equality is stricter than current code** (`StringComparer.OrdinalIgnoreCase` everywhere). The manifest is canonical-lowercase by convention so this is a no-op in practice — but P6 PreFlight must enforce a `^sdl[0-9]+-[a-z][a-z0-9-]*$` invariant on `manifest.package_families[].name` (already added to the refactor plan §P6 task list). A hand-edited mixed-case manifest would silently bypass legacy ignore-case lookups and then break `PackageFamilyId` lookups in P3+/P4+ consumers.
- **`Result<T, TError>` retires the OneOf-style monad pattern** for new code. `Build.Shared.Strategy.ValidationResult` and other `OneOf.Monads.Result<TError, TSuccess>` use sites stay alive in unmigrated targets and migrate per-slice in P3+. Mass cutover at each target migration via Rider rename — preferred over scripted cross-file edits per Deniz's tooling preference.
- **Two analyzer suppressions, local + justified:**
  - `Result<T, TError>`: `[SuppressMessage("Design", "CA1000")]` — generic-type factory pattern matches BCL `ImmutableArray<T>.Create` / `Nullable<T>` helpers.
  - `ValidationReport`: `[SuppressMessage("Naming", "CA1710")]` — behavior-first naming overrides "Collection" suffix convention; the type's identity is "report of validation findings" with report-shaped public surface (`IsValid`, `HasWarnings`, `Errors`/`Warnings`, `Combine`).
- **No consumer migrations.** Every `IReadOnlyDictionary<string, NuGetVersion>` site and every `OneOf.Monads.Result` site is untouched. Migration follows per-target slice in P3+ / P4+.

## Onboarding Snapshot

| Concern | State |
|---|---|
| Stack | .NET 10 / C# 14, Cake Frosting 6.1, vcpkg, TUnit, GitHub Actions |
| RID coverage | 7 targets: `win-{x64,x86,arm64}`, `linux-{x64,arm64}`, `osx-{x64,arm64}` |
| Build host | `build/_build/` — Cake Frosting, currently `Features/` layout, migrating to `Targets/` |
| Dev orchestration | `tools.cs` — .NET 10 file-based app, Spectre.Console + CliWrap, 3 commands (build/setup/ci-sim) |
| CI | `.github/workflows/release.yml` — 9-stage pipeline |
| Testing | TUnit + MTP, 560 tests, 0 failures |
| Test infra (active) | `FakeCakeWorldV2`, `TestLogV2`, `TargetTestHostV2<TTask>` (V2 postfix) |
| Test infra (legacy) | `FakeRepoBuilder`, `TestHostFixture`, `FakeCakeToolContextBuilder` (retire P5) |
| Foundation primitives (active) | `Build.Versioning.{PackageFamilyId, PackageFamilyVersion, PackageFamilyVersionSet}`, `Build.Results.{Result<T,TError>, ValidationSeverity, ValidationCheck, ValidationReport}` |
| NuGet family pattern | Per-library managed+native packages, D-3seg versioning |
| Packaging strategy | Hybrid-static (invariant, not configurable) |
| Local feed | `build/msbuild/Janset.Local.props` generated by `tools.cs setup` |

## Current State You Should Assume Until Verified

- **Master HEAD:** P2b commit (`feat: P2b foundation primitives ...`). Verify with `git log -1`.
- **Working tree:** clean
- **Tests:** 560 total, 0 failed, 0 skipped (508 baseline + 52 P2b)
- **Pre-flight:** `dotnet run --file tools.cs -- build --tree` shows 20 Cake targets (unchanged)
- **Build-host code:** still pre-migration for targets — `Features/`, `Shared/`, `Integrations/`, `Host/Configuration/` all intact. The `Build.Versioning.*` and `Build.Results.*` namespaces are the first ADR-002 named-concept folders to land at root level.
- **Phase:** P0 → P1 → P2a → P2b complete. P3 (BuildContext transition + repositories + deferred P2a polish) ready.

## Deferred Items (for P3)

From the P2a review and P2b coexistence — `target-centric-build-host-refactor-plan.md` §P2a polish + §P3 task list:

1. **Host composition parity.** `TargetTestHostV2` should use production `AddHostBuildingBlocks(parsedArgs)` instead of manual DI. Register fake Cake primitives and config records first, then call the production extension method. Use `Microsoft.Extensions.DependencyInjection.Extensions.Replace` for override clarity.
2. **`IRuntimeProfile` production behavior.** Remove `Substitute.For<IRuntimeProfile>()` from `ToLegacyBuildContext`. Use the `IRuntimeProfile` resolved from `AddHostBuildingBlocks(parsedArgs)`. This gives `IsSystemFile(string)` production-shaped data.
3. **Runtime-bearing fake manifest.** The default fake manifest must include at least one `RuntimeInfo` matching the active RID + populated `SystemExclusions`. Pre-built `RuntimeConfig` fixtures under `Fixtures/Data/` for `win-x64`, `linux-x64`, `osx-x64`.
4. **Shim behavior tests.** Tests for RID → `RuntimeFamily` + triplet, `Rid`/`Config` → `ParsedArguments`, repo root → `PathService`, manifest propagation, default manifest validity.
5. **`ToLegacyBuildContext` retirement tracking.** The shim retires in P5 with `Host/Configuration` and `Configurations`.

P3 also adds the foundation repositories that consume P2b primitives:

6. **`ManifestRepository`** — file-backed manifest loader returning the typed manifest model (replaces ad-hoc `ManifestConfig` injection).
7. **`VersionFileRepository`** — reads/writes `artifacts/resolve-versions/versions.json` returning `PackageFamilyVersionSet`. The JsonConverter shipped with `PackageFamilyVersionSet` already produces the correct wire shape, so this should be a thin Cake-filesystem adapter on top of `CakeJsonExtensions.WriteJsonAsync` / `ToJsonAsync`.
8. **`BuildContext` named CLI properties** — `RuntimeIdentifier`, `TargetRid`, `VersionsFilePath`, `VersionSuffix`, `ExplicitVersion`, `PackageScope`, `Configuration`, etc. Retire the `Configurations` aggregate.

## Deferred Items (for P4)

Documented in `target-centric-build-host-refactor-plan.md` §P4:

**IAnsiConsole injection.** `InfoPipeline` uses `AnsiConsole.Status()` (static interactive widget). Only build-host interactive call site. Fix: constructor-inject `IAnsiConsole`, add `Spectre.Console.Testing.TestConsole` to `FakeCakeWorldV2` (per-world `NoopExclusivityMode` — fully parallel-safe). Migrate all non-interactive `AnsiConsole` call sites (`OtoolAnalyzePipeline`, `HarvestPipeline`, `Program.cs`) for consistency. Unblocks `InfoTask` failure-path scenario and full output assertions.

## Recommended Next Step

**P3 — Foundation completion and BuildContext transition.**

Order suggestion (smallest risk first):

1. `ManifestRepository` (read-only adapter wrapping the existing `ManifestConfig` loading) + tests on V2 fixtures.
2. `VersionFileRepository` (read + write, consumes `PackageFamilyVersionSet`'s JsonConverter) + tests.
3. `BuildContext` named CLI properties — incremental, one CLI option at a time. Keep `Configurations` alive until every consumer reads the named property.
4. Retire `Substitute.For<IRuntimeProfile>()` from `TargetTestHostV2` once `AddHostBuildingBlocks(parsedArgs)` is wired.
5. Add the runtime-bearing default fake manifest + `RuntimeConfig` fixtures.
6. Shim behavior tests for `ToLegacyBuildContext`.

Talk to Deniz before committing to ordering. The `target-centric-build-host-refactor-plan.md` §P3 task list is the canonical phase plan; this is just a suggested execution sequence.

## Mandatory Grounding (read in this order)

1. `docs/onboarding.md` — project overview, repo layout, glossary
2. `AGENTS.md` — operating rules, approval gate, settled decisions, Build-Host Reference Pattern
3. `CLAUDE.md` — quick pointers, common commands
4. `docs/plan.md` — current status, active phase, roadmap
5. `docs/refactoring/p1-baseline-notes.md` — P1 inventory (20 targets, 482 baseline, duplication, G-number naming)
6. `docs/refactoring/target-centric-build-host-refactor-plan.md` — full execution plan (especially §P2a-polish deferred items, §P3 BuildContext transition + repositories, §P4 IAnsiConsole, §P6 manifest lowercase invariant, §11 phase plan, §14 open decisions)
7. `docs/refactoring/target-centric-build-host-review-checklist.md` — per-slice review checklist (includes §13 V2 test infra, §14 non-actions)
8. `docs/decisions/2026-05-05-target-centric-build-host.md` — ADR-002 (north star)
9. `docs/refactoring/extraction-guidelines.md` — private-method decision tree and collaborator design rules
10. `build/_build/Versioning/{PackageFamilyId, PackageFamilyVersionSet}.cs` — P2b foundation primitives + JsonConverter
11. `build/_build/Results/{Result, ValidationReport}.cs` — P2b foundation primitives
12. `build/_build.Tests/Fixtures/` — V2 test infra files (`FakeCakeWorldV2.cs`, `TestLogV2.cs`, `TargetTestHostV2.cs`)
13. `build/_build.Tests/Scenarios/Info/InfoTask_Scenarios.cs` — working scenario example
14. `build/_build/Host/Cake/CakeJsonExtensions.cs` — project-internal STJ extension methods that honor `[JsonConverter]` attributes end-to-end
15. `.github/workflows/release.yml` — CI pipeline contract
16. `tools.cs` — local command contracts

## Locked Policy Recap

Curated from AGENTS.md and ADR-002 — the rules most likely to matter for P3 work:

- **No code changes without explicit "go / apply / proceed / başla / yap"** from Deniz. Docs-only edits are exempt.
- **Before any commit**: present summary + proposed commit message, ask for approval.
- **Master-direct commits are valid with explicit consent** — Deniz consented to master-direct on the P2b slice ("siktir et master'da çalışalım, ben review edeceğim"). Do not assume the same consent for the next slice; AGENTS.md ADR-002 default is isolated branch/worktree until Deniz says otherwise.
- **ADR-002 migration slices**: `brainstorming` → `writing-plans` → user approval → `executing-plans` → review checklist → summary + commit message → approval → commit.
- **`tools.cs` is the canonical local command surface**. Direct Cake invocation is valid for CI debugging and target discovery.
- **`BuildContext` is ambient invocation state**, not a service locator. Exposes named readonly CLI properties.
- **`Tools/` is Cake `Tool<TSettings>` wrappers ONLY.**
- **No catch-all `Shared` or `Common`.** Cross-target code promotes to named concepts only.
- **`*Pipeline` as a mandatory pattern is dead.** Task classes own orchestration.
- **`Host/Configuration` is dead.** Tasks read named `BuildContext` properties.
- **Interface rule**: Only for multiple impls, expensive seams, independent change axes, or important task collaborator contracts.
- **Cake `FakeFileSystem`** is the default in unit/scenario tests. No `System.IO.Abstractions`.
- **Test naming**: `<MethodName>_Should_<Verb>_<optional When/If/Given>`.
- **Test migration rule**: When a target migrates to `Targets/`, its tests migrate to V2 infra in the same slice.
- **V2 test infra default for everything we touch** (broader than the AGENTS.md target-migration-only rule). New unit tests for pure types (no Cake): TUnit-only, no fixtures needed. New tests touching any Cake primitive: V2.
- **Phase numbers and migration timing belong in canonical docs, not `.cs` comments.**
- **No doc references in code**: `.cs`, `.csproj`, `.props`, `.targets`, workflow YAML, and local orchestration scripts must explain local logic directly. Comments do not point to ADR/plan/checklist as a substitute for explanation.
- **Mass renames** (e.g. `OneOf.Monads.Result` → `Build.Results.Result`, `IReadOnlyDictionary<string, NuGetVersion>` → `PackageFamilyVersionSet`): propose the rename plan and ask Deniz to apply it from Rider rather than scripting cross-file edits.

### Foundation primitives (active in P2b — use these in new code)

- `PackageFamilyId(string Value)` — ordinal-exact equality, format-agnostic, validates non-empty/whitespace.
- `PackageFamilyVersion(PackageFamilyId Family, NuGetVersion Version)` — record struct, structural equality.
- `PackageFamilyVersionSet` — `Empty` singleton, `Count`, `Contains`, `TryGetVersion`, `RequireVersion`, `Families` (sorted), value equality, throw-on-duplicate at construction. Serializes as `{family-id: normalized-semver-string}` end-to-end via the co-located `[JsonConverter]`.
- `Result<T, TError>` — `Success(value)` / `Failure(error)` factories, `IsSuccess`/`IsFailure`, throwing `Value`/`Error` accessors, `TryGetValue`/`TryGetError` helpers. No `Match`/`Map`/`Bind`. `default(Result)` is a quiet failure with `default(TError)` — accepted caveat, do not construct default instances.
- `ValidationReport` — `Empty` singleton, `Combine(params ValidationReport[])` aggregator, `IsValid`/`HasWarnings`, `Errors`/`Warnings` projections. Report does not throw — task class translates invalid report into `CakeException` at the task boundary.

### Common commands

```pwsh
# Full test suite
dotnet test --project build/_build.Tests/Build.Tests.csproj -c Release --framework net10.0

# Cake target discovery
dotnet run --file tools.cs -- build --tree
dotnet run --file tools.cs -- build --target Info
```

## Final Steering Note

P0 (docs/guardrails), P1 (baseline), P2a (V2 test infra), and now P2b (foundation primitives) are complete. 560 tests passing, 0 failures. No production code has moved from `Features/` to `Targets/` yet — that starts in P4 after P3 lands the foundation completion (BuildContext transition + repositories + deferred P2a polish).

The natural next step is P3. The repositories (`ManifestRepository`, `VersionFileRepository`) consume the P2b primitives directly — `VersionFileRepository` in particular benefits from the JSON converter that already produces the existing wire shape. The `BuildContext` transition can be done incrementally, one CLI option at a time, keeping `Configurations` alive until every consumer reads the named property.

P2b's analyzer suppressions are the first locally-justified suppressions added during the refactor — both follow the AGENTS.md "local + justified" rule with `[SuppressMessage]` attributes. P3 work should not need new ones; if you find yourself reaching for `#pragma warning disable`, redesign first.

The build host has never been in better shape to refactor. The V2 test infra catches false-green tests, the foundation primitives are ready for repository wiring, and the canonical refactor plan is up-to-date with §P6 PreFlight invariant and §14 folder-layout follow-up. Hold the line on extraction, interface, and naming rules — that's the taste that'll keep the next architecture from rotting.
