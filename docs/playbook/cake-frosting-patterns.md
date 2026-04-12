# Playbook: Cake Frosting Patterns For This Repo

> A trimmed, repo-relevant guide derived from the Cake Frosting deep reference.
> This is not a generic Cake manual; it captures the patterns most likely to matter when editing `build/_build/` or wiring CI around it.

## 1. Keep Workflows Thin, Keep Behavior In The Build Host

- Put cross-platform behavior, packaging rules, and dependency logic in the Cake host, not in GitHub Actions YAML.
- Workflows should mostly select runners, install prerequisites, pass arguments, and publish artifacts.
- If a rule would need to stay consistent across Windows, Linux, and macOS, it probably belongs in `build/_build/`.

## 2. Keep Tasks Thin

- A task should mainly orchestrate intent, validate inputs, and call services.
- Prefer `AsyncFrostingTask<TContext>` only when the task is genuinely async end-to-end.
- Express order with `[IsDependentOn]` instead of manually invoking task logic from another task.

## 3. Use Context For Inputs, Services For Behavior

Good uses of `BuildContext`:

- CLI arguments and derived settings
- repo paths and environment facts
- run-scoped state that truly belongs to a build invocation

Good uses of DI services:

- platform-specific behavior
- dependency analysis
- deployment planning
- filesystem or tool orchestration that needs testing or reuse

If something needs unit tests, platform branching, or multiple collaborators, it should usually be a service rather than more context state.

## 4. Prefer Cake Aliases And Typed Paths

- Prefer Cake aliases such as `DotNetBuild`, `DotNetTest`, `MSBuild`, and `StartProcess` over ad-hoc shell command construction.
- Use `DirectoryPath` and `FilePath` plus `Combine(...)` or `CombineWithFilePath(...)` for path composition.
- Avoid string concatenation and avoid using `+` for paths; it is easy to create subtly wrong paths when implicit conversions get involved.

## 5. Treat Tool Resolution As A Reproducibility Problem

- If a task depends on a specific tool version or installation layout, prefer an explicit tool path or explicit tool-selection policy.
- Do not assume local PATH resolution and CI PATH resolution are equivalent.
- Document any tool-location assumptions in the relevant playbook or knowledge-base doc when they become repo requirements.

## 6. Use Setup/Teardown Hooks Sparingly

- Global lifetime hooks are good for one-time build setup or cleanup.
- Task lifetime hooks are good for consistent cross-cutting behavior such as diagnostics.
- Avoid hiding essential control flow in setup or teardown hooks; the readable path should stay in the task graph.
- If task failure handling needs to continue past a non-critical error, prefer explicit design over magical behavior.

## 7. Design For Debuggability

- Preserve rich console output and structured status artifacts.
- Keep artifact names and output directories predictable.
- When a workflow is still manual or partial, say so in docs instead of implying full automation.
- Use Cake's task graph and explicit arguments to make runs reproducible locally.

## 8. Scale By Extracting Patterns, Not By Growing Giant Classes

- Prefer adding focused services over making orchestrator classes wider.
- Prefer a few explicit abstractions over abstracting every small branch.
- When in doubt, bias toward the simpler design until the repo actually needs the extra abstraction.

## Further Reading

- [cake-build-architecture.md](../knowledge-base/cake-build-architecture.md)
- [design-decisions-and-tradeoffs.md](../knowledge-base/design-decisions-and-tradeoffs.md)
- [cake-frosting-build-expertise.md](../reference/cake-frosting-build-expertise.md)
