using System.Text;
using Build.Host.Processes;

namespace Build.Tests.Integration;

public sealed class HostProcessRunnerTests
{
    [Test]
    public async Task RunAsync_Should_Return_DotNet_Version_As_Utf8()
    {
        var result = await new HostProcessRunner().RunAsync(new HostProcessRequest("dotnet", ["--version"]), CancellationToken.None);

        await Assert.That(result.ExitCode).IsEqualTo(0);
        await Assert.That(result.StandardOutput.Trim()).IsNotEmpty();
        await Assert.That(Encoding.UTF8.GetString(Encoding.UTF8.GetBytes(result.StandardOutput))).IsEqualTo(result.StandardOutput);
        await Assert.That(result.StandardError).IsEmpty();
    }
}
