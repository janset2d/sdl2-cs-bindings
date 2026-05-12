# Cancellation Token Plumbing Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:subagent-driven-development` (recommended) or `superpowers:executing-plans` to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Tier:** 1
**Status:** done
**Depends on:** none
**Slice size:** small (1–2 commits, single PR)
**Cleanup-plan section:** [post-refactor-cleanup-plan.md §1 Correctness Gaps](../post-refactor-cleanup-plan.md#1-correctness-gaps)

**Goal:** Wire process-level cancellation signals (Ctrl+C, SIGTERM) through DI to a new `BuildContext.CancellationToken` property, and forward it from 6 async callsites in 3 tasks that currently pass `CancellationToken.None` or `default`. Eliminates a real correctness gap on freshly-landed S15 code: pre-S15 the surrounding pipelines threaded `CancellationToken`; the re-inline path dropped it.

**Architecture:** `Program.cs.RunCakeHostAsync` creates a `CancellationTokenSource`, subscribes to `Console.CancelKeyPress` (Ctrl+C, all platforms) and `PosixSignalRegistration` for `SIGTERM` (Unix; Windows ignores). The CTS Token is registered as a DI singleton. `BuildContext` accepts a `CancellationToken` ctor parameter and exposes it as a typed property (parallel to `Paths`, `Runtime`, `BuildConfiguration`). Tasks read `var ct = context.CancellationToken;` at the top of `RunAsync` and forward to async collaborators. No interface signatures change — all target collaborators already accept `CancellationToken ct = default` (verified during planning).

**Tech Stack:** .NET 10 / C# 14, Cake Frosting 6.1.0, Microsoft.Extensions.DependencyInjection, TUnit on Microsoft.Testing.Platform, NSubstitute, `FakeCakeWorld` + `TargetTestHost<TTask>` test harness per [`knowledge-base/testing-guidelines.md`](../knowledge-base/testing-guidelines.md).

---

## File Structure

**Files modified (no new files):**

| Path | Responsibility |
| --- | --- |
| `build/_build/Program.cs` | Add CTS, wire `Console.CancelKeyPress` + `PosixSignalRegistration`, register `CancellationToken` in DI |
| `build/_build/Host/BuildContext.cs` | Add `CancellationToken` ctor param + read-only property |
| `build/_build.Tests/Fixtures/FakeCakeWorld.cs` | Add `WithCancellationToken` fluent seeder; update `BuildContext` construction to pass it |
| `build/_build/Targets/PublishStaging/PublishStagingTask.cs` | Forward `context.CancellationToken` to 2 `PushAsync` callsites |
| `build/_build/Targets/PackageConsumerSmoke/PackageConsumerSmokeTask.cs` | Forward `context.CancellationToken` to `EnsureSmokeCsprojsMatchManifestScopeAsync` + `DotNetRuntimeEnvironment.ResolveAsync` |
| `build/_build/Targets/Package/PackageTask.cs` | Forward `context.CancellationToken` to `UpdateAsync` + `PackAsync` |
| `build/_build.Tests/Scenarios/PublishStaging/PublishStagingTaskScenarioTests.cs` | Tighten `Arg.Any<CancellationToken>()` → `Arg.Is<CancellationToken>` against the injected test token |
| `build/_build.Tests/Scenarios/PackageConsumerSmoke/PackageConsumerSmokeTaskScenarioTests.cs` | Same tightening for consumer-smoke collaborators |
| `build/_build.Tests/Scenarios/Package/PackageTaskScenarioTests.cs` | Same tightening for `PackAsync` |

**Out of scope:**

- Narrowing `BinaryClosureWalker.BuildClosureAsync` catch-all (`CA1031`) to operational exception types — separate Tier 3 item (tracked at [`tier-3-deferred.md`](tier-3-deferred.md)).
- Killing native child processes mid-flight when cancellation fires (the `MsvcDevEnvironment.cmd.exe` orphan-process concern from cleanup-plan §4) — separate item.
- Adding `CancellationToken` parameters to sync methods like `IProjectMetadataReader.Read` — its signature is sync; making it async is a separate concern.
- Adding force-quit-on-second-Ctrl+C semantics. First-Ctrl+C cooperative cancellation is enough for this slice; a second Ctrl+C falls back to default `Environment.Exit` behavior.

---

## Pre-flight context for the implementer

If you're new to this codebase, read these before touching code:

- [`AGENTS.md` §Build-Host Reference Pattern](../../AGENTS.md) — target-centric architecture, `Host/`, `Data/`, `Validation/`, `Tools/` layout.
- [`docs/decisions/2026-05-05-target-centric-build-host.md`](../decisions/2026-05-05-target-centric-build-host.md) — ADR-002, the architecture this build-host follows.
- [`knowledge-base/testing-guidelines.md`](../knowledge-base/testing-guidelines.md) — canonical test infrastructure (`FakeCakeWorld`, `TargetTestHost<TTask>`).

Background reading on the cancellation pattern (already done during planning):

- Cake Frosting 6.1.0 `AsyncFrostingTask<TContext>.RunAsync(TContext context)` does **not** accept a `CancellationToken` parameter (no ct overload in the BCL). Forward via `BuildContext`.
- [Meziantou — Handling `CancelKeyPress` using a `CancellationToken`](https://www.meziantou.net/handling-cancelkeypress-using-a-cancellationtoken.htm) — canonical .NET pattern.
- All target collaborators consumed by the 3 tasks below already declare `CancellationToken ct = default` — the fix is purely "stop passing `CancellationToken.None` / `default`, forward `context.CancellationToken` instead." Verified during planning via `grep`.

---

## Task 1: BuildContext.CancellationToken + Program.cs wiring + FakeCakeWorld seam

**Files:**
- Modify: `build/_build/Host/BuildContext.cs:27-44, 47-79`
- Modify: `build/_build/Program.cs:61-71`
- Modify: `build/_build.Tests/Fixtures/FakeCakeWorld.cs` (around line 44 for field, around line 399 for ctor)

This task lands the property + the DI plumbing + the test seam together. They cascade — adding the ctor parameter breaks `FakeCakeWorld`'s `BuildContext` construction at compile time, so the trio must move as one commit.

- [ ] **Step 1.1: Write the failing test asserting `BuildContext.CancellationToken` exposes the injected token**

Create test file `build/_build.Tests/Unit/Host/BuildContextCancellationTokenTests.cs`:

```csharp
using Build.Host;
using Build.Tests.Fixtures;

namespace Build.Tests.Unit.Host;

public sealed class BuildContextCancellationTokenTests
{
    [Test]
    public async Task CancellationToken_Should_Return_Injected_Token_When_Set_Via_FakeCakeWorld()
    {
        using var cts = new CancellationTokenSource();
        var world = FakeCakeWorld.CreateWindows().WithCancellationToken(cts.Token);

        var buildContext = world.BuildContext();

        await Assert.That(buildContext.CancellationToken).IsEqualTo(cts.Token);
    }

    [Test]
    public async Task CancellationToken_Should_Default_To_None_When_Not_Set()
    {
        var world = FakeCakeWorld.CreateWindows();

        var buildContext = world.BuildContext();

        await Assert.That(buildContext.CancellationToken).IsEqualTo(CancellationToken.None);
    }
}
```

- [ ] **Step 1.2: Run the test to verify it fails (compilation error)**

Run: `dotnet test build/_build.Tests/Build.Tests.csproj -c Release --framework net10.0 --filter "FullyQualifiedName~BuildContextCancellationTokenTests"`
Expected: FAIL with compile errors — `BuildContext.CancellationToken` does not exist and `FakeCakeWorld.WithCancellationToken` does not exist.

- [ ] **Step 1.3: Add `CancellationToken` to `BuildContext`**

Modify `build/_build/Host/BuildContext.cs`:

In the field block (after `_libraries`):
```csharp
    private readonly CancellationToken _cancellationToken;
```

Update the constructor signature (around line 27):
```csharp
    public BuildContext(
        ICakeContext context,
        IPathService pathService,
        IRuntimeProfile runtimeProfile,
        ParsedArguments parsedArguments,
        CancellationToken cancellationToken) : base(context)
    {
        Paths = pathService ?? throw new ArgumentNullException(nameof(pathService));
        Runtime = runtimeProfile ?? throw new ArgumentNullException(nameof(runtimeProfile));

        ArgumentNullException.ThrowIfNull(parsedArguments);

        _buildConfiguration = parsedArguments.Config;
        _versionsFilePath = !string.IsNullOrWhiteSpace(parsedArguments.VersionsFile)
            ? new FilePath(parsedArguments.VersionsFile!)
            : null;
        _resolveVersionsSuffix = parsedArguments.Suffix;
        _resolveVersionsScope = [.. parsedArguments.Scope];
        _explicitVersionEntries = [.. parsedArguments.ExplicitVersion];
        _explicitVersions = parsedArguments.ExplicitVersions;
        _dlls = [.. parsedArguments.Dll];
        _libraries = [.. parsedArguments.Library];
        _cancellationToken = cancellationToken;
    }
```

Add the public property (after `Libraries`):
```csharp
    /// <summary>Process-level cancellation token. Wired by Program.cs to Console.CancelKeyPress + SIGTERM.
    /// Tasks must read this at the top of RunAsync and forward to async collaborators rather than
    /// passing CancellationToken.None / default.</summary>
    public CancellationToken CancellationToken => _cancellationToken;
```

- [ ] **Step 1.4: Add `WithCancellationToken` to `FakeCakeWorld` and update its `BuildContext` construction**

Modify `build/_build.Tests/Fixtures/FakeCakeWorld.cs`:

In the field block (after `_libraries`, around line 46):
```csharp
    private CancellationToken _cancellationToken = CancellationToken.None;
```

Add the fluent seeder among the other `With*` methods (locate the section with `WithRid`, `WithVersionsFile`, etc.):
```csharp
    /// <summary>Seeds the CancellationToken that flows through BuildContext.CancellationToken
    /// to test ct propagation through tasks. Defaults to CancellationToken.None.</summary>
    public FakeCakeWorld WithCancellationToken(CancellationToken cancellationToken)
    {
        _cancellationToken = cancellationToken;
        return this;
    }
```

Update the `BuildContext()` factory (around line 399) to pass the new arg:
```csharp
        return new BuildContext(
            CakeContext,
            pathService,
            runtimeProfile,
            parsedArgs,
            _cancellationToken);
```

- [ ] **Step 1.5: Run the test to verify it passes**

Run: `dotnet test build/_build.Tests/Build.Tests.csproj -c Release --framework net10.0 --filter "FullyQualifiedName~BuildContextCancellationTokenTests"`
Expected: PASS (both tests).

- [ ] **Step 1.6: Wire `Console.CancelKeyPress` + `PosixSignalRegistration` + DI registration in Program.cs**

Modify `build/_build/Program.cs:61-71`. Replace `RunCakeHostAsync` body with:

```csharp
static async Task<int> RunCakeHostAsync(InvocationContext context, ParsedArguments parsedArgs)
{
    var repoRootPath = await DetermineRepoRootAsync(parsedArgs.RepoRoot);
    var initialCakeArgs = context.ParseResult.Tokens.Select(t => t.Value).ToArray();
    var effectiveCakeArgs = GetEffectiveCakeArguments(initialCakeArgs, repoRootPath, context);

    using var cts = new CancellationTokenSource();

    // Console.CancelKeyPress fires for Ctrl+C on all platforms; setting e.Cancel=true
    // prevents immediate process termination so collaborators get a chance to clean up.
    // A second Ctrl+C falls through to the default OS handler and force-quits.
    void OnCancelKeyPress(object? sender, ConsoleCancelEventArgs e)
    {
        if (cts.IsCancellationRequested)
        {
            return;
        }
        e.Cancel = true;
        cts.Cancel();
    }
    Console.CancelKeyPress += OnCancelKeyPress;

    // PosixSignalRegistration handles SIGTERM on Unix (kill <pid>, container stop, etc.).
    // No-op on Windows. SIGINT is already covered by Console.CancelKeyPress.
    using var sigTermRegistration = PosixSignalRegistration.Create(PosixSignal.SIGTERM, _ => cts.Cancel());

    try
    {
        return new CakeHost()
            .UseContext<BuildContext>()
            .ConfigureServices(services =>
            {
                ConfigureBuildServices(services, parsedArgs, repoRootPath);
                services.AddSingleton(cts.Token);
            })
            .Run(effectiveCakeArgs);
    }
    finally
    {
        Console.CancelKeyPress -= OnCancelKeyPress;
    }
}
```

Add `using System.Runtime.InteropServices;` at the top of the file if not already present.

- [ ] **Step 1.7: Run the full build-host test suite to verify nothing broke**

Run: `dotnet test build/_build.Tests/Build.Tests.csproj -c Release --framework net10.0`
Expected: PASS — all existing tests still green (BuildContext ctor change cascades through FakeCakeWorld, but the new `default` argument keeps behavior identical for tests that don't call `WithCancellationToken`).

- [ ] **Step 1.8: Commit**

```bash
git add build/_build/Host/BuildContext.cs build/_build/Program.cs build/_build.Tests/Fixtures/FakeCakeWorld.cs build/_build.Tests/Unit/Host/BuildContextCancellationTokenTests.cs
git commit -m "feat(build): expose CancellationToken via BuildContext

Wires Console.CancelKeyPress + SIGTERM (PosixSignalRegistration) in
Program.cs to a process-level CancellationTokenSource, registers the
Token as a DI singleton, and exposes it via BuildContext.CancellationToken.
Tasks can now read context.CancellationToken at the top of RunAsync and
forward to async collaborators instead of passing CancellationToken.None
or default. No behavioral change to tasks yet — forwarding lands in
follow-up commits per the cancellation plumbing plan.

Refs: docs/refactor-followup/01-cancellation-plumbing.md"
```

---

## Task 2: PublishStagingTask forwards context.CancellationToken to PushAsync (2 sites)

**Files:**
- Modify: `build/_build/Targets/PublishStaging/PublishStagingTask.cs:105-106`
- Modify: `build/_build.Tests/Scenarios/PublishStaging/PublishStagingTaskScenarioTests.cs:33-37`

- [ ] **Step 2.1: Tighten the existing scenario assertion to verify ct propagation**

Modify `build/_build.Tests/Scenarios/PublishStaging/PublishStagingTaskScenarioTests.cs`. Find the `RunAsync_Should_Push_Managed_And_Native_Pair_Per_Family_When_Happy_Path` test (around line 20) and update it:

```csharp
    [Test]
    public async Task RunAsync_Should_Push_Managed_And_Native_Pair_Per_Family_When_Happy_Path()
    {
        using var cts = new CancellationTokenSource();
        var feedClient = Substitute.For<INuGetFeedClient>();
        var world = NewWorld(feedClient).WithCancellationToken(cts.Token);
        // Multi-family fixture: sdl2-core@2.32.0 + sdl2-image@2.8.0.
        SeedFeedNupkgs(world, ("sdl2-core", "2.32.0"), ("sdl2-image", "2.8.0"));
        world.Environment.SetEnvironmentVariable("GH_TOKEN", "test-token");

        var result = await CreateHost(world, feedClient).RunAsync();

        await Assert.That(result.Exception).IsNull();
        await Assert.That(result.Success).IsTrue();
        // 2 families × (managed + native) = 4 pushes, all forwarding the BuildContext's ct.
        await feedClient.Received(4).PushAsync(
            Arg.Is<string>(url => url.Contains("nuget.pkg.github.com", StringComparison.Ordinal)),
            Arg.Is<string>(token => token == "test-token"),
            Arg.Any<FilePath>(),
            Arg.Is<CancellationToken>(ct => ct == cts.Token));
    }
```

- [ ] **Step 2.2: Run the test to verify it fails**

Run: `dotnet test build/_build.Tests/Build.Tests.csproj -c Release --framework net10.0 --filter "FullyQualifiedName~PublishStagingTaskScenarioTests.RunAsync_Should_Push_Managed_And_Native_Pair_Per_Family_When_Happy_Path"`
Expected: FAIL — task still passes `CancellationToken.None`, not `cts.Token`. NSubstitute reports "Expected 4 calls matching ...; actually received 0 matching calls (4 received with non-matching arguments)".

- [ ] **Step 2.3: Forward context.CancellationToken in PublishStagingTask**

Modify `build/_build/Targets/PublishStaging/PublishStagingTask.cs`. Inside `RunAsync` (around line 87 — at the top of the `foreach (var family in concreteFamilies)` block, or just before it), introduce a local:

```csharp
        var ct = context.CancellationToken;

        foreach (var family in concreteFamilies)
        {
            var version = familyVersions.RequireVersion(new PackageFamilyId(family.Name));
            EnsureNotLocalSuffix(family.Name, version);

            var managedPackageId = FamilyIdentifierConventions.ManagedPackageId(family.Name);
            var nativePackageId = FamilyIdentifierConventions.NativePackageId(family.Name);

            var managedNupkg = ResolveAndEnsureNupkg(_pathService.PackagesOutput, managedPackageId, version);
            var nativeNupkg = ResolveAndEnsureNupkg(_pathService.PackagesOutput, nativePackageId, version);

            _log.Information(
                "PublishStaging pushing '{0}' = {1} ({2} + {3}).",
                family.Name,
                version.ToNormalizedString(),
                managedPackageId,
                nativePackageId);

            await _feedClient.PushAsync(GitHubPackagesFeedUrl, authToken, managedNupkg, ct);
            await _feedClient.PushAsync(GitHubPackagesFeedUrl, authToken, nativeNupkg, ct);
        }
```

- [ ] **Step 2.4: Run the test to verify it passes**

Run: `dotnet test build/_build.Tests/Build.Tests.csproj -c Release --framework net10.0 --filter "FullyQualifiedName~PublishStagingTaskScenarioTests"`
Expected: PASS (all PublishStaging scenario tests).

- [ ] **Step 2.5: Commit**

```bash
git add build/_build/Targets/PublishStaging/PublishStagingTask.cs build/_build.Tests/Scenarios/PublishStaging/PublishStagingTaskScenarioTests.cs
git commit -m "fix(publish-staging): forward BuildContext.CancellationToken to PushAsync

PublishStagingTask.RunAsync used CancellationToken.None at both PushAsync
sites. Wire context.CancellationToken through so process-level cancellation
(Ctrl+C / SIGTERM) reaches the NuGet push. Tighten happy-path scenario
to assert ct propagation via Arg.Is<CancellationToken>.

Refs: docs/refactor-followup/01-cancellation-plumbing.md"
```

---

## Task 3: PackageConsumerSmokeTask forwards context.CancellationToken (2 sites)

**Files:**
- Modify: `build/_build/Targets/PackageConsumerSmoke/PackageConsumerSmokeTask.cs:110,149`
- Modify: `build/_build.Tests/Scenarios/PackageConsumerSmoke/PackageConsumerSmokeTaskScenarioTests.cs` (verify file path; tighten assertions on `IDotNetRuntimeEnvironment.ResolveAsync`)

- [ ] **Step 3.1: Confirm the scenario test file location**

Run: `dotnet test --list-tests build/_build.Tests/Build.Tests.csproj 2>&1 | grep -i "PackageConsumerSmoke" | head -10`

Or use file search: `find build/_build.Tests -iname "*PackageConsumerSmoke*Test*.cs"`

Note the exact filename for steps below. If the file does not exist yet for scenario coverage, you may need to skip the assertion-tightening for `ResolveAsync` and rely on the existing test scaffolding to confirm no regression — but DotNetRuntimeEnvironment is mocked in fixtures already, so a scenario test should exist.

- [ ] **Step 3.2: Tighten the scenario assertion on `IDotNetRuntimeEnvironment.ResolveAsync`**

In the happy-path test (locate it, name will be something like `RunAsync_Should_Run_Smoke_For_Each_TFM_When_Happy_Path`), update to inject a known `CancellationToken` via `WithCancellationToken` and assert `ResolveAsync` receives it:

```csharp
    [Test]
    public async Task RunAsync_Should_Forward_CancellationToken_To_DotNetRuntimeEnvironment()
    {
        using var cts = new CancellationTokenSource();
        var runtimeEnv = Substitute.For<IDotNetRuntimeEnvironment>();
        runtimeEnv.ResolveAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyDictionary<string, string>>(new Dictionary<string, string>()));

        var world = NewSmokeWorld(runtimeEnv).WithCancellationToken(cts.Token);
        // ... seed nupkgs and version mapping as the existing happy-path test does ...

        var result = await CreateHost(world, runtimeEnv).RunAsync();

        await Assert.That(result.Success).IsTrue();
        await runtimeEnv.Received().ResolveAsync(
            Arg.Any<string>(),
            Arg.Any<IReadOnlyList<string>>(),
            Arg.Is<CancellationToken>(ct => ct == cts.Token));
    }
```

If the existing happy-path test already substitutes `IDotNetRuntimeEnvironment`, tighten that test's assertion instead of adding a new one.

- [ ] **Step 3.3: Run the test to verify it fails**

Run: `dotnet test build/_build.Tests/Build.Tests.csproj -c Release --framework net10.0 --filter "FullyQualifiedName~PackageConsumerSmokeTaskScenarioTests"`
Expected: FAIL on the new/tightened assertion — the task currently passes no `ct` to `ResolveAsync`, so the `=default` overload runs with `CancellationToken.None`, not `cts.Token`.

- [ ] **Step 3.4: Forward context.CancellationToken in PackageConsumerSmokeTask**

Modify `build/_build/Targets/PackageConsumerSmoke/PackageConsumerSmokeTask.cs`. After validating preconditions and resolving smoke packages (around line 107), introduce a local:

```csharp
        var ct = context.CancellationToken;

        var smokePackages = ResolveSmokePackages(manifest);
        EnsureSelectionSupportsCurrentSmokeScope(smokePackages, familyVersions);
        await EnsureSmokeCsprojsMatchManifestScopeAsync(context, smokePackages, ct);
        EnsurePackageArtifactsExist(context, smokePackages, familyVersions, feedPath);
```

Further down (around line 149), update `ResolveAsync`:

```csharp
        var runtimeEnvironmentDelta = await _dotNetRuntimeEnvironment.ResolveAsync(
            _runtimeProfile.Rid,
            projectMetadata.TargetFrameworks,
            ct);
```

- [ ] **Step 3.5: Run the test to verify it passes**

Run: `dotnet test build/_build.Tests/Build.Tests.csproj -c Release --framework net10.0 --filter "FullyQualifiedName~PackageConsumerSmokeTaskScenarioTests"`
Expected: PASS.

- [ ] **Step 3.6: Commit**

```bash
git add build/_build/Targets/PackageConsumerSmoke/PackageConsumerSmokeTask.cs build/_build.Tests/Scenarios/PackageConsumerSmoke/
git commit -m "fix(package-consumer-smoke): forward BuildContext.CancellationToken

EnsureSmokeCsprojsMatchManifestScopeAsync received 'default' and
DotNetRuntimeEnvironment.ResolveAsync received no ct overload arg.
Wire context.CancellationToken so Ctrl+C / SIGTERM reaches the async
preflight + runtime-env probe.

Refs: docs/refactor-followup/01-cancellation-plumbing.md"
```

---

## Task 4: PackageTask forwards context.CancellationToken (2 sites)

**Files:**
- Modify: `build/_build/Targets/Package/PackageTask.cs:98,105`
- Modify: `build/_build.Tests/Scenarios/Package/PackageTaskScenarioTests.cs` (locate happy-path test, tighten `UpdateAsync` + `PackAsync` assertions)

- [ ] **Step 4.1: Confirm scenario test file path**

Run: `find build/_build.Tests -iname "*PackageTask*Scenario*.cs"`

Note the exact filename. If a happy-path test substitutes `IReadmeMappingTableGenerator` and `PackageFamilyPacker` (probably as itself, since `PackageFamilyPacker` is concrete), the tightening targets `UpdateAsync` and `PackAsync` calls.

- [ ] **Step 4.2: Tighten happy-path assertion on `UpdateAsync`**

`PackageFamilyPacker` is `public sealed class` with no interface (verified during planning), so NSubstitute cannot substitute it. The `UpdateAsync` assertion on `IReadmeMappingTableGenerator` is enough to prove the forwarding seam — once `var ct = context.CancellationToken;` lands at the top of `RunAsync`, the local flows to both `UpdateAsync` and `PackAsync` by inspection. If you want indirect `PackAsync` coverage, inject `INativePackageMetadataGenerator` (which `PackageFamilyPacker` consumes) and assert it received the ct, but that's a deeper integration assertion and not required for this slice.

In the happy-path test (something like `RunAsync_Should_Pack_All_Selected_Families_When_Happy_Path`), inject a known ct via `WithCancellationToken` and add a new test or extend the existing one:

```csharp
    [Test]
    public async Task RunAsync_Should_Forward_CancellationToken_To_ReadmeGenerator()
    {
        using var cts = new CancellationTokenSource();
        var generator = Substitute.For<IReadmeMappingTableGenerator>();

        var world = NewWorld(generator).WithCancellationToken(cts.Token);
        // ... seed manifest + nupkgs + versions per existing happy-path setup ...

        var result = await CreateHost(world, generator).RunAsync();

        await Assert.That(result.Success).IsTrue();
        await generator.Received().UpdateAsync(
            Arg.Any<ManifestConfig>(),
            Arg.Is<CancellationToken>(ct => ct == cts.Token));
    }
```

- [ ] **Step 4.3: Run the test to verify it fails**

Run: `dotnet test build/_build.Tests/Build.Tests.csproj -c Release --framework net10.0 --filter "FullyQualifiedName~PackageTaskScenarioTests"`
Expected: FAIL on the new assertion — `UpdateAsync` receives `CancellationToken.None`, not `cts.Token`.

- [ ] **Step 4.4: Forward context.CancellationToken in PackageTask**

Modify `build/_build/Targets/Package/PackageTask.cs`. After version + manifest resolution (around line 95), introduce a local:

```csharp
        var ct = context.CancellationToken;

        // G57 generator: keep README mapping block aligned with manifest before pack validation.
        await _readmeMappingTableGenerator.UpdateAsync(manifest, ct);

        context.EnsureDirectoryExists(context.Paths.PackagesOutput);

        foreach (var family in families)
        {
            var version = versions.RequireVersion(new PackageFamilyId(family.Name)).ToNormalizedString();
            await _packer.PackAsync(manifest, family, version, headSha, context.BuildConfiguration, ct);
        }
```

- [ ] **Step 4.5: Run the test to verify it passes**

Run: `dotnet test build/_build.Tests/Build.Tests.csproj -c Release --framework net10.0 --filter "FullyQualifiedName~PackageTaskScenarioTests"`
Expected: PASS.

- [ ] **Step 4.6: Commit**

```bash
git add build/_build/Targets/Package/PackageTask.cs build/_build.Tests/Scenarios/Package/
git commit -m "fix(package): forward BuildContext.CancellationToken to generator and packer

PackageTask.RunAsync passed CancellationToken.None to
IReadmeMappingTableGenerator.UpdateAsync and PackageFamilyPacker.PackAsync.
Wire context.CancellationToken so Ctrl+C / SIGTERM reaches the readme
regeneration and the per-family pack flow.

Refs: docs/refactor-followup/01-cancellation-plumbing.md"
```

---

## Task 5: Final regression sweep

- [ ] **Step 5.1: Run the full build-host test suite**

Run: `dotnet test build/_build.Tests/Build.Tests.csproj -c Release --framework net10.0`
Expected: PASS — every test green.

- [ ] **Step 5.2: Grep for remaining `CancellationToken.None` / `, default)` in target tasks**

Run: `rg "CancellationToken\.None|, default\)" build/_build/Targets`

Expected: zero matches in the 3 task files modified. Any remaining match is either intentional (e.g., a method that genuinely needs an uncancellable token) or a missed callsite — investigate before closing the slice.

- [ ] **Step 5.3: Manual smoke verification of Ctrl+C cancellation (optional but recommended)**

In a terminal:
```pwsh
dotnet run --file tools.cs -- ci-sim
```

Press **Ctrl+C** during a long-running Cake stage (Harvest is a good candidate). Expected: process should respond cooperatively — the current stage logs a cancellation message via the standard `OperationCanceledException` propagation path; the build host exits with a non-zero code rather than hanging.

Note: this is best-effort. Some native child processes (`cmd.exe`, `cl.exe`, `msbuild.exe`) may not respond to the parent's cancellation — see the separate Tier 3 item on `MsvcDevEnvironment` orphan-process cleanup. The goal here is that the *managed* code paths handle cancellation, not that child processes are killed.

- [ ] **Step 5.4: Update plan status to `done` and add commit SHAs**

Modify `docs/refactor-followup/README.md` index table: change the status of `01-cancellation-plumbing` from `pending` to `done` and append a column with the slice's commit SHAs.

Modify the header of `docs/refactor-followup/01-cancellation-plumbing.md` to reflect `Status: done`.

Optional: tick off the cancellation token bullet at [`post-refactor-cleanup-plan.md §1`](../post-refactor-cleanup-plan.md#1-correctness-gaps).

- [ ] **Step 5.5: Final commit**

```bash
git add docs/refactor-followup/README.md docs/refactor-followup/01-cancellation-plumbing.md docs/post-refactor-cleanup-plan.md
git commit -m "docs(refactor-followup): mark cancellation plumbing done

Refs: docs/refactor-followup/01-cancellation-plumbing.md"
```

---

## Test Strategy Summary

| Layer | Test surface | What it proves |
| --- | --- | --- |
| Unit | `BuildContextCancellationTokenTests` | The property exposes the injected token; defaults to `CancellationToken.None` |
| Scenario | `PublishStagingTaskScenarioTests` happy path (tightened) | `INuGetFeedClient.PushAsync` receives the BuildContext's ct, not `CancellationToken.None` |
| Scenario | `PackageConsumerSmokeTaskScenarioTests` (new or tightened) | `IDotNetRuntimeEnvironment.ResolveAsync` receives the BuildContext's ct |
| Scenario | `PackageTaskScenarioTests` (tightened) | `IReadmeMappingTableGenerator.UpdateAsync` receives the BuildContext's ct |
| Manual | Ctrl+C during `ci-sim` | Process-level signal reaches cooperative cancellation in real tasks |

We do **not** unit-test the `Console.CancelKeyPress` + `PosixSignalRegistration` wiring directly — those touch process-global state and are brittle in CI. The manual smoke test plus the DI singleton registration test (Task 1) gives sufficient coverage that the seam is right.

---

## Risk

- **Behavior risk:** very low. All target collaborators already accept `CancellationToken ct = default`, so forwarding `CancellationToken.None` (the default) is byte-identical to current behavior when no cancellation fires. The only observable change is when Ctrl+C / SIGTERM fires — previously a no-op for these async paths, now they cancel cooperatively.
- **Surface risk:** medium. `BuildContext` ctor signature changes; `FakeCakeWorld` is the only test-side constructor; production-side construction goes through Cake DI which resolves via `Microsoft.Extensions.DependencyInjection`. Any third-party code that newed up `BuildContext` directly (unlikely) would break.
- **Test brittleness risk:** low. Tightening `Arg.Any<CancellationToken>()` → `Arg.Is<CancellationToken>(ct => ct == cts.Token)` is a behavior assertion; if it flakes, the flake is real (ct didn't propagate) and worth fixing.
- **Who-else-touches:** the 3 modified tasks are stable post-S15; the only in-flight concern would be if `02-ctor-policy` lands in parallel and reshapes the same ctors — coordinate the merge order if both slices run concurrently.

---

## References

- [ADR-002](../decisions/2026-05-05-target-centric-build-host.md) §3 (BuildContext shape) + §5 (target-owned orchestration, no pipeline wrappers)
- [ADR-003](../decisions/2026-05-12-build-host-data-layer.md) — Data layer doesn't directly touch cancellation but the contract-centric data flow informs why ct lives on `BuildContext` not in a separate `ICancellationContext`
- [post-refactor-cleanup-plan.md §1 Correctness Gaps](../post-refactor-cleanup-plan.md#1-correctness-gaps) — origin of this slice
- [knowledge-base/testing-guidelines.md](../knowledge-base/testing-guidelines.md) — `FakeCakeWorld` + `TargetTestHost<TTask>` patterns
- S15 P9 review reviewer C1+C2+C3 — original finding on dropped ct plumbing
- Cake Frosting 6.1 — `AsyncFrostingTask<TContext>.RunAsync(TContext context)` has no ct overload; verified via NuGet API browser
- [Meziantou — Handling CancelKeyPress using a CancellationToken](https://www.meziantou.net/handling-cancelkeypress-using-a-cancellationtoken.htm) — canonical .NET pattern reference
- Related commits (refactor-era context): `25bde52` (build refactor canonicalization), `78d5d44` (build-host config removal — context for why ct dropped from re-inline path), `1534ea9` (data repository introduction)
