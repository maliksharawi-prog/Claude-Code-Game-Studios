# Systems Index: Sweet Cascade

> **Status**: Draft — authored autonomously, awaiting user review
> **Created**: 2026-07-17
> **Last Updated**: 2026-07-17
> **Source Concept**: design/gdd/game-concept.md

---

## Overview

Sweet Cascade is a mobile-first match-3 puzzle adventure built around one
validated core loop — swap, match, cascade — wrapped in a level-objective /
move-limit structure, decorated with heavy "juice" feedback (Pillar 1: Every
Swap Sparkles), and governed by an honest difficulty philosophy (Pillar 2:
Clever, Never Cheated) rather than invisible board rigging. This index
enumerates **15 systems** across three commitment tiers, tiered honestly per
`game-concept.md`: **11 systems in approved MVP scope** (the board engine and
the supporting specials / objective / scoring / screens / persistence / feel
layer that make 10 levels playable and worth returning to), **1 system in
Phase 2** (Booster Brewing Meta — the game's PROPOSED unique hook, explicitly
not started until the founder approves it), and **3 systems in Phase 3**
(Events/Theming Engine and Social Layer, which deliver Pillars 3 and 4
respectively, plus an inferred Backend & Accounts Service that sits outside
this agent's design scope). The dependency graph is deliberately narrow at
the base: four small Foundation utilities unblock one central Core system
(the board engine), and every Feature-layer system extends that one engine.

---

## Structural Decisions Worth Flagging

These are judgment calls made during decomposition — not required by the
concept doc verbatim, but derived from good dependency-graph and scope
hygiene. Flagging them explicitly for review rather than burying them in a
table row:

1. **RNG Service split out as its own Foundation system**, even though the
   concept doc only mentions "seedable RNG" as a parenthetical inside the
   board-engine bullet. Reason: `technical-preferences.md`'s determinism
   testing rule needs a stable, independently-testable seed contract, and
   Phase 2/3 systems (ingredient harvest variance, event reward drops) will
   want the same deterministic contract rather than reinventing seeding per
   system. Easy to reverse — fold back into `board-engine.md` if this proves
   over-engineered in practice.
2. **Save & Persistence tiered as MVP** even though the concept's MVP
   Definition bullet list doesn't name it explicitly. Reason: 10 hand-authored
   levels are close to worthless for any playtesting beyond a single sitting
   if star/unlock progress doesn't survive an app close. Flagging in case the
   intent was to demote this to Vertical Slice.
3. **Level Data Format is a living, versioned schema**, not a one-shot spec.
   It has a soft mutual dependency with Level Objective & Move-Limit System
   and Special Candies & Combo Matrix (their data needs shape its final
   shape) — see Circular Dependencies below for the resolution.
4. **Level Progression / World Map split from Level Data Format** and its
   Priority pushed to **Alpha** (multi-region, per the Scope Tiers table: VS
   is still "1 polished region," 4 regions don't appear until Alpha) even
   though its **Phase is MVP** (it's committed scope, not a disputed hook —
   just later in build order). A lightweight star-gate check (no region
   switching) is needed as early as Vertical Slice and is scoped inside
   Level Objective / Game UI, not the full World Map system.
5. **Backend & Accounts Service flagged as non-GDD.** It's an inferred
   dependency of the Social Layer (leaderboards/challenges need accounts and
   a server), but per this agent's role boundaries, architecture/technology
   choices are `technical-director` territory. Recommend an ADR in
   `docs/architecture/`, not a `design/gdd/` document, when Phase 3 is
   approached.

---

## Systems Enumeration

| # | System Name | Scope (one-line) | Category | Layer | Priority | Phase | Status | Design Doc | Depends On |
|---|---|---|---|---|---|---|---|---|---|
| 1 | RNG Service | Seedable PRNG service (seed set/save/restore) providing deterministic randomness to any system that needs it. | Core | Foundation | MVP | MVP | Not Started | design/gdd/rng-service.md | — |
| 2 | Level Data Format | Data schema/resource contract for a level: grid layout, candy palette, objective config, move limit, blockers, star thresholds. | Core | Foundation | MVP | MVP | Not Started | design/gdd/level-data-format.md | — (soft co-design dependency, see Circular Dependencies) |
| 3 | Touch & Input System | Translates tap/swipe/mouse gestures into abstract board swap-intent events; touch and web-mouse parity. | Core | Foundation | MVP | MVP | Not Started | design/gdd/touch-input.md | — |
| 4 | Match-3 Board Engine | Grid state, swap validation, match detection, gravity/refill, cascade resolution — the core moment-to-moment simulation. | Gameplay | Core | MVP | MVP | Not Started | design/gdd/board-engine.md | RNG Service, Touch & Input System, Level Data Format |
| 5 | Special Candies & Combo Matrix | Special-candy creation rules (match-4/L/T/match-5) and the special-×-special combo interaction matrix. | Gameplay | Feature | MVP | MVP | Not Started | design/gdd/special-candies.md | Match-3 Board Engine |
| 6 | Scoring & Star Thresholds | Point-value formulas for matches/cascades/combos and the 1/2/3-star evaluation thresholds per level. | Progression | Feature | MVP | MVP | Not Started | design/gdd/scoring-stars.md | Match-3 Board Engine, Special Candies & Combo Matrix |
| 7 | Level Objective & Move-Limit System | Win/lose objective types (score target, collection, blocker-clear), move countdown, blocker board rules. | Gameplay | Feature | MVP | MVP | Not Started | design/gdd/level-objectives.md | Match-3 Board Engine, Level Data Format, Scoring & Star Thresholds |
| 8 | Juice Layer — VFX & Audio Hooks | Event-driven feedback (particles, screen response, escalating pop/cascade SFX, haptics) in service of Pillar 1. | Feel | Presentation | MVP | MVP | Not Started | design/gdd/juice-layer.md | Match-3 Board Engine, Special Candies & Combo Matrix, Scoring & Star Thresholds |
| 9 | Save & Persistence | Local serialization of player profile (unlocked levels, per-level star record, settings); generic save/load contract. | Persistence | Foundation | MVP | MVP | Not Started | design/gdd/save-persistence.md | — |
| 10 | Game UI/Screens Flow | High-level screen state machine and navigation (menu, level select, in-level HUD shell, results/star ceremony). Per-screen layout detail is UX Designer territory (`design/ux/`). | UI | Presentation | MVP | MVP | Not Started | design/gdd/screen-flow.md | Level Objective & Move-Limit System, Scoring & Star Thresholds, Level Progression/World Map, Save & Persistence |
| 11 | Level Progression / World Map | Multi-region world structure, level node graph, star-gated unlock rules, region theming slots. | Progression | Feature | Alpha | MVP | Not Started | design/gdd/world-map.md | Level Data Format, Scoring & Star Thresholds, Save & Persistence |
| 12 | Booster Brewing Meta | Ingredient harvest from matches, recipe unlock/discovery, pre-level booster loadout. **APPROVED by founder 2026-07-17** — full GDD still gated on a Phase 2 friction prototype. | Economy | Polish | Vertical Slice | **2** | Not Started — hook approved; prototype gate before full GDD | design/gdd/booster-brewing.md | Match-3 Board Engine, Special Candies & Combo Matrix, Level Objective & Move-Limit System, Save & Persistence, Game UI/Screens Flow |
| 13 | Events/Theming Engine | Data-driven seasonal event framework: time-boxed content swaps, limited ingredients/recipes, region re-theming, daily challenges. | Meta | Polish | Alpha | **3** | Not Started — deferred | design/gdd/events-theming.md | Level Data Format, Level Progression/World Map, Booster Brewing Meta, Save & Persistence, Game UI/Screens Flow |
| 14 | Social Layer | Friend leaderboards, async friend challenges, gifting — player-facing social comparison. | Social | Polish | Full Vision | **3** | Not Started — deferred | design/gdd/social-layer.md | Scoring & Star Thresholds, Save & Persistence, Backend & Accounts Service, Game UI/Screens Flow |
| 15 | Backend & Accounts Service *(inferred)* | Account identity, cloud sync, and leaderboard/challenge server backing the Social Layer. | Core | Foundation (Phase-3 infra) | Full Vision | **3** | Deferred — non-GDD | N/A — recommend `docs/architecture/` ADR, not a GDD (technical-director scope) | — (external infra; consumed by Social Layer) |

**Phase legend**: MVP = approved core scope · 2 = brewing hook (approved 2026-07-17; friction prototype gates full GDD) · 3 = live-ops/social, explicitly deferred per `game-concept.md`.

---

## Categories

| Category | Description | Sweet Cascade Systems |
|----------|-------------|-----------------------|
| **Core** | Foundation systems everything depends on | RNG Service, Level Data Format, Touch & Input System, Backend & Accounts Service |
| **Gameplay** | The systems that make the game fun | Match-3 Board Engine, Special Candies & Combo Matrix, Level Objective & Move-Limit System |
| **Progression** | How the player grows over time | Scoring & Star Thresholds, Level Progression / World Map |
| **Economy** | Resource creation and consumption | Booster Brewing Meta |
| **Persistence** | Save state and continuity | Save & Persistence |
| **UI** | Player-facing information displays | Game UI/Screens Flow |
| **Feel** *(custom)* | Sensory feedback — VFX, SFX, haptics, screen response tied to gameplay events | Juice Layer — VFX & Audio Hooks |
| **Meta** | Systems outside the core game loop | Events/Theming Engine |
| **Social** *(custom)* | Asynchronous multiplayer comparison and connection | Social Layer |

Narrative is omitted — Anti-Pillar: "NOT a story game." No dialogue/quest
systems exist in this game's design.

---

## Priority Tiers

| Tier | Definition | Target Milestone | Design Urgency |
|------|------------|------------------|----------------|
| **MVP** | Required for the core loop to function. Without these, you can't test "is this fun?" | First playable prototype (10 levels, 1 region) | Design FIRST |
| **Vertical Slice** | Required for one complete, polished area. Demonstrates the full experience. | 30 levels, 1 polished region, brewing meta, star gates, full juice pass | Design SECOND |
| **Alpha** | All features present in rough form. Complete mechanical scope, placeholder content OK. | 120 levels, 4 regions, events engine | Design THIRD |
| **Full Vision** | Polish, edge cases, nice-to-haves, and content-complete features. | Live seasons, backend social, 2 live events | Design as needed |

This mirrors the Scope Tiers table in `game-concept.md` exactly.

---

## Dependency Map

### Foundation Layer (no dependencies)

1. **RNG Service** — deterministic seedable randomness; `technical-preferences.md`'s testing standard requires seedable board RNG, and future systems (harvest variance, event drops) will want the same contract.
2. **Level Data Format** — the level schema/resource contract; Board Engine, Level Objective, and World Map all read from it, so a v1 draft must exist before those (see Circular Dependencies).
3. **Touch & Input System** — pure gesture-to-intent translation layer; no other system needs to exist first.
4. **Save & Persistence** — generic local serialization; zero dependencies on other design systems even though it isn't consumed until much later (Game UI/Screens Flow, World Map).

### Core Layer (depends on foundation)

1. **Match-3 Board Engine** — depends on: RNG Service (spawn/refill), Touch & Input System (swap trigger), Level Data Format (level bootstrap/grid layout). The single bottleneck system — everything in Feature layer extends it.

### Feature Layer (depends on core)

1. **Special Candies & Combo Matrix** — depends on: Match-3 Board Engine (the match detection it extends).
2. **Scoring & Star Thresholds** — depends on: Match-3 Board Engine (match/cascade events), Special Candies & Combo Matrix (bonus point sources).
3. **Level Objective & Move-Limit System** — depends on: Match-3 Board Engine (collection/blocker events), Level Data Format (objective config), Scoring & Star Thresholds (score-target objective type; triggers end-of-level star evaluation).
4. **Level Progression / World Map** — depends on: Level Data Format (node list, region metadata), Scoring & Star Thresholds (star counts gate unlocks), Save & Persistence (persisted unlock/star state).

### Presentation Layer (depends on features)

1. **Juice Layer — VFX & Audio Hooks** — depends on: Match-3 Board Engine, Special Candies & Combo Matrix, Scoring & Star Thresholds (all three fire the events juice hooks react to).
2. **Game UI/Screens Flow** — depends on: Level Objective & Move-Limit System, Scoring & Star Thresholds, Level Progression/World Map, Save & Persistence (fronts all of these with navigable screens).

### Polish Layer (depends on everything)

1. **Booster Brewing Meta** *(Phase 2 — PENDING APPROVAL)* — depends on: Match-3 Board Engine, Special Candies & Combo Matrix, Level Objective & Move-Limit System, Save & Persistence, Game UI/Screens Flow.
2. **Events/Theming Engine** *(Phase 3)* — depends on: Level Data Format, Level Progression/World Map, Booster Brewing Meta, Save & Persistence, Game UI/Screens Flow.
3. **Social Layer** *(Phase 3)* — depends on: Scoring & Star Thresholds, Save & Persistence, Backend & Accounts Service, Game UI/Screens Flow.
4. **Backend & Accounts Service** *(Phase 3, inferred, non-GDD)* — depends on: none from the design-system graph (external infra); consumed only by Social Layer.

---

## Recommended Design Order

Ordering rule applied: finish **all Phase-MVP systems** (regardless of their
nominal Priority tier) in dependency order before touching Phase 2's
pending-approval hook or Phase 3's explicitly-deferred systems. Within
Phase-MVP, Foundation → Core → Feature → Presentation, with one practical
exception noted below.

| Order | System | Priority | Layer | Phase | Agent(s) | Est. Effort |
|-------|--------|----------|-------|-------|----------|--------------|
| 1 | RNG Service | MVP | Foundation | MVP | game-designer | S |
| 2 | Level Data Format | MVP | Foundation | MVP | game-designer | M |
| 3 | Touch & Input System | MVP | Foundation | MVP | game-designer | S |
| 4 | Match-3 Board Engine | MVP | Core | MVP | game-designer + systems-designer | L |
| 5 | Special Candies & Combo Matrix | MVP | Feature | MVP | game-designer + systems-designer | L |
| 6 | Scoring & Star Thresholds | MVP | Feature | MVP | game-designer + systems-designer | M |
| 7 | Level Objective & Move-Limit System | MVP | Feature | MVP | game-designer | M |
| 8 | Juice Layer — VFX & Audio Hooks | MVP | Presentation | MVP | game-designer | M |
| 9 | Save & Persistence | MVP | Foundation | MVP | game-designer | S |
| 10 | Game UI/Screens Flow | MVP | Presentation | MVP | game-designer | M |
| 11 | Level Progression / World Map | Alpha | Feature | MVP | game-designer | M |
| 12 | Booster Brewing Meta | Vertical Slice | Polish | **2 — GATE** | game-designer + economy-designer | L |
| 13 | Events/Theming Engine | Alpha | Polish | 3 | game-designer + economy-designer | L |
| 14 | Social Layer | Full Vision | Polish | 3 | game-designer | M |
| 15 | Backend & Accounts Service | Full Vision | Foundation (infra) | 3 | technical-director (ADR, not GDD) | — |

**Practical note on ordering vs. layer**: Save & Persistence is architecturally
Foundation-tier (zero dependencies), but nothing MVP-critical consumes it
until Game UI/Screens Flow, so it's sequenced at position 9 rather than
alongside RNG Service / Level Data Format / Touch Input at the very top. Write
order and dependency layer are related but not identical — layer describes
*what could be built first without blocking*, order describes *when it's
actually useful to have the doc*.

**Gate on position 12**: Do not begin `booster-brewing.md` until the founder
has explicitly approved the brewing hook (open question flagged in
`game-concept.md`). If declined, position 12 is removed and Events/Theming
Engine (position 13) loses one dependency (Booster Brewing Meta) — its scope
would shrink to pure seasonal re-theming without ingredient/recipe events.

---

## Circular Dependencies

- **Level Data Format ↔ Level Objective & Move-Limit System / Special Candies
  & Combo Matrix**: Level Data Format's schema needs to express objective
  types and special-candy/blocker vocabulary that doesn't fully exist until
  those two GDDs are drafted — but Board Engine and Level Objective can't be
  written without *some* level schema to bootstrap against.
  **Resolution**: Author Level Data Format as an explicitly versioned
  (`schema_version` field) living contract. Draft a v1 stub early (position
  #2) covering only what Board Engine needs (grid size, candy palette, move
  limit, single score-target objective), then extend it once Special Candies
  and Level Objective are drafted and reveal their real data needs. Do not
  treat v1 as final — flag it as "extend after #5 and #7" in the doc itself.

- **Level Objective & Move-Limit System ↔ Scoring & Star Thresholds**: tight
  mutual coupling (Objective reads the live score to check score-target
  completion and to trigger star evaluation at level end; Scoring never needs
  Objective's internal state). **Not a true cycle** — resolved as a
  one-directional read. Scoring is written first (position #6), Objective
  second (position #7), so the read direction is available when needed.

No other cycles detected.

---

## High-Risk Systems

| System | Risk Type | Risk Description | Mitigation |
|--------|-----------|-------------------|------------|
| Match-3 Board Engine | Technical + Design | The whole game hinges on cascade feel and 60fps mobile performance (≤100 draw calls per `technical-preferences.md`). The prototype validated the *concept* in HTML/CSS only — `REPORT.md` explicitly flags the feel ceiling must be re-validated in-engine before Production commit. | Design and build this first; treat the Vertical Slice gate as the real feel checkpoint, not the concept prototype. Keep RNG seedable so cascade/match unit tests stay deterministic per `technical-preferences.md`. |
| Special Candies & Combo Matrix | Design | Prototype flagged special-×-special combos as the biggest emergent moments, but also flagged passive color-bomb detonation during cascades as feeling "unearned" — an explicitly open design question. | Give the combo interaction matrix dedicated design-review attention; consider a focused prototype of just the combo matrix if the full GDD reveals unresolved edge cases. |
| Booster Brewing Meta | Design + Scope | Explicitly acknowledged risk in `game-concept.md`: may add friction to a genre whose players expect zero-decision flow between levels. Status is PROPOSED, not approved. | Do not author the full GDD until the founder approves the hook. `game-concept.md` recommends a lightweight Phase 2 prototype (paper/HTML) to test "is pre-level choice fun or friction" before full spec work. |
| Level Data Format | Design + Technical | Shared contract depended on by 4+ systems (Board Engine, Level Objective, World Map, Events). Late schema changes ripple across all of them. | Version the schema explicitly (`schema_version`); design iteratively alongside Board Engine and Level Objective instead of trying to finalize it monolithically upfront. |
| Backend & Accounts Service | Scope + Technical | Requires accounts, sync, and anti-abuse handling for leaderboards — `game-concept.md` explicitly calls this "significant scope beyond the game client," deferred behind MVP intentionally. | Defer entirely to Phase 3. When approached, hand to `technical-director` for an ADR — this is not a game-designer GDD. |

---

## Progress Tracker

| Metric | Count |
|--------|-------|
| Total systems identified | 15 (14 planned as GDDs in `design/gdd/`; 1 flagged non-GDD — Backend & Accounts Service) |
| Design docs started | 11 — all Phase-MVP systems drafted (2026-07-18): rng-service, level-data-format, touch-input, board-engine, save-persistence, screen-flow, world-map, special-candies, scoring-stars, level-objectives, juice-layer |
| Design docs reviewed | 4 (design-review lean — logs in `design/gdd/reviews/`; board-engine through full revise/re-review cycle) |
| Design docs approved | 4 — `rng-service.md`, `level-data-format.md`, `touch-input.md`, `board-engine.md` |
| MVP-priority systems designed | 10 / 10 drafted (4 approved; 6 awaiting review — run /review-all-gdds next) |
| Vertical-Slice-priority systems designed | 0 / 1 |
| Alpha-priority systems designed | 0 / 2 |
| Full-Vision-priority systems designed | 0 / 2 |
| Phase-MVP systems designed | 0 / 11 |
| Phase-2 systems designed | 0 / 1 (hook approved 2026-07-17; awaiting friction prototype before full GDD) |
| Phase-3 systems designed | 0 / 3 |

---

## Next Steps

- [ ] User review of this systems enumeration — especially the Structural
      Decisions Worth Flagging section above
- [ ] Founder decision on Booster Brewing Meta (position #12) — approve,
      reject, or request a Phase 2 validation prototype first
- [ ] Design MVP-tier systems first, in the order above, starting with
      `/design-system rng-service` (or start directly at `board-engine` if
      RNG/Level-Data-Format/Touch-Input feel too granular to warrant standalone
      GDDs — see Structural Decisions Worth Flagging, item 1)
- [ ] Run `/design-review [path]` on each completed GDD
- [ ] Run `/gate-check pre-production` once all 10 MVP-priority GDDs are
      designed and reviewed
- [ ] Validate the highest-risk systems (Match-3 Board Engine feel,
      Special Candies combo matrix) with in-engine testing before Production
      commit — the concept prototype's HTML feel ceiling is not sufficient
