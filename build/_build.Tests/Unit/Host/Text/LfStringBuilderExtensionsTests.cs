using System.Text;
using Build.Host.Text;

namespace Build.Tests.Unit.Host.Text;

public sealed class LfStringBuilderExtensionsTests
{
    [Test]
    public async Task AppendLf_Should_Append_LineFeed_When_Using_Each_Overload()
    {
        var builder = new StringBuilder("prefix");
        await Assert.That(builder.AppendLf()).IsSameReferenceAs(builder);
        await Assert.That(builder.AppendLf("text")).IsSameReferenceAs(builder);
        await Assert.That(builder.AppendLf('x')).IsSameReferenceAs(builder);
        await Assert.That(builder.ToString()).IsEqualTo("prefix\ntext\nx\n");
    }
}
