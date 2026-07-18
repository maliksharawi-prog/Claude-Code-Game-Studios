# Characters & Narrative Tone Brief: Sweet Cascade

## Document Status

- **Version**: 1.0
- **Status**: Draft — awaiting founder review
- **Owned By**: narrative-director
- **Created**: 2026-07-18
- **Source documents**: `design/gdd/game-concept.md` (Anti-Pillars, amended 2026-07-18),
  `design/art/art-bible.md` (v1.0, approved 2026-07-17), `design/gdd/systems-index.md`

---

## Purpose & Scope

Sweet Cascade is explicitly **not a story game**. This document exists because
the founder amended the anti-pillar on 2026-07-18 to bring **light character
dressing** into scope. The amended text, quoted here as the authority this
whole document operates under:

> Light character dressing IS in scope — mascot guide characters, region
> personalities, and one-line flavor text framing levels and celebrations.
> Characters serve tone and warmth, never gate progression, and add no
> branching choices.

Everything below is written against that boundary. Nothing in this document
proposes a dialogue system, a branching narrative, a quest structure, or any
mechanic-gating use of character content. See **Section 6, Explicit
Boundaries**, for the enforceable version of this rule.

**A note on timing**: `art-bible.md` (approved one day before the amendment)
still states in its Character Art Standards section that "Sweet Cascade has
no traditional characters... there is no player avatar and no story
campaign." That line was true under the pre-amendment anti-pillar and is
still *mostly* true — there is still no player avatar and no story campaign.
But it will read as a contradiction to anyone who hits it after reading this
document. Flagged in **Section 7 (Coordination Notes)** as a recommended
addendum for `art-director`, not something this document can unilaterally fix.

---

## 1. Tone Guide

**Voice in one sentence**: Sweet Cascade sounds like a genuinely delighted
friend cheering from the sidelines — never a game trying to sell you
something, and never a game that's smarter than you.

### Voice Pillars

1. **Warm** — every line assumes good faith and affection for the player.
   No irony, no knowing winks at the player's expense.
2. **Playful** — light, bouncy word choice; contractions, everyday language,
   the occasional bit of harmless silliness. Never cute-aggressive, never
   trying too hard.
3. **Encouraging** — celebrates effort and progress, not only perfect
   outcomes. A one-star clear gets genuine warmth, not a backhanded
   "well, you cleared it I guess."
4. **Never snarky** — no sarcasm, no jokes that land *on* the player, no
   passive-aggressive nudging toward monetization or "just one more try."
5. **Never punishing** — failure copy never blames, shames, guilts, or
   pressures. It never implies the player is bad at the game.

### Failure Framing Rule

Every "lose" line uses **"so close!" energy**, always. A failed level is a
near-miss, not a loss — this is a direct extension of Pillar 2 (**Clever,
Never Cheated**) from `game-concept.md`: because the board never cheats the
player, the *narration* shouldn't pretend the player was an unlucky victim
either. The tone is "you were right there," not "better luck next time."
Never use the words *fail*, *lose*, *lost*, or *wrong* in player-facing copy.

### Reading Level & Length

- **Reading level**: target 4th–6th grade — short sentences, common words,
  no idioms that don't translate cleanly (matches the 25–45 casual audience
  in `game-concept.md`'s Target Player Profile, many playing one-handed,
  distracted, mid-commute).
- **One-liners only.** No multi-sentence blocks, no paragraphs, anywhere
  in-game. If a thought needs two sentences, it's two separate lines picked
  from a pool, not one string.
- **Repeated in-level copy** (mascot lines, flavor one-liners): target
  **≤60 characters** including spaces and punctuation. This protects
  small-screen legibility (per the art bible's HUD density rules) and gives
  localized languages — which commonly run 15–30% longer than English —
  headroom without truncation or forced font-shrinking. Hard ceiling: ~80
  characters; anything longer must be flagged as an explicit exception.
- **One-time banner/event copy** (Section 4): slightly more headroom is
  acceptable — target ≤80 characters — since it's read once, not on every
  level attempt.
- **No em dashes, semicolons, or colons** in any player-facing string —
  periods, commas, question marks, and exclamation points only. Keeps
  strings simple for small-screen fonts and translation tooling.
- **Second person always** ("you"), never third person about the player.
  Never assumes player gender.
- **No emoji** in player-facing copy. The art-directed iconography, particle
  language, and star-ceremony visuals already carry the emotional signal;
  emoji would fight the established visual language and render
  inconsistently across platforms and fonts.
- **Exclamation points are earned, not default.** Fine — expected, even —
  on wins and celebrations, but rationed to one per line, and never used on
  neutral/informational copy (settings, labels, move counters).
- **Silence is valid.** Not every level needs a line. Flavor-line pools
  should be large enough, and selection logic should avoid repeating the
  same line twice in a row, so the voice never starts to feel like a script
  on loop. (Selection/pooling logic itself is a `game-designer` /
  `ui-programmer` implementation decision, not narrative's to spec.)

---

## 2. Mascot Guide Character — Fizz

### Concept

Fizz is the little burst of delight born from the player's very first
cascade — equal parts sparkle and encouragement, always rooting for the
next great swap. This ties the mascot directly to the game's Core Fantasy
text in `game-concept.md`: *"You are the spark that sets off dazzling chain
reactions."* Fizz doesn't set off the cascades — the player does. Fizz just
can't stop celebrating them.

### Personality (3 traits)

- **Effervescent** — high energy, quick to celebrate, never sits still long.
  This is where the name comes from.
- **Warm-hearted** — genuinely rooting for the player, not performing
  enthusiasm. Fizz's warmth reads as sincere, not salesy.
- **Unflappable** — a bad board or a lost level never rattles Fizz. This is
  what makes the "so close!" failure framing believable instead of forced —
  Fizz's steadiness is what keeps losses feeling light.

### Pronouns & Identity

Fizz uses **they/them** and is never gendered in copy or art direction —
keeps the character universally approachable and sidesteps gendered-language
localization complications where possible.

### Visual One-Liner (for the art team)

> A palm-sized sugar-spirit rendered in the game's established glossy-jelly
> shading model (flat base fill, one soft radial highlight, contact-shadow
> ellipse, thin rim-line — key light fixed upper-left at 315° per the art
> bible's Light Direction Consistency Rule). Body color pulls from the
> existing **Gold Reward** family (`#ffd93d` / `#fff4b8`) so Fizz reads as
> "celebration incarnate" and is never mistaken for one of the 5 matchable
> candy hues. Soft teardrop/blob silhouette with a spun-sugar swirl tail —
> a shape that doesn't belong to any of the 5 base candy silhouettes
> (circle, hexagon, diamond, rounded-square, scallop), so it can never be
> misread as a board piece. Decorated with the shared "sparkle star" VFX
> particle motif already used game-wide, reusing an existing asset family
> instead of inventing a new one. **Lives only on non-board screens**
> (world map, pre-level card, results/star ceremony) — never rendered
> inside the active play area, consistent with the art bible's Visual
> Hierarchy and Board Frame rules.

### Screen Placement

Fizz appears on: the **World Map** (idle companion / map-guide), the
**Pre-Level Card** (one line before the Play button), and the
**Results / Star-Ceremony screen** (win, lose, and star-count reactions).
Fizz never appears inside the board frame during active play — no overlay,
no interruption, no blocking a swipe. This is a hard boundary per the art
bible, not a style preference.

Fizz is **text-only for launch scope** — no voice acting is implied or
committed by this document. If VO is desired later, that is a separate
scoped decision between `producer` and `audio-director`.

### Example Lines

Each pool below is written to final quality and ready for direct
implementation, pending founder approval of the character. Selection logic
(random, no-repeat-last-N) is a UI/gameplay implementation decision.

**Level Intro** (said on the pre-level card, before Play)

1. "Ready when you are!"
2. "Ooh, I like this board already."
3. "Let's get cascading!"
4. "Something sweet's about to happen."
5. "New moves, new magic. Let's go!"
6. "I've got a good feeling about this one."
7. "Take your time. I'll be right here."
8. "Fizz is fizzing! Let's play."
9. "Scan the board, I bet you'll spot it."
10. "Here we go. Make it sparkle!"

**Win** (level cleared, general celebration)

1. "Yes! Look at that board go!"
2. "You did it! Nailed it!"
3. "That cascade was gorgeous."
4. "I knew you had it in you!"
5. "Sweetest win I've seen all day."
6. "Chef's kiss. Truly."
7. "The board never stood a chance."
8. "That's how it's done!"
9. "You made that look easy."
10. "On to the next one. Let's go!"

**Lose** (out of moves, objective incomplete — always "so close!" energy,
never blame)

1. "So close! I saw it too."
2. "Almost had it. Try again?"
3. "That board was tricky. Round two?"
4. "One more look, I bet you'll spot it."
5. "So close it's almost annoying!"
6. "Not this time. There's always a next time."
7. "Tricky board. Let's have another go."
8. "You were one move from glory."
9. "Shake it off, you've got another go."
10. "That was close! Ready to try again?"

**Star Milestones** (results screen, keyed to stars earned)

*One star*
1. "A win's a win. Nice!"
2. "You cleared it! One star, shining bright."
3. "In the books. On to the next!"

*Two stars*
1. "Two stars! You're getting good at this."
2. "Nice combo work. Two stars earned!"
3. "Nailed it. Two stars, well deserved."

*Three stars*
1. "THREE STARS! Absolute perfection."
2. "Flawless! You cleared every star."
3. "Three stars. You're a natural!"
4. "Wow. Just wow. Three stars!"

---

## 3. Region Personalities

Per the art bible's Regional & Seasonal Palette System, each region is a
3-stop 135° gradient plus a trim/accent color, a decorative-prop set, and
an ambient-particle theme. Region 1 and Region 2 already have their
gradients and props locked (or proposed) in `art-bible.md`; Regions 3 and 4
are new proposals from this document, offered as **names + themes + mood**
only — exact hex values and final prop art remain `art-director` territory,
per this document's role boundary.

Each resident character lives only in that region's own map diorama /
region-select vignette (per the art bible's World Map spec) — they are
**not** repeating in-level companions the way Fizz is. Fizz is the one
constant; residents are local color, seen once per visit to that region's
map screen, not once per level.

> **Proposed creative thread — flag for `game-designer` / `economy-designer`
> confirmation**: `game-concept.md`'s booster-brewing hook names three
> example ingredients — *berry juice, citrus zest, mint frost*. This
> document proposes tying Regions 2–4 to those three flavors (Frosted Peak
> = mint frost, Sundrop Grove = citrus zest, Thistleberry Hollow = berry
> juice), leaving Region 1 as the "all flavors welcome" starting hub. This
> is **not a mechanical claim** — it doesn't assume brewing ingredients are
> literally sourced by region — it's a lore/flavor thread that would give
> world and mechanic a shared vocabulary if `game-designer` later wants it.
> Entirely optional to keep; flagged here rather than silently assumed.

### Region 1 — Candy Kingdom Hub

**One-line personality**: The busy, bright town square where every journey
starts — proud, welcoming, a little bit of a showoff.

**Resident**: **Marzi**
- Traits: Hospitable, Theatrical
- Visual one-liner: *A round-bodied marzipan doorkeeper in a striped
  awning-cloth cape, built from the hub's existing candy-cane-pillar and
  striped-awning prop language, in cream and Magenta Pop (`#f107a3`) trim
  tones. Appears only in the Region 1 map diorama and welcome vignette.*

**Flavor lines**
1. "Welcome, welcome! Grab a seat, the board's warmed up."
2. "Every great cascade starts right here."
3. "New to town? You'll fit right in."
4. "The whole kingdom's rooting for you."

### Region 2 — Frosted Peak

**One-line personality**: A crisp, quiet mountain that rewards patience —
cool air, warm hearts.

**Resident**: **Bristle**
- Traits: Steady, Dry-witted (gentle, never sharp)
- Visual one-liner: *A small packed-snow sprite with a mint-swirl tuft,
  built from Frosted Peak's existing icicle-shard and mint-swirl-lamppost
  prop language, in the region's Teal (`#14b8a6`) trim tones. Appears only
  in the Region 2 map diorama.*

**Flavor lines**
1. "Careful. The air's crisp, but the welcome's warm."
2. "Every peak's climbable, one swap at a time."
3. "Mint's in the air. Can you smell it?"
4. "Slow and steady wins the mountain."
5. "Cold outside, cozy in here."

### Region 3 — Sundrop Grove *(proposed — new region, name/theme this doc)*

**One-line personality**: A sun-warmed citrus orchard, lazy and golden —
everything ripens if you give it a beat.

**Suggested mood/palette** *(art-director's call on exact hex; proposed
direction only)*: warm gold-to-orange family, consistent with the same
3-stop 135° formula as Regions 1–2; decorative props in the spirit of
citrus-tree rows, sun-bleached lattice fencing, low terracotta terraces.

**Resident**: **Rind**
- Traits: Sun-warmed & laid-back, Generous
- Visual one-liner: *A citrus-blossom sprite shaped like a sun-ripened
  segment with drooping zest-peel "petals." Suggested warm gold-orange
  color family matching a "sunny orchard" mood — exact gradient/trim hex
  is art-director's decision, following the same formula as Regions 1–2.*

**Flavor lines**
1. "Take your time. The sun's not going anywhere."
2. "Sweetest fruit's always worth the wait."
3. "Zest is in the air today."
4. "Ripe for the picking. You ready?"
5. "Slow mornings, sweet afternoons."

### Region 4 — Thistleberry Hollow *(proposed — new region, name/theme this doc)*

**One-line personality**: A tangled, twilight berry patch — a little wild,
deeply cozy, best friends with mystery.

**Suggested mood/palette** *(art-director's call on exact hex; proposed
direction only)*: deep magenta-to-violet family, consistent with the same
3-stop 135° formula; decorative props in the spirit of bramble hedgerows,
string-lantern trails, low twilight fog.

**Resident**: **Bramblet**
- Traits: Curious, Cozy-mysterious
- Visual one-liner: *A round bramble-berry sprite with a thistle-swirl
  topknot. Suggested deep magenta-to-violet color family matching a
  "twilight berry hollow" mood — exact gradient/trim hex is art-director's
  decision, following the same formula as Regions 1–2.*

**Flavor lines**
1. "Something sweet's hiding in these brambles."
2. "The hollow keeps its best berries for the patient."
3. "Come on in. The thicket's friendlier than it looks."
4. "Every hollow has a secret. This one's tasty."
5. "Twilight's the best time to look around."

---

## 4. Seasonal Event Voice

Per Pillar 3 (**The World Is Alive**), events must visibly transform the
world, not just add a banner and a currency. These two examples show how
event *copy* should sound — tone reference only, not event design (event
mechanics, duration, and rewards belong to `game-designer` /
`economy-designer` when the Events/Theming Engine is built).

### Example A — "Frostlight Festival" (region reskin: Frosted Peak)

- **Banner headline**: "The Frostlight Festival has arrived!"
- **Subcopy**: "Frosted Peak's glowing with lantern-ice for two weeks. Come see!"
- **Fizz line**: "Ooh, the whole peak is sparkling! We can't miss this."

### Example B — "Cascade Carnival" (hub-wide event)

- **Banner headline**: "The Cascade Carnival is in town!"
- **Subcopy**: "New games, new prizes, confetti everywhere. Two weeks only."
- **Fizz line**: "I love carnival season. Everything sparkles twice as much!"

Note what these examples deliberately avoid: no countdown-pressure language
("HURRY, ENDS IN 2 DAYS"), no guilt ("don't miss out or you'll regret it"),
no scarcity manipulation. "For two weeks only" / "two weeks only" are stated
as simple facts, not urgency hooks — consistent with the tone guide's
never-punishing, never-pressuring rule.

---

## 5. Explicit Boundaries — What Narrative Will Never Do

Restated and made enforceable from the amended anti-pillar in
`game-concept.md`. Any future work that crosses these lines requires a new
founder-approved amendment, not a unilateral agent decision.

- **No dialogue trees, ever.** No branching dialogue, no player dialogue
  choices, no conversation UI of any kind.
- **No branching narrative campaign.** No story missions, no chapter/quest
  plot structure, no cutscenes.
- **No dedicated narrative UI.** No codex, no lore log, no quest tracker,
  no cutscene player — unless a future amendment explicitly approves one.
- **Characters never gate progression.** No "talk to Fizz to unlock," no
  fetch quests, no character-locked levels, boosters, or regions. All
  character content rides on top of *existing* screens (map, pre-level
  card, results) — it never becomes a new required interaction.
- **No forced reading.** Every line is skippable and skimmable. A player
  who mutes flavor text or taps straight through loses zero information
  needed to actually play — mechanical information always lives on the
  HUD, never exclusively in a character line (protects Pillar 2).
- **No lore dumps.** No worldbuilding text walls, no history/backstory
  screens, no "read more" narrative content anywhere.
- **No player avatar, no player-authored identity.** Consistent with the
  art bible: the player is never represented as an on-screen character.
- **No romance, rivalry, or morality-choice systems.**
- **No narrative-gated monetization.** Never sell access to a character's
  "story," a line pool, or a region's personality — would also violate the
  game's NOT pay-to-win anti-pillar.
- **No voice-acting commitment.** All character content in this document
  is text-only for launch scope.
- **Any scope increase requires producer approval**, per
  `.claude/docs/coordination-rules.md`'s No Unilateral Cross-Domain Changes
  rule — no agent expands narrative scope (more characters, dialogue,
  quests) without it.

---

## 6. Coordination Notes (for founder / other agents)

Flagged here rather than acted on unilaterally, per this agent's role
boundaries:

1. **`art-bible.md` predates this amendment by one day** and still states
   "Sweet Cascade has no traditional characters." Recommend `art-director`
   add a short addendum to the Character Art Standards section
   acknowledging Fizz + region residents and their non-board-only placement
   rule, so a future reader doesn't see it as a contradiction.
2. **Ingredient–region alignment** (Section 3's flagged proposal) touches
   the Booster Brewing Meta's ingredient list, which is `game-designer` /
   `economy-designer` territory and still gated behind a Phase 2 friction
   prototype per `systems-index.md`. Treat as an optional lore thread, not
   a locked mechanical dependency, until confirmed.
3. **Region 3 and 4 gradients/props** need `art-director` to assign final
   hex values and prop sets following the art bible's existing formula —
   this document only proposes names, mood, and resident characters.
4. **Screen placement and line-pool wiring** (where exactly these strings
   surface, selection/no-repeat logic) belongs in `screen-flow.md` and the
   relevant UX specs — a `game-designer` / `ui-programmer` handoff item.
5. **"Candy Kingdom Hub" naming heads-up**: the name predates this document
   (set in `systems-index.md`) and is out of scope for this brief to
   change, but it's worth a passing legal-awareness flag that "Candy
   Kingdom" has notable pop-culture overlap (e.g., Adventure Time) —
   a `producer`-level naming-clearance question, not a narrative one.
