---
name: feedback: prefer simple cost-effective solutions
description: User prefers pragmatic, simple implementations over over-engineering. Project scope is small.
type: feedback
originSessionId: 7791e3c7-8824-46ca-a085-d27c574c5d97
---
Prefer simple, pragmatic solutions. The project is not large — don't over-engineer. If something can be done simply, do it that way. Cost-effective approaches beat architectural purity.

**Why:** User explicitly stated the project won't be very large and wants to keep velocity high by avoiding unnecessary complexity.

**How to apply:** When choosing between a simple solution (singletons, direct references) and a more complex one (DI frameworks, event buses, elaborate abstractions), default to the simple one unless there's a concrete, immediate problem it can't solve.
