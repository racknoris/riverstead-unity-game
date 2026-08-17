using System;
using Contraption.Domain.Blueprints;

namespace Contraption.Domain.Editing
{
    /// <summary>
    /// Where parts sit, derived from how they are attached.
    ///
    /// This is the piece of arithmetic that makes D11 work. A part's position is *not* authored;
    /// it falls out of its parent's hole, its own mount hole, and its rotation. Hand-authoring a
    /// position that disagrees with the hole it is bolted to was the Milestone 5 bug where the
    /// joint yanked the machine apart at the first physics step — this removes the possibility
    /// rather than validating against it.
    ///
    /// Pure trigonometry, no Unity types: the domain assembly has no Vector2 or Quaternion.
    /// </summary>
    public static class PartLayout
    {
        /// <summary>Rotates a point about the origin, anticlockwise, matching Unity's 2D convention.</summary>
        public static EditorPosition Rotate(EditorPosition point, PartRotation rotation)
        {
            if (rotation.Degrees == 0)
            {
                return point;
            }

            double radians = rotation.Degrees * Math.PI / 180.0;
            double cos = Math.Cos(radians);
            double sin = Math.Sin(radians);

            return new EditorPosition(
                (float)((point.X * cos) - (point.Y * sin)),
                (float)((point.X * sin) + (point.Y * cos)));
        }

        /// <summary>
        /// Where a child part's origin must sit so that its mount hole lands exactly on the
        /// parent's hole.
        ///
        /// Both holes are rotated into world space first: the parent's by the parent's rotation,
        /// the child's by the child's own. Rotating a part therefore *moves* it — it pivots about
        /// its mount point rather than about its centre, which is what "the part stays bolted on
        /// while you turn it" means.
        /// </summary>
        public static EditorPosition PositionChild(
            EditorPosition parentPosition,
            PartRotation parentRotation,
            EditorPosition parentHoleLocal,
            EditorPosition childMountHoleLocal,
            PartRotation childRotation)
        {
            EditorPosition parentHoleWorld = Add(parentPosition, Rotate(parentHoleLocal, parentRotation));
            EditorPosition childMountOffset = Rotate(childMountHoleLocal, childRotation);

            return new EditorPosition(
                parentHoleWorld.X - childMountOffset.X,
                parentHoleWorld.Y - childMountOffset.Y);
        }

        public static EditorPosition Add(EditorPosition left, EditorPosition right) =>
            new EditorPosition(left.X + right.X, left.Y + right.Y);
    }
}
