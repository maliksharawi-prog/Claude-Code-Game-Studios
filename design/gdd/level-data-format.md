# Level Data Format

*Status: Reviewed — APPROVED (design-review lean, 2026-07-18) — see `design/gdd/reviews/level-data-format-review-log.md`*
*Cross-GDD sync, 2026-07-18: added Dependencies rows for Touch & Input
System and Game UI/Screens Flow; recorded that `scoring-stars.md` Formula 4
supersedes `REFERENCE_SCORE_PER_MOVE` with a `color_pool`-size-keyed table
(raised the safe-range ceiling to 260 so K=3's 255 fits); added an Open
Questions row on saw-tooth pacing cross-level validation.*

> **Author**: systems-designer
> **Last Updated**: 2026-07-17
> **Last Verified**: 2026-07-17
> **Implements Pillar**: Pillar 2 — Clever, Never Cheated (primary: every difficulty lever is authored, inspectable data, never hidden runtime rigging); supports Pillar 1 — Every Swap Sparkles (board shape and color-pool size are the levers that keep matches legible)
> **Quick reference** — Layer: `Foundation` · Priority: `MVP` · Key deps: `None (soft co-design dependency — see Dependencies)`

---

## Overview

Level Data Format is the versioned data schema that defines every playable
level in Sweet Cascade — board geometry, active candy palette, pre-placed
pieces, objectives, move limit, and star thresholds — as a hand-authored,
data-driven resource decoupled from the Match-3 Board Engine's and Level
Objective System's GDScript logic. Per `game-concept.md`'s Technical
Considerations ("levels are hand-designed data files, not procedural"),
every level in Sweet Cascade is a discrete file a designer edits directly;
this document defines exactly what fields that file may contain, what makes
it valid, and how it may grow over time. This is schema **v1**: it covers
only what the Match-3 Board Engine and a first pair of objective types need
today. It is explicitly a living contract, not a final specification — see
`systems-index.md`'s Circular Dependencies note — and will extend once
Special Candies & Combo Matrix (#5) and Level Objective & Move-Limit System
(#7) reveal their full data needs.

---

## Player Fantasy

This is a data-format document, but its job is entirely in service of a
player-facing feeling: **every level feels like someone made it for you.**
Concretely, the schema's design choices exist to protect three player-facing
outcomes:

- **Hand-crafted, never generic.** The `cell_mask` field lets a designer
  carve a board into a shape that means something (a heart, a bottle, a
  diamond) instead of every level being an identical rectangle. Players
  should notice board shape as a deliberate creative choice, the same way
  they'd notice a hand-drawn level in a platformer.
- **Fair, never rigged.** Per Pillar 2, difficulty in Sweet Cascade comes
  from visible, inspectable choices — move limit, color pool size,
  objective target — never from runtime board manipulation the player can't
  see. Because every one of those levers lives in a single flat, versioned
  data file, a designer (and eventually a player, via consistent behavior)
  can reason about *why* a level is hard by looking at its numbers, not by
  suspecting the game of cheating.
- **Readable at a glance.** The `color_pool` field is the single biggest
  lever over how "busy" a board reads on a small phone screen — a 3-color
  level is calm and fast, a 5-color level is dense and strategic. Making
  this an explicit per-level authored choice (not a global constant) lets
  designers pace difficulty and visual density level-by-level, in service
  of the onboarding curve described in `game-concept.md` (Levels 1-5 teach
  one concept at a time).

---

## Detailed Rules

### 1. File Format & Location

- Levels are stored one-file-per-level under `assets/data/levels/`, one
  subdirectory per region:
  `assets/data/levels/<region_code>/<level_id>.tres`
- **v1 launch scope** uses a single placeholder region directory,
  `candy_kingdom_hub`, matching Region 1 as named in `design/art/art-bible.md`.
  Additional region directories are added as Level Progression / World Map
  (#11) formalizes the region roster toward the 4-region, 120-level launch
  scope in `game-concept.md`.
- Concretely, each level is represented as a Godot `Resource` (`.tres`
  text-resource file) backed by a custom `LevelData` Resource script — one
  typed `@export` field per schema field below. This is a design-level,
  engine-agnostic schema; the exact `LevelData.gd` implementation is
  `godot-gdscript-specialist` territory, but every field name, type, and
  default below is a load-bearing part of the contract that implementation
  must honor exactly.
- Using a native `Resource` (rather than hand-rolled JSON) is a deliberate
  "Godot-Resource-friendly" choice: it gets designers a free, native
  Inspector-panel editing UI for every level with zero custom tooling
  investment in v1 (see Authoring Workflow below).

### 2. Schema v1 Field Reference

| Field | Type | Required | Default | Description |
|---|---|---|---|---|
| `schema_version` | int | Yes | — | Schema contract version this file was authored against. All v1 files use `1`. |
| `level_id` | String | Yes | — | Globally unique, stable identifier. Format: `<region_code>-<3-digit-sequence>` (e.g. `candy_kingdom_hub-001`). Never reused, even if a level is later removed. Save & Persistence keys unlock/star state off this field, not `display_number`. |
| `region` | String | Yes | — | Region slug this level belongs to. MVP/launch uses `candy_kingdom_hub`. Formal region registry ownership moves to Level Progression / World Map (#11) once that GDD is written. |
| `display_number` | int | Yes | — | Player-facing level number (1–120 at launch scope). Kept independent of `level_id`/`region` ordering specifically so regions can be reordered on the map without invalidating save data keyed by `level_id`. |
| `grid_width` | int | Yes | — | Board width in cells. Range **3–9**. |
| `grid_height` | int | Yes | — | Board height in cells. Range **3–9**. |
| `cell_mask` | Array[String] | No | `grid_height` rows of `grid_width` `"1"` characters (full rectangle) | Row-major list of row strings. Each character is `1` (playable cell) or `0` (void — a non-rectangular cutout). Row count must equal `grid_height`; each row's character count must equal `grid_width`. |
| `pre_placed_pieces` | Array[Dictionary] | No | `[]` (fully RNG-generated start) | Each entry: `{row: int, col: int, candy_type: String}`. Seeds one regular candy at one cell at level start; the piece is a normal, swappable, matchable tile from move 1 — not locked or immovable. |
| `color_pool` | Array[String] | Yes | — | 3–5 unique `candy_type` identifiers drawn from the canonical five defined in `design/art/art-bible.md`'s Base Candy Roster: `strawberry`, `citrus`, `lemon`, `apple`, `grape`. Defines which colors the Board Engine's RNG Service may spawn on this level. |
| `move_limit` | int | Yes | — | Number of swaps the player has to satisfy every objective. Minimum `1`. |
| `objectives` | Array[Dictionary] | Yes | — | Minimum length 1. All objectives in the list must be satisfied (logical AND) before `move_limit` is exhausted to win. See Objective Types below. Order is preserved and may be used by Game UI/Screens Flow to decide primary-badge display order. |
| `star_1_score` | int | Yes | — | Score required to earn 1 star at level completion. |
| `star_2_score` | int | Yes | — | Score required to earn 2 stars. Must exceed `star_1_score`. |
| `star_3_score` | int | Yes | — | Score required to earn 3 stars. Must exceed `star_2_score`. |
| `rng_seed` | int | No | `-1` | `-1` = no fixed seed; RNG Service assigns a fresh seed per attempt (every MVP/launch level). Any value `>= 0` fixes the level's entire RNG stream (initial board **and** every subsequent refill/cascade spawn) for reproducible play — reserved for Phase 3 daily-challenge/event levels; unused at MVP/launch. |

**Objective Types (v1 — closed enum, exactly two supported values):**

| `type` value | Params | Win Condition |
|---|---|---|
| `score_target` | `{target: int}` — `target > 0` | Live score `>= target` before `move_limit` is exhausted. |
| `collect_color` | `{color: String, count: int}` — `color` must be a member of this level's `color_pool`; `count > 0` | Cumulative tiles of `color` cleared (via match or special) `>= count` before `move_limit` is exhausted. |

`collect_color` is intentionally scoped to "count cleared tiles of a color"
only — it does **not** model the Booster Brewing Meta's ingredient-harvest
yield (Phase 2, gated on founder-approved friction prototype), even though
both key off the same underlying match events. Reconciling this terminology
(does a brewing ingredient yield ever differ from a raw tile-color count?)
is an open item for Level Objective & Move-Limit System (#7) — see Open
Questions.

### 3. Versioning & Migration Rules

`schema_version` is a single incrementing integer, not semver. The policy
distinguishes two kinds of schema change, because they have different
safety properties for an old reader encountering a new file:

| Change Type | Bump Required? | Reader Behavior |
|---|---|---|
| New **optional field** added, with a documented default | No | A file missing the field gets the default. A reader that doesn't yet recognize a newer optional field ignores it and logs a warning — it never fails to load over an unrecognized *field*. |
| New **value added to a closed enum** (`candy_type` in `color_pool`, `objectives[].type`, and any future closed vocab such as a blocker-type enum) | **Yes** | Closed-enum values are not safe to silently ignore — an unrecognized objective type could make a level uncompletable if skipped. A reader must explicitly support the new `schema_version` to accept a file using the new enum value; older readers reject the whole file (loudly — see Edge Cases), never partially load it. |
| Existing field **removed or renamed** | **Yes** | Not backward compatible. Requires a one-time migration pass over every existing `.tres` file before the new version ships. |
| Existing field's **type or semantic meaning changed** (e.g., `move_limit` changing from swap-count to a time-based value) | **Yes** | Not backward compatible; same migration requirement as above. |
| New **required field** added | **Yes** | A default cannot always be safely assumed for a field that didn't previously exist; treat as a breaking change and migrate. |

When a bump occurs, `schema_version` values are never reused, and a
migration script upgrades every existing level file in place before the
new reader ships. v1's currently supported version set is exactly `{1}`.

### 4. Validation Contract

A level file is **invalid** — and must not be loadable into the game — if
it violates any rule marked **Blocking**. Rules marked **Advisory** are
logged as warnings but do not prevent a level from loading in v1 (see
rationale on V18).

| ID | Rule | Severity |
|---|---|---|
| V1 | `schema_version` is present and is a value the running engine build supports (v1 build supports exactly `{1}`). | Blocking |
| V2 | `level_id` is a non-empty string, unique across every file under `assets/data/levels/`. | Blocking |
| V3 | `region` is a non-empty string. | Blocking |
| V4 | `display_number` is a positive integer, unique across the full launch level set. | Blocking |
| V5 | `grid_width` and `grid_height` are each integers in `[3, 9]`. | Blocking |
| V6 | If `cell_mask` is present, its row count equals `grid_height` and every row's character count equals `grid_width`; every character is `'1'` or `'0'`. If absent, it defaults to all-`'1'` rows. | Blocking |
| V7 | The count of `'1'` cells in `cell_mask` is `>= MIN_PLAYABLE_CELLS` (16). | Blocking |
| V8 | The playable (`'1'`) cells in `cell_mask` form a single 4-directionally-connected region (a flood-fill from any playable cell reaches every other playable cell). Disconnected/split boards are invalid in v1. | Blocking |
| V9 | Every `pre_placed_pieces` entry's `(row, col)` is within `[0, grid_height) × [0, grid_width)`, refers to a playable cell, and no two entries share the same `(row, col)`. | Blocking |
| V10 | Every `pre_placed_pieces` entry's `candy_type` is a member of this level's `color_pool`. | Blocking |
| V11 | `color_pool` has 3–5 unique entries, each a member of the canonical set `{strawberry, citrus, lemon, apple, grape}`. | Blocking |
| V12 | `move_limit` is an integer `>= 1`. | Blocking |
| V13 | `objectives` has at least 1 entry; every entry's `type` is one of the v1-supported values `{score_target, collect_color}` — any other string is a hard failure (unknown *closed-enum values* are never silently ignored, per Versioning Rules above). | Blocking |
| V14 | For each `score_target` objective, `target` is an integer `> 0`. | Blocking |
| V15 | For each `collect_color` objective, `color` is a member of `color_pool` and `count` is an integer `> 0`. This is the schema-level check that catches an "objective impossible with the color pool" authoring mistake. | Blocking |
| V16 | `star_1_score`, `star_2_score`, `star_3_score` are all integers `> 0`, strictly increasing: `star_1_score < star_2_score < star_3_score`. | Blocking |
| V17 | If the level has at least one `score_target` objective, `star_1_score <= target`, guaranteeing that completing the level's win condition always awards at least 1 star. | Blocking |
| V18 | `star_3_score <= reference_max_score` (Formula A, below). | **Advisory** — `REFERENCE_SCORE_PER_MOVE` is now superseded by `scoring-stars.md` Formula 4's per-`color_pool`-size table (`K=3→255, K=4→185, K=5→160`, not the flat `160` this document's own Formula A still uses); evaluate V18 against `RSPM(K)` for the level's actual `color_pool` size, not the flat constant. Remains Advisory, not Blocking. |
| V19 | `rng_seed`, if present, is an integer `>= -1` (`-1` = unseeded default; any value `>= 0` is a valid fixed seed). | Blocking |

Static validation (V1–V19) cannot verify that a `collect_color` `count` is
*achievable within `move_limit`* against realistic cascade rates — that
requires simulated play, not schema inspection. Per `coding-standards.md`'s
Config/Data test-evidence tier, this is deferred to a smoke check
(`production/qa/smoke-[date].md`), not a blocking validation rule.

### 5. Authoring Workflow

1. A designer duplicates an existing `.tres` level file (or a blank
   `LevelData` template resource) in the appropriate
   `assets/data/levels/<region_code>/` directory.
2. Because `LevelData` is a native Godot `Resource`, the designer edits
   every field — including `cell_mask` and `objectives` — directly in the
   Godot Editor's Inspector panel. No custom level-editor tool is required
   for v1; this is a deliberate scope-saver enabled by the
   "Godot-Resource-friendly" schema choice in Section 1.
3. The designer opens a **Level Preview harness** (a minimal scene,
   conventionally under `src/tools/level_preview/`) that accepts a level
   resource path and boots directly into normal gameplay for that single
   level — bypassing world-map navigation, menus, and unlock-gating — so a
   level under construction is playable in one click without needing prior
   levels three-starred first.
4. Before a level is committed, it is run through the level-validation
   suite (a data-driven gdUnit4 test under `tests/unit/level-data/` that
   iterates every file in `assets/data/levels/` against rules V1–V19 above,
   consistent with `technical-preferences.md`'s testing framework). A level
   that fails any Blocking rule cannot merge.
5. A dedicated visual level-editor tool (drag-and-drop mask painting,
   in-editor objective preview, etc.) is explicitly **not** v1 scope — for
   10–120 hand-authored levels the Inspector-driven workflow above is
   sufficient; revisit as a tools investment only if authoring friction
   becomes a measured bottleneck past Alpha.

### 6. Out of Scope for v1 (Deferred)

The following are explicitly **not** part of schema v1. Each has a noted
extension point for when its owning GDD is written, per the circular
dependency resolution in `systems-index.md`.

| Deferred Feature | Why Deferred | v2+ Extension Point |
|---|---|---|
| **Blockers** (jelly, chocolate, licorice, etc.) | Vocabulary and clear-rules belong to Level Objective & Move-Limit System (#7), not yet written. | Extend `cell_mask` from a boolean playable/void mask to a per-cell enum (`EMPTY`, `PLAYABLE`, `BLOCKER_<type>`), or add a parallel `blocker_layer` field. Requires a `schema_version` bump per the closed-enum policy above. |
| **Pre-placed special candies / locked pieces** | Special-candy taxonomy belongs to Special Candies & Combo Matrix (#5), not yet written. | Extend `pre_placed_pieces` entries with an optional `special_type` field and a `locked: bool` flag once that vocabulary exists. |
| **Teaching / tutorial overlays** | Onboarding-script ownership is Game UI/Screens Flow (#10) territory, not a board-data concern. | Add a separate `tutorial_hint_id` reference field pointing at content owned by Game UI/Screens Flow, rather than embedding overlay content in `LevelData` itself. |
| **Brewing ingredient yield metadata** | Booster Brewing Meta (#12) is Phase 2, gated on founder approval of a friction prototype; its data needs are unknown until that prototype concludes. | Add an `ingredient_yield_overrides` map (candy_type → yield multiplier) once Booster Brewing Meta defines its harvest formula. |
| **Event/seasonal reskin hooks** | Events/Theming Engine (#13) is Phase 3, explicitly deferred. | Add a `theme_override` field (palette/prop swap reference) so an event can re-skin an existing level without duplicating the level file. |
| **Non-contiguous ("split") boards** | No validated design need yet; V8 hard-requires single-region connectivity to keep v1 simple. | Relax V8 to allow multiple disjoint connected regions if a future level design calls for it; requires re-validating Board Engine's gravity/refill assumptions first. |

---

## Formulas

### Formula A — Reference Max Score (Sanity Bound)

```
reference_max_score = move_limit * REFERENCE_SCORE_PER_MOVE
```

| Symbol | Type | Range | Description |
|--------|------|-------|-------------|
| `move_limit` | int | 1–99 | This level's authored move allotment (schema field, Section 2). |
| `REFERENCE_SCORE_PER_MOVE` | int (tuning constant) | 100–260, default `160` | Provisional average score-per-move benchmark. Derived from the concept prototype's greedy-bot playthrough (`prototypes/sweet-cascade-concept/REPORT.md`: 3,960 points over 25 moves ≈ 158.4 pts/move, rounded up to 160). **Superseded** — `scoring-stars.md` Formula 4 now supplies the authoritative, `color_pool`-size-keyed table (`REFERENCE_SCORE_PER_MOVE(K)`: `K=3→255, K=4→185, K=5→160`); this flat constant and Formula A remain only as this document's own pre-authoring sanity-check tool. |
| `reference_max_score` | int | Unbounded, scales linearly with `move_limit` | An advisory upper bound on achievable score for this level, used by V18 to flag a `star_3_score` that may be mathematically out of reach. |

**Output range**: Unbounded above (grows linearly with `move_limit`); not
clamped, because it is a soft advisory ceiling consumed by a validation
warning (V18), never a runtime score cap enforced against the player.

**Worked example** (the prototype's reference level, `move_limit = 25`):
```
reference_max_score = 25 * 160 = 4,000
```
This lands close to the prototype's actually-measured greedy-bot score of
3,960 — a useful cross-check that `REFERENCE_SCORE_PER_MOVE = 160` is a
reasonable provisional constant, not an arbitrary guess.

**Superseded (2026-07-18 cross-GDD sync).** `scoring-stars.md` Formula 4
now supplies the authoritative, per-`color_pool`-size table this constant
was always meant to be provisional for (`REFERENCE_SCORE_PER_MOVE(K)`:
`K=3 → 255`, `K=4 → 185`, `K=5 → 160`) — the flat `160` above remains valid
only as this document's own pre-authoring sanity-check default (it happens
to equal the `K=5` case exactly, since that was the shared empirical
anchor). V18 (§4) should be evaluated against the correct `RSPM(K)` for a
level's actual `color_pool` size, not this flat constant, once Scoring &
Star Thresholds' table is in hand — the safe-range ceiling above is raised
from 250 to 260 so `K=3`'s `255` fits within it.

### Formula B — Star Threshold Validity

```
is_valid_star_config =
    (star_1_score <= score_target OR score_target is absent)
    AND (star_1_score < star_2_score)
    AND (star_2_score < star_3_score)
    AND (star_3_score <= reference_max_score)
```

| Symbol | Type | Range | Description |
|--------|------|-------|-------------|
| `score_target` | int or absent | `> 0`, or absent if the level has no `score_target` objective | This level's `score_target` objective value, if one exists. If the level's only objective is `collect_color`, this constraint is skipped entirely (score still accrues and gates stars, but there is no target value to compare against). |
| `star_1_score` | int | `> 0` | Score required for 1 star (schema field). |
| `star_2_score` | int | `> star_1_score` | Score required for 2 stars (schema field). |
| `star_3_score` | int | `> star_2_score` | Score required for 3 stars (schema field). |
| `reference_max_score` | int | From Formula A | Advisory upper bound. |
| `is_valid_star_config` | bool | `{true, false}` | Validation gate result consumed by V16–V18. |

**Output range**: Boolean — this formula is a pass/fail gate, not a
magnitude.

**Worked example** (the prototype's reference level):
```
score_target    = 2,500
star_1_score    = 2,500
star_2_score    = 3,200
star_3_score    = 3,900
reference_max_score = 4,000   (from Formula A)

2,500 <= 2,500   -> true
2,500 <  3,200   -> true
3,200 <  3,900   -> true
3,900 <= 4,000   -> true
is_valid_star_config = true
```
This configuration passes validation with a deliberately tight 100-point
(2.5%) margin between `star_3_score` and `reference_max_score` — 3-starring
this level is meant to require near-optimal play, not merely completion.

---

## Edge Cases

| Scenario | Expected Behavior | Rationale |
|----------|------------------|-----------|
| `cell_mask` field omitted entirely | Defaults to a full rectangle (`grid_height` rows of `grid_width` `'1'`s). | Most levels are plain rectangles; forcing every level to spell out a trivial full mask would be authoring noise. |
| `pre_placed_pieces` omitted entirely | Defaults to `[]` — fully RNG-generated starting board via RNG Service. | Matches the prototype's reference level, which had no pre-placed setup. |
| `pre_placed_pieces` entry references a `candy_type` not in `color_pool` | Validation rejects the file (V10). Authoring is blocked until fixed — never silently dropped or silently substituted. | A silently-dropped pre-placed piece would make the authored board state diverge invisibly from what the designer intended — a Pillar 2 violation in spirit (invisible manipulation of authored data). |
| A level lists both a `score_target` and a `collect_color` objective simultaneously | Both must be satisfied (logical AND) before win; runtime display of multiple simultaneous objectives is Level Objective & Move-Limit System's (#7) responsibility. | The schema's `objectives` field is a list specifically to support this multi-objective case without a v2 schema change. |
| `rng_seed >= 0` is set on a level | The entire RNG stream for that level attempt — initial board **and** every subsequent refill/cascade spawn — is deterministic from that seed, so two attempts with identical move sequences produce bit-identical boards. | Required for future daily-challenge/event fairness (Phase 3): every player must see the same board for the same seed. Unused at MVP/launch (`rng_seed = -1` on all 10 MVP levels). |
| Two level files under `assets/data/levels/` share the same `level_id` | Validation rejects both files at directory-scan time (V2). Build/CI fails; neither ships. | `level_id` is the save-data key (Save & Persistence); a collision would corrupt or merge two players' unrelated progress records. |
| `schema_version` in a file exceeds the currently supported set (e.g. a file authored against a future v2 is loaded by a v1-only build) | Load fails loudly — the level is excluded from the playable set and the failure is logged as an error, never as a silent skip or a partially-populated level. | Per Pillar 2, an invisible partial-load (e.g., an objective silently missing) would produce an uncompletable or nonsensically-easy level with no visible explanation — worse than refusing to load at all. |
| `cell_mask` describes two or more disconnected playable regions | Validation rejects the file (V8). | v1 explicitly does not support split boards (see Out of Scope); Board Engine's gravity/refill logic assumes single-region connectivity. |
| `grid_width` or `grid_height` is 3 (schema minimum) with a fully-open `cell_mask` | Valid only if the *other* axis is large enough that total playable cells `>= 16` (e.g., `3x6=18` passes, `3x5=15` fails V7). | V5 (dimension bounds) and V7 (minimum playable area) are independent rules that compose naturally to rule out degenerate tiny boards, without needing a separate, higher `MIN_GRID_DIM` constant. |
| A level's only objective is `collect_color` (no `score_target` present) | V17 (`star_1_score <= target`) is skipped — score still accrues from matches throughout play and still gates star count via V16, there's just no `score_target` value to cross-check against. | Scoring and win-condition are parallel systems in this game (per `game-concept.md`'s Core Mechanics); decoupling them is intentional, not a gap. |
| `color_pool` requests all 5 canonical candy types on a small board (e.g. 3x6, 18 cells) | Valid per schema (V11 only checks 3–5 unique canonical members) — but flagged only as an Advisory smoke-check concern (achievability of matches, not schema shape), never a Blocking validation failure. | Static schema validation cannot evaluate live match probability; that is simulation/playtest territory, not this document's job (see Section 4 closing note). |

---

## Dependencies

Level Data Format is Foundation-layer with **zero inbound design
dependencies of its own** — it depends on no other GDD to be authored. The
table below lists systems that depend on *it*, several of which are not yet
written; per `design/CLAUDE.md`'s bidirectionality rule, each of those
future documents is expected to list Level Data Format in its own
Dependencies section when it is authored. This table records that
expectation now so it isn't lost.

| System | Direction | Nature of Dependency |
|--------|-----------|---------------------|
| RNG Service (`design/gdd/rng-service.md`) | Anticipated | `rng_seed` is consumed per RNG Service's seed-set contract (Formulas F1–F3). Separately, `level_id` (a String here, format `<region_code>-<3-digit-sequence>`) must be resolved to a fixed, versioned ordinal integer via a level manifest before being passed to RNG Service's `start_level_session()`, per rng-service.md §3 and its Edge Cases — that resolution is expected to be owned by Match-3 Board Engine (the direct caller of RNG Service), not by this document. This document defines field shapes only, not the seeding mechanism or the manifest itself. |
| Match-3 Board Engine (`design/gdd/board-engine.md`) | Board Engine depends on this | Reads `grid_width`/`grid_height`, `cell_mask`, `pre_placed_pieces`, and `color_pool` to bootstrap a playable board at level start. Does NOT read `move_limit` — move-limit enforcement is Level Objective's scope per board-engine.md. Board Engine also owns the `level_manifest` String→ordinal resolution anticipated in the RNG Service row above. |
| Special Candies & Combo Matrix (#5, not yet written) | Soft co-design (planned extension) | v1 intentionally omits pre-placed specials and blocker vocabulary (Section 6); v2 will extend `pre_placed_pieces` once that GDD defines the special-candy taxonomy. |
| Scoring & Star Thresholds (#6, not yet written) | Soft co-design (planned extension) | `REFERENCE_SCORE_PER_MOVE` and `reference_max_score` (Formula A) are provisional placeholders owned here only until that GDD formalizes the authoritative point-value formula, at which point it should reconcile with or supersede these values. |
| Level Objective & Move-Limit System (#7, not yet written) | Objective depends on this | Reads `objectives` and `move_limit` to drive runtime win/lose evaluation; owns the runtime semantics of each objective type, while this document owns only the data shape. |
| Level Progression / World Map (#11, not yet written) | World Map depends on this | Reads `level_id`, `region`, `display_number`, and (via Save & Persistence) star thresholds to build the node graph and star-gated unlocks. |
| Touch & Input System (`design/gdd/touch-input.md`, APPROVED) | Touch & Input depends on this | Reads `grid_width`/`grid_height` only (mapped to `board_cols`/`board_rows`) for swipe bounds-checking and `cell_size_px` computation (`touch-input.md` Formulas 1–3) — never objective, blocker, or candy-palette data. **Reciprocal note fulfilled** — `touch-input.md`'s own Dependencies section already lists this document. |
| Game UI/Screens Flow (`design/gdd/screen-flow.md`, APPROVED) | Screen Flow depends on this | Reads `objectives` (order-preserved, for Pre-Level Card / HUD badge display order) and `star_1_score`/`star_2_score`/`star_3_score` (for Results display), alongside `level_id`/`display_number`/`region`. **Reciprocal note fulfilled** — `screen-flow.md`'s own Dependencies section already lists this document. |
| Booster Brewing Meta (#12, Phase 2, gated) | Booster Brewing depends on this | Anticipated `ingredient_yield_overrides` extension point (Section 6, out of v1 scope). |
| Events/Theming Engine (#13, Phase 3) | Events depends on this | Anticipated `theme_override` extension point (Section 6, out of v1 scope) so events can re-skin existing levels without duplicating files. |

---

## Tuning Knobs

| Parameter | Current Value | Safe Range | Effect of Increase | Effect of Decrease |
|-----------|--------------|------------|-------------------|-------------------|
| `grid_width` / `grid_height` | 8 × 8 (reference level) | 3–9 each axis | Larger boards support more objective variety and longer cascades, but raise draw-call count and RNG spawn cost against the `technical-preferences.md` budget (≤100 draw calls during heaviest cascade). | Smaller boards resolve faster and cost less, but limit playable area and objective variety; combined with `MIN_PLAYABLE_CELLS`, very small dimensions may become infeasible (see Edge Cases). |
| `MIN_PLAYABLE_CELLS` | 16 | 9–36 (must stay well under the 81-cell max board) | Raising the floor forces bigger minimum boards, reducing design freedom for compact/showcase levels. | Lowering the floor risks boards too cramped to reliably guarantee a legal match exists. |
| `color_pool` size | 5 (reference level) | 3–5 | More colors reduce coincidental matches, producing harder, sparser cascades. The concept prototype found 6 types "too sparse," which is why v1 hard-caps at 5 (`REPORT.md`). | Fewer colors increase match frequency, producing easier, faster, more forgiving cascades — useful for early onboarding levels. |
| `move_limit` | 25 (reference level) | Schema floor 1; launch design guidance 10–60 | More moves is more forgiving and raises the `reference_max_score` ceiling (Formula A), permitting higher star thresholds. | Fewer moves tightens the puzzle; very low values risk objectives becoming unreachable (a smoke-check, not schema-validation, concern). |
| `REFERENCE_SCORE_PER_MOVE` | 160 (flat; superseded by `scoring-stars.md` Formula 4's `K`-keyed table for actual evaluation) | 100–260 (raised ceiling so `K=3`'s `255` fits; ownership transferred to Scoring & Star Thresholds) | Loosens the V18 sanity bound, permitting higher `star_3_score` values to pass without warning. | Tightens the bound, forcing more conservative (easier) 3-star targets or triggering more V18 warnings. |
| `star_1`/`star_2`/`star_3` spacing | 2,500 / 3,200 / 3,900 (reference level) | `star_1_score <= score_target` (if present); each gap should be at least ~10% of `score_target` as a design guideline (not schema-enforced) | Wider gaps make 2- and 3-star mastery rarer and more prestigious. | Narrower gaps make stars easier to collect across the board, reducing replay incentive per `game-concept.md`'s "three-starring old levels" retention hook. |

---

## Acceptance Criteria

- [ ] A `.tres` level resource conforming to schema v1 loads in a Godot
      headless test script with every required field populated and typed
      as specified in the Field Reference (Section 2).
- [ ] Loading a level file that omits every optional field (`cell_mask`,
      `pre_placed_pieces`, `rng_seed`) applies the documented default for
      each without raising an error.
- [ ] Loading a level file containing an unrecognized *optional field* (not
      a closed-enum value) logs a warning but still loads successfully,
      per the Versioning & Migration policy.
- [ ] Loading a level file with `schema_version` outside the currently
      supported set `{1}` fails loudly (excluded from the playable set,
      logged as an error) rather than partially loading.
- [ ] A gdUnit4 test suite under `tests/unit/level-data/` runs validation
      rules V1–V19 against every file in `assets/data/levels/` and reports
      zero Blocking failures for all 10 MVP levels before the Vertical
      Slice gate.
- [ ] A unit test asserts V16 rejects a level with non-strictly-increasing
      star thresholds (e.g., `[2500, 2500, 3000]`).
- [ ] A unit test asserts V15 rejects a `collect_color` objective whose
      `color` is absent from that level's `color_pool`.
- [ ] A unit test asserts V7 rejects a `cell_mask` with fewer than 16
      playable cells.
- [ ] A unit test asserts V8 rejects a `cell_mask` describing two
      disconnected playable regions.
- [ ] A unit test asserts V2 rejects a directory scan containing two files
      with the same `level_id`.
- [ ] The reference level (`region: candy_kingdom_hub`, `grid 8x8`,
      5-color pool, `move_limit: 25`, `score_target: 2500`,
      `star scores [2500, 3200, 3900]`) passes every rule V1–V19 with zero
      Blocking failures and zero Advisory warnings.
- [ ] A designer can open the Level Preview harness, point it at any single
      `.tres` file under `assets/data/levels/`, and enter live play for
      that level without navigating the world map or satisfying any
      unlock gate.
- [ ] No gameplay value defined by this schema (grid size, color pool,
      move limit, objective targets, star thresholds) exists as a
      hardcoded literal anywhere in `src/` — every instance is spot-check
      traceable back to a `.tres` file, per `coding-standards.md`'s
      data-driven rule.

---

## Cross-References

| This Document References | Target GDD | Specific Element Referenced | Nature |
|--------------------------|-----------|----------------------------|--------|
| Canonical candy type names/hexes for `color_pool` and `pre_placed_pieces.candy_type` | `design/art/art-bible.md` | Base Candy Roster table (Strawberry Red, Citrus Orange, Lemon Yellow, Apple Green, Grape Purple) | Data dependency |
| Reference level tuning values (8×8 board, 5 colors, 25 moves, 2,500 target) | `prototypes/sweet-cascade-concept/REPORT.md` | "If Proceeding" section, validated tuning values | Data dependency (prototype, not a GDD — cited as design rationale) |
| `REFERENCE_SCORE_PER_MOVE` constant, pending supersession | `design/gdd/scoring-stars.md` *(not yet written)* | Authoritative point-value formula | Rule dependency — provisional |
| `objectives` field consumption at runtime | `design/gdd/level-objectives.md` *(not yet written)* | Win/lose evaluation logic | Ownership handoff — this doc owns data shape only |
| `grid_width`/`grid_height`/`cell_mask`/`color_pool`/`move_limit` consumption at bootstrap | `design/gdd/board-engine.md` *(not yet written)* | Board initialization | Data dependency |
| `region`/`display_number`/`level_id` node-graph usage | `design/gdd/world-map.md` *(not yet written)* | Region roster, unlock gating | Data dependency |
| `rng_seed` field semantics; `level_id`'s String→integer resolution requirement | `design/gdd/rng-service.md` | Seed-set contract (Formulas F1–F3); integer `level_id` requirement (§3, Edge Cases) | Rule dependency |

---

## Open Questions

| Question | Owner | Deadline | Resolution |
|----------|-------|----------|-----------|
| Should `collect_color`'s tile-count semantics formally reconcile with Booster Brewing Meta's future ingredient-harvest yield, or stay permanently decoupled? | game-designer | At Level Objective & Move-Limit System (#7) authoring | — |
| Should `region`'s value set be validated against a formal registry once one exists, rather than only checked for non-emptiness? | game-designer | At Level Progression / World Map (#11) authoring | — |
| Should `display_number` ownership move from this schema to the World Map node graph once that system exists, to avoid two sources of truth for level ordering? | game-designer | At Level Progression / World Map (#11) authoring | — |
| Does V18's Advisory (non-blocking) status get promoted to Blocking once Scoring & Star Thresholds (#6) supersedes `REFERENCE_SCORE_PER_MOVE` with an authoritative formula? | systems-designer | At Scoring & Star Thresholds (#6) authoring | — |
| Does a Phase-3 level's authored `rng_seed >= 0` require a new RNG Service production entry point? `rng-service.md`'s only literal-seed API (`start_test_session()`) is documented as test/debug-only, and `start_daily_session()` does not read this field — neither currently composes with a per-level authored fixed seed. | systems-designer | Before Events/Theming Engine (#13) or any daily-challenge/event level authoring begins | — |
| Is single-region board connectivity (V8) too restrictive for future "split board" level designs, or is that a real future need worth a v2 schema extension? | game-designer | Revisit at Alpha content planning (120-level scope) | — |
| Should saw-tooth difficulty pacing (`game-concept.md`'s Flow State Design) get cross-level validation, given this schema's V1–V19 rules validate only a single level file in isolation with no awareness of neighboring levels' difficulty curve? | game-designer | At Vertical Slice content-authoring retrospective | Decide between a lightweight Advisory cross-level check added to the validation suite (e.g., comparing consecutive levels' `move_limit`/`reference_max_score` trend) versus manual-playtest-only enforcement with no schema-level check at all — not yet decided. |
