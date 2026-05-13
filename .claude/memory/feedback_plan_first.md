---
name: plan-before-code
description: User wants full flow alignment before implementation, not iterative guess-and-fix
type: feedback
originSessionId: 7791e3c7-8824-46ca-a085-d27c574c5d97
---
**Rule**: Before writing code for a new feature or significant change, write out the complete interaction flow and get user approval. Don't implement piecemeal and fix later.

**Why**: Forge UI was rewritten 4+ times because I didn't understand the full flow first. User had to correct me iteratively. They expect me to think through the entire flow upfront.

**How to apply**: 
- For any multi-step interaction, describe the full flow in plain language before touching code
- Cover all phases, edge cases, and state transitions
- Get explicit confirmation before implementing
