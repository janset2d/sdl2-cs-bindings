# Binding Generator Maintenance

Date: 2026-11-06

[Generation](../architecture/binding-generation.md) owns inputs, determinism, config boundaries and
postprocess order. [ABI policy](../architecture/binding-abi-policy.md) owns translation.
This guide owns runnable maintenance procedures. Run commands from the repository root.
Use a provisioned checkout for native/header work under [workflow](workflow.md#isolated-checkouts).

## Sources and invocation

| Input | Owner |
|---|---|
| Native dependency baseline, features and overlays | [Native maintenance](native-dependency-maintenance.md) |
| ClangSharp tool and its transitive native runtime | [Tool manifest](../../spikes/binding-generators/.config/dotnet-tools.json), then the tool's resolved dependencies |
| Postprocess Roslyn package | [Postprocess project](../../spikes/binding-generators/clangsharp/postprocess/Janset.SDL2.PostProcess.csproj) |
| Oracle Roslyn package | [oracle.cs package directive](../../spikes/binding-generators/clangsharp/oracle.cs) |
| Family identities, header order, handles, flags and C-long function lists | [family-config.json](../../spikes/binding-generators/clangsharp/config/family-config.json) |
| Parser directives | [RSP files](../../spikes/binding-generators/clangsharp/rsp/) |
| Orchestration and family activation | [generate_bindings.py](../../spikes/binding-generators/clangsharp/generate_bindings.py) |
| SDK and build settings | [global.json](../../global.json), [spike properties](../../spikes/binding-generators/Directory.Build.props), [central packages](../../Directory.Packages.props) |

Read versions from these owners, not a copied toolchain table. Revalidate the coherent toolchain
when any component changes. The parser loads the tool's transitive native runtime, not an unrelated
top-level package pin. Oracle and postprocess must use the same Roslyn version because they parse
the same generated corpus. Do not add a CPM bypass or a warning suppression to update a pin.

Restore and inspect the actual manifest before generating:

```pwsh
dotnet tool restore --tool-manifest spikes/binding-generators/.config/dotnet-tools.json
dotnet tool list --tool-manifest spikes/binding-generators/.config/dotnet-tools.json
```

The existing Python invocation is:

```pwsh
python spikes/binding-generators/clangsharp/generate_bindings.py --family core --execute --vcpkg-triplet x64-windows-hybrid --use-platform-header-shims
```

Family choices are core, image, ttf, mixer, gfx and all. Without `--execute`, commands are printed,
not run. `--family all` uses the orchestrator's explicit active set. A config entry alone does not
activate a family. The shim flag is a Windows-local iteration aid, never production evidence.
Use the installed header triplet for the actual target; Linux generation omits shims.

## Configuration audit

Read the actual schema and field inventory from family-config.json and its
[loader](../../spikes/binding-generators/clangsharp/postprocess/Config/). Do not mix it with the host
manifest schema. Config facts are shared by the orchestrator and postprocess, not mirrored in code.

- **Platforms:** `platform_views` selects name, OS attribution and defines. `all_platform_macros`
  owns the complete cross-contamination undefine set. Read its length from config (the corrected
  inventory contains 27 entries), rather than assuming one macro per platform.
- **Identity:** namespace, raw/public class, import library, include subdirectory, project directory,
  family RSP and owner mode. Owner families may also consume Core handles.
- **Headers:** dependency-ordered entries and platform-sensitive header selection. Required surface
  covers intentionally unreachable umbrella declarations; it is not the ordinary header inventory.
- **Handles:** audited discovered names, force-opaque exceptions and excluded candidates. Record
  typedef/tag evidence, header, wiki evidence, rationale and audit date. Triangulate release headers,
  wiki and generated empty-struct output. Wiki absence is explicit, not invented evidence.
- **Flags:** allow-list entries need bitmask rationale when the case-sensitive Flags suffix is absent.
- **C long:** list affected functions, not ABI type rules. Inspect signed and unsigned input/return
  declarations semantically; avoid grep patterns that confuse long long, double or identifier names.

On every changed family's upstream release, recheck header inventory, opaque roster, flags and
C-long functions; update only that family's library version and audit dates. Untouched satellites
retain their independent versions and dates. Use `rg --no-ignore` for installed headers so ignore
rules do not silently hide input evidence. Review stale/unused config findings from postprocess and
oracle; classify them, never declare an unavailable input to be a clean result.

For a platform audit, inspect SDL_platform/config/stdinc/system/main/syswm headers. Compare SDL2
conditionals with the config undefine catalog. Add a parse view only when public declarations or
attribution change, not merely because a driver-internal macro exists. SDL3's SDL_PLATFORM_* catalog
is separate and must be audited from its own pinned headers.

## RSP maintenance

Use base for cross-family directives, family for family scope, and per-header for the narrow case.
They compose additively: a lower tier cannot override a duplicate key. Restructure the owning entry
instead. One unquoted remap goes on each line; spaces are part of the libclang token. For example,
`wchar_t *=nint` differs from `wchar_t*=nint`, and const has a separate entry.

Add only a proven type remap, justified exclusion, needed macro definition or parser typed-enum
override. Keep SDL_PixelFormatEnum distinct from SDL_PixelFormat. Exclusions distinguish inline
helpers, variadic deferrals, BCL replacements and compiler magic. Preserve foreign type provenance
and exclude duplicate tags. A missing SDLCALL annotation does not automatically need an override:
regenerate and inspect Compat Cdecl and Modern UnmanagedCallConv first.

At every parser/tool bump and SDL minor update, reread each rationale and test removal one entry
at a time. Retire an entry only when regeneration proves its original failure is gone. Compare peer
RSPs as evidence, not as options to copy blindly. Bare parser flags without rationale are unacceptable.

[base.rsp](../../spikes/binding-generators/clangsharp/rsp/base.rsp) owns the exact option list:
SDL_DECLSPEC removal normalizes export attributes; SDL's intrinsic-disable defines avoid builtin
definition collisions; `-fdeclspec` enables the needed attribute syntax without the full MS dialect;
undefining __has_builtin removes overflow inline helpers from parsing. Recheck all affected
conditionals so a parser aid does not silently remove public declarations.

## Regenerate-and-classify loop

Use this procedure for SDL/port/header, ClangSharp/native runtime, Roslyn, triplet or generator changes.
Treat output as public API until the complete diff proves otherwise.

1. Record the reason, old pins and expected source change. Follow the single
   [native baseline procedure](native-dependency-maintenance.md#baseline-and-version-update) for vcpkg.
   A port patch can change declarations even when upstream version is unchanged.
2. Update only the affected pin sources and family facts. For Roslyn, update oracle and postprocess
   together through their actual package declarations. Restore the tool manifest and projects.
3. Run generator, postprocess and oracle self-tests:

   ```pwsh
   python spikes/binding-generators/clangsharp/generate_bindings.py --self-test
   dotnet run --project spikes/binding-generators/clangsharp/postprocess/Janset.SDL2.PostProcess.csproj -c Release -- --self-test
   dotnet run --file spikes/binding-generators/clangsharp/oracle.cs -- --self-test
   ```

4. Regenerate the complete affected families, then the active set. For Windows iteration:

   ```pwsh
   python spikes/binding-generators/clangsharp/generate_bindings.py --family all --execute --vcpkg-triplet x64-windows-hybrid --use-platform-header-shims
   git diff --ignore-cr-at-eol -- spikes/binding-generators/clangsharp/src
   ```

5. Review new/removed functions, signatures, layouts, attributes, constants, macro and callback
   shapes. Run the four determinism checks from generation canon: repeated complete-family output,
   isolation, aggregate/individual equivalence and cross-family handle substitution.
6. Produce and compare oracle evidence:

   ```pwsh
   dotnet run --file spikes/binding-generators/clangsharp/oracle.cs -- --family sdl2-core --family sdl2-image --family sdl2-ttf --family sdl2-mixer --family sdl2-gfx --write-report
   git diff -- spikes/binding-generators/output/reports/oracle-evidence-clangsharp.md
   ```

7. Classify each delta using [output review](binding-output-review.md). New opaque candidates need
   triangulation; C-long symbols need public-API versus BCL-exclusion classification and input/return
   tests; flags need header evidence; escaped wchar pointers need a fixture at the actual remap route;
   foreign types need justified per-header mappings. Repeat regeneration after each approved correction.
   A reduced finding count is not automatically success: confirm the missing evidence did not disappear.
8. Build affected generated family projects across their library TFMs. Run ABI tests against matching
   packaged natives on the affected targets:

   ```pwsh
   dotnet test --project spikes/binding-generators/clangsharp/tests/abi-tests/AbiTests.csproj -c Release
   ```

9. Compare test names, counts, skips and deferrals with the prior run. Investigate lost coverage;
   additions need an explicit reason. Check exported symbols after visibility/feature changes, and
   run the package-first matrix before promotion. A dormant host symbol validator is not evidence
   that a pack-stage check already ran.
10. Run [quality gates](quality-gates.md), including its config-owned slopwatch command for authored
    code changes. Keep same-change documentation and record commands, exits, pins and deltas in the
    work-branch checkpoint/review. Major toolchain-policy changes require the applicable ADR review.

Dynapi is Core-only and name-only. Compare emitted ∩ manifest, emitted minus manifest and manifest
minus emitted, with explicit exclusions. Read current counts from the report rather than copied
percentages. Satellite names need header/export evidence; wire correctness needs runtime calls.
Binary-cache hits may not extract vcpkg source. The image and setup action preserve or provision
the manifest-pinned dynapi source; an empty buildtree is missing evidence, not zero expected exports.

## Add a satellite

Follow [native maintenance](native-dependency-maintenance.md#add-a-satellite-family) for package,
manifest, triplet and solution work. Then:

1. Add the real family config, dependency-ordered headers and required surface. Add family RSP;
   per-header RSP is needed only for a proved exception.
2. Use an existing [generated family project](../../spikes/binding-generators/clangsharp/src/) as
   the backend-include template. Preserve the Core reference and per-assembly modern marshalling
   attribute. Read conditions from the project rather than copying obsolete framework names.
3. Audit opacity, force-opaque cases, flags, C-long inputs/returns, exports and macros. Classify
   value-like, computed, function-like and runtime-dependent macros separately. Preserve Core ownership.
4. Confirm oracle support and the explicit activation selector. A disabled family must remain
   side-effect-free; enabling config without tests is not activation evidence.
5. Run self-tests including empty-family and satellite-only fixtures, complete generation, API
   snapshots, multi-TFM compilation, representative init/create/free ABI calls and Linux/platform
   evidence. Review the complete diff, not only the newly added header.

## Linux baked-image evidence

[tools.cs](../../tools.cs) `build-binding-generator-image` builds the Linux x64 ABI-test image with
manifest-pinned vcpkg state. It does not generate bindings or start a container.
The [Dockerfile](../../docker/binding-generator.Dockerfile) copies source into the image and preserves
native/header/dynapi inputs. Rebuild it after a source edit. Do not bind-mount a Windows checkout
into a Linux C/C++ or vcpkg operation; use COPY-into-image and isolated cache volumes.

For explicit generation within that prepared image:

```bash
docker run --rm --entrypoint sh janset-binding-generator:focal-latest \
  -c "cd /workspace && python spikes/binding-generators/clangsharp/generate_bindings.py --family all --execute --vcpkg-triplet x64-linux-hybrid"
```

For ABI tests, provision Core.Native linux-x64 assets in the local feed first. Read the actual feed
and version from the generated local override; copy that feed into a disposable container, not a
checkout bind mount:

```pwsh
dotnet run --file tools.cs -- build-binding-generator-image
if ($LASTEXITCODE -ne 0) { throw 'Image build failed.' }
[xml]$local = Get-Content build/msbuild/Janset.Local.props -Raw
$feed = [string]$local.Project.PropertyGroup.LocalPackageFeed
$version = [string]$local.Project.PropertyGroup.JansetSdl2CorePackageVersion
if ([string]::IsNullOrWhiteSpace($version) -or -not (Test-Path -LiteralPath $feed -PathType Container)) {
    throw 'Provision the local Core.Native feed and version first.'
}
$container = docker create --entrypoint sh janset-binding-generator:focal-latest -c "cd /workspace && dotnet test --project spikes/binding-generators/clangsharp/tests/abi-tests/AbiTests.csproj -c Release --framework net10.0 -p:LocalPackageFeed=/feed -p:JansetSdl2CorePackageVersion=$version"
if ($LASTEXITCODE -ne 0) { throw 'Could not create ABI test container.' }
try {
    docker cp "$feed/." "${container}:/feed"
    if ($LASTEXITCODE -ne 0) { throw 'Could not copy package feed.' }
    docker start --attach $container
    $testExit = docker inspect --format '{{.State.ExitCode}}' $container
    if ($LASTEXITCODE -ne 0 -or $testExit -ne '0') { throw 'ABI test container failed.' }
}
finally {
    docker rm $container
}
```

The explicit entrypoint avoids inherited image-command assumptions. Native-platform validation
does not become cross-OS evidence merely because a synthetic view compiled. SDL3 needs its own
catalog, bool/IO/identity/TFM decisions and real native packaging before activation.
