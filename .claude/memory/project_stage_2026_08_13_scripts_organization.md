# Scripts Organization Stage 2026-08-13

## Directory contract

`Assets/Scripts` is organized by gameplay feature:

- `Battle` — battle orchestration, targets, damage pipeline and status effects
- `Core` — shared project-wide enums and primitive definitions
- `Dice` — dice runtime, abilities, special dice and dice UI input/view scripts
- `Editor` — Unity Editor-only tools
- `Enemy` — enemy runtime and wave definitions
- `GameFlow` — game states and state controller
- `Magic` — magic-circle and slot systems
- `Map` — map runtime, authoring layouts, room data and map assets
- `Player` — player runtime and battle target
- `Progression` — run tracking, resources, forge and player progression
- `UI` — UI scripts split again by feature

`Assets/Scripts/UI` subfolders:

- `Battle`, `Common`, `Effects`, `Event`, `Forge`, `HUD`, `Rewards`, `Start`, `Transitions`, `WeakGuide`

## Safety contract

- Future script moves must use Unity `AssetDatabase.MoveAsset` or move the script together with its `.meta` file.
- Never regenerate a script `.meta` during a move; scene, prefab and ScriptableObject references depend on its GUID.
- Directory moves do not justify class renames, namespace changes or gameplay refactors.

## Verification baseline

- 126 scripts before and after organization.
- Script name/GUID fingerprint remained `540A6E09E33BFD6CDB5F0889A63D047C4C0CBD01D2667510DA138800656F4116`.
- Unity compilation: zero errors and zero warnings.
- All prefabs: zero missing scripts.
- All 2 project scenes: zero missing scripts.
- Play Mode initialized GameFlow, Battle, Map, Player and Tooltip systems.
- `Assets/Scripts/Map/Data/Battle 1.asset` retained its wave reference and spawned `Monster_001` successfully.

## Not part of this organization

- No classes, namespaces, serialized fields or gameplay logic were changed.
- Existing map validation messages about missing Start-room Current/Future state sprites are content configuration, not lost script references.
