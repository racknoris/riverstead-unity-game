using System;
using Newtonsoft.Json;

namespace Contraption.Domain.Blueprints
{
    /// <summary>
    /// A connection between two placed parts, expressed as a hole on each side.
    ///
    /// Note what this does *not* say: which Unity joint realises it. That mapping
    /// (`ARCHITECTURE.md` §10 — weld to <c>FixedJoint2D</c>, hinge to <c>HingeJoint2D</c>, and so
    /// on) belongs to the part catalog and the joint builder. A blueprint records the player's
    /// intent; the Unity layer decides how to honour it.
    /// </summary>
    public sealed record Attachment
    {
        [JsonConstructor]
        public Attachment(
            AttachmentId id,
            PartId fromPartId,
            HoleId fromHoleId,
            PartId toPartId,
            HoleId toHoleId)
        {
            if (string.IsNullOrEmpty(id.Value))
            {
                throw new ArgumentException("An attachment needs a stable id.", nameof(id));
            }

            if (fromPartId == toPartId)
            {
                throw new ArgumentException("A part cannot be attached to itself.", nameof(toPartId));
            }

            Id = id;
            FromPartId = fromPartId;
            FromHoleId = fromHoleId;
            ToPartId = toPartId;
            ToHoleId = toHoleId;
        }

        public AttachmentId Id { get; }

        public PartId FromPartId { get; }

        public HoleId FromHoleId { get; }

        public PartId ToPartId { get; }

        public HoleId ToHoleId { get; }
    }
}
