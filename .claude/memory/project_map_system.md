---
name: map-system
description: Complete map/board system — board generation, node types, movement, landing flow, room clearing, state machine transitions
type: project
originSessionId: 8632ebdf-3b91-4cd5-91fb-5cd351530b29
---
# Map/Board System

## Architecture Overview

The map is a board of connected nodes grouped into **rooms**. It began as a linear board, but now supports branching **between rooms**. Branches are not configured inside a room; they happen when leaving a room and choosing among multiple next rooms.

**Current authoring concept**: map configuration is now **room-first**. A `Region` is a chapter/act, a `Room` is the main authoring unit, and `Node` objects are landing points inside a room. Room type controls the main room icon; node type controls the local node effect.

**Key runtime concept**: A "room" is a group of consecutive nodes sharing the same `roomId`. Entering happens when landing on ANY node in an uncleared room.

## 2026-07-03 Map Authoring Refactor

The map authoring model was cleaned up so art/design can build a chapter from independent rooms:

- `Region` means a chapter/act. It is expected to end with a Boss room.
- `MapRoomLayout` is the primary authoring unit. Configure `roomData`, `roomNodes`, and optional `nextRooms`.
- `MapNodeAnchor` no longer owns the room/base icon. Node main icons are derived from `roomData.roomType`.
- Node local effect configuration remains on `MapNodeAnchor`: `nodeType`, `effectValue`, and `forgeBonusType`.
- `MapNodeAnchor.frameImage` is an explicit prefab reference. Its placement, size, and hierarchy are authored in the node prefab; its sprite and visibility still come from the room-type entry in `MapPresentationCatalogSO`.
- Forge nodes are intentionally modeled as `锻造 + bonus effect`; `forgeBonusType` is required for forge nodes.
- `MapIconConfigSO` was removed. Use `MapPresentationCatalogSO` instead.
- `BoardMapConfigSO` now has one shared `presentationCatalog` reference for the whole map.
- `BoardMapConfigSO.nodePassedBackgroundFolder` indexes all reached-node background Sprites for the map. Nodes reference them by exact Sprite name through `MapNodeAnchor.passedBackgroundSpriteName`.
- `MapPresentationCatalogSO` owns room icons, node effect icons, tooltip text templates, floating text templates/colors, and node state colors.
- `Fill Missing Default Entries` only adds missing catalog entries and fills blank text fields. It must not clear or overwrite configured icons/text/options.
- `RoomDataSO.roomType` is hidden in Inspector and fixed by the concrete SO class to avoid false manual edits.
- Use `StartRoomSO` for map start rooms. Do not use `BattleRoomSO` and manually set it to Start; battle rooms force their type back to Battle.
- `GameEnums.RoomType.Start` exists for map start rooms. If entered accidentally, `GameFlowController.EnterRoom` returns to MapState.
- A map validation menu exists: select `BoardMapConfigSO`, then run `DiceWitch/Map/Validate Selected Map Config`.
- The user-facing setup document is `制作文档/地图新建手册.md`.

## Board Setup

### ScriptableObject hierarchy (design-time, Inspector-configured)
```
BoardMapConfigSO
  ├─ presentationCatalog (MapPresentationCatalogSO)
  ├─ nodePassedBackgroundFolder + indexed Sprite list
  └─ List<BoardRegionConfig> regions
       └─ regionName, regionPrefab (with MapRegionLayout)
            └─ MapRegionLayout
                 └─ List<MapRoomLayout> orderedRooms
                      └─ roomData (RoomDataSO), List<MapNodeAnchor> roomNodes, List<MapRoomLayout> nextRooms
                           └─ MapNodeAnchor: nodeType, effectValue, forgeBonusType, frameImage
```

### Runtime generation (`MapManager.GenerateBoard()`)
Iterates all regions → rooms → anchors, creates flat `List<BoardNode> boardNodes` with global `index`, `roomId`, `regionIndex`, `type`, `effectValue`, `forgeBonusType`, `roomDataRef`.

Also creates `List<BoardRoom> boardRooms`. `BoardRoom` stores room id, region index, room data, start/end node indices, and `nextRoomIds`.

### Branching room configuration

Current configured map prefab:
- `Assets/Prefab/Map/MapBG_new.prefab`

`MapRegionLayout.orderedRooms` still defines the full room set and display order. `MapRoomLayout.nextRooms` defines where a room can go after its last node.

Rules:
- Leave `nextRooms` empty for old linear behavior. The room auto-connects to the next entry in `orderedRooms`.
- Put 1 target room in `nextRooms` for an explicit single successor.
- Put 2+ target rooms in `nextRooms` for a branch choice.
- Target rooms must also be present in `orderedRooms`, because `orderedRooms` is still used to instantiate and map node UI.

Example:
```
A.nextRooms = [B, C]
B.nextRooms = [D]
C.nextRooms = [D]
```

The player does not choose early. Movement remains automatic until the pawn reaches a room exit while the dice roll still has remaining steps. Then the map pauses and shows simple branch buttons to the right of the current node. After choosing, movement continues consuming the remaining steps.

If the roll lands exactly on the branch room's final node with no remaining steps, no branch choice opens yet; that node resolves normally.

### Scene reference lines

`MapRegionLayout.OnDrawGizmos()` draws green editor reference lines using the same room graph logic:
- Room internal lines connect each room's `roomNodes` in order.
- Room exit lines connect the room's last node to each configured `nextRooms` target's first node.
- Empty `nextRooms` falls back to the next `orderedRooms` room.

This means Scene-view reference lines preview the same branch structure used at runtime.

### Runtime route art

Runtime route connections are authored artwork, not generated UI:

- Route images are placed directly in the region prefab: the full unvisited route stays visible underneath, while the visited route is revealed above it through `MapGridRevealLayer`.
- The passed-route overlay uses a vertical cutoff that follows the pawn every frame: every route pixel left of the pawn X is shown as passed. It does not use node-centered circles.
- `MapViewController` preserves those authored objects inside the node-layer copy and does not create a `RoutePathsContainer` or procedural route dots.
- `MapRoutePathRenderer` and the old `showRouteLines` / route-dot settings were removed.
- The green `MapRegionLayout.OnDrawGizmos()` lines remain editor-only graph helpers and are not runtime route art.

Node UI state, reached-node backgrounds, and circular passed-grid reveal use `MapManager.VisitedNodeIndices`. Invalidated/rejected branch nodes are excluded from those systems. Route color is intentionally simpler: it is determined only by whether a route pixel is left or right of the pawn X.

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

## BoardRoom Fields
```csharp
int roomId;
int regionIndex;
RoomDataSO roomDataRef;
int startNodeIndex;
int endNodeIndex;
List<int> nextRoomIds;
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

## Room Type Authoring

Room type is not manually edited in Inspector. It is fixed by the concrete room SO class:

- `StartRoomSO` -> `RoomType.Start`
- `BattleRoomSO` -> `RoomType.Battle`
- `EventRoomSO` -> `RoomType.Event`

If more room data classes are added, they should override the fixed room type rather than asking designers to edit `roomType` by hand.

The map start should be authored as a normal room with `StartRoomSO` and usually one empty node:

```text
RoomData = StartRoomSO
Node Type = 空
Effect Value = 0
```

Do not create a battle room and attempt to set it to Start; `BattleRoomSO` will always restore its fixed room type to Battle.

## 核心规则：经过节点不触发

**只有最终落点触发 `OnPlayerLanded`，棋子跳跃经过的中间节点一律不生效。**

举例：房间有节点1(HP+3)、节点2(HP-3)、节点3(秘点+10)。投出2步，棋子从起点跳过节点1，落在节点2——只有节点2 的 HP-3 生效，节点1 的 HP+3 不触发。

这是由移动路径计算决定的：`MapInteractionManager.HandleDiceResult` 直接计算 `targetIndex`，不存在「每经过一个节点就触发一次」的逻辑。所有节点的效果、buff、debuff 都只在落点生效。

## Player Movement Flow

1. **Click "Roll Dice"** → `MapInteractionManager.OnRollDiceClicked()` spawns physics dice, disables button
2. **Dice settles** → `HandleDiceResult(steps)` starts step-by-step movement:
   - 从 `MapManager.currentPlayerNodeIndex` 出发
   - 每一步调用 `MapManager.TryGetNextNode(currentIndex, out nextIndex, out branchChoices)`
   - 房间内部默认走到下一个节点
   - 房间出口按 `BoardRoom.nextRoomIds` 选择下一房间首节点
   - 如果出口有多个后继房间，且本次投骰仍有剩余步数，暂停并显示分岔选择按钮
   - **跳跃逻辑**：如果当前房间 `skipRemainingNodesOnClear == true` 且已通关，下一步直接离开当前房间，按房间出口图进入后继房间
3. **Pawn animates** → `MapPlayerPawn.MoveAlongPath()` DOTween jump-bounce 每步跳跃
   - 每抵达一个节点，`MapManager.MarkNodeVisited(index)` 记录纯视觉访问状态；这不触发节点效果。
4. **On complete** → update node visuals, call `MapManager.OnPlayerLanded(landedNode)`

Important invariant: branch choice only changes which room path to follow; only the final landing node triggers node effects.

### Visited-node visual state

- Node state is no longer inferred from `nodeIndex < currentIndex`.
- `Current` means the current node, `Passed` means a node in `VisitedNodeIndices`, `Disabled` means `isInvalidated`, and every other node is `Future`.
- The initial node is marked visited during `GenerateBoard()`.
- `MapState.Enter()` refreshes map node visuals so invalidation performed before entering a battle/event is visible immediately when returning to the map.
- Invalidated nodes never reveal node backgrounds or the circular grid texture. The route overlay is independent of node validity and shows every route pixel left of the pawn as passed.

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

## Map Node Tooltip Room Info

`MapNodeAnchor` tooltips now show both node effect and room information.

Implementation:
- `MapViewController.DrawMap()` assigns each instantiated `MapNodeAnchor` its runtime `BoardNode.roomDataRef`, map presentation catalog, and the Sprite resolved from `passedBackgroundSpriteName` using `anchor.SetPresentationContext(...)`.
- `MapNodeAnchor.GetTooltipInfo()` uses `MapPresentationCatalogSO` for node text and appends:
  - `房间: {roomName}`
  - `类别: {roomType display name from catalog}`
- Disabled/invalidated route nodes still show room info after the disabled text.

This lets the player inspect not only HP/resource/forge effects, but also which room and room category the node belongs to.

## Map Presentation Catalog

`MapPresentationCatalogSO` is the single source of truth for map presentation:

- Room type icon and display name.
- Optional room-type frame sprite. A null frame sprite hides the prefab's `frameImage`; a configured sprite assigns and shows it.
- Node effect icon, including positive/negative/neutral variants.
- Node value display rules, including whether positive values show `+`.
- Node tooltip text templates.
- Floating text template and color.
- Node state colors.
- Forge icon.

Template variables:

- `{value}` = raw value
- `{abs}` = absolute value
- `{signed}` = positive value with `+`, negative value as-is

Important: `Fill Missing Default Entries` must be treated as a safe helper. It should not erase existing configured art or text.

The catalog controls which frame sprite is used, but it does not control frame layout. Every node prefab must bind `MapNodeAnchor.frameImage`; the prefab owns its RectTransform, sibling order, Image settings, and resulting style. Runtime code must not auto-find or auto-create `RoomFrame`.

## Reached-node backgrounds

- The map config indexes Sprites from one authored folder in Editor; runtime never uses `AssetDatabase` or `Resources.Load`.
- Use `Refresh Node Passed Backgrounds From Folder` on `BoardMapConfigSO` after adding or renaming files.
- Each non-start `MapNodeAnchor` stores the exact Sprite name without `.png`.
- `Room_Map.prefab` owns `passedBackgroundImage`; it is centered behind node UI and remains fixed at `180×180` when its Sprite changes.
- The background appears for `Current` and `Passed`, and stays hidden for `Future` and `Disabled`. A blank name is allowed for the start node.

## Map ambience and dice hover

- The two authored Fog images each use `UIAmbientMotion` in `SafeOverflow` mode. They move horizontally in opposite directions, calculate safe endpoints from image overflow, ease at reversals, and pause with `Time.timeScale` or while inactive.
- `MapDiceHoverBreath` checks the actual 3D dice collider through `DiceViewMonitor`; entering empty RawImage space does not trigger the animation.
- The map RawImage breathes by 4% while the real map dice is hovered and restores its authored base scale on exit/disable.

## Map Configuration Validation

Editor menu:

```text
DiceWitch/Map/Validate Selected Map Config
```

Usage:

1. Select a `BoardMapConfigSO` in Project.
2. Run the menu.
3. Read Console output.

Checks include:

- Missing `presentationCatalog`.
- Missing region prefab.
- Region prefab missing `MapRegionLayout`.
- Empty `orderedRooms`.
- Missing `roomData`.
- Room with no nodes.
- Forge node missing valid `forgeBonusType`.
- Node UI reference incomplete.
- Chapter last room is not Boss.
- Catalog missing icon/effect entries used by the map.

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
| `Scripts/Map/MapManager.cs` | Singleton, board generation, OnPlayerLanded, clearedRoomIds, DelayEnterRoom |
| `Scripts/Map/BoardRoom.cs` | Runtime room graph data for branching map paths |
| `Scripts/Map/BoardNode.cs` | Runtime node data class |
| `Scripts/Map/MapViewController.cs` | UI rendering, scroll, camera follow, node states |
| `Scripts/Map/MapInteractionManager.cs` | Dice roll, pawn spawn, movement orchestration |
| `Scripts/Map/MapPlayerPawn.cs` | DOTween jump animation |
| `Scripts/Map/MapNodeAnchor.cs` | Per-node MonoBehaviour: icons, tooltips, state colors |
| `Scripts/UI/Effects/UIAmbientMotion.cs` | Reusable UI breath, radius movement, and safe-overflow drift; used by both map Fog images |
| `Scripts/Map/MapDiceHoverBreath.cs` | Exact projected-dice hover detection and RawImage breathing |
| `Scripts/Map/MapPresentationCatalogSO.cs` | Shared room/node presentation catalog for icons, tooltip templates, floating text, state colors |
| `Scripts/Map/MapRegionLayout.cs` | Design-time region layout |
| `Scripts/Map/MapRoomLayout.cs` | Design-time room layout |
| `Scripts/Map/SO/BoardMapConfigSO.cs` | Top-level board config SO |
| `Scripts/Map/SO/RoomDataSO.cs` | Abstract room data (skipRemainingNodesOnClear) |
| `Scripts/Map/SO/StartRoomSO.cs` | Start room data |
| `Scripts/Map/SO/BattleRoomSO.cs` | Battle room data |
| `Scripts/Map/SO/EventRoomSO.cs` | Event room data |
| `Scripts/Editor/MapConfigurationValidator.cs` | Editor validation menu for map authoring |
| `Scripts/GameFlow/GameFlowController.cs` | State machine, room routing |
| `Scripts/GameFlow/MapState.cs` | Map UI state |
| `制作文档/地图新建手册.md` | Art/design friendly guide for creating a new chapter map |
| `Scripts/GameFlow/BattleState.cs` | Battle state |
| `Scripts/GameFlow/EventState.cs` | Event state |
| `Scripts/GameFlow/ForgeState.cs` | Forge state |
| `Scripts/GameFlow/SpellDraftState.cs` | Spell draft state |
| `Scripts/GameFlow/TargetSelectionState.cs` | Target selection state |
| `Scripts/Core/Enum.cs` | BoardNodeType, RoomType enums |
