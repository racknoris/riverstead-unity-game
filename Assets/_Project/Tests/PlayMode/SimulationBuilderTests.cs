using System.Collections;
using System.Collections.Generic;
using Contraption.Domain.Blueprints;
using Contraption.Runtime.Catalog;
using Contraption.Runtime.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Contraption.Tests.PlayMode
{
    /// <summary>
    /// Milestone 4: a blueprint builds into a running machine, and the build → destroy → rebuild
    /// cycle leaves nothing behind.
    ///
    /// Play mode rather than edit mode because lifetime is the subject: <c>Destroy</c> is deferred
    /// to end of frame, so a leak test that never yields would measure nothing.
    /// </summary>
    public sealed class SimulationBuilderTests
    {
        private const string CatalogPath = "Assets/_Project/Runtime/Catalog/PartCatalog.asset";

        private PartCatalog _catalog;
        private SimulationBuilder _builder;
        private SimulationRoot _root;

        [SetUp]
        public void SetUp()
        {
#if UNITY_EDITOR
            _catalog = AssetDatabase.LoadAssetAtPath<PartCatalog>(CatalogPath);
#endif
            if (_catalog == null)
            {
                Assert.Ignore("The part catalog asset is only reachable from the editor.");
            }

            _builder = new SimulationBuilder(_catalog);
        }

        [TearDown]
        public void TearDown()
        {
            if (_root != null)
            {
                Object.DestroyImmediate(_root.gameObject);
            }
        }

        [Test]
        public void Build_ARoverBlueprint_CreatesABodyForEveryPart()
        {
            ContraptionBlueprint blueprint = Rover();

            _root = _builder.Build(Level(), blueprint);

            Assert.That(_root.BodiesByPartId.Count, Is.EqualTo(blueprint.Parts.Count));
            foreach (PlacedPart part in blueprint.Parts)
            {
                Assert.That(_root.TryGetBody(part.Id, out _), Is.True, $"'{part.Id}' has no body.");
            }
        }

        [Test]
        public void Build_ARoverBlueprint_PutsEverythingUnderOneRoot()
        {
            _root = _builder.Build(Level(), Rover());

            Rigidbody2D[] bodies = Object.FindObjectsByType<Rigidbody2D>(FindObjectsSortMode.None);
            foreach (Rigidbody2D body in bodies)
            {
                Assert.That(
                    body.transform.IsChildOf(_root.transform),
                    Is.True,
                    $"'{body.name}' was built outside the simulation root, so reset would not destroy it.");
            }
        }

        [Test]
        public void Build_ARoverBlueprint_CreatesAJointForEveryAttachment()
        {
            ContraptionBlueprint blueprint = Rover();

            _root = _builder.Build(Level(), blueprint);

            Assert.That(_root.JointCount, Is.EqualTo(blueprint.Attachments.Count));
        }

        [Test]
        public void Build_APoweredWheel_ReceivesAMotorFromTheCatalog()
        {
            _root = _builder.Build(Level(), Rover());
            _root.TryGetBody(new PartId("wheel-rear"), out Rigidbody2D wheel);

            var hinge = wheel.GetComponent<HingeJoint2D>();

            Assert.That(hinge, Is.Not.Null, "A powered wheel should be attached with a hinge.");
            Assert.That(hinge.useMotor, Is.True);
            Assert.That(hinge.motor.motorSpeed, Is.GreaterThan(0f), "Positive motorSpeed drives +X.");
        }

        [Test]
        public void Build_ARigidConnector_UsesAWeldLeftAtItsRigidDefault()
        {
            _root = _builder.Build(Level(), Rover());
            _root.TryGetBody(new PartId("plate"), out Rigidbody2D plate);

            var weld = plate.GetComponent<FixedJoint2D>();

            Assert.That(weld, Is.Not.Null);
            // frequency 0 means completely rigid. A non-zero value here would be a regression
            // against docs/ISSUES.md L1 - it makes welds softer, not stiffer.
            Assert.That(weld.frequency, Is.EqualTo(0f));
        }

        [Test]
        public void Build_AJoint_AnchorsAtTheNamedHoleNotThePartOrigin()
        {
            // The bug this guards: anchoring at the attached part's own position instead of at
            // the hole. Welds are indifferent to it, so it stayed invisible until a hinge was
            // attached and pivoted about the wrong point.
            _root = _builder.Build(Level(), Rover());
            _root.TryGetBody(new PartId("wheel-rear"), out Rigidbody2D wheel);

            var hinge = wheel.GetComponent<HingeJoint2D>();

            // Compared against the catalog rather than a copied number: hole positions are tuning
            // and will move, and a test that hard-codes them breaks for the wrong reason.
            _catalog.TryGetDefinition(PartType.Chassis, out PartDefinitionAsset chassis);
            chassis.ToDomainDefinition().TryGetHole(new HoleId("hole-01"), out AttachmentHole hole);

            Assert.That(hinge.connectedAnchor.x, Is.EqualTo(hole.LocalPosition.X).Within(0.001f));
            Assert.That(hinge.connectedAnchor.y, Is.EqualTo(hole.LocalPosition.Y).Within(0.001f));
            Assert.That(hole.LocalPosition, Is.Not.EqualTo(EditorPosition.Origin),
                "This proves nothing unless the hole is actually offset from the part's origin.");
            Assert.That(hinge.autoConfigureConnectedAnchor, Is.False);
        }

        [Test]
        public void Build_TwoPartsOnDifferentHoles_AnchorAtDifferentPoints()
        {
            _root = _builder.Build(Level(), Rover());
            _root.TryGetBody(new PartId("wheel-rear"), out Rigidbody2D rear);
            _root.TryGetBody(new PartId("wheel-front"), out Rigidbody2D front);

            Vector2 rearAnchor = rear.GetComponent<HingeJoint2D>().connectedAnchor;
            Vector2 frontAnchor = front.GetComponent<HingeJoint2D>().connectedAnchor;

            Assert.That(rearAnchor, Is.Not.EqualTo(frontAnchor), "Distinct holes must anchor distinctly.");
        }

        [Test]
        public void Build_EveryBody_Interpolates()
        {
            _root = _builder.Build(Level(), Rover());

            foreach (KeyValuePair<PartId, Rigidbody2D> entry in _root.BodiesByPartId)
            {
                Assert.That(
                    entry.Value.interpolation,
                    Is.EqualTo(RigidbodyInterpolation2D.Interpolate),
                    $"'{entry.Key}' would visibly stutter when moving fast (docs/ISSUES.md L3).");
            }
        }

        [UnityTest]
        public IEnumerator BuildAndDestroy_RepeatedRebuilds_LeaveNothingBehind()
        {
            const int Rebuilds = 5;
            LevelDefinition level = Level();
            ContraptionBlueprint blueprint = Rover();

            yield return null;
            int baselineBodies = CountBodies();
            int baselineJoints = CountJoints();

            for (int i = 0; i < Rebuilds; i++)
            {
                SimulationRoot root = _builder.Build(level, blueprint);
                yield return new WaitForFixedUpdate();
                root.DestroySimulation();
                // Destroy is deferred to end of frame; without this the next iteration would
                // measure objects that are already doomed.
                yield return null;
            }

            yield return null;

            Assert.That(CountBodies(), Is.EqualTo(baselineBodies), "Rebuilding leaked rigidbodies.");
            Assert.That(CountJoints(), Is.EqualTo(baselineJoints), "Rebuilding leaked joints.");
        }

        [UnityTest]
        public IEnumerator Build_AfterSimulating_LeavesTheBlueprintUntouched()
        {
            ContraptionBlueprint blueprint = Rover();
            ContraptionBlueprint pristine = Rover();

            _root = _builder.Build(Level(), blueprint);
            for (int i = 0; i < 25; i++)
            {
                yield return new WaitForFixedUpdate();
            }

            // The machine has moved and fallen; the blueprint must not have noticed.
            // This is the hard constraint from CLAUDE.md: no per-frame physics state ever
            // reaches domain state.
            Assert.That(blueprint, Is.EqualTo(pristine));
        }

        private static int CountBodies() => Object.FindObjectsByType<Rigidbody2D>(FindObjectsSortMode.None).Length;

        private static int CountJoints() => Object.FindObjectsByType<Joint2D>(FindObjectsSortMode.None).Length;

        private static LevelDefinition Level() =>
            new LevelDefinition("level-01", "Test Course", timeLimitSeconds: 75f, maxParts: 12);

        /// <summary>
        /// A chassis with two powered wheels and a welded plate — the smallest blueprint that
        /// exercises a motor, a weld, and multiple joints at once.
        /// </summary>
        private static ContraptionBlueprint Rover()
        {
            var parts = new[]
            {
                new PlacedPart(new PartId("chassis"), PartType.Chassis, EditorPosition.Origin, PartRotation.None),
                new PlacedPart(new PartId("wheel-rear"), PartType.PoweredWheel, new EditorPosition(-1.15f, -0.62f), PartRotation.None),
                new PlacedPart(new PartId("wheel-front"), PartType.PoweredWheel, new EditorPosition(1.15f, -0.62f), PartRotation.None),
                new PlacedPart(new PartId("plate"), PartType.ProtectivePlate, new EditorPosition(0f, 0.6f), PartRotation.None)
            };

            var attachments = new[]
            {
                new Attachment(new AttachmentId("a-rear"), new PartId("chassis"), new HoleId("hole-01"), new PartId("wheel-rear"), new HoleId("axle")),
                new Attachment(new AttachmentId("a-front"), new PartId("chassis"), new HoleId("hole-02"), new PartId("wheel-front"), new HoleId("axle")),
                new Attachment(new AttachmentId("a-plate"), new PartId("chassis"), new HoleId("hole-03"), new PartId("plate"), new HoleId("mount"))
            };

            return ContraptionBlueprint.Create("level-01", parts, attachments);
        }
    }
}
