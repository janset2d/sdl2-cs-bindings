# Phase 1 — Characterization Tests Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use [`superpowers:subagent-driven-development`](https://github.com/anthropics/claude-code-skills) (recommended for fresh-context per task) or [`superpowers:executing-plans`](https://github.com/anthropics/claude-code-skills) (inline batch with checkpoints) to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Lock today's `DependencyRangeNormalizer` behavior into a TUnit characterization test suite + register skipped target-state assertions, so Phase 2a–4 slices have an executable spec to flip.

**Architecture:** Add a `[TopologyRefactorTarget]` skip attribute extending TUnit's `SkipAttribute` (mirroring the existing `WindowsOnlyAttribute` pattern). Add a new characterization test class under `Scenarios/Characterization/` that drives the real `DependencyRangeNormalizer` against `FakeCakeWorld`-seeded nupkg fixtures built via `NuspecBuilder`. Positive tests assert today's behavior (including the cross-family lower-bound bug captured in [`impact-map.md`](impact-map.md) §4.C). Target tests carry `[TopologyRefactorTarget("Phase 4 S9")]` and are skipped; the implementing slice removes the attribute to activate.

**Tech Stack:** TUnit 1.33.0 (`build/_build.Tests/Build.Tests.csproj`), Cake.Testing's `FakeFileSystem`, NSubstitute, NuGet.Versioning 7.3.1, Cake.Frosting 6.1. Existing fixtures under `build/_build.Tests/Fixtures/`.

---

## Required Reading Before Coding

Per the authoring convention in [`phase-planning-methodology.md`](phase-planning-methodology.md) §Authoring Convention: Reference Discipline. Read these before writing any code:

- [`../../../AGENTS.md`](../../../AGENTS.md) §Approval Gate (this Phase produces tests + 1 attribute; documentation-edit exceptions do NOT cover test code — explicit "go" required before `dotnet test` runs implementation code), §Test Naming Convention (`<MethodName>_Should_<Verb>_<optional When/If/Given>`), §Build-Host Reference Pattern.
- [`../../decisions/2026-05-05-target-centric-build-host.md`](../../decisions/2026-05-05-target-centric-build-host.md) (ADR-002) — test files live under `Scenarios/<TargetName>/` or `Unit/Targets/<TargetName>/`. Characterization tests for the package-topology refactor live under `Scenarios/Characterization/` per the new sub-folder; this matches the existing test taxonomy in [`../../knowledge-base/testing-guidelines.md`](../../knowledge-base/testing-guidelines.md) §Test taxonomy.
- [`../../knowledge-base/testing-guidelines.md`](../../knowledge-base/testing-guidelines.md) — canonical test infra (`FakeCakeWorld`, `TargetTestHost`, `FixtureLoader`, `TestLog`), filesystem rule (no `System.IO`, no `AppContext.BaseDirectory` traversal), test data policy (embedded fixtures via `FixtureLoader` vs centralized inline constants), TUnit rules.
- [`../../knowledge-base/extraction-guidelines.md`](../../knowledge-base/extraction-guidelines.md) — if a characterization helper grows beyond test-class-local complexity, extract per the private-method decision tree. For Phase 1 the helpers stay test-class-local (no separate collaborator file).
- [`strategy-brief.md`](strategy-brief.md) §V1 family-lock + §HOW Package layers — strategic anchor for what the target tests describe.
- [`impact-map.md`](impact-map.md) §1 (CPM-aware normalizer modes), §4.C (normalizer extension + lower-bound bug), §9.H (this Phase's test inventory).
- The corrections note in [`phase-planning-methodology.md`](phase-planning-methodology.md) §Outstanding Corrections to impact-map.md: `PackageFamilyVersionSet` is NOT new — already exists at `build/_build/Data/Versions/PackageFamilyVersionSet.cs`. Phase 1 does not instantiate this type; Phase 4 plumbs it through `PackageFamilyPacker`.

Also skim, to align with existing patterns:

- `build/_build/Targets/Package/Services/DependencyRangeNormalizer.cs` — the system under characterization. Today's signature is the baseline.
- `build/_build.Tests/Scenarios/Package/PackageTaskScenarioTests.cs` — closest existing test class to mirror for style and helper shape (especially `NewWorld()`, `SeedHarvestPayload()`, `CreateHost()`).
- `build/_build.Tests/Fixtures/Builders/NuspecBuilder.cs` + `NuspecReader.cs` — nupkg construction + extraction helpers used by characterization tests.
- `build/_build.Tests/Fixtures/WindowsOnlyAttribute.cs` — exemplar for the `SkipAttribute` derived class pattern.

---

## File Structure

| Path | Action | Responsibility |
| --- | --- | --- |
| `build/_build.Tests/Fixtures/TopologyRefactorTargetAttribute.cs` | **Create** | `SkipAttribute`-derived attribute that unconditionally skips a target characterization test with a phase tag in the reason message. Implementing slice removes the attribute to activate. |
| `build/_build.Tests/Scenarios/Characterization/PackageTopologyCharacterizationTests.cs` | **Create** | TUnit characterization tests for `DependencyRangeNormalizer`. Mix of positive (active today, flipped at implementing slice) and target (skipped via `[TopologyRefactorTarget]`). |

No production code changes in Phase 1. No manifest changes. No csproj changes. The implementing agent's commit footprint is two new files plus one or two commits.

---

## Task 1: Add `[TopologyRefactorTarget]` Skip Attribute

**Files:**

- Create: `build/_build.Tests/Fixtures/TopologyRefactorTargetAttribute.cs`

This attribute is the activation mechanism described in [`impact-map.md`](impact-map.md) §9.H. Each implementing slice (Phase 2a S2, Phase 3 S6, Phase 4 S9, etc.) removes this attribute from the corresponding target test to activate the assertion.

- [ ] **Step 1: Create the attribute file**

```csharp
// build/_build.Tests/Fixtures/TopologyRefactorTargetAttribute.cs
namespace Build.Tests.Fixtures;

/// <summary>
/// Marks a TUnit test as a target characterization for the package-topology refactor.
/// The test is unconditionally skipped until the implementing slice (named in the reason
/// message) lands and removes this attribute. Mirrors the SkipAttribute-derived pattern
/// established by <see cref="WindowsOnlyAttribute"/>.
/// </summary>
/// <remarks>
/// Phase 1 of the package-topology refactor registers the target-state assertions as
/// skipped tests so they live in the codebase as an executable spec. Implementing slices
/// remove the attribute (per <c>docs/parking-lot/package-topology/impact-map.md</c> §9.H)
/// rather than relying on global feature flags or env-var-driven predicates.
/// </remarks>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class TopologyRefactorTargetAttribute : SkipAttribute
{
    public TopologyRefactorTargetAttribute(string activatedBy)
        : base($"Topology refactor target — activates when {activatedBy} lands and removes this attribute.")
    {
    }

    public override Task<bool> ShouldSkip(TestRegisteredContext context)
    {
        // Unconditional skip. Activation = attribute removal, not predicate flip.
        // Keeping the predicate stateless avoids hidden test-runtime branches and matches
        // the convention captured in phase-planning-methodology.md §Authoring Convention.
        return Task.FromResult(true);
    }
}
```

- [ ] **Step 2: Verify the file compiles**

Run: `dotnet build build/_build.Tests/Build.Tests.csproj -c Release`
Expected: build succeeds; no warnings about unused class.

- [ ] **Step 3: Commit**

```bash
git add build/_build.Tests/Fixtures/TopologyRefactorTargetAttribute.cs
git commit -m "$(cat <<'EOF'
test(topology): add TopologyRefactorTarget skip attribute

SkipAttribute-derived marker for Phase 1 characterization tests that
describe target-state behavior. Unconditional skip with a phase tag in
the reason; implementing slices remove the attribute to activate per
docs/parking-lot/package-topology/impact-map.md §9.H.

refs #61
EOF
)"
```

---

## Task 2: Add Characterization Test Class Scaffold + Helpers

**Files:**

- Create: `build/_build.Tests/Scenarios/Characterization/PackageTopologyCharacterizationTests.cs`

This file holds all Phase 1 characterization tests. The scaffold establishes helpers used by every test; positive and target tests follow in Tasks 3 and 4. The scaffold compiles green but contains no `[Test]` methods yet.

- [ ] **Step 1: Create the test class scaffold**

```csharp
// build/_build.Tests/Scenarios/Characterization/PackageTopologyCharacterizationTests.cs
using System.IO.Compression;
using Build.Data.Manifest.Models;
using Build.Targets.Package.Services;
using Build.Tests.Fixtures;
using Build.Tests.Fixtures.Builders;
using Cake.Core.IO;

namespace Build.Tests.Scenarios.Characterization;

/// <summary>
/// Phase 1 of the package-topology refactor. Characterizes today's <c>DependencyRangeNormalizer</c>
/// behavior + registers target-state assertions as skipped tests for later activation by
/// Phase 2a-4 slices. Source of truth for "what changes" during this refactor.
/// </summary>
/// <remarks>
/// See <c>docs/parking-lot/package-topology/implementation-plan-phase-1.md</c> for plan context,
/// <c>impact-map.md</c> §9.H for the test inventory, and <c>impact-map.md</c> §4.C for the
/// cross-family lower-bound bug captured here as a positive assertion.
///
/// Test data policy follows <c>docs/knowledge-base/testing-guidelines.md</c> §Test data policy:
/// inline constants for test-class-local data; embedded fixtures (<c>FixtureLoader</c>) when
/// shared across tests. Phase 1's data is small and class-local — inline constants below.
/// </remarks>
public sealed class PackageTopologyCharacterizationTests
{
    private const string SatelliteFamilyName = "sdl2-image";
    private const string CoreFamilyName = "sdl2-core";

    private const string SatelliteFamilyVersion = "2.8.0";
    private const string CoreFamilyVersion = "2.32.0";

    private const string SatelliteManagedPackageIdToday = "Janset.SDL2.Image";
    private const string CoreManagedPackageIdToday = "Janset.SDL2.Core";

    private const string SatelliteBindingsPackageIdTarget = "Janset.SDL2.Image.Bindings";
    private const string CoreNativePackageId = "Janset.SDL2.Core.Native";

    /// <summary>
    /// Builds a Windows-flavored FakeCakeWorld with the project's test manifest seeded.
    /// Used by every test in this class for a consistent baseline.
    /// </summary>
    private static FakeCakeWorld NewWorld()
    {
        return FakeCakeWorld.CreateWindows()
            .WithManifestObject(ManifestFixture.CreateTestManifestConfig());
    }

    /// <summary>
    /// Seeds a single nupkg containing only a nuspec with one cross-family dependency.
    /// Path returned is relative to the fake repo root; ready for normalizer input.
    /// </summary>
    private static FilePath SeedNupkgWithCrossFamilyDep(
        FakeCakeWorld world,
        string packageId,
        string version,
        string dependencyId,
        string currentRange)
    {
        var nuspecBytes = NuspecBuilder.WithCrossFamilyDependency(
            packageId, version, dependencyId, currentRange);
        var nupkgBytes = NuspecBuilder.AsNupkgZip($"{packageId}.nuspec", nuspecBytes);

        var nupkgPath = new FilePath($"artifacts/packages/{packageId}.{version}.nupkg");
        world.WithBinaryFile(nupkgPath, nupkgBytes);
        return nupkgPath;
    }

    /// <summary>
    /// Reads back the resolved <c>version</c> attribute on a <c>dependency</c> element in
    /// a nupkg's nuspec. Returns null if no matching dep exists — distinguishes "rewrite
    /// happened" from "dep was never present."
    /// </summary>
    private static string? GetDependencyVersion(FakeCakeWorld world, FilePath nupkgPath, string dependencyId)
        => NuspecReader.GetDependencyVersion(world, nupkgPath, dependencyId);

    /// <summary>
    /// Returns the family config from the test manifest. Used to drive the normalizer's
    /// <c>family</c> argument without re-deriving it per test.
    /// </summary>
    private static PackageFamilyConfig GetFamily(FakeCakeWorld world, string familyName)
    {
        var manifest = ManifestFixture.CreateTestManifestConfig();
        return manifest.PackageFamilies.Single(f =>
            string.Equals(f.Name, familyName, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Constructs the normalizer with the world's <c>CakeContext</c> and <c>Log</c>.
    /// </summary>
    private static DependencyRangeNormalizer CreateNormalizer(FakeCakeWorld world)
        => new(world.CakeContext, world.Log);
}
```

- [ ] **Step 2: Verify the scaffold compiles**

Run: `dotnet build build/_build.Tests/Build.Tests.csproj -c Release`
Expected: build succeeds. `FakeCakeWorld.WithBinaryFile(FilePath, byte[])` is the canonical API for seeding ZIP/binary content (see `build/_build.Tests/Fixtures/FakeCakeWorld.cs:148` and existing call sites in `Unit/DependencyAnalysis/`).

- [ ] **Step 3: Commit**

```bash
git add build/_build.Tests/Scenarios/Characterization/PackageTopologyCharacterizationTests.cs
git commit -m "$(cat <<'EOF'
test(topology): scaffold characterization test class

Phase 1 of the package-topology refactor. Class skeleton + helpers for
nupkg seeding via NuspecBuilder and nuspec readback via NuspecReader.
No [Test] methods yet — added in subsequent commits.

refs #61
EOF
)"
```

---

## Task 3: Add Positive Characterization Tests

Three tests asserting today's `DependencyRangeNormalizer` behavior. Each test is one logical action against a seeded nupkg; the implementing slice (Phase 4 S9) flips assertion expectations when the corrected behavior lands.

**Files:**

- Modify: `build/_build.Tests/Scenarios/Characterization/PackageTopologyCharacterizationTests.cs`

### Test 1: Cross-family lower bound uses current family's version today (the bug)

This is the canonical capture of the cross-family lower-bound bug described in [`impact-map.md`](impact-map.md) §4.C. Today's `DependencyRangeNormalizer` receives `lowerBoundVersion` as the **current packing family's** version and embeds it as the lower bound — even when the dep is to another family with a different version anchor.

- [ ] **Step 1: Add the positive test method**

Append the following method inside `PackageTopologyCharacterizationTests`:

```csharp
[Test]
public async Task DependencyRangeNormalizer_Should_Use_Current_Family_Version_As_Lower_Bound_For_CrossFamily_Range_Today()
{
    var world = NewWorld();
    var nupkgPath = SeedNupkgWithCrossFamilyDep(
        world,
        packageId: SatelliteManagedPackageIdToday,
        version: SatelliteFamilyVersion,
        dependencyId: CoreManagedPackageIdToday,
        currentRange: SatelliteFamilyVersion); // Seed dep as a bare ">=" lower bound matching today's auto-emit shape

    var manifest = ManifestFixture.CreateTestManifestConfig();
    var satelliteFamily = GetFamily(world, SatelliteFamilyName);
    var normalizer = CreateNormalizer(world);

    await normalizer.NormalizeAsync(
        manifestConfig: manifest,
        family: satelliteFamily,
        managedPackagePath: nupkgPath,
        lowerBoundVersion: SatelliteFamilyVersion, // Today's PackageFamilyPacker passes the current family's version
        ct: TestContext.Current!.CancellationToken);

    var rewrittenRange = GetDependencyVersion(world, nupkgPath, CoreManagedPackageIdToday);

    // Bug-as-characterization: lower bound is the satellite's 2.8.0, NOT Core's 2.32.0.
    // Phase 4 S9 flips this assertion when DependencyRangeNormalizer plumbs PackageFamilyVersionSet
    // and uses the dep family's resolved version as the lower bound.
    await Assert.That(rewrittenRange).IsEqualTo("[2.8.0, 3.0.0)");
}
```

- [ ] **Step 2: Run only this test**

Run: `dotnet test build/_build.Tests/Build.Tests.csproj -c Release --framework net10.0 --filter "FullyQualifiedName~DependencyRangeNormalizer_Should_Use_Current_Family_Version_As_Lower_Bound_For_CrossFamily_Range_Today"`
Expected: PASS (1 passed, 0 failed, 0 skipped). The assertion captures today's reality including the bug.

If FAIL with assertion mismatch, that means today's normalizer is **not** producing `[2.8.0, 3.0.0)` — re-verify the seeded `currentRange` argument shape (`>=2.8.0` vs `2.8.0`) matches what `dotnet pack`'s auto-emit produces, and that `manifest.json package_families[].depends_on` lists `sdl2-core` under `sdl2-image`. Investigate; do not edit the assertion to make it pass — the test exists to document today's behavior.

### Test 2: No-deps families short-circuit (the `family.DependsOn.Count == 0` guard)

`DependencyRangeNormalizer.cs:41-44` short-circuits the entire method when the packing family has no cross-family deps. Core falls into this branch (no `depends_on`). Per [`impact-map.md`](impact-map.md) §4.C, the refactor relocates this guard to scope only the cross-family pass — the within-family pass must run unconditionally on role meta nuspecs. Phase 1 captures today's behavior: the entire normalizer is a no-op for `sdl2-core`.

- [ ] **Step 1: Add the positive test method**

```csharp
[Test]
public async Task DependencyRangeNormalizer_Should_Short_Circuit_When_Family_Has_No_DependsOn_Today()
{
    var world = NewWorld();
    var nupkgPath = SeedNupkgWithCrossFamilyDep(
        world,
        packageId: CoreManagedPackageIdToday,
        version: CoreFamilyVersion,
        dependencyId: "Janset.SDL2.Image", // Plant a fake dep to verify the normalizer does NOT touch it
        currentRange: ">=2.8.0");

    var manifest = ManifestFixture.CreateTestManifestConfig();
    var coreFamily = GetFamily(world, CoreFamilyName); // sdl2-core has empty DependsOn
    var normalizer = CreateNormalizer(world);

    await normalizer.NormalizeAsync(
        manifestConfig: manifest,
        family: coreFamily,
        managedPackagePath: nupkgPath,
        lowerBoundVersion: CoreFamilyVersion,
        ct: TestContext.Current!.CancellationToken);

    var unchangedRange = GetDependencyVersion(world, nupkgPath, "Janset.SDL2.Image");

    // Today's normalizer returns immediately when DependsOn is empty — even fake deps planted
    // in the nuspec are left untouched. Phase 4 S9 relocates this guard so within-family
    // exact-pin still runs on meta nuspecs even when DependsOn is empty (Core meta case).
    await Assert.That(unchangedRange).IsEqualTo(">=2.8.0");
}
```

- [ ] **Step 2: Run only this test**

Run: `dotnet test build/_build.Tests/Build.Tests.csproj -c Release --framework net10.0 --filter "FullyQualifiedName~DependencyRangeNormalizer_Should_Short_Circuit_When_Family_Has_No_DependsOn_Today"`
Expected: PASS.

### Test 3: Cross-family dep rewrites to a bounded range today (happy path)

Today's normalizer's happy path rewrites a satellite's cross-family dep from `>=x` to `[x, (UpstreamMajor+1).0.0)`. This is the existing G56 behavior and stays correct under the refactor — only the **lower-bound source** changes (per Test 1), not the **upper-bound rule**.

- [ ] **Step 1: Add the positive test method**

```csharp
[Test]
public async Task DependencyRangeNormalizer_Should_Rewrite_Cross_Family_Dep_To_Bounded_Range_Today()
{
    var world = NewWorld();
    var nupkgPath = SeedNupkgWithCrossFamilyDep(
        world,
        packageId: SatelliteManagedPackageIdToday,
        version: SatelliteFamilyVersion,
        dependencyId: CoreManagedPackageIdToday,
        currentRange: ">=2.8.0"); // Auto-emit shape from a ProjectReference

    var manifest = ManifestFixture.CreateTestManifestConfig();
    var satelliteFamily = GetFamily(world, SatelliteFamilyName);
    var normalizer = CreateNormalizer(world);

    await normalizer.NormalizeAsync(
        manifestConfig: manifest,
        family: satelliteFamily,
        managedPackagePath: nupkgPath,
        lowerBoundVersion: SatelliteFamilyVersion,
        ct: TestContext.Current!.CancellationToken);

    var rewrittenRange = GetDependencyVersion(world, nupkgPath, CoreManagedPackageIdToday);

    // The upper bound is sourced from Core's UpstreamMajor (2 → 3.0.0). This part of the
    // behavior survives the refactor unchanged. Only the lower bound changes (Test 1).
    await Assert.That(rewrittenRange).IsNotNull();
    await Assert.That(rewrittenRange!).Contains("3.0.0)"); // Upper bound rule
    await Assert.That(rewrittenRange).StartsWith("[");      // Bracket form, not bare ">="
}
```

- [ ] **Step 2: Run only this test**

Run: `dotnet test build/_build.Tests/Build.Tests.csproj -c Release --framework net10.0 --filter "FullyQualifiedName~DependencyRangeNormalizer_Should_Rewrite_Cross_Family_Dep_To_Bounded_Range_Today"`
Expected: PASS.

- [ ] **Step 3: Run all three positive tests together**

Run: `dotnet test build/_build.Tests/Build.Tests.csproj -c Release --framework net10.0 --filter "FullyQualifiedName~PackageTopologyCharacterizationTests"`
Expected: 3 passed, 0 failed, 0 skipped.

- [ ] **Step 4: Commit**

```bash
git add build/_build.Tests/Scenarios/Characterization/PackageTopologyCharacterizationTests.cs
git commit -m "$(cat <<'EOF'
test(topology): positive characterization for DependencyRangeNormalizer

Phase 1 — captures today's behavior including the cross-family
lower-bound bug from impact-map.md §4.C. Three positive tests cover:

  1. Lower bound uses current family's version (the bug)
  2. Empty DependsOn short-circuits the entire method
  3. Cross-family rewrite produces a bounded range

Phase 4 S9 flips test 1's assertion to the dep family's version and
adapts test 2 once the within-family pass is scoped separately from
the cross-family rewrite path.

refs #61
EOF
)"
```

---

## Task 4: Add Target Characterization Tests

Four target-state assertions, each marked with `[TopologyRefactorTarget]` and skipped until the implementing slice removes the attribute. They live in the codebase as an executable spec for Phase 2a–4 to flip.

**Files:**

- Modify: `build/_build.Tests/Scenarios/Characterization/PackageTopologyCharacterizationTests.cs`

### Target 1: Cross-family lower bound uses dep family's version (post-fix)

Pair with Positive Test 1. When Phase 4 S9 plumbs `PackageFamilyVersionSet` into the normalizer and sources the lower bound from the dep family's resolved version (see [`impact-map.md`](impact-map.md) §4.C), this target test activates and Positive Test 1's assertion flips.

- [ ] **Step 1: Add the target test method**

```csharp
[Test]
[TopologyRefactorTarget("Phase 4 S9 — DependencyRangeNormalizer mode dispatch + version set plumb")]
public async Task DependencyRangeNormalizer_Should_Use_DepFamily_Version_As_Lower_Bound_For_CrossFamily_Range()
{
    // Once Phase 4 S9 lands, this test activates by attribute removal. Implementation
    // expectations: NormalizeAsync accepts a PackageFamilyVersionSet (already at
    // build/_build/Data/Versions/PackageFamilyVersionSet.cs) and sources the lower
    // bound via versionSet.RequireVersion(depFamilyId).
    //
    // Example assertion shape (the implementing slice writes the real test body):
    //   var rewrittenRange = GetDependencyVersion(world, nupkgPath, CoreManagedPackageIdToday);
    //   await Assert.That(rewrittenRange).IsEqualTo("[2.32.0, 3.0.0)");

    await Task.CompletedTask;
}
```

- [ ] **Step 2: Verify the test is registered and skipped**

Run: `dotnet test build/_build.Tests/Build.Tests.csproj -c Release --framework net10.0 --filter "FullyQualifiedName~DependencyRangeNormalizer_Should_Use_DepFamily_Version_As_Lower_Bound_For_CrossFamily_Range"`
Expected: 0 passed, 0 failed, 1 skipped. Skip reason includes "Phase 4 S9".

### Target 2: Within-family exact-pin runs unconditionally on role meta nuspec

Pair with Positive Test 2. When Phase 4 S9 splits the cross-family guard from the within-family pass and adds the `CrossFamilyAndWithinFamilyRewrite` mode (see [`impact-map.md`](impact-map.md) §4.C), this target test activates.

- [ ] **Step 1: Add the target test method**

```csharp
[Test]
[TopologyRefactorTarget("Phase 4 S9 — DependencyRangeNormalizer mode dispatch + within-family exact-pin pass")]
public async Task DependencyRangeNormalizer_Should_ExactPin_Bindings_And_Native_In_Role_Meta_Nuspec_When_Mode_Is_CrossFamilyAndWithinFamilyRewrite()
{
    // Implementation expectations after Phase 4 S9:
    //   - NormalizeAsync accepts a DependencyNormalizationMode parameter
    //   - When mode == CrossFamilyAndWithinFamilyRewrite and the artifact is a role
    //     meta nuspec, every <dependency id> matching BindingsPackageId(family) or
    //     NativePackageId(family) is rewritten to "[$(Version)]" exact.
    //   - The pass runs unconditionally — Core role meta (DependsOn.Count == 0) still
    //     gets its within-family pair exact-pinned.

    await Task.CompletedTask;
}
```

- [ ] **Step 2: Verify skip**

Run: `dotnet test build/_build.Tests/Build.Tests.csproj -c Release --framework net10.0 --filter "FullyQualifiedName~DependencyRangeNormalizer_Should_ExactPin_Bindings_And_Native_In_Role_Meta_Nuspec"`
Expected: 0 passed, 0 failed, 1 skipped.

### Target 3: Satellite Native nuspec synthesizes cross-family Core.Native dep

When Phase 4 S9 implements the `CrossFamilySynthesize` mode (see [`impact-map.md`](impact-map.md) §4.C — Satellite Native synthesize path, no `exclude="Build,Analyzers"`), this target test activates.

- [ ] **Step 1: Add the target test method**

```csharp
[Test]
[TopologyRefactorTarget("Phase 4 S9 — DependencyRangeNormalizer synthesize mode for satellite Native cross-family dep")]
public async Task DependencyRangeNormalizer_Should_Synthesize_Core_Native_Dep_When_Mode_Is_CrossFamilySynthesize_On_Satellite_Native_Nuspec()
{
    // Implementation expectations after Phase 4 S9:
    //   - When mode == CrossFamilySynthesize and the input nuspec has zero <dependency>
    //     elements (because src/native/Directory.Build.props sets
    //     SuppressDependenciesWhenPacking=true), the normalizer creates a <dependencies>/<group>
    //     parent chain (attaching both to <metadata>) and adds a <dependency id="Janset.SDL2.Core.Native">
    //     element with version "[<coreFamilyVersion>, 3.0.0)" and NO exclude attribute (Core.Native's
    //     buildTransitive contract must flow through to consumers — see impact-map.md §4.C).
    //   - Re-running synthesize on a nuspec that already has the dep replaces (dedup) rather
    //     than duplicates.

    await Task.CompletedTask;
}
```

- [ ] **Step 2: Verify skip**

Run: `dotnet test build/_build.Tests/Build.Tests.csproj -c Release --framework net10.0 --filter "FullyQualifiedName~DependencyRangeNormalizer_Should_Synthesize_Core_Native_Dep"`
Expected: 0 passed, 0 failed, 1 skipped.

### Target 4: Bindings nuspec carries zero same-family Native deps

When Phase 4 S9 lands (the bindings csproj has no same-family Native `ProjectReference` per [`impact-map.md`](impact-map.md) §3.B, and the normalizer's `CrossFamilyRewrite` mode does not touch within-family slots in bindings nuspec), this target test activates.

- [ ] **Step 1: Add the target test method**

```csharp
[Test]
[TopologyRefactorTarget("Phase 4 S9 — bindings nuspec post-refactor shape")]
public async Task DependencyRangeNormalizer_Should_Leave_Bindings_Nuspec_With_Zero_SameFamily_Native_Deps_After_Refactor()
{
    // Implementation expectations after Phase 4 S9:
    //   - Bindings csproj has no <ProjectReference> to same-family Native (Phase 3 S6
    //     removes it).
    //   - dotnet pack therefore auto-emits no same-family Native dep in the bindings nuspec.
    //   - DependencyRangeNormalizer in CrossFamilyRewrite mode does not synthesize one.
    //   - Verify by: pack a bindings nupkg via FakeCakeWorld (or seed one with NuspecBuilder
    //     in a no-within-family-dep shape), run normalizer in CrossFamilyRewrite, assert
    //     GetDependencyVersion(world, nupkgPath, NativePackageId(family)) returns null.

    await Task.CompletedTask;
}
```

- [ ] **Step 2: Verify skip**

Run: `dotnet test build/_build.Tests/Build.Tests.csproj -c Release --framework net10.0 --filter "FullyQualifiedName~DependencyRangeNormalizer_Should_Leave_Bindings_Nuspec_With_Zero_SameFamily_Native_Deps"`
Expected: 0 passed, 0 failed, 1 skipped.

- [ ] **Step 3: Commit**

```bash
git add build/_build.Tests/Scenarios/Characterization/PackageTopologyCharacterizationTests.cs
git commit -m "$(cat <<'EOF'
test(topology): target characterization (skipped, Phase 4 S9 activates)

Phase 1 — registers four target-state assertions describing the
post-refactor DependencyRangeNormalizer behavior:

  1. Cross-family lower bound from dep family's resolved version
  2. Within-family exact-pin runs unconditionally on role meta
  3. Satellite Native nuspec synthesizes Core.Native cross-family dep
  4. Bindings nuspec carries zero same-family Native deps

All four marked [TopologyRefactorTarget("Phase 4 S9 — …")] and skipped
until the implementing slice removes the attribute and writes the real
test body. Spec lives in the codebase per impact-map.md §9.H activation
pattern.

refs #61
EOF
)"
```

---

## Task 5: Final Suite Verification

- [ ] **Step 1: Run the full characterization suite**

Run: `dotnet test build/_build.Tests/Build.Tests.csproj -c Release --framework net10.0 --filter "FullyQualifiedName~PackageTopologyCharacterizationTests"`
Expected: 7 total — 3 passed, 0 failed, 4 skipped. Skip reasons mention "Phase 4 S9".

- [ ] **Step 2: Run the full test project**

Run: `dotnet test build/_build.Tests/Build.Tests.csproj -c Release --framework net10.0`
Expected: green build, all existing tests still pass, skipped count increases by 4.

- [ ] **Step 3: Run `dotnet-skills:slopwatch` per AGENTS.md Tier 1**

Run: `slopwatch analyze --fail-on warning --exclude "artifacts/**,external/**,vcpkg_installed/**,**/bin/**,**/obj/**"`
Expected: clean. Phase 1 introduces test code only — no disabled tests, no broad suppressions, no empty catch blocks. If slopwatch flags the `[TopologyRefactorTarget]` skip attribute as a "disabled test" pattern, surface that to the operator before merging — characterization-spec tests are a legitimate pattern but slopwatch may not know that yet.

- [ ] **Step 4: Confirm phase completion**

Phase 1 deliverable: characterization test suite green-build + green-test, target tests registered + skipped with phase tags, three commits landed in sequence, no production code touched. Ready to author Phase 2a plan against the now-stable Phase 1 artifacts.

---

## Deferred Coverage (Pushed to Later Phases)

[`impact-map.md`](impact-map.md) §9.H lists ten characterization tests. Phase 1 takes seven (three positive + four target). The remaining three require infrastructure that does not exist at Phase 1 — pre-committing them now produces speculative test scaffolding that the implementing slice would rewrite. Each is recorded against the phase that brings the infrastructure online.

| impact-map.md §9.H test name | Deferred to | Why |
| --- | --- | --- |
| `CurrentGraph_Should_Emit_Three_Nupkgs_Per_Family` (target) | Phase 4 S8 (`PackageFamilyPacker` 3-pack rewrite) | Needs `PackageFamilyPacker` scenario harness that returns the emitted nupkg path triple. Today's harness asserts on orchestration outcomes (exception/log/process invocations) per `PackageTaskScenarioTests.cs:34-48`, not on a real nupkg file set. The harness extension lands with the 3-pack rewrite in Phase 4 S8 — characterization gets written then. |
| `CurrentGraph_Should_Preserve_Native_Payload_Layout_Under_Runtimes_Rid_Native` (positive) | Phase 4 S8 | Asserts on real `runtimes/<rid>/native/` payload layout inside a packed Native nupkg. Requires either real `dotnet pack` invocation (not feasible in `FakeCakeWorld` — process is stubbed) or pre-seeded nupkg fixture covering the full `runtimes/` tree. Both presuppose Phase 4 S8 work to be in place. |
| `CurrentGraph_Should_Preserve_Native_Metadata_Json` (positive) | Phase 4 S8 | Same reason — asserts on real packed Native nupkg content (`janset-native-metadata.json` at root, schema-valid). Phase 4 S8's harness extension covers it. |

The three deferred tests are tracked in `impact-map.md` §13 slice table under S8's scope.

Phase 1 deliberately scopes to `DependencyRangeNormalizer` because (a) it has the cleanest unit-test seam (constructor takes `ICakeContext` + `ICakeLog`, signature accepts a `FilePath` to an arbitrary nupkg), (b) it is the system that the cross-family lower-bound bug lives in and the system that Phase 4 S9 changes most aggressively, and (c) the lower-bound bug deserves a green captured-today characterization on master before Phase 2a touches anything — that's the floor below which the refactor cannot regress.

---

## Cross-Reference

- [`phase-planning-methodology.md`](phase-planning-methodology.md) — methodology, phase status, authoring convention
- [`strategy-brief.md`](strategy-brief.md) — strategic decisions
- [`impact-map.md`](impact-map.md) §4.C, §9.H — normalizer extension + characterization test inventory
- [`../../decisions/2026-05-05-target-centric-build-host.md`](../../decisions/2026-05-05-target-centric-build-host.md) (ADR-002) — test file location convention
- [`../../decisions/2026-05-12-build-host-data-layer.md`](../../decisions/2026-05-12-build-host-data-layer.md) (ADR-003) — `PackageFamilyVersionSet` lives under `Build.Data.Versions` per the contract-centric data layer
- [`../../knowledge-base/testing-guidelines.md`](../../knowledge-base/testing-guidelines.md) — canonical test infra, fixture data policy, TUnit rules
- [`../../knowledge-base/release-guardrails.md`](../../knowledge-base/release-guardrails.md) — G56 (cross-family upper bound), the rule whose lower-bound source this Phase characterizes
- [`../../../AGENTS.md`](../../../AGENTS.md) — operating rules, approval gate, test naming convention, Tier 1 slopwatch mandate
