using Build.Data.BindingGeneration;
using Build.Data.BindingGeneration.Models;
using Build.Targets.GenerateBindings.Oracle;
using Build.Tests.Fixtures;
using TUnit.Assertions.Enums;

namespace Build.Tests.Scenarios.GenerateBindings;

public sealed class OracleEvidenceCollectorTests
{
    [Test]
    public async Task BuildReportAsync_Should_Load_Selected_Families_In_Order_And_Preserve_Missing_And_Inapplicable_Evidence()
    {
        var world = FakeCakeWorld.CreateLinux()
            .WithManifestObject(ManifestFixture.RealManifest)
            .WithTextFile(OracleFamilyFixture.Sdl2Core.ClangSharpCompatRelativePath + "/sample.cs",
                FixtureLoader.Load("BindingGeneration/Oracle/entrypoint-import.cs.txt"));
        var collector = CreateCollector(world);
        var report = await collector.BuildReportAsync(Profiles("sdl2-mixer", "sdl2-gfx", "sdl2-ttf", "sdl2-image", "sdl2-core"));

        await Assert.That(report.RepoRoot).IsEqualTo(world.RepoRoot.FullPath);
        await Assert.That(report.Families.Select(family => family.Config.FamilyId))
            .IsEquivalentTo(["sdl2-mixer", "sdl2-gfx", "sdl2-ttf", "sdl2-image", "sdl2-core"], CollectionOrdering.Matching);
        var core = report.Families.Single(family => family.Config.FamilyId == "sdl2-core");

        await Assert.That(core.ClangSharpCompat.Evidence.Functions.Single().NativeEntryPoint).IsEqualTo("SDL_Init");
        await Assert.That(core.RequiredSurface.RequiredFunctions).Contains("SDL_Init");
        await Assert.That(core.Dynapi.Status).IsEqualTo(SourceStatus.Missing);
        await Assert.That(core.Sdl2Cs.Status).IsEqualTo(SourceStatus.Missing);
        await Assert.That(core.RawAbiChecks.Select(check => check.CheckId))
            .IsEquivalentTo(["required-function-missing", "required-constant-missing"], CollectionOrdering.Matching);

        foreach (var satellite in report.Families.Where(family => family.Config.FamilyId != "sdl2-core"))
        {
            await Assert.That(satellite.Dynapi.Status).IsEqualTo(SourceStatus.NotApplicable);
            await Assert.That(satellite.RequiredSurface.Status).IsEqualTo(SourceStatus.NotApplicable);
        }
    }

    [Test]
    public async Task WriteReportAsync_Should_Create_The_Report_Through_Cake_Without_Changing_Source_Files()
    {
        var source = FixtureLoader.Load("BindingGeneration/Oracle/entrypoint-import.cs.txt");
        var sourcePath = OracleFamilyFixture.Sdl2Core.ClangSharpCompatRelativePath + "/sample.cs";
        var world = FakeCakeWorld.CreateLinux().WithTextFile(sourcePath, source);
        var collector = CreateCollector(world);
        var report = await collector.BuildReportAsync(Profiles("sdl2-core"));

        await collector.WriteReportAsync(report);

        var markdown = world.ReadAllText("artifacts/binding-generation/oracle-evidence.md");

        await Assert.That(markdown).StartsWith("# ClangSharp Oracle Evidence\n\n");
        await Assert.That(markdown).Contains("| `SDL_Init` | yes | no | no |");
        await Assert.That(markdown).Contains("Evidence Missing: SDL2 Dynapi");
        await Assert.That(markdown).DoesNotContain('\r');
        await Assert.That(world.ReadAllText(sourcePath)).IsEqualTo(source);
    }

    private static OracleEvidenceCollector CreateCollector(FakeCakeWorld world)
    {
        var paths = world.CreateBuildContext().Paths;
        return new OracleEvidenceCollector(world.CakeContext, paths, new DynapiManifestRepository(world.CakeContext, paths));
    }

    private static BindingGenerationConfig[] Profiles(params string[] ids) => [.. ids.Select(id =>
        OracleFamilyFixture.Profile(id) with
        {
            RequiredFunctions = OracleFamilyFixture.RequiredProfile.RequiredFunctions,
            RequiredConstants = OracleFamilyFixture.RequiredProfile.RequiredConstants
        })];
}
