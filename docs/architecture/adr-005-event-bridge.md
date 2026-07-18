# ADR-005: Event Bridge & Deferred-Replay Contract (BoardEvent Catalog)

## Status

Accepted

> **Founder review may amend.** This ADR is Technical-Director-accepted on the date
> below to unblock the Board Engine ↔ Juice Layer integration slice. Like the master
> architecture it derives from, its acceptance is subject to founder review; any
> founder amendment supersedes the affected clauses via a revision to this ADR.

## Date

2026-07-18

## Last Verified

2026-07-18

## Decision Makers

Technical Director (owner). Derived from the founder-approved master architecture
(`docs/architecture/architecture.md` §11, Required ADR-D) and the eleven approved
MVP GDDs. Resolves architecture Open Question QQ-01 and TD-ARCHITECTURE approval
condition 3 ("Event catalog completion").

## Summary

Sweet Cascade's pure-C# Domain resolves an entire move synchronously and must hand a
single, ordered, fully-self-describing event stream to a presentation layer that replays
it across many later frames. This ADR fixes the authoritative `BoardEvent` catalog (one
immutable record per GDD signal, full `PieceSnapshot` identity everywhere the GDDs
mandate it), the drained-queue transport, the single-sequence ordering guarantee, the
two consumption modes (in-resolve logic vs. deferred presentation), the four-term
input-lock composition, and the all-single-threaded execution model.

## Engine Compatibility

| Field | Value |
|-------|-------|
| **Engine** | Unity 6.3 LTS (6000.3.x) · C# |
| **Domain** | Core / Scripting (pure-C# Domain assembly; zero `UnityEngine`) |
| **Knowledge Risk** | LOW — the entire mechanism is C# 9 `record` / `readonly record struct` types and plain BCL collections in the `SweetCascade.Domain` assembly, which carries no engine-version exposure by construction (architecture §1, §5). |
| **References Consulted** | `docs/architecture/architecture.md` §§4–8, 11; `docs/architecture/visual-interface-blueprint.md` §3.1; `design/gdd/board-engine.md` §§ Detailed Rules 7, 13; `design/gdd/juice-layer.md` §§1–3, 10; `design/gdd/level-objectives.md` §§5, 6, 9; `design/gdd/scoring-stars.md` §§1, 4, 10a; `docs/engine-reference/unity/VERSION.md` |
| **Post-Cutoff APIs Used** | None. The catalog and transport use only language/BCL features (records, `IReadOnlyList<T>`, `List<T>`), all stable since well before Unity 6. |
| **Verification Required** | Confirm the Unity project's C# language version supports `record` and `readonly record struct` (Unity 6 defaults to C# 9, which does — verify the `SweetCascade.Domain.asmdef` compiles the catalog headlessly in the Edit-Mode test runner). No runtime-engine behavior to verify. |

> **Note**: Knowledge Risk is LOW, so no engine-upgrade re-validation is triggered by
> this ADR alone. The determinism guarantee it relies on is owned by ADR-C (RNG), which
> carries its own platform-stability verification.

## ADR Dependencies

| Field | Value |
|-------|-------|
| **Depends On** | ADR-001 (Engine Selection — Unity 6.3 LTS, Accepted). ADR-C (Deterministic RNG & Domain determinism guard) for the **byte-identical** clause of the ordering guarantee — the sequence is only reproducible if `board-refill` draws are platform-stable. This ADR's catalog and transport stand without ADR-C; only the cross-platform reproducibility claim leans on it. |
| **Enables** | Board Engine ↔ Juice Layer integration; Scoring and Objectives synchronous consumption; Screen Flow input-lock composition and Results routing. |
| **Blocks** | Board Engine slice (cannot be end-to-end tested until the catalog exists — architecture QQ-01); Juice Layer slice (Shadow Board Model + Reveal Queue read this catalog); Level Objective and Scoring integration slices. |
| **Ordering Note** | Foundation-tier ADR (architecture §11 "Must have before coding starts"). Peer to ADR-A/B/C/E. Numbered ADR-005; corresponds to architecture §11's label **ADR-D**. |

## Context

### Problem Statement

The master architecture (Principle 2) mandates: *logic resolves instantly; presentation
paces reveals.* The pure-C# Domain resolves an entire move — swap, every cascade step,
gravity, refill, reshuffle, stabilize — **synchronously in one call** (`board-engine.md`
§13), while the Game layer replays that move across many rendered frames at a tuned tempo
(`juice-layer.md` §1). The only thing crossing that boundary is a stream of events.

That stream is load-bearing for four different consumers with two different timing needs,
and it must be **fully self-describing** so a consumer replaying it seconds later — after
the live board has already moved on — can reconstruct every historical piece's identity
with zero look-ups into live state (`board-engine.md` §13 payload-sufficiency guarantee;
`juice-layer.md` §2 Shadow Board Model). The blueprint (`visual-interface-blueprint.md`
§3.1) sketches a partial record set as a starting point but omits ten catalog members and
strips full piece identity from several payloads. Without the complete catalog, the Board
Engine slice cannot be tested end-to-end (architecture QQ-01) and the Board Engine ↔ Juice
integration cannot be built. This decision must be made now because it is the interface
contract every one of those systems compiles against.

### Current State

- `visual-interface-blueprint.md` §3.1 (`PieceTypes.cs`) defines a **subset**:
  `SwapAccepted`, `SwapRejected`, `MatchCleared`, `SpecialSpawned`, `PiecesSpawned`,
  `CascadeEnded`, `BoardStabilized`, `BoardReshuffled` — several with wrong or missing
  fields (e.g. `MatchCleared` carries `(Cell, Piece)[]` with no `run_data`/`trigger_source`;
  `SwapAccepted`/`SwapRejected` carry no `trigger_source`/`reason`; `PiecesSpawned` uses a
  `bool Bootstrap` instead of the GDD's `{BOOTSTRAP, CASCADE_REFILL}` source enum;
  `SpecialSpawned` diverges from the GDD payload). It is explicitly a "spec-by-example,"
  not the authoritative catalog.
- `board-engine.md` §7 defines the **full** fourteen-signal catalog with exact payloads
  and two verbatim ordering guarantees, plus the `PieceSnapshot` value type.
- `level-objectives.md` and `scoring-stars.md` define three further presentation-bound
  feature events (`objective_progressed`, `moves_remaining_changed`, `level_resolved`) and
  the win-persistence ordering fixed 2026-07-18 (Save write strictly precedes
  `level_resolved`).
- No `BoardEvent` records exist in code yet; there is no Unity project yet.

### Constraints

- **Domain purity (architecture Principle 1, §5):** every catalog type lives in
  `SweetCascade.Domain` and may reference **no** `UnityEngine`/`UnityEditor` type
  (asmdef `noEngineReferences`, CI-guarded). The event records are the widest Domain→Game
  contract, so this is where an accidental engine leak would hurt most.
- **Determinism (architecture Principle 3; `board-engine.md` §12):** under identical
  inputs the *entire* emitted sequence — names, payloads, order — must be byte-identical
  across runs and across iOS/Android/Web.
- **Single-threaded (technical-preferences.md; `rng-service.md` Edge Cases):** all
  gameplay logic runs on the main thread; no worker threads touch board or event state.
- **Deferred replay (`board-engine.md` §13; `juice-layer.md` §1–3):** a consumer must be
  able to store a whole move's stream and replay it later using stored payloads alone.
- **Timeline:** this is a Foundation ADR; the Board Engine, Juice, Objectives, and Scoring
  slices are all blocked on it.

### Requirements

- One immutable record per GDD signal; every payload field typed; full `PieceSnapshot`
  identity on every clear/spawn/place payload the GDDs mandate it on.
- A single authoritative ordered sequence per move, consumable both synchronously in
  emission order (Scoring, Objectives) and as an immutable whole afterward (Juice, HUD,
  Screen Flow), with the two modes reconciled explicitly.
- The four-term input-lock composition (screen-flow Formula 5) with an owner named for
  each term and a single composition point.
- Explicit statement that everything is main-thread single-threaded.

## Decision

### D1 — Transport: a per-move ordered, immutable, drained sequence (not C# `event` subscriptions across the boundary)

The Domain produces, per move, **one ordered `IReadOnlyList<BoardEvent>`**. The Game layer
receives that whole immutable list *after* the move fully resolves and **drains** it at its
own pace. The Domain→presentation boundary is a handed-over buffer, **not** a set of C#
`event`/delegate subscriptions fired live across the assembly seam.

Rationale: this is the literal shape of the GDDs' deferred-replay model. `juice-layer.md`
§1–3 require the presentation to *capture a whole burst atomically, then replay it across
many later frames* from stored payloads. A live cross-boundary `event` would fire all
callbacks inside the synchronous resolve call — exactly what must **not** happen, because
the board is already at its settled state by then, so any live-fired presentation handler
would render against the wrong state. A drained, immutable snapshot makes "replay later
from stored events alone" the default and the Shadow Board Model (`juice-layer.md` §2)
implementable with zero live queries (the one reshuffle exception aside).

The Foundation-layer carrier is `EventBridge` (Game assembly, architecture §6): a plain
in-memory hand-off with no engine dependency.

```csharp
// SweetCascade.Game — the carrier. Plain C#, no UnityEngine types.
public interface IEventBridge
{
    // Called once per fully-resolved move (or the bootstrap "move 0").
    void EnqueueMove(IReadOnlyList<BoardEvent> moveEvents);
}
```

**Single-active-queue invariant (`juice-layer.md` §3).** Because input stays locked for a
move's entire visual replay (D5), the Domain can never resolve a second move before the
first is drained. `EventBridge` therefore never buffers more than one un-drained move at
MVP; it asserts this rather than building a merge/interleave path that cannot be exercised.

### D2 — The authoritative sequence and its single producer path

The single authoritative ordered sequence for a move is owned by a Domain orchestrator,
`MoveResolver` (Domain), not by `BoardModel` directly. `BoardModel` emits every event
through an injected sink; `MoveResolver` is that sink, so it can (a) append every board
event in strict emission order and (b) let the in-resolve logic systems append their own
derived feature events *at the correct interleaved position* (needed so the HUD progress
bar climbs in lockstep with each clear — `level-objectives.md` §5).

```csharp
// SweetCascade.Domain
public interface IBoardEventSink { void Emit(BoardEvent e); }

public sealed class MoveResolver : IBoardEventSink   // synchronous, headless, single-threaded
{
    // Composition root for one level's logic: BoardModel + ScoreKeeper + ObjectiveEvaluator.
    // Owns the authoritative per-move event log. Fixed logic-subscriber order (see D3).

    // Runs the ENTIRE resolution synchronously and returns the move's immutable sequence.
    public IReadOnlyList<BoardEvent> ResolveSwap(Cell a, Cell b);
    public IReadOnlyList<BoardEvent> ResolveBootstrap();   // the opening "move 0"

    void IBoardEventSink.Emit(BoardEvent e) { /* append to log; dispatch to logic subscribers (D3) */ }
}
```

This **refines** architecture §8.2's illustrative `BoardModel.Events { get; }` into a
`MoveResolver`-owned log fed by an emit sink. `BoardModel` no longer exposes a public
mutable event list and — importantly — no longer takes `ScoreKeeper` as a direct callee
(as the blueprint's `TrySwap(..., ScoreKeeper)` and `score.OnMatchCleared(...)` inline call
did). `BoardModel` depends only on `IBoardEventSink`, preserving the one-directional
dependency arrow (Board Engine never depends on Feature-layer Scoring/Objectives —
`systems-index.md`, `board-engine.md` Overview). Scoring and Objectives observe the sink's
output; they are not wired into `BoardModel`.

### D3 — Two consumption modes, reconciled

There is exactly **one** sequence. It is consumed **twice**, at two different times, by two
different consumer sets. The bytes are identical; only the timing and the subset each reads
differ.

**Mode A — in-resolve logic consumption (synchronous, in emission order, Domain-internal).**
Scoring and Objectives must have fully-updated state by the instant `BoardStabilized` is
emitted (win/lose evaluation reads each tracker's `current`, and `get_current_score()` must
reflect every `MatchCleared` — `level-objectives.md` §8, §3). They cannot wait for the
presentation to drain the queue seconds later. So `MoveResolver.Emit` dispatches each event,
as it is appended, synchronously and in emission order to a **fixed, ordered** list of
Domain logic subscribers:

1. `ScoreKeeper` — consumes `MatchCleared` only (`scoring-stars.md` §1). Accrues
   `step_score` = `chain_index × (TILE_BASE_VALUE × |cleared| + Σ activation_bonus(special))`,
   reading each `PieceSnapshot.Special` **directly from `MatchCleared.ClearedPieces`**. It
   needs no side-channel; the blueprint's `OnSpecialConsumed`/`_pendingBonus` accumulator is
   **superseded** by scoring-stars.md Rev 2's "read `special_type` from `cleared_pieces`"
   rule. It ignores `SpecialActivated` to avoid double-counting.
2. `ObjectiveEvaluator` — consumes `SwapAccepted` (decrement moves, emit
   `MovesRemainingChanged`), `MatchCleared` (tally `collect_color` from `ClearedPieces`;
   read `get_current_score()` for `score_target`; emit `ObjectiveProgressed`), and
   `BoardStabilized` (evaluate win/lose; on WIN persist then emit `LevelResolved` — D4). It
   ignores `SpecialActivated` for tallying (`level-objectives.md` §4.3).

**The order [ScoreKeeper, ObjectiveEvaluator] is mandatory and pinned.** On a single
`MatchCleared`, a `score_target` tracker calls `get_current_score()`, which must already
include *this* step's `step_score` — so `ScoreKeeper` must process the event **before**
`ObjectiveEvaluator` on the same event (`level-objectives.md` §5). Registration order is the
mechanism; a determinism test asserts it.

Derived feature events (`ObjectiveProgressed`, `MovesRemainingChanged`, `LevelResolved`) are
emitted by `ObjectiveEvaluator` back through `MoveResolver.Emit`, so they land in the
authoritative log immediately after the board event that produced them. They have **no**
logic subscribers (Scoring and Objectives never react to them), so their dispatch is a pure
append — no re-entrancy loop is possible.

**Mode B — deferred presentation replay (immutable whole, Game/UI layer, drained).** After
`ResolveSwap` returns, the Game-layer `GameController` hands the immutable snapshot to
`EventBridge.EnqueueMove` and the Domain log is cleared for the next move. The presentation
consumers read the drained snapshot, never live Domain state:

- `JuiceDirector` (Game) — builds the Shadow Board Model and Reveal Queue from the board
  events (`juice-layer.md` §2, §3), reconstructing every historical piece from
  `PieceSnapshot` payloads. The one live-query exception is `BoardReshuffled` (§2), which
  the single-active-queue invariant makes safe.
- `HUD` (UI) — updates objective chips and the move counter from `ObjectiveProgressed` /
  `MovesRemainingChanged`, paced by the same replay so the bar climbs per clear.
- `ScreenFlowController` (Game/UI) — routes `LevelResolved` to the Results screen, gating
  the transition on `juice_input_lock` (D5; `screen-flow.md` T15/T16).

No presentation consumer ever mutates Domain state or calls back into the Domain to *decide*
anything gameplay-relevant (`juice-layer.md` §1; architecture §7.2 "never reverse").

### D4 — Win-persistence ordering (fixed 2026-07-18)

Inside `ObjectiveEvaluator`'s handling of `BoardStabilized`, the following happens **in this
exact order, synchronously, at resolve time** (Mode A — not during presentation drain):

1. Evaluate `outcome` (WIN check first, then LOSE — `level-objectives.md` §8, Formula 4).
2. Pull `IScoreProvider.GetScoreResults()` (`scoring-stars.md` §10a).
3. Compose `ResultsData` (Level Objective is the sole assembler — `level-objectives.md` §9).
4. **If `outcome == WIN`:** call `ISaveWriter.RecordLevelCompletion(levelId, stars, score)`
   — a Domain write interface the Game `SaveService` implements (keeps `ObjectiveEvaluator`
   engine-free). Never on LOSE.
5. Emit `LevelResolved(outcome, results)` into the sequence.

Step 4 strictly precedes step 5. Because persistence is requested at **resolve** time while
the Results *screen* transition is gated on `juice_input_lock` at **replay** time, the win
is durably recorded even if the app is killed mid-cascade-replay, yet the Results screen
never celebrates a win Save was not first asked to record (`level-objectives.md` §9;
`save-persistence.md`). This is the ordering guarantee the 2026-07-18 cross-GDD sync fixed.

### D5 — Input-lock composition (screen-flow Formula 5, four terms)

Effective board input is the AND of four independently-owned boolean terms, composed at a
single point — `InputGate` (Game assembly). No owner knows another's internals; each
contributes exactly one veto term.

```csharp
// SweetCascade.Game — the single composition point (screen-flow Formula 5).
bool EffectiveBoardInputEnabled =>
       _baseStateIsGameplay      // term 1
    &&  _boardInputEnabled       // term 2
    && !_overlayIsActive         // term 3
    && !_juiceInputLock;         // term 4
```

| # | Formula 5 term | Owner | Set when | How `InputGate` learns it |
|---|---|---|---|---|
| 1 | `base_state == GAMEPLAY` | `ScreenFlowController` (Game/UI) | every base-state transition (T1–T21) | `ScreenFlowController` writes it on transition |
| 2 | `board_input_enabled` | `BoardModel` (Domain, raw busy term) | `false` on entering `Swapping`; `true` again at `Idle` — **same frame**, synchronously | `GameController` brackets the synchronous `ResolveSwap` call and sets the term false→true around it (equivalently, mirrors the `BoardInputEnabledChanged` payload) |
| 3 | `!overlay_is_active` | `ScreenFlowController` (Game/UI) | overlay push/pop (max depth 2) | `ScreenFlowController` writes it |
| 4 | `!juice_input_lock` | `JuiceDirector` (Game) | `true` when the first Reveal Step is dequeued; `false` when `SETTLE_REVEAL` finishes | `JuiceDirector` fires `juice_input_lock_changed`; `InputGate` caches it |

`InputRouter` (Game) reads `EffectiveBoardInputEnabled` at the instant it would accept a
gesture (≤1-frame latency — `touch-input.md` TR-ti-003). Term 2 is `false` only during the
microscopic synchronous resolve window (within one frame), so in practice term 4
(`juice_input_lock`) is what actually holds input across the multi-frame cascade replay —
term 2 exists to reject re-entrant submission during resolve. This composition is
`screen-flow.md`'s canonical Formula 5; `juice-layer.md` §10 restates it. This ADR fixes
**where** it is composed (`InputGate`, Game) and **who owns each term**.

### D6 — Threading

**Everything in this ADR is single-threaded and runs on the Unity main thread. Stated
explicitly and unconditionally.** The Domain resolves each move synchronously on the main
thread; `MoveResolver.Emit`, all logic-subscriber dispatch, `EventBridge` hand-off, and all
presentation draining happen on the main thread. No `BoardEvent` is ever produced, appended,
dispatched, or consumed off the main thread. `EventBridge` needs no locks. RNG is
single-threaded (`rng-service.md` Edge Cases). Addressables and file IO use async handles but
are awaited on the main thread only at defined boot/level-entry points (architecture §7.4);
no gameplay or event state is ever touched from a worker thread. There is no thread boundary
anywhere in the event path — the "producer/consumer" split (architecture §7.1) is a
*temporal* split (resolve-now vs. replay-later), not a thread split.

### Architecture

```
                        SweetCascade.Domain  (pure C#, single-threaded, no UnityEngine)
  ┌──────────────────────────────────────────────────────────────────────────────────┐
  │  MoveResolver.ResolveSwap(a,b)  — runs the WHOLE move synchronously                 │
  │                                                                                    │
  │   BoardModel ──Emit(evt)──►  MoveResolver (IBoardEventSink)                         │
  │   (knows only the sink)          │  1. append evt to the authoritative ordered log  │
  │                                  │  2. dispatch, in fixed order, to logic subs:      │
  │                                  │       [ ScoreKeeper ] → [ ObjectiveEvaluator ]    │
  │                                  │           (Objectives may Emit derived feature    │
  │                                  │            events → appended in-position)          │
  │                                  ▼                                                   │
  │              authoritative IReadOnlyList<BoardEvent>  (MODE A consumed live above)   │
  └───────────────────────────────────────────┬────────────────────────────────────────┘
                                               │ returns immutable snapshot; log cleared
                                               ▼
  ┌──────────────────────────────  SweetCascade.Game / .UI  ────────────────────────────┐
  │  GameController ─► EventBridge.EnqueueMove(snapshot)   (single un-drained move only)  │
  │                          │                                                            │
  │        ┌─────────────────┼───────────────────────────┬───────────────────┐           │
  │        ▼ (MODE B drain)  ▼                            ▼                   ▼           │
  │  JuiceDirector      HUD (objective/moves)     ScreenFlowController   InputGate (D5)    │
  │  Shadow Board +     chips climb per clear      routes LevelResolved   4-term AND       │
  │  Reveal Queue                                  (gated on juice lock)                   │
  └───────────────────────────────────────────────────────────────────────────────────────┘
```

### Key Interfaces

The complete authoritative catalog. Implementations **copy from this ADR**; it is the single
source of truth for the `BoardEvent` records. All types live in `SweetCascade.Domain`
(`Assets/Domain/Events/`). Payload arrays are typed `IReadOnlyList<T>` and are **immutable by
construction** — never mutated after the record is emitted.

```csharp
// ── Shared value types ────────────────────────────────────────────────────────
public readonly record struct Cell(int Row, int Col);      // record struct → correct value equality/hash
                                                           // (supersedes the blueprint's hand-rolled R*16+C hash)

public enum SpecialType { None = 0, StripeH = 1, StripeV = 2, ColorBomb = 3, Wrapped = 4 /* reserved */ }
public static class ColorSentinel { public const int None = -1; }   // COLOR_NONE (board-engine §1)

// Full piece identity at the instant an event fires (board-engine §7). Color: index into
// the level color_pool, or ColorSentinel.None (-1). A value snapshot, never a live ref.
public readonly record struct PieceSnapshot(Cell Cell, int Color, SpecialType Special);

public enum RunOrientation { Horizontal, Vertical }
public readonly record struct Run(RunOrientation Orientation, int Length,
                                  IReadOnlyList<Cell> Cells, int Color);   // color carried directly (board-engine §4)

public enum TriggerSource      { SwapMatch, SpecialActivation, Bootstrap }
public enum SwapRejectReason   { NotAdjacent, NoMatchNoActivation }
public enum SpawnSource        { Bootstrap, CascadeRefill }
public enum Outcome            { Win, Lose }

// ── Base ──────────────────────────────────────────────────────────────────────
public abstract record BoardEvent;

// ── The 14 Board Engine signals (board-engine §7), one record each ─────────────
public sealed record BoardBootstrapped(int Rows, int Cols,
                                       IReadOnlyList<string> CellMask, int ManifestIndex) : BoardEvent;

public sealed record BoardInputEnabledChanged(bool Enabled) : BoardEvent;

public sealed record PiecesSpawned(IReadOnlyList<PieceSnapshot> Pieces, SpawnSource Source) : BoardEvent;
// Never emitted with an empty Pieces list (board-engine §7).

public sealed record SwapStarted(PieceSnapshot PieceA, PieceSnapshot PieceB) : BoardEvent;
// Pre-swap snapshots: PieceA/PieceB capture color/special immediately BEFORE the model swap.

public sealed record SwapRejected(Cell CellA, Cell CellB, SwapRejectReason Reason) : BoardEvent;
// Bare cells are sufficient: nothing was cleared/spawned/placed on a revert (board-engine §7).

public sealed record SwapAccepted(Cell CellA, Cell CellB, TriggerSource Trigger) : BoardEvent;
// Trigger ∈ { SwapMatch, SpecialActivation } only — never Bootstrap (bootstrap fires no swap).

public sealed record SpecialActivated(PieceSnapshot PieceA, PieceSnapshot PieceB,
                                      IReadOnlyList<PieceSnapshot> ClearedPieces) : BoardEvent;
// Fires immediately BEFORE the first MatchCleared of a SPECIAL_ACTIVATION move. ClearedPieces is
// seam-2's contribution ONLY (a subset of the following MatchCleared) — NOT summed for score/tally.

public sealed record MatchCleared(int ChainIndex, IReadOnlyList<PieceSnapshot> ClearedPieces,
                                  IReadOnlyList<Run> RunData, TriggerSource Trigger) : BoardEvent;
// The finalized clear set for one Clearing state. Each ClearedPieces entry = that cell's identity
// immediately before it cleared (carries pre-clear Special — the sole scoring/tally source).

public sealed record SpecialSpawned(Cell Cell, SpecialType Special, int Color, Run SourceRun) : BoardEvent;
// One per seam-3 exempted+transformed cell, within the same Clearing pass as its MatchCleared.
// Color = resolved spawn color (explicit override, or the source run's color; may be ColorSentinel.None).

public sealed record CascadeStepAdvanced(int ChainIndex) : BoardEvent;

public sealed record CascadeEnded(int FinalChainIndex, int TotalCellsCleared, TriggerSource Trigger) : BoardEvent;
// TotalCellsCleared is an aggregate; per-piece identity already appeared in this move's MatchCleared events.

public sealed record NoValidMovesDetected() : BoardEvent;   // no payload

public sealed record BoardReshuffled(int AttemptsUsed) : BoardEvent;
// The one event whose reveal step (juice-layer §2) is allowed a single live get_piece_at() sweep.

public sealed record BoardStabilized() : BoardEvent;        // no payload; objectives evaluate HERE (D3/D4)

// ── 3 Feature-layer events (level-objectives, scoring-stars) — same sequence ────
public sealed record ObjectiveProgressed(int ObjectiveIndex, string Type, int Current, int Target,
                                         float ProgressFraction, bool Completed) : BoardEvent;

public sealed record MovesRemainingChanged(int MovesRemaining, int MovesUsed, int MoveLimit,
                                           bool IsLowMoves) : BoardEvent;

public sealed record LevelResolved(Outcome Outcome, ResultsData Results) : BoardEvent;

// ── Level-resolution DTOs (Domain; assembled by ObjectiveEvaluator, D4) ─────────
public readonly record struct ScoreResults(long FinalScore, int StarsEarned,     // long (Int64) — NOT int:
                                           float ScoreProgressRatio, int ScoreProgressPercent);  // residue-sweep (a)

public readonly record struct ObjectiveResult(int ObjectiveIndex, string Type,
                                              IReadOnlyDictionary<string, object> Params,
                                              int Current, int TargetValue,
                                              float ProgressFraction, bool IsComplete);

public readonly record struct ClosestMissSummary(float ScoreProgressRatio, int ScoreProgressPercent
                                                 /* + objective-completion dimension: shape TBD, level-objectives Open Q */);

public readonly record struct ResultsData(string LevelId, Outcome Outcome, long ScoreEarned, int StarsEarned,
                                          ClosestMissSummary ClosestMiss,
                                          IReadOnlyList<ObjectiveResult> ObjectivesFinal);
```

**Deltas from the blueprint's §3.1 starting point** (applied so implementers do not copy the
incomplete version): `Trigger` added to `SwapAccepted`; `Reason` added to `SwapRejected`;
`MatchCleared` gains `RunData` + `Trigger` and its `(Cell,Piece)[]` becomes
`IReadOnlyList<PieceSnapshot>`; `PiecesSpawned` `bool Bootstrap` → `SpawnSource Source` and
`(Cell,Piece)[]` → `IReadOnlyList<PieceSnapshot>`; `SpecialSpawned` re-aligned to the GDD
payload `(Cell, Special, Color, SourceRun)`; `CascadeEnded` gains `TotalCellsCleared` +
`Trigger`; `BoardReshuffled` gains `AttemptsUsed`; **added** `BoardBootstrapped`,
`BoardInputEnabledChanged`, `SwapStarted`, `SpecialActivated`, `CascadeStepAdvanced`,
`NoValidMovesDetected`, plus the three feature events; score widened `int → long`.

**Ordering guarantee (single authoritative sequence).** The sequence is byte-identical —
event names, payloads, and order — under identical inputs (level file; seed or
`(level_id, attempt_number)`; ordered swap/cancel intents) across runs and across
iOS/Android/Web. This holds because: (a) `BoardModel` emission order is fixed by the §6
state machine and the two §7 ordering guarantees; (b) the logic-subscriber order is pinned
`[ScoreKeeper, ObjectiveEvaluator]`; (c) all `board-refill` draws follow the single fixed
call order (`board-engine.md` §6) through a platform-stable RNG (ADR-C); (d) no Domain code
reads wall-clock, frame count, or any thread/ordering-nondeterministic input. The two GDD
ordering guarantees are inherited verbatim and MUST be preserved by the emitter:

- **Special-activation move:** `SwapStarted → SwapAccepted → SpecialActivated →
  MatchCleared(1, SpecialActivation) → PiecesSpawned(CascadeRefill) → …(cascade from 2)… →
  CascadeEnded → BoardStabilized`.
- **Bootstrap ("move 0"):** `PiecesSpawned(Bootstrap) → [if accidental match]
  MatchCleared(1, Bootstrap) → PiecesSpawned(CascadeRefill) → … → CascadeEnded → [if step-9
  reshuffle] NoValidMovesDetected → BoardReshuffled → BoardBootstrapped →
  BoardInputEnabledChanged(true)`.

Presentation replay *pacing* (Juice deceleration curve, tween durations) is wall-clock-based
and is deliberately **not** part of the deterministic sequence — the sequence is
deterministic; its reveal timing is not, and need not be.

### Implementation Guidelines

- Put every catalog type in `Assets/Domain/Events/`. Keep `BoardModel` referencing only
  `IBoardEventSink` — it must not name `ScoreKeeper`, `ObjectiveEvaluator`, or any Feature
  type. A CI grep already fails the build on any `UnityEngine`/`UnityEditor` symbol in Domain
  (architecture §5.1); add a Domain-internal lint that `BoardModel` references no Feature
  namespace.
- `MoveResolver` registers logic subscribers in the fixed order `[ScoreKeeper,
  ObjectiveEvaluator]`. Encode this as a constant list, not registration timing, and assert
  it in a test.
- Treat all payload collections as immutable: build the array/list fully, then construct the
  record; never hand out a reference you retain and mutate. This is what makes "store now,
  replay later" safe (`board-engine.md` §13).
- `ObjectiveEvaluator` emits derived feature events through `MoveResolver.Emit` so they
  interleave correctly; it never appends to a private side-list.
- The `ISaveWriter.RecordLevelCompletion` call (D4) is a Domain interface; the Game
  `SaveService` implements it. `ObjectiveEvaluator` fires it once, in the fixed order, and
  neither inspects nor retries the result (`level-objectives.md` §9).
- `InputGate` (Game) is the *only* place the four Formula-5 terms are ANDed. No other code
  recombines them; consumers read `EffectiveBoardInputEnabled`.

## Alternatives Considered

### Alternative 1: Live C# `event` / delegate subscriptions across the Domain→Game boundary

- **Description**: `BoardModel` exposes C# `event`s (`event Action<MatchCleared> MatchCleared`
  …); the Game layer subscribes and handlers fire live during the synchronous resolve call.
- **Pros**: Familiar; no explicit buffer; zero snapshot allocation.
- **Cons**: Fires **all** presentation callbacks inside the synchronous resolve, when the
  board is already at its settled state — directly incompatible with the capture-then-replay
  model (`juice-layer.md` §1) and with a Shadow Board Model that must replay *historical*
  states. Encourages presentation handlers to query live Domain state (the exact anti-pattern
  §7.2 forbids). Cross-assembly live events also make the Domain hold delegates into Game
  types, risking the purity boundary.
- **Estimated Effort**: Similar.
- **Rejection Reason**: It breaks deferred replay at the semantic level. The GDDs' entire
  presentation architecture assumes a stored, replayable stream, not live callbacks.

### Alternative 2: Two separate streams (board events for Juice; a distinct progress stream for HUD)

- **Description**: `BoardModel.Events` stays board-only; Objectives/Scoring publish
  `ObjectiveProgressed`/`MovesRemainingChanged`/`LevelResolved` on a second channel.
- **Pros**: Keeps `BoardModel.Events` conceptually pure to Board Engine.
- **Cons**: The HUD must interleave the two streams by timestamp/position to climb "in
  lockstep with each visual clear" (`level-objectives.md` §5) — reintroducing an ordering
  merge the single-sequence model gives for free. Two sources of truth for "what happened
  this move" invite drift and complicate the byte-identical determinism guarantee.
- **Estimated Effort**: Higher (merge logic + its tests).
- **Rejection Reason**: A single authoritative interleaved sequence is simpler, is trivially
  deterministic as a whole, and matches how every presentation consumer wants to read it.

### Alternative 3: Keep the blueprint's `ScoreKeeper`-into-`TrySwap` direct-call model

- **Description**: `BoardModel.TrySwap(a, b, resolver, score)` calls `score.OnMatchCleared`
  inline (as `visual-interface-blueprint.md` §3.2 sketches), and seams call
  `score.OnSpecialConsumed`.
- **Pros**: Fewer moving parts; matches the existing spec-by-example.
- **Cons**: Makes Board Engine depend on the Feature-layer `ScoreKeeper` (a reverse
  dependency the GDDs and architecture forbid). The `OnSpecialConsumed`/`_pendingBonus`
  side-channel is redundant under scoring-stars.md Rev 2, which prices every activation from
  `MatchCleared.ClearedPieces` alone. It also gives no clean seat for Objectives, which need
  `SwapAccepted`/`BoardStabilized`, not just `MatchCleared`.
- **Estimated Effort**: Lower up front, higher later (dependency untangling).
- **Rejection Reason**: Violates the one-directional dependency arrow and duplicates a
  scoring input the finalized GDD removed. The `MoveResolver` sink keeps Board Engine
  dependency-free while still consuming logic synchronously.

## Consequences

### Positive

- QQ-01 and TD-ARCHITECTURE condition 3 are resolved: the complete catalog exists, so the
  Board Engine slice is end-to-end testable and Board Engine ↔ Juice can be built.
- Full `PieceSnapshot` identity on every clear/spawn/place payload makes deferred replay,
  the Shadow Board Model, and `collect_color` tallying implementable from stored events alone
  (`board-engine.md` §13; `juice-layer.md` §2; `level-objectives.md` §4) — zero live queries
  except the one reshuffle exception.
- One interleaved, byte-identical sequence per move is trivially deterministic as a whole and
  is exactly the shape every presentation consumer wants.
- Board Engine stays dependency-free (emits through a sink), preserving Domain purity and the
  one-directional arrow.
- The win is persisted at resolve time (D4), so a mid-replay app kill still records it, while
  the Results screen still waits for the cascade to finish.

### Negative

- Per move, the Domain allocates one immutable snapshot plus small records (a `MatchCleared`
  can carry up to `max_cells_per_step` — 81 on a 9×9 — `PieceSnapshot`s). Human-paced moves
  keep GC pressure low, but this is real allocation the profiling pass should watch (QQ-05).
- The pinned logic-subscriber order `[ScoreKeeper, ObjectiveEvaluator]` is a subtle
  correctness dependency; a future refactor that reorders it silently breaks `score_target`
  evaluation. Mitigated by an explicit test.
- `MoveResolver` is a new Domain orchestration type the architecture only implied; it must be
  built and owned.

### Neutral

- The architecture's illustrative `BoardModel.Events` property is refined into a
  `MoveResolver`-owned log fed via `IBoardEventSink`. No behavior in the GDDs changes.
- Feature events ride the same `BoardEvent` base type and sequence as board events; this is a
  modeling choice, not a semantic claim that Objectives are part of Board Engine.

## Risks

| Risk | Probability | Impact | Mitigation |
|------|------------|--------|-----------|
| Non-platform-stable RNG makes the "byte-identical across platforms" clause false | Medium | High | ADR-C mandates a SplitMix32-style platform-stable generator; determinism Edit-Mode tests assert identical sequences from identical seeds. |
| Logic-subscriber order regressed (Objectives before Scoring) → wrong `score_target` win timing | Low | High | Fixed constant order `[ScoreKeeper, ObjectiveEvaluator]`; a test asserts `get_current_score()` on a `MatchCleared` already includes that step. |
| A payload array mutated after emit corrupts a later replay | Low | High | `IReadOnlyList<T>` payloads, immutable-by-construction convention, `readonly record struct` value types; review checklist item. |
| Presentation queries live Domain during replay (wrong historical state) | Medium | Medium | Shadow Board Model is the only replay source (`juice-layer.md` §2); the sole live query is the reshuffle sweep, made safe by the single-active-queue invariant. |
| `EventBridge` buffers >1 move (input-lock bug lets a second move resolve early) | Low | Medium | Single-active-queue invariant asserted in `EnqueueMove`; the four-term gate (D5) keeps input locked for the whole replay. |
| Re-entrant `Emit` (a feature event triggering a logic subscriber) loops | Low | Medium | Derived feature events have no logic subscribers by rule; enforced by the fixed subscriber whitelist. |

## Performance Implications

| Metric | Before | Expected After | Budget |
|--------|--------|---------------|--------|
| CPU (frame time) | n/a (new) | Domain resolve is synchronous and off the per-frame path; a typical move resolves well under 1 ms (match detection is O(rows×cols) per cascade step, ≤ `MAX_CASCADE_DEPTH` steps). Presentation drain does bounded per-frame work paced by the reveal curve. | 16.6 ms/frame (technical-preferences.md) |
| Memory | n/a (new) | One per-move immutable snapshot + records; worst case a full-board `MatchCleared` ≈ 81 × `PieceSnapshot` (a few KB), transient and human-paced. No steady-state growth (single-active-queue). | ≤ 400 MB total (technical-preferences.md) |
| Load Time | n/a | No impact — the catalog is code, not loaded content. | — |
| Network | n/a | None — fully offline at MVP. | — |

Re-profiled on 2022-era mid-range Android at the Vertical Slice feel checkpoint alongside
the draw-call accounting (architecture QQ-05). If per-move allocation shows up, pooling the
event/snapshot buffers is the first, non-breaking optimization.

## Migration Plan

Greenfield — no existing system to migrate; this ADR precedes any Board Engine code.

1. Create `Assets/Domain/Events/` and add the full catalog exactly as in **Key Interfaces**.
   Verify it compiles in the Edit-Mode runner (Domain asmdef, `noEngineReferences`).
2. Refactor the blueprint's `BoardModel` sketch to emit through `IBoardEventSink`; drop the
   `ScoreKeeper` parameter and the inline `OnMatchCleared`/`OnSpecialConsumed` calls.
3. Implement `MoveResolver` with the fixed subscriber order and the derived-event interleave.
4. Implement `EventBridge` (Game) and the drained-snapshot hand-off; add the single-queue
   assertion.
5. Implement `InputGate` (Game) with the four owned terms (D5).
6. Add determinism tests: identical `(level, seed, intents)` → byte-identical sequence; and
   the Scoring-before-Objectives ordering test.

**Rollback plan**: The catalog is additive greenfield code; "rollback" is deleting
`Assets/Domain/Events/` and the three new Game types before any slice depends on them. Once a
slice ships against the catalog, changes go through a superseding ADR (append-only discipline
mirrors the manifest/schema conventions elsewhere in the project).

## Validation Criteria

- [ ] `SweetCascade.Domain` compiles the full catalog headlessly with zero
      `UnityEngine`/`UnityEditor` symbols (CI Domain-purity check passes).
- [ ] Every `board-engine.md` §7 signal has exactly one record; every payload field is
      present and typed; every clear/spawn/place payload carries full `PieceSnapshot`
      identity.
- [ ] A stored move sequence replays to the correct visual + per-color tally using **only**
      stored payloads (reproduces `board-engine.md` §13's 2-step red-tile walkthrough).
- [ ] Determinism test: identical `(level file, seed, ordered intents)` yields a
      byte-identical `IReadOnlyList<BoardEvent>` (names, payloads, order) across repeated
      runs; ADR-C makes this hold across platforms.
- [ ] Ordering test: on any `MatchCleared`, `get_current_score()` read by
      `ObjectiveEvaluator` already includes that step's `step_score` (Scoring-before-
      Objectives).
- [ ] Win-persistence test: on a WIN `BoardStabilized`, `RecordLevelCompletion` is observed
      strictly before `LevelResolved` is appended; on LOSE it is never called.
- [ ] `InputGate` returns `false` if any one of the four terms vetoes; input is locked for a
      full multi-step cascade replay and re-enabled exactly at `SETTLE_REVEAL` completion.
- [ ] `EventBridge.EnqueueMove` asserts no un-drained prior move exists (single-active-queue).

## GDD Requirements Addressed

| GDD Document | System | Requirement | How This ADR Satisfies It |
|-------------|--------|-------------|--------------------------|
| `design/gdd/board-engine.md` | Board Engine | §7 full signal catalog with `PieceSnapshot` identity; two ordering guarantees | The complete `BoardEvent` catalog (Key Interfaces) types one record per signal with full identity; the ordering guarantees are inherited verbatim as emitter invariants. |
| `design/gdd/board-engine.md` | Board Engine | §12/§13 determinism + deferred-replay payload sufficiency | Single authoritative byte-identical sequence (D2, ordering guarantee); immutable full-identity payloads make replay-from-stored-events safe (D1, D3 Mode B). |
| `design/gdd/juice-layer.md` | Juice Layer | §1–3 capture-then-replay: Shadow Board Model + single Reveal Queue drained per move | Drained immutable snapshot per move (D1); single-active-queue invariant (D1); presentation reads snapshot only (D3 Mode B). |
| `design/gdd/juice-layer.md` | Juice Layer | §10 `juice_input_lock` as Formula-5 term 4 | `InputGate` composes term 4, owned by `JuiceDirector` (D5). |
| `design/gdd/level-objectives.md` | Level Objectives | §5 `ObjectiveProgressed` interleaved per clear; §6 move accounting; §9 sole `ResultsData` assembler | Feature events emitted in-position into the one sequence (D2/D3); `ObjectiveEvaluator` is the fixed-order logic subscriber that assembles `ResultsData` (D4). |
| `design/gdd/level-objectives.md` | Level Objectives | §9 win-persistence ordering (Save write before `level_resolved`), fixed 2026-07-18 | D4 pins steps 1–5; `RecordLevelCompletion` strictly precedes `LevelResolved`, at resolve time. |
| `design/gdd/scoring-stars.md` | Scoring & Stars | §1 score from `match_cleared` only; §10a pull API; 64-bit score | `ScoreKeeper` consumes `MatchCleared` only, first in subscriber order (D3); `ScoreResults.FinalScore`/`ResultsData.ScoreEarned` are `long` (residue-sweep item a). |
| `design/gdd/screen-flow.md` (via architecture §7.2/§8.5) | Screen Flow | Formula 5 four-term input-lock composition; `overlay_is_active`/`base_state` ownership | D5 names an owner per term and fixes `InputGate` (Game) as the single composition point. |
| `docs/architecture/architecture.md` | Master architecture | §11 ADR-D scope; §10.3 TD condition 3 (event catalog completion); QQ-01 | This ADR is ADR-D; it completes the catalog and discharges the condition/QQ-01. |

## Related

- **Depends on**: `docs/architecture/adr-001-engine-selection-unity.md` (Accepted); ADR-C
  (Deterministic RNG) for the cross-platform byte-identical clause.
- **Derived from**: `docs/architecture/architecture.md` §§4–8, 11; `docs/architecture/visual-interface-blueprint.md` §3.1 (the C# starting point this catalog completes and corrects).
- **Peer Foundation ADRs**: ADR-A (Addressables), ADR-B (Save serialization), ADR-E (Level
  manifest) — architecture §11.
- **Source GDDs**: `design/gdd/board-engine.md`, `design/gdd/juice-layer.md`,
  `design/gdd/level-objectives.md`, `design/gdd/scoring-stars.md`.
- **Future code**: `Assets/Domain/Events/*` (catalog), `Assets/Domain/.../MoveResolver.cs`,
  `Assets/Game/Bridge/EventBridge.cs`, `Assets/Game/Input/InputGate.cs`.
