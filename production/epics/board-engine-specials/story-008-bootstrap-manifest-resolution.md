# Story 008: Level bootstrap procedure & manifest ordinal resolution (Formula 1)

> **Epic**: Board Engine & Special Candies (Domain Core) — E03
> **Status**: Ready
> **Layer**: Core
> **Type**: Integration
> **Estimate**: L
> **Manifest Version**: 2026-07-18
> **Last Updated**: —

## Context

**GDD**: `design/gdd/board-engine.md` (Rev 2) — § Detailed Rules 2 (Level Bootstrap & the Level Manifest), Formula 1 (Level Manifest Resolution)
**Requirement**: `TR-be-004`, `TR-be-005`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time.)*

**ADR Governing Implementation**: ADR-006: Level Manifest Generation & Cross-Validation (primary — `LevelManifest.ResolveOrdinal(level_id) → int`, consumed at board bootstrap)
**Governing ADRs (secondary)**: ADR-004 (`StartLevelSession(level_id = ordinal, attempt_number)` seeds the `board-refill` stream); ADR-005 (`BoardBootstrapped`/`PiecesSpawned(Bootstrap)`/`BoardInputEnabledChanged` records).
**ADR Decision Summary**: `ResolveOrdinal(levelId) = entries.FindIndex(id) + 1`; `0` is the "unresolved" sentinel. Never hash a `level_id` string at runtime; never hardcode a `level_id → int` mapping outside `LevelManifest.ResolveOrdinal`. The manifest loads via a direct serialized reference, never Addressables.

**Engine**: Unity 6.3 LTS (6000.3.x) | **Risk**: LOW
**Engine Notes**: Pure Domain — `LevelManifest`/`ResolveOrdinal` are `noEngineReferences`. `ResolveOrdinal` is O(n≤120), called once per level entry, never in a frame loop. No `UnityEngine`, no post-cutoff API in the Domain path.

**Control Manifest Rules (Domain)**:
- Required: `level_id` reaching RNG is a resolved `int` ordinal from `LevelManifest.ResolveOrdinal`, never a runtime string hash. `BOOTSTRAP_MAX_RETRIES_PER_CELL` is centralized Domain config.
- Required: bootstrap reads exactly five `LevelData` fields (`grid_width`, `grid_height`, `cell_mask`, `pre_placed_pieces`, `color_pool`) — never `move_limit`, `objectives`, or star thresholds.
- Forbidden: hashing a `level_id` string to derive an RNG seed input; hardcoding a `level_id → int` mapping outside `ResolveOrdinal`.

---

## Acceptance Criteria

*From `design/gdd/board-engine.md`, scoped to this story:*

- [ ] **Manifest resolution** (Formula 1): `manifest_index = 1 + INDEX_OF(entries, level_id)`. `test_manifest_resolution_matches_position` (N entries resolve to correct 1-based index, matching the worked example); `test_manifest_missing_level_id_fails_loudly` (absent `level_id` fails with a logged error, does not load); `test_manifest_no_duplicate_entries` (data-driven over the real manifest asset).
- [ ] **Bootstrap procedure** (ordered, deterministic): resolve ordinal → `StartLevelSession(ordinal, attempt_number)` → read grid dims → mark `VOID`/`EMPTY` from `cell_mask` → compute segments → place `pre_placed_pieces` (`special_type = SPECIAL_NONE`) → fill remaining cells via retry-until-no-match (Story 004) → emit one `PiecesSpawned(source = Bootstrap)` covering every playable cell → run one Matching pass (step 8) → step-9 reshuffle-check → emit `BoardBootstrapped(rows, cols, cell_mask, manifest_index)`, set `board_input_enabled = true`, transition to `Idle`.
- [ ] `test_bootstrap_reads_only_five_fields`: bootstrap touches only the five listed `LevelData` fields.
- [ ] `test_bootstrap_never_leaves_empty_cell`: after bootstrap reaches `Idle`, every playable cell is `OCCUPIED`.
- [ ] `test_pre_placed_pieces_placed_before_rng_fill`: pre-placed cells hold the exact specified color, unaltered by the RNG fill.
- [ ] `test_bootstrap_accidental_match_resolves_via_cascade`: a synthetic level whose `pre_placed_pieces` form a run resolves it automatically at bootstrap (`trigger = Bootstrap`) before `Idle`, zero moves consumed.
- [ ] `test_pieces_spawned_fires_once_for_bootstrap`: bootstrap emits exactly one `PiecesSpawned(Bootstrap)` covering every playable cell (pre-placed + RNG-filled), each with correct `color`/`special_type`, fired before step-8 Matching.
- [ ] `test_different_attempt_number_different_bootstrap`: `attempt_number = 1` vs `2` produce different initial boards (RNG Formula F1).

---

## Implementation Notes

*Derived from board-engine §2 + ADR-006 + ADR-004:*

- Bootstrap step 8 reuses the standard cascade pipeline (Story 006) with `trigger_source = Bootstrap` — no special-cased logic; a pre-placed accidental match resolves like any cascade (consumes zero moves, never attributed to a swap `chain_index`).
- Bootstrap step 9 calls `has_available_move()` and, if false, Reshuffling (Story 007) with `trigger_source = Bootstrap` before proceeding to `Idle`.
- Bootstrap ordering guarantee (ADR-005): `PiecesSpawned(Bootstrap) → [if accidental match] MatchCleared(1, Bootstrap) → PiecesSpawned(CascadeRefill) → … → CascadeEnded → [if step-9 reshuffle] NoValidMovesDetected → BoardReshuffled → BoardBootstrapped → BoardInputEnabledChanged(true)`.
- A missing `level_id` fails loudly (does not load) — never auto-append, which would make manifest state depend on load order and break the append-only determinism guarantee.
- `attempt_number` is a caller-supplied parameter BoardModel does not own or persist (for MVP the Level Preview harness supplies it, defaulting to 1).

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 004: the retry-until-no-match fill routine itself (called here).
- Story 006/007: the cascade pipeline and reshuffle mechanism (invoked here).
- The `LevelManifest` generation tool + W6–W7 cross-validation (E02 / Editor scope). This story consumes `ResolveOrdinal`; it does not build the generator.
- `LevelData` schema authoring (E02).

---

## QA Test Cases

*Authored at story creation. Implement against these — do not invent new cases during implementation.*

- **AC — manifest resolution** (`test_manifest_resolution_matches_position`, `test_manifest_missing_level_id_fails_loudly`, `test_manifest_no_duplicate_entries`)
  - Given: a manifest with N entries (and the real manifest asset for the duplicate check).
  - When: `ResolveOrdinal` is called.
  - Then: correct 1-based index; an absent id fails loudly (logged, no load); no duplicate entry exists.
- **AC — five-field read** (`test_bootstrap_reads_only_five_fields`)
  - Given: a mocked `LevelData` recording field access.
  - When: bootstrap runs.
  - Then: only `grid_width`/`grid_height`/`cell_mask`/`pre_placed_pieces`/`color_pool` are touched.
- **AC — bootstrap completeness** (`test_bootstrap_never_leaves_empty_cell`, `test_pre_placed_pieces_placed_before_rng_fill`, `test_pieces_spawned_fires_once_for_bootstrap`)
  - Given: a level with pre-placed pieces.
  - When: bootstrap reaches `Idle`.
  - Then: every playable cell `OCCUPIED`; pre-placed cells hold exact colors; exactly one `PiecesSpawned(Bootstrap)` covers all playable cells before step-8 Matching.
- **AC — accidental match & attempt variance** (`test_bootstrap_accidental_match_resolves_via_cascade`, `test_different_attempt_number_different_bootstrap`)
  - Given: (a) pre-placed pieces forming a run; (b) the same level with `attempt_number` 1 vs 2.
  - When: bootstrap runs.
  - Then: (a) the run resolves via a `Bootstrap` cascade before `Idle`, zero moves; (b) the two initial boards differ.

---

## Test Evidence

**Story Type**: Integration
**Required evidence**: `tests/unit/board-engine/bootstrap_manifest_test.cs` — must exist and pass (seeded RNG + mocked `LevelData`/manifest).

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 004 (bootstrap fill), Story 006 (step-8 cascade), Story 007 (step-9 reshuffle), E02 (`LevelManifest.ResolveOrdinal`, `LevelData` schema, `IRngService.StartLevelSession`).
- Unlocks: Story 009 (`MoveResolver.ResolveBootstrap` wraps this), Story 010 (determinism uses bootstrap + intents).
