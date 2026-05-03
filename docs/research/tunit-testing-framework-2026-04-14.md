# TUnit Testing Framework — Research & Adoption Plan

**Date:** 2026-04-14
**Status:** Approved for adoption — Cake build host test infrastructure
**Related:** [#85](https://github.com/janset2d/sdl2-cs-bindings/issues/85), [cake-strategy-implementation-brief-2026-04-14.md](cake-strategy-implementation-brief-2026-04-14.md)

## Why TUnit (Not xUnit/NUnit)

| Factor | TUnit | xUnit | NUnit |
| --- | --- | --- | --- |
| Test runner | Microsoft.Testing.Platform (modern) | VSTest (legacy) | VSTest (legacy) |
| AOT support | Native AOT compatible | No | No |
| Source generation | Compile-time test discovery | Runtime reflection | Runtime reflection |
| Parallelism | All tests parallel by default | Per-collection serial | Configurable |
| Assertions | Fluent + async-native (`await Assert.That(...)`) | `Assert.Equal()` (sync) | `Assert.That()` (sync) |
| DI support | Built-in (`ClassDataSource`, `IClassConstructor`, `DependencyInjectionDataSourceAttribute`) | Constructor injection only | Limited |
| Instance model | Fresh instance per test | Fresh instance per test | Shared by default |

TUnit aligns with the project's .NET 10.0 target and modern C# patterns. Microsoft.Testing.Platform is the recommended runner going forward.

## Package Version

**TUnit 1.33.0** (latest stable, 2026-04-12)

The `TUnit` meta-package pulls in:

- `TUnit.Core` — test attributes, hooks, data sources
- `TUnit.Assertions` — fluent assertion library
- `TUnit.Assertions.Extensions` — extended assertion methods
- `TUnit.Engine` — Microsoft.Testing.Platform integration

## Core Concepts

### Test Structure

```csharp
// No [TestClass] required — only [Test] on methods
public class RuntimeProfileTests
{
    [Test]
    public async Task IsSystemFile_Should_Return_True_When_Windows_System_Dll()
    {
        var profile = CreateWindowsProfile();
        await Assert.That(profile.IsSystemFile(new FilePath("kernel32.dll"))).IsTrue();
    }
}
```

**Key rules:**

- `[Test]` attribute on methods (no class attribute needed)
- If using `Assert.That(...)`, method MUST be `async Task` (assertions are awaitable)
- `async void` is forbidden (diagnostic `TUnit0031`)
- Each test gets a fresh class instance (no shared state leakage)

### Assertions — Always Await

```csharp
// Equality
await Assert.That(actual).IsEqualTo(expected);

// Boolean
await Assert.That(condition).IsTrue();
await Assert.That(condition).IsFalse();

// Null
await Assert.That(value).IsNotNull();
await Assert.That(value).IsNull();

// String
await Assert.That(text).Contains("expected");
await Assert.That(text).StartsWith("prefix");
await Assert.That(text).Matches(regex);

// Collection
await Assert.That(collection).Contains(item);
await Assert.That(collection).IsNotEmpty();
await Assert.That(collection).HasCount().EqualTo(5);

// Exception
await Assert.That(() => SomeMethod()).Throws<InvalidOperationException>();
await Assert.That(() => SomeMethod()).Throws<ArgumentException>()
    .WithMessage("expected message");

// Chaining with And/Or
await Assert.That(value).IsGreaterThan(0).And.IsLessThan(100);

// Multiple assertions (report all failures)
using (Assert.Multiple())
{
    await Assert.That(a).IsEqualTo(1);
    await Assert.That(b).IsEqualTo(2);
}

// Numeric tolerance
await Assert.That(3.14159).IsEqualTo(3.14).Within(0.01);

// Type checking
await Assert.That(obj).IsTypeOf<string>();
```

### Data-Driven Tests

**Arguments (compile-time constants):**

```csharp
[Test]
[Arguments("2.32.10", 2, 32, 10)]
[Arguments("1.0.4", 1, 0, 4)]
[Arguments("2.8.8-rc1", 2, 8, 8)]
public async Task ParseSemanticVersion_Should_Extract_Components(
    string input, int major, int minor, int patch)
{
    var (m, n, p) = ParseSemanticVersion(input);
    await Assert.That(m).IsEqualTo(major);
    await Assert.That(n).IsEqualTo(minor);
    await Assert.That(p).IsEqualTo(patch);
}
```

**MethodDataSource (complex objects):**

```csharp
[Test]
[MethodDataSource(nameof(GetSystemFileTestCases))]
public async Task IsSystemFile_Should_Match_Expected_Result(string fileName, bool expected)
{
    var result = _profile.IsSystemFile(new FilePath(fileName));
    await Assert.That(result).IsEqualTo(expected);
}

// Must be static, returns IEnumerable<Func<T>> or IEnumerable<Func<(T1, T2)>>
public static IEnumerable<Func<(string FileName, bool Expected)>> GetSystemFileTestCases()
{
    yield return () => ("kernel32.dll", true);
    yield return () => ("SDL2.dll", false);
    yield return () => ("libc.so.6", true);
}
```

**ClassDataSource (shared fixtures):**

```csharp
// SharedType.None = fresh per test (default)
// SharedType.PerClass = one per test class
// SharedType.PerAssembly = one per assembly
// SharedType.PerTestSession = one for entire run
// SharedType.Keyed = shared by key string

[ClassDataSource<ManifestFixture>(Shared = SharedType.PerTestSession)]
public class PreFlightCheckTests(ManifestFixture fixture)
{
    [Test]
    public async Task ParseSemanticVersion_Should_Handle_Standard_Version()
    {
        // fixture is shared across all tests in session
    }
}

public class ManifestFixture : IAsyncInitializer, IAsyncDisposable
{
    public ManifestConfig Config { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        var json = await File.ReadAllTextAsync("build/manifest.json");
        Config = JsonSerializer.Deserialize<ManifestConfig>(json)!;
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
```

### Lifecycle Hooks

```csharp
public class MyTests
{
    [Before(Test)]    // Instance method, runs before each test
    public async Task SetUp() { }

    [After(Test)]     // Instance method, runs after each test
    public async Task TearDown(TestContext context)
    {
        if (context.Execution.Result?.State == TestState.Failed)
        {
            // Cleanup on failure
        }
    }

    [Before(Class)]   // STATIC, runs once before first test in class
    public static async Task ClassSetUp() { }

    [After(Class)]    // STATIC, runs once after all tests in class
    public static async Task ClassTearDown() { }

    [Before(Assembly)] // STATIC, runs once before all tests in assembly
    public static async Task AssemblySetUp() { }
}
```

**Execution order:**

1. `[Before(Assembly)]`
2. `[Before(Class)]`
3. Constructor (fresh instance per test)
4. `IAsyncInitializer.InitializeAsync()`
5. `[BeforeEvery(Test)]`
6. `[Before(Test)]`
7. **Test body**
8. `[After(Test)]`
9. `[AfterEvery(Test)]`
10. `IAsyncDisposable.DisposeAsync()`
11. `[After(Class)]` (after last test in class)
12. `[After(Assembly)]` (after all tests)

### Parallelism Control

```csharp
// Default: all tests parallel

// Sequential for shared state
[Test]
[NotInParallel("DatabaseTests")]
public async Task MyTest() { }

// Group: internal parallel, external sequential
[ParallelGroup("ConfigParsing")]
public class ManifestParsingTests { }

// Limit concurrency
[ParallelLimiter<MaxTwoParallel>]
public class RateLimitedTests { }

public record MaxTwoParallel : IParallelLimit
{
    public int Limit => 2;
}

// Assembly-wide sequential (escape hatch)
[assembly: NotInParallel]
```

### Dependency Injection

```csharp
// Option 1: DependencyInjectionDataSourceAttribute (full DI container)
public class MicrosoftDIAttribute : DependencyInjectionDataSourceAttribute<IServiceScope>
{
    private static readonly IServiceProvider Provider = new ServiceCollection()
        .AddSingleton<IRuntimeProfile, RuntimeProfile>()
        .AddTransient<IPathMatcher, WildcardPathMatcher>()
        .BuildServiceProvider();

    public override IServiceScope CreateScope(DataGeneratorMetadata metadata)
        => Provider.CreateScope();

    public override object? Create(IServiceScope scope, Type type)
        => scope.ServiceProvider.GetService(type);
}

[MicrosoftDI]
public class IntegrationTests(IRuntimeProfile profile, IPathMatcher matcher)
{
    [Test]
    public async Task MyTest() { }
}

// Option 2: IClassConstructor (simple, manual)
public class CustomConstructor : IClassConstructor
{
    public Task<object> Create(Type type, ClassConstructorMetadata metadata)
        => Task.FromResult(Activator.CreateInstance(type)!);
}

[ClassConstructor<CustomConstructor>]
public class SimpleTests { }
```

## Test Naming Convention (Project Standard)

**Pattern:** `<MethodName>_Should_<Do/Have/Return/Throw/etc.>_<optional When/If/Given etc.>`

- Method name is PascalCase, no underscores
- Every other word segment separated by underscores
- `Should` is always present — it's the verb bridge

**Examples:**

```csharp
// Good
IsSystemFile_Should_Return_True_When_Windows_System_Dll()
ParseSemanticVersion_Should_Extract_Major_Minor_Patch()
ParseSemanticVersion_Should_Throw_ArgumentException_When_Invalid_Format()
GetHarvestStageDir_Should_Include_Library_And_Rid_In_Path()
Validate_Should_Reject_Transitive_Dep_Leak_In_Hybrid_Mode()
Validate_Should_Pass_When_Only_Core_And_System_Deps_Present()
BuildClosureAsync_Should_Return_Empty_Closure_When_No_Binaries_Found()
MatchesPattern_Should_Handle_Wildcard_Prefix_And_Suffix()
DeserializeManifest_Should_Parse_All_Library_Entries()

// Bad — missing Should
IsSystemFile_Returns_True_For_Kernel32()
// Bad — no underscores between words
IsSystemFile_Should_ReturnTrue_WhenWindowsSystemDll()
// Bad — method name has underscores
Is_System_File_Should_Return_True()
```

## Project Setup

### global.json Addition

```json
{
    "sdk": { ... },
    "test": {
        "runner": "Microsoft.Testing.Platform"
    }
}
```

### Directory.Packages.props Addition

```xml
<!-- test packages -->
<PackageVersion Include="TUnit" Version="1.33.0" />
<PackageVersion Include="NSubstitute" Version="5.3.0" />
```

### Test Project: build/_build.Tests/_build.Tests.csproj

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <OutputType>Exe</OutputType>
    <IsTestProject>true</IsTestProject>
    <NoWarn>$(NoWarn);CA1050;MA0047</NoWarn>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\_build\Build.csproj" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="TUnit" />
    <PackageReference Include="NSubstitute" />
  </ItemGroup>
</Project>
```

**Note:** TUnit requires `OutputType=Exe` because it uses Microsoft.Testing.Platform (console app entrypoint, not VSTest adapter).

### InternalsVisibleTo (Build.csproj addition)

```xml
<ItemGroup>
  <InternalsVisibleTo Include="Build.Tests" />
</ItemGroup>
```

## Project Test Structure

```
build/_build.Tests/
├── _build.Tests.csproj
├── Fixtures/
│   ├── ManifestFixture.cs           ← Parses real manifest.json, shared per session
│   ├── RuntimeProfileFixture.cs     ← Creates RuntimeProfile with test patterns
│   └── BinaryClosureBuilder.cs      ← Fluent builder for test closures
├── Unit/
│   ├── RuntimeProfile/
│   │   └── IsSystemFileTests.cs
│   ├── PathService/
│   │   └── PathConstructionTests.cs
│   ├── PreFlight/
│   │   └── SemanticVersionParsingTests.cs
│   ├── BinaryClosureWalker/
│   │   └── PatternMatchingTests.cs
│   ├── Config/
│   │   └── ManifestDeserializationTests.cs
│   └── Strategy/                    ← Phase 2: TDD for new code
│       ├── HybridStaticValidatorTests.cs
│       └── StrategyResolutionTests.cs
└── Integration/
    └── Pipeline/
        └── HarvestPipelineTests.cs  ← Phase 3: post-extraction
```

## Reusability for Future Test Projects

This testing infrastructure is designed to be reused across the repo:

| Future project | Test type | Shared infra |
| --- | --- | --- |
| Cake build host (`build/_build.Tests/`) | Unit + integration | Fixtures, conventions, DI pattern |
| Native smoke test validation | Integration | ManifestFixture, naming convention |
| Package consumer tests | End-to-end | ManifestFixture, assertion patterns |
| Binding tests (Phase 3+) | Unit | Naming convention, TUnit setup |

The `ManifestFixture` (session-scoped, reads real config) and `RuntimeProfileFixture` (creates profiles for any platform) are designed as shared test infrastructure. As new test projects appear, they reference the same fixtures.

## Key Differences from xUnit (Quick Reference)

| xUnit | TUnit |
| --- | --- |
| `[Fact]` | `[Test]` |
| `[Theory]` | `[Test]` (same attribute) |
| `[InlineData(...)]` | `[Arguments(...)]` |
| `[MemberData(nameof(...))]` | `[MethodDataSource(nameof(...))]` |
| `[ClassData(typeof(...))]` | `[MethodDataSource(nameof(ClassName.Method))]` |
| `IClassFixture<T>` | `[ClassDataSource<T>(Shared = SharedType.PerClass)]` |
| `ICollectionFixture<T>` | `[ClassDataSource<T>(Shared = SharedType.Keyed, Key = "...")]` |
| `Assert.Equal(expected, actual)` | `await Assert.That(actual).IsEqualTo(expected)` |
| `Assert.True(condition)` | `await Assert.That(condition).IsTrue()` |
| `Assert.Throws<T>(...)` | `await Assert.That(() => ...).Throws<T>()` |
| `Assert.Contains(item, col)` | `await Assert.That(col).Contains(item)` |
| Constructor for setup | `[Before(Test)]` method |
| `IDisposable` for teardown | `[After(Test)]` method |
| `IAsyncLifetime` | `IAsyncInitializer` + `IAsyncDisposable` |

## Coverage with TUnit + Microsoft.Testing.Platform

### Preferred command for this repository

Use test-application options (`--coverage`) with an explicit absolute results directory:

```powershell
dotnet test build/_build.Tests/Build.Tests.csproj -- \
    --results-directory "E:/repos/my-projects/janset2d/sdl2-cs-bindings/artifacts/test-results/build-tests" \
    --coverage \
    --coverage-output-format cobertura \
    --coverage-output "coverage.cobertura.xml"
```

Expected output:

- `artifacts/test-results/build-tests/coverage.cobertura.xml`
- test report/log files in the same directory

### Troubleshooting notes

- If `--results-directory` is relative, output path may resolve under the test project folder. Prefer absolute paths for deterministic CI/local behavior.
- Validate available extension options with:

```powershell
dotnet test build/_build.Tests/Build.Tests.csproj -- --help
```

- Keep the MTP/TUnit coverage path as the canonical workflow for this repository to avoid confusion across runners.

## References

- [TUnit Documentation](https://tunit.dev/docs/intro)
- [TUnit GitHub](https://github.com/thomhurst/TUnit)
- [xUnit Migration Guide](https://tunit.dev/docs/migration/xunit)
- [Microsoft.Testing.Platform](https://learn.microsoft.com/en-us/dotnet/core/testing/microsoft-testing-platform-intro)
