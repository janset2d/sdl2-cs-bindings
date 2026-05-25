#if !NET462
using System.Runtime.CompilerServices;
#endif
using System.Runtime.InteropServices;
using Janset.SDL2.AbiTests.Infrastructure.Classification;
using Janset.SDL2.AbiTests.Infrastructure.Sdl.Scopes;
using SDL2;
using static SDL2.SDLNative;

namespace Janset.SDL2.AbiTests.Upstream.GlobalState;

public sealed class EventsAbiTests
{
    [Test]
    [NotInParallel(AbiParallelKeys.Events)]
    [Category(AbiCategories.UpstreamPort)]
    [Category(AbiCategories.GlobalState)]
    [Category(AbiCategories.SdlEvents)]
    [UpstreamSdlTest("test/testautomation_events.c", "events_pushPumpAndPollUserevent")]
    public async Task SDLPushEvent_Should_Peep_Deterministic_User_Event()
    {
        EventPollResult result = PushAndReadUserEvent();

        await Assert.That(result.PushResult).IsEqualTo(1);
        await Assert.That(result.PeepResult).IsEqualTo(1);
        await Assert.That(result.EventType).IsEqualTo((uint)SDL_EventType.SDL_USEREVENT);
        await Assert.That(result.Code).IsEqualTo(42);
    }

    [Test]
    [NotInParallel(AbiParallelKeys.Events)]
    [Category(AbiCategories.UpstreamPort)]
    [Category(AbiCategories.GlobalState)]
    [Category(AbiCategories.SdlEvents)]
    [UpstreamSdlTest("test/testautomation_events.c", "events_addDelEventWatch")]
    public async Task SDLAddEventWatch_Should_Invoke_Callback_With_Null_Userdata_And_Stop_After_Delete()
    {
        EventWatchResult result = PushUserEventThroughWatch();

        await Assert.That(result.FirstPushResult).IsEqualTo(1);
        await Assert.That(result.FirstWatchCount).IsEqualTo(1);
        await Assert.That(result.SecondPushResult).IsEqualTo(1);
        await Assert.That(result.SecondWatchCount).IsEqualTo(0);
    }

    [Test]
    [NotInParallel(AbiParallelKeys.Events)]
    [Category(AbiCategories.UpstreamPort)]
    [Category(AbiCategories.GlobalState)]
    [Category(AbiCategories.SdlEvents)]
    [UpstreamSdlTest("test/testautomation_events.c", "events_addDelEventWatchWithUserdata")]
    public async Task SDLAddEventWatch_Should_Invoke_Callback_With_Userdata_And_Stop_After_Delete()
    {
        EventWatchWithUserdataResult result = PushUserEventThroughUserdataWatch();

        await Assert.That(result.FirstPushResult).IsEqualTo(1);
        await Assert.That(result.FirstWatchCount).IsEqualTo(1);
        await Assert.That(result.ObservedUserdataValue).IsEqualTo(7);
        await Assert.That(result.SecondPushResult).IsEqualTo(1);
        await Assert.That(result.SecondWatchCount).IsEqualTo(0);
    }

    private static unsafe EventPollResult PushAndReadUserEvent()
    {
        using SdlSubsystemScope events = new(SDL_INIT_EVENTS);

        uint userEventType = (uint)SDL_EventType.SDL_USEREVENT;
        SDL_FlushEvents(userEventType, userEventType);

        SDL_Event pushed = CreateUserEvent(42);
        int pushResult = SDL_PushEvent(&pushed);

        SDL_Event received = default;
        int peepResult = SDL_PeepEvents(&received, 1, SDL_eventaction.SDL_GETEVENT, userEventType, userEventType);

        SDL_FlushEvents(userEventType, userEventType);

        return new EventPollResult(pushResult, peepResult, received.type, received.user.code);
    }

    private static unsafe EventWatchResult PushUserEventThroughWatch()
    {
        using SdlSubsystemScope events = new(SDL_INIT_EVENTS);

        uint userEventType = (uint)SDL_EventType.SDL_USEREVENT;
        SDL_FlushEvents(userEventType, userEventType);

        NullUserdataWatchCount = 0;

        try
        {
            AddNullUserdataEventWatch();

            SDL_Event firstEvent = CreateUserEvent(24);
            int firstPushResult = SDL_PushEvent(&firstEvent);
            SDL_PumpEvents();
            SDL_FlushEvents(userEventType, userEventType);

            int firstWatchCount = NullUserdataWatchCount;

            DeleteNullUserdataEventWatch();
            NullUserdataWatchCount = 0;

            SDL_Event secondEvent = CreateUserEvent(24);
            int secondPushResult = SDL_PushEvent(&secondEvent);
            SDL_PumpEvents();
            SDL_FlushEvents(userEventType, userEventType);

            return new EventWatchResult(firstPushResult, firstWatchCount, secondPushResult, NullUserdataWatchCount);
        }
        finally
        {
            DeleteNullUserdataEventWatch();
            NullUserdataWatchCount = 0;
            SDL_FlushEvents(userEventType, userEventType);
        }
    }

    private static unsafe EventWatchWithUserdataResult PushUserEventThroughUserdataWatch()
    {
        using SdlSubsystemScope events = new(SDL_INIT_EVENTS);

        uint userEventType = (uint)SDL_EventType.SDL_USEREVENT;
        SDL_FlushEvents(userEventType, userEventType);

        EventWatchUserdataState state = new(7);
        GCHandle stateHandle = GCHandle.Alloc(state);
        nint userdata = GCHandle.ToIntPtr(stateHandle);

        try
        {
            AddUserdataEventWatch(userdata);

            SDL_Event firstEvent = CreateUserEvent(7);
            int firstPushResult = SDL_PushEvent(&firstEvent);
            SDL_PumpEvents();
            SDL_FlushEvents(userEventType, userEventType);

            int firstWatchCount = state.WatchCount;
            int? observedUserdataValue = state.ObservedUserdataValue;

            DeleteUserdataEventWatch(userdata);
            state.Reset();

            SDL_Event secondEvent = CreateUserEvent(7);
            int secondPushResult = SDL_PushEvent(&secondEvent);
            SDL_PumpEvents();
            SDL_FlushEvents(userEventType, userEventType);

            return new EventWatchWithUserdataResult(
                firstPushResult,
                firstWatchCount,
                observedUserdataValue,
                secondPushResult,
                state.WatchCount);
        }
        finally
        {
            DeleteUserdataEventWatch(userdata);
            stateHandle.Free();
            SDL_FlushEvents(userEventType, userEventType);
        }
    }

    private static unsafe void AddUserdataEventWatch(nint userdata)
    {
#if NET462
        SDL_AddEventWatch(UserdataEventWatchCallbackPointer, userdata);
#else
        SDL_AddEventWatch(&UserdataEventWatchCallback, userdata);
#endif
    }

    private static unsafe void DeleteUserdataEventWatch(nint userdata)
    {
#if NET462
        SDL_DelEventWatch(UserdataEventWatchCallbackPointer, userdata);
        GC.KeepAlive(UserdataEventWatchCallbackDelegate);
#else
        SDL_DelEventWatch(&UserdataEventWatchCallback, userdata);
#endif
    }

    private static unsafe void AddNullUserdataEventWatch()
    {
#if NET462
        SDL_AddEventWatch(NullUserdataEventWatchCallbackPointer, 0);
#else
        SDL_AddEventWatch(&NullUserdataEventWatchCallback, 0);
#endif
    }

    private static unsafe void DeleteNullUserdataEventWatch()
    {
#if NET462
        SDL_DelEventWatch(NullUserdataEventWatchCallbackPointer, 0);
        GC.KeepAlive(NullUserdataEventWatchCallbackDelegate);
#else
        SDL_DelEventWatch(&NullUserdataEventWatchCallback, 0);
#endif
    }

#if NET462
    private static readonly Delegate NullUserdataEventWatchCallbackDelegate = CreateNullUserdataEventWatchCallbackDelegate();
    private static readonly IntPtr NullUserdataEventWatchCallbackPointer = Marshal.GetFunctionPointerForDelegate(NullUserdataEventWatchCallbackDelegate);

    private static unsafe Delegate CreateNullUserdataEventWatchCallbackDelegate() => (SDL_EventFilter)NullUserdataEventWatchCallback;
#else
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
#endif
    private static unsafe int NullUserdataEventWatchCallback(nint userdata, SDL_Event* @event)
    {
        if (userdata == 0 && @event->type == (uint)SDL_EventType.SDL_USEREVENT)
        {
            NullUserdataWatchCount++;
        }

        return 0;
    }

#if NET462
    private static readonly Delegate UserdataEventWatchCallbackDelegate = CreateUserdataEventWatchCallbackDelegate();
    private static readonly IntPtr UserdataEventWatchCallbackPointer = Marshal.GetFunctionPointerForDelegate(UserdataEventWatchCallbackDelegate);

    private static unsafe Delegate CreateUserdataEventWatchCallbackDelegate() => (SDL_EventFilter)UserdataEventWatchCallback;
#else
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
#endif
    private static unsafe int UserdataEventWatchCallback(nint userdata, SDL_Event* @event)
    {
        if (@event->type == (uint)SDL_EventType.SDL_USEREVENT)
        {
            GCHandle handle = GCHandle.FromIntPtr(userdata);
            EventWatchUserdataState state = (EventWatchUserdataState)handle.Target!;
            state.WatchCount++;
            state.ObservedUserdataValue = state.ExpectedUserdataValue;
        }

        return 0;
    }

    private static int NullUserdataWatchCount { get; set; }

    private static SDL_Event CreateUserEvent(int code)
    {
        uint userEventType = (uint)SDL_EventType.SDL_USEREVENT;
        SDL_Event @event = default;
        @event.type = userEventType;
        @event.user.type = userEventType;
        @event.user.code = code;

        return @event;
    }

    private sealed record EventPollResult(int PushResult, int PeepResult, uint EventType, int Code);

    private sealed record EventWatchResult(
        int FirstPushResult,
        int FirstWatchCount,
        int SecondPushResult,
        int SecondWatchCount);

    private sealed record EventWatchWithUserdataResult(
        int FirstPushResult,
        int FirstWatchCount,
        int? ObservedUserdataValue,
        int SecondPushResult,
        int SecondWatchCount);

    private sealed class EventWatchUserdataState(int expectedUserdataValue)
    {
        public int ExpectedUserdataValue { get; } = expectedUserdataValue;

        public int WatchCount { get; set; }

        public int? ObservedUserdataValue { get; set; }

        public void Reset()
        {
            WatchCount = 0;
            ObservedUserdataValue = null;
        }
    }
}
