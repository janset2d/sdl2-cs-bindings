# Quality Gates

Date: 2026-11-06

## Commands

[tools.cs](../../tools.cs) owns local orchestration. Use its Cake forwarder for ordinary target calls.
Direct Cake calls are for CI debugging and target discovery only. Native setup and validation require
a provisioned checkout under the [workflow rules](workflow.md#isolated-checkouts).

```pwsh
dotnet run --file tools.cs -- build --target Info
dotnet run --file tools.cs -- build --tree
dotnet run --file tools.cs -- setup
dotnet run --file tools.cs -- ci-sim
dotnet run --file tools.cs -- build-binding-generator-image

# CI debugging and target discovery only:
dotnet run --project build/_build -- --tree
dotnet run --project build/_build -- --target Info

# Build-host regression suite:
dotnet test --project build/_build.Tests/Build.Tests.csproj -c Release

# Managed-only build, without the native pipeline:
dotnet build src/SDL2.Core/SDL2.Core.csproj
```

`setup` selects its feed with `--source=local|remote-github|remote-nuget`.
`build-binding-generator-image` builds the Linux x64 ABI-test image only.
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
slopwatch analyze --fail-on warning --exclude "artifacts/**,external/**,vcpkg_installed/**,spikes/binding-generators/references/**,**/bin/**,**/obj/**"
```

The exclusions cover generated payloads, upstream submodules, installed natives, reference clones
and build output. Fix new findings rather than hiding them in the baseline.
After deleting or moving code, rebuild the baseline to remove stale paths, using the same exclusions:

```pwsh
slopwatch init -f --exclude "artifacts/**,external/**,vcpkg_installed/**,spikes/binding-generators/references/**,**/bin/**,**/obj/**"
```

Review the baseline diff: it must not conceal a new finding.

## CI

[ci.yml](../../.github/workflows/ci.yml) runs light managed checks on pushes to `master` and every pull request, including
Markdown-only changes. It calls the reusable [build-host workflow](../../.github/workflows/build-cake-host.yml)
and [spike checks](../../.github/workflows/spike-checks.yml); workflow lint runs alongside them.
[release.yml](../../.github/workflows/release.yml) owns the heavy native and package matrix. It runs
by manual dispatch or the explicitly enabled schedule and reuses the same build-host workflow.
Release dispatch, publishing and merge remain Deniz's actions under [workflow](workflow.md#agent-autonomy).
