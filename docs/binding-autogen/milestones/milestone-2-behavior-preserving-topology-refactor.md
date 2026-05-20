# Milestone 2 Behavior-Preserving Topology Refactor Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Refactor `GenerateBindings` into durable target-local concepts without changing generated SDL2.Core output, while allowing only RED-test-proven safety fixes for real refactor-blocking correctness gaps.

**Architecture:** Keep the generator target-centric under `build/_build/Targets/GenerateBindings/`. Extract the per-family workflow from the Cake task, split current buckets into parse/platform/model-building/emission concepts, collapse one-line records into cohesive model/report files, and make SDL2-specific policy visible without implementing the future SDL2/SDL3 profile system in this milestone.

**Tech Stack:** .NET 10, C# 14, Cake Frosting, CppAst, TUnit, Microsoft.Testing.Platform, Verify.TUnit snapshots, Cake `FakeCakeWorld`, `tools.cs generate-bindings`, `tests/binding-compile-check/SDL2.Core.CompileCheck.csproj`.

---

## Non-Negotiables

- Preserve generated SDL2.Core output. Snapshot content diffs are blockers unless a RED safety test explicitly justifies the behavior change.
- Use `git mv` for every tracked file move/rename.
- Do not introduce `Shared`, `Common`, `Features`, `Pipeline`, `Runner`, `Operation`, or `Processor` as replacement buckets.
- Keep the generator target-local. Do not promote generator code outside `Targets/GenerateBindings/` until a second real target consumes it.
- Do not create empty architecture folders. `Profiles/` and `Emit/Friendly/` are future concepts unless M2 adds real code there.
- Do not convert every static class to DI. Convert only classes that own workflow, policy composition, test-worthy behavior, or profile/config-driven decisions.
- Do not create ceremonial interfaces. Concrete `public sealed` / `internal sealed` classes are the default.
- Do not change `build/manifest.json` schema in M2.
- Do not implement public typed methods, friendly overloads, `LibraryImport` backend splitting, SDL2 satellite generation, SDL3 support, or production source flip.
- Stop before each commit. Present a summary and proposed commit message; wait for Deniz approval.

## North Star

This milestone exists because SDL2.Core internal ABI generation works, but the code that got us here accumulated topology debt:

- `GenerateBindingsTask` hides the generation story behind private methods.
- `Translation/` mixes declaration filtering, type classification, function merge, macro taxonomy, and model composition.
- `Emitting/` mixes file-set orchestration, raw ABI emission, public generated type artifacts, report construction, and TFM guards.
- SDL2-specific concerns are spread across multiple policies instead of being visible seams for the future SDL2/SDL3 profile milestone.
- One-line records and DTOs are scattered enough that navigation feels like filing-cabinet spelunking.

M2 fixes the shape, not the product surface. The result should make M3-M7 easier, not secretly start them.

## Design Rules

### Task-Centric Build Host Rules

Apply ADR-002 and the extraction guidelines directly:

- `GenerateBindingsTask` remains meaningful and owns target lifecycle orchestration.
- Multi-step per-family generation becomes a named collaborator because it is workflow behavior, not local task mechanics.
- Cake-native IO stays visible at build boundaries.
- Pure model-building and emitter policies stay Cake-free where possible.
- Validators remain cross-cutting under `Build.Validation.BindingGeneration`.
- Data-boundary repositories remain under `Build.Data.BindingGeneration`.

### Static Class Decision Rule

Keep a static class when it is:

- pure;
- stateless;
- not configured through DI or future profiles;
- a narrow formatter, factory, or deterministic helper;
- easy to understand from its method signature.

Convert to an instance collaborator when it:

- owns workflow order;
- composes multiple collaborators;
- has policy branches that deserve independent tests;
- needs injected config/profile/policy dependencies;
- crosses Cake IO/logging boundaries;
- would otherwise keep growing private helper chains.

Do not add an interface unless there is a real second implementation, external/process-backed seam, validator/data-boundary convention, or important task/test contract.

### Manifest Vs Policy Boundary

M2 should make this boundary more discoverable, but not change behavior.

Currently manifest-owned facts:

- family id;
- enabled state;
- managed namespace;
- public class name;
- header set;
- owned prefixes;
- parse defines/undefines/clang args;
- explicit excluded/required/deferred declarations;
- macro overrides/exclusions;
- validator opt-ins.

Code-owned policy:

- scalar width mapping;
- SDL2/SDL3 bool shape;
- opaque handle detection;
- callback and function-pointer classification;
- macro taxonomy;
- struct/union layout rules;
- platform view merge rules;
- TFM/backend compatibility rules.

M2 may move hardcoded SDL2 checks into clearer policy classes. M2 must not replace them with a full profile system; that belongs to M3.

Current M2 code may still derive or hardcode some family facts that the manifest does not yet expose, such as the raw ABI class name and native import library name. Do not add manifest schema fields for those in M2. Make those seams visible through named code concepts only when it helps navigation; M3 owns the real profile/config boundary.

## Target File Structure

Planned production shape after M2:

```text
build/_build/Targets/GenerateBindings/
  GenerateBindingsTask.cs
  BindingFamilyGeneration.cs
  ServiceCollectionExtensions.cs

  HeaderSet/
    HeaderSetResolver.cs
    HeaderSetFingerprint.cs
    HeaderSetFingerprintCalculator.cs
    ResolvedHeaderSet.cs

  SyntheticHeaders/         # stable fixture/data inputs consumed by HeaderSet; no M2 move

  Parse/
    CppAstParseRunner.cs
    CppAstParseResult.cs
    LibclangVersionAsserter.cs
    ParseDiagnosticFormatter.cs

  PlatformViews/
    PlatformCatalog.cs
    PlatformCondition.cs
    PlatformParseView.cs

  Model/
    BindingModel.cs
    BindingParseView.cs
    BindingFunctions.cs
    BindingStructs.cs
    BindingEnums.cs
    BindingDeclaredTypes.cs
    BindingTypeRef.cs
    MacroConstantReport.cs
    NativeTypeMetadata.cs
    NativeTypeRef.cs

  ModelBuilding/
    BindingModelBuilder.cs
    Declarations/
    Types/
    Functions/
    Macros/
    SdlPolicy/

  Emit/
    BindingEmitter.cs
    BindingEmissionOptions.cs
    GeneratedFileSet.cs
    RawAbi/
    PublicApi/
    Reports/
    Tfm/
```

No `Profiles/` folder is created in M2 unless a real profile-boundary type is introduced by a RED test or approved design revision. No `Emit/Friendly/` folder is created in M2.

## Planned Move Map

Use `git mv` for all tracked files.

| Current path | M2 path | Notes |
| --- | --- | --- |
| `Parsing/CppAstParseRunner.cs` | `Parse/CppAstParseRunner.cs` | Actual CppAst parse execution. |
| `Parsing/CppAstParseResult.cs` | `Parse/CppAstParseResult.cs` | Parse result contract. |
| `Parsing/ParseDiagnosticFormatter.cs` | `Parse/ParseDiagnosticFormatter.cs` | Parse diagnostic formatting. |
| `Parsing/LibclangVersionAsserter.cs` | `Parse/LibclangVersionAsserter.cs` | Process/tool-backed parse prerequisite. |
| `Parsing/PlatformCatalog.cs` | `PlatformViews/PlatformCatalog.cs` | Platform parse-view catalog policy. |
| `Parsing/PlatformCondition.cs` | `PlatformViews/PlatformCondition.cs` | Platform condition enum. |
| `Parsing/PlatformParseView.cs` | `PlatformViews/PlatformParseView.cs` | Parse-view definition. |
| `Translation/CppAstToBindingModel.cs` | `ModelBuilding/BindingModelBuilder.cs` | Rename type to `BindingModelBuilder`. |
| `Translation/BindableDeclarationPolicy.cs` | `ModelBuilding/Declarations/BindableDeclarationPolicy.cs` | Declaration inclusion policy. |
| `Translation/KnownUnsupportedDeclarationPolicy.cs` | `ModelBuilding/Declarations/KnownUnsupportedDeclarationPolicy.cs` | Unsupported/deferred declarations. |
| `Translation/NativeDeclarationCatalog*.cs` | `ModelBuilding/Declarations/NativeDeclarationCatalog*.cs` | Parsed declaration catalog. |
| `Translation/BindingStructTranslator.cs` | `ModelBuilding/Declarations/BindingStructTranslator.cs` | Struct extraction. |
| `Translation/StructFieldTranslator.cs` | `ModelBuilding/Declarations/StructFieldTranslator.cs` | Struct field translation. |
| `Translation/BindingEnumTranslator.cs` | `ModelBuilding/Declarations/BindingEnumTranslator.cs` | Enum extraction. |
| `Translation/BindingHandleTranslator.cs` | `ModelBuilding/Declarations/BindingHandleTranslator.cs` | Handle extraction. |
| `Translation/BindingCallbackTranslator.cs` | `ModelBuilding/Declarations/BindingCallbackTranslator.cs` | Callback extraction. |
| `Translation/NativeTypeClassifier.cs` | `ModelBuilding/Types/NativeTypeClassifier.cs` | Native type classifier. |
| `Translation/NativeTypeClassificationContext.cs` | `ModelBuilding/Types/NativeTypeClassificationContext.cs` | Classifier context. |
| `Translation/TypeMappingPolicy.cs` | `ModelBuilding/Types/TypeMappingPolicy.cs` | Legacy scalar/type map. |
| `Translation/LegacyBindingTypeRefBridge.cs` | `ModelBuilding/Types/LegacyBindingTypeRefBridge.cs` | Required-function config bridge. |
| `Translation/SdlWideStringPointerPolicy.cs` | `ModelBuilding/SdlPolicy/SdlWideStringPointerPolicy.cs` | SDL wide-string exception policy. |
| `Translation/SdlOpaqueStructPolicy.cs` | `ModelBuilding/SdlPolicy/SdlOpaqueStructPolicy.cs` | SDL opaque struct policy. |
| `Translation/SdlNativeTypeSubstitutionPolicy.cs` | `ModelBuilding/SdlPolicy/SdlNativeTypeSubstitutionPolicy.cs` | SDL value substitution policy. |
| `Translation/ExternalNativeTypePolicy.cs` | `ModelBuilding/Types/ExternalNativeTypePolicy.cs` | External opaque native type policy. |
| `Translation/CoreOwnedTypeMap.cs` | `ModelBuilding/SdlPolicy/CoreOwnedTypeMap.cs` | Current SDL2 core-owned type map; M3 profile work decides final family ownership shape. |
| `Translation/BindingFunctionTranslator.cs` | `ModelBuilding/Functions/BindingFunctionTranslator.cs` | Function translation. |
| `Translation/BindingFunctionDeduplicator.cs` | `ModelBuilding/Functions/BindingFunctionDeduplicator.cs` | Function duplicate elimination. |
| `Translation/NeutralFunctionSetBuilder.cs` | `ModelBuilding/Functions/NeutralFunctionSetBuilder.cs` | Neutral subtraction source. |
| `Translation/BindingConstantTranslator.cs` | `ModelBuilding/Macros/BindingConstantTranslator.cs` | Macro lane orchestrator. |
| `Translation/Macro*.cs` | `ModelBuilding/Macros/Macro*.cs` | Macro policies/evaluator/report inputs. |
| `Translation/RequiredConstantTranslator.cs` | `ModelBuilding/Macros/RequiredConstantTranslator.cs` | Required constants. |
| `Emitting/BindingEmissionOptions.cs` | `Emit/BindingEmissionOptions.cs` | Shared emission options. |
| `Emitting/GeneratedFileSet.cs` | `Emit/GeneratedFileSet.cs` | Generated file-set contract. |
| `Emitting/CsCommandEmitter.cs` | `Emit/BindingEmitter.cs` | Rename top-level file-set orchestrator. |
| `Emitting/CsConstantEmitter.cs` | `Emit/PublicApi/ConstantEmitter.cs` | Public generated constants. |
| `Emitting/CsEnumEmitter.cs` | `Emit/PublicApi/EnumEmitter.cs` | Public generated enums. |
| `Emitting/CsHandleEmitter.cs` | `Emit/PublicApi/HandleEmitter.cs` | Public generated handles. |
| `Emitting/CsStructEmitter.cs` | `Emit/PublicApi/StructEmitter.cs` | Public generated structs. |
| `Emitting/CsCallbackEmitter.cs` | `Emit/PublicApi/CallbackEmitter.cs` | Public generated callbacks. |
| `Emitting/FixedArrayEmissionPolicy.cs` | `Emit/PublicApi/FixedArrayEmissionPolicy.cs` | Struct fixed-array emission policy. |
| `Emitting/ModernCIntegerEmissionPolicy.cs` | `Emit/Tfm/ModernCIntegerEmissionPolicy.cs` | TFM guard policy shared by raw/public artifacts. |
| `Emitting/BindingParseViewReport.cs` | `Emit/Reports/BindingParseViewReport.cs` | Report DTOs. |

New production files:

- `build/_build/Targets/GenerateBindings/BindingFamilyGeneration.cs`
- `build/_build/Targets/GenerateBindings/Emit/RawAbi/RawAbiCommandEmitter.cs`
- `build/_build/Targets/GenerateBindings/Emit/Reports/BindingParseViewReportBuilder.cs`

## Record Collapse Plan

Collapse scattered one-line records only when the grouped file is a real concept.

| New file | Contains | Rationale |
| --- | --- | --- |
| `Model/BindingFunctions.cs` | `BindingFunction`, `BindingParameter` | Function contract and parameters move together. |
| `Model/BindingStructs.cs` | `BindingStruct`, struct field record | Struct contract and fields move together. |
| `Model/BindingEnums.cs` | `BindingEnumeration`, enum member record | Enum contract and members move together. |
| `Model/BindingDeclaredTypes.cs` | `BindingConstant`, `BindingHandle`, `BindingCallback` | Current one-line public generated declaration records that are not worth three files yet. |
| `Model/NativeTypeMetadata.cs` | `NativeAbiShape`, `NativeTypeKind`, `NativeTypeDiagnostic`, `NativeTypeDiagnosticSeverity` | Native type metadata and diagnostics are one type-system support concept. |
| `Emit/GeneratedFileSet.cs` | `GeneratedFileSet`, `GeneratedFile` | Already a paired output contract today; M2 moves it with emission, not as model-record work. |
| `Emit/Reports/BindingParseViewReport.cs` | all parse-view report DTO records | Report schema should be navigable from one file. |

Do not collapse `BindingModel.cs`, `NativeTypeRef.cs`, `BindingTypeRef.cs`, or `MacroConstantReport.cs` unless implementation reveals they are mostly one-line shells. They are important enough concepts to stay visible.

## Task 1: Baseline And Safety RED Tests

**Files:**
- Modify: `build/_build.Tests/Scenarios/GenerateBindings/GenerateBindingsTaskScenarioTests.cs`

- [ ] **Step 1: Confirm clean worktree**

Run:

```pwsh
git status --short
```

Expected: clean tree, or only docs explicitly approved for this M2 plan.

- [ ] **Step 2: Run current build-host suite**

Run:

```pwsh
dotnet test --project build/_build.Tests/Build.Tests.csproj -c Release --framework net10.0
```

Expected: PASS. If it fails, stop and debug before changing tests.

- [ ] **Step 3: Add RED test for unknown validator IDs**

Add this test to `GenerateBindingsTaskScenarioTests` near existing scenario tests:

```csharp
[Test]
public async Task RunAsync_Should_Throw_When_Config_Enables_Unknown_Validator()
{
    var world = FakeCakeWorld.CreateLinux();
    var config = Sdl2CoreConfig() with
    {
        Validators = ImmutableDictionary<string, bool>.Empty.Add("missing-validator", true),
    };
    world.WithTextFile("vcpkg_installed/x64-linux-hybrid/include/SDL2/SDL_video.h", string.Empty);
    var parser = Substitute.For<ICppAstParseRunner>();
    parser
        .Parse(Arg.Any<BindingGenerationConfig>(), Arg.Any<ResolvedHeaderSet>(), Arg.Any<PlatformParseView>())
        .Returns(call => new CppAstParseResult(call.Arg<PlatformParseView>(), []));

    var result = await CreateHost(world, parser: parser, configRepository: CreateEnabledRepository(config), validators: []).RunAsync();

    await Assert.That(result.Success).IsFalse();
    await Assert.That(result.Exception).IsNotNull();
    await Assert.That(result.Exception).IsTypeOf<CakeException>();
    await Assert.That(result.Exception!.Message).Contains("missing-validator");
}
```

- [ ] **Step 4: Verify unknown-validator test fails for the right reason**

Run:

```pwsh
dotnet test --project build/_build.Tests/Build.Tests.csproj -c Release --framework net10.0 -- --treenode-filter "/*/*/GenerateBindingsTaskScenarioTests/RunAsync_Should_Throw_When_Config_Enables_Unknown_Validator"
```

Expected: FAIL because current behavior silently ignores the unknown validator and returns success.

- [ ] **Step 5: Add RED test for stale generated output cleanup**

Add this test to `GenerateBindingsTaskScenarioTests`:

```csharp
[Test]
public async Task RunAsync_Should_Remove_Stale_Generated_Files_When_Regenerating_Family()
{
    var world = FakeCakeWorld.CreateLinux();
    var config = Sdl2CoreConfig() with
    {
        Validators = ImmutableDictionary<string, bool>.Empty,
    };
    world.WithTextFile("vcpkg_installed/x64-linux-hybrid/include/SDL2/SDL_video.h", string.Empty);
    world.WithTextFile("artifacts/generated-bindings-preview/sdl2-core/Types/Obsolete.g.cs", "// stale");
    var parser = Substitute.For<ICppAstParseRunner>();
    parser
        .Parse(Arg.Any<BindingGenerationConfig>(), Arg.Any<ResolvedHeaderSet>(), Arg.Any<PlatformParseView>())
        .Returns(call => new CppAstParseResult(call.Arg<PlatformParseView>(), []));

    var result = await CreateHost(world, parser: parser, configRepository: CreateEnabledRepository(config)).RunAsync();

    await Assert.That(result.Success).IsTrue();
    await Assert.That(world.FileExists("artifacts/generated-bindings-preview/sdl2-core/Types/Obsolete.g.cs")).IsFalse();
}
```

- [ ] **Step 6: Verify stale-file test fails for the right reason**

Run:

```pwsh
dotnet test --project build/_build.Tests/Build.Tests.csproj -c Release --framework net10.0 -- --treenode-filter "/*/*/GenerateBindingsTaskScenarioTests/RunAsync_Should_Remove_Stale_Generated_Files_When_Regenerating_Family"
```

Expected: FAIL because current generation writes new files without clearing obsolete files.

## Task 2: Implement Tiny Safety Fixes

**Files:**
- Modify: `build/_build/Targets/GenerateBindings/GenerateBindingsTask.cs`
- Modify: `build/_build.Tests/Scenarios/GenerateBindings/GenerateBindingsTaskScenarioTests.cs`

- [ ] **Step 1: Fail closed on unknown enabled validators**

Inside the current validator dispatch logic, compute enabled IDs and registered IDs before running validators:

```csharp
var enabledIds = config.Validators
    .Where(kv => kv.Value)
    .Select(kv => kv.Key)
    .ToHashSet(StringComparer.Ordinal);

var registeredIds = _validators
    .Select(v => v.ValidatorId)
    .ToHashSet(StringComparer.Ordinal);

var unknownIds = enabledIds
    .Where(id => !registeredIds.Contains(id))
    .Order(StringComparer.Ordinal)
    .ToArray();
if (unknownIds.Length > 0)
{
    throw new CakeException(
        $"GenerateBindings family '{config.FamilyId}' enables unknown validator id(s): {string.Join(", ", unknownIds)}. " +
        "Register the validator or fix build/manifest.json library_manifests[].binding_generation.validators.");
}
```

Keep this code in the existing task only until `BindingFamilyGeneration` is extracted in Task 3.

- [ ] **Step 2: Clear family output before writing files with a bounded delete guard**

Change the current writer to delete only the resolved family output directory under the generated-preview root before recreating it:

```csharp
private static async Task WriteAsync(BuildContext context, GeneratedFileSet fileSet, DirectoryPath outputDirectory, CancellationToken ct)
{
    EnsureSafeGeneratedOutputDirectory(context.Environment, context.Paths.GenerateBindingsPreviewRoot, outputDirectory);

    if (context.DirectoryExists(outputDirectory))
    {
        context.DeleteDirectory(outputDirectory, new DeleteDirectorySettings
        {
            Recursive = true,
            Force = true,
        });
    }

    context.EnsureDirectoryExists(outputDirectory);

    foreach (var file in fileSet.Files)
    {
        ct.ThrowIfCancellationRequested();

        var targetPath = outputDirectory.CombineWithFilePath(file.RelativePath);
        var parent = targetPath.GetDirectory();
        context.EnsureDirectoryExists(parent);

        await context.WriteAllTextAsync(targetPath, file.Content).ConfigureAwait(false);
    }
}

private static void EnsureSafeGeneratedOutputDirectory(ICakeEnvironment environment, DirectoryPath previewRoot, DirectoryPath outputDirectory)
{
    var root = previewRoot.MakeAbsolute(environment);
    var candidate = outputDirectory.MakeAbsolute(environment);
    var rootSegments = root.Segments;
    var candidateSegments = candidate.Segments;

    if (candidateSegments.Length <= rootSegments.Length)
    {
        throw new CakeException($"Refusing to clear generated bindings output outside '{root.FullPath}': '{candidate.FullPath}'.");
    }

    for (var i = 0; i < rootSegments.Length; i++)
    {
        if (!string.Equals(rootSegments[i], candidateSegments[i], StringComparison.OrdinalIgnoreCase))
        {
            throw new CakeException($"Refusing to clear generated bindings output outside '{root.FullPath}': '{candidate.FullPath}'.");
        }
    }
}
```

Use Cake aliases/settings and Cake path segments for directory operations and safety checks. The guard only exists to prevent a miswired recursive delete from escaping `artifacts/generated-bindings-preview/`.

- [ ] **Step 3: Verify the two safety tests pass**

Run:

```pwsh
dotnet test --project build/_build.Tests/Build.Tests.csproj -c Release --framework net10.0 -- --treenode-filter "/*/*/GenerateBindingsTaskScenarioTests/RunAsync_Should_Throw_When_Config_Enables_Unknown_Validator"
dotnet test --project build/_build.Tests/Build.Tests.csproj -c Release --framework net10.0 -- --treenode-filter "/*/*/GenerateBindingsTaskScenarioTests/RunAsync_Should_Remove_Stale_Generated_Files_When_Regenerating_Family"
```

Expected: both tests PASS.

- [ ] **Step 4: Run all GenerateBindings scenario tests**

Run:

```pwsh
dotnet test --project build/_build.Tests/Build.Tests.csproj -c Release --framework net10.0 -- --treenode-filter "/*/*/GenerateBindingsTaskScenarioTests/*"
dotnet test --project build/_build.Tests/Build.Tests.csproj -c Release --framework net10.0 -- --treenode-filter "/*/*/GenerateBindingsTaskSnapshotTests/*"
```

Expected: PASS. Snapshot content should not change except for deliberate behavior around stale files, which is not part of current snapshot content.

## Task 3: Extract `BindingFamilyGeneration`

**Files:**
- Create: `build/_build/Targets/GenerateBindings/BindingFamilyGeneration.cs`
- Modify: `build/_build/Targets/GenerateBindings/GenerateBindingsTask.cs`
- Modify: `build/_build/Targets/GenerateBindings/ServiceCollectionExtensions.cs`
- Modify: `build/_build.Tests/Scenarios/GenerateBindings/GenerateBindingsTaskScenarioTests.cs`
- Modify: `build/_build.Tests/Scenarios/GenerateBindings/GenerateBindingsTaskSnapshotTests.cs`
- Modify: `build/_build.Tests/Unit/CompositionRoot/ServiceCollectionExtensionsSmokeTests.cs`

- [ ] **Step 1: Create the collaborator with current per-family logic**

Create `BindingFamilyGeneration` as a concrete public class because `GenerateBindingsTask` is public and will inject it through a public constructor. Its initial constructor dependencies are `IBindingGenerationConfigRepository`, `HeaderSetResolver`, `ICppAstParseRunner`, `IEnumerable<IBindingFamilyValidator>`, and `ICakeLog`. Its entrypoint is `public async Task GenerateAsync(BuildContext context, string familyId, CancellationToken ct)`.

Keep `ICakeLog` on this collaborator because it owns sequential post-parse reporting (`LogReport`, `LogPerViewCounts`) and expected-error diagnostics. Do not pass logging through the task as an ad-hoc callback.

Move these methods from `GenerateBindingsTask` into the collaborator:

- `GenerateOneFamilyAsync`
- `ConvertRequiredFunctions`
- `RunFamilyValidatorsAsync`
- `LogReport`
- `LogPerViewCounts`
- `WriteAsync`

Keep names behavior-first. Do not rename methods to `Execute`, `Process`, or `Run` without a noun.

- [ ] **Step 2: Reduce `GenerateBindingsTask` to target lifecycle orchestration**

The task constructor should become:

```csharp
public sealed class GenerateBindingsTask(
    IBindingGenerationConfigRepository configRepository,
    BindingFamilyGeneration familyGeneration,
    ILibclangVersionAsserter libclangVersionAsserter,
    ICakeLog log) : AsyncFrostingTask<BuildContext>
```

The task body should call:

```csharp
foreach (var familyId in families)
{
    await _familyGeneration.GenerateAsync(context, familyId, context.CancellationToken).ConfigureAwait(false);
}
```

Keep `AssertLinuxTriplet` and `LogContainerDigestIfPresent` on the task.

- [ ] **Step 3: Register the new collaborator**

Modify `AddGenerateBindings`:

```csharp
services.AddSingleton<BindingFamilyGeneration>();
```

Update the composition smoke comment after the top-level emitter is renamed so it describes the registered `BindingFamilyGeneration` collaborator and no longer treats emission as an invisible static-only boundary.

- [ ] **Step 4: Update scenario host setup**

In `GenerateBindingsTaskScenarioTests` and `GenerateBindingsTaskSnapshotTests`, register `BindingFamilyGeneration` in the test host service setup:

```csharp
services.AddSingleton<BindingFamilyGeneration>();
```

Keep parser, repository, validators, and libclang substitutions as they are.

- [ ] **Step 5: Verify task scenario and snapshot tests**

Run:

```pwsh
dotnet test --project build/_build.Tests/Build.Tests.csproj -c Release --framework net10.0 -- --treenode-filter "/*/*/GenerateBindingsTaskScenarioTests/*"
dotnet test --project build/_build.Tests/Build.Tests.csproj -c Release --framework net10.0 -- --treenode-filter "/*/*/GenerateBindingsTaskSnapshotTests/*"
dotnet test --project build/_build.Tests/Build.Tests.csproj -c Release --framework net10.0 -- --treenode-filter "/*/*/ServiceCollectionExtensionsSmokeTests/AddGenerateBindings_Should_Register_All_Collaborator_Types"
```

Expected: PASS. Existing snapshot content must stay unchanged.

## Task 4: Split Parse And Platform View Concepts

**Files:**
- Move: `build/_build/Targets/GenerateBindings/Parsing/*.cs`
- Move tests under `build/_build.Tests/Unit/Targets/GenerateBindings/Parsing/`
- Modify all `using Build.Targets.GenerateBindings.Parsing;` references.

- [ ] **Step 1: Create parse and platform destination directories**

Run:

```pwsh
New-Item -ItemType Directory -Force -Path build/_build/Targets/GenerateBindings/Parse, build/_build/Targets/GenerateBindings/PlatformViews, build/_build.Tests/Unit/Targets/GenerateBindings/Parse, build/_build.Tests/Unit/Targets/GenerateBindings/PlatformViews
```

Expected: directories exist before `git mv` writes into them.

- [ ] **Step 2: Move parse execution files**

Run `git mv` commands:

```pwsh
git mv build/_build/Targets/GenerateBindings/Parsing/CppAstParseRunner.cs build/_build/Targets/GenerateBindings/Parse/CppAstParseRunner.cs
git mv build/_build/Targets/GenerateBindings/Parsing/CppAstParseResult.cs build/_build/Targets/GenerateBindings/Parse/CppAstParseResult.cs
git mv build/_build/Targets/GenerateBindings/Parsing/ParseDiagnosticFormatter.cs build/_build/Targets/GenerateBindings/Parse/ParseDiagnosticFormatter.cs
git mv build/_build/Targets/GenerateBindings/Parsing/LibclangVersionAsserter.cs build/_build/Targets/GenerateBindings/Parse/LibclangVersionAsserter.cs
```

`ICppAstParseRunner` and `ILibclangVersionAsserter` are process/tool seams and may stay declared beside their concrete implementations in M2. Do not split them into one-file interface ceremony unless the move creates a compile problem.

- [ ] **Step 3: Move platform view files**

Run:

```pwsh
git mv build/_build/Targets/GenerateBindings/Parsing/PlatformCatalog.cs build/_build/Targets/GenerateBindings/PlatformViews/PlatformCatalog.cs
git mv build/_build/Targets/GenerateBindings/Parsing/PlatformCondition.cs build/_build/Targets/GenerateBindings/PlatformViews/PlatformCondition.cs
git mv build/_build/Targets/GenerateBindings/Parsing/PlatformParseView.cs build/_build/Targets/GenerateBindings/PlatformViews/PlatformParseView.cs
```

- [ ] **Step 4: Update namespaces**

Use these namespace targets:

```csharp
namespace Build.Targets.GenerateBindings.Parse;
namespace Build.Targets.GenerateBindings.PlatformViews;
```

Update tests to mirror:

```csharp
namespace Build.Tests.Unit.Targets.GenerateBindings.Parse;
namespace Build.Tests.Unit.Targets.GenerateBindings.PlatformViews;
```

- [ ] **Step 5: Move tests that map directly**

Run:

```pwsh
git mv build/_build.Tests/Unit/Targets/GenerateBindings/Parsing/CppAstParseRunnerTests.cs build/_build.Tests/Unit/Targets/GenerateBindings/Parse/CppAstParseRunnerTests.cs
git mv build/_build.Tests/Unit/Targets/GenerateBindings/Parsing/CppAstFixtureMatrixTests.cs build/_build.Tests/Unit/Targets/GenerateBindings/Parse/CppAstFixtureMatrixTests.cs
git mv build/_build.Tests/Unit/Targets/GenerateBindings/Parsing/PlatformCatalogTests.cs build/_build.Tests/Unit/Targets/GenerateBindings/PlatformViews/PlatformCatalogTests.cs
```

- [ ] **Step 6: Verify parse/platform tests**

Run:

```pwsh
dotnet test --project build/_build.Tests/Build.Tests.csproj -c Release --framework net10.0 -- --treenode-filter "/*/*/CppAstParseRunnerTests/*"
dotnet test --project build/_build.Tests/Build.Tests.csproj -c Release --framework net10.0 -- --treenode-filter "/*/*/CppAstFixtureMatrixTests/*"
dotnet test --project build/_build.Tests/Build.Tests.csproj -c Release --framework net10.0 -- --treenode-filter "/*/*/PlatformCatalogTests/*"
dotnet test --project build/_build.Tests/Build.Tests.csproj -c Release --framework net10.0 -- --treenode-filter "/*/*/GenerateBindingsTaskScenarioTests/*"
dotnet test --project build/_build.Tests/Build.Tests.csproj -c Release --framework net10.0 -- --treenode-filter "/*/*/GenerateBindingsTaskSnapshotTests/*"
```

Expected: PASS. On Windows, Linux-only fixture tests may be skipped; scenario/snapshot tests must pass.

## Task 5: Reorganize Model Building Topology

**Files:**
- Modify: `build/_build/Targets/GenerateBindings/BindingFamilyGeneration.cs`
- Modify: `build/_build/Targets/GenerateBindings/ServiceCollectionExtensions.cs`
- Move: `build/_build/Targets/GenerateBindings/Translation/*.cs`
- Move/update tests under `build/_build.Tests/Unit/Targets/GenerateBindings/Translation/`
- Modify: `build/_build.Tests/Fixtures/GenerateBindings/SemanticFixtureParser.cs`

- [ ] **Step 1: Create model-building destination directories**

Run:

```pwsh
New-Item -ItemType Directory -Force -Path build/_build/Targets/GenerateBindings/ModelBuilding, build/_build/Targets/GenerateBindings/ModelBuilding/Macros, build/_build/Targets/GenerateBindings/ModelBuilding/Types, build/_build/Targets/GenerateBindings/ModelBuilding/Declarations, build/_build/Targets/GenerateBindings/ModelBuilding/Functions, build/_build/Targets/GenerateBindings/ModelBuilding/SdlPolicy, build/_build.Tests/Unit/Targets/GenerateBindings/ModelBuilding, build/_build.Tests/Unit/Targets/GenerateBindings/ModelBuilding/Macros, build/_build.Tests/Unit/Targets/GenerateBindings/ModelBuilding/Types, build/_build.Tests/Unit/Targets/GenerateBindings/ModelBuilding/Declarations, build/_build.Tests/Unit/Targets/GenerateBindings/ModelBuilding/Functions, build/_build.Tests/Unit/Targets/GenerateBindings/ModelBuilding/SdlPolicy
```

Expected: production and test destination directories exist before `git mv` writes into them.

- [ ] **Step 2: Move macro cluster first**

Move macro-related files into `ModelBuilding/Macros/`:

```pwsh
git mv build/_build/Targets/GenerateBindings/Translation/BindingConstantTranslator.cs build/_build/Targets/GenerateBindings/ModelBuilding/Macros/BindingConstantTranslator.cs
git mv build/_build/Targets/GenerateBindings/Translation/MacroApiPolicy.cs build/_build/Targets/GenerateBindings/ModelBuilding/Macros/MacroApiPolicy.cs
git mv build/_build/Targets/GenerateBindings/Translation/MacroCandidateCollector.cs build/_build/Targets/GenerateBindings/ModelBuilding/Macros/MacroCandidateCollector.cs
git mv build/_build/Targets/GenerateBindings/Translation/MacroConstantCandidate.cs build/_build/Targets/GenerateBindings/ModelBuilding/Macros/MacroConstantCandidate.cs
git mv build/_build/Targets/GenerateBindings/Translation/MacroConstantMerger.cs build/_build/Targets/GenerateBindings/ModelBuilding/Macros/MacroConstantMerger.cs
git mv build/_build/Targets/GenerateBindings/Translation/MacroExpressionEvaluationContext.cs build/_build/Targets/GenerateBindings/ModelBuilding/Macros/MacroExpressionEvaluationContext.cs
git mv build/_build/Targets/GenerateBindings/Translation/MacroIntegerExpressionEvaluator.cs build/_build/Targets/GenerateBindings/ModelBuilding/Macros/MacroIntegerExpressionEvaluator.cs
git mv build/_build/Targets/GenerateBindings/Translation/MacroManualPolicyApplier.cs build/_build/Targets/GenerateBindings/ModelBuilding/Macros/MacroManualPolicyApplier.cs
git mv build/_build/Targets/GenerateBindings/Translation/MacroValueClassifier.cs build/_build/Targets/GenerateBindings/ModelBuilding/Macros/MacroValueClassifier.cs
git mv build/_build/Targets/GenerateBindings/Translation/RequiredConstantTranslator.cs build/_build/Targets/GenerateBindings/ModelBuilding/Macros/RequiredConstantTranslator.cs
```

Namespace:

```csharp
namespace Build.Targets.GenerateBindings.ModelBuilding.Macros;
```

- [ ] **Step 3: Move type cluster**

Move type policy/classification files into `ModelBuilding/Types/`:

```pwsh
git mv build/_build/Targets/GenerateBindings/Translation/NativeTypeClassifier.cs build/_build/Targets/GenerateBindings/ModelBuilding/Types/NativeTypeClassifier.cs
git mv build/_build/Targets/GenerateBindings/Translation/NativeTypeClassificationContext.cs build/_build/Targets/GenerateBindings/ModelBuilding/Types/NativeTypeClassificationContext.cs
git mv build/_build/Targets/GenerateBindings/Translation/TypeMappingPolicy.cs build/_build/Targets/GenerateBindings/ModelBuilding/Types/TypeMappingPolicy.cs
git mv build/_build/Targets/GenerateBindings/Translation/LegacyBindingTypeRefBridge.cs build/_build/Targets/GenerateBindings/ModelBuilding/Types/LegacyBindingTypeRefBridge.cs
git mv build/_build/Targets/GenerateBindings/Translation/ExternalNativeTypePolicy.cs build/_build/Targets/GenerateBindings/ModelBuilding/Types/ExternalNativeTypePolicy.cs
```

Namespace:

```csharp
namespace Build.Targets.GenerateBindings.ModelBuilding.Types;
```

- [ ] **Step 4: Move SDL-specific policy seam**

Move current SDL2-shaped policy files into `ModelBuilding/SdlPolicy/`:

```pwsh
git mv build/_build/Targets/GenerateBindings/Translation/SdlWideStringPointerPolicy.cs build/_build/Targets/GenerateBindings/ModelBuilding/SdlPolicy/SdlWideStringPointerPolicy.cs
git mv build/_build/Targets/GenerateBindings/Translation/SdlOpaqueStructPolicy.cs build/_build/Targets/GenerateBindings/ModelBuilding/SdlPolicy/SdlOpaqueStructPolicy.cs
git mv build/_build/Targets/GenerateBindings/Translation/SdlNativeTypeSubstitutionPolicy.cs build/_build/Targets/GenerateBindings/ModelBuilding/SdlPolicy/SdlNativeTypeSubstitutionPolicy.cs
git mv build/_build/Targets/GenerateBindings/Translation/CoreOwnedTypeMap.cs build/_build/Targets/GenerateBindings/ModelBuilding/SdlPolicy/CoreOwnedTypeMap.cs
```

Namespace:

```csharp
namespace Build.Targets.GenerateBindings.ModelBuilding.SdlPolicy;
```

This is a seam, not the M3 profile system. Keep existing behavior and tests.

- [ ] **Step 5: Move declaration cluster**

Move declaration catalog and declaration translators into `ModelBuilding/Declarations/`:

```pwsh
git mv build/_build/Targets/GenerateBindings/Translation/BindableDeclarationPolicy.cs build/_build/Targets/GenerateBindings/ModelBuilding/Declarations/BindableDeclarationPolicy.cs
git mv build/_build/Targets/GenerateBindings/Translation/KnownUnsupportedDeclarationPolicy.cs build/_build/Targets/GenerateBindings/ModelBuilding/Declarations/KnownUnsupportedDeclarationPolicy.cs
git mv build/_build/Targets/GenerateBindings/Translation/NativeDeclarationCatalog.cs build/_build/Targets/GenerateBindings/ModelBuilding/Declarations/NativeDeclarationCatalog.cs
git mv build/_build/Targets/GenerateBindings/Translation/NativeDeclarationCatalogBuilder.cs build/_build/Targets/GenerateBindings/ModelBuilding/Declarations/NativeDeclarationCatalogBuilder.cs
git mv build/_build/Targets/GenerateBindings/Translation/BindingStructTranslator.cs build/_build/Targets/GenerateBindings/ModelBuilding/Declarations/BindingStructTranslator.cs
git mv build/_build/Targets/GenerateBindings/Translation/StructFieldTranslator.cs build/_build/Targets/GenerateBindings/ModelBuilding/Declarations/StructFieldTranslator.cs
git mv build/_build/Targets/GenerateBindings/Translation/BindingEnumTranslator.cs build/_build/Targets/GenerateBindings/ModelBuilding/Declarations/BindingEnumTranslator.cs
git mv build/_build/Targets/GenerateBindings/Translation/BindingHandleTranslator.cs build/_build/Targets/GenerateBindings/ModelBuilding/Declarations/BindingHandleTranslator.cs
git mv build/_build/Targets/GenerateBindings/Translation/BindingCallbackTranslator.cs build/_build/Targets/GenerateBindings/ModelBuilding/Declarations/BindingCallbackTranslator.cs
```

Namespace:

```csharp
namespace Build.Targets.GenerateBindings.ModelBuilding.Declarations;
```

- [ ] **Step 6: Move function cluster**

Move function translators into `ModelBuilding/Functions/`:

```pwsh
git mv build/_build/Targets/GenerateBindings/Translation/BindingFunctionTranslator.cs build/_build/Targets/GenerateBindings/ModelBuilding/Functions/BindingFunctionTranslator.cs
git mv build/_build/Targets/GenerateBindings/Translation/BindingFunctionDeduplicator.cs build/_build/Targets/GenerateBindings/ModelBuilding/Functions/BindingFunctionDeduplicator.cs
git mv build/_build/Targets/GenerateBindings/Translation/NeutralFunctionSetBuilder.cs build/_build/Targets/GenerateBindings/ModelBuilding/Functions/NeutralFunctionSetBuilder.cs
```

Namespace:

```csharp
namespace Build.Targets.GenerateBindings.ModelBuilding.Functions;
```

- [ ] **Step 7: Rename model composition root**

Move and rename `CppAstToBindingModel`:

```pwsh
git mv build/_build/Targets/GenerateBindings/Translation/CppAstToBindingModel.cs build/_build/Targets/GenerateBindings/ModelBuilding/BindingModelBuilder.cs
```

Rename the type to a concrete instance collaborator because it composes the declaration/type/function/macro policies and is the future profile injection point:

```csharp
public sealed class BindingModelBuilder
{
    public BindingModel Build(
        IReadOnlyList<CppAstParseResult> parseResults,
        BindingGenerationConfig config,
        IReadOnlyList<BindingFunction> requiredFunctions)
```

Register it and inject it into `BindingFamilyGeneration`:

```csharp
services.AddSingleton<BindingModelBuilder>();
```

Update all calls from the old symbol to the new collaborator. Example replacement in `BindingFamilyGeneration`:

```csharp
var model = CppAstToBindingModel.Translate(parseResults, config, requiredFunctions);
```

to:

```csharp
var model = _modelBuilder.Build(parseResults, config, requiredFunctions);
```

Update `build/_build.Tests/Fixtures/GenerateBindings/SemanticFixtureParser.cs` explicitly:

```csharp
return new BindingModelBuilder().Build(
    [ParseResult("Neutral", compilation, defines, undefines)],
    BindingGenerationFixture.Sdl2CoreConfig(),
    requiredFunctions ?? []);
```

- [ ] **Step 8: Move and split tests only where they already contain multiple classes**

Move `CppAstToBindingModelTests.cs` to `ModelBuilding/BindingModelBuilderTests.cs` and update the class name and `CppAstToBindingModel.Translate(parseResults, config, requiredFunctions)` calls to `new BindingModelBuilder().Build(parseResults, config, requiredFunctions)`.

Move already-focused test files with `git mv`:

```pwsh
git mv build/_build.Tests/Unit/Targets/GenerateBindings/Translation/CppAstToBindingModelTests.cs build/_build.Tests/Unit/Targets/GenerateBindings/ModelBuilding/BindingModelBuilderTests.cs
git mv build/_build.Tests/Unit/Targets/GenerateBindings/Translation/KnownUnsupportedDeclarationPolicyTests.cs build/_build.Tests/Unit/Targets/GenerateBindings/ModelBuilding/Declarations/KnownUnsupportedDeclarationPolicyTests.cs
git mv build/_build.Tests/Unit/Targets/GenerateBindings/Translation/NativeTypeClassifierTests.cs build/_build.Tests/Unit/Targets/GenerateBindings/ModelBuilding/Types/NativeTypeClassifierTests.cs
git mv build/_build.Tests/Unit/Targets/GenerateBindings/Translation/TypeMappingPolicyTests.cs build/_build.Tests/Unit/Targets/GenerateBindings/ModelBuilding/Types/TypeMappingPolicyTests.cs
git mv build/_build.Tests/Unit/Targets/GenerateBindings/Translation/BindingConstantTranslatorTests.cs build/_build.Tests/Unit/Targets/GenerateBindings/ModelBuilding/Macros/BindingConstantTranslatorTests.cs
git mv build/_build.Tests/Unit/Targets/GenerateBindings/Translation/MacroApiPolicyTests.cs build/_build.Tests/Unit/Targets/GenerateBindings/ModelBuilding/Macros/MacroApiPolicyTests.cs
git mv build/_build.Tests/Unit/Targets/GenerateBindings/Translation/MacroManualPolicyApplierTests.cs build/_build.Tests/Unit/Targets/GenerateBindings/ModelBuilding/Macros/MacroManualPolicyApplierTests.cs
git mv build/_build.Tests/Unit/Targets/GenerateBindings/Translation/MacroCandidateCollectorTests.cs build/_build.Tests/Unit/Targets/GenerateBindings/ModelBuilding/Macros/MacroCandidateCollectorTests.cs
git mv build/_build.Tests/Unit/Targets/GenerateBindings/Translation/MacroConstantMergerTests.cs build/_build.Tests/Unit/Targets/GenerateBindings/ModelBuilding/Macros/MacroConstantMergerTests.cs
git mv build/_build.Tests/Unit/Targets/GenerateBindings/Translation/MacroIntegerExpressionEvaluatorTests.cs build/_build.Tests/Unit/Targets/GenerateBindings/ModelBuilding/Macros/MacroIntegerExpressionEvaluatorTests.cs
git mv build/_build.Tests/Unit/Targets/GenerateBindings/Translation/MacroSurfaceAuditTests.cs build/_build.Tests/Unit/Targets/GenerateBindings/ModelBuilding/Macros/MacroSurfaceAuditTests.cs
git mv build/_build.Tests/Unit/Targets/GenerateBindings/Translation/MacroValueClassifierTests.cs build/_build.Tests/Unit/Targets/GenerateBindings/ModelBuilding/Macros/MacroValueClassifierTests.cs
git mv build/_build.Tests/Unit/Targets/GenerateBindings/Translation/CoreOwnedTypeMapTests.cs build/_build.Tests/Unit/Targets/GenerateBindings/ModelBuilding/SdlPolicy/CoreOwnedTypeMapTests.cs
```

Move `BindingTranslationCollaboratorTests.cs` classes into concept files. Preserve test method names.

Use `git mv` to preserve history for the first extracted class, then create the remaining concept files by moving whole class declarations:

```pwsh
git mv build/_build.Tests/Unit/Targets/GenerateBindings/Translation/BindingTranslationCollaboratorTests.cs build/_build.Tests/Unit/Targets/GenerateBindings/ModelBuilding/Declarations/BindableDeclarationPolicyTests.cs
```

Target files:

```text
Unit/Targets/GenerateBindings/ModelBuilding/Declarations/BindableDeclarationPolicyTests.cs
Unit/Targets/GenerateBindings/ModelBuilding/Declarations/KnownUnsupportedDeclarationPolicyTests.cs
Unit/Targets/GenerateBindings/ModelBuilding/Declarations/NativeDeclarationCatalogBuilderTests.cs
Unit/Targets/GenerateBindings/ModelBuilding/Declarations/BindingHandleTranslatorTests.cs
Unit/Targets/GenerateBindings/ModelBuilding/Declarations/BindingEnumTranslatorTests.cs
Unit/Targets/GenerateBindings/ModelBuilding/Declarations/BindingCallbackTranslatorTests.cs
Unit/Targets/GenerateBindings/ModelBuilding/Declarations/BindingStructTranslatorTests.cs
Unit/Targets/GenerateBindings/ModelBuilding/Declarations/StructFieldTranslatorTests.cs
Unit/Targets/GenerateBindings/ModelBuilding/Types/NativeTypeClassifierTests.cs
Unit/Targets/GenerateBindings/ModelBuilding/Types/TypeMappingPolicyTests.cs
Unit/Targets/GenerateBindings/ModelBuilding/Functions/BindingFunctionTranslatorTests.cs
Unit/Targets/GenerateBindings/ModelBuilding/Functions/NeutralFunctionSetBuilderTests.cs
Unit/Targets/GenerateBindings/ModelBuilding/Macros/BindingConstantTranslatorTests.cs
Unit/Targets/GenerateBindings/ModelBuilding/Macros/RequiredConstantTranslatorTests.cs
Unit/Targets/GenerateBindings/ModelBuilding/Macros/MacroApiPolicyTests.cs
Unit/Targets/GenerateBindings/ModelBuilding/Macros/MacroManualPolicyApplierTests.cs
Unit/Targets/GenerateBindings/ModelBuilding/Macros/MacroCandidateCollectorTests.cs
Unit/Targets/GenerateBindings/ModelBuilding/Macros/MacroConstantMergerTests.cs
Unit/Targets/GenerateBindings/ModelBuilding/Macros/MacroIntegerExpressionEvaluatorTests.cs
Unit/Targets/GenerateBindings/ModelBuilding/Macros/MacroSurfaceAuditTests.cs
Unit/Targets/GenerateBindings/ModelBuilding/Macros/MacroValueClassifierTests.cs
Unit/Targets/GenerateBindings/ModelBuilding/SdlPolicy/CoreOwnedTypeMapTests.cs
Unit/Targets/GenerateBindings/ModelBuilding/BindingModelBuilderTests.cs
```

Do not over-shatter tests that are already focused. Moving a test file is enough when the file already maps to one concept.

- [ ] **Step 9: Verify model-building tests**

Run:

```pwsh
dotnet test --project build/_build.Tests/Build.Tests.csproj -c Release --framework net10.0 -- --treenode-filter "/*/*/BindingModelBuilderTests/*"
dotnet test --project build/_build.Tests/Build.Tests.csproj -c Release --framework net10.0 -- --treenode-filter "/*/*/SemanticModelSnapshotTests/*"
dotnet test --project build/_build.Tests/Build.Tests.csproj -c Release --framework net10.0 -- --treenode-filter "/*/*/SemanticHeaderFixtureTests/*"
dotnet test --project build/_build.Tests/Build.Tests.csproj -c Release --framework net10.0 -- --treenode-filter "/*/*/BindingConstantTranslatorTests/*"
dotnet test --project build/_build.Tests/Build.Tests.csproj -c Release --framework net10.0 -- --treenode-filter "/*/*/NativeTypeClassifierTests/*"
```

Expected on Windows: Linux-only semantic fixture tests may skip; non-Linux-only tests pass. If the filtered run selects only skipped tests, use `--ignore-exit-code 8` and immediately run the Linux container command in Task 9.

## Task 6: Collapse Cohesive Model Records

**Files:**
- Modify/move under `build/_build/Targets/GenerateBindings/Model/`
- Modify/move under `build/_build.Tests/Unit/Targets/GenerateBindings/Model/`

- [ ] **Step 1: Collapse function records**

Create `Model/BindingFunctions.cs` via `git mv` from `BindingFunction.cs`, then move `BindingParameter` into the same file:

```pwsh
git mv build/_build/Targets/GenerateBindings/Model/BindingFunction.cs build/_build/Targets/GenerateBindings/Model/BindingFunctions.cs
```

Delete `BindingParameter.cs` after moving its exact existing record declaration into `BindingFunctions.cs` below the exact existing `BindingFunction` declaration. Do not rename constructor parameters or change accessibility.

- [ ] **Step 2: Collapse generated declaration records**

Run:

```pwsh
git mv build/_build/Targets/GenerateBindings/Model/BindingStruct.cs build/_build/Targets/GenerateBindings/Model/BindingStructs.cs
git mv build/_build/Targets/GenerateBindings/Model/BindingEnumeration.cs build/_build/Targets/GenerateBindings/Model/BindingEnums.cs
git mv build/_build/Targets/GenerateBindings/Model/BindingConstant.cs build/_build/Targets/GenerateBindings/Model/BindingDeclaredTypes.cs
```

`BindingStruct.cs` already contains both records; keep them together in `BindingStructs.cs`:

- `BindingStruct`
- struct field record from `BindingStruct.cs`

`BindingEnumeration.cs` already contains both records; keep them together in `BindingEnums.cs`:

- `BindingEnumeration`
- enum member record from `BindingEnumeration.cs`

Move these one-line records into `BindingDeclaredTypes.cs` below the existing `BindingConstant` record:

- `BindingConstant`
- `BindingHandle`
- `BindingCallback`

Keep original record names and constructors unchanged, then remove the emptied `BindingHandle.cs` and `BindingCallback.cs` files with `git rm`.

- [ ] **Step 3: Collapse native type metadata records**

Create `Model/NativeTypeMetadata.cs` and move these records/enums into it:

- `NativeAbiShape`
- `NativeTypeKind`
- `NativeTypeDiagnostic`
- `NativeTypeDiagnosticSeverity`

Keep `NativeTypeRef.cs` separate.

- [ ] **Step 4: Verify model record tests**

Run:

```pwsh
dotnet test --project build/_build.Tests/Build.Tests.csproj -c Release --framework net10.0 -- --treenode-filter "/*/*/BindingModelRecordsTests/*"
dotnet test --project build/_build.Tests/Build.Tests.csproj -c Release --framework net10.0 -- --treenode-filter "/*/*/NativeTypeModelTests/*"
```

Expected: PASS.

## Task 7: Reorganize Emission Topology

**Files:**
- Modify: `build/_build/Targets/GenerateBindings/BindingFamilyGeneration.cs`
- Modify: `build/_build/Targets/GenerateBindings/ServiceCollectionExtensions.cs`
- Move: `build/_build/Targets/GenerateBindings/Emitting/*.cs`
- Move/update tests under `build/_build.Tests/Unit/Targets/GenerateBindings/Emitting/`

- [ ] **Step 1: Create emission destination directories**

Run:

```pwsh
New-Item -ItemType Directory -Force -Path build/_build/Targets/GenerateBindings/Emit, build/_build/Targets/GenerateBindings/Emit/RawAbi, build/_build/Targets/GenerateBindings/Emit/PublicApi, build/_build/Targets/GenerateBindings/Emit/Reports, build/_build/Targets/GenerateBindings/Emit/Tfm, build/_build.Tests/Unit/Targets/GenerateBindings/Emit, build/_build.Tests/Unit/Targets/GenerateBindings/Emit/RawAbi, build/_build.Tests/Unit/Targets/GenerateBindings/Emit/PublicApi, build/_build.Tests/Unit/Targets/GenerateBindings/Emit/Reports, build/_build.Tests/Unit/Targets/GenerateBindings/Emit/Tfm
```

Expected: production and test destination directories exist before `git mv` writes into them.

- [ ] **Step 2: Move shared emission contracts**

Run:

```pwsh
git mv build/_build/Targets/GenerateBindings/Emitting/BindingEmissionOptions.cs build/_build/Targets/GenerateBindings/Emit/BindingEmissionOptions.cs
git mv build/_build/Targets/GenerateBindings/Emitting/GeneratedFileSet.cs build/_build/Targets/GenerateBindings/Emit/GeneratedFileSet.cs
```

Namespace:

```csharp
namespace Build.Targets.GenerateBindings.Emit;
```

Keep `GeneratedFileSet` and `GeneratedFile` in the same file.

- [ ] **Step 3: Rename top-level emitter**

Run:

```pwsh
git mv build/_build/Targets/GenerateBindings/Emitting/CsCommandEmitter.cs build/_build/Targets/GenerateBindings/Emit/BindingEmitter.cs
```

Rename the type to a concrete instance collaborator because it owns file-set orchestration and will compose raw/public/friendly emitters over time:

```csharp
public sealed class BindingEmitter
{
    public GeneratedFileSet Emit(BindingModel model, BindingEmissionOptions options)
```

Register it and inject it into `BindingFamilyGeneration`:

```csharp
services.AddSingleton<BindingEmitter>();
```

Update calls from the old symbol to the injected collaborator. Example replacement: `CsCommandEmitter.Emit(model, options)` becomes `_emitter.Emit(model, options)`.

- [ ] **Step 4: Extract raw ABI command emission**

Create `Emit/RawAbi/RawAbiCommandEmitter.cs` and move the existing command-file body into it unchanged:

```csharp
namespace Build.Targets.GenerateBindings.Emit.RawAbi;

internal static class RawAbiCommandEmitter
{
    public static GeneratedFile Emit(BindingParseView view, BindingEmissionOptions options, bool includeLibName)
    {
        return new GeneratedFile(
            RelativePath: $"Platform/{view.Name}/Commands.g.cs",
            Content: EmitCommandsFile(view, options, includeLibName));
    }
}
```

Move the current private `EmitCommandsFile` and `JoinParameters` methods from `CsCommandEmitter` into this type with their existing bodies. Keep generated relative paths unchanged.

- [ ] **Step 5: Move public artifact emitters**

Run:

```pwsh
git mv build/_build/Targets/GenerateBindings/Emitting/CsConstantEmitter.cs build/_build/Targets/GenerateBindings/Emit/PublicApi/ConstantEmitter.cs
git mv build/_build/Targets/GenerateBindings/Emitting/CsEnumEmitter.cs build/_build/Targets/GenerateBindings/Emit/PublicApi/EnumEmitter.cs
git mv build/_build/Targets/GenerateBindings/Emitting/CsHandleEmitter.cs build/_build/Targets/GenerateBindings/Emit/PublicApi/HandleEmitter.cs
git mv build/_build/Targets/GenerateBindings/Emitting/CsStructEmitter.cs build/_build/Targets/GenerateBindings/Emit/PublicApi/StructEmitter.cs
git mv build/_build/Targets/GenerateBindings/Emitting/CsCallbackEmitter.cs build/_build/Targets/GenerateBindings/Emit/PublicApi/CallbackEmitter.cs
git mv build/_build/Targets/GenerateBindings/Emitting/FixedArrayEmissionPolicy.cs build/_build/Targets/GenerateBindings/Emit/PublicApi/FixedArrayEmissionPolicy.cs
```

Rename `Cs*Emitter` types to `ConstantEmitter`, `EnumEmitter`, `HandleEmitter`, `StructEmitter`, and `CallbackEmitter`. These still emit public generated type artifacts; they do not emit public wrapper methods in M2.

- [ ] **Step 6: Extract parse-view report builder**

Move report DTOs:

```pwsh
git mv build/_build/Targets/GenerateBindings/Emitting/BindingParseViewReport.cs build/_build/Targets/GenerateBindings/Emit/Reports/BindingParseViewReport.cs
```

Create `Emit/Reports/BindingParseViewReportBuilder.cs` and move the existing report projection into it unchanged:

```csharp
namespace Build.Targets.GenerateBindings.Emit.Reports;

internal static class BindingParseViewReportBuilder
{
    public static BindingParseViewReport Build(BindingModel model, IReadOnlyList<string> emittedFiles)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(emittedFiles);

        return new BindingParseViewReport(
            SchemaVersion: 1,
            Categories: BuildCategories(model),
            EmittedFiles: emittedFiles,
            Views: BuildViewEntries(model),
            MacroConstants: BuildMacroConstants(model));
    }
}
```

Move the existing categories, view-entry, and macro-report projection expressions from `BindingEmitter` into private helper methods on `BindingParseViewReportBuilder` without changing the produced JSON shape. `BindingEmitter` remains responsible for JSON serialization unless a further extraction is clearly simpler.

- [ ] **Step 7: Move TFM policy**

Run:

```pwsh
git mv build/_build/Targets/GenerateBindings/Emitting/ModernCIntegerEmissionPolicy.cs build/_build/Targets/GenerateBindings/Emit/Tfm/ModernCIntegerEmissionPolicy.cs
```

Namespace:

```csharp
namespace Build.Targets.GenerateBindings.Emit.Tfm;
```

- [ ] **Step 8: Move emitter tests**

Move tests to mirror useful production concepts:

```pwsh
git mv build/_build.Tests/Unit/Targets/GenerateBindings/Emitting/GeneratedFileSetSnapshotTests.cs build/_build.Tests/Unit/Targets/GenerateBindings/Emit/GeneratedFileSetSnapshotTests.cs
git mv build/_build.Tests/Unit/Targets/GenerateBindings/Emitting/BindingModelData.cs build/_build.Tests/Unit/Targets/GenerateBindings/Emit/BindingModelData.cs
git mv build/_build.Tests/Unit/Targets/GenerateBindings/Emitting/BindingParseViewReportTests.cs build/_build.Tests/Unit/Targets/GenerateBindings/Emit/Reports/BindingParseViewReportTests.cs
git mv build/_build.Tests/Unit/Targets/GenerateBindings/Emitting/SemanticEmitterTests.cs build/_build.Tests/Unit/Targets/GenerateBindings/Emit/PublicApi/SemanticEmitterTests.cs
```

Split `CsCommandEmitterTests.cs` into:

```text
Unit/Targets/GenerateBindings/Emit/BindingEmitterTests.cs
Unit/Targets/GenerateBindings/Emit/RawAbi/RawAbiCommandEmitterTests.cs
Unit/Targets/GenerateBindings/Emit/PublicApi/PublicApiEmitterTests.cs
Unit/Targets/GenerateBindings/Emit/Reports/BindingParseViewReportBuilderTests.cs
```

Use `git mv` for the first split file, then create the remaining concept files by moving whole test methods:

```pwsh
git mv build/_build.Tests/Unit/Targets/GenerateBindings/Emitting/CsCommandEmitterTests.cs build/_build.Tests/Unit/Targets/GenerateBindings/Emit/BindingEmitterTests.cs
```

Preserve existing test method names. Rename class names only to match the moved concept. Update `SemanticEmitterTests.cs` calls from `CsCommandEmitter.Emit(model, options)` to `new BindingEmitter().Emit(model, options)` or the final constructor shape if `BindingEmitter` takes collaborators.

- [ ] **Step 9: Verify emitter and snapshot tests**

Run:

```pwsh
dotnet test --project build/_build.Tests/Build.Tests.csproj -c Release --framework net10.0 -- --treenode-filter "/*/*/BindingEmitterTests/*"
dotnet test --project build/_build.Tests/Build.Tests.csproj -c Release --framework net10.0 -- --treenode-filter "/*/*/RawAbiCommandEmitterTests/*"
dotnet test --project build/_build.Tests/Build.Tests.csproj -c Release --framework net10.0 -- --treenode-filter "/*/*/PublicApiEmitterTests/*"
dotnet test --project build/_build.Tests/Build.Tests.csproj -c Release --framework net10.0 -- --treenode-filter "/*/*/BindingParseViewReportBuilderTests/*"
dotnet test --project build/_build.Tests/Build.Tests.csproj -c Release --framework net10.0 -- --treenode-filter "/*/*/GeneratedFileSetSnapshotTests/*"
dotnet test --project build/_build.Tests/Build.Tests.csproj -c Release --framework net10.0 -- --treenode-filter "/*/*/SemanticEmitterTests/*"
```

Expected: PASS. The `GeneratedFileSetSnapshotTests` verified content must not change.

## Task 8: Update Documentation And Roadmap Links

**Files:**
- Modify: `docs/binding-autogen/README.md`
- Modify: `docs/binding-autogen/binding-generator-roadmap.md`
- Modify if needed: `docs/binding-autogen/binding-generator-constitution.md`
- Modify if needed: `docs/playbook/binding-generator-maintenance.md`

- [ ] **Step 1: Update roadmap M2 section**

Ensure `binding-generator-roadmap.md` links this plan:

```markdown
**Detailed plan:** [`milestones/milestone-2-behavior-preserving-topology-refactor.md`](milestones/milestone-2-behavior-preserving-topology-refactor.md).
```

Ensure the target shape does not imply empty M2 folders for `Profiles/` or `Emit/Friendly/`.

- [ ] **Step 2: Update binding-autogen README**

Add this plan to the folder layout and reading order next to M1.

- [ ] **Step 3: Keep grand docs aligned with the policy/profile direction**

Ensure the constitution and roadmap state that M2 exposes an SDL policy seam, while M3 owns the real CppAst ABI engine plus SDL family profile/config boundary. If implementation only moves code after these docs are aligned, do not add more constitution churn.

## Task 9: Full Verification Gate

**Files:**
- No planned edits.

- [ ] **Step 1: Run full build-host suite**

Run:

```pwsh
dotnet test --project build/_build.Tests/Build.Tests.csproj -c Release --framework net10.0
```

Expected: PASS.

- [ ] **Step 2: Run Linux-only semantic snapshots**

Run:

```pwsh
docker run --rm -v "$((Get-Location).Path):/workspace" -w /workspace mcr.microsoft.com/dotnet/sdk:10.0.203 dotnet test --project build/_build.Tests/Build.Tests.csproj -c Release --framework net10.0 -- --treenode-filter "/*/*/SemanticModelSnapshotTests/*"
```

Expected: PASS, `2` tests succeeded.

- [ ] **Step 3: Refresh real generated preview**

Run:

```pwsh
dotnet run --file tools.cs -- generate-bindings
```

Expected: succeeds and writes the same SDL2.Core preview file inventory. Existing documented dynapi warnings may remain for `SDL_LogMessageV`, `SDL_RWFromFP`, `SDL_vasprintf`, `SDL_vsnprintf`, and `SDL_vsscanf`.

- [ ] **Step 4: Run opt-in generated-preview snapshot**

Run:

```pwsh
$env:JANSET_VERIFY_GENERATED_PREVIEW = "1"; dotnet test --project build/_build.Tests/Build.Tests.csproj -c Release --framework net10.0 -- --treenode-filter "/*/*/GeneratedPreviewSnapshotTests/*"
```

Expected: PASS. If the snapshot changes, inspect whether the generated file inventory/hash changed because of an approved safety fix; otherwise treat as a regression.

- [ ] **Step 5: Run compile-check**

Run:

```pwsh
dotnet build tests/binding-compile-check/SDL2.Core.CompileCheck.csproj -c Release
```

Expected: PASS, `0 Warning(s)`, `0 Error(s)`.

- [ ] **Step 6: Run package consumer smoke after central test/runtime changes**

Run this only if M2 changes package versions, build output paths, generated preview files, or compile-check behavior:

```pwsh
dotnet test --project tests/smoke-tests/package-smoke/PackageConsumer.Smoke/PackageConsumer.Smoke.csproj -c Release
```

Expected: PASS across executable TFMs.

- [ ] **Step 7: Run hygiene gates**

Run:

```pwsh
git --no-pager diff --check
slopwatch analyze --fail-on warning --exclude "artifacts/**,external/**,vcpkg_installed/**,**/bin/**,**/obj/**"
```

Expected: no whitespace errors and `0 issue(s) found`.

- [ ] **Step 8: Ensure no transient Verify files are present**

Run:

```pwsh
git status --short
```

Expected: no `*.received.*` files. Commit `*.verified.*` only when a reviewed snapshot intentionally changed.

## Review Checkpoints

Stop for review after each checkpoint before committing:

1. Safety RED/GREEN fixes.
2. `BindingFamilyGeneration` extraction.
3. Parse/platform-view move.
4. Model-building topology and record collapse.
5. Emit topology.
6. Docs and final verification.

Commit boundaries are intentionally review checkpoints, not mandatory commits. Deniz decides whether to squash, keep multiple commits, or adjust messages after seeing the final diff.

## M3 Planning Carry-Forward

The M2 review intentionally deferred several valid objections because fixing them inside this milestone would change policy boundaries rather than topology. The canonical M3 planning guidance now lives in [`../binding-generator-roadmap.md`](../binding-generator-roadmap.md) under **Milestone 3: CppAst Engine, Family Profiles, And Manifest Boundary**.

Carry these findings into the M3 detailed plan before touching code:

- SDL2-specific type policy, especially `SDL_bool`, must move behind an explicit SDL profile/policy seam instead of living inside the legacy `TypeMappingPolicy` bucket.
- Spike-era bridges such as `LegacyBindingTypeRefBridge` and broad `BindingTypeRef` usage should be retired or constrained once required manifest declarations can produce semantic `NativeTypeRef` values directly.
- Foreign ABI mapping for C runtime, Vulkan, GDK, and Windows COM types should remain separate from SDL policy even when SDL headers are the reason those names appear.
- SDL2.Core platform macro hygiene should be catalog/profile-owned rather than presented as a generic `PlatformCatalog` law.
- Repo-wide `Result<T,TError>` remains for expected operation failures; generator output-bag records should be renamed only when it improves clarity, not mechanically converted.
- Behavior preservation stays mandatory unless a RED test proves an ABI/API bug.
