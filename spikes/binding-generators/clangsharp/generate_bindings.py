import argparse
import pathlib
import re
import shutil
import subprocess
import sys
from dataclasses import dataclass


@dataclass(frozen=True)
class EmptyGeneratedOutput:
    header_path: pathlib.Path
    output_path: pathlib.Path
    command_line: str


def platform_header_shim_root(repo: pathlib.Path) -> pathlib.Path:
    return repo / "spikes" / "binding-generators" / "clangsharp" / "shims" / "platform-headers"


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


FAMILY_CONFIG = {
    "core": {
        "rsp": "sdl2-core.rsp",
        "bootstrap_scope": "bootstrap-sdl2-core.headers.txt",
        "full_scope": "sdl2-core.headers.txt",
        "library_dir": "Janset.SDL2.Core",
    },
    "image": {
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
    rsp_root = repo / "spikes" / "binding-generators" / "clangsharp" / "rsp"

    defined_names = {value.split("=", 1)[0] for value in view_defines}
    undefines = [macro for macro in ALL_PLATFORM_MACROS if macro not in defined_names]

    command: list[str] = [
        "dotnet", "tool", "run", "ClangSharpPInvokeGenerator",
        "--config",
    ]
    command.extend(CODEGEN_CONFIG[codegen])
    command.extend([
        f"@{rsp_root / 'base.rsp'}",
        f"@{rsp_root / FAMILY_CONFIG[family]['rsp']}",
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
    """Invoke the Microsoft.CodeAnalysis postprocess. mode = 'strip-varargs' (drops
    __arglist per Constitution L162-176 fmt-only policy) or 'libraryimport'
    (promotes DllImport → LibraryImport per Constitution L48 backend split).
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
    rsp_root = repo / "spikes" / "binding-generators" / "clangsharp" / "rsp"

    command: list[str] = [
        "dotnet", "tool", "run", "ClangSharpPInvokeGenerator",
        "--config",
    ]
    command.extend(CODEGEN_CONFIG[codegen])
    command.extend([
        f"@{rsp_root / 'base.rsp'}",
        f"@{rsp_root / FAMILY_CONFIG[family]['rsp']}",
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
    args = parser.parse_args()

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
