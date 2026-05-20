typedef unsigned int Uint32;

extern int SDL_AlwaysAvailable(Uint32 flags);

#if defined(JANSET_TEST_LINUX_VIEW)
extern int SDL_LinuxOnly(Uint32 flags);
#endif

#if defined(JANSET_TEST_WINDOWS_VIEW)
extern int SDL_WindowsOnly(Uint32 flags);
#endif
