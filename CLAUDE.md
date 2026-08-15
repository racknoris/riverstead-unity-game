# Agent Instructions

You are the coding agent for a Unity 6.5 2D contraption-building game POC.

## Source of truth

Read these before writing code, in this order:

1. `docs/ARCHITECTURE.md` — structural rules. Binding.
2. `docs/TASKS.md` — milestone sequence and decision gates. Work in order.
3. `docs/CONVENTIONS.md` — style, naming, assets, git, tests.
4. `docs/ISSUES.md` — known defects, risks, and lessons. Update it when you find or fix something.

## Working rules

- Complete milestones in order. **Milestone 1's go/no-go verdict blocks everything after it.**
- Prefer the smallest implementation that satisfies the current milestone.
- Record material architectural deviations in `docs/TASKS.md` under "Recorded Deviations" *before* implementing them.
- Keep commits small and coherent; commit after every task, never leave work uncommitted at the end of a session.
- Run the edit-mode test suite after every milestone (command documented in the README) and relevant play-mode tests when physics or lifecycle changed.
- When you discover a defect, log it in `docs/ISSUES.md` with a number, status, and evidence before working around it.

## Hard constraints (never violate)

- `Contraption.Domain` must not reference UnityEngine. If a change needs it to, the change is wrong — redesign, or record a deviation and stop for review.
- No per-frame physics state (position, velocity, contacts) copied into domain state or persisted anywhere.
- The simulation never mutates the stored blueprint; reset = destroy `SimulationRoot` and rebuild.
- Nothing outside `Assets/_Project/Spike/` references the Spike assembly.
- Do not add packages, DOTS/ECS, DI containers, event frameworks, backends, or multiplayer. If a package seems necessary, record a deviation and stop for review.
- A physics regression test is not done until you have shown it fails with the assertion inverted.

## Definition of done for any task

Code compiles for Windows and Android targets; tests pass; conventions followed; committed; `docs/ISSUES.md`/`docs/TASKS.md` updated if state changed.
