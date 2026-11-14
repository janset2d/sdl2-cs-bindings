# Per-Header RSP Files

Third RSP tier for symbol-local ClangSharp concerns (R6 tag/typedef remaps, R2 stdinc excludes). Cross-cutting policy lives in `../base.rsp`; family identity lives in the family files beside it (`../sdl2-core.rsp`, `../sdl2-image.rsp`, and so on).

Naming convention: `<SDL_header_basename>.rsp`, matching the SDL header filename without the `.h` extension. For example, entries that target `SDL_hidapi.h` go in `SDL_hidapi.rsp`; entries targeting `SDL_mutex.h` go in `SDL_mutex.rsp`. The `GenerateBindings` Cake target loads `per-header/<basename>.rsp` for each header it generates if the file exists; the manifest's `binding_generation.per_header_rsp_dir` names this folder.

The tiers compose additively: an entry here cannot override a keyed entry from another tier. [RSP maintenance](../../../../docs/engineering/binding-generator-maintenance.md#rsp-maintenance) owns the composition rules and the review of each entry.
