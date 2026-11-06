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

- The `string?` return form owns the C name; the byte-pointer form uses `<CName>Ptr`.
- Two overloads with the same name and arity must not both accept `null` at the same position.
- Generation fails if an emitted name collides with a native symbol. Native names otherwise remain unchanged.
- String inputs have separate `string` and `ReadOnlySpan<byte>` overloads, not a unified UTF-8 wrapper
  or UTF-16 span. Byte spans are zero-copy when already null-terminated; otherwise append a terminator
  in a bounded stack or pooled copy without reading past the span.
- Friendly forms convert eligible parameters together, rather than emitting a cartesian overload set.
  Optional struct pointers use nullable value types with defaults. Public release requires a safe
  alternative to caller-held pointer forms; opt-in string conveniences may allocate.
- Function-like macros use a family config roster for which symbols and kinds exist, with code-owned
  per-kind emitters for how they work. Irregular forms require an explicit last-resort rationale.
- Differential endian parsing seeds an audited roster. Standalone aliases use runtime byte order;
  endian-conditioned `PIXELFORMAT_*32` enum members remain little-endian for the supported platforms.
- Satellite error macros derive redirects by convention to Core's public surface. Satellites consume,
  rather than duplicate, Core's public types; internal visibility is for tests only.
- Mechanisms are family-blind and accept empty work sets without injecting dead machinery. Test both
  return and input C `long` paths, cross-family handle/struct resolution, aliases and unprefixed names
  before enabling satellites. Every family receives explicit sequential layout on applicable data structs.

The class collapse does not justify flattening ABI differences. Per-member hiding remains necessary;
uniform forwarding wrappers do not. The [ABI constitution](../../spikes/binding-generators/docs/canonical/binding-generator-constitution.md)
is the policy home for detailed mappings as it is reconciled with this decision.
