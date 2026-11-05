# Package Topology Refactor — Impact Map

> Phase 0 step 4 deliverable per [`strategy-brief.md`](strategy-brief.md). File-by-file mechanical impact for the role-metapackage refactor. Not canonical project policy yet — promote into ADR-001 successor, release-guardrails, playbooks, onboarding, and README after Phase 6.
>
> **Companion to** the strategic decisions in [`strategy-brief.md`](strategy-brief.md). This document is the mechanical contract: every file path, class name, method, MSBuild property, manifest field, and guardrail this refactor touches. Phase 1 characterization tests use this map as ground truth.

## 1. CPM-Aware Role-Meta Csproj — Phase 0 Step 3 Candidate

### Direction selected for spike validation: `ProjectReference` + extended `DependencyRangeNormalizer`

This is the **candidate** to validate during the Phase 0 step 3 spike — not a locked decision. The spike must produce real `dotnet pack` output against a CPM-enabled csproj before the choice graduates from "candidate" to "settled". Acceptance criteria below.

Two CPM-compatible approaches under consideration:

| Option | Mechanism | Current assessment |
| --- | --- | --- |
| **A — `ProjectReference` + extended normalizer** | Role meta csproj declares peer Bindings + Native as `<ProjectReference>`. `dotnet pack` auto-emits `>=$(Version)` nuspec deps. `DependencyRangeNormalizer` (existing) extends with a within-family pass that rewrites those to `[$(Version)]` exact range. Cross-family pass continues with a corrected lower-bound source (see §4.C). | **Leading candidate.** Uniform with current bindings csproj pattern. Single normalize pipeline. Symmetric reasoning across all dep types. Subject to spike confirmation that `<ProjectReference>` between two `ManagePackageVersionsCentrally=true` csprojs emits a clean `>=$(Version)` dep on the consuming side. |
| **B — `PackageReference` + `VersionOverride="[$(Version)]"`** | Role meta csproj declares peer Bindings + Native as `<PackageReference Include="..." VersionOverride="[$(Version)]" />`. Pack-time resolution emits `[$(Version)]` directly into nuspec. | **Not categorically rejected.** The repo already uses `VersionOverride` with bracket syntax on the consumer side (`Janset.Smoke.targets:25`, `VersionOverride="[$(JansetSdl2CorePackageVersion)]"`) — proof that the bracket exact-pin survives CPM at restore/build time. Spike must verify the producer side: whether `<PackageReference>` to an internal Janset package (with no `<PackageVersion>` row) passes pack-time validation under CPM. If A's `ProjectReference` path turns out to not emit deps cleanly (mirroring the `SuppressDependenciesWhenPacking=true` hazard called out in §3.C), B becomes the fallback. |

### Spike acceptance criteria

The Phase 0 step 3 spike has produced an answer when:

1. A throwaway role-meta-style csproj packs against the existing `Directory.Build.props` + `Directory.Packages.props` chain WITHOUT manual edits.
2. The emitted nuspec carries the expected `<dependency id="Janset.SDL<M>.<Role>.Bindings" version="..."/>` entry.
3. The post-pack normalizer (extended) successfully rewrites that entry to `[$(Version)]`.
4. Restore from a local feed pulls the role meta plus both layered packages by their actual `$(Version)`, not a fallback.

Until the spike clears all four, the canonical decision row in `strategy-brief.md` remains "Role metapackage implementation — Prefer SDK-style packable projects, but spike the exact template under CPM before locking XML."

### Concrete csproj contract for role metapackages (candidate)

### Concrete csproj contract for role metapackages

Tentative location: `src/meta/SDL2.<Role>/SDL2.<Role>.csproj`, parallel to `src/native/SDL2.<Role>.Native/`. See §3.

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <PackageId>Janset.SDL2.<Role></PackageId>
    <Description>SDL2_<role> .NET bindings + native binaries (meta-package).</Description>

    <!-- Assetless metapackage. lib/ stays empty via _._ placeholder; symbols disabled.
         CRITICAL: root Directory.Build.props sets <TargetFrameworks>$(LibraryTargetFrameworks)</TargetFrameworks>
         (multi-TFM by default). Meta is single-TFM, so we MUST clear the plural form first.
         MSBuild's SDK targets prefer <TargetFrameworks> over <TargetFramework> when both are set;
         leaving the inherited plural value triggers multi-targeting and produces 5 nupkgs
         (one per inherited TFM) instead of a single assetless package. -->
    <TargetFrameworks></TargetFrameworks>
    <TargetFramework>netstandard2.0</TargetFramework>
    <IncludeBuildOutput>false</IncludeBuildOutput>
    <IncludeSymbols>false</IncludeSymbols>
    <SymbolPackageFormat></SymbolPackageFormat>
    <NoWarn>$(NoWarn);NU5128</NoWarn>     <!-- "no lib/ref assemblies" suppressed -->
  </PropertyGroup>

  <!-- NU5128 placeholder so NuGet considers lib/netstandard2.0/ populated. -->
  <ItemGroup>
    <None Include="_._" Pack="true" PackagePath="lib/netstandard2.0/" />
  </ItemGroup>

  <!-- Within-family: same-role Bindings + Native. Pack-time emits `>=$(Version)` nuspec
       deps; extended DependencyRangeNormalizer rewrites them to `[$(Version)]` post-pack. -->
  <ItemGroup>
    <ProjectReference Include="..\..\SDL2.<Role>\SDL2.<Role>.csproj"
                      PrivateAssets="none" IncludeAssets="all" />
    <ProjectReference Include="..\..\native\SDL2.<Role>.Native\SDL2.<Role>.Native.csproj"
                      PrivateAssets="none" IncludeAssets="all" />
  </ItemGroup>

  <!-- Cross-family: core role meta (satellite-only; omit for sdl2-core).
       Pack emits `>=$(Version)`; existing G56 cross-family pass rewrites to `[lower, upper)`. -->
  <ItemGroup Condition="'$(PackageId)' != 'Janset.SDL2.Core'">
    <ProjectReference Include="..\SDL2.Core\SDL2.Core.csproj" />
  </ItemGroup>
</Project>
```

A bare `_._` file lives next to the csproj.

### Why this works under CPM

- `<ProjectReference>` does not consult `Directory.Packages.props`. The version of the emitted nuspec dependency comes from the referenced project's `$(Version)` at pack time, which is supplied uniformly by `PackageFamilyPacker` via `/p:Version=`.
- The role meta itself inherits `ManagePackageVersionsCentrally=true` from `Directory.Build.props` (line 62) but emits no `<PackageReference>`s, so CPM has nothing to validate.
- Existing bindings csproj proves the pattern: today's `src/SDL2.Image/SDL2.Image.csproj` already uses `<ProjectReference Include="..\SDL2.Core\SDL2.Core.csproj" />` cross-family under CPM and packs cleanly.

### Normalizer extension overview

`DependencyRangeNormalizer.NormalizeAsync` (today: single cross-family rewrite pass keyed on `family.DependsOn`) extends to **three modes**, dispatched per-artifact by `PackageFamilyPacker`:

| Mode | Applied to | Behavior |
| --- | --- | --- |
| `CrossFamilyRewrite` | Bindings nuspec | For each `dep` in `family.DependsOn`, find the matching `<dependency>` element by Bindings PackageId of the dep family, and rewrite `version` to `[<depFamilyVersion>, (UpstreamMajor+1).0.0)`. Lower bound comes from the **dep family's resolved version**, NOT the current family's version (see §4.C plumbing). |
| `CrossFamilyAndWithinFamilyRewrite` | Role meta nuspec | Runs the cross-family rewrite at the meta-PackageId layer AND a within-family pass that rewrites `<dependency id="Janset.SDL<M>.<Role>.Bindings\|.Native">` to `[$(Version)]` exact. The within-family pass **always runs** when the artifact is a role meta, independent of `family.DependsOn.Count` — Core role meta has no cross-family deps but must still exact-pin its within-family pair. |
| `CrossFamilySynthesize` | Satellite Native nuspec | Native csprojs ship under `src/native/Directory.Build.props` which sets `SuppressDependenciesWhenPacking=true` (line 6). The auto-emitted nuspec has zero `<dependencies>` group. To declare the hybrid-static cross-family dep on `Janset.SDL<M>.Core.Native`, the normalizer **synthesizes** the missing element rather than rewriting an existing one. See §3.C for the rationale. Core family is exempt (no cross-family deps). |

Mode is supplied per `NormalizeAsync` invocation. Each pack artifact (bindings, native, meta) gets the mode it needs. See §4.C for `PackageFamilyPacker` orchestration.

---

## 2. Manifest Schema v2.1 → v2.2 Diff

### JSON shape changes

```jsonc
{
  "schema_version": "2.2",     // ← was "2.1"

  "packaging_config": { /* unchanged */ },
  "runtimes": [ /* unchanged */ ],

  "package_families": [
    {
      "name": "sdl2-image",
      "tag_prefix": "sdl2-image",
      // RENAMED + ADDED
      "bindings_project": "src/SDL2.Image/SDL2.Image.csproj",      // was: managed_project
      "native_project":   "src/native/SDL2.Image.Native/SDL2.Image.Native.csproj",
      "meta_project":     "src/meta/SDL2.Image/SDL2.Image.csproj",  // NEW
      "library_ref": "SDL2_image",
      "depends_on": ["sdl2-core"],
      "change_paths": [
        "src/SDL2.Image/**",
        "src/native/SDL2.Image.Native/**",
        "src/meta/SDL2.Image/**"                                    // NEW path
      ]
    }
    // … 4 more families: sdl2-core, sdl2-mixer, sdl2-ttf, sdl2-gfx
  ],

  "system_exclusions": { /* unchanged */ },
  "library_manifests": [ /* unchanged */ ]
}
```

**Touch points:**
- `build/manifest.json` — schema_version bump + 5 family entries restructured (10 fields renamed/added across all families) + change_paths extended.

### C# model changes — `build/_build/Data/Manifest/Models/ManifestConfigModels.cs:64-73`

```csharp
public record PackageFamilyConfig
{
    [JsonPropertyName("name")]              public required string Name { get; init; }
    [JsonPropertyName("tag_prefix")]        public required string TagPrefix { get; init; }
    [JsonPropertyName("bindings_project")]  public string? BindingsProject { get; init; }   // was: ManagedProject
    [JsonPropertyName("native_project")]    public string? NativeProject { get; init; }      // unchanged
    [JsonPropertyName("meta_project")]      public string? MetaProject { get; init; }        // NEW
    [JsonPropertyName("library_ref")]       public required string LibraryRef { get; init; }
    [JsonPropertyName("depends_on")]        public required IImmutableList<string> DependsOn { get; init; }
    [JsonPropertyName("change_paths")]      public required IImmutableList<string> ChangePaths { get; init; }
}
```

Property rename `ManagedProject` → `BindingsProject` is a breaking change across the build host (15+ usages). See §5 for the call-site list.

### Repository / parser surface — `Data/Manifest/ManifestRepository.cs`

Schema handling during the refactor follows the **Phase 2b dual-mode pattern**:

- Phase 2b: parser accepts BOTH `schema_version = "2.1"` (legacy, single `managed_project`) AND `"2.2"` (new triplet) until `manifest.json` commits the bump.
- Phase 3: `manifest.json` commits `schema_version = "2.2"` atomically with csproj structural changes. Parser still accepts both for the duration of any in-flight branches.
- Phase 6: parser drops 2.1 acceptance as part of the canonical-doc consolidation slice. Hard-fail with actionable error pointing to the migration commit.

Phase 0 spike-output should confirm where the schema_version check happens today; if not centralized, add a single guard in `ManifestRepository.LoadAsync` before deserialization.

### Validators that read these fields

| Validator | Today's field access | After |
| --- | --- | --- |
| `CsprojPackContractValidator.ValidateManagedCsproj` | `family.ManagedProject` (line 79) | `family.BindingsProject` |
| `CsprojPackContractValidator.ValidateNativeCsproj` | `family.NativeProject` (line 93) | unchanged |
| (NEW) `CsprojPackContractValidator.ValidateMetaCsproj` | n/a | `family.MetaProject` |
| `PackageFamilyPacker.PackAsync` | `family.ManagedProject` (line 71), `family.NativeProject` (line 72) | `family.BindingsProject` + `family.NativeProject` + `family.MetaProject` |
| `CsprojPackContractValidator.CheckNativeProjectReferencePath` | `family.NativeProject` (line 180) | RETIRED — bindings must NOT reference native; new rule says **absence** check |

---

## 3. Csproj Surface Impact

### A. Rename PackageId on existing managed csprojs (5 files)

`src/SDL2.<Role>/SDL2.<Role>.csproj` for `<Role>` ∈ {Core, Image, Mixer, Ttf, Gfx}:

```xml
<PackageId>Janset.SDL2.<Role></PackageId>            <!-- was -->
<PackageId>Janset.SDL2.<Role>.Bindings</PackageId>   <!-- becomes -->
```

**Folder names unchanged** — strategy-brief.md §Current Open Decisions table records "Avoid initial folder renames". Csproj filename and folder stay `SDL2.<Role>/SDL2.<Role>.csproj` even though PackageId carries `.Bindings`. Mild code smell, deliberate trade-off.

### B. Remove same-family Native `<ProjectReference>` from bindings csprojs

Today (`src/SDL2.Image/SDL2.Image.csproj` lines 13-15):

```xml
<ItemGroup>
  <ProjectReference Include="..\SDL2.Core\SDL2.Core.csproj"/>
  <ProjectReference Include="..\native\SDL2.Image.Native\SDL2.Image.Native.csproj"
                    IncludeAssets="all"
                    PrivateAssets="none"/>
</ItemGroup>
```

After: drop the within-family Native ProjectReference. Bindings package emits NO same-family Native dep.

```xml
<ItemGroup>
  <ProjectReference Include="..\SDL2.Core\SDL2.Core.csproj"/>    <!-- cross-family only -->
</ItemGroup>
```

For `sdl2-core`'s bindings csproj: no `<ProjectReference>` items at all (core is dep-less).

### C. Satellite Native cross-family dep — post-pack synthesize (not ProjectReference)

Today `src/native/SDL2.Image.Native/SDL2.Image.Native.csproj` is intentionally dep-less:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <PackageId>Janset.SDL2.Image.Native</PackageId>
    <Description>Native SDL_image libraries built via vcpkg</Description>
  </PropertyGroup>
</Project>
```

**Why adding `<ProjectReference Include="..\SDL2.Core.Native\SDL2.Core.Native.csproj" />` does NOT work:**

`src/native/Directory.Build.props:6` sets `<SuppressDependenciesWhenPacking>true</SuppressDependenciesWhenPacking>` for all Native csprojs. Under that flag, `dotnet pack` deliberately **omits** all `<dependency>` elements from the emitted nuspec — even when a `ProjectReference` would normally produce one. The existing `DependencyRangeNormalizer` rewrites elements that exist; it does not synthesize missing ones. So a naïve ProjectReference addition would leave satellite Native nuspecs with zero `<dependency>` declarations on Core Native — and the override-path consumer that installs only `Janset.SDL2.Image.Native` would silently miss SDL2.dll at runtime.

**Two design paths considered:**

| Path | Mechanism | Verdict |
| --- | --- | --- |
| C1 — Override `SuppressDependenciesWhenPacking=false` for satellite Native | Property override in the 4 satellite Native csprojs (or in a sibling Directory.Build.props). Adds `ProjectReference` to Core Native. Pack auto-emits the dep. Normalizer rewrites to bounded range per §4.C. | Mixes the existing "Native payload only, no deps" intent with a new "Native payload + one dep" hybrid. Risks silent regressions if other transitive analyzer/SourceLink packages leak into nuspec. |
| **C2 — Post-pack synthesize the cross-family dep (selected)** | Native csproj stays minimal and dep-less. After `dotnet pack`, `DependencyRangeNormalizer` (mode `CrossFamilySynthesize`, see §1) adds a `<dependency id="Janset.SDL<M>.Core.Native" version="[<coreFamilyVersion>, (UpstreamMajor+1).0.0)"/>` element to the satellite Native nuspec. Element is created (not rewritten), then standard bounded-range applies. | **Selected.** Keeps `SuppressDependenciesWhenPacking=true` invariant intact; centralizes ALL cross-family dep logic in one normalizer; symmetric with §1 design intent. |

Per Deniz's review note: C2 is the more controlled option because it leaves the pack-time contract untouched and concentrates rewrite vs synthesize decisions in one component the project already trusts.

**Impact:** No csproj change in `src/native/SDL2.<Role>.Native/`. The normalizer + `PackageFamilyPacker` carry the full new responsibility (see §4.C). Touches 0 Native csprojs in `src/`; touches `DependencyRangeNormalizer` only.

### D. Add 5 new role meta csprojs

Path: `src/meta/SDL2.<Role>/SDL2.<Role>.csproj` for `<Role>` ∈ {Core, Image, Mixer, Ttf, Gfx}.

Contract per §1 template. `Janset.SDL2.Core` meta has no cross-family ProjectReference; satellites include `..\SDL2.Core\SDL2.Core.csproj`.

Sibling `_._` file at `src/meta/SDL2.<Role>/_._` (5 placeholder files, byte content empty).

### E. New Directory.Build.props for meta tree (optional, recommended)

`src/meta/Directory.Build.props` — symmetric to `src/native/Directory.Build.props` if one exists. Carries:
- `<IsPackable>true</IsPackable>` (default)
- analyzer suppression for assetless csprojs if needed
- shared `<TargetFramework>netstandard2.0</TargetFramework>` if all 5 metas share it

Phase 0 should compare with `src/native/Directory.Build.props` if it exists to mirror the structure.

### F. ProjectReference graph (after refactor)

```text
src/meta/SDL2.Core/SDL2.Core.csproj
  ├── src/SDL2.Core/SDL2.Core.csproj                 (within-family, → Bindings)
  └── src/native/SDL2.Core.Native/SDL2.Core.Native.csproj   (within-family, → Native)

src/meta/SDL2.Image/SDL2.Image.csproj
  ├── src/SDL2.Image/SDL2.Image.csproj
  ├── src/native/SDL2.Image.Native/SDL2.Image.Native.csproj
  └── src/meta/SDL2.Core/SDL2.Core.csproj            (cross-family meta-to-meta)

src/SDL2.Core/SDL2.Core.csproj
  (no ProjectReference)

src/SDL2.Image/SDL2.Image.csproj
  └── src/SDL2.Core/SDL2.Core.csproj                 (cross-family bindings-to-bindings)

src/native/SDL2.Core.Native/SDL2.Core.Native.csproj
  (no ProjectReference)

src/native/SDL2.Image.Native/SDL2.Image.Native.csproj
  (no ProjectReference — cross-family Core.Native dep synthesized post-pack; see §3.C)
```

Cross-family deps live at **the same layer** (Bindings→Bindings at the csproj level; Native→Native synthesized post-pack; Meta→Meta at the csproj level). Within-family deps live in the meta only (Meta→Bindings, Meta→Native). Bindings does NOT reference Native within-family (override-scenario invariant).

---

## 4. Build Host Surface Impact

### A. `PackageArtifacts` model — `build/_build/Targets/Package/Models/PackageArtifacts.cs`

Today: 3 fields (`ManagedPackage`, `ManagedSymbolsPackage`, `NativePackage`).

After: 4 fields. Rename + add.

```csharp
public sealed record PackageArtifacts(
    FilePath BindingsPackage,           // was: ManagedPackage
    FilePath BindingsSymbolsPackage,    // was: ManagedSymbolsPackage
    FilePath NativePackage,             // unchanged
    FilePath RoleMetaPackage);          // NEW
```

### B. `PackageFamilyPacker.PackAsync` — `build/_build/Targets/Package/Services/PackageFamilyPacker.cs:57-130`

Today (lines 93-94): pack native, then managed. Two `dotnet pack` invocations per family.

After: pack native, bindings, then role meta. Three `dotnet pack` invocations per family, same `$(Version)`, same `$(NativePayloadSource)` (native only).

```csharp
// Phase 3: PackAndValidate
PackProject(nativeProjectPath,  buildConfiguration, version, nativePayloadSource, …);
PackProject(bindingsProjectPath, buildConfiguration, version, nativePayloadSource: null, …);
PackProject(metaProjectPath,     buildConfiguration, version, nativePayloadSource: null, …);

var artifacts  = CreateArtifacts(family, version);
// `versionSet` is constructed once by PackageTask (via IVersionFileRepository.Load — see §4.C)
// and passed into each PackageFamilyPacker.PackAsync call. PackageFamilyPacker does NOT
// load versions.json directly; that responsibility stays at PackageTask orchestration.

// Each artifact gets the normalize mode it needs (see §1 + §4.C):
await _dependencyRangeNormalizer.NormalizeAsync(manifestConfig, family, versionSet,
    DependencyNormalizationMode.CrossFamilyRewrite,        artifacts.BindingsPackage, version, ct);
await _dependencyRangeNormalizer.NormalizeAsync(manifestConfig, family, versionSet,
    DependencyNormalizationMode.CrossFamilySynthesize,     artifacts.NativePackage,   version, ct);
await _dependencyRangeNormalizer.NormalizeAsync(manifestConfig, family, versionSet,
    DependencyNormalizationMode.CrossFamilyAndWithinFamilyRewrite, artifacts.RoleMetaPackage, version, ct);
```

The comment at `PackageFamilyPacker.cs:84-90` ("Within-family dependency is SkiaSharp-style minimum range. No exact-pin CPM plumbing.") describes today's pattern. After refactor: comment block updates to describe the per-artifact mode dispatch and document that within-family exact-pin lives inside the role meta nuspec only (Bindings + Native nuspecs stay SkiaSharp-style minimum-range with corrected cross-family lower bound).

### C. `DependencyRangeNormalizer.NormalizeAsync` — `Services/DependencyRangeNormalizer.cs`

Three changes land in this class:

1. **Mode parameter** controls which passes run (see §1).
2. **`PackageFamilyVersionSet` plumbed in** so cross-family lower bounds come from the **dep family's** version, not the current family's version.
3. **Synthesize path** added for satellite Native nuspec deps that cannot exist before pack (because of `SuppressDependenciesWhenPacking=true`; see §3.C).

#### Today's lower-bound bug (must fix as part of the refactor)

Today's normalizer accepts a single `string lowerBoundVersion` argument and uses it as the lower bound for every cross-family dep range it rewrites:

```csharp
// Current behavior — DependencyRangeNormalizer.cs:104-110
var expectedRange = $"[{lowerBoundVersion}, {upperBound})";
```

`lowerBoundVersion` is passed in from `PackageFamilyPacker.PackAsync` as the **current packing family's version**:

```csharp
// PackageFamilyPacker.cs:103 — current call site
await _dependencyRangeNormalizer.NormalizeAsync(manifestConfig, family, artifacts.ManagedPackage, version, ct);
//                                                                                                ^^^^^^^ current family's version
```

That means `Janset.SDL2.Image 2.8.0` packing today emits a Core dep range of `[2.8.0, 3.0.0)` — but Core is at `2.32.x`, not `2.8.x`. The range happens to match Core 2.32.x by luck (since `2.32 > 2.8`), but the lower bound is semantically wrong: it tells consumers "Image 2.8.0 works with Core ≥ 2.8.0" when in fact no Core 2.8.x exists. Real correctness on a feed with prereleases or partial uploads depends on this lower bound.

Correct shape:

```text
Image 2.8.0 → Core dep range = [<coreFamilyVersion>, (Core.UpstreamMajor + 1).0.0)
                              = [2.32.0, 3.0.0)   (when Core's resolved family version is 2.32.0)
```

#### Fix: plumb resolved family versions through the normalizer

`versions.json` (written by `ResolveVersionsFromManifest`) already carries the per-family resolved version map. Today `PublishStagingTask` reads it via `IVersionFileRepository.Load(context.VersionsFilePath)` (`PublishStagingTask.cs:75`). `PackageTask` follows the same pattern. **`PackageFamilyPacker` does NOT receive a version map today** — it only knows the current packing family's version via the `version` argument. The refactor introduces this plumbing.

Three new artifacts in Phase 4:

| Artifact | Owner | Note |
| --- | --- | --- |
| `PackageFamilyVersionSet` (NEW immutable record) | `Build.Data.Versions` or `Build.Targets.Package.Models` | Wraps `IReadOnlyDictionary<string, NuGetVersion>`. Constructed once per `PackageTask` invocation; passed into each `PackageFamilyPacker.PackAsync`. NOT a `FromVersionsFile` factory — read happens via existing `IVersionFileRepository.Load`. |
| `PackageTask` orchestration update | `Build.Targets.Package.PackageTask` | Load versions via `IVersionFileRepository.Load(context.VersionsFilePath)` (mirroring `PublishStagingTask.cs:75`), construct `PackageFamilyVersionSet`, pass through to `PackageFamilyPacker.PackAsync(..., versionSet, ...)`. |
| `PackageFamilyPacker.PackAsync` signature update | `Build.Targets.Package.Services.PackageFamilyPacker` | Add `PackageFamilyVersionSet versionSet` parameter; forward to `DependencyRangeNormalizer.NormalizeAsync` calls. |

Sketch:

```csharp
public sealed record PackageFamilyVersionSet(IReadOnlyDictionary<string, NuGetVersion> ByFamilyName)
{
    public NuGetVersion Resolve(string familyName) =>
        ByFamilyName.TryGetValue(familyName, out var v)
            ? v
            : throw new CakeException(
                $"Family '{familyName}' has no resolved version in the version set. " +
                "Ensure ResolveVersionsFromManifest ran and the scope includes this family.");
}

// DependencyRangeNormalizer extended signature
public async Task NormalizeAsync(
    ManifestConfig manifestConfig,
    PackageFamilyConfig family,
    PackageFamilyVersionSet versionSet,             // NEW (Phase 4)
    DependencyNormalizationMode mode,                // NEW
    FilePath packagePath,
    string currentFamilyVersion,                     // = versionSet.Resolve(family.Name); kept for clarity at call sites
    CancellationToken ct)
```

Cross-family rewrite loop becomes:

```csharp
foreach (var dependencyFamilyName in family.DependsOn)
{
    var depFamilyVersion = versionSet.Resolve(dependencyFamilyName);   // ← was: lowerBoundVersion
    var depUpperBound    = ResolveCrossFamilyUpperBound(manifestConfig, dependencyFamilyName);
    var expectedRange    = $"[{depFamilyVersion.ToNormalizedString()}, {depUpperBound})";
    // … find <dependency id="<dep PackageId at the layer being normalized>"> and rewrite
}
```

The "layer being normalized" branches by `mode`:
- Bindings nuspec → `BindingsPackageId(dependencyFamilyName)`
- Native nuspec   → `NativePackageId(dependencyFamilyName)` (synthesized when missing, see below)
- Meta nuspec     → `RoleMetaPackageId(dependencyFamilyName)` for cross-family + `BindingsPackageId(family.Name)` / `NativePackageId(family.Name)` for within-family exact-pin

#### Synthesize path (satellite Native nuspec)

When `mode == CrossFamilySynthesize`, the existing nuspec almost certainly has no `<dependencies>` group (because `SuppressDependenciesWhenPacking=true`). The synthesize helper must do three things explicitly: (a) create missing parent elements AND attach them, (b) deduplicate against any pre-existing element for the same dep id, (c) NOT exclude `Build` from the synthesized dep — Core.Native ships `buildTransitive/Janset.SDL2.Native.Common.targets` and the consumer MUST receive that import or the runtime payload extraction (`tar -xzf` on Unix, AnyCPU DLL copy on .NETFramework) never fires.

Excluding `Build` (or any `buildTransitive`) on this specific dep would silently produce a working pack that fails at consumer runtime with `DllNotFoundException` — exactly the failure class hybrid-static is designed to prevent.

```csharp
// Within DependencyRangeNormalizer when mode == CrossFamilySynthesize:
var metadataElement = root.Element(ns + "metadata")
    ?? throw new CakeException("Native nuspec is missing <metadata> element — package is structurally broken.");

var dependenciesElement = metadataElement.Element(ns + "dependencies");
if (dependenciesElement is null)
{
    dependenciesElement = new XElement(ns + "dependencies");
    metadataElement.Add(dependenciesElement);     // ATTACH — XElement created with new is detached until added
}

// Generic group (no targetFramework attribute) — native nupkgs have no TFM-specific lib content.
var groupElement = dependenciesElement.Element(ns + "group");
if (groupElement is null)
{
    groupElement = new XElement(ns + "group");
    dependenciesElement.Add(groupElement);        // ATTACH
}

foreach (var depFamilyName in family.DependsOn)
{
    var depId    = FamilyIdentifierConventions.NativePackageId(depFamilyName);
    var depRange = ComputeBoundedRange(versionSet.Resolve(depFamilyName), manifestConfig, depFamilyName);

    // Dedup: if a previous synthesize pass (or some other producer) already inserted this dep,
    // replace rather than create a duplicate. NuGet rejects duplicate dep ids in the same group.
    var existing = groupElement
        .Elements(ns + "dependency")
        .FirstOrDefault(e => string.Equals((string?)e.Attribute("id"), depId, StringComparison.OrdinalIgnoreCase));
    existing?.Remove();

    // NO `exclude` attribute — Core.Native's buildTransitive contract (Janset.SDL2.Native.Common.targets
    // + the per-family $(PackageId).targets wrapper) MUST flow through to consumers. Setting
    // exclude="Build" would suppress the .targets import and break Unix tar extraction + .NETFramework
    // AnyCPU DLL copy at consumer build time.
    groupElement.Add(new XElement(ns + "dependency",
        new XAttribute("id", depId),
        new XAttribute("version", depRange)));
}
```

Spike acceptance for the synthesize path: pack one satellite Native, open the resulting nupkg, confirm the `<dependencies>` group structure matches a hand-authored nuspec emitted by `dotnet pack` without `SuppressDependenciesWhenPacking`. Verify `nuget restore` on a sample consumer resolves the cross-family dep and the `buildTransitive` import chain fires (Unix tar extract executes; .NETFramework AnyCPU copy executes).

#### Within-family exact-pin pass (meta nuspec only)

```csharp
if (mode == DependencyNormalizationMode.CrossFamilyAndWithinFamilyRewrite)
{
    var bindingsId = FamilyIdentifierConventions.BindingsPackageId(family.Name);
    var nativeId   = FamilyIdentifierConventions.NativePackageId(family.Name);
    var exactPin   = $"[{currentFamilyVersion}]";

    foreach (var dependencyElement in dependencyElements)
    {
        var depId = (string?)dependencyElement.Attribute("id");
        if (string.Equals(depId, bindingsId, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(depId, nativeId,   StringComparison.OrdinalIgnoreCase))
        {
            dependencyElement.SetAttributeValue("version", exactPin);
            hasChanges = true;
        }
    }
}
```

This pass runs **unconditionally** for meta artifacts — Core role meta has `family.DependsOn.Count == 0` but still must exact-pin its Bindings + Native pair. The legacy `if (family.DependsOn.Count == 0) return;` guard at `DependencyRangeNormalizer.cs:41-44` MUST be relocated to scope only the cross-family rewrite path, not the entire method.

### D. `FamilyIdentifierConventions` — `build/_build/Validation/Conventions/FamilyIdentifierConventions.cs`

Current methods (lines 57-86):
- `ManagedPackageId(familyId)` → `Janset.SDL<M>.<Role>`
- `NativePackageId(familyId)` → `Janset.SDL<M>.<Role>.Native`
- `VersionPropertyName(familyId)` → `JansetSdl<M><Role>PackageVersion`

After refactor:

```csharp
// SEMANTIC RENAME — what was "Managed" is now the role meta name.
public static string RoleMetaPackageId(string familyIdentifier)        // was: ManagedPackageId
{
    var (sdlMajor, role) = Parse(familyIdentifier);
    return string.Create(CultureInfo.InvariantCulture, $"Janset.SDL{sdlMajor}.{role}");
}

// NEW
public static string BindingsPackageId(string familyIdentifier)
{
    var (sdlMajor, role) = Parse(familyIdentifier);
    return string.Create(CultureInfo.InvariantCulture, $"Janset.SDL{sdlMajor}.{role}.Bindings");
}

// UNCHANGED
public static string NativePackageId(string familyIdentifier) { … }

// UNCHANGED — still keys consumer-side smoke MSBuild properties at family level
// (V1 family-lock: one version per family covers all 3 packages).
public static string VersionPropertyName(string familyIdentifier) { … }
```

**Method rename `ManagedPackageId` → `RoleMetaPackageId`** propagates across the build host. Call-site survey:

```text
build/_build/Validation/Manifest/CsprojPackContractValidator.cs:102, 163       (uses)
build/_build/Validation/Packaging/PackageOutputValidator.cs:83                  (uses)
build/_build/Targets/Package/Services/PackageFamilyPacker.cs:186                (uses)
build/_build/Targets/Package/Services/DependencyRangeNormalizer.cs:108          (uses for cross-family dep id lookup)
build/_build/Validation/Versioning/CrossFamilyDependencyResolvabilityValidator.cs   (uses)
build/_build/Validation/Packaging/SatelliteUpperBoundValidator.cs               (uses, likely)
build/_build/Validation/Packaging/ReadmeMappingTableValidator.cs                (uses)
build/_build/Targets/Package/Services/ReadmeMappingTableGenerator.cs            (uses)
```

(~10 call sites; verify with full grep during execution).

### E. `NativePackageMetadataGenerator` — unchanged

`build/_build/Targets/Package/Services/NativePackageMetadataGenerator.cs` writes G55 `janset-native-metadata.json` into the Native package. Topology change does not affect native payload metadata.

### F. `ReadmeMappingTableGenerator` + `ReadmeMappingTableBlock` — extend columns

`build/_build/Targets/Package/Services/ReadmeMappingTableGenerator.cs` produces the README table block validated by G57. After refactor the table grows: each row covers a family but should display all 3 packages.

Today's columns (approximate, verify): `Family | Managed Package | Native Package | Upstream Version | vcpkg port`.

After: `Family | Role Meta | Bindings | Native | Upstream Version | vcpkg port`. Or restructure as one row per package with role/layer columns. **Phase 4 design decision**, not strategy-level.

### G. PackageReporter — log strings

`build/_build/Targets/Package/Reporting/PackageReporter.cs` — strings like "Packed family X" probably reference "managed + native". Update to mention all 3 packages where the surface is user-visible.

---

## 4.5. `tools.cs` Surface — The Forgotten Validation Tool

`tools.cs` is a file-based .NET 10 app at repo root that orchestrates local-dev `setup` + `ci-sim` flows. AGENTS.md §"Build-Host Reference Pattern" says it **must not reference `build/_build` internals**, so it carries its own duplicates of the family-identity conventions + manifest model. The strategy-brief omitted this surface; it touches in three places.

Per Deniz: this is the project's biggest end-to-end validation tool. If `tools setup --source=local` produces consumable packages and `tools ci-sim` round-trips them, the refactor is real-world-correct. Treat as a hard validation gate at Phase 4 close.

### A. Duplicate family-identity helpers — `tools.cs:667-714`

Currently has standalone copies of:
- `ManagedPackageId(familyIdentifier)` → `Janset.SDL<M>.<Role>`
- `NativePackageId(familyIdentifier)` → `Janset.SDL<M>.<Role>.Native`
- `VersionPropertyName(familyIdentifier)` → `JansetSdl<M><Role>PackageVersion`
- `ParseFamily(familyIdentifier)`, `ToPascalCase(value)` — internal helpers

After refactor add:
- `RoleMetaPackageId(familyIdentifier)` → `Janset.SDL<M>.<Role>` (semantic rename of the old `ManagedPackageId`)
- `BindingsPackageId(familyIdentifier)` → `Janset.SDL<M>.<Role>.Bindings`
- `NativePackageId(familyIdentifier)` — unchanged
- `VersionPropertyName(familyIdentifier)` — unchanged (V1 family-lock; one version property covers all 3 packages)

Mirror the rename done in `build/_build/Validation/Conventions/FamilyIdentifierConventions.cs` (§4.D). Both source-of-truths drift independently — Phase 2a rename slice covers both files in one commit.

### B. Manifest model in `tools.cs:633-663`

Today's standalone `PackageFamilyConfig` record (lines 633-637):

```csharp
public sealed record PackageFamilyConfig(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("managed_project")] string? ManagedProject,
    [property: JsonPropertyName("native_project")] string? NativeProject,
    [property: JsonPropertyName("library_ref")] string LibraryRef);
```

After:

```csharp
public sealed record PackageFamilyConfig(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("bindings_project")] string? BindingsProject,   // renamed
    [property: JsonPropertyName("native_project")]   string? NativeProject,
    [property: JsonPropertyName("meta_project")]     string? MetaProject,        // new
    [property: JsonPropertyName("library_ref")]      string LibraryRef);
```

`GetConcreteFamilies` (line 642-663) filter at line 655:

```csharp
.Where(f => !string.IsNullOrWhiteSpace(f.ManagedProject) && !string.IsNullOrWhiteSpace(f.NativeProject))
```

becomes:

```csharp
.Where(f => !string.IsNullOrWhiteSpace(f.BindingsProject)
         && !string.IsNullOrWhiteSpace(f.NativeProject)
         && !string.IsNullOrWhiteSpace(f.MetaProject))
```

Phase 2b dual-mode handling is NOT needed in `tools.cs` because by the time Phase 3 commits schema 2.2, `tools.cs` updates atomically with `manifest.json`.

### C. Package consumption flows

| `tools.cs` symbol | Today | After |
| --- | --- | --- |
| `ExpectedNupkgPaths` (lines 766-773) | Returns 2 paths: managed + native | Returns 3 paths: bindings + native + meta. Used by `RunLocalAsync` to verify pack output (lines 194-203). |
| `WipeJansetPackages` (lines 775-792) | Wildcards `Janset.SDL2.*` + `Janset.SDL3.*` — already catches `.Bindings` + `.Native` + suffix-less meta | No change. Wildcards already correct. |
| `WriteLocalProps` (lines 718-740) | Writes one `<JansetSdl<M><Role>PackageVersion>` per family | No change. V1 family-lock; one version property still covers all 3 packages. |
| `WriteVersionsJson` (lines 744-756) | Writes one version per family | No change. |
| `RunRemoteGitHubAcquisitionStepAsync` (lines 265-357) | Per family: query latest version for managed + native, assert they agree, download both (2 packages per family) | Per family: query latest for bindings + native + meta, assert all three agree, download three. Update mismatch error messages to name all three packages. |
| `GetLatestRemoteVersionAsync` + `DownloadRemotePackageAsync` (lines 359-392) | Per-package helpers; package-agnostic | No change. Callers iterate the 3 IDs. |

### D. RunRemoteGitHubAsync mismatch detection

Today's check (lines 313-317):

```csharp
if (!VersionComparer.Default.Equals(managedVersion, nativeVersion))
{
    throw new RemoteGitHubSetupException(
        $"Family '{family.Name}' managed/native versions disagree: ...");
}
```

After: a three-way agreement check. The error message must name which of the three is out of sync. Common case under V1 family-lock: a partial publish where only 2 of 3 got pushed. The diagnostic should call out the missing third explicitly:

```csharp
var versions = new[] { ("bindings", bindingsVersion), ("native", nativeVersion), ("meta", metaVersion) };
if (versions.Select(v => v.Item2).Distinct(VersionComparer.Default).Count() > 1)
{
    throw new RemoteGitHubSetupException(
        $"Family '{family.Name}' three-package versions disagree: " +
        string.Join(", ", versions.Select(v => $"{v.Item1}@{v.Item2.ToNormalizedString()}")) +
        ". V1 family-lock requires all three to share a version. Likely a partial publish — re-run the failing publish wave or use --source=local.");
}
```

### E. Phase mapping for `tools.cs`

- Phase 2a: §4.5 A (helpers rename + add) — alongside `FamilyIdentifierConventions` rename.
- Phase 3: §4.5 B (manifest model) — alongside `manifest.json` schema bump.
- Phase 4: §4.5 C + D (consumption flow expansion to 3 packages) — alongside `PackageFamilyPacker` rewrite.
- Phase 5 validation: `tools setup --source=local` + `tools ci-sim` end-to-end pass on host RID; `tools setup --source=remote-github` exercised against staging GH Packages once a publish wave lands.

---

## 4.6. Build-Host Tasks Beyond Package — Smoke + Publish

The strategy-brief enumerated `PackageTask` impact but glossed over downstream Cake tasks that consume the pack output. These all hard-code the today's 2-package family pair (`Janset.SDL<M>.<Role>` + `Janset.SDL<M>.<Role>.Native`) and need explicit updates.

### A. `PackageConsumerSmokeTask` + `SmokeScopeComparator`

`build/_build/Targets/PackageConsumerSmoke/Services/SmokeScopeComparator.cs` is structurally aligned with the refactor — it parses any `<PackageReference Include="Janset.SDL*">` from a smoke csproj and compares against a manifest-derived expected set. Under the new topology:

- The smoke csproj `JansetSmokeSdl2Families` expansion (`Janset.Smoke.targets:24-41`) emits `<PackageReference Include="Janset.SDL2.<Role>" />`, which now resolves to the role meta. The comparator's csproj-parse side already accepts that shape.
- The "expected manifest-driven IDs" feed comes from `PackageConsumerSmokeTask`. Today it derives expected IDs from `ManifestConfig.PackageFamilies` filtered to concrete families and applies `FamilyIdentifierConventions.ManagedPackageId`. After §4.D rename, that becomes `RoleMetaPackageId` semantically — same string output (`Janset.SDL2.Image`), so smoke csprojs that already reference the suffix-less ID stay green.
- **Where the comparator MAY need attention**: if a smoke csproj wants to validate bindings-only-mode by referencing `Janset.SDL2.Image.Bindings`, the comparator's expected set must accept BOTH the meta ID AND the bindings ID for the same family. Phase 5 / Gap #3 co-design decision — defer until bindings-only smoke shape is locked.

`PackageConsumerSmokeTask` itself does not need to know about 3 vs 2 artifacts per family because consumer-side restore is layer-transparent: referencing the role meta transitively pulls bindings + native, restore + build proceed as before. The smoke runner's only family-aware surface is the comparator + the `JansetSmokeSdl<M>Families` family-list expansion.

### B. `EnsureSelectionSupportsCurrentSmokeScope` (Phase 2b Gap #3 surface)

Today's smoke gate `EnsureSelectionSupportsCurrentSmokeScope` (referenced in `phase-2-adaptation-plan.md` Gap #3) enforces "all-manifest-concrete-families OR none" against the explicit-version mapping. Under the new topology this check stays semantically valid — concrete families are still defined by having all 3 project paths declared in manifest schema 2.2, and the version mapping is still per-family. No structural change required for this refactor; Gap #3 closure remains independent work.

### C. `PublishStagingTask`

`build/_build/Targets/PublishStaging/PublishStagingTask.cs:31` today pushes a managed + native pair per family. After refactor: pushes bindings + native + role meta — 3 nupkgs per family per publish wave.

Specific changes:

- Per-family loop (the "two awaited pushes" referenced at `PublishStagingTask.cs:62`) becomes three awaited pushes: bindings nupkg, native nupkg, role meta nupkg.
- snupkg push surface: today pushes managed `.snupkg`. After: pushes bindings `.snupkg` (same file role, different filename). Native + role meta have no snupkg per §HOW Package layers table.
- Failure handling: today's partial-publish error message names "managed" and "native". After: names "bindings", "native", "meta" — three failure surfaces, three retry points.
- Order of pushes: today is managed-then-native (or vice versa). After: native → bindings → meta is the safe order, mirroring `PackageFamilyPacker` pack order, so a partial publish never publishes a role meta whose dep references a not-yet-published Bindings/Native.

### D. `PublishPublicTask`

`PublishPublicTask` is a stub today (PD-7 scope, `phase-2-adaptation-plan.md`). When it gets real implementation, it inherits the same 3-package-per-family contract as `PublishStagingTask`. Refactor does NOT activate it; just records the contract for the implementing slice.

### E. Phase mapping

- Phase 2b: SmokeScopeComparator + `EnsureSelectionSupportsCurrentSmokeScope` reviewed for dual-mode (manifest 2.1 vs 2.2) safety; no behavioral change yet.
- Phase 4: `PublishStagingTask` 3-push rewrite alongside `PackageFamilyPacker` 3-pack rewrite. Both must land in the same slice or in adjacent slices — a partial state where Pack produces 3 but Publish pushes 2 breaks staging feed coherence.
- Phase 5: bindings-only smoke design (Gap #3 co-design) decides whether SmokeScopeComparator's expected set needs to accept multiple IDs per family.

---

## 5. Guardrail Surface

Per AGENTS.md §Build-Host Reference Pattern, "Guardrail IDs are not code names." This section describes the **behaviors** that need to change; final IDs (split, retire, renumber) are a `knowledge-base/release-guardrails.md` decision in Phase 6. Below the `release-guardrails.md` current-state IDs are cited as locators, not as new-state assignments.

### A. Reframed behaviors

| Behavior today (`release-guardrails.md` locator) | Today's scope | After refactor |
| --- | --- | --- |
| **csproj PackageId canonical** (G6, §2.1) | `<PackageId>` of `managed_project` matches `Janset.SDL<M>.<Role>`; `native_project` matches `Janset.SDL<M>.<Role>.Native` | Split into three sibling rules: bindings PackageId carries `.Bindings` suffix; native unchanged; **new** meta PackageId is suffix-less canonical form. |
| **Native ProjectReference path** (G7, §2.1) | Managed csproj has exactly one ProjectReference resolving to `manifest.json package_families[].native_project` | Flipped + extended:<br>• Bindings csproj has **zero** ProjectReferences to same-family Native (override-scenario invariant)<br>• Meta csproj has exactly one ProjectReference to `bindings_project` AND exactly one to `native_project` (within-family)<br>• Native csproj reference rule retires — satellite Native cross-family dep is post-pack synthesized, not a ProjectReference (§3.C) |
| **Within-family Native dep emitted as bare minimum range** (G21, §2.5) | Managed nuspec emits Native dep with `>= x.y.z` (no brackets); within-family is SkiaSharp-style minimum range | Reframes by layer:<br>• Bindings nuspec carries zero same-family Native deps<br>• Native nuspec carries zero within-family deps (satellite native→core native is **cross**-family)<br>• Meta nuspec carries `[$(Version)]` exact-pin to both same-family Bindings AND Native — this is the topology-level reintroduction of exact-pin that strategy-brief.md §V1 family-lock commits to, scoped to meta only |
| **All TFM dep groups consistent** (G22, §2.5) | Multi-TFM bindings package nuspec has identical dep declarations across `net10/net9/net8/netstandard2.0/net462` groups | Continues to apply to bindings nuspec. Meta is single-TFM (`netstandard2.0` only) so it has one group; rule trivially holds. Native is assetless-of-TFM so no TFM group. |
| **Within-family `<version>` byte-equal** (G23, §2.5) | Native nuspec `<version>` == managed nuspec `<version>` | Generalize to 3-way: bindings nuspec `<version>` == native nuspec `<version>` == meta nuspec `<version>` |
| **Managed snupkg present + valid** (G25, §2.5) | Managed package's `.snupkg` present and well-formed | Scope narrows in name only — now keyed to the bindings package (which is the only artifact that emits symbols). Behavior identical. Meta has `<IncludeSymbols>false</IncludeSymbols>`, native has no managed assemblies — neither produces snupkg. |
| **Cross-family upper bound declared** (G56, §2.5) | Satellite cross-family dep on Core declares both lower bound (`>= x.y.z`) AND upper bound (`< (UpstreamMajor+1).0.0`) | Applies at three layers under the new topology:<br>• bindings→bindings (existing path, but with corrected lower-bound source — see §4.C)<br>• native→native (NEW; synthesized post-pack)<br>• meta→meta (NEW; bindings-style auto-emit from ProjectReference) |

### B. New behaviors to add

Final IDs assigned during Phase 6 `release-guardrails.md` revision. Names below are behavior-first per AGENTS.md.

| Behavior | Surface |
| --- | --- |
| **Meta PackageId is canonical suffix-less form** | `CsprojPackContractValidator.ValidateMetaCsproj` |
| **Bindings csproj has zero same-family Native ProjectReferences** | `CsprojPackContractValidator.ValidateBindingsCsproj` |
| **Meta csproj has exactly one Bindings + one Native ProjectReference** | `CsprojPackContractValidator.ValidateMetaCsproj` |
| **Role meta nuspec exact-pins same-family Bindings + Native (`[$(Version)]`)** | `PackageOutputValidator` (post-pack, after within-family rewrite pass) |
| **Bindings nuspec carries zero same-family Native deps** | `PackageOutputValidator` (post-pack) |
| **Satellite Native nuspec carries bounded core Native dep** (synthesize path) | `PackageOutputValidator` (post-pack) |
| **Meta nuspec is assetless (only `_._` under `lib/<tfm>/`)** | `PackageOutputValidator` (post-pack) |
| **Cross-family lower bound matches dep family's resolved version** (today's bug fix; see §4.C) | `PackageOutputValidator` (post-pack range parse) — applies to all 3 layers |

### C. Untouched behaviors

These keep operating on the Native package (or PreFlight invariants) and are NOT touched by the refactor beyond ID-text updates:

| Behavior (`release-guardrails.md` locator) | Notes |
| --- | --- |
| **Pack of `.Native` fails if `$(NativePayloadSource)` unset** (G46, §2.2) | MSBuild-side gate, not nuspec content |
| **Native ships consumer-side buildTransitive contract** (G47, §2.5) | Per-package wrapper + shared common.targets |
| **Per-RID payload shape** (G48, §2.5) | Windows DLL set + Unix `$(PackageId).tar.gz` |
| **Native license payload + pre-pack gate** (G51, G52, §2.5) | Compliance surface |
| **ConsolidateHarvest staged-replace invariant** (G53, §2.5) | Pre-pack stage; outside scope |
| **`janset-native-metadata.json` schema + commit-SHA match** (G55, §2.5) | Native metadata file |
| **README mapping table current** (G57, §2.5) | Table shape extends (§8.A); validator-vs-emitter contract preserved |
| **Cross-family dep resolvability** (G58, §2.5) | Extends to per-layer resolvability (3-way cross-product); operational gap #2 in `phase-2-adaptation-plan.md` already tracks the feed-probe relaxation |
| **PreFlight cross-section invariants** (G14, G15, G16, G49, G54, G59, §2.4) | Untouched by topology shape; manifest schema 2.2 bump may surface a tweak to G54's anchor lookup |

### D. Retired behaviors not resurrected

`CsprojPackContractValidator.cs:18-19` already retired G1-G5, G8 when SkiaSharp-style minimum-range replaced exact-pin in csprojs. This refactor **does not resurrect** any retired ID — within-family exact-pin reintroduction lives in post-pack normalize (in meta nuspec only), not in csproj `Version="..."`.

---

## 6. Smoke Csproj Surface

### A. Default path (meta) — unchanged

`build/msbuild/Janset.Smoke.targets` lines 24-41 expand `JansetSmokeSdl2Families` into `<PackageReference Include="Janset.SDL2.<Role>" VersionOverride="[$(JansetSdl<M><Role>PackageVersion)]" />`.

Under the new topology, `Janset.SDL2.<Role>` resolves to the role meta package, which transitively pulls Bindings + Native. **No change required for the default smoke path.**

`tests/smoke-tests/package-smoke/PackageConsumer.Smoke/PackageConsumer.Smoke.csproj` keeps the family declaration:

```xml
<JansetSmokeSdl2Families>Core;Image;Mixer;Ttf;Gfx</JansetSmokeSdl2Families>
```

### B. Bindings-only override path — Gap #3 co-design

Per strategy-brief.md §Plan Shape Phase 5 step 3, this is co-designed with Phase 2b Gap #3 (partial-scope smoke). Three candidate directions, defer choice until Gap #3 design slice:

| Candidate | Shape | Pro | Con |
| --- | --- | --- | --- |
| **Per-family per-layer csproj** | `PackageConsumer.Smoke.Image.Bindings.csproj` + `.Meta.csproj` etc. | Cleanest scope isolation | N × 2 file explosion (10 csprojs for SDL2) |
| **Parameterized via family-list properties** | Single `BindingsOnly.Smoke.csproj` declaring `<JansetSmokeSdl2BindingsFamilies>Image;Mixer</JansetSmokeSdl2BindingsFamilies>` (new property), expanded by Janset.Smoke.targets to `.Bindings`-suffixed PackageReference | One additional csproj per generation, low file count | New family-list property + matching guards (JNSMK010+) |
| **Mode flag on existing csproj** | `PackageConsumer.Smoke.csproj` sets `<JansetSmokeMode>BindingsOnly</JansetSmokeMode>`; targets swap meta refs for bindings refs | Zero new csproj | Single csproj cannot run both modes in same CI invocation |

Recommended direction: **parameterized via family-list properties** — adds one symmetric property `<JansetSmokeSdl<M>BindingsFamilies>` next to `<JansetSmokeSdl<M>Families>`, expanded by `Janset.Smoke.targets` into `Janset.SDL<M>.<Role>.Bindings` PackageReferences. Two csprojs cover both modes per generation. But strategy-brief.md "do not pre-commit" caveat applies — Gap #3 design slice owns the final choice.

### C. `Janset.Smoke.props` / `Janset.Local.props` — minimal touch

Per-family `JansetSdl<M><Role>PackageVersion` properties (lines 55-69 of Janset.Smoke.props) **unchanged**. V1 family-lock means one version covers all 3 packages of a family.

Optional future addition: per-family bindings-only override flag, e.g. `<JansetSdl2ImageBindingsOnly>true</JansetSdl2ImageBindingsOnly>` for Local.props-driven IDE workflow. Defer to Phase 5 bindings-only spike.

Guards (`Janset.Smoke.targets` lines 90-163) — JNSMK002-007 + JNSMK101-106 unchanged (still validate per-family version property). If parameterized BindingsFamilies lands, add JNSMK010-015 for the new property's family entries.

---

## 7. buildTransitive Chain — Unchanged

`src/native/_shared/Janset.SDL2.Native.Common.targets` ships into every Native package's `buildTransitive/`. Per-family wrapper at `src/native/SDL2.<Role>.Native/buildTransitive/Janset.SDL2.<Role>.Native.targets` contributes one entry to `@(_JansetSdlNativePayload)` and imports common.targets.

**Three consumer paths under new topology:**

```text
1. Consumer references Janset.SDL2.Image (role meta)
   → transitively pulls Janset.SDL2.Image.Native (per role meta exact-pin)
   → Native's buildTransitive imports → common.targets executes → payload extracted

2. Consumer references Janset.SDL2.Image.Bindings (override)
   → NO transitive Native (Bindings has zero same-family Native dep)
   → common.targets NOT imported → no payload extraction
   → Consumer must supply SDL2_image.dll externally (system, PATH, etc.)

3. Consumer references Janset.SDL2.Image.Native (native-only)
   → Native's buildTransitive imports → common.targets executes → payload extracted
   → No bindings — consumer uses Native for non-managed P/Invoke or FFI scenarios
```

All three paths work correctly **without any change** to `common.targets` or per-family wrappers. The chain is layer-aware by construction.

---

## 8. Documentation Surface

### A. README.md — mapping table

`README.md` mapping table block validated by G57. Today the table has columns like `Family | Janset.SDL2.<Role> | Janset.SDL2.<Role>.Native | Upstream | Port`. After refactor: add a row per package (3 rows per family) OR add a column for role meta.

Recommended shape (verify with G57 validator surface):

```markdown
| Family | Role Meta | Bindings | Native | Upstream | vcpkg Port |
| --- | --- | --- | --- | --- | --- |
| sdl2-core | Janset.SDL2.Core | Janset.SDL2.Core.Bindings | Janset.SDL2.Core.Native | 2.32.10 | 0 |
| sdl2-image | Janset.SDL2.Image | Janset.SDL2.Image.Bindings | Janset.SDL2.Image.Native | 2.8.8 | 2 |
…
```

Touch: `ReadmeMappingTableBlock.cs` model + `ReadmeMappingTableGenerator.cs` emitter + `ReadmeMappingTableValidator.cs` matcher (all under `build/_build/Targets/Package/` and `build/_build/Validation/Packaging/`).

### B. ADR-001 — supersede

`docs/decisions/2026-05-05-d3seg-and-package-first.md` §1 "Versioning — D-3seg" carries the family-lock + within-family `>=` rule. After refactor:
- D-3seg unchanged (V1 family-lock preserved)
- "Within-family" minimum-range → exact-pin (`[$(Version)]`) inside the **role meta** dep declarations
- Bindings + Native packages now stand alone with no within-family deps
- "Package family" glossary entry extends from 2 packages to 3

Phase 6 step 1 produces ADR-001 successor: `docs/decisions/<YYYY-MM-DD>-role-metapackage-topology.md`. Status of ADR-001: Superseded.

### C. AGENTS.md — Settled Strategic Decisions table

Two rows touched:
- "Separate `.Native` packages | Per-family split (SkiaSharp / LibGit2Sharp pattern)" → reword to "Per-family role-metapackage split (Bindings + Native + Meta)"
- "D-3seg versioning" — clarify that family-lock now spans 3 packages

Plus a new "Package layers" row noting Bindings / Native / Meta separation.

### D. docs/onboarding.md — glossary + topology diagram

Current topology diagram (lines 39-53) shows 2-package families. Replace with 3-package shape per strategy-brief.md.

Glossary additions:
- **Role Metapackage**: assetless package per family carrying exact-pinned dependencies to Bindings + Native. User-facing default install target.
- **Bindings Package**: managed P/Invoke + types only; no native dep. For BYO-native scenarios.

### E. docs/plan.md — Phase 2b checklist update

Add PD entries reflecting refactor scope; mark the refactor as PD-7 hard prerequisite per strategy-brief.md.

### F. docs/playbook/adding-new-library.md

Today's playbook assumes 2-package families. After refactor: add a "Step N — Create role meta csproj" + manifest schema 2.2 fields.

### G. docs/phases/phase-5-sdl3-support.md

SDL3 must start on the new topology. Update brief to reference role meta + bindings + native triplet.

### H. Retire / promote package-topology working docs

After Phase 6 completes, `strategy-brief.md` + `impact-map.md` content fully promoted to ADRs, knowledge-base, playbook. Per the refactoring-doc lifecycle convention this repo follows (durable architecture rules live in ADRs and `docs/knowledge-base/`; per-slice working drafts retire after promotion), both working drafts are removed in the final commit of Phase 6.

---

## 9. Test Surface

### A. Convention unit tests — `build/_build.Tests/`

Test style mirrors existing TUnit conventions in `build/_build.Tests/Scenarios/Package/PackageTaskScenarioTests.cs` and siblings — `[Test]` + `public async Task <Name>()` + `await Assert.That(...).IsXxx()`. Use `[Arguments(...)]` for table-driven cases.

```csharp
[Test]
[Arguments("sdl2-core",  "Janset.SDL2.Core")]
[Arguments("sdl2-image", "Janset.SDL2.Image")]
[Arguments("sdl3-mixer", "Janset.SDL3.Mixer")]
public async Task RoleMetaPackageId_Should_Return_Suffixless_Form_When_Family_Identifier_Is_Valid(
    string familyIdentifier, string expected)
{
    var actual = FamilyIdentifierConventions.RoleMetaPackageId(familyIdentifier);
    await Assert.That(actual).IsEqualTo(expected);
}

[Test]
[Arguments("sdl2-image", "Janset.SDL2.Image.Bindings")]
[Arguments("sdl2-net",   "Janset.SDL2.Net.Bindings")]
public async Task BindingsPackageId_Should_Append_Bindings_Suffix(string familyIdentifier, string expected)
{
    var actual = FamilyIdentifierConventions.BindingsPackageId(familyIdentifier);
    await Assert.That(actual).IsEqualTo(expected);
}
```

Plus `Parse` regressions and case-handling tests stay green.

### B. Manifest model tests

- `PackageFamilyConfig_Should_Deserialize_Bindings_Native_Meta_Fields_When_Schema_2_2`
- `PackageFamilyConfig_Should_Deserialize_Managed_Native_Fields_When_Schema_2_1`
- `ManifestRepository_Should_Accept_Schema_2_1_And_2_2_During_Dual_Mode_Window`
- `ManifestRepository_Should_Reject_Missing_meta_project_When_Schema_Is_2_2_And_Family_Is_Concrete`
- `ManifestRepository_Should_Reject_Schema_2_1_After_DualMode_Sunset`   # Phase 6 candidate; track separately

### C. CsprojPackContractValidator tests

Behavior-named test methods (TUnit `[Test]` + `await Assert.That(...).Is...()`):

```text
ValidateBindingsCsproj_Should_Fail_When_PackageId_Lacks_Bindings_Suffix
ValidateBindingsCsproj_Should_Fail_When_ProjectReference_Targets_Same_Family_Native
ValidateMetaCsproj_Should_Fail_When_PackageId_Has_Bindings_Or_Native_Suffix
ValidateMetaCsproj_Should_Fail_When_SameFamily_Bindings_ProjectReference_Missing
ValidateMetaCsproj_Should_Fail_When_SameFamily_Native_ProjectReference_Missing
```

### D. PackageOutputValidator tests

```text
ValidateAsync_Should_Pass_When_Meta_Nuspec_Exact_Pins_Bindings_And_Native
ValidateAsync_Should_Fail_When_Meta_Nuspec_Has_Bounded_Range_To_Same_Family_Bindings
ValidateAsync_Should_Fail_When_Bindings_Nuspec_Declares_Same_Family_Native_Dep
ValidateAsync_Should_Pass_When_Satellite_Native_Nuspec_Has_Bounded_Range_To_Core_Native
ValidateAsync_Should_Fail_When_Meta_Nuspec_Has_Lib_Folder_Beyond_Underscore_Placeholder
ValidateAsync_Should_Use_DepFamily_Version_As_Lower_Bound_For_CrossFamily_Range
```

### E. DependencyRangeNormalizer tests

```text
NormalizeAsync_Should_Rewrite_CrossFamily_Deps_To_Bounded_Range_With_Dep_Family_Lower_Bound_When_Mode_Is_CrossFamilyRewrite
NormalizeAsync_Should_Exact_Pin_WithinFamily_Bindings_And_Native_When_Mode_Is_CrossFamilyAndWithinFamilyRewrite
NormalizeAsync_Should_Run_WithinFamily_Pass_Even_When_DependsOn_Is_Empty_For_Core_Meta
NormalizeAsync_Should_Synthesize_Core_Native_Dep_Element_When_Mode_Is_CrossFamilySynthesize_And_Group_Missing
NormalizeAsync_Should_Use_Generic_Group_Without_TargetFramework_For_Synthesized_Native_Dep
NormalizeAsync_Should_Throw_When_VersionSet_Lacks_Dep_Family
```

### F. PackageFamilyPacker scenario tests

```text
PackAsync_Should_Produce_Three_Nupkgs_Plus_Bindings_Snupkg_When_Family_Is_Concrete
PackAsync_Should_Pass_NativePayloadSource_To_Native_Pack_Only
PackAsync_Should_Dispatch_CrossFamilyRewrite_For_Bindings_Artifact
PackAsync_Should_Dispatch_CrossFamilySynthesize_For_Native_Artifact
PackAsync_Should_Dispatch_CrossFamilyAndWithinFamily_For_Meta_Artifact
```

### G. Consumer smoke tests

Today's `PackageConsumer.Smoke.csproj` keeps working as the meta-path smoke (covers default UX). Bindings-only smoke design is Gap #3 co-design slice; tests there.

### H. Characterization tests (Phase 1)

Per strategy-brief.md Phase 1: capture the CURRENT package graph behavior as a baseline AND describe the target graph. Phase 1 must keep CI green — failing tests on master are not a Phase 1 deliverable. Two test classes solve this:

1. **Positive characterization (current behavior, green now, breaks one-for-one at the implementing slice).** Captures today's reality including bugs that the refactor fixes. The test assertion flips inside the implementing slice — same test name, updated expectation. This is intentional: the diff is the audit trail.
2. **Target characterization (target behavior, skipped now, active after).** Use the repo's TUnit skip pattern — derive from `SkipAttribute` (see `build/_build.Tests/Fixtures/WindowsOnlyAttribute.cs` for the project's existing pattern). Inactive until the implementing slice removes the attribute or its predicate flips.

Suggested split:

```text
# Positive — active [Test], assertions reflect TODAY's behavior. Implementing slice updates assertion.
CurrentGraph_Should_Use_Family_Locked_Version_Across_Emitted_Nupkgs
CurrentGraph_Should_Preserve_Native_Payload_Layout_Under_Runtimes_Rid_Native
CurrentGraph_Should_Preserve_Native_Metadata_Json
CurrentGraph_Should_Emit_Cross_Family_Range_With_Current_Family_Lower_Bound   # asserts the today-bug; flipped by Phase 4 S9

# Target — inactive until the implementing slice removes the skip attribute
TargetGraph_Should_Emit_Three_Nupkgs_Per_Family                                # activated by Phase 4 S8
TargetGraph_Should_Have_Bindings_Suffix_On_Managed_PackageId                   # activated by Phase 3 S6
TargetGraph_Should_Have_No_SameFamily_Native_Dep_On_Bindings_Nupkg             # activated by Phase 4 S9
TargetGraph_Should_Have_ExactPin_On_Meta_To_Bindings_And_Native                # activated by Phase 4 S9
TargetGraph_Should_Emit_CrossFamily_Range_With_Dep_Family_Lower_Bound          # activated by Phase 4 S9 (replaces the flipped positive)
TargetGraph_Should_Synthesize_Core_Native_Dep_On_Satellite_Native_Nuspec       # activated by Phase 4 S9
```

Skip mechanism follows the repo's existing `SkipAttribute`-derived pattern (`build/_build.Tests/Fixtures/NonWindowsOnlyAttribute.cs`, `WindowsOnlyAttribute.cs`). Phase 1 introduces a new `[TopologyRefactorTarget]` skip attribute (or equivalent) whose predicate evaluates a feature flag / build-property the implementing slice flips. Implementation detail decided in S1; do not hand-write `[Test(Skip="...")]` syntax — verify against the installed TUnit version (`Directory.Packages.props:33` pins 1.33.0).

Activation pattern: each implementing slice either removes the skip attribute from its corresponding target test OR flips the global feature flag the attribute reads. At Phase 4 close, the full target suite runs green; the positive characterization with the flipped assertion captures the new reality.

---

## 10. Phase 1-6 Sequencing Map

Revised from the strategy-brief.md skeleton to fix the Phase 3↔PreFlight collision called out in review: model + validator dual-mode awareness lands **before** csproj structural changes, so PreFlight stays green throughout. Phase 2 splits into 2a (conventions + helpers) and 2b (manifest schema + validator dual-mode); Phase 3 then changes csprojs against a validator that already understands both shapes.

### Phase 1 — Characterization tests

- Add §9.H positive characterization tests (active, green now AND after).
- Add §9.H target characterization tests with TUnit skip predicate keyed to the implementing slice, so CI stays green.
- No production code touched.
- Slice deliverable: positive suite green; target suite registered but skipped; CI clean.

### Phase 2a — Conventions + helpers

- §4.D `FamilyIdentifierConventions` rename `ManagedPackageId → RoleMetaPackageId` + add `BindingsPackageId`. Pure rename + add; semantics of `RoleMetaPackageId` produce the same `Janset.SDL<M>.<Role>` string as today's `ManagedPackageId`, so call sites get a name change with no behavioral change. Drive the rename through Rider's refactoring so all call sites update atomically in one commit (any cross-file manual edit-and-replace approach risks missing a usage in test fixtures or string interpolations).
- §9.A convention tests for the new helpers turn green.
- Slice deliverable: build green, every call site reads `RoleMetaPackageId` / `BindingsPackageId` from a clearly-named API; csprojs and validators still behave identically.

### Phase 2b — Manifest schema + validator dual-mode

- §2 schema bump in manifest model: `ManifestConfigModels.cs` gets `BindingsProject` (rename of `ManagedProject`) + `MetaProject` (new). `ManifestRepository` accepts both `schema_version = "2.1"` (legacy) AND `"2.2"` (new) until Phase 3 commits the bump.
- `CsprojPackContractValidator` becomes dual-mode: detects whether a family has a `MetaProject` declared; if no → today's 2-package validation; if yes → tomorrow's 3-package validation (with PackageId split + ProjectReference flip).
- `PackageOutputValidator` extends similarly: detects 3-artifact `PackageArtifacts` vs 2-artifact and dispatches.
- `SmokeScopeComparator` audited for dual-mode safety (no code change expected — comparator parses csproj XML literally; whatever the manifest declares as user-facing per-family ID flows through `RoleMetaPackageId`).
- `manifest.json` stays at `schema_version "2.1"` for this slice — only the parser learns to recognize 2.2. Migration commits in Phase 3.
- Slice deliverable: dual-mode model + validators land; no `meta_project` set yet anywhere; PreFlight + Pack still produce 2 nupkgs per family; ZERO behavioral change.

### Phase 3 — Project topology

- §2 commit `schema_version → "2.2"` in `build/manifest.json` + populate `bindings_project` + `meta_project` for all 5 families. (`managed_project` either removed in this slice OR parser ignores it for 2.2 — see Phase 2b dual-mode.)
- §3.A rename `<PackageId>` on existing managed csprojs to `<Role>.Bindings`. PreFlight already understands this (Phase 2b dual-mode), so green.
- §3.B remove same-family Native `<ProjectReference>` from bindings csprojs.
- §3.D add 5 role meta csprojs at `src/meta/SDL2.<Role>/` + `_._` placeholders.
- §3.E new `src/meta/Directory.Build.props` if helpful.
- §3.C is **NOT** a Phase 3 csproj change anymore — satellite Native cross-family dep moves to post-pack synthesize (§3.C C2 path), so no Native csproj is touched here.
- Target characterization tests for PackageId suffix + meta presence get unskipped.
- §4.5 (NEW section) `tools.cs` PackageId helpers + `ManifestConfig` model surface — touched in this slice so `tools setup` keeps working against the migrated manifest + csprojs. Without it, `tools setup --source=local` fails at `GetConcreteFamilies` (looks for `managed_project`, finds `bindings_project`).
- Slice deliverable: 11 csproj files in their new shape (5 bindings PackageId rename + 5 new meta csprojs + 1 Directory.Build.props); `tools.cs` updated; builds green; pack still uses 2-package flow at this stage; PreFlight green via dual-mode validator.

### Phase 4 — Packaging and guardrails

- §4.A `PackageArtifacts` model expansion.
- §4.B `PackageFamilyPacker.PackAsync` 3-pack rewrite + mode-dispatched normalizer calls.
- §4.C `DependencyRangeNormalizer` extension: mode parameter + `PackageFamilyVersionSet` plumb + within-family pass + satellite-Native synthesize pass + cross-family lower-bound bug fix.
- §4.F `ReadmeMappingTableGenerator` column extension.
- §4.5 `tools.cs` `ExpectedNupkgPaths` extended to 3 paths per family; `RunRemoteGitHubAsync` downloads 3 packages per family with three-way mismatch detection.
- §4.6.C `PublishStagingTask` 3-push rewrite. Must co-land with the `PackageFamilyPacker` rewrite or in the immediately adjacent slice — staging feed must never see a wave where Pack produced 3 but Publish pushed 2.
- §5.A reframed behaviors + §5.B new behaviors land in validators + post-pack checks.
- §9.C/D/E/F validator + normalizer + packer tests turn green.
- All remaining target characterization tests get unskipped.
- Slice deliverable: pack produces 3 nupkgs + bindings snupkg per family; staging publish pushes all 3 per family; all guardrails pass; local feed (`artifacts/packages/`) has new shape; target characterization suite green; `tools setup --source=local` + `ci-sim` round-trip green.

### Phase 5 — Consumer validation

- §6.A meta-path smoke unchanged (regression check — must still pass).
- §6.B bindings-only smoke design via Phase 2b Gap #3 co-design slice.
- §6.C smoke props/targets touched only if bindings-only smoke lands.
- Real-consumer validation: confirm `learning-sdl2` (Deniz's external project consuming this repo's GitHub Packages feed) still works after the first staging publish wave under the new topology. See §15.
- Slice deliverable: package-first contract preserved; local feed pack survives `tools setup` + `ci-sim`; `learning-sdl2` smoke check passes against staging GH Packages.

### Phase 6 — Canonical documentation

- §8.B ADR-001 successor (new ADR).
- §8.A README mapping table.
- §8.C AGENTS.md "Settled Strategic Decisions" update.
- §8.D onboarding.md topology + glossary.
- §8.E plan.md Phase 2b checklist + PD-7 prerequisite.
- §8.F playbook adding-new-library.md.
- §8.G phase-5-sdl3-support.md.
- §5 guardrail catalog update in `knowledge-base/release-guardrails.md` — finalize ID assignments for all reframed + new behaviors; retire obsolete ones.
- §8.H retire strategy-brief.md + impact-map.md after promotion (per the project's refactoring-doc lifecycle: working drafts disappear once durable rules land in ADRs + knowledge-base).
- Slice deliverable: every canonical doc consistent with shipped code; working drafts retired.

---

## 11. Open Decisions Carried Forward

| # | Decision | Owner phase | Default direction |
| --- | --- | --- | --- |
| O1 | Folder names — keep `src/SDL2.<Role>/` despite PackageId carrying `.Bindings`? | Phase 3 | Keep (per strategy-brief.md Open Decisions). Revisit only if friction surfaces. |
| O2 | Bindings-only smoke shape (per-family vs parameterized vs mode flag) | Phase 5 / Gap #3 co-design | Lean parameterized via family-list properties; Gap #3 design slice owns final choice. |
| O3 | README mapping table column shape | Phase 4 | Lean "one row per family, 3 PackageId columns". |
| O4 | New guardrail ID assignments (behaviors per §5.B + reframes per §5.A) | Phase 6 / knowledge-base | Finalize numbering against `release-guardrails.md` convention; behavior-first names used until then per AGENTS.md §Build-Host Reference Pattern. |
| O5 | `src/meta/Directory.Build.props` content | Phase 3 | Mirror `src/native/Directory.Build.props` if present. |
| O6 | Ultimate `Janset.SDL2` metapackage | Deferred past first public release per strategy-brief.md | No implementation in this refactor. |

---

## 12. Risks and Mitigations

| Risk | Mitigation |
| --- | --- |
| Within-family `[=x]` exact-pin clashes with consumer's prerelease workflow (e.g., consumer wants `2.8.1-rc.1` of Bindings against `2.8.0` Native) | Family-lock V1 prevents this by design — strategy-brief.md §Versioning stance commits to single version per family. Document loudly in ADR successor. |
| Empty `_._` placeholder + NU5128 suppress silently emits broken meta nupkg | §9.D new test `ValidateAsync_Should_Fail_When_Meta_Nuspec_Has_Lib_Folder_Beyond_Underscore_Placeholder`. PreFlight + post-pack guardrails cover. |
| Schema 2.2 migration breaks an in-flight branch | Phase 2b dual-mode parser keeps schema 2.1 alive until Phase 3 atomically commits 2.2 + csproj changes. Actionable error message on later 2.1-only manifests. |
| Method rename `ManagedPackageId` → `RoleMetaPackageId` creates large diff that hides logic bugs | Phase 2a is a dedicated Rider-driven mass-rename slice — a single commit whose diff is purely identifier substitution. No behavioral edits land in the same commit; logic changes follow in Phase 2b+. The single-purpose diff lets reviewers verify "no logic change" by reading the file mode alone. |
| Phase 3 produces csprojs that compile but can't pack (NU5128 misconfigured, ProjectReference unresolved) | Phase 3 slice deliverable explicitly says "builds green" — packing comes in Phase 4. Build-only validates structure before adding the pack contract. |
| CPM behavior under role meta `ProjectReference` does not match the §1 candidate | §1 spike acceptance criteria are explicit. If Option A fails any of the four checks, fall back to Option B (`<PackageReference VersionOverride="[$(Version)]" />`) — already proven on the consumer side at `Janset.Smoke.targets:25`. The fallback path is the actual fallback, not an emergency exit. |
| Today's cross-family lower-bound bug (Image 2.8 → Core `[2.8.0, …)` instead of `[2.32.0, …)`) is on the critical path of this refactor | Treated as **must-fix during Phase 4**, not as out-of-scope cleanup. §4.C plumbs `PackageFamilyVersionSet` through the normalizer; Phase 1 positive characterization `CurrentGraph_Should_Emit_Cross_Family_Range_With_Current_Family_Lower_Bound` captures today's wrong behavior; Phase 4 S9 flips its assertion + activates `TargetGraph_Should_Emit_CrossFamily_Range_With_Dep_Family_Lower_Bound`. |
| `tools.cs` duplicate of `FamilyIdentifierConventions` drifts from build-host version mid-refactor | Phase 2a slice updates BOTH files in one commit. Phase 3 updates `tools.cs:633-663` model alongside `manifest.json` schema bump. Phase 4 updates `tools.cs` consumption flows alongside `PackageFamilyPacker`. Per-phase atomicity prevents drift. |
| Satellite Native synthesize path produces a malformed nuspec the existing NuGet client refuses to restore | §1 spike + Phase 4 unit tests on the synthesized XML structure (with TFM-less generic group, see §4.C). Verify against `nuget restore` round-trip in the spike output before locking. |
| Folder name `src/meta/` collides with build artifact directory or git ignore | Verify no `.gitignore` rule shadows `src/meta/`; verify no MSBuild target output path collision. Phase 0 spike output. |

---

## 13. Estimated Slice Sequence

Single-developer cadence estimate. Each slice ends green-build + green-test + AGENTS.md commit-approval gate. Revised to match the §10 phase split (2a/2b before topology changes) and to include `tools.cs` surface work.

| Slice | Phase | Scope | Effort |
| --- | --- | --- | --- |
| S1 | 1 | Characterization tests — positive (active) + target (skipped) | 2-4h |
| S2 | 2a | FamilyIdentifierConventions rename `ManagedPackageId → RoleMetaPackageId` + add `BindingsPackageId` (Rider mass-rename); mirror in `tools.cs:667-714` | 1-2h |
| S3 | 2a | CPM-aware role-meta csproj spike — produce throwaway pack output, validate acceptance criteria from §1; record findings in this impact map | 2-3h |
| S4 | 2b | ManifestConfigModels + ManifestRepository dual-mode (read 2.1 OR 2.2); CsprojPackContractValidator + PackageOutputValidator dispatch by `MetaProject` presence | 3-5h |
| S5 | 3 | `manifest.json` schema 2.2 bump + per-family `bindings_project` + `meta_project` population; `tools.cs:633-663` model update | 2-3h |
| S6 | 3 | 5 bindings csproj rename (PackageId `.Bindings` suffix) + remove same-fam Native ProjectRef | 2-3h |
| S7 | 3 | 5 role meta csprojs + 5 `_._` placeholders + `src/meta/Directory.Build.props` | 2-3h |
| S8 | 4 | PackageArtifacts + PackageFamilyPacker 3-pack rewrite + PackageFamilyVersionSet plumb | 3-4h |
| S9 | 4 | DependencyRangeNormalizer 3-mode rewrite (CrossFamilyRewrite + CrossFamilyAndWithinFamilyRewrite + CrossFamilySynthesize) + cross-family lower-bound bug fix | 5-7h |
| S10 | 4 | Reframed PackageId/ProjectReference/version-equality validators + new behaviors per §5 (final ID assignment deferred to Phase 6) | 6-8h |
| S11 | 4 | ReadmeMappingTableGenerator column extension + G57 validator update | 2-3h |
| S12 | 4 | `tools.cs` consumption flow — `ExpectedNupkgPaths` to 3 paths + `RunRemoteGitHubAsync` 3-package download + three-way mismatch detection | 2-3h |
| S13 | 4 | `PublishStagingTask` 3-push rewrite + per-family error-message update (must co-land with S8 to keep staging feed coherent) | 2-3h |
| S14 | 5 | `tools setup` + `ci-sim` end-to-end validation against host RID; meta-path smoke regression; verify `SmokeScopeComparator` accepts new manifest 2.2 shape | 2-4h |
| S15 | 5 | Bindings-only smoke design (Gap #3 co-design slice; may extend `SmokeScopeComparator` to accept multiple IDs per family) | 4-6h (depends on Gap #3 scope) |
| S16 | 5 | `learning-sdl2` real-consumer validation against first staging publish wave under new topology (see §15) | 2-4h (depends on staging publish cadence) |
| S17 | 6 | ADR-001 successor + AGENTS.md + onboarding + plan + playbook + release-guardrails + phase-5-sdl3-support | 4-6h |
| S18 | 6 | Retire strategy-brief.md + impact-map.md; final commit | 1h |

Estimated total: **46-68 hours** of focused work. Calendar duration depends on slice cadence and external blockers (Gap #3 co-design may pause S15 until rehearsal data lands; S16 depends on a staging publish wave existing under the new topology).

---

## 14. Cross-References

- [`strategy-brief.md`](strategy-brief.md) — strategic decisions; this file is mechanical companion
- [`../../decisions/2026-05-05-d3seg-and-package-first.md`](../../decisions/2026-05-05-d3seg-and-package-first.md) — ADR-001 to supersede
- [`../../knowledge-base/release-guardrails.md`](../../knowledge-base/release-guardrails.md) — G-numbered guardrail catalog
- [`../../knowledge-base/extraction-guidelines.md`](../../knowledge-base/extraction-guidelines.md) — collaborator extraction policy (applies to normalizer + validator surface changes)
- [`../../knowledge-base/testing-guidelines.md`](../../knowledge-base/testing-guidelines.md) — characterization test policy, canonical infra
- [`../../phases/phase-2-adaptation-plan.md`](../../phases/phase-2-adaptation-plan.md) — Gap #3 partial-scope smoke (co-design dependency)
- [`../../../AGENTS.md`](../../../AGENTS.md) — Settled Strategic Decisions + Build-Host Reference Pattern
- [`../../../build/manifest.json`](../../../build/manifest.json) — current schema 2.1 source
- [`../../../tools.cs`](../../../tools.cs) — file-based dev-orchestration app (duplicate family conventions + manifest model + package consumption flows; see §4.5)
- `https://github.com/Blind-Striker/learning-sdl2` — Deniz's real-world consumer of this repo's GitHub Packages feed (see §15)

---

## 15. Real-World Consumer Validation — `learning-sdl2`

Deniz is actively developing `learning-sdl2` as an external consumer of `janset2d/sdl2-cs-bindings` via the GitHub Packages internal feed. It exercises actual SDL surfaces (real applications doing real SDL2 work). This makes it the most credible end-to-end validator the project has — synthetic smoke tests in this repo cannot match what a developer-driven consumer turns up.

### Validation expectations under the new topology

The default-path UX **is preserved by construction**: `learning-sdl2`'s csproj references `Janset.SDL2.<Role>` PackageIds. After refactor those IDs resolve to role metapackages instead of bindings-with-native packages, but the transitive dep graph (meta → bindings + native) reaches the same runtime state. Net runtime behavior: identical.

Risks that warrant explicit Phase 5 verification:

| Risk | Verification |
| --- | --- |
| `learning-sdl2` csproj has `<IncludeAssets>` or `<PrivateAssets>` overrides on the `Janset.SDL2.*` references that interact differently with a meta indirection (e.g., a meta package's transitive native assets get filtered out unexpectedly) | Inspect `learning-sdl2`'s csproj after Phase 4; run the existing real apps; confirm P/Invoke surfaces resolve. |
| First post-refactor staging publish wave on GH Packages introduces version-resolution surprises (e.g., NuGet picks the older 2-package shape over the newer 3-package shape due to feed ordering) | Confirm publish wave shows all 3 packages per family at the same version. Re-run `learning-sdl2`'s restore. |
| Native payload extraction path under `buildTransitive/Janset.SDL2.Native.Common.targets` continues to behave correctly when reached via meta indirection rather than direct managed→native ProjectReference | Verify `tar -xzf` extraction logs on Linux/macOS during `learning-sdl2`'s build; check `bin/<tfm>/runtimes/<rid>/native/` for the expected `libSDL2*` set. |

### MCP-mediated investigation

Deniz offered: *"https://github.com/Blind-Striker/learning-sdl2 projesini şuan hali hazırda bizim reponun github packages'ını kullanarak geliştiriyorum ve orada gerçek SDL testleri yapabileceğim uygulamalarda var, MCP server kullanıp inceleyebilirsin de."*

For Phase 5 step S15, the implementing agent can be invited (with the GitHub MCP server available in this session) to:

- Read `learning-sdl2`'s csproj(s) under the new topology and confirm the `<PackageReference Include="Janset.SDL2.*">` rows resolve to the role meta packages.
- Inspect any post-restore lockfile or `project.assets.json` to confirm the transitive graph reaches both `.Bindings` + `.Native`.
- Optionally open an issue or PR against `learning-sdl2` if a migration note is needed.

Out of scope: changing `learning-sdl2` proactively. Wait for a real failure or migration friction to surface — V1 family-lock + default-UX preservation should make consumer migration a no-op.

### Phase 5 validation flow

1. Phase 4 closes with `tools ci-sim` green producing 3 packages per family at one shared version.
2. A staging publish wave fires that pushes the new 3-package shape to `nuget.pkg.github.com/janset2d`.
3. `learning-sdl2` does a clean `dotnet restore` against the staging feed.
4. Real applications inside `learning-sdl2` run on the host RID — P/Invoke surfaces work; no `DllNotFoundException`; native logs show expected library load.
5. S15 closes with a one-paragraph note in this section recording the observed behavior + any consumer-side migration needed.

If step 4 fails, that's the canonical signal that the refactor's consumer contract regressed — not a CI fault, a real-consumer fault. Treat as Phase 4 reopen, not Phase 5 progress.
