# Binding Generator Implementation Notes

> **Status:** Canonical companion to [`binding-generator-constitution.md`](binding-generator-constitution.md). Policy lives in the constitution; this document captures mechanism, evidence, and current implementation specifics for the ClangSharp + Roslyn postprocess spike under `spikes/binding-generators/clangsharp/`.

## 1. Purpose

The constitution states **what** the binding generator must do — the durable ABI/API policy that any compliant implementation must honor. This document captures **how** the current implementation realizes that policy. The split exists for three reasons:

- **Constitution must survive toolchain swaps.** Pure principles (e.g. "C `long` is platform-sensitive; do not collapse to `nint`/`nuint` in shared signatures") remain binding regardless of whether generation runs on ClangSharp + Roslyn postprocess, single-pass CppAst, or a future engine. Mechanism narratives (rewriter class names, RSP file paths, `family-config.json` field names) belong with the implementation, not the policy contract.
- **Mechanism evolves on each iteration.** Iteration 2's config-surface unification (2026-05-27) consolidated 18 dispersed config sources into a single `family-config.json`; iteration N+1 will land further changes. Updating mechanism without re-litigating policy keeps the policy authority stable.
- **Evidence is fact, not contract.** Peer-binding catalogs (ppy/SDL3-CS, Silk.NET, SDL2-CS, Alimer.Bindings.SDL), pin-set verification commands, and per-toolchain failure modes are durable for diagnostic and historical reasons but do not bind future implementations.

Cross-references use section names (`Constitution §"C \`long\` And \`unsigned long\`"`) rather than line numbers — the constitution gets purified periodically and line numbers shift on every edit. Class names + file paths are cited (`OpaqueHandleEmitRewriter` at `clangsharp/postprocess/OpaqueHandleEmitRewriter.cs`); source code is the canonical authority for the rewriter's exact behavior, so this document does not paste source bodies.

The current implementation lives at `spikes/binding-generators/clangsharp/`. Production flip (Roadmap §"Production Flip") promotes the generated `Janset.SDL2.<Family>/Generated/` trees into `src/Janset.SDL2.<Family>/Generated/` and retires the spike directory; mechanism narratives in this document survive the move, paths under `spikes/` rebase to `src/`.

## 2. Generation Determinism Implementation

Constitution §"Generation Determinism Contract" pins the invariants. The current implementation realizes them through the pin set and verification commands below.

### Pin set (8 inputs)

Every output byte is determined by these inputs alone. If none change, regeneration produces byte-identical output (CRLF aside on Windows).

1. **Vcpkg-installed native headers**, pinned via `build/manifest.json library_manifests[].vcpkg_version` per family: SDL2 Core 2.32.10, SDL2_image 2.8.8, SDL2_ttf 2.24.0, SDL2_mixer 2.8.1, SDL2_gfx 1.0.4 at the 2026-05-28 audit date. `build/manifest.json` is the single source of truth for which native headers exist.
2. **Vcpkg triplet** (e.g. `x64-windows-hybrid`). CI matrix pins one triplet per RID.
3. **ClangSharp tool version** pinned via `dotnet-tools.json` (currently 20.x line). `libclang.runtime.*` ships transitively through the tool.
4. **RSP files** under `spikes/binding-generators/clangsharp/rsp/`: three-tier organization — cross-cutting `base.rsp` + family `sdl2-<family>.rsp` + per-header `per-header/<header>.rsp`. All versionable text in git.
5. **Production per-family header lists** under `family-config.json families.<family>.headers[]`. Bootstrap/header-subset lists are not production inputs.
6. **`family-config.json`** at `spikes/binding-generators/clangsharp/config/family-config.json` — the unified config surface (per-family identity, opaque handles, flags enums, C long method lists, platform views, header inventories, required surface, owner mode). Single source of truth for both generator orchestrator and postprocess toolchain.
7. **Postprocess code** — the rewriter implementations under `spikes/binding-generators/clangsharp/postprocess/`.
8. **Orchestrator code** — `spikes/binding-generators/clangsharp/generate_bindings.py` (config loader, `selected_families`, pipeline order).

### Verification commands

Every Item-1+ slice that touches generation or postprocess MUST verify the determinism contract in its exit evidence. The four canonical checks:

```pwsh
# 1. Complete-family idempotency — regenerate twice, second diff must be empty
dotnet run --file spikes/binding-generators/clangsharp/generate_bindings.py -- --family core --execute
dotnet run --file spikes/binding-generators/clangsharp/generate_bindings.py -- --family core --execute
git diff --ignore-cr-at-eol -- spikes/binding-generators/clangsharp/src/Janset.SDL2.Core/Generated/

# 2. Family isolation — targeted run leaves other families byte-untouched
dotnet run --file spikes/binding-generators/clangsharp/generate_bindings.py -- --family ttf --execute
git status -- spikes/binding-generators/clangsharp/src/Janset.SDL2.Core/Generated/
# Expected: empty

# 3. Per-family equivalence — full active set equals union of per-family runs
dotnet run --file spikes/binding-generators/clangsharp/generate_bindings.py -- --family all --execute
# Capture trees, compare against per-family aggregation

# 4. Cross-family handle pull preserved — satellite output rewrites Core-owned pointers to by-value
Select-String -Path "spikes/binding-generators/clangsharp/src/Janset.SDL2.Image/Generated/**/*.g.cs" `
  -Pattern "SDL_Renderer\*|SDL_Texture\*|SDL_RWops\*"
# Expected: zero matches (all pointers rewritten to by-value)
```

### Family isolation specifics

The orchestrator's CLI exposes `--family <X>` and `--execute`. `--family X --execute` semantics:

- **Targeted-family-only writes.** Cleans and regenerates only `Janset.SDL2.<X>/Generated/`. Execute-mode cleanup deletes only the selected family's Generated tree.
- **Backends always paired.** Compat and Modern run together as internal backend projections of the same family artifact; production generation does not write partial bootstrap/header-subset output into committed `Generated/` roots.
- **Postprocess scoped per family.** Each family's postprocess pipeline executes against that family's own Generated tree only. The only cross-family flow at postprocess execution time is the data-only roster pull described in §3.

`--family all` activates the families listed in the orchestrator's `selected_families("all")` set. Families present in `family-config.json` but not in the active set remain **dormant**: their config entries are reachable for audit but no generation runs for them. The aggregate selection list is the activation gate; adding a family entry to `family-config.json` does not auto-enable it.

### Synthetic platform header shims (spike-only)

`spikes/binding-generators/clangsharp/shims/platform-headers/` contains three synthetic stubs (`endian.h`, `AvailabilityMacros.h`, `TargetConditionals.h`) so Windows-local iteration can exercise Linux/macOS/iOS parse views without needing a real Linux SDK. The shims are **opt-in** via `--use-platform-header-shims` and are **not a production substitute**. Production evidence runs against native platform headers via the binding-generator docker container (`docker/binding-generator.Dockerfile`) or per-RID CI. The shim flag exists for inner-loop velocity; canonical generation never sets it.

## 3. Opaque Handles Implementation Mechanism

Constitution §"Opaque Handles" defines Pattern B (`readonly partial struct X(nint value)`), the auto-detect criterion, the force-opaque allow-list rule, the cross-family data-only pull, and the cross-assembly `[assembly: DisableRuntimeMarshalling]` contract. The current implementation realizes them through `OpaqueHandleEmitRewriter` (`clangsharp/postprocess/OpaqueHandleEmitRewriter.cs`), invoked via the `uniform-opaque` postprocess mode.

### Rewriter input channels

The rewriter feeds two input channels into one Pattern B template:

- **Auto-detect channel.** Walks the post-ClangSharp generated tree; identifies every type `X` whose declaration is an empty `public partial struct X { }` and whose name appears as `X*` in at least one raw ABI signature position (parameter, return, or struct field) within the same TFM view. The intersection is the auto-detect roster.
- **Force-opaque channel.** Reads the family's `force_opaque_exceptions` list from `family-config.json`; these are types whose C body is declared in the public header but is unsafe to expose (function-pointer subclass state, platform-conditioned unions). Same Pattern B template, different input channel.

Both channels feed the same `BuildPatternBStruct(name)` template emitting the struct shape verbatim per Constitution §"Opaque Handles" **How** clause.

### Auto-detect criterion

Detection is **purely syntactic** and **family-blind**:

- The empty-struct check uses post-ClangSharp output, not `[NativeTypeName("X *")]` annotations. ClangSharp omits `NativeTypeName` when the C tag/typedef name matches the emitted C# name (the common case for `SDL_Window`, `TTF_Font`, `Mix_Music`).
- No name-prefix gate. The earlier `StartsWith("SDL_")` filter was dropped during Item 1 (Iteration 1) so satellite-owned handles satisfy the same structural test as Core handles. `TTF_Font` and `Mix_Music` enter their respective families' rosters via the same auto-detect path.

### Field-position rewrite

The rewriter rewrites single-pointer references to handle types at **three** raw ABI positions:

1. Method parameter positions: `SDL_Window* w` → `SDL_Window w`.
2. Method return positions: `SDL_Window* SDL_CreateWindow(...)` → `SDL_Window SDL_CreateWindow(...)`.
3. Struct field positions: `SDL_SysWMmsg* msg` field → `SDL_SysWMmsg msg` field.

Double-pointer (`X**`) and `out X` positions are preserved as-is. The ABI invariant holds because Pattern B structs carry exactly one `nint` field; their layout is bit-identical to a pointer at the corresponding position. Field-position rewrite improves API ergonomics without changing native C struct layout.

### Family-keyed roster integration

The canonical roster lives in `family-config.json` under `families.<family>.opaque_handles` with three sub-keys:

- `auto_detect_well_known` — pre-known names for this family, with `last_audited` date and (optional) `wiki_url` evidence per entry.
- `force_opaque_exceptions` — Constitution-bound force-opaque names. For SDL2.Core: `SDL_RWops`, `SDL_SysWMinfo`, `SDL_SysWMmsg`.
- `excluded_candidates` — names that look like Pattern B candidates but are deliberately excluded (e.g. `SDL_iconv_t` residue after `SDL_iconv_*` exclusion under §"BCL-Replaceable Helper Exclusion Policy").

Each family entry also carries `library_version` (e.g. `"2.32.10"` for Core, `"2.24.0"` for TTF) and `last_audited` (ISO date). Audit method per family is **three-source triangulation**: family-pinned release headers (forward-decl evidence), wiki / project documentation pages (opacity phrasing where present), and ClangSharp Modern output (empty-struct emit). All three sources must agree before a name enters `auto_detect_well_known`; wiki evidence may be `not_found` when sources 1 and 3 agree.

### Cross-family handle name resolution

A satellite's postprocess pass must know which names are handle types — not just satellite-owned ones (`TTF_Font`, `Mix_Music`) but also Core-owned ones (`SDL_Renderer`, `SDL_Texture`, `SDL_RWops`) that the satellite consumes by-value at its `[LibraryImport]` surface. The loader contract:

- **Core's** loader returns Core's own `auto_detect_well_known` ∪ Core's `force_opaque_exceptions`.
- **Each satellite's** loader returns the satellite's own `auto_detect_well_known` ∪ the satellite's `force_opaque_exceptions` ∪ **Core's `auto_detect_well_known`** ∪ **Core's `force_opaque_exceptions`**.

This preserves Pattern B's uniform by-value semantic at every raw ABI position regardless of which family owns the handle. The pull is **data-only**: satellite postprocess execution still does not read cross-family `.g.cs` files — the `family-config.json` Core section provides the names directly.

### Owner mode and namespace plumbing

Per-family `owner_mode` (boolean in `family-config.json families.<family>.owner_mode`) decides whether a family emits its own `Handles.g.cs`. Current assignments at the 2026-05-28 audit date:

- **Owners** (emit own `Handles.g.cs`): Core, TTF, Mixer.
- **Consumers** (no local `Handles.g.cs`; resolve handle names through ProjectReference): Image, GFX.

`OpaqueHandleEmitRewriter` reads the namespace from the `.g.cs` file being processed and emits `Handles.g.cs` into that namespace. Earlier iterations passed `--handles-namespace SDL2.<Family>` as an explicit CLI flag; post-Iteration-2 config-surface unification consolidated this into `families.<family>.namespace` in `family-config.json`. The substring-based fallback in `UniformOpaqueOwnerMode.IsOwnerDirectoryByPath` remains as a safety net only — the config-driven contract is authoritative.

### Drift watchdog

A per-family drift watchdog warns when syntactic discovery diverges from the family's `auto_detect_well_known` section: each family's owner directory is checked against its own roster section. Consumer directories (Image, GFX) skip drift reporting because they declare no local handle types — their reachable handles all come from Core's roster via the cross-family pull above. Satellite-owned handles participate in the same drift-watchdog discipline as Core's handles.

A future low-priority extension is the **owner-mode zero-handle warning**: if an owner-mode family suddenly produces zero auto-detected handles, that is a red flag (likely a generator regression or upstream header change) — the watchdog should warn rather than treat zero as healthy.

### Peer-divergence catalog

The constitution's Pattern B by-value handle policy is a **deliberate divergence** from every peer SDL binding. For diagnostic and historical reasons, the peer landscape:

- **ppy/SDL3-CS** keeps satellite handles as empty `partial struct X` used via `X*` pointer at every signature position. The handle type carries no value; consumers pass the pointer. No by-value handle.
- **Silk.NET 2.X** uses the same empty-struct-plus-pointer shape as ppy. Different conventions than ppy on attribution but identical handle shape.
- **SDL2-CS** uses `IntPtr` for every handle slot, with `/* IntPtr refers to a TTF_Font* */` inline comments preserving provenance. No typed handle.
- **Alimer.Bindings.SDL** uses Pattern B for Core handles (the implementation that proved the cross-assembly contract under §3 below). Does not extend the pattern to satellites because Alimer only covers Core + Image at the audit date.

Zero peers use typed Pattern B by-value handles for satellites. Janset's Pattern B is deliberate: the structural ABI invariant (single `nint` field → bit-identical to bare pointer) lets the typed shape ship without the explicit-conversion friction that wrapping a foreign-binding handle would incur. Constitution §"Opaque Handles" **Why** clause captures the ABI rationale; this catalog is the supporting peer evidence.

## 4. C `long` Hybrid Implementation

Constitution §"C `long` And `unsigned long`" pins the contract: do not collapse to `nint`/`nuint` in shared signatures; internal raw ABI uses `CLong`/`CULong` where available, guarded to `NET6_0_OR_GREATER`; structural thread symbols preserved on every TFM via a hybrid emit. The current implementation realizes the hybrid through `ClongDualDispatchRewriter` (`clangsharp/postprocess/ClongDualDispatchRewriter.cs`), invoked via the `clong-dispatch` postprocess mode.

### Convenience-helper exclusion (Compat + Modern)

The six SDL_stdinc convenience helpers (`SDL_lround`, `SDL_lroundf`, `SDL_ltoa`, `SDL_ultoa`, `SDL_strtol`, `SDL_strtoul`) fall under §"BCL-Replaceable Helper Exclusion Policy" and are excluded all-TFM via per-header RSP `--exclude` entries in `clangsharp/rsp/per-header/SDL_stdinc.rsp`. No emit on any TFM; consumers use `Math.Round` / `long.Parse` / `ulong.Parse` / `ToString()` from the BCL.

### Structural thread symbols (hybrid emit)

The three SDL_thread symbols (`SDL_threadID` typedef + `SDL_ThreadID`/`SDL_GetThreadID` functions) preserve on every TFM via a hybrid emit, mode-split across two backend projections:

**Modern projection** (compiled for `net6.0`+ via csproj conditional `<Compile Include>`):

```csharp
[LibraryImport("SDL2", EntryPoint = "SDL_ThreadID")]
[UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
internal static partial CULong SDL_ThreadID();
```

**Compat projection** (compiled for `netstandard2.0` / `net462` only):

```csharp
internal static ulong SDL_ThreadID()
{
    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
    {
        return SDL_ThreadID_Win32();    // private [DllImport] uint
    }
    return (ulong)SDL_ThreadID_Unix64();   // private [DllImport] nint
}

[DllImport("SDL2", EntryPoint = "SDL_ThreadID", CallingConvention = CallingConvention.Cdecl)]
private static extern uint SDL_ThreadID_Win32();

[DllImport("SDL2", EntryPoint = "SDL_ThreadID", CallingConvention = CallingConvention.Cdecl)]
private static extern nint SDL_ThreadID_Unix64();
```

The Compat dual-dispatch follows Microsoft's [documented cross-platform `long` dispatch pattern](https://learn.microsoft.com/en-us/dotnet/standard/native-interop/best-practices). Each output emits the **single TFM-appropriate form** — no `#if NET6_0_OR_GREATER` directives inside generated source. TFM gating happens via the csproj's conditional `<Compile Include>` items, mirroring the existing `libraryimport` postprocess split (Compat keeps `[DllImport]`; Modern is rewritten to `[LibraryImport]`).

### csproj `<Compile Include>` gating

Each family's `Janset.SDL2.<Family>.csproj` carries conditional compile includes that route Compat vs Modern based on the active TFM:

```xml
<ItemGroup Condition="'$(TargetFramework)' == 'netstandard2.0' OR '$(TargetFramework)' == 'net462'">
  <Compile Include="Generated\Compat\**\*.g.cs" />
</ItemGroup>
<ItemGroup Condition="'$(TargetFramework)' != 'netstandard2.0' AND '$(TargetFramework)' != 'net462'">
  <Compile Include="Generated\Modern\**\*.g.cs" />
</ItemGroup>
```

Compat sources contain `[DllImport]` declarations only; Modern sources contain `[LibraryImport]` declarations only. No source file mixes both styles; no source file uses `#if` directives to switch between them.

### Roslyn-node-level mutation rationale

The rewriter uses **true Roslyn syntax mutation** (move mutation point to `VisitClassDeclaration`, build replacement members via `SyntaxFactory` calls) rather than text substitution. The rationale:

- Inherits Roslyn's normalization automatically (whitespace, trivia, attribute lists).
- Composes cleanly with future attribute-shape changes (e.g. `[UnmanagedCallConv]` additions).
- Supports parameter-position rewrites that text substitution cannot handle reliably.

Acceptable cost: byte-identical output between text-substitution and Roslyn `SyntaxFactory` formatting is non-trivial to guarantee, so the first migration absorbs a one-time formatting churn. Subsequent regenerations are byte-stable.

### Parameter-position rewrite

`AffectedMethodNames` covers `SDL_ThreadID`, `SDL_GetThreadID`, and (when TTF activates) the five TTF C `long` functions: `TTF_OpenFontIndex`, `TTF_OpenFontIndexRW`, `TTF_OpenFontIndexDPI`, `TTF_OpenFontIndexDPIRW`, `TTF_FontFaces`. The rewriter inspects return + parameter native type names; the rewrite applies per-family and **skips families with no matching function names**. Image/GFX have no C `long` surface — the rewriter visits but emits no mutation.

### Signed-vs-unsigned discriminator

The rewriter inspects each affected method's `[NativeTypeName(...)]` annotations to choose `CLong` (signed) vs `CULong` (unsigned):

- TTF index parameters carry `[NativeTypeName("long")]` → signed → emits `CLong` Modern, `int`/`nint` Compat dual-dispatch.
- `SDL_ThreadID` / `SDL_GetThreadID` returns carry `[NativeTypeName("unsigned long")]` → unsigned → emits `CULong` Modern, `uint`/`nint` Compat dual-dispatch.

The annotation-driven discriminator means new C `long` surfaces in future SDL versions automatically pick the right variant without code change — provided their `[NativeTypeName]` annotations are accurate.

### Peer evidence

- **Alimer.Bindings.SDL** validates the Modern path. `CLong`/`CULong` emitted directly via Roslyn `SyntaxFactory`.
- **ppy/SDL3-CS** and **SDL2-CS** ship the Constitution-rejected anti-pattern: `uint` for unsigned long, `int` for long on LLP64. Silently wrong on Linux LP64 (32-bit values where the native ABI carries 64-bit). The SDL2-CS source file `external/sdl2-cs/src/SDL2_ttf.cs` includes an inline `/* IntPtr is actually a C long! This ignores Win64! */` comment acknowledging the gap.
- **Microsoft's `RuntimeInformation.IsOSPlatform` dispatch pattern** is the only Compat-path precedent — documented as the cross-platform `long` strategy in the official native-interop best-practices guide.

## 5. wchar_t Implementation Mechanism

Constitution §"wchar_t" pins the policy: shared `wchar_t*` maps to opaque `nint` at the raw ABI layer; no `int*`/`char*` collapse; `[SupportedOSPlatform("windows")]` retained for Windows-only API surface like `SDL_WinRTGetFSPathUNICODE`. The current implementation uses two layered mechanisms.

### Primary path — RSP-level `--remap`

The cross-cutting `rsp/base.rsp` carries two literal-space `--remap` entries:

```
--remap
wchar_t *=nint
const wchar_t *=nint
```

These entries are **unquoted, one per line, with literal spaces**. System.CommandLine treats each RSP line as one argv element verbatim; libclang then performs byte-exact match against the typedef / pointer spelling. The space-containing variants are essential — `wchar_t*=nint` (no space) does not match libclang's canonical spelling, which always carries the space before the asterisk.

This is the **type-system path**. It catches shared `wchar_t*` at parameter positions, return positions, and field positions throughout the generated output. ClangSharp emits `nint` at every match site and preserves a `[NativeTypeName("wchar_t *")]` annotation for downstream provenance.

### Fallback path — `WcharStarToNintRewriter`

A few `wchar_t*` use sites the type-system path does not reach get caught by a Roslyn postprocess fallback (`clangsharp/postprocess/` — currently inlined behavior; the dedicated rewriter file lands when an unreached case is identified). The fallback walks the generated tree, finds any remaining `wchar_t*` references that escaped the RSP remap, and rewrites them to `nint` preserving the `NativeTypeName` annotation.

The two-layer design — type-system primary path + Roslyn postprocess fallback — exists because libclang's canonical spelling occasionally drifts for parameter-vs-field contexts or const-vs-non-const variants. The fallback is conservative defensive scaffolding; in current SDL2 generation it operates as a no-op.

### What does NOT happen

- No `string` parameter, no `[MarshalAs(UnmanagedType.LPWStr)]`. Those would be Layer 3 ergonomics, not Layer 1 ABI.
- No Layer 1 platform-conditional shape. Shared `wchar_t*` is uniformly `nint` regardless of TFM or RID — the platform-aware decoding happens at Layer 3 friendly wrappers (`Marshal.PtrToStringUni` on Windows; UTF-32 transcode on POSIX).

## 6. SDL_GUID Substitution

Constitution §"Foreign Type Boundary Policy" treats `SDL_GUID` as a special SDL-owned substitution: SDL's 16-byte GUID type is wire-identical to .NET's `System.Guid`, so the generator emits `System.Guid` at every signature position instead of generating an `SDL_GUID` struct. The current implementation uses `GuidSubstitutionRewriter` (`clangsharp/postprocess/GuidSubstitutionRewriter.cs`), invoked via the `guid-substitute` postprocess mode.

### Mechanism

The rewriter walks every `.g.cs` file in the family's Generated tree and:

1. Finds every `[NativeTypeName("SDL_GUID")]` annotation.
2. Rewrites the annotated managed type to `System.Guid`.
3. Suppresses emission of any `SDL_GUID` struct declaration that would otherwise be generated.

The 16-byte wire-identical layout makes the substitution ABI-safe: SDL's `SDL_GUID` is `typedef struct SDL_GUID { Uint8 data[16]; } SDL_GUID;`; `System.Guid` is also a 16-byte sequential value type with identical wire footprint. Field ordering inside `Guid` differs from `SDL_GUID.data[16]` in the .NET API surface (Guid exposes a `Data1`/`Data2`/`Data3`/`Data4` structured view), but the **wire byte sequence** is identical for blittable purposes.

### Scope

Currently only `SDL_GUID` (Core) is treated this way. Future foreign types with portable BCL equivalents could follow the same pattern (`SDL_Locale` has no direct BCL equivalent; UUID types in other SDL satellites would qualify if introduced).

## 7. Foreign Type Boundary Implementation

Constitution §"Foreign Type Boundary Policy" defines the **why** (avoid wrapping foreign types in Pattern B; SDL-owned typed handles vs foreign IntPtr boundary) and **what** (categorical allow-list with per-header attribution). The current implementation uses per-header RSP `--remap` entries with byte-exact textual matching.

### Mechanism

Each foreign type category lives in a per-header RSP file at `clangsharp/rsp/per-header/<header>.rsp`. The mechanism shape:

```
# rsp/per-header/SDL_vulkan.rsp (excerpt)
--remap
VkInstance_T *=nint
VkSurfaceKHR_T *=nint
--exclude
VkInstance_T
VkSurfaceKHR_T
```

The `--remap` entry maps the foreign typedef's pointer spelling to `nint` at every signature position. The `--exclude` entry suppresses the parser tag struct emission (libclang sees `struct VkInstance_T;` forward declaration; without `--exclude`, ClangSharp would emit a stub `public partial struct VkInstance_T { }`).

A `[NativeTypeName("VkInstance")]` annotation (or equivalent for the category) preserves provenance for documentation and downstream postprocess sensors. The allow-list lives close to the header that references the foreign type, with comments citing the SDL header source line and the upstream owner library (Vulkan, Win32, GDK, etc.).

### Disposition table

The disposition column moved out of the constitution per Q3C purification. Active vs Deferred status at the 2026-05-28 audit:

| Category | Foreign types in SDL2 public API | Location | Disposition |
| --- | --- | --- | --- |
| Vulkan | `VkInstance` (= `VkInstance_T *`), `VkSurfaceKHR` (= `VkSurfaceKHR_T *` on 64-bit) | `SDL_vulkan.h:52-53,187-188` | **Active** — `rsp/per-header/SDL_vulkan.rsp` |
| Direct3D COM | `IDirect3DDevice9 *`, `ID3D11Device *`, `ID3D12Device *` | `SDL_system.h:77-127` (Windows pass) | **Active** — `rsp/per-header/SDL_system.rsp` (Windows-conditioned) |
| Microsoft GDK | `XTaskQueueHandle` (= `XTaskQueueObject *`), `XUserHandle` (= `XUser *`) | `SDL_system.h:599-630` (GDK pass) | **Active** — `rsp/per-header/SDL_system.rsp` (GDK-conditioned) |
| Win32 handles | `HWND`, `HDC`, `HINSTANCE` | `SDL_syswm.h:165,235-237` | **Already handled** — `rsp/sdl2-core.rsp` remap `HWND__* / HDC__* / HINSTANCE__* = nint` |
| Android JNI | `JNIEnv *`, `jobject` | `SDL_system.h:258-294` (pre-erased to `void*` by SDL) | **Already handled** — `rsp/base.rsp` `void*=nint` covers |
| C stdlib | `FILE *`, `va_list` | `SDL_rwops.h`, `SDL_log.h`, `SDL_stdinc.h` | **Already handled** — `rsp/sdl2-core.rsp` FILE* remap + variadic-deferral excludes |
| Linux X11 | `Display *`, `Window`, `XEvent *` | `SDL_syswm.h:173,249-250` | **Deferred** — `SDL_syswm.h` not yet in multi-OS pass |
| Linux Wayland | `wl_display *`, `wl_surface *`, `wl_egl_window *`, `xdg_*` | `SDL_syswm.h:295-302` | **Deferred** — same |
| Linux KMSDRM | `gbm_device *` | `SDL_syswm.h:342` | **Deferred** — same |
| macOS Cocoa | `NSWindow *` | `SDL_syswm.h:266-271` (Apple pass) | **Deferred** — Apple multi-OS pass not enabled |
| iOS UIKit | `UIWindow *`, `UIViewController *` | `SDL_syswm.h:280-289` (iOS pass) | **Deferred** — same |
| Metal | `void *` (already opaque upstream) | `SDL_render.h:1890-1911`, `SDL_metal.h` | **Already handled** — `void*=nint` covers; SDL deliberately exposes `CAMetalLayer*` / `MTLCommandEncoder` as `void*` |
| WinRT | `IInspectable *` | `SDL_syswm.h:243` (WinRT pass) | **Deferred** — gated behind `SDL_GetWindowWMInfo` (currently excluded) |
| OpenGL / EGL / GLES | `EGL*`, `GL*` (Khronos types) | `SDL_opengl*.h`, `SDL_egl.h` | **Not in scope** — entire header set excluded from parse. `SDL_GLContext` is SDL-owned (`typedef void *` in `SDL_video.h:221`), not foreign |
| DirectFB / Mir / Vivante / OS/2 | various | `SDL_syswm.h` | **Excluded** — Constitution Stage 1 exclusions; never emitted |

When future slices activate additional platform passes (Linux variant of `SDL_syswm.h`, Apple variant, WinRT variant), the corresponding deferred rows transition to **active** and earn their own per-header RSP entries. The allow-list is grown deliberately; no auto-detection sweep silently expands it.

### Cross-family error-macro alias exclusion

Across SDL2 satellites (Image, TTF, Mixer), each family ships `<FAM>_SetError`/`<FAM>_GetError`/`<FAM>_ClearError` macro aliases that redirect to Core SDL error APIs (e.g. `IMG_SetError(...)` → `SDL_SetError(...)`). Janset excludes them from raw output — they are not satellite-owned functions and would create duplicate raw ABI surface. SDL2-CS keeps them as manual redirects in the satellite's managed surface; Janset's exclusion is the cleaner posture given the ProjectReference resolution. The exclusion lives in each family's `clangsharp/rsp/sdl2-<family>.rsp` under `--exclude`.

## 8. `[Flags]` Detection Mechanism

Constitution §"Enums" → "`[Flags]` auto-decoration policy" defines the rule: suffix match (`EndsWith("Flags")` case-sensitive) OR family-keyed allow-list. The current implementation uses `FlagsAttributeRewriter` (`clangsharp/postprocess/FlagsAttributeRewriter.cs`), invoked via the `flags-detect` postprocess mode.

### Mechanism

The rewriter is **name-only**; it does not analyze enum values. For each enum declaration in the generated tree:

1. Check if the enum already carries `[Flags]` (e.g. pre-existing from a manual edit). If yes, pass through unchanged.
2. Check if the enum's name `EndsWith("Flags")` (case-sensitive). If yes, decorate.
3. Check if the enum's name appears in `family-config.json families.<family>.flags_enums.allow_list`. If yes, decorate.
4. Otherwise, leave undecorated.

The decoration emits a `[System.Flags]` attribute above the enum declaration. Trivia (comments, blank lines) before and after the attribute is preserved. Idempotent: running the rewriter twice produces the same output.

### Why power-of-two heuristic was rejected

Three reasons:

1. **Constitution friction with `SDL_bool`.** A naive "every value is a power of two → decorate" heuristic would false-positive on `SDL_bool` (`SDL_FALSE = 0`, `SDL_TRUE = 1` — both qualify under the naive check). `[Flags]` on `SDL_bool` would break the int-backed bool contract in Constitution §"SDL_bool". Strengthening with "at least 2 distinct non-zero values" fixes this specific case but doesn't address every future shape — better to require explicit evidence.
2. **No SDL2-CS precedent for heuristic detection.** The reference binding uses no heuristic; flags are decorated manually by the SDL2-CS maintainer where they belong.
3. **Only Alimer auto-detects in production.** Of the four peer SDL bindings surveyed (ppy/SDL3-CS, Silk.NET 2.X, SDL2-CS, Alimer.Bindings.SDL), only Alimer auto-detects `[Flags]` — and Alimer uses precisely the suffix-plus-hardcoded-allow-list approach Janset adopted (`CsCodeGenerator.Enum.cs:256-264`). ppy uses manual companion-file annotations (incompatible with Janset's no-magic-companion philosophy); Silk.NET 2.X drops `[Flags]` entirely.

The suffix + allow-list rule is the safest combination: catches the common case (naming conventions across Core and satellites: `SDL_RendererFlags`, `IMG_InitFlags`, `MIX_InitFlags`) without value-pattern false positives, with an explicit allow-list slot for composite-alias-bearing enums (`SDL_Keymod`, `SDL_BlendMode`, `SDL_GLcontextFlag`, `SDL_RendererFlip`, `SDL_TextureModulate`).

### Family-keyed roster

The allow-list lives in `family-config.json families.<family>.flags_enums.allow_list` with `library_version` and `last_audited` metadata, schema-symmetric with the opaque-handle roster (§3). Audited per SDL2/satellite release.

### Known flag enums data (Stage 1)

The constitution previously enumerated these per family; the enumeration lives here as durable evidence:

- **SDL2.Core** (decorated by `Flags` suffix): `SDL_MessageBoxFlags`, `SDL_MessageBoxButtonFlags`, `SDL_RendererFlags`, `SDL_WindowFlags`.
- **SDL2.Core** (decorated by allow-list match): `SDL_Keymod`, `SDL_BlendMode`, `SDL_GLcontextFlag`, `SDL_RendererFlip`, `SDL_TextureModulate`.
- **SDL2.Image** (suffix): `IMG_InitFlags`.
- **SDL2.Mixer** (suffix): `MIX_InitFlags`.
- **SDL2.Ttf**, **SDL2.Gfx**: no Stage 1 flag enums known at audit date.

Composed alias values (`KMOD_CTRL = KMOD_LCTRL | KMOD_RCTRL`) are preserved as enum members regardless of decoration; the bitmask semantics flow from the allow-list entry, not from value analysis.

## 9. Cross-Assembly Pattern B Contract

Constitution §"Opaque Handles" → "Cross-assembly Pattern B contract" pins the rule: `[assembly: DisableRuntimeMarshalling]` lands in every generation-emitting assembly that exposes `[LibraryImport]` declarations consuming Pattern B handles, guarded by `#if NET7_0_OR_GREATER`. The current implementation realizes this through a hand-authored support file per family.

### Mechanism

Each generation-emitting assembly (`Janset.SDL2.Core`, `Janset.SDL2.Image`, `Janset.SDL2.Ttf`, `Janset.SDL2.Mixer`, `Janset.SDL2.Gfx`) carries a `Support/DisableRuntimeMarshalling.cs` file with the shape:

```csharp
#if NET7_0_OR_GREATER
[assembly: System.Runtime.InteropServices.DisableRuntimeMarshalling]
#endif
```

The file is hand-authored, not generated. The rationale for per-assembly placement (rather than a shared file in a referenced support package) is that `[assembly: ...]` attributes are scoped to the declaring assembly; cross-assembly `DisableRuntimeMarshalling` from a referenced package does not apply.

### SYSLIB1051 resolution rationale

The modern `[LibraryImport]` source generator (net7+) emits SYSLIB1051 when a satellite assembly's P/Invoke surface uses a Pattern B handle struct by value that is defined in a *referenced* assembly. The struct is blittable by construction (`readonly partial struct X { nint Value; }` — single pointer-sized field, layout bit-identical to a raw pointer), but the source generator inspects cross-assembly types through metadata rather than source declarations and falls back to a conservative "user-defined struct requires runtime marshalling opt-in" path.

The documented Microsoft resolution per the [P/Invoke source generator design](https://learn.microsoft.com/en-us/dotnet/standard/native-interop/pinvoke-source-generation) and the peer convention in [Alimer.Bindings.SDL](https://github.com/amerkoleci/Alimer.Bindings) is to apply `[assembly: DisableRuntimeMarshalling]` to every assembly that exposes `[LibraryImport]` declarations consuming Pattern B handles. The attribute:

1. Suppresses SYSLIB1051 for blittable cross-assembly Pattern B.
2. Guarantees the whole assembly uses only blittable types at every P/Invoke position.
3. Is conditional on `NET7_0_OR_GREATER` because the attribute itself doesn't exist on legacy TFMs (and the legacy `[DllImport]` backend used by the Compat tree doesn't trip the source-gen diagnostic).

The blittable-clean invariant rationale lives in Constitution §"Opaque Handles" Cross-Assembly Pattern B Contract subsection. Mechanism summary: the attribute formalizes an existing invariant via build-time enforcement — the single observable change is that adding a non-blittable type to any P/Invoke signature now fails at build time with a clear diagnostic, instead of silently triggering runtime marshalling.

## 10. Oracle Classification Labels

`oracle.cs` (the spike-local Roslyn-based validation app at `spikes/binding-generators/clangsharp/oracle.cs`) emits findings labeled with one of six classification labels. The labels are mechanical evidence-availability + family-applicability markers, **not** the multi-oracle review playbook's labels.

| Label | Definition |
| --- | --- |
| **Hard Bug** | Contradicts pinned headers, native exports, or constitution. Must be fixed before promotion to production. Example: raw ABI public-class leak, missing dynapi-listed symbol, public struct emitted with platform-conditioned layout. |
| **Likely Bug** | Strong evidence of a problem but one source is missing or incomplete (e.g. native export evidence not yet wired). Should be fixed but might be re-classified as Hard Bug or Accepted Deferral once evidence improves. |
| **Accepted Deferral** | Intentional policy deferral with documented source (Constitution §"Function Surface" Stage 1 deferrals, Constitution §"Structs And Unions" SysWM Stage 2, etc.). Not a fix candidate. |
| **Compatibility Risk** | Mismatch with SDL2-CS that may affect migration but is not target truth. SDL2-CS is an oracle, not the API target; differences here surface for awareness, not as bugs. |
| **Evidence Missing** | Input source unavailable locally or not wired yet (e.g. `oracle-evidence-clangsharp.md` hasn't been regenerated for this family since a config change). Not a bug — gap in evidence pipeline. |
| **Out Of Scope** | Source not valid for this family or category (e.g. SDL.h required surface checks Core-only by design; satellites return `SourceStatus.Missing` for this check rather than Hard Bug). |

### Relationship to oracle-validation playbook

The canonical multi-oracle review playbook ([`binding-output-oracle-validation.md`](binding-output-oracle-validation.md)) defines its own 6-label set: Hard Bug / Likely Bug / Compatibility Risk / Design Signal / Accepted Delta / Follow-up. The two sets share Hard Bug + Likely Bug + Compatibility Risk; oracle.cs uses Accepted Deferral (synonymous with the playbook's Accepted Delta for policy-deferred items) + Evidence Missing + Out Of Scope (mechanical labels not present in the multi-oracle-review playbook). The playbook's Design Signal + Follow-up labels are out of scope for mechanical raw-ABI checks — they apply to human multi-oracle review, not automated evidence extraction.

### Initial Raw ABI checks

The 10 mechanical check IDs `oracle.cs` emits:

- `raw-abi-public-class` — public raw ABI class leak (containing class becomes effectively public).
- `raw-abi-public-import` — public `[DllImport]` / `[LibraryImport]` leak.
- `required-function-missing` — SDL.h required surface declares a function the generator did not emit (Core-only).
- `required-constant-missing` — SDL.h required surface declares a constant the generator did not emit (Core-only).
- `deferred-layout-sdl-rwops` — `SDL_RWops` emitted with full layout instead of Pattern B quarantine.
- `deferred-layout-sdl-syswminfo` — `SDL_SysWMinfo` emitted with full layout.
- `deferred-layout-sdl-syswmmsg` — `SDL_SysWMmsg` emitted with full layout.
- `platform-sensitive-long` — C `long` collapsed to `nint`/`nuint` in shared signature.
- `platform-sensitive-wchar` — `wchar_t*` collapsed to `ushort*`/`char*` in shared signature.
- `family-namespace-drift` — generated satellite emits in wrong namespace (e.g. SDL2_image in `SDL2` instead of `SDL2.Image`).

## 11. Hardcoding Rules

The constitution defines manifest-vs-code-owned-policy at the principle level. This section catalogs the concrete acceptable hardcoding patterns vs the anti-patterns — derived from spike experience and codified during Item 1 (Iteration 1).

### Good code-owned policy

These belong in code, not in JSON config:

- **C primitive + SDL typedef width mapping.** `Sint8`/`Uint8`/`Sint16`/`Uint16`/`Sint32`/`Uint32`/`Sint64`/`Uint64`/`size_t`/`ptrdiff_t` map to fixed-width C# primitives. Same for every family forever; ABI rule, not configuration.
- **SDL2 `SDL_bool` shape.** Int-backed enum, wire type `int`. ABI rule.
- **C `long` / `unsigned long` handling.** Platform-sensitive; hybrid emit per Constitution §"C `long`". ABI rule.
- **`wchar_t` / `FILE` / `va_list` / Vulkan / Direct3D handling.** Foreign-type boundary policy per Constitution §"Foreign Type Boundary Policy". Type-system mechanism, not per-family configuration.
- **C# keyword escaping.** `event`, `params`, `string`, etc. need `@`-prefix when used as parameter names. Generator policy.
- **Macro taxonomy.** Object-like vs function-like vs platform-control vs assertion macro classification per Constitution §"Constants And Macros". Source-first conservative evaluation.
- **SDL-specific substitutions.** `SDL_GUID` → `System.Guid` substitution per §6. Substitution policy.

### Good config-owned facts

These belong in `family-config.json`, not in code:

- **Library/family identity.** Which families exist (`families.core`, `families.image`, `families.ttf`, `families.mixer`, `families.gfx`).
- **Input headers.** The per-family `headers[]` list.
- **Output namespace + public class name.** `families.<family>.namespace`, `families.<family>.raw_class`.
- **Native library import name.** Derived from manifest or per-family `library_name`.
- **Owned prefixes / explicit owned symbols.** Which symbols belong to which family.
- **Excluded / deferred declarations.** Per-family exclusion lists.
- **Manually-required declarations or constants** from intentionally excluded umbrella headers.

### Bad patterns

These were observed during the spike and explicitly rejected:

- `SDL2` library identity hardcoded in the emitter while also living in config. Library identity flows from config alone.
- `/SDL2/` path checks scattered through unrelated policies. Path matching is fragile and couples generator engine to filesystem layout.
- `SDL_` prefix rules duplicated across macro / declaration / type-ownership code. The prefix is a manifest fact (owned prefixes); code reads it from one place.
- Turning manifest JSON into a mini ABI policy language (e.g. `families.<family>.bool_byte_width: 4`). ABI rules stay in code; config encodes which symbols / which families, not how policy operates.

### Postprocess as standalone console app — not Roslyn Source Generator

The postprocess pipeline runs as a standalone .NET console app at `spikes/binding-generators/clangsharp/postprocess/Janset.SDL2.PostProcess.csproj`, **not** as a Roslyn Source Generator. Three reasons:

1. **Constitution §"Generator Home" requires committed `.g.cs` outputs with reproducibility stamp.** Roslyn SG adds files at consumer-build-time which can't be committed; postprocess console app reads/transforms/writes existing `.g.cs` at build-host time.
2. **Roslyn SG cannot modify existing generated files, only add new ones.** The required transforms (`[DllImport]` → `[LibraryImport]`, `enum X` → `[Flags] enum X`, Pattern B field-position rewrite) all need to mutate existing source. Roslyn SG is the wrong tool category.
3. **Build-host-time invocation is deterministic and reproducible.** The console app reads `.g.cs`, applies transforms, writes back. The output is byte-stable; subsequent regeneration is idempotent. Roslyn SG runs in the consumer's compiler invocation — different consumers might see different outputs, breaking reproducibility.

## 12. Config Surface Evolution

The current `family-config.json` is the unified config surface. It did not start that way. This section captures the pre-Iteration-2 inventory + the classification matrix + the post-unification state, as durable Config Surface Evolution evidence.

### Pre-Iteration-2 inventory (18 distinct config sources)

Before Iteration 2's config-surface unification (closed 2026-05-27), binding-generator configuration was dispersed across **18 distinct sources** — 9 external files + 9 code-internal data structures:

**External files (9):**

1. `clangsharp/policy/opaque-handle-roster.json` — Core opaque-handle roster.
2. `clangsharp/policy/flags-enum-roster.json` — Core flags-enum allow-list.
3. `clangsharp/scope/sdl2-core.headers.txt` — Core header list.
4. `clangsharp/scope/sdl2-image.headers.txt` — Image header list.
5. `clangsharp/rsp/base.rsp` — cross-cutting RSP.
6. `clangsharp/rsp/sdl2-core.rsp` — Core family RSP.
7. `clangsharp/rsp/sdl2-image.rsp` — Image family RSP.
8. `clangsharp/rsp/per-header/<header>.rsp` — per-header RSPs (multiple files).
9. `clangsharp/sdl2-core-sdlh-required.json` — Core's SDL.h required surface.

**Code-internal data structures (9):**

10. `generate_bindings.py FAMILY_CONFIG` — per-family identity dict (Python).
11. `generate_bindings.py PLATFORM_SENSITIVE_HEADERS` — list of platform-sensitive header names.
12. `generate_bindings.py ALL_PLATFORM_MACROS` — full SDL2 platform macro enumeration.
13. `generate_bindings.py SDL2_PLATFORM_VIEWS` — per-OS platform view definitions.
14. `ClongDualDispatchRewriter.AffectedMethodNames` (C#) — Core's C `long` method names hardcoded.
15. `UniformOpaqueFamilyIdentity.cs` (C#) — family directory → family ID mapping (copy 1).
16. `UniformOpaqueOwnerMode.cs` (C#) — family directory → owner mode (copy 2).
17. `oracle.cs FamilyConfigs` (C#) — per-family identity dict (copy 3, in oracle).
18. `Program.cs ResolveFamilyFromOutputDir` (C#) — namespace → family ID mapping (copy 4).

Three risks from this dispersion:

- **Drift between source types.** Same fact in JSON + Python dict + C# enum runs out of sync.
- **Discoverability tax for new agents.** A new contributor needs to find each fact's home — 18 different searches.
- **Expansion tax for new families.** Adding TTF/Mixer/GFX would multiply mess (each new family adds ~3-4 new copies of family-directory mapping).

### Classification matrix (7-row config-owns-vs-code-owns)

Operationalizing the Constitution §"Manifest Configuration Vs Code-Owned Policy" boundary at row-level granularity:

| Concern | Config owns ("which") | Code owns ("how") |
| --- | --- | --- |
| Family identity | Which families exist; their namespaces; their raw class names; their library names; their owner mode | Family-name → C# namespace string-templating; raw-class-name suffix convention |
| Opaque handles | Which names per family (`auto_detect_well_known`, `force_opaque_exceptions`, `excluded_candidates`) | Pattern B struct template; field-position rewrite; cross-family pull mechanism |
| Flags enums | Which enum names per family (`allow_list`) | Suffix-rule detection; allow-list lookup; `[Flags]` attribute emission |
| C `long` | Which functions per family (`clong_methods`) | Hybrid emit shape (Compat dual-dispatch + Modern `CULong`); parameter-position rewrite; signed-vs-unsigned discriminator |
| Platform views | Which views exist; which OS each maps to; which preprocessor defines each view sets | Platform-merge algorithm; `[SupportedOSPlatform]` attribution; multi-view dedupe |
| Required surface | Which functions / constants are required (Core only) | Required-surface validation algorithm |
| Postprocess identity | Which family-directory paths map to which family IDs | Postprocess pipeline orchestration; rewriter implementation |

### Post-unification single source — `family-config.json` schema 1.0

After Iteration 2 closure (2026-05-27), the unified config lives at `spikes/binding-generators/clangsharp/config/family-config.json` with schema version 1.0. Five family entries: `core`, `image`, `ttf`, `mixer`, `gfx`. Schema shape (abridged):

```json
{
  "schema_version": "1.0",
  "last_modified": "...",
  "global": {
    "platform_views": [ /* per-OS view definitions */ ],
    "all_platform_macros": [ /* full SDL2 platform macro enumeration */ ]
  },
  "families": {
    "core": {
      "library_version": "2.32.10",
      "namespace": "SDL2",
      "raw_class": "SDLNative",
      "library_name": "SDL2",
      "owner_mode": true,
      "headers": [ /* ordered per-family header list */ ],
      "platform_sensitive_headers": [ "SDL_main.h", "SDL_system.h", ... ],
      "required_surface": { /* SDL.h required functions + constants */ },
      "opaque_handles": {
        "last_audited": "...",
        "auto_detect_well_known": [ /* 14 entries with wiki_url */ ],
        "force_opaque_exceptions": [ "SDL_RWops", "SDL_SysWMinfo", "SDL_SysWMmsg" ],
        "excluded_candidates": [ /* with rationale */ ]
      },
      "flags_enums": {
        "last_audited": "...",
        "allow_list": [ "SDL_Keymod", "SDL_BlendMode", ... ]
      },
      "clong_methods": [ "SDL_ThreadID", "SDL_GetThreadID" ]
    },
    "image": { /* similar shape, no clong_methods, no force_opaque, library_version 2.8.8 */ },
    "ttf":   { /* satellite-owned TTF_Font, clong_methods for 5 functions, library_version 2.24.0 */ },
    "mixer": { /* satellite-owned Mix_Music, library_version 2.8.1 */ },
    "gfx":   { /* no handles, no flags allow-list, library_version 1.0.4 */ }
  }
}
```

### Path migrations

The Iteration 2 unification produced these retirements:

- `clangsharp/policy/opaque-handle-roster.json` (retired) → `family-config.json families.<family>.opaque_handles`.
- `clangsharp/policy/flags-enum-roster.json` (retired) → `family-config.json families.<family>.flags_enums`.
- `clangsharp/scope/sdl2-<family>.headers.txt` (retired) → `family-config.json families.<family>.headers[]`.
- `sdl2-core-sdlh-required.json` retained as standalone (load-bearing for required-surface validation).
- `compare_oracle.py` retired (replaced by `oracle.cs`).

The 4-copy family-directory mapping collapsed to a single `families.<family>.namespace` + `families.<family>.owner_mode` lookup; `UniformOpaqueFamilyIdentity.cs` and `UniformOpaqueOwnerMode.cs` retain substring-based fallback as safety net only.

### What did not move

Per Iteration 2 §2.2 inventory, these stayed outside `family-config.json`:

- **`vcpkg.json` + overlay triplets** — vcpkg native input. Orthogonal to binding-generator scope.
- **`build/manifest.json`** — production manifest. Spike does not touch it per spike isolation; production flip absorbs `family-config.json` into the manifest schema.
- **`dotnet-tools.json`** — tool pinning (ClangSharp tool version).
- **`Directory.Packages.props`** — Central Package Management.
- **Per-header RSP contents** — filesystem data, byte-exact match required.
- **csproj TFM patterns** — build infrastructure.
- **Postprocess rewriter logic** — code-owned policy mechanism per Constitution.

### Rejected alternatives (during Iteration 2)

The Iteration 2 spec rejected four alternatives, codifying the why-this-design rationale:

- **Minimal keep-layout** (just rename existing files) — documentation cannot fix maintainability bugs.
- **Keep separate roster JSONs** — discoverability tax of 6+ files persists.
- **Flatten roster metadata into string arrays** — audit traceability requires full object shape (wiki_url, last_audited).
- **Put `clong_type_names` in config** — rejected because that would put ABI knowledge in config (Constitution §"Configurable Scope Vs Policy Mechanism" forbids JSON encoding *how* a transform operates). The function-name list (`clong_methods`) is config-appropriate ("which symbols"); the C `long` classification rule itself stays in code.

### Single-source-of-truth rule

After unification: **if a fact appears in both config and code, config is authoritative and the code copy is a bug.** The orchestrator and postprocess both read `family-config.json` directly — no parallel copies, no per-consumer mirror dicts. The legacy C# substring-based family-detection helpers remain as fallback only; the config-driven contract is the load-bearing path.

## 13. Postprocess Pipeline Mechanism Detail

The postprocess pipeline is a 7-step sequence that runs **per family** against that family's `Generated/{Compat,Modern}/` tree. Each step targets one concept; no rewriter accumulates multiple unrelated transforms.

### Pipeline order

```
1. platform-delta        (multi-OS dedupe + guarded [SupportedOSPlatform])
2. strip-varargs         (Constitution fmt-only enforcement for C variadics)
3. libraryimport         (Modern only — [DllImport] → [LibraryImport] + [UnmanagedCallConv])
4. flags-detect          (suffix + allow-list emission of [Flags])
5. guid-substitute       (SDL_GUID → System.Guid)
6. clong-dispatch        (C long hybrid emit — Compat dual-dispatch + Modern CULong)
7. uniform-opaque        (Pattern B by-value handle emit + 3-position rewrite)
```

### Per-step rationale

1. **platform-delta.** Runs first because subsequent rewriters must operate on the deduplicated platform-merged tree. Multi-OS pass produces multiple platform views; this step dedupes Neutral-equivalent declarations and attaches guarded `[SupportedOSPlatform("<os>")]` attributes to platform-only declarations.
2. **strip-varargs.** Enforces Constitution §"C Variadics" fmt-only mapping. Strips `params object[]` shapes that ClangSharp emits for C ellipsis functions, leaving the fixed-prefix `byte* fmt` form.
3. **libraryimport (Modern only).** Transforms `[DllImport]` declarations into `[LibraryImport]` + `[UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]`. Runs only against the Modern backend tree; Compat sources keep `[DllImport]`. This is the only step in the pipeline that is Modern-only — all others operate on both backends.
4. **flags-detect.** Adds `[Flags]` to qualifying enums per §8. Runs after `libraryimport` and before `guid-substitute` for readability (enum-only, no compositional constraint — ordering is for legibility, not correctness).
5. **guid-substitute.** Rewrites `SDL_GUID` annotations to `System.Guid` per §6. Runs before `clong-dispatch` because `clong-dispatch` may emit method signatures that reference `Guid` (none currently, but the ordering is defensive).
6. **clong-dispatch.** Emits the C `long` hybrid per §4. Runs before `uniform-opaque` because some C `long` methods take or return opaque-handle types; uniform-opaque expects already-rewritten method signatures.
7. **uniform-opaque.** Emits Pattern B handle structs per §3 + rewrites all three pointer positions (parameter / return / struct field) to by-value. Runs last because it touches the most surface area and depends on every other rewriter's output being final.

### Compat vs Modern

- `libraryimport` is **Modern-only** — runs against `Generated/Modern/` tree only.
- All other steps operate on **both** Compat and Modern trees. For example, `clong-dispatch` rewrites the Compat tree to use `RuntimeInformation.IsOSPlatform`-dispatched `[DllImport]` helpers and rewrites the Modern tree to use `CULong` + `LibraryImport`.

### Scope-bounded discipline

Each rewriter has a single responsibility — one concept, no accumulation:

- `PlatformDeltaPostProcessor` — multi-OS dedupe + platform attribution. Nothing else.
- `StripVarargsRewriter` — C variadic fmt-only enforcement. Nothing else.
- `DllImportToLibraryImportRewriter` — `[DllImport]` → `[LibraryImport]` (Modern). Nothing else.
- `FlagsAttributeRewriter` — `[Flags]` emission via suffix + allow-list. Nothing else.
- `GuidSubstitutionRewriter` — `SDL_GUID` → `System.Guid`. Nothing else.
- `ClongDualDispatchRewriter` — C `long` hybrid emit. Nothing else.
- `OpaqueHandleEmitRewriter` — Pattern B emit + 3-position pointer rewrite. Nothing else.

When a new transform is needed, it gets a new rewriter class — never bolted onto an existing one. This keeps each rewriter's self-test scope narrow and the pipeline understandable.

### Self-test discipline

Each rewriter has self-tests in the postprocess project (`spikes/binding-generators/clangsharp/postprocess/PostProcessSelfTests.cs`). Run via:

```pwsh
dotnet run --project spikes/binding-generators/clangsharp/postprocess/Janset.SDL2.PostProcess.csproj -c Release -- self-test
```

Self-tests cover at minimum: empty-input idempotence, single-target transform correctness, trivia preservation (comments, blank lines), no-mutation on non-matching input, family-allow-list wiring (where applicable). New rewriters land with self-tests in the same commit.

### Per-family invocation

`generate_bindings.py` invokes each step per family via `run_postprocess(family, ...)`. The orchestrator selects which families' Generated trees to process based on `--family <X>` or `--family all`. Per-family invocation means dormant families are skipped entirely (no parse, no emit, no postprocess) — Constitution §"Generation Determinism Contract" → "Disabled-family side-effect isolation" applies at the postprocess boundary too.

### Standalone console app, not Roslyn Source Generator

Per §11, the postprocess project is a standalone .NET console app. The CLI entry point (`PostProcessCli.cs`) dispatches each step by mode key:

```pwsh
dotnet run --project spikes/binding-generators/clangsharp/postprocess/Janset.SDL2.PostProcess.csproj -c Release -- \
    uniform-opaque --family core --input-dir ../src/Janset.SDL2.Core/Generated --config ../config/family-config.json
```

The `generate_bindings.py` orchestrator wires up the per-step invocation chain; humans rarely invoke a single mode by hand except during self-test or debugging a specific rewriter.

## References

- [`binding-generator-constitution.md`](binding-generator-constitution.md) — pure policy contract. This document is its mechanism companion.
- [`binding-generator-maintenance.md`](binding-generator-maintenance.md) — version-bump procedures, RSP file maintenance, family-config.json schema maintenance, new-family checklist.
- [`binding-output-oracle-validation.md`](binding-output-oracle-validation.md) — multi-oracle review methodology; relationship to §10 oracle.cs classification labels.
- [ADR-004 — Binding Auto-Generation Toolchain](../../../docs/decisions/2026-05-14-binding-autogen-toolchain.md) — Reopened; formal amendment deferred pending Layer 2 closure per design-spec Q13.
- [`docs/knowledge-base/extraction-guidelines.md`](../../../docs/knowledge-base/extraction-guidelines.md) — collaborator extraction discipline; applies to rewriter class extraction patterns.
- [`docs/knowledge-base/testing-guidelines.md`](../../../docs/knowledge-base/testing-guidelines.md) — canonical TUnit/MTP test infrastructure; postprocess self-tests defer to this.
- **Postprocess source code:** `spikes/binding-generators/clangsharp/postprocess/` — canonical authority for rewriter behavior. Classes cited in this document: `OpaqueHandleEmitRewriter`, `ClongDualDispatchRewriter`, `DllImportToLibraryImportRewriter`, `FlagsAttributeRewriter`, `GuidSubstitutionRewriter`, `StripVarargsRewriter`, `PlatformDeltaPostProcessor`, `UniformOpaqueFamilyIdentity`, `UniformOpaqueOwnerMode`.
- **Unified config:** `spikes/binding-generators/clangsharp/config/family-config.json` — canonical authority for per-family identity, opaque handles, flags enums, C long methods, platform views, header inventories, required surface, owner mode.
- **Generator orchestrator:** `spikes/binding-generators/clangsharp/generate_bindings.py` — canonical authority for pipeline orchestration, family selection, postprocess invocation chain.
- **Oracle app:** `spikes/binding-generators/clangsharp/oracle.cs` — canonical authority for §10 classification label emission and raw ABI check IDs.
