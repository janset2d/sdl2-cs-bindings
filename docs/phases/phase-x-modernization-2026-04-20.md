# Phase X — Modernization Wave (Standalone Parked Proposal)

> **Status:** PARKED / UNASSIGNED. This document captures a comprehensive modernization research pass and staged implementation roadmap produced during the 2026-04-20 session. It is intentionally **not tied to any specific phase** — the individual stages (M0–M8) can be pulled into Phase 2a, Phase 2b, Phase 3, or later work items as scheduling and priority dictate.
>
> **ADR-002 references throughout this doc are historical.** The build-host architecture has since moved to [ADR-004 (Cake-native feature-oriented, 2026-05-02)](../decisions/2026-05-02-cake-native-feature-architecture.md), which supersedes ADR-002. M-stage implementations that pull into a real wave should read mappings against ADR-004 §2.1 (top-level shape) — for example, `Domain/Results/` referenced below maps to `Shared/Results/` (or to a feature folder per ADR-004 §2.6.1 admission criteria).
>
> **Date:** 2026-04-20
> **Maintainer:** Deniz Irgin (@denizirgin)
> **Scope:** Cross-cutting repository modernization (target frameworks, C# language, NuGet packages, analyzer posture, .editorconfig, Result pattern, vcpkg baseline, documentation drift, Native AOT future-feasibility)
> **Gate:** All M-stages require explicit approval before implementation per [`AGENTS.md`](../../AGENTS.md) approval-gate rules.

## 1. Executive Summary

This document answers four modernization questions researched in parallel on 2026-04-20:

1. **Is migrating the repo to .NET 10 + C# 14 feasible today, given our multi-target library matrix?** Yes, empirically clean. The multi-target C# 14 trap list (`Enumerable.Reverse` binding flip, `field` contextual keyword, reserved-identifier conflicts) has **zero call sites in this codebase** today. Uniform `LangVersion=14.0` across all TFMs is safe.
2. **Should we migrate off OneOf.Monads, and if so, to what?** Yes — OneOf.Monads is effectively orphaned (last public release 2022-09-01; our pinned `1.21.0` is not present on public nuget.org, only `1.20.0` is). No third-party library satisfies all four of Deniz's hard requirements (implicit/explicit conversion, error distinction via `TError` generic, T0/T1 wrap, OneOf interop) without major compromises. **Recommendation: in-house 60-line `Result<TError, T>` readonly struct. Keep OneOf itself.** No source generator — defer until Result type count exceeds ~25.
3. **Is the analyzer + .editorconfig set Deniz dropped in `artifacts/temp/` industry-standard?** Structurally yes, well-aligned with 2026 Microsoft guidance — but **not plug-and-play** for this repo. Requires tailoring: port our generated-code carve-outs, skip the leaked `BannedSymbols.txt` reference, defer `GenerateDocumentationFile=true`, pin `AnalysisLevel=10.0` explicitly (to sidestep [`dotnet/sdk#52467`](https://github.com/dotnet/sdk/issues/52467)), and stage rule escalation to avoid build breaks under `TreatWarningsAsErrors=true`.
4. **Does Deniz's Native AOT hypothesis ("static lib embedding helps DllImports") hold?** Directionally right but misidentifies the mechanism. The P/Invoke win comes from `<DirectPInvoke>`, **not** static linking. `<NativeLibrary>` static archives are a separate, much more expensive feature. FNA — the SDL2 consumer that has shipped NativeAOT in production for years — explicitly keeps SDL2 dynamic. No one in the NuGet ecosystem ships statically-AOT-linked SDL2/Skia/Silk.NET. **Split into Phase 6a (cheap DirectPInvoke win, ~days) and Phase 6b (static linking, parking-lot indefinitely).**

The overall modernization is carved into nine independently-committable stages (M0–M8) spanning ~5–7 focused working days of implementation. Each stage is validatable and revertable in isolation.

## 2. Current Repository Snapshot (2026-04-20 baseline)

| Axis | Current value | Source |
|---|---|---|
| SDK pin | `9.0.200`, `rollForward: latestFeature`, no prerelease | [`global.json`](../../global.json) |
| `$(LatestDotNet)` | `net9.0` | [`Directory.Build.props:16`](../../Directory.Build.props) |
| `LibraryTargetFrameworks` | `net9.0;net8.0;netstandard2.0;net462` | Same, line 17 |
| `ExecutableTargetFrameworks` | `net9.0;net8.0;net462` | Same, line 18 |
| `LangVersion` | `13.0` (uniform) | Same, line 23 |
| Analyzer posture | `AnalysisLevel=latest` + `AnalysisMode=All` + `TreatWarningsAsErrors=true` + `Features=strict` | Same, lines 49–57 |
| AOT/Trim | `IsAotCompatible=IsTrimmable=true` on non-net462/netstandard2.0 rows | Same, lines 65–69 |
| Package management | Central (`ManagePackageVersionsCentrally=true`) | Same, line 62 |
| vcpkg baseline | `0b88aacde46a853151730fbe7d0b7ee45f4b6864` (2026-04-11, 9 days old at review time) | [`vcpkg.json:4`](../../vcpkg.json) |
| vcpkg SDL2 family | `2.32.10` / `2.8.8#2` / `2.8.1#2` / `2.24.0` / `1.0.4#11` / `2.2.0#3` | [`vcpkg.json:56-87`](../../vcpkg.json) |
| Cake build host | `Cake.Frosting 6.1.0` (already latest); single-TFM tracks `$(LatestDotNet)` | [`build/_build/Build.csproj`](../../build/_build/Build.csproj) |
| Build-host tests | ~340 TUnit tests green at baseline (verified 2026-04-20) | [`build/_build.Tests/`](../../build/_build.Tests/) |
| Result wrapper library | `OneOf 3.0.271` + `OneOf.Monads 1.21.0` + `OneOf.SourceGenerator 3.0.271` | [`Directory.Packages.props:19-21`](../../Directory.Packages.props) |

Baseline `dotnet build` + `dotnet test build/_build.Tests` both green at session start. **Nothing in this modernization is fixing a broken system — all findings are optional improvements, drift closures, or supply-chain hygiene.**

## 3. Research Track 1 — .NET 10 / C# 14 Feasibility

### 3.1 .NET 10 SDK status

| Claim | Evidence | Confidence |
|---|---|---|
| .NET 10 is LTS; GA 2025-11-11; support through 2028-11-10 | [`Announcing .NET 10`](https://devblogs.microsoft.com/dotnet/announcing-dotnet-10/) | High |
| .NET 9 is STS (extended to **2 years**); EOL 2026-11-10 — **not an imminent forcing function** | Microsoft support-policy update; Deniz corrected the initial urgency framing | High |
| Current SDK patch band is `10.0.2xx`; `10.0.202` is the April 2026 Patch Tuesday release | `NetAnalyzers 10.0.202` (2026-04-14) on NuGet confirms the feature-band increment | High |
| Cake.Frosting 6.1.0 targets `net10.0` natively (published 2026-03-01) | [Cake v6.1.0 release notes](https://cakebuild.net/blog/2026/03/cake-v6.1.0-released); [`Cake.Frosting` on NuGet](https://www.nuget.org/packages/Cake.Frosting) | High |

### 3.2 C# 14 breaking changes (repo-relevant)

Per [C# 14 compiler breaking changes](https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/breaking-changes/compiler%20breaking%20changes%20-%20dotnet%2010):

| Break | Impact potential | Repo exposure |
|---|---|---|
| `field` contextual keyword inside property accessors (CS9258/CS9272) | High in general | **Zero hits.** 5 matches in grep, all are `foreach` loop variables / string literals / doc comments. None inside property accessors. |
| `extension` / `scoped` / `partial` reserved as contextual | Medium | **Zero hits** in src/, build/_build/, build/_build.Tests/, tests/. |
| Span overload resolution flip (`ReadOnlySpan<T>` preferred over `Span<T>`) | Low without hot-path span interop | Not exercised; no runtime-covariant-array pattern. |
| **`Enumerable.Reverse(this T[])` overload trap** — when compiling C# 14 + targeting net462/netstandard2.0, `arr.Reverse()` may bind to `MemoryExtensions.Reverse` (in-place, void return) instead of `Enumerable.Reverse` (non-destructive iterator) | High — Microsoft calls this an "unsupported configuration" | **Zero `.Reverse(` call sites in the entire repo.** Trap is theoretically present (we reference `System.Memory 4.6.3` for net462/netstandard2.0), but unreachable. |

### 3.3 New analyzer wave

- Only one new default-enabled rule in .NET 10: **CA2023** (invalid braces in message template, Reliability/Warning). Source: [code-analysis overview — .NET 10 tab](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/overview?tabs=net-10).
- `AnalysisMode=All` automatically opts into every rule in NetAnalyzers 10.x; full delta not enumerated but additive on top of .NET 9 baseline.
- **IL3058 / IL2125** trim/AOT verification rules introduced (opt-in via `VerifyReferenceAotCompatibility=true` / `VerifyReferenceTrimCompatibility=true`). Defaults remain off; only relevant if we later explicitly enable reference verification. Source: [`dotnet/runtime#117712`](https://github.com/dotnet/runtime/issues/117712), [trim-library prep docs](https://learn.microsoft.com/en-us/dotnet/core/deploying/trimming/prepare-libraries-for-trimming).

### 3.4 Other .NET 10 break vectors relevant to this repo

| Break | Repo exposure |
|---|---|
| `$(DefineConstants.Contains('...'))` no longer resolves at evaluation time | Safe. Repo uses `'$(TargetFramework)' == '...'` shape throughout. |
| `dotnet pack` excludes packages with no runtime assets from deps.json | Needs review on `.Native` packages (which are deliberately build-output-free) — G48 guardrail probably already covers the shape we want. |
| `DllImportSearchPath.AssemblyDirectory` narrower | Safe. No usage. |
| Single-file apps no longer search exe directory for native libs | N/A — consumers framework-dependent. |
| `System.Text.Json` conflicting `[JsonPropertyName]` validation | Low risk; manifest DTOs audited. |
| `dotnet restore` transitive audit (NU1902/1903/1904) under `TreatWarningsAsErrors=true` | **Real risk.** First restore under .NET 10 SDK + existing `TreatWarningsAsErrors=true` could break CI until `NuGetAudit` / `NuGetAuditMode` / `NuGetAuditLevel` tuned. Addressed in M0 pre-flight. |

### 3.5 Verdict

Uniform `LangVersion=14.0` across all TFMs is safe for **today's codebase**. No TFM-conditional `LangVersion` split needed. A dev-note in `docs/playbook/` preserving the empirical audit protects future contributors.

## 4. Research Track 2 — NuGet Package Landscape

Full data in per-package form (dates, breaking change summaries, .NET 10 TFM support) is captured in the session's research agent output. Condensed recommendation matrix:

| Package | Current | Recommended target | Classification |
|---|---|---|---|
| Meziantou.Analyzer | 2.0.189 | **3.0.50** (major) | Adopt With Guardrails — stage new rules via `.editorconfig` |
| Microsoft.CodeAnalysis.BannedApiAnalyzers | 3.3.4 | 3.3.4 | Stay — 3.12.x is beta only |
| Microsoft.CodeAnalysis.NetAnalyzers | 9.0.0 | **10.0.202** | Adopt Now — SDK-aligned |
| Microsoft.VisualStudio.Threading.Analyzers | 17.13.61 | **17.14.15** | Adopt Now |
| Roslynator.Analyzers | 4.13.1 | **4.15.0** | Adopt Now (lockstep with Formatting) |
| Roslynator.CodeAnalysis.Analyzers | 4.13.1 | **DROP** (template omits) | Defensible drop — we have no `Microsoft.CodeAnalysis.*` consumers |
| Roslynator.Formatting.Analyzers | 4.13.1 | **4.15.0** | Adopt Now (lockstep) |
| SecurityCodeScan.VS2019 | 5.6.7 | **DROP** | Project dormant since 2022-09; overlap with CA3xxx/CA5xxx + Sonar covers gap |
| SonarAnalyzer.CSharp | 10.7.0.110445 | **10.23.0.137933** | Adopt With Guardrails — 16 minor versions; rule-behavior changes between minors are maintainer policy |
| Microsoft.SourceLink.GitHub | 8.0.0 | **10.0.202** | Adopt Now — SDK-aligned |
| MinVer | 7.0.0 | Stay | Current stable; works with SDK 10+ |
| Cake.Frosting | 6.1.0 | Stay | Already latest; targets net10.0 natively |
| Cake.Testing | 6.1.0 | Stay | Lockstep with Frosting |
| NuGet.Frameworks | 7.3.0 | **7.3.1** | Adopt Now — patch bump |
| NuGet.Versioning | 7.3.0 | **7.3.1** | Adopt Now (lockstep) |
| OneOf | 3.0.271 | Stay | Kept — DU foundation retained |
| **OneOf.Monads** | **1.21.0 (suspicious)** | **REMOVE** | In-house `Result<TError, T>` struct replaces it — see Track 5 |
| OneOf.SourceGenerator | 3.0.271 | Stay | Paired with OneOf |
| PolySharp | 1.15.0 | Stay | Current; net462/netstandard2.0 polyfills still required |
| System.CommandLine | **2.0.0-beta4.22272.1** (2022 beta) | **2.0.6 GA** | Adopt With Guardrails — API rewrite, see M2 |
| System.CommandLine.NamingConventionBinder | 2.0.0-beta4.22272.1 | **REMOVE** (package deprecated on NuGet) | Forced by SCL 2.0 migration — manual option binding in new shape |
| System.Memory | 4.6.3 | Stay | Needed for net462/netstandard2.0 support |
| System.Runtime.CompilerServices.Unsafe | 6.1.2 | Stay | Same |
| TUnit | 1.33.0 | **1.37.0** | Adopt Now — no breaking `[Test]`/`Assert.That` changes in window |
| NSubstitute | 5.3.0 | Stay | 6.0.0-rc exists, defer until GA |
| Microsoft.TestPlatform.ObjectModel | 17.11.1 | **18.4.0** (major) | Adopt With Guardrails — net462-only row, low blast |

### 4.1 Critical finding: System.CommandLine 2.0 GA + binder deprecation

Source: [System.CommandLine beta5+ migration guide](https://learn.microsoft.com/en-us/dotnet/standard/commandline/migration-guide-2.0.0-beta5), [System.CommandLine on NuGet](https://www.nuget.org/packages/System.CommandLine), [NamingConventionBinder on NuGet](https://www.nuget.org/packages/System.CommandLine.NamingConventionBinder).

- `System.CommandLine 2.0.0` shipped GA on 2025-11-11; current `2.0.6` dated 2026-04-14.
- `System.CommandLine.NamingConventionBinder` is **marked "no longer maintained"** on NuGet. There is no GA successor package — the binder is being retired in favor of manual `ParseResult.GetValue<T>("--name")` binding inside action handlers.
- Surface breaks: `Command.SetHandler` → `Command.SetAction`; `ICommandHandler` removed → `SynchronousCommandLineAction` / `AsynchronousCommandLineAction`; `InvocationContext` removed → `ParseResult` passed directly; `AddOption/AddArgument/AddAlias/AddValidator` removed → mutable collection properties; `CommandLineBuilder` + `AddMiddleware` → `ParserConfiguration` + `InvocationConfiguration`; `IConsole` removed → `InvocationConfiguration.Output/.Error`; `Option<T>` ctor now requires name as first argument (silent break on old alias-in-description calls).

### 4.2 Critical finding: OneOf.Monads 1.21.0 provenance

- Public NuGet shows `1.20.0` (2022-08-25) as the latest release of `OneOf.Monads`.
- Repo pins `1.21.0`. Local `dotnet list package` confirms it resolves — cached, or present on a private feed we don't control directly.
- Supply-chain smell. Adopting an in-house Result struct (Track 5) closes this hole permanently.

## 5. Research Track 3 — vcpkg Baseline + Overlays

Full evidence in the session's vcpkg research agent output.

### 5.1 Baseline state

| Item | Value | Verdict |
|---|---|---|
| Current baseline | `0b88aacd…` (2026-04-11 18:50 UTC) | 9 days old at review time |
| vcpkg master HEAD | `256acc640…` (2026-04-18) | Optional refresh target; ~60 commits delta, no SDL-related churn |
| sdl2 port version | `2.32.10#0` matches master | Stay |
| sdl2-image | `2.8.8#2` matches master (upstream is at 2.8.10 but vcpkg port has not caught up) | **Defer** — cannot override past port build |
| sdl2-mixer | `2.8.1#2` matches master | Stay |
| sdl2-ttf | `2.24.0#0` matches master | Stay |
| sdl2-gfx | `1.0.4#11` matches master (upstream frozen) | Stay |
| sdl2-net | `2.2.0#3` matches master (upstream dormant) | Stay |
| LGPL-free feature set (sdl2-mixer) | Intact in current master port | No drift |
| Open SDL-family port bugs | Zero in last 90 days | Clean |

### 5.2 SDL2 maintenance-mode note

SDL2 is officially in maintenance mode since 2.28 (June 2023). Current 2.32.10 is likely the effective terminal release absent a CVE. Source: [Phoronix announcement](https://www.phoronix.com/news/SDL2-To-Maintenance-Mode). `sdl2-compat` (SDL2-API-on-SDL3 shim) is seeing active 2.32.66+ releases but the native 2.x tree is quiet.

### 5.3 Overlay triplets + ports audit

- `_hybrid-common.cmake` is port-agnostic. Regex pattern on line 28 (`sdl2|sdl2-image|sdl2-mixer|sdl2-ttf|sdl2-gfx|sdl2-net`) is extensible to `sdl3*` when Phase 5 lands. No baseline bump re-sync needed.
- Per-RID triplets are thin shell around the common fragment. Stable.
- `vcpkg-overlay-ports/sdl2-mixer/` (LGPL-free overlay #84): `2.8.1#2` unchanged in vcpkg master → overlay still aligned, no re-sync needed.
- `vcpkg-overlay-ports/sdl2-gfx/` (Unix visibility patch): `1.0.4#11` unchanged in vcpkg master → overlay still applies.
- `vcpkg-overlay-ports/mpg123/` is marked DEPRECATED (LGPL-free dropped the mpg123 feature). Safe to delete; tracked separately.
- **PD-15 regression guard gap** — no automated CI check asserts the sdl2-gfx visibility patch's symbols remain `GLOBAL DEFAULT` after a baseline bump. Addressed in M7.

## 6. Research Track 4 — Analyzer + .editorconfig Template Alignment

Template dropped in `artifacts/temp/` (for comparison only; sourced from Deniz's MCP/Aspire project template).

### 6.1 Industry-standard alignment check (verdict: largely YES, two caveats)

**Aligned with Microsoft 2026 guidance:**

- `AnalysisMode=All` + `AnalysisLevel=latest` + `EnforceCodeStyleInBuild=true` — matches [code-analysis overview](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/overview) recommendation.
- `DebugType=embedded` — modern SourceLink guidance.
- NetAnalyzers 10.x bump surfaces CA1871 (`ThrowIfNull` nullable-struct) + CA1872 (`Convert.ToHexString`). Template enables both explicitly.
- Collection-expression enforcement (IDE0300–IDE0305) — .NET 8+/C# 12+ modern baseline.
- Test-project glob pattern (`[{*Tests/**/*.cs,tests/**/*.cs}]`) relaxing `CA1707` — removes per-project suppression boilerplate.
- De-duplicates our current `.editorconfig`'s conflicting entries (CA1819, CA1027, CA1507, VSTHRD111 each have duplicate severity lines today).

**Known bug caveat:**

- `AnalysisLevel=latest` currently pins to .NET 9 rules on .NET 10 SDK due to a missed increment in `Microsoft.NET.Sdk.Analyzers.targets`. Source: [`dotnet/sdk#52467`](https://github.com/dotnet/sdk/issues/52467). **M5 will pin `AnalysisLevel=10.0` explicitly** to sidestep this.

**Template-specific misalignments for this repo (non-negotiable tailorings):**

1. **Generated-code carve-outs are missing** — template has no equivalent of our current:

   ```
   [external/sdl2-cs/src/**]
   [src/SDL2.Core/SDL2.cs]
     dotnet_analyzer_diagnostic.severity = none
     generated_code = true
   ```

   `external/sdl2-cs/src/SDL2.cs` is **8,966 lines** of auto-imported binding code. Without carve-outs, `TreatWarningsAsErrors=true` drowns the build in analyzer errors. **Must port.**

2. **`GenerateDocumentationFile=true` + missing NoWarn** — template enables doc-file generation but omits `CS1591;CS1573;CS1572;CS1574` from `NoWarn`. Under `TreatWarningsAsErrors=true` our 5 binding projects + Cake host emit hundreds of missing-XML-doc errors. **Do not adopt yet.** Schedule separately when Phase 4 CppAst generator lands (generated bindings can emit XML comments).

3. **`BannedSymbols.txt` leaked reference** — template ships:

   ```xml
   <AdditionalFiles Include="$(MSBuildThisFileDirectory)BannedSymbols.txt" Link="Properties/BannedSymbols.txt"/>
   ```

   but no `BannedSymbols.txt` file exists in `artifacts/temp/`. MSBuild tolerates missing `AdditionalFiles`, but `Microsoft.CodeAnalysis.BannedApiAnalyzers` has nothing to check. **Omit the ItemGroup until we have a real banned-API list.**

4. **IDE0300–IDE0305 (collection expressions) at `error`** is risky for netstandard2.0/net462 rows. Collection-expression lowering paths differ across TFMs and can surprise on older BCL constraints. **Start at `suggestion`, promote to `error` after behavioral verification.**

5. **`dotnet_analyzer_diagnostic.category-roslynator.severity = error`** (template-enabled) would surface an estimated 50–300 RCS hits on first build. **Start at `warning`, triage, then escalate.**

### 6.2 Template analyzer-package delta

Template adds: NetAnalyzers 10, VSTHRD 17.14, Roslynator 4.15, Sonar 10.20, Meziantou 3.x.
Template drops: `Roslynator.CodeAnalysis.Analyzers` (only useful for `Microsoft.CodeAnalysis.*`-consuming projects — we have none), `SecurityCodeScan.VS2019` (3.5-year dormancy, branding drift, CA3xxx/CA5xxx overlap).

### 6.3 Template is wrong-fit source for TUnit, System.CommandLine, native-package posture

Ignore template versions for: TUnit (template: 1.12.65; ours: 1.33.0, want 1.37.0), all non-analyzer runtime packages (Aspire, OTel, MCP, Algolia, Moq, AutoFixture, Testcontainers — all irrelevant to this repo). **Scope: analyzer posture + .editorconfig + select Directory.Build.props properties only.**

## 7. Research Track 5 — OneOf.Monads → In-House Result Pattern

### 7.1 Current repo measurements

| Surface | Count |
|---|---|
| Sealed Result subclasses (`: Result<TError, TSuccess>(...)`) under `Domain/*/Results/` | **17** |
| Call sites using `IsError()/ErrorValue()/SuccessValue()/AsT0/AsT1` | **131** across **23 files** |
| Accumulating validators (domain-aggregate accumulation pattern) | **2** (`PackageOutputValidator`, `VersionConsistency*` family) |

### 7.2 Third-party library survey (verdicts)

| Library | Latest | Verdict |
|---|---|---|
| **ErrorOr** (`amichai/error-or`) | 2.0.1 (2024-03-26) | **Reject** — no `TError` generic, collapses all errors to a single `Error` shape, breaks BuildError / PackagingError / PreflightError / HarvestingError hierarchy |
| **FluentResults** | 4.0.0 (2025-06-29) | **Reject** — pulls `Microsoft.Extensions.Logging.Abstractions`, class-based, violates minimum-deps principle |
| **CSharpFunctionalExtensions** | 3.7.0 (2026-03-02) | **Fallback only** — closest API shape, keeps `Result<T, E>` generic, zero deps, but class-based (one alloc per Result) and no aggregation primitive |
| **Ardalis.Result** | 10.1.0 (2024-10-28) | **Reject** — ASP.NET-first, no strong `TError` discrimination |
| **LanguageExt.Core** | 5.0.0-beta-35 | **Reject** — imports an entire FP runtime (HKT, monad transformers), AOT concerns, overkill |
| **Dunet** | 1.16.1 (2026-02-16) | **Reserve** — source-generated DU, active, AOT-clean; could replace OneOf+OneOf.Monads together if we ever want that path |

### 7.3 Language-level DU status

- [`dotnet/csharplang#8928`](https://github.com/dotnet/csharplang/issues/8928) in Working Set. Three champions (333fred, MadsTorgersen, mattwar).
- C# 14 shipped November 2025 **without** discriminated unions.
- Realistic target: **C# 15 (Nov 2026)** at the earliest; C# 16 (Nov 2027) equally plausible given open questions in [`proposals/unions.md`](https://github.com/dotnet/csharplang/blob/main/proposals/unions.md).
- Not shipping soon enough to wait. When it ships, migration to native `union` is a second, independent step.

### 7.4 In-house `Result<TError, T>` prototype

```csharp
namespace Build.Domain.Results;

/// <summary>
/// Error-first discriminated result. TError is left-hand (OneOf.Monads convention preserved).
/// Struct to keep allocation-free on the hot path.
/// </summary>
public readonly struct Result<TError, T>
    where TError : class            // BuildError hierarchy
    where T : notnull
{
    private readonly TError? _error;
    private readonly T? _value;
    private readonly bool _isError;

    private Result(TError error)  { _error = error; _value = default; _isError = true; }
    private Result(T value)       { _error = null;  _value = value;   _isError = false; }

    public bool IsError   => _isError;
    public bool IsSuccess => !_isError;

    public TError ErrorValue()   => _isError ? _error! : throw new InvalidOperationException("Not an error.");
    public T      SuccessValue() => _isError ? throw new InvalidOperationException("Not a success.") : _value!;

    // Deniz requirement 1: implicit + explicit both directions
    public static implicit operator Result<TError, T>(TError error) => new(error);
    public static implicit operator Result<TError, T>(T value)      => new(value);
    public static explicit operator TError(Result<TError, T> r)     => r.ErrorValue();
    public static explicit operator T     (Result<TError, T> r)     => r.SuccessValue();

    // Analyzer-preserved From*/To* surface (per feedback_result_pattern_surface.md memory)
    public static Result<TError, T> FromError  (TError error) => error;
    public static Result<TError, T> FromSuccess(T value)      => value;
    public static TError ToError  (Result<TError, T> r) => r.ErrorValue();
    public static T      ToSuccess(Result<TError, T> r) => r.SuccessValue();

    // OneOf interop — bidirectional, lets us retain OneOf for genuine 3+-arm DU cases
    public OneOf<Error<TError>, Success<T>> ToOneOf() =>
        _isError ? new Error<TError>(_error!) : new Success<T>(_value!);

    public static Result<TError, T> FromOneOf(OneOf<Error<TError>, Success<T>> source) =>
        source.Match<Result<TError, T>>(e => e.Value, s => s.Value);

    // Fluent surface
    public TOut Match<TOut>(Func<TError, TOut> onError, Func<T, TOut> onSuccess)
        => _isError ? onError(_error!) : onSuccess(_value!);

    public Result<TError, TOut> Map<TOut>(Func<T, TOut> selector) where TOut : notnull
        => _isError ? new Result<TError, TOut>(_error!) : new Result<TError, TOut>(selector(_value!));

    public Result<TError, TOut> Bind<TOut>(Func<T, Result<TError, TOut>> binder) where TOut : notnull
        => _isError ? new Result<TError, TOut>(_error!) : binder(_value!);
}
```

### 7.5 Accumulating validator pattern preservation

Both `PackageOutputValidator` and `VersionConsistency*` already factor accumulation into the **domain aggregate** (`PackageValidation`, `VersionConsistencyValidation`) rather than the Result wrapper. This is the correct design — no library on the market handles it more cleanly. The Result migration preserves this shape verbatim:

```csharp
public sealed class PackageValidationError : PackagingError
{
    public PackageValidation Validation { get; }
    // unchanged — aggregate carries the List<PackageValidationCheck>
}

// Consumer remains:
return validation.HasErrors
    ? PackageValidationResult.Fail(validation)
    : PackageValidationResult.Pass(validation);
```

### 7.6 Source generator build-vs-buy

- Realistic in-house effort: 3–5 focused days + 0.5 day/year Roslyn API drift.
- Reference implementations: Dunet, Vogen, StronglyTypedId, Riok.Mapperly, OneOf.SourceGenerator.
- Justified if ≥25 Result types OR repeated boilerplate pain.
- **Current count: 17.** Defer.

### 7.7 Migration cost

- 1–2 focused days, mostly mechanical (namespace `using` swap + sealed-class ctor signature).
- Existing sealed-class façade isolates call sites from OneOf.Monads generic internals — most of the 131 call sites need zero edits.
- Full 340-test suite covers regression.

## 8. Research Track 6 — Native AOT Future-Feasibility

### 8.1 The mechanism Deniz's hypothesis refers to

Two independent MSBuild items:

```xml
<ItemGroup>
  <DirectPInvoke Include="SDL2" />
  <NativeLibrary Include="SDL2.lib" Condition="$(RuntimeIdentifier.StartsWith('win'))" />
  <NativeLibrary Include="libSDL2.a" Condition="!$(RuntimeIdentifier.StartsWith('win'))" />
</ItemGroup>
```

- **`<DirectPInvoke>`** is what "helps DllImports." ILC replaces the runtime P/Invoke lookup with an OS-level direct reference. **Library can stay dynamic.** This is the cheap, effective win.
- **`<NativeLibrary>`** is a separate opt-in consuming a `.a`/`.lib` at consumer publish time. Only runs under `dotnet publish /t:PublishAot`.
- Source: [Native code interop with Native AOT](https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/interop).

### 8.2 Why we cannot "embed static native libs inside .NET assemblies" as library authors

- Library-project Native AOT publish **only produces shared libraries**, not static archives. "Static libraries are not officially supported and may require compiling Native AOT from source" — [Building native libraries](https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/libraries).
- Static linking must happen at the **consumer's** AOT-publish step, not at our NuGet pack time.

### 8.3 Ecosystem precedent

| Project | AOT pattern |
|---|---|
| **FNA-XNA** (SDL2 + NativeAOT in production for years) | Dynamic SDL2 + `DirectPInvoke` + `DirectPInvokeList` (SDLApis.txt). [Explicitly recommends against static linking.](https://fna-xna.github.io/docs/appendix/Appendix-A:-NativeAOT-on-PC/) |
| SkiaSharp | Dynamic `.dll`/`.so`/`.dylib`. No static NuGet. Community builds own static libs. |
| Silk.NET 2.18+ | AOT-compat via SilkTouch marshaller. Dynamic native loads. |
| LibGit2Sharp | Dynamic. No AOT static path. |
| Avalonia | [Feature request #9503](https://github.com/AvaloniaUI/Avalonia/issues/9503) open since 2022 — unmerged. |

**No NuGet-distributed SDL/graphics/game binding library ships statically-AOT-linked native deps today.**

### 8.4 Tradeoff ranking

| Dimension | Dynamic + DirectPInvoke (FNA model) | Full static link |
|---|---|---|
| Perf | Inlined marshalling stubs; first-call resolve eliminated | Same + single-file exe |
| Binary size per exe | ≈0 | +3–10 MB |
| 7-RID matrix | Current structure unchanged | Per-RID `.a`/`.lib` overlay-triplet rewrite needed |
| Consumer dev loop | Works as today | Needs both dynamic (build/debug) and static (publish) payloads per RID |
| Debuggability | Step-into with PDBs | Lost (stripped static archives) |
| `.Core` + `.Image` collision | N/A | Real risk: both shipping `libSDL2.a`, linker dedupe fragile under version drift |
| Ecosystem fit | Matches precedent | Pioneer territory |

### 8.5 Proposed phasing (deferred; not part of this modernization batch)

- **Phase 6a — "AOT-friendly dynamic"** (days of work, ~95% of the benefit): Ship `<DirectPInvoke>` in `buildTransitive/Janset.SDL2.*.targets` guarded by `PublishAot=true`. Add per-library `DirectPInvokeList` satellite file. Hybrid Static + Dynamic Core strategy intact.
- **Phase 6b — "Optional static payload"** (weeks-to-months, pioneer risk): Separate `Janset.SDL2.*.NativeAssets.Static.{rid}` opt-in packages; overlay-triplet rewrite; per-RID link matrix validation. **Parking-lot indefinitely until a consumer asks for single-file deployment.**

**Per Deniz's 2026-04-20 direction: both 6a and 6b defer to the last phases. Not included in the M-stage plan below.**

## 9. Empirical Repository Audits (2026-04-20)

Local grep audits done during the session, evidence that the "general case" C# 14 / .NET 10 risks do not fire here:

| Audit | Result |
|---|---|
| `\.Reverse\(` across all `*.cs` in repo | **0 hits** |
| `\bfield\b` inside property accessors (vs string / doc / loop var) | **0 hits** (5 total matches; all safe contexts) |
| `\b(extension|scoped|partial)\b` as identifier (param / local / field) | **0 hits** |
| `$(DefineConstants.Contains(...))` in MSBuild conditions | **0 hits** (repo uses `'$(TargetFramework)' == '...'` style) |
| `DllImportSearchPath.AssemblyDirectory` usage | **0 hits** |
| `BufferedStream.WriteByte` usage | **0 hits** |

**OneOf.Monads `1.21.0` provenance:** resolved from local cache per `dotnet list package`. Public nuget.org shows `1.20.0` as latest. Origin unclear — out of scope for M-stage implementation; M6 removes the dependency entirely.

**vcpkg overlay ports review:** `sdl2-mixer` (LGPL-free) and `sdl2-gfx` (Unix visibility) overlays still align with master `256acc6` — no re-sync required on baseline bump. `mpg123` overlay marked DEPRECATED; can be deleted standalone.

**Documentation drift (summary from dedicated drift-agent sweep):**

| Doc | Stale claim | Current reality |
|---|---|---|
| `docs/plan.md:172,178` + `docs/onboarding.md:235` | "241 / 247 / 273 / 324 tests" | ~340 tests (2026-04-20) |
| `docs/onboarding.md:57` | "SDL2 (2.32.4, latest: 2.32.10)" | 2.32.10 pinned |
| `docs/knowledge-base/cake-build-architecture.md:59-75` | ASCII tree with top-level `Modules/` + `Tools/` | ADR-002 shape: `Application/ + Domain/ + Infrastructure/ + Tasks/` + `Infrastructure/Tools/` |
| `docs/knowledge-base/harvesting-process.md:53-54` | "obtained from BuildContext, which gets it from `runtimes.json`" | Merged into `manifest.json` schema v2.1 |
| `docs/knowledge-base/ci-cd-packaging-and-release-plan.md:188-191,336` | Prose lists `build/runtimes.json` as loadable | Same — process prose out of sync |
| `docs/phases/phase-4-binding-autogen.md:94` | "[LibraryImport] (net7.0+)" imprecise | Current TFM set has net9.0 top |
| `docs/reference/cake-frosting-build-expertise.md:390,483,498` | Cake.Frosting 5.0.0 examples; future-dated "May 1, 2025" citations | CPM pins 6.1.0 |

Classification: mechanical (test-count / version / ASCII-tree) are autonomous-safe. Behavioral prose edits (`harvesting-process.md`, `ci-cd-packaging-and-release-plan.md`) need Deniz review.

## 10. Discussion Arc (Session 2026-04-20)

### 10.1 Initial proposal (agent)

- Opened with `.NET 9 EOL 2026-05-12 (~3 weeks)` as a Critical / time-critical driver.
- Recommended TFM-conditional `LangVersion` split (14 on modern TFMs, 13 on net462/netstandard2.0) to sidestep the `Enumerable.Reverse` trap.
- Proposed isolating the System.CommandLine 2.0 migration to `Program.cs` only as a minimal-surface change.
- Deferred OneOf.Monads investigation; deferred Native AOT; bundled analyzer bumps + posture changes in one stage.

### 10.2 Deniz corrections + direction (2026-04-20)

1. **".NET 9 support stays."** STS was extended to 2 years; EOL is 2026-11-10, not 2026-05-12. Urgency framing was wrong. .NET 9 remains in `LibraryTargetFrameworks`.
2. **"Uniform LangVersion=14."** Project is not yet live, in active development. Migrate cleanly, discuss and mitigate net462/netstandard2.0 edge cases rather than TFM-split the language version.
3. **"System.CommandLine full conversion — not isolated."** Migrate to best practices, don't wrap or shim. Active development means we don't carry legacy orchestration forward.
4. **"Consider the new analyzer + editorconfig drop."** Template in `artifacts/temp/` from Deniz's modern .NET 10 project template should be evaluated. Focus analyzer packages + .editorconfig only; ignore runtime-package differences.
5. **"Modern Result pattern — keep OneOf, consider dropping OneOf.Monads, consider in-house source generator."** Research requirements: explicit/implicit conversion, error distinction, T0/T1 wrap.
6. **"vcpkg baseline — keep up, but verify custom overlays."**
7. **"Docs drift sweep — do it."**
8. **"Executable TFM legacy refs — reason is .NET Framework support; keep as-is."**
9. **"Native AOT can embed static native libs — research for later stages."**

### 10.3 Final consolidated direction (this document)

All inputs applied:

- `.NET 9` stays; `net10.0;net9.0;net8.0;netstandard2.0;net462` for libraries.
- Uniform `LangVersion=14.0`; empirical audit proved the multi-target traps are dormant here.
- System.CommandLine 2.0 full rewrite, not isolation.
- Tailor-then-adopt analyzer + `.editorconfig` (template + repo-specific carve-outs).
- In-house `Result<TError, T>` struct replaces OneOf.Monads; no source generator (defer).
- vcpkg baseline refresh + PD-15 sdl2-gfx visibility guardrail landing together.
- Documentation drift sweep in autonomous-safe subset + review-needed subset.
- Native AOT → Phase 6a (cheap) and Phase 6b (pioneer) — **both deferred to a later phase, out of scope for this modernization wave.**

## 11. Staged Implementation Plan (M0–M8)

Every stage is:

- Independently committable (per [`feedback_no_scope_creep_on_critical_findings.md`](../../../memory/feedback_no_scope_creep_on_critical_findings.md))
- Independently validatable
- Independently revertable
- Gated by explicit Deniz approval before implementation per [`AGENTS.md`](../../AGENTS.md)

### M0 — Pre-flight preparation (~30 min; read-only + one audit-config change)

**Scope:**

- Cold-cache restore confirmation: `dotnet nuget locals all --clear && dotnet restore` from a fresh state (tests that the OneOf.Monads 1.21.0 pin actually resolves from a supply-chain-clean starting point). Done without commit impact.
- Tune `NuGetAudit` / `NuGetAuditLevel` / `NuGetAuditMode` in `Directory.Build.props` **before** the SDK bump, so new NU1902/1903/1904 transitive-audit warnings don't brick CI under `TreatWarningsAsErrors=true`.

**Exit criteria:** repo builds clean; audit-tuning policy documented in `Directory.Build.props` comment; any supply-chain findings logged.

**Rollback:** revert `Directory.Build.props` audit block.

### M1 — .NET 10 TFM cascade + C# 14 uniform activation (largest stage by diff count)

> **Atomicity note:** This stage is deliberately the biggest single commit in the batch. TFM cascade, hardcoded-TFM test fixture updates, and workflow binary-path updates must land together so the CI matrix stays green after the bump. Splitting into sub-stages would create transient "SDK installed but binary path wrong" windows.

**Scope — `Directory.Build.props` + `global.json`:**

- `global.json` SDK → `10.0.202`.
- `$(LatestDotNet)` → `net10.0`.
- `LibraryTargetFrameworks` → `net10.0;net9.0;net8.0;netstandard2.0;net462` (**net9 retained**).
- `ExecutableTargetFrameworks` → `net10.0;net9.0;net8.0;net462`.
- `LangVersion` → `14.0` (uniform; empirical audit shows traps are dormant).
- **Deferred cosmetic:** `$(LatestDotNet)` → `$(CurrentNetVersion)` template-alignment rename. Defer unless we burn a doc-sweep cycle on the cascade (docs reference `LatestDotNet` by name in several places).

**Scope — hardcoded `net9.0` test fixture updates (9 hit points across 3 files, verified 2026-04-20 via grep):**

| File | Line | Pattern | Risk |
| --- | --- | --- | --- |
| `build/_build.Tests/Unit/Application/Packaging/PackageTaskRunnerTests.cs` | 76 | `["net9.0", "net8.0", "netstandard2.0", "net462"]` (expected TFM array) | Mechanical — add `net10.0` as first entry |
| `build/_build.Tests/Unit/Application/Packaging/PackageTaskRunnerTests.cs` | 225 | `new ProjectMetadata(["net9.0"], ...)` (single-TFM expectation) | Mechanical — flip to `net10.0` |
| `build/_build.Tests/Unit/Domain/Packaging/PackageOutputValidatorTests.cs` | 26 | `NuspecFrameworkGroups = [".NETFramework4.6.2", ".NETStandard2.0", "net8.0", "net9.0"]` | Mechanical — add `net10.0` |
| `build/_build.Tests/Unit/Domain/Packaging/PackageOutputValidatorTests.cs` | 29 | `CsprojTargetFrameworks = ["net9.0", "net8.0", "netstandard2.0", "net462"]` | Mechanical — widen |
| `build/_build.Tests/Unit/Domain/Packaging/PackageOutputValidatorTests.cs` | **130** | `group == "net9.0"` — **TFM-string logic branch** | **High attention — NOT mechanical replace.** Either flip branch to `== "net10.0"` or parameterize. Verify the test's intent (which TFM's nuspec group is under assertion) before editing. |
| `build/_build.Tests/Unit/Domain/Packaging/PackageOutputValidatorTests.cs` | 199 | `targetFrameworks: ["net9.0", "net8.0", "netstandard2.0"]` | Mechanical |
| `build/_build.Tests/Unit/Domain/Packaging/PackageOutputValidatorTests.cs` | **423** | `$"lib/net9.0/{managedPackageId}.pdb"` — **nuspec pack-output asset path assertion** | **High attention — verification required.** Before flipping to `lib/net10.0/`, confirm `dotnet pack` output under .NET 10 SDK actually emits `lib/net10.0/` (not `lib/net9.0/` lingering from a stale obj cache). If it does not, the test is masking real pack-behavior drift. Clean `bin/obj` + dry pack before trusting the edit. |
| `build/_build.Tests/Unit/Domain/Packaging/SmokeScopeComparatorTests.cs` | 110, 132, 154, 180, 200 | `<TargetFramework>net9.0</TargetFramework>` across 5 fixture XML blobs | Mechanical but verify each fixture's intent per-case — some may assert "Project X targets net9.0 specifically" (keep), others may test general behavior (flip). |

**Scope — workflow binary invocation path sweep (4 workflow files, verified 2026-04-20):**

| Workflow | Line | Current path | Action |
| --- | --- | --- | --- |
| `.github/workflows/prepare-native-assets-windows.yml` | 63 | `.\build\_build\bin\Release\net9.0\Build.exe --target Harvest` | Flip to `net10.0` |
| `.github/workflows/prepare-native-assets-linux.yml` | 118 | `./build/_build/bin/Release/net9.0/Build --target Harvest` | Flip to `net10.0` |
| `.github/workflows/prepare-native-assets-macos.yml` | 69 | `./build/_build/bin/Release/net9.0/Build --target Harvest` | Flip to `net10.0` |
| `.github/workflows/release-candidate-pipeline.yml` | 131, 241 | Example commands in comments referencing `net9.0/Build` | Low priority — update for consistency; see "pre-existing stub note" below |

**Scope — CI workflow SDK pin:**

- `actions/setup-dotnet@v4` `dotnet-version:` → `10.0.202` in every workflow file (the 4 workflows above plus any reusable composite actions under `.github/actions/`).

**Scope — new developer-facing documentation:**

- `docs/playbook/c-sharp-14-multi-target-hygiene.md` — record the empirical trap-free state of this repo (`.Reverse` / `field` in accessors / `extension|scoped|partial`) so future contributors know which C# 14 multi-target traps to watch for when adding new code.

**Scope — leave alone:**

- Cake host `Build.csproj` + `Janset.Smoke.{props,targets}` untouched (both track `$(LatestDotNet)` implicitly).
- `.github/prompts/dependency_modernization_and_net_10_updater_prompt_v_1.prompt.md` untouched — it's a modernization-meta prompt; naturally obsolete after M1 lands.

**Pre-existing stub note — RC pipeline:**

`release-candidate-pipeline.yml` carries hardcoded `net9.0` references only inside comment-level example commands (lines 131, 241). This pipeline is [already flagged as a stub in `docs/plan.md`](../plan.md) and is owned by Stream D-ci. M1 updates these comment references for consistency, but any deeper RC pipeline work — including finishing the stub — stays within Stream D-ci scope, not modernization scope. The modernization commitment is: "do not degrade the current half-broken state."

**Validation:**

- Full `dotnet build -c Release` at repo root across all 5 library TFMs.
- Full `dotnet test build/_build.Tests/Build.Tests.csproj -c Release` — all ~340 tests green under `net10.0` SDK.
- `dotnet cake --target=Package-Consumer-Smoke --rid=win-x64 --family=sdl2-core,sdl2-image --source=local` on the proof slice.
- **net462 executable runtime confirmation on Linux:** `PackageConsumer.Smoke` net462 TFM must still run under Mono/Wine via Cake orchestration. Recent commits wired `freepats` (Linux MIDI) + mono bridge — M1 must not regress that path. Confirm via the platform-conditional smoke row.
- **CI matrix wall-clock baseline:** adding `net10.0` to `ExecutableTargetFrameworks` expands executable matrix 3→4 and library matrix 4→5. Capture the new CI wall-clock baseline for future regression detection — increase should be linear, not step-function.
- Diagnostic audit: new warnings under `TreatWarningsAsErrors=true` triaged before commit.

**Rollback:** single revert of the Directory.Build.props + global.json + CI workflow + test-fixture changes (one commit, reversible).

### M2 — System.CommandLine 2.0 GA full conversion

**Scope:**

- `Directory.Packages.props`: `System.CommandLine 2.0.0-beta4.22272.1` → `2.0.6`; **remove** `System.CommandLine.NamingConventionBinder` (package deprecated).
- `build/_build/Program.cs`: rewrite entry point on GA API surface. `Command.SetAction` instead of `SetHandler`; `ParseResult.GetValue<T>("--name")` binding inside action handlers; `AsynchronousCommandLineAction` for the async root; `ParserConfiguration` + `InvocationConfiguration` replace `CommandLineBuilder` middleware; mutable `Options.Add`/`Subcommands.Add` collections replace `Add*` methods.
- Option classes (`CakeOptions`, `RepositoryOptions`, `DotNetOptions`, `VcpkgOptions`, `PackageOptions`, `DumpbinOptions`): refactored onto `Option<T>` with name-first constructor; ambiguous alias-in-description patterns fixed.
- Keep `ParsedArguments` record as a DTO; hydrate manually inside the root-command action.
- Add characterization test in `build/_build.Tests/Unit/Context/` that parses known-good argument vectors end-to-end — migration becomes mechanically verifiable.

**Validation:** every existing Cake target invocation surface: `Info`, `PreFlight-Check`, `Harvest`, `Consolidate-Harvest`, `Package`, `Package-Consumer-Smoke`, `Coverage-Check`, `SetupLocalDev --source=local`. Characterization test passes.

**Rollback:** revert Program.cs + option class changes; restore beta4 pin.

**Note:** Independent of M1. Can run in parallel or swap order.

### M3 — Analyzer package bumps (version-only, posture unchanged)

> **Operational constraint — CPM manual-edit workflow (applies to every M-stage that touches `Directory.Packages.props`):**
>
> This repo uses Central Package Management (`ManagePackageVersionsCentrally=true`). `dotnet package add` / `dotnet package update` does **not** do what its name suggests here — it tries to write a local `<PackageReference Version="...">` into the csproj, which CPM forbids. Version edits happen manually in `Directory.Packages.props` (and partially in `Directory.Build.props` for analyzer `PackageReference` metadata like `PrivateAssets`/`IncludeAssets`).
>
> **Workflow for version bumps:**
>
> 1. Survey with `dotnet list package --outdated --format json` (read-only, CPM-safe).
> 2. Edit `Directory.Packages.props` XML directly — bump `<PackageVersion Include="..." Version="..." />` entries.
> 3. Full `dotnet restore` to validate the new graph resolves; audit new NU1xxx/NU19xx warnings.
> 4. If `NuGet.Versioning` is already available in the build host, Cake can orchestrate the survey/validate loop but **not** the edit step — XML-level bumps stay manual.
>
> **Do not** attempt `dotnet add package` / `dotnet package update` mid-stage — they will either silently no-op, emit CPM error NU1008, or worse, inject a lockfile-style pin that diverges from CPM intent.

**Scope (CPM edits only, no rule/`.editorconfig` changes yet):**

| Package | From | To |
| --- | --- | --- |
| Meziantou.Analyzer | 2.0.189 | 3.0.50 |
| Microsoft.CodeAnalysis.NetAnalyzers | 9.0.0 | 10.0.202 |
| Microsoft.SourceLink.GitHub | 8.0.0 | 10.0.202 |
| Microsoft.VisualStudio.Threading.Analyzers | 17.13.61 | 17.14.15 |
| Roslynator.Analyzers | 4.13.1 | 4.15.0 |
| Roslynator.Formatting.Analyzers | 4.13.1 | 4.15.0 |
| SonarAnalyzer.CSharp | 10.7.0.110445 | 10.23.0.137933 |
| NuGet.Frameworks | 7.3.0 | 7.3.1 |
| NuGet.Versioning | 7.3.0 | 7.3.1 |
| TUnit | 1.33.0 | 1.37.0 |
| Microsoft.TestPlatform.ObjectModel (net462 only) | 17.11.1 | 18.4.0 |
| **Removed:** SecurityCodeScan.VS2019 | 5.6.7 | — |
| **Decision needed:** Roslynator.CodeAnalysis.Analyzers | 4.13.1 | drop or stay (Deniz's call) |

**Validation:** full build. **Expect new analyzer signal** (CA2023, CA1871, CA1872 from NetAnalyzers 10; RCS drift from Roslynator 4.15; Sonar across 16 minor versions; Meziantou MA01xx; TUnit0015/0049/0064/0016). Capture signal for M4 triage — **do not** resolve rule hits in M3; gate via `.editorconfig` changes in M4 instead.

**Rollback:** single CPM revert.

### M4 — `.editorconfig` rebuild (tailor-then-adopt)

**Scope:** Migrate to template's sectioned structure with repo-specific tailoring:

1. Port the generated-code carve-outs (`[external/sdl2-cs/src/**]`, `[src/SDL2.Core/SDL2.cs]`) — **non-negotiable**.
2. Add template's test-project glob `[{*Tests/**/*.cs,tests/**/*.cs}]` relaxing `CA1707`; remove per-project suppressions.
3. De-dupe current file's conflicting severity entries (CA1819, CA1027, CA1507, VSTHRD111).
4. Keep `CS1591;CS1573;CS1572;CS1574 = none` suppressions.
5. Add new template rules (IDE0040, IDE0041, CA1871, CA1872, S3963) but **gate IDE0300–IDE0305 at `suggestion`** for legacy-TFM safety (promote to `error` after verification).
6. Set `dotnet_analyzer_diagnostic.category-roslynator.severity = warning` (not `error`) initially; triage 50–300 expected RCS hits over follow-up iterations.
7. Triage M3 diagnostic signal into targeted `.editorconfig` severity overrides.

**Validation:** full build under `TreatWarningsAsErrors=true`. Residual warnings form triage backlog.

**Rollback:** restore previous `.editorconfig`.

### M5 — `Directory.Build.props` posture tightening

**Scope:**

- `AnalysisLevel` → explicit `10.0` (sidesteps [`dotnet/sdk#52467`](https://github.com/dotnet/sdk/issues/52467)).
- Add `DebugType=embedded` + `DebugSymbols=true`.
- Add `AccelerateBuildsInVisualStudio=true` (VS-only, zero CI impact).
- **Do not** add `GenerateDocumentationFile=true` (see §6.1 caveat 2). Schedule as a separate work item aligned with Phase 4 CppAst generator landing.
- **Do not** add `BannedSymbols.txt` ItemGroup (template's reference is leaked — no file exists). Revisit when a real banned-API list is authored.

**Validation:** full build + smoke run.

**Rollback:** revert Directory.Build.props changes.

### M6 — OneOf.Monads → in-house `Result<TError, T>`

**Scope (pilot-gated):**

1. Add `build/_build/Domain/Results/Result.cs` — the 60-line struct from §7.4. Struct-based (zero allocation), `TError : class` constraint aligned with `BuildError` hierarchy, full `From*/To*/Pass/Fail/implicit/explicit/Match/Map/Bind/ToOneOf/FromOneOf` surface preserved per [`feedback_result_pattern_surface.md`](../../../memory/feedback_result_pattern_surface.md) memory.
2. **Pilot migration (gate):** port `CopierResult` (smallest, `Unit` payload). Hypothesis to verify: the existing sealed-class façade isolates `IsError()/ErrorValue()/SuccessValue()/AsT0/AsT1`-consuming call sites from OneOf.Monads generic internals, so most call sites need no edits. **The pilot is the test of that hypothesis — not its confirmation.**
3. **Decision point after pilot:**
   - **Pilot green with zero call-site edits:** proceed to mechanical sweep of the remaining 16 Result subclasses + 23-file call-site surface. Namespace `using` changes + sealed-class ctor signature swap only.
   - **Pilot requires call-site edits:** M6 scope widens to include a mechanical call-site sweep phase; revise effort estimate (originally 1–2 days; add ~0.5–1 day per ~30 additional call sites needing touch). Re-ack with Deniz before proceeding.
4. Accumulating-validator domain aggregates (`PackageValidation`, `VersionConsistencyValidation`) unchanged.
5. Remove `OneOf.Monads` from `Directory.Packages.props` and `build/_build/Build.csproj`. Keep `OneOf` + `OneOf.SourceGenerator`.
6. Full test suite green (target ~340; exact count confirmed at pilot time).

**Validation:**

- `dotnet build -c Release` — all TFMs.
- `dotnet test build/_build.Tests/Build.Tests.csproj -c Release` — full suite.
- Cold-cache restore (`dotnet nuget locals all --clear && dotnet restore`) — supply-chain hygiene validation.

**Rollback:** single commit revert restores OneOf.Monads + struct removal.

### M7 — vcpkg baseline refresh + PD-15 guardrail

**Scope:**

- `vcpkg.json`: baseline `0b88aacd…` → `256acc64012b23a13041d8705805e1f23b43a024` (or later as of bump date; re-verify overlay alignment at bump time).
- Overlay ports verification: `sdl2-mixer 2.8.1#2` + `sdl2-gfx 1.0.4#11` against new master.
- Delete `vcpkg-overlay-ports/mpg123/` (DEPRECATED per overlay README §43).
- **Close PD-15:** add post-harvest symbol-visibility guardrail. Suggested shape: `SdlGfxSymbolVisibilityValidator` as a new preflight validator invoked at post-pack time, asserting four sdl2-gfx public API entry points (`filledCircleRGBA`, `rotozoomSurface`, `stringRGBA`, `imageFilterAdd`) are `GLOBAL DEFAULT` (ELF) / `T` (Mach-O) on Linux/macOS satellite binaries. Alternative: fold into `PackageOutputValidator` — tradeoff is single-responsibility (separate validator wins) vs fewer moving parts (folded wins).

**Validation:**

- Full 7-RID Harvest dry-run via CI workflow trigger (local is insufficient — not all RIDs are locally reproducible).
- Guardrail integration tests on at least `linux-x64` + `osx-arm64` (representative Unix triplets).

**Rollback:** revert `vcpkg.json` + validator code.

**Note:** M7 **should not land without PD-15 closure.** Blind baseline bumps risk a silent sdl2-gfx symbol re-hiding regression.

### M8 — Documentation drift sweep

**Autonomous-safe subset (AGENTS.md Documentation-only exception):**

- `docs/plan.md` + `docs/onboarding.md` test-count refresh (324 → current measured count).
- `docs/onboarding.md:57` SDL2 version table fix.
- `docs/knowledge-base/cake-build-architecture.md:59-75` ASCII tree refresh to ADR-002 shape.
- Cross-doc `runtimes.json` / `system_artefacts.json` references gain "merged into manifest.json schema v2.1" caveat.

**Review-needed subset (not autonomous):**

- `docs/knowledge-base/harvesting-process.md:53-54` — behavioral prose about RID intake path. Verify against `BuildContext` RID-read code path.
- `docs/knowledge-base/ci-cd-packaging-and-release-plan.md:188-191,336` — process prose still mentions `build/runtimes.json` as loadable. Review with Deniz.
- `docs/reference/cake-frosting-build-expertise.md:390,483,498` — Cake.Frosting 5.0.0 examples; future-dated citations. Keep / deprecate / refresh? Deniz's call.
- `docs/phases/phase-4-binding-autogen.md:94` — `[LibraryImport] (net7.0+)` clarification.

**Rollback:** trivial — all doc edits.

## 12. Cross-Phase Compatibility + Sequencing

### 12.1 Does this interfere with active Phase 2a work?

| Active work | M-stage collision? | Notes |
|---|---|---|
| PA-2 behavioral validation on 4 newly-covered RIDs | No | M1/M3/M4/M5 don't touch packaging infrastructure |
| Stream C (PreflightGate as CI gate) | No | M7 adds a preflight validator (same pattern, non-overlapping) |
| Stream D-ci (CI publish + internal feed) | No | — |
| HarvestTaskRunner extraction (#87) | No, but M2 is easier before any further Cake-host refactor | — |
| ADR-001 / ADR-002 invariants | No | M6's Result type lives under `Domain/Results/` per ADR-002 layer discipline |

### 12.2 Suggested ordering (not strict)

**Current preferred execution order (revised 2026-04-20 v2 after peer review):**

1. **M0** — pre-flight, ~30 min
2. **M1** — TFM cascade, validates .NET 10 foundation
3. **M2** — System.CommandLine 2.0 GA rewrite on clean M1 baseline
4. **M3** — analyzer package bumps on clean M1+M2 baseline, generates diagnostic signal
5. **M4** — `.editorconfig` rebuild informed by M3 signal
6. **M5** — `Directory.Build.props` tightening after M3/M4 settle
7. **M6** — Result pattern migration (pilot-gated)
8. **M7** — vcpkg baseline + PD-15 (independent; schedule when CI capacity permits)
9. **M8** — doc sweep (lands alongside / after M1 so facts match code)

**Rationale for M2-before-M3 (attribution cleanliness):**

- **M1 failure mode** = .NET 10 / C# 14 regressions only.
- **M2 failure mode on clean M1** = System.CommandLine semantic-migration bugs only. Isolated and debuggable.
- **M3/M4/M5 failure mode on clean M1+M2** = analyzer-rule-surface changes only.

The prior ordering (M3 → M4 → M5 → M2) put the biggest semantic diff (M2) **on top of accumulated analyzer triage debt**, making it hard to distinguish "this is a new rule-behavior change" from "this is a real SCL migration regression." Swapping M2 in front costs nothing — SCL 2.0 GA doesn't require .NET 10 SDK specifically (it targets netstandard2.0/net8+), but we still want M1 to land first so we're not juggling two SDK-environment changes concurrently.

**Technically M2 ↔ M3/M4/M5 are independent**, so a team with parallel capacity could interleave them. For a solo execution loop, serial M1 → M2 → M3 → M4 → M5 gives the cleanest attribution.

### 12.3 Pull-into-phase guidance

| M-stage | Natural phase landing |
|---|---|
| M0, M1, M3, M5 | Any clean sprint within Phase 2a or Phase 2b. Low risk, fast validation. |
| M2 | Before HarvestTaskRunner extraction (#87) lands — any Cake-host refactor benefits from GA API surface. |
| M4 | Pair with M3 or immediately after. |
| M6 | Low-blast-radius; fits any phase. |
| M7 | Pairs well with Stream C CI gate work in late Phase 2a or early Phase 2b. |
| M8 | Fire-and-forget alongside any other commit that touches docs. |

## 13. Open Questions (For Deniz)

Carried forward from the session; not blocking but worth explicit answers before execution:

1. **Roslynator.CodeAnalysis.Analyzers** — drop (template's call) or keep (defensive; zero cost)?
2. **`$(LatestDotNet)` → `$(CurrentNetVersion)` rename** — adopt for template cosmetic alignment, or skip and avoid doc cascade?
3. **`GenerateDocumentationFile=true` scheduling** — defer to Phase 4 (CppAst-generated XML comments) or own work item?
4. **PD-15 guardrail shape** — separate `SdlGfxSymbolVisibilityValidator` (single-responsibility) **or** fold into `PackageOutputValidator`. **Author preference: separate validator.** Rationale: matches [`feedback_strong_guardrails.md`](../../../memory/feedback_strong_guardrails.md) defense-in-depth principle (new invariants land with their own guardrail), keeps `PackageOutputValidator` focused on pack-output shape rather than native-symbol-visibility concerns, testable in isolation without a pack artifact. Awaiting Deniz's confirmation before M7 implementation.
5. **OneOf.Monads 1.21.0 origin** — any institutional memory on the pin (private feed? fork? yanked version cached?)? Doesn't block M6 but worth knowing before the removal commit.
6. **M2 timing now resolved** (2026-04-20 v2 revision, attribution-cleanliness argument): M2 lands before M3 in the preferred ordering. Independence vs M3/M4/M5 preserved for teams with parallel capacity.

## 14. References

### 14.1 .NET 10 / C# 14

- [Announcing .NET 10 — .NET Blog](https://devblogs.microsoft.com/dotnet/announcing-dotnet-10/)
- [.NET support policy](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core)
- [C# 14 compiler breaking changes](https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/breaking-changes/compiler%20breaking%20changes%20-%20dotnet%2010)
- [What's new in C# 14](https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-14)
- [.NET 10 breaking changes (full)](https://learn.microsoft.com/en-us/dotnet/core/compatibility/10.0)
- [Code analysis overview (.NET 10 tab)](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/overview?tabs=net-10)
- [`dotnet/sdk#52467` — AnalysisLevel=latest pin-to-9.0 bug](https://github.com/dotnet/sdk/issues/52467)
- [`dotnet/runtime#117712` — IL3058/IL2125 rules](https://github.com/dotnet/runtime/issues/117712)
- [Prepare .NET libraries for trimming](https://learn.microsoft.com/en-us/dotnet/core/deploying/trimming/prepare-libraries-for-trimming)

### 14.2 Package landscape

- [Microsoft.CodeAnalysis.NetAnalyzers 10.0.202 on NuGet](https://www.nuget.org/packages/Microsoft.CodeAnalysis.NetAnalyzers)
- [roslyn-analyzers README](https://github.com/dotnet/roslyn-analyzers/blob/main/src/NetAnalyzers/Microsoft.CodeAnalysis.NetAnalyzers.md)
- [Cake v6.1.0 release notes](https://cakebuild.net/blog/2026/03/cake-v6.1.0-released)
- [TUnit releases](https://github.com/thomhurst/TUnit/releases)
- [SonarAnalyzer.CSharp on NuGet](https://www.nuget.org/packages/SonarAnalyzer.CSharp)
- [sonar-dotnet releases](https://github.com/SonarSource/sonar-dotnet/releases)
- [Meziantou.Analyzer on NuGet](https://www.nuget.org/packages/Meziantou.Analyzer)
- [SecurityCodeScan.VS2019 on NuGet](https://www.nuget.org/packages/SecurityCodeScan.VS2019)
- [System.CommandLine on NuGet](https://www.nuget.org/packages/System.CommandLine)
- [System.CommandLine beta5+ migration guide](https://learn.microsoft.com/en-us/dotnet/standard/commandline/migration-guide-2.0.0-beta5)
- [System.CommandLine.NamingConventionBinder on NuGet](https://www.nuget.org/packages/System.CommandLine.NamingConventionBinder) — marked deprecated

### 14.3 vcpkg / SDL

- [vcpkg commit `0b88aac`](https://github.com/microsoft/vcpkg/commit/0b88aacde46a853151730fbe7d0b7ee45f4b6864)
- [microsoft/vcpkg master](https://github.com/microsoft/vcpkg)
- Port manifests: [sdl2](https://github.com/microsoft/vcpkg/blob/master/ports/sdl2/vcpkg.json), [sdl2-image](https://github.com/microsoft/vcpkg/blob/master/ports/sdl2-image/vcpkg.json), [sdl2-mixer](https://github.com/microsoft/vcpkg/blob/master/ports/sdl2-mixer/vcpkg.json), [sdl2-ttf](https://github.com/microsoft/vcpkg/blob/master/ports/sdl2-ttf/vcpkg.json), [sdl2-gfx](https://github.com/microsoft/vcpkg/blob/master/ports/sdl2-gfx/vcpkg.json), [sdl2-net](https://github.com/microsoft/vcpkg/blob/master/ports/sdl2-net/vcpkg.json)
- Upstream releases: [SDL](https://github.com/libsdl-org/SDL/releases), [SDL_image](https://github.com/libsdl-org/SDL_image/releases), [SDL_mixer](https://github.com/libsdl-org/SDL_mixer/releases), [SDL_ttf](https://github.com/libsdl-org/SDL_ttf/releases), [SDL_net](https://github.com/libsdl-org/SDL_net/releases)
- [SDL2 to Maintenance Mode — Phoronix](https://www.phoronix.com/news/SDL2-To-Maintenance-Mode)

### 14.4 Result pattern libraries

- [OneOf on NuGet](https://www.nuget.org/packages/OneOf)
- [OneOf.Monads on NuGet](https://www.nuget.org/packages/OneOf.Monads/)
- [ErrorOr on NuGet](https://www.nuget.org/packages/erroror) + [GitHub](https://github.com/amantinband/error-or)
- [FluentResults on NuGet](https://www.nuget.org/packages/FluentResults/) + [GitHub](https://github.com/altmann/FluentResults)
- [CSharpFunctionalExtensions on NuGet](https://www.nuget.org/packages/CSharpFunctionalExtensions)
- [Ardalis.Result on NuGet](https://www.nuget.org/packages/Ardalis.Result)
- [LanguageExt.Core on NuGet](https://www.nuget.org/packages/LanguageExt.Core/5.0.0-beta-35)
- [Dunet on GitHub](https://github.com/domn1995/dunet)
- [`csharplang#8928` — Discriminated Unions proposal](https://github.com/dotnet/csharplang/issues/8928)
- [`csharplang/proposals/unions.md`](https://github.com/dotnet/csharplang/blob/main/proposals/unions.md)

### 14.5 Native AOT

- [Native code interop with Native AOT](https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/interop)
- [Building native libraries with Native AOT](https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/libraries)
- [Native AOT deployment overview](https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/)
- [FNA Appendix A: NativeAOT on PC](https://fna-xna.github.io/docs/appendix/Appendix-A:-NativeAOT-on-PC/)
- [`dotnet/runtime#113291` — DLL required during development](https://github.com/dotnet/runtime/discussions/113291)
- [`dotnet/runtime#108981` — link static lib from nuget](https://github.com/dotnet/runtime/discussions/108981)
- [Avalonia #9503 — NativeAOT static linking request (open since 2022)](https://github.com/AvaloniaUI/Avalonia/issues/9503)
- [NuGet buildTransitive design — NuGet/Home#6091](https://github.com/NuGet/Home/issues/6091)

### 14.6 Canonical repo cross-references

- [`AGENTS.md`](../../AGENTS.md) — operating rules, approval-gate, communication preferences
- [`docs/onboarding.md`](../onboarding.md) — project overview, strategic decisions, repo layout
- [`docs/plan.md`](../plan.md) — canonical status, phase roll-up, version tracking
- [`docs/phases/README.md`](README.md) — phase workflow navigation
- [`docs/decisions/2026-04-18-versioning-d3seg.md`](../decisions/2026-04-18-versioning-d3seg.md) — ADR-001 D-3seg versioning
- [`docs/decisions/2026-04-19-ddd-layering-build-host.md`](../decisions/2026-04-19-ddd-layering-build-host.md) — ADR-002 build-host layering
- [`docs/knowledge-base/cake-build-architecture.md`](../knowledge-base/cake-build-architecture.md) — Cake Frosting deep reference
- [`docs/knowledge-base/release-guardrails.md`](../knowledge-base/release-guardrails.md) — G-numbered guardrails
- [`docs/phases/phase-2-adaptation-plan.md`](phase-2-adaptation-plan.md) — Stream A/B/C/D/E/F + Pending Decisions (PD-15 referenced in M7)
- [`vcpkg-overlay-ports/README.md`](../../vcpkg-overlay-ports/README.md) — overlay maintenance rules
- [`vcpkg-overlay-triplets/`](../../vcpkg-overlay-triplets/) — hybrid triplet set

## 15. Revision History

| Date | Revision | Change |
| --- | --- | --- |
| 2026-04-20 | v1 | Initial proposal: research across four parallel tracks, Deniz direction corrections, consolidated M0–M8 staged plan. Parked as standalone modernization document pending pull-into-phase scheduling. |
| 2026-04-20 | v2 | Peer review pass (external reviewer via Deniz). Six operational refinements applied: (1) M3 — explicit CPM operational-constraint box warning against `dotnet add/update` under Central Package Management; (2) M1 scope expanded with grep-verified hardcoded-`net9.0` test-fixture inventory (9 hit points across 3 files, including the `PackageOutputValidatorTests.cs:130` TFM-string logic branch and `:423` nuspec pack-output assertion) plus the 4-workflow binary-invocation path sweep; (3) M1 atomicity note added acknowledging this is the largest stage by diff count — deliberate for CI matrix coherence; (4) M1 validation gains explicit Mono/Wine net462 runtime confirmation on Linux + CI matrix wall-clock baseline capture (matrix expansion 3→4 executable, 4→5 library); (5) §12.2 ordering revised to **M0 → M1 → M2 → M3 → M4 → M5 → M6 → M7 → M8** on attribution-cleanliness argument — SCL rewrite on clean M1 baseline before analyzer triage debt accumulates; (6) M6 tone softened — pilot is the **test** of the zero-edit-call-site hypothesis, not its confirmation; M6 scope widens if pilot requires call-site edits. RC pipeline handling clarified as pre-existing Stream D-ci stub concern, not new modernization risk. §13 PD-15 author preference stated (separate validator per defense-in-depth principle). |
