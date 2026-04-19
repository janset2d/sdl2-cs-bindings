# ADR-001: D-3seg Versioning, Package-First Local Dev, and Artifact Source Profile Abstraction

- **Status:** Accepted
- **Date:** 2026-04-18
- **Deciders:** Deniz İrgin (primary), collaborative synthesis across multiple LLM sessions
- **Supersedes:** Portions of `docs/knowledge-base/release-lifecycle-direction.md` §3, §4, §7 (pre-D-3seg versioning model); portions of `docs/research/execution-model-strategy-2026-04-13.md` (three-mode model); portions of `docs/research/source-mode-native-visibility-2026-04-15.md` (ProjectReference-based Source Mode mechanism)
- **Amends:** `docs/phases/phase-2-adaptation-plan.md` (adds PD-12, rewrites Stream F scope); `docs/knowledge-base/release-guardrails.md` (adds G54–G57)

---

## 1. Context

### 1.1 What was broken

Three tangled concerns surfaced simultaneously during the 2026-04-18 review meta-pass:

1. **Versioning source-of-truth was unclear to the repo owner.** Canonical docs committed to "independent SemVer + MinVer tag-derived" family versions (e.g., `Janset.SDL2.Core 1.2.0`) while `manifest.json` carried `native_lib_version` fields and `vcpkg.json` carried native library versions — three overlapping version concepts with no single owner. The "1.3.0" strings strewn across the `artifacts/packages/` directory were revealed to be ad-hoc `--family-version` CLI flags from test runs, **not** derived from any git tag (there were no git tags). MinVer had never actually been exercised against a tagged release.

2. **Source Mode mechanism introduced a second consumer contract.** `docs/research/source-mode-native-visibility-2026-04-15.md` established that in-tree test/sample/sandbox csprojs would reach native payloads via `ProjectReference` chains, using MSBuild `<Content>` injection from `src/native/<lib>/runtimes/<rid>/native/` into consumer `bin/.../runtimes/<rid>/native/`. This created a parallel track to the package-consumer path (PackageReference + local feed restore via `buildTransitive/*.targets`). Two tracks, two failure surfaces, two sets of bugs.

3. **Local dev UX was broken at the IDE layer.** Smoke csprojs opened in IDE without Cake orchestration produced `JNSMK001+` guard errors because dynamic properties (`LocalPackageFeed`, per-family `JansetSdl<N><Role>PackageVersion`) come from the runner and have no stable local-authoring state. Contributors trying to work on the repo by opening `Janset.SDL2.sln` in Rider/VS were met with immediate restore failures.

### 1.2 The strategic observation

The knot was not "which version format is prettier." It was three questions masquerading as one:

- **Version identity**: what does a package version actually represent?
- **Consumer contract**: how does anything (CI, smoke, sample, sandbox) consume our artifacts?
- **Source resolution**: where do the underlying bits come from (locally produced vs remote-fetched)?

Each was being answered independently and accumulating divergent abstractions. This ADR resolves all three together.

### 1.3 Decision precedents reviewed

Before converging on this decision, the following paths were evaluated:

| Option | Version shape | Source tree model | Outcome |
|---|---|---|---|
| **A** (prior canon) | Independent SemVer, MinVer tag-derived (`1.2.0`) | Source Mode (ProjectReference + content injection) distinct from package consumption | Rejected: version identity abstract; two consumer contracts; README required to answer "which SDL?" |
| **B** (pure upstream-tracked) | `<vcpkg-Major>.<vcpkg-Minor>.<vcpkg-Patch>.<build>` (`2.32.10.1`) | Same as A | Rejected: 4-segment MinVer friction; vcpkg patch bumps force releases; binding-only fixes unsignaled; ReleaseLifecycle canonical wording breaks |
| **C** (pack-time version override) | Pack-time CLI override of any semver | Not addressed | Rejected explicitly in `docs/research/full-train-release-orchestration-2026-04-16.md` §3.3 — loses git-as-source-of-truth |
| **D-4seg** | `<vcpkg-Major>.<vcpkg-Minor>.<family-Patch>.<build>` (`2.32.0.1`) | Same as A | Rejected: MinVer 3-part limitation requires bypass; NuGet consumer surprise |
| **D-3seg** (this ADR) | `<vcpkg-Major>.<vcpkg-Minor>.<family-Patch>` (`2.32.0`) | Package-first: Source Mode retired, all consumers go through package restore | **Accepted** |

Industry precedent surveyed: SkiaSharp (independent SemVer), ppy/SDL3-CS (date-versioned auto-gen), flibitijibibo/SDL2-CS (independent), Silk.NET (unified), LibGit2Sharp (approx-upstream-tracked), MonoGame (independent). Our position is closest to LibGit2Sharp's historical model — upstream Major.Minor anchored, binding iteration in Patch — but without the 4-segment trailing build counter.

---

## 2. Decision

### 2.1 Versioning (D-3seg)

**Family version format:** `<UpstreamMajor>.<UpstreamMinor>.<FamilyPatch>`

- **UpstreamMajor.UpstreamMinor** is drawn from the family's native upstream library version as declared in `manifest.json library_manifests[].vcpkg_version`. For `sdl2-core`, that is SDL2's Major.Minor (currently `2.32`). For `sdl2-image`, that is SDL2_image's Major.Minor (currently `2.8`). Each family follows its own upstream library, so cross-family Major.Minor values differ (Core `2.32.x`, Image `2.8.x`, Gfx `1.0.x`).
- **FamilyPatch** is the repo's own iteration counter within a given UpstreamMajor.UpstreamMinor line. Monotonically increasing. Human-set at release time. Reset to `0` when UpstreamMajor or UpstreamMinor changes.
- **No build segment.** Three-part SemVer, no trailing `.<build>`. Keeps MinVer compatible, avoids NuGet consumer surprise, matches SemVer 2.0 norms.

**Examples:**

| Scenario | Family version | Notes |
|---|---|---|
| Initial release wrapping SDL2 2.32.10 | `Janset.SDL2.Core 2.32.0` | FamilyPatch starts at 0 |
| Binding marshal fix, SDL2 still 2.32.10 | `Janset.SDL2.Core 2.32.1` | FamilyPatch increments |
| vcpkg updates SDL2 to 2.32.12 (patch bump), binding re-pack | `Janset.SDL2.Core 2.32.2` | Upstream patch does not propagate to version; FamilyPatch moves |
| vcpkg port_version bumps (e.g., packaging fix), binding re-pack | `Janset.SDL2.Core 2.32.3` | port_version does not propagate to version; FamilyPatch moves |
| SDL2 minor bump to 2.33.0 | `Janset.SDL2.Core 2.33.0` | UpstreamMinor bumps; FamilyPatch resets |
| SDL3 transition | `Janset.SDL3.Core 3.x.y` under `sdl3-core` family identifier | Cross-major boundary → different family identifier, not a version bump |

**Prerelease suffixes** are permitted per NuGet SemVer 2.0 convention: `2.32.0-rc.1`, `2.32.0-alpha.3`, `2.32.0-local.20260418T1342`. Prereleases MUST preserve the UpstreamMajor.UpstreamMinor prefix — no `0.1.0-local.*` shortcuts even in bootstrap flows.

**Breaking managed API changes** are not a normal-flow occurrence. If required (e.g., a CppAst generator migration that restructures namespaces in a way SDL's own major didn't), they require a separate Architecture Decision Record documenting the break, the affected consumer surface, and the transition plan. This ADR does not prescribe a mechanism for signaling the break in the version number itself — that decision is deferred to the hypothetical future ADR.

### 2.2 Family-lock (retained from prior canon)

Unchanged from `release-lifecycle-direction.md` §1: a package family consists of one managed package (`Janset.SDL<N>.<Role>`) and one native package (`Janset.SDL<N>.<Role>.Native`). Both share the same version string and always release together. There is no separate "binding version" and "native version" within a family.

### 2.3 MinVer role clarification

MinVer is a **tag reader**, not a version inventor. Its sole function in this project is to resolve the build-time `$(Version)` property from the family tag matching `<MinVerTagPrefix>sdl<N>-<role>-</MinVerTagPrefix>` at the commit being built.

- Release builds (CI): the operator (human or workflow) creates the family tag at the release commit (`sdl2-core-2.32.0`) and pushes; CI checks out the tag, MinVer reads it, pack uses `$(Version) = 2.32.0`.
- Untagged commits: MinVer produces a deterministic prerelease fallback, e.g. `2.32.0-alpha.0.N`. Never `0.0.0-*` unless no predecessor tag exists anywhere in history.
- Cake `PackageTaskRunner.PackageVersionResolver` accepts an explicit `--family-version=<semver>` CLI flag which, when supplied, bypasses MinVer for that pack invocation. Used today only by PD-8 manual escape-hatch flows. It is **not** a primary mechanism; the tag is the source of truth for stable releases. **Note (2026-04-19, PD-13):** `SetupLocalDev` was refactored (pre-ADR-001 canon) to auto-generate per-family versions directly from `manifest.json library_manifests[].vcpkg_version` + local timestamp without consulting the flag, and `PackageConsumerSmoke` was reconciled to trust `Janset.Smoke.local.props` as the version source of truth for smoke testing. The flag's surviving legitimate surface has narrowed to the PD-8 escape path; retirement is tracked as [PD-13](../phases/phase-2-adaptation-plan.md#pending-decisions).

### 2.4 Dependency contracts

**Within-family (unchanged from post-S1 canon):** minimum range.

```text
Janset.SDL2.Image 2.8.3  →  Janset.SDL2.Image.Native (>= 2.8.3)
```

Drift prevention at orchestration time (atomic `PackageTask`, post-pack validator G23), not at consumer resolution time.

**Cross-family (AMENDED — upper bound added):** minimum + explicit upper bound at next SDL major.

```text
Janset.SDL2.Image 2.8.3  →  Janset.SDL2.Core (>= 2.32.4, < 3.0.0)
```

Rationale for upper bound:

- SemVer-idiomatic "don't accept next major" hygiene.
- Cheap insurance against accidental resolution if any future packaging convention accidentally brings `Janset.SDL2.Core 3.x` into existence (which should not happen under the `sdl3-core` family identifier scheme, but explicit is better).
- Does NOT tighten to `< nextMinor` — that would make each SDL minor bump cascade satellite re-releases and defeat version independence.

### 2.5 Upstream metadata (exact patch + port_version)

Exact upstream library version and vcpkg port_version are **not** encoded in the family version string. They are preserved as package-shipped metadata in two complementary forms:

**Form 1 — machine-readable metadata file** (`janset-native-metadata.json`, packed into every `.Native` nupkg at root):

```json
{
  "janset_family_version": "2.32.0",
  "family_identifier": "sdl2-core",
  "upstream_library": "sdl2",
  "upstream_version": "2.32.10",
  "vcpkg_port_version": 0,
  "triplet_set": [
    "x64-windows-hybrid",
    "win-arm64-hybrid",
    "x86-windows-hybrid",
    "x64-linux-hybrid",
    "arm64-linux-hybrid",
    "x64-osx-hybrid",
    "arm64-osx-hybrid"
  ],
  "build_commit": "<git-sha>"
}
```

Generated at pack time by the Cake `Package` task; asserted by post-pack validator G55.

**Form 2 — human-readable README mapping table** (`README.md` root):

```markdown
<!-- JANSET:MAPPING-TABLE-START -->
| Family | Version | Upstream | vcpkg Port |
| --- | --- | --- | --- |
| Janset.SDL2.Core | 2.32.0 | SDL 2.32.10 | 0 |
| Janset.SDL2.Image | 2.8.0 | SDL2_image 2.8.8 | 2 |
| Janset.SDL2.Mixer | 2.8.0 | SDL2_mixer 2.8.1 | 2 |
| Janset.SDL2.Ttf | 2.24.0 | SDL2_ttf 2.24.0 | 0 |
| Janset.SDL2.Gfx | 1.0.0 | SDL2_gfx 1.0.4 | 11 |
| Janset.SDL2.Net | 2.2.0 | SDL2_net 2.2.0 | 3 |
<!-- JANSET:MAPPING-TABLE-END -->
```

Generated by Cake, delimited by HTML comment markers so a validator can extract and diff the block without parsing free-form markdown. Asserted current by post-pack validator G57.

### 2.6 Package-first consumer contract (Source Mode retired)

**Locked principle:**

> All consumer-facing validation paths use packages; local vs remote changes only how the local feed is prepared.

Operationally this means:

- **Every** consumer-type csproj (smoke, example, sandbox, future samples) consumes Janset packages via `PackageReference` against a local folder feed, identical to the external-consumer experience. No `ProjectReference` chain reaches from a consumer csproj into `src/native/`.
- The previously-designed Source Mode mechanism (MSBuild `<Content>` injection from staging into consumer `bin/` via Stream F `Directory.Build.targets`) is retired. The research note `docs/research/source-mode-native-visibility-2026-04-15.md` is DEPRECATED; its symlink-preservation findings remain informational for future remote-feed cache work.
- The "three execution modes" framing (Source Mode / Package Validation Mode / Release Mode) from `docs/research/execution-model-strategy-2026-04-13.md` collapses to a single consumer contract (PackageReference-based) with two feed-preparation flavors (Local / Remote). Release Mode is a promotion step on top of that, not a separate consumer contract.
- Binding debug against live source changes is no longer a supported mainline flow. If required, it is handled by an explicit, opt-in, throwaway harness outside the smoke system. The repo's official consumer model is package-based.

### 2.7 Artifact Source Profile abstraction

A single Cake-level abstraction governs where artifacts come from and where/how the local feed is populated.

```csharp
public enum ArtifactProfile
{
    Local,            // repo-local pack produces the feed
    RemoteInternal,   // internal feed (GitHub Packages or equivalent) populates local cache
    ReleasePublic     // public NuGet.org is the origin (promotion path)
}

public interface IArtifactSourceResolver
{
    ArtifactProfile Profile { get; }
    Task PrepareFeedAsync(CancellationToken ct);
    DirectoryPath LocalFeedPath { get; }
    Task WriteConsumerOverrideAsync(CancellationToken ct); // writes Janset.Smoke.local.props
}
```

Only `LocalArtifactSourceResolver` is implemented in Phase 2a. `RemoteInternal` and `ReleasePublic` are stubbed with interface-level contracts and concrete implementation deferred to Phase 2b (Stream D-ci delivery).

### 2.8 Local dev UX contract

A new Cake task `SetupLocalDev` is the canonical entry point for a developer booting a fresh checkout.

```bash
dotnet run --project build/_build -- --target SetupLocalDev --source=local
```

The task:

1. Bootstraps vcpkg (submodule init + platform-specific bootstrap script).
2. Installs vcpkg packages for the current-host triplet.
3. Runs `Harvest` + `ConsolidateHarvest` + `Package` (all families) at an auto-generated upstream-aligned prerelease version per family, e.g.:
   - `sdl2-core-2.32.0-local.<YYYYMMDDTHHMMSS>`
   - `sdl2-image-2.8.0-local.<YYYYMMDDTHHMMSS>`
   - (one timestamp shared across families for a given `SetupLocalDev` invocation)
4. Writes `build/msbuild/Janset.Smoke.local.props` (gitignored) with:
   - `<LocalPackageFeed>` pointing to `artifacts/packages`.
   - Per-family `<JansetSdl<N><Role>PackageVersion>` set to the freshly-packed versions.
5. Reports: "IDE'de smoke csproj aç, restore çalışır. Family versions written to `Janset.Smoke.local.props`."

`Janset.Smoke.local.props` is consumed by a conditional `<Import>` at the tail of `build/msbuild/Janset.Smoke.props`:

```xml
<Import Project="$(MSBuildThisFileDirectory)Janset.Smoke.local.props"
        Condition="Exists('$(MSBuildThisFileDirectory)Janset.Smoke.local.props')" />
```

The `--source=remote` variant skips steps 1–3 and instead pulls prebuilt nupkgs from an internal feed into a local cache; the override-file write (step 4) and IDE readiness (step 5) are identical. This is the Phase 2b deliverable whose interface contract is locked in this ADR.

### 2.9 New preflight + post-pack guardrails (G54–G57)

Added to `docs/knowledge-base/release-guardrails.md` in the same diff that rewrites the versioning canon:

| ID | Scope | Check |
|---|---|---|
| **G54** | PreFlight | Family tag UpstreamMajor.UpstreamMinor ≡ `manifest.json library_manifests[].vcpkg_version` UpstreamMajor.UpstreamMinor. Example: a tag `sdl2-core-2.32.0` requires `manifest.json` to declare SDL2 at 2.32.x. |
| **G55** | Post-pack | Every `.Native` nupkg contains `janset-native-metadata.json` at root with a valid schema; `upstream_version` field matches the vcpkg-resolved version for the primary triplet; `build_commit` matches the HEAD SHA at pack time. |
| **G56** | Post-pack | Every satellite `.nuspec` declares the cross-family dependency on Core with both lower bound (`>= x.y.z`) and upper bound (`< (UpstreamMajor+1).0.0`). Parsing the exact range expression, not just "dependency present." |
| **G57** | Post-pack | `README.md` mapping table block (delimited by `<!-- JANSET:MAPPING-TABLE-START -->` / `<!-- JANSET:MAPPING-TABLE-END -->`) matches the current `manifest.json` `library_manifests[]` set. Byte-equivalent to the Cake-generated output. |

**Patch-bump enforcement** (release-must-bump-patch / no-overwrite-same-version) is explicitly deferred to Phase 2b. The Phase 2a bar: release playbook policy + a lightweight overwrite guard (if a nupkg at the same `<PackageId>.<Version>.nupkg` path already exists in `artifacts/packages/` at pack time, hard fail). Full API-diff / native-hash enforcement requires internal feed introspection which Stream D-ci delivers.

### 2.10 Meta-package (unchanged from prior canon)

The `Janset.SDL2` meta-package remains defined per `release-lifecycle-direction.md` §4 "The Meta-Package": exact version pins to each family, its own independent SemVer, releases only as part of a full-train. The meta-package's own version does NOT follow the D-3seg rule (it has no single upstream library to anchor). It remains independently SemVered (`1.0.0`, `2.0.0`, …) or date-versioned — that decision remains OPEN and is tracked as a sub-item under PD-7 (full-train orchestration).

---

## 3. Rationale

### 3.1 Why D-3seg over A (independent SemVer)

- **Cognitive load:** "Janset.SDL2.Core 2.32.0 wraps SDL2 2.32.x" is discoverable from the version alone. "Janset.SDL2.Core 1.2.0 wraps SDL2 ???" requires consulting README or release notes.
- **Release discipline naturally aligns with upstream cadence:** UpstreamMinor bumps force family re-release (necessary anyway, because binding API surface expands), which matches how hand-maintained bindings actually work.
- **Patch semantics preserved:** the family's Patch segment stays meaningful for binding-iteration signal, unlike pure upstream-tracked B.

### 3.2 Why D-3seg over B (pure upstream-tracked)

- **vcpkg patch and port_version bumps do NOT force a family release.** The native re-pack happens when the maintainer decides to ship a patch roll-up, not automatically on every upstream touch. Binding can ship a FamilyPatch increment without waiting for a vcpkg change, and conversely can skip three vcpkg patch bumps by shipping FamilyPatch that picks up the latest.
- **No 4-segment SemVer.** MinVer-native, no NuGet consumer-side surprise on 4-part versions.

### 3.3 Why package-first consumer contract

- **Eliminates an entire class of MSBuild `ProjectReference` transitive-asset bugs.** Content injection via `Directory.Build.targets` solved the problem empirically (documented in the now-deprecated Source Mode research note), but at the cost of a parallel consumer contract that drifts from the external-consumer experience.
- **Smoke validates what consumers actually experience.** Every regression caught in smoke is a regression a real external consumer would hit.
- **Removes one dimension of "works here, breaks there":** local smoke green + package-consumer smoke green is the same signal now; they can't diverge because they are the same consumer model.
- **The binding-debug use case (live source change → test) is rare enough to warrant a separate opt-in harness rather than bending the mainline contract.**

### 3.4 Why profile abstraction (Local / RemoteInternal / ReleasePublic)

- **Same consumer contract across modes.** The thing that varies is feed preparation, not consumer behavior. Abstraction captures the actual variability point.
- **CI and local dev converge on one mental model.** `SetupLocalDev --source=remote` is the "download the CI-produced packages and run smoke" equivalent of what external adopters will do.
- **Stream D-ci work is pre-locked at the interface level.** When CI pipeline implementation happens in Phase 2b, it plugs into `RemoteInternal` + `ReleasePublic` resolvers without reshaping the consumer surface.

### 3.5 Why metadata file + README mapping over version-string encoding

- **Version string stays short and NuGet-idiomatic.**
- **Machine-readable metadata file enables CI introspection** (future diff tooling, changelog automation, auditability).
- **Human-readable README table is consumer-facing documentation without requiring users to parse version strings for upstream-version info.**
- **Generated + validated (G57) keeps the two sources (metadata file + README table + manifest) from drifting.**

---

## 4. Consequences

### 4.1 Positive

- Version identity is unambiguous and informative. Consumer reads `Janset.SDL2.Core 2.32.0` and knows the upstream minor line without lookup.
- Smoke, examples, sandbox, and external consumers exercise the same consumer code path. Bug classes collapse.
- MinVer works as designed, no 4-part workarounds.
- Artifact Source Profile abstraction future-proofs CI integration.
- `SetupLocalDev` offers a one-command path from fresh clone to IDE-ready smoke.
- PreFlight G54 + post-pack G55–G57 replace weak conventional hygiene with structural enforcement.

### 4.2 Negative / trade-offs accepted

- **Breaking managed API change path is abnormal.** Requires a separate future ADR when it happens. Accepted because it's expected to be rare and because forcing a binding-rewrite scenario to fit SemVer-major signal was not worth the version-shape complication.
- **Cross-family version shapes differ visually** (Core `2.32.x` vs Image `2.8.x` vs Gfx `1.0.x`). Accepted because `release-lifecycle-direction.md` §3 "Version Independence" already made this a design principle; D-3seg now makes it literal. README mapping table explains.
- **SDL upstream patch version is invisible in the family version string.** Consumer must consult metadata file or README to learn that `Janset.SDL2.Core 2.32.0` wraps SDL 2.32.10 vs 2.32.12. Accepted; metadata file + mapping table are the compensating mechanism.
- **`SetupLocalDev` full loop on a fresh clone is not instant.** vcpkg install on Linux in particular is slow on first run (no cache). Accepted; subsequent runs hit the vcpkg binary cache.
- **Source Mode research work is superseded.** The symlink-preservation findings remain useful reference material (for future remote-feed tar extraction), but the ProjectReference mechanism is retired.
- **Binding-debug fast-loop flow is no longer mainline.** If and when needed, a separate harness will be established. Accepted.
- **Phase 2b workload includes patch-bump strict enforcement, remote profile impl, and Stream D-ci integration.** These were scope-deferred in this ADR to preserve delivery tempo; they are tracked in `phase-2-adaptation-plan.md` and PD-7 / PD-8.

### 4.3 Neutral

- Meta-package versioning scheme remains OPEN (sub-item of PD-7). D-3seg does not commit one way or another.

---

## 5. Non-goals (explicit exclusions)

This ADR does NOT:

- Specify a mechanism for signaling breaking managed API changes in the version string. That is deferred to a hypothetical future ADR if/when such a change is required.
- Implement the `RemoteInternal` or `ReleasePublic` `IArtifactSourceResolver` concrete classes. Those are Phase 2b deliverables under Stream D-ci.
- Implement strict release-must-bump-patch enforcement via nupkg hash diff / API surface comparison. Phase 2a uses file-existence overwrite guard only.
- Change the meta-package versioning scheme. That decision is tracked separately under PD-7.
- Retain any Source Mode or ProjectReference-based native-payload mechanism. These are explicitly retired.
- Reopen the family-lock decision (managed + native share one version). This is canon and remains locked.
- Reopen the minimum-range within-family dependency contract. Post-S1 canon remains.
- Reopen MinVer as the tag-derivation tool choice. It stays.

---

## 6. Precedent survey reference

Full analysis in the 2026-04-18 conversation transcript (strategic discussion across multiple LLM sessions). Key observations:

| Project | Version scheme | Relevance |
|---|---|---|
| SkiaSharp | Independent SemVer (`3.116.x`) | Closest architectural analogue (binding + native), but curated API model not ours |
| ppy/SDL3-CS | Date-versioned auto-gen | Mechanical wrapper pattern, informs our acceptance of "binding-only releases are rare" |
| flibitijibibo/SDL2-CS | Independent `1.x.y` | Hand-maintained; our current external dependency (transitional; retires with CppAst migration) |
| Silk.NET | Unified cross-binding | Different scope model (multi-binding monorepo, single version) |
| LibGit2Sharp | 4-segment approx-upstream-tracked | Closest precedent to D-3seg direction; we simplify by dropping the 4th segment |
| MonoGame | Independent (`3.8.x`) | Curated API pattern, not ours |
| Avalonia, OpenTelemetry | Independent SemVer | Cited in post-S1 canon for minimum-range dependency contract; unchanged |

---

## 7. Impact Checklist

Tracks the implementation work required by this ADR. Each item updates as waves complete. This section is a **living part of the ADR** — not a historical snapshot.

### 7.1 Canonical docs (Wave V2)

- [x] `docs/knowledge-base/release-lifecycle-direction.md` — §3 Versioning Model full rewrite (D-3seg), §4 Dependency Contract Model (upper bound added), §7 Two Version Planes renamed to "Cross-Referenced Version Planes" and rewritten, Tradeoffs #4/#5 updated + #6/#7 added — **DONE 2026-04-18**
- [x] `docs/knowledge-base/release-guardrails.md` — G54–G57 new rows, stream-mapping + failure-mode catalog + cross-references updated — **DONE 2026-04-18**
- [x] `docs/plan.md` — Strategic Decisions rows: execution model (3-mode → 2-source), tag-derived versioning (D-3seg), source-graph-vs-shipping-graph (package-first), two-source framework (profile abstraction) — **DONE 2026-04-18**
- [x] `docs/phases/phase-2-adaptation-plan.md` — ADR-001 addendum, PD-12 created, PD-4 CLOSED, PD-5 REFRAMED, PD-6 CLOSED, Stream F scope rewritten (Source Mode retired → feed-prep), Strategy State Audit `INativeAcquisitionStrategy` retired, Execution Model diagram rewritten — **DONE 2026-04-18**
- [x] `docs/research/execution-model-strategy-2026-04-13.md` — ADR-001 amendment header block explaining three-mode → two-source shift — **DONE 2026-04-18**
- [x] `docs/research/source-mode-native-visibility-2026-04-15.md` — DEPRECATED banner, symlink findings kept as reference for future remote-feed caching — **DONE 2026-04-18**

### 7.2 Moderate doc amendments (Wave V3)

- [x] `docs/knowledge-base/ci-cd-packaging-and-release-plan.md` — D-3seg schema note; `native_lib_version` removed from library_manifests example; G54/G55 references added — **DONE 2026-04-18**
- [x] `docs/knowledge-base/cake-build-architecture.md` — manifest example updated (native_lib_version removed, vcpkg_port_version added), ADR-001 schema note — **DONE 2026-04-18**
- [x] `docs/research/full-train-release-orchestration-2026-04-16.md` — ADR-001 addendum added at top (meta-package versioning OPEN sub-item, D-3seg placeholder note on version-string examples) — **DONE 2026-04-18**
- [x] `docs/research/release-recovery-and-manual-escape-hatch-2026-04-16.md` — ADR-001 addendum added (D-3seg + Artifact Source Profile impact on manual escape-hatch design) — **DONE 2026-04-18**
- [x] `docs/research/cake-strategy-implementation-brief-2026-04-14.md` — manifest example updated, ADR-001 schema note — **DONE 2026-04-18**
- [x] `docs/playbook/cross-platform-smoke-validation.md` — `--family-version` examples updated to D-3seg (2.32.0-smoke.1), D-3seg caveat note on multi-family packing, "Relationship to Local Dev (ADR-001)" section added — **DONE 2026-04-18**
- [x] `docs/playbook/vcpkg-update.md` — "Update native_lib_version" step removed; Step 6a mapping-table regeneration added; ADR-001 reference — **DONE 2026-04-18**
- [x] `docs/playbook/adding-new-library.md` — manifest entry updated (native_lib_version removed); ADR-001 note; mapping-table step added — **DONE 2026-04-18**
- [x] `docs/playbook/local-development.md` — ADR-001 transition banner; Quick Start (recommended V5) section added; retired triplets replaced with hybrid variants; "Until PackageTask" wording retired; Step 5 rewritten as "Pack Families" with D-3seg version note; G46 note on direct pack; D-3seg version note on manual pack example — **DONE 2026-04-18**
- [x] `docs/onboarding.md` — Repository tree fixed (`build/_build.Tests/` instead of `tests/Build.Tests/`), test count updated 241→324, Family Version glossary updated to D-3seg + G54 reference — **DONE 2026-04-18**
- [x] `docs/phases/phase-2-cicd-packaging.md` — ADR-001 amendment header, D-3seg references, stale section flags (§2.2/§2.3/§2.4 status) — **DONE 2026-04-18**
- [x] `docs/README.md` — Reviews table expanded (7 rows + entry-point consolidated index); Decisions (ADR) section added; retention policy paragraph added — **DONE 2026-04-18** (closes review-finding L38 + L39)

### 7.3 Historical markers (Wave V6)

- [ ] `docs/research/exact-pin-spike-and-nugetizer-eval-2026-04-16.md` — add ADR pointer to SUPERSEDED header
- [ ] `docs/research/release-lifecycle-patterns-2026-04-14-claude-opus.md` — "D-3seg adopted 2026-04-18" marker
- [ ] `docs/research/release-lifecycle-strategy-research-2026-04-14-gpt-codex.md` — same
- [ ] `docs/research/release-strategy-history-audit-2026-04-14-gpt-codex.md` — same; `binding_version` question closed

### 7.4 Manifest + model cleanup (Wave V4)

- [x] Final repo-wide grep for `native_lib_version` / `NativeLibVersion` consumption (source/build-host scope; generated test-output artifacts excluded) — orphan confirmed before removal — **DONE 2026-04-19**
- [x] `build/manifest.json` — remove `native_lib_version` from 6 library entries — **DONE 2026-04-19**
- [x] `build/_build.Tests/Fixtures/Data/manifest.fixture.json` — same — **DONE 2026-04-19**
- [x] `build/_build/Context/Models/ManifestConfigModels.cs` — remove `NativeLibVersion` property — **DONE 2026-04-19**
- [x] `build/_build.Tests/Fixtures/ManifestFixture.cs` — remove fixture seeding — **DONE 2026-04-19**
- [x] `build/_build.Tests/Fixtures/Seeders/ManifestConfigSeeder.cs` — checked; no remaining `NativeLibVersion` reference — **DONE 2026-04-19**

### 7.5 New validators + metadata (Wave V4, merged with cleanup)

- [x] `UpstreamVersionAlignmentValidator` (G54) + contract + Result + tests — **DONE 2026-04-19**
- [x] `NativePackageMetadataValidator` (G55) + validator + PackageOutputValidator integration tests — **DONE 2026-04-19**
- [x] `SatelliteUpperBoundValidator` (G56) + validator + PackageOutputValidator integration tests — **DONE 2026-04-19**
- [x] `ReadmeMappingTableValidator` (G57) + validator + PackageOutputValidator integration tests — **DONE 2026-04-19**
- [x] Native metadata file generator (pack-time include enforced from `.targets`) — **DONE 2026-04-19**
- [x] README mapping table generator + HTML comment markers — **DONE 2026-04-19**
- [x] `PackageOutputValidator` — G55–G57 wired into the existing result-accumulation pattern (G54 wired in PreFlight pipeline) — **DONE 2026-04-19**
- [x] Build.Tests green post-edit — **DONE 2026-04-19**

### 7.6 Profile abstraction + SetupLocalDev (Wave V5)

- [x] `IArtifactSourceResolver` interface + `ArtifactProfile` enum + `IArtifactSourceResolver` infrastructure — **DONE 2026-04-19**
- [x] `LocalArtifactSourceResolver` concrete — **DONE 2026-04-19**
- [x] `RemoteArtifactSourceResolver` stub (contract-only) — **DONE 2026-04-19**
- [x] `ReleaseArtifactSourceResolver` stub (contract-only) — **DONE 2026-04-19**
- [x] `SetupLocalDev` Cake task — `--source=local` fully wired, `--source=remote` accepted but stubbed — **DONE 2026-04-19**
- [x] `Janset.Smoke.local.props` conditional import in `Janset.Smoke.props` — **DONE 2026-04-19**
- [x] `.gitignore` — `build/msbuild/Janset.Smoke.local.props` — **DONE 2026-04-19**
- [ ] IDE open test: smoke csproj restores + builds in IDE after `SetupLocalDev` runs
- [ ] Source Mode mechanism removal — any landed code retires

### 7.7 Memory sidecar (Wave V6)

- [ ] Update `packaging_strategy_decisions.md` memory
- [ ] Retire `release_lifecycle_direction_2026_04_15.md` memory (rename or supersede)
- [ ] Create `versioning_d3seg_2026_04_18.md` memory

---

## 8. References

### 8.1 Superseded / amended by this ADR

- `docs/knowledge-base/release-lifecycle-direction.md` (§3, §4, §7 amendments; rest stays)
- `docs/research/execution-model-strategy-2026-04-13.md` (three-mode model retired)
- `docs/research/source-mode-native-visibility-2026-04-15.md` (DEPRECATED)

### 8.2 Cited unchanged

- `docs/knowledge-base/release-guardrails.md` §G-number registry structure
- `docs/phases/phase-2-adaptation-plan.md` Stream identifier convention
- `docs/research/full-train-release-orchestration-2026-04-16.md` (PD-7; only version-shape clauses are affected)
- `docs/research/release-recovery-and-manual-escape-hatch-2026-04-16.md` (PD-8; same)

### 8.3 External references

- [NuGet package versioning](https://learn.microsoft.com/en-us/nuget/concepts/package-versioning?tabs=semver20sort) — SemVer 2.0 convention
- [MinVer documentation](https://github.com/adamralph/minver) — tag-based versioning
- [SemVer 2.0 specification](https://semver.org/)
- Industry precedent: SkiaSharp, ppy/SDL3-CS, flibitijibibo/SDL2-CS, Silk.NET, LibGit2Sharp, MonoGame (reviewed in §6)

---

## 9. Change log

| Date | Change | Editor |
|---|---|---|
| 2026-04-18 | Initial draft and adoption | Deniz İrgin + LLM synthesis session |
