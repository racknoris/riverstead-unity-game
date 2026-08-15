# Contraption Game POC Tasks (Unity)

Agent working rules live in `CLAUDE.md` (project root). Architecture rules live in `docs/ARCHITECTURE.md`. Style and project conventions live in `docs/CONVENTIONS.md`. Defects and risks live in `docs/ISSUES.md`.

## Recorded Deviations

Record material architectural deviations here **before** implementing them: decision, why, scope, and status. (In the previous project this log caught a re-sequencing that saved weeks; keep the habit.)

*None yet.*

## Milestone 0: Project Baseline

- [x] Create a Unity 6.5 project from the **2D URP** template. (`6000.5.8f1`)
- [x] `git init` immediately: Unity `.gitignore`, ~~Git LFS for binary assets~~, first commit before any other work. **LFS deferred — see `docs/ISSUES.md` #2.**
- [x] Editor settings: Force Text serialization, Visible Meta Files. (Both already correct from the template; verified, not assumed.)
- [x] Add packages: Input System (1.20.0), UI Toolkit (built in), Unity Test Framework (1.7.0), `com.unity.nuget.newtonsoft-json` (3.2.1, added).
- [x] Create the asmdef structure from `docs/ARCHITECTURE.md` §13 (Domain, Runtime, UI, Spike, Tests). Verify Domain compiles with no UnityEngine reference.
- [x] Add one trivial edit-mode test and document the test command in the README (Unity CLI `-runTests -testPlatform EditMode`).
- [x] Physics 2D settings: confirm fixed timestep; record solver iteration counts in `docs/CONVENTIONS.md` once tuned. (Baseline defaults recorded; tuned values pending Milestone 1.)

### Done when

- ~~The project builds for Windows~~ and Android target is configured. **Windows build support is not installed on this machine — `docs/ISSUES.md` #1.** Android, Mac and WebGL modules are installed; Android orientation is locked to landscape.
- [x] The edit-mode test suite runs from the command line. 2 tests, both passing; command documented in `README.md`.
- [x] Everything is committed.

**Notes.** The Domain no-UnityEngine rule is enforced twice: `noEngineReferences: true`
in the asmdef (compile time) and `DomainAssembly_Always_DoesNotReferenceUnityEngine`
(test time). Per `docs/CONVENTIONS.md`, that guard was only trusted after being observed
to fail: flipping `noEngineReferences` to `false` and adding a field of type
`UnityEngine.Vector2` to a Domain type made it fail with
`Found: UnityEngine.CoreModule`. The violation was then reverted and the suite re-run green.

Nullable reference types are enabled in Domain via `Assets/_Project/Domain/csc.rsp`
(asmdefs have no nullable toggle).

## Milestone 1: Fun Checkpoint — Go/No-Go (throwaway)

All code in `Assets/_Project/Spike/`. Exempt from structural rules; deleted in one commit after the verdict.

- [ ] Crude horizontal course: flat ground, one ramp, one gap, finish marker.
- [ ] 3–4 hard-coded contraption variants assembled in code: powered wheels + chassis; chassis + hinged arm; chassis + welded beam chain; chassis + spring part.
- [ ] Fragile cargo box with simple impact damage and a health number on screen.
- [ ] One-tap restart that destroys and rebuilds the whole simulation.
- [ ] Variant switcher (cycle button is enough).
- [x] **Joint verification** (this killed the last stack — prove it early):
  - [x] `FixedJoint2D` weld chain under load: measure droop angle; record it. **1.30°** at the tuned 16/8 solver iterations (6.18° at Unity's defaults). Full table in `docs/ISSUES.md`.
  - [x] `HingeJoint2D` with `useLimits` under motor load: no launching, limits respected; record swing. **Overshoot 0.056° past a ±45° limit, anchor drift 0.0000 units**, under a deliberately over-powered 900°/s, 10 000-torque motor.
  - [x] Motorised wheel torque feels controllable. **5.50 units in 3 s, top speed 2.40 units/s** at 360°/s. Numbers pinned; the *feel* half of this is still a human judgement and belongs to the verdict below.
  - [x] Each verification is a play-mode test that **fails when the assertion is inverted** (a test that cannot fail proves nothing).

#### Joint verification notes

Tests live in `Assets/_Project/Tests/PlayMode/JointFidelityTests.cs` — deliberately **not**
in `Spike/`. They verify Unity's Physics 2D, not spike code, so they must survive the spike's
deletion. They build their own rigs and step physics with `Physics2D.Simulate` so results are
deterministic rather than frame-paced.

**Inversion proof.** All **nine** assertions were observed failing, in three passes, so that
no assertion was masked by an earlier one short-circuiting its test:

1. Primary assertion of each of the 5 tests inverted → all 5 failed.
2. The 4 secondary assertions inverted → all 3 tests containing one failed.
3. The chassis-height assertion inverted alone → its test failed. (Needed separately: NUnit
   stops at the first failed assert, so pass 2 left it unproven.)

The suite was restored and re-run green after each pass.

**Two findings changed the design**, both the opposite of the obvious reading, both recorded
as `docs/ISSUES.md` L1 and L2: `FixedJoint2D.frequency` is inverted (0 = rigid), and the
hinge motor sign convention is backwards from the naive guess. The first one matters most —
it means `ARCHITECTURE.md` §10's fallback plan ("or `RelativeJoint2D` if stiffness
disappoints") is **not needed**. Weld stiffness was never a joint-parameter problem; it was a
solver-iteration problem, now fixed project-wide at 16/8.
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
