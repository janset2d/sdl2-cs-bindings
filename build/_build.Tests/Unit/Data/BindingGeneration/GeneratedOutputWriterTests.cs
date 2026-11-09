using Build.Data.BindingGeneration;
using Build.Data.BindingGeneration.Models;
using Build.Tests.Fixtures;
using Cake.Common.IO;
using Cake.Core;
using Cake.Core.IO;

namespace Build.Tests.Unit.Data.BindingGeneration;

public sealed class GeneratedOutputWriterTests
{
    [Test]
    [MethodDataSource(typeof(GeneratedOutputWriterData), nameof(GeneratedOutputWriterData.NullInputs))]
    public async Task WriteAsync_Should_Reject_Null_Inputs_Before_Clearing(
        DirectoryPath? root, DirectoryPath? output, IReadOnlyList<GeneratedFile>? files, string parameter)
    {
        var world = FakeCakeWorld.CreateLinux().WithTextFile("Generated/family/keep.txt", "keep");
        var writer = new GeneratedOutputWriter(world.CakeContext);
        var exception = await Assert.That(() => writer.WriteAsync(root!, output!, files!))
            .Throws<ArgumentNullException>();

        await Assert.That(exception!.ParamName).IsEqualTo(parameter);
        await Assert.That(Inventory(world, world.RepoRoot.Combine("Generated")))
            .IsEquivalentTo(["/repo/Generated/family/keep.txt"], TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(world.ReadAllText("Generated/family/keep.txt")).IsEqualTo("keep");
    }

    [Test]
    [Arguments("x/../../src", "/repo/artifacts/src")]
    [Arguments("x/..", "/repo/artifacts/generated-bindings-preview")]
    public async Task WriteAsync_Should_Reject_Output_When_Traversal_Escapes_Root(string suffix, string collapsed)
    {
        var world = FakeCakeWorld.CreateLinux().WithTextFile("artifacts/src/keep.txt", "keep");
        var root = world.RepoRoot.Combine("artifacts/generated-bindings-preview");
        await AssertRefusalAsync(world, root, root.Combine(suffix), collapsed);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task WriteAsync_Should_Reject_Output_When_Root_Case_Differs_On_Unix(bool osx)
    {
        var world = (osx ? FakeCakeWorld.CreateOsx() : FakeCakeWorld.CreateLinux())
            .WithTextFile("generated/family/keep.txt", "keep");
        await AssertRefusalAsync(world, world.RepoRoot.Combine("Generated"),
            world.RepoRoot.Combine("generated/family"), "/repo/generated/family");
    }

    [Test]
    public async Task WriteAsync_Should_Write_Output_When_Root_Case_Differs_On_Windows()
    {
        var world = FakeCakeWorld.CreateWindows().WithTextFile("generated/family/old/nested.txt", "stale");
        var writer = new GeneratedOutputWriter(world.CakeContext);
        await Assert.That(() => writer.WriteAsync(world.RepoRoot.Combine("Generated"), world.RepoRoot.Combine("generated/family"),
            [new GeneratedFile("new.txt", "new")]))
            .ThrowsNothing();
        await Assert.That(world.FileExists("generated/family/new.txt")).IsTrue();
        await Assert.That(world.ReadAllText("generated/family/new.txt")).IsEqualTo("new");
        await Assert.That(world.FileExists("generated/family/old/nested.txt")).IsFalse();
        await Assert.That(world.CakeContext.DirectoryExists(world.RepoRoot.Combine("generated/family/old"))).IsFalse();
    }

    [Test]
    public async Task WriteAsync_Should_Replace_Stale_Files_When_Output_Is_Inside_Root()
    {
        var world = FakeCakeWorld.CreateLinux()
            .WithTextFile("Generated/family/old/nested.txt", "stale")
            .WithTextFile("Generated/sibling/keep.txt", "keep");
        var writer = new GeneratedOutputWriter(world.CakeContext);
        await writer.WriteAsync(world.RepoRoot.Combine("Generated"), world.RepoRoot.Combine("Generated/family"),
            [new GeneratedFile("nested/new.txt", "nested"), new GeneratedFile("root.txt", "root")]);
        string[] expected =
        [
            "/repo/Generated/family/nested/new.txt", "/repo/Generated/family/root.txt", "/repo/Generated/sibling/keep.txt",
        ];
        await Assert.That(Inventory(world, world.RepoRoot.Combine("Generated"))).IsEquivalentTo(expected, TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(world.ReadAllText("Generated/family/nested/new.txt")).IsEqualTo("nested");
        await Assert.That(world.ReadAllText("Generated/family/root.txt")).IsEqualTo("root");
        await Assert.That(world.ReadAllText("Generated/sibling/keep.txt")).IsEqualTo("keep");
        await Assert.That(world.CakeContext.DirectoryExists(world.RepoRoot.Combine("Generated/family/old"))).IsFalse();
    }

    [Test]
    public async Task WriteAsync_Should_Reject_Output_When_Output_Equals_Root()
    {
        var world = FakeCakeWorld.CreateLinux().WithTextFile("Generated/keep.txt", "keep");
        var root = world.RepoRoot.Combine("Generated");
        await AssertRefusalAsync(world, root, root, "/repo/Generated");
    }

    [Test]
    public async Task WriteAsync_Should_Reject_Output_When_Output_Is_Outside_Root()
    {
        var world = FakeCakeWorld.CreateLinux().WithTextFile("src/keep.txt", "keep");
        await AssertRefusalAsync(world, world.RepoRoot.Combine("Generated"), world.RepoRoot.Combine("src"), "/repo/src");
    }

    [Test]
    public async Task WriteAsync_Should_Cancel_Before_Writing_When_Token_Is_Cancelled()
    {
        var world = FakeCakeWorld.CreateLinux();
        var writer = new GeneratedOutputWriter(world.CakeContext);
        await Assert.That(() => writer.WriteAsync(world.RepoRoot.Combine("Generated"), world.RepoRoot.Combine("Generated/family"),
            [new GeneratedFile("new.txt", "new")], new CancellationToken(true)))
            .Throws<OperationCanceledException>();
        await Assert.That(world.FileExists("Generated/family/new.txt")).IsFalse();
    }

    private static async Task AssertRefusalAsync(FakeCakeWorld world, DirectoryPath root, DirectoryPath output, string collapsed)
    {
        var before = Inventory(world, world.RepoRoot);
        var contents = before.Select(path => world.ReadAllText(world.RepoRoot.GetRelativePath(new FilePath(path)).FullPath)).ToArray();
        var writer = new GeneratedOutputWriter(world.CakeContext);
        var exception = await Assert.That(() => writer.WriteAsync(root, output, [new GeneratedFile("new.txt", "new")])).Throws<CakeException>();
        await Assert.That(exception!.Message).IsEqualTo(
            $"Refusing to clear generated bindings output outside '{root.FullPath}': '{collapsed}'.");
        await Assert.That(Inventory(world, world.RepoRoot)).IsEquivalentTo(before, TUnit.Assertions.Enums.CollectionOrdering.Matching);

        for (var i = 0; i < before.Length; i++)
        {
            await Assert.That(world.ReadAllText(world.RepoRoot.GetRelativePath(new FilePath(before[i])).FullPath)).IsEqualTo(contents[i]);
        }

        var sentinel = before.Single(path => path.EndsWith("/keep.txt", StringComparison.Ordinal));
        await Assert.That(world.ReadAllText(world.RepoRoot.GetRelativePath(new FilePath(sentinel)).FullPath)).IsEqualTo("keep");
    }

    private static string[] Inventory(FakeCakeWorld world, DirectoryPath root) =>
        [.. world.CakeContext.GetFiles($"{root.FullPath}/**/*").Select(path => path.FullPath).Order(StringComparer.Ordinal)];
}
