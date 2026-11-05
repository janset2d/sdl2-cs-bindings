# Per-Header RSP Files

Third RSP tier for symbol-local ClangSharp concerns (R6 tag/typedef remaps, R2 stdinc excludes, future per-header overrides). Cross-cutting policy lives in `../base.rsp`; family identity lives in `../sdl2-core.rsp` / `../sdl2-image.rsp`.

Naming convention: `<SDL_header_basename>.rsp`, matching the SDL header filename without the `.h` extension. For example, entries that target `SDL_hidapi.h` go in `SDL_hidapi.rsp`; entries targeting `SDL_mutex.h` go in `SDL_mutex.rsp`. The orchestrator loads `per-header/<basename>.rsp` automatically for each header it generates if the file exists.

Design rationale and the three-tier layout are documented in `spikes/binding-generators/docs/priority-c-closure-summary.md` and `spikes/binding-generators/docs/next-iteration-plan.md`.
