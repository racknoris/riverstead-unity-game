using System.Collections.Generic;
using Contraption.Domain.Blueprints;
using Contraption.Domain.Editing;
using NUnit.Framework;

namespace Contraption.Tests.EditMode
{
    /// <summary>
    /// Milestone 7. Two things are being checked: that invalid machines are refused, and that
    /// every refusal says something a player could act on.
    /// </summary>
    public sealed class PlacementRulesTests
    {
        private PartId _chassisId;

        [SetUp]
        public void SetUp() => _chassisId = new PartId("chassis");

        // -----------------------------------------------------------------------------------
        // Part budget.
        // -----------------------------------------------------------------------------------

        [Test]
        public void PlacePart_UpToTheLimit_IsAllowed()
        {
            ContraptionEditor editor = Editor(maxParts: 3);

            for (int i = 0; i < 3; i++)
            {
                EditResult result = editor.PlacePart(_chassisId, new HoleId($"hole-{i}"), PartType.Wheel);
                Assert.That(result.Accepted, Is.True, result.RejectionReason);
            }

            Assert.That(editor.PartsUsed, Is.EqualTo(3));
        }

        [Test]
        public void PlacePart_PastTheLimit_IsRejectedWithAReasonNamingTheLimit()
        {
            ContraptionEditor editor = Editor(maxParts: 2);
            editor.PlacePart(_chassisId, new HoleId("hole-0"), PartType.Wheel);
            editor.PlacePart(_chassisId, new HoleId("hole-1"), PartType.Wheel);

            EditResult result = editor.PlacePart(_chassisId, new HoleId("hole-2"), PartType.Wheel);

            Assert.That(result.Accepted, Is.False);
            Assert.That(result.RejectionReason, Does.Contain("2"));
        }

        [Test]
        public void PartsUsed_Always_ExcludesTheChassis()
        {
            // The chassis is what you build on, not something you spend.
            ContraptionEditor editor = Editor(maxParts: 12);

            Assert.That(editor.PartsUsed, Is.EqualTo(0));
        }

        [Test]
        public void RemovePart_WhenOverTheLimit_IsStillAllowed()
        {
            // A machine can only get over the limit by the limit changing beneath it, but if it
            // ever does, refusing removal would leave the player with no way back.
            ContraptionEditor editor = Editor(maxParts: 1);
            editor.PlacePart(_chassisId, new HoleId("hole-0"), PartType.Wheel);
            PartId wheelId = editor.Blueprint.Parts[1].Id;

            EditResult result = editor.RemovePart(wheelId);

            Assert.That(result.Accepted, Is.True, result.RejectionReason);
        }

        // -----------------------------------------------------------------------------------
        // Overlap.
        // -----------------------------------------------------------------------------------

        [Test]
        public void PlacePart_WhereItWouldSitInsideAnother_IsRejectedNamingBothParts()
        {
            // hole-0 and hole-near are almost the same point, so the second wheel lands on top
            // of the first.
            ContraptionEditor editor = Editor(maxParts: 12);
            editor.PlacePart(_chassisId, new HoleId("hole-0"), PartType.Wheel);

            EditResult result = editor.PlacePart(_chassisId, new HoleId("hole-near"), PartType.Wheel);

            Assert.That(result.Accepted, Is.False);
            Assert.That(result.RejectionReason, Does.Contain("Wheel"));
        }

        [Test]
        public void PlacePart_OnWellSeparatedHoles_IsAllowed()
        {
            ContraptionEditor editor = Editor(maxParts: 12);
            editor.PlacePart(_chassisId, new HoleId("hole-0"), PartType.Wheel);

            EditResult result = editor.PlacePart(_chassisId, new HoleId("hole-1"), PartType.Wheel);

            Assert.That(result.Accepted, Is.True, result.RejectionReason);
        }

        [Test]
        public void PlacePart_TouchingItsOwnParent_IsAllowed()
        {
            // An attached part meets its parent at a hole by definition. Counting that as overlap
            // would make every placement illegal.
            ContraptionEditor editor = Editor(maxParts: 12);

            EditResult result = editor.PlacePart(_chassisId, new HoleId("hole-0"), PartType.Wheel);

            Assert.That(result.Accepted, Is.True, result.RejectionReason);
        }

        // -----------------------------------------------------------------------------------

        // -----------------------------------------------------------------------------------
        // CanPlace must agree with PlacePart, and must leave nothing behind.
        // -----------------------------------------------------------------------------------

        [Test]
        public void CanPlace_Always_AgreesWithWhatPlacePartDoes()
        {
            // A palette that greys out the wrong buttons is worse than no palette filtering: the
            // player is denied something legal with no explanation at all.
            string[] holes = { "hole-0", "hole-1", "hole-2", "hole-near" };

            foreach (string hole in holes)
            {
                ContraptionEditor editor = Editor(maxParts: 12);
                editor.PlacePart(_chassisId, new HoleId("hole-0"), PartType.Wheel);

                bool predicted = editor.CanPlace(_chassisId, new HoleId(hole), PartType.Wheel);
                bool actual = editor.PlacePart(_chassisId, new HoleId(hole), PartType.Wheel).Accepted;

                Assert.That(predicted, Is.EqualTo(actual), $"CanPlace disagreed with PlacePart for '{hole}'.");
            }
        }

        [Test]
        public void CanPlace_Always_LeavesTheBlueprintUntouched()
        {
            ContraptionEditor editor = Editor(maxParts: 12);
            ContraptionBlueprint before = editor.Blueprint;
            int changes = 0;
            editor.BlueprintChanged += _ => changes++;

            editor.CanPlace(_chassisId, new HoleId("hole-0"), PartType.Wheel);

            Assert.That(editor.Blueprint, Is.SameAs(before));
            Assert.That(changes, Is.Zero, "A dry run must not announce a change.");
        }

        [Test]
        public void CanPlace_RepeatedDryRuns_DoNotDriftPartIds()
        {
            // Burning ids on dry runs would make the palette's mere presence change the ids of
            // parts the player later places.
            ContraptionEditor editor = Editor(maxParts: 12);
            for (int i = 0; i < 5; i++)
            {
                editor.CanPlace(_chassisId, new HoleId("hole-0"), PartType.Wheel);
            }

            editor.PlacePart(_chassisId, new HoleId("hole-0"), PartType.Wheel);
            PartId first = editor.Blueprint.Parts[1].Id;

            ContraptionEditor fresh = Editor(maxParts: 12);
            fresh.PlacePart(_chassisId, new HoleId("hole-0"), PartType.Wheel);

            Assert.That(first, Is.EqualTo(fresh.Blueprint.Parts[1].Id));
        }

        [Test]
        public void EveryRejection_Always_ReadsAsASentenceForAPlayer()
        {
            // The weak form of silent rejection is a message only a programmer understands.
            ContraptionEditor editor = Editor(maxParts: 1);
            editor.PlacePart(_chassisId, new HoleId("hole-0"), PartType.Wheel);

            var reasons = new List<string?>
            {
                editor.PlacePart(_chassisId, new HoleId("hole-1"), PartType.Wheel).RejectionReason,
                editor.PlacePart(_chassisId, new HoleId("nonsense"), PartType.Wheel).RejectionReason,
                editor.PlacePart(new PartId("ghost"), new HoleId("hole-1"), PartType.Wheel).RejectionReason,
                editor.RemovePart(_chassisId).RejectionReason,
                editor.RotatePart(_chassisId).RejectionReason
            };

            foreach (string? reason in reasons)
            {
                Assert.That(reason, Is.Not.Null.And.Not.Empty);
                Assert.That(reason!.EndsWith("."), Is.True, $"Not a sentence: '{reason}'");
                Assert.That(reason, Does.Not.Contain("_").And.Not.Contain("null"),
                    $"Reads like an internal error: '{reason}'");
            }
        }

        private ContraptionEditor Editor(int maxParts)
        {
            ContraptionBlueprint blueprint = ContraptionBlueprint.Create(
                "level-01",
                new[] { new PlacedPart(_chassisId, PartType.Chassis, EditorPosition.Origin, PartRotation.None) });

            return new ContraptionEditor(blueprint, Definitions(), maxParts);
        }

        private static IReadOnlyDictionary<PartType, PartDefinition> Definitions()
        {
            return new Dictionary<PartType, PartDefinition>
            {
                [PartType.Chassis] = new PartDefinition(
                    PartType.Chassis, "Chassis", 2f, 0,
                    new[]
                    {
                        new AttachmentHole(new HoleId("hole-0"), new EditorPosition(-2f, 0f)),
                        new AttachmentHole(new HoleId("hole-1"), new EditorPosition(0f, 0f)),
                        new AttachmentHole(new HoleId("hole-2"), new EditorPosition(2f, 0f)),
                        new AttachmentHole(new HoleId("hole-near"), new EditorPosition(-1.9f, 0f))
                    },
                    PartShape.Box(6f, 0.3f)),
                [PartType.Wheel] = new PartDefinition(
                    PartType.Wheel, "Wheel", 0.5f, 1,
                    new[] { new AttachmentHole(new HoleId("axle"), EditorPosition.Origin) },
                    PartShape.Circle(0.45f))
            };
        }
    }
}
