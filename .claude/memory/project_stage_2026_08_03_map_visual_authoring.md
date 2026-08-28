---
name: map-visual-authoring-2026-08-03
description: Explicit prefab node frames, named passed backgrounds, route progress, dice hover, and fog drift
type: project
originSessionId: 2026-08-03-map-visual-authoring
---
# Map Visual Authoring — 2026-08-03

## User intent

- Node borders must be positioned and styled in the node prefab instead of being auto-created at runtime.
- Whether a border is visible and which border sprite it uses must continue to come from the room-type presentation configuration.
- Runtime procedural dotted connections must be removed because route artwork is now drawn and placed by hand.
- Scene-view green graph Gizmos should remain as editor helpers.
- Reached nodes show individually named background Sprites loaded from one map folder.
- Visited and unvisited full-map route images display simultaneously through a cumulative reveal mask.
- Rejected branches must remain visually unvisited, including the existing circular grid reveal.
- The projected map dice responds only when the actual 3D dice is hovered.
- Upper and lower map fog drift slowly in opposite directions and reverse at safe image bounds.

## Implemented contract

### Node frame

- `MapNodeAnchor` exposes `public Image frameImage`.
- `Room_Map.prefab` owns and binds a `RoomFrame` Image. Its initial RectTransform matches the main room icon and can be edited freely in the prefab.
- `MapNodeAnchor.UpdateVisuals()` reads `MapPresentationCatalogSO.GetRoomFrameSprite(roomData)`:
  - non-null sprite: assign and show `frameImage`;
  - null sprite: clear and hide `frameImage`.
- Runtime auto-find and auto-create behavior for `RoomFrame` was removed.
- Map configuration validation reports an error when `frameImage` or another required node UI reference is missing.

### Route artwork

- `MapViewController` no longer exposes procedural route-dot settings or creates runtime route objects.
- `DrawRouteLines`, bounds helpers, and `MapRoutePathRenderer` were removed.
- Independent route artwork such as `Assets/Prefab/Map/Line.prefab` remains in the authored region hierarchy. Its Image has raycast disabled.
- `MapRegionLayout.OnDrawGizmos()` remains unchanged and only previews graph connections in the Scene view.

### Unified visited state

- `MapManager` owns `VisitedNodeIndices`, initialized with the start node and updated after every animated movement step.
- Node UI state, node background visibility, and passed-grid circles consume this same set. Passed-route masking instead follows the pawn X position every frame.
- `isInvalidated` always wins over visited state. Rejected branches remain unvisited and reveal nothing.
- `MapState.Enter()` refreshes node visuals so room-clear invalidation is applied immediately after returning from battle/event UI.
- Intermediate visited nodes are visual-only; the existing invariant remains that only the final landing node triggers gameplay effects.

### Named node backgrounds

- `BoardMapConfigSO` stores one folder reference and a serialized Sprite index refreshed in Editor.
- `MapNodeAnchor.passedBackgroundSpriteName` stores the exact Sprite name without extension.
- `Room_Map.prefab` binds a centered `PassedBackground` Image behind the node UI and keeps it fixed at `180×180`; assigning a Sprite must not call `SetNativeSize()`.
- The current map indexes 40 Sprites from `Assets/Art/地图2/地图已经过`; all 40 non-start nodes are configured, and the start node intentionally has a blank name.

### Route progress images

- `路线（未经过）` is the bottom, always-visible image.
- `路线（已经过）` is the top image controlled by its own `MapGridRevealLayer` on the `Line` root.
- Route progress uses a vertical cutoff at the pawn X, with its own small feather. The old passed-grid texture keeps the visited-node circle radius/feather behavior.

### Ambient feedback

- Each child of `Canvas_Map/MapUIPanel/Fog` uses `UIAmbientMotion` in `SafeOverflow` mode. The two images retain their authored 350s/360s one-way durations and opposite directions.
- `MapDiceHoverBreath` is bound to the map `UI_DiceView (1)` and `MapInteractionManager`. It pulses by 4% only when collider-accurate dice projection hit testing succeeds.

## Key files

- `Assets/Scripts/Map/MapNodeAnchor.cs`
- `Assets/Scripts/Map/MapViewController.cs`
- `Assets/Scripts/Map/MapRegionLayout.cs`
- `Assets/Scripts/UI/Effects/UIAmbientMotion.cs`
- `Assets/Scripts/Map/MapDiceHoverBreath.cs`
- `Assets/Scripts/Map/SO/BoardMapConfigSO.cs`
- `Assets/Scripts/Map/MapManager.cs`
- `Assets/Scripts/Map/MapInteractionManager.cs`
- `Assets/Scripts/GameFlow/MapState.cs`
- `Assets/Scripts/Editor/MapConfigurationValidator.cs`
- `Assets/Prefab/Map/Room_Map.prefab`
- `Assets/Prefab/Map/Line.prefab`
- `制作文档/地图新建手册.md`
