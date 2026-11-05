namespace Janset.SDL2.AbiTests.Infrastructure.Sdl.Requirements;

[AttributeUsage(AttributeTargets.Assembly | AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class RequiresAudioDummyDriverAttribute : SkipAttribute
{
    public RequiresAudioDummyDriverAttribute()
        : base(SdlRuntimeProbe.MissingAudioDriverMessage("dummy"))
    {
    }

    public override Task<bool> ShouldSkip(TestRegisteredContext context)
    {
        return Task.FromResult(!SdlRuntimeProbe.HasAudioDriver("dummy"));
    }
}
