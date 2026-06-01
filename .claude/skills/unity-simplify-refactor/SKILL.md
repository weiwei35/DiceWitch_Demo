---
name: unity-simplify-refactor
description: Simplify and refactor Unity C# project code while preserving gameplay semantics, prefab/Inspector contracts, scene-driven references, and existing user workflows. Use when the user asks for code cleanup, code optimization, simplification, reducing duplication, removing unnecessary auto-find/fallback logic, clarifying manager boundaries, or making a Unity implementation easier to maintain without changing behavior.
---

# Unity Simplify Refactor

## Purpose

Use this skill to make Unity code smaller, clearer, and safer without redesigning the game. Prefer behavior-preserving cleanup over clever abstractions.

## Ground Rules

- Read the local code and project memory before editing.
- Preserve player-visible behavior unless the user explicitly asks to change it.
- Preserve Unity Inspector and prefab contracts. If a serialized field is removed, renamed, or repurposed, call that out clearly.
- Prefer explicit Inspector references when the scene relationship is stable and visible. Avoid adding auto-find fallbacks for the same reference unless there is a clear recovery reason.
- Do not introduce broad managers, base classes, or interfaces just to reduce a few lines.
- Keep edits near the feature being simplified.
- Avoid mixing refactor with new gameplay behavior. If a bug fix is needed, identify it separately.

## Simplification Workflow

1. Map responsibilities.
   Identify what each class owns: data, UI rendering, input, animation, persistence, or gameplay resolution.

2. Locate duplication and ambiguity.
   Look for repeated refresh methods, duplicate serialized references to the same UI object, duplicated tooltip builders, parallel data lists, repeated material consume/refund logic, or methods whose names no longer match behavior.

3. Choose the smallest cleanup.
   Prefer extracting a small method, deleting dead fallback code, consolidating one update path, or renaming a misleading field over inventing a new abstraction.

4. Protect Unity serialization.
   If a serialized field changes, consider whether existing scene/prefab references will break. When possible, keep field names stable or use `[FormerlySerializedAs]` for renamed fields.

5. Keep UI state and game state separate.
   UI animations, hover effects, tooltips, and line rendering should not be the source of truth for inventory, dice affixes, battle state, or staged materials.

6. Verify with static checks appropriate to the change.
   Prefer `rg`, targeted file reads, compile-aware reasoning, and small diffs. Do not claim Unity runtime verification unless actually run.

## DiceWitch Preferences

- The user values simple, inspectable Unity setup over hidden magic.
- The user dislikes configuring the same relationship in two places.
- The user is comfortable wiring explicit references in Unity Inspector.
- The user prefers designing interaction flow before implementing complex UI changes.
- The user often validates Unity behavior manually, so provide clear "what changed" and "where to configure" notes.

## Common Refactor Targets

- Merge duplicated icon or tooltip refresh paths when battle, forge, and dice display must show the same affix data.
- Centralize material staging, replacement, refund, and consume logic so resources cannot be lost during phase changes.
- Replace scattered animation calls with small named UI helper methods while keeping gameplay data outside animation code.
- Remove stale fallback/reference discovery code after explicit Inspector references are established.
- Split oversized methods by phase: collect input, mutate model, refresh UI, play feedback.

## Output Shape

When reviewing, lead with concrete simplification opportunities and risks. When editing, summarize:

- What behavior was preserved.
- What code path was simplified.
- What duplicate or ambiguous configuration was removed.
- Whether any Unity Inspector references need to be checked.

If the safest simplification would change behavior, stop and explain the tradeoff before editing.
