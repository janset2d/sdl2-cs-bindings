# Tier 3 — Deferred One-Liners

Items tracked for visibility but not actively planned. Each row stays one sentence; when a trigger fires, promote to a Tier 2 outline (or directly to Tier 1 if the work is small) and remove the row.

## Code & API polish

- **`Otool-Analyze` vcpkg-mode fallback** — `Targets/OtoolAnalyze/` — `--dll` empty fallback walks pre-hybrid-static triplets that no longer match packaging model. **Activates when:** `tier-2/diagnostic-target-ux` lands (bundle the deletion decision). [Cleanup-plan §2]
- **`BinaryClosureWalker` catch-all narrowing** — `Targets/Harvest/Services/BinaryClosureWalker.cs:6,139` — file-scope `CA1031` swallows programmer bugs. **Activates when:** `tier-2/extraction-harvest-services` BFS split lands and the narrowing has stable ground to stand on. [Cleanup-plan §1]
- **`ConsolidateHarvestTask` aggregate-exception trim** — `Targets/ConsolidateHarvest/ConsolidateHarvestTask.cs:78` — log-and-throw amplification (ADR-002 §11). **Activates when:** any reviewer/observer flags the noisy CI output. [Cleanup-plan §4]
- **`StagedArtifactSwapper.SwapAtomic` rename** — `Targets/ConsolidateHarvest/Services/StagedArtifactSwapper.cs:24,46` — name vs reality mismatch. **Activates when:** any consolidation-side slice touches this file. [Cleanup-plan §4]
- **`LicenseUnionWriter` `Convert.ToHexStringLower` modernization** — `Targets/ConsolidateHarvest/Services/LicenseUnionWriter.cs:201-216` — replace StringBuilder hex loop. **Activates when:** any license-consolidation slice touches this file. [Cleanup-plan §4]
- **`AuthEnvVarChain` shape decision** — `Targets/PublishStaging/PublishStagingTask.cs:36` — `string[]` vs `ImmutableArray<string>`. **Activates when:** PD-7 (`PublishPublic` real implementation) lands and the auth chain extracts to a shared resolver (see watch list below). [Cleanup-plan §4]
- **`BuildContext` property type drift audit** — `Host/BuildContext.cs:19-25` — `FilePath?`, `IReadOnlyList<string>`, `string?` mix on CLI-derived properties. **Activates when:** a slice adds a new CLI-derived property and the cohort inconsistency causes a concrete decision. [Cleanup-plan §3]
- **`ManifestConfig.ResolveRequested` extension dedup** — duplicated algorithm in `HarvestTask:181` + `NativeSmokeTask:182`. **Activates when:** a third consumer appears or one of the two tasks gets touched. [Cleanup-plan §5]
- **`SmokePackage` nested record promotion** — `Targets/PackageConsumerSmoke/PackageConsumerSmokeTask.cs:60` — promote to `Targets/PackageConsumerSmoke/Models/SmokePackage.cs`. **Activates when:** `tier-2/extraction-package-smoke` lands (trivial bundle). [Cleanup-plan §5]
- **`ShouldSkipTfm` tuple rewrite** — `Targets/PackageConsumerSmoke/PackageConsumerSmokeTask.cs:389` — convert `out string reason` → `(bool, string)`. **Activates when:** `tier-2/extraction-package-smoke` lands and `Net4xRuntimeSkipPolicy` collaborator emerges. [Cleanup-plan §5]
- **`DotNetSmokeRunner` collapse decision** — `Targets/PackageConsumerSmoke/Services/DotNetSmokeRunner.cs` — two entry points differ on env-vars AND flag-injection. **Activates when:** `tier-2/extraction-package-smoke` lands; decide collapse vs keep-explicit alongside the cohort. [Cleanup-plan §5]
- **`PackageFamilyVersionSet` API refinements** — `Data/Versions/PackageFamilyVersionSet.cs:42,88,127` — 3 sub-items (interface boundary, GetEnumerator allocation, GetHashCode double-lookup). **Activates when:** any foundation-type performance review pass OR any slice touches this file. [Cleanup-plan §4]
- **`tools.cs` `build` → `cake` rename** — `tools.cs:45` — passthrough subcommand misleading name; 5 callsite update. **Activates when:** any CLI surface review pass OR a new `tools.cs` subcommand lands. [Cleanup-plan §2]

## Watch list (conditional triggers)

These activate only when a specific external signal fires — until then, no plan, just visibility:

- **`IGitHubAuthTokenResolver` extraction** — `Targets/PublishStaging/PublishStagingTask.ResolveAuthToken:112`. **Activates when:** PD-7 (`PublishPublic` real implementation) needs the same env-var chain → extract to a shared resolver at that point. [Cleanup-plan §5 / watch]
- **`IGitHeadResolver` interface** — `Targets/Package/PackageTask.cs:33-57` `Func<ICakeContext, DirectoryPath, string>` hook. **Activates when:** a second consumer of git HEAD SHA resolution appears (Harvest-time or Publish-time stamping). Today's Func is fine. [Cleanup-plan §5 / watch]
- **`SystemFileFilter` extraction from `RuntimeProfile`** — `Host/Runtime/RuntimeProfile.cs`. **Activates when:** `RuntimeProfile` accumulates more non-profile logic and exceeds its single-responsibility scope. [Cleanup-plan §5 / watch]
- **`HarvestReporter` method-count ceiling** — currently 9 public methods; ADR-002 §15 risk register cap is 10. **Activates when:** the next public method is added (forces the IReporter base decision in `tier-2/reporter-cohort`). [Cleanup-plan §6 / watch]
- **C# opportunistic polish** — `ReadmeMappingTableBlock.BuildBlock` StringBuilder + LF/CRLF consistency, `SmokeScopeComparator.Descendants()` typed-query optimization, `MonoAvailabilityProbe.mono.exe` recognition on Windows. **Activates when:** in-flight slices touch the same files — fix opportunistically; no dedicated pass. [Cleanup-plan §5 / watch]
