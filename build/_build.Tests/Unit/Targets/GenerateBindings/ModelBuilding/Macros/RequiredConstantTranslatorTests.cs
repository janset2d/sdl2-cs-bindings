using Build.Targets.GenerateBindings.Model;
using Build.Targets.GenerateBindings.ModelBuilding.Macros;
using Build.Tests.Fixtures;

namespace Build.Tests.Unit.Targets.GenerateBindings.ModelBuilding.Macros;

public sealed class RequiredConstantTranslatorTests
{
    [Test]
    public async Task Translate_Should_Model_Utf8_Literal_Constant_As_Managed_Span_Metadata()
    {
        var constant = BindingGenerationFixture.RequiredConstant(
            name: "SDL_HINT_RENDER_DRIVER",
            type: "ReadOnlySpan<byte>",
            value: "\"SDL_RENDER_DRIVER\"u8",
            sourceHeader: "SDL_hints.h");

        var sut = RequiredConstantTranslator.Translate([constant]).Single();

        await Assert.That(sut.Type.Kind).IsEqualTo(NativeTypeKind.SubstitutedManagedType);
        await Assert.That(sut.Type.NativeName).IsEqualTo("const char[]");
        await Assert.That(sut.Type.ManagedName).IsEqualTo("ReadOnlySpan<byte>");
        await Assert.That(sut.Type.PointerDepth).IsEqualTo(0);
        await Assert.That(sut.Type.AbiShape.StorageName).IsEqualTo("ReadOnlySpan<byte>");
        await Assert.That(sut.Type.AbiShape.SizeBytes).IsEqualTo(IntPtr.Size * 2);
        await Assert.That(sut.Type.AbiShape.IsBlittable).IsFalse();
    }
}
