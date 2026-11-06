# No SafeHandle Types in v1

Date: 2026-11-05

The v1 binding exposes only `nint`-backed handle structs, not SafeHandle owner types.
Lifetime and ownership belong to Janset2D. This keeps the binding a low-level native interface,
not a high-level SDL object model.

## Considered options

Owning wrappers could centralize disposal, but they add lifetime policy beyond the binding's remit.
Evidence: internal research, 2026-05-22, "Semantic ABI type classification" places SafeHandle in later
owner wrappers rather than in ABI handle declarations.

## Reversal trigger

A v2 owner-wrapper design may reopen this decision. It is not an implicit extension of v1 handles.
