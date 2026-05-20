namespace Build.Tests.Fixtures;

[AttributeUsage(AttributeTargets.Assembly | AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class GeneratedPreviewSnapshotAttribute : SkipAttribute
{
    public GeneratedPreviewSnapshotAttribute()
        : base("Generated preview snapshot tests require JANSET_VERIFY_GENERATED_PREVIEW=1")
    {
    }

    public override Task<bool> ShouldSkip(TestRegisteredContext context)
    {
        return Task.FromResult(!string.Equals(
            Environment.GetEnvironmentVariable("JANSET_VERIFY_GENERATED_PREVIEW"),
            "1",
            StringComparison.Ordinal));
    }
}
