# Packaging Strategy Synthesis - 2026-04-13 (Copilot)

**Date:** 2026-04-13
**Author:** GitHub Copilot (GPT-5.4)
**Status:** Working synthesis for decision support
**Scope:** Native packaging strategy, build-host evolution, local development model, package-validation model, temp-doc audit

---

## 1. Purpose

This document is the consolidated research report for the current SDL2 packaging decision point.

It is intended to do three things in one place:

1. Summarize what was actually validated in the repo and in supporting tooling docs.
2. Separate strong findings from assumptions and over-assertive temp-doc claims.
3. Give a decision-ready recommendation before any canonical docs or phases are redesigned.

This report is more comprehensive than the other temp docs and should be treated as the primary entry point for the folder.

---

## 2. Executive Summary

### Recommended strategy

**Choose Hybrid Static + Dynamic Core as the target direction, but implement it pragmatically rather than in the most maximalist form described by the temp drafts.**

### The short version

1. Keep SDL2 core dynamic and separately packaged.
2. Push permissive, collision-prone transitive dependencies inward as far as practical.
3. Keep LGPL-sensitive `SDL2_mixer` extras opt-in and dynamic.
4. Treat Source Mode and Package Validation Mode as different truths.
5. Stop treating tracked `src/native/**/runtimes` payloads as the long-term source of truth.
6. Do **not** assume wrapper or bundle DLL projects are mandatory for every satellite by default, but do treat package-consumer validation and symbol/export-boundary validation as hard gates before ruling them out.

### Why this is the recommendation

The repo evidence does not support staying on a pure-dynamic packaging model as the default end state.

Native asset flattening in NuGet output, overlapping native basenames, and the already-observed drift between harvested payloads and tracked package payloads make the current direction fragile.

At the same time, the evidence also does **not** justify some of the most aggressive implementation claims in [packaging-strategy-verdict-2026-04-13-gemini.md](packaging-strategy-verdict-2026-04-13-gemini.md) and [packaging-strategy-verdict-2026-04-13-grok.md](packaging-strategy-verdict-2026-04-13-grok.md), especially the idea that hybrid automatically requires a new wrapper native project for every non-core SDL library. That should be read as a conditional implementation warning, not as proof that wrapper scope, loader behavior, or symbol/export boundaries are already safe.

---

## 3. Questions This Research Set Out To Answer

The work focused on these questions:

1. Is Pure Dynamic still a viable default packaging model for this repo?
2. Is Hybrid Static + Dynamic Core technically feasible with vcpkg, NuGet, and current .NET loading behavior?
3. Which parts of the temp strategy docs are validated by the actual codebase?
4. Which parts are still hypotheses or overstatements?
5. If Hybrid is chosen, how big does the Phase 2 redesign really need to be?

---

## 4. Evidence Base

## 4.1 Canonical docs reviewed

- [../../onboarding.md](../../onboarding.md)
- [../../../AGENTS.md](../../../AGENTS.md)
- [../../plan.md](../../plan.md)
- [../../phases/README.md](../../phases/README.md)
- [../license-inventory-2026-04-13.md](../license-inventory-2026-04-13.md)
- [../packaging-strategy-hybrid-static-2026-04-13.md](../packaging-strategy-hybrid-static-2026-04-13.md)
- [../packaging-strategy-pure-dynamic-2026-04-13.md](../packaging-strategy-pure-dynamic-2026-04-13.md)

## 4.2 Temp docs reviewed

- [packaging-strategy-verdict-2026-04-13-shared.md](packaging-strategy-verdict-2026-04-13-shared.md)
- [execution-model-strategy-2026-04-13-shared.md](execution-model-strategy-2026-04-13-shared.md)
- [packaging-strategy-verdict-2026-04-13-gemini.md](packaging-strategy-verdict-2026-04-13-gemini.md)
- [packaging-strategy-verdict-2026-04-13-grok.md](packaging-strategy-verdict-2026-04-13-grok.md)

## 4.3 Build-host and repo implementation reviewed

- [../../../build/_build/Program.cs](../../../build/_build/Program.cs)
- [../../../build/_build/Tasks/Harvest/HarvestTask.cs](../../../build/_build/Tasks/Harvest/HarvestTask.cs)
- [../../../build/_build/Tasks/Harvest/ConsolidateHarvestTask.cs](../../../build/_build/Tasks/Harvest/ConsolidateHarvestTask.cs)
- [../../../build/_build/Tasks/Preflight/PreFlightCheckTask.cs](../../../build/_build/Tasks/Preflight/PreFlightCheckTask.cs)
- [../../../build/_build/Context/Options/VcpkgOptions.cs](../../../build/_build/Context/Options/VcpkgOptions.cs)
- [../../../build/_build/Modules/PathService.cs](../../../build/_build/Modules/PathService.cs)
- [../../../build/_build/Modules/Harvesting/ArtifactPlanner.cs](../../../build/_build/Modules/Harvesting/ArtifactPlanner.cs)
- [../../../vcpkg.json](../../../vcpkg.json)
- [../../../build/manifest.json](../../../build/manifest.json)
- [../../../src/native/Directory.Build.props](../../../src/native/Directory.Build.props)
- [../../../src/SDL2.Core/SDL2.Core.csproj](../../../src/SDL2.Core/SDL2.Core.csproj)
- [../../../src/SDL2.Mixer/SDL2.Mixer.csproj](../../../src/SDL2.Mixer/SDL2.Mixer.csproj)
- [../../../src/native/SDL2.Core.Native/buildTransitive/Janset.SDL2.Core.Native.targets](../../../src/native/SDL2.Core.Native/buildTransitive/Janset.SDL2.Core.Native.targets)
- [../../../test/Sandboc/Program.cs](../../../test/Sandboc/Program.cs)
- [../../../.github/actions/vcpkg-setup/action.yml](../../../.github/actions/vcpkg-setup/action.yml)

## 4.4 Upstream and tooling references

- Microsoft Docs: [Including native libraries in .NET packages](https://learn.microsoft.com/en-us/nuget/create-packages/native-files-in-net-packages)
- Microsoft Docs: [Native library loading](https://learn.microsoft.com/en-us/dotnet/standard/native-interop/native-library-loading)
- Microsoft Docs: [vcpkg triplet variables and per-port customization](https://learn.microsoft.com/en-us/vcpkg/users/triplets)

## 4.5 Local empirical checks performed

The following were validated directly in the local workspace:

1. Project topology and package graph assumptions.
2. CI wiring for overlay ports vs overlay triplets.
3. Presence or absence of custom native loading logic.
4. Presence of `buildTransitive` targets in native packages.
5. Harvested-vs-tracked payload drift for `SDL2_mixer`.
6. Sample hash comparison for duplicated native basenames across harvested outputs.
7. Local SDL buildtree visibility/export behavior for `SDL2_image`, `SDL2_ttf`, and `SDL2_mixer`.

---

## 5. Current Repo Reality

## 5.1 Canonical status has not changed yet

The canonical repo status is still **Phase 2: CI/CD & Packaging** in [../../plan.md](../../plan.md).

That matters because the repo has not yet formally adopted a redesigned packaging model, even if the temp research strongly suggests that the current Phase 2 framing is incomplete.

## 5.2 The build host is harvest-first, not package-truth-first

The current Cake host is strong in the following areas:

- vcpkg-aware runtime scanning
- binary closure walking
- system-artifact filtering
- per-RID harvest output
- consolidation into manifest and summary outputs

But the host is still oriented around a harvest-and-deploy model rather than a validated package-consumer model.

Relevant evidence:

- [../../../build/_build/Tasks/Harvest/HarvestTask.cs](../../../build/_build/Tasks/Harvest/HarvestTask.cs)
- [../../../build/_build/Tasks/Harvest/ConsolidateHarvestTask.cs](../../../build/_build/Tasks/Harvest/ConsolidateHarvestTask.cs)
- [../../../build/_build/Modules/Harvesting/ArtifactPlanner.cs](../../../build/_build/Modules/Harvesting/ArtifactPlanner.cs)

Important nuance:

- native-source selection flags are intentionally not on the active CLI surface.
- harvest staging helpers exist in [../../../build/_build/Modules/PathService.cs](../../../build/_build/Modules/PathService.cs).
- those hooks suggest an intended evolution path, but they are not yet the operational center of the pipeline.

## 5.3 The source graph and shipping graph are still conflated in practice

The repo is still source-graph first:

- [../../../src/SDL2.Core/SDL2.Core.csproj](../../../src/SDL2.Core/SDL2.Core.csproj) directly references `SDL2.Core.Native`.
- [../../../src/SDL2.Mixer/SDL2.Mixer.csproj](../../../src/SDL2.Mixer/SDL2.Mixer.csproj) directly references both `SDL2.Core` and `SDL2.Mixer.Native`.

That means current success in the source solution does **not** prove that package-consumer boundaries are correct.

## 5.4 There is no real package-consumer smoke-test spine yet

The repo does not currently have a dedicated package-consumer validation project that restores local packages and proves real end-user native loading behavior.

[../../../test/Sandboc/Program.cs](../../../test/Sandboc/Program.cs) is useful as a local Visual Studio and `dumpbin` utility, but it is not the integration spine described in the temp strategy docs.

This is an important gap because it means the repo currently lacks a reliable package-truth signal.

## 5.5 Native package projects are content-pack containers

[../../../src/native/Directory.Build.props](../../../src/native/Directory.Build.props) confirms that native package projects pack:

- `runtimes/**`
- `build/**`
- `buildTransitive/**`
- `licenses/**`

This is compatible with a hybrid future, but the current package payload model is still file-layout based.

## 5.6 Only Core.Native currently has a buildTransitive target

The only current `buildTransitive` target file under `src/native/**` is:

- [../../../src/native/SDL2.Core.Native/buildTransitive/Janset.SDL2.Core.Native.targets](../../../src/native/SDL2.Core.Native/buildTransitive/Janset.SDL2.Core.Native.targets)

That target handles copying `SDL2.dll` for .NET Framework builds.

This means non-core native package behavior is still asymmetric, especially for older framework consumers.

## 5.7 CI currently supports overlay ports, not overlay triplets

[../../../.github/actions/vcpkg-setup/action.yml](../../../.github/actions/vcpkg-setup/action.yml) wires `--overlay-ports` when `vcpkg-overlay-ports` exists.

It does **not** currently wire `--overlay-triplets`.

This is a real gap if the chosen strategy depends on mixed-linkage custom triplets.

## 5.8 Managed native loading remains conventional

The imported SDL2-CS bindings still use plain library names through `DllImport`, for example:

- [../../../external/sdl2-cs/src/SDL2_image.cs](../../../external/sdl2-cs/src/SDL2_image.cs)
- [../../../external/sdl2-cs/src/SDL2_ttf.cs](../../../external/sdl2-cs/src/SDL2_ttf.cs)
- [../../../external/sdl2-cs/src/SDL2_mixer.cs](../../../external/sdl2-cs/src/SDL2_mixer.cs)

No custom `SetDllImportResolver` or `NativeLibrary.Load` usage was found under `src/**`.

That matters because the managed side currently assumes standard native resolution behavior, not a custom probing regime.

---

## 6. Packaging Risk Assessment

## 6.1 Pure dynamic has a structural collision problem

The strongest direct tooling fact is from the official NuGet packaging guidance:

- native libraries are taken from `runtimes/{rid}/native/`
- the .NET SDK flattens directory structure beneath that path when copying to output

This means same-basename files from multiple native packages are not harmless.

The repo already has overlapping basenames across harvested SDL satellite outputs, including sampled duplicates like:

- `zlib1.dll`
- `libpng16.dll`

The sampled duplicates compared during validation had matching hashes today, but that does **not** eliminate the structural risk. It only shows that the sampled files happen to be identical in the current local build state.

### Collision risk implication

Pure Dynamic is not safe enough as the long-term default unless the repo is willing to manage either:

- a shared-common-packages ecosystem, or
- a very careful collision-avoidance regime

Neither is attractive here.

## 6.2 The tracked native payload workflow already shows drift

A direct comparison between harvested and tracked `SDL2_mixer` win-x64 payloads showed drift.

Examples seen as tracked-only stale files:

- `charset-1.dll`
- `ffi-8.dll`
- `gio-2.0-0.dll`
- `girepository-2.0-0.dll`
- `glib-2.0-0.dll`
- `gobject-2.0-0.dll`
- `iconv-2.dll`
- `intl-8.dll`
- `pcre2-*`
- `zlib1.dll`

Example seen as harvest-only current file:

- `libfluidsynth-3.dll`

### Payload drift implication

The current docs that still say "copy harvested files into tracked `src/native/**/runtimes`" are describing a workflow that has already drifted away from current reality.

That does not just argue for cleanup. It argues for changing what is treated as authoritative.

## 6.3 License pressure remains concentrated around mixer extras

[../../../vcpkg.json](../../../vcpkg.json) declares `sdl2-mixer` with the following enabled feature set:

- `fluidsynth`
- `libflac`
- `libmodplug`
- `mpg123`
- `opusfile`
- `wavpack`

Combined with the earlier license inventory research, this keeps the same conclusion intact:

- permissive codec and support libs are not the strategic problem
- LGPL-sensitive extras are the policy problem

### License policy implication

An opt-in Extras package model still makes sense for `SDL2_mixer`.

---

## 7. Feasibility of Hybrid Static + Dynamic Core

## 7.1 vcpkg can support mixed linkage

Official vcpkg triplet docs explicitly support:

- `VCPKG_LIBRARY_LINKAGE`
- per-port customization through the `PORT` variable

That means the tooling model needed for a mixed-linkage build is legitimate, not a hack.

## 7.2 The repo and ports already lean partway in that direction

The current SDL vcpkg portfiles already show behavior that pushes dependencies inward rather than keeping everything as loose dynamic runtime surface.

Examples:

- [../../../external/vcpkg/ports/sdl2-image/portfile.cmake](../../../external/vcpkg/ports/sdl2-image/portfile.cmake) sets `SDL2IMAGE_DEPS_SHARED=OFF`
- [../../../external/vcpkg/ports/sdl2-mixer/portfile.cmake](../../../external/vcpkg/ports/sdl2-mixer/portfile.cmake) sets `SDL2MIXER_DEPS_SHARED=OFF`
- the same mixer portfile also sets `SDL2MIXER_OPUS_SHARED=OFF` and `SDL2MIXER_VORBIS_VORBISFILE_SHARED=OFF`
- `SDL2MIXER_MOD_XMP_SHARED=${BUILD_SHARED}` shows that shared-vs-static behavior is already selectively configurable in practice

### Feasibility implication from current port behavior

Hybrid is not starting from zero. The repo is already dealing with a world where not every dependency is meant to remain externally loose.

## 7.3 Wrapper DLLs are not yet proven mandatory

This is the most important nuance added by the latest validation pass.

The strongest wrapper-heavy argument in the temp docs was that if satellites statically absorb dependencies, symbol leakage would force a new wrapper or bundle shared library layer.

The local buildtree evidence weakens that claim:

- local SDL buildtrees for `sdl2-image`, `sdl2-ttf`, and `sdl2-mixer` already use hidden visibility
- their public headers export the intended SDL-family API explicitly

In other words, upstream/shared-library behavior is already doing part of the sealing work.

But this should not be read too comfortably.

The current evidence weakens the claim that wrapper projects are automatically required everywhere. It does **not** prove that symbol visibility, export boundaries, or package-consumer loader behavior are already solved for the repo's chosen shipping model.

### Design implication for wrapper scope

Hybrid still looks right, but the repo should start with the smallest implementation that proves the policy and packaging model.

Do **not** assume a wrapper-native-project explosion until a concrete unresolved issue remains after mixed-linkage validation.

At the same time, do **not** downgrade wrapper scope to a disproven risk. The hard gate is: if mixed-linkage package-consumer validation still shows unresolved loader, symbol, or export-boundary problems, wrapper or bundle-native projects move back onto the table immediately.

## 7.4 SDL core still needs to stay dynamic

[../../../build/manifest.json](../../../build/manifest.json) marks SDL2 as `core_lib`, and [../../../build/_build/Modules/Harvesting/ArtifactPlanner.cs](../../../build/_build/Modules/Harvesting/ArtifactPlanner.cs) already contains logic to avoid copying core-owned artifacts into non-core packages.

That existing ownership distinction is consistent with the strategy direction:

- SDL2 core remains the shared dynamic anchor
- satellites become more sealed around their own non-core transitive surface

---

## 8. What the Official Tooling Docs Mean for This Repo

## 8.1 NuGet native packaging guidance

Relevant official guidance:

1. Native assets belong under `runtimes/{rid}/native/`.
2. The SDK flattens that directory structure when copying to output.
3. RID-specific behavior is part of the package consumer contract.
4. .NET Framework support is more complex and often requires explicit `buildTransitive` or `build` logic.

### Repo implication of NuGet guidance

- the repo cannot treat duplicate basenames as merely a cosmetic problem
- package truth must be validated through actual package restore/build/run flows
- asymmetric `buildTransitive` coverage across native packages is a real support gap

## 8.2 .NET native library loading guidance

Relevant official guidance:

1. `DllImport("SDL2_image")` style library names rely on conventional platform name resolution.
2. `SetDllImportResolver` is available only when the project chooses to introduce custom logic.

### Repo implication of managed loading guidance

- the current managed binding layer is still conventional and simple
- the repo should treat standard loading as the starting position, not as a guaranteed final answer
- the repo does not need to invent a custom resolver unless the chosen package layout proves it necessary
- any future resolver should be justified by a concrete problem found in package-consumer validation, not added by default

## 8.3 vcpkg triplet guidance

Relevant official guidance:

1. triplets can set default linkage policy
2. triplets can customize behavior per port using `PORT`
3. Linux and macOS relocation and RPATH-related behavior are first-class concerns in vcpkg

### Repo implication of vcpkg guidance

- mixed-linkage Hybrid is a supported vcpkg pattern
- the repo's main tooling gap is workflow wiring, not absence of platform support

---

## 9. Temp Doc Audit

## 9.1 `packaging-strategy-verdict-2026-04-13-shared.md`

### Strengths of `packaging-strategy-verdict-2026-04-13-shared.md`

- broad strategic direction
- collision problem framing
- keeping SDL core dynamic
- pushing package topology toward a smaller, cleaner family
- not wanting to become a generic transitive-dependency distribution platform

### Softening needed in `packaging-strategy-verdict-2026-04-13-shared.md`

- any implementation statements that imply wrapper DLLs are obviously required before mixed-linkage validation is attempted

### Recommended use of `packaging-strategy-verdict-2026-04-13-shared.md`

Use as the best short-form strategy doc in the folder, but make the wrapper story conditional rather than automatic.

## 9.2 `execution-model-strategy-2026-04-13-shared.md`

### Strengths of `execution-model-strategy-2026-04-13-shared.md`

- separate Source Mode, Package Validation Mode, and Release Mode
- package-consumer smoke tests as the integration spine
- strategy vs native-source separation
- preserving the current build-host spine while adding policy seams

### Grounding still needed in `execution-model-strategy-2026-04-13-shared.md`

- it describes the right model, but the repo still lacks the package-validation spine it assumes should exist

### Recommended use of `execution-model-strategy-2026-04-13-shared.md`

Promote the mode model and integration-spine concept. Treat the rest as execution design, not current reality.

## 9.3 `packaging-strategy-verdict-2026-04-13-gemini.md`

### Contributions from `packaging-strategy-verdict-2026-04-13-gemini.md`

- useful articulation of why Pure Dynamic and Shared Common Packages are unattractive
- strong emphasis on collision and license pressure
- helpful framing of why Hybrid feels operationally cleaner

### Overstatements in `packaging-strategy-verdict-2026-04-13-gemini.md`

- `Durum: ONAYLANDI` is ahead of the evidence
- the wrapper-project requirement is framed too absolutely
- it reads more like a final architecture decree than a research input

### Recommended use of `packaging-strategy-verdict-2026-04-13-gemini.md`

Keep as an argument source, not as an approved design artifact.

## 9.4 `packaging-strategy-verdict-2026-04-13-grok.md`

### Contributions from `packaging-strategy-verdict-2026-04-13-grok.md`

- concise comparison framing
- good instincts on keeping package count low
- clear articulation of why Phase 2 is blocked on packaging truth

### Overstatements in `packaging-strategy-verdict-2026-04-13-grok.md`

- `Status: Final Recommendation` is too final
- it inherits the same overconfidence around wrapper-heavy implementation

### Recommended use of `packaging-strategy-verdict-2026-04-13-grok.md`

Keep as secondary input. Do not promote as-is.

---

## 10. Final Recommendation

## 10.1 Strategy choice

Choose **Hybrid Static + Dynamic Core**.

## 10.2 But choose the leanest believable implementation path

The implementation assumptions should be:

1. SDL2 core remains dynamic.
2. LGPL-sensitive mixer extras stay dynamic and become opt-in.
3. permissive transitive libraries are pushed inward as far as practical
4. mixed-linkage triplet and port behavior should be proven first
5. wrapper or bundle native projects should be introduced only if a concrete unresolved loader, symbol, export-boundary, or packaging problem remains after package-consumer validation

## 10.3 What should be rejected

The following should **not** be the target direction:

1. continuing to treat the current tracked native payload folders as the long-term packaging truth
2. pretending source-solution success proves package-consumer success
3. promoting the Gemini or Grok verdict docs unchanged into canonical docs
4. turning the Cake host into a giant abstract orchestration framework before the package-validation spine exists

---

## 11. Consequences for Doc and Phase Redesign

If the strategy is accepted, these repo docs should be reconsidered first.

## 11.1 `docs/plan.md`

The phase can stay Phase 2, but its description should shift from a tracked-payload packaging story to a package-truth packaging story.

Phase 2 should explicitly include:

- package-consumer validation
- policy-driven native staging
- local feed validation
- Extras package boundaries where needed

## 11.2 `docs/phases/phase-2-cicd-packaging.md`

This is the most obvious redesign target.

It currently still assumes a workflow that copies harvested binaries into tracked `src/native/{Library}.Native/runtimes/{rid}/native/` folders.

That model is no longer strong enough to serve as the future-state architectural center.

## 11.3 `docs/playbook/local-development.md`

This playbook should stop teaching manual copy-as-truth and instead define:

- Source Mode
- Package Validation Mode
- native asset acquisition options
- local feed validation flow

---

## 12. Recommended Near-Term Roadmap

These are the most defensible next steps if the repo wants to move without overcommitting too early.

1. Formally accept Hybrid Static + Dynamic Core as the direction.
2. Canonize the three-mode model: Source, Package Validation, Release.
3. Add a real package-consumer smoke-test spine.
4. Reframe native assets as acquired build inputs, not tracked payload truth.
5. Wire the build host and CI for explicit strategy and native-source selection.
6. Validate a minimal mixed-linkage path before introducing wrapper-heavy native projects.
7. Treat symbol visibility and export-boundary validation as a separate acceptance gate, not as something implicitly covered by a successful build or harvest.

---

## 13. Open Questions That Still Need Proof

The main remaining questions are narrower than the earlier debate suggested.

1. What is the smallest mixed-linkage configuration that works cleanly across the supported RID set?
2. Which exact mixer extras belong in the default package vs an Extras package boundary?
3. Do any non-core packages need their own `buildTransitive` support for the repo's target compatibility promise?
4. Is a custom managed resolver needed at all, or does package-consumer validation show that standard loading remains sufficient under the chosen package layout?
5. Which parts of the current harvest/deploy pipeline should become staging, validation, and pack services rather than direct file-copy conventions?

---

## 14. Bottom Line

The research does support making a strategy decision now.

That decision should be:

**Adopt Hybrid Static + Dynamic Core, but do it with a proof-first, minimal-assumption implementation path.**

The repo does not need to keep debating whether the packaging problem is real. It is real.

The repo also does not need to accept every heavy implementation claim in the current temp docs. Some of those claims outran the evidence.

The right move is to lock the strategic direction, tighten the confidence levels of the existing temp docs, and redesign Phase 2 around package-validation truth instead of tracked native payload folders.

---

## 15. Reference Appendix

## 15.1 Key repo references

- [../../plan.md](../../plan.md)
- [../../phases/phase-2-cicd-packaging.md](../../phases/phase-2-cicd-packaging.md)
- [../../playbook/local-development.md](../../playbook/local-development.md)
- [../../../vcpkg.json](../../../vcpkg.json)
- [../../../build/manifest.json](../../../build/manifest.json)
- [../../../.github/actions/vcpkg-setup/action.yml](../../../.github/actions/vcpkg-setup/action.yml)
- [../../../src/native/Directory.Build.props](../../../src/native/Directory.Build.props)
- [../../../src/SDL2.Core/SDL2.Core.csproj](../../../src/SDL2.Core/SDL2.Core.csproj)
- [../../../src/SDL2.Mixer/SDL2.Mixer.csproj](../../../src/SDL2.Mixer/SDL2.Mixer.csproj)
- [../../../src/native/SDL2.Core.Native/buildTransitive/Janset.SDL2.Core.Native.targets](../../../src/native/SDL2.Core.Native/buildTransitive/Janset.SDL2.Core.Native.targets)
- [../../../test/Sandboc/Program.cs](../../../test/Sandboc/Program.cs)
- [../../../build/_build/Modules/Harvesting/ArtifactPlanner.cs](../../../build/_build/Modules/Harvesting/ArtifactPlanner.cs)
- [../../../external/vcpkg/ports/sdl2-image/portfile.cmake](../../../external/vcpkg/ports/sdl2-image/portfile.cmake)
- [../../../external/vcpkg/ports/sdl2-mixer/portfile.cmake](../../../external/vcpkg/ports/sdl2-mixer/portfile.cmake)
- [../../../external/vcpkg/ports/sdl2-ttf/portfile.cmake](../../../external/vcpkg/ports/sdl2-ttf/portfile.cmake)

## 15.2 External references

- [Including native libraries in .NET packages](https://learn.microsoft.com/en-us/nuget/create-packages/native-files-in-net-packages)
- [Native library loading](https://learn.microsoft.com/en-us/dotnet/standard/native-interop/native-library-loading)
- [vcpkg triplet variables and per-port customization](https://learn.microsoft.com/en-us/vcpkg/users/triplets)
