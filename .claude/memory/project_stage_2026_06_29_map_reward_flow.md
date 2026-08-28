---
name: map-branch-reward-flow-stage-2026-06-29
description: Stage summary — branching map movement/config, room info in map node tooltips, and reward spell-to-dice selection panel separated from battle UI
type: project
originSessionId: current-codex-session
date: 2026-06-29
---

# Map Branch + Reward Flow Stage Summary (2026-06-29)

## User Preferences Learned
- User validates Unity runtime personally; Codex should do source inspection and static checks unless asked otherwise.
- For UI-heavy features, first preserve logic and make simple functional UI, then improve visuals later.
- Avoid reusing battle-only UI for non-battle progression flows when it creates state coupling.

## Branching Map System

The map now supports branch paths between rooms. Branching does not happen within a room. A branch is configured as room exit choices.

Configuration:
- Main prefab currently configured by user: `Assets/Prefab/Map/MapBG_new.prefab`.
- `MapRegionLayout.orderedRooms` remains the full ordered set of rooms for generation/display.
- `MapRoomLayout.nextRooms` defines explicit successor rooms.
- Empty `nextRooms` preserves old linear behavior and automatically connects to the next `orderedRooms` room.
- Multiple `nextRooms` entries create a branch.

Movement behavior:
- Player still rolls dice and moves automatically while no branch is reached.
- If movement reaches a room exit and there are remaining dice steps, the map pauses and shows branch choice buttons to the right of the current node.
- After the player chooses a branch, movement continues and consumes the remaining steps.
- If the roll ends exactly at the branch point, no branch selection opens until a later roll tries to leave that room.
- Only the final landing node triggers effects. Passing through nodes still does not trigger effects.

Runtime data:
- `BoardRoom` stores room id, room data, start/end node indices, and successor room ids.
- `MapManager.GenerateBoard()` builds both `boardNodes` and `boardRooms`.
- `MapManager.TryGetNextNode()` is the central movement query and handles linear movement, skip-cleared-room movement, single successor movement, and branch choices.

Scene/editor preview:
- `MapRegionLayout.OnDrawGizmos()` now draws reference lines using the same branch graph:
  - internal room nodes connect in order;
  - room exit connects to configured `nextRooms`;
  - empty `nextRooms` connects to the next ordered room.

Runtime map route lines:
- Historical implementation: `MapViewController.DrawRouteLines()` drew room-exit branch lines from `BoardRoom.nextRoomIds`.
- Superseded on 2026-08-03: runtime procedural route dots were removed. Route lines are now independent, hand-authored UI artwork inside the region prefab. Scene-view Gizmo helpers remain.

## Map Node Tooltips

Map node tooltips now show room context in addition to node effect:
```
房间: {roomName}
类别: {roomType}
```

Implementation:
- `MapViewController` injects runtime `BoardNode.roomDataRef` into each instantiated `MapNodeAnchor`.
- `MapNodeAnchor` stores this room data and appends room name/category to the tooltip.
- Disabled/invalidated nodes also show room info.

## Reward Spell Selection Decoupled From Battle UI

Old reward flow:
```
Battle victory
→ SpellDraftPanel
→ select spell
→ panel closes
→ return to battle UI
→ highlight battle MagicCircleDisplay
→ click battle dice slot to attach spell
→ MapState
```

New reward flow:
```
Battle victory
→ SpellDraftPanel
→ select spell
→ panel closes
→ RewardDiceSelectionPanel
→ choose a dice slot/dice
→ MagicCircleManager.ImprintAbilityToDice(...)
→ MapState
```

The important change is that `TargetSelectionState` no longer uses battle `MagicCircleDisplay.SetSelectionMode(true)` and no longer relies on battle UI slot clicks. It opens an independent reward dice selection panel.

## New Reward Dice Selection UI Scripts

### `Assets/Scripts/UI/Rewards/RewardDiceSelectionPanel.cs`

Responsibilities:
- Owns the independent dice selection panel shown after selecting a reward spell.
- Shows pending spell preview.
- Generates selectable dice slot buttons from `MagicCircleManager.magicSlots`.
- Calls back with selected `MagicCircleSlot`.
- Has a runtime fallback panel if no explicit UI prefab/scene object is configured.
- Sets itself as last sibling when shown so it appears above other UI.

Inspector fields for a custom panel:
- `panelRoot`
- `slotsContainer`
- `slotButtonPrefab`
- `spellIconImage`
- `titleText`
- `spellNameText`
- `spellDescriptionText`

### `Assets/Scripts/UI/Rewards/RewardDiceSlotButton.cs`

Responsibilities:
- Displays one selectable dice slot/dice.
- Shows dice icon, slot label, and dice name.
- Shows tooltip for bound ability and forged affixes. The retired slot-attribute system was removed on 2026-08-13.
- Does not call battle `DiceThrower.HighlightDice`; this keeps the reward panel independent from battle 3D dice and battle dice tray state.

## State Flow Changes

### `TargetSelectionState`

Now:
- `Enter()` hides battle UI and opens `RewardDiceSelectionPanel`.
- `Exit()` hides `RewardDiceSelectionPanel`.
- `OnSlotClicked(...)` is intentionally empty; battle `MagicCircleDisplay` clicks are no longer used for reward attachment.
- `OnSlotSelected(...)` validates slot, calls `MagicCircleManager.Instance.ImprintAbilityToDice(slot.currentDice, _pendingSpell)`, refreshes `MagicCircleDisplay` if present, then completes the original continuation.

### `MapState`

Now hides `RewardDiceSelectionPanel` as a cleanup guard when entering map state.

## Important Flow Invariants

- Battle UI remains hidden during reward draft and reward dice selection.
- Reward spell must be applied exactly once to a selected unlocked slot with a non-null dice.
- After successful imprint, the original battle victory continuation should still return to `MapState`.
- `RewardDiceSelectionPanel` should not depend on battle `MagicCircleDisplay`, battle `DiceThrower`, or battle dice tray input.
- Existing `MagicCircleDisplay` can still be refreshed after imprint so any map/battle visual copy is up to date when shown later.

## Key Files

| File | Purpose |
|---|---|
| `Assets/Scripts/Map/BoardRoom.cs` | Runtime room graph data for branch paths |
| `Assets/Scripts/Map/MapRoomLayout.cs` | Design-time room config, including `nextRooms` |
| `Assets/Scripts/Map/MapRegionLayout.cs` | Ordered room list and Scene-view reference lines |
| `Assets/Scripts/Map/MapManager.cs` | Builds `boardRooms`, resolves next node/branch choices |
| `Assets/Scripts/Map/MapInteractionManager.cs` | Stepwise map movement and branch choice buttons |
| `Assets/Scripts/Map/MapViewController.cs` | Runtime route lines and room data injection into node anchors |
| `Assets/Scripts/Map/MapNodeAnchor.cs` | Node visual/tips, now includes room info |
| `Assets/Scripts/GameFlow/SpellDraftState.cs` | Spell draft state, still hands chosen spell to target selection |
| `Assets/Scripts/GameFlow/TargetSelectionState.cs` | Reward target selection now opens independent panel |
| `Assets/Scripts/GameFlow/MapState.cs` | Cleanup guard for reward dice selection panel |
| `Assets/Scripts/UI/Rewards/RewardDiceSelectionPanel.cs` | Independent reward dice selection panel |
| `Assets/Scripts/UI/Rewards/RewardDiceSlotButton.cs` | Button UI for selecting a dice slot/dice |

## Caveats / Future UI Work

- Branch choice UI is intentionally simple: buttons are generated to the right of the current node. It is logic-first and can later be replaced with polished map UI.
- `RewardDiceSelectionPanel` has a runtime fallback panel for logic validation. For final UI, create a designed Unity panel and assign its references explicitly.
- `RewardDiceSlotButton` intentionally avoids battle dice highlight. If a future reward panel needs its own visual preview, implement it inside the reward panel rather than reusing battle tray state.
