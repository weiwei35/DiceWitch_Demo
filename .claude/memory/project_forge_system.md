---
name: forge-system
description: Dice affix forging system — complete backend code, UI flow, inventory, and remaining Editor setup
type: project
originSessionId: 7791e3c7-8824-46ca-a085-d27c574c5d97
updatedSessionId: 94465425-8040-44c3-bc12-66a7a5b3173e
---

# Dice Forging System

## Architecture
Forging is a **node event** (BoardNodeType.Forge), not a room event. Trigger order: ProcessNodeEffect (node bonus) → Forge → DelayEnterRoom.

## New files (8)
- `Scripts/Progression/ForgeData.cs` — ForgeSlot, ForgeSession, ForgeResourceType enum, ResourceInventoryEntry struct
- `Scripts/Progression/SO/ForgeAffixSO.cs` — ScriptableObject: affixName, tier, tag, quality, icon, description, bonus
- `Scripts/Progression/SO/ForgeResourceSO.cs` — ScriptableObject: resourceName, resourceType, rarity, icon, description
- `Scripts/Progression/ForgeManager.cs` — Singleton, dual-track probability engine, inventory system (GainResource, GetResourceCount, _inventory), initialInventory list visible in Inspector
- `Scripts/GameManager/ForgeState.cs` — IGameState, calls ForgeUIManager.ShowForge/Hide
- `Scripts/UI/ForgeUIManager.cs` — Singleton, two-phase flow: Preparing → Forging, loops back to Preparing after attach
- `Scripts/UI/ForgeOptionButton.cs` — Icon + name + tooltip + attachButton
- `Scripts/UI/ForgeResourceButton.cs` — Icon + countText (TextMeshProUGUI) + tooltip
- `Scripts/UI/ForgeDiceSelector.cs` — Icon + tooltip

## Modified files (key ones)
- `ProgressionData.cs` — PlayerDice.forgeSlots, PlayerDice.icon
- `MagicCircleManager.cs` — defaultDiceIcon (centralized blank dice icon config), InitializeGame creates 3 ForgeSlot entries per dice
- `MagicSlotUI.cs` — blank dice reads icon from MagicCircleManager.Instance.defaultDiceIcon
- `ForgeManager.cs` — inventory system: _inventory Dictionary, GainResource(SO, amount) public API, GetResourceCount(SO), initialInventory Inspector list loaded on Awake, AddResource now returns bool and consumes from inventory
- `ForgeUIManager.cs` — selection order free (dice+resource or resource+dice), after attach returns to Preparing (can forge multiple dice in one session), UpdateResourceInteractable updates both button state and count text real-time, resource buttons disabled when count=0
- `ForgeResourceButton.cs` — countText display, RefreshCount method
- `ForgeDiceSelector.cs` — icon priority: dice.icon → dice.boundAbility?.icon → fallbackIcon

## Forge UI Flow (updated)
**Preparing phase**: select dice + select resource (any order) → "开始锻造" enabled
**Forging phase**: dice locked, resource consumed from inventory, option generated. Can add more resources (same type allowed if count>0) → "再次锻造" up to 3 times. Must attach one option to complete.
**After attach**: returns to Preparing phase, dice list refreshed, can forge another dice. Panel only closes when user clicks close button.

## Resource Inventory
- `ForgeManager.initialInventory` — Inspector-configured starting quantities, loaded into _inventory on Awake
- `ForgeManager.GainResource(res, amount)` — public API for reward systems to grant resources
- `ForgeManager.GetResourceCount(res)` — query current inventory count
- Each forge investment deducts 1 from inventory; button disabled when count reaches 0
- Count text updates real-time via UpdateResourceInteractable()

## Dice Icon System
- Blank dice icon: configured once in `MagicCircleManager.defaultDiceIcon` (Inspector)
- Ability dice icon: on each `DiceAbilitySO` asset's icon field
- Priority: dice.icon → dice.boundAbility?.icon → fallbackIcon (defaultDiceIcon)

## Combat pipeline
1. OnRollEnd: `finalValue += forgeSlot.affix.bonus` → synced to `currentResultData.bonusValue` via hookDelta
2. OnCalculateDamage: pass-through
3. OnPostHit: available for status effects etc.

## Hold-to-Commit (Long Press Affix)
- Replaced click-to-commit with long-press: player holds an option for holdDuration seconds (default 3s).
- `ForgeOptionButton` now implements `IPointerDownHandler`/`IPointerUpHandler`, forwards events to `ForgeUIManager.OnOptionPressStart`/`OnOptionPressEnd`.
- `HoldCommitSequence` coroutine: two lines grow from spell-icon edge and option edge toward midpoint via `Image.Type.Filled` (fillAmount 0→1), not stretch. Lines shake during hold, intensity decreases near end.
- Releasing early cancels (lines destroyed, option resets, no commit). Holding full duration triggers `CommitAffix`.
- All interactions locked during hold: dice switch, material slots, bag, confirm button, close button cancels hold.
- Key Inspector fields: `holdDuration`, `holdLineColor` (tint), `holdLineThickness` (default 20), `holdShakeIntensity`, `holdShakeFrequency`.
- `connectionLineSprite` field (shared by hold lines and committed option lines). Committed lines use `Image.Type.Sliced`; hold lines use `Image.Type.Filled`.
- Helpers: `CreateHoldLine(fillFromStart)`, `SetHoldLineFull(from,to,dir)`, `SetHoldLineFill(progress)`, `ApplyHoldLineShake(offset)`, `CancelHold()`, `GetCenterRectTransform()`, `GetRectEdgePoint(center,size,target)`.

## Remaining Editor setup
- Create SO assets: right-click → Create → Forge → Affix / Resource; right-click → Abilities → ...
- Add ForgeManager to a persistent GameObject, populate allAffixes + allResources + initialInventory
- Create ForgeUIManager on Canvas with all sections
- Prefabs: diceSelectButtonPrefab, resourceButtonPrefab (needs countText child TMP), optionButtonPrefab
- MapIconConfigSO, node prefab ForgeRow, BoardMapConfigSO forge nodes
- Wire ForgeResourceButton.countText to a TextMeshProUGUI child in resourceButtonPrefab
