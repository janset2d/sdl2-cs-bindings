# Semantic Binding Pipeline Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the binding generator's string-first type flow with a semantic binding pipeline that can support SDL2.Core generated replacement without another foundational rewrite.

**Architecture:** `build\manifest.json` remains declarative family configuration plus explicit exceptions. CppAst translation produces a semantic `BindingModel` carrying `NativeTypeRef` values; validators and emitters consume that model and never infer semantics from rendered C# strings. The rewrite is one architecture pass with checkpointed tasks and real `.h` fixture coverage.

**Tech Stack:** .NET 10, C# 14, CppAst, Cake Frosting, TUnit, Microsoft.Testing.Platform, embedded test fixtures, `tools.cs generate-bindings`.

---

## File Structure

### Create

- `build\_build\Targets\GenerateBindings\Model\NativeTypeKind.cs` — semantic type category enum.
- `build\_build\Targets\GenerateBindings\Model\NativeAbiShape.cs` — ABI-width/storage metadata for primitive, pointer, handle, and bool-like shapes.
- `build\_build\Targets\GenerateBindings\Model\NativeTypeDiagnostic.cs` — source-attached type diagnostics.
- `build\_build\Targets\GenerateBindings\Model\NativeTypeRef.cs` — canonical semantic type reference.
- `build\_build\Targets\GenerateBindings\Translation\NativeTypeClassifier.cs` — CppAst type to `NativeTypeRef` classifier.
- `build\_build\Targets\GenerateBindings\Translation\NativeTypeClassificationContext.cs` — immutable classifier context from family config and ownership policy.
- `build\_build\Targets\GenerateBindings\Translation\NativeDeclarationCatalog.cs` — collected declarations from parse results.
- `build\_build\Targets\GenerateBindings\Translation\NativeDeclarationCatalogBuilder.cs` — CppAst declaration catalog builder.
- `build\_build\Targets\GenerateBindings\Translation\BindingEnumTranslator.cs` — enum/flags translation from catalog.
- `build\_build\Targets\GenerateBindings\Translation\BindingHandleTranslator.cs` — opaque-handle translation from catalog.
- `build\_build\Targets\GenerateBindings\Translation\BindingCallbackTranslator.cs` — callback/function-pointer typedef translation.
- `build\_build\Targets\GenerateBindings\Emitting\CsHandleEmitter.cs` — typed handle output.
- `build\_build\Targets\GenerateBindings\Emitting\CsCallbackEmitter.cs` — callback output.
- `build\_build.Tests\Unit\Targets\GenerateBindings\Model\NativeTypeModelTests.cs` — semantic model record tests.
- `build\_build.Tests\Unit\Targets\GenerateBindings\Translation\NativeTypeClassifierTests.cs` — pure classifier tests.
- `build\_build.Tests\Unit\Targets\GenerateBindings\Translation\SemanticHeaderFixtureTests.cs` — embedded `.h` translator fixture tests.
- `build\_build.Tests\Unit\Targets\GenerateBindings\Emitting\SemanticEmitterTests.cs` — emitter tests using semantic model input.
- `build\_build.Tests\Fixtures\Data\GenerateBindings\SemanticTypes\opaque-and-concrete-structs.h`
- `build\_build.Tests\Fixtures\Data\GenerateBindings\SemanticTypes\typedefs-bool-and-strings.h`
- `build\_build.Tests\Fixtures\Data\GenerateBindings\SemanticTypes\callbacks-arrays-and-unions.h`
- `build\_build.Tests\Fixtures\Data\GenerateBindings\SemanticTypes\enums-and-flags.h`

### Modify

- `build\_build\Targets\GenerateBindings\Model\BindingTypeRef.cs` — retire string-first semantics or convert to a rendering projection after `NativeTypeRef` is introduced.
- `build\_build\Targets\GenerateBindings\Model\BindingFunction.cs`
- `build\_build\Targets\GenerateBindings\Model\BindingParameter.cs`
- `build\_build\Targets\GenerateBindings\Model\BindingStruct.cs`
- `build\_build\Targets\GenerateBindings\Model\BindingEnumeration.cs`
- `build\_build\Targets\GenerateBindings\Model\BindingConstant.cs`
- `build\_build\Targets\GenerateBindings\Model\BindingHandle.cs`
- `build\_build\Targets\GenerateBindings\Model\BindingCallback.cs`
- `build\_build\Targets\GenerateBindings\Model\BindingModel.cs`
- `build\_build\Targets\GenerateBindings\Translation\TypeMappingPolicy.cs` — remove responsibility for semantic classification after replacement.
- `build\_build\Targets\GenerateBindings\Translation\ExternalNativeTypePolicy.cs`
- `build\_build\Targets\GenerateBindings\Translation\SdlNativeTypeSubstitutionPolicy.cs`
- `build\_build\Targets\GenerateBindings\Translation\KnownUnsupportedDeclarationPolicy.cs`
- `build\_build\Targets\GenerateBindings\Translation\BindingFunctionTranslator.cs`
- `build\_build\Targets\GenerateBindings\Translation\StructFieldTranslator.cs`
- `build\_build\Targets\GenerateBindings\Translation\BindingStructTranslator.cs`
- `build\_build\Targets\GenerateBindings\Translation\RequiredConstantTranslator.cs`
- `build\_build\Targets\GenerateBindings\Translation\CppAstToBindingModel.cs`
- `build\_build\Targets\GenerateBindings\Emitting\CsCommandEmitter.cs`
- `build\_build\Targets\GenerateBindings\Emitting\CsStructEmitter.cs`
- `build\_build\Targets\GenerateBindings\Emitting\CsEnumEmitter.cs`
- `build\_build\Targets\GenerateBindings\Emitting\CsConstantEmitter.cs`
- `build\_build\Validation\BindingGeneration\DynapiCoherenceValidator.cs`
- `build\_build.Tests\Fixtures\BindingGenerationFixture.cs`
- Existing GenerateBindings tests under `build\_build.Tests\Unit\Targets\GenerateBindings\*` and `build\_build.Tests\Scenarios\GenerateBindings\*`.
- `docs\superpowers\specs\2026-05-18-semantic-binding-pipeline-design.md` if implementation reveals a durable design clarification.

---

## Task 1: Semantic Type Model Records

**Files:**
- Create: `build\_build\Targets\GenerateBindings\Model\NativeTypeKind.cs`
- Create: `build\_build\Targets\GenerateBindings\Model\NativeAbiShape.cs`
- Create: `build\_build\Targets\GenerateBindings\Model\NativeTypeDiagnostic.cs`
- Create: `build\_build\Targets\GenerateBindings\Model\NativeTypeRef.cs`
- Create: `build\_build.Tests\Unit\Targets\GenerateBindings\Model\NativeTypeModelTests.cs`

- [ ] **Step 1: Write failing semantic model tests**

Add `NativeTypeModelTests.cs`:

```csharp
using Build.Targets.GenerateBindings.Model;

namespace Build.Tests.Unit.Targets.GenerateBindings.Model;

public sealed class NativeTypeModelTests
{
    [Test]
    public async Task OpaqueHandle_Should_Carry_Native_And_Managed_Identity_Separately()
    {
        var sut = NativeTypeRef.OpaqueHandle(
            nativeName: "SDL_Window",
            managedName: "SDL_Window",
            owningFamilyId: "sdl2-core",
            sourceHeader: "SDL_video.h");

        await Assert.That(sut.NativeName).IsEqualTo("SDL_Window");
        await Assert.That(sut.ManagedName).IsEqualTo("SDL_Window");
        await Assert.That(sut.Kind).IsEqualTo(NativeTypeKind.OpaqueHandle);
        await Assert.That(sut.PointerDepth).IsEqualTo(0);
        await Assert.That(sut.AbiShape.StorageName).IsEqualTo("nint");
        await Assert.That(sut.OwningFamilyId).IsEqualTo("sdl2-core");
    }

    [Test]
    public async Task Pointer_To_Concrete_Struct_Should_Not_Be_Classified_As_Handle()
    {
        var element = NativeTypeRef.ConcreteStruct("SDL_Texture", "SDL_Texture", "sdl2-core", "SDL_render.h");
        var sut = NativeTypeRef.Indirection(element, indirectionDepth: 1, managedName: "SDL_Texture*");

        await Assert.That(sut.Kind).IsEqualTo(NativeTypeKind.TypedPointer);
        await Assert.That(sut.ElementType).IsEqualTo(element);
        await Assert.That(sut.ManagedName).IsEqualTo("SDL_Texture*");
    }
}
```

- [ ] **Step 2: Run tests and verify RED**

Run:

```pwsh
dotnet test --project build\_build.Tests\Build.Tests.csproj -c Release --framework net10.0 -- --treenode-filter "/*/*/NativeTypeModelTests/*"
```

Expected: build fails because `NativeTypeRef`, `NativeTypeKind`, and `NativeAbiShape` do not exist.

- [ ] **Step 3: Add model records**

Create `NativeTypeKind.cs`:

```csharp
namespace Build.Targets.GenerateBindings.Model;

internal enum NativeTypeKind
{
    Primitive,
    Enum,
    FlagsEnum,
    ValueTypedef,
    ConcreteStruct,
    Union,
    OpaqueHandle,
    VoidPointer,
    Utf8Pointer,
    TypedPointer,
    FunctionPointer,
    Callback,
    Array,
    ExternalOpaque,
    SubstitutedManagedType,
    Deferred,
    Unsupported,
}
```

Create `NativeAbiShape.cs`:

```csharp
namespace Build.Targets.GenerateBindings.Model;

internal sealed record NativeAbiShape(string StorageName, int? SizeBytes, bool IsBlittable)
{
    public static NativeAbiShape Of(string storageName, int? sizeBytes = null, bool isBlittable = true) =>
        new(storageName, sizeBytes, isBlittable);
}
```

Create `NativeTypeDiagnostic.cs`:

```csharp
namespace Build.Targets.GenerateBindings.Model;

internal sealed record NativeTypeDiagnostic(
    string Severity,
    string Message,
    string? SourceHeader,
    string? NativeName);
```

Create `NativeTypeRef.cs`:

```csharp
namespace Build.Targets.GenerateBindings.Model;

internal sealed record NativeTypeRef(
    string NativeName,
    string ManagedName,
    NativeTypeKind Kind,
    int PointerDepth,
    string? OwningFamilyId,
    string? SourceHeader,
    NativeAbiShape AbiShape,
    NativeTypeRef? ElementType,
    IReadOnlyList<NativeTypeDiagnostic> Diagnostics)
{
    public static NativeTypeRef Primitive(string nativeName, string managedName, NativeAbiShape abiShape, string? sourceHeader = null) =>
        new(nativeName, managedName, NativeTypeKind.Primitive, 0, null, sourceHeader, abiShape, null, []);

    public static NativeTypeRef ConcreteStruct(string nativeName, string managedName, string? owningFamilyId, string? sourceHeader) =>
        new(nativeName, managedName, NativeTypeKind.ConcreteStruct, 0, owningFamilyId, sourceHeader, NativeAbiShape.Of(managedName), null, []);

    public static NativeTypeRef OpaqueHandle(string nativeName, string managedName, string? owningFamilyId, string? sourceHeader) =>
        new(nativeName, managedName, NativeTypeKind.OpaqueHandle, 0, owningFamilyId, sourceHeader, NativeAbiShape.Of("nint", IntPtr.Size), null, []);

    public static NativeTypeRef Pointer(NativeTypeRef elementType, int pointerDepth, string managedName) =>
        new(elementType.NativeName, managedName, NativeTypeKind.TypedPointer, pointerDepth, elementType.OwningFamilyId, elementType.SourceHeader, NativeAbiShape.Of("nint", IntPtr.Size), elementType, elementType.Diagnostics);
}
```

- [ ] **Step 4: Run tests and verify GREEN**

Run:

```pwsh
dotnet test --project build\_build.Tests\Build.Tests.csproj -c Release --framework net10.0 -- --treenode-filter "/*/*/NativeTypeModelTests/*"
```

Expected: `NativeTypeModelTests` pass.

- [ ] **Step 5: Checkpoint commit after approval**

Present summary and proposed commit message:

```text
feat(binding-autogen): add semantic native type model

Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>
```

After approval:

```pwsh
git add build\_build\Targets\GenerateBindings\Model build\_build.Tests\Unit\Targets\GenerateBindings\Model\NativeTypeModelTests.cs
git commit -m "feat(binding-autogen): add semantic native type model" -m "Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>"
```

---

## Task 2: Native Type Classifier

**Files:**
- Create: `build\_build\Targets\GenerateBindings\Translation\NativeTypeClassificationContext.cs`
- Create: `build\_build\Targets\GenerateBindings\Translation\NativeTypeClassifier.cs`
- Create: `build\_build.Tests\Unit\Targets\GenerateBindings\Translation\NativeTypeClassifierTests.cs`
- Modify: `build\_build\Targets\GenerateBindings\Translation\TypeMappingPolicy.cs`
- Modify: `build\_build\Targets\GenerateBindings\Translation\ExternalNativeTypePolicy.cs`
- Modify: `build\_build\Targets\GenerateBindings\Translation\SdlNativeTypeSubstitutionPolicy.cs`

- [ ] **Step 1: Write failing classifier tests**

Create `NativeTypeClassifierTests.cs`:

```csharp
using Build.Targets.GenerateBindings.Model;
using Build.Targets.GenerateBindings.Translation;
using Build.Tests.Fixtures;
using CppAst;

namespace Build.Tests.Unit.Targets.GenerateBindings.Translation;

public sealed class NativeTypeClassifierTests
{
    [Test]
    public async Task Classify_Should_Distinguish_Opaque_Handle_From_Concrete_Struct()
    {
        var classifier = CreateClassifier();
        var opaqueWindow = new CppClass("SDL_Window")
        {
            ClassKind = CppClassKind.Struct,
            IsDefinition = false,
        };
        var texture = new CppClass("SDL_Texture")
        {
            ClassKind = CppClassKind.Struct,
            IsDefinition = true,
            SizeOf = 16,
        };
        texture.Fields.Add(new CppField(CppPrimitiveType.UnsignedInt, "format"));

        var window = classifier.Classify(opaqueWindow, sourceHeader: "SDL_video.h");
        var concrete = classifier.Classify(texture, sourceHeader: "SDL_render.h");

        await Assert.That(window.Kind).IsEqualTo(NativeTypeKind.OpaqueHandle);
        await Assert.That(window.ManagedName).IsEqualTo("SDL_Window");
        await Assert.That(concrete.Kind).IsEqualTo(NativeTypeKind.ConcreteStruct);
        await Assert.That(concrete.ManagedName).IsEqualTo("SDL_Texture");
    }

    [Test]
    public async Task Classify_Should_Map_SDL2_Bool_To_Int_Backed_Primitive()
    {
        var classifier = CreateClassifier();
        var type = new CppTypedef("SDL_bool", new CppEnum("SDL_bool"));

        var sut = classifier.Classify(type, sourceHeader: "SDL_stdinc.h");

        await Assert.That(sut.Kind).IsEqualTo(NativeTypeKind.ValueTypedef);
        await Assert.That(sut.ManagedName).IsEqualTo("int");
        await Assert.That(sut.AbiShape.StorageName).IsEqualTo("int");
    }

    [Test]
    public async Task Classify_Should_Classify_Const_Char_Pointer_As_Utf8Pointer()
    {
        var classifier = CreateClassifier();
        var pointer = new CppPointerType(new CppQualifiedType(CppPrimitiveType.Char, CppTypeQualifier.Const));

        var sut = classifier.Classify(pointer, sourceHeader: "SDL_video.h");

        await Assert.That(sut.Kind).IsEqualTo(NativeTypeKind.Utf8Pointer);
        await Assert.That(sut.ManagedName).IsEqualTo("byte*");
        await Assert.That(sut.PointerDepth).IsEqualTo(1);
    }

    private static NativeTypeClassifier CreateClassifier()
    {
        var config = BindingGenerationFixture.Sdl2CoreConfig();
        var context = NativeTypeClassificationContext.FromConfig(config);
        return new NativeTypeClassifier(context);
    }
}
```

- [ ] **Step 2: Run tests and verify RED**

Run:

```pwsh
dotnet test --project build\_build.Tests\Build.Tests.csproj -c Release --framework net10.0 -- --treenode-filter "/*/*/NativeTypeClassifierTests/*"
```

Expected: build fails because classifier types do not exist.

- [ ] **Step 3: Implement classification context**

Create `NativeTypeClassificationContext.cs`:

```csharp
using Build.Data.BindingGeneration.Models;

namespace Build.Targets.GenerateBindings.Translation;

internal sealed record NativeTypeClassificationContext(
    string FamilyId,
    IReadOnlySet<string> OwnedPrefixes,
    IReadOnlySet<string> DeferredDeclarations)
{
    public static NativeTypeClassificationContext FromConfig(BindingGenerationConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        return new NativeTypeClassificationContext(
            config.FamilyId,
            config.OwnedPrefixes.ToHashSet(StringComparer.Ordinal),
            config.DeferredDeclarations.Keys.ToHashSet(StringComparer.Ordinal));
    }

    public bool IsOwned(string nativeName) =>
        OwnedPrefixes.Any(prefix => nativeName.StartsWith(prefix, StringComparison.Ordinal));
}
```

- [ ] **Step 4: Implement minimal classifier**

Create `NativeTypeClassifier.cs`:

```csharp
using Build.Targets.GenerateBindings.Model;
using CppAst;

namespace Build.Targets.GenerateBindings.Translation;

internal sealed class NativeTypeClassifier(NativeTypeClassificationContext context)
{
    private readonly NativeTypeClassificationContext _context = context ?? throw new ArgumentNullException(nameof(context));

    public NativeTypeRef Classify(CppType type, string? sourceHeader) =>
        Classify(UnwrapQualified(type), sourceHeader, depth: 0);

    private NativeTypeRef Classify(CppType type, string? sourceHeader, int depth)
    {
        if (depth > TypeMappingPolicy.MaxTypedefDepth)
        {
            throw new InvalidOperationException($"Native type classification exceeded typedef depth {TypeMappingPolicy.MaxTypedefDepth} for '{type}'.");
        }

        return type switch
        {
            CppPrimitiveType primitive => ClassifyPrimitive(primitive, sourceHeader),
            CppTypedef typedef => ClassifyTypedef(typedef, sourceHeader, depth),
            CppPointerType pointer => ClassifyPointer(pointer, sourceHeader, depth),
            CppArrayType array => ClassifyArray(array, sourceHeader, depth),
            CppEnum enumeration => NativeTypeRef.Primitive(enumeration.Name, "int", NativeAbiShape.Of("int", 4), sourceHeader) with { Kind = NativeTypeKind.Enum },
            CppClass cls => ClassifyClass(cls, sourceHeader),
            _ => Unsupported(type.ToString(), sourceHeader, $"Unsupported CppAst type '{type.GetType().Name}'."),
        };
    }

    private NativeTypeRef ClassifyPrimitive(CppPrimitiveType primitive, string? sourceHeader)
    {
        var managedName = primitive.Kind switch
        {
            CppPrimitiveKind.Void => "void",
            CppPrimitiveKind.Bool => "byte",
            CppPrimitiveKind.Char => "sbyte",
            CppPrimitiveKind.UnsignedChar => "byte",
            CppPrimitiveKind.Short => "short",
            CppPrimitiveKind.UnsignedShort => "ushort",
            CppPrimitiveKind.Int => "int",
            CppPrimitiveKind.UnsignedInt => "uint",
            CppPrimitiveKind.Long => "nint",
            CppPrimitiveKind.UnsignedLong => "nuint",
            CppPrimitiveKind.LongLong => "long",
            CppPrimitiveKind.UnsignedLongLong => "ulong",
            CppPrimitiveKind.Float => "float",
            CppPrimitiveKind.Double => "double",
            _ => "IntPtr",
        };
        return NativeTypeRef.Primitive(primitive.KindSpelling, managedName, NativeAbiShape.Of(managedName), sourceHeader);
    }

    private NativeTypeRef ClassifyTypedef(CppTypedef typedef, string? sourceHeader, int depth)
    {
        if (string.Equals(typedef.Name, "SDL_bool", StringComparison.Ordinal))
        {
            return new NativeTypeRef(typedef.Name, "int", NativeTypeKind.ValueTypedef, 0, _context.FamilyId, sourceHeader, NativeAbiShape.Of("int", 4), null, []);
        }

        if (SdlNativeTypeSubstitutionPolicy.TryMapName(typedef.Name, out var substituted))
        {
            return new NativeTypeRef(typedef.Name, substituted, NativeTypeKind.SubstitutedManagedType, 0, _context.FamilyId, sourceHeader, NativeAbiShape.Of(substituted), null, []);
        }

        var element = Classify(typedef.ElementType, sourceHeader, depth + 1);
        return element with
        {
            NativeName = typedef.Name,
            Kind = element.Kind is NativeTypeKind.Primitive or NativeTypeKind.Enum ? NativeTypeKind.ValueTypedef : element.Kind,
            OwningFamilyId = _context.IsOwned(typedef.Name) ? _context.FamilyId : element.OwningFamilyId,
        };
    }

    private NativeTypeRef ClassifyPointer(CppPointerType pointer, string? sourceHeader, int depth)
    {
        var element = UnwrapQualified(pointer.ElementType);
        if (element is CppPrimitiveType { Kind: CppPrimitiveKind.Void })
        {
            return new NativeTypeRef("void*", "nint", NativeTypeKind.VoidPointer, 1, null, sourceHeader, NativeAbiShape.Of("nint", IntPtr.Size), null, []);
        }

        if (element is CppPrimitiveType { Kind: CppPrimitiveKind.Char or CppPrimitiveKind.UnsignedChar })
        {
            return new NativeTypeRef("char*", "byte*", NativeTypeKind.Utf8Pointer, 1, null, sourceHeader, NativeAbiShape.Of("nint", IntPtr.Size), null, []);
        }

        var elementType = Classify(element, sourceHeader, depth + 1);
        var managedName = elementType.Kind == NativeTypeKind.OpaqueHandle
            ? elementType.ManagedName
            : elementType.ManagedName + "*";
        return NativeTypeRef.Indirection(elementType, indirectionDepth: 1, managedName);
    }

    private NativeTypeRef ClassifyArray(CppArrayType array, string? sourceHeader, int depth)
    {
        var element = Classify(array.ElementType, sourceHeader, depth + 1);
        return new NativeTypeRef(element.NativeName, element.ManagedName, NativeTypeKind.Array, 0, element.OwningFamilyId, sourceHeader, element.AbiShape, element, element.Diagnostics);
    }

    private NativeTypeRef ClassifyClass(CppClass cls, string? sourceHeader)
    {
        if (_context.DeferredDeclarations.Contains(cls.Name))
        {
            return new NativeTypeRef(cls.Name, "nint", NativeTypeKind.Deferred, 0, _context.FamilyId, sourceHeader, NativeAbiShape.Of("nint", IntPtr.Size), null, []);
        }

        if (SdlNativeTypeSubstitutionPolicy.TryMapName(cls.Name, out var substituted))
        {
            return new NativeTypeRef(cls.Name, substituted, NativeTypeKind.SubstitutedManagedType, 0, _context.FamilyId, sourceHeader, NativeAbiShape.Of(substituted), null, []);
        }

        var ownedFamily = _context.IsOwned(cls.Name) ? _context.FamilyId : null;
        if (cls.ClassKind == CppClassKind.Struct && (!cls.IsDefinition || cls.SizeOf == 0) && cls.Fields.Count == 0)
        {
            return NativeTypeRef.OpaqueHandle(cls.Name, cls.Name, ownedFamily, sourceHeader);
        }

        return NativeTypeRef.ConcreteStruct(cls.Name, cls.Name, ownedFamily, sourceHeader);
    }

    private static CppType UnwrapQualified(CppType type)
    {
        while (type is CppQualifiedType qualified)
        {
            type = qualified.ElementType;
        }

        return type;
    }

    private static NativeTypeRef Unsupported(string nativeName, string? sourceHeader, string message) =>
        new(nativeName, "nint", NativeTypeKind.Unsupported, 0, null, sourceHeader, NativeAbiShape.Of("nint", IntPtr.Size), null,
            [new NativeTypeDiagnostic("Error", message, sourceHeader, nativeName)]);
}
```

- [ ] **Step 5: Expose substitution by name**

Modify `SdlNativeTypeSubstitutionPolicy.cs` to add:

```csharp
public static bool TryMapName(string name, out string managedName) =>
    ValueTypeMappings.TryGetValue(name, out managedName!);
```

Keep the existing `TryMap(...)` methods until old code is removed.

- [ ] **Step 6: Run tests and verify GREEN**

Run:

```pwsh
dotnet test --project build\_build.Tests\Build.Tests.csproj -c Release --framework net10.0 -- --treenode-filter "/*/*/NativeTypeClassifierTests/*"
```

Expected: `NativeTypeClassifierTests` pass.

- [ ] **Step 7: Checkpoint commit after approval**

Proposed commit:

```text
feat(binding-autogen): classify native types semantically

Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>
```

---

## Task 3: Embedded Header Fixture Translator Tests

**Files:**
- Create: `build\_build.Tests\Fixtures\Data\GenerateBindings\SemanticTypes\opaque-and-concrete-structs.h`
- Create: `build\_build.Tests\Fixtures\Data\GenerateBindings\SemanticTypes\typedefs-bool-and-strings.h`
- Create: `build\_build.Tests\Fixtures\Data\GenerateBindings\SemanticTypes\callbacks-arrays-and-unions.h`
- Create: `build\_build.Tests\Fixtures\Data\GenerateBindings\SemanticTypes\enums-and-flags.h`
- Create: `build\_build.Tests\Unit\Targets\GenerateBindings\Translation\SemanticHeaderFixtureTests.cs`

- [ ] **Step 1: Add embedded header fixtures**

Create `opaque-and-concrete-structs.h`:

```c
typedef struct SDL_Window SDL_Window;

typedef struct SDL_Texture
{
    unsigned int format;
    int w;
    int h;
    int refcount;
} SDL_Texture;

typedef struct SDL_Rect
{
    int x;
    int y;
    int w;
    int h;
} SDL_Rect;
```

Create `typedefs-bool-and-strings.h`:

```c
typedef enum SDL_bool
{
    SDL_FALSE = 0,
    SDL_TRUE = 1
} SDL_bool;

typedef unsigned short Uint16;
typedef Uint16 SDL_AudioFormat;

extern const char* SDL_GetError(void);
extern int SDL_SetHint(const char* name, const char* value);
extern void SDL_ClearError(void);
```

Create `callbacks-arrays-and-unions.h`:

```c
typedef unsigned char Uint8;
typedef unsigned int Uint32;

typedef void (*SDL_AudioCallback)(void* userdata, Uint8* stream, int len);

typedef struct SDL_AudioSpec
{
    int freq;
    Uint16 format;
    Uint8 channels;
    Uint8 silence;
    Uint16 samples;
    Uint32 size;
    SDL_AudioCallback callback;
    void* userdata;
} SDL_AudioSpec;

typedef struct SDL_Event
{
    Uint32 type;
    Uint8 padding[56];
} SDL_Event;

typedef struct SDL_GameControllerButtonBind
{
    int bindType;
    union
    {
        int button;
        int axis;
    } value;
} SDL_GameControllerButtonBind;
```

Create `enums-and-flags.h`:

```c
typedef enum SDL_WindowFlags
{
    SDL_WINDOW_FULLSCREEN = 0x00000001,
    SDL_WINDOW_OPENGL = 0x00000002,
    SDL_WINDOW_SHOWN = 0x00000004,
    SDL_WINDOW_FULLSCREEN_DESKTOP = (SDL_WINDOW_FULLSCREEN | 0x00001000)
} SDL_WindowFlags;

typedef enum SDL_EventType
{
    SDL_FIRSTEVENT = 0,
    SDL_QUIT = 0x100
} SDL_EventType;
```

- [ ] **Step 2: Write failing fixture tests**

Create `SemanticHeaderFixtureTests.cs`:

```csharp
using Build.Tests.Fixtures;
using CppAst;

namespace Build.Tests.Unit.Targets.GenerateBindings.Translation;

[LinuxOnly]
public sealed class SemanticHeaderFixtureTests
{
    [Test]
    public async Task Fixtures_Should_Parse_Opaque_And_Concrete_Struct_Shapes()
    {
        var compilation = ParseFixture("GenerateBindings/SemanticTypes/opaque-and-concrete-structs.h");

        var window = compilation.Classes.Single(c => c.Name == "SDL_Window");
        var texture = compilation.Classes.Single(c => c.Name == "SDL_Texture");

        await Assert.That(window.Fields).IsEmpty();
        await Assert.That(texture.Fields.Select(f => f.Name).ToArray())
            .IsEquivalentTo(["format", "w", "h", "refcount"]);
    }

    [Test]
    public async Task Fixtures_Should_Parse_Callback_Array_And_Anonymous_Union_Shapes()
    {
        var compilation = ParseFixture("GenerateBindings/SemanticTypes/callbacks-arrays-and-unions.h");

        await Assert.That(compilation.Typedefs.Select(t => t.Name).ToArray())
            .Contains("SDL_AudioCallback");
        var sdlEvent = compilation.Classes.Single(c => c.Name == "SDL_Event");
        await Assert.That(sdlEvent.Fields.Single(f => f.Name == "padding").Type).IsTypeOf<CppArrayType>();
        var bind = compilation.Classes.Single(c => c.Name == "SDL_GameControllerButtonBind");
        await Assert.That(bind.Fields.Single(f => f.Name == "value").Type).IsTypeOf<CppClass>();
    }

    private static CppCompilation ParseFixture(string fixturePath)
    {
        var directory = Path.Combine(Path.GetTempPath(), "janset-semantic-fixtures", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var header = Path.Combine(directory, Path.GetFileName(fixturePath));
        try
        {
            File.WriteAllText(header, FixtureLoader.Load(fixturePath));

            var options = new CppParserOptions
            {
                ParserKind = CppParserKind.C,
                TargetSystem = "linux",
                ParseMacros = false,
            };

            var compilation = CppParser.ParseFile(header, options);
            if (compilation.HasErrors)
            {
                throw new InvalidOperationException(compilation.Diagnostics.ToString());
            }

            return compilation;
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
```

- [ ] **Step 3: Run tests and verify RED on missing semantic assertions or GREEN on fixture shape**

Run:

```pwsh
dotnet test --project build\_build.Tests\Build.Tests.csproj -c Release --framework net10.0 -- --treenode-filter "/*/*/SemanticHeaderFixtureTests/*"
```

Expected on Windows: tests are skipped by `[LinuxOnly]`, matching the existing `CppAstFixtureMatrixTests` pattern. Expected on Linux: fixtures parse successfully and assertions pass. If Linux parsing fails, the fixture text is malformed and must be corrected in the fixture file before continuing.

- [ ] **Step 4: Keep fixture parsing on canonical test infrastructure**

If the existing pattern uses `FixtureLoader` plus a temp header abstraction, reuse it. Do not add `File.ReadAllText`, `AppContext.BaseDirectory`, or `System.IO.Abstractions`.

- [ ] **Step 5: Run fixture tests and verify GREEN**

Run the same focused command. Expected: fixture tests pass.

---

## Task 4: Semantic Binding Records

**Files:**
- Modify: `build\_build\Targets\GenerateBindings\Model\BindingFunction.cs`
- Modify: `build\_build\Targets\GenerateBindings\Model\BindingParameter.cs`
- Modify: `build\_build\Targets\GenerateBindings\Model\BindingStruct.cs`
- Modify: `build\_build\Targets\GenerateBindings\Model\BindingEnumeration.cs`
- Modify: `build\_build\Targets\GenerateBindings\Model\BindingConstant.cs`
- Modify: `build\_build\Targets\GenerateBindings\Model\BindingHandle.cs`
- Modify: `build\_build\Targets\GenerateBindings\Model\BindingCallback.cs`
- Modify: `build\_build.Tests\Unit\Targets\GenerateBindings\Model\BindingModelRecordsTests.cs`

- [ ] **Step 1: Write failing record tests for semantic type refs**

Add to `BindingModelRecordsTests.cs`:

```csharp
[Test]
public async Task BindingParameter_Should_Carry_Semantic_Native_Type()
{
    var type = NativeTypeRef.OpaqueHandle("SDL_Window", "SDL_Window", "sdl2-core", "SDL_video.h");
    var sut = new BindingParameter(type, "window");

    await Assert.That(sut.Type.Kind).IsEqualTo(NativeTypeKind.OpaqueHandle);
    await Assert.That(sut.Type.ManagedName).IsEqualTo("SDL_Window");
    await Assert.That(sut.Name).IsEqualTo("window");
}
```

- [ ] **Step 2: Run tests and verify RED**

Run:

```pwsh
dotnet test --project build\_build.Tests\Build.Tests.csproj -c Release --framework net10.0 -- --treenode-filter "/*/*/BindingModelRecordsTests/*"
```

Expected: compile fails because `BindingParameter` still accepts `BindingTypeRef`.

- [ ] **Step 3: Replace record properties with `NativeTypeRef`**

Update these record signatures:

```csharp
public sealed record BindingParameter(NativeTypeRef Type, string Name);

public sealed record BindingFunction(
    string Name,
    NativeTypeRef ReturnType,
    IReadOnlyList<BindingParameter> Parameters,
    string SourceHeader);

public sealed record BindingStructField(
    string Name,
    NativeTypeRef Type,
    int? FieldOffset,
    int? FixedBufferLength);

public sealed record BindingEnumeration(
    string Name,
    NativeTypeRef UnderlyingType,
    bool IsFlags,
    IReadOnlyList<BindingEnumMember> Members);

public sealed record BindingConstant(string Name, NativeTypeRef Type, string Value, ConstantKind Kind);
```

For `BindingHandle` and `BindingCallback`, use semantic types:

```csharp
public sealed record BindingHandle(string Name, NativeTypeRef Type);

public sealed record BindingCallback(
    string Name,
    NativeTypeRef ReturnType,
    IReadOnlyList<BindingParameter> Parameters);
```

- [ ] **Step 4: Add temporary test fixture helpers**

Update `BindingGenerationFixture` with semantic helpers:

```csharp
public static NativeTypeRef NativePrimitive(string nativeName, string managedName) =>
    NativeTypeRef.Primitive(nativeName, managedName, NativeAbiShape.Of(managedName));

public static NativeTypeRef NativeUInt() => NativePrimitive("unsigned int", "uint");

public static NativeTypeRef NativeVoid() => NativePrimitive("void", "void");
```

- [ ] **Step 5: Fix compile errors in tests by replacing `BindingTypeRef.Of(...)`**

Example replacements:

```csharp
BindingTypeRef.Of("uint")
```

becomes:

```csharp
BindingGenerationFixture.NativeUInt()
```

For pointer cases, use:

```csharp
NativeTypeRef.Indirection(
    NativeTypeRef.ConcreteStruct("SDL_Surface", "SDL_Surface", "sdl2-core", "SDL_surface.h"),
    indirectionDepth: 1,
    managedName: "SDL_Surface*")
```

- [ ] **Step 6: Run model tests and verify GREEN**

Run:

```pwsh
dotnet test --project build\_build.Tests\Build.Tests.csproj -c Release --framework net10.0 -- --treenode-filter "/*/*/BindingModelRecordsTests/*"
```

Expected: model record tests pass.

---

## Task 5: Declaration Catalog and Translators

**Status:** Done. Implementation keeps parsed function translation view-scoped, wires `NativeDeclarationCatalog` into global struct extraction, and uses `NativeTypeClassifier` for parsed function/field type refs. Review fixes added coverage for `SDL_GLContext`, pointer-to-pointer typedefs, `SDL_Window*` / `SDL_Window**`, primitive native-name preservation, enum ownership, required-constant source headers, and anonymous nested struct source-header inheritance.

**Files:**
- Create: `build\_build\Targets\GenerateBindings\Translation\NativeDeclarationCatalog.cs`
- Create: `build\_build\Targets\GenerateBindings\Translation\NativeDeclarationCatalogBuilder.cs`
- Modify: `build\_build\Targets\GenerateBindings\Translation\BindingFunctionTranslator.cs`
- Modify: `build\_build\Targets\GenerateBindings\Translation\StructFieldTranslator.cs`
- Modify: `build\_build\Targets\GenerateBindings\Translation\BindingStructTranslator.cs`
- Modify: `build\_build\Targets\GenerateBindings\Translation\RequiredConstantTranslator.cs`
- Modify: `build\_build\Targets\GenerateBindings\Translation\CppAstToBindingModel.cs`
- Modify: `build\_build.Tests\Unit\Targets\GenerateBindings\Translation\CppAstToBindingModelTests.cs`
- Modify: `build\_build.Tests\Unit\Targets\GenerateBindings\Translation\BindingTranslationCollaboratorTests.cs`

- [ ] **Step 1: Write failing translator test for semantic output**

Add to `CppAstToBindingModelTests.cs`:

```csharp
[Test]
public async Task Translate_Should_Populate_Semantic_Type_Refs_For_Function_Parameters()
{
    var createWindow = new CppFunction("SDL_CreateWindow")
    {
        ReturnType = new CppPointerType(new CppClass("SDL_Window")
        {
            ClassKind = CppClassKind.Struct,
            IsDefinition = false,
        }),
        Span = SdlHeaderSpan("SDL_video.h"),
    };
    createWindow.Parameters.Add(new CppParameter(new CppPointerType(new CppQualifiedType(CppPrimitiveType.Char, CppTypeQualifier.Const)), "title"));

    var compilation = new CppCompilation();
    compilation.Functions.Add(createWindow);

    var model = CppAstToBindingModel.Translate(
        [ParseResult("Neutral", null, compilation)],
        DefaultConfig,
        NoRequired);

    var function = model.Views.Single().Functions.Single(f => f.Name == "SDL_CreateWindow");
    await Assert.That(function.ReturnType.Kind).IsEqualTo(NativeTypeKind.TypedPointer);
    await Assert.That(function.ReturnType.ElementType?.Kind).IsEqualTo(NativeTypeKind.OpaqueHandle);
    await Assert.That(function.Parameters.Single().Type.Kind).IsEqualTo(NativeTypeKind.Utf8Pointer);
}
```

- [ ] **Step 2: Run translator tests and verify RED**

Run:

```pwsh
dotnet test --project build\_build.Tests\Build.Tests.csproj -c Release --framework net10.0 -- --treenode-filter "/*/*/CppAstToBindingModelTests/*"
```

Expected: compile fails until translators use `NativeTypeClassifier`.

- [ ] **Step 3: Add declaration catalog**

Create `NativeDeclarationCatalog.cs`:

```csharp
using CppAst;

namespace Build.Targets.GenerateBindings.Translation;

internal sealed record NativeDeclarationCatalog(
    IReadOnlyList<CppFunction> Functions,
    IReadOnlyList<CppClass> Classes,
    IReadOnlyList<CppEnum> Enums,
    IReadOnlyList<CppTypedef> Typedefs);
```

Create `NativeDeclarationCatalogBuilder.cs`:

```csharp
using Build.Targets.GenerateBindings.Parsing;
using CppAst;

namespace Build.Targets.GenerateBindings.Translation;

internal sealed class NativeDeclarationCatalogBuilder(BindableDeclarationPolicy declarationPolicy)
{
    private readonly BindableDeclarationPolicy _declarationPolicy = declarationPolicy ?? throw new ArgumentNullException(nameof(declarationPolicy));

    public NativeDeclarationCatalog Build(IReadOnlyList<CppAstParseResult> parseResults)
    {
        ArgumentNullException.ThrowIfNull(parseResults);
        var functions = new List<CppFunction>();
        var classes = new List<CppClass>();
        var enums = new List<CppEnum>();
        var typedefs = new List<CppTypedef>();

        foreach (var compilation in parseResults.SelectMany(result => result.Compilations))
        {
            functions.AddRange(compilation.Functions.Where(_declarationPolicy.IsBindableFunction));
            classes.AddRange(compilation.Classes);
            enums.AddRange(compilation.Enums);
            typedefs.AddRange(compilation.Typedefs);
        }

        return new NativeDeclarationCatalog(
            functions.OrderBy(function => function.Name, StringComparer.Ordinal).ToList(),
            classes.OrderBy(cls => cls.Name, StringComparer.Ordinal).ToList(),
            enums.OrderBy(enumeration => enumeration.Name, StringComparer.Ordinal).ToList(),
            typedefs.OrderBy(typedef => typedef.Name, StringComparer.Ordinal).ToList());
    }
}
```

- [ ] **Step 4: Inject classifier into function and struct translators**

Change constructor signatures:

```csharp
internal sealed class BindingFunctionTranslator(
    BindableDeclarationPolicy declarationPolicy,
    NativeTypeClassifier typeClassifier)
```

Use:

```csharp
typeClassifier.Classify(parameter.Type, sourceHeader)
typeClassifier.Classify(function.ReturnType, sourceHeader)
```

Change `StructFieldTranslator.Translate(...)` to accept `NativeTypeClassifier typeClassifier` and call:

```csharp
Type: typeClassifier.Classify(fieldType, sourceHeader: Path.GetFileName(field.SourceFile ?? string.Empty))
```

- [ ] **Step 5: Update required constants translator**

Map manifest required constant type strings through semantic primitives:

```csharp
private static NativeTypeRef TypeFromRequiredConstant(RequiredConstantConfig required) =>
    required.Type switch
    {
        "uint" => NativeTypeRef.Primitive("unsigned int", "uint", NativeAbiShape.Of("uint", 4), required.SourceHeader),
        "ReadOnlySpan<byte>" => new NativeTypeRef("const char[]", "ReadOnlySpan<byte>", NativeTypeKind.Utf8Pointer, 0, null, required.SourceHeader, NativeAbiShape.Of("byte"), null, []),
        _ => NativeTypeRef.Primitive(required.Type, required.Type, NativeAbiShape.Of(required.Type), required.SourceHeader),
    };
```

- [ ] **Step 6: Update orchestrator wiring**

In `CppAstToBindingModel.Translate(...)`, create:

```csharp
var classificationContext = NativeTypeClassificationContext.FromConfig(config);
var typeClassifier = new NativeTypeClassifier(classificationContext);
var functionTranslator = new BindingFunctionTranslator(declarationPolicy, typeClassifier);
var structTranslator = new BindingStructTranslator(declarationPolicy, typeClassifier);
```

- [ ] **Step 7: Run focused translator tests**

Run:

```pwsh
dotnet test --project build\_build.Tests\Build.Tests.csproj -c Release --framework net10.0 -- --treenode-filter "/*/*/CppAstToBindingModelTests/*"
dotnet test --project build\_build.Tests\Build.Tests.csproj -c Release --framework net10.0 -- --treenode-filter "/*/*/BindingTranslationCollaboratorTests/*"
```

Expected: both pass.

---

## Task 6: Enum, Handle, and Callback Translators

**Import policy for Stage 1:** keep generated raw extern methods on `DllImport` for every target framework. `LibraryImport` / TFM-aware dual import emission is a Stage 2 emitter-policy concern, not part of Task 6 translator work. This keeps the near-term smoke path focused on semantic model correctness and compile-safe cross-TFM output.

**Status:** Completed. The production implementation uses `BindingEnumTranslator`, `BindingHandleTranslator`, and `BindingCallbackTranslator` over the shared `NativeDeclarationCatalog` / `NativeTypeClassifier` foundation, with `BindableDeclarationPolicy` filtering for SDL2 headers, owned names, and deferred declarations. Review tightened the handle path to include SDL private-tag typedefs such as `_SDL_GameController -> SDL_GameController`, and smoke validation tightened enum filtering so SDL's internal `SDL_DUMMY_ENUM` compile-time sentinel is deferred instead of emitted as public API.

**Smoke result:** `dotnet run --file tools.cs -- generate-bindings` now writes 12 preview files for `sdl2-core`, including `Types\Enums.g.cs`. The generated enum surface includes 56 public SDL enums and `[Flags] SDL_WindowFlags`; `SDL_DUMMY_ENUM` is absent after adding it to `binding_generation.deferred_declarations`. Handles and callbacks are present in the semantic model but do not emit separate files until Task 7 adds `CsHandleEmitter` and `CsCallbackEmitter`.

**Files:**
- Create: `build\_build\Targets\GenerateBindings\Translation\BindingEnumTranslator.cs`
- Create: `build\_build\Targets\GenerateBindings\Translation\BindingHandleTranslator.cs`
- Create: `build\_build\Targets\GenerateBindings\Translation\BindingCallbackTranslator.cs`
- Modify: `build\_build\Targets\GenerateBindings\Translation\CppAstToBindingModel.cs`
- Modify: `build\_build.Tests\Unit\Targets\GenerateBindings\Translation\BindingTranslationCollaboratorTests.cs`
- Modify: `build\_build.Tests\Unit\Targets\GenerateBindings\Translation\SemanticHeaderFixtureTests.cs`

- [ ] **Step 1: Write failing collaborator tests**

Add tests:

```csharp
public sealed class BindingHandleTranslatorTests
{
    [Test]
    public async Task Extract_Should_Create_Handles_From_Opaque_Owned_Structs()
    {
        var classifier = new NativeTypeClassifier(NativeTypeClassificationContext.FromConfig(BindingGenerationFixture.Sdl2CoreConfig()));
        var translator = new BindingHandleTranslator(classifier);
        var window = new CppClass("SDL_Window") { ClassKind = CppClassKind.Struct, IsDefinition = false };

        var handles = translator.Extract([window]);

        await Assert.That(handles.Single().Name).IsEqualTo("SDL_Window");
        await Assert.That(handles.Single().Type.Kind).IsEqualTo(NativeTypeKind.OpaqueHandle);
    }
}
```

- [ ] **Step 2: Run tests and verify RED**

Run:

```pwsh
dotnet test --project build\_build.Tests\Build.Tests.csproj -c Release --framework net10.0 -- --treenode-filter "/*/*/BindingHandleTranslatorTests/*"
```

Expected: compile fails because translator does not exist.

- [ ] **Step 3: Implement handle translator**

Create:

```csharp
using Build.Targets.GenerateBindings.Model;
using CppAst;

namespace Build.Targets.GenerateBindings.Translation;

internal sealed class BindingHandleTranslator(NativeTypeClassifier typeClassifier)
{
    private readonly NativeTypeClassifier _typeClassifier = typeClassifier ?? throw new ArgumentNullException(nameof(typeClassifier));

    public IReadOnlyList<BindingHandle> Extract(IReadOnlyList<CppClass> classes) =>
        classes
            .Select(cls => _typeClassifier.Classify(cls, Path.GetFileName(cls.SourceFile ?? string.Empty)))
            .Where(type => type.Kind == NativeTypeKind.OpaqueHandle)
            .OrderBy(type => type.ManagedName, StringComparer.Ordinal)
            .Select(type => new BindingHandle(type.ManagedName, type))
            .ToList();
}
```

- [ ] **Step 4: Add enum translator**

Create `BindingEnumTranslator.cs`:

```csharp
using Build.Targets.GenerateBindings.Model;
using CppAst;

namespace Build.Targets.GenerateBindings.Translation;

internal sealed class BindingEnumTranslator
{
    public IReadOnlyList<BindingEnumeration> Extract(IReadOnlyList<CppEnum> enums) =>
        enums
            .Where(enumeration => !string.IsNullOrWhiteSpace(enumeration.Name))
            .OrderBy(enumeration => enumeration.Name, StringComparer.Ordinal)
            .Select(Translate)
            .ToList();

    private static BindingEnumeration Translate(CppEnum enumeration)
    {
        var isFlags = enumeration.Name.EndsWith("Flags", StringComparison.Ordinal);
        var underlyingType = NativeTypeRef.Primitive("int", "int", NativeAbiShape.Of("int", 4), Path.GetFileName(enumeration.SourceFile ?? string.Empty));
        var members = enumeration.Items
            .Select(item => new BindingEnumMember(item.Name, item.ValueExpression ?? item.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)))
            .ToList();
        return new BindingEnumeration(enumeration.Name, underlyingType, isFlags, members);
    }
}
```

- [ ] **Step 5: Add callback translator**

Create `BindingCallbackTranslator.cs`:

```csharp
using Build.Targets.GenerateBindings.Model;
using CppAst;

namespace Build.Targets.GenerateBindings.Translation;

internal sealed class BindingCallbackTranslator(NativeTypeClassifier typeClassifier)
{
    private readonly NativeTypeClassifier _typeClassifier = typeClassifier ?? throw new ArgumentNullException(nameof(typeClassifier));

    public IReadOnlyList<BindingCallback> Extract(IReadOnlyList<CppTypedef> typedefs) =>
        typedefs
            .Where(typedef => typedef.ElementType is CppPointerType { ElementType: CppFunctionType })
            .OrderBy(typedef => typedef.Name, StringComparer.Ordinal)
            .Select(Translate)
            .ToList();

    private BindingCallback Translate(CppTypedef typedef)
    {
        var pointer = (CppPointerType)typedef.ElementType;
        var functionType = (CppFunctionType)pointer.ElementType;
        var sourceHeader = Path.GetFileName(typedef.SourceFile ?? string.Empty);
        var parameters = functionType.Parameters
            .Select((parameter, index) => new BindingParameter(
                _typeClassifier.Classify(parameter.Type, sourceHeader),
                TypeMappingPolicy.SafeIdentifier(parameter.Name, index)))
            .ToList();
        return new BindingCallback(
            typedef.Name,
            _typeClassifier.Classify(functionType.ReturnType, sourceHeader),
            parameters);
    }
}
```

- [ ] **Step 6: Wire into `CppAstToBindingModel`**

After building the catalog:

```csharp
var catalog = new NativeDeclarationCatalogBuilder(declarationPolicy).Build(parseResults);
var enums = new BindingEnumTranslator().Extract(catalog.Enums);
var handles = new BindingHandleTranslator(typeClassifier).Extract(catalog.Classes);
var callbacks = new BindingCallbackTranslator(typeClassifier).Extract(catalog.Typedefs);
```

Return:

```csharp
return new BindingModel(views, structs, enums, constants, handles, callbacks);
```

- [ ] **Step 7: Run translator suite**

Run:

```pwsh
dotnet test --project build\_build.Tests\Build.Tests.csproj -c Release --framework net10.0 -- --treenode-filter "/*/*/BindingTranslationCollaboratorTests/*"
dotnet test --project build\_build.Tests\Build.Tests.csproj -c Release --framework net10.0 -- --treenode-filter "/*/*/CppAstToBindingModelTests/*"
```

Expected: pass.

---

## Task 7: Semantic Emitters

**Status:** Completed. `CsHandleEmitter` now emits typed `readonly partial struct` handle wrappers in `Types\Handles.g.cs`, and `CsCallbackEmitter` emits SDL callback typedef delegates in `Types\Callbacks.g.cs`. `CsCommandEmitter` writes deterministic semantic category files, generated preview now writes 14 files, and `tests\binding-compile-check\SDL2.Core.CompileCheck.csproj` passes against the regenerated SDL2.Core preview.

Task 7 also closed the compile-check blockers exposed by the intermediate smoke: external/native-private opaque names (`_IO_FILE`, `SDL_iconv_t` / `_SDL_iconv_t`, `ID3D11Device`, `ID3D12Device`, `IDirect3DDevice9`) now map to `IntPtr` through semantic classification instead of leaking compiler/platform identifiers into generated C#. Code review found one semantic metadata issue for `ReadOnlySpan<byte>` required constants; it is now represented as a managed substitution with non-blittable span-shaped ABI metadata instead of a fake byte-sized UTF-8 pointer.

Callback surface decision: `Types\Callbacks.g.cs` is part of the public typed low-level surface, not the internal raw ABI surface and not the friendly callback layer. Peer research aligned on preserving typed callback pointer arguments in low-level APIs (`SDL_AssertData*`, `SDL_Event*`, `byte*`, etc.) rather than SDL2-CS-style all-`IntPtr` erasure. Stage 1 keeps `[UnmanagedFunctionPointer]` delegates for compile-safe legacy-TFM-friendly output; Stage 2 may introduce raw `delegate* unmanaged[Cdecl]` signatures and/or named function-pointer wrapper types, while friendly managed delegates/events/trampolines remain a later API-layer extension.

**Files:**
- Create: `build\_build\Targets\GenerateBindings\Emitting\CsHandleEmitter.cs`
- Create: `build\_build\Targets\GenerateBindings\Emitting\CsCallbackEmitter.cs`
- Modify: `build\_build\Targets\GenerateBindings\Emitting\CsCommandEmitter.cs`
- Modify: `build\_build\Targets\GenerateBindings\Emitting\CsStructEmitter.cs`
- Modify: `build\_build\Targets\GenerateBindings\Emitting\CsEnumEmitter.cs`
- Modify: `build\_build\Targets\GenerateBindings\Emitting\CsConstantEmitter.cs`
- Create: `build\_build.Tests\Unit\Targets\GenerateBindings\Emitting\SemanticEmitterTests.cs`
- Modify: `build\_build.Tests\Unit\Targets\GenerateBindings\Emitting\CsCommandEmitterTests.cs`

- [x] **Step 1: Write failing emitter tests**

Create `SemanticEmitterTests.cs`:

```csharp
using Build.Targets.GenerateBindings.Emitting;
using Build.Targets.GenerateBindings.Model;

namespace Build.Tests.Unit.Targets.GenerateBindings.Emitting;

public sealed class SemanticEmitterTests
{
    [Test]
    public async Task Emit_Should_Produce_Typed_Handle_File_When_Model_Has_Handles()
    {
        var windowType = NativeTypeRef.OpaqueHandle("SDL_Window", "SDL_Window", "sdl2-core", "SDL_video.h");
        var model = new BindingModel([], [], [], [], [new BindingHandle("SDL_Window", windowType)], []);

        var fileSet = CsCommandEmitter.Emit(model, new BindingEmissionOptions("SDL2", "SDL"));
        var handles = fileSet.Files.Single(file => file.RelativePath == "Types/Handles.g.cs");

        await Assert.That(handles.Content).Contains("public readonly partial struct SDL_Window(nint value)");
        await Assert.That(handles.Content).Contains("public readonly nint Value = value;");
    }
}
```

- [x] **Step 2: Run tests and verify RED**

Run:

```pwsh
dotnet test --project build\_build.Tests\Build.Tests.csproj -c Release --framework net10.0 -- --treenode-filter "/*/*/SemanticEmitterTests/*"
```

Expected: fails because handle emitter is not wired.

- [x] **Step 3: Implement handle emitter**

Create `CsHandleEmitter.cs`:

```csharp
using System.Text;
using Build.Host.Text;
using Build.Targets.GenerateBindings.Model;

namespace Build.Targets.GenerateBindings.Emitting;

internal static class CsHandleEmitter
{
    public static GeneratedFile Emit(IReadOnlyList<BindingHandle> handles, BindingEmissionOptions options)
    {
        ArgumentNullException.ThrowIfNull(handles);
        ArgumentNullException.ThrowIfNull(options);

        var builder = new StringBuilder();
        builder.AppendLf("// <auto-generated />");
        builder.AppendLf();
        builder.AppendLf("using System;");
        builder.AppendLf();
        builder.Append("namespace ").Append(options.ManagedNamespace).AppendLf(';');

        foreach (var handle in handles)
        {
            builder.AppendLf();
            builder.Append("public readonly partial struct ").Append(handle.Name).AppendLf("(nint value) : IEquatable<" + handle.Name + ">");
            builder.AppendLf('{');
            builder.AppendLf("    public readonly nint Value = value;");
            builder.AppendLf("    public bool IsNull => Value == 0;");
            builder.AppendLf("    public bool IsNotNull => Value != 0;");
            builder.Append("    public static ").Append(handle.Name).AppendLf(" Null => new(0);");
            builder.Append("    public bool Equals(").Append(handle.Name).AppendLf(" other) => Value == other.Value;");
            builder.AppendLf("    public override bool Equals(object? obj) => obj is " + handle.Name + " other && Equals(other);");
            builder.AppendLf("    public override int GetHashCode() => Value.GetHashCode();");
            builder.Append("    public static bool operator ==(").Append(handle.Name).Append(" left, ").Append(handle.Name).AppendLf(" right) => left.Equals(right);");
            builder.Append("    public static bool operator !=(").Append(handle.Name).Append(" left, ").Append(handle.Name).AppendLf(" right) => !left.Equals(right);");
            builder.Append("    public static implicit operator nint(").Append(handle.Name).AppendLf(" value) => value.Value;");
            builder.Append("    public static explicit operator ").Append(handle.Name).AppendLf("(nint value) => new(value);");
            builder.AppendLf('}');
        }

        return new GeneratedFile("Types/Handles.g.cs", builder.ToString());
    }
}
```

- [x] **Step 4: Wire category emitters**

In `CsCommandEmitter.Emit(...)`, add deterministic category order:

```csharp
if (model.Constants.Count > 0) files.Add(CsConstantEmitter.Emit(model.Constants, options));
if (model.Enums.Count > 0) files.Add(CsEnumEmitter.Emit(model.Enums, options));
if (model.Handles.Count > 0) files.Add(CsHandleEmitter.Emit(model.Handles, options));
if (model.Structs.Count > 0) files.Add(CsStructEmitter.Emit(model.Structs, options));
if (model.Callbacks.Count > 0) files.Add(CsCallbackEmitter.Emit(model.Callbacks, options));
```

- [x] **Step 5: Update existing emitters to read `NativeTypeRef.ManagedName`**

Replace uses of:

```csharp
field.Type.ManagedName
constant.Type.ManagedName
enumeration.UnderlyingType.ManagedName
```

with the same property on `NativeTypeRef`. Do not inspect `Kind` unless the emitted shape requires it.

- [x] **Step 6: Implement callback emitter**

Create `CsCallbackEmitter.cs`:

```csharp
using System.Text;
using Build.Host.Text;
using Build.Targets.GenerateBindings.Model;

namespace Build.Targets.GenerateBindings.Emitting;

internal static class CsCallbackEmitter
{
    public static GeneratedFile Emit(IReadOnlyList<BindingCallback> callbacks, BindingEmissionOptions options)
    {
        var builder = new StringBuilder();
        builder.AppendLf("// <auto-generated />");
        builder.AppendLf();
        builder.AppendLf("using System.Runtime.InteropServices;");
        builder.AppendLf();
        builder.Append("namespace ").Append(options.ManagedNamespace).AppendLf(';');

        foreach (var callback in callbacks)
        {
            builder.AppendLf();
            builder.AppendLf("[UnmanagedFunctionPointer(CallingConvention.Cdecl)]");
            builder.Append("public unsafe delegate ")
                .Append(callback.ReturnType.ManagedName)
                .Append(' ')
                .Append(callback.Name)
                .Append('(')
                .Append(string.Join(", ", callback.Parameters.Select(parameter => parameter.Type.ManagedName + " " + parameter.Name)))
                .AppendLf(");");
        }

        return new GeneratedFile("Types/Callbacks.g.cs", builder.ToString());
    }
}
```

- [x] **Step 7: Run emitter tests**

Run:

```pwsh
dotnet test --project build\_build.Tests\Build.Tests.csproj -c Release --framework net10.0 -- --treenode-filter "/*/*/SemanticEmitterTests/*"
dotnet test --project build\_build.Tests\Build.Tests.csproj -c Release --framework net10.0 -- --treenode-filter "/*/*/CsCommandEmitterTests/*"
```

Expected: pass.

---

## Task 8: Semantic Validators

**Files:**
- Modify: `build\_build\Validation\BindingGeneration\DynapiCoherenceValidator.cs`
- Modify: existing validator tests under `build\_build.Tests\Unit\Validation\BindingGeneration\`
- Create or modify: `build\_build\Validation\BindingGeneration\SemanticTypeConsistencyValidator.cs`
- Create or modify: `build\_build.Tests\Unit\Validation\BindingGeneration\SemanticTypeConsistencyValidatorTests.cs`

- [x] **Step 1: Add dynapi exclusion test**

In `DynapiCoherenceValidatorTests.cs`, add:

```csharp
[Test]
public async Task ValidateAsync_Should_Ignore_Configured_Excluded_Functions()
{
    var validator = new DynapiCoherenceValidator(
        new FakeDynapiManifestRepository(Symbols("SDL_Init", "SDL_DYNAPI_entry")));
    var config = BindingGenerationFixture.Sdl2CoreConfig();
    var model = BindingGenerationFixture.ModelWithNeutralFunctions("SDL_Init");

    var report = await validator.ValidateAsync(model, config, CancellationToken.None);

    await Assert.That(report.Errors).IsEmpty();
}
```

- [x] **Step 2: Run validator test and verify RED**

Run:

```pwsh
dotnet test --project build\_build.Tests\Build.Tests.csproj -c Release --framework net10.0 -- --treenode-filter "/*/*/DynapiCoherenceValidatorTests/*"
```

Expected: fails if `SDL_DYNAPI_entry` is still reported as missing.

- [x] **Step 3: Update dynapi validator**

When computing missing exports, subtract `config.ExcludedFunctions`:

```csharp
var expectedExports = manifest.PublicSymbols
    .Except(config.ExcludedFunctions, StringComparer.Ordinal)
    .ToHashSet(StringComparer.Ordinal);
```

- [x] **Step 4: Add semantic consistency validator**

Create validator that fails on `NativeTypeKind.Unsupported` and unexpected `NativeTypeKind.Deferred` outside configured deferrals. Keep it narrow:

```csharp
public sealed class SemanticTypeConsistencyValidator : IBindingFamilyValidator
{
    public string ValidatorId => "semantic-type-consistency";

    public Task<ValidationReport> ValidateAsync(BindingModel model, BindingGenerationConfig config, CancellationToken ct)
    {
        var checks = new List<ValidationCheck>();
        foreach (var function in model.Views.SelectMany(view => view.Functions))
        {
            AddTypeChecks(checks, function.Name, function.ReturnType, config);
            foreach (var parameter in function.Parameters)
            {
                AddTypeChecks(checks, function.Name + "." + parameter.Name, parameter.Type, config);
            }
        }

        return Task.FromResult(new ValidationReport(checks));
    }

    private static void AddTypeChecks(List<ValidationCheck> checks, string owner, NativeTypeRef type, BindingGenerationConfig config)
    {
        if (type.Kind == NativeTypeKind.Unsupported)
        {
            checks.Add(new ValidationCheck(
                "Unsupported native type",
                ValidationSeverity.Error,
                $"{owner} uses unsupported native type '{type.NativeName}'."));
        }

        if (type.Kind == NativeTypeKind.Deferred && !config.DeferredDeclarations.ContainsKey(type.NativeName))
        {
            checks.Add(new ValidationCheck(
                "Unexpected deferred native type",
                ValidationSeverity.Error,
                $"{owner} uses deferred native type '{type.NativeName}' without manifest declaration."));
        }
    }
}
```

- [x] **Step 5: Register validator**

Modify `build\_build\Validation\ServiceCollectionExtensions.cs` to register:

```csharp
services.AddSingleton<IBindingFamilyValidator, SemanticTypeConsistencyValidator>();
```

Add manifest validator toggle only if the validator is enabled through manifest in the same way as existing validators.

- [x] **Step 6: Run validator tests**

Run:

```pwsh
dotnet test --project build\_build.Tests\Build.Tests.csproj -c Release --framework net10.0 -- --treenode-filter "/*/*/DynapiCoherenceValidatorTests/*"
dotnet test --project build\_build.Tests\Build.Tests.csproj -c Release --framework net10.0 -- --treenode-filter "/*/*/SemanticTypeConsistencyValidatorTests/*"
```

Expected: pass.

**Task 8 result:** Done. `DynapiCoherenceValidator` now subtracts `binding_generation.excluded_functions` before comparing emitted symbols against SDL2 dynapi exports, so `SDL_DYNAPI_entry` no longer reports as missing. `SemanticTypeConsistencyValidator` is registered and enabled through `build\manifest.json`; it fails on unsupported semantic types and on deferred types that are not explicitly listed in `binding_generation.deferred_declarations`.

The first real `generate-bindings` run with semantic validation exposed classifier gaps instead of mere validator noise. The classifier now maps callback typedef references, direct function-pointer fields, pointer-to-callback output parameters, and decayed array parameters to ABI-safe function-pointer/pointer storage. Public callback declarations still emit in `Types\Callbacks.g.cs`, but struct fields and raw ABI references store callback pointers as `IntPtr` / `IntPtr*` so generated structs remain unmanaged and compile-check stays legal. Current generation still reports the five known dynapi warnings for `SDL_LogMessageV`, `SDL_RWFromFP`, `SDL_vasprintf`, `SDL_vsnprintf`, and `SDL_vsscanf`; those are not Task 8 failures.

---

## Task 9: GenerateBindings Orchestration, Rich Parse-Views, and Spike Remnant Retirement

**Files:**
- Modify: `build\_build\Targets\GenerateBindings\GenerateBindingsTask.cs`
- Modify: `build\_build\Targets\GenerateBindings\Emitting\BindingParseViewReport.cs`
- Modify: `build\_build\Targets\GenerateBindings\Emitting\CsCommandEmitter.cs`
- Modify: semantic model / translation records only if `parse-views.json` needs additional already-owned evidence to avoid recomputing it in the emitter.
- Modify: `build\_build.Tests\Scenarios\GenerateBindings\GenerateBindingsTaskScenarioTests.cs`
- Modify: `build\_build.Tests\Unit\Targets\GenerateBindings\Emitting\CsCommandEmitterTests.cs`
- Modify: `tests\binding-compile-check\SDL2.Core.CompileCheck.csproj` only if generated file inventory path assumptions changed.
- Modify: `C:\Users\deniz\.copilot\session-state\f5ef8536-6970-41b4-b5bc-bffcfbc179dc\plan.md`

- [x] **Step 1: Add scenario test for semantic inventory**

Extend `GenerateBindingsTaskScenarioTests.cs` with a fake parser result that produces at least one handle, struct, enum, constant, callback, and function. Assert output includes:

```text
Constants.g.cs
Types/Enums.g.cs
Types/Handles.g.cs
Types/Structs.g.cs
Types/Callbacks.g.cs
Platform/Neutral/Commands.g.cs
parse-views.json
```

- [x] **Step 2: Add emitter test for rich parse-views.json**

Extend `CsCommandEmitterTests.cs` so `parse-views.json` is no longer only a thin view/function list. The report should remain a deterministic audit sidecar, not a public API contract. It should include enough review evidence to answer "why did this symbol/file land here?" without opening every generated `.g.cs` file:

```text
schema/version marker
semantic category counts: views, functions, structs, enums, constants, handles, callbacks
emitted file inventory
per-view name, platform kind/attribution, defines, undefines
per-view function count and function source headers/signatures
```

If a field needs parse-input metadata that is currently lost before emission, carry it through the semantic model explicitly instead of re-reading headers or recomputing parse state in the emitter.

- [x] **Step 3: Run scenario/emitter tests and verify RED**

Run:

```pwsh
dotnet test --project build\_build.Tests\Build.Tests.csproj -c Release --framework net10.0 -- --treenode-filter "/*/*/GenerateBindingsTaskScenarioTests/*"
dotnet test --project build\_build.Tests\Build.Tests.csproj -c Release --framework net10.0 -- --treenode-filter "/*/*/CsCommandEmitterTests/*"
```

Expected: fails until all category emitters are wired and `parse-views.json` carries the richer audit shape.

- [x] **Step 4: Update orchestration logging**

In `GenerateBindingsTask.LogPerViewCounts(...)`, add category counts:

```csharp
_log.Information(
    "Model categories: {0} structs, {1} enums, {2} constants, {3} handles, {4} callbacks.",
    model.Structs.Count,
    model.Enums.Count,
    model.Constants.Count,
    model.Handles.Count,
    model.Callbacks.Count);
```

- [x] **Step 5: Retire generator-spike remnants deliberately**

Audit the GenerateBindings target and tests for spike-era bridges/shapes that are now superseded by the semantic model rewrite. Retire only the remnants whose behavior is covered by semantic translator/emitter tests in this slice. Good candidates include stale report assumptions, old preview-inventory expectations, and bridge helpers that no longer have a real caller after required functions/types flow through semantic translation. Do not delete `external\sdl2-cs`; this item is only about generator-internal spike leftovers.

- [x] **Step 6: Run scenario tests and compile check**

Run:

```pwsh
dotnet test --project build\_build.Tests\Build.Tests.csproj -c Release --framework net10.0 -- --treenode-filter "/*/*/GenerateBindingsTaskScenarioTests/*"
dotnet test --project build\_build.Tests\Build.Tests.csproj -c Release --framework net10.0 -- --treenode-filter "/*/*/CsCommandEmitterTests/*"
dotnet build tests\binding-compile-check\SDL2.Core.CompileCheck.csproj -c Release
```

Expected: scenario tests pass; compile check succeeds against current generated preview or clearly fails because preview needs regeneration. If compile check fails due stale preview, run Task 10 before treating this as a defect.

**Task 9 result:** Done. `parse-views.json` now carries schema version, semantic category counts/names, emitted-file inventory, per-view parse inputs (`PlatformConditionKind`, defines, undefines, supported OS), and function signature evidence. `GenerateBindingsTask` logs semantic category counts before per-view function counts. Generator-internal spike remnants were audited and stale phase comments were rewritten around the current semantic pipeline; transitional `BindingTypeRef`/bridge adapters remain only where still load-bearing for manifest-required functions or legacy tests. Real generation reports 8 views, 866 functions, 74 structs, 56 enums, 10 constants, 17 handles, 19 callbacks, and 14 emitted files. The known dynapi warnings remain `SDL_LogMessageV`, `SDL_RWFromFP`, `SDL_vasprintf`, `SDL_vsnprintf`, and `SDL_vsscanf`.

---

## Task 10: Full Verification and SDL2.Core Readiness Report

**Files:**
- Modify: `docs\superpowers\specs\2026-05-18-semantic-binding-pipeline-design.md` only for durable clarifications.
- Modify: `C:\Users\deniz\.copilot\session-state\f5ef8536-6970-41b4-b5bc-bffcfbc179dc\plan.md`

- [ ] **Step 1: Regenerate preview from a clean folder**

Run:

```pwsh
if (Test-Path artifacts\generated-bindings-preview\sdl2-core) { Remove-Item -Recurse -Force artifacts\generated-bindings-preview\sdl2-core }
dotnet run --file tools.cs -- generate-bindings
```

Expected: `GenerateBindings` succeeds and writes category files under `artifacts\generated-bindings-preview\sdl2-core`.

- [ ] **Step 2: Inspect generated inventory**

Run:

```pwsh
Get-ChildItem -Path artifacts\generated-bindings-preview\sdl2-core -Recurse -File |
    Sort-Object FullName |
    Select-Object @{Name='Path';Expression={$_.FullName.Substring((Get-Location).Path.Length + 1)}}, Length |
    Format-Table -AutoSize
```

Expected inventory includes:

```text
artifacts\generated-bindings-preview\sdl2-core\Constants.g.cs
artifacts\generated-bindings-preview\sdl2-core\Types\Enums.g.cs
artifacts\generated-bindings-preview\sdl2-core\Types\Handles.g.cs
artifacts\generated-bindings-preview\sdl2-core\Types\Structs.g.cs
artifacts\generated-bindings-preview\sdl2-core\Types\Callbacks.g.cs
artifacts\generated-bindings-preview\sdl2-core\Platform\Neutral\Commands.g.cs
artifacts\generated-bindings-preview\sdl2-core\parse-views.json
```

- [ ] **Step 3: Verify high-value symbols**

Run:

```pwsh
rg "SDL_INIT_TIMER|SDL_HINT_RENDER_DRIVER|SDL_WindowFlags|readonly partial struct SDL_Window|SDL_AudioCallback|SDL_bool" artifacts\generated-bindings-preview\sdl2-core -n
```

Expected: every listed symbol appears under `artifacts\generated-bindings-preview\sdl2-core`.

- [ ] **Step 4: Run compile check**

Run:

```pwsh
dotnet build tests\binding-compile-check\SDL2.Core.CompileCheck.csproj -c Release
```

Expected: build succeeds.

- [ ] **Step 5: Run full test suite**

Run:

```pwsh
dotnet test --project build\_build.Tests\Build.Tests.csproj -c Release --framework net10.0
```

Expected: all non-skipped tests pass.

- [ ] **Step 6: Run slopwatch**

Run:

```pwsh
slopwatch analyze --fail-on warning --exclude "artifacts/**,external/**,vcpkg_installed/**,tools/binding-spike/**,**/bin/**,**/obj/**"
```

Expected: `Scan complete: 0 issue(s) found`.

- [ ] **Step 7: Run diff check**

Run:

```pwsh
git --no-pager diff --check
```

Expected: exit code 0. CRLF warnings without a non-zero exit are acceptable in this repository.

- [ ] **Step 8: Update session plan**

Update:

```text
C:\Users\deniz\.copilot\session-state\f5ef8536-6970-41b4-b5bc-bffcfbc179dc\plan.md
```

Record:

- semantic pipeline rewrite completed
- generated inventory
- known remaining SDL2.Core flip prerequisites
- exact verification commands and outcomes

- [ ] **Step 9: Present completion summary and commit request**

Before committing, present:

```text
Summary:
- Replaced string-first type flow with semantic NativeTypeRef model.
- Rebuilt translators, emitters, and validators around semantic BindingModel.
- Added embedded .h fixture tests and generated inventory gates.
- Regenerated preview output and compile-checked SDL2.Core generated input.

Proposed commit:
feat(binding-autogen): rewrite generator around semantic type model

Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>
```

Only commit after explicit approval.

---

## Self-Review Checklist

- Spec coverage:
  - Semantic type model: Tasks 1-2.
  - Manifest config vs policy: Tasks 2, 5, 8.
  - Binding model rewrite: Task 4.
  - Translators: Tasks 5-6.
  - Emitters: Task 7.
  - Validators: Task 8.
  - `.h` fixture architecture: Task 3.
  - SDL2.Core readiness lane: Task 10.
- Placeholder scan:
  - The plan avoids unfinished-marker words and unspecified "add tests" steps.
  - Each code-producing task includes concrete file paths and code skeletons.
- Type consistency:
  - `NativeTypeRef`, `NativeTypeKind`, `NativeAbiShape`, and `NativeTypeDiagnostic` are introduced before use.
  - Translators and emitters consistently consume `NativeTypeRef`.
  - Existing `BindingTypeRef` is intentionally retired or reduced after the semantic records exist.
