# SDL_bool as an ABI-Exact Struct

Date: 2026-11-05

Represent `SDL_bool` as an ABI-exact struct with implicit conversion to and from `bool`.
For SDL2 its storage is int-sized. This keeps native layout exact while permitting normal .NET
boolean use without a separate predicate-overload layer.

## Consequences

This supersedes the int-backed enum rule in the [ABI constitution](../architecture/binding-abi-policy.md)
and the earlier public-projection design's prohibition of boolean conversions.
Only native `SDL_bool` types receive this meaning; do not infer booleans from arbitrary integer returns.
Evidence: internal research, 2026-05-29, "Layer 2 typed public API design".
The reversal follows peer evidence for ABI-exact boolean wrappers: internal research, 2026-10-12,
"How peer .NET binding libraries layer their API surface, and what it means for the Janset.SDL2 collapse proposal".
