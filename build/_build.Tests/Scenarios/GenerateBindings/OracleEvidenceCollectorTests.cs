using Build.Data.BindingGeneration;
using Build.Data.BindingGeneration.Models;
using Build.Targets.GenerateBindings.Oracle;
using Build.Tests.Fixtures;
using TUnit.Assertions.Enums;

namespace Build.Tests.Scenarios.GenerateBindings;

public sealed class OracleEvidenceCollectorTests
{
    [Test]
    public async Task BuildReportAsync_Should_Label_Required_Findings_With_The_Configured_Surface_Header()
    {
        var world = FakeCakeWorld.CreateLinux();
        var profile = OracleFamilyFixture.Profile("sdl2-core") with { RequiredSurfaceHeader = "umbrella.h" };
        var report = await CreateCollector(world).BuildReportAsync([profile]);
        var markdown = MarkdownReportRenderer.Render(report.Value);
        await Assert.That(markdown).Contains("- Missing umbrella.h required functions (`required-function-missing`)");
        await Assert.That(markdown).Contains("- Missing umbrella.h required constants (`required-constant-missing`)");
        await Assert.That(markdown).Contains("Evidence Missing: Missing umbrella.h required functions");
    }

    [Test]
    public async Task BuildReportAsync_Should_Use_The_Profile_Peer_Name_And_Source_File()
    {
        var world = FakeCakeWorld.CreateLinux()
            .WithTextFile("external/sdl2-cs/src/custom.cs", FixtureLoader.Load("BindingGeneration/Oracle/entrypoint-import.cs.txt"));
        var profile = OracleFamilyFixture.Profile("sdl2-image") with
        {
            PeerEvidence = new PeerEvidenceConfig { DisplayName = "Custom peer", Sdl2CsFile = "custom.cs" },
        };
        var report = await CreateCollector(world).BuildReportAsync([profile]);
        var family = report.Value.Families.Single();
        await Assert.That(family.Config.DisplayName).IsEqualTo("Custom peer");
        await Assert.That(family.Config.Sdl2CsRelativePath).IsEqualTo("external/sdl2-cs/src/custom.cs");
        await Assert.That(family.Sdl2Cs.Status).IsEqualTo(SourceStatus.Present);
        await Assert.That(family.Sdl2Cs.Evidence.Functions.Select(function => function.NativeEntryPoint))
            .IsEquivalentTo(["SDL_Init"], CollectionOrdering.Matching);
    }

    [Test]
    [Arguments(ManifestResolutionErrorKind.Ambiguous)]
    [Arguments(ManifestResolutionErrorKind.ParseFailure)]
    public async Task BuildReportAsync_Should_Return_Resolution_Error_Without_Throwing(ManifestResolutionErrorKind kind)
    {
        var world = FakeCakeWorld.CreateLinux();
        var error = new ManifestResolutionError(kind, "operator diagnosis");
        var collector = new OracleEvidenceCollector(world.CakeContext, world.CreateBuildContext().Paths,
            new FakeDynapiManifestRepository(null, error));

        await Assert.That(async () => await collector.BuildReportAsync(Profiles("sdl2-core"))).ThrowsNothing();
        var result = await collector.BuildReportAsync(Profiles("sdl2-core"));
        await Assert.That(result.IsFailure).IsTrue();
        await Assert.That(result.Error).IsEqualTo(error);
    }

    [Test]
    public async Task BuildReportAsync_Should_Report_Missing_Evidence_By_Kind_Regardless_Of_Reason()
    {
        var world = FakeCakeWorld.CreateLinux();
        var collector = new OracleEvidenceCollector(world.CakeContext, world.CreateBuildContext().Paths,
            new FakeDynapiManifestRepository(null, new ManifestResolutionError(ManifestResolutionErrorKind.NotFound, "different diagnostic")));

        var result = await collector.BuildReportAsync(Profiles("sdl2-core"));

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value.Families.Single().Dynapi.Status).IsEqualTo(SourceStatus.Missing);
        var markdown = MarkdownReportRenderer.Render(result.Value);
        await Assert.That(markdown).Contains("| Stage1Generator | Evidence Missing | Evidence Missing |");
        await Assert.That(markdown).Contains("| Stage2Strict | Evidence Missing | Evidence Missing |");
    }

    [Test]
    public async Task BuildReportAsync_Should_Load_Selected_Families_In_Order_And_Preserve_Missing_And_Inapplicable_Evidence()
    {
        var world = FakeCakeWorld.CreateLinux()
            .WithManifestObject(ManifestFixture.RealManifest)
            .WithTextFile(OracleFamilyFixture.Sdl2Core.ClangSharpCompatRelativePath + "/sample.cs",
                FixtureLoader.Load("BindingGeneration/Oracle/entrypoint-import.cs.txt"));
        var collector = CreateCollector(world);
        var result = await collector.BuildReportAsync(Profiles("sdl2-mixer", "sdl2-gfx", "sdl2-ttf", "sdl2-image", "sdl2-core"));
        await Assert.That(result.IsSuccess).IsTrue();
        var report = result.Value;

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
        var result = await collector.BuildReportAsync(Profiles("sdl2-core"));
        await Assert.That(result.IsSuccess).IsTrue();
        var report = result.Value;

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
