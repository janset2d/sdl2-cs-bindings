# Parking Lot — Preserved Ideas and Partial Threads

> Deliberate landing zone for forward-looking ideas that are valid or partially implemented but not active work today. **Post-refactor (Phase X / build-host) cleanup tasks live in [`post-refactor-cleanup-plan.md`](post-refactor-cleanup-plan.md)**, not here. The goal of this file: no important *future* idea should survive only because it is buried in retired notes or half-expressed in code.

## How To Use This File

- Items here are worth preserving but not in the active phase.
- Promote items into [`plan.md`](plan.md), a phase doc, a playbook, or a knowledge-base doc when work actually starts.
- Remove items only when they are clearly rejected or superseded — record why.

## Status Legend

| Status | Meaning |
| --- | --- |
| `planned` | Idea is intentionally kept; no working implementation yet |
| `parked` | Valuable, but not on the current roadmap |
| `hardening-backlog` | Quality / reliability / maintenance work to revisit |

## Planned Operational Features

### Known-Issues Skip List

- Status: `planned`
- Skip known-bad `library/version/RID` combos in CI instead of repeatedly burning time on predictable failures.
- Intended artifact: `build/known-issues.json`.
- Preserve:
  - Shape for keyed entries (`library/version/RID` or equivalent).
  - CI behavior: skip vs warn vs hard-fail.
  - Expiry / revalidation policy so the list does not become permanent sediment.
  - Guidance for proving an item can be removed after upstream fixes.

### Recovery Procedures (beyond PD-8 manual escape)

- Status: `planned`
- Package yanking strategy (internal + public feeds).
- Internal / public feed rollback procedures.
- Temporary artifact backups during publish.

### Maintenance Mode / Health Checks

- Status: `planned`
- Scheduled feed health checks.
- Stale package validation.
- `known-issues.json` drift detection.
- Recovery time benchmarking.

## Hardening Backlog

### Invariant-Culture Logging Hygiene

- Status: `hardening-backlog`
- Preserve:
  - Keep numeric / date logging culture-invariant anywhere the build host prints metrics or timestamps.
  - If invariant-format logging appears in multiple tasks, factor a tiny helper instead of repeating ad hoc `string.Create(CultureInfo.InvariantCulture, ...)` shapes.

### Optional Coverage Signal

- Status: `parked`
- ADR-002 §14 left the door open for coverage to return later as an optional non-blocking signal (not a build-host target concern). The Cake-owned `Coverage-Check` gate retired in S10 (2026-05-08); coverage instrumentation came out of CI entirely.
- Preserve:
  - Re-introduce only when there is concrete motivation (PR-comment summary, dashboard surface, external SaaS, etc.) and a deliberate design.
  - Do NOT reintroduce as a build-host Cake target — that shape was rejected. If it returns, it returns as a CI-side signal owned by the workflow, not a build-host gate.
  - Re-add must include the rationale and the surface it serves; otherwise it stays parked.

### Performance And Caching

- Status: `hardening-backlog`
- Caching for expensive package-info queries; smarter reuse of repeated filesystem or dependency-analysis work.

### Tool Reproducibility

- Status: `hardening-backlog`
- Explicit tool-path selection where environment drift matters; reproducible CI vs local tool resolution guidance.

### Release CI Log Hygiene

- Status: `hardening-backlog`
- Surfaced during inspection of green Release workflow run `25637587638`. No failing job or hidden test failure was found, but several noisy log patterns are worth cleaning up so future failures are easier to spot.
- Preserve:
  - `actions/download-artifact@v8` emits repeated `[DEP0005] Buffer()` deprecation warnings. Likely upstream/action noise, but track in case an action upgrade or pin can remove it.
  - Build-host invocations repeatedly warn that `--vcpkg-dir` and `--vcpkg-installed-dir` were not specified before defaulting to repository-relative paths. Either pass explicit paths from CI or downgrade expected default-path messages.
  - NativeSmoke CMake configure warns that `VCPKG_OVERLAY_TRIPLETS` is manually specified but unused on every RID. Check whether the variable is unnecessary for the native-smoke CMake project.
  - Windows vcpkg setup downloads PowerShell 7.5.4 during compiler hash detection. Consider caching/preinstalling if this remains repeated runtime noise.
  - linux-x64 Harvest logs `Package info not found for dependency gperf, continuing.` for each SDL family. Harmless in the green run, but it may deserve an explicit exclusion or clearer diagnostic.

### Vcpkg Cache Key Mutability + CI Cache Audit

- Status: `hardening-backlog`
- Surfaced post-S12 (2026-05-09). Two distinct concerns surfaced together:
  1. **Cache invalidation cost** (confirmed empirically): every time something busts the cache key (vcpkg.json edit, overlay-triplet/port edit, vcpkg submodule bump), the next release pipeline pays ~18-20 min cold-build cost. Recent example: run `25550425138` (post-S11 retirement on `392c35e`) was a cold MISS (`Cache not found for input keys: vcpkg-bin-windows-2025-...`, `Restored 0 package(s)`, all 29 packages compiled from source). Total pipeline 30 min vs the next run on the same key (run `25575839739`) which HIT cleanly and finished in 12 min. Pattern repeats whenever `vcpkg-overlay-triplets/**` or `vcpkg-overlay-ports/**` content changes. The S11 strategy retirement touched overlay triplets, busting the cache.
  2. **Silent-recompile risk** (latent, not yet observed): cache key includes `windows-2025` / `macos-15-intel` / `macos-26` / `windows-11-arm` (mutable runner labels) but NOT runner image version or compiler ABI hash. If GitHub patches MSVC / Apple updates Xcode CLT / Linux container content shifts, the GH Actions cache HIT can be a false positive while vcpkg's compiler-ABI hash differs, forcing 29-package recompile (5-30 min) WITHOUT any "cache miss" log indicator. Deniz reports having hit this in past projects.
- Cache key composition (current — `vcpkg-setup` action.yml lines 122-135):
  - `vcpkg-bin-{platform-identity}-{triplet}-{hashFiles(vcpkg.json + overlay-triplets/** + overlay-ports/**)}-{vcpkg submodule SHA}`
  - `platform-identity` = container digest for Linux (immutable) OR raw runner label for Windows + macOS (mutable)
- Concrete fixes (Phase X "CI cache audit" candidate):
  1. **Include runner image version in cache key for Windows + macOS** — guard against silent MSVC / Xcode-CLT patches. Inject the runner image version (e.g., from `Image Release` field in the job log header, or `runner.image_version` if exposed). Busts cache deterministically on runner-image rolls.
  2. **Cache `external/vcpkg/downloads/` alongside `.vcpkg-cache`** — Windows runs currently re-download CMake 4.2.3 (~9s) and PowerShell 7.5.4 (~22s) every run because vcpkg's vendored tools live outside the cache path. Adding the downloads folder saves ~31s per Windows run × 3 Windows RIDs = ~1.5 min total per release pipeline.
  3. **Pre-warm the cache on master push** (separate trigger): a workflow that runs `vcpkg-setup` only on every master push primes the cache for subsequent release pipelines. Costs one cold-build-equivalent run after a cache-busting commit; saves all subsequent release pipelines from paying it.
  4. **Consider matching Linux's container-digest approach for Windows** — Linux already uses `ghcr.io/janset2d/sdl2-bindings-linux-builder@sha256:...` (immutable digest). Windows can't easily container-fy, but baking a Windows runner image with prebuilt vcpkg + CMake + PS7 would be equivalent at higher infra cost.
- Other variance hotspots from the same investigation (separate from caching):
  - **macOS `platform-build-prereqs` (42-63s)** — `brew install` of build tooling on every run. Cache `/opt/homebrew` keyed on a brewfile hash. Largest single optimization win across all platforms.
  - **Windows checkout submodule init (28-50s)** — `submodules: false` + separate `git submodule update --init --depth=1 external/vcpkg` step. Saves ~10-15s.
  - **Defender exclusion for Windows `.vcpkg-cache/`** — `Add-MpPreference -ExclusionPath` could shave ~5-10s off NTFS extraction.
- Preserve:
  - Run `25575839739` had cache key working correctly (cold-build paid by previous run `25550425138`).
  - Real symptom: ~18-min penalty per cache-busting commit. Confirmed empirically. Worth a dedicated "CI cache audit" Phase X slice once P7-P9 boss fights settle.

### Windows Runner Architecture Plan (3 CPU architectures)

- Status: `parked` (current setup is correct; preserve for cache key plan)
- Surfaced post-S12 (2026-05-09). Manifest maps Windows RIDs to runners as:
  - `win-x64` → `windows-2025-vs2026` (Intel x64 native; migrated from `windows-2025` 2026-05-09 ahead of 2026-05-12 GitHub deprecation) ✅
  - `win-arm64` → `windows-11-arm` (Windows 11 Desktop ARM64 image, native ARM hardware) ✅
  - `win-x86` → `windows-2025-vs2026` (Intel x64 host cross-compiling x86 via MSVC `/arch:IA32`; migrated 2026-05-09) ✅
- Verification per public docs:
  - Windows arm64 runners GA Sep 2024 ([changelog](https://github.blog/changelog/2024-09-03-github-actions-arm64-linux-and-windows-runners-are-now-generally-available/)); public-repo preview Apr 2025 ([changelog](https://github.blog/changelog/2025-04-14-windows-arm64-hosted-runners-now-available-in-public-preview/)); private-repo standard Jan 2026 ([changelog](https://github.blog/changelog/2026-01-29-arm64-standard-runners-are-now-available-in-private-repositories/)). Label: `windows-11-arm`, 4 vCPUs free in public, Windows 11 Desktop image with full toolchain.
  - **No native Windows x86 (32-bit) hosted runner exists.** GitHub Actions only ships 64-bit Windows. x86 builds must cross-compile from x64 host using MSVC's vendored x86 toolchain (vcpkg's `x86-windows-hybrid` triplet handles this transparently). Confirmed via vcpkg discussions and CI cross-build write-ups.
- Verdict: **current mapping is correct for all 3 Windows architectures.** Deniz's intuition was half-correct (win-x86 IS x64-host cross-compile) and half-incorrect (win-arm64 is native, not x64-host).
- Pending action items (next CI cache audit slice):
  1. **Pin runner image versions explicitly** for cache-key stability (see Vcpkg Cache Key Mutability entry above). Per-arch consideration: each Windows runner label rolls independently — `windows-2025-vs2026` patches affect both win-x64 and win-x86 (same cache key bust); `windows-11-arm` patches affect only win-arm64.
  2. **No need to add native x86 runners** — they don't exist on GitHub Actions and cross-compile from x64 is the standard pattern. vcpkg's binary cache works correctly across host/target arch since the cache key includes the target triplet.
  3. **Optional: investigate larger ARM64 runner** — public-repo ARM64 runners ship 4 vCPUs; if private-repo conversion happens later, larger runners (8/16/32 vCPU) may speed Harvest on win-arm64 (currently 1m38s-2m18s).

## Packaging, Supply Chain, And Release Detail

### SBOM Generation

- Status: `planned`
- Software bill of materials generation for native and managed artifacts.

### Native Symbol Handling

- Status: `planned`
- Managed `.snupkg` publication is live. Deferred: native symbol handling strategy (per-platform `.pdb` / `.dSYM` / `.debug` payloads + symbol server publish).

## Retention Rule

Retired material disappears only after the useful parts are either:

- moved into canonical docs ([`plan.md`](plan.md), phase docs, playbook, knowledge-base, [`post-refactor-cleanup-plan.md`](post-refactor-cleanup-plan.md)), or
- preserved here as an intentionally parked thread, or
- explicitly rejected (with the why captured).
