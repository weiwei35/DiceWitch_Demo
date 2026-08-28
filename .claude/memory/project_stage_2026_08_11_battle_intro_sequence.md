---
name: battle-intro-sequence-stage-2026-08-11
description: Battle-panel intro gates first-round setup and input
type: project
date: 2026-08-11
---

# Battle Intro Sequence

- `Canvas_Main/Battle` owns the authored `Battle.controller` intro Animator.
- `BattleManager.battleIntroAnimator` explicitly references that Animator.
- `StartNewBattle` cleans the previous battle, hides the generated magic-circle slot icons, disables battle state/input, and waits for the Animator's current state to finish.
- Only after the intro completes does it refresh/show the slot icons, spawn enemies, start the player round, and roll dice.
- Battle background `MouseParallaxUI` components under the intro Animator are paused during the authored animation and resume afterward, so both systems never write the same layer positions at once.
- The battle Animator itself is disabled after the intro and re-enabled before the next playback. Otherwise its finished state keeps writing animated X positions every frame and blocks horizontal parallax on the split tree layers.
- Leaving the battle during the intro cancels the pending start, preventing a hidden or map-side battle from starting later.
- `nextBattleDamageBonus` is not consumed until the intro has completed and the battle actually starts, so an interrupted intro cannot lose the queued buff.
- If no valid intro Animator is assigned, battle setup falls back to starting on the next frame.
