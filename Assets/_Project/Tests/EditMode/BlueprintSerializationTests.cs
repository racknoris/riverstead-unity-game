using System.Collections.Generic;
using Contraption.Domain.Blueprints;
using NUnit.Framework;

namespace Contraption.Tests.EditMode
{
    /// <summary>
    /// Milestone 2: round-trip fidelity and loud, typed failure on payloads this build cannot read.
    /// </summary>
    public sealed class BlueprintSerializationTests
    {
        [Test]
        public void RoundTrip_PopulatedBlueprint_ProducesAnEqualBlueprint()
        {
            ContraptionBlueprint original = SampleBlueprint();

            string json = BlueprintSerializer.Serialize(original);
            ContraptionBlueprint restored = BlueprintSerializer.Deserialize(json);

            // Value equality across the whole graph, not reference equality - this is the
            // property the immutable-model design exists to give us.
            Assert.That(restored, Is.EqualTo(original));
            Assert.That(restored.GetHashCode(), Is.EqualTo(original.GetHashCode()));
        }

        [Test]
        public void RoundTrip_BlueprintWithNoParts_ProducesAnEqualBlueprint()
        {
            ContraptionBlueprint original = ContraptionBlueprint.Create("level-01");

            ContraptionBlueprint restored = BlueprintSerializer.Deserialize(BlueprintSerializer.Serialize(original));

            Assert.That(restored, Is.EqualTo(original));
            Assert.That(restored.Parts, Is.Empty);
            Assert.That(restored.Attachments, Is.Empty);
        }

        [Test]
        public void RoundTrip_PartConfiguration_PreservesEveryTunedValue()
        {
            PartConfiguration configuration = PartConfiguration.Empty
                .With("motorSpeed", 330f)
                .With("maxMotorTorque", 120f);
            ContraptionBlueprint original = ContraptionBlueprint.Create(
                "level-01",
                new[] { new PlacedPart(new PartId("p1"), PartType.PoweredWheel, new EditorPosition(1f, 2f), PartRotation.FromDegrees(30), configuration) });

            ContraptionBlueprint restored = BlueprintSerializer.Deserialize(BlueprintSerializer.Serialize(original));

            IReadOnlyDictionary<string, float> values = restored.Parts[0].Configuration.Values;
            Assert.That(values["motorSpeed"], Is.EqualTo(330f));
            Assert.That(values["maxMotorTorque"], Is.EqualTo(120f));
        }

        [Test]
        public void Serialize_Always_WritesTheSchemaVersion()
        {
            string json = BlueprintSerializer.Serialize(ContraptionBlueprint.Create("level-01"));

            Assert.That(json, Does.Contain("\"schemaVersion\":1"));
        }

        [Test]
        public void Serialize_Always_WritesEnumsAndIdsAsReadableStrings()
        {
            string json = BlueprintSerializer.Serialize(SampleBlueprint());

            // Enums by name, not ordinal: reordering PartType must not silently reinterpret
            // every saved blueprint. Ids as bare strings, not wrapper objects.
            Assert.That(json, Does.Contain("\"PoweredWheel\""));
            Assert.That(json, Does.Contain("\"chassis-1\""));
            Assert.That(json, Does.Not.Contain("\"value\""));
        }

        // -----------------------------------------------------------------------------------
        // Loud failure. Each of these would otherwise present to the player as "my machine
        // vanished" rather than as an error.
        // -----------------------------------------------------------------------------------

        [Test]
        public void Deserialize_MalformedJson_ThrowsMalformedJson()
        {
            BlueprintSerializationException exception = Assert.Throws<BlueprintSerializationException>(
                () => BlueprintSerializer.Deserialize("{ this is not json"));

            Assert.That(exception.Error, Is.EqualTo(BlueprintSerializationError.MalformedJson));
        }

        [Test]
        public void Deserialize_EmptyPayload_ThrowsMalformedJson()
        {
            BlueprintSerializationException exception = Assert.Throws<BlueprintSerializationException>(
                () => BlueprintSerializer.Deserialize("   "));

            Assert.That(exception.Error, Is.EqualTo(BlueprintSerializationError.MalformedJson));
        }

        [Test]
        public void Deserialize_PayloadWithoutSchemaVersion_ThrowsMissingSchemaVersion()
        {
            BlueprintSerializationException exception = Assert.Throws<BlueprintSerializationException>(
                () => BlueprintSerializer.Deserialize("{\"levelId\":\"level-01\",\"parts\":[],\"attachments\":[]}"));

            Assert.That(exception.Error, Is.EqualTo(BlueprintSerializationError.MissingSchemaVersion));
        }

        [Test]
        public void Deserialize_OlderSchemaVersion_ThrowsUnsupportedSchemaVersion()
        {
            BlueprintSerializationException exception = Assert.Throws<BlueprintSerializationException>(
                () => BlueprintSerializer.Deserialize("{\"schemaVersion\":0,\"levelId\":\"level-01\"}"));

            Assert.That(exception.Error, Is.EqualTo(BlueprintSerializationError.UnsupportedSchemaVersion));
            Assert.That(exception.Message, Does.Contain("0").And.Contain("1"));
        }

        [Test]
        public void Deserialize_FutureSchemaVersion_ThrowsUnsupportedSchemaVersion()
        {
            BlueprintSerializationException exception = Assert.Throws<BlueprintSerializationException>(
                () => BlueprintSerializer.Deserialize("{\"schemaVersion\":99,\"levelId\":\"level-01\"}"));

            Assert.That(exception.Error, Is.EqualTo(BlueprintSerializationError.UnsupportedSchemaVersion));
        }

        [Test]
        public void Deserialize_CorrectVersionButInvalidContent_ThrowsInvalidContent()
        {
            // Right version, but no level id - the blueprint constructor rejects it.
            BlueprintSerializationException exception = Assert.Throws<BlueprintSerializationException>(
                () => BlueprintSerializer.Deserialize("{\"schemaVersion\":1,\"levelId\":\"\"}"));

            Assert.That(exception.Error, Is.EqualTo(BlueprintSerializationError.InvalidContent));
        }

        private static ContraptionBlueprint SampleBlueprint()
        {
            var parts = new[]
            {
                new PlacedPart(
                    new PartId("chassis-1"), PartType.Chassis, EditorPosition.Origin, PartRotation.None),
                new PlacedPart(
                    new PartId("wheel-1"),
                    PartType.PoweredWheel,
                    new EditorPosition(-1.15f, -0.62f),
                    PartRotation.FromDegrees(15),
                    PartConfiguration.Empty.With("motorSpeed", 330f))
            };

            var attachments = new[]
            {
                new Attachment(
                    new AttachmentId("att-1"),
                    new PartId("chassis-1"),
                    new HoleId("hole-a"),
                    new PartId("wheel-1"),
                    new HoleId("axle"))
            };

            return ContraptionBlueprint.Create("level-01", parts, attachments);
        }
    }
}
