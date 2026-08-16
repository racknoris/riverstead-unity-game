using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace Contraption.Domain.Blueprints
{
    /// <summary>
    /// Reads and writes blueprints as JSON.
    ///
    /// Lives in the domain assembly because it is pure text-to-model work with no Unity types
    /// involved. *Where* the text is stored — a file under <c>persistentDataPath</c>, IndexedDB on
    /// Web — is the repository's problem in the Unity layer (`ARCHITECTURE.md` §12), and this
    /// class knows nothing about it.
    ///
    /// The schema version is checked *before* the payload is bound to the model, so an old or
    /// future file reports "version 2, expected 1" rather than a confusing property-level error.
    /// </summary>
    public static class BlueprintSerializer
    {
        private const string SchemaVersionProperty = "schemaVersion";

        private static readonly JsonSerializerSettings Settings = CreateSettings();

        public static string Serialize(ContraptionBlueprint blueprint)
        {
            if (blueprint is null)
            {
                throw new ArgumentNullException(nameof(blueprint));
            }

            return JsonConvert.SerializeObject(blueprint, Settings);
        }

        /// <exception cref="BlueprintSerializationException">
        /// The payload is empty, malformed, unversioned, of an unknown version, or not a valid
        /// blueprint. Never returns null and never returns a partially populated blueprint.
        /// </exception>
        public static ContraptionBlueprint Deserialize(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new BlueprintSerializationException(
                    BlueprintSerializationError.MalformedJson,
                    "Cannot read a blueprint from an empty payload.");
            }

            JObject payload = ParsePayload(json);
            RequireSupportedSchemaVersion(payload);

            ContraptionBlueprint? blueprint;
            try
            {
                blueprint = payload.ToObject<ContraptionBlueprint>(JsonSerializer.Create(Settings));
            }
            catch (Exception exception) when (exception is JsonException or ArgumentException)
            {
                throw new BlueprintSerializationException(
                    BlueprintSerializationError.InvalidContent,
                    $"The payload is a valid version {ContraptionBlueprint.CurrentSchemaVersion} document "
                    + $"but not a valid blueprint: {exception.Message}",
                    exception);
            }

            if (blueprint is null)
            {
                throw new BlueprintSerializationException(
                    BlueprintSerializationError.InvalidContent,
                    "The payload deserialised to nothing.");
            }

            return blueprint;
        }

        private static JObject ParsePayload(string json)
        {
            try
            {
                return JObject.Parse(json);
            }
            catch (JsonException exception)
            {
                throw new BlueprintSerializationException(
                    BlueprintSerializationError.MalformedJson,
                    $"The blueprint payload is not valid JSON: {exception.Message}",
                    exception);
            }
        }

        private static void RequireSupportedSchemaVersion(JObject payload)
        {
            JToken? versionToken = payload[SchemaVersionProperty];
            if (versionToken is null || versionToken.Type == JTokenType.Null)
            {
                throw new BlueprintSerializationException(
                    BlueprintSerializationError.MissingSchemaVersion,
                    $"The blueprint payload has no '{SchemaVersionProperty}'. Refusing to guess at its shape.");
            }

            int version;
            try
            {
                version = versionToken.Value<int>();
            }
            catch (Exception exception) when (exception is FormatException or InvalidCastException or OverflowException)
            {
                throw new BlueprintSerializationException(
                    BlueprintSerializationError.MissingSchemaVersion,
                    $"The blueprint payload has a non-numeric '{SchemaVersionProperty}'.",
                    exception);
            }

            if (version != ContraptionBlueprint.CurrentSchemaVersion)
            {
                throw new BlueprintSerializationException(
                    BlueprintSerializationError.UnsupportedSchemaVersion,
                    $"Blueprint schema version {version} cannot be read by this build, which understands "
                    + $"version {ContraptionBlueprint.CurrentSchemaVersion}. Migration is deliberately not "
                    + "attempted; the POC has no saved data worth migrating.");
            }
        }

        private static JsonSerializerSettings CreateSettings()
        {
            var settings = new JsonSerializerSettings
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver(),
                Formatting = Formatting.None,
                // A missing required constructor argument should surface as an error rather than
                // quietly binding to a default.
                MissingMemberHandling = MissingMemberHandling.Ignore,
                NullValueHandling = NullValueHandling.Include
            };

            // Enums as names, not ordinals: reordering the enum must not silently reinterpret
            // every saved blueprint.
            settings.Converters.Add(new StringEnumConverter());
            settings.Converters.Add(new StringIdJsonConverter());
            settings.Converters.Add(new PartRotationJsonConverter());
            return settings;
        }
    }
}
