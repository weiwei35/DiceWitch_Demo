---
name: forge-dice-ui-stage-2026-05-31
description: Current stage summary — fixed dice results, clone dice handling, forge meditation UI, material flow, animations, and DiceViewMonitor issue
type: project
originSessionId: current-codex-session
date: 2026-05-31
---

# Forge + Dice UI Stage Summary (2026-05-31)

## User Preferences Learned
- User wants full interaction design aligned before code for complex UI changes.
- User prefers simple, explicit Unity Inspector configuration when references are already clear.
- Avoid duplicate configuration paths: do not both auto-find scene children and expose the same references in Inspector.
- Runtime auto-find by hierarchy/name is acceptable only as a fallback when the user asks for less manual setup; for this project stage, explicit fields are preferred for Forge UI.
- User does not require Unity runtime verification from Codex; code-level sanity is enough unless asked otherwise.

## Fixed Dice Result Feature
- Requirement: map node can set `nextBattleFixedDiceValue`; next battle one random dice must land/reveal as that value.
- `ProgressionData.BattleDiceEntry` now carries `forcedResultValue`.
- `BattleManager` assigns forced result to a randomly selected battle dice entry when a map node provided it.
- `PhysicsDice` supports pending forced result and applies it after layout, including forge affix settlement.
- Visual flow: dice rolls normally, settles, layout organizes dice, then chosen forced dice is nudged/revealed to target face.

## Clone / Squad Dice Handling
- Clone dice split into multiple mini dice after landing.
- Mini dice are grouped under `DiceSquadGroup` so the whole split result occupies one normal layout slot.
- Mini dice are arranged compactly inside that slot instead of spreading all dice across the tray.
- Mini dice face orientation is normalized during layout so active face text/bonus faces camera.
- Dragging a clone group and releasing without target returns the group to the saved tray pose.
- Mini dice now participate as independent dice for enemy effects that care about first/last dice order.
- Forge affixes on original clone dice propagate to mini dice so tooltips and damage match.

## Hover / Layout Interaction
- Original issue: hover breathing animation could interrupt dice layout organization.
- Resolution: dice highlight uses static material/color highlight rather than transform scale/breathing on the physical dice.
- Avoid transform breathing effects on dice while layout or physics is controlling dice positions.

## Forge Return-To-Map Extension
- `BattleManager.EndBattleVictory()` now has an extension path for battles without card/reward draft.
- If no draft reward is configured, it transitions back to map state instead of only logging.
- Future configuration point: battle room reward/draft flag, currently `rewardAbilityDraft` style flow.

## Forge Meditation Backend
- Old flow was one material -> one option.
- New flow: 3 material slots must be filled before meditation.
- Putting a material into a slot immediately consumes inventory.
- Replacing/removing a slotted material refunds the old resource.
- `ForgeManager.TryConsumeResource(res, amount)` and `ForgeManager.RefundResource(res, amount)` expose inventory mutation.
- `ForgeManager.MeditateWithResources(dice, resources)` requires exactly 3 resources, generates one affix option, and supports max 3 generated options per slot.
- `ForgeSession.CanForgeMore` and `ForgeCount` now use generated option count, not invested material count.
- Selecting/committing one affix writes to the current forge slot and clears `CurrentSession`.
- Important bug fix: when committing a generated option, any unmeditated materials still sitting in ingredient slots must be refunded before commit.

## Forge Meditation UI Flow
- Current intended flow:
  1. Select/current dice is shown.
  2. Center spell icon and lower current dice icon both display current dice icon; blank dice uses `MagicCircleManager.defaultDiceIcon`.
  3. Player fills 3 material slots.
  4. Meditation button becomes enabled.
  5. Meditation consumes the 3 slotted materials and generates an inspiration/affix option.
  6. Player can fill 3 more materials and meditate again, up to 3 generated options.
  7. Player selects one generated option to commit to the dice.
  8. Already committed affixes remain visible near center and are connected to the center spell icon.

## Material Slot Interaction
- Current material flow is "continuous fill":
  - Click an empty slot to open bag.
  - Bag stays open.
  - Clicking a resource fills the current slot and advances to first empty slot.
  - After all 3 slots are filled, bag closes and meditation button enables.
- Replace / refund behavior:
  - Click a filled slot to open bag in replacement mode.
  - Click a bag resource to replace that slot; old material is refunded.
  - Click the same filled slot again while bag is open to refund and clear it.
- Bag inventory display:
  - Bag shows only resources with count > 0.
  - 0-count resources are not shown greyed out.

## Forge UI Explicit References
- `ForgeUIManager` should be configured through Inspector references.
- Removed `EnsureReferences()` and child-name auto-finding from Forge UI.
- Important fields:
  - `spellIconImage`: center spell/dice icon.
  - `currentDiceIcon`: lower current dice icon between left/right buttons.
  - `optionPlacementCenter`: usually the center spell icon RectTransform; option positions are relative to it.
  - `materialSlotButtons`: explicit list of 3 material slot buttons.
  - `bagItemContainer`: where bag resource buttons are instantiated.
  - `bagCloseButton`: optional explicit close button for bag.
  - `optionsContainer`: parent for generated/committed affix UI and branch lines.

## Forge UI Tooltips
- Added `ForgeDiceTooltipTarget`.
- Attach this component to dice/spell icons that should show dice tooltip.
- Tooltip includes bound ability and forged affixes. The retired slot-attribute system was removed on 2026-08-13.
- Blank dice with no special properties shows "没有任何特殊属性".

## Forge UI Option Display and Lines
- Pending generated options are displayed around `optionPlacementCenter` using `optionOffsets`.
- Committed affixes are displayed persistently using `committedOptionOffsets`.
- Committed affixes have UI lines drawn from center icon to affix node.
- Lines are generated as UI `Image` objects with `raycastTarget = false`.
- Config:
  - `showCommittedOptionLines`
  - `committedOptionLineColor`
  - `committedOptionLineThickness`

## Forge UI Animations
- Center spell icon has idle breathing; lower current dice icon does not breathe.
- Switching dice:
  - Center spell icon and lower current dice icon play a small switch pop.
  - If material slots contain resources, they refund and clear with sequential slot animations.
- Material slot animation:
  - New material pops into slot.
  - Replacement does a short shrink/pop transition.
  - Refund/clear shrinks out then restores empty slot.
- Pending option animation:
  - Generated option appears from center, moves to target offset, scales in, then floats lightly.
- Commit animation:
  - Selected option plays a heavier "hammer / final decision" style scale pulse before `CommitAffix`.
  - `_isCommittingAffix` guards against repeated clicks/switch/meditate during commit animation.

## Dice RawImage Interaction Issue
- Symptom: left-top battle dice tray visible, but actual interactable area appeared at right-bottom.
- Cause: project has two valid `UI_DiceView` objects:
  - Battle left-top `UI_DiceView`.
  - Map right-bottom `UI_DiceView (1)`.
- Old `DiceViewMonitor.Awake()` used `Instance = this`, so whichever monitor Awoke last stole the global singleton.
- After Forge UI scene edits, Awake/order changed and battle input started using map DiceViewMonitor.
- Fix:
  - `DiceInputManager` now has explicit `diceViewMonitor` field.
  - In Inspector, drag the battle left-top `UI_DiceView` monitor into `DiceInputManager.diceViewMonitor`.
  - `DiceViewMonitor` singleton now prefers object named exactly `UI_DiceView` as a fallback, but explicit reference is the stable solution.
- Do not delete map `UI_DiceView (1)`; it is a valid separate map view.

## Files Touched Most In This Stage
- `Assets/Scripts/UI/Forge/ForgeUIManager.cs`
- `Assets/Scripts/UI/Forge/ForgeOptionButton.cs`
- `Assets/Scripts/UI/Forge/ForgeDiceTooltipTarget.cs`
- `Assets/Scripts/Progression/ForgeManager.cs`
- `Assets/Scripts/Progression/ForgeData.cs`
- `Assets/Scripts/Dice/UI/DiceInputManager.cs`
- `Assets/Scripts/Dice/UI/DiceViewMonitor.cs`
- `Assets/Scripts/Dice/DiceThrower.cs`
- `Assets/Scripts/Dice/DiceDragger.cs`
- `Assets/Scripts/Dice/PhysicsDice.cs`
- `Assets/Scripts/Dice/SpecialDice/DiceSquadGroup.cs`

## Current Caveats / Future Notes
- `project_forge_system.md` contains older Forge UI flow notes; this stage summary supersedes its UI flow sections.
- `DiceInputManager.diceViewMonitor` should remain explicitly configured in the scene.
- If adding new Forge UI elements, prefer explicit serialized fields over auto-find logic to avoid hidden duplicate configuration.
- Unity runtime verification was intentionally left to the user; Codex used static code checks (`git diff --check`) and source inspection.
