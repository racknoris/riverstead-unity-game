using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Contraption.Domain.Blueprints
{
    /// <summary>
    /// The durable description of a machine: which parts, placed where, connected how.
    ///
    /// Immutable, with value equality. Every edit produces a *new* blueprint
    /// (`ARCHITECTURE.md` §6.3), and a running simulation never writes back into one — reset means
    /// destroying the simulation and rebuilding from this unchanged object (§8).
    ///
    /// The incoming collections are defensively copied, so a caller holding the list it passed in
    /// cannot mutate a blueprint after the fact.
    /// </summary>
    public sealed record ContraptionBlueprint
    {
        /// <summary>
        /// Bumped whenever the persisted shape changes incompatibly. Deserialising any other
        /// version fails loudly rather than guessing (see <see cref="BlueprintSerializer"/>).
        /// </summary>
        public const int CurrentSchemaVersion = 1;

        private readonly PlacedPart[] _parts;
        private readonly Attachment[] _attachments;

        [JsonConstructor]
        public ContraptionBlueprint(
            int schemaVersion,
            string levelId,
            IReadOnlyList<PlacedPart>? parts,
            IReadOnlyList<Attachment>? attachments)
        {
            if (string.IsNullOrWhiteSpace(levelId))
            {
                throw new ArgumentException("A blueprint must name the level it was built for.", nameof(levelId));
            }

            SchemaVersion = schemaVersion;
            LevelId = levelId;
            _parts = parts is null ? Array.Empty<PlacedPart>() : CopyOf(parts, nameof(parts));
            _attachments = attachments is null ? Array.Empty<Attachment>() : CopyOf(attachments, nameof(attachments));
        }

        /// <summary>Creates a blueprint stamped with the current schema version.</summary>
        public static ContraptionBlueprint Create(
            string levelId,
            IReadOnlyList<PlacedPart>? parts = null,
            IReadOnlyList<Attachment>? attachments = null)
        {
            return new ContraptionBlueprint(CurrentSchemaVersion, levelId, parts, attachments);
        }

        public int SchemaVersion { get; }

        public string LevelId { get; }

        public IReadOnlyList<PlacedPart> Parts => _parts;

        public IReadOnlyList<Attachment> Attachments => _attachments;

        public bool Equals(ContraptionBlueprint? other)
        {
            if (other is null)
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return SchemaVersion == other.SchemaVersion
                && LevelId == other.LevelId
                && ValueEquality.SequenceEquals(_parts, other._parts)
                && ValueEquality.SequenceEquals(_attachments, other._attachments);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(
                SchemaVersion,
                LevelId,
                ValueEquality.SequenceHashCode(_parts),
                ValueEquality.SequenceHashCode(_attachments));
        }

        private static T[] CopyOf<T>(IReadOnlyList<T> source, string parameterName)
            where T : class
        {
            var copy = new T[source.Count];
            for (int i = 0; i < source.Count; i++)
            {
                copy[i] = source[i] ?? throw new ArgumentException(
                    "A blueprint cannot contain a null entry.", parameterName);
            }

            return copy;
        }
    }
}
