# Save & Persistence

*Status: Draft — awaiting /design-review*
*Layer: Foundation · Priority: MVP · Phase: MVP · Category: Persistence*
*Author: systems-designer · Created: 2026-07-18 · Last Updated: 2026-07-18*
*Implements Pillar: supports Pillar 2 — Clever, Never Cheated (indirectly: the
game never silently alters or discards a player's legitimately-earned record)*
*Depends On: — (none — Foundation layer, no prerequisite GDDs; reads Level
Data Format's `level_id` key format as an opaque String, not a build
dependency)*
*Depended On By: Game UI/Screens Flow, Level Progression/World Map (MVP,
active); Booster Brewing Meta (Phase 2, forward dependency); Events/Theming
Engine, Social Layer (Phase 3, forward dependencies)*
*Source: `design/gdd/game-concept.md` · `design/gdd/systems-index.md` ·
`design/gdd/level-data-format.md` · `design/gdd/rng-service.md`*

---

## Overview

Save & Persistence is the Foundation-layer system that durably stores a
single player's profile — settings, per-level star/score records, and a
handful of aggregate/bookkeeping fields — entirely on-device, with zero
backend or account dependency at MVP (`game-concept.md`: "Networking: None
for core play (offline-capable)"). It has no design-system dependencies of
its own: every input it needs (a `level_id` String and a stars/score result)
is handed to it as an opaque parameter by whichever system triggers a save,
exactly as RNG Service accepts opaque parameters rather than reading other
systems' schemas directly (`rng-service.md` §Dependencies). This document
defines schema **v1**: the fields that exist today, the atomic write
strategy that protects them against a mobile OS process kill, the ladder of
fallbacks a corrupted or missing save file falls through before the player
ever sees a silent, unexplained reset, and the versioning discipline that
lets future systems (Level Progression/World Map, Booster Brewing Meta,
Events/Theming Engine, Social Layer) extend the schema without breaking
existing save files. It is intentionally a thin, generic save/load
*contract* — it owns durability, integrity, and the shape of what's stored,
never the gameplay meaning of what's stored.

---

---

## Player Fantasy

Like RNG Service, Save & Persistence is invisible infrastructure — no player
ever opens a menu called "Save & Persistence." What they experience instead
is the *absence* of a category of bad feelings: the dread of "did that just
get wiped?", the annoyance of re-earning something they already proved they
could do, the sense that the app doesn't respect the five stolen minutes
they just spent on it. Concretely, this system exists to protect three
guarantees:

1. **Progress is never lost.** Closing the app, a phone call interrupting a
   session, the OS killing the process to reclaim memory, a battery dying
   mid-cascade — none of these should cost the player anything they had
   already legitimately earned. The worst-case cost of any interruption is
   the player's *current, unfinished* level attempt — never a level they
   already completed, never a star they already earned.
2. **Stars are permanent trophies.** Once a star is earned on a level, it is
   never revoked, never silently recalculated downward, and never at risk
   from a subsequent worse replay. A player who three-stars a hard level and
   comes back months later to a bad run should still see three stars
   waiting for them — mastery, once proven, is a fact about the player's
   history, not a live-recomputed score that can regress.
3. **The game respects your time.** Sweet Cascade's target player has 5-15
   minute sessions squeezed into a commute or a break
   (`game-concept.md`, Target Player Profile). This system's entire job is
   making sure that time is never wasted retroactively — a save that
   silently corrupts, resets without explanation, or requires the player to
   notice and manually "fix" something is a direct tax on the one resource
   this audience has the least of.

This is also this system's specific, narrow contribution to **Pillar 2 —
Clever, Never Cheated**: the game never quietly rewrites a player's own
earned history against them. A checksum failure that turns out to be a
player's own deliberate hand-edit is treated as *their* data, not an attack
to punish (see Detailed Rules §6) — because Sweet Cascade has no
leaderboard or backend at MVP to protect, punishing a solo player for
editing their own single-player save file would be paranoia in service of
nothing.

---

---

## Detailed Rules

### 1. File Format & Storage Location

- The profile is serialized as **compact UTF-8 JSON** (no pretty-printing
  whitespace in the stored file — the "canonical bytes" used for checksum
  computation, §Formulas, must be byte-identical every time the same logical
  data is serialized). JSON is chosen over a native Godot `Resource`
  (`.tres`) — the format Level Data Format uses — because that choice was
  specifically about giving *designers* a free Inspector-editing UI
  (`level-data-format.md` §1); nobody hand-edits a save file in the
  Inspector, so that benefit doesn't apply here, while JSON's benefits do:
  it is trivially portable across every Godot 4.6 export target
  (iOS/Android native filesystem and Web's virtual filesystem alike), is
  simple to checksum deterministically, and is human-inspectable for QA and
  bug triage without custom tooling.
- Two on-disk files back one logical profile (the A/B double-buffer, §4):
  conventionally `user://save/profile_a.sav` and `user://save/profile_b.sav`.
  Both live under Godot's `user://` path — the engine's per-install,
  per-platform-appropriate persistent-data convention (an app-sandboxed
  directory on iOS/Android; a virtual, IndexedDB-backed persistent
  filesystem on Web, see §9) — never under `res://` (the read-only exported
  game-content path). Exact file extension/naming is a
  `godot-gdscript-specialist` implementation detail; the two-slot contract
  and their independence from each other is the load-bearing part of this
  document.
- Total on-disk footprint for both slots combined is small (see Formula 5)
  — well inside the ≤400MB memory ceiling (`technical-preferences.md`) with
  enormous headroom, so no size-driven compression or pruning strategy is
  needed at MVP/launch scope.

### 2. Profile Schema v1 — Field Reference

**Top-level `Profile` object:**

| Field | Type | Required | Default | Description |
|---|---|---|---|---|
| `schema_version` | int | Yes | — | Schema contract version this file was authored/written against. v1 files use `1`. |
| `write_counter` | int (uint64) | Yes | `0` at profile creation | Monotonically increasing counter, incremented by exactly 1 on every successful save. Drives A/B slot selection (Formula 4) — never reset, never decremented. |
| `checksum` | int (uint32) | Yes | — | FNV-1a 32-bit checksum (Formula 1) over the canonical serialization of every other field in this table, computed fresh on every write. |
| `created_utc` | int (unix epoch seconds) | Yes | set once, at first-ever profile creation on this device | Never modified after initial creation — an immutable "born on" timestamp for this local profile. |
| `modified_utc` | int (unix epoch seconds) | Yes | — | Updated to the current time on every successful save. |
| `total_stars` | int | Yes | `0` | Aggregate star count across every `level_records` entry. **Stored (denormalized), not computed live on every read** — see Formula 3 for why, and its self-healing consistency rule. |
| `settings` | `Settings` object | Yes | see sub-schema | Player-configurable preferences (§below). |
| `level_records` | `Dictionary<level_id: String, LevelRecord>` | Yes | `{}` (empty at first launch) | Per-level progress, keyed by Level Data Format's stable `level_id` String (`level-data-format.md` §2 — never `display_number`, which is explicitly not the save-data key). |

**`Settings` sub-schema:**

| Field | Type | Required | Default | Description |
|---|---|---|---|---|
| `music_enabled` | bool | Yes | `true` | Background music toggle. |
| `sfx_enabled` | bool | Yes | `true` | Sound-effect toggle. |
| `haptics_enabled` | bool | Yes | `true` | Haptic feedback toggle; has no effect on Web (no haptics API), stored uniformly regardless of platform so the same profile is meaningful if ever synced across platforms (Phase 3). |
| `reduced_motion_enabled` | bool | Yes | `false` | Corresponds to the art bible's proposed reduced-motion toggle (`art-bible.md` §Accessibility) — dampens screen-shake/bloom. This document stores the flag only; the Juice Layer owns what it visually does with it. |
| `colorblind_assist_enabled` | bool | Yes | `false` | Stores a player-facing accessibility preference. Per the art bible, the 5 base candy types are shape+color double-coded **unconditionally** (never gated behind this flag) — this flag is reserved for whatever *additional* accessibility treatment a future UI/Juice Layer accessibility pass defines (e.g., stronger outline/contrast reinforcement). Save & Persistence stores the bit; it does not define its visual meaning. |

**`LevelRecord` sub-schema** (one entry per completed level, keyed by
`level_id` in the parent `level_records` dictionary):

| Field | Type | Required | Default | Description |
|---|---|---|---|---|
| `best_stars` | int | Yes | `0` | Highest star rating (0–3, per `level-data-format.md`'s star thresholds) ever achieved on this level. Monotonic — never decreases (Formula 2). |
| `best_score` | int | Yes | `0`, `>= 0` | Highest score ever achieved on this level. Monotonic — never decreases (Formula 2). |
| `completion_count` | int | Yes | `0`, `>= 0` | Number of times this level has been **won** (move-limit-exhausted losses and mid-play abandonment never increment this — see §10). |
| `first_completed_utc` | int (unix epoch) or `null` | Yes | `null` | Timestamp of the first-ever win on this level. Set once, immutable thereafter; `null` only if `completion_count == 0`. |
| `last_completed_utc` | int (unix epoch) or `null` | Yes | `null` | Timestamp of the most recent win on this level. Updates on every win, including a replay that scores worse than `best_score`. |

A `level_records` entry only exists once a level has been won at least
once. There is no entry, and no representation at all, for a level that has
been attempted but never completed — per §10, an attempt that doesn't end
in a win produces no save-visible artifact whatsoever.

### 3. Save Triggers

A save is attempted only when the in-memory profile is **dirty** (has
unsaved changes since the last successful write). The following are the
exhaustive set of MVP triggers — nothing outside this list causes a disk
write:

| Trigger | Fires When | Marks Dirty? | Notes |
|---|---|---|---|
| **Level completion (win)** | Immediately after Level Objective & Move-Limit System's win evaluation and Scoring & Star Thresholds' star computation both resolve, via `record_level_completion()` (§11) | Yes | The primary, highest-value trigger — called as early as possible after a win resolves, before the results/star-ceremony screen even animates, to minimize the window described in Edge Cases. |
| **Settings change** | Any `update_setting()` call from a settings screen | Yes | Debounced (`SETTINGS_SAVE_DEBOUNCE_MS`, Tuning Knobs) so rapid toggling coalesces into one write, not one per toggle. |
| **App background/pause** | Godot's application-paused / focus-lost notification | No (checks existing dirty flag) | Defensive flush via `flush_if_dirty()` — a no-op if nothing changed since the last write. Closes the gap between a pending debounced settings save and the player backgrounding before the debounce timer fires. |
| **App quit (graceful)** | Godot's close-request notification, when the platform delivers one | No (checks existing dirty flag) | Best-effort only — a hard OS kill does not guarantee this fires, which is exactly why level-completion and settings-change are the reliability backbone, not this trigger. |

Mid-level board state (candy positions, in-progress move count, live score
before the level ends) is **never** a save trigger — see §10.

### 4. Atomic Write Strategy — A/B Double-Buffer

**Decision: double-buffer A/B slots, not write-temp-then-rename.** Two
independent files (`profile_a.sav`, `profile_b.sav`) each hold a complete,
self-describing copy of the profile (including its own `write_counter` and
`checksum`). Exactly one is written per save; the other is never touched
during that write.

**Why double-buffer over temp-then-rename**: write-temp-then-rename leans
on the filesystem's `rename()` being atomic — true on iOS/Android's native
filesystems, but not a guarantee this document can safely assume holds
identically on Godot's Web export target, whose `user://` is a virtual,
IndexedDB-backed filesystem (§9) without a documented atomic-rename
primitive. Double-buffering makes no such assumption: safety comes from
*never modifying the slot that currently holds the last-known-good state*,
a property that holds regardless of what write primitive the underlying
platform uses.

**Write algorithm**, executed on every triggered save with `dirty == true`:

1. Determine `target_slot` = whichever slot is **not** the current primary
   (Formula 4) — i.e., the older, currently-inactive slot.
2. Increment the in-memory `write_counter` by 1.
3. Serialize the full profile to canonical bytes (fixed field order, §2 and
   Formula 1) with `modified_utc` set to the current time.
4. Compute `checksum` (Formula 1) over those canonical bytes and append it
   to the payload.
5. Write the complete payload to `target_slot`'s file, then close it.
6. On successful close, `target_slot` is now provably the most-recent valid
   slot (highest `write_counter` with a matching `checksum`) — no separate
   "which slot is active" pointer file is needed; Formula 4 derives it from
   the two slots' own contents at load time.

**OS-kill safety**: if the process is killed at any point during step 5,
`target_slot` is left incomplete or absent — it will simply fail load-time
validation (§5) and be treated as invalid. The **other** slot was never
opened for writing during this cycle and remains exactly as valid as it was
before the interrupted write began. The worst-case outcome of a kill at any
point in this algorithm is "this one save attempt didn't happen" — never
"a previously-good save was damaged." The next successful write naturally
targets the previously-bad slot again (Formula 4), self-healing it without
any dedicated repair step.

### 5. Load-Time Validation Rules

Applied independently to each of the two slots at every `load_profile()`
call (app boot only — see §11, disk is read once per app session).

| ID | Rule | Failure Severity |
|---|---|---|
| S1 | `schema_version` is present and is a value this build supports (`{1}`), or is an older value with a defined migration path to the current version (§8). | Blocking |
| S2 | `write_counter` is present and is a non-negative integer. | Blocking |
| S3 | `checksum` field is present. | Blocking — an absent checksum is a structural defect, not a case for the tamper-accept policy (§6), which only applies when a checksum *is* present but doesn't match. |
| S4 | Recomputed FNV-1a checksum (Formula 1) over the payload equals the stored `checksum`. | See §6 — a mismatch alone is not automatically Blocking. |
| S5 | Every `settings.*` field is present (or defaulted per §2) with the correct type. A present field with the wrong type (e.g., `music_enabled: "yes"`) fails this rule. | Blocking |
| S6 | `level_records` is a dictionary; every key is a non-empty String; every value has `best_stars` in `[0, 3]`, `best_score >= 0`, `completion_count >= 0`, and (when both are non-null) `first_completed_utc <= last_completed_utc`. | Blocking |
| S7 | `total_stars == sum(best_stars across all level_records entries)`. | **Advisory** — a mismatch triggers the self-healing correction in Formula 3, not slot rejection. |
| S8 | `created_utc <= modified_utc`. | Blocking — internally impossible under normal operation regardless of checksum status, so this check applies unconditionally, independent of the tamper-accept policy. |

A slot that fails any **Blocking** rule (or fails S4 in combination with
any other rule, per §6) is treated as **invalid** for this load. A slot
that passes S1, S2, S3, S5, S6, and S8 but fails only S4 is **accepted**
under the tamper policy (§6). S7 failures never invalidate a slot on their
own; they trigger a silent, immediate correction (Formula 3).

### 6. Corruption Recovery Ladder

`load_profile()` runs S1–S8 against both slots, then follows this ladder —
**never a silent full reset** without the explicit notice at the bottom
rung:

1. **Primary**: if at least one slot is valid (or accepted-with-flag per
   the tamper policy below), the valid slot with the higher `write_counter`
   (Formula 4) is loaded as the active profile. If only one slot is valid,
   that one is loaded — no notice is shown; this is the expected, common
   case whenever the other slot is simply one generation behind (its
   normal, healthy state under the A/B scheme, §4) or was corrupted by a
   past interrupted write and hasn't been the target of a fresh write yet.
2. **Backup self-heal**: no dedicated repair action is needed. Whichever
   slot was invalid stays invalid in memory only until the *next* save
   (Formula 4's rotation always targets the non-primary slot), at which
   point it is overwritten with fresh, valid data automatically.
3. **Fresh profile with explicit notice**: if **both** slots are invalid
   (fail S1–S8, excluding the tamper-accepted case), a brand-new default
   profile is created (`schema_version: 1`, empty `level_records`, default
   `settings`, `total_stars: 0`) **and** a `profile_recovery_notice_needed`
   flag is set on the loaded profile for a consuming UI system (Game
   UI/Screens Flow) to surface an explicit, honest message to the player —
   never a silent reset the player has no way to notice or explain.
4. **First launch is not corruption**: if both slots are simply **absent**
   (no file at either path — the expected state for a player who has never
   played before, or who intentionally cleared app data at the OS level),
   a fresh default profile is created **without** setting the recovery
   notice flag. This system cannot distinguish "never existed" from
   "existed and both copies were silently evicted" (relevant on Web, §9),
   so it deliberately treats total absence as the benign case rather than
   alarming every genuine first-time player.

### 7. Integrity & Tamper Policy

**Decision: detect + accept with an internal flag, never reject.** Sweet
Cascade is single-player at MVP with no backend, no leaderboard, and an
explicit Anti-Pillar of "NOT pay-to-win" (`game-concept.md`) — a player who
hand-edits their own local save file (e.g., via a third-party save editor
to set `best_stars: 3` everywhere) harms nobody but themselves, and there is
no competitive integrity to protect yet. Rejecting or reverting such an
edit would be paranoia in service of nothing, and would actively work
against Player Fantasy guarantee #2 ("stars are permanent trophies") if the
game ever second-guessed a player's own claimed history.

The checksum's real job is **corruption detection** (bit flips, truncated
writes, an interrupted save that still happened to leave a parseable file)
— not tamper-proofing. The two failure modes are disambiguated precisely
because they look different in practice: a deliberate hand-edit almost
always produces a well-formed, in-range file (someone editing values with a
save editor changes numbers, not JSON structure); a corrupted write
routinely does not.

| Condition | Classification | Outcome |
|---|---|---|
| Checksum mismatch (S4 fails), but S1, S2, S3, S5, S6, S8 all pass | Suspected manual edit | **Accepted.** Loaded normally; an internal `integrity_flag = true` is set on the in-memory profile (not surfaced to the player at MVP); the very next successful save re-stamps a fresh, correct checksum over the current (possibly edited) data, so the flag only ever reflects "since the last load," not a permanent scarlet letter. |
| Checksum mismatch (S4 fails) **and** any other Blocking rule also fails | Genuine corruption | **Rejected** (invalid slot) — falls to the next rung of the ladder (§6). |
| Checksum matches (S4 passes) | Trusted | Loaded normally, no flag. |

This policy is explicitly scoped to MVP's single-player, no-backend
reality. **Deferred**: if/when Social Layer (Phase 3) ships leaderboards or
friend-score comparison, a locally-edited `best_score` becomes able to harm
someone else's experience (an inflated leaderboard entry), at which point
this policy needs `security-engineer` review and likely flips toward
server-side verification for anything leaderboard-visible — this document
does not pre-design that; it only leaves the `integrity_flag` signal
already sitting in the data model for that future work to consume (see
Open Questions).

### 8. Versioning & Migration Rules

Same philosophy as `level-data-format.md` §3, applied to the save schema
instead of the level schema:

| Change Type | Bump Required? | Reader Behavior |
|---|---|---|
| New **optional field** added at any level (top-level, `settings`, or `LevelRecord`), with a documented default | No | A file missing the field gets the default; a reader that doesn't yet recognize a newer optional field ignores it and logs a warning, never fails to load over it. |
| Existing field **removed, renamed, or its type/semantic meaning changed** | **Yes** | Not backward compatible — requires an ordered migration function for every profile still on the old version. |
| New **required field with no safe default** added | **Yes** | Same as above — a default cannot always be safely assumed. |

`schema_version` is a single incrementing integer, never reused. When a
loaded slot's `schema_version` is older than `CURRENT_SCHEMA_VERSION`
(currently `1`, so no migrations exist yet), an ordered pipeline of pure
migration functions (`migrate_v1_to_v2()`, `migrate_v2_to_v3()`, …) runs in
strict ascending order at load time, each taking the previous version's
payload and returning the next version's payload with `schema_version`
incremented. The fully-migrated result is immediately persisted via the
normal atomic write algorithm (§4), so migration cost is paid at most once
per profile per device — never repeated on every subsequent load. A slot
whose `schema_version` is **newer** than anything this build supports (a
downgrade scenario, or an impossible/corrupted value) fails S1 outright and
is treated as invalid, per Level Data Format's precedent of never
partially loading an unrecognized future version.

**Anticipated additive extensions** (no bump needed, by the rule above):
Booster Brewing Meta's inventory/recipe-unlock state (a new optional
top-level field, e.g. `brewing_inventory`), Events/Theming Engine's
event-progress state (e.g. `event_progress`), and Social Layer's
account-link metadata are all expected to arrive as new optional top-level
fields with safe empty defaults — exactly the kind of change this schema
is built to absorb without a version bump. A bump would only be needed if
one of those systems required changing the *meaning* of an existing field
(e.g., `best_score` needing to become mode-scoped) rather than adding a new
one.

### 9. Web Build Storage Policy (Degraded Guarantee)

Godot 4.6's Web export backs `user://` with a virtual, persistent
filesystem layered over the browser's IndexedDB storage. Two risks exist
there that do not exist on native iOS/Android app-sandboxed storage:
**quota** limits and **eviction** (browsers may reclaim origin storage
under storage pressure, particularly for infrequently-visited sites; iOS
Safari is the strictest, historically capping script-writable storage
retention for sites without a recent visit).

- **Quota is not the practical risk.** Per Formula 5, a full 120-level
  profile's on-disk footprint (both A/B slots combined) is on the order of
  tens of kilobytes — negligible against any browser's minimum guaranteed
  storage quota (commonly megabytes at minimum). This system will never hit
  a quota ceiling at Sweet Cascade's content scale.
- **Eviction is the real, unmitigated risk**, and it is explicitly
  **accepted, not solved, at MVP.** Because Web is the game's secondary
  platform (`game-concept.md`: "Mobile-first (iOS/Android), Web as
  secondary reach platform") and there is no backend at MVP, this document
  does not implement a Web-specific workaround (e.g., prompting for
  persistent-storage permission, cookie-based redundancy, or similar). The
  eventual real fix is Phase 3 cloud sync, not local-storage engineering
  effort spent now on a secondary platform.
- **The save/load contract itself does not fork by platform.** The exact
  same schema, atomic A/B algorithm, and corruption ladder run unmodified
  on Web — the risk lives one layer below this system, in the browser's
  storage guarantees, not in this system's own logic. This keeps the
  "generic save/load contract" honestly generic: one implementation, no
  platform-specific save code paths to maintain or diverge.
- **Eviction is indistinguishable from first launch.** When a browser
  evicts both A/B slots, this system sees exactly what it would see for a
  genuine new player: two absent files. Per §6 rule 4, that produces a
  fresh profile with no recovery notice — a returning Web player who was
  evicted will silently appear to be starting over, with no special
  messaging, because this system has no way to tell the two cases apart.
  This is a known, accepted MVP trade-off, not an oversight.

### 10. No Mid-Level Resume (Explicit MVP Call)

**MVP has no mid-level board-state save.** A level that is abandoned before
it is won — the app is backgrounded and killed by the OS, the process
crashes, or the player quits to the menu without finishing — counts as
**not completed**. No `LevelRecord` is written or touched for that attempt;
re-entering the level starts a completely fresh attempt (a new RNG session
with an incremented `attempt_number`, per `rng-service.md`'s own identical
call in its Edge Cases table: "match-3 games typically don't resume
mid-cascade after a hard process kill").

This is a deliberate MVP scope boundary, justified on three grounds:

1. **Determinism cost.** Resuming a live board mid-cascade would require
   either (a) serializing the Board Engine's full live simulation state
   (grid contents, in-flight cascade step, exact RNG stream position,
   special-candy states) — a large, fragile surface that would couple this
   Foundation-layer, zero-dependency system tightly to Board Engine's
   internal representation, which doesn't exist yet and is expected to
   evolve — or (b) accepting an approximate, non-bit-exact resume, which
   would undermine RNG Service's provable-fairness guarantee (a resumed
   board that can't be reconstructed exactly is not honestly the same board
   the player was playing).
2. **Casual session-length economics.** `game-concept.md`'s Core Loop
   describes 5-15 minute sessions across 2-5 levels; a single level's
   `move_limit` (reference: 25 moves, `level-data-format.md`) resolves in a
   few minutes. Losing at most one in-progress attempt's worth of moves to
   an interruption is a low, easily-recovered cost for this genre and
   audience — not a peer to losing an earned star.
3. **Scope discipline.** MVP targets a 6-8 week build (`game-concept.md`).
   Mid-level resume is nontrivial engineering investment for a comparatively
   rare event (an OS kill mid-level, versus the much more common
   play-to-completion or deliberate quit-to-menu, both of which already
   cost nothing under this rule).

**Logged as a Phase 2 revisit point** (Open Questions): once Board Engine's
actual internal state representation exists and its real serialization cost
can be estimated (impossible to evaluate honestly before that document is
written), this call should be re-examined — not because the reasoning above
is expected to be wrong, but because "no mid-level resume" was made without
that system yet existing to consult.

### 11. Save/Load API Surface (Design-Level, Engine-Agnostic)

Signatures are described at the design level — parameter names and types,
not GDScript. The exact class/singleton/autoload shape is
`godot-gdscript-specialist` implementation territory.

| Operation | Signature | Returns | Description |
|---|---|---|---|
| `load_profile` | `()` | `Profile` | Called once at app boot. Runs the full load algorithm (§5–§6). Returns either a validated existing profile or a freshly-defaulted one. Disk is read exactly once per app session — every other operation below reads/writes the in-memory copy. |
| `get_profile` | `()` | `Profile` | Read-only accessor to the current in-memory profile for any consuming system (World Map, Screen Flow, etc.). Never touches disk. |
| `record_level_completion` | `(level_id: String, stars_earned: int, score_earned: int)` | — | Merges a win's result into `level_records` via the Level Record Merge (Formula 2), updates `total_stars`, marks the profile dirty, and triggers an immediate save. The **only** entry point that ever creates or updates a `LevelRecord` — callers (expected: Level Objective & Move-Limit System, orchestrated by Game UI/Screens Flow) call this only on an actual win, never on a loss or abandonment (§10). |
| `update_setting` | `(setting_key: String, value: Variant)` | — | Updates one `settings` field, marks the profile dirty, triggers a save (subject to the debounce, §3/Tuning Knobs). |
| `flush_if_dirty` | `()` | `bool` | Called from app background/pause/quit notifications (§3). Performs the atomic A/B write only if `dirty == true`; returns whether a write occurred. |
| `get_total_stars` | `()` | `int` | Returns the current in-memory `total_stars` (already maintained live, not a live recompute-on-call — see Formula 3). |

**Reciprocal note** (per `design/CLAUDE.md`'s bidirectionality rule): when
`level-objectives.md`, `screen-flow.md`, and `world-map.md` are authored,
each must list this document in its own Dependencies section and specify
which of the operations above it calls and when.

### 12. Out of Scope for v1 (Deferred)

| Deferred Feature | Why Deferred | Future Owner / Extension Point |
|---|---|---|
| **Mid-level board-state resume** | Explicit MVP call, §10 — determinism cost, casual session economics, scope discipline. | Phase 2 revisit, once Board Engine's internal state shape is known (see Open Questions). |
| **Cloud sync / accounts** | No backend exists at MVP (`game-concept.md`); requires an inferred Backend & Accounts Service that is explicitly non-GDD, technical-director/ADR territory (`systems-index.md` #15). | Phase 3. This document's schema is deliberately shaped (§8's additive-field philosophy; Formula 2's monotonic merge reused for conflict resolution) so a sync layer can wrap it without a local migration. |
| **Booster Brewing Meta inventory & recipe unlocks** | Booster Brewing Meta (#12) is Phase 2, gated on a founder-approved friction prototype; its exact data needs are unknown until that prototype concludes. | `booster-brewing.md` extends this schema additively (e.g. `brewing_inventory` top-level field) — no bump required per §8. |
| **Event progress** | Events/Theming Engine (#13) is Phase 3, explicitly deferred. | `events-theming.md` extends this schema additively (e.g. `event_progress` top-level field). |
| **Social/leaderboard data, friend lists** | Social Layer (#14) is Phase 3, depends on the deferred Backend & Accounts Service. | `social-layer.md`; also revisits the tamper-accept policy (§6/§7 Open Questions) once leaderboard integrity matters. |
| **Monetization state (currency, lives, purchases)** | `game-concept.md`: "Monetization... explicitly deferred until core loop is validated." Not yet designed anywhere. | Whichever future economy GDD defines lives/currency — additive top-level field(s), same extension philosophy. |

---

## Formulas

### Formula 1 — FNV-1a 32-bit Checksum

**Named expression:**
```
hash = OFFSET_BASIS
for each byte b in payload_bytes:
    hash = hash XOR b
    hash = (hash * FNV_PRIME) mod 2^32
checksum = hash
```

| Symbol | Type | Range | Description |
|--------|------|-------|--------------|
| `payload_bytes` | byte array | length ≥ 0 | The canonical UTF-8 serialization of every `Profile` field **except** `checksum` itself, in the fixed field order given in Detailed Rules §2 (top-level fields in table order, `settings` fields in table order, `level_records` entries sorted ascending by `level_id`, each entry's fields in table order), with no extraneous whitespace. Fixed ordering is required because JSON object key order is not guaranteed by the format itself — without it, two serializations of identical data could produce different checksums. |
| `b` | int | 0 – 255 | One byte of `payload_bytes`, processed left to right. |
| `OFFSET_BASIS` | int (constant) | fixed = 2,166,136,261 | Standard FNV-1a 32-bit offset basis (`0x811C9DC5`). |
| `FNV_PRIME` | int (constant) | fixed = 16,777,619 | Standard FNV-1a 32-bit prime (`0x01000193`). |
| `hash` | int (uint32) | 0 – 4,294,967,295 | Running accumulator, reduced mod 2^32 after every byte. |
| `checksum` | int (uint32) | 0 – 4,294,967,295 | Final output, stored in the `checksum` field. |

**Output range**: always a full 32-bit unsigned integer, `[0, 2^32 − 1]` —
not clamped further, since it is a fixed-width hash, not a magnitude with a
gameplay meaning.

**Worked example**: FNV-1a's per-byte loop body, applied to a single-byte
payload — the ASCII byte for `'a'` (decimal `97`, `0x61`) — because this
exact input/output pair is a well-known, independently verifiable public
FNV-1a 32-bit test vector, letting anyone confirm this document's constants
and loop direction are correct without needing to trust extended
multi-byte hand arithmetic:

```
hash = OFFSET_BASIS XOR 97
     = 2,166,136,261 XOR 97
     = 2,166,136,228

hash = (2,166,136,228 * 16,777,619) mod 2^32
     = 3,826,002,220

checksum = 3,826,002,220   (0xE40C292C)
```

This matches the published FNV-1a-32 test vector for the single-byte input
`"a"`. A real profile payload is hundreds of bytes; the loop above is
applied byte-by-byte, in canonical field order, by the implementation — the
single-byte example fully specifies the deterministic mechanism (the
XOR-then-multiply loop body) without hand-verifying a full-payload
computation digit by digit, following the same "fully specify the
deterministic contract, leave large-scale computation to
implementation/tooling" precedent `rng-service.md` uses for its `mix32`
finalizer (F1 §Formulas).

---

### Formula 2 — Level Record Merge (Monotonic Merge)

**Named expression:**
```
best_stars'          = max(existing.best_stars, stars_earned)
best_score'           = max(existing.best_score, score_earned)
completion_count'     = existing.completion_count + 1
first_completed_utc'  = existing.first_completed_utc if existing.first_completed_utc != null else now_utc
last_completed_utc'   = now_utc
```

| Symbol | Type | Range | Description |
|--------|------|-------|--------------|
| `existing` | `LevelRecord` or absent | — | The level's current record before this win, or an all-zero/null record if this is the level's first-ever completion. |
| `stars_earned` | int | 0 – 3 | Stars awarded for this specific win, supplied by `record_level_completion()`'s caller (Scoring & Star Thresholds, via Level Objective & Move-Limit System). |
| `score_earned` | int | ≥ 0 | Score achieved on this specific win. |
| `now_utc` | int (unix epoch) | current time | The moment this merge runs. |
| `best_stars'`, `best_score'`, `completion_count'`, `first_completed_utc'`, `last_completed_utc'` | — | — | The record's updated field values after the merge, replacing `existing`'s. |

**Output range**: `best_stars'` and `best_score'` are non-decreasing across
any sequence of calls for the same `level_id` — this formula is only ever
invoked on a win (§10), so `stars_earned >= 1` always (guaranteed by
`level-data-format.md`'s V17: completing a level's win condition always
awards at least 1 star). `completion_count'` strictly increases by exactly
1 per call, unbounded above.

**Worked example**: a player has already three-starred a level
(`existing = {best_stars: 3, best_score: 4100, completion_count: 6,
first_completed_utc: 1751000000, last_completed_utc: 1752000000}`) and
replays it, earning only 1 star this time (`stars_earned = 1, score_earned
= 2600`, `now_utc = 1753000000`):

```
best_stars'         = max(3, 1) = 3            (unchanged — trophy preserved)
best_score'          = max(4100, 2600) = 4100   (unchanged)
completion_count'    = 6 + 1 = 7                (every win counts, regardless of stars)
first_completed_utc' = 1751000000               (unchanged — set once)
last_completed_utc'  = 1753000000               (always updates)
```

This is also the exact merge rule a future cloud-sync layer can reuse
unmodified to reconcile two devices' divergent play histories for the same
`level_id` (see Detailed Rules §12 and Open Questions) — it is commutative
and idempotent (merging the same result twice, or in either order, produces
the same outcome), which is precisely the property a conflict-free merge
needs.

---

### Formula 3 — Total Stars Consistency (Self-Healing Aggregate)

**Named expression:**
```
true_total = sum(record.best_stars for record in level_records.values())

on write:  total_stars = true_total                          (always recomputed, never caller-supplied)
on load:   if stored_total_stars != true_total:
               total_stars = true_total                       (corrected silently, not treated as corruption)
           else:
               total_stars = stored_total_stars
```

| Symbol | Type | Range | Description |
|--------|------|-------|--------------|
| `level_records` | dictionary | 0 – 120+ entries | The profile's per-level records. |
| `true_total` | int | 0 – (3 × level count) | The sum of `best_stars` across every entry — the single source of truth. |
| `stored_total_stars` | int | ≥ 0 | The `total_stars` value found in a loaded slot's payload. |
| `total_stars` | int | 0 – (3 × level count) | The value actually held in memory and exposed via `get_total_stars()`. |

**Output range**: bounded below by 0, bounded above by `3 × (number of
level_records entries)` — at full 120-level launch scope, `[0, 360]`.

**Design rationale (stored vs. computed)**: `total_stars` is **stored**
(denormalized), not recomputed on every read, because World Map's
star-gated unlock checks and Screen Flow's profile-header display both want
a cheap O(1) read rather than walking the `level_records` dictionary on
every access. But it is never *trusted blindly* — it is always
recomputed by the save logic itself at write time (never accepted as a
caller-supplied value) and re-validated against the true sum at every load,
correcting silently if it drifts. This gets the read-side convenience of a
stored aggregate while keeping a single source of truth (`best_stars`) that
a desync always self-heals back toward, rather than escalating a benign
bookkeeping drift into the corruption ladder (§6).

**Worked example**: a profile has three completed levels with `best_stars`
values `3, 2, 3`. `true_total = 3 + 2 + 3 = 8`. If a loaded slot's stored
`total_stars` field reads `7` (e.g., from a save-write that happened before
a since-added fourth star was merged in, or any other bookkeeping drift),
the load step silently corrects it to `8` — no notice, no ladder fallback,
because S6/S8 (structural/range validity) and, separately, the checksum
(S4) are the actual integrity gates; S7 alone is advisory by design.

---

### Formula 4 — A/B Slot Selection & Rotation

**Named expression:**
```
primary_slot   = argmax over {A, B} where is_valid(slot) of write_counter(slot)
                 (tie-break, unreachable in normal operation: prefer A)
next_target    = B if primary_slot == A else A
```

| Symbol | Type | Range | Description |
|--------|------|-------|--------------|
| `is_valid(slot)` | bool | {true, false} | Result of S1–S8 (with the §6/§7 tamper-acceptance carve-out) for that slot. |
| `write_counter(slot)` | int | ≥ 0 | The slot's own stored `write_counter`. |
| `primary_slot` | enum | {A, B, none} | The slot loaded as the active profile this session; `none` if neither is valid (§6 rung 3/4). |
| `next_target` | enum | {A, B} | Which slot the *next* save will write to — always the one **not** currently primary. |

**Output range**: a binary choice between exactly two slots; no other
values are possible. This formula is what makes backup rotation "free" —
there is no separate rotation algorithm or explicit backup-copy step. Every
successful write alternates which slot is most-recent by construction, so
the non-primary slot is always, automatically, exactly one generation
behind the primary.

**Worked example**: at load, `write_counter(A) = 42`, `write_counter(B) =
41`, both valid → `primary_slot = A` (higher counter). The next save's
`next_target = B` (the non-primary slot). That write increments the
in-memory counter to `43` and writes it into slot B. On the *following*
load, `write_counter(A) = 42`, `write_counter(B) = 43` → `primary_slot`
flips to `B`, and the subsequent `next_target` becomes `A` again. The two
slots simply trade the "most recent" role back and forth forever; slot A
is never touched by a write while slot B is being written, and vice versa.

---

### Formula 5 — Profile Storage Size Bound

**Named expression:**
```
profile_size_bytes ≈ FIXED_OVERHEAD_BYTES + (level_count × PER_LEVEL_RECORD_BYTES)
total_on_disk_bytes ≈ 2 × profile_size_bytes                     (both A/B slots)
```

| Symbol | Type | Range | Description |
|--------|------|-------|--------------|
| `FIXED_OVERHEAD_BYTES` | int (constant) | provisional, ≈300 | Serialized size of every top-level/`settings` field with `level_records` empty — measured directly from a representative compact-JSON skeleton (§below). |
| `PER_LEVEL_RECORD_BYTES` | int (constant) | provisional, ≈150 | Serialized size of one `level_records` entry (key + `LevelRecord` object), measured from a representative entry using a real MVP-format `level_id` (`level-data-format.md`'s `<region_code>-<3-digit-sequence>` convention). |
| `level_count` | int | 0 – 120 (launch scope) | Number of entries in `level_records` — at most, one per level ever won. |
| `profile_size_bytes` | int | unbounded, scales linearly with `level_count` | Estimated serialized size of one slot's payload, excluding the small fixed checksum overhead. |
| `total_on_disk_bytes` | int | unbounded, scales linearly with `level_count` | Combined footprint of both A/B slots. |

**Output range**: unbounded above (grows linearly with `level_count`), but
not a runtime-enforced cap — this is a sanity-check estimate, not a hard
ceiling this system clamps against.

**Worked example** (full 120-level launch scope, per `game-concept.md`'s
"Launch target: 4 regions × 30 levels (120 levels)"): measuring a
representative compact-JSON skeleton with empty `level_records` gives
`FIXED_OVERHEAD_BYTES ≈ 293`, rounded to **300**. Measuring one
representative entry (`"candy_kingdom_hub-001":{"best_stars":3,
"best_score":3850,"completion_count":7,"first_completed_utc":1751000000,
"last_completed_utc":1752900000},`) gives `PER_LEVEL_RECORD_BYTES ≈ 146`,
rounded up to **150** (same rounding-up convention `level-data-format.md`
uses for its own provisional per-unit constant).

```
profile_size_bytes  = 300 + (120 × 150) = 300 + 18,000 = 18,300 bytes ≈ 17.9 KB
total_on_disk_bytes = 2 × 18,300 = 36,600 bytes ≈ 35.8 KB
```

At full launch scope, the entire save system's on-disk footprint is
**under 40 KB** — negligible against the ≤400MB memory ceiling
(`technical-preferences.md`) and, per Detailed Rules §9, several orders of
magnitude under any browser's minimum guaranteed storage quota on Web.
Storage *size* is never the risk this system needs to design around;
storage *eviction* (Web only) is the one real, accepted risk.

---

---

## Edge Cases

| Scenario | Expected Behavior | Rationale |
|----------|--------------------|-----------|
| First launch — both slots absent (no files at either path) | Fresh default profile created; **no** `profile_recovery_notice_needed` flag set | This system cannot distinguish "never played" from "both copies were evicted"; treating absence as benign avoids alarming every genuine new player (§6 rule 4). |
| Both slots fail validation (S1–S8, excluding tamper-accepted case) | Fresh default profile created; `profile_recovery_notice_needed` flag **is** set for a consuming UI to surface | The one case that must never be silent — a profile that existed and is now unreadable (§6 rule 3). |
| One slot valid, one slot invalid or absent | The valid slot loads as primary with zero data loss; the invalid slot self-heals automatically on the next save (it becomes the next `next_target`, Formula 4) | No dedicated repair step is needed; the alternating write rule already guarantees eventual repair. |
| Checksum mismatch, but payload otherwise structurally/range valid | Accepted; `integrity_flag = true` set internally (not shown to player at MVP); checksum re-stamped correctly on the next save | Suspected manual edit, not corruption — proportionate to a single-player, no-leaderboard MVP (§7). |
| Checksum mismatch **and** a structural/range rule also fails | Slot rejected as invalid, falls to the next rung of the ladder | A benign hand-edit is very unlikely to also break structural validity; this combination is treated as genuine corruption (§7). |
| Atomic write to `target_slot` is interrupted (OS kill, crash) mid-write | `target_slot` is left incomplete/corrupt and fails S1–S8 on the next load; the untouched other slot remains fully valid and loads as primary with zero loss | The core OS-kill safety property of the A/B scheme (§4) — the active, last-known-good slot is never the one being written. |
| A level is completed (win) but the process is killed in the narrow window between win-evaluation resolving and `record_level_completion()`'s write completing | That single win is lost — the level shows as not-yet-completed on next launch, exactly as if the attempt had been abandoned | An inherent limit of any save system; minimized (not eliminated) by calling `record_level_completion()` immediately on win, before the results/star-ceremony screen even animates (§3). No corruption occurred — indistinguishable from the app being killed one second earlier, before the win resolved. |
| Player replays an already-3-starred level and earns only 1 star this run | `best_stars`/`best_score` remain unchanged (monotonic max, Formula 2); `completion_count` still increments; `last_completed_utc` still updates | Every win counts toward completion history even when it doesn't improve the trophy; the trophy itself is never allowed to regress (Player Fantasy guarantee #2). |
| A level attempt ends in a loss (move limit exhausted without meeting the objective) or is abandoned mid-play (app killed, backgrounded and not returned to, quit to menu) | No `LevelRecord` is created or modified for that attempt; no save write occurs for it at all | Explicit MVP call (§10) — only a win is a save-worthy event. |
| Settings toggled rapidly multiple times within `SETTINGS_SAVE_DEBOUNCE_MS` | Coalesced into a single disk write at the trailing edge of the debounce window (or earlier, if a background/pause trigger fires first) | Avoids write-storming and unnecessary flash wear/`write_counter` churn from a burst of toggles (§3). |
| App is force-quit with a pending debounced settings write not yet flushed | The background/pause trigger (§3), which checks the dirty flag unconditionally rather than waiting on the debounce timer, flushes it first | Closes the gap between "debounce window still open" and "player quits before it elapses." |
| A loaded slot's `schema_version` is newer than anything this build supports (app downgrade, or an impossible/corrupted value) | Fails S1; treated as invalid, falls to the next rung of the ladder | An older build has no safe way to interpret or migrate backward from a schema it has never seen, per Level Data Format's identical precedent. |
| A `level_id` present in `level_records` no longer exists in the current level manifest (a level was removed or renamed in a content update) | The orphaned record is retained as-is (never silently deleted) and still contributes to `total_stars`; any UI that enumerates levels for display simply won't show it, since it iterates the current manifest, not the save file | Data hygiene for a removed level_id is a future World Map concern (whether to prune, migrate, or ignore), not something this Foundation-layer document actively decides now. |
| Web build: browser evicts both storage slots between sessions | Functionally identical to first launch (both slots simply absent) — fresh profile, no notice | This system has no visibility into browser-level eviction; the degraded-guarantee policy (§9) treats this as accepted, not detectable. |
| `write_counter` reaches an extreme value | Stored as a 64-bit integer; even one save per minute for a player's entire lifetime falls many orders of magnitude short of overflowing it | Not a real-world concern at any plausible play volume — flagged only for completeness, not because it is an expected scenario. |

---

---

## Dependencies

Save & Persistence has **zero inbound design-system dependencies** —
confirmed by `systems-index.md`'s Foundation-layer entry ("Depends On: —").
Every input it accepts (`level_id`, `stars_earned`, `score_earned`, a
`setting_key`/`value` pair) is an opaque caller-supplied parameter, never
read from another system's schema directly — the same dependency-avoidance
pattern `rng-service.md` uses (§Dependencies there). The table below lists
systems that depend on *this* one; per `design/CLAUDE.md`'s bidirectionality
rule, each — several not yet written — is expected to list this document in
its own Dependencies section when authored.

| System | Direction | Nature of Dependency |
|--------|-----------|----------------------|
| Level Data Format (`level-data-format.md`, APPROVED) | This reads its key format (data only, not a build dependency) | `level_records` is keyed by `level_id`, using Level Data Format's stable String identifier (`<region_code>-<3-digit-sequence>`) — never `display_number`, per that document's own explicit design intent (§2 there). |
| Game UI/Screens Flow (`screen-flow.md`, not yet authored) | Screen Flow depends on this | Expected to call `load_profile()` at boot, `get_profile()`/`get_total_stars()` for menu/profile-header display, `update_setting()` from settings screens, and to surface `profile_recovery_notice_needed` (§6) as a player-facing message. **Reciprocal note**: its Dependencies section must list this document and the operations it calls. |
| Level Progression / World Map (`world-map.md`, not yet authored) | World Map depends on this | Expected to read `level_records`/`total_stars` (via `get_profile()`/`get_total_stars()`) to drive star-gated region/level unlocks. **Reciprocal note** required at authoring. |
| Level Objective & Move-Limit System (`level-objectives.md`, not yet authored) | Objective System depends on this | Expected to be the system (via Scoring & Star Thresholds' star computation) that triggers `record_level_completion()` on a win. **Reciprocal note** required at authoring. |
| Booster Brewing Meta (`booster-brewing.md`, Phase 2, gated) | Booster Brewing depends on this | Anticipated additive `brewing_inventory` schema extension (Detailed Rules §8/§12). |
| Events/Theming Engine (`events-theming.md`, Phase 3) | Events depends on this | Anticipated additive `event_progress` schema extension. |
| Social Layer (`social-layer.md`, Phase 3) | Social Layer depends on this | Anticipated additive account-link/social metadata; also the consumer that will force a re-examination of the tamper-accept policy (§7) once leaderboard integrity matters. |
| `.claude/docs/technical-preferences.md` (not a `design/gdd/` system) | This depends on it (constant + rule only) | Supplies the ≤400MB memory ceiling referenced in Formula 5's sanity check, and the determinism-testing standard this document's gdUnit4 acceptance criteria follow. |
| `design/art/art-bible.md` (not a `design/gdd/` system) | This depends on it (constant only) | Supplies the accessibility toggle semantics (`reduced_motion_enabled`, `colorblind_assist_enabled`) whose *storage* this document owns but whose visual meaning it does not define. |

---

---

## Tuning Knobs

| Parameter | Current Value | Safe Range | Effect of Increase | Effect of Decrease |
|-----------|--------------|------------|---------------------|----------------------|
| `SETTINGS_SAVE_DEBOUNCE_MS` | 750ms | 200 – 2,000ms | Fewer redundant disk writes/flash wear during rapid toggling, but a larger (still background-trigger-covered, §3) window between a settings change and it being durably saved | Settings persist sooner after each change, at the cost of more frequent writes if the player toggles repeatedly |
| `SLOT_COUNT` (A/B double-buffer width) | 2 | fixed at 2 for MVP | A 3rd slot would add marginal resilience (surviving two consecutive interrupted writes in a row) at a storage cost that is still trivial per Formula 5, but adds write-rotation complexity not justified without evidence of real corruption incidents | Not applicable — 2 is the minimum for the self-healing rotation property (Formula 4) to exist at all |
| `CHECKSUM_ALGORITHM_VERSION` | `"fnv1a-32-v1"` | increment-only tag, never reuse a retired value | N/A — a compatibility tag, not a magnitude, in case the checksum function is ever swapped (mirrors `rng-service.md`'s `ALGORITHM_VERSION` pattern for `mix32`) | N/A — changing the underlying algorithm without incrementing this would silently make previously-computed checksums meaningless |
| Integrity policy (`detect_and_accept` vs `reject`) | `detect_and_accept` (§7) | `{detect_and_accept, reject}` | `reject` would revert any hand-edited slot to the backup instead of accepting it — the correct posture once leaderboard integrity matters (Phase 3, pending `security-engineer` review), but disproportionate and against Player Fantasy guarantee #2 for a solo, no-backend MVP | N/A — `detect_and_accept` is already the most permissive option; there is no "less strict" alternative below it |
| `CURRENT_SCHEMA_VERSION` / supported set | `1` / `{1}` | increment-only, append-only supported set | Each future bump enables newer additive/breaking fields (§8) but requires an ordered migration function to exist for every older version still in the wild | Not applicable — versions are never decremented |

---

---

## Acceptance Criteria

All BLOCKING per `coding-standards.md`'s Logic-tier rule (formulas, state
handling); tests live under `tests/unit/save-persistence/`, deterministic,
no real timers or real OS process kills — interruption is simulated by
mocking the write call to stop partway through.

**Round-trip fidelity & checksum**

- [ ] `test_round_trip_fidelity`: a `Profile` populated with non-default
      settings and multiple `level_records` entries, saved via the atomic
      write algorithm and immediately loaded back, is field-for-field
      identical to the original (excluding `write_counter`, which is
      expected to have incremented by exactly 1).
- [ ] `test_checksum_matches_formula_1_worked_example`: computing the
      checksum over the single-byte payload `[0x61]` (`'a'`) reproduces
      `3826002220`, regression-pinning Formula 1's documented worked
      example.
- [ ] `test_checksum_detects_corruption`: a saved slot's payload bytes are
      mutated (single byte flipped) without recomputing the checksum, in a
      way that also breaks a structural/range rule (S5/S6); `load_profile()`
      classifies the slot as invalid.
- [ ] `test_checksum_mismatch_well_formed_accepted`: a saved slot's
      `best_stars` value is hand-edited to a different in-range value
      without recomputing the checksum; the payload otherwise still passes
      S1, S2, S3, S5, S6, S8; `load_profile()` accepts the slot's data,
      sets `integrity_flag = true`, and does **not** fall back to the other
      slot.

**Corruption recovery ladder**

- [ ] `test_ladder_primary_to_backup`: Slot A (higher `write_counter`) is
      corrupted; Slot B is valid; `load_profile()` returns Slot B's data
      with zero loss relative to Slot B's own last known-good state.
- [ ] `test_ladder_both_invalid_fresh_profile_with_notice`: both slots fail
      validation; `load_profile()` returns a fresh default profile **and**
      `profile_recovery_notice_needed` is set.
- [ ] `test_first_launch_no_notice`: both slots absent; `load_profile()`
      returns a fresh default profile **without**
      `profile_recovery_notice_needed` set.

**Atomicity under simulated interrupt**

- [ ] `test_atomic_write_interrupt_preserves_previous_state`: the write to
      `target_slot` is mocked to stop after a partial byte count; the
      untouched other slot remains fully valid and is still returned as
      primary by the next `load_profile()` call, with zero loss of its
      previously-saved state.
- [ ] `test_slot_alternation`: two consecutive successful saves target
      opposite slots, each incrementing `write_counter` by exactly 1, with
      `primary_slot` flipping accordingly per Formula 4.

**Level record merge & aggregates**

- [ ] `test_level_record_merge_monotonic`: `record_level_completion()` with
      a worse result than an existing record leaves `best_stars`/
      `best_score` unchanged, while `completion_count` increments and
      `last_completed_utc` updates — reproducing Formula 2's worked example
      numbers exactly.
- [ ] `test_total_stars_self_heals`: a loaded profile whose stored
      `total_stars` deliberately does not equal `sum(best_stars)` is
      corrected to the true sum immediately after load, without setting
      `profile_recovery_notice_needed` or invalidating the slot.

**Versioning & migration**

- [ ] `test_migration_v1_fixture_identity`: a hand-authored fixture at
      `tests/fixtures/save-persistence/valid_v1_profile.json`
      (`schema_version: 1`) loads with zero migration steps applied and
      `schema_version` remains `1`.
- [ ] `test_future_schema_version_rejected`: a fixture with
      `schema_version: 999` (outside the supported set `{1}`) fails S1 and
      is treated as invalid on load.

**Save triggers & mid-level scope**

- [ ] `test_mid_level_abandon_not_saved`: a simulated level attempt that
      never calls `record_level_completion()` leaves that `level_id`
      entirely absent from `level_records` after "restart" — no partial or
      zero-star artifact is ever written.
- [ ] `test_settings_debounce_coalesces_writes`: multiple `update_setting()`
      calls within `SETTINGS_SAVE_DEBOUNCE_MS` produce exactly one disk
      write, not one per call.
- [ ] `test_background_flushes_pending_dirty_state`: a dirty, not-yet-
      debounce-elapsed settings change is written to disk when the
      background/pause trigger fires, before the debounce timer would have.

**Storage & data-driven compliance**

- [ ] `test_storage_size_within_bound`: a synthetically populated 120-level
      profile serializes to a size within Formula 5's estimated bound
      (~18 KB per slot / ~36 KB total), confirmed well under any browser's
      minimum guaranteed Web storage quota.
- [ ] No constant this document defines (`SETTINGS_SAVE_DEBOUNCE_MS`,
      `CHECKSUM_ALGORITHM_VERSION`, the supported `schema_version` set,
      FNV-1a constants) exists as a hardcoded literal scattered through
      `src/` — every instance is sourced from a single data-driven config
      location, per `coding-standards.md`.

---

---

## Open Questions

| Question | Owner | Deadline | Resolution |
|----------|-------|----------|-----------|
| Should the "no mid-level resume" call (§10) be revisited once Board Engine's internal simulation-state representation exists and its serialization cost can be estimated? | systems-designer | After `board-engine.md` is authored | — |
| Should the integrity policy (§7, currently `detect_and_accept`) flip to `reject` — or move to server-side verification for leaderboard-visible fields specifically — once Social Layer ships? | security-engineer | Before `social-layer.md` (Phase 3) authoring | — |
| Does compact JSON remain the right save-file encoding once profile size grows substantially in Phase 2/3 (brewing inventory, event progress), or should a binary format be reconsidered for parse-time/size at that larger scale? | technical-director | Phase 2 gate, post-`booster-brewing.md` | — |
| Should `SLOT_COUNT` increase beyond 2 (A/B) if real corruption incidents are observed in production telemetry post-launch? | technical-director | Post-launch, data-driven | — |
| Should orphaned `level_records` entries (a `level_id` removed from a later content update, Edge Cases) ever be actively pruned, and if so by this system or by World Map at content-update time? | game-designer | At `world-map.md` (#11) authoring | — |
| Should Godot's exact `user://` write primitive (FileAccess flush/sync behavior across iOS/Android/Web export targets in 4.6) be confirmed to actually guarantee "either the write fully lands or the file stays exactly as it was," or does it need an explicit fsync-equivalent call to hold that guarantee? | technical-director / godot-specialist | Before implementation begins | — |
