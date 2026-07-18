# UX Spec: Pause Menu

> **Status**: Draft — awaiting `/ux-review`
> **Author**: ux-designer
> **Last Updated**: 2026-07-18
> **Screen / Flow Name**: `Pause` — Overlay Layer state `O2`; composite states `(B3,[O2])` and `(B3,[O2,O3])` (`design/gdd/screen-flow.md` § Detailed Rules 1)
> **Platform Target**: Mobile (iOS/Android, primary), Web (secondary — HTML playable-slice; Unity WebGL optional later) — `.claude/docs/technical-preferences.md`
> **Related GDDs**:
> - `design/gdd/screen-flow.md` (APPROVED — Overlay Layer, State Transition Table `T8`–`T14`, Back/OS-Back Policy § 3, Modal vs. Full-Screen Policy § 4, Data Contract § 5, Formula 5)
> - `design/gdd/save-persistence.md` (APPROVED — § 3 Save Triggers, § 10 No Mid-Level Resume — the exact scope of what "no resume" does and does not cover)
> - `design/gdd/juice-layer.md` (APPROVED — § 3 Reveal Queue suspension/fast-forward rule, § 10 `juice_input_lock` and its composition into `screen-flow.md`'s Formula 5)
> - `design/gdd/level-objectives.md` (Revised — § Detailed Rules 9, confirms `record_level_completion()` fires on WIN only, never on an abandoned/quit attempt)
> **Related ADRs**: ADR-001 — Engine Selection: Unity 6.3 LTS
> **Related UX Specs**:
> - `design/ux/interaction-patterns.md` — reuses Overlay Open/Dismiss (Modal Card), No-Confirmation Exit — Quit to Map, OS/Hardware Back Mapping, Icon Button (Chrome), Button (Primary CTA)/(Secondary) patterns verbatim
> - `design/ux/hud.md` — § 5 HUD States by Gameplay Context defines exactly what the frozen HUD looks like behind this overlay; this document does not redefine it
> - `design/ux/accessibility-requirements.md` — project-wide accessibility commitments inherited, not redefined
> - `design/ux/main-menu.md` — sibling spec; Quit to Map's destination (`WORLD_MAP`)
> **Accessibility Tier**: Basic-to-Standard working assessment (`design/ux/accessibility-requirements.md` § Accessibility Tier Definition) — inherited, not redefined here

---

## 1. Purpose & Player Need

**What player need does this screen serve?**

Pause exists so a player can step away from an in-progress attempt — mid-commute interruption, an
incoming call, or simply wanting a break — without any fear that doing so costs them something they
already earned, and with a guaranteed, single-tap way back into exactly where they left off. It is also
the sole gateway to three deliberate player-initiated actions mid-level: resuming, restarting the current
attempt, and abandoning it entirely to return to the map. Per `screen-flow.md`'s own Player Fantasy
guarantee 2 ("the board is sacred — nothing interrupts it uninvited"), Pause is one of only two things
allowed to interrupt a live board — the other being the app losing OS focus, which itself routes through
this exact same overlay (`T9`).

**The player goal** (what the player wants to accomplish):

Either (a) confirm the game has safely stopped and get back in with one tap (Resume), or (b) deliberately
abandon or restart the current attempt with minimal friction and zero anxiety about losing anything they
had already earned before this attempt began.

**The game goal** (what the game needs to communicate or capture):

Make the suspension of live gameplay **visible** rather than silent (`screen-flow.md` § Detailed Rules 4:
"a return-from-background feels like nothing was protected" if the game just silently freezes without
acknowledgment) — and correctly route the player's choice into exactly one of three Screen Flow
transitions (`T10` Resume, `T11` Restart, `T12` Quit to Map), never mutating any board or save state
itself. This screen owns zero direct writes beyond a defensive `flush_if_dirty()` call on the
background-trigger path (`screen-flow.md` § Detailed Rules 5, Data Contract: Pause's Writes column is
"`flush_if_dirty()` (defensive, on background trigger only)").

---

## 2. Player Context on Arrival

| Question | Answer |
|---|---|
| What was the player just doing? | Actively playing — mid-swipe, mid-cascade, or between moves — when they either (a) deliberately tapped the Pause icon, or (b) the app was backgrounded (a phone call, switching apps, the OS interrupting) |
| What is their emotional state? | Variable — a deliberate pause is usually neutral/practical ("I need to stop for a second"); an app-background interruption can carry mild anxiety about whether progress is safe |
| What cognitive load are they carrying? | Whatever they were tracking mid-board (candy positions, an in-progress cascade) is now frozen — this screen's entire job is to let that cognitive state be safely "parked," not lost |
| What information do they already have? | They know roughly how many moves/objectives remain (the HUD was visible right up until this overlay opened) — Pause does not need to re-surface that information; it is frozen, visible, and correct behind the dimmed overlay (`hud.md` § 5) |
| What are they most likely trying to do? | In the deliberate-tap case: resume almost immediately in the common case, or occasionally restart/quit. In the auto-open case: understand that the game is safely stopped and either resume when ready or leave |
| What are they likely afraid of? | Losing progress they've already earned (mitigated — nothing at risk from Pause itself, § 6); the game "cheating" them by silently discarding something without telling them (mitigated by making the suspension visible, per § 1) |

**Emotional design target for this screen**: Calm and reassuring — a clean stop, not an alarm. Nothing
about this screen should imply urgency, loss, or a countdown; it is the game politely holding still until
the player is ready to continue.

---

## 3. Navigation Position

**Screen hierarchy**:

```
Gameplay (B3) — host base state
  └── Pause (O2)                    [Overlay-Push, T8 (manual) or T9 (automatic)]
        └── Settings (O3, from Pause) [Overlay-Push, T13 — depth 2, the overlay stack's hard maximum]
```

**Modal behavior**: **Overlay** (renders over the game world, game paused) — per
`design/ux/interaction-patterns.md` § Overlay Open/Dismiss — Modal Card. Pause is dismissible by its own
primary action (Resume), by OS/hardware back (which maps to the identical Resume result, § 7.2), and by
Restart or Quit to Map (both of which also close Pause as part of a compound transition). There is no way
to have Pause open with zero dismiss path.

**Overlay stack depth**: Pause occupies stack depth **1** on top of `GAMEPLAY`. Settings, if opened from
within Pause (`T13`), occupies depth **2** — the hard architectural maximum
(`OVERLAY_STACK_MAX_DEPTH = 2`, `screen-flow.md` Tuning Knobs). No third overlay layer exists or is
reachable from here.

**Reachability — all entry points**:

| Entry Point | Triggered By | Notes |
|---|---|---|
| Tap the Pause icon | Player-initiated, from `(B3,[])` only | The icon lives in the HUD's footer zone, bottom-right (recommendation, `hud.md` § 4.3 / `interaction-patterns.md` § Icon Button (Chrome)) — it is the **sole** path to a deliberate pause |
| App loses OS focus/backgrounds | Automatic, from `(B3,[])` only | Also triggers a defensive save flush (§ 8, § 9) — this is the *same* overlay a manual tap opens, with identical visual treatment (§ 6) |

Pause is reachable from exactly one Base state (`GAMEPLAY`) and no other — it does not exist as a concept
on World Map, Pre-Level Card, or either Results screen (`screen-flow.md` § Detailed Rules 1's Overlay
Layer table: `O2`'s only legal host is `GAMEPLAY`).

---

## 4. Entry & Exit Points

**Entry table**:

| Trigger | Source Screen / State | Transition Type | Data Passed In | Notes |
|---|---|---|---|---|
| `T8` | Gameplay, no overlay (`B3,[]`) | Overlay-Push | None — the in-memory board/level session is untouched, merely frozen | Suspends gameplay (§ 9) |
| `T9` | Gameplay, no overlay (`B3,[]`) | Overlay-Push, **automatic** | None | Also triggers `flush_if_dirty()` (§ 8) — the background-save trigger this task explicitly calls out |

**Exit table**:

| Exit Action | Destination | Transition Type | Data Returned / Saved | Notes |
|---|---|---|---|---|
| Tap Resume (or OS back) | Gameplay, no overlay (`B3,[])`) | Overlay-Pop (`T10`) | None — the board resumes exactly as frozen, no reset | See § 6 for the precise scope of "resume" here vs. the unrelated "no mid-level resume" cross-session policy |
| Tap Restart Level | Gameplay, no overlay (`B3,[])`) | Overlay-Pop + reset (`T11`) | Same `level_id`; `attempt_number` increments by 1; Board Engine re-bootstraps a fresh attempt | The in-progress attempt's board state is discarded — see § 15 for an open question on whether this needs a confirmation |
| Tap Quit to Map | World Map (`B2,[])`) | Base + Pop (`T12`) | None — the abandoned attempt leaves no `LevelRecord` write | `interaction-patterns.md` § No-Confirmation Exit — Quit to Map; see § 6, § 15 |
| Tap Settings gear | Settings, from Pause (`B3,[O2,O3])`) | Overlay-Push (`T13`) | None | Depth-2 overlay; see § 6 for the frozen-behind state |

---

## 5. Layout Specification

### 5.1 Wireframe

Rendered as a Modal Card overlay per `interaction-patterns.md` § Overlay Open/Dismiss — Modal Card: a
generously-rounded Patisserie Cream (`#fff8ef`) card, centered, over a dimmed `GAMEPLAY` host screen.

```
┌──────────────────────────────────────────────────────────┐
│      (dimmed GAMEPLAY host — frozen board, frozen HUD      │
│       chips at last-known values per hud.md § 5)           │
│                                                            │
│        ╔════════════════════════════════════╗            │
│        ║                                    ║            │
│        ║              Paused                ║  ← title (label, no Fizz — § 5.3)
│        ║                                    ║            │
│        ║  ┌──────────────────────────────┐  ║            │
│        ║  │           Resume             │  ║  ← Primary CTA
│        ║  └──────────────────────────────┘  ║            │
│        ║                                    ║            │
│        ║  ┌──────────────────────────────┐  ║            │
│        ║  │       Restart Level           │  ║  ← Secondary
│        ║  └──────────────────────────────┘  ║            │
│        ║  ┌──────────────────────────────┐  ║            │
│        ║  │       Quit to Map             │  ║  ← Secondary
│        ║  └──────────────────────────────┘  ║            │
│        ║                                    ║            │
│        ║                            [⚙]     ║  ← Settings gear, Icon Button (Chrome)
│        ╚════════════════════════════════════╝            │
│                                                            │
└──────────────────────────────────────────────────────────┘
```

### 5.2 Zone Definitions

| Zone Name | Description | Approximate Size | Scrollable? | Overflow Behavior |
|---|---|---|---|---|
| Dimmed Host | The frozen `GAMEPLAY` screen (board + HUD) rendered beneath, per `hud.md` § 5's "Pause open" row | Full screen, behind the card | No | N/A — fully non-interactive while Pause is open (§ 7.3) |
| Card | Centered Patisserie Cream card hosting the title label and all four controls | **[UX-authored — not GDD-sourced]**: no source fixes exact card dimensions; recommend a compact, content-hugging card (not full-width) consistent with `art-bible.md`'s Menu Layout Principles ("generously rounded... cards hosting content"), sized to its four stacked controls plus title, not the full screen | No — content fits without scrolling by design (4 controls + 1 label is a small, fixed inventory) | N/A |

### 5.3 Component Inventory

| Component Name | Type | Zone | Purpose | Required? | Reuses Existing Pattern? |
|---|---|---|---|---|---|
| "Paused" Title Label | Static text | Card | Orients the player; confirms what screen this is | Yes | No pattern exists for a generic modal title label — **[UX-authored — not GDD-sourced]**, minimal static text, not a Fizz line (Fizz is hard-excluded here, see below) |
| Resume Button | Primary CTA | Card | Closes Pause, resumes Gameplay (`T10`) | Yes | Yes — `interaction-patterns.md` § Button (Primary CTA). Per `art-bible.md`'s one-primary-action rule, Resume is the single emphasized action |
| Restart Level Button | Secondary Button | Card | Discards the current attempt, starts fresh (`T11`) | Yes | Yes — `interaction-patterns.md` § Button (Secondary) |
| Quit to Map Button | Secondary Button | Card | Abandons the attempt, returns to World Map (`T12`) | Yes | Yes — `interaction-patterns.md` § Button (Secondary); its dismiss behavior specifically reuses § No-Confirmation Exit — Quit to Map |
| Settings Gear | Icon Button | Card (corner) | Opens Settings at overlay depth 2 (`T13`) | Yes | Yes — `interaction-patterns.md` § Icon Button (Chrome) |

**Hard exclusion — no Fizz content anywhere on this screen.** `screen-flow.md` § Detailed Rules 8's mount
point table lists `Gameplay, Pause, Settings, Boot/Loading` under the "Never" row — "Hard rule, inherited
from `characters-and-tone.md` — never conditional, never overridden by any future screen content."
`characters-and-tone.md`'s own Screen Placement section independently confirms: "Fizz never appears inside
the board frame during active play — no overlay, no interruption." Pause is explicitly an overlay *of*
active play (it hosts only on `GAMEPLAY`), so this exclusion applies without exception. This document adds
zero Fizz content, by design, not by omission.

**Primary focus element on open**: Resume — the single primary action per `art-bible.md`'s Menu Layout
Principles ("one primary action per screen... emphasized by size + saturated color"). Because this project
has no keyboard/gamepad focus model, "primary" here means visual weight and position, not an input-focus
default.

---

## 6. States & Variants

| State Name | Trigger | What Changes Visually | What Changes Behaviorally | Notes |
|---|---|---|---|---|
| Manually Opened (`(B3,[O2])`, from `T8`) | Player taps the Pause icon | Card opens over a dimmed, frozen host | Board simulation stops; input locked (§ 9) | The common case |
| Auto-Opened (`(B3,[O2])`, from `T9`) | App loses OS focus | **Identical visual treatment to the manually-opened case** — no distinct "we paused you" messaging exists in any source | Same suspension, plus a defensive save flush of the profile (§ 8, § 9) | This is intentional, not an oversight — `screen-flow.md` § Detailed Rules 4's own rationale is that the *suspension itself*, made visible via the overlay, is what reassures the player; no extra copy is specified anywhere |
| Settings Stacked (`(B3,[O2,O3])`, from `T13`)| Settings card renders on top; Pause's own card remains visible underneath but fully non-interactive | Pause's controls (Resume/Restart/Quit/Settings gear) cannot be tapped while Settings is open on top | `interaction-patterns.md` § Overlay Open/Dismiss: "there is no tunneling input through to... content behind an open overlay" — this applies to Pause exactly as it does to World Map |
| Mid-Cascade Pause | Player taps Pause icon (or the app backgrounds) while a cascade is still resolving/replaying | Card opens exactly as normal — no special "cascade interrupted" visual exists | The in-flight Reveal Queue is **fast-forwarded to completion**, not paused mid-animation (`juice-layer.md` § 3's Suspension rule) — every remaining step's end-state is applied immediately, so the board the player sees *behind* the dimmed Pause card is always fully settled, never a half-popped cascade | See § 9 for the full input-lock composition this depends on |
| Resumed (`(B3,[])`, from `T10`) | Player taps Resume or OS back | Card closes, host undims | Board simulation resumes from exactly the frozen state — no reset, no reload | See § 9's Edge Cases for the precise "this is not the same thing as mid-level resume" clarification |
| Restarted (`(B3,[])`, from `T11`) | Player taps Restart Level | Card closes; a fresh Board Engine bootstrap begins for the same `level_id` | Current in-memory board/RNG session is discarded; `attempt_number` increments | See § 15 — whether this needs a confirmation is an open question |
| Quit (`(B2,[])`, from `T12`) | Player taps Quit to Map | Card closes; World Map appears directly (no Results screen) | Current attempt is abandoned; nothing recorded | See § 9's Edge Cases for the full save-data guarantee |

---

## 7. Interaction Map

### 7.1 Navigation Inputs

| Input | Platform | Action | Visual Response | Audio Cue | Notes |
|---|---|---|---|---|---|
| Mouse hover (desktop/web only) | Mouse | Additive-only soft highlight on any of the four controls | Hover Ring pattern | None | Never required to discover or use anything (`interaction-patterns.md`'s NEVER Pattern 1) |
| Touch tap / mouse click | Touch, Mouse | Selects and activates in one gesture | Press state, then transition | `audio_ui_tap` | Standard Button (Primary/Secondary)/Icon Button press-state contract |

### 7.2 Action Inputs

| Input | Platform | Context | Action | Response | Transition | Audio/Haptic | Notes |
|---|---|---|---|---|---|---|---|
| Tap | Touch, Mouse | Resume button | Closes Pause, resumes Gameplay | Card scales/fades out, host undims | `T10`, ≤`MAX_MODAL_TRANSITION_MS` (200ms) | `audio_ui_tap` + `haptic_ui_light` | |
| Tap | Touch, Mouse | Restart Level button | Discards current attempt, re-bootstraps | Card closes; board's opening reveal replays (`BOOTSTRAP_OPEN_REVEAL`, `juice-layer.md`) | `T11`, ≤`MAX_MODAL_TRANSITION_MS` for the overlay-pop portion | `audio_ui_tap` + `haptic_ui_light` | No confirmation dialog exists in any source (§ 15 — open question on whether one should) |
| Tap | Touch, Mouse | Quit to Map button | Abandons current attempt | Card closes; World Map appears directly | `T12`, ≤`MAX_SCREEN_TRANSITION_MS` (300ms, base-state transition) | `audio_ui_tap` + `haptic_ui_light` | **No confirmation dialog, deliberately** — `interaction-patterns.md` § No-Confirmation Exit — Quit to Map |
| Tap | Touch, Mouse | Settings gear | Opens Settings at depth 2 | Settings card scales/fades in over Pause | `T13`, ≤`MAX_MODAL_TRANSITION_MS` | `audio_ui_tap` + `haptic_ui_light` | |
| OS/hardware back | Android, platform-equivalent | Anywhere on `(B3,[O2])` | Same as Resume | Same as Resume | `T10` | Same | `interaction-patterns.md` § OS/Hardware Back Mapping: "Pause | Closes Pause, resumes Gameplay | T10" |
| OS/hardware back | Android, platform-equivalent | Anywhere on `(B3,[O2,O3])` (Settings stacked) | Closes Settings only, returns to Pause | Settings card closes; Pause card re-becomes interactive | `T14` | Same | Back never skips two levels at once — it always resolves exactly one overlay-pop |

### 7.3 State-Specific Behaviors

| State | Input Restriction | Reason |
|---|---|---|
| Pause open, no Settings stacked (`(B3,[O2])`) | Only the four card controls (Resume/Restart/Quit/Settings gear) are interactive; the dimmed `GAMEPLAY` host beneath accepts zero input | `interaction-patterns.md` § Overlay Open/Dismiss's focus-trap-equivalent rule |
| Settings stacked (`(B3,[O2,O3])`) | Only Settings' own controls are interactive; Pause's own card (visible underneath) is fully non-interactive | Same rule, one layer deeper |
| Mid-cascade at the instant Pause opens | The board's own gesture input was already locked before Pause opened (per `screen-flow.md` Formula 5's `board_input_enabled`/`juice_input_lock` terms); Pause opening adds a *second*, independent veto (`overlay_is_active`) on top | See § 9's full worked-example citation — input remains locked by at least one active veto throughout, and by two simultaneously in this specific case |

---

## 8. Data Requirements

| Data Element | Source System | Update Frequency | Who Owns It | Format | Null / Missing Handling |
|---|---|---|---|---|---|
| Current `level_id`, in-memory board/RNG session state | Held in memory by Board Engine/Gameplay, not read fresh by this overlay | N/A — Pause does not query or display any of it | Board Engine / Gameplay (owns), Pause (does not touch) | N/A | N/A — Pause has zero new reads of gameplay data per `screen-flow.md`'s Data Contract: "Pause | Reads: Currently-held in-memory level context (no new reads)" |
| `settings` (read indirectly, only via the Settings gear's own destination screen) | Save & Persistence | On Settings screen open (not Pause's own concern) | Save & Persistence | Per `save-persistence.md` § 2 Settings sub-schema | N/A |

**Writes**: Pause's **only** write is a defensive `flush_if_dirty()` call, fired exclusively on the
background-trigger path (`T9`), never on a manual pause tap (`T8`). This call flushes the in-memory
**profile** (e.g., a pending debounced Settings change) to disk if — and only if — it is already dirty; it
performs zero writes if nothing has changed. **This is the exact scope of the "background-save trigger"**
called out in this document's brief: it protects settings/profile data, never the mid-level board itself
— see § 9 for the explicit boundary between the two.

**Rule** (restated from the studio template): Pause never writes directly to any board or level-progress
system. Restart (`T11`) and Quit to Map (`T12`) both discard in-memory board state via Screen Flow's own
transition logic, not via any write this screen itself issues.

---

## 9. Edge Cases

> This section is elevated to its own numbered section (ahead of Events Fired) because the task governing
> this document specifically calls out four edge-case behaviors that deserve worked, cited treatment
> rather than a single table row each: mid-cascade pause, the precise scope of "resume," the
> background-save trigger, and what quitting mid-level actually costs the player.

### 9.1 Mid-Cascade Pause — What Suspends, and How

Live board simulation is "active" only when `base_state == GAMEPLAY` **and** no overlay is open
(`screen-flow.md` § Detailed Rules 4). The moment Pause opens (`T8` or `T9`), the board freezes visually,
Board Engine performs no further simulation steps, and Touch & Input accepts no gestures — formalized as
`screen-flow.md`'s Formula 5:

```
effective_board_input_enabled = (base_state == GAMEPLAY)
                                 AND board_input_enabled
                                 AND NOT overlay_is_active
                                 AND NOT juice_input_lock
```

**Worked example, reproduced from `screen-flow.md` Formula 5** (directly relevant to the mid-cascade
case): a player is mid-cascade (`board_input_enabled = false`, still resolving) when they tap the Pause
icon. At that instant: `overlay_is_active` becomes `true` and `juice_input_lock` is also `true` (the Juice
Layer is still replaying that move's cascade) → `effective_board_input_enabled` evaluates `false` for two
independent reasons simultaneously. Touch & Input was already dropping gestures from the live cascade; it
now stays dropped for the open overlay as well, and remains dropped even once the cascade finishes
settling, until the player taps Resume and `overlay_is_active` clears.

**What the player actually sees**: `juice-layer.md` § 3's Suspension rule guarantees the in-flight Reveal
Queue is **fast-forwarded to completion**, not frozen mid-animation — "remaining `inter_step_beat_ms` and
in-flight tween durations are skipped, and every remaining step's end-state is applied immediately." So
the board rendered behind the dimmed Pause card is always fully settled the instant the card finishes
opening, never a half-popped cascade or a piece frozen mid-fall. This mirrors `screen-flow.md`'s own
identical policy for a transition interrupted mid-animation.

### 9.2 What "Resume" Does and Does Not Mean — The Precise Scope of No-Mid-Level-Resume

**These are two different things, and this document deliberately does not conflate them:**

1. **Pause → Resume (`T10`), within the same live app session.** The board was never unloaded — it is
   merely frozen in memory, visually and behaviorally, for the duration Pause is open. Tapping Resume
   simply clears the `overlay_is_active` veto term; the exact same in-memory Board Engine/RNG session
   picks back up from precisely where it stopped, with zero reset and zero reload. This is a fully
   supported, zero-cost, unlimited-use operation with no restriction from any source.

2. **"No mid-level resume" (`save-persistence.md` § 10) — a *cross-session* persistence limitation, not a
   restriction on Pause's Resume action.** That policy states MVP has no mid-level *board-state save*: if
   the app process is killed (not merely backgrounded) or the player quits to the menu before winning, the
   attempt is not recoverable — re-entering the level always starts a completely fresh attempt. **Pause's
   Resume button is never subject to this policy** — it operates entirely within one continuous, unkilled
   process. The two are related only in that both protect the same underlying guarantee ("nothing the
   player already earned is ever at risk"), not because Resume is a limited or degraded version of true
   mid-level resume.

**The boundary in practice**: if the app is *backgrounded* (not killed) while Pause is open, the in-memory
overlay and board state survive the background/foreground cycle intact — `screen-flow.md`'s own Edge Cases
table confirms this exact pattern for Pre-Level Card ("still open exactly as left... in-memory overlay
state survives a background/foreground cycle that does not kill the process") and the identical mechanism
applies to Pause. If the process is instead *killed*, that in-memory state is gone, and relaunching goes
through `T21` → Boot/Loading → World Map — never a resumed Gameplay/Pause state
(`screen-flow.md` Edge Cases: "Relaunch is T21 → Boot/Loading → World Map, never a resumed Gameplay/Results
state").

### 9.3 The Background-Save Trigger — What It Actually Protects

When the app loses OS focus during active `GAMEPLAY` with no overlay open, `T9` fires automatically:
Pause opens (§ 9.1's suspension applies) **and** `flush_if_dirty()` is called. This flush protects exactly
one thing: the in-memory **save profile** (most commonly, a pending debounced Settings change that hasn't
yet reached its `SETTINGS_SAVE_DEBOUNCE_MS` window) — never the mid-level board itself.
`save-persistence.md` § 3 states this explicitly: **"Mid-level board state (candy positions, in-progress
move count, live score before the level ends) is never a save trigger."** So the background-save trigger
is a defensive flush of *settings/profile* data, not a mechanism that makes the in-progress attempt
recoverable after a subsequent process kill — that remains governed entirely by § 9.2's no-mid-level-resume
policy. **Pause does not save board state, by design, at any point in its lifecycle.**

If the app backgrounds again *while Pause is already open* (i.e., a second, redundant background signal),
`T20` applies instead: `flush_if_dirty()` only, no state change — the profile is already flushed and the
board is already suspended, so there is nothing new to do.

### 9.4 What Quitting Mid-Level Actually Costs the Player

Both Quit to Map (`T12`, a deliberate tap) and any other form of mid-level abandonment (an app kill, or
simply never returning to the level) are treated **identically** by the save system: the attempt "counts
as not completed. No `LevelRecord` is written or touched for that attempt" (`save-persistence.md` § 10).
This is independently confirmed by `level-objectives.md` § Detailed Rules 9: `record_level_completion()`
is called **only when `outcome == WIN`, never on `LOSE`, and never at all for an attempt that is abandoned
before any outcome is evaluated** — there is no code path anywhere in the approved design that writes a
partial or zero-star record for a quit.

**Concretely, what this means for the player tapping Quit to Map**: nothing they had already earned before
this attempt began is at risk (`completion_count`, `best_stars`, and `best_score` from any prior successful
run of this level are all untouched — Save & Persistence's monotonic merge, `save-persistence.md`
Formula 2, is never even invoked). This is precisely why `interaction-patterns.md`'s
No-Confirmation Exit — Quit to Map pattern justifies skipping a confirmation dialog: "the in-progress
attempt was never going to be saved regardless... nothing the player has already earned is ever at risk
from this action."

---

## 10. Events Fired

**No analytics event catalog exists in any approved GDD for Sweet Cascade at MVP** (same honest gap noted
in the sibling `design/ux/main-menu.md` § 9). This section documents only the already-approved Screen Flow
transitions each interaction fires:

| Player Action | Transition Fired | Receiver | Notes |
|---|---|---|---|
| Tap Resume / OS back | `T10` | Screen Flow's own state machine | Clears `overlay_is_active`; Board Engine/Touch & Input resume accepting gestures the instant `effective_board_input_enabled` re-evaluates `true` |
| Tap Restart Level | `T11` | Screen Flow's own state machine, then Board Engine (bootstrap) | `attempt_number` increments; a new RNG session begins (`rng-service.md`'s documented increment policy) |
| Tap Quit to Map | `T12` | Screen Flow's own state machine | No `LevelRecord`-related event fires anywhere in this path — see § 9.4 |
| Tap Settings gear | `T13` | Screen Flow's own state machine | |
| App backgrounds during `(B3,[])` (automatic) | `T9` | Screen Flow's own state machine, then Save & Persistence's `flush_if_dirty()` | See § 9.3 |

**If `analytics-engineer` later defines an event catalog** (e.g., `pause_opened`, `level_restarted`,
`level_quit_from_pause`), add rows here without renegotiating any other section — same discipline applied
in `main-menu.md` § 9.

---

## 11. Transition & Animation

| Transition | Trigger | Direction / Type | Duration Ceiling | Interruptible? | Skipped by Reduced Motion? |
|---|---|---|---|---|---|
| Pause card open | `T8` / `T9` | Overlay-Push, card scale/fade in over a dimming host | ≤`MAX_MODAL_TRANSITION_MS` (200ms → 12 frames, `screen-flow.md` Formula 2) | No — must complete before interaction is enabled | Yes — cross-fade only, no scale (`accessibility-requirements.md` § Reduced Motion table, Modal/overlay row) |
| Pause card close (Resume) | `T10` | Overlay-Pop, card scale/fade out, host undims | ≤`MAX_MODAL_TRANSITION_MS` | No | Same |
| Pause card close (Restart) | `T11` | Overlay-Pop + reset — card closes, then Board Engine's `BOOTSTRAP_OPEN_REVEAL` plays | Overlay-pop portion ≤`MAX_MODAL_TRANSITION_MS`; the subsequent bootstrap reveal is Juice Layer's own timing (`juice-layer.md` § 4), not this document's ceiling | No | Overlay-pop portion: cross-fade only. Bootstrap reveal: governed by `juice-layer.md` § 8's reduced-motion table independently |
| Pause card close (Quit to Map) | `T12` | Base + Pop — a base-state transition, not merely an overlay-pop | ≤`MAX_SCREEN_TRANSITION_MS` (300ms → 18 frames) | No | Yes — instant appear at World Map |
| Settings card open/close (from Pause) | `T13` / `T14` | Overlay-Push/Pop over Pause | ≤`MAX_MODAL_TRANSITION_MS` | No | Cross-fade only |

---

## 12. Input Method Completeness Checklist

**Keyboard**: N/A — no keyboard-driven gameplay or menu input exists in this project
(`.claude/docs/technical-preferences.md`). See `accessibility-requirements.md` § Known Intentional
Limitations for the full, honest scope statement.

**Gamepad**: N/A — no gamepad support (`.claude/docs/technical-preferences.md`).

**Mouse** (desktop/Web secondary target):
- [x] Hover states defined for all four controls (Resume, Restart, Quit, Settings gear — Hover Ring pattern)
- [x] Clickable hit targets ≥44×44px (`interaction-patterns.md` § Button (Primary CTA)/(Secondary)/Icon Button (Chrome))
- [ ] Right-click behavior — not defined by any source; recommend no-op, consistent with `main-menu.md`'s identical flagged gap
- [x] No scrollable content exists on this screen (§ 5.2) — scroll wheel behavior is N/A by design

**Touch** (primary):
- [x] All touch targets ≥44×44px (`accessibility-requirements.md` § Touch Target Floor)
- [x] All four controls achievable with one hand in portrait orientation — the card is compact and centered, well within one-handed thumb reach at any screen position
- [ ] Long-press behavior — not defined by any source; no long-press interaction exists on this screen

---

## 13. Screen-Level Accessibility Requirements

**Text contrast requirements for this screen**:

| Text Element | Background Context | Required Ratio | Status |
|---|---|---|---|
| "Paused" title label | Patisserie Cream card | ≥4.5:1 (WCAG AA) | **Validated at the palette level** — Cocoa Brown (`#6b4226`) on Patisserie Cream (`#fff8ef`) computes to ≈8.2:1, already validated in `accessibility-requirements.md` § Visual Accessibility for this exact color pair, reused here unmodified |
| Button labels (Resume/Restart/Quit) | Button fill (varies by Primary/Secondary state) | ≥4.5:1 | Inherited from `interaction-patterns.md`'s Button patterns — not re-derived here |

**Colorblind-unsafe elements and mitigations**: None specific to this screen — every control uses
icon+label or text+shape (button shape/size hierarchy communicates primary vs. secondary), never color
alone, consistent with `art-bible.md`'s Iconography rule ("icon + label or icon + color pairing is
mandatory everywhere").

**Focus order**: N/A — no keyboard/gamepad input model exists
(`accessibility-requirements.md` § Known Intentional Limitations).

**Screen reader announcements**: Not supported at MVP, consistent with the project-wide honest scope
statement in `accessibility-requirements.md` § Screen Reader Intent.

**Cognitive load assessment**: A single information stream — four static controls, no live data, no
timers, no urgency signal of any kind. This is the lowest-cognitive-load screen in the game by
construction; no mitigation is needed.

---

## 14. Localization Considerations

| Text Element | English Baseline | Max Characters | RTL Behavior | Overflow Behavior | Risk |
|---|---|---|---|---|---|
| "Paused" title | 6 chars | Recommend ≤20 chars | Mirror | Truncate with ellipsis if ever exceeded — non-critical content | Low |
| "Resume" | 6 chars | Recommend ≤16 chars, matching `interaction-patterns.md`'s own worked example for a short CTA label ("Ausrüsten" in German runs 9 chars for a 5-char English source) | Button layout mirrors; text right-aligns in RTL | Shrink font to 90% minimum, then truncate (per the studio's generic Button pattern precedent, `.claude/docs/templates/ux-spec.md` § 13) | Medium — German/French equivalents ("Fortsetzen," "Reprendre") run meaningfully longer |
| "Restart Level" | 14 chars | Recommend ≤24 chars | Same | Same | Medium |
| "Quit to Map" | 12 chars | Recommend ≤24 chars | Same | Same | Medium |

All four button labels are short, static, non-repeating UI chrome — none are subject to
`characters-and-tone.md`'s Fizz-specific ≤60-character repeated-line ceiling (this screen has zero Fizz
content, § 5.3), but the general 40% expansion tolerance and no-text-in-images rules apply project-wide.

---

## 15. Acceptance Criteria

**Performance**
- [ ] Pause opens (first frame visible) within `MAX_MODAL_TRANSITION_MS` (200ms) of the Pause icon tap, on minimum-spec hardware.
- [ ] Pause opens within the same ceiling when triggered automatically by an app-background event (`T9`).

**Layout & Rendering**
- [ ] The card renders with zero overlap/cutoff at every supported resolution and aspect ratio, including the 320px-viewport worst case (reused from `hud.md` § 7 for cross-screen consistency).
- [ ] All four controls (Resume, Restart, Quit to Map, Settings gear) render within the card with correct visual hierarchy — Resume unambiguously reads as the single primary action per `art-bible.md`'s one-primary-action rule.
- [ ] The dimmed host behind the card correctly shows the frozen board and frozen HUD chips at their last-known values, matching `hud.md` § 5's "Pause open" row exactly.

**Input**
- [ ] Tapping Resume, or triggering OS/hardware back while Pause is open with no Settings stacked, both produce `T10` and resume Gameplay with zero board-state change.
- [ ] Tapping Restart Level produces `T11`: same `level_id`, `attempt_number` incremented by exactly 1, a fresh Board Engine bootstrap begins.
- [ ] Tapping Quit to Map produces `T12` directly to World Map with **zero** intermediate confirmation dialog, verified across at least 5 tester attempts.
- [ ] Tapping the Settings gear opens Settings at overlay depth 2 (`(B3,[O2,O3])`); OS back from that state returns to `(B3,[O2])`, never skipping directly to `(B3,[])`.
- [ ] A gesture in progress on the board at the exact instant Pause opens (either via tap or app-background) is cancelled, never left in a half-resolved state, verified via `effective_board_input_enabled` transitioning to `false` within the same frame.

**Events & Data**
- [ ] Pause never calls any Save & Persistence write API other than `flush_if_dirty()`, and only on the `T9` (background) path — verified via no other call sites in this screen's implementation.
- [ ] A tester who mid-cascade-pauses, waits, then resumes sees the board exactly as it would have settled had Pause never opened (fast-forward-to-completion verified against `juice-layer.md` § 3).
- [ ] A tester who quits mid-level via Quit to Map, then re-enters the same level, sees no `LevelRecord` entry created and no change to any prior `best_stars`/`best_score`/`completion_count` for that level.

**Accessibility**
- [ ] All text on this screen passes the ≥4.5:1 contrast minimum specified in § 13.
- [ ] Reduced-motion setting results in cross-fade-only open/close for this overlay (no scale) per § 11 and `accessibility-requirements.md` § Reduced Motion.
- [ ] All four controls are reachable by mouse and by touch tap with no hover-only discovery requirement.

**Localization**
- [ ] No button label overflows its container in the longest-translation target language once real translated strings are available.

---

## 16. Open Questions

| Question | Owner | Deadline | Resolution |
|---|---|---|---|
| **Does Restart Level need a confirmation dialog?** No source (`screen-flow.md`, `interaction-patterns.md`) names one for `T11`, and `interaction-patterns.md`'s NEVER Pattern 5 states every destructive action needs a confirmation *except* the one explicitly-named exception (Quit to Map). Restart discards the current in-progress attempt exactly as Quit does, and the identical "nothing earned is actually at risk" reasoning applies (no `LevelRecord` exists yet for an unfinished attempt either way, § 9.4) — but `interaction-patterns.md` scoped its named exception to Quit to Map only, not generalized to Restart. **Recommendation**: extend the same no-confirmation reasoning to Restart Level, since the risk profile is identical, but this is this document's own call, not a re-statement of an already-approved pattern. | ux-designer / interaction-patterns.md's next revision pass | Before this document exits Draft | **Not resolved** — recommendation given; `interaction-patterns.md` should formally add Restart Level as a second named no-confirmation exception at its next review pass (not made here, per this document's file-edit scope) |
| Does Pause need to display any contextual information about the current attempt (moves remaining, objective progress) inside the card itself, or is relying on the frozen, visible HUD behind the dimmed overlay sufficient? No source specifies duplicate content inside the Pause card. | ux-designer / game-designer | Before this document exits Draft | Not resolved — this document currently assumes the frozen HUD behind the overlay (§ 5.1's wireframe) is sufficient and does not duplicate any HUD data inside the card |
| Exact card sizing/padding constants (not fixed by any source — § 5.2 flags this as UX-authored) | ux-designer / unity-ui-specialist | Before implementation begins | Not resolved |
| Should right-click on any Pause control (desktop/Web) have an explicit defined behavior, or is silent no-op sufficient? | ux-designer / ui-programmer | Before Web export QA pass | Not resolved — mirrors the identical open question in `main-menu.md` |
