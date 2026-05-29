# Binding Generator Maintenance Playbook

> **Status:** Canonical maintenance playbook for the ClangSharp + Roslyn postprocess binding generator under `spikes/binding-generators/clangsharp/`. Replaces the sunset Cake-hosted CppAst maintenance playbook. Restores to `docs/playbook/` at Production Flip (see roadmap §"Production Flip — SDL2.Core Reproducibility").

Operational procedures for maintaining generated bindings, the `family-config.json` schema, the three-tier RSP file organization, the seven-step Roslyn postprocess pipeline, and the version-coherent toolchain pin set. Generated bindings are public API; the generator is build infrastructure that produces a contract consumers depend on. Treat every maintenance change as a public-API change until proven otherwise via the drift detection workflow in §"Drift Detection Between Versions".

## 1. Purpose And When To Use

This playbook covers maintenance work that touches the ClangSharp + Roslyn binding generator, the `family-config.json` configuration surface, the RSP files that drive the per-header parse, the postprocess pipeline, or the native hybrid-static inputs the generated bindings depend on.

Use this playbook when:

- **SDL2 version bump** lands through a vcpkg baseline or library-version field change (per-family `library_version` in `family-config.json`).
- **vcpkg baseline update** changes upstream port patches without changing SDL upstream version — header patches can still change ABI-relevant declarations.
- **ClangSharp tool version bump** in `spikes/binding-generators/.config/dotnet-tools.json`.
- **libclang.runtime.*** version changes through the ClangSharp tool's transitive dependency surface.
- **Microsoft.CodeAnalysis.CSharp postprocess version bump** in `spikes/binding-generators/clangsharp/postprocess/Janset.SDL2.PostProcess.csproj`.
- **`global.platform_views` or `global.all_platform_macros` in `family-config.json`** need review (new platform target, new SDL platform macro, retired backend).
- **New SDL satellite family enters scope** — see §"Adding A New SDL Satellite Family".
- **SDL3 planning starts** — SDL3 platform macros (`SDL_PLATFORM_*`) are a new catalog, not an extension of SDL2's; the maintenance posture below is SDL2-specific.
- **Overlay triplets or vcpkg ports change** in a way that may affect exported symbols, header visibility, or SDL feature flags.

The maintenance posture is intentionally conservative: assume regenerate-and-diff is mandatory, demote to no-op only after the diff confirms zero output drift.

## 2. Toolchain Version Pinning

The ClangSharp + Roslyn toolchain is a **coherent-set quartet** that moves as one unit. Bumping any single member without coordinated revalidation creates drift between AST parsing, postprocess rewriting, and evidence reporting that surfaces as runtime parse failures, postprocess crashes, or mismatched generated output between local runs and CI.

The four pinned components:

| Component | Pin location | Current pin |
| --- | --- | --- |
| ClangSharp tool | `spikes/binding-generators/.config/dotnet-tools.json` | `clangsharppinvokegenerator` 17.0.1 |
| libclang.runtime.* | Transitive via ClangSharp tool package | 20.x line |
| Microsoft.CodeAnalysis.CSharp (postprocess) | `spikes/binding-generators/clangsharp/postprocess/Janset.SDL2.PostProcess.csproj` `VersionOverride` | 4.12.0 |
| Microsoft.CodeAnalysis.CSharp (oracle.cs) | `spikes/binding-generators/clangsharp/oracle.cs` `#:package` directive | 4.12.0 |

The postprocess project and the oracle script share a Roslyn version because they parse the same generated C# corpus and must agree on syntax-tree shape. Drift between them surfaces as oracle false positives or postprocess crashes on syntax the other tool accepted.

### 2.1 ClangSharp tool

The orchestrator (`spikes/binding-generators/clangsharp/generate_bindings.py`) invokes `ClangSharpPInvokeGenerator` via the spike-local .NET tool manifest. The tool ships its own bundled `libclang.runtime.*` packages as transitive dependencies; that is the libclang version the parser actually loads, regardless of any top-level `libclang.runtime.*` pins elsewhere in the repo.

Verification at the entry of `generate_bindings.py --execute`: confirm the manifest resolves to the pinned ClangSharp major version via `dotnet tool list --tool-path spikes/binding-generators` before issuing generate commands. The orchestrator does not currently fail-closed on mismatch; the maintainer is the gate.

### 2.2 libclang.runtime.* (transitive)

ClangSharp 17.x ships `libclang.runtime.*` 20.x as a transitive dependency. The new playbook does not pin `libclang.runtime.*` directly; the ClangSharp tool pin is the single source of truth. This differs from the sunset Cake-hosted toolchain, which pinned the `CppAst` + `libclang.runtime.*` + `libClangSharp.runtime.*` trio independently in `Directory.Packages.props`.

When investigating a parse regression after a bump, the resolved libclang version is the relevant evidence: run `dotnet tool list` against the spike manifest to see the ClangSharp version, then check the ClangSharp release notes for which `libclang.runtime.*` major it bundles.

### 2.3 Microsoft.CodeAnalysis.CSharp (postprocess)

The Roslyn postprocess project carries a `VersionOverride` on `Microsoft.CodeAnalysis.CSharp` to keep the spike's Roslyn dependency isolated from production Central Package Management (CPM). The current pin is `4.12.0`.

The override is intentional: production code does not consume Microsoft.CodeAnalysis.CSharp, and the spike's postprocess version should not leak into root `Directory.Packages.props`. The carve-out is annotated with a `slopwatch-ignore` comment so the anti-slop gate does not flag the CPM bypass.

### 2.4 Microsoft.CodeAnalysis.CSharp (oracle.cs)

The evidence reporter (`spikes/binding-generators/clangsharp/oracle.cs`) is a .NET 10 file-based script that pulls Microsoft.CodeAnalysis.CSharp via a `#:package` directive at the top of the file. The directive must match the postprocess project's `VersionOverride` to keep both tools on the same Roslyn syntax-tree shape.

Verification: `grep -n '#:package' spikes/binding-generators/clangsharp/oracle.cs` must return a version that equals the postprocess `VersionOverride`. Mismatch produces oracle false positives when the postprocess emits syntax the oracle's older Roslyn rejects (or vice versa).

### 2.5 Bump Procedure

The quartet bumps as a coordinated maintenance slice. Procedure:

1. **Identify the trigger.** Which component is driving the bump? (security advisory on libclang, new ClangSharp option needed for a satellite family, Roslyn API change required by a new rewriter, etc.)
2. **Update the leading component:**
   - ClangSharp tool: edit `spikes/binding-generators/.config/dotnet-tools.json` `tools.clangsharppinvokegenerator.version`.
   - Microsoft.CodeAnalysis.CSharp: edit both the postprocess csproj `VersionOverride` and the `oracle.cs` `#:package` directive together.
3. **Restore tools and packages:**

   ```pwsh
   dotnet tool restore --tool-manifest spikes/binding-generators/.config/dotnet-tools.json
   dotnet restore spikes/binding-generators/clangsharp/postprocess/Janset.SDL2.PostProcess.csproj --force-evaluate
   ```

4. **Run postprocess self-tests** (each rewriter has them; see §"Postprocess Pipeline Maintenance"):

   ```pwsh
   dotnet run --project spikes/binding-generators/clangsharp/postprocess/Janset.SDL2.PostProcess.csproj -c Release -- --self-test
   ```

5. **Run oracle self-tests:**

   ```pwsh
   dotnet run --file spikes/binding-generators/clangsharp/oracle.cs -- --self-test
   ```

6. **Regenerate the full family corpus and diff:**

   ```pwsh
   python spikes/binding-generators/clangsharp/generate_bindings.py --family all --execute --vcpkg-triplet x64-windows-hybrid --use-platform-header-shims
   git --no-pager diff --ignore-cr-at-eol -- 'src/Janset.SDL2.*/Generated/**/*.g.cs'
   ```

7. **Run oracle and compare** the before/after `oracle-evidence-clangsharp.md`:

   ```pwsh
   dotnet run --file spikes/binding-generators/clangsharp/oracle.cs -- --family sdl2-core --family sdl2-image --family sdl2-ttf --family sdl2-mixer --family sdl2-gfx --write-report
   ```

8. **Run AbiTests** (see §"Drift Detection Between Versions" for expected pass counts) and **Slopwatch** (zero issues required).
9. **Update this playbook table** if the pin changed.
10. **Update ADR-004** if the bump crossed a major version boundary on any component.

### 2.6 Post-Layer-1 Quartet Revalidation

After Layer 1 is stable and at every Layer-boundary milestone (Layer 2 closure, Layer 3 closure, Production Flip), attempt to bump the quartet to the latest compatible NuGet versions as a deliberate maintenance slice. The discipline above applies; the goal is to keep production-flip on an actively-maintained toolchain line rather than carry a long-pinned version into the public release.

## 3. Configuration Surface Map

`spikes/binding-generators/clangsharp/config/family-config.json` is the single source of truth for the orchestrator's per-family generation contract. Every editable field is documented below; the audit cadence is in §"Adding A New SDL Satellite Family" and §"Version-Bump Procedures".

### 3.1 Orchestrator Invocation Form

The orchestrator argv shape is verbatim — do not improvise flags. The Iteration-2 form is:

```pwsh
python spikes/binding-generators/clangsharp/generate_bindings.py --family <id> --execute --vcpkg-triplet <RID-triplet> --use-platform-header-shims
```

- `--family <id>` — one of `core`, `image`, `ttf`, `mixer`, `gfx`, or `all`.
- `--execute` — required to actually run ClangSharp; the script is dry-run by default and prints commands only.
- `--vcpkg-triplet <RID-triplet>` — e.g. `x64-windows-hybrid` for local Windows iteration, `x64-linux-hybrid` for Linux-canonical generation (see §"Linux-Canonical Generation").
- `--use-platform-header-shims` — Windows-local spike aid only; do not treat shim-enabled output as final production evidence. See §"Linux-Canonical Generation".

### 3.2 `global.*` Fields

- `global.platform_views[]` — seven SDL2 platform views (`WindowsDesktop`, `WinRT`, `GDK`, `Linux`, `MacOS`, `IOS`, `Android`). Each view carries a `name`, `supported_os` token (drives `[SupportedOSPlatform]` attribution), and `defines[]` (the macros the postprocess `platform-delta` rewriter recognises as the platform's identity). Add a view only when public declarations or platform attribution change — see §"Adding A New SDL Satellite Family" platform-macro audit and the 6-step procedure adapted below.
- `global.all_platform_macros[]` — the 28-macro cross-contamination undefine set. Every macro that any platform view defines must appear here; postprocess uses this list to undefine the wrong platforms' macros when generating the per-view passes. SDL2 currently covers seven platforms but lists 28 macros because each platform usually defines multiple identity macros (e.g. `_WIN32` + `WIN32` + `__WIN32__` + `__WINDOWS__` + `SDL_VIDEO_DRIVER_WINDOWS` for Windows desktop).
- `global.base_rsp` — path to the cross-cutting RSP file (`rsp/base.rsp`). See §"RSP File Maintenance".

#### Platform Macro Catalog Audit (Adapted From Sunset)

Run this audit on every SDL2 minor or patch release that touches the platform headers, and at every quartet revalidation:

1. Inspect SDL2 platform-related headers: `SDL_platform.h`, `SDL_config.h`, `SDL_stdinc.h`, `SDL_system.h`, `SDL_main.h`, `SDL_syswm.h`.
2. Search for SDL2-style platform/backend conditionals: `_WIN32`, `__APPLE__`, `__MACOSX__`, `__IPHONEOS__`, `__ANDROID__`, `linux`, `__linux__`, `SDL_VIDEO_DRIVER_*`.
3. Compare found macros against `global.all_platform_macros`.
4. Add or remove `platform_views[]` entries **only** when public declarations or platform attribution change. New macros that gate driver internals (e.g. a hypothetical `SDL_VIDEO_DRIVER_FOO`) do not require a new view if no public declaration is gated on them.
5. Regenerate and diff per §"Drift Detection Between Versions".
6. Do **not** copy the SDL2 macro list into SDL3 when SDL3 planning starts — SDL3 has a different platform macro contract (`SDL_PLATFORM_*`) and gets its own catalog.

### 3.3 `families.<family>.*` Identity Fields

Every family entry carries identity fields that the orchestrator and postprocess both consume:

- `namespace` — the C# namespace root (e.g. `SDL2`, `SDL2.Image`, `SDL2.Gfx`).
- `library_version` — upstream library version pinned to the vcpkg port (e.g. `2.32.10` for SDL2 core, `2.8.8` for SDL2_image). Updated on every SDL version bump per §"Version-Bump Procedures".
- `raw_class` — the internal raw ABI container class name (e.g. `SDLNative`, `SDL_imageNative`).
- `include_subdir` — vcpkg include subdirectory (currently `SDL2` for all SDL2 families).
- `library_name` — DllImport library identifier (e.g. `SDL2`, `SDL2_image`, `SDL2_gfx`).
- `rsp` — path to the family RSP file (e.g. `rsp/sdl2-core.rsp`). See §"RSP File Maintenance".
- `project_dir` — managed project directory under `src/` (e.g. `Janset.SDL2.Core`).
- `owner_mode` — boolean controlling Pattern B opaque handle emit:
  - `owner_mode: true` — the family owns its handles and emits them into its own namespace (Core, TTF, Mixer).
  - `owner_mode: false` — the family consumes handles from another family (Image consumes Core handles; GFX has no local handles).
- `headers[]` — ordered list of `{ "order": <int>, "name": "<header>.h" }` entries. **Headers must be in dependency order**: SDL_assert before SDL_atomic before SDL_audio, etc. ClangSharp emits per-header, and the order influences which header earns which declaration in the platform-delta cleanup pass.
- `platform_sensitive_headers[]` — headers that contain platform-conditional declarations (e.g. `SDL_main.h`, `SDL_system.h` for Core). These trigger the per-view passes that `platform-delta` consolidates.
- `required_surface` — `{ "functions": [...], "constants": [...] }` for declarations the per-header parse loop cannot reach (e.g. `SDL_INIT_*` constants and the umbrella `SDL.h` base functions). Hand-curated; document additions in the per-family RSP rationale. `null` for satellites with no umbrella-exclusion gap.

### 3.4 `families.<family>.opaque_handles`

Pattern B opaque handle policy is configured per family with a **3-source triangulation audit method**:

1. **SDL release headers** (`vcpkg_installed/<triplet>/include/SDL2/`) — definitive source for opaque struct typedefs and tag/typedef shape.
2. **wiki.libsdl.org** cross-validation — wiki page evidence on whether the type is intentionally opaque (recorded in `wiki_evidence` field).
3. **ClangSharp Modern output empty-struct verification** — after generation, the typedef appears as an empty `partial struct X { }` declaration in the generated output. If ClangSharp emits the struct with fields, it is not opaque.

Subfields:

- `last_audited` — ISO date of the most recent 3-source triangulation. Updated on every SDL version bump.
- `source` — short description of the triangulation evidence used.
- `auto_detect_well_known[]` — typedefs the `uniform-opaque` rewriter discovers automatically. Each entry carries `name`, `header`, `header_decl`, `wiki_url`, `wiki_evidence`, and optional `header_note` (used when struct tag and typedef name diverge, e.g. `SDL_semaphore` tag vs `SDL_sem` typedef).
- `force_opaque_exceptions[]` — typedefs that ClangSharp would emit with full body but policy quarantines as opaque (e.g. `SDL_RWops` because its function-pointer subclass layout is unsafe; `SDL_SysWMinfo` / `SDL_SysWMmsg` because their platform-conditional unions vary by build host). Each entry carries `name`, `header`, a free-form `reason` paragraph, and wiki cross-validation.
- `excluded_candidates[]` — typedefs that look opaque-shaped but are not Pattern B handles. Documents the rationale to prevent a future audit from re-classifying them. Examples: `SDL_BlitMap` (field-scope only, remap to nint), `SDL_GLContext` / `SDL_MetalView` (pointer typedefs, not struct handles), `SDL_TimerID` / `SDL_TLSID` / `SDL_*ID` (value-type integer aliases, not handles).

### 3.5 `families.<family>.flags_enums`

- `last_audited` — ISO date of the most recent flags-allow-list audit. Updated on every SDL version bump.
- `allow_list[]` — enums the `flags-detect` rewriter must mark with `[System.Flags]` despite **not** matching the name-suffix `EndsWith("Flags")` rule. Each entry carries `name`, `header`, and a `reason` paragraph explaining the bitmask nature (e.g. `SDL_Keymod` composes left/right modifier pairs into `KMOD_CTRL`).

### 3.6 `families.<family>.clong_methods`

List of function names whose raw ABI signatures use C `long` / `unsigned long`. The `clong-dispatch` postprocess targets these by name to emit the hybrid Compat (managed wrapper + RuntimeInformation.IsOSPlatform dispatch) vs Modern (`[LibraryImport]` + `CLong`/`CULong`) shape.

The audit cadence is per family: a new clong-using function appears only when SDL adds a function that takes `long` / `unsigned long` parameters (rare for SDL2 in maintenance mode; possible for satellites that use `long` for index or face counts — see TTF's `TTF_OpenFontIndex` family).

### 3.7 Audit Cadence Summary

- `opaque_handles.last_audited` — updated on every SDL version bump (§"Version-Bump Procedures" 6.1 step 7).
- `flags_enums.last_audited` — updated on every SDL version bump (same step).
- Header inventory `headers[]` array — re-confirmed via `ls vcpkg_installed/<triplet>/include/SDL2/` and diffed against the previous pin's listing on every SDL bump.
- `clong_methods` — re-confirmed via grep over the family's headers for `long ` parameters/returns.

When auditing local checkouts, `vcpkg_installed/` is gitignored; use `rg --no-ignore` (or equivalent) so the directory is not silently skipped.

> **Fail-by-default discipline.** Unused or stale config entries (e.g., a `required_surface` function name that no longer appears in the generated output, an `opaque_handles.auto_detect_well_known` entry whose tag no longer matches an empty partial struct in any generated `.g.cs`) MUST surface as an error or warning during regeneration, not silently no-op. The ClangSharp + Roslyn pipeline enforces this via oracle.cs reports (`Evidence Missing` classification) + drift watchdog warnings during postprocess. The sunset playbook's `macro_constants.excluded` + `macro_constants.overrides` + `allow_stale` rules are replaced by oracle-time + postprocess-time enforcement; the discipline survives the toolchain transition.

## 4. RSP File Maintenance

ClangSharp accepts response files via `@<path>` argv tokens. The spike organises RSP files in a **three-tier hierarchy**:

| Tier | Path | Scope | Examples |
| --- | --- | --- | --- |
| Base | `rsp/base.rsp` | Cross-cutting toolchain options applied to every family/header | SDL_DECLSPEC suppression, 9 GCC-intrinsic-disable defines, scalar-type remaps (Sint8/Uint8/.../Sint64/Uint64 → C# primitives), `wchar_t *` literal-space remap, `-fdeclspec`, `--undefine-macro=__has_builtin` |
| Family | `rsp/sdl2-<family>.rsp` | Family-scope exclusions and remaps | sdl2-core: `SDL_PixelFormatEnum=uint` typed enum; `SDL_RWFromFP`/`SDL_GetWindowWMInfo` exclusions; SDL_assert / SDL_stdinc / SDL_endian / SDL_bits inline-helper exclusions |
| Per-header | `rsp/per-header/<header>.rsp` | Per-header overrides | `SDL_stdinc.rsp`: `_SDL_iconv_t=SDL_iconv_t` tag/typedef canonicalization; BCL-replaceable helper family exclusions (`SDL_lround`, `SDL_iconv_*`) |

### 4.1 When To Add An RSP Entry

Add an entry only when one of the following applies:

- **Type-remap** — a foreign type or platform-sensitive type needs `--remap` to map to a C# representation. Example: `wchar_t *=nint` in `base.rsp` because the parser sees `wchar_t` from system headers under Windows compiles.
- **Exclude** — a symbol must not emit because (a) it is an inline helper with no public binding meaning (`SDL_Swap16`), (b) it is a varargs deferral (`SDL_LogMessageV`), (c) it is a BCL-replaceable helper per Constitution §"BCL-Replaceable Helper Exclusion Policy" (`SDL_lround`, `SDL_iconv_*`), or (d) it is compiler-magic (`__FUNCTION__`, `SDL_TriggerBreakpoint`).
- **Define-macro** — scope macros or feature-test macros need recognition. Example: `SDL_DISABLE_IMMINTRIN_H=1` in `base.rsp` short-circuits SDL's intrinsic-header pulls.
- **`--with-type`** — a parser-time typed-enum override is needed. Example: `SDL_PixelFormatEnum=uint` in `sdl2-core.rsp` keeps the enum tag distinct from the `SDL_PixelFormat` struct in SDL2 (collapsed in SDL3 — ppy's blanket `SDL_PixelFormat=uint` would break SDL2).

### 4.2 Type-Remap Discipline

The `--remap` syntax matches libclang byte-exact. Literal-space matters: `wchar_t *=nint` includes the space before `*` because that is the exact spelling libclang emits in the type token. `const wchar_t *=nint` is a separate entry; do not collapse it.

- One remap per RSP line.
- Unquoted; no commas; libclang token = C# replacement.
- Spaces between tokens are part of the libclang token shape.

### 4.3 Additive-Only Semantics

ClangSharp rejects duplicate keys across the RSP chain. A per-header RSP entry **cannot override** a family RSP entry on the same key — ClangSharp errors with a duplicate-key diagnostic. The discipline:

- Use the most general tier that captures the rule. A type-remap that applies to all families goes in `base.rsp`; a family-scope exclusion goes in `sdl2-<family>.rsp`; a single-header carve-out goes in `per-header/<header>.rsp`.
- When a per-header rule conflicts with a higher tier, restructure the higher tier rather than try to override.

### 4.4 No-SDLCALL Cdecl Verification (Phase 0.5 Item-4 Fold)

When a header has functions that lack the `SDLCALL` annotation, do **not** preemptively add a per-header RSP. ClangSharp's default emit is already `[DllImport(..., CallingConvention = CallingConvention.Cdecl)]` for the Compat backend and `[LibraryImport]` + `[UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]` for the Modern backend, which matches SDL's de facto `cdecl` ABI on every supported platform.

Verification procedure when investigating a no-SDLCALL header:

1. Regenerate the family that owns the header.
2. Grep the Compat output for the function name: `grep -n '<FunctionName>' src/Janset.SDL2.<Family>/Generated/Compat/<Header>.g.cs`.
3. Confirm the emit carries `Cdecl` (in the `CallingConvention` argument of `[DllImport]` or via `[UnmanagedCallConv]` on the partial wrapper).
4. If the emit is correct, no RSP entry is needed. Add a `# Verified Cdecl emit without RSP override (<date>)` comment to the family RSP if future audits would otherwise re-investigate.

This discipline keeps the RSP files minimal and prevents accumulating per-header workarounds that turn out to be no-ops.

### 4.5 Parser Options Audit Cadence (Adapted From Sunset)

The `base.rsp` entries carry workarounds for SDL header / libclang interaction bugs. Each entry is a documented mechanism, and adding without justification or keeping after the underlying SDL behaviour changes both create drift.

Run this audit at every quartet bump (§"Toolchain Version Pinning" 2.5) and at every SDL2 minor release:

1. **Re-read each entry's rationale** (the comments above the entry block in `base.rsp` and the corresponding sunset-rationale snapshot below). If a rationale references SDL header line numbers (e.g. `SDL_cpuinfo.h:118-133`), verify those lines still match the current SDL2 pin.
2. **Try removing one entry at a time, regenerate, observe.** If the parse succeeds without the entry, retire it (delete from RSP + this playbook). If it fails with the symptom the rationale documents, keep it.
3. **Compare against peer RSP files.** ppy/SDL3-CS's `SDL3-CS/SDL3/*.rsp` files document equivalent workarounds for SDL3. Cross-check whether SDL2's stable equivalents still apply or have been superseded by upstream SDL changes.

Bare-flag additions (e.g. `-fms-extensions` without rationale) are not acceptable.

### 4.6 sdl2-core Rationale Snapshot

The `base.rsp` workarounds verified at SDL2 2.32.10 + ClangSharp 17.0.1 + libclang 20.x:

- **`SDL_DECLSPEC=`** — Suppresses platform-specific export attributes (`__declspec(dllexport)` on Windows, `__attribute__((visibility("default")))` on Linux) so ClangSharp sees plain function declarations.
- **`SDL_DISABLE_IMMINTRIN_H=1`, `SDL_DISABLE_MMINTRIN_H=1`, `SDL_DISABLE_XMMINTRIN_H=1`, `SDL_DISABLE_EMMINTRIN_H=1`, `SDL_DISABLE_PMMINTRIN_H=1`, `SDL_DISABLE_MM3DNOW_H=1`, `SDL_DISABLE_LSX_H=1`, `SDL_DISABLE_LASX_H=1`, `SDL_DISABLE_ARM_NEON_H=1`** — Short-circuit `SDL_cpuinfo.h:118-133`'s intrinsic-header includes. Without these defines, `SDL_cpuinfo.h` pulls GCC's intrinsic headers, whose `extern __inline` declarations of `_mm_pause` / `_mm_getcsr` / `__rdtsc` / `_mm_clflush` / `_mm_{l,m,s}fence` collide with libclang's internal builtin-function table and break the parse with `error: definition of builtin function`. The disable macros are the documented SDL2 escape hatch for binding generators. Peer evidence: amerkoleci's Alimer.Bindings.SDL uses the SDL3 equivalents (`SDL_PLATFORM_*`) for the same purpose.
- **`-fdeclspec`** (via `--additional`) — Enables `__declspec` parsing in C mode. `libegl-dev`'s `/usr/include/EGL/egl.h` declares functions with `EGLAPI` which expands to `__declspec(dllimport/export)` on a Windows target, and SDL_egl.h is pulled by SDL_video.h under `SDL_VIDEO_DRIVER_WINDOWS`. Without `-fdeclspec`, clang's default C mode rejects every EGL function declaration in Windows-flavoured views. Narrower than `-fms-extensions` (which enables the full MS dialect).
- **`--undefine-macro=__has_builtin`** (via `--additional`) — Forces `SDL_stdinc.h:127-131` `#ifdef __has_builtin` to false, which makes `_SDL_HAS_BUILTIN(x)` always expand to 0. That short-circuits the `#if _SDL_HAS_BUILTIN(__builtin_{mul,add}_overflow)` blocks at SDL_stdinc.h:822 and 853 so the `_SDL_size_mul_overflow_builtin` and `_SDL_size_add_overflow_builtin` `SDL_FORCE_INLINE` helpers never enter the AST. Peer reference: ppy/SDL3-CS `SDL_stdinc.rsp` uses the exact same flag.

Side-effect audit (verified 2026-05-28): three SDL2 sites reference `_SDL_HAS_BUILTIN` beyond the two declaration gates above — `SDL_assert.h:54` (selects `__builtin_debugtrap` for `SDL_TriggerBreakpoint` impl — macro body, no declaration impact), `SDL_endian.h:134/136/138` (selects `__builtin_bswap{16,32,64}` for `SDL_Swap*` `SDL_FORCE_INLINE` bodies — body content, helpers are already inline-filtered). No public-API declaration is affected by undefining the macro.

### 4.7 SDL2 Drift Posture

SDL2 has been in upstream maintenance mode since 2.28.0 (June 2023): bug-fix releases only, no new public-API additions. The hand-curated `required_surface.constants` (`SDL_INIT_*` set) has not changed since SDL 2.0.0 (2013). Drift risk for this list is essentially zero for SDL2's maintenance lifetime; the per-bump diff-review checklist (§"Version-Bump Procedures" 6.1) catches the rare addition.

## 5. Postprocess Pipeline Maintenance

The Roslyn postprocess CLI dispatches seven rewriters in a fixed order. The order is load-bearing: later rewriters depend on earlier rewriters having normalised the syntax tree. Source of truth: `spikes/binding-generators/clangsharp/postprocess/Program.cs` dispatch switch.

| Step | Mode | Scope | Purpose |
| --- | --- | --- | --- |
| 1 | `platform-delta` | Compat + Modern | Removes per-view declarations already emitted in neutral/earlier views; adds `[SupportedOSPlatform]` attribution by platform view. |
| 2 | `strip-varargs` | Compat + Modern | Drops `__arglist` parameters from variadic P/Invokes (fmt-only policy per Constitution §"C Variadics"). Applied before `libraryimport` so the Modern emit sees clean signatures. |
| 3 | `libraryimport` | Modern only | Promotes `[DllImport]` to `[LibraryImport]` + `[UnmanagedCallConv]` + `partial`. Compat keeps `DllImport` for legacy TFMs (netstandard2.0, net462). |
| 4 | `flags-detect` | Compat + Modern | Adds `[System.Flags]` to enum declarations matching the `EndsWith("Flags")` suffix rule or the `family-config.json` `families.<family>.flags_enums.allow_list`. Value-pattern blind by design; `SDL_bool` must not qualify. |
| 5 | `guid-substitute` | Compat + Modern | Removes the generated `partial struct SDL_GUID` and rewrites every reference to `System.Guid`. Wire size is bit-identical (both 16 bytes); see `GuidSubstitutionRewriter` for the ABI trade-off rationale. |
| 6 | `clong-dispatch` | Compat + Modern (mode auto-detected) | Roslyn node-level emit for C `long` / `unsigned long` raw ABI signatures. Targets the `family-config.json` `families.<family>.clong_methods` list. Modern emits `[LibraryImport]` + `CLong`/`CULong`; Compat emits a managed wrapper + `RuntimeInformation.IsOSPlatform` dispatch to per-RID `[DllImport]` helpers (`uint` on Windows, `nint` on Unix64). |
| 7 | `uniform-opaque` | Compat + Modern | Pattern B opaque handle emit. Two channels: (1) auto-detect — empty `partial struct X { }` declarations referenced via `[NativeTypeName("X *")]` elsewhere; (2) force-opaque — Constitution-bound allow-list from `family-config.json` `opaque_handles.force_opaque_exceptions`. Both channels emit the same Pattern B shape (readonly partial struct wrapping `nint` with `IEquatable<T>`, explicit operators only) and rewrite single-pointer `X*` references in raw ABI signatures to by-value `X`. |

### 5.1 Compat Vs Modern Mode Boundary

`libraryimport` is the only Modern-only step. Every other step processes both backends. Mode detection inside the postprocess is auto from the input directory path:

- Path contains `Compat/` segment → Compat mode.
- Path contains `Modern/` segment → Modern mode.

The `clong-dispatch` rewriter changes shape across modes (managed-wrapper-plus-dispatch vs `LibraryImport` + `CLong`/`CULong`); the others apply the same transformation in both modes.

### 5.2 When To Add A New Rewriter

Add a new postprocess step only when:

1. **A new policy mechanism is needed** that cannot be expressed via `family-config.json` schema or RSP directives. Example: a new ABI-shape rewrite that depends on cross-file analysis ClangSharp does not provide.
2. **The rewriter is scope-bounded** — one concept per rewriter. Resist the temptation to bundle multiple unrelated transformations into a single mode; the seven-step shape is intentional.
3. **Self-tests cover the new rewriter** before it ships. See §"Self-Test Discipline" below.
4. **Pipeline placement is documented** — where in the seven-step order does it run, and why? Document any cross-step dependency.

### 5.3 Self-Test Discipline

Each rewriter has self-tests in the postprocess project. The full set runs via:

```pwsh
dotnet run --project spikes/binding-generators/clangsharp/postprocess/Janset.SDL2.PostProcess.csproj -c Release -- --self-test
```

Run self-tests after:

- Any quartet bump (§"Toolchain Version Pinning" 2.5).
- Any rewriter source-code change.
- Any `family-config.json` schema field addition that a rewriter consumes (the rewriter must read the new field and the self-test must cover the read path).

## 6. Version-Bump Procedures

Four procedures, each as a numbered checklist. The drift-detection gate (§"Drift Detection Between Versions") applies to every bump.

> **Consolidated Maintenance Checklist.** The sunset playbook's flat 13-item checklist is decomposed in this playbook into procedure-specific checklists: §6.1 (SDL2 Version Bump, 12 steps) + §6.2 (ClangSharp Tool Version Bump) + §6.3 (Microsoft.CodeAnalysis Postprocess Version Bump) + §6.4 (vcpkg Triplet Bump, 5 binding-specific checks) + §7 (Adding A New SDL Satellite Family, 14 steps) + §8 (Drift Detection, 5 stages). Together these subsume the sunset's flat list with finer per-trigger scoping.
>
> **Note on `.generated-stamp` and reproducible-state.** The `.generated-stamp` discipline (per-family stamp recording generator version + vcpkg state + SDL library version + header-set fingerprint, with no wall-clock fields) is part of the Production Flip layer (see roadmap §"Production Flip — SDL2.Core Reproducibility") and lives in the Cake host data layer per ADR-003. It is NOT part of the current spike-host ClangSharp orchestrator surface; it re-engages at Production Flip when generated source moves from `spikes/binding-generators/clangsharp/src/Janset.SDL2.<Family>/Generated/` to `src/Janset.SDL2.<Family>/Generated/`.

### 6.1 SDL2 Version Bump (e.g. 2.32.10 → 2.x.y)

1. **Update the vcpkg port** (`vcpkg.json` baseline + overlay if needed). See `docs/playbook/vcpkg-update.md` for the vcpkg-side procedure.
2. **Run `tools.cs setup`** to install the new vcpkg port and refresh `vcpkg_installed/<triplet>/include/SDL2/`.
3. **Confirm header inventory** with `ls vcpkg_installed/x64-windows-hybrid/include/SDL2/` — diff against the previous pin's listing. New `.h` files that look like real binding surface (have `extern DECLSPEC` declarations) need `headers[]` entries in `family-config.json` (and dependency-order placement); new convenience wrappers or sub-headers (no `extern DECLSPEC`) stay omitted.
4. **Update `family-config.json`** — bump `families.core.library_version` to the new SDL2 version. **Do not modify satellite `library_version` fields unless the satellite vcpkg port also bumped.** SDL2 satellites (Image, Mixer, TTF, GFX) version independently per upstream release schedule. For an SDL2 *core* bump, only `families.core.library_version` updates. For an SDL2 *satellite* bump, only that satellite's `library_version` updates. A coordinated multi-family bump (e.g., SDL2 core + a satellite both bumped in the same vcpkg baseline update) requires per-family `library_version` edits matching each vcpkg port version.
5. **Regenerate the full family corpus:**

   ```pwsh
   python spikes/binding-generators/clangsharp/generate_bindings.py --family all --execute --vcpkg-triplet x64-windows-hybrid --use-platform-header-shims
   ```

6. **Diff the generated output** for drift review:

   ```pwsh
   git --no-pager diff --ignore-cr-at-eol -- 'src/Janset.SDL2.*/Generated/**/*.g.cs'
   ```

7. **Run oracle and compare** the before/after `oracle-evidence-clangsharp.md`:

   ```pwsh
   dotnet run --file spikes/binding-generators/clangsharp/oracle.cs -- --family sdl2-core --family sdl2-image --family sdl2-ttf --family sdl2-mixer --family sdl2-gfx --write-report
   git --no-pager diff -- spikes/binding-generators/output/reports/oracle-evidence-clangsharp.md
   ```

   **Sub-step 7.5 — Handle oracle deltas.** If the oracle diff shows new findings, branch by category:

   - **New opaque-shaped type** (empty partial struct + pointer use): run 3-source triangulation (release headers + wiki + ClangSharp Modern output empty-struct verification). If durable opaque, add to `families.<family>.opaque_handles.auto_detect_well_known[]`. If platform-conditioned union (like `SDL_RWops`), add to `force_opaque_exceptions[]` with rationale. Re-run regeneration + oracle to confirm closure.
   - **New C `long`-using function** (raw signature contains `long` / `unsigned long`): see step 9 below for `clong_methods` vs BCL-exclusion classification + the new-function branching sub-steps.
   - **New `[Flags]`-candidate enum** (`*Flags` suffix or composite-alias values): verify via Constitution §"Enums" auto-decoration policy. Suffix-matching enums need no config change. Add to `families.<family>.flags_enums.allow_list[]` only if composite-alias values present without the `Flags` suffix.
   - **New `wchar_t*` use**: verify covered by `base.rsp` `wchar_t *=nint` literal-space `--remap` pattern. If a new path escapes the type-system route, see Constitution §"wchar_t" + implementation-notes §5 mechanism for the Roslyn fallback rewriter.
   - **New foreign-type reference** (Vulkan / Direct3D / GDK / Win32 / etc.): add per-header RSP `--remap` per Constitution §"Foreign Type Boundary Policy" + implementation-notes §7 disposition table.
   - **Reduced findings** (closure): verify the closed finding was a known accepted deferral; update `oracle-evidence-clangsharp.md` regeneration timestamp; no further action.

   **Iteration loop:** for each new finding category, the loop is: classify → edit `family-config.json` or RSP → re-run regeneration (step 5) → re-run oracle (step 7) → loop until clean.

8. **Update audit-cadence dates** in `family-config.json`:
   - `families.<family>.opaque_handles.last_audited` — set to today (after running 3-source triangulation against the new headers; see §"Configuration Surface Map" 3.4).
   - `families.<family>.flags_enums.last_audited` — set to today (after re-confirming the allow-list against the new headers).

   **Per-family scope:** update `last_audited` dates only for families whose headers actually changed in this bump. For an SDL2 *core* bump: update `families.core.opaque_handles.last_audited` + `families.core.flags_enums.last_audited`. For a *satellite* bump: update only that satellite's audit dates. For a *coordinated multi-family* bump: update each affected family. The 3-source triangulation method (release headers + wiki + ClangSharp Modern output) re-runs on the families whose headers changed; untouched families keep their prior `last_audited` value.
9. **Audit `clong_methods`** — grep the new headers for `long ` parameters/returns using a word-boundary pattern that excludes `long long`, `unsigned long` (already handled separately by raw-token capture), and `long_var`-style identifiers:

   ```pwsh
   # Word-boundary grep targeting real C long parameters/returns (not 'long long', 'unsigned long', or 'long_var'):
   Grep -nP '\blong\s+\w+\s*[,)]|\blong\s+\w+\s*[*]' vcpkg_installed/x64-windows-hybrid/include/SDL2/*.h
   # Or with semantic awareness: --include='*.h' for project-relative scoping.
   ```

   Confirm the list still covers every C `long`-using function.

   **If new C `long`-using function found:**

   1. **Classify:** is the function user-facing public API (add to `clong_methods`)? Or is it BCL-replaceable convenience helper (exclude per Constitution §"BCL-Replaceable Helper Exclusion Policy")?
   2. **If add:** append the function name to `families.<family>.clong_methods[]` in alphabetical order.
   3. **If exclude:** add to family RSP `--exclude <function_name>` with a comment citing the BCL-Replaceable Helper Exclusion Policy three-condition rule.
   4. **Re-run** regeneration (step 5) + oracle (step 7) + AbiTests (step 10) to verify the new declaration is handled correctly.
10. **Run AbiTests:**

    ```pwsh
    dotnet test spikes/binding-generators/clangsharp/tests/abi-tests/AbiTests.csproj -c Release
    ```

    Expected total pass count stays stable across the bump (see §"Drift Detection Between Versions").
11. **Run Slopwatch** with the canonical exclude set:

    ```pwsh
    slopwatch analyze --fail-on warning --exclude "artifacts/**,external/**,vcpkg_installed/**,spikes/binding-generators/references/**,**/bin/**,**/obj/**"
    ```

    Expected: `Scan complete: 0 issue(s) found`. If new files were added or removed since the last baseline, refresh the baseline with `slopwatch init -f --exclude "artifacts/**,external/**,vcpkg_installed/**,spikes/binding-generators/references/**,**/bin/**,**/obj/**"` per AGENTS.md §"Slopwatch" guidance.
12. **Commit** per AGENTS.md §"Approval Gate".

### 6.2 ClangSharp Tool Version Bump

1. **Edit `spikes/binding-generators/.config/dotnet-tools.json`** `tools.clangsharppinvokegenerator.version` to the new version.
2. **Restore tools:** `dotnet tool restore --tool-manifest spikes/binding-generators/.config/dotnet-tools.json`.
3. **Read ClangSharp release notes** for new options, behaviour changes, or bundled libclang.runtime.* major changes. Document any RSP-impacting changes in the corresponding RSP file's header comment.
4. **Regenerate the full family corpus** (same command as §6.1 step 5).
5. **Diff for output drift** (same command as §6.1 step 6). New ClangSharp emit behaviour usually surfaces as changed syntax shape, not new declarations — review carefully.
6. **Run oracle, AbiTests, Slopwatch** (steps 7, 10, 11 of §6.1).
7. **Update the toolchain pin table** in §"Toolchain Version Pinning".
8. **Commit** per AGENTS.md §"Approval Gate".

### 6.3 Microsoft.CodeAnalysis Postprocess Version Bump

1. **Edit `spikes/binding-generators/clangsharp/postprocess/Janset.SDL2.PostProcess.csproj`** `<PackageReference Include="Microsoft.CodeAnalysis.CSharp" VersionOverride="..." />` to the new version.
2. **Edit `spikes/binding-generators/clangsharp/oracle.cs`** `#:package Microsoft.CodeAnalysis.CSharp@...` directive to the same version (they must match — see §"Toolchain Version Pinning" 2.4).
3. **Build the postprocess project:** `dotnet build spikes/binding-generators/clangsharp/postprocess/Janset.SDL2.PostProcess.csproj -c Release`.
4. **Run postprocess self-tests:**

   ```pwsh
   dotnet run --project spikes/binding-generators/clangsharp/postprocess/Janset.SDL2.PostProcess.csproj -c Release -- --self-test
   ```

5. **Run oracle self-tests:** `dotnet run --file spikes/binding-generators/clangsharp/oracle.cs -- --self-test`.
6. **Regenerate the full family corpus** (same command as §6.1 step 5).
7. **Diff for output drift** (same command as §6.1 step 6). Roslyn API changes can surface as different formatting/whitespace in the rewriter output; review with `--ignore-cr-at-eol`.
8. **Run AbiTests, Slopwatch** (steps 10, 11 of §6.1).
9. **Update the toolchain pin table** in §"Toolchain Version Pinning".
10. **Commit** per AGENTS.md §"Approval Gate".

### 6.4 Vcpkg Triplet Bump

Triplet changes alter compiler flags, symbol visibility, and the hybrid-static encoding. Binding maintenance is **coupled to native build maintenance**; the 5 binding-specific checks (adapted from the sunset playbook §"Hybrid-Static / Overlay Coupling"):

1. **If an overlay port changes SDL feature flags** → confirm affected public headers and exported symbols. New features can add `extern DECLSPEC` declarations under a guard macro.
2. **If a hybrid triplet changes compiler flags** → confirm symbol visibility assumptions still hold. The Compat backend depends on `[DllImport(SDL2)]` resolving against the same exported symbol set on every triplet.
3. **If Linux / macOS visibility flags or version scripts change** → check whether generated entry points still match exported symbols. The Pack-stage `BindingSymbolExistenceValidator` catches mismatches.
4. **If a satellite starts or stops exposing a function due to build options** → the generated binding surface and future symbol-existence validation must reflect that. Regenerate the affected satellite and diff.
5. **If a vcpkg baseline changes upstream port patches without changing SDL upstream version** → still regenerate and validate per §6.1; header patches can change ABI-relevant declarations.

Then:

6. **Regenerate the full family corpus** for the affected triplet (vary `--vcpkg-triplet` per RID).
7. **Run cross-triplet ABI smoke** — execute AbiTests per RID via the Docker container (see §"Linux-Canonical Generation") for non-Windows RIDs.
8. **Update overlay** if needed (`vcpkg-overlay-triplets/`) and revalidate per `docs/playbook/overlay-management.md`.

### 6.5 SDL2 Vs SDL3 Maintenance Posture

SDL2 is in upstream maintenance mode; SDL3 is not.

For SDL2:

- Patch updates usually require regeneration plus diff review (the sunset playbook's posture survives the toolchain transition).
- `SDL2_gfx` is effectively frozen but still needs export/symbol validation because it is third-party.
- `SDL_syswm.h` typed unions remain a separate work item (see roadmap §"SysWM Layout And Satellite Sweep Close").

For SDL3 (when planning starts per AGENTS.md):

- Treat platform macros as a new catalog, not an extension of SDL2 — re-audit `global.platform_views` and `global.all_platform_macros` from scratch against `SDL_PLATFORM_*` conditionals.
- Expect `SDL_PLATFORM_*` usage; SDL2-style `_WIN32` / `__APPLE__` / `SDL_VIDEO_DRIVER_*` macros do not carry over.
- Re-evaluate legacy TFMs before copying SDL2's `netstandard2.0` / `net462` obligations.
- Re-evaluate bool, IO, and handle rules before emitting public API (SDL3 changed `SDL_bool` to standard C `bool`).

## 7. Adding A New SDL Satellite Family

Checklist for bringing a new SDL satellite into generation scope. Run every step; the order matters (manifest → config → RSP → csproj → audits → activation → tests).

1. **Manifest entry** — add a `package_families[]` entry to `build/manifest.json` with the family identity (managed project, native project, library reference, depends_on). The orchestrator does not read this directly, but the Cake build host / PreFlight validation will.
2. **`family-config.json` family entry** — add `families.<family>` with all required fields per §"Configuration Surface Map" 3.3:
   - `namespace`, `library_version`, `raw_class`, `include_subdir`, `library_name`, `rsp`, `project_dir`, `owner_mode`, `headers[]` (dependency-ordered), `platform_sensitive_headers[]`, `required_surface` (or `null`).
3. **`rsp/sdl2-<family>.rsp`** — family identity (no required fields by default; add family-scope exclusions as the per-family audit identifies them). Copy from a similar-shaped existing family (Image for thin satellites; Mixer/TTF for satellites with owned handles).
4. **Per-header RSP overlays** under `rsp/per-header/<header>.rsp` — only if the per-header audit (§"RSP File Maintenance" 4.1) identifies a need. Most satellite headers do not need per-header RSPs.
5. **Managed csproj** at `src/Janset.SDL2.<Family>/Janset.SDL2.<Family>.csproj`:
   - `<ProjectReference Include="..\Janset.SDL2.Core\Janset.SDL2.Core.csproj" />` (every satellite depends on Core).
   - Multi-TFM conditional `<Compile Include>` for `Generated/Compat/` (legacy TFMs) and `Generated/Modern/` (net8+).
   - Copy the multi-TFM conditional shape from an existing satellite csproj.
6. **`Support/DisableRuntimeMarshalling.cs`** — `[assembly: DisableRuntimeMarshalling]` under NET7_0_OR_GREATER gating. Required for Modern backend's `[LibraryImport]` emit shape. Copy verbatim from an existing satellite.
7. **Opaque handle audit** — apply the 3-source triangulation method (§"Configuration Surface Map" 3.4) against the new satellite's headers. Populate `opaque_handles.auto_detect_well_known`, `force_opaque_exceptions`, and `excluded_candidates`. Set `last_audited` to today.
8. **Flags enum audit** — grep the satellite's headers for bitmask-shaped enums; populate `flags_enums.allow_list` for any that don't match the `EndsWith("Flags")` suffix rule. Set `last_audited` to today.
9. **`clong_methods` audit** — grep the satellite's headers for `long ` parameters/returns; populate `clong_methods` with the function names. Examples: TTF has 5 (`TTF_OpenFontIndex` + variants + `TTF_FontFaces`).
10. **Macro classification** (Phase 0.5 Item-5 fold) — audit the satellite's macros and classify each as one of:
    - **Value-like** — `IMG_INIT_PNG`, `MIX_DEFAULT_FREQUENCY`, etc. Emit as `const` constants if reachable through the per-header parse, or via `required_surface.constants` if umbrella-only.
    - **Function-like** — `SDL_RWFromFile`, scope macros (`SDL2_GFXPRIMITIVES_SCOPE`). Function-like macros usually need exclusion or rewriting via per-header RSP — document the disposition in `excluded_candidates` with rationale (the `excluded_candidates` notes field accepts macro-classification entries alongside opaque-handle entries).
    - **Computed** — `SDL_INIT_EVERYTHING` (bitwise OR over other constants). C# constant-expression viable as `const` when operands are `const`.
11. **Oracle activation** — the oracle (`oracle.cs`) hardcodes known families. Add the new family identifier to oracle's `FamilyConfigs` / `KnownFamilies` table (or invoke per the runtime-config path if the refactor has landed).
12. **AbiTests expansion** — add per-family AbiTests under `spikes/binding-generators/clangsharp/tests/abi-tests/` exercising at minimum one representative function (typical: an init/quit pair or a handle-owning constructor/destructor).
13. **Slopwatch + multi-TFM build verification:**

    ```pwsh
    dotnet build src/Janset.SDL2.<Family>/Janset.SDL2.<Family>.csproj -c Release
    slopwatch analyze --fail-on warning --exclude "artifacts/**,external/**,vcpkg_installed/**,**/bin/**,**/obj/**,spikes/binding-generators/references/**"
    ```

14. **Linux-canonical regeneration verification** — confirm the family generates cleanly under Linux per §"Linux-Canonical Generation" (Windows-local generation with `--use-platform-header-shims` is iteration aid, not production evidence).

## 8. Drift Detection Between Versions

Drift detection runs as **3-stage defense-in-depth** on every regeneration. Each stage catches a different drift class.

### 8.1 Stage 1 — Generated Output Diff

After regeneration, diff the generated `.g.cs` corpus to detect raw output drift:

```pwsh
git --no-pager diff --ignore-cr-at-eol -- 'src/Janset.SDL2.*/Generated/**/*.g.cs'
```

The `--ignore-cr-at-eol` flag is required because the Compat backend's emit may differ in line-ending shape across Windows-local and Linux-canonical runs. Real drift surfaces as changed declarations, changed attributes, changed signatures.

Review the diff for:

- New `extern DECLSPEC` declarations (new public binding surface — expected on a real SDL bump).
- Removed declarations (downstream surface change — confirm against SDL release notes; investigate if unexpected).
- Changed signatures (ABI-relevant change — investigate immediately; this can break consumers).
- Changed attributes (`[SupportedOSPlatform]` shifts indicate `platform_views[]` or `all_platform_macros[]` drift).

### 8.2 Stage 2 — Oracle Report Diff

The oracle (`oracle.cs`) produces `spikes/binding-generators/output/reports/oracle-evidence-clangsharp.md` — a Markdown evidence report aggregating per-family category counts, dynapi coherence percentages, and ABI-policy compliance evidence.

Run before and after the bump:

```pwsh
dotnet run --file spikes/binding-generators/clangsharp/oracle.cs -- --family sdl2-core --family sdl2-image --family sdl2-ttf --family sdl2-mixer --family sdl2-gfx --write-report
git --no-pager diff -- spikes/binding-generators/output/reports/oracle-evidence-clangsharp.md
```

Review for:

- Category-count shifts (Functions/Enums/POD structs/Opaque handles/Constants/Callbacks) per family.
- Dynapi coherence percentage drops below the current 98.1% baseline for SDL2.Core (see §"Dynapi Cross-Check" below).
- New oracle-flagged false positives or false negatives.

### 8.3 Stage 3 — AbiTests Runtime Smoke

AbiTests (`spikes/binding-generators/clangsharp/tests/abi-tests/AbiTests.csproj`) exercises Layer 1 raw ABI signatures per executable TFM (net462, net8.0, net9.0, net10.0). Linux x64 runs via Docker (see §"Linux-Canonical Generation").

```pwsh
dotnet test spikes/binding-generators/clangsharp/tests/abi-tests/AbiTests.csproj -c Release
```

Expected pass count stays stable across version bumps. A drop indicates ABI drift the generation pipeline did not catch. A rise indicates new test coverage was added in the same slice; document the addition in the commit.

### 8.4 Dynapi Manifest Cross-Check

SDL2 ships `src/dynapi/SDL2.exports` (Watcom-format text manifest, autogenerated by `gendynapi.pl`) listing every public export. Cross-platform single source of truth — Linux / macOS / Windows binaries all export the same symbol set per SDL2's dynapi design. The file lives inside vcpkg's `buildtrees/sdl2/src/<sha>.clean/src/dynapi/SDL2.exports` after `vcpkg install sdl2` completes a real source build.

The oracle computes **dynapi coherence percentage** as a 3-set-operation summary:

- Generator emit ∩ dynapi manifest → expected vast majority (correct public API entries).
- Generator emit \\ dynapi manifest → false-positive leaks (inline helper bleed, internal symbol bleed, header-parse mishap).
- Dynapi manifest \\ generator emit → false-negative under-emit (header exclusion too aggressive, missing platform pass).

Current baseline: **98.1% coherence for SDL2.Core**. Track per-bump; drops below ~97% deserve investigation before commit.

**Scope limit:** dynapi is SDL2-only. SDL2_image / SDL2_mixer / SDL2_ttf / SDL2_gfx / SDL2_net do not produce equivalent textual export manifests — their public surface is the public header (`extern DECLSPEC` declarations). Satellite drift detection consumes header-derived expected-export sets or harvested-binary symbol extraction via the Cake host's `BinaryClosureWalker` at Pack stage.

**Coverage limit — name-only, not signature.** The Watcom DEF-file format records only function names exported from the dynamic library, with no argument types, parameter order, or return shape. The dynapi cross-check proves name coverage only; it cannot prove parameter widths or ABI wire shape. The natural place to catch wire-format ABI mismatch is the per-RID consumer-smoke matrix (Cake host Pack stage), where bindings call selected functions against the native library and assert marshalling round-trips.

**Vcpkg buildtrees reach mechanism:** vcpkg's binary cache stores only the compiled install payload. On a binary-cache hit, vcpkg unpacks the cached archive directly into `installed/` and skips source extraction entirely — `buildtrees/sdl2/src/` is empty. Reach mechanisms:

- **Binding-generator Docker image** bakes vcpkg install state at image build time as a dedicated Docker layer; `buildtrees/sdl2/src/` is preserved in the image.
- **CI `vcpkg-setup` action** uses multi-path `actions/cache@v5` to cache both the binary cache and `external/vcpkg/buildtrees/sdl2/src/`.
- **Host dev:** `tools.cs setup` (or any `vcpkg install sdl2`) populates `external/vcpkg/buildtrees/sdl2/src/` once; subsequent runs preserve it.

### 8.5 Slopwatch Gate

```pwsh
slopwatch analyze --fail-on warning --exclude "artifacts/**,external/**,vcpkg_installed/**,**/bin/**,**/obj/**,spikes/binding-generators/references/**"
```

Zero issues required before commit.

## 9. Linux-Canonical Generation

Production-evidence generation is **native Linux** via the binding-generator Docker container. The container bakes vcpkg's `x64-linux-hybrid` triplet at image build time so the generator runs against the real Linux SDL2 headers, not Windows-host shims.

Dockerfile: `docker/binding-generator.Dockerfile`. Image tag: `janset-binding-generator:focal-latest`.

### 9.1 Full-Generation Pattern

```bash
docker run --rm --entrypoint sh janset-binding-generator:focal-latest \
  -c "cd /workspace/spikes/binding-generators/clangsharp && \
      python generate_bindings.py --family all --execute --vcpkg-triplet x64-linux-hybrid"
```

### 9.2 Local Windows Iteration

`--use-platform-header-shims` is a Windows-local spike aid for synthetic Linux/macOS/iOS platform passes. It adds `spikes/binding-generators/clangsharp/shims/platform-headers/` for missing system SDK headers such as `endian.h`, `AvailabilityMacros.h`, and `TargetConditionals.h`. **Do not treat shim-enabled output as final production evidence.** Real platform generation still needs native Linux/macOS runners (the binding-generator container for Linux x64; the CI matrix for macOS / ARM64).

Shim retirement criteria: shims retire if (and only if) the project escalates from single-host Linux-canonical parsing to a multi-runner cross-OS pipeline. Until then, treat the shim set as durable maintenance surface, not a workaround.

### 9.3 M3 Focused-Docker-Override Pattern

When proving narrow Linux / libclang / vcpkg-installed-header readiness facts, prefer a **focused command override** against the binding-generator image over running full generation:

```pwsh
$repo = (Get-Location).Path
docker run --rm --entrypoint bash `
  -v "${repo}:/workspace" `
  -w /workspace `
  janset-binding-generator:focal-latest `
  -lc "<focused test filter>"
```

For example, to run just the AbiTests project against the Linux container:

```bash
docker run --rm --entrypoint sh janset-binding-generator:focal-latest \
  -c "cd /workspace/spikes/binding-generators/clangsharp/tests/abi-tests && \
      dotnet test --project AbiTests.csproj -c Release --framework net10.0"
```

The pattern mounts the current checkout at `/workspace`, bypasses the image entrypoint, and runs only the requested command. This is build-host plumbing wisdom that survives toolchain transitions: when full generation is not the behaviour under test, do not pay the full-generation cost.

### 9.4 Cross-OS Container-Mount Discipline

When working with the binding-generator container, do not bind-mount Windows-host repo paths into a Linux container that runs vcpkg or compiled C/C++ toolchains. The line-ending and case-sensitivity differences corrupt vcpkg's hash-based reproducibility. Prefer COPY-into-image at build time + isolated cache volumes for vcpkg state.

## 10. Build-Host Configuration

The spike lives under `spikes/binding-generators/` with intentional **isolation from production multi-TFM build configuration**.

### 10.1 Spike Directory.Build.props

`spikes/binding-generators/Directory.Build.props` overrides the root `Directory.Build.props` for spike projects:

- `<LangVersion>preview</LangVersion>` — spike uses preview C# features.
- `<Nullable>enable</Nullable>`, `<ImplicitUsings>enable</ImplicitUsings>`, `<AllowUnsafeBlocks>true</AllowUnsafeBlocks>` — standard.
- `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` — spike enforces zero-warning discipline.
- `<ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>` — uses root `Directory.Packages.props` for CPM, but the postprocess csproj's `VersionOverride` on Microsoft.CodeAnalysis.CSharp carves out its own version (see §"Toolchain Version Pinning" 2.3).

The spike does **not** inherit the production multi-TFM matrix (`net462` / `netstandard2.0` / `net8.0` / `net9.0` / `net10.0`) by default — spike projects target `net10.0` for tooling and the generated-output managed projects under `src/Janset.SDL2.*/` carry the multi-TFM matrix themselves.

### 10.2 Root References

The spike intentionally references:

- Root `Directory.Build.props` (inherited via `<Project>` chain).
- Root `Directory.Packages.props` (CPM source of truth for non-Roslyn packages).
- Root `global.json` (SDK pinning — the spike uses the same SDK as production).

### 10.3 Isolation End — Production Flip

The spike isolation discipline ends at **Production Flip** (see roadmap §"Production Flip — SDL2.Core Reproducibility"). At that milestone, the code moves to `src/Janset.SDL2.*/` and inherits the production multi-TFM build matrix. The spike `Directory.Build.props` retires; production builds become the single source of truth.

Until Production Flip, treat any code that needs production multi-TFM compilation as a sign that the work belongs under `src/`, not under `spikes/binding-generators/`.

## 11. References

### Canonical Workstream Docs

- [`binding-generator-constitution.md`](binding-generator-constitution.md) — pure-principles policy authority. Cross-references: §"Manifest Configuration Vs Code-Owned Policy", §"Opaque Handles", §"BCL-Replaceable Helper Exclusion Policy", §"C Variadics", §"Scalar Type Translation", §"Foreign Type Boundary Policy".
- [`binding-generator-implementation-notes.md`](binding-generator-implementation-notes.md) — mechanism and evidence sibling to the constitution. Cross-references: §"Postprocess Pipeline Order", §"Opaque Handles Implementation Mechanism", §"C `long` Hybrid Implementation", §"Configuration Surface Evolution".
- [`binding-generator-roadmap.md`](binding-generator-roadmap.md) — layer-based forward milestones. Cross-references: §"Production Flip — SDL2.Core Reproducibility", §"SysWM Layout And Satellite Sweep Close", §"SDL3 Extension".
- [`testing-strategy.md`](testing-strategy.md) — testing layer model and raw ABI upstream port backlog.
- [`binding-output-oracle-validation.md`](binding-output-oracle-validation.md) — multi-oracle review workflow for generated binding output.

### Sibling Playbooks

- [`docs/playbook/overlay-management.md`](../../../../docs/playbook/overlay-management.md) — vcpkg overlay triplet procedure. Coupled to §"Version-Bump Procedures" 6.4 (Vcpkg Triplet Bump).
- [`docs/playbook/vcpkg-update.md`](../../../../docs/playbook/vcpkg-update.md) — vcpkg baseline procedure. Coupled to §"Version-Bump Procedures" 6.1 (SDL2 Version Bump).

### ADRs

- [ADR-002 — Target-centric build-host pattern](../../../../docs/decisions/2026-05-05-target-centric-build-host.md) — Cake-host architectural pattern; binding generator's eventual production landing in the Cake host inherits this.
- [ADR-003 — Contract-centric data layer](../../../../docs/decisions/2026-05-12-build-host-data-layer.md) — `build/_build/Data/BindingGeneration/` data contracts (stamp, manifest) extend this pattern.
- [ADR-004 — Binding Auto-Generation Toolchain](../../../../docs/decisions/2026-05-14-binding-autogen-toolchain.md) — toolchain selection record; currently Reopened pending Layer 2 closure.

### Knowledge-Base

- [`docs/knowledge-base/extraction-guidelines.md`](../../../../docs/knowledge-base/extraction-guidelines.md) — collaborator extraction discipline; applies to postprocess rewriter design.
- [`docs/knowledge-base/testing-guidelines.md`](../../../../docs/knowledge-base/testing-guidelines.md) — canonical TUnit / MTP test infrastructure; AbiTests follows this for the per-TFM smoke shape.

### Source Of Truth Artifacts

- [`spikes/binding-generators/clangsharp/config/family-config.json`](../../clangsharp/config/family-config.json) — per-family generation contract; the configuration surface mapped in §"Configuration Surface Map".
- [`spikes/binding-generators/clangsharp/generate_bindings.py`](../../clangsharp/generate_bindings.py) — orchestrator; invocation form documented in §"Configuration Surface Map" 3.1.
- [`spikes/binding-generators/clangsharp/postprocess/Janset.SDL2.PostProcess.csproj`](../../clangsharp/postprocess/Janset.SDL2.PostProcess.csproj) — Microsoft.CodeAnalysis.CSharp `VersionOverride` location.
- [`spikes/binding-generators/clangsharp/postprocess/Program.cs`](../../clangsharp/postprocess/Program.cs) — 7-step postprocess CLI dispatch; the pipeline order in §"Postprocess Pipeline Maintenance".
- [`spikes/binding-generators/clangsharp/oracle.cs`](../../clangsharp/oracle.cs) — evidence reporter; invocation form documented in §"Drift Detection Between Versions" 8.2.
- [`spikes/binding-generators/clangsharp/rsp/`](../../clangsharp/rsp/) — three-tier RSP organization (base / family / per-header).
- [`spikes/binding-generators/.config/dotnet-tools.json`](../../.config/dotnet-tools.json) — ClangSharp tool pin location.
- [`spikes/binding-generators/Directory.Build.props`](../../Directory.Build.props) — spike isolation contract.
- [`docker/binding-generator.Dockerfile`](../../../../docker/binding-generator.Dockerfile) — Linux production container.
