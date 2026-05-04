using System.Text.Json;
using Build.Features.Versioning;
using Build.Shared.Versioning;
using Build.Tests.Fixtures;
using Cake.Core;

namespace Build.Tests.Unit.Features.Versioning;

/// <summary>
/// Integration-style tests for <see cref="ResolveVersionsFromExplicitTask"/>.
/// Each test sets up <see cref="ParsedArguments"/> via <see cref="FakeRepoBuilder"/>,
/// invokes the task, and asserts on the produced versions.json or thrown exception.
/// Scenarios are ports of the retired <c>ExplicitVersionProviderTests</c> + relevant
/// entries from <c>ResolveVersionsPipelineTests</c> (plan v4 §4.5), plus three new
/// scenarios (mutex, comma-separated path, parser propagation) introduced in v4.
/// </summary>
public sealed class ResolveVersionsFromExplicitTaskTests
{
    [Test]
    public async Task RunAsync_Should_Write_Full_Mapping_From_Operator_Input()
    {
        // Ports: ExplicitVersionProviderTests.ResolveAsync_Should_Return_Full_Mapping_When_RequestedScope_Is_Empty
        // + ResolveVersionsPipelineTests.RunAsync_Should_Write_Canonical_Json_For_Explicit_Source_Happy_Path
        var manifest = ManifestFixture.CreateTestManifestConfig();
        var repo = new FakeRepoBuilder(FakeRepoPlatform.Windows)
            .WithManifest(manifest)
            .WithExplicitVersion("sdl2-image=2.8.0-rc.2", "sdl2-core=2.32.0-rc.1")
            .BuildContextWithHandles();

        var task = new ResolveVersionsFromExplicitTask(manifest, new UpstreamVersionAlignmentValidator(), CreateWriter(repo));

        await task.RunAsync(repo.BuildContext);

        var deserialized = await ReadVersionsJsonAsync(repo);
        await Assert.That(deserialized.Count).IsEqualTo(2);
        await Assert.That(deserialized.Keys.ToArray()).IsEquivalentTo(["sdl2-core", "sdl2-image"]);
        await Assert.That(deserialized["sdl2-core"]).IsEqualTo("2.32.0-rc.1");
        await Assert.That(deserialized["sdl2-image"]).IsEqualTo("2.8.0-rc.2");
    }

    [Test]
    public async Task RunAsync_Should_Throw_When_Operator_Mapping_Fails_G54_Alignment()
    {
        // Ports: ExplicitVersionProviderTests.ResolveAsync_Should_Reject_Mapping_With_Invalid_Major_Version
        // sdl2-core upstream is 2.32.x in fixture; "3.0.0" fails G54 major alignment.
        var manifest = ManifestFixture.CreateTestManifestConfig();
        var repo = new FakeRepoBuilder(FakeRepoPlatform.Windows)
            .WithManifest(manifest)
            .WithExplicitVersion("sdl2-core=3.0.0")
            .BuildContextWithHandles();

        var task = new ResolveVersionsFromExplicitTask(manifest, new UpstreamVersionAlignmentValidator(), CreateWriter(repo));

        var exception = await Assert.ThrowsAsync<CakeException>(() => task.RunAsync(repo.BuildContext));
        await Assert.That(exception!.Message).Contains("G54");
        await Assert.That(exception.Message).Contains("sdl2-core");
        await Assert.That(exception.Message).Contains("major");
    }

    [Test]
    public async Task RunAsync_Should_Throw_When_No_ExplicitVersion_Or_ExplicitVersions_Supplied()
    {
        // Ports: ExplicitVersionProviderTests.ResolveAsync_Should_Throw_When_Constructed_With_Empty_Mapping
        // + ResolveVersionsPipelineTests.RunAsync_Should_Throw_When_Explicit_Source_Has_No_ExplicitVersions
        var manifest = ManifestFixture.CreateTestManifestConfig();
        var repo = new FakeRepoBuilder(FakeRepoPlatform.Windows)
            .WithManifest(manifest)
            .BuildContextWithHandles();

        var task = new ResolveVersionsFromExplicitTask(manifest, new UpstreamVersionAlignmentValidator(), CreateWriter(repo));

        var exception = await Assert.ThrowsAsync<CakeException>(() => task.RunAsync(repo.BuildContext));
        await Assert.That(exception!.Message).Contains("--explicit-version");
        await Assert.That(exception.Message).Contains("--explicit-versions");
    }

    [Test]
    public async Task RunAsync_Should_Throw_When_Both_ExplicitVersion_And_ExplicitVersions_Supplied()
    {
        // NEW (plan v4 §4.4 N1): mutex check moves from Program.cs to the task. Coverage at
        // the task layer is required because the mutex is the primary "did the operator pick
        // an input shape?" gate.
        var manifest = ManifestFixture.CreateTestManifestConfig();
        var repo = new FakeRepoBuilder(FakeRepoPlatform.Windows)
            .WithManifest(manifest)
            .WithExplicitVersion("sdl2-core=2.32.0-rc.1")
            .WithExplicitVersions("sdl2-image=2.8.0-rc.2")
            .BuildContextWithHandles();

        var task = new ResolveVersionsFromExplicitTask(manifest, new UpstreamVersionAlignmentValidator(), CreateWriter(repo));

        var exception = await Assert.ThrowsAsync<CakeException>(() => task.RunAsync(repo.BuildContext));
        await Assert.That(exception!.Message).Contains("mutually exclusive");
    }

    [Test]
    public async Task RunAsync_Should_Parse_ExplicitVersions_Comma_Separated_Form()
    {
        // NEW (plan v4 §4.4 N2): integration-level wiring proof that the comma-separated
        // input shape reaches versions.json. Parser-level coverage exists in
        // ExplicitVersionParserTests; this is task-level smoke.
        var manifest = ManifestFixture.CreateTestManifestConfig();
        var repo = new FakeRepoBuilder(FakeRepoPlatform.Windows)
            .WithManifest(manifest)
            .WithExplicitVersions("sdl2-core=2.32.0-test.smoke,sdl2-image=2.8.0-test.smoke")
            .BuildContextWithHandles();

        var task = new ResolveVersionsFromExplicitTask(manifest, new UpstreamVersionAlignmentValidator(), CreateWriter(repo));

        await task.RunAsync(repo.BuildContext);

        var deserialized = await ReadVersionsJsonAsync(repo);
        await Assert.That(deserialized.Count).IsEqualTo(2);
        await Assert.That(deserialized["sdl2-core"]).IsEqualTo("2.32.0-test.smoke");
        await Assert.That(deserialized["sdl2-image"]).IsEqualTo("2.8.0-test.smoke");
    }

    [Test]
    public async Task RunAsync_Should_Throw_When_ExplicitVersions_Comma_Form_Has_Malformed_Entry()
    {
        // NEW (plan v4 §4.4 N3): verifies ArgumentException from ExplicitVersionParser
        // propagates as CakeException at task entry (parser → task wrapping discipline).
        var manifest = ManifestFixture.CreateTestManifestConfig();
        var repo = new FakeRepoBuilder(FakeRepoPlatform.Windows)
            .WithManifest(manifest)
            .WithExplicitVersions("sdl2-core:2.32.0,sdl2-image=2.8.0")
            .BuildContextWithHandles();

        var task = new ResolveVersionsFromExplicitTask(manifest, new UpstreamVersionAlignmentValidator(), CreateWriter(repo));

        var exception = await Assert.ThrowsAsync<CakeException>(() => task.RunAsync(repo.BuildContext));
        await Assert.That(exception!.Message).Contains("could not parse operator input");
    }

    private static VersionsJsonWriter CreateWriter(FakeRepoHandles repo) =>
        new(repo.CakeContext, repo.Paths, repo.CakeContext.Log);

    private static async Task<SortedDictionary<string, string>> ReadVersionsJsonAsync(FakeRepoHandles repo)
    {
        var outputFile = repo.Paths.GetResolveVersionsOutputFile();
        var writtenFile = repo.FileSystem.GetFile(outputFile);
        await Assert.That(writtenFile.Exists).IsTrue();

        string content;
        using (var stream = writtenFile.Open(FileMode.Open, FileAccess.Read, FileShare.Read))
        using (var reader = new StreamReader(stream))
        {
            content = await reader.ReadToEndAsync();
        }

        var deserialized = JsonSerializer.Deserialize<SortedDictionary<string, string>>(content);
        await Assert.That(deserialized).IsNotNull();
        return deserialized!;
    }
}
