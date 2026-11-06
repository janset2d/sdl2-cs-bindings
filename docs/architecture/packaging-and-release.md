# Packaging and Release

Date: 2026-11-06

## Package and version contract

[ADR-0001](../adr/0001-d3seg-and-package-first.md) owns D-3seg, family-lock, dependency ranges and
the package-first consumer contract. One family is a managed package and a `.Native` package at
the same version. Consumers reference the managed package; native assets arrive transitively.
Use the [manifest](../../build/manifest.json) for concrete families, library metadata and runtime mappings.
Read TFMs and package metadata from [build properties](../../Directory.Build.props).

Native payloads come from [the native pipeline](native-build.md), not source-tree fallback files.
Windows packages carry DLLs. Unix packages carry a package-named tar.gz preserving symlinks, with
the shared [consumer targets](../../src/native/_shared/Janset.SDL2.Native.Common.targets).
Both the package wrapper and shared targets must ship. Build assets must remain visible through
the managed-to-native dependency, including for .NET Framework consumers.

## Guardrail principles

Fail early and explicitly. Run cross-cutting input checks before the matrix, package checks at pack,
and consumer checks against actual packages. Critical invariants have independent checks at more
than one boundary. Report the offending input and value; never silently skip a failed check.
New executable invariants ship with tests that prove refusal at the owning boundary.

### Stage routing

| Stage | Responsibility | Implementation |
|---|---|---|
| PreFlight | Manifest/project/overlay coherence and version reachability | [PreFlightCheckTask](../../build/_build/Targets/PreFlightCheck/PreFlightCheckTask.cs) |
| Harvest | Binary closure, static-dependency leaks, primary output | [HarvestTask](../../build/_build/Targets/Harvest/HarvestTask.cs) |
| NativeSmoke | OS-level native loading and initialization | [NativeSmoke](../../build/_build/Targets/NativeSmoke/) |
| Consolidation | Replace cross-RID receipts and license union atomically | [ConsolidateHarvestTask](../../build/_build/Targets/ConsolidateHarvest/ConsolidateHarvestTask.cs) |
| Pack | Prepare metadata, pack both family members, validate output | [PackageTask](../../build/_build/Targets/Package/PackageTask.cs) |
| ConsumerSmoke | Restore, extract and run packaged interop for executable TFMs | [PackageConsumerSmoke](../../build/_build/Targets/PackageConsumerSmoke/) |
| Staging | Push coherent family pairs; refuse local-suffix packages | [PublishStagingTask](../../build/_build/Targets/PublishStaging/PublishStagingTask.cs) |

### Preflight registry

Each row names the current enforcement owner, not a proposed class.
New IDs continue after the highest ID ever assigned; IDs are never reused.

| ID | Invariant | Owner |
|---|---|---|
| G6 | Canonical managed/native package IDs | [CsprojPackContractValidator](../../build/_build/Validation/Manifest/CsprojPackContractValidator.cs) |
| G7 | Native project reference resolves to the manifest project | [CsprojPackContractValidator](../../build/_build/Validation/Manifest/CsprojPackContractValidator.cs) |
| G14 | Root override and library upstream version agree | [VersionConsistencyValidator](../../build/_build/Validation/Manifest/VersionConsistencyValidator.cs) |
| G15 | Root override and library port-version agree | [VersionConsistencyValidator](../../build/_build/Validation/Manifest/VersionConsistencyValidator.cs) |
| G16 | Runtime uses an existing hybrid overlay triplet | [HybridStaticOverlayValidator](../../build/_build/Validation/Packaging/HybridStaticOverlayValidator.cs) |
| G17 | Family dependencies reference existing family identifiers | [CsprojPackContractValidator](../../build/_build/Validation/Manifest/CsprojPackContractValidator.cs) |
| G18 | Family library reference exists | [CsprojPackContractValidator](../../build/_build/Validation/Manifest/CsprojPackContractValidator.cs) |
| G49 | Exactly one core library matches packaging core identity | [CoreLibraryIdentityValidator](../../build/_build/Validation/Manifest/CoreLibraryIdentityValidator.cs) |
| G54 | Resolved family major/minor matches its upstream major/minor | [UpstreamVersionAlignmentValidator](../../build/_build/Validation/Versioning/UpstreamVersionAlignmentValidator.cs) |
| G58 | Every cross-family dependency is in the resolved invocation | [CrossFamilyDependencyResolvabilityValidator](../../build/_build/Validation/Versioning/CrossFamilyDependencyResolvabilityValidator.cs), repeated by PackageTask |
| G59 | Family names match lowercase `sdl<major>-<role>` conventions | [ManifestFamilyNameInvariantValidator](../../build/_build/Validation/Manifest/ManifestFamilyNameInvariantValidator.cs) |
| G60 | Every overlay version and port-version matches upstream at the pinned submodule commit | [OverlayPortVersionCoherenceValidator](../../build/_build/Validation/Vcpkg/OverlayPortVersionCoherenceValidator.cs) |

### Harvest, consolidation and package registry

| ID | Invariant | Owner |
|---|---|---|
| G12 | Each central PackageReference has a matching central version | NuGet restore |
| G13 | Manifest parses into the required typed configuration | [ManifestRepository](../../build/_build/Data/Manifest/ManifestRepository.cs) |
| G19 | No non-system transitive shared-library leaks | [HybridStaticLeakValidator](../../build/_build/Validation/Harvesting/HybridStaticLeakValidator.cs) |
| G21 | Within-family bare minimum range, cross-family lower bound, visible build assets | [PackageOutputValidator](../../build/_build/Validation/Packaging/PackageOutputValidator.cs) |
| G22 | Dependency groups agree across all TFMs resolved from the project | [PackageOutputValidator](../../build/_build/Validation/Packaging/PackageOutputValidator.cs) |
| G23 | Managed/native versions match byte-for-byte | [PackageOutputValidator](../../build/_build/Validation/Packaging/PackageOutputValidator.cs) |
| G25 | Managed symbol package exists and contains nuspec plus PDB | [PackageOutputValidator](../../build/_build/Validation/Packaging/PackageOutputValidator.cs) |
| G26 | Repository metadata records the expected commit SHA | [PackageOutputValidator](../../build/_build/Validation/Packaging/PackageOutputValidator.cs) |
| G27 | ID, version, authors, license and icon match project metadata | [PackageOutputValidator](../../build/_build/Validation/Packaging/PackageOutputValidator.cs) |
| G46 | Native pack requires an external payload source | `_GuardAgainstEmptyNativePayload` in [source targets](../../src/Directory.Build.targets) |
| G47 | Native package contains its wrapper and common consumer targets | [PackageOutputValidator](../../build/_build/Validation/Packaging/PackageOutputValidator.cs) |
| G48 | Windows RID has DLLs and no tarball; Unix RID has exactly one package-named tarball | [PackageOutputValidator](../../build/_build/Validation/Packaging/PackageOutputValidator.cs) |
| G50 | Harvest produces at least one primary binary for each selected library/RID | [HarvestTask](../../build/_build/Targets/Harvest/HarvestTask.cs) |
| G51 | Native package includes license attribution | [PackageOutputValidator](../../build/_build/Validation/Packaging/PackageOutputValidator.cs) |
| G52 | Pre-pack checks actual runtimes and consolidated licenses, not the parent license directory | [PackageFamilyPacker](../../build/_build/Targets/Package/Services/PackageFamilyPacker.cs) and [HarvestReadinessValidator](../../build/_build/Validation/Packaging/HarvestReadinessValidator.cs) |
| G53 | Staged-replace keeps old valid receipts/licenses until replacements are complete; malformed receipts and per-library failures are fatal | [ConsolidateHarvestTask](../../build/_build/Targets/ConsolidateHarvest/ConsolidateHarvestTask.cs), [StagedArtifactSwapper](../../build/_build/Targets/ConsolidateHarvest/Services/StagedArtifactSwapper.cs), [HarvestTask](../../build/_build/Targets/Harvest/HarvestTask.cs) |
| G55 | Root native metadata parses and matches family, upstream version, triplets and build commit | [NativePackageMetadataValidator](../../build/_build/Validation/Packaging/NativePackageMetadataValidator.cs) |
| G56 | Satellite dependency has the explicit next-upstream-major exclusive upper bound | [SatelliteUpperBoundValidator](../../build/_build/Validation/Packaging/SatelliteUpperBoundValidator.cs) |
| G57 | README marker block matches manifest-driven generation | [ReadmeMappingTableValidator](../../build/_build/Validation/Packaging/ReadmeMappingTableValidator.cs) |

Native payload-only projects do not generate symbol packages; G25 covers the managed member.
The native metadata file preserves exact upstream patch and port-version separately from D-3seg.
Its schema is owned by [NativePackageMetadataDocument](../../build/_build/Data/NativePackageMetadata/NativePackageMetadataDocument.cs).

`AllowEmptyNativePayload=true` is the explicit, loudly reported opt-in for deliberate empty native
shape checks. It is not a production pack path. Normal packs pass `NativePayloadSource` from harvest.
Do not add another bypass to make an invalid payload pass.

## Operational principles

Preflight runs before native matrix work. PackageTask packs a family pair at one resolved version;
post-pack validation independently compares the two emitted versions. The
[release workflow](../../.github/workflows/release.yml) owns stage dependencies and orchestration.
Resolved version mappings are produced by
[ResolveVersionsFromManifest](../../build/_build/Targets/ResolveVersionsFromManifest/) or
[ResolveVersionsFromExplicit](../../build/_build/Targets/ResolveVersionsFromExplicit/).
Stage targets consume them through `--versions-file`, not a tag-trigger assumption.

### Release scope requirements

G58 requires dependencies in the same invocation; it does not query a feed to satisfy missing Core.
[PackageConsumerSmokePreconditionsValidator](../../build/_build/Validation/Packaging/PackageConsumerSmokePreconditionsValidator.cs)
requires all concrete manifest families or no family selection. A structurally valid partial pack
does not establish that partial-scope consumer smoke is supported.
Current release limitations and proposed relaxations have one operational owner: the
[roadmap](../ROADMAP.md). The registries above describe implemented checks only.

## Versioning and labels

Apply [ADR-0001](../adr/0001-d3seg-and-package-first.md), including its rule for a separate ADR when
a managed-only breaking change needs a transition plan. The brand's v1.0 launch is not a package
version reset. Package versions remain anchored to their upstream major/minor line.

| Distribution | Label | Feed |
|---|---|---|
| Local rehearsal | `-local.<timestamp>` | Local folder feed; never pushed (`PublishStaging` refuses it) |
| Internal CI wave | `-ci.<run-id>.<attempt>` | GitHub Packages |
| Public preview | `-preview.N`, incremented per wave | nuget.org |
| Release candidate | `-rc.N`, incremented per wave | nuget.org |
| Stable | No prerelease suffix | nuget.org |

Skip an alpha label: internal builds already serve the untrusted-development role. Public previews
are expected to work even though their API may still move. Never promote merely because time passed.

## Promotion gates

| Promotion | Required evidence |
|---|---|
| Internal to public preview | AST-generated coverage for the promoted family; learning-sdl2 or equivalent real downstream runs without breakage; every supported RID green; at least one green internal wave through native and consumer smoke |
| Preview to release candidate | Every in-scope family has a public preview; no consumer-reported preview defect remains open for two weeks; one upstream-bump regeneration validated |
| Release candidate to stable | Brand launch artifacts ready (blog, documentation site, samples); real downstream production-scenario validation; explicit maintainer commitment to maintenance |

Each public wave has at least two weeks of consumer dwell time. Wave counts do not replace evidence.
[Workflow](../engineering/workflow.md#agent-autonomy) reserves dispatch and publication for Deniz.

## Maintenance

Maintain the latest stable line, with no backport commitment or formal SLA. On an upstream bump,
regenerate and validate a release wave; aim for release within four weeks of the upstream stable
release. Critical interop and payload bugs take priority over cosmetic issues.
Native and consumer smoke catch loading and packaging regressions. Semantic API drift also needs
occasional real play-test passes at the maintainer's discretion.

When adding a guardrail, identify the failure mode, choose the earliest boundary with its inputs,
implement the check and its refusal test, then add its real owner to this registry. Planned checks
and unresolved release questions belong in the operational-state document until implemented.
