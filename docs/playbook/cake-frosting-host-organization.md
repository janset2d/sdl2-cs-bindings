# Playbook: Structuring the Cake Frosting Host

> A repo-focused guide for organizing `build/_build/` as it grows.
> This is the second trimmed extraction from the Cake Frosting deep reference, focused on host structure rather than general build principles.

## 1. One Build Context Type Per Run

Cake Frosting is built around a single registered context type for a build run.
Do not try to model the host around switching between multiple context classes during one invocation.

Practical rule:

- keep one `BuildContext`
- keep that context small enough to understand
- move related settings into grouped objects instead of adding endless flat properties

## 2. Use Composition Before A Giant Context

When context state starts growing, group it into focused settings objects instead of expanding `BuildContext` indefinitely.

Good examples of grouped state:

- repository paths
- vcpkg-related options
- release/publish settings
- parsed CLI options that logically belong together

If a setting group starts containing behavior, it is probably a service instead of a settings object.

## 3. Prefer DI For Complex Configuration

For non-trivial build logic, use `ConfigureServices(...)` and inject typed dependencies into the host rather than parsing and re-parsing state in multiple places.

Use DI when you need:

- reusable configuration objects
- testable service boundaries
- shared parsing or resolution logic
- dependencies that should be mocked in unit tests

Keep `Program.cs` responsible for wiring, not for containing business logic.

## 4. Split By Responsibility, Not By Layer Fashion

The current repo structure is already close to the right shape:

- `Tasks/` for orchestration
- `Modules/` and service registrations for wiring
- `Tools/` for concrete helpers
- `Context/` and `Models/` for state and data contracts

When adding new code, prefer asking "what responsibility is this?" rather than forcing everything into an arbitrary architectural pattern.

Good split examples:

- a task decides *when* something runs
- a service decides *how* it works
- a model records *what* was decided

## 5. Extract Shared Task Libraries Only When Reuse Is Real

Frosting supports reusable task libraries via normal .NET packages and `AddAssembly(...)`, but this repo should not split build logic into shared packages prematurely.

A shared task library becomes reasonable only if:

- the same tasks are needed across multiple repos
- the behavior is stable enough to version
- reuse is worth the packaging and maintenance overhead

For this repo today, keep the build host local unless SDL2/SDL3 or Janset-wide reuse becomes concrete.

## 6. CI Modules Are Optional, Not Automatic

If richer CI integration is needed, register modules explicitly in `Program.cs`.
Do not assume the host gets enhanced build-system behavior for free.

Practical guidance:

- use CI modules only when their extra reporting or environment integration clearly helps
- keep the YAML readable even when Cake-side modules are added
- treat CI modules as infrastructure wiring, not as the place where build logic should live

## 7. Prefer Modern Tool Paths And Tooling Contracts

For modern .NET work, prefer `dotnet`-based aliases and workflows over older `nuget.exe`-centric patterns unless a tool truly requires otherwise.

When external tools matter:

- make the dependency explicit
- prefer repeatable installation or resolution strategy
- document whether the tool is expected from PATH, a tool manifest, or an explicit path

This matters more in CI than on a comfortable local machine.

## 8. Host Growth Checklist

Before adding a new property, class, or task to `build/_build/`, ask:

1. Is this build-run state, or is it behavior?
2. Does it belong in `BuildContext`, a settings object, or a service?
3. Will another task need the same logic soon?
4. Am I making a class broader when I should be extracting a focused collaborator instead?
5. Is this solving a current repo problem, or am I designing ahead of reality?

If the honest answer to the last question is "ahead of reality," stop and keep it simpler.

## Further Reading

- [cake-frosting-patterns.md](cake-frosting-patterns.md)
- [cake-build-architecture.md](../knowledge-base/cake-build-architecture.md)
- [design-decisions-and-tradeoffs.md](../knowledge-base/design-decisions-and-tradeoffs.md)
- [cake-frosting-build-expertise.md](../reference/cake-frosting-build-expertise.md)
