---
name: dice-system-fixes
description: Dice throw fixes — text orientation, hover-during-rolling prevention, WaitForStop timeout
type: project
originSessionId: 94465425-8040-44c3-bc12-66a7a5b3173e
---

# Dice System Fixes

## Text orientation on settled dice
`DiceThrower.OrientUpFaceToCamera()` — after snapping Euler to 90° increments, finds the up-face and rotates around Y so the face text faces the camera. Uses face transform's local Y as text-up direction with -90° offset. Called in `OrganizeDiceLayout()` between snap and DORotate.

## Hover-during-rolling breaks layout organization
**Root cause**: `DiceInputManager.HandleHover()` does Physics.Raycast every frame hitting rolling dice colliders, waking Rigidbody from sleep, preventing `WaitForStop()` from ever completing.

**Fix 1** — `PhysicsDice.WaitForStop()`: Added 5-second timeout. If velocity never settles below threshold, force-exit and proceed with CalculateValue().

**Fix 2** — `DiceInputManager.HandleHover()`: Early return while any dice is rolling (`DiceThrower.IsAnyDiceRolling()`), preventing raycasts from waking physics bodies.

**Fix 3** — `DiceThrower.IsAnyDiceRolling()`: Public method checking all activeDiceList for isRolling flag.
