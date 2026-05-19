# SDL2.Core P0 Translation Fixes Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Remove the current SDL2.Core generated-binding P0 translation blockers by making each risky C declaration either ABI-correct or explicitly quarantined.

**Architecture:** Keep the existing Cake-hosted CppAst semantic pipeline. Add real `.h` fixture coverage for each blocker, then make the smallest translator/policy change that prevents false generated C# from reaching `artifacts/generated-bindings-preview`. For declarations that cannot be represented honestly across the current target frameworks, prefer explicit Stage 1 quarantine over success-shaped lies.

**Tech Stack:** .NET 10, C# 14, Cake Frosting, CppAst, TUnit on Microsoft.Testing.Platform, Docker Linux binding-generator image, `tools.cs generate-bindings`, generated binding compile-check, Slopwatch.

---

## Execution Guardrails

- Use TDD. No production translator change starts before a failing test has been run and observed.
- Every P0 fix starts with a real `.h` fixture-backed test, not only hand-built `CppAst` object tests.
- Use the Linux Docker command override for `[LinuxOnly]` semantic fixture tests:

```pwsh
$repo = (Get-Location).Path; docker run --rm --entrypoint dotnet -v "${repo}:/workspace" -w /workspace "janset-binding-generator:focal-latest" test --project "/workspace/build/_build.Tests/Build.Tests.csproj" -c Release --framework net10.0 -- --treenode-filter "/*/*/SemanticHeaderFixtureTests/*"
```

- Use focused Windows/host unit tests for non-Linux-only collaborator/emitter tests:

```pwsh
dotnet test --project build\_build.Tests\Build.Tests.csproj -c Release --framework net10.0 -- --treenode-filter "/*/*/<ClassName>/*"
```

- Use `tools.cs generate-bindings` only as a generation/probe gate after focused tests are green.
- Do not commit without Deniz's explicit approval. Commit steps below are approval checkpoints and proposed messages only.
- Use `apply_patch` for manual edits.
- Run Slopwatch after code/test/project changes:

```pwsh
slopwatch analyze --fail-on warning --exclude "artifacts/**,external/**,vcpkg_installed/**,**/bin/**,**/obj/**"
```

## File Structure

### Test fixtures to create

- `build\_build.Tests\Fixtures\Data\GenerateBindings\SemanticTypes\opaque-handle-aliases.h`
  - Covers `typedef struct tag Name;` aliases that currently leak both the tag and typedef as handles.
- `build\_build.Tests\Fixtures\Data\GenerateBindings\SemanticTypes\platform-c-integers-and-wide-strings.h`
  - Covers C `long`, `unsigned long`, `SDL_threadID`, `wchar_t*` fields, and `wchar_t*` function parameters.
- `build\_build.Tests\Fixtures\Data\GenerateBindings\SemanticTypes\rwops-platform-conditioned-layout.h`
  - Covers `SDL_RWops` with a Windows-sized hidden union arm so Stage 1 proves it does not emit a false full layout.
- `build\_build.Tests\Fixtures\Data\GenerateBindings\SemanticTypes\enums-and-platform-control-macros.h`
  - Covers SDL2 `SDL_bool`, known flag-like enums, and `SDL_WINAPI_FAMILY_PHONE`.

### Test files to modify

- `build\_build.Tests\Unit\Targets\GenerateBindings\Translation\SemanticHeaderFixtureTests.cs`
  - Adds Linux-only parse/translate tests backed by the new P0 headers.
- `build\_build.Tests\Unit\Targets\GenerateBindings\Translation\BindingTranslationCollaboratorTests.cs`
  - Adds narrow unit tests for handle canonicalization, `SDL_RWops` opaque quarantine, enum flags, and `SDL_bool` backing behavior.
- `build\_build.Tests\Unit\Targets\GenerateBindings\Translation\TypeMappingPolicyTests.cs`
  - Updates C `long` / `unsigned long` expectations away from `nint` / `nuint`.
- `build\_build.Tests\Unit\Targets\GenerateBindings\Translation\NativeTypeClassifierTests.cs`
  - Adds focused `wchar_t*` and C long classification tests.
- `build\_build.Tests\Unit\Targets\GenerateBindings\Translation\MacroApiPolicyTests.cs`
  - Adds the `SDL_WINAPI_FAMILY_PHONE` non-API regression guard.
- `build\_build.Tests\Unit\Targets\GenerateBindings\Emitting\CsCommandEmitterTests.cs`
  - Adds the CLong/CULong downlevel quarantine emission guard.

### Production files to create

- `build\_build\Targets\GenerateBindings\Translation\SdlOpaqueStructPolicy.cs`
  - Owns Stage 1 struct-layout quarantines such as `SDL_RWops`.

### Production files to modify

- `build\_build\Targets\GenerateBindings\Translation\TypeMappingPolicy.cs`
  - Stops mapping C `long` / `unsigned long` to `nint` / `nuint`; maps them to `CLong` / `CULong` metadata for .NET 6+ raw ABI emission.
- `build\_build\Targets\GenerateBindings\Translation\NativeTypeClassifier.cs`
  - Preserves CLong/CULong classification and maps `wchar_t*` to opaque pointer storage.
- `build\_build\Targets\GenerateBindings\Translation\BindableDeclarationPolicy.cs`
  - Excludes `SdlOpaqueStructPolicy` names from public struct emission.
- `build\_build\Targets\GenerateBindings\Translation\BindingHandleTranslator.cs`
  - Prefers public typedef names over underlying opaque struct tags.
- `build\_build\Targets\GenerateBindings\Translation\BindingEnumTranslator.cs`
  - Forces SDL2 `SDL_bool` to int-backed and recognizes known SDL flag-like enums.
- `build\_build\Targets\GenerateBindings\Translation\MacroApiPolicy.cs`
  - Classifies `SDL_WINAPI_FAMILY_PHONE` as a platform-control macro.
- `build\_build\Targets\GenerateBindings\Emitting\CsCommandEmitter.cs`
  - Wraps functions using `CLong` / `CULong` in `#if NET6_0_OR_GREATER` so downlevel TFMs do not compile false ABI signatures.

### Documentation files to modify

- `docs\binding-autogen\binding-translation-contract.md`
  - Update the current P0 blocker list after fixes land, noting any quarantines.
- `docs\plan.md`
  - Update Phase 4 status if the P0 blocker state changes materially.

---

## Task 1: Duplicate Opaque Handle Canonicalization

**Files:**
- Create: `build\_build.Tests\Fixtures\Data\GenerateBindings\SemanticTypes\opaque-handle-aliases.h`
- Modify: `build\_build.Tests\Unit\Targets\GenerateBindings\Translation\SemanticHeaderFixtureTests.cs`
- Modify: `build\_build.Tests\Unit\Targets\GenerateBindings\Translation\BindingTranslationCollaboratorTests.cs`
- Modify: `build\_build\Targets\GenerateBindings\Translation\BindingHandleTranslator.cs`

- [ ] **Step 1: Add the fixture header**

Create `build\_build.Tests\Fixtures\Data\GenerateBindings\SemanticTypes\opaque-handle-aliases.h`:

```c
typedef struct SDL_hid_device_ SDL_hid_device;
typedef struct SDL_semaphore SDL_sem;
typedef struct SDL_Window SDL_Window;
```

- [ ] **Step 2: Add the RED fixture-backed translation test**

Append this test to `SemanticHeaderFixtureTests`:

```csharp
[Test]
public async Task Fixtures_Should_Translate_Opaque_Handle_Typedefs_Without_Tag_Leaks()
{
    var model = TranslateFixture("GenerateBindings/SemanticTypes/opaque-handle-aliases.h");

    var handleNames = model.Handles.Select(handle => handle.Name).ToArray();

    await Assert.That(handleNames).IsEquivalentTo(["SDL_hid_device", "SDL_sem", "SDL_Window"]);
    await Assert.That(handleNames).DoesNotContain("SDL_hid_device_");
    await Assert.That(handleNames).DoesNotContain("SDL_semaphore");
}
```

Also add this helper near the existing `ParseFixture` helper:

```csharp
private static BindingModel TranslateFixture(string fixturePath, bool parseMacros = false)
{
    var compilation = ParseFixture(fixturePath, parseMacros, parseAsSdl2Header: true);
    return CppAstToBindingModel.Translate(
        [ParseResult("Neutral", compilation)],
        BindingGenerationFixture.Sdl2CoreConfig(),
        requiredFunctions: []);
}
```

- [ ] **Step 3: Run the RED fixture test**

Run:

```pwsh
$repo = (Get-Location).Path; docker run --rm --entrypoint dotnet -v "${repo}:/workspace" -w /workspace "janset-binding-generator:focal-latest" test --project "/workspace/build/_build.Tests/Build.Tests.csproj" -c Release --framework net10.0 -- --treenode-filter "/*/*/SemanticHeaderFixtureTests/*Fixtures_Should_Translate_Opaque_Handle_Typedefs_Without_Tag_Leaks"
```

Expected: FAIL because `SDL_hid_device_` and `SDL_semaphore` are still emitted.

- [ ] **Step 4: Add the focused collaborator RED test**

Append this test to `BindingHandleTranslatorTests`:

```csharp
[Test]
public async Task Extract_Should_Prefer_Public_Typedef_Name_Over_Opaque_Struct_Tag()
{
    var translator = CreateTranslator();
    var hidTag = new CppClass("SDL_hid_device_")
    {
        ClassKind = CppClassKind.Struct,
        IsDefinition = false,
        Span = SdlHeaderSpan("SDL_hidapi.h"),
    };
    var hidTypedef = new CppTypedef("SDL_hid_device", hidTag)
    {
        Span = SdlHeaderSpan("SDL_hidapi.h"),
    };
    var semaphoreTag = new CppClass("SDL_semaphore")
    {
        ClassKind = CppClassKind.Struct,
        IsDefinition = false,
        Span = SdlHeaderSpan("SDL_mutex.h"),
    };
    var semaphoreTypedef = new CppTypedef("SDL_sem", semaphoreTag)
    {
        Span = SdlHeaderSpan("SDL_mutex.h"),
    };

    var handles = translator.Extract(new NativeDeclarationCatalog([], [hidTag, semaphoreTag], [], [hidTypedef, semaphoreTypedef]));

    await Assert.That(handles.Select(handle => handle.Name).ToArray())
        .IsEquivalentTo(["SDL_hid_device", "SDL_sem"]);
}
```

Run:

```pwsh
dotnet test --project build\_build.Tests\Build.Tests.csproj -c Release --framework net10.0 -- --treenode-filter "/*/*/BindingHandleTranslatorTests/*Extract_Should_Prefer_Public_Typedef_Name_Over_Opaque_Struct_Tag"
```

Expected: FAIL for duplicate tag handles.

- [ ] **Step 5: Implement canonical handle filtering**

Modify `BindingHandleTranslator` so class-derived handles are skipped when an owned public typedef aliases that opaque class tag:

```csharp
public IReadOnlyList<BindingHandle> Extract(NativeDeclarationCatalog catalog)
{
    ArgumentNullException.ThrowIfNull(catalog);

    var aliasedOpaqueTags = FindAliasedOpaqueTags(catalog.Typedefs);
    var handles = new Dictionary<string, BindingHandle>(StringComparer.Ordinal);

    foreach (var handle in ExtractClasses(catalog.Classes, aliasedOpaqueTags).Concat(ExtractTypedefs(catalog.Typedefs)))
    {
        handles.TryAdd(handle.Name, handle);
    }

    return handles.Values
        .OrderBy(handle => handle.Name, StringComparer.Ordinal)
        .ToList();
}

private HashSet<string> FindAliasedOpaqueTags(IEnumerable<CppTypedef> typedefs)
{
    var tags = new HashSet<string>(StringComparer.Ordinal);
    foreach (var typedef in typedefs.Where(_declarationPolicy.IsBindableOwnedType))
    {
        var element = UnwrapQualified(typedef.ElementType);
        if (element is CppClass cls && IsOpaqueClass(cls))
        {
            tags.Add(cls.Name);
        }
    }

    return tags;
}

private IEnumerable<BindingHandle> ExtractClasses(IEnumerable<CppClass> classes, IReadOnlySet<string> aliasedOpaqueTags) =>
    classes
        .Where(cls => !aliasedOpaqueTags.Contains(cls.Name))
        .Where(_declarationPolicy.IsBindableOwnedType)
        .Select(cls => _typeClassifier.Classify(cls, Path.GetFileName(cls.SourceFile ?? string.Empty)))
        .Select(TryCreateHandle)
        .Where(handle => handle is not null)
        .Select(handle => handle!);

private static bool IsOpaqueClass(CppClass cls) =>
    !cls.IsDefinition || (cls.SizeOf == 0 && cls.Fields.Count == 0);

private static CppType UnwrapQualified(CppType type)
{
    while (type is CppQualifiedType qualified)
        type = qualified.ElementType;

    return type;
}
```

- [ ] **Step 6: Verify GREEN**

Run the focused collaborator test and Docker fixture test again. Expected: PASS.

- [ ] **Step 7: Commit checkpoint**

Proposed commit message after approval:

```text
fix: canonicalize generated opaque handle typedefs
```

---

## Task 2: C `long` / `unsigned long` ABI Mapping

**Files:**
- Create: `build\_build.Tests\Fixtures\Data\GenerateBindings\SemanticTypes\platform-c-integers-and-wide-strings.h`
- Modify: `build\_build.Tests\Unit\Targets\GenerateBindings\Translation\SemanticHeaderFixtureTests.cs`
- Modify: `build\_build.Tests\Unit\Targets\GenerateBindings\Translation\TypeMappingPolicyTests.cs`
- Modify: `build\_build.Tests\Unit\Targets\GenerateBindings\Translation\NativeTypeClassifierTests.cs`
- Modify: `build\_build.Tests\Unit\Targets\GenerateBindings\Emitting\CsCommandEmitterTests.cs`
- Modify: `build\_build\Targets\GenerateBindings\Translation\TypeMappingPolicy.cs`
- Modify: `build\_build\Targets\GenerateBindings\Translation\NativeTypeClassifier.cs`
- Modify: `build\_build\Targets\GenerateBindings\Emitting\CsCommandEmitter.cs`

- [ ] **Step 1: Add the fixture header**

Create `build\_build.Tests\Fixtures\Data\GenerateBindings\SemanticTypes\platform-c-integers-and-wide-strings.h`:

```c
#include <stddef.h>

typedef unsigned long SDL_threadID;

long SDL_lround(double x);
unsigned long SDL_strtoul(const char* text, char** endp, int radix);
SDL_threadID SDL_ThreadID(void);

typedef struct SDL_hid_device_info
{
    wchar_t* serial_number;
    wchar_t* manufacturer_string;
    wchar_t* product_string;
    struct SDL_hid_device_info* next;
} SDL_hid_device_info;

int SDL_hid_get_serial_number_string(void* device, wchar_t* string, size_t maxlen);
```

- [ ] **Step 2: Add the RED fixture-backed C long test**

Append this test to `SemanticHeaderFixtureTests`:

```csharp
[Test]
public async Task Fixtures_Should_Translate_C_Long_And_Unsigned_Long_As_Platform_C_Integers()
{
    var model = TranslateFixture("GenerateBindings/SemanticTypes/platform-c-integers-and-wide-strings.h");
    var functions = model.Views.Single().Functions;

    await Assert.That(functions.Single(function => function.Name == "SDL_lround").ReturnType.ManagedName)
        .IsEqualTo("CLong");
    await Assert.That(functions.Single(function => function.Name == "SDL_strtoul").ReturnType.ManagedName)
        .IsEqualTo("CULong");
    await Assert.That(functions.Single(function => function.Name == "SDL_ThreadID").ReturnType.ManagedName)
        .IsEqualTo("CULong");
}
```

Run with Docker and the exact test filter. Expected: FAIL because current output is `nint` / `nuint`.

- [ ] **Step 3: Add focused RED mapping tests**

Update `TypeMappingPolicyTests`:

```csharp
[Test]
public async Task MapPrimitive_Should_Emit_CLong_For_C_Long()
{
    await Assert.That(TypeMappingPolicy.MapPrimitive(CppPrimitiveType.Long).ManagedName).IsEqualTo("CLong");
}

[Test]
public async Task MapPrimitive_Should_Emit_CULong_For_C_Unsigned_Long()
{
    await Assert.That(TypeMappingPolicy.MapPrimitive(CppPrimitiveType.UnsignedLong).ManagedName).IsEqualTo("CULong");
}
```

Remove or replace the old tests that expect `nint` / `nuint` for C `long` / `unsigned long`.

Run:

```pwsh
dotnet test --project build\_build.Tests\Build.Tests.csproj -c Release --framework net10.0 -- --treenode-filter "/*/*/TypeMappingPolicyTests/*"
```

Expected: FAIL on the new CLong/CULong expectations.

- [ ] **Step 4: Add the RED classifier metadata test**

Append this test to `NativeTypeClassifierTests` before the emitter test:

```csharp
[Test]
public async Task Classify_Should_Preserve_CLong_And_CULong_Primitive_Metadata()
{
    var classifier = CreateClassifier();

    var signedLong = classifier.Classify(CppPrimitiveType.Long, sourceHeader: "SDL_stdinc.h");
    var unsignedLong = classifier.Classify(CppPrimitiveType.UnsignedLong, sourceHeader: "SDL_stdinc.h");

    await Assert.That(signedLong.NativeName).IsEqualTo("long");
    await Assert.That(signedLong.ManagedName).IsEqualTo("CLong");
    await Assert.That(unsignedLong.NativeName).IsEqualTo("unsigned long");
    await Assert.That(unsignedLong.ManagedName).IsEqualTo("CULong");
}
```

Run the focused classifier test. Expected: FAIL.

- [ ] **Step 5: Add the RED emitter quarantine test**

Append this test to `CsCommandEmitterTests`:

```csharp
[Test]
public async Task Emit_Should_Guard_CLong_Functions_To_Modern_Tfms()
{
    var model = new BindingModel(
        [new BindingParseView(
            "Neutral",
            null,
            [new BindingFunction(
                "SDL_lround",
                NativeTypeRef.Primitive("long", "CLong", NativeAbiShape.Of("CLong")),
                [new BindingParameter(NativeTypeRef.Primitive("double", "double", NativeAbiShape.Of("double", 8)), "x")],
                "SDL_stdinc.h")])]);

    var fileSet = CsCommandEmitter.Emit(model, new BindingEmissionOptions("SDL2", "SDL"));
    var commands = fileSet.Files.Single(file => file.RelativePath == "Platform/Neutral/Commands.g.cs").Content;

    await Assert.That(commands).Contains("#if NET6_0_OR_GREATER");
    await Assert.That(commands).Contains("internal static extern CLong SDL_lround(double x);");
    await Assert.That(commands).Contains("#endif");
}
```

Run the focused emitter test. Expected: FAIL because the method is currently unguarded.

- [ ] **Step 6: Implement CLong/CULong mapping**

Modify `TypeMappingPolicy.MapPrimitive`:

```csharp
CppPrimitiveKind.Long => "CLong",
CppPrimitiveKind.UnsignedLong => "CULong",
```

Update the XML/comment block above `MapPrimitive` so it says C `long` is platform-sensitive and must not be represented as `nint` / `nuint`.

Modify `NativeTypeClassifier.PrimitiveSizeBytes`:

```csharp
"CLong" or "CULong" => null,
```

- [ ] **Step 7: Implement .NET 6+ CLong/CULong command guards**

Modify `CsCommandEmitter.EmitCommandsFile` so each function using `CLong` or `CULong` is wrapped in `#if NET6_0_OR_GREATER`:

```csharp
foreach (var function in view.Functions)
{
    var requiresModernCInteger = UsesModernCInteger(function);
    if (requiresModernCInteger)
    {
        builder.AppendLf();
        builder.AppendLf("#if NET6_0_OR_GREATER");
    }
    else
    {
        builder.AppendLf();
    }

    // existing attribute + extern emission stays here

    if (requiresModernCInteger)
    {
        builder.AppendLf("#endif");
    }
}

private static bool UsesModernCInteger(BindingFunction function) =>
    IsModernCInteger(function.ReturnType) || function.Parameters.Any(parameter => IsModernCInteger(parameter.Type));

private static bool IsModernCInteger(NativeTypeRef type) =>
    type.ManagedName is "CLong" or "CULong";
```

This is a deliberate Stage 1 downlevel quarantine: .NET 6+ TFMs get exact BCL C integer ABI, while `netstandard2.0` / `net462` do not compile false `long` signatures.

- [ ] **Step 8: Verify GREEN**

Run the TypeMapping, NativeTypeClassifier, CsCommandEmitter, and Docker fixture tests. Expected: PASS.

- [ ] **Step 9: Commit checkpoint**

Proposed commit message after approval:

```text
fix: stop mapping C long to native-sized integers
```

---

## Task 3: `wchar_t*` Opaque Pointer Translation

**Files:**
- Modify: `build\_build.Tests\Unit\Targets\GenerateBindings\Translation\SemanticHeaderFixtureTests.cs`
- Modify: `build\_build.Tests\Unit\Targets\GenerateBindings\Translation\NativeTypeClassifierTests.cs`
- Modify: `build\_build\Targets\GenerateBindings\Translation\NativeTypeClassifier.cs`

- [ ] **Step 1: Add the RED fixture-backed wide string test**

Append this test to `SemanticHeaderFixtureTests`:

```csharp
[Test]
public async Task Fixtures_Should_Translate_WChar_Pointers_As_Opaque_Pointers()
{
    var model = TranslateFixture("GenerateBindings/SemanticTypes/platform-c-integers-and-wide-strings.h");

    var hidInfo = model.Structs.Single(structure => structure.Name == "SDL_hid_device_info");
    var serial = hidInfo.Fields.Single(field => field.Name == "serial_number");
    var manufacturer = hidInfo.Fields.Single(field => field.Name == "manufacturer_string");
    var getSerial = model.Views.Single().Functions.Single(function => function.Name == "SDL_hid_get_serial_number_string");

    await Assert.That(serial.Type.ManagedName).IsEqualTo("nint");
    await Assert.That(manufacturer.Type.ManagedName).IsEqualTo("nint");
    await Assert.That(getSerial.Parameters.Single(parameter => parameter.Name == "@string").Type.ManagedName).IsEqualTo("nint");
}
```

Run with Docker and the exact test filter. Expected: FAIL because current translation emits a typed primitive pointer.

- [ ] **Step 2: Add the focused classifier RED test**

Append this test to `NativeTypeClassifierTests`:

```csharp
[Test]
public async Task Classify_Should_Map_WChar_Pointer_To_Opaque_Native_Pointer()
{
    var classifier = CreateClassifier();
    var pointer = new CppPointerType(CppPrimitiveType.WChar);

    var sut = classifier.Classify(pointer, sourceHeader: "SDL_hidapi.h");

    await Assert.That(sut.NativeName).IsEqualTo("wchar_t*");
    await Assert.That(sut.ManagedName).IsEqualTo("nint");
    await Assert.That(sut.PointerDepth).IsEqualTo(1);
}
```

Run the focused classifier test. Expected: FAIL.

- [ ] **Step 3: Implement `wchar_t*` opaque pointer classification**

Modify `NativeTypeClassifier.CreatePointerRef` before the `IsCharPrimitive` branch:

```csharp
if (elementRef.NativeName == "wchar_t")
{
    return pointerDepth == 1
        ? new NativeTypeRef("wchar_t*", "nint", NativeTypeKind.TypedPointer, 1,
            elementRef.OwningFamilyId, sourceHeader, NativeAbiShape.Of("nint", IntPtr.Size), elementRef, elementRef.Diagnostics)
        : NativeTypeRef.Indirection(elementRef, pointerDepth, "nint" + RepeatStars(pointerDepth - 1));
}
```

Leave friendly wide-string decoding out of this task. The fix is only to stop exposing a false shared `int*` / `char*` shape.

- [ ] **Step 4: Verify GREEN**

Run the focused classifier test and Docker fixture test. Expected: PASS.

- [ ] **Step 5: Commit checkpoint**

Proposed commit message after approval:

```text
fix: map wchar pointers to opaque native pointers
```

---

## Task 4: `SDL_RWops` False Layout Quarantine

**Files:**
- Create: `build\_build.Tests\Fixtures\Data\GenerateBindings\SemanticTypes\rwops-platform-conditioned-layout.h`
- Create: `build\_build\Targets\GenerateBindings\Translation\SdlOpaqueStructPolicy.cs`
- Modify: `build\_build.Tests\Unit\Targets\GenerateBindings\Translation\SemanticHeaderFixtureTests.cs`
- Modify: `build\_build.Tests\Unit\Targets\GenerateBindings\Translation\BindingTranslationCollaboratorTests.cs`
- Modify: `build\_build\Targets\GenerateBindings\Translation\BindableDeclarationPolicy.cs`
- Modify: `build\_build\Targets\GenerateBindings\Translation\NativeTypeClassifier.cs`

- [ ] **Step 1: Add the fixture header**

Create `build\_build.Tests\Fixtures\Data\GenerateBindings\SemanticTypes\rwops-platform-conditioned-layout.h`:

```c
typedef long long Sint64;
typedef unsigned int Uint32;
typedef unsigned char Uint8;
typedef unsigned long size_t;
typedef int SDL_bool;

typedef struct SDL_RWops
{
    Sint64 (*size)(struct SDL_RWops* context);
    Sint64 (*seek)(struct SDL_RWops* context, Sint64 offset, int whence);
    size_t (*read)(struct SDL_RWops* context, void* ptr, size_t size, size_t maxnum);
    size_t (*write)(struct SDL_RWops* context, const void* ptr, size_t size, size_t num);
    int (*close)(struct SDL_RWops* context);
    Uint32 type;
    union
    {
        struct
        {
            SDL_bool append;
            void* h;
            struct
            {
                void* data;
                size_t size;
                size_t left;
            } buffer;
        } windowsio;
        struct
        {
            Uint8* base;
            Uint8* here;
            Uint8* stop;
        } mem;
    } hidden;
} SDL_RWops;

SDL_RWops* SDL_RWFromMem(void* mem, int size);
```

- [ ] **Step 2: Add the RED fixture-backed quarantine test**

Append this test to `SemanticHeaderFixtureTests`:

```csharp
[Test]
public async Task Fixtures_Should_Quarantine_SDL_RWops_False_Full_Layout_As_Opaque_Handle()
{
    var model = TranslateFixture("GenerateBindings/SemanticTypes/rwops-platform-conditioned-layout.h");

    await Assert.That(model.Structs.Select(structure => structure.Name).ToArray()).DoesNotContain("SDL_RWops");
    await Assert.That(model.Structs.Select(structure => structure.Name).ToArray()).DoesNotContain("SDL_RWops_hidden");
    await Assert.That(model.Handles.Select(handle => handle.Name).ToArray()).Contains("SDL_RWops");

    var rwFromMem = model.Views.Single().Functions.Single(function => function.Name == "SDL_RWFromMem");
    await Assert.That(rwFromMem.ReturnType.ManagedName).IsEqualTo("SDL_RWops");
}
```

Run with Docker and the exact test filter. Expected: FAIL because `SDL_RWops` / `SDL_RWops_hidden` are currently emitted as structs.

- [ ] **Step 3: Add focused RED classifier and struct-policy tests**

Append this test to `BindingStructTranslatorTests`:

```csharp
[Test]
public async Task Extract_Should_Not_Emit_SDL_RWops_As_Public_Struct()
{
    var translator = CreateTranslator();
    var compilation = new CppCompilation();
    var rwops = new CppClass("SDL_RWops")
    {
        ClassKind = CppClassKind.Struct,
        IsDefinition = true,
        SizeOf = 88,
        Span = SdlHeaderSpan("SDL_rwops.h"),
    };
    rwops.Fields.Add(new CppField(CppPrimitiveType.Int, "type"));
    compilation.Classes.Add(rwops);

    var structs = translator.Extract([ParseResult("Neutral", compilation)]);

    await Assert.That(structs.Select(structure => structure.Name).ToArray()).DoesNotContain("SDL_RWops");
}
```

Append this test to `NativeTypeClassifierTests`:

```csharp
[Test]
public async Task Classify_Should_Treat_SDL_RWops_Definition_As_Opaque_Handle_For_Stage1()
{
    var classifier = CreateClassifier();
    var rwops = new CppClass("SDL_RWops")
    {
        ClassKind = CppClassKind.Struct,
        IsDefinition = true,
        SizeOf = 88,
    };
    rwops.Fields.Add(new CppField(CppPrimitiveType.Int, "type"));

    var sut = classifier.Classify(rwops, sourceHeader: "SDL_rwops.h");

    await Assert.That(sut.Kind).IsEqualTo(NativeTypeKind.OpaqueHandle);
    await Assert.That(sut.ManagedName).IsEqualTo("SDL_RWops");
}
```

Run the focused tests. Expected: FAIL.

- [ ] **Step 4: Implement Stage 1 opaque struct policy**

Create `SdlOpaqueStructPolicy.cs`:

```csharp
using System.Collections.Frozen;

namespace Build.Targets.GenerateBindings.Translation;

internal static class SdlOpaqueStructPolicy
{
    private static readonly FrozenSet<string> OpaqueStructNames =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "SDL_RWops",
        }.ToFrozenSet(StringComparer.Ordinal);

    public static bool IsOpaqueStruct(string name) =>
        OpaqueStructNames.Contains(name);
}
```

Modify `BindableDeclarationPolicy.IsBindableStruct`:

```csharp
&& !SdlOpaqueStructPolicy.IsOpaqueStruct(cls.Name)
```

Modify `NativeTypeClassifier.Classify(CppClass cls, string? sourceHeader)` before the concrete-struct branch:

```csharp
if (SdlOpaqueStructPolicy.IsOpaqueStruct(cls.Name))
{
    return NativeTypeRef.OpaqueHandle(cls.Name, cls.Name, owningFamilyId, sourceHeader);
}
```

- [ ] **Step 5: Verify GREEN**

Run the focused classifier/struct tests and Docker fixture test. Expected: PASS.

- [ ] **Step 6: Commit checkpoint**

Proposed commit message after approval:

```text
fix: quarantine SDL_RWops false generated layout
```

---

## Task 5: `SDL_WINAPI_FAMILY_PHONE` Macro Leak

**Files:**
- Create: `build\_build.Tests\Fixtures\Data\GenerateBindings\SemanticTypes\enums-and-platform-control-macros.h`
- Modify: `build\_build.Tests\Unit\Targets\GenerateBindings\Translation\SemanticHeaderFixtureTests.cs`
- Modify: `build\_build.Tests\Unit\Targets\GenerateBindings\Translation\MacroApiPolicyTests.cs`
- Modify: `build\_build\Targets\GenerateBindings\Translation\MacroApiPolicy.cs`

- [ ] **Step 1: Add the fixture header**

Create `build\_build.Tests\Fixtures\Data\GenerateBindings\SemanticTypes\enums-and-platform-control-macros.h`:

```c
#define SDL_WINAPI_FAMILY_PHONE 2

typedef enum SDL_bool
{
    SDL_FALSE = 0,
    SDL_TRUE = 1
} SDL_bool;

typedef enum SDL_Keymod
{
    KMOD_NONE = 0x0000,
    KMOD_LSHIFT = 0x0001,
    KMOD_RSHIFT = 0x0002,
    KMOD_SHIFT = (KMOD_LSHIFT | KMOD_RSHIFT)
} SDL_Keymod;

typedef enum SDL_GLcontextFlag
{
    SDL_GL_CONTEXT_DEBUG_FLAG = 0x0001,
    SDL_GL_CONTEXT_FORWARD_COMPATIBLE_FLAG = 0x0002
} SDL_GLcontextFlag;

typedef enum SDL_RendererFlip
{
    SDL_FLIP_NONE = 0x00000000,
    SDL_FLIP_HORIZONTAL = 0x00000001,
    SDL_FLIP_VERTICAL = 0x00000002
} SDL_RendererFlip;
```

- [ ] **Step 2: Add the RED fixture-backed macro test**

Append this test to `SemanticHeaderFixtureTests`:

```csharp
[Test]
public async Task Fixtures_Should_Skip_Platform_Control_Macros()
{
    var compilation = ParseFixture(
        "GenerateBindings/SemanticTypes/enums-and-platform-control-macros.h",
        parseMacros: true,
        parseAsSdl2Header: true);
    var result = BindingConstantTranslator.Translate(
        [ParseResult("Neutral", compilation)],
        BindingGenerationFixture.Sdl2CoreConfig());

    await Assert.That(result.Constants.Select(constant => constant.Name).ToArray())
        .DoesNotContain("SDL_WINAPI_FAMILY_PHONE");
    await AssertSkipped(result, "SDL_WINAPI_FAMILY_PHONE", "platform control macro");
}
```

Run with Docker and the exact test filter. Expected: FAIL because the macro is currently emitted.

- [ ] **Step 3: Add the focused RED policy test**

Append this test to `MacroApiPolicyTests`:

```csharp
[Test]
public async Task Classify_Should_Treat_SDL_WINAPI_FAMILY_PHONE_As_Platform_Control_Macro()
{
    var decision = MacroApiPolicy.Classify(Candidate("SDL_WINAPI_FAMILY_PHONE", "2", sourceHeader: "SDL_platform.h"));

    await Assert.That(decision.Disposition).IsEqualTo(MacroApiDisposition.NonApi);
    await Assert.That(decision.Reason).IsEqualTo("platform control macro");
}
```

Use the existing `Candidate` helper in that test file; if it does not accept `sourceHeader`, extend it with a defaulted parameter.

Run the focused policy test. Expected: FAIL.

- [ ] **Step 4: Implement the macro skip**

Modify `MacroApiPolicy.ClassifyNonApiSdlMacro`:

```csharp
"SDL_WINAPI_FAMILY_PHONE" => NonApi("platform control macro"),
```

- [ ] **Step 5: Verify GREEN**

Run the focused policy test and Docker fixture test. Expected: PASS.

- [ ] **Step 6: Commit checkpoint**

Proposed commit message after approval:

```text
fix: skip SDL platform-control macro leak
```

---

## Task 6: SDL2 `SDL_bool` Int-Backed Enum

**Files:**
- Modify: `build\_build.Tests\Unit\Targets\GenerateBindings\Translation\SemanticHeaderFixtureTests.cs`
- Modify: `build\_build.Tests\Unit\Targets\GenerateBindings\Translation\BindingTranslationCollaboratorTests.cs`
- Modify: `build\_build\Targets\GenerateBindings\Translation\BindingEnumTranslator.cs`

- [ ] **Step 1: Add the RED fixture-backed enum backing test**

Append this test to `SemanticHeaderFixtureTests`:

```csharp
[Test]
public async Task Fixtures_Should_Translate_SDL2_Bool_As_Int_Backed_Enum()
{
    var model = TranslateFixture("GenerateBindings/SemanticTypes/enums-and-platform-control-macros.h");

    var sdlBool = model.Enums.Single(enumeration => enumeration.Name == "SDL_bool");

    await Assert.That(sdlBool.UnderlyingType.ManagedName).IsEqualTo("int");
    await Assert.That(sdlBool.Members.Select(member => member.Name).ToArray())
        .IsEquivalentTo(["SDL_FALSE", "SDL_TRUE"]);
}
```

Run with Docker and the exact test filter. Expected: FAIL if CppAst exposes the enum as unsigned and the translator preserves `uint`.

- [ ] **Step 2: Add the focused RED enum translator test**

Append this test to `BindingEnumTranslatorTests`:

```csharp
[Test]
public async Task Extract_Should_Force_SDL2_Bool_To_Int_Underlying_Type()
{
    var translator = CreateTranslator();
    var sdlBool = new CppEnum("SDL_bool")
    {
        IntegerType = CppPrimitiveType.UnsignedInt,
        Span = SdlHeaderSpan("SDL_stdinc.h"),
    };
    sdlBool.Items.Add(new CppEnumItem("SDL_FALSE", 0));
    sdlBool.Items.Add(new CppEnumItem("SDL_TRUE", 1));

    var enumeration = translator.Extract([sdlBool]).Single();

    await Assert.That(enumeration.UnderlyingType.ManagedName).IsEqualTo("int");
}
```

Run the focused test. Expected: FAIL.

- [ ] **Step 3: Implement SDL_bool backing override**

Modify `BindingEnumTranslator.Translate`:

```csharp
var underlyingType = string.Equals(enumeration.Name, "SDL_bool", StringComparison.Ordinal)
    ? NativeTypeRef.Primitive("int", "int", NativeAbiShape.Of("int", 4), sourceHeader)
    : _typeClassifier.Classify(enumeration.IntegerType ?? CppPrimitiveType.Int, sourceHeader);
```

- [ ] **Step 4: Verify GREEN**

Run the focused enum test and Docker fixture test. Expected: PASS.

- [ ] **Step 5: Commit checkpoint**

Proposed commit message after approval:

```text
fix: force SDL2 bool enum to int backing
```

---

## Task 7: Known SDL Bitmask Enum Flags

**Files:**
- Modify: `build\_build.Tests\Unit\Targets\GenerateBindings\Translation\SemanticHeaderFixtureTests.cs`
- Modify: `build\_build.Tests\Unit\Targets\GenerateBindings\Translation\BindingTranslationCollaboratorTests.cs`
- Modify: `build\_build\Targets\GenerateBindings\Translation\BindingEnumTranslator.cs`

- [ ] **Step 1: Add the RED fixture-backed flags test**

Append this test to `SemanticHeaderFixtureTests`:

```csharp
[Test]
public async Task Fixtures_Should_Translate_Known_Bitmask_Enums_As_Flags()
{
    var model = TranslateFixture("GenerateBindings/SemanticTypes/enums-and-platform-control-macros.h");

    await Assert.That(model.Enums.Single(enumeration => enumeration.Name == "SDL_Keymod").IsFlags).IsTrue();
    await Assert.That(model.Enums.Single(enumeration => enumeration.Name == "SDL_GLcontextFlag").IsFlags).IsTrue();
    await Assert.That(model.Enums.Single(enumeration => enumeration.Name == "SDL_RendererFlip").IsFlags).IsTrue();
}
```

Run with Docker and the exact test filter. Expected: FAIL for enum names that do not end with `Flags`.

- [ ] **Step 2: Add the focused RED enum translator test**

Append this test to `BindingEnumTranslatorTests`:

```csharp
[Test]
public async Task Extract_Should_Mark_Known_SDL_Bitmask_Enums_As_Flags()
{
    var translator = CreateTranslator();
    var keymod = SdlEnum("SDL_Keymod", "SDL_keycode.h");
    var glContextFlag = SdlEnum("SDL_GLcontextFlag", "SDL_video.h");
    var rendererFlip = SdlEnum("SDL_RendererFlip", "SDL_render.h");

    var enumerations = translator.Extract([keymod, glContextFlag, rendererFlip]);

    await Assert.That(enumerations.Single(enumeration => enumeration.Name == "SDL_Keymod").IsFlags).IsTrue();
    await Assert.That(enumerations.Single(enumeration => enumeration.Name == "SDL_GLcontextFlag").IsFlags).IsTrue();
    await Assert.That(enumerations.Single(enumeration => enumeration.Name == "SDL_RendererFlip").IsFlags).IsTrue();
}
```

Run the focused test. Expected: FAIL.

- [ ] **Step 3: Implement known flags policy**

Modify `BindingEnumTranslator`:

```csharp
private static readonly HashSet<string> KnownFlagsEnumNames = new(StringComparer.Ordinal)
{
    "SDL_Keymod",
    "SDL_GLcontextFlag",
    "SDL_RendererFlip",
};

private static bool IsFlagsEnum(CppEnum enumeration) =>
    enumeration.Name.EndsWith("Flags", StringComparison.Ordinal)
    || KnownFlagsEnumNames.Contains(enumeration.Name);
```

- [ ] **Step 4: Verify GREEN**

Run the focused enum test and Docker fixture test. Expected: PASS.

- [ ] **Step 5: Commit checkpoint**

Proposed commit message after approval:

```text
fix: mark known SDL bitmask enums as flags
```

---

## Task 8: Generated Preview, Compile Gate, Docs, And Slopwatch

**Files:**
- Modify: `docs\binding-autogen\binding-translation-contract.md`
- Modify: `docs\plan.md`
- Generated/probe output: `artifacts\generated-bindings-preview\sdl2-core\**`

- [ ] **Step 1: Run focused tests sequentially**

Run Docker semantic fixture tests:

```pwsh
$repo = (Get-Location).Path; docker run --rm --entrypoint dotnet -v "${repo}:/workspace" -w /workspace "janset-binding-generator:focal-latest" test --project "/workspace/build/_build.Tests/Build.Tests.csproj" -c Release --framework net10.0 -- --treenode-filter "/*/*/SemanticHeaderFixtureTests/*"
```

Run focused host tests:

```pwsh
dotnet test --project build\_build.Tests\Build.Tests.csproj -c Release --framework net10.0 -- --treenode-filter "/*/*/TypeMappingPolicyTests/*"
dotnet test --project build\_build.Tests\Build.Tests.csproj -c Release --framework net10.0 -- --treenode-filter "/*/*/NativeTypeClassifierTests/*"
dotnet test --project build\_build.Tests\Build.Tests.csproj -c Release --framework net10.0 -- --treenode-filter "/*/*/BindingHandleTranslatorTests/*"
dotnet test --project build\_build.Tests\Build.Tests.csproj -c Release --framework net10.0 -- --treenode-filter "/*/*/BindingEnumTranslatorTests/*"
dotnet test --project build\_build.Tests\Build.Tests.csproj -c Release --framework net10.0 -- --treenode-filter "/*/*/MacroApiPolicyTests/*"
dotnet test --project build\_build.Tests\Build.Tests.csproj -c Release --framework net10.0 -- --treenode-filter "/*/*/CsCommandEmitterTests/*"
```

Expected: all focused tests PASS.

- [ ] **Step 2: Run the full managed build-host suite**

If prior parallel runs caused compiler server locks, run this first:

```pwsh
dotnet build-server shutdown
```

Then run:

```pwsh
dotnet test --project build\_build.Tests\Build.Tests.csproj -c Release --framework net10.0
```

Expected: PASS, with existing skips only.

- [ ] **Step 3: Regenerate SDL2.Core preview**

Run:

```pwsh
dotnet run --file tools.cs -- generate-bindings --family=sdl2-core
```

Expected: succeeds. Known accepted dynapi warnings may remain for `SDL_LogMessageV`, `SDL_RWFromFP`, `SDL_vasprintf`, `SDL_vsnprintf`, and `SDL_vsscanf`.

- [ ] **Step 4: Inspect generated P0 deltas**

Verify the generated preview no longer contains these false public declarations:

```pwsh
rg "SDL_hid_device_|SDL_semaphore|SDL_WINAPI_FAMILY_PHONE|int\* serial_number|char\* serial_number|nint SDL_lround|nuint SDL_strtoul|public enum SDL_bool : uint|public partial struct SDL_RWops_hidden" artifacts\generated-bindings-preview\sdl2-core
```

Expected: no matches for the false shapes.

Verify the intended replacements exist:

```pwsh
rg "public readonly partial struct SDL_hid_device|public readonly partial struct SDL_sem|internal static extern CLong SDL_lround|internal static extern CULong SDL_strtoul|public enum SDL_bool : int|\[Flags\]\s+public enum SDL_Keymod|\[Flags\]\s+public enum SDL_GLcontextFlag|\[Flags\]\s+public enum SDL_RendererFlip" artifacts\generated-bindings-preview\sdl2-core
```

Expected: matches for the fixed/quarantined shapes that are applicable after generation.

- [ ] **Step 5: Run generated compile-check**

Run:

```pwsh
dotnet build tests\binding-compile-check\SDL2.Core.CompileCheck.csproj -c Release
```

Expected: 0 errors. CLong/CULong imports are guarded out for TFMs older than .NET 6.

- [ ] **Step 6: Update docs**

Update `docs\binding-autogen\binding-translation-contract.md` current P0 blocker section:

```markdown
The 2026-05-19 P0 translation blockers were addressed by fixture-backed generator tests. `SDL_RWops` remains Stage 1 opaque rather than a public full-layout struct; full typed layout requires a later platform-specific layout proof. C `long` / `unsigned long` raw imports are emitted only for TFMs with BCL `CLong` / `CULong`; downlevel TFMs intentionally do not compile false signatures.
```

Update `docs\plan.md` Phase 4 status with one short bullet:

```markdown
- SDL2.Core P0 translation blockers now have fixture-backed tests: opaque handle aliases, platform-sensitive C integers, `wchar_t*`, `SDL_RWops` quarantine, platform-control macros, SDL2 `SDL_bool`, and known bitmask enums.
```

- [ ] **Step 7: Run Slopwatch and whitespace checks**

Run:

```pwsh
slopwatch analyze --fail-on warning --exclude "artifacts/**,external/**,vcpkg_installed/**,**/bin/**,**/obj/**"
git --no-pager diff --check
```

Expected: no warnings, no whitespace errors.

- [ ] **Step 8: Final review checkpoint**

Before asking for commit approval, summarize:

```text
Changed:
- Added P0 fixture headers and translation tests.
- Canonicalized opaque handle typedefs.
- Replaced C long nint/nuint mapping with CLong/CULong .NET 6+ guarded emission.
- Mapped wchar_t* to opaque native pointer storage.
- Quarantined SDL_RWops as a Stage 1 opaque handle.
- Skipped SDL_WINAPI_FAMILY_PHONE.
- Forced SDL2 SDL_bool to int and marked known bitmask enums as [Flags].
- Updated translation contract and roadmap status.

Verification:
- Docker SemanticHeaderFixtureTests: PASS
- Focused translator/emitter tests: PASS
- Full build-host suite: PASS
- generate-bindings sdl2-core: PASS
- binding compile-check: PASS
- Slopwatch: PASS
- git diff --check: PASS
```

Proposed commit message after approval:

```text
fix: harden SDL2.Core P0 binding translations
```

---

## Self-Review

Spec coverage:

- Duplicate opaque handles are covered in Task 1.
- C `long` / `unsigned long` are covered in Task 2.
- `wchar_t*` is covered in Task 3.
- `SDL_RWops_hidden` false layout is covered by Task 4 through explicit Stage 1 quarantine.
- `SDL_WINAPI_FAMILY_PHONE` is covered in Task 5.
- SDL2 `SDL_bool` is covered in Task 6.
- Known `[Flags]` enum gaps are covered in Task 7.
- Verification, docs, compile-check, generated preview inspection, and Slopwatch are covered in Task 8.

Placeholder scan:

- No task uses unresolved placeholder language.
- Each production behavior change has a fixture-backed RED test, a focused test where useful, a minimal implementation step, and a GREEN verification step.

Type consistency:

- C `long` / `unsigned long` use `CLong` / `CULong` consistently across `TypeMappingPolicy`, `NativeTypeClassifier`, fixture tests, and command emission.
- `wchar_t*` uses `nint` as opaque pointer storage consistently in structs and function parameters.
- `SDL_RWops` is consistently treated as an opaque handle for Stage 1, not as a public full-layout struct.
