#pragma once
// Stub <process.h>: SDL_thread.h:39 pulls this on __WIN32__ for _beginthreadex /
// _endthreadex declarations. We never bind those symbols; an empty include lets
// SDL_thread.h parse under the WindowsDesktop / WinRT / GDK views without a real
// Windows SDK. Peer reference: ppy/SDL3-CS ships the same stub at
// SDL3-CS/include/process.h.
