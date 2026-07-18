# Accessibility Requirements: Sweet Cascade

> **Status**: APPROVED (ux-review, 2026-07-18) — see `design/ux/ux-review-2026-07-18.md`
> **Author**: ux-designer
> **Last Updated**: 2026-07-18
> **Accessibility Tier Target**: See § Accessibility Tier Definition below — this document states a
> **working tier assessment derived from already-approved GDD commitments**, not a new policy decision.
> Formal producer sign-off on the target tier is still open (see Open Questions).
> **Platform(s)**: Mobile (iOS / Android, primary), Web (secondary — HTML playable-slice; Unity WebGL
> optional later) — `.claude/docs/technical-preferences.md`
> **External Standards Referenced**: WCAG 2.1 (contrast ratios, flash-safety threshold SC 2.3.1) —
> referenced for numeric targets only; no formal WCAG conformance level is claimed at MVP. No console
> platform (Xbox/PlayStation) is in scope, so XAG/PlayStation Accessibility Guidelines do not apply.
> **Accessibility Consultant**: None engaged.
> **Linked Documents**:
> - `design/gdd/touch-input.md` (APPROVED) — gesture grammar, 44px floor, tap-tap first-class path
> - `design/gdd/board-engine.md` (APPROVED) — Formula 4, the 44px touch-target floor proof
> - `design/gdd/juice-layer.md` (APPROVED) — reduced-motion behavior, flash safety, haptics map
> - `design/gdd/save-persistence.md` (APPROVED) — Settings sub-schema (toggle storage, defaults)
> - `design/art/art-bible.md` (Approved by founder 2026-07-17) — palette, colorblind double-coding,
>   typography, contrast
> - `design/ux/interaction-patterns.md` (APPROVED, companion document)
> - `design/ux/hud.md` (APPROVED, companion document)
> - `.claude/docs/technical-preferences.md` — Input & Platform section (Touch primary, no gamepad)

> **Why this document exists**: This document captures the **project-wide accessibility commitments
> already made across approved GDDs and the art bible**, consolidated into one place, plus the gaps
> those sources leave open. It does not invent new commitments beyond what is already approved —
> where a claim below extends past what a source explicitly states, it is marked
> **[UX-authored — not GDD-sourced]**. If a future feature conflicts with a commitment recorded here,
> this document should win by default; escalate to `producer` for a formal revision rather than
> silently drifting.

---

## Accessibility Tier Definition

### Tier Definitions (studio-standard, `.claude/docs/templates/accessibility-requirements.md`)

| Tier | Core Commitment |
|---|---|
| **Basic** | Critical text readable at standard resolution. No feature requires color discrimination alone. Independent volume controls. No uncontrolled photosensitivity risk. |
| **Standard** | All of Basic, plus: full input remapping on all platforms, subtitle support with speaker ID, adjustable text size, at least one colorblind mode, no unextendable timed input. |
| **Comprehensive** | All of Standard, plus: screen reader support for menus, mono audio, difficulty assist modes, HUD repositioning, reduced motion, visual indicators for gameplay-critical audio. |
| **Exemplary** | All of Comprehensive, plus: full subtitle customization, high contrast mode, cognitive load assist tools, tactile/haptic alternatives, external audit. |

### This Project's Working Assessment

**Working Tier**: Between **Basic** and **Standard**, with several individual Standard/Comprehensive-
tier *features* already committed in approved GDDs, but **not** a full Standard-tier commitment — two
concrete gaps prevent that claim (see below). This is stated honestly rather than rounded up.

**What is already true, matched against the tier ladder**:
- ✅ Basic: readable critical text (28px/24px minimums, art bible), no color-only signal (candy
  double-coding, unconditional), independent volume controls (`music_enabled`/`sfx_enabled` are
  separate toggles), flash-safety hard ceiling (3Hz, `juice-layer.md` Formula 6).
- ✅ Standard-tier features present without the full Standard bundle: one colorblind-safe design
  (shape+color double-coding, though the formal colorblind-*simulation* pass is still a pending
  production checklist item per `art-bible.md`), a motor-accessible alternate input path (Tap-Tap,
  first-class from day one — arguably *stronger* than Standard's baseline "no unextendable timed
  input" requirement, since Sweet Cascade has no timed input at all), reduced motion mode
  (`reduced_motion_enabled`).
- ❌ **Gap 1 — no adjustable text size.** No GDD or the art bible defines a player-facing text-scale
  setting. `save-persistence.md`'s Settings sub-schema has no `text_scale` field. Only fixed minimums
  are committed (28px/24px at reference canvas).
- ❌ **Gap 2 — full input remapping is not applicable, but this is a genuine, not merely moot, gap.**
  Sweet Cascade has no gamepad support and no keyboard-driven gameplay input
  (`.claude/docs/technical-preferences.md`), so there is no default input scheme to remap in the first
  place. This satisfies the *letter* of "nothing needs remapping" but does not satisfy the *intent*
  (players who cannot perform the game's one physical input method — a touch/drag or a tap on a
  touchscreen — have no alternate input device path at all, e.g. no switch-scanning support beyond
  Tap-Tap's low precision requirement). See § Known Intentional Limitations.

**Rationale for not claiming Standard outright**: Per the studio template's own guidance, a tier claim
should be an honest, checkable statement, not the tier that "sounds right." Sweet Cascade's genre
(casual match-3, no fast-twitch input, no timed pressure) removes the most severe motor barriers by
design, which is real and valuable — but the two gaps above are real gaps, not merely unstated
features, and should not be papered over by rounding the tier up.

**Features explicitly in scope (already committed, beyond a bare Basic floor)**:
- Tap-Tap as a first-class, always-on alternate input path (not a toggle) — `touch-input.md` §7.
- Independent audio toggles for music, SFX, and haptics — `save-persistence.md` Settings sub-schema.
- Reduced-motion mode with a documented, complete behavior table (§ below) — `juice-layer.md` §8.
- Colorblind-safe candy roster by construction (shape + color double-coding), unconditional, not
  gated behind a toggle — `art-bible.md`.

**Features explicitly out of scope at MVP** (see § Known Intentional Limitations for full detail):
- Player-adjustable text scale.
- Platform screen-reader / TalkBack / VoiceOver integration, for any screen.
- Console platform accessibility API compliance (no console platform targeted).
- Full subtitle customization (moot — there is no voiced dialogue in Sweet Cascade at all; see §
  Auditory Accessibility for why this is a structural non-issue, not a deferred feature).

---

## Visual Accessibility

| Feature | Status | Source / Value |
|---|---|---|
| Minimum text size — HUD numbers (moves, score) | **Committed** | 28px minimum at the 1080×1920 reference canvas (`art-bible.md` Accessibility; Typography) |
| Minimum text size — secondary labels/body copy | **Committed** | 24px minimum at reference canvas; no text below 24px anywhere in the game (`art-bible.md`) |
| Minimum text size — Fizz mascot lines | **Committed** | Same 24px body-copy floor; Fizz is always text, never voice-only (`characters-and-tone.md`) |
| Text contrast — UI text on Patisserie Cream cards | **Committed, numerically validated below** | Cocoa Brown (`#6b4226`) on Patisserie Cream (`#fff8ef`) computed contrast ratio ≈ **8.2:1** — passes WCAG AA (4.5:1) *and* AAA (7:1) for body text. **[UX-authored — not GDD-sourced: the art bible states the pairing qualitatively; this document supplies the numeric WCAG validation.]** |
| Text contrast — UI/HUD text over region gradients | **Partially committed — verification pending** | `art-bible.md`'s Regional & Seasonal Palette System requires every region gradient to keep average lightness ≤60% so the brighter candy/UI layer stays legible against it, and explicitly flags per-region contrast verification as a **production checklist item, not yet executed** — "must be verified visually (and ideally with a contrast-ratio tool) per region before ship." Recommend applying WCAG AA (4.5:1 body / 3:1 large text) as the numeric bar when that verification pass runs. |
| Colorblind double-coding — candy roster | **Committed, unconditional** | All 5 base candy types are shape + color double-coded (Strawberry/Orange/Lemon/Apple/Grape); special candies (Striped, Color Bomb) distinguished by pattern/silhouette, never hue alone (`art-bible.md` Character Art Standards, Accessibility). This is **not** behind the `colorblind_assist_enabled` toggle — it is always on for everyone. |
| Colorblind simulation pass (protanopia/deuteranopia/tritanopia) | **Pending — production checklist item, not yet executed** | `art-bible.md` explicitly names this as an outstanding task ("Outstanding production tasks (not approval blockers): colorblind-simulation pass on the candy shape set"). The fruit-silhouette roster (2026-07-18 founder direction) has **not yet** been run through this pass. |
| `colorblind_assist_enabled` setting | **Stored, meaning not yet defined** | `save-persistence.md` stores the bit (default `false`); its visual effect is reserved for "whatever *additional* accessibility treatment a future UI/Juice Layer pass defines" — not yet designed. Do not confuse this toggle with the unconditional double-coding above, which needs no toggle. |
| Semantic accent colors distinct from candy hues | **Committed** | Warning `#d97b2e` (distinct from Citrus Orange `#ff9f45`), Error `#d94f5c` (distinct from Strawberry Red `#ff5d73`), Positive `#3ecf8e` (distinct from Apple Green `#7ddf64`) — deliberately chosen so no UI state color can be mistaken for "a candy is dangerous" (`art-bible.md` Semantic UI Accents, PROPOSED status per that document but treated as approved working direction per its founder sign-off note). |
| High contrast mode | **PROPOSED, not committed** | `art-bible.md` names a proposed toggle (2px dark outline on candy silhouettes + 15% darker background) "confirm feasibility with `ui-programmer`" — not yet a locked feature. |
| Flash / strobe safety | **Committed, hard ceiling** | No full-screen brightness/color pulse may exceed **3Hz** (`FLASH_SAFETY_MAX_HZ`, WCAG 2.3.1-sourced), enforced unconditionally — independent of `reduced_motion_enabled` (Formula 6, `juice-layer.md` §8). Default tuning values clear this with headroom (worked example: ≈2.6Hz at the fastest-paced default cadence). This is a hard floor the Juice Layer enforces by construction, not a toggle. |
| Screen flash pre-launch warning | **Not found in any source — gap** | No GDD or art-bible section mentions a pre-launch photosensitivity notice screen. Flagged in § Known Intentional Limitations. |

### Touch Target Floor — Formula 4 Citation

**Every interactive element in Sweet Cascade has an effective touch target ≥44×44px, with ≥8px
spacing between adjacent interactive elements** (`art-bible.md`; restated
`.claude/docs/technical-preferences.md`'s `MIN_TOUCH_TARGET_PX = 44` hard floor).

This is not merely a stated target — it is a **proven guarantee** for the board itself, across the
entire schema-legal grid range:

> `board-engine.md` **Formula 4 — Touch-Target Floor Proof Across the Full Grid Range (3–9)**:
> `cell_size_px(gw, gh) = min(available_width_px / gw, available_height_px / gh)` is monotonically
> non-increasing in both grid dimensions, so its global minimum over the schema-legal `[3,9] × [3,9]`
> domain occurs at the corner `(9, 9)`. Verifying the constraint there is therefore a **complete
> proof**, not a spot check: `cell_size_px(9, 9) = min(1000/9, 1080/9) = 111.11px ≥ 44px`, a headroom
> ratio of **2.52×** at the schema's most extreme dimensions. Every other legal grid size produces a
> strictly larger cell — e.g. the reference 8×8 level computes to 125px.

Hit-testing itself always uses the **full cell pitch** (`cell_size_px`), never the visually smaller
84%-scaled candy sprite, per `touch-input.md` Formula 3 — this guarantees zero dead zones between
adjacent candies and keeps the tappable region strictly larger than the drawn art.

**Chrome buttons, chips, and map nodes** (icon buttons, primary/secondary CTAs, world map nodes) carry
the same ≥44×44px floor as a design requirement, restated per-pattern in
`design/ux/interaction-patterns.md` — these are not covered by Formula 4's board-specific proof and
must each be verified individually against real asset sizes before implementation sign-off.

---

## Motor Accessibility

| Feature | Status | Notes |
|---|---|---|
| Alternate input path for the core mechanic (no drag required) | **Committed, first-class** | Tap-Tap ships simultaneously with Swipe, always — never a fallback or a settings-gated mode (`touch-input.md` §1, §7). Directly serves players using adaptive switches or other single-tap devices who cannot perform a drag gesture. Must independently complete a full level start to finish. |
| No maximum hold duration on any gesture | **Committed** | A slow, deliberate press/drag is still valid — deliberate accessibility choice for a casual, older-skewing (25–45) audience (`touch-input.md` § Detailed Rules 1). |
| No timed input anywhere in core gameplay | **Committed by omission — verified, no source contradicts this** | No GDD defines a QTE, timed choice, or rapid-input-sequence requirement in Sweet Cascade's MVP scope. Move limits (`move_limit`) are a *count* constraint, never a *clock* constraint. |
| Single-touch-only gesture handling | **Committed** | While a gesture is in progress, any additional simultaneous touch point is ignored — only the first/primary contact drives the gesture (`touch-input.md` §5, accidental-touch rejection). Prevents an off-hand brush during one-handed play from corrupting a gesture. |
| One-handed reach for all interactive chrome | **Committed, structurally** | Board occupies the middle third of the portrait screen; interactive chrome (Pause, Settings gear) sits in a bottom corner within thumb reach; read-only status sits in the top third, where a thumb never needs to reach (`art-bible.md` HUD Density & Layout). See § One-Handed Reach Notes below. |
| Full input remapping | **N/A by design, flagged as a real gap** | No default input scheme exists to remap (touch/tap/swipe is the entire input surface; no gamepad, no keyboard gameplay input). See § Known Intentional Limitations. |
| Aim assist / precision assist | **N/A** | Not applicable — Sweet Cascade has no aiming or fine-cursor-precision mechanic; the 44px floor (above) is this game's precision-accessibility equivalent. |
| HUD element repositioning | **Not committed** | No source defines a player-facing HUD reposition feature. Not expected to be needed at Sweet Cascade's HUD density (3 read-only chips + 1 icon button — see `design/ux/hud.md`), but noted as absent rather than silently assumed. |

### One-Handed Reach Notes

Sweet Cascade's entire interactive surface is designed for one-handed portrait play
(`.claude/docs/technical-preferences.md` Platform Notes):
- The board (the highest-frequency interaction) occupies the middle third of the screen — reachable
  by a thumb without requiring the player to reposition their grip mid-session.
- The only persistent interactive chrome during gameplay (the Pause icon) sits in a bottom corner,
  the natural resting position of a one-handed thumb grip.
- Read-only status (move counter, objective tracker, score) sits in the **top third**, deliberately
  placed where a thumb does *not* need to reach — because it is read-only, reach distance for it does
  not matter (`art-bible.md` HUD Density & Layout: "a thumb doesn't need to reach here").
- **[UX-authored — not GDD-sourced]**: no source locks *which* bottom corner hosts the Pause icon, or
  whether a left-handed mirroring option exists. `design/ux/interaction-patterns.md` records a
  right-corner recommendation as an open question, not a locked decision — left-handed players are a
  currently-undocumented gap, not a solved one.

---

## Cognitive Accessibility

| Feature | Status | Notes |
|---|---|---|
| Objective/progress always visible, never hidden or requiring a menu dig | **Committed** | Level Objective's `ObjectiveDisplayModel` and `objective_progressed` signal drive live HUD chip updates during play (`level-objectives.md` § Detailed Rules 5, 10) — full spec in `design/ux/hud.md`. |
| Pause anywhere during gameplay | **Committed** | Pause is reachable at any point during `GAMEPLAY` via the Pause icon or an automatic app-background trigger (`screen-flow.md` T8, T9). No gameplay state exists where pausing is blocked. |
| No hidden countdown / no surprise ambush | **Committed** | `move_limit` and every objective target are always visible, numeric, and authored — the player is never asked to guess "am I doing well?" (`level-objectives.md` § Player Fantasy, "Legibility of purpose"). |
| Low-moves warning gives advance notice, not a last-second surprise | **Committed** | `is_low_moves` triggers at `moves_remaining ≤ 3` (default), giving the player multiple moves of advance notice before the level can end (`level-objectives.md` Formula 6). |
| "So close" failure framing is emotionally honest, not comforting fiction | **Committed** | The closest-miss metric (`scoring-stars.md` Formula 8) measures actual proximity to the first star threshold — never a made-up percentage (`scoring-stars.md` § Player Fantasy, "'So close' must be true"). |
| Retry requires minimal taps, no re-navigation tax after a loss | **Committed, formally bounded** | Retry from either Results screen skips the Pre-Level Card entirely and reaches a fresh attempt in exactly 1 tap (`RETRY_TAPS = 1`, `screen-flow.md` Formula 1, `TAP_BUDGET_RETRY` design ceiling of 2, currently met with headroom). |
| Never-punishing failure copy | **Committed** | The words "fail," "lose," "lost," and "wrong" are never used in player-facing copy; every lose line uses "so close!" energy (`characters-and-tone.md` § Failure Framing Rule). |
| Reading level appropriate to target audience | **Committed** | Target 4th–6th grade reading level, short sentences, common words, no untranslatable idioms — matches the 25–45 casual, often-distracted audience (`characters-and-tone.md`). |
| Difficulty options (granular assist sliders) | **Not committed — not in MVP scope** | No GDD defines a difficulty-assist system. Not flagged as a defect given the genre (no combat/reflex difficulty axis exists to make adjustable), but noted as absent per this document's honesty mandate. |
| Objective clarity within 2 taps at all times | **Committed** | The `ObjectiveDisplayModel` is present on the HUD continuously during gameplay (not gated behind a menu) — 0 taps required, exceeding the studio-standard "within 2 button presses" bar. |

---

## Auditory Accessibility

**Structural note**: Sweet Cascade has **no voiced dialogue anywhere in the game** — Fizz is
"text-only for launch scope, no voice acting is implied or committed" (`characters-and-tone.md` §2).
This changes the shape of this section relative to the studio's generic template: the
"subtitles for all spoken dialogue" requirement is **structurally satisfied by design**, not by a
toggle — there is no audio-only narrative content to caption in the first place.

| Feature | Status | Notes |
|---|---|---|
| Dialogue captioning | **N/A — structural non-issue** | No voiced dialogue exists. All Fizz content is always rendered as text. |
| Independent volume controls | **Committed** | `music_enabled` and `sfx_enabled` are independent toggles, both default `true` (`save-persistence.md` Settings sub-schema). No separate "voice" bus exists (none is needed — no VO). |
| Gameplay-critical audio has a visual equivalent | **Committed, by construction** | Every event in the Juice Layer's Feedback Vocabulary (`juice-layer.md` §4) pairs a visual response with its audio cue — audio is never the sole channel for any gameplay-relevant signal (match confirmation, swap rejection, low-moves warning, milestone escalation, win/lose). The one partial exception — `audio_cascade_finale`, an optional embellishment tail with no dedicated new visual — carries no new *information* beyond what `cascade_ended`'s existing visual state already communicated, so it does not violate the "every informative sound has a visual equivalent" rule. |
| Haptic feedback toggle | **Committed** | `haptics_enabled`, default `true`, independent of audio toggles (`save-persistence.md`). Has no effect on Web export (no haptics API there) — this is itself documented, not a silent gap. Full haptics map in `juice-layer.md` §7. |
| Mono audio option | **Not committed — not in MVP scope** | No GDD defines this. Flagged as absent; relevant primarily to players with single-sided deafness listening on stereo hardware. |
| Hearing-aid-frequency audit | **Not committed — not in MVP scope** | No GDD audits Sweet Cascade's SFX for high-frequency-only critical cues; given every cue already has a visual pair (row above), this is a lower-priority gap than in an audio-only-cue-heavy game, but it is unverified, not verified-safe. |

---

## AccessibilityRole — Unity 6.3 Platform Constraint

**Effective immediately for any future accessibility-node authoring work**, independent of whether
platform screen-reader support ships at MVP: Unity 6.3 converted `AccessibilityRole` from a
bitwise-flags enum to a **standard enum** — roles can no longer be bitwise-combined; every accessible
UI element must expose exactly one role (`docs/engine-reference/unity/VERSION.md`; restated
`.claude/docs/technical-preferences.md` Forbidden Patterns: "Bitwise-combined `AccessibilityRole`
values (standard enum since 6.3)").

**Practical implication for Sweet Cascade's UI Toolkit screens**: any control that might previously
have been authored as, say, `Button | Toggle` (a button that also carries toggle semantics) must be
decomposed into a single, unambiguous role per element under Unity 6.3 — this affects any future
implementation of the **Icon Button (Chrome)**, **Overlay Open/Dismiss**, and **World Map Node**
patterns (`design/ux/interaction-patterns.md`) if/when they gain platform accessibility-node metadata.
This is a forward-looking constraint to design around now, even though — per § Screen Reader Intent
below — no platform screen-reader integration ships at MVP.

---

## Screen Reader Intent — Honest MVP Scope

**What IS supported at MVP**: Nothing, at the platform-API level. No GDD, the art bible, or the
narrative doc claims VoiceOver/TalkBack/JAWS/NVDA integration for any screen (menus, HUD, or the
board) at MVP. This is stated plainly rather than implied by omission.

**What ISN'T supported at MVP**:
- No `UIAccessibility`/`AccessibilityService` node exposure on any UI Toolkit element.
- No announcement of HUD chip value changes (moves remaining, objective progress, score) to an
  assistive technology.
- No screen-reader-driven navigation path through the World Map, Pre-Level Card, Pause, or Settings.
- No alternative non-visual description of board state (candy positions/colors) — the board is a
  fundamentally visual/spatial puzzle with no designed non-visual equivalent at MVP.

**Why this is the honest call, not an oversight being quietly accepted**: Sweet Cascade is a small
indie project (per the studio's own scope), and full screen-reader support for a spatial grid puzzle
is a Comprehensive/Exemplary-tier commitment requiring dedicated engineering investment beyond
current approved scope. This is recorded here as a **known, real accessibility gap** — it
disproportionately excludes blind and low-vision players from the entire game, not just one feature —
rather than being silently absorbed into a rounded-up tier claim.

**Forward path, if this is ever prioritized**: Unity 6.3's UI Toolkit accessibility-node system,
combined with the single-role `AccessibilityRole` enum (above), is the technical mechanism that would
carry menu-level screen-reader support (matching the studio template's own Comprehensive-tier
baseline: "screen reader support for menus" — explicitly *not* extending to in-board content, which
would remain a harder, separately-scoped problem even then).

---

## Reduced Motion — Complete Behavior Table

`reduced_motion_enabled` (`save-persistence.md` Settings sub-schema, default `false`) is read **per
Reveal Step, at the moment that step begins presenting** — a setting change takes effect starting with
the very next step, never retroactively altering a step already mid-animation (`juice-layer.md` §8).

| Animation / Motion Element | When `reduced_motion_enabled = true` | Replacement Behavior | Core Feedback Preserved? |
|---|---|---|---|
| Screen-shake (camera) | **Fully disabled** — hard off, never scaled down | Camera never moves, regardless of cascade depth | Yes — no core signal lost |
| Screen-color pulse / bloom (cascade-combo full-screen pulse, cascade milestone callout's pulse) | **Fully disabled** — hard off | No replacement needed; this also trivially satisfies flash-safety, since a disabled pulse cannot flash | Yes — the milestone *text* callout and pop VFX still play |
| Particle count (every match/cascade/special pop) | **Reduced, not eliminated** — multiplied by `REDUCED_MOTION_PARTICLE_MULTIPLIER` = 0.5, stacking on top of any performance-driven LOD reduction already applied | Fewer particles, same burst shape/color | **Yes — explicitly required.** The core particle-burst pop on a match is preserved, never fully disabled, only reduced in magnitude (`art-bible.md` Accessibility; `juice-layer.md` § Player Fantasy guarantee 4) |
| Positional slide/tween transitions (swap slide, fall-drop, refill drop) | **Replaced**, not merely shortened | A shorter, opacity-based cross-fade at `REDUCED_MOTION_DURATION_SCALE` = 0.7× the normal duration, rather than a full positional traversal — communicates "this piece is now here" without vestibular-triggering motion | Yes — position change is still legible via the cross-fade |
| Modal/overlay open-close scale animation (Pre-Level Card, Pause, Settings) | **[UX-authored — not GDD-sourced, extending the same documented principle]** Cross-fade only, no scale — matches `juice-layer.md` §11's note that UI/modal transitions reuse the same tween-first animation language as board juice | Instant-feeling fade in/out | Yes — overlay open/close remains clearly communicated |
| Selection highlight (Tap-Tap armed state) | **Unaffected** — not a motion effect | N/A | Yes — always on regardless of this setting (`touch-input.md` §7) |
| Audio (all cues) | **Unaffected** | N/A | Yes — `reduced_motion_enabled` is specifically a visual-motion setting; audio has its own independent toggle |
| Haptics (all patterns) | **Unaffected** | N/A | Yes — independent toggle |
| Low-moves warning pulse (move-counter chip) | Governed by the same rule as screen-color pulse — the visual pulse dampens/disables per the reduced-motion-general rule; the numeric moves-remaining value is the primary, always-present signal regardless | Numeric value alone communicates the state if the pulse animation is suppressed | Yes |

**Interaction with performance-driven LOD**: particle-count reduction from `reduced_motion_enabled`
and particle-count reduction from the Juice Layer's own draw-call LOD ladder (`juice-layer.md` §9)
are **independent, stacking multipliers** — one is a player preference, the other is a performance
safety net, and both can be active simultaneously without either being treated as authoritative over
the other.

---

## Flash Safety

**Hard ceiling, unconditional**: no full-screen brightness/color pulse effect may exceed **3Hz**
(`FLASH_SAFETY_MAX_HZ`, WCAG 2.3.1-sourced general flash threshold guideline), enforced regardless of
the `reduced_motion_enabled` setting — reduced motion additionally disables the screen-pulse effect
outright, but even with reduced motion **off**, cadence must satisfy this ceiling
(`juice-layer.md` §8, Formula 6).

```
pulse_frequency_hz = 1000 / (pulse_duration_ms + pulse_gap_ms)
constraint: pulse_frequency_hz ≤ 3
```

**Validated by the default tuning values**: the fastest-paced default cadence (near-floor beat timing,
minimum fall distance) computes to ≈2.6Hz — under the ceiling with headroom by construction. This
should structurally never fire in practice; it is defense-in-depth, not a value the design expects to
approach in normal play. Any future reduction to `BEAT_FLOOR_MS` or `MATCH_POP_DURATION_MS` below
their documented safe ranges must re-run this check before shipping.

**Pre-launch photosensitivity warning screen**: **not found in any source — a genuine gap**, recorded
in § Known Intentional Limitations, not silently assumed to exist.

---

## Haptics & Audio Toggles

| Setting | Default | Independent of | Storage |
|---|---|---|---|
| `music_enabled` | `true` | `sfx_enabled`, `haptics_enabled` | `save-persistence.md` Settings sub-schema |
| `sfx_enabled` | `true` | `music_enabled`, `haptics_enabled` | Same |
| `haptics_enabled` | `true` | `music_enabled`, `sfx_enabled`; **no effect on Web export** (no haptics API there — stored uniformly anyway, for profile portability) | Same |
| `reduced_motion_enabled` | `false` | Audio/haptics (unaffected by this setting) | Same |
| `colorblind_assist_enabled` | `false` | All of the above; visual meaning not yet defined (see § Visual Accessibility) | Same |

All five settings are read-only from the Juice Layer / presentation layer's perspective — writes are
owned exclusively by the Settings screen (`screen-flow.md` § Data Contract Per Screen). Muting one
toggle never silently mutes another (`juice-layer.md` §6, §7).

---

## Text Size & Contrast Minimums — Cream and Gradient Surfaces

| Surface | Minimum Text Size | Contrast Target | Status |
|---|---|---|---|
| Patisserie Cream cards (`#fff8ef`) — menu/overlay body text, Cocoa Brown (`#6b4226`) | 24px body / 28px HUD numbers, reference canvas | ≈8.2:1 computed (passes AA 4.5:1 and AAA 7:1) | **Validated** — see § Visual Accessibility |
| Region gradient backgrounds (World Map, behind cards) | Same 24px/28px floors apply to any text rendered directly over a gradient (not inside a cream card) | WCAG AA (4.5:1 body / 3:1 large text) — **recommended target; not yet a locked numeric requirement in any source** | **Pending verification** — art bible's own checklist item, not yet executed per-region |
| In-board callout text (milestone "Sweet!"/"Delicious!"/"Spectacular!" overlay) | Not explicitly sized in any source | **[UX-authored — not GDD-sourced]**: recommend treating as "large text" (≥18px bold equivalent at reference scale) given its brief, high-salience presentation; apply the same 3:1 minimum against the board's dimmed backdrop | **Gap — needs `art-director` sizing decision** |
| HUD chip text (moves/objectives/score) | 28px (numbers), 24px (labels) | Chips render on a dedicated header zone, not directly on the board — contrast target should follow the cream-card row above if the chip background is a cream-family surface; needs confirmation once chip visual treatment is locked (`design/ux/hud.md`) | **Pending art direction** |

---

## Per-Feature Accessibility Matrix

| System | Visual Concerns | Motor Concerns | Cognitive Concerns | Auditory Concerns | Addressed |
|---|---|---|---|---|---|
| Board / Match-3 core loop | Candy hue distinction (colorblind) | Drag precision (44px floor); alternate tap-only path | Objective/moves legibility | Match/swap SFX — all paired with visual | **Largely addressed** — colorblind simulation pass still pending |
| HUD chips (moves/objectives/score) | Text minimum size, contrast on chip background | N/A — read-only | Always-visible progress, low-moves advance warning | N/A | **Largely addressed** — chip background contrast pending art direction |
| Overlays (Pre-Level Card, Pause, Settings) | Card contrast (validated) | 44px touch targets on all controls | Single primary action per screen (no decision overload) | UI tap SFX only, no informational-only audio | **Addressed** |
| World Map | Node lock-state must not be color-only | 44px node targets | Unlock gate always visible/numeric | N/A | **Partially addressed** — lock icon treatment pending art direction |
| Fizz mascot lines | N/A (text, not color-coded) | N/A — never independently interactive | Non-blocking, never delays primary action | Text-only, no VO — structural non-issue | **Addressed** |
| Cascade Juice (pops, callouts, screen pulse) | Flash-safety ceiling (validated); reduced-motion table (validated) | N/A | N/A | Audio paired with every visual event | **Addressed** |
| Settings screen (toggle authoring) | Not yet speced | Not yet speced | Not yet speced | Not yet speced | **Not yet addressed — out of this task's scope, flagged for `design/ux/settings.md`** |

---

## Known Intentional Limitations

| Feature | Tier It Would Require | Why Not Included at MVP | Risk / Impact | Mitigation |
|---|---|---|---|---|
| Platform screen reader support (menus and/or board) | Comprehensive+ | No engineering scope committed in any approved GDD; small indie team capacity | Excludes blind/low-vision players from the entire game, not one feature | None at MVP. Forward path noted (§ Screen Reader Intent) via Unity 6.3's UI Toolkit accessibility nodes + single-role `AccessibilityRole`, if prioritized post-MVP |
| Player-adjustable text scale | Standard | Not in any GDD's committed scope; `save-persistence.md` has no `text_scale` field | Players needing larger-than-28px text have no in-game recourse beyond OS-level display scaling (untested, likely breaks fixed-canvas HUD layout) | Fixed minimums (28px/24px) are generous relative to typical mobile body text as a partial mitigation; log for a future settings pass |
| Full input remapping / alternate input device support beyond Tap-Tap | Standard | No default input scheme exists to remap (touch-only, no gamepad); Tap-Tap already lowers the precision bar to a single, untimed tap | Players who cannot register *any* touch/tap (not just a drag) have no path into the game — a narrower but real population than "no remapping needed" implies | Tap-Tap's low precision/no-timing requirement is the practical mitigation; true switch-scanning support is not designed |
| Mono audio option | Comprehensive | Not in MVP scope | Affects single-sided-deafness players on stereo hardware | None at MVP |
| Pre-launch photosensitivity warning screen | Basic (industry-standard practice) | Not found in any GDD or the art bible — appears to be a genuine spec gap, not a deliberate omission | Standard industry courtesy notice absent; the underlying 3Hz flash-safety ceiling (which is the actual seizure-risk mitigation) is committed and validated regardless | **Recommend closing this gap** — flagged loudly in Open Questions below, since a warning screen is low-cost and this appears to be an oversight rather than a considered trade-off |
| High contrast mode | Comprehensive | PROPOSED in art bible, feasibility not yet confirmed with `ui-programmer` | Affects low-vision players who need maximum contrast beyond the base palette | None at MVP; tracked as a PROPOSED feature, not abandoned |
| HUD element repositioning | Comprehensive | Not needed at current HUD density (3 chips + 1 icon), not committed in any source | Low — HUD density is already minimal per `design/ux/hud.md`'s Rule of Necessity | None planned; revisit only if HUD density grows materially post-MVP |
| Console platform accessibility APIs (XAG, PlayStation Accessibility) | N/A | No console platform in scope (`.claude/docs/technical-preferences.md`) | None — not applicable | N/A |

---

## Accessibility Test Plan (Lightweight — MVP Scope)

Given the small feature surface above, the studio's full generic test-plan template is scoped down to
what is actually testable against this document's commitments:

| Feature | Test Method | Pass Criteria | Responsible |
|---|---|---|---|
| Text contrast (cream cards) | Automated contrast-ratio check on final UI mockups | ≥4.5:1 body, ≥3:1 large text (already validated analytically at ≈8.2:1 for the Cocoa Brown/Cream pair; re-verify against final rendered assets) | ux-designer |
| Text contrast (region gradients) | Manual — screenshot + contrast tool per region, per `art-bible.md`'s own checklist item | ≥4.5:1 for any text rendered directly over a gradient | art-director / ux-designer |
| Colorblind simulation | Manual — Coblis or equivalent simulator on the final fruit-candy roster | All 5 candy types + both specials remain distinguishable in protanopia, deuteranopia, and tritanopia simulation | art-director |
| Tap-Tap-only completion | Manual — complete a full level using zero swipe gestures | Full level completable start to finish (already an Acceptance Criterion in `touch-input.md`) | qa-tester |
| Swipe-only completion | Manual — complete a full level using zero taps | Full level completable start to finish (already an Acceptance Criterion in `touch-input.md`) | qa-tester |
| Reduced-motion mode | Manual — enable, play a full level including a 4+ chain cascade | Screen-shake and color-pulse absent; particle burst still visible at reduced count; positional tweens replaced by cross-fade | qa-tester |
| Flash-safety ceiling | Automated — instrument pulse frequency during the deepest reachable cascade | ≤3Hz measured, matching the ≈2.6Hz analytical worked example | ui-programmer |
| Independent audio/haptic toggles | Manual — toggle each of the 4 settings independently | Muting one never silently mutes another; state persists across app restart | qa-tester |
| 44px touch target floor | Automated — measure rendered hit-area of every interactive element at min and max supported grid size and at the 320px worst-case viewport (`design/ux/hud.md`) | No element measures below 44×44px in canvas-space at any tested configuration | ui-programmer |

---

## External Resources

| Resource | Relevance |
|---|---|
| WCAG 2.1 (https://www.w3.org/TR/WCAG21/) | Contrast ratio (SC 1.4.3) and flash threshold (SC 2.3.1) numeric targets referenced above |
| Colour Blindness Simulator (Coblis) | Tool named by `art-bible.md` for the pending colorblind-simulation production checklist item |

---

## Open Questions

| Question | Owner | Deadline | Resolution |
|---|---|---|---|
| **Flag for immediate attention**: should a pre-launch photosensitivity/seizure-warning notice be added? No source currently includes one, and it appears to be a genuine gap rather than a considered omission, given the game already has real (if rare) cascade-driven screen-pulse VFX. | producer / ux-designer | Before Vertical Slice content lock | Not resolved — recommend adding a one-time, dismissible notice on first launch |
| Does the project want to formally commit to a numeric accessibility tier (this document's "Basic-to-Standard" working assessment), and who signs off? | producer | Before Pre-Production gate | Not resolved |
| Should `colorblind_assist_enabled`'s visual meaning be defined now or deferred? | art-director / ux-designer | Before the pending colorblind-simulation pass | Not resolved |
| Is a player-facing text-scale setting worth adding given the fixed-canvas HUD's current layout assumptions (see `design/ux/hud.md` § 320px worst case)? | ux-designer / ui-programmer | Before HUD implementation | Not resolved |
| Does the Web build's minimum supported browser-viewport width need its own accessibility floor separate from mobile's, given desktop monitor DPI differs from phone DPI at the same logical pixel width? | ux-designer / ui-programmer | Before Web export QA pass | Not resolved — see `design/ux/hud.md` § 320px-Width Worst Case |
