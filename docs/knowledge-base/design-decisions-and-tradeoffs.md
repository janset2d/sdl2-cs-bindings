# Knowledge Base: Build-System Design Decisions and Tradeoffs

> Repo-specific architecture notes preserved from earlier build-system reviews.
> This document exists so important design critique does not disappear once the old migration notes are retired.

## Why This Exists

The build host is already functional enough that it is easy to forget which parts are solid design and which parts are tolerated debt.
This document records the tradeoffs that matter when touching `build/_build/`.

## Strengths Worth Preserving

- **DI-first composition**: platform-specific scanners and services are selected centrally instead of being hard-coded inside tasks.
- **Configuration-driven behavior**: `manifest.json`, `runtimes.json`, and `system_artefacts.json` keep packaging policy out of the task bodies.
- **Hybrid dependency discovery**: the system combines vcpkg package metadata with runtime binary scanning instead of trusting either source alone.
- **Platform-correct deployment**: Windows uses loose-file deployment while Linux/macOS preserve symlink structure via `tar.gz`.
- **Useful diagnostics**: per-RID status JSON, consolidation artifacts, and rich console output make harvest failures debuggable.

## Tradeoffs To Watch

### BinaryClosureWalker Is Doing Too Much

Current responsibilities effectively include:

- root package metadata lookup
- primary binary resolution
- package dependency walking
- binary dependency scanning
- package ownership inference
- binary/file classification

That breadth is workable, but it is also the easiest place to accidentally create a god object.

Preferred future split if refactoring becomes necessary:

- `IPrimaryBinaryResolver`
- `IPackageDependencyWalker`
- `IBinaryDependencyScanner`
- `IFileClassifier`

Current guidance: do not refactor this just for cleanliness before `PackageTask` and release-pipeline work land. The bigger delivery gaps are elsewhere.

### RuntimeProfile Mixes Identity And Behavior

`RuntimeProfile` currently answers both:

- what platform/triplet/RID is active
- whether a file should be treated as a system artifact

That is acceptable today because the system-file decision is runtime-specific, but it is also a warning sign. If more file classification logic starts accumulating there, split the concept into a pure platform-information service plus a dedicated system-file filter.

### Result Pattern Has Two Plausible Futures

The current manual `OneOf` wrapper approach works, but it is verbose and was already called out as maintenance debt.

There are two coherent futures:

1. **Simplify the pattern** if the number of result types stays modest.
2. **Revive generation** if the build host keeps expanding result wrappers and async composition helpers.

The bad middle state is to keep growing manual wrappers while also keeping half-adopted generator infrastructure around.

### Partial CI Plumbing Exists Ahead Of Integration

The repo already contains design intent that is not yet a working workflow capability:

- `PathService` harvest-staging helpers
- `--use-overrides` on the build host

These are useful signals for future design, but current docs should always label them as partial or planned, not active capabilities.

## macOS Status: Old Critique, New Reality

Earlier architecture review material flagged macOS support as a critical implementation gap. That is no longer accurate.

Current reality:

- `otool`-based dependency scanning exists
- macOS system-artifact filtering exists
- Unix archive deployment covers `.dylib` symlink preservation
- macOS harvest workflows exist for `osx-x64` and `osx-arm64`

The remaining concern is validation depth around `@rpath`, `@loader_path`, and unusual dylib layouts, not a missing implementation.

## Practical Guidance For Future Changes

- Prefer thin Frosting tasks that delegate to services.
- Prefer updating current docs when a formerly parked idea becomes real.
- Treat architecture-review recommendations as design pressure, not automatic refactor tickets.
- When choosing between simplification and abstraction, bias toward the smaller change until Phase 2 delivery gaps are closed.

## Related Docs

- [cake-build-architecture.md](cake-build-architecture.md)
- [harvesting-process.md](harvesting-process.md)
- [parking-lot.md](../parking-lot.md)
- [cake-frosting-patterns.md](../playbook/cake-frosting-patterns.md)

## Origin Note

This document consolidates earlier architecture-review and result-pattern review material that was retired during the 2026 documentation cleanup.
