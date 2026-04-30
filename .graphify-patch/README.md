# graphify Patch

> **Scope of this doc:** patch internals only — what the patch does, why,
> safety semantics, manual verification. For project-level lifecycle
> (initial setup, post-commit hook, `--update` cadence, common queries,
> troubleshooting), see
> [`docs/playbook/graphify-runbook.md`](../docs/playbook/graphify-runbook.md).

graphify's `detect.py` has three hardcoded behaviors that hide content we
explicitly want in the knowledge graph for this repo:

| graphify behavior | What we lose without the patch |
| --- | --- |
| `_SKIP_DIRS` contains `"build"` | [build/](../build/) — Cake DDD host (ADR-002), `manifest.json` SSOT, MSBuild targets |
| `os.walk` prunes any dir starting with `.` | [.github/](../.github/) — workflows, CI/CD topology |
| `DOC_EXTENSIONS` only covers `.md/.mdx/.txt/.rst/.html` | `.yml`/`.yaml` workflows, `.json` configs (incl. `manifest.json`, `vcpkg.json`, `global.json`) |

`.graphifyignore` cannot un-skip any of these — it can only **add**
exclusions. The three-edit patch in `patch-graphify.ps1` removes the
hardcoded skips and widens the document classification so a single
`/graphify .` from the repo root captures the full project surface.

## Files

- [patch-graphify.ps1](patch-graphify.ps1) — applies / reverts the patch
- [README.md](README.md) — this file

## Usage

```powershell
# From PowerShell (Windows native):
./.graphify-patch/patch-graphify.ps1 patch
./.graphify-patch/patch-graphify.ps1 unpatch
```

```bash
# From Git Bash / WSL — invoke pwsh explicitly with -File:
pwsh -File ./.graphify-patch/patch-graphify.ps1 patch
pwsh -File ./.graphify-patch/patch-graphify.ps1 unpatch
```

## When to run

Re-run `patch` after **every** `pip install -U graphifyy` — the upgrade
overwrites `detect.py` and silently removes the patch. The script is
idempotent within a single graphify version (refuses to double-patch
because the backup already exists).

## What the patch does

Two literal string replacements inside the installed `detect.py`:

**Edit 1 — remove `"build"` from `_SKIP_DIRS`:**

```diff
-    "dist", "build", "target", "out",
+    "dist", "target", "out",
```

**Edit 2 — allow `.github` through the hidden-dir filter:**

```diff
-                    if not d.startswith(".")
+                    if (not d.startswith(".") or d == ".github")
```

Other dotfolders (`.idea`, `.claude`, `.vcpkg-cache`, `.logs`) are still
auto-skipped — only `.github` is whitelisted.

**Edit 3 — extend `DOC_EXTENSIONS` to cover yaml + json:**

```diff
-DOC_EXTENSIONS = {'.md', '.mdx', '.txt', '.rst', '.html'}
+DOC_EXTENSIONS = {'.md', '.mdx', '.txt', '.rst', '.html', '.yml', '.yaml', '.json'}
```

This lets graphify ingest CI workflows (`.github/workflows/*.yml`) and
the build SSOT (`build/manifest.json`, `vcpkg.json`, `global.json`) as
document nodes for semantic extraction. Noisy JSON (`packages.lock.json`,
generated CMake presets) is filtered via `.graphifyignore`.

## Safety

- `patch` writes `detect.py.bak` next to `detect.py` before mutating.
- `patch` refuses if the backup already exists (treats that as "you're
  already patched").
- `patch` refuses if either expected pre-patch marker is missing
  (defensive against graphify upstream drift — bail before touching
  anything).
- After both `patch` and `unpatch`, the script re-reads the file from
  disk and verifies the edit landed exactly. Any verification failure
  throws.

## Manual verification

```bash
python -c "import graphify.detect as d; print('build skip removed:', 'build' not in d._SKIP_DIRS)"
# Patched:    build skip removed: True
# Unpatched:  build skip removed: False

python -c "import graphify.detect as d; print('yaml recognized:', '.yaml' in d.DOC_EXTENSIONS)"
# Patched:    yaml recognized: True
# Unpatched:  yaml recognized: False
```

## Why this folder is hidden (`.graphify-patch/`)

graphify's own scanner skips any directory starting with `.` — the very
behavior we patch around. Naming this folder with a leading dot keeps
the patch tooling out of every graph generated from this repo without
needing a `.graphifyignore` entry.

## Why we patch instead of running multiple graphify invocations

Running `/graphify build/_build` and `/graphify .github` separately
works without any patching, but produces three disconnected graphs.
Cross-cutting connections — e.g. a `Tasks/PackageTask.cs` ↔
`manifest.json` ↔ `.github/workflows/release.yml` triangle — only show
up under community detection when all three corpora live in **one**
graph. That cohesion is the whole reason this repo wants graphify in the
first place.
