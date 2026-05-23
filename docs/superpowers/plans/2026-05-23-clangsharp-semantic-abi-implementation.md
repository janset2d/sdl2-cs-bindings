# ClangSharp Semantic ABI Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the ClangSharp spike honestly classify and handle semantic ABI hazards from the C-track research: capability limits, dynapi evidence, `SDL_RWops`, C `long`, `wchar_t*`, opaque handles, and SysWM layout proof/deferral.

**Architecture:** Add a small spike-local semantic ABI catalog and evidence pipeline around the existing ClangSharp generator. C0 runs first as an evidence-gated mechanism matrix: each later slice consumes the previous slice's report instead of relying on pre-baked assumptions. SysWM is split into classification, native-probe infrastructure, proven layout synthesis, and `SDL_GetWindowWMInfo` re-entry so missing platform proof remains explicit instead of blocking scalar, wide-character, opaque-handle, and dynapi fixes.

**Tech Stack:** Python 3 spike orchestrator, ClangSharpPInvokeGenerator RSP probes, Roslyn source rewriters on .NET 10, .NET 10 file-based oracle, generated C# multi-TFM projects, native C layout probes where available, Slopwatch.

---

## File Structure

- Create: `spikes/binding-generators/clangsharp/semantic_abi.py`
  - Spike-local semantic ABI catalog, C0 probe definitions, C0 mechanism report renderer, and pure self-tests.
- Create: `spikes/binding-generators/clangsharp/probes/semantic_abi_probe.h`
  - Minimal C declarations for ClangSharp capability probes: C `long`, `wchar_t*`, opaque/incomplete structs, pointer depth, and marker experiments.
- Create: `spikes/binding-generators/clangsharp/probes/syswm_layout_probe.c`
  - Native layout probe source for `SDL_syswm.h` size/alignment/offset evidence.
- Modify: `spikes/binding-generators/clangsharp/generate_bindings.py`
  - Add `--probe-semantic-abi`, call semantic catalog self-tests from `--self-test`, route C0 probe output, and later apply selected synthetic/rewrite generation hooks.
- Modify: `spikes/binding-generators/clangsharp/oracle.cs`
  - Add C0/C6 evidence categories, A/B regression summaries, dynapi duplicate counts, SysWM view classification, and resolved/deferred semantic ABI reporting.
- Modify: `spikes/binding-generators/clangsharp/postprocess/Program.cs`
  - Add `self-test`, relative-path-aware rewrite modes, and catalog-driven fallback modes introduced by C3-C5.
- Create: `spikes/binding-generators/clangsharp/postprocess/OpaqueHandleRewriteRewriter.cs`
  - C5 fallback opaque pointer rewrite driven directly by a generated catalog file. Do not leave marker attributes in final output.
- Create: `spikes/binding-generators/clangsharp/postprocess/NativeTypeRewriteRewriter.cs`
  - C3/C4 source-time native type rewrite for Modern C `long` / `unsigned long`, shared `wchar_t*`, and any exact Compat C-long strategy proven by the Compat prototype.
- Create: `spikes/binding-generators/clangsharp/postprocess/PostProcessSelfTests.cs`
  - Roslyn self-tests for pointer-depth rewrite and marker-removal guardrails.
- Regenerate: `spikes/binding-generators/clangsharp/src/Janset.SDL2.Core/Generated/**`
  - ClangSharp Core generated output after each slice that changes generation.
- Regenerate: `spikes/binding-generators/clangsharp/src/Janset.SDL2.Image/Generated/**`
  - Image generated output only when A/B regression gates or shared postprocess changes require it.
- Regenerate: `spikes/binding-generators/output/reports/clangsharp-full.md`
  - Generation command/report evidence.
- Regenerate: `spikes/binding-generators/output/reports/oracle-evidence-clangsharp.md`
  - Final oracle evidence for fixed, deferred, and residual findings.
- Regenerate: `spikes/binding-generators/output/reports/semantic-abi-capabilities.md`
  - C0 mechanism matrix: hazard, mechanism tested, result, selected layer, fallback, tests, deferral condition.
- Regenerate: `spikes/binding-generators/output/reports/syswm-layout-evidence.md`
  - C2 native layout evidence when probes are available; explicit unavailable-platform entries when they are not.

Do not modify production `src/`, project files, build system, vcpkg config, manifest schema, CI, or CppAst production generator code in this plan. Documentation-only plan/spec files are already in scope. Any commit step below requires Deniz approval before running `git commit`.

## Evidence-Gated Execution Rules

This plan is intentionally not a straight-line "we already know the fix" recipe. The C-track has semantic ABI hazards where compilation success can be wrong. Execute it as an evidence-gated loop:

1. Each risky task starts with a hypothesis and a small probe, prototype, or report.
2. The next task consumes that evidence and follows an explicit branch table.
3. If the evidence contradicts the expected path, do not force the next implementation step. Update the C0/Cx report and ask Deniz for a decision when the branch is not already described here.
4. Prefer ClangSharp/RSP/header-profile mechanisms first. Roslyn postprocess is allowed only when C0 records why ClangSharp cannot express the shape safely, or when it is backend syntax cleanup such as `libraryimport` / `strip-varargs`.
5. A finding may remain reported only when the plan names the reason. Do not hide a hazard because the generated project happens to compile.
6. Compat C `long` is not an automatic deferral. Research and prototype exact downlevel code generation before accepting any Compat outcome.

Every slice must keep the A/B regression gates visible: `raw-abi-public-class: 0`, `raw-abi-public-import: 0`, `family-namespace-drift: 0`, and SDL.h required function/constant missing count `0` for the B-slice surface.

## Task 1: Add Semantic ABI Catalog And C0 Probe Harness

**Files:**
- Create: `spikes/binding-generators/clangsharp/semantic_abi.py`
- Create: `spikes/binding-generators/clangsharp/probes/semantic_abi_probe.h`
- Modify: `spikes/binding-generators/clangsharp/generate_bindings.py`

- [ ] **Step 1: Write the semantic ABI probe header**

Create `spikes/binding-generators/clangsharp/probes/semantic_abi_probe.h` with exactly this C surface:

```c
#pragma once

typedef struct SDL_semantic_opaque SDL_semantic_opaque;
typedef struct SDL_semantic_private_tag SDL_semantic_public_handle;

long JansetProbe_ReturnLong(void);
unsigned long JansetProbe_ReturnUnsignedLong(void);
void JansetProbe_TakeLong(long value);
void JansetProbe_TakeUnsignedLong(unsigned long value);

const wchar_t* JansetProbe_ReturnConstWchar(void);
wchar_t* JansetProbe_ReturnWchar(void);
void JansetProbe_TakeConstWchar(const wchar_t* value);
void JansetProbe_TakeWchar(wchar_t* value);
void JansetProbe_TakeWcharOut(wchar_t** value);
void JansetProbe_TakeConstWcharOut(const wchar_t** value);

SDL_semantic_opaque* JansetProbe_ReturnOpaque(void);
void JansetProbe_TakeOpaque(SDL_semantic_opaque* value);
void JansetProbe_TakeOpaqueOut(SDL_semantic_opaque** value);
void JansetProbe_TakeConstOpaque(const SDL_semantic_opaque* value);

SDL_semantic_public_handle* JansetProbe_ReturnTypedefHandle(void);
void JansetProbe_TakeTypedefHandle(SDL_semantic_public_handle* value);

typedef void (*JansetProbe_OpaqueCallback)(SDL_semantic_opaque* value, SDL_semantic_opaque** output);
typedef void (*JansetProbe_WcharCallback)(const wchar_t* value, wchar_t** output);

struct JansetProbe_FieldCarrier
{
    long long_field;
    unsigned long unsigned_long_field;
    wchar_t* wide_field;
    SDL_semantic_opaque* opaque_field;
    SDL_semantic_opaque** opaque_out_field;
    JansetProbe_OpaqueCallback opaque_callback;
    JansetProbe_WcharCallback wchar_callback;
};
```

- [ ] **Step 2: Add semantic ABI catalog module skeleton**

Create `spikes/binding-generators/clangsharp/semantic_abi.py` with this initial content:

```python
from __future__ import annotations

import dataclasses
import pathlib
import subprocess
from typing import Iterable


@dataclasses.dataclass(frozen=True)
class SemanticHazard:
    id: str
    description: str
    preferred_mechanisms: tuple[str, ...]
    deferral_condition: str


@dataclasses.dataclass(frozen=True)
class MechanismDecision:
    hazard: str
    preferred_mechanism_tested: str
    result: str
    selected_implementation_layer: str
    fallback_if_selected_layer_fails: str
    required_tests_evidence: str
    deferral_condition: str


@dataclasses.dataclass(frozen=True)
class ProbeRun:
    name: str
    command: tuple[str, ...]
    output_path: pathlib.Path


SEMANTIC_HAZARDS: tuple[SemanticHazard, ...] = (
    SemanticHazard(
        "c-long",
        "C long and unsigned long must not map to pointer-sized or Windows-only managed types.",
        ("clangsharp-rsp", "modern-semantic-rewrite", "compat-manual-codegen", "blocker"),
        "No exact Modern or Compat strategy is proven for every emitted declaration category.",
    ),
    SemanticHazard(
        "wchar-pointer",
        "Shared wchar_t pointers must not map to ushort* outside Windows-only APIs.",
        ("clangsharp-rsp", "semantic-rewrite", "explicit-deferral"),
        "Pointer-depth or platform-specific Unicode ownership cannot be represented honestly.",
    ),
    SemanticHazard(
        "opaque-handle",
        "Opaque SDL handles must use canonical SDL typedef names, not parser-private tags.",
        ("clangsharp-rsp", "semantic-catalog", "marker-rewrite", "explicit-deferral"),
        "Pointer-depth or canonical-name rewrite cannot be proven for fields, returns, parameters, and callbacks.",
    ),
    SemanticHazard(
        "sdl-rwops",
        "SDL_RWops must not expose a false full layout.",
        ("clangsharp-exclude-remap", "synthetic-opaque", "marker-rewrite", "explicit-deferral"),
        "Opaque replacement does not preserve all SDL_RWops reference contexts.",
    ),
    SemanticHazard(
        "sdl-syswm",
        "SDL_SysWMinfo and SDL_SysWMmsg require platform size/alignment/offset proof.",
        ("platform-evidence", "synthetic-layout", "explicit-deferral"),
        "Native layout proof is unavailable for an emitted platform view.",
    ),
    SemanticHazard(
        "dynapi-evidence",
        "Dynapi evidence is name coverage only and must distinguish totals, uniques, duplicates, exclusions, and deferrals.",
        ("oracle-report",),
        "Dynapi source files are absent from the local checkout.",
    ),
)


def default_mechanism_decisions() -> tuple[MechanismDecision, ...]:
    return tuple(
        MechanismDecision(
            hazard=hazard.id,
            preferred_mechanism_tested=", ".join(hazard.preferred_mechanisms),
            result="Not run",
            selected_implementation_layer="not-selected",
            fallback_if_selected_layer_fails="stop-and-review",
            required_tests_evidence="Run --probe-semantic-abi and oracle self-test.",
            deferral_condition=hazard.deferral_condition,
        )
        for hazard in SEMANTIC_HAZARDS
    )


def render_mechanism_report(decisions: Iterable[MechanismDecision]) -> str:
    lines = [
        "# Semantic ABI Capability Matrix",
        "",
        "| Hazard | Preferred Mechanism Tested | Result | Selected Layer | Fallback | Required Tests/Evidence | Deferral Condition |",
        "| --- | --- | --- | --- | --- | --- | --- |",
    ]
    for decision in decisions:
        lines.append(
            "| "
            + " | ".join(
                escape_cell(value)
                for value in (
                    decision.hazard,
                    decision.preferred_mechanism_tested,
                    decision.result,
                    decision.selected_implementation_layer,
                    decision.fallback_if_selected_layer_fails,
                    decision.required_tests_evidence,
                    decision.deferral_condition,
                )
            )
            + " |"
        )
    lines.append("")
    return "\n".join(lines)


def escape_cell(value: str) -> str:
    return value.replace("|", "\\|").replace("\n", " ")


def write_default_mechanism_report(repo: pathlib.Path) -> pathlib.Path:
    report_path = repo / "spikes" / "binding-generators" / "output" / "reports" / "semantic-abi-capabilities.md"
    report_path.parent.mkdir(parents=True, exist_ok=True)
    report_path.write_text(render_mechanism_report(default_mechanism_decisions()), encoding="utf-8")
    return report_path


def run_self_tests() -> list[str]:
    failures: list[str] = []
    report = render_mechanism_report(default_mechanism_decisions())
    for hazard in SEMANTIC_HAZARDS:
        if hazard.id not in report:
            failures.append(f"missing hazard in mechanism report: {hazard.id}")
    if "| Hazard | Preferred Mechanism Tested | Result | Selected Layer |" not in report:
        failures.append("mechanism report header is missing")
    return failures
```

- [ ] **Step 3: Wire semantic ABI self-tests into `generate_bindings.py`**

At the top of `generate_bindings.py`, add:

```python
from semantic_abi import run_self_tests as run_semantic_abi_self_tests
from semantic_abi import write_default_mechanism_report
```

Inside `run_self_tests()` before the final `if failures:` block, add:

```python
    failures.extend(run_semantic_abi_self_tests())
```

Add this CLI argument after `--self-test`:

```python
    parser.add_argument("--probe-semantic-abi", action="store_true", help="Write the C0 semantic ABI mechanism report and exit")
```

After the `args.self_test` branch, add:

```python
    if args.probe_semantic_abi:
        repo = find_repository_root()
        report_path = write_default_mechanism_report(repo)
        print(f"semantic-abi report: {report_path}")
        return 0
```

- [ ] **Step 4: Run the red/green checks**

Run:

```pwsh
python spikes/binding-generators/clangsharp/generate_bindings.py --self-test
```

Expected: `self-test: PASS`.

Run:

```pwsh
python spikes/binding-generators/clangsharp/generate_bindings.py --probe-semantic-abi
```

Expected: command exits `0` and writes `spikes/binding-generators/output/reports/semantic-abi-capabilities.md`.

- [ ] **Step 5: Review the C0 report**

Open `spikes/binding-generators/output/reports/semantic-abi-capabilities.md` and confirm it contains all hazards:

```text
c-long
wchar-pointer
opaque-handle
sdl-rwops
sdl-syswm
dynapi-evidence
```

- [ ] **Step 6: Commit checkpoint, approval required**

Before committing, present the changed files and this proposed message to Deniz:

```text
test(binding-spike): add semantic ABI capability report scaffold
```

After explicit approval, run:

```pwsh
git add spikes/binding-generators/clangsharp/semantic_abi.py spikes/binding-generators/clangsharp/probes/semantic_abi_probe.h spikes/binding-generators/clangsharp/generate_bindings.py spikes/binding-generators/output/reports/semantic-abi-capabilities.md
git commit -m "test(binding-spike): add semantic ABI capability report scaffold"
```

## Task 2: Add Oracle Taxonomy, A/B Regression Gates, And Dynapi Detail

**Files:**
- Modify: `spikes/binding-generators/clangsharp/oracle.cs`
- Regenerate: `spikes/binding-generators/output/reports/oracle-evidence-clangsharp.md`

- [ ] **Step 1: Add richer dynapi evidence types**

Replace the current `DynapiEvidence` shape with a record that preserves total occurrences and duplicates. Keep the existing `Exports` property name for unique names so existing call sites stay small:

```csharp
internal sealed record DynapiEvidence(
    SourceStatus Status,
    IReadOnlySet<string> Exports,
    int TotalOccurrences,
    IReadOnlyDictionary<string, int> DuplicateOccurrences)
{
    public static DynapiEvidence NotApplicable { get; } = new(
        SourceStatus.NotApplicable,
        new HashSet<string>(StringComparer.Ordinal),
        0,
        new Dictionary<string, int>(StringComparer.Ordinal));
}
```

Update every `new DynapiEvidence(...)` call site to pass unique exports, total count, and duplicate dictionary. For not-applicable cases, use `DynapiEvidence.NotApplicable`.

- [ ] **Step 2: Add parser result for dynapi duplicates**

Add this record above `DynapiParser`:

```csharp
internal sealed record DynapiParseResult(
    IReadOnlySet<string> UniqueNames,
    int TotalOccurrences,
    IReadOnlyDictionary<string, int> ExportCounts,
    IReadOnlyDictionary<string, int> DuplicateOccurrences);
```

Change `DynapiParser.Parse` to return `DynapiParseResult`:

```csharp
public static DynapiParseResult Parse(string text)
{
    const char quote = (char)39;
    var counts = new Dictionary<string, int>(StringComparer.Ordinal);
    var total = 0;
    foreach (var line in text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
    {
        var fields = line.Split(quote);
        if (fields.Length < 6)
        {
            continue;
        }

        var export = fields[5];
        total++;
        counts[export] = counts.TryGetValue(export, out var current) ? current + 1 : 1;
    }

    var unique = counts.Keys.ToHashSet(StringComparer.Ordinal);
    var duplicates = counts
        .Where(pair => pair.Value > 1)
        .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
    return new DynapiParseResult(unique, total, counts, duplicates);
}
```

- [ ] **Step 3: Update dynapi loader and self-test**

Update `DynapiEvidenceLoader.Load` so it merges counts from every export file:

```csharp
var counts = new Dictionary<string, int>(StringComparer.Ordinal);
var total = 0;
foreach (var path in FindDynapiExportPaths(repoRoot))
{
    var parsed = DynapiParser.Parse(File.ReadAllText(path));
    total += parsed.TotalOccurrences;
    foreach (var (export, count) in parsed.ExportCounts)
    {
        counts[export] = counts.TryGetValue(export, out var current) ? current + count : count;
    }
}

var exports = counts.Keys.ToHashSet(StringComparer.Ordinal);
var duplicates = counts
    .Where(pair => pair.Value > 1)
    .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
return exports.Count == 0
    ? new DynapiEvidence(SourceStatus.Missing, exports, total, duplicates)
    : new DynapiEvidence(SourceStatus.Present, exports, total, duplicates);
```

Update the self-test fixture to include a duplicate `SDL_Log` export:

```csharp
public const string DynapiExports = """
++'_SDL_Init'.'SDL2.dll'.'SDL_Init'
++'_SDL_Log'.'SDL2.dll'.'SDL_Log'
++'_SDL_Log_REAL'.'SDL2.dll'.'SDL_Log'
""";
```

Add self-test expectations:

```csharp
var dynapiParse = DynapiParser.Parse(Fixtures.DynapiExports);
Expect(dynapiParse.UniqueNames.Contains("SDL_Init"), "parses SDL2 dynapi exports", failures);
Expect(dynapiParse.TotalOccurrences == 3, "counts total dynapi occurrences", failures);
Expect(dynapiParse.ExportCounts.TryGetValue("SDL_Log", out var logTotal) && logTotal == 2, "preserves same-file dynapi duplicate counts", failures);
Expect(dynapiParse.DuplicateOccurrences.TryGetValue("SDL_Log", out var logCount) && logCount == 2, "reports duplicate dynapi exports", failures);
```

- [ ] **Step 4: Add A/B regression summary to Markdown**

In `MarkdownReportRenderer`, add a section after the family raw ABI checks that renders these four gates for every report:

```text
raw-abi-public-class: expected 0
raw-abi-public-import: expected 0
family-namespace-drift: expected 0 for Image
required-function-missing / required-constant-missing: expected 0 for Core
```

Use existing `RawAbiChecks` results; do not add a second checking system.

- [ ] **Step 5: Run oracle self-test**

Run:

```pwsh
dotnet run --file spikes/binding-generators/clangsharp/oracle.cs -- --self-test
```

Expected: `self-test: PASS`.

- [ ] **Step 6: Regenerate oracle report**

Run:

```pwsh
dotnet run --file spikes/binding-generators/clangsharp/oracle.cs -- --family sdl2-core --family sdl2-image --write-report
```

Expected: `Report: ...oracle-evidence-clangsharp.md` and no parse error.

- [ ] **Step 7: Commit checkpoint, approval required**

Before committing, present the changed files and this proposed message to Deniz:

```text
test(binding-spike): classify semantic ABI evidence in oracle
```

After explicit approval, run:

```pwsh
git add spikes/binding-generators/clangsharp/oracle.cs spikes/binding-generators/output/reports/oracle-evidence-clangsharp.md
git commit -m "test(binding-spike): classify semantic ABI evidence in oracle"
```

## Task 3: Replace The Default C0 Report With Data-Driven Probe Decisions

**Files:**
- Modify: `spikes/binding-generators/clangsharp/semantic_abi.py`
- Modify: `spikes/binding-generators/clangsharp/generate_bindings.py`
- Regenerate: `spikes/binding-generators/output/reports/semantic-abi-capabilities.md`

- [ ] **Step 1: Add probe variants that test mechanisms before selecting fallbacks**

Extend `ProbeRun` so each run records its purpose and command-line variant:

```python
@dataclasses.dataclass(frozen=True)
class ProbeRun:
    name: str
    purpose: str
    command: tuple[str, ...]
    output_path: pathlib.Path
```

Add a command builder that emits a matrix. The matrix must include baseline, explicit C-long remaps, `--with-type`, `--with-attribute`, `--exclude`, `--with-transparent-struct`, `exclude-empty-records`, `latest-codegen`, `compatible-codegen`, `windows-types`, and `unix-types` probes:

```python
def build_probe_runs(repo: pathlib.Path) -> tuple[ProbeRun, ...]:
    probe_header = repo / "spikes" / "binding-generators" / "clangsharp" / "probes" / "semantic_abi_probe.h"
    output_root = repo / "spikes" / "binding-generators" / "output" / "semantic-abi-probes"
    rsp_root = repo / "spikes" / "binding-generators" / "clangsharp" / "rsp"
    base_rsp = "@" + str(rsp_root / "base.rsp")

    def command(name: str, configs: tuple[str, ...], extra: tuple[str, ...] = ()) -> ProbeRun:
        output_path = output_root / f"{name}.g.cs"
        return ProbeRun(
            name=name,
            purpose=PROBE_PURPOSES[name],
            command=(
                "dotnet", "tool", "run", "ClangSharpPInvokeGenerator",
                "--config", *configs,
                base_rsp,
                *extra,
                "--namespace", "SemanticAbiProbe",
                "--file", str(probe_header),
                "--output", str(output_path),
            ),
            output_path=output_path,
        )

    return (
        command("latest-only", ("latest-codegen",)),
        command("latest-windows-types", ("latest-codegen", "windows-types")),
        command("latest-unix-types", ("latest-codegen", "unix-types")),
        command("compatible-windows-types", ("compatible-codegen", "windows-types")),
        command("compatible-unix-types", ("compatible-codegen", "unix-types")),
        command("long-remap-windows-types", ("latest-codegen", "windows-types"), ("--remap", "long=CLong", "--remap", "unsigned long=CULong")),
        command("long-remap-unix-types", ("latest-codegen", "unix-types"), ("--remap", "long=CLong", "--remap", "unsigned long=CULong")),
        command("with-type-long", ("latest-codegen", "unix-types"), ("--with-type", "long=CLong", "--with-type", "unsigned long=CULong")),
        command("with-attribute-opaque", ("latest-codegen", "unix-types"), ("--with-attribute", "SDL_semantic_opaque=NativeOpaqueType")),
        command("exclude-rwops-shape", ("latest-codegen", "unix-types"), ("--exclude", "SDL_RWops")),
        command("transparent-struct", ("latest-codegen", "unix-types"), ("--with-transparent-struct", "SDL_semantic_opaque")),
        command("exclude-empty-records", ("latest-codegen", "unix-types", "exclude-empty-records")),
    )
```

Add this dictionary above `build_probe_runs` so report output explains why each file exists:

```python
PROBE_PURPOSES: dict[str, str] = {
    "latest-only": "Detect whether trailing type-mode config values change output at all.",
    "latest-windows-types": "Baseline latest-codegen Windows data-model output.",
    "latest-unix-types": "Baseline latest-codegen Unix data-model output.",
    "compatible-windows-types": "Baseline compatible-codegen Windows data-model output.",
    "compatible-unix-types": "Baseline compatible-codegen Unix data-model output.",
    "long-remap-windows-types": "Test whether --remap can express C long / unsigned long as CLong / CULong on Windows mode.",
    "long-remap-unix-types": "Test whether --remap can express C long / unsigned long as CLong / CULong on Unix mode.",
    "with-type-long": "Test whether --with-type can express C long / unsigned long mappings.",
    "with-attribute-opaque": "Test whether --with-attribute can mark opaque handle declarations.",
    "exclude-rwops-shape": "Test whether --exclude removes a layout declaration while pointer references remain compilable.",
    "transparent-struct": "Test whether --with-transparent-struct helps opaque handle shape.",
    "exclude-empty-records": "Test whether empty parser tag structs can be omitted safely.",
}
```

- [ ] **Step 2: Execute or dry-run probes from `--probe-semantic-abi`**

Add this function to `semantic_abi.py`:

```python
def run_probe_commands(repo: pathlib.Path, execute: bool) -> tuple[MechanismDecision, ...]:
    for run in build_probe_runs(repo):
        run.output_path.parent.mkdir(parents=True, exist_ok=True)
        print(f"# {run.name}: {run.purpose}")
        print(" ".join(run.command))
        if not execute:
            continue
        result = subprocess.run(run.command, cwd=repo / "spikes" / "binding-generators", capture_output=True, text=True)
        if result.returncode != 0:
            raise RuntimeError(f"probe {run.name} failed with exit {result.returncode}:\n{result.stdout}\n{result.stderr}")

    return classify_probe_outputs(repo) if execute else default_mechanism_decisions()
```

Add `--execute` behavior to `--probe-semantic-abi`: without `--execute`, print commands and write the default report; with `--execute`, run commands and classify outputs. Note in the CLI help that `--execute` requires a provisioned checkout with the ClangSharp local tool and SDL headers under `vcpkg_installed/<triplet>/include/SDL2/`.

- [ ] **Step 3: Classify generated probe output instead of hardcoding decisions**

Add output readers:

```python
def read_probe_output(repo: pathlib.Path, name: str) -> str:
    path = repo / "spikes" / "binding-generators" / "output" / "semantic-abi-probes" / f"{name}.g.cs"
    return path.read_text(encoding="utf-8") if path.is_file() else ""


def probe_contains(repo: pathlib.Path, name: str, text: str) -> bool:
    return text in read_probe_output(repo, name)
```

Then add `classify_probe_outputs`. It must choose decisions from output facts and must use `stop-and-review` for unknown branches rather than silently selecting a fallback:

```python
def classify_probe_outputs(repo: pathlib.Path) -> tuple[MechanismDecision, ...]:
    return (
        classify_config_modes(repo),
        classify_c_long(repo),
        classify_wchar_pointer(repo),
        classify_opaque_handle(repo),
        classify_sdl_rwops(repo),
        classify_sdl_syswm(),
        classify_dynapi_evidence(),
    )
```

Add the config-mode guard first. If `latest-only` and `latest-windows-types` are identical for declarations whose type shape should differ, treat `windows-types`/`unix-types` as unproven and stop before selecting downstream fallbacks:

```python
def classify_config_modes(repo: pathlib.Path) -> MechanismDecision:
    latest_only = read_probe_output(repo, "latest-only")
    latest_windows = read_probe_output(repo, "latest-windows-types")
    latest_unix = read_probe_output(repo, "latest-unix-types")
    mode_changes_output = latest_only != latest_windows or latest_windows != latest_unix
    return MechanismDecision(
        "clangsharp-config-modes",
        "--config latest-codegen with windows-types / unix-types",
        "Type-mode config values changed generated output." if mode_changes_output else "Type-mode config values did not produce distinguishable output; downstream platform conclusions are untrusted.",
        "clangsharp-rsp" if mode_changes_output else "stop-and-review",
        "Use separate explicit ClangSharp invocations once config syntax is proven.",
        "Probe output diff between latest-only, latest-windows-types, and latest-unix-types.",
        "Config mode is ignored or cannot be proven from generated output.",
    )
```

Add the C-long classifier. This classifier records whether ClangSharp solved Modern shape directly and whether Compat still needs a manual strategy prototype:

```python
def classify_c_long(repo: pathlib.Path) -> MechanismDecision:
    remap_works = (
        probe_contains(repo, "long-remap-windows-types", "CLong")
        and probe_contains(repo, "long-remap-unix-types", "CLong")
        and probe_contains(repo, "long-remap-windows-types", "CULong")
        and probe_contains(repo, "long-remap-unix-types", "CULong")
    )
    with_type_works = probe_contains(repo, "with-type-long", "CLong") and probe_contains(repo, "with-type-long", "CULong")
    selected = "clangsharp-rsp" if remap_works or with_type_works else "modern-semantic-rewrite-plus-compat-prototype"
    result = (
        "ClangSharp emitted CLong / CULong through RSP/type options."
        if selected == "clangsharp-rsp"
        else "ClangSharp did not prove exact CLong / CULong output for all C long probes; Modern rewrite and Compat manual-codegen prototype required."
    )
    return MechanismDecision(
        "c-long",
        "--remap long=CLong, --remap unsigned long=CULong, --with-type long=CLong",
        result,
        selected,
        "stop-and-review if the Compat prototype finds struct fields or callbacks that cannot be represented exactly in one assembly.",
        "Probe outputs, postprocess self-tests, occurrence inventory, Core build for Modern and Compat TFMs.",
        "No exact strategy exists for a declaration category after the Compat prototype.",
    )
```

Add wide-character and opaque classifiers. They should inspect pointer-depth cases explicitly, including `const wchar_t**` and callback signatures:

```python
def classify_wchar_pointer(repo: pathlib.Path) -> MechanismDecision:
    unix_output = read_probe_output(repo, "latest-unix-types")
    remap_removed_ushort = "ushort*" not in unix_output and "ushort**" not in unix_output
    const_double_pointer_safe = "JansetProbe_TakeConstWcharOut" in unix_output and ("nint*" in unix_output or "void**" in unix_output)
    selected = "clangsharp-rsp" if remap_removed_ushort and const_double_pointer_safe else "semantic-rewrite"
    return MechanismDecision(
        "wchar-pointer",
        "base.rsp wchar_t*=nint plus const wchar_t** probe",
        "ClangSharp remap covered shared wchar_t pointer-depth." if selected == "clangsharp-rsp" else "ClangSharp output still needs a pointer-depth-preserving wchar_t rewrite.",
        selected,
        "stop-and-review for wchar_t*** or Windows-only UTF-16 surfaces outside the known WinRT path.",
        "Probe checks for wchar_t*, const wchar_t*, wchar_t**, const wchar_t**, fields, returns, parameters, callbacks.",
        "Pointer depth cannot be preserved without breaking Windows-only Unicode APIs.",
    )


def classify_opaque_handle(repo: pathlib.Path) -> MechanismDecision:
    with_attribute = read_probe_output(repo, "with-attribute-opaque")
    transparent = read_probe_output(repo, "transparent-struct")
    exclude_empty = read_probe_output(repo, "exclude-empty-records")
    clangsharp_native = "NativeOpaqueType" in with_attribute and "SDL_semantic_opaque**" not in transparent
    selected = "clangsharp-rsp" if clangsharp_native else "catalog-driven-opaque-rewrite"
    return MechanismDecision(
        "opaque-handle",
        "--with-attribute, --with-transparent-struct, exclude-empty-records",
        "ClangSharp can carry an opaque marker and avoid parser tag leakage." if selected == "clangsharp-rsp" else "ClangSharp does not prove canonical opaque handle output; use catalog-driven pointer rewrite with no final marker attributes.",
        selected,
        "stop-and-review if pointer-depth or delegate signatures cannot be rewritten from the catalog.",
        "Postprocess self-test covering T*, T**, const T*, fields, returns, parameters, and callbacks.",
        "Canonical typedef names or pointer-depth rewrites cannot be proven for every opaque handle context.",
    )
```

Add `SDL_RWops`, SysWM, and dynapi classifiers. `SDL_RWops` must prefer `--exclude SDL_RWops` when the probe proves pointer references still compile:

```python
def classify_sdl_rwops(repo: pathlib.Path) -> MechanismDecision:
    exclude_output = read_probe_output(repo, "exclude-rwops-shape")
    exclude_kept_references = "SDL_RWops*" in exclude_output or "nint" in exclude_output or "void*" in exclude_output
    exclude_removed_layout = "struct SDL_RWops" not in exclude_output and "partial struct SDL_RWops" not in exclude_output
    selected = "clangsharp-exclude" if exclude_kept_references and exclude_removed_layout else "struct-body-quarantine"
    return MechanismDecision(
        "sdl-rwops",
        "--exclude SDL_RWops before synthetic replacement",
        "ClangSharp exclude preserves references without emitting the layout." if selected == "clangsharp-exclude" else "Use struct-body quarantine while preserving functions/constants in SDL_rwops.g.cs.",
        selected,
        "catalog-driven opaque pointer rewrite if struct-body quarantine cannot preserve all reference contexts.",
        "Generated Core build and oracle deferred-layout-sdl-rwops count.",
        "No representation preserves SDL_RWops references without exposing false fields.",
    )


def classify_sdl_syswm() -> MechanismDecision:
    return MechanismDecision(
        "sdl-syswm",
        "SysWM classification plus native layout evidence report",
        "Current host-shaped SysWM layout is not proof; preserve enum/constants and quarantine only unproven layouts until native evidence exists.",
        "layout-quarantine-plus-evidence-gate",
        "synthetic-layout only after real sizeof/alignment/offsetof evidence exists.",
        "SysWM evidence report, generated Core build, oracle SysWM classification.",
        "Native layout proof is unavailable for at least one emitted target view.",
    )


def classify_dynapi_evidence() -> MechanismDecision:
    return MechanismDecision(
        "dynapi-evidence",
        "Oracle report",
        "Dynapi is report-only name evidence and is handled in oracle taxonomy.",
        "oracle-report",
        "stop-and-review only if dynapi source files cannot be found and the report cannot explain missing evidence.",
        "Oracle self-test and regenerated oracle evidence report.",
        "Dynapi export source files are absent from the checkout.",
    )
```

- [ ] **Step 4: Render all C0 decisions, including the config-mode guard**

Update `render_mechanism_report` consumers to accept the extra `clangsharp-config-modes` row. The report must make these outcomes visible:

```text
clangsharp-config-modes
c-long
wchar-pointer
opaque-handle
sdl-rwops
sdl-syswm
dynapi-evidence
```

The report is valid only when `selected_implementation_layer` is not `not-selected` for every non-default row. A `stop-and-review` selected layer is valid evidence, but it blocks the next dependent task until Deniz chooses the branch.

- [ ] **Step 5: Run C0 dry-run and executed probes**

Run dry-run first:

```pwsh
python spikes/binding-generators/clangsharp/generate_bindings.py --probe-semantic-abi
```

Expected: command exits `0`, prints every probe command, and writes the default report without claiming a selected fallback.

Run executed probes:

```pwsh
python spikes/binding-generators/clangsharp/generate_bindings.py --probe-semantic-abi --execute
```

Expected: command exits `0`, probe outputs are created under `spikes/binding-generators/output/semantic-abi-probes/`, and `semantic-abi-capabilities.md` records data-driven results. It must not contain the phrase `ClangSharp does not emit CLong/CULong directly for the probe` unless that exact sentence is derived from inspected probe output.

- [ ] **Step 6: Run generator self-test**

Run:

```pwsh
python spikes/binding-generators/clangsharp/generate_bindings.py --self-test
```

Expected: `self-test: PASS`.

- [ ] **Step 7: Commit checkpoint, approval required**

Before committing, present the changed files and this proposed message to Deniz:

```text
test(binding-spike): record ClangSharp semantic ABI capability decisions
```

After explicit approval, run:

```pwsh
git add spikes/binding-generators/clangsharp/semantic_abi.py spikes/binding-generators/clangsharp/generate_bindings.py spikes/binding-generators/output/semantic-abi-probes spikes/binding-generators/output/reports/semantic-abi-capabilities.md
git commit -m "test(binding-spike): record ClangSharp semantic ABI capability decisions"
```

## Task 4: Quarantine `SDL_RWops` Layout

**Files:**
- Modify: `spikes/binding-generators/clangsharp/semantic_abi.py`
- Modify: `spikes/binding-generators/clangsharp/generate_bindings.py`
- Regenerate: `spikes/binding-generators/clangsharp/src/Janset.SDL2.Core/Generated/**/SDL_rwops.g.cs`
- Regenerate: `spikes/binding-generators/output/reports/oracle-evidence-clangsharp.md`

- [ ] **Step 1: Follow the C0-selected `SDL_RWops` branch**

Open `spikes/binding-generators/output/reports/semantic-abi-capabilities.md` and read the `sdl-rwops` row.

- If the selected layer is `clangsharp-exclude`, update the relevant RSP/generation input so `SDL_RWops` itself is excluded and pointer references still compile. Do not add a synthetic replacement.
- If the selected layer is `struct-body-quarantine`, continue with Step 2.
- If the selected layer is `catalog-driven opaque pointer rewrite`, stop this task and implement the opaque pointer rewrite branch in Task 7 first.
- If the selected layer is `stop-and-review`, ask Deniz which branch to take.

- [ ] **Step 2: Add a struct-body-only quarantine helper**

In `semantic_abi.py`, add a helper that replaces only the `SDL_RWops` struct declaration body inside the existing generated file. It must preserve all other `SDL_rwops.g.cs` content: `SDL_RWFromFile`, `SDL_RWread`, `SDL_RWsize`, `SDL_RWclose`, `RW_SEEK_SET`, `SDL_RWOPS_UNKNOWN`, usings, namespace, and constants.

```python
def quarantine_struct_body(source: str, struct_name: str) -> str:
    marker = f"partial struct {struct_name}"
    start = source.find(marker)
    if start < 0:
        return source

    brace_start = source.find("{", start)
    if brace_start < 0:
        return source

    depth = 0
    for index in range(brace_start, len(source)):
        char = source[index]
        if char == "{":
            depth += 1
        elif char == "}":
            depth -= 1
            if depth == 0:
                return source[: brace_start + 1] + "\n    }" + source[index + 1 :]
    return source
```

Add a self-test in `run_self_tests()` with a fixture containing an `SDL_RWops` struct, one constant, and one extern function. The self-test must prove that the struct body is empty and the constant/function text remains present.

- [ ] **Step 3: Apply struct-body quarantine after ClangSharp generation only when selected**

In `generate_bindings.py`, add:

```python
from semantic_abi import quarantine_struct_body
```

Add:

```python
def quarantine_rwops_layout(repo: pathlib.Path, family: str, codegen: str) -> int:
    if family != "core":
        return 0
    rwops_path = output_path_for_header(repo, codegen, family, "SDL_rwops.h")
    if not rwops_path.is_file():
        return 0
    original = rwops_path.read_text(encoding="utf-8")
    rewritten = quarantine_struct_body(original, "SDL_RWops")
    if rewritten == original:
        return 0
    rwops_path.write_text(rewritten, encoding="utf-8")
    return 1
```

Call this after each codegen pass has generated headers and before `platform-delta`, but only when the C0 `sdl-rwops` selected layer is `struct-body-quarantine`.

- [ ] **Step 3: Tighten oracle self-test for `SDL_RWops`**

In `oracle.cs`, add a fixture for opaque `SDL_RWops`:

```csharp
public const string OpaqueRwopsSource = """
namespace SDL2
{
    public unsafe partial struct SDL_RWops
    {
    }
}
""";
```

Add self-test expectations:

```csharp
var opaqueRwopsEvidence = CSharpEvidenceExtractor.Extract("opaque-rwops.g.cs", Fixtures.OpaqueRwopsSource, ["NET5_0_OR_GREATER"]);
var opaqueRwopsChecks = RawAbiChecks.Run(FamilyConfigs.Sdl2Core, opaqueRwopsEvidence, RequiredSurface.Empty);
Expect(!opaqueRwopsChecks.Any(c => c.CheckId == "deferred-layout-sdl-rwops"), "does not flag empty SDL_RWops opaque stub", failures);
```

- [ ] **Step 4: Regenerate Core output**

Run:

```pwsh
python spikes/binding-generators/clangsharp/generate_bindings.py --scope full --codegen both --family core --execute --clean-output --vcpkg-triplet x64-windows-hybrid --use-platform-header-shims
```

Expected: command exits `0`. `Generated/Compat/SDL_rwops.g.cs` and `Generated/Modern/SDL_rwops.g.cs` contain an empty `partial struct SDL_RWops` with no fields, and still contain the non-layout `SDL_rwops.h` surface such as constants and `SDL_RWFromFile` / `SDL_RWread` / `SDL_RWsize` declarations.

- [ ] **Step 5: Run verification**

Run:

```pwsh
python spikes/binding-generators/clangsharp/generate_bindings.py --self-test
dotnet run --file spikes/binding-generators/clangsharp/oracle.cs -- --self-test
dotnet run --file spikes/binding-generators/clangsharp/oracle.cs -- --family sdl2-core --family sdl2-image --write-report
dotnet build spikes/binding-generators/clangsharp/src/Janset.SDL2.Core/Janset.SDL2.Core.csproj -c Release
```

Expected:

```text
self-test: PASS
self-test: PASS
Build succeeded.
```

The oracle report must no longer include `deferred-layout-sdl-rwops` for `SDL_RWops`.

- [ ] **Step 6: Commit checkpoint, approval required**

Before committing, present the changed files and this proposed message to Deniz:

```text
fix(binding-spike): quarantine SDL_RWops layout
```

After explicit approval, run:

```pwsh
git add spikes/binding-generators/clangsharp/semantic_abi.py spikes/binding-generators/clangsharp/generate_bindings.py spikes/binding-generators/clangsharp/oracle.cs spikes/binding-generators/clangsharp/src/Janset.SDL2.Core/Generated spikes/binding-generators/output/reports/oracle-evidence-clangsharp.md spikes/binding-generators/output/reports/clangsharp-full.md
git commit -m "fix(binding-spike): quarantine SDL_RWops layout"
```

## Task 5: Research And Correct C `long` / `unsigned long` Mapping

**Files:**
- Modify: `spikes/binding-generators/clangsharp/postprocess/Program.cs`
- Create: `spikes/binding-generators/clangsharp/postprocess/NativeTypeRewriteRewriter.cs`
- Create: `spikes/binding-generators/clangsharp/postprocess/PostProcessSelfTests.cs`
- Modify: `spikes/binding-generators/clangsharp/semantic_abi.py`
- Modify: `spikes/binding-generators/clangsharp/generate_bindings.py`
- Regenerate: Core generated output and oracle report

- [ ] **Step 1: Add native type rewrite and Compat prototype modes**

Update `Program.cs` usage and mode list to include `native-type-rewrite`, `compat-long-prototype`, and `self-test`:

```csharp
//   dotnet run --project postprocess -- native-type-rewrite <input-dir> [<output-dir>] [--codegen modern|compat]
//   dotnet run --project postprocess -- compat-long-prototype <input-dir> <report-path>
//   dotnet run --project postprocess -- self-test
```

Change the argument guard to:

```csharp
if (args.Length == 1 && args[0] == "self-test")
{
    return PostProcessSelfTests.Run();
}

if (args.Length < 2 || args[0] is not ("strip-varargs" or "libraryimport" or "platform-delta" or "native-type-rewrite" or "compat-long-prototype"))
{
    Console.Error.WriteLine("usage: dotnet run --project postprocess -- <strip-varargs|libraryimport|platform-delta|native-type-rewrite|compat-long-prototype> <input-dir> [<output-dir>]");
    Console.Error.WriteLine("usage: dotnet run --project postprocess -- self-test");
    return 1;
}
```

`compat-long-prototype` must scan the generated Compat files and write an inventory report grouped by declaration category:

```text
return
parameter
field
callback-or-delegate
typedef-alias
```

This report decides whether exact manual Compat codegen is possible. Function returns/parameters can be prototyped with Windows and Unix private externs that share the native `EntryPoint` and a non-extern wrapper. Struct fields and callback/delegate signatures require separate proof; if any are found and no exact one-assembly representation is proven, stop and ask Deniz rather than accepting a fake downlevel signature.

- [ ] **Step 2: Implement `NativeTypeRewriteRewriter` for Modern C long**

Create `spikes/binding-generators/clangsharp/postprocess/NativeTypeRewriteRewriter.cs` with a Roslyn rewriter that replaces types only when the same node has source-time native evidence:

```csharp
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Janset.SDL2.PostProcess;

internal sealed class NativeTypeRewriteRewriter(string relativePath = "") : CSharpSyntaxRewriter
{
    public bool AnyChanges { get; private set; }

    public void Reset()
    {
        AnyChanges = false;
        _needsInteropUsing = false;
    }

    public override SyntaxNode? VisitMethodDeclaration(MethodDeclarationSyntax node)
    {
        var visited = (MethodDeclarationSyntax)base.VisitMethodDeclaration(node)!;
        var returnNativeType = FindReturnNativeTypeName(visited);
        if (returnNativeType is "long")
        {
            visited = visited.WithReturnType(SyntaxFactory.IdentifierName("CLong").WithTriviaFrom(visited.ReturnType));
            AnyChanges = true;
        }
        else if (returnNativeType is "unsigned long")
        {
            visited = visited.WithReturnType(SyntaxFactory.IdentifierName("CULong").WithTriviaFrom(visited.ReturnType));
            AnyChanges = true;
        }

        return visited;
    }

    public override SyntaxNode? VisitParameter(ParameterSyntax node)
    {
        var visited = (ParameterSyntax)base.VisitParameter(node)!;
        var nativeType = FindNativeTypeName(visited.AttributeLists);
        if (visited.Type is null)
        {
            return visited;
        }

        if (nativeType is "long")
        {
            AnyChanges = true;
            return visited.WithType(SyntaxFactory.IdentifierName("CLong").WithTriviaFrom(visited.Type));
        }

        if (nativeType is "unsigned long")
        {
            AnyChanges = true;
            return visited.WithType(SyntaxFactory.IdentifierName("CULong").WithTriviaFrom(visited.Type));
        }

        return visited;
    }

    public override SyntaxNode? VisitFieldDeclaration(FieldDeclarationSyntax node)
    {
        var visited = (FieldDeclarationSyntax)base.VisitFieldDeclaration(node)!;
        var nativeType = FindNativeTypeName(visited.AttributeLists);
        if (nativeType is not ("long" or "unsigned long"))
        {
            return visited;
        }

        var replacement = nativeType == "long" ? "CLong" : "CULong";
        _needsInteropUsing = true;
        AnyChanges = true;
        return visited.WithDeclaration(
            visited.Declaration.WithType(SyntaxFactory.IdentifierName(replacement).WithTriviaFrom(visited.Declaration.Type)));
    }

    private static string? FindReturnNativeTypeName(MethodDeclarationSyntax method)
    {
        foreach (var list in method.AttributeLists.Where(list => list.Target?.Identifier.Text == "return"))
        {
            var name = FindNativeTypeName(list.Attributes);
            if (name is not null)
            {
                return name;
            }
        }

        return null;
    }

    private static string? FindNativeTypeName(SyntaxList<AttributeListSyntax> attributeLists)
        => FindNativeTypeName(attributeLists.SelectMany(list => list.Attributes));

    private static string? FindNativeTypeName(IEnumerable<AttributeSyntax> attributes)
    {
        foreach (var attribute in attributes)
        {
            var name = attribute.Name.ToString();
            if (name is not "NativeTypeName" and not "NativeTypeNameAttribute")
            {
                continue;
            }

            var expression = attribute.ArgumentList?.Arguments.FirstOrDefault()?.Expression;
            if (expression is LiteralExpressionSyntax literal && literal.Token.ValueText.Length > 0)
            {
                return literal.Token.ValueText;
            }
        }

        return null;
    }
}
```

- [ ] **Step 3: Add `System.Runtime.InteropServices` using guard**

The generated files already use `System.Runtime.InteropServices`, but the rewriter should be safe for fixtures without that using. Add this helper to `NativeTypeRewriteRewriter` and call it from `VisitCompilationUnit` when the rewriter emits `CLong` or `CULong`:

```csharp
private bool _needsInteropUsing;

public override SyntaxNode? VisitCompilationUnit(CompilationUnitSyntax node)
{
    var result = (CompilationUnitSyntax)base.VisitCompilationUnit(node)!;
    if (!_needsInteropUsing || result.Usings.Any(u => u.Name?.ToString() == "System.Runtime.InteropServices"))
    {
        return result;
    }

    var newUsing = SyntaxFactory
        .UsingDirective(SyntaxFactory.ParseName("System.Runtime.InteropServices"))
        .NormalizeWhitespace()
        .WithTrailingTrivia(SyntaxFactory.LineFeed);
    return result.AddUsings(newUsing);
}
```

Set `_needsInteropUsing = true` in the branches that emit `CLong` and `CULong`.

- [ ] **Step 4: Run the Compat C-long strategy prototype**

Before wiring any rewrite into Compat output, run the prototype against the current generated Compat directory:

```pwsh
dotnet run --project spikes/binding-generators/clangsharp/postprocess/Janset.SDL2.PostProcess.csproj -c Release -- compat-long-prototype spikes/binding-generators/clangsharp/src/Janset.SDL2.Core/Generated/Compat spikes/binding-generators/output/reports/compat-c-long-prototype.md
```

Expected: the report lists every C `long` / `unsigned long` occurrence by category. Continue only when one of these branches is true:

```text
Branch A: no Compat C-long occurrences remain after ClangSharp/RSP changes.
Branch B: occurrences are functions only, and exact manual Windows/Unix private extern + wrapper generation is implemented and self-tested.
Branch C: fields, callbacks, delegates, or typedef aliases exist, and an exact one-assembly representation is proven with generated compile tests.
Branch D: fields, callbacks, delegates, or typedef aliases exist, no exact representation is proven, and the task stops for Deniz decision.
```

Do not write `CLong` / `CULong` into `Generated/Compat` unless the project no longer targets `netstandard2.0` / `net462`, which is outside this plan. Do not add a fake downlevel `CLong` / `CULong` polyfill.

- [ ] **Step 5: Wire postprocess mode into `generate_bindings.py`**

After `platform-delta` and `strip-varargs`, and before `libraryimport`, run `native-type-rewrite` for Modern output. Run it for Compat only if the C0 row and the Compat prototype report selected an exact manual Compat strategy:

```python
    if args.execute:
        print("--- postprocess: native-type-rewrite (semantic ABI) ---")
        for codegen in codegen_passes:
            for family in selected:
                if codegen == "compat" and not compat_c_long_strategy_is_proven(repo, family):
                    print(f"  skipped native-type-rewrite for {family}/{codegen}: Compat C long strategy not proven")
                    continue
                exit_code = run_postprocess(repo, family, spike_root, "native-type-rewrite", codegen)
                if exit_code != 0:
                    postprocess_failures += 1
                    print(f"WARNING: native-type-rewrite postprocess for {family}/{codegen} returned exit {exit_code}")
```

Add `compat_c_long_strategy_is_proven(repo, family)` beside the postprocess helpers. It returns `True` only when `compat-c-long-prototype.md` proves that every generated Compat C-long occurrence is either absent or covered by exact manual Compat codegen. If it returns `False` while the prototype report contains any Compat C-long occurrence, stop the task before regeneration verification and ask Deniz which strategy to implement.

- [ ] **Step 6: Create postprocess self-test fixture**

Create `spikes/binding-generators/clangsharp/postprocess/PostProcessSelfTests.cs` with this initial harness:

```csharp
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Janset.SDL2.PostProcess;

internal static class PostProcessSelfTests
{
    public static int Run()
    {
        var failures = new List<string>();
        var nativeTypeFixture = """
using System.Runtime.InteropServices;

namespace SDL2;

internal static unsafe partial class SDLNative
{
    [return: NativeTypeName("long")]
    public static extern int SDL_strtol(byte* text, byte** endp, int radix);

    public static extern byte* SDL_ltoa([NativeTypeName("long")] int value, byte* text, int radix);

    [return: NativeTypeName("unsigned long")]
    public static extern uint SDL_strtoul(byte* text, byte** endp, int radix);
}

public struct LongFieldCarrier
{
    [NativeTypeName("long")]
    public int long_field;

    [NativeTypeName("unsigned long")]
    public uint unsigned_long_field;
}
""";

        var rewritten = RewriteNativeTypes(nativeTypeFixture, "SDL_stdinc.g.cs");
        Expect(rewritten.Contains("public static extern CLong SDL_strtol", StringComparison.Ordinal), "rewrites long return to CLong", failures);
        Expect(rewritten.Contains("[NativeTypeName(\"long\")] CLong value", StringComparison.Ordinal), "rewrites long parameter to CLong", failures);
        Expect(rewritten.Contains("public static extern CULong SDL_strtoul", StringComparison.Ordinal), "rewrites unsigned long return to CULong", failures);
        Expect(rewritten.Contains("public CLong long_field", StringComparison.Ordinal), "rewrites long fields to CLong", failures);
        Expect(rewritten.Contains("public CULong unsigned_long_field", StringComparison.Ordinal), "rewrites unsigned long fields to CULong", failures);

        if (failures.Count == 0)
        {
            Console.WriteLine("postprocess self-test: PASS");
            return 0;
        }

        Console.Error.WriteLine("postprocess self-test: FAIL");
        foreach (var failure in failures)
        {
            Console.Error.WriteLine("- " + failure);
        }
        return 1;
    }

    private static string RewriteNativeTypes(string source, string relativePath)
    {
        var tree = CSharpSyntaxTree.ParseText(source);
        var root = tree.GetCompilationUnitRoot();
        var rewriter = new NativeTypeRewriteRewriter(relativePath);
        return ((CompilationUnitSyntax)rewriter.Visit(root)!).ToFullString();
    }

    private static void Expect(bool condition, string message, List<string> failures)
    {
        if (!condition)
        {
            failures.Add(message);
        }
    }
}
```

This fixture contains the three C `long` declarations the self-test must rewrite:

```csharp
[return: NativeTypeName("long")]
public static extern int SDL_strtol(byte* text, byte** endp, int radix);

public static extern byte* SDL_ltoa([NativeTypeName("long")] int value, byte* text, int radix);

[return: NativeTypeName("unsigned long")]
public static extern uint SDL_strtoul(byte* text, byte** endp, int radix);
```

Add a field fixture as well so C `long` fields do not silently remain platform-wrong in Modern output:

```csharp
public struct LongFieldCarrier
{
    [NativeTypeName("long")]
    public int long_field;

    [NativeTypeName("unsigned long")]
    public uint unsigned_long_field;
}
```

Expected rewritten snippets:

```csharp
public static extern CLong SDL_strtol(byte* text, byte** endp, int radix);
public static extern byte* SDL_ltoa([NativeTypeName("long")] CLong value, byte* text, int radix);
public static extern CULong SDL_strtoul(byte* text, byte** endp, int radix);
public CLong long_field;
public CULong unsigned_long_field;
```

- [ ] **Step 7: Run postprocess self-test**

Run:

```pwsh
dotnet run --project spikes/binding-generators/clangsharp/postprocess/Janset.SDL2.PostProcess.csproj -c Release -- self-test
```

Expected: `postprocess self-test: PASS`.

- [ ] **Step 8: Regenerate and verify**

Run:

```pwsh
python spikes/binding-generators/clangsharp/generate_bindings.py --scope full --codegen both --family core --execute --clean-output --vcpkg-triplet x64-windows-hybrid --use-platform-header-shims
dotnet run --file spikes/binding-generators/clangsharp/oracle.cs -- --self-test
dotnet run --file spikes/binding-generators/clangsharp/oracle.cs -- --family sdl2-core --family sdl2-image --write-report
dotnet build spikes/binding-generators/clangsharp/src/Janset.SDL2.Core/Janset.SDL2.Core.csproj -c Release
```

Expected: postprocess succeeds, build succeeds, and oracle no longer reports `platform-sensitive-long` for Modern rewritten functions/fields. Compat is accepted only if `compat-c-long-prototype.md` proves an exact strategy for every Compat C-long declaration category found in the generated output. If the prototype reaches Branch D, stop the implementation and ask Deniz; do not merge a fake Compat mapping and do not hide the oracle finding.

- [ ] **Step 9: Commit checkpoint, approval required**

Before committing, present the changed files and this proposed message to Deniz:

```text
fix(binding-spike): map C long through semantic rewrite
```

After explicit approval, run:

```pwsh
git add spikes/binding-generators/clangsharp/postprocess spikes/binding-generators/clangsharp/generate_bindings.py spikes/binding-generators/clangsharp/src/Janset.SDL2.Core/Generated spikes/binding-generators/output/reports
git commit -m "fix(binding-spike): map C long through semantic rewrite"
```

## Task 6: Correct Shared `wchar_t*` Mapping

**Files:**
- Modify: `spikes/binding-generators/clangsharp/postprocess/NativeTypeRewriteRewriter.cs`
- Modify: `spikes/binding-generators/clangsharp/postprocess/PostProcessSelfTests.cs`
- Regenerate: Core generated output and oracle report

- [ ] **Step 1: Extend native type rewrite for shared `wchar_t*` with pointer-depth preservation**

In `NativeTypeRewriteRewriter`, add this predicate:

```csharp
private static bool IsSharedWcharPointer(string? nativeType)
    => nativeType is not null
        && nativeType.Contains("wchar_t", StringComparison.Ordinal)
        && nativeType.Contains("*", StringComparison.Ordinal);
```

Add pointer-depth helpers. They preserve ABI indirection instead of collapsing every `wchar_t*` spelling to `nint`:

```csharp
private static int CountNativePointerDepth(string nativeType)
{
    var count = 0;
    foreach (var character in nativeType)
    {
        if (character == '*')
        {
            count++;
        }
    }
    return count;
}


private static TypeSyntax CreateOpaquePointerType(string nativeType, TypeSyntax originalType)
{
    var depth = CountNativePointerDepth(nativeType);
    TypeSyntax rewritten = SyntaxFactory.IdentifierName("nint").WithTriviaFrom(originalType);
    for (var index = 1; index < depth; index++)
    {
        rewritten = SyntaxFactory.PointerType(rewritten);
    }
    return rewritten;
}
```

In return and parameter rewrite paths, when `IsSharedWcharPointer(nativeType)` is true, replace the managed type using `CreateOpaquePointerType`. This gives `wchar_t*` / `const wchar_t*` -> `nint`, and `wchar_t**` / `const wchar_t**` -> `nint*`.

For returns:

```csharp
if (IsSharedWcharPointer(returnNativeType))
{
    visited = visited.WithReturnType(CreateOpaquePointerType(returnNativeType, visited.ReturnType));
    AnyChanges = true;
}
```

For parameters:

```csharp
if (IsSharedWcharPointer(nativeType))
{
    AnyChanges = true;
    return visited.WithType(CreateOpaquePointerType(nativeType, visited.Type));
}
```

If `CountNativePointerDepth(nativeType) > 2`, do not rewrite the declaration in this slice. Add an oracle/report entry explaining the deeper pointer case and stop for review if it appears in generated shared SDL output.

- [ ] **Step 2: Add field rewrite for HIDAPI wide-string fields**

Extend the existing `VisitFieldDeclaration` override so single-variable fields with `[NativeTypeName("wchar_t *")] ushort*` become `nint` and `[NativeTypeName("wchar_t **")] ushort**` becomes `nint*`:

```csharp
public override SyntaxNode? VisitFieldDeclaration(FieldDeclarationSyntax node)
{
    var visited = (FieldDeclarationSyntax)base.VisitFieldDeclaration(node)!;
    var nativeType = FindNativeTypeName(visited.AttributeLists);
    if (!IsSharedWcharPointer(nativeType))
    {
        return visited;
    }

    if (CountNativePointerDepth(nativeType) > 2)
    {
        return visited;
    }

    AnyChanges = true;
    return visited.WithDeclaration(
        visited.Declaration.WithType(CreateOpaquePointerType(nativeType, visited.Declaration.Type)));
}
```

- [ ] **Step 3: Add Windows-only exception guard**

Add a helper that skips files under `Platforms/WinRT/SDL_system.g.cs` until a Windows-only UTF-16 helper policy is explicitly designed. The postprocess file walker must pass each file's relative path into `NativeTypeRewriteRewriter`; without `relativePath`, this guard is not considered implemented.

```csharp
internal sealed class NativeTypeRewriteRewriter(string relativePath) : CSharpSyntaxRewriter
{
    private bool IsWindowsOnlyUnicodeFile => relativePath.Replace('\\', '/').Contains("Platforms/WinRT/SDL_system.g.cs", StringComparison.Ordinal);
}
```

If `IsWindowsOnlyUnicodeFile` is true, do not rewrite `wchar_t*` nodes in that file.

- [ ] **Step 4: Add postprocess self-test fixture**

Add a fixture with:

```csharp
[NativeTypeName("wchar_t *")]
public ushort* serial_number;

[return: NativeTypeName("wchar_t *")]
public static extern ushort* SDL_wcsdup([NativeTypeName("const wchar_t *")] ushort* wstr);

public static extern int SDL_hid_get_serial_number_string(nint dev, [NativeTypeName("wchar_t *")] ushort* @string, nuint maxlen);

public static extern void JansetProbe_TakeConstWcharOut([NativeTypeName("const wchar_t **")] ushort** value);
```

Expected rewritten snippets:

```csharp
[NativeTypeName("wchar_t *")]
public nint serial_number;

[return: NativeTypeName("wchar_t *")]
public static extern nint SDL_wcsdup([NativeTypeName("const wchar_t *")] nint wstr);

public static extern int SDL_hid_get_serial_number_string(nint dev, [NativeTypeName("wchar_t *")] nint @string, nuint maxlen);

public static extern void JansetProbe_TakeConstWcharOut([NativeTypeName("const wchar_t **")] nint* value);
```

Also add a negative self-test for `Platforms/WinRT/SDL_system.g.cs` proving that a Windows-only UTF-16 file is not rewritten by the shared wchar rule.

- [ ] **Step 5: Run postprocess self-test**

Run:

```pwsh
dotnet run --project spikes/binding-generators/clangsharp/postprocess/Janset.SDL2.PostProcess.csproj -c Release -- self-test
```

Expected: `postprocess self-test: PASS`.

- [ ] **Step 6: Regenerate and verify**

Run:

```pwsh
python spikes/binding-generators/clangsharp/generate_bindings.py --scope full --codegen both --family core --execute --clean-output --vcpkg-triplet x64-windows-hybrid --use-platform-header-shims
dotnet run --file spikes/binding-generators/clangsharp/oracle.cs -- --self-test
dotnet run --file spikes/binding-generators/clangsharp/oracle.cs -- --family sdl2-core --family sdl2-image --write-report
dotnet build spikes/binding-generators/clangsharp/src/Janset.SDL2.Core/Janset.SDL2.Core.csproj -c Release
```

Expected: build succeeds and oracle no longer reports `platform-sensitive-wchar` for shared stdinc/HIDAPI surfaces. If WinRT still reports `wchar_t*`, the report must classify it as Windows-only rather than shared hazard.

- [ ] **Step 7: Commit checkpoint, approval required**

Before committing, present the changed files and this proposed message to Deniz:

```text
fix(binding-spike): treat shared wchar pointers as opaque
```

After explicit approval, run:

```pwsh
git add spikes/binding-generators/clangsharp/postprocess spikes/binding-generators/clangsharp/src/Janset.SDL2.Core/Generated spikes/binding-generators/output/reports
git commit -m "fix(binding-spike): treat shared wchar pointers as opaque"
```

## Task 7: Add Opaque Handle Catalog And Catalog-Driven Rewrite Fallback

**Files:**
- Modify: `spikes/binding-generators/clangsharp/semantic_abi.py`
- Create: `spikes/binding-generators/clangsharp/postprocess/OpaqueHandleRewriteRewriter.cs`
- Modify: `spikes/binding-generators/clangsharp/postprocess/Program.cs`
- Modify: `spikes/binding-generators/clangsharp/postprocess/PostProcessSelfTests.cs`
- Modify: `spikes/binding-generators/clangsharp/generate_bindings.py`
- Regenerate: Core generated output and oracle report

- [ ] **Step 1: Add opaque handle catalog**

In `semantic_abi.py`, add:

```python
OPAQUE_HANDLES: dict[str, str] = {
    "SDL_Window": "SDL_Window",
    "SDL_Renderer": "SDL_Renderer",
    "SDL_Texture": "SDL_Texture",
    "SDL_AudioStream": "SDL_AudioStream",
    "SDL_Cursor": "SDL_Cursor",
    "SDL_Joystick": "SDL_Joystick",
    "SDL_GameController": "SDL_GameController",
    # ClangSharp parser tag -> canonical SDL typedef name.
    "SDL_hid_device_": "SDL_hid_device",
    "SDL_semaphore": "SDL_sem",
    "SDL_mutex": "SDL_mutex",
    "SDL_cond": "SDL_cond",
    "SDL_Thread": "SDL_Thread",
}
```

Add a JSON writer for the C# postprocess tool. Do not make the C# tool parse Python source:

```python
def write_opaque_handle_catalog(repo: pathlib.Path) -> pathlib.Path:
    import json

    catalog_path = repo / "spikes" / "binding-generators" / "output" / "semantic-abi-opaque-handles.json"
    catalog_path.parent.mkdir(parents=True, exist_ok=True)
    entries = [
        {"nativeTagName": native_tag, "canonicalName": canonical}
        for native_tag, canonical in sorted(OPAQUE_HANDLES.items())
    ]
    catalog_path.write_text(json.dumps(entries, indent=2) + "\n", encoding="utf-8")
    return catalog_path
```

- [ ] **Step 2: Implement catalog-driven opaque pointer rewrite mode**

Create `OpaqueHandleRewriteRewriter.cs`. It must not inject `[NativeOpaqueType]` attributes. It receives native tag names from the JSON catalog and replaces pointer usages of catalog-known native tags with `nint` while preserving pointer depth, so `SDL_semaphore*` becomes `nint` and `SDL_semaphore**` becomes `nint*`:

```csharp
internal sealed class OpaquePointerShapeRewriter(IReadOnlySet<string> nativeTagNames) : CSharpSyntaxRewriter
{
    public bool AnyChanges { get; private set; }

    public override SyntaxNode? VisitPointerType(PointerTypeSyntax node)
    {
        var depth = CountPointerDepth(node, out var elementType);
        if (elementType is IdentifierNameSyntax identifier && nativeTagNames.Contains(identifier.Identifier.Text))
        {
            AnyChanges = true;
            TypeSyntax rewritten = SyntaxFactory.IdentifierName("nint").WithTriviaFrom(elementType);
            for (var i = 1; i < depth; i++)
            {
                rewritten = SyntaxFactory.PointerType(rewritten);
            }
            return rewritten.WithTriviaFrom(node);
        }

        return base.VisitPointerType(node);
    }

    private static int CountPointerDepth(PointerTypeSyntax pointer, out TypeSyntax elementType)
    {
        var depth = 1;
        elementType = pointer.ElementType;
        while (elementType is PointerTypeSyntax nested)
        {
            depth++;
            elementType = nested.ElementType;
        }
        return depth;
    }
}
```

Add a second rewriter in the same file to remove empty parser-tag struct declarations after their pointer usages have been rewritten:

```csharp
internal sealed class OpaqueParserTagDeclarationRewriter(IReadOnlySet<string> nativeTagNames) : CSharpSyntaxRewriter
{
    public bool AnyChanges { get; private set; }

    public override SyntaxNode? VisitStructDeclaration(StructDeclarationSyntax node)
    {
        if (!nativeTagNames.Contains(node.Identifier.Text))
        {
            return base.VisitStructDeclaration(node);
        }

        if (node.Members.Count != 0)
        {
            return base.VisitStructDeclaration(node);
        }

        AnyChanges = true;
        return null;
    }
}
```

If a catalog-known parser-tag struct has members, do not remove it in this task. Report it as an opaque-handle blocker because it is no longer an opaque empty parser tag.

- [ ] **Step 3: Add final-output guardrail and delegate coverage self-test**

Add a verifier function in the postprocess self-tests that fails if rewritten output contains:

```text
NativeOpaqueTypeAttribute
[NativeOpaqueType
public partial struct SDL_semaphore
public partial struct SDL_hid_device_
```

The final source must not require a marker attribute at runtime. Add a fixture that includes:

```csharp
public unsafe partial struct SDL_semaphore
{
}

public unsafe delegate void JansetProbe_OpaqueCallback(SDL_semaphore* value, SDL_semaphore** output);

public unsafe partial struct OpaqueFieldCarrier
{
    public SDL_semaphore* input;
    public SDL_semaphore** output;
    public JansetProbe_OpaqueCallback callback;
}
```

Expected rewritten snippets:

```csharp
public unsafe delegate void JansetProbe_OpaqueCallback(nint value, nint* output);
public nint input;
public nint* output;
public JansetProbe_OpaqueCallback callback;
```

Expected absent text:

```text
partial struct SDL_semaphore
NativeOpaqueType
```

- [ ] **Step 4: Wire opaque rewrite into postprocess**

Add `opaque-handles` mode to `Program.cs`. Its usage must accept a catalog path:

```csharp
// dotnet run --project postprocess -- opaque-handles <input-dir> [<output-dir>] --catalog <catalog-json>
```

In `generate_bindings.py`, call `write_opaque_handle_catalog(repo)` before postprocess and pass the resulting path to the C# tool. Run `opaque-handles` after `native-type-rewrite`, before `libraryimport`:

```python
    if args.execute:
        print("--- postprocess: opaque-handles (selected codegens) ---")
        catalog_path = write_opaque_handle_catalog(repo)
        for codegen in codegen_passes:
            for family in selected:
                exit_code = run_postprocess(repo, family, spike_root, "opaque-handles", codegen, extra_args=("--catalog", str(catalog_path)))
                if exit_code != 0:
                    postprocess_failures += 1
                    print(f"WARNING: opaque-handles postprocess for {family}/{codegen} returned exit {exit_code}")
```

- [ ] **Step 5: Run postprocess self-test**

Run:

```pwsh
dotnet run --project spikes/binding-generators/clangsharp/postprocess/Janset.SDL2.PostProcess.csproj -c Release -- self-test
```

Expected: `postprocess self-test: PASS`.

- [ ] **Step 6: Regenerate and verify**

Run:

```pwsh
python spikes/binding-generators/clangsharp/generate_bindings.py --scope full --codegen both --family core --execute --clean-output --vcpkg-triplet x64-windows-hybrid --use-platform-header-shims
dotnet run --file spikes/binding-generators/clangsharp/oracle.cs -- --family sdl2-core --family sdl2-image --write-report
dotnet build spikes/binding-generators/clangsharp/src/Janset.SDL2.Core/Janset.SDL2.Core.csproj -c Release
```

Expected: parser-private handle names do not appear as selected public concepts in final generated output. Raw ABI still compiles.

- [ ] **Step 7: Commit checkpoint, approval required**

Before committing, present the changed files and this proposed message to Deniz:

```text
fix(binding-spike): canonicalize opaque handle shapes
```

After explicit approval, run:

```pwsh
git add spikes/binding-generators/clangsharp/semantic_abi.py spikes/binding-generators/clangsharp/postprocess spikes/binding-generators/clangsharp/generate_bindings.py spikes/binding-generators/clangsharp/src/Janset.SDL2.Core/Generated spikes/binding-generators/output/reports
git commit -m "fix(binding-spike): canonicalize opaque handle shapes"
```

## Task 8: Classify SysWM Current Output And Prevent Host-Shaped False Success

**Files:**
- Modify: `spikes/binding-generators/clangsharp/semantic_abi.py`
- Modify: `spikes/binding-generators/clangsharp/generate_bindings.py`
- Modify: `spikes/binding-generators/clangsharp/oracle.cs`
- Regenerate: Core generated output and oracle report

- [ ] **Step 1: Add SysWM catalog**

In `semantic_abi.py`, add:

```python
SYSWM_ENUM_VALUES: tuple[str, ...] = (
    "SDL_SYSWM_UNKNOWN",
    "SDL_SYSWM_WINDOWS",
    "SDL_SYSWM_X11",
    "SDL_SYSWM_DIRECTFB",
    "SDL_SYSWM_COCOA",
    "SDL_SYSWM_UIKIT",
    "SDL_SYSWM_WAYLAND",
    "SDL_SYSWM_MIR",
    "SDL_SYSWM_WINRT",
    "SDL_SYSWM_ANDROID",
    "SDL_SYSWM_VIVANTE",
    "SDL_SYSWM_OS2",
    "SDL_SYSWM_HAIKU",
    "SDL_SYSWM_KMSDRM",
    "SDL_SYSWM_RISCOS",
)

SYSWM_TARGET_VIEWS: tuple[str, ...] = ("WindowsDesktop", "Linux", "MacOS")
SYSWM_NON_TARGET_VIEWS: tuple[str, ...] = ("WinRT", "GDK", "IOS", "Android")
SYSWM_PRESERVED_NON_LAYOUT_SYMBOLS: tuple[str, ...] = ("SDL_SYSWM_TYPE", "SDL_METALVIEW_TAG")
```

- [ ] **Step 2: Quarantine only ClangSharp host-shaped SysWM layout bodies**

Do not overwrite the whole `SDL_syswm.g.cs` file. Use the same `quarantine_struct_body` helper shape from Task 4 to empty only `SDL_SysWMmsg` and `SDL_SysWMinfo` bodies inside the existing generated file. This preserves enum/constants and non-layout symbols such as `SDL_SYSWM_TYPE` and `SDL_METALVIEW_TAG`.

Add `quarantine_syswm_false_success_outputs(repo, family, codegen)` in `generate_bindings.py`:

```python
def quarantine_syswm_false_success_outputs(repo: pathlib.Path, family: str, codegen: str) -> int:
    if family != "core":
        return 0
    syswm_path = output_path_for_header(repo, codegen, family, "SDL_syswm.h")
    if not syswm_path.is_file():
        return 0
    original = syswm_path.read_text(encoding="utf-8")
    rewritten = quarantine_struct_body(original, "SDL_SysWMmsg")
    rewritten = quarantine_struct_body(rewritten, "SDL_SysWMinfo")
    if rewritten == original:
        return 0
    for symbol in SYSWM_PRESERVED_NON_LAYOUT_SYMBOLS:
        if symbol not in rewritten and symbol in original:
            raise RuntimeError(f"SysWM quarantine removed non-layout symbol: {symbol}")
    syswm_path.write_text(rewritten, encoding="utf-8")
    return 1
```

Call it with the deferred-layout replacement step. The implementation must not delete `SDL_SYSWM_TYPE`, enum members, constants, or non-layout declarations while layout proof is missing.

- [ ] **Step 3: Add oracle classification**

In `oracle.cs`, keep `deferred-layout-sdl-syswminfo` and `deferred-layout-sdl-syswmmsg` as hard bugs when fields are present. Add a separate informational/report-only section in Markdown named `SysWM View Classification` with:

```text
WindowsDesktop: target view, layout proof required
Linux: target view, layout proof required
MacOS: target view, layout proof required
WinRT: non-target/special view, classified only
GDK: non-target/special view, classified only
IOS: non-target/special view, classified only
Android: non-target/special view, classified only
```

- [ ] **Step 4: Regenerate and verify**

Run:

```pwsh
python spikes/binding-generators/clangsharp/generate_bindings.py --scope full --codegen both --family core --execute --clean-output --vcpkg-triplet x64-windows-hybrid --use-platform-header-shims
dotnet run --file spikes/binding-generators/clangsharp/oracle.cs -- --self-test
dotnet run --file spikes/binding-generators/clangsharp/oracle.cs -- --family sdl2-core --family sdl2-image --write-report
dotnet build spikes/binding-generators/clangsharp/src/Janset.SDL2.Core/Janset.SDL2.Core.csproj -c Release
```

Expected: build succeeds. The oracle report no longer presents host-shaped SysWM fields as proven layout; it either reports empty stubs or explicit deferred/proof-required entries.

- [ ] **Step 5: Commit checkpoint, approval required**

Before committing, present the changed files and this proposed message to Deniz:

```text
fix(binding-spike): classify SysWM layout proof boundary
```

After explicit approval, run:

```pwsh
git add spikes/binding-generators/clangsharp/semantic_abi.py spikes/binding-generators/clangsharp/generate_bindings.py spikes/binding-generators/clangsharp/oracle.cs spikes/binding-generators/clangsharp/src/Janset.SDL2.Core/Generated spikes/binding-generators/output/reports
git commit -m "fix(binding-spike): classify SysWM layout proof boundary"
```

## Task 9: Add SysWM Native Layout Probe Infrastructure

**Files:**
- Create: `spikes/binding-generators/clangsharp/probes/syswm_layout_probe.c`
- Modify: `spikes/binding-generators/clangsharp/semantic_abi.py`
- Modify: `spikes/binding-generators/clangsharp/generate_bindings.py`
- Regenerate: `spikes/binding-generators/output/reports/syswm-layout-evidence.md`

- [ ] **Step 1: Create native probe source**

Create `spikes/binding-generators/clangsharp/probes/syswm_layout_probe.c`:

```c
#include <stddef.h>
#include <stdio.h>
#include "SDL_syswm.h"

#define PRINT_SIZE(type_name) printf("size.%s=%zu\n", #type_name, sizeof(type_name))
#define PRINT_ALIGN(type_name) printf("align.%s=%zu\n", #type_name, _Alignof(type_name))
#define PRINT_OFFSET(type_name, field_name) printf("offset.%s.%s=%zu\n", #type_name, #field_name, offsetof(type_name, field_name))

int main(void)
{
    PRINT_SIZE(SDL_version);
    PRINT_ALIGN(SDL_version);
    PRINT_SIZE(SDL_SYSWM_TYPE);
    PRINT_ALIGN(SDL_SYSWM_TYPE);
    PRINT_SIZE(SDL_SysWMmsg);
    PRINT_ALIGN(SDL_SysWMmsg);
    PRINT_OFFSET(SDL_SysWMmsg, version);
    PRINT_OFFSET(SDL_SysWMmsg, subsystem);
    PRINT_OFFSET(SDL_SysWMmsg, msg);
    PRINT_SIZE(SDL_SysWMinfo);
    PRINT_ALIGN(SDL_SysWMinfo);
    PRINT_OFFSET(SDL_SysWMinfo, version);
    PRINT_OFFSET(SDL_SysWMinfo, subsystem);
    PRINT_OFFSET(SDL_SysWMinfo, info);
    printf("size.SDL_SysWMinfo.info=%zu\n", sizeof(((SDL_SysWMinfo*)0)->info));
    return 0;
}
```

Actual compilation and execution of this probe is not required in this task. This task creates evidence infrastructure and the explicit `not-run` branch; Task 10 may synthesize layouts only if real probe output is later captured for every required target RID family.

- [ ] **Step 2: Add probe report renderer**

In `semantic_abi.py`, add:

```python
def render_syswm_layout_evidence(entries: dict[str, str]) -> str:
    lines = [
        "# SysWM Layout Evidence",
        "",
        "| Key | Value |",
        "| --- | --- |",
    ]
    for key in sorted(entries):
        lines.append(f"| `{key}` | `{entries[key]}` |")
    lines.append("")
    return "\n".join(lines)


def write_syswm_layout_evidence(repo: pathlib.Path, entries: dict[str, str], overwrite_real_evidence: bool = False) -> pathlib.Path:
    report_path = repo / "spikes" / "binding-generators" / "output" / "reports" / "syswm-layout-evidence.md"
    report_path.parent.mkdir(parents=True, exist_ok=True)
    if report_path.is_file() and not overwrite_real_evidence:
        existing = report_path.read_text(encoding="utf-8")
        evidence_rows = [line for line in existing.splitlines() if line.startswith("| `")]
        has_real_evidence = any("`not-run`" not in line for line in evidence_rows)
        if has_real_evidence:
            raise RuntimeError(f"Refusing to overwrite real SysWM layout evidence: {report_path}")
    report_path.write_text(render_syswm_layout_evidence(entries), encoding="utf-8")
    return report_path
```

- [ ] **Step 3: Add generator CLI hook**

Add `--write-empty-syswm-layout-evidence` to `generate_bindings.py`. This hook writes a report with explicit unavailable entries when native compilation is not run, and refuses to overwrite real evidence unless an explicit force flag is provided:

```python
    parser.add_argument("--write-empty-syswm-layout-evidence", action="store_true", help="Write SysWM layout evidence report with explicit unavailable entries")
    parser.add_argument("--overwrite-real-syswm-layout-evidence", action="store_true", help="Allow the empty SysWM evidence writer to overwrite an existing real evidence report")
```

When set, write:

```python
entries = {
    "windows.desktop.win-x86": "not-run",
    "windows.desktop.win-x64": "not-run",
    "windows.desktop.win-arm64": "not-run",
    "linux.x64": "not-run",
    "linux.arm64": "not-run",
    "macos.x64": "not-run",
    "macos.arm64": "not-run",
}
report_path = write_syswm_layout_evidence(repo, entries, overwrite_real_evidence=args.overwrite_real_syswm_layout_evidence)
print(f"syswm-layout evidence: {report_path}")
return 0
```

- [ ] **Step 4: Run evidence report generation**

Run:

```pwsh
python spikes/binding-generators/clangsharp/generate_bindings.py --write-empty-syswm-layout-evidence
```

Expected: report is written and every target RID family is explicitly marked `not-run`.

- [ ] **Step 5: Commit checkpoint, approval required**

Before committing, present the changed files and this proposed message to Deniz:

```text
test(binding-spike): add SysWM layout evidence probe scaffold
```

After explicit approval, run:

```pwsh
git add spikes/binding-generators/clangsharp/probes/syswm_layout_probe.c spikes/binding-generators/clangsharp/semantic_abi.py spikes/binding-generators/clangsharp/generate_bindings.py spikes/binding-generators/output/reports/syswm-layout-evidence.md
git commit -m "test(binding-spike): add SysWM layout evidence probe scaffold"
```

## Task 10: Synthesize Proven SysWM Layout Or Keep Explicit Deferral

**Files:**
- Modify: `spikes/binding-generators/clangsharp/semantic_abi.py`
- Modify: `spikes/binding-generators/clangsharp/generate_bindings.py`
- Regenerate: Core generated output and SysWM/oracle reports

- [ ] **Step 1: Check SysWM layout evidence report**

Open `spikes/binding-generators/output/reports/syswm-layout-evidence.md`.

If every target entry is `not-run`, keep the explicit deferral from Task 8 and do not synthesize full layout.

If Windows/Linux/macOS target entries contain real `sizeof`, alignment, and `offsetof` data, continue with layout synthesis.

- [ ] **Step 2: Add explicit deferral report when evidence is unavailable**

If evidence is unavailable, update the oracle Markdown SysWM section to include:

```text
SysWM full layout: deferred because native size/alignment/offset evidence is unavailable for at least one target RID family.
SDL_GetWindowWMInfo: remains excluded until SDL_SysWMinfo layout proof exists.
```

Run:

```pwsh
dotnet run --file spikes/binding-generators/clangsharp/oracle.cs -- --family sdl2-core --family sdl2-image --write-report
```

Expected: report includes the deferral text and no host-shaped SysWM layout is emitted.

- [ ] **Step 3: Add synthesis only when evidence exists**

If evidence exists, add `render_syswm_layout(namespace, evidence)` in `semantic_abi.py`. It must emit explicit `[StructLayout(LayoutKind.Sequential, Size = ...)]` / `[StructLayout(LayoutKind.Explicit, Size = ...)]` declarations using evidence values, not guesses.

The first accepted synthesized output must include only views with proof:

```text
WindowsDesktop when win-x86, win-x64, and win-arm64 proof exists
Linux when linux-x64 and linux-arm64 proof exists
MacOS when osx-x64 and osx-arm64 proof exists
```

Do not emit WinRT, UIKit/iOS, Android, DirectFB, Vivante, OS/2, MIR, Haiku, RISCOS, or KMSDRM arms unless the evidence report has matching proof.

- [ ] **Step 4: Reconsider `SDL_GetWindowWMInfo` only after layout proof**

If and only if `SDL_SysWMinfo` is synthesized from proof, remove `SDL_GetWindowWMInfo` from the ClangSharp exclusion path and regenerate. If proof remains unavailable, leave `SDL_GetWindowWMInfo` excluded and reported as deferred.

- [ ] **Step 5: Run verification**

Run:

```pwsh
python spikes/binding-generators/clangsharp/generate_bindings.py --scope full --codegen both --family core --execute --clean-output --vcpkg-triplet x64-windows-hybrid --use-platform-header-shims
dotnet run --file spikes/binding-generators/clangsharp/oracle.cs -- --self-test
dotnet run --file spikes/binding-generators/clangsharp/oracle.cs -- --family sdl2-core --family sdl2-image --write-report
dotnet build spikes/binding-generators/clangsharp/src/Janset.SDL2.Core/Janset.SDL2.Core.csproj -c Release
```

Expected: build succeeds. The oracle either proves SysWM layout for emitted views or reports explicit deferral. It must not show host-shaped full layout as success.

- [ ] **Step 6: Commit checkpoint, approval required**

Before committing, present the changed files and one of these proposed messages to Deniz:

```text
fix(binding-spike): synthesize proven SysWM layout
```

or:

```text
test(binding-spike): record explicit SysWM layout deferral
```

After explicit approval, stage only the files touched by this task and run the selected commit command.

## Task 11: Full Regeneration And Final Verification

**Files:**
- Regenerate: `spikes/binding-generators/clangsharp/src/Janset.SDL2.Core/Generated/**`
- Regenerate: `spikes/binding-generators/clangsharp/src/Janset.SDL2.Image/Generated/**`
- Regenerate: `spikes/binding-generators/output/reports/*.md`

- [ ] **Step 1: Regenerate all ClangSharp spike output**

Run:

```pwsh
python spikes/binding-generators/clangsharp/generate_bindings.py --scope full --codegen both --family all --execute --clean-output --vcpkg-triplet x64-windows-hybrid --use-platform-header-shims
```

Expected: command exits `0`. Known ClangSharp command warnings may remain in `clangsharp-full.md`, but no empty generated output errors are allowed.

- [ ] **Step 2: Regenerate reports**

Run:

```pwsh
python spikes/binding-generators/clangsharp/generate_bindings.py --probe-semantic-abi --execute
dotnet run --file spikes/binding-generators/clangsharp/oracle.cs -- --family sdl2-core --family sdl2-image --write-report
```

Run the empty SysWM evidence writer only if `spikes/binding-generators/output/reports/syswm-layout-evidence.md` is absent or already contains only `not-run` entries:

```pwsh
python spikes/binding-generators/clangsharp/generate_bindings.py --write-empty-syswm-layout-evidence
```

Do not run it over real native evidence unless Deniz explicitly approves `--overwrite-real-syswm-layout-evidence`.

Expected: reports are updated:

```text
spikes/binding-generators/output/reports/semantic-abi-capabilities.md
spikes/binding-generators/output/reports/syswm-layout-evidence.md
spikes/binding-generators/output/reports/oracle-evidence-clangsharp.md
spikes/binding-generators/output/reports/clangsharp-full.md
```

- [ ] **Step 3: Run self-tests**

Run:

```pwsh
python spikes/binding-generators/clangsharp/generate_bindings.py --self-test
dotnet run --project spikes/binding-generators/clangsharp/postprocess/Janset.SDL2.PostProcess.csproj -c Release -- self-test
dotnet run --file spikes/binding-generators/clangsharp/oracle.cs -- --self-test
```

Expected:

```text
self-test: PASS
postprocess self-test: PASS
self-test: PASS
```

- [ ] **Step 4: Build generated projects**

Run:

```pwsh
dotnet build spikes/binding-generators/clangsharp/src/Janset.SDL2.Core/Janset.SDL2.Core.csproj -c Release
dotnet build spikes/binding-generators/clangsharp/src/Janset.SDL2.Image/Janset.SDL2.Image.csproj -c Release
```

Expected: both builds succeed with `0 Error(s)`. Investigate any warnings introduced by this plan before accepting the task.

- [ ] **Step 5: Check whitespace and Slopwatch**

Run:

```pwsh
git diff --check
slopwatch analyze --fail-on warning --exclude "artifacts/**,external/**,vcpkg_installed/**,spikes/binding-generators/references/**,**/bin/**,**/obj/**"
```

Expected: both commands exit `0`; Slopwatch reports `0 issue(s) found`.

- [ ] **Step 6: Inspect final oracle report**

Open `spikes/binding-generators/output/reports/oracle-evidence-clangsharp.md` and verify:

```text
raw-abi-public-class: 0 findings for Core/Image
raw-abi-public-import: 0 effective public leaks for Core/Image
family-namespace-drift: 0 findings for Image
required SDL.h functions/constants: 0 missing for B-slice surface
deferred-layout-sdl-rwops: 0 findings
platform-sensitive-long: 0 findings, or explicit Compat strategy/blocker entries backed by compat-c-long-prototype.md
platform-sensitive-wchar: 0 shared-surface findings or explicit Windows-only classification
SDL_syswm: proven layouts or explicit deferral, never host-shaped false success
dynapi: total occurrences, unique names, duplicates, missing, extra, exclusions, deferrals visible
```

- [ ] **Step 7: Commit checkpoint, approval required**

Before committing, present the final changed files and this proposed message to Deniz:

```text
fix(binding-spike): implement semantic ABI classification track
```

After explicit approval, stage only the intended files and run:

```pwsh
git add spikes/binding-generators/clangsharp spikes/binding-generators/output/reports
git commit -m "fix(binding-spike): implement semantic ABI classification track"
```

## Task 12: Final Documentation Review

**Files:**
- Modify if needed: `docs/superpowers/specs/2026-05-23-clangsharp-semantic-abi-implementation-design.md`
- Modify if needed: `docs/research/semantic-abi-type-classification-research.md`

- [ ] **Step 1: Check whether implementation changed the spec contract**

Read the final C0 mechanism table and final oracle report. If the implemented layer differs from the spec, update the spec with the final layer decision. If the implementation merely follows the existing spec, do not edit docs.

- [ ] **Step 2: Preserve research/spec distinction**

If new external facts were discovered during C0, add them to `docs/research/semantic-abi-type-classification-research.md` as evidence. Do not turn the research document into the implementation plan.

- [ ] **Step 3: Run docs checks**

Run:

```pwsh
git diff --check
```

Expected: command exits `0`.

- [ ] **Step 4: Commit checkpoint, approval required**

If docs changed, present the changed files and this proposed message to Deniz:

```text
docs(binding-spike): record semantic ABI implementation outcomes
```

After explicit approval, run:

```pwsh
git add docs/superpowers/specs/2026-05-23-clangsharp-semantic-abi-implementation-design.md docs/research/semantic-abi-type-classification-research.md
git commit -m "docs(binding-spike): record semantic ABI implementation outcomes"
```

If docs did not change, skip this commit checkpoint and state that the implementation stayed within the approved spec.
