using System.Text;
using Build.Data.BindingGeneration;
using Build.Data.BindingGeneration.Models;
using Build.Tests.Fixtures;
using Cake.Core;
using Cake.Core.IO;
using Cake.Testing;
using TUnit.Assertions.Enums;

namespace Build.Tests.Unit.Data.BindingGeneration;

public sealed class GeneratedOutputSafetyTests
{
    [Test]
    [Arguments("../escape.g.cs")]
    [Arguments("nested/../escape.g.cs")]
    [Arguments("/escape.g.cs")]
    [Arguments("C:/escape.g.cs")]
    [Arguments("C:escape.g.cs")]
    [Arguments("\\\\server\\share\\escape.g.cs")]
    public async Task WriteAsync_Should_Reject_Every_Unsafe_Path_Before_Cleanup(string path)
    {
        var world = FakeCakeWorld.CreateLinux().WithTextFile("family/Generated/keep.txt", "sentinel");
        var writer = new GeneratedOutputWriter(world.CakeContext);
        await Assert.That(() => writer.WriteAsync(world.RepoRoot.Combine("family"), world.RepoRoot.Combine("family/Generated"),
            [new GeneratedFile("valid.g.cs", "valid"), new GeneratedFile(path, "invalid")]))
            .Throws<CakeException>();
        await Assert.That(world.ReadAllText("family/Generated/keep.txt")).IsEqualTo("sentinel");
        await Assert.That(world.FileSystem.GetDirectory(world.RepoRoot).GetFiles("*", SearchScope.Recursive).Select(file => file.Path.FullPath).Order(StringComparer.Ordinal))
            .IsEquivalentTo(["/repo/build/manifest.json", "/repo/family/Generated/keep.txt"], CollectionOrdering.Matching);
    }

    [Test]
    [Arguments("a.g.cs")]
    [Arguments("A.g.cs")]
    public async Task WriteAsync_Should_Reject_Duplicate_And_Case_Colliding_Paths_Before_Cleanup(string duplicate)
    {
        var world = FakeCakeWorld.CreateLinux().WithTextFile("family/Generated/keep.txt", "sentinel");
        var writer = new GeneratedOutputWriter(world.CakeContext);
        await Assert.That(() => writer.WriteAsync(world.RepoRoot.Combine("family"), world.RepoRoot.Combine("family/Generated"),
            [new GeneratedFile("a.g.cs", "one"), new GeneratedFile(duplicate, "two")]))
            .Throws<CakeException>();
        await Assert.That(world.ReadAllText("family/Generated/keep.txt")).IsEqualTo("sentinel");
        await Assert.That(world.FileExists("family/Generated/a.g.cs")).IsFalse();
    }

    [Test]
    public async Task WriteAsync_Should_Preserve_The_Previous_Tree_When_Already_Cancelled()
    {
        var world = FakeCakeWorld.CreateLinux().WithTextFile("family/Generated/keep.txt", "sentinel");
        var writer = new GeneratedOutputWriter(world.CakeContext);
        await Assert.That(() => writer.WriteAsync(world.RepoRoot.Combine("family"), world.RepoRoot.Combine("family/Generated"),
            [new GeneratedFile("new.g.cs", "new")], new CancellationToken(true)))
            .Throws<OperationCanceledException>();
        await Assert.That(world.ReadAllText("family/Generated/keep.txt")).IsEqualTo("sentinel");
    }

    [Test]
    public async Task WriteAsync_Should_Write_Empty_Files_And_Utf8_Without_Bom_With_Lf()
    {
        var world = FakeCakeWorld.CreateLinux();
        var writer = new GeneratedOutputWriter(world.CakeContext);
        await writer.WriteAsync(world.RepoRoot.Combine("family"), world.RepoRoot.Combine("family/Generated"),
            [new GeneratedFile("empty.g.cs", ""), new GeneratedFile("nested/text.g.cs", "é\r\nsecond\rthird\n")]);
        await Assert.That(world.FileSystem.GetFile(world.RepoRoot.CombineWithFilePath("family/Generated/empty.g.cs")).GetBinaryContent()).IsEmpty();
        await Assert.That(world.FileSystem.GetFile(world.RepoRoot.CombineWithFilePath("family/Generated/nested/text.g.cs")).GetBinaryContent())
            .IsEquivalentTo(Encoding.UTF8.GetBytes("é\nsecond\nthird\n"), CollectionOrdering.Matching);
    }
}
