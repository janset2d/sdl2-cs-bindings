/*
 * Spike-only shim for Windows-local ClangSharp platform-view parsing.
 * Real Linux generation must use the native system header instead.
 */
#ifndef JANSET_SDL2_SPIKE_ENDIAN_H_
#define JANSET_SDL2_SPIKE_ENDIAN_H_

#ifndef __LITTLE_ENDIAN
#define __LITTLE_ENDIAN 1234
#endif

#ifndef __BIG_ENDIAN
#define __BIG_ENDIAN 4321
#endif

#ifndef __BYTE_ORDER
#define __BYTE_ORDER __LITTLE_ENDIAN
#endif

#ifndef LITTLE_ENDIAN
#define LITTLE_ENDIAN __LITTLE_ENDIAN
#endif

#ifndef BIG_ENDIAN
#define BIG_ENDIAN __BIG_ENDIAN
#endif

#ifndef BYTE_ORDER
#define BYTE_ORDER __BYTE_ORDER
#endif

#endif
