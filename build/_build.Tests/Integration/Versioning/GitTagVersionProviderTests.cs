using System.Text.Json;
using Build.Features.Versioning;
using Build.Host.Configuration;
using Build.Host.Paths;
using Build.Shared.Versioning;
using Build.Tests.Fixtures;
using Cake.Core;
using Cake.Core.IO;
using Cake.Testing;
using NSubstitute;
using NuGet.Versioning;

namespace Build.Tests.Integration.Versioning;

/// <summary>
/// Integration tests for <see cref="GitTagVersionProvider"/>.
/// These tests run against ephemeral real-filesystem git repos because
/// Cake.Frosting.Git bypasses <c>ICakeContext.FileSystem</c> and uses LibGit2Sharp
/// directly.
/// </summary>
public sealed class GitTagVersionProviderTests
{
    [Test]
    public async Task RunAsync_GitTagSource_Should_Write_Canonical_Json_When_Scope_Is_Full_Family_Tag()
    {
        using var repo = new TempGitRepo();
        repo.CommitFile("README.md", "initial", "initial commit");
        repo.TagHead("sdl2-image-2.8.0");

        var manifest = ManifestFixture.CreateTestManifestConfig();
        var cakeContext = CreateCakeContext();
        var outputFile = new FilePath(System.IO.Path.Combine(repo.Path, "artifacts", "resolve-versions", "versions.json"));
        var pathService = Substitute.For<IPathService>();
        pathService.RepoRoot.Returns(new DirectoryPath(repo.Path));
        pathService.GetResolveVersionsOutputFile().Returns(outputFile);

        var runner = new ResolveVersionsPipeline(
            cakeContext,
            cakeContext.Log,
            pathService,
            manifest,
            new PackageBuildConfiguration(new Dictionary<string, NuGetVersion>(StringComparer.OrdinalIgnoreCase)),
            new VersioningConfiguration(
                versionSource: "git-tag",
                suffix: null,
                scope: ["sdl2-image-2.8.0"]),
            new UpstreamVersionAlignmentValidator());

        await runner.RunAsync();

        var content = await System.IO.File.ReadAllTextAsync(outputFile.FullPath);
        var deserialized = JsonSerializer.Deserialize<SortedDictionary<string, string>>(content);
        await Assert.That(deserialized).IsNotNull();
        await Assert.That(deserialized!.Count).IsEqualTo(1);
        await Assert.That(deserialized["sdl2-image"]).IsEqualTo("2.8.0");
    }

    [Test]
    public async Task ResolveAsync_Targeted_Should_Return_Family_Version_From_Tag_At_Head()
    {
        using var repo = new TempGitRepo();
        repo.CommitFile("README.md", "initial", "initial commit");
        repo.TagHead("sdl2-image-2.8.0");

        var provider = CreateProvider(repo, new GitTagScope.Targeted("sdl2-image"));

        var result = await provider.ResolveAsync(Empty.Scope);

        await Assert.That(result.Count).IsEqualTo(1);
        await Assert.That(result["sdl2-image"].ToNormalizedString()).IsEqualTo("2.8.0");
    }

    [Test]
    public async Task ResolveAsync_Targeted_Should_Accept_Family_Tag_As_Targeted_Scope()
    {
        using var repo = new TempGitRepo();
        repo.CommitFile("README.md", "initial", "initial commit");
        repo.TagHead("sdl2-image-2.8.0");

        var provider = CreateProvider(repo, new GitTagScope.Targeted("sdl2-image-2.8.0"));

        var result = await provider.ResolveAsync(Empty.Scope);

        await Assert.That(result.Count).IsEqualTo(1);
        await Assert.That(result["sdl2-image"].ToNormalizedString()).IsEqualTo("2.8.0");
    }

    [Test]
    public async Task ResolveAsync_Targeted_Should_Throw_When_Family_Tag_Missing_At_Head()
    {
        using var repo = new TempGitRepo();
        repo.CommitFile("README.md", "initial", "initial commit");
        // No tag applied — provider should surface an actionable error.

        var provider = CreateProvider(repo, new GitTagScope.Targeted("sdl2-image"));

        var ex = await Assert.That(async () => await provider.ResolveAsync(Empty.Scope)).Throws<CakeException>();
        await Assert.That(ex!.Message).Contains("no 'sdl2-image-<semver>' tag at HEAD");
    }

    [Test]
    public async Task ResolveAsync_Targeted_Should_Throw_When_G54_Rejects_Tag_Version()
    {
        using var repo = new TempGitRepo();
        repo.CommitFile("README.md", "initial", "initial commit");
        // Manifest fixture pins SDL2_image at vcpkg_version 2.8.x; a 3.0.0 tag must fail
        // G54 upstream-version alignment.
        repo.TagHead("sdl2-image-3.0.0");

        var provider = CreateProvider(repo, new GitTagScope.Targeted("sdl2-image"));

        var ex = await Assert.That(async () => await provider.ResolveAsync(Empty.Scope)).Throws<CakeException>();
        await Assert.That(ex!.Message).Contains("G54");
    }

    [Test]
    public async Task ResolveAsync_Train_Should_Discover_All_Concrete_Families_Ordered_By_Dependencies()
    {
        using var repo = new TempGitRepo();
        repo.CommitFile("README.md", "initial", "initial commit");
        // Both concrete families' tags point at the same commit — meta-tag release shape.
        repo.TagHead("sdl2-core-2.32.0");
        repo.TagHead("sdl2-image-2.8.0");

        var provider = CreateProvider(repo, new GitTagScope.Train());

        var result = await provider.ResolveAsync(Empty.Scope);

        await Assert.That(result.Count).IsEqualTo(2);
        await Assert.That(result["sdl2-core"].ToNormalizedString()).IsEqualTo("2.32.0");
        await Assert.That(result["sdl2-image"].ToNormalizedString()).IsEqualTo("2.8.0");

        // Topological order: core before image (image depends on core per
        // ManifestFixture.CreateTestManifestConfig()).
        var ordered = result.Keys.ToList();
        await Assert.That(ordered[0]).IsEqualTo("sdl2-core");
        await Assert.That(ordered[1]).IsEqualTo("sdl2-image");
    }

    [Test]
    public async Task ResolveAsync_Train_Should_Filter_Result_By_Requested_Scope()
    {
        using var repo = new TempGitRepo();
        repo.CommitFile("README.md", "initial", "initial commit");
        repo.TagHead("sdl2-core-2.32.0");
        repo.TagHead("sdl2-image-2.8.0");

        var provider = CreateProvider(repo, new GitTagScope.Train());

        // Requested scope filters to core only — image excluded from the returned mapping.
        var result = await provider.ResolveAsync(
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "sdl2-core" });

        await Assert.That(result.Count).IsEqualTo(1);
        await Assert.That(result.ContainsKey("sdl2-core")).IsTrue();
        await Assert.That(result.ContainsKey("sdl2-image")).IsFalse();
    }

    private static GitTagVersionProvider CreateProvider(TempGitRepo repo, GitTagScope scope)
    {
        var manifest = ManifestFixture.CreateTestManifestConfig();
        var g54 = new UpstreamVersionAlignmentValidator();
        var cakeContext = CreateCakeContext();
        return new GitTagVersionProvider(
            manifest,
            g54,
            cakeContext,
            new DirectoryPath(repo.Path),
            scope);
    }

    private static ICakeContext CreateCakeContext()
    {
        var env = OperatingSystem.IsWindows()
            ? FakeEnvironment.CreateWindowsEnvironment()
            : FakeEnvironment.CreateUnixEnvironment();

        var context = Substitute.For<ICakeContext>();
        context.Environment.Returns(env);
        context.Log.Returns(new FakeLog());
        context.FileSystem.Returns(new FileSystem());
        return context;
    }

    private static class Empty
    {
        public static readonly IReadOnlySet<string> Scope =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    }
}
