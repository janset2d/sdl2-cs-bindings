# Research: A0 Exact-Pin Mechanism + NuGetizer Evaluation

**Date:** 2026-04-16
**Status:** A0 spike — mechanism proven, PD-2 resolved
**Context:** Stream A0 (blocks D-local, blocks A-risky). See [phase-2-adaptation-plan.md](../phases/phase-2-adaptation-plan.md) Amendment 2.

---

## Part 1: Within-Family Exact Pin Mechanism

### Problem Statement

The release lifecycle direction requires two different dependency contracts in the same `.nupkg`:

```text
Janset.SDL2.Image (managed, v1.0.3)
  ├── Janset.SDL2.Image.Native (= 1.0.3)   ← exact pin [1.0.3]
  └── Janset.SDL2.Core (>= 1.0.3)          ← minimum range
```

NuGet's `[x.y.z]` bracket notation means "exactly this version." Bare `x.y.z` means "this version or higher" (`>=`).

**Default `dotnet pack` cannot produce this.** When `dotnet pack` converts a `ProjectReference` into a nuspec dependency, it always emits a bare version (minimum range). There is no built-in `ExactVersion` metadata on `ProjectReference`. This is a known NuGet limitation: [NuGet/Home#5525](https://github.com/NuGet/Home/issues/5525) (closed as duplicate), [NuGet/Home#5556](https://github.com/NuGet/Home/issues/5556) (still open since 2017).

**Empirical confirmation:** Running `dotnet pack` on the current `SDL2.Image.csproj` with `-p:PackageVersion=1.0.3 -p:Version=1.0.3` produces both dependencies as bare `version="1.0.3"` (minimum range). The exact pin target is not met.

### Approaches Investigated

Four approaches were evaluated:

| # | Approach | Result |
|---|---|---|
| 1 | Custom `.nuspec` template + `NuspecFile` / `NuspecProperties` | **Works** — full control, but requires maintaining a separate nuspec file per managed package |
| 2 | Post-pack nuspec patching via Cake (unzip → patch XML → rezip) | **Would work** — but fragile and bypasses the build system's own validation |
| 3 | MSBuild property injection (`ExactVersion=true` or similar) | **Does not exist** — no such property on `ProjectReference` |
| 4 | `PrivateAssets="all"` on ProjectReference + explicit `PackageReference` with bracket notation | **Works** — clean, standard MSBuild, no extra files |

### Chosen Mechanism: Approach 4 (Mechanism 3)

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
  <!-- Native: exact pin dependency injected into pack output -->
  <!-- Version follows family version variable; in production, from MinVer / CLI -->
  <PackageVersion Include="Janset.SDL2.Image.Native" Version="[$(ImageFamilyVersion)]" />
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

**LibGit2Sharp** (production): Uses `PackageReference Include="LibGit2Sharp.NativeBinaries" Version="[2.0.323]"` for exact pin of its native binaries package. Visible on [NuGet.org](https://www.nuget.org/packages/LibGit2Sharp/0.31.0) as `= 2.0.323`.

**SkiaSharp**: Uses minimum-version (`>=`) for all NativeAssets dependencies. Different policy choice — they accept newer patch versions of native assets.

### Implications for csproj Topology

Every managed satellite package needs these 3 lines added:

1. `PrivateAssets="all"` on the existing Native `ProjectReference`
2. A `PackageVersion` item for the Native package with bracket notation
3. A `PackageReference` item for the Native package

Core family needs the same 3 lines (minus the cross-family ProjectReference to Core, since Core has no cross-family dependency).

**No `.nuspec` template files needed.** No custom MSBuild targets. No post-pack patching. Standard MSBuild/NuGet features only.

### Version Injection Strategy

The `$(ImageFamilyVersion)` property (or `$(CoreFamilyVersion)`, etc.) controls the exact pin version. In production:

- **MinVer** sets the family version from git tags (Stream A-risky)
- **CLI override** via `dotnet pack -p:ImageFamilyVersion=x.y.z` for explicit control
- **Cake orchestration** passes the resolved family version to both restore and pack

The `CoreMinVersion` property (cross-family minimum) is a separate concern. For the default ProjectReference behavior, the version emitted is whatever the referenced project resolves to at pack time. If finer control is needed (e.g., pinning the floor at `1.2.0` even when Core is at `1.5.0`), that is a Stream D-local concern, not A0.

### Resolution

**PD-2 is resolved.** The mechanism produces both within-family exact pin `[x.y.z]` and cross-family minimum range `x.y.z` in the same `.nupkg`, using only standard MSBuild/NuGet features.

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

### Does It Solve Our Exact Pin Problem?

**No.** NuGetizer has the same limitation: `ProjectReference` → dependency conversion always uses minimum-inclusive range. There is no per-reference metadata to control version range format. The workaround in NuGetizer would be manual `PackageFile` items with `PackFolder="Dependency"` and explicit `Version="[1.0.3]"` — functionally equivalent to our Mechanism 3 or the nuspec template approach.

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

| Capability | Stock `dotnet pack` + Mechanism 3 | NuGetizer |
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

**Not adopting NuGetizer for Phase 2a.** The stock MSBuild pack pipeline with Mechanism 3 handles all current requirements. NuGetizer does not solve our critical problem (exact pin) any better than the chosen approach.

**Not discarding either.** Two scenarios where it could become relevant:

1. **Phase 2b+:** If packaging iteration becomes a frequent pain point, `dotnet-nugetize` can be installed as a standalone global tool (`dotnet tool install -g dotnet-nugetize`) for layout preview without adopting the full NuGetizer PackageReference. This is a zero-risk way to get the best feature.

2. **Phase 3 (SDL2 Complete):** If the per-satellite packaging grows complex enough that the stock `Content` + `PackagePath` approach becomes unwieldy across 6+ native packages, NuGetizer's unified `PackageFile` model could simplify maintenance. Re-evaluate at that point.

---

## Sources

- [NuGet Package Versioning](https://learn.microsoft.com/en-us/nuget/concepts/package-versioning) — version range notation reference
- [NuGet pack and restore as MSBuild targets](https://learn.microsoft.com/en-us/nuget/reference/msbuild-targets) — NuspecFile, NuspecProperties, pack target internals
- [NuGet/Home#5525](https://github.com/NuGet/Home/issues/5525) — request for exact version on ProjectReference (closed as duplicate)
- [NuGet/Home#5556](https://github.com/NuGet/Home/issues/5556) — request for upper-limit version on ProjectReference (open since 2017)
- [NuGet/NuGet.Client PR#3097](https://github.com/NuGet/NuGet.Client/pull/3097) — VersionRange support for project references (merged Feb 2020)
- [LibGit2Sharp on NuGet.org](https://www.nuget.org/packages/LibGit2Sharp/) — production exact pin precedent
- [SkiaSharp on NuGet.org](https://www.nuget.org/packages/SkiaSharp/) — alternative approach (minimum range for native assets)
- [devlooped/nugetizer](https://github.com/devlooped/nugetizer) — NuGetizer project repository
- [NuGetizer 3000 spec](https://github.com/NuGet/Home/wiki/NuGetizer-3000) — original NuGet team proposal
