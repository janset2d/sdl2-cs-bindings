namespace Build.Tests.Fixtures;

[AttributeUsage(AttributeTargets.Assembly | AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class LinuxOnlyAttribute : SkipAttribute
{
    public LinuxOnlyAttribute()
        : base("Linux only")
    {
    }

    public override Task<bool> ShouldSkip(TestRegisteredContext context)
    {
        return Task.FromResult(!OperatingSystem.IsLinux());
    }
}
