using System.Collections.Generic;
using Contraption.Domain.Blueprints;
using Contraption.Domain.Editing;
using NUnit.Framework;

namespace Contraption.Tests.EditMode
{
    /// <summary>
    /// Milestone 6: the editor. Pure domain, so the whole build model is testable without a scene,
    /// input, or a catalog asset.
    /// </summary>
    public sealed class ContraptionEditorTests
    {
        private const float Tolerance = 0.0001f;

        private ContraptionEditor _editor = null!;
        private PartId _chassisId;

        [SetUp]
        public void SetUp()
        {
            _chassisId = new PartId("chassis");
            ContraptionBlueprint blueprint = ContraptionBlueprint.Create(
                "level-01",
                new[] { new PlacedPart(_chassisId, PartType.Chassis, EditorPosition.Origin, PartRotation.None) });
            _editor = new ContraptionEditor(blueprint, Definitions());
        }

        // -----------------------------------------------------------------------------------
        // Placement is connection (D11).
        // -----------------------------------------------------------------------------------

        [Test]
        public void PlacePart_OnAFreeHole_AddsBothThePartAndItsAttachment()
        {
            EditResult result = _editor.PlacePart(_chassisId, new HoleId("hole-left"), PartType.PoweredWheel);

            Assert.That(result.Accepted, Is.True, result.RejectionReason);
            Assert.That(result.Blueprint.Parts.Count, Is.EqualTo(2));
            Assert.That(result.Blueprint.Attachments.Count, Is.EqualTo(1));
        }

        [Test]
        public void PlacePart_Always_PositionsTheChildAtTheParentHole()
        {
            // The wheel's mount hole is at its own origin, so it lands exactly on the chassis hole.
            EditResult result = _editor.PlacePart(_chassisId, new HoleId("hole-left"), PartType.PoweredWheel);

            PlacedPart wheel = result.Blueprint.Parts[1];
            Assert.That(wheel.Position.X, Is.EqualTo(-1f).Within(Tolerance));
            Assert.That(wheel.Position.Y, Is.EqualTo(-0.5f).Within(Tolerance));
        }

        [Test]
        public void PlacePart_APartMountedByAnOffsetHole_IsShiftedSoTheHolesMeet()
        {
            // A beam mounts by end-a at (-0.45, 0), so its centre must sit 0.45 to the right of
            // the chassis hole for the two to coincide.
            EditResult result = _editor.PlacePart(_chassisId, new HoleId("hole-right"), PartType.Beam);

            PlacedPart beam = result.Blueprint.Parts[1];
            Assert.That(beam.Position.X, Is.EqualTo(1f + 0.45f).Within(Tolerance));
            Assert.That(beam.Position.Y, Is.EqualTo(-0.5f).Within(Tolerance));
        }

        [Test]
        public void PlacePart_OnAnOccupiedHole_IsRejectedWithAReason()
        {
            _editor.PlacePart(_chassisId, new HoleId("hole-left"), PartType.PoweredWheel);

            EditResult result = _editor.PlacePart(_chassisId, new HoleId("hole-left"), PartType.Wheel);

            Assert.That(result.Accepted, Is.False);
            Assert.That(result.RejectionReason, Is.Not.Empty);
        }

        [Test]
        public void PlacePart_OnAHoleThatDoesNotExist_IsRejectedWithAReason()
        {
            EditResult result = _editor.PlacePart(_chassisId, new HoleId("nonsense"), PartType.Wheel);

            Assert.That(result.Accepted, Is.False);
            Assert.That(result.RejectionReason, Does.Contain("attachment point"));
        }

        [Test]
        public void PlacePart_OnAPartThatIsGone_IsRejectedWithAReason()
        {
            EditResult result = _editor.PlacePart(new PartId("ghost"), new HoleId("hole-left"), PartType.Wheel);

            Assert.That(result.Accepted, Is.False);
            Assert.That(result.RejectionReason, Is.Not.Empty);
        }

        // -----------------------------------------------------------------------------------
        // Rotation pivots about the mount hole, and carries descendants.
        // -----------------------------------------------------------------------------------

        [Test]
        public void RotatePart_OneStep_TurnsByTheSnapIncrement()
        {
            PartId beamId = Place(PartType.Beam, "hole-right");

            EditResult result = _editor.RotatePart(beamId);

            Assert.That(FindPart(result.Blueprint, beamId).Rotation.Degrees,
                Is.EqualTo(ContraptionEditor.RotationStepDegrees));
        }

        [Test]
        public void RotatePart_Always_KeepsTheMountHoleOnTheParentHole()
        {
            // This is the property that makes rotation feel like turning a bolted-on part rather
            // than spinning it about its own centre and floating free.
            PartId beamId = Place(PartType.Beam, "hole-right");

            EditResult result = _editor.RotatePart(beamId, steps: 3); // 90 degrees

            PlacedPart beam = FindPart(result.Blueprint, beamId);
            // end-a rotated by 90 degrees is (0, -0.45); the centre must sit that far the other
            // way from the chassis hole at (1, -0.5).
            Assert.That(beam.Position.X, Is.EqualTo(1f).Within(Tolerance));
            Assert.That(beam.Position.Y, Is.EqualTo(-0.5f + 0.45f).Within(Tolerance));
        }

        [Test]
        public void RotatePart_AParentWithChildren_MovesTheChildrenToo()
        {
            PartId beamId = Place(PartType.Beam, "hole-right");
            PartId wheelId = Place(PartType.Wheel, "end-b", beamId);
            EditorPosition before = FindPart(_editor.Blueprint, wheelId).Position;

            EditResult result = _editor.RotatePart(beamId, steps: 3);

            EditorPosition after = FindPart(result.Blueprint, wheelId).Position;
            Assert.That(after, Is.Not.EqualTo(before), "A part on a rotated parent must move with it.");
        }

        [Test]
        public void RotatePart_TheChassis_IsRejected()
        {
            EditResult result = _editor.RotatePart(_chassisId);

            Assert.That(result.Accepted, Is.False);
        }

        [Test]
        public void RotatePart_FullTurn_ReturnsToWhereItStarted()
        {
            PartId beamId = Place(PartType.Beam, "hole-right");
            EditorPosition start = FindPart(_editor.Blueprint, beamId).Position;

            EditResult result = _editor.RotatePart(beamId, steps: 360 / ContraptionEditor.RotationStepDegrees);

            PlacedPart beam = FindPart(result.Blueprint, beamId);
            Assert.That(beam.Rotation.Degrees, Is.EqualTo(0));
            Assert.That(beam.Position.X, Is.EqualTo(start.X).Within(Tolerance));
            Assert.That(beam.Position.Y, Is.EqualTo(start.Y).Within(Tolerance));
        }

        // -----------------------------------------------------------------------------------
        // Removal takes the subtree.
        // -----------------------------------------------------------------------------------

        [Test]
        public void RemovePart_WithPartsHangingOffIt_RemovesThemToo()
        {
            PartId beamId = Place(PartType.Beam, "hole-right");
            Place(PartType.Wheel, "end-b", beamId);

            EditResult result = _editor.RemovePart(beamId);

            // Leaving the wheel behind would strand a part attached to nothing.
            Assert.That(result.Blueprint.Parts.Count, Is.EqualTo(1));
            Assert.That(result.Blueprint.Attachments, Is.Empty);
        }

        [Test]
        public void RemovePart_TheChassis_IsRejected()
        {
            EditResult result = _editor.RemovePart(_chassisId);

            Assert.That(result.Accepted, Is.False);
        }

        [Test]
        public void RemovePart_ThenPlacingThereAgain_IsAllowed()
        {
            PartId wheelId = Place(PartType.PoweredWheel, "hole-left");
            _editor.RemovePart(wheelId);

            EditResult result = _editor.PlacePart(_chassisId, new HoleId("hole-left"), PartType.Wheel);

            Assert.That(result.Accepted, Is.True, result.RejectionReason);
        }

        // -----------------------------------------------------------------------------------

        [Test]
        public void FreeHoles_Always_ExcludesOccupiedOnes()
        {
            Assert.That(_editor.FreeHoles(_chassisId).Count, Is.EqualTo(2));

            _editor.PlacePart(_chassisId, new HoleId("hole-left"), PartType.Wheel);

            IReadOnlyList<AttachmentHole> free = _editor.FreeHoles(_chassisId);
            Assert.That(free.Count, Is.EqualTo(1));
            Assert.That(free[0].Id.Value, Is.EqualTo("hole-right"));
        }

        [Test]
        public void FreeHoles_APartsOwnMountHole_IsNotFree()
        {
            // The wheel's axle is how it is bolted to the chassis. Reporting it as free would
            // both draw a phantom target and let two parts share one point.
            PartId wheelId = Place(PartType.PoweredWheel, "hole-left");

            Assert.That(_editor.FreeHoles(wheelId), Is.Empty);
        }

        [Test]
        public void PlacePart_OnAPartsOwnMountHole_IsRejected()
        {
            PartId wheelId = Place(PartType.PoweredWheel, "hole-left");

            EditResult result = _editor.PlacePart(wheelId, new HoleId("axle"), PartType.Beam);

            Assert.That(result.Accepted, Is.False);
        }

        [Test]
        public void FreeHoles_AMultiHolePart_ReportsOnlyItsUnusedEnds()
        {
            PartId beamId = Place(PartType.Beam, "hole-right");

            IReadOnlyList<AttachmentHole> free = _editor.FreeHoles(beamId);

            Assert.That(free.Count, Is.EqualTo(1));
            Assert.That(free[0].Id.Value, Is.EqualTo("end-b"));
        }

        [Test]
        public void ConfigurePart_Always_LeavesPositionAndRotationAlone()
        {
            PartId wheelId = Place(PartType.PoweredWheel, "hole-left");
            PlacedPart before = FindPart(_editor.Blueprint, wheelId);

            EditResult result = _editor.ConfigurePart(wheelId, PartConfiguration.Empty.With("motorSpeed", 200f));

            PlacedPart after = FindPart(result.Blueprint, wheelId);
            Assert.That(after.Position, Is.EqualTo(before.Position));
            Assert.That(after.Rotation, Is.EqualTo(before.Rotation));
            Assert.That(after.Configuration.Values["motorSpeed"], Is.EqualTo(200f));
        }

        [Test]
        public void AnyEdit_Always_LeavesThePreviousBlueprintUntouched()
        {
            ContraptionBlueprint before = _editor.Blueprint;

            _editor.PlacePart(_chassisId, new HoleId("hole-left"), PartType.Wheel);

            Assert.That(before.Parts.Count, Is.EqualTo(1), "Edits must produce a new blueprint, not mutate one.");
        }

        [Test]
        public void BlueprintChanged_OnAcceptedEdit_IsRaisedOnce()
        {
            int raised = 0;
            _editor.BlueprintChanged += _ => raised++;

            _editor.PlacePart(_chassisId, new HoleId("hole-left"), PartType.Wheel);
            _editor.PlacePart(_chassisId, new HoleId("hole-left"), PartType.Wheel); // rejected

            Assert.That(raised, Is.EqualTo(1), "A rejected edit must not announce a change.");
        }

        // -----------------------------------------------------------------------------------

        private PartId Place(PartType type, string holeId) => Place(type, holeId, _chassisId);

        private PartId Place(PartType type, string holeId, PartId parentId)
        {
            EditResult result = _editor.PlacePart(parentId, new HoleId(holeId), type);
            Assert.That(result.Accepted, Is.True, result.RejectionReason);
            return result.Blueprint.Parts[result.Blueprint.Parts.Count - 1].Id;
        }

        private static PlacedPart FindPart(ContraptionBlueprint blueprint, PartId id)
        {
            foreach (PlacedPart part in blueprint.Parts)
            {
                if (part.Id == id)
                {
                    return part;
                }
            }

            Assert.Fail($"'{id}' is not in the blueprint.");
            return null!;
        }

        /// <summary>
        /// A deliberately small stand-in for the catalog: round numbers make the placement
        /// arithmetic readable in the assertions above.
        /// </summary>
        private static IReadOnlyDictionary<PartType, PartDefinition> Definitions()
        {
            return new Dictionary<PartType, PartDefinition>
            {
                [PartType.Chassis] = new PartDefinition(
                    PartType.Chassis, "Chassis", 2f, 0,
                    new[]
                    {
                        new AttachmentHole(new HoleId("hole-left"), new EditorPosition(-1f, -0.5f)),
                        new AttachmentHole(new HoleId("hole-right"), new EditorPosition(1f, -0.5f))
                    }),
                [PartType.Wheel] = new PartDefinition(
                    PartType.Wheel, "Wheel", 0.5f, 1,
                    new[] { new AttachmentHole(new HoleId("axle"), EditorPosition.Origin) }),
                [PartType.PoweredWheel] = new PartDefinition(
                    PartType.PoweredWheel, "Powered Wheel", 0.5f, 2,
                    new[] { new AttachmentHole(new HoleId("axle"), EditorPosition.Origin) }),
                [PartType.Beam] = new PartDefinition(
                    PartType.Beam, "Beam", 0.25f, 1,
                    new[]
                    {
                        new AttachmentHole(new HoleId("end-a"), new EditorPosition(-0.45f, 0f)),
                        new AttachmentHole(new HoleId("end-b"), new EditorPosition(0.45f, 0f))
                    })
            };
        }
    }
}
