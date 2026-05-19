# Binding Auto-Generation Workstream

**Status (2026-05-20):** active binding-generator workstream. The canonical documents are now the constitution and roadmap below. Historical specs, implementation transcripts, and spike research were folded for unique current facts and removed from active docs; git history remains the archive.

The generator is hosted inside the Cake build host under `build/_build/Targets/GenerateBindings/`, runs Linux-canonical from the pinned builder container, and is configured per family through `build/manifest.json library_manifests[].binding_generation`. SDL2.Core currently has a generated internal ABI-shaped preview; the next work is public typed wrappers, friendly overloads, production source flip, and package-first smoke.

## Folder Layout

| Path | Purpose |
| --- | --- |
| [`README.md`](README.md) | Workstream index and reading order. |
| [`binding-generator-constitution.md`](binding-generator-constitution.md) | Canonical binding-generator constitution: internal ABI, public API layers, C-to-C# translation, manifest config vs policy, and evidence gates. |
| [`binding-generator-roadmap.md`](binding-generator-roadmap.md) | Canonical future roadmap: remaining SDL2.Core work, SDL2 satellite sweep, and SDL3 extension. |
| [`../playbook/binding-generator-maintenance.md`](../playbook/binding-generator-maintenance.md) | In-progress maintenance playbook for platform macro catalogs, generated stamps, and overlay coupling. |
| [`../playbook/binding-output-oracle-validation.md`](../playbook/binding-output-oracle-validation.md) | Reusable multi-agent review workflow for validating generated output against official SDL sources, peer bindings, and .NET API evidence. |
| [`../decisions/2026-05-14-binding-autogen-toolchain.md`](../decisions/2026-05-14-binding-autogen-toolchain.md) | ADR-004 CppAst toolchain decision and migration-door rationale. |

## Reading Order

| # | Document | Purpose |
| --- | --- | --- |
| 1 | [`binding-generator-constitution.md`](binding-generator-constitution.md) | Read first. It defines the binding generator's ABI/API law and evidence gates. |
| 2 | [`binding-generator-roadmap.md`](binding-generator-roadmap.md) | Read second. It tracks only future work. |
| 3 | [`../playbook/binding-generator-maintenance.md`](../playbook/binding-generator-maintenance.md) | Operational procedure for parser config, synthetic headers, stamps, upstream bumps, and hybrid-static coupling. |
| 4 | [`../playbook/binding-output-oracle-validation.md`](../playbook/binding-output-oracle-validation.md) | Output validation workflow against SDL headers, peer bindings, and .NET API evidence. |
| 5 | [`../decisions/2026-05-14-binding-autogen-toolchain.md`](../decisions/2026-05-14-binding-autogen-toolchain.md) | Toolchain ADR. Read only when reopening CppAst vs ClangSharp or bumping the trio. |

Historical research and task-by-task plans were intentionally removed from active docs. Preserve new durable facts in the constitution or roadmap instead of reviving one-off plan files.

## Current Decision Posture

- CppAst remains selected, recorded in [ADR-004](../decisions/2026-05-14-binding-autogen-toolchain.md).
- Public API shape is **internal raw ABI externs + public typed low-level API + friendly overloads** per [`binding-generator-constitution.md`](binding-generator-constitution.md). Public raw `IntPtr` externs are not part of v1 preview; typed handles expose native values as the escape hatch. SDL2-CS compatibility is best-effort: useful as an oracle, not the shape to freeze.
- Output class identity is family-based, manifest-driven, and not parse-view-based. SDL2 core currently uses namespace `SDL2`, public class `SDL`, and internal raw ABI class `SDLNative`; parse views produce files/attributes, not `Sdl2_Neutral` or `Sdl2_MacOS` classes.
- String-like SDL macro constants such as `SDL_HINT_*` use canonical `ReadOnlySpan<byte>` UTF-8 literal properties. String ergonomics is provided by method overloads; do not duplicate every macro as both `const string` and `ReadOnlySpan<byte>`.
- **Generator lives inside the Cake build host** under `build/_build/Targets/GenerateBindings/` with cross-cutting validators under `build/_build/Validation/BindingGeneration/`. No standalone `src/`-tree console app. Pure emitter code stays Cake-free; the Cake-aware shell owns orchestration.
- **Linux-canonical** generation. Only `libclang.runtime.linux-x64` + `libClangSharp.runtime.linux-x64` are pinned. The `GenerateBindings` Cake target fails closed on non-`linux-x64` hosts. Local invocation routes through `tools.cs generate-bindings`, which orchestrates the pinned `linux-builder` Docker container — Docker is a hard prerequisite, no host-OS fallback.
- **Preprocessor-macro switching only** for platform passes. No `--target` cross-compile flag, no mingw-w64, no Apple SDK headers. SDL's public headers carry the cross-platform opaque-type forward declarations the parser needs. Verified against ppy/SDL3-CS Dockerfile + `generate_bindings.py` (WebFetch 2026-05-15) and the local CppAst spike.
- **`SDL_syswm.h` typed-union layout is Stage 2.** Stage 1 emits `SDL_GetWindowWMInfo` as a function with opaque `SDL_SysWMinfo*` parameter. The typed union with `[StructLayout(LayoutKind.Explicit, Size = 64)]` plus the small forward-declaration stub library (~15–20 types) lands in Stage 2.
- **SDL3 binding generation is gated on PD-7** (SDL2 real-public-release). The SDL3 vcpkg port + overlay triplet work is its own substantial scope and must not block SDL2 v1.0 stable.
- ClangSharp remains the documented migration path if CppAst's version-trio coupling or owned-emitter cost becomes painful in practice.
- ppy/SDL3-CS is the strongest SDL-specific reference for neutral + platform-specific passes. SkiaSharp is a strong CppAst discipline reference, but not a platform-split reference. Alimer is useful for C# shape ideas, but its single-pass union macro strategy is not sufficient for platform-conditioned headers/layout across our 7-RID correctness bar.
- Structural emission is generic AST-driven work. Name allowlists such as `Stage1StructNames` and `SDL_GameControllerButtonBind`-specific flattening are not canonical design; anonymous unions are translated from AST shape. `SDL_GUID` is explicitly mapped to `System.Guid` through substitution policy and is not emitted as a generated struct.

## Related Canonical Docs

- [`../phases/phase-4-binding-autogen.md`](../phases/phase-4-binding-autogen.md) — Phase 4 design brief.
- [`../release-strategy.md`](../release-strategy.md) — AST-first sequencing and v1.0 release strategy.
- [`../plan.md`](../plan.md) — roadmap and current phase status.
- [`../decisions/2026-05-05-target-centric-build-host.md`](../decisions/2026-05-05-target-centric-build-host.md) — ADR-002 target-centric build-host pattern the Cake-host fold inherits.
- [`../decisions/2026-05-12-build-host-data-layer.md`](../decisions/2026-05-12-build-host-data-layer.md) — ADR-003 contract-centric data layer pattern the binding validators extend.
- [`../decisions/2026-05-14-binding-autogen-toolchain.md`](../decisions/2026-05-14-binding-autogen-toolchain.md) — ADR-004 binding-autogen toolchain.
- [`../knowledge-base/testing-guidelines.md`](../knowledge-base/testing-guidelines.md) — canonical TUnit/MTP test infrastructure for generator tests.
- [`../knowledge-base/extraction-guidelines.md`](../knowledge-base/extraction-guidelines.md) — collaborator extraction discipline.
- [`../../AGENTS.md`](../../AGENTS.md) — operating rules and settled project decisions.
