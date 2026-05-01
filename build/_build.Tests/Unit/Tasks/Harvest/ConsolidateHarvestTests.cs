using System.Text.Json;
using Build.Application.Harvesting;
using Build.Domain.Harvesting;
using Build.Domain.Harvesting.Models;
using Build.Tests.Fixtures;
using Build.Tests.Fixtures.Seeders;
using Build.Tasks.Harvest;
using Cake.Core;
using NSubstitute;

namespace Build.Tests.Unit.Tasks.Harvest;

public class ConsolidateHarvestTests
{
    private static JsonSerializerOptions JsonOptions => HarvestJsonContract.Options;

    [Test]
    public async Task RunAsync_Should_Calculate_Correct_Summary_For_Mixed_Rids()
    {
        var manifest = await RunConsolidationForStatusesAsync(
            "SDL2",
            CreateSuccessStatus("SDL2", "win-x64", "x64-windows-hybrid"),
            CreateSuccessStatus("SDL2", "linux-x64", "x64-linux-hybrid"),
            CreateFailedStatus("SDL2", "osx-arm64", "arm64-osx-dynamic", "Build timeout"));

        await Assert.That(manifest.LibraryName).IsEqualTo("SDL2");
        await Assert.That(manifest.Summary.TotalRids).IsEqualTo(3);
        await Assert.That(manifest.Summary.SuccessfulRids).IsEqualTo(2);
        await Assert.That(manifest.Summary.FailedRids).IsEqualTo(1);
    }

    [Test]
    public async Task RunAsync_Should_Calculate_Correct_Success_Rate_For_All_Success_Rids()
    {
        var manifest = await RunConsolidationForStatusesAsync(
            "SDL2_image",
            CreateSuccessStatus("SDL2_image", "win-x64", "x64-windows-hybrid"),
            CreateSuccessStatus("SDL2_image", "linux-x64", "x64-linux-hybrid"),
            CreateSuccessStatus("SDL2_image", "osx-x64", "x64-osx-hybrid"));

        await Assert.That(manifest.Summary.SuccessRate).IsEqualTo(1.0);
        await Assert.That(manifest.Summary.SuccessfulRids).IsEqualTo(3);
        await Assert.That(manifest.Summary.FailedRids).IsEqualTo(0);
    }

    [Test]
    public async Task RunAsync_Should_Calculate_Correct_Summary_For_All_Failed_Rids()
    {
        var manifest = await RunConsolidationForStatusesAsync(
            "SDL2_mixer",
            CreateFailedStatus("SDL2_mixer", "win-x64", "x64-windows-hybrid", "vcpkg install failed"),
            CreateFailedStatus("SDL2_mixer", "linux-x64", "x64-linux-hybrid", "ldd not found"));

        await Assert.That(manifest.Summary.SuccessRate).IsEqualTo(0.0);
        await Assert.That(manifest.Summary.SuccessfulRids).IsEqualTo(0);
        await Assert.That(manifest.Summary.FailedRids).IsEqualTo(2);
    }

    [Test]
    public async Task RidHarvestStatus_Should_Serialize_And_Deserialize_Correctly()
    {
        var original = CreateSuccessStatus("SDL2", "win-x64", "x64-windows-hybrid");
        var json = JsonSerializer.Serialize(original, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<RidHarvestStatus>(json, JsonOptions);

        await Assert.That(deserialized).IsNotNull();
        await Assert.That(deserialized!.LibraryName).IsEqualTo("SDL2");
        await Assert.That(deserialized.Rid).IsEqualTo("win-x64");
        await Assert.That(deserialized.Triplet).IsEqualTo("x64-windows-hybrid");
        await Assert.That(deserialized.Success).IsTrue();
        await Assert.That(deserialized.ErrorMessage).IsNull();
    }

    [Test]
    public async Task RidHarvestStatus_Should_Preserve_Error_Message_On_Failure()
    {
        var original = CreateFailedStatus("SDL2", "linux-arm64", "arm64-linux-dynamic", "Package not found");
        var json = JsonSerializer.Serialize(original, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<RidHarvestStatus>(json, JsonOptions);

        await Assert.That(deserialized).IsNotNull();
        await Assert.That(deserialized!.Success).IsFalse();
        await Assert.That(deserialized.ErrorMessage).IsEqualTo("Package not found");
        await Assert.That(deserialized.Statistics).IsNull();
    }

    [Test]
    public async Task HarvestManifest_Should_Serialize_And_Deserialize_Roundtrip()
    {
        var statuses = new List<RidHarvestStatus>
        {
            CreateSuccessStatus("SDL2", "win-x64", "x64-windows-hybrid"),
            CreateFailedStatus("SDL2", "osx-arm64", "arm64-osx-dynamic", "timeout"),
        };

        var manifest = new HarvestManifest
        {
            LibraryName = "SDL2",
            GeneratedTimestamp = DateTimeOffset.UtcNow,
            Rids = statuses.AsReadOnly(),
            Summary = new HarvestSummary
            {
                TotalRids = 2,
                SuccessfulRids = 1,
                FailedRids = 1,
                SuccessRate = 0.5,
            },
        };

        var json = JsonSerializer.Serialize(manifest, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<HarvestManifest>(json, JsonOptions);

        await Assert.That(deserialized).IsNotNull();
        await Assert.That(deserialized!.LibraryName).IsEqualTo("SDL2");
        await Assert.That(deserialized.Rids.Count).IsEqualTo(2);
        await Assert.That(deserialized.Summary.TotalRids).IsEqualTo(2);
    }

    [Test]
    public async Task RunAsync_Should_Generate_Harvest_Manifest_And_Summary_From_Rid_Status_Files()
    {
        var repo = new FakeRepoBuilder(FakeRepoPlatform.Windows)
            .WithHarvestStatus("SDL2", "win-x64", CreateSuccessStatus("SDL2", "win-x64", "x64-windows-hybrid"))
            .WithHarvestStatus("SDL2", "linux-x64", CreateFailedStatus("SDL2", "linux-x64", "x64-linux-hybrid", "ldd failed"))
            .BuildContextWithHandles();

        var task = new ConsolidateHarvestTask(new ConsolidateHarvestTaskRunner());

        await task.RunAsync(repo.BuildContext);

        const string manifestPath = "artifacts/harvest_output/SDL2/harvest-manifest.json";
        const string summaryPath = "artifacts/harvest_output/SDL2/harvest-summary.json";

        await Assert.That(repo.Exists(manifestPath)).IsTrue();
        await Assert.That(repo.Exists(summaryPath)).IsTrue();

        var manifestJson = await repo.ReadAllTextAsync(manifestPath);
        var manifest = JsonSerializer.Deserialize<HarvestManifest>(manifestJson, JsonOptions);

        await Assert.That(manifest).IsNotNull();
        await Assert.That(manifest!.LibraryName).IsEqualTo("SDL2");
        await Assert.That(manifest.Rids.Count).IsEqualTo(2);
        await Assert.That(manifest.Summary.TotalRids).IsEqualTo(2);
        await Assert.That(manifest.Summary.SuccessfulRids).IsEqualTo(1);
        await Assert.That(manifest.Summary.FailedRids).IsEqualTo(1);
        await Assert.That(manifest.Summary.SuccessRate).IsEqualTo(0.5);
    }

    [Test]
    public async Task RunAsync_Should_Fail_When_Any_Rid_Status_File_Is_Invalid()
    {
        var repo = new FakeRepoBuilder(FakeRepoPlatform.Windows)
            .WithHarvestStatus("SDL2_image", "win-x64", CreateSuccessStatus("SDL2_image", "win-x64", "x64-windows-hybrid"))
            .WithTextFile("artifacts/harvest_output/SDL2_image/rid-status/corrupt.json", "{ this is not valid json")
            .BuildContextWithHandles();

        var task = new ConsolidateHarvestTask(new ConsolidateHarvestTaskRunner());

        var thrown = await Assert.That(() => task.RunAsync(repo.BuildContext)).Throws<CakeException>();
        await Assert.That(thrown!.Message).Contains("corrupt.json");
        await Assert.That(thrown.Message).Contains("false-green compliance surface");

        const string manifestPath = "artifacts/harvest_output/SDL2_image/harvest-manifest.json";
        await Assert.That(repo.Exists(manifestPath)).IsFalse();
    }

    [Test]
    public async Task RunAsync_Should_Fail_When_All_Rid_Status_Files_Are_Invalid()
    {
        var repo = new FakeRepoBuilder(FakeRepoPlatform.Windows)
            .WithTextFile("artifacts/harvest_output/SDL2_mixer/rid-status/one.json", "{ invalid")
            .WithTextFile("artifacts/harvest_output/SDL2_mixer/rid-status/two.json", "also invalid")
            .BuildContextWithHandles();

        var task = new ConsolidateHarvestTask(new ConsolidateHarvestTaskRunner());

        var thrown = await Assert.That(() => task.RunAsync(repo.BuildContext)).Throws<CakeException>();
        await Assert.That(thrown!.Message).Contains("one.json");
        await Assert.That(thrown.Message).Contains("two.json");

        const string manifestPath = "artifacts/harvest_output/SDL2_mixer/harvest-manifest.json";
        const string summaryPath = "artifacts/harvest_output/SDL2_mixer/harvest-summary.json";

        await Assert.That(repo.Exists(manifestPath)).IsFalse();
        await Assert.That(repo.Exists(summaryPath)).IsFalse();
    }

    [Test]
    public async Task RunAsync_Should_Union_Per_Rid_Licenses_Into_Consolidated_Output()
    {
        // Post-H1: Consolidate unions per-RID license evidence into licenses/_consolidated/
        // so PackageTask consumes a single deduplicated set regardless of contributing RIDs.
        var repo = new FakeRepoBuilder(FakeRepoPlatform.Windows)
            .Seed(new HarvestOutputSeeder("SDL2", "win-x64", "x64-windows-hybrid")
                .WithPrimary("SDL2.dll", "windows-sdl2")
                .WithLicense("sdl2", content: "sdl2-copyright")
                .WithLicense("zlib", content: "zlib-copyright"))
            .Seed(new HarvestOutputSeeder("SDL2", "linux-x64", "x64-linux-hybrid")
                .WithPrimary("libSDL2.so", "linux-sdl2")
                .WithLicense("sdl2", content: "sdl2-copyright")
                .WithLicense("zlib", content: "zlib-copyright")
                .WithLicense("libasound", content: "alsa-copyright"))
            .BuildContextWithHandles();

        var task = new ConsolidateHarvestTask(new ConsolidateHarvestTaskRunner());
        await task.RunAsync(repo.BuildContext);

        await Assert.That(repo.Exists("artifacts/harvest_output/SDL2/licenses/_consolidated/sdl2/copyright")).IsTrue();
        await Assert.That(repo.Exists("artifacts/harvest_output/SDL2/licenses/_consolidated/zlib/copyright")).IsTrue();
        // Linux-only transitive dep survives because Linux's per-RID evidence contributed it.
        await Assert.That(repo.Exists("artifacts/harvest_output/SDL2/licenses/_consolidated/libasound/copyright")).IsTrue();
    }

    [Test]
    public async Task RunAsync_Should_Deduplicate_Identical_License_Content_Across_Rids()
    {
        // Identical SHA across RIDs → single canonical file under _consolidated/, no per-RID suffix.
        var repo = new FakeRepoBuilder(FakeRepoPlatform.Windows)
            .Seed(new HarvestOutputSeeder("SDL2_image", "win-x64", "x64-windows-hybrid")
                .WithPrimary("SDL2_image.dll", "win-image")
                .WithLicense("libpng", content: "libpng license text v1"))
            .Seed(new HarvestOutputSeeder("SDL2_image", "linux-x64", "x64-linux-hybrid")
                .WithPrimary("libSDL2_image.so", "linux-image")
                .WithLicense("libpng", content: "libpng license text v1"))
            .BuildContextWithHandles();

        var task = new ConsolidateHarvestTask(new ConsolidateHarvestTaskRunner());
        await task.RunAsync(repo.BuildContext);

        await Assert.That(repo.Exists("artifacts/harvest_output/SDL2_image/licenses/_consolidated/libpng/copyright")).IsTrue();
        // No per-RID variants when content matches.
        await Assert.That(repo.Exists("artifacts/harvest_output/SDL2_image/licenses/_consolidated/libpng/copyright.win-x64")).IsFalse();
        await Assert.That(repo.Exists("artifacts/harvest_output/SDL2_image/licenses/_consolidated/libpng/copyright.linux-x64")).IsFalse();

        var consolidatedContent = await repo.ReadAllTextAsync("artifacts/harvest_output/SDL2_image/licenses/_consolidated/libpng/copyright");
        await Assert.That(consolidatedContent).IsEqualTo("libpng license text v1");
    }

    [Test]
    public async Task RunAsync_Should_Emit_Per_Rid_Variants_When_License_Content_Diverges()
    {
        // Divergent SHA across RIDs → keep both, suffixed by RID; canonical unsuffixed file
        // is intentionally omitted so no RID's attribution is silently chosen as the winner.
        var repo = new FakeRepoBuilder(FakeRepoPlatform.Windows)
            .Seed(new HarvestOutputSeeder("SDL2_mixer", "win-x64", "x64-windows-hybrid")
                .WithPrimary("SDL2_mixer.dll", "win-mixer")
                .WithLicense("libvorbis", content: "libvorbis v1.3.7 copyright"))
            .Seed(new HarvestOutputSeeder("SDL2_mixer", "linux-x64", "x64-linux-hybrid")
                .WithPrimary("libSDL2_mixer.so", "linux-mixer")
                .WithLicense("libvorbis", content: "libvorbis v1.3.8 copyright"))
            .BuildContextWithHandles();

        var task = new ConsolidateHarvestTask(new ConsolidateHarvestTaskRunner());
        await task.RunAsync(repo.BuildContext);

        // Per-RID variants exist, canonical unsuffixed does not.
        await Assert.That(repo.Exists("artifacts/harvest_output/SDL2_mixer/licenses/_consolidated/libvorbis/copyright.win-x64")).IsTrue();
        await Assert.That(repo.Exists("artifacts/harvest_output/SDL2_mixer/licenses/_consolidated/libvorbis/copyright.linux-x64")).IsTrue();
        await Assert.That(repo.Exists("artifacts/harvest_output/SDL2_mixer/licenses/_consolidated/libvorbis/copyright")).IsFalse();

        var winVariant = await repo.ReadAllTextAsync("artifacts/harvest_output/SDL2_mixer/licenses/_consolidated/libvorbis/copyright.win-x64");
        var linuxVariant = await repo.ReadAllTextAsync("artifacts/harvest_output/SDL2_mixer/licenses/_consolidated/libvorbis/copyright.linux-x64");
        await Assert.That(winVariant).IsEqualTo("libvorbis v1.3.7 copyright");
        await Assert.That(linuxVariant).IsEqualTo("libvorbis v1.3.8 copyright");
    }

    [Test]
    public async Task RunAsync_Should_Preserve_File_Extension_In_Per_Rid_Variants()
    {
        // Locks the Cake FilePath.GetFilenameWithoutExtension / GetExtension path used by
        // WriteConsolidatedEntryAsync: a divergent 'LICENSE.md' must land as
        // 'LICENSE.<rid>.md', not 'LICENSE.md.<rid>' or 'LICENSE.<rid>'.
        var repo = new FakeRepoBuilder(FakeRepoPlatform.Windows)
            .Seed(new HarvestOutputSeeder("SDL2_gfx", "win-x64", "x64-windows-hybrid")
                .WithPrimary("SDL2_gfx.dll", "win-gfx")
                .WithLicense("libwebp", fileName: "LICENSE.md", content: "webp apache-2.0 text"))
            .Seed(new HarvestOutputSeeder("SDL2_gfx", "linux-x64", "x64-linux-hybrid")
                .WithPrimary("libSDL2_gfx.so", "linux-gfx")
                .WithLicense("libwebp", fileName: "LICENSE.md", content: "webp apache-2.0 text with linux-only footer"))
            .BuildContextWithHandles();

        var task = new ConsolidateHarvestTask(new ConsolidateHarvestTaskRunner());
        await task.RunAsync(repo.BuildContext);

        await Assert.That(repo.Exists("artifacts/harvest_output/SDL2_gfx/licenses/_consolidated/libwebp/LICENSE.win-x64.md")).IsTrue();
        await Assert.That(repo.Exists("artifacts/harvest_output/SDL2_gfx/licenses/_consolidated/libwebp/LICENSE.linux-x64.md")).IsTrue();

        // Canonical unsuffixed is intentionally absent under divergence.
        await Assert.That(repo.Exists("artifacts/harvest_output/SDL2_gfx/licenses/_consolidated/libwebp/LICENSE.md")).IsFalse();
        // And no malformed variants like LICENSE.md.win-x64.
        await Assert.That(repo.Exists("artifacts/harvest_output/SDL2_gfx/licenses/_consolidated/libwebp/LICENSE.md.win-x64")).IsFalse();
    }

    [Test]
    public async Task RunAsync_Should_Stamp_Consolidation_State_Onto_Harvest_Manifest()
    {
        // H1: the harvest-manifest receipt carries a ConsolidationState recording the
        // license-union work. Pack-time gate in PackageTaskRunner depends on this section
        // being present and declaring a non-zero license entry count.
        var repo = new FakeRepoBuilder(FakeRepoPlatform.Windows)
            .Seed(new HarvestOutputSeeder("SDL2_mixer", "win-x64", "x64-windows-hybrid")
                .WithPrimary("SDL2_mixer.dll", "win-mixer")
                .WithLicense("libvorbis", content: "vorbis-license"))
            .Seed(new HarvestOutputSeeder("SDL2_mixer", "linux-x64", "x64-linux-hybrid")
                .WithPrimary("libSDL2_mixer.so", "linux-mixer")
                .WithLicense("libvorbis", content: "vorbis-license")
                .WithLicense("libopus", content: "opus-license"))
            .BuildContextWithHandles();

        var task = new ConsolidateHarvestTask(new ConsolidateHarvestTaskRunner());
        await task.RunAsync(repo.BuildContext);

        var manifestJson = await repo.ReadAllTextAsync("artifacts/harvest_output/SDL2_mixer/harvest-manifest.json");
        var manifest = JsonSerializer.Deserialize<HarvestManifest>(manifestJson, JsonOptions);

        await Assert.That(manifest).IsNotNull();
        await Assert.That(manifest!.Consolidation).IsNotNull();
        await Assert.That(manifest.Consolidation!.LicensesConsolidated).IsTrue();
        // Two unique (package, filename) entries: (libvorbis, copyright) + (libopus, copyright).
        await Assert.That(manifest.Consolidation.LicenseEntriesCount).IsEqualTo(2);
        await Assert.That(manifest.Consolidation.DivergentLicenses.Count).IsEqualTo(0);
    }

    [Test]
    public async Task RunAsync_Should_Record_Divergent_Licenses_In_Receipt()
    {
        // When license content differs across RIDs, the receipt carries an audit trail
        // of which (package, file, RID-set) triples diverged. Operators can open
        // harvest-manifest.json and see exactly what variance survived consolidation.
        var repo = new FakeRepoBuilder(FakeRepoPlatform.Windows)
            .Seed(new HarvestOutputSeeder("SDL2_image", "win-x64", "x64-windows-hybrid")
                .WithPrimary("SDL2_image.dll", "win-image")
                .WithLicense("libwebp", content: "webp v1.3 copyright"))
            .Seed(new HarvestOutputSeeder("SDL2_image", "linux-x64", "x64-linux-hybrid")
                .WithPrimary("libSDL2_image.so", "linux-image")
                .WithLicense("libwebp", content: "webp v1.4 copyright with trailing footer"))
            .BuildContextWithHandles();

        var task = new ConsolidateHarvestTask(new ConsolidateHarvestTaskRunner());
        await task.RunAsync(repo.BuildContext);

        var manifestJson = await repo.ReadAllTextAsync("artifacts/harvest_output/SDL2_image/harvest-manifest.json");
        var manifest = JsonSerializer.Deserialize<HarvestManifest>(manifestJson, JsonOptions);

        await Assert.That(manifest!.Consolidation!.LicensesConsolidated).IsTrue();
        await Assert.That(manifest.Consolidation.LicenseEntriesCount).IsEqualTo(1);
        await Assert.That(manifest.Consolidation.DivergentLicenses.Count).IsEqualTo(1);

        var divergence = manifest.Consolidation.DivergentLicenses[0];
        await Assert.That(divergence.Package).IsEqualTo("libwebp");
        await Assert.That(divergence.FileName).IsEqualTo("copyright");
        await Assert.That(divergence.Rids).IsEquivalentTo(["linux-x64", "win-x64"]);
    }

    [Test]
    public async Task RunAsync_Should_Record_Empty_Consolidation_State_When_No_Successful_Rids()
    {
        // Even the skipped-consolidation path must write a receipt — the receipt's
        // LicensesConsolidated=false + LicenseEntriesCount=0 is the signal Pack uses to
        // reject. Without the receipt at all, pre-H1 "missing Consolidation" gate fires;
        // without correct content, Pack could authorize a degenerate run.
        var repo = new FakeRepoBuilder(FakeRepoPlatform.Windows)
            .Seed(new HarvestOutputSeeder("SDL2_ttf", "win-x64", "x64-windows-hybrid")
                .WithPrimary("SDL2_ttf.dll", "win-ttf")
                .WithLicense("freetype", content: "freetype-text")
                .AsFailure("ldd scan failed"))
            .BuildContextWithHandles();

        var task = new ConsolidateHarvestTask(new ConsolidateHarvestTaskRunner());
        await task.RunAsync(repo.BuildContext);

        var manifestJson = await repo.ReadAllTextAsync("artifacts/harvest_output/SDL2_ttf/harvest-manifest.json");
        var manifest = JsonSerializer.Deserialize<HarvestManifest>(manifestJson, JsonOptions);

        await Assert.That(manifest!.Consolidation).IsNotNull();
        await Assert.That(manifest.Consolidation!.LicensesConsolidated).IsFalse();
        await Assert.That(manifest.Consolidation.LicenseEntriesCount).IsEqualTo(0);
        await Assert.That(manifest.Consolidation.DivergentLicenses.Count).IsEqualTo(0);
    }

    [Test]
    public async Task RunAsync_Should_Clean_Up_Temp_Artifacts_On_Happy_Path()
    {
        // H1 completion: staged-replace writes to .tmp siblings in Phase 1, then Phase 2
        // swaps them into place. After a successful Consolidate, no .tmp artifacts should
        // survive — they're all moved to their final names or (for the consolidated dir)
        // renamed atomically onto the live location.
        var repo = new FakeRepoBuilder(FakeRepoPlatform.Windows)
            .Seed(new HarvestOutputSeeder("SDL2_gfx", "win-x64", "x64-windows-hybrid")
                .WithPrimary("SDL2_gfx.dll", "win-gfx")
                .WithLicense("libfreetype", content: "freetype-license"))
            .BuildContextWithHandles();

        var task = new ConsolidateHarvestTask(new ConsolidateHarvestTaskRunner());
        await task.RunAsync(repo.BuildContext);

        // Final artifacts present:
        await Assert.That(repo.Exists("artifacts/harvest_output/SDL2_gfx/harvest-manifest.json")).IsTrue();
        await Assert.That(repo.Exists("artifacts/harvest_output/SDL2_gfx/harvest-summary.json")).IsTrue();
        await Assert.That(repo.Exists("artifacts/harvest_output/SDL2_gfx/licenses/_consolidated/libfreetype/copyright")).IsTrue();

        // No .tmp siblings survive:
        await Assert.That(repo.Exists("artifacts/harvest_output/SDL2_gfx/harvest-manifest.tmp.json")).IsFalse();
        await Assert.That(repo.Exists("artifacts/harvest_output/SDL2_gfx/harvest-summary.tmp.json")).IsFalse();
        await Assert.That(repo.Exists("artifacts/harvest_output/SDL2_gfx/licenses/_consolidated.tmp/libfreetype/copyright")).IsFalse();
    }

    [Test]
    public async Task RunAsync_Should_Preserve_Old_State_When_Consolidate_Crashes_During_Temp_Write_Phase()
    {
        // H1 completion — Phase 1 (temp-write) crash invariant. Previous Consolidate left
        // a valid state on disk (_consolidated/libpng/copyright + harvest-manifest.json).
        // New Consolidate writes entries into licenses/_consolidated.tmp/ — inject a
        // failure mid-loop. Expected: swap never begins, so the old state is fully intact.
        // Operators can keep packing against the previous valid receipt until the next
        // Consolidate retry succeeds. Phase 2 (delete-then-move swap) is an accepted
        // staged-replace window documented separately; this test covers the larger and
        // more common failure surface.
        var repo = SeedRepoWithPreviousValidConsolidation();
        var throwingFileSystem = new ThrowingFileSystem(
            inner: repo.FileSystem,
            shouldThrow: trigger =>
                trigger.Operation == ThrowOperation.FileOpen &&
                trigger.FileMode == FileMode.Create &&
                trigger.SourcePath.FullPath.Contains("_consolidated.tmp", StringComparison.OrdinalIgnoreCase),
            exceptionFactory: _ => new IOException("injected disk failure during staged license write"));

        repo.CakeContext.FileSystem.Returns(throwingFileSystem);

        var task = new ConsolidateHarvestTask(new ConsolidateHarvestTaskRunner());

        await Assert.That(() => task.RunAsync(repo.BuildContext)).Throws<CakeException>();

        // Old valid state survives — the swap never started because Phase 1 threw before
        // SwapTempArtifactsIntoPlace was invoked.
        await Assert.That(repo.Exists("artifacts/harvest_output/SDL2_image/licenses/_consolidated/libpng/copyright")).IsTrue();
        var oldManifestJson = await repo.ReadAllTextAsync("artifacts/harvest_output/SDL2_image/harvest-manifest.json");
        var oldManifest = JsonSerializer.Deserialize<HarvestManifest>(oldManifestJson, JsonOptions);
        await Assert.That(oldManifest).IsNotNull();
        await Assert.That(oldManifest!.LibraryName).IsEqualTo("SDL2_image");
        await Assert.That(oldManifest.Consolidation).IsNotNull();
        await Assert.That(oldManifest.Consolidation!.LicensesConsolidated).IsTrue();
        await Assert.That(oldManifest.Consolidation.LicenseEntriesCount).IsEqualTo(1);

        var oldSummaryJson = await repo.ReadAllTextAsync("artifacts/harvest_output/SDL2_image/harvest-summary.json");
        var oldSummary = JsonSerializer.Deserialize<HarvestSummary>(oldSummaryJson, JsonOptions);
        await Assert.That(oldSummary).IsNotNull();
        await Assert.That(oldSummary!.SuccessfulRids).IsEqualTo(1);
        await Assert.That(oldSummary.TotalRids).IsEqualTo(1);

        // Tmp artifacts get cleaned by the catch handler so the next Consolidate run
        // starts from a clean slate — no orphan state survives into subsequent runs.
        await Assert.That(repo.Exists("artifacts/harvest_output/SDL2_image/licenses/_consolidated.tmp/libpng/copyright")).IsFalse();
        await Assert.That(repo.Exists("artifacts/harvest_output/SDL2_image/harvest-manifest.tmp.json")).IsFalse();
        await Assert.That(repo.Exists("artifacts/harvest_output/SDL2_image/harvest-summary.tmp.json")).IsFalse();
    }

    [Test]
    public async Task RunAsync_Should_Fail_Fatally_When_Any_Library_Consolidation_Fails()
    {
        // H1 completion: per-library exceptions are no longer silently swallowed. The
        // task aggregates failures across libraries then throws at the end so operators
        // see every broken library in one run AND the overall Cake task status is failed.
        // Pre-H1 behavior (log + continue as green) hid compliance regressions entirely.
        var repo = SeedRepoWithPreviousValidConsolidation();
        var throwingFileSystem = new ThrowingFileSystem(
            inner: repo.FileSystem,
            shouldThrow: trigger =>
                trigger.Operation == ThrowOperation.FileOpen &&
                trigger.SourcePath.FullPath.EndsWith("harvest-manifest.tmp.json", StringComparison.OrdinalIgnoreCase),
            exceptionFactory: _ => new IOException("injected write failure during staged manifest"));

        repo.CakeContext.FileSystem.Returns(throwingFileSystem);

        var task = new ConsolidateHarvestTask(new ConsolidateHarvestTaskRunner());

        var thrown = await Assert.That(() => task.RunAsync(repo.BuildContext)).Throws<CakeException>();
        await Assert.That(thrown!.Message).Contains("ConsolidateHarvest failed");
        await Assert.That(thrown.Message).Contains("SDL2_image");
    }

    private static FakeRepoHandles SeedRepoWithPreviousValidConsolidation()
    {
        // Set up: a previous Consolidate succeeded and left valid artifacts on disk.
        // Then Harvest ran for a new RID, but crashed. Operator re-runs Harvest + Consolidate.
        // For the purposes of these tests we skip the Harvest invalidation step and set up
        // the pre-new-run state directly — simulates "Consolidate invoked after a crash."
        var previousManifest = new HarvestManifest
        {
            LibraryName = "SDL2_image",
            GeneratedTimestamp = DateTimeOffset.UtcNow,
            Rids = [CreateSuccessStatus("SDL2_image", "win-x64", "x64-windows-hybrid")],
            Summary = new HarvestSummary
            {
                TotalRids = 1,
                SuccessfulRids = 1,
                FailedRids = 0,
                SuccessRate = 1,
            },
            Consolidation = new ConsolidationState
            {
                LicensesConsolidated = true,
                LicenseEntriesCount = 1,
                DivergentLicenses = [],
            },
        };

        var repo = new FakeRepoBuilder(FakeRepoPlatform.Windows)
            .Seed(new HarvestOutputSeeder("SDL2_image", "win-x64", "x64-windows-hybrid")
                .WithPrimary("SDL2_image.dll", "win-image")
                .WithLicense("libpng", content: "libpng-license"))
            .WithTextFile("artifacts/harvest_output/SDL2_image/licenses/_consolidated/libpng/copyright", "libpng-license")
            .WithTextFile("artifacts/harvest_output/SDL2_image/harvest-manifest.json", JsonSerializer.Serialize(previousManifest, JsonOptions))
            .WithTextFile("artifacts/harvest_output/SDL2_image/harvest-summary.json", JsonSerializer.Serialize(previousManifest.Summary, JsonOptions))
            .BuildContextWithHandles();

        return repo;
    }

    [Test]
    public async Task RunAsync_Should_Skip_License_Consolidation_When_No_Successful_Rids()
    {
        var repo = new FakeRepoBuilder(FakeRepoPlatform.Windows)
            .Seed(new HarvestOutputSeeder("SDL2_ttf", "win-x64", "x64-windows-hybrid")
                .WithPrimary("SDL2_ttf.dll", "win-ttf")
                .WithLicense("freetype", content: "freetype-text")
                .AsFailure("harvest failed post-deploy"))
            .BuildContextWithHandles();

        var task = new ConsolidateHarvestTask(new ConsolidateHarvestTaskRunner());
        await task.RunAsync(repo.BuildContext);

        // _consolidated/ is never created when no successful RID contributed.
        await Assert.That(repo.Exists("artifacts/harvest_output/SDL2_ttf/licenses/_consolidated/freetype/copyright")).IsFalse();
    }

    private static RidHarvestStatus CreateSuccessStatus(string library, string rid, string triplet) => new()
    {
        LibraryName = library,
        Rid = rid,
        Triplet = triplet,
        Success = true,
        ErrorMessage = null,
        Timestamp = DateTimeOffset.UtcNow,
        Statistics = new HarvestStatistics
        {
            PrimaryFilesCount = 1,
            RuntimeFilesCount = 2,
            LicenseFilesCount = 1,
            DeployedPackagesCount = 3,
            FilteredPackagesCount = 0,
            DeploymentStrategy = "DirectCopy",
        },
    };

    private static RidHarvestStatus CreateFailedStatus(string library, string rid, string triplet, string error) => new()
    {
        LibraryName = library,
        Rid = rid,
        Triplet = triplet,
        Success = false,
        ErrorMessage = error,
        Timestamp = DateTimeOffset.UtcNow,
        Statistics = null,
    };

    private static async Task<HarvestManifest> RunConsolidationForStatusesAsync(string libraryName, params RidHarvestStatus[] statuses)
    {
        var builder = new FakeRepoBuilder(FakeRepoPlatform.Windows);

        foreach (var status in statuses)
        {
            builder.WithHarvestStatus(libraryName, status.Rid, status);
        }

        var repo = builder.BuildContextWithHandles();

        var task = new ConsolidateHarvestTask(new ConsolidateHarvestTaskRunner());
        await task.RunAsync(repo.BuildContext);

        var manifestPath = $"artifacts/harvest_output/{libraryName}/harvest-manifest.json";
        var manifestJson = await repo.ReadAllTextAsync(manifestPath);
        return JsonSerializer.Deserialize<HarvestManifest>(manifestJson, JsonOptions)
            ?? throw new InvalidOperationException("Failed to deserialize generated harvest manifest.");
    }
}
