namespace Janset.SDL2.AbiTests.Infrastructure.Sdl.Requirements;

[AttributeUsage(AttributeTargets.Assembly | AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class RequiresVideoDummyDriverAttribute : SkipAttribute
{
    public RequiresVideoDummyDriverAttribute()
        : base(SdlRuntimeProbe.MissingVideoDriverMessage("dummy"))
    {
    }

    public override Task<bool> ShouldSkip(TestRegisteredContext context)
    {
        return Task.FromResult(!SdlRuntimeProbe.HasVideoDriver("dummy"));
    }
}
