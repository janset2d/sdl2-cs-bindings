# Agent Discipline

**Status:** Active reference for any LLM agent working in this repo.
**Effective from:** 2026-05-17.
**Read order:** `AGENTS.md` → this doc → task-specific docs.

## Why this exists

Mid-Phase 3C (binding-autogen workstream, May 2026) a recurring discipline-defect pattern emerged across multiple turns. Decisions were being made without research, "verification" was being claimed without inspecting actual evidence, and scope creep was happening without approval. The maintainer's trust dropped sharply — visible in his Turkish/English bilingual feedback during the session (`docs/superpowers/plans/2026-05-17-binding-generator-unified-plan.md` Phase 3B/3C area + `.github/prompts/binding-autogen-stage-1-phase-3c-and-3d-prime-handover.prompt.md` retrospective).

This document captures the four disciplines that prevent the same failures, with concrete examples drawn from the actual defects. It is not aspirational — every example below is a real failure that landed in code or doc and had to be backed out.

## The four disciplines

### 1. Research before deciding

When the situation calls for a design choice — pick a library, name a parameter, choose between two emit shapes, pick a peer pattern — **the first action is research, not decision**.

Research inputs, in order:

1. **Existing codebase** — grep for prior decisions, ADRs, knowledge-base, manifest entries
2. **Peer projects** — the projects this repo defines as architectural peers (`amerkoleci/Alimer.Bindings.SDL`, `ppy/SDL3-CS`, `flibitijibibo/SDL2-CS`, `dotnet/Silk.NET`, `dotnet/ClangSharp.PInvokeGenerator`, `mono/SkiaSharp`)
3. **Microsoft docs** — official guidance for the .NET / C# / P/Invoke / Roslyn / analyzers concern at hand
4. **Analyzer rules** — what does the current analyzer config enforce, what additional rules exist for the surface being changed

**Anti-patterns observed:**

- *"Microsoft.CodeAnalysis.CSharp paketi 3 MB overkill, hardcoded FrozenSet yapayım."*
  — Source: Phase 3C, May 2026. Decision was made without checking the actual package size (turned out to be 22.81 MB compressed, lighter dep tree on .NET 10), the actual peer pattern (Alimer + ClangSharpPInvokeGenerator + Silk.NET all use hardcoded keyword lists — the decision was correct, but the **reasoning** was a fabricated number; peer-validated outcome reached via wrong path).
  — The right path: WebFetch the package details, search peer repos for `EscapeName` / `SafeIdentifier` implementations, then form the decision.

- *"C-variadic functions emit broken P/Invoke stubs, must exclude in `KnownUnsupportedDeclarationPolicy.IsVariadic`."*
  — Source: Phase 3C, May 2026. The emit shape `void SDL_Log(sbyte* fmt)` was interpreted as broken without checking what SDL2-CS (hand-written reference binding) does. SDL2-CS literally writes `/* Use string.Format for arglists */` and emits the same fmt-only shape with a managed wrapper. Alimer.Bindings.SDL (our CppAst SDL3 architectural mirror) emits the same shape. ppy/SDL3-CS adds `__arglist` (ClangSharp default) but loses variadic capacity across friendly overloads. The "broken" framing was wrong — fmt-only is the de-facto SDL-family pattern.
  — The right path: peer evidence first, decision second.

### 2. Validate output shape — counts alone don't prove preservation

When the work claims "behavior preserved" or "output unchanged":

- **Per-view counts byte-identical** ≠ output files byte-identical
- **Test pass count unchanged** ≠ assertions exercise the same surface
- **Smoke "succeeded"** ≠ the result is compile-valid

Required verification before claiming preservation:

1. `grep` for specific known-tricky outputs (variadic function emits, pointer parameters, namespace declarations, class headers)
2. `head` / `tail` the relevant generated files and visually compare to expected shape
3. Build the output if a compile gate exists; if not, treat the claim as **unverified**

**Anti-pattern observed:**

- *"Phase 3C container smoke 4m 33s; per-view function counts byte-identical to Phase 3B baseline."*
  — Source: Phase 3C, May 2026. The maintainer asked "did you actually verify variadics in the output?" — the agent had not. A grep of the smoke output revealed all 12 variadic functions emitting (correct, peer-pattern), but the broader question exposed a separate defect: the emit shape used pointer parameters (`sbyte* fmt`) without `unsafe` modifier on the class header. This was a **Phase 3A regression** (P3.10 `unsafe` drop, made on the false premise that the emit had no unsafe operations). The defect had been latent across Phases 3A → 3B → 3C because the smoke target emits files but does **not compile them**. The "byte-identical preservation" claim was technically true (same broken state preserved across phases) but semantically misleading.
  — The right path: open the generated file, look at the first 20 lines, check the class header, look for `unsafe`, check parameter signatures, **then** claim preservation.

### 3. Ask before scope-creeping

Any decision that:

- expands beyond the agreed slice scope (Phase 3C said "lift and shift, no behavior change" — variadic exclusion would have been a behavior change)
- introduces an architectural pivot (e.g., changing a class from instance to static when the spec drew it as instance)
- adds a new dependency (NuGet package, analyzer rule, framework abstraction)
- changes the manifest schema or any file under explicit AGENTS.md approval gate

**must surface the decision to the maintainer first**, with peer evidence + tradeoff analysis + recommendation. The maintainer's "go / yap / başla / apply" is the gate.

**Anti-pattern observed:**

- *"Variadic emit is broken; I'll add `IsVariadic` to `KnownUnsupportedDeclarationPolicy` while doing the Phase 3C extraction."*
  — Source: Phase 3C, May 2026. This added a real behavior change (12 fewer emitted functions) inside a slice that had been explicitly framed as behavior-preserving. The maintainer caught it mid-implementation: "hop dur lan bana sormadan ne iş yapıyorsun, variadic bug ne ne yapıyorsun anlamadım."
  — The right path: notice the scope expansion *before* writing the code; surface the question (what is the variadic emit, what do peers do, do we filter or pass through?) with the evidence collected, and wait for the call.

### 4. Verification-before-completion — back every "succeeded" claim with evidence

A "succeeded" claim requires:

| Claim | Evidence required |
|---|---|
| `dotnet build` clean | Build output: `0 Warning(s)`, `0 Error(s)`, total elapsed |
| `dotnet test` passes | Test summary block: `total: N`, `failed: 0`, before/after delta if behavior-preserving |
| `slopwatch analyze` clean | Output line: `Scan complete: 0 issue(s) found` |
| Container smoke succeeded | Per-view function counts + `head`/`tail` of representative output file + `grep` for known-tricky symbols (variadic emits, pointer parameter signatures, namespace) |
| "Output byte-identical to baseline" | If both outputs are available, `diff -r`; if not, structural inspection of representative files |
| "Peer pattern matches" | Quoted code excerpt from peer source with file path + line numbers OR URL |

If the evidence cannot be produced, the claim is not yet earned. State that explicitly: "build green, but I haven't verified emit shape" — not "all gates passed."

## Pattern recognition — red flags during work

When any of these thoughts appear, pause and re-route:

- "X is overkill / too heavy / not worth it." → Numbers? Sources? Peer choices?
- "This is decorative / unused / can be dropped." → Is the assumption that it's unused verified by inspection?
- "The spec says X but it'd be cleaner if Y." → That's a scope expansion. Surface to maintainer.
- "Counts match, we're good." → Did the *content* shape match too?
- "Smoke succeeded." → Did it compile, or did it just emit files?
- "Tests pass, but the surface I changed didn't have tests." → That's an opportunity to write a test, not a green light.

## Companion docs

- `AGENTS.md` §Approval Gate (Hard Rule)
- `MEMORY.md` (auto-memory feedback entries: "No workarounds/shortcuts", "Follow explicit paths", "No timing pressure or motive assumptions", "Verification before completion")
- `docs/knowledge-base/extraction-guidelines.md` §LLM-specific note
- `docs/superpowers/specs/2026-05-16-binding-generator-unified-design.md` §8.1 (identity vs category split — peer-evidence framing)
- `docs/superpowers/plans/2026-05-17-binding-generator-unified-plan.md` §Phase 3C design intent block

## Concrete lessons from the 2026-05-17 session

These are the actual mistakes that led to this document. Future agents: read these so the same failures don't repeat.

| Phase | Mistake | Consequence | Right action |
|---|---|---|---|
| **3A** | Dropped `unsafe partial class` modifier on the emitted class header (P3.10), claiming current emit has no unsafe operations. **Did not inspect parameter signatures**, which had `sbyte*` pointer parameters that require `unsafe` context. | Emitted code does not compile. Latent across Phases 3A → 3B → 3C because the smoke target does not compile output. Maintainer caught it during Phase 3C review. | Inspect emit string output, search for `*` in parameter types, check peer header conventions (Alimer + ppy: class-level `unsafe`; SDL2-CS: method-level `unsafe`). Only then decide. |
| **3C start** | Diagnosed variadic emit as "broken P/Invoke stubs" and added `function.IsVariadic` filter to `KnownUnsupportedDeclarationPolicy` without checking peer behavior. | Scope creep into "lift and shift" slice. Behavior change of −12 emitted functions snuck in. Maintainer stopped the work: "sormadan ne iş yapıyorsun". | Peer evidence first: SDL2-CS + Alimer both emit fmt-only; pattern is documented in their source. Surface the question + evidence to maintainer; wait for the call. |
| **3C start** | Decided to use hardcoded `FrozenSet` for `SafeIdentifier` keyword list with the reasoning "Microsoft.CodeAnalysis.CSharp is 3 MB overkill." | The outcome was correct (hardcoded list is peer-validated) but the reasoning was fabricated — actual package is 22.81 MB compressed, dep tree is light on .NET 10, and the **real** justification is peer consistency (Alimer + ClangSharpPInvokeGenerator + Silk.NET all use hardcoded sets). Maintainer pushed back: "belki ben git codeanalysis e ekle diyeceğim. Gidip internetten best practiceleri araştırdın mı, peer'larımız ne yapmış baktın mı sikik". | Research the actual package details + peer behavior first. Reach the same decision via the right evidence. |
| **3C smoke** | Claimed "container smoke 4m 33s, per-view counts byte-identical to Phase 3B baseline" as the completion gate. | Counts were identical but the *shape* of the emitted file (the Phase 3A `unsafe` defect) was unchecked. Maintainer asked: "smoke çalıştırdın mı variadicleri doğruladın mı outputtan" — the answer should have been verifiable structurally, not just counts. | Open the generated file, grep for variadic emits, check the class header for `unsafe`, look at representative pointer-parameter signatures. **Then** claim preservation. |

## How to apply this doc

When picking up new work in this repo:

1. Read `AGENTS.md` first
2. Read this doc second
3. Read the task-specific spec/plan/handover
4. Before any non-trivial decision: research → peer evidence → propose → wait for the maintainer
5. Before any "succeeded" claim: evidence → verification → state explicitly what's verified vs. what's not
6. When in doubt: ask. The cost of a question is low; the cost of a wrong silent decision is what this document exists to prevent.
