# Iteration 2 — Config Surface Unification: Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:subagent-driven-development` (recommended) or `superpowers:executing-plans` to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Consolidate the post-Item-1 binding-generator's dispersed config surface into a single mechanically-seeded `config/family-config.json`, read by both `generate_bindings.py` and the C# postprocess, without changing one byte of generated output.

**Architecture:** Parity-gated mechanical migration. Phase 0 seeds the config from live sources and proves it byte-equals those sources. Phases 1–3 repoint each consumer (Python orchestrator, family RSP identity, C# postprocess) at the config one slice at a time, each gated by a full regeneration that must produce a byte-identical `git diff`. Phase 4 deletes the now-orphaned source files only after parity passes, then sweeps stale references and finishes docs. Phase 5 runs the full evidence battery.

**Tech Stack:** Python 3 (orchestrator + seed/verify tooling, `json` stdlib only), C# / .NET 10 + `System.Text.Json` (postprocess console app), ClangSharp tool, Roslyn (`Microsoft.CodeAnalysis`), TUnit AbiTests, Slopwatch.

---

## Spec deltas — read before executing

This plan is faithful to [`iteration-2-config-surface-unification-spec.md`](iteration-2-config-surface-unification-spec.md) with the following **explicitly recorded deviations**. Each was verified against the live repo at branch `spike/binding-autogen-sdl2-gfx`, commit `923c78d`. Confirm with Deniz if any look wrong before starting.

1. **Oracle is out of scope this iteration (Deniz decision, 2026-05-27).** `oracle.cs FamilyConfigs` / `KnownFamilies` keep their own per-family records. Reason: 3 of the oracle's 9 fields (`CakePreviewRelativePath`, `Sdl2CsRelativePath`, `UsesSdl2Dynapi`) are oracle-comparison concerns with no home in a scope-only config, and forcing them in would pollute the M7 manifest dry-run. **Consequence:** drop spec exit criterion **§9.6**; mark spec inventory items **#17 (`oracle.cs FamilyConfigs`)** and **#18 (`oracle.cs KnownFamilies`)** and the `oracle.cs` row in **§7.1** as *Deferred — see Item TBD*. The family-identity duplication in the oracle is a known, documented carry-over, not a regression.

2. **Spec §7.1 names the wrong file for flags loading.** The `[Flags]` allow-list is loaded by **`postprocess/FlagsEnumRosterLoader.cs`** (`LoadForFamily` + `ResolveRosterPath`), not by `FlagsAttributeRewriter.cs` (which only consumes the resulting `HashSet`). Likewise the opaque roster is loaded by `OpaqueHandleEmitRewriter.LoadRoster` + the `ResolveOpaqueHandleRosterPath` local function in `Program.cs`. This plan targets the real files.

3. **C# locates rosters by ancestor-walk, not by a passed path.** `Program.cs ResolveOpaqueHandleRosterPath(inputDir)` walks `inputDir` ancestors; `FlagsEnumRosterLoader.ResolveRosterPath()` walks the **current working directory** ancestors. The new config gets one shared locator (`Config/FamilyConfigLocator.cs`) that preserves both entry points.

4. **Dormant families are seeded with empty header lists.** Only `core` and `image` have `scope/*.headers.txt` files; `ttf`/`mixer`/`gfx` have none. There is no live source to mechanically seed dormant header lists, so they are seeded as `"headers": []`, `"required_surface": null`, `"platform_sensitive_headers": []`. Their identity, `opaque_handles`, `flags_enums`, `clong_methods`, and `owner_mode` **are** seeded mechanically (those sources exist for all 5). Items 3–5 populate dormant `headers[]` directly in config when they activate each family. This diverges from the spec's illustrative sample (which showed dormant headers populated) — but the spec's §4.1 banner says that sample is non-reference data, and `selected_families("all")` is `["core","image"]`, so dormant header lists are dead data in Iteration 2.

5. **The spec's sample `required_surface.functions` order is wrong.** Live `scope/sdl2-core-sdlh-required.json` orders them `SDL_Init, SDL_InitSubSystem, SDL_QuitSubSystem, SDL_WasInit, SDL_Quit` (Quit last); the spec sample shows Quit third. This array is **order-sensitive** (validated against `build/manifest.json` and the reversed-order rejection self-test). This is exactly why the seed must be mechanical, not hand-copied.

6. **`library_version` is sourced from the existing rosters**, which both already carry per-family `library_version` (`2.32.10` core, `2.8.8` image, `2.24.0` ttf, `2.8.1` mixer, `1.0.4` gfx). It is **not** in `FAMILY_CONFIG`. Added to parity checks (spec §8.3 omits it).

7. **`library_name` is sourced from the family RSP `--libraryPath` line.** It lives nowhere else today. The seed reads it from `rsp/sdl2-<family>.rsp`. `include_subdir` is `"SDL2"` for all families (the hardcoded `include / "SDL2"` path segment in the orchestrator).

8. **Constitution + spike README are already edited (uncommitted) toward the target.** `git diff` shows Determinism Input #6 repointed to `config/family-config.json`, the new "Configurable Scope Vs Policy Mechanism" section added, the opaque/flags roster references repointed, and the README "Endgame Vision" section added. Phase 4 **verifies and completes** these; it does not redo them. Spec exit **§9.12** is therefore mostly satisfied already — confirm wording, don't duplicate.

### Hard constraints (from spec §1.1 / handoff / AGENTS.md)

- **Do not touch** `build/manifest.json`, `vcpkg.json`, `.config/dotnet-tools.json`, csproj/`.slnx`, `shims/`, per-header RSP contents.
- **Do not** make `selected_families("all")` activate ttf/mixer/gfx — stays `["core","image"]`.
- **Do not** use one family's generated `.g.cs` as input to another's generation.
- **Run Slopwatch** after every C#/project/test change: `slopwatch analyze --fail-on warning --exclude "artifacts/**,external/**,vcpkg_installed/**,**/bin/**,**/obj/**"`.
- This plan **executes code/build changes** — executing it is itself the approval-gated work. Treat plan approval as the "go" for the changes it describes.

### Branching & commit workflow

This iteration lands as **one revertable changeset** on `spike/binding-autogen-sdl2-gfx`. To get that while keeping per-phase safety/bisectability, work on a dedicated branch with checkpoint commits, then squash back:

1. **Work branch (Task 0.0):** branch off `spike/binding-autogen-sdl2-gfx` into `spike/iteration-2-config-unification`. **No git worktree** (Deniz: plain branch).
2. **Per-phase checkpoint commits:** each phase ends with a normal commit *on the work branch*. These are intermediate WIP markers — **no approval needed**, they never reach the spike branch as separate commits.
3. **Single landing commit (Task 5.4):** when all evidence is green, squash-merge the work branch into `spike/binding-autogen-sdl2-gfx` as **one approval-gated commit**. That commit is the only thing Deniz signs off, and `git revert <sha>` undoes the entire iteration atomically.

So "gated" applies to exactly one commit — the final squash landing. Intermediate checkpoint commits on the work branch are free.

> **Approval-gate exception (explicitly granted by Deniz, 2026-05-27).** AGENTS.md §"Before Any Commit" normally requires approval for *every* commit. Deniz designed and approved this work-branch + squash model in conversation, granting an explicit exception: intermediate WIP checkpoint commits on `spike/iteration-2-config-unification` do **not** need per-commit approval. Only the single squash-landing commit onto `spike/binding-autogen-sdl2-gfx` (Task 5.4) is approval-gated. This exception is scoped to this iteration.

### Environment preconditions

- Windows host with `vcpkg_installed/x64-windows-hybrid/include/SDL2/*.h` present (run `dotnet run --file tools.cs -- setup` first if absent). Full regen reads those headers.
- ClangSharp tool restored (`dotnet tool restore` under the spike if needed).
- All commands below assume working directory `E:\repos\my-projects\janset2d\sdl2-cs-bindings` unless stated; the spike root is `spikes\binding-generators`.

### Determinism gate (used in every phase)

The single most important regression check. After any consumer change:

```pwsh
python spikes/binding-generators/clangsharp/generate_bindings.py --family all --execute
git diff --ignore-cr-at-eol --stat -- "spikes/binding-generators/clangsharp/src/**/Generated"
```

**Expected: empty output** (no generated file changed). A non-empty diff means the change altered output — stop and debug per `superpowers:systematic-debugging` before proceeding. Generated output is already committed at `923c78d`, so the baseline is HEAD.

---

## File structure

**Created:**

- `spikes/binding-generators/clangsharp/config/family-config.json` — the unified config (survives the iteration).
- `spikes/binding-generators/clangsharp/config/config_migration.py` — throwaway seed + parity-verify tool (deleted in Phase 4 with the source files it references).
- `spikes/binding-generators/clangsharp/postprocess/Config/FamilyConfigLocator.cs` — ancestor-walk path resolution for the config.
- `spikes/binding-generators/clangsharp/postprocess/Config/FamilyConfig.cs` — typed model + `System.Text.Json` loader; exposes per-family identity, opaque/flags/clong sections, and global platform views.

**Modified:**

- `generate_bindings.py` — load config; remove `FAMILY_CONFIG`, `PLATFORM_SENSITIVE_HEADERS`, `ALL_PLATFORM_MACROS`, `SDL2_PLATFORM_VIEWS`, `owner_mode_for_family`, `uniform_opaque_extra_args_for_family`; derive `--methodClassName`/`--libraryPath`; rewrite self-tests.
- `rsp/sdl2-core.rsp`, `rsp/sdl2-image.rsp` — remove `--libraryPath` and `--methodClassName` lines.
- `postprocess/Program.cs` — replace `ResolveFamilyFromOutputDir` switch and `ResolveOpaqueHandleRosterPath` with config-driven resolution; flags/opaque loading via `FamilyConfig`.
- `postprocess/UniformOpaqueFamilyIdentity.cs`, `postprocess/UniformOpaqueOwnerMode.cs` — derive family/owner from config.
- `postprocess/OpaqueHandleEmitRewriter.cs` — move roster JSON parsing to `FamilyConfig`.
- `postprocess/FlagsEnumRosterLoader.cs` — load allow-list from `FamilyConfig` (or delete; see Task 3.3).
- `postprocess/ClongDualDispatchRewriter.cs` — read `clong_methods` from config; keep native-type classification in code.
- `postprocess/PlatformDeltaPostProcessor.cs` — derive `PlatformOrder`/`SupportedOsByPlatform` from config.
- `postprocess/PostProcessSelfTests.cs` — update assertions to config-derived data.

**Deleted (Phase 4, after parity passes):**

- `scope/sdl2-core.headers.txt`, `scope/sdl2-image.headers.txt`, `scope/sdl2-core-sdlh-required.json`, `policy/opaque-handle-roster.json`, `policy/flags-enum-roster.json`, and `config/config_migration.py`.

---

## Phase 0 — Seed the config and prove parity (no consumer changes)

Goal: a `family-config.json` that **byte-equals** every live source, with an automated parity check. Nothing else changes; consumers still read the old files. This de-risks everything downstream.

### Task 0.0: Create the work branch and reconcile the spec

**Files:** `spikes/binding-generators/docs/items/iteration-2-config-surface-unification-spec.md` (doc edit).

- [ ] **Step 1: Branch off the spike branch (no worktree)**

Run:
```pwsh
git switch spike/binding-autogen-sdl2-gfx
git switch -c spike/iteration-2-config-unification
git branch --show-current
```
Expected: `spike/iteration-2-config-unification`. All checkpoint commits below land here; the squash back to `spike/binding-autogen-sdl2-gfx` happens in Task 5.4.

- [ ] **Step 2: Reconcile the spec with the agreed deltas FIRST (specs before scalpels)**

Before any code, edit the spec so it agrees with this plan — otherwise implementation starts from a spec/plan mismatch. Apply:
- Annotate inventory items **#17/#18** and the `oracle.cs` row in **§7.1** with "**Deferred (2026-05-27 Deniz decision)** — oracle retains its own FamilyConfigs this iteration." Mark exit criterion **§9.6** as deferred.
- Amend **§9.1** / the §4.1 sample note: dormant families (`ttf`/`mixer`/`gfx`) are seeded with `headers: []` and `required_surface: null` (no live source exists); their identity/rosters/clong/owner_mode are seeded mechanically. Items 3–5 populate dormant `headers[]`.
- Add a note recording delta 5 (sample `required_surface` order corrected — `SDL_Quit` last) and delta 7 (`library_name` from RSP for active families; explicit mapping for dormant — see Task 0.2).

(Task 4.4 later only **verifies** this landed — it does not redo it.)

### Task 0.1: Confirm clean baseline

**Files:** none (verification only).

- [ ] **Step 1: Verify generated output is committed and clean**

Run:

```pwsh
git status --short -- "spikes/binding-generators/clangsharp/src"
```

Expected: empty (no pending changes under generated trees). If not empty, stop — the determinism gate needs a clean baseline.

- [ ] **Step 2: Record current self-test + build health as the baseline**

Run:

```pwsh
python spikes/binding-generators/clangsharp/generate_bindings.py --self-test
dotnet run --project spikes/binding-generators/clangsharp/postprocess/Janset.SDL2.PostProcess.csproj -c Release -- --self-test
```

Expected: both print `self-test: PASS`. Note the results; these must still pass at the end.

### Task 0.2: Write the seed step of the migration tool

**Files:**

- Create: `spikes/binding-generators/clangsharp/config/config_migration.py`

- [ ] **Step 1: Write the seeder**

This script imports `generate_bindings` as a module to read its in-code constants, and reads the external JSON/txt/rsp sources directly. It writes `family-config.json` with 2-space indent and a trailing newline, LF line endings.

Create `spikes/binding-generators/clangsharp/config/config_migration.py`:

```python
"""Throwaway Iteration 2 migration tool: seed + verify family-config.json.

Usage:
    python config_migration.py --seed     # write family-config.json from live sources
    python config_migration.py --verify   # assert family-config.json == live sources

Deleted in Phase 4 once the old sources are removed (it imports them).
"""
import argparse
import json
import pathlib
import re
import sys

CLANGSHARP = pathlib.Path(__file__).resolve().parent.parent  # .../clangsharp
SPIKE = CLANGSHARP.parent                                    # .../binding-generators
SCOPE = SPIKE / "scope"
POLICY = CLANGSHARP / "policy"
RSP = CLANGSHARP / "rsp"
CONFIG_PATH = CLANGSHARP / "config" / "family-config.json"
CLONG_CS = CLANGSHARP / "postprocess" / "ClongDualDispatchRewriter.cs"

sys.path.insert(0, str(CLANGSHARP))
import generate_bindings as gb  # noqa: E402

# Family ordering is fixed and matches FAMILY_CONFIG insertion order.
FAMILIES = ["core", "image", "ttf", "mixer", "gfx"]
INCLUDE_SUBDIR = "SDL2"  # all SDL2 families share the SDL2 include subdir today

# Only active families have RSP files on disk today; sdl2-ttf/mixer/gfx.rsp are
# future. For dormant families library_name cannot be read mechanically, so it is
# stated here as known audited fact (the upstream native library names). verify()
# still re-reads active families from their RSP, so this is not "fully mechanical"
# for dormant families — that is documented in delta 7.
DORMANT_LIBRARY_NAME = {"ttf": "SDL2_ttf", "mixer": "SDL2_mixer", "gfx": "SDL2_gfx"}

# Per-family C-long method scope — the genuine library-level assignment. There
# is no machine-readable family tag on these names in the C# source (it is a
# single flat HashSet), so the assignment is stated explicitly here as known
# domain fact. verify() independently asserts the union of these equals the flat
# AffectedMethodNames set in the C# source, so a typo or a drift fails loudly.
CLONG_METHODS = {
    "core": ["SDL_ThreadID", "SDL_GetThreadID"],
    "image": [],
    "ttf": ["TTF_OpenFontIndex", "TTF_OpenFontIndexRW", "TTF_OpenFontIndexDPI", "TTF_OpenFontIndexDPIRW", "TTF_FontFaces"],
    "mixer": [],
    "gfx": [],
}


def read_json(path: pathlib.Path) -> dict:
    return json.loads(path.read_text(encoding="utf-8"))


def library_name_for_family(family: str) -> str:
    """Active families: read --libraryPath verbatim from their RSP (authoritative).
    Dormant families have no RSP on disk yet, so fall back to the known mapping."""
    rsp_path = RSP / gb.FAMILY_CONFIG[family]["rsp"]
    if not rsp_path.is_file():
        if family in DORMANT_LIBRARY_NAME:
            return DORMANT_LIBRARY_NAME[family]
        raise RuntimeError(f"No RSP for active family '{family}' and no dormant mapping")
    text = rsp_path.read_text(encoding="utf-8").splitlines()
    for i, line in enumerate(text):
        if line.strip() == "--libraryPath":
            return text[i + 1].strip()
    raise RuntimeError(f"--libraryPath not found in {rsp_path.name}")


def headers_for_family(family: str) -> list:
    """Ordered header objects from the scope .txt file, or [] for dormant families."""
    scope_file = SCOPE / gb.FAMILY_CONFIG[family]["headers"]
    if not scope_file.is_file():
        return []
    names = gb.read_header_list(scope_file)
    return [{"order": (i + 1) * 10, "name": name} for i, name in enumerate(names)]


def required_surface_for_family(family: str):
    if family != "core":
        return None
    data = read_json(SCOPE / "sdl2-core-sdlh-required.json")
    return {"functions": list(data["functions"]), "constants": list(data["constants"])}


def clong_flat_from_cs() -> set:
    """Read the flat AffectedMethodNames HashSet from the C# source (the live
    source of truth) so verify() can assert the config's per-family union matches
    it. Used only for parity verification, never to partition."""
    text = CLONG_CS.read_text(encoding="utf-8")
    block = re.search(r"AffectedMethodNames\s*=\s*new\([^)]*\)\s*\{(.*?)\};", text, re.DOTALL)
    if block is None:
        raise RuntimeError("AffectedMethodNames block not found in ClongDualDispatchRewriter.cs")
    return set(re.findall(r'"([^"]+)"', block.group(1)))


def opaque_section(family: str, roster: dict) -> dict:
    entry = roster["families"][family]
    section = {
        "last_audited": entry["last_audited"],
        "source": entry.get("source", ""),
        "auto_detect_well_known": entry["auto_detect_well_known"],
        "force_opaque_exceptions": entry["force_opaque_exceptions"],
        "excluded_candidates": entry.get("excluded_candidates", []),
    }
    return section


def flags_section(family: str, roster: dict) -> dict:
    entry = roster["families"][family]
    return {"last_audited": entry["last_audited"], "allow_list": entry["allow_list"]}


def build_config() -> dict:
    opaque_roster = read_json(POLICY / "opaque-handle-roster.json")
    flags_roster = read_json(POLICY / "flags-enum-roster.json")

    views = [
        {"name": name, "supported_os": os_str, "defines": list(defines)}
        for name, os_str, defines in gb.SDL2_PLATFORM_VIEWS
    ]

    families = {}
    for family in FAMILIES:
        fc = gb.FAMILY_CONFIG[family]
        families[family] = {
            "namespace": fc["namespace"],
            "library_version": opaque_roster["families"][family]["library_version"],
            "raw_class": fc["raw_class"],
            "include_subdir": INCLUDE_SUBDIR,
            "library_name": library_name_for_family(family),
            "rsp": f"rsp/{fc['rsp']}",
            "project_dir": fc["library_dir"],
            "owner_mode": gb.owner_mode_for_family(family) == "owner",
            "headers": headers_for_family(family),
            "platform_sensitive_headers": list(gb.PLATFORM_SENSITIVE_HEADERS.get(family, [])),
            "required_surface": required_surface_for_family(family),
            "opaque_handles": opaque_section(family, opaque_roster),
            "flags_enums": flags_section(family, flags_roster),
            "clong_methods": list(CLONG_METHODS[family]),
        }

    return {
        "schema_version": "1.0",
        "last_modified": opaque_roster["last_audited"],
        "global": {
            "platform_views": views,
            "all_platform_macros": list(gb.ALL_PLATFORM_MACROS),
            "base_rsp": "rsp/base.rsp",
        },
        "families": families,
    }


def seed() -> int:
    config = build_config()
    CONFIG_PATH.parent.mkdir(parents=True, exist_ok=True)
    text = json.dumps(config, indent=2, ensure_ascii=False) + "\n"
    with CONFIG_PATH.open("w", encoding="utf-8", newline="\n") as fh:
        fh.write(text)
    print(f"seed: wrote {CONFIG_PATH}")
    return 0


# CLI entry point goes in this same Step 1 so the script is runnable in Step 2.
# verify() is added in Task 0.3; main() resolves it at call time, so --seed works
# now and --verify becomes functional once Task 0.3 lands.
def main() -> int:
    parser = argparse.ArgumentParser(description="Iteration 2 family-config migration tool")
    parser.add_argument("--seed", action="store_true")
    parser.add_argument("--verify", action="store_true")
    args = parser.parse_args()
    if args.seed:
        return seed()
    if args.verify:
        return verify()
    parser.error("pass --seed or --verify")


if __name__ == "__main__":
    sys.exit(main())
```

- [ ] **Step 2: Run the seeder**

Run:

```pwsh
python spikes/binding-generators/clangsharp/config/config_migration.py --seed
```

Expected: `seed: wrote .../config/family-config.json` and the file exists. (`--verify` is wired in Task 0.3.)

### Task 0.3: Write the parity-verify step

**Files:**

- Modify: `spikes/binding-generators/clangsharp/config/config_migration.py`

- [ ] **Step 1: Add the verifier (the spec §8.3 assertions + deltas 6/7)**

Insert before `main()`:

```python
def _fail(failures: list, ok: bool, message: str) -> None:
    if not ok:
        failures.append(message)


def verify() -> int:
    config = read_json(CONFIG_PATH)
    expected = build_config()
    failures: list = []

    # The seeded config must be byte-equal to a fresh build from live sources.
    _fail(failures, config == expected,
          "family-config.json does not match a fresh mechanical build from live sources")

    # §8.3.2 headers match scope .txt exactly (names + order) for active families.
    for family in ("core", "image"):
        live = gb.read_header_list(SCOPE / gb.FAMILY_CONFIG[family]["headers"])
        cfg = [h["name"] for h in sorted(config["families"][family]["headers"], key=lambda h: h["order"])]
        _fail(failures, cfg == live, f"headers parity mismatch for {family}: {cfg} != {live}")

    # §8.3.3/8.3.4 opaque + flags rosters match by name.
    opaque_roster = read_json(POLICY / "opaque-handle-roster.json")
    flags_roster = read_json(POLICY / "flags-enum-roster.json")
    for family in FAMILIES:
        for key in ("auto_detect_well_known", "force_opaque_exceptions", "excluded_candidates"):
            live_names = [e["name"] for e in opaque_roster["families"][family].get(key, [])]
            cfg_names = [e["name"] for e in config["families"][family]["opaque_handles"].get(key, [])]
            _fail(failures, cfg_names == live_names, f"opaque {key} mismatch for {family}")
        live_flags = [e["name"] for e in flags_roster["families"][family]["allow_list"]]
        cfg_flags = [e["name"] for e in config["families"][family]["flags_enums"]["allow_list"]]
        _fail(failures, cfg_flags == live_flags, f"flags allow_list mismatch for {family}")
        # delta 6: library_version sourced from roster.
        _fail(failures, config["families"][family]["library_version"] == opaque_roster["families"][family]["library_version"],
              f"library_version mismatch for {family}")

    # §8.3.5 required surface matches (ORDER-SENSITIVE — see delta 5).
    req = read_json(SCOPE / "sdl2-core-sdlh-required.json")
    cfg_req = config["families"]["core"]["required_surface"]
    _fail(failures, cfg_req["functions"] == req["functions"], "required functions order/content mismatch")
    _fail(failures, cfg_req["constants"] == req["constants"], "required constants order/content mismatch")

    # §8.3.6 platform views + macros verbatim.
    cfg_views = [(v["name"], v["supported_os"], v["defines"]) for v in config["global"]["platform_views"]]
    live_views = [(n, os_str, list(d)) for n, os_str, d in gb.SDL2_PLATFORM_VIEWS]
    _fail(failures, cfg_views == live_views, "platform_views verbatim mismatch")
    _fail(failures, config["global"]["all_platform_macros"] == list(gb.ALL_PLATFORM_MACROS),
          "all_platform_macros verbatim mismatch")

    # §8.3.7 family identity matches FAMILY_CONFIG.
    for family in FAMILIES:
        fc = gb.FAMILY_CONFIG[family]
        cf = config["families"][family]
        _fail(failures, (cf["namespace"], cf["raw_class"], cf["project_dir"]) == (fc["namespace"], fc["raw_class"], fc["library_dir"]),
              f"identity mismatch for {family}")
        # delta 7: library_name from RSP --libraryPath (active) or known mapping (dormant).
        _fail(failures, cf["library_name"] == library_name_for_family(family), f"library_name mismatch for {family}")

    # The union of per-family clong_methods must equal the flat C# HashSet
    # (catches a typo in CLONG_METHODS or drift if the C# set changes).
    flat_cfg = set(m for fam in FAMILIES for m in config["families"][fam]["clong_methods"])
    _fail(failures, flat_cfg == clong_flat_from_cs(), "clong_methods union mismatch vs AffectedMethodNames")

    # Header-order validation (§8.4.6): no duplicate order or name within a family.
    for family in FAMILIES:
        headers = config["families"][family]["headers"]
        orders = [h["order"] for h in headers]
        names = [h["name"] for h in headers]
        _fail(failures, len(orders) == len(set(orders)), f"duplicate header order in {family}")
        _fail(failures, len(names) == len(set(names)), f"duplicate header name in {family}")

    if failures:
        for f in failures:
            print(f"verify: FAIL: {f}")
        return 1
    print("verify: PASS")
    return 0
```

- [ ] **Step 2: Run the verifier**

Run:

```pwsh
python spikes/binding-generators/clangsharp/config/config_migration.py --verify
```

Expected: `verify: PASS`. If any line FAILs, fix the seeder (Task 0.2) and re-seed before continuing — the config is wrong, not the verifier.

- [ ] **Step 3: Human diff-review the seeded config**

Run:

```pwsh
git diff --no-index NUL spikes/binding-generators/clangsharp/config/family-config.json
```

Read the whole file. Confirm: 5 families present; `core`/`image` have populated `headers[]`; `ttf`/`mixer`/`gfx` have `"headers": []` and `"required_surface": null`; `required_surface.functions` ends with `SDL_Quit`; `clong_methods` has `["SDL_ThreadID","SDL_GetThreadID"]` under core and the 5 `TTF_*` under ttf; `owner_mode` is `true` for core/ttf/mixer and `false` for image/gfx.

- [ ] **Step 4: Checkpoint commit (on the work branch — no approval needed)**

```pwsh
git add spikes/binding-generators/clangsharp/config/
git commit -m "wip(bindings): seed family-config.json + parity verifier"
```

---

## Phase 1 — Python orchestrator reads config

Goal: `generate_bindings.py` loads all per-family identity/headers/platform/required/owner facts from `family-config.json`. The in-code constants are removed. Output must not change.

### Task 1.1: Add the config loader and accessors to generate_bindings.py

**Files:**

- Modify: `spikes/binding-generators/clangsharp/generate_bindings.py`

- [ ] **Step 1: Add a config loader near the top (after imports, before `FAMILY_CONFIG`)**

Insert:

```python
_CONFIG_CACHE: dict | None = None


def config_path(repo: pathlib.Path) -> pathlib.Path:
    return repo / "spikes" / "binding-generators" / "clangsharp" / "config" / "family-config.json"


def load_config() -> dict:
    global _CONFIG_CACHE
    if _CONFIG_CACHE is None:
        path = config_path(find_repository_root())
        _CONFIG_CACHE = json.loads(path.read_text(encoding="utf-8"))
    return _CONFIG_CACHE


def family_section(family: str) -> dict:
    return load_config()["families"][family]


def family_namespace(family: str) -> str:
    return family_section(family)["namespace"]


def family_raw_class(family: str) -> str:
    return family_section(family)["raw_class"]


def family_rsp_relpath(family: str) -> str:
    # Stored relative to the config file (e.g. "rsp/sdl2-core.rsp"); callers
    # join against the clangsharp root, which is the config file's parent's parent.
    return family_section(family)["rsp"]


def family_library_dir(family: str) -> str:
    return family_section(family)["project_dir"]


def family_library_name(family: str) -> str:
    return family_section(family)["library_name"]


def family_header_names(family: str) -> list[str]:
    headers = sorted(family_section(family)["headers"], key=lambda h: h["order"])
    return [h["name"] for h in headers]


def family_platform_sensitive_headers(family: str) -> list[str]:
    return list(family_section(family)["platform_sensitive_headers"])


def platform_views() -> list[tuple[str, str, list[str]]]:
    return [(v["name"], v["supported_os"], list(v["defines"])) for v in load_config()["global"]["platform_views"]]


def all_platform_macros() -> list[str]:
    return list(load_config()["global"]["all_platform_macros"])


def owner_mode_for_family(family: str) -> str:
    return "owner" if family_section(family)["owner_mode"] else "consumer"
```

Note: `family_rsp_relpath` returns `"rsp/sdl2-core.rsp"`; `extend_rsp_arguments` currently joins `rsp_root / FAMILY_CONFIG[family]['rsp']` where `rsp_root` is `.../clangsharp/rsp`. Since the config value now includes the `rsp/` prefix, that call site changes to join against `.../clangsharp` instead (Step 4).

- [ ] **Step 2: Run self-test to confirm the loader doesn't break existing tests yet**

Run:

```pwsh
python spikes/binding-generators/clangsharp/generate_bindings.py --self-test
```

Expected: still `self-test: PASS` (nothing rewired yet; `owner_mode_for_family` now reads config but returns identical values — the existing owner-mode self-test at the old lines still passes).

### Task 1.2: Repoint call sites from FAMILY_CONFIG/constants to accessors

**Files:**

- Modify: `spikes/binding-generators/clangsharp/generate_bindings.py`

- [ ] **Step 1: Rewrite each generation call site**

Apply these exact substitutions (the right-hand accessor was defined in Task 1.1):

| Location (function) | Old | New |
|---|---|---|
| `extend_rsp_arguments` | `command.append(f"@{rsp_root / FAMILY_CONFIG[family]['rsp']}")` | `clangsharp_root = repo / "spikes" / "binding-generators" / "clangsharp"`<br>`command.append(f"@{clangsharp_root / family_rsp_relpath(family)}")` |
| `platform_output_path` | `library_dir = FAMILY_CONFIG[family]["library_dir"]` | `library_dir = family_library_dir(family)` |
| `output_path_for_required_surface` | same as above | `library_dir = family_library_dir(family)` |
| `generate_required_sdlh_surface` | `FAMILY_CONFIG["core"]["namespace"]`, `FAMILY_CONFIG["core"]["raw_class"]` | `family_namespace("core")`, `family_raw_class("core")` |
| `platform_command_for_header` | `FAMILY_CONFIG[family]["namespace"]`, `FAMILY_CONFIG[family]['raw_class']` | `family_namespace(family)`, `family_raw_class(family)` |
| `platform_command_for_header` | `ALL_PLATFORM_MACROS` | `all_platform_macros()` |
| `output_path_for_header` | `library_dir = FAMILY_CONFIG[family]["library_dir"]` | `library_dir = family_library_dir(family)` |
| `generated_root_for_family` | same | `library_dir = family_library_dir(family)` |
| `command_for_header` | `FAMILY_CONFIG[family]["namespace"]`, `FAMILY_CONFIG[family]['raw_class']` | `family_namespace(family)`, `family_raw_class(family)` |
| `command_for_header` | `PLATFORM_SENSITIVE_HEADERS.get(family, [])` (both uses) | `family_platform_sensitive_headers(family)` |
| `command_for_header` | `ALL_PLATFORM_MACROS` | `all_platform_macros()` |
| `generate_platform_specific_headers` | `for view_name, supported_os, defines in SDL2_PLATFORM_VIEWS:` | `for view_name, supported_os, defines in platform_views():` |
| `uniform_opaque_extra_args_for_family` | `FAMILY_CONFIG[family]["namespace"]` | `family_namespace(family)` |
| `main` (multi-OS pass) | `PLATFORM_SENSITIVE_HEADERS.get(family, [])` | `family_platform_sensitive_headers(family)` |

- [ ] **Step 2: Replace the header-list loading to read from config**

In `main`, the header source changes from scope `.txt` files to config `headers[]`. Replace:

```python
        headers_by_family = {
            family: read_header_list(scope_root / production_header_list_file_name(family))
            for family in selected
        }
```

with:

```python
        for family in selected:
            if not family_header_names(family):
                # A dormant family (ttf/mixer/gfx) has no headers in config yet.
                # selected_families("all") never includes them, so this only fires
                # for an explicit `--family ttf|mixer|gfx`. Fail loudly — do NOT
                # let an explicitly-selected family silently no-op to success
                # (preserves the pre-config FileNotFoundError behavior).
                print(f"ERROR: family '{family}' is dormant — no headers configured "
                      f"in family-config.json yet; it is activated in its expansion item.")
                return 2
        headers_by_family = {
            family: family_header_names(family)
            for family in selected
        }
```

And replace the required-surface allowlist source. The allowlist currently reads `scope_root / "sdl2-core-sdlh-required.json"` in two places (`generate_required_sdlh_surface` and `main`). Add a config-backed builder near `load_config`:

```python
def required_surface_allowlist_from_config() -> RequiredSurfaceAllowlist:
    surface = family_section("core")["required_surface"]
    return RequiredSurfaceAllowlist(
        family="sdl2-core",
        header="SDL.h",
        functions=tuple(surface["functions"]),
        constants=tuple(surface["constants"]),
    )
```

Then in `main`, replace `allowlist = read_required_surface_allowlist(scope_root / "sdl2-core-sdlh-required.json")` with `allowlist = required_surface_allowlist_from_config()`, and in `generate_required_sdlh_surface` replace its `read_required_surface_allowlist(...)` call likewise. The `family != "sdl2-core"` guard stays.

- [ ] **Step 3: Remove the now-dead constants and helpers**

Delete from the file:

- `FAMILY_CONFIG = { ... }` (the whole literal dict, lines ~373–409)
- `ALL_PLATFORM_MACROS = [ ... ]` (lines ~434–462)
- `SDL2_PLATFORM_VIEWS = [ ... ]` (lines ~471–508)
- `PLATFORM_SENSITIVE_HEADERS = { ... }` (lines ~516–525)
- `def production_header_list_file_name(...)` (lines ~985–986) — no longer used after Step 2
- `def uniform_opaque_extra_args_for_family(...)` keeps (it now reads config) — **do not delete**; spec moves it to config-derived, and it's still used in `main`. Keep its body using `family_namespace`.
- The old top-of-file `owner_mode_for_family` (lines ~974–975) is **replaced** by the config version from Task 1.1 — delete the old hardcoded one so there is exactly one definition.

Keep `read_header_list`, `read_required_surface_allowlist`, and `normalize_macro_value` etc. — `read_header_list` is still referenced by the self-test sentinel check (Task 1.3 decides its fate) and `read_required_surface_allowlist` may still be exercised by tests.

- [ ] **Step 4: Run the orchestrator in dry-run to confirm commands are byte-identical**

Run:

```pwsh
python spikes/binding-generators/clangsharp/generate_bindings.py --family all > E:\tmp\cmds-after.txt
git stash; python spikes/binding-generators/clangsharp/generate_bindings.py --family all > E:\tmp\cmds-before.txt; git stash pop
fc E:\tmp\cmds-before.txt E:\tmp\cmds-after.txt
```

Expected: `fc` reports no differences. (Dry-run prints every ClangSharp command; identical commands ⇒ identical output. This is a fast pre-check before the full regen.)

### Task 1.3: Rewrite the Python self-tests for config-derived data

**Files:**

- Modify: `spikes/binding-generators/clangsharp/generate_bindings.py` (`run_self_tests`)

- [ ] **Step 1: Replace the hardcoded `FAMILY_CONFIG` parity block**

The block at ~lines 1621–1678 builds `expected_family_config` and asserts `FAMILY_CONFIG == expected`. Replace it with assertions against the config accessors:

```python
    expected_identity = {
        "core": ("SDL2", "SDLNative", "Janset.SDL2.Core"),
        "image": ("SDL2.Image", "SDL_imageNative", "Janset.SDL2.Image"),
        "ttf": ("SDL2.Ttf", "SDL_ttfNative", "Janset.SDL2.Ttf"),
        "mixer": ("SDL2.Mixer", "SDL_mixerNative", "Janset.SDL2.Mixer"),
        "gfx": ("SDL2.Gfx", "SDL2_gfxNative", "Janset.SDL2.Gfx"),
    }
    for family, (ns, raw, proj) in expected_identity.items():
        actual = (family_namespace(family), family_raw_class(family), family_library_dir(family))
        if actual != (ns, raw, proj):
            failures.append(f"config identity for {family!r}: expected {(ns, raw, proj)!r}, got {actual!r}")
        if family != "core" and family_platform_sensitive_headers(family) != []:
            failures.append(f"platform_sensitive_headers[{family!r}] expected empty list")
    if family_platform_sensitive_headers("core") != ["SDL_main.h", "SDL_system.h"]:
        failures.append("core platform_sensitive_headers regressed")
    expected_library_names = {"core": "SDL2", "image": "SDL2_image", "ttf": "SDL2_ttf", "mixer": "SDL2_mixer", "gfx": "SDL2_gfx"}
    for family, lib in expected_library_names.items():
        if family_library_name(family) != lib:
            failures.append(f"library_name for {family!r}: expected {lib!r}, got {family_library_name(family)!r}")
```

- [ ] **Step 2: Update the `extend_rsp_arguments` self-test fixture**

The fixture at ~lines 1294–1317 builds a temp repo and asserts `expected_family = f"@{rsp_dir / FAMILY_CONFIG['core']['rsp']}"`. Since `FAMILY_CONFIG` is gone and `extend_rsp_arguments` now reads the real config via `family_rsp_relpath`, this self-test needs a config file in the temp repo. Simplest faithful fix: seed a minimal config into the temp repo before calling `extend_rsp_arguments`, and reset the module cache:

```python
        config_dir = tmp / "spikes" / "binding-generators" / "clangsharp" / "config"
        config_dir.mkdir(parents=True)
        (config_dir / "family-config.json").write_text(
            json.dumps({"families": {"core": {"rsp": "rsp/sdl2-core.rsp"}}}), encoding="utf-8")
        (tmp / "tools.cs").write_text("// fixture\n", encoding="utf-8")  # for find_repository_root
        global _CONFIG_CACHE
        _CONFIG_CACHE = None
        expected_family = f"@{(tmp / 'spikes' / 'binding-generators' / 'clangsharp') / 'rsp' / 'sdl2-core.rsp'}"
```

Because `extend_rsp_arguments` calls `find_repository_root()` indirectly only via the passed `repo` arg, confirm the call signature: it takes `repo` explicitly, so `_CONFIG_CACHE` must be reset and `load_config()` must resolve to the temp repo. If `load_config()` always uses `find_repository_root()` (the real repo), refactor `extend_rsp_arguments` to read `family_rsp_relpath` via a small `repo`-scoped read instead of the cache. **Cleaner alternative:** give `extend_rsp_arguments` its rsp path from a `repo`-scoped helper:

```python
def family_rsp_relpath_in(repo: pathlib.Path, family: str) -> str:
    data = json.loads(config_path(repo).read_text(encoding="utf-8"))
    return data["families"][family]["rsp"]
```

and have `extend_rsp_arguments` use `family_rsp_relpath_in(repo, family)`. Then the self-test just writes the temp config and asserts — no cache juggling. Prefer this; reset `_CONFIG_CACHE` only if you keep the cached accessor.

- [ ] **Step 3: Remove obsolete self-test assertions**

Delete the now-meaningless checks:

- the `stale_helpers`/`scope_file_name` check (~1676–1678) keeps but also assert `production_header_list_file_name` is gone: add `"production_header_list_file_name"` to the `stale_helpers` candidate list.
- the `production_header_list_file_name(family)` assertions inside the identity loop (~1663–1667) — delete (function removed).
- the `owner_mode_for_family` self-test (~1870–1882) keeps but now validates config-backed values — leave it; values are unchanged.
- the `uniform_opaque_extra_args_for_family` self-test (~1884–1895) keeps unchanged.
- `selected_families("all") == ["core","image"]` check (~1694–1695) — **keep** (dormancy guard, spec hard constraint).

- [ ] **Step 4: Run the self-test**

Run:

```pwsh
python spikes/binding-generators/clangsharp/generate_bindings.py --self-test
```

Expected: `self-test: PASS`.

### Task 1.4: Full regen determinism gate + commit

- [ ] **Step 1: Regenerate and assert no output change**

Run the determinism gate:

```pwsh
python spikes/binding-generators/clangsharp/generate_bindings.py --family all --execute
git diff --ignore-cr-at-eol --stat -- "spikes/binding-generators/clangsharp/src"
```

Expected: empty diff. If non-empty, debug before continuing.

- [ ] **Step 2: Family-isolation spot check**

Run:

```pwsh
python spikes/binding-generators/clangsharp/generate_bindings.py --family core --execute
git diff --ignore-cr-at-eol --stat -- "spikes/binding-generators/clangsharp/src/Janset.SDL2.Image"
```

Expected: empty (a core-only run must not touch Image's tree). Then regen `--family all` again to restore full state and re-confirm empty diff.

- [ ] **Step 3: Checkpoint commit (work branch)**

```pwsh
git add spikes/binding-generators/clangsharp/generate_bindings.py
git commit -m "wip(bindings): orchestrator reads identity/platform/headers from family-config.json"
```

---

## Phase 2 — RSP identity removal + orchestrator-derived ClangSharp args

Goal: remove the duplicated `--methodClassName` and `--libraryPath` from the family RSPs; have the orchestrator pass them from config. Output unchanged.

### Task 2.1: Pass `--methodClassName` / `--libraryPath` from config

**Files:**

- Modify: `spikes/binding-generators/clangsharp/generate_bindings.py` (`command_for_header`, `platform_command_for_header`)

- [ ] **Step 1: Inject the two args in both command builders**

In `command_for_header`, after the existing `command.extend([... "--include-directory", str(include_root)])` block and before the `--file` block, add:

```python
    command.extend([
        "--methodClassName", family_raw_class(family),
        "--libraryPath", family_library_name(family),
    ])
```

Apply the identical insertion in `platform_command_for_header` at the matching point (after `--include-directory`, before the shim/`--file` block).

> **Determinism note:** the RSP previously supplied these as keyed scalar options loaded via `@sdl2-core.rsp`. ClangSharp uses last-write-wins for keyed entries; since nothing else sets them, moving them to explicit CLI args yields the identical effective value. The regen gate (Step 3) is the proof.

- [ ] **Step 2: Remove the identity lines from the family RSPs**

Edit `rsp/sdl2-core.rsp`: delete lines 1–5 (the `--libraryPath` / `SDL2` / blank / `--methodClassName` / `SDLNative` block) plus the trailing blank line 6, so the file now begins with the `# SDL2.Core enum underlying type.` comment.

Edit `rsp/sdl2-image.rsp`: delete lines 1–5 (the `--libraryPath` / `SDL2_image` / blank / `--methodClassName` / `SDL_imageNative` block) plus the trailing blank line 6, so the file begins with the `# IMG_SetError / IMG_GetError` comment.

Leave every other line (remaps, excludes, type maps) untouched.

- [ ] **Step 3: Regen determinism gate**

Run:

```pwsh
python spikes/binding-generators/clangsharp/generate_bindings.py --family all --execute
git diff --ignore-cr-at-eol --stat -- "spikes/binding-generators/clangsharp/src"
```

Expected: empty. (RSP files themselves show as modified in `git status` — that's the intended source change. Only the `src/**/Generated` trees must be unchanged.)

- [ ] **Step 4: Self-test + checkpoint commit (work branch)**

```pwsh
python spikes/binding-generators/clangsharp/generate_bindings.py --self-test
```

Expected: PASS. Then:

```pwsh
git add spikes/binding-generators/clangsharp/generate_bindings.py spikes/binding-generators/clangsharp/rsp/sdl2-core.rsp spikes/binding-generators/clangsharp/rsp/sdl2-image.rsp
git commit -m "wip(bindings): derive ClangSharp methodClassName/libraryPath from config, drop RSP duplication"
```

---

## Phase 3 — C# postprocess reads config

Goal: the postprocess derives family identity, owner mode, opaque/flags rosters, clong method scope, and platform views from `family-config.json`. No hardcoded family switch/if-chains remain (except oracle, deferred). Output unchanged.

> **Phase 3 builds as one unit — do NOT expect a green build mid-phase.** `Janset.SDL2.PostProcess` is a single project. Tasks 3.2–3.4 change constructor signatures (`ClongDualDispatchRewriter`, `PlatformDeltaPostProcessor`) and method signatures (`UniformOpaqueFamilyIdentity.Resolve`, `UniformOpaqueOwnerMode.Resolve`) and remove loaders (`FlagsEnumRosterLoader`, `OpaqueHandleEmitRewriter.LoadRoster`) that **`PostProcessSelfTests.cs` still calls**. The project will not compile until Task 3.5 updates those self-test call sites. Therefore the **authoritative green build + Slopwatch + self-test gate is Task 3.6 Step 2** (after 3.5). The only standalone build that must pass mid-phase is Task 3.1 (new isolated files). Intermediate edit tasks end with a "compiles in isolation? n/a — unit builds at 3.6" marker, not a green-build gate.

### Task 3.1: Add the C# config locator + model

**Files:**

- Create: `spikes/binding-generators/clangsharp/postprocess/Config/FamilyConfigLocator.cs`
- Create: `spikes/binding-generators/clangsharp/postprocess/Config/FamilyConfig.cs`

- [ ] **Step 1: Write the locator**

```csharp
namespace Janset.SDL2.PostProcess.Config;

/// <summary>
/// Resolves the unified family-config.json by walking directory ancestors.
/// Replaces the per-roster ancestor walks previously duplicated in Program.cs
/// (ResolveOpaqueHandleRosterPath) and FlagsEnumRosterLoader (ResolveRosterPath).
/// </summary>
internal static class FamilyConfigLocator
{
    private static readonly string[] RelativeSegments =
        ["spikes", "binding-generators", "clangsharp", "config", "family-config.json"];

    public static string Resolve(string? startDir = null)
    {
        var dir = new DirectoryInfo(Path.GetFullPath(startDir ?? Directory.GetCurrentDirectory()));
        while (dir is not null)
        {
            var candidate = Path.Combine(new[] { dir.FullName }.Concat(RelativeSegments).ToArray());
            if (File.Exists(candidate))
            {
                return candidate;
            }
            dir = dir.Parent;
        }

        throw new FileNotFoundException(
            "Could not locate family-config.json by walking ancestors of " +
            $"'{startDir ?? Directory.GetCurrentDirectory()}'. " +
            "Expected at <repo>/spikes/binding-generators/clangsharp/config/family-config.json.");
    }
}
```

- [ ] **Step 2: Write the model + loader**

```csharp
using System.Text.Json;

namespace Janset.SDL2.PostProcess.Config;

internal sealed record PlatformView(string Name, string SupportedOs, IReadOnlyList<string> Defines);

internal sealed class FamilyConfig
{
    private readonly JsonElement _root;

    private FamilyConfig(JsonElement root) => _root = root;

    private const string ExpectedSchemaVersion = "1.0";

    public static FamilyConfig Load(string? startDir = null)
    {
        var path = FamilyConfigLocator.Resolve(startDir);
        // Clone the root so it survives JsonDocument disposal; dispose the doc.
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var root = doc.RootElement.Clone();
        // Preserve the schema-version guard the retired roster loaders enforced,
        // so a future schema bump fails loudly instead of silently mis-parsing.
        var schema = root.GetProperty("schema_version").GetString();
        if (!string.Equals(schema, ExpectedSchemaVersion, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"family-config.json has schema_version '{schema}', expected '{ExpectedSchemaVersion}'.");
        }
        return new FamilyConfig(root);
    }

    private JsonElement Family(string family)
    {
        if (!_root.GetProperty("families").TryGetProperty(family, out var entry))
        {
            throw new InvalidDataException($"family-config.json is missing family '{family}'.");
        }
        return entry;
    }

    public string Namespace(string family) => Family(family).GetProperty("namespace").GetString()!;
    public string ProjectDir(string family) => Family(family).GetProperty("project_dir").GetString()!;
    public bool OwnerMode(string family) => Family(family).GetProperty("owner_mode").GetBoolean();

    /// <summary>All family ids declared in the config, in document order.</summary>
    public IReadOnlyList<string> Families()
        => _root.GetProperty("families").EnumerateObject().Select(p => p.Name).ToList();

    /// <summary>Map of project_dir -> family id, replacing the hardcoded directory switch.</summary>
    public IReadOnlyDictionary<string, string> ProjectDirToFamily()
        => Families().ToDictionary(ProjectDir, f => f, StringComparer.OrdinalIgnoreCase);

    /// <summary>Map of namespace -> family id, replacing the hardcoded namespace switch.</summary>
    public IReadOnlyDictionary<string, string> NamespaceToFamily()
        => Families().ToDictionary(Namespace, f => f, StringComparer.Ordinal);

    public HashSet<string> FlagsAllowList(string family)
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        foreach (var e in Family(family).GetProperty("flags_enums").GetProperty("allow_list").EnumerateArray())
        {
            set.Add(e.GetProperty("name").GetString()!);
        }
        return set;
    }

    public IReadOnlyList<string> ClongMethods(string family)
        => Family(family).GetProperty("clong_methods").EnumerateArray().Select(e => e.GetString()!).ToList();

    /// <summary>Opaque handle name sets for a family; optionally union Core's handles (satellite pull).</summary>
    public (HashSet<string> AutoDetect, HashSet<string> ForceOpaque) OpaqueHandles(string family, bool includeCoreHandles)
    {
        var autoDetect = new HashSet<string>(StringComparer.Ordinal);
        var forceOpaque = new HashSet<string>(StringComparer.Ordinal);
        AppendOpaque(Family(family), autoDetect, forceOpaque);
        if (includeCoreHandles && !family.Equals("core", StringComparison.Ordinal))
        {
            AppendOpaque(Family("core"), autoDetect, forceOpaque);
        }
        return (autoDetect, forceOpaque);
    }

    private static void AppendOpaque(JsonElement family, HashSet<string> autoDetect, HashSet<string> forceOpaque)
    {
        var opaque = family.GetProperty("opaque_handles");
        foreach (var e in opaque.GetProperty("auto_detect_well_known").EnumerateArray())
        {
            autoDetect.Add(e.GetProperty("name").GetString()!);
        }
        foreach (var e in opaque.GetProperty("force_opaque_exceptions").EnumerateArray())
        {
            forceOpaque.Add(e.GetProperty("name").GetString()!);
        }
    }

    public IReadOnlyList<PlatformView> PlatformViews()
        => _root.GetProperty("global").GetProperty("platform_views").EnumerateArray()
            .Select(v => new PlatformView(
                v.GetProperty("name").GetString()!,
                v.GetProperty("supported_os").GetString()!,
                v.GetProperty("defines").EnumerateArray().Select(d => d.GetString()!).ToList()))
            .ToList();
}
```

- [ ] **Step 3: Build to confirm it compiles**

Run:

```pwsh
dotnet build spikes/binding-generators/clangsharp/postprocess/Janset.SDL2.PostProcess.csproj -c Release
```

Expected: 0 warnings, 0 errors. (Nothing consumes `FamilyConfig` yet; this just verifies the new files compile.)

### Task 3.2: Replace family-identity resolution in Program.cs / UniformOpaque*

**Files:**

- Modify: `postprocess/Program.cs`, `postprocess/UniformOpaqueFamilyIdentity.cs`, `postprocess/UniformOpaqueOwnerMode.cs`

- [ ] **Step 1: Add the `using` and load config once in Program.cs**

At the top of `Program.cs` add `using Janset.SDL2.PostProcess.Config;`. After `var outputDir = PostProcessCli.ResolveOutputDirectory(args, inputDir);` add:

```csharp
var familyConfig = FamilyConfig.Load(inputDir);
```

- [ ] **Step 2: Replace `ResolveFamilyFromOutputDir` with config-driven resolution**

Replace the `flags-detect` case body's family resolution and the static `ResolveFamilyFromOutputDir` function. New shared local function (keep it a local static at file end):

```csharp
static string ResolveFamilyFromOutputDir(string outputDir, FamilyConfig config)
{
    var map = config.ProjectDirToFamily();
    var directory = new DirectoryInfo(Path.GetFullPath(outputDir));
    while (directory is not null)
    {
        if (map.TryGetValue(directory.Name, out var family))
        {
            return family;
        }
        directory = directory.Parent;
    }

    throw new InvalidOperationException(
        $"Could not resolve SDL2 family from output directory '{outputDir}'. " +
        $"Expected a path segment matching a config project_dir: {string.Join(", ", map.Keys)}.");
}
```

Update the `flags-detect` case: replace

```csharp
        var rosterPath = FlagsEnumRosterLoader.ResolveRosterPath();
        var family = ResolveFamilyFromOutputDir(outputDir);
        var allowList = FlagsEnumRosterLoader.LoadForFamily(rosterPath, family);
```

with

```csharp
        var family = ResolveFamilyFromOutputDir(outputDir, familyConfig);
        var allowList = familyConfig.FlagsAllowList(family);
```

- [ ] **Step 3: Replace opaque roster loading in the `uniform-opaque` case**

Replace

```csharp
        var rosterPath = ResolveOpaqueHandleRosterPath(inputDir);
        uniformOpaqueHandlesNamespace = ParseOptionValue(args, "--handles-namespace") ?? "SDL2";
        var family = UniformOpaqueFamilyIdentity.Resolve(outputDir, uniformOpaqueHandlesNamespace);
        var (rosterAutoDetect, rosterForceOpaque) = OpaqueHandleEmitRewriter.LoadRoster(rosterPath, family);
        var (familyAutoDetect, familyForceOpaque) = OpaqueHandleEmitRewriter.LoadFamilyOwnedRoster(rosterPath, family);
```

with

```csharp
        uniformOpaqueHandlesNamespace = ParseOptionValue(args, "--handles-namespace") ?? "SDL2";
        var family = UniformOpaqueFamilyIdentity.Resolve(outputDir, uniformOpaqueHandlesNamespace, familyConfig);
        var (rosterAutoDetect, rosterForceOpaque) = familyConfig.OpaqueHandles(family, includeCoreHandles: true);
        var (familyAutoDetect, familyForceOpaque) = familyConfig.OpaqueHandles(family, includeCoreHandles: false);
```

Update the later log line that prints `Path.GetFileName(rosterPath)` to print `"family-config.json"` literally (the path variable is gone).

- [ ] **Step 4: Delete the orphaned `ResolveOpaqueHandleRosterPath` local function** (Program.cs ~221–236) — no longer called.

- [ ] **Step 5: Make `UniformOpaqueFamilyIdentity.Resolve` config-driven**

Rewrite `UniformOpaqueFamilyIdentity.cs`:

```csharp
using Janset.SDL2.PostProcess.Config;

namespace Janset.SDL2.PostProcess;

internal static class UniformOpaqueFamilyIdentity
{
    public static string Resolve(string outputDir, string handlesNamespace, FamilyConfig config)
    {
        var normalized = Path.GetFullPath(outputDir).Replace('\\', '/');
        foreach (var (projectDir, family) in config.ProjectDirToFamily())
        {
            if (normalized.Contains($"/{projectDir}/", StringComparison.OrdinalIgnoreCase))
            {
                return family;
            }
        }

        if (config.NamespaceToFamily().TryGetValue(handlesNamespace, out var byNamespace))
        {
            return byNamespace;
        }

        throw new InvalidOperationException(
            $"Could not resolve family from output directory: {outputDir} or namespace '{handlesNamespace}'. " +
            "Expected a config project_dir path segment or a configured handles namespace.");
    }
}
```

- [ ] **Step 6: Make `UniformOpaqueOwnerMode` config-driven (remove the hardcoded substring)**

The explicit `--owner-mode` flag still wins. Replace the `IsOwnerDirectoryByPath` fallback so it consults config instead of the hardcoded `/Janset.SDL2.Core/` substring. Change the `Resolve` signature to accept the config and the resolved family, and rewrite `IsOwnerDirectoryByPath`:

```csharp
    public static bool Resolve(string[] arguments, string outputDirectory, FamilyConfig config, string family)
    {
        var flag = ParseFlag(arguments);
        if (flag is { } explicitMode)
        {
            Console.WriteLine($"uniform-opaque: owner-mode={(explicitMode ? "owner" : "consumer")} (explicit --owner-mode flag)");
            return explicitMode;
        }

        Console.WriteLine("uniform-opaque: WARNING — --owner-mode flag not provided; falling back to config owner_mode.");
        var fallback = config.OwnerMode(family);
        Console.WriteLine($"uniform-opaque: owner-mode={(fallback ? "owner" : "consumer")} (config fallback)");
        return fallback;
    }
```

Delete the `IsOwnerDirectoryByPath` method and its doc-comment. Update the call site in `Program.cs`:

```csharp
        uniformOpaqueIsOwner = UniformOpaqueOwnerMode.Resolve(args, outputDir, familyConfig, family);
```

- [ ] **Step 7: Edits complete — no green-build gate here**

These signature changes leave `PostProcessSelfTests.cs` referencing old signatures, so the project will not compile until Task 3.5. Do **not** run a green-build gate now. Optional sanity: `dotnet build` and confirm the *only* errors come from `PostProcessSelfTests.cs` (fixed in 3.5) — any error elsewhere means a mistake in this task. The authoritative build + Slopwatch is Task 3.6.

### Task 3.3: Move opaque/flags JSON parsing into FamilyConfig; retire the loaders

**Files:**

- Modify: `postprocess/OpaqueHandleEmitRewriter.cs`
- Delete: `postprocess/FlagsEnumRosterLoader.cs`

- [ ] **Step 1: Remove the JSON-roster methods from OpaqueHandleEmitRewriter**

`FamilyConfig.OpaqueHandles` now owns roster parsing. Delete `LoadRoster`, `LoadFamilyOwnedRoster`, and `AppendFamilyEntries` from `OpaqueHandleEmitRewriter.cs` (~lines 245–303). **Keep** `DiscoverAutoDetectedHandles`, `ReportDrift`, and `BuildHandlesFileContent` — those are rewrite mechanism, not config. Update `ReportDrift`'s final message (line ~342) to point at `config/family-config.json families.{family}` instead of `policy/opaque-handle-roster.json`.

- [ ] **Step 2: Delete `FlagsEnumRosterLoader.cs`**

It is fully replaced by `FamilyConfig.FlagsAllowList`. Remove the file. Confirm no other reference:

```pwsh
rg "FlagsEnumRosterLoader" spikes/binding-generators/clangsharp
```

Expected: matches remain only in `PostProcessSelfTests.cs` (lines ~470, ~624, ~711 call `FlagsEnumRosterLoader.ResolveRosterPath` / the test's own `ResolveRosterPath`) — those are updated in Task 3.5. No match should remain in `Program.cs` after Task 3.2 Step 2.

- [ ] **Step 3: Edits complete — no green-build gate here**

`PostProcessSelfTests.cs` still calls the removed loaders, so the project won't compile until Task 3.5. Authoritative build is Task 3.6.

### Task 3.4: Make ClongDualDispatchRewriter and PlatformDeltaPostProcessor config-driven

**Files:**

- Modify: `postprocess/ClongDualDispatchRewriter.cs`, `postprocess/PlatformDeltaPostProcessor.cs`, `postprocess/Program.cs`

- [ ] **Step 1: Feed `clong_methods` from config into the rewriter**

Replace the static `AffectedMethodNames` HashSet (lines 28–39) with an instance field set from the constructor. The native-type classification (`SDL_threadID`/`unsigned long`/`long`) at lines ~115–119 and ~334–364 **stays in code** (spec §3 / §5.4). Change:

```csharp
    private readonly HashSet<string> _affectedMethodNames;
    private readonly Mode _mode;

    public ClongDualDispatchRewriter(Mode mode, IEnumerable<string> affectedMethodNames)
    {
        _mode = mode;
        _affectedMethodNames = new HashSet<string>(affectedMethodNames, StringComparer.Ordinal);
    }
```

Replace every internal use of `AffectedMethodNames` with `_affectedMethodNames`. **Note:** `IsAffectedMethod(MethodDeclarationSyntax)` is currently `private static` (line ~108); drop `static` so it can read the instance field:

```csharp
    private bool IsAffectedMethod(MethodDeclarationSyntax method)   // static removed
    {
        if (!_affectedMethodNames.Contains(method.Identifier.ValueText)) ...
```

In `Program.cs` `clong-dispatch` case, resolve the family from the output directory (same helper `flags-detect` uses) and feed **that family's** `clong_methods`. Postprocess runs per library/family, so the rewriter scope is the family's own method list — not a global set:

```csharp
        var clongMode = ClongDualDispatchRewriter.DetectMode(inputDir);
        var family = ResolveFamilyFromOutputDir(outputDir, familyConfig);
        var clongMethods = familyConfig.ClongMethods(family);
        Console.WriteLine($"clong-dispatch: mode={clongMode}, family={family}, methods={clongMethods.Count}");
        var r = new ClongDualDispatchRewriter(clongMode, clongMethods);
```

This is the correct library-scoped model **and** byte-identical: a core tree only ever contains core's `clong_methods` (`SDL_ThreadID`, `SDL_GetThreadID`); a ttf tree only the `TTF_*` set. The old global HashSet matched only because no tree contained another family's symbols — per-family makes that explicit instead of relying on it.

- [ ] **Step 2: Feed platform views from config into PlatformDeltaPostProcessor**

Replace the static `PlatformOrder` array and `SupportedOsByPlatform` dictionary (lines 10–31) with instance fields populated from config. Add a constructor:

```csharp
    private readonly string[] _platformOrder;
    private readonly IReadOnlyDictionary<string, string> _supportedOsByPlatform;

    public PlatformDeltaPostProcessor(IReadOnlyList<Config.PlatformView> views)
    {
        _platformOrder = views.Select(v => v.Name).ToArray();
        _supportedOsByPlatform = views.ToDictionary(v => v.Name, v => v.SupportedOs, StringComparer.Ordinal);
    }
```

Replace internal `PlatformOrder`/`SupportedOsByPlatform` uses with the instance fields. In `Program.cs`, the `platform-delta` case becomes:

```csharp
if (mode == "platform-delta")
{
    var result = new PlatformDeltaPostProcessor(familyConfig.PlatformViews()).Process(inputDir, outputDir);
    ...
}
```

> **Order preservation:** `PlatformViews()` returns views in config document order, which the seed wrote in `SDL2_PLATFORM_VIEWS` order (`WindowsDesktop, WinRT, GDK, Linux, MacOS, IOS, Android`) — identical to the old hardcoded `PlatformOrder`. The parity verifier (Task 0.3) asserts this verbatim.

- [ ] **Step 3: Edits complete — green build deferred to Task 3.6**

All postprocess source edits (3.1–3.4) are now done, but `PostProcessSelfTests.cs` still uses old signatures. Proceed to Task 3.5 (update self-tests), then Task 3.6 runs the single authoritative build + Slopwatch + self-test gate.

### Task 3.5: Update C# self-tests

**Files:**

- Modify: `postprocess/PostProcessSelfTests.cs`

- [ ] **Step 1: Repoint config-dependent checks**

`CheckUniformOpaqueFamilyIdentity`, `CheckOpaqueHandleFamilyAwareness`, and `CheckFlagsAttributeDetection` previously exercised `UniformOpaqueFamilyIdentity.Resolve(outputDir, ns)`, `OpaqueHandleEmitRewriter.LoadRoster(...)`, and `FlagsEnumRosterLoader.LoadForFamily(...)`. Update them to:

- call `FamilyConfig.Load()` (the real repo config is reachable from the test's CWD),
- use `UniformOpaqueFamilyIdentity.Resolve(outputDir, ns, config)`,
- use `config.OpaqueHandles(family, includeCoreHandles: …)` and `config.FlagsAllowList(family)`.

Read each check body and apply the new signatures. Keep the behavioral assertions (e.g. Image pulls Core handles; `SDL_bool` is not flagged; `SDL_Keymod` is in the core allow-list).

Concrete call sites to fix (verified line numbers):
- `CheckOpaqueHandleFamilyAwareness` (line ~468) calls the test's own `ResolveRosterPath()` at line ~470 → replace with `FamilyConfig.Load()` + `config.OpaqueHandles(...)`.
- `CheckFlagsAttributeDetection` calls `FlagsEnumRosterLoader.ResolveRosterPath()` at lines ~624 and ~711 → replace with `FamilyConfig.Load()` + `config.FlagsAllowList(family)`.
- **Delete the orphaned `private static string ResolveRosterPath()` at line ~758** — it resolved `opaque-handle-roster.json`, which no longer exists; nothing should call it after the above.

- [ ] **Step 2: Run the C# self-test**

```pwsh
dotnet run --project spikes/binding-generators/clangsharp/postprocess/Janset.SDL2.PostProcess.csproj -c Release -- --self-test
```

Expected: `self-test: PASS`.

### Task 3.6: Full regen + isolation gate + commit

- [ ] **Step 1: Determinism gate**

```pwsh
python spikes/binding-generators/clangsharp/generate_bindings.py --family all --execute
git diff --ignore-cr-at-eol --stat -- "spikes/binding-generators/clangsharp/src"
```

Expected: empty.

- [ ] **Step 2: Build + Slopwatch + both self-tests**

```pwsh
dotnet build spikes/binding-generators/clangsharp/postprocess/Janset.SDL2.PostProcess.csproj -c Release
dotnet run --project spikes/binding-generators/clangsharp/postprocess/Janset.SDL2.PostProcess.csproj -c Release -- --self-test
python spikes/binding-generators/clangsharp/generate_bindings.py --self-test
slopwatch analyze --fail-on warning --exclude "artifacts/**,external/**,vcpkg_installed/**,**/bin/**,**/obj/**"
```

Expected: all clean / PASS.

- [ ] **Step 3: Checkpoint commit (work branch)**

```pwsh
git add spikes/binding-generators/clangsharp/postprocess/
git commit -m "wip(bindings): postprocess derives family identity, rosters, clong scope, platform views from config"
```

---

## Phase 4 — Delete retired sources, sweep stale references, finish docs

Goal: with every consumer reading config and the regen gate clean, retire the orphaned source files and complete documentation. Deletion is gated on a final parity pass.

### Task 4.1: Final parity gate before deletion

> **Why not re-run `config_migration.py --verify` here:** that tool imports `generate_bindings`'s `FAMILY_CONFIG` / `SDL2_PLATFORM_VIEWS` / `ALL_PLATFORM_MACROS`, which Phase 1 deleted — and after Phase 1 there is no independent source left to compare platform views against (config is the only copy). So **Phase 0 is the authoritative parity gate** (run while every source is live). Phase 4 proves parity still holds two ways: (a) the retired source files are byte-unchanged since the Phase 0 seed checkpoint commit, and (b) every regeneration shape produces byte-identical output.

- [ ] **Step 1: Assert the retired sources are untouched since parity passed**

The Phase 0 checkpoint (`wip(bindings): seed family-config.json + parity verifier`) is where `config_migration.py --verify` passed. Confirm nothing edited the retired sources since:
```pwsh
git diff --stat HEAD -- spikes/binding-generators/scope spikes/binding-generators/clangsharp/policy
```
Expected: empty (no phase ever modifies these files — they are read-only inputs until deletion). If the working tree somehow differs, stop: parity was only proven for the seeded content.

- [ ] **Step 2: Byte-identical across all regen shapes (all + image-only + core-only)**

Deniz's gate: the old files are deleted only after `--family all` **and** `--family image` (and `--family core`) all regenerate with no `.g.cs` change. The image-only run matters specifically because Image is the active satellite that pulls Core's handle names (`OpaqueHandles(..., includeCoreHandles: true)`), so it exercises the cross-family data pull that an all-run can mask.

```pwsh
python spikes/binding-generators/clangsharp/generate_bindings.py --family all --execute
git diff --ignore-cr-at-eol --stat -- "spikes/binding-generators/clangsharp/src"
python spikes/binding-generators/clangsharp/generate_bindings.py --family image --execute
git diff --ignore-cr-at-eol --stat -- "spikes/binding-generators/clangsharp/src"
python spikes/binding-generators/clangsharp/generate_bindings.py --family core --execute
git diff --ignore-cr-at-eol --stat -- "spikes/binding-generators/clangsharp/src"
python spikes/binding-generators/clangsharp/generate_bindings.py --family all --execute
git diff --ignore-cr-at-eol --stat -- "spikes/binding-generators/clangsharp/src"
```
Expected: **every** diff empty. (The final `--family all` restores the full tree after the single-family runs.) Byte-identical output across every shape is the strongest possible proof that config equals the files about to be deleted. Only proceed to Task 4.2 if all four diffs are clean.

### Task 4.2: Delete retired files

**Files:** delete 5 sources + the migration tool.

- [ ] **Step 1: Delete with `git rm` (preserves history)**

```pwsh
git rm spikes/binding-generators/scope/sdl2-core.headers.txt `
       spikes/binding-generators/scope/sdl2-image.headers.txt `
       spikes/binding-generators/scope/sdl2-core-sdlh-required.json `
       spikes/binding-generators/clangsharp/policy/opaque-handle-roster.json `
       spikes/binding-generators/clangsharp/policy/flags-enum-roster.json `
       spikes/binding-generators/clangsharp/config/config_migration.py
```

Then check whether `policy/` and `scope/` are now empty; if empty, they may be removed too (confirm nothing else lives there first with `ls`).

- [ ] **Step 2: Remove now-dead Python helpers that referenced deleted files**

In `generate_bindings.py`, `read_required_surface_allowlist` and `read_header_list` may now be unused (config supplies both). Check:

```pwsh
rg "read_required_surface_allowlist|read_header_list" spikes/binding-generators/clangsharp/generate_bindings.py
```

If a function is only referenced by its own definition and obsolete self-tests, delete the function and the obsolete self-test fixtures (the missing-scope-file sentinel test at ~1897–1911 references `scope/` and `read_header_list` — remove it; the scope dir is gone). Keep anything still used.

- [ ] **Step 3: Regen + self-tests to confirm deletion broke nothing**

```pwsh
python spikes/binding-generators/clangsharp/generate_bindings.py --self-test
python spikes/binding-generators/clangsharp/generate_bindings.py --family all --execute
git diff --ignore-cr-at-eol --stat -- "spikes/binding-generators/clangsharp/src"
dotnet run --project spikes/binding-generators/clangsharp/postprocess/Janset.SDL2.PostProcess.csproj -c Release -- --self-test
```

Expected: self-tests PASS; generated diff empty.

### Task 4.3: Stale-reference sweep (spec §9.11)

- [ ] **Step 1: Search for every retired path/name**

```pwsh
rg "opaque-handle-roster\.json|flags-enum-roster\.json|sdl2-core\.headers\.txt|sdl2-image\.headers\.txt|sdl2-core-sdlh-required\.json|FAMILY_CONFIG|SDL2_PLATFORM_VIEWS|ALL_PLATFORM_MACROS|PLATFORM_SENSITIVE_HEADERS|FlagsEnumRosterLoader" spikes/binding-generators docs
```

Expected after fixes: matches only in (a) this plan, (b) the spec doc (which records the migration), (c) historical notes explicitly marked stale, or (d) the Constitution's already-edited prose. **No active-policy reference may remain.** Fix any live doc (`spikes/binding-generators/README.md`, `docs/llm-handoff.md`, `docs/next-iteration-plan.md`) to reference `config/family-config.json`.

- [ ] **Step 2: Update spike docs that describe the config surface**

Edit `spikes/binding-generators/README.md`, `spikes/binding-generators/docs/llm-handoff.md`, and `spikes/binding-generators/docs/next-iteration-plan.md` to point at `config/family-config.json` and note the retired files. (Documentation-only — allowed without a code-approval gate, but still commit-gated.)

### Task 4.4: Verify + complete the already-applied Constitution/README edits

**Files:** `docs/binding-autogen/binding-generator-constitution.md`, `spikes/binding-generators/README.md`

- [ ] **Step 1: Confirm the uncommitted doc edits are correct and complete**

Review the existing `git diff` for these two files. Confirm:

- Determinism Input #6 now reads `config/family-config.json` (done).
- The "Configurable Scope Vs Policy Mechanism" section exists and matches the final config-vs-code boundary, including the C-long clause (config owns `clong_methods` only) (done — verify wording).
- Opaque-handle "Canonical roster" and `[Flags]` policy paragraphs point at `family-config.json` (done — verify).

- [ ] **Step 2: Fix the now-stale Constitution Determinism Input #8 (explicit edit)**

Input #8 still lists `FAMILY_CONFIG` / `PLATFORM_SENSITIVE_HEADERS` as orchestrator code facts, but Phase 1 deleted those constants. Edit Input #8 to read: *orchestrator code = `generate_bindings.py` (config loader, `selected_families`, pipeline order)* — no `FAMILY_CONFIG` / `PLATFORM_SENSITIVE_HEADERS` references. Re-grep afterward:
```pwsh
rg "FAMILY_CONFIG|PLATFORM_SENSITIVE_HEADERS" docs/binding-autogen/binding-generator-constitution.md
```
Expected: no active references (only historical prose explicitly marked as pre-Iteration-2, if any).

- [ ] **Step 3: Confirm the spec deltas from Task 0.0 Step 2 are present**

The oracle deferral (§9.6, items #17/#18, §7.1 row) and dormant-headers/order notes were applied to the spec in **Task 0.0 Step 2** (specs before scalpels). Just verify they are still present and consistent — do not re-edit.

### Task 4.5: Checkpoint commit Phase 4 (work branch)

- [ ] **Step 1: Commit deletions + doc updates**

```pwsh
git add -A spikes/binding-generators docs/binding-autogen
git commit -m "wip(bindings): retire roster/scope sources absorbed into family-config.json; refresh docs"
```

---

## Phase 5 — Final evidence battery (spec §8.4)

Goal: produce the closure evidence. No code changes; if any gate fails, return to the relevant phase under `superpowers:systematic-debugging`.

### Task 5.1: Determinism + idempotency

- [ ] **Step 1: Double-regen idempotency (§8.1.1)**

```pwsh
python spikes/binding-generators/clangsharp/generate_bindings.py --family all --execute
python spikes/binding-generators/clangsharp/generate_bindings.py --family all --execute
git diff --ignore-cr-at-eol --stat -- "spikes/binding-generators/clangsharp/src"
```

Expected: empty.

- [ ] **Step 2: Family isolation (§8.2.1)**

```pwsh
python spikes/binding-generators/clangsharp/generate_bindings.py --family core --execute
git diff --ignore-cr-at-eol --stat -- "spikes/binding-generators/clangsharp/src/Janset.SDL2.Image"
python spikes/binding-generators/clangsharp/generate_bindings.py --family all --execute
git diff --ignore-cr-at-eol --stat -- "spikes/binding-generators/clangsharp/src"
```

Expected: Image untouched by the core-only run; full regen leaves an empty diff.

### Task 5.2: Build / oracle / ABI / Slopwatch / self-tests (§8.4)

- [ ] **Step 1: Solution build (§8.4.1)**

```pwsh
dotnet build spikes/binding-generators/clangsharp/Janset.SDL2.ClangSharpSpike.slnx -c Release
```

Expected: 0 warnings, 0 errors.

- [ ] **Step 2: Oracle report (§8.4.2)**

The oracle is a file-based app (`oracle.cs`, run with `dotnet run --file`); families are named with the `sdl2-` prefix and listed individually (there is no `--family all`):

```pwsh
dotnet run --file spikes/binding-generators/clangsharp/oracle.cs -- --family sdl2-core --family sdl2-image --family sdl2-ttf --family sdl2-mixer --family sdl2-gfx --write-report
```

Expected: report written to `spikes/binding-generators/output/reports/oracle-evidence-clangsharp.md`; Core/Image clean; TTF/Mixer/GFX rows missing as expected. (`oracle.cs` is unchanged this iteration — deferred per delta 1.)

- [ ] **Step 3: AbiTests (§8.4.3) — `792/792` across net462/net8.0/net9.0/net10.0**

```pwsh
dotnet test spikes/binding-generators/clangsharp/tests/abi-tests/AbiTests.csproj -c Release
```

Expected: `792/792` across `net462`, `net8.0`, `net9.0`, `net10.0`.

- [ ] **Step 4: Both self-tests (§8.4.5) + Slopwatch (§8.4.4)**

```pwsh
python spikes/binding-generators/clangsharp/generate_bindings.py --self-test
dotnet run --project spikes/binding-generators/clangsharp/postprocess/Janset.SDL2.PostProcess.csproj -c Release -- --self-test
slopwatch analyze --fail-on warning --exclude "artifacts/**,external/**,vcpkg_installed/**,**/bin/**,**/obj/**"
```

Expected: PASS / PASS / 0 issues.

### Task 5.3: Exit-criteria checklist (spec §9, amended)

- [ ] Confirm each (amended) exit criterion: §9.1 (config populated, mechanically seeded — dormant headers empty per delta 4), §9.2 (8 parity assertions pass), §9.3 (no hardcoded `FAMILY_CONFIG`/platform constants in Python), §9.4 (postprocess identity config-derived; switch/if-chains gone from `Program.cs`/`UniformOpaque*`), §9.5 (clong from config; type classification in code), **§9.6 deferred**, §9.7 (platform views config-derived), §9.8 (5 files deleted post-parity), §9.9 (determinism+isolation gates), §9.10 (evidence gates), §9.11 (stale-reference sweep clean), §9.12 (Constitution/README updated — verify Input #8 fix landed).

### Task 5.4: Squash-land onto the spike branch (the single gated commit)

This is the **only** approval-gated commit. The work branch's WIP history is collapsed into one revertable changeset on `spike/binding-autogen-sdl2-gfx`.

- [ ] **Step 1: Present the full evidence summary to Deniz and get approval**

Summarize: determinism (all/image/core byte-clean), parity (Phase 0 verify), build 0/0, oracle Core/Image clean, AbiTests `792/792` × 4 TFMs, Slopwatch 0, both self-tests PASS, stale-reference sweep clean. Wait for explicit "go".

- [ ] **Step 2: Squash-merge the work branch into the spike branch**

On approval:
```pwsh
git switch spike/binding-autogen-sdl2-gfx
git merge --squash spike/iteration-2-config-unification
git commit -m "feat(bindings): unify config surface into family-config.json

Consolidate per-family identity, opaque/flags rosters, C-long method scope,
platform views, header inventories, and required surface into a single
config/family-config.json read by generate_bindings.py and the C# postprocess.
Removes FAMILY_CONFIG / platform constants from the orchestrator, the RSP
methodClassName/libraryPath duplication, and the hardcoded family switches in
postprocess. Retires 5 source files after parity + byte-identical regen proof.
Oracle identity dedup deferred (see plan delta 1). Generated output byte-identical."
```

- [ ] **Step 3: Verify the landing is one commit and the tree matches**

```pwsh
git log --oneline -1
git diff --stat spike/iteration-2-config-unification..HEAD
```
Expected: one new commit; empty diff (the squashed result equals the work-branch tip). The work branch may then be deleted (`git branch -D spike/iteration-2-config-unification`) once Deniz confirms.

- [ ] **Step 4: Write the closure note** in `docs/next-iteration-plan.md` / `llm-handoff.md` recording Iteration 2 closed, the landing commit sha, and the deferred oracle item.

---

## Self-review notes (author)

- **Spec coverage:** §2 inventory items 1–16, 19 are all migrated by Phases 1–3; items 17–18 (oracle) explicitly deferred (delta 1). §3 classification honored (clong type names, [Flags] suffix, GUID, Pattern B shape all stay in code). §6.1 RSP identity removal = Phase 2. §7.3 deletions = Phase 4. §8.1–8.4 gates = Phases 1/3/5. §9 exit criteria = Task 5.3 (amended). §10 endgame is informational.
- **Known risk — full regen requires the Windows + `vcpkg_installed` environment.** Work happens on a plain branch (`spike/iteration-2-config-unification`, no worktree per Deniz), so the provisioned checkout already has `vcpkg_installed`; regen gates run in place.
- **Commit model:** WIP checkpoint commits on the work branch (free), one approval-gated squash landing on `spike/binding-autogen-sdl2-gfx` (Task 5.4) ⇒ the iteration is a single revertable changeset.
- **Type consistency:** `FamilyConfig.OpaqueHandles(family, includeCoreHandles)` is the single roster accessor used by both Program.cs call sites; `PlatformView` record fields (`Name`/`SupportedOs`/`Defines`) match their uses in `PlatformDeltaPostProcessor`; `ResolveFamilyFromOutputDir(outputDir, config)` and `UniformOpaqueFamilyIdentity.Resolve(outputDir, ns, config)` carry the config consistently.
- **Phase 5 paths are concrete** (verified against the live tree): solution `spikes/binding-generators/clangsharp/Janset.SDL2.ClangSharpSpike.slnx`; oracle `dotnet run --file .../oracle.cs -- --family sdl2-core … --write-report` → `output/reports/oracle-evidence-clangsharp.md`; tests `.../tests/abi-tests/AbiTests.csproj` (TFMs `net10.0;net9.0;net8.0;net462`).
- **clong scope is per-family** (Task 3.4): the rewriter receives the resolved family's `clong_methods`, matching the per-library postprocess model. No global union.
- **No "minimal-touch" shortcuts:** roster JSON parsing is centralized in `FamilyConfig`; `FlagsEnumRosterLoader.cs` and `OpaqueHandleEmitRewriter`'s JSON loaders are removed, not left in place pointing at the new path.
