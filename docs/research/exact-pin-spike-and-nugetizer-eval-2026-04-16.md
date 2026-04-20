# Research: A0 Exact-Pin Mechanism + NuGetizer Evaluation [Historical, SUPERSEDED]

**Date:** 2026-04-16
**Status:** **SUPERSEDED 2026-04-17 (S1 adoption).** Mechanism proven on 2026-04-16 and PD-2 was marked resolved at the time; retired on 2026-04-17 when production orchestration integration hit upstream NuGet limitations. This document is preserved as historical record of the investigation. It is no longer binding policy.
**Note (2026-04-18):** [ADR-001](../decisions/2026-04-18-versioning-d3seg.md) locked the successor policy: D-3seg family versioning, package-first local development, and metadata-based upstream patch disclosure rather than within-family exact pin.
**Context:** Stream A0 (originally: blocks D-local, blocks A-risky). Amendment 2 of [phase-2-adaptation-plan.md](../phases/phase-2-adaptation-plan.md) (which specified the A0 requirement) was itself SUPERSEDED by S1.

---

> **SUPERSEDED HEADER (2026-04-17).** The mechanism described here — `PrivateAssets="all"` on the Native `ProjectReference` + bracket-notation CPM `PackageVersion` + paired `PackageReference` — was proven mechanically correct on 2026-04-16 and landed in Stream A-risky. Subsequent production integration (2026-04-17) reproduced `NU5016` errors during Cake `PackageTask` execution. The failure mode: NuGet's pack-time sub-evaluation does not preserve CLI globals through the ProjectReference walk; the csproj's sentinel fallback fires and `PackageVersion` freezes at `[0.0.0-restore]`. Our best-diagnosed mechanical explanation is the `<MSBuild ... Properties="BuildProjectReferences=false;">` invocation at [`NuGet.Build.Tasks.Pack.targets` line 335](../../artifacts/temp/nu5016-cake-restore-investigation-2026-04-17.md) — an explicit `Properties=` attribute that appears to replace the child evaluation's global property set rather than extend it. Research confirmed that specific code path has shipped unchanged for 8+ years (introduced [NuGet/NuGet.Client#1915](https://github.com/NuGet/NuGet.Client/pull/1915), 2018-01-05) and is identical in .NET 10 SDK. [NuGet/Home#11617](https://github.com/NuGet/Home/issues/11617) has been open since 2022 with no upstream traction. Rather than carry a permanent local workaround against a specific NuGet internal, **S1 adoption** retired the within-family exact-pin requirement and adopted SkiaSharp-style minimum range — a shape that does not depend on the behavior of NuGet's sub-evaluation. See [phase-2-adaptation-plan.md "S1 Adoption Record"](../phases/phase-2-adaptation-plan.md), [release-lifecycle-direction.md §4 Drift Protection Model](../knowledge-base/release-lifecycle-direction.md), PD-11. The Part 3 "production-time version flow constraint" section of this document was the empirical finding that motivated S1.
>
> **What this doc is still useful for:** as historical record of why we investigated exact-pin, what mechanism works in isolation, what the industry precedent was, and why we ultimately retired the approach. Do not treat recommendations here as current policy. Treat the specific NuGet-internal mechanism citation as supporting evidence for our diagnosis, not as a definitive statement about NuGet internals.

---

## Part 1: Within-Family Exact Pin Mechanism [Historical, SUPERSEDED]

### Problem Statement

The release lifecycle direction requires two different dependency contracts in the same `.nupkg`:

```text
Janset.SDL2.Image (managed, v1.0.3)
  ├── Janset.SDL2.Image.Native (= 1.0.3)   ← exact pin [1.0.3] (historical target)
  └── Janset.SDL2.Core (>= 1.0.3)          ← minimum range
```

NuGet's `[x.y.z]` bracket notation means "exactly this version." Bare `x.y.z` means "this version or higher" (`>=`).

**Default `dotnet pack` cannot produce this.** When `dotnet pack` converts a `ProjectReference` into a nuspec dependency, it always emits a bare version (minimum range). There is no built-in `ExactVersion` metadata on `ProjectReference`. This is a known NuGet limitation: [NuGet/Home#5525](https://github.com/NuGet/Home/issues/5525) (closed as duplicate), [NuGet/Home#5556](https://github.com/NuGet/Home/issues/5556) (still open since 2017).

**Historical empirical confirmation:** Running `dotnet pack` on the current `SDL2.Image.csproj` with `-p:PackageVersion=1.0.3 -p:Version=1.0.3` produces both dependencies as bare `version="1.0.3"` (minimum range). The exact pin target is not met.

### Approaches Investigated

Four approaches were evaluated:

| # | Approach | Result |
|---|---|---|
| 1 | Custom `.nuspec` template + `NuspecFile` / `NuspecProperties` | **Works** — full control, but requires maintaining a separate nuspec file per managed package |
| 2 | Post-pack nuspec patching via Cake (unzip → patch XML → rezip) | **Would work** — but fragile and bypasses the build system's own validation |
| 3 | MSBuild property injection (`ExactVersion=true` or similar) | **Does not exist** — no such property on `ProjectReference` |
| 4 | `PrivateAssets="all"` on ProjectReference + explicit `PackageReference` with bracket notation | **Works** — clean, standard MSBuild, no extra files |

### Historical Chosen Mechanism: Approach 4 (Mechanism 3, SUPERSEDED)

Two standard MSBuild/NuGet features combine to solve the problem:

**Feature A — `PrivateAssets="all"` on ProjectReference:** Suppresses the reference from appearing as a dependency in the pack output nuspec. The ProjectReference still works for build (compilation, build ordering) but the nupkg's dependency list does not include it.

**Feature B — `PackageReference` with bracket notation:** `dotnet pack` preserves bracket notation verbatim in the output nuspec. In a CPM-enabled repo (like this one), the version is declared via `<PackageVersion>` and the `<PackageReference>` is bare. In a non-CPM repo, the version would go directly on `<PackageReference Version="[1.0.3]" />`. Either way, the bracket notation flows through to the nuspec as `<dependency id="Foo" version="[1.0.3]" />`.

**Combined csproj shape:**

```xml
<ItemGroup>
  <!-- Core: normal ProjectReference → emits >= in pack (minimum range) -->
  <ProjectReference Include="..\SDL2.Core\SDL2.Core.csproj" />

  <!-- Native: build dependency stays, suppressed from pack output -->
  <ProjectReference Include="..\native\SDL2.Image.Native\SDL2.Image.Native.csproj"
                    PrivateAssets="all" />
</ItemGroup>

<ItemGroup>
  <!-- Native: historical exact pin dependency injected into pack output -->
  <!-- Version follows family version variable; in production, from MinVer / CLI. -->
  <!-- Property name follows canonical naming: Sdl<Major><Role>FamilyVersion -->
  <!-- (see release-lifecycle-direction.md §1). Spike used "ImageFamilyVersion" -->
  <!-- before the convention was canonicalized; the mechanism is name-agnostic. -->
  <PackageVersion Include="Janset.SDL2.Image.Native" Version="[$(Sdl2ImageFamilyVersion)]" />
  <PackageReference Include="Janset.SDL2.Image.Native" />
</ItemGroup>
```

**How each phase works:**

| Phase | Behavior |
|---|---|
| **Restore** | NuGet sees that the `PackageReference`'s `PackageId` matches a `ProjectReference` in the solution. It uses the project output to satisfy the dependency — no feed lookup needed. |
| **Build** | The `ProjectReference` with `PrivateAssets="all"` still provides compilation reference and build ordering. Code compiles normally. |
| **Pack** | The `ProjectReference` is suppressed from the dependency list. The `PackageReference` with bracket notation is emitted verbatim as `[x.y.z]` in the nuspec. |

### Empirical Proof

All tests run on the current repo (Windows, .NET SDK 9.0.309):

| Test | Restore | Build | Pack | Nuspec | Verdict |
|---|---|---|---|---|---|
| Image family, static version `1.0.3` | PASS | PASS | PASS | Native `[1.0.3]`, Core `1.0.3` | **PASS** |
| Image family, parametrized `2.1.0` | PASS | PASS | PASS | Native `[2.1.0]`, Core `2.1.0` | **PASS** |
| Core family (no cross-family deps) | PASS | PASS | PASS | Native `[1.2.0]` only | **PASS** |
| Version mismatch (restore=1.0.3, pack=2.1.0) | PASS | PASS | **NU5016** | — | **Expected fail** |
| Debug build | PASS | PASS | n/a | n/a | **PASS** |
| Multi-TFM (net9.0, net8.0, netstandard2.0, net462) | All 4 dependency groups correct | — | — | — | **PASS** |

**Version mismatch safety:** When restore and pack receive different family version values, NuGet reports `NU5016: Package version constraints return an empty range`. This is correct behavior — it prevents accidental version drift between restore and pack. The Cake orchestration must pass the same version to both invocations.

**Probe artifacts** preserved at `artifacts/temp/a0-mechanism3/` (gitignored).

### Industry Precedent

**Historical LibGit2Sharp precedent** (production): Uses `PackageReference Include="LibGit2Sharp.NativeBinaries" Version="[2.0.323]"` for exact pin of its native binaries package. Visible on [NuGet.org](https://www.nuget.org/packages/LibGit2Sharp/0.31.0) as `= 2.0.323`.

**SkiaSharp**: Uses minimum-version (`>=`) for all NativeAssets dependencies. Different policy choice — they accept newer patch versions of native assets.

### Implications for csproj Topology

Every managed satellite package needs these 3 lines added:

1. `PrivateAssets="all"` on the existing Native `ProjectReference`
2. A `PackageVersion` item for the Native package with bracket notation
3. A `PackageReference` item for the Native package

Core family needs the same 3 lines (minus the cross-family ProjectReference to Core, since Core has no cross-family dependency).

**No `.nuspec` template files needed.** No custom MSBuild targets. No post-pack patching. Standard MSBuild/NuGet features only.

### Version Injection Strategy

The historical `$(Sdl2ImageFamilyVersion)` property (or `$(Sdl2CoreFamilyVersion)`, etc.) controlled the exact pin version. Property naming follows the canonical `Sdl<Major><Role>FamilyVersion` convention from [release-lifecycle-direction.md §1](../knowledge-base/release-lifecycle-direction.md). In production:

- **MinVer** sets the family version from git tags (Stream A-risky); the property defaults to `$(Version)`
- **CLI override** via `dotnet pack -p:Sdl2ImageFamilyVersion=x.y.z` for explicit control
- **Cake orchestration** passes the resolved family version to both restore and pack
- **Historical restore-time fallback:** at restore time MinVer has not yet set `$(Version)`, so the property resolves to `0.0.0-restore` (a parseable sentinel that lets restore succeed). [`src/Directory.Build.targets`](../../src/Directory.Build.targets) rewrites the `PackageVersion` `Version` metadata to `[$(Version)]` `BeforeTargets="GenerateNuspec"` so the produced nuspec carries the correct family version, never the sentinel

The `CoreMinVersion` property (cross-family minimum) is a separate concern. For the default ProjectReference behavior, the version emitted is whatever the referenced project resolves to at pack time. If finer control is needed (e.g., pinning the floor at `1.2.0` even when Core is at `1.5.0`), that is a Stream D-local concern, not A0.

### Resolution

**Historical PD-2 resolution (SUPERSEDED).** The mechanism produces both within-family exact pin `[x.y.z]` and cross-family minimum range `x.y.z` in the same `.nupkg`, using only standard MSBuild/NuGet features.

---

## Part 2: NuGetizer Evaluation

### What Is NuGetizer?

[devlooped/nugetizer](https://github.com/devlooped/nugetizer) is a complete replacement for the stock `dotnet pack` MSBuild targets. Created by Daniel Cazzulino (kzu, author of Moq, ThisAssembly, GitInfo). It replaces the entire packing pipeline with a `PackageFile`-based model and a `GetPackageContents` protocol.

| Metric | Value |
|---|---|
| NuGet downloads | ~1M total |
| GitHub stars | 268 |
| Latest release | v1.4.7 (January 2026) |
| License | MIT + OSMF (Open Source Maintenance Fee for commercial use) |
| Maintenance | Active, commits as of January 2026 |

### What It Solves

| Pain point with stock `dotnet pack` | NuGetizer solution |
|---|---|
| "What's in my nupkg?" requires build → pack → unzip → inspect | `dotnet nugetize` — instant package layout preview, no build needed |
| Content placement uses fragmented metadata (`Content` + `Pack=true` + `PackagePath` + `LinkBase`) | Single `PackageFile` item type with `PackFolder` metadata |
| Pack inference is sometimes surprising (what gets included, what doesn't) | `EnablePackInference=false` kill switch for fully explicit control |
| Multi-project aggregation into single nupkg is MSBuild acrobatics | `Microsoft.Build.NoTargets` SDK + `GetPackageContents` protocol |
| No README content reuse | `<!-- include file.md#section -->` directives |
| Package validation off by default | On by default for Release builds |

### Historical Question: Does It Solve Our Exact Pin Problem?

**Historical answer: no.** NuGetizer has the same limitation: `ProjectReference` → dependency conversion always uses minimum-inclusive range. There is no per-reference metadata to control version range format. The workaround in NuGetizer would be manual `PackageFile` items with `PackFolder="Dependency"` and explicit `Version="[1.0.3]"` — functionally equivalent to our Mechanism 3 or the nuspec template approach.

### Adoption Trade-offs

**What we would gain:**
- `dotnet nugetize` CLI tool for rapid package layout iteration (the strongest benefit)
- Cleaner syntax for complex packages via `PackageFile`
- Package validation on by default
- README include directives

**What we would lose or risk:**
- **Viral dependency:** NuGetizer must be installed on ALL projects in the pack graph, not just the top-level packable project
- **OSMF licensing:** Revenue-generating commercial use requires GitHub sponsorship (same model that caused Moq controversy)
- **Ecosystem lock-in:** Completely replaces SDK pack targets. Removal requires reconfiguring all pack settings
- **Small community:** 268 stars, ~1M downloads. Limited debugging help compared to stock SDK pack
- **Learning curve:** First-time users report difficulty; documentation is theory-heavy
- **IDE warnings:** Visual Studio shows warning icons on `PackageFile` items (project system limitation)

### Comparison

| Capability | Stock `dotnet pack` + Mechanism 3 (historical) | NuGetizer |
|---|---|---|
| Exact version pin `[x.y.z]` per dependency | Yes (proven) | Same workaround needed |
| Mixed version ranges in same package | Yes (proven) | Yes, via manual `PackageFile` |
| Native-only packages | Yes (current setup works) | Yes, different syntax |
| buildTransitive targets | Yes (current setup works) | Yes (`PackFolder="buildTransitive"`) |
| Multi-TFM | Yes | Yes |
| Package preview CLI | No | **Yes** (`dotnet nugetize`) |
| Adoption complexity | Zero (built-in) | High (all projects, new model) |
| Ecosystem risk | None | Medium (OSMF, single maintainer) |

### Verdict

**Historical verdict for Phase 2a.** The stock MSBuild pack pipeline with Mechanism 3 handles all current requirements. NuGetizer does not solve our critical problem (exact pin) any better than the chosen approach.

**Not discarding either.** Two scenarios where it could become relevant:

1. **Phase 2b+:** If packaging iteration becomes a frequent pain point, `dotnet-nugetize` can be installed as a standalone global tool (`dotnet tool install -g dotnet-nugetize`) for layout preview without adopting the full NuGetizer PackageReference. This is a zero-risk way to get the best feature.

2. **Phase 3 (SDL2 Complete):** If the per-satellite packaging grows complex enough that the stock `Content` + `PackagePath` approach becomes unwieldy across 6+ native packages, NuGetizer's unified `PackageFile` model could simplify maintenance. Re-evaluate at that point.

---

## Part 3: Production-Time Version Flow Constraint (Empirical Finding, 2026-04-16)

### Context

Historical finding: Part 1 proved the csproj SHAPE for within-family exact pin works. The spike used hardcoded literal versions or `-p:CLI` overrides for testing. During A-risky implementation we hit a deeper constraint when trying to chain MinVer's auto-derived `$(Version)` into the bracket-notation `PackageVersion` for the developer-convenience case (`dotnet pack` without explicit version flags).

### The Static-Eval Timing Constraint

`<PackageVersion Include="..." Version="[$(Sdl<Major><Role>FamilyVersion)]"/>` resolves the `$(Sdl<Major><Role>FamilyVersion)` substitution at MSBuild **static evaluation** time — when the project file is loaded, BEFORE any target runs.

MinVer sets `$(Version)` at TARGET time (via `BeforeTargets="GenerateNuspec;GetPackageVersion;..."`). By the time MinVer fires, the `PackageVersion` item's `Version` metadata is already locked.

Worse: NuGet's restore phase writes the resolved version range into `project.assets.json` from this statically-captured value. By pack time, the pack target reads from `project.assets.json`, so even a target that updates `PackageVersion` items at `BeforeTargets="GenerateNuspec"` is **too late** — `assets.json` already has the wrong value baked in.

### Why the Restore-Time Hook Doesn't Save Us

You might think `BeforeTargets="_GenerateRestoreSpecs"` with `DependsOnTargets="MinVer"` would fix this — fire MinVer early, update `PackageVersion`, then let restore proceed. We tried this. It fails:

```text
error MSB4057: The target "MinVer" does not exist in the project.
```

**Chicken-and-egg:** MinVer's targets file lives inside its own NuGet package. NuGet restores the package, then loads its targets. At the very first restore (or after a clean), MinVer's targets aren't available yet, so `DependsOnTargets="MinVer"` fails. By the second build, MinVer's targets exist, but `assets.json` was already written.

### Empirical Probe Results (2026-04-16, this repo)

| Invocation | Restore behavior | Pack behavior | Verdict |
| --- | --- | --- | --- |
| Hardcoded `Version="[1.3.0-test]"` literal in csproj | `assets.json` captures `[1.3.0-test]` | nuspec emits `[1.3.0-test]` | **PASS — proves shape is sound** |
| `dotnet pack` (no flags) | `assets.json` captures `[0.0.0-restore]` (historical sentinel) | Guard target hard-fails | **PASS — historical guard catches sentinel** |
| `dotnet pack -p:Version=X -p:MinVerSkip=true` (single invocation) | `assets.json` captures `[0.0.0-restore]` (historical sentinel; CLI doesn't reach restore-phase static eval) | NU5016 empty range | **FAIL — properties don't reach restore** |
| `dotnet pack -p:MinVerVersionOverride=X` (single invocation) | Same as above | NU5016 empty range | **FAIL** |
| `dotnet pack -p:Sdl2ImageFamilyVersion=X -p:Version=X -p:MinVerSkip=true` (single) | Same — restore step doesn't honor CLI properties on bracket-notation items | NU5016 | **FAIL** |
| Explicit two-step: `dotnet restore -p:Sdl2ImageFamilyVersion=X -p:Version=X -p:MinVerSkip=true` then `dotnet pack --no-restore -p:...` (same flags) | `assets.json` captures `[X]` correctly | Pack emits `[X]` for managed nuspec, but ProjectReference sub-build of native csproj fails (-p: doesn't reliably propagate to sub-builds) | **PARTIAL — managed correct, native version mismatch** |
| Hardcoded literal in csproj `+ -p:Version=X -p:MinVerSkip=true` | `assets.json` captures `[X]` literal | nuspec emits `[X]` correctly across all 4 TFM groups | **PASS — confirms literal works in CLI flow** |

### Frontier Confirmation

Independent industry survey (separate explore-agent investigation, 2026-04-16):

| Project | Within-family pin approach |
| --- | --- |
| **LibGit2Sharp** | Hardcoded literal `Version="[2.0.323]"` in csproj. Manually updated per release. Doesn't auto-derive from MinVer. |
| **SkiaSharp** | Minimum range (`>=`), no exact pin. Historical comparison; different policy choice. |
| **Avalonia** | Minimum range, CPM. No exact pin. Historical comparison. |
| **Magick.NET** | Hardcoded version, doesn't use MinVer. |
| **SDL3-CS (ppy)** | Bundles natives in main package, no separate native package, no pinning needed. |

**Historical frontier finding:** no major .NET multi-package monorepo solves auto-derived within-family exact pin from MinVer for standalone `dotnet pack`. We were at the frontier. The solution space was narrow:

- **A:** Accept the constraint, document, use Cake orchestration for production. (Chosen.)
- **B:** Hardcode literal versions per release, manually edited. (LibGit2Sharp pattern. Loses MinVer benefit.)
- **C:** Historical option — drop exact pin, use minimum range. (SkiaSharp pattern. Loses within-family safety.)
- **D:** Post-pack nuspec patching (open `.nupkg`, rewrite XML, re-zip). (Hacky, fragile.)
- **E:** Switch to NuGetizer with manual `PackageFile` items. (Same constraint, different surface.)

### Chosen Approach: Cake Two-Step Orchestration

Production path is Cake-driven (Stream D-local `PackageTask`). Cake:

1. Determines the family version (from MinVer-derived git tag, or from `release-set.json` lookup).
2. Pre-builds the Native csproj with explicit properties:
   ```bash
   dotnet build src/native/SDL2.<Role>.Native/SDL2.<Role>.Native.csproj \
     -p:Configuration=Release \
     -p:Version=X.Y.Z \
     -p:Sdl2<Role>FamilyVersion=X.Y.Z \
     -p:MinVerSkip=true
   ```
3. Restores the managed csproj with the same properties:
   ```bash
   dotnet restore src/SDL2.<Role>/SDL2.<Role>.csproj \
     -p:Version=X.Y.Z \
     -p:Sdl2<Role>FamilyVersion=X.Y.Z \
     -p:MinVerSkip=true
   ```
4. Packs the managed csproj with `--no-restore` and the same properties:
   ```bash
   dotnet pack --no-restore src/SDL2.<Role>/SDL2.<Role>.csproj \
     -c Release \
     -p:Version=X.Y.Z \
     -p:Sdl2<Role>FamilyVersion=X.Y.Z \
     -p:MinVerSkip=true \
     -o artifacts/packages/
   ```

The pre-built native ensures the ProjectReference sub-build resolution finds a Native binary at the correct version. The two-step restore + pack ensures `project.assets.json` captures the correct bracket-notation value before pack reads from it.

### Standalone `dotnet pack` Without Cake — Wrong-on-Purpose

A developer who ran `dotnet pack` directly on a managed csproj without explicit properties triggered the historical MSBuild guard target in [src/Directory.Build.targets](../../src/Directory.Build.targets) (`_GuardAgainstShippingRestoreSentinel`). The guard hard-failed the pack with a banner-level error message naming the missing properties and pointing at this doc.

For deliberate historical sentinel inspection (e.g., A-risky validation, structural smoke tests), the operator opted in via `-p:AllowSentinelExactPin=true`. The bypass is loud, single-purpose, and never the production path.

### Implications for Stream D-local

`PackageTask` design must:

- Pre-build native csprojs as a separate step.
- Issue restore + pack as separate `dotnet` invocations with the full property set.
- Pass `-p:MinVerSkip=true` even when MinVer would have produced the same value, so version flow is deterministic and auditable.
- Validate produced nuspecs post-pack (guardrails G20–G27 in [release-guardrails.md](../knowledge-base/release-guardrails.md)) — defense-in-depth beyond the structural + MSBuild guards.

### Implications for Manual Escape Hatch (PD-8)

The same two-step orchestration must be reproducible by a human operator without Cake. The escape-hatch playbook ([release-recovery-and-manual-escape-hatch-2026-04-16.md](release-recovery-and-manual-escape-hatch-2026-04-16.md)) reproduces these steps verbatim. Cake `Pack-Family` helper (Stream D-local) wraps them so the operator types one command instead of remembering all the flags.

### What's NOT Affected

- **csproj structural shape:** correct. `PrivateAssets="all"` + bracket-notation `PackageVersion` is the right pattern.
- **PD-2 resolution:** still resolved. The mechanism produces correct nuspecs; the constraint is on HOW you invoke pack, not WHETHER the mechanism works.
- **A0 spike conclusions:** intact. The spike's parametrized test passed because it used CLI overrides, which is exactly what Cake will do in production.
- **Historical as-of-2026-04-16 conclusion:** within-family exact pin policy was then considered unchanged, locked, enforced, and correct.

What changed is our understanding of what "auto-derive from MinVer for standalone `dotnet pack`" can deliver: nothing, due to MSBuild static-eval timing. We accept this, guard against it leaking, and orchestrate via Cake.

---

## Sources

- [NuGet Package Versioning](https://learn.microsoft.com/en-us/nuget/concepts/package-versioning) — version range notation reference
- [NuGet pack and restore as MSBuild targets](https://learn.microsoft.com/en-us/nuget/reference/msbuild-targets) — NuspecFile, NuspecProperties, pack target internals
- [NuGet/Home#5525](https://github.com/NuGet/Home/issues/5525) — request for exact version on ProjectReference (closed as duplicate)
- [NuGet/Home#5556](https://github.com/NuGet/Home/issues/5556) — request for upper-limit version on ProjectReference (open since 2017)
- [NuGet/NuGet.Client PR#3097](https://github.com/NuGet/NuGet.Client/pull/3097) — VersionRange support for project references (merged Feb 2020)
- [LibGit2Sharp on NuGet.org](https://www.nuget.org/packages/LibGit2Sharp/) — historical production exact pin precedent (hardcoded literal)
- [SkiaSharp on NuGet.org](https://www.nuget.org/packages/SkiaSharp/) — alternative approach (minimum range for native assets)
- [devlooped/nugetizer](https://github.com/devlooped/nugetizer) — NuGetizer project repository
- [NuGetizer 3000 spec](https://github.com/NuGet/Home/wiki/NuGetizer-3000) — original NuGet team proposal
- [MinVer Changelog](https://github.com/adamralph/minver/blob/main/CHANGELOG.md) — release notes through v7.0.0
- [`src/Directory.Build.targets`](../../src/Directory.Build.targets) — historical `_GuardAgainstShippingRestoreSentinel` MSBuild guard (SUPERSEDED)
- [`release-guardrails.md`](../knowledge-base/release-guardrails.md) — full guardrail roadmap including post-pack assertions (G20–G27)
