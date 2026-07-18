# Game UI / Screens Flow

*Status: Draft — awaiting /design-review*
*Created: 2026-07-18*
*Last Updated: 2026-07-18*
*Layer: Presentation · Priority: MVP · Phase: MVP · Category: UI*
*Author: systems-designer*
*Implements Pillar: supports Pillar 1 — Every Swap Sparkles (protects
retry/flow-state momentum so juice is experienced back-to-back, never diluted
by navigation friction); supports Pillar 2 — Clever, Never Cheated
(indirectly — every screen reflects literal game/save state, never a
decorative lie, per `art-bible.md`'s Pillar 2 Compliance Note)*
*Depends On: Level Data Format (`design/gdd/level-data-format.md`, APPROVED),
Save & Persistence (`design/gdd/save-persistence.md`, Draft), Touch & Input
System (`design/gdd/touch-input.md`, APPROVED), Match-3 Board Engine
(`design/gdd/board-engine.md`, APPROVED), Juice Layer — VFX & Audio Hooks
(`design/gdd/juice-layer.md`, Draft — mutual, supplies the `juice_input_lock`
term composed into Formula 5), Level Objective & Move-Limit System (#7,
Draft — forward seam), Scoring & Star Thresholds (#6, Draft — forward seam),
Level Progression / World Map (#11, Draft — forward seam)*
*Depended On By: Booster Brewing Meta (Phase 2, gated), Events/Theming Engine
(Phase 3), Social Layer (Phase 3) — per `design/gdd/systems-index.md`*
*Source: `design/gdd/game-concept.md` · `design/gdd/systems-index.md` ·
`design/gdd/level-data-format.md` · `design/gdd/save-persistence.md` ·
`design/gdd/touch-input.md` · `design/gdd/board-engine.md` ·
`design/narrative/characters-and-tone.md` · `design/art/art-bible.md`*

---

## Overview

Game UI/Screens Flow is the Presentation-layer state machine that owns
**which screen the player is looking at, at every moment**, and the rules
that govern moving between them — it does not own how any individual screen
looks (that is `design/ux/` territory) or how any gameplay/scoring value is
computed (that belongs to Level Objective, Scoring & Star Thresholds, and
Board Engine). Concretely, this document defines eight screen states (Boot
& Loading, World Map, Pre-Level Card, Gameplay, Pause, Settings, Results
Win, Results Lose), the exhaustive legal-transition table between them, the
Android/OS back-button behavior for each, which screen suspends live
gameplay and why, and the exact data each screen is allowed to read and
write. Because two of this document's listed dependencies — Level Objective
& Move-Limit System and Scoring & Star Thresholds — are not yet written,
this document deliberately designs against their **already-fixed data
surface** (Level Data Format's `objectives` list and 0–3 star schema) and
declares explicit, named seams (a `level_resolved` event and a `ResultsData`
payload) where those future systems plug in, rather than inventing their
internals. This document also formally discharges two reciprocal-reference
obligations flagged by already-approved sibling documents: `touch-input.md`
asked which screen hosts its gesture controller and how input scope is
confined, and `board-engine.md` asked who supplies `attempt_number` at
bootstrap and co-owns the external `board_input_enabled` gate — both are
answered explicitly in Detailed Rules §7.

---

## Player Fantasy

Sweet Cascade's target player has 5–15 minutes, often one-handed, often
distracted (`game-concept.md`'s Target Player Profile). This system's entire
job is protecting a single feeling: **the game never makes you wait, and
never makes you hunt.** One thumb, reaching only the bottom corners and the
board itself, does everything — no menu is ever more than one tap deep from
where the player currently is, no screen ever requires a second hand or a
precise small target to escape from, and no navigation choice is ever
irreversible in a way that costs the player something they already earned.

Concretely, this system exists to protect three guarantees:

1. **Losing costs nothing but time, and not much of that.** Per
   `game-concept.md`'s Flow State Design, failure must feel educational, not
   punishing — "so close!" energy, not a wall. Mechanically, that means the
   path from a lose back into a fresh attempt of the *same* level is the
   single shortest, most frictionless path in the entire navigation graph
   (Detailed Rules §6, Formulas 1 and 3) — shorter than the path to
   literally anywhere else in the game, because it is the path the flow
   state most depends on being instant.
2. **The board is sacred — nothing interrupts it uninvited.** Fizz, banners,
   settings, and every other piece of UI chrome stay off the active play
   area entirely (Detailed Rules §8) — a rule inherited directly from
   `characters-and-tone.md`'s mascot placement boundary and the art bible's
   Visual Hierarchy. The only things that can pull a player's attention away
   from a live board are things the player themselves asked for (opening
   Pause) or that protect their save (the app being backgrounded).
3. **Every screen tells the truth, instantly.** Per the art bible's Pillar 2
   Compliance Note, no screen ever shows a stale star count, a locked level
   that's secretly unlocked, or a settings toggle that hasn't actually taken
   effect. A player should never need to force-quit and reopen the app to
   "fix" a screen that looks wrong — because none ever do.

---

## Detailed Rules

### 1. Screen State Model — Base Layer & Overlay Layer

The state machine has two independent layers, composed together into one
**composite state**. This two-layer split exists specifically so a Pause
menu opened on top of Gameplay, or Settings opened on top of either World
Map or Pause, never needs its own copy of "what was I on top of" logic
scattered through the base states — it is a single, generic stacking rule.

**Base Layer** — exactly one is active at all times; entering a new Base
state always fully replaces the previous one:

| ID | State | One-line role |
|----|-------|----------------|
| B1 | `BOOT_LOADING` | App cold-start only. Loads the save profile; no player interaction. |
| B2 | `WORLD_MAP` | The navigation hub/root. Level selection entry point. |
| B3 | `GAMEPLAY` | The live board + HUD shell. The only state that ever hosts board input (§7). |
| B4 | `RESULTS_WIN` | Star-ceremony celebration screen for a won attempt. |
| B5 | `RESULTS_LOSE` | Closest-miss framing screen for a lost attempt. |

**Overlay Layer** — at most one overlay stack is active on top of the
current Base state; the stack has a hard maximum depth of **2**
(`OVERLAY_STACK_MAX_DEPTH`, Tuning Knobs):

| ID | State | Legal host Base state(s) | Max stack depth as this state |
|----|-------|--------------------------|-------------------------------|
| O1 | `PRE_LEVEL_CARD` | `WORLD_MAP` only | 1 (never has a further overlay pushed on top of it) |
| O2 | `PAUSE` | `GAMEPLAY` only | 1 (may have O3 pushed on top of it) |
| O3 | `SETTINGS` | `WORLD_MAP` or `PAUSE` (i.e., `GAMEPLAY`+O2) | 1 (from Map) or 2 (from Pause) |

**Legal composite states** (the complete set this state machine may ever be
in — anything not in this list is invalid and must never be reachable):

`(B1, [])` · `(B2, [])` · `(B2, [O1])` · `(B2, [O3])` · `(B3, [])` ·
`(B3, [O2])` · `(B3, [O2, O3])` · `(B4, [])` · `(B5, [])`

Nine legal composite states total. `PRE_LEVEL_CARD` never co-occurs with
`GAMEPLAY`, and `SETTINGS` is never opened directly from `PRE_LEVEL_CARD` —
Pre-Level Card exposes exactly one primary action (Play) and one dismiss
action (Back), per the art bible's "one primary action per screen" rule.

### 2. State Transition Table

`level_id` and `results_data` are carried as transition parameters where
relevant. `attempt_number` handling is detailed in §7.

| ID | From | Trigger | To | Type | Notes |
|----|------|---------|-----|------|-------|
| T1 | `(B1,[])` | Save profile finishes loading (automatic) | `(B2,[])` | Base | If `profile_recovery_notice_needed` is set, a dismissible notice is shown on this same entry (Edge Cases). |
| T2 | `(B2,[])` | Tap an **unlocked** level node | `(B2,[O1])` | Overlay-Push | Opens Pre-Level Card for the tapped `level_id`. |
| T3 | `(B2,[])` | Tap a **locked** level node | `(B2,[])` | No-op | Formula 4 resolves `is_unlocked = false`; nothing happens. |
| T4 | `(B2,[O1])` | Tap Play | `(B3,[])` | Base + Pop | `attempt_number` resets to 1 (§7); Board Engine bootstrap begins for `level_id`. |
| T5 | `(B2,[O1])` | Tap Back / OS back | `(B2,[])` | Overlay-Pop | |
| T6 | `(B2,[])` | Tap Settings gear | `(B2,[O3])` | Overlay-Push | |
| T7 | `(B2,[O3])` | Tap Back / OS back | `(B2,[])` | Overlay-Pop | |
| T8 | `(B3,[])` | Tap Pause icon | `(B3,[O2])` | Overlay-Push | Suspends gameplay (§4, Formula 5). |
| T9 | `(B3,[])` | App loses focus / backgrounds (automatic) | `(B3,[O2])` | Overlay-Push (automatic) | Also triggers Save & Persistence's `flush_if_dirty()`. |
| T10 | `(B3,[O2])` | Tap Resume / OS back | `(B3,[])` | Overlay-Pop | Resumes gameplay. |
| T11 | `(B3,[O2])` | Tap Restart Level | `(B3,[])` | Overlay-Pop + reset | Same `level_id`, `attempt_number` increments by 1 (§7); Board Engine re-bootstraps. |
| T12 | `(B3,[O2])` | Tap Quit to Map | `(B2,[])` | Base + Pop | Level abandoned; no `LevelRecord` write (`save-persistence.md` §10). No confirmation dialog (§4). |
| T13 | `(B3,[O2])` | Tap Settings gear | `(B3,[O2,O3])` | Overlay-Push (depth 2) | |
| T14 | `(B3,[O2,O3])` | Tap Back / OS back | `(B3,[O2])` | Overlay-Pop | |
| T15 | `(B3,[])` | `level_resolved(WIN, results_data)` *(seam event)* | `(B4,[])` | Base | Fired by the future Level Objective / Scoring systems. |
| T16 | `(B3,[])` | `level_resolved(LOSE, results_data)` *(seam event)* | `(B5,[])` | Base | Fired by the future Level Objective / Scoring systems; `results_data.closest_miss_summary` populated. |
| T17 | `(B4,[])` or `(B5,[])` | Tap Retry | `(B3,[])` | Base | Same `level_id`, `attempt_number` increments by 1 (§7); **Pre-Level Card is skipped** (§6). |
| T18 | `(B4,[])` | Tap Next Level *(conditional — only rendered if a next level exists and is unlocked)* | `(B2,[O1])` | Compound | `attempt_number` resets to 1 for the new `level_id`. |
| T19 | `(B4,[])` or `(B5,[])` | Tap Map / OS back | `(B2,[])` | Base | |
| T20 | Any state | App loses focus / backgrounds, not covered by T9 | Unchanged | No-op (side effect only) | `flush_if_dirty()` is called; no screen-state change (state is already suspended or has nothing dirty). |
| T21 | (process killed, cold relaunch) | App relaunch | `(B1,[])` | Base (implicit) | Any prior composite state is discarded — Boot/Loading always re-derives from a fresh `load_profile()` call, never from a remembered screen. |

### 3. Back / OS-Back Button Policy

Android hardware/gesture back is treated as synonymous with each state's
designated dismiss action wherever one exists. This table is the complete,
enumerable policy:

| Composite State | Back Result | Transition |
|---|---|---|
| `(B1,[])` Boot/Loading | Disabled — no in-app handling; nothing to back out of | — |
| `(B2,[])` World Map (root) | OS default (app suspend/exit) — **not intercepted** | — |
| `(B2,[O1])` Pre-Level Card | Closes card, returns to World Map | T5 |
| `(B2,[O3])` Settings (from Map) | Closes Settings, returns to World Map | T7 |
| `(B3,[])` Gameplay | Opens Pause | T8 |
| `(B3,[O2])` Pause | Closes Pause, resumes Gameplay | T10 |
| `(B3,[O2,O3])` Settings (from Pause) | Closes Settings, returns to Pause | T14 |
| `(B4,[])` Results Win | Same as tapping Map | T19 |
| `(B5,[])` Results Lose | Same as tapping Map | T19 |

**Deliberate decision: World Map's root back is never intercepted with a
custom "exit app?" confirmation dialog.** A confirmation dialog is itself a
tax on the player's time and a violation of the "never makes you wait"
Player Fantasy guarantee; standard Android/OS back-to-suspend behavior at
the navigation root is what players already expect and is unsurprising.
**Results screens never leave back as a true no-op** — Android convention
expects back to always do *something* functional; treating it as identical
to the already-designed Map action (rather than inventing a separate
no-op-in-the-middle-of-a-ceremony special case) keeps the policy small and
fully covered by an existing transition.

### 4. Modal vs. Full-Screen Policy — What Suspends Gameplay

Live board simulation is considered **active** only when
`base_state == GAMEPLAY` **and** no overlay is open. Any overlay opened
while the base is `GAMEPLAY` (`PAUSE`, or `PAUSE`+`SETTINGS`) suspends it;
so does the app losing OS focus (T9). "Suspended" means: the board freezes
visually, Board Engine performs no further simulation steps, and Touch &
Input accepts no gestures — formalized as Formula 5 (`Formulas`, below).

| Suspending event | Composite state after | Board frozen? | Input accepted? |
|---|---|---|---|
| Player taps Pause icon (T8) | `(B3,[O2])` | Yes | No |
| App backgrounds during active play (T9) | `(B3,[O2])` | Yes | No |
| Player opens Settings from Pause (T13) | `(B3,[O2,O3])` | Yes (already frozen by O2) | No |
| App backgrounds during Pause/Settings/Pre-Level Card (T20) | Unchanged | Already frozen / N/A | No |

**Why app-background auto-opens Pause rather than merely flagging
`board_input_enabled = false` silently**: if the player returns to the
foreground and sees the exact board they left, with no visible
acknowledgment that anything happened, a return-from-background feels like
nothing was protected. Auto-opening Pause makes the suspension *visible* and
gives the player an explicit, single-tap way back in (Resume) — consistent
with the Pillar 2 Compliance Note that UI must never silently diverge from
what actually happened. This also satisfies Touch & Input's own edge case
("Pause menu, results overlay, or any modal opens mid-gesture" → the
in-flight gesture is cancelled), so a background event can never leave a
half-resolved swipe hanging.

**Modal vs. full-screen classification**: `PRE_LEVEL_CARD`, `PAUSE`, and
`SETTINGS` are modal overlays — they render on top of, and do not replace,
their host Base state's background (per the art bible's card-based Menu
Layout Principles). `BOOT_LOADING`, `WORLD_MAP`, `GAMEPLAY`, `RESULTS_WIN`,
and `RESULTS_LOSE` are full-screen Base states — entering one fully replaces
whatever was rendered before.

### 5. Data Contract Per Screen

Per the task boundary that per-screen *layout* is UX territory, this table
specifies only what each screen is permitted to read and write — never
pixel arrangement.

| Screen | Reads | Writes |
|---|---|---|
| Boot/Loading | Triggers Save & Persistence's `load_profile()` | None directly |
| World Map | `get_profile()` / `get_total_stars()` (star counts, per-level `best_stars` for node badges and Formula 4); Level Data Format's level manifest (`level_id`, `display_number`, `region`) | None directly (navigation only) |
| Pre-Level Card | Level Data Format for the tapped level (`objectives` list, in authored order — first entry is the primary badge, per `level-data-format.md`'s own note; `star_1/2/3_score`); `get_profile()`'s existing `best_stars`/`best_score` for this level (badge + Formula 6 snapshot) | None |
| Gameplay | Level Data Format (handed to Board Engine for bootstrap); snapshots `pre_attempt_best_stars`/`pre_attempt_best_score` from `get_profile()` at bootstrap (Formula 6) | **None directly.** Level results flow through the Objectives → Save seam: Level Objective/Scoring calls `record_level_completion()` before `level_resolved` even fires (per `save-persistence.md` §3) — Screen Flow never calls it. |
| Pause | Currently-held in-memory level context (no new reads) | `flush_if_dirty()` (defensive, on background trigger only) |
| Settings | `get_profile().settings` | `update_setting(setting_key, value)` per toggle |
| Results Win | `ResultsData` seam payload (§11); the Formula 6 snapshot captured at Gameplay entry | None directly |
| Results Lose | `ResultsData` seam payload, including `closest_miss_summary` (§11) | None directly |

### 6. Retry Loop — Pre-Level Card Skip Rule

**The Pre-Level Card is only ever shown on a level's first entry in a
navigation session (T4) or when advancing to a genuinely new level (T18).**
On both Retry paths (T11 from Pause, T17 from Results) it is unconditionally
skipped — Gameplay is entered directly. This is the single mechanism that
makes the flow-state retry requirement possible: every extra screen between
"I lost" and "I'm playing again" is friction the design explicitly refuses
to add. See Formulas 1 and 3 for the exact tap-count and latency accounting
this rule produces.

### 7. Board Input Hosting, Confinement & `attempt_number` Supply

This section formally answers the reciprocal-reference obligations flagged
in `touch-input.md` §Dependencies and `board-engine.md` §Dependencies.

- **Hosting and confinement.** Touch & Input's gesture state machine is
  instantiated, and listens for pointer/touch events, **only** while
  `base_state == GAMEPLAY`. It does not exist in a "disabled" state on any
  other screen — there is no board grid to hit-test against on World Map,
  Pre-Level Card, Pause, Settings, or either Results screen, so input scope
  is confined by construction, not by a runtime flag alone.
- **`board_input_enabled` co-ownership — now a three-way composition.**
  Board Engine owns and emits its own internal busy-state signal
  (`board_input_enabled` in its own vocabulary — true only when idle between
  cascade/gravity/refill steps). Screen Flow owns a second, independent
  signal: `overlay_is_active`, true whenever `PAUSE` or `SETTINGS` is open on
  top of `GAMEPLAY`. Juice Layer owns a third, independent signal:
  `juice_input_lock` (`juice-layer.md` §10) — true for the full duration of
  its Reveal Queue replay, which outlives Board Engine's own busy window
  (Board Engine reports idle the instant its synchronous resolution loop
  finishes, well before the Juice Layer has replayed a single visible frame
  of that move). The value Touch & Input's own Rule 4 actually gates on is
  the logical composition of all three (Formula 5) — Screen Flow never
  overwrites Board Engine's internal signal, and Juice Layer's veto is
  composed on top of both rather than requiring Board Engine or Touch & Input
  to know Juice Layer exists. This closes the loop `juice-layer.md` §10
  proposed as a "recommended follow-up, not made there" — the follow-up is
  made here, in this revision.
- **`attempt_number` supply.** Screen Flow owns a single in-memory counter
  per level, reset to `1` on every fresh entry from the map or from a
  results screen's "Next Level" (T4, T18), and incremented by exactly `1`
  on every Restart or Retry (T11, T17) — matching `rng-service.md`'s
  documented policy verbatim ("Resets to 1 only on fresh level entry from
  the map; does not persist across app relaunch mid-session"). Screen Flow
  passes this value to Board Engine at the start of every bootstrap; Board
  Engine forwards it unmodified into `RNG_Service.start_level_session()`.
  This supersedes the MVP-only Level Preview harness named in
  `board-engine.md` as production Screen Flow's role once this document
  exists.

### 8. Mascot (Fizz) Mount Points & Non-Blocking Rule

Per `characters-and-tone.md`, Fizz appears only on non-board screens, never
inside the active play area. Each mount point below shows **exactly one
line**, drawn from exactly one named pool — kept simple and testable rather
than choreographing multi-beat sequences, which is left as future UX/Juice
scope if ever wanted:

| Screen | Pool | Selection detail |
|---|---|---|
| Pre-Level Card | Level Intro | One line shown alongside the Play button. |
| Results Win | Star Milestones (sub-pool keyed to `stars_earned` ∈ {1,2,3}) | Falls back to the general Win pool if `stars_earned` resolves to `0` on a WIN outcome (Edge Cases — no 0-star milestone pool exists). |
| Results Lose | Lose | Shown alongside `closest_miss_summary` (§11), a separate, non-Fizz-owned data field. |
| World Map | *(none required at MVP)* | Fizz is a persistent idle-animation companion only; no dedicated map line pool exists yet — seam for a future addition (§11). |
| Gameplay, Pause, Settings, Boot/Loading | Never | Hard rule, inherited from `characters-and-tone.md` — never conditional, never overridden by any future screen content. |

**Non-blocking rule**: a Fizz line auto-dismisses after
`FIZZ_LINE_DISPLAY_MS` (Tuning Knobs) **and** is instantly dismissed by any
tap on that screen's primary action (Play, Retry, Map) — it never delays
that action becoming tappable, never requires its own dismiss tap, and its
selection/no-repeat pooling logic (a `game-designer`/`ui-programmer`
implementation decision per the narrative doc) never blocks the frame the
screen itself becomes interactive on.

### 9. World Map — MVP Scope & Level Unlock Gate

Per `systems-index.md`'s structural decision #4, the full multi-region node
graph belongs to Level Progression / World Map (#11, Alpha priority, not yet
authored). At MVP (10 levels, 1 region), the `WORLD_MAP` base state is
populated by a **placeholder linear level list** — the 10 levels of
`candy_kingdom_hub` in `display_number` order, each a tappable node showing
a locked/unlocked state and (once completed) a star badge. This is the same
architectural state (`B2`) the full node graph will later populate; no
state-machine rework is required when `world-map.md` supersedes the
placeholder content (§11, Declared Seams).

Which nodes are tappable is governed by a lightweight star-gate check —
explicitly the placeholder version `systems-index.md` scoped to "Level
Objective / Game UI" pending World Map's full unlock system — defined as
Formula 4, below.

### 10. Transition Juice Boundaries

Screen transitions and overlay open/close animations are presentation —
their exact visual treatment (fades, slides, easing) is Juice
Layer/UX territory. This document's job is only to bound their **maximum
duration** so navigation never itself becomes the thing slowing the player
down, expressed as both a millisecond ceiling and a frame-count budget at
the project's 60fps target (Formula 2). The concrete default values live in
Tuning Knobs (`MAX_SCREEN_TRANSITION_MS`, `MAX_MODAL_TRANSITION_MS`); no
transition — base-state or overlay — may exceed its class's ceiling.

### 11. Declared Seams

Three seams are deliberately named, not designed, in this document — each
plugs in a future system per `systems-index.md`'s dependency graph.

**`ResultsData` payload** (fired with `level_resolved`, T15/T16) —
Screen Flow's *consumption* contract only; the fields below marked "seam
owner" are computed elsewhere and simply rendered here:

| Field | Type | Seam owner | Notes |
|---|---|---|---|
| `level_id` | String | Already fixed (Level Data Format) | Pass-through, not part of the seam itself. |
| `outcome` | enum `{WIN, LOSE}` | Level Objective & Move-Limit System (#7) | Drives T15 vs. T16. |
| `stars_earned` | int, 0–3 | Scoring & Star Thresholds (#6) | Bounded by Level Data Format's already-fixed star schema; computation not invented here. |
| `score_earned` | int, ≥0 | Scoring & Star Thresholds (#6) | |
| `closest_miss_summary` | shape TBD (e.g., a normalized progress ratio or a short renderable string) | Level Objective & Move-Limit System (#7) | Screen Flow only requires that *some* renderable summary exists for Results Lose; format is explicitly not designed here. |

**World-map node state** — the full node graph, region theming, and
region-to-region unlock rules are Level Progression / World Map's (#11)
scope entirely; this document only fixes the architectural slot
(`WORLD_MAP` base state, §9) it will render into.

**Phase 2/3 mount points** — named UI anchor points reserved, unrendered at
MVP, for systems not yet approved for full design:

| Slot | Host screen | Reserved for |
|---|---|---|
| `brewing_loadout_slot` | Pre-Level Card | Booster Brewing Meta (#12, Phase 2, gated) — a future pre-level booster loadout selector. |
| `events_banner_slot` | World Map | Events/Theming Engine (#13, Phase 3) — a future seasonal event banner. |

Naming these slots now (without designing their content) lets Pre-Level
Card's and World Map's layouts reserve space conceptually without this
document overstepping into Phase 2/3 scope it has no approval to design.

---

## Formulas

### Formula 1 — Core Loop Navigation Tap Count

**Named expression:**
```
nav_taps_total = ENTRY_TAPS + (RETRY_TAPS * n_retries) + EXIT_TAPS
```

| Symbol | Type | Range | Description |
|--------|------|-------|--------------|
| `ENTRY_TAPS` | int (constant) | fixed = 2 | Taps from World Map to a fresh Gameplay attempt: tap a level node (T2) + tap Play (T4). |
| `RETRY_TAPS` | int (constant) | fixed = 1 | Taps from a Results screen directly back into a fresh attempt of the same level: tap Retry (T17) — Pre-Level Card is skipped (§6). |
| `n_retries` | int | 0 – unbounded | Number of retries taken within one continuous map→…→map loop before the player finally leaves via Map. |
| `EXIT_TAPS` | int (constant) | fixed = 1 | Tap Map on a Results screen (T19). |
| `nav_taps_total` | int | ≥3, unbounded above | Total navigation-chrome taps for one full core-loop path, excluding in-board swipes. |

**Output range**: unbounded above (a player may retry indefinitely); floor
of 3 (`n_retries = 0`). Not clamped — a measurement, not a runtime cap.

**Worked example**: a player enters a level, loses once, retries, then wins,
then returns to the map.
```
ENTRY_TAPS = 2, RETRY_TAPS * n_retries = 1 * 1 = 1, EXIT_TAPS = 1
nav_taps_total = 2 + 1 + 1 = 4
```
Isolating just the lose→retry sub-path: `RETRY_TAPS = 1`, which is
`<= TAP_BUDGET_RETRY` (2, Tuning Knobs) — the flow-state requirement is met
with one full tap of headroom unused at MVP.

---

### Formula 2 — Screen Transition Duration Frame Budget

**Named expression:**
```
frame_budget = ceil(duration_ms / FRAME_MS)
FRAME_MS = 1000 / 60
```

| Symbol | Type | Range | Description |
|--------|------|-------|--------------|
| `duration_ms` | float | 0 – `MAX_SCREEN_TRANSITION_MS` or `MAX_MODAL_TRANSITION_MS` | The authored duration of a given transition or overlay open/close animation. |
| `FRAME_MS` | float (constant) | fixed ≈16.6667 | Duration of one frame at the project's 60fps target (`technical-preferences.md`). |
| `frame_budget` | int | ≥0 | Frames the transition is allotted to complete in, rounded up. |

**Output range**: `frame_budget` is always a non-negative integer; it is a
ceiling implementers/QA verify against, not a value this system computes at
runtime.

**Worked example**: `MAX_SCREEN_TRANSITION_MS = 300` (default) →
`frame_budget = ceil(300 / 16.6667) = 18` frames for any base-state
transition (e.g., T15, Gameplay → Results Win). `MAX_MODAL_TRANSITION_MS =
200` (default) → `frame_budget = ceil(200 / 16.6667) = 12` frames for any
overlay open/close.

---

### Formula 3 — Retry Perceived Latency Budget

**Named expression:**
```
t_perceived_ms = t_exit_ms + t_reset_ms + t_enter_ms
constraint: t_perceived_ms <= RETRY_LATENCY_BUDGET_MS
```

| Symbol | Type | Range | Description |
|--------|------|-------|--------------|
| `t_exit_ms` | float | 0 – `MAX_SCREEN_TRANSITION_MS` | Duration of the Results screen's exit transition (Formula 2 bounds it). |
| `t_reset_ms` | float | provisional allocation, 0–1,400 | Time for Gameplay to reset to a fresh attempt (new RNG session + Board Engine bootstrap) for an **already-resident** level. This is a budget allocation this document proposes, not an authoritative measurement — Board Engine owns the true cost. |
| `t_enter_ms` | float | 0 – `MAX_SCREEN_TRANSITION_MS` | Duration of the Gameplay entry transition, ending at the first interactive frame. |
| `RETRY_LATENCY_BUDGET_MS` | float (constant, tuning knob) | 1,500–2,500, default 2,000 | The flow-state hard ceiling (`game-concept.md`: "Instant retry"). |
| `t_perceived_ms` | float | 0 – unbounded, constrained by the inequality | Total elapsed time from the Retry tap to a fully interactive fresh board. |

**Output range**: not clamped by this document — a budget the composed
system must satisfy; a violation is a cross-system tuning/performance issue
to escalate, not a runtime-enforced cap. **Scope note**: this formula
applies specifically to transitions where the target `level_id` is already
resident in memory (T11 Restart, T17 Retry). It does not bound first-time
entry to a new level (T4, T18), which may include a level-file load step —
`game-concept.md`'s flow-state ask specifically names *retry*, not first
entry, as the instant-feeling path.

**Worked example**: `t_exit_ms = 250`, `t_reset_ms = 900`,
`t_enter_ms = 250`.
```
t_perceived_ms = 250 + 900 + 250 = 1,400ms <= 2,000ms
```
Passes with 600ms of headroom.

---

### Formula 4 — Level Unlock Gate (MVP Placeholder)

**Named expression:**
```
is_unlocked(level) = (display_number(level) == 1)
                      OR (best_stars(level_prev) >= MIN_STARS_TO_UNLOCK_NEXT)
```

| Symbol | Type | Range | Description |
|--------|------|-------|--------------|
| `display_number(level)` | int | 1–120 (launch scope) | This level's sequence number, read from Level Data Format (already-fixed field). |
| `level_prev` | level reference or none | — | The level whose `display_number` is one less, within the same region; none if `display_number == 1`. |
| `best_stars(level_prev)` | int | 0–3, defaults to 0 if never played | Read from Save & Persistence's `level_records[level_id].best_stars` (already-fixed field). |
| `MIN_STARS_TO_UNLOCK_NEXT` | int (tuning knob) | 0–2, default 1 | Minimum stars required on the previous level to unlock this one. |
| `is_unlocked(level)` | bool | {true, false} | Whether this level's World Map node is tappable (T2 vs. T3). |

**Output range**: boolean; level 1 of a region is always `true` (no gate).
Explicitly the lightweight MVP placeholder flagged by `systems-index.md`'s
structural decision #4 — no concept of region-to-region gating exists here;
Level Progression / World Map (#11) supersedes it entirely.

**Worked example**: `candy_kingdom_hub-004` (`display_number = 4`);
`level_prev = candy_kingdom_hub-003`, whose save record shows
`best_stars = 2`. `MIN_STARS_TO_UNLOCK_NEXT = 1`. `2 >= 1` → `is_unlocked =
true`. If `candy_kingdom_hub-003` had never been completed
(`best_stars = 0`, no record per `save-persistence.md`'s default), `0 >= 1`
is false → `is_unlocked = false`, and T3 (no-op tap) applies.

---

### Formula 5 — Effective Board Input Enable (Composition)

**Named expression:**
```
effective_board_input_enabled = (base_state == GAMEPLAY)
                                 AND board_engine_ready
                                 AND NOT overlay_is_active
```

| Symbol | Type | Range | Description |
|--------|------|-------|--------------|
| `base_state` | enum | `{BOOT_LOADING, WORLD_MAP, GAMEPLAY, RESULTS_WIN, RESULTS_LOSE}` | Current Base Layer state (§1); Screen Flow-owned. |
| `board_engine_ready` | bool | {true, false} | Board Engine's own internal busy-state signal — true only when idle between cascade/gravity/refill steps. |
| `overlay_is_active` | bool | {true, false} | True whenever `PAUSE` or `SETTINGS` is currently open; Screen Flow-owned. |
| `effective_board_input_enabled` | bool | {true, false} | The value Touch & Input's Rule 4 busy-gate actually reads before accepting any gesture. |

**Output range**: boolean. `PRE_LEVEL_CARD` never co-occurs with
`GAMEPLAY` (§1), so `overlay_is_active` is only ever practically exercised
by `PAUSE`/`SETTINGS`, but the formula is written generally so it stays
correct if a future screen composes overlays over Gameplay differently.

**Worked example**: a player is mid-cascade (`board_engine_ready = false`)
when they tap the Pause icon (T8). At the instant of the tap:
`base_state = GAMEPLAY`, `overlay_is_active` becomes `true`,
`board_engine_ready` is still `false` (cascade still resolving) →
`effective_board_input_enabled = true AND false AND NOT true = false`
either way. Touch & Input was already dropping gestures from the live
cascade; it now stays dropped for the independent reason of the open
overlay, even once the cascade finishes settling `board_engine_ready` back
to `true`, until the player taps Resume (T10) and `overlay_is_active`
clears.

---

### Formula 6 — New-Best Detection (Pre-Attempt Snapshot Comparison)

**Named expression:**
```
is_new_best_stars = stars_earned > pre_attempt_best_stars
is_new_best_score = score_earned > pre_attempt_best_score
```

| Symbol | Type | Range | Description |
|--------|------|-------|--------------|
| `pre_attempt_best_stars` | int | 0–3 | Snapshot of `level_records[level_id].best_stars` (or 0 if no record exists), captured by Screen Flow at the moment this Gameplay attempt begins (T4/T11/T17/T18) — **before** this attempt's win/loss is recorded. |
| `pre_attempt_best_score` | int | ≥0 | Same snapshot, for `best_score`. |
| `stars_earned` | int | 0–3 | This attempt's result, from `ResultsData` (§11). |
| `score_earned` | int | ≥0 | This attempt's result, from `ResultsData`. |
| `is_new_best_stars`, `is_new_best_score` | bool | {true, false} | Whether this attempt improved on the player's prior record; drives an optional "New Best!" badge on Results Win. |

**Output range**: boolean pair. On a level's first-ever completion,
`pre_attempt_best_stars = 0` and `stars_earned >= 1` on any win, so
`is_new_best_stars` is always `true` — the formula is self-consistent at
the boundary without a special case.

**Design rationale (why a snapshot, not a live comparison)**: per
`save-persistence.md` §3, `record_level_completion()` fires "before the
results/star-ceremony screen even animates" — by the time Results Win reads
`get_profile()`, the live record has **already** been overwritten with this
attempt's result via that document's Formula 2 monotonic merge. Comparing
against the live, already-updated profile would make `is_new_best`
trivially wrong. The snapshot must be taken strictly before the attempt, at
Gameplay entry, per the Data Contract (§5).

**Worked example**: existing record `best_stars = 2, best_score = 3400`
(snapshotted as `pre_attempt_best_stars = 2, pre_attempt_best_score = 3400`
at bootstrap). This attempt resolves `stars_earned = 3, score_earned =
3900`.
```
is_new_best_stars = 3 > 2 = true
is_new_best_score = 3900 > 3400 = true
```
Results Win renders a "New Best!" badge. If instead this attempt resolved
`stars_earned = 2, score_earned = 3100` (worse than before), both flags are
`false` — the stored record stays unchanged at `{2, 3400}` per
`save-persistence.md`'s own monotonic merge, and Screen Flow's snapshot
comparison correctly agrees rather than misreading the (also-unchanged)
live profile as ambiguous evidence.

---

## Edge Cases

| Scenario | Expected Behavior | Rationale |
|----------|--------------------|-----------|
| App backgrounds during active Gameplay with no overlay open | Auto-triggers T9: Pause overlay opens, `effective_board_input_enabled` becomes false, `flush_if_dirty()` is called | Makes suspension visible and gives an explicit single-tap way back in; never a silent state change (§4). |
| App backgrounds while any overlay is already open (Pause, Settings, Pre-Level Card) | T20 applies: `flush_if_dirty()` only, no state change | Already suspended; nothing new to do. |
| App process is killed (not just backgrounded) mid-level | Per `save-persistence.md` §10, the attempt is abandoned — no `LevelRecord` write. Relaunch is T21 → Boot/Loading → World Map, never a resumed Gameplay/Results state | Consistent with Save & Persistence's explicit "no mid-level resume" MVP call. |
| Player taps Retry from Results Lose or Results Win | T17: Pre-Level Card is unconditionally skipped, straight to a fresh Gameplay attempt | The flow-state-critical path (§6, Formulas 1 and 3). |
| Player taps "Next Level" on Results Win but no next level exists in the current build's manifest (e.g., the last MVP level) | The "Next Level" button is never rendered in this case — only Retry and Map appear | T18 is explicitly conditional; there is no legal transition target when no next level exists. |
| Player taps a locked level node on World Map | T3: no-op, no transition, no modal explanation | A locked node's visual state (disabled/dimmed) already communicates why; no extra screen needed. |
| Same setting toggled rapidly while Paused, then Resume tapped | The setting write is already debounced and in flight per `save-persistence.md` §3; Resume (T10) simply closes the overlay — no special handling needed here | Settings persistence is entirely Save & Persistence's concern; this document only routes the tap. |
| Two rapid taps on the same primary CTA (e.g., double-tapping Retry) | The state machine ignores the second trigger once a transition has begun — one tap produces exactly one transition | Prevents double-booting Gameplay or firing two overlapping transitions. |
| App backgrounds mid-transition-animation (e.g., between T15 firing and Results Win finishing its enter animation) | The transition completes instantly to its end composite state rather than resuming mid-animation on foreground return | Prevents a visually stuck or half-finished screen on return; the composite state, not the animation, is the source of truth. |
| A screen's Fizz line pool has no content available for the current locale/build | The mount point renders empty — never a placeholder string or a broken/missing-string glyph | Per `characters-and-tone.md`: "silence is valid." |
| `profile_recovery_notice_needed` is set on the profile loaded at Boot/Loading | A one-time, dismissible, non-blocking notice appears on the first World Map entry (T1) and never reappears afterward this session | Never a silent reset the player has no way to notice (`save-persistence.md` §6). |
| `ResultsData.stars_earned` resolves to `0` on a `WIN` outcome (a possible edge case for `collect_color`-only levels, pending Scoring & Star Thresholds' own resolution) | Results Win falls back to the general Win-pool line rather than a Star Milestone sub-pool | No 0-star milestone pool exists in `characters-and-tone.md`; this is not treated as an error. |
| Player opens Settings from World Map, changes a toggle, then kills the app before returning to World Map | On next launch, the setting persists (Save & Persistence's own contract); the *navigation position* does not — relaunch always starts at Boot/Loading → World Map, never a restored Settings overlay | Only data persists across a cold restart; in-memory navigation state does not (T21). |
| Pre-Level Card is open, app is backgrounded (not killed), player returns within the same session | The Pre-Level Card overlay is still open exactly as left — in-memory overlay state survives a background/foreground cycle that does not kill the process | Distinguishes "backgrounded" (state preserved) from "process killed" (state discarded, T21) — the two have different save/UI consequences and must not be conflated. |
| Both `outcome == WIN` and the move limit is simultaneously exhausted on the same resolving move | Screen Flow does not resolve this ambiguity — it strictly trusts the single `outcome` field the `level_resolved` seam event carries | Precedence between simultaneous win/lose conditions is Level Objective & Move-Limit System's future call, not invented here. |
| A level's `objectives` list has more than one entry | Pre-Level Card and Gameplay's HUD shell render badges in the array's authored order; the first entry is treated as the primary badge | Directly honors `level-data-format.md`'s own note that objective order "may be used by Game UI/Screens Flow to decide primary-badge display order." |

---

## Dependencies

| System | Direction | Nature of Dependency |
|--------|-----------|----------------------|
| Level Data Format (`level-data-format.md`, APPROVED) | This depends on it | Reads `level_id`, `display_number`, `region`, `objectives` (order-preserved), and `star_1/2/3_score` for Pre-Level Card, Gameplay HUD shell badges, and Results display. |
| Save & Persistence (`save-persistence.md`, Draft) | This depends on it | Calls `load_profile()` at Boot/Loading; `get_profile()`/`get_total_stars()` for World Map header display and Formula 4's unlock gate; `update_setting()` from Settings; `flush_if_dirty()` on background triggers (T9, T20); reads `profile_recovery_notice_needed`. Never calls `record_level_completion()` directly. **This document fulfills the reciprocal note requested in `save-persistence.md` §Dependencies.** |
| Touch & Input System (`touch-input.md`, APPROVED) | Mutual | Touch & Input's controller is hosted only while `base_state == GAMEPLAY` (§7); Screen Flow composes `overlay_is_active` into the `effective_board_input_enabled` signal Touch & Input's Rule 4 reads (Formula 5). **This document fulfills the reciprocal note requested in `touch-input.md` §Dependencies.** |
| Match-3 Board Engine (`board-engine.md`, Drafting) | Mutual | Screen Flow triggers Board Engine's bootstrap on every Gameplay entry (T4, T11, T17, T18) and supplies the caller-owned `attempt_number` parameter (§7). **This document fulfills the reciprocal note requested in `board-engine.md` §Dependencies**, superseding the MVP-only Level Preview harness as the production supplier. |
| Level Objective & Move-Limit System (#7, not yet authored) | Forward seam — this depends on it | Expected to emit `level_resolved(outcome, results_data)` (T15/T16), designed here only against Level Data Format's already-fixed `objectives`/star-threshold data surface. **Reciprocal note**: when authored, its Dependencies section must list this document and specify exactly how/when it emits `level_resolved`. |
| Scoring & Star Thresholds (#6, not yet authored) | Forward seam — this depends on it | Expected to supply `stars_earned` (0–3) and `score_earned` inside `ResultsData` (§11). **Reciprocal note required at authoring.** |
| Level Progression / World Map (#11, not yet authored, Alpha) | Forward seam — this depends on it | Expected to supersede the MVP linear level-list placeholder (§9) while reusing the same `WORLD_MAP` base state and Data Contract (§5) unchanged. **Reciprocal note required at authoring.** |
| Booster Brewing Meta (`booster-brewing.md`, #12, Phase 2, gated) | It depends on this (`systems-index.md`) | Reserved, unrendered `brewing_loadout_slot` mount point on Pre-Level Card (§11) — named only, not designed. |
| Events/Theming Engine (`events-theming.md`, #13, Phase 3) | It depends on this | Reserved, unrendered `events_banner_slot` mount point on World Map (§11) — named only, not designed. |
| Social Layer (`social-layer.md`, #14, Phase 3) | It depends on this | No mount point reserved yet; out of scope for this draft. |
| `design/narrative/characters-and-tone.md` (not a `design/gdd/` system) | This depends on it (content only) | Supplies Fizz's Level Intro / Win / Lose / Star Milestone line pools consumed at the mount points in §8. |
| `design/art/art-bible.md` (not a `design/gdd/` system) | This depends on it (constant + rule only) | Supplies card-based Menu Layout Principles, HUD Density zoning, "one primary action per screen," and the Pillar 2 Compliance Note this document's transition/data-contract rules honor. |
| `.claude/docs/technical-preferences.md` (not a `design/gdd/` system) | This depends on it (constant + rule only) | Supplies the 60fps/16.6ms frame budget consumed in Formula 2 and the ≥44px touch-target floor for every tappable chrome element. |

---

## Tuning Knobs

| Parameter | Current Value | Safe Range | Effect of Increase | Effect of Decrease |
|-----------|--------------|------------|---------------------|----------------------|
| `MAX_SCREEN_TRANSITION_MS` | 300ms | 150–500ms | More time for a base-state transition to feel polished/weighty, but eats into the retry latency budget (Formula 3) and raises Formula 2's frame count | Snappier feel and more latency headroom, but risks a transition reading as an abrupt cut if pushed too low |
| `MAX_MODAL_TRANSITION_MS` | 200ms | 100–350ms | More time for overlay open/close polish; overlays should generally feel faster than full base-state changes | Snappier overlay response; too low risks overlays feeling like a hard pop-in |
| `RETRY_LATENCY_BUDGET_MS` | 2,000ms | 1,500–2,500ms | Loosens the flow-state ceiling, giving Board Engine/RNG more headroom but weakening the "instant retry" guarantee | Tightens the ceiling, forcing tighter transition/reset budgets; going below 1,500ms risks a budget no board-reset step can realistically meet |
| `TAP_BUDGET_RETRY` | 2 (design ceiling; MVP achieves 1) | fixed at 2 | N/A — a hard design ceiling from the flow-state requirement, not a magnitude to tune up | N/A — 1 (the current measured value) is already the practical floor for a single confirming tap |
| `FIZZ_LINE_DISPLAY_MS` | 3,000ms | 2,000–5,000ms | Longer time for a player to read the line before auto-dismiss, but only matters if they haven't already tapped through (non-blocking rule, §8) | Shorter display window; since it never blocks input, a low value mainly affects players who deliberately pause to read |
| `MIN_STARS_TO_UNLOCK_NEXT` | 1 | 0–2 | Higher values gate progression more tightly, rewarding mastery before advancing | 0 removes the gate entirely (every level always unlocked); useful for QA/debug builds, not intended as a shipped default |
| `OVERLAY_STACK_MAX_DEPTH` | 2 | fixed at 2 | N/A — architectural constant; a 3rd overlay layer would require re-deriving the Legal Composite States list (§1) and has no current design need | N/A — 1 would remove the ability to open Settings from Pause (T13), a validated part of this design |
| `BOARD_RESET_BUDGET_ALLOCATION_MS` (`t_reset_ms` default) | 900ms | 400–1,400ms | Provisional advisory allocation only — the true cost is owned by Board Engine; raising this number here doesn't make a reset faster, it only changes what Formula 3 treats as "in budget" | Tightens the advisory allocation, putting more pressure on Board Engine's own bootstrap performance to hit the retry latency budget |

---

## Acceptance Criteria

**State machine / transition logic** (`tests/unit/screen-flow/`, BLOCKING
per `coding-standards.md`'s Logic-tier rule; deterministic, no real timers —
background/foreground and interrupted transitions are simulated by mocking
the relevant signal):

- [ ] Every transition T1–T21 in the State Transition Table (§2), given its
      documented `From` state and `Trigger`, produces exactly its documented
      `To` state.
- [ ] Every reachable state, after any sequence of legal transitions, is a
      member of the Legal Composite States set (§1) — no test sequence ever
      produces `(B4, [O1])`, `(B2, [O2])`, or any other combination outside
      the nine enumerated states.
- [ ] `SETTINGS` can be reached from `WORLD_MAP` (T6) and from `PAUSE`
      (T13), but never directly from `PRE_LEVEL_CARD` — no transition table
      entry connects them.
- [ ] Retry (T17) from either `RESULTS_WIN` or `RESULTS_LOSE` transitions
      directly to `(B3, [])` — the resulting state is never `(B2, [O1])`
      (Pre-Level Card is never visited on retry, §6).
- [ ] Tapping a level node where Formula 4's `is_unlocked` resolves `false`
      produces T3 (no state change) — the resulting composite state is
      identical, byte-for-byte, to the state before the tap.
- [ ] The Back Button Policy table (§3) is fully reproduced: for each of the
      9 legal composite states, simulating an OS back event produces exactly
      the documented result.
- [ ] Simulating an app-background signal while state is `(B3, [])`
      (Gameplay, no overlay) produces `(B3, [O2])` (T9) and sets
      `effective_board_input_enabled = false` (Formula 5).
- [ ] Simulating an app-background signal while state is anything other
      than `(B3, [])` (T20) produces no state change, and `flush_if_dirty()`
      is recorded as called exactly once.
- [ ] Quit to Map (T12) from `(B3, [O2])` transitions directly to
      `(B2, [])` without ever passing through `RESULTS_WIN` or
      `RESULTS_LOSE`.
- [ ] Two rapid Retry triggers fired within the same transition window
      produce exactly one state transition, not two.
- [ ] Formula 1 (`nav_taps_total`), given `n_retries = 0` and `n_retries =
      2`, reproduces `3` and `5` respectively.
- [ ] Formula 2, given `MAX_SCREEN_TRANSITION_MS = 300` and
      `MAX_MODAL_TRANSITION_MS = 200`, reproduces `frame_budget` values of
      `18` and `12` respectively.
- [ ] Formula 3's worked example (`t_exit_ms=250, t_reset_ms=900,
      t_enter_ms=250`) reproduces `t_perceived_ms = 1,400`, and the
      inequality against `RETRY_LATENCY_BUDGET_MS = 2,000` evaluates `true`.
- [ ] Formula 4 reproduces both worked-example outcomes (`best_stars=2` →
      unlocked; `best_stars=0`/no record → locked) for
      `MIN_STARS_TO_UNLOCK_NEXT = 1`.
- [ ] Formula 5 reproduces `effective_board_input_enabled = false` for the
      worked example (overlay active, regardless of `board_engine_ready`),
      and `true` only when all three of `base_state == GAMEPLAY`,
      `board_engine_ready == true`, and `overlay_is_active == false` hold
      simultaneously.
- [ ] Formula 6's worked examples reproduce `is_new_best_stars = true,
      is_new_best_score = true` for the improving-result case and `false,
      false` for the non-improving case, using the pre-attempt snapshot
      rather than a live profile re-read.

**Manual walkthrough — UI/feel** (`production/qa/evidence/`, ADVISORY per
`coding-standards.md`'s Testing Standards):

- [ ] On a physical touch device, the full loop map → level → lose → Retry
      → win → Map completes with no more taps than Formula 1 predicts for
      that retry count, and the lose→retry step feels instant with no
      visible loading spinner.
- [ ] Fizz's line on Pre-Level Card and on both Results screens never
      delays the primary CTA (Play/Retry/Map) becoming tappable — a tester
      can tap through immediately without waiting for the line's animation.
- [ ] Backgrounding the app mid-swipe during active Gameplay and returning
      shows the Pause overlay, never a stuck highlight or a mid-animation
      board.
- [ ] Android hardware/gesture back is exercised on all 9 legal composite
      states and produces the behavior documented in §3 in every case.
- [ ] All screen transitions and overlay open/closes visually complete
      within their documented millisecond ceiling on a mid-range Android
      reference device, spot-checked via frame capture.
- [ ] A locked level node cannot be tapped into a Pre-Level Card under any
      tester attempt across the full 10-level MVP set.
- [ ] Settings toggled from within Pause, then Resume tapped, shows the
      toggle already reflected the next time Settings is reopened from
      either entry point (World Map or Pause).

**Data-driven compliance**:

- [ ] No constant this document defines (`MAX_SCREEN_TRANSITION_MS`,
      `MAX_MODAL_TRANSITION_MS`, `RETRY_LATENCY_BUDGET_MS`,
      `FIZZ_LINE_DISPLAY_MS`, `MIN_STARS_TO_UNLOCK_NEXT`,
      `OVERLAY_STACK_MAX_DEPTH`) exists as a hardcoded literal scattered
      through `src/` — every instance is sourced from a single data-driven
      config location, per `coding-standards.md`.

---

## Cross-References

| This Document References | Target GDD | Specific Element Referenced | Nature |
|---|---|---|---|
| `attempt_number` reset/increment policy | `design/gdd/rng-service.md` | "Resets to 1 only on fresh level entry from the map" (§Edge Cases / Tuning Knobs) | Rule dependency — this document is the production supplier that policy anticipated. |
| `board_input_enabled` internal signal, bootstrap procedure step 2 | `design/gdd/board-engine.md` | Bootstrap procedure (§Detailed Rules), reciprocal dependency note | Rule dependency, mutual — discharged in this document's §7 and Dependencies. |
| Gesture state machine hosting, Rule 4 busy-gate | `design/gdd/touch-input.md` | §4 Input Locking During Cascade Resolution, reciprocal dependency note | Rule dependency, mutual — discharged in this document's §7 and Dependencies. |
| `objectives` array order for primary-badge display | `design/gdd/level-data-format.md` | §2 Schema v1 Field Reference, `objectives` field note | Data dependency — this document is the anticipated consumer named there. |
| `record_level_completion()` call timing, `profile_recovery_notice_needed` | `design/gdd/save-persistence.md` | §3 Save Triggers, §6 Corruption Recovery Ladder | Rule dependency — the timing assumption behind Formula 6 and the recovery-notice edge case. |
| Fizz mount points, line pools, non-blocking/silence-is-valid rules | `design/narrative/characters-and-tone.md` | §2 Mascot Guide Character — Fizz | Content dependency. |
| Card-based Menu Layout Principles, HUD Density zoning, one-primary-action rule | `design/art/art-bible.md` | UI Art Standards | Rule dependency. |

---

## Open Questions

| Question | Owner | Deadline | Resolution |
|----------|-------|----------|-----------|
| What exact shape does `closest_miss_summary` take (a single normalized ratio, a per-objective breakdown, or a pre-formatted string)? | game-designer / systems-designer | At Level Objective & Move-Limit System (#7) authoring | — |
| Does Level Objective's `level_resolved` event fire before or after Board Engine's board fully settles visually (last cascade finishes), and does Screen Flow need to wait on an additional "board is visually idle" signal before transitioning to Results? | systems-designer | At Level Objective & Move-Limit System (#7) authoring | — |
| Should the World Map's placeholder linear level-list (§9) live in a dedicated intermediate file, or should Level Progression / World Map (#11) simply replace its contents in place when authored? | game-designer | At Level Progression / World Map (#11) authoring | — |
| Should `BOARD_RESET_BUDGET_ALLOCATION_MS` be reconciled with an authoritative Board Engine bootstrap-cost measurement once that document's Vertical Slice performance pass exists? | technical-director | Post-Vertical-Slice performance validation | — |
| Does the World Map need its own idle Fizz line pool (a "map ambient" pool), or does Fizz remain a purely visual companion there through launch? | narrative-director | Before Vertical Slice content pass | — |
