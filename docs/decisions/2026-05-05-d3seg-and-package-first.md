# ADR-001: D-3seg Versioning + Package-First Consumer Contract

- **Status:** Accepted
- **Date:** 2026-05-05
- **Supersedes:** an earlier 2026-04-18 ADR-001 retired during the 2026-05-04 docs cleanup. The earlier version covered three concerns; the third (`IArtifactSourceResolver` Cake abstraction) was retired in Phase Y when feed preparation moved to repo-root `tools.cs setup`. This lightweight version preserves the two normative decisions still active in code.

## 1. Versioning — D-3seg

Family version format: `<UpstreamMajor>.<UpstreamMinor>.<FamilyPatch>`.

- **UpstreamMajor.UpstreamMinor** is anchored to `manifest.library_manifests[].vcpkg_version` first two segments. Enforced at PreFlight by guardrail **G54**.
- **FamilyPatch** is the repo's own iteration counter within a given `UpstreamMajor.UpstreamMinor` line. Monotonically increasing; reset to `0` when either upstream segment changes.
- No build / fourth segment.
- Prereleases follow NuGet SemVer 2.0 (`-rc.1`, `-local.<timestamp>`, `-ci.<run-id>.<attempt>`). Prereleases preserve the `UpstreamMajor.UpstreamMinor` anchor — `0.1.0-local.*` is rejected.

| Family | Wraps | Version |
| --- | --- | --- |
| `sdl2-core` | SDL2 2.32.10 | `Janset.SDL2.Core 2.32.0` |
| `sdl2-image` | SDL2_image 2.8.8 | `Janset.SDL2.Image 2.8.0` |
| `sdl2-gfx` | SDL2_gfx 1.0.4 | `Janset.SDL2.Gfx 1.0.0` |
| `sdl3-core` (future) | SDL3 3.4.4 | `Janset.SDL3.Core 3.4.0` |

vcpkg patch and port_version bumps do **not** automatically force a family release. Native re-pack happens on the maintainer's release cadence; the binding can ship a `FamilyPatch` increment without waiting for a vcpkg change. Exact upstream metadata lives in `janset-native-metadata.json` per `.Native` nupkg (asserted by **G55**) plus the README mapping table block (asserted by **G57**).

### Family-lock

A package family is one managed package + one native package, sharing the same version, always released together. There is no separate "binding version" and "native version" within a family.

### Dependency contracts

| Boundary | Contract | Enforcement |
| --- | --- | --- |
| Within-family (e.g. `Janset.SDL2.Image` → `Janset.SDL2.Image.Native`) | minimum range (`>= x.y.z`) | post-pack G21 + G23 (byte-equal version) |
| Cross-family (e.g. `Janset.SDL2.Image` → `Janset.SDL2.Core`) | minimum + upper bound (`>= x.y.z, < (UpstreamMajor+1).0.0`) | post-pack G56 |
| Cross-family resolvability at pack time | every declared cross-family dep is in the resolved versions mapping (feed-probe relaxation deferred — `release-guardrails.md` §4.1 gap #2) | Pack stage G58 + PreFlight G58 mirror |

### Breaking managed API changes

Out of scope for D-3seg. If a binding-only API break is required (e.g., a CppAst generator restructures namespaces beyond what an SDL major bump would imply), it requires a separate ADR documenting the break, the affected consumer surface, and the transition plan. Expected to be rare.

## 2. Package-first consumer contract

> All consumer-facing validation paths use packages; local vs remote changes only how the local feed is prepared.

- Every consumer-side csproj (smoke, sample, sandbox, future contributor projects) consumes Janset packages via `PackageReference` against a local folder feed.
- No `ProjectReference` chain reaches from a consumer csproj into `src/native/`. The retired Source Mode mechanism (MSBuild `<Content>` injection from staging into consumer `bin/`) stays retired.
- Local feed preparation lives in `tools.cs setup` (`--source=local | remote-github | remote-nuget`). The Cake build host has no role in feed preparation.
- The local consumer override file `build/msbuild/Janset.Local.props` (gitignored) carries `<LocalPackageFeed>` plus per-family `<JansetSdl<N><Role>PackageVersion>` properties. It is imported conditionally by `build/msbuild/Janset.Smoke.props`.

### What's locked

- Package-first is the only supported consumer model.
- `tools setup --source=local` is the canonical fresh-clone bootstrap path (vcpkg → harvest → consolidate → package → write `Janset.Local.props`).
- `tools setup --source=remote-github` exercises the same consumer surface against the latest GitHub Packages internal-feed wave. Requires a Classic PAT with `read:packages` (fine-grained PATs are unsupported by GH Packages NuGet).
- `tools setup --source=remote-nuget` is stubbed pending public-feed promotion (Phase 2b PD-7).

### Out of scope (tracked elsewhere)

- Public NuGet promotion path (PD-7).
- Operator manual escape hatch (PD-8).
- Tag-push workflow fan-out (gap #4 in `release-guardrails.md` §4.1).

## 3. Why this shape

- **D-3seg over independent SemVer.** The consumer reads `Janset.SDL2.Core 2.32.0` and knows the upstream minor line without consulting the README. Release discipline aligns with upstream cadence.
- **D-3seg over pure upstream-tracked.** vcpkg patch / port_version bumps do not force a family release. `FamilyPatch` belongs to the repo's iteration cadence, not upstream's.
- **Package-first over Source Mode.** Smoke catches the same bugs an external consumer would catch. Eliminates the "works in source mode, breaks in package mode" failure class.

## 4. References

- [`AGENTS.md`](../../AGENTS.md) "Settled Strategic Decisions" + "Configuration File Relationships" + "Build Host Pipeline"
- [`docs/knowledge-base/release-guardrails.md`](../knowledge-base/release-guardrails.md) — G54, G55, G56, G57, G58 + dependency contract guardrails
- `build/manifest.json library_manifests[].vcpkg_version` — UpstreamMajor.Minor anchor source
- `build/_build/Targets/{ResolveVersionsFromManifest,ResolveVersionsFromExplicit,StageVersions}/` — version provider implementations
- `tools.cs` — feed preparation orchestration
