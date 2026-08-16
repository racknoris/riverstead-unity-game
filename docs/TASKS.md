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

- [x] Crude horizontal course: flat ground, one ramp, one gap, finish marker. Plus two bumps on the run-in and a back wall.
- [x] 3–4 hard-coded contraption variants assembled in code: powered wheels + chassis; chassis + hinged arm; chassis + welded beam chain; chassis + spring part. **All four include the powered-wheel drive** — a chassis that cannot attempt the course answers neither the fun question nor the joint question.
- [x] Fragile cargo box with simple impact damage and a health number on screen. Damage applies above a 2.5 units/s impact threshold, so ordinary rolling contact is free and only hard landings hurt.
- [x] One-tap restart that destroys and rebuilds the whole simulation. Restart destroys `SimulationRoot` and rebuilds — the checkpoint deliberately honours `ARCHITECTURE.md` §8 here, because that is one of the things it exists to test.
- [x] Variant switcher (cycle button is enough).

#### Headless variant sweep

Verified by building a desktop player, instrumenting it to auto-cycle the variants, and
running it headless. Final state — all four complete the course:

| Variant | Result | Time | Cargo health |
| --- | --- | --- | --- |
| Powered wheels | Finished | 22.5 s | 89 / 100 |
| Hinged arm | Finished | 23.6 s | 81 / 100 |
| Welded beam chain | Finished | 22.7 s | 96 / 100 |
| Spring suspension | Finished | 22.3 s | 91 / 100 |

The sweep paid for itself three times, and every bug it found was mine rather than Unity's:

1. **Wheels mounted at `+0.45` instead of `-0.62`** — inside the cargo tray rather than under
   the chassis. Every machine sat on its belly and ground its own cargo to pieces without
   moving (`cargoX=0.0`, health 2–55). The chassis-local frame is now written down as named
   constants in `SpikeContraptions` instead of being re-derived at each call site.
2. **Bumps 0.35–0.45 high against a 0.45 wheel radius** — walls, not bumps. Now ~0.2.
3. **A rotated ramp box whose upper end poked 0.18 above the plateau it met.** That lip
   launched the bare rover into a backflip it could never recover from; the trace showed it
   inverted at `rot=-178` and motionless for 55 straight seconds. This one is worth
   remembering: it presents exactly like a physics or joint defect, and it was a content bug.
   The ramp is now derived from its two surface endpoints so it meets adjoining geometry flush
   by construction. The wheelbase was also widened from ±1.00 to ±1.15.

The lesson generalises past the spike: **when a machine misbehaves, suspect the level geometry
before suspecting the solver.** Two of the three bugs above were invisible in a still frame and
obvious in a two-second position trace.
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
- [x] Android build on a real device: touch restart works, frame time measured with the Profiler and recorded in `docs/ISSUES.md`. **Done** — runs smooth on device, physics at 0.16 ms/step (~1% of the frame budget), no spikes. Numbers and caveats in `docs/ISSUES.md`. Setup notes, since they were not obvious: `EditorSettings.m_HideBuildProfileClassicPlatforms` is `1` in this project, so platforms do not appear in the Build Profiles window until you use **+ Add Build Profile**; and the profiler needs **Development Build** plus **Autoconnect Profiler** ticked on the profile. Everything else was ready: scripts compile against the Android target, the SDK and NDK ship with the installed module, orientation is locked to landscape, and `Assets/_Project/Scenes/Spike.unity` is scene 0 in Build Settings. The HUD is IMGUI, whose buttons take touch, so Restart and Next variant work without an EventSystem.

### Done when

- A written verdict is recorded below: is tinker-retry fun? Are the joints trustworthy?
- **Go/No-Go decision recorded. No milestone past this line starts before "Go".**
- [x] `Spike/` deleted in one commit after tuning values are noted for Milestone 3. Tuning values are recorded under Milestone 3; the spike is recoverable from git at `5fdb726`. `Scenes/Spike.unity` went with it, replaced by a placeholder `Scenes/Main.unity` holding only a camera so Build Settings still points at something real. The joint verification tests were **kept** — they test Unity, not spike code.

**Verdict: GO.** Recorded 2026-08-16 by the project owner after playing the checkpoint on
desktop and on an Android device.

Both halves of the question came back positive: the joints are trustworthy with recorded
numbers rather than assurances, and the loop was judged worth continuing with. Performance on
device is comfortable — roughly 3× frame-time headroom, physics at ~1% of budget.

Milestones 2 onward are unblocked.

The evidence behind the verdict, and its limits:

- **Are the joints trustworthy? Yes**, with recorded numbers rather than assurances — see the
  joint verification notes above and the measurement table in `docs/ISSUES.md`. Welds hold to
  1.30°, hinge limits hold under a deliberately over-powered motor to 0.056° of overshoot with
  zero anchor drift, and motorised drive is stable and controllable. `ARCHITECTURE.md` §10's
  `RelativeJoint2D` fallback is not needed. The one caveat is that these are desktop numbers;
  the solver was raised to 16/8 iterations and **that cost is unmeasured on a phone.**
- **Is tinker-retry fun? Unknown, and not answerable from a headless sweep.** All four variants
  complete the course, which establishes that the slice *works*, not that it is *enjoyable*.
  The honest read is that the spike currently under-tests the question: the variants are fixed
  and the machine is autonomous, so a run is watched rather than played, and every variant now
  finishes on the first try with 81–96 health. Nobody has yet had a reason to retry. Worth
  keeping in mind while playing it — if the loop feels flat, the likely cause is that there is
  nothing to tune between runs, which is precisely what Milestone 6 adds.

**To play it:** open `Assets/_Project/Scenes/Spike.unity` and press Play. `R` or `Space`
restarts, `Tab` cycles variants; on a device, use the two on-screen buttons.

## Milestone 2: Immutable Blueprint Models

- [x] Domain models from `docs/ARCHITECTURE.md` §7 with value equality and stable IDs. All eight, plus `EditorPosition` — a domain stand-in for `Vector2`, since the domain cannot reference UnityEngine.
- [x] JSON serialization with schema version; round-trip tests (serialize → deserialize → equal).
- [x] A malformed/old-version payload fails loudly with a typed error, not silently. `BlueprintSerializationException` carries a `BlueprintSerializationError` distinguishing malformed JSON, missing version, unsupported version, and invalid content; each has a test.

### Done when

- [x] Round-trip and versioning tests pass; no UnityEngine types anywhere in Domain. **29 tests green.**

### Notes

- **Typed ids.** `PartId`, `HoleId` and `AttachmentId` are distinct types rather than strings,
  because attachments pair a part id with a hole id and those are precisely the two things an
  editor transposes. JSON converters keep them as bare strings in the saved format.
- **Value equality is hand-written where collections are involved.** Records compare list members
  by reference, so a deserialised blueprint would never equal its original. See
  `docs/CONVENTIONS.md`.
- **The defensive-copy test was observed failing** before being trusted: removing the copy in
  `ContraptionBlueprint` made it fail, then it was restored.
- **Schema version is checked before binding**, so an old file reports "version 0, expected 1"
  rather than a confusing property-level error. Migration is deliberately not attempted — the POC
  has no saved data worth migrating, and a real migration path is a Milestone 9 concern at the
  earliest.
- **`RunResult` carries scoring inputs but computes no score.** How health, time and part count
  combine will change repeatedly; baking a formula into the result object makes past results
  unreadable when it does.
- **Unity 6.5 is C# 9**, which cost a compile cycle: `record struct` is C# 10. Recorded in
  `docs/CONVENTIONS.md` so it is not rediscovered in Milestone 5.

## Milestone 3: Part Catalog

- [x] `PartDefinitionAsset` ScriptableObjects for the seven initial parts, tuned from checkpoint numbers (re-derived, not pasted). **Eight assets, not seven:** the seven attachable parts plus `Chassis`, which is a `PartType` and needs mass and hole ids like any other. `ARCHITECTURE.md` §10 counts it separately as the thing parts attach *to*.
- [x] `PartCatalog` asset as the single registry; a validation test asserting every `PartType` has an entry.
- [x] Each asset exposes a plain domain `PartDefinition`.

### Done when

- [x] Catalog is complete and covered by the validation test. **41 tests green** across both suites.

### Notes

- **The rule lives in the domain, the data in the asset.** `PartCatalogValidator` is pure C# in
  `Domain/Validation/`, per `ARCHITECTURE.md` §9's requirement that the domain validate the
  catalog without touching Unity types. The payoff is that the rule is testable without loading
  an asset, so `PartCatalogValidatorTests` proves it can *fail* (missing type, duplicate, empty
  entry) and `PartCatalogAssetTests` only asks whether the shipped asset satisfies it.
- **Validation returns a list of problems rather than throwing.** A catalog with three gaps
  should report three; failing on the first would mean fixing it one compile cycle at a time.
- **Values were re-derived, not pasted**, as `ARCHITECTURE.md` §14 requires. Two deliberate
  departures from the checkpoint numbers: the generic `Hinge` part gets symmetric ±45° limits
  rather than the spike's −35°/+55°, which were tuned for one specific sweeping arm; and
  `PoweredWheel` costs 2 against every other part's 1, so the "use fewer parts" scoring input has
  something to bite on. `Chassis` costs 0 — it is mandatory, not a choice.
- **Two asset tests encode Milestone 1 lessons**: wheels must carry friction (an unmaterialed
  wheel spins in place) and a radius (obstacle heights are judged against it).
- `_prefab` is deliberately unassigned on every asset. Prefabs arrive with the builders in
  Milestone 4; the field exists so the catalog is the place they land.
- `Contraption.Tests.EditMode` now references `Contraption.Runtime`. No new asmdef, so no
  deviation required — the dependency direction is still Tests → Runtime → Domain.

### Checkpoint tuning values (harvested before `Spike/` was deleted)

Recorded here because `ARCHITECTURE.md` §14 requires these to be **re-derived deliberately, not
copy-pasted**. They are a starting point that produced a machine which crossed the course at
~2.4 units/s in 22–24 s with cargo health 81–96 — not values that have earned a place in the
catalog. The originals are in git at `5fdb726` if the context around one is ever needed.

| Value | Checkpoint setting | Note |
| --- | --- | --- |
| Drive `motorSpeed` | 330 °/s | Sets speed. Yielded ~2.4 units/s ground speed. |
| Drive `maxMotorTorque` | 120 | Buys climbing, **not** speed — above ~10 it stops affecting flat-ground travel (`docs/ISSUES.md`). Sized for the ramp. |
| Wheel radius / mass | 0.45 / 0.6 | Obstacles above ~half the radius stop the machine; a full radius is a wall. |
| Chassis mass | 2.2 | Tray: floor 2.8 × 0.30, walls 0.30 × 1.10 at ±1.25. |
| Wheel mount | ±1.15, y −0.62 | Wheelbase is load-bearing for stability: ±1.00 tipped the bare rover over the plateau edge. |
| Cargo | 1.1 × 1.1, mass 0.8 | |
| Cargo damage | threshold 2.5 units/s, 9 HP per excess unit/s, 100 HP | Threshold matters: below it, ordinary rolling contact is free. |
| Weld (`FixedJoint2D`) | **defaults, untouched** | `frequency` 0 = rigid. Never raise it (`docs/ISSUES.md` L1). |
| Beam link | 0.9 × 0.18, mass 0.25, ×4 | |
| Hinge arm | 1.8 × 0.22, mass 0.5, limits −35°/+55°, motor 120 °/s @ 260 torque | |
| Spring suspension | `frequency` 3.5, `dampingRatio` 0.5, arm limits ±18°, arm mass 0.3 | Note `SpringJoint2D.frequency` is a genuine spring parameter, unlike `FixedJoint2D`'s. |
| Wheel/ground friction | `PhysicsMaterial2D` friction 1.0, bounciness 0 | Required — unmaterialed wheels spin in place. |
| Run time limit | 75 s | Course took 22–24 s, so this was generous. |

Level-shape numbers, for whoever builds the real level in Milestone 8: plateau top at y 1.4,
landing top at y 0.4 (a **1.0 drop** — at 1.5 the bare rover pitched onto its back every run),
obstacles protruding ~0.2, finish at x 58.

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
