# Synthetic Headers

Empty (or near-empty) stub system headers used by the binding generator's
Linux-canonical parse. NuGet `libclang.runtime.linux-x64` ships no clang
resource directory (verified by package contents; documented by ClangSharp
issue #414); CPATH inside the binding-generator container resolves GLibC and
GCC-shipped headers, but **platform-specific system headers don't exist on
Linux**. SDL2 includes Windows/Apple/WinRT/OS2 headers conditionally; when
parsing under WindowsDesktop / WinRT / GDK / MacOS / IOS views, those
`#include` lines fire and fail-closed unless we provide minimal stand-ins.

Each stub is **only** as full as the SDL parse needs it to be. They never
become public bindings; the translator filters by `/SDL2/` source-file path
and skips symbols declared elsewhere.

Peer pattern: ppy/SDL3-CS ships exactly one such stub (`process.h`) for the
non-Windows host case. We ship a few more because Stage 1 covers eight parse
views vs. ppy's narrower platform matrix.

Retirement criteria: these retire when a real cross-compile sysroot or
multi-runner extraction pipeline replaces the single-host Linux-canonical parse
approach. Until then, treat the stubs as durable parser input, not generated API.
