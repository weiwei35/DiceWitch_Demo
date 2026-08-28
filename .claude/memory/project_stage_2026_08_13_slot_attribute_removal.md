# Slot Attribute Removal Stage 2026-08-13

## Removed feature boundary

The retired magic-circle slot attribute system was completely removed:

- `SlotAttributeSO` and `RuntimeSlotAttribute`
- `GameEnums.SlotAttributeType`
- `PlayerDice.currentAttribute` and `MagicCircleSlot.currentAttribute`
- Slot attribute libraries, random selection and debug mutation methods
- Base dice-value bonus injection from slot attributes
- Attribute text in magic-slot, forge, reward, battle tooltip and run-summary UI
- The magic-slot `Lv` badge object and its serialized references
- The three legacy `锋利` SlotAttribute assets and empty `Magic/Data`, `Magic/SO` folders

## Preserved systems

- `DiceFaceData.bonusValue` remains because active dice abilities, combat hooks and ghost dice use it for temporary battle point modifiers.
- Forge material elements and forged affix tags remain; they are part of the current meditation/forge system and are unrelated to the removed slot attribute feature.

## Verification

- Project-wide search found no retired type, field, method, resource GUID or UI wording.
- Unity compilation completed with zero errors and zero warnings.
- All prefabs and all project scenes contain zero missing scripts.
- Base battle deck contains three dice with faces `1-6` and zero initial bonus.
- Battle 1 loaded, spawned one enemy and rolled physical dice with no console errors.

## Existing unrelated issue observed

`PhysicsDice.ApplyTemporaryBonus` can double-apply the bonus to the currently cached face because `currentResultData` may reference an entry already updated in `visualManager.faceDatas`. This existed independently of slot attributes and was not changed during this removal.
