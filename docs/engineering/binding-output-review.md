# Binding Output Review

Date: 2026-11-06

Use this workflow after regeneration, a header/tool pin change, a public projection change or an
unexpected oracle/dynapi delta. It answers whether the generated .NET surface is correct, not merely
compilable. It complements [testing](binding-testing.md), never replaces compile, fixture or runtime
evidence. [ABI policy](../architecture/binding-abi-policy.md) owns the target shape.

## Source hierarchy

| Source | Proves | Does not prove |
|---|---|---|
| Exact pinned public headers | Declarations, signatures, typedefs, constants, layouts and conditional surface | Runtime exports or .NET ergonomics |
| Actual packaged native binaries | Shipped export names | Parameter order, widths, structs, constants or callbacks |
| SDL2.exports | Core function-name existence | Signatures, satellites or SDL3 |
| SDL_dynapi_procs.h | Secondary Core signature/name evidence | Managed API design |
| SDL wiki | Ownership, UTF-8, lifetime, threading and version semantics | Exhaustive ABI coverage |
| Curated peer bindings | Compatibility signals, concepts and ergonomics | Independent correctness authority |
| [Oracle report](../../spikes/binding-generators/output/reports/oracle-evidence-clangsharp.md) | What this generator parsed/emitted/skipped | Independent upstream truth |

Record exact versions, commits and source URLs. Do not compare a pin against moving upstream main
and call the difference a binding bug. Normalize typed handles versus IntPtr, pointer versus string,
enum names, platform conditions and public wrappers before comparing peers.

Input inventory is [generated family projects and sources](../../spikes/binding-generators/clangsharp/src/),
the oracle report, installed pinned headers, native exports and
[vendored SDL2-CS](../../external/sdl2-cs/). Installed headers/buildtrees are local evidence under
`vcpkg_installed/` and the vcpkg buildtree, not a mandatory reference clone dependency.
Read current file paths from generation config and [maintenance](binding-generator-maintenance.md).

## Evidence collection and interpretation

Automate narrow facts: compile results, effective accessibility, symbol names, file presence, schema
shape and per-TFM snapshots. Public C-named imports form the binding tier; private imports appear
only for per-member ABI demotion. Inspect their effective accessibility and public signatures.

Check required surface, actual exports, explicit macro skips, consumed overrides and justified stale
exceptions. Names from dynapi never prove ABI. Review strings, ownership, callback lifetimes and
platform-conditioned layouts against headers and semantic documentation.

The [oracle app](../../spikes/binding-generators/clangsharp/oracle.cs) is evidence extraction, not an
independent oracle. Confirm callback typedef findings against headers and generated declarations;
do not let an evidence-tool gap decide API correctness. Missing evidence remains missing.

### Curated peers

| Peer | Use | Limit |
|---|---|---|
| [SDL2-CS](../../external/sdl2-cs/) | SDL2 migration signals and legacy functions/constants/callbacks | Old; its IntPtr/string conventions do not define this product |
| [ppy SDL3-CS](https://github.com/ppy/SDL3-CS) | Generator, platform passes and friendly concepts | SDL3, not SDL2 ABI truth |
| [Alimer](https://github.com/amerkoleci/Alimer.Bindings.SDL) | Typed handles and generator design | Different coverage and platform strategy |
| [Silk.NET](https://github.com/dotnet/Silk.NET) | Generated interop scale and span/ref patterns | Not SDL2 semantic authority |
| [Rust-SDL2 sys](https://github.com/Rust-SDL2/rust-sdl2/tree/master/sdl2-sys) | Header-generated raw and satellite cross-checks | Rust choices and package lag need normalization |
| [Rust-SDL2 wrapper](https://github.com/Rust-SDL2/rust-sdl2) | Ownership and callback design signals | High-level API hides ABI detail |
| [go-sdl2](https://github.com/veandco/go-sdl2) | Functions, constants and satellite exposure | cgo and maintenance/version differences |
| [BindBC SDL2](https://github.com/BindBC/bindbc-sdl/tree/SDL2) | Version gates, loaders and satellite constants | D loader model differs |
| [PySDL2](https://github.com/py-sdl/py-sdl2) | Broad function/macro presence | ctypes flattening is not layout proof |

Use [Fermium](https://github.com/Lokathor/fermium), [haskell-game/sdl2](https://github.com/haskell-game/sdl2)
or [sdl2_nim](https://github.com/Vladar4/sdl2_nim) only for a specific unresolved concept. Record
staleness and host-language limits. Do not add abandoned forks, demos, obsolete satellite crates,
DerelictSDL2 or pygame as routine direct binding oracles. Adoption, maintenance, version proximity
and licensing decide whether another peer earns a place; popularity alone does not.

## Finding taxonomy

| Classification | Meaning and action |
|---|---|
| Hard Bug | Contradicts pinned headers, exports, exact ABI or settled API policy; fix before promotion |
| Likely Bug | Strong evidence with an ambiguous source; investigate or add targeted proof |
| Compatibility Risk | Migration/user expectation difference, not necessarily wrong; decide and record |
| Design Signal | A conceptual improvement, not a correctness verdict |
| Accepted Delta | Intentional difference with current policy rationale; retain that rationale in the report |
| Follow-up | Real work outside the slice; file/link an issue with its trigger |

A cross-language peer alone cannot establish Hard Bug. Mechanical Accepted Deferral maps to a
review Accepted Delta for explicitly deferred policy. Mechanical Evidence Missing and Out Of Scope
are evidence states, not votes for approval. [Generation](../architecture/binding-generation.md#oracle-evidence-labels)
owns those mechanical labels.

## Review lanes

Independent reviews may run in parallel when no shared edits are needed. Give each reviewer exact
repository commit, family, generated paths, pins, native payload and report inputs. Reviews are
read-only unless the brief explicitly authorizes edits.

| Lane | Required checks and output |
|---|---|
| Official headers/docs | Sample functions and high-risk types, constants/macros, platform attribution, UTF-8/ownership/freeing; exact symbols and source references |
| Dynapi/exports | Emitted-extra and missing-name sets, accepted exclusions, missing binary evidence and open questions; do not infer signatures |
| Peers | Normalize shape before comparing; classify compatibility/design signals and confirm suspected bugs against authoritative inputs |
| Public .NET API | Compile every library TFM; inspect effective accessibility, public signatures, private ABI demotion, typed handles, callbacks, constants and platform attributes |
| Macros/internal details | Hunt build toggles, assertions, printf/cast/compiler controls and fake constants; distinguish deterministic values from helper candidates |
| Aggregation | Merge duplicates, resolve conflicts against source hierarchy, classify every finding and give go/no-go with unresolved blockers |

Every Hard/Likely Bug names symbol/type/member, evidence, generated source/report location, next
action and the fixture/runtime proof needed. Do not recommend an API change solely to match a peer.

## Tools and commands

Use source scans for a quick inventory, then Roslyn/reflection for effective public surface. Use
PublicApiGenerator and Verify for reviewed per-TFM API snapshots. ApiCompat supplies compatibility
evidence when an approved baseline exists; it is not a substitute for ABI tests or policy review.
Snapshot diffs are review inputs, not an invitation to approve received output blindly.

```pwsh
dotnet build spikes/binding-generators/clangsharp/src/Janset.SDL2.Core/Janset.SDL2.Core.csproj -c Release
dotnet test --project spikes/binding-generators/clangsharp/tests/abi-tests/AbiTests.csproj -c Release --framework net10.0
dotnet run --file spikes/binding-generators/clangsharp/oracle.cs -- --family sdl2-core --write-report
git diff --check
```

[Maintenance](binding-generator-maintenance.md) owns regeneration and the baked-image Linux test
recipe. Rebuild the image and COPY sources; never use a Windows checkout bind mount for vcpkg or
native toolchain work. [Quality gates](quality-gates.md) owns the exact config-mirrored slopwatch command.

Build a new evidence extractor only when at least two reviews repeat the same costly manual step.
Keep it small and evidence-first. A new fail gate needs a precise, low-noise failure contract and
tests; broad API judgment remains review-led. Candidates include per-TFM API extraction, narrow
accessibility checks, platform evidence consolidation and finding aggregation.

## Report

Record date, commit, family, source pins, complete regeneration commands, build/ABI results and
skips. Include a concise go/no-go and counts for each taxonomy category, followed by findings with
exact symbols, source refs, risk and recommended action. Accepted deltas carry rationale; follow-ups
carry issue/owner and trigger. Attach each lane's evidence and list missing inputs explicitly.

Do not flatten all deltas into bugs, treat dynapi as signatures, skip header fixtures when changing
parsing, or let the local report validate itself. Compile, runtime, upstream and managed API evidence
answer different questions.

## Reference-clone appendix

Reference clones are gitignored local evidence, not submodules, vendored dependencies or runtime test
inputs. Clone from the repository root and checkout a recorded commit before comparing. Selective
copying requires source/license review. Never commit the clone directory.

```pwsh
New-Item -ItemType Directory -Force spikes/binding-generators/references
git clone https://github.com/ppy/SDL3-CS.git spikes/binding-generators/references/ppy-SDL3-CS
git clone https://github.com/amerkoleci/Alimer.Bindings.SDL.git spikes/binding-generators/references/alimer-bindings-sdl
git clone https://github.com/libsdl-org/SDL.git spikes/binding-generators/references/SDL
git clone https://github.com/dotnet/Silk.NET.git spikes/binding-generators/references/silk-net
git -C spikes/binding-generators/references/ppy-SDL3-CS checkout 442a5a2346600f574f654e95a12d6bf4305a4732
git -C spikes/binding-generators/references/alimer-bindings-sdl checkout 74abceec054534406776c5b216551f9b184475ee
git -C spikes/binding-generators/references/SDL checkout cee83f2d9a916e9de30c5712e798541fdc134b06
git -C spikes/binding-generators/references/silk-net checkout fd0c2871dd08019b9e6a166f13f7bfe44fb4f11a
```

These comparison refs were read with git ls-remote on 2026-11-06 (SDL2 branch for SDL; HEAD for peers).
They are comparison pins, **not** the authoritative SDL release input: resolve that from the native
manifest and exact installed headers. Refresh a comparison pin only with the review that uses it.
Inspect ppy's generator/RSPs, Alimer's generator, SDL's headers/tests/dynapi and Silk.NET's SDL
generation/output. Do not infer source location from an old peer layout without checking the pin.
