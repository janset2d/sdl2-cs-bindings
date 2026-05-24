import argparse
import json
import pathlib
import re
import shutil
import subprocess
import sys
import tempfile
from dataclasses import dataclass


@dataclass(frozen=True)
class EmptyGeneratedOutput:
    header_path: pathlib.Path
    output_path: pathlib.Path
    command_line: str


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


def platform_header_shim_root(repo: pathlib.Path) -> pathlib.Path:
    return repo / "spikes" / "binding-generators" / "clangsharp" / "shims" / "platform-headers"


def per_header_rsp_path(repo: pathlib.Path, header_name: str) -> pathlib.Path | None:
    """Return path to per-header RSP if it exists, else None.

    header_name MUST be a bare basename like 'SDL_audio.h' (no directory
    component). Looks under
    spikes/binding-generators/clangsharp/rsp/per-header/<basename>.rsp where
    <basename> is the header name with the .h extension stripped, so
    'SDL_audio.h' maps to 'SDL_audio.rsp'. Passing a path-qualified header
    name (e.g. 'foo/SDL_audio.h') would silently still resolve via
    pathlib.Path.stem and mask a caller mistake, so it is rejected up front.

    Implements the third RSP tier in Decision 4 of the Priority C semantic-ABI
    design (docs/superpowers/specs/2026-05-24-clangsharp-priority-c-semantic-abi-design.md
    — see "Decision 4 — Per-Header RSP Organization (ppy Alignment)"). Mirrors
    the per-header RSP pattern used by ppy/SDL3-CS generate_bindings.py:318-320.

    Returning the path lets callers feed it through the @<path> response-file
    syntax that ClangSharp already accepts for base.rsp and the family RSP.
    """
    assert "/" not in header_name and "\\" not in header_name, (
        f"header_name must be a bare basename, got: {header_name}"
    )
    basename = pathlib.Path(header_name).stem
    candidate = (
        repo / "spikes" / "binding-generators" / "clangsharp"
        / "rsp" / "per-header" / f"{basename}.rsp"
    )
    return candidate if candidate.is_file() else None


def extend_rsp_arguments(
    command: list[str],
    repo: pathlib.Path,
    family: str,
    header: str,
) -> None:
    """Append the three-tier RSP @-arguments to the ClangSharp command.

    Loads, in order, base.rsp (cross-cutting policy), the family RSP (family
    identity), and the per-header RSP if one exists. RSP precedence under
    ClangSharp is "last write wins for keyed entries; lists like --exclude
    accumulate", so the order base → family → per-header lets per-header
    files override family-level keyed entries when needed.

    Consolidates the wiring shared by `command_for_header` and
    `platform_command_for_header` into a single place so future RSP-policy
    changes (additional tiers, ordering tweaks) are made once. Decision 4 of
    the Priority C semantic-ABI design (docs/superpowers/specs/
    2026-05-24-clangsharp-priority-c-semantic-abi-design.md) drives the
    three-tier organization.
    """
    rsp_root = repo / "spikes" / "binding-generators" / "clangsharp" / "rsp"
    command.append(f"@{rsp_root / 'base.rsp'}")
    command.append(f"@{rsp_root / FAMILY_CONFIG[family]['rsp']}")
    per_header_rsp = per_header_rsp_path(repo, header)
    if per_header_rsp is not None:
        command.append(f"@{per_header_rsp}")


def find_repository_root() -> pathlib.Path:
    current = pathlib.Path(__file__).resolve()
    for parent in [current, *current.parents]:
        if (parent / "tools.cs").is_file():
            return parent
    raise RuntimeError("Repository root was not found from generate_bindings.py")


def read_scope(scope_file: pathlib.Path) -> list[str]:
    headers: list[str] = []
    for line in scope_file.read_text(encoding="utf-8").splitlines():
        stripped = line.strip()
        if not stripped or stripped.startswith("#"):
            continue
        headers.append(stripped)
    return headers


def read_required_surface_allowlist(path: pathlib.Path) -> RequiredSurfaceAllowlist:
    data = json.loads(path.read_text(encoding="utf-8"))
    return RequiredSurfaceAllowlist(
        family=str(data["family"]),
        header=str(data["header"]),
        functions=tuple(str(name) for name in data["functions"]),
        constants=tuple(str(name) for name in data["constants"]),
    )


def read_manifest_required_sdlh_names(repo: pathlib.Path) -> tuple[tuple[str, ...], tuple[str, ...]]:
    data = json.loads((repo / "build" / "manifest.json").read_text(encoding="utf-8"))
    for library in data["library_manifests"]:
        if library.get("name") == "SDL2" and library.get("core_lib") is True:
            binding_generation = library["binding_generation"]
            functions = tuple(
                str(function["name"])
                for function in binding_generation["required_functions"]
                if function.get("source_header") == "SDL.h"
            )
            constants = tuple(
                str(constant["name"])
                for constant in binding_generation["required_constants"]
                if constant.get("source_header") == "SDL.h"
            )
            return functions, constants

    raise RuntimeError("SDL2 Core binding_generation manifest entry was not found")


def validate_required_surface_names(
    manifest_functions: tuple[str, ...],
    manifest_constants: tuple[str, ...],
    allowlist: RequiredSurfaceAllowlist,
) -> None:
    spike_functions = tuple(allowlist.functions)
    if spike_functions != manifest_functions:
        raise RuntimeError(
            f"SDL.h required function manifest parity mismatch: spike={list(spike_functions)} manifest={list(manifest_functions)}"
        )

    spike_constants = tuple(allowlist.constants)
    if spike_constants != manifest_constants:
        raise RuntimeError(
            f"SDL.h required constant manifest parity mismatch: spike={list(spike_constants)} manifest={list(manifest_constants)}"
        )


def validate_required_surface_against_manifest(repo: pathlib.Path, allowlist: RequiredSurfaceAllowlist) -> None:
    manifest_functions, manifest_constants = read_manifest_required_sdlh_names(repo)
    validate_required_surface_names(manifest_functions, manifest_constants, allowlist)


def should_validate_required_sdlh_surface(execute: bool, selected: list[str], scope: str) -> bool:
    return execute and "core" in selected and scope == "full"


FUNCTION_DECLARATION_PATTERN = re.compile(
    r"^extern\s+DECLSPEC\s+(?P<return_type>.+?)\s+SDLCALL\s+(?P<name>SDL_\w+)\((?P<parameters>.*?)\);$"
)
MACRO_PATTERN = re.compile(r"^#define\s+(?P<name>SDL_INIT_\w+)\s+(?P<value>.+)$")


def parse_required_sdlh_surface(
    header_path: pathlib.Path,
    allowlist: RequiredSurfaceAllowlist,
) -> tuple[list[RequiredFunction], list[RequiredConstant]]:
    text = header_path.read_text(encoding="utf-8")
    return (
        parse_required_sdlh_functions(text, allowlist.functions),
        parse_required_sdlh_constants(text, allowlist.constants),
    )


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
        if name not in allowed:
            continue

        native_return_type = match.group("return_type")
        discovered[name] = RequiredFunction(
            name=name,
            native_return_type=native_return_type,
            managed_return_type=map_sdlh_type(native_return_type),
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
        if name not in allowed:
            continue

        value = normalize_macro_value(match.group("value"))
        discovered[name] = RequiredConstant(
            name=name,
            raw_value=value,
            managed_value=value,
            kind="Computed" if "|" in value else "Literal",
            native_type_name=f"#define {name} {value}",
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
    value_without_comments = re.sub(r"/\*.*?\*/", "", value)
    normalized = " ".join(value_without_comments.strip().split())
    if normalized.startswith("(") and normalized.endswith(")"):
        normalized = normalized[1:-1].strip()
    return re.sub(r"\b(0x[0-9A-Fa-f]+)u\b", lambda match: match.group(1) + "U", normalized)


def map_sdlh_type(native_type: str) -> str:
    return {
        "void": "void",
        "int": "int",
        "Uint32": "uint",
    }.get(native_type, native_type)


FAMILY_CONFIG = {
    "core": {
        "namespace": "SDL2",
        "raw_class": "SDLNative",
        "rsp": "sdl2-core.rsp",
        "bootstrap_scope": "bootstrap-sdl2-core.headers.txt",
        "full_scope": "sdl2-core.headers.txt",
        "library_dir": "Janset.SDL2.Core",
    },
    "image": {
        "namespace": "SDL2.Image",
        "raw_class": "SDL_imageNative",
        "rsp": "sdl2-image.rsp",
        "bootstrap_scope": "bootstrap-sdl2-image.headers.txt",
        "full_scope": "sdl2-image.headers.txt",
        "library_dir": "Janset.SDL2.Image",
    },
}

# ClangSharp config presets per codegen target. compatible-codegen produces
# netstandard2.0-compatible output (no InlineArray, no UTF-8 u8 literal, no
# delegate* function pointers); latest-codegen produces .NET 10 / C# 14
# output. exclude-fnptr-codegen + latest gives modern types except function
# pointers — kept for the modern pass so structs with fn-pointer fields stay
# representable as nint instead of delegate*<...>, which is the only modern
# construct that would still trip netstandard2.0 if it leaked there. (We do
# not need that knob for the compat pass; compatible-codegen already drops
# function pointers.)
CODEGEN_CONFIG = {
    "compat": ["compatible-codegen", "windows-types", "generate-macro-bindings"],
    "modern": ["latest-codegen", "windows-types", "generate-macro-bindings"],
}


# Adapted verbatim from
# build/_build/Targets/GenerateBindings/PlatformViews/PlatformCatalog.cs:18-47
# (Cake `AllPlatformMacros`). Every per-platform parse pass must undefine the
# macros that don't belong to its view so the SDL2 headers don't pick up the
# host parse target's defaults (the spike runs against `x64-windows-hybrid`,
# which would otherwise leak `_WIN32` into every view including Linux/Android).
ALL_PLATFORM_MACROS = [
    "_WIN32",
    "WIN32",
    "__WIN32__",
    "__WINDOWS__",
    "__WINRT__",
    "__GDK__",
    "__WINGDK__",
    "linux",
    "__linux",
    "__linux__",
    "__LINUX__",
    "__APPLE__",
    "__MACOSX__",
    "__IPHONEOS__",
    "__ANDROID__",
    "SDL_VIDEO_DRIVER_WINDOWS",
    "SDL_VIDEO_DRIVER_WINRT",
    "SDL_VIDEO_DRIVER_X11",
    "SDL_VIDEO_DRIVER_WAYLAND",
    "SDL_VIDEO_DRIVER_KMSDRM",
    "SDL_VIDEO_DRIVER_COCOA",
    "SDL_VIDEO_DRIVER_UIKIT",
    "SDL_VIDEO_DRIVER_ANDROID",
    "SDL_VIDEO_DRIVER_DIRECTFB",
    "SDL_VIDEO_DRIVER_VIVANTE",
    "SDL_VIDEO_DRIVER_MIR",
    "SDL_VIDEO_DRIVER_OS2",
]


# Adapted from PlatformCatalog.CreateSdl2Catalog() at the same path. Each tuple
# is (view_name, supported_os_platform_string, defines_with_values). The
# undefine set is derived as (ALL_PLATFORM_MACROS minus the macros named in
# `defines`). Suffix strings are canonical .NET [SupportedOSPlatform] tokens
# from Microsoft.NET.SupportedPlatforms — see PlatformCatalog.cs comments for
# why "windows10.0.10240.0" is used for WinRT and not "winrt".
SDL2_PLATFORM_VIEWS: list[tuple[str, str, list[str]]] = [
    ("WindowsDesktop", "windows", [
        "_WIN32=1", "WIN32=1", "__WIN32__=1", "__WINDOWS__=1",
        "SDL_VIDEO_DRIVER_WINDOWS=1",
    ]),
    ("WinRT", "windows10.0.10240.0", [
        "_WIN32=1", "__WINRT__=1", "SDL_VIDEO_DRIVER_WINRT=1",
    ]),
    ("GDK", "windows", [
        "_WIN32=1", "__GDK__=1", "__WINGDK__=1",
        "SDL_VIDEO_DRIVER_WINDOWS=1",
    ]),
    ("Linux", "linux", [
        "linux=1", "__linux=1", "__linux__=1", "__LINUX__=1",
        "SDL_VIDEO_DRIVER_X11=1", "SDL_VIDEO_DRIVER_WAYLAND=1",
        "SDL_VIDEO_DRIVER_KMSDRM=1",
    ]),
    ("MacOS", "macos", [
        "__APPLE__=1", "__MACOSX__=1",
        # SDL_platform.h requires Mac OS X >= 10.7 — encode the deployment
        # target define here so the synthetic parse satisfies the #error
        # check that real Apple toolchains would satisfy via
        # <AvailabilityMacros.h>.
        "MAC_OS_X_VERSION_MIN_REQUIRED=1070",
        "SDL_VIDEO_DRIVER_COCOA=1",
    ]),
    ("IOS", "ios", [
        "__APPLE__=1", "__IPHONEOS__=1",
        # TARGET_OS_IPHONE=1 routes SDL_platform.h into the iOS branch
        # which self-defines __IPHONEOS__ and skips the macOS deployment
        # target #error.
        "TARGET_OS_IPHONE=1",
        "SDL_VIDEO_DRIVER_UIKIT=1",
    ]),
    ("Android", "android", [
        "__ANDROID__=1", "SDL_VIDEO_DRIVER_ANDROID=1",
    ]),
]


# Header subset that needs the multi-OS pass. The Explore-agent scan
# (2026-05-21) of every SDL2.Core header showed only these two have public
# function or struct shapes that differ per platform. SDL_syswm.h is Stage 1
# quarantined (Constitution L292-294); SDL_platform.h is macro-only. Every
# other header is platform-neutral at the public API surface.
PLATFORM_SENSITIVE_HEADERS: dict[str, list[str]] = {
    "core": [
        "SDL_main.h",
        "SDL_system.h",
    ],
    "image": [],
}


# Captures top-level SDL_* declaration names from true-neutral output. Platform
# passes exclude these so common declarations stay emitted once while
# Platforms/<View>/ files carry only the platform delta. A later Roslyn
# postprocess removes any remaining cross-platform duplicates and adds guarded
# SupportedOSPlatform attributes by output path.
_NEUTRAL_SYMBOL_RE = re.compile(
    r"^\s*(?:\[[^\]]+\]\s*)*public[^(;]*?\b(SDL_\w+)\s*[({=;]",
    re.MULTILINE,
)


def extract_neutral_symbols(neutral_output: pathlib.Path) -> list[str]:
    """Read a true-neutral .g.cs and return SDL_* declaration names that
    ClangSharp already emitted. Per-platform passes exclude these so common
    declarations stay in the neutral file."""
    if not neutral_output.is_file():
        return []
    text = neutral_output.read_text(encoding="utf-8")
    seen: list[str] = []
    seen_set: set[str] = set()
    for match in _NEUTRAL_SYMBOL_RE.finditer(text):
        name = match.group(1)
        if name not in seen_set:
            seen.append(name)
            seen_set.add(name)
    return seen


def is_empty_generated_output(output_path: pathlib.Path) -> bool:
    return output_path.is_file() and output_path.stat().st_size == 0


def had_fatal_parse_failure(output: str, input_file: pathlib.Path) -> bool:
    return "fatal error:" in output or f"Skipping '{input_file}' due to one or more errors listed above." in output


def platform_output_path(repo: pathlib.Path, codegen: str, family: str, header: str, view_name: str) -> pathlib.Path:
    subdir = "Compat" if codegen == "compat" else "Modern"
    library_dir = FAMILY_CONFIG[family]["library_dir"]
    return (
        repo / "spikes" / "binding-generators" / "clangsharp" / "src" / library_dir
        / "Generated" / subdir / "Platforms" / view_name / (pathlib.Path(header).stem + ".g.cs")
    )


def output_path_for_required_surface(repo: pathlib.Path, codegen: str, family: str) -> pathlib.Path:
    subdir = "Compat" if codegen == "compat" else "Modern"
    library_dir = FAMILY_CONFIG[family]["library_dir"]
    return repo / "spikes" / "binding-generators" / "clangsharp" / "src" / library_dir / "Generated" / subdir / "SDL_required.g.cs"


def render_required_parameter(parameter: RequiredParameter) -> str:
    prefix = ""
    if parameter.native_type != parameter.managed_type:
        prefix = f'[NativeTypeName("{parameter.native_type}")] '
    return f"{prefix}{parameter.managed_type} {parameter.name}"


def render_required_surface(
    namespace: str,
    raw_class: str,
    functions: list[RequiredFunction],
    constants: list[RequiredConstant],
) -> str:
    lines = [
        "using System.Runtime.InteropServices;",
        "",
        f"namespace {namespace}",
        "{",
        f"    internal static unsafe partial class {raw_class}",
        "    {",
    ]

    for constant in constants:
        lines.append(f'        [NativeTypeName("{constant.native_type_name}")]')
        lines.append(f"        public const uint {constant.name} = {constant.managed_value};")
        lines.append("")

    for index, function in enumerate(functions):
        lines.append('        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]')
        if function.native_return_type != function.managed_return_type:
            lines.append(f'        [return: NativeTypeName("{function.native_return_type}")]')
        rendered_parameters = ", ".join(render_required_parameter(parameter) for parameter in function.parameters)
        lines.append(f"        public static extern {function.managed_return_type} {function.name}({rendered_parameters});")
        if index != len(functions) - 1:
            lines.append("")

    lines.extend([
        "    }",
        "}",
        "",
    ])
    return "\n".join(lines)


def generate_required_sdlh_surface(repo: pathlib.Path, triplet: str, codegen: str, scope_root: pathlib.Path) -> int:
    allowlist = read_required_surface_allowlist(scope_root / "sdl2-core-sdlh-required.json")
    if allowlist.family != "sdl2-core":
        raise RuntimeError(f"SDL.h required surface only supports family sdl2-core, not {allowlist.family}")

    validate_required_surface_against_manifest(repo, allowlist)

    header_path = repo / "vcpkg_installed" / triplet / "include" / "SDL2" / allowlist.header
    functions, constants = parse_required_sdlh_surface(header_path, allowlist)
    output_path = output_path_for_required_surface(repo, codegen, "core")
    output_path.parent.mkdir(parents=True, exist_ok=True)
    output_path.write_text(
        render_required_surface(
            FAMILY_CONFIG["core"]["namespace"],
            FAMILY_CONFIG["core"]["raw_class"],
            functions,
            constants,
        ),
        encoding="utf-8",
    )
    return 1


def platform_command_for_header(
    repo: pathlib.Path,
    triplet: str,
    codegen: str,
    family: str,
    header: str,
    view_name: str,
    view_defines: list[str],
    neutral_symbols: list[str],
    use_platform_header_shims: bool,
) -> tuple[list[str], pathlib.Path]:
    """ClangSharp invocation for a single (header, codegen, platform-view)
    triple. Mirrors ppy's `generate_platform_specific_headers` shape:
      * base/family RSP for shared policy
      * per-view --define-macro block
      * cross-contamination --additional --undefine-macro for every macro in
        ALL_PLATFORM_MACROS that this view does NOT define
      * --exclude for every symbol the neutral pass already produced
    SupportedOSPlatform attribution is intentionally not passed to ClangSharp.
    The Roslyn postprocess owns path-based platform annotation and TFM guards."""
    include_root = repo / "vcpkg_installed" / triplet / "include" / "SDL2"
    input_file = include_root / header
    output_path = platform_output_path(repo, codegen, family, header, view_name)

    defined_names = {value.split("=", 1)[0] for value in view_defines}
    undefines = [macro for macro in ALL_PLATFORM_MACROS if macro not in defined_names]

    command: list[str] = [
        "dotnet", "tool", "run", "ClangSharpPInvokeGenerator",
        "--config",
    ]
    command.extend(CODEGEN_CONFIG[codegen])
    extend_rsp_arguments(command, repo, family, header)
    command.extend([
        "--namespace", FAMILY_CONFIG[family]["namespace"],
        "--with-access-specifier", f"{FAMILY_CONFIG[family]['raw_class']}=Internal",
        "--include-directory", str(include_root),
    ])
    if use_platform_header_shims:
        command.extend(["--include-directory", str(platform_header_shim_root(repo))])
    command.extend([
        "--file", str(input_file),
        "--output", str(output_path),
        "--define-macro",
    ])
    command.extend(view_defines)

    if undefines:
        command.append("--additional")
        for macro in undefines:
            command.append(f"--undefine-macro={macro}")

    if neutral_symbols:
        command.append("--exclude")
        command.extend(neutral_symbols)

    return command, output_path


def generate_platform_specific_headers(
    repo: pathlib.Path,
    triplet: str,
    codegen: str,
    family: str,
    header: str,
    spike_root: pathlib.Path,
    use_platform_header_shims: bool,
) -> tuple[int, list[tuple[pathlib.Path, str, int]], list[EmptyGeneratedOutput]]:
    """Adapt of ppy/SDL3-CS generate_bindings.py:341-365 for SDL2 macros.
    Runs one ClangSharp invocation per platform view after the true-neutral
    pass has emitted Generated/<Codegen>/SDL_<header>.g.cs. The per-view output
    is cleaned up and annotated by the Roslyn platform-delta postprocess.
    """
    neutral_path = output_path_for_header(repo, codegen, family, header)
    neutral_symbols = extract_neutral_symbols(neutral_path)
    include_root = repo / "vcpkg_installed" / triplet / "include" / "SDL2"
    input_file = include_root / header

    commands_run = 0
    failures: list[tuple[pathlib.Path, str, int]] = []
    empty_outputs: list[EmptyGeneratedOutput] = []

    for view_name, supported_os, defines in SDL2_PLATFORM_VIEWS:
        command, output_path = platform_command_for_header(
            repo, triplet, codegen, family, header, view_name, defines, neutral_symbols, use_platform_header_shims
        )
        commands_run += 1
        command_line = format_command(command)
        output_path.parent.mkdir(parents=True, exist_ok=True)
        if output_path.exists():
            output_path.unlink()
        print(command_line)
        result = subprocess.run(command, cwd=spike_root, capture_output=True, text=True)
        diagnostic_output = "\n".join(part for part in [result.stdout, result.stderr] if part)
        if result.returncode != 0:
            failures.append((input_file, command_line, result.returncode))
            if result.stdout:
                print(f"  STDOUT ({view_name}):\n{result.stdout.rstrip()}")
            if result.stderr:
                print(f"  STDERR ({view_name}):\n{result.stderr.rstrip()}")
        if is_empty_generated_output(output_path) and had_fatal_parse_failure(diagnostic_output, input_file):
            empty_outputs.append(EmptyGeneratedOutput(input_file, output_path, command_line))

    return commands_run, failures, empty_outputs


def output_path_for_header(repo: pathlib.Path, codegen: str, family: str, header: str) -> pathlib.Path:
    subdir = "Compat" if codegen == "compat" else "Modern"
    library_dir = FAMILY_CONFIG[family]["library_dir"]
    return repo / "spikes" / "binding-generators" / "clangsharp" / "src" / library_dir / "Generated" / subdir / (pathlib.Path(header).stem + ".g.cs")


def generated_root_for_family(repo: pathlib.Path, family: str) -> pathlib.Path:
    library_dir = FAMILY_CONFIG[family]["library_dir"]
    return repo / "spikes" / "binding-generators" / "clangsharp" / "src" / library_dir / "Generated"


def modern_root_for_family(repo: pathlib.Path, family: str) -> pathlib.Path:
    return generated_root_for_family(repo, family) / "Modern"


def run_postprocess(repo: pathlib.Path, family: str, spike_root: pathlib.Path, mode: str, codegen: str) -> int:
    """Invoke the Microsoft.CodeAnalysis postprocess. mode is one of:
    - 'strip-varargs'     drops __arglist per Constitution L162-176 fmt-only policy
    - 'libraryimport'     promotes DllImport -> LibraryImport per Constitution L48 backend split
    - 'platform-delta'    SDL2 platform-view pass cleanup
    - 'guid-substitute'   Slice C-C: SDL_GUID -> System.Guid (16-byte wire-identical)
    - 'threadid-dispatch' Slice C-A R2 structural: SDL_threadID family hybrid TFM emit
    - 'uniform-opaque'    Slice C-B Pattern B opaque handle emit + pointer-to-by-value rewrites
    codegen selects the Generated/<Codegen>/ subtree to operate on."""
    subdir = "Compat" if codegen == "compat" else "Modern"
    target_dir = generated_root_for_family(repo, family) / subdir
    if not target_dir.is_dir():
        return 0
    postprocess_csproj = repo / "spikes" / "binding-generators" / "clangsharp" / "postprocess" / "Janset.SDL2.PostProcess.csproj"
    cmd = [
        "dotnet", "run",
        "--project", str(postprocess_csproj),
        "-c", "Release",
        "--", mode, str(target_dir),
    ]
    print(" ".join(cmd))
    result = subprocess.run(cmd, cwd=spike_root)
    return result.returncode


def command_for_header(
    repo: pathlib.Path,
    triplet: str,
    codegen: str,
    family: str,
    header: str,
    use_platform_header_shims: bool,
) -> tuple[list[str], pathlib.Path]:
    include_root = repo / "vcpkg_installed" / triplet / "include" / "SDL2"
    input_file = include_root / header
    output_path = output_path_for_header(repo, codegen, family, header)

    command: list[str] = [
        "dotnet", "tool", "run", "ClangSharpPInvokeGenerator",
        "--config",
    ]
    command.extend(CODEGEN_CONFIG[codegen])
    extend_rsp_arguments(command, repo, family, header)
    command.extend([
        "--namespace", FAMILY_CONFIG[family]["namespace"],
        "--with-access-specifier", f"{FAMILY_CONFIG[family]['raw_class']}=Internal",
        "--include-directory", str(include_root),
    ])
    if use_platform_header_shims and header in PLATFORM_SENSITIVE_HEADERS.get(family, []):
        command.extend(["--include-directory", str(platform_header_shim_root(repo))])
    command.extend([
        "--file", str(input_file),
        "--output", str(output_path),
    ])
    if header in PLATFORM_SENSITIVE_HEADERS.get(family, []):
        command.append("--additional")
        for macro in ALL_PLATFORM_MACROS:
            command.append(f"--undefine-macro={macro}")
    return command, output_path


def format_command(command: list[str]) -> str:
    return " ".join(f'\"{part}\"' if " " in part else part for part in command)


def selected_families(family: str) -> list[str]:
    if family == "all":
        return ["core", "image"]
    return [family]


def scope_file_name(scope: str, family: str) -> str:
    key = "bootstrap_scope" if scope == "bootstrap" else "full_scope"
    return FAMILY_CONFIG[family][key]


def write_report(
    reports_root: pathlib.Path,
    scope: str,
    triplet: str,
    mode: str,
    selected: list[str],
    stats: dict[str, dict[str, int]],
    failures: list[tuple[pathlib.Path, str, int]],
    empty_outputs: list[EmptyGeneratedOutput],
    use_platform_header_shims: bool,
) -> None:
    reports_root.mkdir(parents=True, exist_ok=True)
    report_path = reports_root / f"clangsharp-{scope}.md"
    lines = [
        f"# ClangSharp {scope} Report",
        "",
        f"**Triplet:** {triplet}",
        f"**Mode:** {mode}",
        f"**Platform header shims:** {'enabled' if use_platform_header_shims else 'disabled'}",
        "",
        "| Family | Headers | Commands | Generated Files |",
        "| --- | ---: | ---: | ---: |",
    ]

    for family in ["core", "image"]:
        family_stats = stats[family]
        lines.append(
            f"| {family} | {family_stats['headers']} | {family_stats['commands']} | {family_stats['generated_files']} |"
        )

    lines.extend(["", "## Selection", "", f"Selected families: {', '.join(selected)}", "", "## Failures", ""])
    if failures:
        for header_path, command_line, exit_code in failures:
            lines.extend([
                f"- Header: `{header_path}`",
                f"- Command: `{command_line}`",
                f"- Exit code: `{exit_code}`",
            ])
            lines.append("")
    else:
        lines.append("No failures recorded.")

    lines.extend(["", "## Empty Generated Outputs", ""])
    if empty_outputs:
        for empty_output in empty_outputs:
            lines.extend([
                f"- Header: `{empty_output.header_path}`",
                f"- Output: `{empty_output.output_path}`",
                f"- Command: `{empty_output.command_line}`",
            ])
            lines.append("")
    else:
        lines.append("No empty generated outputs recorded.")

    report_path.write_text("\n".join(lines) + "\n", encoding="utf-8")


def run_self_tests() -> int:
    allowlist = RequiredSurfaceAllowlist(
        "sdl2-core",
        "SDL.h",
        ("SDL_Init", "SDL_InitSubSystem", "SDL_QuitSubSystem", "SDL_WasInit", "SDL_Quit"),
        (
            "SDL_INIT_TIMER",
            "SDL_INIT_AUDIO",
            "SDL_INIT_VIDEO",
            "SDL_INIT_JOYSTICK",
            "SDL_INIT_HAPTIC",
            "SDL_INIT_GAMECONTROLLER",
            "SDL_INIT_EVENTS",
            "SDL_INIT_SENSOR",
            "SDL_INIT_NOPARACHUTE",
            "SDL_INIT_EVERYTHING",
        ),
    )
    fixture = r"""
#define SDL_INIT_TIMER          0x00000001u
#define SDL_INIT_AUDIO          0x00000010u
#define SDL_INIT_VIDEO          0x00000020u /**< SDL_INIT_VIDEO implies SDL_INIT_EVENTS */
#define SDL_INIT_JOYSTICK       0x00000200u /* joystick support */
#define SDL_INIT_HAPTIC         0x00001000u
#define SDL_INIT_GAMECONTROLLER 0x00002000u /**< game controller support */
#define SDL_INIT_EVENTS         0x00004000u
#define SDL_INIT_SENSOR         0x00008000u
#define SDL_INIT_NOPARACHUTE    0x00100000u /**< compatibility; this flag is ignored. */
#define SDL_INIT_EVERYTHING ( \
                SDL_INIT_TIMER | SDL_INIT_AUDIO | SDL_INIT_VIDEO | SDL_INIT_EVENTS | \
                SDL_INIT_JOYSTICK | SDL_INIT_HAPTIC | SDL_INIT_GAMECONTROLLER | SDL_INIT_SENSOR \
            )
extern DECLSPEC int SDLCALL SDL_Init(Uint32 flags);
extern DECLSPEC int SDLCALL SDL_InitSubSystem(Uint32 flags);
extern DECLSPEC void SDLCALL SDL_QuitSubSystem(Uint32 flags);
extern DECLSPEC Uint32 SDLCALL SDL_WasInit(Uint32 flags);
extern DECLSPEC void SDLCALL SDL_Quit(void);
"""
    failures: list[str] = []

    functions = parse_required_sdlh_functions(fixture, allowlist.functions)
    if [function.name for function in functions] != list(allowlist.functions):
        failures.append("function order did not match allowlist")
    if functions[0].parameters != (RequiredParameter("Uint32", "uint", "flags"),):
        failures.append("Uint32 flags parameter was not mapped to uint flags")
    if functions[3].managed_return_type != "uint":
        failures.append("Uint32 return type was not mapped to uint")

    constants = parse_required_sdlh_constants(fixture, allowlist.constants)
    if [constant.name for constant in constants] != list(allowlist.constants):
        failures.append("constant order did not match allowlist")
    if constants[-1].kind != "Computed":
        failures.append("SDL_INIT_EVERYTHING was not parsed as computed")
    if "SDL_INIT_GAMECONTROLLER" not in constants[-1].managed_value:
        failures.append("SDL_INIT_EVERYTHING did not retain identifier text")
    constants_by_name = {constant.name: constant for constant in constants}
    if constants_by_name["SDL_INIT_VIDEO"].managed_value != "0x00000020U":
        failures.append("SDL_INIT_VIDEO comment text was not stripped before numeric normalization")
    for name in ("SDL_INIT_VIDEO", "SDL_INIT_JOYSTICK", "SDL_INIT_GAMECONTROLLER", "SDL_INIT_NOPARACHUTE"):
        if "/*" in constants_by_name[name].managed_value or "*/" in constants_by_name[name].managed_value:
            failures.append(f"{name} managed value retained block comment text")

    rendered = render_required_surface("SDL2", "SDLNative", functions, constants)
    if "internal static unsafe partial class SDLNative" not in rendered:
        failures.append("rendered output did not contain the SDLNative raw class declaration")
    if "public const uint SDL_INIT_EVERYTHING" not in rendered:
        failures.append("rendered output did not contain computed SDL_INIT_EVERYTHING")
    if "[DllImport(\"SDL2\", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]" not in rendered:
        failures.append("rendered output did not contain the SDL2 DllImport attribute")
    if "public static extern int SDL_Init([NativeTypeName(\"Uint32\")] uint flags);" not in rendered:
        failures.append("rendered output did not contain the SDL_Init extern signature")

    wrong_order = RequiredSurfaceAllowlist(
        "sdl2-core",
        "SDL.h",
        tuple(reversed(allowlist.functions)),
        allowlist.constants,
    )
    try:
        validate_required_surface_names(allowlist.functions, allowlist.constants, wrong_order)
        failures.append("manifest parity validation did not reject reordered required functions")
    except RuntimeError:
        pass

    if not should_validate_required_sdlh_surface(True, ["core"], "full"):
        failures.append("required SDL.h manifest parity validation was not enabled for executed full core generation")
    if should_validate_required_sdlh_surface(False, ["core"], "full"):
        failures.append("required SDL.h manifest parity validation was enabled during dry-run")
    if should_validate_required_sdlh_surface(True, ["core"], "bootstrap"):
        failures.append("required SDL.h manifest parity validation was enabled outside full scope")
    if should_validate_required_sdlh_surface(True, ["image"], "full"):
        failures.append("required SDL.h manifest parity validation was enabled without core selected")

    try:
        parse_required_sdlh_functions(fixture + "extern DECLSPEC int SDLCALL SDL_Unexpected(void);\n", allowlist.functions)
        failures.append("unexpected direct SDL.h functions were not rejected")
    except RuntimeError:
        pass

    try:
        parse_required_sdlh_constants(fixture + "#define SDL_INIT_SURPRISE 0x80000000u\n", allowlist.constants)
        failures.append("unexpected SDL_INIT macros were not rejected")
    except RuntimeError:
        pass

    # Per-header RSP lookup — Decision 4 (Priority C semantic-ABI design). The
    # helper returns the absolute RSP path when a per-header file exists under
    # spikes/binding-generators/clangsharp/rsp/per-header/<basename>.rsp, and
    # None when it does not. Strips the .h extension from the header name so
    # 'SDL_audio.h' maps to 'SDL_audio.rsp'.
    with tempfile.TemporaryDirectory() as raw_tmp:
        tmp = pathlib.Path(raw_tmp)
        rsp_dir = tmp / "spikes" / "binding-generators" / "clangsharp" / "rsp"
        per_header_dir = rsp_dir / "per-header"
        per_header_dir.mkdir(parents=True)
        (per_header_dir / "SDL_audio.rsp").write_text("# fixture\n", encoding="utf-8")

        resolved = per_header_rsp_path(tmp, "SDL_audio.h")
        if resolved is None:
            failures.append("per_header_rsp_path returned None for an existing SDL_audio.rsp fixture")
        elif resolved != per_header_dir / "SDL_audio.rsp":
            failures.append(f"per_header_rsp_path returned unexpected path: {resolved}")

        missing = per_header_rsp_path(tmp, "SDL_video.h")
        if missing is not None:
            failures.append(f"per_header_rsp_path returned non-None for a missing header: {missing}")

        # Verifies basename derivation strips the .h extension correctly even
        # for header names that contain dots or extra characters.
        (per_header_dir / "SDL_hidapi.rsp").write_text("# fixture\n", encoding="utf-8")
        hidapi = per_header_rsp_path(tmp, "SDL_hidapi.h")
        if hidapi != per_header_dir / "SDL_hidapi.rsp":
            failures.append(f"per_header_rsp_path basename derivation failed: {hidapi}")

        # The input contract rejects path-qualified header names so a caller
        # bug surfaces immediately rather than silently resolving to the wrong
        # basename via pathlib.Path.stem.
        try:
            per_header_rsp_path(tmp, "foo/SDL_audio.h")
            failures.append("per_header_rsp_path did not reject a path-qualified header name with a forward slash")
        except AssertionError:
            pass
        try:
            per_header_rsp_path(tmp, "foo\\SDL_audio.h")
            failures.append("per_header_rsp_path did not reject a path-qualified header name with a backslash")
        except AssertionError:
            pass

        # Wiring-level self-test for extend_rsp_arguments: the shared helper
        # MUST emit base.rsp and family RSP @-arguments unconditionally, and
        # MUST emit a per-header @-argument iff the per-header file exists on
        # disk. Exercises the actual code path used by both command_for_header
        # and platform_command_for_header.
        expected_base = f"@{rsp_dir / 'base.rsp'}"
        expected_family = f"@{rsp_dir / FAMILY_CONFIG['core']['rsp']}"
        expected_per_header = f"@{per_header_dir / 'SDL_audio.rsp'}"

        with_per_header: list[str] = []
        extend_rsp_arguments(with_per_header, tmp, "core", "SDL_audio.h")
        if with_per_header != [expected_base, expected_family, expected_per_header]:
            failures.append(
                "extend_rsp_arguments did not emit base + family + per-header @-arguments in order; "
                f"got: {with_per_header}"
            )

        without_per_header: list[str] = []
        extend_rsp_arguments(without_per_header, tmp, "core", "SDL_video.h")
        if without_per_header != [expected_base, expected_family]:
            failures.append(
                "extend_rsp_arguments emitted unexpected arguments when no per-header RSP exists; "
                f"got: {without_per_header}"
            )

    if failures:
        for failure in failures:
            print(f"self-test: FAIL: {failure}")
        return 1

    print("self-test: PASS")
    return 0


def main() -> int:
    parser = argparse.ArgumentParser(description="ppy-style ClangSharp spike orchestrator")
    parser.add_argument("--vcpkg-triplet", default="x64-windows-hybrid")
    parser.add_argument("--scope", choices=["bootstrap", "full"], default="bootstrap")
    parser.add_argument("--family", choices=["core", "image", "all"], default="all")
    parser.add_argument("--codegen", choices=["compat", "modern", "both"], default="modern",
                        help="compat = compatible-codegen (netstandard2.0/net462); modern = latest-codegen (.NET 8+); both = run two passes")
    parser.add_argument("--execute", action="store_true", help="Actually run ClangSharp; absent means print commands only")
    parser.add_argument("--clean-output", action="store_true", help="Delete generated ClangSharp output before generating")
    parser.add_argument(
        "--use-platform-header-shims",
        action="store_true",
        help="Add spike-only shim headers for Windows-local synthetic Linux/macOS/iOS platform parses",
    )
    parser.add_argument("--self-test", action="store_true", help="Run generator parser self-tests and exit")
    args = parser.parse_args()

    if args.self_test:
        return run_self_tests()

    repo = find_repository_root()
    spike_root = repo / "spikes" / "binding-generators"
    scope_root = repo / "spikes" / "binding-generators" / "scope"
    reports_root = spike_root / "output" / "reports"
    mode = "execute" if args.execute else "dry-run"
    failures: list[tuple[pathlib.Path, str, int]] = []
    empty_outputs: list[EmptyGeneratedOutput] = []
    postprocess_failures = 0
    stats = {
        "core": {"headers": 0, "commands": 0, "generated_files": 0},
        "image": {"headers": 0, "commands": 0, "generated_files": 0},
    }

    selected = selected_families(args.family)
    headers_by_family = {
        family: read_scope(scope_root / scope_file_name(args.scope, family))
        for family in selected
    }

    for family, headers in headers_by_family.items():
        stats[family]["headers"] = len(headers)

    codegen_passes = ["compat", "modern"] if args.codegen == "both" else [args.codegen]

    if should_validate_required_sdlh_surface(args.execute, selected, args.scope):
        allowlist = read_required_surface_allowlist(scope_root / "sdl2-core-sdlh-required.json")
        validate_required_surface_against_manifest(repo, allowlist)

    if args.clean_output and args.execute:
        for family in selected:
            family_generated_root = generated_root_for_family(repo, family)
            if family_generated_root.exists():
                shutil.rmtree(family_generated_root)

    if args.execute:
        print("ppy-style ClangSharp spike scaffold")
        print(f"Repository root: {repo}")
        print(f"Triplet: {args.vcpkg_triplet}")
        print(f"Scope: {args.scope}")
        print(f"Codegen passes: {codegen_passes}")
        print(f"Mode: {mode}")
        print(f"Platform header shims: {'enabled' if args.use_platform_header_shims else 'disabled'}")

    for codegen in codegen_passes:
        if args.execute:
            print(f"--- codegen pass: {codegen} ---")
        for family in selected:
            headers = headers_by_family[family]
            if args.execute:
                print(f"{family}: {len(headers)} scoped headers")
            for header in headers:
                command, output_path = command_for_header(
                    repo, args.vcpkg_triplet, codegen, family, header, args.use_platform_header_shims
                )
                command_line = format_command(command)
                stats[family]["commands"] += 1
                print(command_line)
                if args.execute:
                    output_path.parent.mkdir(parents=True, exist_ok=True)
                    if output_path.exists():
                        output_path.unlink()

                    result = subprocess.run(command, cwd=spike_root)
                    if output_path.is_file():
                        stats[family]["generated_files"] += 1

                    if result.returncode != 0:
                        failures.append((repo / "vcpkg_installed" / args.vcpkg_triplet / "include" / "SDL2" / header, command_line, result.returncode))

    if should_validate_required_sdlh_surface(args.execute, selected, args.scope):
        print("--- required SDL.h surface ---")
        for codegen in codegen_passes:
            generated_count = generate_required_sdlh_surface(repo, args.vcpkg_triplet, codegen, scope_root)
            stats["core"]["generated_files"] += generated_count

    # Multi-OS pass for the SDL2 headers whose public API genuinely splits per
    # platform (SDL_main, SDL_system — Explore-agent scan 2026-05-21). Adapted
    # from ppy generate_bindings.py:341-365 `generate_platform_specific_headers`
    # with Cake PlatformCatalog SDL2 macro views. Output lands under
    # Generated/<Codegen>/Platforms/<View>/.
    if args.execute:
        print("--- multi-OS pass (SDL_main.h, SDL_system.h) ---")
        for codegen in codegen_passes:
            for family in selected:
                platform_headers = PLATFORM_SENSITIVE_HEADERS.get(family, [])
                in_scope = set(headers_by_family[family])
                for header in platform_headers:
                    if header not in in_scope:
                        # Header not in the current scope file — bootstrap
                        # mode for example does not include SDL_system.
                        continue
                    print(f"  multi-OS: {family}/{codegen}/{header}")
                    commands_run, platform_failures, platform_empty_outputs = generate_platform_specific_headers(
                        repo, args.vcpkg_triplet, codegen, family, header, spike_root, args.use_platform_header_shims
                    )
                    stats[family]["commands"] += commands_run
                    # Per-view output files are counted by recursive glob in the
                    # final report rather than incrementing stats here, because
                    # the two-pass flow rewrites the same file twice.
                    if platform_failures:
                        failures.extend(platform_failures)
                        print(f"WARNING: {len(platform_failures)} multi-OS invocations failed for {family}/{codegen}/{header}")
                    if platform_empty_outputs:
                        empty_outputs.extend(platform_empty_outputs)
                        print(f"ERROR: {len(platform_empty_outputs)} empty platform output file(s) produced for {family}/{codegen}/{header}")

    # SDL2 platform views need two cleanups after ClangSharp emits per-view
    # files: remove neutral/earlier-platform duplicates, then annotate the
    # remaining methods based on Platforms/<View>. Doing this with Roslyn avoids
    # ppy's SDL3-only dependency on sdl.json function metadata and keeps the TFM
    # guard in the same postprocess layer as LibraryImport.
    if args.execute:
        print("--- postprocess: platform-delta (all codegens) ---")
        for codegen in codegen_passes:
            for family in selected:
                exit_code = run_postprocess(repo, family, spike_root, "platform-delta", codegen)
                if exit_code != 0:
                    postprocess_failures += 1
                    print(f"WARNING: platform-delta postprocess for {family}/{codegen} returned exit {exit_code}")

    # Constitution L162-176 fmt-only variadic policy: drop `__arglist` parameters
    # from both Compat and Modern output before any subsequent transform. This
    # also unblocks the LibraryImport step (Microsoft.Interop.LibraryImportGenerator
    # rejects varargs).
    if args.execute:
        print("--- postprocess: strip-varargs (all codegens) ---")
        for codegen in codegen_passes:
            for family in selected:
                exit_code = run_postprocess(repo, family, spike_root, "strip-varargs", codegen)
                if exit_code != 0:
                    postprocess_failures += 1
                    print(f"WARNING: strip-varargs postprocess for {family}/{codegen} returned exit {exit_code}")

    # Constitution L48 backend split: Compat stays [DllImport] for legacy TFMs;
    # Modern is promoted to [LibraryImport] + [UnmanagedCallConv] + partial for
    # the .NET 7+ source-generated marshalling perf benefit.
    if args.execute and "modern" in codegen_passes:
        print("--- postprocess: libraryimport (modern only) ---")
        for family in selected:
            exit_code = run_postprocess(repo, family, spike_root, "libraryimport", "modern")
            if exit_code != 0:
                postprocess_failures += 1
                print(f"WARNING: libraryimport postprocess for {family} returned exit {exit_code}")

    # Slice C-C SDL_GUID substitution: removes the generated `partial struct
    # SDL_GUID` (Uint8 data[16]) and rewrites every reference to System.Guid
    # (also 16 bytes; wire-identical). Workstream README Current Decision
    # Posture pins SDL_GUID -> System.Guid; Cake's SdlNativeTypeSubstitutionPolicy
    # applies the same substitution. Applied to both Compat and Modern after
    # the LibraryImport pass so the rewrite operates on the final attribute
    # shape and the `using System;` insertion lands once per touched file.
    if args.execute:
        print("--- postprocess: guid-substitute (all codegens) ---")
        for codegen in codegen_passes:
            for family in selected:
                exit_code = run_postprocess(repo, family, spike_root, "guid-substitute", codegen)
                if exit_code != 0:
                    postprocess_failures += 1
                    print(f"WARNING: guid-substitute postprocess for {family}/{codegen} returned exit {exit_code}")

    # Slice C-A R2 structural SDL_threadID hybrid dispatch: replaces the
    # single uint-returning SDL_ThreadID / SDL_GetThreadID P/Invoke with a
    # TFM-conditional pair — CLong/CULong + LibraryImport on net6+, and
    # Microsoft's documented dual-DllImport + RuntimeInformation.IsOSPlatform
    # dispatch on legacy TFMs (uint return on Windows = 32-bit C unsigned long;
    # nint return on Unix LP64 = 64-bit). Caller-side surface uniform ulong.
    # Applied to both Compat and Modern after guid-substitute so the rewrite
    # sees the final attribute shape across both codegen trees.
    if args.execute:
        print("--- postprocess: threadid-dispatch (all codegens) ---")
        for codegen in codegen_passes:
            for family in selected:
                exit_code = run_postprocess(repo, family, spike_root, "threadid-dispatch", codegen)
                if exit_code != 0:
                    postprocess_failures += 1
                    print(f"WARNING: threadid-dispatch postprocess for {family}/{codegen} returned exit {exit_code}")

    # Slice C-B Pattern B uniform opaque handle emit. Applied last so it operates
    # on the final signature shape after threadid-dispatch has rewritten the
    # SDL_threadID family. Per Constitution §"Opaque Handles" Implementation
    # mechanism: owner mode (Janset.SDL2.Core/Generated/*) writes a single
    # consolidated Handles.g.cs with the full Pattern B struct body for every
    # roster handle; consumer mode (Janset.SDL2.Image/Generated/*) only removes
    # any partial struct declarations + applies pointer-to-by-value rewrites,
    # since Core's Handles.g.cs is referenced via ProjectReference + the shared
    # SDL2 namespace. Owner/consumer detection is path-based inside Program.cs.
    if args.execute:
        print("--- postprocess: uniform-opaque (all codegens) ---")
        for codegen in codegen_passes:
            for family in selected:
                exit_code = run_postprocess(repo, family, spike_root, "uniform-opaque", codegen)
                if exit_code != 0:
                    postprocess_failures += 1
                    print(f"WARNING: uniform-opaque postprocess for {family}/{codegen} returned exit {exit_code}")

    write_report(
        reports_root,
        args.scope,
        args.vcpkg_triplet,
        mode,
        selected,
        stats,
        failures,
        empty_outputs,
        args.use_platform_header_shims,
    )

    if postprocess_failures:
        print(f"ERROR: {postprocess_failures} postprocess command(s) failed")
        return 3

    if empty_outputs:
        print(f"ERROR: {len(empty_outputs)} generated output file(s) were empty")
        return 4

    return 0


if __name__ == "__main__":
    sys.exit(main())
