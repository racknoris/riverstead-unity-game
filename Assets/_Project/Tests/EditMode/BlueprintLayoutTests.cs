using System.Collections.Generic;
using Contraption.Domain.Blueprints;
using Contraption.Domain.Editing;
using NUnit.Framework;

namespace Contraption.Tests.EditMode
{
    /// <summary>
    /// The layout pass that makes `PlacedPart.Position` a cache of a derived value rather than an
    /// independent claim. See `docs/TASKS.md` D12.
    /// </summary>
    public sealed class BlueprintLayoutTests
    {
        private const float Tolerance = 0.0001f;

        [Test]
        public void Normalise_APositionThatDisagreesWithItsHole_IsCorrected()
        {
            // This is the Milestone 5 defect, reproduced: the wheel claims to be somewhere its
            // own mounting hole is not. Physics resolved that disagreement by yanking the machine
            // apart on the first step. Normalising makes the stored value follow the graph.
            ContraptionBlueprint wrong = Machine(wheelPosition: new EditorPosition(-1f, 5f));

            ContraptionBlueprint fixedUp = BlueprintLayout.Normalise(wrong, Definitions());

            PlacedPart wheel = fixedUp.Parts[1];
            Assert.That(wheel.Position.X, Is.EqualTo(-1f).Within(Tolerance));
            Assert.That(wheel.Position.Y, Is.EqualTo(-0.5f).Within(Tolerance));
        }

        [Test]
        public void Normalise_AnAlreadyCorrectBlueprint_ChangesNothing()
        {
            ContraptionBlueprint correct = Machine(wheelPosition: new EditorPosition(-1f, -0.5f));

            Assert.That(BlueprintLayout.Normalise(correct, Definitions()), Is.EqualTo(correct));
        }

        [Test]
        public void Normalise_Always_LeavesRootsWhereTheyWereAuthored()
        {
            // A machine has to sit somewhere and nothing derives that.
            ContraptionBlueprint machine = Machine(
                wheelPosition: new EditorPosition(-1f, 5f), chassisPosition: new EditorPosition(3f, 2f));

            ContraptionBlueprint normalised = BlueprintLayout.Normalise(machine, Definitions());

            Assert.That(normalised.Parts[0].Position, Is.EqualTo(new EditorPosition(3f, 2f)));
            // ...and the child follows the root rather than its own stored value.
            Assert.That(normalised.Parts[1].Position.X, Is.EqualTo(2f).Within(Tolerance));
            Assert.That(normalised.Parts[1].Position.Y, Is.EqualTo(1.5f).Within(Tolerance));
        }

        [Test]
        public void Normalise_ABlueprintWithNoAttachments_IsUnchanged()
        {
            ContraptionBlueprint lone = ContraptionBlueprint.Create(
                "level-01",
                new[] { new PlacedPart(new PartId("chassis"), PartType.Chassis, new EditorPosition(1f, 2f), PartRotation.None) });

            Assert.That(BlueprintLayout.Normalise(lone, Definitions()), Is.EqualTo(lone));
        }

        [Test]
        public void Normalise_AnUnknownHole_LeavesThePartAloneRatherThanThrowing()
        {
            // A blueprint naming a hole the catalog lacks is a validation problem for Milestone 7.
            // Layout should not be the thing that crashes on it.
            var parts = new[]
            {
                new PlacedPart(new PartId("chassis"), PartType.Chassis, EditorPosition.Origin, PartRotation.None),
                new PlacedPart(new PartId("wheel"), PartType.Wheel, new EditorPosition(9f, 9f), PartRotation.None)
            };
            var attachments = new[]
            {
                new Attachment(new AttachmentId("a1"), new PartId("chassis"), new HoleId("nonsense"),
                    new PartId("wheel"), new HoleId("axle"))
            };
            ContraptionBlueprint blueprint = ContraptionBlueprint.Create("level-01", parts, attachments);

            ContraptionBlueprint normalised = BlueprintLayout.Normalise(blueprint, Definitions());

            Assert.That(normalised.Parts[1].Position, Is.EqualTo(new EditorPosition(9f, 9f)));
        }

        private static ContraptionBlueprint Machine(EditorPosition wheelPosition, EditorPosition? chassisPosition = null)
        {
            var parts = new[]
            {
                new PlacedPart(
                    new PartId("chassis"), PartType.Chassis,
                    chassisPosition ?? EditorPosition.Origin, PartRotation.None),
                new PlacedPart(new PartId("wheel"), PartType.Wheel, wheelPosition, PartRotation.None)
            };
            var attachments = new[]
            {
                new Attachment(new AttachmentId("a1"), new PartId("chassis"), new HoleId("hole-left"),
                    new PartId("wheel"), new HoleId("axle"))
            };

            return ContraptionBlueprint.Create("level-01", parts, attachments);
        }

        private static IReadOnlyDictionary<PartType, PartDefinition> Definitions()
        {
            return new Dictionary<PartType, PartDefinition>
            {
                [PartType.Chassis] = new PartDefinition(
                    PartType.Chassis, "Chassis", 2f, 0,
                    new[] { new AttachmentHole(new HoleId("hole-left"), new EditorPosition(-1f, -0.5f)) }),
                [PartType.Wheel] = new PartDefinition(
                    PartType.Wheel, "Wheel", 0.5f, 1,
                    new[] { new AttachmentHole(new HoleId("axle"), EditorPosition.Origin) })
            };
        }
    }
}
