using Build.Data.Versions;

namespace Build.Tests.Unit.Data.Versions;

/// <summary>
/// Tests for <see cref="PackageFamilyId"/> — the format-agnostic, ordinal-exact
/// value object for package-family identity.
/// </summary>
public sealed class PackageFamilyIdTests
{
    [Test]
    public async Task Constructor_Should_Throw_ArgumentException_When_Value_Is_Null()
    {
        await Assert.That(() => new PackageFamilyId(null!)).Throws<ArgumentException>();
    }

    [Test]
    public async Task Constructor_Should_Throw_ArgumentException_When_Value_Is_Empty()
    {
        await Assert.That(() => new PackageFamilyId(string.Empty)).Throws<ArgumentException>();
    }

    [Test]
    public async Task Constructor_Should_Throw_ArgumentException_When_Value_Is_Whitespace()
    {
        await Assert.That(() => new PackageFamilyId("   ")).Throws<ArgumentException>();
    }

    [Test]
    public async Task Constructor_Should_Accept_Format_Agnostic_Value()
    {
        var id = new PackageFamilyId("anything-goes-123");

        await Assert.That(id.Value).IsEqualTo("anything-goes-123");
    }

    [Test]
    public async Task ToString_Should_Return_Value()
    {
        var id = new PackageFamilyId("sdl2-core");

        await Assert.That(id.ToString()).IsEqualTo("sdl2-core");
    }

    [Test]
    public async Task Equals_Should_Treat_Different_Case_As_Different_Identifiers()
    {
        var lower = new PackageFamilyId("sdl2-core");
        var upper = new PackageFamilyId("SDL2-Core");

        await Assert.That(lower.Equals(upper)).IsFalse();
        await Assert.That(lower == upper).IsFalse();
    }

    [Test]
    public async Task Implicit_String_Conversion_Should_Return_Value()
    {
        var id = new PackageFamilyId("sdl2-core");

        string asString = id;

        await Assert.That(asString).IsEqualTo("sdl2-core");
    }
}
