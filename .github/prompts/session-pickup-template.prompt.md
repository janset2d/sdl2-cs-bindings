# Session Pickup Prompt — Template

> **How to use:** at the end of a session that closed a non-trivial wave, copy this file to `s<N>-<short-summary>.prompt.md` (e.g. `s23-p5-naming-cleanup.prompt.md`). Replace every `{{placeholder}}` with session-specific content. Drop any section that has no content. Length is not a virtue — useful pickups are 100-200 lines, not 250+.
>
> **When NOT to write a pickup:** doc-only edits, single-bug fixes, or routine refreshes. Pickups are for handing off **architectural waves, multi-commit refactors, or genuinely stateful in-flight work** that the next session needs primed context to continue.

---

## Frontmatter (replace every field)

```yaml
---
name: "S{{N}} {{short-summary-of-what-just-closed}}"
description: "Priming prompt for the next agent entering janset2d/sdl2-cs-bindings after {{wave-or-task}} landed at {{sha}} on {{date}}. {{one-sentence-state-summary}}. Recommended next: {{A | B | C}}."
argument-hint: "Optional focus area, constraints, or reason to override the recommended next step"
agent: "agent"
model: "{{model identifier — e.g. Claude Opus 4.7 (1M context)}}"
---
```

---

## Body skeleton

### Opening paragraph

One paragraph stating: who you are (engineer entering the repo), what just landed (wave + sha + date), and the one most important state observation. No fluff.

### `## First Principle`

> Treat every claim here as **current-as-of-authoring (`{{date}}` — `{{wave-tag}}`)** and verify against the live repo, git log, and canonical docs before acting.

This block stays nearly verbatim across pickups — it's the standing reminder that pickup prompts go stale.

### `## What Just Happened`

The detailed change ledger. Use sub-headers for distinct waves / concerns. For each:

- **What changed** — file paths, signatures, removed / added types
- **Why** — 1-2 lines anchored to ADR / phase doc / guardrail
- **Verification** — test count, smoke baseline state, cross-platform witness

For mechanical changes (interface removals, signature cut-overs, rename waves) prefer **tables**: more dense than prose, easier for the next agent to skim. Include any inline deferrals or known carry-forward items at the end of each sub-section.

This is the longest section. Don't pad it; do not omit anything load-bearing for the next agent.

### `## Onboarding Snapshot` *(optional — drop if the previous pickup already covered it well and nothing shifted)*

Quick re-orientation. Brief stack reminder, RID coverage, the locked-decisions list. **Most of this is durable across pickups** — copy from the previous pickup and adjust where things shifted. Don't re-derive from scratch every session.

### `## Current State You Should Assume Until Verified`

A short bullet list with concrete current values:

- **Master HEAD**: `{{sha}}` — `{{commit-summary}}`
- **Worktree expectation**: `{{clean | unstaged X | uncommitted-doc-sweep}}`
- **Build-host tests**: `{{N/N passed / M skipped}}`
- **Behaviour signal (smoke-witness baselines)**: `{{per-platform results if relevant — drop if no smoke baseline was committed}}`
- **`{{ArchitectureTests | other invariants}}`**: `{{state}}`
- **`{{Phase X / parallel work}}`**: `{{state}}`

Always verifiable, always specific. Vague status entries are worse than absent ones.

### `## Recommended Next Step`

1-3 numbered options. Each option:

- **Name + classification** — e.g. "lightweight, well-scoped" / "multi-session arc" / "optional"
- **Pre-flight steps** — what to read / verify before starting
- **Specific files / sections to touch** — concrete paths
- **Acceptance criteria** — what "done" looks like

End with: "Talk to Deniz before committing to which one." Master-direct commits are the default; do not branch unless there is a concrete reason.

### `## Mandatory Grounding (read in this order)`

A numbered read order. Adjust per scope, but the canonical core stays:

1. `docs/onboarding.md`
2. `AGENTS.md`
3. `CLAUDE.md`
4. `docs/plan.md`
5. `docs/phases/{{active-phase-doc}}`
6. `{{any active ADRs in docs/decisions/}}`
7. `docs/knowledge-base/release-guardrails.md`
8. `{{playbooks specific to scope}}`
9. `{{test fixtures / architecture tests if changing them}}`

Skip entries the next session does not need; do not pad with everything.

### `## Locked Policy Recap`

Curated invariants list. Most carry over verbatim from session to session. Reference `AGENTS.md` "Settled Strategic Decisions" — this section is for the **most-likely-to-be-tempting-to-violate** rules in the upcoming work, not a full mirror of AGENTS.md.

Examples of invariants worth re-stating in a pickup:

- Master-direct commits / no commit without explicit "go / yap / apply / proceed / başla"
- Cake owns orchestration policy; YAML stays thin
- `BuildContext` is invocation state, not a service locator
- `Tools/` is Cake `Tool<TSettings>` wrappers ONLY
- Test naming convention
- Living-docs rule

### `## Final Steering Note`

1-2 paragraphs. Closing direction. Hint at the natural rhythm for the next session — not a hard mandate. End with something like "Hold the line." or "The build host has never been in better shape." — short, specific, motivating.

---

## Drift sensitivity per section

| Section | Drift sensitivity |
| --- | --- |
| Frontmatter | Session-specific — rewrite |
| Opening paragraph | Session-specific — rewrite |
| First Principle | Stable |
| What Just Happened | Session-specific — rewrite |
| Onboarding Snapshot | Mostly stable; adjust per shift |
| Current State | Session-specific — rewrite |
| Recommended Next Step | Session-specific — rewrite |
| Mandatory Grounding | Stable; adjust if doc topology shifts |
| Locked Policy Recap | Stable; copy from `AGENTS.md` "Settled Strategic Decisions" |
| Final Steering Note | Session-specific — rewrite |

When the doc topology shifts (renamed phase doc, new ADR, retired playbook), update Mandatory Grounding in this template too — that's the entry point the next pickup author copies from.

---

## Authoring discipline

- **Verify before claiming.** Don't write "X is at Y state" without `git log --oneline -5` + a quick repo grep. Pickup prompts are read by future agents who will treat your claims as current.
- **Anchor to commit SHAs.** Every "just landed" claim should reference a specific commit.
- **Drop sections that don't apply.** A pickup with no smoke baselines should not have a "smoke-witness baselines: TBD" line — drop the bullet.
- **Don't re-derive AGENTS.md.** If the next session needs it, point at it; do not paraphrase.
- **Sign off with a concrete next-action recommendation, not five parallel futures.** Default + alternatives, not buffet.
