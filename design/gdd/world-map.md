# Level Progression / World Map

*Status: Draft — awaiting /design-review*
*Layer: Feature · Priority: Alpha · Phase: MVP · Category: Progression*
*Author: systems-designer · Created: 2026-07-18 · Last Updated: 2026-07-18*
*Implements Pillar: Pillar 3 — The World Is Alive (primary: the multi-region
structure and per-region theming slots are the literal backbone of a living,
traveled world); supports Pillar 2 — Clever, Never Cheated (every unlock gate
is a visible, inspectable, authored star-count formula — never hidden
manipulation and never a monetized bypass)*
*Depends On: Level Data Format (`design/gdd/level-data-format.md`, APPROVED),
Save & Persistence (`design/gdd/save-persistence.md`, Draft), Match-3 Board
Engine (`design/gdd/board-engine.md`, APPROVED — manifest
cross-validation only, no runtime dependency), Scoring & Star Thresholds
(`design/gdd/scoring-stars.md`, #6, Draft — soft/forward
dependency, see Dependencies)*
*Depended On By: Game UI/Screens Flow (`design/gdd/screen-flow.md`, #10,
Draft), Events/Theming Engine (`design/gdd/events-theming.md`, #13,
Phase 3, forward dependency)*
*Source: `design/gdd/systems-index.md` · `design/gdd/level-data-format.md` ·
`design/gdd/board-engine.md` · `design/gdd/save-persistence.md` ·
`design/narrative/characters-and-tone.md` · `design/art/art-bible.md` ·
`design/gdd/game-concept.md`*

---

## Overview

Level Progression / World Map is the Feature-layer system that organizes
Sweet Cascade's levels into a persistent, navigable structure: a strictly
linear node graph per region, gated region-to-region by cumulative
star-count thresholds, and dressed with per-region theming slots (gradient
set, resident character, ambient mood) that make the journey feel alive
without ever gating progress behind anything but honestly-earned stars. It
owns a single authoritative manifest, `world_map_manifest.tres`, that groups
every level into an ordered region sequence and declares each region's
unlock threshold; every node's locked/unlocked/completed state and every
completion percentage is **derived live** from that manifest plus Save &
Persistence's existing `level_records`/`total_stars` data — zero duplicated
unlock booleans are ever written to the save file. Though this system's
Priority is Alpha (the 4-region, 120-level roster arrives at Alpha per
`game-concept.md`'s Scope Tiers), it is designed in full now and
content-gated to a single region for MVP (Candy Kingdom Hub, 10 levels,
always-unlocked, its region gate present but never exercised) — the same
rules, formulas, and manifest schema serve both scales without modification.

---

## Player Fantasy

The World Map is where Sweet Cascade's "collector-traveler" half of the
Core Fantasy (`game-concept.md`) lives — the player isn't just clearing a
list of levels, they're visiting a world that has places, moods, and
neighbors in it. This system exists to protect five specific feelings:

1. **A journey, not a level-select grid.** Each region has a personality —
   Candy Kingdom Hub's welcoming showoff energy, Frosted Peak's quiet
   patience, Sundrop Grove's sun-warmed calm, Thistleberry Hollow's cozy
   mystery (`design/narrative/characters-and-tone.md`) — so moving from
   region to region feels like arriving somewhere new, not unlocking the
   next row of a spreadsheet.
2. **Fair, visible progress — never a paywall.** Every gate a player meets
   on the map is a star count they can see accumulating from their own
   play. `game-concept.md`'s Anti-Pillar is explicit: monetization is
   undesigned and Sweet Cascade is NOT pay-to-win. This document's entire
   unlock surface is built exclusively from earned stars — there is no
   currency, timer, or purchase parameter anywhere in its formulas, and
   none may be added without a founder-approved amendment, exactly like the
   precedent set for the character-dressing anti-pillar amendment.
3. **Curiosity about what's next.** The next, still-locked region is
   visible on the map as a silhouette — a held-back promise, not a full
   spoiler — directly serving the "What does the next region look like?"
   curiosity retention hook named in `game-concept.md`.
4. **Achievable mastery, never grind.** The star-gate math (Formula 1) is
   tuned so a player who plays reasonably well — not perfectly — clears
   every gate through natural play. Replaying old levels for extra stars is
   an invitation ("you could three-star that one"), never mandatory
   homework to keep moving forward.
5. **Warm company, never a gatekeeper.** Fizz is always on the map,
   cheering the same way at every visit; each region's resident greets the
   player with a one-line hello. Per `characters-and-tone.md`'s explicit
   boundary, no character ever blocks, delays, or is required to be
   "talked to" before a level or region opens — characters dress the
   journey, they never gate it.

---

## Detailed Rules

**Scope boundary.** This document defines the node graph, the unlock
rules, and the region theming *contract* (what fields a region declares).
It does **not** define path rendering, camera/scroll animation, node icon
art, the diorama vignette art assets, or exact theming hex values — those
are `design/ux/`, Game UI/Screens Flow, Juice Layer, and `art-bible.md`
territory respectively. Every schema below is a design-level field
contract; the exact `WorldMapManifest`/`RegionManifestEntry` GDScript
`Resource` implementation is `godot-gdscript-specialist` territory — field
names, types, and defaults are the load-bearing part of this contract.

### 1. Node Graph Model

**Decision: strictly linear, both within a region and across regions. No
branching, for the full system, not only MVP.**

- **Within a region**: a region's levels form a single ordered sequence
  (`level_sequence: Array[String]`, § Detailed Rules 2). Level `i+1` is
  reachable only after level `i` is completed. No forks, no alternate
  routes, no optional side-nodes.
- **Across regions**: regions themselves are an ordered sequence
  (`regions: Array[RegionManifestEntry]`). Region unlock is governed by a
  single cumulative star-count gate (Formula 3), which — as proven in
  § Detailed Rules 3 — produces sequential region access as an emergent
  property of monotonically increasing gates, without needing a second,
  separate "previous region unlocked" check.

**Justification against a branching (DAG) alternative:**

| Consideration | Linear | Branching (rejected) |
|---|---|---|
| Art direction | Already locked: `art-bible.md`'s World Map spec describes "a **path** (one path per region, gated sequentially), rendered as a ribbon" — singular, sequential. Building branch support would contradict already-approved art direction. | Would require a new, unapproved ribbon-rendering model (forks, merges) with no design need identified anywhere in the source docs. |
| Validation complexity | A flat ordered array; "is level N unlocked" is one array-index comparison plus one dictionary lookup (Formula 2). | A directed graph requires cycle detection, multi-parent unlock logic (AND vs. OR prerequisites), and a materially larger validation surface for a genre convention that doesn't ask for it. |
| Onboarding curve | `game-concept.md`: "Levels 1-5 teach swap → match-4 → match-5 → objectives, one concept at a time." A single path guarantees every player sees concepts in the exact authored order. | A branch could let a player skip a teaching level entirely, breaking the onboarding sequence guarantee. |
| Genre precedent | Matches Candy Crush Saga / Royal Match's map convention, the game's own comparable titles. | No comparable title in `game-concept.md`'s Inspiration table uses branching for its core map. |
| Evidence of design need | None found in any read source document. | None found — would be scope-adding speculation, not a validated requirement. |

Branching is not deferred as a "later phase" — it is explicitly rejected for
the system as designed, with a note in Open Questions in case a future,
clearly-scoped alternate-mode need (e.g., a hypothetical "hard mode" path)
emerges post-launch.

**Node states (per level):**

| State | Meaning |
|---|---|
| `LOCKED` | Not yet playable. Either its region is locked, or the immediately preceding level in its region's sequence has never been completed. |
| `UNLOCKED` | Playable, not yet won. Either it is the first level in an unlocked region, or the immediately preceding level in its sequence has been completed at least once. |
| `COMPLETED` | Has been won at least once. Always carries a `best_stars` value of **1–3** (never 0 — Level Data Format's V17 guarantees any win awards at least 1 star). Remains fully replayable. |

Exactly one of these three states applies to a given level at any moment;
there is no separate "current"/"frontier" node state stored anywhere — the
map's current-frontier position (§ Detailed Rules 5) is a UI concept
computed by scanning node states, not a fourth stored state.

**Region states:**

| State | Meaning |
|---|---|
| `LOCKED` | The player's cumulative `total_stars` has not yet met this region's `stars_required_to_unlock`. No level within it is playable. |
| `UNLOCKED` | The gate is met. Individual levels within it still follow the per-level sequential rule above — unlocking a region only ever opens its *first* level automatically. |

There is no separate "region fully completed" state — a region where every
level has been three-starred is simply `UNLOCKED` with
`region_completion_pct = 100` (Formula 5); "fully mastered" is a UI badge
computed from that percentage, not a distinct stored or derived state.

### 2. Manifest Architecture & Ownership

**Two files, cross-validated — not one shared file.** Match-3 Board Engine
already owns `assets/data/level_manifest.tres` (a flat, ordered,
**append-only** `Array[String]` of every `level_id` ever authored), used
exclusively to resolve `level_id` → `manifest_index` for RNG Service's
`start_level_session()` (`board-engine.md` §2, Formula 1). This document
respects that ownership rather than requesting board-engine.md be edited or
merged, and introduces a second, separately-owned manifest:

- **File**: `assets/data/world_map_manifest.tres`, a custom
  `WorldMapManifest` Resource, owned by this document.
- **Why two files, not one shared file**:
  1. **Different stability contracts.** `level_manifest.tres`'s array order
     is append-only and *must never change* — reordering it would silently
     rewrite every affected level's entire RNG draw sequence
     (`board-engine.md` §2). `world_map_manifest.tres`'s `regions` array
     order and each region's `level_sequence` order, by contrast, **are**
     expected to be edited over time (new regions inserted, a level's map
     position adjusted) as content planning evolves toward the 4-region,
     120-level Alpha roster. Merging a field that must never reorder with
     fields that routinely do would either freeze world-map curation
     unnecessarily or put RNG determinism at risk from an unrelated edit.
  2. **Different consumers.** `level_manifest.tres` is read only by Board
     Engine, once per level bootstrap, for a single integer. World map
     data is read by Game UI/Screens Flow on every map-screen visit, for
     rich per-region structure (theming slots, gates, sequences) Board
     Engine has no use for. Coupling them would give each consumer an
     unrelated dependency on data it does not need.
  3. **No cost to keeping them separate.** Both are small, versioned,
     validated resources; a shared gdUnit4 cross-validation test (§
     Formulas' Validation Contract, rules W6–W7) makes the "two sources of
     truth" risk fully mechanical and CI-enforced, not a matter of
     discipline.

**Recommended follow-up** (not made here, per this document's file-edit
scope): `board-engine.md`'s Acceptance Criteria / Cross-References should
add a reciprocal test entry acknowledging this document's cross-manifest
validation rules, matching the exact pattern `board-engine.md` itself used
when it recommended (without editing) a follow-up to
`level-data-format.md`'s Authoring Workflow.

**`WorldMapManifest` top-level schema:**

| Field | Type | Required | Default | Description |
|---|---|---|---|---|
| `schema_version` | int | Yes | — | Versioning contract, § Detailed Rules 6. v1 files use `1`. |
| `regions` | Array[RegionManifestEntry] | Yes | — | Ordered list of every region. Array order is the region unlock/display sequence — region 0 is always the starting region. Minimum length 1 (MVP: exactly 1 entry). |

**`RegionManifestEntry` schema:**

| Field | Type | Required | Default | Description |
|---|---|---|---|---|
| `region_code` | String | Yes | — | Matches Level Data Format's `region` field for every level assigned to this region, and is the join key art's Region Theme Resource is keyed by (§ Detailed Rules 4). Unique across the manifest. |
| `display_name` | String | Yes | — | Player-facing region name (narrative content, e.g. "Frosted Peak"). |
| `stars_required_to_unlock` | int | Yes | — | This region's star gate (Formula 1). `regions[0]` is always `0`. |
| `resident_character_id` | String | No | `""` | Narrative mount point — the resident character's id (e.g. `"marzi"`), per `characters-and-tone.md`. Empty string is legal (a region with no assigned resident yet). |
| `map_diorama_asset_id` | String | No | `""` | Reference to the region's map-screen diorama vignette art asset (`art-bible.md`'s World Map spec: "a small diorama vignette using that region's decorative props"). |
| `level_sequence` | Array[String] | Yes | — | Ordered `level_id` strings — this region's linear node graph. Minimum length `MIN_LEVELS_PER_REGION` (Tuning Knobs, default `1`). |

**Region theming values (gradient hex triplet, UI trim color, decorative
prop set, ambient particle theme) are deliberately NOT fields on this
manifest.** They live in an art-owned Region Theme Resource, keyed by the
same `region_code` string, per `art-bible.md`'s "Regional Modularity"
4-element system. This document declares that the slot exists and is
looked up by `region_code`; it does not own or validate the pixel/hex
values themselves — the same ownership split Level Data Format uses for
`color_pool` (candy type *names*, not hex values).

**Resolving Level Data Format's open ownership questions.** This document
resolves both items `level-data-format.md`'s Open Questions table flagged
as deferred to World Map's authoring:

- *"Should `region`'s value set be validated against a formal registry?"*
  **Yes.** `world_map_manifest.tres`'s `regions[*].region_code` list **is**
  that formal registry. A level file's `region` value must equal the
  `region_code` of whichever region's `level_sequence` contains that
  level's `level_id` (Validation rule W8). This is a Blocking,
  build-time/CI check — never a silent runtime reconciliation.
- *"Should `display_number` ownership move to World Map?"* **Partially —
  and in a way stronger than a straight ownership transfer.** The level
  file's `display_number` field is **kept** (no `schema_version` bump to
  Level Data Format needed) as an authoring convenience: it lets a
  designer preview a rough number in the Godot Inspector and lets the
  Level Preview harness (`level-data-format.md` §5) display *something*
  for a level not yet added to the world-map manifest. But once a level
  **is** map-integrated, the **authoritative, player-facing** display
  number is *derived* from its position in the manifest's concatenated
  region sequence (Formula 4) — not read from the level file's authored
  field. This is stronger than "world-map validates the authored value" —
  it removes the possibility of drift entirely, because the authoritative
  value is computed, not duplicated. This achieves `level-data-format.md`'s
  own originally-stated goal ("regions can be reordered on the map without
  invalidating save data") more completely than the original design,
  since reordering `world_map_manifest.tres`'s `regions` array now
  automatically and correctly recomputes every subsequent level's display
  number with zero manual bookkeeping and zero migration step (§ Edge
  Cases). `level-data-format.md`'s Open Questions table should be updated
  to record this resolution at its next review pass — flagged here, not
  edited there, per this document's file-edit scope.

### 3. Unlock Rules & Derived State

**No unlock booleans are ever stored in the save file.** Every node and
region state in § Detailed Rules 1 is a pure function of (a) this
document's manifest and (b) Save & Persistence's existing, unmodified
schema: `level_records: Dictionary<level_id, LevelRecord>` (presence of a
key = the level has been won at least once) and `get_total_stars()`
(already a maintained, self-healing aggregate — `save-persistence.md`
Formula 3). **Zero schema changes to Save & Persistence are required by
this document** — its existing shape was already exactly what World Map
needs, and its Dependencies section already anticipated this consumption
pattern.

**Scoring surface boundary.** This document reads exactly one number per
level from Save & Persistence: `best_stars` (int, 0–3, already computed and
persisted by the time World Map queries it). It never computes, weights,
reinterprets, or second-guesses a star value itself — Scoring & Star
Thresholds (#6, Draft) owns how a raw score becomes a star
count; Level Objective & Move-Limit System (#7, Draft) is expected to own
calling `record_level_completion()` on a win — flagged as not yet
confirmed by `level-objectives.md` itself as of the 2026-07-18 review, see
Dependencies. World Map's dependency on
those two systems is therefore **soft and indirect** — it depends only on
the already-persisted output, never on their internal formulas. **Named
seam, not designed here**: if a future system introduces additional
per-level completion metadata beyond the 0–3 star count (e.g., a "flawless
clear" flag) that World Map might someday want to surface as a map badge,
that would arrive as a new, optional, additive field on Save &
Persistence's `LevelRecord` (per that document's §8 additive-field
philosophy) — not designed or reserved here, only named as an anticipated
future extension point.

**Sequential region unlock is an emergent property, not a second rule.**
Because `stars_required_to_unlock` is validated to be non-decreasing across
the `regions` array (Validation rule W9) and `regions[0]`'s gate is always
`0` (rule W10), a player can never have region `k+1` unlocked while region
`k` remains locked — reaching a higher gate value mathematically implies
every lower gate value is already met. This is a single, visible, inspect
able formula producing sequential access as a *consequence*, not a
second, overlapping "is the previous region unlocked" check bolted on top
of it — directly in the spirit of Pillar 2 (one authored lever, not hidden
double-gating logic).

**No pay-gates, ever.** Restated from Player Fantasy for enforceability:
Formulas 1–3 (below) take no currency, timer, purchase, or account-tier
parameter of any kind. `stars_required_to_unlock` is earned progress only.

### 4. Region Theming Slots

Each region declares exactly the following, per `art-bible.md`'s
"Regional Modularity" 4-element production system plus two narrative mount
points. World Map's job is to declare that each slot **exists** and is
addressable; the actual creative content in every slot below (hex values,
prop art, character personality, flavor lines) is authored by
`art-director` / `narrative-director`, never by this document:

| Slot | Owner (content) | This document's role |
|---|---|---|
| Background gradient triplet (3-stop, 135°) | `art-bible.md` | Declares the join key (`region_code`) only |
| UI trim/accent color | `art-bible.md` | Declares the join key only |
| Decorative prop set (2–4 silhouettes) | `art-bible.md` | Declares the join key only |
| Ambient particle theme | `art-bible.md` | Declares the join key only |
| Resident character | `characters-and-tone.md` | Owns the field: `resident_character_id` |
| Map diorama vignette asset | `art-bible.md` (art) / this doc (slot) | Owns the field: `map_diorama_asset_id` |
| Region personality flavor lines | `characters-and-tone.md` | Not a manifest field — flavor-line pool selection is a Game UI/Screens Flow + narrative wiring concern (`characters-and-tone.md` §6, item 4), out of this document's data contract |

**Fizz's mount point is not a per-region field.** Per
`characters-and-tone.md` §2, Fizz appears on the World Map screen
unconditionally — the same companion, present at every region view, never
gated or regionally reskinned. This document therefore declares Fizz's map
placement as a **structural requirement on the World Map screen itself**
(a single, always-reserved "companion anchor," independent of which region
is currently in camera view), not as manifest data. Exact placement,
idle-loop animation, and screen-transition behavior are Game
UI/Screens Flow, Juice Layer, and art implementation territory.

**Worked example** (illustrative, using already-drafted narrative content
— exact hex/art remain `art-director` territory):

```
RegionManifestEntry {
  region_code: "candy_kingdom_hub"
  display_name: "Candy Kingdom Hub"
  stars_required_to_unlock: 0
  resident_character_id: "marzi"
  map_diorama_asset_id: "diorama_candy_kingdom_hub"
  level_sequence: ["candy_kingdom_hub-001", ..., "candy_kingdom_hub-010"]
}
```

**Phase 3 event-retheme seam — named, not designed.** Per Pillar 3, a
future season must be able to visibly transform a region. `art-bible.md`'s
Seasonal Event Overlay Rule already specifies the *mechanism's shape*: an
event swaps only the decorative-prop set and ambient-particle theme (the
two art-owned elements above) and injects one UI accent color, while the
gradient and trim stay stable ("the world redresses, the cast doesn't").
This document reserves — but does not add as a v1 schema field, does not
validate, and does not design the runtime behavior of — a future
region-level theme-override reference, owned by Events/Theming Engine
(#13, Phase 3) when that system is authored. See § Detailed Rules 8, Out
of Scope for v1.

### 5. Scroll/Camera Policy (Design-Level Rules Only)

This section specifies the **rules** the presentation layer (Game
UI/Screens Flow, Juice Layer) must honor. It does not specify tween
curves, input handling, or rendering — those are UX/implementation
territory.

- **Default position on map open.** The camera centers on the **current
  frontier node**: the first node with state `UNLOCKED` encountered when
  scanning region-by-region, then level-by-level, in manifest order. If
  every level in the furthest-unlocked region is `COMPLETED`, the frontier
  is that region's last node (or, if the next region is also already
  unlocked per Formula 3, the first node of that next region instead —
  the scan simply continues past a fully-completed region rather than
  stopping on it).
- **Free-scroll range.** The player may freely pan across: (a) every node
  in every currently-`UNLOCKED` region in full, plus (b) the next region
  **beyond** the furthest unlocked region, up to `LOCKED_REGION_PREVIEW_DEPTH`
  regions ahead (Tuning Knobs, default `1` — "unlocked + 1 preview," per
  the task's naming). Regions beyond that depth are not scrollable into at
  all — fully outside the pannable area, not merely visually hidden.
- **Locked-region visibility policy (the "curiosity" hook).** The
  region(s) within `LOCKED_REGION_PREVIEW_DEPTH` of the frontier are
  visible as a **silhouette teaser**: the region's diorama vignette
  renders desaturated/silhouetted, its `stars_required_to_unlock` value
  (or a "X more stars to unlock" derived readout) is shown, but its
  individual level nodes are **not** enumerated or rendered — there is no
  node graph to preview for content the player cannot yet enter. Regions
  beyond the preview depth are not visible in any form (no silhouette, no
  name) — this protects the "curiosity" retention hook (`game-concept.md`)
  from being spoiled by seeing the entire world's roster from level 1.
- **MVP degradation.** At MVP scope (a single region in the manifest), this
  entire policy is inert by construction — there is no next region to
  preview and no locked-region silhouette to render. No separate code path
  is needed for this case; it is simply what the rules above produce when
  `regions.size() == 1`.

### 6. Versioning & Migration Rules

Same philosophy as `level-data-format.md` §3 and `save-persistence.md` §8,
applied to `world_map_manifest.tres`:

| Change Type | Bump Required? | Reader Behavior |
|---|---|---|
| New **optional field** added, with a documented default | No | A file missing the field gets the default; ignored with a warning by a reader that doesn't yet recognize it. |
| New **required field**, existing field **removed/renamed**, or a field's **type/semantic meaning changed** | Yes | Not backward compatible — requires an ordered migration pass over the single `world_map_manifest.tres` file (only one file exists per build, unlike per-level files, so migration cost here is trivially small). |

`schema_version` is a single incrementing integer, never reused. Currently
supported set: `{1}`. A manifest whose `schema_version` is newer than the
running build supports fails loudly (excluded from load, logged as an
error) — never partially loaded — per the identical precedent
`level-data-format.md` and `save-persistence.md` both already established.

### 7. Validation Contract

A `world_map_manifest.tres` is **invalid** and must not be loadable if it
violates any **Blocking** rule below. **Advisory** rules are logged as
warnings only.

| ID | Rule | Severity |
|---|---|---|
| W1 | `schema_version` is present and is a value the running build supports (`{1}`). | Blocking |
| W2 | `regions` has at least 1 entry. | Blocking |
| W3 | Every `region_code` in `regions` is unique. | Blocking |
| W4 | Every `RegionManifestEntry.level_sequence` has at least `MIN_LEVELS_PER_REGION` entries (default 1). | Blocking |
| W5 | Every `level_id` string across every region's `level_sequence` is non-empty, and appears in exactly one place in the entire manifest — no duplicate within a region's own sequence, and no level assigned to two different regions. | Blocking |
| W6 | Every `level_id` referenced in any `level_sequence` also appears in `assets/data/level_manifest.tres`'s `entries` (Board Engine's manifest). | Blocking |
| W7 | Every entry in `assets/data/level_manifest.tres`'s `entries` appears in exactly one region's `level_sequence` in this manifest — no RNG-registered level is invisible to the map. | Blocking |
| W8 | For every `level_id` in a region's `level_sequence`, that level's own `.tres` file's `region` field (Level Data Format) equals this region's `region_code`. | Blocking |
| W9 | `stars_required_to_unlock` is non-decreasing across the `regions` array order (`regions[i].stars_required_to_unlock <= regions[i+1].stars_required_to_unlock`). | Blocking |
| W10 | `regions[0].stars_required_to_unlock == 0`. | Blocking |
| W11 | For every region at index `k > 0`, `stars_required_to_unlock <= 3 × Σ(level_count(regions[0..k-1]))` — the gate must be mathematically achievable using stars available from strictly earlier regions alone (Formula 1's `max_stars_before_region`). | Blocking |
| W12 | `resident_character_id` and `map_diorama_asset_id`, if non-empty, are syntactically well-formed identifier strings. This rule does **not** verify the referenced character/asset actually exists — that is a content-pipeline/QA smoke-check concern, mirroring `level-data-format.md`'s own precedent of not validating cross-domain asset existence at the schema layer. | Advisory |

### 8. Out of Scope for v1 (Deferred)

| Deferred Feature | Why Deferred | v2+ Extension Point |
|---|---|---|
| **Branching / non-linear node graphs** | No validated design need in any source document; contradicts `art-bible.md`'s already-locked single-ribbon-path spec (§ Detailed Rules 1). | Would require a new art direction pass and a DAG-based unlock model before any schema work begins. |
| **Region-level event theme-override reference** | Events/Theming Engine (#13) is Phase 3, explicitly deferred; its exact data needs are unknown until that system is authored. | Add an optional `active_theme_override_id` (or equivalent) field to `RegionManifestEntry` once #13 defines the override mechanism — additive, no bump needed per § Detailed Rules 6's policy. |
| **Region-level completion badge / trophy state** | No design requirement identified for a stored "region mastered" flag beyond the UI-computed `region_completion_pct == 100` case (§ Detailed Rules 1). | If a future need for a persisted region-trophy state emerges, it is an additive `LevelRecord`-adjacent or region-scoped Save & Persistence field, not a World Map manifest field. |
| **Orphaned-record pruning** | Resolved, not deferred — see Edge Cases: orphaned `level_records` entries are never pruned, matching `save-persistence.md`'s own existing precedent. Listed here only because `save-persistence.md`'s Open Questions table names World Map authoring as the point this gets decided. | N/A — decision is final as stated. |

---

## Formulas

### Formula 1 — Region Star Gate (Recommended Default)

**Named expression:**
```
max_stars_before_region(k) = 3 × Σ( level_count(regions[j]) for j in [0, k-1] )
stars_required_to_unlock(k) = round( REGION_GATE_PERCENT × max_stars_before_region(k) )   for k > 0
stars_required_to_unlock(0) = 0
```

| Symbol | Type | Range | Description |
|--------|------|-------|--------------|
| `k` | int | `[0, region_count-1]` | 0-based index of the region within the `regions` array. |
| `level_count(regions[j])` | int | `>= MIN_LEVELS_PER_REGION` | Number of entries in region `j`'s `level_sequence`. |
| `max_stars_before_region(k)` | int | `>= 0` | Maximum stars obtainable from every region strictly before region `k`. |
| `REGION_GATE_PERCENT` | float (tuning constant) | `[0, 1]`, default `0.6` | Recommended fraction of "stars available so far" required to open the next region. |
| `stars_required_to_unlock(k)` | int | `[0, max_stars_before_region(k)]` (hard ceiling enforced by Validation rule W11) | The **authored** field value stored in `RegionManifestEntry`. This formula produces the recommended value at content-authoring time; the actual stored value is data, per Pillar 2's "authored, inspectable levers" philosophy — a designer may deviate from the formula's recommendation per-region, subject only to W9 (non-decreasing) and W11 (mathematically achievable). |
| `round(x)` | function | — | Round to nearest integer; `.5` rounds up. |

**Output range**: `stars_required_to_unlock(k)` is bounded below by `0` and
above by `max_stars_before_region(k)` (Validation rule W11) — never a gate
that is mathematically unreachable, which would be a silent, permanent
soft-lock and a severe Pillar 2 violation.

**Worked example — Alpha/launch scope** (4 regions × 30 levels each, per
`game-concept.md`'s "Launch target: 4 regions × 30 levels (120 levels)",
`REGION_GATE_PERCENT = 0.6`):

```
Region 0 (candy_kingdom_hub, 30 levels):
  stars_required_to_unlock(0) = 0                                        (starting region)

Region 1 (frosted_peak, 30 levels):
  max_stars_before_region(1) = 3 × 30 = 90
  stars_required_to_unlock(1) = round(0.6 × 90) = 54

Region 2 (sundrop_grove, 30 levels):
  max_stars_before_region(2) = 3 × (30 + 30) = 180
  stars_required_to_unlock(2) = round(0.6 × 180) = 108

Region 3 (thistleberry_hollow, 30 levels):
  max_stars_before_region(3) = 3 × (30 + 30 + 30) = 270
  stars_required_to_unlock(3) = round(0.6 × 270) = 162
```

**Replay-incentive sanity check** (why `0.6` produces "healthy replay, not
grind"): a player who wins every level in region 0 exactly once, earning
the guaranteed minimum of 1 star each, has `30` stars — short of the
`54`-star gate into region 1. Reaching `54` from a `30`-star floor requires
raising `12` of the region's `30` levels from 1★ to 3★ (a `+2` gain each,
`12 × 2 = 24`, `30 + 24 = 54`) — a meaningful but partial replay ask (40%
of the region), not full mastery. A player who plays only moderately well
(a realistic ~2★ average across the region, `2 × 30 = 60` stars) clears the
gate through natural first-pass play with **zero** dedicated replay,
confirming the tuning delivers "replay is an invitation, not a
requirement" (Player Fantasy #4).

**Worked example — MVP scope** (1 region, 10 levels): `stars_required_to_
unlock(0) = 0`. `max_stars_before_region` is never computed for a second
region because none exists yet in the manifest — the gate mechanism is
present and correctly implemented, simply unexercised at MVP content
scope, exactly as `systems-index.md`'s Structural Decision #4 anticipates.

---

### Formula 2 — Level Unlock State (Derived)

**Named expression:**
```
level_state(L) =
    COMPLETED   if level_records[L.level_id] exists
    LOCKED      elif region_state(L.region_code) == LOCKED
    LOCKED      elif L.sequence_index > 0 AND level_records[prev(L)] does not exist
    UNLOCKED    otherwise
```

| Symbol | Type | Range | Description |
|--------|------|-------|--------------|
| `L` | node reference | — | One entry in some region's `level_sequence`, at a known `sequence_index`. |
| `L.level_id` | String | — | The level's stable id (Level Data Format). |
| `L.region_code` | String | — | The `region_code` of the `RegionManifestEntry` whose `level_sequence` contains `L`. |
| `L.sequence_index` | int | `[0, level_count(L's region)-1]` | `L`'s 0-based position within its own region's `level_sequence` — purely array-position-derived, not a stored field. |
| `prev(L)` | function | — | The `level_id` at `L.sequence_index - 1` within the same region's `level_sequence`; undefined when `L.sequence_index == 0`. |
| `level_records` | Dictionary | Save & Persistence's `level_records` field (`save-persistence.md` §2) | Key presence = the level has been won at least once. |
| `region_state(...)` | function | `{LOCKED, UNLOCKED}` | Formula 3's output. |
| `level_state(L)` | enum | `{LOCKED, UNLOCKED, COMPLETED}` | Result. |

**Output range**: Exactly one of 3 discrete states, resolved by strict
priority order (`COMPLETED` checked first, so a level that has ever been
won stays viewable/replayable even in an edge case where an upstream
region/sequence state would otherwise suggest `LOCKED` — see Edge Cases).

**Worked example**: region `candy_kingdom_hub`'s `level_sequence =
[L1..L10]`. Player's `level_records` contains entries for `L1` (3★), `L2`
(2★), `L3` (1★) only. Region state is `UNLOCKED` (region 0, gate `0`).
`level_state(L1) = level_state(L2) = level_state(L3) = COMPLETED`.
`level_state(L4)`: no record for `L4`, region unlocked, `sequence_index=3
> 0`, `prev(L4) = L3` exists in `level_records` → `UNLOCKED`.
`level_state(L5)` through `level_state(L10)`: no record, `prev` (`L4`
through `L9` respectively) does not exist in `level_records` → `LOCKED`.

---

### Formula 3 — Region Unlock State (Derived)

**Named expression:**
```
region_state(R) = UNLOCKED   if get_total_stars() >= R.stars_required_to_unlock
region_state(R) = LOCKED     otherwise
```

| Symbol | Type | Range | Description |
|--------|------|-------|--------------|
| `R` | region reference | — | One entry in `world_map_manifest.tres`'s `regions` array. |
| `R.stars_required_to_unlock` | int | `>= 0` | Formula 1's authored output for this region. |
| `get_total_stars()` | function | `int >= 0` | Save & Persistence's existing API (`save-persistence.md` §11) — an already-maintained, self-healing aggregate. No new read path is introduced. |
| `region_state(R)` | enum | `{LOCKED, UNLOCKED}` | Result. |

**Output range**: Boolean-equivalent gate. `regions[0]` is always
`UNLOCKED` (its gate is `0` and `get_total_stars() >= 0` is trivially
always true).

**Worked example**: `get_total_stars() = 40`. Region 0
(`candy_kingdom_hub`, gate `0`) → `UNLOCKED`. Region 1 (`frosted_peak`,
gate `54`) → `40 < 54` → `LOCKED`.

**Sequential-access proof (why no separate "previous region unlocked"
check is needed)**: because Validation rule W9 enforces
`stars_required_to_unlock` is non-decreasing across `regions`, and a
single `get_total_stars()` value is compared against every region's gate,
`region_state(regions[k+1]) == UNLOCKED` implies
`get_total_stars() >= stars_required_to_unlock(k+1) >=
stars_required_to_unlock(k)`, which in turn implies
`region_state(regions[k]) == UNLOCKED`. Region `k+1` can never be unlocked
while region `k` is locked — sequential access is a mathematical
consequence of Formula 3 plus W9, not a second rule.

---

### Formula 4 — Display Number Derivation (Supersedes Level Data Format's Authored Field)

**Named expression:**
```
display_number(L) = 1 + Σ( level_count(regions[0..region_index(L)-1]) ) + L.sequence_index
```

| Symbol | Type | Range | Description |
|--------|------|-------|--------------|
| `L` | node reference | — | As in Formula 2. |
| `region_index(L)` | int | `[0, region_count-1]` | 0-based position of `L`'s region within the `regions` array — array-position-derived. |
| `level_count(regions[0..region_index(L)-1])` | int | `>= 0` | Sum of `level_sequence` lengths for every region strictly before `L`'s own region. |
| `L.sequence_index` | int | `[0, level_count(L's region)-1]` | As in Formula 2. |
| `display_number(L)` | int | `[1, total_level_count]` | The authoritative, player-facing level number — globally unique, strictly increasing along the manifest's full concatenated order. |

**Output range**: Bounded, `[1, total_level_count]` (`total_level_count`
being the sum of every region's `level_sequence` length across the entire
manifest — `120` at full Alpha/launch scope, `10` at MVP scope). This value
supersedes Level Data Format's authored `display_number` field as the
value UI must display for any level present in `world_map_manifest.tres`
(§ Detailed Rules 2's resolution of `level-data-format.md`'s Open
Question).

**Worked example**: region order `[candy_kingdom_hub (30 levels),
frosted_peak (30), sundrop_grove (30), thistleberry_hollow (30)]`. A level
at `frosted_peak`, `sequence_index = 4` (the 5th level in that region):
`display_number = 1 + 30 + 4 = 35`.

---

### Formula 5 — Region Completion Percentage

**Named expression:**
```
region_completion_pct(R) = 100 × ( Σ best_stars(L) for L in R.level_sequence ) / ( 3 × level_count(R) )
```

| Symbol | Type | Range | Description |
|--------|------|-------|--------------|
| `R` | region reference | — | As in Formula 3. |
| `best_stars(L)` | int | `[0, 3]` | `level_records[L.level_id].best_stars` if a record exists, else `0` (no record = 0 stars for display purposes). |
| `level_count(R)` | int | `>= MIN_LEVELS_PER_REGION` | Number of levels in `R.level_sequence`. |
| `region_completion_pct(R)` | float | `[0, 100]` | Percent of this region's maximum obtainable stars the player has earned. Rounded to the nearest integer for display. |

**Output range**: `[0, 100]`, not clamped beyond natural bounds — the
numerator can never exceed the denominator by construction, since
`best_stars` is itself clamped to `[0,3]` by Save & Persistence's own
validation (`save-persistence.md` S6).

**Worked example**: a 10-level region where the summed `best_stars` across
all 10 levels is `24`: `region_completion_pct = 100 × 24 / (3 × 10) =
100 × 24/30 = 80%`.

---

### Formula 6 — Overall World Completion Percentage

**Named expression:**
```
world_completion_pct = 100 × get_total_stars() / ( 3 × Σ level_count(regions[all]) )
profile_fully_completed = ( world_completion_pct == 100 )
```

| Symbol | Type | Range | Description |
|--------|------|-------|--------------|
| `get_total_stars()` | function | `int >= 0` | As in Formula 3. |
| `Σ level_count(regions[all])` | int | `>= MIN_LEVELS_PER_REGION` | Total levels across every region in the manifest. |
| `world_completion_pct` | float | `[0, 100]` | Percent of every obtainable star, world-wide, the player has earned. |
| `profile_fully_completed` | bool | `{true, false}` | Derived flag, `true` only when every level in the entire manifest has been three-starred. |

**Output range**: `[0, 100]`, same clamping guarantee as Formula 5.
`profile_fully_completed` is a pure boolean derivation — never stored,
recomputed on demand — consumed by the completionist-end-state edge case
below.

**Worked example** (full Alpha/launch scope, `120` levels, max `360`
stars, matching `save-persistence.md` Formula 3's own stated range):
`get_total_stars() = 180` → `world_completion_pct = 100 × 180/360 = 50%`,
`profile_fully_completed = false`. If `get_total_stars() = 360`:
`world_completion_pct = 100`, `profile_fully_completed = true`.

---

## Edge Cases

| Scenario | Expected Behavior | Rationale |
|----------|--------------------|-----------|
| First-ever launch (`level_records = {}`, `total_stars = 0`) | Region 0 is `UNLOCKED` (gate `0`), its first level is `UNLOCKED`, every other level and every other region is `LOCKED`. | The base case of Formulas 2–3, not a special branch — stated explicitly for clarity. |
| A save from a newer app version references a `region_code` (via an orphaned `level_records` entry) that the current, older build's `world_map_manifest.tres` does not define | The orphaned entry's stars still count toward `total_stars` (Save & Persistence's own aggregate, unaffected by manifest content); no level or region referencing it is enumerated on the map, since map UI iterates the *current* manifest, never the save file. No crash, no data loss, no notice shown. | Matches `save-persistence.md`'s existing "orphaned record retained, not shown" edge case exactly — this document adds no new handling, only confirms compatibility with a scenario that document already covers (e.g., a player downgrading a build, or a region temporarily removed in a content update). |
| A level is removed from `world_map_manifest.tres`'s `level_sequence` in a content update | The level's `LevelRecord` (if any) is retained in the save file, orphaned, still counting toward `total_stars` — per `save-persistence.md`'s existing precedent, never actively pruned (this document's resolution of that document's Open Question, § Detailed Rules 8). The level simply no longer appears on the map. | Consistent with Player Fantasy guarantee "stars are permanent trophies" — a retired level's earned stars are never revoked. |
| `world_map_manifest.tres`'s `regions` array or a region's `level_sequence` is **reordered** in a content update (levels re-sequenced, a new level inserted mid-region) | **No save migration is required at all.** Because every node/region state (Formulas 2–3) and the authoritative display number (Formula 4) are recomputed live from the manifest's *current* order on every load, a reorder simply produces different derived results on the very next map load — with zero stored booleans to reconcile. A level inserted before a player's frontier is automatically `COMPLETED` (if a record already exists for its `level_id`) or `LOCKED` (if not), with no special-cased migration code needed anywhere. | This is the direct payoff of the "derive everything, store nothing" design (§ Detailed Rules 3) — the single largest practical advantage over a design that stored unlock booleans, which would require an explicit migration pass on every manifest edit. |
| A `level_id` is "renumbered" (its string identity changed) between versions | Not a supported scenario — Level Data Format's own contract states `level_id` is "Never reused, even if a level is later removed" (`level-data-format.md` §2). The closest real scenario is retirement (see the "level removed" row above) followed by authoring a brand-new `level_id` for its replacement content, inserted at the desired map position. | This document does not need its own renumbering rule because the upstream contract already forbids the underlying event. |
| All levels in the entire manifest are three-starred | `profile_fully_completed = true` (Formula 6) is exposed as a derived flag for Game UI/Screens Flow to trigger a completionist celebration. Exact copy/visual treatment is narrative/UX content, not specified by this document — only the trigger condition and its boolean derivation are owned here. | Player Fantasy's "achievable mastery" promise has an honest end state; the game does not pretend there is always more to chase once a player has genuinely earned everything. |
| A `level_sequence` entry's `level_id` is absent from `assets/data/level_manifest.tres` (Board Engine's manifest) | Fails Validation rule W6 — the manifest is invalid, build/CI fails, never partially loaded. | Mirrors Level Data Format's and Board Engine's shared "load fails loudly, never partially" philosophy — a world-map node the player could tap but Board Engine cannot bootstrap would be a broken, uncompletable entry point. |
| An authored, RNG-registered level (present in `level_manifest.tres`) is never assigned to any region's `level_sequence` | Fails Validation rule W7 — the manifest is invalid. | An authored level with no map entry point is permanently unreachable by any player — a silent content-authoring mistake this rule catches at build time rather than discovering it live. |
| A designer authors `stars_required_to_unlock` for a region higher than `max_stars_before_region` (Formula 1) allows | Fails Validation rule W11 — the manifest is invalid. | An unreachable gate is a permanent, invisible soft-lock — the single worst possible outcome for a Pillar-2 game; caught at content-authoring time, never discovered by a stuck player. |
| MVP-scope build: the manifest contains exactly one region | Region-gate math (Formula 1/3), sequential cross-region access (Formula 3's proof), and the locked-region-preview scroll policy (§ Detailed Rules 5) all degrade gracefully to "nothing to gate into, nothing to preview" with no separate code path — this is simply what the general-purpose rules produce at `regions.size() == 1`. | Directly satisfies the task's design goal: "design the full system now, content-gate later" — the same manifest schema and formulas serve MVP's 1-region cut and Alpha's 4-region roster without modification. |
| A level's `.tres` file `region` field does not match the `region_code` of whichever region's `level_sequence` actually contains it | Fails Validation rule W8 — the manifest (or the level file) is invalid; the mismatch is a Blocking, build-time content-authoring error, never silently reconciled in either direction. | Two independently-authored copies of "which region is this level in" must never be allowed to silently drift — exactly the failure mode `level-data-format.md`'s Open Question flagged as a risk, resolved here as a hard, mechanical check. |

---

## Dependencies

| System | Direction | Nature of Dependency |
|--------|-----------|----------------------|
| Level Data Format (`design/gdd/level-data-format.md`, APPROVED) | World Map depends on it | Reads `level_id` (join key), `region` (validated against the manifest's registry, Validation rule W8), and the now-superseded `display_number` (kept as an authoring convenience only, § Detailed Rules 2). |
| Save & Persistence (`design/gdd/save-persistence.md`, Draft) | World Map depends on it | Reads `level_records` (presence = completed) and calls `get_profile()`/`get_total_stars()` to derive every node/region state (Formulas 2–3). **Zero writes** — World Map never calls `record_level_completion()` or any mutating API; that remains exclusively Level Objective & Move-Limit System's responsibility. **Zero schema changes required** — the existing shape already serves every read this document needs. |
| Match-3 Board Engine (`design/gdd/board-engine.md`, NEEDS REVISION) | World Map depends on it (validation only, no runtime dependency) | Cross-validates its owned `assets/data/level_manifest.tres` against this document's `world_map_manifest.tres` (Validation rules W6–W7, § Detailed Rules 2). World Map never reads Board Engine's live/runtime state — only its static manifest file, at build/CI time. |
| Scoring & Star Thresholds (`design/gdd/scoring-stars.md`, #6, not yet authored) | World Map depends on it (soft, forward, indirect) | World Map consumes only the already-persisted `best_stars` (0–3) integer this system will eventually compute — never its internal formula. See § Detailed Rules 3's "Scoring surface boundary" for the declared seam covering any future additional per-level metadata. **Reciprocal note** (per `design/CLAUDE.md`'s bidirectionality rule): when authored, its Dependencies section should note that World Map is a downstream consumer of its output, not of its formula. |
| Level Objective & Move-Limit System (`design/gdd/level-objectives.md`, #7, not yet authored) | World Map depends on it (soft, forward, indirect) | The system that actually triggers `record_level_completion()` on a win. World Map has no direct dependency on it — only on the resulting Save & Persistence state — but is listed for completeness of the data-provenance chain. |
| Game UI/Screens Flow (`design/gdd/screen-flow.md`, #10, not yet authored) | Screen Flow depends on this document | Expected to query this document's derived states (Formulas 2, 3, 5, 6) to render the map screen, gate navigation into a level/region, and surface `profile_fully_completed`. **Reciprocal note**: its Dependencies section must list this document when authored. |
| Events/Theming Engine (`design/gdd/events-theming.md`, #13, Phase 3) | Events depends on this document | Anticipated consumer of the named-but-undesigned region-level event-retheme seam (§ Detailed Rules 4, 8). **Reciprocal note**: its Dependencies section must list this document when authored. |
| `design/art/art-bible.md` (not a `design/gdd/` system) | World Map depends on it (constants + rules only) | Supplies the "one path per region, gated sequentially" World Map spec (justifying § Detailed Rules 1's linear decision), the Regional Modularity 4-element theming system (§ Detailed Rules 4), and the Seasonal Event Overlay Rule the Phase 3 seam is named after. |
| `design/narrative/characters-and-tone.md` (not a `design/gdd/` system) | World Map depends on it (constants + rules only) | Supplies the resident character roster, region personality mood, and the Fizz map-placement rule (§ Detailed Rules 4). |
| `design/gdd/game-concept.md` (not a `design/gdd/` system in the systems-index sense, but a foundational document) | World Map depends on it (pillar/scope constants) | Supplies the no-monetization/no-pay-gate anti-pillar constraint (Player Fantasy #2, § Detailed Rules 3), the 4-region/120-level Alpha target used in Formula 1's worked example, and the "curiosity" retention hook § Detailed Rules 5 is built to serve. |

---

## Tuning Knobs

| Parameter | Current Value | Safe Range | Effect of Increase | Effect of Decrease |
|-----------|--------------|------------|---------------------|----------------------|
| `REGION_GATE_PERCENT` | `0.6` | `0.4 – 0.8` | Raises every region's star gate (Formula 1), demanding more replay/higher average star quality before the next region opens — pushed too high, it risks reading as grind rather than invitation. | Lowers every gate, letting players advance with lower average star quality — pushed too low, it risks the gate becoming meaningless (effectively unlocked after a single clean pass), undermining the "mastery" retention hook. |
| `MIN_LEVELS_PER_REGION` | `1` | `1 – 30` | A higher floor forces every region (including any future small/bonus region) to have substantial content, reducing content-planning flexibility for a deliberately small showcase region. | A floor below `1` is meaningless (an empty region has nothing to gate or display); `1` is the structural minimum. |
| `LOCKED_REGION_PREVIEW_DEPTH` | `1` | `0 – 2` | Previewing more locked regions ahead gives players more to look forward to at once, but risks diluting the "one specific next place" curiosity hook into a diffuse, less exciting preview of everything at once. | `0` removes the silhouette teaser entirely — a player would see nothing about the next region until it unlocks, forgoing the curiosity retention hook (`game-concept.md`) entirely. |
| Region-level content-planning target (`level_count` per region) | MVP: `10` (region 0 only); Alpha/launch: `30` per region (`game-concept.md`: "4 regions × 30 levels") | Not schema-enforced (any value `>= MIN_LEVELS_PER_REGION` is legal); this row records the current content-planning intent, not a validated constant | A larger region gives more room for the region's onboarding/difficulty arc, at higher authoring cost (per-level, hand-authored per `level-data-format.md`). | A smaller region reaches its own star gate faster but compresses difficulty pacing within it. |

---

## Acceptance Criteria

All BLOCKING per `coding-standards.md`'s Logic-tier rule (formulas, derived
state); tests live under `tests/unit/world-map/`, deterministic, no live
file I/O beyond loading fixture `.tres`/JSON resources.

**Level unlock state (Formula 2)**

- [ ] `test_level_locked_before_previous_completed`: a level whose
      `prev(L)` has no `level_records` entry resolves to `LOCKED`.
- [ ] `test_level_unlocked_when_previous_completed`: a level whose
      `prev(L)` has a `level_records` entry (any `best_stars` 1–3)
      resolves to `UNLOCKED`.
- [ ] `test_level_completed_when_record_exists`: a level with its own
      `level_records` entry resolves to `COMPLETED` regardless of its
      region or sequence state, reproducing Formula 2's worked example
      exactly (`L1`–`L3` completed, `L4` unlocked, `L5`–`L10` locked).
- [ ] `test_first_level_in_region_unlocked_when_region_unlocked_and_no_record`:
      `sequence_index == 0` with no prior record resolves to `UNLOCKED`
      whenever its region is `UNLOCKED`.
- [ ] `test_level_locked_when_region_locked_regardless_of_sequence`: every
      level in a `LOCKED` region resolves to `LOCKED`, even a level whose
      own `prev(L)` has a completion record (defensive — should not occur
      given W9's monotonicity, but the resolution order must hold anyway).

**Region unlock state (Formula 3)**

- [ ] `test_region_locked_below_gate`: `get_total_stars() < stars_required_to_unlock` resolves `LOCKED`.
- [ ] `test_region_unlocked_at_or_above_gate`: `get_total_stars() >= stars_required_to_unlock` resolves `UNLOCKED`, including the exact-equality boundary case.
- [ ] `test_first_region_always_unlocked`: `regions[0]` resolves `UNLOCKED` at `get_total_stars() == 0`.
- [ ] `test_sequential_access_proof`: given a manifest satisfying W9/W10, no synthetic `get_total_stars()` value ever produces `region_state(regions[k+1]) == UNLOCKED` while `region_state(regions[k]) == LOCKED`, checked exhaustively across a 4-region fixture's gate boundaries.

**Star gate formula (Formula 1)**

- [ ] `test_region_gate_matches_worked_example`: the 4-region, 30-levels-each fixture with `REGION_GATE_PERCENT = 0.6` reproduces `0, 54, 108, 162` exactly.
- [ ] `test_gate_ceiling_validation_rejects_unreachable_gate`: a fixture with `stars_required_to_unlock` exceeding `max_stars_before_region` fails Validation rule W11.
- [ ] `test_gate_non_decreasing_validation`: a fixture with a decreasing gate sequence fails Validation rule W9.

**Display number (Formula 4)**

- [ ] `test_display_number_matches_worked_example`: the 4-region, 30-levels-each fixture reproduces `display_number = 35` for `frosted_peak`'s 5th level (`sequence_index = 4`) exactly.
- [ ] `test_display_number_globally_unique_and_sequential`: across a full fixture manifest, `display_number` values are unique and strictly increasing in manifest order with no gaps, from `1` to `total_level_count`.
- [ ] `test_display_number_recomputes_after_reorder`: reordering a fixture's `regions` array and reloading produces updated `display_number` values with zero special-cased migration code invoked.

**Completion percentages (Formulas 5–6)**

- [ ] `test_region_completion_percent_matches_worked_example`: a 10-level region fixture with summed `best_stars = 24` reproduces `80%` exactly.
- [ ] `test_world_completion_percent_matches_worked_example`: a 120-level fixture with `get_total_stars() = 180` reproduces `50%` exactly.
- [ ] `test_profile_fully_completed_true_at_max_stars`: `get_total_stars()` equal to `3 × total_level_count` sets `profile_fully_completed = true`.
- [ ] `test_profile_fully_completed_false_below_max`: any value below the maximum sets `profile_fully_completed = false`.

**Manifest validation (W1–W12)**

- [ ] `test_w6_orphaned_level_sequence_entry_rejected`: a `level_id` present in `level_sequence` but absent from `level_manifest.tres` fails W6.
- [ ] `test_w7_orphaned_board_engine_entry_rejected`: a `level_id` present in `level_manifest.tres` but absent from every region's `level_sequence` fails W7.
- [ ] `test_w5_duplicate_level_id_across_regions_rejected`: the same `level_id` appearing in two different regions' `level_sequence` fails W5.
- [ ] `test_w8_region_field_mismatch_rejected`: a level `.tres` file's `region` field differing from its assigned region's `region_code` fails W8.
- [ ] `test_w3_duplicate_region_code_rejected`: two `RegionManifestEntry` entries sharing a `region_code` fail W3.
- [ ] `test_w4_empty_level_sequence_rejected`: a region with zero `level_sequence` entries fails W4.
- [ ] `test_real_manifest_zero_blocking_failures`: the actual `assets/data/world_map_manifest.tres` (MVP: 1 region, 10 levels) passes every Blocking rule W1–W11 with zero failures.

**Manual walkthrough items** (per `coding-standards.md`'s UI/Integration
tier — documented playtest, `production/qa/evidence/`, ADVISORY):

- [ ] Fresh install: only `candy_kingdom_hub`'s first level shows
      `UNLOCKED`; every other level in the region shows `LOCKED`; no
      second region is visible or referenced anywhere in the UI at MVP
      scope.
- [ ] Completing the first level visibly unlocks exactly the second level
      on the next map view, with no other node changing state.
- [ ] (Alpha-scope fixture) Reaching a region's star-gate threshold
      visibly unlocks that region's first level on the next map view; the
      region no longer renders as a locked silhouette.
- [ ] (Alpha-scope fixture) The next locked region beyond the current
      frontier renders as a silhouette teaser with a visible "stars
      needed" readout, but shows no individual level nodes; a second
      locked region beyond that is not visible or scrollable-into at all.
- [ ] (Alpha-scope fixture) Reaching `profile_fully_completed = true`
      (every level three-starred) triggers a distinguishable completionist
      state in the UI, not identical to a normal "region unlocked" state.
- [ ] No gameplay value defined by this document (`REGION_GATE_PERCENT`,
      `MIN_LEVELS_PER_REGION`, `LOCKED_REGION_PREVIEW_DEPTH`, any
      `stars_required_to_unlock`) exists as a hardcoded literal anywhere in
      `src/` — every instance is spot-check traceable back to
      `world_map_manifest.tres` or its documented tuning source, per
      `coding-standards.md`'s data-driven rule.

---

## Cross-References

| This Document References | Target GDD | Specific Element Referenced | Nature |
|--------------------------|-----------|----------------------------|--------|
| `level_id`, `region`, `display_number` field shapes | `design/gdd/level-data-format.md` | Schema v1, §2 | Data dependency; this document resolves that document's two flagged Open Questions (§ Detailed Rules 2) |
| `level_manifest.tres` (Board Engine's RNG-ordinal manifest) | `design/gdd/board-engine.md` | §2, Formula 1 | Cross-validated, not merged (§ Detailed Rules 2) — recommends (does not make) a reciprocal follow-up note in that document |
| `level_records`, `get_total_stars()`, `LevelRecord.best_stars` | `design/gdd/save-persistence.md` | §2, §11, Formula 3 | Read-only data dependency; resolves that document's orphaned-record-pruning Open Question (§ Detailed Rules 8) |
| "One path per region, gated sequentially"; Regional Modularity 4-element theming system; World Map spec (node markers, diorama vignette) | `design/art/art-bible.md` | World Map section; Regional & Seasonal Palette System; Regional Modularity | Justifies § Detailed Rules 1's linear decision and § Detailed Rules 4's theming slot contract |
| Resident character roster; Fizz's map placement rule; region personality | `design/narrative/characters-and-tone.md` | §2 (Fizz), §3 (Region Personalities) | Justifies § Detailed Rules 4's narrative mount points |
| No-pay-gate anti-pillar; 4-region/120-level Alpha scope target; curiosity retention hook | `design/gdd/game-concept.md` | Anti-Pillars; Scope Tiers table; Retention Hooks | Justifies § Detailed Rules 3's pay-gate prohibition, Formula 1's worked-example scope, and § Detailed Rules 5's silhouette-teaser policy |

---

## Open Questions

| Question | Owner | Deadline | Resolution |
|----------|-------|----------|-----------|
| Should `board-engine.md`'s Acceptance Criteria / Cross-References formally add a reciprocal cross-manifest validation test entry (mirroring this document's W6–W7), rather than that obligation living only in this document's test suite? | systems-designer | At next `board-engine.md` review pass | — |
| Should `level-data-format.md`'s Open Questions table be updated to record this document's resolution of the `region`/`display_number` ownership questions (§ Detailed Rules 2)? | systems-designer | At next `level-data-format.md` review pass | — |
| Is `REGION_GATE_PERCENT = 0.6` the right default once real playtest data exists (Vertical Slice / early Alpha), or should it be tuned per-region rather than as one global constant applied uniformly? | game-designer | At Vertical Slice, once real 30-level region play data exists | — |
| Should a future, clearly-scoped alternate-path mode (e.g., a post-launch "hard mode" alternate route) ever revisit the strictly-linear decision (§ Detailed Rules 1), or should branching remain permanently out of scope for this game? | game-designer / creative-director | Post-launch, only if a concrete design need emerges | — |
| Once Events/Theming Engine (#13) is authored, does the named-but-undesigned region-level theme-override seam (§ Detailed Rules 4, 8) require a `schema_version` bump to `world_map_manifest.tres`, or does it qualify as a purely additive optional field under this document's existing versioning policy (§ Detailed Rules 6)? | systems-designer | At `events-theming.md` (#13) authoring | — |
| Should `MIN_LEVELS_PER_REGION`'s safe range upper bound (currently `30`, matching the launch content-planning target) be revisited once real Alpha-scope region content exists, to confirm `30` levels per region is still the intended ceiling rather than merely today's planning assumption? | game-designer | At Alpha content planning | — |
