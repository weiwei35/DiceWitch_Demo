---
name: dicewitch-state-flow-audit
description: Audit DiceWitch Unity gameplay state flows for sequencing bugs, stuck states, inventory/resource loss, UI blocking, and cross-system regressions. Use when changing or reviewing map node resolution, forge/meditation, event rooms, battle start/end, dice roll settlement, rewards, returning to map, material consume/refund, or any feature where multiple managers must hand off control safely.
---

# DiceWitch State Flow Audit

## Purpose

Use this skill to review gameplay flow changes as player-visible state transitions, not just isolated code edits. The goal is to catch lost resources, delayed panels, broken handoffs, blocked input, duplicate effects, and states that cannot recover.

## Required Context

Before judging a flow, read the relevant project memory first when available:

- `.claude/memory/MEMORY.md`
- `.claude/memory/project_stage_2026_05_31_forge_dice_ui.md`
- Other linked project memories for map, forge, or dice systems

Then inspect the concrete scripts and prefabs touched by the requested change. Do not rely only on memory.

## Audit Workflow

1. Identify the intended player story in plain language.
   Example: "land on event-forge node -> apply node effect -> open forge -> close forge -> open event."

2. List every system that participates.
   Common systems include `MapManager`, `BattleManager`, `ForgeManager`, `ForgeUIManager`, `DiceController`, `DiceInputManager`, `DiceViewMonitor`, `ProgressionManager`, `UIManager`, event panels, tooltip targets, inventory/resource data, and scene/prefab references.

3. Trace ownership of continuation.
   For each step, name which method starts the next step and what condition gates it. Pay special attention to callbacks, coroutines, panel close handlers, animation completion, and "no reward" or "empty list" branches.

4. Check state mutation timing.
   Verify when HP, inventory, materials, dice affixes, rewards, selected options, temporary roll modifiers, and battle/map states are consumed, refunded, reset, or persisted.

5. Check UI/input blocking.
   Look for disabled buttons that never re-enable, panels that intercept raycasts while visually hidden, duplicate `UI_DiceView` references, tooltip objects with raycast targets, and animations that interrupt drag or layout.

6. Check interruption and exit paths.
   Test mentally what happens if the player closes a panel, changes dice, selects an option, enters battle, exits battle, has no reward, has partial materials staged, or leaves before confirming.

7. Report concrete findings first.
   Lead with bugs and risks grounded in files/methods. Then give the smallest safe fix. Keep design alternatives separate from defects.

## DiceWitch-Specific Invariants

- Map node effects that modify player stats should resolve before room panels open when that is the configured node flow.
- Forge/meditation material staging must never silently lose materials. If the player changes phase, chooses an affix, closes the panel, switches dice, or leaves the flow, staged materials must be consumed exactly once or refunded exactly once.
- A generated forge option may be unchosen while the player stages the next meditation; choosing an older option must still clean up any currently staged materials correctly.
- Battle victory must always have a continuation path back to map or reward, including the no-card-reward branch.
- Temporary roll effects such as "next roll fixed to X" must be consumed once and then cleared.
- Clone/squad mini dice should participate in dice settlement as independent dice when enemy/status effects depend on roll order, first die, or last die.
- Tooltip display and actual settlement must be sourced from the same dice/affix data. A tip that shows an affix while damage ignores it is a bug.
- `DiceInputManager.diceViewMonitor` should reference the battle left-top `UI_DiceView`. The map `UI_DiceView` is valid but should not drive battle input.

## Output Shape

For reviews, use this order:

1. Findings ordered by severity with file and method references.
2. Questions or assumptions only when they affect the fix.
3. Minimal fix plan or implemented change summary.
4. Verification performed or why Unity runtime verification was skipped.

When implementing, keep edits scoped to the broken handoff or state mutation. Avoid broad refactors unless the flow cannot be made reliable without them.
