---
name: forge-meditation-refactor-2026-07-03
description: Forge meditation UI module split, removed old compatibility fields, bag pagination, and constellation overlay fix
type: project
originSessionId: 2026-07-03-forge-meditation-refactor
---
# Forge Meditation Refactor — 2026-07-03

## User Intent

The user wanted the Forge/Meditation UI cleaned up because `ForgeUIManager` had become a large mixed-responsibility class with too many Inspector fields, old compatibility paths, and animation parameters mixed into core configuration.

Durable preference reinforced during this work:

- Do not keep old compatibility paths unless explicitly requested.
- If a prefab or scene breaks because it still uses the old shape, migrate it to the current shape instead of keeping both paths.
- Prefer clear module ownership over a large manager with many loosely related serialized fields.
- Animation tweak parameters should not clutter the main manager Inspector when they are not core authoring configuration.

## Current Module Shape

### `ForgeUIManager`

Main orchestration only:

- show/hide Forge panel
- selected dice state
- meditate confirm
- hold-to-commit flow
- status text
- coordination between submodules

It should not own material slot internals, dice preview internals, inspiration layout, or constellation rendering internals.

### `ForgeDiceSelectionPanel`

Owns:

- available dice filtering
- previous/next navigation
- current dice/spell icon preview
- tooltip setup
- icon switch animation
- spell-icon breath

Removed old list-mode fields:

- `diceSelectContainer`
- `diceSelectButtonPrefab`

The current Forge UI uses carousel-style dice selection only.

### `ForgeMaterialInputPanel`

Owns:

- material slots
- bag popup
- continuous fill
- replacement/refund
- inventory consume/refund
- bag pagination
- material slot animations

Removed old fallback field:

- `fallbackResourceContainer`

The bag requires one explicit `bagItemContainer`.

Bag behavior:

- page size is 9
- each page always creates 9 grid cells
- resource cells show background, icon, and count
- empty cells keep the background frame but hide icon/count and are not clickable
- previous/next buttons are hidden unless resources span multiple pages
- bag visibility callback is exposed so foreground bag popup can suppress background constellation lines

### `ForgeInspirationPanel`

Owns:

- generated/committed inspiration node rendering
- position allocation
- collision push layout
- option appear animation
- idle floating
- dimmed old unchosen inspirations
- option lookup

It provides `FindFreeOptionIndex` so `ForgeUIManager` no longer calculates inspiration positions.

### `ForgeConstellationRenderer`

Owns:

- UI fallback constellation lines
- world-space glowing lines
- nodes
- particles
- live follow
- hold-line progress
- temporary visibility

It provides `SetVisible(bool)`.

When the material bag opens, `ForgeUIManager` hides constellation lines/nodes to prevent world-space constellation effects from rendering above the bag. When the bag closes, constellation visibility is restored.

## Configuration Expectations

`ForgeUIManager` should reference:

- `diceSelection`
- `materialInput`
- `inspirationPanel`
- `constellationRenderer`

`ForgeDiceSelectionPanel` should reference:

- previous/next buttons
- center spell icon image
- current dice icon image

`ForgeMaterialInputPanel` should reference:

- material slot bar/toggle
- bag panel
- bag close button
- three material slot buttons
- `bagItemContainer`
- resource button prefab
- optional bag previous/next buttons

`ForgeInspirationPanel` should reference:

- options container
- option button prefab
- option placement center
- option offsets/layout config

`ForgeConstellationRenderer` should own the constellation line visual parameters and world-rendering references.

## Important Files

- `Assets/Scripts/UI/ForgeUIManager.cs`
- `Assets/Scripts/UI/ForgeDiceSelectionPanel.cs`
- `Assets/Scripts/UI/ForgeMaterialInputPanel.cs`
- `Assets/Scripts/UI/ForgeInspirationPanel.cs`
- `Assets/Scripts/UI/ForgeConstellationRenderer.cs`
- `Assets/Scripts/UI/ForgeResourceButton.cs`

## Verification

Unity MCP script refresh and compilation completed with no C# errors after:

- module split
- deletion of old compatibility fields
- bag 9-cell pagination
- constellation overlay visibility fix

