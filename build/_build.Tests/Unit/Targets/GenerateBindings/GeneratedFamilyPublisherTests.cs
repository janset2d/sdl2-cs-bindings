using Build.Data.BindingGeneration.Models;
using Build.Targets.GenerateBindings;
using Build.Tests.Fixtures;
using Cake.Core;
using Cake.Core.IO;
using Cake.Testing;
using NSubstitute;
using TUnit.Assertions.Enums;

namespace Build.Tests.Unit.Targets.GenerateBindings;

public sealed class GeneratedFamilyPublisherTests
{
    [Test]
    public async Task PublishAsync_Should_Replace_All_Selected_Roots_And_Preserve_Siblings()
    {
        var world = World();
        await new GeneratedFamilyPublisher(world.CakeContext).PublishAsync(Families(world), "/temporary/backups", CancellationToken.None);
        await Assert.That(world.FileExists("one/Generated/old.bin")).IsFalse();
        await Assert.That(world.ReadAllText("one/Generated/new.g.cs")).IsEqualTo("new one\n");
        await Assert.That(world.ReadAllText("two/Generated/new.g.cs")).IsEqualTo("new two\n");
        await Assert.That(world.ReadAllText("other/Generated/keep.txt")).IsEqualTo("untouched");
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task PublishAsync_Should_Restore_All_Roots_When_A_Later_Replacement_Fails_Or_Cancels(bool cancel)
    {
        var world = World();
        var before = Snapshot(world);
        using var cancellation = new CancellationTokenSource();
        var cancellationCompletion = Task.CompletedTask;
        var fileSystem = Substitute.For<IFileSystem>();
        fileSystem.GetDirectory(Arg.Any<DirectoryPath>()).Returns(call => world.FileSystem.GetDirectory(call.Arg<DirectoryPath>()));
        fileSystem.GetFile(Arg.Any<FilePath>()).Returns(call =>
        {
            var path = call.Arg<FilePath>();

            if (path.FullPath == "/repo/two/Generated/new.g.cs")
            {
                if (cancel)
                {
                    cancellationCompletion = cancellation.CancelAsync();
                }
                else
                {
                    throw new IOException("later replacement failed");
                }
            }

            return world.FileSystem.GetFile(path);
        });
        world.CakeContext.FileSystem.Returns(fileSystem);
        var publisher = new GeneratedFamilyPublisher(world.CakeContext);

        try
        {
            if (cancel)
            {
                await Assert.That(() => publisher.PublishAsync(Families(world), "/temporary/backups", cancellation.Token)).Throws<OperationCanceledException>();
            }
            else
            {
                await Assert.That(() => publisher.PublishAsync(Families(world), "/temporary/backups", cancellation.Token)).Throws<IOException>();
            }
        }
        finally
        {
            await cancellationCompletion;
        }

        await Assert.That(Snapshot(world)).IsEquivalentTo(before, CollectionOrdering.Matching);
    }

    [Test]
    public async Task PublishAsync_Should_Reject_A_Later_Path_Before_Changing_Any_Root()
    {
        var world = World();
        var before = Snapshot(world);
        var families = Families(world);
        families[1] = families[1] with { Files = [new GeneratedFile("../bad.g.cs", "bad")] };
        var exception = await Assert.That(() => new GeneratedFamilyPublisher(world.CakeContext).PublishAsync(families, "/temporary/backups", CancellationToken.None))
            .Throws<CakeException>();
        await Assert.That(exception!.Message).IsEqualTo("Refusing unsafe or duplicate generated file path: '../bad.g.cs'.");
        await Assert.That(Snapshot(world)).IsEquivalentTo(before, CollectionOrdering.Matching);
    }

    private static FakeCakeWorld World() => FakeCakeWorld.CreateLinux()
        .WithBinaryFile("one/Generated/old.bin", [0, 255, 13, 10])
        .WithTextFile("two/Generated/sentinel.txt", "two sentinel")
        .WithTextFile("other/Generated/keep.txt", "untouched");

    private static GeneratedFamilySnapshot[] Families(FakeCakeWorld world) =>
    [
        new(world.RepoRoot.Combine("one"), world.RepoRoot.Combine("one/Generated"), [new GeneratedFile("new.g.cs", "new one\n")]),
        new(world.RepoRoot.Combine("two"), world.RepoRoot.Combine("two/Generated"), [new GeneratedFile("new.g.cs", "new two\n")]),
    ];

    private static string[] Snapshot(FakeCakeWorld world) => [.. world.FileSystem
        .GetDirectory(world.RepoRoot)
        .GetFiles("*", SearchScope.Recursive)
        .OrderBy(file => file.Path.FullPath, StringComparer.Ordinal)
        .Select(file => file.Path.FullPath + "|" + Convert.ToHexString(world.FileSystem.GetFile(file.Path).GetBinaryContent()))];

}
