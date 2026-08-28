---
description: Reusable UI ambient motion, mouse parallax migration, and Animator exclusions
---

# Reusable UI Ambient Motion — 2026-08-10

## Scope

- Current battle, map, Forge, and start backgrounds are UI `Image` objects with `RectTransform`, even though their source assets are imported Sprites.
- World-space `SpriteRenderer` support is intentionally not implemented until a real scene needs it.
- Objects already animated by an Animator are not added to code-driven idle motion.

## Shared components

- `MouseParallaxUI` is the only mouse-parallax implementation. It uses bounded, smoothed, reverse parallax and scaled `Time.deltaTime`.
- `UIAmbientMotion` is opt-in per `RectTransform` and supports:
  - symmetric breath around the authored scale;
  - directional movement within `±movementRadius`;
  - `SafeOverflow` movement calculated from image/parent overflow so large layers do not expose blank edges.
- `UIAmbientParticles` is the reusable UI flying-dust/stardust/firefly component. Configure its spawn area, optional container/sprites, count, color/glow, lifetime, drift, sway, and rotation directly in the Inspector.
- The former `StartPanelAmbientParticles` script was renamed in place with its Unity GUID preserved, so the StartPanel scene reference and serialized settings remain intact.
- Generated fallback dot/glow sprites are shared by all `UIAmbientParticles` instances, and their motion uses scaled time so particles pause with the game.
- Movement and breath can run together. Both restore the authored position/scale when disabled and pause when `Time.timeScale == 0`.

## Migrations and scene setup

- Deleted `ParallaxBG`; `Canvas_Global/StartPanel/BG` now owns one `MouseParallaxUI`.
- StartPanel parallax includes only `bg (3)` and `bg (7)`, preserving the former 0.1/0.3 depth ratio. `bg (4)` is excluded because `startPanel_idle` animates its anchored position.
- `Canvas_Main/Battle/BG_default` and `BG_first` each own a conservative `MouseParallaxUI` with `16×8` maximum movement.
- Forge keeps its existing `MouseParallaxUI`.
- Deleted `MapFogDrift`; the upper/lower map Fog children each own `UIAmbientMotion/SafeOverflow`, with 350s/360s durations and opposite movement.

## Deliberate exclusions

- `MapDiceHoverBreath`, `StarImageIdleEffect`, and `ForgeUIEffects` remain specialized because they contain interaction or business-specific behavior.
- `ForgeConstellationEffect` particles remain specialized because they emit along revealed constellation paths rather than acting as screen-wide idle dust.
- Do not place `MouseParallaxUI` and `UIAmbientMotion` on the same `RectTransform`; use a parent for parallax and a child for idle movement if both are needed.
