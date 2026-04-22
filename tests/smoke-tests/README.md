# Smoke Tests

> Post-pipeline integrity checks for the Janset.SDL2 stack. The symmetric counterpart to PreFlight — where PreFlight validates structural integrity **before** work starts, smoke tests validate runtime behaviour **after** build artifacts and packages are produced.

## What Lives Here

| Subtree | Language | What it proves | Runner |
| --- | --- | --- | --- |
| [`native-smoke/`](native-smoke/) | C++ / CMake | Hybrid-built native SDL libraries load and initialize at runtime. 13-case coverage across SDL2 + satellites (PNG/JPG decoding, mixer init, TTF init, GFX primitives). Tests **natives directly**, no P/Invoke layer. | CMake via `ctest` / platform build scripts |
| [`package-smoke/`](package-smoke/) | C# / .NET | Shipping graph end-to-end: `PackageReference` restore from local folder feed → native assets land in `bin/` → `SDL_Init()` + `IMG_Linked_Version()` succeed. Per-TFM validation (executable TFMs only — netstandard2.0 compile sanity separate). | `dotnet` CLI driven by Cake `PostFlight` task |

## Why a Dedicated Section

A full integrity story has three stages:

1. **PreFlight** — structural config validation (manifest ↔ vcpkg coherence, csproj pack contract, strategy resolution). No code runs. Lives in Cake `PreFlightCheck` task.
2. **Build & Pack** — actual build and package production. Cake `Harvest`/`Package` tasks.
3. **PostFlight (smoke-tests)** — after artifacts exist, confirm they actually work when consumed the way a downstream user will consume them.

Without smoke, PreFlight passing and a clean build can still ship a package that fails on `SDL_Init`. Smoke closes that gap.

## Naming Convention

Subfolders follow the kebab-case `<subject>-smoke/` pattern:

- `native-smoke/` — "smoke of the natives"
- `package-smoke/` — "smoke of the packaged consumer path"

Future siblings (e.g., `cli-smoke/`, `sample-smoke/`) should stick to this pattern — same form, same narrative: **this folder smokes X**.

## Orchestration

Cake owns the orchestration surface. The umbrella task is `PostFlight`:

```text
PostFlight
  ├── PackageConsumerSmoke    (package-smoke invocation)
  └── ... (native-smoke .NET binding coverage and other sanity gates as they land)
```

Run explicitly:

```bash
dotnet run --project build/_build/Build.csproj -- --target PostFlight --family sdl2-core --family sdl2-image --family-version <semver>
```

Native-smoke's C++ runs today via its own `build.bat` / `cmake --build` flow — see [`native-smoke/README.md`](native-smoke/README.md). Weaving it into Cake `PostFlight` is [docs/plan.md](../../docs/plan.md) 2b work.

For IDE or direct-CLI validation, run `SetupLocalDev --source=local` once first. It writes `build/msbuild/Janset.Local.props`, after which `PackageConsumer.Smoke.csproj` restores and builds directly without runner-injected package properties.

## What This Section Is NOT

- **Not unit tests.** Those live in `build/_build.Tests/` and exercise the Cake build host in isolation (fake file systems, synthetic manifests, composition-root wiring).
- **Not samples.** Samples (future `samples/` tree) are consumer-facing documentation. Smoke tests are **internal integrity gates** that may or may not have pedagogical value.
- **Not a separate source-graph validation lane.** ADR-001 retired Source Mode as a supported consumer contract. Smoke tests validate the canonical package-first path; if a throwaway binding-debug harness is ever needed later, it lives outside this directory.

## Relationship to Cross-Platform Smoke Validation Matrix

[`docs/playbook/cross-platform-smoke-validation.md`](../../docs/playbook/cross-platform-smoke-validation.md) catalogs the checkpoints (A-G active, H-L planned). The checkpoints in this directory:

- **G** — native-smoke C++ runtime test (active on 3 platforms)
- **K** — package-smoke consumer test (planned; active on win-x64 only for Phase 2a proof slice)

Promotion to "active on all 3 platforms" for K is Phase 2b work, gated on Unix `buildTransitive/*.targets` landing (tar.gz extraction for Linux/macOS package consumers).
