---
name: weak-guide-system-2026-07-25
description: One-time weak-guide persistence, visual rules, screen arbitration, and Forge, map, branch, and battle guide flows
type: project
originSessionId: 2026-07-25-weak-guide-system
---

# Weak Guide System — 2026-07-25

## User Intent

The weak-guide system exists to solve a UI affordance problem: after an interface opens, players may not know which element is actionable.

Confirmed product rules:

- A guide is shown only when its step has never been completed.
- Once a step is completed, it is persisted and never automatically replayed.
- The earlier idea of replaying after five seconds of inactivity was explicitly removed.
- Clicking blank space or an unrelated button does not dismiss or complete the active guide.
- A guide completes only after the intended business action succeeds.
- No hand indicator is used. Ordinary button guides have no arrow; battle drag guides explicitly use a target arrow.
- The target keeps its original color.
- UI guide halos duplicate the target `Image` sprite and display parameters, so round and irregular buttons follow their real alpha silhouette instead of receiving a rectangular frame.
- A dedicated UI halo shader reads only sprite alpha and ignores source RGB, keeping every halo warm white-yellow even when the source art has black edges.
- Click targets use breathing scale plus halo. Hold targets keep their own scale and use a looping outer ring that contracts inward.
- Multi-step guides are uncommon. Ordinary screens should use generic Inspector configuration; special flows such as Forge/Meditation may use explicit orchestration code.

## Runtime Architecture

### `WeakGuideService`

Global runtime and persistence owner:

- Bootstraps before scene load and survives scene changes with `DontDestroyOnLoad`.
- Stores completed stable guide IDs in PlayerPrefs.
- Current persistence key: `DiceWitch.WeakGuideProgress.v1`.
- Arbitrates active screens with a stack so only the topmost eligible screen displays a guide.
- Supports suspending a screen while a modal layer blocks its underlying controls.
- Provides `ActivateScreen`, `DeactivateScreen`, `ShowGuide`, `ClearGuide`, `CompleteGuide`, and `IsCompleted`.
- Includes the development ContextMenu action `开发/清除全部弱引导记录`.

### `WeakGuideEffect`

Pure visual behavior:

- Does not change the source `Graphic.color`.
- Creates a non-raycast halo child that copies the target `Image` sprite, type, fill, aspect, and sprite-mesh settings.
- Uses `UIWeakGuideHalo.shader` to sample the copied sprite alpha, discard its RGB, and render only two soft bands outside the real silhouette.
- Do not use Unity `Outline` for this halo: inheriting a transparent base hides the outline, while disabling alpha inheritance duplicates the full sprite and fills solid buttons with light.
- Only targets without a usable `Image` sprite fall back to the generated hollow rectangular frame.
- `Pulse` mode breathes the target scale and halo.
- `HoldCharge` mode leaves the target scale untouched and repeatedly contracts the halo from outside toward the target edge.
- Uses `JuicyButtonEffect.SetGuideScaleFactor` when available so hover/press and guide scaling compose instead of fighting over `localScale`.
- Falls back to direct scale animation when a button has no `JuicyButtonEffect`.

### Generic Screen Configuration

- `WeakGuideScreen` owns ordinary screen activation, ordering, and blocking state.
- `WeakGuideTarget` is the Inspector-configurable target entry.
- Use the generic components for normal one-step screens instead of adding screen-specific manager code.

### Stable IDs

All stable IDs live in `WeakGuideIds`.

Changing an existing ID is equivalent to releasing a new guide version and causes that step to appear again for every player.

## Forge/Meditation Five-Step Guide

Forge/Meditation is a special state-dependent guide implemented in `ForgeUIManager` and `ForgeMaterialInputPanel`.

The accepted sequence is:

1. `forge.material_entry.v1`
   - Guide the material-slot entry button.
   - Complete only after the material-slot bar successfully opens.
2. `forge.material_slot.v1`
   - Guide the first active material-slot frame.
   - Complete only after clicking the slot successfully opens the material bag.
3. `forge.first_resource.v1`
   - Guide the first valid raw-material button on the current bag page.
   - Complete only when that exact guided resource is successfully consumed and placed into a slot.
4. `forge.meditate.v1`
   - After all three slots are filled, guide the Meditation button.
   - Complete only when meditation successfully creates an inspiration.
5. `forge.commit_inspiration.v1`
   - After meditation, guide the preferred pending inspiration.
   - Complete only after the required hold duration succeeds and the inspiration is committed.

Forge guide arbitration:

- An open material bag is the top modal layer.
- While the bag is open, underlying Meditation or inspiration guides must not show through.
- Pending-inspiration hold shake and success feedback animate the child icon, not the option-button root. The root scale is shared by `JuicyButtonEffect` and `WeakGuideEffect`; writing hold shake there causes short-click scale multipliers to accumulate.
- The bag dynamically destroys and recreates its resource buttons during refresh, so the guide target must be rebound to the currently generated first valid resource button.
- If the player closes the bag without selecting the guided resource, the resource guide remains incomplete and resumes when the bag reopens.
- After a step is completed and the interface is reopened, that step must not replay.
- Outside the bag modal, pending inspiration has priority over Meditation; Meditation has priority when all materials are ready; otherwise the unfinished material steps are considered.

## Material and State-Flow Invariants

Weak-guide integration must not own or duplicate inventory mutation.

- Placing a material continues to consume exactly one resource through `ForgeManager.TryConsumeResource`.
- Closing or abandoning the interface continues to refund each staged material exactly once.
- Replacing a staged material preserves the existing refund-then-consume rollback behavior.
- Guide completion callbacks observe successful material actions but do not perform inventory mutation.
- Closing a modal never marks its current guide complete.
- Test or development resets of weak-guide progress must restore the previous PlayerPrefs state after automated verification.

## Important Files

- `Assets/Scripts/UI/WeakGuide/WeakGuideService.cs`
- `Assets/Scripts/UI/WeakGuide/WeakGuideEffect.cs`
- `Assets/Scripts/UI/WeakGuide/WeakGuideScreen.cs`
- `Assets/Scripts/UI/WeakGuide/WeakGuideTarget.cs`
- `Assets/Scripts/UI/WeakGuide/WeakGuideIds.cs`
- `Assets/Scripts/UI/WeakGuide/ProjectedDiceWeakGuide.cs`
- `Assets/Scripts/UI/JuicyButtonEffect.cs`
- `Assets/Scripts/UI/ForgeUIManager.cs`
- `Assets/Scripts/UI/ForgeMaterialInputPanel.cs`
- `Assets/Scripts/Map_New/MapInteractionManager.cs`
- `Assets/Scripts/Battle/BattleManager.cs`
- `Assets/Scripts/Dice/UI_DiceView/DiceViewMonitor.cs`
- `Assets/Scripts/Dice/UI_DiceView/DiceInputManager.cs`

## Map And Battle Extensions

### Map Dice

- Stable ID: `map.roll_dice.v1`.
- The old invisible `RollDice_Map` button was removed.
- `MapInteractionManager` now raycasts through its explicit map `DiceViewMonitor`; only clicking the rendered 3D map dice starts a roll.
- Clicking empty space inside the RawImage does not roll.
- The map `UI_DiceView (1)` monitor explicitly references `DiceCamra_map`. Do not point it at the battle dice camera.
- A `ProjectedDiceWeakGuide` draws a non-raycast UI frame around the 3D dice without scaling or recoloring the physical model.
- The guide completes when a valid map-dice click successfully starts the roll.

### Battle Drag Sequence

- Stable IDs:
  - `battle.throw_to_self.v1`
  - `battle.throw_to_enemy.v1`
- Display order is self first, enemy second.
- Step 1 projects a guide frame around one available die and draws a static arrow to `PlayerUITarget`.
- Step 2 rebinds to an available die and draws the arrow to the first living enemy.
- Battle guidance owns a warm white-yellow `TargetingArrow` visual copy. The original singleton remains exclusively owned by live drag input.
- While a die is dragged, the fixed guide arrow remains visible and the original mouse-following arrow appears alongside it.
- Hiding the drag arrow on release never hides the guide arrow; completing or exiting the guide hides only the guide copy.
- A successful drop on a player target completes the self step; a successful drop on any enemy completes the enemy step.
- Completion is target-based, not final-number-based. Spells, forge affixes, or enemy statuses may reduce, redirect, or replace the result without invalidating that the player learned the drag action.
- Either target action may be completed early. If the player attacks an enemy before finishing the displayed self step, the enemy step is persisted silently and will not replay.
- Clone/squad dice notify the same target-based completion path.
- `BattleManager.battleDiceViewMonitor` explicitly references the battle `UI_DiceView`.

### Battle End Turn

- Stable ID: `battle.end_turn.v1`.
- During the player turn, after every physical dice object has finished its throw and been consumed, the End Turn button pulses and glows.
- The guide waits for the last die's flight and hit resolution because consumed state is derived from the remaining live dice objects, not only the dice-use counter.
- A player turn with an empty starting deck also shows the guide immediately.
- The guide completes only after the End Turn click passes the active-battle and player-turn guards.
- Starting a battle replaces the End Turn button listener instead of stacking another listener, so one click starts exactly one enemy-turn flow.

### Map Branch Choice

- Stable ID: `map.choose_branch.v1`.
- All dynamically created branch buttons pulse and glow simultaneously so no route looks recommended.
- Choosing any branch completes the guide after `CommitBranchChoice`.
- `WeakGuideService` supports multiple `WeakGuideEffect` instances under one guide ID while preserving the existing single-target API.

### Projected 3D Dice Visual

- `ProjectedDiceWeakGuide` maps 3D renderer bounds through the selected dice camera into its RawImage.
- The generated frame reuses the same source-independent warm white-yellow hollow sprite as button guides.
- The projection never intercepts input and never changes the physical dice transform, collider, material color, or layout tween.

## Dice Target Semantics

The project has one ordinary dice behavior determined by the recipient:

- Dice dropped on an enemy enter the damage pipeline.
- Dice dropped on the player grant armor.
- Bound abilities, forge affixes, and statuses modify the value or add post-hit behavior.

`DiceActionType` and `DiceFaceData.type` were removed, together with their dice-asset serialization, because `Defend` / `Magic` face modes were not part of the intended design and created ambiguity.

## Future Extension Rules

When adding a guide:

1. Add a stable versioned ID to `WeakGuideIds`.
2. Prefer `WeakGuideScreen` and `WeakGuideTarget` for ordinary screens.
3. Call `CompleteGuide` from the successful business action, not from pointer-down, hover, opening animation start, or unrelated clicks.
4. Explicitly arbitrate modal layers so only the topmost actionable target can play.
5. For dynamically generated UI, refresh or rebind the target after the UI collection is rebuilt.
6. Verify persistence, close/reopen behavior, unrelated clicks, modal blocking, and the affected gameplay resource/state invariants.

## Verification

Accepted runtime verification covered:

- Entry -> first slot -> first bag resource continuous guide progression.
- Dynamic bag-button target binding.
- Closing the bag without selection does not complete the resource guide.
- Reopening the bag resumes the unfinished resource guide.
- A completed step does not replay after reopening.
- Material placement consumes exactly once.
- Leaving the flow refunds staged material exactly once.
- Unity script compilation completed without errors.
- No new Unity Console errors were introduced.
- Map-dice empty-space raycast does not hit a die; the projected die center does.
- Map guide frame renders as a hollow glow and follows the visible 3D die.
- Battle self-first and enemy-second arrows were visually verified in Play Mode.
- The guide arrow and live drag arrow can remain visible simultaneously; hiding the live arrow does not affect the guide copy.
- The real `OptionUI_Forge` prefab's halo reuses its round source sprite and fixed-color shader; the previous rectangular-box regression is covered by a runtime assertion.
- The halo must have visible vertex alpha and zero Unity `Outline` components; the edge-only shader keeps the button center transparent.
- Hold-charge mode starts outside the target, contracts inward, and does not modify the target's own scale.
- Enemy completion can be persisted out of order while the self guide remains active.
- Completing both battle steps removes the frame and static arrow.
- With no remaining physical dice during the player turn, the End Turn button receives the guide effect; accepting the click completes it and disables the button for the enemy turn.
- Multiple generated branch buttons simultaneously receive the guide effect.
- The pre-verification weak-guide PlayerPrefs JSON was restored after testing.
