#if !NET462
using System.Runtime.CompilerServices;
#endif
using System.Runtime.InteropServices;
using Janset.SDL2.AbiTests.Infrastructure.Classification;
using Janset.SDL2.AbiTests.Infrastructure.Sdl.Scopes;
using SDL2;
using static SDL2.SDLNative;

namespace Janset.SDL2.AbiTests.Upstream.GlobalState;

public sealed class EventFilterCallbackAbiTests
{
    private const int AllowedUserEventCode = 1001;
    private const int RejectedUserEventCode = 1002;

    [Test]
    [NotInParallel(AbiParallelKeys.Events)]
    [Category(AbiCategories.Smoke)]
    [Category(AbiCategories.Mechanical)]
    [Category(AbiCategories.GlobalState)]
    [Category(AbiCategories.SdlEvents)]
    public async Task SDLSetEventFilter_Should_RoundTrip_Userdata_And_Filter_User_Events()
    {
        EventFilterResult result = PushUserEventsThroughFilter();

        await Assert.That(result.AllowedPushResult).IsEqualTo(1);
        await Assert.That(result.RejectedPushResult).IsEqualTo(0);
        await Assert.That(result.CallbackCount).IsGreaterThanOrEqualTo(2);
        await Assert.That(result.SawUserdata).IsTrue();
        await Assert.That(result.SawAllowedEvent).IsTrue();
        await Assert.That(result.SawRejectedEvent).IsTrue();
        await Assert.That(result.PeepResult).IsEqualTo(1);
        await Assert.That(result.PeepedCode).IsEqualTo(AllowedUserEventCode);
        await Assert.That(result.GetResult).IsEqualTo(1);
        await Assert.That(result.GetCode).IsEqualTo(AllowedUserEventCode);
        await Assert.That(result.RemainingUserEventCount).IsEqualTo(0);
    }

    private static unsafe EventFilterResult PushUserEventsThroughFilter()
    {
        using SdlSubsystemScope events = new(SDL_INIT_EVENTS);

        uint userEventType = (uint)SDL_EventType.SDL_USEREVENT;
        SDL_FlushEvents(userEventType, userEventType);

        EventFilterState state = new();
        GCHandle stateHandle = GCHandle.Alloc(state);
        nint userdata = GCHandle.ToIntPtr(stateHandle);

        try
        {
            SetEventFilter(userdata);

            SDL_Event allowedEvent = CreateUserEvent(AllowedUserEventCode);
            int allowedPushResult = SDL_PushEvent(&allowedEvent);

            SDL_Event rejectedEvent = CreateUserEvent(RejectedUserEventCode);
            int rejectedPushResult = SDL_PushEvent(&rejectedEvent);

            SDL_Event peeked = default;
            int peepResult = SDL_PeepEvents(&peeked, 1, SDL_eventaction.SDL_PEEKEVENT, userEventType, userEventType);

            SDL_Event received = default;
            int getResult = SDL_PeepEvents(&received, 1, SDL_eventaction.SDL_GETEVENT, userEventType, userEventType);

            SDL_Event remaining = default;
            int remainingUserEventCount = SDL_PeepEvents(&remaining, 1, SDL_eventaction.SDL_GETEVENT, userEventType, userEventType);

            return new EventFilterResult(
                allowedPushResult,
                rejectedPushResult,
                state.CallbackCount,
                state.SawUserdata,
                state.SawAllowedEvent,
                state.SawRejectedEvent,
                peepResult,
                peeked.user.code,
                getResult,
                received.user.code,
                remainingUserEventCount);
        }
        finally
        {
            ClearEventFilter();
            stateHandle.Free();
            SDL_FlushEvents(userEventType, userEventType);
        }
    }

    private static unsafe void SetEventFilter(nint userdata)
    {
#if NET462
        SDL_SetEventFilter(EventFilterCallbackPointer, userdata);
#else
        SDL_SetEventFilter(&EventFilterCallback, userdata);
#endif
    }

    private static unsafe void ClearEventFilter()
    {
#if NET462
        SDL_SetEventFilter(IntPtr.Zero, 0);
        GC.KeepAlive(EventFilterCallbackDelegate);
#else
        SDL_SetEventFilter(null, 0);
#endif
    }

#if NET462
    private static readonly Delegate EventFilterCallbackDelegate = CreateEventFilterCallbackDelegate();
    private static readonly IntPtr EventFilterCallbackPointer = Marshal.GetFunctionPointerForDelegate(EventFilterCallbackDelegate);

    private static unsafe Delegate CreateEventFilterCallbackDelegate() => (SDL_EventFilter)EventFilterCallback;
#else
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
#endif
    private static unsafe int EventFilterCallback(nint userdata, SDL_Event* @event)
    {
        GCHandle handle = GCHandle.FromIntPtr(userdata);
        EventFilterState state = (EventFilterState)handle.Target!;

        state.CallbackCount++;
        state.SawUserdata = true;

        if (@event->type != (uint)SDL_EventType.SDL_USEREVENT)
        {
            return 1;
        }

        if (@event->user.code == AllowedUserEventCode)
        {
            state.SawAllowedEvent = true;
            return 1;
        }

        if (@event->user.code == RejectedUserEventCode)
        {
            state.SawRejectedEvent = true;
        }

        return 0;
    }

    private static SDL_Event CreateUserEvent(int code)
    {
        uint userEventType = (uint)SDL_EventType.SDL_USEREVENT;
        SDL_Event @event = default;
        @event.type = userEventType;
        @event.user.type = userEventType;
        @event.user.code = code;

        return @event;
    }

    private sealed record EventFilterResult(
        int AllowedPushResult,
        int RejectedPushResult,
        int CallbackCount,
        bool SawUserdata,
        bool SawAllowedEvent,
        bool SawRejectedEvent,
        int PeepResult,
        int PeepedCode,
        int GetResult,
        int GetCode,
        int RemainingUserEventCount);

    private sealed class EventFilterState
    {
        public int CallbackCount { get; set; }

        public bool SawUserdata { get; set; }

        public bool SawAllowedEvent { get; set; }

        public bool SawRejectedEvent { get; set; }
    }
}
