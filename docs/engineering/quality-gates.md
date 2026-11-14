# Quality Gates

Date: 2026-11-14

## Commands

[tools.cs](../../tools.cs) owns local orchestration. Use its Cake forwarder for ordinary target calls.
Direct Cake calls are for CI debugging and target discovery only. Native setup and validation require
a provisioned checkout under the [workflow rules](workflow.md#isolated-checkouts).

```pwsh
dotnet run --file tools.cs -- build --target Info
dotnet run --file tools.cs -- build --tree
dotnet run --file tools.cs -- setup
dotnet run --file tools.cs -- ci-sim
dotnet run --file tools.cs -- generate-bindings --check
dotnet run --file tools.cs -- build-binding-generator-image

# CI debugging and target discovery only:
dotnet run --project build/_build -- --tree
dotnet run --project build/_build -- --target Info

# Build-host regression suite:
dotnet test --project build/_build.Tests/Build.Tests.csproj -c Release

# Managed-only build, without the native pipeline:
dotnet build src/SDL2.Core/SDL2.Core.csproj
```

`setup` selects its feed with `--source=local|remote-github`.
`build-binding-generator-image` builds the Linux x64 ABI-test image only.
`generate-bindings` plans by default; [maintenance](binding-generator-maintenance.md) owns its
generation, stamp-check and evidence procedures.
Tests use TUnit on Microsoft.Testing.Platform; [global.json](../../global.json) selects the MTP runner.
Use `--treenode-filter`, not VSTest `--filter`, for a focused run:

```pwsh
dotnet test --project build/_build.Tests/Build.Tests.csproj -c Release --treenode-filter "/*/Build.Tests.Integration.RepositoryGates/*/*"
```

Replace the namespace, class or method segments to focus further. Run the full suite before handover.

## Completion gate

After the last edit, run the checks for the changed behaviour and inspect their actual output.
Record commands, exit codes and test totals. A successful build is not runtime evidence.
Documentation-only changes run the repository gates. Build-host changes run the full build-host suite.
Native, packaging and interop changes also require the affected RID/TFM runtime checks, across Windows,
Linux and macOS where their contracts apply. Read the supported matrices from the [manifest](../../build/manifest.json)
and [build properties](../../Directory.Build.props), not a copied list.
Confirm CI is green on all required platforms before claiming integration readiness. If a platform
cannot be checked locally, state the missing evidence; never imply that an unrun check passed.

## Slopwatch

Run after authored source, project or test changes. It rejects disabled tests, broad suppressions,
empty catches, arbitrary test delays and central-package-management bypasses.

```pwsh
dotnet tool restore
dotnet tool run slopwatch analyze --fail-on warning --exclude "artifacts/**,external/**,vcpkg_installed/**,spikes/binding-generators/references/**,.nuget/**,**/bin/**,**/obj/**"
```

The exclusions mirror [.slopwatch/slopwatch.json](../../.slopwatch/slopwatch.json).
They cover generated payloads, upstream submodules, installed natives, reference clones,
the CI package cache and build output. Fix new findings rather than hiding them in the baseline.
After deleting or moving code, rebuild the baseline to remove stale paths, using the same exclusions:

```pwsh
dotnet tool run slopwatch init -f --exclude "artifacts/**,external/**,vcpkg_installed/**,spikes/binding-generators/references/**,.nuget/**,**/bin/**,**/obj/**"
```

Review the baseline diff: it must not conceal a new finding.

## CI

[ci.yml](../../.github/workflows/ci.yml) runs light managed checks on pushes to `master` and every pull request, including
Markdown-only changes. It calls the reusable [build-host workflow](../../.github/workflows/build-cake-host.yml)
and [spike checks](../../.github/workflows/spike-checks.yml); workflow lint runs alongside them.
The Lint job format-checks whitespace and style and builds feed-independent projects with analyzers, runs analyzer tests, and builds `tools.cs`.
The x86 job builds every library's `net472` target on Windows with `-p:Platform=x86`, the global property the
release consumer smoke passes for .NET Framework, so a project reference that inherits the platform fails in the pull request.
The build-host workflow's CI OS legs compile and test without analyzers; Lint runs that wall once, while release retains analysis by default.
Slopwatch uses the root tool manifest: `dotnet tool restore`, then `dotnet tool run slopwatch analyze` with the exclusions above.
[release.yml](../../.github/workflows/release.yml) owns the heavy native and package matrix. It runs
by manual dispatch or the explicitly enabled schedule and reuses the same build-host workflow.
It checks committed generation stamps before the matrix, requires the recorded headers on win-x64,
and repeats freshness validation at Package; [generation canon](../architecture/binding-generation.md#generation-stamp-contract)
owns the check contract.
Release dispatch, publishing and merge remain Deniz's actions under [workflow](workflow.md#agent-autonomy).
