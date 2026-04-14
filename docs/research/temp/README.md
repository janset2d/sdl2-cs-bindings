# Temp Research Workspace

**Status:** Decision cycle completed (2026-04-14). All working documents have been resolved.

## What Happened

The April 2026 packaging decision cycle evaluated hybrid static vs. pure dynamic packaging strategies. Eight verdicts from six different LLM models/authors were synthesized into a decision.

**Decision:** Hybrid Static + Dynamic Core, LGPL-free codec stack, custom vcpkg overlay triplets.

## Promoted Documents

The following documents were promoted to `docs/research/` (canonical):

| Document | Promoted As | Reason |
| --- | --- | --- |
| `packaging-strategy-synthesis-2026-04-13-copilot.md` | Same name in `research/` | Primary synthesis — consolidates all verdict inputs |
| `packaging-strategy-verdict-2026-04-14-claude-2.md` | Same name in `research/` | Unique technical corrections (symbol visibility, minimp3, arm64 triplets) |
| `execution-model-strategy-2026-04-13-shared.md` | `execution-model-strategy-2026-04-13.md` in `research/` | Canonical execution model (Source / Package Validation / Release modes) |

## Deleted Documents

The following documents were deleted (content fully subsumed by the synthesis and decisions made):

- `packaging-strategy-verdict-2026-04-13-claude.md`
- `packaging-strategy-verdict-2026-04-13-chatgpt.md`
- `packaging-strategy-verdict-2026-04-13-shared.md`
- `packaging-strategy-verdict-2026-04-13-gemini.md`
- `packaging-strategy-verdict-2026-04-13-grok.md`

## This Folder Going Forward

This folder can be reused for future decision cycles. Follow the contribution guidelines and naming conventions documented in the original README (preserved in git history).
