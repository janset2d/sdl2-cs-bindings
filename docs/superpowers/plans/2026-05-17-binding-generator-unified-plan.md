# Binding Generator — Unified Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Finish Stage 1 SDL2.Core in production shape (real `BindingModel` with all 6 categories, per-category emitters, typed handles, friendly overloads, dual P/Invoke emit), driven from `build/manifest.json` as the per-family configuration center. Ship the infrastructure once in a shape that absorbs Stage 2 (satellites) and Stage 3 (SDL3) by manifest-entry addition only.

**Architecture:** Manifest-driven per-family generator hosted in the Cake build host under `build/_build/Targets/GenerateBindings/`. One `[TaskName("GenerateBindings")]` task processes one or more families per invocation; per-family config (`binding_generation` block) is loaded from `build/manifest.json` via a typed repository; family-scoped validators opt-in via manifest flags; cross-family type refs use qualified namespace + `<ProjectReference>` + `CoreOwnedTypeMap` enforcement. Phase 2 swaps in the manifest-driven config without changing emit output (still Preview); Phase 3 rename-extends Preview shape to real `BindingModel` + 6 category emitters.

**Tech Stack:** .NET 10 / C# 14, Cake.Frosting 6.1, CppAst 0.24.0 + libclang.runtime.linux-x64 20.1.2 + libClangSharp.runtime.linux-x64 20.1.2 (ADR-004 trio), TUnit + Microsoft.Testing.Platform, Spectre.Console.Cli (tools.cs), Docker (binding-generator.Dockerfile derived from linux-builder).

**Source spec:** [`../specs/2026-05-16-binding-generator-unified-design.md`](../specs/2026-05-16-binding-generator-unified-design.md).

**Approval-gate reminder (AGENTS.md):** No commit, no `manifest.json` edit, no deployment without explicit Deniz approval. Manifest schema bump in Phase 2A requires explicit "go / apply / proceed / başla / yap" before touching the file. Plan uses 🔒 markers for tasks that need approval before execution.

---

## Phase boundaries and commit checkpoints

Deniz decides every commit boundary. The plan suggests checkpoints at phase exits, but does not enforce them. Phases:

| Phase | Scope | Approval needed before starting |
|---|---|---|
| **Phase 1** | Docs unification (write plan, retire old specs/plans, update pointers) | Documentation-only — no approval gate per AGENTS.md exception |
| **Phase 2A** | manifest.json v2.2 schema bump | 🔒 Explicit approval required (manifest.json edit) |
| **Phase 2B** | BindingGenerationConfig + repository | Continues from 2A |
| **Phase 2C** | `IBindingFamilyValidator` refactor | Continues from 2B |
| **Phase 2D** | GenerateBindingsTask refactor + Sdl2CoreGenerationConfig retire | Continues from 2C |
| **Phase 3A** | Rider-driven mass rename (Preview → real) | Rider-driven — Deniz executes mass rename, agent prepares rename map |
| **Phase 3B** | BindingModel extension (5 new categories + BindingTypeRef) | Continues from 3A |
| **Phase 3C** | TypeMappingPolicy + KnownUnsupportedDeclarationPolicy + CoreOwnedTypeMap | Continues from 3B |
| **Phase 3C-prime** | Stabilization/foundation corrections from 2026-05-17/18 review: manifest-derived stable raw ABI class contract (`SDLNative` for SDL2 core), class-level unsafe preserved, compile-check diagnostic kept manual until structural output is complete, fallback parameter uniqueness, SDL2 `SDL_bool` ABI correction, CppAst fixture matrix, external/native type taxonomy, constants/enums/macro policy | Continues from 3C before structural emission |
| **Phase 3D** | CppAst-to-BindingModel translator refactor into named collaborators; generic struct/anonymous-union translation; no `Stage1StructNames` production path | Continues from 3C-prime |
| **Phase 3E** | 6 per-category emitters + BindingEmitter dispatcher + EmitContext | Continues from 3D |
| **Phase 3F** | Friendly overloads + dual P/Invoke emit in CsCommandEmitter | Continues from 3E |
| **Phase 3G** | Output wiring + smoke + peer-oracle visual diff | Continues from 3F |

Suggested commit boundaries: end of Phase 1, end of Phase 2A, end of Phase 2D, end of Phase 3A, end of Phase 3D, end of Phase 3E, end of Phase 3G. Deniz overrides freely.

---

## 2026-05-17 stabilization and API-surface correction

This plan now incorporates two review passes:

1. **Generator stabilization review**: blind compiler, CppAst translator, interop/API, peer-oracle, build/validation, and red-team reviews after the class-level unsafe fix and compile-check project.
2. **API surface research**: Silk.NET, ppy/SDL3-CS, SkiaSharp, SDL2-CS, Alimer-informed synthesis, and independent API-shape risk analysis.

Canonical API-surface decision: [`../../binding-autogen/binding-api-surface-strategy.md`](../../binding-autogen/binding-api-surface-strategy.md).

Corrections that override older snippets in this plan:

- Generated raw ABI command classes use one stable internal family class derived from manifest `primary_class_name`; SDL2 core is `internal static unsafe partial class SDLNative`. Parse-view names (`Neutral`, `MacOS`, etc.) affect file path and `[SupportedOSPlatform]` metadata, not class names. Dropping class-level `unsafe` was a Phase 3A regression; view-specific classes such as `Sdl2_MacOS` are superseded.
- Public raw `IntPtr` externs are not part of v1 preview. Raw ABI externs stay internal; public surface is typed low-level wrappers plus friendly overloads. SDL2-CS compatibility is best-effort, not the shape to freeze.
- Family namespaces and public class names are manifest-driven. SDL2 Core emits under `namespace SDL2` with public class `SDL`; satellites use their `binding_generation.managed_namespace` / `primary_class_name` values.
- String-like SDL macro constants such as `SDL_HINT_*` use canonical `ReadOnlySpan<byte>` UTF-8 literal properties. String ergonomics belongs to method overloads, not duplicate `const string` aliases.
- SDL2 `SDL_bool` maps to `int` at the raw ABI. SDL3 bool-like values keep a separate 1-byte policy.
- `KnownUnsupportedDeclarationPolicy` is manifest/type-policy driven; C variadic fmt-only functions are not blanket-filtered. Explicit `va_list` / `FILE*` / non-portable C-runtime APIs must be deferred or mapped by taxonomy.
- Missing native types are not fixed by empty struct stubs. SDL-owned structs require real layout; external types require explicit emit/map/defer policy.
- `Stage1StructNames` and `SDL_GameControllerButtonBind` name-specific flattening are scaffolding mistakes, not the production direction. SDL-owned structs/unions are discovered by AST shape, and anonymous unions are modeled generically with deterministic generated sibling types.
- `SDL_GUID` is represented as `System.Guid` through a named SDL native type substitution policy. This follows SDL2-CS and Alimer; tests pin class/typedef mapping to `Guid` and exclude `SDL_GUID` from generated `BindingStruct` output.
- Compile-check under `tests\binding-compile-check` is a diagnostic tripwire until generated output is structurally complete. Its non-empty generated-file guard is required before any blocking gate to avoid false green.

Immediate execution order before Phase 3D structural emission:

1. Pin output contract from manifest: SDL2 Core `namespace SDL2`, public `SDL`, internal `SDLNative`, no `Sdl2_<View>` classes.
2. Extract `CppAstToBindingModel` into named translation collaborators for functions, neutral merge, declaration policy, structs, and fields.
3. Remove `Stage1StructNames`; discover SDL-owned structs/unions generically from AST shape.
4. Model anonymous nested unions generically; use `SDL_GameControllerButtonBind` only as a characterization test.
5. Pin `SDL_GUID` as `System.Guid` through docs/tests.
6. Proceed to broader structural model population and category emitters.

---

## Phase 1 — Docs unification

### Task 1.1: Retire predecessor specs to `superseded/`

**Files:**
- Create: `docs/superpowers/specs/superseded/` (directory)
- Move: `docs/superpowers/specs/2026-05-14-binding-generator-architecture-design.md` → `docs/superpowers/specs/superseded/2026-05-14-binding-generator-architecture-design.md`
- Move: `docs/superpowers/specs/2026-05-15-binding-generator-local-output-loop-design.md` → `docs/superpowers/specs/superseded/2026-05-15-binding-generator-local-output-loop-design.md`

- [ ] **Step 1: Create superseded directory + git mv specs**

Run:

```pwsh
mkdir docs/superpowers/specs/superseded
git mv docs/superpowers/specs/2026-05-14-binding-generator-architecture-design.md docs/superpowers/specs/superseded/
git mv docs/superpowers/specs/2026-05-15-binding-generator-local-output-loop-design.md docs/superpowers/specs/superseded/
```

Expected: `git status --short` shows both files renamed (`R`).

- [ ] **Step 2: Add superseded banner to 2026-05-14 spec**

Edit `docs/superpowers/specs/superseded/2026-05-14-binding-generator-architecture-design.md`. Replace the first `>` blockquote (status line) with:

```markdown
> **Status (2026-05-17):** ⚠ **SUPERSEDED.** Architecture decisions absorbed into [`../2026-05-16-binding-generator-unified-design.md`](../2026-05-16-binding-generator-unified-design.md). Kept in `superseded/` for historical reference; do not treat as authoritative. The unified design retires the standalone `Preview*` shape, locks `build/manifest.json` as the per-family config center, and reframes the work as a manifest-driven per-family generator.
>
> Original status banner preserved below for context.
>
> **Original status (2026-05-14, revised 2026-05-15):** Temporary design spec for the Phase 4 binding-generator architecture...
```

Keep the rest of the original content unchanged below the new banner.

- [ ] **Step 3: Add superseded banner to 2026-05-15 spec**

Edit `docs/superpowers/specs/superseded/2026-05-15-binding-generator-local-output-loop-design.md`. Replace the first `>` blockquote (status line) with:

```markdown
> **Status (2026-05-17):** ⚠ **SUPERSEDED.** Local Docker loop infrastructure, multi-pass parsing decisions, parse-time configuration surface, and synthetic-header strategy absorbed into [`../2026-05-16-binding-generator-unified-design.md`](../2026-05-16-binding-generator-unified-design.md) §§13–14. Kept in `superseded/` for historical reference; the `Preview*` scratch-emitter shape it introduced is retired by the unified design. Do not treat as authoritative.
>
> Original status banner preserved below for context.
>
> **Original status:** Implementation complete 2026-05-16 (Tasks 1-11)...
```

- [ ] **Step 4: Verify both banners**

Run:

```pwsh
head -n 10 docs/superpowers/specs/superseded/2026-05-14-binding-generator-architecture-design.md
head -n 10 docs/superpowers/specs/superseded/2026-05-15-binding-generator-local-output-loop-design.md
```

Expected: Each file shows the SUPERSEDED banner at top.

### Task 1.2: Retire predecessor plans to `superseded/`

**Files:**
- Create: `docs/superpowers/plans/superseded/`
- Move: `docs/superpowers/plans/2026-05-14-sdl2-core-binding-generator-stage-1.md` → `docs/superpowers/plans/superseded/2026-05-14-sdl2-core-binding-generator-stage-1.md`
- Move: `docs/superpowers/plans/2026-05-15-binding-generator-local-output-loop.md` → `docs/superpowers/plans/superseded/2026-05-15-binding-generator-local-output-loop.md`

- [ ] **Step 1: Create superseded plans dir + git mv**

Run:

```pwsh
mkdir docs/superpowers/plans/superseded
git mv docs/superpowers/plans/2026-05-14-sdl2-core-binding-generator-stage-1.md docs/superpowers/plans/superseded/
git mv docs/superpowers/plans/2026-05-15-binding-generator-local-output-loop.md docs/superpowers/plans/superseded/
```

- [ ] **Step 2: Add superseded banner to Stage 1 plan**

Edit `docs/superpowers/plans/superseded/2026-05-14-sdl2-core-binding-generator-stage-1.md`. Insert at line 1:

```markdown
> **Status (2026-05-17):** ⚠ **SUPERSEDED.** Stage 1 Task 1-3.5 work + Post-Implementation Review P0/P1 fixes already landed in commits `c00a2cb`, `0db0e31`, `9a5f59e`. Remaining Task 4/5/7/8/9 + PSTH-A/B/C/D/E/F/G + P2/P3 items are absorbed into [`../2026-05-17-binding-generator-unified-plan.md`](../2026-05-17-binding-generator-unified-plan.md) per the unified spec [`../../specs/2026-05-16-binding-generator-unified-design.md`](../../specs/2026-05-16-binding-generator-unified-design.md) §16 absorption map. P2.4 / P2.5 retraction notes (Task 8 forward-looking) and P2.6 cascade-lock (Task-visibility-convention slice) remain in force in this superseded plan but are also captured in the unified spec out-of-scope list. Kept in `superseded/` for historical reference + implementation-seed mining (Task 4 §Step 2 `CoreOwnedTypeMap.cs` + `KnownUnsupportedDeclarationPolicy.cs` factory snippets feed into Phase 3 of the unified plan).

```

- [ ] **Step 3: Add superseded banner to local-output-loop plan**

Edit `docs/superpowers/plans/superseded/2026-05-15-binding-generator-local-output-loop.md`. Insert at line 1:

```markdown
> **Status (2026-05-17):** ⚠ **SUPERSEDED.** Tasks 1-11 + Task 11.5 (AST inline filter + `-U__has_builtin` restore + dynapi cross-check) shipped in commits `0db0e31` + `9a5f59e`. The `Preview*` shape this plan introduced retires per [`../2026-05-17-binding-generator-unified-plan.md`](../2026-05-17-binding-generator-unified-plan.md) Phase 3. Kept in `superseded/` for historical reference.

```

### Task 1.3: Update pointer docs to point at unified spec + plan

**Files:**
- Modify: `docs/plan.md` (Phase 4 references)
- Modify: `docs/README.md`
- Modify: `docs/binding-autogen/README.md`
- Modify: `docs/binding-autogen/binding-autogen-strategy-brief.md` (cross-reference block)

- [ ] **Step 1: Update `docs/plan.md` Phase 4 block**

In `docs/plan.md`, find the `### Phase 4 — Binding Auto-Generation` block. Replace the spec/plan reference lines:

```markdown
Design brief: [phases/phase-4-binding-autogen.md](phases/phase-4-binding-autogen.md).
Strategy brief: [binding-autogen/binding-autogen-strategy-brief.md](binding-autogen/binding-autogen-strategy-brief.md) (revised 2026-05-17).
Architecture design spec: [superpowers/specs/2026-05-14-binding-generator-architecture-design.md](superpowers/specs/2026-05-14-binding-generator-architecture-design.md).
Stage 1 implementation plan: [superpowers/plans/2026-05-14-sdl2-core-binding-generator-stage-1.md](superpowers/plans/2026-05-14-sdl2-core-binding-generator-stage-1.md).
```

with:

```markdown
Design brief: [phases/phase-4-binding-autogen.md](phases/phase-4-binding-autogen.md).
Strategy brief: [binding-autogen/binding-autogen-strategy-brief.md](binding-autogen/binding-autogen-strategy-brief.md) (revised 2026-05-17).
API surface strategy: [binding-autogen/binding-api-surface-strategy.md](binding-autogen/binding-api-surface-strategy.md) (internal raw ABI + public typed low-level + friendly overloads).
Unified design spec: [superpowers/specs/2026-05-16-binding-generator-unified-design.md](superpowers/specs/2026-05-16-binding-generator-unified-design.md) (supersedes 2026-05-14 architecture spec + 2026-05-15 local-output-loop spec).
Unified implementation plan: [superpowers/plans/2026-05-17-binding-generator-unified-plan.md](superpowers/plans/2026-05-17-binding-generator-unified-plan.md) (supersedes Stage 1 plan + local-output-loop plan).
```

Also update the "Status (2026-05-15)" line in the same block to "Status (2026-05-17): Unified spec + plan accepted. Phase 1 — docs unification — complete..." with current status sentence.

- [ ] **Step 2: Update `docs/README.md` references**

Run `grep -n "2026-05-14-binding-generator-architecture-design\|2026-05-15-binding-generator-local-output-loop\|2026-05-14-sdl2-core-binding-generator-stage-1" docs/README.md` to find all references; replace each with the unified spec/plan path.

- [ ] **Step 3: Update `docs/binding-autogen/README.md`**

Same `grep -n` approach; replace references to old spec/plan paths with unified-spec/plan paths.

- [ ] **Step 4: Update `docs/binding-autogen/binding-autogen-strategy-brief.md` cross-reference block**

Find the §"Cross-Reference" or §"References" block; update spec/plan pointers to unified docs.

- [ ] **Step 5: Verify no stale references remain**

Run:

```pwsh
grep -rn "2026-05-14-binding-generator-architecture-design\|2026-05-15-binding-generator-local-output-loop-design\|2026-05-14-sdl2-core-binding-generator-stage-1\|2026-05-15-binding-generator-local-output-loop" docs/
```

Expected: only matches inside `docs/superpowers/specs/superseded/` and `docs/superpowers/plans/superseded/` and the unified spec/plan's own "Superseded" sections.

### Task 1.4: Extend `docs/playbook/binding-generator-maintenance.md` with parse-defines rationale

**Files:**
- Modify: `docs/playbook/binding-generator-maintenance.md`

- [ ] **Step 1: Read the current playbook to find insertion point**

```pwsh
grep -n "^##" docs/playbook/binding-generator-maintenance.md
```

Find the §"Parse-time configuration surface" section (or equivalent — created in `0db0e31` per the strategy brief §"Stage 1 Task 3.5 refinements"). If it exists, extend it. If absent, add it as a new top-level section before any later sections.

- [ ] **Step 2: Add per-family rationale section**

Add this section content (or extend if it exists):

```markdown
## Parse-time configuration surface (per family)

The `binding_generation.parse_defines` + `binding_generation.clang_args` entries in `build/manifest.json` carry the per-family clang-CLI surface. Each entry has a paragraph of rationale that doesn't fit in JSON. This is the durable home for those rationales.

### sdl2-core

#### parse_defines

- **`SDL_DECLSPEC=`** — Suppresses the platform-specific export attributes (`__declspec(dllexport)` on Windows, `__attribute__((visibility("default")))` on Linux) so CppAst sees plain function declarations. Without this, libclang parses the attribute syntax and CppAst's function records pick up annotations that don't translate to our emit shape.

- **`SDL_DISABLE_IMMINTRIN_H=1`**, **`SDL_DISABLE_MMINTRIN_H=1`**, **`SDL_DISABLE_XMMINTRIN_H=1`**, **`SDL_DISABLE_EMMINTRIN_H=1`**, **`SDL_DISABLE_PMMINTRIN_H=1`**, **`SDL_DISABLE_MM3DNOW_H=1`**, **`SDL_DISABLE_LSX_H=1`**, **`SDL_DISABLE_LASX_H=1`**, **`SDL_DISABLE_ARM_NEON_H=1`** — Short-circuit `SDL_cpuinfo.h:118-133`'s intrinsic-header includes (immintrin/mmintrin/xmmintrin/emmintrin/pmmintrin/mm3dnow/lsx/lasx/arm_neon). Without these defines, `SDL_cpuinfo.h` pulls GCC's intrinsic headers (via the Dockerfile's CPATH), whose `extern __inline` declarations of `_mm_pause` / `_mm_getcsr` / `__rdtsc` / `_mm_clflush` / `_mm_{l,m,s}fence` collide with libclang's internal builtin-function table and break the parse with `error: definition of builtin function ...`. The disable macros are the documented SDL2 escape hatch for binding generators — see SDL_cpuinfo.h lines 118-133 in any SDL2 release. Peer evidence: amerkoleci's Alimer.Bindings.SDL uses the SDL3 equivalents (SDL_PLATFORM_ANDROID/IOS/WINRT) for the same purpose.

#### clang_args

- **`-fdeclspec`** — Enables `__declspec` parsing in C mode. `libegl-dev`'s `/usr/include/EGL/egl.h` declares its functions with `EGLAPI` which expands to `__declspec(dllimport/export)` on a Windows target — and SDL_egl.h is pulled by SDL_video.h under SDL_VIDEO_DRIVER_WINDOWS. Without `-fdeclspec`, clang's default C mode rejects every EGL function declaration in Windows-flavoured views. The flag is narrower than `-fms-extensions` (which enables the full MS dialect).

- **`-U__has_builtin`** — Forces `SDL_stdinc.h:127-131` `#ifdef __has_builtin` to false, which makes `_SDL_HAS_BUILTIN(x)` always expand to 0. That short-circuits the `#if _SDL_HAS_BUILTIN(__builtin_{mul,add}_overflow)` blocks at SDL_stdinc.h:822 and 853, so `_SDL_size_mul_overflow_builtin` and `_SDL_size_add_overflow_builtin` SDL_FORCE_INLINE helpers never enter the AST. Defense-in-depth complement to the translator's `CppFunctionFlags.Inline` filter; kills 2 of the 16 known SDL_FORCE_INLINE leaks at parse time before the AST filter ever sees them. Peer reference: ppy/SDL3-CS `SDL_stdinc.rsp` uses the exact same flag.

  Side-effect audit: three SDL2 sites reference `_SDL_HAS_BUILTIN` beyond the two SDL_stdinc.h declaration gates above — `SDL_assert.h:54` (selects `__builtin_debugtrap` for `SDL_TriggerBreakpoint` impl — macro body, no declaration impact), `SDL_endian.h:134/136/138` (selects `__builtin_bswap{16,32,64}` for `SDL_Swap*` SDL_FORCE_INLINE bodies — body content, helpers are already inline-filtered by the AST filter). No public-API declaration is affected by undefining the macro.

### sdl2-image / sdl2-mixer / sdl2-ttf / sdl2-gfx / sdl2-net

Satellite per-family rationales added in Stage 2 when each satellite's `binding_generation.enabled` flips to `true`.
```

- [ ] **Step 4: Verify playbook structure**

```pwsh
grep -n "^##" docs/playbook/binding-generator-maintenance.md
```

Expected: section listing includes "Parse-time configuration surface (per family)".

### Task 1.5: Final Phase 1 verification

- [ ] **Step 1: Run docs link check (if any tool exists)**

```pwsh
grep -rn "binding-generator-architecture-design\|local-output-loop-design\|sdl2-core-binding-generator-stage-1\|binding-generator-local-output-loop" docs/ | grep -v "superseded/" | grep -v "^docs/superpowers/specs/2026-05-16-binding-generator-unified-design.md" | grep -v "^docs/superpowers/plans/2026-05-17-binding-generator-unified-plan.md"
```

Expected: zero output (no stale references outside the superseded/ and unified spec+plan files themselves).

- [ ] **Step 2: Suggest commit boundary**

Phase 1 ends here. Suggested commit message (Deniz approves before commit):

```text
docs(binding-autogen): unified spec + plan; retire 2026-05-14/05-15 specs+plans

Supersedes:
- specs/2026-05-14-binding-generator-architecture-design.md
- specs/2026-05-15-binding-generator-local-output-loop-design.md
- plans/2026-05-14-sdl2-core-binding-generator-stage-1.md
- plans/2026-05-15-binding-generator-local-output-loop.md

Moved to superseded/ with banner notes pointing at the unified docs.
Strategy brief, plan.md, README pointers updated.
Maintenance playbook gains the per-family parse-defines + clang_args
rationale section that doesn't fit in manifest.json.

P0/P1 from PIR already landed in 0db0e31.
PSTH-H/I/J already landed in 9a5f59e.
Unified plan absorbs the remaining P2/P3/PSTH/Task-4-5 work.
```

Do NOT run `git commit` without explicit Deniz approval.

---

## Phase 2 — Manifest schema + per-family infrastructure

### Phase 2A — manifest.json v2.2 schema bump 🔒

**Approval gate (AGENTS.md):** Updating `build/manifest.json` requires explicit Deniz approval. Surface the proposed diff before applying.

### Task 2A.1: Surface schema-bump diff for approval

**Files:**
- Read-only: `build/manifest.json`

- [ ] **Step 1: Show current schema_version**

```pwsh
grep -n "schema_version" build/manifest.json
```

Expected: `"schema_version": "2.1"`.

- [ ] **Step 2: Surface proposed diff to Deniz**

Surface the proposed `binding_generation` block additions (see Task 2A.2 below) to Deniz with the question:

> "Phase 2A bumps `build/manifest.json` schema_version 2.1 → 2.2 and adds `binding_generation` blocks under `library_manifests[]` for SDL2 (full config) and SDL2_image / SDL2_mixer / SDL2_ttf / SDL2_gfx / SDL2_net (placeholders with `enabled: false`). Proposed diff attached. Apply?"

Wait for explicit "yes / go / apply / başla / yap" before proceeding.

### Task 2A.2: Apply schema bump + add `binding_generation` block to SDL2 entry

**Files:**
- Modify: `build/manifest.json`

- [ ] **Step 1: Bump `schema_version` to 2.2**

Replace `"schema_version": "2.1"` with `"schema_version": "2.2"` (single edit at top of file).

- [ ] **Step 2: Add `binding_generation` block to the SDL2 entry**

In `library_manifests[]`, find the entry where `"name": "SDL2"`. After the `"primary_binaries"` array (currently the last property in the entry), add a comma and the new `binding_generation` block:

```jsonc
    {
      "name": "SDL2",
      "vcpkg_name": "sdl2",
      "vcpkg_version": "2.32.10",
      "vcpkg_port_version": 0,
      "native_lib_name": "SDL2.Core.Native",
      "core_lib": true,
      "primary_binaries": [
        { "os": "Windows", "patterns": ["SDL2.dll"] },
        { "os": "Linux", "patterns": ["libSDL2*"] },
        { "os": "OSX", "patterns": ["libSDL2*.dylib"] }
      ],
      "binding_generation": {
        "enabled": true,
        "managed_namespace": "SDL2",
        "primary_class_name": "SDL",
        "platform_catalog": "sdl2-core",
        "owned_prefixes": ["SDL_", "SDLK_", "SDL_HINT_", "SDL_INIT_"],
        "parse_defines": [
          "SDL_DECLSPEC=",
          "SDL_DISABLE_IMMINTRIN_H=1",
          "SDL_DISABLE_MMINTRIN_H=1",
          "SDL_DISABLE_XMMINTRIN_H=1",
          "SDL_DISABLE_EMMINTRIN_H=1",
          "SDL_DISABLE_PMMINTRIN_H=1",
          "SDL_DISABLE_MM3DNOW_H=1",
          "SDL_DISABLE_LSX_H=1",
          "SDL_DISABLE_LASX_H=1",
          "SDL_DISABLE_ARM_NEON_H=1"
        ],
        "clang_args": ["-fdeclspec", "-U__has_builtin"],
        "header_set": {
          "include_dir_glob": "include/SDL2",
          "header_glob": "*.h",
          "excluded_headers": [
            "SDL.h",
            "begin_code.h",
            "close_code.h",
            "SDL_opengl.h",
            "SDL_opengl_glext.h",
            "SDL_opengles.h",
            "SDL_opengles2.h",
            "SDL_opengles2_gl2.h",
            "SDL_opengles2_gl2ext.h",
            "SDL_opengles2_gl2platform.h",
            "SDL_opengles2_khrplatform.h",
            "SDL_egl.h",
            "SDL_image.h",
            "SDL_mixer.h",
            "SDL_net.h",
            "SDL_ttf.h"
          ],
          "excluded_header_prefixes": ["SDL_test", "SDL2_"]
        },
        "excluded_functions": ["SDL_main", "SDL_DYNAPI_entry"],
        "required_functions": [
          { "name": "SDL_Init",          "return_type": "int",  "parameters": [{ "type": "uint", "name": "flags" }], "source_header": "SDL.h" },
          { "name": "SDL_InitSubSystem", "return_type": "int",  "parameters": [{ "type": "uint", "name": "flags" }], "source_header": "SDL.h" },
          { "name": "SDL_QuitSubSystem", "return_type": "void", "parameters": [{ "type": "uint", "name": "flags" }], "source_header": "SDL.h" },
          { "name": "SDL_WasInit",       "return_type": "uint", "parameters": [{ "type": "uint", "name": "flags" }], "source_header": "SDL.h" },
          { "name": "SDL_Quit",          "return_type": "void", "parameters": [],                                    "source_header": "SDL.h" }
        ],
        "deferred_declarations": {
          "SDL_SysWMinfo": {
            "category": "deferred-to-stage-2",
            "reason": "SDL_syswm typed-union layout requires the platform-handle forward-declaration stub library (HWND/HDC/Display*/Window/etc.) and [StructLayout(LayoutKind.Explicit, Size = 64)] emission. Stage 1 emits SDL_GetWindowWMInfo with an opaque SDL_SysWMinfo* parameter; the typed union lands in Stage 2."
          },
          "SDL_SysWMmsg": {
            "category": "deferred-to-stage-2",
            "reason": "Same as SDL_SysWMinfo."
          }
        },
        "validators": {
          "dynapi-coherence": true,
          "neutral-view-non-empty": true,
          "required-functions-emitted": true
        },
        "dynapi": {
          "exports_glob": "buildtrees/sdl2/src/*/src/dynapi/SDL2.exports"
        }
      }
    },
```

### Task 2A.3: Add placeholder `binding_generation` blocks to satellite entries

**Files:**
- Modify: `build/manifest.json` (SDL2_image, SDL2_mixer, SDL2_ttf, SDL2_gfx entries; SDL2_net entry is pending bind per docs/plan.md so manifest doesn't have it yet — confirm by inspection)

- [ ] **Step 1: Add to SDL2_image entry**

In `library_manifests[]`, find `"name": "SDL2_image"`. After the `"primary_binaries"` array, append a comma and:

```jsonc
      "binding_generation": {
        "enabled": false,
        "_stage": "stage-2-placeholder",
        "managed_namespace": "SDL2.Image",
        "primary_class_name": "SDL_image",
        "platform_catalog": "sdl2-image",
        "owned_prefixes": ["IMG_"]
      }
```

The `_stage` field is a comment-marker indicating this block is a placeholder; no code reads it. When Stage 2 activates the satellite, fields fill in and `enabled` flips to `true`.

- [ ] **Step 2: Repeat for SDL2_mixer with `managed_namespace: "SDL2.Mixer"`, `primary_class_name: "SDL_mixer"`, `platform_catalog: "sdl2-mixer"`, `owned_prefixes: ["Mix_"]`.**

- [ ] **Step 3: Repeat for SDL2_ttf with `managed_namespace: "SDL2.Ttf"`, `primary_class_name: "SDL_ttf"`, `platform_catalog: "sdl2-ttf"`, `owned_prefixes: ["TTF_"]`.**

- [ ] **Step 4: Repeat for SDL2_gfx with `managed_namespace: "SDL2.Gfx"`, `primary_class_name: "SDL2_gfx"`, `platform_catalog: "sdl2-gfx"`, `owned_prefixes: ["SDL2_gfx", "rotozoom", "boxColor", "lineColor", "pixelColor", "filledCircle", "circleRGBA", "stringColor", "characterColor", "FPSmanager"]`.**

(SDL2_gfx's prefixes don't follow a clean single-word pattern; the list captures the most common name fragments. Stage 2 will refine.)

- [ ] **Step 5: Validate JSON**

```pwsh
python -c "import json; json.load(open('build/manifest.json'))"
```

Expected: no output (no parse error). If python unavailable, use any JSON validator; `dotnet run --project build/_build -- --target Info` will also surface invalid JSON.

- [ ] **Step 6: Run PreFlight to confirm manifest still validates against existing schema validators**

```pwsh
dotnet run --project build/_build -- --target PreFlightCheck
```

Expected: PASS. Any new schema-validation work for v2.2 enforcement happens in Task 2A.4.

### Task 2A.4: Update manifest-schema validators for v2.2

**Files:**
- Read first: `build/_build/Data/Manifest/Models/*.cs` (find the C# model + parsers for manifest.json)
- Modify: any C# model class that mirrors `library_manifests[]` if it strictly enforces shape (likely lives under `build/_build/Data/Manifest/Models/`)
- Modify: any schema-version assertion in PreFlight if it pins on "2.1"

- [ ] **Step 1: Locate `schema_version` assertion code**

```pwsh
grep -rn "schema_version\|2\\.1" build/_build/Data/Manifest build/_build/Validation/Manifest
```

Identify the parser/validator that pins schema_version. If it accepts any "2.x", bump test fixtures to 2.2. If it pins exactly "2.1", widen to accept "2.2" + add test for both.

- [ ] **Step 2: Update the C# model for `LibraryManifest` if it's strict about unknown fields**

Find the C# record that mirrors `library_manifests[]` entries. If it uses `[JsonExtensionData]` or similar to allow unknown fields, no change needed. If it strictly maps to known properties, add `BindingGenerationConfig? BindingGeneration` as an optional property. **Do NOT define the BindingGenerationConfig record yet** — Phase 2B owns that. For Phase 2A, the model just acknowledges the field exists (or `[JsonExtensionData]` captures it).

If unsure, the safest no-op is to add `[JsonExtensionData] public Dictionary<string, JsonElement>? ExtensionData { get; init; }` to the LibraryManifest record so unknown fields don't crash deserialization.

- [ ] **Step 3: Run build host tests**

```pwsh
dotnet test --project build/_build.Tests/Build.Tests.csproj -c Release --framework net10.0
```

Expected: all green. If any test pins on `schema_version == "2.1"` or fails because of unknown `binding_generation` field, fix.

- [ ] **Step 4: Suggested commit boundary**

Phase 2A ends here. Suggested commit:

```text
feat(binding-autogen): manifest.json v2.2 — binding_generation block per family

- schema_version 2.1 -> 2.2
- SDL2 entry: full binding_generation config (parse_defines, clang_args,
  header_set, excluded_functions, required_functions, deferred_declarations,
  validators, dynapi)
- SDL2_image / SDL2_mixer / SDL2_ttf / SDL2_gfx: stage-2-placeholder
  with enabled=false
- LibraryManifest C# model widened to accept the new field

No behaviour change yet — Phase 2B consumes the new config via repository.
```

Do NOT commit without Deniz approval.

### Phase 2B — `BindingGenerationConfig` + repository

### Task 2B.1: Write failing tests for BindingGenerationConfigRepository

**Files:**
- Create: `build/_build.Tests/Unit/Data/BindingGeneration/BindingGenerationConfigRepositoryTests.cs`

- [ ] **Step 1: Create test class with TUnit conventions**

Match the existing test pattern (see `build/_build.Tests/Unit/Data/Manifest/ManifestRepositoryTests.cs` or equivalent for the conventional shape).

Write the file:

```csharp
using Build.Data.BindingGeneration;
using Build.Data.BindingGeneration.Models;
using Build.Tests.Fixtures;
using TUnit.Core;

namespace Build.Tests.Unit.Data.BindingGeneration;

public sealed class BindingGenerationConfigRepositoryTests
{
    [Test]
    public async Task Load_Should_Return_Sdl2Core_Config_When_Manifest_Has_Full_Block()
    {
        var world = new FakeCakeWorld();
        var manifestPath = FixtureLoader.Load(world, "manifests/full-binding-generation.json", "build/manifest.json");
        var repo = new BindingGenerationConfigRepository(world.Context, manifestPath);

        var result = repo.Load("sdl2-core");

        await Assert.That(result.IsSuccess).IsTrue();
        var config = result.Value;
        await Assert.That(config.FamilyId).IsEqualTo("sdl2-core");
        await Assert.That(config.Enabled).IsTrue();
        await Assert.That(config.ManagedNamespace).IsEqualTo("SDL2");
        await Assert.That(config.PrimaryClassName).IsEqualTo("SDL");
        await Assert.That(config.OwnedPrefixes).Contains("SDL_");
        await Assert.That(config.OwnedPrefixes).Contains("SDLK_");
        await Assert.That(config.OwnedPrefixes).Contains("SDL_HINT_");
        await Assert.That(config.OwnedPrefixes).Contains("SDL_INIT_");
        await Assert.That(config.ParseDefines).Contains("SDL_DECLSPEC=");
        await Assert.That(config.ClangArgs).Contains("-fdeclspec");
        await Assert.That(config.ClangArgs).Contains("-U__has_builtin");
        await Assert.That(config.ExcludedFunctions).Contains("SDL_main");
        await Assert.That(config.ExcludedFunctions).Contains("SDL_DYNAPI_entry");
        await Assert.That(config.RequiredFunctions.Count).IsEqualTo(5);
        await Assert.That(config.DeferredDeclarations.ContainsKey("SDL_SysWMinfo")).IsTrue();
        await Assert.That(config.Validators["dynapi-coherence"]).IsTrue();
        await Assert.That(config.Dynapi).IsNotNull();
    }

    [Test]
    public async Task Load_Should_Return_FamilyNotFound_When_Family_Missing()
    {
        var world = new FakeCakeWorld();
        var manifestPath = FixtureLoader.Load(world, "manifests/full-binding-generation.json", "build/manifest.json");
        var repo = new BindingGenerationConfigRepository(world.Context, manifestPath);

        var result = repo.Load("sdl9-nonexistent");

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).IsTypeOf<BindingGenerationConfigError.FamilyNotFound>();
    }

    [Test]
    public async Task Load_Should_Return_Disabled_When_Family_Has_Enabled_False()
    {
        var world = new FakeCakeWorld();
        var manifestPath = FixtureLoader.Load(world, "manifests/full-binding-generation.json", "build/manifest.json");
        var repo = new BindingGenerationConfigRepository(world.Context, manifestPath);

        var result = repo.Load("sdl2-image");

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).IsTypeOf<BindingGenerationConfigError.Disabled>();
    }

    [Test]
    public async Task Load_Should_Return_InvalidConfig_When_Required_Field_Missing()
    {
        var world = new FakeCakeWorld();
        var manifestPath = FixtureLoader.Load(world, "manifests/missing-namespace.json", "build/manifest.json");
        var repo = new BindingGenerationConfigRepository(world.Context, manifestPath);

        var result = repo.Load("sdl2-core");

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).IsTypeOf<BindingGenerationConfigError.InvalidConfig>();
    }

    [Test]
    public async Task EnumerateEnabledFamilies_Should_Return_Only_Enabled_Families()
    {
        var world = new FakeCakeWorld();
        var manifestPath = FixtureLoader.Load(world, "manifests/full-binding-generation.json", "build/manifest.json");
        var repo = new BindingGenerationConfigRepository(world.Context, manifestPath);

        var enabled = repo.EnumerateEnabledFamilies().ToList();

        await Assert.That(enabled).IsEquivalentTo(new[] { "sdl2-core" });
    }
}
```

- [ ] **Step 2: Create fixture files**

Create `build/_build.Tests/Fixtures/manifests/full-binding-generation.json` — a minimal manifest.json fragment with `library_manifests[]` containing the SDL2 + SDL2_image entries copied from the live manifest.json after Task 2A.3 (full SDL2 block + placeholder satellite blocks).

Create `build/_build.Tests/Fixtures/manifests/missing-namespace.json` — same but the SDL2 entry's `binding_generation.managed_namespace` is removed.

- [ ] **Step 3: Run tests — expect FAIL**

```pwsh
dotnet test --project build/_build.Tests/Build.Tests.csproj -c Release --framework net10.0
```

Expected: 5 NEW test failures (the types `BindingGenerationConfigRepository`, `BindingGenerationConfig`, `BindingGenerationConfigError` don't exist yet).

### Task 2B.2: Define BindingGenerationConfig record types

**Files:**
- Create: `build/_build/Data/BindingGeneration/Models/BindingGenerationConfig.cs`
- Create: `build/_build/Data/BindingGeneration/Models/BindingGenerationConfigError.cs`

- [ ] **Step 1: Write `BindingGenerationConfig.cs`**

```csharp
namespace Build.Data.BindingGeneration.Models;

public sealed record BindingGenerationConfig(
    string FamilyId,
    bool Enabled,
    string ManagedNamespace,
    string PrimaryClassName,
    string PlatformCatalogId,
    IReadOnlyList<string> OwnedPrefixes,
    IReadOnlyList<string> ParseDefines,
    IReadOnlyList<string> ClangArgs,
    HeaderSetConfig HeaderSet,
    IReadOnlySet<string> ExcludedFunctions,
    IReadOnlyList<RequiredFunctionConfig> RequiredFunctions,
    IReadOnlyDictionary<string, DeferredDeclarationConfig> DeferredDeclarations,
    IReadOnlyDictionary<string, bool> Validators,
    DynapiConfig? Dynapi);

public sealed record HeaderSetConfig(
    string IncludeDirGlob,
    string HeaderGlob,
    IReadOnlyList<string> ExcludedHeaders,
    IReadOnlyList<string> ExcludedHeaderPrefixes);

public sealed record RequiredFunctionConfig(
    string Name,
    string ReturnType,
    IReadOnlyList<RequiredFunctionParameter> Parameters,
    string SourceHeader);

public sealed record RequiredFunctionParameter(string Type, string Name);

public sealed record DeferredDeclarationConfig(string Category, string Reason);

public sealed record DynapiConfig(string ExportsGlob);
```

- [ ] **Step 2: Write `BindingGenerationConfigError.cs`**

```csharp
namespace Build.Data.BindingGeneration.Models;

public abstract record BindingGenerationConfigError(string Reason)
{
    public sealed record FamilyNotFound(string FamilyId)
        : BindingGenerationConfigError($"Manifest does not declare a library with binding_generation matching family '{FamilyId}'.");

    public sealed record Disabled(string FamilyId)
        : BindingGenerationConfigError($"Family '{FamilyId}' has binding_generation.enabled=false in manifest.");

    public sealed record InvalidConfig(string FamilyId, string Detail)
        : BindingGenerationConfigError($"Family '{FamilyId}' has invalid binding_generation config: {Detail}");

    public sealed record ManifestNotFound(string Path)
        : BindingGenerationConfigError($"Manifest file not found at '{Path}'.");
}
```

### Task 2B.3: Implement BindingGenerationConfigRepository

**Files:**
- Create: `build/_build/Data/BindingGeneration/BindingGenerationConfigRepository.cs`

- [ ] **Step 1: Write the repository**

```csharp
using System.Text.Json;
using Build.Data.BindingGeneration.Models;
using Build.Host.Cake;
using Build.Results;
using Cake.Core;
using Cake.Core.IO;

namespace Build.Data.BindingGeneration;

public interface IBindingGenerationConfigRepository
{
    Result<BindingGenerationConfig, BindingGenerationConfigError> Load(string familyId);
    IReadOnlyList<string> EnumerateEnabledFamilies();
}

public sealed class BindingGenerationConfigRepository(ICakeContext context, FilePath manifestPath) : IBindingGenerationConfigRepository
{
    private readonly ICakeContext _context = context ?? throw new ArgumentNullException(nameof(context));
    private readonly FilePath _manifestPath = manifestPath ?? throw new ArgumentNullException(nameof(manifestPath));

    public Result<BindingGenerationConfig, BindingGenerationConfigError> Load(string familyId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(familyId);

        if (!_context.FileExists(_manifestPath))
        {
            return new BindingGenerationConfigError.ManifestNotFound(_manifestPath.FullPath);
        }

        var raw = _context.ReadAllText(_manifestPath);
        using var doc = JsonDocument.Parse(raw);

        if (!doc.RootElement.TryGetProperty("library_manifests", out var libs))
        {
            return new BindingGenerationConfigError.InvalidConfig(familyId, "manifest missing library_manifests[]");
        }

        foreach (var lib in libs.EnumerateArray())
        {
            if (!lib.TryGetProperty("name", out var nameEl)) continue;
            var libName = nameEl.GetString();
            if (string.IsNullOrEmpty(libName)) continue;

            if (!lib.TryGetProperty("binding_generation", out var bg)) continue;

            // family-id is derived from package_families[].name; the package_families[]
            // entry whose library_ref == libName is the matching family. For now,
            // a simple convention map is sufficient (SDL2 -> sdl2-core, SDL2_image -> sdl2-image, etc.).
            // Stage 2 wires a proper resolver if multiple package_families[] entries share a library_ref.
            var derivedFamilyId = MapLibraryNameToFamilyId(libName);
            if (!string.Equals(derivedFamilyId, familyId, StringComparison.Ordinal)) continue;

            return Parse(familyId, bg);
        }

        return new BindingGenerationConfigError.FamilyNotFound(familyId);
    }

    public IReadOnlyList<string> EnumerateEnabledFamilies()
    {
        if (!_context.FileExists(_manifestPath))
        {
            return [];
        }

        var raw = _context.ReadAllText(_manifestPath);
        using var doc = JsonDocument.Parse(raw);

        if (!doc.RootElement.TryGetProperty("library_manifests", out var libs))
        {
            return [];
        }

        var enabled = new List<string>();
        foreach (var lib in libs.EnumerateArray())
        {
            if (!lib.TryGetProperty("name", out var nameEl)) continue;
            var libName = nameEl.GetString();
            if (string.IsNullOrEmpty(libName)) continue;

            if (!lib.TryGetProperty("binding_generation", out var bg)) continue;
            if (!bg.TryGetProperty("enabled", out var enEl) || !enEl.GetBoolean()) continue;

            enabled.Add(MapLibraryNameToFamilyId(libName));
        }
        return enabled;
    }

    private static string MapLibraryNameToFamilyId(string libName) => libName switch
    {
        "SDL2"       => "sdl2-core",
        "SDL2_image" => "sdl2-image",
        "SDL2_mixer" => "sdl2-mixer",
        "SDL2_ttf"   => "sdl2-ttf",
        "SDL2_gfx"   => "sdl2-gfx",
        "SDL2_net"   => "sdl2-net",
        _            => libName.ToLowerInvariant(),
    };

    private static Result<BindingGenerationConfig, BindingGenerationConfigError> Parse(string familyId, JsonElement bg)
    {
        try
        {
            var enabled = bg.GetProperty("enabled").GetBoolean();
            if (!enabled)
            {
                return new BindingGenerationConfigError.Disabled(familyId);
            }

            string managedNamespace = GetRequiredString(bg, familyId, "managed_namespace");
            string primaryClassName = GetRequiredString(bg, familyId, "primary_class_name");
            string platformCatalog = GetRequiredString(bg, familyId, "platform_catalog");

            var ownedPrefixes = ReadStringArray(bg, "owned_prefixes");
            var parseDefines = ReadStringArray(bg, "parse_defines");
            var clangArgs = ReadStringArray(bg, "clang_args");

            var headerSetEl = bg.GetProperty("header_set");
            var headerSet = new HeaderSetConfig(
                IncludeDirGlob: GetRequiredString(headerSetEl, familyId, "header_set.include_dir_glob"),
                HeaderGlob: GetRequiredString(headerSetEl, familyId, "header_set.header_glob"),
                ExcludedHeaders: ReadStringArray(headerSetEl, "excluded_headers"),
                ExcludedHeaderPrefixes: ReadStringArray(headerSetEl, "excluded_header_prefixes"));

            var excludedFunctions = ReadStringArray(bg, "excluded_functions").ToHashSet(StringComparer.Ordinal);

            var requiredFunctions = new List<RequiredFunctionConfig>();
            if (bg.TryGetProperty("required_functions", out var rfEl))
            {
                foreach (var rf in rfEl.EnumerateArray())
                {
                    var name = GetRequiredString(rf, familyId, "required_functions[].name");
                    var returnType = GetRequiredString(rf, familyId, "required_functions[].return_type");
                    var sourceHeader = GetRequiredString(rf, familyId, "required_functions[].source_header");
                    var parameters = new List<RequiredFunctionParameter>();
                    if (rf.TryGetProperty("parameters", out var paramsEl))
                    {
                        foreach (var p in paramsEl.EnumerateArray())
                        {
                            parameters.Add(new RequiredFunctionParameter(
                                Type: GetRequiredString(p, familyId, "required_functions[].parameters[].type"),
                                Name: GetRequiredString(p, familyId, "required_functions[].parameters[].name")));
                        }
                    }
                    requiredFunctions.Add(new RequiredFunctionConfig(name, returnType, parameters, sourceHeader));
                }
            }

            var deferred = new Dictionary<string, DeferredDeclarationConfig>(StringComparer.Ordinal);
            if (bg.TryGetProperty("deferred_declarations", out var ddEl))
            {
                foreach (var entry in ddEl.EnumerateObject())
                {
                    deferred[entry.Name] = new DeferredDeclarationConfig(
                        Category: GetRequiredString(entry.Value, familyId, $"deferred_declarations[{entry.Name}].category"),
                        Reason: GetRequiredString(entry.Value, familyId, $"deferred_declarations[{entry.Name}].reason"));
                }
            }

            var validators = new Dictionary<string, bool>(StringComparer.Ordinal);
            if (bg.TryGetProperty("validators", out var vEl))
            {
                foreach (var entry in vEl.EnumerateObject())
                {
                    validators[entry.Name] = entry.Value.GetBoolean();
                }
            }

            DynapiConfig? dynapi = null;
            if (bg.TryGetProperty("dynapi", out var dEl))
            {
                dynapi = new DynapiConfig(
                    ExportsGlob: GetRequiredString(dEl, familyId, "dynapi.exports_glob"));
            }

            return new BindingGenerationConfig(
                FamilyId: familyId,
                Enabled: true,
                ManagedNamespace: managedNamespace,
                PrimaryClassName: primaryClassName,
                PlatformCatalogId: platformCatalog,
                OwnedPrefixes: ownedPrefixes,
                ParseDefines: parseDefines,
                ClangArgs: clangArgs,
                HeaderSet: headerSet,
                ExcludedFunctions: excludedFunctions,
                RequiredFunctions: requiredFunctions,
                DeferredDeclarations: deferred,
                Validators: validators,
                Dynapi: dynapi);
        }
        catch (KeyNotFoundException ex)
        {
            return new BindingGenerationConfigError.InvalidConfig(familyId, ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return new BindingGenerationConfigError.InvalidConfig(familyId, ex.Message);
        }
    }

    private static string GetRequiredString(JsonElement el, string familyId, string fieldName)
    {
        if (!el.TryGetProperty(fieldName.Split('.', '[').Last().TrimEnd(']'), out var v))
        {
            // Field walking is needed when fieldName is nested; for simple fields the property name is the last segment.
            // For nested paths in error messages, we still throw with the full path.
            throw new KeyNotFoundException($"Family '{familyId}': required field '{fieldName}' missing.");
        }
        var s = v.GetString();
        if (string.IsNullOrEmpty(s))
        {
            throw new InvalidOperationException($"Family '{familyId}': field '{fieldName}' is empty.");
        }
        return s;
    }

    private static IReadOnlyList<string> ReadStringArray(JsonElement el, string fieldName)
    {
        if (!el.TryGetProperty(fieldName, out var arrEl)) return [];
        var list = new List<string>();
        foreach (var item in arrEl.EnumerateArray())
        {
            var s = item.GetString();
            if (s is not null) list.Add(s);
        }
        return list;
    }
}
```

> **Note for implementer**: The `GetRequiredString` field-walking heuristic above is a simplification; if it produces wrong errors, switch to explicit `TryGetProperty` per field. Tests will catch the difference.

- [ ] **Step 2: Run tests — expect PASS**

```pwsh
dotnet test --project build/_build.Tests/Build.Tests.csproj -c Release --framework net10.0
```

Expected: 5 tests PASS (including the 5 new tests from Task 2B.1). All other tests remain green.

If any test fails, narrow with the test's name (TUnit / MTP — use plain `dotnet test`, not `--filter`).

### Task 2B.4: DI registration for repository

**Files:**
- Modify: `build/_build/Data/ServiceCollectionExtensions.cs`

- [ ] **Step 1: Register the repository in `AddData()`**

In the existing `AddData()` extension method, add a registration:

```csharp
services.AddSingleton<IBindingGenerationConfigRepository>(sp =>
{
    var context = sp.GetRequiredService<ICakeContext>();
    var paths = sp.GetRequiredService<IPathService>();
    return new BindingGenerationConfigRepository(context, paths.GetVcpkgManifestFile() /* or whatever path accessor points at build/manifest.json — check IPathService for the existing method, likely GetManifestFile() */);
});
```

Use the existing `IPathService` method that already returns `build/manifest.json`'s `FilePath` (Cake-native; see `ManifestRepository` for the pattern).

- [ ] **Step 2: Add composition root smoke test**

In `build/_build.Tests/Unit/CompositionRoot/ServiceCollectionExtensionsSmokeTests.cs`, find the test that asserts `AddData()` resolves; add:

```csharp
[Test]
public async Task AddData_Should_Register_BindingGenerationConfigRepository()
{
    var provider = ServiceCollectionTestHost.AddFakeCakeWorld().AddData().BuildServiceProvider();
    var repo = provider.GetRequiredService<IBindingGenerationConfigRepository>();
    await Assert.That(repo).IsNotNull();
}
```

- [ ] **Step 3: Run tests + slopwatch**

```pwsh
dotnet test --project build/_build.Tests/Build.Tests.csproj -c Release --framework net10.0
slopwatch analyze --fail-on warning --exclude "artifacts/**,external/**,vcpkg_installed/**,tools/**,**/bin/**,**/obj/**"
```

Expected: all green; zero slopwatch warnings.

- [ ] **Step 4: Suggested commit boundary**

```text
feat(binding-autogen): BindingGenerationConfig + repository from manifest.json

- Data/BindingGeneration/Models/BindingGenerationConfig.cs (typed record set)
- Data/BindingGeneration/Models/BindingGenerationConfigError.cs (Result error union)
- Data/BindingGeneration/BindingGenerationConfigRepository.cs (sync Load + EnumerateEnabledFamilies)
- DI registration in Data/ServiceCollectionExtensions.cs
- Tests pin: load sdl2-core, FamilyNotFound, Disabled (sdl2-image), InvalidConfig, EnumerateEnabledFamilies

No consumer wiring yet — Phase 2D wires GenerateBindingsTask onto this.
```

### Phase 2C — `IBindingFamilyValidator` refactor

### Task 2C.1: Write failing tests for IBindingFamilyValidator

**Files:**
- Create: `build/_build.Tests/Unit/Validation/BindingGeneration/DynapiCoherenceValidatorTests.cs`
- Create: `build/_build.Tests/Unit/Validation/BindingGeneration/NeutralViewNonEmptyValidatorTests.cs`
- Create: `build/_build.Tests/Unit/Validation/BindingGeneration/RequiredFunctionsEmittedValidatorTests.cs`

- [ ] **Step 1: Write `DynapiCoherenceValidatorTests.cs`**

Mirror the existing `BindingPublicApiCoherenceValidator` test cases (find via `grep -rn "BindingPublicApiCoherence" build/_build.Tests`). The test should now construct the validator + call `ValidateAsync(model, config, ct)` instead of the old `Validate(emittedSymbols, manifest, profile)` signature.

```csharp
using Build.Data.BindingGeneration.Models;
using Build.Targets.GenerateBindings.Model;
using Build.Validation.BindingGeneration;
using Build.Tests.Fixtures;
using TUnit.Core;

namespace Build.Tests.Unit.Validation.BindingGeneration;

public sealed class DynapiCoherenceValidatorTests
{
    [Test]
    public async Task ValidateAsync_Should_Return_Empty_When_Config_Has_No_Dynapi_Block()
    {
        var fakeRepo = new FakeDynapiManifestRepository(/* no manifest */);
        var validator = new DynapiCoherenceValidator(fakeRepo);
        var config = ConfigFixtures.WithoutDynapi();
        var model = ModelFixtures.WithFunctions(["SDL_Init", "SDL_Quit"]);

        var report = await validator.ValidateAsync(model, config, CancellationToken.None);

        await Assert.That(report.IsValid).IsTrue();
        await Assert.That(report.Checks.Count).IsEqualTo(0);
    }

    [Test]
    public async Task ValidateAsync_Should_Pass_When_Emitted_Matches_Dynapi_Exports()
    {
        var fakeRepo = new FakeDynapiManifestRepository(
            new DynapiManifest(SourcePath: "fake", Origin: "test", PublicSymbols: new HashSet<string>(StringComparer.Ordinal) { "SDL_Init", "SDL_Quit" }));
        var validator = new DynapiCoherenceValidator(fakeRepo);
        var config = ConfigFixtures.Sdl2CoreWithDynapi();
        var model = ModelFixtures.WithFunctions(["SDL_Init", "SDL_Quit"]);

        var report = await validator.ValidateAsync(model, config, CancellationToken.None);

        await Assert.That(report.IsValid).IsTrue();
    }

    [Test]
    public async Task ValidateAsync_Should_Fail_When_Emitted_Symbol_Missing_From_Dynapi()
    {
        var fakeRepo = new FakeDynapiManifestRepository(
            new DynapiManifest(SourcePath: "fake", Origin: "test", PublicSymbols: new HashSet<string>(StringComparer.Ordinal) { "SDL_Init" }));
        var validator = new DynapiCoherenceValidator(fakeRepo);
        var config = ConfigFixtures.Sdl2CoreWithDynapi();
        var model = ModelFixtures.WithFunctions(["SDL_Init", "SDL_LeakySymbol"]);

        var report = await validator.ValidateAsync(model, config, CancellationToken.None);

        await Assert.That(report.IsValid).IsFalse();
        await Assert.That(report.Errors.Count).IsGreaterThan(0);
    }

    [Test]
    public async Task ValidatorId_Should_Be_Stable_String()
    {
        var validator = new DynapiCoherenceValidator(new FakeDynapiManifestRepository(null));
        await Assert.That(validator.ValidatorId).IsEqualTo("dynapi-coherence");
    }
}
```

`FakeDynapiManifestRepository`, `ConfigFixtures`, `ModelFixtures` will need to exist in `build/_build.Tests/Fixtures/` — create as needed during implementation.

- [ ] **Step 2: Write `NeutralViewNonEmptyValidatorTests.cs`**

```csharp
namespace Build.Tests.Unit.Validation.BindingGeneration;

public sealed class NeutralViewNonEmptyValidatorTests
{
    [Test]
    public async Task ValidatorId_Should_Be_Stable_String()
    {
        await Assert.That(new NeutralViewNonEmptyValidator().ValidatorId).IsEqualTo("neutral-view-non-empty");
    }

    [Test]
    public async Task ValidateAsync_Should_Pass_When_Neutral_Has_Functions()
    {
        var validator = new NeutralViewNonEmptyValidator();
        var model = ModelFixtures.WithNeutralFunctions(["SDL_Init"]);

        var report = await validator.ValidateAsync(model, ConfigFixtures.Sdl2CoreWithDynapi(), CancellationToken.None);

        await Assert.That(report.IsValid).IsTrue();
    }

    [Test]
    public async Task ValidateAsync_Should_Fail_When_Neutral_Has_Zero_Functions()
    {
        var validator = new NeutralViewNonEmptyValidator();
        var model = ModelFixtures.WithNeutralFunctions([]);

        var report = await validator.ValidateAsync(model, ConfigFixtures.Sdl2CoreWithDynapi(), CancellationToken.None);

        await Assert.That(report.IsValid).IsFalse();
    }

    [Test]
    public async Task ValidateAsync_Should_Fail_When_Neutral_View_Missing()
    {
        var validator = new NeutralViewNonEmptyValidator();
        var model = ModelFixtures.WithoutNeutralView();

        var report = await validator.ValidateAsync(model, ConfigFixtures.Sdl2CoreWithDynapi(), CancellationToken.None);

        await Assert.That(report.IsValid).IsFalse();
    }
}
```

- [ ] **Step 3: Write `RequiredFunctionsEmittedValidatorTests.cs`**

```csharp
namespace Build.Tests.Unit.Validation.BindingGeneration;

public sealed class RequiredFunctionsEmittedValidatorTests
{
    [Test]
    public async Task ValidatorId_Should_Be_Stable_String()
    {
        await Assert.That(new RequiredFunctionsEmittedValidator().ValidatorId).IsEqualTo("required-functions-emitted");
    }

    [Test]
    public async Task ValidateAsync_Should_Pass_When_All_Required_Functions_Are_In_Neutral_View()
    {
        var validator = new RequiredFunctionsEmittedValidator();
        var config = ConfigFixtures.Sdl2CoreWithDynapi();   // SDL_Init/Quit/etc. required
        var model = ModelFixtures.WithNeutralFunctions(["SDL_Init", "SDL_InitSubSystem", "SDL_QuitSubSystem", "SDL_WasInit", "SDL_Quit"]);

        var report = await validator.ValidateAsync(model, config, CancellationToken.None);

        await Assert.That(report.IsValid).IsTrue();
    }

    [Test]
    public async Task ValidateAsync_Should_Fail_When_Required_Function_Missing()
    {
        var validator = new RequiredFunctionsEmittedValidator();
        var config = ConfigFixtures.Sdl2CoreWithDynapi();
        var model = ModelFixtures.WithNeutralFunctions(["SDL_Init"]);   // missing 4 others

        var report = await validator.ValidateAsync(model, config, CancellationToken.None);

        await Assert.That(report.IsValid).IsFalse();
        await Assert.That(report.Errors.Count).IsEqualTo(4);
    }
}
```

- [ ] **Step 4: Run tests — expect FAIL**

Expected: 11 new test failures (validators + fixtures not yet defined).

### Task 2C.2: Define IBindingFamilyValidator + refactor BindingPublicApiCoherenceValidator → DynapiCoherenceValidator

**Files:**
- Create: `build/_build/Validation/BindingGeneration/IBindingFamilyValidator.cs`
- Modify+rename: `build/_build/Validation/BindingGeneration/BindingPublicApiCoherenceValidator.cs` → `build/_build/Validation/BindingGeneration/DynapiCoherenceValidator.cs`
- Create: `build/_build/Validation/BindingGeneration/NeutralViewNonEmptyValidator.cs`
- Create: `build/_build/Validation/BindingGeneration/RequiredFunctionsEmittedValidator.cs`

- [ ] **Step 1: Write `IBindingFamilyValidator.cs`**

```csharp
using Build.Data.BindingGeneration.Models;
using Build.Results;
using Build.Targets.GenerateBindings.Model;

namespace Build.Validation.BindingGeneration;

public interface IBindingFamilyValidator
{
    string ValidatorId { get; }
    Task<ValidationReport> ValidateAsync(PreviewBindingModel model, BindingGenerationConfig config, CancellationToken ct);
    // Note: PreviewBindingModel becomes BindingModel after Phase 3 rename. Type reference updates automatically via Rider rename.
}
```

- [ ] **Step 2: Rename `BindingPublicApiCoherenceValidator.cs` → `DynapiCoherenceValidator.cs` via `git mv`**

```pwsh
git mv build/_build/Validation/BindingGeneration/BindingPublicApiCoherenceValidator.cs build/_build/Validation/BindingGeneration/DynapiCoherenceValidator.cs
```

- [ ] **Step 3: Rewrite `DynapiCoherenceValidator.cs`**

Replace the existing class with:

```csharp
using Build.Data.BindingGeneration;
using Build.Data.BindingGeneration.Models;
using Build.Results;
using Build.Targets.GenerateBindings.Model;
using Cake.Core;

namespace Build.Validation.BindingGeneration;

public sealed class DynapiCoherenceValidator(IDynapiManifestRepository dynapiRepo) : IBindingFamilyValidator
{
    private readonly IDynapiManifestRepository _dynapiRepo = dynapiRepo ?? throw new ArgumentNullException(nameof(dynapiRepo));
    private const string FalsePositiveCheckName = "Emitted binding references unexported symbol";
    private const string FalseNegativeCheckName = "Public SDL2 export missing from emitted bindings";

    public string ValidatorId => "dynapi-coherence";

    public async Task<ValidationReport> ValidateAsync(PreviewBindingModel model, BindingGenerationConfig config, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(config);

        if (config.Dynapi is null)
        {
            return ValidationReport.Empty;
        }

        var manifestResult = await _dynapiRepo.LoadAsync(ct).ConfigureAwait(false);
        if (manifestResult.TryGetError(out var err))
        {
            throw new InvalidOperationException(err.Reason);
        }

        var manifest = manifestResult.Value;
        var emittedSymbols = model.Views
            .SelectMany(v => v.Functions)
            .Select(f => f.Name)
            .ToHashSet(StringComparer.Ordinal);

        var checks = new List<ValidationCheck>();
        var profile = SeverityProfile.Stage1Generator;

        foreach (var emitted in emittedSymbols.OrderBy(static s => s, StringComparer.Ordinal))
        {
            if (manifest.PublicSymbols.Contains(emitted)) continue;
            checks.Add(new ValidationCheck(
                Name: FalsePositiveCheckName,
                Severity: profile.FalsePositiveSeverity,
                Message: $"Binding emits P/Invoke for '{emitted}' but SDL2's dynapi manifest does not list it as a public export (source: {manifest.Origin} '{manifest.SourcePath}'). Calling it would raise EntryPointNotFoundException; inspect translator inline filter / header exclusion list."));
        }

        foreach (var exported in manifest.PublicSymbols.OrderBy(static s => s, StringComparer.Ordinal))
        {
            if (emittedSymbols.Contains(exported)) continue;
            checks.Add(new ValidationCheck(
                Name: FalseNegativeCheckName,
                Severity: profile.FalseNegativeSeverity,
                Message: $"SDL2 dynapi manifest lists '{exported}' as a public export but the generator did not emit it (source: {manifest.Origin} '{manifest.SourcePath}'). Likely cause: header exclusion list too aggressive, parse-view defines missing a platform, or AST inline filter over-classifying."));
        }

        return new ValidationReport(checks);
    }
}
```

- [ ] **Step 4: Write `NeutralViewNonEmptyValidator.cs`**

```csharp
using Build.Data.BindingGeneration.Models;
using Build.Results;
using Build.Targets.GenerateBindings.Model;

namespace Build.Validation.BindingGeneration;

public sealed class NeutralViewNonEmptyValidator : IBindingFamilyValidator
{
    public string ValidatorId => "neutral-view-non-empty";

    public Task<ValidationReport> ValidateAsync(PreviewBindingModel model, BindingGenerationConfig config, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(model);

        var neutral = model.Views.FirstOrDefault(v => string.Equals(v.Name, "Neutral", StringComparison.Ordinal));
        var checks = new List<ValidationCheck>();

        if (neutral is null)
        {
            checks.Add(new ValidationCheck(
                Name: "Neutral parse view missing",
                Severity: ValidationSeverity.Error,
                Message: $"Family '{config.FamilyId}' has no Neutral parse view in the binding model. Platform catalog misconfigured."));
        }
        else if (neutral.Functions.Count == 0)
        {
            checks.Add(new ValidationCheck(
                Name: "Neutral parse view empty",
                Severity: ValidationSeverity.Error,
                Message: $"Family '{config.FamilyId}' Neutral parse view returned 0 functions. Header set or platform macro hygiene likely misconfigured."));
        }

        return Task.FromResult(new ValidationReport(checks));
    }
}
```

- [ ] **Step 5: Write `RequiredFunctionsEmittedValidator.cs`**

```csharp
using Build.Data.BindingGeneration.Models;
using Build.Results;
using Build.Targets.GenerateBindings.Model;

namespace Build.Validation.BindingGeneration;

public sealed class RequiredFunctionsEmittedValidator : IBindingFamilyValidator
{
    public string ValidatorId => "required-functions-emitted";

    public Task<ValidationReport> ValidateAsync(PreviewBindingModel model, BindingGenerationConfig config, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(config);

        var checks = new List<ValidationCheck>();
        if (config.RequiredFunctions.Count == 0) return Task.FromResult(new ValidationReport(checks));

        var neutral = model.Views.FirstOrDefault(v => string.Equals(v.Name, "Neutral", StringComparison.Ordinal));
        var emittedInNeutral = neutral?.Functions.Select(f => f.Name).ToHashSet(StringComparer.Ordinal) ?? new HashSet<string>();

        foreach (var required in config.RequiredFunctions.OrderBy(rf => rf.Name, StringComparer.Ordinal))
        {
            if (emittedInNeutral.Contains(required.Name)) continue;
            checks.Add(new ValidationCheck(
                Name: "Required function not emitted in Neutral view",
                Severity: ValidationSeverity.Error,
                Message: $"Family '{config.FamilyId}' declares required function '{required.Name}' from {required.SourceHeader} but it was not emitted in the Neutral view. Either the header exclusion list is too aggressive or the function should be removed from required_functions."));
        }

        return Task.FromResult(new ValidationReport(checks));
    }
}
```

- [ ] **Step 6: DI registration**

In `build/_build/Validation/ServiceCollectionExtensions.cs` (or the binding-generation-specific extension method), register each validator:

```csharp
services.AddTransient<IBindingFamilyValidator, DynapiCoherenceValidator>();
services.AddTransient<IBindingFamilyValidator, NeutralViewNonEmptyValidator>();
services.AddTransient<IBindingFamilyValidator, RequiredFunctionsEmittedValidator>();
```

This wires three separate registrations so `IEnumerable<IBindingFamilyValidator>` resolves to all three.

- [ ] **Step 7: Add fixtures**

Create `build/_build.Tests/Fixtures/ConfigFixtures.cs` + `ModelFixtures.cs` + `FakeDynapiManifestRepository.cs` with the static factories the tests reference (`Sdl2CoreWithDynapi`, `WithoutDynapi`, `WithFunctions`, `WithNeutralFunctions`, `WithoutNeutralView`).

- [ ] **Step 8: Run tests — expect PASS**

```pwsh
dotnet test --project build/_build.Tests/Build.Tests.csproj -c Release --framework net10.0
```

Expected: all green; 11 new tests pass.

- [ ] **Step 9: Suggested commit boundary**

```text
feat(binding-autogen): IBindingFamilyValidator + manifest opt-in dispatch

- New: IBindingFamilyValidator interface + ValidatorId
- BindingPublicApiCoherenceValidator -> DynapiCoherenceValidator (rename + refactor signature)
- New: NeutralViewNonEmptyValidator, RequiredFunctionsEmittedValidator
- DI: three IBindingFamilyValidator registrations
- Tests: 11 new tests covering empty/pass/fail paths + ValidatorId stability

GenerateBindingsTask not yet rewired -- Phase 2D switches to manifest dispatch.
```

### Phase 2D — `GenerateBindingsTask` refactor + retire `Sdl2CoreGenerationConfig`

### Task 2D.1: Add `--family` CLI option to BuildContext

**Files:**
- Modify: `build/_build/Host/Cli/Options/` (find the existing options shape; mirror it)
- Modify: `build/_build/Host/BuildContext.cs`

- [ ] **Step 1: Find the existing CLI option pattern**

```pwsh
ls build/_build/Host/Cli/Options/
grep -rn "Option<" build/_build/Host/Cli/Options/ | head -20
```

Pick an existing single-string option (e.g., `--target` is built into Cake, but custom ones like `--rid` may exist) and mirror its shape.

- [ ] **Step 2: Add `FamilyOption.cs`**

Create `build/_build/Host/Cli/Options/FamilyOption.cs`:

```csharp
using System.CommandLine;

namespace Build.Host.Cli.Options;

internal static class FamilyOption
{
    public static readonly Option<string?> Instance = new(
        name: "--family",
        description: "Limit binding-generation to a single family-id (e.g. 'sdl2-core'). Default: every family with binding_generation.enabled=true.");
}
```

- [ ] **Step 3: Register the option in the CLI builder + plumb into BuildContext**

Wherever `BuildContext` properties are derived from CLI options (look for `ParsedArguments` + similar), add `string? Family { get; }` to `BuildContext` and `ParsedArguments`. Read `--family` from the parsed args; pass to `BuildContext` constructor.

- [ ] **Step 4: Add a test for the CLI option roundtrip**

In `build/_build.Tests/Unit/Host/Cli/`, add a test that asserts `--family sdl2-core` populates `BuildContext.Family == "sdl2-core"`.

- [ ] **Step 5: Run tests**

Expected: green.

### Task 2D.2: Refactor GenerateBindingsTask to manifest-driven per-family loop

**Files:**
- Modify: `build/_build/Targets/GenerateBindings/GenerateBindingsTask.cs`
- Modify: `build/_build/Targets/GenerateBindings/HeaderSet/HeaderSetResolver.cs` (accept `BindingGenerationConfig` instead of hardcoded SDL2 logic)
- Modify: `build/_build/Targets/GenerateBindings/Parsing/CppAstParseRunner.cs` (read `_baseDefines` + `clang_args` from config)

> **Note**: Renames in this task are content-only refactors of method signatures; the type rename (PreviewBindingModel → BindingModel) happens in Phase 3A. For now, methods continue to operate on `PreviewBindingModel` and `PreviewParseView`.

- [ ] **Step 1: Refactor `HeaderSetResolver.cs` signature to take BindingGenerationConfig**

Replace the existing `ResolveSdl2CoreHeaders(vcpkgInstalledRoot, syntheticHeadersRoot, triplet)` method with a per-family method:

```csharp
public ResolvedHeaderSet Resolve(BindingGenerationConfig config, DirectoryPath vcpkgInstalledRoot, DirectoryPath syntheticHeadersRoot, string triplet)
{
    ArgumentNullException.ThrowIfNull(config);
    // ... existing logic but read excluded_headers + excluded_header_prefixes + header_glob from config.HeaderSet instead of hardcoded constants ...
}
```

Keep the existing instance method but mark it `[Obsolete("Use Resolve(BindingGenerationConfig, ...).")]` if Stage 2 still needs the SDL2-specific path; otherwise remove it once GenerateBindingsTask is the only caller.

- [ ] **Step 2: Refactor `CppAstParseRunner.cs` to read parse_defines + clang_args from config**

Drop the `_baseDefines` field and `BaseAdditionalArguments` field. Add config parameter to `CreateOptions`:

```csharp
public CppParserOptions CreateOptions(BindingGenerationConfig config, ResolvedHeaderSet headerSet, PlatformParseView parseView)
{
    ArgumentNullException.ThrowIfNull(config);
    ArgumentNullException.ThrowIfNull(headerSet);
    ArgumentNullException.ThrowIfNull(parseView);

    var options = new CppParserOptions
    {
        ParseMacros = true,
        ParserKind = CppParserKind.C,
        TargetSystem = "linux",
        SystemIncludeFolders = { headerSet.SyntheticIncludeRoot.FullPath, headerSet.IncludeRoot.FullPath },
    };

    options.Defines.AddRange(config.ParseDefines);
    options.Defines.AddRange(parseView.Defines);
    options.AdditionalArguments.AddRange(config.ClangArgs);
    foreach (var undefine in parseView.Undefines)
    {
        options.AdditionalArguments.Add($"-U{undefine}");
    }
    return options;
}

public CppAstParseResult Parse(BindingGenerationConfig config, ResolvedHeaderSet headerSet, PlatformParseView parseView)
{
    // same body, but call CreateOptions(config, headerSet, parseView)
}
```

Update `ICppAstParseRunner` interface to match. Update existing call sites + tests.

- [ ] **Step 3: Refactor `GenerateBindingsTask.cs`**

Replace the existing task body with the manifest-driven loop. Full file:

```csharp
using Build.Data.BindingGeneration;
using Build.Data.BindingGeneration.Models;
using Build.Host;
using Build.Host.Cake;
using Build.Results;
using Build.Targets.GenerateBindings.Emitting;
using Build.Targets.GenerateBindings.HeaderSet;
using Build.Targets.GenerateBindings.Model;
using Build.Targets.GenerateBindings.Parsing;
using Build.Validation.BindingGeneration;
using Cake.Common.IO;
using Cake.Core;
using Cake.Core.Diagnostics;
using Cake.Core.IO;
using Cake.Frosting;

namespace Build.Targets.GenerateBindings;

[TaskName("GenerateBindings")]
[TaskDescription("Regenerates SDL2 family bindings driven by manifest.library_manifests[].binding_generation. Default: every family with binding_generation.enabled=true. --family <id>: only the named family.")]
public sealed class GenerateBindingsTask(
    IBindingGenerationConfigRepository configRepo,
    HeaderSetResolver headerResolver,
    ICppAstParseRunner parseRunner,
    ILibclangVersionAsserter libclangVersionAsserter,
    IEnumerable<IBindingFamilyValidator> validators,
    ICakeLog log) : AsyncFrostingTask<BuildContext>
{
    private readonly IBindingGenerationConfigRepository _configRepo = configRepo ?? throw new ArgumentNullException(nameof(configRepo));
    private readonly HeaderSetResolver _headerResolver = headerResolver ?? throw new ArgumentNullException(nameof(headerResolver));
    private readonly ICppAstParseRunner _parseRunner = parseRunner ?? throw new ArgumentNullException(nameof(parseRunner));
    private readonly ILibclangVersionAsserter _libclangVersionAsserter = libclangVersionAsserter ?? throw new ArgumentNullException(nameof(libclangVersionAsserter));
    private readonly IReadOnlyList<IBindingFamilyValidator> _validators = validators?.ToList() ?? throw new ArgumentNullException(nameof(validators));
    private readonly ICakeLog _log = log ?? throw new ArgumentNullException(nameof(log));

    public override async Task RunAsync(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        AssertLinuxTriplet(context.RuntimeIdentifier, context.Runtime.Triplet);
        _libclangVersionAsserter.Assert();
        LogContainerDigestIfPresent();

        var families = ResolveTargetFamilies(context);
        if (families.Count == 0)
        {
            throw new CakeException("No enabled binding-generation families in manifest. Set binding_generation.enabled=true on at least one library_manifests[] entry, or pass --family X.");
        }

        foreach (var familyId in families)
        {
            await GenerateOneFamilyAsync(context, familyId, context.CancellationToken).ConfigureAwait(false);
        }
    }

    private IReadOnlyList<string> ResolveTargetFamilies(BuildContext context)
    {
        if (!string.IsNullOrWhiteSpace(context.Family))
        {
            return [context.Family];
        }
        return _configRepo.EnumerateEnabledFamilies();
    }

    private async Task GenerateOneFamilyAsync(BuildContext context, string familyId, CancellationToken ct)
    {
        var configResult = _configRepo.Load(familyId);
        if (configResult.TryGetError(out var err))
        {
            throw new CakeException($"Family '{familyId}' config load failed: {err.Reason}");
        }
        var config = configResult.Value;

        _log.Information("Generating '{0}' bindings (namespace {1}, primary class {2}).",
            config.FamilyId, config.ManagedNamespace, config.PrimaryClassName);

        var vcpkgInstalledRoot = context.Paths.GetVcpkgInstalledDir;
        var syntheticHeadersRoot = context.Paths.BindingGeneratorSyntheticHeadersRoot;
        var triplet = context.Runtime.Triplet;
        var outputDirectory = context.Paths.GetGenerateBindingsPreviewFamilyRoot(familyId);

        var headerSet = _headerResolver.Resolve(config, vcpkgInstalledRoot, syntheticHeadersRoot, triplet);
        _log.Information("Resolved {0} headers under '{1}'.", headerSet.Headers.Count, headerSet.IncludeRoot.FullPath);

        var catalog = PlatformCatalog.For(config.PlatformCatalogId);

        foreach (var view in catalog.ParseViews)
        {
            _log.Information("Parsing view '{0}' ({1} defines, {2} undefines).", view.Name, view.Defines.Count, view.Undefines.Count);
        }

        var parseResults = catalog.ParseViews
            .AsParallel()
            .AsOrdered()
            .WithCancellation(ct)
            .Select(view => _parseRunner.Parse(config, headerSet, view))
            .ToList();

        var model = CppAstToPreviewModel.Translate(parseResults, config.ExcludedFunctions, ConvertRequiredFunctions(config));
        LogPerViewCounts(model);

        await RunFamilyValidatorsAsync(model, config, ct).ConfigureAwait(false);

        var fileSet = PreviewEmitter.Emit(model);
        await WriteAsync(context, fileSet, outputDirectory, ct).ConfigureAwait(false);

        _log.Information("Wrote {0} files to '{1}'.", fileSet.Files.Count, outputDirectory.FullPath);
    }

    private static IReadOnlyList<PreviewFunction> ConvertRequiredFunctions(BindingGenerationConfig config)
    {
        return [.. config.RequiredFunctions.Select(rf =>
            new PreviewFunction(
                Name: rf.Name,
                ReturnType: rf.ReturnType,
                Parameters: [.. rf.Parameters.Select(p => new PreviewParameter(p.Type, p.Name))],
                SourceHeader: rf.SourceHeader))];
    }

    private async Task RunFamilyValidatorsAsync(PreviewBindingModel model, BindingGenerationConfig config, CancellationToken ct)
    {
        var enabledIds = config.Validators
            .Where(kv => kv.Value)
            .Select(kv => kv.Key)
            .ToHashSet(StringComparer.Ordinal);

        var toRun = _validators.Where(v => enabledIds.Contains(v.ValidatorId)).ToList();

        foreach (var validator in toRun)
        {
            var report = await validator.ValidateAsync(model, config, ct).ConfigureAwait(false);
            LogReport(report, validator.ValidatorId);
            if (!report.IsValid)
            {
                throw new CakeException($"Validator '{validator.ValidatorId}' failed for family '{config.FamilyId}': {report.Errors.Count} error(s). See preceding log lines.");
            }
        }
    }

    private void LogReport(ValidationReport report, string validatorId)
    {
        foreach (var warning in report.Warnings)
        {
            _log.Warning("[{0}] {1}: {2}", validatorId, warning.Name, warning.Message);
        }
        foreach (var error in report.Errors)
        {
            _log.Error("[{0}] {1}: {2}", validatorId, error.Name, error.Message);
        }
    }

    private static void AssertLinuxTriplet(string rid, string triplet)
    {
        if (!triplet.EndsWith("-linux-hybrid", StringComparison.OrdinalIgnoreCase))
        {
            throw new CakeException(
                "GenerateBindings is Linux-canonical; expected the host triplet to end with '-linux-hybrid' " +
                "(per build/manifest.json runtimes[linux-x64/linux-arm64].triplet) inside the linux-builder " +
                $"container; got RID '{rid}' / triplet '{triplet}'. Invoke via 'tools.cs generate-bindings'.");
        }
    }

    private void LogContainerDigestIfPresent()
    {
        var digest = Environment.GetEnvironmentVariable("CONTAINER_DIGEST");
        if (!string.IsNullOrWhiteSpace(digest))
        {
            _log.Information("linux-builder container digest: {0}", digest);
        }
    }

    private void LogPerViewCounts(PreviewBindingModel model)
    {
        foreach (var view in model.Views)
        {
            _log.Information("View {0}: {1} functions.", view.Name, view.Functions.Count);
        }
    }

    private static async Task WriteAsync(ICakeContext context, GeneratedFileSet fileSet, DirectoryPath outputDirectory, CancellationToken ct)
    {
        context.EnsureDirectoryExists(outputDirectory);

        foreach (var file in fileSet.Files)
        {
            ct.ThrowIfCancellationRequested();
            var targetPath = outputDirectory.CombineWithFilePath(file.RelativePath);
            var parent = targetPath.GetDirectory();
            context.EnsureDirectoryExists(parent);
            await context.WriteAllTextAsync(targetPath, file.Content).ConfigureAwait(false);
        }
    }
}
```

- [ ] **Step 4: Update `PlatformCatalog.cs` to add `For(string catalogId)` factory**

Add to `PlatformCatalog`:

```csharp
public static PlatformCatalog For(string catalogId) => catalogId switch
{
    "sdl2-core" => CreateSdl2Catalog(),
    _ => throw new InvalidOperationException($"Unknown platform_catalog id '{catalogId}'. Add a case in PlatformCatalog.For when introducing a new catalog.")
};
```

Stage 2 introduces `"sdl2-image"`, `"sdl2-mixer"`, etc. catalogs as needed (most satellites will share a much smaller catalog — typically Neutral only).

- [ ] **Step 5: Update `IPathService` path accessor**

Rename `GetGenerateBindingsPreviewFamilyRoot(string family)` is fine to keep for now; the path it returns is still `artifacts/generated-bindings-preview/<family>/` per the unified spec §11.

- [ ] **Step 6: Update DI registration in `Targets/GenerateBindings/ServiceCollectionExtensions.cs`**

Remove `Sdl2CoreGenerationConfig`-specific registrations. Ensure:

```csharp
public static IServiceCollection AddGenerateBindings(this IServiceCollection services)
{
    services.AddSingleton<HeaderSetResolver>();
    services.AddSingleton<HeaderSetFingerprintCalculator>();
    services.AddSingleton<ParseDiagnosticFormatter>();
    services.AddSingleton<ICppAstParseRunner, CppAstParseRunner>();
    services.AddSingleton<ILibclangVersionAsserter, LibclangVersionAsserter>();
    return services;
}
```

The validators register in `AddValidators()` (or its binding-generation subset).

- [ ] **Step 7: Delete `Sdl2CoreGenerationConfig.cs` + `Sdl2CoreGenerationConfigTests.cs`**

```pwsh
rm build/_build/Targets/GenerateBindings/Sdl2CoreGenerationConfig.cs
rm build/_build.Tests/Unit/Targets/GenerateBindings/Sdl2CoreGenerationConfigTests.cs
```

(Use `git rm` if tracked; `rm` if untracked.)

- [ ] **Step 8: Run full test suite + container smoke**

```pwsh
dotnet build build/_build/Build.csproj -c Release
dotnet test --project build/_build.Tests/Build.Tests.csproj -c Release --framework net10.0
slopwatch analyze --fail-on warning --exclude "artifacts/**,external/**,vcpkg_installed/**,tools/**,**/bin/**,**/obj/**"

# Container smoke — Phase 2 acceptance gate
dotnet run --file tools.cs -- generate-bindings
```

Expected:
- Build clean
- All tests pass
- Slopwatch zero warnings
- Container smoke produces output at `artifacts/generated-bindings-preview/sdl2-core/Platform/<View>/Commands.g.cs` × 8 views + `parse-views.json`
- Per-view function counts in the container log match pre-pivot counts (verify against `0db0e31` baseline)

- [ ] **Step 9: Byte-identity check (Phase 2 acceptance)**

```pwsh
# Compare with a known-good baseline from 9a5f59e
git stash
dotnet run --file tools.cs -- generate-bindings   # baseline
cp -r artifacts/generated-bindings-preview/sdl2-core /tmp/baseline-sdl2-core
git stash pop
dotnet run --file tools.cs -- generate-bindings   # post-pivot
diff -rq /tmp/baseline-sdl2-core artifacts/generated-bindings-preview/sdl2-core
```

Expected: zero diff (Phase 2 is behaviour-preserving).

If there are diffs that aren't whitespace or comment-only, the refactor introduced a regression — investigate before committing.

- [ ] **Step 10: Suggested commit boundary (Phase 2 exit)**

```text
feat(binding-autogen): manifest-driven per-family GenerateBindingsTask

- GenerateBindingsTask reads BindingGenerationConfig per family from manifest
- --family CLI arg narrows to one; default = every enabled family
- HeaderSetResolver.Resolve(BindingGenerationConfig, ...) replaces SDL2-specific path
- CppAstParseRunner reads parse_defines + clang_args from config
- PlatformCatalog.For(catalogId) replaces hardcoded SDL2-only factory
- Three IBindingFamilyValidator validators dispatched per manifest opt-in
- Sdl2CoreGenerationConfig + .Default + test class retired entirely

No behaviour change for sdl2-core: byte-identical preview output vs baseline.
Phase 3 retires the Preview* shape and lands real BindingModel + per-category emitters.
```

---

## Phase 3 — Preview → Real shape

### Phase 3A — Rider-driven mass rename

> **Execution note (Rider-driven):** Per Deniz's feedback memory `feedback_rider_for_mass_renames`, this phase is Rider-driven mass rename. The agent prepares the rename map; Deniz executes the renames via Rider's "Rename" refactoring; the agent validates afterward via test + slopwatch.

### Task 3A.1: Prepare the rename map for Rider

**Files:**
- Document only (no edits in this task)

- [ ] **Step 1: Surface the rename map to Deniz**

Present this rename map:

```text
Types:
  PreviewBindingModel               -> BindingModel
  PreviewParseView                  -> BindingParseView
  PreviewFunction                   -> BindingFunction
  PreviewParameter                  -> BindingParameter
  PreviewParseViewReport            -> BindingParseViewReport
  PreviewParseViewReportEntry       -> BindingParseViewReportEntry
  PreviewParseViewReportFunction    -> BindingParseViewReportFunction
  CppAstToPreviewModel              -> CppAstToBindingModel
  PreviewEmitter                    -> CsCommandEmitter

Files (auto-rename via Rider should follow types):
  Model/PreviewBindingModel.cs              -> Model/BindingModel.cs
  Model/PreviewParseView.cs                 -> Model/BindingParseView.cs
  Model/PreviewFunction.cs                  -> Model/BindingFunction.cs
  Model/PreviewParameter.cs                 -> Model/BindingParameter.cs
  Model/CppAstToPreviewModel.cs             -> Model/CppAstToBindingModel.cs
  Emitting/PreviewEmitter.cs                -> Emitting/CsCommandEmitter.cs
  Emitting/PreviewParseViewReport.cs        -> Emitting/BindingParseViewReport.cs
  Tests/.../Emitting/PreviewEmitterTests.cs           -> Tests/.../Emitting/CsCommandEmitterTests.cs
  Tests/.../Emitting/PreviewParseViewReportTests.cs   -> Tests/.../Emitting/BindingParseViewReportTests.cs
  Tests/.../Emitting/PreviewBindingModelData.cs       -> Tests/.../Emitting/BindingModelData.cs

Namespaces / class names in emitted output:
  Janset.Sdl2.Preview                       -> SDL2
  Sdl2Preview_<view>                         -> SDLNative
  internal static unsafe partial class       -> internal static unsafe partial class
    (keep class-level `unsafe`; pointer parameters in generated extern signatures require unsafe context)

Comment strings (full match) in emitted output to remove:
  "// Stage 1 binding-autogen preview output. Placeholder shape;"  -> (delete)
  "// replaced by real emitter at Stage 1 Task 5."                 -> (delete)
```

- [ ] **Step 2: Confirm baseline green before Rider rename**

```pwsh
dotnet test --project build/_build.Tests/Build.Tests.csproj -c Release --framework net10.0
```

Expected: green (Phase 2 left it green).

- [ ] **Step 3: Hand off to Deniz for Rider execution**

> "Rename map ready. Open Rider, perform each rename via Rename refactoring (Shift+F6) so usages update consistently. After all renames complete, run `dotnet test`; if green, proceed to Task 3A.2."

### Task 3A.2: Post-rename validation

- [ ] **Step 1: Run tests + slopwatch + container smoke**

```pwsh
dotnet build build/_build/Build.csproj -c Release
dotnet test --project build/_build.Tests/Build.Tests.csproj -c Release --framework net10.0
slopwatch analyze --fail-on warning --exclude "artifacts/**,external/**,vcpkg_installed/**,tools/**,**/bin/**,**/obj/**"
dotnet run --file tools.cs -- generate-bindings
```

Expected: green; container smoke produces output (with new class names + namespace).

- [ ] **Step 2: Compare emit output to pre-rename**

Output files (`Platform/Neutral/Commands.g.cs` etc.) should have:
- `namespace SDL2;` instead of `namespace Janset.Sdl2.Preview;`
- `SDLNative` class instead of `Sdl2Preview_Neutral`, `Sdl2_Neutral`, or any other view-specific raw ABI class
- No "preview placeholder" comments
- `internal static unsafe partial class SDLNative` on every generated raw command class

- [ ] **Step 3: Fix any rename misses (P2.14, P2.15, P2.16, P2.17, P2.18, P2.19)**

After Rider rename, sweep through these stale-comment locations and rewrite:

- `Targets/GenerateBindings/Parsing/CppAstParseRunner.cs` opening comment about `SystemIncludeFolders` (P2.14)
- `Targets/GenerateBindings/GenerateBindingsTask.cs` top-of-class comment about entrypoint script (P2.15)
- `build/_build.Tests/Unit/CompositionRoot/ServiceCollectionExtensionsSmokeTests.cs` line ~100 comment referencing deleted `IBindingGenerationRunner` (P2.16)
- `build/_build/Targets/GenerateBindings/SyntheticHeaders/windows.h` + `Inspectable.h` comments referencing "DeferredDeclarations filter" (P2.17 — now references `KnownUnsupportedDeclarationPolicy`, which lands in Phase 3C)
- Test data `BindingModelData.cs` (renamed) — fix `SDL_Init` / `SDL_Quit` source-header attribution to `SDL.h`, not `SDL_main.h` (P2.18)
- `HeaderSetResolver.cs` opening comment claiming "every SDL.h symbol is in another header" — replace with reference to `required_functions` config (P2.19)

- [ ] **Step 4: Suggested commit boundary**

```text
refactor(binding-autogen): retire Preview* naming; land production type names (PSTH-A)

Rider mass rename:
  PreviewBindingModel               -> BindingModel
  PreviewParseView                  -> BindingParseView
  PreviewFunction                   -> BindingFunction
  PreviewParameter                  -> BindingParameter
  PreviewParseViewReport            -> BindingParseViewReport
  CppAstToPreviewModel              -> CppAstToBindingModel
  PreviewEmitter                    -> CsCommandEmitter
  Janset.Sdl2.Preview ns            -> SDL2
  Sdl2Preview_<view> class          -> SDLNative

Preserve class-level unsafe modifier, strip preview-placeholder comments,
fix stale doc comments (P2.14-P2.19).

Container smoke green; output byte-identical to pre-rename modulo
namespace/class names + comment-strip.
```

### Phase 3B — `BindingModel` extension + `BindingTypeRef`

> **Finding from 2026-05-17 unified-slice smoke (SDL.h exclusion vs constants gap):**
>
> **Decided 2026-05-17: Option A landed in commit `5bd563e` per peer-evidence review (SDL3 binding generators amerkoleci/Alimer.Bindings.SDL + ppy/SDL3-CS both exclude SDL3.h umbrella; pattern transfers Stage 3 unchanged) + SDL2 maintenance-mode drift posture (officially in maintenance since SDL 2.28.0 / June 2023; `SDL_INIT_*` set frozen since SDL 2.0.0 / 2013). Options B (umbrella parse pass) and C (drift-detection validator) preserved below as historical alternatives; if drift becomes a real concern after manifest-only proves leaky, Option C can layer on top of A without rip-out.**
>
> Below is the original three-option analysis as authored on 2026-05-17.
>
> ## The gap
>
> SDL.h is excluded from the per-header parse loop by `HeaderSetResolver` for three reasons (umbrella TU pulls ~50 transitive headers; failure isolation; pragmatic shortcut). The 5 base-API functions SDL.h declares (`SDL_Init`, `SDL_InitSubSystem`, `SDL_QuitSubSystem`, `SDL_WasInit`, `SDL_Quit`) are recovered via `manifest.binding_generation.required_functions` + translator-side Neutral-view injection.
>
> **The same gap exists for SDL.h-only CONSTANTS** that Stage 1's function-only emit doesn't currently surface:
>
> ```c
> // SDL.h declarations not reachable from any other header:
> #define SDL_INIT_TIMER          0x00000001u
> #define SDL_INIT_AUDIO          0x00000010u
> #define SDL_INIT_VIDEO          0x00000020u   // SDL_INIT_VIDEO implies SDL_INIT_EVENTS
> #define SDL_INIT_JOYSTICK       0x00000200u   // implies SDL_INIT_EVENTS
> #define SDL_INIT_HAPTIC         0x00001000u
> #define SDL_INIT_GAMECONTROLLER 0x00002000u   // implies SDL_INIT_JOYSTICK
> #define SDL_INIT_EVENTS         0x00004000u
> #define SDL_INIT_SENSOR         0x00008000u
> #define SDL_INIT_NOPARACHUTE    0x00100000u   // compatibility; flag is ignored
> #define SDL_INIT_EVERYTHING ( \
>                 SDL_INIT_TIMER | SDL_INIT_AUDIO | SDL_INIT_VIDEO | SDL_INIT_EVENTS | \
>                 SDL_INIT_JOYSTICK | SDL_INIT_HAPTIC | SDL_INIT_GAMECONTROLLER | SDL_INIT_SENSOR \
>             )
> ```
>
> SDL.h declares these via `#define`; nothing else does. When `CsConstantEmitter` lands at Phase 3E and starts emitting `public const uint` / `public static readonly uint`, these would be missing from the bound surface — consumers couldn't call `SDL_Init(SDL_INIT_VIDEO)` because `SDL_INIT_VIDEO` wouldn't exist managed-side.
>
> **Why this isn't surfaced as a Stage 1 fix now**: Stage 1 emits functions only; constants are Phase 3E scope. No consumer-visible break in the current bulk slice; the gap is deferred-by-design until `CsConstantEmitter` lands. The Phase 3B model + Phase 3D translator + Phase 3E emitter all need to coordinate on the chosen resolution shape, so the design decision lives here as the source of truth.
>
> ## Option A — `required_constants` manifest field (analog to `required_functions`)
>
> Add a new `BindingGenerationConfig.RequiredConstants` field that hand-curates the SDL.h-only constants. Matches the existing pattern (`required_functions`) for SDL.h-only base-API functions.
>
> **Shape (Phase 3B):**
>
> ```csharp
> public sealed record RequiredConstantConfig
> {
>     [JsonPropertyName("name")]          public required string Name { get; init; }
>     [JsonPropertyName("type")]          public required string Type { get; init; }            // e.g. "uint"
>     [JsonPropertyName("value")]         public string? Value { get; init; }                   // literal form, e.g. "0x00000001u"
>     [JsonPropertyName("value_expr")]    public string? ValueExpr { get; init; }              // computed form, e.g. "SDL_INIT_TIMER | SDL_INIT_AUDIO | ..."
>     [JsonPropertyName("source_header")] public required string SourceHeader { get; init; }   // e.g. "SDL.h"
>     [JsonPropertyName("kind")]          public required ConstantKind Kind { get; init; }     // Literal | Computed
> }
> ```
>
> **Translator (Phase 3D)**: `CppAstToBindingModel.CollectConstants` merges `config.RequiredConstants` into the parsed-macros set (Neutral-view-anchored), same pattern as the function path already does for `config.RequiredFunctions`.
>
> **Emitter (Phase 3E)**: `CsConstantEmitter` distinguishes `ConstantKind.Literal` → `public const <type> NAME = <value>;` from `ConstantKind.Computed` → `public static readonly <type> NAME = <value_expr>;` (compound `SDL_INIT_EVERYTHING` needs `static readonly` because C#'s `const` rejects non-literal expressions).
>
> **manifest.json**: SDL2 `binding_generation.required_constants` seeded with 10 entries (9 literal + 1 computed). Maintenance lives in `docs/playbook/binding-generator-maintenance.md` §"Parse-time configuration surface (per family)" alongside `required_functions` rationale.
>
> **Pros**: symmetrical with existing `required_functions` pattern; deterministic; maintenance burden low (SDL2 base API is stable since SDL2.0.0 — `SDL_INIT_*` set hasn't changed across the 2.x line); compound macro handled cleanly via the `Literal` / `Computed` kind split.
>
> **Cons**: hand-curated → if SDL2 ever adds a new `SDL_INIT_*` macro the manifest needs a manual update; drift can go unnoticed until someone notices the missing constant in generated output.
>
> ## Option B — Separate macro-only SDL.h parse pass
>
> Run SDL.h through an out-of-band `CppParser.ParseFile(SDL.h, options)` call (separate from the main per-header loop), extract only `compilation.Macros` whose `SourceFile` resolves to `SDL.h` itself (not transitively included files), discard everything else (functions / structs / typedefs).
>
> **Translator (Phase 3D)**: dedicated `CollectSdlHOnlyMacros` step that runs once per parse view (or once total — SDL.h's `SDL_INIT_*` macros are platform-unconditional, so a single Neutral-anchored pass is probably enough). Filters `compilation.Macros` to `m.SourceFile.EndsWith("SDL.h")`.
>
> **Pros**: automatic — if SDL2 adds a new `SDL_INIT_*` macro it lands in generated output without any manifest update; no hand-curated list to maintain.
>
> **Cons**:
> - **Umbrella TU cost**: SDL.h transitively pulls ~50 headers; one extra umbrella parse adds noticeable wall time per parse view (potentially 30-60s if naive, depends on caching).
> - **Failure isolation lost**: a parse failure anywhere in the SDL.h umbrella chain poisons the constants pass; current per-header strategy specifically avoids this.
> - **Translator complexity**: source-file filtering logic for "SDL.h-declared macros only" needs to be defensive against libclang resolving SDL.h via an absolute path vs a relative include path, depending on parse options.
> - **Per-view question**: do we parse SDL.h once (Neutral only) or per-view? Currently the SDL_INIT_* set is platform-unconditional in SDL2.32.10, but if a future SDL minor introduces a platform-gated `SDL_INIT_*` (unlikely but possible), per-view parsing would be needed.
>
> ## Option C — Hybrid: Option A + drift-detection validator
>
> Layer Option A's hand-curated `required_constants` (used for actual emit) with a new `IBindingFamilyValidator` implementation — `SdlHConstantCoverageValidator` — that text-greps the SDL.h source file (resolvable via the same path the dynapi validator uses, after this slice's `git clone` fallback puts SDL2 source at a stable location) for `^#define SDL_(INIT|HINT)_` patterns and warns when manifest's `required_constants` is missing any.
>
> **Pros**: deterministic emit (Option A's discipline) + automatic drift detection (catches a new `SDL_INIT_*` introduced by an SDL2 minor bump before generated output ships); validator-id matches the existing manifest opt-in pattern (`sdl-h-constant-coverage` joins `dynapi-coherence` / `neutral-view-non-empty` / `required-functions-emitted`); warning-level under Stage1Generator profile (doesn't fail builds, surfaces in logs).
>
> **Cons**: extra validator implementation (~100 lines of code + tests); grep-based parser is fragile against multi-line macro definitions (the `SDL_INIT_EVERYTHING` compound case spans multiple lines via `\` continuation); validator path resolution coupled to the Dockerfile's conditional clone path or vcpkg buildtree state.
>
> ## Decision points to discuss with Deniz before implementation
>
> 1. **Curation policy**: how often does Deniz want to update manifest entries on SDL2 minor bumps? If "rarely, and the playbook already mandates an upstream-diff step on every bump" → Option A is sufficient. If "drift is a real risk and I don't trust myself to remember" → Option C earns its keep.
> 2. **Umbrella parse appetite**: how much extra wall-time per generation run is acceptable for Option B's automatic coverage? Stage 1's current full-run is ~5 min total; +30-60s would be tolerable but visible.
> 3. **Validator-surface budget**: Option C adds a 4th `IBindingFamilyValidator`. The validator-shape parking lot (`docs/parking-lot/validator-shape-standardization/README.md`) is the relevant context — do we want to spend that budget on drift detection or save it for higher-value Stage 2 checks?
> 4. **Phase 3E coordination**: Whichever option lands, Phase 3E's `CsConstantEmitter` design (Literal vs Computed kind split) is the same. The disagreement is only on Phase 3B's `RequiredConstants` field shape (Option A = present, Option B = absent + parse-driven, Option C = present + validator-checked).

### Task 3B.1: Add new model records (Struct, Enum, Constant, Handle, Callback, TypeRef)

**Files:**
- Modify: `build/_build/Targets/GenerateBindings/Model/BindingModel.cs`
- Create: `build/_build/Targets/GenerateBindings/Model/BindingTypeRef.cs`
- Create: `build/_build/Targets/GenerateBindings/Model/BindingStruct.cs`
- Create: `build/_build/Targets/GenerateBindings/Model/BindingEnum.cs`
- Create: `build/_build/Targets/GenerateBindings/Model/BindingConstant.cs`
- Create: `build/_build/Targets/GenerateBindings/Model/BindingHandle.cs`
- Create: `build/_build/Targets/GenerateBindings/Model/BindingCallback.cs`

- [ ] **Step 1: Extend `BindingModel.cs` with new collections**

Replace the existing record body:

```csharp
namespace Build.Targets.GenerateBindings.Model;

public sealed record BindingModel(
    IReadOnlyList<BindingParseView> Views,
    IReadOnlyList<BindingStruct> Structs,
    IReadOnlyList<BindingEnum> Enums,
    IReadOnlyList<BindingConstant> Constants,
    IReadOnlyList<BindingHandle> Handles,
    IReadOnlyList<BindingCallback> Callbacks);
```

- [ ] **Step 2: Write `BindingTypeRef.cs`**

```csharp
namespace Build.Targets.GenerateBindings.Model;

public sealed record BindingTypeRef(
    string ManagedName,
    string? OwningFamilyId,
    bool IsPointer,
    bool IsOpaqueHandle);
```

- [ ] **Step 3: Write `BindingStruct.cs` + `BindingStructField.cs`**

```csharp
using System.Runtime.InteropServices;

namespace Build.Targets.GenerateBindings.Model;

public sealed record BindingStruct(
    string Name,
    IReadOnlyList<BindingStructField> Fields,
    LayoutKind Layout,
    int? ExplicitSize);

public sealed record BindingStructField(string Name, BindingTypeRef Type, int? FieldOffset);
```

- [ ] **Step 4: Write `BindingEnum.cs`**

```csharp
namespace Build.Targets.GenerateBindings.Model;

public sealed record BindingEnum(
    string Name,
    BindingTypeRef UnderlyingType,
    IReadOnlyList<BindingEnumMember> Members,
    bool IsFlags);

public sealed record BindingEnumMember(string Name, string Value);
```

- [ ] **Step 5: Write `BindingConstant.cs`**

```csharp
namespace Build.Targets.GenerateBindings.Model;

public enum ConstantKind { Literal, Computed }

public sealed record BindingConstant(string Name, BindingTypeRef Type, string Value, ConstantKind Kind);
```

- [ ] **Step 6: Write `BindingHandle.cs`**

```csharp
namespace Build.Targets.GenerateBindings.Model;

public sealed record BindingHandle(string Name);
```

- [ ] **Step 7: Write `BindingCallback.cs`**

```csharp
namespace Build.Targets.GenerateBindings.Model;

public sealed record BindingCallback(string Name, BindingTypeRef ReturnType, IReadOnlyList<BindingParameter> Parameters);
```

- [ ] **Step 8: Replace BindingFunction's ReturnType + Parameter.Type from string → BindingTypeRef**

In `BindingFunction.cs` and `BindingParameter.cs`, change the type field from `string` to `BindingTypeRef`:

```csharp
namespace Build.Targets.GenerateBindings.Model;

public sealed record BindingFunction(
    string Name,
    BindingTypeRef ReturnType,
    IReadOnlyList<BindingParameter> Parameters,
    string SourceHeader,
    bool IsVariadic);

public sealed record BindingParameter(BindingTypeRef Type, string Name);
```

This is the major model shape change. It will break the current `CppAstToBindingModel` (which emits strings) and `CsCommandEmitter` (which reads strings). Phase 3D + 3E refactor them.

- [ ] **Step 9: Update temporary call sites (translator + emitter) to compile against new shape**

`CppAstToBindingModel` currently emits `string` types from `MapType`. Wrap each call site in a temporary adapter:

```csharp
private static BindingTypeRef ToTypeRef(string managedName) => new(managedName, OwningFamilyId: null, IsPointer: managedName.EndsWith("*"), IsOpaqueHandle: false);
```

Use `ToTypeRef(...)` everywhere a string previously flowed through. This is a temporary bridge; Phase 3D rewrites `CppAstToBindingModel` properly.

Similarly, in `CsCommandEmitter`, read `.ManagedName` off the new `BindingTypeRef` records:

```csharp
.Append(function.ReturnType.ManagedName)   // was: function.ReturnType
.Append(' ')
.Append(function.Name)
.Append('(')
.Append(string.Join(", ", function.Parameters.Select(p => $"{p.Type.ManagedName} {p.Name}")))   // was: p.Type
.AppendLf(");");
```

Also pass empty lists for the new model fields when constructing `BindingModel`:

```csharp
return new BindingModel(
    Views: views,
    Structs: [],
    Enums: [],
    Constants: [],
    Handles: [],
    Callbacks: []);
```

- [ ] **Step 10: Update existing tests for the new shape**

`BindingModelData` fixture + emitter tests will need to construct `BindingTypeRef`s where they used strings before. Wrap each fixture function:

```csharp
new BindingFunction(
    Name: "SDL_Init",
    ReturnType: new BindingTypeRef("int", null, false, false),
    Parameters: [new BindingParameter(new BindingTypeRef("uint", null, false, false), "flags")],
    SourceHeader: "SDL.h",
    IsVariadic: false)
```

- [ ] **Step 11: Run tests + smoke**

Expected: green; container smoke still byte-identical (Phase 3B is shape-only, no behaviour change).

- [ ] **Step 12: Suggested commit boundary**

```text
feat(binding-autogen): extend BindingModel with 5 declaration categories

- BindingTypeRef record (ManagedName, OwningFamilyId, IsPointer, IsOpaqueHandle)
- BindingStruct + BindingStructField (with LayoutKind + ExplicitSize for union)
- BindingEnum + BindingEnumMember (with IsFlags)
- BindingConstant + ConstantKind enum (Literal vs Computed)
- BindingHandle (name only — emitted as readonly partial struct(nint))
- BindingCallback (return type + parameters; emitted as delegate*)
- BindingFunction.ReturnType + BindingParameter.Type changed from string -> BindingTypeRef

Temporary string -> BindingTypeRef bridge in CppAstToBindingModel + CsCommandEmitter
(rewritten in Phase 3D / 3E). All new collections empty for now.
Container smoke byte-identical (modulo no functional change).
```

### Phase 3C — Policy extractions

> **Design intent (read before implementing 3C.1–3C.3):** Phase 3C extracts the type-mapping logic that currently lives inline as private static methods on `CppAstToBindingModel`. The split follows the two-axis design recorded in the unified spec §8.1:
>
> - **`CoreOwnedTypeMap` answers identity only** — "Is identifier Y owned by family X?" Implementation stays prefix-based against `BindingGenerationConfig.OwnedPrefixes` (manifest-declared, family-scoped).
> - **`TypeMappingPolicy` answers value-type mapping only** — primitive / explicit-width typedef / chain-resolved typedef / enum-as-int. **Does NOT classify pointers as handles vs structs; that's the translator's job in Phase 3D.**
> - **`KnownUnsupportedDeclarationPolicy` answers deferred / explicitly unsupported declarations** — the manifest's `deferred_declarations` block plus the external/native type taxonomy. C variadic fmt-only functions are not blanket-filtered.
>
> The current `MapPointer`'s `SDL_*`-prefix → `IntPtr` fallback IS the fragility we're cleaning up. Phase 3D translator populates `BindingTypeRef.IsOpaqueHandle` by structural inspection of `CppTypedef.ElementType` (empty `CppClass` = handle, primitive-resolving = value, etc.), not by re-encoding the prefix fallback inside the new policies. Emitters consume `BindingTypeRef` fields directly; the prefix check stays in `CoreOwnedTypeMap.IsOwned` for cross-family identity only.
>
> Peer evidence for the split (spec §8.2): Alimer.Bindings.SDL (CppAst SDL3), ppy/SDL3-CS (ClangSharp SDL3), SkiaSharp, and Silk.NET all separate identity from category. None use prefix-only for category — even single-family generators carry an explicit `_handleTypes` set built at translation time. Phase 3D follows the same pattern; Phase 3C just prepares the policy seams.

### Task 3C.1: Extract `TypeMappingPolicy`

**Files:**
- Create: `build/_build/Targets/GenerateBindings/Model/TypeMappingPolicy.cs`
- Create: `build/_build.Tests/Unit/Targets/GenerateBindings/Model/TypeMappingPolicyTests.cs`

- [ ] **Step 1: Write failing tests**

```csharp
using Build.Data.BindingGeneration.Models;
using Build.Targets.GenerateBindings.Model;
using CppAst;
using TUnit.Core;

namespace Build.Tests.Unit.Targets.GenerateBindings.Model;

public sealed class TypeMappingPolicyTests
{
    [Test]
    public async Task MapPrimitive_Should_Map_Long_To_Nint_On_Lp64()
    {
        var policy = new TypeMappingPolicy(ConfigFixtures.Sdl2CoreWithDynapi(), new CoreOwnedTypeMap(ConfigFixtures.Sdl2CoreWithDynapi()));
        var prim = MakeCppPrimitive(CppPrimitiveKind.Long);
        var result = policy.MapPrimitive(prim);
        await Assert.That(result.ManagedName).IsEqualTo("nint");
    }

    [Test]
    public async Task MapTypedef_Should_Resolve_Sdl_AudioFormat_Through_Underlying_Uint16()
    {
        var policy = new TypeMappingPolicy(ConfigFixtures.Sdl2CoreWithDynapi(), new CoreOwnedTypeMap(ConfigFixtures.Sdl2CoreWithDynapi()));
        var td = MakeSdlAudioFormatTypedef();   // typedef Uint16 SDL_AudioFormat
        var result = policy.MapTypedef(td, ConfigFixtures.Sdl2CoreWithDynapi());
        await Assert.That(result.ManagedName).IsEqualTo("ushort");
    }

    [Test]
    public async Task MapTypedef_Should_Fail_With_DepthGuard_When_Circular_Typedef()
    {
        var policy = new TypeMappingPolicy(ConfigFixtures.Sdl2CoreWithDynapi(), new CoreOwnedTypeMap(ConfigFixtures.Sdl2CoreWithDynapi()));
        var circular = MakeCircularTypedef();
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
        {
            policy.MapTypedef(circular, ConfigFixtures.Sdl2CoreWithDynapi());
            return Task.CompletedTask;
        });
    }

    [Test]
    public async Task SafeIdentifier_Should_Escape_All_Reserved_Keywords()
    {
        var policy = new TypeMappingPolicy(ConfigFixtures.Sdl2CoreWithDynapi(), new CoreOwnedTypeMap(ConfigFixtures.Sdl2CoreWithDynapi()));
        // Sample from Roslyn SyntaxFacts contextual + reserved keywords
        foreach (var kw in new[] { "ref", "out", "in", "params", "object", "string", "event", "where", "volatile", "using", "unsafe", "fixed", "lock", "is", "as", "new" })
        {
            await Assert.That(policy.SafeIdentifier(kw)).IsEqualTo("@" + kw);
        }
    }

    [Test]
    public async Task SafeIdentifier_Should_Return_Indexed_Fallback_When_Parameter_Name_Is_Empty()
    {
        var policy = new TypeMappingPolicy(ConfigFixtures.Sdl2CoreWithDynapi(), new CoreOwnedTypeMap(ConfigFixtures.Sdl2CoreWithDynapi()));
        await Assert.That(policy.SafeIdentifier("", 0)).IsEqualTo("@_p0");
        await Assert.That(policy.SafeIdentifier("", 1)).IsEqualTo("@_p1");
    }

    [Test]
    public async Task MapTypedef_Should_Map_Sdl2_Bool_To_Int()
    {
        var policy = new TypeMappingPolicy(ConfigFixtures.Sdl2CoreWithDynapi(), new CoreOwnedTypeMap(ConfigFixtures.Sdl2CoreWithDynapi()));
        var td = MakeSdlBoolTypedef(); // typedef enum SDL_bool
        var result = policy.MapTypedef(td, ConfigFixtures.Sdl2CoreWithDynapi());
        await Assert.That(result.ManagedName).IsEqualTo("int");
    }

    [Test]
    public async Task Map_Should_Raise_Warning_For_Unknown_CppType()
    {
        // Map's catch-all branch should not silently emit IntPtr; should produce a typed warning result
        // (Implementation choice: return a BindingTypeRef with ManagedName="IntPtr" AND set IsOpaqueHandle=false,
        //  but emit a log warning. For now, assert ManagedName falls back but record the warning side-effect.)
        var policy = new TypeMappingPolicy(ConfigFixtures.Sdl2CoreWithDynapi(), new CoreOwnedTypeMap(ConfigFixtures.Sdl2CoreWithDynapi()));
        var unknown = MakeUnknownCppType();
        var result = policy.Map(unknown, ConfigFixtures.Sdl2CoreWithDynapi());
        await Assert.That(result.ManagedName).IsEqualTo("IntPtr");
        // TODO when warning-collection contract solidifies: assert policy.Warnings contains entry
    }

    // CppAst helper builders — implementer fills in
    private static CppPrimitiveType MakeCppPrimitive(CppPrimitiveKind kind) => /* ... */ null!;
    private static CppTypedef MakeSdlAudioFormatTypedef() => /* ... */ null!;
    private static CppTypedef MakeSdlBoolTypedef() => /* ... */ null!;
    private static CppTypedef MakeCircularTypedef() => /* ... */ null!;
    private static CppType MakeUnknownCppType() => /* ... */ null!;
}
```

- [ ] **Step 2: Implement `TypeMappingPolicy.cs`**

Port the type-mapping logic from `CppAstToBindingModel.cs` (currently inline private static methods at lines 168–298 of pre-Phase-3B file) into a sealed class. Apply P0.1 fix (chain-resolve through nested typedef before `SDL_`-prefix fallback), P0.2 fix (Long → nint), P0.3 fix (catch-all returns warning + IntPtr, not silent IntPtr), P2.8 (max depth guard), P2.9 (Roslyn SyntaxFacts keyword set).

```csharp
using System.Collections.Frozen;
using Build.Data.BindingGeneration.Models;
using CppAst;
using Microsoft.CodeAnalysis.CSharp;

namespace Build.Targets.GenerateBindings.Model;

public sealed class TypeMappingPolicy(BindingGenerationConfig config, CoreOwnedTypeMap ownedTypes)
{
    private const int MaxTypedefDepth = 16;
    private readonly BindingGenerationConfig _config = config ?? throw new ArgumentNullException(nameof(config));
    private readonly CoreOwnedTypeMap _ownedTypes = ownedTypes ?? throw new ArgumentNullException(nameof(ownedTypes));

    private static readonly FrozenDictionary<string, string> ExplicitTypedefMap = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["Sint8"] = "sbyte", ["Uint8"] = "byte",
        ["Sint16"] = "short", ["Uint16"] = "ushort",
        ["Sint32"] = "int", ["Uint32"] = "uint",
        ["Sint64"] = "long", ["Uint64"] = "ulong",
        ["SDL_bool"] = "int",   // SDL2: enum-backed, int-width. SDL3 gets a separate 1-byte bool policy.
        ["size_t"] = "nuint", ["ptrdiff_t"] = "nint",
    }.ToFrozenDictionary(StringComparer.Ordinal);

    public BindingTypeRef Map(CppType type, BindingGenerationConfig familyConfig)
    {
        ArgumentNullException.ThrowIfNull(type);
        while (type is CppQualifiedType qt) type = qt.ElementType;

        return type switch
        {
            CppPrimitiveType prim => MapPrimitive(prim),
            CppPointerType ptr   => MapPointer(ptr, familyConfig),
            CppTypedef td        => MapTypedef(td, familyConfig),
            CppArrayType arr     => MapPointer(new CppPointerType(arr.ElementType), familyConfig),
            CppEnum              => new BindingTypeRef("int", null, false, false),
            CppClass cls         => MapClass(cls, familyConfig),
            _                    => UnknownFallback(type),
        };
    }

    public BindingTypeRef MapPrimitive(CppPrimitiveType prim)
    {
        ArgumentNullException.ThrowIfNull(prim);
        return prim.Kind switch
        {
            CppPrimitiveKind.Void => new("void", null, false, false),
            CppPrimitiveKind.Bool => new("byte", null, false, false),
            CppPrimitiveKind.Char => new("sbyte", null, false, false),
            CppPrimitiveKind.WChar => new("char", null, false, false),
            CppPrimitiveKind.Short => new("short", null, false, false),
            CppPrimitiveKind.Int => new("int", null, false, false),
            CppPrimitiveKind.LongLong => new("long", null, false, false),
            CppPrimitiveKind.UnsignedChar => new("byte", null, false, false),
            CppPrimitiveKind.UnsignedShort => new("ushort", null, false, false),
            CppPrimitiveKind.UnsignedInt => new("uint", null, false, false),
            CppPrimitiveKind.UnsignedLongLong => new("ulong", null, false, false),
            CppPrimitiveKind.Float => new("float", null, false, false),
            CppPrimitiveKind.Double => new("double", null, false, false),
            CppPrimitiveKind.Long => new("nint", null, false, false),         // P0.2 — was "int"
            CppPrimitiveKind.UnsignedLong => new("nuint", null, false, false),
            _ => new("IntPtr", null, false, false),
        };
    }

    public BindingTypeRef MapPointer(CppPointerType ptr, BindingGenerationConfig familyConfig)
    {
        ArgumentNullException.ThrowIfNull(ptr);
        var element = ptr.ElementType;
        while (element is CppQualifiedType qt) element = qt.ElementType;

        return element switch
        {
            CppPrimitiveType { Kind: CppPrimitiveKind.Void } => new("IntPtr", null, true, false),
            CppPrimitiveType prim => new(MapPrimitive(prim).ManagedName + "*", null, true, false),
            CppTypedef td when _ownedTypes.IsOwned(td.Name) => MakeOpaqueHandleRef(td.Name, familyConfig),
            CppTypedef td => MapTypedefPointer(td, familyConfig),
            CppClass cls when cls.Name.StartsWith("ID", StringComparison.Ordinal) => new("IntPtr", null, true, false),
            CppClass cls when _ownedTypes.IsOwned(cls.Name) => MakeOpaqueHandleRef(cls.Name, familyConfig),
            CppClass cls => new(cls.Name + "*", null, true, false),
            _ => new("IntPtr", null, true, false),
        };
    }

    public BindingTypeRef MapTypedef(CppTypedef td, BindingGenerationConfig familyConfig, int depth = 0)
    {
        ArgumentNullException.ThrowIfNull(td);
        if (depth > MaxTypedefDepth)
        {
            throw new InvalidOperationException($"Typedef chain for '{td.Name}' exceeded max depth {MaxTypedefDepth}; likely a circular typedef.");
        }

        if (ExplicitTypedefMap.TryGetValue(td.Name, out var mapped))
        {
            return new BindingTypeRef(mapped, null, false, false);
        }

        var element = td.ElementType;
        while (element is CppQualifiedType qt) element = qt.ElementType;

        switch (element)
        {
            case CppPrimitiveType prim:
                return MapPrimitive(prim);
            case CppTypedef nested:
                return MapTypedef(nested, familyConfig, depth + 1);   // P0.1 — chain-resolve first
            case CppEnum:
                return new BindingTypeRef("int", null, false, false);
        }

        if (_ownedTypes.IsOwned(td.Name))
        {
            return MakeOpaqueHandleRef(td.Name, familyConfig);
        }

        return Map(td.ElementType, familyConfig);
    }

    public BindingTypeRef MapTypedefPointer(CppTypedef td, BindingGenerationConfig familyConfig)
    {
        var element = td.ElementType;
        while (element is CppQualifiedType qt) element = qt.ElementType;
        if (element is CppPrimitiveType prim) return new BindingTypeRef(MapPrimitive(prim).ManagedName + "*", null, true, false);
        if (element is CppTypedef nested) return MapTypedefPointer(nested, familyConfig);
        return new BindingTypeRef(td.Name + "*", null, true, false);
    }

    private BindingTypeRef MapClass(CppClass cls, BindingGenerationConfig familyConfig)
    {
        if (_ownedTypes.IsOwned(cls.Name))
        {
            return MakeOpaqueHandleRef(cls.Name, familyConfig);
        }
        return new BindingTypeRef(cls.Name, null, false, false);
    }

    private BindingTypeRef MakeOpaqueHandleRef(string sdlTypeName, BindingGenerationConfig familyConfig)
    {
        // For Stage 1 (sdl2-core), the handle is emitted within the same namespace; OwningFamilyId tracks
        // attribution so satellite emitters (Stage 2) can qualify it.
        return new BindingTypeRef(sdlTypeName, OwningFamilyId: familyConfig.FamilyId, IsPointer: false, IsOpaqueHandle: true);
    }

    private static BindingTypeRef UnknownFallback(CppType type)
    {
        // P0.3 — should ideally emit a warning to a thread-safe accumulator; for now, return IntPtr fallback
        // but a future patch surfaces a Warnings property on TypeMappingPolicy for the task to log.
        return new BindingTypeRef("IntPtr", null, false, false);
    }

    public string SafeIdentifier(string name, int parameterIndex = -1)
    {
        if (string.IsNullOrEmpty(name)) return parameterIndex >= 0 ? $"@_p{parameterIndex}" : "@_";
        var kind = SyntaxFacts.GetKeywordKind(name);
        if (kind != SyntaxKind.None) return "@" + name;
        return name;
    }
}
```

> **Implementer note**: The `Microsoft.CodeAnalysis.CSharp` package needs to be added to `Build.csproj` if not already present (via `dotnet add build/_build/Build.csproj package Microsoft.CodeAnalysis.CSharp`). Use the same version pinned for other Roslyn-dependent build-host code.

- [ ] **Step 3: Run tests — expect PASS**

Expected: 5 new TypeMappingPolicy tests pass.

### Task 3C.2: Extract `KnownUnsupportedDeclarationPolicy`

**Files:**
- Create: `build/_build/Targets/GenerateBindings/Model/KnownUnsupportedDeclarationPolicy.cs`
- Create: `build/_build.Tests/Unit/Targets/GenerateBindings/Model/KnownUnsupportedDeclarationPolicyTests.cs`

- [ ] **Step 1: Write failing tests**

```csharp
using Build.Data.BindingGeneration.Models;
using Build.Targets.GenerateBindings.Model;
using TUnit.Core;

namespace Build.Tests.Unit.Targets.GenerateBindings.Model;

public sealed class KnownUnsupportedDeclarationPolicyTests
{
    [Test]
    public async Task IsUnsupported_Should_Return_False_For_C_Variadic_Fmt_Only_Functions_By_Default()
    {
        var policy = new KnownUnsupportedDeclarationPolicy(ConfigFixtures.Sdl2CoreWithDynapi());
        await Assert.That(policy.IsUnsupported("SDL_Log", out _)).IsFalse();
        await Assert.That(policy.IsUnsupported("SDL_snprintf", out _)).IsFalse();
    }

    [Test]
    public async Task IsUnsupported_Should_Return_True_For_Manifest_Deferred_Declarations()
    {
        var policy = new KnownUnsupportedDeclarationPolicy(ConfigFixtures.Sdl2CoreWithDynapi());
        await Assert.That(policy.IsUnsupported("SDL_SysWMinfo", out var reason)).IsTrue();
        await Assert.That(reason).Contains("deferred-to-stage-2");
    }

    [Test]
    public async Task IsUnsupported_Should_Return_False_For_Normal_Symbol()
    {
        var policy = new KnownUnsupportedDeclarationPolicy(ConfigFixtures.Sdl2CoreWithDynapi());
        await Assert.That(policy.IsUnsupported("SDL_Init", out _)).IsFalse();
    }
}
```

- [ ] **Step 2: Implement `KnownUnsupportedDeclarationPolicy.cs`**

```csharp
using Build.Data.BindingGeneration.Models;

namespace Build.Targets.GenerateBindings.Model;

public sealed class KnownUnsupportedDeclarationPolicy
{
    private readonly IReadOnlyDictionary<string, string> _unsupported;

    public KnownUnsupportedDeclarationPolicy(BindingGenerationConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        var dict = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (name, deferred) in config.DeferredDeclarations)
        {
            dict[name] = $"{deferred.Category}: {deferred.Reason}";
        }
        _unsupported = dict;
    }

    public bool IsUnsupported(string declarationName, out string reason)
    {
        if (_unsupported.TryGetValue(declarationName, out var r))
        {
            reason = r;
            return true;
        }
        reason = string.Empty;
        return false;
    }
}
```

### Task 3C.3: Add `CoreOwnedTypeMap`

**Files:**
- Create: `build/_build/Targets/GenerateBindings/Model/CoreOwnedTypeMap.cs`
- Create: `build/_build.Tests/Unit/Targets/GenerateBindings/Model/CoreOwnedTypeMapTests.cs`

- [ ] **Step 1: Write tests**

```csharp
public sealed class CoreOwnedTypeMapTests
{
    [Test]
    public async Task IsOwned_Should_Return_True_For_SDL_Prefixed_Identifiers()
    {
        var map = new CoreOwnedTypeMap(ConfigFixtures.Sdl2CoreWithDynapi());
        await Assert.That(map.IsOwned("SDL_Surface")).IsTrue();
        await Assert.That(map.IsOwned("SDL_Renderer")).IsTrue();
        await Assert.That(map.IsOwned("SDLK_RETURN")).IsTrue();
        await Assert.That(map.IsOwned("SDL_HINT_RENDER_DRIVER")).IsTrue();
        await Assert.That(map.IsOwned("SDL_INIT_VIDEO")).IsTrue();
    }

    [Test]
    public async Task IsOwned_Should_Return_False_For_Non_Core_Identifiers()
    {
        var map = new CoreOwnedTypeMap(ConfigFixtures.Sdl2CoreWithDynapi());
        await Assert.That(map.IsOwned("IMG_Load")).IsFalse();
        await Assert.That(map.IsOwned("Mix_OpenAudio")).IsFalse();
        await Assert.That(map.IsOwned("TTF_OpenFont")).IsFalse();
    }

    [Test]
    public async Task QualifiedManagedReference_Should_Prepend_Namespace()
    {
        var map = new CoreOwnedTypeMap(ConfigFixtures.Sdl2CoreWithDynapi());
        await Assert.That(map.QualifiedManagedReference("SDL_Surface")).IsEqualTo("Janset.SDL2.SDL_Surface");
    }
}
```

- [ ] **Step 2: Implement `CoreOwnedTypeMap.cs`**

```csharp
using Build.Data.BindingGeneration.Models;

namespace Build.Targets.GenerateBindings.Model;

public sealed class CoreOwnedTypeMap
{
    private readonly IReadOnlyList<string> _ownedPrefixes;
    private readonly string _coreManagedNamespace;
    private readonly string _coreFamilyId;

    public CoreOwnedTypeMap(BindingGenerationConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        _ownedPrefixes = config.OwnedPrefixes;
        _coreManagedNamespace = $"Janset.{config.ManagedNamespace}";
        _coreFamilyId = config.FamilyId;
    }

    public bool IsOwned(string identifier)
    {
        ArgumentNullException.ThrowIfNull(identifier);
        return _ownedPrefixes.Any(p => identifier.StartsWith(p, StringComparison.Ordinal));
    }

    public string QualifiedManagedReference(string identifier)
    {
        ArgumentNullException.ThrowIfNull(identifier);
        return $"{_coreManagedNamespace}.{identifier}";
    }

    public string CoreFamilyId => _coreFamilyId;
}
```

- [ ] **Step 3: DI registration in `Targets/GenerateBindings/ServiceCollectionExtensions.cs`**

Register all three policies as transient (per-family scoping happens at construction time inside the task; DI provides a factory):

```csharp
services.AddTransient<Func<BindingGenerationConfig, CoreOwnedTypeMap>>(_ => config => new CoreOwnedTypeMap(config));
services.AddTransient<Func<BindingGenerationConfig, KnownUnsupportedDeclarationPolicy>>(_ => config => new KnownUnsupportedDeclarationPolicy(config));
services.AddTransient<Func<BindingGenerationConfig, CoreOwnedTypeMap, TypeMappingPolicy>>(_ => (config, owned) => new TypeMappingPolicy(config, owned));
```

- [ ] **Step 4: Run tests**

Expected: green.

- [ ] **Step 5: Suggested commit boundary**

```text
feat(binding-autogen): extract TypeMappingPolicy + KnownUnsupportedDeclarationPolicy + CoreOwnedTypeMap

- TypeMappingPolicy with P0.1 (chain-resolve typedef) + P0.2 (Long -> nint)
  + P0.3 (catch-all fallback explicit) + P2.8 (typedef depth guard, 16)
  + P2.9 (Roslyn SyntaxFacts.GetKeywordKind keyword set via Microsoft.CodeAnalysis.CSharp)
- KnownUnsupportedDeclarationPolicy with manifest deferred_declarations
  (SDL_SysWMinfo, SDL_SysWMmsg, explicit va_list/FILE*/external-native deferrals)
- CoreOwnedTypeMap with IsOwned + QualifiedManagedReference (consumed at Stage 2 satellite emit)
- DI factories register all three with BindingGenerationConfig + dependency injection
- Tests: TypeMappingPolicy 5 scenarios, KnownUnsupportedDeclarationPolicy 3, CoreOwnedTypeMap 3

CppAstToBindingModel still uses temporary string bridge from Phase 3B —
Phase 3D rewrites it to use these policies properly.
```

### Phase 3D — `CppAstToBindingModel` translator refactor

### Phase 3D-prime guardrail: CppAst fixture matrix + external type taxonomy

Before implementing structural category collection, pin the CppAst shapes that caused the current compile-check failures. Do not guess from generated C# errors alone.

Required fixture headers/tests:

- unnamed parameters: two unnamed parameters produce unique fallback names (`@_p0`, `@_p1`);
- SDL2 `SDL_bool`: enum-backed typedef maps to raw `int`;
- C variadic `...`: fmt parameter survives, trailing varargs do not create duplicate C# parameters;
- explicit `va_list`: deferred or mapped by policy, never leaks `__va_list_tag`;
- `FILE*` / `_IO_FILE*`: deferred or mapped by policy, never leaks `_IO_FILE`;
- SDL-owned opaque handle: empty `SDL_*` class/typedef becomes `BindingHandle`;
- SDL-owned POD struct: ordinary SDL-owned PODs become `BindingStruct`, while explicitly substituted values such as `SDL_GUID` map to their .NET type and are not emitted as structs;
- union: layout is explicit or deferred until layout proof exists;
- Vulkan: dispatchable/non-dispatchable handles are mapped explicitly (`VkInstance -> IntPtr`, `VkSurfaceKHR -> ulong`) or header/function deferred;
- GDK/platform SDK handles: mapped to `IntPtr`/opaque platform handles only through explicit platform policy.

The output of this slice is tests + taxonomy, not broad structural emission. It exists to prevent compile-green-but-ABI-wrong fixes.

Implementation hygiene:

- `Targets\GenerateBindings\Model\` is for binding declaration records only.
- `Targets\GenerateBindings\Translation\` owns CppAst translation and semantic policies: `CppAstToBindingModel`, `TypeMappingPolicy`, `ExternalNativeTypePolicy`, `KnownUnsupportedDeclarationPolicy`, and `CoreOwnedTypeMap`.
- `build\manifest.json` remains declarative family config; ABI semantics stay in translation policy code unless a real per-family override is needed.

### Task 3D.1: Rewrite `CppAstToBindingModel.Translate` to populate all 6 categories

**Files:**
- Modify: `build/_build/Targets/GenerateBindings/Model/CppAstToBindingModel.cs`
- Modify: `build/_build.Tests/Unit/Targets/GenerateBindings/Model/*` (translator tests)

This task is large; below is the high-level structure. The implementer fills in the per-category collection methods following the existing `ExtractFunctions` pattern.

- [ ] **Step 1: Rewrite the translator entry**

```csharp
using Build.Data.BindingGeneration.Models;
using Build.Targets.GenerateBindings.Parsing;
using CppAst;

namespace Build.Targets.GenerateBindings.Model;

public sealed class CppAstToBindingModel(TypeMappingPolicy typeMapping, KnownUnsupportedDeclarationPolicy unsupported, CoreOwnedTypeMap ownedTypes)
{
    public BindingModel Translate(IReadOnlyList<CppAstParseResult> parseResults, BindingGenerationConfig config)
    {
        ArgumentNullException.ThrowIfNull(parseResults);
        ArgumentNullException.ThrowIfNull(config);

        var requiredAsFunctions = ConvertRequired(config.RequiredFunctions);

        var views = CollectViews(parseResults, config, requiredAsFunctions);
        var structs = CollectStructs(parseResults, config);
        var enums = CollectEnums(parseResults, config);
        var constants = CollectConstants(parseResults, config);
        var handles = CollectHandles(structs, parseResults, config);
        var callbacks = CollectCallbacks(parseResults, config);

        return new BindingModel(views, structs, enums, constants, handles, callbacks);
    }

    // CollectViews — same shape as the existing ExtractFunctions / ExtractNeutralFunctionNames logic
    // but using TypeMappingPolicy + KnownUnsupportedDeclarationPolicy instead of inline static methods.

    // CollectStructs — iterate compilation.Classes, filter SDL2-sourced, build BindingStruct + Fields.
    // Honour KnownUnsupportedDeclarationPolicy.IsUnsupported (skip SDL_SysWMinfo/SDL_SysWMmsg with logging).

    // CollectEnums — iterate compilation.Enums, build BindingEnum + Members. Detect IsFlags via SDL_* naming convention
    // ("Flags" suffix) or value-pattern (powers of 2).

    // CollectConstants — iterate compilation.Macros (which CppAst captures because ParseMacros=true);
    // categorize Literal vs Computed by attempting to parse the macro body.

    // CollectHandles — for every opaque CppClass with no fields that's SDL_*-prefixed (or owned-prefix-prefixed),
    // emit a BindingHandle. Handles' actual emission uses Rule 2 typed-struct shape (Phase 3E).

    // CollectCallbacks — iterate compilation.Typedefs whose ElementType is CppFunctionType.
    // Build BindingCallback with return type + parameters mapped via TypeMappingPolicy.

    private IReadOnlyList<BindingFunction> ConvertRequired(IReadOnlyList<RequiredFunctionConfig> required)
        => [.. required.Select(rf => new BindingFunction(
            Name: rf.Name,
            ReturnType: new BindingTypeRef(rf.ReturnType, null, false, false),
            Parameters: [.. rf.Parameters.Select(p => new BindingParameter(new BindingTypeRef(p.Type, null, false, false), p.Name))],
            SourceHeader: rf.SourceHeader,
            IsVariadic: false))];

    // ... per-category methods follow ...
}
```

- [ ] **Step 2: Implement per-category collection methods**

Each method follows the same skeleton:

```csharp
private IReadOnlyList<BindingStruct> CollectStructs(IReadOnlyList<CppAstParseResult> parseResults, BindingGenerationConfig config)
{
    var seen = new HashSet<(string SourceFile, string Name)>();
    var structs = new List<BindingStruct>();

    foreach (var result in parseResults)
    {
        foreach (var compilation in result.Compilations)
        {
            foreach (var cls in compilation.Classes)
            {
                if (!IsSdl2Sourced(cls.SourceFile)) continue;
                if (string.IsNullOrWhiteSpace(cls.Name)) continue;
                if (cls.Fields.Count == 0) continue;  // opaque -> goes to handles
                if (unsupported.IsUnsupported(cls.Name, out _)) continue;

                var key = (cls.SourceFile ?? "", cls.Name);
                if (!seen.Add(key)) continue;

                var fields = cls.Fields.Select(f => new BindingStructField(
                    Name: typeMapping.SafeIdentifier(f.Name),
                    Type: typeMapping.Map(f.Type, config),
                    FieldOffset: null))   // Phase 3E may compute offsets via CppAst
                    .ToList();

                structs.Add(new BindingStruct(
                    Name: cls.Name,
                    Fields: fields,
                    Layout: cls.ClassKind == CppClassKind.Union ? LayoutKind.Explicit : LayoutKind.Sequential,
                    ExplicitSize: null));
            }
        }
    }

    return [.. structs.OrderBy(s => s.Name, StringComparer.Ordinal)];
}
```

Implementer applies the same pattern to `CollectEnums`, `CollectConstants`, `CollectHandles`, `CollectCallbacks`. Refer to CppAst's `CppCompilation.Enums`, `Macros`, `Classes`, `Typedefs` API surface.

- [ ] **Step 3: Update `GenerateBindingsTask` to construct the translator from per-family config**

In `GenerateBindingsTask.GenerateOneFamilyAsync`, build the policies from config and pass to the translator:

```csharp
var owned = new CoreOwnedTypeMap(config);
var typeMap = new TypeMappingPolicy(config, owned);
var unsupported = new KnownUnsupportedDeclarationPolicy(config);
var translator = new CppAstToBindingModel(typeMap, unsupported, owned);

var model = translator.Translate(parseResults, config);
```

- [ ] **Step 4: Write tests for the new collection methods**

For each category, a focused unit test that constructs minimal `CppCompilation` fixtures or uses small representative SDL2 headers. Reference the existing `CppAstParseRunnerTests` pattern for the parse-fixture shape.

- [ ] **Step 5: Run tests + container smoke**

Expected: tests green; container smoke now produces a non-empty `Structs`/`Enums`/`Constants`/`Handles`/`Callbacks` portion of `BindingModel` (logged), but the emitter still only emits `Commands.g.cs` so output files don't change yet.

- [ ] **Step 6: Suggested commit boundary**

```text
feat(binding-autogen): extract binding translation collaborators

- CppAstToBindingModel reduced to orchestration
- BindingFunctionTranslator owns function filtering/dedup/type mapping
- NeutralFunctionSetBuilder owns required-function merge into Neutral
- BindableDeclarationPolicy owns source-header/ownership/exclusion rules
- BindingStructTranslator owns SDL-owned struct/union discovery
- StructFieldTranslator owns fixed buffers and anonymous nested union modeling
- Type mapping flows through TypeMappingPolicy (P0.1/P0.2/P0.3/P2.8/P2.9 baked)
- Unsupported declarations skipped via KnownUnsupportedDeclarationPolicy
  (variadic baseline + manifest deferred_declarations like SDL_SysWMinfo)
- No Stage1StructNames allowlist or SDL_GameControllerButtonBind-specific flattening

Emitter still emits Commands.g.cs only unless the task explicitly includes the category emitter slice.
Phase 3E adds or finalizes per-category emitters.
```

### Phase 3E — Per-category emitters

### Task 3E.1: Add `EmitContext` + `CodeWriter` + `BindingEmitter` dispatcher

**Files:**
- Create: `build/_build/Targets/GenerateBindings/Emitting/EmitContext.cs`
- Modify: `build/_build/Targets/GenerateBindings/Emitting/CodeWriter.cs` (may already exist; otherwise create)
- Create: `build/_build/Targets/GenerateBindings/Emitting/BindingEmitter.cs`

- [ ] **Step 1: Write `EmitContext.cs`**

```csharp
using Build.Data.BindingGeneration.Models;
using Build.Targets.GenerateBindings.Model;

namespace Build.Targets.GenerateBindings.Emitting;

public sealed record EmitContext(
    BindingGenerationConfig Config,
    CoreOwnedTypeMap OwnedTypes,
    TypeMappingPolicy TypeMapping,
    KnownUnsupportedDeclarationPolicy Unsupported);
```

- [ ] **Step 2: Verify/create `CodeWriter.cs`**

If a `CodeWriter` already exists from spike work, ensure it has `AppendLf` (LF-only line endings, host-OS-agnostic), indentation tracking, and a `ToString()`/`Build()` method that returns the final string. Otherwise create one matching the existing `Build.Host.Text.AppendLf` extension pattern.

- [ ] **Step 3: Write `BindingEmitter.cs`**

```csharp
using Build.Targets.GenerateBindings.Model;

namespace Build.Targets.GenerateBindings.Emitting;

public sealed class BindingEmitter(
    CsConstantEmitter constantEmitter,
    CsEnumEmitter enumEmitter,
    CsHandleEmitter handleEmitter,
    CsStructEmitter structEmitter,
    CsCallbackEmitter callbackEmitter,
    CsCommandEmitter commandEmitter)
{
    public GeneratedFileSet Emit(BindingModel model, EmitContext ctx)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(ctx);

        var files = new List<GeneratedFile>();
        // Deterministic order — earlier categories define types referenced by later ones.
        files.AddRange(constantEmitter.Emit(model, ctx));
        files.AddRange(enumEmitter.Emit(model, ctx));
        files.AddRange(handleEmitter.Emit(model, ctx));
        files.AddRange(structEmitter.Emit(model, ctx));
        files.AddRange(callbackEmitter.Emit(model, ctx));
        files.AddRange(commandEmitter.Emit(model, ctx));
        files.Add(new GeneratedFile("parse-views.json", BindingParseViewReport.Serialize(model)));
        return new GeneratedFileSet(files);
    }
}
```

### Task 3E.2: Implement `CsHandleEmitter` (Rule 2 typed handles)

**Files:**
- Create: `build/_build/Targets/GenerateBindings/Emitting/CsHandleEmitter.cs`
- Create: `build/_build.Tests/Unit/Targets/GenerateBindings/Emitting/CsHandleEmitterTests.cs`

- [ ] **Step 1: Write failing test**

```csharp
public sealed class CsHandleEmitterTests
{
    [Test]
    public async Task Emit_Should_Generate_Readonly_Partial_Struct_With_Full_Rule2_Feature_Set()
    {
        var emitter = new CsHandleEmitter();
        var model = ModelFixtures.WithHandles([new BindingHandle("SDL_Window")]);
        var ctx = EmitContextFixtures.Sdl2Core();

        var files = emitter.Emit(model, ctx);

        await Assert.That(files.Count).IsEqualTo(1);
        var content = files[0].Content;
        await Assert.That(content).Contains("public readonly partial struct SDL_Window(nint value) : IEquatable<SDL_Window>");
        await Assert.That(content).Contains("public readonly nint Value = value;");
        await Assert.That(content).Contains("public bool IsNull => Value == 0;");
        await Assert.That(content).Contains("public static SDL_Window Null => new(0);");
        await Assert.That(content).Contains("public static implicit operator nint(SDL_Window value) => value.Value;");
        await Assert.That(content).Contains("public bool Equals(SDL_Window other) => Value.Equals(other.Value);");
        await Assert.That(content).Contains("DebuggerDisplay");
    }
}
```

- [ ] **Step 2: Implement emitter**

```csharp
using System.Text;
using Build.Host.Text;
using Build.Targets.GenerateBindings.Model;

namespace Build.Targets.GenerateBindings.Emitting;

public sealed class CsHandleEmitter
{
    public IReadOnlyList<GeneratedFile> Emit(BindingModel model, EmitContext ctx)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(ctx);

        if (model.Handles.Count == 0) return [];

        var sb = new StringBuilder();
        sb.AppendLf("// <auto-generated />");
        sb.AppendLf();
        sb.AppendLf("using System;");
        sb.AppendLf("using System.Diagnostics;");
        sb.AppendLf();
        sb.Append("namespace ").Append("Janset.").Append(ctx.Config.ManagedNamespace).AppendLf(";");
        sb.AppendLf();

        foreach (var handle in model.Handles.OrderBy(h => h.Name, StringComparer.Ordinal))
        {
            sb.AppendLf("[DebuggerDisplay(\"{DebuggerDisplay,nq}\")]");
            sb.Append("public readonly partial struct ").Append(handle.Name).Append("(nint value) : IEquatable<").Append(handle.Name).AppendLf(">");
            sb.AppendLf("{");
            sb.AppendLf("    public readonly nint Value = value;");
            sb.AppendLf();
            sb.AppendLf("    public bool IsNull => Value == 0;");
            sb.Append("    public static ").Append(handle.Name).AppendLf(" Null => new(0);");
            sb.AppendLf();
            sb.Append("    public static implicit operator nint(").Append(handle.Name).AppendLf(" value) => value.Value;");
            sb.Append("    public static implicit operator ").Append(handle.Name).AppendLf("(nint value) => new(value);");
            sb.AppendLf();
            sb.Append("    public static bool operator ==(").Append(handle.Name).Append(" left, ").Append(handle.Name).AppendLf(" right) => left.Value == right.Value;");
            sb.Append("    public static bool operator !=(").Append(handle.Name).Append(" left, ").Append(handle.Name).AppendLf(" right) => left.Value != right.Value;");
            sb.AppendLf();
            sb.Append("    public bool Equals(").Append(handle.Name).AppendLf(" other) => Value.Equals(other.Value);");
            sb.Append("    public override bool Equals(object? obj) => obj is ").Append(handle.Name).AppendLf(" other && Equals(other);");
            sb.AppendLf("    public override int GetHashCode() => Value.GetHashCode();");
            sb.AppendLf();
            sb.Append("    private string DebuggerDisplay => $\"").Append(handle.Name).AppendLf(" [0x{Value:X}]\";");
            sb.AppendLf("}");
            sb.AppendLf();
        }

        return [new GeneratedFile("Handles.g.cs", sb.ToString())];
    }
}
```

- [ ] **Step 3: Run tests**

Expected: green.

### Task 3E.3 — 3E.6: Implement the other 4 emitters (Constants, Enums, Structs, Callbacks)

Each follows the same TDD shape:

- [ ] **Task 3E.3: `CsConstantEmitter`** — emit `public const` for `ConstantKind.Literal`, `public static readonly` for `ConstantKind.Computed`. Test: assert both shapes for representative SDL_* macro inputs.

- [ ] **Task 3E.4: `CsEnumEmitter`** — emit `public enum Name : underlyingType { Member = value, ... }` with `[Flags]` when `IsFlags == true`. Test: assert both shapes; assert `[Flags]` attribute.

- [ ] **Task 3E.5: `CsStructEmitter`** — emit `[StructLayout(LayoutKind.Sequential)]` for normal structs, `[StructLayout(LayoutKind.Explicit)]` with `[FieldOffset(N)]` for unions. Field types render via `BindingTypeRef.ManagedName`. Test: assert layout attribute, field offsets, type rendering.

- [ ] **Task 3E.6: `CsCallbackEmitter`** — emit `public unsafe delegate* unmanaged[Cdecl]<ReturnType, Params, void>` typedef alias as a struct field type within the callback's BindingCallback record. For Stage 1 simplicity emit `[UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate ReturnType Name(...)` as the legacy form alongside the `delegate*` modern form. Test: assert both shapes.

Each emitter task structure:

```text
Step 1: Write failing CsXxxEmitterTests.cs (3-5 scenarios)
Step 2: Run -- expect FAIL (emitter not defined)
Step 3: Implement CsXxxEmitter.cs
Step 4: Run -- expect PASS
Step 5: Commit (suggested)
```

### Task 3E.7: Refactor `CsCommandEmitter` to read from new BindingModel + use EmitContext

**Files:**
- Modify: `build/_build/Targets/GenerateBindings/Emitting/CsCommandEmitter.cs`
- Modify: `build/_build.Tests/Unit/Targets/GenerateBindings/Emitting/CsCommandEmitterTests.cs`

- [ ] **Step 1: Update CsCommandEmitter to take EmitContext + emit one `Commands.g.cs` (Neutral) + per-platform `Platform/<View>/Commands.g.cs`**

The current emitter already does this; refactor signature to `(BindingModel model, EmitContext ctx) -> IReadOnlyList<GeneratedFile>`. Read namespace from `ctx.Config.ManagedNamespace`. Read primary class name from `ctx.Config.PrimaryClassName` (`public static partial class SDL`).

- [ ] **Step 2: Wire BindingEmitter into GenerateBindingsTask**

Replace the `var fileSet = PreviewEmitter.Emit(model);` line (or `CsCommandEmitter.Emit(model)`) with:

```csharp
var bindingEmitter = new BindingEmitter(constantEmitter, enumEmitter, handleEmitter, structEmitter, callbackEmitter, commandEmitter);
var emitCtx = new EmitContext(config, owned, typeMap, unsupported);
var fileSet = bindingEmitter.Emit(model, emitCtx);
```

- [ ] **Step 3: DI registration for all 6 emitter classes + BindingEmitter**

In `Targets/GenerateBindings/ServiceCollectionExtensions.cs`:

```csharp
services.AddSingleton<CsConstantEmitter>();
services.AddSingleton<CsEnumEmitter>();
services.AddSingleton<CsHandleEmitter>();
services.AddSingleton<CsStructEmitter>();
services.AddSingleton<CsCallbackEmitter>();
services.AddSingleton<CsCommandEmitter>();
services.AddSingleton<BindingEmitter>();
```

- [ ] **Step 4: Run tests + container smoke**

```pwsh
dotnet test --project build/_build.Tests/Build.Tests.csproj -c Release --framework net10.0
dotnet run --file tools.cs -- generate-bindings
```

Expected: container smoke produces:

```
artifacts/generated-bindings-preview/sdl2-core/
├── Constants.g.cs
├── Enums.g.cs
├── Handles.g.cs
├── Structs.g.cs
├── Callbacks.g.cs
├── Commands.g.cs
├── Platform/Neutral/Commands.g.cs
├── Platform/WindowsDesktop/Commands.g.cs
├── ... (8 platform views)
└── parse-views.json
```

All files compile cleanly under SDL2.Core csproj (`dotnet build` succeeds).

- [ ] **Step 5: Suggested commit boundary**

```text
feat(binding-autogen): six per-category emitters + BindingEmitter dispatcher

- EmitContext record (config + 3 policies)
- CsConstantEmitter (literal vs computed)
- CsEnumEmitter (with [Flags])
- CsHandleEmitter (Rule 2 typed readonly partial struct with full feature set)
- CsStructEmitter ([StructLayout(Sequential)] / [StructLayout(Explicit)] union)
- CsCallbackEmitter (delegate* unmanaged[Cdecl] + legacy [UnmanagedFunctionPointer])
- CsCommandEmitter refactored to consume BindingModel + EmitContext
- BindingEmitter orchestrates emit order (Constants -> Enums -> Handles ->
  Structs -> Callbacks -> Commands)
- DI registers all 6 emitter classes + BindingEmitter

Output: 6 per-category .g.cs files + 8 Platform/<View>/Commands.g.cs.
Phase 3F adds friendly overloads + dual P/Invoke emit in CsCommandEmitter.
```

### Phase 3F — Friendly overloads + dual P/Invoke emit

### Task 3F.1: Add friendly-overload generation to CsCommandEmitter

**Files:**
- Modify: `build/_build/Targets/GenerateBindings/Emitting/CsCommandEmitter.cs`

- [ ] **Step 1: Write failing tests**

```csharp
[Test]
public async Task Emit_Should_Generate_Utf8_String_Overload_For_Byte_Pointer_Params()
{
    // Input: SDL_LoadBMP(const char* file) -> byte* file at AST level
    // Expected: emit internal `byte*` raw ABI extern + public byte* low-level wrapper
    // + `ReadOnlySpan<byte>` overload + `string` overload.
    // `string` is convenience and may allocate while encoding UTF-16 -> UTF-8.
}

[Test]
public async Task Emit_Should_Generate_Out_Overload_For_Pointer_Output_Params()
{
    // Input: SDL_GetWindowSize(SDL_Window* window, int* w, int* h)
    // Expected: raw int* + `out int w, out int h` overload
}

[Test]
public async Task Emit_Should_Generate_Span_Overload_For_Buffer_Params()
{
    // Input: SDL_RWread(SDL_RWops* ctx, void* buffer, size_t size, size_t count)
    // Expected: raw void* + `Span<byte> buffer` overload
}
```

- [ ] **Step 2: Implement the overload-generation logic**

This is non-trivial. The implementer adds methods to `CsCommandEmitter`:

```csharp
private void EmitFunction(StringBuilder sb, BindingFunction func, EmitContext ctx)
{
    EmitInternalRawAbi(sb, func, ctx);                             // internal byte* / int* / void* signature
    EmitLowLevelWrapper(sb, func, ctx);                            // public typed low-level API, no extern attribute
    if (HasUtf8StringParam(func)) EmitUtf8Overloads(sb, func, ctx);
    if (HasOutputPointerParam(func)) EmitOutOverload(sb, func, ctx);
    if (HasBufferParam(func)) EmitSpanOverload(sb, func, ctx);
}

private static bool HasUtf8StringParam(BindingFunction f) =>
    f.Parameters.Any(p => p.Type.ManagedName == "byte*" && /* heuristic: const char* */ true);
// ... helpers
```

The string heuristic: any `const char*` parameter (which maps to `byte*` per `MapPointer`) is a UTF-8 candidate. Prefer extending `BindingTypeRef` with an `IsConstCharPointer` or `StringEncoding` flag before broad emission so friendly overloads do not rely on comment/name inference.

- [ ] **Step 3: Run tests + verify container smoke + try to compile generated SDL2.Core**

Compile the generated source against the compile-check project to verify the internal raw ABI layer, public low-level wrappers, and friendly overloads compile cleanly.

```pwsh
# Compile generated against a tiny harness csproj
# Or, temporarily wire artifacts/generated-bindings-preview/sdl2-core/*.cs into a harness
dotnet build /tmp/sdl2-core-harness.csproj
```

Expected: green compile.

### Task 3F.2: Add dual P/Invoke emit (LibraryImport net7+ / DllImport legacy)

**Files:**
- Modify: `build/_build/Targets/GenerateBindings/Emitting/CsCommandEmitter.cs`

- [ ] **Step 1: Add `#if NET7_0_OR_GREATER` branching to each function emit**

Modify `EmitRawPInvoke` to produce both shapes:

```csharp
sb.AppendLf("#if NET7_0_OR_GREATER");
sb.Append("    [LibraryImport(LibName, EntryPoint = \"").Append(func.Name).AppendLf("\")]");
// Raw ABI methods keep byte*/T* signatures. UTF-8 string convenience is emitted
// by public wrapper overloads, not by the raw extern attribute.
sb.Append("    internal static partial ").Append(func.ReturnType.ManagedName).Append(' ').Append(func.Name).AppendLf("(...);");
sb.AppendLf("#else");
sb.Append("    [DllImport(LibName, EntryPoint = \"").Append(func.Name).AppendLf("\", CallingConvention = CallingConvention.Cdecl)]");
sb.Append("    internal static extern ").Append(func.ReturnType.ManagedName).Append(' ').Append(func.Name).AppendLf("(...);");
sb.AppendLf("#endif");
```

- [ ] **Step 2: Tests**

```csharp
[Test]
public async Task Emit_Should_Produce_LibraryImport_For_Net7_And_DllImport_Fallback()
{
    var content = /* emit SDL_Quit() */;
    await Assert.That(content).Contains("#if NET7_0_OR_GREATER");
    await Assert.That(content).Contains("[LibraryImport(LibName, EntryPoint = \"SDL_Quit\")]");
    await Assert.That(content).Contains("internal static partial void SDL_Quit()");
    await Assert.That(content).Contains("#else");
    await Assert.That(content).Contains("[DllImport(LibName, EntryPoint = \"SDL_Quit\", CallingConvention = CallingConvention.Cdecl)]");
    await Assert.That(content).Contains("internal static extern void SDL_Quit()");
    await Assert.That(content).Contains("#endif");
}
```

- [ ] **Step 3: Run tests + smoke**

Expected: green.

- [ ] **Step 4: Suggested commit boundary**

```text
feat(binding-autogen): friendly overloads + dual P/Invoke emit (CppAst Rule 1+4+6)

- CsCommandEmitter generates internal raw byte*/int*/void* ABI signature
- Adds public typed low-level wrapper over the internal raw ABI method
- Adds string overload for const char* params (convenience, may allocate)
- Adds ReadOnlySpan<byte> overload (pins via fixed, dispatches to raw)
- Adds out T overload for single output pointer params
- Adds Span<T> overload for buffer params
- Wraps every function in #if NET7_0_OR_GREATER [LibraryImport] / #else [DllImport]
  block so both modern + legacy TFMs emit from same emitter pass
```

### Phase 3G — Output wiring + smoke + peer-oracle diff

### Task 3G.0: Add compile-check non-empty input guard before promoting the gate

The diagnostic compile-check project currently glob-includes `artifacts\generated-bindings-preview\sdl2-core\**\*.g.cs`. Before wiring it into an opt-in or blocking flow, add a guard that fails when the generated input set is empty. Otherwise a missing/stale preview folder can produce a false green.

- [ ] **Step 1: Add a generated-file existence check**

Implement this either in the compile-check project file or in the `tools.cs generate-bindings --compile-check` wrapper when that wrapper exists. The failure message must say which generated root was empty.

- [ ] **Step 2: Verify failure mode**

Temporarily point the check at an empty generated root and confirm it fails with the explicit empty-input message.

- [ ] **Step 3: Keep the check opt-in until generated output moves to `src\SDL2.Core\Generated`**

The real `src\SDL2.Core\SDL2.Core.csproj` build becomes the blocking compile gate only after Task 7 flips from preview artifacts to production source.

### Task 3G.1: Final acceptance gates

- [ ] **Step 1: Full container regen + 5-TFM compile**

```pwsh
dotnet run --file tools.cs -- generate-bindings

# Wire the regenerated output into src/SDL2.Core (temporary; Task 7 flip is separate slice)
# For Phase 3G smoke only, set up a temporary csproj that compiles the output
# against all 5 target frameworks.
```

Expected: regenerate produces 6 per-category .g.cs + 8 platform .g.cs + parse-views.json; SDL2.Core temporary harness compiles under `net10`, `net9`, `net8`, `netstandard2.0`, `net462`.

- [ ] **Step 2: Run all three family validators**

The validators run automatically as part of `GenerateBindings`. Verify the log shows:

```
[dynapi-coherence] 0 errors, 0 warnings
[neutral-view-non-empty] 0 errors
[required-functions-emitted] 0 errors
```

- [ ] **Step 3: Peer-oracle visual diff**

Compare the regenerated `Commands.g.cs` / `Handles.g.cs` / `Structs.g.cs` against:

```pwsh
# Visual side-by-side
code --diff external/sdl2-cs/src/SDL2.cs artifacts/generated-bindings-preview/sdl2-core/Commands.g.cs
code --diff tools/binding-spike/cppast-platform/bindings/Generated/SDL2.Platform.Neutral.g.cs artifacts/generated-bindings-preview/sdl2-core/Platform/Neutral/Commands.g.cs
# Pull Alimer + ppy outputs for comparison
```

Categorize differences as:
- Typed-handle delta (expected: we emit `SDL_Window window` with `readonly partial struct`, sdl2-cs emits `IntPtr window`)
- Friendly-overload delta (expected: we emit string + Span + out overloads, sdl2-cs raw-only)
- Known SDL2-vs-SDL3 API drift
- True generator defect

- [ ] **Step 4: Suggested commit boundary (Phase 3 exit)**

```text
feat(binding-autogen): Stage 1 SDL2.Core production binding generator — real shape

End of unified slice. Output:
- src/SDL2.Core/Generated/* (when Task 7 flag-flips; for now artifacts/generated-bindings-preview/sdl2-core/)
- 6 per-category .g.cs (Constants/Enums/Handles/Structs/Callbacks/Commands)
- 8 Platform/<View>/Commands.g.cs (Neutral/WindowsDesktop/WinRT/GDK/Linux/MacOS/IOS/Android)
- parse-views.json audit sidecar

Architecture:
- manifest.json v2.2 binding_generation block per family
- GenerateBindingsTask loops enabled families; --family X narrows
- BindingModel: 6 declaration categories + BindingTypeRef
- TypeMappingPolicy + KnownUnsupportedDeclarationPolicy + CoreOwnedTypeMap extracted
- IBindingFamilyValidator: DynapiCoherence / NeutralViewNonEmpty / RequiredFunctionsEmitted
- 6 per-category emitters + BindingEmitter dispatcher + EmitContext
- Rule 1 dual emit (LibraryImport/DllImport) + Rule 2 typed handles + Rules 4+6 overloads

Out of unified slice: Task 7 (flag-flip from artifacts -> src/SDL2.Core/Generated/),
Task 8 (PreFlight stamp drift validator), Task 9 (public-API snapshot tests),
Stage 2 satellites, Stage 3 SDL3.
```

---

## Self-review

**Spec coverage check** (against `2026-05-16-binding-generator-unified-design.md`):

| Spec section | Plan task(s) | Status |
|---|---|---|
| §1 Goal | All phases | ✅ |
| §2 Approved Scope | Phase 1-3G | ✅ |
| §2 Out of scope | Documented in plan + spec refs | ✅ (Tasks 7/8/9, Stage 2, Stage 3, visibility rollback, SysWM stubs explicitly excluded) |
| §3 Architecture Summary | Phase 2D task body | ✅ |
| §4 manifest.json v2.2 schema | Task 2A.2 + 2A.3 | ✅ |
| §5 Per-family fan-out | Task 2D.2 Step 3 | ✅ |
| §6 BindingModel + 6 categories | Phase 3B | ✅ |
| §7 Per-category emitter classes | Phase 3E (Tasks 3E.1–3E.7) | ✅ |
| §8 TypeMappingPolicy + KnownUnsupportedDeclarationPolicy | Phase 3C (Tasks 3C.1–3C.3) | ✅ |
| §9 IBindingFamilyValidator | Phase 2C (Tasks 2C.1–2C.2) | ✅ |
| §10 CoreOwnedTypeMap + cross-family refs | Task 3C.3 + Phase 3D translator | ✅ (Stage 2 activation acknowledged) |
| §11 Output topology | Task 3G.1 acceptance | ✅ |
| §12 5 production-shape anchors | Phase 3E + 3F | ✅ |
| §13 Multi-pass parsing preserved | Task 2D.2 (CppAstParseRunner reads config) | ✅ |
| §14 Local Docker loop preserved | Plan does not touch | ✅ |
| §15 Stage boundary | Plan scope statement | ✅ |
| §16 P0/P1/P2/P3/PSTH absorption | Tasks 3A.2 Step 3 (P2.14–P2.19); Task 3C.1 (P0.1/P0.2/P0.3/P2.7/P2.8/P2.9); Phase 2 (PSTH-B/C/D/E); Phase 3A (PSTH-A + P3.10); Phase 1 (PSTH-F + P3.12 reframing) | ✅ |
| §17 Failure policy / determinism / validation | Phase 2C validators + Phase 3 acceptance gates | ✅ |

**Placeholder scan**: zero `TBD` / `TODO` / `implement later` / `fill in details` markers. A few "implementer fills in" notes appear in Phase 3D/3E for CppAst-specific helper construction — these are intentional pointers to known CppAst API surface, not undefined work.

**Type consistency check**: spec uses `BindingModel`, `BindingTypeRef`, `BindingFunction` (post-Phase-3A names). Plan uses `PreviewBindingModel` etc. up through Phase 2 (pre-rename) and switches to `BindingModel` from Phase 3A onward — consistent with the rename boundary.

**Naming consistency check**:
- Repository: `BindingGenerationConfigRepository` (Phase 2B) — consistent throughout
- Validator IDs: `dynapi-coherence`, `neutral-view-non-empty`, `required-functions-emitted` (Phase 2C) — consistent with manifest §4 schema
- Policy classes: `TypeMappingPolicy`, `KnownUnsupportedDeclarationPolicy`, `CoreOwnedTypeMap` (Phase 3C) — match spec §8
- Emitters: `CsConstantEmitter`, `CsEnumEmitter`, `CsHandleEmitter`, `CsStructEmitter`, `CsCallbackEmitter`, `CsCommandEmitter`, dispatcher `BindingEmitter` (Phase 3E) — match spec §7

**Known gaps acknowledged**:
- Phase 3D/3E include "implementer fills in" pointers for CppAst-specific patterns where the exact API surface needs the implementer to choose (e.g. CppAst's enum-flag detection heuristic, macro literal-vs-computed parsing). These are intentional handoffs to the implementer's CppAst familiarity, not plan failures.
- Phase 3F friendly-overload heuristics for `const char*` detection use a temporary "all `byte*` are string candidates" heuristic; a future patch could extend `BindingTypeRef` with `IsConstCharPointer` for precision. Documented as a known refinement, not a blocker.

**Approval-gate compliance check**: every potential commit boundary is suggested, not enforced. Manifest schema bump (Task 2A.1–2A.4) has explicit 🔒 approval marker. Rider rename (Phase 3A) is handoff-driven, not agent-executed. Plan respects AGENTS.md §"Approval Gate" throughout.
