# Packaging Strategy — Independent Verdict (Claude the Second)

- **Date:** 2026-04-14
- **Author:** Claude (Opus 4.6, 1M context) — second independent pass, untouched by the other verdict drafts
- **Status:** Independent verdict / evidence report
- **Scope:** Packaging strategy validation, vcpkg/CMake feasibility, symbol visibility discipline, LGPL handling, build host evolution path, phase/doc redesign implications
- **Confidence:** High
- **Question:** Should Janset.SDL2 adopt Hybrid Static + Dynamic Core, and if so, what do the existing verdicts get wrong or underestimate?

## 0. About this document

This is an **independent second Claude verdict**. It was produced without reading the other temp verdicts (`-shared`, `-gemini`, `-grok`, `-claude` [the first], `-chatgpt`, `-copilot` synthesis) in order to avoid cross-contamination of conclusions. The input set was intentionally restricted to:

- The three canonical research docs in `docs/research/` (license inventory, hybrid-static, pure-dynamic)
- The execution model strategy draft (`execution-model-strategy-2026-04-13-shared.md`)
- The actual build host source code in `build/_build/`
- Canonical project docs (`AGENTS.md`, `docs/onboarding.md`, `docs/plan.md`)
- Four parallel external research passes (vcpkg feasibility, .NET native-binding ecosystem, symbol visibility, SDL_mixer dlopen mechanism)

Other temp verdicts are referenced as **pointers**, not as sources. When this doc converges with them, it converges from independent reasoning; when it diverges, the divergence is intentional.

The first Claude verdict (`packaging-strategy-verdict-2026-04-13-claude.md`) exists and is described in the temp README as "external-evidence-heavy, surfaces traps the other verdicts gloss over." This doc was written without opening it. If the two Claude verdicts converge, that's a good signal. If they diverge, both should be read and reconciled.

## 1. TL;DR

- **Hybrid Static + Dynamic Core is the correct strategic direction.** This is not just "the least bad of three options" — it's the dominant pattern in the mature .NET native binding ecosystem, and it is what the single closest architectural peer (ppy/SDL3-CS) already ships.
- **But the existing verdict docs carry six technical inaccuracies or underestimations** that, if the phases were written as-is, would cause hard failures in implementation. These need to be fixed before any canonical doc is rewritten.
- **The build host is in much better shape than it feels.** ~75-85% of the existing Cake Frosting code survives a hybrid refactor. Scanners, closure walker, vcpkg provider, artifact deployer, path service, runtime profile, task orchestration — all stay. The ArtifactPlanner evolves from "copy everything" to "validate then copy." One net-new service (`IDependencyPolicyValidator`) is required; PackageTask is still missing as a separate concern.
- **The LGPL calculus needs to be re-run** because the default SDL_mixer 2.x MP3 decoder is now `minimp3` (permissive, header-only), not mpg123. Option C (Extras package) may still be the right answer — or it may collapse into Option A (drop LGPL codecs entirely) with almost no user-facing loss.
- **The execution-model strategy doc (Source / Package Validation / Release modes, strategy ≠ asset source) is more than a workflow convenience.** It's a real architectural constraint that should land in the manifest schema and build host, not just the playbook.
- **Phase 2 cannot absorb this without getting bent out of shape.** Recommended: open a **Phase 2.5 "Hybrid Packaging Foundation"** between Phase 2 close-out and Phase 3 start, rather than renumbering everything or overloading Phase 2.

## 2. Why this decision matters right now

The project hit a structural wall that the pure-dynamic model created: satellite packages ship overlapping transitive DLLs (`zlib1.dll`, `libpng16.dll`, etc.), MSBuild resolves the collision via last-write-wins, and the resulting `bin/` folder is a silent runtime hazard. Issue #75 calls this out, and Q2 2026 roadmap has "Document and approve shared native dependency policy" as an explicit blocker item.

Choosing the wrong strategy here cascades into:

- Phase 3 (SDL2 Complete) — populating 7 RIDs × 6 libraries with the wrong model means throwing away that work
- Phase 4 (CppAst autogen) — generator public boundary choices depend on which symbols stay visible from satellites
- Phase 5 (SDL3) — doubling the problem if SDL3 family inherits whatever we lock in now

So: the pressure is correctly placed. This is not premature optimization; this is a load-bearing decision.

## 3. Scope and methodology

Four parallel external research passes were run:

1. **Build host architectural assessment** — exhaustive read of `build/_build/` source to map what's reusable vs. what changes under a hybrid model.
2. **vcpkg overlay triplet + selective linkage feasibility** — validated the `if(PORT MATCHES …)` per-port linkage mechanism against official Microsoft docs, vcpkg GitHub issues/discussions, and real-world overlay triplet repositories maintained over years.
3. **.NET native binding ecosystem comparative analysis** — what SkiaSharp, LibGit2Sharp, SQLitePCLRaw, Magick.NET, Alimer.Bindings.SDL, ppy/SDL3-CS, flibitijibibo/SDL2-CS, HarfBuzzSharp, Microsoft.ML.OnnxRuntime actually ship and how they build.
4. **Symbol visibility + SDL_mixer dlopen + SDL2 dual-static** — cross-referenced SDL_image / SDL_mixer / SDL_ttf upstream CMakeLists, vcpkg port scripts, SDL2 source for static globals, and CMake/linker documentation across all three OS families.

Each research pass was instructed to be skeptical of existing claims, not sycophantic. Findings that contradict the existing verdicts are highlighted explicitly in Section 8.

Findings are labeled:

- **[REPO]** — verified from this repository's code/config
- **[UPSTREAM]** — verified from external source code (SDL, vcpkg, etc.)
- **[DOCS]** — verified from official vendor documentation (Microsoft Learn, CMake docs, etc.)
- **[ECOSYSTEM]** — verified from published NuGet packages, real repositories, or long-running production use

## 4. The ecosystem verdict is unambiguous

### 4.1 What mature .NET native bindings actually ship

This was the single most clarifying piece of research. Every .NET native binding project with non-trivial transitive deps converges on the same pattern:

| Project | Transitive native deps | Ships separate transitive .dll/.so/.dylib? | Packaging model |
| --- | --- | :---: | --- |
| **ppy/SDL3-CS** (osu! framework) | libpng, libjpeg, zlib, libogg, libvorbis, libopus, freetype, harfbuzz | **No** | Single SDL3_image / SDL3_mixer per RID, vendored deps **[ECOSYSTEM]** |
| **SkiaSharp** | freetype, zlib, libpng, libwebp, libjpeg-turbo, expat, ICU | **No** | Single `libSkiaSharp.*` per RID **[ECOSYSTEM]** |
| **Magick.NET** | ~30 deps (freetype, zlib, libpng, libtiff, harfbuzz, cairo, glib…) | **No** | Single native per RID **[ECOSYSTEM]** |
| **HarfBuzzSharp** | ICU subset | **No** | Single `libHarfBuzzSharp.*` per RID **[ECOSYSTEM]** |
| **LibGit2Sharp** | zlib (+ optional ssh/tls) | **No** | Single native per RID with hash-suffix disambiguation **[ECOSYSTEM]** |
| **OnnxRuntime** | protobuf, Eigen, and more | **No** | Single native per RID **[ECOSYSTEM]** |
| **SQLitePCLRaw** | N/A (no transitive deps) | N/A | Plugin-style bundles **[ECOSYSTEM]** |

**The pattern is 7-0.** No mature .NET native binding project with many transitive deps ships those deps as separately resolvable files. They all bake statically into a single shared library per RID.

### 4.2 Why ppy/SDL3-CS is the most important data point

ppy/SDL3-CS is maintained by the osu! framework team — the largest shipping C# game codebase with SDL dependency. Their `External/build.sh` is the single most direct architectural peer to Janset.SDL2. Relevant CMake flags from their script **[ECOSYSTEM]**:

```
-DSDLIMAGE_VENDORED=ON
-DSDLIMAGE_DEPS_SHARED=OFF
-DSDLMIXER_VENDORED=ON
-DSDLMIXER_DEPS_SHARED=OFF
```

That is **exactly** the Hybrid Static + Dynamic Core pattern: SDL3 core dynamic (separate), all satellite codec deps static, one binary per satellite. They solved this problem already. This is the best piece of prior art available, and it should inform every detail of the implementation.

### 4.3 Patterns flagged as risks because nobody uses them

| Pattern | Prior art in .NET ecosystem |
| --- | :---: |
| Common.*.Native shared dep packages (Mechanism A, ~26 packages) | **Zero** |
| RPATH/$ORIGIN tricks for NuGet-distributed natives (Mechanism B) | **Zero** |
| Selective static bake (Mechanism C, only collision-prone deps baked) | **Zero** |
| Hybrid Static + Dynamic Core (everything permissive baked, LGPL dynamic opt-in) | **Multiple, including closest peer** |

This is a strong signal. Mechanism A and B are dismissible on this evidence alone. Mechanism C is interesting in isolation — but the only justification for it is "the collision list is small and stable forever," and there's no precedent that validates it as a long-term architecture.

## 5. vcpkg feasibility — validated with caveats

### 5.1 The `if(PORT MATCHES …)` mechanism is officially supported, not a hack

Confirmed from Microsoft's own triplet variables reference **[DOCS]** — there's a dedicated "Per-port customization" section with this exact example:

```cmake
set(VCPKG_LIBRARY_LINKAGE static)
if(PORT MATCHES "qt5-")
    set(VCPKG_LIBRARY_LINKAGE dynamic)
endif()
```

Additional evidence:

- Triplets concept page explicitly says: "you could have one triplet that builds openssl statically and zlib dynamically" **[DOCS]**
- `vcpkg_check_linkage` documentation uses this pattern in its own examples **[DOCS]**
- microsoft/vcpkg#15067 closed as completed with this pattern as the solution **[UPSTREAM]**
- Neumann-A/my-vcpkg-triplets has maintained 25+ custom triplets with this pattern for 4+ years (102 commits, still active) **[ECOSYSTEM]**
- microsoft/vcpkg#27043 (request for manifest-level linkage overrides) was closed "not planned" in Feb 2026, confirming custom triplets as the sanctioned path **[UPSTREAM]**

**Verdict:** The proposed selective linkage mechanism is production-grade infrastructure. No technical doubt.

### 5.2 CRT linkage — the boring-but-critical detail

The verdict docs get this right: `VCPKG_CRT_LINKAGE dynamic` stays constant across the triplet. Don't turn this into a static CRT experiment. `vcpkg_check_linkage` documentation explicitly warns: "Building a dynamic library with a static CRT creates conditions many developers find surprising… each DLL will get its own copy of the CRT." **[DOCS]**

Zero tolerance for CRT heroics. This should be codified in the canonical doc.

### 5.3 Mixed static + dynamic linking is well-understood

Confirmed [UPSTREAM]:

- None of the key transitive deps (zlib, libpng, freetype, harfbuzz, libjpeg, libwebp, libogg, libvorbis, libflac, opus) call `vcpkg_check_linkage(ONLY_DYNAMIC_LIBRARY)`. They all respect `VCPKG_LIBRARY_LINKAGE static` from a triplet.
- SDL2's vcpkg port respects `VCPKG_LIBRARY_LINKAGE` cleanly.
- The dep graph flows in one direction — zlib.lib does not "need" SDL2.dll, so resolving SDL2 symbols via import lib while pulling zlib symbols from static lib works correctly under a unified dynamic CRT.

### 5.4 The gotcha the verdict docs miss on the triplet regex

Looking at gemini.md's proposed triplet:

```cmake
if(PORT MATCHES "^(sdl2|mpg123|libxmp|fluidsynth)$")
    set(VCPKG_LIBRARY_LINKAGE dynamic)
endif()
```

This regex matches `sdl2` exactly. It does **not** match `sdl2-image`, `sdl2-mixer`, `sdl2-ttf`, `sdl2-gfx`, `sdl2-net`. So satellites are built **static** under this triplet.

Is that correct? It depends on the next step:

- If the plan is to use vcpkg's static satellite libs + a custom CMake wrapper project that statically absorbs them into a SHARED bundle DLL → **yes, this is intentional** (matches ppy/SDL3-CS pattern)
- If the plan is to use vcpkg's satellite output directly as the final shipped DLL → **no, satellites need to be dynamic**

The existing verdict docs are silent on which path is chosen. This needs to be made explicit. My recommendation: **use the bundle-wrapper path** because it matches ppy/SDL3-CS prior art and gives cleaner symbol control.

A smaller point, but worth capturing: the regex should probably also include `sdl3` when SDL3 family lands, and should have a documented "when adding a new LGPL-sensitive port, add it here too" comment.

### 5.5 The arm64 reality check

Three of the seven target RIDs are arm64 (win-arm64, linux-arm64, osx-arm64). vcpkg CI tests only nine main triplets, none of which include custom hybrid variants or community arm64 variants. **[DOCS]** vcpkg FAQ confirms this.

- `arm64-linux` and `arm64-osx` are **community triplets** — port breakage will be silent until the project's own CI catches it
- Historical evidence of fragility: microsoft/vcpkg#24241 (SDL2_image failed to link on arm64-osx, x64-osx, x64-linux with static deps due to CMake target file issues, fixed in #24248); microsoft/vcpkg#35507 (sdl2-mixer-ext arm64-osx failure) **[UPSTREAM]**
- vcpkg's Linux toolchain adds `-fPIC` unconditionally, which is good news — the arm64 relocation errors that plague some static-to-dynamic setups elsewhere are not a project concern here **[UPSTREAM]**

**Operational implication:** the CI matrix must validate custom hybrid triplets on all three arm64 RIDs. The cost of not doing this is "silent failure on baseline bump." This needs a dedicated phase item.

## 6. The symbol visibility story is much more subtle than the verdicts claim

This is the single biggest technical correction in this doc.

### 6.1 Windows export behavior is inverted relative to Linux/macOS

All three verdict docs (shared, gemini, grok) treat symbol visibility as one cross-platform discipline: "set `C_VISIBILITY_PRESET hidden`, use `--whole-archive`, done." This is wrong in the details:

- **Windows PE/COFF:** Nothing is exported by default. Exports are opt-in via `__declspec(dllexport)` or `.def` file. `C_VISIBILITY_PRESET hidden` is a **no-op** on MSVC. `/WHOLEARCHIVE` pulls in all object files but does **not** cause their symbols to be exported (Raymond Chen has a blog post explicitly on this). **[DOCS]** This means Windows is actually the safest platform for symbol hygiene — zlib's `deflate` will not leak from SDL2_image.dll even if you do nothing.
- **Linux ELF:** Everything is exported by default unless hidden. `-fvisibility=hidden` is a **compile-time** flag; it affects only the compilation units being compiled. When you statically link `libpng.a` (built without `-fvisibility=hidden`, because vcpkg doesn't pass that to port builds), `--whole-archive` pulls its objects in **and their symbols retain default visibility**. They will appear in the `.so`'s dynamic symbol table. **[DOCS]**
- **macOS Mach-O:** Same problem as Linux ELF. `-fvisibility=hidden` is compile-time only.

### 6.2 The mandatory additions the verdict docs miss

The correct cross-platform pattern is:

| Platform | Required addition to verdict doc recipe |
| --- | --- |
| Windows MSVC | Nothing additional — but satellite headers must use proper export macros (they do) |
| Windows MinGW | Safe with SDL's `DECLSPEC` macros; auto-export disabled once any `__declspec(dllexport)` is present |
| Linux | Add `-Wl,--exclude-libs,ALL` to linker flags — this treats static archive symbols as hidden in the dynamic symbol table |
| macOS | Add `-Wl,-exported_symbols_list,<file>` with an explicit list of `_IMG_*` / `_Mix_*` / `_TTF_*` / `_SDL_*` symbols, or use `-load_hidden` per static library |

**[DOCS]** This is documented in CMake docs, the macOS `ld` man page, Android NDK symbol visibility guide, and is used by real projects (LabJack's libLabJackM.so absorbs Boost with `-fvisibility=hidden` + ELF-specific flags; FFmpeg ships version scripts; Chromium uses `--exclude-libs,ALL` throughout).

### 6.3 SDL satellite upstream already does the right thing on the source side

Good news: SDL_image, SDL_mixer, and SDL_ttf upstream CMakeLists all set:

```cmake
DEFINE_SYMBOL DLL_EXPORT
C_VISIBILITY_PRESET "hidden"
```

and their headers use the `begin_code.h` export macro pattern correctly **[UPSTREAM]**. On Windows, `DECLSPEC` becomes `__declspec(dllexport)` when `DLL_EXPORT` is defined. On GCC/Clang, it becomes `__attribute__((visibility("default")))`. This means the SDL public API surface is already annotated — we just need to ensure the bundle wrapper build doesn't pollute it with transitive symbols.

### 6.4 CMake 3.24+ generator expression is the portable answer

Instead of mixing `-Wl,--whole-archive`, `-Wl,-force_load`, and `/WHOLEARCHIVE` manually across three OSes:

```cmake
target_link_libraries(SDL2_image_bundle PRIVATE
    SDL2::SDL2
    $<LINK_LIBRARY:WHOLE_ARCHIVE,SDL2_image::SDL2_image-static>
    $<LINK_LIBRARY:WHOLE_ARCHIVE,PNG::PNG>
    # ...
)
```

CMake 3.24 resolves this to the correct flag per platform (Linux BSD Apple Windows MSVC Cygwin MSYS all supported). **[DOCS]** The verdict docs should standardize on this pattern.

### 6.5 CI validation for symbol hygiene

This is a net-new capability the build host should gain. Deferred, but specified:

| Tool | Platform | Use |
| --- | --- | --- |
| `dumpbin /exports foo.dll` | Windows | Assert export list matches the allowlist |
| `nm -D --defined-only libfoo.so` | Linux | Assert only `IMG_*`/`Mix_*`/`TTF_*`/`SDL_*` in `T ` symbols |
| `objdump -T libfoo.so` | Linux | Alternative Linux check |
| `nm -gU libfoo.dylib` | macOS | Same assertion for Mach-O exports |

Recommended home: a new `ISymbolVisibilityValidator` service in the Cake host, invoked after the bundle build, before harvest. Failure mode: unexpected exported symbol → hard build failure. This naturally slots in between `BinaryClosureWalker` and `ArtifactPlanner` in the proposed `IDependencyPolicyValidator` pipeline.

## 7. The SDL_mixer dlopen story — and the vcpkg overlay port problem

### 7.1 The dlopen mechanism is real and cross-platform

Confirmed from `libsdl-org/SDL_mixer` CMakeLists.txt (SDL2 branch) **[UPSTREAM]**:

```
SDL2MIXER_DEPS_SHARED            default ON
SDL2MIXER_MP3_MPG123_SHARED      default = ${SDL2MIXER_DEPS_SHARED}
SDL2MIXER_MIDI_FLUIDSYNTH_SHARED default = ${SDL2MIXER_DEPS_SHARED}
SDL2MIXER_MOD_XMP_SHARED         default = ${SDL2MIXER_DEPS_SHARED}
```

When `*_SHARED=ON`, SDL_mixer compiles with `MPG123_DYNAMIC="libmpg123-0.dll"` (or platform equivalent), and in `src/codecs/music_mpg123.c`:

```c
#ifdef MPG123_DYNAMIC
    mpg123.handle = SDL_LoadObject(MPG123_DYNAMIC);
    if (mpg123.handle == NULL) {
        return -1;   // graceful failure
    }
#endif
```

Failure mode is confirmed graceful: `Mix_Init(MIX_INIT_MP3)` returns without the MP3 bit set, `Mix_LoadMUS(<mp3>)` returns NULL with `Mix_SetError("Unrecognized audio format")`. **The library does not crash or fail to initialize.** **[UPSTREAM]**

The same pattern holds on Linux (`dlopen` via `SDL_LoadObject`) and macOS (also `dlopen`). Fully symmetric. **[UPSTREAM]**

SDL3_mixer preserves the same mechanism — option names change to `SDLMIXER_*` but the runtime design is identical. **[UPSTREAM]**

### 7.2 The bomb the vcpkg port drops on the plan

vcpkg's `sdl2-mixer` portfile hardcodes **[UPSTREAM]**:

```cmake
-DSDL2MIXER_DEPS_SHARED=OFF
```

This disables the entire dlopen pattern. Under the default vcpkg port, mpg123/fluidsynth/libxmp are **load-time dependencies** (DT_NEEDED on ELF, import lib on Windows). If the LGPL DLL isn't present, `LoadLibrary`/`dlopen` of `SDL2_mixer.dll` itself **fails immediately** — no graceful "MP3 not supported," just a hard load-time failure.

This is a **direct contradiction** of the LGPL strategy proposed in every existing verdict. None of them caught this. The Janset.SDL2.Mixer.Extras.Native plan cannot work with the stock vcpkg port.

**Fix required:** a vcpkg **overlay port** for `sdl2-mixer` that passes `-DSDL2MIXER_DEPS_SHARED=ON` (or at least enables the per-codec `_SHARED` flags for the LGPL codecs). This means the project commits to maintaining not just a custom overlay triplet, but also at least one overlay port. Overlay ports must survive vcpkg baseline bumps — this is an ongoing maintenance cost.

### 7.3 The reframe: minimp3 changes the LGPL math

This is the finding that may quietly rewrite the LGPL decision entirely.

SDL_mixer 2.x has **multiple MP3 backends**, selectable at build time **[UPSTREAM]**:

- `SDL2MIXER_MP3_MINIMP3` — `minimp3`, permissive, header-only, no external dep. **ON by default.**
- `SDL2MIXER_MP3_MPG123` — mpg123, LGPL, external. **OFF by default.**

Modern vcpkg builds can ship MP3 support entirely via minimp3 with zero LGPL exposure. The "we must ship mpg123 for MP3" assumption that every verdict doc implicitly carries is **based on old SDL_mixer versions**. This deserves independent verification against the specific vcpkg port version, but if confirmed it changes the entire LGPL calculus:

- MIDI via `fluidsynth` — **LGPL** — can be replaced by `timidity` (permissive, built-in in SDL_mixer)
- Tracker music via `libxmp` — LGPL on 4.x (the license-inventory says so), but check whether the license has changed in newer versions. If still LGPL, can be partially replaced by `libmodplug` (public domain, already in our feature set)
- MP3 via mpg123 — **LGPL, but not needed if minimp3 is enabled**

So the actual LGPL pain point may be **just fluidsynth for high-quality MIDI**. Everything else has a permissive substitute already present in our feature set.

**Strategic implication:** Option A (drop LGPL codecs entirely) may be more defensible than the existing docs imply. The user-visible loss is "MIDI quality drop from fluidsynth-SoundFont to timidity-GUS-patches" — significant for some users, invisible for most game audio pipelines. If Option A is adopted, the whole `Mixer.Extras.Native` package topology disappears, the overlay port becomes unnecessary, and the architecture simplifies by one full dimension.

I'm not recommending Option A outright — but I am saying Option C's value drops substantially if MP3/tracker can be handled permissively. This deserves an explicit re-decision in the redesigned phase docs.

## 8. SDL2 dual-static would absolutely break — validated from source

Every verdict doc claims this; I want to confirm it's not folklore.

Verified from `libsdl-org/SDL` source **[UPSTREAM]**:

- `src/SDL.c`: file-scope `static Uint8 SDL_SubsystemRefCount[32]`, `static SDL_bool SDL_MainIsReady`, `static SDL_bool SDL_bInMainQuit`
- `src/events/SDL_events.c`: file-scope `SDL_event_watchers`, `SDL_EventOK`, event queue linked list, disabled events table
- `src/video/SDL_video.c`: file-scope `static SDL_VideoDevice *_this`
- `src/audio/SDL_audio.c`: file-scope `static SDL_AudioDevice *open_devices[16]`, `static SDL_AudioDriver current_audio`

If SDL2 were statically linked into both SDL2_image.dll and SDL2_mixer.dll, each DLL would get its own copy of these file-scope statics. Effects would include:

- Separate subsystem reference counts → `SDL_Init(SDL_INIT_VIDEO)` from one DLL invisible to the other
- Separate event queues → video events never reach mixer's event handling
- Two independent audio device arrays → mixer's device never findable from image's view
- Independent mutex instances → cross-DLL synchronization silently broken

This is not a theoretical concern. It's a **hard failure mode**. The decision to keep SDL2 dynamic as a shared core is not debatable and should be stated as a principle in the canonical doc, not as a trade-off.

## 9. Build host assessment — much better shape than it feels

I walked through the whole `build/_build/` tree. Summary impression: **this codebase was designed with the right abstractions for the evolution it's about to undergo.**

### 9.1 What survives as-is (~75%)

| Component | Why it survives |
| --- | --- |
| `IRuntimeScanner` + `WindowsDumpbinScanner` / `LinuxLddScanner` / `MacOtoolScanner` | Pure dependency detection, unchanged under hybrid |
| `IBinaryClosureWalker` (`BinaryClosureWalker.cs`) | Core BFS closure walking algorithm is independent of packaging model |
| `IPackageInfoProvider` (`VcpkgCliProvider.cs`) | Canonical vcpkg metadata lookup, still needed for ownership truth |
| `IRuntimeProfile` + `IsSystemFile()` | System library filtering stays identical |
| `IPathService` | All path logic centralized, extends cleanly |
| `IArtifactDeployer` (`ArtifactDeployer.cs`) | Pure executor of a plan, agnostic to what plan contains |
| Task orchestration in `HarvestTask` / `ConsolidateHarvestTask` | Orchestration shape correct; just gets a new step inserted |
| Per-RID status file format + consolidation | CI matrix design already correct; just gets richer content |
| DI wiring in `Program.cs` | All service contracts abstracted; net-new services plug in cleanly |

### 9.2 What evolves (~15%)

| Component | Change |
| --- | --- |
| `IArtifactPlanner` (`ArtifactPlanner.cs`) | Gains a policy validation gate before deciding what to copy. Current core/satellite filtering logic (lines 59-63, see `build/_build/Modules/Harvesting/ArtifactPlanner.cs`) is already policy-shaped — just generalize it |
| `LibraryManifest` schema (`build/manifest.json`) | Add `packaging_strategy`, `allowed_external_deps`, `optional_external_deps`, `lgpl_sensitive` fields. Additive, no breaking change |
| `HarvestStatistics` / `RidHarvestStatus` | Gain `allowed_external_deps_found`, `unexpected_external_deps`, `baked_dependency_count`, `validation_status` fields |
| `HarvestTask` task body | Insert call to new validator between closure walk and planning |

### 9.3 What's net-new (~10%)

| Component | Purpose | Rough effort |
| --- | --- | --- |
| `IDependencyPolicyValidator` + implementation | Compares actual closure against policy, reports allowed vs. unexpected external deps | ~150-200 lines |
| Hybrid triplet files + optional sdl2-mixer overlay port | vcpkg-side linkage policy | Small line count, high validation cost across 7 RIDs |
| `SatelliteBundleBuilder` + CMake wrapper projects | Per-satellite thin CMake that statically absorbs deps into a SHARED bundle DLL | Per satellite: ~30-50 lines CMakeLists + invocation glue in Cake |
| `PackageTask` | Already missing under the old plan — generates .nupkg from harvest output with package metadata, license bundling, buildTransitive props | ~300-500 lines |
| `ISymbolVisibilityValidator` (deferred) | Enforces no-leak discipline via dumpbin/nm/objdump | ~200 lines + per-platform tool glue |

### 9.4 Native asset source is a separate axis from packaging strategy

The execution-model strategy doc's native asset source model (`vcpkg-build`, `overrides`, `harvest-output`, `ci-artifact`) explains the right home for this concern: it's not a top-level strategy dimension, it's a parameter to the build host that says "where are the binaries coming from for this run?" Source-mode work should keep this axis explicit.

### 9.5 Critical gotcha: zero Cake host test coverage

Research note worth raising: the `test/` directory only contains the `Sandboc` sandbox project. The build host has **no automated test coverage**. For a ~3500-line system that's about to grow a policy validation layer, a CMake wrapper invocation layer, and a PackageTask, "ship untested and hope" is not safe.

Recommendation: write unit tests for at least `IDependencyPolicyValidator` and `IArtifactPlanner` before Phase 2.5 closes. This is a smaller lift than it sounds because the contracts are already well-shaped for testing.

## 10. Six corrections the verdict docs need

Consolidated list of the technical inaccuracies that, if left in place, will cause implementation failures. These should land in a consolidated canonical doc before phases are rewritten.

| # | Correction | Source |
| --- | --- | --- |
| 1 | Symbol visibility is NOT one cross-platform discipline. Windows PE is export-opt-in (safe by default); Linux/macOS need explicit `-Wl,--exclude-libs,ALL` / `-exported_symbols_list` for static deps. `C_VISIBILITY_PRESET hidden` alone does not prevent leakage on Linux/macOS. | §6 |
| 2 | vcpkg's default `sdl2-mixer` port hardcodes `SDL2MIXER_DEPS_SHARED=OFF`, killing the dlopen pattern. An overlay port is mandatory for the Extras package plan to work. | §7.2 |
| 3 | The proposed overlay triplet regex `^(sdl2\|...)$` does NOT match `sdl2-image`/`sdl2-mixer`/etc. If the CMake bundle-wrapper path is chosen (recommended), this is correct behavior but must be documented explicitly. | §5.4 |
| 4 | Use CMake 3.24+ `$<LINK_LIBRARY:WHOLE_ARCHIVE,…>` instead of mixing raw `--whole-archive`/`-force_load`/`/WHOLEARCHIVE` flags. | §6.4 |
| 5 | linux-arm64 and osx-arm64 are community triplets, not in vcpkg CI. The project's own CI matrix must validate custom hybrid triplets on all arm64 RIDs on every baseline bump. | §5.5 |
| 6 | SDL_mixer 2.x ships `minimp3` (permissive) as its default MP3 backend. mpg123 is not required for MP3 support. The LGPL calculus needs to be re-run with this in mind; Option A (drop LGPL entirely) may now be viable. | §7.3 |

## 11. Risk matrix by platform

Synthesizing all findings into a per-RID risk assessment:

| RID | Triplet risk | Linkage risk | Symbol visibility risk | Overall |
| --- | --- | --- | --- | --- |
| win-x64 | Low | Low | **Low** (PE opt-in exports) | **LOW** |
| win-x86 | Low | Low | Low | **LOW** |
| win-arm64 | Low | Low | Low | **LOW-MED** (less-tested tooling) |
| linux-x64 | Low | Low (`-fPIC` auto-added) | **MED** (needs `--exclude-libs,ALL`) | **LOW-MED** |
| linux-arm64 | **MED** (community triplet) | Low | MED | **MED** |
| osx-x64 | Low | Low | **MED** (needs exported_symbols_list) | **LOW-MED** |
| osx-arm64 | **MED** (less tested) | Low | MED | **MED** |

Key insight: Windows is the safest target under this strategy (inverted from "Windows is always harder" received wisdom), because its export model is opt-in. The arm64 Linux/macOS RIDs are the ones that need dedicated CI validation and probably a staged rollout (x64 first, arm64 second).

## 12. Execution model — why it's more than workflow advice

The execution-model strategy draft proposes three modes (Source / Package Validation / Release) and a strategy ≠ asset source separation. I agree strongly, and I want to elevate two points:

### 12.1 The three-mode split should land in the manifest schema

It's not enough to describe this in a playbook. The build host needs to understand:

- What **packaging strategy** applies to each library (core / satellite / extras)
- What **asset source** is in use for this run (vcpkg-build / overrides / harvest-output / ci-artifact)

These should be first-class fields in the manifest and CLI, not implicit assumptions. A boolean native-source switch is the degenerate form of this; use an enum-valued `--native-source` option instead.

### 12.2 Package-consumer smoke test is the real integration truth

The current test story is `test/Sandboc/`, which is a sandbox, not a test suite. Under hybrid model, the only way to catch collision/symbol/LGPL failures is to:

1. Build native packages
2. Publish to a local NuGet folder feed
3. Have a dedicated `test/PackageConsumer.*` project that references via `PackageReference`, not `ProjectReference`
4. Run SDL_Init / Mix_OpenAudio / IMG_Load / TTF_OpenFont / SDL_Quit smoke tests
5. Do this per-RID in CI

This is not a nice-to-have. It's the only integration truth the project has. Phase 2.5 must include it.

### 12.3 The "controlled parallelism" framing is correct

The strategy doc proposes letting build-host refactor, local dev upgrade, smoke tests, and binding autogen spike progress in parallel, as long as they all converge on the package-consumer smoke test as the integration spine. I agree — **as long as the integration spine is actually built**, which it isn't yet. Without the spine, parallelism becomes divergence. The spine is Phase 2.5's most important deliverable.

## 13. LGPL decision — what actually needs to happen

Section 7.3 raised the possibility that Option A (drop LGPL codecs) is now viable because minimp3 covers MP3 permissively. To decide between Option A and Option C, three empirical questions need answers:

1. **Does the current vcpkg sdl2-mixer port build with `SDL2MIXER_MP3_MINIMP3=ON` by default?** The default is ON upstream but vcpkg may have disabled it. Needs a quick check of the portfile.
2. **Is libxmp 4.6.0 still LGPL, or has it migrated?** The license-inventory doc says LGPL; worth verifying against the current package's copyright file.
3. **Is `timidity` (permissive MIDI) enabled in our current feature set?** If not, enabling it covers MIDI without fluidsynth.

If answers 1 and 3 are yes, and libxmp can be swapped for libmodplug or dropped, then Option A becomes clean. The whole Mixer.Extras.Native package, the sdl2-mixer overlay port, and a good chunk of the LGPL compliance surface evaporate.

Worst case (can't drop): Option C (Extras package) stays, but with **only fluidsynth and possibly libxmp** in it, not mpg123. Extras becomes much smaller and more focused.

My recommendation for the doc redesign: include a **"LGPL Re-evaluation" task** in Phase 2.5 that runs these three checks and locks the final Option explicitly. Don't silently carry the existing Option C assumption into canonical docs.

## 14. Phase and doc redesign proposal

### 14.1 Canonical docs to update

Confirmed by cross-reading current state:

1. **`docs/plan.md`** — Status, phase roll-up, Q2/Q3 roadmap. Needs Phase 2.5 insertion and issue-table updates.
2. **`docs/phases/phase-2-cicd-packaging.md`** — Scope reduction (close out pure-dynamic harvest work cleanly).
3. **NEW: `docs/phases/phase-2.5-hybrid-packaging-foundation.md`** — New phase covering overlay triplet, overlay port, CMake wrappers, policy validator, symbol validator, package-consumer smoke test spine, PackageTask.
4. **`docs/phases/phase-3-sdl2-complete.md`** — Update scope to assume hybrid model; RID matrix validation expectations change.
5. **`docs/playbook/local-development.md`** — Rewrite around three modes and native asset sources.
6. **NEW: `docs/knowledge-base/hybrid-packaging.md`** — Canonical technical reference for the strategy: triplet shape, overlay port, CMake wrapper pattern, symbol hygiene per platform, validation tooling. Promoted from the best parts of the verdict docs, with the six corrections in §10 baked in.
7. **`docs/research/license-inventory-2026-04-13.md`** — Add Option A viability note, minimp3 finding.
8. **`docs/research/packaging-strategy-hybrid-static-2026-04-13.md`** and **`…-pure-dynamic-…`** — Mark as historical / superseded once the canonical hybrid doc lands.

### 14.2 Phase topology recommendation

My strong preference: **insert Phase 2.5 rather than renumbering**. Renumbering touches every doc cross-reference and every issue in the tracker. Insertion is localized.

```
Phase 1  SDL2 Core Bindings + Harvesting       DONE
Phase 2  CI/CD & Packaging                     IN PROGRESS → CLOSE OUT on existing scope
Phase 2.5 Hybrid Packaging Foundation          NEW — strategy implementation
Phase 3  SDL2 Complete                         PLANNED (scope refined under hybrid)
Phase 4  Binding Auto-Generation               PLANNED (unchanged)
Phase 5  SDL3 Support                          PLANNED (unchanged — inherits hybrid)
```

### 14.3 Phase 2.5 rough contents

Ordered by dependency:

1. **Policy lock-in** — consolidated canonical `hybrid-packaging.md` doc with the six corrections; LGPL re-decision; approval gate.
2. **Triplet feasibility spike** — build one hybrid triplet (start with `x64-windows-hybrid` as lowest-risk), validate SDL2 stays dynamic, satellites static, all deps resolve. Move to linux-x64, osx-x64, then the three arm64s.
3. **Optional: sdl2-mixer overlay port** — only if Option C survives the LGPL re-evaluation.
4. **CMake bundle wrapper template** — one wrapper project, one satellite (start with SDL2_image, it's the smallest dep graph). Validate single-DLL output, symbol hygiene per platform.
5. **Build host policy integration** — manifest schema extension, `IDependencyPolicyValidator`, `HarvestTask` pipeline insertion.
6. **Package-consumer smoke test spine** — `test/PackageConsumer.*` project, local folder feed, CI smoke job matrix.
7. **PackageTask** — generates .nupkg from harvest + license bundle + buildTransitive props.
8. **Symbol visibility validator** (optional, may be deferred to late Phase 3) — enforces no-leak discipline.
9. **Build host unit tests** — for at least the validator and planner.

### 14.4 Issue tracker follow-up

Current tracker has `#75 Define shared native dependency collision policy before packaging changes` as the approval gate. Once the canonical doc lands, that issue closes, and Phase 2.5 gets its own set of issues:

- `Establish hybrid overlay triplet for all 7 RIDs`
- `Maintain sdl2-mixer overlay port` (conditional on LGPL decision)
- `Implement CMake satellite bundle wrapper pattern`
- `Extend manifest schema with packaging policy fields`
- `Implement IDependencyPolicyValidator in build host`
- `Create PackageConsumer smoke test spine with local folder feed`
- `Implement PackageTask`
- `Add symbol visibility validator (optional)`
- `Unit-test build host services`
- `Re-evaluate LGPL strategy (Option A vs C)`

Existing Phase 3 issues mostly stay, but `#80` (NoDependencies variant) and `#82` (linux-musl) can be evaluated under the hybrid model — and linux-musl's answer changes because it depends on what's baked into satellites.

## 15. Final verdict

Adopt **Hybrid Static + Dynamic Core**. Open **Phase 2.5**. Publish a **consolidated canonical technical doc** that includes the six corrections in §10. Re-run the **LGPL decision** with minimp3 in mind. Build the **package-consumer smoke test spine** as the integration truth. Preserve the current build host — it survives the evolution in much better shape than the handwringing suggests.

This is a clear yes on the direction with specific and non-negotiable technical corrections on the implementation details.

## 16. Four decision points that need explicit lock-in

Before phase/doc redesign starts, these four decisions need explicit approval:

1. **Hybrid Static + Dynamic Core adoption with the six corrections in §10** — yes / no
2. **LGPL approach** — Option A (drop, pending three checks in §13) / Option C (Extras, refined) / defer pending LGPL re-evaluation
3. **Execution-model three-mode split lands in manifest schema and build host** — yes / no / defer
4. **Phase topology: insert Phase 2.5** — yes / renumber instead / different shape

## 17. References

### Repository

- `AGENTS.md` — operating rules, approval gates
- `docs/onboarding.md` — project overview and strategic decisions
- `docs/plan.md` — canonical status and roadmap
- `docs/phases/phase-2-cicd-packaging.md` — current active phase
- `docs/research/license-inventory-2026-04-13.md` — the DLL/license truth table
- `docs/research/packaging-strategy-hybrid-static-2026-04-13.md` — original hybrid proposal
- `docs/research/packaging-strategy-pure-dynamic-2026-04-13.md` — pure dynamic alternative
- `docs/research/temp/execution-model-strategy-2026-04-13-shared.md` — three-mode execution model
- `build/_build/Program.cs` — build host entry point
- `build/_build/Modules/Contracts/` — service interfaces
- `build/_build/Modules/Harvesting/BinaryClosureWalker.cs` — core closure walk
- `build/_build/Modules/Harvesting/ArtifactPlanner.cs` — current filter/copy logic
- `build/_build/Tasks/Harvest/HarvestTask.cs` — orchestration
- `build/manifest.json`, `build/runtimes.json`, `build/system_artefacts.json` — current config shape

### Pointer to related temp docs (not read during this pass)

- `packaging-strategy-synthesis-2026-04-13-copilot.md` — described as primary synthesis
- `packaging-strategy-verdict-2026-04-13-claude.md` — first Claude verdict; worth reading alongside this one for convergence/divergence check
- `packaging-strategy-verdict-2026-04-13-chatgpt.md` — independent source-grounded verdict
- `packaging-strategy-verdict-2026-04-13-shared.md` — strategy verdict draft
- `packaging-strategy-verdict-2026-04-13-gemini.md` — argument inventory
- `packaging-strategy-verdict-2026-04-13-grok.md` — alternate argument inventory

### External — vcpkg

- [Triplet variables | Microsoft Learn](https://learn.microsoft.com/en-us/vcpkg/users/triplets) — Per-port customization section, `PORT` variable
- [Triplets concept | Microsoft Learn](https://learn.microsoft.com/en-us/vcpkg/concepts/triplets)
- [`vcpkg_check_linkage` | Microsoft Learn](https://learn.microsoft.com/en-us/vcpkg/maintainers/functions/vcpkg_check_linkage)
- [vcpkg FAQ | Microsoft Learn](https://learn.microsoft.com/en-us/vcpkg/about/faq) — community triplet status
- [microsoft/vcpkg#15067](https://github.com/microsoft/vcpkg/issues/15067) — per-port customization confirmed
- [microsoft/vcpkg#27043](https://github.com/microsoft/vcpkg/issues/27043) — manifest-level linkage override not planned
- [microsoft/vcpkg#24241](https://github.com/microsoft/vcpkg/issues/24241) — SDL2_image link failure on arm64-osx/x64-osx/x64-linux (historical fragility)
- [microsoft/vcpkg#35507](https://github.com/microsoft/vcpkg/issues/35507) — sdl2-mixer-ext arm64-osx failure
- [microsoft/vcpkg#7356](https://github.com/microsoft/vcpkg/issues/7356) — SDL2_mixer load-time dependency
- [Neumann-A/my-vcpkg-triplets](https://github.com/Neumann-A/my-vcpkg-triplets) — long-running overlay triplet repo
- [vcpkg sdl2-mixer portfile](https://github.com/microsoft/vcpkg/blob/master/ports/sdl2-mixer/portfile.cmake) — `SDL2MIXER_DEPS_SHARED=OFF` hardcode
- [vcpkg sdl2 portfile](https://github.com/microsoft/vcpkg/blob/master/ports/sdl2/portfile.cmake)
- [vcpkg linux.cmake toolchain](https://github.com/microsoft/vcpkg/blob/master/scripts/toolchains/linux.cmake) — `-fPIC` unconditional

### External — SDL family

- [libsdl-org/SDL_mixer CMakeLists.txt (SDL2 branch)](https://github.com/libsdl-org/SDL_mixer/blob/SDL2/CMakeLists.txt) — `SDL2MIXER_DEPS_SHARED`, per-codec `_SHARED` options
- [libsdl-org/SDL_mixer main branch](https://github.com/libsdl-org/SDL_mixer) — SDL3 preservation of dlopen
- [libsdl-org/SDL_image](https://github.com/libsdl-org/SDL_image) — `DEFINE_SYMBOL DLL_EXPORT`, `C_VISIBILITY_PRESET hidden`
- [libsdl-org/SDL_ttf](https://github.com/libsdl-org/SDL_ttf) — same pattern
- [libsdl-org/SDL_ttf#159](https://github.com/libsdl-org/SDL_ttf/issues/159) — FreeType intentional symbol hiding
- [libsdl-org/SDL source — SDL.c, events, video, audio](https://github.com/libsdl-org/SDL) — file-scope statics confirming dual-static breakage

### External — CMake / linker / platform

- [`CMAKE_LANG_LINK_LIBRARY_USING_FEATURE` docs](https://cmake.org/cmake/help/latest/variable/CMAKE_LANG_LINK_LIBRARY_USING_FEATURE.html) — `$<LINK_LIBRARY:WHOLE_ARCHIVE,…>`
- [`WINDOWS_EXPORT_ALL_SYMBOLS` property](https://cmake.org/cmake/help/latest/prop_tgt/WINDOWS_EXPORT_ALL_SYMBOLS.html)
- [`/WHOLEARCHIVE` MSVC docs](https://learn.microsoft.com/en-us/cpp/build/reference/wholearchive-include-all-library-object-files?view=msvc-170)
- [Raymond Chen — Why can't I dllexport from a static library](https://devblogs.microsoft.com/oldnewthing/20140321-00/?p=1433)
- [CMake Discourse — Windows DLL with static lib symbols](https://discourse.cmake.org/t/windows-dll-with-all-symbols-of-dependent-static-libs/8185)
- [macOS `ld` man page](https://keith.github.io/xcode-man-pages/ld.1.html) — `-exported_symbols_list`, `-load_hidden`, `-force_load`
- [Android NDK symbol visibility guide](https://developer.android.com/ndk/guides/symbol-visibility)
- [LabJack — simple C++ symbol visibility demo](https://labjack.com/blogs/news/simple-c-symbol-visibility-demo)

### External — ecosystem prior art

- [ppy/SDL3-CS](https://github.com/ppy/SDL3-CS) — closest architectural peer (osu! framework)
- [mono/SkiaSharp](https://github.com/mono/SkiaSharp)
- [libgit2/libgit2sharp.nativebinaries](https://github.com/libgit2/libgit2sharp.nativebinaries)
- [dlemstra/Magick.NET](https://github.com/dlemstra/Magick.NET)
- [mono/SkiaSharp (HarfBuzzSharp)](https://github.com/mono/SkiaSharp)
- [ericsink/SQLitePCL.raw](https://github.com/ericsink/SQLitePCL.raw)
- [amerkoleci/Alimer.Bindings.SDL](https://github.com/amerkoleci/Alimer.Bindings)
- [flibitijibibo/SDL2-CS](https://github.com/flibitijibibo/SDL2-CS)
- [microsoft/onnxruntime](https://github.com/microsoft/onnxruntime)

---

**End of independent verdict.** Ready to discuss any of the four decision points in §16, or to proceed to doc/phase redesign once the lock-in happens.
