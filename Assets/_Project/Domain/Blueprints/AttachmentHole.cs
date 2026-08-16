using System;

namespace Contraption.Domain.Blueprints
{
    /// <summary>
    /// One predefined attachment point on a part: its stable id and where it sits, in the part's
    /// own local space.
    ///
    /// The position is what makes a hole usable rather than merely nameable. It has two
    /// consumers: the joint builder anchors a connection at the hole instead of at the attached
    /// part's centre, and the editor computes where a part lands when the player taps a hole.
    ///
    /// Deliberately no facing or orientation. The player already rotates parts in fixed
    /// increments (`ARCHITECTURE.md` §10), so a per-hole facing may simply be redundant with
    /// that; adding it now would be guessing at Milestone 6's answer.
    /// </summary>
    public sealed record AttachmentHole
    {
        public AttachmentHole(HoleId id, EditorPosition localPosition)
        {
            if (string.IsNullOrEmpty(id.Value))
            {
                throw new ArgumentException("An attachment hole needs a stable id.", nameof(id));
            }

            Id = id;
            LocalPosition = localPosition;
        }

        public HoleId Id { get; }

        /// <summary>Offset from the part's origin, in the part's unrotated local space.</summary>
        public EditorPosition LocalPosition { get; }
    }
}
