using Build.Targets.GenerateBindings.Emit;

namespace Build.Tests.Unit.Targets.GenerateBindings.Emit;

public sealed class GeneratedFileSetSnapshotTests
{
    [Test]
    public Task Emit_Should_Match_Rich_Model_Generated_File_Set()
    {
        var fileSet = new BindingEmitter().Emit(
            BindingModelData.ModelWithRichParseViewEvidence(),
            new BindingEmissionOptions("SDL2", "SDL"));

        var snapshot = fileSet.Files
            .OrderBy(file => file.RelativePath, StringComparer.Ordinal)
            .Select(file => new GeneratedFileSnapshot(file.RelativePath, file.Content.Replace("\r\n", "\n", StringComparison.Ordinal)))
            .ToArray();

        return Verify(snapshot);
    }

    private sealed record GeneratedFileSnapshot(string RelativePath, string Content);
}
