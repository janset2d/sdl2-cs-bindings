using Build.Tests.Fixtures.RepositoryGates;
using TUnit.Assertions.Enums;

namespace Build.Tests.Unit.RepositoryGates;

public sealed class MarkdownLinksTests
{
    [Test]
    public async Task Extract_Should_Not_Open_Fence_When_Backtick_Info_String_Contains_Backtick()
    {
        var links = MarkdownLinks.Extract("```x``` see\n[b](y.md)\n");
        await Assert.That(links.Select(l => l.Target)).IsEquivalentTo(["y.md"], CollectionOrdering.Matching);
    }

    [Test]
    public async Task Extract_Should_Skip_Links_Inside_Fenced_Code()
    {
        var links = MarkdownLinks.Extract("```\n[a](x.md)\n```\n~~~text\n[c](z.md)\n~~~\n[b](y.md)\n");
        await Assert.That(links.Select(l => l.Target)).IsEquivalentTo(["y.md"], CollectionOrdering.Matching);
    }

    [Test]
    public async Task Extract_Should_Skip_Inline_Code_Spans()
    {
        var links = MarkdownLinks.Extract("`[a](x.md)` ``[c](z.md)`` [b](y.md)");
        await Assert.That(links.Select(l => l.Target)).IsEquivalentTo(["y.md"], CollectionOrdering.Matching);
    }

    [Test]
    public async Task Extract_Should_Read_Reference_Definitions()
    {
        var links = MarkdownLinks.Extract("[id]: docs/a.md \"title\"\n[next]: <docs/b c.md> 'title'");
        await Assert.That(links.Select(l => l.Target)).IsEquivalentTo(["docs/a.md", "docs/b c.md"], CollectionOrdering.Matching);
    }

    [Test]
    public async Task Extract_Should_Read_Images_And_Angle_Bracket_Targets()
    {
        var links = MarkdownLinks.Extract("![b](<b c.png> \"title\") [a](a.md \"title\")");
        await Assert.That(links.Select(l => l.Target)).IsEquivalentTo(["b c.png", "a.md"], CollectionOrdering.Matching);
    }

    [Test]
    public async Task Extract_Should_Report_Line_Numbers_When_Input_Uses_Crlf()
    {
        var links = MarkdownLinks.Extract("x\r\n[b](y.md)\r\n");
        await Assert.That(links).IsEquivalentTo([new MarkdownLink(2, "y.md")], CollectionOrdering.Matching);
    }

    [Test]
    [Arguments("https://example.com/a.md")]
    [Arguments("mailto:a@b")]
    [Arguments("#top")]
    public async Task ToRepositoryPath_Should_Return_Null_When_Target_Is_External_Url(string target)
    {
        ArgumentNullException.ThrowIfNull(target);
        await Assert.That(MarkdownLinks.ToRepositoryPath("README.md", target)).IsNull();
    }

    [Test]
    [Arguments("blob")]
    [Arguments("tree")]
    public async Task ToRepositoryPath_Should_Map_Absolute_GitHub_Blob_Link_To_Repository_Path(string kind)
    {
        var target = $"https://github.com/janset2d/sdl2-cs-bindings/{kind}/master/docs/ROADMAP.md#now";
        await Assert.That(MarkdownLinks.ToRepositoryPath("README.md", target)).IsEqualTo("docs/ROADMAP.md");
    }

    [Test]
    public async Task ToRepositoryPath_Should_Strip_Fragment_And_Decode_Spaces()
    {
        await Assert.That(MarkdownLinks.ToRepositoryPath("docs/a.md", "b%20c.md?raw=1#x"))
            .IsEqualTo("docs/b c.md");
    }

    [Test]
    [Arguments("docs/engineering/a.md", "../adr/0001-x.md", "docs/adr/0001-x.md")]
    [Arguments("docs/a.md", "../../x.md", "../x.md")]
    public async Task ToRepositoryPath_Should_Resolve_Parent_Segments_Against_Source_Directory(
        string source, string target, string expected)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);
        await Assert.That(MarkdownLinks.ToRepositoryPath(source, target)).IsEqualTo(expected);
    }

    [Test]
    public async Task ToRepositoryPath_Should_Keep_Drive_Letter_Path_Unresolvable()
    {
        await Assert.That(MarkdownLinks.ToRepositoryPath("README.md", "E:/repos/x.md"))
            .IsEqualTo("E:/repos/x.md");
    }
}
