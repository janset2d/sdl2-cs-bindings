namespace Build.Tests.Fixtures;

[AttributeUsage(AttributeTargets.Assembly | AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class NonWindowsOnlyAttribute : SkipAttribute
{
    public NonWindowsOnlyAttribute()
        : base("Non-Windows only")
    {
    }

    public override Task<bool> ShouldSkip(TestRegisteredContext context)
    {
        return Task.FromResult(OperatingSystem.IsWindows());
    }
}
