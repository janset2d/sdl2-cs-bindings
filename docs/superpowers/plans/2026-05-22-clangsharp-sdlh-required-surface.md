# ClangSharp SDL.h Required Surface Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Recover SDL2 Core init/quit functions and `SDL_INIT_*` constants declared only in `SDL.h` for the ClangSharp spike.

**Architecture:** Add a spike-local name allowlist for the narrow `SDL.h` surface, parse installed `SDL.h` only for those names, emit synthetic generated raw ABI files for Compat and Modern, and let the existing Modern postprocess convert DllImport declarations to LibraryImport. Keep `build/manifest.json` as validation/evidence only, not as generation input.

**Tech Stack:** Python 3 spike orchestrator, ClangSharp-generated C#, Roslyn postprocess, .NET 10 file-based oracle, TUnit-style repo verification commands, Slopwatch.

---

## File Structure

- Create: `spikes/binding-generators/scope/sdl2-core-sdlh-required.json`
  - Spike-local allowlist of `SDL.h` names to recover.
- Modify: `spikes/binding-generators/clangsharp/generate_bindings.py`
  - Load the allowlist.
  - Parse constrained declarations/macros from installed `SDL.h`.
  - Validate allowlist parity against installed `SDL.h` and manifest-required surface.
  - Emit `Generated/<Compat|Modern>/SDL_required.g.cs` before postprocess runs.
  - Add a small `--self-test` mode for parser/emitter validation.
- Modify: `spikes/binding-generators/clangsharp/oracle.cs`
  - Add self-test coverage for const computed constants.
  - Keep existing missing-required report semantics.
- Regenerate: `spikes/binding-generators/clangsharp/src/Janset.SDL2.Core/Generated/**`
  - Adds `SDL_required.g.cs` in Compat and Modern.
- Regenerate: `spikes/binding-generators/output/reports/clangsharp-full.md`
  - Captures synthetic generated-file count and existing ClangSharp command evidence.
- Regenerate: `spikes/binding-generators/output/reports/oracle-evidence-clangsharp.md`
  - Shows required SDL.h functions/constants are no longer missing.

Do not modify production `src/`, project files, manifests, CI, vcpkg config, package files, or satellite generation scope in this slice.

## Task 1: Add Spike-Local Required Surface Allowlist

**Files:**
- Create: `spikes/binding-generators/scope/sdl2-core-sdlh-required.json`

- [ ] **Step 1: Create the allowlist file**

Create `spikes/binding-generators/scope/sdl2-core-sdlh-required.json` with exactly this content:

```json
{
  "family": "sdl2-core",
  "header": "SDL.h",
  "functions": [
    "SDL_Init",
    "SDL_InitSubSystem",
    "SDL_QuitSubSystem",
    "SDL_WasInit",
    "SDL_Quit"
  ],
  "constants": [
    "SDL_INIT_TIMER",
    "SDL_INIT_AUDIO",
    "SDL_INIT_VIDEO",
    "SDL_INIT_JOYSTICK",
    "SDL_INIT_HAPTIC",
    "SDL_INIT_GAMECONTROLLER",
    "SDL_INIT_EVENTS",
    "SDL_INIT_SENSOR",
    "SDL_INIT_NOPARACHUTE",
    "SDL_INIT_EVERYTHING"
  ]
}
```

- [ ] **Step 2: Verify the JSON is valid**

Run:

```pwsh
python -m json.tool spikes/binding-generators/scope/sdl2-core-sdlh-required.json
```

Expected: formatted JSON is printed and the command exits `0`.

- [ ] **Step 3: Confirm worktree scope**

Run:

```pwsh
git status --short -- spikes/binding-generators/scope/sdl2-core-sdlh-required.json
```

Expected:

```text
?? spikes/binding-generators/scope/sdl2-core-sdlh-required.json
```

## Task 2: Add Required SDL.h Parser And Self-Test

**Files:**
- Modify: `spikes/binding-generators/clangsharp/generate_bindings.py`

- [ ] **Step 1: Add JSON import and required-surface dataclasses**

At the top of `generate_bindings.py`, add `json` to the imports:

```python
import argparse
import json
import pathlib
import re
import shutil
import subprocess
import sys
from dataclasses import dataclass
```

After `EmptyGeneratedOutput`, add focused data records:

```python
@dataclass(frozen=True)
class RequiredSurfaceAllowlist:
    family: str
    header: str
    functions: tuple[str, ...]
    constants: tuple[str, ...]


@dataclass(frozen=True)
class RequiredParameter:
    native_type: str
    managed_type: str
    name: str


@dataclass(frozen=True)
class RequiredFunction:
    name: str
    native_return_type: str
    managed_return_type: str
    parameters: tuple[RequiredParameter, ...]


@dataclass(frozen=True)
class RequiredConstant:
    name: str
    raw_value: str
    managed_value: str
    kind: str
    native_type_name: str
```

- [ ] **Step 2: Add allowlist loading**

Add this helper near `read_scope`:

```python
def read_required_surface_allowlist(path: pathlib.Path) -> RequiredSurfaceAllowlist:
    data = json.loads(path.read_text(encoding="utf-8"))
    return RequiredSurfaceAllowlist(
        family=str(data["family"]),
        header=str(data["header"]),
        functions=tuple(str(name) for name in data["functions"]),
        constants=tuple(str(name) for name in data["constants"]),
    )
```

- [ ] **Step 3: Add constrained SDL.h parsing helpers**

Add these helpers after `read_required_surface_allowlist`:

```python
FUNCTION_DECLARATION_PATTERN = re.compile(
    r"^extern\s+DECLSPEC\s+(?P<return_type>.+?)\s+SDLCALL\s+(?P<name>SDL_\w+)\((?P<parameters>.*?)\);$"
)
MACRO_PATTERN = re.compile(r"^#define\s+(?P<name>SDL_INIT_\w+)\s+(?P<value>.+)$")


def parse_required_sdlh_surface(header_path: pathlib.Path, allowlist: RequiredSurfaceAllowlist) -> tuple[list[RequiredFunction], list[RequiredConstant]]:
    text = header_path.read_text(encoding="utf-8")
    functions = parse_required_sdlh_functions(text, allowlist.functions)
    constants = parse_required_sdlh_constants(text, allowlist.constants)
    return functions, constants


def parse_required_sdlh_functions(text: str, allowed_names: tuple[str, ...]) -> list[RequiredFunction]:
    allowed = set(allowed_names)
    discovered: dict[str, RequiredFunction] = {}
    direct_function_names: set[str] = set()
    for line in text.splitlines():
        match = FUNCTION_DECLARATION_PATTERN.match(line.strip())
        if match is None:
            continue
        name = match.group("name")
        direct_function_names.add(name)
        if name in allowed:
            discovered[name] = RequiredFunction(
                name=name,
                native_return_type=match.group("return_type"),
                managed_return_type=map_sdlh_type(match.group("return_type")),
                parameters=parse_required_parameters(match.group("parameters")),
            )

    extra = sorted(direct_function_names - allowed)
    if extra:
        raise RuntimeError(f"SDL.h declares unexpected direct functions: {', '.join(extra)}")
    missing = [name for name in allowed_names if name not in discovered]
    if missing:
        raise RuntimeError(f"SDL.h is missing required functions: {', '.join(missing)}")
    return [discovered[name] for name in allowed_names]


def parse_required_parameters(parameters_text: str) -> tuple[RequiredParameter, ...]:
    stripped = parameters_text.strip()
    if stripped == "void" or not stripped:
        return ()

    parameters: list[RequiredParameter] = []
    for parameter_text in stripped.split(","):
        parts = parameter_text.strip().split()
        if len(parts) != 2:
            raise RuntimeError(f"Unsupported SDL.h parameter declaration: {parameter_text}")
        native_type, name = parts
        parameters.append(RequiredParameter(native_type, map_sdlh_type(native_type), name))
    return tuple(parameters)


def parse_required_sdlh_constants(text: str, allowed_names: tuple[str, ...]) -> list[RequiredConstant]:
    allowed = set(allowed_names)
    discovered: dict[str, RequiredConstant] = {}
    direct_macro_names: set[str] = set()
    for logical_line in join_macro_continuations(text):
        match = MACRO_PATTERN.match(logical_line.strip())
        if match is None:
            continue
        name = match.group("name")
        direct_macro_names.add(name)
        if name in allowed:
            raw_value = normalize_macro_value(match.group("value"))
            discovered[name] = RequiredConstant(
                name=name,
                raw_value=raw_value,
                managed_value=map_sdlh_macro_value(raw_value),
                kind="Computed" if "|" in raw_value else "Literal",
                native_type_name=f"#define {name} {raw_value}",
            )

    extra = sorted(direct_macro_names - allowed)
    if extra:
        raise RuntimeError(f"SDL.h declares unexpected SDL_INIT macros: {', '.join(extra)}")
    missing = [name for name in allowed_names if name not in discovered]
    if missing:
        raise RuntimeError(f"SDL.h is missing required SDL_INIT macros: {', '.join(missing)}")
    return [discovered[name] for name in allowed_names]


def join_macro_continuations(text: str) -> list[str]:
    lines: list[str] = []
    pending = ""
    for line in text.splitlines():
        stripped = line.rstrip()
        if stripped.endswith("\\"):
            pending += stripped[:-1].strip() + " "
            continue
        if pending:
            lines.append(pending + stripped.strip())
            pending = ""
        else:
            lines.append(stripped)
    if pending:
        lines.append(pending.strip())
    return lines


def normalize_macro_value(value: str) -> str:
    normalized = " ".join(value.strip().split())
    if normalized.startswith("(") and normalized.endswith(")"):
        normalized = normalized[1:-1].strip()
    return normalized


def map_sdlh_type(native_type: str) -> str:
    return {
        "void": "void",
        "int": "int",
        "Uint32": "uint",
    }.get(native_type, native_type)


def map_sdlh_macro_value(raw_value: str) -> str:
    return re.sub(r"\b(0x[0-9A-Fa-f]+)u\b", lambda match: match.group(1) + "U", raw_value)
```

- [ ] **Step 4: Add self-test command path**

Extend the argument parser with:

```python
parser.add_argument("--self-test", action="store_true", help="Run generator parser self-tests and exit")
```

Immediately after `args = parser.parse_args()`, add:

```python
if args.self_test:
    return run_self_tests()
```

Add this self-test helper before `main()`:

```python
def run_self_tests() -> int:
    allowlist = RequiredSurfaceAllowlist(
        "sdl2-core",
        "SDL.h",
        ("SDL_Init", "SDL_InitSubSystem", "SDL_QuitSubSystem", "SDL_WasInit", "SDL_Quit"),
        ("SDL_INIT_TIMER", "SDL_INIT_AUDIO", "SDL_INIT_VIDEO", "SDL_INIT_JOYSTICK", "SDL_INIT_HAPTIC", "SDL_INIT_GAMECONTROLLER", "SDL_INIT_EVENTS", "SDL_INIT_SENSOR", "SDL_INIT_NOPARACHUTE", "SDL_INIT_EVERYTHING"),
    )
    fixture = r"""
#define SDL_INIT_TIMER          0x00000001u
#define SDL_INIT_AUDIO          0x00000010u
#define SDL_INIT_VIDEO          0x00000020u
#define SDL_INIT_JOYSTICK       0x00000200u
#define SDL_INIT_HAPTIC         0x00001000u
#define SDL_INIT_GAMECONTROLLER 0x00002000u
#define SDL_INIT_EVENTS         0x00004000u
#define SDL_INIT_SENSOR         0x00008000u
#define SDL_INIT_NOPARACHUTE    0x00100000u
#define SDL_INIT_EVERYTHING ( \\
                SDL_INIT_TIMER | SDL_INIT_AUDIO | SDL_INIT_VIDEO | SDL_INIT_EVENTS | \\
                SDL_INIT_JOYSTICK | SDL_INIT_HAPTIC | SDL_INIT_GAMECONTROLLER | SDL_INIT_SENSOR \\
            )
extern DECLSPEC int SDLCALL SDL_Init(Uint32 flags);
extern DECLSPEC int SDLCALL SDL_InitSubSystem(Uint32 flags);
extern DECLSPEC void SDLCALL SDL_QuitSubSystem(Uint32 flags);
extern DECLSPEC Uint32 SDLCALL SDL_WasInit(Uint32 flags);
extern DECLSPEC void SDLCALL SDL_Quit(void);
"""
    functions = parse_required_sdlh_functions(fixture, allowlist.functions)
    constants = parse_required_sdlh_constants(fixture, allowlist.constants)
    failures: list[str] = []
    if [function.name for function in functions] != list(allowlist.functions):
        failures.append("function order did not match allowlist")
    if functions[0].parameters != (RequiredParameter("Uint32", "uint", "flags"),):
        failures.append("Uint32 flags parameter was not parsed")
    if functions[3].managed_return_type != "uint":
        failures.append("Uint32 return type was not mapped to uint")
    if [constant.name for constant in constants] != list(allowlist.constants):
        failures.append("constant order did not match allowlist")
    if constants[-1].kind != "Computed" or "SDL_INIT_GAMECONTROLLER" not in constants[-1].managed_value:
        failures.append("SDL_INIT_EVERYTHING was not parsed as a computed expression")

    try:
        parse_required_sdlh_functions(fixture + "extern DECLSPEC int SDLCALL SDL_Unexpected(void);\n", allowlist.functions)
        failures.append("unexpected direct SDL.h functions were not rejected")
    except RuntimeError:
        pass

    if failures:
        for failure in failures:
            print(f"self-test: FAIL: {failure}")
        return 1
    print("self-test: PASS")
    return 0
```

- [ ] **Step 5: Run self-test red/green checkpoint**

Run:

```pwsh
python spikes/binding-generators/clangsharp/generate_bindings.py --self-test
```

Expected after implementation: `self-test: PASS`.

If implementing with TDD, first add the self-test expectations before the parser helpers and observe failure due missing functions, then add the helpers and rerun to pass.

## Task 3: Emit Required SDL.h Generated Files

**Files:**
- Modify: `spikes/binding-generators/clangsharp/generate_bindings.py`
- Generated later: `spikes/binding-generators/clangsharp/src/Janset.SDL2.Core/Generated/Compat/SDL_required.g.cs`
- Generated later: `spikes/binding-generators/clangsharp/src/Janset.SDL2.Core/Generated/Modern/SDL_required.g.cs`

- [ ] **Step 1: Add output path and C# rendering helpers**

Add these helpers near `output_path_for_header`:

```python
def output_path_for_required_surface(repo: pathlib.Path, codegen: str, family: str) -> pathlib.Path:
    subdir = "Compat" if codegen == "compat" else "Modern"
    library_dir = FAMILY_CONFIG[family]["library_dir"]
    return repo / "spikes" / "binding-generators" / "clangsharp" / "src" / library_dir / "Generated" / subdir / "SDL_required.g.cs"


def render_required_surface(namespace: str, raw_class: str, functions: list[RequiredFunction], constants: list[RequiredConstant]) -> str:
    lines = [
        "using System.Runtime.InteropServices;",
        "",
        f"namespace {namespace}",
        "{",
        f"    internal static unsafe partial class {raw_class}",
        "    {",
    ]
    for constant in constants:
        lines.append(f"        [NativeTypeName(\"{constant.native_type_name}\")]")
        lines.append(f"        public const uint {constant.name} = {constant.managed_value};")
        lines.append("")
    for function in functions:
        lines.append("        [DllImport(\"SDL2\", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]")
        if function.native_return_type != function.managed_return_type and function.native_return_type != "void":
            lines.append(f"        [return: NativeTypeName(\"{function.native_return_type}\")]")
        parameters = ", ".join(render_required_parameter(parameter) for parameter in function.parameters)
        lines.append(f"        public static extern {function.managed_return_type} {function.name}({parameters});")
        lines.append("")
    if lines[-1] == "":
        lines.pop()
    lines.extend(["    }", "}", ""])
    return "\n".join(lines)


def render_required_parameter(parameter: RequiredParameter) -> str:
    if parameter.native_type != parameter.managed_type:
        return f"[NativeTypeName(\"{parameter.native_type}\")] {parameter.managed_type} {parameter.name}"
    return f"{parameter.managed_type} {parameter.name}"
```

- [ ] **Step 2: Add required-surface emission helper**

Add this helper near `generate_platform_specific_headers`:

```python
def generate_required_sdlh_surface(repo: pathlib.Path, triplet: str, codegen: str, scope_root: pathlib.Path) -> int:
    allowlist_path = scope_root / "sdl2-core-sdlh-required.json"
    allowlist = read_required_surface_allowlist(allowlist_path)
    if allowlist.family != "sdl2-core":
        raise RuntimeError(f"Unsupported SDL.h required surface family: {allowlist.family}")
    header_path = repo / "vcpkg_installed" / triplet / "include" / "SDL2" / allowlist.header
    functions, constants = parse_required_sdlh_surface(header_path, allowlist)
    output_path = output_path_for_required_surface(repo, codegen, "core")
    output_path.parent.mkdir(parents=True, exist_ok=True)
    output_path.write_text(
        render_required_surface(FAMILY_CONFIG["core"]["namespace"], FAMILY_CONFIG["core"]["raw_class"], functions, constants),
        encoding="utf-8",
    )
    return 1
```

- [ ] **Step 3: Call required-surface emission before postprocess**

After the normal ClangSharp per-header loop and before the multi-OS pass comment, add:

```python
    if args.execute and "core" in selected and args.scope == "full":
        print("--- synthetic required SDL.h surface (core) ---")
        for codegen in codegen_passes:
            generated_count = generate_required_sdlh_surface(repo, args.vcpkg_triplet, codegen, scope_root)
            stats["core"]["generated_files"] += generated_count
```

This placement ensures the synthetic Modern file is present before `libraryimport` runs.

- [ ] **Step 4: Run generator self-test**

Run:

```pwsh
python spikes/binding-generators/clangsharp/generate_bindings.py --self-test
```

Expected: `self-test: PASS`.

- [ ] **Step 5: Run dry-run and confirm no synthetic files are emitted in dry-run**

Run:

```pwsh
python spikes/binding-generators/clangsharp/generate_bindings.py --scope full --family core --codegen both
```

Expected: command lines print; `SDL_required.g.cs` is not created because `--execute` is absent.

## Task 4: Add Manifest Parity Validation Without Using Manifest As Source

**Files:**
- Modify: `spikes/binding-generators/clangsharp/generate_bindings.py`

- [ ] **Step 1: Add manifest reader helpers**

Add these helpers near the required-surface parser helpers:

```python
def read_manifest_required_sdlh_names(repo: pathlib.Path) -> tuple[tuple[str, ...], tuple[str, ...]]:
    manifest = json.loads((repo / "build" / "manifest.json").read_text(encoding="utf-8"))
    for library in manifest["library_manifests"]:
        if library.get("name") == "SDL2" and library.get("core_lib") is True:
            generation = library["binding_generation"]
            functions = tuple(item["name"] for item in generation.get("required_functions", []) if item.get("source_header") == "SDL.h")
            constants = tuple(item["name"] for item in generation.get("required_constants", []) if item.get("source_header") == "SDL.h")
            return functions, constants
    raise RuntimeError("SDL2 Core binding_generation manifest entry was not found")


def validate_required_surface_names(
    manifest_functions: tuple[str, ...],
    manifest_constants: tuple[str, ...],
    allowlist: RequiredSurfaceAllowlist,
) -> None:
    if tuple(allowlist.functions) != manifest_functions:
        raise RuntimeError(
            "Spike SDL.h required functions differ from manifest required_functions: "
            f"spike={list(allowlist.functions)} manifest={list(manifest_functions)}"
        )
    if tuple(allowlist.constants) != manifest_constants:
        raise RuntimeError(
            "Spike SDL.h required constants differ from manifest required_constants: "
            f"spike={list(allowlist.constants)} manifest={list(manifest_constants)}"
        )


def validate_required_surface_against_manifest(repo: pathlib.Path, allowlist: RequiredSurfaceAllowlist) -> None:
    manifest_functions, manifest_constants = read_manifest_required_sdlh_names(repo)
    validate_required_surface_names(manifest_functions, manifest_constants, allowlist)
```

- [ ] **Step 2: Call parity validation from emission helper**

In `generate_required_sdlh_surface`, after loading the allowlist and checking family, add:

```python
    validate_required_surface_against_manifest(repo, allowlist)
```

- [ ] **Step 3: Add self-test coverage for mismatch detection**

Extend `run_self_tests()` with a direct validation check that tuple ordering matters:

```python
    wrong_order = RequiredSurfaceAllowlist("sdl2-core", "SDL.h", tuple(reversed(allowlist.functions)), allowlist.constants)
    try:
        validate_required_surface_names(allowlist.functions, allowlist.constants, wrong_order)
        failures.append("manifest parity validation did not reject reordered required functions")
    except RuntimeError:
        pass
```

The full repo manifest parity is covered by running generation against the real checkout in Task 6. Keep `run_self_tests()` filesystem-free.

- [ ] **Step 4: Run generator self-test**

Run:

```pwsh
python spikes/binding-generators/clangsharp/generate_bindings.py --self-test
```

Expected: `self-test: PASS`.

## Task 5: Update Oracle Self-Test For Computed Required Constants

**Files:**
- Modify: `spikes/binding-generators/clangsharp/oracle.cs`

- [ ] **Step 1: Add a const required constant to the fixture**

In `Fixtures.MixedGeneratedSource`, add this member inside the raw `SDLNative` fixture class near the existing `SDL_INIT_VIDEO` constant:

```csharp
[NativeTypeName("#define SDL_INIT_EVERYTHING SDL_INIT_TIMER | SDL_INIT_AUDIO | SDL_INIT_VIDEO")]
public const uint SDL_INIT_EVERYTHING = SDL_INIT_TIMER | SDL_INIT_AUDIO | SDL_INIT_VIDEO;
```

If the fixture does not already contain `SDL_INIT_TIMER` and `SDL_INIT_AUDIO`, add them as `public const uint` fields next to `SDL_INIT_VIDEO`.

- [ ] **Step 2: Add the self-test expectation**

In `SelfTests.Run()`, after the existing `SDL_INIT_VIDEO` constant expectation, add:

```csharp
Expect(evidence.Constants.Any(c => c.Name == "SDL_INIT_EVERYTHING" && c.Kind == "Field"), "extracts const computed required constants", failures);
```

- [ ] **Step 3: Run oracle self-test**

Run:

```pwsh
dotnet run --file spikes/binding-generators/clangsharp/oracle.cs -- --self-test
```

Expected: `self-test: PASS`.

If following strict TDD, add the expectation before the fixture member first and observe failure, then add the fixture member and rerun.

## Task 6: Regenerate And Verify Required Surface Recovery

**Files:**
- Regenerate: `spikes/binding-generators/clangsharp/src/Janset.SDL2.Core/Generated/**`
- Regenerate: `spikes/binding-generators/clangsharp/src/Janset.SDL2.Image/Generated/**`
- Regenerate: `spikes/binding-generators/output/reports/clangsharp-full.md`
- Regenerate: `spikes/binding-generators/output/reports/oracle-evidence-clangsharp.md`

- [ ] **Step 1: Run full generation**

Run:

```pwsh
python spikes/binding-generators/clangsharp/generate_bindings.py --scope full --codegen both --execute --clean-output --vcpkg-triplet x64-windows-hybrid --use-platform-header-shims
```

Expected:

- command exits `0`;
- `Generated/Compat/SDL_required.g.cs` exists;
- `Generated/Modern/SDL_required.g.cs` exists;
- normal full generation refreshes Core and Image generated output;
- existing ClangSharp per-header failures in `clangsharp-full.md` may remain at the known baseline count, but no postprocess failure occurs.

- [ ] **Step 2: Inspect generated Compat file**

Run:

```pwsh
rg -n "SDL_INIT_EVERYTHING|SDL_Init|DllImport|LibraryImport" spikes/binding-generators/clangsharp/src/Janset.SDL2.Core/Generated/Compat/SDL_required.g.cs
```

Expected:

- constants are present;
- `SDL_Init` is present;
- `[DllImport("SDL2"` is present;
- `[LibraryImport` is absent.

- [ ] **Step 3: Inspect generated Modern file**

Run:

```pwsh
rg -n "SDL_INIT_EVERYTHING|SDL_Init|DllImport|LibraryImport|UnmanagedCallConv" spikes/binding-generators/clangsharp/src/Janset.SDL2.Core/Generated/Modern/SDL_required.g.cs
```

Expected:

- constants are present;
- `SDL_Init` is present;
- `[LibraryImport("SDL2"` is present;
- `[UnmanagedCallConv` is present;
- `[DllImport` is absent.

- [ ] **Step 4: Run oracle report**

Run:

```pwsh
dotnet run --file spikes/binding-generators/clangsharp/oracle.cs -- --family sdl2-core --family sdl2-image --write-report
```

Expected: command exits `0` and writes `spikes/binding-generators/output/reports/oracle-evidence-clangsharp.md`.

- [ ] **Step 5: Verify B-slice findings are gone**

Run:

```pwsh
rg -n "required-function-missing|required-constant-missing" spikes/binding-generators/output/reports/oracle-evidence-clangsharp.md
```

Expected: no matches and `rg` exit code `1`.

- [ ] **Step 6: Verify C-slice findings remain visible**

Run:

```pwsh
rg -n "deferred-layout|platform-sensitive" spikes/binding-generators/output/reports/oracle-evidence-clangsharp.md
```

Expected: matches remain for deferred layouts and platform-sensitive scalar risks.

## Task 7: Build And Anti-Slop Verification

**Files:**
- No additional edits expected.

- [ ] **Step 1: Run generator self-test**

Run:

```pwsh
python spikes/binding-generators/clangsharp/generate_bindings.py --self-test
```

Expected: `self-test: PASS`.

- [ ] **Step 2: Run oracle self-test**

Run:

```pwsh
dotnet run --file spikes/binding-generators/clangsharp/oracle.cs -- --self-test
```

Expected: `self-test: PASS`.

- [ ] **Step 3: Build Core spike project**

Run:

```pwsh
dotnet build spikes/binding-generators/clangsharp/src/Janset.SDL2.Core/Janset.SDL2.Core.csproj -c Release
```

Expected: `Build succeeded.` with `0 Warning(s)` and `0 Error(s)`.

- [ ] **Step 4: Build Image spike project**

Run:

```pwsh
dotnet build spikes/binding-generators/clangsharp/src/Janset.SDL2.Image/Janset.SDL2.Image.csproj -c Release
```

Expected: `Build succeeded.` with `0 Warning(s)` and `0 Error(s)`.

- [ ] **Step 5: Run whitespace check**

Run:

```pwsh
git diff --check
```

Expected: no whitespace errors. CRLF/LF warnings from generated files are acceptable if no error lines are reported.

- [ ] **Step 6: Run Slopwatch**

Run:

```pwsh
slopwatch analyze --fail-on warning --exclude "artifacts/**,external/**,vcpkg_installed/**,spikes/binding-generators/references/**,**/bin/**,**/obj/**"
```

Expected: `Scan complete: 0 issue(s) found`.

## Task 8: Final Review And Commit Approval Gate

**Files:**
- Review all changed files.

- [ ] **Step 1: Inspect status and diff**

Run:

```pwsh
git status --short
git diff --stat
```

Expected: changes are limited to the spike allowlist, spike generator/oracle, generated spike output, and spike reports.

- [ ] **Step 2: Request final review**

Use a fresh reviewer subagent with this checklist:

```text
Review B-slice: SDL.h required surface recovery.
Check that generation source is spike-local allowlist, signatures/values come from installed SDL.h, manifest is validation only, Modern output is LibraryImport, required missing findings are gone, C findings remain, and no production/manifest/CI/package files changed.
```

Expected: reviewer returns `APPROVED` or concrete findings. Fix approved findings before continuing.

- [ ] **Step 3: Present commit approval request**

Do not commit automatically. Present:

```text
Summary:
- Added spike-local SDL.h required-surface allowlist.
- Generated SDL_required.g.cs for Compat/Modern from constrained SDL.h parsing.
- Verified Modern LibraryImport conversion.
- Updated oracle evidence so required SDL.h functions/constants are no longer missing.

Proposed commit message:
feat(binding-spike): recover SDL.h required surface
```

Ask for explicit approval before committing, per `AGENTS.md`.
