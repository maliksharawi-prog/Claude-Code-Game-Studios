# UX Spec: Main Menu — World Map (`B2`)

> **Status**: Draft — awaiting `/ux-review`
> **Author**: ux-designer
> **Last Updated**: 2026-07-18
> **Screen / Flow Name**: `WorldMap` — composite states `(B2,[])`, `(B2,[O1])`, `(B2,[O3])` (`design/gdd/screen-flow.md` § Detailed Rules 1)
> **Platform Target**: Mobile (iOS/Android, primary), Web (secondary — HTML playable-slice; Unity WebGL optional later) — `.claude/docs/technical-preferences.md`
> **Related GDDs**:
> - `design/gdd/screen-flow.md` (APPROVED — Base/Overlay Layer state model, transition table, back-button policy, modal/suspension rules, data contract, Fizz mount points)
> - `design/gdd/world-map.md` (APPROVED — node graph model, unlock Formulas 1–6, scroll/camera policy, region theming slots)
> - `design/gdd/save-persistence.md` (APPROVED — `get_profile()`/`get_total_stars()`, Settings sub-schema, `profile_recovery_notice_needed`, corruption recovery ladder)
> - `design/gdd/level-data-format.md` (APPROVED — level manifest fields consumed at MVP)
> **Related ADRs**: ADR-001 — Engine Selection: Unity 6.3 LTS (`docs/architecture/adr-001-engine-selection-unity.md`)
> **Related UX Specs**:
> - `design/ux/interaction-patterns.md` — reuses Overlay Open/Dismiss (Modal Card), World Map Node, Icon Button (Chrome), Fizz Mount Point, OS/Hardware Back Mapping patterns verbatim
> - `design/ux/hud.md` — the companion in-`GAMEPLAY` HUD spec; explicitly **not** this document's scope (see § 1)
> - `design/ux/accessibility-requirements.md` — project-wide accessibility commitments this spec must satisfy, not redefine
> - `design/ux/pause-menu.md` — sibling overlay spec authored alongside this document; Quit to Map's destination
> **Accessibility Tier**: Basic-to-Standard working assessment (`design/ux/accessibility-requirements.md` § Accessibility Tier Definition) — inherited, not redefined here

---

## Naming Clarification — Why This Document Specs World Map, Not a New Screen

**This document was commissioned as "main-menu.md." No such screen exists in Sweet Cascade's approved
design.** `screen-flow.md` § Detailed Rules 1's Base Layer table enumerates exactly five Base states —
`BOOT_LOADING` (B1), `WORLD_MAP` (B2), `GAMEPLAY` (B3), `RESULTS_WIN` (B4), `RESULTS_LOSE` (B5) — and its
State Transition Table's very first row, **T1**, is `(B1,[]) → (B2,[])`, fired automatically the instant
the save profile finishes loading. There is no intervening main-menu state, no title screen requiring a
tap to proceed, and no `(B1,[]) → [some other screen] → (B2,[])` path anywhere in the nine Legal Composite
States enumerated in that section. **The game boots directly into the World Map.**

Per this task's governing instruction — *"if screen-flow has no separate main-menu state, spec the World
Map AS the home surface at MVP"* — this document therefore specs `WORLD_MAP` (`B2`) itself: the screen a
player sees first after boot, returns to after every level attempt (win, lose, or quit), and that hosts
Settings access. Filing it as `design/ux/main-menu.md` is a naming convenience for this task's deliverable
list, not a claim that a second, separate screen exists. Every section below refers to the literal
`WORLD_MAP` base state already defined and approved in `screen-flow.md` and `world-map.md`.

**What this means concretely for scope**: this document does **not** invent a title screen, a "tap to
start" prompt, a splash/attract loop, or a New Game/Continue choice — none of these exist in the approved
state machine, and inventing one would contradict `screen-flow.md`'s own explicit navigation contract.
Where the task brief asks for "title treatment placement," this document places a branding lockup *within*
the World Map's header zone (§ 5), not as a separate pre-map screen — flagged explicitly in § 15 as a
UX-authored addition, since no source document names a logo/title element anywhere.

---

## 1. Purpose & Player Need

**What player need does this screen serve?**

The World Map is the first thing a player sees after opening the app, and the place every single attempt
— won, lost, or abandoned — returns them to. Its job is the one a "main menu" would normally do in a more
traditional game: orient the player, offer exactly one obvious next action, and get out of the way fast.
But because Sweet Cascade has no title screen to pass through first, the World Map must do that job
*while simultaneously* being a browsable, living place (`world-map.md`'s "collector-traveler" Player
Fantasy) — a player opening the app for a two-minute commute session needs to find their next level in
under a second; a player opening it to admire progress needs room to look around. Both must be true at
once, without the screen ever feeling like it is choosing one job over the other.

**The player goal** (what the player wants to accomplish):

See where they left off and get into a level in the fewest possible taps — `screen-flow.md` Formula 1's
`ENTRY_TAPS = 2` (tap a level node, tap Play) is the measured floor this screen must not exceed for the
common case of resuming at the frontier.

**The game goal** (what the game needs to communicate or capture):

Render the player's actual, current save state — total stars, per-level completion, which levels are
reachable — with zero staleness or decoration that implies something isn't true (`art-bible.md`'s Pillar 2
Compliance Note, restated in `screen-flow.md`'s Player Fantasy guarantee 3: "every screen tells the truth,
instantly"). Route a level-node tap into Pre-Level Card (`T2`) or a Settings-gear tap into Settings
(`T6`), and do nothing else — this screen owns zero writes (`screen-flow.md` § Detailed Rules 5, Data
Contract: World Map's Writes column is "None directly (navigation only)").

---

## 2. Player Context on Arrival

| Question | Answer |
|---|---|
| What was the player just doing? | One of: (a) just opened the app cold (`T1`, Boot/Loading finished); (b) just backed out of Pre-Level Card without playing (`T5`); (c) just closed Settings (`T7`); (d) just quit a level from Pause with zero ceremony (`T12`, no-confirmation exit); (e) just finished a level, win or lose, and tapped Map (`T19`) |
| What is their emotional state? | Highly variable by arrival path — (a)/(b) neutral/browsing, (c) neutral, (d) mildly deflated but explicitly *not* punished (`characters-and-tone.md`'s never-punishing voice pillar extends to this transition even though Quit to Map itself carries no Fizz line), (e) either triumphant (win) or "so close" (lose, never framed as failure) |
| What cognitive load are they carrying? | Low in every arrival case — no gameplay state survives any of these transitions into World Map (mid-level board state is never carried forward, `save-persistence.md` § 10) |
| What information do they already have? | Their own play history (stars, which levels they've beaten) — nothing new is being revealed to them by this screen; it is a mirror of what they already know they've done |
| What are they most likely trying to do? | Find the next unplayed level (the frontier) and tap into it — the dominant, by-far-most-common case per `world-map.md` § Detailed Rules 5's own default camera behavior |
| What are they likely afraid of? | Losing track of where they were (mitigated by the frontier auto-center, § 6); seeing a wrong/stale star count (mitigated by Pillar 2's live-truth guarantee) |

**Emotional design target for this screen**: Welcoming and unhurried — a place, not a form. The player
should feel like returning to a spot they recognize, with their next step already obvious, never like
they're navigating a menu tree to find where to click.

---

## 3. Navigation Position

**Screen hierarchy**:

```
World Map (B2) — the navigation root
  ├── Pre-Level Card (O1)         [Overlay-Push, T2 — from an unlocked level node]
  └── Settings (O3, from Map)     [Overlay-Push, T6 — from the Settings gear]
```

World Map is **the root of the entire navigation graph** — it is not reachable from any deeper screen by
a "forward" navigation; every other screen's exit path ultimately leads back here (`T5`, `T7`, `T12`,
`T19`, and via `T1` after every fresh boot). No screen has World Map as a child; World Map has no parent.

**Modal behavior**: **Base (full-screen)** — World Map, by itself (`(B2,[])`), is never dimmed or covered.
When `PRE_LEVEL_CARD` (`O1`) or `SETTINGS` (`O3`) is pushed on top of it, World Map becomes the **host**
screen for a Modal overlay per the Overlay Open/Dismiss — Modal Card pattern
(`design/ux/interaction-patterns.md`): it remains visible but fully non-interactive behind the dimmed
scrim until the overlay is dismissed (`T5`/`T7`).

**Reachability — all entry points**:

| Entry Point | Triggered By | Notes |
|---|---|---|
| Boot/Loading finishes (`T1`) | Automatic, cold app start | The **only** entry point on a fresh launch — there is no intervening screen |
| App relaunch after process kill (`T21` → `T1`) | Automatic | Any prior composite state is discarded; always re-derives World Map fresh, never a remembered screen (`screen-flow.md` § Detailed Rules 7) |
| Back / Tap Back from Pre-Level Card (`T5`) | Player dismisses the card without playing | Card closes; World Map underneath is exactly as it was |
| Back / Tap Back from Settings, opened from Map (`T7`) | Player closes Settings | Returns to World Map; any changed toggle already persisted (`save-persistence.md` § 3) |
| Quit to Map from Pause (`T12`) | Player abandons an in-progress attempt | **No confirmation dialog** — see `interaction-patterns.md` § No-Confirmation Exit — Quit to Map. No `LevelRecord` write for the abandoned attempt (§ 6, § 15) |
| Tap Map / OS back from either Results screen (`T19`) | Player finishes a win or loss ceremony | Standard, expected end of the results flow |

---

## 4. Entry & Exit Points

**Entry table**:

| Trigger | Source Screen / State | Transition Type | Data Passed In | Notes |
|---|---|---|---|---|
| `T1` | Boot/Loading (`B1`) | Base (implicit, first entry) | `Profile` from `load_profile()`; `profile_recovery_notice_needed` flag if set | If the recovery notice flag is set, a one-time, dismissible, non-blocking notice appears on this same entry and never reappears this session (`screen-flow.md` Edge Cases; `save-persistence.md` § 6 rung 3) |
| `T5` | Pre-Level Card (`B2,[O1]`) | Overlay-Pop | None — World Map's own state is unaffected | Back / OS back / card's own Back button all resolve identically |
| `T7` | Settings, opened from Map (`B2,[O3]`) | Overlay-Pop | Updated `settings` (already persisted before this pop, per `save-persistence.md`'s debounce) | |
| `T12` | Pause (`B3,[O2]`) | Base + Pop | None — the abandoned attempt leaves no trace in save data | See § 6, § 15 for the "no LevelRecord write" guarantee |
| `T19` | Results Win (`B4,[]`) or Results Lose (`B5,[]`) | Base | `ResultsData` is **not** carried forward — any star/score just earned is already reflected in the profile World Map reads on this entry | A just-earned star updates the tapped node's badge and the header Total Stars chip on this very entry, with zero staleness |

**Exit table**:

| Exit Action | Destination | Transition Type | Data Returned / Saved | Notes |
|---|---|---|---|---|
| Tap an **unlocked** level node | Pre-Level Card (`B2,[O1]`) | Overlay-Push (`T2`) | `level_id` of the tapped node | |
| Tap a **locked** level node | No-op (`T3`) | — | Nothing — resulting state is byte-identical to before the tap | No dialog explaining the lock; the node's own dimmed/desaturated visual state already communicates why (`screen-flow.md` Edge Cases) |
| Tap the Settings gear | Settings (`B2,[O3]`) | Overlay-Push (`T6`) | None | |
| OS/hardware back at the map root | OS default (app suspend/exit) | **Not intercepted** | — | Deliberate: a custom "exit app?" confirmation dialog would itself be a tax on the player's time, violating the "never makes you wait" Player Fantasy guarantee (`screen-flow.md` § 3) |

---

## 5. Layout Specification

### 5.1 Wireframe

Drawn at the 1080×1920 reference canvas (`design/ux/hud.md` § 3.1's established reference — reused here
for cross-screen consistency, not because `hud.md` itself scopes to this screen).

```
0                                                          1080   (CANVAS_WIDTH_PX)
┌──────────────────────────────────────────────────────────┐  0
│ [40px margin]  {Sweet Cascade}       ⭐ 42      [⚙]  [40px]│  ← HEADER ZONE
│                 title lockup      total stars  settings   │    (~header band, safe-area inset top)
├──────────────────────────────────────────────────────────┤
│                                                            │
│     (Fizz — idle companion anchor, upper-left of map      │
│      content zone, never overlapping a level node)        │
│                                                            │
│         { region diorama vignette — Candy Kingdom Hub }   │
│                                                            │
│      ●━━━○━━━○━━━○━━━○━━━○━━━○━━━○━━━○━━━○                │  ← MAP CONTENT ZONE
│      L1  L2  L3  L4  L5  L6  L7  L8  L9  L10               │    (scrollable ribbon path,
│      done done done ★★ next  locked locked ...             │     world-map.md's node graph)
│                                                            │
│   [ silhouette teaser — next region, if unlocked-depth+1 ] │
│   (MVP: absent — single-region content, world-map.md §5)  │
│                                                            │
│                    ...scrollable, pan freely...            │
│                                                            │
├──────────────────────────────────────────────────────────┤
│           (no persistent footer chrome at MVP —            │
│            no booster tray, screen-flow.md §11 seam)       │
└──────────────────────────────────────────────────────────┘  1920
```

**One-time overlay (not part of the base layout)**: the profile-recovery notice, when
`profile_recovery_notice_needed` is set, renders as a dismissible banner/toast at the top of this same
screen on the very first `T1` entry — see § 6.

### 5.2 Zone Definitions

| Zone Name | Description | Approximate Size | Scrollable? | Overflow Behavior |
|---|---|---|---|---|
| Header Zone | Title/branding lockup (left), Total Stars chip (center-right), Settings gear (right) | Full width, top band; **[UX-authored — not GDD-sourced]** exact height not fixed by any source, recommend reusing `hud.md`'s `HUD_SAFE_AREA_TOP_INSET_PX` (32px) for notch clearance only, not its full `BOARD_TOP_ALLOCATION_PX` allocation, since this screen has no board to reserve space for | No | Title lockup truncates/scales before ever colliding with the Total Stars chip or Settings gear — see § 13 |
| Map Content Zone | The scrollable region-diorama + node-ribbon path; hosts the Fizz companion anchor and any locked-region silhouette teaser | Full width, remaining height below header | **Yes** — free horizontal/vertical pan per `world-map.md` § Detailed Rules 5's Free-Scroll Range | Panning is bounded — cannot scroll past every unlocked region plus `LOCKED_REGION_PREVIEW_DEPTH` (default 1) regions ahead; nothing renders beyond that bound (not merely clipped) |
| Footer Zone | Reserved, currently empty at MVP | — | — | No content ships here yet — `brewing_loadout_slot`/`events_banner_slot` are Pre-Level Card and World Map mount points respectively for Phase 2/3 systems (`screen-flow.md` § 11), not this zone; naming them here only for future-proofing, not populating them |

### 5.3 Component Inventory

| Component Name | Type | Zone | Purpose | Required? | Reuses Existing Pattern? |
|---|---|---|---|---|---|
| Title/Branding Lockup | Static image/text lockup | Header | Identifies the game; the closest thing to a "main menu" visual this game has | Yes | No pattern exists for this — **[UX-authored — not GDD-sourced]**, see § 15 |
| Total Stars Chip | Read-only counter | Header | Displays `get_total_stars()` — a live, self-healing aggregate (`save-persistence.md` Formula 3) | Yes | No — new component, but data-committed per `screen-flow.md`'s Dependencies row: "`get_total_stars()` for World Map header display" |
| Settings Gear (Icon Button) | Icon Button | Header | Opens Settings (`T6`) | Yes | Yes — `interaction-patterns.md` § Icon Button (Chrome) |
| Fizz Companion Anchor | Idle-animation sprite, non-interactive | Map Content | Persistent, always-present companion; MVP has no line pool here | Yes | Yes — `interaction-patterns.md` § Fizz Mount Point (World Map row: idle-only, no line pool) |
| Region Diorama Vignette | Static/ambient illustration | Map Content | Region 1 (Candy Kingdom Hub)'s preview art, per `world-map.md` § Detailed Rules 4's `map_diorama_asset_id` slot | Yes | No — art-owned asset, this document only declares the slot per `world-map.md`'s own scope boundary |
| Level Node (×10 at MVP) | Tappable node | Map Content | Locked / Unlocked / Completed states; opens Pre-Level Card | Yes | Yes — `interaction-patterns.md` § World Map Node |
| Node Ribbon Path | Static/decorative connector | Map Content | Visually links nodes in sequence order | Yes | Art-owned; `art-bible.md`'s World Map spec: "rendered as a ribbon in the region's trim color" |
| Locked-Region Silhouette Teaser | Static/desaturated preview | Map Content | Shown for regions within `LOCKED_REGION_PREVIEW_DEPTH` of the frontier | Conditional — **absent at MVP** (single-region content, `world-map.md` § Detailed Rules 5's "MVP degradation") | No — new component, reserved for Alpha content scope |
| Profile Recovery Notice | Dismissible banner/toast | Overlay (top of screen) | One-time, non-blocking notice per `save-persistence.md` § 6 rung 3 | Conditional — only when `profile_recovery_notice_needed` is set | No — new component; **[UX-authored — not GDD-sourced]** exact visual treatment, see § 15 |

**Primary focus element on open**: The current frontier node (§ 6) — the camera/scroll position centers
on it by default (`world-map.md` § Detailed Rules 5), so it is the visually dominant, centered element the
player's eye lands on without any input required. Because this project has no keyboard/gamepad focus
model (`.claude/docs/technical-preferences.md`: no gamepad support), "focus" here means visual/camera
centering, not an input-focus ring.

---

## 6. States & Variants

| State Name | Trigger | What Changes Visually | What Changes Behaviorally | Notes |
|---|---|---|---|---|
| First Launch | `T1`, `level_records = {}`, `total_stars = 0` | Only the first level node is Unlocked; every other node is Locked/dimmed; Total Stars chip reads `0` | Only the first node is tappable | `world-map.md` Edge Cases — "the base case of Formulas 2–3, not a special branch" |
| Returning Player (frontier) | `T1`, `T5`, `T7`, `T12`, `T19` with existing progress | Camera/scroll centers on the current frontier node (first `UNLOCKED` node scanned in manifest order); completed nodes show star badges | Frontier node is immediately tappable without any scrolling | `world-map.md` § Detailed Rules 5's default camera position rule — this **is** the "continue where you left off" affordance; there is no separate "Continue" button anywhere in any source |
| Recovery Notice Active | `T1` with `profile_recovery_notice_needed = true` | A dismissible banner/toast appears; underlying map renders normally beneath/behind it | Notice is dismissible by a tap; dismissing does not block or delay tapping a level node underneath | Never a silent reset the player has no way to notice (`save-persistence.md` § 6 rung 3); never reappears after this session's first dismissal (`screen-flow.md` T1's note) |
| Pre-Level Card Open (host, dimmed) | `T2` | World Map dims behind the card; fully visible but non-interactive | No taps reach any World Map element until the card is dismissed | `interaction-patterns.md` § Overlay Open/Dismiss — Modal Card |
| Settings Open (host, dimmed) | `T6` | Same dimming/non-interactive treatment as above | Same | |
| Mid-Scroll Browsing | Player pans away from the frontier | No state change to any node — this is a pure camera-position interaction | Locked nodes remain non-tappable no-ops regardless of scroll position | Free-scroll range is bounded per `world-map.md` § Detailed Rules 5 |
| Fully Completed (`profile_fully_completed = true`) | Every level in the manifest three-starred (`world-map.md` Formula 6) | A distinguishable completionist state — exact visual treatment not specified by any source | — | Flagged as an open design item, § 15 — the trigger condition is committed data (`profile_fully_completed`), the presentation is not |

---

## 7. Interaction Map

### 7.1 Navigation Inputs

| Input | Platform | Action | Visual Response | Audio Cue | Notes |
|---|---|---|---|---|---|
| Touch drag / mouse drag | Touch (primary), Mouse (desktop/web) | Pans the Map Content Zone's camera within the bounded free-scroll range | Camera translates smoothly with the drag | None | No inertial/momentum scroll behavior is specified by any source — implementation detail for `unity-ui-specialist` |
| Mouse hover (desktop/web only) | Mouse | Additive-only soft highlight on a hovered level node or the Settings gear | Hover Ring pattern | None | Never required to discover or use anything — `interaction-patterns.md`'s NEVER Pattern 1 (never hover-only) |
| Touch tap / mouse click on a level node | Touch, Mouse | Selects and activates in one gesture | Press state, then transition | `audio_ui_tap` | `interaction-patterns.md` § Icon Button/Button press-state contract applies to nodes identically |

### 7.2 Action Inputs

| Input | Platform | Context (What must be tapped) | Action | Response | Transition | Audio Cue | Notes |
|---|---|---|---|---|---|---|---|
| Tap | Touch, Mouse | An **unlocked** level node | Opens Pre-Level Card for that `level_id` | Card scales/fades in over a dimmed World Map | `T2` — Overlay-Push, ≤`MAX_MODAL_TRANSITION_MS` (200ms) | `audio_ui_tap` | See § World Map Node pattern for the three node visual states |
| Tap | Touch, Mouse | A **locked** level node | No-op | None — no shake, no dialog | `T3` — no state change | None | The dimmed/desaturated visual state is the only communication of "why"; `interaction-patterns.md`'s Open Questions flags whether a tiny reject-shake should be added later — not resolved, see § 15 |
| Tap | Touch, Mouse | Settings gear (Icon Button Chrome) | Opens Settings | Card scales/fades in over a dimmed World Map | `T6` — Overlay-Push, ≤`MAX_MODAL_TRANSITION_MS` | `audio_ui_tap` + `haptic_ui_light` | `interaction-patterns.md` § Icon Button (Chrome) |
| Tap | Touch, Mouse | Profile recovery notice's own dismiss control | Dismisses the notice | Notice fades/slides out | No screen-state change | `audio_ui_tap` | Never blocks a level-node tap underneath while visible — **[UX-authored — not GDD-sourced]**, non-blocking behavior modeled on the Fizz Mount Point's own non-blocking rule, since no source specifies this notice's exact interaction contract |
| OS/hardware back | Android, and any platform-equivalent system back | Anywhere on `(B2,[])` (map root) | OS default (app suspend/exit) | — | **Not intercepted** | — | `interaction-patterns.md` § OS/Hardware Back Mapping — deliberate exception to a "confirm before exit" pattern |

### 7.3 State-Specific Behaviors

| State | Input Restriction | Reason |
|---|---|---|
| Pre-Level Card or Settings open (host, dimmed) | Zero World Map inputs reach the map — every tap is captured by the topmost overlay | `interaction-patterns.md` § Overlay Open/Dismiss: "there is no tunneling input through to board or map content behind an open overlay" |
| Recovery notice active | Level nodes and Settings gear remain fully tappable underneath/around the notice | The notice is explicitly non-blocking — never delays access to the primary map functions |

---

## 8. Data Requirements

| Data Element | Source System | Update Frequency | Who Owns It | Format | Null / Missing Handling |
|---|---|---|---|---|---|
| Total stars | Save & Persistence, `get_total_stars()` | On every screen entry (`T1`, `T5`, `T7`, `T12`, `T19`) | Save & Persistence | int, self-healing aggregate (`save-persistence.md` Formula 3) | Never null — defaults to `0` on a fresh profile |
| Per-node lock/unlock/completed state | World Map's derived Formula 2, reading `level_records` presence | On every screen entry | Level Progression / World Map (derivation), Save & Persistence (source data) | Enum `{LOCKED, UNLOCKED, COMPLETED}` per node | Absence of a `level_records` entry is the expected, common "not yet completed" case — never an error state |
| Per-node `best_stars` badge (0–3) | Save & Persistence's `level_records[level_id].best_stars` | On every screen entry | Save & Persistence | int, 0–3 | No record → no badge rendered (not a "0-star" badge — see World Map Node pattern's Unlocked-not-yet-played state, which shows no badge at all) |
| Level manifest fields (`level_id`, `display_number`, `region`) | Level Data Format + `world_map_manifest.tres` (World Map's own manifest) | Static per build, read at screen entry | Level Data Format (authored), World Map (display-number derivation, Formula 4 — supersedes the level file's own authored value) | Per `level-data-format.md` schema | N/A — build-time validated, never missing at runtime |
| `profile_recovery_notice_needed` | Save & Persistence, set during `load_profile()`'s corruption recovery ladder | Read once, at `T1` only | Save & Persistence | bool | `false` in the overwhelming common case — never surfaced when absent |
| Region theming (gradient, trim, diorama asset, resident character) | `world_map_manifest.tres` (join key) + art-owned Region Theme Resource | Static per build | World Map (declares slot), `art-director` (content) | Per `world-map.md` § Detailed Rules 4 | MVP: single region, always present — no locked-region theming is rendered at MVP |

**Rule** (restated from the studio template): this screen never writes directly to any system above.
Every player action either fires a Screen Flow transition (`T2`, `T3`, `T6`) or, for Settings, delegates
the write entirely to the Settings screen itself (`screen-flow.md`'s Data Contract: World Map's own
Writes column is "None directly").

---

## 9. Events Fired

**No analytics event catalog exists in any approved GDD for Sweet Cascade at MVP.** Unlike a project with
an established telemetry system, none of the source documents governing this screen (`screen-flow.md`,
`world-map.md`, `save-persistence.md`) name an analytics event, event bus, or telemetry hook anywhere.
This section therefore documents only the already-approved Screen Flow **transitions** each interaction
fires — which are internal state-machine transitions, not external "events" with a payload/receiver
contract in the template's usual sense:

| Player Action | Transition Fired | Receiver | Notes |
|---|---|---|---|
| Tap an unlocked level node | `T2` | Screen Flow's own state machine | Not a cross-system event — an internal Base/Overlay transition already fully specified in `screen-flow.md` § 2 |
| Tap a locked level node | `T3` | Screen Flow's own state machine | No-op transition; produces byte-identical state |
| Tap Settings gear | `T6` | Screen Flow's own state machine | |

**If `analytics-engineer` later defines an event catalog** (e.g., `world_map_viewed`, `level_node_tapped`,
`recovery_notice_shown`), add rows to this table without renegotiating any other section of this document
— flagged as an explicit, honest gap rather than invented instrumentation, per the same "no hand-waving"
discipline the sibling `design/ux/` documents apply throughout.

---

## 10. Transition & Animation

| Transition | Trigger | Direction / Type | Duration Ceiling | Interruptible? | Skipped by Reduced Motion? |
|---|---|---|---|---|---|
| Boot/Loading → World Map | `T1` | Base-state transition | ≤`MAX_SCREEN_TRANSITION_MS` (300ms, `screen-flow.md` Formula 2 → 18 frames at 60fps) | No — must complete before interaction is enabled | Yes — instant appear |
| Pre-Level Card open/close | `T2` / `T5` | Overlay-Push/Pop, card scale+fade over a dimmed host | ≤`MAX_MODAL_TRANSITION_MS` (200ms → 12 frames) | No | Yes — cross-fade only, no scale (`accessibility-requirements.md` § Reduced Motion table, Modal/overlay row) |
| Settings open/close | `T6` / `T7` | Same as above | ≤`MAX_MODAL_TRANSITION_MS` | No | Same |
| Camera pan to frontier node on open | `T1`, `T5`, `T7`, `T12`, `T19` (any World Map entry) | Camera-only, not a screen transition | Not bounded by `screen-flow.md`'s Formula 2 (that formula governs screen/overlay transitions, not in-screen camera moves) — **[UX-authored — not GDD-sourced]**: recommend the camera settle within one `MAX_SCREEN_TRANSITION_MS` window (300ms) of the screen becoming visible, so the frontier reads as "already there," not as a secondary animation the player waits through | Yes — an in-progress pan may be interrupted by the player's own manual drag | Recommend instant-cut under reduced motion, consistent with the tween-first-but-skippable philosophy `juice-layer.md` § 11 claims for UI/modal transition *language* |
| Recovery notice enter/dismiss | `T1` (conditional) / player tap | Slide or fade, non-blocking | **[UX-authored — not GDD-sourced]**: recommend the same `MAX_MODAL_TRANSITION_MS` ceiling for consistency, though this is a banner, not a modal overlay | Yes | Yes — instant appear/disappear |

Screen and overlay transition **durations** are bound by `screen-flow.md` § 10 (Formula 2); their exact
visual *treatment* (fade vs. slide, easing) is Juice Layer/UX territory per that same section — this
document claims only the tween-first animation language `juice-layer.md` § 11 already establishes as the
project-wide UI convention, not a new choreography.

---

## 11. Input Method Completeness Checklist

**Keyboard**: N/A — Sweet Cascade has no keyboard-driven gameplay or menu input at any scope
(`.claude/docs/technical-preferences.md` Input & Platform: "No keyboard-driven gameplay input"). See
`design/ux/accessibility-requirements.md` § Known Intentional Limitations for the full, honest scope
statement on why this is a real, accepted gap, not an oversight.

**Gamepad**: N/A — no gamepad support in this project (`.claude/docs/technical-preferences.md`: "Gamepad
Support: None").

**Mouse** (desktop/Web secondary target):
- [x] Hover states defined for all interactive elements (level nodes, Settings gear — Hover Ring pattern, additive-only)
- [x] Clickable hit targets ≥44×44px (level nodes, Settings gear — `interaction-patterns.md` § World Map Node / Icon Button (Chrome))
- [ ] Right-click behavior — not defined by any source; recommend no-op (consistent with the rest of this project's chrome, which defines no right-click behavior anywhere) — flagged for confirmation, § 15
- [x] Scroll wheel/drag behavior defined in the scrollable Map Content Zone (§ 7.1)

**Touch** (primary):
- [x] All touch targets ≥44×44px (`accessibility-requirements.md` § Touch Target Floor — restated per-element for chrome/nodes, not covered by Board Engine's board-specific Formula 4 proof)
- [x] Swipe/pan gestures on the Map Content Zone do not conflict with system-level swipe navigation (a vertical/horizontal camera pan within a bounded scroll area is not an edge-swipe gesture)
- [x] All actions achievable with one hand in portrait orientation — Header zone and Map Content Zone are both within one-handed reach per `art-bible.md`'s HUD Density & Layout philosophy (though that section is technically scoped to in-`GAMEPLAY` HUD, the same one-handed-portrait platform commitment applies project-wide per `.claude/docs/technical-preferences.md`'s Platform Notes)
- [ ] Long-press behavior — not defined by any source; no long-press interaction exists on this screen at MVP

---

## 12. Screen-Level Accessibility Requirements

**Text contrast requirements for this screen**:

| Text Element | Background Context | Required Ratio | Status |
|---|---|---|---|
| Total Stars chip number | Header zone — likely a cream-family or region-gradient background, not yet locked | ≥4.5:1 (WCAG AA normal text) | Pending art direction — same open gap `hud.md` § Text Size & Contrast Minimums already flags for its own HUD chips; this screen shares the same unresolved dependency |
| Level node star badges | Region gradient background | ≥3:1 (WCAG AA large text/graphical objects) | Pending — `art-bible.md`'s own per-region contrast verification checklist item, not yet executed (`accessibility-requirements.md` § Visual Accessibility) |
| Recovery notice text | Banner background, not yet locked | ≥4.5:1 | Pending — new component, not yet art-directed |

**Colorblind-unsafe elements and mitigations**:

| Element | Colorblind Risk | Mitigation |
|---|---|---|
| Locked vs. unlocked node state | Risk if communicated by color/saturation alone | Locked nodes are dimmed/desaturated **and** structurally different in interaction (a no-op tap) — `interaction-patterns.md` § World Map Node already commits to a non-color-only signal (padlock icon or reduced-contrast silhouette), citing the project-wide double-coding rule. This document adds no new requirement, only inherits it |
| Star badges (0–3) | Low risk — star count is a numeric/count signal, not a hue-coded one | Star badges are already shape-based (filled vs. unfilled star icons), not color-coded severity |

**Focus order**: N/A — no keyboard/gamepad input exists to define a focus order for
(`accessibility-requirements.md` § Known Intentional Limitations). Touch/mouse interaction is
direct-manipulation (tap the element you want), not sequential-focus-based.

**Screen reader announcements**: **Not supported at MVP**, consistent with the project-wide honest scope
statement in `accessibility-requirements.md` § Screen Reader Intent — "no screen-reader-driven navigation
path through the World Map... at MVP." This document does not attempt to partially solve that gap; it is
recorded here as inherited, not re-litigated.

**Cognitive load assessment**: Two concurrent information streams on this screen — (1) which nodes are
locked/unlocked/completed, and (2) the Total Stars aggregate. This is well within the 7±2 standard limit
and is the same or lower density than `hud.md`'s own in-gameplay HUD (3 chips). No mitigation needed
beyond the default camera-centering behavior (§ 6), which already removes the "where am I" decision
entirely on the common-case entry.

---

## 13. Localization Considerations

**General rules for this screen** (restated from the studio template, applicable project-wide): text
elements must tolerate ≥40% expansion from English baseline; no text baked into images.

| Text Element | English Baseline | Max Characters | RTL Behavior | Overflow Behavior | Risk |
|---|---|---|---|---|---|
| Title/branding lockup | "Sweet Cascade" (14 chars) | N/A — **[UX-authored — not GDD-sourced]**: if this lockup is a stylized art asset (a logo), it is exempt from the "no text in images" rule the same way a company/game logo conventionally is — flagged explicitly in § 15 since no source confirms whether this should be a static art logo or a localized text string | Mirror layout position in RTL, though no RTL launch language is currently named in any source | If it is a text string, not an image, must not be baked/hardcoded — sourced from localization strings | Low if treated as a logo asset; Medium if treated as localized text (14 chars baseline is short, but German/French expansion could still crowd the header alongside the Total Stars chip and Settings gear) |
| Total Stars chip | Numeric only, no localized word ("⭐ 42") | N/A — numeric formatting only | Numerals do not mirror; container position may | Numerals never truncate — this project already commits to numeric locale-formatting discipline elsewhere (`level-objectives.md`'s HUD numbers are never abbreviated below full precision) | Low |
| Region display name (e.g., "Candy Kingdom Hub") | Up to ~20 chars for launch names | Recommend ≤30 chars, matching `characters-and-tone.md`'s general flavor-copy headroom philosophy | Mirror | Not currently rendered as a persistent on-screen label at MVP (single region, no region-switcher UI) — deferred to Alpha scope when multi-region navigation exists | Low at MVP, Medium at Alpha |
| Level node star badge / lock icon | Iconographic, no text | N/A | N/A | N/A | None — icons are not localized |
| Recovery notice copy | Not yet authored | Recommend the same ≤80-character one-time-banner ceiling `characters-and-tone.md` § Reading Level & Length already sets for "one-time banner/event copy" | Mirror | Wrap within the banner, never truncate silently — this is a trust/integrity message and must remain fully readable | Medium — exact copy not yet authored, flagged in § 15 |

---

## 14. Acceptance Criteria

**Performance**
- [ ] World Map's first frame is visible within `MAX_SCREEN_TRANSITION_MS` (300ms) of `T1` firing, on minimum-spec hardware.
- [ ] Panning the Map Content Zone produces no perceptible frame drop from the project's 60fps target (`.claude/docs/technical-preferences.md`).

**Layout & Rendering**
- [ ] Header zone (title lockup, Total Stars chip, Settings gear) renders with zero overlap at every supported resolution/aspect ratio, including the 320px-viewport worst case documented in `hud.md` § 7 (reused here for consistency, even though that section is technically scoped to the in-`GAMEPLAY` HUD).
- [ ] All 10 MVP level nodes and the region diorama render within the Map Content Zone with correct locked/unlocked/completed visual states matching World Map's Formula 2 output exactly.
- [ ] The camera/scroll position on every fresh entry (`T1`, `T5`, `T7`, `T12`, `T19`) centers on the current frontier node per `world-map.md` § Detailed Rules 5, verified across at least three distinct save-state fixtures (fresh install, mid-progress, fully-completed region).

**Input**
- [ ] Tapping any of the 10 MVP level nodes in their `UNLOCKED` or `COMPLETED` state opens Pre-Level Card for the correct `level_id` within `MAX_MODAL_TRANSITION_MS` (200ms).
- [ ] Tapping any `LOCKED` node produces zero visual/state change — verified byte-identical composite state before and after the tap.
- [ ] Tapping the Settings gear opens Settings within `MAX_MODAL_TRANSITION_MS`.
- [ ] OS/hardware back at `(B2,[])` (map root, no overlay open) produces the platform's default suspend/exit behavior — never a custom in-app confirmation dialog.
- [ ] All interactive elements (level nodes, Settings gear) are reachable by mouse and by touch tap with no element requiring hover-only discovery.

**Events & Data**
- [ ] World Map never calls any Save & Persistence write API (`update_setting`, `record_level_completion`, `flush_if_dirty`) directly — verified via no direct call sites in this screen's implementation.
- [ ] Total Stars chip and every node's star badge match `get_total_stars()`/`level_records` exactly on every entry, with zero staleness across a win→Map, lose→Map, and quit→Map transition each verified independently.
- [ ] The profile recovery notice appears exactly once per session when `profile_recovery_notice_needed` is set, and never reappears on any subsequent World Map entry within that same session.

**Accessibility**
- [ ] All text on this screen passes the minimum contrast ratios specified in § 12 once final art assets are locked (currently pending, flagged in § 15).
- [ ] Locked node state does not rely on color/desaturation alone as the sole differentiator (verified against `interaction-patterns.md` § World Map Node's non-color-only commitment).
- [ ] Reduced-motion setting results in instant camera centering and cross-fade-only overlay transitions (no scale/slide) per § 10 and `accessibility-requirements.md` § Reduced Motion.

**Localization**
- [ ] No text element on this screen overflows its container in the longest-translation target language once real translated strings are available.
- [ ] The title/branding lockup's localization treatment (logo asset vs. localized string) is explicitly resolved before this document exits Draft status (§ 15).

---

## 15. Open Questions

| Question | Owner | Deadline | Resolution |
|---|---|---|---|
| **No source names a title/logo element anywhere.** Should the World Map header host a static art-directed logo lockup, a localized text wordmark, or neither (letting the region diorama itself carry the "arrival" branding moment with no explicit title text at all)? This document's Component Inventory (§ 5.3) proposes a lockup as a placeholder pending this decision. | art-director / ux-designer | Before this document exits Draft | Not resolved |
| **No source defines a persistent "quick sound toggle" separate from Settings.** The task brief that commissioned this document named "sound toggle" as expected World Map content, but no GDD or the art bible names a quick-mute control living directly on the map — `music_enabled`/`sfx_enabled` are Settings sub-schema fields, reached only via the Settings gear (`T6`). **Recommendation**: do not add a redundant quick-mute chip — a second control surface for the same two booleans risks exactly the kind of state-divergence `art-bible.md`'s Pillar 2 Compliance Note and `interaction-patterns.md`'s NEVER Pattern 9 (never a UI element that silently diverges from actual game state) warn against, unless it reads live from the same source with zero lag. Flagged for explicit confirmation rather than silently added or silently dropped. | producer / ux-designer | Before this document exits Draft | Not resolved — recommendation given, not locked |
| Does the World Map need its own idle Fizz line pool (a "map ambient" pool), or does Fizz remain a purely visual companion there through launch? | narrative-director | Before Vertical Slice content pass | **Already an open question in `screen-flow.md`'s own Open Questions table** — restated here for this document's completeness, not newly raised |
| Should a locked World Map node get a tap-rejection micro-feedback (a tiny shake) instead of a pure no-op, to confirm the tap registered at all? | ux-designer / game-designer | Before Level Progression / World Map (#11) authoring | **Already an open question in `interaction-patterns.md`'s own Open Questions table** — restated here for cross-reference; `screen-flow.md` T3 currently specifies a pure no-op |
| What exact visual treatment and copy does the profile recovery notice use (banner vs. toast vs. modal; exact wording)? | ux-designer / narrative-director / art-director | Before Vertical Slice content lock | Not resolved — no source specifies this beyond "a dismissible, non-blocking notice" |
| Does right-click on a level node or the Settings gear (desktop/Web) need an explicit no-op confirmation, or is silence (no menu, no response) sufficient? | ux-designer / ui-programmer | Before Web export QA pass | Not resolved |
| Should this document's exact header zone sizing (title lockup + Total Stars chip + Settings gear) formally migrate a set of pixel constants the way `hud.md` § 3.1 migrated Board Engine's margin constants, once real layout is locked? | ux-designer / unity-ui-specialist | Before implementation begins | Not resolved — this document currently describes zones qualitatively, not with locked pixel constants |
