---
name: design-philosophy
description: User preferences about design approach, simplicity, and WYSIWYG
type: feedback
originSessionId: 7791e3c7-8824-46ca-a085-d27c574c5d97
---
**Rule**: Keep it simple, cost-effective. This is a small project — avoid over-engineering.

**Why**: User explicitly stated preference multiple times. Project scope is modest.

**How to apply**: 
- Prefer single-field configs over multi-field (e.g., forge bonus = one number = WYSIWYG)
- Auto-find UI children by name instead of requiring manual Inspector drags on every instance
- Shared SO configs over per-instance field duplication
- Don't create derived classes when a simple field on a base class works
- WYSIWYG: tooltip shows what player gets, damage equals tooltip value — no hidden separate bonuses
