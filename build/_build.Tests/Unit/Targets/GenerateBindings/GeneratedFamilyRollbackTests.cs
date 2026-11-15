using Build.Data.BindingGeneration.Models;
using Build.Targets.GenerateBindings;
using Build.Tests.Fixtures;
using Cake.Core.IO;
using Cake.Testing;
using NSubstitute;
using TUnit.Assertions.Enums;

namespace Build.Tests.Unit.Targets.GenerateBindings;

public sealed class GeneratedFamilyRollbackTests
{
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task PublishAsync_Should_Delete_Backups_After_Publication_Or_Uninterrupted_Rollback(bool rollback)
    {
        var world = World();
        using var cancellation = new CancellationTokenSource();
        var cancellationCompletion = Task.CompletedTask;
        var system = Substitute.For<IFileSystem>();
        system.GetFile(Arg.Any<FilePath>()).Returns(call =>
        {
            var path = call.Arg<FilePath>();

            if (rollback && path.FullPath == "/repo/three/Generated/new.g.cs")
            {
                throw new IOException("replacement failed");
            }

            return world.FileSystem.GetFile(path);
        });
        system.GetDirectory(Arg.Any<DirectoryPath>()).Returns(call =>
        {
            var real = world.FileSystem.GetDirectory(call.Arg<DirectoryPath>());
            return rollback && real.Path.FullPath == "/temporary/backups/1" && real.Exists
                ? CopySource(real, _ => cancellationCompletion = cancellation.CancelAsync())
                : real;
        });
        world.CakeContext.FileSystem.Returns(system);
        var publisher = new GeneratedFamilyPublisher(world.CakeContext);

        try
        {
            if (rollback)
            {
                var error = await Assert.That(() => publisher.PublishAsync(Families(world), "/temporary/backups", cancellation.Token)).Throws<IOException>();
                await Assert.That(error!.Message).IsEqualTo("replacement failed");
                await Assert.That(world.FileExists("one/Generated/sentinel.bin")).IsTrue();
                await Assert.That(world.ReadAllText("two/Generated/sentinel.txt")).IsEqualTo("two before");
                await Assert.That(world.ReadAllText("three/Generated/sentinel.txt")).IsEqualTo("three before");
                await Assert.That(world.FileExists("one/Generated/new.g.cs")).IsFalse();
                await Assert.That(world.FileSystem.GetDirectory("/repo/one/Generated/nested").Exists).IsFalse();
                await Assert.That(cancellation.IsCancellationRequested).IsTrue();
            }
            else
            {
                await publisher.PublishAsync(Families(world), "/temporary/backups", cancellation.Token);
                await Assert.That(world.ReadAllText("one/Generated/new.g.cs")).IsEqualTo("one after");
                await Assert.That(world.ReadAllText("one/Generated/nested/new.g.cs")).IsEqualTo("nested after");
                await Assert.That(world.ReadAllText("two/Generated/new.g.cs")).IsEqualTo("two after");
                await Assert.That(world.ReadAllText("three/Generated/new.g.cs")).IsEqualTo("three after");
            }
        }
        finally
        {
            await cancellationCompletion;
        }

        await Assert.That(world.FileSystem.GetDirectory("/temporary/backups").Exists).IsFalse();
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task PublishAsync_Should_Keep_Backups_And_Restore_Other_Families_When_A_Restore_Copy_Fails(bool cancelRestore)
    {
        var world = World();
        var families = Families(world);
        var rollback = false;
        var system = Substitute.For<IFileSystem>();
        system.GetFile(Arg.Any<FilePath>()).Returns(call =>
        {
            var path = call.Arg<FilePath>();

            if (path.FullPath == "/repo/three/Generated/new.g.cs")
            {
                rollback = true;
                throw new IOException("replacement failed");
            }

            return world.FileSystem.GetFile(path);
        });
        system.GetDirectory(Arg.Any<DirectoryPath>()).Returns(call =>
        {
            var real = world.FileSystem.GetDirectory(call.Arg<DirectoryPath>());
            return rollback && real.Path.FullPath == "/temporary/backups/1"
                ? CopySource(real, _ => throw (cancelRestore ? new OperationCanceledException("restore cancelled") : new IOException("restore copy failed")))
                : real;
        });
        world.CakeContext.FileSystem.Returns(system);

        var publisher = new GeneratedFamilyPublisher(world.CakeContext);
        var error = await Assert.That(() => publisher.PublishAsync(families, "/temporary/backups", CancellationToken.None)).Throws<Exception>();

        await Assert.That(world.FileExists("one/Generated/sentinel.bin")).IsTrue();
        await Assert.That(world.FileSystem.GetFile("/repo/one/Generated/sentinel.bin").GetBinaryContent())
            .IsEquivalentTo(new byte[] { 0, 255, 13, 10 }, CollectionOrdering.Matching);
        await Assert.That(world.ReadAllText("three/Generated/sentinel.txt")).IsEqualTo("three before");
        await Assert.That(world.FileExists("two/Generated/new.g.cs")).IsTrue();
        await Assert.That(world.ReadAllText("two/Generated/new.g.cs")).IsEqualTo("two after");
        await Assert.That(world.FileExists("one/Generated/new.g.cs")).IsFalse();
        await Assert.That(world.FileExists("three/Generated/new.g.cs")).IsFalse();
        await Assert.That(world.FileSystem.GetDirectory("/repo/two/Generated").Exists).IsTrue();
        await Assert.That(world.FileSystem.GetFile("/temporary/backups/1/sentinel.txt").GetTextContent()).IsEqualTo("two before");
        await Assert.That(world.FileSystem.GetFile("/temporary/backups/0/sentinel.bin").GetBinaryContent())
            .IsEquivalentTo(new byte[] { 0, 255, 13, 10 }, CollectionOrdering.Matching);
        await Assert.That(world.FileSystem.GetFile("/temporary/backups/2/sentinel.txt").GetTextContent()).IsEqualTo("three before");
        await Assert.That(error!.Message).Contains("/temporary/backups/1");
        await Assert.That(error.Message).Contains(cancelRestore ? "restore cancelled" : "restore copy failed");
        await Assert.That(error.ToString()).Contains("replacement failed");
    }

    private static IDirectory CopySource(FakeDirectory real, Action<FilePath> beforeCopy)
    {
        var directory = Substitute.For<IDirectory>();
        directory.Path.Returns(real.Path);
        directory.Exists.Returns(real.Exists);
        directory
            .GetDirectories(Arg.Any<string>(), Arg.Any<SearchScope>())
            .Returns(call => real.GetDirectories(call.Arg<string>(), call.Arg<SearchScope>()));
        directory.GetFiles(Arg.Any<string>(), Arg.Any<SearchScope>()).Returns(call =>
            real.GetFiles(call.Arg<string>(), call.Arg<SearchScope>()).Select(source =>
            {
                var file = Substitute.For<IFile>();
                file.Path.Returns(source.Path);
                file.When(copy => copy.Copy(Arg.Any<FilePath>(), Arg.Any<bool>())).Do(copy =>
                {
                    beforeCopy(source.Path);
                    source.Copy(copy.Arg<FilePath>(), copy.Arg<bool>());
                });
                return file;
            }));
        return directory;
    }

    private static FakeCakeWorld World() => FakeCakeWorld.CreateLinux()
        .WithBinaryFile("one/Generated/sentinel.bin", [0, 255, 13, 10])
        .WithTextFile("two/Generated/sentinel.txt", "two before")
        .WithTextFile("three/Generated/sentinel.txt", "three before");

    private static GeneratedFamilySnapshot[] Families(FakeCakeWorld world) =>
    [
        new(world.RepoRoot.Combine("one"), world.RepoRoot.Combine("one/Generated"),
            [new GeneratedFile("new.g.cs", "one after"), new GeneratedFile("nested/new.g.cs", "nested after")]),
        new(world.RepoRoot.Combine("two"), world.RepoRoot.Combine("two/Generated"), [new GeneratedFile("new.g.cs", "two after")]),
        new(world.RepoRoot.Combine("three"), world.RepoRoot.Combine("three/Generated"), [new GeneratedFile("new.g.cs", "three after")]),
    ];
}
