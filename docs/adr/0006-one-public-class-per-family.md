# One Public Class per Family

Date: 2026-11-05

Emit one public `static unsafe partial class` per family. Public, C-named, typed P/Invokes form the
binding tier; friendly overloads live in the same class. Use a private extern behind a public managed
method only when that member's public shape cannot itself be the extern, such as platform-varying C `long`.

## Considered options

Keeping an internal raw container plus a public projection duplicates most signatures without adding
behaviour. The reviewed Core output had 834 of 861 public methods as one-line forwarders; Modern had
854 externs and Compat 856, with projectable identity differences of 0/0. These are historical census
figures, not current test totals.

The separate projection also obscured real ABI differences: projected `long` passed to Compat's
`TTF_OpenFontIndex` `int index` is a CS1503 hazard. A friendly `string? SDL_GetError()` beside a
`byte* SDL_GetError()` is CS0111 because return type alone cannot distinguish C# overloads.

Evidence: internal research, 2026-10-06, "Layer census: L1 raw ABI vs L2 public projection (Core, plus satellite simulation)";
internal research, 2026-10-06, "Layer debate ruling (concern 3): drop the separate L2 class, but keep the per-member ABI hiding rule";
internal research, 2026-05-29, "Layer 2 peer mapping".

## Consequences

- The managed string form owns the C name; the `byte*` form, parameter or return, uses `<CName>Ptr`.
- Two overloads with the same name and arity must not both accept `null` at the same position.
- Generation fails if an emitted name collides with a native symbol. Native names otherwise remain unchanged.
- Separate managed-text and pre-encoded UTF-8 forms keep allocation visible, without a unifying wrapper
  or a low-value UTF-16 span conversion. A `ReadOnlySpan<byte>` sibling applies only to parameters that
  are non-nullable in C. Recognizing an existing terminator avoids a copy; a bounded copy otherwise
  supplies the terminator without reading past caller-owned memory.
- Converting eligible parameters together avoids a cartesian overload set. Nullable value-type defaults
  cover optional struct-pointer combinations without heap allocation. This makes safe alternatives
  practical without charging pointer callers for opt-in string conveniences.
- ClangSharp does not emit function-like macro bodies. A curated symbol/kind roster and reusable
  per-kind emitters separate family facts from policy; irregular hand-authored forms remain a last resort.
- Preprocessing erases endian conditionals. Differential parsing recovers an auditable roster;
  runtime aliases retain byte-order meaning, while enum members need fixed values. The supported
  little-endian platforms make the `PIXELFORMAT_*32` enum choice valid.
- Satellite error macros already alias Core functions, so convention-derived redirects avoid a second
  mapping roster. Satellites call Core's public surface; `InternalsVisibleTo` is granted only to test assemblies.
  Sharing Core-owned types avoids competing handle and struct identities.
- Family-blind mechanisms avoid Core-only assumptions. Empty work sets occur in real satellites and
  should add no machinery. Core's return-only C `long` cases cannot prove TTF's input range checks;
  Gfx's structs expose missing layout attributes. Cross-family handles, struct pointers, endian aliases
  and mixed prefixed/unprefixed names motivate satellite-case evidence before activation.

The class collapse does not justify flattening ABI differences. Per-member hiding remains necessary;
uniform forwarding wrappers do not. The [ABI constitution](../../spikes/binding-generators/docs/canonical/binding-generator-constitution.md)
owns the detailed string, macro, endian, cross-family and layout rules. The rationale above explains
those choices rather than defining a second policy home.
