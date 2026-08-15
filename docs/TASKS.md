# Contraption Game POC Tasks (Unity)

Agent working rules live in `CLAUDE.md` (project root). Architecture rules live in `docs/ARCHITECTURE.md`. Style and project conventions live in `docs/CONVENTIONS.md`. Defects and risks live in `docs/ISSUES.md`.

## Recorded Deviations

Record material architectural deviations here **before** implementing them: decision, why, scope, and status. (In the previous project this log caught a re-sequencing that saved weeks; keep the habit.)

*None yet.*

## Milestone 0: Project Baseline

- [ ] Create a Unity 6.5 project from the **2D URP** template.
- [ ] `git init` immediately: Unity `.gitignore`, Git LFS for binary assets, first commit before any other work.
- [ ] Editor settings: Force Text serialization, Visible Meta Files.
- [ ] Add packages: Input System, UI Toolkit (built in), Unity Test Framework, `com.unity.nuget.newtonsoft-json`.
- [ ] Create the asmdef structure from `docs/ARCHITECTURE.md` §13 (Domain, Runtime, UI, Spike, Tests). Verify Domain compiles with no UnityEngine reference.
- [ ] Add one trivial edit-mode test and document the test command in the README (Unity CLI `-runTests -testPlatform EditMode`).
- [ ] Physics 2D settings: confirm fixed timestep; record solver iteration counts in `docs/CONVENTIONS.md` once tuned.

### Done when

- The project builds for Windows and Android target is configured.
- The edit-mode test suite runs from the command line.
- Everything is committed.

## Milestone 1: Fun Checkpoint — Go/No-Go (throwaway)

All code in `Assets/_Project/Spike/`. Exempt from structural rules; deleted in one commit after the verdict.

- [ ] Crude horizontal course: flat ground, one ramp, one gap, finish marker.
- [ ] 3–4 hard-coded contraption variants assembled in code: powered wheels + chassis; chassis + hinged arm; chassis + welded beam chain; chassis + spring part.
- [ ] Fragile cargo box with simple impact damage and a health number on screen.
- [ ] One-tap restart that destroys and rebuilds the whole simulation.
- [ ] Variant switcher (cycle button is enough).
- [ ] **Joint verification** (this killed the last stack — prove it early):
  - [ ] `FixedJoint2D` weld chain under load: measure droop angle; record it.
  - [ ] `HingeJoint2D` with `useLimits` under motor load: no launching, limits respected; record swing.
  - [ ] Motorised wheel torque feels controllable.
  - [ ] Each verification is a play-mode test that **fails when the assertion is inverted** (a test that cannot fail proves nothing).
- [ ] Android build on a real device: touch restart works, frame time measured with the Profiler and recorded in `docs/ISSUES.md`.

### Done when

- A written verdict is recorded below: is tinker-retry fun? Are the joints trustworthy?
- **Go/No-Go decision recorded. No milestone past this line starts before "Go".**
- `Spike/` deleted in one commit after tuning values are noted for Milestone 3.

**Verdict:** *(pending)*

## Milestone 2: Immutable Blueprint Models

- [ ] Domain models from `docs/ARCHITECTURE.md` §7 with value equality and stable IDs.
- [ ] JSON serialization with schema version; round-trip tests (serialize → deserialize → equal).
- [ ] A malformed/old-version payload fails loudly with a typed error, not silently.

### Done when

- Round-trip and versioning tests pass; no UnityEngine types anywhere in Domain.

## Milestone 3: Part Catalog

- [ ] `PartDefinitionAsset` ScriptableObjects for the seven initial parts, tuned from checkpoint numbers (re-derived, not pasted).
- [ ] `PartCatalog` asset as the single registry; a validation test asserting every `PartType` has an entry.
- [ ] Each asset exposes a plain domain `PartDefinition`.

### Done when

- Catalog is complete and covered by the validation test.

## Milestone 4: Simulation Builders

- [ ] `SimulationBuilder.Build(level, blueprint)` producing everything under one `SimulationRoot`.
- [ ] `PartViewFactory`, `BodyBuilder`, `JointBuilder`; joints created after all bodies, one deterministic pass.
- [ ] Build → run → destroy → rebuild cycle is leak-free (play-mode test: repeated rebuilds do not accumulate GameObjects or joints).

### Done when

- A hard-coded blueprint runs, resets, and rebuilds cleanly through the builders.

## Milestone 5: Game Flow

- [ ] `GameFlow` state machine: Editing / Running / Paused / Completed / Failed, with plain C# events.
- [ ] HUD (UI Toolkit): run, pause, reset; phase-appropriate visibility.
- [ ] `RunResult` produced on completion or failure.

### Done when

- The full phase cycle works from the UI against a hard-coded blueprint.

## Milestone 6: Touch Editor

- [ ] Hole-based placement: tap a hole, pick a part from a palette.
- [ ] Rotate in fixed increments; remove; connect/disconnect supported parts.
- [ ] Editor edits go through `ContraptionEditor` only; every edit yields a new blueprint.
- [ ] Selected part panel showing configuration.

### Done when

- A machine equivalent to a checkpoint variant can be built by touch alone on a device.

## Milestone 7: Placement Validation

- [ ] Overlap rejection, connection rules, ~12-part limit — all in the Domain assembly with unit tests.
- [ ] **Every rejection returns a player-readable reason, surfaced in the UI** (silent rejection was a recorded failure last time).

### Done when

- Invalid edits are impossible and the player is always told why.

## Milestone 8: Level, Win and Fail

- [ ] Level: start area, terrain, one obstacle, fragile cargo, finish sensor, slow projectile launcher, time limit, world bounds.
- [ ] Cargo damage model; fail on cargo death, out-of-bounds, or timeout; succeed on finish with health.
- [ ] Result screen with score inputs (health, time, parts).

### Done when

- A built machine can genuinely win or lose the course.

## Milestone 9: Persistence

- [ ] `IContraptionRepository` + JSON implementation in `persistentDataPath`.
- [ ] Current draft blueprint and level ID restored on launch; schema version checked.
- [ ] Transient editor state (selection, tool) resets on startup.

### Done when

- Kill the app mid-edit; relaunch restores the draft. Works on Android and Unity Web.

## Milestone 10: Device and Performance Pass

- [ ] Android build: touch feel review, landscape lock, safe-area check.
- [ ] Profile on the real device; record frame time and physics cost in `docs/ISSUES.md`. (Last project shipped a milestone with unverified mobile frame rate — do not repeat.)
- [ ] Unity Web build verified as the tester link.

### Done when

- Testers can play the loop on a phone and via a link, and someone retries voluntarily.
