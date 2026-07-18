# RNG Service

*Status: Reviewed — APPROVED (design-review lean, 2026-07-18) — see `design/gdd/reviews/rng-service-review-log.md`*

> **Layer**: Foundation · **Priority**: MVP · **Phase**: MVP · **Category**: Core
> **Author**: systems-designer · **Last Updated**: 2026-07-17
> **Implements Pillar**: Pillar 2 — Clever, Never Cheated
> **Depends On**: None (Foundation layer, zero design-system dependencies — confirmed in `design/gdd/systems-index.md`)
> **Depended On By**: Match-3 Board Engine, Special Candies & Combo Matrix (MVP, active); Booster Brewing Meta (Phase 2, forward dependency), Events/Theming Engine (Phase 3, forward dependency)

---

## Overview

The RNG Service is a Foundation-layer utility that provides deterministic,
seedable, stream-isolated randomness to every system in Sweet Cascade that
needs unpredictable-but-reproducible values — starting with the Match-3 Board
Engine's candy refill, and reserved in advance for future special-candy,
ingredient-harvest, and event-reward randomness. It exists to satisfy two hard
requirements at once: `technical-preferences.md`'s determinism rule ("All
board RNG must be seedable so match/cascade tests are reproducible") and
Pillar 2, *Clever, Never Cheated* (randomness must be honest — visibly and
provably free of hidden difficulty manipulation). Rather than one global
random-number generator, the service exposes independently-seeded **named
streams** so that consuming one stream (e.g., `board-refill`) can never
perturb another (e.g., a future `harvest` roll) — every feature's randomness
stays isolated, testable in isolation, and fully reproducible from a single
master seed.

## Player Fantasy

The RNG Service is invisible infrastructure — the player never opens a menu
that says "RNG Service." What they experience instead is the *effect* of its
guarantees, every single time a candy drops into an empty cell or a cascade
resolves. This system exists to protect one feeling above all: **trust**.
When a player loses a level, it should be because they ran out of good moves
— never because they suspect the game secretly stacked the deck against them.
Concretely, this system upholds three player-facing guarantees:

1. **Honest odds.** Every active candy color has exactly the same chance to
   appear on refill, every time, for every player — no hidden favoritism
   toward or against any individual player based on their streak, spend, or
   how badly they're losing. What you see is genuinely what you get.
2. **Provable fairness.** If a player (or the studio, investigating a
   complaint) ever suspects a specific sequence "felt wrong," that exact
   board sequence can be pulled from a logged seed and independently
   replayed — nothing about the board's history is unknowable or
   unfalsifiable.
3. **A level playing field on shared content.** Every player who plays
   today's daily challenge faces the *identical* board sequence. The
   leaderboard measures who read the same puzzle best, not who got luckier
   RNG.

This is the mechanical backbone of Pillar 2's design test: *"If a difficulty
mechanic works by invisibly manipulating the board against the player, we cut
it."* The RNG Service is the system that makes that promise structurally true
rather than just a marketing claim — its API has no input for "how is this
player doing," so that kind of manipulation isn't just discouraged, it's
architecturally impossible to build without visibly adding a new parameter
that would show up in code review and in this document's Honest Randomness
Contract (see Detailed Rules §4).

## Detailed Rules

### 1. Stream Model & Isolation Guarantee

- All randomness is drawn from **named streams**, never from one shared
  global generator. Each stream has its own independent internal state.
- **Isolation guarantee**: consuming N values from stream A never changes
  what stream B produces next, regardless of interleaving order or how many
  draws A has consumed. This is the property every determinism-dependent
  test in `tests/unit/` relies on, and it is non-negotiable — any
  implementation that shares mutable state between streams violates this
  contract.
- A **session** = one level-attempt playthrough or one daily-challenge
  playthrough. Starting a new session fully re-derives every stream's state
  from that session's master seed (see §3). No state carries over between
  sessions — a fresh `start_*_session()` call is a hard reset.
- Streams are cheap: each is a small state value, not a heavyweight object.
  There is no meaningful cost to reserving a stream before it has a
  consumer.

### 2. Stream Registry

Streams are identified by a fixed, versioned, **append-only** table mapping
each stream name to a small integer `stream_id`. IDs are assigned once and
never reused or renumbered — even if a stream is later deprecated — because
`stream_id` feeds Formula F3 (Stream Sub-Seed Derivation), and renumbering
would silently change every other stream's derived sequence.

| stream_id | stream_name | Owning / Consuming System | Status |
|---|---|---|---|
| 1 | `board-refill` | Match-3 Board Engine | Active (MVP) — gravity/refill candy color selection, no-valid-moves reshuffle |
| 2 | `special-drop` | Special Candies & Combo Matrix | Reserved (MVP infra ready) — this document reserves the stream; `special-candies.md` owns what it draws and why |
| 3 | `harvest` | Booster Brewing Meta | Reserved (Phase 2, not yet designed) |
| 4 | `events` | Events/Theming Engine | Reserved (Phase 3, not yet designed) |

Every registered stream gets a valid, derived sub-seed at every session
start (per Formula F3) regardless of whether anything consumes it yet — this
costs nothing and keeps ID numbering stable for when a future system defines
its first real consumer.

For randomness that doesn't warrant a permanent registry entry (a one-off,
feature-local need), a system may call `fork_stream()` (§6) instead of
requesting a new row in this table.

### 3. Seed Lifecycle

Every session begins with a **master seed**, derived one of three ways:

1. **Standard level attempt** — the caller (Match-3 Board Engine, at level
   load) supplies `level_id` and `attempt_number`. `attempt_number` is a
   simple in-memory counter starting at 1 each time the player enters the
   level from the map, incremented by 1 on each retry within that session.
   RNG Service does not track or persist `attempt_number` itself — ownership
   stays entirely with the calling system, which is what keeps RNG Service
   dependency-free per `systems-index.md`. `master_seed = F1(level_id,
   attempt_number)`.
2. **Daily / shared-seed challenge** — the caller supplies
   `daily_challenge_id` and `calendar_date_utc` (a UTC day count, not a
   player-specific value). `master_seed = F2(daily_challenge_id,
   calendar_date_utc)`. This formula takes **no player-identifying or
   player-performance input** by design — see the Honest Randomness Contract
   below.
3. **Test injection** — gdUnit4 tests call `start_test_session(master_seed)`
   with a literal value, bypassing F1/F2 entirely, for full deterministic
   control over test fixtures.

Once `master_seed` exists, every stream in the registry (§2) derives its own
independent sub-seed via Formula F3 — this is what makes stream isolation
hold even though all streams ultimately trace back to one master value.

**`level_id` must be an integer**, not a raw string. If Level Data Format
authors level identifiers as strings (e.g., `"region1_level07"`), they must
be resolved to a fixed, versioned ordinal integer via a level manifest before
reaching this service — never through a runtime string-hash function, whose
output stability across Godot's iOS/Android/Web export targets is not
guaranteed. `calendar_date_utc` avoids this problem entirely by being a plain
integer day-count (e.g., days since a fixed epoch), never a hashed date
string.

**Bug-repro logging.** At the start of every session, RNG Service writes a
session log record containing exactly these fields:

| Field | Example | Purpose |
|---|---|---|
| `level_id` or `daily_challenge_id` | `1007` | Identifies what was played |
| `attempt_number` or `calendar_date_utc` | `3` | Identifies which instance |
| `master_seed` | `1547274724` | The value needed to replay the exact sequence |
| `algorithm_version` | `"v1"` | Which mixing-function version produced this seed (see Edge Cases) |
| `session_start_timestamp` | ISO 8601 UTC | When the session began |

This record is retrievable via `get_session_log()` (§6) for attachment to bug
reports, and is sufficient on its own to exactly reproduce the session's
entire draw sequence in a test.

**No version salt.** `master_seed` derivation does not fold in a
game-build/content version. Cross-build seed reproducibility is not
guaranteed or required — if content changes between builds, a logged seed
from an old build simply isn't expected to replay meaningfully against new
level data. Build version, if needed for a bug report, is captured by
standard app logging, which is outside this service's scope.

### 4. Honest Randomness Contract (Pillar 2)

This is the enforceable version of Pillar 2's design test, scoped to what
RNG Service specifically guarantees and forbids.

**Forbidden — RNG Service will never do this, and no caller may build this
behavior on top of it without triggering a design-review failure:**

- Reading any player skill, performance, or session-state signal (win
  streak, remaining lives, retry count, time-in-level, purchase history) as
  an input to any draw's probability. The API surface has no parameter for
  any of this (see §6) — it is architecturally excluded, not just
  discouraged.
- Any "rubber-band" logic that silently raises or lowers the odds of a
  favorable outcome (a needed color, a special-candy spawn) based on how the
  player is doing.
- Pity timers or bias mechanics that alter draw probability without a
  visible, player-facing indicator of their existence and current state.
- Per-player or per-cohort probability variation applied silently (e.g.,
  different odds for payers vs. non-payers) — this would violate both
  Pillar 2 and the game's explicit Anti-Pillar, "NOT pay-to-win."

**Allowed — declared, level-config-driven variation that stays visible by
design:**

- Level Data Format constraining *which* colors are in the active pool for a
  given level (e.g., a level authored with 4 of 5 colors instead of 5). This
  is transparent because it's baked into the level's visible design, not
  hidden per-player logic — every player on that level sees the same
  constrained pool.
- A future, *explicitly declared* mechanic (e.g., "guaranteed special every
  15 moves without one") — if and when game-designer/creative-director
  choose to add one, it must be specified as a deterministic, player-visible
  counter-based rule in its *owning* system's GDD (not this one), with its
  own UI representation (a pip, a meter) so it reads as a stated rule, not a
  hidden rig. RNG Service supplies raw draws; it never implements adaptive
  weighting itself.
- Difficulty coming from visible level design — blockers, move limits,
  objective targets — exactly per Pillar 2's design test. Never from
  invisible RNG weighting.

### 5. Refill Distribution Rules

- Candy refill draws are **uniform** over the level's active color pool:
  every active color has probability `1 / N` where `N` is the pool size for
  that level. No color is weighted up or down by scarcity on the current
  board, cascade state, or any other run-time signal.
- The active color pool itself is **entirely owned by Level Data Format**
  (its `color_pool` field, schema v1, rule V11 — see Dependencies).
  RNG Service does not hardcode a color list; it receives `active_colors` as
  a caller-supplied parameter to `next_color()` (§6). This keeps the
  "5 colors at launch" value a *content* decision, not an RNG Service
  constant.
- **Launch default: 5 active colors.** Per `prototypes/sweet-cascade-concept/REPORT.md`,
  5 colors on an 8×8 board produced frequent, legible cascades in design
  analysis; 6 felt too sparse and was flagged as worth an A/B test later.
  This value lives in Tuning Knobs below and is confirmed/owned by
  `level-data-format.md`'s `color_pool` field (V11: 3–5 unique entries drawn
  from the 5-color canonical roster in `design/art/art-bible.md`).
- Non-color tiles (blockers, if any) are excluded from the refill
  distribution entirely — they are never a possible outcome of a
  `next_color()` draw. Blocker placement/behavior is Level Objective &
  Move-Limit System's scope, not RNG Service's.

### 6. API Surface (Design-Level, Engine-Agnostic)

RNG Service exposes the following operations. Signatures are described at
the design level (parameter names and types), not as GDScript — the exact
class/singleton shape is `godot-gdscript-specialist` implementation territory.

| Operation | Signature | Returns | Description |
|---|---|---|---|
| `start_level_session` | `(level_id: int, attempt_number: int)` | — | Begins a session for a standard level attempt. Derives `master_seed` via F1, derives every registry stream's sub-seed via F3, writes the session log. |
| `start_daily_session` | `(daily_challenge_id: int, calendar_date_utc: int)` | — | Begins a session for a shared-seed daily challenge. Derives `master_seed` via F2 (no player-specific input), derives stream sub-seeds via F3, writes the session log. |
| `start_test_session` | `(master_seed: int)` | — | Test/debug entry point. Sets `master_seed` directly, bypassing F1/F2, for gdUnit4 seed injection. |
| `next_float` | `(stream_name: string)` | `float` in `[0, 1)` | The atomic uniform draw every other operation is built on. |
| `next_int` | `(stream_name: string, min_value: int, max_value: int)` | `int` | Uniform integer in `[min_value, max_value]` inclusive. Formula F4. |
| `next_color` | `(stream_name: string, active_colors: array)` | element of `active_colors` | Uniform pick from the level's active color pool. Formula F5. |
| `shuffle` | `(stream_name: string, array: array)` | new `array` | Returns a uniformly-shuffled **copy**; never mutates the input array. Fisher–Yates. Formula F6. |
| `fork_stream` | `(stream_name: string, label: string)` | `string` (child stream name) | Deterministically derives a new child stream scoped to `(parent, label)`, for ad hoc feature-local randomness without a permanent registry row. Idempotent per `(parent, label)` pair within a session. |
| `get_session_log` | `()` | record | Returns the current session's bug-repro record (see §3 table). |

---

## Formulas

### F1 — Master Seed Derivation (Standard Level Attempt)

```
combine(level_id, attempt_number) = (level_id × K1 + attempt_number × K2) mod 2^32
master_seed = mix32( combine(level_id, attempt_number) )
```

| Symbol | Type | Range | Source | Description |
|--------|------|-------|--------|-------------|
| level_id | int | 0 – 2^32−1 | caller (Board Engine, resolved from Level Data Format's level manifest) | Integer identifier for the level definition being played |
| attempt_number | int | 1 – unbounded (practically ≤ 9999) | caller (session-owning system's in-memory counter) | Which play attempt this is, starting at 1, incremented per retry |
| K1 | int (constant) | fixed = 2,654,435,761 | design constant (Knuth's 32-bit multiplicative hash constant, ≈2^32/φ) | Spreads `level_id`'s contribution across the 32-bit space |
| K2 | int (constant) | fixed = 40,503 | design constant | Arbitrary odd constant separating `attempt_number`'s term from `level_id`'s; any odd 32-bit constant satisfies the formula's requirements |
| mix32 | function | — | implementation-owned (§ note below) | A deterministic 32-bit avalanche finalizer |
| master_seed | int (uint32) | 0 – 4,294,967,295 | derived | Root seed for this session; input to Formula F3 for every stream |

**Output range**: unbounded/opaque within `[0, 2^32−1]` — not meant to be
human-interpreted, only used as input to F3. Fully deterministic: identical
`(level_id, attempt_number)` always yields identical `master_seed`.

**`mix32` contract** (binding on implementation, not prescribed here as
code): must be (a) deterministic, (b) strongly avalanching — flipping any
single input bit flips ~50% of output bits, so adjacent `attempt_number`
values don't produce visibly-related boards, and (c) platform-stable —
identical output for identical input across every Godot export target
(iOS/Android/Web), for a given `algorithm_version`. A SplitMix32-style
finalizer is the recommended concrete choice; final selection is a
lead-programmer/technical-director implementation decision, version-tagged
via `algorithm_version` (see Edge Cases — algorithm version changes).

**Worked example**: `level_id = 1007` (Level 1-7), `attempt_number = 3`
(player's third try).

```
combine(1007, 3) = (1007 × 2,654,435,761 + 3 × 40,503) mod 2^32
                  = (2,673,016,811,327 + 121,509) mod 4,294,967,296
                  = 2,673,016,932,836 mod 4,294,967,296
                  = 1,547,274,724
master_seed = mix32(1,547,274,724)
```

The `combine()` stage above is fully computed and verifiable — this is the
fully-specified, formula-level portion of the seed derivation. `mix32`'s
exact numeric output is implementation-produced (its role is purely
statistical bit-spreading for quality, not changing *which* inputs map to
*which* outputs deterministically) and is not hand-computed here.

---

### F2 — Master Seed Derivation (Daily Challenge)

```
combine(daily_challenge_id, calendar_date_utc) = (daily_challenge_id × K1 + calendar_date_utc × K2) mod 2^32
master_seed = mix32( combine(daily_challenge_id, calendar_date_utc) )
```

| Symbol | Type | Range | Source | Description |
|--------|------|-------|--------|-------------|
| daily_challenge_id | int | 0 – 2^32−1 | caller (Events/Theming Engine or daily-challenge config) | Identifies which daily challenge template is active |
| calendar_date_utc | int | 0 – unbounded | caller, computed from device/server UTC clock | Integer day-count (e.g., days since a fixed epoch) — never a hashed date string, to stay platform-stable |
| K1, K2, mix32 | — | — | same as F1 | Identical constants and finalizer contract as F1 |
| master_seed | int (uint32) | 0 – 4,294,967,295 | derived | Root seed shared by every player who plays this daily challenge on this date |

**Output range**: identical structure to F1. Critically, **this formula
takes no player-identifying or player-performance input** — only a challenge
ID and a shared calendar date, both identical for every player. This is the
formula-level guarantee behind the Player Fantasy claim "every player faces
the identical board."

**Worked example**: `daily_challenge_id = 42`, `calendar_date_utc = 20,650`
(illustrative day-count for the session's UTC date; exact epoch/encoding is
an implementation detail).

```
combine(42, 20650) = (42 × 2,654,435,761 + 20,650 × 40,503) mod 2^32
                    = (111,486,301,962 + 836,386,950) mod 4,294,967,296
                    = 112,322,688,912 mod 4,294,967,296
                    = 653,539,216
master_seed = mix32(653,539,216)
```

Note the input set contains nothing about *who* is playing — only *what*
(the challenge) and *when* (the shared UTC date).

---

### F3 — Stream Sub-Seed Derivation

```
stream_seed[stream_id] = mix32( combine(master_seed, stream_id) )
```

Uses the same `combine()`/`mix32` machinery as F1/F2, with `master_seed` and
`stream_id` (from the registry table, §2) as the two inputs.

| Symbol | Type | Range | Source | Description |
|--------|------|-------|--------|-------------|
| master_seed | int (uint32) | 0 – 4,294,967,295 | F1 or F2 output for this session | The session's root seed |
| stream_id | int | 1 – unbounded, append-only | Stream Registry table (§2) | Fixed integer identifying a named stream |
| stream_seed | int (uint32) | 0 – 4,294,967,295 | derived | Initial state for this stream's `next_float` sequence this session |

**Output range**: `[0, 2^32−1]`, one independent value per registered
stream. This is the formula that makes the isolation guarantee (§1) hold:
because `stream_id` differs per stream and `mix32` avalanches, streams
derived from the same `master_seed` produce statistically unrelated
sequences from each other.

**Worked example**: illustrative `master_seed = 500` (a small value chosen
purely to keep this example's arithmetic legible; a real session's
`master_seed` is a full-range F1/F2 output), `stream_id = 1` (`board-refill`,
per the registry table).

```
combine(500, 1) = (500 × 2,654,435,761 + 1 × 40,503) mod 2^32
                 = (1,327,217,880,500 + 40,503) mod 4,294,967,296
                 = 1,327,217,921,003 mod 4,294,967,296
                 = 73,026,539
stream_seed[1] = mix32(73,026,539)
```

Repeating this for `stream_id = 2` (`special-drop`) with the *same*
`master_seed = 500` produces a different `combine()` input (`stream_id`
changed from 1 to 2), and therefore an unrelated `stream_seed` — demonstrating
isolation at the formula level.

---

### F4 — Uniform Integer in Range

```
range_size = max_value − min_value + 1
raw_index  = floor( next_float(stream) × range_size )
index      = min(raw_index, range_size − 1)      // floating-point boundary clamp
result     = min_value + index
```

| Symbol | Type | Range | Source | Description |
|--------|------|-------|--------|-------------|
| min_value | int | any, min_value ≤ max_value | caller | Inclusive lower bound |
| max_value | int | any, max_value ≥ min_value | caller | Inclusive upper bound |
| next_float(stream) | float | [0, 1) | stream's underlying draw | One uniform draw from the named stream |
| range_size | int | ≥ 1 | derived | Number of possible integer outcomes |
| raw_index | int | 0 – range_size (rare boundary case) | derived | Unclamped candidate index |
| index | int | 0 – range_size−1 | derived | Clamped candidate index |
| result | int | min_value – max_value | derived | The returned uniform integer |

**Output range**: `[min_value, max_value]` inclusive, always. `min_value >
max_value` is invalid input and errors rather than silently swapping (see
Edge Cases).

**Worked example**: `min_value = 1`, `max_value = 6`, illustrative
`next_float(stream) = 0.42`.

```
range_size = 6 − 1 + 1 = 6
raw_index  = floor(0.42 × 6) = floor(2.52) = 2
index      = min(2, 5) = 2
result     = 1 + 2 = 3
```

---

### F5 — Uniform Color Selection

```
pool_size = length(active_colors)
raw_index = floor( next_float(stream) × pool_size )
index     = min(raw_index, pool_size − 1)      // floating-point boundary clamp
result    = active_colors[index]
```

| Symbol | Type | Range | Source | Description |
|--------|------|-------|--------|-------------|
| active_colors | array | length ≥ 1 | caller, ultimately Level Data Format's `color_pool` | The level's active color pool this session |
| next_float(stream) | float | [0, 1) | stream's underlying draw | One uniform draw from the named stream |
| pool_size | int | ≥ 1 | derived | Number of active colors |
| result | element | one of `active_colors` | derived | The returned color, uniformly selected |

**Output range**: exactly one element of `active_colors`, each with
probability `1 / pool_size`. `active_colors` of length 0 is invalid input and
errors (see Edge Cases) — it never silently returns a default color.

**Worked example**: `active_colors = ["red","blue","green","yellow","purple"]`
(the 5-color launch default), illustrative `next_float(stream) = 0.83`.

```
pool_size = 5
raw_index = floor(0.83 × 5) = floor(4.15) = 4
index     = min(4, 4) = 4
result    = active_colors[4] = "purple"
```

---

### F6 — Fisher–Yates Shuffle

```
result = copy(array)
for i from length(result) − 1 down to 1:
    j = next_int(stream, 0, i)      // Formula F4
    swap(result[i], result[j])
return result
```

| Symbol | Type | Range | Source | Description |
|--------|------|-------|--------|-------------|
| array | array | length ≥ 0 | caller | The input sequence to shuffle (never mutated) |
| result | array | same length as `array` | derived | A uniformly-random permutation of `array`'s elements |
| i | int | length(array)−1 down to 1 | loop index | Current position being finalized |
| j | int | 0 – i | Formula F4 | Randomly chosen swap partner for position `i` |

**Output range**: `result` is always a permutation of `array` (same
multiset, same length) — never adds, drops, or duplicates an element. Total
draws consumed from the stream = `length(array) − 1` (0 draws for arrays of
length 0 or 1).

**Worked example**: `array = [A, B, C, D]` (length 4), illustrative `next_int`
outputs at each step (produced by the stream's underlying draws via F4):

```
i=3: j = next_int(stream, 0, 3) = 1 → swap result[3],result[1] → [A, D, C, B]
i=2: j = next_int(stream, 0, 2) = 0 → swap result[2],result[0] → [C, D, A, B]
i=1: j = next_int(stream, 0, 1) = 1 → swap result[1],result[1] → [C, D, A, B]  (no-op)
result = [C, D, A, B]
```

Total draws consumed: 3 (`= length(array) − 1`).

---

## Edge Cases

| Scenario | Expected Behavior | Rationale |
|----------|------------------|-----------|
| `next_int(stream, min, max)` called with `min > max` | Error/assertion is raised immediately; no value is returned, min/max are never silently swapped | Surfaces a caller bug loudly instead of masking it — masking it would hide a real defect in the calling system's logic |
| `next_color(stream, [])` called with an empty color pool | Error/assertion is raised immediately; no default color is substituted | An empty active-color pool is a Level Data Format authoring error, not a case RNG Service should paper over — silent substitution would hide bad level data until it shipped |
| Floating-point rounding produces `raw_index == range_size` (or `== pool_size`) at the exact upper boundary | Clamped down to `range_size − 1` (or `pool_size − 1`) per Formulas F4/F5 — this is the **only** silent clamp RNG Service performs | Corrects a floating-point representation artifact (a legitimate IEEE-754 edge case), not a caller error — distinguishing this from the two rows above is intentional |
| Two consuming calls draw from the same stream within the same board-resolution frame (e.g., gravity-fill and a pre-fill "no starting matches" pass both hit `board-refill`) | Draws are strictly sequential in call order; RNG Service has no concurrency model | Board Engine resolves single-threaded per `technical-preferences.md` — call order alone determines the sequence, and that order must be documented in `board-engine.md`'s implementation |
| `level_id` is authored as a string in Level Data Format rather than an integer | Must be resolved to a fixed, versioned ordinal integer via the level manifest before calling `start_level_session()` — RNG Service's signature only accepts an integer `level_id` | Runtime string-hash stability is not guaranteed across Godot's iOS/Android/Web export targets; IEEE-754 integer/float math (used everywhere else in this service) is portable, string hashing is not |
| App is backgrounded or killed by the OS mid-level | RNG Service state exists only in memory for the session's duration; it is never persisted to disk. If the process is killed, the session is abandoned — re-entering the level starts a **new** session with an incremented `attempt_number` and a new `master_seed` | Keeps RNG Service dependency-free (no coupling to Save & Persistence) for MVP; matches genre convention — match-3 games typically don't resume mid-cascade after a hard process kill |
| The core `mix32` finalizer needs to change (bug fix, better statistical quality) | `algorithm_version` increments (e.g., `"v1"` → `"v2"`); new sessions use the new version. Previously logged bug-repro seeds captured under an older version are documented as non-reproducible with the new build — the log records which version produced them, so this is a known, disclosed limitation, not a silent break | Determinism must be locked per-version once seeds are logged in the wild; the alternative (silently changing the mixing function) would break reproducibility without anyone knowing why |
| `fork_stream(parent, label)` is called twice with identical `(parent, label)` in the same session | Idempotent: the second call returns a reference to the **same** child stream, continuing its existing sequence — it does not create a second independent stream or reset position to the start | Prevents an accidental double-fork from silently duplicating or resetting randomness a system already consumed from earlier in the same session |
| A daily challenge session spans a UTC day boundary (player starts at 23:59 UTC, keeps playing past midnight) | `calendar_date_utc` is fixed once at `start_daily_session()` call time for the whole session; it is never re-evaluated mid-session | The board must never change mid-play just because the calendar rolled over — fairness within a single playthrough takes priority over strict "today's date" purity |
| Two levels are accidentally authored with the same `level_id` | Out of RNG Service's scope to detect — Level Data Format owns ID uniqueness. RNG Service will correctly (and silently) produce identical seeds/boards for both, per its contract | This is the *correct* behavior given the formula; it is not RNG Service's job to special-case bad content data, only to be predictable given whatever `level_id` it's handed |
| A registered stream (e.g., `special-drop`) has no active consumer yet in the current build | It still receives a valid derived sub-seed at every session start, per the registry table (§2) | Zero cost, keeps `stream_id` numbering stable for the future consumer defined in `special-candies.md` |
| A single session draws an extremely large number of values from one stream (e.g., a pathological infinite-cascade test board with thousands of refill steps) | No special handling required in this document; the underlying per-call generator (implementation-owned) must support at least 10^9 draws per stream with no measurable statistical degradation or performance cliff | Ordinary play never approaches this volume — flagged here as a non-functional requirement on the implementation, not a real gameplay edge case |

## Dependencies

RNG Service depends on **no other design system** — this is confirmed by
`systems-index.md`'s Foundation-layer entry ("Depends On: —") and preserved
deliberately: every input to this service (`level_id`, `attempt_number`,
`daily_challenge_id`, `calendar_date_utc`, `active_colors`, arbitrary
arrays) is accepted as an **opaque caller-supplied parameter**, never read
by this service from another system's data directly. In particular, RNG
Service does **not** depend on Level Data Format even though it consumes
values that originate there (`level_id`, `active_colors`) — it never reads
the Level Data Format schema/resource itself, only whatever plain int/array
the caller passes in. This distinction is what lets RNG Service be authored
and implemented first (design order position #1), before Level Data Format
(#2) exists.

| System | Direction | Nature of Dependency |
|--------|-----------|----------------------|
| Match-3 Board Engine | Board Engine depends on RNG Service | Consumes `board-refill` stream for gravity/refill candy selection every cascade step, for bootstrap fill, AND for the "no valid moves" reshuffle (direct draws with pinned row-major traversal — `board-engine.md` § Detailed Rules 11); `fork_stream()` is available but not currently used by Board Engine. **Confirmed against `board-engine.md`** (authored 2026-07-18; corrected from the pre-authoring anticipation per its design-review advisory). |
| Special Candies & Combo Matrix | Special Candies depends on RNG Service | Reserves the `special-drop` stream (stream_id 2); this document only reserves the infrastructure, `special-candies.md` owns the semantics of what (if anything) it draws. **To be confirmed in `special-candies.md` when authored.** |
| Booster Brewing Meta *(Phase 2, not yet designed)* | Will depend on RNG Service | Reserves the `harvest` stream (stream_id 3) for ingredient-drop variance from matches. **Forward dependency — confirm in `booster-brewing.md` when the Phase 2 friction prototype gate is passed and the GDD is authored.** |
| Events/Theming Engine *(Phase 3, not yet designed)* | Will depend on RNG Service | Reserves the `events` stream (stream_id 4) for event reward-drop variance. **Forward dependency — confirm in `events-theming.md` when authored.** |
| Level Data Format | **Not a dependency** (see note above) | RNG Service accepts `level_id` and `active_colors` as opaque parameters; it does not read Level Data Format's schema/resource directly. Flagged here explicitly to prevent this being mistaken for an implicit dependency during future GDD reviews. |

## Tuning Knobs

| Parameter | Current Value | Safe Range | Effect of Increase | Effect of Decrease |
|-----------|--------------|------------|-------------------|-------------------|
| Active color pool size (`active_colors` length — owned by Level Data Format's `color_pool` field, consumed here via `next_color`) | 5 (launch default, from `prototypes/sweet-cascade-concept/REPORT.md` design analysis) | 3 – 8 (RNG Service technical ceiling; current authored content is capped at 3–5 by Level Data Format's V11 and the 5-color canonical roster in `design/art/art-bible.md` — 6–8 is unused headroom until the roster grows) | More colors = sparser matches, harder to spot cascades, lower cascade frequency (6 felt "too sparse" per prototype learnings) | Fewer colors = near-guaranteed matches on almost every swap, cascades feel automatic/unearned, low skill expression |
| `ALGORITHM_VERSION` (mix32 finalizer version tag) | `"v1"` | Increment-only string/int tag; never reuse a retired version number | N/A — this is a compatibility tag, not a magnitude | N/A — changing the underlying finalizer without incrementing this breaks reproducibility of previously logged bug-repro seeds silently, which is the one outcome this knob exists to prevent |
| Registered stream count (Stream Registry table, §2) | 2 active (`board-refill`, `special-drop`) + 2 reserved (`harvest`, `events`) | Unbounded, append-only | More streams = more parallel independent randomness sources for future features; negligible cost (each stream is a small state value) | Fewer/shared streams = risk of violating the isolation guarantee if two unrelated features are made to share one stream instead of getting their own row |
| `attempt_number` reset policy | Resets to 1 only on fresh level entry from the map; does **not** persist across app relaunch mid-session | N/A — binary policy choice, not a numeric range | N/A | If changed to persist indefinitely across app restarts, this requires wiring RNG Service's session lifecycle into Save & Persistence — explicitly out of scope for MVP (see Edge Cases — app killed mid-level) |
| Daily-seed date granularity (`calendar_date_utc` resolution) | UTC calendar day | Hour – Week | Finer granularity (e.g., hourly) makes the daily challenge board change more often within a day, which risks players in different time zones seeing different boards mid-"day" — undermines the shared-seed fairness guarantee | Coarser granularity (e.g., weekly) reduces content freshness for daily challenges |

## Acceptance Criteria

**Determinism**

- [ ] `test_same_seed_same_sequence`: two `start_test_session(seed=X)` calls,
      each followed by 50 `next_int` draws on `board-refill`, produce two
      identical sequences.
- [ ] `test_different_seeds_different_sequences`: `start_test_session(seed=X)`
      and `start_test_session(seed=X+1)`, each followed by 50 `next_int`
      draws on `board-refill`, produce non-identical sequences.
- [ ] `test_attempt_number_changes_seed`: `start_level_session(level_id=X,
      attempt_number=1)` and `start_level_session(level_id=X,
      attempt_number=2)` produce different `master_seed` values and
      different first-10-draw sequences on `board-refill`.
- [ ] `test_level_id_changes_seed`: `start_level_session(level_id=X,
      attempt_number=1)` and `start_level_session(level_id=Y,
      attempt_number=1)` with `X != Y` produce different `master_seed`
      values.

**Stream Isolation**

- [ ] `test_stream_isolation`: a run that interleaves 100 `next_int` draws on
      `special-drop` between draws on `board-refill` produces the exact same
      `board-refill` sequence as a control run with no interleaved
      `special-drop` draws.
- [ ] `test_fork_stream_isolated_from_parent`: drawing N values from a
      stream created by `fork_stream(parent, label)` does not change what
      `parent` returns next, compared to a control run that never forked.
- [ ] `test_fork_stream_deterministic`: two fresh, identically-seeded
      sessions calling `fork_stream(parent, label)` with the same
      `(parent, label)` produce a child stream with identical output
      sequences.
- [ ] `test_fork_stream_idempotent_within_session`: calling
      `fork_stream(parent, label)` twice with identical arguments in one
      session returns a reference to the same child stream (continuing its
      sequence), not a second independent stream reset to the start.

**Distribution Fairness**

- [ ] `test_uniform_color_distribution`: over 100,000 `next_color` draws with
      a 5-element `active_colors` pool, each color's observed frequency
      falls within ±2% of the expected 20% share (chi-square goodness-of-fit
      at p > 0.01 is the pass bar).
- [ ] `test_next_int_bounds_respected`: 10,000 `next_int(stream, min, max)`
      calls across several `(min, max)` pairs never return a value outside
      `[min, max]`.
- [ ] `test_shuffle_preserves_multiset`: `shuffle(stream, array)` returns a
      permutation with the exact same multiset of elements as the input, for
      arrays of length 0, 1, 2, and 20 — never adds, drops, or duplicates an
      element.
- [ ] `test_shuffle_does_not_mutate_input`: the array passed into `shuffle()`
      is unchanged after the call; only the returned array is shuffled.

**Input Validation**

- [ ] `test_next_int_invalid_range_errors`: `next_int(stream, min, max)` with
      `min > max` raises an error/assertion rather than returning a value.
- [ ] `test_next_color_empty_pool_errors`: `next_color(stream, [])` raises an
      error/assertion rather than returning a default value.
- [ ] `test_boundary_rounding_clamped`: with a mocked `next_float` source
      returning a value where `floor(next_float × range_size)` would equal
      `range_size` due to floating-point rounding, `next_int`/`next_color`
      clamp to `range_size − 1` / `pool_size − 1` and never throw or return
      an out-of-bounds index.

**Honest Randomness Contract**

- [ ] `test_daily_seed_deterministic_across_calls`: `start_daily_session()`
      called multiple times (simulating multiple players/devices) with
      identical `(daily_challenge_id, calendar_date_utc)` always produces
      the same `master_seed` and the same first 50 `board-refill` draws.
- [ ] `test_daily_seed_excludes_player_data`: `start_daily_session()`'s
      signature accepts no player-identifying or player-performance
      parameter — verified by interface inspection (no such parameter
      exists to pass), confirming F2's "no player input" property.
- [ ] `test_no_hidden_bias_parameter_on_draw_functions`: `next_int`,
      `next_float`, `next_color`, and `shuffle` accept no parameter
      representing player skill, streak, session performance, or purchase
      state — verified by interface inspection.

**Bug Repro & Logging**

- [ ] `test_session_log_contains_repro_fields`: after
      `start_level_session()`, `get_session_log()` returns a record with
      non-null `level_id`, `attempt_number`, `master_seed`, and
      `algorithm_version` fields.
- [ ] `test_session_log_replay`: feeding a previously-logged
      `(level_id, attempt_number)` pair back into a fresh
      `start_level_session()` reproduces the same `master_seed` recorded in
      the original log.

**Performance**

- [ ] Filling an 8×8 board (64 draws) plus one worst-case full-board cascade
      refill (≤64 additional draws) from `board-refill` completes in under
      1ms on the target mid-range mobile profile — well inside the 16.6ms
      frame budget from `technical-preferences.md`.
