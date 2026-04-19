using Build.Infrastructure.Coverage;
using Cake.Core.IO;
using Cake.Testing;

namespace Build.Tests.Unit.Infrastructure.Coverage;

public class CoberturaReaderTests
{
    private const string ValidXml = """
        <?xml version="1.0" encoding="utf-8" standalone="yes"?>
        <coverage line-rate="0.6079" branch-rate="0.4995" complexity="1142" version="1.9" timestamp="1776274992" lines-covered="1526" lines-valid="2510" branches-covered="524" branches-valid="1049">
          <packages></packages>
        </coverage>
        """;

    // Parse(string) never touches the filesystem, so a fake/shared file system is fine.
    private static CoberturaReader CreateReader() => new(new FakeFileSystem(FakeEnvironment.CreateUnixEnvironment()));

    // Parse(string) — pure string parsing

    [Test]
    public async Task Parse_Should_Extract_LineRate_And_BranchRate_From_Root_Element()
    {
        var metrics = CreateReader().Parse(ValidXml);

        await Assert.That(metrics.LineRate).IsEqualTo(0.6079).Within(0.0001);
        await Assert.That(metrics.BranchRate).IsEqualTo(0.4995).Within(0.0001);
    }

    [Test]
    public async Task Parse_Should_Extract_Lines_And_Branches_Counts_From_Root_Element()
    {
        var metrics = CreateReader().Parse(ValidXml);

        await Assert.That(metrics.LinesCovered).IsEqualTo(1526);
        await Assert.That(metrics.LinesValid).IsEqualTo(2510);
        await Assert.That(metrics.BranchesCovered).IsEqualTo(524);
        await Assert.That(metrics.BranchesValid).IsEqualTo(1049);
    }

    [Test]
    public async Task Parse_Should_Compute_LinePercent_And_BranchPercent_From_Rates()
    {
        const string xml = """
            <?xml version="1.0"?>
            <coverage line-rate="0.5" branch-rate="0.25" lines-covered="100" lines-valid="200" branches-covered="50" branches-valid="200">
              <packages></packages>
            </coverage>
            """;

        var metrics = CreateReader().Parse(xml);

        await Assert.That(metrics.LinePercent).IsEqualTo(50.0).Within(0.001);
        await Assert.That(metrics.BranchPercent).IsEqualTo(25.0).Within(0.001);
    }

    [Test]
    public void Parse_Should_Throw_ArgumentException_When_Root_Is_Not_Coverage()
    {
        const string xml = "<root></root>";
        var reader = CreateReader();

        Assert.Throws<ArgumentException>(() => reader.Parse(xml));
    }

    [Test]
    public void Parse_Should_Throw_ArgumentException_When_LineRate_Attribute_Missing()
    {
        const string xml = """
            <?xml version="1.0"?>
            <coverage branch-rate="0.5" lines-covered="100" lines-valid="200" branches-covered="50" branches-valid="100">
              <packages></packages>
            </coverage>
            """;

        var reader = CreateReader();

        Assert.Throws<ArgumentException>(() => reader.Parse(xml));
    }

    [Test]
    public void Parse_Should_Throw_ArgumentException_When_BranchRate_Attribute_Missing()
    {
        const string xml = """
            <?xml version="1.0"?>
            <coverage line-rate="0.5" lines-covered="100" lines-valid="200" branches-covered="50" branches-valid="100">
              <packages></packages>
            </coverage>
            """;

        var reader = CreateReader();

        Assert.Throws<ArgumentException>(() => reader.Parse(xml));
    }

    [Test]
    public void Parse_Should_Throw_ArgumentException_When_Rate_Attribute_Is_Not_Numeric()
    {
        const string xml = """
            <?xml version="1.0"?>
            <coverage line-rate="not-a-number" branch-rate="0.5" lines-covered="100" lines-valid="200" branches-covered="50" branches-valid="100">
              <packages></packages>
            </coverage>
            """;

        var reader = CreateReader();

        Assert.Throws<ArgumentException>(() => reader.Parse(xml));
    }

    [Test]
    public void Parse_Should_Throw_ArgumentException_When_LinesCovered_Is_Not_Integer()
    {
        const string xml = """
            <?xml version="1.0"?>
            <coverage line-rate="0.5" branch-rate="0.5" lines-covered="oops" lines-valid="200" branches-covered="50" branches-valid="100">
              <packages></packages>
            </coverage>
            """;

        var reader = CreateReader();

        Assert.Throws<ArgumentException>(() => reader.Parse(xml));
    }

    [Test]
    public void Parse_Should_Throw_ArgumentException_When_Xml_Is_Malformed()
    {
        const string xml = "<coverage line-rate=\"0.5\"";
        var reader = CreateReader();

        Assert.Throws<ArgumentException>(() => reader.Parse(xml));
    }

    [Test]
    public void Parse_Should_Throw_ArgumentException_When_Content_Is_Whitespace()
    {
        var reader = CreateReader();

        Assert.Throws<ArgumentException>(() => reader.Parse("   "));
    }

    [Test]
    public async Task Parse_Should_Parse_Cobertura_With_InvariantCulture_Double_Format()
    {
        // MTP always emits invariant-culture doubles ("0.6079" not "0,6079"),
        // so the reader must parse against InvariantCulture regardless of host locale.
        const string xml = """
            <?xml version="1.0"?>
            <coverage line-rate="0.123456789" branch-rate="0.987654321" lines-covered="1" lines-valid="2" branches-covered="3" branches-valid="4">
              <packages></packages>
            </coverage>
            """;

        var metrics = CreateReader().Parse(xml);

        await Assert.That(metrics.LineRate).IsEqualTo(0.123456789).Within(1e-9);
        await Assert.That(metrics.BranchRate).IsEqualTo(0.987654321).Within(1e-9);
    }

    // ParseFile(FilePath) — exercises the Cake IFileSystem integration via FakeFileSystem

    [Test]
    public async Task ParseFile_Should_Read_Content_Through_Cake_FileSystem_Abstraction()
    {
        var environment = FakeEnvironment.CreateUnixEnvironment();
        var fileSystem = new FakeFileSystem(environment);
        var path = new FilePath("/fake/coverage.cobertura.xml");
        fileSystem.CreateFile(path).SetContent(ValidXml);

        var reader = new CoberturaReader(fileSystem);
        var metrics = reader.ParseFile(path);

        await Assert.That(metrics.LinesCovered).IsEqualTo(1526);
        await Assert.That(metrics.LinesValid).IsEqualTo(2510);
    }

    [Test]
    public async Task ParseFile_Should_Yield_Same_Metrics_As_Parse_For_Identical_Content()
    {
        var environment = FakeEnvironment.CreateUnixEnvironment();
        var fileSystem = new FakeFileSystem(environment);
        var path = new FilePath("/fake/coverage.cobertura.xml");
        fileSystem.CreateFile(path).SetContent(ValidXml);

        var reader = new CoberturaReader(fileSystem);

        var fromFile = reader.ParseFile(path);
        var fromString = reader.Parse(ValidXml);

        await Assert.That(fromFile).IsEqualTo(fromString);
    }

    [Test]
    public void ParseFile_Should_Throw_ArgumentException_When_File_Content_Is_Not_Valid_Cobertura()
    {
        var environment = FakeEnvironment.CreateUnixEnvironment();
        var fileSystem = new FakeFileSystem(environment);
        var path = new FilePath("/fake/bogus.xml");
        fileSystem.CreateFile(path).SetContent("<nonsense></nonsense>");

        var reader = new CoberturaReader(fileSystem);

        Assert.Throws<ArgumentException>(() => reader.ParseFile(path));
    }

    [Test]
    public void ParseFile_Should_Propagate_FileNotFoundException_When_Target_Does_Not_Exist()
    {
        var environment = FakeEnvironment.CreateUnixEnvironment();
        var fileSystem = new FakeFileSystem(environment);
        // Deliberately do NOT create the file.
        var reader = new CoberturaReader(fileSystem);

        Assert.Throws<FileNotFoundException>(() => reader.ParseFile(new FilePath("/fake/missing.xml")));
    }
}
