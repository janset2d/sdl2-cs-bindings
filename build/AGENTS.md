# Build host — scoped agent rules

Rules for [`_build/`](_build/) (Cake Frosting host), [`_build.Tests/`](_build.Tests/) and [`manifest.json`](manifest.json).
They relay [`build-host.md`](../docs/architecture/build-host.md) and [testing guidelines](../docs/knowledge-base/testing-guidelines.md), which own
them; read those before a structural change.

- A Cake target lives in [`Targets/`](_build/Targets/) under its Cake target name; the task class owns orchestration, input
  validation and `CakeException` translation, and larger behaviour moves into named collaborators.
- New code starts in its target folder or in [`Data/`](_build/Data/), [`Validation/`](_build/Validation/), [`Results/`](_build/Results/), [`Host/`](_build/Host/) or
  [`Tools/`](_build/Tools/). Code leaves a target only when a second real consumer exists in the same change.
- At build boundaries use Cake-native abstractions: aliases and addins first, then
  `Tool<TSettings>`, then a named adapter. Raw BCL file IO or raw process start needs a stated
  reason in review.
- Expected failures return `Result<T, TError>`; multi-check validation returns `ValidationReport`.
- [`tools.cs`](../tools.cs) stays standalone: it reads [`manifest.json`](manifest.json) itself and references nothing in
  [`_build/`](_build/).
- Tests use Cake `FakeFileSystem` and fixtures under [`Fixtures/Data/`](_build.Tests/Fixtures/Data/); the real filesystem appears
  only in [`Integration/`](_build.Tests/Integration/). Names follow `<Method>_Should_<Verb>_<When>`.
- Before reporting completion run
  `dotnet test --project build/_build.Tests/Build.Tests.csproj -c Release`.
