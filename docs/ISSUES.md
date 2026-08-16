# Known Issues

State at project start. Nothing built yet.

## Library / engine defects

When one is found, suspect the pattern, not just the instance — in the previous stack, one careless porting mistake produced three separate joint defects.

Neither entry below is a Unity bug. Both are API semantics that are the *opposite* of the
obvious reading, which is worse than a bug: they fail quietly and get "fixed" in the wrong
direction. Both are pinned by tests in `Assets/_Project/Tests/PlayMode/JointFidelityTests.cs`.

| # | Issue | Status |
| --- | --- | --- |
| L1 | **`FixedJoint2D.frequency` is inverted from intuition: `0` (the default) means *completely rigid*, and any finite value is a soft spring.** "Make the weld stiffer" invites raising it, which makes welds dramatically worse — and non-monotonically, so tuning by feel produces noise. Measured droop on a 4-link cantilever with a 4× tip mass, at 8/3 solver iterations: default/`0` → **6.18°**; `1` Hz → **96.1°**; `10` → 12.5°; `100` → 48.7°; `1000` → 151.0°; `1e6` → 148.3°. | **Understood, guarded.** Leave `frequency` at its default for welds. Never set it to buy stiffness. Pinned by `FixedJoint2D_NonZeroFrequency_IsFarSofterThanTheRigidDefault`. |
| L3 | **`Rigidbody2D.interpolation` defaults to `None`, which looks like a performance problem and is not one.** Physics steps at 50 Hz against a 60 Hz+ display, so on frames with no physics step the body is drawn at a stale position and then jumps. Error scales with velocity, so it presents as constant low-level stutter that gets visibly worse the faster something moves. Found by playing the checkpoint, not by any test. | **Fixed, and generalised into `docs/CONVENTIONS.md`.** All visible dynamic bodies set `Interpolate`, and the camera follows in `LateUpdate` so it reads interpolated poses. Display-only, so `ARCHITECTURE.md` §11 determinism is unaffected. |
| L2 | **`HingeJoint2D` motor sign: a *positive* `motorSpeed` on a wheel hinged under a chassis drives in +X.** The intuitive reading (negative = clockwise = forward) is backwards and cost a debugging cycle — the first wheel test failed with `travelled = -5.50`, a perfectly healthy rig driving the wrong way. | **Understood, guarded.** Pinned by `HingeJoint2D_MotorisedWheels_DriveForwardAtControllableSpeed`, which asserts on signed travel, not distance. |

**Note on L3, for the Milestone 1 and 10 device passes.** The stutter it caused was nearly
misattributed twice: first to the spike's IMGUI HUD allocating every frame, then to the raised
16/8 solver iterations. Both were plausible, both were wrong, and the second would have been
*confirmed* by profiling on a phone — a slower device makes a sampling artefact look exactly
like a cost problem. It was only isolated by ruling the HUD out in a standalone build and by
noticing the judder scaled with fall speed. **Before concluding that anything is a mobile
performance problem, check that it is not a rendering-versus-simulation sampling artefact.**

## Open problems in the game

| # | Issue | Notes |
| --- | --- | --- |
| 1 | **Windows Standalone build module is not installed.** The Unity 6000.5.8f1 install has only `AndroidPlayer`, `MacStandaloneSupport`, `WebGLSupport`. Milestone 0's "builds for Windows" cannot be satisfied on this machine. | Open, low impact. Dev machine is macOS, so the *desktop development loop* role from `ARCHITECTURE.md` §3 is filled by the Mac standalone player, which builds and runs clean. Scripts **do** compile against the Android target, and the Android SDK/NDK ship with the installed module, so an APK can be built from the Editor. Install Windows Build Support via Unity Hub only if a Windows player is genuinely wanted; the gating platforms for the POC verdict are Android and Unity Web, both installed. |
| 2 | **Git LFS is not installed** (`git lfs` is not a command). `CONVENTIONS.md` requires LFS for textures, audio, and binary assets. | Open, not yet blocking — the repo currently has no binary assets. Deliberately did **not** add a `.gitattributes` with LFS filters, because committing LFS filter rules without the LFS client installed corrupts those files on checkout. Install `git-lfs` and add `.gitattributes` *before* the first texture or audio file lands (Milestone 1 art, at the latest). |

## Measured joint fidelity (Milestone 1)

Rig: 4-link welded cantilever, 1×0.2 links at mass 1, tip mass 4, 3 s settle, stepped via
`Physics2D.Simulate` at the 0.02 fixed timestep. Reproduce by running the play-mode suite.

**Weld droop is controlled by solver iterations, not by any joint parameter.** This is the
single most useful number from the checkpoint:

| Velocity / position iterations | Weld chain droop |
| --- | --- |
| 8 / 3 (Unity default) | 6.18° |
| **16 / 8 (adopted)** | **1.30°** |
| 32 / 16 | 0.41° |

16/8 was adopted: it buys a 4.75× stiffness improvement and lands well inside the 3° budget,
while 32/16 costs more solver time for a difference no player can see. **This is a desktop
measurement — the cost side is unverified until the Android profiling pass**, which is why
Milestone 10 revisits it.

Other recorded results at 16/8:

- `HingeJoint2D` with `useLimits`, driven by a deliberately over-powered motor (900°/s,
  10 000 torque) into a ±45° limit: worst overshoot **0.056°**, worst anchor drift
  **0.0000 units**. Limits hold and nothing launches. (At 8/3 the same rig overshot 1.736° —
  the tuned iterations improved limit fidelity ~30×.)
- Motorised wheels, `HingeJoint2D` + motor at 360°/s, chassis mass 2, wheel mass 0.5,
  friction 1.0: **5.50 units travelled in 3 s, top speed 2.40 units/s.** Doubling `motorSpeed`
  to 720°/s roughly doubles both.
- **`maxMotorTorque` is not a speed knob.** Across a 16× sweep (10 → 160) travel moved only
  5.48 → 5.50 units on flat ground. `motorSpeed` sets speed; torque only buys climbing and
  load capacity. Tune them separately in the catalog (Milestone 3).

## Measured device performance (Milestone 1)

Android, development build with Autoconnect Profiler, spike scene.
Device: **Samsung Galaxy S24 (SM-S921B/DS)**.

Two things to hold in mind when reading these. Development builds carry profiler
instrumentation, so the figures are pessimistic bounds rather than the ceiling — do not read a
later release-build improvement as a regression fix. And the S24 is a 2024 flagship, so this is
close to a **best case** for Android; a mid-range or older device is the number that would
actually gate the POC, and has not been measured.

| Measure | Result |
| --- | --- |
| `PlayerLoop` frame time | ~16 ms — i.e. **at the 60 fps cap**, not near a limit. `SpikeBootstrap` sets `targetFrameRate = 60`, so most of that is idle. |
| `WaitForTargetFPS` (idle) | **11 ms typical, 14 ms peak** — so actual work is only ~2–5 ms of the 16.67 ms budget, roughly **3× headroom**. This is the figure that makes the frame time meaningful; the ~16 ms above says nothing on its own. |
| `FixedUpdate.Physics2DFixedUpdate` | **0.16 ms** typical, **0.19 ms** peak during the gap landing (peak contacts and bodies in motion) |
| Frame spikes | None observed |
| `GC.Alloc` | 0–0.02 ms per frame |

**The solver question is answered.** Physics costs roughly **1% of the 16.67 ms frame budget**
on device. The 16/8 iteration count raised in Milestone 1 — the one desktop-tuned value whose
mobile cost was unverified — is comfortably affordable, with room for 32/16 if a future part
ever needs stiffer welds. The `docs/CONVENTIONS.md` caveat about unverified mobile cost is
retired for the POC's scale.

Three limits on how far this generalises. The spike scene is small (roughly a dozen static
bodies and eight dynamic ones); Milestone 8 adds projectiles and Milestone 6 allows ~12 player
parts, so this is not a measurement of the finished game — but 0.16 ms leaves a lot of room to
grow into. It is a flagship device. And it is a development build. Milestone 10 should re-measure
on the weakest device anyone is expected to play on, which is the one that decides whether the
POC ships, not this one.

The absence of spikes independently confirms the L3 interpolation fix holds on device.

## Risks carried over from the previous project

- ~~**Joint fidelity is unproven until measured.**~~ **Retired.** Measured above; weld,
  hinge-under-limit, and motorised drive are verified with recorded numbers.
- **A regression test counts only if it has been seen to fail.** The old hinge test passed against broken code. Every physics regression test must fail when its assertion is inverted before it is trusted. *(All nine assertions in `JointFidelityTests` have been observed failing when inverted — see `docs/TASKS.md` Milestone 1.)*
- ~~**Mobile frame rate is a claim until profiled on a device.**~~ **Discharged for Milestone 1** — measured above, on hardware. Still live for Milestone 10, when the scene is no longer trivial.
- **Uncommitted work is lost work.** Git from Milestone 0, small commits. (Cost one fix last time.)
- **Silent rejection is a defect.** Any editor action the game refuses must explain itself to the player (Milestone 7).

## Planned work, not defects

Tracked in `docs/TASKS.md`; do not duplicate milestones here.

## Suggested order

1. Milestone 0, committed.
2. Milestone 1 joint verification before anything else is trusted.
