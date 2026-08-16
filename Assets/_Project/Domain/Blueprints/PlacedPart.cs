using System;
using Newtonsoft.Json;

namespace Contraption.Domain.Blueprints
{
    /// <summary>
    /// One part the player has placed: what it is, where they put it, how it is turned, and how
    /// they tuned it.
    ///
    /// Deliberately absent: anything the simulation owns. No body, no collider, no velocity, no
    /// contacts (`ARCHITECTURE.md` §5 and §7). <see cref="Position"/> is where the part was
    /// *placed in the editor*, and is never written back from a running simulation.
    /// </summary>
    public sealed record PlacedPart
    {
        [JsonConstructor]
        public PlacedPart(
            PartId id,
            PartType type,
            EditorPosition position,
            PartRotation rotation,
            PartConfiguration? configuration = null)
        {
            if (string.IsNullOrEmpty(id.Value))
            {
                throw new ArgumentException("A placed part needs a stable id.", nameof(id));
            }

            Id = id;
            Type = type;
            Position = position;
            Rotation = rotation;
            Configuration = configuration ?? PartConfiguration.Empty;
        }

        public PartId Id { get; }

        public PartType Type { get; }

        public EditorPosition Position { get; }

        public PartRotation Rotation { get; }

        public PartConfiguration Configuration { get; }
    }
}
