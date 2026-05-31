# Layer 2 Core Foundation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Generate the public typed low-level (Layer 2) API for SDL2.Core — public `SDL` methods + constants forwarding to the internal `SDLNative` raw ABI — via **family-blind** postprocess rewriters that already handle every satellite's cases (config-driven), validated by self-tests carrying satellite-case fixtures (incl. a real **compile-check** of the emitted output) before any satellite is wired.

**Architecture:** Two new Roslyn postprocess passes in `Janset.SDL2.PostProcess`, modeled on `OpaqueHandleEmitRewriter` (discover→build→emit) and `FlagsAttributeRewriter` (in-place decoration). `PublicTypedMethodProjection` discovers `internal SDLNative` extern methods + `public const` fields (reading `Generated/Modern`) and emits a **single TFM-shared** `public partial class {public_class}` into `Generated/Public/`; `StructLayoutSequentialRewriter` prepends `[StructLayout(LayoutKind.Sequential)]` to every layout-free struct (universal, all families/codegens, in-place). Family facts (`public_class`, `raw_class`, `clong_methods`) from `family-config.json`; mechanism code-owned. Tested via `PostProcessSelfTests` (`--self-test`) + a Verify public-API snapshot.

**Design decision (2026-05-30, Deniz) — SINGLE public surface, pragma-separated:** the public layer is ONE TFM-shared file with **uniform signatures across all 5 TFMs**; per-TFM divergences are handled by `#if` in method bodies + **verbatim copy of the raw member's attribute trivia** (which already carries `#if NET5_0_OR_GREATER` guards) — NOT a TFM-split of the public layer. Revisit only if the divergent method count becomes unmanageable; current divergences (~25 `delegate*`-param + ~35 platform-attributed + few clong) are manageable as generated `#if` blocks. Concretely: `delegate*`-param methods expose a uniform `nint` param (`#if`-cast to `delegate*` only on Modern; typed-callback ergonomics → L3); clong methods expose uniform `long`/`ulong` (`#if`-normalized body); platform/obsolete attributes are copied verbatim with their guard trivia.

**Tech Stack:** C# / .NET 10 console (postprocess), Microsoft.CodeAnalysis.CSharp (Roslyn — incl. `CSharpCompilation` for the compile-check fixture), TUnit + Verify + PublicApiGenerator (snapshot), `generate_bindings.py` (orchestrator). Generated source compiles across `net10.0/net9.0/net8.0/netstandard2.0/net462`.

**Scope:** SDL2.Core, Layer 2 method forwarders + public constants + universal struct-layout + clong (both paths) + public-API snapshot. **Out of scope (later plans, see spec §1 reconciliation in Deferred):** TypedEnumProjection at `int`-flag positions, StructFieldAccessorProjection, function-like MacroHelperGeneration, EndianRuntimeAlias, ErrorRedirect, satellite activation, Mixer callbacks, typed-delegate callback ergonomics.

**Review status:** Two independent expert reviews (unbiased P/Invoke + context-full peer-benchmarked, 2026-05-30) → **proceed fixes-first**; architecture + ratified decisions ecosystem-validated (internal-raw+forwarder, `[NativeTypeName]`-drop, clong dual-path, deterministic sort, snapshot all best-in-class vs CsWin32 / ClangSharp / TerraFX / csbindgen). **All must-fixes from both reviews are integrated into the tasks below.** Remaining should-fixes are in "Deferred review findings."

---

## Deferred review findings (to be revisited)

- **(should-fix) Cross-namespace satellite fixture** — when satellites activate, add a self-test where a satellite raw method returns/takes a Core-owned `SDL_Surface*` / by-value `SDL_Renderer` in `SDL2.Image`, asserting the Core type emits **unqualified** with no `using SDL2;` (spec §10B-4). Forward-looking.
- **(spec reconciliation) Macro/endian deferral** — spec §1 lists Type-A macros + endian aliases in the Core slice; this plan defers them (object-like `public const` ARE re-emitted in Task 3; function-like macros + endian runtime-aliases are later slices). Update spec §1 to match in the Layer 2 changeset (Constitution Maintenance Rule).
- **(L3, in `next-iteration-plan.md` Forward Backlog) `SDL_bool` bool affordance** — D6 keeps the typed enum at L2; revisit at L3 with a non-overloading `bool ToBoolean(this SDL_bool)` / `IsTrue`.

---

## File structure

**Create:**
- `postprocess/PublicTypedMethodProjection.cs` — static `Discover`/`DiscoverFromSource` (methods + `public const` fields of the raw class only) + `BuildPublicClassContent` + emit, mirroring `OpaqueHandleEmitRewriter`.
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

## Task 3: `PublicTypedMethodProjection` (forwarders + constants + clong + divergence handling)

The core pass. Discovers, from `Generated/Modern`, the `internal ... partial class {raw_class}`'s **methods AND `public const` fields** (only members of the raw class — ignore namespace-level enums/delegates/handle structs), and emits ONE TFM-shared `public static unsafe partial class {public_class}` into `Generated/Public/{public_class}.g.cs`.

**Integrated must-fixes (both reviews):**
- **[blocker → fixed] `delegate*`-param Modern/Compat divergence.** ~25 Core methods take `delegate* unmanaged[Cdecl]<…>` (Modern) vs `IntPtr` (Compat). Per the single-surface decision: public param is **uniform `nint`** on all TFMs; the body is `#if NET6_0_OR_GREATER => {raw}.{name}((delegate* unmanaged[Cdecl]<…>)p, …)` `#else => {raw}.{name}(p, …)` — so `delegate*` is never referenced on netstandard2.0/net462. (Typed-callback ergonomics deferred to L3.)
- **[blocker → fixed] `[SupportedOSPlatform]` legacy-TFM break.** The attribute is .NET 5+ only (absent on netstandard2.0/net462, no polyfill); emitting it unconditionally breaks the legacy compile and contradicts roadmap §"Layer 2" ("same platform attribution as the raw ABI member"). Fix: **copy each raw member's attribute lists VERBATIM with their original leading/trailing trivia** — the trivia already carries the `#if NET5_0_OR_GREATER`/`#endif` directives AND all 5 platform values (`windows`, `android`, `ios`, `linux`, `windows10.0.10240.0`). Drop ONLY `[NativeTypeName]`, `[LibraryImport]`/`[DllImport]`, `[UnmanagedCallConv]`. This also preserves `[Obsolete]` (e.g. `SDL_GetRevisionNumber`) and any `[UnsupportedOSPlatform]`.
- **[major → fixed] clong input truncation on Unix.** Public param is `long`/`ulong`; forward `new CLong((nint)p)` / `new CULong((nuint)p)` — **NOT `(int)p`** (`(int)` truncates a valid 64-bit value on Unix x64 where `CLong.Value` is `nint`/64-bit). The `ThrowIfWindowsOutOfRange(long)` helper throws `ArgumentOutOfRangeException` only when `RuntimeInformation.IsOSPlatform(OSPlatform.Windows)` AND `value < int.MinValue || value > int.MaxValue` (fast non-Windows return; the native Windows `long` is 32-bit). Returns widen losslessly (no check): `#if NET6_0_OR_GREATER => (long|ulong){raw}.{name}(…).Value` `#else => {raw}.{name}(…)` (Compat raw already normalized).
- **[major → fixed] dropped public constants.** Re-emit the raw class's `public const` fields onto the public class (~130–155 object-like consts like `SDL_ALPHA_OPAQUE`, `SDL_RWOPS_*`). Object-like consts only; function-like macros stay deferred.
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
  - (g) **`delegate*`-param uniform nint** — raw `void SDL_SetEventFilter(delegate* unmanaged[Cdecl]<nint, SDL_Event*, int> filter, nint userdata)` → public `void SDL_SetEventFilter(nint filter, nint userdata)` with `#if NET6_0_OR_GREATER` containing `(delegate* unmanaged[Cdecl]<nint, SDL_Event*, int>)filter` and `#else` forwarding `filter` directly; the public **signature** must NOT contain `delegate*`.
  - (h) **public const re-emit** — raw class with `public const int SDL_ALPHA_OPAQUE = 255;` → public class contains `public const int SDL_ALPHA_OPAQUE = 255;`.
  - (i) **Discover filters to raw class** — a file with a namespace-level `public enum E {}` + `public delegate void D();` + the raw class with one method → only the method (not E/D) is projected.
  - (j) **deterministic order** — `SDL_Zebra` + `SDL_Apple` emit alphabetically regardless of source order.
  - (k) **no-op** — empty raw class → still emits `public static unsafe partial class SDL` (with `unsafe`), no throw.

- [ ] **Step 2: Write the COMPILE-CHECK fixture** `CheckPublicProjectionCompiles` (the single highest-value test — catches the divergence blockers in self-test, not at build time). Build the emitted public class text + a **Modern-shaped** stub raw class (using `delegate*`, `CLong`/`CULong`, `[SupportedOSPlatform]`) into a `CSharpCompilation` targeting net8-style refs and assert zero errors; then build the same public text against a **Compat-shaped** stub raw class (using `IntPtr`, `long`/`ulong`, no `SupportedOSPlatform`) with `netstandard2.0`-style refs (no `System.Runtime.Versioning.SupportedOSPlatformAttribute`, no function pointers used) and assert zero errors. Use `CSharpCompilation.Create(...).GetDiagnostics()` filtered to `Error`. (Roslyn is already referenced by the postprocess project.)

```csharp
private static void CheckPublicProjectionCompiles(List<string> failures)
{
    // Emit the public class from a fixture that exercises every divergence (clong, delegate*, platform attr, const).
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

- [ ] **Step 4: Implement `PublicTypedMethodProjection.cs`.** `DiscoverFromSource(string)` / `Discover(inputDir)` parse `*.g.cs`, collect from the `{raw_class}` declaration only: each `MethodDeclarationSyntax` (name, return type, params with attribute lists, the method's own attribute lists) and each `public const` `FieldDeclarationSyntax`. `BuildPublicClassContent(members, publicClass, rawClass, namespaceName, clongMethods)`:
  - Sort: consts (by name) then methods (by name, then full signature) — deterministic.
  - Emit `// <auto-generated>` header + `namespace` + `public static unsafe partial class {publicClass}` (mirror `BuildHandlesFileContent`).
  - **Attributes:** for each member, copy its attribute lists **verbatim with trivia** EXCEPT those named `NativeTypeName`/`LibraryImport`/`DllImport`/`UnmanagedCallConv` (drop). This carries `#if`-guarded `[SupportedOSPlatform]`/`[UnsupportedOSPlatform]` + `[Obsolete]` intact.
  - **const:** re-emit verbatim (`public const {type} {name} = {value};`).
  - **non-clong, non-delegate* method:** `public static {ret} {name}({params}) => {rawClass}.{name}({args});` (drop `[NativeTypeName]` from params/return).
  - **`delegate*`-param method:** public param type → `nint`; body `#if NET6_0_OR_GREATER` casts each such arg `(<original delegate* type>)p` forwarding to `{rawClass}.{name}`, `#else` forwards `p` directly.
  - **clong method** (name ∈ `clongMethods`): public uses `long`/`ulong` (signed from `CLong`, unsigned from `CULong`); body `#if NET6_0_OR_GREATER` (return: `(long|ulong){rawClass}.{name}(args).Value`; input param: `ThrowIfWindowsOutOfRange(p); … new CLong((nint)p) / new CULong((nuint)p) …`) `#else` (`{rawClass}.{name}(args)`).
  - Emit a private `static void ThrowIfWindowsOutOfRange(long value)` once per file (`if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && (value < int.MinValue || value > int.MaxValue)) throw new ArgumentOutOfRangeException(...)`).
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

- [ ] **Step 6: Run → `self-test: PASS`** (all fixtures a–k + the Modern+Compat compile-check).

- [ ] **Step 7: Commit (WIP).** `git commit -m "feat(bindings): Layer 2 PublicTypedMethodProjection (forwarders, consts, clong, delegate*/attr divergence, compile-checked)"`.

---

## Task 4: Orchestrator + csproj wiring; generate real Core output

**Files:** Modify `generate_bindings.py`, `Janset.SDL2.Core.csproj`; Create (generated) `Generated/Public/SDL.g.cs`.

- [ ] **Step 1: Add the unconditional shared-public csproj include** to `Janset.SDL2.Core.csproj` (`<Compile Include="Generated/Public/**/*.cs" />`) — correct because the public surface is uniform across TFMs (divergences are `#if`-guarded inside it).
- [ ] **Step 2: Wire the orchestrator** in `generate_bindings.py`, inserting **immediately after the `uniform-opaque` loop** (≈ line 2052, before `refresh_generated_file_counts`). Ordering matters: `public-typed` must be the **last** read of Modern signatures (after platform-delta + libraryimport + clong-dispatch + uniform-opaque), so it sees final shapes and de-duplicated platform methods. `struct-layout` runs over both codegens (reuses `run_postprocess`); `public-typed` for `"modern"` only (the Program.cs block redirects output to `Generated/Public`):

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

    print("--- postprocess: public-typed (Modern -> Generated/Public) ---")
    for family in selected:
        exit_code = run_postprocess(repo, family, spike_root, "public-typed", "modern")
        if exit_code != 0:
            postprocess_failures += 1
            print(f"WARNING: public-typed postprocess for {family} returned exit {exit_code}")
```

- [ ] **Step 3: Regenerate Core + idempotency.** Run generator for Core; run twice and diff → byte-identical. Verify `Generated/Public/SDL.g.cs` has `public static unsafe partial class SDL`; `SDL_ThreadID` → `ulong` (`#if`-guarded); `SDL_SetEventFilter(nint filter, …)` (uniform, `#if`-cast body); `[SupportedOSPlatform]` forwarders carry their `#if NET5_0_OR_GREATER` guard; `public const SDL_ALPHA_OPAQUE`; no `[NativeTypeName]`.
- [ ] **Step 4: Compile-check all 5 TFMs.** `dotnet build .../Janset.SDL2.Core.csproj -c Release` → clean on `net10.0/net9.0/net8.0/netstandard2.0/net462` (the `#if` guards keep delegate*/CLong/`[SupportedOSPlatform]` off the legacy TFMs).
- [ ] **Step 5: Commit (WIP).** `git commit -m "feat(bindings): wire Layer 2 struct-layout + public-typed into orchestrator; emit Core public SDL.g.cs"`.

---

## Task 5: Public-API snapshot (Verify)

**Files:** Create `tests/api-snapshot/ApiSnapshot.csproj`, `CoreApiSnapshotTests.cs`, `*.verified.txt`. (`Verify.TUnit` already in CPM; **add `PublicApiGenerator` to CPM** — not present yet.) Sequence after Task 3 fix lands so the Core ProjectReference builds.

- [ ] **Step 1: Create the snapshot project** (TUnit + `Verify.TUnit` + `PublicApiGenerator`), targeting `net8.0`+ (not net462), referencing `Janset.SDL2.Core` **by ProjectReference only** (no `.Native` package — `GeneratePublicApi()` reads metadata only). Add to `Janset.SDL2.ClangSharpSpike.slnx`.
- [ ] **Step 2: Snapshot test:** `[Test] public Task SDL2_Core_public_api_is_unchanged() => Verify(typeof(SDL2.SDL).Assembly.GeneratePublicApi());`
- [ ] **Step 3: Run, review `.received.txt`, accept `.verified.txt`** — confirm `public static class SDL` with forwarders + consts, and **no public `SDLNative`**.
- [ ] **Step 4: No-leak gate** — assert the public API does not expose `SDLNative` as a public type.
- [ ] **Step 5: Commit (WIP).** `git commit -m "test(bindings): Core Layer 2 public-API snapshot + no-raw-leak gate"`.

---

## Final verification (before squash)

- [ ] `dotnet run --project .../postprocess -- --self-test` → PASS (fixtures a–k + Modern+Compat compile-check).
- [ ] `dotnet build .../Janset.SDL2.Core.csproj -c Release` → clean across all 5 TFMs.
- [ ] Snapshot green; no public raw-ABI leak; public consts present.
- [ ] Twice-regenerated Core `Generated/` diff empty (deterministic sort).
- [ ] `slopwatch analyze --fail-on warning --exclude "artifacts/**,external/**,vcpkg_installed/**,**/bin/**,**/obj/**"` clean (CA1416 clean via preserved `[SupportedOSPlatform]`).
- [ ] Multi-agent review checkpoint (spec §13) passed.
- [ ] Squash WIP checkpoints (`git reset --soft`), present summary + message for approval (approval gate).

---

## References

- Spec: [`../specs/2026-05-29-binding-autogen-layer-2-typed-public-api-design.md`](../specs/2026-05-29-binding-autogen-layer-2-typed-public-api-design.md) (§2, §10B, §10, §12).
- Cross-family matrix: [`../../spikes/binding-generators/docs/canonical/references/2026-05-30-layer-2-cross-family-requirements.md`](../../spikes/binding-generators/docs/canonical/references/2026-05-30-layer-2-cross-family-requirements.md).
- Constitution: [`../../spikes/binding-generators/docs/canonical/binding-generator-constitution.md`](../../spikes/binding-generators/docs/canonical/binding-generator-constitution.md) — §"Layer Contract", §"C long" (L262), §"Generation Determinism Contract".
- In-repo pattern models: `postprocess/OpaqueHandleEmitRewriter.cs` (discover/build/emit), `FlagsAttributeRewriter.cs` (decoration), `Program.cs` (mode dispatch), `PostProcessSelfTests.cs` (self-tests), `generate_bindings.py:796-833` (`run_postprocess`), `:2042-2052` (insertion point). Divergence evidence: `Generated/Modern/SDL_events.g.cs` vs `Generated/Compat/SDL_events.g.cs` (delegate* vs IntPtr); `Generated/Modern/Platforms/**` (`#if`-guarded `[SupportedOSPlatform]`).
