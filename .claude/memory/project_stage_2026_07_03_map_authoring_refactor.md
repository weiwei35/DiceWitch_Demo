---
name: map-authoring-refactor-2026-07-03
description: Room-first map authoring refactor, presentation catalog, StartRoomSO, validation tool, and designer-facing map creation guide
type: project
originSessionId: 2026-07-03-map-authoring-refactor
---
# Map Authoring Refactor — 2026-07-03

## User Intent

The user wanted the map system easier to author after branching support was added. Their clarified model:

- A `Region` is a chapter/act and should normally end with a Boss room.
- A `Room` should be the main standalone authoring unit.
- A chapter should be assembled by placing/configuring rooms in Scene.
- Node positions must remain Scene-adjustable to match map background art.
- The node's main icon should come from the room type, not be configured per node.
- Forge nodes always have an extra node effect.

The user is willing to reconfigure maps for a cleaner model and does not want long-term old compatibility paths.

## Implemented Direction

### Room-first authoring

`MapRoomLayout` remains the main authoring component:

- `roomData`
- `roomNodes`
- `nextRooms`

`MapNodeAnchor` is now only the landing/effect point:

- `nodeType`
- `effectValue`
- `forgeBonusType`

Removed from node authoring:

- `baseIconSprite`
- `iconConfig`
- `hidePlusSignForPositiveValue`

### Presentation catalog

`MapIconConfigSO` was removed.

`MapPresentationCatalogSO` was added as the single map presentation source:

- room icons and room display names
- node effect icons
- node value display rules
- tooltip templates
- floating text templates/colors
- node state colors
- forge icon

`BoardMapConfigSO` now references one `presentationCatalog`.

Important helper behavior:

- `Fill Missing Default Entries` must only add missing entries or fill blank text fields.
- It must not clear or overwrite already configured icons, text, or options.

### Start room

`GameEnums.RoomType.Start` and `StartRoomSO` were added.

Use `StartRoomSO` for the map starting room. Do not create a `BattleRoomSO` and manually change the type; room type is now fixed by the concrete SO class.

`RoomDataSO.roomType` is hidden in Inspector and fixed by subclasses:

- `StartRoomSO` -> Start
- `BattleRoomSO` -> Battle
- `EventRoomSO` -> Event

`GameFlowController.EnterRoom` handles Start by returning to `MapState`.

### Validation and docs

Added editor menu:

```text
DiceWitch/Map/Validate Selected Map Config
```

Select a `BoardMapConfigSO` and run it to check map authoring problems.

Added designer-facing guide:

```text
制作文档/地图新建手册.md
```

This guide explains how art/design can create a chapter from scratch.

## Configuration Expectations

To build a new chapter:

1. Create/fill `MapPresentationCatalogSO`.
2. Create room SO assets: Start, Battle, Event, Boss etc.
3. Build room objects with `MapRoomLayout`.
4. Place nodes in each room and configure only node effect fields.
5. Assemble rooms in `MapRegionLayout.orderedRooms`.
6. Configure branches through `MapRoomLayout.nextRooms`.
7. Assign the region prefab and presentation catalog in `BoardMapConfigSO`.
8. Run the map validation menu.

## Important Files

- `Assets/Scripts/Map/MapPresentationCatalogSO.cs`
- `Assets/Scripts/Map/SO/BoardMapConfigSO.cs`
- `Assets/Scripts/Map/MapNodeAnchor.cs`
- `Assets/Scripts/Map/MapManager.cs`
- `Assets/Scripts/Map/MapViewController.cs`
- `Assets/Scripts/Map/SO/RoomDataSO.cs`
- `Assets/Scripts/Map/SO/StartRoomSO.cs`
- `Assets/Scripts/Editor/MapConfigurationValidator.cs`
- `制作文档/地图新建手册.md`

## Pitfalls

- Do not manually edit `roomType`; it is hidden and controlled by concrete room SO classes.
- Do not use `BattleRoomSO` as a start room.
- Do not reintroduce `MapIconConfigSO` or per-node base icons.
- Do not make `Fill Missing Default Entries` destructive.
- Keep room metadata injection in `MapViewController.DrawMap()` through `SetPresentationContext`, or tooltips and room icons will lose room context.
