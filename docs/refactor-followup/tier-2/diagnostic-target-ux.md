# Diagnostic Target UX: Dumpbin/Ldd Parity + Binary Input Collapse (Outline)

**Tier:** 2
**Status:** outline
**Cleanup-plan section:** [post-refactor-cleanup-plan.md §2](../../post-refactor-cleanup-plan.md#2-ux--cli-surface)

## Goal

Four diagnostic targets exist with wildly different elaboration levels: `Otool-Analyze` has a per-platform classifier, dependency table, and `system_exclusions` suggestion section; `Dumpbin-Dependents` (41 LOC) just dumps raw output via `context.Verbose`; `Ldd-Dependents` (36 LOC) iterates `deps` and logs `{key} => {value}`; `Inspect-HarvestedDependencies` manifest-resolves but has its own input shape. Two adjacent UX problems: the elaboration gap (Dumpbin/Ldd should match Otool's depth) and the input mismatch (`--dll` vs `--library` flags split arbitrarily across the four targets).

## Open questions

- **Operator-usage check first:** does anyone actually invoke these targets interactively? Or are they CI-debugging tools that nobody opens unless a harvest fails? If usage is near-zero, lower-priority. Either way, the elaboration is asymmetric and looks lazy from outside — feels worth fixing even if cold path.
- Should `Dumpbin/Ldd` reuse the existing scanner implementations from Harvest (`IRuntimeScanner` cohort)? They already classify system files via `IRuntimeProfile.IsSystemFile`. Reuse vs duplicate-per-target.
- Binary input collapse: how to deprecate `--dll` and `--library` cleanly? Option (a) — auto-detect via heuristic (path-like → file mode, otherwise → manifest name mode). Option (b) — single `--target` flag with explicit `--mode=path|name`. (a) is more ergonomic; (b) is more explicit.
- `Otool-Analyze` stale vcpkg-mode fallback (cleanup-plan §2, currently parking-lot escape) — bundle into this slice or land separately? Probably bundle: same target, related UX cleanup.

## Sketch of scope

- Bring `Dumpbin-Dependents` + `Ldd-Dependents` up to `Otool-Analyze` elaboration: per-platform classifier, dependency table, `system_exclusions` suggestion. Reuse existing scanners.
- Collapse `--dll` + `--library` to a single auto-detecting CLI option (option (a) or (b)).
- Update `launchSettings.json`, `tools.cs` passthrough examples, [`docs/playbook/`](../../playbook/) references.
- Decide `Otool-Analyze` vcpkg-mode fallback: delete or migrate to hybrid-static triplet awareness via `IRuntimeProfile.Triplet`. Bundle here.
- Deprecation cycle for old flags: 1-2 slices with `[Obsolete]`-style warnings, then removal.

## Promotion criteria

- Quick operator-usage spot check: `git log --all --oneline -- '*Dumpbin*' '*Ldd*'` to see how often these targets are invoked or modified. If actually-unused, defer to Tier 3.
- Decide binary input collapse shape via mini-brainstorm.
- Decide otool vcpkg-mode (delete vs migrate) — needs context on whether anyone uses it.

## References

- [post-refactor-cleanup-plan.md §2](../../post-refactor-cleanup-plan.md#2-ux--cli-surface)
- [`Targets/OtoolAnalyze/`](../../../build/_build/Targets/OtoolAnalyze/)
- [`Targets/DumpbinDependents/DumpbinDependentsTask.cs`](../../../build/_build/Targets/DumpbinDependents/DumpbinDependentsTask.cs)
- [`Targets/LddDependents/LddDependentsTask.cs`](../../../build/_build/Targets/LddDependents/LddDependentsTask.cs)
- [`Targets/InspectHarvestedDependencies/InspectHarvestedDependenciesTask.cs`](../../../build/_build/Targets/InspectHarvestedDependencies/InspectHarvestedDependenciesTask.cs)
