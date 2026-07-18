# Sweet Cascade — Master Architecture

> Translates all 11 approved MVP GDDs into a concrete Unity 6.3 LTS / C#
> technical blueprint. This document sits between design and implementation and
> must exist before sprint planning begins. It does not restate the GDDs — it
> makes the architectural calls that satisfy them.

## Document Status

- **Version:** 1.0
- **Status:** Reviewed — **CONCERNS** (`/architecture-review` 2026-07-18). Traceability: 42 TRs,
  40 covered, 2 Presentation-tier partials (pending ADR-G/ADR-H), 0 uncovered gaps. All five
  Foundation ADRs (A–E = ADR-002…006) Accepted; dependency graph acyclic; engine-consistent. One
  cross-ADR conflict to reconcile before the BootLoader slice: **ADR-002 (manifests in the
  Core Addressables group) vs ADR-006 (manifests loaded by direct reference)** — engine reference
  favors ADR-006; amend ADR-002. Still awaiting founder sign-off.
  See `docs/architecture/architecture-review-2026-07-18.md`.
- **Last Updated:** 2026-07-18
- **Engine:** Unity 6.3 LTS (6000.3.x) · C# · URP (Render Graph path only) — per ADR-001
- **GDDs Covered (11, all APPROVED 2026-07-18):** rng-service · level-data-format ·
  touch-input · board-engine (Rev 2) · special-candies · scoring-stars (Rev 2) ·
  level-objectives (Rev 2) · juice-layer · save-persistence · screen-flow · world-map
- **Source blueprint:** `docs/architecture/visual-interface-blueprint.md`
  (founder-approved 3D "glass candy" target; seeds the Domain layer)
- **Cross-GDD review:** `design/gdd/reviews/all-gdds-review-2026-07-18.md` — **PASS**,
  cleared for `/create-architecture`
- **ADRs Referenced:** ADR-001 (Engine Selection — Unity 6.3 LTS, Accepted)
- **Technical Director Sign-Off:** deferred to founder review (TD self-review recorded in
  §"ADR Audit + TD Self-Review"; verdict: **APPROVE WITH CONDITIONS**)
- **Lead Programmer Feasibility:** LP-FEASIBILITY skipped — Lean review mode (not a PHASE-GATE).

---

## Table of Contents

1. Engine Knowledge Gap Summary
2. Godot → Unity Residue Sweep (mandated by the cross-GDD advisory)
3. Technical Requirements Baseline
4. System Layer Map
5. Assembly Architecture (SweetCascade.Domain / .Game / .UI)
6. Module Ownership
7. Data Flow
8. API Boundaries
9. Rendering Architecture & Draw-Call Budget
10. ADR Audit + TD Self-Review
11. Required ADRs
12. Architecture Principles
13. Open Questions

---

## 1. Engine Knowledge Gap Summary

**Engine:** Unity 6.3 LTS (6000.3.x), released December 2025.
**LLM training reliably covers:** ~Unity 2022 LTS (2022.3), with partial 6.0/6.1.
**Post-cutoff versions in scope:** 6.0 (Oct 2024), 6.1, 6.2, 6.3 LTS (Dec 2025).

Every recommendation touching a HIGH/MEDIUM risk domain below was cross-referenced
against `docs/engine-reference/unity/` and is flagged inline where it appears.

### HIGH RISK domains (verify against engine reference before acting)

| Domain | Post-cutoff change (6.0→6.3) | Systems that touch it |
|---|---|---|
| **URP Rendering** | Render Graph is **mandatory** in 6.3 — `RenderGraphSettings.enableRenderCompatibilityMode` is read-only `false`; custom passes use `RecordRenderGraph(RenderGraph, ContextContainer)`, **not** `Execute(ScriptableRenderContext, ref RenderingData)`; unified URP/HDRP shader compiler; new Bloom filtering (Kawase / Dual). | Juice Layer (bloom emissive pass), 13-material glass-candy set, board rig rendering |
| **UI Toolkit** | Production-ready for runtime UI; **USS parser is stricter** in 6.3 (previously-tolerated invalid USS now raises validation errors — lint USS during implementation); `AccessibilityRole` converted from a flags enum to a standard enum (no bitwise combining). | Game UI/Screens Flow (HUD, overlays, Results), accessibility pass |
| **Input System** | Legacy `Input` class deprecated; unified pointer via Input System package; touch via `EnhancedTouch`. | Touch & Input System |
| **Addressables** | 6.2+ — asset-load failures **throw by default** instead of returning `null`; async `AsyncOperationHandle`; reference-counted release required. | Level Data Format loading, Level Manifest, region assets |

### MEDIUM RISK domains

| Domain | Change | Systems |
|---|---|---|
| **Persistence / File IO** | `Application.persistentDataPath` replaces Godot's `user://`; no atomic-`rename` guarantee on WebGL (IndexedDB-backed virtual FS) — the save GDD's A/B double-buffer already assumes this. C# BCL (`System.IO`, `System.Text`) is stable and low-risk itself. | Save & Persistence |
| **Particles / VFX** | Legacy Particle System deprecated in favour of VFX Graph; 6.3 adds VFX Graph GPU-event instancing (relevant to cascade FX at scale). Shuriken Particle System still supported for the pooled 2D-style bursts. | Juice Layer |

### LOW RISK domains (in training data, reliable)

- **The pure-C# Domain layer** (`BoardModel`, `SpecialResolver`, `ScoreKeeper`,
  objective evaluation, scoring, save serialization logic, RNG formulas). Zero
  `UnityEngine` references, standard C#/.NET, no engine-version exposure. This is
  deliberately the largest and most test-covered part of the codebase — see §5.

**Determinism caveat (raised loudly, see §2 and §11 QQ-04):** `System.Random`'s
algorithm is **not** guaranteed byte-stable across .NET runtimes/platforms. The
blueprint's `new Random(seed)` is a spec-by-example placeholder only. Production
board RNG MUST implement `rng-service.md`'s F1–F6 with an explicit, platform-stable
SplitMix32-style finalizer — this is the mechanical backbone of Pillar 2 and of every
deterministic Edit-Mode test.

---

## 2. Godot → Unity Residue Sweep

The aggregated advisory in `design/gdd/reviews/all-gdds-review-2026-07-18.md`
("Godot residue in GDD engine notes … no design depends on a Godot-only capability")
is discharged here. Every Godot-specific mechanism named across the GDDs maps to a
Unity 6.3 equivalent with **no loss of the underlying design contract**:

| Godot mechanism (in GDD text) | Unity 6.3 equivalent | Contract preserved? |
|---|---|---|
| `Resource` / `.tres` (`LevelData`, `LevelManifest`) with `@export` fields | `ScriptableObject` with `[SerializeField]` / public fields; Inspector editing preserved | ✅ Designer Inspector-editing workflow (level-data-format §5) intact |
| Godot signals (board event catalog, snake_case past tense) | Plain C# `record` events drained from `BoardModel.Events` (already so in the blueprint); Game layer subscribes/replays | ✅ Deferred-replay + full-piece-identity payloads intact (board-engine §7/§13) |
| gdUnit4 (`godot --headless --script …`) | Unity Test Framework — Edit Mode (headless domain) + Play Mode (integration); `game-ci/unity-test-runner@v4` | ✅ Determinism + AC test lists intact (tests/README.md) |
| `user://save/profile_*.sav` | `Application.persistentDataPath + "/save/…"` | ✅ Per-install sandboxed path; A/B double-buffer intact (save-persistence §4/§9) |
| `FileAccess` flush/sync durability | `System.IO.FileStream.Flush(true)` / atomic A/B write ladder | ✅ Durability guarantee is an implementation ADR item (save-persistence Open Q) |
| GDScript `int` (64-bit) for `final_score` | C# **`long`** (Int64) — NOT C# `int` (32-bit). The blueprint's `int FinalScore` must widen to `long` per scoring-stars §9 | ✅ Maps cleanly; flagged so the literal blueprint snippet is corrected on port |
| `GPUParticles3D` / `CPUParticles2D` | Particle System (Shuriken) for pooled bursts; VFX Graph for GPU-event cascade FX at scale | ✅ Atlas-packed single-material budget intact (juice-layer §9) |
| `MultiMeshInstance3D` (64 wells) | GPU Instancing (`Graphics.RenderMeshInstanced` / SRP Batcher + per-instance `MaterialPropertyBlock`) | ✅ One-instanced-draw well grid intact (§9 budget) |
| Godot app-paused / focus-lost notifications | `MonoBehaviour.OnApplicationPause(bool)` / `OnApplicationFocus(bool)` | ✅ `flush_if_dirty()` + auto-Pause triggers intact (screen-flow T9/T20) |
| `Label3D` / `TextMesh` score popups | TextMeshPro (world-space) or UI Toolkit label (decision = ADR, §11) | ✅ Popup-window timing owned by Juice; value by Scoring |
| `godot-gdscript-specialist` routing | `unity-specialist` / `unity-shader-specialist` / `unity-ui-specialist` / `unity-addressables-specialist` (technical-preferences.md) | ✅ Specialist routing intact |

**Result — no un-mappable contract found.** Every Godot mechanism in the GDDs has a
1:1 Unity 6.3 equivalent that preserves the design intent. Two items require a literal
source edit when the blueprint scripts are ported (both map cleanly, neither is a
design change): (a) `ScoreKeeper.FinalScore` widens `int → long`; (b) `new Random(seed)`
is replaced by the deterministic `RngService` per rng-service.md. Both are captured in
the Required ADR list (§11).

---

## 3. Technical Requirements Baseline

Extracted from 11 GDDs. Every requirement below is answered by an architectural
decision in §§4–9 and/or an ADR in §11. `Domain` = pure-C# assembly; `Game` =
MonoBehaviour/engine-integration assembly; `UI` = UI Toolkit assembly.

| Req ID | GDD | Requirement | Assembly / Layer |
|---|---|---|---|
| TR-rng-001 | rng-service | Named, independently-seeded streams; isolation guarantee (draw on A never perturbs B) | Domain / Foundation |
| TR-rng-002 | rng-service | Platform-stable seed derivation (F1–F3 + `mix32` avalanche) — byte-identical across iOS/Android/Web; **not** `System.Random` | Domain / Foundation |
| TR-rng-003 | rng-service | `next_float/int/color/shuffle`, `fork_stream`, session log (`get_session_log`) API | Domain / Foundation |
| TR-rng-004 | rng-service | `level_id` supplied as **int** (never runtime string-hash) | Domain (via manifest resolve) |
| TR-ldf-001 | level-data-format | Per-level data as a versioned schema (`schema_version`, closed enums, migration policy) | Domain schema + Game (SO) / Foundation |
| TR-ldf-002 | level-data-format | Designer Inspector-editable, one file per level, region subdirectories | Game (ScriptableObject) / Foundation |
| TR-ldf-003 | level-data-format | V1–V19 validation suite (blocking + advisory); connectivity flood-fill (V8) | Domain (pure) + Editor test |
| TR-ldf-004 | level-data-format | Additive `display_name` (v1.1, no bump) per adoption ledger | Domain schema |
| TR-be-001 | board-engine | Headless, synchronous, fully-deterministic resolution state machine (zero `await`/timer) | Domain / Core |
| TR-be-002 | board-engine | Four extension seams (activation-check / activation-clears / special-spawns / chain-expansion) with no-op MVP defaults | Domain / Core |
| TR-be-003 | board-engine | Full event catalog w/ `PieceSnapshot` identity for deferred replay; `board_input_enabled_changed` | Domain / Core |
| TR-be-004 | board-engine | Level Manifest (String→append-only ordinal) resolution; termination caps (`MAX_CASCADE_DEPTH`, `MAX_CHAIN_EXPANSION_ITERATIONS`) | Domain + generated asset |
| TR-be-005 | board-engine | Segment-scoped gravity/refill; retry-on-bootstrap vs plain-uniform-cascade asymmetry; reshuffle | Domain / Core |
| TR-sc-001 | special-candies | Combo matrix (Formulas 4–7) + passive detonation (deterministic most-common-color, F8) via the 4 seams, zero Board Engine change | Domain / Feature |
| TR-sc-002 | special-candies | Append-only `special_type` vocabulary; `WRAPPED=4` reserved | Domain / Feature |
| TR-sc-003 | special-candies | Harvest Observation Point (per-color identity preserved on every clear path) | Domain (event payload) |
| TR-ss-001 | scoring-stars | `step_score` from `match_cleared` only; unified per-piece activation bonus; linear chain multiplier | Domain / Feature |
| TR-ss-002 | scoring-stars | `long` score accumulator (64-bit); `REFERENCE_SCORE_PER_MOVE(K)` table | Domain / Feature |
| TR-ss-003 | scoring-stars | Pull API: `get_current_score()`, `get_score_results() -> ScoreResults` | Domain / Feature |
| TR-lo-001 | level-objectives | Objective tracker registry (`score_target`, `collect_color`), field normalization, extensible handler map | Domain / Feature |
| TR-lo-002 | level-objectives | Win/lose evaluated **only** at `board_stabilized`; last-move-cascade win | Domain / Feature |
| TR-lo-003 | level-objectives | Assembles `ResultsData`; fires `level_resolved`; calls `record_level_completion()` on WIN **before** the event | Domain + Game bridge |
| TR-lo-004 | level-objectives | Move accounting on `swap_accepted`; `moves_remaining_changed`, `objective_progressed` | Domain / Feature |
| TR-ti-001 | touch-input | Gesture → intent (`select_cell`/`swap_request`/`cancel`); swipe threshold + dominant-axis | Game (Input System) / Core |
| TR-ti-002 | touch-input | Unified touch+mouse pointer; hover additive-only; single-active-touch | Game / Core |
| TR-ti-003 | touch-input | Reads composed `effective_board_input_enabled` gate; ≤1 frame latency; ≥44px hit target | Game / Core |
| TR-jl-001 | juice-layer | Capture-then-replay: Shadow Board Model + Reveal Queue, single active queue | Game / Presentation |
| TR-jl-002 | juice-layer | Owns `juice_input_lock_changed`; gravity re-derivation from events; one live-query reshuffle exception | Game / Presentation |
| TR-jl-003 | juice-layer | Audio hook map (runtime pitch-shift), haptics map, reduced-motion + WCAG 3Hz flash-safety | Game / Presentation |
| TR-jl-004 | juice-layer | VFX sub-budget (`JUICE_VFX_DRAW_CALL_BUDGET=40`), 4-tier LOD degradation, atlas single-material | Game / Presentation |
| TR-sf-001 | screen-flow | 2-layer (base+overlay) state machine, 9 legal composites, T1–T21 transitions | Game / Presentation |
| TR-sf-002 | screen-flow | Formula 5 four-term input-lock composition; owns `overlay_is_active`, `attempt_number` | Game / Presentation |
| TR-sf-003 | screen-flow | Results transition waits on `juice_input_lock` w/ safety ceiling; retry skips Pre-Level Card | Game / Presentation |
| TR-sf-004 | screen-flow | Per-screen data contract (reads Save/LevelData; never writes level results directly) | Game + UI |
| TR-sp-001 | save-persistence | JSON at `persistentDataPath`; compact canonical bytes; FNV-1a checksum | Domain (serialize) + Game (IO) / Foundation |
| TR-sp-002 | save-persistence | Atomic A/B double-buffer (no rename dependency); corruption ladder; tamper detect+accept | Domain + Game / Foundation |
| TR-sp-003 | save-persistence | Monotonic `LevelRecord` merge; self-healing `total_stars`; additive schema migration | Domain / Foundation |
| TR-sp-004 | save-persistence | `record_level_completion` / `load_profile` / `get_profile` / `update_setting` / `flush_if_dirty` API | Domain + Game / Foundation |
| TR-wm-001 | world-map | Region/node graph; star-gated unlocks; MVP placeholder linear list in `WORLD_MAP` base state | Domain (data) + UI |
| TR-wm-002 | world-map | W6–W7 manifest cross-validation (every authored level ↔ manifest ordinal) | Editor-time check |
| TR-perf-001 | technical-preferences | 60fps / 16.6ms; ≤100 draw calls heaviest cascade; ≤400MB; no realtime shadows (blob decals); one directional light | Game / Presentation (§9) |
| TR-perf-002 | technical-preferences | URP Render Graph path only; Input System only; UI Toolkit; domain assembly zero-`UnityEngine` | All (§5) |

**Coverage:** every TR above is addressed by a decision in §§4–9 or a Required ADR
in §11. No TR is left without an architectural home.

---

## 4. System Layer Map

The 11 MVP systems mapped onto the standard game-architecture layers. The distinctive
call for Sweet Cascade is that **the entire logic core is a pure-C# Domain assembly**
that runs and tests outside Unity (board-engine §13's logic/presentation boundary,
elevated to an assembly boundary — see §5).

```
┌──────────────────────────────────────────────────────────────────────────────┐
│  PRESENTATION LAYER                                          [Game + UI asm]   │
│  • Juice Layer (Reveal Queue, Shadow Board Model, VFX/audio/haptics)          │
│  • Game UI/Screens Flow (base+overlay state machine, HUD, Results)   [UI]     │
│  • World Map view (MVP placeholder linear list)                     [UI]      │
├──────────────────────────────────────────────────────────────────────────────┤
│  FEATURE LAYER                        logic → [Domain]  ·  view → [Game/UI]    │
│  • Special Candies & Combo Matrix (seams 1/2/3/4)          [Domain]           │
│  • Scoring & Star Thresholds (ScoreKeeper, pull API)        [Domain]           │
│  • Level Objective & Move-Limit (trackers, ResultsData)     [Domain]           │
├──────────────────────────────────────────────────────────────────────────────┤
│  CORE LAYER                                                                    │
│  • Match-3 Board Engine (BoardModel state machine, events)  [Domain]          │
│  • Touch & Input System (gesture→intent)                    [Game: Input Sys] │
├──────────────────────────────────────────────────────────────────────────────┤
│  FOUNDATION LAYER                                                              │
│  • RNG Service (streams, F1–F6, session log)                [Domain]          │
│  • Level Data Format (schema, validation) + LevelData SO    [Domain + Game]   │
│  • Level Manifest (String→ordinal, generated asset)         [Domain + Editor] │
│  • Save & Persistence (serialize+checksum / file IO)        [Domain + Game]   │
│  • Event Bridge / Deferred-Replay Bus                       [Game]            │
│  • Scene/Screen boot + Addressables loader                  [Game]            │
├──────────────────────────────────────────────────────────────────────────────┤
│  PLATFORM LAYER                                             [Unity 6.3 API]    │
│  URP Render Graph · Input System · UI Toolkit · Addressables ·                 │
│  Application.persistentDataPath · Particle System / VFX Graph · TextMeshPro    │
└──────────────────────────────────────────────────────────────────────────────┘
```

**Engine-awareness check (Core + Foundation layers):**

- **Touch & Input (Core)** → Input System (HIGH risk). Uses the unified pointer /
  `EnhancedTouch` API, NOT the legacy `Input` class (deprecated, forbidden by
  technical-preferences.md). Verified against `modules/input.md`.
- **Addressables loader (Foundation)** → Addressables (HIGH risk). Load failures
  **throw** in 6.2+; loader wraps every load in try/catch or `TryLoad`, per
  `plugins/addressables.md`. Verified.
- **Save file IO (Foundation)** → `System.IO` (LOW/MEDIUM risk, BCL-stable) at
  `Application.persistentDataPath`. WebGL has no atomic rename — the A/B scheme
  already avoids depending on one.
- **RNG Service / BoardModel / ScoreKeeper (Core+Foundation logic)** → pure C# (LOW
  risk), zero engine surface. The only engine-version exposure in the whole logic
  core is *none* by construction.

---

## 5. Assembly Architecture

The board-engine.md logic/presentation separation (§13) is elevated from a code
convention to a **compiler-enforced assembly boundary**. Three production assemblies
plus test/editor assemblies, with a strict one-way dependency direction.

### 5.1 Assemblies & dependency direction

```
        ┌──────────────────────────┐        ┌──────────────────────────┐
        │   SweetCascade.UI (asm)  │        │  SweetCascade.Editor(asm)│
        │  UI Toolkit: UXML/USS,   │        │  Manifest gen, LevelData │
        │  HUD, overlays, Results  │        │  validation, Level       │
        └────────────┬─────────────┘        │  Preview tool (EditorWin)│
                     │ references            └────────┬─────────┬───────┘
                     ▼                                │         │
        ┌──────────────────────────┐   references     │         │
        │  SweetCascade.Game (asm) │◄─────────────────┘         │
        │  Views, JuiceDirector,   │                            │
        │  InputRouter (InputSys), │                            │
        │  ScreenFlowController,   │                            │
        │  SaveService (file IO),  │                            │
        │  Addressables loader,    │                            │
        │  EventBridge             │                            │
        └────────────┬─────────────┘                            │
                     │ references                                │ references
                     ▼                                            ▼
        ┌──────────────────────────────────────────────────────────────┐
        │             SweetCascade.Domain (asm)  — PURE C#              │
        │  ZERO UnityEngine / UnityEditor references (asmdef enforced)  │
        │  BoardModel · SpecialResolver · ScoreKeeper ·                 │
        │  ObjectiveEvaluator · RngService · LevelData(POCO) ·          │
        │  SaveModel + serializer + FNV-1a · BoardEvent records ·       │
        │  LevelManifest(POCO) · ResultsData/ScoreResults DTOs          │
        └──────────────────────────────────────────────────────────────┘
                     ▲                          ▲
                     │ references               │ references
        ┌────────────┴───────────┐   ┌──────────┴─────────────────────┐
        │ SweetCascade.Domain.   │   │ SweetCascade.Game.Tests (asm)  │
        │ Tests (Edit Mode asm)  │   │ Play Mode — event replay,      │
        │ NUnit; the bulk of     │   │ input routing, save round-trip │
        │ coverage; runs headless│   │ (references Game + Domain)     │
        └────────────────────────┘   └────────────────────────────────┘
```

**Invariant (compiler-enforced by asmdef, CI-guarded):**
`Domain` references **nothing above it and no engine assembly**. `Game → Domain`.
`UI → Game` (and may reference `Domain` for pure DTO/record types — a *downward*
reference, still acyclic). No assembly ever references one above it. The `Domain`
asmdef sets **no** engine assembly references and `noEngineReferences: true`; a CI
check greps the compiled `Domain` for `UnityEngine`/`UnityEditor` symbols and fails
the build if any appear (enforces technical-preferences.md's forbidden pattern).

### 5.2 Why an assembly boundary, not just namespaces

1. **Determinism & headless testability** — the Domain compiles and runs in a plain
   .NET test runner (Edit Mode uses it directly). A test calls `board.TrySwap(...)`
   and asserts on `board.Events` + final grid with no scene, no frame simulation
   (board-engine §13). tests/README.md's Edit-Mode-heavy layout depends on this.
2. **Provable Pillar 2** — no gameplay/scoring/RNG code can read wall-clock, frame
   count, input, or player-performance signals, because those types are not even
   linkable from Domain. "Clever, Never Cheated" becomes architecturally true, not
   just reviewed.
3. **Reversibility** — the Domain is engine-neutral (ADR-001's rollback pillar). A
   future engine change re-writes only Game/UI; Domain moves unchanged.

### 5.3 Repository / folder layout

```
src/SweetCascade/                         # the Unity project (Unity 6.3 LTS, URP Render Graph)
  Assets/
    Domain/            SweetCascade.Domain.asmdef      (noEngineReferences)
      Board/  Specials/  Scoring/  Objectives/  Rng/  Levels/  Save/  Events/
    Game/              SweetCascade.Game.asmdef
      Views/  Juice/  Input/  ScreenFlow/  Save/  Loading/  Bridge/
    UI/                SweetCascade.UI.asmdef
      Screens/*.uxml  *.uss   HUD/  Overlays/
    Editor/            SweetCascade.Editor.asmdef
      ManifestGen/  LevelValidation/  LevelPreview/
    Art/  Materials/  Prefabs/  Scenes/  AddressableGroups/
    Tests/
      EditMode/        SweetCascade.Domain.Tests.asmdef
      PlayMode/        SweetCascade.Game.Tests.asmdef
assets/data/levels/<region_code>/*.asset  # LevelData ScriptableObjects (Addressable)
assets/data/level_manifest.asset          # generated LevelManifest ScriptableObject
```

---

## 6. Module Ownership

Per layer: **Owns** (sole authority over this state) · **Exposes** (readable/callable
by others) · **Consumes** (reads/calls elsewhere) · **Engine APIs** (with risk flag).

### Foundation Layer

| Module (asm) | Owns | Exposes | Consumes | Engine APIs |
|---|---|---|---|---|
| **RngService** (Domain) | Per-stream generator state; stream registry; session log record | `NextFloat/Int/Color/Shuffle`, `Fork`, `StartLevelSession(int,int)`, `GetSessionLog()` | Opaque caller params only | none (pure C#) |
| **LevelData schema** (Domain POCO) | Field shapes, defaults, V1–V19 rules | Typed level record; `Validate()` | — | none |
| **LevelDataAsset** (Game SO) | Serialized level content on disk | `ToDomain() : LevelData` | Addressables load | `ScriptableObject` ⚠️ standard; `Addressables.LoadAssetAsync` ⚠️ 6.2+ throws (HIGH) |
| **LevelManifest** (Domain POCO + generated SO) | Append-only `entries: List<string>`; ordinal resolution | `ResolveOrdinal(level_id) : int` | — | `ScriptableObject`; Editor generates the asset |
| **SaveModel + serializer** (Domain) | Profile schema, canonical bytes, FNV-1a, monotonic merge, A/B selection logic | `Serialize()`, `Deserialize()`, `Merge()`, `Checksum()` | — | none |
| **SaveService** (Game) | The two on-disk slot files; disk read/write timing; dirty flag | `LoadProfile`, `GetProfile`, `RecordLevelCompletion`, `UpdateSetting`, `FlushIfDirty` | SaveModel; app lifecycle | `Application.persistentDataPath`, `System.IO` (MEDIUM); `OnApplicationPause/Focus` |
| **EventBridge** (Game) | Per-move ordered event buffer handed Domain→presentation | `EnqueueMove(IReadOnlyList<BoardEvent>)` | Domain `BoardModel.Events` | none (plain C#) |
| **BootLoader** (Game) | App cold-start sequence; Addressables init | — | SaveService, Addressables | `Addressables`, scene load (HIGH) |

### Core Layer

| Module (asm) | Owns | Exposes | Consumes | Engine APIs |
|---|---|---|---|---|
| **BoardModel** (Domain) | Grid truth, swap execution, match detection, gravity/refill, cascade loop, reshuffle, `chain_index`, event emission | `TrySwap`, `At`, `GetCellState`, `HasAvailableMove`, `Events`, query API | RngService (`board-refill`), SpecialResolver (via seams); ScoreKeeper decoupled — consumes events via IBoardEventSink (ADR-005 D2) | none |
| **InputRouter** (Game) | Gesture state machine (Idle/AwaitingSecond); swipe threshold; dominant-axis | Emits `SelectCell`/`SwapRequest`/`Cancel` C# events | `effective_board_input_enabled` gate; grid dims/`cell_size_px` | Input System `EnhancedTouch`/`Pointer`/`Mouse` (HIGH); no legacy `Input` |

### Feature Layer (logic in Domain; presenters in Game/UI)

| Module (asm) | Owns | Exposes | Consumes | Engine APIs |
|---|---|---|---|---|
| **SpecialResolver** (Domain) | Combo matrix, creation eligibility, cluster precedence, passive detonation (F8) | Implements the 4 Board Engine seams | Board query API (read-only) | none |
| **ScoreKeeper** (Domain) | `long FinalScore`, activation bonus, chain multiplier, star thresholds | `OnMatchCleared`, `GetCurrentScore()`, `GetScoreResults()` | `match_cleared` payloads | none |
| **ObjectiveEvaluator** (Domain) | Trackers, move counter, win/lose predicate, `ResultsData` assembly | Emits `ObjectiveProgressed`/`MovesRemainingChanged`/`LevelResolved`; handler registry | ScoreKeeper pull API; board events; Save `RecordLevelCompletion` | none |

### Presentation Layer

| Module (asm) | Owns | Exposes | Consumes | Engine APIs |
|---|---|---|---|---|
| **JuiceDirector** (Game) | Shadow Board Model, Reveal Queue, replay scheduler, `juice_input_lock_changed`, VFX/audio/haptics, LOD ladder | `juice_input_lock` (bool) | Domain event stream (via EventBridge); Save `settings` (read-only) | Particle System / VFX Graph (MEDIUM); URP Bloom (HIGH); `Handheld.Vibrate`/haptics; AudioSource pitch |
| **BoardPresenter** (Game) | Pooled `FruitPiece` GameObjects; mapping cells→transforms; renders one settled frame | — | JuiceDirector reveal steps; BoardModel query (settled only) | GPU Instancing, `MaterialPropertyBlock` (per-instance hue), URP materials (HIGH) |
| **ScreenFlowController** (Game/UI) | Base+overlay composite state; T1–T21; `overlay_is_active`; `attempt_number`; Formula 5 composition | `effective_board_input_enabled`; screen transitions | Save (`load/get/update/flush`), LevelData, `level_resolved`, `juice_input_lock` | UI Toolkit `UIDocument` (HIGH — USS strict); scene mgmt |
| **HUD / Screens** (UI) | UXML/USS layout of chips, buttons, overlays, Results star ceremony host | Button click callbacks | ObjectiveDisplayModel, ResultsData, profile | UI Toolkit `VisualElement`/`Button`/`Label` (HIGH); `AccessibilityRole` standard-enum (HIGH) |
| **WorldMapView** (UI) | MVP placeholder linear node list; node lock/star badges | node-tap → `PreLevelCard` | manifest, `get_profile`, unlock gate (Formula 4) | UI Toolkit |
| **AudioDirector** (Game) | Music/SFX bus, runtime pitch-shift of `audio_pop_base`, mute gating | `Play(audio_event)` | Save `music_enabled`/`sfx_enabled` | `AudioSource`, `AudioMixer` |

### ASCII module dependency diagram (data + control)

```
   [Input System]                                   [Addressables]      [persistentDataPath]
        │ pointer                                         │ LevelDataAsset       │ slot files
        ▼                                                 ▼                      ▼
   InputRouter ──SwapRequest──► BoardModel ◄──seams── SpecialResolver      SaveService
   (Game)          (intent)      (Domain)   1/2/3/4    (Domain)            (Game)
        ▲                          │ Events                 │                    ▲
   effective_board_input_enabled   │ (records)         ScoreKeeper           record_level_completion
        │  (Formula 5, 4 terms)    ▼                   (Domain)                   │ (WIN only)
   ScreenFlowController ◄── EventBridge ──► JuiceDirector    │              ObjectiveEvaluator
   (overlay_is_active)   (Game)             (Game)           │ pull API      (Domain)
        │                                    │ juice_input_lock ◄────────────────┘ level_resolved
        ▼                                    ▼                                     │ ResultsData
   HUD / Screens / WorldMap (UI) ◄── BoardPresenter (Game) renders settled frame ──┘
```

**Engine-API verification flags (post-cutoff, HIGH risk — confirm against reference):**

- ⚠️ `Addressables.LoadAssetAsync<LevelDataAsset>()` — Unity 6.2+ **throws** on failure
  (NEEDS wrap in try/catch). Verified: `plugins/addressables.md`.
- ⚠️ URP custom passes (if the emissive-only bloom needs one) use `RecordRenderGraph`,
  NOT `Execute(...ref RenderingData)`. Verified: `modules/rendering.md`. Prefer the
  built-in URP Bloom Volume override (no custom pass needed) — see §9.
- ⚠️ `UIDocument` + USS — 6.3 parser rejects previously-tolerated USS. Lint during UI
  work. Verified: `modules/ui.md`, VERSION.md.
- ⚠️ `AccessibilityRole` is a standard enum in 6.3 (no bitwise OR). Verified: VERSION.md.
- ⚠️ Input System `EnhancedTouchSupport.Enable()` required for `Touch.activeTouches`.
  Verified: `modules/input.md`.

---

## 7. Data Flow

### 7.1 Frame update path — a player swap (the core loop)

Logic resolves **synchronously in one call**; presentation paces the reveal across
many frames (board-engine §13; juice-layer §1). This split is the frame-budget
strategy: no per-frame work in the Domain, all pacing in the Game layer.

```
Frame N (input):
  Input System pointer ─► InputRouter.OnPointerUp
     │ reads effective_board_input_enabled (Formula 5: GAMEPLAY ∧ board_input_enabled
     │   ∧ ¬overlay_is_active ∧ ¬juice_input_lock)  — if false: drop gesture (MVP)
     ▼ emits SwapRequest(a,b)  [synchronous C# event]
  BoardPresenter/GameController ─► BoardModel.TrySwap(a,b, specials, score)
     │  BoardModel runs the ENTIRE resolution loop in this call:
     │    swap → match → seam3 spawns → seam4 chain fixpoint → clear → gravity → refill
     │    → (repeat cascade) → reshuffle-check → stabilize
     │  appends ordered BoardEvent records to board.Events (SwapAccepted, MatchCleared…,
     │    SpecialSpawned, PiecesSpawned, CascadeEnded, BoardStabilized)
     │  ScoreKeeper.OnMatchCleared accrues long score inline
     │  ObjectiveEvaluator handlers update trackers inline; at BoardStabilized it
     │    evaluates win/lose, (on WIN) calls Save.RecordLevelCompletion, fires LevelResolved
     ▼ returns true (board_input_enabled flips true again THIS FRAME — Domain is done)
  GameController ─► EventBridge.EnqueueMove(board.Events snapshot); board.Events.Clear()
  EventBridge ─► JuiceDirector.CaptureMove(events)
     │  JuiceDirector sets juice_input_lock = true (first Reveal Step dequeued)
     ▼
Frames N+1 … N+K (reveal replay, paced by Formula 1 deceleration curve):
  JuiceDirector drains the Reveal Queue one step per scheduled beat:
     SWAP_REVEAL → CLEAR_REVEAL(i) → FALL_REVEAL(i) [gravity re-derived from Shadow
     Board Model] → REFILL_REVEAL(i) → … → SETTLE_REVEAL
  BoardPresenter animates pooled FruitPiece transforms; AudioDirector plays hooks;
  HUD updates from ObjectiveProgressed/MovesRemainingChanged (also buffered per move)
  On SETTLE_REVEAL complete: juice_input_lock = false → input re-enabled (Formula 5)
```

Data transferred: `IReadOnlyList<BoardEvent>` (immutable records, full `PieceSnapshot`
identity) — **producer** Domain `BoardModel`, **consumer** Game `JuiceDirector`, via
**shared buffer** (`EventBridge`, not a live query). No thread boundary crossed
(single-threaded per technical-preferences.md and rng-service.md Edge Cases). The
Shadow Board Model exists precisely because Domain query state is *live* (already at
the settled state) while the reveal replays *historical* steps — so replay reads only
the captured events, never `BoardModel.At()` (the one exception: reshuffle, §7.4).

### 7.2 Event / signal path (decoupling)

- **Domain → presentation:** `BoardEvent` records collected in `BoardModel.Events`,
  handed over per move as an immutable snapshot list. This is the "signals → C# events"
  residue mapping. The event catalog must be completed to match board-engine.md §7 in
  full (the blueprint's `PieceTypes.cs` defines the core subset; the remaining catalog
  members — `SwapStarted`, `SwapRejected`, `SpecialActivated`, `CascadeStepAdvanced`,
  `BoardBootstrapped`, `BoardInputEnabledChanged`, `NoValidMovesDetected`,
  `MovesRemainingChanged`, `ObjectiveProgressed`, `LevelResolved` — are added as records
  in the same file; see QQ-01).
- **Game ↔ Game (composition):** `juice_input_lock`, `overlay_is_active`,
  `board_input_enabled`, `base_state` are four independently-owned booleans combined by
  a single `InputGate` in the Game assembly (Formula 5). No owner knows the others'
  internals; each contributes one veto term.
- **Never reverse:** presentation never calls back into Domain to *decide* anything
  gameplay-relevant (juice-layer §1). BoardPresenter may read `BoardModel` query API
  only to render the already-settled state, never to alter it.

### 7.3 Save / load path

```
Boot:   BootLoader ─► SaveService.LoadProfile()
          reads BOTH slot files (Application.persistentDataPath/save/profile_{a,b})
          SaveModel: run S1–S8 per slot → pick higher write_counter valid slot
          (tamper detect+accept; both-invalid → fresh + recovery_notice; absent → fresh)
        returns in-memory Profile (disk read exactly once per session)

Win:    ObjectiveEvaluator (at board_stabilized, outcome==WIN):
          composes ResultsData → Save.RecordLevelCompletion(level_id, stars, score)
          SaveModel.Merge (monotonic max) → total_stars recompute → dirty=true
          → SaveService atomic write to non-primary slot (A/B rotation), fsync, close
          [strictly BEFORE LevelResolved fires → Results screen can never celebrate an
           unpersisted win]

Settings: HUD ─► Save.UpdateSetting(k,v) → dirty → debounced write (750ms)
Background: OnApplicationPause(true) ─► Save.FlushIfDirty() (defensive, no-op if clean)
```

Serialization: compact UTF-8 JSON, fixed field order (canonical bytes for the FNV-1a
checksum). Unity's built-in `JsonUtility` cannot serialize `Dictionary<string,
LevelRecord>` — the serializer choice (System.Text.Json vs Newtonsoft vs a
hand-rolled canonical writer in Domain) is a Required ADR (§11, ADR-B). The
serialization *logic* lives in Domain (pure, testable); the file *IO* lives in Game.

### 7.4 Initialisation order (boot dependency graph)

```
1. BootLoader (Game)               — B1 BOOT_LOADING base state
2. Addressables init               — content catalog ready
3. SaveService.LoadProfile()       — profile in memory (settings drive audio/juice)
4. AudioDirector / JuiceDirector read settings
5. RngService constructed (idle until a level session starts)
6. LevelManifest asset loaded      — String→ordinal ready before any board bootstrap
7. ScreenFlowController → WORLD_MAP (T1); if recovery_notice → show once
--- per level entry (T4/T11/T17/T18) ---
8. Load LevelDataAsset via Addressables (try/catch — 6.2+ throws)
9. ScreenFlow supplies attempt_number → BoardModel bootstrap
10. BoardModel resolves manifest ordinal → RngService.StartLevelSession(ordinal, attempt)
11. BoardModel.Bootstrap() → emits pieces_spawned(BOOTSTRAP) → JuiceDirector opens board
12. ObjectiveEvaluator initializes trackers from LevelData.objectives; state = Tracking
```

**Cross-thread flags:** none. All gameplay logic is single-threaded. Addressables and
file IO use async handles but are awaited on the main thread at defined boot/entry
points — no gameplay state is touched from a worker thread.

---

## 8. API Boundaries

Contracts programmers implement against. C# signatures per technical-preferences.md
naming (PascalCase members, `_camelCase` private, past-tense events). Types shown are
Domain unless marked. These are the *seams*, not full class bodies.

### 8.1 Board Engine ↔ Special Candies (the four synchronous seams)

```csharp
// SweetCascade.Domain — the extension surface BoardModel calls into.
public interface ISpecialResolver
{
    // Seam 1 — called during swap validity determination, every swap.
    bool IsActivationSwap(Piece a, Piece b);                                   // MVP default: false

    // Seam 2 — only when Seam 1 returned true. Returns the seed clear set;
    // consumes the two swap pieces (bonuses are priced downstream by ScoreKeeper
    // from MatchCleared.ClearedPieces — no ScoreKeeper threading, per ADR-005 D2).
    HashSet<Cell> ActivationClears(BoardModel board, Cell a, Cell b);

    // Seam 3 — every cascade step, after Matching, before Clearing finalizes.
    // color:null => colorless special (Board assigns COLOR_NONE=-1).
    IReadOnlyDictionary<Cell, SpecialSpawn> ResolveSpecialSpawns(
        IReadOnlyList<Run> runs, IReadOnlyCollection<Cell> swapAnchorCells);   // MVP default: empty

    // Seam 4 — ONE single-pass expansion; BoardModel iterates to fixpoint or
    // MAX_CHAIN_EXPANSION_ITERATIONS. Never recurses internally.
    bool ExpandChain(BoardModel board, HashSet<Cell> clearedSet); // default: false (ADR-005 D2: no ScoreKeeper param)
}
public readonly record struct SpecialSpawn(SpecialType Special, int? Color);
```

**Invariants callers respect:** BoardModel owns ALL validity judgment — every cell a
seam returns is defensively re-checked against live OCCUPIED/in-bounds state and
silently dropped+logged if invalid; a malformed `SpecialSpawn.Color` falls back to the
run's own color. BoardModel guarantees the seams see a consistent grid snapshot per
step and that both iteration caps (`MAX_CASCADE_DEPTH`, `MAX_CHAIN_EXPANSION_ITERATIONS`)
bound termination regardless of resolver behavior.

### 8.2 Board Engine → the rest (events + query)

```csharp
public abstract record BoardEvent;                                  // full catalog in Domain/Events
// carries FULL PieceSnapshot identity so consumers replay from stored events alone:
public readonly record struct PieceSnapshot(Cell Cell, int Color, SpecialType Special);
public sealed record MatchCleared(int ChainIndex, PieceSnapshot[] ClearedPieces,
                                  Run[] RunData, TriggerSource Trigger) : BoardEvent;
// … SwapStarted, SwapAccepted, SwapRejected, SpecialActivated, SpecialSpawned,
//    PiecesSpawned, CascadeStepAdvanced, CascadeEnded, NoValidMovesDetected,
//    BoardReshuffled, BoardStabilized, BoardBootstrapped, BoardInputEnabledChanged.

public sealed class BoardModel   // synchronous, headless, deterministic
{
    public IReadOnlyList<BoardEvent> Events { get; }        // drained per move by EventBridge
    public bool TrySwap(Cell a, Cell b, ISpecialResolver s);  // false => rejected (scoring via IBoardEventSink, ADR-005)
    public Piece? At(Cell c);
    public CellState GetCellState(int r, int c);            // Void|Empty|Occupied
    public bool HasAvailableMove(ISpecialResolver s);
    public bool BoardInputEnabled { get; }                  // Domain busy signal (raw term)
    public int CurrentChainIndex { get; }
}
```

### 8.3 Scoring pull API (consumed by Level Objective)

```csharp
public interface IScoreProvider
{
    long GetCurrentScore();                                 // live authoritative running total
    ScoreResults GetScoreResults();                         // called once at resolving stabilize
}
public readonly record struct ScoreResults(
    long FinalScore, int StarsEarned, float ScoreProgressRatio, int ScoreProgressPercent);
```

### 8.4 Level resolution & persistence handshake

```csharp
// ObjectiveEvaluator assembles this and fires it — after RecordLevelCompletion on WIN.
public sealed record LevelResolved(Outcome Outcome, ResultsData Results) : BoardEvent;
public readonly record struct ResultsData(
    string LevelId, Outcome Outcome, long ScoreEarned, int StarsEarned,
    ClosestMissSummary ClosestMiss);

public interface ISaveService                               // Game assembly, wraps Domain SaveModel
{
    Profile LoadProfile();                                  // boot only; disk read once
    Profile GetProfile();                                   // in-memory, never disk
    void RecordLevelCompletion(string levelId, int starsEarned, long scoreEarned); // WIN only
    void UpdateSetting(string key, object value);           // debounced
    bool FlushIfDirty();                                    // background/pause/quit
    int GetTotalStars();
}
```

### 8.5 Input & input-lock composition

```csharp
// InputRouter emits (Game). Coords are (row,col) only — never color/piece identity.
public event Action<Cell> SelectCell;
public event Action<Cell, Cell> SwapRequest;               // Manhattan distance == 1 guaranteed
public event Action Cancel;

// InputGate (Game) — Screen Flow Formula 5, four independently-owned terms.
bool EffectiveBoardInputEnabled =>
    _baseState == BaseState.Gameplay
    && _board.BoardInputEnabled            // Domain (raw busy)
    && !_overlayIsActive                   // Screen Flow-owned
    && !_juiceInputLock;                   // Juice Layer-owned (JuiceDirector)
```

**Engine-type flag:** the only engine types crossing these boundaries live in Game/UI
(`UIDocument`, `AsyncOperationHandle`, `ParticleSystem`, `AudioSource`). No Domain
interface exposes a `UnityEngine` type — verified by the asmdef `noEngineReferences`
constraint. All Domain DTOs are `record`/`readonly struct` (C# 9, supported in Unity 6
per `current-best-practices.md`).

---

## 9. Rendering Architecture & Draw-Call Budget

**Target:** ≤100 draw calls during the heaviest cascade, 60fps / 16.6ms on a 2022-era
mid-range Android, ≤400MB (technical-preferences.md). **No realtime shadows** (blob
decals), **one** directional key light, **bloom on emissives only**.

### 9.1 Pipeline

- **URP, Render Graph path only.** Compatibility Mode is read-only `false` in 6.3
  (VERSION.md) — the project authors against Render Graph from day one. No custom
  `ScriptableRenderPass` is required at MVP: the emissive-only bloom is achieved with a
  **URP Bloom Volume override** (threshold tuned above the LDR range so only emissive
  materials — BombOrb dots/ring, ClearBurst, CreamStripe shimmer — bloom). If a custom
  pass is ever added, it uses `RecordRenderGraph(RenderGraph, ContextContainer)`
  (`modules/rendering.md`), never the deprecated `Execute(...ref RenderingData)`.
- **Camera:** orthographic (or ~10° FOV), straight-on. Grid readability is the
  accessibility contract and is never sacrificed to camera angle (blueprint §1).
- **SRP Batcher enabled; GPU Instancing enabled** on the fruit and well materials.
- **Post stack:** Bloom + Vignette only. No chromatic aberration / film grain.

### 9.2 The 13-material set (from the visual blueprint)

CandyGlass (instanced per-hue via `MaterialPropertyBlock`), GildedCream, GlassPanel,
WellA/WellB, CreamStripe, BombOrb (emissive-only bloom source), BlobShadow, Gradient,
HillSoft, Bokeh, Vignette, BurstAdditive, CreamChipUI. Shared shader families → SRP
Batcher compatible. Authored via `unity-shader-specialist` (Shader Graph URP / HLSL).

### 9.3 Worst-case draw-call accounting (heaviest single-step full-board cascade)

Board rendering uses **5 shared fruit meshes** with per-instance hue, and **one
instanced draw for the 64 wells**. Fruits of the same mesh+material batch via GPU
Instancing / SRP Batcher.

| Group | Contents | Est. draw calls |
|---|---|---|
| Environment | Gradient backdrop, 2 hill cards, 1 bokeh particle system, vignette | ~5 |
| Board frame | BoardFrame (GildedCream), BoardPanel (GlassPanel, translucent) | 2 |
| Cell wells | 64 wells, 2 tints, GPU-instanced | 1–2 |
| Fruit pieces | ≤64 pieces across 5 shared meshes, per-instance hue (instanced by mesh) | 5–8 |
| Piece overlays | CreamStripe (only on stripes, few), BombDress/GlowRing (rare) | 2–4 |
| Blob shadows | 64 decals, instanced single batch | 1 |
| FX (Juice) | ClearBurst (≤1/step), PopParticles (≤24, atlas single material), ScorePopups (TMP) | 3–6 (≤ `JUICE_VFX_DRAW_CALL_BUDGET`=40 sub-budget) |
| UI (UI Toolkit) | HUD chips + footer, retained-mode batched (overlays only when active) | 4–8 |
| Post | Bloom + Vignette full-screen passes (Render Graph) | ~3 |
| **Total (typical worst case)** | | **~26–39** |

**Headroom:** the typical heaviest-cascade frame lands ~26–39 draw calls, well under
the 100 ceiling. The Juice Layer's own 40-call VFX sub-budget (juice-layer §9) plus a
4-tier LOD degradation ladder (Formula 4) guarantees VFX alone can never breach the
global ceiling even in Board Engine's degenerate `max_cells_per_step` case (up to 81 on
a 9×9). Board + environment + UI consume the remainder with large margin. **Validation
gate:** this accounting is a design-time estimate; it is re-profiled on target hardware
at the Vertical Slice feel checkpoint (systems-index High-Risk table; QQ-05).

### 9.4 Memory

5 shared meshes + 13 materials + one candy/particle atlas + UI Toolkit assets +
pooled prefabs (64 FruitPiece, never destroyed) are trivially inside 400MB. LevelData
and region assets load on demand via Addressables and release by refcount, so only the
active region is resident. Save footprint is <40KB (save-persistence Formula 5).

---

## 10. ADR Audit + TD Self-Review

### 10.1 ADR quality check

Only one ADR exists (`docs/architecture/adr-001-engine-selection-unity.md`).

| ADR | Engine Compat section | Version recorded | Post-cutoff flagged | GDD linkage | Conflicts w/ this architecture | Valid for 6.3 |
|---|---|---|---|---|---|---|
| ADR-001: Engine Selection — Unity 6.3 LTS | ⚠️ partial (in Consequences, not a titled section) | ✅ 6.3 LTS (6000.3.x) | ✅ RenderGraph, AccessibilityRole, USS, unified compiler | ⚠️ implicit ("all 11 GDDs engine-neutral"), no titled "GDD Requirements Addressed" | **None** | ✅ Accepted, valid |

**Finding:** ADR-001 is valid, correctly version-pinned, flags the right post-cutoff
risks, and does **not conflict** with any layer/ownership/data-flow decision here. It is
missing three sections the `docs/CLAUDE.md` ADR template now requires (**ADR
Dependencies**, a titled **Engine Compatibility** section, **GDD Requirements
Addressed**). ADR-001 predates that template as an engine-selection decision.
**Recommendation (advisory, non-blocking):** retrofit those three sections when the
next ADR is written, cross-linking the GDD-neutrality argument already present in its
Consequences. This does not block coding.

### 10.2 Traceability coverage

Every TR in §3 is covered either by an architectural decision in §§4–9 or by a Required
ADR in §11. No TR maps to an *existing* ADR other than ADR-001 (engine selection),
because no system-level ADRs exist yet — hence the Required ADR list below is the
principal traceability output of this pass.

### 10.3 Technical Director self-review (gate TD-ARCHITECTURE)

Applied as self-review per the skill's Phase 7b. Verdict: **APPROVE WITH CONDITIONS.**

| TD-ARCHITECTURE criterion | Assessment |
|---|---|
| Every GDD system placed in a layer with clear ownership | ✅ All 11 mapped (§4, §6); no orphaned system. |
| Dependency direction is acyclic and enforced | ✅ Domain→nothing, Game→Domain, UI→Game; asmdef + CI guard (§5). |
| Cross-system contracts are explicit and testable | ✅ Seams, events, pull API, save handshake, input-lock composition all specified (§8). |
| Performance budgets have an accounting, not a hope | ✅ Draw-call table (§9); revalidation gate flagged for Vertical Slice. |
| Post-cutoff engine risk is identified and mitigated | ✅ HIGH/MEDIUM domains flagged inline with reference citations (§1, §6). |
| Determinism / Pillar 2 is architecturally protected | ✅ Pure-Domain assembly; **condition:** RNG must not use `System.Random` (ADR-C). |

**Conditions attached to approval (must be resolved before the relevant code lands, not
before this document is accepted):**
1. **ADR-C (RNG determinism)** — replace `System.Random` with a platform-stable
   SplitMix32-style generator implementing rng-service.md F1–F6. (QQ-04)
2. **ADR-A/B (save serialization + Addressables grouping)** — Foundation ADRs are a
   prerequisite for the save and level-load slices.
3. **Event catalog completion** — the `BoardEvent` records must cover board-engine.md
   §7 in full before the Board Engine slice is testable end-to-end. (QQ-01)

---

## 11. Required ADRs

Decisions surfaced by this architecture that still need an ADR (one-line scope each).
**Do not treat these as written** — run `/architecture-decision "<title>"` for each.
Grouped by priority. ADR-001 is the only ADR that exists today.

### Must have before coding starts (Foundation & Core)

- **ADR-A — Addressables grouping & content-load strategy:** LevelData/region group
  layout, local-vs-remote split for MVP (all local), catalog, and the throw-on-failure
  wrapping policy (6.2+). Unblocks: level loading, boot sequence.
- **ADR-B — Save serialization format details:** JSON writer choice (System.Text.Json vs
  Newtonsoft vs hand-rolled canonical writer) given `Dictionary` + canonical-byte +
  FNV-1a needs; `persistentDataPath` atomic A/B write ladder and fsync/durability on
  iOS/Android/WebGL. Unblocks: Save & Persistence slice.
- **ADR-C — Deterministic RNG generator & domain-assembly determinism guard:** the
  platform-stable `mix32`/SplitMix32 choice (rng-service.md F1), the ban on
  `System.Random` in Domain, and the CI check that Domain contains zero `UnityEngine`
  symbols. Unblocks: every deterministic Edit-Mode test (Pillar 2).
- **ADR-D — Event bridge & deferred-replay contract:** where the per-move event buffer
  and Reveal Queue live (Game `EventBridge`/`JuiceDirector`), the four-term input-lock
  composition ownership (Formula 5), and the full `BoardEvent` catalog surface. Unblocks:
  Board Engine ↔ Juice Layer integration.
- **ADR-E — Level manifest generation & W6–W7 cross-validation:** the generated
  `LevelManifest` ScriptableObject, its append-only ordinal discipline, and the
  editor-time check that every authored `level_id` ↔ manifest entry (world-map W6–W7).
  Unblocks: RNG session seeding, world-map wiring.

### Should have before the relevant system is built (Feature & Presentation)

- **ADR-F — UI Toolkit vs UGUI for board-adjacent FX:** whether score popups / cascade
  callouts render as world-space TextMeshPro or UI Toolkit elements, and the HUD's
  USS-strict-parser + `AccessibilityRole` standard-enum compliance approach. Unblocks:
  Juice Layer popups, HUD.
- **ADR-G — Audio middleware vs Unity native:** Unity `AudioSource`/`AudioMixer` with
  runtime pitch-shift of `audio_pop_base` vs FMOD/Wwise; mute-bus routing. Unblocks:
  AudioDirector.
- **ADR-H — Particle strategy (Shuriken vs VFX Graph):** which system backs the pooled,
  atlas-single-material cascade bursts against the 40-call VFX sub-budget (6.3 VFX Graph
  GPU-event instancing is available). Unblocks: Juice VFX.

### Can defer to implementation / later phase

- **ADR-I — Analytics & backend abstraction seam (Phase 3):** a thin, no-op-at-MVP
  interface seam for the inferred Backend & Accounts Service (systems-index #15),
  leaderboards/sync, so no MVP code hard-codes an offline assumption that later blocks
  Social Layer. Scope only; not implemented at MVP.
- **ADR-J — CI build pipeline:** `game-ci/unity-test-runner@v4` Edit+Play gating,
  export templates, the iOS Mac build step, and the `UNITY_LICENSE` secret (tests/README).
  Unblocks: CI green gate.
- **ADR-001 retrofit (advisory):** add the three template sections (ADR Dependencies,
  Engine Compatibility, GDD Requirements Addressed) noted in §10.1.

---

## 12. Architecture Principles

Five principles govern every technical decision on Sweet Cascade. Derived from the game
pillars (game-concept.md), the GDDs, and technical-preferences.md.

1. **The logic core is pure and headless.** All board, specials, scoring, objective,
   RNG, and save-serialization logic lives in `SweetCascade.Domain` with zero
   `UnityEngine` references — compiler-enforced, CI-guarded. If a decision would put
   gameplay truth behind an engine type, it is wrong. (Serves Pillar 2, testability,
   ADR-001 reversibility.)

2. **Logic resolves instantly; presentation paces reveals.** The Domain resolves an
   entire move synchronously in one call; the Game layer replays it across frames via a
   captured event stream, a Shadow Board Model, and a Reveal Queue. No `await`/timer in
   Domain; no gameplay decision in presentation. (Serves Pillar 1 juice + the 16.6ms
   frame budget.)

3. **The board can never be rigged, and can always be replayed.** All randomness flows
   through seedable, stream-isolated, platform-stable RNG with no player-performance
   input. Every board is reproducible from a logged seed. (Serves Pillar 2, "Clever,
   Never Cheated.")

4. **Data is authored, versioned, and inspectable — never hardcoded.** Levels are
   ScriptableObjects under a versioned schema with a validation suite; every difficulty
   lever is visible authored data. Save schema is additively versioned. No gameplay
   constant is a literal in code. (Serves Pillar 2 + coding-standards data-driven rule.)

5. **Respect the pinned engine's post-cutoff reality.** Render Graph path only, Input
   System only, UI Toolkit with strict USS, Addressables with throw-on-failure handling
   — always cross-referenced against `docs/engine-reference/unity/` before an API is
   cited. The LLM's Unity knowledge predates 6.3; the reference library is authoritative.

---

## 13. Open Questions

Decisions deferred; each must be resolved before the relevant layer is built. These
seed the handoff's watch-list.

| ID | Question | Priority | Resolution path |
|---|---|---|---|
| QQ-01 | The `BoardEvent` catalog in the blueprint covers only a subset of board-engine.md §7 — the remaining records must be added before the Board Engine slice is end-to-end testable. | High | ADR-D + Board Engine implementation |
| QQ-02 | Save JSON writer: `System.Text.Json` vs Newtonsoft vs hand-rolled canonical writer (Unity `JsonUtility` cannot serialize the `Dictionary` profile). | High | ADR-B |
| QQ-03 | Addressables grouping + the iOS/Android/WebGL `persistentDataPath` durability (fsync) guarantee the save GDD's atomic A/B ladder assumes. | High | ADR-A + ADR-B |
| QQ-04 | Replace `System.Random` (blueprint placeholder) with a platform-stable SplitMix32-style generator — required for cross-platform determinism (Pillar 2). | High | ADR-C |
| QQ-05 | The ≤100 draw-call accounting (§9) is a design-time estimate; it must be re-profiled on 2022-era mid-range Android at the Vertical Slice feel checkpoint. | Medium | Vertical Slice perf pass (performance-analyst) |
| QQ-06 | Score popups / cascade callouts: world-space TextMeshPro vs UI Toolkit, plus USS-strict + `AccessibilityRole` standard-enum compliance. | Medium | ADR-F + `/ux-design` |
| QQ-07 | Audio: Unity native `AudioSource`/`AudioMixer` (runtime pitch-shift) vs FMOD/Wwise. | Medium | ADR-G |
| QQ-08 | Particle backing: Shuriken vs VFX Graph (6.3 GPU-event instancing) under the 40-call VFX sub-budget. | Medium | ADR-H |
| QQ-09 | Phase-3 backend/analytics abstraction seam — define the no-op MVP interface so no MVP code blocks Social Layer later. | Low | ADR-I (scope only) |
| QQ-10 | ADR-001 template retrofit (three missing sections). | Low | ADR-001 revision (advisory) |

---

*End of master architecture v1.0 (Draft). Next: run the Required ADRs (§11) — Foundation
first (ADR-A/B/C/D/E) — then `/architecture-review`, `/test-setup`, `/ux-design`, and
`/gate-check pre-production`.*
