# Control Manifest — Sweet Cascade

> **Engine**: Unity 6.3 LTS (6000.3.x) · C# · URP (Render Graph path only)
> **Last Updated**: 2026-07-18
> **Manifest Version**: 2026-07-18
> **ADRs Covered**: ADR-001 (Engine Selection), ADR-002 (Addressables Strategy, as amended 2026-07-18), ADR-003 (Save Serialization), ADR-004 (Deterministic RNG & Purity Guard), ADR-005 (Event Bridge / BoardEvent Catalog), ADR-006 (Level Manifest Generation)
> **Status**: Active — regenerate with `/create-control-manifest update` when ADRs change

`Manifest Version` is the date this manifest was generated. Story files embed this
date when created. `/story-readiness` compares a story's embedded version to this
field to detect stories written against stale rules. Always matches `Last Updated`.

This manifest is a programmer's quick-reference extracted from all six Accepted ADRs,
`.claude/docs/technical-preferences.md`, `.claude/docs/coding-standards.md`,
`docs/engine-reference/unity/` (VERSION.md, deprecated-apis.md, current-best-practices.md),
`docs/architecture/architecture.md` (v1.0 + review resolutions), and
`docs/architecture/architecture-review-2026-07-18.md`. For the reasoning behind each
rule, see the referenced source.

**Layering note:** Sweet Cascade's architecture is organized by compiled assembly, not
by generic feature tier — so this manifest uses the project's actual assembly
boundaries (**Domain / Game / UI / Editor & CI**, `architecture.md` §5) in place of the
skill template's generic Foundation/Core/Feature/Presentation split. Every ADR rule is
filed under the assembly it governs; where a rule spans two assemblies (e.g. an
Addressables load contract that Domain must never touch but Game must enforce), it is
duplicated into both sections.

**Engine pin (read first):**
- Unity 6.3 LTS (6000.3.x), C# only. Do not introduce Godot/GDScript patterns; the
  Godot reference library remains in-tree for history only, not as guidance. [ADR-001]
- The LLM's Unity training reliably covers only ~6.0/6.1. Cross-reference
  `docs/engine-reference/unity/` before citing any Unity 6.2+ API, especially in the
  HIGH-risk domains: URP Rendering, UI Toolkit, Input System, Addressables. [VERSION.md; architecture.md §1]

---

## Domain Layer Rules

*Applies to: `SweetCascade.Domain` (`Assets/Domain/`) — `BoardModel`, `SpecialResolver`,
`ScoreKeeper`, `ObjectiveEvaluator`, `RngService`, `SaveModel`/serializer, `BoardEvent`
catalog, `LevelManifest`/`ManifestCrossValidator`. Pure C#, zero engine surface.*

> **The event catalog is copied, never invented.** Every `BoardEvent` record type,
> field, and payload shape must be copied verbatim from ADR-005's Key Interfaces
> section — no implementer re-derives or "improves" the catalog. [ADR-005]

### Required Patterns

- **Domain assembly (`SweetCascade.Domain.asmdef`) sets `noEngineReferences: true`;
  zero `UnityEngine`/`UnityEditor` symbols may appear in compiled Domain, CI-guarded.** [architecture.md §5.1; ADR-004 L1]
- **All Domain logic resolves synchronously in one call — no `await`, no timers, no
  per-frame state.** A move's entire resolution (swap → match → seams → clear →
  gravity → refill → cascade → reshuffle-check → stabilize) happens inside one
  function call. [architecture.md Principle 2; ADR-005]
- **RNG generator is SplitMix32 (stream) + MurmurHash3 `fmix32` (`Mix32`, finalizer)**
  with the exact normative constants `K1=0x9E3779B1`, `K2=0x9E37`, `GAMMA=0x9E3779B9`,
  `M1=0x85EBCA6B`, `M2=0xC2B2AE35`, `FNV_OFFSET=0x811C9DC5`, `FNV_PRIME=0x01000193`. [ADR-004]
- **`IRngService` implements `rng-service.md` §6 1:1**: `StartLevelSession`,
  `StartDailySession`, `StartTestSession`, `NextFloat`, `NextInt`, `NextColor`,
  `Shuffle`, `ForkStream`, `GetSessionLog`. [ADR-004]
- **`NextInt`/`NextColor` use the exact integer multiply-shift `(raw × range) >> 32`**,
  never `floor(next_float × range)` — this is the normative core path, not an
  approximation. [ADR-004]
- **`level_id` reaching the RNG must be a resolved `int` ordinal from
  `LevelManifest.ResolveOrdinal`**, never a runtime string hash. [ADR-004; ADR-006]
- **All RNG mixing/combining arithmetic is `uint`/`ulong` and `unchecked`; RNG is
  single-threaded, no `[ThreadStatic]`/locks.** [ADR-004]
- **Wall-clock time enters Domain only via injected `IClock`** (timestamp only, never
  a draw input) — the concrete `SystemClock` implementation lives in Game. [ADR-004]
- **Save serializer is the hand-rolled `CanonicalJsonWriter` + `SaveJsonReader`
  (`SaveSerializer`)**, pure C# (`System.Text`/`Globalization`/`Collections` only),
  zero third-party dependency. [ADR-003]
- **FNV-1a-32 checksum computed exactly per Formula 1** (`OFFSET_BASIS=0x811C9DC5`,
  `PRIME=0x01000193`, unchecked `uint`); canonical byte contract is frozen: UTF-8 no
  BOM, compact (no whitespace), fixed positional field order (never alphabetical),
  `level_records` sorted ascending by `level_id` via `StringComparer.Ordinal`,
  numbers via `ToString(InvariantCulture)`, booleans lowercase, absent optional
  timestamps = literal `null`. Any change to this contract is a
  `CHECKSUM_ALGORITHM_VERSION` bump event, not a silent edit. [ADR-003]
- **`best_score` / final score is `long` (Int64), never `int`**, on the wire and in
  `ScoreResults`/`ResultsData`. [ADR-003; ADR-005; architecture.md §2]
- **`SaveModel`/`RngService`/`ManifestCrossValidator` are constructor-injected into
  their Game-layer consumers** — dependency injection, not singletons. [ADR-003; coding-standards.md]
- **Schema migration is an ordered pipeline of pure `MigrateV{n}ToV{n+1}` functions.** [ADR-003]
- **Every GDD/ADR constant lives in one centralized Domain config location** — e.g.
  `SETTINGS_SAVE_DEBOUNCE_MS`, `MAX_CASCADE_DEPTH`, `MAX_CHAIN_EXPANSION_ITERATIONS`,
  stream IDs, `CURRENT_SCHEMA_VERSION` — never a scattered literal. [ADR-003; ADR-004; coding-standards.md]
- **`BoardModel` emits every event only through an injected `IBoardEventSink`** — it
  must never reference `ScoreKeeper`, `ObjectiveEvaluator`, or any Feature-layer type
  by name. [ADR-005]
- **`MoveResolver` dispatches Domain logic subscribers in the fixed, constant order
  `[ScoreKeeper, ObjectiveEvaluator]`** — `ScoreKeeper` must process a `MatchCleared`
  before `ObjectiveEvaluator` reads `get_current_score()` on that same event. Encode
  as a constant list, and assert the order in a test. [ADR-005]
- **All `BoardEvent` payload collections are `IReadOnlyList<T>`, immutable by
  construction** — build the array/list fully, then construct the record. [ADR-005]
- **Win-persistence ordering is fixed (D4):** on `BoardStabilized` with `outcome ==
  WIN`: evaluate outcome → pull `ScoreResults` → compose `ResultsData` → call
  `ISaveWriter.RecordLevelCompletion(levelId, stars, score)` → **only then** emit
  `LevelResolved`. `RecordLevelCompletion` is never called on LOSE. Step 4 strictly
  precedes step 5. [ADR-005 D4]
- **`LevelManifest`, `WorldMapManifest`, `ManifestCrossValidator`, `LevelIdentity`
  live in Domain (`Levels/`)**, `noEngineReferences`. [ADR-006]
- **`ResolveOrdinal(levelId) = entries.FindIndex(id) + 1`; `0` is the "unresolved"
  sentinel.** O(n≤120), called once per level entry, never in a frame loop. [ADR-006]
- **`ManifestCrossValidator.Validate` is a single pure function called identically by
  the Editor tool and the Edit Mode/CI test** — never duplicated logic between them. [ADR-006]

### Forbidden Approaches

- **Never `System.Random` / `new Random(...)` anywhere in Domain** — its algorithm is
  not byte-stable across .NET runtimes (Mono/IL2CPP/WebGL disagree), which breaks
  daily-challenge fairness and seed-replay bug-repro. [ADR-004]
- **Never any `UnityEngine.*`/`UnityEditor.*` type in Domain**, including
  `UnityEngine.Random`, `Mathf`, `Time`, `Debug.Log`. [ADR-004; architecture.md §5.1]
- **Never 32-bit `float`, `Mathf`/`MathF`, or `Math.Floor` on the RNG core/draw path**
  (scoped to `Assets/Domain/Rng/**`) — float ops are the least portable primitive
  across Mono/IL2CPP/WebGL; the integer multiply-shift removes them entirely. [ADR-004]
- **Never `string.GetHashCode()` / `object.GetHashCode()` as a draw or seed input** —
  randomized per-process since .NET Core. [ADR-004]
- **Never `DateTime.Now`/`UtcNow`/`Today`, `Environment.TickCount`, `Stopwatch`,
  `Guid.NewGuid()` anywhere in Domain** — wall-clock/entropy sources are
  non-deterministic. [ADR-004]
- **Never `UnityEngine.JsonUtility` for save serialization** — engine-bound, cannot
  link from Domain, cannot serialize `Dictionary<string, LevelRecord>`. [ADR-003]
- **Never `System.Text.Json` or Newtonsoft.Json as the write/checksum serializer** —
  neither guarantees byte-stable canonical output without hand-driving the writer
  anyway, which defeats the point of the dependency. (Newtonsoft is the *only*
  sanctioned fallback, and only for the read path, only if the bespoke reader proves
  too costly — see Ambiguous/Deferred Items below.) [ADR-003]
- **Never depend on `rename()`/atomic-rename semantics for save durability** — WebGL's
  IndexedDB-backed FS has no such guarantee; the A/B scheme is designed around this. [ADR-003]
- **Never any file IO / `Application.persistentDataPath` access directly in Domain** —
  IO lives behind `ISaveStore` in Game. [ADR-003]
- **Never a live C# `event`/delegate subscription across the Domain→Game boundary for
  board events** — this fires presentation callbacks while the board is already
  settled, breaking the capture-then-replay model. [ADR-005]
- **Never thread `ScoreKeeper` through the four Special Candies seams**
  (`ActivationClears`, `ExpandChain`) — this is the blueprint's superseded pattern;
  scoring reads exclusively from `MatchCleared.ClearedPieces`. [ADR-005]
- **Never register a logic subscriber for a derived feature event**
  (`ObjectiveProgressed`, `MovesRemainingChanged`, `LevelResolved`) — they are
  append-only, to prevent a re-entrant `Emit` loop. [ADR-005]
- **Never merge the ordinal `LevelManifest` and the hand-authored `WorldMapManifest`
  into one ScriptableObject/file** — a generated artifact and a hand-authored one must
  not share a file. [ADR-006]
- **Never delete or renumber an existing `entries` slot on level removal** — tombstone
  via the additive `retired_ordinals` field instead; a slot's ordinal is retained
  forever once assigned. [ADR-006]
- **Never hash the string `level_id` at runtime to derive an RNG seed input.** [ADR-006]
- **Never hardcode a `level_id → int` mapping anywhere outside `LevelManifest.ResolveOrdinal`.** [ADR-006]

### Performance Guardrails

- **RNG**: bootstrap fill (64 draws) + worst-case cascade refill (≤64 draws) < 1ms;
  per-stream state ≈ a few hundred bytes total. [ADR-004]
- **Save**: serialize + FNV-1a checksum over a ~18KB profile is sub-ms, off the
  16.6ms hot path — runs only on win/settings/pause. On-disk footprint < 40KB at full
  120-level scope. [ADR-003]
- **Move resolve**: a typical move resolves well under 1ms; worst case a single
  `MatchCleared` carries up to ~81 `PieceSnapshot`s (9×9 full board) — transient,
  human-paced allocation, watched at the profiling pass. [ADR-005]
- **Manifest resolve**: 0ms per-frame cost (no per-frame Domain calls); both manifests
  combined < 0.05MB. [ADR-006]
- **Domain-purity guard is three-layer and mandatory**: L1 asmdef compile failure, L2
  CI `ripgrep` denylist scan of `Assets/Domain/` (blocking, pre-Unity-build), L3
  golden-vector suite run byte-for-byte under Mono + IL2CPP + WebGL. [ADR-004]

---

## Game Layer Rules

*Applies to: `SweetCascade.Game` (`Assets/Game/`) — `ContentLoader`/`BootLoader`,
`SaveService`/`ISaveStore`, `EventBridge`, `JuiceDirector`, `InputRouter`/`InputGate`,
`ScreenFlowController`, `AudioDirector`, `BoardPresenter`. MonoBehaviour/engine
integration.*

### Required Patterns

- **`IContentLoader` is the only code that may call `Addressables.*`.** Every load is
  wrapped so a Unity 6.2+ throw-on-failure becomes a `LoadResult<T>` (`Ok`|`Fail`) —
  no exception ever escapes to a caller. [ADR-002]
- **Seven-group Addressables layout** authored under `Assets/AddressableGroups/`:
  `Core_Bootstrap`, `Shared_BoardRig`, `Region_Theme_<code>` (per region),
  `Levels_<code>` (per region), `Audio_Region_<code>` (per region), `Remote_Events`
  (reserved, empty/unbuilt at MVP), `Editor_Fixtures` (test-only). [ADR-002]
- **Resident-set invariant enforced at runtime**: `Core ∪ Shared_BoardRig ∪ (exactly
  one region's {theme, levels, music})` at any instant. Release the old region's
  three groups **before** loading the new region's. [ADR-002]
- **Per-content load-failure policy is fixed**: `Core_Bootstrap`/`Shared_BoardRig`
  failure = **FATAL** (boot-error screen with Retry, never enter GAMEPLAY);
  `LevelData` failure = abort the level, return to `WORLD_MAP` with a notice, **never
  a partial board**; `Region_Theme` failure = fall back to the Core neutral theme;
  `Audio_Region` failure = silent-degrade (no music). [ADR-002]
- **`IContentHandle` stays opaque** — no consumer outside `ContentLoader` may reach
  the underlying `AsyncOperationHandle`. [ADR-002]
- **The two manifests (`level_manifest.asset`, `world_map_manifest.asset`) load via a
  direct serialized reference on the `BootLoader`/config object — NOT via
  Addressables.** This is the amended, current state of ADR-002 (its `Core_Bootstrap`
  group explicitly excludes both manifests) reconciled with ADR-006's engine-guidance
  rationale ("don't use Addressables for startup-immediate assets"; also sidesteps
  the 6.2+ throw). [ADR-006; ADR-002 as amended 2026-07-18; architecture-review-2026-07-18.md CONFLICT-1]
- **Save write sequence is fixed**: `SaveService` computes bytes in Domain, then
  `ISaveStore.WriteSlotDurable` writes + flushes (native: `FileStream.Flush(true)`;
  WebGL: IDBFS sync request), then `SaveService` immediately **read-back-verifies**
  before trusting the write. On read-back failure, `dirty` stays `true` and the
  untouched slot remains authoritative — no swap step exists; primary is derived from
  `write_counter` at next load. [ADR-003]
- **`SaveService.FlushIfDirty()` fires on `OnApplicationPause(true)`/focus-lost/quit**;
  settings writes are debounced 750ms (`SETTINGS_SAVE_DEBOUNCE_MS`). [ADR-003; architecture.md §7.3]
- **`SystemClock : IClock` lives in Game** (reads `DateTime.UtcNow`) and is injected
  into Domain — Domain never reads the clock itself. [ADR-004]
- **`EventBridge.EnqueueMove` asserts no un-drained prior move exists** before
  accepting a new one (single-active-queue invariant). [ADR-005]
- **`GameController` hands the Domain's immutable per-move snapshot to
  `EventBridge.EnqueueMove` only after `ResolveSwap`/`ResolveBootstrap` fully
  returns**; the Domain log is cleared immediately after. [ADR-005]
- **`InputGate` is the single composition point for the four Formula-5 input-lock
  terms**: `_baseStateIsGameplay && _boardInputEnabled && !_overlayIsActive &&
  !_juiceInputLock`. No other code recombines these terms; each owner
  (`ScreenFlowController`, `BoardModel`/`GameController`, `ScreenFlowController`,
  `JuiceDirector`) writes only its own term. [ADR-005 D5]
- **`ISaveWriter.RecordLevelCompletion` is implemented by `SaveService`**;
  `ObjectiveEvaluator` (Domain) calls it once, fire-and-forget — Game never retries
  or inspects the result from the Domain side. [ADR-005]
- **Input System package only** (`EnhancedTouch`/`Pointer`/`Mouse`) for all gesture
  and pointer handling — never the legacy `Input` class. [architecture.md §4/§6; technical-preferences.md; deprecated-apis.md]
- **URP, Render Graph path only.** Any custom pass uses
  `RecordRenderGraph(RenderGraph, ContextContainer)`, never
  `Execute(ScriptableRenderContext, ref RenderingData)`. [architecture.md §9.1; VERSION.md; current-best-practices.md]
- **Emissive-only bloom via a URP Bloom Volume override** (threshold tuned above LDR
  so only emissive materials bloom) — no custom render pass needed at MVP. [architecture.md §9.1]
- **One directional key light only; no realtime shadows** — blob-shadow decals. [architecture.md §9; technical-preferences.md]
- **SRP Batcher + GPU Instancing enabled on fruit and well materials**; 5 shared fruit
  meshes with per-instance hue via `MaterialPropertyBlock`; the 64 cell wells render
  as one instanced draw. [architecture.md §9.1–9.3; technical-preferences.md]
- **All gameplay/event/RNG/file-IO code runs on the Unity main thread only.**
  Addressables and file IO use async handles but are awaited only at defined
  boot/level-entry points — no gameplay or event state is ever touched from a worker
  thread. [ADR-005 D6; architecture.md §7.4]

### Forbidden Approaches

- **Never call `Addressables.*` from anywhere outside `IContentLoader`.** [ADR-002]
- **Never `Resources.Load()` / synchronous asset loading**, and never
  direct-serialized-reference everything as the general content strategy (the two
  manifests are the sole, explicitly-decided exception). [ADR-002; deprecated-apis.md]
- **Never one monolithic Addressables bundle for all content, and never a
  per-level Addressables group/bundle** — both were explicitly evaluated and
  rejected (defeats on-demand release; per-level bundle/catalog overhead dwarfs the
  KB-scale payload). [ADR-002]
- **Never ship a Remote build/load path at MVP** — `Remote_Events` must stay
  empty/unbuilt. [ADR-002]
- **Never duplicate a shared asset (e.g. a candy material) into a region bundle**
  instead of `Shared_BoardRig`. [ADR-002]
- **Never depend on `rename()`/atomic-rename semantics for save durability.** [ADR-003]
- **Never the legacy `Input` class** (`Input.GetKey`, `Input.GetMouseButton`,
  `Input.GetAxis`, `Input.mousePosition`, etc.). [technical-preferences.md; deprecated-apis.md]
- **Never URP Compatibility Mode** —
  `RenderGraphSettings.enableRenderCompatibilityMode` is read-only `false` in 6.3;
  author against Render Graph from day one. [technical-preferences.md; VERSION.md]
- **Never `UGUI`/`Canvas`/`Text`/`Image` components for new screens or HUD** — use UI
  Toolkit `UIDocument`. [deprecated-apis.md]
- **Never the legacy (pre-Shuriken) Particle System emitter.** Pooled bursts use the
  modern Shuriken `ParticleSystem` (still supported) pending the ADR-H decision on
  Shuriken vs VFX Graph — see Ambiguous/Deferred Items. [deprecated-apis.md; architecture.md §2]
- **Never `CommandBuffer.DrawMesh()`, `OnPreRender()`/`OnPostRender()`, or
  `Camera.SetReplacementShader()`** — incompatible with SRP. [deprecated-apis.md]
- **Never bitwise-combine `AccessibilityRole` values** — it is a standard (non-flags)
  enum in 6.3; assign single roles only. [technical-preferences.md; VERSION.md]
- **Never wire a live C# event/delegate from Domain across the Domain→Game
  boundary for board events** — Game consumes only `EventBridge`'s drained snapshot. [ADR-005]
- **Never let presentation code mutate Domain state or call back into Domain to
  decide a gameplay outcome; never query live `BoardModel` state during replay** —
  the sole exception is `BoardReshuffled`'s one live `get_piece_at()` sweep, made safe
  by the single-active-queue invariant. [ADR-005]
- **Never a physics dependency for core gameplay** — board logic is grid-based and
  headless; no `Rigidbody`/`Physics.*` on the gameplay path. [technical-preferences.md]
- **Never a static singleton for game state** — all dependencies injected. [coding-standards.md; CLAUDE.md]

### Performance Guardrails

- **60fps target / 16.6ms frame budget** on a 2022-era mid-range Android. [technical-preferences.md]
- **≤100 draw calls during the heaviest cascade** (typical worst-case accounting is
  ~26–39; re-profiled on target hardware at the Vertical Slice checkpoint). [architecture.md §9.3; technical-preferences.md]
- **≤400MB total memory ceiling; Addressables resident sub-budget ≤120MB.** [ADR-002; technical-preferences.md]
- **Juice VFX sub-budget: `JUICE_VFX_DRAW_CALL_BUDGET = 40`** draw calls, with a
  4-tier LOD degradation ladder. [architecture.md §9.3]
- **Boot Core+Rig async load target < ~2s on mid-range Android** (verification item,
  not yet measured). [ADR-002]
- **Touch input reads `EffectiveBoardInputEnabled` at ≤1-frame latency** from the
  gesture. [architecture.md TR-ti-003]

---

## UI Layer Rules

*Applies to: `SweetCascade.UI` (`Assets/UI/`) — UI Toolkit UXML/USS screens, HUD,
overlays, Results, World Map view.*

### Required Patterns

- **UI Toolkit (`UIDocument`, UXML + USS) for all screens/HUD** — legacy UGUI is not
  used for new screens. [technical-preferences.md; deprecated-apis.md; current-best-practices.md]
- **Lint/validate USS against Unity 6.3's stricter parser** — previously-tolerated
  invalid USS now raises validation errors. [VERSION.md; architecture.md §1/§6]
- **`AccessibilityRole` is a single standard-enum value per element**, never
  bitwise-combined. [VERSION.md; technical-preferences.md]
- **All interactive elements ≥44px touch target.** [technical-preferences.md]
- **Portrait orientation, one-handed play; hover is additive-only and desktop-only**,
  never a required interaction. [technical-preferences.md]
- **HUD updates (`ObjectiveProgressed`/`MovesRemainingChanged`) are paced by the same
  per-move drained event replay JuiceDirector uses** — objective chips/move counter
  climb in lockstep with each visual clear, never ahead of or independent from it. [ADR-005 D3 Mode B]
- **UI never owns or mutates game state directly** — screens read from
  `ScreenFlowController`/`SaveService`/`LevelData`/Domain DTOs;
  `ScreenFlowController` never writes level results directly. [architecture.md §6/§8.4]

### Forbidden Approaches

- **Never `Canvas` (UGUI), `Text`, or `Image` components for new screens.** [deprecated-apis.md]
- **Never bitwise OR'd `AccessibilityRole` flags.** [VERSION.md]
- **Never ship USS the 6.3 parser would reject** — lint before merging UI work. [VERSION.md]
- **Never read or mutate live `BoardModel`/Domain gameplay state directly from UI
  code** — UI reads only presentation-layer DTOs/pull APIs. [architecture.md §7.2/§8]

### Performance Guardrails

- **UI Toolkit's retained-mode batching keeps HUD chips/footer within the ~4–8
  draw-call share of the ≤100 total budget**; overlays add draw calls only while
  active. [architecture.md §9.3]

---

## Editor & CI Layer Rules

*Applies to: `SweetCascade.Editor` (`Assets/Editor/`) — `LevelManifestGenerator`,
`AddressablesValidation`, `ManifestCrossValidator` invocation, level validation/preview
tools — plus the CI pipeline (`game-ci/unity-test-runner@v4`).*

### Required Patterns

- **Addressables checks A1–A6 run as a designer menu item AND as a blocking CI
  step**: A1 every `LevelData` addressable and in its region group; A2 `address ==
  level_id` invariant; A3 manifest↔catalog cross-check **scoped to non-retired
  entries only** (amended 2026-07-18 to compose with ADR-006's tombstoning); A4
  label/grouping integrity, no orphans; A5 local-only-at-MVP policy (no Remote build
  path); A6 duplicate-dependency analyzer. [ADR-002 as amended; architecture-review-2026-07-18.md CONFLICT-2]
- **`LevelManifestGenerator.Reconcile(apply: true)` is the only writer of
  `level_manifest.asset`**, invoked only from the `Sweet Cascade ▸ Level Manifest ▸
  Regenerate` menu item. [ADR-006]
- **Before writing, the generator asserts the new `entries` list has the committed
  list as an exact prefix** — any existing `level_id` changed or missing at its slot
  aborts the tool with an error and writes nothing. [ADR-006]
- **New `level_id`s are sorted lexicographically ascending (ordinal
  `StringComparer`) before being appended** — deterministic regardless of OS
  file-enumeration order; `AssetDatabase.FindAssets` is used only as an unordered set
  source. [ADR-006]
- **A pre-build hook (`IPreprocessBuildWithReport`) regenerates into memory and diffs
  against the committed asset; the build fails if they differ** ("manifest stale —
  run Regenerate and commit"). It never silently mutates during build. [ADR-006]
- **The same dry-run diff plus the full `ManifestCrossValidator` (bijection, W6, W7,
  W8) runs headless in CI as a blocking gate.** [ADR-006]
- **The generator is idempotent**: a no-change Regenerate produces `Changed ==
  false` and a byte-identical asset. [ADR-006]
- **`domain-purity` CI job (L2 denylist `ripgrep` scan of `Assets/Domain/`) runs
  before the Unity build** (no Unity license needed, fails fast) and blocks the
  PR/push on any forbidden-token hit. A reviewed `// rng-purity-allow: <reason>`
  pragma is the only sanctioned per-line escape. [ADR-004]
- **The golden-vector RNG regression suite (`rng_golden_v1.json` +
  `RngGoldenVectorTest.cs`) is blocking on every PR (Mono) and on the release matrix
  (IL2CPP + WebGL) — byte-for-byte.** [ADR-004]
- **The save codec's byte-exact golden fixture (`canonical_golden_v1.sav`) fails CI
  on any output change to the canonical writer.** [ADR-003]
- **CI runs `game-ci/unity-test-runner@v4` as a blocking gate on every PR and every
  push to main.** [technical-preferences.md; coding-standards.md]
- **All board-logic and scoring formulas are unit-tested** (match detection, cascade
  resolution, special-candy creation rules, star thresholds). [technical-preferences.md]

### Forbidden Approaches

- **Never write `level_manifest.asset` from anywhere other than
  `LevelManifestGenerator.Reconcile(apply: true)`** — no hand-editing the Inspector
  list; any hand-edit that reorders/removes an entry must fail the next Reconcile. [ADR-006]
- **Never let the pre-build hook silently mutate the manifest** — it verifies and
  fails only; it never writes. [ADR-006]
- **Never disable or skip a failing test to force CI green.** [coding-standards.md]
- **Never merge the ordinal-manifest generator with the hand-authored world-map
  manifest tool.** [ADR-006]

### Performance Guardrails

- **Manifest generation/validation is O(n≤120) directory scan + reconcile** —
  sub-second, off the runtime path. [ADR-006]
- **`Editor_Fixtures` Addressables group is excluded from player builds.** [ADR-002]

---

## Global Rules (All Layers)

### Naming Conventions

| Element | Convention | Example |
|---------|-----------|---------|
| Classes | PascalCase | `BoardModel` |
| Public fields/properties | PascalCase | `MoveCount` |
| Private fields | `_camelCase` | `_moveCount` |
| Methods | PascalCase | `ResolveMatches()` |
| Events | PascalCase, past tense (mirrors the GDD signal catalog) | `MatchCleared`, `CascadeEnded` |
| Files | PascalCase matching class | `BoardModel.cs` |
| Prefabs/Scenes | PascalCase | `FruitPiece.prefab`, `Gameplay.unity` |
| Constants | PascalCase, or `UPPER_SNAKE_CASE` for GDD-registry constants | `MAX_CASCADE_DEPTH` |

Source: `.claude/docs/technical-preferences.md`.

### Performance Budgets

| Target | Value |
|--------|-------|
| Framerate | 60fps on 2022-era mid-range Android |
| Frame budget | 16.6ms |
| Draw calls | ≤100 during the heaviest cascade |
| Memory ceiling | ≤400MB total (≤120MB Addressables resident sub-budget; ADR-002) |
| Shadows | None realtime — blob-shadow decals; one directional key light |
| Juice VFX sub-budget | `JUICE_VFX_DRAW_CALL_BUDGET = 40`, 4-tier LOD ladder |

Source: `.claude/docs/technical-preferences.md`; `architecture.md` §9; `ADR-002`.

### Approved Libraries / Addons

- Unity Input System — approved, mandatory for all input.
- UI Toolkit — approved, mandatory for all screens/HUD.
- Addressables — approved, mandatory content-load path (behind `IContentLoader`).
- Unity Test Framework — approved, mandatory test framework (Edit Mode for Domain, Play Mode for integration).
- **No other dependency is approved.** Add others only when actively integrated — no
  speculative dependencies (this explicitly excludes Cinemachine, DOTS/Entities, and
  any networking package until a dedicated ADR authorizes them; DOTS is confirmed
  "not currently used" and Physics is confirmed "None for core gameplay"). [technical-preferences.md]

### Forbidden APIs (Unity 6.3 — from `deprecated-apis.md`)

| Deprecated | Use instead | Relevant to Sweet Cascade |
|---|---|---|
| `Input.GetKey/GetKeyDown/GetMouseButton/GetAxis/mousePosition` | Input System (`Keyboard.current`, `Mouse.current`, `EnhancedTouch`) | Yes — Touch & Input |
| `Canvas` (UGUI), `Text`, `Image` | `UIDocument` (UI Toolkit), TextMeshPro, `VisualElement` | Yes — all screens/HUD |
| `Resources.Load()` / synchronous asset loading | Addressables (`IContentLoader.TryLoad`) | Yes — content pipeline |
| Legacy (pre-Shuriken) Particle System | Shuriken `ParticleSystem` (current default, still supported) or VFX Graph | Yes, pending ADR-H — see Ambiguous/Deferred Items |
| `CommandBuffer.DrawMesh()`, `OnPreRender()`/`OnPostRender()`, `Camera.SetReplacementShader()` | RenderGraph API / `RenderPipelineManager` callbacks | Yes — any custom render pass |
| Legacy Animation component, `Animation.Play()` | Animator Controller, `Animator.Play()` | If used for piece/UI animation |
| `WWW` class | `UnityWebRequest` | Only if/when a network feature is added (none at MVP) |
| `Application.LoadLevel()` | `SceneManager.LoadScene()` | If scene switching is used |
| `ComponentSystem`/`JobComponentSystem`/`GameObjectEntity` (DOTS) | N/A | **Out of scope** — DOTS not used on this project |
| `Physics.RaycastAll()`, `Rigidbody.velocity` direct write | N/A | **Out of scope** — no physics dependency on this project |

Source: `docs/engine-reference/unity/deprecated-apis.md`.

### Cross-Cutting Constraints

- **Assembly dependency direction is one-way and compiler-enforced**: `Domain`
  references nothing above it and no engine assembly; `Game → Domain`; `UI → Game`
  (and may reference `Domain` downward for pure DTO/record types). No assembly ever
  references one above it. [architecture.md §5.1]
- **Domain purity is enforced at three independent layers** for the RNG (asmdef +
  CI denylist + golden vectors) and at two layers for the rest of Domain (asmdef +
  general CI grep for `UnityEngine`/`UnityEditor` symbols). [ADR-004; architecture.md §5.1]
- **All randomness flows through the seedable, stream-isolated, platform-stable RNG**
  with no player-performance input — every board must be reproducible from a logged
  seed. [architecture.md Principle 3]
- **No gameplay constant is a literal in code** — every difficulty lever, budget cap,
  and schema constant is authored/versioned data. [architecture.md Principle 4; coding-standards.md]
- **All dependencies are injected — no static singletons for game state.** [coding-standards.md; CLAUDE.md]
- **All public methods and classes have doc comments.** [coding-standards.md]
- **Maximum cyclomatic complexity of 10 per method; no method longer than 40 lines**
  (excluding data declarations). [CLAUDE.md Coding Standards Enforcement]
- **Every system exposes a clear interface, not a concrete-class dependency.** [CLAUDE.md; coding-standards.md]
- **Every system has a corresponding ADR in `docs/architecture/`.** [coding-standards.md]
- **Commits reference the governing design doc or task/story ID**; Conventional
  Commits format (`feat:`, `fix:`, `chore:`, `docs:`, `test:`, `refactor:`). [coding-standards.md]
- **Verification-driven development**: write tests first for gameplay systems; UI
  changes are verified with screenshots; every implementation has a way to prove it
  works before being marked complete. [coding-standards.md]

### Test Evidence by Story Type

| Story Type | Required Evidence | Location | Gate Level |
|---|---|---|---|
| **Logic** (formulas, AI, state machines) | Automated unit test — must pass | `tests/unit/[system]/` | BLOCKING |
| **Integration** (multi-system) | Integration test OR documented playtest | `tests/integration/[system]/` | BLOCKING |
| **Visual/Feel** (animation, VFX, feel) | Screenshot + lead sign-off | `production/qa/evidence/` | ADVISORY |
| **UI** (menus, HUD, screens) | Manual walkthrough doc OR interaction test | `production/qa/evidence/` | ADVISORY |
| **Config/Data** (balance tuning) | Smoke check pass | `production/qa/smoke-[date].md` | ADVISORY |

Automated test rules: naming `[system]_[feature]_test.[ext]` / `test_[scenario]_[expected]`;
deterministic (no random seeds, no time-dependent assertions); isolated (no
execution-order dependence); no hardcoded fixture data except boundary-value tests;
unit tests never touch external APIs/DB/file IO (dependency injection instead).
CI never disables or skips a failing test to force a pass.

Source: `.claude/docs/coding-standards.md` (Testing Standards).

---

## Ambiguous / Deferred Items (flagged, not encoded as hard rules)

These are places where a source document states a *conditional*, *pending*, or
*soon-to-be-superseded* position rather than a settled rule. They are called out here
so a programmer does not mistake a contingency for a mandate, and so the next manifest
regeneration knows to check whether they have resolved.

1. **Particle backend (Shuriken vs VFX Graph) is not yet decided** — `ADR-H` is
   listed as "Should have before the relevant system is built" and is still
   unwritten; `TR-jl-004` is a tracked Partial in the architecture review. Today's
   only certain rule is "never the legacy pre-Shuriken emitter" — which system
   backs the pooled cascade bursts is still open. [architecture.md §11; architecture-review-2026-07-18.md]
2. **Audio middleware (Unity native vs FMOD/Wwise) is not yet decided** — `ADR-G` is
   unwritten; `TR-jl-003` is a tracked Partial. [architecture.md §11; architecture-review-2026-07-18.md]
3. **Score-popup rendering technology (world-space TextMeshPro vs UI Toolkit) is not
   yet decided** — `ADR-F` is unwritten (QQ-06). [architecture.md §11/§13]
4. **Newtonsoft.Json as a save-codec fallback is a conditional, not an active rule** —
   ADR-003 permits swapping only the *reader* (`SaveJsonReader`) to Newtonsoft "if the
   bespoke reader proves too costly," with the write/checksum path staying
   hand-rolled unconditionally regardless. No trigger condition is defined for when
   this becomes active; treat the hand-rolled reader as the only implementation until
   a future ADR revision states otherwise. [ADR-003 Alternatives/Migration Plan]
5. **The master architecture's §8.1 seam signatures are stale** — they still show
   `ISpecialResolver.ActivationClears(..., ScoreKeeper score)` /
   `ExpandChain(..., ScoreKeeper score)` threading a `ScoreKeeper` parameter. ADR-005
   D2 is authoritative and drops that parameter; this manifest encodes ADR-005's
   version as the rule (see Domain Forbidden Approaches), but `architecture.md` §8.1
   itself has not been edited to match — flagged so nobody copies the stale
   signature straight from the master doc. [architecture-review-2026-07-18.md, item 3]

---

*Regenerate this manifest (`/create-control-manifest update`) whenever ADR-F, ADR-G,
or ADR-H land, or whenever any of ADR-001–006 is revised/superseded.*
