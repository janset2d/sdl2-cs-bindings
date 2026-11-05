using Build.Tests.Fixtures.RepositoryGates;

namespace Build.Tests.Fixtures;

[AttributeUsage(AttributeTargets.Assembly | AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class GitWorkTreeOnlyAttribute : SkipAttribute
{
    public GitWorkTreeOnlyAttribute()
        : base("Requires a git work tree")
    {
    }

    public override Task<bool> ShouldSkip(TestRegisteredContext context)
    {
        return Task.FromResult(!RepositoryFiles.IsAvailable
            && Environment.GetEnvironmentVariable("GITHUB_ACTIONS") != "true");
    }
}
