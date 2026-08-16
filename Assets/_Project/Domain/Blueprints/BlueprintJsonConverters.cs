using System;
using Newtonsoft.Json;

namespace Contraption.Domain.Blueprints
{
    /// <summary>
    /// Writes the typed id structs as plain JSON strings rather than as
    /// <c>{ "value": "..." }</c> objects.
    ///
    /// This is about the persisted format, not convenience. Blueprints are saved to disk from
    /// Milestone 9 onward, so the shape written today is one we have to keep reading or
    /// version-bump away from. A readable format is also a debuggable one.
    /// </summary>
    internal sealed class StringIdJsonConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(PartId)
                || objectType == typeof(HoleId)
                || objectType == typeof(AttachmentId);
        }

        public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
        {
            switch (value)
            {
                case PartId partId:
                    writer.WriteValue(partId.Value);
                    break;
                case HoleId holeId:
                    writer.WriteValue(holeId.Value);
                    break;
                case AttachmentId attachmentId:
                    writer.WriteValue(attachmentId.Value);
                    break;
                default:
                    writer.WriteNull();
                    break;
            }
        }

        public override object ReadJson(
            JsonReader reader,
            Type objectType,
            object? existingValue,
            JsonSerializer serializer)
        {
            var raw = reader.Value as string;
            if (string.IsNullOrWhiteSpace(raw))
            {
                throw new JsonSerializationException(
                    $"Expected a non-empty string identifier for {objectType.Name}.");
            }

            if (objectType == typeof(PartId))
            {
                return new PartId(raw!);
            }

            if (objectType == typeof(HoleId))
            {
                return new HoleId(raw!);
            }

            return new AttachmentId(raw!);
        }
    }

    /// <summary>
    /// Writes <see cref="PartRotation"/> as a bare number. It is conceptually one integer, and
    /// storing it as an object would leak an implementation detail into the saved format.
    /// </summary>
    internal sealed class PartRotationJsonConverter : JsonConverter<PartRotation>
    {
        public override void WriteJson(JsonWriter writer, PartRotation value, JsonSerializer serializer)
        {
            writer.WriteValue(value.Degrees);
        }

        public override PartRotation ReadJson(
            JsonReader reader,
            Type objectType,
            PartRotation existingValue,
            bool hasExistingValue,
            JsonSerializer serializer)
        {
            if (reader.Value is null)
            {
                throw new JsonSerializationException("Expected a number for a part rotation.");
            }

            return PartRotation.FromDegrees(Convert.ToInt32(reader.Value));
        }
    }
}
