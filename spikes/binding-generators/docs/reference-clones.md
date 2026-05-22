# Reference Clone Workflow

Reference repositories are local evidence, not vendored dependencies.

Clone commands from the repository root:

```pwsh
if (-not (Test-Path -LiteralPath "spikes/binding-generators")) { throw "spikes/binding-generators was not found" }
New-Item -ItemType Directory -Force -Path "spikes/binding-generators/references"
git clone https://github.com/ppy/SDL3-CS.git "spikes/binding-generators/references/ppy-SDL3-CS"
git clone https://github.com/amerkoleci/Alimer.Bindings.SDL.git "spikes/binding-generators/references/alimer-bindings-sdl"
```

Use these repositories only for comparison and selective spike copying. Do not add them as submodules and do not commit files under `references/`.

Evidence to inspect:

- `ppy-SDL3-CS/SDL3-CS/generate_bindings.py`
- `ppy-SDL3-CS/SDL3-CS/*.rsp`
- `alimer-bindings-sdl/src/Generator/`
