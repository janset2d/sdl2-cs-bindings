#if !NET462
using Janset.SDL2.AbiTests.Infrastructure.Sdl.Callbacks;
#else
using Janset.SDL2.AbiTests.Infrastructure.Callbacks;
#endif
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
    public async Task SDLAddEventWatch_Should_Invoke_Callback_For_User_Event()
    {
        EventWatchResult result = PushUserEventThroughWatch();

        await Assert.That(result.PushResult).IsEqualTo(1);
        await Assert.That(result.WatchCount).IsEqualTo(1);
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

        using SdlEventWatchCounter watchCounter = SdlEventWatchCounter.ForUserEvents();
        int pushResult;

        try
        {
            SDL_Event pushed = CreateUserEvent(24);
            pushResult = SDL_PushEvent(&pushed);
        }
        finally
        {
            SDL_FlushEvents(userEventType, userEventType);
        }

        return new EventWatchResult(pushResult, watchCounter.Count);
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

    private sealed record EventPollResult(int PushResult, int PeepResult, uint EventType, int Code);

    private sealed record EventWatchResult(int PushResult, int WatchCount);
}
