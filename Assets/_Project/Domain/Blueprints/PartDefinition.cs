using System;
using System.Collections.Generic;

namespace Contraption.Domain.Blueprints
{
    /// <summary>
    /// The plain-C# face of a part's catalog entry.
    ///
    /// `PartDefinitionAsset` is a ScriptableObject in the Unity layer holding prefabs, sprites and
    /// physics tuning; it exposes *this* so the domain can validate placements and compute scores
    /// without touching Unity types (`ARCHITECTURE.md` §9). Only the fields the domain actually
    /// reasons about live here — mass and cost for scoring, holes for connection rules. Anything
    /// the renderer or the physics builder needs stays on the asset.
    /// </summary>
    public sealed record PartDefinition
    {
        private readonly HoleId[] _attachmentHoles;

        public PartDefinition(
            PartType type,
            string displayName,
            float mass,
            int cost,
            IReadOnlyList<HoleId>? attachmentHoles = null)
        {
            if (string.IsNullOrWhiteSpace(displayName))
            {
                throw new ArgumentException("A part definition needs a display name.", nameof(displayName));
            }

            if (mass <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(mass), mass, "Part mass must be positive.");
            }

            Type = type;
            DisplayName = displayName;
            Mass = mass;
            Cost = cost;
            _attachmentHoles = attachmentHoles is null
                ? Array.Empty<HoleId>()
                : CopyOf(attachmentHoles);
        }

        public PartType Type { get; }

        public string DisplayName { get; }

        public float Mass { get; }

        /// <summary>Budget cost, for the "use fewer parts" scoring input (`ARCHITECTURE.md` §2).</summary>
        public int Cost { get; }

        /// <summary>The holes this part offers for others to attach to.</summary>
        public IReadOnlyList<HoleId> AttachmentHoles => _attachmentHoles;

        public bool Equals(PartDefinition? other)
        {
            if (other is null)
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return Type == other.Type
                && DisplayName == other.DisplayName
                && Mass.Equals(other.Mass)
                && Cost == other.Cost
                && ValueEquality.SequenceEquals(_attachmentHoles, other._attachmentHoles);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(
                Type, DisplayName, Mass, Cost, ValueEquality.SequenceHashCode(_attachmentHoles));
        }

        private static HoleId[] CopyOf(IReadOnlyList<HoleId> source)
        {
            var copy = new HoleId[source.Count];
            for (int i = 0; i < source.Count; i++)
            {
                copy[i] = source[i];
            }

            return copy;
        }
    }
}
