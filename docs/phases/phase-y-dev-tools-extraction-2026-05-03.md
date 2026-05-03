# Phase Y — Dev-Tools Extraction & Cake CI-Only Narrowing

- **Date:** 2026-05-03
- **Status:** Wave A IMPLEMENTED (2026-05-03), Wave B IMPLEMENTED (2026-05-03)
- **Author:** Deniz İrgin (@denizirgin) + 2026-05-03 collaborative critique synthesis (two reviewer passes incorporated)
- **Governing ADR:** [ADR-004 — Cake-Native Feature-Oriented Build-Host Architecture](../decisions/2026-05-02-cake-native-feature-architecture.md) (in-place rewrite, no new ADR)
- **Supersedes:** No prior plan
- **Standalone phase:** This is a build-host **scope reduction** wave that is parallel to (and partially overlaps with) Phase X. Sequencing relative to Phase X P5 is documented in §3.

## 0. TL;DR

Move dev-experience orchestration out of Cake into a single repo-root file-based .NET 10 script (`tools.cs`). Cake remains, but its mission narrows to **only** what `release.yml` invokes. The `Features/LocalDev/` slice and the `Features/Packaging/ArtifactSourceResolvers/` family disappear entirely. The smoke-witness + behavior-baseline scaffolding from Phase X P0 retires — its mission was the P1–P4-A migration safety net, which is closed.

```text
Before                                  After
─────────────────────────────────       ─────────────────────────────────
build/_build/                            build/_build/
  Features/                                Features/
    LocalDev/                                Harvesting/
    Harvesting/                              Packaging/      (no resolvers,
    Packaging/                                              no JansetLocalPropsWriter,
      ArtifactSourceResolvers/                              no VersionsFileWriter)
      ArtifactProfile.cs                     Preflight/
      JansetLocalPropsWriter.cs              Publishing/
      VersionsFileWriter.cs                  Vcpkg/
    Preflight/                               Versioning/
    Publishing/                            ...
    Vcpkg/                                 tools.cs            (NEW, repo root,
    Versioning/                                                shebang + chmod +x)
  Host/Cli/Options/PackageOptions.cs       build.ps1           (kept, Cake-only)
  ...                                      build.sh            (kept, Cake-only)
tests/scripts/                           (no tests/scripts/)
  smoke-witness.cs
  verify-baselines.cs                    CLI surface:
  baselines/                               tools setup [--source=local|remote-github|remote-nuget]
build.ps1                                  tools setup --no-clean      (skip CleanArtifacts)
build.sh                                   tools ci-sim [--verbose]
                                           tools build [<cake-args>...] (passthrough)
CLI surface:                               build.{ps1,sh} [...] ← unchanged Cake forwarders
  --target SetupLocalDev --source=...
  --target {Cake targets}
  (and SetupLocalDevTask)                Invocation forms:
                                           dotnet run --file tools.cs -- <subcommand> ...
                                           ./tools.cs <subcommand> ...        (Unix, shebang + chmod +x)
```

> **Invocation note (cross-platform):** Repo root contains a solution file (`Janset.SDL2.sln`) and project files. `dotnet run tools.cs ...` from repo root **fails** with project-context resolution. Use `dotnet run --file tools.cs -- ...` on Windows; Unix shebang form `./tools.cs ...` works after `chmod +x`. This is the same invocation pattern smoke-witness used (`tests/scripts/README.md` documented it).

## 1. Wave-Status Snapshot (2026-05-03)

| Wave | Status | Notes |
| --- | --- | --- |
| **A** Phase X baseline retirement + `tools.cs` skeleton | ✅ IMPLEMENTED 2026-05-03 | Tek atomic commit. (1) `tests/scripts/baselines/`, `verify-baselines.cs` silinir; smoke-witness `--emit-baseline` flag + bağlı record'lar silinir. (2) Repo-root `tools.cs` yaratılır: `build` subcommand çalışır (Cake forwarder), `setup` ve `ci-sim` **explicit stub** olarak exit 64 + "not yet implemented in Wave A — see Phase Y plan §10" mesajı verir. Unix shebang + `git update-index --chmod=+x`. `build.ps1`/`build.sh` dokunulmuyor. Cake host dokunulmuyor. |
| **B** Atomic dev-tools cut-over | ✅ IMPLEMENTED 2026-05-03 | Tek atomic commit. `tools setup` + `tools ci-sim` subcommand'ları full implementation. **Aynı commit'te** Cake host'tan `Features/LocalDev/`, `Features/Packaging/ArtifactSourceResolvers/`, `Features/Packaging/ArtifactProfile.cs`, `Features/Packaging/JansetLocalPropsWriter.cs`, `Features/Packaging/VersionsFileWriter.cs`, `Host/Cli/Options/PackageOptions.cs`, `--source` CLI option, ParsedArguments.Source, smoke-witness.cs silinir. ADR-004 §2.5 + §2.13 invariant #4 + §2.15 (komple delete) + motto + reference layout + glossary + DI chain örneği rewrite. ArchitectureTests `OrchestrationFeatureAllowlist` kaldırılır + invariant #4 strict halini alır. Phase X plan §1.2/§1.3/§1.5/§1.6/§2.1.x amendments. Coverage baseline reset (515 → 485). `tests/scripts/` klasörü silinir. |

**Single-session execution:** PR review döngüsü yok; tüm iş tek session'da iterasyonlarla gider. Wave A ve Wave B'nin ayrı atomic commit'ler olarak land etmesi sadece **rollback granularity** içindir — Wave A bittikten sonra `git revert` Wave A → Wave B impact'i sıfırlar, ya da Wave B `git revert` Wave B → Wave A korunur. PR review sırası optimizasyonu değil.

**Behavior-signal preservation strategy:** Phase X bu sayede smoke-witness'a dayanıyordu; Phase Y kapanışı bu mekanizmayı emekli ediyor. Phase Y içi davranış güveni iki damar:

1. **`release.yml`'e sıfır etki** — Wave A/B'nin hiçbir adımı `release.yml`'in çağırdığı target setine dokunmaz. Release pipeline her wave'in açılışı ve kapanışında yeşil olmalı (§4.1 + §11).
2. **`tools ci-sim` paritesi** — Wave B'de `smoke-witness ci-sim` davranışı `tools ci-sim`'e taşınır; aynı RID'de aynı sıralı adımlar aynı `versions.json` mapping'iyle çalıştırılır. Wave B öncesi/sonrası aynı host'ta `tools ci-sim` çalıştırılarak step listesi + exit code'lar manuel diff'lenir (otomatize gerek yok — Phase X-stili formal baseline file'ı bu phase'de kullanmıyoruz).

---

## 2. Purpose and Scope

### 2.1 Why this plan exists

Phase X (ADR-004 migration) build-host'u Cake-native, feature-oriented bir mimariye taşıdı. Bu yapının içinde tek bir feature (`Features/LocalDev/`) **multi-feature compose** sorumluluğu için designated orchestration feature olarak yer aldı (ADR-004 §2.5). Bu istisna pratikte iki problem yarattı:

1. **Cake mission drift.** Build host'un teorik mission'ı CI/CD pipeline lifecycle'ı çalıştırmak. `SetupLocalDev` ise yalnızca local geliştirici ergonomi için var — `release.yml`'in herhangi bir job'ı bu target'ı çağırmıyor. Cake host'u dev-experience concern'ünü taşıyarak iki farklı mission boundary'sini tek artefact'ta birleştiriyor.
2. **Architectural carve-out maintenance.** ADR-004 §2.13 invariant #4 (Features cross-reference yasağı) bir allowlist exception'ıyla yumuşatılmış durumda. ArchitectureTests'in `OrchestrationFeatureAllowlist` allowlist'i, ADR-004 §2.5'in "designated orchestration feature" koreografisini, `--source` CLI option'ını ve `Features/Packaging/ArtifactSourceResolvers/` factory chain'ini birlikte ayakta tutuyor. Bu carve-out tek bir feature uğruna "exception, not convention" disiplinini her geliştirme oturumunda yeniden müzakere ettiriyor.

.NET 10'un [file-based apps](https://learn.microsoft.com/en-us/dotnet/core/sdk/file-based-apps) feature'ı ile dev-experience orkestrasyonu Cake'in dışına, repo-root seviyesinde tek bir `tools.cs` script'ine taşınabilir. Aynı pattern repo'da zaten iki kez pivot edildi (`tests/scripts/smoke-witness.cs`, `tests/scripts/verify-baselines.cs`); model olgun.

Aynı zamanda Phase X'in P0'da ürettiği behavior-baseline scaffolding'i (`smoke-witness.cs` + `verify-baselines.cs` + `tests/scripts/baselines/`) misyonunu tamamladı: Phase X P1–P4-A wave'leri kapandı, bu commit boundary'lerinde behavior signal MATCH olarak doğrulandı (phase-x [§1 Wave-Status Snapshot](phase-x-build-host-modernization-2026-05-02.md#wave-status-snapshot-2026-05-02)). Geri kalan iş yapısal değil naming + atomic (P5), dolayısıyla baseline file'lar artık aktif risk surface'i değil.

### 2.2 What is in scope

Ayrıntılı dosya / sembol listesi §10'da. Yüksek seviye:

- **Tüm `Features/LocalDev/` feature folder'ının silinmesi**.
- **Tüm `Features/Packaging/ArtifactSourceResolvers/` klasörünün silinmesi**.
- **`Features/Packaging/ArtifactProfile.cs`, `JansetLocalPropsWriter.cs`, `VersionsFileWriter.cs` silinmesi** — hepsi resolver / LocalDev tarafından kullanılıyor, başka caller'ı yok.
- **`Host/Cli/Options/PackageOptions.cs` silinmesi** — sadece `SourceOption` taşıyor, başka option yok.
- **`Program.cs`'de `--source` ile ilgili tüm satırların silinmesi** (line 14 using, line 54 option registration, lines 126-130 source parse + validation, line 154 `AddPackagingFeature(source)` → `AddPackagingFeature()`, line 290 `ParsedArguments.Source` field).
- **`AddPackagingFeature(string source)` overload'unun `string source` parametresinin silinmesi**.
- **`IPathService.GetLocalPropsFile()` (line 159) + `PathService.GetLocalPropsFile()` (line 291-293) implementation'ının silinmesi**.
- **`smoke-witness.cs` + `verify-baselines.cs` + `tests/scripts/baselines/*` + `tests/scripts/README.md` silinmesi** — `tests/scripts/` klasörü tamamen kalkar.
- **`build/_build.Tests/` altındaki LocalDev / Resolver / Writer test dosyalarının silinmesi**, `ArchitectureTests` + `ProgramCompositionRootTests` + `PathConstructionTests` + `ServiceCollectionExtensionsSmokeTests`'in `IArtifactSourceResolver` / `Source` / `GetLocalPropsFile` / `AddPackagingFeature("local")` referanslarından arındırılması.
- **Yeni `tools.cs` (repo root)**: 3 subcommand — `setup`, `ci-sim`, `build`. Unix shebang + chmod +x via `git update-index --chmod=+x`.
- **Janset.Local.props writer'ının `tools.cs setup`'a taşınması** — `JansetLocalPropsWriter.BuildContent` mantığının birebir portu (XElement bazlı, sorted family list, `LocalPackageFeed` + per-family `JansetSdl<Major><Role>PackageVersion` properties).
- **versions.json yazımının `tools.cs setup`'a taşınması** — `VersionsFileWriter` script-side replicated; `ResolveVersions` Cake target'ı zaten kendi versions.json'unu yazıyor (ResolveVersionsPipeline.cs:21 doc), ama `tools setup --source=remote-github` Cake'i atladığı için kendi yazıcısına ihtiyaç duyar.
- **GitHub Packages remote-feed download'unun `tools.cs setup --source=remote-github`'a taşınması** (NuGet.Protocol package directive + GH_TOKEN env var + behavior parity checklist §10.4'te detaylı).
- **ADR-004 in-place rewrite** — §2.5 (Flow class), §2.13 invariant #4, §2.15 (komple delete), motto, reference layout, glossary, all `LocalDev` referansları.
- **Phase X plan in-place amendment** — §1.2 / §1.3 / §1.5 wave tablosu / §1.6 motto / §2.1.x section'ları (line-by-line rewrite, sadece header amendment yetmez).
- **Diğer canonical docs update** — onboarding, plan, README phase index, playbook'lar, knowledge-base, AGENTS.md, CLAUDE.md, Janset.Smoke.props yorumları.
- **Coverage baseline reset** ([`build/coverage-baseline.json`](../../build/coverage-baseline.json)) — silinen 30 test nedeniyle 515 → **485**.

### 2.3 What is explicitly out of scope

- **`release.yml` job topology** — bu plan `release.yml`'i bozmaz. Tek değişiklik: cake-host artifact'ı içinde artık `SetupLocalDev` target'ı bulunmuyor; release.yml zaten bu target'ı çağırmıyor, dolayısıyla diff = 0.
- **Cake target naming cleanup** — `PreFlightCheck` → `Preflight` vs. Bu phase-x P5'in scope'unda kalır; Phase Y dokunmaz.
- **Phase X P4-C** — Large Pipeline decomposition (PackageConsumerSmoke, Harvest, Package). Phase X scope'unda kalır.
- **`--versions-file` veya version override flag'leri `tools setup`'a eklemek** — şimdilik latest-only. `--versions-file <path>` veya `--versions <family>=<semver>` gelecekteki bir Phase Y tail wave'ine bırakılır.
- **`--source=release` profile** — Phase Y bu değeri tamamen yok ediyor (CLI'dan + ADR-004 §2.15'ten). Phase 2b PD-7 ileride bir release-feed mode'u getirirse `tools setup --source=release` olarak script tarafına eklenir, Cake'e değil.
- **`ArtifactProfile` enum'ın manifest semantiği** — değişmez (zaten enum tamamen siliniyor).
- **Manifest schema v2.1** — değişmez.
- **Pack guardrails (G14/G15/G16/G46/G54/G58)** — değişmez.

### 2.4 Why a standalone phase

Phase X bir mimari **shape change** — bütün feature'ları aynı yeni şablona taşıdı. Phase Y bir mimari **scope reduction** — Cake'in mission boundary'sini daraltıyor, bir feature ailesini ve onun bağlı CLI/DI yüzeyini siliyor, dev orkestrasyonunu Cake'in dışına çıkarıyor. Phase X'in son wave'leriyle aynı commit train'ine sıkıştırmak şu üç problemi yaratırdı:

1. Phase X review yükünü çift yönlü artırırdı (yapı değişikliği + ölçek değişikliği aynı diff'te).
2. Phase X'in canonical kapanışı (P5 atomic naming wave) Phase Y'nın silme yüküyle karışırdı; rollback granularity kaybolurdu.
3. Phase X'in "behavior signal MATCH" green criterion'ı Phase Y'da geçerli değil — Phase Y zaten `local` mode'unu siliyor. Phase X'in baseline sözleşmesi içinde Phase Y atomic kapanışını işletemezdik.

### 2.5 Mechanical vs structural waves

| Wave | Character | What changes in production code |
| --- | --- | --- |
| **A** | Mechanical (deletion + additive) | `tests/scripts/baselines/*` + `verify-baselines.cs` siler; `smoke-witness.cs` `--emit-baseline` flag + `EmitBaselineAsync` + `BaselineSignal` + `BaselineStep` record'larını siler (witness `local` / `remote` / `ci-sim` üç modu duruyor). Repo-root `tools.cs` dosyası eklenir; `build` subcommand pure Cake forwarder olarak çalışır; `setup` ve `ci-sim` explicit stub (exit 64). Cake host'a sıfır dokunma; `build.ps1` / `build.sh` dokunulmuyor. |
| **B** | **Structural (atomic surface change)** | `tools.cs`'e `setup` + `ci-sim` subcommand'ları full implementation. **Aynı commit'te** Cake host'tan tüm LocalDev + ArtifactSourceResolvers + ArtifactProfile + JansetLocalPropsWriter + VersionsFileWriter + PackageOptions + `--source` CLI option silinir; `Features/Packaging/ServiceCollectionExtensions.cs` `string source` parametresinden arındırılır; `IPathService` Janset.Local.props metodu (`GetLocalPropsFile`) silinir; `smoke-witness.cs` silinir; `tests/scripts/` boşalıyor → silinir; ADR-004 §2.5 + §2.13 invariant #4 + §2.15 (delete) + motto + reference layout + glossary + DI chain örneği rewrite; ArchitectureTests `OrchestrationFeatureAllowlist` kaldırılır + invariant #4 strict halini alır; Phase X plan §1.2 + §1.3 + §1.5 + §1.6 + §2.1.x amendments; coverage baseline reset (515 → 485); tüm canonical doc'larda `SetupLocalDev` referansları update. Davranış olarak: `release.yml` çıktıları aynı; dev'in tek girişi `dotnet run --file tools.cs -- setup --source=local`. |

Wave A bağımsız (rollback granularity = sadece A). Wave B Wave A'nın ön-koşulu (tools.cs'in skeleton'ı Wave A'da geliyor; Wave B onu doldurur ve Cake host kıyametini açar).

---

## 3. Sequencing relative to Phase X

Phase X mevcut planı şu collision'ları üretiyor:

| Phase X §X | Item | Phase Y kapsamı |
| --- | --- | --- |
| §1.2 (Scope) | "Retirement of `UnsupportedArtifactSourceResolver`" — P5 atomic | Phase Y Wave B'de **tüm resolver ailesi** siliniyor; `UnsupportedArtifactSourceResolver` o sırada gidiyor |
| §1.3 (Out of scope) | "§2.15 `--source` narrowing (`release` / `release-public` retired) ... landed atomically with callsite updates in P5" | Phase Y Wave B'de `--source` CLI option **tamamen** kaldırılıyor (yalnızca narrowing değil, deletion). ADR-004 §2.15 komple silinir |
| §1.5 wave tablosu | P5 row: "Naming + atomic" — UnsupportedArtifactSourceResolver retirement + `--source` narrowing dahil | Phase Y bu iki maddeyi P5'ten çekiyor; P5 sadece pure target-rename atomic commit'i olarak küçülüyor |
| §2.1 / §2.1.1–§2.1.5 | Smoke-witness behavior signal sözleşmesi (mode'lar, baseline format, fast/milestone loop) | Phase Y Wave A baseline scaffolding'i siliyor; Wave B'de smoke-witness silindiği için **§2.1 line-by-line rewrite** edilir (sadece "retired" header'ı yetmez — "developer's pre-merge ritual is `tools ci-sim` + manual diff" wording'ine geçer) |
| §1.6 motto + reference layout | "LocalDev owns orchestration" + LocalDev row'u | Phase Y Wave B'de motto rewrite + LocalDev row silinir |

**Sequencing kararı:** Phase Y, Phase X P5'ten **önce** girer. Bu sayede:

- Phase X P5'in scope'u küçülür: sadece target-rename atomic commit'i (`PreFlightCheck` → `Preflight`, `Coverage-Check` → `CoverageCheck`, `Inspect-HarvestedDependencies` → `InspectHarvestedDependencies`). UnsupportedArtifactSourceResolver retirement ve `--source` narrowing P5'ten çıkar; Phase Y bunları zaten kapsıyor.
- Phase X P5 pre-merge ritüeli olarak smoke-witness'a güvenmiyor (Phase Y Wave A onu zaten emekli etti).
- Phase X P5 değişikliği gerçek anlamda mekanik: sadece string'ler.

Phase X plan amendment'ları Phase Y Wave B'de yapılır (Phase X'in P5 satırı + §2.1.x bölümleri yeniden yazılır).

---

## 4. North Stars

Üç invariant Phase Y boyunca her commit'te yeşil olmalı.

### 4.1 `release.yml` invariance

Phase Y'nin hiçbir adımı `release.yml`'in çağırdığı Cake target'larını silmez veya semantiğini değiştirmez. Hedef set:

```text
ResolveVersions, PreFlightCheck, GenerateMatrix, EnsureVcpkgDependencies,
Harvest, NativeSmoke, ConsolidateHarvest, Package, PackageConsumerSmoke,
PublishStaging, Coverage-Check, CleanArtifacts, Info
```

Wave A ve Wave B kapanış commit'lerinde bu target'lar `tools build --target <name>` üzerinden host'ta yeşil olmalı. CI'da release.yml workflow_dispatch dry-run gate'i §11'de detaylı.

### 4.2 `tools ci-sim` parity

Wave B öncesi ve sonrası aynı host'ta:

```pwsh
dotnet run --file tools.cs -- ci-sim                # post-Wave-B
dotnet run --file tests/scripts/smoke-witness.cs -- ci-sim   # pre-Wave-B
```

birebir aynı sıralı target çağrılarını üretir. Step listesi aynı, exit code'ları aynı, `versions.json` çıktısı aynı (timestamp suffix farkı normalize edilerek). Otomatize bir baseline file'ı yok (Phase X'in formalization'ı Phase Y Wave A'da retire ediliyor); doğrulama operatör tarafından manuel diff. Wave B atomic commit boundary'sinde bu pariteyi kanıtlamak zorunlu.

### 4.3 `tools setup` semantic equivalence

`tools setup --source=local` post-Wave-B davranışı = pre-Wave-B `smoke-witness local` mode'unun ilk iki adımına eşdeğer (CleanArtifacts → SetupLocalDev). PackageConsumerSmoke `tools ci-sim`'in son adımı; `tools setup`'ta yok.

| Output surface | Pre-Wave-B (`smoke-witness local`) | Post-Wave-B (`tools setup --source=local`) |
| --- | --- | --- |
| `artifacts/` clean state | CleanArtifacts wipes | CleanArtifacts wipes (default; `--no-clean` skips) |
| `artifacts/packages/*.nupkg` | 5 family × 3 nupkg = 15 nupkg | Same 5 × 3 = 15 nupkg |
| `artifacts/resolve-versions/versions.json` | `local.{timestamp}` suffix, sorted by family name (OrdinalIgnoreCase) | Identical format (timestamp differs, structure same) |
| `build/msbuild/Janset.Local.props` | XML: `LocalPackageFeed` + per-family `JansetSdl<Major><Role>PackageVersion`, sorted | Identical XML structure |
| Exit code | 0 | 0 |

`--source=remote-github`: Aynı parite GitHub Packages feed'inden discovery + download + props yazımıyla.

`--source=remote-nuget`: Stub — exit 64 + "not yet implemented (Phase 2b PD-7 territory)" mesajı. Janset.Local.props yazılmaz; CleanArtifacts da default'ta atılmaz (stub erken çıkar).

### 4.4 ArchitectureTests strictness

Wave B sonrası `ArchitectureTests.Features_Should_Not_Cross_Reference` allowlist'siz halinde yeşil. Pre-Wave-B'de zaten yeşildi (LocalDev silinmemiş bile olsa, allowlist short-circuit'i nedeniyle); Wave B'de allowlist field + short-circuit silindiğinde de yeşil kalmalı. Wave A'nın son adımı bu invariant'ı **henüz allowlist boşken** çalıştırmak (geçici bir test kabusu — Wave B'ye girmeden cross-feature sızıntı kalmamış olduğunu doğrulamak için).

---

## 5. ADR-004 in-place rewrite

ADR-004 (`docs/decisions/2026-05-02-cake-native-feature-architecture.md`) Phase Y Wave B'de in-place rewrite olarak güncellenir. Aşağıdaki noktalar etkilenir:

| Line | Mevcut wording | Phase Y rewrite |
| --- | --- | --- |
| 37 (Motto) | "Motto: *Features own behavior. **LocalDev owns orchestration.** Shared owns vocabulary. Tools run commands. Host runs Cake.*" | "Motto: *Features own behavior. Shared owns vocabulary. Tools run commands. Host runs Cake. **Dev orchestration lives outside Cake — see `tools.cs`.***" |
| 58 (Architecture tree) | "Features/ ← operational vertical slices (Harvesting, Packaging, Preflight, Versioning, Publishing, **LocalDev**, ...)" | "Features/ ← operational vertical slices (Harvesting, Packaging, Preflight, Versioning, Publishing, ...). No orchestration features — multi-feature compose lives in repo-root `tools.cs`, not in Cake." |
| 146-156 (Reference layout — LocalDev) | "Reference layout (LocalDev, the only orchestration feature)" + dosya listesi | Tüm bölüm silinir |
| 170 (Features tablosu LocalDev row) | "Features/LocalDev/ row — designated orchestration feature" | Satır silinir |
| 240-256 (§2.5 Flow class) | "Multi-feature composition only — SetupLocalDev today" | Tüm §2.5 rewrite olur. Yeni başlık: "§2.5 Multi-feature composition lives outside Cake". İçerik: "Cake feature'larının cross-reference yasağı **istisnasız**. Multi-feature compose ihtiyacı çıkarsa, repo-root `tools.cs`'in bir subcommand'ı olarak yazılır; Cake task'larını sırayla `dotnet run --file tools.cs -- build --target X` ile çağırır. Designated orchestration feature pattern'i ADR-004 v1'in geçici bir çıkışıydı, Phase Y'da retired. Eski wording (`SetupLocalDev` örneği, "today only LocalDev") kaldırıldı." |
| 426 (Glossary "Flow") | "Flow: Multi-feature composition: one Cake Task that orchestrates pipelines from different features (only `SetupLocalDev` today)" | Glossary entry silinir |
| 500 (`internal sealed record SetupLocalDevRequest(...)` örneği) | Code sample | Örnek silinir veya farklı bir feature'ın Request DTO'su ile değiştirilir (örn. `HarvestRequest`) |
| 562 (DI chain `.AddLocalDevFeature()` örneği) | Code sample | `.AddLocalDevFeature()` satırı silinir; "registered last" yorumu güncellenir |
| §2.13 invariant #4 wording | "Features cross-reference yasağı, designated orchestration feature exception (LocalDev) ile" | "Features cross-reference yasağı, **istisnasız**. Cross-feature data sharing yalnızca `Shared/` üzerinden." Allowlist exception cümlesi tamamen kaldırılır. |
| §2.15 (`--source` narrowing) | "release / release-public retired until Phase 2b PD-7" | **§2.15 başlığıyla birlikte komple silinir** — ADR'den çöp bilgi temizlenir. Phase 2b PD-7'de `tools setup --source=release` geldiğinde release-feed mode'unun ADR'si o zaman yazılır |

Memory'deki amendment philosophy gereği "Amendment 2026-05-03" şeklinde ayrı paragraflar **eklenmez** — wording yerinde rewrite olur, eski cümleler temizlenir.

---

## 6. Phase X plan amendments

Phase X plan dökümanı (`docs/phases/phase-x-build-host-modernization-2026-05-02.md`) Phase Y Wave B'de amend edilir. Bu in-place rewrite — Phase X kapanmıyor (P4-C ve P5 hâlâ açık), Phase Y'nın aldığı ilgili maddeler işaretleniyor.

| Phase X §X | Amendment |
| --- | --- |
| §1.2 (Scope) | "Retirement of `UnsupportedArtifactSourceResolver`" satırı silinir; "(retired in Phase Y Wave B — see `phase-y-dev-tools-extraction-2026-05-03.md`)" cümlesi eklenir |
| §1.3 (Out of scope) | "§2.15 `--source` narrowing" satırı silinir (Phase Y bu maddeyi tamamen aldı, ADR-004 §2.15 da silindi) |
| §1.5 wave tablosu (P5 row) | Description sadeleşir: "Atomic same-commit target rename: `PreFlightCheck` → `Preflight`, `Coverage-Check` → `CoverageCheck`, `Inspect-HarvestedDependencies` → `InspectHarvestedDependencies`. UnsupportedArtifactSourceResolver retirement + `--source` narrowing **moved to Phase Y Wave B**." |
| §1.6 motto | LocalDev satırı silinir (ADR-004 motto rewrite'ıyla aynı) |
| §1.4 (Why a standalone phase) | Cümle eklenir: "Phase Y (2026-05-03) Cake mission narrowing'i + dev-tools extraction'ı bu plan'ın dışında, paralel bir track olarak ele alıyor. Phase X mimari shape, Phase Y mimari scope." |
| §2.1 başı | "**Retired by Phase Y Wave A (2026-05-03).** Bu bölüm Phase X P0–P4-A'da load-bearing'di; Phase Y Wave A sonrası retired. Dev pre-merge ritüeli artık `tools ci-sim` + manuel step-list/exit-code diff. Aşağıdaki §2.1.x alt bölümleri kronolojik kanıt olarak korunur, ama load-bearing değildir." |
| §2.1.4 ("--emit-baseline P0 deliverable") | İçeriğe ekleme: "Wave A bu flag'i ve `BaselineSignal` record'larını kaldırdı." |
| §2.1.5 (Loop cadence — fast vs milestone) | "developer's pre-merge ritual" wording'i satır-satır gözden geçirilip rewrite edilir (sadece header amendment yetmez): "Bu cadence Phase X migration safety net'iydi; Phase Y sonrası dev `tools ci-sim` çıktısını manuel inceler. Otomatize cadence yok." |
| §2.1.3 (When the signal changes) | "P5 naming cleanup wave" referansı korunur — Phase X P5 hâlâ var, sadece daraldı. Naming değişikliği P5'in scope'unda. Phase Y'da signal kavramı zaten emekli olduğu için "baseline file is updated atomically" cümlesi rewrite edilir: "P5 target rename'leri `tools ci-sim` step label'larında atomic same-commit'te update edilir; baseline file artık yok." |
| §6 P-stages (eğer atıf var ise) | "P5 = Naming + atomic" — `--source` narrowing referansı silinir, sadece target-rename kalır |
| §13 (release.yml integration, varsa) | LocalDev'e referans yoksa dokunulmaz |
| §14 Adım 13 — IPathService named exception | Phase Y'da `GetLocalPropsFile()` silindiği için bu bölüm güncel kalır (named exception işliyor olmaya devam eder, sadece JansetLocalProps satırı düşer) |

---

## 7. ArchitectureTests invariant #4 simplification

`build/_build.Tests/Unit/CompositionRoot/ArchitectureTests.cs`'in mevcut hali:

- **Line 39**: `private static readonly string[] OrchestrationFeatureAllowlist = ["Build.Features.LocalDev"];`
- **Lines 14-16** (XML doc): "except from the designated orchestration feature `Build.Features.LocalDev`"
- **Lines 114-115** (test method declaration): `Features_Should_Not_Cross_Reference_Except_From_LocalDev`
- **Lines 117-123** (method body comment): allowlist'in açıklaması
- **Lines 137-141** (method body): `if (OrchestrationFeatureAllowlist.Contains(...)) continue;` short-circuit

Wave B sonrası:

```csharp
// Line 39: field tamamen silinir.

// Lines 14-16: XML doc rewrite.
//   "<item><description><c>Features</c> do not cross-reference each other in code.</description></item>"

// Lines 114-115: method rename.
//   public async Task Features_Should_Not_Cross_Reference()

// Lines 117-123: method body comment rewrite.
//   "Build.Features.X.* may not reference types in Build.Features.Y.*. Cross-feature
//    data sharing flows through Build.Shared.* exclusively. Phase Y (2026-05-03) retired
//    the LocalDev orchestration-feature carve-out; multi-feature compose now lives
//    in repo-root tools.cs, not in Cake."

// Lines 137-141: short-circuit silinir. Method body sadece mevcut violation tespiti yapar.
```

---

## 8. Wave A — Phase X baseline retirement + `tools.cs` skeleton

### 8.1 Goal

Phase X P0'da gelen behavior-baseline mekanizmasını emekli et. Repo-root `tools.cs` script'ini yarat — `build` subcommand çalışsın, `setup` ve `ci-sim` explicit stub. Cake host'a sıfır dokunma.

### 8.2 File deletes

| Path | Reason |
| --- | --- |
| `tests/scripts/baselines/smoke-witness-local-win-x64.json` | Phase X P0 baseline — mission complete |
| `tests/scripts/baselines/smoke-witness-local-linux-x64.json` | Phase X P0 baseline — mission complete |
| `tests/scripts/baselines/smoke-witness-local-osx-x64.json` | Phase X P0 baseline — mission complete |
| `tests/scripts/baselines/smoke-witness-ci-sim-win-x64.json` | Phase X P0 baseline — mission complete |
| `tests/scripts/baselines/cake-targets.txt` | Phase X P0 deliverable, no longer load-bearing |
| `tests/scripts/baselines/test-count.txt` | Phase X P0 deliverable, no longer load-bearing |
| `tests/scripts/baselines/` (klasör) | Boşalır → silinir |
| `tests/scripts/verify-baselines.cs` | Operationalized baseline cadence — emekli oluyor |

### 8.3 File edits

| Path | Edit (line numbers as of 2026-05-03 HEAD) |
| --- | --- |
| `tests/scripts/smoke-witness.cs` | Lines 65-87 (`--emit-baseline` flag parsing), lines 512-550 (`EmitBaselineAsync`), lines 622-630 (`BaselineSignal` + `BaselineStep` records) silinir. `Build.Scripts.SmokeWitness` namespace'inden bu record'ların referansları çıkarılır. Line 122 (`PrintUnknownMode` help text'inde `--emit-baseline` satırı) silinir. `ParseArgs` return tipi `(SmokeMode? Mode, bool Verbose, string? BaselinePath)` → `(SmokeMode? Mode, bool Verbose)`. Main'in `if (baselinePath is not null)` block'u (lines 54-57) silinir |
| `tests/scripts/README.md` | `verify-baselines.cs` table row + bütün §`verify-baselines.cs` bölümü silinir; `--emit-baseline` flag mention'ları silinir; "Phase X baseline cadence" referansları silinir. (Wave B'de bu dosya tamamen siliniyor; Wave A'da kısmi clean.) |

### 8.4 New files

| Path | Content shape |
| --- | --- |
| `tools.cs` | Repo root. File-based .NET 10 app. Header: `#!/usr/bin/env dotnet`, `#:property TargetFramework=net10.0`, `#:property TargetFrameworks=`, `#:property PublishAot=false`, `#:property NoError=$(NoError);CA1502;CA1505;CA1031;CA1515;CA2007;CA1812;CA1869;IL2026;IL3050`, `#:property NoWarn=$(NoWarn);CA1502;CA1505;CA1031;CA1515;CA2007;CA1812;CA1869;IL2026;IL3050`, `#:package Spectre.Console`. Subcommand routing: ilk positional arg `build`, `setup`, veya `ci-sim`; bilinmeyen subcommand'da help yazıp exit 2. **Wave A'da yalnızca `build` implement edilir.** `setup` ve `ci-sim` **explicit stub** — exit 64 + "not yet implemented in Wave A — see Phase Y plan §10" mesajı. Repo root resolution `git rev-parse --show-toplevel` ile (smoke-witness'tan birebir reuse). Wave A scope'u: router + `build` direct forwarder + explicit stub'lar; shared process helper opsiyonel. `.logs/tools/...` orchestration logging, verbose tee, and step-summary table Wave B'de `setup` / `ci-sim` full implementation ile aktif olur (yeni record adları: `ToolsContext` / `StepResult` / `StepFailedException` — namespace `Build.Scripts.Tools`). |

### 8.5 `tools build` semantics

```pwsh
dotnet run --file tools.cs -- build [<cake-args>...]   # Windows / cross-platform
./tools.cs build [<cake-args>...]                       # Unix (chmod +x ile)
```

```text
→ dotnet run --project build/_build/Build.csproj --configuration Release -- <cake-args>
```

Argv passthrough birebir. Cake'in `--tree`, `--target`, `--rid`, `--versions-file`, `--explicit-version`, `--description`, `--info`, `--help` flag'leri tools.cs tarafından **görmezden gelinir**, doğrudan Cake'e iletilir. `tools build`'un kendi flag'i yok. Exit code = Cake'in exit code'u.

Log persistence: `tools build` için **yok** — konsola direkt teelenir (Cake'in kendi UI'ı zaten var). Bu `setup` ve `ci-sim`'den farklı: oradaki `.logs/tools/...` mekanizması orchestration için, `build` saf forwarder olduğu için gereksiz.

### 8.6 Unix shebang + executable bit

```bash
chmod +x tools.cs
git update-index --chmod=+x tools.cs
git add tools.cs
git commit
```

`.gitattributes` zaten `*.cs text eol=lf` taşıyor (smoke-witness pattern); ek satır gerekmiyor. Windows'ta `dotnet run --file tools.cs -- build ...` formu çalışıyor.

### 8.7 Wave A doc updates

| Doc | Update |
| --- | --- |
| `docs/phases/phase-x-build-host-modernization-2026-05-02.md` | §2.1 başına "**Retired by Phase Y Wave A (2026-05-03)**" amendment header'ı eklenir; §2.1.4'e ekleme "Wave A bu flag'i kaldırdı"; §2.1.5 wording'i `tools ci-sim` + manual diff cümlesine geçer |
| `.gitignore` | `.logs/tools/` ekle (Wave B'de `tools setup` ve `tools ci-sim` bu klasöre log yazacak; `.logs/witness/` Wave B'de silinir) |
| `CLAUDE.md` | "Common Commands" bölümünde mevcut `dotnet run --project build/_build` örneklerinin yanına opsiyonel olarak `dotnet run --file tools.cs -- build` shorthand'i eklenir (henüz canonical yapmadan) |

### 8.8 Wave A success criteria

- `dotnet run --file tools.cs -- build --target Info` ve `./tools.cs build --target Info` (Unix) cake info'yu döker.
- `dotnet run --file tools.cs -- build --target Coverage-Check` `dotnet run --project build/_build -- --target Coverage-Check` ile birebir aynı output.
- `dotnet run --file tools.cs -- build --tree` Cake'in target tree'sini döker.
- `dotnet run --file tools.cs -- setup` exit 64 + "not yet implemented in Wave A" mesajı.
- `dotnet run --file tools.cs -- ci-sim` exit 64 + "not yet implemented in Wave A" mesajı.
- `release.yml` etkilenmiyor (Cake host artifact aynı).
- Build host test suite **515/515 yeşil** (Wave A Cake host'a dokunmuyor, dolayısıyla test sayısı değişmiyor).
- `dotnet run --file tests/scripts/smoke-witness.cs -- ci-sim` host RID'inde başarıyla çalışıyor (regresyon yok; baseline flag silinmesi witness'ı bozmadı).
- `tests/scripts/baselines/` klasörü silinmiş; `verify-baselines.cs` silinmiş.
- Mevcut `./build.ps1 --target Info` ve `./build.sh --target Info` aynı şekilde çalışıyor (regresyon yok).
- **Bonus**: `OrchestrationFeatureAllowlist`'i geçici olarak `[]`'a indirip ArchitectureTests çalıştırılır → cross-feature sızıntı LocalDev dışında yoksa Wave B kıyametine girmeden temizlik kanıtı; sızıntı varsa Wave A çıkışı yapılmadan temizlenir. Bu test geçici, commit'lenmez.

---

## 9. Wave B — Atomic dev-tools cut-over

Bu wave **tek atomic commit**. Aşağıdaki dosya silmeleri, eklemeleri, edit'leri ve doc güncellemeleri tek bir branch + tek bir commit'te land eder. Bölünmez.

### 9.1 Goal

`tools setup` ve `tools ci-sim` subcommand'ları çalışır hale gelir. Aynı commit'te Cake host'tan LocalDev + ArtifactSourceResolvers + ArtifactProfile + JansetLocalPropsWriter + VersionsFileWriter + PackageOptions + `--source` CLI option silinir. Janset.Local.props writer mantığı + versions.json yazıcısı `tools.cs setup`'a taşınır. ADR-004 + Phase X plan amend edilir. ArchitectureTests sıkılaşır. Coverage baseline reset.

### 9.2 New code in `tools.cs`

| Subcommand / flag | Behavior |
| --- | --- |
| `tools setup` | Default `--source=local`. CleanArtifacts default ON. |
| `tools setup --source=local` | 1) `tools build --target CleanArtifacts` (skipped if `--no-clean`). 2) Concrete-family scope hesapla — manifest.json'u `git rev-parse --show-toplevel`/build/manifest.json'dan oku, `package_families[]` içinden hem `managed_project` hem `native_project` non-empty olanları al (bkz. §9.3). 3) `tools build --target ResolveVersions --version-source=manifest --suffix=local.{ts}` repeated `--scope <family>` (her concrete family için). Cake `artifacts/resolve-versions/versions.json` üretir. 4) `tools build --target PreFlightCheck --versions-file artifacts/resolve-versions/versions.json`. 5) `tools build --target EnsureVcpkgDependencies --rid {host}`. 6) `tools build --target Harvest --rid {host}`. 7) `tools build --target ConsolidateHarvest`. 8) `tools build --target Package --versions-file artifacts/resolve-versions/versions.json`. 9) Verify nupkgs exist (script-side `File.Exists` walk over `artifacts/packages/`, per concrete-family managed + native pair). 10) Write `build/msbuild/Janset.Local.props` from versions.json (script-side trivial XElement build, parity with deleted `JansetLocalPropsWriter.BuildContent`). 11) Print summary. |
| `tools setup --source=remote-github` | 1) `tools build --target CleanArtifacts` (skipped if `--no-clean`). 2) GitHub Packages NuGet feed'inden discovery (NuGet.Protocol via `#:package NuGet.Protocol`): her concrete family için latest version. 3) Per-family managed + native nupkg pair'ları `artifacts/packages/`'a download. 4) versions.json script-side write (parity with deleted `VersionsFileWriter.WriteAsync`: sorted by family OrdinalIgnoreCase, normalized SemVer). 5) Janset.Local.props script-side write. 6) Print summary. `GH_TOKEN` env var şart (Classic PAT, `read:packages`). Bkz. §9.4 porting checklist. |
| `tools setup --source=remote-nuget` | Stub — exit 64 + mesaj: "remote-nuget source not yet implemented (Phase 2b PD-7 territory)". CleanArtifacts atılmaz, Janset.Local.props yazılmaz. |
| `tools setup --no-clean` | Tüm `--source` value'larında step 1 (CleanArtifacts) atlanır. Geri kalan davranış aynı. |
| `tools ci-sim` | smoke-witness `ci-sim` modunun birebir portu. Sıralı target çağrıları: CleanArtifacts → ResolveVersions (`--version-source=manifest --suffix=local.{ts}` + repeated `--scope <family>` for concrete families) → PreFlightCheck → EnsureVcpkgDependencies → Harvest → NativeSmoke → ConsolidateHarvest → Package → PackageConsumerSmoke. `versions.json` Cake tarafından üretilir, sonraki adımlara `--versions-file` ile beslenir. |
| `tools ci-sim --verbose` | Step output console'a tee edilir (smoke-witness `--verbose` davranışı). |
| Common UX | Spectre UI (rule + figlet + summary table), per-step log dosyaları `.logs/tools/<platform>-<subcommand>-<runid>/NN-<step>.log`, step-fail-halt. Smoke-witness'tan birebir taşınır, sadece klasör adı `.logs/witness/` → `.logs/tools/`. |

### 9.3 Concrete-family scope rule (R1.4 fix)

Mevcut [`SetupLocalDevFlow.cs:103-121`](../../build/_build/Features/LocalDev/SetupLocalDevFlow.cs#L103-L121) concrete-family filtering yapıyor:

```csharp
var concreteFamilies = _manifestConfig.PackageFamilies
    .Where(family => !string.IsNullOrWhiteSpace(family.ManagedProject) && !string.IsNullOrWhiteSpace(family.NativeProject))
    .ToList();
```

Mevcut [`ResolveVersionsPipeline`](../../build/_build/Features/Versioning/ResolveVersionsPipeline.cs) ve [`ManifestVersionProvider:47-67`](../../build/_build/Features/Versioning/ManifestVersionProvider.cs#L47-L67) ise empty scope'ta **tüm `manifest.PackageFamilies`'i** resolve ediyor. Mevcut manifest yalnızca concrete family'leri taşıdığı için fark görünmüyor; ama plan'ın doğruluğu için `tools setup` Cake target'ına **explicit scope** geçmek zorunda.

`tools setup`'ın yapacağı:

1. `manifest.json`'u read.
2. `package_families[]` filter: `managed_project` non-empty AND `native_project` non-empty.
3. Resulting family name list'i `--scope` repeated arg olarak ResolveVersions çağrısına geç.

```bash
tools build --target ResolveVersions --version-source=manifest --suffix=local.{ts} \
  --scope sdl2-core --scope sdl2-image --scope sdl2-mixer --scope sdl2-ttf --scope sdl2-gfx
```

JSON parsing: System.Text.Json (built-in, ek package gerek yok). Manifest schema v2.1 — `tools.cs` sadece `package_families[].name`, `.managed_project`, `.native_project` field'larını okur, partial deserialize.

### 9.4 Remote GitHub porting contract (R1.6 fix)

`RemoteArtifactSourceResolver` siliniyor. Davranışın `tools setup --source=remote-github`'a aynen taşınması gereken bullet'lar (kod silinmeden önce review edilip script'e port edilir):

- **Auth fallback**: `GH_TOKEN` env var önce, yoksa `GITHUB_TOKEN` env var. Stale `gh` CLI token'ı kabul edilmez — env-var birinci sınıf source.
- **Feed URL**: `https://nuget.pkg.github.com/janset2d/index.json` (hard-coded, Phase 2b'de değişmez).
- **Prerelease inclusion**: NuGet.Protocol query'si `includePrerelease: true` ile çalışır — published `local.*` veya `ci.*` versionları discovery'ye dahil olur (zaten o feed'e local-pack pushable değil, ama policy önemli).
- **Concrete-family filtering**: §9.3'te tanımlı kuralla aynı — sadece `managed_project + native_project` taşıyan family'ler. Diğerleri discover edilmez.
- **Latest-per-family**: Her family için published version'ları al, `NuGetVersion.Compare` ile en büyük olanı seç. Ties (impossible — aynı feed'de aynı version iki kere olamaz) için doc gerekmiyor.
- **Managed/native version equality**: Bir family için `Janset.SDL2.<Role>` (managed) ve `Janset.SDL2.<Role>.Native` (native) package'larının latest version'ları **aynı olmalı**. Aynı değilse — operator-friendly error: "managed Janset.SDL2.Core@2.32.1 and native Janset.SDL2.Core.Native@2.32.0 versions disagree; likely a partial publish. Re-run the failing publish wave or supply explicit versions."
- **Download target**: `artifacts/packages/<family-managed-id>.<version>.nupkg` ve `artifacts/packages/<family-native-id>.<version>.nupkg`.
- **Wipe-on-prepare**: Download'dan önce `artifacts/packages/` içindeki `Janset.SDL2.*` / `Janset.SDL3.*` glob'una uyan dosyalar silinir (stale local-pack'leri remote-pull'a karıştırma). `CleanArtifacts` zaten clean state veriyor (default ON), ama `--no-clean` modu için bu safety guard önemli.
- **versions.json output**: Sorted by family name (OrdinalIgnoreCase), `NuGetVersion.ToNormalizedString()` formatında.
- **Operator-friendly errors**: 401/403 → "GH_TOKEN scope eksik; Classic PAT + read:packages gerek. Fine-grained PATs unsupported by GH Packages NuGet"; 404 family-version → "Family `<x>` has no published version on GH Packages — first publish wave hasn't shipped yet"; net error → "Network failure querying GH Packages; retry, or check feed availability".

Wave B implementation order: önce porting checklist'i bullet-by-bullet `tools.cs setup`'a yaz, sonra silmeyi yap. Silindikten sonra discover edilirse junior'a bedel.

### 9.5 Janset.Local.props writer port

Mevcut [`JansetLocalPropsWriter.BuildContent`](../../build/_build/Features/Packaging/JansetLocalPropsWriter.cs#L32-L53) (deleted in Wave B) script tarafına XElement bazlı taşınır:

```xml
<?xml version="1.0" encoding="utf-8"?>
<Project>
  <PropertyGroup>
    <LocalPackageFeed>{repoRoot}/artifacts/packages</LocalPackageFeed>
    <JansetSdl2CorePackageVersion>{version}</JansetSdl2CorePackageVersion>
    <JansetSdl2ImagePackageVersion>{version}</JansetSdl2ImagePackageVersion>
    <!-- ... per concrete family, sorted by family name OrdinalIgnoreCase -->
  </PropertyGroup>
</Project>
```

Property naming: `FamilyIdentifierConventions.VersionPropertyName(familyName)` — bu helper Shared/Packaging'de kalıyor (LocalDev/Packaging'in dışında, dokunulmaz). `tools.cs` bu helper'ı çağıramaz (file-based app project reference yapamıyor); **convention'ı script tarafında inline replicate edilir** — pattern: `JansetSdl<Major><Role>PackageVersion`, `family-name` = `sdl<major>-<role>` parsing'iyle elde edilir.

`build/msbuild/Janset.Local.props` path'i tools.cs içinde sabit string olarak (`Path.Combine(repoRoot, "build", "msbuild", "Janset.Local.props")`). `Janset.Smoke.props` `Condition="Exists(...)"` ile import ettiği için dosyanın varlığı opsiyonel — `--source=remote-nuget` stub'da yazılmıyor olabilir.

### 9.6 versions.json writer port

Mevcut [`VersionsFileWriter.WriteAsync`](../../build/_build/Features/Packaging/VersionsFileWriter.cs#L8) (deleted in Wave B) script tarafına replicated. Format: JSON object, key = family name (OrdinalIgnoreCase ascending), value = `NuGetVersion.ToNormalizedString()`. Path: `artifacts/resolve-versions/versions.json`. System.Text.Json yeterli (dictionary serialize, indented format opsiyonel — Cake'in output'una match etmek için indented kullanılabilir).

Bu writer **sadece** `--source=remote-github` path'inde kullanılır. `--source=local` path'i `ResolveVersions` Cake target'ı üzerinden ilerlediği için Cake'in kendi versions.json yazıcısı (ResolveVersionsPipeline) zaten yazıyor.

### 9.7 Production file deletes (Cake host)

| Path | Reason | Test count etkisi |
| --- | --- | --- |
| `build/_build/Features/LocalDev/SetupLocalDevTask.cs` | Task siliniyor (orchestration script'te) | — |
| `build/_build/Features/LocalDev/SetupLocalDevFlow.cs` | Flow siliniyor (orchestration script'te) | — |
| `build/_build/Features/LocalDev/ServiceCollectionExtensions.cs` | DI registration siliniyor | — |
| `build/_build/Features/LocalDev/` (klasör) | Boşalıyor → siliniyor | — |
| `build/_build/Features/Packaging/ArtifactSourceResolvers/IArtifactSourceResolver.cs` | Seam script'in concern'i | — |
| `build/_build/Features/Packaging/ArtifactSourceResolvers/ArtifactSourceResolverFactory.cs` | Factory dispatch DI'dan kalkıyor | — |
| `build/_build/Features/Packaging/ArtifactSourceResolvers/LocalArtifactSourceResolver.cs` | Verify + props yazımı script'te | — |
| `build/_build/Features/Packaging/ArtifactSourceResolvers/RemoteArtifactSourceResolver.cs` | NuGet client çağrısı script'te | — |
| `build/_build/Features/Packaging/ArtifactSourceResolvers/UnsupportedArtifactSourceResolver.cs` | Sentinel resolver gereksiz | — |
| `build/_build/Features/Packaging/ArtifactSourceResolvers/` (klasör) | Boşalıyor → siliniyor | — |
| `build/_build/Features/Packaging/ArtifactProfile.cs` | Yalnız resolver'lar kullanıyordu | — |
| `build/_build/Features/Packaging/JansetLocalPropsWriter.cs` | Sadece resolver'lar caller'ı; resolver'lar gidince orphan | — |
| `build/_build/Features/Packaging/VersionsFileWriter.cs` | Caller'lar: SetupLocalDevFlow.cs:83 + RemoteArtifactSourceResolver.cs:113. İkisi de gidince orphan. ResolveVersionsPipeline kendi yazıcısını kullanıyor | — |
| `build/_build/Host/Cli/Options/PackageOptions.cs` | Sadece `SourceOption` taşıyor; başka option yok → komple delete | — |

### 9.8 Production file edits (Cake host)

| Path | Edit |
| --- | --- |
| `build/_build/Program.cs` | **Line 14** `using Build.Features.LocalDev;` → silinir. **Line 23** `using Build.Host.Cli.Options;` → **kalır** (VersioningOptions hâlâ orada, lines 56-60). **Line 54** `root.AddOption(PackageOptions.SourceOption);` → silinir. **Lines 126-130** (`var source = parsedArgs.Source?.Trim();` + validation block) → silinir. **Line 154** `.AddPackagingFeature(source)` → `.AddPackagingFeature()`. **Line 290** `string Source,` (ParsedArguments record field) → silinir |
| `build/_build/Features/Packaging/ServiceCollectionExtensions.cs` | **Line 1** `using Build.Features.Packaging.ArtifactSourceResolvers;` → silinir. **Line 26** `AddPackagingFeature(this IServiceCollection services, string source)` → `AddPackagingFeature(this IServiceCollection services)`. `ArgumentException.ThrowIfNullOrWhiteSpace(source)` (line 29) → silinir. Lines 47-51 (Local/Remote/Factory + IArtifactSourceResolver registration block) → silinir. Lines 19-25 yorum güncellenir veya silinir |
| `build/_build/Host/ServiceCollectionExtensions.cs` | **Line 27** XML doc yorumunda `AddPackagingFeature(string source)` mention'ı `AddPackagingFeature()` haline güncellenir. (Bu dosya `AddPackagingFeature` çağırmıyor; sadece yorumda bahsi geçiyor.) |
| `build/_build/Host/Paths/IPathService.cs` | **Line 159** `FilePath GetLocalPropsFile();` → silinir. Lines 149-158 civarı XML doc'ta `SetupLocalDev --source=local` referansı kaldırılır |
| `build/_build/Host/Paths/PathService.cs` | **Lines 291-293** `public FilePath GetLocalPropsFile() { ... }` implementation → silinir |
| `build/_build/Features/Packaging/PackageConsumerSmokePipeline.cs` | **Line 62 yorum** "keeps package version injection on a single path" — yorum güncellenir; davranış değişmiyor. Janset.Local.props'a referans eden diğer yorumlar gözden geçirilir |
| `build/_build/Features/Packaging/PackageConsumerSmokeTask.cs` | Janset.Local.props mention varsa yorumlar güncellenir |
| `build/_build/Features/Packaging/PackageTask.cs` | Aynı |
| `build/_build/Features/Vcpkg/EnsureVcpkgDependenciesPipeline.cs` | Aynı |
| `build/_build/Features/Publishing/PublishPipeline.cs` | `INuGetFeedClient` kullanımı **kalıyor** (Publishing tarafından kullanılıyor). Sadece varsa Janset.Local.props yorum referansı silinir |
| `build/_build/Integrations/NuGet/INuGetFeedClient.cs` | **Dokunulmuyor** — Publishing kullanıyor, kalmak zorunda |
| `build/_build/Integrations/NuGet/NuGetProtocolFeedClient.cs` | **Dokunulmuyor** — yukarıdaki sebep |
| `build/_build/Host/Configuration/PackageBuildConfiguration.cs` | **Dokunulmuyor** — `Source` property mevcut değil; reviewer 2'nin "varsa" hedge'i false alarm |
| `tools.cs` | Wave A'da eklenen subcommand router'a `setup` ve `ci-sim` implementation'ları eklenir (§9.2/§9.3/§9.4/§9.5/§9.6) |

### 9.9 Test file deletes (full file deletion)

Toplam **26 test** silinen dosyalardan gidiyor.

| Path | Test count |
| --- | --- |
| `build/_build.Tests/Unit/Features/LocalDev/SetupLocalDevFlowTests.cs` | 4 |
| `build/_build.Tests/Unit/Features/LocalDev/` (klasör) | — |
| `build/_build.Tests/Unit/Features/Packaging/LocalArtifactSourceResolverTests.cs` | 5 |
| `build/_build.Tests/Unit/Features/Packaging/RemoteArtifactSourceResolverTests.cs` | 12 |
| `build/_build.Tests/Unit/Features/Packaging/JansetLocalPropsWriterTests.cs` | 3 |
| `build/_build.Tests/Unit/Features/Packaging/VersionsFileWriterTests.cs` | 2 |
| **Total** | **26** |

### 9.10 Test file edits (method-level deletion + signature update)

Toplam **4 test** silinen method'lardan, ek olarak callsite update'leri.

| Path | Edit |
| --- | --- |
| `build/_build.Tests/Unit/CompositionRoot/ProgramCompositionRootTests.cs` | **Line 5** `using Build.Features.Packaging.ArtifactSourceResolvers;` → silinir. **Lines 237-260** `[Test] ConfigureBuildServices_Should_Resolve_Remote_Source_Resolver` method → silinir (1 test). **Lines 262-285** `[Test] ConfigureBuildServices_Should_Resolve_Release_Source_Resolver` → silinir (1 test). **Lines 287-303** `[Test] ConfigureBuildServices_Should_Throw_When_Source_Is_Whitespace` → silinir (1 test). **Lines 385-398** `CreateParsedArguments` helper'ında `string source = "local"` parameter ve constructor'da `Source: source` named arg → silinir (`source` parametresi kalkar). **Lines 251, 276, 301**: Bu satırlardaki `CreateParsedArguments(... "remote" / "release" / "   ")` çağrıları zaten silinen testlerle birlikte gidiyor. Diğer kalan testlerde `CreateParsedArguments(repo.RepoRoot.FullPath, "win-x64")` çağrıları (lines 155, 207) `source` arg'ı olmadan zaten çalışıyor (default kalkıyor) |
| `build/_build.Tests/Unit/Host/Paths/PathConstructionTests.cs` | **Lines 88-93** `[Test] GetLocalPropsFile_Should_Point_To_Build_Msbuild_Override` method → silinir (1 test) |
| `build/_build.Tests/Unit/CompositionRoot/ServiceCollectionExtensionsSmokeTests.cs` | 5 callsite update — **lines 70, 101, 115, 137, 154** `services.AddPackagingFeature("local")` → `services.AddPackagingFeature()`. **Line 128** test method adı `AddPackagingFeature_Should_Register_All_Pipeline_And_Validator_Types` (mevcut) — değişmez. Yorum (line 98) güncellenir |
| `build/_build.Tests/Unit/CompositionRoot/ArchitectureTests.cs` | §7'de detaylanan: line 39 (allowlist field) silinir, lines 14-16 XML doc rewrite, lines 114-115 method rename, lines 117-123 + 137-141 body update |

**Net test sayısı**: 515 - 26 (full file) - 4 (method-level) = **485**.

### 9.11 tests/scripts/ deletes

| Path | Action |
| --- | --- |
| `tests/scripts/smoke-witness.cs` | DELETE — `tools ci-sim` portu Wave B'de hazır |
| `tests/scripts/README.md` | DELETE (Wave A'da kısmen güncellendi; Wave B'de full delete) |
| `tests/scripts/` (klasör) | Boşalıyor → DELETE |

### 9.12 Build configuration edits

| Path | Edit |
| --- | --- |
| `build/coverage-baseline.json` | RESET — yeni baseline **485**. Coverage-Check Wave B kapanışında yeşil olmalı. Wave B'nin son adımı: `dotnet run --file tools.cs -- build --target Coverage-Check` yeşil |
| `build/_build/Build.csproj` | **Dokunulmuyor** — INuGetFeedClient Publishing kullandığı için NuGet.Protocol kalıyor; LocalDev/Resolvers feature'ları Cake host'un csproj'una package eklemiyordu, dolayısıyla siliyor da değil |
| `build/_build.Tests/Build.Tests.csproj` | **Dokunulmuyor** — package değişimi yok |
| `Directory.Packages.props` | **Dokunulmuyor** — central package versions list'inde değişiklik yok |

### 9.13 Documentation update catalog

#### Canonical decisions

| Doc | Update |
| --- | --- |
| `docs/decisions/2026-05-02-cake-native-feature-architecture.md` | §5'te detaylanan in-place rewrite — motto, §2.5 (Flow class), §2.13 invariant #4, §2.15 (komple delete), glossary, reference layout, DI chain örneği, motto satırı |
| `docs/decisions/2026-04-18-versioning-d3seg.md` | §2.7 "consumer-feed seam" wording'inde `IArtifactSourceResolver` referansı varsa amend: "consumer-feed seam responsibility → repo-root `tools.cs setup`". `--source release` retirement Phase Y kapanışı ile aynı tarih |
| `docs/decisions/2026-04-19-ddd-layering-build-host.md` | ADR-002 retired durumda — sadece historical referans; LocalDev mention varsa "Phase Y'da retired" footnote'u |
| `docs/decisions/2026-04-20-release-lifecycle-orchestration.md` | "Provider/scope/version axes" `IArtifactSourceResolver`'a değil `tools setup --source=...`'a bağlanıyor — wording amend |

#### Phases

| Doc | Update |
| --- | --- |
| `docs/phases/README.md` | Phase status overview'a Phase Y entry eklenir: status (Wave A/B), standalone scope-reduction track, Phase X P5'ten önce sequencing, ve link olarak `phase-y-dev-tools-extraction-2026-05-03.md`. Bu canonical phase katalogudur; top-level README'deki phase navigation varsa ayrıca güncellenir |
| `docs/phases/phase-x-build-host-modernization-2026-05-02.md` | §6'da detaylanan amendments — §1.2, §1.3, §1.4, §1.5 (P5 row), §1.6, §2.1.x line-by-line rewrite |
| `docs/phases/phase-x-modernization-2026-04-20.md` | Phase X'in eski sürümü — historical, sadece `SetupLocalDev` referansları "see phase-y" footnote'u |
| `docs/phases/phase-x-lightweight-dotnet-update.md` | Phase X tail — `SetupLocalDev` referansları update |
| `docs/phases/phase-2-adaptation-plan.md` | Phase 2 LocalDev mention'ları update |
| `docs/phases/phase-2-release-cycle-orchestration-implementation-plan.md` | Phase 2b PD-7 placeholder — `--source release` `tools setup` tarafına geleceğini belirten footnote |
| `docs/phases/phase-y-dev-tools-extraction-2026-05-03.md` | (Bu plan) — Wave A/B kapanış commit hash'leri eklendikçe status table güncellenir |

#### Playbooks

| Doc | Update |
| --- | --- |
| `docs/playbook/local-development.md` | **Heavy rewrite**. Tüm `--target SetupLocalDev --source=...` örnekleri `dotnet run --file tools.cs -- setup --source=...` veya `./tools.cs setup --source=...` haline dönüşür. `GH_TOKEN` setup recipe'ı `tools setup --source=remote-github` örneğinin yanında kalır. `--source release` paragrafı silinir. `Janset.Local.props` üretiminin `tools setup` tarafından yapıldığı vurgulanır |
| `docs/playbook/cross-platform-smoke-validation.md` | smoke-witness referansları `tools ci-sim` referanslarına dönüşür. A-K script bölümleri güncellenir. SetupLocalDev mention'ları update |
| `docs/playbook/unix-smoke-runbook.md` | Aynı şekilde — smoke-witness → tools ci-sim, SetupLocalDev → tools setup |
| `docs/playbook/overlay-management.md` | LocalDev mention varsa update (muhtemelen yok) |
| `docs/playbook/adding-new-library.md` | "Janset.Local.props ne zaman yenilenir" rehberi `tools setup` etrafına döner |

#### Knowledge base

| Doc | Update |
| --- | --- |
| `docs/knowledge-base/cake-build-architecture.md` | LocalDev kutusu silinir; Cake'in mission boundary'sinin `release.yml` ile sınırlı olduğu vurgulanır; `tools.cs`'in ayrı orchestration surface'i olduğu eklenir |
| `docs/knowledge-base/harvesting-process.md` | SetupLocalDev tetiklemesi `tools setup` referansına döner |
| `docs/knowledge-base/ci-cd-packaging-and-release-plan.md` | release.yml etkisiz; sadece varsa LocalDev wording'i temizlenir |
| `docs/knowledge-base/release-lifecycle-direction.md` | `--source release` Phase Y'da retired; ileride `tools setup --source=release` |

#### Top-level

| Doc | Update |
| --- | --- |
| `CLAUDE.md` | "Common Commands" bölümü: tüm `--target SetupLocalDev` örnekleri `dotnet run --file tools.cs -- setup --source=local` ve `./tools.cs setup --source=local`'a döner. Cake host layout tablosunda `Features/LocalDev/` satırı silinir. "tools.cs at repo root — dev orchestration entry point" cümlesi eklenir |
| `AGENTS.md` | LocalDev'i build-host reference pattern olarak kullanan mention'lar varsa update; "Cake host serves CI; dev orchestration is `tools.cs`" satırı eklenir; approval gate listesinde `tools.cs` script (Cake'le aynı seviyede approval gate'e tabi) belirtilir |
| `docs/onboarding.md` | Setup recipe'ı `tools setup --source=local` etrafına döner; `tools ci-sim` mini-CI replay olarak tanıtılır |
| `docs/plan.md` | Active phase satırı: Phase X (P5 küçültülmüş) + Phase Y (Wave A/B status). Roadmap'te Phase Y kapanışı + sonraki Phase X P5 |
| `README.md` | Quick-start `tools setup --source=local` ile başlar; `build.ps1`/`build.sh` değişmez; smoke-witness mention'ı silinir. **Phase navigation tablosu / phase index'i varsa Phase Y entry eklenir** (R1.7 fix) |

#### Build / msbuild

| Doc | Update |
| --- | --- |
| `build/msbuild/Janset.Smoke.props` (yorumlar) | Lines 12-17 ve 97-110: "SetupLocalDev's generated Janset.Local.props" → "`tools setup`'s generated Janset.Local.props". Davranış değişmiyor |
| `build/msbuild/Janset.Smoke.targets` (yorumlar) | Aynı şekilde — yorum update |

#### Tests

| Doc | Update |
| --- | --- |
| `tests/smoke-tests/README.md` | SetupLocalDev → tools setup wording update |
| `tests/Sandbox/Sandbox.csproj` (yorum varsa) | SetupLocalDev mention update |
| `tests/smoke-tests/native-smoke/README.md` | SetupLocalDev wording (varsa) update |

#### Archive / historical

| Path | Update |
| --- | --- |
| `docs/_archive/*.md` | **Dokunulmaz** — historical record |
| `.github/prompts/*.md` | **Dokunulmaz** — historical session prompt'ları |

### 9.14 Wave B success criteria

- `dotnet run --file tools.cs -- setup --source=local` → 5 family × 3 nupkg üretildi, versions.json yazıldı, Janset.Local.props yazıldı, exit 0
- `dotnet run --file tools.cs -- setup --source=local --no-clean` → CleanArtifacts atlanır, geri kalan adımlar aynı, exit 0
- `dotnet run --file tools.cs -- setup --source=remote-github` (GH_TOKEN ile) → GitHub Packages'tan latest discovery + download + props yazımı, exit 0
- `dotnet run --file tools.cs -- setup --source=remote-nuget` → exit 64 + "not yet implemented" mesajı (props yazılmaz, CleanArtifacts atılmaz)
- `dotnet run --file tools.cs -- ci-sim` → 9 sıralı target call, hepsi yeşil, summary table OK
- `dotnet run --file tools.cs -- ci-sim --verbose` → live tee + log persistence
- `tools setup --source=local` post-Wave-B output'u Wave B öncesi `smoke-witness local` mode output'una semantik denk (CleanArtifacts + SetupLocalDev kısmı; smoke-witness'ın 3. adımı PackageConsumerSmoke `tools ci-sim`'in son adımı, `tools setup` kapsamında değil). `artifacts/packages/` aynı dosya seti, `versions.json` aynı format, `Janset.Local.props` aynı XML
- `tools ci-sim` post-Wave-B step listesi pre-Wave-B `smoke-witness ci-sim` step listesine birebir eşit
- Build host test suite — silinen testler hariç **485/485 yeşil**; `ArchitectureTests.Features_Should_Not_Cross_Reference` allowlist'siz halinde yeşil
- `release.yml` workflow_dispatch dry-run yeşil (mode=manifest-derived, publish-staging=false; Cake host artifact'ı içinde `--target SetupLocalDev` artık yok ama release.yml zaten çağırmıyordu)
- `dotnet build Janset.SDL2.sln` solution-level build IDE'de Janset.Local.props imported, smoke restore yeşil (post-`tools setup`)
- ADR-004, Phase X plan, ve canonical doc'lar güncellenmiş; broken-link checker (varsa) yeşil
- Coverage baseline reset edilmiş; `dotnet run --file tools.cs -- build --target Coverage-Check` yeşil
- `tests/scripts/` klasörü tamamen silinmiş; `.logs/witness/` artifacts'ı varsa gitignored kalır (hiç commit'lenmiyor zaten)

---

## 10. Symbol checklist (junior handoff helper)

Aşağıdaki tablo Wave B implementation sırasında her edit/delete için tam grep pattern + line referansı:

| Symbol / pattern | Where | Action |
| --- | --- | --- |
| `Build.Features.LocalDev` (namespace) | All `using` statements + namespace declarations | Delete with namespace |
| `Build.Features.Packaging.ArtifactSourceResolvers` (namespace) | All `using` statements | Delete |
| `IArtifactSourceResolver` (type) | All references | Delete with type |
| `ArtifactProfile` (enum) | All references | Delete with type |
| `LocalArtifactSourceResolver` / `RemoteArtifactSourceResolver` / `UnsupportedArtifactSourceResolver` (types) | All references | Delete with type |
| `ArtifactSourceResolverFactory` (type) | All references | Delete with type |
| `JansetLocalPropsWriter` (type) | LocalArtifactSourceResolver.cs:95, RemoteArtifactSourceResolver.cs:139 (both deleted) | File delete |
| `VersionsFileWriter` (type) | SetupLocalDevFlow.cs:83, RemoteArtifactSourceResolver.cs:113 (both deleted) | File delete |
| `GetLocalPropsFile` (method) | IPathService.cs:159, PathService.cs:291, deleted resolvers | Delete from interface + impl; resolvers already going |
| `PackageOptions` (type) | Program.cs:54 | File delete; Program.cs line silinir |
| `PackageOptions.SourceOption` (field) | Program.cs:54 | Same as above |
| `ParsedArguments.Source` (record field) | Program.cs:290; ProgramCompositionRootTests.cs:387, 393 | Field delete; helper signature update |
| `parsedArgs.Source` (access) | Program.cs:126 | Delete |
| `AddPackagingFeature(string source)` (overload) | Features/Packaging/ServiceCollectionExtensions.cs:26 | Signature change to `()` |
| `AddPackagingFeature("local")` / `AddPackagingFeature(...)` (callsite) | Program.cs:154; ServiceCollectionExtensionsSmokeTests.cs:70, 101, 115, 137, 154 | Call argument removed |
| `--source` (CLI flag) | Program.cs:54 (registration), 126-130 (parse), error messages | Delete all |
| `OrchestrationFeatureAllowlist` (field) | ArchitectureTests.cs:39 | Delete |
| `Features_Should_Not_Cross_Reference_Except_From_LocalDev` (test method) | ArchitectureTests.cs:115 | Rename to `Features_Should_Not_Cross_Reference` |
| `--emit-baseline` (CLI flag) | smoke-witness.cs:65-87 (parsing), 122 (help), 512-550 (impl) | Wave A: delete from smoke-witness. Wave B: smoke-witness file goes |
| `BaselineSignal` / `BaselineStep` (records) | smoke-witness.cs:622-630 | Wave A: delete |
| `tests/scripts/baselines/` | All 6 files + dir | Wave A delete |
| `verify-baselines.cs` | tests/scripts/verify-baselines.cs | Wave A delete |

---

## 11. Validation gates per wave

### 11.1 Junior local gates (developer-runnable)

Her wave kapanışında dev makine'de tek tek koşulup yeşil olmalı:

```pwsh
# Build + test gate
dotnet run --file tools.cs -- build --target Coverage-Check       # post-Wave-A: ratchet 515; post-Wave-B: ratchet 485

# Cake target tree (mevcut target'ların hâlâ olduğunu doğrula)
dotnet run --file tools.cs -- build --tree

# Solution-level build (post-tools setup)
dotnet build Janset.SDL2.sln

# Phase Y subcommand smokes
dotnet run --file tools.cs -- setup --source=local                # Wave B sonrası: yeşil
dotnet run --file tools.cs -- setup --source=local --no-clean      # Wave B sonrası: yeşil
dotnet run --file tools.cs -- setup --source=remote-nuget         # Wave B sonrası: exit 64
dotnet run --file tools.cs -- ci-sim                              # Wave B sonrası: 9 step PASS

# (Wave A only) smoke-witness still works without baseline flag
dotnet run --file tests/scripts/smoke-witness.cs -- ci-sim
```

Wave B kapanışında ek olarak:

```pwsh
# Pre-Wave-B baseline capture (Wave B çalışmasına başlamadan ÖNCE):
dotnet run --file tests/scripts/smoke-witness.cs -- ci-sim 2>&1 | tee pre-wave-b-ci-sim.log

# Post-Wave-B parity check:
dotnet run --file tools.cs -- ci-sim 2>&1 | tee post-wave-b-ci-sim.log

# Step listesi + exit code'lar manuel diff edilir.
```

### 11.2 Maintainer GitHub dispatch gate

Her wave kapanışında release.yml'in workflow_dispatch ile manuel-tetiği maintainer tarafından çalıştırılır:

- **Trigger**: `gh workflow run release.yml -f mode=manifest-derived -f publish-staging=false`
  - `mode=manifest-derived`: ResolveVersions manifest-tabanlı, `local.{run-id}` suffix.
  - `publish-staging=false`: PublishStaging job skip; sadece pack + consumer-smoke pipeline'ı koşar.
- **Owner**: maintainer (Deniz veya devredilmiş onaylı dev). Junior-sole çalıştırmaz.
- **Expected outcome**: Required logical jobs pass: `build-cake-host`, `resolve-versions`, `preflight`, `generate-matrix`, `harvest` 7/7 matrix executions, `consolidate-harvest`, `pack`, and `consumer-smoke` 7/7 matrix executions. `publish-staging` is skipped because `publish-staging=false`; `publish-public` is skipped because the job is still `if: false` until PD-7.
- **Failure interpretation**: Wave Y'nin Cake-side delta'sı release.yml semantiğini kırdı demektir. Wave commit'i revert + root-cause analizi.

Junior dev bu gate'i kendi başına atmaz — maintainer ile coordination şart.

---

## 12. Rollback granularity

| Wave | Rollback shape |
| --- | --- |
| **A** | `git revert` Wave A commit'i: `tests/scripts/baselines/`, `verify-baselines.cs`, smoke-witness `--emit-baseline` flag'i geri gelir; `tools.cs` ve `.gitignore` değişimi geri gider. Hiçbir prod davranış etkilenmediği için bağımsız revert güvenli |
| **B** | `git revert` Wave B commit'i = full restore. LocalDev + ArtifactSourceResolvers + ArtifactProfile + JansetLocalPropsWriter + VersionsFileWriter + PackageOptions + `--source` CLI option + smoke-witness + tüm doc state'i geri gelir. Yan etki: `tools setup` ve `tools ci-sim` "not yet implemented" haline geri döner; `tools build` sürer (Wave A). ADR-004 + Phase X plan amendments revert olur |

Wave A ve Wave B birbirinden bağımsız atomic'ler. Wave B sırasında problem çıkarsa Wave A kaybolmaz.

---

## 13. Risks

| # | Risk | Mitigation |
| --- | --- | --- |
| 1 | `tools setup --source=remote-github` NuGet.Protocol bağımlılığı script'in ilk compile'ını uzatır | File-based app cache `~/.dotnet`. İlk run yavaş, sonraki run'lar hızlı. Ergonomik tradeoff kabul edilebilir; eğer kötü görünürse Wave B sonrası `dotnet publish tools.cs --output ~/.local/bin/tools` fallback paragrafı doc'a eklenebilir |
| 2 | Janset.Local.props XML üretiminin Cake'tekinden çok küçük bir farkı IDE smoke restore'unu kırabilir | Wave B öncesi — pre-Wave-B `JansetLocalPropsWriter.BuildContent` output'u byte-by-byte snapshot'lanır; Wave B `tools setup --source=local` output'u aynı snapshot ile diff edilir. Tek difference: trailing whitespace/EOL. Diff sıfır olmadan Wave B land etmez. Aynı kontrol versions.json için: `VersionsFileWriter` output snapshot'lanır, post-Wave-B output diff'lenir |
| 3 | `tools ci-sim` step ordering veya target çağrı argv'sinde subtle drift | §11.1 son blok: pre-Wave-B `smoke-witness ci-sim` console output kaydedilir; post-Wave-B `tools ci-sim` aynı şekilde; satır-satır diff (timestamp / runId / Cake progress satırları normalize edilir). Drift sıfır olmalı |
| 4 | `release.yml`'in cake-host artifact build'i `--target SetupLocalDev` olmadığı için bozulur | Çok düşük risk — release.yml hiçbir job'ı `SetupLocalDev` çağırmıyor. Coverage-Check ratchet düşmez (yeni baseline 485 shipped). §11.2 maintainer gate'i Wave A ve Wave B kapanışında çalıştırılır |
| 5 | `--source` CLI option silindiğinde Program.cs'in System.CommandLine binding chain'i kırılır | Wave B öncesi Program.cs full audit (line 14, 23, 54, 126-130, 154, 290 satırlarının hepsi listede); Cake host build clean (treat-warnings-as-errors kalkanı zaten var) |
| 6 | ArchitectureTests invariant #4 sıkılaşırken pre-Wave-B koddan sızmış bir cross-feature reference yakalanır | Wave A son adımı: `OrchestrationFeatureAllowlist`'i geçici olarak `[]`'a indirip test çalıştır → eğer LocalDev dışı sızıntı varsa Wave B öncesi temizlenir. Bu test commit'lenmez, geçici |
| 7 | `JansetLocalPropsWriter`'ın `FamilyIdentifierConventions.VersionPropertyName` çağrısı script'e taşınınca kaybolur | Convention pattern script'te inline replicate edilir (§9.5). Wave B implementation aşamasında 5 known concrete-family için (sdl2-core, sdl2-image, sdl2-mixer, sdl2-ttf, sdl2-gfx) output property name'i pre-Wave-B ile diff'lenir; equality şart |
| 8 | Concrete-family scope filtering tools.cs'te yanlış uygulanırsa ResolveVersions tüm family'leri resolve eder | §9.3'teki rule explicit: manifest.json read + `package_families[]` filter on `managed_project + native_project` non-empty + repeated `--scope` arg. Implementation review edilmeden Wave B kapanmaz |
| 9 | `verify-baselines.cs` ve baseline file'ları ile diğer doc'ların referansları wave A'dan sonra dangling kalır | Wave A doc updates eksiksiz — phase-x §2.1.x line-by-line rewrite, tests/scripts/README.md kısmi clean (Wave B'de full delete), playbook'lar tek seferde temizlenir; broken-link checker (varsa) Wave A kapanışında çalıştırılır |
| 10 | Coverage baseline reset alt limiti yeni testleri kabul edersek geriden gelen coverage drop'a kapı açar | Wave B kapanışında `Coverage-Check`'in yeşil olduğu doğrulanır; baseline yeni minimum'da kilitlenir (485); sonraki phase'lerde drop'a izin verilmez |
| 11 | `tools.cs` Spectre.Console + NuGet.Protocol package directive'leri çakışırsa veya version mismatch çıkarsa | NuGet.Protocol son stable, Spectre.Console son stable — file-based app `#:package` direktifleri NuGet'in resolution algorithm'ını kullanır, çakışma riski düşük. Wave B implementation'ında `tools.cs` ilk compile'ı yapılır + tüm subcommand'lar smoke edilir, sonra Cake silmesi gelir |

---

## 14. Open questions / explicitly deferred

- **`tools setup --versions-file` veya `--versions <family>=<semver>` flag'leri**: Phase Y scope dışı. Latest-only şimdilik. Gelecek tail wave (Y-tail veya Phase Y P2) eklenebilir.
- **`tools setup --source=remote-nuget` implementation**: Stub kalıyor. nuget.org'dan latest discovery + download mantığı NuGet.Protocol ile mümkün ama Phase 2b PD-7 ile birlikte tasarlanmalı.
- **`tools setup --source=release`**: Phase Y bu değeri **yok ediyor** (CLI'dan + ADR-004 §2.15 silindi). PD-7 geldiğinde `tools setup --source=release` olarak script'e eklenir, ADR'si o zaman yazılır.
- **`tools.ps1` / `tools.sh` shim'leri**: Sen istemedin. Direct `dotnet run --file tools.cs --` veya Unix shebang yeterli. Gelecek bir wave'de kullanıcı feedback'i kötü gelirse eklenebilir.
- **`build.ps1` / `build.sh` consolidation**: Bu wave'de değil. `tools build` zaten paralel yol; Cake-only forwarder shim'leri mevcut UX için kalıyor.
- **Phase X P5 timing**: Phase Y kapandıktan sonra phase-x P5 (target rename atomic) ayrı bir commit olarak land eder. Phase Y onun ön-koşulu — UnsupportedArtifactSourceResolver retirement + `--source` narrowing Phase Y'da kapsanmadan P5 atılamazdı.
- **Phase X P4-C (Large Pipeline decomposition)**: Tamamen ortogonal. Phase Y açıkken paralel devam edebilir; Phase Y'nın silmediği Pipeline'lar (HarvestPipeline, PackagePipeline, PackageConsumerSmokePipeline) Phase X tarafında P4-C kapsamında.
- **`tools.cs`'in kendi unit test'i**: Yok. File-based app, tooling-shaped. Test'i `tools ci-sim` çıktısının manuel diff'iyle yapılır (parite gate).

---

## 15. Approval gate

Phase Y koda dokunmadan önce ekstra doğrulama:

- [ ] Sequencing onayı: Phase Y, Phase X P5'ten önce
- [ ] Wave A scope'u (silme + tools.cs skeleton + setup/ci-sim explicit stub) onayı
- [ ] Wave B atomic-commit kararı onayı
- [ ] ADR-004 in-place rewrite plan'ı (motto + §2.5 + invariant #4 + §2.15 komple delete + glossary + örnekler) onayı
- [ ] Phase X plan amendments (§1.2/§1.3/§1.5/§2.1.x line-by-line) onayı
- [ ] `tools setup --source=local` includes CleanArtifacts default + `--no-clean` flag karar onayı
- [ ] `tools setup --source=remote-nuget` stub davranışı onayı (exit 64 + mesaj)
- [ ] Coverage baseline reset hedefi (515 → 485) onayı
- [ ] `tests/scripts/` klasörünün Wave B'de tamamen silinmesi onayı
- [ ] §11.2 maintainer GitHub dispatch gate cadence (Wave A + Wave B kapanışlarında) onayı
- [ ] Concrete-family scope filtering script-side rule (§9.3) onayı
- [ ] Remote GitHub porting checklist (§9.4) eksiksizliği onayı

Onaylar geldiğinde Wave A açılır. Wave A → Wave B sırasıyla, her wave kapanışında §11 validation gate'leri.
