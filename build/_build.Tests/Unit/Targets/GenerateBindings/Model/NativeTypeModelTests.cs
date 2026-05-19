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

    [Test]
    public async Task Pointer_Should_Throw_When_Element_Type_Is_Already_A_Pointer()
    {
        var element = NativeTypeRef.Primitive("int", "int", NativeAbiShape.Of("int", 4));
        var pointer = NativeTypeRef.Indirection(element, indirectionDepth: 1, managedName: "int*");

        await Assert.That(() => NativeTypeRef.Indirection(pointer, indirectionDepth: 2, managedName: "int**"))
            .Throws<ArgumentException>();
    }

    [Test]
    public async Task Indirection_Should_Throw_When_Indirection_Depth_Is_Not_Positive()
    {
        var element = NativeTypeRef.Primitive("int", "int", NativeAbiShape.Of("int", 4));

        await Assert.That(() => NativeTypeRef.Indirection(element, indirectionDepth: 0, managedName: "int"))
            .Throws<ArgumentOutOfRangeException>();
    }
}
