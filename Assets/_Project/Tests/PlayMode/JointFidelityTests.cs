using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Contraption.Tests.PlayMode
{
    /// <summary>
    /// Milestone 1 joint verification (docs/TASKS.md).
    ///
    /// These tests exist because joint fidelity killed the previous stack: weld stiffness,
    /// hinge limits under motor load, and multi-joint stability were assumed rather than
    /// measured. They verify Unity's Physics 2D behaviour directly and build their own rigs,
    /// so they deliberately do NOT reference Contraption.Spike and survive its deletion.
    ///
    /// Physics is stepped manually via <see cref="Physics2D.Simulate"/> so a run is
    /// deterministic and does not depend on frame pacing.
    ///
    /// Measured numbers and the two counterintuitive findings they produced are recorded in
    /// docs/ISSUES.md. Every assertion here has been observed to fail when inverted.
    /// </summary>
    public sealed class JointFidelityTests
    {
        /// <summary>Project fixed timestep (docs/CONVENTIONS.md).</summary>
        private const float FixedStep = 0.02f;

        /// <summary>Three seconds: long enough for a chain to settle, short enough to keep tests fast.</summary>
        private const int SettleSteps = 150;

        private readonly List<GameObject> _spawned = new List<GameObject>();
        private PhysicsMaterial2D _grippyMaterial;
        private SimulationMode2D _previousSimulationMode;

        [SetUp]
        public void SetUp()
        {
            _previousSimulationMode = Physics2D.simulationMode;
            Physics2D.simulationMode = SimulationMode2D.Script;
        }

        [TearDown]
        public void TearDown()
        {
            DestroySpawned();
            Physics2D.simulationMode = _previousSimulationMode;
        }

        // =====================================================================================
        // Weld chain: FixedJoint2D under cantilever load.
        // =====================================================================================

        /// <summary>
        /// Guards the project's Physics 2D solver settings, which are the lever that actually
        /// controls weld stiffness (see <see cref="FixedJoint2D_WeldChain_HoldsNearlyRigidAtProjectSolverSettings"/>).
        /// Lowering these back to Unity's 8/3 defaults silently makes every weld in the game soggy,
        /// so it should break a test rather than be discovered by feel.
        /// </summary>
        [Test]
        public void Physics2DSettings_Always_KeepTunedSolverIterations()
        {
            Assert.That(
                Physics2D.velocityIterations,
                Is.GreaterThanOrEqualTo(TunedVelocityIterations),
                "Physics 2D velocity iterations dropped below the value tuned in Milestone 1.");

            Assert.That(
                Physics2D.positionIterations,
                Is.GreaterThanOrEqualTo(TunedPositionIterations),
                "Physics 2D position iterations dropped below the value tuned in Milestone 1.");
        }

        /// <summary>Tuned in Milestone 1; recorded in docs/CONVENTIONS.md.</summary>
        private const int TunedVelocityIterations = 16;

        private const int TunedPositionIterations = 8;

        /// <summary>
        /// The shipping weld configuration: FixedJoint2D left at its defaults, stiffness bought
        /// with solver iterations. A 4-link cantilever carrying a tip mass four times a link's
        /// mass must stay visually straight.
        /// </summary>
        [Test]
        public void FixedJoint2D_WeldChain_HoldsNearlyRigidAtProjectSolverSettings()
        {
            float droop = MeasureWeldChainDroop(overrideFrequency: null);

            TestContext.WriteLine(
                $"Weld chain droop at project settings "
                + $"({Physics2D.velocityIterations}/{Physics2D.positionIterations} iterations): {droop:F3} degrees");

            Assert.That(
                droop,
                Is.LessThan(MaxAcceptableWeldDroopDegrees),
                $"A welded beam chain drooped {droop:F3} degrees, past the {MaxAcceptableWeldDroopDegrees} degree "
                + "budget. Welds this soft read as rubber rather than as a rigid machine.");
        }

        /// <summary>
        /// Records the trap that cost time in Milestone 1: <see cref="AnchoredJoint2D"/> stiffness
        /// reads like a spring parameter, so "make the weld stiffer" invites raising
        /// <see cref="FixedJoint2D.frequency"/>. It does the opposite. Frequency 0 - the default -
        /// means *completely rigid*; any finite frequency is a soft spring, and the resulting droop
        /// does not even vary monotonically with the value.
        ///
        /// If this test ever fails, Unity changed that behaviour and the tuning guidance in
        /// docs/ISSUES.md needs rewriting.
        /// </summary>
        [Test]
        public void FixedJoint2D_NonZeroFrequency_IsFarSofterThanTheRigidDefault()
        {
            float rigidDroop = MeasureWeldChainDroop(overrideFrequency: null);
            DestroySpawned();
            float springyDroop = MeasureWeldChainDroop(overrideFrequency: 1f);

            TestContext.WriteLine(
                $"Weld droop: default (frequency 0, rigid) = {rigidDroop:F3} deg, "
                + $"frequency 1 Hz = {springyDroop:F3} deg");

            Assert.That(
                springyDroop,
                Is.GreaterThan(rigidDroop * 10f),
                "Expected a finite FixedJoint2D frequency to be dramatically softer than the rigid "
                + "default. If it is not, Unity's joint stiffness semantics changed.");
        }

        /// <summary>
        /// Measured droop at the tuned 16/8 solver settings is ~1.3 degrees; the budget carries
        /// margin for the extra load a real contraption puts on a chain.
        /// </summary>
        private const float MaxAcceptableWeldDroopDegrees = 3f;

        /// <param name="overrideFrequency">
        /// Null leaves FixedJoint2D at its rigid defaults. A value assigns
        /// <see cref="FixedJoint2D.frequency"/>, turning the weld into a soft spring.
        /// </param>
        private float MeasureWeldChainDroop(float? overrideFrequency)
        {
            const int LinkCount = 4;
            const float LinkLength = 1f;
            const float LinkHeight = 0.2f;
            const float LinkMass = 1f;
            // A tip mass several times the link mass turns a mild sag into a clear signal.
            const float TipMass = 4f;

            GameObject anchor = CreateBox("Anchor", Vector2.zero, new Vector2(0.2f, 0.4f), RigidbodyType2D.Static, 1f);

            // Bodies first, then joints in one pass - the deterministic order the real
            // SimulationBuilder will use (ARCHITECTURE.md §8).
            var links = new List<Rigidbody2D>();
            for (int i = 0; i < LinkCount; i++)
            {
                bool isTip = i == LinkCount - 1;
                GameObject link = CreateBox(
                    $"Link{i}",
                    new Vector2((i * LinkLength) + (LinkLength * 0.5f), 0f),
                    new Vector2(LinkLength, LinkHeight),
                    RigidbodyType2D.Dynamic,
                    isTip ? TipMass : LinkMass);
                links.Add(link.GetComponent<Rigidbody2D>());
            }

            Rigidbody2D previous = anchor.GetComponent<Rigidbody2D>();
            for (int i = 0; i < links.Count; i++)
            {
                var joint = links[i].gameObject.AddComponent<FixedJoint2D>();
                joint.connectedBody = previous;
                joint.autoConfigureConnectedAnchor = false;
                // Weld at the seam between this link and the previous one.
                joint.anchor = new Vector2(-LinkLength * 0.5f, 0f);
                joint.connectedAnchor = i == 0 ? Vector2.zero : new Vector2(LinkLength * 0.5f, 0f);

                if (overrideFrequency.HasValue)
                {
                    joint.frequency = overrideFrequency.Value;
                    joint.dampingRatio = 1f;
                }

                previous = links[i];
            }

            Step(SettleSteps);

            Vector2 anchorPosition = anchor.GetComponent<Rigidbody2D>().position;
            Vector2 tipPosition = links[links.Count - 1].position;
            return Mathf.Abs(Vector2.SignedAngle(Vector2.right, tipPosition - anchorPosition));
        }

        // =====================================================================================
        // Hinge limits under motor load.
        // =====================================================================================

        /// <summary>
        /// A motor driving hard into a hinge limit is the case that produced launching bugs in the
        /// previous stack. Verifies the limit holds and that the arm stays pinned to its anchor.
        /// </summary>
        [Test]
        public void HingeJoint2D_MotorDrivingIntoLimit_RespectsLimitAndDoesNotLaunch()
        {
            const float ArmLength = 2f;
            const float LimitDegrees = 45f;
            // Deliberately over-powered: a motor that cannot overwhelm the limit proves nothing.
            const float MotorSpeedDegreesPerSecond = 900f;
            const float MaxMotorTorque = 10000f;

            GameObject anchor = CreateBox("HingeAnchor", Vector2.zero, new Vector2(0.3f, 0.3f), RigidbodyType2D.Static, 1f);
            GameObject arm = CreateBox(
                "Arm",
                new Vector2(ArmLength * 0.5f, 0f),
                new Vector2(ArmLength, 0.2f),
                RigidbodyType2D.Dynamic,
                1f);

            var hinge = arm.AddComponent<HingeJoint2D>();
            hinge.connectedBody = anchor.GetComponent<Rigidbody2D>();
            hinge.autoConfigureConnectedAnchor = false;
            hinge.anchor = new Vector2(-ArmLength * 0.5f, 0f);
            hinge.connectedAnchor = Vector2.zero;
            hinge.useLimits = true;
            hinge.limits = new JointAngleLimits2D { min = -LimitDegrees, max = LimitDegrees };
            hinge.useMotor = true;
            hinge.motor = new JointMotor2D
            {
                motorSpeed = MotorSpeedDegreesPerSecond,
                maxMotorTorque = MaxMotorTorque
            };

            Rigidbody2D armBody = arm.GetComponent<Rigidbody2D>();
            float worstOvershoot = 0f;
            float worstAnchorDrift = 0f;

            for (int step = 0; step < SettleSteps; step++)
            {
                Physics2D.Simulate(FixedStep);

                worstOvershoot = Mathf.Max(worstOvershoot, Mathf.Abs(hinge.jointAngle) - LimitDegrees);

                // The hinge point must stay pinned to the anchor at the origin. If it separates,
                // the arm is being pulled off its joint - the launching failure mode.
                Vector2 hingeWorldPoint = armBody.transform.TransformPoint(hinge.anchor);
                worstAnchorDrift = Mathf.Max(worstAnchorDrift, hingeWorldPoint.magnitude);
            }

            TestContext.WriteLine(
                $"Hinge under motor load: worst overshoot past the {LimitDegrees} deg limit = {worstOvershoot:F3} deg, "
                + $"worst anchor drift = {worstAnchorDrift:F4} units");

            Assert.That(
                worstOvershoot,
                Is.LessThan(MaxAcceptableLimitOvershootDegrees),
                $"Hinge overshot its {LimitDegrees} degree limit by {worstOvershoot:F3} degrees under motor load.");

            Assert.That(
                worstAnchorDrift,
                Is.LessThan(MaxAcceptableAnchorDriftUnits),
                $"Hinge anchor drifted {worstAnchorDrift:F4} units from its connected anchor - the arm is being "
                + "pulled off its joint, which is the launching failure mode from the previous stack.");
        }

        /// <summary>Solver softness lets a hard-driven limit overshoot slightly; measured ~0.5 deg at 16/8.</summary>
        private const float MaxAcceptableLimitOvershootDegrees = 2f;

        /// <summary>The hinge point should stay effectively pinned; measured drift is ~0.</summary>
        private const float MaxAcceptableAnchorDriftUnits = 0.05f;

        // =====================================================================================
        // Motorised wheel.
        // =====================================================================================

        /// <summary>
        /// A motorised two-wheel chassis must actually drive, and at a speed a player can read.
        /// Also pins the motor sign convention: a positive <see cref="JointMotor2D.motorSpeed"/>
        /// on a wheel hinged under a chassis drives in +X. This is the opposite of the naive
        /// "negative = clockwise = forward" guess and is worth a failing test if it ever changes.
        /// </summary>
        [Test]
        public void HingeJoint2D_MotorisedWheels_DriveForwardAtControllableSpeed()
        {
            const float MotorSpeedDegreesPerSecond = 360f;
            // Above ~10 the torque ceiling stops mattering on flat ground; it buys climbing, not speed.
            const float MaxMotorTorque = 40f;
            const float WheelRadius = 0.3f;

            CreateGround();

            GameObject chassis = CreateBox("Chassis", new Vector2(0f, 0.75f), new Vector2(1.6f, 0.4f), RigidbodyType2D.Dynamic, 2f);
            Rigidbody2D chassisBody = chassis.GetComponent<Rigidbody2D>();

            CreatePoweredWheel(new Vector2(-0.6f, WheelRadius), WheelRadius, chassisBody, MotorSpeedDegreesPerSecond, MaxMotorTorque);
            CreatePoweredWheel(new Vector2(0.6f, WheelRadius), WheelRadius, chassisBody, MotorSpeedDegreesPerSecond, MaxMotorTorque);

            float startX = chassisBody.position.x;
            float fastestSpeed = 0f;

            for (int step = 0; step < SettleSteps; step++)
            {
                Physics2D.Simulate(FixedStep);
                fastestSpeed = Mathf.Max(fastestSpeed, Mathf.Abs(chassisBody.linearVelocity.x));
            }

            float travelled = chassisBody.position.x - startX;

            TestContext.WriteLine(
                $"Motorised wheels over {SettleSteps * FixedStep:F1}s at motorSpeed {MotorSpeedDegreesPerSecond}: "
                + $"travelled {travelled:F2} units, top speed {fastestSpeed:F2} units/s, "
                + $"final height {chassisBody.position.y:F2}");

            Assert.That(
                travelled,
                Is.GreaterThan(MinimumExpectedTravelUnits),
                $"Motorised wheels moved the chassis {travelled:F2} units in +X. A negative value means the motor "
                + "sign convention flipped; a near-zero value means the drive has no grip or no torque.");

            Assert.That(
                fastestSpeed,
                Is.LessThan(MaxControllableSpeedUnitsPerSecond),
                $"Chassis reached {fastestSpeed:F2} units/s. Past this the machine outruns the player's ability "
                + "to react and the course cannot be read.");

            Assert.That(
                chassisBody.position.y,
                Is.GreaterThan(0f),
                "Chassis sank through the ground - the drive rig is unstable, not merely fast.");
        }

        /// <summary>Measured travel at motorSpeed 360 is ~5.5 units in 3s; below 2 it is not driving.</summary>
        private const float MinimumExpectedTravelUnits = 2f;

        /// <summary>Measured top speed at motorSpeed 360 is ~2.4 units/s. Upper bound on readable speed.</summary>
        private const float MaxControllableSpeedUnitsPerSecond = 12f;

        private void CreatePoweredWheel(
            Vector2 position,
            float radius,
            Rigidbody2D chassisBody,
            float motorSpeed,
            float maxMotorTorque)
        {
            var wheel = new GameObject("PoweredWheel");
            _spawned.Add(wheel);
            wheel.transform.position = position;

            var body = wheel.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Dynamic;
            body.useAutoMass = false;
            body.mass = 0.5f;

            CircleCollider2D collider = wheel.AddComponent<CircleCollider2D>();
            collider.radius = radius;
            // Wheels need grip; without a friction material a driven wheel just spins in place.
            collider.sharedMaterial = GrippyMaterial;

            var hinge = wheel.AddComponent<HingeJoint2D>();
            hinge.connectedBody = chassisBody;
            hinge.autoConfigureConnectedAnchor = false;
            hinge.anchor = Vector2.zero;
            hinge.connectedAnchor = chassisBody.transform.InverseTransformPoint(position);
            hinge.useMotor = true;
            hinge.motor = new JointMotor2D { motorSpeed = motorSpeed, maxMotorTorque = maxMotorTorque };
        }

        private PhysicsMaterial2D GrippyMaterial
        {
            get
            {
                if (_grippyMaterial == null)
                {
                    _grippyMaterial = new PhysicsMaterial2D("JointFidelityGrip") { friction = 1f, bounciness = 0f };
                }

                return _grippyMaterial;
            }
        }

        private void CreateGround()
        {
            GameObject ground = CreateBox("Ground", new Vector2(0f, -0.5f), new Vector2(200f, 1f), RigidbodyType2D.Static, 1f);
            ground.GetComponent<BoxCollider2D>().sharedMaterial = GrippyMaterial;
        }

        // =====================================================================================

        private GameObject CreateBox(string name, Vector2 position, Vector2 size, RigidbodyType2D bodyType, float mass)
        {
            var box = new GameObject(name);
            _spawned.Add(box);
            box.transform.position = position;

            var body = box.AddComponent<Rigidbody2D>();
            body.bodyType = bodyType;
            if (bodyType == RigidbodyType2D.Dynamic)
            {
                body.useAutoMass = false;
                body.mass = mass;
            }

            BoxCollider2D collider = box.AddComponent<BoxCollider2D>();
            collider.size = size;

            return box;
        }

        private void DestroySpawned()
        {
            foreach (GameObject spawned in _spawned)
            {
                if (spawned != null)
                {
                    Object.DestroyImmediate(spawned);
                }
            }

            _spawned.Clear();
        }

        private static void Step(int steps)
        {
            for (int i = 0; i < steps; i++)
            {
                Physics2D.Simulate(FixedStep);
            }
        }
    }
}
