# Layer 2 Core Foundation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Generate the public typed low-level (Layer 2) API for SDL2.Core — public `SDL` methods + constants/properties forwarding to the internal `SDLNative` raw ABI — via **family-blind** postprocess rewriters that already handle every satellite's cases (config-driven), validated by self-tests carrying satellite-case fixtures (incl. a real **compile-check** of the emitted output) before any satellite is wired. The existing ABI test project under `spikes/binding-generators/clangsharp/tests/abi-tests/` remains the runtime ABI harness; this slice extends it with a small public-L2 smoke instead of creating a second runtime-test lane.

**Architecture:** Two new Roslyn postprocess passes in `Janset.SDL2.PostProcess`, modeled on `OpaqueHandleEmitRewriter` (discover→build→emit) and `FlagsAttributeRewriter` (in-place decoration). `PublicTypedMethodProjection` discovers `internal SDLNative` methods + public macro/value members (reading `Generated/Modern`) and emits a **single TFM-shared** `public partial class {public_class}` into `Generated/Public/`; `StructLayoutSequentialRewriter` prepends `[StructLayout(LayoutKind.Sequential)]` to every layout-free struct (universal, all families/codegens, in-place). Family facts (`public_class`, `raw_class`, `clong_methods`) from `family-config.json`; mechanism code-owned. Tested via `PostProcessSelfTests` (`--self-test`) + a Verify public-API snapshot + a small public-L2 smoke through the existing ABI test project.

**Design decision (2026-05-30, Deniz) — SINGLE public surface, pragma-separated:** the public layer is ONE TFM-shared file with **uniform signatures across all 5 TFMs**; per-TFM divergences are handled by `#if` in method bodies + **verbatim copy of the raw member's attribute trivia** (which already carries `#if NET5_0_OR_GREATER` guards) — NOT a TFM-split of the public layer. Revisit only if the divergent method count becomes unmanageable; current divergences (callback function-pointer shapes + ~35 platform-attributed + few clong) are manageable as generated `#if` blocks. Concretely: callback-shaped public types expose uniform `nint`/`nint*` (`#if`-cast to `delegate*` shapes only on Modern; typed-callback ergonomics → L3); clong methods expose uniform `long`/`ulong` (`#if`-normalized body); platform/obsolete attributes are copied verbatim with their guard trivia.

**Tech Stack:** C# / .NET 10 console (postprocess), Microsoft.CodeAnalysis.CSharp (Roslyn — incl. `CSharpCompilation` for the compile-check fixture), TUnit + Verify + PublicApiGenerator (snapshot), `generate_bindings.py` (orchestrator). Generated source compiles across `net10.0/net9.0/net8.0/netstandard2.0/net462`.

**Scope:** SDL2.Core, Layer 2 method forwarders + public object-like constants + public string-like macro properties + universal struct-layout + clong (both paths) + public-API snapshot + a minimal public-L2 ABI smoke. **Out of scope (later plans, see spec §1 reconciliation in Deferred):** TypedEnumProjection at `int`-flag positions, StructFieldAccessorProjection, function-like MacroHelperGeneration, EndianRuntimeAlias, ErrorRedirect, satellite activation, Mixer callbacks, typed-delegate callback ergonomics. Runtime/endian-dependent constants (`SDL_BYTEORDER`, `SDL_FLOATWORDORDER`, `AUDIO_*SYS`, and the known endian alias set) are **not** emitted as public `const` in this slice; they stay deferred until `EndianRuntimeAlias` can publish the intended runtime-aware shape.

**Review status:** Two independent expert reviews (unbiased P/Invoke + context-full peer-benchmarked, 2026-05-30) → **proceed fixes-first**; architecture + ratified decisions ecosystem-validated (internal-raw+forwarder, `[NativeTypeName]`-drop, clong dual-path, deterministic sort, snapshot all best-in-class vs CsWin32 / ClangSharp / TerraFX / csbindgen). **All must-fixes from both reviews are integrated into the tasks below.** Remaining should-fixes are in "Deferred review findings."

---

## Deferred review findings (to be revisited)

- **(should-fix) Cross-namespace satellite fixture** — when satellites activate, add a self-test where a satellite raw method returns/takes a Core-owned `SDL_Surface*` / by-value `SDL_Renderer` in `SDL2.Image`, asserting the Core type emits **unqualified** with no `using SDL2;` (spec §10B-4). Forward-looking.
- **(spec reconciliation) Macro/endian deferral** — spec §1 lists Type-A macros + endian aliases in the Core slice; this plan defers them (ordinary object-like constants and string-like macro properties are re-emitted in Task 3; function-like macros + endian runtime-aliases are later slices). Update spec §1 to match in the Layer 2 changeset (Constitution Maintenance Rule).
- **(L3, in `next-iteration-plan.md` Forward Backlog) `SDL_bool` bool affordance** — D6 keeps the typed enum at L2; revisit at L3 with a non-overloading `bool ToBoolean(this SDL_bool)` / `IsTrue`.
- **(do-now from 2026-05-31 review) Public projection corrections** — before implementation, fold in the parallel review findings: callback type-shape classification (`delegate*`, `delegate* *`, callback returns), guarded platform-attribute usings, public macro member classification, Core-only orchestrator activation, stronger attribute filtering, and public-L2 ABI smoke through the existing ABI test project.

---

## File structure

**Create:**
- `postprocess/PublicTypedMethodProjection.cs` — static `Discover`/`DiscoverFromSource` (methods + public macro/value members of the raw class only) + `BuildPublicClassContent` + emit, mirroring `OpaqueHandleEmitRewriter`. Public macro/value members include ordinary object-like `public const` fields and string-like `public static ReadOnlySpan<byte>` properties; known runtime/endian-dependent constants are suppressed until the `EndianRuntimeAlias` slice.
- `postprocess/StructLayoutSequentialRewriter.cs` — universal `[StructLayout(LayoutKind.Sequential)]`, mirroring `FlagsAttributeRewriter`.
- Generated, committed: `src/Janset.SDL2.Core/Generated/Public/SDL.g.cs` (named after `public_class`; single TFM-shared; unconditional csproj include).
- `tests/api-snapshot/ApiSnapshot.csproj` + `CoreApiSnapshotTests.cs` + `*.verified.txt`.

**Modify:**
- `postprocess/Config/FamilyConfig.cs` — add `PublicClass(family)` + `RawClass(family)`.
- `config/family-config.json` — add `"public_class"` to all 5 families.
- `postprocess/Program.cs` — add `public-typed` (early-return, emits to `Generated/Public`) + `struct-layout` (switch case) modes.
- `postprocess/PostProcessSelfTests.cs` — add `CheckStructLayoutSequential` + `CheckPublicTypedMethodProjection` (incl. the compile-check fixture), wired into `Run()`.
- `src/Janset.SDL2.Core/Janset.SDL2.Core.csproj` — add unconditional `<Compile Include="Generated/Public/**/*.cs" />` (correct because the surface is uniform across TFMs).
- `generate_bindings.py` — add `struct-layout` (both codegens, after uniform-opaque) + `public-typed` (Modern only → `Generated/Public`) steps.
- `tests/abi-tests/` — extend the existing ABI test project with a tiny public-L2 smoke after `Generated/Public/SDL.g.cs` exists; do not create a second runtime ABI project.
- Constitution/spec/resume docs — reconcile the foundation slice with the reviewed scope: macro/endian deferral, callback shape handling, public macro member projection, and Core-only activation.

---

## Task 1: `public_class` / `raw_class` config + readers

**Files:** Modify `config/family-config.json`, `postprocess/Config/FamilyConfig.cs`; Test `postprocess/PostProcessSelfTests.cs`.

- [ ] **Step 1: Add `"public_class"` to all 5 families** (alongside the existing `raw_class`, which all 5 already have): core→`SDL`, image→`SDL_image`, ttf→`SDL_ttf`, mixer→`SDL_mixer`, gfx→`SDL2_gfx`.
- [ ] **Step 2: Failing self-test** `CheckFamilyClassConfig` (call from `Run()`): assert `PublicClass("core")=="SDL"`, `RawClass("core")=="SDLNative"`, `PublicClass("gfx")=="SDL2_gfx"`.
- [ ] **Step 3: Run → FAIL.** `dotnet run --project spikes/binding-generators/clangsharp/postprocess -- --self-test`.
- [ ] **Step 4: Add readers** to `FamilyConfig.cs`: `public string PublicClass(string family) => Family(family).GetProperty("public_class").GetString()!;` and `public string RawClass(string family) => Family(family).GetProperty("raw_class").GetString()!;`.
- [ ] **Step 5: Run → `self-test: PASS`.**
- [ ] **Step 6: Commit (WIP).** `git commit -m "feat(bindings): add public_class config + FamilyConfig readers for Layer 2"`.

---

## Task 2: `StructLayoutSequentialRewriter` (universal pass)

(Unchanged from prior plan — sound per both reviews.) Prepend `[StructLayout(LayoutKind.Sequential)]` to every `struct` lacking any `[StructLayout]`; ensure `using System.Runtime.InteropServices;`. Universal across families/codegens; unions (`Explicit`) + Pattern B handles (already `Sequential`) skipped by the `HasStructLayout` short-circuit.

- [ ] **Step 1: Failing self-tests** `CheckStructLayoutSequential`: (a) bare POD struct → gains the attribute + using; (b) struct with `[StructLayout(LayoutKind.Explicit)]` untouched (exactly one `LayoutKind.`, no `Sequential`); (c) `readonly partial struct` with existing `[StructLayout(LayoutKind.Sequential)]` (Pattern B handle) not double-decorated.
- [ ] **Step 2: Run → FAIL.**
- [ ] **Step 3: Implement** `StructLayoutSequentialRewriter` (`CSharpSyntaxRewriter`, `AnyChanges`/`Reset`; `VisitStructDeclaration` prepends if no `[StructLayout]`, preserving indentation via the `FlagsAttributeRewriter.ExtractIndentation` pattern + `_needsUsing` flag; `VisitCompilationUnit` adds the using if missing).
- [ ] **Step 4: Wire `struct-layout` mode** in `Program.cs` (validation tuple + switch case).
- [ ] **Step 5: Run → PASS.**
- [ ] **Step 6: Commit (WIP).** `git commit -m "feat(bindings): universal StructLayout(Sequential) postprocess pass"`.

---

## Task 3: `PublicTypedMethodProjection` (forwarders + public macro members + clong + divergence handling)

The core pass. Discovers, from `Generated/Modern`, the `internal ... partial class {raw_class}`'s **methods AND public macro/value members** (only members of the raw class — ignore namespace-level enums/delegates/handle structs), and emits ONE TFM-shared `public static unsafe partial class {public_class}` into `Generated/Public/{public_class}.g.cs`.

Public macro/value member classification is part of this task, not a later cleanup:

- **ordinary object-like constants** → re-emit as `public const` after dropping raw provenance attributes;
- **string-like SDL macro keys** (for example `SDL_HINT_*`) → re-emit as `public static ReadOnlySpan<byte>` UTF-8 literal properties, preserving the Constitution/AGENTS decision that these are the canonical public shape;
- **runtime/endian-dependent constants** (`SDL_BYTEORDER`, `SDL_FLOATWORDORDER`, `AUDIO_*SYS`, and the audited endian alias set) → suppress/defer in this slice so the later `EndianRuntimeAlias` pass owns their first public shape. Do **not** snapshot them as public `const` values.

**Integrated must-fixes (both reviews):**
- **[blocker → fixed] callback type-shape Modern/Compat divergence.** Core has direct function-pointer parameters, pointer-to-function-pointer output parameters, and function-pointer returns (for example `SDL_SetEventFilter`, `SDL_GetEventFilter`, `SDL_GetDefaultAssertionHandler`). Modern uses `delegate* unmanaged[Cdecl]<…>` shapes; Compat uses `IntPtr` / `IntPtr*`. Per the single-surface decision: public callback-shaped types are uniform `nint`/`nint*` on all TFMs; bodies use `#if NET6_0_OR_GREATER` casts back to the original Modern `delegate*` shape and `#else` forwards the `nint`/`nint*` value directly to Compat. `delegate*` must never appear in public signatures or legacy-TFM-active code. (Typed-callback ergonomics and lifetime-safe wrappers are deferred to L3 / callback policy.)
- **[blocker → fixed] `[SupportedOSPlatform]` legacy-TFM break.** The attribute is .NET 5+ only (absent on netstandard2.0/net462, no polyfill); emitting it unconditionally breaks the legacy compile and contradicts roadmap §"Layer 2" ("same platform attribution as the raw ABI member"). Fix: **copy each raw member's attribute lists VERBATIM with their original leading/trailing trivia** — the trivia already carries the `#if NET5_0_OR_GREATER`/`#endif` directives AND all 5 platform values (`windows`, `android`, `ios`, `linux`, `windows10.0.10240.0`). Drop ONLY `[NativeTypeName]`, `[LibraryImport]`/`[DllImport]`, `[UnmanagedCallConv]`. This also preserves `[Obsolete]` (e.g. `SDL_GetRevisionNumber`) and any `[UnsupportedOSPlatform]`.
- **[major → fixed] clong input truncation on Unix.** Public param is `long`/`ulong`; forward `new CLong((nint)p)` / `new CULong((nuint)p)` — **NOT `(int)p`** (`(int)` truncates a valid 64-bit value on Unix x64 where `CLong.Value` is `nint`/64-bit). The `ThrowIfWindowsOutOfRange(long)` helper throws `ArgumentOutOfRangeException` only when `RuntimeInformation.IsOSPlatform(OSPlatform.Windows)` AND `value < int.MinValue || value > int.MaxValue` (fast non-Windows return; the native Windows `long` is 32-bit). Returns widen losslessly (no check): `#if NET6_0_OR_GREATER => (long|ulong){raw}.{name}(…).Value` `#else => {raw}.{name}(…)` (Compat raw already normalized).
- **[major → fixed] public macro/value member projection.** Re-emit ordinary raw-class `public const` fields onto the public class (~130–155 object-like consts like `SDL_ALPHA_OPAQUE`, `SDL_RWOPS_*`) after filtering raw provenance attributes. Also re-emit string-like `ReadOnlySpan<byte>` macro properties (`SDL_HINT_*` and similar) so callers can use public `SDL.SDL_SetHint` without reaching internal `SDLNative`. Function-like macros stay deferred. Runtime/endian-dependent values stay deferred to `EndianRuntimeAlias` and are not emitted as public `const` in this slice.
- **[do-now → fixed] determinism.** Sort discovered members (consts then methods, each by name+signature) before emit — `Directory.EnumerateFiles` order is filesystem-dependent (mirror `BuildHandlesFileContent`).
- **`[NativeTypeName]` dropped** on the public surface (ABI provenance; keep on raw only — best-in-class per CsWin32/TerraFX/ClangSharp).

**Files:** Create `postprocess/PublicTypedMethodProjection.cs`; Modify `postprocess/Program.cs`, `postprocess/PostProcessSelfTests.cs`.

- [ ] **Step 1: Write failing self-tests** `CheckPublicTypedMethodProjection` (fixtures = behavioral spec). Cover, with `string.Contains` assertions on `BuildPublicClassContent` output:
  - (a) **plain forwarder** — `byte* SDL_GetError()` and `int SDL_LockSurface(SDL_Surface* surface)` forward 1:1; class is `public static unsafe partial class SDL`; output contains **no** `NativeTypeName`.
  - (b) **clong signed return** — `CLong TTF_FontFaces(TTF_Font)` → `public static long TTF_FontFaces(TTF_Font font)` with `#if NET6_0_OR_GREATER` and `(long)SDL_ttfNative.TTF_FontFaces(font).Value`.
  - (c) **clong unsigned return** — `CULong SDL_ThreadID()` → `public static ulong SDL_ThreadID()` with `(ulong)SDLNative.SDL_ThreadID().Value`.
  - (d) **clong input** — `CLong index` param → `public static ... (..., long index)`; body contains `ThrowIfWindowsOutOfRange(index)` AND `new CLong((nint)index)` and **does NOT contain** `(int)index` (assert the absence — the Unix-truncation guard).
  - (e) **platform attribute trivia** — raw method with `#if NET5_0_OR_GREATER\n[SupportedOSPlatform("android")]\n#endif` → output contains that exact `#if`-guarded `[SupportedOSPlatform("android")]` block verbatim (assert the `#if NET5_0_OR_GREATER` and the `android` value both survive).
  - (f) **`[Obsolete]` preserved** — raw method with `[Obsolete("…")]` → forwarder keeps it.
  - (g) **callback direct-param uniform nint** — raw `void SDL_SetEventFilter(delegate* unmanaged[Cdecl]<nint, SDL_Event*, int> filter, nint userdata)` → public `void SDL_SetEventFilter(nint filter, nint userdata)` with `#if NET6_0_OR_GREATER` containing `(delegate* unmanaged[Cdecl]<nint, SDL_Event*, int>)filter` and `#else` forwarding `filter` directly; the public **signature** must NOT contain `delegate*`.
  - (h) **callback pointer-to-function-pointer uniform nint pointer** — raw `SDL_bool SDL_GetEventFilter(delegate* unmanaged[Cdecl]<nint, SDL_Event*, int>* filter, nint* userdata)` → public `SDL_bool SDL_GetEventFilter(nint* filter, nint* userdata)`; Modern body casts `filter` to the original `delegate* ...*` shape, Compat forwards `filter` directly.
  - (i) **callback return uniform nint** — raw `delegate* unmanaged[Cdecl]<SDL_AssertData*, void*, SDL_AssertState> SDL_GetDefaultAssertionHandler()` → public `nint SDL_GetDefaultAssertionHandler()`; Modern body casts the raw return to `nint`, Compat returns `IntPtr`/`nint` without `delegate*` in the legacy-active branch.
  - (j) **public const re-emit** — raw class with `[NativeTypeName("#define SDL_ALPHA_OPAQUE 255")] public const int SDL_ALPHA_OPAQUE = 255;` → public class contains `public const int SDL_ALPHA_OPAQUE = 255;` and does not contain `NativeTypeName`.
  - (k) **string-like macro property re-emit** — raw class with `public static ReadOnlySpan<byte> SDL_HINT_RENDER_SCALE_QUALITY => "SDL_RENDER_SCALE_QUALITY"u8;` → public class contains the same public property shape.
  - (l) **endian-dependent const suppression** — raw class with `public const int SDL_BYTEORDER = 1234;` / `public const ushort AUDIO_S16SYS = ...;` → public class does not contain those names; they remain deferred to `EndianRuntimeAlias`.
  - (m) **Discover filters to raw class** — a file with a namespace-level `public enum E {}` + `public delegate void D();` + the raw class with one method → only the method (not E/D) is projected.
  - (n) **deterministic order** — `SDL_Zebra` + `SDL_Apple` emit alphabetically regardless of source order.
  - (o) **no-op** — empty raw class → still emits `public static unsafe partial class SDL` (with `unsafe`), no throw.
  - (p) **cross-family compile fixture** — before satellites are wired, compile a satellite-shaped fixture where `namespace SDL2.Image` public projection consumes Core-owned `SDL_Renderer` by value and `SDL_Surface*` by pointer from `namespace SDL2` without `using SDL2;`. This guards the family-blind type-resolution invariant while keeping real satellite emission gated to a later slice.

- [ ] **Step 2: Write the COMPILE-CHECK fixture** `CheckPublicProjectionCompiles` (the single highest-value test — catches the divergence blockers in self-test, not at build time). Build the emitted public class text + a **Modern-shaped** stub raw class (using direct `delegate*` params, `delegate* ...*` params, `delegate*` returns, `CLong`/`CULong`, guarded `[SupportedOSPlatform]`, public consts, and string-like macro properties) into a `CSharpCompilation` targeting net8-style refs and assert zero errors; then build the same public text against a **Compat-shaped** stub raw class (using `IntPtr`, `IntPtr*`, `long`/`ulong`, no platform attributes and no function pointers) with legacy-style refs and assert zero errors. Use `CSharpCompilation.Create(...).GetDiagnostics()` filtered to `Error`. (Roslyn is already referenced by the postprocess project.)

```csharp
private static void CheckPublicProjectionCompiles(List<string> failures)
{
    // Emit the public class from a fixture that exercises every divergence
    // (clong, callback direct/pointer/return shapes, platform attr, const, string-like property).
    var rawModern = /* fixture: internal SDLNative with delegate* param, CULong return, [SupportedOSPlatform] (#if-guarded), public const */;
    var publicText = PublicTypedMethodProjection.BuildPublicClassContent(
        PublicTypedMethodProjection.DiscoverFromSource(rawModern), "SDL", "SDLNative", "SDL2",
        new HashSet<string> { "SDL_ThreadID" });

    AssertCompiles("Modern", publicText, ModernStubRawClass(), netstandard: false, failures);
    AssertCompiles("Compat", publicText, CompatStubRawClass(), netstandard: true,  failures);
}
// AssertCompiles: CSharpCompilation.Create with the two sources + appropriate MetadataReferences
// (collect via AppContext.BaseDirectory / typeof(object).Assembly etc.), define NET6_0_OR_GREATER
// only for the Modern compile, and fail on any Error diagnostic.
```

- [ ] **Step 3: Run → FAIL** (`PublicTypedMethodProjection` undefined).

- [ ] **Step 4: Implement `PublicTypedMethodProjection.cs`.** `DiscoverFromSource(string)` / `Discover(inputDir)` parse `*.g.cs`, collect from the `{raw_class}` declaration only: each `MethodDeclarationSyntax` (name, return type, params with attribute lists, return attributes, and method attribute lists), public object-like `const` `FieldDeclarationSyntax`, and public string-like `ReadOnlySpan<byte>` `PropertyDeclarationSyntax`. `BuildPublicClassContent(members, publicClass, rawClass, namespaceName, clongMethods)`:
  - Sort: constants/properties first (by name), then methods (by name, then full signature) — deterministic.
  - Emit `// <auto-generated>` header + fixed usings + `namespace` + `public static unsafe partial class {publicClass}` (mirror `BuildHandlesFileContent`). Fixed usings include `using System;`, `using System.Runtime.InteropServices;`, and guarded `#if NET5_0_OR_GREATER using System.Runtime.Versioning; #endif` when any copied platform attribute requires it.
  - **Attributes:** for each method, return, parameter, and public macro/value member, copy attribute lists **verbatim with trivia** EXCEPT those named `NativeTypeName`/`LibraryImport`/`DllImport`/`UnmanagedCallConv` (drop). This carries `#if`-guarded `[SupportedOSPlatform]`/`[UnsupportedOSPlatform]` + `[Obsolete]` intact.
  - **const:** re-emit declaration only (`public const {type} {name} = {value};`), after dropping `[NativeTypeName]`. Suppress known runtime/endian-dependent names so they do not become public `const`.
  - **string-like macro property:** re-emit `public static ReadOnlySpan<byte> Name => "..."u8;` after dropping raw provenance attributes.
  - **non-clong, non-delegate* method:** `public static {ret} {name}({params}) => {rawClass}.{name}({args});` (drop `[NativeTypeName]` from params/return).
  - **callback-shaped method:** public direct function-pointer type → `nint`; pointer-to-function-pointer → `nint*`; function-pointer return → `nint`. Body uses `#if NET6_0_OR_GREATER` casts to/from the original Modern `delegate*` shape and `#else` forwards `nint`/`nint*` directly. No public signature or legacy-active branch may contain `delegate*`.
  - **clong method** (name ∈ `clongMethods`): public uses `long`/`ulong` (signed from `CLong`, unsigned from `CULong`); body `#if NET6_0_OR_GREATER` (return: `(long|ulong){rawClass}.{name}(args).Value`; input param: `ThrowIfWindowsOutOfRange(p); … new CLong((nint)p) / new CULong((nuint)p) …`) `#else` (`{rawClass}.{name}(args)`).
  - Emit private range helpers once per file when needed: `ThrowIfWindowsOutOfRange(long value)` for signed C `long` inputs (`int.MinValue..int.MaxValue` on Windows only), and `ThrowIfWindowsOutOfRange(ulong value)` for any future unsigned C `unsigned long` inputs (`<= uint.MaxValue` on Windows only). If no unsigned-long input exists in current SDL2 families, self-tests must document the no-op path or fixture the future shape explicitly.
  - Author the emitter against the Step-1/2 expected output (the self-tests + compile-check ARE the spec).

- [ ] **Step 5: Wire `public-typed` mode** in `Program.cs` (validation tuple + early-return block after `platform-delta`). Derive `Generated/Public` from the `Generated/Modern` input parent; **resolve the family from `inputDir`** (guaranteed to exist; lexical resolution) not the not-yet-created `publicDir`:

```csharp
if (mode == "public-typed")
{
    var family = ResolveFamilyFromOutputDir(inputDir, familyConfig); // inputDir = .../Generated/Modern (exists)
    var publicDir = Path.Combine(Path.GetDirectoryName(inputDir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))!, "Public");
    var members = PublicTypedMethodProjection.Discover(inputDir);
    var content = PublicTypedMethodProjection.BuildPublicClassContent(
        members, familyConfig.PublicClass(family), familyConfig.RawClass(family),
        familyConfig.Namespace(family), new HashSet<string>(familyConfig.ClongMethods(family), StringComparer.Ordinal));
    Directory.CreateDirectory(publicDir);
    var dest = Path.Combine(publicDir, familyConfig.PublicClass(family) + ".g.cs");
    PostProcessCli.WriteAllTextLf(dest, content);
    Console.WriteLine($"public-typed: family={family}, {members.Count} members -> {dest}");
    return 0;
}
```

- [ ] **Step 6: Run → `self-test: PASS`** (all projection fixtures + the Modern+Compat compile-check).

- [ ] **Step 7: Commit (WIP).** `git commit -m "feat(bindings): Layer 2 PublicTypedMethodProjection (forwarders, macro members, clong, callback/attr divergence, compile-checked)"`.

---

## Task 4: Orchestrator + csproj wiring; generate real Core output

**Files:** Modify `generate_bindings.py`, `Janset.SDL2.Core.csproj`; Create (generated) `Generated/Public/SDL.g.cs`.

- [ ] **Step 1: Add the unconditional shared-public csproj include** to `Janset.SDL2.Core.csproj` (`<Compile Include="Generated/Public/**/*.cs" />`) — correct because the public surface is uniform across TFMs (divergences are `#if`-guarded inside it).
- [ ] **Step 2: Wire the orchestrator** in `generate_bindings.py`, inserting **immediately after the `uniform-opaque` loop** (≈ line 2052, before `refresh_generated_file_counts`). Ordering matters: `public-typed` must be the **last** read of Modern signatures (after platform-delta + libraryimport + clong-dispatch + uniform-opaque), so it sees final shapes and de-duplicated platform methods. `struct-layout` runs over both codegens (reuses `run_postprocess`). `public-typed` runs for **Core only** in this foundation slice, even when `--family all` is selected; satellite public output is activated later by config/slice, not accidentally by this shared loop.

```python
# Layer 2: universal struct-layout (both codegens, after uniform-opaque so Pattern B
# handles already carry [StructLayout] and are skipped) + public-typed projection
# (Modern only -> Generated/Public/{public_class}.g.cs, single TFM-shared file).
if args.execute:
    print("--- postprocess: struct-layout (all codegens) ---")
    for codegen in codegen_passes:
        for family in selected:
            exit_code = run_postprocess(repo, family, spike_root, "struct-layout", codegen)
            if exit_code != 0:
                postprocess_failures += 1
                print(f"WARNING: struct-layout postprocess for {family}/{codegen} returned exit {exit_code}")

    print("--- postprocess: public-typed (Core Modern -> Generated/Public) ---")
    for family in selected:
        if family != "core":
            continue
        exit_code = run_postprocess(repo, family, spike_root, "public-typed", "modern")
        if exit_code != 0:
            postprocess_failures += 1
            print(f"WARNING: public-typed postprocess for {family} returned exit {exit_code}")
```

- [ ] **Step 3: Regenerate Core + idempotency.** Run generator for Core; run twice and diff → byte-identical. Verify `Generated/Public/SDL.g.cs` has `public static unsafe partial class SDL`; `SDL_ThreadID` → `ulong` (`#if`-guarded); `SDL_SetEventFilter(nint filter, …)`, `SDL_GetEventFilter(nint* filter, …)`, and `SDL_GetDefaultAssertionHandler()` returning `nint` (uniform, `#if`-cast bodies); `[SupportedOSPlatform]` forwarders carry their `#if NET5_0_OR_GREATER` guard and the file has the guarded `System.Runtime.Versioning` using; `public const SDL_ALPHA_OPAQUE`; public `ReadOnlySpan<byte>` `SDL_HINT_*` properties; no `[NativeTypeName]`; no public `SDL_BYTEORDER` / `AUDIO_*SYS` consts.
- [ ] **Step 4: Compile-check all 5 TFMs.** `dotnet build .../Janset.SDL2.Core.csproj -c Release` → clean on `net10.0/net9.0/net8.0/netstandard2.0/net462` (the `#if` guards keep delegate*/CLong/`[SupportedOSPlatform]` off the legacy TFMs).
- [ ] **Step 5: Commit (WIP).** `git commit -m "feat(bindings): wire Layer 2 struct-layout + public-typed into orchestrator; emit Core public SDL.g.cs"`.

---

## Task 5: Public-API snapshot (Verify)

**Files:** Create `tests/api-snapshot/ApiSnapshot.csproj`, `CoreApiSnapshotTests.cs`, `*.verified.txt`. (`Verify.TUnit` already in CPM; **add `PublicApiGenerator` to CPM** — not present yet.) Sequence after Task 3 fix lands so the Core ProjectReference builds.

- [ ] **Step 1: Create the snapshot project** (TUnit + `Verify.TUnit` + `PublicApiGenerator`), targeting `net8.0`+ (not net462), referencing `Janset.SDL2.Core` **by ProjectReference only** (no `.Native` package — `GeneratePublicApi()` reads metadata only). Add to `Janset.SDL2.ClangSharpSpike.slnx`.
- [ ] **Step 2: Snapshot test:** `[Test] public Task SDL2_Core_public_api_is_unchanged() => Verify(typeof(SDL2.SDL).Assembly.GeneratePublicApi());`
- [ ] **Step 3: Run, review `.received.txt`, accept `.verified.txt`** — confirm `public static class SDL` with forwarders, ordinary consts, string-like `ReadOnlySpan<byte>` macro properties, and **no public `SDLNative`**.
- [ ] **Step 4: No-leak gate** — assert the public API does not expose `SDLNative` as a public type.
- [ ] **Step 5: Compatibility-policy gate** — assert the public API text does not expose raw ABI implementation markers (`SDLNative`, `LibraryImport`, `DllImport`, `UnmanagedCallConv`, `NativeTypeName`, public `CLong`/`CULong`) and does not publish the deferred endian alias names as `const` members. Snapshot review is necessary but not sufficient; these assertions encode the policy so the snapshot is not a human-only vibe check.
- [ ] **Step 6: Commit (WIP).** `git commit -m "test(bindings): Core Layer 2 public-API snapshot + no-raw-leak gate"`.

---

## Task 6: Public-L2 ABI smoke + policy docs reconciliation

**Files:** Extend `spikes/binding-generators/clangsharp/tests/abi-tests/`; update `binding-generator-constitution.md`, this plan/spec/ledger or `next-iteration-plan.md` where they would otherwise conflict.

- [ ] **Step 1: Extend the existing ABI test project** instead of creating a new runtime test project. Add a tiny public-L2 smoke that calls the generated public `SDL` surface after `Generated/Public/SDL.g.cs` exists. Minimum coverage: one clong-normalized call (`SDL_ThreadID()` or `SDL_GetThreadID(SDL_Thread.Null)`), one non-clong raw forwarder (`SDL_ClearError()` / `SDL_GetError()`), and one callback-shaped public signature if it can be exercised without introducing callback-lifetime policy. Keep deeper callback lifetime tests deferred to the dedicated callback slice.
- [ ] **Step 2: Add/adjust compile-only evidence if runtime smoke cannot exercise a shape.** If callback registration is unsafe to run here, the self-test compile fixture remains the blocker gate for callback direct/pointer/return shapes; the ABI test can stay focused on low-risk runtime forwarding.
- [ ] **Step 3: Reconcile docs in the same changeset.** Update the Constitution amendments/spec/resume docs so durable policy matches the executed slice: Litmus A, Safe-Alternative Guarantee as a public-release gate, string axis, function-like macro strategy, endian deferral/no public const freeze, callback `nint` L2 shape, Core-only public-typed activation, and family-blind/no-op-tolerant mechanisms. Do not amend ADR-004 yet; that remains deferred to Layer 2 closure.
- [ ] **Step 4: Commit (WIP).** `git commit -m "docs/test(bindings): record Layer 2 public API policy gates and ABI smoke"`.

---

## Final verification (before squash)

- [ ] `dotnet run --project .../postprocess -- --self-test` → PASS (projection fixtures incl. callback direct/pointer/return shapes, public macro member classification, endian suppression, and Modern+Compat compile-check).
- [ ] `dotnet build .../Janset.SDL2.Core.csproj -c Release` → clean across all 5 TFMs.
- [ ] Existing ABI test project green after adding the public-L2 smoke, or documented skip for runtime-only shapes that remain compile-checked.
- [ ] Snapshot green; no public raw-ABI leak; public consts and string-like macro properties present.
- [ ] Snapshot confirms deferred endian aliases are not frozen as public `const`.
- [ ] Twice-regenerated Core `Generated/` diff empty (deterministic sort).
- [ ] Determinism scope reviewed against Constitution §"Verification Contract": either run complete-family idempotency/family-isolation/per-family-equivalence checks, or record why this Core-only foundation slice uses a narrower check and where the full gate lands before public preview.
- [ ] Backend parity reviewed: compare actual Modern/Compat projectable raw member identities and fail or record any unclassified differences before treating Modern as the public projection source of truth.
- [ ] `slopwatch analyze --fail-on warning --exclude "artifacts/**,external/**,vcpkg_installed/**,**/bin/**,**/obj/**"` clean (CA1416 clean via preserved `[SupportedOSPlatform]`).
- [ ] Multi-agent review checkpoint (spec §13) passed.
- [ ] Squash WIP checkpoints (`git reset --soft`), present summary + message for approval (approval gate).

---

## References

- Spec: [`../specs/2026-05-29-binding-autogen-layer-2-typed-public-api-design.md`](../specs/2026-05-29-binding-autogen-layer-2-typed-public-api-design.md) (§2, §10B, §10, §12).
- Cross-family matrix: [`../../spikes/binding-generators/docs/canonical/references/2026-05-30-layer-2-cross-family-requirements.md`](../../spikes/binding-generators/docs/canonical/references/2026-05-30-layer-2-cross-family-requirements.md).
- Constitution: [`../../spikes/binding-generators/docs/canonical/binding-generator-constitution.md`](../../spikes/binding-generators/docs/canonical/binding-generator-constitution.md) — §"Layer Contract", §"C long" (L262), §"Generation Determinism Contract".
- In-repo pattern models: `postprocess/OpaqueHandleEmitRewriter.cs` (discover/build/emit), `FlagsAttributeRewriter.cs` (decoration), `Program.cs` (mode dispatch), `PostProcessSelfTests.cs` (self-tests), `generate_bindings.py:796-833` (`run_postprocess`), `:2042-2052` (insertion point). Divergence evidence: `Generated/Modern/SDL_events.g.cs` vs `Generated/Compat/SDL_events.g.cs` (delegate* vs IntPtr); `Generated/Modern/Platforms/**` (`#if`-guarded `[SupportedOSPlatform]`).
