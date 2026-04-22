---
name: "S11 Slice E follow-up pass continuation"
description: "Priming prompt for the next agent continuing the Slice E follow-up pass after P1–P4 + P4b (CI iteration fixes) landed on master. Three concrete outstanding items: (1) NativeSmoke win-arm64 cross-compile execution fail, (2) ConsumerSmoke 0/7 investigation, (3) Deniz-directed CI-layer collapse of Harvest + NativeSmoke into a single matrix job. Validate current repo state, continue per §2.2 / §6.6, no commits without explicit approval."
argument-hint: "Optional focus area, constraints, or whether implementation is already approved for a specific item"
agent: "agent"
model: "Claude Opus 4.7 (1M context)"
---

You are a software engineer entering `janset2d/sdl2-cs-bindings` mid-pass, after the **ADR-003 pass-1 merged to master** at `bfc6713` (Slice A→C + Slice E partial) and the **Slice E follow-up pass P1 → P4 + P4b CI iteration fixes** have subsequently landed on master. Your default job is to **continue** the Slice E follow-up pass — closing P5 → P8 — with three open issues from the P4c CI witness observation (run [`24786888297`](https://github.com/janset2d/sdl2-cs-bindings/actions/runs/24786888297)) as the immediate priority.

## First Principle

Treat every claim in this prompt as **current-as-of-authoring** and **verify against live repo + git log + plan docs before acting**. Drift is expected. If something here contradicts what you observe, trust the observation and report the drift to Deniz before proceeding.

The Slice E follow-up pass is running as **master-direct commits** (no dedicated branch) per Deniz direction 2026-04-22 — pass-1 already end-of-passed the long-branch cadence, the follow-up work is CI infrastructure polish + witness not ADR surface evolution. Do not open a new branch without cause.

## Where the Pass Is

- **Master HEAD** `eecc921` as of session handoff (verify via `git log --oneline -1`).
- **Commits on master since the `bfc6713` merge commit** (follow-up-pass timeline):
  1. `d9b3217` — feat(adr003): slice E partial (E1a-b + E2 infra) — cake-host FDD + Dockerfile + build-linux-container workflow (pre-pass, landed on the branch before the merge)
  2. `bfc6713` — Merge branch 'feat/adr003-impl' — pass-1 end-of-pass merge
  3. `a147675` + `5a75130` + `f458bc7` — early post-merge fixes (pass closure + focal base + `ubuntu-24.04-arm` arm64 runner)
  4. `3a6ffd9` / `f9ad299` — feat(slice-e-followup): P1+P2+P3+P4 — composites + build-and-test-cake-host + prereq absorption
  5. `5cd87fb` — fix(workflows): update restore command to target specific test project
  6. `c3e230c` — fix(ci): tracked coverage baseline + TUnit HTML auto-upload pattern
  7. `907ec86` — fix(macOS): improve Homebrew prereqs installation with idempotent checks
  8. `7fa724b` — fix(docker): install GCC 11 in builder image for modern vcpkg SIMD
  9. `eecc921` — feat(build): enhance MSVC target architecture support and update Dockerfile for CMake 3.21+
- **Build-host tests: 446/446 green** on Windows host, ~720ms. Trajectory: pre-pass 426 → post-P4b 446 (+20 `MsvcTargetArchTests`).
- **Coverage ratchet:** line 66.37% / branch 59.43%, floors 65.0 / 58.0 (reviewed 2026-04-22).
- **GHCR builder image:** `ghcr.io/janset2d/sdl2-bindings-linux-builder:focal-latest` — public, multi-arch (amd64 + arm64), contains: ubuntu:20.04 base (glibc 2.31), GCC 11.5 via `ubuntu-toolchain-r/test` PPA, CMake 3.27+ via Kitware APT, ninja, full `prepare-native-assets-linux.yml` apt preamble (SDL2 dev packages + freepats + libicu + autoconf 2.72 from source), `ENV CC=/usr/bin/gcc-11 CXX=/usr/bin/g++-11`, `git config --system --add safe.directory '*'`. Operator rebuilds via `workflow_dispatch` on `build-linux-container.yml`.
- **Manifest.runtimes[].container_image** for linux-x64 + linux-arm64 → `ghcr.io/janset2d/sdl2-bindings-linux-builder:focal-latest` (multi-arch OCI manifest list). Windows/macOS RIDs → `null`.

### Last CI run state (run 24786888297, 2026-04-22)

| Job | Status | Notes |
|-----|--------|-------|
| build-cake-host (Build + Test Cake Host) | ✓ | 446/446, Coverage-Check PASSED, TUnit HTML auto-uploaded |
| resolve-versions / generate-matrix / preflight | ✓ | clean |
| Harvest (× 7 RID) | **7/7 ✓** | GCC 11 fixed libyuv arm64 `i8mm` + libwebp x64 `_mm256_cvtsi256_si32` linker; Kitware CMake 3.27+ added schema-v3 preset support |
| ConsolidateHarvest | ✓ | clean |
| NativeSmoke (× 7 RID) | **6/7 ✓ / 1 ✗** | win-arm64 fails at the *run-binary* step (see §Issue 1 below) |
| Pack | ✓ | sdl2-net concrete-family filter lands versions properly |
| ConsumerSmoke (× 7 RID) | **0/7 ✗** | all seven RIDs fail — investigation pending (see §Issue 2 below) |
| Publish (Staging / Public) | gated `if: false` | Phase 2b |

## Your Primary Mission

**Close the three P4c-surfaced issues in order**, then proceed to P5 (lock-file) → P6 (PublishTask stubs) → P7 (three-platform witness) → P8 (retirement + doc sweep tail).

The three P4c issues are the natural next session's opening surface:

### Issue 1 — NativeSmoke (win-arm64) cross-compile execution

**Symptom** (from run 24786888297):
```
cmake --preset win-arm64 + MsvcDevEnvironment x64_arm64 cross-compile + build → OK
Linking C executable native-smoke.exe → OK
Attempting to launch native-smoke.exe →
  Error: An error occurred trying to start process '.../native-smoke.exe' with
  working directory '.../sdl2-cs-bindings'. The specified executable is not a
  valid application for this OS platform.
```

Root cause: the CI matrix runs win-arm64 on `windows-latest` (Windows x64 runner). MSVC cross-compiles an ARM64 `.exe` successfully, but the x64 host cannot execute an ARM64 binary. The same will happen for win-x86 if we ever add runtime smoke on x86 (32-bit executable *can* run on x64 Windows via WOW64, but the current job is green for win-x86 because binary execution succeeds via WOW64 — verify this is actually happening and not just "build pass / execution skipped").

Three solution paths (Deniz direction pending):

- **(a) Native ARM64 runner.** `manifest.runtimes[win-arm64].runner` → `windows-11-arm` (GitHub hosted, currently in preview but publicly available for public repos as of 2025). No Cake code change; `IMsvcDevEnvironment` would then source `arm64` vcvars (native, not cross-compile) and binary execution is native ARM64 → runs on the ARM64 host. Check runner label naming at `https://github.com/actions/runner-images` before pinning.
- **(b) Cake-level skip-with-log when host != target.** `NativeSmokeTaskRunner.RunNativeSmokeBinary` detects host CPU architecture via `RuntimeInformation.OSArchitecture`, compares to target RID's architecture, and if they mismatch logs "built successfully; skipping execution because host {X} cannot run {Y}" and returns success. Preserves the build-side validation (linker, symbol resolution) without blocking the matrix. Consistent with the smoke-witness spirit — build proves the binary *could* run, execution proves it actually does on matching host.
- **(c) QEMU / user-mode emulation.** Not practical on Windows runners. Skip.

Recommended: **(b) for the immediate fix** (retains current matrix shape, no infra change), with **(a) logged as a Phase 2b improvement** once native ARM64 runners stabilize on public repos. Open for Deniz's call.

### Issue 2 — ConsumerSmoke 0/7 all-RID fail

**Symptom:** all seven ConsumerSmoke jobs in run 24786888297 failed. `gh run view 24786888297 --job <id> --log-failed` outputs truncated post-cleanup logs; the actual failure step is above the cleanup tail. Strategy: fetch the real `Run PackageConsumerSmoke` step output via `gh run view --job <id> --log` (not `--log-failed`) and grep for the first error signal.

**Hypotheses** (investigation order):
1. **NuGet feed path mismatch.** `release.yml` ConsumerSmoke downloads `nupkg-output` artifact to `artifacts/packages`; `PackageConsumerSmokeRunner` (via `LocalArtifactSourceResolver`) reads from... check `IPathService.GetPackagesOutputDirectory()` vs artifact-download path. If the Cake host expects `artifacts/nupkgs/` but artifact lands at `artifacts/packages/`, restore fails.
2. **Linux container `libicu` runtime.** Our builder image apt-installs `libicu*` via the `apt-cache` shim. `dotnet` on Linux needs specific ICU versions (typically libicu67 for focal). If `dotnet test` fails to initialize ICU, consumer-smoke can't even start.
3. **TUnit assertion surface.** Slice C.8a's runner-strict `--explicit-version` + the PackageConsumer.Smoke csproj's `JansetSmokeSdl2Families` glob — if the consumer csproj expects families that aren't in the passed `--explicit-version` mapping (e.g., expects `sdl2-net` but we filter it out at the CI layer), a TUnit assertion fires.
4. **Feed index / NuGet.config.** Local feed path must be registered as a NuGet source for the consumer csproj restore. The `Janset.Local.props` stamping was retired as a runner input (Slice C.8), so the runner passes `-p:RestoreSources=<local-feed>` or `--feed-path` explicitly. Verify the CLI contract matches the consumer csproj's restore invocation.

Start with **`gh run view 24786888297 --job 72536450199 --log | less`** (win-x64 ConsumerSmoke, best chance of isolating a non-platform-specific failure). Pattern-match with Linux and macOS logs to separate "all-platform issue" (hypothesis 1 / 3 / 4) from "Linux-specific" (hypothesis 2).

### Issue 3 — Harvest + NativeSmoke CI-layer collapse (Deniz-directed)

Deniz's 2026-04-22 direction:

> "NativeSmoke dan geçmemiş Harvest bizim için failed harvest'dır yani Native Smoke harvest'ın gaurdrail'i diyebiliriz bence. Native smoke'u Harvest'a collapse edelim en mantıklı karar bence o."

Semantic argument: NativeSmoke is a *guardrail* for Harvest. A Harvest RID that produced binaries which fail to load / run is a failed Harvest. Collapse them into one CI matrix job; the **Cake task graph stays flat** (Harvest and NativeSmoke remain independent Cake stages — ADR-003 §3.3 is not affected), only the CI job topology merges.

Shape (proposed):

```yaml
harvest:
  name: Harvest + NativeSmoke (${{ matrix.rid }})
  needs: [build-cake-host, preflight, generate-matrix]
  # ... checkout + setup-dotnet + platform-build-prereqs + vcpkg-setup + nuget-cache
  steps:
    - ... download cake-host
    - name: Run Harvest
      run: dotnet ./cake-host/Build.dll --target Harvest --rid ${{ matrix.rid }}
    - name: Run NativeSmoke (guardrail for Harvest)
      run: dotnet ./cake-host/Build.dll --target NativeSmoke --rid ${{ matrix.rid }}
    - name: Upload harvest output
      uses: actions/upload-artifact@v7
      # NativeSmoke-fail = upload skipped by default = Consolidate won't see the RID
      # → Pack / ConsumerSmoke excludes that RID. That IS the desired semantics.
      with: { name: harvest-output-${{ matrix.rid }}, path: artifacts/harvest_output }
```

Delete the separate `native-smoke` matrix job entirely. `ConsolidateHarvest` `needs: [..., harvest]` → still correct (the merged job still publishes the `harvest-output-{rid}` artifact).

**Side benefit:** `vcpkg-setup` composite action runs once per matrix RID instead of twice (Harvest + NativeSmoke). Binary cache already made the second invocation cheap, but removing it cleans the graph.

**PA-2 caveat:** when Issue 1 is resolved via option (b) — "skip execution on host/target mismatch" — NativeSmoke becomes reliably green on all 7 RIDs (build always works; execution only blocks when host matches target, skipping otherwise). The collapsed job's gate semantics are then clean: any Harvest RID that truly fails to link / build signals a real problem, not PA-2 noise.

**If Issue 1 is solved via option (a) (native ARM64 runner):** even cleaner. Execution always happens on matching host.

**Recommended order:** Issue 1 (b) → Issue 3 collapse → Issue 2 investigation (some ConsumerSmoke fails may be upstream of the collapse, some may not; solving 1 + 3 first ensures the harvest artifact the collapse produces is trusted input to ConsumerSmoke).

## Mandatory Grounding (read these first, in order)

1. `AGENTS.md` — operating rules, approval gates, communication style.
2. `docs/onboarding.md` — strategic decisions + repo layout.
3. `docs/plan.md` — current phase + roadmap (updated to 2026-04-22 Slice A→C pass-1 closure + Slice E follow-up pass open).
4. `docs/phases/phase-2-release-cycle-orchestration-implementation-plan.md` — §2.1 (closed pass-1 progress log), §2.2 (Slice E follow-up pass table — **P1–P4 + P4b DONE, P4c observation entry recording the run 24786888297 matrix**), §6.6 Slice E scope, §11 open questions (Q17 still open: delegate-hook tech-debt), §14 change log v2.14 for this session's story.
5. `docs/decisions/2026-04-18-versioning-d3seg.md` (ADR-001) — D-3seg versioning + package-first consumer contract.
6. `docs/decisions/2026-04-19-ddd-layering-build-host.md` (ADR-002) — DDD layering + interface three-criteria rule + `LayerDependencyTests` catchnet.
7. `docs/decisions/2026-04-20-release-lifecycle-orchestration.md` (ADR-003) — three axes, resolve-once, stage-owned validation, Option A SetupLocalDev. §3.3 v1.6 amendment for the B2 design shift. PD-13 formally closed in Slice C.12.
8. `docs/playbook/cross-platform-smoke-validation.md` — Cake-first smoke matrix, A-K checkpoints (P7 witness target).
9. `docs/playbook/unix-smoke-runbook.md` — manual Unix witness runbook.
10. `tests/scripts/README.md` + `tests/scripts/smoke-witness.cs` — the file-based witness app. Deniz local ran `dotnet smoke-witness.cs ci-sim` successfully at 9/9 PASS on 2026-04-22 after `eecc921` — MSVC arch changes proven zero-regression on win-x64.
11. `.github/workflows/release.yml` — current matrix shape (harvest + native-smoke separate; Issue 3 collapses them).
12. `.github/workflows/build-linux-container.yml` — GHCR image build + monthly cron. If Dockerfile changes for Issue 1/2/3, operator must re-dispatch before next `release.yml` run.
13. `docker/linux-builder.Dockerfile` — focal + GCC 11 + Kitware CMake + full apt preamble. Understand the `Addition (1)` through `(3)` block structure before modifying.
14. `build/_build/Application/Harvesting/NativeSmokeTaskRunner.cs` + `build/_build/Domain/Runtime/MsvcTargetArch.cs` + `build/_build/Domain/Runtime/IMsvcDevEnvironment.cs` + `build/_build/Infrastructure/Tools/Msvc/MsvcDevEnvironment.cs` — the new MSVC target-arch mapping + per-target cache. Issue 1 option (b) modifies `NativeSmokeTaskRunner.RunNativeSmokeBinary` to compare host vs target arch.
15. `tests/smoke-tests/native-smoke/CMakePresets.json` — v3 schema, 7 RIDs × 3 variants. If Deniz's CMakePresets-refactor suggestion (session-end note: "is interactive a separate axis? best-practice research shows CMakeUserPresets for per-developer tweaks, `SMOKE_INTERACTIVE` as a cache variable, not a preset") is re-opened, the refactor lands here.

## Locked Policy Recap (do not re-debate without cause)

- **Pass-1 end-of-pass merge completed** at `bfc6713` (2026-04-22). `feat/adr003-impl` branch archived. Slice E follow-up pass is **master-direct**.
- **Approval gate (AGENTS.md).** Never commit unless Deniz explicitly approves. Never push without approval. Present diff summary + proposed commit message; wait for "commit" / "onayla" / "yap" / "başla".
- **No release tag push until full Slice E follow-up pass closure** (§6.6 gating). `prepare-native-assets-main.yml` (workflow_dispatch) is no longer the operator-facing path — `release.yml` dispatch is the canonical surface now that P1–P4 + P4b landed.
- **Commit shape.** Master-direct follow-up commits are short, focused, "fix(ci):" / "feat(slice-e-followup):" / "docs(adr003):" scope-prefixed. No "one big closure commit" since the branch-merge cadence ended. Individual issue fixes = individual commits, aligned with Deniz's "atomic CI fix" preference.
- **Cake-native, repo-native (§3.4).** New code uses `ICakeContext.FileSystem`, `IPathService`, Cake's `FilePath` / `DirectoryPath`. CLI wrappers follow the `Tool<TSettings>` / `Aliases` / `Settings` triad. **Zero raw `JsonSerializer.Serialize` / `Deserialize` calls** outside `Build.Context.CakeExtensions`.
- **Runner-strict `--explicit-version` (Q5a decision, locked Slice C.8).** `PackageConsumerSmokeRunner` throws on empty `request.Versions`. `Janset.Local.props` is an IDE artefact, not a runner input. Don't revert this.
- **NativeSmoke is symmetric 7-RID at the matrix layer.** Pre-collapse the issue 3 proposal — this still holds. Post-collapse, it's "Harvest + NativeSmoke" symmetric 7-RID.
- **Caller gates platform, resolver asserts platform (CA pattern).** `IMsvcDevEnvironment.ResolveAsync` throws `PlatformNotSupportedException` on non-Windows; caller must gate via `OperatingSystem.IsWindows()`.
- **MSVC target-arch fail-fast.** `MsvcTargetArchExtensions.FromRid(string)` throws `PlatformNotSupportedException` on unknown RID. Do not add a default fallback — silent misdetection is the anti-pattern (compiles the wrong binary, surfaces at consumer load time).
- **glibc 2.31 is a hill Deniz dies on.** Any Dockerfile change that would bump the builder image's glibc floor (e.g., bullseye → bookworm, jammy → noble as default base) is a **hard no** without a Deniz-explicit re-opening of that decision. The GCC 11 upgrade via PPA preserves glibc 2.31 — this pattern is the template for any future compiler bump.

## Session Lessons (critical context — carry forward)

These bind the next session because they define how the P4b iteration decided things you must maintain.

### 1. Silent gitignore + coverage baseline = an hour of confused CI

`build/coverage-baseline.json` was shadow-ignored by the `coverage*.json` glob in `.gitignore` for months. Nobody noticed because local runs had the file present; CI saw it missing only once the Coverage-Check gate was wired. Lesson: when adding a config file that sits near a product-of-build artefact, grep `.gitignore` for the filename *before* considering it tracked. The whitelist exception pattern (`!build/coverage-baseline.json` after the coverlet glob) is now the canonical answer.

### 2. Kitware APT for CMake is non-optional on focal

Focal's default CMake is 3.16.3, which lacks `CMakePresets.json` schema v3 support (schema v3 requires CMake ≥ 3.21). The failure mode is **silent**: `cmake --preset linux-x64` on 3.16.3 interprets the preset name as a positional source directory and reports "source directory does not exist". Lesson: when adding a new tool to a container image that a consumer (here, CMakePresets.json v3 authored by Cake.CMake addin + our own file) depends on, verify version compatibility with the consumer, not just "apt has a package by this name."

### 3. `update-alternatives` master vs slave is a Debian Policy constraint, not a style choice

`cc` and `c++` are registered as **master** alternatives on Debian/Ubuntu (Debian Policy §15.4) — they are not slaves of `gcc` / `g++`. Trying to register `--slave /usr/bin/cc cc /usr/bin/gcc-11` fails with `alternative cc can't be slave of gcc: it is a master alternative`. The canonical fix is three `--install` calls: `gcc` (with `g++` + `gcov` slaves), `cc` (standalone), `c++` (standalone). Additionally, `ENV CC=/usr/bin/gcc-11 CXX=/usr/bin/g++-11` is belt-and-suspenders for CMake / autotools ports that read those env vars directly instead of traversing the `/usr/bin/cc` symlink.

### 4. SLSA attestation conflicts with GHCR retention cleanup

`docker/build-push-action@v5` defaulted to `provenance: max` which produces both a runtime manifest and an attestation manifest per platform. The attestation manifests are tagless and appear to GHCR's retention API as "untagged versions"; `actions/delete-package-versions@v5` with `delete-only-untagged-versions: false` + `min-versions-to-keep: 3` swept one of the runtime manifests, breaking `docker pull` with a 404. Fix is two-pronged: `provenance: false + sbom: false` on `build-push-action`, and retention switched to `delete-only-untagged-versions: true + min-versions-to-keep: 5`. Don't re-enable attestation without also reworking the retention logic to identify and protect manifest-list referenced children.

### 5. MSVC target-arch must flow from RID to vcvarsall, not from host

Pre-P4b, `MsvcDevEnvironment` hard-coded `vcvarsall.bat x64` using `RuntimeInformation.OSArchitecture` as input. That works for win-x64 only; win-x86 needs `x64_x86` and win-arm64 needs `x64_arm64` cross-compile args on an x64 host. The fix introduced `MsvcTargetArch` enum + `FromRid` mapping + `ToVcvarsArg` host×target combo builder, wired through `ResolveAsync(MsvcTargetArch, CT)` with per-target-arch `ConcurrentDictionary` cache. Don't regress this: any new NativeSmoke / Pack / Publish stage that invokes MSVC tooling must accept a rid parameter and pass it through to `IMsvcDevEnvironment`.

### 6. `sdl2-net` placeholder is a permanent ConsumerSmoke / Pack filter requirement

`sdl2-net` is in `manifest.json` as a placeholder (`managed_project: null`, `native_project: null`) because the binding work isn't scheduled until Phase 3 (SDL2 Complete). `ManifestVersionProvider.ResolveAsync` includes sdl2-net in its output because the scope filter uses family-name presence, not concreteness. `PackageTaskRunner` strict-rejects placeholders at pack time. Therefore both Pack + ConsumerSmoke CI steps need the `jq concrete-family filter` (`select(.managed_project != null and .native_project != null)`) **until Phase 3 makes sdl2-net concrete**. Any new consumer CI step added in the follow-up pass (Publish, etc.) inherits the same requirement.

### 7. GitHub macOS runners preinstall the brew formulas

GitHub macOS runner images (all `macos-*` labels as of 2025) preinstall `pkg-config`, `autoconf`, `automake`, `libtool`. The brew install step emits "already installed" warnings per-formula, which is noise but not a failure. The idempotent per-formula check (`brew list --formula "$f" >/dev/null 2>&1 || brew install --formula "$f"`) kills the noise + still installs when a formula is genuinely missing (runner image drift protection). Don't simplify to an unconditional `brew install`; the warnings waste log lines and confuse triage.

### 8. Dockerfile addition numbering is load-bearing for future readers

The Dockerfile header comment enumerates "Three additions over the prepare-* preamble": (1) cmake + ninja (now via Kitware + focal universe), (2) GCC 11 + `ENV CC/CXX`, (3) `safe.directory '*'`. If a future session adds a fourth (e.g., pinning `dotnet` SDK in the image, or adding `tar` build prereqs), bump the numbering in both the header block and the inline section comment. A mismatch between header and inline numbering will silently mislead.

## What NOT to Do (Failure Modes To Avoid)

- **Don't bump glibc floor.** Focal is the hill — GCC 11 via PPA is the template for any future compiler bump that preserves glibc. Any proposed move to a newer base image (bullseye / jammy / noble) requires explicit Deniz approval, and the default answer is no.
- **Don't merge to master from a feature branch.** Slice E follow-up pass is master-direct. No `git merge --no-ff`; individual commits land on master directly with Deniz approval per commit.
- **Don't squash follow-up commits.** Per-commit bisectability matters when a future session debugs which fix caused which behaviour change.
- **Don't introduce raw `JsonSerializer.Serialize` / `Deserialize` anywhere outside `CakeExtensions`.**
- **Don't break `LayerDependencyTests`.** New Task classes read from `BuildContext.*` properties, not Domain-service DI.
- **Don't re-introduce the `Janset.Local.props`-as-runner-fallback branch in ConsumerSmoke.** Runner-strict `--explicit-version` is locked.
- **Don't propagate the delegate-hook pattern beyond `PackageTaskRunner.ResolveHeadCommitSha`.** `GitTagVersionProvider` + any new Cake.Git test call sites use the `TempGitRepo` integration fixture. Q17 is still open — a Slice E follow-up pass P8 item is to resolve it (either extend ADR-002 §2.3 to cover delegate hooks, or retire the hook in favour of a temp-repo fixture).
- **Don't skip the concrete-family filter on Pack / ConsumerSmoke / any new consumer step.** sdl2-net (and any future placeholder family) must be filtered out client-side until the family becomes concrete.
- **Don't add a default `MsvcTargetArch` fallback in `FromRid`.** Fail-fast on unknown RID is the design — a silent default compiles the wrong binary and surfaces at consumer load time.
- **Don't change `release.yml` job topology without flagging the semantic implication.** Issue 3's Harvest+NativeSmoke collapse is explicitly green-lit by Deniz; other matrix / job graph changes need the same explicit go-ahead because they affect ConsolidateHarvest / Pack / ConsumerSmoke dependency semantics.
- **Don't re-enable SLSA provenance / SBOM attestations on `build-linux-container.yml` without reworking retention logic.** (See Session Lesson 4.)

## Communication Preferences

- Deniz communicates in Turkish + English interchangeably. Answer in whichever language he used, or a mix.
- Narrate findings + propose + wait for ack before commit/push/close. No chaining from discovery to action.
- Be direct. Challenge decisions when needed; don't be yes-man.
- When in doubt, stop and ask. "No scope creep on critical findings" per memory — if an issue 1/2/3 diff uncovers a cross-scope concern, isolate the current delta and file the concern as a follow-up commit or later phase.
- Validate non-trivial claims with WebSearch / WebFetch / doc-read before proposing code — Deniz explicitly values "internetten valide et, dönüp dönüp birşey düzeltmeyelim". `update-alternatives` split, Kitware APT pattern, GCC 11 PPA for focal — all were validated via search before landing.

## Definition of Done for Your Session

### Default (close the three P4c issues + make progress on P5–P8)

Progress the Slice E follow-up pass forward from the P4c observation state, closing with:

- Each issue (1 → 3 → 2, or Deniz-preferred order) lands as an **individual focused commit** on master with Deniz approval. Build + tests green after each; Coverage-Check ratchet holds.
- After Issue 3 lands: `release.yml` shows one matrix job per RID for "Harvest + NativeSmoke"; the separate `native-smoke` job is deleted; ConsolidateHarvest / Pack / ConsumerSmoke dependency graph unchanged.
- After Issue 2 lands: ConsumerSmoke ≥ 5/7 green (win-x64 + linux-x64 + osx-x64 + osx-arm64 + linux-arm64 — the five concrete-native-runner RIDs). win-x86 + win-arm64 ConsumerSmoke may still fail if they exercise a matching runtime execution constraint (x86 binaries run on x64 via WOW64 so win-x86 consumer-smoke *should* pass; win-arm64 consumer-smoke runs actual .NET Test on x64 runner which *cannot* load arm64 RID natives — this parallels Issue 1's host-vs-target constraint).
- After Issue 1 lands: depending on the chosen path, either (a) native ARM64 runner green or (b) Cake-level skip-with-log prints the skip and NativeSmoke job exits 0. Either way, the matrix reports NativeSmoke 7/7 "green or skipped with reason."
- P5 (lock-file): `build/_build/packages.lock.json` + `build/_build.Tests/packages.lock.json` generated + committed; `<RestorePackagesWithLockFile>true</RestorePackagesWithLockFile>` in the relevant csprojs; CI restores use `--locked-mode`. Incidental validation: when someone opens a PR that bumps a PackageReference, CI fails on lock drift until the lockfile is regenerated.
- P6 (PublishTask stubs): landing body throws `NotImplementedException` with a Phase-2b message.
- Plan doc §2.2 progress table updated in the same cadence as each landed commit; §14 change log v2.15+ entries chronicle the issue closures.

### Optional (pass closure)

P7 (three-platform CI witness per `cross-platform-smoke-validation.md` A-K checkpoints) + P8 (retire `prepare-native-assets-*.yml` + `release-candidate-pipeline.yml` + Q17 ADR-002 §2.3 resolution + broader canonical doc sweep tail) close the pass. After P8: no explicit merge (master-direct), no release tag (Phase 2b gating still in effect), just a progress-log flip to "pass closed".

Do not leave master in a half-closed state without a visible handoff note.

## Handoff note from the authoring session (2026-04-22)

- CMakePresets.json developer-experience refactor is **recorded as §11 Q18** (direction-selected 2026-04-22, implementation pending). Research resolved (per CMake docs + martin-fieber.de + Matt Gibson + CLion docs + VS docs) — see §11 Q18 for the full trail and sources. Summary: drop the `*-interactive` preset variants from `CMakePresets.json` (21 → 14 presets), add `CMakeUserPresets.json.example` template + `tests/smoke-tests/native-smoke/README.md` (CLion / VS / CLI usage), `.gitignore` entry for `CMakeUserPresets.json`. Lands in **P8 doc sweep** (§2.2 P8 row already references Q18) or a standalone post-pass commit if P8 churn is too large. Not blocking P4c / P5 / P6 / P7.
- Also captured as a backlog bullet in `docs/plan.md` under **Deferred to Phase 2b / Q3**.
- Local `smoke-witness.cs ci-sim` ran 9/9 PASS on Windows x64 immediately before the session end — `eecc921` (MSVC target-arch refactor) is zero-regression on win-x64. Any new work that touches `NativeSmokeTaskRunner` should re-run ci-sim locally before pushing.
- The `docs/research/temp/*.md` files (chatgpt-discussion, claude-discussion, gemini-discussion) are Deniz's external-AI-consultation scratch. Treat them as read-only session ephemera, not source material to cite.
