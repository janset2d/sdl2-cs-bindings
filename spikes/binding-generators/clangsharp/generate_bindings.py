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
class NoOpGeneratedOutput:
    header_path: pathlib.Path
    output_path: pathlib.Path
    command_line: str
    reason: str


@dataclass(frozen=True)
class AcceptedClangSharpWarnings:
    header_path: pathlib.Path
    output_path: pathlib.Path
    command_line: str
    exit_code: int
    macros: tuple[str, ...]
    diagnostics: tuple[str, ...]
    output_empty: bool


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

    Implements the third RSP tier used by the Priority C semantic-ABI closure.
    Mirrors the per-header RSP pattern used by ppy/SDL3-CS generate_bindings.py:318-320.

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
    changes (additional tiers, ordering tweaks) are made once. The Priority C
    semantic-ABI closure uses this three-tier organization for per-header
    excludes, remaps, and foreign-boundary overrides.
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


def read_header_list(header_list_file: pathlib.Path) -> list[str]:
    if not header_list_file.is_file():
        raise FileNotFoundError(
            f"Header list file not found: {header_list_file}. "
            "For new families, create the header list file in spikes/binding-generators/scope/."
        )

    headers: list[str] = []
    for line in header_list_file.read_text(encoding="utf-8").splitlines():
        stripped = line.strip()
        if not stripped or stripped.startswith("#"):
            continue
        headers.append(stripped)
    return headers


def normalize_line_endings(content: str) -> str:
    return content.replace("\r\n", "\n").replace("\r", "\n")


def write_text_lf(path: pathlib.Path, content: str) -> None:
    with path.open("w", encoding="utf-8", newline="\n") as output:
        output.write(normalize_line_endings(content))


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


def should_validate_required_sdlh_surface(execute: bool, selected: list[str]) -> bool:
    return execute and "core" in selected


FUNCTION_DECLARATION_PATTERN = re.compile(
    r"^extern\s+DECLSPEC\s+(?P<return_type>.+?)\s+SDLCALL\s+(?P<name>SDL_\w+)\((?P<parameters>.*?)\);$"
)
MACRO_PATTERN = re.compile(r"^#define\s+(?P<name>SDL_INIT_\w+)\s+(?P<value>.+)$")
FUNCTION_LIKE_MACRO_WARNING_PATTERN = re.compile(
    r"^Warning \([^)]*\bLine\b[^)]*,\s*\bColumn\b[^)]*\): "
    r"Function like macro definition records are not supported: '(?P<name>[^']+)'. "
    r"Generated bindings may be incomplete\.$"
)
DIAGNOSTICS_FOR_INPUT_PATTERN = re.compile(r"^Diagnostics for '[^']+':$")
PROCESSING_INPUT_PATTERN = re.compile(r"^Processing '[^']+'$")
DIAGNOSTICS_FOR_BINDING_GENERATION_PATTERN = re.compile(r"^Diagnostics for binding generation of .+:$")
BUILTIN_MACRO_REDEFINED_WARNINGS = {
    "warning: redefining builtin macro [-Wbuiltin-macro-redefined]",
    "warning: undefining builtin macro [-Wbuiltin-macro-redefined]",
}


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
        "headers": "sdl2-core.headers.txt",
        "library_dir": "Janset.SDL2.Core",
    },
    "image": {
        "namespace": "SDL2.Image",
        "raw_class": "SDL_imageNative",
        "rsp": "sdl2-image.rsp",
        "headers": "sdl2-image.headers.txt",
        "library_dir": "Janset.SDL2.Image",
    },
    "ttf": {
        "namespace": "SDL2.Ttf",
        "raw_class": "SDL_ttfNative",
        "rsp": "sdl2-ttf.rsp",
        "headers": "sdl2-ttf.headers.txt",
        "library_dir": "Janset.SDL2.Ttf",
    },
    "mixer": {
        "namespace": "SDL2.Mixer",
        "raw_class": "SDL_mixerNative",
        "rsp": "sdl2-mixer.rsp",
        "headers": "sdl2-mixer.headers.txt",
        "library_dir": "Janset.SDL2.Mixer",
    },
    "gfx": {
        "namespace": "SDL2.Gfx",
        "raw_class": "SDL2_gfxNative",
        "rsp": "sdl2-gfx.rsp",
        "headers": "sdl2-gfx.headers.txt",
        "library_dir": "Janset.SDL2.Gfx",
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

PRODUCTION_CODEGEN_PASSES = ("compat", "modern")


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
    "ttf": [],
    "mixer": [],
    "gfx": [],
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


def is_non_empty_generated_output(output_path: pathlib.Path) -> bool:
    return output_path.is_file() and output_path.stat().st_size > 0


def should_record_no_op_generated_output(
    output_path: pathlib.Path,
    accepted_warning: AcceptedClangSharpWarnings | None,
    exit_code: int,
) -> bool:
    return accepted_warning is None and exit_code == 0 and is_empty_generated_output(output_path)


def should_record_empty_generated_output(
    output_path: pathlib.Path,
    accepted_warning: AcceptedClangSharpWarnings | None,
    exit_code: int,
) -> bool:
    return (
        accepted_warning is None
        and not should_record_no_op_generated_output(output_path, accepted_warning, exit_code)
        and not is_non_empty_generated_output(output_path)
    )


def has_known_declspec_parse_diagnostic(output: str) -> bool:
    return (
        "Parsing failed for 'declspec' due to 'CXError_Failure'" in output
        and "Skipping 'declspec' due to one or more errors listed above." in output
    )


def had_fatal_parse_failure(output: str, input_file: pathlib.Path) -> bool:
    return "fatal error:" in output or f"Skipping '{input_file}' due to one or more errors listed above." in output


def classify_warning_only_clangsharp_exit(
    header_path: pathlib.Path,
    output_path: pathlib.Path,
    command_line: str,
    exit_code: int,
    diagnostic_output: str,
) -> AcceptedClangSharpWarnings | None:
    if exit_code == 0:
        return None
    if had_fatal_parse_failure(diagnostic_output, header_path) or has_known_declspec_parse_diagnostic(diagnostic_output):
        return None
    if not output_path.is_file():
        return None

    macros: list[str] = []
    diagnostics: list[str] = []
    for line in diagnostic_output.splitlines():
        stripped = line.strip()
        if not stripped:
            continue
        if (
            DIAGNOSTICS_FOR_INPUT_PATTERN.match(stripped) is not None
            or PROCESSING_INPUT_PATTERN.match(stripped) is not None
            or DIAGNOSTICS_FOR_BINDING_GENERATION_PATTERN.match(stripped) is not None
        ):
            continue
        if stripped in BUILTIN_MACRO_REDEFINED_WARNINGS:
            diagnostics.append(stripped)
            continue

        match = FUNCTION_LIKE_MACRO_WARNING_PATTERN.match(stripped)
        if match is None:
            return None
        macros.append(match.group("name"))

    if not macros:
        return None

    return AcceptedClangSharpWarnings(
        header_path,
        output_path,
        command_line,
        exit_code,
        tuple(macros),
        tuple(diagnostics),
        is_empty_generated_output(output_path),
    )


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
    write_text_lf(
        output_path,
        render_required_surface(
            FAMILY_CONFIG["core"]["namespace"],
            FAMILY_CONFIG["core"]["raw_class"],
            functions,
            constants,
        ),
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
      * cross-contamination --additional=--undefine-macro=<macro> for every
        macro in ALL_PLATFORM_MACROS that this view does NOT define
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
        for macro in undefines:
            command.append(f"--additional=--undefine-macro={macro}")

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
) -> tuple[
    int,
    list[tuple[pathlib.Path, str, int]],
    list[EmptyGeneratedOutput],
    list[NoOpGeneratedOutput],
    list[AcceptedClangSharpWarnings],
]:
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
    no_op_outputs: list[NoOpGeneratedOutput] = []
    accepted_warnings: list[AcceptedClangSharpWarnings] = []

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
        accepted: AcceptedClangSharpWarnings | None = None
        if result.returncode != 0:
            accepted = classify_warning_only_clangsharp_exit(
                input_file,
                output_path,
                command_line,
                result.returncode,
                diagnostic_output,
            )
            if accepted is not None:
                accepted_warnings.append(accepted)
            else:
                failures.append((input_file, command_line, result.returncode))
                if result.stdout:
                    print(f"  STDOUT ({view_name}):\n{result.stdout.rstrip()}")
                if result.stderr:
                    print(f"  STDERR ({view_name}):\n{result.stderr.rstrip()}")
        if should_record_no_op_generated_output(output_path, accepted, result.returncode):
            no_op_outputs.append(NoOpGeneratedOutput(
                input_file,
                output_path,
                command_line,
                "Platform view emitted no declarations after neutral symbol excludes.",
            ))
        elif should_record_empty_generated_output(output_path, accepted, result.returncode):
            empty_outputs.append(EmptyGeneratedOutput(input_file, output_path, command_line))

    return commands_run, failures, empty_outputs, no_op_outputs, accepted_warnings


def output_path_for_header(repo: pathlib.Path, codegen: str, family: str, header: str) -> pathlib.Path:
    subdir = "Compat" if codegen == "compat" else "Modern"
    library_dir = FAMILY_CONFIG[family]["library_dir"]
    return repo / "spikes" / "binding-generators" / "clangsharp" / "src" / library_dir / "Generated" / subdir / (pathlib.Path(header).stem + ".g.cs")


def generated_root_for_family(repo: pathlib.Path, family: str) -> pathlib.Path:
    library_dir = FAMILY_CONFIG[family]["library_dir"]
    return repo / "spikes" / "binding-generators" / "clangsharp" / "src" / library_dir / "Generated"


def modern_root_for_family(repo: pathlib.Path, family: str) -> pathlib.Path:
    return generated_root_for_family(repo, family) / "Modern"


def run_postprocess(
    repo: pathlib.Path,
    family: str,
    spike_root: pathlib.Path,
    mode: str,
    codegen: str,
    extra_args: list[str] | None = None,
) -> int:
    """Invoke the Microsoft.CodeAnalysis postprocess. mode is one of:
    - 'strip-varargs'     drops __arglist per Constitution L162-176 fmt-only policy
    - 'libraryimport'     promotes DllImport -> LibraryImport per Constitution L48 backend split
    - 'platform-delta'    SDL2 platform-view pass cleanup
    - 'guid-substitute'   Slice C-C: SDL_GUID -> System.Guid (16-byte wire-identical)
    - 'threadid-dispatch' Slice C-A R2 structural: SDL_threadID family hybrid TFM emit
    - 'uniform-opaque'    Slice C-B Pattern B opaque handle emit + pointer-to-by-value rewrites
    codegen selects the Generated/<Codegen>/ subtree to operate on.

    extra_args, if provided, are appended to the postprocess CLI after the
    target directory. The uniform-opaque mode uses this to pass an explicit
    --owner-mode owner|consumer flag instead of relying on the substring-based
    fallback inside Program.cs."""
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
    if extra_args:
        cmd.extend(extra_args)
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
        for macro in ALL_PLATFORM_MACROS:
            command.append(f"--additional=--undefine-macro={macro}")
    return command, output_path


def format_command(command: list[str]) -> str:
    return " ".join(f'\"{part}\"' if " " in part else part for part in command)


def selected_families(family: str) -> list[str]:
    # "all" intentionally covers only families that already have complete scope,
    # response-file, project, and postprocess support. New family metadata can be
    # CLI-addressable before it is safe to include in aggregate generation.
    if family == "all":
        return ["core", "image"]
    return [family]


def create_generation_stats(selected: list[str]) -> dict[str, dict[str, int]]:
    return {family: {"headers": 0, "commands": 0, "generated_files": 0} for family in selected}


def refresh_generated_file_counts(
    repo: pathlib.Path,
    selected: list[str],
    stats: dict[str, dict[str, int]],
) -> None:
    for family in selected:
        family_root = generated_root_for_family(repo, family)
        stats[family]["generated_files"] = sum(1 for path in family_root.rglob("*.g.cs") if path.is_file())


def owner_mode_for_family(family: str) -> str:
    return "owner" if family in ("core", "ttf", "mixer") else "consumer"


def uniform_opaque_extra_args_for_family(family: str) -> list[str]:
    return [
        "--owner-mode", owner_mode_for_family(family),
        "--handles-namespace", FAMILY_CONFIG[family]["namespace"],
    ]


def production_header_list_file_name(family: str) -> str:
    return FAMILY_CONFIG[family]["headers"]


def postprocess_steps_for_codegen(codegen: str) -> tuple[str, ...]:
    common_steps = (
        "platform-delta",
        "strip-varargs",
        "guid-substitute",
        "threadid-dispatch",
        "uniform-opaque",
    )
    if codegen == "modern":
        return (
            "platform-delta",
            "strip-varargs",
            "libraryimport",
            "guid-substitute",
            "threadid-dispatch",
            "uniform-opaque",
        )
    return common_steps


def generation_exit_code(
    failures: list[tuple[pathlib.Path, str, int]],
    empty_outputs: list[EmptyGeneratedOutput],
    postprocess_failures: int,
    accepted_warnings: list[AcceptedClangSharpWarnings] | None = None,
) -> int:
    if failures:
        return 2
    if postprocess_failures:
        return 3
    if empty_outputs:
        return 4
    return 0


def write_report(
    reports_root: pathlib.Path,
    header_set_label: str,
    triplet: str,
    mode: str,
    selected: list[str],
    stats: dict[str, dict[str, int]],
    failures: list[tuple[pathlib.Path, str, int]],
    empty_outputs: list[EmptyGeneratedOutput],
    no_op_outputs: list[NoOpGeneratedOutput],
    accepted_warnings: list[AcceptedClangSharpWarnings],
    use_platform_header_shims: bool,
) -> None:
    reports_root.mkdir(parents=True, exist_ok=True)
    report_path = reports_root / f"clangsharp-{header_set_label}.md"
    lines = [
        f"# ClangSharp {header_set_label} Report",
        "",
        f"**Triplet:** {triplet}",
        f"**Mode:** {mode}",
        f"**Platform header shims:** {'enabled' if use_platform_header_shims else 'disabled'}",
        "",
        "| Family | Headers | Commands | Generated Files |",
        "| --- | ---: | ---: | ---: |",
    ]

    for family in selected:
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

    lines.extend(["", "## No-op Generated Outputs", ""])
    if no_op_outputs:
        for no_op_output in no_op_outputs:
            lines.extend([
                f"- Header: `{no_op_output.header_path}`",
                f"- Output: `{no_op_output.output_path}`",
                f"- Reason: {no_op_output.reason}",
                f"- Command: `{no_op_output.command_line}`",
            ])
            lines.append("")
    else:
        lines.append("No no-op generated outputs recorded.")

    lines.extend(["", "## Accepted Warning-Only ClangSharp Exits", ""])
    if accepted_warnings:
        for accepted in accepted_warnings:
            lines.extend([
                f"- Header: `{accepted.header_path}`",
                f"- Output: `{accepted.output_path}`",
                f"- Output status: {'empty (accepted warning-only)' if accepted.output_empty else 'non-empty'}",
                f"- Exit code: `{accepted.exit_code}`",
                f"- Macros: {', '.join(accepted.macros)}",
            ])
            if accepted.diagnostics:
                lines.append(f"- Accepted diagnostics: {'; '.join(accepted.diagnostics)}")
            lines.append(f"- Command: `{accepted.command_line}`")
            lines.append("")
    else:
        lines.append("No accepted warning-only ClangSharp exits recorded.")

    while lines and lines[-1] == "":
        lines.pop()

    write_text_lf(report_path, "\n".join(lines) + "\n")


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

    if not should_validate_required_sdlh_surface(True, ["core"]):
        failures.append("required SDL.h manifest parity validation was not enabled for executed core generation")
    if should_validate_required_sdlh_surface(False, ["core"]):
        failures.append("required SDL.h manifest parity validation was enabled during dry-run")
    if should_validate_required_sdlh_surface(True, ["image"]):
        failures.append("required SDL.h manifest parity validation was enabled without core selected")

    normalized = normalize_line_endings("alpha\r\nbeta\rgamma\n")
    if normalized != "alpha\nbeta\ngamma\n":
        failures.append(f"normalize_line_endings returned unexpected output: {normalized!r}")

    with tempfile.TemporaryDirectory() as raw_tmp:
        lf_file = pathlib.Path(raw_tmp) / "lf.txt"
        write_text_lf(lf_file, "alpha\r\nbeta\rgamma\n")
        if b"\r" in lf_file.read_bytes():
            failures.append("write_text_lf emitted CR bytes")

    with tempfile.TemporaryDirectory() as raw_tmp:
        reports_root = pathlib.Path(raw_tmp)
        report_stats = {"core": {"headers": 1, "commands": 2, "generated_files": 0}}
        write_report(
            reports_root,
            "production",
            "x64-windows-hybrid",
            "dry-run",
            ["core"],
            report_stats,
            [],
            [],
            [],
            [],
            True,
        )
        report_bytes = (reports_root / "clangsharp-production.md").read_bytes()
        if b"\r\n" in report_bytes:
            failures.append("write_report emitted CRLF line endings")

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

    with tempfile.TemporaryDirectory() as raw_tmp:
        tmp = pathlib.Path(raw_tmp)
        rsp_dir = tmp / "spikes" / "binding-generators" / "clangsharp" / "rsp"
        rsp_dir.mkdir(parents=True)
        include_root = tmp / "vcpkg_installed" / "x64-windows-hybrid" / "include" / "SDL2"
        include_root.mkdir(parents=True)
        input_file = include_root / "SDL_system.h"
        input_file.write_text("/* fixture */\n", encoding="utf-8")

        command, _ = platform_command_for_header(
            tmp,
            "x64-windows-hybrid",
            "compat",
            "core",
            "SDL_system.h",
            "Linux",
            ["linux=1", "__linux=1", "__linux__=1", "__LINUX__=1"],
            [],
            True,
        )
        if "--additional" in command:
            failures.append("platform command emitted standalone --additional for platform undefines")

        defined_names = {"linux", "__linux", "__linux__", "__LINUX__"}
        expected_undefines = [macro for macro in ALL_PLATFORM_MACROS if macro not in defined_names]
        expected_additional_undefines = [
            f"--additional=--undefine-macro={macro}"
            for macro in expected_undefines
        ]
        actual_additional_undefines = [
            token for token in command
            if token.startswith("--additional=--undefine-macro=")
        ]
        if actual_additional_undefines != expected_additional_undefines:
            failures.append(
                "platform command did not emit every platform undefine as attached --additional=--undefine-macro tokens; "
                f"got: {actual_additional_undefines}"
            )
        if "--exclude" in command:
            failures.append(f"platform command excluded function-like macros dynamically: {command!r}")

        neutral_command, _ = command_for_header(
            tmp,
            "x64-windows-hybrid",
            "compat",
            "core",
            "SDL_system.h",
            True,
        )
        if "--additional" in neutral_command:
            failures.append("neutral platform-sensitive command emitted standalone --additional for platform undefines")
        expected_neutral_undefines = [
            f"--additional=--undefine-macro={macro}"
            for macro in ALL_PLATFORM_MACROS
        ]
        actual_neutral_undefines = [
            token for token in neutral_command
            if token.startswith("--additional=--undefine-macro=")
        ]
        if actual_neutral_undefines != expected_neutral_undefines:
            failures.append(
                "neutral platform-sensitive command did not emit every undefine as attached --additional=--undefine-macro tokens; "
                f"got: {actual_neutral_undefines}"
            )
        if "--exclude" in neutral_command:
            failures.append(f"neutral command excluded function-like macros dynamically: {neutral_command!r}")

        platform_neutral_command, _ = platform_command_for_header(
            tmp,
            "x64-windows-hybrid",
            "compat",
            "core",
            "SDL_system.h",
            "Linux",
            ["linux=1", "__linux=1", "__linux__=1", "__LINUX__=1"],
            ["SDL_SystemTheme", "SDL_FunctionLike"],
            True,
        )
        if "--exclude" not in platform_neutral_command:
            failures.append(f"platform command did not preserve neutral symbol excludes: {platform_neutral_command!r}")
        else:
            exclude_index = platform_neutral_command.index("--exclude")
            platform_neutral_excludes = platform_neutral_command[exclude_index + 1:]
            if platform_neutral_excludes != ["SDL_SystemTheme", "SDL_FunctionLike"]:
                failures.append(
                    "platform command did not preserve neutral symbol excludes exactly; "
                    f"got: {platform_neutral_excludes!r}"
                )

        if "classify_warning_only_clangsharp_exit" not in globals():
            failures.append("classify_warning_only_clangsharp_exit helper is missing")
        else:
            output_path = tmp / "SDL_mixer.g.cs"
            output_path.write_text("public static partial class SDL_mixerNative {}\n", encoding="utf-8")
            accepted_warning = (
                "Warning (Line 86, Column 9): Function like macro definition records are not supported: "
                "'SDL_MIXER_VERSION_ATLEAST'. Generated bindings may be incomplete."
            )
            accepted_with_path = (
                "Warning (C:/vcpkg/include/SDL2/SDL_mixer.h: Line 55, Column 9): Function like macro definition "
                "records are not supported: 'SDL_MIXER_VERSION'. Generated bindings may be incomplete."
            )
            accepted = classify_warning_only_clangsharp_exit(
                input_file,
                output_path,
                "clangsharp SDL_mixer.h",
                1,
                accepted_warning + "\n" + accepted_with_path,
            )
            if accepted is None:
                failures.append("warning-only classifier rejected known function-like macro warnings with non-empty output")
            elif accepted.macros != ("SDL_MIXER_VERSION_ATLEAST", "SDL_MIXER_VERSION"):
                failures.append(
                    "warning-only classifier did not preserve accepted macro names in order; "
                    f"got: {accepted.macros!r}"
                )

            accepted_with_framing = classify_warning_only_clangsharp_exit(
                input_file,
                output_path,
                "clangsharp SDL_mixer.h",
                1,
                "\n".join(
                    [
                        "Diagnostics for 'C:/vcpkg/include/SDL2/SDL_mixer.h':",
                        "Processing 'C:/vcpkg/include/SDL2/SDL_mixer.h'",
                        "Diagnostics for binding generation of SDL_mixer.h:",
                        "warning: redefining builtin macro [-Wbuiltin-macro-redefined]",
                        "warning: undefining builtin macro [-Wbuiltin-macro-redefined]",
                        accepted_warning,
                    ]
                ),
            )
            if accepted_with_framing is None:
                failures.append("warning-only classifier rejected function-like macro warnings with allowed framing")
            elif accepted_with_framing.macros != ("SDL_MIXER_VERSION_ATLEAST",):
                failures.append(
                    "warning-only classifier did not preserve macro names when allowed framing is present; "
                    f"got: {accepted_with_framing.macros!r}"
                )
            elif getattr(accepted_with_framing, "diagnostics", ()) != (
                "warning: redefining builtin macro [-Wbuiltin-macro-redefined]",
                "warning: undefining builtin macro [-Wbuiltin-macro-redefined]",
            ):
                failures.append(
                    "warning-only classifier did not preserve accepted builtin macro diagnostics; "
                    f"got: {getattr(accepted_with_framing, 'diagnostics', None)!r}"
                )

            unsupported_attribute = classify_warning_only_clangsharp_exit(
                input_file,
                output_path,
                "clangsharp SDL_assert.h",
                1,
                "\n".join(
                    [
                        "Diagnostics for 'C:/vcpkg/include/SDL2/SDL_assert.h':",
                        "Processing 'C:/vcpkg/include/SDL2/SDL_assert.h'",
                        "Diagnostics for binding generation of SDL_assert.h:",
                        "Warning (Line 120, Column 18): Unsupported attribute: 'AnalyzerNoReturn'.",
                        accepted_warning,
                    ]
                ),
            )
            if unsupported_attribute is not None:
                failures.append("warning-only classifier accepted unsupported AnalyzerNoReturn attribute warning")

            other_lowercase_warning = classify_warning_only_clangsharp_exit(
                input_file,
                output_path,
                "clangsharp SDL_mixer.h",
                1,
                "\n".join(
                    [
                        "Diagnostics for binding generation of SDL_mixer.h:",
                        "warning: something else happened [-Wexample]",
                        accepted_warning,
                    ]
                ),
            )
            if other_lowercase_warning is not None:
                failures.append("warning-only classifier accepted an unknown lower-case warning")

            mixed = classify_warning_only_clangsharp_exit(
                input_file,
                output_path,
                "clangsharp SDL_mixer.h",
                1,
                accepted_warning + "\nWarning (Line 1, Column 1): Something else happened.",
            )
            if mixed is not None:
                failures.append("warning-only classifier accepted mixed warning text")

            no_warnings = classify_warning_only_clangsharp_exit(
                input_file,
                output_path,
                "clangsharp SDL_mixer.h",
                1,
                "",
            )
            if no_warnings is not None:
                failures.append("warning-only classifier accepted an empty diagnostic stream")

            output_path.write_text("", encoding="utf-8")
            empty = classify_warning_only_clangsharp_exit(
                input_file,
                output_path,
                "clangsharp SDL_mixer.h",
                1,
                accepted_warning,
            )
            if empty is None:
                failures.append("warning-only classifier rejected empty output with only function-like macro warnings")
            elif empty.macros != ("SDL_MIXER_VERSION_ATLEAST",):
                failures.append(
                    "warning-only classifier did not preserve macro names for empty warning-only output; "
                    f"got: {empty.macros!r}"
                )

            if "should_record_empty_generated_output" not in globals():
                failures.append("should_record_empty_generated_output helper is missing for zero-byte output gating")
            elif empty is not None:
                try:
                    zero_exit_empty = should_record_empty_generated_output(output_path, None, 0)
                    accepted_empty = should_record_empty_generated_output(output_path, empty, 1)
                except TypeError:
                    failures.append("should_record_empty_generated_output must include the ClangSharp exit code")
                else:
                    if zero_exit_empty:
                        failures.append("exit-zero zero-byte output was incorrectly gated as unexpected empty output")
                    if accepted_empty:
                        failures.append("accepted warning-only zero-byte output was incorrectly gated as empty")
                if "should_record_no_op_generated_output" not in globals():
                    failures.append("should_record_no_op_generated_output helper is missing for exit-zero zero-byte output reporting")
                elif not should_record_no_op_generated_output(output_path, None, 0):
                    failures.append("exit-zero zero-byte output was not reported as a no-op generated output")

                if "NoOpGeneratedOutput" not in globals():
                    failures.append("NoOpGeneratedOutput dataclass is missing for report-visible no-op outputs")
                elif "should_record_no_op_generated_output" in globals() and should_record_no_op_generated_output(output_path, empty, 1):
                    failures.append("accepted warning-only zero-byte output was incorrectly gated as empty")

            missing_output_path = tmp / "SDL_missing.g.cs"
            if missing_output_path.exists():
                missing_output_path.unlink()
            missing_warning_only = classify_warning_only_clangsharp_exit(
                input_file,
                missing_output_path,
                "clangsharp SDL_missing.h",
                1,
                accepted_warning,
            )
            if missing_warning_only is not None:
                failures.append("warning-only classifier accepted diagnostics with a missing output file")
            try:
                missing_is_empty = should_record_empty_generated_output(missing_output_path, None, 0)
            except TypeError:
                pass
            else:
                if not missing_is_empty:
                    failures.append("missing output without accepted warning-only diagnostics was not gated as empty")

    base_rsp_text = (
        find_repository_root()
        / "spikes" / "binding-generators" / "clangsharp" / "rsp" / "base.rsp"
    ).read_text(encoding="utf-8")
    if "--additional\n--undefine-macro=__has_builtin\n-fdeclspec" in base_rsp_text:
        failures.append("base.rsp retained ambiguous standalone --additional sequence for __has_builtin/-fdeclspec")
    if "--additional=-fdeclspec" not in base_rsp_text:
        failures.append("base.rsp did not emit -fdeclspec using attached --additional=-fdeclspec form")
    if "__has_feature(x)=0" not in base_rsp_text:
        failures.append("base.rsp did not define __has_feature(x)=0")

    clangsharp_failure = (pathlib.Path("SDL_video.h"), "clangsharp SDL_video.h", 1)
    if generation_exit_code([clangsharp_failure], [], 0) != 2:
        failures.append("ClangSharp invocation failures did not produce exit code 2")
    if generation_exit_code([], [], 1) != 3:
        failures.append("postprocess failures did not produce exit code 3")
    if generation_exit_code([], [EmptyGeneratedOutput(pathlib.Path("SDL_video.h"), pathlib.Path("SDL_video.g.cs"), "clangsharp")], 0) != 4:
        failures.append("empty generated outputs did not produce exit code 4")
    if generation_exit_code([], [], 0) != 0:
        failures.append("clean generation did not produce exit code 0")
    if "AcceptedClangSharpWarnings" not in globals():
        failures.append("AcceptedClangSharpWarnings dataclass is missing")
    else:
        accepted_warning_exit = AcceptedClangSharpWarnings(
            pathlib.Path("SDL_mixer.h"),
            pathlib.Path("SDL_mixer.g.cs"),
            "clangsharp SDL_mixer.h",
            1,
            ("SDL_MIXER_VERSION",),
            (),
            False,
        )
        if generation_exit_code([], [], 0, [accepted_warning_exit]) != 0:
            failures.append("accepted warning-only exits were counted as generation failures")

    if "PRODUCTION_CODEGEN_PASSES" not in globals():
        failures.append("PRODUCTION_CODEGEN_PASSES constant is missing")
    elif PRODUCTION_CODEGEN_PASSES != ("compat", "modern"):
        failures.append(f"production codegen passes must be exactly ('compat', 'modern'); got {PRODUCTION_CODEGEN_PASSES!r}")

    expected_family_config = {
        "core": {
            "namespace": "SDL2",
            "raw_class": "SDLNative",
            "rsp": "sdl2-core.rsp",
            "headers": "sdl2-core.headers.txt",
            "library_dir": "Janset.SDL2.Core",
        },
        "image": {
            "namespace": "SDL2.Image",
            "raw_class": "SDL_imageNative",
            "rsp": "sdl2-image.rsp",
            "headers": "sdl2-image.headers.txt",
            "library_dir": "Janset.SDL2.Image",
        },
        "ttf": {
            "namespace": "SDL2.Ttf",
            "raw_class": "SDL_ttfNative",
            "rsp": "sdl2-ttf.rsp",
            "headers": "sdl2-ttf.headers.txt",
            "library_dir": "Janset.SDL2.Ttf",
        },
        "mixer": {
            "namespace": "SDL2.Mixer",
            "raw_class": "SDL_mixerNative",
            "rsp": "sdl2-mixer.rsp",
            "headers": "sdl2-mixer.headers.txt",
            "library_dir": "Janset.SDL2.Mixer",
        },
        "gfx": {
            "namespace": "SDL2.Gfx",
            "raw_class": "SDL2_gfxNative",
            "rsp": "sdl2-gfx.rsp",
            "headers": "sdl2-gfx.headers.txt",
            "library_dir": "Janset.SDL2.Gfx",
        },
    }
    for family, expected in expected_family_config.items():
        if FAMILY_CONFIG.get(family) != expected:
            failures.append(f"FAMILY_CONFIG[{family!r}] did not match expected S1-2 identity")
        if family != "core" and PLATFORM_SENSITIVE_HEADERS.get(family) != []:
            failures.append(f"PLATFORM_SENSITIVE_HEADERS[{family!r}] expected empty list")
        try:
            if production_header_list_file_name(family) != expected["headers"]:
                failures.append(f"production_header_list_file_name({family!r}) did not return the production header list")
        except KeyError as exc:
            failures.append(f"production_header_list_file_name({family!r}) raised KeyError: {exc}")

    for family, config in FAMILY_CONFIG.items():
        if "headers" not in config:
            failures.append(f"FAMILY_CONFIG[{family!r}] does not expose the production headers field")
        stale_keys = sorted(set(config) & {"bootstrap_scope", "full_scope"})
        if stale_keys:
            failures.append(f"FAMILY_CONFIG[{family!r}] retained stale scope fields: {stale_keys}")

    stale_helpers = [name for name in ("scope_file_name", "production_header_scope_file_name") if name in globals()]
    if stale_helpers:
        failures.append(f"stale scope helper(s) still exist: {stale_helpers}")

    if "postprocess_steps_for_codegen" not in globals():
        failures.append("postprocess_steps_for_codegen helper is missing")
    else:
        compat_steps = postprocess_steps_for_codegen("compat")
        modern_steps = postprocess_steps_for_codegen("modern")
        if "libraryimport" in compat_steps:
            failures.append(f"compat postprocess steps included modern-only libraryimport: {compat_steps!r}")
        if "libraryimport" not in modern_steps:
            failures.append(f"modern postprocess steps did not include libraryimport: {modern_steps!r}")

    if selected_families("all") != ["core", "image"]:
        failures.append(f"selected_families('all') must stay dormant as ['core', 'image']; got {selected_families('all')!r}")

    if "create_generation_stats" not in globals():
        failures.append("create_generation_stats helper is missing for selected-driven stats initialization")
    else:
        expected_ttf_stats = {"ttf": {"headers": 0, "commands": 0, "generated_files": 0}}
        actual_ttf_stats = create_generation_stats(["ttf"])
        if actual_ttf_stats != expected_ttf_stats:
            failures.append(f"selected-driven stats for ['ttf'] had unexpected shape: {actual_ttf_stats!r}")

    with tempfile.TemporaryDirectory() as raw_tmp:
        report_root = pathlib.Path(raw_tmp)
        try:
            write_report(
                report_root,
                "production",
                "x64-windows-hybrid",
                "dry-run",
                ["ttf"],
                {"ttf": {"headers": 1, "commands": 2, "generated_files": 3}},
                [],
                [],
                [],
                [],
                False,
            )
            report_text = (report_root / "clangsharp-production.md").read_text(encoding="utf-8")
            if "| ttf | 1 | 2 | 3 |" not in report_text:
                failures.append("write_report did not emit the selected ttf stats row")
            if "| core |" in report_text or "| image |" in report_text:
                failures.append("write_report emitted unselected hardcoded family rows")
            accepted_warning_exit = AcceptedClangSharpWarnings(
                pathlib.Path("SDL_mixer.h"),
                pathlib.Path("SDL_mixer.g.cs"),
                "clangsharp SDL_mixer.h",
                1,
                ("SDL_MIXER_VERSION", "SDL_MIXER_VERSION_ATLEAST"),
                (),
                False,
            )
            write_report(
                report_root,
                "production",
                "x64-windows-hybrid",
                "execute",
                ["mixer"],
                {"mixer": {"headers": 1, "commands": 1, "generated_files": 1}},
                [],
                [],
                [],
                [accepted_warning_exit],
                False,
            )
            report_text = (report_root / "clangsharp-production.md").read_text(encoding="utf-8")
            if "## Accepted Warning-Only ClangSharp Exits" not in report_text:
                failures.append("write_report did not include accepted warning-only exits section")
            if "SDL_MIXER_VERSION, SDL_MIXER_VERSION_ATLEAST" not in report_text:
                failures.append("write_report did not list accepted warning-only macro names")

            try:
                empty_warning_exit = AcceptedClangSharpWarnings(
                    pathlib.Path("SDL_quit.h"),
                    pathlib.Path("SDL_quit.g.cs"),
                    "clangsharp SDL_quit.h",
                    1,
                    ("SDL_QuitRequested",),
                    (),
                    True,
                )
            except TypeError:
                failures.append("AcceptedClangSharpWarnings must carry output path and empty-output status")
            else:
                write_report(
                    report_root,
                    "production",
                    "x64-windows-hybrid",
                    "execute",
                    ["core"],
                    {"core": {"headers": 1, "commands": 1, "generated_files": 0}},
                    [],
                    [],
                    [],
                    [empty_warning_exit],
                    False,
                )
                report_text = (report_root / "clangsharp-production.md").read_text(encoding="utf-8")
                if "- Output: `SDL_quit.g.cs`" not in report_text:
                    failures.append("write_report did not include accepted warning-only output paths")
                if "- Output status: empty (accepted warning-only)" not in report_text:
                    failures.append("write_report did not flag accepted empty warning-only outputs")

            try:
                builtin_warning_exit = AcceptedClangSharpWarnings(
                    pathlib.Path("SDL_mixer.h"),
                    pathlib.Path("SDL_mixer.g.cs"),
                    "clangsharp SDL_mixer.h",
                    1,
                    ("SDL_MIXER_VERSION",),
                    ("warning: undefining builtin macro [-Wbuiltin-macro-redefined]",),
                    False,
                )
            except TypeError:
                failures.append("AcceptedClangSharpWarnings must carry accepted diagnostic warning text")
            else:
                write_report(
                    report_root,
                    "production",
                    "x64-windows-hybrid",
                    "execute",
                    ["mixer"],
                    {"mixer": {"headers": 1, "commands": 1, "generated_files": 1}},
                    [],
                    [],
                    [],
                    [builtin_warning_exit],
                    False,
                )
                report_text = (report_root / "clangsharp-production.md").read_text(encoding="utf-8")
                if "warning: undefining builtin macro [-Wbuiltin-macro-redefined]" not in report_text:
                    failures.append("write_report did not surface accepted non-macro diagnostic warnings")

            if "NoOpGeneratedOutput" in globals():
                try:
                    write_report(
                        report_root,
                        "production",
                        "x64-windows-hybrid",
                        "execute",
                        ["core"],
                        {"core": {"headers": 1, "commands": 1, "generated_files": 1}},
                        [],
                        [],
                        [NoOpGeneratedOutput(
                            pathlib.Path("SDL_bits.h"),
                            pathlib.Path("SDL_bits.g.cs"),
                            "clangsharp SDL_bits.h",
                            "ClangSharp exited 0 with no Layer 1 declarations emitted.",
                        )],
                        [],
                        False,
                    )
                except TypeError:
                    failures.append("write_report must accept report-visible no-op generated outputs")
                else:
                    report_text = (report_root / "clangsharp-production.md").read_text(encoding="utf-8")
                    if "## No-op Generated Outputs" not in report_text:
                        failures.append("write_report did not include no-op generated outputs section")
                    if "SDL_bits.g.cs" not in report_text:
                        failures.append("write_report did not list no-op generated output paths")
        except KeyError as exc:
            failures.append(f"write_report was not selected-driven and raised KeyError: {exc}")

    if "refresh_generated_file_counts" not in globals():
        failures.append("refresh_generated_file_counts helper is missing for final report file counts")
    else:
        with tempfile.TemporaryDirectory() as raw_tmp:
            temp_repo = pathlib.Path(raw_tmp)
            core_generated = generated_root_for_family(temp_repo, "core")
            (core_generated / "Compat" / "SDL.h.g.cs").parent.mkdir(parents=True, exist_ok=True)
            (core_generated / "Compat" / "SDL.h.g.cs").write_text("// compat\n", encoding="utf-8")
            (core_generated / "Modern" / "Platforms" / "Windows" / "SDL_system.g.cs").parent.mkdir(parents=True, exist_ok=True)
            (core_generated / "Modern" / "Platforms" / "Windows" / "SDL_system.g.cs").write_text("// platform\n", encoding="utf-8")
            (core_generated / "Modern" / "ignore.txt").parent.mkdir(parents=True, exist_ok=True)
            (core_generated / "Modern" / "ignore.txt").write_text("not generated\n", encoding="utf-8")

            stats = {
                "core": {"headers": 1, "commands": 2, "generated_files": 999},
                "image": {"headers": 1, "commands": 2, "generated_files": 999},
            }
            refresh_generated_file_counts(temp_repo, ["core", "image"], stats)
            if stats["core"]["generated_files"] != 2:
                failures.append(f"refresh_generated_file_counts did not count recursive .g.cs files; got {stats['core']['generated_files']}")
            if stats["image"]["generated_files"] != 0:
                failures.append(f"refresh_generated_file_counts did not reset missing family counts; got {stats['image']['generated_files']}")

    if "owner_mode_for_family" not in globals():
        failures.append("owner_mode_for_family helper is missing for S1-2 owner-mode policy")
    else:
        for family, expected in (
            ("core", "owner"),
            ("ttf", "owner"),
            ("mixer", "owner"),
            ("image", "consumer"),
            ("gfx", "consumer"),
        ):
            actual = owner_mode_for_family(family)
            if actual != expected:
                failures.append(f"owner-mode wiring for {family!r}: expected {expected!r}, got {actual!r}")

    if "uniform_opaque_extra_args_for_family" not in globals():
        failures.append("uniform_opaque_extra_args_for_family helper is missing for S1-4 handles namespace plumbing")
    else:
        ttf_args = uniform_opaque_extra_args_for_family("ttf")
        expected_ttf_args = ["--owner-mode", "owner", "--handles-namespace", "SDL2.Ttf"]
        if ttf_args != expected_ttf_args:
            failures.append(f"uniform-opaque args for 'ttf': expected {expected_ttf_args!r}, got {ttf_args!r}")

        image_args = uniform_opaque_extra_args_for_family("image")
        expected_image_args = ["--owner-mode", "consumer", "--handles-namespace", "SDL2.Image"]
        if image_args != expected_image_args:
            failures.append(f"uniform-opaque args for 'image': expected {expected_image_args!r}, got {image_args!r}")

    scope_root = find_repository_root() / "spikes" / "binding-generators" / "scope"
    missing_scope_file = scope_root / "__self-test-missing-scope-sentinel__.headers.txt"
    try:
        read_header_list(missing_scope_file)
        failures.append("read_header_list did not raise for missing sentinel header-list file")
    except FileNotFoundError as exc:
        message = str(exc)
        if "For new families" not in message:
            failures.append(f"read_header_list FileNotFoundError lacks 'For new families' hint; got: {message}")
        if "spikes/binding-generators/scope" not in message.replace("\\", "/"):
            failures.append(f"read_header_list FileNotFoundError lacks scope directory hint; got: {message}")
    except KeyError as exc:
        failures.append(f"read_header_list raised KeyError instead of friendly FileNotFoundError: {exc}")
    except Exception as exc:
        failures.append(f"read_header_list raised unexpected exception type {type(exc).__name__}: {exc}")

    if failures:
        for failure in failures:
            print(f"self-test: FAIL: {failure}")
        return 1

    print("self-test: PASS")
    return 0


def main() -> int:
    parser = argparse.ArgumentParser(description="ppy-style ClangSharp spike orchestrator")
    parser.add_argument("--vcpkg-triplet", default="x64-windows-hybrid")
    parser.add_argument("--family", choices=["core", "image", "ttf", "mixer", "gfx", "all"], default="all")
    parser.add_argument("--execute", action="store_true", help="Actually run ClangSharp; absent means print commands only")
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
    no_op_outputs: list[NoOpGeneratedOutput] = []
    accepted_warnings: list[AcceptedClangSharpWarnings] = []
    postprocess_failures = 0
    selected = selected_families(args.family)
    stats = create_generation_stats(selected)

    try:
        headers_by_family = {
            family: read_header_list(scope_root / production_header_list_file_name(family))
            for family in selected
        }
    except FileNotFoundError as exc:
        print(f"ERROR: {exc}")
        return 2

    for family, headers in headers_by_family.items():
        stats[family]["headers"] = len(headers)

    codegen_passes = list(PRODUCTION_CODEGEN_PASSES)

    if should_validate_required_sdlh_surface(args.execute, selected):
        allowlist = read_required_surface_allowlist(scope_root / "sdl2-core-sdlh-required.json")
        validate_required_surface_against_manifest(repo, allowlist)

    if args.execute:
        for family in selected:
            family_generated_root = generated_root_for_family(repo, family)
            if family_generated_root.exists():
                shutil.rmtree(family_generated_root)

    if args.execute:
        print("ppy-style ClangSharp spike scaffold")
        print(f"Repository root: {repo}")
        print(f"Triplet: {args.vcpkg_triplet}")
        print(f"Codegen passes: {codegen_passes}")
        print(f"Mode: {mode}")
        print(f"Platform header shims: {'enabled' if args.use_platform_header_shims else 'disabled'}")

    for codegen in codegen_passes:
        if args.execute:
            print(f"--- codegen pass: {codegen} ---")
        for family in selected:
            headers = headers_by_family[family]
            if args.execute:
                print(f"{family}: {len(headers)} production headers")
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

                    result = subprocess.run(command, cwd=spike_root, capture_output=True, text=True)
                    diagnostic_output = "\n".join(part for part in [result.stdout, result.stderr] if part)
                    header_path = repo / "vcpkg_installed" / args.vcpkg_triplet / "include" / "SDL2" / header
                    accepted: AcceptedClangSharpWarnings | None = None
                    if output_path.is_file():
                        stats[family]["generated_files"] += 1

                    if result.returncode != 0:
                        accepted = classify_warning_only_clangsharp_exit(
                            header_path,
                            output_path,
                            command_line,
                            result.returncode,
                            diagnostic_output,
                        )
                        if accepted is not None:
                            accepted_warnings.append(accepted)
                        else:
                            failures.append((header_path, command_line, result.returncode))
                            if result.stdout:
                                print(f"  STDOUT:\n{result.stdout.rstrip()}")
                            if result.stderr:
                                print(f"  STDERR:\n{result.stderr.rstrip()}")
                    if should_record_no_op_generated_output(output_path, accepted, result.returncode):
                        no_op_outputs.append(NoOpGeneratedOutput(
                            header_path,
                            output_path,
                            command_line,
                            "ClangSharp exited 0 with no Layer 1 declarations emitted.",
                        ))
                    elif should_record_empty_generated_output(output_path, accepted, result.returncode):
                        empty_outputs.append(EmptyGeneratedOutput(header_path, output_path, command_line))

    if should_validate_required_sdlh_surface(args.execute, selected):
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
                        # Header not in the selected production header list.
                        continue
                    print(f"  multi-OS: {family}/{codegen}/{header}")
                    commands_run, platform_failures, platform_empty_outputs, platform_no_op_outputs, platform_accepted_warnings = generate_platform_specific_headers(
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
                    if platform_no_op_outputs:
                        no_op_outputs.extend(platform_no_op_outputs)
                    if platform_accepted_warnings:
                        accepted_warnings.extend(platform_accepted_warnings)

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

    # Uniform opaque handle emit runs last so it sees the final signature shape.
    # Owner-mode families emit their local opaque handle bodies into Handles.g.cs;
    # consumer-mode families only remove local placeholder declarations and
    # rewrite pointer use to the owner-provided handle types they reference.
    #
    # Owner/consumer is declared here explicitly via --owner-mode rather than
    # detected from directory names: owners are families with local opaque
    # handles, while image/gfx-style families consume handles from referenced
    # owner assemblies instead of emitting their own Handles.g.cs file.
    if args.execute:
        print("--- postprocess: uniform-opaque (all codegens) ---")
        for codegen in codegen_passes:
            for family in selected:
                exit_code = run_postprocess(
                    repo, family, spike_root, "uniform-opaque", codegen,
                    extra_args=uniform_opaque_extra_args_for_family(family),
                )
                if exit_code != 0:
                    postprocess_failures += 1
                    print(f"WARNING: uniform-opaque postprocess for {family}/{codegen} returned exit {exit_code}")

        refresh_generated_file_counts(repo, selected, stats)

    write_report(
        reports_root,
        "production",
        args.vcpkg_triplet,
        mode,
        selected,
        stats,
        failures,
        empty_outputs,
        no_op_outputs,
        accepted_warnings,
        args.use_platform_header_shims,
    )

    exit_code = generation_exit_code(failures, empty_outputs, postprocess_failures, accepted_warnings)

    if failures:
        print(f"ERROR: {len(failures)} ClangSharp command(s) failed")
        return exit_code

    if postprocess_failures:
        print(f"ERROR: {postprocess_failures} postprocess command(s) failed")
        return exit_code

    if empty_outputs:
        print(f"ERROR: {len(empty_outputs)} generated output file(s) were empty")
        return exit_code

    return exit_code


if __name__ == "__main__":
    sys.exit(main())
