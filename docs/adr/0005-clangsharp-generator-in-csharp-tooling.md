# ClangSharp Generator in C# Tooling

Date: 2026-10-06

Use ClangSharp for binding generation in the C# build tooling and retire the Python orchestrator.
Remove the CppAst generator while retaining its toolchain-independent safeguards and repositories.
Commit generated sources with a reproducibility stamp: generation is an explicit tooling operation,
not a Roslyn source generator running in a consumer build.

## Considered options

The CppAst path gave more control over emission but required maintaining the generator implementation.
The ClangSharp path uses an established generator and Roslyn postprocessing for repository policy.
Evidence: internal research, 2026-05-21, "Binding Generator Spike Iteration 2 Comparison";
internal research, 2026-10-06, "CppAst salvage inventory" and "Python to C# port map".

## Consequences

This supersedes [the original toolchain decision](0004-binding-autogen-toolchain.md).
Read the generator version from its [tool manifest](../../spikes/binding-generators/.config/dotnet-tools.json),
not from this record. The output-and-stamp contract permits review and reproducibility checks without
regeneration during release. Tested salvage may remain dormant at its existing paths until production
integration supplies its consumer; do not add a generation target merely to activate it.
