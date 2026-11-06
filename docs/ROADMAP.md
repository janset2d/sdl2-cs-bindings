# Roadmap — Janset.SDL2 / Janset.SDL3

Date: 2026-11-06

Operational summary. Remove completed work; rules belong in architecture, engineering and ADRs.
Read native versions and runtime coverage from [the manifest](../build/manifest.json), and framework
values from [build properties](../Directory.Build.props). Issues hold detailed work and acceptance.

## Now

The native harvest, family packaging and internal GitHub Packages staging pipeline exist. Lightweight
managed CI runs separately from the reserved full release matrix. Generated bindings are still a
[ClangSharp/Roslyn spike](../spikes/binding-generators/clangsharp/); production projects still compile
vendored SDL2-CS sources. The build host is target-centric; remaining work is hardening and production
generator integration, not another host architecture migration.

| Workstream | Milestone |
|---|---|
| Packaging/publication | [CI/CD & Packaging](https://github.com/janset2d/sdl2-cs-bindings/milestone/10) |
| SDL2 release completeness | [SDL2 Complete](https://github.com/janset2d/sdl2-cs-bindings/milestone/11) |
| Generation | [Binding Auto-Generation](https://github.com/janset2d/sdl2-cs-bindings/milestone/12) |
| SDL3 | [SDL3 Support](https://github.com/janset2d/sdl2-cs-bindings/milestone/13) |
| Deferred ideas | [Parking Lot](https://github.com/janset2d/sdl2-cs-bindings/milestone/14) |
| Hardening | [Hardening](https://github.com/janset2d/sdl2-cs-bindings/milestone/15) |

## Next

1. **Build-host cleanup and latest packages:** adopt the agreed quality model, refresh toolchain
   dependencies and framework targets, preserve managed/native separation and cross-OS feedback.
2. **Generator baseline:** regenerate stale families with the stable toolchain and classify the
   complete diff. Preserve deterministic file sets, isolation, family equivalence and API evidence.
3. **Generator into C# tooling:** port the existing orchestration with a byte-identical output oracle;
   keep the tested host salvage at its current paths until a production consumer replaces it.
   Revalidate parser/Roslyn upgrades against that baseline after the port.
4. **Binding surface collapse:** implement the decided one-class binding/friendly tiers, per-member
   ABI demotion, boolean wrapper, typed callbacks and naming policy. Complete macro helpers,
   runtime endian aliases and Mixer callback lifetimes. Core public support precedes satellites.
5. **Linux-canonical generation:** canonical headers come from the unchanged builder; separate the
   generator runtime from consumer natives and prove drift on every development OS.
6. **Native pins and glibc research:** exercise the regenerate/classify maintenance loop on native
   updates. Research a lower glibc floor without raising or proposing to raise the constitutional floor
   in [ADR-0010](adr/0010-linux-builder-glibc-floor.md).
7. **Production flip:** promote Core generated source and reproducibility/stale-output checks
   ([#69](https://github.com/janset2d/sdl2-cs-bindings/issues/69)); close SysWM layout, satellite sweep,
   duplicate Core-type diagnostics, pack symbol checks and remaining vendored-import retirement
   ([#70](https://github.com/janset2d/sdl2-cs-bindings/issues/70)). Net needs a concrete managed/native
   family, manifest re-entry, vcpkg features and harvest proof ([#58](https://github.com/janset2d/sdl2-cs-bindings/issues/58)).

[Generation promotion gates](architecture/binding-generation.md#promotion-evidence-gates) own the exit
contract. Optional enum source-fidelity polish (composed keycodes/character literals) follows correctness;
numeric values remain acceptable when ABI-correct.

## Release path

First public previews carry generated bindings, not transitional SDL2-CS imports: avoid imposing a
second API migration on preview consumers. Complete Core, then SDL2 satellite generation and package
evidence before the first public wave. SDL3 follows a real SDL2 public release and its own native
package support; do not make its bring-up block the first SDL2 public release.

The brand v1.0 goal is SDL2 Core/Image/Mixer/Ttf/Gfx/Net plus SDL3 Core/Image/Mixer/Ttf, all generated,
with native coverage across the supported matrix and stable nuget.org distribution. SDL3 Net and
shadercross stay deferred until their upstream maturity and packaging are deliberately assessed.
The brand launch is not a package-version reset. Labels, dwell times, promotion evidence and
post-launch maintenance are owned by [packaging and release](architecture/packaging-and-release.md).
The path is internal waves → public previews → release candidates → stable launch with real-consumer
feedback, samples, documentation and launch artifacts, then Janset2D development on these bindings.

Public promotion and first release metadata are tracked in
[#63](https://github.com/janset2d/sdl2-cs-bindings/issues/63). Samples
[#60](https://github.com/janset2d/sdl2-cs-bindings/issues/60), convenience packages
[#61](https://github.com/janset2d/sdl2-cs-bindings/issues/61), contributor guidance
[#62](https://github.com/janset2d/sdl2-cs-bindings/issues/62), smoke expansion
[#59](https://github.com/janset2d/sdl2-cs-bindings/issues/59) and Linux compatibility
[#64](https://github.com/janset2d/sdl2-cs-bindings/issues/64) remain explicit release work.

## Open questions

| Question | Decision point |
|---|---|
| dotnet-affected library versus CLI integration | Feasibility evidence and an ADR before integration |
| Public orchestration and promotion mechanism | Before first nuget.org prerelease; manual dispatch versus a single release-object event remains a design question, not a return to per-tag fan-out |
| Recovery/manual operation | Write an operator runbook with credential provisioning, explicit feed, versions and audit trail; reuse the normal pipeline, or explicitly defer the proposed family helpers ([#119](https://github.com/janset2d/sdl2-cs-bindings/issues/119)) |
| RID-specific smoke versus framework-dependent resolver coverage | Promote the runtime checkpoint only with Windows/Linux/macOS evidence |
| Linux MIDI runtime packaging | Before first public Mixer release; Timidity configuration/patch data and native MIDI are distinct contracts |
| Gfx Unix export regression guard | Next touching baseline/hardening change; compare smoke-time nm/readelf checks, pack checks or both |
| Shared-native duplicate policy | Reopen only if pure-dynamic packaging returns; dormant under hybrid-static |
| Ultimate convenience-package versioning | After a real full-train rehearsal; core-anchored, train-anchored and prerelease-only alternatives remain open. Individual package installation must remain clear without a metapackage |
| Public dwell-time revision | Only with real consumer feedback; packaging canon owns the current rule |

The role-meta/Bindings/Native topology refactor stays parked. Any one trigger reopens it: three or
more real-consumer issues requesting bindings-only/native-only use; friction from learning-sdl2 or
another active downstream with the current two-package shape; SDL3 launch planning selects the
new shape at introduction; or brand-launch planning selects it for that wave. Evidence: internal
research, 2026-05-12, "Package Topology Refactor Research - DEFERRED". Its dependency-lower-bound
carve-out is independent work in [#104](https://github.com/janset2d/sdl2-cs-bindings/issues/104), using
the existing family version set rather than waiting for a topology redesign.

## Known gaps

- **Public path:** PublishPublic is a stub; staging publishes to GitHub Packages only. The setup
  remote-nuget source also returns not implemented. Use local or remote-github setup meanwhile.
- **Policy versus output:** spike output still uses an internal raw container and public forwarders.
  The new binding/friendly policy, ABI-exact boolean struct and uniform typed callbacks are not yet
  implemented throughout. The oracle's public-import checks still reflect the old container policy.
  [Directory.Build.props](../Directory.Build.props) and spike TFMs differ from [ADR-0009](adr/0009-target-frameworks.md).
- **API enforcement:** the snapshot covers Core on one TFM; package validation is not enabled.
- **Tooling:** the executable generator is Python; maintenance intentionally retains those commands.
  Tested header/config/stamp/output safeguards and symbol validation are dormant salvage. Production
  stamp writing, stale-output validation and the integrated pack symbol check still need consumers.
- **Compiler:** the earlier projection hit CS9363 under SDK 10.0.401 and retained one output delta.
  Revalidate that historical hazard under the actual pinned SDK before further projection work.
- **Scope:** G58 requires Core in the same version invocation; feed-probe relaxation must query the
  actual destination feed. A CI prerelease does not satisfy a stable lower bound. Consumer smoke
  requires all concrete families or no selection. Per-family projects are candidates; evaluate file
  count, manifest drift, scope isolation and cross-family coverage before choosing.
- **Publication checks:** planned family/SemVer validation, feed monotonicity, existing-version refusal,
  smoke-before-publish, staging-before-public and destination Core availability are not established
  by the old proposed ValidateTask. Preserve these intended safeguards when implementing promotion.
- **Train/recovery:** unique identities, dependency ordering, explicit partial-train recovery,
  operator-version agreement with audit tags, explicit destination and who/what/when/hash records
  remain design requirements to wire, not proof that every proposed check exists.
- **Linux exports:** hidden-default compiler flags do not hide symbols explicitly marked default.
  Per-satellite version scripts are candidate hardening, not a current guarantee. Preserve gfx's public
  exports and audit Mixer codec parity across the supported native matrix.
- **Assets and breadth:** establish smoke-owned provenance/licensing and replace branding PNG use
  with tiny fixtures. Add BMP/WAV/UTF-8 RWops/resource pairs, mandatory image loads, clean-license font
  render, gfx pixel mutations and agreed mixer codecs. QOI remains optional; MIDI waits for its runtime
  decision. Manual window/image/font/audio/mixer/sprite/input/backend/network diagnostics stay outside CI.
- **Upstream ports:** continue deterministic rect/RWops/surface/pixel/GUID/platform/error/version,
  timer/hints/events, dummy audio and software-render work; then math, keyboard/mouse, stdlib,
  subsystem/log, virtual joystick and deterministic atomic/mutex cases. Capability-aware clipboard,
  filesystem/locale/power/loadso, YUV/iconv/geometry and focus need separate evidence. Thread creation
  awaits macro/REAL entrypoint policy; SysWM awaits proven unions; SDL_GetErrorMsg needs upstream-backed
  behavior evidence. Keep exact source cases, actual counts/skips and deferrals per slice.
- **Planning estimates:** historical aspirations were a 70–100-test spine, 80–130 further headless
  cases and 20–40 gated cases, roughly 260–280 stable candidates and 300+ only with gated lanes.
  These are neither current counts nor acceptance thresholds. Broaden mechanical symbol/constant/
  layout/callback/macro coverage separately from representative runtime cases.

## Backlog

| Group | Issues |
|---|---|
| Correctness, CLI/UX, style, API/performance | [#110](https://github.com/janset2d/sdl2-cs-bindings/issues/110), [#111](https://github.com/janset2d/sdl2-cs-bindings/issues/111), [#112](https://github.com/janset2d/sdl2-cs-bindings/issues/112), [#113](https://github.com/janset2d/sdl2-cs-bindings/issues/113) |
| Extractions, reporters, tests, conditional watch list | [#114](https://github.com/janset2d/sdl2-cs-bindings/issues/114), [#115](https://github.com/janset2d/sdl2-cs-bindings/issues/115), [#116](https://github.com/janset2d/sdl2-cs-bindings/issues/116), [#117](https://github.com/janset2d/sdl2-cs-bindings/issues/117) |
| Consumer/harvest boundaries, paths and readiness | [#101](https://github.com/janset2d/sdl2-cs-bindings/issues/101), [#87](https://github.com/janset2d/sdl2-cs-bindings/issues/87), [#102](https://github.com/janset2d/sdl2-cs-bindings/issues/102), [#103](https://github.com/janset2d/sdl2-cs-bindings/issues/103) |
| Dependency ranges, reporter/fixture API, diagnostics, oracles and deferred triggers | [#104](https://github.com/janset2d/sdl2-cs-bindings/issues/104), [#105](https://github.com/janset2d/sdl2-cs-bindings/issues/105), [#106](https://github.com/janset2d/sdl2-cs-bindings/issues/106), [#107](https://github.com/janset2d/sdl2-cs-bindings/issues/107), [#108](https://github.com/janset2d/sdl2-cs-bindings/issues/108), [#109](https://github.com/janset2d/sdl2-cs-bindings/issues/109) |
| Bad-native combinations, recovery, feed health and culture | [#118](https://github.com/janset2d/sdl2-cs-bindings/issues/118), [#119](https://github.com/janset2d/sdl2-cs-bindings/issues/119), [#120](https://github.com/janset2d/sdl2-cs-bindings/issues/120), [#121](https://github.com/janset2d/sdl2-cs-bindings/issues/121) |
| Optional coverage, metadata caching, tool reproducibility, logs and CI caches | [#122](https://github.com/janset2d/sdl2-cs-bindings/issues/122), [#123](https://github.com/janset2d/sdl2-cs-bindings/issues/123), [#124](https://github.com/janset2d/sdl2-cs-bindings/issues/124), [#125](https://github.com/janset2d/sdl2-cs-bindings/issues/125), [#126](https://github.com/janset2d/sdl2-cs-bindings/issues/126), [#127](https://github.com/janset2d/sdl2-cs-bindings/issues/127) |
| Validator output shape | [#128](https://github.com/janset2d/sdl2-cs-bindings/issues/128) |
| Package/feed and supply-chain integrity | [#65](https://github.com/janset2d/sdl2-cs-bindings/issues/65), [#66](https://github.com/janset2d/sdl2-cs-bindings/issues/66), [#81](https://github.com/janset2d/sdl2-cs-bindings/issues/81) |
| Native overrides, dependency-free variant and musl | [#67](https://github.com/janset2d/sdl2-cs-bindings/issues/67), [#80](https://github.com/janset2d/sdl2-cs-bindings/issues/80), [#82](https://github.com/janset2d/sdl2-cs-bindings/issues/82) |
| History cleanup and local-development validation | [#56](https://github.com/janset2d/sdl2-cs-bindings/issues/56), [#57](https://github.com/janset2d/sdl2-cs-bindings/issues/57) |
| SDL3 bindings and release support | [#71](https://github.com/janset2d/sdl2-cs-bindings/issues/71), [#72](https://github.com/janset2d/sdl2-cs-bindings/issues/72) |

Groups overlap. Cross-link issues rather than implementing the same work twice. Retired historical
plans and research are preserved in the companion repository; current rules remain public canon.
