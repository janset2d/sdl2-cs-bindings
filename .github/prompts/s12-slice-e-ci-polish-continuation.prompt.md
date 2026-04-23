---
name: "S12 Slice E CI polish continuation"
description: "Priming prompt for the next agent continuing the Slice E follow-up pass. Previous session (S11→S12 bridge) resolved ConsumerSmoke failures (--versions-file, sdl2-net removal, .NET 8 SDK), diagnosed win-x86 hostfxr arch mismatch, and identified linux-x64 vcpkg cache inefficiency. Two open issues remain: (1) win-x86 ConsumerSmoke x86 runtime provisioning, (2) linux-x64 Harvest 11min vcpkg full-rebuild despite cache hit. Plus P5–P8 roadmap tail."
argument-hint: "Optional focus area, constraints, or whether implementation is already approved for a specific item"
agent: "agent"
model: "Claude Opus 4.7 (1M context)"
---

You are a software engineer entering `janset2d/sdl2-cs-bindings` mid-pass. The **ADR-003 pass-1** merged to master at `bfc6713`, and the **Slice E follow-up pass** has been progressing as master-direct commits since 2026-04-22. You are picking up where the previous session (S11→S12 bridge, 2026-04-23) left off.

## First Principle

Treat every claim in this prompt as **current-as-of-authoring (2026-04-23)** and **verify against live repo + git log + plan docs before acting**. Drift is expected. If something here contradicts what you observe, trust the observation and report the drift to Deniz before proceeding.

The Slice E follow-up pass is running as **master-direct commits** (no dedicated branch) per Deniz direction — pass-1 already end-of-passed the long-branch cadence. Do not open a new branch without cause.

## Where the Pass Is

- **Master HEAD** `3fe0303` as of session handoff (verify via `git log --oneline -1`).
- **Commits on master since the `bfc6713` merge commit** (follow-up-pass timeline):
  1. `bfc6713` — Merge branch 'feat/adr003-impl' — pass-1 end-of-pass merge
  2. `a147675` + `5a75130` + `f458bc7` — early post-merge fixes (pass closure + focal base + `ubuntu-24.04-arm` arm64 runner)
  3. `3a6ffd9` / `f9ad299` — feat(slice-e-followup): P1+P2+P3+P4 — composites + build-and-test-cake-host + prereq absorption
  4. `5cd87fb` — fix(workflows): update restore command to target specific test project
  5. `c3e230c` — fix(ci): tracked coverage baseline + TUnit HTML auto-upload pattern
  6. `907ec86` — fix(macOS): improve Homebrew prereqs installation with idempotent checks
  7. `7fa724b` — fix(docker): install GCC 11 in builder image for modern vcpkg SIMD
  8. `eecc921` — feat(build): enhance MSVC target architecture support and update Dockerfile for CMake 3.21+
  9. `b0ccda0` — ci(release): replace jq/mapfile with --versions-file and absorb NativeSmoke into Harvest
  10. `bc652d1` — fix(tests): remove SDL2_net from manifest + update expected counts
  11. `3fe0303` — ci(release): add .NET 8 SDK to ConsumerSmoke setup-dotnet
- **Build-host tests: 455/455 green** on Windows host, ~640ms. Trajectory: pre-pass 426 → post-P4b 446 → post-P4c 455 (+9 from manifest/version tests).
- **GHCR builder image:** `ghcr.io/janset2d/sdl2-bindings-linux-builder:focal-latest` — public, multi-arch (amd64 + arm64), ubuntu:20.04 base (glibc 2.31), GCC 11.5 via PPA, CMake 3.27+ via Kitware APT.

### Uncommitted Working Tree State (IMPORTANT)

Two files are modified but **NOT committed** — they were produced during the S11→S12 bridge session but Deniz halted the commit to discuss the approach:

1. **`.github/workflows/release.yml`** — adds a 20-line conditional step `Install x86 .NET runtimes (win-x86 only)` between `setup-dotnet` and `platform-build-prereqs` in the `consumer-smoke` job. Uses `dotnet-install.ps1` to install x86 .NET 9.0 + 8.0 runtimes and sets `DOTNET_ROOT(x86)` env var.
2. **`docs/phases/phase-2-release-cycle-orchestration-implementation-plan.md`** — P4c resolution + P4d TFM SDK automation research item + v2.15 changelog.

**Deniz explicitly chose NOT to commit these yet** — he wants to discuss the x86 fix approach first (see Issue 1 below). The doc change can be committed once the direction is settled. You may need to `git diff` to see the exact changes, or `git checkout -- .github/workflows/release.yml` if the discussion leads to a different approach.

### CI Run History (relevant)

| Run | Date | Trigger | Key Observations |
|-----|------|---------|------------------|
| [`24786888297`](https://github.com/janset2d/sdl2-cs-bindings/actions/runs/24786888297) | 2026-04-22 | Post-P4b | Harvest 7/7 ✓, NativeSmoke 6/7 (win-arm64 ✗), ConsumerSmoke 0/7 ✗ |
| [`24806134959`](https://github.com/janset2d/sdl2-cs-bindings/actions/runs/24806134959) | 2026-04-23 | Post-P4c (b0ccda0+bc652d1+3fe0303) | Harvest+NativeSmoke collapsed ✓, ConsumerSmoke 6/7 ✓, **win-x86 ConsumerSmoke ✗** |

The second run proved that 3 commits (b0ccda0, bc652d1, 3fe0303) fixed most ConsumerSmoke failures. Only **win-x86** remains broken — root cause is an architecture mismatch (see Issue 1).

## What the Previous Session (S11→S12 Bridge) Accomplished

### Resolved Issues

1. **ConsumerSmoke jq/mapfile portability** — replaced shell-based `jq` + `mapfile` version extraction with Cake's `--versions-file` CLI option. Cross-platform, no shell dependency. Commit `b0ccda0`.

2. **sdl2-net placeholder contamination** — removed `sdl2-net` from `manifest.json` `library_manifests[]` per ADR-003 §2.2. Previously, `ManifestVersionProvider` included sdl2-net versions in the `--versions-file` output, but no matching NuGet package existed → ConsumerSmoke restore failed on all 7 RIDs. Commit `bc652d1`.

3. **Missing .NET 8 SDK in ConsumerSmoke** — `setup-dotnet` only installed .NET 9; the `PackageConsumer.Smoke` project targets `net9.0;net8.0;net462` → `dotnet test -f net8.0` failed. Added `8.0.x` to the setup-dotnet version list. Commit `3fe0303`.

4. **NativeSmoke absorbed into Harvest** — the separate `native-smoke` matrix job was deleted; NativeSmoke now runs as a step inside each Harvest matrix leg. `vcpkg-setup` runs once per RID instead of twice. Commit `b0ccda0`.

5. **win-arm64 NativeSmoke host/target skip** — already handled in `NativeSmokeTaskRunner.RunNativeSmokeBinary` which detects `RuntimeInformation.OSArchitecture` vs target RID arch mismatch and logs skip. Landed in the same `b0ccda0` commit via the NativeSmoke-into-Harvest collapse.

### Diagnosed but NOT Resolved

Two issues were identified, analyzed in depth, and **discussed with Deniz** but not yet committed. See §Open Issues below.

## Open Issues (Your Primary Mission)

### Issue 1 — win-x86 ConsumerSmoke: x86 .NET runtime provisioning

**Root Cause (confirmed):**

CI run `24806134959`, job `ConsumerSmoke (win-x86)` on `windows-2025` (x64 runner):
```
dotnet test -f net9.0 -r win-x86
→ bin\Release\net9.0\win-x86\PackageConsumer.Smoke.exe (x86 binary)
→ TUnit launches x86 exe as separate process
→ x86 process needs hostfxr.dll from x86 .NET installation
→ DOTNET_ROOT(x86) not set → falls back to DOTNET_ROOT (x64)
→ x86 process loads x64 hostfxr.dll → HRESULT 0x800700C1 (ERROR_BAD_EXE_FORMAT)
```

The .NET host resolver for 32-bit processes on 64-bit Windows checks the `DOTNET_ROOT(x86)` environment variable (parenthesized form, **not** the underscored `DOTNET_ROOT_X86`). GitHub Actions runners only ship x64 .NET — there is no x86 runtime preinstalled.

**Key facts from source-code analysis:**
- `PackageConsumerSmokeRunner` passes `-r win-x86` to `dotnet test`, producing an x86 binary.
- `ShouldSkipTfm` has zero x86 awareness — only gates `net4x` + OS platform.
- `net462` is unaffected — .NET Framework CLR handles x86 natively.
- Both `net9.0` and `net8.0` TFMs need x86 runtimes.

**Dilemma: Three approaches discussed with Deniz, no decision yet**

#### Option A: Move x86 logic into `platform-build-prereqs` composite action (Recommended by previous agent)

Add a `rid` input to `.github/actions/platform-build-prereqs/action.yml`. When `rid` contains `x86` on Windows, use `dotnet-install.ps1` to install x86 runtimes and set `DOTNET_ROOT(x86)`:

```yaml
inputs:
  rid:
    description: "Runtime Identifier (e.g. win-x64, win-x86)"
    required: false
    default: ''

steps:
  - name: Install x86 .NET runtimes (win-x86 only)
    if: runner.os == 'Windows' && contains(inputs.rid, 'x86')
    shell: pwsh
    run: |
      $x86Root = "${env:ProgramFiles(x86)}\dotnet"
      $script = Join-Path $env:RUNNER_TEMP 'dotnet-install.ps1'
      Invoke-WebRequest -Uri 'https://dot.net/v1/dotnet-install.ps1' -OutFile $script
      & $script -Architecture x86 -Channel 9.0 -Runtime dotnet -InstallDir $x86Root
      & $script -Architecture x86 -Channel 8.0 -Runtime dotnet -InstallDir $x86Root
      "DOTNET_ROOT(x86)=$x86Root" >> $env:GITHUB_ENV
```

**Pro:** Clean separation of concerns; reusable across jobs; consistent with action's purpose ("platform-specific build prerequisites").
**Con:** Adds a `rid` input to an action that currently takes none. Slightly misleading — "build prereqs" is now also "runtime prereqs." Also, this action is used by both Harvest and ConsumerSmoke — Harvest doesn't need x86 runtimes.

#### Option B: Use `actions/setup-dotnet@v5` with `architecture: x86`

```yaml
- uses: actions/setup-dotnet@v5
  if: matrix.rid == 'win-x86'
  with:
    dotnet-version: |
      9.0.x
      8.0.x
    architecture: x86
```

**Pro:** GitHub-blessed, minimal YAML.
**Con:** Source code analysis of `setup-dotnet@v5` reveals that when `architecture` differs from host, it **overwrites `DOTNET_ROOT`** to `C:\Program Files\dotnet\x86`. This breaks the x64 Cake host which also needs `DOTNET_ROOT`. You'd need to save/restore `DOTNET_ROOT` after the x86 call AND manually set `DOTNET_ROOT(x86)` — at which point you're doing option A's work anyway, but fighting the action's side effects. The action does NOT set `DOTNET_ROOT(x86)` (parenthesized) — it only sets `DOTNET_ROOT` (overwrite).

#### Option C: Inline script in `release.yml` (current uncommitted change)

The current uncommitted diff in `release.yml` — a 20-line conditional step directly in the `consumer-smoke` job. Uses `dotnet-install.ps1` + `DOTNET_ROOT(x86)`.

**Pro:** Works, tested approach.
**Con:** Deniz said "çok çirkin duruyor" (looks very ugly) — he doesn't want raw infra scripts inline in the workflow.

**Deniz's stance:** Leaning toward A but hasn't committed. Wants the agent to consider whether the composite action is the right home or whether a dedicated `install-x86-dotnet` action makes more sense. Key question: will any other job besides ConsumerSmoke ever need x86 runtimes?

### Issue 2 — linux-x64 Harvest takes 11 minutes despite cache hit

**Root Cause (confirmed):**

CI run `24806134959`, linux-x64 Harvest job log shows:
```
Cache restored from key: vcpkg-bin-ghcr.io/janset2d/sdl2-bindings-linux-builder:focal-latest-x64-linux-hybrid-...
Restored 0 package(s) from .vcpkg-cache in 262 us
```

GitHub Actions cache **hit** and restored the 122MB `.vcpkg-cache` tarball. But vcpkg's own binary cache layer **rejected all 42 packages** because ABI hashes didn't match. All 42 were rebuilt from source — total: 10 minutes.

**Why ABI hashes don't match despite a GH Actions cache hit:**

vcpkg's ABI hash for each package includes:
- vcpkg commit SHA
- Portfile content hash
- Compiler path + version string
- Triplet file content (every line)
- Recursive dependency ABI hashes

The GitHub Actions cache key includes:
```
vcpkg-bin-{image-tag}-{triplet}-{hashFiles(vcpkg.json, overlays)}-{vcpkg-commit}
```

The `restore-keys` fallback:
```yaml
restore-keys: |
  vcpkg-bin-...-{overlays-hash}-    # partial match without vcpkg-commit
  vcpkg-bin-...-{triplet}-          # even broader partial match
```

When the vcpkg submodule commit bumps (or overlays change), the second `restore-key` can restore a cache from a **different vcpkg commit**. GH Actions says "cache hit" (prefix matched), but vcpkg sees different ABI hashes → `Restored 0` → full rebuild.

**Top time consumers in the 42-package rebuild:**
| Package | Time |
|---------|------|
| harfbuzz (meson) | **2.9 min** |
| libmount (util-linux) | 1 min |
| sdl2 | 51s |
| alsa | 38s |
| libsystemd | 39s |

harfbuzz alone is ~30% of the total build time.

**Dilemma: Is this chronic or one-time?**

If this was a one-time cache prime (first run after vcpkg submodule bump), subsequent runs with the same commit will have matching ABI hashes and `Restored 42` → near-zero vcpkg time. **The next CI run will tell.**

If it's chronic (happening every run), the cache key strategy needs fixing.

**Possible solutions if chronic:**

| Option | Description | Effort | Impact |
|--------|-------------|--------|--------|
| **A: Tighten restore-keys** | Remove the broad `vcpkg-bin-...-{triplet}-` fallback. Only allow exact + overlay-hash-prefix matches. Prevents restoring stale cache from a different vcpkg commit. | Low | Eliminates false-hit problem; cold starts become explicit misses (longer, but honest) |
| **B: Pin Docker image digest in cache key** | Replace mutable `focal-latest` tag with `sha256:...` digest. Prevents cache pollution when image rebuilds change the compiler environment. | Low | Only helps if image rebuild is the drift source |
| **C: vcpkg NuGet binary caching** | Use GitHub Packages NuGet feed as vcpkg binary cache instead of file-based `.vcpkg-cache`. Per-package granular caching — partial hits possible even across vcpkg commits for unchanged packages. | Medium | Most correct solution long-term; survives any key drift |
| **D: Do nothing, confirm it's one-time** | Wait for next CI run. If `Restored 42` → problem self-resolved. | Zero | May waste CI minutes if chronic |

**Deniz's stance:** Wants to understand the root cause before acting. Agreed to observe the next CI run to determine if chronic.

## Roadmap Tail (P5–P8)

After the two open issues are resolved, the Slice E follow-up pass has four remaining items:

| Item | Description | Status |
|------|-------------|--------|
| P5 | Lock-file discipline: generate `packages.lock.json` for `_build` + `_build.Tests`, add `<RestorePackagesWithLockFile>true</RestorePackagesWithLockFile>`, CI uses `--locked-mode` | Not started |
| P6 | PublishTask stubs: landing body throws `NotImplementedException` with Phase-2b message | Not started |
| P7 | Three-platform witness: confirm CI matrix produces Harvest + Pack + ConsumerSmoke green on win-x64 + linux-x64 + osx-arm64 (minimum viable witness set) | Blocked on Issue 1 + 2 |
| P8 | Retirement + doc sweep: close obsolete docs, update plan.md, clean up parking lot | Not started |

P4d (TFM SDK automation research) was logged as a non-blocking research item — it's not on the critical path.

## Mandatory Grounding (read these first, in order)

1. `AGENTS.md` — operating rules, approval gates, communication style.
2. `docs/onboarding.md` — strategic decisions + repo layout.
3. `docs/plan.md` — current phase + roadmap.
4. `docs/phases/phase-2-release-cycle-orchestration-implementation-plan.md` — §2.2 Slice E follow-up pass table (P1–P4c entries), §6.6 Slice E scope.
5. `docs/decisions/2026-04-18-versioning-d3seg.md` (ADR-001) — D-3seg versioning.
6. `docs/decisions/2026-04-19-ddd-layering-build-host.md` (ADR-002) — DDD layering.
7. `docs/decisions/2026-04-20-release-lifecycle-orchestration.md` (ADR-003) — release orchestration.
8. `.github/workflows/release.yml` — current CI shape (Harvest has NativeSmoke absorbed; ConsumerSmoke has the x86 issue).
9. `.github/actions/platform-build-prereqs/action.yml` — composite action, candidate home for Issue 1 Option A.
10. `build/_build/Application/ConsumerSmoke/PackageConsumerSmokeRunner.cs` — the runner that invokes `dotnet test -r <rid>`.
11. `.github/actions/vcpkg-setup/action.yml` — vcpkg binary cache key strategy (Issue 2).

## Locked Policy Recap (do not re-debate without cause)

- **Master-direct commits** — no feature branches for Slice E follow-up.
- **Approval gate (AGENTS.md).** Never commit/push without Deniz's explicit "go" / "yap" / "başla" / "onayla".
- **455/455 tests green** — do not regress. Any new code should maintain or increase this count.
- **glibc 2.31 is non-negotiable.** Focal base image, GCC 11 via PPA. No image base changes.
- **`DOTNET_ROOT(x86)` parenthesized form** — the .NET host resolver checks this exact env var name for 32-bit processes on 64-bit Windows. `DOTNET_ROOT_X86` (underscored) is NOT what the resolver looks for. Any solution must use the parenthesized form.
- **`dotnet-install.ps1`** — this is the same script `actions/setup-dotnet` uses internally. It's the canonical way to install .NET runtimes outside the action.
- **Concrete-family filter** — sdl2-net is removed from `manifest.json` as of `bc652d1`. If it returns in Phase 3, the filter logic must be re-evaluated.
- **NativeSmoke is absorbed into Harvest** as of `b0ccda0`. The separate `native-smoke` job no longer exists.
- **Runner-strict `--explicit-version`** — `PackageConsumerSmokeRunner` throws on empty versions. `--versions-file` is the CI delivery mechanism.
- **MSVC target-arch fail-fast** — `MsvcTargetArchExtensions.FromRid` throws on unknown RID. No default fallback.
- **No SLSA attestation** on GHCR images until retention logic is reworked.

## Session Lessons (carry forward from S11)

### 1. `actions/setup-dotnet@v5` overwrites `DOTNET_ROOT` on cross-arch

When `architecture` input differs from host, the action sets `DOTNET_ROOT` to `<install-dir>/<arch>` — it does NOT set a separate `DOTNET_ROOT(x86)` or `DOTNET_ROOT_ARM64`. This means a second `setup-dotnet` call with `architecture: x86` will **overwrite** the x64 `DOTNET_ROOT`, breaking the x64 Cake host. Source: [`actions/setup-dotnet/src/setup-dotnet.ts`](https://github.com/actions/setup-dotnet/blob/main/src/setup-dotnet.ts) and [`installer.ts`](https://github.com/actions/setup-dotnet/blob/main/src/installer.ts).

### 2. vcpkg binary cache ABI hash ≠ GitHub Actions cache key

GitHub Actions `actions/cache` uses a string key with prefix matching. vcpkg uses a content-addressed ABI hash that includes compiler version, triplet content, portfile content, and dependency hashes. A GH Actions "cache hit" does NOT guarantee vcpkg will accept the cached binaries. The `restore-keys` fallback can restore a cache from a different vcpkg commit → `Restored 0`.

### 3. `net462` on x86 is a non-issue

.NET Framework CLR handles x86 natively on Windows. The `AnyCPU` platform target runs as x86 or x64 depending on the host OS bitness preference, but .NET Framework's own hostfxr resolution is completely separate from .NET Core's. Only `net9.0` and `net8.0` TFMs are affected by the x86 runtime provisioning issue.

### 4. TUnit launches test exe as separate process

`dotnet test` with TUnit compiles the test project and launches the resulting executable as a separate child process. This is why the architecture mismatch manifests at runtime (the child process loading hostfxr) rather than at compile time. The `dotnet` CLI itself runs as x64 — only the test executable is x86.

### 5. Harvest+NativeSmoke collapse side benefit

The NativeSmoke absorption into Harvest (`b0ccda0`) means `vcpkg-setup` runs once per matrix RID instead of twice. Even when vcpkg binary cache hits perfectly, the bootstrap + cache restore + validation still takes ~30s per invocation. Across 7 RIDs, that's ~3.5 minutes of CI time saved.

## What NOT to Do

- **Don't commit the uncommitted `release.yml` change without resolving the approach discussion with Deniz.** The inline script is a working fix but Deniz wants a cleaner approach.
- **Don't use `actions/setup-dotnet` `architecture: x86` without accounting for `DOTNET_ROOT` overwrite.**
- **Don't assume vcpkg GH Actions cache hit = vcpkg binary cache hit.** They are different layers.
- **Don't bump glibc floor.** Focal is the hill.
- **Don't merge to master from a feature branch.** Master-direct commits only.
- **Don't break `LayerDependencyTests`.** New code follows DDD layering.
- **Don't skip the approval gate.** Present diff summary + proposed commit message; wait for explicit go-ahead.
- **Don't add a default `MsvcTargetArch` fallback in `FromRid`.** Fail-fast is the design.
- **Don't re-enable SLSA attestation** on GHCR images without reworking retention logic.

## Communication Preferences

- Deniz communicates in Turkish + English interchangeably. Answer in whichever language he used, or a mix.
- Be talkative and conversational with practical humor. Challenge decisions when needed.
- Narrate findings → propose → wait for approval before commit/push.
- Validate non-trivial claims with search/docs before proposing code.
- "No scope creep on critical findings" — isolate current delta, file cross-scope concerns as follow-up.

## Definition of Done for Your Session

### Minimum Viable

1. **Issue 1 resolved:** win-x86 ConsumerSmoke passes. The x86 .NET runtime provisioning approach is agreed with Deniz and committed.
2. **Issue 2 triaged:** Either confirmed one-time (next CI run shows `Restored 42`) or fix committed if chronic.
3. **Plan doc updated** to reflect Issue 1 + 2 resolution.
4. **455+ tests green** after each commit.

### Stretch Goals

5. **P5 lock-file discipline** landed.
6. **P6 PublishTask stubs** landed.
7. **P7 three-platform witness** confirmed (requires Issue 1 + 2 green first).
8. **P8 retirement + doc sweep** started or completed.
