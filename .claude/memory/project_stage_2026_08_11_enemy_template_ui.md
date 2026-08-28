# Enemy Template UI Stage 2026-08-11

## Final presentation contract

- All existing enemy prefabs use the same presentation hierarchy under the world-space `EnemyUI` canvas.
- Above the enemy, `TopRow` is centered as one group. `StatusPanel` is always first, followed by `AttackIntent`.
- Attack intent is a configurable icon plus the final attack number. If no icon is assigned, the icon is hidden and the number remains visible, including attack value `0`.
- Below the enemy, `HealthBar` contains the decorative frame, smooth horizontal fill, enemy name, and `current/max` health value.
- The name uses `BattleTarget.targetName`; when it is blank, the runtime object name without `(Clone)` is used.
- Health fill changes tween over `0.2` seconds with `Ease.OutQuad`; initial setup is immediate.
- UI vertical positions are authored per prefab from the visible `SpriteRenderer` bounds so large monsters and the Boss do not overlap the top row or health bar.

## Shared art and prefab scope

- Frame: `Assets/Art/战斗界面/血条底框.png`
- Fill: `Assets/Art/战斗界面/血条填充.png`
- Status item prefab remains `Assets/Prefab/Battle/StatesUI.prefab` so existing status tooltips and stack numbers are preserved.
- Migrated prefabs: `Monster_001` through `Monster_006`, plus `Boss`.

## Code owner

- `Assets/Scripts/Enemy/EnemyTarget.cs` owns name, health text/fill, attack intent, status creation, damage, and healing updates.
- Per-monster attack icon is assigned through `EnemyTarget.attackIntentIcon` in the prefab Inspector.

## Verification

- Unity compilation completed with zero errors and zero warnings.
- All 7 prefabs passed hierarchy, reference, child-order, art, retained-stat, and old-UI-removal checks.
- Runtime check on `Monster_001`: `20/20 -> 15/20 -> 20/20`; fill tween completed `1.00 -> 0.75 -> 1.00`.
