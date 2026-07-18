# Visual Interface Blueprint — 3D "Glass Candy" Target

*Created: 2026-07-18 · Source: founder-supplied 3D concept render (Nano Banana,
from `design/art/concept-prompts.md` Prompt 1) + approved GDDs.*
*Status: Reference blueprint — precedes the formal `/create-architecture` phase.
Core logic mirrors the APPROVED GDDs (`board-engine.md` Rev 2, `special-candies.md`,
`scoring-stars.md`); nothing here overrides them.*

> **Stack note (needs one founder word):** the project is pinned to
> **Godot 4.6 + GDScript**. The founder's deliverable request asks for **C#**.
> The scripts below are therefore written as **engine-agnostic pure C# domain
> logic** (zero `UnityEngine`/`Godot` references) — they compile in Unity, in
> Godot-with-.NET, or in a plain test runner. The UI hierarchy is given in both
> namings. If the founder intends a **Unity pivot**, say so and we update
> CLAUDE.md/technical-preferences via `/setup-engine`; if **Godot stands**, the
> production implementation of this same logic is GDScript per the pinned stack
> and these C# files remain the reference/spec-by-example.

---

## 0. Render Review — Adoption Ledger

What the concept render shows vs. the approved design:

| Element in render | Decision | Rationale |
|---|---|---|
| 3D glass-candy fruits w/ deep specular + SSS jelly look | **ADOPT** (material target) | Exactly the art bible's "patisserie glass" pushed to 3D |
| Gold metallic bevel trim on board frame + UI chips | **ADOPT** | Richer than flat cream; add "Gilded Cream" material variant to art bible on adoption |
| Deep cell wells with dark waffle/lattice texture | **ADOPT** | Upgrades our flat checkerboard wells; keep alternating A/B tint |
| "LEVEL 1: FRUIT DELIGHT" named levels | **ADOPT (small)** | Add optional `display_name` field to level-data-format v1.1 (additive, no version bump) |
| Energy arcs linking adjacent special pieces | **ADOPT (VFX backlog)** | Juicy special-adjacency tell; log in juice-layer VFX table, post-MVP |
| Booster bar (hammer/wand/bomb ×N) | **DEFER → Phase 2** | This is the brewing hook's loadout slot (`screen-flow.md` `brewing_loadout_slot`); not MVP |
| Bottom nav shell (Home/Map/Shop/Events/Profile) | **DEFER → Phase 3** | Live-ops shell; MVP screen-flow has no persistent tab bar |
| **Coins 1,200 / Gems 50 chips** | **EXCLUDE** | Monetization is explicitly not designed (concept doc anti-pillar guardrail); no currency UI until the founder scopes monetization |
| 6×6 board in render | **IGNORE** | Spec is 8×8 (up to 9×9 per level-data-format); render artifact |
| Phone bezel framing | IGNORE | Mockup dressing |

---

## 1. UI Hierarchy Tree (3D-optimized)

Left naming = requested Canvas-style (Unity); right = Godot 4.6 equivalent.
Principle: **the board is a 3D rig; the HUD is screen-space UI; game logic is
headless** (per board-engine's logic/presentation split — the 3D layer only
replays events).

```
GameRoot                                        (Node — root scene)
├── Systems  [no visuals — pure logic owners]   (Node)
│   ├── GameController                          (Node: game_controller)
│   ├── BoardModel        ← C#/GDScript domain  (Node: board_model, headless)
│   ├── InputRouter       ← gestures → intents  (Node: input_router)
│   ├── AudioDirector                           (Node: audio_director)
│   └── SaveService                             (Node: save_service)
├── Environment                                 (Node3D or CanvasLayer -1)
│   ├── GradientBackdrop   [M-Gradient]         (fullscreen quad / ColorRect+shader)
│   ├── HillLayerBack      [M-HillSoft]         (unlit alpha card, drift anim)
│   ├── HillLayerFront     [M-HillSoft]         (unlit alpha card, drift anim)
│   ├── BokehParticles     [M-Bokeh]            (ParticleSystem / GPUParticles3D, ~10 alive)
│   └── VignetteOverlay    [M-Vignette]         (screen-space quad, topmost of env)
├── BoardRig                                    (Node3D, orthographic-framed)
│   ├── BoardFrame         [M-GildedCream]      (beveled frame mesh, gold trim edge)
│   ├── BoardPanel         [M-GlassPanel]       (translucent back panel)
│   ├── CellWellGrid       [M-WellA / M-WellB]  (64 instanced well meshes — MultiMeshInstance3D / GPU-instanced)
│   ├── PieceLayer                              (Node3D)
│   │   └── FruitPiece ×64 (prefab / .tscn)     — pooled, never destroyed
│   │       ├── FruitMesh  [M-CandyGlass(hue)]  (5 meshes: berry, slice, lemon, apple, grapes)
│   │       ├── StripesOverlay [M-CreamStripe]  (enabled for Line Blast; H or V variant)
│   │       ├── BombDress  [M-BombOrb]          (swapped-in mesh for Color Bomb)
│   │       ├── GlowRing   [M-EmissiveGold]     (bomb idle pulse; bloom source)
│   │       └── ContactShadow [M-BlobShadow]    (soft ellipse decal — NOT realtime shadow)
│   └── FXLayer                                 (Node3D)
│       ├── ClearBurst pool [M-BurstAdditive]   (≤1 per cascade step)
│       ├── PopParticles pool [M-CandyBits]     (capped 24 spawns/step)
│       └── ScorePopups pool                    (billboarded TextMesh / Label3D)
└── UICanvas                                    (CanvasLayer + Control root)
    ├── HeaderPanel                             (HBoxContainer)
    │   ├── MovesChip      [M-CreamChipUI]      (PanelContainer)
    │   │   ├── MovesLabel  "MOVES"             (Label — cocoa #6b4226, tracked caps)
    │   │   └── MovesValue  "20"                (Label — bold, tabular numerals)
    │   ├── ObjectiveChip (wide, flex)          (PanelContainer)
    │   │   ├── TargetIcon                      (TextureRect)
    │   │   └── ObjectiveValue "0/1,800"        (Label)
    │   └── ScoreChip                           (PanelContainer)
    │       ├── ScoreLabel  "SCORE"             (Label)
    │       └── ScoreValue  "0"                 (Label)
    ├── FooterPanel                             (HBoxContainer)
    │   ├── MapButton      "◀ Map"             (Button, cream pill)
    │   ├── LevelLabel     "Level 1"            (Label, white + soft shadow)
    │   └── SoundButton    (speaker icon)       (Button, cream pill)
    ├── CalloutLayer       "Sweet! ×2"          (Control, non-blocking, pass-through)
    ├── OverlayStack (max depth 2, screen-flow.md) (Control)
    │   ├── PreLevelCard / ResultsCard / PauseModal
    │   └── FizzMount  ← cards ONLY, never BoardRig (narrative boundary)
    └── [reserved seams] brewing_loadout_slot (Ph2) · events_banner_slot (Ph3)
```

3D framing: orthographic (or ~10° FOV) camera straight-on; pieces get depth
from materials/lighting, not perspective distortion — grid readability is the
accessibility contract and must not be sacrificed to camera angle.

Lighting rig: ONE key directional light top-left (matches painted highlight
grammar), ambient tinted from the region gradient, **no realtime shadows on
mobile** (blob decals instead), bloom post-pass for emissives only.
Budgets (technical-preferences): 60fps mid-range mobile, ≤100 draw calls in
heaviest cascade → fruits via 5 shared meshes + per-instance hue, wells via
one MultiMesh/instanced draw, particles pooled and capped.

## 2. Materials & Shaders

| # | Material | Applied to | Technique (engine-agnostic) | Key parameters |
|---|---|---|---|---|
| 1 | **M-CandyGlass** | 5 fruit meshes | PBR: clearcoat + light subsurface "jelly" + fresnel rim; one painted highlight in albedo so gloss reads even unlit | albedo = candy hue (#ff5d73/#ff9f45/#ffd93d/#7ddf64/#b47aea per-instance), roughness 0.08–0.15, clearcoat 1.0, SSS strength ~0.25 hue-tinted, rim white @ grazing |
| 2 | **M-GildedCream** | Board frame, chip borders | Cream dielectric + gold metallic trim via edge mask; bevel normal map gives "inner highlight top / inner shadow bottom" from the single key light | cream #fff8ef rough 0.35; trim metallic 1.0 rough 0.25 gold #d6a94e; bevel normals baked |
| 3 | **M-GlassPanel** | Board back panel | Transmission/translucent, slight frost blur, tinted toward region gradient | opacity ~0.25, blur radius small, tint from region palette |
| 4 | **M-WellA / M-WellB** | 64 cell wells (instanced) | Darkened translucent inset + waffle/lattice normal texture (adopted from render); AO baked into well mesh for inner shadow | two tints (alternating checkerboard), normal strength subtle so fruits stay dominant |
| 5 | **M-CreamStripe** | Line Blast overlay | Thin cream bands clipped to fruit UV/mask (3 bands, H or V) + faint emissive so shimmer reads | #fff8ef, emissive 0.15, band width per art bible "hue stays dominant" rule |
| 6 | **M-BombOrb** | Color Bomb | Dark glass orb + 6 emissive rainbow dots + white fresnel ring + **pulsing gold emissive** (sine, 1.6s) — the only per-piece dynamic light source (emissive+bloom, not a real light) | base #17172b→#4a4a6a radial, dots = 5 fruit hues + gold, pulse amplitude modest; disabled pulse under reduced-motion |
| 7 | **M-BlobShadow** | Under every piece | Soft ellipse decal/quad, multiply blend | ~28% piece width, alpha 0.25; replaces realtime shadows |
| 8 | **M-Gradient** | Backdrop quad | Unlit 3-stop diagonal gradient in shader (NOT a texture — reskins per region by swapping 3 colors, Pillar 3 seam) | #7b2ff7 → #f107a3 (55%) → #ff8c42, 160° |
| 9 | **M-HillSoft** | 2 hill cards | Unlit radial-gradient alpha cards, slow X drift (46s/30s alternate) | deep violet rgba(74,15,160,.75), raspberry rgba(190,30,120,.7) |
| 10 | **M-Bokeh** | Particle orbs | Additive soft-circle sprite, depth-fade, slow rise | white core→transparent 70%, 10 alive, 18–40s lifetimes |
| 11 | **M-Vignette** | Screen overlay | Radial darkening, static | transparent 55% → rgba(40,5,60,.38) at corners |
| 12 | **M-BurstAdditive** | Clear burst | Additive radial gold flash, scale 0.25→1.5 over 380ms (flash-safety: ≤1/step) | #fff4b8→#ffd93d 45%→transparent 70% |
| 13 | **M-CreamChipUI** | HUD chips (2D) | 9-slice cream panel, bevel top-highlight/bottom-shadow baked; gold hairline border (adopted) | cream #fff8ef, text cocoa #6b4226 |

Post stack: bloom (threshold tuned so ONLY emissives bloom: bomb dots/ring,
burst, stripes shimmer), vignette can live here instead of a quad. No
chromatic aberration / film grain — art bible: clean, readable, cheerful.

## 3. Core C# Scripts (engine-agnostic domain logic)

Pure C#, no engine namespaces. `BoardView`/`MonoBehaviour`/Godot node layers
subscribe to `BoardEvent`s and replay them (deferred-replay model,
board-engine.md §13). Faithful to the approved GDDs: same-axis Line Blast,
colorless Color Bomb, four-cell combo matrix, deterministic passive
detonation, chain-multiplier scoring with per-piece bonuses.

### 3.1 `PieceTypes.cs`

```csharp
public enum SpecialType { None = 0, StripeH = 1, StripeV = 2, ColorBomb = 3, Wrapped = 4 /*reserved*/ }

public readonly struct Piece
{
    public const int ColorNone = -1;              // Color Bomb is colorless
    public readonly int Color;                    // index into level color_pool
    public readonly SpecialType Special;
    public Piece(int color, SpecialType special = SpecialType.None) { Color = color; Special = special; }
    public bool IsBomb => Special == SpecialType.ColorBomb;
    public bool IsStripe => Special == SpecialType.StripeH || Special == SpecialType.StripeV;
}

public readonly struct Cell { public readonly int R, C; public Cell(int r, int c) { R = r; C = c; }
    public override int GetHashCode() => R * 16 + C;
    public override bool Equals(object o) => o is Cell x && x.R == R && x.C == C; }

// Event payloads carry FULL piece identity (board-engine.md Rev 2, Blocking-1 fix)
public abstract record BoardEvent;
public record SwapAccepted(Cell A, Cell B) : BoardEvent;
public record SwapRejected(Cell A, Cell B) : BoardEvent;
public record MatchCleared(int ChainIndex, (Cell cell, Piece piece)[] ClearedPieces) : BoardEvent;
public record SpecialSpawned(Cell At, Piece Piece, int ChainIndex) : BoardEvent;
public record PiecesSpawned((Cell cell, Piece piece)[] Pieces, bool Bootstrap) : BoardEvent;
public record CascadeEnded(int TotalChains) : BoardEvent;
public record BoardStabilized() : BoardEvent;
public record BoardReshuffled() : BoardEvent;
```

### 3.2 `BoardModel.cs` — grid, swaps, matching, cascade loop

```csharp
using System; using System.Collections.Generic; using System.Linq;

public sealed class BoardModel
{
    public readonly int Size;                 // 3..9 (level-data-format)
    readonly bool[,] _mask;                   // true = playable
    readonly Piece?[,] _grid;
    readonly int _poolSize;                   // 3..5 colors this level
    readonly Random _refillRng;               // seeded: "board-refill" stream (rng-service.md)
    public readonly List<BoardEvent> Events = new();
    public const int MaxCascadeDepth = 20, MaxChainExpansion = 10;

    public BoardModel(int size, bool[,] mask, int poolSize, int seed)
    { Size = size; _mask = mask; _poolSize = poolSize; _refillRng = new Random(seed);
      _grid = new Piece?[size, size]; Bootstrap(); }

    public Piece? At(Cell c) => InBounds(c) && _mask[c.R, c.C] ? _grid[c.R, c.C] : null;
    bool InBounds(Cell c) => c.R >= 0 && c.R < Size && c.C >= 0 && c.C < Size;
    int ColorAt(int r, int c) { var p = _mask[r, c] ? _grid[r, c] : null;
        return p is { } q && !q.IsBomb ? q.Color : Piece.ColorNone; }

    void Bootstrap()
    {
        var spawned = new List<(Cell, Piece)>();
        for (int r = 0; r < Size; r++) for (int c = 0; c < Size; c++)
        {
            if (!_mask[r, c]) continue;
            int col;                                   // bootstrap retries to avoid instant matches;
            do { col = _refillRng.Next(_poolSize); _grid[r, c] = new Piece(col); }
            while (MakesRun(r, c, col));               // cascade refill (below) deliberately does NOT
            spawned.Add((new Cell(r, c), _grid[r, c]!.Value));
        }
        Events.Add(new PiecesSpawned(spawned.ToArray(), Bootstrap: true));
    }

    bool MakesRun(int r, int c, int col) =>
        (c >= 2 && ColorAt(r, c - 1) == col && ColorAt(r, c - 2) == col) ||
        (r >= 2 && ColorAt(r - 1, c) == col && ColorAt(r - 2, c) == col);

    public record Run(bool Horizontal, int Length, int Color, Cell[] Cells);

    public List<Run> FindRuns()
    {
        var runs = new List<Run>();
        for (int r = 0; r < Size; r++) ScanLine(runs, i => new Cell(r, i), true);
        for (int c = 0; c < Size; c++) ScanLine(runs, i => new Cell(i, c), false);
        return runs;
        void ScanLine(List<Run> acc, Func<int, Cell> at, bool horiz)
        {
            int i = 0;
            while (i < Size)
            {
                var c0 = at(i); int col = _mask[c0.R, c0.C] ? ColorAt(c0.R, c0.C) : Piece.ColorNone, len = 1;
                while (col >= 0 && i + len < Size) { var cn = at(i + len);
                    if (!_mask[cn.R, cn.C] || ColorAt(cn.R, cn.C) != col) break; len++; }
                if (col >= 0 && len >= 3)
                    acc.Add(new Run(horiz, len, col, Enumerable.Range(i, len).Select(k => at(k)).ToArray()));
                i += Math.Max(1, col >= 0 ? len : 1);
            }
        }
    }

    /// Entry point per player intent. Returns false → SwapRejected (no move consumed).
    public bool TrySwap(Cell a, Cell b, SpecialResolver specials, ScoreKeeper score)
    {
        if (At(a) is not { } pa || At(b) is not { } pb) return false;
        (_grid[a.R, a.C], _grid[b.R, b.C]) = (pb, pa);

        if (specials.IsActivationSwap(pa, pb))                       // seam 1 — always a valid move
        { Events.Add(new SwapAccepted(a, b));
          var seed = specials.ActivationClears(this, a, b, score);   // seam 2 (+ consumes swap specials)
          ClearAndCascade(seed, startChain: 1, specials, score, swapCells: null);
          return true; }

        if (FindRuns().Count == 0)                                   // no match → revert
        { (_grid[a.R, a.C], _grid[b.R, b.C]) = (pa, pb);
          Events.Add(new SwapRejected(a, b)); return false; }

        Events.Add(new SwapAccepted(a, b));
        ClearAndCascade(null, startChain: 1, specials, score, swapCells: new[] { a, b });
        return true;
    }

    void ClearAndCascade(HashSet<Cell> preSeed, int startChain, SpecialResolver specials, ScoreKeeper score, Cell[] swapCells)
    {
        int chain = startChain;
        if (preSeed != null) { ResolveStep(preSeed, chain++, specials, score); }
        while (chain <= MaxCascadeDepth)
        {
            var runs = FindRuns(); if (runs.Count == 0) break;
            var cleared = new HashSet<Cell>();
            var owned = new HashSet<Cell>();
            foreach (var run in runs.OrderByDescending(x => x.Length))   // cluster precedence (specials F3)
            {
                foreach (var cl in run.Cells) cleared.Add(cl);
                if (run.Length >= 4 && !run.Cells.Any(owned.Contains))
                {
                    var anchor = swapCells?.FirstOrDefault(s => run.Cells.Contains(s))
                                 ?? run.Cells[run.Length / 2];           // swap-anchor else run-middle
                    if (anchor.Equals(default(Cell)) && swapCells != null) anchor = run.Cells[run.Length / 2];
                    var sp = run.Length >= 5
                        ? new Piece(Piece.ColorNone, SpecialType.ColorBomb)
                        : new Piece(run.Color, run.Horizontal ? SpecialType.StripeH : SpecialType.StripeV); // SAME-AXIS rule
                    cleared.Remove(anchor); _grid[anchor.R, anchor.C] = sp;
                    Events.Add(new SpecialSpawned(anchor, sp, chain));
                }
                foreach (var cl in run.Cells) owned.Add(cl);
            }
            swapCells = null;                                            // steps 2+: run-middle anchoring only
            ResolveStep(cleared, chain, specials, score);
            chain++;
        }
        Events.Add(new CascadeEnded(chain - 1));
        if (!HasAvailableMove(specials)) Reshuffle();
        Events.Add(new BoardStabilized());                               // objectives evaluate HERE
    }

    void ResolveStep(HashSet<Cell> cleared, int chain, SpecialResolver specials, ScoreKeeper score)
    {
        for (int i = 0; i < MaxChainExpansion && specials.ExpandChain(this, cleared, score); i++) { } // seam 4 fixpoint
        var payload = cleared.Where(c => At(c) != null).Select(c => (c, At(c)!.Value)).ToArray();
        score.OnMatchCleared(chain, payload);
        Events.Add(new MatchCleared(chain, payload));
        foreach (var c in cleared) _grid[c.R, c.C] = null;
        ApplyGravityAndRefill();
    }

    void ApplyGravityAndRefill()
    {
        var spawned = new List<(Cell, Piece)>();
        for (int c = 0; c < Size; c++)
        {
            int segStart = 0;
            while (segStart < Size)                                     // column segments: candy NEVER
            {                                                           // falls through masked voids
                while (segStart < Size && !_mask[segStart, c]) segStart++;
                int segEnd = segStart; while (segEnd < Size && _mask[segEnd, c]) segEnd++;
                int write = segEnd - 1;
                for (int r = segEnd - 1; r >= segStart; r--)
                    if (_grid[r, c] is { } p) { if (write != r) { _grid[write, c] = p; _grid[r, c] = null; } write--; }
                for (int r = write; r >= segStart; r--)
                { var np = new Piece(_refillRng.Next(_poolSize));        // cascade refill: no retry — that IS the cascade
                  _grid[r, c] = np; spawned.Add((new Cell(r, c), np)); }
                segStart = segEnd;
            }
        }
        if (spawned.Count > 0) Events.Add(new PiecesSpawned(spawned.ToArray(), Bootstrap: false));
    }

    public bool HasAvailableMove(SpecialResolver specials)
    {
        for (int r = 0; r < Size; r++) for (int c = 0; c < Size; c++)
        {
            if (At(new Cell(r, c)) is not { } p) continue;
            foreach (var (dr, dc) in new[] { (0, 1), (1, 0) })
            {
                var n = new Cell(r + dr, c + dc);
                if (At(n) is not { } q) continue;
                if (specials.IsActivationSwap(p, q)) return true;
                (_grid[r, c], _grid[n.R, n.C]) = (q, p);
                bool ok = FindRuns().Count > 0;
                (_grid[r, c], _grid[n.R, n.C]) = (p, q);
                if (ok) return true;
            }
        }
        return false;
    }

    void Reshuffle()   // row-major collect + reapply, deterministic via refill stream (board-engine §11)
    {
        var cells = new List<Cell>(); var pieces = new List<Piece>();
        for (int r = 0; r < Size; r++) for (int c = 0; c < Size; c++)
            if (At(new Cell(r, c)) is { } p) { cells.Add(new Cell(r, c)); pieces.Add(p); }
        for (int t = 0; t < 80; t++)
        {
            for (int i = pieces.Count - 1; i > 0; i--)
            { int j = _refillRng.Next(i + 1); (pieces[i], pieces[j]) = (pieces[j], pieces[i]); }   // Fisher–Yates
            for (int i = 0; i < cells.Count; i++) _grid[cells[i].R, cells[i].C] = pieces[i];
            if (FindRuns().Count == 0 && HasAvailableMove(new SpecialResolver())) break;
        }
        Events.Add(new BoardReshuffled());
    }
}
```

### 3.3 `SpecialResolver.cs` — combo matrix + chain expansion (seams 1/2/4)

```csharp
using System; using System.Collections.Generic; using System.Linq;

public sealed class SpecialResolver
{
    // Seam 1 (special-candies.md §5): exactly {any Bomb-involving swap, Stripe+Stripe}.
    // Deliberately NO solo stripe-swap activation at MVP.
    public bool IsActivationSwap(Piece a, Piece b) =>
        a.IsBomb || b.IsBomb || (a.IsStripe && b.IsStripe);

    // Seam 2: returns the seed clear set; consumes the two swap pieces (bonuses credited).
    public HashSet<Cell> ActivationClears(BoardModel board, Cell ca, Cell cb, ScoreKeeper score)
    {
        Piece a = board.At(ca)!.Value, b = board.At(cb)!.Value;
        var set = new HashSet<Cell> { ca, cb };
        score.OnSpecialConsumed(a); score.OnSpecialConsumed(b);

        if (a.IsBomb && b.IsBomb) { ForEach(board, (cell, _) => set.Add(cell)); return set; }        // F5: board blast
        if (a.IsBomb || b.IsBomb)
        {
            var other = a.IsBomb ? b : a; 
            if (other.IsStripe)                                                                       // F7: every same-color
                ForEach(board, (cell, p) => { if (!p.IsBomb && p.Color == other.Color) {              //     cell fires a line
                    set.Add(cell); AddLine(board, set, cell, other.Special == SpecialType.StripeH); } });
            else ForEach(board, (cell, p) => { if (!p.IsBomb && p.Color == other.Color) set.Add(cell); }); // F4: color clear
            return set;
        }
        AddLine(board, set, ca, a.Special == SpecialType.StripeH);                                    // F6: both fire from
        AddLine(board, set, cb, b.Special == SpecialType.StripeH);                                    //     landing cells
        return set;
    }

    // Seam 4: ONE pass; BoardModel iterates to fixpoint. Passive bomb = DETERMINISTIC
    // most-common color, tie-break lowest pool index (F8) — never random, never defused.
    public bool ExpandChain(BoardModel board, HashSet<Cell> set, ScoreKeeper score)
    {
        var add = new List<Cell>(); var consumed = new List<(Cell, Piece)>();
        foreach (var cell in set.ToArray())
        {
            if (board.At(cell) is not { } p || p.Special == SpecialType.None) continue;
            if (p.IsStripe) AddLineTo(board, add, cell, p.Special == SpecialType.StripeH);
            else if (p.IsBomb)
            { int target = MostCommonColor(board);
              if (target >= 0) ForEach(board, (c2, q) => { if (!q.IsBomb && q.Color == target) add.Add(c2); }); }
            else continue;
            score.OnSpecialConsumed(p); consumed.Add((cell, new Piece(p.Color)));
        }
        foreach (var (cell, plain) in consumed) SetPlain(board, cell, plain);   // consume marker (fixpoint bookkeeping)
        bool grew = false; foreach (var c in add) if (set.Add(c)) grew = true;
        return grew;
    }

    static int MostCommonColor(BoardModel b)
    { var counts = new Dictionary<int, int>();
      ForEach(b, (_, p) => { if (!p.IsBomb) counts[p.Color] = counts.GetValueOrDefault(p.Color) + 1; });
      return counts.Count == 0 ? -1 : counts.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key).First().Key; }

    static void ForEach(BoardModel b, Action<Cell, Piece> f)
    { for (int r = 0; r < b.Size; r++) for (int c = 0; c < b.Size; c++)
        { var cell = new Cell(r, c); if (b.At(cell) is { } p) f(cell, p); } }
    static void AddLine(BoardModel b, HashSet<Cell> set, Cell at, bool horizontal)
    { if (horizontal) for (int c = 0; c < b.Size; c++) { var x = new Cell(at.R, c); if (b.At(x) != null) set.Add(x); }
      else            for (int r = 0; r < b.Size; r++) { var x = new Cell(r, at.C); if (b.At(x) != null) set.Add(x); } }
    static void AddLineTo(BoardModel b, List<Cell> list, Cell at, bool horizontal)
    { var tmp = new HashSet<Cell>(); AddLine(b, tmp, at, horizontal); list.AddRange(tmp); }
    static void SetPlain(BoardModel b, Cell c, Piece plain) => BoardModelAccessor.SetPiece(b, c, plain);
}
// Note: SetPiece is an internal accessor (or make _grid internal) — kept out of the
// public API so views can never mutate the board (board-engine logic/presentation rule).
```

### 3.4 `ScoreKeeper.cs` — scoring-stars.md formula

```csharp
public sealed class ScoreKeeper
{
    public const int TileBaseValue = 20;
    public int FinalScore { get; private set; }
    int _pendingBonus;                                     // per-piece activation bonuses accrue
                                                           // at consumption, credited to the step
    public int BonusFor(SpecialType s) => s switch
    { SpecialType.StripeH or SpecialType.StripeV => 60, SpecialType.ColorBomb => 180, _ => 0 };

    public void OnSpecialConsumed(Piece p) => _pendingBonus += BonusFor(p.Special);

    // step_score = chain_index × (TILE_BASE_VALUE × |pieces| + Σ bonuses); linear, uncapped
    public void OnMatchCleared(int chainIndex, (Cell cell, Piece piece)[] cleared)
    { FinalScore += chainIndex * (TileBaseValue * cleared.Length + _pendingBonus); _pendingBonus = 0; }

    public int GetCurrentScore() => FinalScore;            // ratified pull API (Rev 2)
    public (int score, int stars) GetScoreResults(int s1, int s2, int s3) =>
        (FinalScore, FinalScore >= s3 ? 3 : FinalScore >= s2 ? 2 : FinalScore >= s1 ? 1 : 0);
}
```

**Binding notes**: Unity — a `BoardView : MonoBehaviour` drains `board.Events`
each move and coroutine-replays them onto the pooled `FruitPiece` prefabs;
Godot-.NET — identical, a `board_view.cs` Node replays into the scene tree.
Objectives/moves live outside `BoardModel` (level-objectives.md owns them):
count a move on `SwapAccepted`, evaluate win/lose only on `BoardStabilized`.

## Open items for the founder

1. **Engine word**: Unity pivot vs Godot 4.6 stands (changes nothing in this
   blueprint; changes which language the production build is written in).
2. Adoption ledger rows marked ADOPT will be reconciled into `art-bible.md`
   (gilded-cream trim, lattice wells, level display names, special-arc VFX)
   on confirmation.
3. Coins/Gems and Shop remain excluded until monetization is scoped.
