# Game Concept: Sweet Cascade

*Created: 2026-07-17*
*Status: Approved — core loop validated by concept prototype (**PROCEED**, 2026-07-17); booster-brewing hook approved by founder (2026-07-17). Engine: Godot 4.6 stands unless console/native-SDK needs force a revisit.*

---

## Elevator Pitch

> It's a vibrant match-3 puzzle adventure where you swap candies to trigger
> spectacular cascades and craft your own power-ups, journeying through a
> world map of themed regions that transform with live seasonal events —
> while competing with friends on leaderboards and weekly challenges.

---

## Core Identity

| Aspect | Detail |
| ---- | ---- |
| **Genre** | Casual Puzzle — Match-3 (tile-matching, level-based progression) |
| **Platform** | Mobile-first (iOS / Android), Web as secondary reach platform |
| **Target Audience** | Casual players 25-45, short-session mobile players (see Player Profile) |
| **Player Count** | Single-player core loop with asynchronous social layer (leaderboards, friend challenges) |
| **Session Length** | 5-15 minutes (2-5 levels per session) |
| **Monetization** | F2P intent (lives/boosters model) — **not designed yet; fun-first, monetization deferred until after core loop validation** |
| **Estimated Scope** | Large (9-14 months to live-service launch, solo + AI studio agents; MVP in 6-8 weeks) |
| **Comparable Titles** | Candy Crush Saga, Royal Match, Gardenscapes |

---

## Core Fantasy

You are the spark that sets off dazzling chain reactions. Every swap is a
small decision; every cascade is the board rewarding you with fireworks. The
player feels *clever and lucky at the same time* — they made the smart move,
and the game amplified it into something spectacular. Layered on top is the
collector-traveler fantasy: journeying through a colorful world region by
region, returning each season to find the world transformed and friends
waiting to be overtaken on the leaderboard.

---

## Unique Hook

**Craft-your-own power-ups.** Like Candy Crush, AND ALSO: boosters are not
bought off a shelf — matches harvest themed ingredients (berry juice, citrus
zest, mint frost), and players brew their own power-ups between levels from
recipes they unlock. Seasonal events introduce limited-time ingredients and
recipes, making events mechanically meaningful rather than purely cosmetic.

- Explainable in one sentence: "You brew your own boosters from what you match."
- Affects gameplay: booster loadout becomes a pre-level strategic choice.
- Connects to the core fantasy: cascades literally *produce* the resources
  that fuel bigger future cascades.
- **Status: APPROVED by founder (2026-07-17).** The concept prototype tested
  only the base match-3 loop; the brewing layer remains a Phase 2 validation
  target — approval covers the direction, and a lightweight Phase 2 prototype
  still gates full implementation ("is pre-level choice fun or friction?").

---

## Player Experience Analysis (MDA Framework)

### Target Aesthetics (What the player FEELS)

| Aesthetic | Priority | How We Deliver It |
| ---- | ---- | ---- |
| **Sensation** (sensory pleasure) | 1 | Juicy cascade VFX, satisfying pop sounds, vibrant saturated palette, haptics on mobile |
| **Challenge** (obstacle course, mastery) | 2 | Level objectives, move limits, blockers, escalating mechanics per region |
| **Submission** (relaxation, comfort zone) | 3 | Low-stakes retry, no fail punishment beyond a life, ambient soundscape |
| **Fellowship** (social connection) | 4 | Leaderboards, friend challenges, seasonal community goals |
| **Discovery** (exploration, secrets) | 5 | New regions, new mechanics per world, unlockable recipes |
| **Fantasy** (make-believe) | 6 | Light world theming — candy realms, seasonal transformations |
| **Expression** (creativity) | 7 | Booster loadout choice, profile cosmetics |
| **Narrative** (drama, story arc) | N/A | No story campaign; theming carries tone instead |

### Key Dynamics (Emergent player behaviors)

- Players scan for match-4/5 setups instead of taking the first available match-3.
- Players deliberately bank ingredients toward a recipe before hard levels ("I'll brew a Rainbow Bomb for level 40").
- Players time sessions around seasonal events and daily challenges.
- Players compare progress on the map and scores on shared levels with friends, prompting "one more try" retries.

### Core Mechanics (Systems we build)

1. **Match-3 board engine** — swap, match, gravity, refill, cascade resolution.
2. **Special candies & combos** — striped, wrapped, color-bomb equivalents and their combination matrix.
3. **Level objective system** — score targets, ingredient collection, blocker clearing, move limits.
4. **Booster brewing (meta)** — ingredient harvest from matches, recipe unlocks, pre-level loadout.
5. **Live-ops layer** — seasonal themes/events, daily challenges, leaderboards, friend challenges.

---

## Player Motivation Profile

### Primary Psychological Needs Served

| Need | How This Game Satisfies It | Strength |
| ---- | ---- | ---- |
| **Autonomy** (freedom, meaningful choice) | Which match to take, which booster to brew and bring, which event to chase | Supporting |
| **Competence** (mastery, skill growth) | Reading the board, setting up specials, beating hard levels within move limits | Core |
| **Relatedness** (connection, belonging) | Friend leaderboards, challenges, seasonal community events | Supporting |

### Player Type Appeal (Bartle Taxonomy)

- [x] **Achievers** (goal completion, collection, progression) — How: star ratings, map progression, recipe collection, event completion.
- [x] **Killers/Competitors** (leaderboards) — How: friend leaderboards, weekly challenge ladders.
- [x] **Socializers** — How: friend challenges, gifting lives (post-MVP), community event goals.
- [ ] **Explorers** — Secondary: new mechanics per region reward system understanding.

### Flow State Design

- **Onboarding curve**: Levels 1-5 teach swap → match-4 → match-5 → objectives, one concept at a time, zero text walls.
- **Difficulty scaling**: New blockers/mechanics introduced per region, then mixed; difficulty saw-tooths (hard level → cooldown level).
- **Feedback clarity**: Score popups, cascade multiplier callouts, star thresholds visible during play, end-of-level star ceremony.
- **Recovery from failure**: Instant retry (costs a life); failure shows "closest miss" feedback so it feels educational, not punishing.

---

## Core Loop

### Moment-to-Moment (30 seconds)
Scan the board → spot the best swap → swap → watch matches pop and candies
cascade → new opportunities appear. The dopamine center of the game: cascades
must feel like the board is applauding the player.

### Short-Term (5-15 minutes)
Complete 2-5 levels: each level is a puzzle with an objective (score target,
collect ingredients, clear blockers) under a move limit. Earn stars, harvest
ingredients, occasionally unlock a new recipe or region gate.

### Session-Level (30-120 minutes)
Longer sessions chain level runs with brewing decisions, event progress, and
leaderboard pushes. Natural stopping points: out of lives, region gate, event
milestone reached.

### Long-Term Progression
World map advancement region by region (each with a visual theme + new
mechanic), recipe collection growth, seasonal event participation, cosmetic
unlocks, and friend leaderboard standing.

### Retention Hooks

- **Curiosity**: What does the next region look like? What's this season's event and limited recipe?
- **Investment**: Star completion, recipe collection, event progress meters.
- **Social**: A friend just passed you on the map / beat your level score.
- **Mastery**: Three-starring old levels, climbing weekly challenge ladders.

---

## Game Pillars

### Pillar 1: Every Swap Sparkles
The moment-to-moment interaction must be intrinsically juicy — visual, audio,
and haptic feedback make even a plain match-3 feel rewarding.

*Design test*: If we're debating between adding a feature and polishing
match/cascade feedback, we polish the feedback.

### Pillar 2: Clever, Never Cheated
Players lose because they ran out of good moves, never because the game felt
rigged. Randomness amplifies skill; it doesn't replace it.

*Design test*: If a difficulty mechanic works by invisibly manipulating the
board against the player, we cut it — difficulty comes from visible level
design (blockers, objectives, move limits).

### Pillar 3: The World Is Alive
Themes, seasons, and events visibly transform the game. Returning after a
break should feel like coming back to a place where something is happening.

*Design test*: If an event is purely a banner + a currency, it's not an
event — every event must change something the player sees or plays.

### Pillar 4: Friendly Rivalry
Social features create warm competition between friends, never obligation or
harassment. Compare, challenge, celebrate.

*Design test*: If a social feature punishes a player for friends being
inactive (or requires spamming invites to progress), we cut it.

### Anti-Pillars (What This Game Is NOT)

- **NOT pay-to-win**: Monetization (when designed) must never sell guaranteed level completion; it would destroy Pillar 2 (Clever, Never Cheated).
- **NOT a story game**: No dialogue trees, no narrative campaign — theming carries tone. Protects scope and Pillar 1 focus.
- **NOT real-time multiplayer**: All social features are asynchronous. Real-time netcode would blow scope and add nothing to the core fantasy.
- **NOT a mechanic zoo**: Each region introduces at most ONE new mechanic. Depth comes from combinations, not from endless novelty (protects onboarding and Pillar 2).

---

## Inspiration and References

| Reference | What We Take From It | What We Do Differently | Why It Matters |
| ---- | ---- | ---- | ---- |
| Candy Crush Saga | Level-based map, special candy combo matrix, objective variety | Player-crafted boosters instead of shelf boosters; honest difficulty (no invisible board rigging) | The genre blueprint — validates market size and session shape |
| Royal Match | Generous cascade feel, fast retry loop, modern polish bar | Ingredient/brewing meta ties events to gameplay, not just decoration | Proves the genre still grows with better game-feel |
| Gardenscapes | Meta-layer between levels drives retention | Our meta is brewing/collection, far lighter than base-building | Validates that a meta-layer multiplies retention |

**Non-game inspirations**: Candy shop window displays and patisserie aesthetics
(color palette, glossy finish); seasonal festivals (event theming); the
tactile satisfaction of bubble wrap and popping toys (feedback design).

---

## Target Player Profile

| Attribute | Detail |
| ---- | ---- |
| **Age range** | 25-45, skewing 30+ |
| **Gaming experience** | Casual; match-3 familiar — zero tutorial patience needed |
| **Time availability** | 5-15 minute sessions: commute, breaks, before bed |
| **Platform preference** | Phone (portrait, one-handed); browser at desk |
| **Current games they play** | Candy Crush Saga, Royal Match, Wordscapes |
| **What they're looking for** | Relaxing-but-clever moments, visible progress, light competition with people they know |
| **What would turn them away** | Aggressive paywalls, forced social spam, punishing difficulty spikes, cluttered screens |

---

## Technical Considerations

| Consideration | Assessment |
| ---- | ---- |
| **Recommended Engine** | Godot 4.6 + GDScript — best-in-class 2D, free (MIT), exports cleanly to Android/iOS/Web, fastest iteration for a solo-plus-agents team. (Unity is the conventional mobile pick; Godot chosen for cost, openness, and 2D strength — revisit if console or heavy native SDK integrations become priorities.) |
| **Key Technical Challenges** | Deterministic, testable match/cascade resolution; board-state serialization for challenge sharing; live-ops content pipeline (events as data, not builds); leaderboard backend |
| **Art Style** | 2D stylized, glossy "candy shop" look — saturated palette, rounded shapes, heavy juice (squash/stretch, particles) |
| **Art Pipeline Complexity** | Medium — custom 2D sprites + particle VFX; AI-assisted asset generation via studio pipeline |
| **Audio Needs** | Moderate — pop/cascade SFX layers (pitch-escalating), ambient music per region, event stingers |
| **Networking** | None for core play (offline-capable). Thin backend later for leaderboards/challenges/events — asynchronous only |
| **Content Volume** | Launch target: 4 regions × 30 levels (120 levels), ~8 recipes, 2 seasonal event templates |
| **Procedural Systems** | Candy spawn RNG (seeded, testable). Levels are hand-designed data files, not procedural |

---

## Risks and Open Questions

### Design Risks
- The brewing hook may add friction to a genre whose players expect zero-decision flow between levels — must be optional-feeling, never homework.
- Honest difficulty (no board rigging) may make late-game difficulty tuning much harder than incumbents'.

### Technical Risks
- Leaderboards/friend challenges require a backend + accounts — significant scope beyond the game client; deferred behind MVP intentionally.
- iOS deployment from Godot requires a Mac build step in the pipeline (solvable, but plan it).

### Market Risks
- The most saturated genre on mobile; discoverability against King/Playrix marketing budgets is the single biggest commercial risk.
- Differentiator (brewing) is invisible in a screenshot — store-page appeal depends on polish level.

### Scope Risks
- Live-ops (seasons, events) is a content treadmill; must be data-driven from day one or it will consume all development capacity.
- 120 hand-tuned levels is a large content commitment for a small team.

### Open Questions
- Is the base cascade loop satisfying in our hands? → **Concept prototype (built: `prototypes/sweet-cascade-concept/`)**
- Does brewing make pre-level choice fun, or is it friction? → Phase 2 paper/HTML prototype after core loop validation.
- Web as first public platform (itch.io soft launch) vs. straight to app stores? → Decide at vertical-slice gate.
- Monetization model details → Explicitly deferred until core loop is proven fun.

---

## MVP Definition

**Core hypothesis**: Swapping candies to trigger matches and multi-tile
cascades is intrinsically satisfying enough that players voluntarily replay
levels — before any meta, theming, or social features exist.

**Required for MVP**:
1. Match-3 board: swap, match detection, gravity, refill, cascade chains with visible combo feedback
2. Special candies (striped, color bomb) created by match-4/match-5, plus their basic combos
3. 10 levels with objectives (score target / collection) and move limits, saw-tooth difficulty
4. Star ratings + instant retry

**Explicitly NOT in MVP** (defer to later):
- Booster brewing meta (Phase 2 — validate separately)
- Seasonal events, themes beyond one placeholder region
- Leaderboards, friends, accounts, any backend
- Monetization, lives system, cosmetics

### Scope Tiers (if budget/time shrinks)

| Tier | Content | Features | Timeline |
| ---- | ---- | ---- | ---- |
| **MVP** | 10 levels, 1 region theme | Core loop + specials + objectives | 6-8 weeks |
| **Vertical Slice** | 30 levels, 1 polished region | + brewing meta, star gates, full juice pass | +6 weeks |
| **Alpha** | 120 levels, 4 regions | + events engine (data-driven), local leaderboards | +12 weeks |
| **Full Vision** | 120+ levels, live seasons | + backend social (leaderboards, friend challenges), 2 live events | +12 weeks |

---

## Next Steps

- [ ] **User review of this draft** — especially the brewing hook (biggest autonomous decision) and the Godot engine choice
- [x] Fill in CLAUDE.md technology stack based on engine choice (`/setup-engine`) — Godot 4.6 / GDScript, done this session
- [x] **Prototype core idea** (`/prototype`) — HTML concept prototype built at `prototypes/sweet-cascade-concept/prototype.html`; awaiting user playtest verdict
- [x] Playtest the prototype → **PROCEED** recorded in `prototypes/sweet-cascade-concept/REPORT.md` (2026-07-17)
- [x] `/art-bible` to lock the visual identity (Draft), then `/map-systems` (Draft)
- [ ] Design each system (`/design-system [system-name]`) using prototype learnings
- [ ] `/create-architecture` once MVP systems have GDDs
- [ ] Plan first milestone (`/sprint-plan new`)
