namespace Janset.SDL2.AbiTests.Infrastructure.Classification;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public sealed class UpstreamSdlTestAttribute(string sourceFile, string testFunction) : Attribute
{
    public string SourceFile { get; } = sourceFile;

    public string TestFunction { get; } = testFunction;
}
