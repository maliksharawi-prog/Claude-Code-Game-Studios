# Epic: Board Engine & Special Candies (Domain Core)

> **Epic ID**: E03
> **Layer**: Core (BoardModel) + Feature (SpecialResolver)
> **GDD**: `design/gdd/board-engine.md` (Rev 2) · `design/gdd/special-candies.md`
> **Architecture Module**: `SweetCascade.Domain` — BoardModel (grid truth, swap, match, gravity/refill, cascade loop, reshuffle, `chain_index`, event emission), MoveResolver sink pipeline, the full BoardEvent catalog, SpecialResolver (combo matrix + F8 passive detonation) implementing the four seams
> **Status**: Ready
> **Stories**: Not yet created — run `/create-stories board-engine-specials`

## Scope

The deterministic heart of the game, as a headless synchronous state machine. Delivers the
**BoardModel** — swap validation, match detection, segment-scoped gravity/refill, the cascade
loop to fixpoint under `MAX_CASCADE_DEPTH` / `MAX_CHAIN_EXPANSION_ITERATIONS` caps, reshuffle,
and the full **BoardEvent catalog** (14-signal board-engine §7 set + feature events, with
`PieceSnapshot` identity for deferred replay) emitted through the `IBoardEventSink` /
MoveResolver pipeline (ADR-005 D2). Delivers the **four extension seams** (`ISpecialResolver`)
with no-op MVP defaults on the Board side, and the **SpecialResolver** implementation of them:
the combo matrix (Formulas 4–7), creation eligibility, cluster precedence, and deterministic
most-common-color passive detonation (F8) — all with zero Board Engine change. Resolves fully
in one call, zero `await`/timer, byte-identical across platforms. This is the Edit-Mode
AC-coverage centerpiece.

## Governing ADRs

| ADR | Decision Summary | Engine Risk |
|-----|-----------------|-------------|
| ADR-005: Event Bridge & BoardEvent Catalog | MoveResolver sink, `IBoardEventSink`, full §7 catalog + `PieceSnapshot`, `SpecialType` enum (WRAPPED=4 reserved), byte-identical ordering (leans on ADR-004) | LOW |
| ADR-006: Level Manifest Generation | `ResolveOrdinal(level_id) → int` consumed at board bootstrap | LOW |
| ADR-004: Deterministic RNG | BoardModel draws the `board-refill` stream from `IRngService` | LOW |
| arch §8.1 | The four synchronous seams (`ISpecialResolver`) — Board owns all validity judgment; seams re-checked against live state | — |

> **Advisory (non-blocking):** master-architecture §8.1 still shows `ScoreKeeper` threaded
> through the `ActivationClears` / `ExpandChain` seam signatures. ADR-005 (D2) is authoritative
> and removes it (scoring observes the sink; activations priced from `MatchCleared.ClearedPieces`).
> Drop the `ScoreKeeper` parameter when speccing these stories.

## TR-IDs Owned

| TR-ID | Requirement | ADR Coverage |
|-------|-------------|--------------|
| TR-be-001 | Headless, synchronous, fully-deterministic resolution state machine (zero await/timer) | ADR-005 (MoveResolver) + arch §7.1 ✅ |
| TR-be-002 | Four extension seams (activation-check / activation-clears / special-spawns / chain-expansion) with no-op MVP defaults | arch §8.1 (`ISpecialResolver`) ✅ |
| TR-be-003 | Full event catalog with `PieceSnapshot` identity for deferred replay; `board_input_enabled_changed` | ADR-005 ✅ |
| TR-be-004 | Manifest String→ordinal resolution; termination caps (`MAX_CASCADE_DEPTH`, `MAX_CHAIN_EXPANSION_ITERATIONS`) | ADR-006 + arch §6 (caps) ✅ |
| TR-be-005 | Segment-scoped gravity/refill; retry-on-bootstrap vs plain-uniform-cascade asymmetry; reshuffle | arch §7 / board-engine GDD ✅ |
| TR-sc-001 | Combo matrix (Formulas 4–7) + passive detonation (deterministic most-common-color, F8) via the 4 seams, zero Board change | arch §8.1 seam contract ✅ |
| TR-sc-002 | Append-only `special_type` vocabulary; `WRAPPED=4` reserved | ADR-005 (`SpecialType` enum) ✅ |
| TR-sc-003 | Harvest Observation Point (per-color identity preserved on every clear path) | ADR-005 (`PieceSnapshot`) ✅ |

## Depends On

- **E01** (assemblies + purity guard + CI).
- **E02** (RngService for the `board-refill` stream; LevelData schema for bootstrap; manifest ordinal + BoardEvent record types authored as Domain foundation).

## Engine-Risk Notes (per `docs/engine-reference/unity/VERSION.md`)

- **LOW — pure Domain, zero engine surface.** BoardModel / SpecialResolver / MoveResolver
  touch no `UnityEngine` type (CI-enforced). This is deliberate: the whole logic core is
  linkable and testable in a plain .NET runner (arch §5.2).
- Uses C# 9 `record` / `readonly record struct` for events + `Cell` (`Cell` supersedes the
  blueprint's hand-rolled `R*16+C` hash) — supported in Unity 6 (`current-best-practices.md`).
- **Determinism condition:** correctness depends on E02's platform-stable RNG (ADR-004);
  all cascade/match tests are seeded and reproducible.
- **Design watch (systems-index High-Risk):** the passive color-bomb detonation during
  cascades was flagged as potentially feeling "unearned" — a design-review concern, not an
  engine risk; F8 is deterministic and specced, but flag it for game-designer sign-off.

## Definition of Done

This epic is complete when:
- All stories are implemented, reviewed, and closed via `/story-done`.
- BoardModel resolves a full move synchronously with the complete ordered BoardEvent stream
  (matching board-engine §7) and correct `PieceSnapshot` identity — proven in Edit Mode.
- The four seams are exercised by SpecialResolver: the combo matrix (F4–7) and F8 passive
  detonation produce the specified clear sets, and both termination caps bound worst-case
  resolution — all deterministic against seeded fixtures.
- All board-engine.md and special-candies.md acceptance criteria have passing Edit Mode tests
  in `tests/unit/`.

## Next Step

Run `/create-stories board-engine-specials`.
