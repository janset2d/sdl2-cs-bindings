# Onboarding — Janset.SDL2 / Janset.SDL3

> First stop for human contributors and LLM agents entering this repo.

## What This Project Is

Modular C# bindings for **SDL2** (and upcoming **SDL3**), bundled with **cross-platform native libraries built from source via vcpkg**, distributed as **NuGet packages**.

Foundation layer for **Janset2D**, a cross-platform 2D game framework, but designed as a fully independent open-source project.

## Why This Project Exists

Existing .NET SDL bindings (SDL2-CS, ppy/SDL3-CS, Alimer.Bindings.SDL, etc.) provide P/Invoke declarations but **none of them ship cross-platform native binaries built from source via a reproducible pipeline**. Users are expected to source their own SDL natives.

Janset.SDL2/SDL3 fills this gap by:

1. Providing C# bindings (currently SDL2-CS imports; auto-generated planned for Phase 4).
2. Building natives from source using **vcpkg** across 7 RIDs.
3. Packaging into modular NuGet packages with proper `runtimes/{rid}/native/` layout.
4. Handling Linux/macOS symlink preservation via tar.gz archives with consumer-side extraction targets.

No other project in the ecosystem does all four.

## Who Maintains This

**Deniz Irgin** (@denizirgin) — senior .NET developer based in Istanbul. Bilingual (Turkish + English).

Communication: prefers comprehensive solutions over quick hacks; expects challenge and reasoning, not yes-person behavior; conversational tone with practical humor.

## SDL Libraries in Scope

- **SDL2** (priority): SDL2, SDL2_image, SDL2_mixer, SDL2_ttf, SDL2_gfx, SDL2_net (binding pending).
- **SDL3** (future, after SDL2 ships): SDL3, SDL3_image, SDL3_mixer, SDL3_ttf. SDL3_net not yet in vcpkg (upstream WIP).

## NuGet Package Topology

Users reference the managed package; the `.Native` dependency is pulled transitively.

```text
Janset.SDL2                              ← Meta-package (not yet shipped — #61)
├── Janset.SDL2.Core                     ← Managed bindings
│   └── Janset.SDL2.Core.Native          ← Native SDL2 binaries (all RIDs)
├── Janset.SDL2.Image
│   └── Janset.SDL2.Image.Native
├── Janset.SDL2.Mixer
│   └── Janset.SDL2.Mixer.Native
├── Janset.SDL2.Ttf
│   └── Janset.SDL2.Ttf.Native
├── Janset.SDL2.Gfx
│   └── Janset.SDL2.Gfx.Native
└── Janset.SDL2.Net (Phase 3 — #58)
    └── Janset.SDL2.Net.Native
```

Same shape for SDL3 when it lands (Phase 5).

## Where to Go Next

| If you are... | Read |
| --- | --- |
| LLM/agent entering for the first time | [AGENTS.md](../AGENTS.md) — operating rules + approval gate + settled decisions |
| Contributor wanting current status / roadmap | [plan.md](plan.md) |
| Working on the active phase | [phases/phase-2-adaptation-plan.md](phases/phase-2-adaptation-plan.md) |
| Exploring how the project is organized | [README.md](README.md) — full doc map |
| Setting up locally | [playbook/local-development.md](playbook/local-development.md) |
| Adding a new SDL satellite | [playbook/adding-new-library.md](playbook/adding-new-library.md) |
| Touching vcpkg overlays | [playbook/overlay-management.md](playbook/overlay-management.md) |
| Bumping vcpkg baseline | [playbook/vcpkg-update.md](playbook/vcpkg-update.md) |
| Validating against guardrails | [knowledge-base/release-guardrails.md](knowledge-base/release-guardrails.md) |

For settled strategic decisions (versioning, packaging strategy, hybrid-static, LGPL-free codecs, etc.), see the **Settled Strategic Decisions** table in [AGENTS.md](../AGENTS.md).

## Non-Goals

- **Not a game engine**: this is a binding/packaging layer. Engine logic belongs in Janset2D (separate future repo).
- **Not a high-level SDL wrapper**: raw P/Invoke bindings, no OOP abstraction.
- **Not a tutorial project**: production infrastructure, samples will exist but documentation isn't a learning resource.
- **Not cross-language**: C#/.NET only. No C++, Rust, or Python bindings.

## Glossary

| Term | Meaning |
| --- | --- |
| **RID** | Runtime Identifier — .NET's platform descriptor (e.g., `win-x64`, `linux-arm64`) |
| **Triplet** | vcpkg's platform descriptor (e.g., `x64-windows-hybrid`). Encodes packaging strategy in the name. |
| **Harvest** | Process of collecting compiled native binaries + transitive deps from vcpkg output |
| **Binary Closure Walk** | Recursively scanning a binary's dependencies (dumpbin on Windows, ldd on Linux, otool on macOS) |
| **Satellite Library** | SDL companion libraries: SDL_image, SDL_mixer, SDL_ttf, SDL_gfx, SDL_net |
| **Package Family** | Release unit: one managed package + its `.Native` package, always versioned and released together (e.g., `sdl2-core` family = `Janset.SDL2.Core` + `Janset.SDL2.Core.Native`) |
| **Family Identifier** | Canonical `sdl<major>-<role>` string used in `manifest.json`, MinVer tag prefix, and git tags |
| **Core Family** | The SDL family's core package: `sdl2-core` for SDL2, `sdl3-core` (future) for SDL3. Released first when multiple families release together. |
| **Satellite Family** | Any non-core package family within an SDL major-version line. Depends on the core family of the same line. |
| **Family Version** | Single shared version string for both packages in a family. Shape: `<UpstreamMajor>.<UpstreamMinor>.<FamilyPatch>` (D-3seg). |
| **Targeted Release** | Release of specific families without touching others. The default release mode. |
| **Full-Train Release** | Coordinated release of all families together, triggered by cross-cutting changes. |
| **SONAME** | Shared Object Name — versioned name Linux shared libraries link against (e.g., `libSDL2-2.0.so.0`) |
| **buildTransitive** | NuGet targets that apply to consuming projects (not just direct references) |
| **Cake Frosting** | C#-based build automation framework (strongly-typed, DI-enabled) |
