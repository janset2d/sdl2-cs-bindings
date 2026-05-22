"""
Compare a spike approach's generated output against the Cake-generated preview
oracle and against the SDL2 dynapi export list.

The Cake preview under artifacts/generated-bindings-preview/sdl2-core/ is the
north star for this spike (constitution §"Authority Order" #3). It is not a
bit-for-bit target — ClangSharp emits per-header, Cake emits per-category,
Alimer-style CppAst emits one-file-per-family-internal-class, and the
public-API layering differs — but symbol coverage and dynapi coherence are
direct enough signals to compare.

Outputs spikes/binding-generators/output/reports/oracle-comparison-<approach>.md.

Usage:
    python spikes/binding-generators/clangsharp-style/compare_oracle.py [--approach clangsharp|alimer]
"""

from __future__ import annotations

import argparse
import pathlib
import re
import sys
from datetime import date


def find_repository_root() -> pathlib.Path:
    current = pathlib.Path(__file__).resolve()
    for parent in [current, *current.parents]:
        if (parent / "tools.cs").is_file():
            return parent
    raise RuntimeError("Repository root was not found from compare_oracle.py")


FUNCTION_RE = re.compile(r"static extern\s+[\w*\[\]:\s\.<>]+?\s+(\w+)\s*\(")
ENUM_RE = re.compile(r"^\s*public enum (\w+)", re.MULTILINE)
STRUCT_RE = re.compile(r"^\s*public(?:\s+unsafe)?\s+partial struct (\w+)", re.MULTILINE)
CONSTANT_RE = re.compile(
    r"^\s*public (?:const|static readonly)\s+[\w*\[\]:\s\.<>&]+?\s+(\w+)\s*=",
    re.MULTILINE,
)
CALLBACK_INLINE_RE = re.compile(r"delegate\* unmanaged\[[A-Za-z]+\]<")


def read_text(path: pathlib.Path) -> str:
    return path.read_text(encoding="utf-8")


def extract_symbols(source: str, pattern: re.Pattern[str]) -> set[str]:
    return {match.group(1) for match in pattern.finditer(source)}


def categorize_struct_body(struct_name: str, source: str) -> str:
    """Return 'opaque' if struct body is empty, else 'pod'."""
    needle = re.search(
        rf"public(?:\s+unsafe)?\s+partial struct {re.escape(struct_name)}\b[^\{{]*\{{(.*?)\}}",
        source,
        re.DOTALL,
    )
    if needle is None:
        return "pod"
    body = needle.group(1).strip()
    return "opaque" if body == "" else "pod"


def scan_spike_outputs(spike_generated_roots: list[pathlib.Path]) -> dict[str, set[str] | int]:
    functions: set[str] = set()
    enums: set[str] = set()
    pod_structs: set[str] = set()
    opaque_structs: set[str] = set()
    constants: set[str] = set()
    callbacks_inline = 0

    for root in spike_generated_roots:
        for cs_file in root.rglob("*.g.cs"):
            source = read_text(cs_file)
            functions |= extract_symbols(source, FUNCTION_RE)
            enums |= extract_symbols(source, ENUM_RE)
            constants |= extract_symbols(source, CONSTANT_RE)
            callbacks_inline += len(CALLBACK_INLINE_RE.findall(source))
            for struct_name in extract_symbols(source, STRUCT_RE):
                kind = categorize_struct_body(struct_name, source)
                (opaque_structs if kind == "opaque" else pod_structs).add(struct_name)

    return {
        "functions": functions,
        "enums": enums,
        "pod_structs": pod_structs,
        "opaque_structs": opaque_structs,
        "constants": constants,
        "callbacks_inline_count": callbacks_inline,
    }


def scan_oracle(oracle_root: pathlib.Path) -> dict[str, set[str]]:
    types = oracle_root / "Types"
    platform = oracle_root / "Platform"

    enums = extract_symbols(read_text(types / "Enums.g.cs"), ENUM_RE)
    structs_file = read_text(types / "Structs.g.cs")
    handles_file = read_text(types / "Handles.g.cs")
    callbacks_file = read_text(types / "Callbacks.g.cs")
    constants = extract_symbols(read_text(oracle_root / "Constants.g.cs"), CONSTANT_RE)

    pod_structs = extract_symbols(structs_file, STRUCT_RE) | extract_symbols(
        structs_file, re.compile(r"^public(?:\s+unsafe)?\s+(?:readonly\s+)?partial struct (\w+)", re.MULTILINE)
    )
    opaque_structs = extract_symbols(handles_file, STRUCT_RE) | extract_symbols(
        handles_file, re.compile(r"^public(?:\s+unsafe)?\s+(?:readonly\s+)?partial struct (\w+)", re.MULTILINE)
    )

    callback_names = extract_symbols(
        callbacks_file,
        re.compile(r"^public\s+(?:unsafe\s+)?delegate\s+[\w*\[\]:\s\.<>]+?\s+(\w+)\s*\(", re.MULTILINE),
    )

    functions: set[str] = set()
    for commands in platform.rglob("Commands.g.cs"):
        functions |= extract_symbols(read_text(commands), FUNCTION_RE)

    return {
        "functions": functions,
        "enums": enums,
        "pod_structs": pod_structs,
        "opaque_structs": opaque_structs,
        "callbacks": callback_names,
        "constants": constants,
    }


def find_dynapi_exports(repo: pathlib.Path) -> tuple[pathlib.Path | None, set[str]]:
    candidates = list(repo.glob("external/vcpkg/buildtrees/sdl2/src/*/src/dynapi/SDL2.exports"))
    candidates += list(repo.glob("vcpkg_installed/vcpkg/blds/sdl2/src/*/src/dynapi/SDL2.exports"))
    if not candidates:
        return None, set()
    chosen = candidates[0]
    # SDL2.exports lines look like:
    #   ++'_SDL_Init'.'SDL2.dll'.'SDL_Init'
    # The third quoted field is the unmangled function name.
    line_re = re.compile(r"^\+\+'[^']+'\.'[^']+'\.'([^']+)'")
    exports: set[str] = set()
    for line in read_text(chosen).splitlines():
        stripped = line.strip()
        if not stripped or stripped.startswith("#"):
            continue
        match = line_re.match(stripped)
        if match:
            exports.add(match.group(1))
    return chosen, exports


def render_set_diff(title: str, left_label: str, right_label: str, left: set[str], right: set[str], limit: int = 30) -> list[str]:
    only_left = sorted(left - right)
    only_right = sorted(right - left)
    common = left & right
    lines = [
        f"### {title}",
        "",
        f"- {left_label}: **{len(left)}**",
        f"- {right_label}: **{len(right)}**",
        f"- Common: **{len(common)}**",
        f"- {left_label}-only: **{len(only_left)}**",
        f"- {right_label}-only: **{len(only_right)}**",
        "",
    ]
    if only_left:
        lines.append(f"<details><summary>{left_label}-only ({len(only_left)})</summary>")
        lines.append("")
        lines.append("```text")
        lines.extend(only_left[:limit])
        if len(only_left) > limit:
            lines.append(f"... ({len(only_left) - limit} more)")
        lines.append("```")
        lines.append("")
        lines.append("</details>")
        lines.append("")
    if only_right:
        lines.append(f"<details><summary>{right_label}-only ({len(only_right)})</summary>")
        lines.append("")
        lines.append("```text")
        lines.extend(only_right[:limit])
        if len(only_right) > limit:
            lines.append(f"... ({len(only_right) - limit} more)")
        lines.append("```")
        lines.append("")
        lines.append("</details>")
        lines.append("")
    return lines


APPROACH_CONFIG = {
    "clangsharp": {
        "scan_roots": [
            "clangsharp/src/Janset.SDL2.Core/Generated",
        ],
        "report_name": "oracle-comparison-clangsharp.md",
        "label": "ClangSharp Spike",
        "display_root": "clangsharp/src/Janset.SDL2.Core/Generated/",
    },
    "alimer": {
        "scan_roots": ["output/alimer/Generated"],
        "report_name": "oracle-comparison-alimer.md",
        "label": "Alimer-Style CppAst Spike",
        "display_root": "output/alimer/Generated/",
    },
}


def main() -> int:
    parser = argparse.ArgumentParser(description="Compare spike output to Cake oracle + SDL2 dynapi")
    parser.add_argument("--approach", choices=list(APPROACH_CONFIG.keys()), default="clangsharp")
    args = parser.parse_args()
    config = APPROACH_CONFIG[args.approach]

    repo = find_repository_root()
    spike_generated_roots = [
        repo / "spikes" / "binding-generators" / sub for sub in config["scan_roots"]
    ]
    existing_roots = [d for d in spike_generated_roots if d.is_dir()]
    oracle_root = repo / "artifacts" / "generated-bindings-preview" / "sdl2-core"
    reports_root = repo / "spikes" / "binding-generators" / "output" / "reports"
    reports_root.mkdir(parents=True, exist_ok=True)

    if not existing_roots:
        print(f"No spike generated output found under: {[str(d) for d in spike_generated_roots]}", file=sys.stderr)
        return 2
    if not oracle_root.is_dir():
        print(f"Cake oracle preview not found: {oracle_root}", file=sys.stderr)
        return 2

    spike = scan_spike_outputs(existing_roots)
    oracle = scan_oracle(oracle_root)
    dynapi_path, dynapi_exports = find_dynapi_exports(repo)

    summary_rows = [
        ("Functions", len(spike["functions"]), len(oracle["functions"])),
        ("Enums", len(spike["enums"]), len(oracle["enums"])),
        ("POD structs", len(spike["pod_structs"]), len(oracle["pod_structs"])),
        ("Opaque handles", len(spike["opaque_structs"]), len(oracle["opaque_structs"])),
        ("Constants", len(spike["constants"]), len(oracle["constants"])),
        ("Callbacks", spike["callbacks_inline_count"], len(oracle["callbacks"])),
    ]

    lines: list[str] = [
        f"# Oracle Comparison — {config['label']} vs Cake-generated Preview",
        "",
        f"**Date:** {date.today().isoformat()}",
        f"**Spike output:** `spikes/binding-generators/{config['display_root']}`",
        "**Oracle:** `artifacts/generated-bindings-preview/sdl2-core/`",
        f"**Dynapi exports source:** `{dynapi_path.relative_to(repo)}`" if dynapi_path else "**Dynapi exports source:** *not found locally*",
        "",
        "## Why this is not a 1:1 comparison",
        "",
        "- ClangSharp emits per-header (each `.g.cs` mixes functions, enums, structs, constants for that header).",
        "- Cake emits per-category (`Enums.g.cs`, `Structs.g.cs`, `Handles.g.cs`, `Callbacks.g.cs`, `Constants.g.cs`, per-platform `Commands.g.cs`).",
        "- Cake distinguishes opaque handles from POD structs; the spike has to infer it from empty struct bodies.",
        "- Cake emits named callback delegates (`SDL_AudioCallback` etc.); ClangSharp emits inline `delegate* unmanaged[Cdecl]<...>` at every use site — the inline count is informational only.",
        "- Cake's macro pipeline emits computed constants (FOURCC literals etc.) that ClangSharp may inline at the use site rather than as standalone constants; expect spike `Constants` to skew low or differently-shaped.",
        "",
        "## Summary",
        "",
        "| Category | Spike | Oracle | Delta |",
        "| --- | ---: | ---: | ---: |",
    ]

    for name, left, right in summary_rows:
        delta = left - right
        lines.append(f"| {name} | {left} | {right} | {delta:+d} |")

    lines.append("")

    if dynapi_path is not None:
        dyn_missing = sorted(dynapi_exports - spike["functions"])
        dyn_extra = sorted(spike["functions"] - dynapi_exports)
        lines.extend([
            "## Dynapi Coherence",
            "",
            f"- Dynapi exports: **{len(dynapi_exports)}**",
            f"- Spike functions: **{len(spike['functions'])}**",
            f"- In dynapi AND emitted: **{len(dynapi_exports & spike['functions'])}**",
            f"- In dynapi but NOT emitted: **{len(dyn_missing)}**",
            f"- Emitted but NOT in dynapi: **{len(dyn_extra)}**",
            "",
        ])
        if dyn_missing:
            lines.append(f"<details><summary>In dynapi, missing from spike ({len(dyn_missing)})</summary>")
            lines.append("")
            lines.append("```text")
            lines.extend(dyn_missing[:60])
            if len(dyn_missing) > 60:
                lines.append(f"... ({len(dyn_missing) - 60} more)")
            lines.append("```")
            lines.append("")
            lines.append("</details>")
            lines.append("")
        if dyn_extra:
            lines.append(f"<details><summary>Emitted but not in dynapi ({len(dyn_extra)})</summary>")
            lines.append("")
            lines.append("```text")
            lines.extend(dyn_extra[:60])
            if len(dyn_extra) > 60:
                lines.append(f"... ({len(dyn_extra) - 60} more)")
            lines.append("```")
            lines.append("")
            lines.append("</details>")
            lines.append("")
    else:
        lines.extend([
            "## Dynapi Coherence",
            "",
            "Dynapi exports file not found. Searched:",
            "- `external/vcpkg/buildtrees/sdl2/src/*/src/dynapi/SDL2.exports`",
            "- `vcpkg_installed/vcpkg/blds/sdl2/src/*/src/dynapi/SDL2.exports`",
            "",
        ])

    lines.append("## Per-Category Set Diffs")
    lines.append("")
    lines.extend(render_set_diff("Functions", "Spike", "Oracle", spike["functions"], oracle["functions"], limit=50))
    lines.extend(render_set_diff("Enums", "Spike", "Oracle", spike["enums"], oracle["enums"], limit=40))
    lines.extend(render_set_diff("POD structs", "Spike", "Oracle", spike["pod_structs"], oracle["pod_structs"], limit=40))
    lines.extend(render_set_diff("Opaque handles", "Spike", "Oracle", spike["opaque_structs"], oracle["opaque_structs"], limit=40))
    lines.extend(render_set_diff("Constants", "Spike", "Oracle", spike["constants"], oracle["constants"], limit=80))

    output_path = reports_root / config["report_name"]
    output_path.write_text("\n".join(lines) + "\n", encoding="utf-8")
    print(f"Wrote {output_path}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
