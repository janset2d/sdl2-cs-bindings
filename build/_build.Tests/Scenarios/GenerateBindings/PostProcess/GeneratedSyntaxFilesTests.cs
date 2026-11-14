using System.Text;
using Build.Targets.GenerateBindings.PostProcess;
using Build.Tests.Fixtures;
using Cake.Core.IO;

namespace Build.Tests.Scenarios.GenerateBindings.PostProcess;

public sealed class GeneratedSyntaxFilesTests
{
    [Test]
    public async Task WriteAllTextLfAsync_Should_Write_Lf_Without_A_Bom()
    {
        var world = PostProcessFixture.World();
        var file = world.RepoRoot.CombineWithFilePath("lf.txt");
        await new GeneratedSyntaxFiles(world.CakeContext).WriteAllTextLfAsync(file, "alpha\r\nbeta\rgamma\n");
        using var stream = world.FileSystem.GetFile(file).OpenRead();
        using var bytes = new MemoryStream();
        await stream.CopyToAsync(bytes);
        await Assert.That(Convert.ToHexString(bytes.ToArray())).IsEqualTo(Convert.ToHexString(Encoding.UTF8.GetBytes("alpha\nbeta\ngamma\n")));
    }

    [Test]
    public async Task RewriteAsync_Should_Honor_The_Explicit_Output_Directory_And_Write_Lf()
    {
        var world = PostProcessFixture.World().WithTextFile("input/Generated/Modern/SDL.g.cs", PostProcessFixture.Source("source"));
        var input = world.RepoRoot.Combine("input/Generated/Modern");
        var output = world.RepoRoot.Combine("output");
        var rewriter = new DllImportToLibraryImportRewriter();
        var counts = await new GeneratedSyntaxFiles(world.CakeContext).RewriteAsync(input, output, rewriter, () => rewriter.AnyChanges, rewriter.Reset);
        await Assert.That(counts.Processed).IsEqualTo(1);
        await Assert.That(counts.Transformed).IsEqualTo(1);
        var content = world.ReadAllText("output/SDL.g.cs");
        await Assert.That(content).Contains("LibraryImport");
        await Assert.That(content).DoesNotContain("\r");
        await Assert.That(content).DoesNotContain("\uFEFF");
    }

    [Test]
    public async Task RewriteAsync_Should_Enforce_The_LibraryImport_Backend_Without_Restricting_Other_Passes()
    {
        var world = PostProcessFixture.World();
        var files = new GeneratedSyntaxFiles(world.CakeContext);
        var modern = world.RepoRoot.Combine("Generated/Modern");
        var compat = world.RepoRoot.Combine("Generated/Compat");
        var nested = modern.Combine("Nested");
        await Assert.That(DllImportToLibraryImportRewriter.ValidateInput(modern)).IsNull();

        foreach (var invalid in new[] { compat, nested })
        {
            var rewriter = new DllImportToLibraryImportRewriter();
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                files.RewriteAsync(invalid, invalid, rewriter, () => rewriter.AnyChanges, rewriter.Reset));
            await Assert.That(exception!.Message).IsEqualTo(
                "libraryimport postprocess only supports Generated/Modern input directories; received: " + invalid.FullPath);
        }

        world.WithTextFile("Generated/Compat/SDL.g.cs", PostProcessFixture.Source("varargsSource"));
        var strip = new StripVarargsRewriter();
        await files.RewriteAsync(compat, compat, strip, () => strip.AnyChanges, strip.Reset);
        await Assert.That(world.ReadAllText("Generated/Compat/SDL.g.cs")).IsEqualTo(PostProcessFixture.Source("varargsExpected"));
    }
}
