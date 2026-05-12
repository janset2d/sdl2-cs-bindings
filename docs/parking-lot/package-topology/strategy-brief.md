# Package Topology Refactor Strategy Brief

> Working draft. This is not canonical project policy yet. Promote accepted decisions into ADRs, release guardrails, playbooks, onboarding, and README after review.

## Decision Hypothesis

Reframe a package family from "managed package + native package" to a role-level release unit made of:

```text
Janset.SDL2.<Role>              role metapackage
Janset.SDL2.<Role>.Bindings     managed-only P/Invoke bindings
Janset.SDL2.<Role>.Native       native-only payload
```

The same shape should be reusable for SDL3 when Phase 5 activates. A role family remains versioned and released together, but it now emits three packages instead of two.

## WHY

### Current contract hides two different promises in one package

The current user-facing package `Janset.SDL2.<Role>` is both:

1. The managed binding API.
2. The convenience package that brings matching native binaries.

That is ergonomic for normal consumers, but it weakens the project's explicit value proposition: users should be able to consume bindings and native binaries separately when they need to. Advanced cases include:

- system-provided SDL/native deployment;
- custom native builds;
- future external-native override support;
- bindings-only compile-time use;
- native-only smoke/probe scenarios;
- package-size-sensitive deployment experiments.

Today, those cases fight the package graph because the managed binding package transitively carries the native package.

### Public release will freeze package IDs into user muscle memory

Renaming `Janset.SDL2.Image` to `Janset.SDL2.Image.Bindings` after nuget.org publication would create avoidable upgrade churn. Before production, package topology is still malleable. After production, it becomes public API.

This refactor is therefore a hard prerequisite for Phase 2b PD-7 (first public NuGet prerelease). PD-7 must not fire until role metapackages, bindings packages, and native packages are emitting under the new IDs.

### The build pipeline already thinks in role families

The existing D-3seg versioning, manifest family identifiers, release tags, and cross-family dependency logic already model `sdl2-image` as the release unit. The refactor does not need to make bindings and native independently versioned. It only needs to make the emitted package set more honest.

## HOW

### Package layers

| Layer | Example | Contains | Dependency policy | Symbol package |
| --- | --- | --- | --- | --- |
| Bindings package | `Janset.SDL2.Image.Bindings` | Managed P/Invoke declarations only | Satellite bindings depend on `Janset.SDL2.Core.Bindings` with an SDL-major bounded range. No dependency on native packages. | `.snupkg` produced |
| Native package | `Janset.SDL2.Image.Native` | RID native payload + `buildTransitive` targets + native metadata | Satellite native packages depend on `Janset.SDL2.Core.Native` with an SDL-major bounded range. The hybrid-static contract static-bakes transitive deps into satellites, but SDL core remains dynamic and is filtered out of satellite payloads. | None; current `.Native` behavior preserved |
| Role metapackage | `Janset.SDL2.Image` | No assets, only dependencies | Exact-pins same-role `Image.Bindings` and `Image.Native`; for satellites, also depends on the core role metapackage with an SDL-major bounded range. | None; assetless |
| Ultimate metapackage | `Janset.SDL2` | No assets, only dependencies | Deferred past the first public release. When implemented, it should depend on role metapackages with bounded ranges, not exact pins. It is a convenience entrypoint, not a lockfile. | None; assetless |

### Exact pins belong inside a role, not at the ultimate bundle

Role metapackage dependency example:

```text
Janset.SDL2.Image 2.8.3
├── Janset.SDL2.Image.Bindings [2.8.3]
├── Janset.SDL2.Image.Native   [2.8.3]
└── Janset.SDL2.Core           >= 2.32.0, < 3.0.0
```

Future ultimate metapackage dependency example:

```text
Janset.SDL2 2.32.0
├── Janset.SDL2.Core  >= 2.32.0, < 3.0.0
├── Janset.SDL2.Image >= 2.8.0,  < 3.0.0
├── Janset.SDL2.Mixer >= 2.8.0,  < 3.0.0
├── Janset.SDL2.Ttf   >= 2.24.0, < 3.0.0
└── Janset.SDL2.Gfx   >= 1.0.0,  < 2.0.0
```

The ultimate metapackage should not exact-pin every role because this project supports both targeted family releases and full release trains. Exact pins would force every single-family release to republish `Janset.SDL2`, effectively turning the ultimate metapackage into a lockfile and undermining targeted release.

The first public NuGet release should ship role metapackages only. `Janset.SDL2` should remain deferred until the project has an explicit release-train versioning policy for an aggregate package that spans libraries with different upstream version anchors.

### Versioning stance

Role family versions continue to use D-3seg:

```text
<UpstreamMajor>.<UpstreamMinor>.<FamilyPatch>
```

Within a role family, all emitted packages share the same version:

```text
Janset.SDL2.Image 2.8.3
Janset.SDL2.Image.Bindings 2.8.3
Janset.SDL2.Image.Native 2.8.3
```

The ultimate metapackage needs a separate decision because it spans libraries with different upstream version anchors. Candidate policies:

| Option | Version source | Pros | Cons |
| --- | --- | --- | --- |
| Core-anchored | SDL core D-3seg line, e.g. `2.32.x` | Familiar to users; aligns with SDL2 identity | Ultimate package can change when only satellite baselines change, so the version may look core-driven even when the content change is not |
| Train-anchored | Dedicated train version, e.g. `2.0.x` or `2.32-train.x` style prerelease during previews | Makes the bundle a release artifact | Adds a second versioning concept |
| Delay ultimate meta | Ship role metapackages first; add `Janset.SDL2` after first release-train policy is locked | Avoids premature commitment | Defers the easiest onboarding package |

Decision for this refactor: ship role metapackages as part of the first public release path, but defer `Janset.SDL2` implementation and versioning policy until after the role topology has shipped and at least one release-train cycle has been rehearsed.

### Role metapackage implementation stance

Role metapackages should preferably be SDK-style packable projects, not hand-maintained nuspec files. However, the exact project template is an implementation spike, not a strategy-level settled detail, because this repository uses Central Package Management and currently shapes dependency ranges through build-host post-pack normalization. Do not hard-code `PackageReference Version="[$(Version)]"` in the strategy before validating restore and pack behavior under CPM.

The implementation plan should compare:

1. SDK-style role metapackage projects using `ProjectReference` and post-pack nuspec normalization for exact same-role dependencies.
2. SDK-style role metapackage projects using generated package references or explicit nuspec generation only if the `ProjectReference` route cannot emit the required dependency graph cleanly.

Avoid nuspec-only metapackages unless the spike proves SDK-style projects introduce more complexity than they remove.

## WHAT

### Impact inventory

| Area | Current shape | Expected impact |
| --- | --- | --- |
| `build/manifest.json` | `schema_version` is `2.1`; `package_families[]` has `managed_project` + `native_project` | Bump schema to `2.2`. Replace or extend with `meta_project`, `bindings_project`, `native_project`. Keep `library_ref`, `depends_on`, `tag_prefix`, `change_paths`. PreFlight should reject stale `2.1` manifests after this slice ships. |
| Managed projects under `src\SDL2.*` | Package IDs are `Janset.SDL2.<Role>` and include native `ProjectReference` | Rename package IDs to `Janset.SDL2.<Role>.Bindings`; remove same-role native dependency from bindings packages. |
| Native projects under `src\native\SDL2.*.Native` | Package IDs remain `Janset.SDL2.<Role>.Native` | Mostly preserved. Wire satellite cross-native dependency to `Janset.SDL2.Core.Native` with an SDL-major bounded range, per the hybrid-static / dynamic-core contract documented in §HOW Package layers. |
| Role metapackage projects | Not present | Add assetless projects for `Janset.SDL2.<Role>`. Preferred physical location for impact mapping: `src\meta\SDL2.<Role>\SDL2.<Role>.csproj`, parallel to `src\native\SDL2.<Role>.Native\`. |
| Ultimate metapackage project | Planned but not present | Defer past first public release. Do not include in this refactor's implementation scope except for documenting the future bounded-range policy. |
| `PackageTask` | Packs native then managed per family | Pack native, bindings, then role meta per family. Ultimate metapackage packing is out of scope for this refactor; revisit when `Janset.SDL2` is unblocked per the deferred-versioning decision. |
| `PackageArtifacts` model | Managed package + symbols + native package | Split managed into bindings package + symbols and role meta package. Native package remains. |
| Family naming conventions | Managed package ID = `Janset.SDL<Major>.<Role>` | Add role meta package ID, bindings package ID, and native package ID helpers. |
| Csproj validators | G6/G7 assume managed/native pair | Validate all declared family project paths and all three package IDs. |
| Nuspec validators | G21/G23 enforce managed -> native minimum range and version equality | Reframe as role meta exact-pins same-role bindings/native; bindings package must not depend on same-role native. |
| Cross-family validators | G56/G58 operate on managed role packages | Validate dependency graph at both role-meta and component (bindings + native) levels, per the Open Decisions table. Role metas exact-pin same-role components; satellite components carry bounded ranges to core counterparts. |
| Smoke MSBuild props/targets | Smoke expands role names to `Janset.SDL2.<Role>` PackageReferences | Keep role-meta smoke as default; add bindings-only restore/build coverage. Co-design this with Phase 2b Gap #3 partial-scope smoke work; do not pre-commit to one csproj per family per layer until the impact map compares shared/parameterized alternatives. |
| README mapping table | Maps role package to upstream/native metadata | Expand to show role meta, bindings, native, upstream version, and port version. |
| SDL3 phase docs | Mirror current managed/native topology | Update after strategy is accepted so SDL3 starts with the new topology. |

### Test strategy

The refactor should be test-first because the current behavior is encoded in several guardrails.

| Test layer | New coverage |
| --- | --- |
| Convention unit tests | `RoleMetaPackageId("sdl2-image") -> Janset.SDL2.Image`, `BindingsPackageId(...) -> Janset.SDL2.Image.Bindings`, `NativePackageId(...) -> Janset.SDL2.Image.Native`. |
| Manifest model tests | Real manifest declares all required project paths for concrete families. Placeholder families fail with actionable messages if selected for packaging. |
| Csproj contract validator tests | Role meta, bindings, and native project IDs match conventions; bindings package does not reference same-role native project. |
| Package output validator tests | Role meta exact-pins same-version bindings/native; bindings package has no native dependency; satellite bindings/native packages and satellite role metas carry the expected bounded ranges to the corresponding core packages. |
| PackageTask scenario tests | Selected family produces three `.nupkg` files plus bindings `.snupkg`; native metadata and payload validation still run. |
| Consumer smoke tests | Default smoke references role metapackages and runs P/Invoke; bindings-only smoke restores/builds without native assets and does not execute native calls. Physical smoke project count is an impact-map decision, not a strategy-level commitment. |
| Regression tests | Existing native payload shape, Unix tarball rename/extraction, license payload, and G55 metadata checks still apply unchanged to `.Native` packages. |

## Plan Shape

### Phase 0: finish strategy and impact analysis

1. Agree on package taxonomy and dependency policy.
2. Defer ultimate metapackage implementation past the first public release and record the future bounded-range policy.
3. Spike the SDK-style role metapackage project template under CPM and confirm how exact same-role dependency pins will be emitted.
4. Produce `docs\superpowers\package-topology\impact-map.md` as the file-by-file impact map.
5. Convert accepted decisions into an implementation plan.

### Phase 1: characterization tests

1. Add tests that describe current package graph behavior.
2. Add failing tests for the desired package graph.
3. Keep native build tests out of scope unless package graph changes touch native payload behavior.

### Phase 2: manifest and convention model

1. Add package ID helpers for role meta, bindings, and native.
2. Extend manifest schema to `2.2` and real-manifest tests.
3. Update selection and version mapping code only enough to understand the three-package family shape.

### Phase 3: project topology

1. Rename current managed package IDs to `.Bindings`.
2. Add role metapackage projects, tentatively under `src\meta\SDL2.<Role>\`.
3. Preserve existing source folder names unless a later review decides physical folder renames are worth the churn.

### Phase 4: packaging and guardrails

1. Teach `PackageTask` to emit three packages per role family.
2. Rework G6/G7/G21/G23/G56/G58 around the new graph.
3. Keep G46/G47/G48/G51/G52/G55 native checks focused on `.Native`.

### Phase 5: consumer validation

1. Keep role metapackage smoke as the default user-facing smoke.
2. Add bindings-only restore/build validation.
3. Fold this into Phase 2b Gap #3 partial-scope smoke design so the topology refactor does not create a second smoke decomposition effort.
4. Confirm package-first flow still uses local feed packages, not project references.

### Phase 6: canonical documentation

1. Supersede or revise ADR-001.
2. Update onboarding topology and glossary.
3. Update release guardrails.
4. Update adding-new-library playbook.
5. Update README install guidance and mapping table.
6. Update Phase 5 SDL3 topology.

## Current Open Decisions

| Decision | Recommended default |
| --- | --- |
| Ultimate metapackage versioning | Deferred past the first public release. Do not implement `Janset.SDL2` in this topology refactor; future policy must use bounded role-meta ranges, not exact pins. |
| Role metapackage implementation | Prefer SDK-style packable projects, but spike the exact template under CPM before locking XML. Avoid direct `PackageReference Version="[$(Version)]"` until restore/pack behavior is verified. |
| Physical folder renames | Avoid initially. Change PackageId first; folder renames can be a separate cleanup if worth it. |
| Role metapackage physical location | Tentatively `src\meta\SDL2.<Role>\SDL2.<Role>.csproj`; confirm in `impact-map.md` before implementation. |
| Cross-family validation level | Validate both role-meta dependencies and component-level direct-use dependencies when both are emitted. |
