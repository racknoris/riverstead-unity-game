# Known Issues

State at project start. Nothing built yet.

## Library / engine defects

*None found yet.* When one is found, suspect the pattern, not just the instance — in the previous stack, one careless porting mistake produced three separate joint defects.

| # | Issue | Status |
| --- | --- | --- |
| — | — | — |

## Open problems in the game

| # | Issue | Notes |
| --- | --- | --- |
| — | — | — |

## Risks carried over from the previous project

- **Joint fidelity is unproven until measured.** Unity's Box2D is maintained, but weld stiffness, hinge limits under motor load, and multi-joint stability must be verified in Milestone 1 with recorded numbers, not assumed.
- **A regression test counts only if it has been seen to fail.** The old hinge test passed against broken code. Every physics regression test must fail when its assertion is inverted before it is trusted.
- **Mobile frame rate is a claim until profiled on a device.** Record physics step cost and frame time in this file at Milestones 1 and 10.
- **Uncommitted work is lost work.** Git from Milestone 0, small commits. (Cost one fix last time.)
- **Silent rejection is a defect.** Any editor action the game refuses must explain itself to the player (Milestone 7).

## Planned work, not defects

Tracked in `docs/TASKS.md`; do not duplicate milestones here.

## Suggested order

1. Milestone 0, committed.
2. Milestone 1 joint verification before anything else is trusted.
