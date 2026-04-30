# graphify Knowledge Graph — Runbook

This repo maintains a persistent code + docs knowledge graph at
`graphify-out/graph.json` (~2100 nodes / ~2800 edges across 252
communities). It is the preferred entry point for cross-cutting
architectural questions; grep is the fallback.

This runbook covers project-level lifecycle. The patch internals
(why we patch, what edits are made, safety, manual verification) are
authoritative in [`.graphify-patch/README.md`](../../.graphify-patch/README.md).

## When to query vs. when to grep

Query the graph (`/graphify query "<question>"`) when:

- You need to know what a symbol connects to across many files.
- You're tracing a flow between two named concepts.
- You want the architectural community a node belongs to.
- You're looking for non-obvious bridges between unrelated areas.

Use grep when:

- You need every literal occurrence of a string.
- You're looking inside a known file or directory.
- The graph is stale relative to the area you're asking about (run
  `--update` first if recent doc edits in that area haven't been merged
  into the graph).

## Initial setup (once per fresh checkout)

```bash
# 1. Install graphify into the active Python interpreter
pip install graphifyy

# 2. Patch site-packages so build/ + .github/ + yaml/json are scannable
pwsh -File .graphify-patch/patch-graphify.ps1 patch

# 3. Initial graph build (~200K LLM tokens, one-time cost)
/graphify .

# 4. Install the post-commit hook so code changes update the graph for free
graphify hook install
```

After step 4, every `git commit` re-runs AST extraction on the changed
`.cs` / `.ps1` files and rebuilds `graph.json` + `GRAPH_REPORT.md`.
**No LLM tokens, no manual command.**

## Maintenance cadence

| Change type | Action | Cost |
| --- | --- | --- |
| `.cs` / `.ps1` source code | Auto — post-commit hook handles it | Zero (AST only) |
| `.md` docs (incl. ADRs) | Manual `/graphify . --update` after a session | ~10–30K tokens depending on extent |
| `manifest.json` / `vcpkg.json` / workflow `.yml` | Manual `/graphify . --update` | ~10K tokens (1–2 chunks) |
| Adding a new top-level folder | Update [`.graphifyignore`](../../.graphifyignore), then `--update` | Varies |
| `pip install -U graphifyy` | Re-run `patch-graphify.ps1 patch` | Zero |

**Rule of thumb for docs:** batch your edits, run `--update` once at
session end. Don't update on every doc-only commit — wasted tokens
because the per-file cache only saves work, it doesn't make extraction
free.

## Common queries

```bash
# Architectural reasoning (BFS — broad context)
/graphify query "how does HarvestTask reach NativeSmokeRunner"
/graphify query "what depends on IPathService"

# Path tracing between two concepts
/graphify path "PackageTaskRunner" "manifest.json"

# Plain-language explanation of a single node
/graphify explain "PackageOutputValidator"

# Re-cluster without re-extracting (cheap, takes seconds)
/graphify . --cluster-only

# Open the interactive graph in a browser
# (graph.html is regenerated on every full run)
start graphify-out/graph.html
```

`graphify-out/GRAPH_REPORT.md` is the human-readable audit. It lists
god nodes, surprising connections, weakly-connected components, and
suggested questions — useful for periodic architecture review.

## Doc-only update flow

```bash
# After editing ADRs, manifest, workflows, etc.
/graphify . --update
```

graphify uses a per-file cache (`graphify-out/cache/`). Only changed
files trigger LLM extraction; everything else replays from cache.
Typical 1–2 doc edit costs <20K tokens, not the full 200K of a fresh
build.

## Troubleshooting

**"Marker not found" during `patch`** — graphify upstream changed shape.
Inspect the literal markers in `.graphify-patch/patch-graphify.ps1` vs.
the current installed `detect.py`. Update markers in the script if
needed; do **not** silently force the patch.

**Backup already exists** — `detect.py.bak` is present, so the script
assumes you're already patched. Run `unpatch` first, or delete the
backup manually if the backup itself is wrong.

**Unicode/encoding error in benchmark or report** — set
`PYTHONIOENCODING=utf-8` before invoking; Windows cp1252 default trips
on box-drawing characters used by graphify's report formatting.

**Graph is stale** — for code, ensure the post-commit hook is installed
(`graphify hook status`). For docs / manifest / workflows, run
`/graphify . --update`.

**`build/` or `.github/` content missing from the graph** — patch is
not applied; run `pwsh -File .graphify-patch/patch-graphify.ps1 patch`.

**Graph empty after build** — extraction probably failed. Re-read
`/graphify` skill output and check the chunk JSON files in
`graphify-out/`. If subagents were dispatched as read-only (Explore
type), the chunk JSONs won't exist on disk; rerun with the
general-purpose subagent type.

## What's intentionally NOT in the graph

- `.csproj`, `.props`, `.targets`, `.cmake` — XML/CMake structural
  extensions are not in graphify's classification. MSBuild glue is
  invisible. If this becomes a real architectural blind spot, extend
  the patch script with another `DOC_EXTENSIONS` edit.
- Most of `docs/` — only `docs/decisions/` (ADRs) is graphed; the rest
  churns too fast to be worth indexing. See
  [`.graphifyignore`](../../.graphifyignore) for the exact list.
- `external/` (sdl2-cs + vcpkg vendor) — transitional, not part of
  project architecture per AGENTS.md settled decisions.
- `bin/`, `obj/`, `vcpkg_installed/`, `artifacts/`, `cmake-build-*` —
  generated outputs, no semantic value.

## Why this exists

For a repo this size (~2100 graph nodes, 252 communities, DDD-layered
Cake host with cross-cutting AGENTS.md / ADR / manifest / workflow
connections), persistent structural memory beats per-session grep. The
graph survives across LLM sessions and surfaces relationships nobody
would think to grep for (community bridges, weakly-connected node
sets).

For smaller repos this is overkill — read the whole thing into context
and skip the graph.
