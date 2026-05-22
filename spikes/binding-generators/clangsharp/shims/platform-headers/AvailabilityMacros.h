/*
 * Spike-only shim for Windows-local ClangSharp Apple platform-view parsing.
 * Real macOS/iOS generation must use the Apple SDK header instead.
 */
#ifndef JANSET_SDL2_SPIKE_AVAILABILITY_MACROS_H_
#define JANSET_SDL2_SPIKE_AVAILABILITY_MACROS_H_

#ifndef MAC_OS_X_VERSION_10_7
#define MAC_OS_X_VERSION_10_7 1070
#endif

#ifndef MAC_OS_X_VERSION_MIN_REQUIRED
#define MAC_OS_X_VERSION_MIN_REQUIRED MAC_OS_X_VERSION_10_7
#endif

#endif
