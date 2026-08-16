using System;
using System.Collections.Generic;
using Contraption.Domain.Blueprints;
using NUnit.Framework;

namespace Contraption.Tests.EditMode
{
    /// <summary>
    /// Milestone 2: the immutability and value-equality guarantees the rest of the architecture
    /// leans on. If these break, "every edit returns a new blueprint" and "reset rebuilds from the
    /// unchanged blueprint" stop being true.
    /// </summary>
    public sealed class BlueprintModelTests
    {
        [Test]
        public void Blueprint_TwoIdenticalBlueprints_AreEqual()
        {
            Assert.That(Sample(), Is.EqualTo(Sample()));
        }

        [Test]
        public void Blueprint_DifferingInOnePartRotation_AreNotEqual()
        {
            ContraptionBlueprint rotated = ContraptionBlueprint.Create(
                "level-01",
                new[] { new PlacedPart(new PartId("p1"), PartType.Beam, EditorPosition.Origin, PartRotation.FromDegrees(90)) });

            Assert.That(rotated, Is.Not.EqualTo(Sample()));
        }

        [Test]
        public void Blueprint_MutatingTheSourceList_DoesNotAffectTheBlueprint()
        {
            var parts = new List<PlacedPart>
            {
                new PlacedPart(new PartId("p1"), PartType.Beam, EditorPosition.Origin, PartRotation.None)
            };
            ContraptionBlueprint blueprint = ContraptionBlueprint.Create("level-01", parts);

            parts.Add(new PlacedPart(new PartId("p2"), PartType.Wheel, EditorPosition.Origin, PartRotation.None));

            // Count via the interface, not Has.Count: the backing store is an array, which
            // exposes Length, and NUnit reflects on the runtime type.
            Assert.That(
                blueprint.Parts.Count,
                Is.EqualTo(1),
                "The blueprint must defensively copy its collections.");
        }

        [Test]
        public void PartConfiguration_With_LeavesTheOriginalUntouched()
        {
            PartConfiguration original = PartConfiguration.Empty.With("a", 1f);

            PartConfiguration extended = original.With("b", 2f);

            Assert.That(original.Values, Has.Count.EqualTo(1));
            Assert.That(extended.Values, Has.Count.EqualTo(2));
        }

        [Test]
        public void PartConfiguration_SameEntriesInAnyOrder_AreEqual()
        {
            PartConfiguration first = PartConfiguration.Empty.With("a", 1f).With("b", 2f);
            PartConfiguration second = PartConfiguration.Empty.With("b", 2f).With("a", 1f);

            Assert.That(first, Is.EqualTo(second));
            Assert.That(first.GetHashCode(), Is.EqualTo(second.GetHashCode()));
        }

        [Test]
        public void PartRotation_EquivalentAngles_AreEqual()
        {
            Assert.That(PartRotation.FromDegrees(370), Is.EqualTo(PartRotation.FromDegrees(10)));
            Assert.That(PartRotation.FromDegrees(-90), Is.EqualTo(PartRotation.FromDegrees(270)));
        }

        [Test]
        public void PartId_EmptyValue_IsRejected()
        {
            Assert.Throws<ArgumentException>(() => new PartId(" "));
        }

        [Test]
        public void Attachment_ConnectingAPartToItself_IsRejected()
        {
            Assert.Throws<ArgumentException>(() => new Attachment(
                new AttachmentId("a1"),
                new PartId("p1"),
                new HoleId("h1"),
                new PartId("p1"),
                new HoleId("h2")));
        }

        [Test]
        public void Blueprint_WithoutALevelId_IsRejected()
        {
            Assert.Throws<ArgumentException>(() => ContraptionBlueprint.Create(""));
        }

        [Test]
        public void RunResult_FailedWithoutAReason_IsRejected()
        {
            // Silent rejection was a recorded failure of the previous project (docs/ISSUES.md).
            Assert.Throws<ArgumentException>(
                () => Domain.Flow.RunResult.Failed("", cargoHealthRemaining: 0f, elapsedSeconds: 1f, partsUsed: 3));
        }

        [Test]
        public void RunResult_Completed_HasNoFailureReason()
        {
            Domain.Flow.RunResult result = Domain.Flow.RunResult.Completed(
                cargoHealthRemaining: 89f, elapsedSeconds: 22.5f, partsUsed: 6);

            Assert.That(result.Outcome, Is.EqualTo(Domain.Flow.RunOutcome.Completed));
            Assert.That(result.FailureReason, Is.Null);
        }

        private static ContraptionBlueprint Sample()
        {
            return ContraptionBlueprint.Create(
                "level-01",
                new[] { new PlacedPart(new PartId("p1"), PartType.Beam, EditorPosition.Origin, PartRotation.None) });
        }
    }
}
