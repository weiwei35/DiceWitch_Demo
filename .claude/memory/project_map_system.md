---
name: map-system
description: Complete map/board system — board generation, node types, movement, landing flow, room clearing, state machine transitions
type: project
originSessionId: 8632ebdf-3b91-4cd5-91fb-5cd351530b29
---
# Map/Board System

## Architecture Overview

The map is a linear board of connected nodes grouped into **rooms**. Each room can contain multiple nodes of different types. When the player lands on a node, it triggers effects and (if the room hasn't been cleared) enters the room's event (battle, shop, etc.).

**Key concept**: A "room" is a group of consecutive nodes sharing the same `roomId`. One node per room (type `RoomEvent`) is the entry trigger. But entering happens when landing on ANY node in an uncleared room.

## Board Setup

### ScriptableObject hierarchy (design-time, Inspector-configured)
```
BoardMapConfigSO
  └─ List<BoardRegionConfig> regions
       └─ regionName, regionPrefab (with MapRegionLayout)
            └─ MapRegionLayout
                 └─ List<MapRoomLayout> orderedRooms
                      └─ roomData (RoomDataSO), List<MapNodeAnchor> roomNodes
                           └─ MapNodeAnchor: nodeType, effectValue, forgeBonusType
```

### Runtime generation (`MapManager.GenerateBoard()`)
Iterates all regions → rooms → anchors, creates flat `List<BoardNode> boardNodes` with global `index`, `roomId`, `regionIndex`, `type`, `effectValue`, `forgeBonusType`, `roomDataRef`.

## BoardNode Fields
```csharp
int index;                              // Global position (0..N-1)
GameEnums.BoardNodeType type;           // Node effect type
int effectValue;                        // Effect magnitude
GameEnums.BoardNodeType forgeBonusType; // Bonus sub-type when type==Forge
int roomId;                             // Room membership ID
RoomDataSO roomDataRef;                 // Room data reference
int regionIndex;                        // Region section
bool isInvalidated;                     // Skipped via room-clear jump
```

## BoardNodeType Enum (10 types)
| Type | Effect |
|---|---|
| `Empty` (0) | Nothing |
| `HpChange` (1) | Heal (+) or damage (-) |
| `ResourceChange` (2) | Gain/lose mana dust |
| `RoomEvent` (3) | Triggers room's main event |
| `NextBattleArmor` (4) | Armor next battle |
| `NextBattleFixedDice` (5) | Fixed dice value next battle |
| `BlockNextDamage` (6) | Shield next damage |
| `NextBattleDamageUp` (7) | +Damage next battle |
| `Relic` (8) | Gain relic |
| `Forge` (9) | Enter forge (dice enchantment) |

## 核心规则：经过节点不触发

**只有最终落点触发 `OnPlayerLanded`，棋子跳跃经过的中间节点一律不生效。**

举例：房间有节点1(HP+3)、节点2(HP-3)、节点3(秘点+10)。投出2步，棋子从起点跳过节点1，落在节点2——只有节点2 的 HP-3 生效，节点1 的 HP+3 不触发。

这是由移动路径计算决定的：`MapInteractionManager.HandleDiceResult` 直接计算 `targetIndex`，不存在「每经过一个节点就触发一次」的逻辑。所有节点的效果、buff、debuff 都只在落点生效。

## Player Movement Flow

1. **Click "Roll Dice"** → `MapInteractionManager.OnRollDiceClicked()` spawns physics dice, disables button
2. **Dice settles** → `HandleDiceResult(steps)` computes path:
   - 从 `MapManager.currentPlayerNodeIndex` 出发
   - 每步 +1 前进
   - **跳跃逻辑**：如果当前房间 `skipRemainingNodesOnClear == true` 且已通关，第一步直接跳到 `GetNextRoomStartIndex()`（下一房间的首节点），跳过当前房间剩余所有节点
   - 收集路径上每个节点的世界坐标
3. **Pawn animates** → `MapPlayerPawn.MoveAlongPath()` DOTween jump-bounce 沿路径跳跃
4. **On complete** → update node visuals, call `MapManager.OnPlayerLanded(landedNode)`

## Landing Logic: OnPlayerLanded (MapManager.cs:113)

### Phase 1: ProcessNodeEffect
Immediate effects + floating text for the node type:
- `HpChange` → `PlayerManager.Heal/TakeDamage`
- `ResourceChange` → `ResourceManager.AddManaDust/TrySpendManaDust`
- `NextBattleArmor` → sets `PlayerManager.nextBattleArmorBonus`
- `NextBattleFixedDice` → sets `PlayerManager.nextBattleFixedDiceValue`
- `BlockNextDamage` → sets `PlayerManager.hasBlockNextDamageShield`
- `NextBattleDamageUp` → sets `PlayerManager.nextBattleDamageBonus`
- `Forge` → checks `forgeBonusType` and applies corresponding sub-effect

### Phase 2: Room entry decision
```
If node.type == Forge:
  1. StartForgeProcess(callback)
  2. callback: if room NOT cleared → DelayEnterRoom(landedNode)
               if room cleared → ChangeState(MapState)
Else:
  DelayEnterRoom(landedNode)
```

## skipRemainingNodesOnClear 详解

这是 `RoomDataSO` 上的 bool 属性，决定房间通关后对同一房间内剩余节点的处理。

### 作用两级

**1. 标记失效（DelayEnterRoom 内）**
战斗/事件首次进入时，`DelayEnterRoom` 将 `roomId` 加入 `clearedRoomIds`。如果 `skipRemainingNodesOnClear == true`，遍历落地节点之后所有同 `roomId` 的节点，将其 `isInvalidated` 设为 true。

```csharp
if (landedNode.roomDataRef.skipRemainingNodesOnClear)
{
    for (int i = landedNode.index + 1; i < boardNodes.Count; i++)
    {
        if (boardNodes[i].roomId == landedNode.roomId)
            boardNodes[i].isInvalidated = true;
        else break;
    }
}
```

**2. 移动时跳过（HandleDiceResult 内）**
掷骰时计算路径，如果当前所在房间 `skipRemainingNodesOnClear && room cleared`，则第一步直接跳到 `GetNextRoomStartIndex()`——找到第一个不属于当前 roomId 的节点索引，从那里开始。等价于跳过当前房间剩余所有节点。

### 举例

房间1 (skipRemainingNodesOnClear=true) 包含节点1、节点2、节点3：
- 第一次投出 1 步，落在节点1 → 触发节点效果 → 进入战斗 → 胜利
- 节点2、节点3 被标记 `isInvalidated = true`
- 下次掷骰，第一步直接跳到房间2 的首节点

### 默认行为（false）
如果 `skipRemainingNodesOnClear == false`：房间通关后剩余节点不会被标记失效。玩家仍可一步步经过并落在这些节点上，触发节点效果（锻造、HP变化等），只是不再重复进入房间事件。

## DelayEnterRoom (MapManager.cs:144)
```
Wait 0.3s (Empty/RoomEvent) or 2.0s (other nodes)

if (roomDataRef != null && !clearedRoomIds.Contains(roomId))
{
    clearedRoomIds.Add(roomId);           // 标记房间已通关
    if (skipRemainingNodesOnClear)        // 使同一房间后续节点失效
        mark same-room nodes isInvalidated
    GameFlowController.EnterRoom(roomDataRef);
}
// 房间已通关 → 不执行任何操作
```

**重要**：房间在 `EnterRoom` 之前就被加入 `clearedRoomIds`。一旦通关，再落在同一房间的任何节点上都不会重复进入房间事件。但节点的自身效果（HP变化、锻造等）仍会触发。

## Room Type Routing (GameFlowController.EnterRoom)
- `BattleRoomSO` → `BattleState` (hides map, starts battle)
- `EventRoomSO` → `EventState` (hides map, shows random event)
- Other types → currently fall through to MapState

## State Machine (GameFlowController)

### All States (IGameState: Enter/Exit/OnSlotClicked)
| State | Enter | Exit | → Next |
|---|---|---|---|
| `MapState` | Show map, hide room UI | — | (waits for dice roll) |
| `BattleState` | Hide map, start battle | — | Victory→Draft→TargetSelect→Map / Defeat→Summary |
| `EventState` | Hide map, show event UI | Hide event UI | callback→MapState |
| `ForgeState` | Hide map, show forge UI | Hide forge UI | callback→DelayEnterRoom or MapState |
| `SpellDraftState` | Show draft panel | Hide draft | callback→TargetSelectionState |
| `TargetSelectionState` | Show selection tip | — | callback→MapState |

### Complete State Flow
```
MapState → [dice roll → land on node]
  ├─ Forge node → ForgeState → (room cleared?) → MapState
  │                          └─ (not cleared) → DelayEnterRoom → enter room
  └─ Other node → DelayEnterRoom → enter room
                                      │
                    ┌─────────────────┘
                    ▼
              BattleRoomSO → BattleState
                    │  ├─ Victory → SpellDraftState → TargetSelectionState → MapState
                    │  └─ Defeat → RunSummary
                    │
              EventRoomSO → EventState → MapState
```

## Key Files
| File | Purpose |
|---|---|
| `Scripts/Map_New/MapManager.cs` | Singleton, board generation, OnPlayerLanded, clearedRoomIds, DelayEnterRoom |
| `Scripts/Map_New/BoardNode.cs` | Runtime node data class |
| `Scripts/Map_New/MapViewController.cs` | UI rendering, scroll, camera follow, node states |
| `Scripts/Map_New/MapInteractionManager.cs` | Dice roll, pawn spawn, movement orchestration |
| `Scripts/Map_New/MapPlayerPawn.cs` | DOTween jump animation |
| `Scripts/Map_New/MapNodeAnchor.cs` | Per-node MonoBehaviour: icons, tooltips, state colors |
| `Scripts/Map_New/MapRegionLayout.cs` | Design-time region layout |
| `Scripts/Map_New/MapRoomLayout.cs` | Design-time room layout |
| `Scripts/Map_New/SO/BoardMapConfigSO.cs` | Top-level board config SO |
| `Scripts/Map_New/SO/RoomDataSO.cs` | Abstract room data (skipRemainingNodesOnClear) |
| `Scripts/Map_New/SO/BattleRoomSO.cs` | Battle room data |
| `Scripts/Map_New/SO/EventRoomSO.cs` | Event room data |
| `Scripts/GameManager/GameFlowController.cs` | State machine, room routing |
| `Scripts/GameManager/MapState.cs` | Map UI state |
| `Scripts/GameManager/BattleState.cs` | Battle state |
| `Scripts/GameManager/EventState.cs` | Event state |
| `Scripts/GameManager/ForgeState.cs` | Forge state |
| `Scripts/GameManager/SpellDraftState.cs` | Spell draft state |
| `Scripts/GameManager/TargetSelectionState.cs` | Target selection state |
| `Scripts/Enum.cs` | BoardNodeType, RoomType enums |
