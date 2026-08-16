using Contraption.Domain.Blueprints;
using Contraption.Runtime.Catalog;
using UnityEngine;

namespace Contraption.Runtime.Simulation
{
    /// <summary>
    /// Realises one <see cref="Attachment"/> as a Unity 2D joint.
    ///
    /// The blueprint records the player's *intent* to connect two parts; choosing the joint that
    /// honours it is this class's job alone (`ARCHITECTURE.md` §10). The mapping is driven by the
    /// attached part's type — connecting a Hinge produces a hinge, connecting a powered wheel
    /// produces a motorised one.
    ///
    /// Every choice below is constrained by the Milestone 1 measurements, and two of them are the
    /// opposite of the obvious implementation. See `docs/ISSUES.md` L1 and L2.
    /// </summary>
    public sealed class JointBuilder
    {
        private readonly PartCatalog _catalog;

        public JointBuilder(PartCatalog catalog)
        {
            _catalog = catalog;
        }

        /// <summary>
        /// Creates the joint on the attached body, connected back to the anchor body.
        /// Returns null if either part is missing or the type has no catalog entry.
        /// </summary>
        public Joint2D Build(Rigidbody2D attachedBody, PartType attachedType, Rigidbody2D anchorBody)
        {
            if (attachedBody == null || anchorBody == null)
            {
                return null;
            }

            if (!_catalog.TryGetDefinition(attachedType, out PartDefinitionAsset definition))
            {
                return null;
            }

            switch (attachedType)
            {
                case PartType.PoweredWheel:
                    return BuildPoweredHinge(attachedBody, anchorBody, definition);
                case PartType.Wheel:
                    return BuildFreeHinge(attachedBody, anchorBody);
                case PartType.Hinge:
                    return BuildLimitedHinge(attachedBody, anchorBody, definition);
                case PartType.Spring:
                    return BuildSpring(attachedBody, anchorBody, definition);
                default:
                    return BuildWeld(attachedBody, anchorBody);
            }
        }

        /// <summary>
        /// A rigid connection. <c>frequency</c> is deliberately left at its default of 0, which
        /// means *completely rigid*; setting it to any finite value makes the weld softer, not
        /// stiffer, and non-monotonically so (`docs/ISSUES.md` L1). Weld stiffness comes from the
        /// project's solver iteration count, not from this joint.
        /// </summary>
        private static FixedJoint2D BuildWeld(Rigidbody2D attachedBody, Rigidbody2D anchorBody)
        {
            FixedJoint2D weld = attachedBody.gameObject.AddComponent<FixedJoint2D>();
            Connect(weld, attachedBody, anchorBody);
            return weld;
        }

        private static HingeJoint2D BuildFreeHinge(Rigidbody2D attachedBody, Rigidbody2D anchorBody)
        {
            HingeJoint2D hinge = attachedBody.gameObject.AddComponent<HingeJoint2D>();
            Connect(hinge, attachedBody, anchorBody);
            return hinge;
        }

        private static HingeJoint2D BuildLimitedHinge(
            Rigidbody2D attachedBody,
            Rigidbody2D anchorBody,
            PartDefinitionAsset definition)
        {
            HingeJoint2D hinge = attachedBody.gameObject.AddComponent<HingeJoint2D>();
            Connect(hinge, attachedBody, anchorBody);
            hinge.useLimits = true;
            hinge.limits = new JointAngleLimits2D
            {
                min = definition.HingeLimitMinDegrees,
                max = definition.HingeLimitMaxDegrees
            };
            return hinge;
        }

        private static HingeJoint2D BuildPoweredHinge(
            Rigidbody2D attachedBody,
            Rigidbody2D anchorBody,
            PartDefinitionAsset definition)
        {
            HingeJoint2D hinge = attachedBody.gameObject.AddComponent<HingeJoint2D>();
            Connect(hinge, attachedBody, anchorBody);
            hinge.useMotor = true;
            // A POSITIVE motorSpeed drives +X. The intuitive reading is backwards, and getting it
            // wrong produces a perfectly healthy machine driving the wrong way (docs/ISSUES.md L2).
            hinge.motor = new JointMotor2D
            {
                motorSpeed = definition.MotorSpeedDegreesPerSecond,
                maxMotorTorque = definition.MaxMotorTorque
            };
            return hinge;
        }

        /// <summary>
        /// Unlike <see cref="FixedJoint2D"/>, <see cref="SpringJoint2D.frequency"/> really is a
        /// spring parameter and is meant to be set.
        /// </summary>
        private static SpringJoint2D BuildSpring(
            Rigidbody2D attachedBody,
            Rigidbody2D anchorBody,
            PartDefinitionAsset definition)
        {
            SpringJoint2D spring = attachedBody.gameObject.AddComponent<SpringJoint2D>();
            spring.connectedBody = anchorBody;
            spring.autoConfigureConnectedAnchor = false;
            spring.autoConfigureDistance = false;
            spring.anchor = Vector2.zero;
            spring.connectedAnchor = anchorBody.transform.InverseTransformPoint(attachedBody.transform.position);
            spring.distance = Vector2.Distance(attachedBody.transform.position, anchorBody.transform.position);
            spring.frequency = definition.SpringFrequency;
            spring.dampingRatio = definition.SpringDampingRatio;
            return spring;
        }

        /// <summary>
        /// Anchors the joint at the attached part's own origin, pinned to the corresponding point
        /// on the anchor body.
        ///
        /// Note this uses the parts' *placed positions*, not hole geometry: the catalog records
        /// hole ids but not where those holes sit on a part. That is enough to build a correct
        /// machine from a blueprint, and hole positions become necessary in Milestone 6 when the
        /// player places parts onto specific holes. Recorded in `docs/TASKS.md`.
        /// </summary>
        private static void Connect(AnchoredJoint2D joint, Rigidbody2D attachedBody, Rigidbody2D anchorBody)
        {
            joint.connectedBody = anchorBody;
            joint.autoConfigureConnectedAnchor = false;
            joint.anchor = Vector2.zero;
            joint.connectedAnchor = anchorBody.transform.InverseTransformPoint(attachedBody.transform.position);
        }
    }
}
