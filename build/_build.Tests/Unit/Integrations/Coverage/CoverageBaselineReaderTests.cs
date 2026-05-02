using Build.Integrations.Coverage;
using Cake.Core.IO;
using Cake.Testing;

namespace Build.Tests.Unit.Integrations.Coverage;

public class CoverageBaselineReaderTests
{
    // Parse(string) never touches the filesystem, so a fake/shared file system is fine.
    private static CoverageBaselineReader CreateReader() => new(new FakeFileSystem(FakeEnvironment.CreateUnixEnvironment()));

    // Parse(string) — pure JSON deserialization

    [Test]
    public async Task Parse_Should_Extract_Minimum_Thresholds_From_Required_Fields()
    {
        const string json = """
            {
              "line_coverage_min": 60.0,
              "branch_coverage_min": 49.0
            }
            """;

        var baseline = CreateReader().Parse(json);

        await Assert.That(baseline.LineCoverageMin).IsEqualTo(60.0);
        await Assert.That(baseline.BranchCoverageMin).IsEqualTo(49.0);
    }

    [Test]
    public async Task Parse_Should_Extract_Optional_Metadata_When_Present()
    {
        const string json = """
            {
              "line_coverage_min": 60.0,
              "branch_coverage_min": 49.0,
              "reviewed_at": "2026-04-15",
              "measured_line": 60.80,
              "measured_branch": 49.95,
              "notes": "Initial baseline"
            }
            """;

        var baseline = CreateReader().Parse(json);

        await Assert.That(baseline.ReviewedAt).IsEqualTo("2026-04-15");
        await Assert.That(baseline.MeasuredLine).IsEqualTo(60.80);
        await Assert.That(baseline.MeasuredBranch).IsEqualTo(49.95);
        await Assert.That(baseline.Notes).IsEqualTo("Initial baseline");
    }

    [Test]
    public async Task Parse_Should_Default_Optional_Metadata_To_Null_When_Missing()
    {
        const string json = """
            {
              "line_coverage_min": 50.0,
              "branch_coverage_min": 40.0
            }
            """;

        var baseline = CreateReader().Parse(json);

        await Assert.That(baseline.ReviewedAt).IsNull();
        await Assert.That(baseline.MeasuredLine).IsNull();
        await Assert.That(baseline.MeasuredBranch).IsNull();
        await Assert.That(baseline.Notes).IsNull();
    }

    [Test]
    public void Parse_Should_Throw_ArgumentException_When_LineCoverageMin_Is_Missing()
    {
        const string json = """
            {
              "branch_coverage_min": 40.0
            }
            """;

        var reader = CreateReader();

        Assert.Throws<ArgumentException>(() => reader.Parse(json));
    }

    [Test]
    public void Parse_Should_Throw_ArgumentException_When_BranchCoverageMin_Is_Missing()
    {
        const string json = """
            {
              "line_coverage_min": 60.0
            }
            """;

        var reader = CreateReader();

        Assert.Throws<ArgumentException>(() => reader.Parse(json));
    }

    [Test]
    public void Parse_Should_Throw_ArgumentException_When_Json_Is_Malformed()
    {
        const string json = "{ not json }";
        var reader = CreateReader();

        Assert.Throws<ArgumentException>(() => reader.Parse(json));
    }

    [Test]
    public void Parse_Should_Throw_ArgumentException_When_Content_Is_Whitespace()
    {
        var reader = CreateReader();

        Assert.Throws<ArgumentException>(() => reader.Parse("   "));
    }

    // ParseFile(FilePath) — exercises the Cake IFileSystem integration via FakeFileSystem

    [Test]
    public async Task ParseFile_Should_Read_Content_Through_Cake_FileSystem_Abstraction()
    {
        const string json = """
            {
              "line_coverage_min": 55.5,
              "branch_coverage_min": 42.5,
              "reviewed_at": "2026-04-15"
            }
            """;

        var environment = FakeEnvironment.CreateUnixEnvironment();
        var fileSystem = new FakeFileSystem(environment);
        var path = new FilePath("/fake/coverage-baseline.json");
        fileSystem.CreateFile(path).SetContent(json);

        var reader = new CoverageBaselineReader(fileSystem);
        var baseline = reader.ParseFile(path);

        await Assert.That(baseline.LineCoverageMin).IsEqualTo(55.5);
        await Assert.That(baseline.BranchCoverageMin).IsEqualTo(42.5);
        await Assert.That(baseline.ReviewedAt).IsEqualTo("2026-04-15");
    }

    [Test]
    public void ParseFile_Should_Propagate_FileNotFoundException_When_Target_Does_Not_Exist()
    {
        var environment = FakeEnvironment.CreateUnixEnvironment();
        var fileSystem = new FakeFileSystem(environment);
        // Deliberately do NOT create the file.
        var reader = new CoverageBaselineReader(fileSystem);

        Assert.Throws<FileNotFoundException>(() => reader.ParseFile(new FilePath("/fake/missing.json")));
    }

    [Test]
    public void ParseFile_Should_Throw_ArgumentException_When_File_Content_Is_Missing_Required_Field()
    {
        var environment = FakeEnvironment.CreateUnixEnvironment();
        var fileSystem = new FakeFileSystem(environment);
        var path = new FilePath("/fake/bad-baseline.json");
        fileSystem.CreateFile(path).SetContent("""{ "line_coverage_min": 60.0 }""");

        var reader = new CoverageBaselineReader(fileSystem);

        Assert.Throws<ArgumentException>(() => reader.ParseFile(path));
    }
}
