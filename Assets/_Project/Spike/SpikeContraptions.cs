using UnityEngine;

namespace Contraption.Spike
{
    public enum SpikeVariant
    {
        PoweredWheels,
        HingedArm,
        WeldedBeamChain,
        SpringSuspension
    }

    /// <summary>
    /// The four hard-coded contraption variants for the fun checkpoint (docs/TASKS.md Milestone 1).
    ///
    /// Every variant is the powered-wheel rover *plus* one distinguishing feature. That is a
    /// deliberate reading of the task list: a chassis with only a hinged arm cannot cross the
    /// course, and a variant that cannot attempt the course answers neither the fun question nor
    /// the joint question.
    ///
    /// Tuning constants here are checkpoint numbers. Milestone 3 re-derives them into
    /// PartDefinitionAssets deliberately — they are not to be copy-pasted.
    /// </summary>
    public static class SpikeContraptions
    {
        // Drive tuning, taken from the measurements in docs/ISSUES.md: motorSpeed sets speed,
        // maxMotorTorque buys climbing. The ramp needs noticeably more torque than flat ground.
        private const float MotorSpeedDegreesPerSecond = 330f;
        private const float MaxMotorTorque = 120f;
        private const float WheelRadius = 0.45f;
        private const float WheelMass = 0.6f;
        private const float ChassisMass = 2.2f;

        // Chassis-local geometry. These are load bearing: getting the wheel offset sign wrong
        // once already produced a machine that sat on its belly and ground its own cargo to
        // pieces, so the frame is written down explicitly rather than inferred at each call site.
        //
        //   floor collider spans local y -0.15 .. +0.15
        //   walls span local y  0.00 .. +1.10  (the cargo tray)
        //   wheels hang BELOW the floor at local y -0.62
        //
        // ChassisStartY is chosen so a wheel centre lands exactly one radius above ground y = 0.
        // Wheels sit just inboard of the tray walls. A narrower wheelbase than this makes the
        // bare rover tip forward over the plateau edge, which measures the wheelbase rather than
        // anything the checkpoint is trying to learn.
        private const float WheelLocalX = 1.15f;
        private const float WheelLocalY = -0.62f;
        private const float ChassisStartY = WheelRadius - WheelLocalY;
        private const float FloorHalfThickness = 0.15f;

        private static readonly Color ChassisColor = new Color(0.85f, 0.62f, 0.25f);
        private static readonly Color WheelColor = new Color(0.22f, 0.24f, 0.28f);
        private static readonly Color PartColor = new Color(0.55f, 0.70f, 0.90f);
        private static readonly Color CargoColor = new Color(0.90f, 0.35f, 0.40f);

        public static string DisplayName(SpikeVariant variant)
        {
            switch (variant)
            {
                case SpikeVariant.PoweredWheels: return "1. Powered wheels";
                case SpikeVariant.HingedArm: return "2. Hinged arm";
                case SpikeVariant.WeldedBeamChain: return "3. Welded beam chain";
                case SpikeVariant.SpringSuspension: return "4. Spring suspension";
                default: return variant.ToString();
            }
        }

        public static string Description(SpikeVariant variant)
        {
            switch (variant)
            {
                case SpikeVariant.PoweredWheels:
                    return "Bare rover. The baseline: does the drive feel controllable?";
                case SpikeVariant.HingedArm:
                    return "Motorised arm on a limited hinge. Tests hinge limits under load.";
                case SpikeVariant.WeldedBeamChain:
                    return "Four welded beams cantilevered forward. Tests weld stiffness.";
                case SpikeVariant.SpringSuspension:
                    return "Wheels on sprung arms. Tests whether springs protect the cargo.";
                default:
                    return string.Empty;
            }
        }

        /// <summary>
        /// Builds a variant under <paramref name="root"/> and returns its cargo.
        /// Bodies are created before joints, in one pass, mirroring ARCHITECTURE.md §8.
        /// </summary>
        public static SpikeCargo Build(Transform root, SpikeVariant variant)
        {
            GameObject chassis = BuildChassisTray(root, new Vector2(SpikeCourse.StartX, ChassisStartY));
            Rigidbody2D chassisBody = chassis.GetComponent<Rigidbody2D>();

            foreach (float side in new[] { -1f, 1f })
            {
                var mount = new Vector2(side * WheelLocalX, WheelLocalY);
                if (variant == SpikeVariant.SpringSuspension)
                {
                    AttachSprungWheel(root, chassisBody, mount, side);
                }
                else
                {
                    AttachPoweredWheel(root, chassisBody, mount);
                }
            }

            if (variant == SpikeVariant.HingedArm)
            {
                AttachHingedArm(root, chassisBody);
            }

            if (variant == SpikeVariant.WeldedBeamChain)
            {
                AttachWeldedBeamChain(root, chassisBody);
            }

            // Seated just above the tray floor, so it settles rather than drops in.
            const float CargoHalfHeight = 0.55f;
            float cargoY = ChassisStartY + FloorHalfThickness + CargoHalfHeight + 0.04f;
            return CreateCargo(root, new Vector2(SpikeCourse.StartX, cargoY));
        }

        /// <summary>
        /// The chassis is a tray: floor plus two walls, as three colliders on one body. The walls
        /// matter — without them the cargo simply slides off on the first slope, and the player
        /// never gets to make an interesting choice about protecting it.
        /// </summary>
        private static GameObject BuildChassisTray(Transform root, Vector2 position)
        {
            var chassis = new GameObject("Chassis");
            chassis.transform.SetParent(root, worldPositionStays: false);
            chassis.transform.localPosition = position;

            var body = chassis.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Dynamic;
            body.useAutoMass = false;
            body.mass = ChassisMass;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;

            AddPlate(chassis, new Vector2(0f, 0f), new Vector2(2.8f, 0.30f));
            AddPlate(chassis, new Vector2(-1.25f, 0.55f), new Vector2(0.30f, 1.10f));
            AddPlate(chassis, new Vector2(1.25f, 0.55f), new Vector2(0.30f, 1.10f));

            return chassis;
        }

        private static void AddPlate(GameObject chassis, Vector2 offset, Vector2 size)
        {
            BoxCollider2D collider = chassis.AddComponent<BoxCollider2D>();
            collider.offset = offset;
            collider.size = size;
            collider.sharedMaterial = SpikeVisuals.Grip;
            SpikeVisuals.AddSquareSprite(chassis.transform, offset, size, ChassisColor);
        }

        private static void AttachPoweredWheel(Transform root, Rigidbody2D chassisBody, Vector2 chassisLocalPosition)
        {
            Vector2 worldPosition = (Vector2)chassisBody.transform.localPosition + chassisLocalPosition;
            GameObject wheel = SpikeVisuals.CreateWheel("PoweredWheel", root, worldPosition, WheelRadius, WheelColor, WheelMass);

            var hinge = wheel.AddComponent<HingeJoint2D>();
            hinge.connectedBody = chassisBody;
            hinge.autoConfigureConnectedAnchor = false;
            hinge.anchor = Vector2.zero;
            hinge.connectedAnchor = chassisLocalPosition;
            hinge.useMotor = true;
            // Positive motorSpeed drives +X. This is the opposite of the intuitive reading and
            // is recorded as docs/ISSUES.md L2.
            hinge.motor = new JointMotor2D
            {
                motorSpeed = MotorSpeedDegreesPerSecond,
                maxMotorTorque = MaxMotorTorque
            };
        }

        /// <summary>
        /// A powered wheel on a sprung trailing arm. The arm pivots on an inboard hinge and
        /// carries the driven wheel at its outboard end; a SpringJoint2D across the arm gives
        /// the travel. The question this variant asks is whether suspension actually protects
        /// the cargo over the bumps and the landing.
        /// </summary>
        private static void AttachSprungWheel(
            Transform root,
            Rigidbody2D chassisBody,
            Vector2 wheelMount,
            float side)
        {
            Vector2 chassisOrigin = chassisBody.transform.localPosition;

            // Pivot sits inboard and slightly above the wheel, so the arm trails outward/downward.
            var pivot = new Vector2(side * 0.35f, WheelLocalY + 0.30f);
            Vector2 pivotToWheel = wheelMount - pivot;
            float armLength = pivotToWheel.magnitude;
            float armAngle = Mathf.Atan2(pivotToWheel.y, pivotToWheel.x) * Mathf.Rad2Deg;

            GameObject arm = SpikeVisuals.CreateBox(
                "SuspensionArm",
                root,
                chassisOrigin + pivot + (pivotToWheel * 0.5f),
                new Vector2(armLength, 0.18f),
                armAngle,
                PartColor,
                RigidbodyType2D.Dynamic,
                0.3f);
            Rigidbody2D armBody = arm.GetComponent<Rigidbody2D>();

            var armHinge = arm.AddComponent<HingeJoint2D>();
            armHinge.connectedBody = chassisBody;
            armHinge.autoConfigureConnectedAnchor = false;
            armHinge.anchor = new Vector2(-armLength * 0.5f, 0f);
            armHinge.connectedAnchor = pivot;
            armHinge.useLimits = true;
            armHinge.limits = new JointAngleLimits2D { min = -18f, max = 18f };

            // Spring from the arm's outboard end up to the chassis, so it resists the arm
            // swinging up under load - i.e. it carries the machine's weight.
            var springAnchorOnChassis = new Vector2(side * WheelLocalX, WheelLocalY + 0.75f);
            var spring = arm.AddComponent<SpringJoint2D>();
            spring.connectedBody = chassisBody;
            spring.autoConfigureDistance = false;
            spring.autoConfigureConnectedAnchor = false;
            spring.anchor = new Vector2(armLength * 0.5f, 0f);
            spring.connectedAnchor = springAnchorOnChassis;
            spring.distance = Vector2.Distance(wheelMount, springAnchorOnChassis);
            spring.frequency = 3.5f;
            spring.dampingRatio = 0.5f;

            GameObject wheel = SpikeVisuals.CreateWheel(
                "SprungWheel", root, chassisOrigin + wheelMount, WheelRadius, WheelColor, WheelMass);
            var driveHinge = wheel.AddComponent<HingeJoint2D>();
            driveHinge.connectedBody = armBody;
            driveHinge.autoConfigureConnectedAnchor = false;
            driveHinge.anchor = Vector2.zero;
            driveHinge.connectedAnchor = new Vector2(armLength * 0.5f, 0f);
            driveHinge.useMotor = true;
            driveHinge.motor = new JointMotor2D
            {
                motorSpeed = MotorSpeedDegreesPerSecond,
                maxMotorTorque = MaxMotorTorque
            };
        }

        /// <summary>
        /// A motorised arm on a limited hinge, sweeping continuously into its limits. This is the
        /// rig that produced launching bugs in the previous stack, so the checkpoint runs it live
        /// alongside the play-mode test rather than trusting the test alone.
        /// </summary>
        private static void AttachHingedArm(Transform root, Rigidbody2D chassisBody)
        {
            Vector2 chassisOrigin = chassisBody.transform.localPosition;
            // Mounted ahead of the tray, not above it: an arm sweeping through the cargo bay
            // just destroys the cargo and tells us nothing about the hinge.
            var anchorOnChassis = new Vector2(1.5f, 0.35f);

            GameObject arm = SpikeVisuals.CreateBox(
                "HingedArm", root, chassisOrigin + anchorOnChassis + new Vector2(0.9f, 0f),
                new Vector2(1.8f, 0.22f), 0f, PartColor, RigidbodyType2D.Dynamic, 0.5f);

            var hinge = arm.AddComponent<HingeJoint2D>();
            hinge.connectedBody = chassisBody;
            hinge.autoConfigureConnectedAnchor = false;
            hinge.anchor = new Vector2(-0.9f, 0f);
            hinge.connectedAnchor = anchorOnChassis;
            hinge.useLimits = true;
            hinge.limits = new JointAngleLimits2D { min = -35f, max = 55f };
            hinge.useMotor = true;
            hinge.motor = new JointMotor2D { motorSpeed = 120f, maxMotorTorque = 260f };

            arm.AddComponent<SpikeArmSweeper>().Initialise(hinge);
        }

        /// <summary>
        /// Four beams welded nose-to-tail, cantilevered off the front of the chassis. The visible
        /// question is whether the chain reads as one rigid object or as rubber; the measured
        /// answer is 1.3 degrees of droop (docs/ISSUES.md).
        /// </summary>
        private static void AttachWeldedBeamChain(Transform root, Rigidbody2D chassisBody)
        {
            const int BeamCount = 4;
            const float BeamLength = 0.9f;

            Vector2 chassisOrigin = chassisBody.transform.localPosition;
            // Forward of the tray wall, at floor height, so the chain cantilevers ahead of the
            // machine where its droop is actually visible.
            var attachOnChassis = new Vector2(1.45f, 0f);

            Rigidbody2D previous = chassisBody;
            for (int i = 0; i < BeamCount; i++)
            {
                float centreX = attachOnChassis.x + (i * BeamLength) + (BeamLength * 0.5f);
                GameObject beam = SpikeVisuals.CreateBox(
                    $"Beam{i}", root, chassisOrigin + new Vector2(centreX, attachOnChassis.y),
                    new Vector2(BeamLength, 0.18f), 0f, PartColor, RigidbodyType2D.Dynamic, 0.25f);

                var weld = beam.AddComponent<FixedJoint2D>();
                weld.connectedBody = previous;
                weld.autoConfigureConnectedAnchor = false;
                weld.anchor = new Vector2(-BeamLength * 0.5f, 0f);
                weld.connectedAnchor = i == 0
                    ? attachOnChassis
                    : new Vector2(BeamLength * 0.5f, 0f);
                // frequency is deliberately left at its default. 0 means rigid; setting it
                // would make the weld softer, not stiffer (docs/ISSUES.md L1).

                previous = beam.GetComponent<Rigidbody2D>();
            }
        }

        private static SpikeCargo CreateCargo(Transform root, Vector2 position)
        {
            GameObject cargo = SpikeVisuals.CreateBox(
                "Cargo", root, position, new Vector2(1.1f, 1.1f), 0f,
                CargoColor, RigidbodyType2D.Dynamic, 0.8f);
            return cargo.AddComponent<SpikeCargo>();
        }
    }
}
