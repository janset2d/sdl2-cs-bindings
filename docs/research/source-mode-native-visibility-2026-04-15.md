# Source Mode Native Payload Visibility — Mechanism Decision

**Date:** 2026-04-15
**Status:** Mechanism locked — the **copy mechanism** (staging → `bin/`) is empirically verified on Windows (worktree, 2026-04-15), Linux (WSL Ubuntu, 2026-04-15), and macOS (SSH Intel Mac, Darwin 24.6.0, 2026-04-15). "Verified" here means "the MSBuild-level mechanism delivers the right files to `bin/` with the right shape and correct platform gating." It does **not** mean "real SDL2 natives load at runtime under dyld / ld.so with correct install-names and any code-signing baggage" — that is end-to-end validation, which is pending Stream F implementation with real SDL2 natives. Resolves Pending Decision **PD-4**. **PD-5 direction** (non-host RID acquisition via `--source=remote`) is locked in §7.2 but its concrete mechanism (URL convention, producer workflow, archive contract, auth, caching) is **unresolved and remains 2b scope** — see the open-questions list in §9.
**Scope:** How native payloads (`*.dll`, `*.so`, `*.dylib`) become visible at `bin/$(TargetFramework)/runtimes/$(RID)/native/` for in-tree csprojs that reach native `.Native` projects via `ProjectReference` (not `PackageReference`). The opt-in mechanism is generic (any subtree can opt in via its own `Directory.Build.props`); Phase 2a ships only the `test/` preset, since `test/` is the only non-`src/` tree that currently contains csprojs. Future `samples/`, `sandbox/`, or other subtrees follow the same pattern — each contributes its own `Directory.Build.props` when it comes online. Includes symlink preservation on Linux/macOS.

**Note on evidence:** Three PoCs were run during this research cycle in transient locations (`.claude/worktrees/agent-aca25dd8` on Windows; `/tmp/jansetsrcmode-poc/` in WSL Ubuntu; `/tmp/jansetsrcmode-poc-mac/` on Intel Mac via SSH). None are tracked in repo history. The mechanism decision rests on (a) the Windows PoC verifying platform-gating and `Content` propagation via `ProjectReference` chains, (b) the WSL PoC measuring `<Content CopyToOutputDirectory>` symlink flattening (3× duplication on a 2-level chain) vs `<Exec cp -a>` preservation, (c) the macOS PoC reproducing the same measurement on a 3-level dylib chain (see §5.1 and §5.2), (d) the prior art survey in §6, (e) MSBuild semantic reasoning. The PoC recipes are inlined in §5 so any contributor can reproduce. End-to-end validation with real SDL2 natives will land as part of Stream F execution — see [`docs/phases/phase-2-adaptation-plan.md`](../phases/phase-2-adaptation-plan.md).

**Related docs:**

- [`docs/research/execution-model-strategy-2026-04-13.md`](execution-model-strategy-2026-04-13.md) — established three-mode model (Source / Package Validation / Release) and source-graph vs shipping-graph separation
- [`docs/phases/phase-2-adaptation-plan.md`](../phases/phase-2-adaptation-plan.md) — Stream F owns implementation of the mechanism this doc recommends
- [`docs/knowledge-base/release-lifecycle-direction.md`](../knowledge-base/release-lifecycle-direction.md) — shipping-graph dependency contracts

---

## 1. Problem

The repo ships native SDL2 binaries via per-library `.Native` NuGet packages with the standard `runtimes/<rid>/native/*` layout. Consumers that take a `PackageReference` get the natives placed into their `bin/.../runtimes/<rid>/native/` automatically — either via NuGet's built-in native asset resolution (modern .NET) or via a `buildTransitive/*.targets` shim (.NET Framework).

In-tree test, sandbox, and sample csprojs take a `ProjectReference` chain instead:

```text
test/FooTest.csproj
  └── ProjectReference → src/SDL2.Core/SDL2.Core.csproj
                           └── ProjectReference → src/native/SDL2.Core.Native/SDL2.Core.Native.csproj
```

`ProjectReference` does **not** invoke `buildTransitive/*.targets` (that is a NuGet pack-to-consumer mechanism, not an MSBuild graph mechanism). The `.Native` csproj has `<IncludeBuildOutput>false</IncludeBuildOutput>` and produces no assembly of its own. Without a deliberate mechanism, nothing propagates native payloads to the consumer's `bin/` directory, so P/Invoke at test time fails with `DllNotFoundException`.

This is the **Source Mode** problem described in [`execution-model-strategy-2026-04-13.md §6`](execution-model-strategy-2026-04-13.md) — Source Mode needs natives for the current RID available locally, as fast as possible, without going through pack → local-feed → restore.

## 2. Constraints

From canonical docs:

- Native binaries must not be committed to git (`execution-model-strategy-2026-04-13.md` Principle 2, confirmed by [`plan.md`](../plan.md) Known Issues)
- Source graph (`ProjectReference`) and shipping graph (`PackageReference`) are distinct realities and should not be conflated (`execution-model-strategy-2026-04-13.md §9`)
- All orchestration should live in Cake, CI and local dev trigger Cake tasks ([`plan.md`](../plan.md) Strategic Decisions: "Cake as single orchestration surface")
- Local acquisition should be deterministic and reproducible — not "copy files around until it works"
- Source Mode is explicitly host-RID-only; non-host RIDs are Phase 2b scope

Implied constraint (not written but reinforced by the ProjectReference/PackageReference split): whatever mechanism we land **must not repurpose `buildTransitive/*.targets` for source-mode visibility**. Those targets are the shipping-graph contract and should remain untouched.

## 3. Existing Shipping-Graph Context

Today's `.Native` csproj structure (grounded against [`src/native/Directory.Build.props`](../../src/native/Directory.Build.props) and [`src/native/SDL2.Core.Native/SDL2.Core.Native.csproj`](../../src/native/SDL2.Core.Native/SDL2.Core.Native.csproj)):

- `<IncludeBuildOutput>false</IncludeBuildOutput>` — no compiled assembly output
- Pack-time `Content` items at `src/native/<Lib>/runtimes/<rid>/native/*` with `Pack="true"` + `PackagePath="runtimes\%(Link)"`
- `buildTransitive/<PackageId>.targets` with `Condition="'$(TargetFrameworkIdentifier)' == '.NETFramework'"` — only runs for .NET Framework consumers of the **packaged** NuGet

Two observations shape the solution space:

1. **Modern .NET (net6+) needs no `.targets` at all.** NuGet's native asset resolution (`runtimes/<rid>/native/`) handles placement automatically at restore/publish time. The `buildTransitive` shim is a legacy-framework crutch.
2. **`runtimes/` folders inside `src/native/*/` are pack-time truth.** They need to be populated before pack. Today there is no supported pipeline that populates them on demand — contributors rely on manually running vcpkg + harvest, or (worse) on committed-to-git binaries (flagged in Known Issues).

Source Mode does not intersect with either of these mechanisms: it needs a separate, explicitly-opt-in pathway that injects natives into consumer `bin/` without touching pack-time plumbing.

## 4. Candidate Mechanisms Investigated

Four shapes were considered:

| ID | Shape | Where it lives |
| --- | --- | --- |
| **A** | Unconditional `<Content CopyToOutputDirectory>` on `.Native` csproj | Modifies shipping-graph csproj unconditionally |
| **B** | Solution-root `Directory.Build.targets` with opt-in `<JansetSdl2SourceMode>` flag; Content items injected when flag is true; opt-in set in consumer `Directory.Build.props` | New repo-root and test-tree files; no shipping-graph changes |
| **C** | Conditional `<Content>` on `.Native` csproj gated on `$(JansetSdl2SourceMode)` flag | Shipping-graph csproj, but feature-flagged |
| **D** | Force all tests to use `PackageReference` via a local folder feed | Kills fast inner loop; documented as anti-goal by `execution-model-strategy-2026-04-13.md` Principle 1 |

Candidate D was ruled out by policy, not by experiment. A, B, and C were empirically tested via a throwaway PoC.

## 5. PoC Findings

PoC built in an isolated worktree: three minimal csprojs (`Dummy.Native` → `Dummy.Managed` → `Dummy.Test`) + dummy native payload at `staging/native/win-x64/dummy.dll`. Preserved at `.claude/worktrees/agent-aca25dd8` for inspection.

| Scenario | Verdict | Detail |
| --- | --- | --- |
| **A.** Unconditional Content on `.Native` csproj | Works | `CopyToOutputDirectory="PreserveNewest"` + `Link="runtimes\win-x64\native\dummy.dll"` propagated cleanly through the 2-hop `ProjectReference` chain. Landed at `Dummy.Test/bin/Debug/net9.0/runtimes/win-x64/native/dummy.dll`. No warnings, no duplicates. **Downside:** unconditional — always copies, even in scenarios where the consumer does not want Source Mode behavior (e.g., a consumer that ProjectReferences the .Native csproj as part of pack tooling). |
| **B.** Solution-root `Directory.Build.targets` + opt-in flag in consumer `Directory.Build.props` | Works — cleanest | Content injected at solution-root level when `$(JansetSdl2SourceMode) == 'true'`. Flag set in `Dummy.Test/Directory.Build.props`. Dummy.dll landed at expected path. **No bleed:** MSBuild evaluates `Directory.Build.props` per project relative to its own directory, so the flag set in `Dummy.Test/Directory.Build.props` is invisible to `Dummy.Native` and `Dummy.Managed` builds. Empirically verified: their `bin/` directories contain zero Content from the staging dir. |
| **C.** Conditional Content on `.Native` csproj gated on flag | Dead end | MSBuild properties do **not** propagate from a consumer project to its `ProjectReference` dependencies. When MSBuild builds `Dummy.Native` as a dependency of `Dummy.Test`, the property `$(JansetSdl2SourceMode)` is unset in `Dummy.Native`'s evaluation context, so the conditional `ItemGroup` never activates. Zero Content propagates. This is expected MSBuild behavior, not a bug. |

**Gotcha surfaced (applies to any mechanism):** Setting `RuntimeIdentifier` in a `Directory.Build.props` triggers `NETSDK1047` ("assets file doesn't have a target for net9.0/win-x64") unless a `dotnet restore` is run first. The recommended mechanism (below) avoids requiring `RuntimeIdentifier` as a property by relying on MSBuild's `%(RecursiveDir)` item metadata to preserve the RID path segment from the staging directory layout.

### 5.1 Linux Symlink Verification (WSL Ubuntu, 2026-04-15)

A second PoC under `/tmp/jansetsrcmode-poc/` (WSL Ubuntu, `dotnet` 9.0.310) tested whether MSBuild's default `<Content CopyToOutputDirectory>` preserves Unix symlink chains, and whether an `<Exec cp -a>` target preserves them.

**Staging setup:**

```text
/tmp/jansetsrcmode-poc/staging/linux-x64/native/
├── libfake.so            → libfake.so.0          (symlink)
├── libfake.so.0          → libfake.so.0.2600.4   (symlink)
└── libfake.so.0.2600.4                           (1 MB regular file)
Total: 1.1 MB
```

**Results:**

| Test | Mechanism | `bin/` size | Symlink chain in `bin/`? | Verdict |
| --- | --- | --- | --- | --- |
| 1 | `<Content Include="staging/**/*" CopyToOutputDirectory="PreserveNewest" />` | **3.1 MB** (3× staging) | **No** — all three entries are independent 1 MB regular files (`-rw-r--r--`) | MSBuild's `File.Copy` follows symlinks and writes regular files. Unusable for Unix Source Mode. |
| 2 | `<Target AfterTargets="CopyFilesToOutputDirectory"><Exec Command="cp -a staging/. $(TargetDir)runtimes/" /></Target>` | **1.1 MB** (1× staging) | **Yes** — `ls -la` shows two `lrwxrwxrwx` entries with proper `->` arrows pointing to the actual file | Preserves the full chain. This is the required Unix path. |
| 3 | Combined: `<Content>` gated on `IsOsPlatform('Windows')`, `<Exec>` gated on `!IsOsPlatform('Windows')`, same solution file | 1.1 MB | Yes | Platform gating works — Windows `Content` did **not** fire on Linux, only the `Exec` target ran. 0 warnings. |

**Implication:** The mechanism must branch on platform. A single `<Content>`-only approach would functionally work on Unix (ELF loaders resolve by SONAME → duplicated files are still found) but at a 3× disk cost and with misleading file metadata — contributors inspecting `bin/` would see regular files where a system library convention expects symlinks. The `<Exec cp -a>` target is cheap, preserves correctness, and required no gymnastics in the MSBuild scope. Staging → `bin/` takes ~50 ms on the 1.1 MB test payload.

**PoC preserved:** `/tmp/jansetsrcmode-poc/` in WSL Ubuntu for inspection (survives until WSL restart).

### 5.2 macOS Symlink Verification (Intel Mac, SSH, 2026-04-15)

A third PoC under `/tmp/jansetsrcmode-poc-mac/` on an Intel Mac (Darwin 24.6.0 x86_64, `dotnet` 10.0.101, accessed via SSH) tested the same two mechanisms against a **three-level** dylib symlink chain — macOS install-name convention often produces longer chains than Linux SONAME.

**Staging setup:**

```text
/tmp/jansetsrcmode-poc-mac/staging/osx-x64/native/
├── libfake.dylib              → libfake.1.dylib           (top-level symlink)
├── libfake.1.dylib            → libfake.1.2.600.dylib     (intermediate symlink)
└── libfake.1.2.600.dylib                                  (1 MB regular file)
Total: 1.0 MB
```

**Results:**

| Test | Mechanism | `bin/` size | Chain in `bin/`? | Verdict |
| --- | --- | --- | --- | --- |
| 1 | `<Content Include="staging/**/*" CopyToOutputDirectory="PreserveNewest" />` | **3.0 MB** (3× staging) | **No** — three independent 1 MB regular files | Same failure as Linux: `File.Copy` follows symlinks. Worse here because chain is longer, scaling with chain length |
| 2 | `<Target><Exec Command="cp -a staging/. $(TargetDir)runtimes/" /></Target>` | **1.0 MB** (1× staging) | **Yes** — `lrwxr-xr-x` entries, full 3-level chain: `libfake.dylib → libfake.1.dylib → libfake.1.2.600.dylib` | BSD `cp -a` preserves symlinks identically to GNU `cp -a`. Chain depth does not degrade the mechanism |
| 3 | Combined mechanism with `$([MSBuild]::IsOsPlatform('Windows'))` gating | 1.0 MB | Yes, 3-level | Platform gating works on Darwin: `IsOsPlatform('Windows')` returns false, so only the `<Exec>` path fires |

**macOS-specific concerns that did NOT materialize:**

- Extended attributes (`xattr`): tested, none present on fake payload (none were set). `cp -a` preserves xattrs per POSIX, so real payloads with quarantine or code-signing attributes would retain them — not verified in this PoC but not blocked either.
- Gatekeeper / notarization: not involved — local filesystem operations, no `/bin/sh` intermediation, no launchd hooks.
- Case-sensitivity: default macOS filesystem is case-insensitive (HFS+/APFS without case-sensitive variant). Not an issue for the chain tested; would need attention only if a library has names differing only by case.

**PoC preserved:** `/tmp/jansetsrcmode-poc-mac/` on the Intel Mac (survives until reboot or manual cleanup). Accessible via SSH for inspection.

**Combined empirical base (2026-04-15):**

| Platform | Chain depth | Content-only mechanism | `<Exec cp -a>` mechanism | Combined + gating |
| --- | --- | --- | --- | --- |
| Windows 11 (worktree) | N/A (no symlinks) | Works — loose file copy | N/A | Works — `Content` fires, `<Exec>` suppressed |
| Linux (WSL Ubuntu) | 2-level | 3× dup, chain destroyed | 1×, chain preserved | Works — `<Exec>` fires, `Content` suppressed |
| macOS (Intel, Darwin) | 3-level | 3× dup, chain destroyed | 1×, 3-level chain preserved | Works — `<Exec>` fires, `Content` suppressed |

## 6. Prior Art

Surveyed mono/SkiaSharp, libgit2/libgit2sharp, and dlemstra/Magick.NET — all three are .NET projects that ship native payloads across multiple RIDs and need to make in-tree tests see those natives without going through pack-restore.

| Project | Approach | Fit for us |
| --- | --- | --- |
| **SkiaSharp** | Per-test-csproj explicit `<Import>` of a shared `IncludeNativeAssets.SkiaSharp.targets` file containing `<Content CopyToOutputDirectory>` globbing from `output/native/{os}/{arch}/`. Populated by Cake (`dotnet cake --target=externals-download`). | Same shape as our Option B, but less automated — every test csproj has to opt in with an explicit `<Import>`. Our `Directory.Build.targets` + flag variant is strictly cleaner. |
| **LibGit2Sharp** | Sidesteps the problem by publishing natives as a separate NuGet package (`LibGit2Sharp.NativeBinaries`); tests take a `ProjectReference` to the managed library but natives flow via the transitive `PackageReference` chain. | Not viable for us — Source Mode's purpose is to avoid the pack/restore loop entirely. Interesting alternative if we ever want external consumers to iterate on natives without rebuilding managed. |
| **Magick.NET** | Pre-build PowerShell script (`install.ps1`) downloads a native-binaries NuGet package and physically copies `.dll` files into every test project's `bin/{Config}/{Platform}/{TFM}/`. | Reject — brute force, fragile, hardcodes every platform×config combination, requires re-run after clean builds. |

**Pattern confirmed across all three:** none of them repurpose `buildTransitive/*.targets` for source-mode visibility. Our constraint to keep source-graph and shipping-graph mechanisms separate is consistent with the ecosystem.

**Gotcha to steal from SkiaSharp:** their mechanism silently no-ops when the staging directory is empty (e.g., contributor forgot to run `externals-download`). First build succeeds, tests fail at runtime with `DllNotFoundException`. Mitigation: emit an MSBuild `<Warning>` at build time if Source Mode is opted in but the staging directory is missing or empty.

## 7. Recommendation

Adopt PoC Scenario B with the following concrete shape.

### 7.1 Files

**`Directory.Build.targets`** (solution root — new file). Mechanism is **platform-branched**: Windows uses `<Content>` + `CopyToOutputDirectory` (no symlinks to worry about on Windows native layouts); Linux/macOS use a `<Target>` with `<Exec cp -a>` (preserves symlink chains, see §5.1 WSL verification).

```xml
<Project>

  <!-- Windows path: Content + CopyToOutputDirectory.
       Windows native payloads are loose .dll files with no symlinks,
       so MSBuild's default file-copy semantics are correct here. -->
  <ItemGroup Condition="'$(JansetSdl2SourceMode)' == 'true'
                         And $([MSBuild]::IsOsPlatform('Windows'))">
    <Content Include="$(MSBuildThisFileDirectory)artifacts\native-staging\**\*">
      <Link>runtimes\%(RecursiveDir)%(Filename)%(Extension)</Link>
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
      <Pack>false</Pack>
      <Visible>false</Visible>
    </Content>
  </ItemGroup>

  <!-- Linux/macOS path: cp -a preserves symlink chains, permissions, and mtime.
       MSBuild's default <Content>-based file copy follows symlinks and writes
       regular files, producing N× duplication of every binary in a chain.
       See §5.1 for WSL empirical evidence (3× duplication observed). -->
  <Target Name="_JansetSourceModeUnixSync"
          AfterTargets="CopyFilesToOutputDirectory"
          Condition="'$(JansetSdl2SourceMode)' == 'true'
                      And !$([MSBuild]::IsOsPlatform('Windows'))
                      And Exists('$(MSBuildThisFileDirectory)artifacts/native-staging')">
    <MakeDir Directories="$(TargetDir)runtimes" />
    <Exec Command="cp -a &quot;$(MSBuildThisFileDirectory)artifacts/native-staging/&quot;. &quot;$(TargetDir)runtimes/&quot;" />
  </Target>

  <!-- Sanity check (platform-agnostic).
       JANSET0001 and JANSET0002 are mutually exclusive by construction. -->
  <Target Name="_JansetSourceModeSanityCheck"
          BeforeTargets="Build"
          Condition="'$(JansetSdl2SourceMode)' == 'true'">
    <ItemGroup>
      <_JansetStagedFiles Include="$(MSBuildThisFileDirectory)artifacts/native-staging/**/*.*" />
    </ItemGroup>
    <Warning Condition="!Exists('$(MSBuildThisFileDirectory)artifacts/native-staging')"
             Code="JANSET0001"
             Text="Source Mode is enabled but artifacts/native-staging/ does not exist. Run: dotnet cake --target=Source-Mode-Prepare --rid=&lt;rid&gt;" />
    <Warning Condition="Exists('$(MSBuildThisFileDirectory)artifacts/native-staging') And '@(_JansetStagedFiles)' == ''"
             Code="JANSET0002"
             Text="Source Mode is enabled and artifacts/native-staging/ exists, but no files are staged. Did you stage for the right RID?" />
  </Target>

</Project>
```

**Verification summary:**

- Windows PoC (worktree, 2026-04-15): sanity-check warnings fire correctly across the three staging states (populated → 0 warnings; empty → `JANSET0002`; missing → `JANSET0001`). `<Content>` propagates cleanly through the 2-hop `ProjectReference` chain with the correct `Link` path.
- Linux PoC (WSL, 2026-04-15 — see §5.1): `<Exec cp -a>` preserves symlink chains at 1× source size. `<Content>` on the same staging would 3× the payload and flatten symlinks. Platform gating verified — the Windows `<Content>` block does not fire on Linux.

**MSBuild idiom note:** An earlier draft used `@(Content->WithMetadataValue('Link', 'runtimes\'))` to detect empty staging. That was broken — `WithMetadataValue` is exact-match filtering, and actual `Link` values are `runtimes\win-x64\native\<file>`, so the filter never matched. The corrected idiom (above) declares a synthetic `_JansetStagedFiles` item group **inside** the `Target` (evaluated at target-execution time, not project-parse time) and checks whether it is empty.

**`test/Directory.Build.props`** (new file — the Phase 2a preset; opts in for everything under `test/`):

```xml
<Project>
  <Import Project="../Directory.Build.props" />
  <PropertyGroup>
    <JansetSdl2SourceMode>true</JansetSdl2SourceMode>
  </PropertyGroup>
</Project>
```

**Other subtrees follow the same pattern when they exist.** `samples/Directory.Build.props`, `sandbox/Directory.Build.props`, or any future tree of non-`src/` csprojs that need Source Mode would drop the same 5-line props file. `src/` is deliberately excluded — the `Directory.Build.targets` at the solution root ships the mechanism, but `src/` projects do not opt in (they are consumed by tests, not consuming natives themselves). MSBuild evaluates `Directory.Build.props` per project directory, so no opt-in file under `src/` means no bleed — empirically verified in the Windows PoC (see §5).

**`.gitignore`** additions:

```text
# Source Mode native staging (populated by Cake Source-Mode-Prepare)
artifacts/native-staging/
```

### 7.2 Cake task — `Source-Mode-Prepare` (two-source framework)

Stream F scope. The task has a single stable interface but accepts two distinct native-payload sources. Contributors never interact with vcpkg directly — it is one of the two backends, hidden behind the task.

```bash
# Option 1: Remote artifact / release URL (non-host RIDs supported)
dotnet cake --target=Source-Mode-Prepare --rid=<rid> --source=remote --url=<gha-artifact-or-release-url>

# Option 2: Local vcpkg build (default, host RID only)
dotnet cake --target=Source-Mode-Prepare --rid=<host-rid> --source=local
```

**Option 1 — Remote source (direction locked; mechanism details open):**

- Direction locked: non-host RIDs acquire natives from a URL, and Cake extracts the downloaded archive into staging.
- **Unresolved (Phase 2b scope):** URL convention (GitHub Actions workflow artifact, GitHub release asset, or something else), which producer workflow publishes those artifacts, artifact granularity (one archive per RID vs one per library-per-RID), the **archive contract** itself (format, internal layout, extract semantics), authentication story (public vs GH token), and caching. None of these have been validated end-to-end; this research doc does not try to specify them.
- Locked constraint on whatever the archive contract turns out to be: it must preserve Linux/macOS symlink chains end-to-end. If the producer ends up emitting a format that flattens symlinks (loose-file zip, a tar without `--preserve-symlinks` equivalents, etc.), Option 1 regresses to the same symlink-destruction problem §5.1/§5.2 ruled out.
- When it works, it supports **any RID** regardless of the contributor's host architecture — a Windows contributor can stage `linux-x64` natives for cross-RID testing via this path.
- Implementation lands in Phase 2b alongside D-ci work.

**Option 2 — Local source (default, 2a scope):**

- Input: the target RID (must be host-buildable).
- Flow: Cake invokes the existing vcpkg install pipeline for the RID's triplet → runs the existing Harvest pipeline with its output root redirected from `src/native/<Lib>/runtimes/<rid>/native/` to `artifacts/native-staging/<rid>/native/`.
- On Unix, the redirect step uses `cp -a` (or equivalent symlink-preserving copy) so that vcpkg's symlinked output reaches staging intact.
- Host-RID only (vcpkg needs the host toolchain). Non-host RIDs fail with a message redirecting the contributor to Option 1.
- Reuses existing Cake services — no new vcpkg or harvest code needs to be written; only the output root changes.

**Common to both options:**

- Emit a sanity report after success: files staged, total size, RIDs present.
- Idempotent per-RID: re-running with the same `--rid` overwrites the staging dir for that RID, leaves other RIDs untouched.
- Do not touch any shipping-graph output (`src/native/<Lib>/runtimes/` or pack staging).

### 7.3 Staging directory layout

```text
artifacts/native-staging/
├── win-x64/
│   └── native/
│       ├── SDL2.dll
│       ├── SDL2_image.dll
│       └── ...
├── linux-x64/
│   └── native/
│       └── libSDL2-2.0.so.0
└── ...
```

`%(RecursiveDir)` in the `Directory.Build.targets` captures the path below the glob wildcard (e.g., `win-x64\native\`), which combined with the `Link` pattern `runtimes\%(RecursiveDir)%(Filename)%(Extension)` produces the correct bin-output path `runtimes\win-x64\native\SDL2.dll` without ever reading `$(RuntimeIdentifier)` from MSBuild properties. This sidesteps the `NETSDK1047` gotcha.

### 7.4 Expected contributor workflow

```bash
# First-time setup or after adding new libraries
dotnet cake --target=Source-Mode-Prepare --rid=win-x64

# Normal iteration
dotnet build test/SomeTest/
dotnet test test/SomeTest/
```

If the contributor forgets `Source-Mode-Prepare`, they get MSBuild warning `JANSET0001` at build time pointing at the exact Cake target to run. No silent failure.

### 7.5 Platform Variance — Why the Mechanism Branches

Native library layout differs between Windows and Unix, and the mechanism reflects that:

| Platform | Payload shape | Copy mechanism | Rationale |
| --- | --- | --- | --- |
| Windows (any arch) | Loose `*.dll` files, no symlinks | `<Content Include>` + `CopyToOutputDirectory="PreserveNewest"` | Native MSBuild semantics are correct — file copy matches file reality |
| Linux / macOS | Symlink chains encoding SONAME / `LC_ID_DYLIB` (e.g., `libSDL2.so → libSDL2.so.0 → libSDL2.so.0.2600.4`) | `<Target>` + `<Exec Command="cp -a ...">` | `cp -a` preserves symlinks, permissions, and mtime. Empirically required — see §5.1 |

**Why not a single `<Content>`-only mechanism:** §5.1 measured this directly in WSL. A `<Content>`-only mechanism on Linux produces 3× disk usage and flattens symlink chains into independent regular files. Whether real SDL2 natives would still load correctly from such a flattened layout is end-to-end territory (see the status note at the top of this doc). The mechanism is rejected on copy-mechanism grounds regardless:

- Disk bloat scales with chain length × library count (SDL2 satellites + all transitive deps could hit 20+ chains; chains up to 3 links deep on macOS)
- Contributors inspecting `bin/` would see file metadata that contradicts system library conventions — a debugging footgun
- The tradeoff saves nothing in return: the `<Target>` + `<Exec cp -a>` block is ~5 lines of MSBuild and runs in under 100 ms on Source-Mode-scale payloads

**Why not tar.gz in Source Mode:** The shipping graph uses tar.gz because NuGet packages are ZIP archives and ZIP cannot store symlinks (see [`symlink-handling.md`](symlink-handling.md) for the shipping rationale). Source Mode does not ship through NuGet — the staging dir is a plain filesystem location. `cp -a` from filesystem to filesystem preserves symlinks directly without the intermediate archive step. Introducing tar.gz into Source Mode would add build-time archive/extract work and a dependency on `tar` being on the `PATH` for no benefit over `cp -a`.

**Platform-detection idiom used:** `$([MSBuild]::IsOsPlatform('Windows'))` is a built-in MSBuild property function that returns `true` when the MSBuild host is running on Windows. It is the canonical way to branch on OS family in MSBuild targets; no external conditions or SDK-version checks required.

**Empirical base:** §5.1 Linux PoC (WSL Ubuntu, 2-level chain) and §5.2 macOS PoC (Intel Mac, Darwin 24.6.0, 3-level chain) — both 2026-04-15. On both platforms: `<Content>` path produced 3× bin output with zero symlinks; `<Exec cp -a>` path produced 1× output with full chain preserved. Combined platform-gated mechanism runs with 0 warnings on both. macOS BSD `cp -a` and Linux GNU `cp -a` behaved identically for chain preservation in these PoCs. Note: the `-a` flag is a coreutils convention (present on both BSD and GNU `cp`), not strictly POSIX — the cross-platform compatibility observed here is empirical, not guaranteed by the standard. If a future coreutils variant in scope drops or changes `-a` semantics, the mechanism would need re-validation on that platform.

## 8. Rationale

Why this combination over alternatives:

- **Opt-in via `test/Directory.Build.props`, not default-on globally** — keeps `src/` projects unaffected. The `src/` tree never evaluates Source Mode conditionals because `Directory.Build.props` evaluation is per-project and scoped to directory ancestry. Empirically verified by PoC (no bleed into Dummy.Native or Dummy.Managed).
- **Solution-root `Directory.Build.targets` as the injection point, not `test/Directory.Build.targets`** — the solution-root location knows the absolute path to `artifacts/native-staging/` via `$(MSBuildThisFileDirectory)` regardless of how deep the consuming test project is nested. Placing it at `test/Directory.Build.targets` would work but couples the mechanism to the test tree; future sandbox and sample directories would need to duplicate or inherit from it. Solution-root is DRY.
- **No `RuntimeIdentifier` property manipulation** — `%(RecursiveDir)` trick avoids the `NETSDK1047` restore-before-build gotcha entirely. The staging dir layout encodes the RID, the Link pattern preserves it.
- **Separate staging dir (`artifacts/native-staging/`) from pack-time staging (`src/native/*/runtimes/`)** — shipping-graph and source-graph remain structurally separate, matching `execution-model-strategy-2026-04-13.md §9`. No accidental coupling.
- **Warning on empty staging, not Error** — devs should be able to build helper/non-native tests without having run `Source-Mode-Prepare`. A forced error would make the inner loop more hostile for no strong reason.
- **`<Pack>false</Pack>` on every injected Content item** — Source Mode Content must never bleed into any pack output, even if a test project is accidentally packed.

## 9. Open Followups

| ID | Followup | Blocks |
| --- | --- | --- |
| PD-5 | Non-host RID local acquisition — **direction locked** (two-source framework in §7.2 makes `--source=remote --url=<url>` the non-host path). Concrete mechanism (URL convention, producer workflow, artifact granularity, auth, caching) is **still open** and remains Phase 2b scope alongside D-ci | Cross-RID local testing on a contributor machine — direction is clear, but the remote half has not been validated end-to-end |
| **PD-6** | .NET Framework source-mode visibility — current recommendation targets modern .NET (net8/net9); a `net462` test project would need an additional `AfterTargets="Build"` copy hook similar to today's `buildTransitive/*.targets` but activated by `$(JansetSdl2SourceMode)`. Not needed in 2a (no .NET Framework test projects exist today) but **must** land before any net462 consumer validation. | Any net462 in-tree test project (future) |
| — | Source Mode testing across multiple RIDs simultaneously — the current mechanism supports multi-RID staging (glob matches all RIDs present) but test project runtime loader will pick one based on `RuntimeIdentifier`. Cross-RID runtime validation is 2b scope. | Emulated cross-RID smoke testing |
| — | Integration with `dotnet-affected` change detection (Stream E feasibility spike, 2b for full impl) — when Source Mode is active, `Source-Mode-Prepare` should only rebuild natives for libraries whose vcpkg inputs changed. | Faster incremental native rebuilds |

## 10. Decisions Locked

| Decision | Value | Rationale |
| --- | --- | --- |
| Mechanism | Opt-in `Directory.Build.targets` at solution root, flag via `test/Directory.Build.props` | PoC empirically verified as the only clean no-bleed path |
| Opt-in default | On for everything under `test/` | One props file, zero per-csproj boilerplate |
| Staging location | `artifacts/native-staging/<rid>/native/*` | Matches existing `artifacts/` convention (gitignored), keeps source-graph and shipping-graph separate |
| Staging missing behavior | MSBuild Warning (codes `JANSET0001` / `JANSET0002`) | Dev ergonomics — non-native tests should still build. Warning-condition idiom verified in worktree 2026-04-15 |
| `RuntimeIdentifier` dependency | None — use `%(RecursiveDir)` from staging path | Avoid `NETSDK1047` restore gotcha |
| Platform variance (copy mechanism) | Windows: `<Content>` + `CopyToOutputDirectory`. Linux/macOS: `<Target>` + `<Exec cp -a>`. Both in the same solution-root `Directory.Build.targets`, gated on `$([MSBuild]::IsOsPlatform('Windows'))` | Empirical: Linux (WSL Ubuntu, 2-level chain, §5.1) and macOS (Intel Mac, Darwin 24.6, 3-level chain, §5.2) both measured on 2026-04-15. `<Content>` produces 3× duplication and destroys symlinks on both. `<Exec cp -a>` produces 1× output with full chain preserved on both. Tar.gz is not used in Source Mode (no NuGet ZIP barrier to work around) |
| Native acquisition sources | Cake `Source-Mode-Prepare` accepts `--source=remote --url=<url>` (any RID, download-and-extract with archive contract TBD) or `--source=local` (host RID via existing vcpkg+harvest). Contributor interface is uniform; vcpkg stays hidden | Single orchestration surface in Cake (`plan.md` Strategic Decisions). Option 1 locks PD-5's **direction**; producer contract, archive contract (format, internal layout, symlink preservation guarantee), and auth are all unresolved and 2b scope |
| Owner | Stream F in [`phase-2-adaptation-plan.md`](../phases/phase-2-adaptation-plan.md) | F is the source-truth stream; D-local is shipping-truth |

## 11. References

- Windows PoC worktree (preserved for inspection): `.claude/worktrees/agent-aca25dd8`, branch `worktree-agent-aca25dd8`. Validated `<Content>` propagation through `ProjectReference` chains + sanity-check warning states.
- Linux PoC (preserved until WSL restart): `/tmp/jansetsrcmode-poc/` in WSL Ubuntu. Validated `<Exec cp -a>` symlink preservation (1× disk, chain intact) vs `<Content>` flattening (3× disk, no symlinks). See §5.1 for data.
- [`docs/research/symlink-handling.md`](symlink-handling.md) — shipping graph symlink rationale (tar.gz for NuGet ZIP limitation). Source Mode does not inherit this mechanism; reasoning in §7.5.
- [`docs/research/execution-model-strategy-2026-04-13.md`](execution-model-strategy-2026-04-13.md) — three-mode model (Source / Package Validation / Release); source-graph vs shipping-graph separation.
- [`docs/phases/phase-2-adaptation-plan.md`](../phases/phase-2-adaptation-plan.md) — Stream F implementation scope.
- SkiaSharp mechanism: [`binding/IncludeNativeAssets.SkiaSharp.targets`](https://github.com/mono/SkiaSharp/blob/main/binding/IncludeNativeAssets.SkiaSharp.targets)
- LibGit2Sharp mechanism: [`LibGit2Sharp/LibGit2Sharp.csproj`](https://github.com/libgit2/libgit2sharp/blob/main/LibGit2Sharp/LibGit2Sharp.csproj)
- Magick.NET mechanism: [`src/Magick.Native/install.ps1`](https://github.com/dlemstra/Magick.NET/blob/main/src/Magick.Native/install.ps1)
