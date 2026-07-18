# Epics Index — Sweet Cascade (MVP)

Last Updated: 2026-07-18
Engine: Unity 6.3 LTS (6000.3.x) · C# · URP (Render Graph path only) — ADR-001
Source: `docs/architecture/architecture.md` v1.0 (§6 module ownership) · `docs/architecture/tr-registry.yaml` (42 TRs) · `docs/architecture/architecture-review-2026-07-18.md` (dependency order) · ADRs 001–006 (Accepted)

> **Scope:** these 11 epics cover the **11 APPROVED MVP GDDs only**. Phase 2 (Booster
> Brewing Meta, system #12) and Phase 3 (Events/Theming #13, Social Layer #14, Backend &
> Accounts #15) are **explicitly OUT** of every epic below and are not decomposed here.
> The Phase-3 backend seam is reserved as ADR-I (scope only, not implemented).

## Traceability

All **42** TR-IDs in `tr-registry.yaml` are allocated to exactly one epic (no gaps, no
double-ownership). 40 are covered by an Accepted ADR (002–006) or a master-architecture
§§4–9 decision; **2 are Presentation-tier Partials** (TR-jl-003 audio, TR-jl-004 VFX),
both owned by **E09**, pending the deliberately-deferred ADR-G / ADR-H.

## Epics (dependency order)

| Epic | Layer | System(s) | Primary GDD(s) | TRs Owned | Depends On | Stories | Status |
|------|-------|-----------|----------------|-----------|------------|---------|--------|
| E01 project-scaffold-ci | Foundation (infra) | Unity project / assemblies / CI | technical-preferences.md · arch §5 | 1 (perf-002) | — | 6 stories | Ready |
| E02 domain-foundation | Foundation | RNG · Level Data schema · Save codec · Manifest tool | rng-service · level-data-format · save-persistence · world-map | 10 | E01 | Not yet created | Ready |
| E03 board-engine-specials | Core / Feature | Match-3 Board Engine · Special Candies | board-engine · special-candies | 8 | E01, E02 | Not yet created | Ready |
| E04 scoring-objectives | Feature | Scoring & Stars · Level Objective & Move-Limit | scoring-stars · level-objectives | 7 | E01, E02, E03 | Not yet created | Ready |
| E05 game-runtime-input-screenflow | Core / Presentation | Touch & Input · Reveal replay · Screen Flow | touch-input · juice-layer · screen-flow | 8 | E01, E02, E03, E04 | Not yet created | Ready |
| E06 app-shell-persistence-loading | Foundation | BootLoader · Addressables loader · Save IO · LevelData SO | save-persistence · level-data-format | 3 | E01, E02, E05 | Not yet created | Ready |
| E07 ui-toolkit-screens-hud | Presentation | Game UI / Screens · World Map view | screen-flow · world-map | 2 | E01, E04, E05, E06 | Not yet created | Ready |
| E08 rendering-materials | Presentation | BoardPresenter · glass-candy material set | visual-interface-blueprint · technical-preferences.md | 1 (perf-001) | E01, E05 | Not yet created | Ready |
| E09 juice-vfx-audio-haptics | Presentation | Juice Layer (VFX / Audio / Haptics) | juice-layer | 2 (⚠️ both Partial) | E01, E03, E05, E08 | Not yet created | Ready (2 TRs ADR-blocked) |
| E10 content-levels-manifest | Content | 10 MVP levels · generated manifest · balance | level-data-format · scoring-stars · world-map | 0 (consumes E02/E04) | E02, E03, E04 | Not yet created | Ready |
| E11 device-qa-performance | QA / Perf | On-device perf & QA validation | technical-preferences.md · arch §9 | 0 (validates perf budgets) | E05, E06, E07, E08, E09, E10 | Not yet created | Ready |

## Out of scope (do NOT create epics here)

| System | Phase | Reason |
|--------|-------|--------|
| Booster Brewing Meta (#12) | 2 | Hook approved 2026-07-17; full GDD gated on friction prototype. Not started. |
| Events/Theming Engine (#13) | 3 | Deferred per game-concept.md. |
| Social Layer (#14) | 3 | Deferred; requires Backend & Accounts. |
| Backend & Accounts Service (#15) | 3 | Non-GDD infra; reserved as ADR-I (seam only, not implemented at MVP). |

## Next Step

Run `/create-stories [epic-slug]` per epic, in dependency order, as each layer is
approached. Recommended first: **E01 project-scaffold-ci** (see epic file rationale).
