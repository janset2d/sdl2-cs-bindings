# Smoke Tests

Smoke checks prove artifacts work, not merely that they compile.

| Harness | Boundary | Orchestration |
|---|---|---|
| [native-smoke](native-smoke/) | C++/CMake loading, initialization and native API behavior | Cake `NativeSmoke` |
| [package-smoke](package-smoke/) | PackageReference restore, native extraction/copy and managed P/Invoke | Cake `PackageConsumerSmoke` |

[Developing and local validation](../../docs/engineering/developing.md) owns package-first setup,
the host-RID ci-sim loop, manual diagnosis, TFM behavior and cross-OS evidence. Prepare its local
feed before an IDE-driven consumer restore. [Native harness details](native-smoke/README.md) cover
direct CMake debugging. [Quality gates](../../docs/engineering/quality-gates.md) owns completion checks.

These are integrity gates, not samples or build-host unit tests. Consumer validation uses packages,
not source-graph native injection, as decided in
[ADR-0001](../../docs/adr/0001-d3seg-and-package-first.md).
