#if !NET462
using System.Runtime.CompilerServices;
#endif
using System.Runtime.InteropServices;
using Janset.SDL2.AbiTests.Infrastructure.Classification;
using Janset.SDL2.AbiTests.Infrastructure.Sdl;
using Janset.SDL2.AbiTests.Infrastructure.Sdl.Scopes;
using SDL2;
using static SDL2.SDLNative;

namespace Janset.SDL2.AbiTests.Upstream.GlobalState;

public sealed class HintsAbiTests
{
    [Test]
    [NotInParallel(AbiParallelKeys.Hints)]
    [Category(AbiCategories.Smoke)]
    [Category(AbiCategories.Mechanical)]
    [Category(AbiCategories.GlobalState)]
    [Category(AbiCategories.SdlHints)]
    public async Task SDLSetHint_Should_RoundTrip_Custom_Test_Hint()
    {
        using SdlHintScope hint = new("JANSET_SDL2_ABI_TEST_HINT", "enabled");

        await Assert.That(hint.CurrentValue).IsEqualTo("enabled");
    }

    [Test]
    [NotInParallel(AbiParallelKeys.Hints)]
    [Category(AbiCategories.UpstreamPort)]
    [Category(AbiCategories.Mechanical)]
    [Category(AbiCategories.GlobalState)]
    [Category(AbiCategories.SdlHints)]
    [UpstreamSdlTest("test/testautomation_hints.c", "hints_setHint")]
    public async Task SDLSetHintWithPriority_Should_Respect_Environment_Default_Override_And_Reset()
    {
        const string testHint = "SDL_AUTOMATED_TEST_HINT";

        SdlEnvironmentScope environment = new(testHint, "original");
        try
        {
            ResetHint(testHint);

            await Assert.That(GetHint(testHint)).IsEqualTo("original");

            SetHint(testHint, "temp");
            SetHint(testHint, "original");
            await Assert.That(GetHint(testHint)).IsEqualTo("original");

            SetHintWithPriority(testHint, null, SDL_HintPriority.SDL_HINT_DEFAULT);
            await Assert.That(GetHint(testHint)).IsEqualTo("original");

            SetHintWithPriority(testHint, "temp", SDL_HintPriority.SDL_HINT_OVERRIDE);
            await Assert.That(GetHint(testHint)).IsEqualTo("temp");

            SetHintWithPriority(testHint, null, SDL_HintPriority.SDL_HINT_OVERRIDE);
            await Assert.That(GetHint(testHint)).IsNull();

            ResetHint(testHint);
            await Assert.That(GetHint(testHint)).IsEqualTo("original");
        }
        finally
        {
            environment.Dispose();
            ResetHint(testHint);
        }
    }

    [Test]
    [NotInParallel(AbiParallelKeys.Hints)]
    [Category(AbiCategories.UpstreamPort)]
    [Category(AbiCategories.Mechanical)]
    [Category(AbiCategories.GlobalState)]
    [Category(AbiCategories.SdlHints)]
    [UpstreamSdlTest("test/testautomation_hints.c", "hints_setHint")]
    public async Task SDLAddHintCallback_Should_Invoke_After_Reset_And_Stop_After_Delete()
    {
        const string testHint = "SDL_AUTOMATED_TEST_HINT";

        SdlEnvironmentScope environment = new(testHint, "original");
        try
        {
            ResetHint(testHint);
            HintCallbackState state = new();
            GCHandle stateHandle = GCHandle.Alloc(state);

            try
            {
                AddHintCallback(testHint, GCHandle.ToIntPtr(stateHandle));
                await Assert.That(state.LastValue).IsEqualTo("original");

                state.LastValue = null;
                SetHintWithPriority(testHint, "temp", SDL_HintPriority.SDL_HINT_OVERRIDE);
                await Assert.That(state.LastValue).IsEqualTo("temp");

                state.LastValue = null;
                ResetHint(testHint);
                await Assert.That(state.LastValue).IsEqualTo("original");

                state.LastValue = null;
                SetHintWithPriority(testHint, "temp", SDL_HintPriority.SDL_HINT_OVERRIDE);
                await Assert.That(state.LastValue).IsEqualTo("temp");

                state.LastValue = null;
                DeleteHintCallback(testHint, GCHandle.ToIntPtr(stateHandle));
                ResetHint(testHint);
                await Assert.That(state.LastValue).IsNull();
            }
            finally
            {
                DeleteHintCallback(testHint, GCHandle.ToIntPtr(stateHandle));
                stateHandle.Free();
            }
        }
        finally
        {
            environment.Dispose();
            ResetHint(testHint);
        }
    }

    private static unsafe string? GetHint(string name)
    {
        using PinnedUtf8 pinnedName = SdlUtf8.Pin(name);
        byte* value = SDL_GetHint(pinnedName.Pointer);

        return value is null ? null : SdlUtf8.FromNullTerminated(value);
    }

    private static unsafe void SetHint(string name, string value)
    {
        using PinnedUtf8 pinnedName = SdlUtf8.Pin(name);
        using PinnedUtf8 pinnedValue = SdlUtf8.Pin(value);

        SDL_SetHint(pinnedName.Pointer, pinnedValue.Pointer);
    }

    private static unsafe void SetHintWithPriority(string name, string? value, SDL_HintPriority priority)
    {
        using PinnedUtf8 pinnedName = SdlUtf8.Pin(name);
        using PinnedUtf8? pinnedValue = value is null ? null : SdlUtf8.Pin(value);

        byte* valuePointer = pinnedValue is null ? null : pinnedValue.Pointer;
        SDL_SetHintWithPriority(pinnedName.Pointer, valuePointer, priority);
    }

    private static unsafe void ResetHint(string name)
    {
        using PinnedUtf8 pinnedName = SdlUtf8.Pin(name);
        SDL_ResetHint(pinnedName.Pointer);
    }

    private static unsafe void AddHintCallback(string name, nint userdata)
    {
        using PinnedUtf8 pinnedName = SdlUtf8.Pin(name);

#if NET462
        SDL_AddHintCallback(pinnedName.Pointer, HintChangedCallbackPointer, userdata);
#else
        SDL_AddHintCallback(pinnedName.Pointer, &HintChanged, userdata);
#endif
    }

    private static unsafe void DeleteHintCallback(string name, nint userdata)
    {
        using PinnedUtf8 pinnedName = SdlUtf8.Pin(name);

#if NET462
        SDL_DelHintCallback(pinnedName.Pointer, HintChangedCallbackPointer, userdata);
        GC.KeepAlive(HintChangedCallback);
#else
        SDL_DelHintCallback(pinnedName.Pointer, &HintChanged, userdata);
#endif
    }

#if NET462
    private static readonly Delegate HintChangedCallback = CreateHintChangedCallback();
    private static readonly IntPtr HintChangedCallbackPointer = Marshal.GetFunctionPointerForDelegate(HintChangedCallback);

    private static unsafe Delegate CreateHintChangedCallback() => (SDL_HintCallback)HintChanged;
#else
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
#endif
    private static unsafe void HintChanged(nint userdata, byte* name, byte* oldValue, byte* hint)
    {
        GCHandle handle = GCHandle.FromIntPtr(userdata);
        ((HintCallbackState)handle.Target!).LastValue = hint is null ? null : SdlUtf8.FromNullTerminated(hint);
    }

    private sealed class HintCallbackState
    {
        public string? LastValue { get; set; }
    }
}
