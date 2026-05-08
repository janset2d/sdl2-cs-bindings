# P6 PreFlightCheck Migration — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Migrate `Features/Preflight/` → `Targets/PreFlightCheck/` while establishing a root-level `Validation/` named concept holding all 7 build-host validators, retiring `IReadOnlyDictionary<string, NuGetVersion>` from every task/service boundary, deleting 5 OneOf result types + 3 single-impl interfaces, adding `IVcpkgManifestRepository` and a new `ManifestFamilyNameInvariantValidator` (G59), migrating all touched tests to V2, and validating end-to-end across Windows + WSL + macOS + GitHub Actions release pipeline.

**Architecture:** `Validation/{Manifest,Versioning,Packaging,Models,Conventions}/` root concept with single `AddValidators()` registration; `Targets/PreFlightCheck/` orchestrates 7 sealed-class validators directly via DI; `PackageFamilyVersionSet` typed boundary replaces dict everywhere; static validators promoted to instance for uniformity; `IVcpkgManifestRepository` mirrors `IManifestRepository`; PreFlightCheckTask body has zero private methods, no path plumbing, no defensive double-checks.

**Tech Stack:** .NET 10 / C# 14, Cake Frosting 6.1, TUnit + Microsoft.Testing.Platform, V2 test infra (FakeCakeWorldV2, TargetTestHostV2, TestLogV2), vcpkg, GitHub Actions.

**Spec:** `docs/superpowers/specs/2026-05-08-p6-preflight-migration-design.md`
**ADR:** `docs/decisions/2026-05-05-target-centric-build-host.md`
**Refactor plan:** `docs/refactoring/target-centric-build-host-refactor-plan.md` §11 P6
**Lifecycle:** This plan is a temp slice doc per `docs/refactoring/README.md` — working-tree scratch, never enters git history. Migrate any durable findings to canonical docs before deletion.

---

## Approval gate notes

Per AGENTS.md "Approval Gate":

- Every commit needs Deniz approval — present summary + proposed commit message before each `git commit`.
- Documentation-only edits (Phase 8) are exempt from approval gate but still benefit from a heads-up.
- Push to `master` only after the full slice passes cross-platform validation AND Deniz signs off (Phase 9).
- GitHub Actions release pipeline manual trigger is Deniz-only — plan does not invoke `workflow_dispatch`.

---

## Phase 0: Worktree setup (NOT a commit)

### Task 0: Create isolated worktree on clean master

Per AGENTS.md "ADR-002 migration execution rules": migrate on an isolated branch/worktree from clean master.

- [ ] **Step 1: Verify clean working tree on `master`**

```pwsh
git status
git log -1 --oneline
```

Expected: HEAD on `392c35e docs: add cross-platform smoke validation playbook` (or later post-S11 commit). Working tree may have untracked files in `docs/superpowers/plans/`, `docs/superpowers/specs/`, and `.github/prompts/` (from the brainstorming session) — these are temp slice docs and stay outside the worktree.

- [ ] **Step 2: Invoke `superpowers:using-git-worktrees` skill to create the slice worktree**

Skill creates worktree at `e:\tmp\sdl2-cs-bindings-p6-preflight\` (or platform equivalent) on a fresh branch `slice/p6-preflight` based off `master`. From this point forward all editing happens inside the worktree; `master` checkout stays untouched.

- [ ] **Step 3: Verify worktree is operational**

```pwsh
cd e:\tmp\sdl2-cs-bindings-p6-preflight
dotnet build build/_build/Build.csproj
dotnet test --project build/_build.Tests/Build.Tests.csproj -c Release --framework net10.0
```

Expected: build green, tests green (529 baseline post-S11). If anything red, fix before starting Phase 1.

- [ ] **Step 4: Initialize submodules in the worktree**

```pwsh
git submodule update --init --recursive
```

Per AGENTS.md: "Isolated worktrees are not native-build workspaces unless explicitly provisioned." This slice does not run native vcpkg flows in the worktree — only managed builds + tests. Cross-platform validation runs from the main provisioned checkout AFTER merge to master.

---

## Phase 1: Foundation (3 commits)

### Task 1: Create Validation/ root concept skeleton + AddValidators() extension

**Files:**

- Create: `build/_build/Validation/ServiceCollectionExtensions.cs`

- [ ] **Step 1: Write the extension skeleton**

Contents of `build/_build/Validation/ServiceCollectionExtensions.cs`:

```csharp
using Microsoft.Extensions.DependencyInjection;

namespace Build.Validation;

/// <summary>
/// Single registration point for every build-host validator. Validators live under
/// <c>Validation/Manifest/</c>, <c>Validation/Versioning/</c>, and <c>Validation/Packaging/</c>
/// — the alt-folders organize them by domain while the canonical lookup point stays here.
/// Composition root calls this once; PreFlightCheck, ResolveVersionsFromExplicit, and Package
/// all consume validators from DI.
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddValidators(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Validators get wired in by subsequent migration steps.
        // Final shape: 7 sealed-class AddSingleton registrations covering Manifest, Versioning, Packaging.
        return services;
    }
}
```

- [ ] **Step 2: Add `Build.Validation` namespace usage to Program.cs composition root**

Read `build/_build/Program.cs`. Find the existing service registration block. Add `services.AddValidators();` after `services.AddRepositories();`. Add `using Build.Validation;` to the file's using directives.

- [ ] **Step 3: Build green check**

```pwsh
dotnet build build/_build/Build.csproj
```

Expected: 0 warnings, 0 errors. Empty `AddValidators()` is a no-op so no DI failure expected.

- [ ] **Step 4: Run tests**

```pwsh
dotnet test --project build/_build.Tests/Build.Tests.csproj -c Release --framework net10.0
```

Expected: all green (529).

- [ ] **Step 5: Commit (request Deniz approval first)**

Present this commit message preview:

```text
refactor: introduce Validation/ root concept + empty AddValidators() extension

Establishes the single registration point per the P6 design. Subsequent
slice steps populate AddValidators() with sealed-class validators relocated
from Features/Preflight/, Shared/Packaging/, and Shared/Versioning/.
```

After approval:

```pwsh
git add build/_build/Validation/ServiceCollectionExtensions.cs build/_build/Program.cs
git commit -m "refactor: introduce Validation/ root concept + empty AddValidators() extension"
```

---

### Task 2: Add IVcpkgManifestRepository + impl + tests

**Files:**

- Create: `build/_build/Repositories/IVcpkgManifestRepository.cs`
- Create: `build/_build/Repositories/VcpkgManifestRepository.cs`
- Modify: `build/_build/Repositories/ServiceCollectionExtensions.cs`
- Create: `build/_build.Tests/Unit/Repositories/VcpkgManifestRepositoryTests.cs`

- [ ] **Step 1: Write the failing test**

Contents of `build/_build.Tests/Unit/Repositories/VcpkgManifestRepositoryTests.cs`:

```csharp
using Build.Integrations.Vcpkg;
using Build.Repositories;
using Build.Tests.Fixtures;
using Cake.Core;
using Cake.Core.IO;
using NSubstitute;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace Build.Tests.Unit.Repositories;

public sealed class VcpkgManifestRepositoryTests
{
    [Test]
    public async Task Load_Should_Return_Parsed_Manifest_When_File_Exists()
    {
        var world = FakeCakeWorldV2.CreateWindows()
            .WithTextFile("vcpkg.json", """{"name":"sdl2-cs-bindings","overrides":[{"name":"sdl2","version":"2.32.0"}]}""");

        var reader = Substitute.For<IVcpkgManifestReader>();
        var fakeManifest = new VcpkgManifest(
            Name: "sdl2-cs-bindings",
            Overrides: [new VcpkgOverride(Name: "sdl2", Version: "2.32.0", PortVersion: 0)]);
        reader.ParseFile(Arg.Any<FilePath>()).Returns(fakeManifest);

        var path = world.RepoRoot.CombineWithFilePath("vcpkg.json");
        var repository = new VcpkgManifestRepository(world.CakeContext, reader, path);

        var loaded = repository.Load();

        await Assert.That(loaded).IsSameReferenceAs(fakeManifest);
    }

    [Test]
    public async Task Load_Should_Throw_CakeException_When_File_Is_Missing()
    {
        var world = FakeCakeWorldV2.CreateWindows();
        var reader = Substitute.For<IVcpkgManifestReader>();
        var path = world.RepoRoot.CombineWithFilePath("vcpkg.json");
        var repository = new VcpkgManifestRepository(world.CakeContext, reader, path);

        var ex = await Assert.That(() => repository.Load()).Throws<CakeException>();

        await Assert.That(ex.Message).Contains("vcpkg manifest");
        await Assert.That(ex.Message).Contains("does not exist");
    }

    [Test]
    public async Task Constructor_Should_Throw_ArgumentNullException_When_Context_Is_Null()
    {
        var reader = Substitute.For<IVcpkgManifestReader>();
        var path = new FilePath("/repo/vcpkg.json");

        await Assert.That(() => new VcpkgManifestRepository(null!, reader, path))
            .Throws<ArgumentNullException>();
    }

    [Test]
    public async Task Constructor_Should_Throw_ArgumentNullException_When_Reader_Is_Null()
    {
        var world = FakeCakeWorldV2.CreateWindows();
        var path = world.RepoRoot.CombineWithFilePath("vcpkg.json");

        await Assert.That(() => new VcpkgManifestRepository(world.CakeContext, null!, path))
            .Throws<ArgumentNullException>();
    }

    [Test]
    public async Task Constructor_Should_Throw_ArgumentNullException_When_Path_Is_Null()
    {
        var world = FakeCakeWorldV2.CreateWindows();
        var reader = Substitute.For<IVcpkgManifestReader>();

        await Assert.That(() => new VcpkgManifestRepository(world.CakeContext, reader, null!))
            .Throws<ArgumentNullException>();
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

```pwsh
dotnet test --project build/_build.Tests/Build.Tests.csproj -c Release --framework net10.0 --filter "VcpkgManifestRepositoryTests"
```

Expected: compilation error — `VcpkgManifestRepository` and `IVcpkgManifestRepository` types do not exist yet.

- [ ] **Step 3: Write the interface**

Contents of `build/_build/Repositories/IVcpkgManifestRepository.cs`:

```csharp
using Build.Integrations.Vcpkg;

namespace Build.Repositories;

/// <summary>
/// Practical file adapter (ADR-002 §6) for the repository's vcpkg manifest. Mirrors
/// <see cref="IManifestRepository"/>: knows the canonical file path via DI, parses
/// it via <see cref="IVcpkgManifestReader"/>, and surfaces missing-file failures as
/// <c>CakeException</c> with an operator-friendly message.
/// </summary>
public interface IVcpkgManifestRepository
{
    VcpkgManifest Load();
}
```

- [ ] **Step 4: Write the implementation**

Contents of `build/_build/Repositories/VcpkgManifestRepository.cs`:

```csharp
using Build.Integrations.Vcpkg;
using Cake.Common.IO;
using Cake.Core;
using Cake.Core.IO;

namespace Build.Repositories;

public sealed class VcpkgManifestRepository : IVcpkgManifestRepository
{
    private readonly ICakeContext _context;
    private readonly IVcpkgManifestReader _reader;
    private readonly FilePath _vcpkgManifestPath;

    public VcpkgManifestRepository(ICakeContext context, IVcpkgManifestReader reader, FilePath vcpkgManifestPath)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _reader = reader ?? throw new ArgumentNullException(nameof(reader));
        _vcpkgManifestPath = vcpkgManifestPath ?? throw new ArgumentNullException(nameof(vcpkgManifestPath));
    }

    public VcpkgManifest Load()
    {
        if (!_context.FileExists(_vcpkgManifestPath))
        {
            throw new CakeException(
                $"VcpkgManifestRepository cannot load vcpkg manifest: file does not exist at '{_vcpkgManifestPath.FullPath}'. " +
                "Run from the repository root or pass --repo-root to point Cake at a valid checkout.");
        }

        return _reader.ParseFile(_vcpkgManifestPath);
    }
}
```

- [ ] **Step 5: Register in AddRepositories()**

Read `build/_build/Repositories/ServiceCollectionExtensions.cs`. Add after the `IManifestRepository` registration (mirroring its factory shape):

```csharp
services.AddSingleton<IVcpkgManifestRepository>(sp => new VcpkgManifestRepository(
    sp.GetRequiredService<ICakeContext>(),
    sp.GetRequiredService<IVcpkgManifestReader>(),
    sp.GetRequiredService<IPathService>().GetVcpkgManifestFile()));
```

Add `using Build.Integrations.Vcpkg;` and `using Build.Host.Paths;` if not already present.

- [ ] **Step 6: Run tests to verify they pass**

```pwsh
dotnet test --project build/_build.Tests/Build.Tests.csproj -c Release --framework net10.0 --filter "VcpkgManifestRepositoryTests"
```

Expected: 5 tests PASS.

- [ ] **Step 7: Build + full test suite**

```pwsh
dotnet build build/_build/Build.csproj
dotnet test --project build/_build.Tests/Build.Tests.csproj -c Release --framework net10.0
```

Expected: 0 warnings, 0 errors. All tests green (534 = 529 + 5 new).

- [ ] **Step 8: Commit (request Deniz approval first)**

Commit message preview:

```text
refactor: add IVcpkgManifestRepository for ADR-002 §6 repository symmetry

Mirrors IManifestRepository — wraps the existing IVcpkgManifestReader behind
a path-aware repository that throws operator-friendly CakeException on missing
files. Eliminates the need for PreFlightCheckTask to plumb vcpkg paths.
```

After approval:

```pwsh
git add build/_build/Repositories/IVcpkgManifestRepository.cs build/_build/Repositories/VcpkgManifestRepository.cs build/_build/Repositories/ServiceCollectionExtensions.cs build/_build.Tests/Unit/Repositories/VcpkgManifestRepositoryTests.cs
git commit -m "refactor: add IVcpkgManifestRepository for ADR-002 §6 repository symmetry"
```

---

### Task 3: Add ManifestFamilyNameInvariantValidator (G59) + tests

**Files:**

- Create: `build/_build/Validation/Manifest/ManifestFamilyNameInvariantValidator.cs`
- Create: `build/_build.Tests/Unit/Validation/Manifest/ManifestFamilyNameInvariantValidatorTests.cs`
- Modify: `build/_build/Validation/ServiceCollectionExtensions.cs`

- [ ] **Step 1: Write the failing tests**

Contents of `build/_build.Tests/Unit/Validation/Manifest/ManifestFamilyNameInvariantValidatorTests.cs`:

```csharp
using System.Collections.Immutable;
using Build.Shared.Manifest;
using Build.Tests.Fixtures;
using Build.Validation.Manifest;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace Build.Tests.Unit.Validation.Manifest;

public sealed class ManifestFamilyNameInvariantValidatorTests
{
    [Test]
    public async Task Validate_Should_Return_Empty_Report_When_All_Names_Match_Canonical_Pattern()
    {
        var manifest = ManifestFixture.CreateTestManifestConfig();
        var validator = new ManifestFamilyNameInvariantValidator();

        var report = validator.Validate(manifest);

        await Assert.That(report.IsValid).IsTrue();
        await Assert.That(report.Errors.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Validate_Should_Return_Error_When_Name_Has_Uppercase()
    {
        var manifest = WithExtraFamily(ManifestFixture.CreateTestManifestConfig(), "SDL2-Test");
        var validator = new ManifestFamilyNameInvariantValidator();

        var report = validator.Validate(manifest);

        await Assert.That(report.IsValid).IsFalse();
        await Assert.That(report.Errors.Count).IsEqualTo(1);
        await Assert.That(report.Errors[0].Message).Contains("SDL2-Test");
        await Assert.That(report.Errors[0].Code).IsEqualTo("G59");
    }

    [Test]
    public async Task Validate_Should_Return_Error_When_Name_Is_Underscore_Delimited()
    {
        var manifest = WithExtraFamily(ManifestFixture.CreateTestManifestConfig(), "sdl2_core");
        var validator = new ManifestFamilyNameInvariantValidator();

        var report = validator.Validate(manifest);

        await Assert.That(report.IsValid).IsFalse();
        await Assert.That(report.Errors[0].Message).Contains("sdl2_core");
    }

    [Test]
    public async Task Validate_Should_Return_Error_When_Name_Is_Empty()
    {
        var manifest = WithExtraFamily(ManifestFixture.CreateTestManifestConfig(), "");
        var validator = new ManifestFamilyNameInvariantValidator();

        var report = validator.Validate(manifest);

        await Assert.That(report.IsValid).IsFalse();
        await Assert.That(report.Errors[0].Message).Contains("empty name");
    }

    [Test]
    public async Task Validate_Should_Return_Error_When_Major_Is_Not_Digits()
    {
        var manifest = WithExtraFamily(ManifestFixture.CreateTestManifestConfig(), "sdlx-core");
        var validator = new ManifestFamilyNameInvariantValidator();

        var report = validator.Validate(manifest);

        await Assert.That(report.IsValid).IsFalse();
        await Assert.That(report.Errors[0].Message).Contains("sdlx-core");
    }

    [Test]
    public async Task Validate_Should_Return_Multiple_Errors_For_Multiple_Violations()
    {
        var manifest = WithExtraFamily(
            WithExtraFamily(ManifestFixture.CreateTestManifestConfig(), "SDL2-Foo"),
            "sdl2_bar");
        var validator = new ManifestFamilyNameInvariantValidator();

        var report = validator.Validate(manifest);

        await Assert.That(report.Errors.Count).IsEqualTo(2);
    }

    private static ManifestConfig WithExtraFamily(ManifestConfig manifest, string name)
    {
        var families = manifest.PackageFamilies.Add(new PackageFamilyConfig(
            Name: name,
            ManagedProject: null,
            NativeProject: null,
            LibraryRef: "sdl2",
            DependsOn: ImmutableList<string>.Empty));
        return manifest with { PackageFamilies = families };
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

```pwsh
dotnet test --project build/_build.Tests/Build.Tests.csproj -c Release --framework net10.0 --filter "ManifestFamilyNameInvariantValidatorTests"
```

Expected: compilation error — `ManifestFamilyNameInvariantValidator` does not exist.

- [ ] **Step 3: Write the validator**

Contents of `build/_build/Validation/Manifest/ManifestFamilyNameInvariantValidator.cs`:

```csharp
using System.Text.RegularExpressions;
using Build.Results;
using Build.Shared.Manifest;

namespace Build.Validation.Manifest;

/// <summary>
/// Validates that every <c>package_families[].name</c> is lowercase kebab-case
/// matching the canonical <c>sdl&lt;major&gt;-&lt;role&gt;</c> pattern. Hand-edited
/// mixed-case entries (e.g. <c>SDL2-Core</c>) silently bypass <see cref="PackageFamilyId"/>
/// ordinal-exact lookups in downstream consumers; this validator catches the drift at
/// PreFlight time before any build operation runs.
/// </summary>
public sealed partial class ManifestFamilyNameInvariantValidator
{
    [GeneratedRegex(@"^sdl[0-9]+-[a-z][a-z0-9-]*$", RegexOptions.CultureInvariant)]
    private static partial Regex FamilyNamePattern();

    public ValidationReport Validate(ManifestConfig manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        var checks = new List<ValidationCheck>();
        foreach (var family in manifest.PackageFamilies)
        {
            if (string.IsNullOrWhiteSpace(family.Name))
            {
                checks.Add(new ValidationCheck(
                    Name: "Manifest family name invariant",
                    Severity: ValidationSeverity.Error,
                    Message: "Manifest package_families[] entry has an empty name. Every family must declare a 'sdl<major>-<role>' kebab-case identifier.",
                    Code: "G59"));
                continue;
            }

            if (!FamilyNamePattern().IsMatch(family.Name))
            {
                checks.Add(new ValidationCheck(
                    Name: "Manifest family name invariant",
                    Severity: ValidationSeverity.Error,
                    Message: $"Family name '{family.Name}' does not match required pattern 'sdl<major>-<role>' (lowercase kebab, e.g. 'sdl2-core', 'sdl2-image'). Mixed-case or underscore-delimited names break PackageFamilyId ordinal-exact lookups in downstream consumers.",
                    Code: "G59"));
            }
        }

        return checks.Count == 0 ? ValidationReport.Empty : new ValidationReport(checks);
    }
}
```

Note: `using Build.Versioning;` is intentionally omitted from the `<see cref>` — XML doc cref to a class in another namespace requires either the using or a fully-qualified cref, but the non-resolving cref is a documentation lint warning at worst, not a compile error. If the worktree's docs build complains, fully-qualify as `<see cref="Build.Versioning.PackageFamilyId"/>`.

- [ ] **Step 4: Register in AddValidators()**

Read `build/_build/Validation/ServiceCollectionExtensions.cs`. Replace the no-op body:

```csharp
using Build.Validation.Manifest;
using Microsoft.Extensions.DependencyInjection;

namespace Build.Validation;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddValidators(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<ManifestFamilyNameInvariantValidator>();

        return services;
    }
}
```

Subsequent tasks expand this list as validators relocate into `Validation/`.

- [ ] **Step 5: Run tests to verify all pass**

```pwsh
dotnet test --project build/_build.Tests/Build.Tests.csproj -c Release --framework net10.0 --filter "ManifestFamilyNameInvariantValidatorTests"
```

Expected: 6 tests PASS.

- [ ] **Step 6: Build + full test suite**

```pwsh
dotnet build build/_build/Build.csproj
dotnet test --project build/_build.Tests/Build.Tests.csproj -c Release --framework net10.0
```

Expected: 0 warnings, 0 errors. All tests green (540 = 534 + 6 new).

- [ ] **Step 7: Commit (request Deniz approval first)**

Commit message preview:

```text
feat: add ManifestFamilyNameInvariantValidator (G59) under Validation/Manifest/

New PreFlight guardrail catches hand-edited manifest entries with mixed-case
or underscore-delimited family names (e.g. SDL2-Core, sdl2_core) that would
silently bypass PackageFamilyId ordinal-exact lookups downstream.
```

After approval:

```pwsh
git add build/_build/Validation/Manifest/ManifestFamilyNameInvariantValidator.cs build/_build.Tests/Unit/Validation/Manifest/ManifestFamilyNameInvariantValidatorTests.cs build/_build/Validation/ServiceCollectionExtensions.cs
git commit -m "feat: add ManifestFamilyNameInvariantValidator (G59) under Validation/Manifest/"
```

---

## Phase 2: Relocate existing validators to Validation/ (6 commits)

Each task in this phase: `git mv` first (preserves `git log --follow` and `git blame -C` history per AGENTS.md "git mv during migrations" rule), then content edits in a follow-up step. Validator return types swap from OneOf-shaped result wrappers to direct domain `*Validation` records inside the same task — OneOf result type files are deleted in Phase 6 once nothing references them.

### Task 4: Move HybridStaticOverlayValidator → Validation/Packaging/

**Files:**

- `git mv`: `build/_build/Features/Preflight/HybridStaticOverlayValidator.cs` → `build/_build/Validation/Packaging/HybridStaticOverlayValidator.cs`
- `git mv`: `build/_build.Tests/Unit/Features/Preflight/HybridStaticOverlayValidatorTests.cs` → `build/_build.Tests/Unit/Validation/Packaging/HybridStaticOverlayValidatorTests.cs`
- Modify: `build/_build/Validation/ServiceCollectionExtensions.cs` (add registration)
- Modify: `build/_build/Features/Preflight/PreflightPipeline.cs` (using directive update)
- Modify: `build/_build/Features/Preflight/ServiceCollectionExtensions.cs` (drop registration — moves to AddValidators)

- [ ] **Step 1: git mv production file**

```pwsh
git mv build/_build/Features/Preflight/HybridStaticOverlayValidator.cs build/_build/Validation/Packaging/HybridStaticOverlayValidator.cs
```

- [ ] **Step 2: git mv test file**

```pwsh
git mv build/_build.Tests/Unit/Features/Preflight/HybridStaticOverlayValidatorTests.cs build/_build.Tests/Unit/Validation/Packaging/HybridStaticOverlayValidatorTests.cs
```

- [ ] **Step 3: Update production namespace**

Read `build/_build/Validation/Packaging/HybridStaticOverlayValidator.cs`. Change `namespace Build.Features.Preflight;` to `namespace Build.Validation.Packaging;`. The class body, ctor signature, and `Validate(IImmutableList<RuntimeInfo>)` return shape (`ValidationReport`) stay unchanged — already canonical.

- [ ] **Step 4: Update test namespace + using directives**

Read `build/_build.Tests/Unit/Validation/Packaging/HybridStaticOverlayValidatorTests.cs`. Change `namespace Build.Tests.Unit.Features.Preflight;` to `namespace Build.Tests.Unit.Validation.Packaging;`. Change any `using Build.Features.Preflight;` to `using Build.Validation.Packaging;`. No assertion changes — return type was already `ValidationReport`.

- [ ] **Step 5: Update PreflightPipeline using directive**

Read `build/_build/Features/Preflight/PreflightPipeline.cs`. Add `using Build.Validation.Packaging;`. The existing `HybridStaticOverlayValidator` field stays — same class, new namespace.

- [ ] **Step 6: Drop registration from Features/Preflight/ServiceCollectionExtensions**

Read `build/_build/Features/Preflight/ServiceCollectionExtensions.cs`. Delete the line:

```csharp
services.AddSingleton<HybridStaticOverlayValidator>();
```

- [ ] **Step 7: Add registration to Validation/ServiceCollectionExtensions**

Read `build/_build/Validation/ServiceCollectionExtensions.cs`. Add `using Build.Validation.Packaging;`. Add inside `AddValidators`:

```csharp
services.AddSingleton<HybridStaticOverlayValidator>();
```

- [ ] **Step 8: Build + tests**

```pwsh
dotnet build build/_build/Build.csproj
dotnet test --project build/_build.Tests/Build.Tests.csproj -c Release --framework net10.0
```

Expected: 0 warnings, 0 errors. All tests green.

- [ ] **Step 9: Commit (request approval)**

Commit message preview:

```text
refactor: relocate HybridStaticOverlayValidator to Validation/Packaging/

Moves the G16 hybrid-static overlay coherence validator out of Features/Preflight/
to its domain home under the new Validation/ root concept. Registration moves
from AddPreflightFeature to AddValidators. Class shape unchanged.
```

After approval:

```pwsh
git add -A
git commit -m "refactor: relocate HybridStaticOverlayValidator to Validation/Packaging/"
```

---

> **Note on subsequent Phase 2 tasks (Tasks 5–9):** Each follows the same pattern as Task 4 — `git mv` production + test files first to preserve history, update namespaces and using directives, drop OneOf result wrappers in favor of direct domain `*Validation` returns, drop single-impl interfaces, relocate validation models to `Validation/Models/`, register in `AddValidators()`, drop registration from old feature ServiceCollectionExtensions, update PreflightPipeline + downstream consumers (PackagePipeline, ResolveVersionsFromExplicitTask) for type/namespace changes, build + test green, request commit approval.
>
> **Detailed step-by-step instructions for Tasks 5–9 are written out as separate plan extensions below — agents executing this slice should follow them strictly. Do NOT batch Tasks 5–9 into a single commit; each is its own approval gate.**

### Task 5: Move VersionConsistencyValidator → Validation/Manifest/ + drop OneOf return type + static→instance

(See spec §4.2 for shape; commit message: "refactor: relocate VersionConsistencyValidator to Validation/Manifest/, static→instance")

Detailed step list — execute in order:

1. `git mv build/_build/Features/Preflight/VersionConsistencyValidator.cs build/_build/Validation/Manifest/VersionConsistencyValidator.cs`
2. `git mv build/_build.Tests/Unit/Features/Preflight/VersionConsistencyValidatorTests.cs build/_build.Tests/Unit/Validation/Manifest/VersionConsistencyValidatorTests.cs`
3. `git mv build/_build.Tests/Unit/Features/Preflight/VersionConsistencyTests.cs build/_build.Tests/Unit/Validation/Manifest/VersionConsistencyTests.cs`
4. `git mv build/_build.Tests/Unit/Features/Preflight/SemanticVersionParsingTests.cs build/_build.Tests/Unit/Validation/Manifest/SemanticVersionParsingTests.cs`
5. Extract `LibraryVersionCheckStatus` + `LibraryVersionCheck` to `build/_build/Validation/Models/LibraryVersionCheck.cs` (namespace `Build.Validation.Models`).
6. Extract `VersionConsistencyValidation` (without `ManifestPath` / `VcpkgManifestPath` properties — D5) to `build/_build/Validation/Models/VersionConsistencyValidation.cs`.
7. Remove the extracted types from `build/_build/Features/Preflight/PreflightValidationModels.cs` (file still has CoreLibraryIdentity types — Task 6 finishes the split).
8. Rewrite the validator: `static class` → `sealed class`, namespace `Build.Validation.Manifest`, drop `manifestPath` + `vcpkgManifestPath` parameters from `Validate(...)`, return `VersionConsistencyValidation` directly (no OneOf).
9. Update test files: namespace, using directives, drop `VersionConsistencyResult.Pass/Fail` references, switch to instance construction `new VersionConsistencyValidator().Validate(...)`, drop `ManifestPath`/`VcpkgManifestPath` assertions.
10. Update `PreflightPipeline.cs`: ctor takes `VersionConsistencyValidator versionConsistencyValidator`, field added, body uses instance + `.HasErrors` check + direct `CakeException` throw.
11. Update `PreflightReporter.cs`: drop `validation.ManifestPath` / `validation.VcpkgManifestPath` log lines, replace with hardcoded "build/manifest.json" / "vcpkg.json" (D5).
12. Add `services.AddSingleton<VersionConsistencyValidator>();` to `Validation/ServiceCollectionExtensions.AddValidators()`.
13. Build + tests green. Commit with approval.

### Task 6: Move CoreLibraryIdentityValidator → Validation/Manifest/ + drop OneOf return type + static→instance

Steps:

1. `git mv build/_build/Features/Preflight/CoreLibraryIdentityValidator.cs build/_build/Validation/Manifest/CoreLibraryIdentityValidator.cs`
2. `git mv build/_build.Tests/Unit/Features/Preflight/CoreLibraryIdentityValidatorTests.cs build/_build.Tests/Unit/Validation/Manifest/CoreLibraryIdentityValidatorTests.cs`
3. Extract `CoreLibraryIdentityCheckStatus` + `CoreLibraryIdentityCheck` to `build/_build/Validation/Models/CoreLibraryIdentityCheck.cs`.
4. Extract `CoreLibraryIdentityValidation` to `build/_build/Validation/Models/CoreLibraryIdentityValidation.cs`.
5. Delete the now-empty `build/_build/Features/Preflight/PreflightValidationModels.cs` (`git rm`).
6. Rewrite validator: `static class` → `sealed class`, namespace `Build.Validation.Manifest`, return `CoreLibraryIdentityValidation` directly (no OneOf), self-contained XML doc (D7 code-doc sweep).
7. Update tests: namespace, using directives, instance construction, drop `CoreLibraryIdentityResult.Pass/Fail`, switch to `validation.HasErrors`.
8. Update `PreflightPipeline.cs`: ctor + field for `CoreLibraryIdentityValidator`, body uses instance + `.HasErrors` + `CakeException`.
9. Update `PreflightReporter.cs`: using directive (`Build.Validation.Models`).
10. Add `services.AddSingleton<CoreLibraryIdentityValidator>();` to `AddValidators()`.
11. Build + tests green. Commit with approval ("refactor: relocate CoreLibraryIdentityValidator to Validation/Manifest/, static→instance").

### Task 7: Move CsprojPackContractValidator → Validation/Manifest/ + drop interface + drop OneOf return + relocate models + relocate FamilyIdentifierConventions

Steps:

1. `git mv build/_build/Features/Preflight/CsprojPackContractValidator.cs build/_build/Validation/Manifest/CsprojPackContractValidator.cs`
2. `git mv build/_build/Features/Preflight/CsprojPackContractModels.cs build/_build/Validation/Models/CsprojPackContractModels.cs`
3. `git mv build/_build/Features/Preflight/FamilyIdentifierConventions.cs build/_build/Validation/Conventions/FamilyIdentifierConventions.cs`
4. `git mv` test files for both (`CsprojPackContractValidatorTests.cs` → `Validation/Manifest/`, `FamilyIdentifierConventionsTests.cs` → `Validation/Conventions/`).
5. `git rm build/_build/Features/Preflight/ICsprojPackContractValidator.cs`.
6. Update namespaces: `CsprojPackContractModels.cs` → `Build.Validation.Models`, `FamilyIdentifierConventions.cs` → `Build.Validation.Conventions`.
7. Rewrite validator: namespace `Build.Validation.Manifest`, drop `: ICsprojPackContractValidator`, return `CsprojPackContractValidation` directly (no OneOf), update using directives (`Build.Validation.Conventions`, `Build.Validation.Models`).
8. Update tests: namespace, using directives, drop interface references, drop `CsprojPackContractResult.Pass/Fail`, switch to `validation.HasErrors`.
9. Update `PreflightPipeline.cs`: ctor parameter type interface→concrete, body uses `.HasErrors` + `CakeException`.
10. Update `PreflightReporter.cs`: using directive (`Build.Validation.Models`).
11. Update `PackagePipeline.cs`: change `using Build.Features.Preflight;` (FamilyIdentifierConventions consumer) to `using Build.Validation.Conventions;`.
12. Drop interface registration from `Features/Preflight/ServiceCollectionExtensions.cs`.
13. Add `services.AddSingleton<CsprojPackContractValidator>();` to `AddValidators()`.
14. Build + tests green. Commit with approval ("refactor: relocate CsprojPackContractValidator + FamilyIdentifierConventions, drop interface").

### Task 8: Rename G58 → CrossFamilyDependencyResolvabilityValidator + relocate to Validation/Versioning/ + drop interface

Steps:

1. `git mv build/_build/Shared/Packaging/G58CrossFamilyDepResolvabilityValidator.cs build/_build/Validation/Versioning/CrossFamilyDependencyResolvabilityValidator.cs`
2. `git mv build/_build/Shared/Packaging/G58CrossFamilyCheckModels.cs build/_build/Validation/Models/CrossFamilyDependencyModels.cs`
3. `git rm build/_build/Shared/Packaging/IG58CrossFamilyDepResolvabilityValidator.cs`
4. `git mv build/_build.Tests/Unit/Features/Packaging/G58CrossFamilyDepResolvabilityValidatorTests.cs build/_build.Tests/Unit/Validation/Versioning/CrossFamilyDependencyResolvabilityValidatorTests.cs`
5. Rewrite `CrossFamilyDependencyModels.cs`: namespace `Build.Validation.Models`. Type renames: `G58CrossFamilyCheckStatus` → `CrossFamilyDependencyCheckStatus`, `G58CrossFamilyCheck` → `CrossFamilyDependencyCheck`, `G58CrossFamilyValidation` → `CrossFamilyDependencyValidation`. XML doc updated to refer to G58 as the release-guardrails ID (metadata position) rather than primary identity.
6. Rewrite validator: namespace `Build.Validation.Versioning`, class rename `G58CrossFamilyDepResolvabilityValidator` → `CrossFamilyDependencyResolvabilityValidator`, drop interface base. Internal type references renamed. Error message strings: replace `"G58: "` prefix with `"[G58] "` (bracketed metadata per ADR §10).
7. Update tests: namespace `Build.Tests.Unit.Validation.Versioning`, using directives (`Build.Validation.Versioning`, `Build.Validation.Models`), all `G58CrossFamily*` type renames in assertions, `"G58:"` literal → `"[G58]"` in message-content assertions.
8. Update `PreflightPipeline.cs`: namespace using, parameter/field rename interface→concrete + name (`g58CrossFamilyDepResolvabilityValidator` → `crossFamilyDependencyResolvabilityValidator`), local variable rename, reporter call rename (`ReportG58CrossFamilyResolvability` → `ReportCrossFamilyDependencyResolvability` — Step 10 renames the reporter method), CakeException message updated.
9. Update `PackagePipeline.cs`: namespace using, parameter/field rename, local rename, log message G58 prefix → bracketed, CakeException message updated.
10. Update `PreflightReporter.cs`: rename method `ReportG58CrossFamilyResolvability` → `ReportCrossFamilyDependencyResolvability`, parameter type rename, all log strings — replace `"(G58)"` and bare `"G58 "` prefixes with `"[G58]"` bracketed metadata, replace generic "G58 cross-family ..." phrases with "Cross-family dependency resolvability [G58] ...".
11. Drop G58 registration from `Features/Packaging/ServiceCollectionExtensions.cs`.
12. Add `services.AddSingleton<CrossFamilyDependencyResolvabilityValidator>();` to `AddValidators()`.
13. Build + tests green. Commit with approval ("refactor: rename G58 to CrossFamilyDependency*, relocate to Validation/Versioning/").

Note: dictionary boundary stays; Phase 5 swaps to `PackageFamilyVersionSet`.

### Task 9: Move UpstreamVersionAlignmentValidator → Validation/Versioning/ + drop interface + drop OneOf result type

Steps:

1. `git mv build/_build/Shared/Versioning/UpstreamVersionAlignmentValidator.cs build/_build/Validation/Versioning/UpstreamVersionAlignmentValidator.cs`
2. `git mv build/_build/Shared/Versioning/UpstreamVersionAlignmentValidation.cs build/_build/Validation/Models/UpstreamVersionAlignmentModels.cs`
3. `git rm build/_build/Shared/Versioning/IUpstreamVersionAlignmentValidator.cs`
4. `git rm build/_build/Shared/Versioning/UpstreamVersionAlignmentResult.cs`
5. If test exists: `git mv build/_build.Tests/Unit/Shared/Versioning/UpstreamVersionAlignmentValidatorTests.cs build/_build.Tests/Unit/Validation/Versioning/UpstreamVersionAlignmentValidatorTests.cs`
6. Update models file namespace to `Build.Validation.Models`.
7. Rewrite validator: namespace `Build.Validation.Versioning`, drop `: IUpstreamVersionAlignmentValidator` interface base, drop `using OneOf;`, both `Validate(...)` overloads return `UpstreamVersionAlignmentValidation` directly (no OneOf wrapper). Both overloads (`PackageFamilyVersionSet` + dict) preserved here; Phase 5 collapses to typed-only after PackagePipeline boundary swap.
8. Update test file: namespace `Build.Tests.Unit.Validation.Versioning`, using directives, drop interface references, drop `UpstreamVersionAlignmentResult.Pass/Fail`, switch assertions to `validation.HasErrors`.
9. Update `PreflightPipeline.cs`: namespace using, parameter/field interface→concrete, body switches to `.HasErrors` + `CakeException`.
10. Update `Targets/ResolveVersionsFromExplicit/ResolveVersionsFromExplicitTask.cs`: namespace using, parameter/field interface→concrete, `EnforceUpstreamVersionAlignment` body switches from `validationResult.IsError()` + `.Validation.Checks` to `validation.HasErrors` + `validation.Checks`.
11. Update `PreflightReporter.cs`: using directive update.
12. Drop registration from `Features/Preflight/ServiceCollectionExtensions.cs`.
13. Add `services.AddSingleton<UpstreamVersionAlignmentValidator>();` to `AddValidators()`.
14. Delete now-empty `build/_build/Shared/Versioning/` directory.
15. Build + tests green. Commit with approval ("refactor: relocate UpstreamVersionAlignmentValidator to Validation/Versioning/").

---

## Phase 3: PreFlightCheckTask migration (1 atomic commit)

### Task 10: Replace Features/Preflight/PreFlightCheckTask + PreflightPipeline with Targets/PreFlightCheck/ module

**This is a single atomic commit.** Cake `[TaskName("PreFlightCheck")]` cannot exist on two classes — the old task class must be deleted in the same commit that introduces the new one. PreflightPipeline + PreflightRequest + PreflightError are deleted in this commit.

**Files:**

- Create: `build/_build/Targets/PreFlightCheck/PreFlightCheckTask.cs`
- Create: `build/_build/Targets/PreFlightCheck/ServiceCollectionExtensions.cs`
- `git mv`: `build/_build/Features/Preflight/PreflightReporter.cs` → `build/_build/Targets/PreFlightCheck/Reporting/PreflightReporter.cs`
- `git rm`: `build/_build/Features/Preflight/PreFlightCheckTask.cs`
- `git rm`: `build/_build/Features/Preflight/PreflightPipeline.cs`
- `git rm`: `build/_build/Features/Preflight/PreflightRequest.cs`
- `git rm`: `build/_build/Features/Preflight/PreflightError.cs`
- `git rm`: `build/_build/Features/Preflight/ServiceCollectionExtensions.cs`
- Delete: `build/_build/Features/Preflight/` directory (empty after Phase 2 + this commit)
- Modify: `build/_build/Program.cs`
- Modify: `build/_build/Validation/ServiceCollectionExtensions.cs` (no change here — already complete from Phase 2)

- [ ] **Step 1: git mv PreflightReporter to new location**

```pwsh
git mv build/_build/Features/Preflight/PreflightReporter.cs build/_build/Targets/PreFlightCheck/Reporting/PreflightReporter.cs
```

- [ ] **Step 2: Update PreflightReporter content**

Read `build/_build/Targets/PreFlightCheck/Reporting/PreflightReporter.cs`. Apply changes:

- Namespace: `Build.Targets.PreFlightCheck.Reporting`
- Using directives: replace `Build.Shared.Packaging` and `Build.Shared.Versioning` with `Build.Validation.Models`. Add `Build.Results;`.
- `ReportRunStart` scope-line message: extend to mention all 7 validators ("version consistency + hybrid-static overlay coherence + core identity + manifest family name invariants + csproj pack contract + upstream version alignment + cross-family dependency resolvability").
- Add new method `ReportManifestFamilyNameInvariant(ValidationReport report)`:

```csharp
public void ReportManifestFamilyNameInvariant(ValidationReport report)
{
    ArgumentNullException.ThrowIfNull(report);

    Log.Information("");
    Log.Information("🔄 Checking manifest family name invariants [G59]...");

    foreach (var error in report.Errors)
    {
        Log.Error("  ❌ {0}", error.Message);
    }

    Log.Information("");
    if (!report.IsValid)
    {
        Log.Error("❌ Pre-flight check FAILED - {0} family name invariant violation(s) detected [G59]", report.Errors.Count);
        Log.Error("   All package_families[].name entries must be lowercase kebab-case matching 'sdl<major>-<role>'.");
        return;
    }

    Log.Information("✅ Manifest family name invariant check PASSED - all family names follow canonical convention");
}
```

- The `ReportVersionConsistency` method drops the `validation.ManifestPath` and `validation.VcpkgManifestPath` log lines (those properties were removed in Task 5). Replace with hardcoded labels:

```csharp
Log.Information("Checking manifest: build/manifest.json");
Log.Information("Checking vcpkg manifest: vcpkg.json");
```

- All G-number references in log strings remain as bracketed metadata (`[G16]`, `[G49]`, `[G54]`, `[G58]`, `[G59]`) — no bare-prefix `"G54: ..."` strings.

- [ ] **Step 3: Write the new PreFlightCheckTask**

Contents of `build/_build/Targets/PreFlightCheck/PreFlightCheckTask.cs`:

```csharp
using Build.Host;
using Build.Repositories;
using Build.Targets.PreFlightCheck.Reporting;
using Build.Validation.Manifest;
using Build.Validation.Packaging;
using Build.Validation.Versioning;
using Cake.Core;
using Cake.Frosting;

namespace Build.Targets.PreFlightCheck;

/// <summary>
/// Cross-cutting validation gate. Loads manifest + vcpkg manifest + resolved versions,
/// runs every build-host validator, reports findings, and throws once at the boundary
/// if any validator flags an error. Owns orchestration directly — no pipeline class.
/// </summary>
[TaskName("PreFlightCheck")]
[TaskDescription("Validates manifest+vcpkg consistency, hybrid-static overlay coherence, core identity, family name invariants, csproj pack contract, upstream version alignment, and cross-family dependency resolvability before any build operation.")]
public sealed class PreFlightCheckTask(
    IManifestRepository manifestRepository,
    IVcpkgManifestRepository vcpkgManifestRepository,
    IVersionFileRepository versionFileRepository,
    VersionConsistencyValidator versionConsistencyValidator,
    HybridStaticOverlayValidator hybridStaticOverlayValidator,
    CoreLibraryIdentityValidator coreLibraryIdentityValidator,
    ManifestFamilyNameInvariantValidator manifestFamilyNameInvariantValidator,
    CsprojPackContractValidator csprojPackContractValidator,
    UpstreamVersionAlignmentValidator upstreamVersionAlignmentValidator,
    CrossFamilyDependencyResolvabilityValidator crossFamilyDependencyResolvabilityValidator,
    PreflightReporter reporter)
    : AsyncFrostingTask<BuildContext>
{
    private readonly IManifestRepository _manifestRepository = manifestRepository ?? throw new ArgumentNullException(nameof(manifestRepository));
    private readonly IVcpkgManifestRepository _vcpkgManifestRepository = vcpkgManifestRepository ?? throw new ArgumentNullException(nameof(vcpkgManifestRepository));
    private readonly IVersionFileRepository _versionFileRepository = versionFileRepository ?? throw new ArgumentNullException(nameof(versionFileRepository));
    private readonly VersionConsistencyValidator _versionConsistencyValidator = versionConsistencyValidator ?? throw new ArgumentNullException(nameof(versionConsistencyValidator));
    private readonly HybridStaticOverlayValidator _hybridStaticOverlayValidator = hybridStaticOverlayValidator ?? throw new ArgumentNullException(nameof(hybridStaticOverlayValidator));
    private readonly CoreLibraryIdentityValidator _coreLibraryIdentityValidator = coreLibraryIdentityValidator ?? throw new ArgumentNullException(nameof(coreLibraryIdentityValidator));
    private readonly ManifestFamilyNameInvariantValidator _manifestFamilyNameInvariantValidator = manifestFamilyNameInvariantValidator ?? throw new ArgumentNullException(nameof(manifestFamilyNameInvariantValidator));
    private readonly CsprojPackContractValidator _csprojPackContractValidator = csprojPackContractValidator ?? throw new ArgumentNullException(nameof(csprojPackContractValidator));
    private readonly UpstreamVersionAlignmentValidator _upstreamVersionAlignmentValidator = upstreamVersionAlignmentValidator ?? throw new ArgumentNullException(nameof(upstreamVersionAlignmentValidator));
    private readonly CrossFamilyDependencyResolvabilityValidator _crossFamilyDependencyResolvabilityValidator = crossFamilyDependencyResolvabilityValidator ?? throw new ArgumentNullException(nameof(crossFamilyDependencyResolvabilityValidator));
    private readonly PreflightReporter _reporter = reporter ?? throw new ArgumentNullException(nameof(reporter));

    public override Task RunAsync(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.VersionsFilePath is null)
        {
            throw new CakeException(
                "PreFlightCheck requires --versions-file <path>. " +
                "Run --target ResolveVersionsFromManifest first to produce a versions.json, " +
                "then re-run with --versions-file artifacts/resolve-versions/versions.json.");
        }

        var manifest = _manifestRepository.Load();
        var vcpkgManifest = _vcpkgManifestRepository.Load();
        var versions = _versionFileRepository.Load(context.VersionsFilePath);

        if (versions.Count == 0)
        {
            throw new CakeException(
                "PreFlightCheck requires a non-empty version mapping. " +
                "Re-run --target ResolveVersionsFromManifest or --target ResolveVersionsFromExplicit.");
        }

        _reporter.ReportRunStart();

        var versionConsistency       = _versionConsistencyValidator.Validate(manifest, vcpkgManifest);
        var hybridStaticOverlay      = _hybridStaticOverlayValidator.Validate(manifest.Runtimes);
        var coreLibraryIdentity      = _coreLibraryIdentityValidator.Validate(manifest);
        var manifestFamilyName       = _manifestFamilyNameInvariantValidator.Validate(manifest);
        var csprojPackContract       = _csprojPackContractValidator.Validate(manifest, context.Paths.RepoRoot);
        var upstreamVersionAlignment = _upstreamVersionAlignmentValidator.Validate(manifest, versions);
        var crossFamilyDependency    = _crossFamilyDependencyResolvabilityValidator.Validate(versions, manifest);

        _reporter.ReportVersionConsistency(versionConsistency);
        _reporter.ReportHybridStaticOverlay(hybridStaticOverlay);
        _reporter.ReportCoreLibraryIdentity(coreLibraryIdentity);
        _reporter.ReportManifestFamilyNameInvariant(manifestFamilyName);
        _reporter.ReportCsprojPackContract(csprojPackContract);
        _reporter.ReportUpstreamVersionAlignment(upstreamVersionAlignment);
        _reporter.ReportCrossFamilyDependencyResolvability(crossFamilyDependency);

        var fatal =
            versionConsistency.HasErrors
            || !hybridStaticOverlay.IsValid
            || coreLibraryIdentity.HasErrors
            || !manifestFamilyName.IsValid
            || csprojPackContract.HasErrors
            || upstreamVersionAlignment.HasErrors
            || crossFamilyDependency.HasErrors;

        if (fatal)
        {
            throw new CakeException(
                "Pre-flight check failed. Review the errors above and fix manifest.json / vcpkg.json / csproj files.");
        }

        return Task.CompletedTask;
    }
}
```

**Note on `_upstreamVersionAlignmentValidator.Validate(manifest, versions)`:** at this point in the slice, the validator still has both overloads (typed `PackageFamilyVersionSet` + dict). The task uses the typed overload directly. Phase 5 (Task 11) collapses to typed-only.

- [ ] **Step 4: Write the target's ServiceCollectionExtensions**

Contents of `build/_build/Targets/PreFlightCheck/ServiceCollectionExtensions.cs`:

```csharp
using Build.Targets.PreFlightCheck.Reporting;
using Microsoft.Extensions.DependencyInjection;

namespace Build.Targets.PreFlightCheck;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPreFlightCheck(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<PreflightReporter>();
        return services;
    }
}
```

`PreFlightCheckTask` is NOT registered — Cake Frosting discovers it via `[TaskName]`. Validators are registered by `AddValidators()` (already wired by Phase 2).

- [ ] **Step 5: git rm the old files**

```pwsh
git rm build/_build/Features/Preflight/PreFlightCheckTask.cs
git rm build/_build/Features/Preflight/PreflightPipeline.cs
git rm build/_build/Features/Preflight/PreflightRequest.cs
git rm build/_build/Features/Preflight/PreflightError.cs
git rm build/_build/Features/Preflight/ServiceCollectionExtensions.cs
```

- [ ] **Step 6: Verify Features/Preflight/ is empty and delete the directory**

```pwsh
ls build/_build/Features/Preflight/
```

Expected output: empty. Then on filesystem (git tracks files, not directories — directory deletion happens automatically when last file is removed):

```pwsh
Remove-Item -Recurse -Force build/_build/Features/Preflight/ -ErrorAction SilentlyContinue
```

- [ ] **Step 7: Update Program.cs**

Read `build/_build/Program.cs`. Find:

```csharp
using Build.Features.Preflight;
```

Replace with:

```csharp
using Build.Targets.PreFlightCheck;
```

Find:

```csharp
.AddPreflightFeature()
```

Replace with:

```csharp
.AddPreFlightCheck()
```

(The `AddValidators()` call from Task 1 is already in place.)

- [ ] **Step 8: Build + tests**

```pwsh
dotnet build build/_build/Build.csproj
dotnet test --project build/_build.Tests/Build.Tests.csproj -c Release --framework net10.0
```

Expected: 0 warnings, 0 errors. Old V1 `Unit/Features/Preflight/PreFlightCheckTaskRunTests.cs` will likely fail compilation because `PreFlightCheckTask` ctor signature changed dramatically. **Expected red test compilation here is OK — Phase 6 Task 13 deletes that V1 test and replaces with V2 scenario.** Do not commit yet.

If the build itself errors, fix imports/refs; if only test compilation errors block, proceed to Step 9.

- [ ] **Step 9: Temporarily quarantine V1 PreFlightCheck test (compile-fix only)**

To keep the slice committable per-step, temporarily delete the old V1 test:

```pwsh
git rm build/_build.Tests/Unit/Features/Preflight/PreFlightCheckTaskRunTests.cs
git rm build/_build.Tests/Unit/Features/Preflight/PreflightRequestTests.cs
```

These tests are formally retired in Phase 6 Task 13/14; deleting now keeps the build green between phases. The replacement V2 scenario tests are added in Phase 6 Task 14.

- [ ] **Step 10: Build + tests green**

```pwsh
dotnet build build/_build/Build.csproj
dotnet test --project build/_build.Tests/Build.Tests.csproj -c Release --framework net10.0
```

Expected: 0 warnings, 0 errors. Test count drops by ~deletion count (was 540 + Phase 2 adjustments; minus 2-N V1 PreFlight tests).

- [ ] **Step 11: Run PreFlightCheck target end-to-end**

First produce a versions file:

```pwsh
dotnet run --project build/_build -- --target ResolveVersionsFromManifest --versions-file artifacts/resolve-versions/versions.json --suffix p6.test
```

Then run PreFlight:

```pwsh
dotnet run --project build/_build -- --target PreFlightCheck --versions-file artifacts/resolve-versions/versions.json
```

Expected: all 7 validators report PASS. No `PreflightPipeline` mention in logs (the pipeline is gone).

- [ ] **Step 12: Commit (request approval)**

Commit message preview:

```text
refactor: migrate PreFlightCheck to Targets/PreFlightCheck/, retire PreflightPipeline

PreFlightCheckTask now owns orchestration directly: loads manifest + vcpkg
manifest + versions via repositories, runs 7 validators consumed from
AddValidators(), throws once if any flag fatal. PreflightPipeline,
PreflightRequest, PreflightError, and the old task class are deleted.
PreflightReporter relocates under Reporting/ with a new
ReportManifestFamilyNameInvariant method and updated [G##] bracketed
metadata in log strings.

V1 PreFlightCheckTaskRunTests + PreflightRequestTests deleted; V2
scenario tests land in Phase 6.
```

After approval:

```pwsh
git add -A
git commit -m "refactor: migrate PreFlightCheck to Targets/PreFlightCheck/, retire PreflightPipeline"
```

---

## Phase 4: Universal PackageFamilyVersionSet boundary (1 atomic mega-commit)

### Task 11: Retire IReadOnlyDictionary<string, NuGetVersion> from every task/service boundary

**This is a single atomic commit.** Type swaps cascade through `PackageBuildConfiguration` → `Program.cs` factory → 4 tasks → 3 pipelines → 2 validators. Splitting any of these into separate commits leaves the build broken between commits, which violates the "every commit builds green" baseline.

**Files modified:**

- `build/_build/Host/Configuration/PackageBuildConfiguration.cs` — ctor + property
- `build/_build/Program.cs` — composition root factory
- `build/_build/Validation/Versioning/UpstreamVersionAlignmentValidator.cs` — drop dict overload
- `build/_build/Validation/Versioning/CrossFamilyDependencyResolvabilityValidator.cs` — typed signature, internal lookups
- `build/_build/Validation/Models/UpstreamVersionAlignmentModels.cs` — XML doc cleanup
- `build/_build/Versioning/PackageFamilyVersionSet.cs` — XML doc cleanup
- `build/_build/Features/Packaging/PackagePipeline.cs` — boundary type swap, internal lookups
- `build/_build/Features/Packaging/PackageRequest.cs` (or wherever `PackRequest` lives — verify) — typed `Versions` property
- `build/_build/Features/Packaging/PackageTask.cs` — guard + request construction
- `build/_build/Features/Packaging/PackageConsumerSmokePipeline.cs` — boundary type swap
- `build/_build/Features/Packaging/PackageConsumerSmokeTask.cs` — guard + boundary
- `build/_build/Features/Publishing/PublishStagingPipeline.cs` — boundary type swap
- `build/_build/Features/Publishing/PublishStagingTask.cs` — guard + boundary
- `build/_build/Features/Publishing/PublishStagingRequest.cs` — typed `Versions`

- [ ] **Step 1: Update PackageBuildConfiguration**

Read `build/_build/Host/Configuration/PackageBuildConfiguration.cs`. Replace with:

```csharp
using Build.Versioning;

namespace Build.Host.Configuration;

public sealed class PackageBuildConfiguration(PackageFamilyVersionSet familyVersions)
{
    public PackageFamilyVersionSet FamilyVersions { get; } =
        familyVersions ?? throw new ArgumentNullException(nameof(familyVersions));
}
```

Note the rename `FamilyVersionMapping` → `FamilyVersions` (the `Mapping` suffix was dict-implying).

- [ ] **Step 2: Update Program.cs composition factory**

Read `build/_build/Program.cs`. Find the `PackageBuildConfiguration` factory (the lambda registered via `services.AddSingleton<PackageBuildConfiguration>(...)` or similar). The current factory parses `--versions-file` into a `Dictionary<string, NuGetVersion>`. Replace it to produce a `PackageFamilyVersionSet` instead, using the existing `IVersionFileRepository` if path is supplied (or empty set otherwise).

Replacement factory shape:

```csharp
services.AddSingleton<PackageBuildConfiguration>(sp =>
{
    var ctx = sp.GetRequiredService<ICakeContext>();
    var args = sp.GetRequiredService<ParsedArguments>();

    if (string.IsNullOrWhiteSpace(args.VersionsFile))
    {
        return new PackageBuildConfiguration(PackageFamilyVersionSet.Empty);
    }

    var path = new FilePath(args.VersionsFile);
    if (!ctx.FileExists(path))
    {
        return new PackageBuildConfiguration(PackageFamilyVersionSet.Empty);
    }

    var repo = sp.GetRequiredService<IVersionFileRepository>();
    return new PackageBuildConfiguration(repo.Load(path));
});
```

Existing imports may need `using Build.Versioning;` and `using Build.Repositories;`. The fall-back to `Empty` mirrors the previous "empty dict if no file" behavior — stage tasks (PreFlight, Package, Smoke, Publish) still fail-loud at task entry when `versions.Count == 0`.

- [ ] **Step 3: Update UpstreamVersionAlignmentValidator (drop dict overload)**

Read `build/_build/Validation/Versioning/UpstreamVersionAlignmentValidator.cs`. Delete the dict-shaped `Validate(ManifestConfig, IReadOnlyDictionary<string, NuGetVersion>)` overload entirely. The single remaining `Validate(ManifestConfig, PackageFamilyVersionSet)` becomes the implementation directly (was previously a thin wrapper that built a dict and called the dict overload).

New shape:

```csharp
using Build.Shared.Manifest;
using Build.Validation.Models;
using Build.Versioning;
using NuGet.Versioning;

namespace Build.Validation.Versioning;

/// <summary>
/// G54: every entry in the resolved per-family version set must align with the family's
/// upstream library major/minor from <c>manifest.json library_manifests[].vcpkg_version</c>.
/// Strict-minor alignment applies unconditionally — each set entry is an explicit
/// per-family assertion. Invoked by PreFlightCheckTask and ResolveVersionsFromExplicitTask.
/// </summary>
public sealed class UpstreamVersionAlignmentValidator
{
    public UpstreamVersionAlignmentValidation Validate(ManifestConfig manifestConfig, PackageFamilyVersionSet versions)
    {
        ArgumentNullException.ThrowIfNull(manifestConfig);
        ArgumentNullException.ThrowIfNull(versions);

        var checks = new List<UpstreamVersionAlignmentCheck>(versions.Count);
        checks.AddRange(ValidateUniqueManifestKeys(manifestConfig));

        if (checks.Count > 0)
        {
            return new UpstreamVersionAlignmentValidation(checks);
        }

        foreach (var entry in versions)
        {
            checks.Add(ValidateEntry(manifestConfig, entry.Family.Value, entry.Version));
        }

        return new UpstreamVersionAlignmentValidation(checks);
    }

    // ValidateUniqueManifestKeys + ValidateEntry stay unchanged — they take (ManifestConfig, string, NuGetVersion).
}
```

- [ ] **Step 4: Update CrossFamilyDependencyResolvabilityValidator (typed signature + internal lookups)**

Read `build/_build/Validation/Versioning/CrossFamilyDependencyResolvabilityValidator.cs`. Replace the signature and internal logic:

```csharp
using Build.Shared.Manifest;
using Build.Validation.Models;
using Build.Versioning;
using NuGet.Versioning;

namespace Build.Validation.Versioning;

public sealed class CrossFamilyDependencyResolvabilityValidator
{
    public CrossFamilyDependencyValidation Validate(
        PackageFamilyVersionSet versions,
        ManifestConfig manifest)
    {
        ArgumentNullException.ThrowIfNull(versions);
        ArgumentNullException.ThrowIfNull(manifest);

        var checks = new List<CrossFamilyDependencyCheck>();

        foreach (var entry in versions)
        {
            var dependentFamilyName = entry.Family.Value;
            var dependentVersion = entry.Version;

            var dependentFamily = manifest.PackageFamilies.SingleOrDefault(family =>
                string.Equals(family.Name, dependentFamilyName, StringComparison.OrdinalIgnoreCase));

            if (dependentFamily is null)
            {
                checks.Add(new CrossFamilyDependencyCheck(
                    DependentFamily: dependentFamilyName,
                    DependencyFamily: dependentFamilyName,
                    ExpectedMinVersion: dependentVersion.ToNormalizedString(),
                    Status: CrossFamilyDependencyCheckStatus.Missing,
                    ErrorMessage:
                    $"[G58] family '{dependentFamilyName}' is in the resolved version set but not declared in manifest.json package_families[]. " +
                    "Either the set is malformed (rerun ResolveVersions) or the manifest is missing this family."));
                continue;
            }

            if (dependentFamily.DependsOn is null || dependentFamily.DependsOn.Count == 0)
            {
                continue;
            }

            foreach (var dependencyName in dependentFamily.DependsOn)
            {
                checks.Add(EvaluateDependency(
                    dependentFamilyName: dependentFamily.Name,
                    dependencyFamilyName: dependencyName,
                    expectedMinVersion: dependentVersion.ToNormalizedString(),
                    versions: versions));
            }
        }

        return new CrossFamilyDependencyValidation(checks);
    }

    private static CrossFamilyDependencyCheck EvaluateDependency(
        string dependentFamilyName,
        string dependencyFamilyName,
        string expectedMinVersion,
        PackageFamilyVersionSet versions)
    {
        var dependencyFamilyId = new PackageFamilyId(dependencyFamilyName);
        if (versions.Contains(dependencyFamilyId))
        {
            return new CrossFamilyDependencyCheck(
                DependentFamily: dependentFamilyName,
                DependencyFamily: dependencyFamilyName,
                ExpectedMinVersion: expectedMinVersion,
                Status: CrossFamilyDependencyCheckStatus.InScope,
                ErrorMessage: null);
        }

        return new CrossFamilyDependencyCheck(
            DependentFamily: dependentFamilyName,
            DependencyFamily: dependencyFamilyName,
            ExpectedMinVersion: expectedMinVersion,
            Status: CrossFamilyDependencyCheckStatus.Missing,
            ErrorMessage:
            $"[G58] family '{dependentFamilyName}' declares cross-family dependency on '{dependencyFamilyName}', " +
            $"but '{dependencyFamilyName}' is not in the resolved version set (scope). " +
            "Either include it in --explicit-version / --scope, or wire --feed <URL> for the Pack stage to probe the target feed for an already-published version " +
            "satisfying the lower bound (feed-probe surface is reserved for a later slice).");
    }
}
```

Note `PackageFamilyId(dependencyFamilyName)` allocates per check — acceptable; PackagePipeline G58 invocation runs once per pack invocation, not in a hot loop. If concerned, lift to a `HashSet<PackageFamilyId>` in `Validate(...)` upfront — leave as-is for clarity unless a profile flags it.

- [ ] **Step 5: Update XML doc on PackageFamilyVersionSet.cs and UpstreamVersionAlignmentModels.cs**

Read `build/_build/Versioning/PackageFamilyVersionSet.cs`. Update XML doc (around line 14-20):

```csharp
/// <summary>
/// Typed family→version mapping. Used at every task/service boundary in the build host.
/// Immutable; two sets with the same contents are equal regardless of input order.
/// Throws on duplicate family at construction.
/// </summary>
```

(Drops the "Replaces raw IReadOnlyDictionary..." historical framing — D7 doc sweep.)

Read `build/_build/Validation/Models/UpstreamVersionAlignmentModels.cs`. Update the file-level XML doc / comments to drop dict-shape historical language.

- [ ] **Step 6: Update PackagePipeline boundary**

Read `build/_build/Features/Packaging/PackagePipeline.cs`. Apply changes:

- Add `using Build.Versioning;`
- `PackRequest` (defined here or in a separate file — locate via `grep`):

```csharp
public sealed record PackRequest(PackageFamilyVersionSet Versions);
```

- `RunAsync(PackRequest request, CancellationToken ct = default)`:
  - `var explicitVersions = request.Versions;` (now `PackageFamilyVersionSet`)
  - `if (explicitVersions.Count == 0) { ... }` (already works on the typed set)
  - G58 call: `var crossFamilyValidation = _crossFamilyDependencyResolvabilityValidator.Validate(explicitVersions, _manifestConfig);` — typed signature
  - Error log message: `"[G58] {0} → {1}: {2}"` (bracketed metadata)
  - Throw message updated: replace `"G58 cross-family"` with `"cross-family dependency"` (behavior-first per ADR §10)

- `ResolveSelectedFamilies(PackageFamilyVersionSet explicitVersions)`:

```csharp
private IReadOnlyList<PackageFamilyConfig> ResolveSelectedFamilies(PackageFamilyVersionSet explicitVersions)
{
    var selectedFamilies = new List<PackageFamilyConfig>(explicitVersions.Count);

    foreach (var entry in explicitVersions)
    {
        var requestedFamily = entry.Family.Value;
        var family = _manifestConfig.PackageFamilies.SingleOrDefault(candidate =>
            string.Equals(candidate.Name, requestedFamily, StringComparison.OrdinalIgnoreCase));

        if (family is null)
        {
            throw new CakeException(
                $"Package task received unknown family '{requestedFamily}'. Add it to build/manifest.json package_families[] or fix the CLI value.");
        }

        if (string.IsNullOrWhiteSpace(family.ManagedProject) || string.IsNullOrWhiteSpace(family.NativeProject))
        {
            throw new CakeException(
                $"Package task cannot pack family '{family.Name}' yet because manifest.json does not declare both managed_project and native_project. This usually means the family is still a placeholder.");
        }

        selectedFamilies.Add(family);
    }

    if (!FamilyTopologyHelpers.TryOrderByDependencies(selectedFamilies, out var orderedFamilies, out var errorMessage))
    {
        throw new CakeException(errorMessage);
    }

    return orderedFamilies;
}
```

- `PackFamilyAsync` per-family loop in `RunAsync`: replace `var familyVersion = explicitVersions[family.Name].ToNormalizedString();` with:

```csharp
var familyVersion = explicitVersions.RequireVersion(new PackageFamilyId(family.Name)).ToNormalizedString();
```

- [ ] **Step 7: Update PackageTask**

Read `build/_build/Features/Packaging/PackageTask.cs`. Apply changes:

- Replace `_packageBuildConfiguration.FamilyVersionMapping.Count == 0` with `_packageBuildConfiguration.FamilyVersions.Count == 0`
- Replace `new PackRequest(_packageBuildConfiguration.FamilyVersionMapping)` with `new PackRequest(_packageBuildConfiguration.FamilyVersions)`

- [ ] **Step 8: Update PackageConsumerSmokePipeline + PackageConsumerSmokeTask**

`PackageConsumerSmokePipeline.cs`:

- Locate the request shape (`PackageConsumerSmokeRequest` or similar). Update its `Versions` property to `PackageFamilyVersionSet`.
- Internal usage: replace any `versions[familyName]` lookups with `versions.RequireVersion(new PackageFamilyId(familyName))`. Replace `versions.Keys` enumeration with `versions.Families` (or direct `foreach (var entry in versions)`).
- Replace `Dictionary<string, NuGetVersion>` local declarations with typed equivalents where possible; if internal orchestration constructs a working dict, keep the local dict — only the boundary types are mandated typed. (The internal-implementation freedom is open question §17 in the spec; defer to the second pipeline-internal cleanup phase.)

`PackageConsumerSmokeTask.cs`:

- `_packageBuildConfiguration.FamilyVersionMapping` → `_packageBuildConfiguration.FamilyVersions`
- Request construction: pass `PackageFamilyVersionSet` instead of dict.

- [ ] **Step 9: Update PublishStagingPipeline + PublishStagingTask + PublishStagingRequest**

Same pattern as Step 8: locate request type, swap `Versions` to `PackageFamilyVersionSet`, update the task's guard + request construction, update pipeline internals minimally to match the new boundary type. Internal Dictionary construction is permitted; only boundary types are mandated typed.

- [ ] **Step 10: Verify zero IReadOnlyDictionary<string, NuGetVersion> in production code**

```pwsh
grep -r "IReadOnlyDictionary<string, NuGetVersion>" build/_build --include "*.cs"
```

Expected: only the XML doc comment in `PackageFamilyVersionSet.cs` may match if the doc sweep missed it. Otherwise empty result. If any production code still references the type, fix and re-grep.

```pwsh
grep -r "Dictionary<string, NuGetVersion>" build/_build --include "*.cs"
```

Internal `Dictionary<string, NuGetVersion>` declarations inside pipeline orchestration are permitted (open question §17). The hard rule is on PUBLIC boundaries (parameters / return types / public properties) — internal helpers may still construct dicts.

- [ ] **Step 11: Build + tests**

```pwsh
dotnet build build/_build/Build.csproj
dotnet test --project build/_build.Tests/Build.Tests.csproj -c Release --framework net10.0
```

Expected: 0 warnings, 0 errors. Tests may have V1 setup that constructs dicts — those compile errors land in Phase 6 V2 migration. If compile errors block, deal with them in Phase 6 Tasks 13-14; for now, ensure production code (`build/_build/`) is green.

If V1 test compile errors block the test pass, add per-test V1→V2 migrations inline as part of Phase 6. For Phase 4 commit boundary, the criterion is **production build green** + **previously-passing tests still pass once their V1 dict-construction is migrated** (Phase 6 deliverable).

Practical: Phase 6 Tasks 13-14 are the immediate next step after this commit, and the slice does not push between them. So momentary test compile failures between Phase 4 commit and Phase 6 commit are acceptable.

- [ ] **Step 12: PreFlight + ResolveVersions end-to-end smoke**

```pwsh
dotnet run --project build/_build -- --target ResolveVersionsFromManifest --versions-file artifacts/resolve-versions/versions.json --suffix p6.test
dotnet run --project build/_build -- --target PreFlightCheck --versions-file artifacts/resolve-versions/versions.json
```

Expected: both targets succeed; PreFlight reports all green.

- [ ] **Step 13: Commit (request approval)**

Commit message preview:

```text
refactor: retire IReadOnlyDictionary<string,NuGetVersion> from all task/service boundaries

PackageBuildConfiguration.FamilyVersions: PackageFamilyVersionSet replaces the
dict-shaped FamilyVersionMapping. Program.cs composition factory builds a typed
set via IVersionFileRepository. UpstreamVersionAlignmentValidator and
CrossFamilyDependencyResolvabilityValidator collapse to single typed signatures.
PackagePipeline + PackageConsumerSmokePipeline + PublishStagingPipeline boundary
types swap to PackageFamilyVersionSet (internal orchestration unchanged — full
internal refactor is P7/P9 boss-fight scope).

V1 test setups that constructed Dictionary<string,NuGetVersion> directly may have
broken compilation; Phase 6 V2 migration tasks fix those. Production build is
0 warnings, 0 errors.

Closes the ADR-002 §6 / review-checklist §6 hard rule "Version APIs avoid raw
IReadOnlyDictionary<string,NuGetVersion> boundaries" across the entire build host.
```

After approval:

```pwsh
git add -A
git commit -m "refactor: retire IReadOnlyDictionary<string,NuGetVersion> from all task/service boundaries"
```

---

## Phase 5: OneOf result type cleanup (1 commit)

### Task 12: Delete 5 OneOf result types now unused

After Phase 2 + 3 + 4, the 5 OneOf result types have zero production callers (validators return `*Validation` directly; `PreflightPipeline` is gone).

**Files:**

- `git rm build/_build/Features/Preflight/VersionConsistencyResult.cs` (already-empty Features/Preflight/ post-Phase 3 — verify)
- `git rm build/_build/Features/Preflight/CoreLibraryIdentityResult.cs`
- `git rm build/_build/Features/Preflight/CsprojPackContractResult.cs`
- (`PreflightError.cs` already deleted in Phase 3 Task 10)
- `git rm build/_build/Shared/Versioning/UpstreamVersionAlignmentResult.cs` (already-empty Shared/Versioning/ post-Phase 2 — verify)

Wait — Tasks 5-9 already had these listed as "drop OneOf result type" inline. **Re-verify:** if the Phase 2 tasks correctly deleted these files inline, this phase is a no-op confirmation. If any file survived (because the Phase 2 task author kept it for "later"), this phase finishes the job.

- [ ] **Step 1: Audit for surviving OneOf result types**

```pwsh
ls build/_build/Features/Preflight/ -ErrorAction SilentlyContinue
ls build/_build/Shared/Versioning/ -ErrorAction SilentlyContinue
ls build/_build/Shared/Packaging/G58* -ErrorAction SilentlyContinue
```

Expected: all empty / not found. If any files survive, audit them — they're either OneOf result types that should be deleted, or surviving consumers (which means a Phase 2 task didn't finish cleanly — return to fix it before this phase).

- [ ] **Step 2: Grep for orphan OneOf consumers**

```pwsh
grep -rn "VersionConsistencyResult\|CoreLibraryIdentityResult\|CsprojPackContractResult\|UpstreamVersionAlignmentResult\|PreflightError" build/_build --include "*.cs"
```

Expected: no matches. If any match, those references are orphans and should be cleaned up (likely XML doc comments — drop them).

- [ ] **Step 3: Build + tests**

```pwsh
dotnet build build/_build/Build.csproj
dotnet test --project build/_build.Tests/Build.Tests.csproj -c Release --framework net10.0
```

Expected: 0 warnings, 0 errors. (Tests may still be in transition from Phase 4; that's fine — see Phase 6.)

- [ ] **Step 4: Verify directories deleted**

```pwsh
Remove-Item -Recurse -Force build/_build/Features/Preflight -ErrorAction SilentlyContinue
Remove-Item -Recurse -Force build/_build/Shared/Versioning -ErrorAction SilentlyContinue
```

Both directories should now not exist. `Shared/Packaging/` STAYS — it still has `DotNetPack*`, `PackagingError`, `ProjectMetadata*` (P7/P10 concern).

- [ ] **Step 5: Commit (request approval, only if any actual deletions happened)**

If Step 1 confirms zero remaining OneOf result type files, this task is a no-op — do NOT create an empty commit. Simply note in the slice summary that the OneOf cleanup completed inline during Phase 2/3/4 and proceed to Phase 6.

If any files were deleted in Step 1, commit message preview:

```text
refactor: confirm OneOf result type retirement (parking-lot 11 → 6)

Cleans up any OneOf result types that survived Phase 2-4 refactors. Result:
no `VersionConsistencyResult`, `CoreLibraryIdentityResult`,
`CsprojPackContractResult`, `UpstreamVersionAlignmentResult`, or
`PreflightError` references in production code.
```

After approval:

```pwsh
git add -A
git commit -m "refactor: confirm OneOf result type retirement (parking-lot 11 → 6)"
```

---

## Phase 6: V2 test migration + scenario tests (2 commits)

### Task 13: Migrate touched test files to V2 + fix Phase 4 fallout

After Phase 4 dict retirement, V1 test setups that constructed `Dictionary<string, NuGetVersion>` directly are broken. This task migrates every touched test file to V2 per testing-guidelines.md altın kural.

**Inventory of touched test files:**

Production-side touched (Phase 1-5) — tests must compile against new namespaces / types / DI shape:

- `Unit/Features/Packaging/PackagePipelineTests.cs` — boundary type swap, V1 fixtures replaced
- `Unit/Features/Packaging/PackageConsumerSmokePipelineTests.cs` — boundary type swap
- `Unit/Features/Publishing/PublishStagingPipelineTests.cs` — boundary type swap
- `Unit/Features/Packaging/PackageOutputValidatorTests.cs` — likely touched if it constructs versions; verify during execution
- `Unit/CompositionRoot/ProgramCompositionRootTests.cs` — DI shape change for `AddValidators()` / `AddPreFlightCheck()` rename / `AddPackagingFeature` G58 registration removal
- `Unit/CompositionRoot/ServiceCollectionExtensionsSmokeTests.cs` — same DI shape change
- `Characterization/ConfigContract/ManifestDeserializationTests.cs` — G58 type names appear in assertions
- `Scenarios/ResolveVersionsFromExplicit/ResolveVersionsFromExplicitTaskScenarios.cs` — `IUpstreamVersionAlignmentValidator` interface drop

**Inventory of orphan V1 PreFlight tests already deleted in Phase 3 Task 10:**

- `Unit/Features/Preflight/PreFlightCheckTaskRunTests.cs` (deleted in Step 9 of Task 10 to keep build green)
- `Unit/Features/Preflight/PreflightRequestTests.cs` (deleted)

Replacement V2 scenario tests come in Task 14.

- [ ] **Step 1: Audit V1 fixture usage in touched test files**

```pwsh
grep -rln "FakeRepoBuilder\|TestHostFixture\|FakeCakeToolContextBuilder\|ToLegacyBuildContext" build/_build.Tests --include "*.cs"
```

Note: existing `ResolveVersionsFromExplicitTaskScenarios.cs` may already be on V2; verify.

For each file in the touched list, decide:

- **Already V2** → just update namespaces/types for compile fix; no infra migration.
- **Still V1** → migrate to `FakeCakeWorldV2` + `TargetTestHostV2<TTask>` + `TestLogV2` per `docs/refactoring/testing-guidelines.md` §"V2 vs V1 infrastructure".

- [ ] **Step 2: Per touched test file, apply changes**

Generic V1→V2 migration template (for tests that need infra change):

Before (V1):

```csharp
[Test]
public async Task SomeTest()
{
    using var fakeRepo = new FakeRepoBuilder().WithDefaults().Build();
    using var host = new TestHostFixture(fakeRepo, services => services.AddX());
    var task = host.GetService<XTask>();
    // ...
}
```

After (V2):

```csharp
[Test]
public async Task SomeTest()
{
    var world = FakeCakeWorldV2.CreateWindows()
        .WithManifestObject(ManifestFixture.CreateTestManifestConfig())
        .WithTextFile("artifacts/resolve-versions/versions.json", VersionsData.Valid)
        .WithVersionsFile("artifacts/resolve-versions/versions.json");

    var result = await new TargetTestHostV2<XTask>(world)
        .WithServices(services => services.AddX())
        .RunAsync();

    await Assert.That(result.Success).IsTrue();
}
```

For tests that previously constructed `Dictionary<string, NuGetVersion>` directly:

Before:

```csharp
var versions = new Dictionary<string, NuGetVersion>(StringComparer.OrdinalIgnoreCase)
{
    ["sdl2-core"] = NuGetVersion.Parse("2.32.0"),
    ["sdl2-image"] = NuGetVersion.Parse("2.8.0"),
};
var config = new PackageBuildConfiguration(versions);
```

After:

```csharp
var versions = new PackageFamilyVersionSet([
    new PackageFamilyVersion(new PackageFamilyId("sdl2-core"), NuGetVersion.Parse("2.32.0")),
    new PackageFamilyVersion(new PackageFamilyId("sdl2-image"), NuGetVersion.Parse("2.8.0")),
]);
var config = new PackageBuildConfiguration(versions);
```

For tests that previously asserted on `IG58CrossFamilyDepResolvabilityValidator` mock or interface type, switch to `CrossFamilyDependencyResolvabilityValidator` concrete (`Substitute.For<...>` won't work on sealed class — replace with real construction or use NSubstitute's `Substitute.ForPartsOf<...>` only if mocking is genuinely needed; preferred approach is real construction since the validator is pure).

For tests asserting on `ICsprojPackContractValidator` mock — replace with concrete or refactor to use real `CsprojPackContractValidator(IFileSystem)` against `world.CakeContext.FileSystem`.

For tests asserting on G58-prefixed type names (`G58CrossFamilyValidation`, `G58CrossFamilyCheck`, `G58CrossFamilyCheckStatus`) — rename to `CrossFamilyDependency*`.

For `ResolveVersionsFromExplicitTaskScenarios.cs`:

- `using Build.Shared.Versioning;` → `using Build.Validation.Versioning;`
- Constructor parameter type: `IUpstreamVersionAlignmentValidator` → `UpstreamVersionAlignmentValidator`
- Mock construction: `Substitute.For<IUpstreamVersionAlignmentValidator>()` → real construction `new UpstreamVersionAlignmentValidator()` (the validator is pure, no DI deps).

For `ProgramCompositionRootTests.cs`:

- Add `services.AddValidators()` to expected DI registration assertions.
- Drop `services.AddPreflightFeature()` (renamed `AddPreFlightCheck`).
- Update existing test that previously asserted `IUpstreamVersionAlignmentValidator` registration → assert `UpstreamVersionAlignmentValidator` (concrete) registration.
- Drop assertion on `IG58CrossFamilyDepResolvabilityValidator` registration; add assertion on `CrossFamilyDependencyResolvabilityValidator` (concrete) via `AddValidators`.
- Add assertion on `IVcpkgManifestRepository` registration via `AddRepositories`.

For `ServiceCollectionExtensionsSmokeTests.cs`:

- Drop `AddPreflightFeature` smoke test (already done in S11 — verify).
- Add `AddValidators` smoke test exercising the new extension method.

For `ManifestDeserializationTests.cs` Characterization:

- Update any assertions referencing `G58CrossFamily*` to `CrossFamilyDependency*` (this file likely doesn't reference these; verify via `grep`).

- [ ] **Step 3: Build + tests**

```pwsh
dotnet build build/_build.Tests/Build.Tests.csproj
dotnet test --project build/_build.Tests/Build.Tests.csproj -c Release --framework net10.0
```

Expected: 0 errors, 0 warnings. All tests green.

- [ ] **Step 4: V2 audit — confirm no V1 fixture usage in touched files**

```pwsh
grep -rln "FakeRepoBuilder\|TestHostFixture\|ToLegacyBuildContext" build/_build.Tests --include "*.cs"
```

Acceptable: V1 fixtures still appear in tests for **unmigrated** targets only (Harvest, Vcpkg, Ci feature tests not touched by this slice). Touched files must have zero V1 fixture references.

- [ ] **Step 5: Commit (request approval)**

Commit message preview:

```text
test: migrate touched tests to V2 + fix dict retirement fallout

Per testing-guidelines.md altın kural: every test file the slice touches
migrates to V2 infrastructure (FakeCakeWorldV2, TargetTestHostV2, TestLogV2).
Dict-construction utilities replaced with PackageFamilyVersionSet builders.
Interface mocks replaced with concrete validator construction (validators
are sealed pure classes — no mocking needed). G58-prefixed type names in
assertions updated to CrossFamilyDependency*.
```

After approval:

```pwsh
git add -A
git commit -m "test: migrate touched tests to V2 + fix dict retirement fallout"
```

---

### Task 14: Add V2 scenario tests for PreFlightCheckTask + relocate validator unit tests

**Files:**

- `git mv build/_build.Tests/Unit/Features/Preflight/VersionConsistencyTests.cs build/_build.Tests/Unit/Validation/Manifest/VersionConsistencyTests.cs` (if not already moved during Phase 2 Task 5)
- `git mv build/_build.Tests/Unit/Features/Preflight/SemanticVersionParsingTests.cs build/_build.Tests/Unit/Validation/Manifest/SemanticVersionParsingTests.cs` (if not already moved)
- Create: `build/_build.Tests/Scenarios/PreFlightCheck/PreFlightCheckTaskScenarioTests.cs`
- Create: `build/_build.Tests/Fixtures/Data/Vcpkg/vcpkg-valid.json`
- Create: `build/_build.Tests/Fixtures/Data/Vcpkg/vcpkg-version-mismatch.json`

- [ ] **Step 1: Verify Phase 2 test relocations are complete**

```pwsh
ls build/_build.Tests/Unit/Features/Preflight/ -ErrorAction SilentlyContinue
```

Expected: empty / not found. If any test files survived, complete their relocation now (`git mv` to the appropriate `Unit/Validation/...` location and update namespaces). Then delete the empty `Unit/Features/Preflight/` directory.

- [ ] **Step 2: Add Vcpkg embedded fixtures**

Contents of `build/_build.Tests/Fixtures/Data/Vcpkg/vcpkg-valid.json`:

```json
{
  "name": "sdl2-cs-bindings",
  "overrides": [
    { "name": "sdl2", "version": "2.32.0" },
    { "name": "sdl2-image", "version": "2.8.0" },
    { "name": "sdl2-mixer", "version": "2.8.0" },
    { "name": "sdl2-ttf", "version": "2.24.0" },
    { "name": "sdl2-gfx", "version": "1.0.2" }
  ]
}
```

Contents of `build/_build.Tests/Fixtures/Data/Vcpkg/vcpkg-version-mismatch.json`:

```json
{
  "name": "sdl2-cs-bindings",
  "overrides": [
    { "name": "sdl2", "version": "99.99.99" },
    { "name": "sdl2-image", "version": "2.8.0" },
    { "name": "sdl2-mixer", "version": "2.8.0" },
    { "name": "sdl2-ttf", "version": "2.24.0" },
    { "name": "sdl2-gfx", "version": "1.0.2" }
  ]
}
```

`Build.Tests.csproj` should already glob-include `Fixtures/Data/**/*.json` as embedded resources from the existing test infra setup; verify the new files appear via:

```pwsh
dotnet build build/_build.Tests/Build.Tests.csproj
```

then inspect compiled assembly (or just trust that `<EmbeddedResource Include="Fixtures\Data\**\*.json" />` glob picks up the new files).

- [ ] **Step 3: Write scenario tests**

Contents of `build/_build.Tests/Scenarios/PreFlightCheck/PreFlightCheckTaskScenarioTests.cs`:

```csharp
using System.Collections.Immutable;
using Build.Repositories;
using Build.Shared.Manifest;
using Build.Targets.PreFlightCheck;
using Build.Targets.PreFlightCheck.Reporting;
using Build.Tests.Fixtures;
using Build.Validation;
using Microsoft.Extensions.DependencyInjection;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace Build.Tests.Scenarios.PreFlightCheck;

public sealed class PreFlightCheckTaskScenarioTests
{
    [Test]
    public async Task RunAsync_Should_Pass_When_All_Validators_Green()
    {
        var world = SeedHappyPath();

        var result = await CreateHost(world).RunAsync();

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Exception).IsNull();
    }

    [Test]
    public async Task RunAsync_Should_Throw_When_VersionsFile_Missing()
    {
        var world = FakeCakeWorldV2.CreateWindows()
            .WithManifestObject(ManifestFixture.CreateTestManifestConfig());

        var result = await CreateHost(world).RunAsync();

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Exception!.Message).Contains("--versions-file");
    }

    [Test]
    public async Task RunAsync_Should_Throw_When_Version_Mapping_Is_Empty()
    {
        var world = FakeCakeWorldV2.CreateWindows()
            .WithManifestObject(ManifestFixture.CreateTestManifestConfig())
            .WithTextFile("vcpkg-overlay-triplets/x64-windows-hybrid.cmake", "# overlay")
            .WithTextFile("vcpkg.json", FixtureLoader.Load("Vcpkg/vcpkg-valid.json"))
            .WithTextFile("artifacts/resolve-versions/versions.json", "{}")
            .WithVersionsFile("artifacts/resolve-versions/versions.json");

        var result = await CreateHost(world).RunAsync();

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Exception!.Message).Contains("non-empty version mapping");
    }

    [Test]
    public async Task RunAsync_Should_Throw_When_Manifest_Family_Name_Has_Mixed_Case()
    {
        var manifest = ManifestFixture.CreateTestManifestConfig();
        var families = manifest.PackageFamilies.ToList();
        families[0] = families[0] with { Name = "SDL2-Core" };
        var badManifest = manifest with { PackageFamilies = [.. families] };

        var world = SeedHappyPath()
            .WithManifestObject(badManifest);

        var result = await CreateHost(world).RunAsync();

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Exception!.Message).Contains("Pre-flight check failed");
    }

    [Test]
    public async Task RunAsync_Should_Throw_When_Runtime_Triplet_Is_Not_Hybrid_Overlay()
    {
        var manifest = ManifestFixture.CreateTestManifestConfig();
        var badRuntimes = manifest.Runtimes.Select(r => r with { Triplet = "x64-windows" }).ToImmutableList();
        var badManifest = manifest with { Runtimes = badRuntimes };

        var world = SeedHappyPath()
            .WithManifestObject(badManifest);

        var result = await CreateHost(world).RunAsync();

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Exception!.Message).Contains("Pre-flight check failed");
    }

    [Test]
    public async Task RunAsync_Should_Throw_When_Version_Consistency_Mismatched()
    {
        var world = SeedHappyPath()
            .WithTextFile("vcpkg.json", FixtureLoader.Load("Vcpkg/vcpkg-version-mismatch.json"));

        var result = await CreateHost(world).RunAsync();

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Exception!.Message).Contains("Pre-flight check failed");
    }

    [Test]
    public async Task RunAsync_Should_Throw_When_Hybrid_Overlay_File_Missing()
    {
        // Same world as happy path, but skip the overlay file seed
        var world = FakeCakeWorldV2.CreateWindows()
            .WithManifestObject(ManifestFixture.CreateTestManifestConfig())
            .WithTextFile("vcpkg.json", FixtureLoader.Load("Vcpkg/vcpkg-valid.json"))
            .WithTextFile("artifacts/resolve-versions/versions.json",
                FixtureLoader.Load("Versions/versions-valid.json"))
            .WithVersionsFile("artifacts/resolve-versions/versions.json");

        var result = await CreateHost(world).RunAsync();

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Exception!.Message).Contains("Pre-flight check failed");
    }

    private static FakeCakeWorldV2 SeedHappyPath()
    {
        var world = FakeCakeWorldV2.CreateWindows()
            .WithManifestObject(ManifestFixture.CreateTestManifestConfig())
            .WithTextFile("vcpkg-overlay-triplets/x64-windows-hybrid.cmake", "# overlay")
            .WithTextFile("vcpkg.json", FixtureLoader.Load("Vcpkg/vcpkg-valid.json"))
            .WithTextFile("artifacts/resolve-versions/versions.json",
                FixtureLoader.Load("Versions/versions-valid.json"))
            .WithVersionsFile("artifacts/resolve-versions/versions.json");
        return world;
    }

    private static TargetTestHostV2<PreFlightCheckTask> CreateHost(FakeCakeWorldV2 world)
    {
        return new TargetTestHostV2<PreFlightCheckTask>(world)
            .WithServices(services =>
            {
                services.AddRepositories();
                services.AddValidators();
                services.AddPreFlightCheck();
                // IVcpkgManifestReader registration: the existing AddVcpkgFeature() registers it,
                // OR the test relies on a fake stub injected via FakeCakeWorldV2.
                // If a fake is needed, register here via services.AddSingleton<IVcpkgManifestReader>(stub).
            });
    }
}
```

**Note on `Versions/versions-valid.json`:** this fixture exists from prior phases (per testing-guidelines.md §"Embedded fixtures"). Verify it carries family versions matching the manifest fixture's library upstream major/minor — if mismatch, G54 alignment fails the happy-path scenario. Adjust the fixture inline if needed.

**Note on `IVcpkgManifestReader` registration:** if `AddVcpkgFeature` is composed in production but not invoked here, the scenario will fail with "no service for `IVcpkgManifestReader`". Either:

- Call `services.AddVcpkgFeature()` in `CreateHost` if the feature is side-effect-free, or
- Register a fake reader stub: `services.AddSingleton<IVcpkgManifestReader>(world.CreateFakeVcpkgReader())` — extend `FakeCakeWorldV2` with a `CreateFakeVcpkgReader()` helper that parses `vcpkg.json` from the fake filesystem (NOT a separate parsing implementation; just call the real `VcpkgManifestReader` against the fake context).

The simpler path: register the production reader, since `VcpkgManifestReader.ParseFile(FilePath)` consumes `ICakeContext.FileSystem` which `FakeCakeWorldV2` already provides.

- [ ] **Step 4: Run scenario tests**

```pwsh
dotnet test --project build/_build.Tests/Build.Tests.csproj -c Release --framework net10.0 --filter "PreFlightCheckTaskScenarioTests"
```

Expected: 7 tests PASS. If any FAIL, debug per-validator behavior — most common cause is fixture data not matching manifest expectations (G54 alignment, csproj path validation). Adjust fixtures, NOT the validator logic.

- [ ] **Step 5: Build + full test suite**

```pwsh
dotnet build build/_build/Build.csproj
dotnet build build/_build.Tests/Build.Tests.csproj
dotnet test --project build/_build.Tests/Build.Tests.csproj -c Release --framework net10.0
```

Expected: 0 warnings, 0 errors. Test count up by 7 + retained migrations (varies based on Phase 2 outcomes).

- [ ] **Step 6: Commit (request approval)**

Commit message preview:

```text
test: add V2 scenario tests for PreFlightCheckTask + relocate Validation tests

Adds 7 in-process scenario tests covering happy path + 6 representative
failure modes (missing versions file, empty mapping, mixed-case family name,
non-hybrid triplet, version-consistency mismatch, missing hybrid overlay).
Embedded vcpkg.json fixtures under Fixtures/Data/Vcpkg/. Establishes the
boss-fight scenario pattern V2 uses for cross-cutting validation tasks
(template for future P7 Package + P9 Smoke/Publish slices).
```

After approval:

```pwsh
git add -A
git commit -m "test: add V2 scenario tests for PreFlightCheckTask + relocate Validation tests"
```

---

## Phase 7: Documentation + slopwatch baseline (3 commits)

### Task 15: Documentation sweep — canonical docs

**Files modified:**

- `docs/refactoring/target-centric-build-host-refactor-plan.md`
- `docs/refactoring/target-centric-build-host-review-checklist.md`
- `docs/parking-lot.md`
- `docs/knowledge-base/release-guardrails.md`
- `AGENTS.md`
- `docs/decisions/2026-05-05-target-centric-build-host.md`
- `docs/plan.md`

- [ ] **Step 1: Refactor plan updates**

Read `docs/refactoring/target-centric-build-host-refactor-plan.md`. Apply:

- §3 hotspots row: change `Features/Preflight/PreflightPipeline.cs` row to "(retired in S12 P6 — see §11)".
- §6 Shared concept map: add `Validation` row:

```markdown
| `Validation` | Cross-cutting validators with domain alt-folders (`Manifest/`, `Versioning/`, `Packaging/`) + `Models/` + `Conventions/`. Single `AddValidators()` registration. | Target-specific validators that have only one consumer (kept inline in target module if any survive). |
```

- §11 P6 status block: rewrite from "PreFlightCheck migration" pending to:

```markdown
### P6 - PreFlightCheck migration ✅ (completed 2026-05-08)

**Slice:** S12 (`docs/superpowers/specs/2026-05-08-p6-preflight-migration-design.md`).

Completed:

- `Features/Preflight/` retired; `Targets/PreFlightCheck/` owns PreFlightCheckTask + Reporting/ + ServiceCollectionExtensions.
- `Validation/` root concept established with `Manifest/`, `Versioning/`, `Packaging/`, `Models/`, `Conventions/` alt-folders. Single `AddValidators()` registration covers all 7 build-host validators.
- 7 validators uniformly shaped: `sealed class`, no interface, `AddSingleton`. Statics promoted to instance. `IUpstreamVersionAlignmentValidator`, `IG58CrossFamilyDepResolvabilityValidator`, `ICsprojPackContractValidator` deleted (single-impl).
- G58-prefixed types renamed to `CrossFamilyDependency*` (behavior-first per ADR §10).
- `IVcpkgManifestRepository` added; `Repositories/` symmetry with `IManifestRepository`.
- `ManifestFamilyNameInvariantValidator` (G59) added; catches mixed-case manifest authoring drift.
- `IReadOnlyDictionary<string, NuGetVersion>` retired from every task/service boundary. `PackageBuildConfiguration.FamilyVersions: PackageFamilyVersionSet` replaces dict.
- 5 OneOf result types deleted (`VersionConsistencyResult`, `CoreLibraryIdentityResult`, `CsprojPackContractResult`, `UpstreamVersionAlignmentResult`, `PreflightError`). Parking-lot count 11 → 6.
- All touched test files migrated to V2; new V2 scenario tests for PreFlightCheckTask establish the boss-fight pattern.
- Cross-platform validation passed on Windows + WSL + macOS; full GitHub Actions release pipeline run validated (run [TBD]).

**P7 (Package boss fight) is the next slice.**
```

- [ ] **Step 2: Review checklist updates**

Read `docs/refactoring/target-centric-build-host-review-checklist.md`. Apply:

- §6 add a new row: "Validation/ root concept used for cross-cutting validators with domain alt-folders; single `AddValidators()` registration."
- §11 retired abstractions: confirm `PreflightPipeline` listed.
- §13 V2 — add line "Validators registered as concrete sealed classes via `AddValidators()`; no `IValidator` interfaces."

- [ ] **Step 3: Parking-lot updates**

Read `docs/parking-lot.md`. Update the OneOf entry:

- "11 surviving OneOf types" → "6 surviving OneOf types" (verify exact count by `grep` after slice ships).
- Remove the 5 retired types from the listed survivors.
- Note "P6 closed S12 (2026-05-08); next OneOf retirement during P7 (Package)".

- [ ] **Step 4: Release guardrails updates**

Read `docs/knowledge-base/release-guardrails.md`. Apply:

- Add a new G59 row:

```markdown
| **G59** | Manifest family-name invariant — every `package_families[].name` matches `^sdl[0-9]+-[a-z][a-z0-9-]*$`. | PreFlight | `Validation/Manifest/ManifestFamilyNameInvariantValidator` | Catches hand-edited mixed-case / underscore-delimited family names that bypass `PackageFamilyId` ordinal-exact lookups in downstream consumers. |
```

- Update G16 owner column: `Validation/Packaging/HybridStaticOverlayValidator`.
- Update G49 owner column: `Validation/Manifest/CoreLibraryIdentityValidator`.
- Update G54 owner column: `Validation/Versioning/UpstreamVersionAlignmentValidator`.
- Update G58 owner column: `Validation/Versioning/CrossFamilyDependencyResolvabilityValidator`.

- [ ] **Step 5: AGENTS.md updates**

Read `AGENTS.md`. Apply:

- "Settled Strategic Decisions" — review whether to add a row for `Validation` as a named concept. Recommendation: add this row to make the ADR-002 §6 elevation explicit:

```markdown
| `Validation` named concept | Root-level `Validation/{Manifest,Versioning,Packaging,Models,Conventions}/` holds all build-host validators with single `AddValidators()` registration. Sealed classes, no interfaces, instance via DI singleton. |
```

- "Configuration File Relationships" — `IReadOnlyDictionary<string, NuGetVersion>` is no longer mentioned anywhere in this section; verify and clean any historical references.

- [ ] **Step 6: ADR-002 updates**

Read `docs/decisions/2026-05-05-target-centric-build-host.md`. Apply:

- §6 — add a paragraph or footnote noting `Validation` is an elevated named concept with domain alt-folders providing organization while preserving the "no catch-all" principle:

```markdown
**Elevation note (2026-05-08, S12 P6):** `Validation/` is an elevated named concept holding cross-cutting build-host validators. Domain alt-folders (`Manifest/`, `Versioning/`, `Packaging/`) organize validators by what they validate; `Models/` and `Conventions/` carry shared validation shapes. Single `AddValidators()` registration. Single-consumer validators that have only one target consumer in practice still live under `Validation/` (not under `Targets/<TargetName>/Validation/`) to keep the canonical lookup point uniform — a pragmatic deviation from §6's "single-consumer code stays with target" rule, accepted for maintenance simplicity.
```

- [ ] **Step 7: docs/plan.md updates**

Read `docs/plan.md`. Apply:

- Phase X status sentence: P6 closed via S12.
- Active plan paragraph: "P0-P6 closed... P7 (Package boss fight) is next."
- Bumped test count (verify via `dotnet test` count).

- [ ] **Step 8: Commit (request approval — note: docs-only, exempt from approval gate but still confirm before commit)**

Commit message preview:

```text
docs: log P6 PreFlightCheck migration closure across canonical docs

Refactor plan §11 P6 status block updated, parking-lot OneOf count
11 → 6, release-guardrails G59 added + G16/G49/G54/G58 owner columns
updated, AGENTS.md Validation named-concept row added, ADR-002 §6
elevation note appended, docs/plan.md Phase X status bumped to P6
closed.
```

After approval:

```pwsh
git add -A
git commit -m "docs: log P6 PreFlightCheck migration closure across canonical docs"
```

---

### Task 16: Code documentation sweep — XML doc + inline comments

**Sweep targets** per spec §13.2:

- XML doc `<see cref>` references to old namespaces.
- File-header copyright headers (preserve SPDX).
- Inline migration-timing comments ("introduced in S11", "retired in P6", "replaced by") — delete.
- ADR-link comments — keep only when explaining a non-obvious local decision.
- G-number-first naming in comment language → behavior-first with bracketed metadata.
- TODO comments — convert to canonical doc / GitHub issue / delete.
- Unused `using` directives.

- [ ] **Step 1: Audit for stale references**

```pwsh
grep -rln "Build.Features.Preflight\|Build.Shared.Versioning\|Build.Shared.Packaging.G58" build/_build --include "*.cs"
```

Expected: zero matches in production code (test code already migrated in Phase 6). If any survive, fix.

```pwsh
grep -rn "introduced in S\|retired in P\|see ADR-\|\bP[0-9]\b\s*--\|\bS1[0-9]\b" build/_build --include "*.cs"
```

Inspect each match. Delete migration-timing comments. Convert ADR cross-references to self-contained explanations where the local code's behavior is the subject.

- [ ] **Step 2: Specific known sites**

- `Validation/Manifest/CoreLibraryIdentityValidator.cs`: XML doc may reference "S11" — drop, replace with self-contained explanation of what the validator enforces.
- `Validation/Manifest/CsprojPackContractValidator.cs`: XML doc remarks describe retired guardrail history (G1-G5, G8) — trim to current rules (G6, G7, G17, G18) and brief retirement note (1 sentence).
- `Validation/Models/CrossFamilyDependencyModels.cs`: enum doc references "G58" — keep as bracketed metadata `[G58]` only, drop bare-prefix `G58: ...` style.
- `Targets/PreFlightCheck/Reporting/PreflightReporter.cs`: log strings already updated in Phase 3, but verify XML doc on the class is self-contained (no `see ADR-002` substitute).
- `Versioning/PackageFamilyVersionSet.cs`: XML doc updated in Phase 4 Step 5 — verify "Replaces raw IReadOnlyDictionary" framing is gone.
- Any `// Phase X` / `// P5` / `// P6` markers in production code — delete.

- [ ] **Step 3: Drop unused `using` directives**

After all the namespace shuffles, dead `using` directives accumulate. Apply Roslyn `IDE0005`-style cleanup to every modified file:

```pwsh
dotnet format build/_build/Build.csproj --include "build/_build/Validation/" "build/_build/Targets/PreFlightCheck/" "build/_build/Repositories/" "build/_build/Host/Configuration/" "build/_build/Features/Packaging/" "build/_build/Features/Publishing/" --verify-no-changes
```

If `--verify-no-changes` reports diffs, run without that flag to apply, then verify build green:

```pwsh
dotnet format build/_build/Build.csproj --include "build/_build/Validation/" "build/_build/Targets/PreFlightCheck/" "build/_build/Repositories/" "build/_build/Host/Configuration/" "build/_build/Features/Packaging/" "build/_build/Features/Publishing/"
dotnet build build/_build/Build.csproj
```

- [ ] **Step 4: Build + tests**

```pwsh
dotnet build build/_build/Build.csproj
dotnet test --project build/_build.Tests/Build.Tests.csproj -c Release --framework net10.0
```

Expected: 0 warnings, 0 errors, all tests green.

- [ ] **Step 5: Commit (request approval)**

Commit message preview:

```text
docs: code-doc sweep — XML doc updates, drop migration-timing comments, behavior-first naming

ADR §10 boundary cleanup. XML doc <see cref> updated to new namespaces.
Migration-timing inline comments ("introduced in S11", "retired in P6")
removed — phase metadata lives in canonical docs. G58 references
demoted to bracketed [G58] metadata position. CoreLibraryIdentity +
CsprojPackContract XML doc bodies trimmed for self-containment.
Unused using directives removed via dotnet format.
```

After approval:

```pwsh
git add -A
git commit -m "docs: code-doc sweep — XML doc updates, drop migration-timing comments, behavior-first naming"
```

---

### Task 17: Slopwatch baseline rebuild (chore commit)

After all the deletions + relocations from Phase 1-7, the Slopwatch baseline points at deleted files. Rebuild it.

- [ ] **Step 1: Run slopwatch analyze first to verify clean state**

```pwsh
slopwatch analyze --fail-on warning --exclude "artifacts/**,external/**,vcpkg_installed/**,**/bin/**,**/obj/**" --baseline .slopwatch/baseline.json
```

Expected: 0 issue(s) found. If issues found, audit each — most likely they're stale entries pointing at relocated files. Rebuilding the baseline (next step) clears those.

- [ ] **Step 2: Rebuild baseline**

```pwsh
slopwatch init -f --exclude "artifacts/**,external/**,vcpkg_installed/**,**/bin/**,**/obj/**"
```

Expected: new baseline written to `.slopwatch/baseline.json` with current entry count (will likely be ~40-45, down from 48 post-S11 due to S12 deletions).

- [ ] **Step 3: Verify clean run**

```pwsh
slopwatch analyze --fail-on warning --exclude "artifacts/**,external/**,vcpkg_installed/**,**/bin/**,**/obj/**" --baseline .slopwatch/baseline.json
```

Expected: 0 issue(s) found.

- [ ] **Step 4: Commit (request approval)**

Commit message preview:

```text
chore: rebuild slopwatch baseline post-S12 P6 deletions

Drops baseline entries pointing at deleted Features/Preflight/,
Shared/Versioning/, Shared/Packaging/G58* paths. Post-S12 entry count
[count]; was 48 post-S11.
```

After approval:

```pwsh
git add .slopwatch/baseline.json
git commit -m "chore: rebuild slopwatch baseline post-S12 P6 deletions"
```

---

## Phase 8: Validation gates (Windows + WSL + macOS + GitHub Actions)

### Task 18: Windows ci-sim (final pre-merge gate from worktree)

- [ ] **Step 1: Run full ci-sim from the worktree**

```pwsh
cd e:\tmp\sdl2-cs-bindings-p6-preflight
dotnet run --file tools.cs -- ci-sim
```

Expected: 8/8 PASS. All 8 stages green in Spectre summary. Time should be ~3 min with vcpkg cache from main checkout.

If any stage fails, fix on the worktree (per playbook §"Failure triage" — fix on Windows source of truth), re-run from Step 1. Do not merge to master with red ci-sim.

- [ ] **Step 2: Capture ci-sim log evidence**

```pwsh
ls -la .logs/tools/ci-sim-*/
```

Expected: latest run directory with 8 per-step logs. Keep this evidence for the slice summary at Task 21.

- [ ] **Step 3: Build + test final gate**

```pwsh
dotnet build build/_build/Build.csproj
dotnet test --project build/_build.Tests/Build.Tests.csproj -c Release --framework net10.0
slopwatch analyze --fail-on warning --exclude "artifacts/**,external/**,vcpkg_installed/**,**/bin/**,**/obj/**"
```

Expected: 0 warnings, 0 errors. All tests green. 0 slopwatch issues.

- [ ] **Step 4: PreFlight + ResolveVersions end-to-end smoke**

```pwsh
dotnet run --project build/_build -- --target ResolveVersionsFromManifest --versions-file artifacts/resolve-versions/versions.json --suffix p6.test
dotnet run --project build/_build -- --target PreFlightCheck --versions-file artifacts/resolve-versions/versions.json
```

Expected: both succeed. PreFlight reports all 7 validators PASS. No `Pipeline` mention in PreFlight log lines (the pipeline class is gone).

- [ ] **Step 5: Build target tree audit**

```pwsh
dotnet run --file tools.cs -- build --tree
```

Expected: PreFlightCheck listed; no Coverage-Check; no strategy targets; no Features/Preflight artifacts.

This task is a validation gate, not a commit. No `git commit` here.

---

### Task 19: Merge worktree branch to master (Deniz approval gate)

Per AGENTS.md "ADR-002 migration execution rules": "Merge to master only after the full refactor is accepted."

- [ ] **Step 1: Present slice summary + cumulative commit message preview to Deniz**

Prepare a single conceptual summary covering all Phase 1-7 commits:

```text
S12 P6 PreFlightCheck migration

- Targets/PreFlightCheck/ owns orchestration; PreflightPipeline retired.
- Validation/{Manifest,Versioning,Packaging,Models,Conventions}/ root
  concept holds 7 sealed-class validators with single AddValidators().
- IVcpkgManifestRepository added (Repositories/ symmetry).
- ManifestFamilyNameInvariantValidator (G59) added.
- IReadOnlyDictionary<string,NuGetVersion> retired from every
  task/service boundary; PackageFamilyVersionSet everywhere.
- 5 OneOf result types deleted (parking-lot 11 → 6).
- 3 single-impl interfaces deleted (ICsprojPackContract,
  IG58CrossFamily, IUpstreamVersionAlignment).
- G58-prefixed types renamed CrossFamilyDependency*.
- All touched test files migrated to V2; new V2 scenario tests for
  PreFlightCheckTask establish boss-fight pattern.
- Doc + code-doc sweep complete; slopwatch baseline rebuilt.

[N] commits across [list of phases].
```

Wait for Deniz approval. **Do NOT merge or push without explicit "go / apply / proceed / başla / yap".**

- [ ] **Step 2: Switch to master checkout, fast-forward merge from worktree branch**

```pwsh
cd e:\repos\my-projects\janset2d\sdl2-cs-bindings
git status
```

Expected: any uncommitted temp slice docs (the design + plan files) still in working tree but tracked OUTSIDE the worktree branch (they are scratch per refactor doc lifecycle).

```pwsh
git fetch
git merge --ff-only slice/p6-preflight
```

If non-fast-forward (master moved during slice work), rebase the slice branch on top of master inside the worktree first:

```pwsh
cd e:\tmp\sdl2-cs-bindings-p6-preflight
git fetch origin
git rebase origin/master
```

Resolve any conflicts (unlikely given slice scope), then return to main checkout and `git merge --ff-only`.

- [ ] **Step 3: Push to master**

```pwsh
git push origin master
```

Master now has the slice; worktree branch can be retained for reference until WSL/macOS validation completes.

---

### Task 20: WSL ci-sim validation

Per playbook §"Workflow" Step 3.

- [ ] **Step 1: Drive WSL non-interactively**

```pwsh
wsl -- zsh -lc 'cd /home/deniz/repos/sdl2-cs-bindings && git fetch origin && git reset --hard origin/master && git submodule update --init --recursive && dotnet run --file tools.cs -- ci-sim'
```

Expected: 8/8 PASS, ~234s with vcpkg cache.

- [ ] **Step 2: If failed, triage per playbook**

Per playbook §"Failure triage": fix on Windows (source of truth), commit, push, re-pull on WSL, re-run. Each fix is a separate commit through the same approval gate.

This task is a validation gate, not a commit (only commits happen if WSL surfaces a Windows-missed bug requiring a fix-forward).

---

### Task 21: macOS ci-sim validation

Per playbook §"Workflow" Step 4.

- [ ] **Step 1: Drive macOS over SSH**

```pwsh
ssh Armut@192.168.50.178 'zsh -lc "cd /Users/armut/repos/sdl2-cs-bindings && git fetch origin && git reset --hard origin/master && git submodule update --init --recursive && dotnet run --file tools.cs -- ci-sim"'
```

Expected: 8/8 PASS, ~193s with vcpkg cache (osx-x64 host).

- [ ] **Step 2: If failed, triage per playbook**

Same fix-forward flow as Task 20 Step 2.

---

### Task 22: GitHub Actions release pipeline (Deniz manual trigger)

This task is **Deniz-only**. The plan does not invoke `workflow_dispatch`.

- [ ] **Step 1: Notify Deniz the slice is ready for release pipeline trigger**

Slice summary handed off:

- Worktree branch merged to master, pushed.
- Windows + WSL + macOS ci-sim 8/8 PASS each.
- Slopwatch clean, tests green, build clean.
- Ready for `release.yml` `workflow_dispatch` manual trigger.

- [ ] **Step 2: Wait for Deniz to trigger release pipeline + observe outcome**

Expected: full 7-RID matrix run, 22/22 jobs PASS (1 disabled `Publish (Public)` by design), 10 nupkgs pushed to staging GitHub Packages feed.

If any job fails, capture run ID + failing job log → triage on Windows → fix-forward through the approval gate → re-trigger pipeline.

- [ ] **Step 3: Capture release pipeline run ID for the slice doc record**

After successful run, record the run ID. Update `docs/refactoring/target-centric-build-host-refactor-plan.md` §11 P6 status block (already prepared in Task 15 with `[TBD]` placeholder) — replace `[TBD]` with the actual run ID. Commit as a tiny doc-fix:

```pwsh
git add docs/refactoring/target-centric-build-host-refactor-plan.md
git commit -m "docs: log GitHub Actions run ID for P6 release pipeline validation"
git push origin master
```

(No approval gate needed — docs-only edit.)

---

## Phase-X backlog items surfaced during execution

These are deferred items discovered while implementing the slice. **Phase 7 doc sweep (Task 15) must transcribe them into canonical `docs/parking-lot.md` Phase X entries before this slice plan is deleted.**

| Item | Notes |
| --- | --- |
| **PreflightReporter still uses `ICakeLog`, not `IAnsiConsole`** | Migrated targets (e.g. `InfoTask`, `OtoolAnalyze`) use `IAnsiConsole` for output (per S03/P3-P4 IAnsiConsole injection slice). PreflightReporter was relocated as-is, still constructor-injects `ICakeContext` and uses `_cakeContext.Log.Information/Error`. Phase-X cleanup: switch to `IAnsiConsole` for visual consistency (Spectre tables, panels, color emojis already partially used via `Log.Information("✅ ...")`). |
| **Other "weird usages" of `ICakeLog` static-style logging in migrated/relocated build-host code** | Audit during Phase-X: any logging that should be `IAnsiConsole` for richer output (`AnsiConsole.MarkupLine`, status panels) versus genuine plain text where `ICakeLog` is appropriate. |
| **Validator analyzer suppressions (CA1822/S2325) review** | Already obviated by interface introduction during this slice — validators implementing interfaces can't be marked static; analyzer is satisfied. **No suppressions remain in `Validation/`.** Item kept for documentation closure: confirm in Phase X that no Validation/ class carries a CA1822/S2325 SuppressMessage attribute. |
| **`Build.Validation.Versioning` namespace duplication with `Build.Versioning`** | Two namespaces named "Versioning": one root (`Build.Versioning` — `PackageFamilyId`, `PackageFamilyVersionSet`, `ExplicitVersionParser`), one under Validation (`Build.Validation.Versioning` — Upstream + CrossFamily validators). Confusing import patterns possible. Phase-X: consider rename of Validation alt-folder (`Validation/VersionAlignment/`?) or root concept (`Build.Versioning` → `Build.PackageFamilies/`?). Current state defensible (both names accurately describe their domain) but worth a re-read post-P10. |

---

## Self-review checklist

After all tasks ship, walk through this checklist:

**1. Spec coverage:**

- [ ] §3 D1 (Validation/ root with domain alt-folders) — Phase 1 Task 1 + Phase 2 Tasks 4-9.
- [ ] §3 D2 (uniform sealed class shape, no interface, AddSingleton) — Phase 2 Tasks 4-9 (drop interface), Phase 1 Task 1 + Task 3 (AddValidators).
- [ ] §3 D3 (dict full retirement) — Phase 4 Task 11.
- [ ] §3 D4 (IVcpkgManifestRepository) — Phase 1 Task 2.
- [ ] §3 D5 (path metadata removal) — Phase 2 Task 5 (Validation drop ManifestPath/VcpkgManifestPath) + Phase 3 Task 10 Step 2 (PreflightReporter hardcode labels).
- [ ] §3 D6 (slice-scope private audit) — Phase 2 Tasks 5-9 audit notes per validator.
- [ ] §3 D7 (doc + code-doc sweep) — Phase 7 Tasks 15-16.
- [ ] §3 D8 (cross-platform validation) — Phase 8 Tasks 18-22.
- [ ] §4 Validation/ folder layout — Phase 1 + Phase 2.
- [ ] §5 PreFlightCheckTask body shape — Phase 3 Task 10.
- [ ] §6 IVcpkgManifestRepository — Phase 1 Task 2.
- [ ] §7 dict retirement — Phase 4 Task 11.
- [ ] §8 ManifestFamilyNameInvariantValidator (G59) — Phase 1 Task 3.
- [ ] §9 OneOf cleanup — Phase 5 Task 12 (with inline cleanup during Phase 2 Tasks 5-9).
- [ ] §10 Test policy V2 altın kural — Phase 6 Tasks 13-14.
- [ ] §11 Private method audit — Phase 2 Tasks 5-9 (audit notes inline per validator).
- [ ] §12 Doc sweep — Phase 7 Task 15.
- [ ] §13 Code doc sweep — Phase 7 Task 16.
- [ ] §14 Cross-platform validation — Phase 8 Tasks 18-22.
- [ ] §15 Risks acknowledged across phases.
- [ ] §16 Exit criteria mirror Task 18-22 validation gates.

**2. Placeholder scan:**

Search this plan for red flags:

```pwsh
grep -n "TBD\|TODO\|FIXME\|placeholder\|fill in\|implement later" docs/superpowers/plans/2026-05-08-p6-preflight-migration-plan.md
```

Expected matches: only intentional `[TBD]` markers in the GitHub Actions run ID row (Task 22 Step 3) and Task 15 Step 1 status block — these are explicit "Deniz fills this in after pipeline run" placeholders, NOT plan failures.

**3. Type consistency:**

Spot-check method/property/class names match across tasks:

- `PackageBuildConfiguration.FamilyVersions` (Phase 4) — used in Phase 6 Task 13 Step 2 V1→V2 examples. ✓
- `CrossFamilyDependencyResolvabilityValidator.Validate(versions, manifest)` parameter order (Phase 4 Step 4) — invoked in Phase 3 Task 10 Step 3 PreFlightCheckTask body. ✓
- `UpstreamVersionAlignmentValidator.Validate(manifest, versions)` parameter order (Phase 4 Step 3) — invoked in Phase 3 Task 10 Step 3. ✓
- `PreflightReporter.ReportCrossFamilyDependencyResolvability` rename (Phase 2 Task 8 Step 10) — invoked in Phase 3 Task 10 Step 3. ✓
- `PreflightReporter.ReportManifestFamilyNameInvariant` new method (Phase 3 Task 10 Step 2) — invoked in Phase 3 Task 10 Step 3. ✓
- `IVcpkgManifestRepository.Load()` (Phase 1 Task 2 Step 3) — used in Phase 3 Task 10 Step 3. ✓
- `PackageFamilyVersionSet.Empty` (Phase 4 Step 2) — referenced in `Versioning/PackageFamilyVersionSet.cs` (existing API). ✓
- `Validation/ServiceCollectionExtensions.AddValidators()` (Phase 1 Task 1) — extended throughout Phase 1-2, called from Program.cs (Phase 1 Task 1 Step 2). ✓

If any inconsistency found during execution, fix inline and update both sites.

---

## Execution handoff

Plan complete and saved to `docs/superpowers/plans/2026-05-08-p6-preflight-migration-plan.md`.

Two execution options:

**1. Subagent-Driven (recommended)** — I dispatch a fresh subagent per task, review between tasks, fast iteration. Best for the boss-fight scenario where each phase is large and benefits from focused per-task context.

**2. Inline Execution** — Execute tasks in this session using `executing-plans`, batch execution with checkpoints. Best if Deniz wants to drive each step interactively in the same conversation thread.

**Which approach, başkanım?**
