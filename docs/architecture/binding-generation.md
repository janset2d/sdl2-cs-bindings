# Binding Generation

Date: 2026-11-14

[ABI policy](binding-abi-policy.md) owns translation and public shape. This document owns generator
inputs, isolation, configuration, mechanism and promotion evidence.
[ADR-0005](../adr/0005-clangsharp-generator-in-csharp-tooling.md) records the toolchain direction.
Use [maintenance](../engineering/binding-generator-maintenance.md) for runnable procedures.

## Authority and generator boundary

Pinned public headers define declarations and native signatures. Built package exports prove shipped
function names. Current implementation and tests establish actual behavior, not permission to ignore
policy. Dynapi proves Core function-name coverage only. Peer bindings and historical reports are
evidence, not policy. If code and canon disagree, follow [Deviation](../engineering/workflow.md#deviation).

The generator is build infrastructure. Generation must produce committed `.g.cs` files plus a
deterministic per-family stamp. It never runs in a consumer build or release pack; release validates
the stamp and never regenerates. Route local production orchestration through tools.cs, keep contracts
in the host data layer, and expose cross-cutting validation to preflight and pack. The [roadmap](../ROADMAP.md)'s
Tooling gap owns the current implementation state.

Separate ABI mechanics from named SDL family policy without creating a general generator product.
Keep concepts target-local until another real consumer exists. Use actual installed satellite headers
to establish export macros, ownership and naming; gfx's scope macros must not become generic AST
special cases. A family routing key selects code-owned policy; it does not turn JSON into an ABI language.

## Generation determinism contract

Every output byte comes from pinned local inputs: native headers, triplet/platform views, tool
version, response files, family configuration and header inventory, postprocess and orchestrator code.
Read library pins from [the manifest](../../build/manifest.json), vcpkg state from
[vcpkg.json](../../vcpkg.json) and [the submodule](../../external/vcpkg/), and the actual ClangSharp
tool version from [the root tool manifest](../../dotnet-tools.json).
Do not infer the parser's native runtime from unrelated central package pins.

The determinism unit is the **complete family artifact**, including every backend and postprocess
output. Do not write bootstrap/header subsets into a committed Generated root. Committed source
and stamps have no clocks, usernames, machine paths, build IDs or other ambient values. No network
access occurs during generation. Host differences are explicit parse-view inputs, not hidden OS branches.
Use stable ordering and LF output; ignore checkout CRLF only when comparing semantic regeneration drift.

The generator must provide all of these guarantees:

- Targeted regeneration cleans and writes only that family's generated root. Other families stay
  byte-untouched. A path-prefix guard bounds cleanup before deletion.
- The full active-family run equals the union of individual runs. Dormant families stay dormant.
- Postprocess reads only the selected family's generated tree. Cross-family handle names come from
  shared **configuration data**, never another family's generated C#.
- Satellite-to-Core ProjectReference is a compilation dependency, not a generation-order dependency.
  Core can regenerate independently; its public support must exist before satellite API activation.
- Parse includes resolve the entire header tree, but emit scope includes owned declarations only.
  Every public C type has one owner; satellites reference Core rather than duplicating its types.
- Unknown enabled validator IDs fail closed and name the ID. Disabled-family audit resolution has
  no parse, emit, cleanup, validation, compile or smoke side effects.
- Empty family work sets complete without dead-code injection. Prove the mechanisms with satellite
  fixtures before enabling the satellite.

Every generation/postprocess change supplies four checks: regenerate a complete family twice;
prove untouched-family isolation; compare aggregate and individual family artifacts; prove satellites
retain Core handle substitutions. A change to an input or isolation rule updates this contract and
its verification in the same change.

## Configuration owns scope; code owns mechanism

The [manifest](../../build/manifest.json) is the single configuration owner. Its top-level
`binding_generation` owns aggregate family order, platform views, macro tokens and shared input paths;
each library's `binding_generation` owns its typed family profile. Package families resolve to libraries
through `library_ref`. The [configuration repository](../../build/_build/Data/BindingGeneration/BindingGenerationConfigRepository.cs)
returns canonical package-family identities and enabled families in the explicit generation order.
Postprocess uses a thin typed view of those profiles, not an independent JSON reader.

Existing library versions, managed namespaces, public class names and required declarations are reused,
not copied into parallel generator fields. Raw class names derive from the primary class plus `Native`;
the generated root derives from the generation project's parent plus `Generated`. Generation projects
remain distinct from package projects. Response-file contents remain owned by the RSP files.
Manifest loading refuses unsupported schemas and unknown keys in generation objects. PreFlight's
[shape validator](../../build/_build/Validation/BindingGeneration/BindingGenerationManifestValidator.cs)
checks identities, ordered inventories, view/macro uniqueness, bounded input paths and enabled validator
IDs against production registrations. It never resolves installed headers or runs postprocess.

| Concern | Config facts | Code mechanism |
|---|---|---|
| Identity | Families, namespace, public class, import library, project path and owner mode | Identity resolution, raw-class derivation and validated convention derivation |
| Headers | Ordered inventory, family/header RSP selection, required/excluded declarations | Parse, ownership and required-surface validation |
| Handles | Audited names, force-opaque exceptions, excluded candidates | Structural discovery, handle template, pointer rewrite and cross-family pull |
| Flags | Family allow-list and audit provenance | Suffix match plus allow-list lookup |
| C long | Affected functions | Native-type discriminator, exact-width dispatch and input range checking |
| Platforms | Views, defines, complete undefine inventory and OS attribution tokens | View merge, neutral subtraction and incompatible-signature refusal |
| Macros | Explicit symbol/kind rosters and justified exceptions | Taxonomy, expression evaluation and reusable emitters |

Scalar widths, boolean wire shape, pointer/array/callback classification, UTF-8 behavior, variadics
and layout remain code-owned policies. A JSON exception names declaration, category, evidence and
rationale; no silent knob redefines a universal ABI rule. Derive identity from existing facts first.
If derivation cannot prove a real family case, return a typed family/profile/derivation error rather
than adding a convenient new field. One fact is read from one file, not mirrored in consumer dictionaries.

## Parser and backend mechanism

The Python orchestrator is retired; this revision has no generation command. Generation moves into
the `GenerateBindings` Cake target. The [ClangSharp wrapper](../../build/_build/Tools/ClangSharp/)
renders and captures one invocation; family, header and platform-view selection belong to orchestration.
Aggregate selection must be explicit, not inferred from the mere existence of config entries.
Both Compat and Modern projections belong to the same family run.

RSP composition is base, family, then per-header. ClangSharp keys are additive, not overrides: a
duplicate key is an error. Literal spaces in remap tokens matter. The
[RSP directory](../../build/binding-generation/rsp/) owns exact parser options and rationale.
Synthetic [platform-header shims](../../build/binding-generation/shims/platform-headers/)
are an explicit Windows iteration aid, never native-platform or production evidence.
Linux-canonical parsing and the consumer builder floor are separate contracts under
[ADR-0010](../adr/0010-linux-builder-glibc-floor.md).

The internal [postprocess collaborators](../../build/_build/Targets/GenerateBindings/PostProcess/)
run in-process in the build host; they have no command-line entry point. Orchestration owns their order.
The normalization sequence is:

| Mode | Effect |
|---|---|
| platform-delta | Subtract neutral/earlier declarations and add guarded platform attribution |
| strip-varargs | Remove unsupported ellipsis shapes under explicit fmt-only policy |
| libraryimport | Modern-only DllImport to LibraryImport plus Cdecl call-convention attributes |
| flags-detect | Apply the ABI policy's name/allow-list rule, not value heuristics |
| guid-substitute | Replace SDL_GUID declarations/references with the proven Guid representation |
| clong-dispatch | Emit backend-specific C-long handling from native annotations and config function names |
| uniform-opaque | Emit handle structs and rewrite single-pointer parameters, returns and fields |
| struct-layout | Add sequential layout only when layout is absent; preserve explicit unions and handles |
| public-typed | Discover the final Core Modern surface and emit its TFM-shared public projection |

Platform subtraction precedes all rewrites; varargs normalization precedes Modern import promotion.
C-long rewrite precedes handle substitution. Keep syntax changes at Roslyn-node level so attributes,
trivia and parameter shapes compose. Each transform owns one concept and has host tests for empty
input, matching and nonmatching shapes, trivia, idempotence and config wiring. New transforms
document placement and dependency; do not bolt unrelated rules onto another rewriter.

Postprocess is not a Roslyn source generator: it must mutate existing
files and commit reproducible output before consumer compilation. A source generator can add
compiler inputs, not rewrite those files into a reviewed repository artifact.

### Handles and cross-assembly imports

[OpaqueHandleEmitRewriter](../../build/_build/Targets/GenerateBindings/PostProcess/OpaqueHandleEmitRewriter.cs)
combines syntactic empty-struct/pointer discovery with force-opaque config. Discovery is not gated
on SDL_ prefixes or NativeTypeName presence. Owner mode emits local handles; consumer mode does
not invent local copies. The config loader merges satellite-owned names with Core's audited and
force-opaque names. Double pointers/out positions survive unchanged.

Audits triangulate pinned release headers, wiki opacity evidence and Modern empty-struct output.
Record library version, source and last-audited date; wiki evidence may be absent when headers and
output agree. Compare discovery with each owner's roster and review drift rather than swallowing it.

Each importing assembly has its own guarded support-file attribute:

```csharp
#if NET7_0_OR_GREATER
[assembly: System.Runtime.CompilerServices.DisableRuntimeMarshalling]
#endif
```

This declares the blittable-only interop model and resolves the cross-assembly source-generator
limitation reported as SYSLIB1051 for otherwise proven unmanaged handle structs. It is not a warning
suppression. It is assembly-scoped and does not propagate from Core to satellites. The Compat
DllImport backend remains independently tested. See the
[Microsoft disabled-marshalling contract](https://learn.microsoft.com/en-us/dotnet/standard/native-interop/disabled-marshalling).

### C long backend detail

[ClongDualDispatchRewriter](../../build/_build/Targets/GenerateBindings/PostProcess/ClongDualDispatchRewriter.cs)
reads affected function names from config and signedness from NativeTypeName. Modern uses
CLong/CULong; Compat selects Windows int/uint versus Unix LP64 native width at runtime.
Families without affected functions are no-ops. The
[generated project files](../../spikes/binding-generators/clangsharp/src/) choose backend files;
read their conditions instead of copying a TFM list into prose. ABI policy owns the normalized
public shape and Windows input checks, not these backend spellings.

### wchar_t and foreign types

The exact literal-space entries `wchar_t *=nint` and `const wchar_t *=nint` in
[base.rsp](../../build/binding-generation/rsp/base.rsp) cover shared wide pointers.
There is no WcharStarToNintRewriter fallback. If a new case escapes remapping, reproduce and fix
the actual translation route with a fixture rather than documenting a nonexistent defense.
Per-header foreign remaps likewise match libclang spelling and exclude duplicate tag declarations.
Use the [per-header inventory](../../build/binding-generation/rsp/per-header/) for exact
Vulkan, Direct3D, GDK and other mappings. Do not turn a missing platform layout into a guessed one.

## Oracle evidence labels

[oracle.cs](../../spikes/binding-generators/clangsharp/oracle.cs) owns mechanical check IDs and report
emission. Read IDs from code instead of duplicating a check roster here.

| Mechanical label | Meaning |
|---|---|
| Hard Bug | Contradicts authoritative input or the check's policy |
| Likely Bug | Strong signal with incomplete evidence |
| Accepted Deferral | Explicit policy deferral, corresponding to a review Accepted Delta |
| Compatibility Risk | SDL2-CS difference, not proof that the target is wrong |
| Evidence Missing | Required input is unavailable or not wired |
| Out Of Scope | The evidence source is not applicable to the family/category |

The human review taxonomy also has Design Signal and Follow-up; those are not mechanical
extraction outcomes. The report is local-generator provenance, not an independent upstream oracle.
[Output review](../engineering/binding-output-review.md) owns interpretation and reconciliation.

## Promotion evidence gates

Before promoting generated source, prove or explicitly classify each applicable gate:

- Compile every library TFM and review per-TFM API snapshots. Public C-named imports form the
  binding tier; private imports appear only for per-member ABI demotion.
- Match header/dynapi/export function names after justified exclusions; fixture-test C long,
  wchar_t, boolean storage, size_t, pointers, callbacks, fixed arrays and platform-conditioned layouts.
- Report macro emissions, skips, unsupported candidates, overrides and stale exceptions.
  Review oracle deltas and all remaining blockers.
- Test every friendly pattern red/green, including strings, buffers, out/ref and fmt-only calls;
  package smoke covers string/path/resource pairs and makes allocation/formatting semantics visible.
- Production project build must fully replace any retired compile harness's value. Commit the
  complete generated family and a stamp covering generator/toolchain, vcpkg state, library version,
  header fingerprint/count and parse views, with no wall-clock fields. Stale diagnostics name the
  family, stale field and remediation. Same-input regeneration must be diff-clean.
- Prove package-first init/quit, error retrieval, an environment-permitting create/destroy path and
  a deterministic callback. No promoted family still compiles vendored SDL2-CS declarations.
- Satellite promotion proves consume-not-redeclare Core types, explicit duplicate-type diagnostics
  (family/type/header/expected owner), actual symbol existence and the full supported runtime matrix.
- Fixtures stay tiny, committed, deterministic and licensed; CI stays headless. SDL3 proves its own
  boolean, IOStream, macro, namespace/import and TFM contracts before any public preview.

The [testing guide](../engineering/binding-testing.md) owns test topology and the
[maintenance guide](../engineering/binding-generator-maintenance.md) owns commands.
