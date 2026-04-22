# Unix Native Identity Strategy — vcpkg, SDL2 Family, Overlay Ports, and the Single-File Question

**Date:** 2026-04-22
**Status:** Research complete, recommendation ready; no canonical repo decision applied yet
**Audience:** Maintainer / future contributors evaluating whether to keep the current tar.gz symlink-preserving strategy or replace it with single-file Unix outputs
**Related:** [symlink-handling.md](symlink-handling.md), [native-packaging-comparative-analysis-2026-04-13.md](native-packaging-comparative-analysis-2026-04-13.md), [packaging-strategy-hybrid-static-2026-04-13.md](packaging-strategy-hybrid-static-2026-04-13.md), [source-mode-native-visibility-2026-04-15.md](source-mode-native-visibility-2026-04-15.md), [../onboarding.md](../onboarding.md), [../plan.md](../plan.md)

## 1. Executive Summary

This research answers a very specific question:

> Can Janset.SDL2 stay inside the vcpkg ecosystem and still produce a **single unversioned Unix `.so` / `.dylib` per SDL family library with no symlink chain**, without relying on ad hoc runtime path hacks as the primary mechanism?

Short answer:

- **Yes, technically possible.**
- **No, not via a generic vcpkg triplet switch.**
- **Yes, the correct implementation layer is overlay ports, not triplets.**
- **Yes, this is fundamentally a maintainability vs reliability tradeoff.**

The strongest conclusion from this session is:

1. The repo's **hybrid triplets already solve the transitive-dependency-bake problem**.
2. The remaining problem is **Unix shared-library identity and packaging topology**.
3. That second problem is not controlled by triplets in any general way.
4. Current SDL2-family upstream build definitions, as consumed by vcpkg, explicitly encode:
   - `VERSION`
   - `SOVERSION`
   - Mach-O compatibility/current version fields on Apple
   - explicit `create_symlink` steps for unversioned aliases
5. Therefore, if the repo wants to replace the current `tar.gz` symlink-preserving strategy with true single-file Unix outputs, the work belongs at the **port/build-definition layer**.

That means:

- **Overlay-port patching** is the most semantically correct and generally the most reliable path.
- **Post-build ELF/Mach-O surgery** (`patchelf`, `install_name_tool`, file renames, etc.) is a fallback path, not the preferred primary design.
- The repo should treat this as a deliberate product-level packaging strategy decision, not as a small build tweak.

## 2. Why This Research Exists

The repo's current canonical Unix packaging decision is documented in [../onboarding.md](../onboarding.md): Linux/macOS native payloads are archived as `tar.gz` because NuGet cannot preserve symlink chains and naive flattening breaks transitive dependency identity.

That decision was correct for the system as originally implemented. However, the packaging architecture evolved:

- the repo now uses **hybrid triplets**,
- transitive dependencies are already **statically baked** into SDL family satellites where appropriate,
- Windows behavior is already much closer to the desired “one binary per package payload” model,
- and the remaining awkwardness is concentrated on Unix shared-library identity.

This raised a new question:

> If hybrid triplets already solved dependency baking, is the remaining symlink/versioned-name problem something vcpkg can solve more cleanly inside the native build itself?

That is the scope of this document.

## 3. The Real Problem

The important distinction is this:

### 3.1 Solved Problem: Dependency Baking / Collision Avoidance

The hybrid strategy already addresses the “shared transitive DLL/SO/dylib collision” problem documented in [packaging-strategy-hybrid-static-2026-04-13.md](packaging-strategy-hybrid-static-2026-04-13.md).

Repo-local evidence:

- [../../vcpkg-overlay-triplets/_hybrid-common.cmake](../../vcpkg-overlay-triplets/_hybrid-common.cmake) sets default static linkage, then flips SDL-family libraries to dynamic.
- [../../docs/playbook/cross-platform-smoke-validation.md](../../docs/playbook/cross-platform-smoke-validation.md) shows the hybrid-static witness path is already validated on the original 3-platform host slice.
- [../../tests/smoke-tests/native-smoke/README.md](../../tests/smoke-tests/native-smoke/README.md) shows the runtime smoke surface that proves baked-in codec/functionality behavior.

### 3.2 Unsolved Problem: Shared-Library Identity / Topology on Unix

The remaining issue is not “dynamic vs static.”

It is:

- Do we ship a symlink chain on Linux/macOS?
- Do we ship one versioned real file plus one or more alias names?
- What is the actual `SONAME` / `install_name` identity of the library?
- What filename does the consumer-facing loader probe resolve against?
- Can the NuGet payload remain a normal loose-file native payload instead of tar-extracted symlink-preserving content?

This is a **library identity** problem, not a linkage-mode problem.

## 4. Evidence Collected

This session gathered evidence from five layers:

1. official vcpkg documentation
2. the repo's hybrid triplets and overlay ports
3. official SDL2-family vcpkg portfiles
4. actual extracted SDL2-family source trees in `vcpkg_installed/vcpkg/blds/...`
5. the repo's managed binding surface

### 4.1 vcpkg Docs: What Triplets Actually Control

Official Microsoft Learn documentation says:

- `VCPKG_LIBRARY_LINKAGE` is **preferred library linkage**
- `VCPKG_FIXUP_ELF_RPATH` adds relocatable `RUNPATH` entries on Linux
- `VCPKG_INSTALL_NAME_DIR` sets the install-name directory on macOS
- `VCPKG_FIXUP_MACHO_RPATH` rewrites Mach-O install-name / rpath data for relocatability
- triplets support per-port customization of variables such as linkage

That is enough to conclude:

- Triplets can influence **how a library is built and fixed up**.
- Triplets do **not** define a first-class generic policy for suppressing upstream symlink creation or rewriting a library family's basename strategy.

References:

- [vcpkg triplet variables](https://learn.microsoft.com/en-us/vcpkg/users/triplets)
- [vcpkg overlay ports](https://learn.microsoft.com/en-us/vcpkg/concepts/overlay-ports)

### 4.2 Repo Triplets: The Current Hybrid Policy Stops at Linkage/Fixups

The current hybrid triplet behavior is exactly what it should be for the dependency-bake problem:

- [../../vcpkg-overlay-triplets/_hybrid-common.cmake](../../vcpkg-overlay-triplets/_hybrid-common.cmake) sets `VCPKG_CRT_LINKAGE dynamic`
- same file sets `VCPKG_LIBRARY_LINKAGE static`
- same file flips SDL-family ports to dynamic based on `PORT`
- same file enables `VCPKG_FIXUP_ELF_RPATH` on Linux

This is strong evidence that the current triplets are already doing their job.

They do **not** contain logic that:

- rewrites `OUTPUT_NAME`
- removes `SOVERSION`
- disables symlink creation
- changes macOS `LC_ID_DYLIB` semantics beyond relocatability support

That is not a flaw in the current triplets. That is simply the wrong layer for most of this problem.

### 4.3 Overlay Ports: Repo-Local Precedent Already Exists

The repo already uses overlay ports for “actual port behavior” changes.

Key references:

- [../../vcpkg-overlay-ports/README.md](../../vcpkg-overlay-ports/README.md)
- [../../vcpkg-overlay-ports/sdl2-gfx/portfile.cmake](../../vcpkg-overlay-ports/sdl2-gfx/portfile.cmake)
- [../../vcpkg-overlay-ports/sdl2-mixer/portfile.cmake](../../vcpkg-overlay-ports/sdl2-mixer/portfile.cmake)

Concrete examples already in-tree:

- `sdl2-gfx` overlay adds Unix visibility/export behavior fixes
- `sdl2-mixer` overlay changes codec/backend choices to enforce licensing/product policy

This matters because it proves the repo already accepts the core architectural principle:

> If upstream port behavior does not match repo policy, use an overlay port.

That principle maps directly onto Unix shared-library identity.

### 4.4 Official SDL2-Family vcpkg Portfiles Mostly Pass Through Upstream Behavior

The official portfiles under [../../external/vcpkg/ports](../../external/vcpkg/ports) do not currently implement a repo-specific “single unversioned Unix output, no symlink chain” behavior.

Representative files:

- [../../external/vcpkg/ports/sdl2/portfile.cmake](../../external/vcpkg/ports/sdl2/portfile.cmake)
- [../../external/vcpkg/ports/sdl2-image/portfile.cmake](../../external/vcpkg/ports/sdl2-image/portfile.cmake)
- [../../external/vcpkg/ports/sdl2-mixer/portfile.cmake](../../external/vcpkg/ports/sdl2-mixer/portfile.cmake)
- [../../external/vcpkg/ports/sdl2-ttf/portfile.cmake](../../external/vcpkg/ports/sdl2-ttf/portfile.cmake)
- [../../external/vcpkg/ports/sdl2-net/portfile.cmake](../../external/vcpkg/ports/sdl2-net/portfile.cmake)
- [../../external/vcpkg/ports/sdl2-gfx/CMakeLists.txt](../../external/vcpkg/ports/sdl2-gfx/CMakeLists.txt)

Observed pattern:

- standard `vcpkg_cmake_configure(...)`
- standard `vcpkg_cmake_install()`
- standard config/pkgconfig fixups
- feature toggles and minor packaging adjustments
- no general “flatten Unix shared-library identity” mode

Important nuance:

- `sdl2-gfx` is structurally simpler and less version-metadata-heavy than the rest of the family
- `sdl2` core is the most special case because the basename itself is intentionally version-flavored on Unix (`SDL2-2.0`)

### 4.5 Extracted Buildtree Source: The Behavior Comes from Upstream SDL CMake

This was the strongest evidence collected.

The workspace contains actual extracted SDL2-family source trees used by vcpkg builds under [../../vcpkg_installed/vcpkg/blds](../../vcpkg_installed/vcpkg/blds).

Representative files:

- [../../vcpkg_installed/vcpkg/blds/sdl2/src/se-2.32.10-3b143ac573.clean/CMakeLists.txt](../../vcpkg_installed/vcpkg/blds/sdl2/src/se-2.32.10-3b143ac573.clean/CMakeLists.txt)
- [../../vcpkg_installed/vcpkg/blds/sdl2-image/src/ease-2.8.8-a451848362.clean/CMakeLists.txt](../../vcpkg_installed/vcpkg/blds/sdl2-image/src/ease-2.8.8-a451848362.clean/CMakeLists.txt)
- [../../vcpkg_installed/vcpkg/blds/sdl2-mixer/src/ease-2.8.1-f9fde3a2e2.clean/CMakeLists.txt](../../vcpkg_installed/vcpkg/blds/sdl2-mixer/src/ease-2.8.1-f9fde3a2e2.clean/CMakeLists.txt)
- [../../vcpkg_installed/vcpkg/blds/sdl2-ttf/src/ase-2.24.0-bdb8d0cb53.clean/CMakeLists.txt](../../vcpkg_installed/vcpkg/blds/sdl2-ttf/src/ase-2.24.0-bdb8d0cb53.clean/CMakeLists.txt)
- [../../vcpkg_installed/vcpkg/blds/sdl2-net/src/8052cb7a4e-5eb8996ab7.clean/CMakeLists.txt](../../vcpkg_installed/vcpkg/blds/sdl2-net/src/8052cb7a4e-5eb8996ab7.clean/CMakeLists.txt)

What these show:

- `sdl2` core explicitly calculates libtool-style version fields and sets versioned Unix output naming
- `sdl2-image`, `sdl2-mixer`, `sdl2-ttf`, and `sdl2-net` explicitly set `SOVERSION` and `VERSION`
- Apple builds also set Mach-O compatibility/current version metadata
- several of these projects explicitly run `cmake -E create_symlink` for unversioned aliases

In other words:

> The symlink/versioned-name behavior is not an accidental vcpkg quirk. It is the upstream SDL2-family build definition.

That is exactly why changing the behavior cleanly means changing the port/build layer.

### 4.6 Managed Binding Constraint: No Resolver Layer Exists Yet

The current managed bindings still compile SDL2-CS source directly.

Representative files:

- [../../external/sdl2-cs/src/SDL2.cs](../../external/sdl2-cs/src/SDL2.cs)
- [../../external/sdl2-cs/src/SDL2_image.cs](../../external/sdl2-cs/src/SDL2_image.cs)
- [../../external/sdl2-cs/src/SDL2_mixer.cs](../../external/sdl2-cs/src/SDL2_mixer.cs)
- [../../external/sdl2-cs/src/SDL2_ttf.cs](../../external/sdl2-cs/src/SDL2_ttf.cs)
- [../../external/sdl2-cs/src/SDL2_gfx.cs](../../external/sdl2-cs/src/SDL2_gfx.cs)
- [../../src/SDL2.Core/SDL2.Core.csproj](../../src/SDL2.Core/SDL2.Core.csproj)

Practical implication:

- there is no repo-local `DllImportResolver` abstraction that hides native name changes
- if native output identity changes materially, bindings or resolver behavior may also need to change

This does **not** block overlay-port work, but it increases the blast radius of any “curated naming” strategy.

## 5. Feasibility Verdict

### 5.1 Is a Single-File Unix Output Possible?

**Yes, technically possible.**

But “possible” does not mean “available as a stock vcpkg setting.”

Possible implementation routes:

1. patch upstream SDL-family CMake/build behavior in overlay ports so the desired file identity is emitted directly
2. accept upstream build output, then normalize it post-build by rewriting loader metadata and renaming files
3. keep current versioned identity but introduce a managed resolver/curated package layout that hides the complexity from consumers

### 5.2 Is There a Generic vcpkg-Supported Switch for This?

**No evidence was found for that.**

The official vcpkg model supports:

- linkage preference
- toolchain flags
- RPATH/install-name fixups
- overlay port replacement

But not a generic “flatten Unix shared-library naming topology” feature.

### 5.3 Does This Belong in Triplets?

**No, not as the primary implementation.**

Triplets may still be involved as a supporting mechanism:

- per-port configure flags
- post-port include hooks
- fixup toggles

But the core ownership remains the port/build definition, because that is where:

- `OUTPUT_NAME`
- `VERSION`
- `SOVERSION`
- Mach-O version fields
- symlink generation

are actually defined.

## 6. Decision Matrix

The repo has four realistic paths.

| Option | Reliability | Maintainability | Blast Radius | Cross-platform consistency | Recommendation |
| --- | --- | --- | --- | --- | --- |
| Keep current `tar.gz` symlink-preserving strategy | High | Medium | Low | High | Safe default today |
| Overlay-port patching per library | High | Low-to-medium | Medium | High | Best path if single-file Unix outputs are truly required |
| Post-build surgery (`patchelf`, `install_name_tool`, renames) | Medium | Medium | High | Low-to-medium | Fallback only |
| Managed resolver + curated names | Medium | Low-to-medium | High | Medium | Possible later, but not the first move |

### 6.1 Why Overlay Ports Are More Reliable

Overlay-port patching changes the build at the point where the binary identity is born.

That means the build can produce a consistent result across:

- installed filename
- embedded `SONAME` / `LC_ID_DYLIB`
- symlink strategy
- exported config/pkgconfig data
- install layout

This is more reliable because it keeps the artifact internally self-consistent before packaging ever happens.

### 6.2 Why Post-Build Surgery Is More Fragile

Post-build normalization can work, but it is operating after the artifact has already been defined.

Typical fragility points:

- one file renamed, but dependent binaries still expect the old loader identity
- ELF and Mach-O need different tools and rules
- package metadata or exported CMake config still references the old name
- upstream adds a new install step or additional alias and the normalization script silently misses it

This is why it is reasonable to describe the tradeoff as:

> **overlay ports = higher reliability, higher maintenance**
>
> **post-build surgery = lower initial friction, lower long-term confidence**

## 7. Maintainability vs Reliability Tradeoff

Yes, this repo should think about the problem in exactly these terms.

### 7.1 Overlay-Port Path

**What you buy:**

- better semantic correctness
- better reproducibility
- fewer “it built, but metadata drifted” failure modes
- better CI confidence if the validation suite is strong

**What you pay:**

- per-library patch maintenance
- upstream drift handling on SDL releases and vcpkg port bumps
- SDL3-era recurring review of the same problem class

### 7.2 Post-Build Surgery Path

**What you buy:**

- faster proof-of-concept
- fewer upstream source patches at the beginning
- easier experimentation if the goal is just to learn whether the target shape is viable

**What you pay:**

- more fragile pipeline logic
- platform-specific normalization differences
- higher chance of silent regressions
- weaker long-term story for SDL3 and future port changes

### 7.3 Practical Judgment for This Repo

For this repo specifically, the reliability side matters a lot because:

- the project's core value proposition is reproducible cross-platform native packaging
- consumers are expected to trust the NuGet payload directly
- Phase 5 already anticipates SDL3 on top of the same infrastructure
- the repo already accepts overlay ports as the place where upstream packaging behavior is bent to match project policy

That makes overlay-port patching the more principled path if the repo decides to chase single-file Unix outputs.

## 8. Recommended Strategy

### 8.1 Default Recommendation

**Do not replace the current tar.gz strategy casually.**

The current strategy is still correct, and it exists because [symlink-handling.md](symlink-handling.md) documented a real packaging limitation.

If the repo wants to move away from it, treat that as a **new packaging initiative**, not as cleanup.

### 8.2 If the Repo Pursues Single-File Unix Outputs

Recommended approach:

1. keep current tar.gz strategy as the canonical shipping path until an alternative is proven on real artifacts
2. run a pilot using **overlay-port patching**, not post-build normalization
3. validate the pilot with a much stricter test surface than the repo currently enforces
4. only then decide whether the strategy is worth scaling across SDL2 and later SDL3

### 8.3 Pilot Scope Recommendation

Recommended first pilot library: **SDL2_ttf**

Why:

- it already has a managed package path in the repo
- its smoke surface is simple (`TTF_Init()` is enough for a first runtime proof)
- it uses the standard SDL-family `VERSION` / `SOVERSION` / `create_symlink` pattern
- it is lower blast radius than `sdl2` core

Recommended order if the pilot succeeds:

1. `sdl2-ttf`
2. `sdl2-image`
3. `sdl2-mixer`
4. `sdl2-net`
5. `sdl2` core
6. `sdl2-gfx` only if its separate simpler shape still needs alignment

Why core is last:

- `sdl2` core is the most special case
- the upstream Unix basename is intentionally version-flavored (`SDL2-2.0`)
- a mistake there affects every satellite and every consumer

## 9. What “Very Strict Validation” Actually Means

This is the most important operational recommendation in the whole document.

If the repo goes down the overlay-port route, patching alone is not enough. The validation surface needs to prove that the new shape is correct on disk, in metadata, and at runtime.

At minimum, the following must exist.

### 9.1 File Topology Test

Goal:

- verify the packaged payload contains exactly the expected file layout
- verify there is exactly one real `.so` / `.dylib` when that is the design goal
- verify no symlink chain survived accidentally

Questions to answer:

- Is there a single Unix binary file?
- Did a symlink get recreated anyway?
- Did packaging duplicate the same binary content under multiple names?

Suggested checks:

- compare file count and basenames in `artifacts/harvest_output/...`
- compare file count and basenames in `runtimes/{rid}/native/` inside the produced package
- assert no `native.tar.gz` is used for the pilot library if loose-file shipping is the intended replacement

### 9.2 Loader Metadata Test

Goal:

- prove the binary's embedded identity matches the packaging design

Linux:

- `readelf -d`
- `objdump -p`

macOS:

- `otool -L`
- `otool -D`
- `vtool` where useful for extra Mach-O metadata inspection

Questions to answer:

- What is the actual `SONAME` / `LC_ID_DYLIB`?
- Does it match the filename we are shipping?
- Do dependent binaries still point at the old version-qualified identity?

### 9.3 Runtime Closure Test

Goal:

- prove the runtime dependency graph still resolves cleanly after the topology change

Linux:

- `ldd`

macOS:

- `otool -L`

Repo-local helpers already exist in the build host:

- [../../build/_build/Application/Harvesting/BinaryClosureWalker.cs](../../build/_build/Application/Harvesting/BinaryClosureWalker.cs)
- [../../build/_build/Domain/Strategy/HybridStaticValidator.cs](../../build/_build/Domain/Strategy/HybridStaticValidator.cs)
- [../../build/_build/Tasks/Diagnostics/InspectHarvestedDependenciesTask.cs](../../build/_build/Tasks/Diagnostics/InspectHarvestedDependenciesTask.cs)
- [../../build/_build/Infrastructure/DependencyAnalysis/LinuxLddScanner.cs](../../build/_build/Infrastructure/DependencyAnalysis/LinuxLddScanner.cs)
- [../../build/_build/Infrastructure/DependencyAnalysis/MacOtoolScanner.cs](../../build/_build/Infrastructure/DependencyAnalysis/MacOtoolScanner.cs)
- [../../build/_build/Infrastructure/DependencyAnalysis/WindowsDumpbinScanner.cs](../../build/_build/Infrastructure/DependencyAnalysis/WindowsDumpbinScanner.cs)

Recommended outcome:

- dependency closure must remain limited to the intended SDL core + system/CRT surface
- no accidentally reintroduced transitive codec/font/image libs should appear as runtime dependencies

### 9.4 Native Runtime Smoke

Goal:

- prove the native library actually loads and initializes, not just that metadata looks pretty

Use the existing C/C++ harness documented in [../../tests/smoke-tests/native-smoke/README.md](../../tests/smoke-tests/native-smoke/README.md).

Minimum rule for a pilot:

- the pilot library's relevant native smoke test must pass on every supported host platform used for the experiment

### 9.5 Consumer Smoke

Goal:

- prove the final NuGet consumer contract still works after the native topology change

This is non-negotiable.

The repo already has package-consumer smoke expectations documented in [../../docs/playbook/cross-platform-smoke-validation.md](../../docs/playbook/cross-platform-smoke-validation.md).

If a pilot changes native packaging shape, the existing `PackageConsumerSmoke` flow must still pass for the pilot family.

This is especially important because the managed bindings still use direct `DllImport` names from SDL2-CS.

### 9.6 Cross-Platform Matrix Rule

Any serious move away from `tar.gz` should be validated on at least:

1. `linux-x64`
2. `osx-x64`
3. the matching Windows control case (`win-x64`) to prove that the pilot did not regress mixed-platform assumptions in harvest/pack logic

Later, before rollout, the full intended RID surface must be exercised.

### 9.7 Upstream Drift Regression Rule

For every overlay-port pilot, future port bumps should include a mandatory checklist:

1. does the patch still apply?
2. did upstream change `VERSION` / `SOVERSION` / `OUTPUT_NAME` logic?
3. did upstream add new install or symlink steps?
4. does the validation suite still prove the intended single-file topology?

This is the part that makes SDL3 particularly important operationally.

## 10. SDL2 vs SDL3 Outlook

### 10.1 SDL2

SDL2 is relatively stable. That reduces patch churn risk.

This is the strongest argument in favor of trying the approach on SDL2 first if the repo wants to learn whether the complexity is worth it.

### 10.2 SDL3

SDL3 is exactly where the maintainability warning becomes real.

If the repo adopts overlay-port customization as a packaging policy for single-file Unix outputs, SDL3 would likely require:

- repeated port review on upstream bumps
- repeated patch maintenance
- repeated validation of naming/identity semantics

That does not invalidate the approach.

It just means the repo should adopt it only if the product benefit is strong enough to justify a standing maintenance budget.

## 11. Recommended Rollout Model

If the repo chooses to pursue this, the safest rollout looks like this.

### Stage 0: Keep Current Canonical Behavior

- current shipping path remains symlink-preserving `tar.gz`
- no consumer contract changes

### Stage 1: Pilot Overlay-Port Implementation on One SDL2 Satellite

- choose `sdl2-ttf`
- patch build/install behavior in overlay port
- keep scope as small as possible
- no broad refactor of harvest/package logic yet

### Stage 2: Add Validation Before Broadening Scope

- topology assertions
- metadata assertions
- runtime closure assertions
- native smoke
- package consumer smoke

### Stage 3: Re-evaluate With Real Cost Data

Ask:

- Was the patch easy or invasive?
- Was the validation suite enough to trust the result?
- Would this scale to `sdl2` core and later SDL3?

### Stage 4: Decide Strategy Family-Wide

Possible outcomes:

1. stay on `tar.gz` for everything
2. use single-file overlay-port strategy for selected libraries only
3. adopt single-file strategy family-wide for SDL2 only
4. adopt it as an SDL2+SDL3 long-term packaging policy

## 12. Bottom-Line Recommendation

If the repo's question is:

> “What is the most reliable way to get to single-file Unix shared libraries?”

the answer is:

> **overlay-port patching per library, backed by strict validation**

If the repo's question is:

> “What is the cheapest way to experiment?”

the answer is:

> **post-build normalization can be a prototype tool, but it should not become the default shipping architecture unless the overlay-port path proves impossible or disproportionately invasive**

If the repo's question is:

> “Should we rush to replace the current `tar.gz` strategy?”

the answer is:

> **No. The current strategy is still defensible and should remain canonical until a replacement is proven on real artifacts, with real smokes, on real platforms.**

## 13. Reference Notes for Future Contributors

The most important things to remember from this research are:

1. hybrid triplets already solved the dependency-bake problem
2. Unix naming/symlink behavior is a different problem
3. upstream SDL2-family CMake currently owns that behavior
4. overlay ports are the correct place to override it
5. the real cost is not “can this be patched?” but “can we validate and maintain it over time?”

## 14. References

### Repo-Local

- [../onboarding.md](../onboarding.md)
- [../plan.md](../plan.md)
- [symlink-handling.md](symlink-handling.md)
- [native-packaging-comparative-analysis-2026-04-13.md](native-packaging-comparative-analysis-2026-04-13.md)
- [packaging-strategy-hybrid-static-2026-04-13.md](packaging-strategy-hybrid-static-2026-04-13.md)
- [../../vcpkg-overlay-triplets/_hybrid-common.cmake](../../vcpkg-overlay-triplets/_hybrid-common.cmake)
- [../../vcpkg-overlay-ports/README.md](../../vcpkg-overlay-ports/README.md)
- [../../vcpkg-overlay-ports/sdl2-mixer/portfile.cmake](../../vcpkg-overlay-ports/sdl2-mixer/portfile.cmake)
- [../../vcpkg-overlay-ports/sdl2-gfx/portfile.cmake](../../vcpkg-overlay-ports/sdl2-gfx/portfile.cmake)
- [../../external/vcpkg/ports/sdl2/portfile.cmake](../../external/vcpkg/ports/sdl2/portfile.cmake)
- [../../external/vcpkg/ports/sdl2-image/portfile.cmake](../../external/vcpkg/ports/sdl2-image/portfile.cmake)
- [../../external/vcpkg/ports/sdl2-mixer/portfile.cmake](../../external/vcpkg/ports/sdl2-mixer/portfile.cmake)
- [../../external/vcpkg/ports/sdl2-ttf/portfile.cmake](../../external/vcpkg/ports/sdl2-ttf/portfile.cmake)
- [../../external/vcpkg/ports/sdl2-net/portfile.cmake](../../external/vcpkg/ports/sdl2-net/portfile.cmake)
- [../../external/vcpkg/ports/sdl2-gfx/CMakeLists.txt](../../external/vcpkg/ports/sdl2-gfx/CMakeLists.txt)
- [../../vcpkg_installed/vcpkg/blds/sdl2/src/se-2.32.10-3b143ac573.clean/CMakeLists.txt](../../vcpkg_installed/vcpkg/blds/sdl2/src/se-2.32.10-3b143ac573.clean/CMakeLists.txt)
- [../../vcpkg_installed/vcpkg/blds/sdl2-image/src/ease-2.8.8-a451848362.clean/CMakeLists.txt](../../vcpkg_installed/vcpkg/blds/sdl2-image/src/ease-2.8.8-a451848362.clean/CMakeLists.txt)
- [../../vcpkg_installed/vcpkg/blds/sdl2-mixer/src/ease-2.8.1-f9fde3a2e2.clean/CMakeLists.txt](../../vcpkg_installed/vcpkg/blds/sdl2-mixer/src/ease-2.8.1-f9fde3a2e2.clean/CMakeLists.txt)
- [../../vcpkg_installed/vcpkg/blds/sdl2-ttf/src/ase-2.24.0-bdb8d0cb53.clean/CMakeLists.txt](../../vcpkg_installed/vcpkg/blds/sdl2-ttf/src/ase-2.24.0-bdb8d0cb53.clean/CMakeLists.txt)
- [../../vcpkg_installed/vcpkg/blds/sdl2-net/src/8052cb7a4e-5eb8996ab7.clean/CMakeLists.txt](../../vcpkg_installed/vcpkg/blds/sdl2-net/src/8052cb7a4e-5eb8996ab7.clean/CMakeLists.txt)
- [../../external/sdl2-cs/src/SDL2.cs](../../external/sdl2-cs/src/SDL2.cs)
- [../../src/SDL2.Core/SDL2.Core.csproj](../../src/SDL2.Core/SDL2.Core.csproj)
- [../../docs/playbook/cross-platform-smoke-validation.md](../../docs/playbook/cross-platform-smoke-validation.md)
- [../../tests/smoke-tests/native-smoke/README.md](../../tests/smoke-tests/native-smoke/README.md)
- [../../build/_build/Application/Harvesting/BinaryClosureWalker.cs](../../build/_build/Application/Harvesting/BinaryClosureWalker.cs)
- [../../build/_build/Domain/Strategy/HybridStaticValidator.cs](../../build/_build/Domain/Strategy/HybridStaticValidator.cs)
- [../../build/_build/Tasks/Diagnostics/InspectHarvestedDependenciesTask.cs](../../build/_build/Tasks/Diagnostics/InspectHarvestedDependenciesTask.cs)

### External

- [Microsoft Learn: vcpkg triplet variables](https://learn.microsoft.com/en-us/vcpkg/users/triplets)
- [Microsoft Learn: vcpkg overlay ports](https://learn.microsoft.com/en-us/vcpkg/concepts/overlay-ports)
- [CMake `SOVERSION` property](https://cmake.org/cmake/help/latest/prop_tgt/SOVERSION.html)
- [patchelf](https://github.com/NixOS/patchelf)
- [Apple dynamic library guidance](https://forums.developer.apple.com/forums/thread/736719)
- [NuGet/Home#10734 — symlink support in nupkg](https://github.com/NuGet/Home/issues/10734)
- [NuGet/Home#12136 — Linux shared libraries with symlinks](https://github.com/NuGet/Home/issues/12136)
