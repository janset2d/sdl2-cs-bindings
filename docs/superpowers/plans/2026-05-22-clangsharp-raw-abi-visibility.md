# ClangSharp Raw ABI Visibility Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the ClangSharp spike hide generated raw ABI containers from package consumers and fix SDL2_image namespace drift without adding a Roslyn visibility postprocess.

**Architecture:** Use ClangSharp's native `--with-access-specifier <RawClass>=Internal` for raw ABI container visibility, keep raw import member text as generated, and update the oracle to check effective public leakage through the containing raw class. Public generated enums/structs/constants remain public for the future typed layer; raw extern containers remain implementation detail.

**Tech Stack:** Python `generate_bindings.py`, ClangSharp P/Invoke Generator RSP/CLI options, Roslyn-based `oracle.cs`, .NET 10 file/project execution.

---

## File Structure

- Modify `spikes/binding-generators/clangsharp/postprocess/Program.cs`: remove the stale `--self-test` hook added by the abandoned postprocess approach.
- Delete `spikes/binding-generators/clangsharp/postprocess/RawAbiVisibilityRewriter.cs`: no visibility rewriter is used.
- Delete `spikes/binding-generators/clangsharp/postprocess/PostProcessSelfTests.cs`: no postprocess self-test is needed for visibility.
- Modify `spikes/binding-generators/clangsharp/rsp/base.rsp`: remove shared `--namespace SDL2`; namespace becomes family-owned.
- Modify `spikes/binding-generators/clangsharp/generate_bindings.py`: add family namespace/raw-class facts, emit `--namespace` and class-level `--with-access-specifier <raw-class>=Internal` per family.
- Modify `spikes/binding-generators/clangsharp/src/Janset.SDL2.Image/Support/NativeTypeNameAttribute.cs`: move support attribute to `SDL2.Image` namespace during the regeneration task, when Image generated files move to the same namespace.
- Modify `spikes/binding-generators/clangsharp/oracle.cs`: treat raw import leaks as effective public API leaks only when the containing raw class is public.
- Regenerate generated `.g.cs` files under `spikes/binding-generators/clangsharp/src/Janset.SDL2.Core/Generated/` and `spikes/binding-generators/clangsharp/src/Janset.SDL2.Image/Generated/`.
- Regenerate evidence reports under `spikes/binding-generators/output/reports/`.
- Modify docs that mention internal raw ABI philosophy: `docs/binding-autogen/binding-generator-constitution.md`, `docs/binding-autogen/README.md`, `spikes/binding-generators/docs/next-iteration-plan.md`, and `spikes/binding-generators/docs/llm-handoff.md`.

Do not modify production Cake generator code, manifests, package projects, CI, or production `src/` package output.

## Task 1: Remove Stale Visibility Postprocess Work

**Files:**
- Modify: `spikes/binding-generators/clangsharp/postprocess/Program.cs`
- Delete: `spikes/binding-generators/clangsharp/postprocess/RawAbiVisibilityRewriter.cs`
- Delete: `spikes/binding-generators/clangsharp/postprocess/PostProcessSelfTests.cs`

- [ ] **Step 1: Remove the stale `--self-test` hook**

In `Program.cs`, remove this block if present:

```csharp
if (args is ["--self-test"])
{
    return PostProcessSelfTests.Run();
}
```

Also remove the usage comment line:

```csharp
//   dotnet run --project postprocess -- --self-test
```

- [ ] **Step 2: Delete stale rewriter files**

Delete these files if present:

```text
spikes/binding-generators/clangsharp/postprocess/RawAbiVisibilityRewriter.cs
spikes/binding-generators/clangsharp/postprocess/PostProcessSelfTests.cs
```

- [ ] **Step 3: Build postprocess project**

Run:

```pwsh
dotnet build spikes/binding-generators/clangsharp/postprocess/Janset.SDL2.PostProcess.csproj -c Release
```

Expected: build succeeds with `0 Warning(s)` and `0 Error(s)`.

## Task 2: Make Family Namespace And Raw Class Visibility Explicit

**Files:**
- Modify: `spikes/binding-generators/clangsharp/generate_bindings.py`
- Modify: `spikes/binding-generators/clangsharp/rsp/base.rsp`

- [ ] **Step 1: Move namespace/raw-class facts into `FAMILY_CONFIG`**

In `generate_bindings.py`, update `FAMILY_CONFIG` to include `namespace` and `raw_class` values:

```python
FAMILY_CONFIG = {
    "core": {
        "rsp": "sdl2-core.rsp",
        "bootstrap_scope": "bootstrap-sdl2-core.headers.txt",
        "full_scope": "sdl2-core.headers.txt",
        "library_dir": "Janset.SDL2.Core",
        "namespace": "SDL2",
        "raw_class": "SDLNative",
    },
    "image": {
        "rsp": "sdl2-image.rsp",
        "bootstrap_scope": "bootstrap-sdl2-image.headers.txt",
        "full_scope": "sdl2-image.headers.txt",
        "library_dir": "Janset.SDL2.Image",
        "namespace": "SDL2.Image",
        "raw_class": "SDL_imageNative",
    },
}
```

- [ ] **Step 2: Remove shared namespace from `base.rsp`**

In `spikes/binding-generators/clangsharp/rsp/base.rsp`, delete:

```text
--namespace
SDL2
```

The file should start with:

```text
--define-macro
SDL_DECLSPEC=
```

- [ ] **Step 3: Add per-family namespace and class-level internal access to normal commands**

In `command_for_header`, update the shared command extension block to include family namespace and raw class access:

```python
    command.extend([
        f"@{rsp_root / 'base.rsp'}",
        f"@{rsp_root / FAMILY_CONFIG[family]['rsp']}",
        "--namespace", FAMILY_CONFIG[family]["namespace"],
        "--with-access-specifier", f"{FAMILY_CONFIG[family]['raw_class']}=Internal",
        "--include-directory", str(include_root),
    ])
```

- [ ] **Step 4: Add the same options to platform commands**

In `platform_command_for_header`, make the same command extension change:

```python
    command.extend([
        f"@{rsp_root / 'base.rsp'}",
        f"@{rsp_root / FAMILY_CONFIG[family]['rsp']}",
        "--namespace", FAMILY_CONFIG[family]["namespace"],
        "--with-access-specifier", f"{FAMILY_CONFIG[family]['raw_class']}=Internal",
        "--include-directory", str(include_root),
    ])
```

- [ ] **Step 5: Dry-run command generation for both families**

Run:

```pwsh
python spikes/binding-generators/clangsharp/generate_bindings.py --scope bootstrap --family core --codegen compat
```

Expected: printed commands include `--namespace SDL2` and `--with-access-specifier SDLNative=Internal`, with no `--namespace SDL2` coming from `base.rsp`.

Run:

```pwsh
python spikes/binding-generators/clangsharp/generate_bindings.py --scope bootstrap --family image --codegen compat
```

Expected: printed commands include `--namespace SDL2.Image` and `--with-access-specifier SDL_imageNative=Internal`.

## Task 3: Update Oracle Effective Visibility Semantics

**Files:**
- Modify: `spikes/binding-generators/clangsharp/oracle.cs`

- [ ] **Step 1: Add raw class accessibility lookup in `RawAbiChecks.Run`**

In `RawAbiChecks.Run`, compute whether the expected raw class is effectively public before checking raw import methods:

```csharp
        var rawClassIsPublic = evidence.Types.Any(type =>
            type.Kind == "Class"
            && type.Name == config.ExpectedRawClassName
            && type.Accessibility == "public");
```

- [ ] **Step 2: Keep public raw class as a hard bug**

Keep the existing raw class check equivalent to:

```csharp
        foreach (var type in evidence.Types.Where(type => type.Kind == "Class" && type.Name == config.ExpectedRawClassName && type.Accessibility == "public"))
        {
            checks.Add(HardBug("raw-abi-public-class", type.Name, "Raw ABI class is public; generated raw extern containers must be internal.", type.SourcePath));
        }
```

- [ ] **Step 3: Gate raw import leak check on public raw class**

Change the raw import method leak loop to:

```csharp
        if (rawClassIsPublic)
        {
            foreach (var function in evidence.Functions.Where(function => IsRawImport(config, function) && function.Accessibility == "public"))
            {
                checks.Add(HardBug("raw-abi-public-import", function.ManagedName, "Raw native import method is effectively public because the raw ABI container is public.", function.SourcePath));
            }
        }
```

This intentionally allows lexical `public static extern` members inside an `internal` raw ABI class, because C# containing type accessibility prevents those methods from becoming public package API.

- [ ] **Step 4: Update oracle self-test expectations if needed**

Run:

```pwsh
dotnet run --file spikes/binding-generators/clangsharp/oracle.cs -- --self-test
```

Expected before updating tests: any failure should point to expected raw import leak text/count assumptions. Update only those self-test fixtures/expected messages that assume lexical public methods are leaks even when the containing class is not public.

Run again:

```pwsh
dotnet run --file spikes/binding-generators/clangsharp/oracle.cs -- --self-test
```

Expected: `self-test: PASS`.

## Task 4: Enrich Internal Raw ABI Documentation With WHY/HOW/WHAT

**Files:**
- Modify: `docs/binding-autogen/binding-generator-constitution.md`
- Modify: `docs/binding-autogen/README.md`
- Modify: `docs/superpowers/specs/2026-05-22-clangsharp-raw-abi-visibility-design.md`
- Modify: `spikes/binding-generators/docs/next-iteration-plan.md`
- Modify: `spikes/binding-generators/docs/llm-handoff.md`

- [ ] **Step 1: Add WHY/HOW/WHAT to the constitution**

In `docs/binding-autogen/binding-generator-constitution.md`, expand the Layer Contract section near the internal raw ABI layer with this content, adapted to fit the existing prose:

```markdown
### Internal Raw ABI: Why / How / What

**Why:** Generated native imports are volatile implementation detail, not the user-facing compatibility contract. Keeping them internal lets the generator fix C `long`, `wchar_t`, bool, struct layout, platform attribution, and backend choices without turning every correction into a public breaking change. It also follows .NET interop guidance: visible P/Invokes are a design smell unless the package is explicitly a raw-header catalog.

**How:** The raw ABI container type is internal. Member declarations may remain lexically public when produced by upstream generators, but they are not effectively public if their containing type is internal. Public low-level APIs call the internal raw container and expose honest typed handles, `nint` values, spans, pointers, and unsafe overloads where SDL requires them.

**What:** The main package exposes a typed, low-allocation SDL API and later friendly overloads. It does not expose generated `[DllImport]` / `[LibraryImport]` classes as the blessed user API. If demand appears later, a separate raw package or namespace can be designed with a different compatibility promise.
```

- [ ] **Step 2: Update binding-autogen README summary**

In `docs/binding-autogen/README.md`, enrich the bullet that currently says public API shape is internal raw ABI + public typed + friendly. Mention:

```markdown
Internal means the raw container is not public API; generated methods may remain lexically public inside an internal container when using upstream emitters. Escape hatches belong in typed handles, `DangerousGetHandle()` / `nint`, span/pointer overloads, and deliberately unsafe APIs, not in public generated extern classes.
```

- [ ] **Step 3: Update the A-slice design spec**

In `docs/superpowers/specs/2026-05-22-clangsharp-raw-abi-visibility-design.md`:

- Replace the postprocess-first design with class-level `--with-access-specifier <RawClass>=Internal`.
- Replace exit evidence `raw-abi-public-import = 0` wording with effective leak wording.
- Add the WHY/HOW/WHAT rationale or link to the constitution section.
- Preserve the `NativeTypeNameAttribute` decision: internal, assembly-local, per-family.

- [ ] **Step 4: Update spike active docs**

In `spikes/binding-generators/docs/next-iteration-plan.md` and `spikes/binding-generators/docs/llm-handoff.md`, update A repair queue wording:

```markdown
Make raw ABI containers internal via ClangSharp class-level access specifiers. Raw import methods inside an internal container are not public API leaks; the oracle checks effective visibility rather than lexical member modifiers.
```

## Task 5: Regenerate, Verify, And Update Evidence

**Files:**
- Regenerate: `spikes/binding-generators/clangsharp/src/Janset.SDL2.Core/Generated/**/*.g.cs`
- Regenerate: `spikes/binding-generators/clangsharp/src/Janset.SDL2.Image/Generated/**/*.g.cs`
- Modify: `spikes/binding-generators/clangsharp/src/Janset.SDL2.Image/Support/NativeTypeNameAttribute.cs`
- Regenerate: `spikes/binding-generators/output/reports/clangsharp-full.md`
- Regenerate: `spikes/binding-generators/output/reports/oracle-evidence-clangsharp.md`

- [ ] **Step 1: Move Image support attribute namespace**

In `spikes/binding-generators/clangsharp/src/Janset.SDL2.Image/Support/NativeTypeNameAttribute.cs`, change:

```csharp
namespace SDL2;
```

to:

```csharp
namespace SDL2.Image;
```

Do not make `NativeTypeNameAttribute` public. It remains internal and assembly-local.

- [ ] **Step 2: Run full generation**

Run:

```pwsh
python spikes/binding-generators/clangsharp/generate_bindings.py --scope full --codegen both --execute --clean-output --vcpkg-triplet x64-windows-hybrid --use-platform-header-shims
```

Expected: exit code `0`. Output includes existing postprocess phases `platform-delta`, `strip-varargs`, and `libraryimport`; it must not include `raw-abi-visibility`.

- [ ] **Step 3: Run the oracle report**

Run:

```pwsh
dotnet run --file spikes/binding-generators/clangsharp/oracle.cs -- --family sdl2-core --family sdl2-image --write-report
```

Expected: exit code `0`; report is rewritten.

- [ ] **Step 4: Inspect oracle A findings**

Confirm in `spikes/binding-generators/output/reports/oracle-evidence-clangsharp.md`:

```text
raw-abi-public-class
raw-abi-public-import
family-namespace-drift
```

Expected: those check IDs are absent, or their count is explicitly `0` if the renderer keeps zero-count sections. B/C gap IDs may remain.

- [ ] **Step 5: Build the spike libraries**

Run:

```pwsh
dotnet build spikes/binding-generators/clangsharp/src/Janset.SDL2.Image/Janset.SDL2.Image.csproj -c Release
```

Expected: build succeeds for Core + Image across `net462`, `netstandard2.0`, `net8.0`, `net9.0`, and `net10.0` with `0 Warning(s)` and `0 Error(s)`.

- [ ] **Step 6: Run whitespace and anti-slop checks**

Run:

```pwsh
git diff --check
```

Expected: no output, exit code `0`.

Run:

```pwsh
slopwatch analyze --fail-on warning --exclude "artifacts/**,external/**,vcpkg_installed/**,spikes/binding-generators/references/**,**/bin/**,**/obj/**"
```

Expected: `Scan complete: 0 issue(s) found`.

## Task 6: Final Review And Commit Approval Gate

**Files:**
- Review all modified files from `git status --short`.

- [ ] **Step 1: Inspect status**

Run:

```pwsh
git status --short
```

Expected: only spike docs, canonical binding-autogen docs, spike generator/oracle files, regenerated spike outputs, and regenerated spike reports are changed.

- [ ] **Step 2: Inspect diff**

Run:

```pwsh
git diff -- docs/superpowers docs/binding-autogen spikes/binding-generators/docs spikes/binding-generators/clangsharp/generate_bindings.py spikes/binding-generators/clangsharp/rsp spikes/binding-generators/clangsharp/oracle.cs spikes/binding-generators/clangsharp/postprocess spikes/binding-generators/clangsharp/src spikes/binding-generators/output/reports
```

Expected: diff matches this plan: no raw visibility postprocess, class-level ClangSharp internal raw containers, effective oracle visibility, namespace fix, docs WHY/HOW/WHAT, regenerated output/report updates.

- [ ] **Step 3: Prepare commit summary, but do not commit yet**

Because `AGENTS.md` requires approval before any commit, stop and present:

```text
Summary:
- Hid ClangSharp raw ABI containers using class-level access specifiers.
- Updated oracle checks to use effective raw ABI visibility.
- Fixed SDL2_image namespace drift.
- Documented the internal raw ABI WHY/HOW/WHAT philosophy and escape-hatch posture.

Proposed commit message:
spike: hide ClangSharp raw ABI containers

Please approve before I commit.
```

Expected: no commit is made until the user explicitly approves.
