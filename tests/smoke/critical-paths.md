# Smoke Test: Critical Paths

**Purpose**: Run these 10-15 checks in under 15 minutes before any QA hand-off.  
**Run via**: `/smoke-check` (which reads this file)  
**Update**: Add new entries when new core systems are implemented.

## Core Stability (always run)

1. Game launches to main menu without crash
2. New game / session can be started from the main menu
3. Main menu responds to all inputs without freezing

## Core Mechanic (update per sprint)

**Board Engine & Cascade System** (once Match-3 Board Engine is implemented):
- [ ] Player can tap to select a candy on the board
- [ ] Player can swipe to swap two adjacent candies
- [ ] Swap creates a valid match (3+ in a row) or is rejected with visual feedback
- [ ] Matches clear and cascade resolutions trigger correctly
- [ ] Cascade continues until no more matches exist
- [ ] Board refills with new candies (deterministically for testing, seeded RNG)
- [ ] Match-4 creates a special candy (cross or bomb, per spec)
- [ ] Match-5 creates a bomb (per spec)
- [ ] Special-×-special combo interactions trigger (per combo matrix)

**Touch Parity** (once Touch & Input System is implemented):
- [ ] Touch selection works on mobile devices / simulator
- [ ] Swipe gestures are recognized correctly
- [ ] Web build accepts mouse clicks with same swap/select logic
- [ ] No input lag or missed taps

## Level Objectives & Scoring (once Level Objective & Move-Limit System is implemented)

- [ ] Move counter decrements correctly after each swap
- [ ] Match/cascade/combo point values display correctly
- [ ] Score target objective recognizes when threshold is met
- [ ] Collection objective (collect X candies) tracks correctly
- [ ] Blocker-clear objective recognizes blocker removal
- [ ] Lose condition triggers when moves = 0 before objective complete
- [ ] Win condition triggers when objective complete
- [ ] Star rating evaluates correctly at level end

## Data Integrity

- [ ] Save game completes without error (once Save & Persistence is implemented)
- [ ] Load game restores correct state (board position, move count, score) (once Save & Persistence is implemented)
- [ ] Player progress persists across app restart

## Performance

- [ ] No visible frame rate drops on target hardware (60fps target during cascade)
- [ ] No memory growth over 5 minutes of continuous play

## UI/Navigation (once Game UI/Screens Flow is implemented)

- [ ] Level select screen displays all unlocked levels
- [ ] Star gate prevents locked levels from being played
- [ ] Level complete screen shows star rating and awards
- [ ] Results screen allows returning to level select or main menu

---

## Notes for Testers

- **Determinism**: Cascade and refill order should be reproducible when using the same seed (critical for balance tuning).
- **Mobile-First**: Test on actual mobile device or simulator; web build must work with mouse equivalents.
- **Touch Targets**: Ensure all candies are ≥ 44px for touch; no hover-only interactions.
