# Testing Guidelines — Build-Host Tests

Canonical reference for writing and maintaining tests in `build/_build.Tests/`. Extracted from ADR-002, the refactoring plan, the review checklist, and AGENTS.md so those documents stay lean and this one carries the operational detail.

## The rule in one sentence

Build-host tests use **Cake `FakeFileSystem`** and **V2 test infrastructure by default**. Test data lives in **embedded fixture files** under `Fixtures/Data/`. Touched tests migrate to V2 immediately.

## Filesystem rule

Cake `FakeFileSystem` is the standard. Do not use `System.IO` in unit or scenario tests.

**Allowed real filesystem only when:**

- The system under test bypasses Cake abstractions (e.g., NuGet.Protocol reads real disk).
- The test is explicitly an `Integration/` test and the real FS boundary is the point.
- `TempDirectory` fixture is available for integration tests that need real temp directories.

**Never:**

- `System.IO.Abstractions` (do not introduce).
- `AppContext.BaseDirectory` traversal to find repo files (brittle — use `FixtureLoader`).
- Real `File.ReadAllText` / `File.WriteAllText` from fixture helpers or seeders.

## Test data policy

Test data is either **embedded** or **centralized inline** — never raw-string-literal in a test method.

### Embedded fixtures (`Fixtures/Data/`)

Use for **critical, reusable, or large** test data. Follow the homeruntech pattern:

```text
Fixtures/Data/
  Manifest/
    manifest-win-x64.json
    manifest-linux-x64.json
    manifest-minimal.json
  Versions/
    versions-valid.json
    versions-empty.json
    versions-invalid-semver.json
    versions-single-family.json
```

- File names describe the scenario: `versions-valid.json`, `versions-invalid-semver.json`.
- Include both valid and invalid variants for every domain object.
- Load via `FixtureLoader.Load("Domain/file.json")` — uses `Assembly.GetManifestResourceStream`.
- `.csproj` glob catches all: `<EmbeddedResource Include="Fixtures\Data\**\*.json" />`.

### Centralized inline data

Use for **small, test-class-local** data that isn't worth a separate file. Centralize in a `static class`:

```csharp
internal static class VersionsData
{
    public const string Valid = """{"sdl2-core":"2.32.0","sdl2-image":"2.8.0"}""";
    public const string Empty = """{}""";
    public const string InvalidSemVer = """{"sdl2-core":"not-a-version"}""";
}
```

- The class lives near the tests that use it (same file or same namespace).
- Name pattern: `<Domain>Data`.
- Never scatter the same string literal across multiple test methods.

**Decision trigger:** If the same data appears in ≥3 test methods or ≥2 test files → embedded fixture. If it's a single-test detail → centralized inline. If it's version/manifest/harvest data (build-domain critical) → embedded fixture regardless of count.

## V2 vs V1 infrastructure

| | V2 (current) | V1 (frozen) |
|---|---|---|
| Filesystem | `FakeCakeWorldV2` | `FakeRepoBuilder` |
| Test host | `TargetTestHostV2<TTask>` | `TestHostFixture` |
| Log | `TestLogV2` | `FakeLog` (Cake) |
| Context bridge | None needed | `ToLegacyBuildContext` |

**Rules:**

- **Touching a test?** Migrate it to V2. Applies to new tests AND modifications of existing tests — broader than the "migrated target" rule in the plan.
- **V1 fixtures are frozen.** Do not add methods to `FakeRepoBuilder`, `TestHostFixture`, or `FakeCakeToolContextBuilder`. Do not extend `ToLegacyBuildContext`.
- **Migrated target?** Existing tests under `Unit/Features/<OldFeature>/` move to V2 in the same migration slice. New tests go under `Unit/Targets/<CakeTargetName>/` or `Scenarios/<CakeTargetName>/`.
- **Unmigrated target?** V1 fixtures remain available but degrade naturally as targets migrate.

## Test taxonomy

| Folder | When | Fake or real? |
|---|---|---|
| `Unit/` | Pure policies, validators, small algorithms | No FS dependency; just instantiate and assert |
| `Scenarios/` | Real task orchestration, in-process | Cake `FakeFileSystem`, fake env, fake log, fake tools |
| `Integration/` | Mission-critical external boundaries only | Real `dotnet`, real archives, real FS if Cake can't model it |
| `Characterization/` | Temporary safety net during migration | V1 infra; graduate to `Scenarios` or delete |
| `Fixtures/Data/` | Reusable fake data files | Embedded resources, loaded via `FixtureLoader` |

## TUnit rules

- New test class instance per test — no shared state between tests.
- Constructor for simple setup, `[Before(Test)]` / `[After(Test)]` for async lifecycle.
- `ClassDataSource<T>` only for expensive shared resources.
- Always `await` assertions.
- Naming: `<MethodName>_Should_<Verb>_<optional When/If/Given>`. Underscores between word segments, `Should` always present.

## Scenario test structure

```csharp
[Test]
public async Task RunAsync_Should_Write_Versions_When_Valid_Input()
{
    // 1. Build the fake world with fixture data
    var world = FakeCakeWorldV2.CreateWindows()
        .WithManifestObject(ManifestFixture.CreateTestManifestConfig())
        .WithTextFile("artifacts/resolve-versions/versions.json",
            VersionsData.Valid)  // ← centralized inline OR fixture file content
        .WithVersionsFile("artifacts/resolve-versions/versions.json")
        .WithSuffix("ci.12345");

    // 2. Run the task
    var result = await CreateHost(world).RunAsync();

    // 3. Assert on result, log, process invocations
    await Assert.That(result.Success).IsTrue();
    await Assert.That(result.Exception).IsNull();

    // 4. Assert on filesystem output
    var json = world.ReadAllText("artifacts/resolve-versions/versions.json");
    await Assert.That(json).Contains("\"sdl2-core\": \"2.32.0-ci.12345\"");
}

private static TargetTestHostV2<MyTask> CreateHost(FakeCakeWorldV2 world)
{
    return new TargetTestHostV2<MyTask>(world)
        .WithServices(services =>
        {
            services.AddRepositories();
        });
}
```

Scenario host rules:

- Process commands: `WithProcessResult(cmd, exitCode, stdOut)` or `WithDefaultProcessResult(...)` — **unconfigured commands fail fast**. The `cmd` key is matched against the executable filename including extension on Windows (e.g. `dumpbin.exe`, not `dumpbin`); Unix tools have no extension (`tar`, `ldd`, `otool`).
- Tool paths: `WithToolPath(path)` (legacy global default), `WithToolPath(toolName, path)` (per-tool — required when a scenario invokes multiple `Tool<TSettings>` wrappers and each must resolve to a distinct executable so process invocations get distinct filename keys), or `WithDefaultToolPath(path)`.
- Process side effects: `WithProcessSideEffect(cmd, world => ...)` — fires before the process result is returned and lets the test mutate the fake world, typically seeding files into the fake filesystem to simulate the side effects of a real tool. Required for happy-path scenarios that invoke tools that produce filesystem output (e.g. `tar` extraction populating a destination directory) — pre-seeding via `WithTextFile` alone gets wiped by `DeleteDirectory` calls inside the workflow.
- Assert on `world.ProcessInvocations` when process behavior matters.
- `TargetTestHostV2<TTask>` constraint is `IFrostingTask` — both sync and async supported.
- V2 tests instantiate task classes from the service provider without registering the task type as a service.
- `TargetTestHostV2<TTask>.RunAsync()` returns `TargetRunResultV2` with `.Success`, `.Exception`, `.Log`.

## Filesystem seeding in FakeCakeWorldV2

```csharp
// Single file from fixture content
var content = FixtureLoader.Load("Versions/versions-valid.json");
world.WithTextFile("artifacts/versions.json", content);

// Minimal inline (when really needed)
world.WithTextFile("artifacts/empty.json", "{}");

// Manifest from object (not JSON string)
world.WithManifestObject(ManifestFixture.CreateTestManifestConfig());
```

`WithTextFile` writes into the fake filesystem, not real disk. `ReadAllText` reads from the fake filesystem. Both operate against `_repoRoot`.

## Anti-patterns

### Real filesystem in tests

```csharp
// ❌ BAD — real file I/O in unit/scenario tests
var json = File.ReadAllText("/some/real/path.json");

// ❌ BAD — brittle AppContext traversal
var dir = Path.Combine(AppContext.BaseDirectory, "../../../../../");
var manifest = File.ReadAllText(Path.Combine(dir, "build/manifest.json"));

// ✅ GOOD — embedded fixture via FixtureLoader
var json = FixtureLoader.Load("Versions/versions-valid.json");
world.WithTextFile("artifacts/versions.json", json);

// ✅ GOOD — Cake FakeFileSystem
var path = world.RepoRoot.CombineWithFilePath("artifacts/versions.json");
var repo = new VersionFileRepository(world.CakeContext);
```

### Inline JSON in test methods

```csharp
// ❌ BAD — inline string literal, repeated across tests
var world = FakeCakeWorldV2.CreateWindows()
    .WithTextFile("versions.json", """
    {
      "sdl2-core": "2.32.0",
      "sdl2-image": "2.8.0"
    }
    """);

// ✅ GOOD — embedded fixture (critical/reusable)
var content = FixtureLoader.Load("Versions/versions-valid.json");
world.WithTextFile("artifacts/versions.json", content);

// ✅ OK — centralized static class (small, single-test-scope)
world.WithTextFile("versions.json", VersionsData.Valid);
```

### Extending frozen infrastructure

```csharp
// ❌ BAD — adding methods to V1 fixtures
public FakeRepoBuilder WithNewFeature(...) { ... }

// ❌ BAD — extending the shim
public static BuildContext ToLegacyBuildContext(this FakeCakeWorldV2 world, ...) { ... }

// ✅ GOOD — use V2 fixtures for new work
var world = FakeCakeWorldV2.CreateWindows().With...;
```

### TestBase

```csharp
// ❌ BAD — large abstract TestBase with shared state
public abstract class TestBase { protected FakeCakeWorldV2 World; ... }

// ✅ GOOD — composable builders, narrow base only if it earns a domain name
var world = FakeCakeWorldV2.CreateWindows().With...;
```

## Non-actions (do not reopen without explicit approval)

- Do not split `FakeRepoPlatformV2.Unix` into Linux and macOS.
- Do not add unnecessary test helpers (e.g., `HasMessageExact` on `TestLogV2`).
- Do not introduce `System.IO.Abstractions`.
- Do not introduce a giant abstract `TestBase`.
- Do not use `Spectre.Console.Testing.TestConsole` outside `FakeCakeWorldV2`.
- Do not add reflection-based shim usage trackers or architecture-police tests.
- V1 fixtures are frozen — do not extend them, do not add new V1 fixture features.

## References

- [ADR-002 §12 (Testing model)](../decisions/2026-05-05-target-centric-build-host.md) — taxonomy, FakeFileSystem rule, V2 migration
- [Refactoring plan §9 (Testing architecture)](target-centric-build-host-refactor-plan.md) — folders, boundaries, composable helpers
- [Review checklist §10 + §13](target-centric-build-host-review-checklist.md) — per-slice testing gate + V2 infra rules
- [Extraction guidelines](extraction-guidelines.md) — private method extraction and collaborator design (sister document)
- [AGENTS.md](../../AGENTS.md) — test naming convention, V2 default rule, slopwatch
- [Homeruntech reference](https://github.com/homeruntech/dotnet-backend-monorepo-tool/tree/master/tests/Homerun.Dotnet.MonoRepoTools.Tests) — embedded fixture pattern inspiration
