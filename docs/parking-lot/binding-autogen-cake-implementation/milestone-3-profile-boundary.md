# Milestone 3 Profile Boundary Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Introduce the `GenerateBindings` profile/config boundary so SDL2.Core continues to generate unchanged while SDL2 satellite families gain explicit, non-emitting profile resolution.

**Architecture:** Keep manifest-backed data contracts under `Build.Data.BindingGeneration`, compose those facts with target-local profiles under `Build.Targets.GenerateBindings`, and keep cross-cutting config/model/output checks under `Build.Validation.BindingGeneration`. `GenerateBindingsTask` resolves enabled generation plans once and passes a target-local plan to `BindingFamilyGeneration`; disabled satellites resolve through config/profile tests without running parse, emit, compile-check, package, or smoke by default.

**Tech Stack:** .NET 10, C# 14, Cake Frosting, CppAst, TUnit, Microsoft.Testing.Platform, Verify.TUnit snapshots, Cake `FakeCakeWorld`, `tools.cs generate-bindings`, `docker/binding-generator.Dockerfile`, `tests/binding-compile-check/SDL2.Core.CompileCheck.csproj`.

---

## Non-Negotiables

- Preserve SDL2.Core generated output unless a RED characterization test proves a real ABI/API bug and Deniz approves the behavior change.
- Do not enable SDL2.Image, SDL2.Mixer, SDL2.Ttf, or SDL2.Gfx generation in M3.
- Do not flip package source layout, compile-check scope, package-consumer smoke, or production generated-source paths for satellites.
- Do not implement SDL3 behavior. SDL3 profile names remain future/reserved documentation only.
- Do not introduce JSON ABI policy knobs for scalar widths, SDL bool shape, C `long`, `wchar_t`, pointer classification, callbacks, variadics, macro taxonomy, platform merge, or struct/union layout.
- Keep `Build.Data.*` free of `Build.Targets.GenerateBindings.*` dependencies.
- Keep profiles target-local under `build/_build/Targets/GenerateBindings/`.
- Use interfaces when they create a real test or variant seam. Do not create ceremonial `IFoo` / `Foo` pairs for simple records or pure helpers.
- Remove `LegacyBindingTypeRefBridge` during M3. It is spike scaffolding; no backward-compatibility shim is required.
- Enrich embedded `.h` fixture tests whenever a slice touches parse/model behavior that can be characterized with a focused header.
- Use `docker/binding-generator.Dockerfile` with command override for Linux/header-specific checks when useful; do not run full binding generation only to prove a narrow readiness condition.
- Do not start implementation Tasks 2+ until Deniz explicitly approves with `go`, `proceed`, `başla`, or equivalent. This plan touches `build/manifest.json` and build-host production code, so the repo approval gate applies before mutation, not only before commits.
- Stop before each commit. Present a summary and proposed commit message; wait for Deniz approval.

## Design Reference

- Accepted design spec: `docs/superpowers/specs/2026-05-20-m3-binding-profile-boundary-design.md`
- Canonical roadmap section: `docs/binding-autogen/binding-generator-roadmap.md` §Milestone 3
- Binding-generator constitution: `docs/binding-autogen/binding-generator-constitution.md`
- Maintenance playbook: `docs/playbook/binding-generator-maintenance.md`

## Target File Structure

Planned production additions:

```text
build/_build/Data/BindingGeneration/
  Models/
    BindingGenerationConfigEnvelope.cs

build/_build/Targets/GenerateBindings/
  Profiles/
    BindingGenerationProfile.cs
    BindingGenerationProfileRegistry.cs
    DeclarationVisibilityStrategy.cs
    Sdl2CoreProfile.cs
    Sdl2SatelliteProfile.cs
    Sdl2GfxProfile.cs
  Planning/
    BindingGenerationPlanResolver.cs
    BindingGenerationResolutionError.cs
    ResolvedBindingGenerationConfig.cs
    ResolvedBindingGenerationPlan.cs
    ResolvedBindingGenerationAuditPlan.cs
```

Planned test additions:

```text
build/_build.Tests/Unit/Targets/GenerateBindings/
  Profiles/
    BindingGenerationProfileRegistryTests.cs
    DeclarationVisibilityStrategyTests.cs
  Planning/
    BindingGenerationPlanResolverTests.cs

build/_build.Tests/Fixtures/Data/GenerateBindings/SemanticTypes/
  sdl2-gfx-scope-macros.h
  sdl2-satellite-declspec-and-core-types.h
  sdl2-satellite-callbacks-long-and-bool.h

build/_build.Tests/Fixtures/Data/Manifest/
  manifest-binding-generation-profiles.json
  manifest-binding-generation-unknown-profile.json
  manifest-binding-generation-enabled-missing-header-set.json
  manifest-binding-generation-profile-mismatch.json
  manifest-binding-generation-invalid-native-import.json
  manifest-binding-generation-unknown-validator.json
```

Existing files expected to change:

- `build/manifest.json`
- `build/_build/Data/BindingGeneration/Models/BindingGenerationConfig.cs`
- `build/_build/Data/BindingGeneration/Models/BindingGenerationConfigError.cs`
- `build/_build/Data/BindingGeneration/BindingGenerationConfigRepository.cs`
- `build/_build/Targets/GenerateBindings/ServiceCollectionExtensions.cs`
- `build/_build/Targets/GenerateBindings/GenerateBindingsTask.cs`
- `build/_build/Targets/GenerateBindings/BindingFamilyGeneration.cs`
- `build/_build/Targets/GenerateBindings/Emit/BindingEmissionOptions.cs`
- `build/_build/Targets/GenerateBindings/Emit/RawAbi/RawAbiCommandEmitter.cs`
- `build/_build/Targets/GenerateBindings/ModelBuilding/BindingModelBuilder.cs`
- `build/_build/Targets/GenerateBindings/ModelBuilding/Types/NativeTypeClassificationContext.cs`
- `build/_build/Targets/GenerateBindings/ModelBuilding/Types/TypeMappingPolicy.cs`
- `build/_build/Targets/GenerateBindings/ModelBuilding/Types/LegacyBindingTypeRefBridge.cs` (delete)
- `build/_build.Tests/Fixtures/ManifestFixture.cs`
- `build/_build.Tests/Fixtures/BindingGenerationFixture.cs`
- `build/_build.Tests/Unit/Data/BindingGeneration/BindingGenerationConfigRepositoryTests.cs`
- `build/_build.Tests/Scenarios/GenerateBindings/GenerateBindingsTaskScenarioTests.cs`
- `build/_build.Tests/Scenarios/GenerateBindings/GenerateBindingsTaskSnapshotTests.cs`
- `docs/binding-autogen/binding-generator-roadmap.md`
- `docs/binding-autogen/binding-generator-constitution.md`
- `docs/playbook/binding-generator-maintenance.md`

## Task 1: Baseline Verification

**Files:**
- Read: `docs/superpowers/specs/2026-05-20-m3-binding-profile-boundary-design.md`
- Read: `build/_build/Targets/GenerateBindings/**`
- Read: `build/_build.Tests/Unit/Data/BindingGeneration/BindingGenerationConfigRepositoryTests.cs`

- [ ] **Step 1: Inspect worktree**

Run:

```pwsh
git status --short
```

Expected: only approved M3 documentation work or a clean tree. If unrelated user changes exist, leave them alone.

- [ ] **Step 2: Run current build-host tests**

Run:

```pwsh
dotnet test --project build/_build.Tests/Build.Tests.csproj -c Release --framework net10.0
```

Expected: PASS. If it fails, stop and diagnose with `systematic-debugging` before writing M3 tests.

- [ ] **Step 3: Capture current generated-output posture**

Run when Docker is provisioned:

```pwsh
dotnet run --file tools.cs -- generate-bindings
```

Expected: SDL2.Core preview files remain under `artifacts/generated-bindings-preview/sdl2-core/`. If Docker is not available, record the exact failure in the implementation notes and continue with native-free tasks.

- [ ] **Step 4: Run compile-check if generated preview exists**

Run:

```pwsh
dotnet build tests/binding-compile-check/SDL2.Core.CompileCheck.csproj -c Release
```

Expected: PASS when preview exists. If preview is absent, `BINDING-COMPILE-CHECK-001` is an environment/baseline issue, not an M3 refactor result.

## Approval Gate Before Implementation

- [ ] **Step 1: Stop for explicit implementation approval**

Before starting Task 2, present the Task 1 baseline result and ask Deniz for explicit approval to mutate `build/manifest.json` and build-host production code.

Expected: Deniz replies with an explicit `go`, `proceed`, `başla`, `yap`, or equivalent. Without that approval, do not continue into Task 2.

## Task 2: Add Manifest Profile Fields And Data Round-Trip Tests

**Files:**
- Modify: `build/_build/Data/BindingGeneration/Models/BindingGenerationConfig.cs`
- Modify: `build/manifest.json`
- Modify: `build/_build.Tests/Fixtures/Data/Manifest/manifest-real.json`
- Modify: `build/_build.Tests/Fixtures/Data/Manifest/manifest-with-macro-policy.json`
- Create: `build/_build.Tests/Fixtures/Data/Manifest/manifest-binding-generation-profiles.json`
- Modify: `build/_build.Tests/Unit/Data/BindingGeneration/BindingGenerationConfigRepositoryTests.cs`
- Modify: `build/_build.Tests/Fixtures/ManifestFixture.cs`
- Modify: `build/_build.Tests/Fixtures/BindingGenerationFixture.cs`

- [ ] **Step 1: Write failing round-trip assertions for profile fields**

Patch `BindingGenerationConfigRepositoryRoundTripTests.Load_Should_Return_Sdl2Core_Config_When_Manifest_Has_Enabled_Block` to assert profile fields:

```csharp
await Assert.That(config.ProfileId).IsEqualTo("sdl2-core");
await Assert.That(config.ExportMacroNames).Contains("DECLSPEC");
```

Add a new test:

```csharp
[Test]
public async Task LoadConfig_Should_Return_Disabled_Config_When_Family_Disabled()
{
    var repo = BuildRepo("Manifest/manifest-binding-generation-profiles.json");

    var result = repo.LoadConfig("sdl2-image");

    await Assert.That(result.IsSuccess).IsTrue();
    var envelope = result.Value;
    await Assert.That(envelope.Config.Enabled).IsFalse();
    await Assert.That(envelope.Config.ProfileId).IsEqualTo("sdl2-satellite");
    await Assert.That(envelope.Config.ManagedNamespace).IsEqualTo("SDL2.Image");
    await Assert.That(envelope.Config.HeaderSet).IsNull();
}
```

Add the same disabled-placeholder coverage for `sdl2-gfx`, because gfx is the profile outlier and normal satellite coverage is not enough:

```csharp
[Test]
public async Task LoadConfig_Should_Return_Disabled_Gfx_Config_With_Gfx_Profile()
{
    var repo = BuildRepo("Manifest/manifest-binding-generation-profiles.json");

    var result = repo.LoadConfig("sdl2-gfx");

    await Assert.That(result.IsSuccess).IsTrue();
    var envelope = result.Value;
    await Assert.That(envelope.Config.Enabled).IsFalse();
    await Assert.That(envelope.Config.ProfileId).IsEqualTo("sdl2-gfx");
    await Assert.That(envelope.Config.ExportMacroNames).Contains("SDL2_GFXPRIMITIVES_SCOPE");
    await Assert.That(envelope.Config.HeaderSet).IsNull();
}
```

- [ ] **Step 2: Run the failing data tests**

Run:

```pwsh
dotnet test --project build/_build.Tests/Build.Tests.csproj -c Release --framework net10.0 --filter "FullyQualifiedName~BindingGenerationConfigRepository"
```

Expected: FAIL because `ProfileId`, `ExportMacroNames`, `LoadConfig`, and `BindingGenerationConfigEnvelope` do not exist.

- [ ] **Step 3: Add profile fields to the config model**

Patch `BindingGenerationConfig.cs` with:

```csharp
[JsonPropertyName("profile_id")] public required string ProfileId { get; init; }
[JsonPropertyName("export_macro_names")] public ImmutableList<string> ExportMacroNames { get; init; } = [];
```

Keep `ExportMacroNames` defaulted so fixture migration can be staged, but production manifest entries must carry it by the end of Task 2.

- [ ] **Step 4: Add the data envelope model**

Create `build/_build/Data/BindingGeneration/Models/BindingGenerationConfigEnvelope.cs`:

```csharp
using Build.Data.Manifest.Models;

namespace Build.Data.BindingGeneration.Models;

public sealed record BindingGenerationConfigEnvelope(
    string FamilyId,
    ManifestConfig Manifest,
    PackageFamilyConfig PackageFamily,
    LibraryManifest Library,
    BindingGenerationConfig Config);
```

- [ ] **Step 5: Add `LoadConfig` and configured-family enumeration**

Patch `IBindingGenerationConfigRepository`:

```csharp
Result<BindingGenerationConfigEnvelope, BindingGenerationConfigError> LoadConfig(string familyId);
Result<BindingGenerationConfig, BindingGenerationConfigError> Load(string familyId);
IReadOnlyList<string> EnumerateConfiguredFamilies();
IReadOnlyList<string> EnumerateGenerationEnabledFamilies();
IReadOnlyList<string> EnumerateEnabledFamilies();
```

Patch `BindingGenerationConfigRepository` so `LoadConfig` returns disabled configs and `Load` keeps generation-only disabled failure semantics:

```csharp
public Result<BindingGenerationConfigEnvelope, BindingGenerationConfigError> LoadConfig(string familyId)
{
    ArgumentException.ThrowIfNullOrWhiteSpace(familyId);

    var manifest = _manifestRepository.Load();
    var family = manifest.PackageFamilies.FirstOrDefault(pf => string.Equals(pf.Name, familyId, StringComparison.Ordinal));
    if (family is null)
    {
        return Result<BindingGenerationConfigEnvelope, BindingGenerationConfigError>
            .Failure(BindingGenerationConfigError.FamilyNotFound(familyId));
    }

    var lib = manifest.LibraryManifests.FirstOrDefault(l => string.Equals(l.Name, family.LibraryRef, StringComparison.Ordinal));
    if (lib is null)
    {
        return Result<BindingGenerationConfigEnvelope, BindingGenerationConfigError>
            .Failure(BindingGenerationConfigError.FamilyNotFound(familyId));
    }

    var config = lib.BindingGeneration with { FamilyId = familyId };
    return Result<BindingGenerationConfigEnvelope, BindingGenerationConfigError>
        .Success(new BindingGenerationConfigEnvelope(familyId, manifest, family, lib, config));
}
```

The envelope deliberately carries the manifest root so target-local planning can derive cross-family facts such as a satellite's core managed namespace without adding `binding_generation.core_*` JSON fields.

Then make `Load` call `LoadConfig` and return `Disabled` only when a generation caller asks for a disabled family.

- [ ] **Step 6: Update production and fixture manifests**

For `sdl2-core` add:

```json
"profile_id": "sdl2-core",
"export_macro_names": ["DECLSPEC"],
```

For SDL2.Image/Mixer/Ttf disabled placeholders add:

```json
"profile_id": "sdl2-satellite",
"export_macro_names": ["DECLSPEC"],
```

For SDL2.Gfx disabled placeholder add:

```json
"profile_id": "sdl2-gfx",
"export_macro_names": [
  "SDL2_GFXPRIMITIVES_SCOPE",
  "SDL2_IMAGEFILTER_SCOPE",
  "SDL2_ROTOZOOM_SCOPE",
  "SDL2_FRAMERATE_SCOPE"
],
```

- [ ] **Step 7: Update fixture builders**

Patch `ManifestFixture.CreateTestCoreBindingGeneration`:

```csharp
ProfileId = "sdl2-core",
ExportMacroNames = ["DECLSPEC"],
```

Patch `CreateTestSatellitePlaceholderBindingGeneration` to accept `profileId` and `exportMacroNames` or set defaults for normal satellites:

```csharp
ProfileId = profileId,
ExportMacroNames = [.. exportMacroNames],
```

Patch `BindingGenerationFixture.Sdl2CoreConfig` and `Sdl2ImagePlaceholderConfig` with the same fields.

- [ ] **Step 8: Run data tests**

Run:

```pwsh
dotnet test --project build/_build.Tests/Build.Tests.csproj -c Release --framework net10.0 --filter "FullyQualifiedName~BindingGenerationConfigRepository"
```

Expected: PASS.

## Task 3: Add Profile Registry And Profile Value Types

**Files:**
- Create: `build/_build/Targets/GenerateBindings/Profiles/BindingGenerationProfile.cs`
- Create: `build/_build/Targets/GenerateBindings/Profiles/BindingGenerationProfileRegistry.cs`
- Create: `build/_build/Targets/GenerateBindings/Profiles/DeclarationVisibilityStrategy.cs`
- Create: `build/_build/Targets/GenerateBindings/Profiles/Sdl2CoreProfile.cs`
- Create: `build/_build/Targets/GenerateBindings/Profiles/Sdl2SatelliteProfile.cs`
- Create: `build/_build/Targets/GenerateBindings/Profiles/Sdl2GfxProfile.cs`
- Modify: `build/_build/Targets/GenerateBindings/ServiceCollectionExtensions.cs`
- Create: `build/_build.Tests/Unit/Targets/GenerateBindings/Profiles/BindingGenerationProfileRegistryTests.cs`
- Create: `build/_build.Tests/Unit/Targets/GenerateBindings/Profiles/DeclarationVisibilityStrategyTests.cs`

- [ ] **Step 1: Write failing profile registry tests**

Create `BindingGenerationProfileRegistryTests.cs` with tests:

```csharp
namespace Build.Tests.Unit.Targets.GenerateBindings.Profiles;

public sealed class BindingGenerationProfileRegistryTests
{
    [Test]
    public async Task Resolve_Should_Return_Sdl2Core_Profile_When_ProfileId_Is_Sdl2Core()
    {
        var registry = new BindingGenerationProfileRegistry();

        var result = registry.Resolve("sdl2-core", ["DECLSPEC"]);

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value.ProfileId).IsEqualTo("sdl2-core");
        await Assert.That(result.Value.BoolWirePolicy).IsEqualTo(SdlBoolWirePolicy.Sdl2Int32);
        await Assert.That(result.Value.DeclarationVisibility.RequiredMacroNames).Contains("DECLSPEC");
    }

    [Test]
    public async Task Resolve_Should_Return_Sdl2Satellite_Profile_When_ProfileId_Is_Sdl2Satellite()
    {
        var registry = new BindingGenerationProfileRegistry();

        var result = registry.Resolve("sdl2-satellite", ["DECLSPEC"]);

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value.RequiresCoreFamilyReference).IsTrue();
        await Assert.That(result.Value.DeclarationVisibility.RequiredMacroNames).Contains("DECLSPEC");
    }

    [Test]
    public async Task Resolve_Should_Return_Sdl2Gfx_Profile_When_ProfileId_Is_Sdl2Gfx()
    {
        var registry = new BindingGenerationProfileRegistry();

        var result = registry.Resolve("sdl2-gfx", ["SDL2_GFXPRIMITIVES_SCOPE", "SDL2_ROTOZOOM_SCOPE"]);

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value.DeclarationVisibility.Kind).IsEqualTo(DeclarationVisibilityKind.Sdl2GfxScopeMacros);
        await Assert.That(result.Value.DeclarationVisibility.RequiredMacroNames).Contains("SDL2_GFXPRIMITIVES_SCOPE");
    }

    [Test]
    public async Task Resolve_Should_Fail_When_ProfileId_Is_Unknown()
    {
        var registry = new BindingGenerationProfileRegistry();

        var result = registry.Resolve("sdl9-unknown", ["DECLSPEC"]);

        await Assert.That(result.IsFailure).IsTrue();
        await Assert.That(result.Error.Reason).Contains("sdl9-unknown");
    }
}
```

- [ ] **Step 2: Run the failing profile tests**

Run:

```pwsh
dotnet test --project build/_build.Tests/Build.Tests.csproj -c Release --framework net10.0 --filter "FullyQualifiedName~GenerateBindings.Profiles"
```

Expected: FAIL because profile types do not exist.

- [ ] **Step 3: Add profile value types**

Create `BindingGenerationProfile.cs`:

```csharp
namespace Build.Targets.GenerateBindings.Profiles;

public enum SdlBoolWirePolicy
{
    Sdl2Int32,
}

public sealed record BindingGenerationProfile(
    string ProfileId,
    SdlBoolWirePolicy BoolWirePolicy,
    DeclarationVisibilityStrategy DeclarationVisibility,
    bool RequiresCoreFamilyReference,
    bool UsesFullSdl2CorePlatformCatalog);
```

Create `DeclarationVisibilityStrategy.cs`:

```csharp
namespace Build.Targets.GenerateBindings.Profiles;

public enum DeclarationVisibilityKind
{
    SdlDeclspec,
    Sdl2GfxScopeMacros,
}

public sealed record DeclarationVisibilityStrategy(
    DeclarationVisibilityKind Kind,
    IReadOnlyList<string> RequiredMacroNames)
{
    public static DeclarationVisibilityStrategy SdlDeclspec(IReadOnlyList<string> macroNames) =>
        new(DeclarationVisibilityKind.SdlDeclspec, macroNames);

    public static DeclarationVisibilityStrategy Sdl2GfxScopeMacros(IReadOnlyList<string> macroNames) =>
        new(DeclarationVisibilityKind.Sdl2GfxScopeMacros, macroNames);
}
```

- [ ] **Step 4: Add concrete SDL2 profiles**

Create `Sdl2CoreProfile.cs`:

```csharp
namespace Build.Targets.GenerateBindings.Profiles;

internal static class Sdl2CoreProfile
{
    public const string Id = "sdl2-core";

    public static BindingGenerationProfile Create(IReadOnlyList<string> exportMacroNames) =>
        new(
            ProfileId: Id,
            BoolWirePolicy: SdlBoolWirePolicy.Sdl2Int32,
            DeclarationVisibility: DeclarationVisibilityStrategy.SdlDeclspec(exportMacroNames),
            RequiresCoreFamilyReference: false,
            UsesFullSdl2CorePlatformCatalog: true);
}
```

Create `Sdl2SatelliteProfile.cs` and `Sdl2GfxProfile.cs` with the same shape, setting `RequiresCoreFamilyReference: true`; `Sdl2GfxProfile` uses `DeclarationVisibilityStrategy.Sdl2GfxScopeMacros(exportMacroNames)`.

- [ ] **Step 5: Add registry and error type**

Create `BindingGenerationProfileRegistry.cs`:

```csharp
using Build.Results;

namespace Build.Targets.GenerateBindings.Profiles;

public sealed record BindingGenerationProfileError(string Reason);

public interface IBindingGenerationProfileRegistry
{
    Result<BindingGenerationProfile, BindingGenerationProfileError> Resolve(string profileId, IReadOnlyList<string> exportMacroNames);
}

public sealed class BindingGenerationProfileRegistry : IBindingGenerationProfileRegistry
{
    public Result<BindingGenerationProfile, BindingGenerationProfileError> Resolve(string profileId, IReadOnlyList<string> exportMacroNames)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(profileId);
        ArgumentNullException.ThrowIfNull(exportMacroNames);

        return profileId switch
        {
            Sdl2CoreProfile.Id => Result<BindingGenerationProfile, BindingGenerationProfileError>.Success(Sdl2CoreProfile.Create(exportMacroNames)),
            Sdl2SatelliteProfile.Id => Result<BindingGenerationProfile, BindingGenerationProfileError>.Success(Sdl2SatelliteProfile.Create(exportMacroNames)),
            Sdl2GfxProfile.Id => Result<BindingGenerationProfile, BindingGenerationProfileError>.Success(Sdl2GfxProfile.Create(exportMacroNames)),
            _ => Result<BindingGenerationProfile, BindingGenerationProfileError>.Failure(new BindingGenerationProfileError($"Unknown binding-generation profile_id '{profileId}'.")),
        };
    }
}
```

Do not add an empty macro-name overload in M3 tests. Passing the manifest-provided `export_macro_names` through the registry is part of the profile-boundary contract; an empty overload would make the tests too weak to catch broken token flow.

- [ ] **Step 6: Register profile services**

Patch `ServiceCollectionExtensions.AddGenerateBindings`:

```csharp
services.AddSingleton<IBindingGenerationProfileRegistry, BindingGenerationProfileRegistry>();
```

- [ ] **Step 7: Run profile tests**

Run:

```pwsh
dotnet test --project build/_build.Tests/Build.Tests.csproj -c Release --framework net10.0 --filter "FullyQualifiedName~GenerateBindings.Profiles"
```

Expected: PASS.

## Task 4: Add Plan Resolver And Resolution Contracts

**Files:**
- Create: `build/_build/Targets/GenerateBindings/Planning/BindingGenerationResolutionError.cs`
- Create: `build/_build/Targets/GenerateBindings/Planning/ResolvedBindingGenerationConfig.cs`
- Create: `build/_build/Targets/GenerateBindings/Planning/ResolvedBindingGenerationPlan.cs`
- Create: `build/_build/Targets/GenerateBindings/Planning/ResolvedBindingGenerationAuditPlan.cs`
- Create: `build/_build/Targets/GenerateBindings/Planning/BindingGenerationPlanResolver.cs`
- Modify: `build/_build/Targets/GenerateBindings/ServiceCollectionExtensions.cs`
- Create: `build/_build.Tests/Unit/Targets/GenerateBindings/Planning/BindingGenerationPlanResolverTests.cs`
- Create: `build/_build.Tests/Fixtures/Data/Manifest/manifest-binding-generation-invalid-native-import.json`

- [ ] **Step 1: Write failing plan resolver tests**

Create tests with these names:

```csharp
ResolveConfig_Should_Return_Config_When_Sdl2Core_Is_Enabled
ResolveConfig_Should_Return_DisabledPlaceholder_When_Sdl2Image_Is_Disabled
ResolveConfig_Should_Fail_When_ProfileId_Is_Unknown
ResolveConfig_Should_Derive_RawAbiClassName_From_PrimaryClassName
ResolveConfig_Should_Derive_NativeImportName_From_PrimaryBinaryMetadata
ResolveConfig_Should_Derive_CoreReference_From_DependsOn_For_Satellite
ResolveConfig_Should_Derive_CoreManagedNamespace_From_DependsOn_For_Satellite
ResolveConfig_Should_Fail_When_NativeImportName_Cannot_Be_Derived_From_PrimaryBinaryMetadata
ResolveGenerationPlan_Should_Return_Plan_When_Sdl2Core_Is_Enabled
ResolveGenerationPlan_Should_Fail_When_Family_Is_Disabled
ResolveGenerationPlan_Should_Fail_When_Enabled_Family_Has_No_HeaderSet
ResolveEnabledGenerationPlans_Should_Fail_When_Any_Enabled_Family_Cannot_Be_Resolved
```

The disabled-image assertion should verify:

```csharp
await Assert.That(result.Value.Enabled).IsFalse();
await Assert.That(result.Value.Profile.ProfileId).IsEqualTo("sdl2-satellite");
await Assert.That(result.Value.HeaderSet).IsNull();
```

The satellite core-reference assertion should verify both fields are derived from `package_families[].depends_on` and the referenced core family's binding config:

```csharp
await Assert.That(result.Value.CoreFamilyId).IsEqualTo("sdl2-core");
await Assert.That(result.Value.CoreManagedNamespace).IsEqualTo("SDL2");
```

- [ ] **Step 2: Run the failing resolver tests**

Run:

```pwsh
dotnet test --project build/_build.Tests/Build.Tests.csproj -c Release --framework net10.0 --filter "FullyQualifiedName~GenerateBindings.Planning"
```

Expected: FAIL because planning types do not exist.

- [ ] **Step 3: Add resolution error type**

Create `BindingGenerationResolutionError.cs`:

```csharp
namespace Build.Targets.GenerateBindings.Planning;

public enum BindingGenerationResolutionErrorKind
{
    ConfigLoadFailed,
    UnknownProfile,
    ProfileMismatch,
    Disabled,
    MissingHeaderSet,
    MissingCoreFamilyReference,
    UnknownValidator,
}

public sealed record BindingGenerationResolutionError(
    string FamilyId,
    string? ProfileId,
    string Mode,
    string Reason,
    BindingGenerationResolutionErrorKind Kind);
```

Use `Mode` values such as `config`, `enabled-generation`, and `audit` so the task boundary can report which resolution path failed. Use `ProfileId = null` only before a manifest config has been read.

- [ ] **Step 4: Add resolved config and plan records**

Create `ResolvedBindingGenerationConfig.cs`:

```csharp
using Build.Data.BindingGeneration.Models;
using Build.Targets.GenerateBindings.Profiles;

namespace Build.Targets.GenerateBindings.Planning;

public sealed record ResolvedBindingGenerationConfig(
    string FamilyId,
    string LibraryName,
    bool Enabled,
    string ProfileId,
    BindingGenerationProfile Profile,
    string ManagedNamespace,
    string PrimaryClassName,
    string PlatformCatalogId,
    string RawAbiClassName,
    string NativeImportName,
    IReadOnlyList<string> OwnedPrefixes,
    string? CoreFamilyId,
    string? CoreManagedNamespace,
    BindingGenerationConfig SourceConfigForCurrentGenerator);
```

Create `ResolvedBindingGenerationPlan.cs`:

```csharp
using Build.Data.BindingGeneration.Models;
using Build.Targets.GenerateBindings.PlatformViews;

namespace Build.Targets.GenerateBindings.Planning;

public sealed record ResolvedBindingGenerationPlan(
    ResolvedBindingGenerationConfig ResolvedConfig,
    PlatformCatalog PlatformCatalog,
    HeaderSetConfig HeaderSet,
    IReadOnlyList<string> EnabledValidatorIds);
```

`ResolvedBindingGenerationConfig.SourceConfigForCurrentGenerator` is an explicit M3 bridge for existing parser, validator, and output plumbing that still accepts `BindingGenerationConfig`. New profile policy must read from `ResolvedBindingGenerationConfig` / `BindingGenerationProfile`; do not add new behavior that treats the raw config as authoritative.

Create `ResolvedBindingGenerationAuditPlan.cs`:

```csharp
namespace Build.Targets.GenerateBindings.Planning;

public sealed record ResolvedBindingGenerationAuditPlan(ResolvedBindingGenerationConfig ResolvedConfig);
```

- [ ] **Step 5: Add resolver interface and implementation**

Create `BindingGenerationPlanResolver.cs`:

```csharp
using Build.Data.BindingGeneration;
using Build.Results;
using Build.Targets.GenerateBindings.PlatformViews;
using Build.Targets.GenerateBindings.Profiles;

namespace Build.Targets.GenerateBindings.Planning;

public interface IBindingGenerationPlanResolver
{
    Result<ResolvedBindingGenerationConfig, BindingGenerationResolutionError> ResolveConfig(string familyId);
    Result<ResolvedBindingGenerationPlan, BindingGenerationResolutionError> ResolveGenerationPlan(string familyId);
    Result<IReadOnlyList<ResolvedBindingGenerationPlan>, BindingGenerationResolutionError> ResolveEnabledGenerationPlans();
}

public sealed class BindingGenerationPlanResolver(
    IBindingGenerationConfigRepository configRepository,
    IBindingGenerationProfileRegistry profileRegistry)
    : IBindingGenerationPlanResolver
{
    private readonly IBindingGenerationConfigRepository _configRepository = configRepository ?? throw new ArgumentNullException(nameof(configRepository));
    private readonly IBindingGenerationProfileRegistry _profileRegistry = profileRegistry ?? throw new ArgumentNullException(nameof(profileRegistry));

    public Result<ResolvedBindingGenerationConfig, BindingGenerationResolutionError> ResolveConfig(string familyId)
    {
        var configResult = _configRepository.LoadConfig(familyId);
        if (configResult.TryGetError(out var configError))
        {
            return Result<ResolvedBindingGenerationConfig, BindingGenerationResolutionError>.Failure(
                new BindingGenerationResolutionError(familyId, null, "config", configError.Reason, BindingGenerationResolutionErrorKind.ConfigLoadFailed));
        }

        var envelope = configResult.Value;
        var profileResult = _profileRegistry.Resolve(envelope.Config.ProfileId, envelope.Config.ExportMacroNames);
        if (profileResult.TryGetError(out var profileError))
        {
            return Result<ResolvedBindingGenerationConfig, BindingGenerationResolutionError>.Failure(
                new BindingGenerationResolutionError(familyId, envelope.Config.ProfileId, "config", profileError.Reason, BindingGenerationResolutionErrorKind.UnknownProfile));
        }

        var rawAbiClassName = envelope.Config.PrimaryClassName + "Native";
        var nativeImportNameResult = DeriveNativeImportName(familyId, envelope.Config.ProfileId, envelope.Library);
        if (nativeImportNameResult.TryGetError(out var nativeImportNameError))
        {
            return Result<ResolvedBindingGenerationConfig, BindingGenerationResolutionError>.Failure(nativeImportNameError);
        }

        var coreReferenceResult = ResolveCoreReference(familyId, envelope, profileResult.Value);
        if (coreReferenceResult.TryGetError(out var coreReferenceError))
        {
            return Result<ResolvedBindingGenerationConfig, BindingGenerationResolutionError>.Failure(coreReferenceError);
        }

        var coreReference = coreReferenceResult.Value;
        var coreFamilyId = profileResult.Value.RequiresCoreFamilyReference
            ? coreReference.FamilyId
            : null;
        var coreManagedNamespace = profileResult.Value.RequiresCoreFamilyReference
            ? coreReference.ManagedNamespace
            : null;

        return Result<ResolvedBindingGenerationConfig, BindingGenerationResolutionError>.Success(
            new ResolvedBindingGenerationConfig(
                FamilyId: familyId,
                LibraryName: envelope.Library.Name,
                Enabled: envelope.Config.Enabled,
                ProfileId: envelope.Config.ProfileId,
                Profile: profileResult.Value,
                ManagedNamespace: envelope.Config.ManagedNamespace,
                PrimaryClassName: envelope.Config.PrimaryClassName,
                PlatformCatalogId: envelope.Config.PlatformCatalogId,
                RawAbiClassName: rawAbiClassName,
                NativeImportName: nativeImportNameResult.Value,
                OwnedPrefixes: envelope.Config.OwnedPrefixes,
                CoreFamilyId: coreFamilyId,
                CoreManagedNamespace: coreManagedNamespace,
                SourceConfigForCurrentGenerator: envelope.Config));
    }

    public Result<ResolvedBindingGenerationPlan, BindingGenerationResolutionError> ResolveGenerationPlan(string familyId)
    {
        var configResult = ResolveConfig(familyId);
        if (configResult.TryGetError(out var error))
        {
            return Result<ResolvedBindingGenerationPlan, BindingGenerationResolutionError>.Failure(error);
        }

        var resolved = configResult.Value;
        if (!resolved.Enabled)
        {
            return Result<ResolvedBindingGenerationPlan, BindingGenerationResolutionError>.Failure(
                new BindingGenerationResolutionError(familyId, resolved.ProfileId, "enabled-generation", $"Family '{familyId}' has binding_generation.enabled=false.", BindingGenerationResolutionErrorKind.Disabled));
        }

        var sourceConfig = resolved.SourceConfigForCurrentGenerator;
        if (sourceConfig.HeaderSet is null)
        {
            return Result<ResolvedBindingGenerationPlan, BindingGenerationResolutionError>.Failure(
                new BindingGenerationResolutionError(familyId, resolved.ProfileId, "enabled-generation", $"Family '{familyId}' is enabled but binding_generation.header_set is missing.", BindingGenerationResolutionErrorKind.MissingHeaderSet));
        }

        return Result<ResolvedBindingGenerationPlan, BindingGenerationResolutionError>.Success(
            new ResolvedBindingGenerationPlan(
                resolved,
                PlatformCatalog.For(resolved.PlatformCatalogId),
                sourceConfig.HeaderSet,
                sourceConfig.Validators.Where(kv => kv.Value).Select(kv => kv.Key).ToArray()));
    }

    public Result<IReadOnlyList<ResolvedBindingGenerationPlan>, BindingGenerationResolutionError> ResolveEnabledGenerationPlans()
    {
        var plans = new List<ResolvedBindingGenerationPlan>();
        foreach (var familyId in _configRepository.EnumerateGenerationEnabledFamilies())
        {
            var planResult = ResolveGenerationPlan(familyId);
            if (planResult.TryGetError(out var error))
            {
                return Result<IReadOnlyList<ResolvedBindingGenerationPlan>, BindingGenerationResolutionError>.Failure(error);
            }

            if (planResult.TryGetValue(out var plan))
            {
                plans.Add(plan);
            }
        }

        return Result<IReadOnlyList<ResolvedBindingGenerationPlan>, BindingGenerationResolutionError>.Success(plans);
    }
}
```

The snippet intentionally keeps enabled-plan enumeration typed. Do not reintroduce a LINQ projection that resolves each family and then accesses `Value` inline; expected failures must reach `GenerateBindingsTask` as `BindingGenerationResolutionError` so the Cake boundary can translate them with family/profile/mode context.

Implement `DeriveNativeImportName` by preferring an exact Windows primary-binary pattern such as `SDL2_image.dll` and removing the `.dll` suffix; validate the derived basename is non-empty and has at least one matching primary-binary stem across `primary_binaries[]` (`SDL2_image.dll`, `libSDL2_image*`, `libSDL2_image*.dylib`). If the manifest metadata is too wildcarded to prove the import name, fail with `ProfileMismatch` rather than silently falling back to a new JSON field.

Implement `ResolveCoreReference` for satellite profiles by requiring exactly one `package_families[].depends_on` entry, resolving that family to its `library_ref`, then reading the referenced core library's `binding_generation.managed_namespace`. For SDL2 satellites the resolved values are `CoreFamilyId = "sdl2-core"` and `CoreManagedNamespace = "SDL2"`.

- [ ] **Step 6: Register plan resolver**

Patch `ServiceCollectionExtensions.AddGenerateBindings`:

```csharp
services.AddSingleton<IBindingGenerationPlanResolver, BindingGenerationPlanResolver>();
```

- [ ] **Step 7: Run resolver tests**

Run:

```pwsh
dotnet test --project build/_build.Tests/Build.Tests.csproj -c Release --framework net10.0 --filter "FullyQualifiedName~GenerateBindings.Planning"
```

Expected: PASS.

## Task 5: Add Pre-Parse Validation For Validator IDs And Profile Coherence

**Files:**
- Create: `build/_build/Validation/BindingGeneration/BindingGenerationPlanReadinessValidator.cs`
- Modify: `build/_build/Targets/GenerateBindings/Planning/BindingGenerationPlanResolver.cs`
- Create or extend: `build/_build.Tests/Unit/Targets/GenerateBindings/Planning/BindingGenerationPlanResolverTests.cs`
- Create: `build/_build.Tests/Fixtures/Data/Manifest/manifest-binding-generation-unknown-validator.json`
- Create: `build/_build.Tests/Fixtures/Data/Manifest/manifest-binding-generation-profile-mismatch.json`

- [ ] **Step 1: Write failing validation tests**

Add tests:

```csharp
ResolveGenerationPlan_Should_Fail_Before_Parse_When_ValidatorId_Is_Unknown
GenerateBindings_Should_Throw_CakeException_When_ValidatorId_Is_Unknown
ResolveConfig_Should_Fail_When_Sdl2Gfx_Profile_Has_No_Gfx_Export_Macros
ResolveConfig_Should_Fail_When_Sdl2Satellite_Has_No_Core_Dependency
```

Use fixture manifests so the behavior is driven by JSON shape. Preserve scenario-level unknown-validator coverage from the pre-M3 task flow, either by keeping the existing scenario test or replacing it with the `GenerateBindings_Should_Throw_CakeException_When_ValidatorId_Is_Unknown` scenario that exercises task-boundary translation.

- [ ] **Step 2: Run the failing validation tests**

Run:

```pwsh
dotnet test --project build/_build.Tests/Build.Tests.csproj -c Release --framework net10.0 --filter "FullyQualifiedName~GenerateBindings.Planning"
```

Expected: FAIL with resolver assertions not implemented.

- [ ] **Step 3: Add validator-id preflight inside generation resolution**

In `BindingGenerationPlanResolver.ResolveGenerationPlan`, compare enabled validator ids against registered `IBindingFamilyValidator.ValidatorId` values before returning a generation plan. If dependency direction makes that awkward in this slice, add a target-local validator-id catalog adapter injected into the resolver.

Expected error reason shape:

```text
GenerateBindings family 'sdl2-core' enables unknown validator id(s): example-unknown.
```

- [ ] **Step 4: Add profile coherence checks**

Add checks in profile resolution:

```csharp
if (profile.ProfileId == "sdl2-gfx" && !resolved.SourceConfigForCurrentGenerator.ExportMacroNames.Any(name => name.StartsWith("SDL2_", StringComparison.Ordinal)))
{
    return Failure(ProfileMismatch, "Profile 'sdl2-gfx' requires SDL2_gfx scope export macro names.");
}
```

For `sdl2-satellite` and `sdl2-gfx`, require exactly one core dependency and derive it from `package_families[].depends_on`.

- [ ] **Step 5: Run validation tests**

Run:

```pwsh
dotnet test --project build/_build.Tests/Build.Tests.csproj -c Release --framework net10.0 --filter "FullyQualifiedName~GenerateBindings.Planning"
```

Expected: PASS.

## Task 6: Integrate Plans Into GenerateBindings Task Flow

**Files:**
- Modify: `build/_build/Targets/GenerateBindings/GenerateBindingsTask.cs`
- Modify: `build/_build/Targets/GenerateBindings/BindingFamilyGeneration.cs`
- Modify: `build/_build.Tests/Scenarios/GenerateBindings/GenerateBindingsTaskScenarioTests.cs`
- Modify: `build/_build.Tests/Scenarios/GenerateBindings/GenerateBindingsTaskSnapshotTests.cs`

- [ ] **Step 1: Write failing task scenario tests**

Add or update scenario tests:

```csharp
GenerateBindings_Should_Resolve_Enabled_Generation_Plans_Only
GenerateBindings_Should_Not_Invoke_Generation_For_Disabled_Satellite
GenerateBindings_Should_Throw_CakeException_When_Plan_Resolution_Fails
GenerateBindings_Should_Throw_CakeException_With_Family_Profile_And_Mode_When_Plan_Resolution_Fails
```

Use `IBindingGenerationPlanResolver` substitutes so task tests do not need manifest/profile fixture setup.

- [ ] **Step 2: Run failing scenario tests**

Run:

```pwsh
dotnet test --project build/_build.Tests/Build.Tests.csproj -c Release --framework net10.0 --filter "FullyQualifiedName~Scenarios.GenerateBindings"
```

Expected: FAIL because `GenerateBindingsTask` still depends on `IBindingGenerationConfigRepository` and family ids.

- [ ] **Step 3: Update task constructor and run flow**

Patch `GenerateBindingsTask` constructor to take `IBindingGenerationPlanResolver` instead of `IBindingGenerationConfigRepository`.

Replace enabled family enumeration with:

```csharp
var plansResult = _planResolver.ResolveEnabledGenerationPlans();
if (plansResult.TryGetError(out var resolutionError))
{
    throw CreatePlanResolutionException(resolutionError);
}

var plans = plansResult.Value;
if (plans.Count == 0)
{
    throw new CakeException(
        "No enabled binding-generation families in manifest. Set binding_generation.enabled=true on at least one library_manifests[] entry.");
}

foreach (var plan in plans)
{
    await _familyGeneration.GenerateAsync(context, plan, context.CancellationToken).ConfigureAwait(false);
}
```

Add the task-boundary translation helper:

```csharp
private static CakeException CreatePlanResolutionException(BindingGenerationResolutionError error) =>
    new(
        $"GenerateBindings plan resolution failed for family '{error.FamilyId}', " +
        $"profile '{error.ProfileId ?? "<unresolved>"}', mode '{error.Mode}' ({error.Kind}): {error.Reason}");
```

Expected: typed planning failures remain typed below the Cake target and become `CakeException` only at `GenerateBindingsTask`, with family/profile/mode context in the message.

- [ ] **Step 4: Update family generation signature**

Change:

```csharp
public async Task GenerateAsync(BuildContext context, string familyId, CancellationToken ct)
```

to:

```csharp
public async Task GenerateAsync(BuildContext context, ResolvedBindingGenerationPlan plan, CancellationToken ct)
```

Use:

```csharp
var config = plan.ResolvedConfig.SourceConfigForCurrentGenerator;
var catalog = plan.PlatformCatalog;
```

Keep the validator bridge explicit:

```csharp
var report = await validator.ValidateAsync(model, config, ct).ConfigureAwait(false);
```

- [ ] **Step 5: Run scenario tests**

Run:

```pwsh
dotnet test --project build/_build.Tests/Build.Tests.csproj -c Release --framework net10.0 --filter "FullyQualifiedName~Scenarios.GenerateBindings"
```

Expected: PASS.

## Task 7: Move Emission Identity To The Resolved Plan

**Files:**
- Modify: `build/_build/Targets/GenerateBindings/Emit/BindingEmissionOptions.cs`
- Modify: `build/_build/Targets/GenerateBindings/Emit/RawAbi/RawAbiCommandEmitter.cs`
- Modify: `build/_build/Targets/GenerateBindings/BindingFamilyGeneration.cs`
- Modify: emitter tests under `build/_build.Tests/Unit/Targets/GenerateBindings/Emit/**`

- [ ] **Step 1: Write failing emitter tests for import name and raw class name**

Add tests that construct `BindingEmissionOptions` with non-core identity:

```csharp
var options = new BindingEmissionOptions(
    managedNamespace: "SDL2.Image",
    primaryClassName: "SDL_image",
    rawClassName: "SDL_imageNative",
    nativeImportName: "SDL2_image");
```

Assert emitted raw commands contain:

```csharp
internal static unsafe partial class SDL_imageNative
private const string LibName = "SDL2_image";
```

- [ ] **Step 2: Run failing emitter tests**

Run:

```pwsh
dotnet test --project build/_build.Tests/Build.Tests.csproj -c Release --framework net10.0 --filter "FullyQualifiedName~GenerateBindings.Emit"
```

Expected: FAIL because raw identity is derived locally and `LibName` is hardcoded to `SDL2`.

- [ ] **Step 3: Update emission options**

Patch `BindingEmissionOptions`:

```csharp
public sealed record BindingEmissionOptions
{
    public BindingEmissionOptions(string managedNamespace, string primaryClassName, string rawClassName, string nativeImportName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(managedNamespace);
        ArgumentException.ThrowIfNullOrWhiteSpace(primaryClassName);
        ArgumentException.ThrowIfNullOrWhiteSpace(rawClassName);
        ArgumentException.ThrowIfNullOrWhiteSpace(nativeImportName);

        ManagedNamespace = managedNamespace;
        PrimaryClassName = primaryClassName;
        RawClassName = rawClassName;
        NativeImportName = nativeImportName;
    }

    public string ManagedNamespace { get; }
    public string PrimaryClassName { get; }
    public string RawClassName { get; }
    public string NativeImportName { get; }
}
```

Add a factory from plan rather than raw config:

```csharp
public static BindingEmissionOptions FromPlan(ResolvedBindingGenerationPlan plan) =>
    new(
        plan.ResolvedConfig.ManagedNamespace,
        plan.ResolvedConfig.PrimaryClassName,
        plan.ResolvedConfig.RawAbiClassName,
        plan.ResolvedConfig.NativeImportName);
```

- [ ] **Step 4: Replace hardcoded `SDL2` import**

Patch `RawAbiCommandEmitter`:

```csharp
builder.Append("    private const string LibName = \"").Append(options.NativeImportName).AppendLf("\";");
```

- [ ] **Step 5: Use plan-based emission options**

Patch `BindingFamilyGeneration`:

```csharp
var fileSet = _emitter.Emit(model, BindingEmissionOptions.FromPlan(plan));
```

- [ ] **Step 6: Run emitter tests and scenario tests**

Run:

```pwsh
dotnet test --project build/_build.Tests/Build.Tests.csproj -c Release --framework net10.0 --filter "FullyQualifiedName~GenerateBindings.Emit|FullyQualifiedName~Scenarios.GenerateBindings"
```

Expected: PASS.

## Task 8: Select SDL Bool Policy Through Profiles Without Changing SDL2 Output

**Files:**
- Modify: `build/_build/Targets/GenerateBindings/ModelBuilding/Types/NativeTypeClassificationContext.cs`
- Modify: `build/_build/Targets/GenerateBindings/ModelBuilding/Types/TypeMappingPolicy.cs`
- Modify: `build/_build/Targets/GenerateBindings/ModelBuilding/BindingModelBuilder.cs`
- Modify: `build/_build/Targets/GenerateBindings/BindingFamilyGeneration.cs`
- Modify: `build/_build.Tests/Unit/Targets/GenerateBindings/ModelBuilding/Types/NativeTypeClassifierTests.cs`
- Create: `build/_build.Tests/Fixtures/Data/GenerateBindings/SemanticTypes/sdl2-satellite-callbacks-long-and-bool.h`

- [ ] **Step 1: Write profile-selected bool tests**

Add a test that builds a classification context from a resolved SDL2 profile and classifies `SDL_bool` as int-backed.

Expected assertion:

```csharp
await Assert.That(type.ManagedName).IsEqualTo("int");
await Assert.That(type.AbiShape.SizeBytes).IsEqualTo(4);
```

- [ ] **Step 2: Add fixture header for satellite bool/long opportunities**

Create `sdl2-satellite-callbacks-long-and-bool.h`:

```c
#pragma once

typedef int SDL_bool;
typedef long TTF_FontIndex;
typedef struct SDL_Color { unsigned char r; unsigned char g; unsigned char b; unsigned char a; } SDL_Color;
typedef struct TTF_Font TTF_Font;
typedef void (*Mix_EffectDone_t)(int channel, void *udata);

extern DECLSPEC SDL_bool Mix_Playing(int channel);
extern DECLSPEC TTF_Font *TTF_OpenFontIndex(const char *file, int ptsize, long index);
extern DECLSPEC int TTF_RenderGlyph_Blended(TTF_Font *font, unsigned short ch, SDL_Color fg);
extern DECLSPEC void Mix_RegisterEffect(int channel, Mix_EffectDone_t done, void *arg);
```

- [ ] **Step 3: Run failing type tests**

Run:

```pwsh
dotnet test --project build/_build.Tests/Build.Tests.csproj -c Release --framework net10.0 --filter "FullyQualifiedName~NativeTypeClassifier"
```

Expected: FAIL until bool policy is selected through the profile/context rather than a static SDL2 table.

- [ ] **Step 4: Add bool policy to classification context**

Patch `NativeTypeClassificationContext` to include:

```csharp
SdlBoolWirePolicy BoolWirePolicy
```

Update factory creation to consume the resolved plan/config profile. During migration, keep a `FromConfig` overload for tests only if necessary and make it select `SdlBoolWirePolicy.Sdl2Int32` explicitly.

- [ ] **Step 5: Move `SDL_bool` mapping out of static explicit typedef map**

Remove `SDL_bool` from `TypeMappingPolicy.ExplicitTypedefMap` and handle it in `NativeTypeClassifier.ClassifyTypedef` using the context's bool policy.

SDL2 mapping:

```csharp
private NativeTypeRef ClassifySdlBool(CppTypedef typedef, string? sourceHeader) =>
    new NativeTypeRef(typedef.Name, "int", NativeTypeKind.ValueTypedef, 0,
        _context.IsOwned(typedef.Name) ? _context.FamilyId : null,
        sourceHeader,
        NativeAbiShape.Of("int", 4),
        null,
        []);
```

- [ ] **Step 6: Run type tests**

Run:

```pwsh
dotnet test --project build/_build.Tests/Build.Tests.csproj -c Release --framework net10.0 --filter "FullyQualifiedName~NativeTypeClassifier"
```

Expected: PASS.

## Task 9: Retire `LegacyBindingTypeRefBridge`

**Files:**
- Delete: `build/_build/Targets/GenerateBindings/ModelBuilding/Types/LegacyBindingTypeRefBridge.cs`
- Modify: `build/_build/Targets/GenerateBindings/BindingFamilyGeneration.cs`
- Create: `build/_build/Targets/GenerateBindings/ModelBuilding/Types/RequiredFunctionNativeTypeParser.cs`
- Create: `build/_build.Tests/Unit/Targets/GenerateBindings/ModelBuilding/Types/RequiredFunctionNativeTypeParserTests.cs`

- [ ] **Step 1: Write failing parser tests**

Create tests:

```csharp
Parse_Should_Return_Primitive_When_Type_Is_Int
Parse_Should_Return_Primitive_When_Type_Is_Uint
Parse_Should_Return_Void_When_Type_Is_Void
Parse_Should_Return_Primitive_When_Type_Is_CLong
Parse_Should_Return_Primitive_When_Type_Is_CULong
Parse_Should_Return_Primitive_When_Type_Is_SdlBool
Parse_Should_Return_Utf8Pointer_When_Type_Is_ConstCharPointer
Parse_Should_Return_TypedPointer_When_Type_Is_WcharTPointer
Parse_Should_Return_TypedPointer_When_Type_Is_ConstStructPointer
Parse_Should_Return_TypedPointer_When_Type_Has_One_Pointer
Parse_Should_Return_TypedPointer_When_Type_Has_Two_Pointers
Parse_Should_Fail_When_Type_Is_Empty
Parse_Should_Fail_When_Type_Is_Unsupported
```

For `byte**`, assert pointer depth `2` and managed name `byte**`. For `const char *`, assert `NativeTypeKind.Utf8Pointer`, pointer depth `1`, and managed name `byte*`. For `wchar_t *`, assert `NativeTypeKind.TypedPointer`, pointer depth `1`, and managed name `nint`. For `long` and `unsigned long`, assert managed names `CLong` and `CULong` so the bridge retirement does not regress platform-sensitive C integer handling. For unsupported spellings such as `struct Unknown value` or `void (*callback)(int)`, assert a parser failure with the original spelling in the error reason.

- [ ] **Step 2: Run failing parser tests**

Run:

```pwsh
dotnet test --project build/_build.Tests/Build.Tests.csproj -c Release --framework net10.0 --filter "FullyQualifiedName~RequiredFunctionNativeTypeParser"
```

Expected: FAIL because the parser type does not exist.

- [ ] **Step 3: Add purpose-built required-function type parser**

Create `RequiredFunctionNativeTypeParser.cs`:

```csharp
using Build.Results;
using Build.Targets.GenerateBindings.Model;

namespace Build.Targets.GenerateBindings.ModelBuilding.Types;

internal sealed record RequiredFunctionNativeTypeParserError(string Type, string Reason);

internal static class RequiredFunctionNativeTypeParser
{
    public static Result<NativeTypeRef, RequiredFunctionNativeTypeParserError> Parse(string type)
    {
        if (string.IsNullOrWhiteSpace(type))
        {
            return Result<NativeTypeRef, RequiredFunctionNativeTypeParserError>.Failure(
                new RequiredFunctionNativeTypeParserError(type, "Required-function type is empty."));
        }

        var trimmed = type.Trim();
        trimmed = trimmed.Replace("const ", string.Empty, StringComparison.Ordinal).Trim();
        var pointerDepth = 0;
        while (trimmed.EndsWith('*'))
        {
            pointerDepth++;
            trimmed = trimmed[..^1].TrimEnd();
        }

        var elementResult = ParseElement(type, trimmed);
        if (elementResult.TryGetError(out var error))
        {
            return Result<NativeTypeRef, RequiredFunctionNativeTypeParserError>.Failure(error);
        }

        var element = elementResult.Value;
        if (pointerDepth == 0)
        {
            return Result<NativeTypeRef, RequiredFunctionNativeTypeParserError>.Success(element);
        }

        if (element.NativeName == "char" && pointerDepth == 1)
        {
            return Result<NativeTypeRef, RequiredFunctionNativeTypeParserError>.Success(
                new NativeTypeRef("char*", "byte*", NativeTypeKind.Utf8Pointer, 1, null, null, NativeAbiShape.Of("nint", IntPtr.Size), element, []));
        }

        if (element.NativeName == "wchar_t" && pointerDepth == 1)
        {
            return Result<NativeTypeRef, RequiredFunctionNativeTypeParserError>.Success(
                new NativeTypeRef("wchar_t*", "nint", NativeTypeKind.TypedPointer, 1, null, null, NativeAbiShape.Of("nint", IntPtr.Size), element, []));
        }

        return Result<NativeTypeRef, RequiredFunctionNativeTypeParserError>.Success(
            NativeTypeRef.Indirection(element, pointerDepth, element.ManagedName + new string('*', pointerDepth)));
    }

    private static Result<NativeTypeRef, RequiredFunctionNativeTypeParserError> ParseElement(string originalType, string normalizedType)
    {
        if (normalizedType.StartsWith("struct ", StringComparison.Ordinal))
        {
            var structName = normalizedType["struct ".Length..].Trim();
            return string.IsNullOrWhiteSpace(structName) || structName.Contains(' ')
                ? Result<NativeTypeRef, RequiredFunctionNativeTypeParserError>.Failure(
                    new RequiredFunctionNativeTypeParserError(originalType, $"Unsupported required-function type spelling '{originalType}'."))
                : Result<NativeTypeRef, RequiredFunctionNativeTypeParserError>.Success(
                    NativeTypeRef.OpaqueHandle(structName, structName, null, null));
        }

        var element = normalizedType switch
        {
            "void" => NativeTypeRef.Primitive("void", "void", NativeAbiShape.Of("void", 0, false)),
            "char" => NativeTypeRef.Primitive("char", "byte", NativeAbiShape.Of("byte", 1)),
            "byte" => NativeTypeRef.Primitive("byte", "byte", NativeAbiShape.Of("byte", 1)),
            "int" => NativeTypeRef.Primitive("int", "int", NativeAbiShape.Of("int", 4)),
            "uint" => NativeTypeRef.Primitive("uint", "uint", NativeAbiShape.Of("uint", 4)),
            "long" => NativeTypeRef.Primitive("long", "CLong", NativeAbiShape.Of("CLong")),
            "unsigned long" => NativeTypeRef.Primitive("unsigned long", "CULong", NativeAbiShape.Of("CULong")),
            "wchar_t" => NativeTypeRef.Primitive("wchar_t", "char", NativeAbiShape.Of("char", 2)),
            "SDL_bool" => NativeTypeRef.Primitive("SDL_bool", "int", NativeAbiShape.Of("int", 4)),
            _ => null,
        };

        return element is null
            ? Result<NativeTypeRef, RequiredFunctionNativeTypeParserError>.Failure(
                new RequiredFunctionNativeTypeParserError(originalType, $"Unsupported required-function type spelling '{originalType}'."))
            : Result<NativeTypeRef, RequiredFunctionNativeTypeParserError>.Success(element);
    }
}
```

- [ ] **Step 4: Replace required-function conversion**

Patch `BindingFamilyGeneration.ConvertRequiredFunctions`:

```csharp
ReturnType: ParseRequiredFunctionType(rf.Name, rf.ReturnType),
Parameters: [.. rf.Parameters.Select(p => new BindingParameter(ParseRequiredFunctionType(rf.Name, p.Type), p.Name))],
```

Add a local helper that translates parser failures into the same expected-failure path used for manifest-driven generation errors. Do not call `.Value` on parser results without first handling `TryGetError`; otherwise M3 replaces one spike bridge with a different hidden crash path.

- [ ] **Step 5: Delete bridge and remove references**

Delete:

```text
build/_build/Targets/GenerateBindings/ModelBuilding/Types/LegacyBindingTypeRefBridge.cs
```

Search:

```pwsh
rg "LegacyBindingTypeRefBridge" build/_build build/_build.Tests
```

Expected: no matches.

- [ ] **Step 6: Run parser and scenario tests**

Run:

```pwsh
dotnet test --project build/_build.Tests/Build.Tests.csproj -c Release --framework net10.0 --filter "FullyQualifiedName~RequiredFunctionNativeTypeParser|FullyQualifiedName~Scenarios.GenerateBindings"
```

Expected: PASS.

## Task 10: Add Declaration Visibility Strategy Fixtures

**Files:**
- Create: `build/_build.Tests/Fixtures/Data/GenerateBindings/SemanticTypes/sdl2-gfx-scope-macros.h`
- Create: `build/_build.Tests/Fixtures/Data/GenerateBindings/SemanticTypes/sdl2-satellite-declspec-and-core-types.h`
- Modify or create: relevant fixture parser/model tests under `build/_build.Tests/Unit/Targets/GenerateBindings/ModelBuilding/**`
- Modify: `build/_build/Targets/GenerateBindings/ModelBuilding/Declarations/BindableDeclarationPolicy.cs`
- Modify: `build/_build/Targets/GenerateBindings/ModelBuilding/Macros/MacroApiPolicy.cs`

- [ ] **Step 1: Add SDL2_gfx scope macro fixture**

Create `sdl2-gfx-scope-macros.h`:

```c
#pragma once

#define SDL2_GFXPRIMITIVES_SCOPE extern
#define SDL2_IMAGEFILTER_SCOPE extern
#define SDL2_ROTOZOOM_SCOPE extern
#define SDL2_FRAMERATE_SCOPE extern

typedef struct SDL_Surface SDL_Surface;
typedef struct FPSmanager FPSmanager;

SDL2_GFXPRIMITIVES_SCOPE int pixelColor(SDL_Surface *dst, short x, short y, unsigned int color);
SDL2_ROTOZOOM_SCOPE SDL_Surface *rotozoomSurface(SDL_Surface *src, double angle, double zoom, int smooth);
SDL2_IMAGEFILTER_SCOPE int SDL_imageFilterAdd(unsigned char *src1, unsigned char *src2, unsigned char *dst, unsigned int length);
SDL2_FRAMERATE_SCOPE void SDL_initFramerate(FPSmanager *manager);
```

- [ ] **Step 2: Add normal satellite fixture**

Create `sdl2-satellite-declspec-and-core-types.h`:

```c
#pragma once

#define DECLSPEC extern

typedef struct SDL_Surface SDL_Surface;
typedef struct SDL_RWops SDL_RWops;
typedef struct IMG_Animation IMG_Animation;

DECLSPEC SDL_Surface *IMG_Load(const char *file);
DECLSPEC SDL_Surface *IMG_Load_RW(SDL_RWops *src, int freesrc);
DECLSPEC void IMG_FreeAnimation(IMG_Animation *anim);
```

- [ ] **Step 3: Write fixture tests for declaration visibility and core references**

Add tests proving:

```text
sdl2-gfx profile recognizes scope-macro declarations as bindable
sdl2-satellite profile recognizes DECLSPEC declarations as bindable
core-owned SDL_Surface / SDL_RWops are not emitted as satellite-owned types
real installed SDL2_gfx headers still contain at least one configured scope macro token when opt-in real-header tests are enabled
```

Use existing fixture parser helpers under `build/_build.Tests/Fixtures/GenerateBindings` rather than string-only parser tests.

CppAst sees preprocessed declarations, so do not make the whole strategy depend on recovering original macro tokens from `CppFunction` nodes after preprocessing. Use embedded `.h` fixtures to prove the policy seam and bindable-declaration behavior, and use a narrow real-header text audit only as evidence that configured `export_macro_names` still match upstream header tokens.

- [ ] **Step 4: Run failing fixture tests**

Run:

```pwsh
dotnet test --project build/_build.Tests/Build.Tests.csproj -c Release --framework net10.0 --filter "FullyQualifiedName~GenerateBindings.ModelBuilding"
```

Expected: FAIL until declaration policy consumes profile visibility instead of hardcoded SDL2 header/name checks.

- [ ] **Step 5: Inject declaration visibility policy into bindable declaration and macro policies**

Make declaration inclusion use the resolved profile's visibility strategy instead of hardcoded assumptions such as only `/SDL2/` and `SDL_`.

Keep SDL2.Core output unchanged by preserving the current core strategy for `sdl2-core`.

- [ ] **Step 6: Run model-building tests**

Run:

```pwsh
dotnet test --project build/_build.Tests/Build.Tests.csproj -c Release --framework net10.0 --filter "FullyQualifiedName~GenerateBindings.ModelBuilding"
```

Expected: PASS.

## Task 11: Add Disabled Satellite Audit Path Without Emission

**Files:**
- Modify: `build/_build/Targets/GenerateBindings/Planning/BindingGenerationPlanResolver.cs`
- Modify: `build/_build.Tests/Unit/Targets/GenerateBindings/Planning/BindingGenerationPlanResolverTests.cs`
- Optional create: `build/_build.Tests/Unit/Targets/GenerateBindings/Planning/BindingGenerationAuditPlanTests.cs`

- [ ] **Step 1: Write disabled audit tests**

Add tests:

```csharp
ResolveAuditPlan_Should_Return_AuditPlan_When_Sdl2Image_Is_Disabled
ResolveAuditPlan_Should_Return_AuditPlan_When_Sdl2Gfx_Is_Disabled
ResolveAuditPlan_Should_Not_Require_HeaderSet_For_DisabledPlaceholder
```

- [ ] **Step 2: Run failing audit tests**

Run:

```pwsh
dotnet test --project build/_build.Tests/Build.Tests.csproj -c Release --framework net10.0 --filter "FullyQualifiedName~GenerateBindings.Planning"
```

Expected: FAIL until `ResolveAuditPlan` is implemented.

- [ ] **Step 3: Implement audit resolution**

Add to `IBindingGenerationPlanResolver`:

```csharp
Result<ResolvedBindingGenerationAuditPlan, BindingGenerationResolutionError> ResolveAuditPlan(string familyId);
```

Implementation delegates to `ResolveConfig` and wraps the resolved config. It does not call header resolution, parser, model builder, emitter, validators, output cleanup, compile-check, or packaging.

- [ ] **Step 4: Run audit tests**

Run:

```pwsh
dotnet test --project build/_build.Tests/Build.Tests.csproj -c Release --framework net10.0 --filter "FullyQualifiedName~GenerateBindings.Planning"
```

Expected: PASS.

## Task 12: Focused Linux/Header Readiness Command

**Files:**
- Modify: `docs/playbook/binding-generator-maintenance.md`
- Optional create: `build/_build.Tests/Unit/Targets/GenerateBindings/Profiles/RealHeaderReadinessTests.cs` with opt-in skip guard

- [ ] **Step 1: Document focused Docker command override**

Add this command pattern to the maintenance playbook:

```pwsh
$repo = (Get-Location).Path
docker build -f docker/binding-generator.Dockerfile -t janset-binding-generator:focal-latest .
docker run --rm --entrypoint bash `
  -v "${repo}:/workspace" `
  -w /workspace `
  -e REPO_ROOT=/workspace `
  -e RID=linux-x64 `
  janset-binding-generator:focal-latest `
  -lc "dotnet test --project /workspace/build/_build.Tests/Build.Tests.csproj -c Release --framework net10.0 --filter 'FullyQualifiedName~RealHeaderReadiness'"
```

Expected: builds or refreshes the local binding-generator image tag, mounts the repo at `/workspace`, runs a focused Linux/header test inside the image without invoking the `binding-generator` entrypoint, and avoids full generation.

- [ ] **Step 2: Add an opt-in readiness test only if implementation has a narrow check**

If the slice adds a focused test, guard it with an environment variable such as `JANSET_BINDING_REAL_HEADER_TESTS=1` so normal managed test runs stay native-free.

The test must assert a narrow fact, such as installed SDL2_gfx headers being discoverable or export macro names being present in header text. It must not emit generated satellite files.

- [ ] **Step 3: Run managed tests**

Run:

```pwsh
dotnet test --project build/_build.Tests/Build.Tests.csproj -c Release --framework net10.0 --filter "FullyQualifiedName~GenerateBindings"
```

Expected: PASS. Opt-in Linux readiness tests remain skipped unless the environment variable is set.

## Task 13: Documentation Updates

**Files:**
- Modify: `docs/binding-autogen/binding-generator-roadmap.md`
- Modify: `docs/binding-autogen/binding-generator-constitution.md`
- Modify: `docs/playbook/binding-generator-maintenance.md`
- Modify: `docs/superpowers/specs/2026-05-20-m3-binding-profile-boundary-design.md`

- [ ] **Step 1: Update roadmap M3 section**

Ensure `binding-generator-roadmap.md` links to this plan:

```markdown
**Detailed plan:** [`milestone-3-profile-boundary.md`](milestones/milestone-3-profile-boundary.md).
```

Replace ambiguous `Profile/config-owned family identity` text with the M3 authority split:

```text
Manifest owns reviewable family facts and explicit exceptions.
Profile code owns defaults, interpretation, and SDL-family policy selection.
Engine code owns ABI mechanics and C-to-C# translation semantics.
```

- [ ] **Step 2: Update constitution with accepted M3 field decisions**

Update `Manifest Configuration Vs Code-Owned Policy` so it says:

```text
M3 promotes profile_id as a required binding_generation family fact. export_macro_names may be promoted as token inventory when tests prove it earns its place. raw ABI class, native import name, and core reference are derived first and become manifest fields only when a RED test proves convention is insufficient.
```

Also state that `profile_id` is a routing key into code-owned profile policy, not a JSON behavior switch.

- [ ] **Step 3: Update maintenance playbook**

Record the profile boundary maintenance rules:

```text
When adding a satellite to generation, first add/verify profile_id, export macro token inventory, core dependency derivation, and disabled/audit resolution tests. Use focused Docker command overrides for Linux/header checks instead of running full generation for narrow readiness facts.
```

- [ ] **Step 4: Mark the superpowers spec accepted**

Change the spec status line to:

```markdown
**Status:** Accepted design; executable plan lives in [`../../binding-autogen/milestones/milestone-3-profile-boundary.md`](../../binding-autogen/milestones/milestone-3-profile-boundary.md).
```

- [ ] **Step 5: Run documentation hygiene checks**

Run:

```pwsh
git --no-pager diff --check
```

Expected: no output.

## Task 14: Milestone Verification

**Files:**
- No new files unless verification reveals a real gap.

- [ ] **Step 1: Run focused binding-generation tests**

Run:

```pwsh
dotnet test --project build/_build.Tests/Build.Tests.csproj -c Release --framework net10.0 --filter "FullyQualifiedName~BindingGeneration|FullyQualifiedName~GenerateBindings"
```

Expected: PASS.

- [ ] **Step 2: Run full build-host regression suite**

Run:

```pwsh
dotnet test --project build/_build.Tests/Build.Tests.csproj -c Release --framework net10.0
```

Expected: PASS.

- [ ] **Step 3: Run slopwatch**

Run:

```pwsh
slopwatch analyze --fail-on warning --exclude "artifacts/**,external/**,vcpkg_installed/**,**/bin/**,**/obj/**"
```

Expected: zero warning-or-higher issues.

- [ ] **Step 4: Run canonical generation if Docker is available**

Run:

```pwsh
dotnet run --file tools.cs -- generate-bindings
```

Expected: SDL2.Core generation succeeds and satellite families remain non-emitting.

- [ ] **Step 5: Run compile-check if generated preview exists**

Run:

```pwsh
dotnet build tests/binding-compile-check/SDL2.Core.CompileCheck.csproj -c Release
```

Expected: PASS when generated preview exists.

- [ ] **Step 6: Review SDL2.Core generated-output diff**

Run:

```pwsh
git status --short
```

Expected: no unexpected source changes. Generated preview under `artifacts/` is ignored. Any tracked generated-output delta must be tied to an approved RED test.

- [ ] **Step 7: Stop for commit approval**

Prepare a summary and proposed commit message. Do not commit until Deniz approves.

Suggested message:

```text
feat(binding-autogen): add M3 profile boundary
```
