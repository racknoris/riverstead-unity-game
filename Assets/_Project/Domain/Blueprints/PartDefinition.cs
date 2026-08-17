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
        private readonly AttachmentHole[] _attachmentHoles;

        public PartDefinition(
            PartType type,
            string displayName,
            float mass,
            int cost,
            IReadOnlyList<AttachmentHole>? attachmentHoles = null,
            PartShape? shape = null)
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
            // A part with no declared shape is treated as a small box, so an incomplete catalog
            // entry cannot silently switch overlap checking off for that part.
            Shape = shape ?? PartShape.Box(0.5f, 0.5f);
            _attachmentHoles = attachmentHoles is null
                ? Array.Empty<AttachmentHole>()
                : CopyOf(attachmentHoles);
        }

        public PartType Type { get; }

        public string DisplayName { get; }

        public float Mass { get; }

        /// <summary>Budget cost, for the "use fewer parts" scoring input (`ARCHITECTURE.md` §2).</summary>
        public int Cost { get; }

        /// <summary>Coarse size, used only to judge whether placements overlap.</summary>
        public PartShape Shape { get; }

        /// <summary>The holes this part offers for others to attach to, with their local positions.</summary>
        public IReadOnlyList<AttachmentHole> AttachmentHoles => _attachmentHoles;

        /// <summary>Finds a hole by id. Returns false rather than throwing, so callers can
        /// report an unknown hole in their own terms.</summary>
        public bool TryGetHole(HoleId holeId, out AttachmentHole hole)
        {
            for (int i = 0; i < _attachmentHoles.Length; i++)
            {
                if (_attachmentHoles[i].Id == holeId)
                {
                    hole = _attachmentHoles[i];
                    return true;
                }
            }

            hole = null!;
            return false;
        }

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
                && Shape.Equals(other.Shape)
                && ValueEquality.SequenceEquals(_attachmentHoles, other._attachmentHoles);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(
                Type, DisplayName, Mass, Cost, Shape, ValueEquality.SequenceHashCode(_attachmentHoles));
        }

        private static AttachmentHole[] CopyOf(IReadOnlyList<AttachmentHole> source)
        {
            var copy = new AttachmentHole[source.Count];
            for (int i = 0; i < source.Count; i++)
            {
                copy[i] = source[i] ?? throw new ArgumentException(
                    "A part definition cannot contain a null hole.", nameof(source));
            }

            return copy;
        }
    }
}
