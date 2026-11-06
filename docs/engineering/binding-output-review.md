# Playbook: Binding Output Oracle Validation

> **Status:** Canonical multi-oracle review workflow. Toolchain-neutral methodology complementing the automated `oracle.cs` evidence extractor under [`spikes/binding-generators/clangsharp/`](../../clangsharp/).

This playbook defines how to validate generated binding output using multiple imperfect oracles: official SDL sources, native exports, generated evidence, peer bindings, and .NET API extraction. It is intentionally review-led. Tooling collects evidence; agents and humans decide what the evidence means.

## When To Use This

Use this playbook when:

- a binding generator regeneration produces a new SDL2 family preview (Core or a satellite family);
- macro, enum, struct, callback, or function translation changes the generated public API;
- `oracle-evidence-clangsharp.md` shows unexpected category/count movement or new evidence-categorisation deltas;
- dynapi validation emits missing/extra function warnings;
- peer comparison is needed before promoting generated output toward production source;
- a vcpkg SDL2 pin bump changes public headers or exports.

Do not use this playbook as a substitute for compile checks, fixture tests, native smoke tests, or package consumer smoke. Those layers are defined in [`testing-strategy.md`](testing-strategy.md). This playbook complements them by answering a different question: "Does this generated .NET API look like the correct SDL binding surface, not just compilable C#?"

## Principle

The validator must not pretend that every useful oracle is machine-truth.

Reliable automation is welcome for narrow facts: exported symbol names, public access modifiers, generated file presence, schema shape, and compile success. Broader API correctness remains a review problem because SDL2-CS is stale, SDL3 peer libraries have different public shapes, SDL wiki pages are semantic documentation rather than ABI manifests, and dynapi is name-only.

Use automation as an evidence extractor. Use multi-agent review and maintainer judgment for interpretation.

### Relationship To `oracle.cs`

The active automated narrow-fact extractor is [`oracle.cs`](../../clangsharp/oracle.cs), a Roslyn-based evidence reporter that emits [`oracle-evidence-clangsharp.md`](../../output/reports/oracle-evidence-clangsharp.md) covering generated category counts, dynapi coherence, opaque-handle inference, and per-family symbol enumerations. That report is the automated lane; this playbook covers the broader review-led workflow whose evidence inputs include the `oracle.cs` report alongside pinned SDL headers, native exports, peer bindings, and .NET API extraction.

Treat `oracle.cs` output as local-generator provenance, not independent upstream truth (see "Source Hierarchy" rank 7 and "Common Mistakes").

## Source Hierarchy

| Rank | Source | Use For | Do Not Use For |
| --- | --- | --- | --- |
| 1 | Pinned SDL2 public headers from the exact vcpkg/upstream version | Function declarations, C signatures, typedefs, constants, enums, structs, callbacks, platform conditionals | Runtime export reality or .NET API ergonomics |
| 2 | Actual built native binaries | Shipped export names; missing/unexpected exported function reality | Signatures, structs, constants, callbacks |
| 3 | SDL2 `src/dynapi/SDL2.exports` | SDL2.Core public function name existence | Signatures, constants, structs, callbacks, satellites, SDL3 |
| 4 | SDL2 `src/dynapi/SDL_dynapi_procs.h` | Secondary function signature/name evidence for SDL2.Core | Public API design truth |
| 5 | SDL wiki/API docs | Ownership, UTF-8 expectations, lifetime, thread-safety, version notes, semantics | Exhaustive ABI truth |
| 6 | Peer bindings (.NET first, curated cross-language second) | Compatibility signals, conceptual cross-checks, ergonomics patterns, suspicious surface deltas | Authoritative correctness |
| 7 | `oracle-evidence-clangsharp.md` (oracle.cs evidence report) | Generator provenance: what this repo parsed, emitted, skipped, and attributed per family | Independent upstream truth |

The current SDL2 pin wins. Do not compare generated output to SDL `main`, a moving wiki page, or a peer binding without recording the exact source version/ref.

## Existing Evidence Inputs

| Input | Why It Matters |
| --- | --- |
| `spikes/binding-generators/clangsharp/src/Janset.SDL2.*/Generated/**/*.g.cs` | Consumer-visible generated source per family (Core + satellite families). |
| `spikes/binding-generators/output/reports/oracle-evidence-clangsharp.md` | Machine-extracted, human-reviewed evidence report: per-family category counts, dynapi coherence, opaque-handle inference, emitted/skipped facts. |
| `spikes/binding-generators/clangsharp/src/Janset.SDL2.*/Janset.SDL2.*.csproj` | Per-family multi-TFM compile proof for generated output. |
| `vcpkg_installed/<triplet>/include/SDL2/*.h` | Local pinned public header input. |
| `external/vcpkg/buildtrees/sdl2/src/*/src/dynapi/SDL2.exports` | SDL2.Core function-name export manifest when buildtrees source exists. |
| `external/sdl2-cs/src/SDL2.cs` | Local legacy SDL2 compatibility oracle. Useful but not authoritative. |

Remote peer sources may be consulted during review, but record URLs and refs in the report. Do not add a peer to the regular review set just because it exists on GitHub; it needs adoption, maintenance, version proximity, and license sanity.

## What Can Be Automated Reliably

These checks are good candidates for fail gates once they are stable:

- generated output compiles across `net10.0`, `net9.0`, `net8.0`, `netstandard2.0`, and `net462`;
- no effectively public `[DllImport]` or `[LibraryImport]` declarations leak into generated public API;
- no public raw ABI class such as `SDL2Native` / `SDL_<family>Native` is exposed. Lexically public generated methods inside an internal raw container are not public API leaks;
- no function emitted by the generator is absent from the SDL2 dynapi manifest, excluding explicit configured exclusions;
- generated function names are present in actual packaged native binary exports at Pack stage;
- `oracle-evidence-clangsharp.md` exists, has the expected schema, and lists all generated families;
- hard internal macro denylist entries stay skipped with explicit reasons;
- source-visible manual macro includes/excludes/overrides are consumed or explicitly stale-tolerant;
- generated API snapshots can be produced per TFM for review.

The first implementation of these checks should usually be evidence-first, not fail-first. Promote a check to fail-gate only after repeated runs show that failures indicate real product risk rather than expected generator churn.

## What Must Stay Review-Led

These checks require agent/human interpretation:

- SDL2-CS missing a generated function: it may be stale rather than our bug;
- SDL2-CS exposing `IntPtr` where we expose typed handles;
- SDL3 peer libraries modeling a similar concept differently;
- cross-language bindings modeling a concept differently because their host language has different FFI, ownership, or pointer ergonomics;
- SDL wiki documentation implying ownership or lifetime semantics not visible in headers;
- platform-specific declarations whose visibility depends on parse-view macros;
- structs/unions that are public in headers but not useful or not safe in Stage 1;
- callback and string overload ergonomics;
- acceptable modern API deltas from legacy SDL2-CS compatibility.

If a reviewer cannot tie a finding to current SDL2 headers, shipped exports, or an explicit public API decision, classify it as a signal rather than a hard bug.

## Peer Binding Tiers

Peer bindings are useful because they capture community-maintained interpretations of the SDL surface. They are dangerous when treated as truth. The routine peer set must stay curated.

### Primary .NET Peers

| Peer | URL | Use | Caveat |
| --- | --- | --- | --- |
| SDL2-CS | `external/sdl2-cs/src/SDL2.cs` | SDL2 compatibility and migration signals; legacy constants/enums/functions/callbacks | Old and explicitly not the target shape. `IntPtr` and string patterns are not correctness requirements. |
| ppy/SDL3-CS | https://github.com/ppy/SDL3-CS | SDL3 generator/process reference; platform-pass and friendly-overload concepts | SDL3 only; public raw shape differs from Janset's chosen API. |
| Alimer.Bindings.SDL | https://github.com/amerkoleci/Alimer.Bindings.SDL | CppAst-style SDL3 output and typed-handle ergonomics | SDL3 only; single-pass/platform model is not our validation target. |
| Silk.NET | https://github.com/dotnet/Silk.NET | General generated interop scale, span/ref overload ideas, low-level binding conventions | Not SDL2 semantic truth. |

### Secondary Cross-Language Peers

Use these as soft oracles. A cross-language peer can create a `Design Signal`, `Compatibility Risk`, or suspicious-delta note. It cannot create a `Hard Bug` by itself; confirm against pinned SDL2 headers, dynapi, or shipped exports first.

| Peer | URL | Routine Use | Caveat |
| --- | --- | --- | --- |
| Rust-SDL2 `sdl2-sys` | https://github.com/Rust-SDL2/rust-sdl2/tree/master/sdl2-sys | Strongest cross-language raw/header-generated signal for functions, constants, enums, structs, callbacks, and satellite coverage | Use exact ref; published crates can lag the current SDL2 pin. Rust type choices do not map directly to C#. |
| Rust-SDL2 `sdl2` | https://github.com/Rust-SDL2/rust-sdl2 | Ownership, lifetime, resource wrapper, callback, and semantic design signals | High-level Rust API hides raw details and should not drive generated low-level shape. |
| go-sdl2 | https://github.com/veandco/go-sdl2 | Popular Go/cgo SDL2 reference for obvious public functions, constants, satellite coverage, and semantic wrapper choices | Maintainer bandwidth is mixed; CI/header versions can lag. Go cgo shape is not ABI proof for C#. |
| BindBC SDL2 branch | https://github.com/BindBC/bindbc-sdl/tree/SDL2 | D binding with direct/static/dynamic SDL2 coverage, version gates, satellites, and constants | D loader/version-id model is language-specific; not at the exact SDL2 pin. |
| PySDL2 | https://github.com/py-sdl/py-sdl2 | Python ctypes signal for raw function presence, constants/macros, and broad satellite exposure | Handwritten ctypes flattens type nuance; not a struct-layout authority. |

### Ad Hoc Secondary Peers

These may be useful for a specific dispute but should not be part of every validation run.

| Peer | URL | Use Only When |
| --- | --- | --- |
| Fermium | https://github.com/Lokathor/fermium | A raw Rust third opinion is needed for an older/core SDL2 symbol. Stale versus the current SDL2 pin. |
| haskell-game/sdl2 | https://github.com/haskell-game/sdl2 | A core SDL2 semantic/lifetime question benefits from another mature high-level wrapper. Core-focused and Haskell-shaped. |
| Vladar4/sdl2_nim | https://github.com/Vladar4/sdl2_nim | Satellite symbol names/constants need a fallback signal and stronger peers disagree. Stale for current SDL2.Core. |

Exclude abandoned, tiny, stale, or demo-only repos from routine review. Examples: old Go SDL2 forks, obsolete standalone Rust satellite crates, `DerelictSDL2`, Zig demos, and pygame/pygame-ce as direct binding oracles. Popular runtime libraries are not the same as direct SDL binding surfaces.

## Finding Taxonomy

| Classification | Meaning | Expected Action |
| --- | --- | --- |
| Hard Bug | Contradicts pinned headers, native exports, ABI width/layout, public/internal boundary, or settled API strategy | Fix before proceeding. |
| Likely Bug | Strong evidence of wrong output, but one source is ambiguous | Investigate or add targeted proof. |
| Compatibility Risk | Could affect SDL2-CS migration or user expectations, but not necessarily wrong | Document, decide, possibly defer. |
| Design Signal | Peer/docs suggest a better future API shape | Consider for follow-up, not a blocker. |
| Accepted Delta | Difference is intentional under current strategy | Record once; suppress in future reviews if noisy. |
| Follow-up | Real work, but outside the current slice or Stage 1 scope | Create/update issue or plan item. |

## Recommended Agent Lanes

Run lanes in parallel when possible. Each lane should receive only the context it needs and must not edit files unless explicitly asked.

Angle-bracket values in the prompt templates are placeholders to fill in before dispatching an agent; they are not unresolved playbook work.

### Lane 1: Official SDL Header And Docs Review

Purpose: verify generated output against pinned SDL2 public headers and docs semantics.

Agent prompt template:

```text
Review generated SDL2 family output against official SDL2 sources.

Do not edit files. Produce findings only.

Repository: <repo path>
Generated output: spikes/binding-generators/clangsharp/src/Janset.SDL2.<Family>/Generated
Oracle evidence: spikes/binding-generators/output/reports/oracle-evidence-clangsharp.md
Pinned SDL2 headers: vcpkg_installed/<triplet>/include/SDL2
SDL version: <version>

Check:
- sampled generated functions trace to public SDL2 headers with correct return/parameter concepts;
- high-risk typedefs preserve ABI width: SDL_bool, size_t, Sint64/Uint64, long, pointers, callbacks;
- constants/enums/macros match source values or are intentionally skipped;
- platform-specific APIs are attributed to the right parse view;
- docs semantics for UTF-8 strings, ownership, and freeing are not contradicted by public wrappers.

Classify findings as Hard Bug, Likely Bug, Compatibility Risk, Design Signal, Accepted Delta, or Follow-up.
Include exact symbol names, source header/doc references, and generated file/report references.
```

### Lane 2: Dynapi And Export Review

Purpose: verify that function-name coverage evidence is interpreted correctly.

Agent prompt template:

```text
Review generated SDL2.Core function coverage against SDL2 dynapi/export evidence.

Do not edit files. Produce findings only.

Repository: <repo path>
Generated output: spikes/binding-generators/clangsharp/src/Janset.SDL2.Core/Generated
Oracle evidence: spikes/binding-generators/output/reports/oracle-evidence-clangsharp.md
Dynapi manifest: external/vcpkg/buildtrees/sdl2/src/*/src/dynapi/SDL2.exports

Check:
- generator emits no functions absent from dynapi, except explicit configured exclusions;
- dynapi exports missing from generated output are understood and classified;
- current warnings are true Stage 1 deferrals, parse/header misses, or unsupported variadic/raw-shape cases;
- dynapi result is not overinterpreted as signature proof.

Return a table of emitted-extra, dynapi-missing, accepted exclusions, and open questions.
```

### Lane 3: Peer Binding Review

Purpose: triangulate against SDL2-CS, modern .NET peers, and curated cross-language peers without treating them as truth.

Agent prompt template:

```text
Review generated SDL2 family output against peer SDL binding oracles.

Do not edit files. Produce findings only.

Repository: <repo path>
Generated output: spikes/binding-generators/clangsharp/src/Janset.SDL2.<Family>/Generated
Local SDL2-CS: external/sdl2-cs/src/SDL2.cs
Optional remote peers: ppy/SDL3-CS, Alimer.Bindings.SDL, flibitijibibo/SDL3-CS, bottlenoselabs/SDL3-cs, Silk.NET.
Curated secondary peers: Rust-SDL2 sdl2-sys/sdl2, veandco/go-sdl2, BindBC SDL2 branch, PySDL2.

Rules:
- current SDL2 headers/native exports are authoritative for ABI;
- SDL2-CS is a compatibility oracle, not correctness truth;
- SDL3 peers are conceptual/style/process oracles only;
- cross-language peers are conceptual/suspicious-delta oracles only unless confirmed by SDL2 headers or exports;
- normalize typed handles vs IntPtr, byte* vs string, enum names, raw vs wrapper layering, and platform conditions;
- do not recommend changing Janset API solely to match a peer.

Classify deltas as Hard Bug, Compatibility Risk, Design Signal, Accepted Delta, or Ignore.
Include source refs and explain normalization used.
```

### Lane 4: Generated .NET Public API Review

Purpose: inspect what .NET consumers can actually call.

Agent prompt template:

```text
Review generated SDL2 family .NET public API shape.

Do not edit files. Produce findings only.

Repository: <repo path>
Generated output: spikes/binding-generators/clangsharp/src/Janset.SDL2.<Family>/Generated
Compile-check: build the per-family csproj at spikes/binding-generators/clangsharp/src/Janset.SDL2.<Family>/Janset.SDL2.<Family>.csproj across the target TFM set.

Use any practical evidence method: source scan, reflection on compiled assemblies, PublicApiGenerator, Roslyn analysis, or the oracle.cs evidence report.

Check:
- no public raw ABI container, effectively public SDL2Native/SDL_<family>Native externs, effectively public DllImport/LibraryImport, or unexpected public IntPtr-heavy API;
- handles, structs, enums, callbacks, constants, and platform command files match the API surface strategy;
- TFM-specific public differences are intentional;
- platform attributes appear where platform-specific functions are emitted;
- generated code has no obvious public API accidents that compile-check would miss.

Return findings with exact generated file/line refs and suggested action.
```

### Lane 5: Macro And Internal Leak Review

Purpose: hunt C/preprocessor/internal SDL details that should not become public .NET API.

Agent prompt template:

```text
Review generated SDL2 family constants/macros for internal leaks.

Do not edit files. Produce findings only.

Repository: <repo path>
Generated constants: spikes/binding-generators/clangsharp/src/Janset.SDL2.<Family>/Generated/**/Constants.g.cs
Oracle evidence: spikes/binding-generators/output/reports/oracle-evidence-clangsharp.md
Macro policy source: the spike postprocess equivalent under spikes/binding-generators/clangsharp/postprocess/ (FlagsAttributeRewriter, StripVarargsRewriter, and adjacent rewriters), governed by config under spikes/binding-generators/clangsharp/config/.

Check:
- SDL_config* build toggles are skipped;
- SDL assertion helpers, printf format/annotation macros, cast helpers, include guards, platform/compiler controls, and obsolete revision macros are skipped;
- source-visible public expression constants are emitted only when deterministic;
- function-like public helper macros are helper candidates, not constants;
- every suspicious public constant has an SDL public API reason.

Return hard leaks, suspicious deltas, accepted skips, and proposed policy/test additions.
```

### Lane 6: Aggregator Review

Purpose: merge independent findings into an actionable report.

Agent prompt template:

```text
Aggregate oracle validation lane results for the generated SDL2 family.

Do not edit files. Produce a final report only.

Inputs: lane reports from official SDL, dynapi/export, peer binding, .NET API, and macro/internal leak reviews.

Merge duplicates, resolve conflicts, and classify every item as Hard Bug, Likely Bug, Compatibility Risk, Design Signal, Accepted Delta, or Follow-up.

For each Hard Bug or Likely Bug, include:
- exact symbol/type/member;
- evidence sources;
- generated file/report reference;
- recommended next action;
- whether a fixture-backed test is required.

End with a short go/no-go recommendation for the current generated output.
```

## Tooling Guidance

Use existing evidence sources first. Build custom tooling only after repeated reviews show the same extraction step is wasting time.

| Tool/Source | Best Use | Caveat |
| --- | --- | --- |
| `rg` / source scan | Fast leak hunts and symbol lookups | Not semantic enough for final API judgment. |
| `oracle-evidence-clangsharp.md` (oracle.cs output) | Provenance, category counts, dynapi coherence, opaque-handle inference, per-family symbol enumerations | Generated by this repo; not independent truth. **Expect possible evidence-tool gaps around callback typedef inspection** — when the report appears to disagree with header reality on callback typedefs (delegate emit shape, parameter pointer-shape rules, no-Layer-1-rooting deferrals), confirm against pinned SDL headers before treating the report as authoritative. Callback-typedef-evidence-limitations are a known coverage gap. |
| Multi-TFM build of per-family csproj | Compile proof | Does not prove correct ABI or API intent. |
| Reflection over compiled assemblies | Actual public API surface | Needs per-TFM handling and unsafe type interpretation. |
| PublicApiGenerator | Reviewable API snapshots | Useful evidence; noisy while surface is still moving. |
| Roslyn analysis | Source-level policies such as effective public raw extern leaks | More work; best for narrow checks. |
| ApiCompat | Future breaking-change baseline checks | Premature before stable baseline/package API exists. |
| Verify snapshots | Future approved API/report baselines | Avoid until churn is low enough to make snapshot diffs meaningful. |

## Current Command Evidence

Typical local evidence set:

```pwsh
# Regenerate the binding output (per active toolchain — see binding-generator-maintenance.md).
# Build per-family csprojs to confirm multi-TFM compile.
dotnet build spikes/binding-generators/clangsharp/src/Janset.SDL2.Core/Janset.SDL2.Core.csproj -c Release
# Run the spike's ABI tests project for raw-ABI shape verification.
dotnet test spikes/binding-generators/clangsharp/tests/abi-tests/AbiTests.csproj -c Release --framework net10.0
# Regenerate the oracle evidence report.
dotnet run --file spikes/binding-generators/clangsharp/oracle.cs
slopwatch analyze --fail-on warning --exclude "spikes/binding-generators/references/**,external/**,vcpkg_installed/**,**/bin/**,**/obj/**"
git --no-pager diff --check
```

Run Docker-mounted Linux fixture/parser checks when validating parser/header behavior that Windows skips. Use the spike's Docker image built from [`docker/binding-generator.Dockerfile`](../../../../docker/binding-generator.Dockerfile) and the spike's ABI-tests test filter analogous to what was previously the Cake-host semantic-header fixture filter (confirm the active image tag and test filter from the maintenance playbook before invoking).

## Report Template

Use this shape for the final oracle validation report:

Placeholder values in this template are filled by the aggregator agent for a concrete validation run.

```md
# SDL2.<Family> Output Oracle Validation Report

Date: YYYY-MM-DD
Generated output: spikes/binding-generators/clangsharp/src/Janset.SDL2.<Family>/Generated
SDL2 version: X.Y.Z
Regeneration evidence: <command(s) used to regenerate; see binding-generator-maintenance.md>
Build / ABI-test result: <multi-TFM build summary + AbiTests outcome>

## Summary

- Go/no-go: <go | no-go | conditional>
- Hard bugs: N
- Likely bugs: N
- Compatibility risks: N
- Follow-ups: N

## Hard Bugs

| Symbol | Evidence | Why It Matters | Recommended Action |
| --- | --- | --- | --- |

## Likely Bugs

| Symbol | Evidence | Why It Matters | Recommended Action |
| --- | --- | --- | --- |

## Compatibility Risks

| Symbol | Evidence | Risk | Decision Needed |
| --- | --- | --- | --- |

## Accepted Deltas

| Delta | Rationale | Future Suppression? |
| --- | --- | --- |

## Follow-ups

| Item | Owner/Issue | Trigger |
| --- | --- | --- |

## Lane Evidence

- Official SDL/header lane: <link or summary>
- Dynapi/export lane: <link or summary>
- Peer binding lane: <link or summary>
- Generated .NET API lane: <link or summary>
- Macro/internal leak lane: <link or summary>
```

## When To Build A Small Internal Tool

Do not start with a custom validator. Start with this playbook and parallel agents.

`oracle.cs` is the existing realization of this discipline for narrow facts: it loads generated assemblies/source, emits compact per-family category/macro/opaque-handle summaries, joins generated functions with dynapi facts, and produces the markdown report `oracle-evidence-clangsharp.md` for agents to review.

Build additional small internal report tools only when at least two review runs repeat the same manual extraction. Good candidates beyond what `oracle.cs` already covers:

- per-TFM public API extraction to JSON (compile + reflect across the target TFM set);
- Roslyn source checks for public raw container and effective public extern leaks beyond the spike's existing postprocess rewriters;
- platform-pass-specific evidence consolidation across native-Linux and native-macOS runners;
- merged finding aggregators that combine oracle output with peer-binding cross-checks.

Keep each new tool evidence-first. Promote individual checks to fail gates only after their failure mode is precise and low-noise.

## Common Mistakes

- Treating SDL2-CS as truth. It is useful, old, and sometimes wrong.
- Treating SDL3 peer output as SDL2 ABI evidence. It is mostly conceptual/style evidence for this stage.
- Treating dynapi as signature validation. It proves names only.
- Treating `oracle-evidence-clangsharp.md` as an upstream oracle. It is local-generator provenance, not independent truth.
- Flattening all deltas into bugs. Some are intentional typed-handle/string/span/API-shape decisions.
- Skipping embedded `.h` fixture coverage when changing parser or macro evaluator behavior.
- Building a large validator before knowing which findings are stable enough to automate.
- Treating apparent evidence-report disagreement on callback typedefs as a generator bug before confirming against pinned headers — callback-typedef inspection is a known evidence-tool coverage gap (see "Tooling Guidance").

## Related Docs

- [`binding-generator-constitution.md`](binding-generator-constitution.md)
- [`binding-generator-roadmap.md`](binding-generator-roadmap.md)
- [`testing-strategy.md`](testing-strategy.md)
- [`binding-generator-maintenance.md`](binding-generator-maintenance.md)
