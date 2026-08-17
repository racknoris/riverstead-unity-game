using Contraption.Domain.Blueprints;
using UnityEngine;

namespace Contraption.UI
{
    /// <summary>
    /// The hard-coded blueprint Milestone 5 runs against, plus a placeholder sprite for the
    /// throwaway world.
    ///
    /// This exists only until Milestone 6 lets the player build a machine by touch, and until
    /// Milestone 9 restores a saved draft on launch. Both replace it; neither extends it.
    /// </summary>
    internal static class PlaceholderBlueprints
    {
        public static readonly PartId ChassisId = new PartId("chassis");

        private static Sprite _square;

        /// <summary>
        /// A bare chassis: the machine the player starts from and builds outward from. The
        /// chassis is the tree's root, so its position is authored rather than derived (D12) —
        /// it sits one wheel-radius-plus-half-a-deck above the ground so a wheel hung from an
        /// under-deck hole lands exactly on the surface.
        /// </summary>
        public static ContraptionBlueprint BareChassis(string levelId)
        {
            return ContraptionBlueprint.Create(
                levelId,
                new[]
                {
                    new PlacedPart(ChassisId, PartType.Chassis, new EditorPosition(0f, 0.60f), PartRotation.None)
                });
        }

        /// <summary>
        /// A chassis on two powered wheels. Hole ids match the catalog's chassis layout:
        /// hole-01 and hole-02 are the under-deck wheel mounts, at chassis-local (±1.15, −0.15).
        ///
        /// **A part's placed position must agree with the hole it attaches to.** Getting this
        /// wrong does not fail loudly — the joint simply wins and yanks the part to the hole at
        /// the first physics step, which reads as a machine that convulses and refuses to drive.
        /// An earlier version of this blueprint put the chassis 0.47 units too high and did
        /// exactly that. Milestone 6 removes the hazard by computing placement from the hole;
        /// Milestone 7 should reject the mismatch outright.
        ///
        /// Derivation: a wheel rests one radius (0.45) above ground, so the chassis centre sits
        /// at 0.45 + 0.15 = 0.60, putting its wheel holes exactly at the wheel centres.
        /// </summary>
        public static ContraptionBlueprint Rover(string levelId)
        {
            const float WheelCentreY = 0.45f;
            const float ChassisCentreY = WheelCentreY + 0.15f;

            var parts = new[]
            {
                new PlacedPart(
                    ChassisId, PartType.Chassis,
                    new EditorPosition(0f, ChassisCentreY), PartRotation.None),
                new PlacedPart(
                    new PartId("wheel-rear"), PartType.PoweredWheel,
                    new EditorPosition(-1.15f, WheelCentreY), PartRotation.None),
                new PlacedPart(
                    new PartId("wheel-front"), PartType.PoweredWheel,
                    new EditorPosition(1.15f, WheelCentreY), PartRotation.None)
            };

            var attachments = new[]
            {
                new Attachment(
                    new AttachmentId("attach-rear"), ChassisId, new HoleId("hole-01"),
                    new PartId("wheel-rear"), new HoleId("axle")),
                new Attachment(
                    new AttachmentId("attach-front"), ChassisId, new HoleId("hole-02"),
                    new PartId("wheel-front"), new HoleId("axle"))
            };

            return ContraptionBlueprint.Create(levelId, parts, attachments);
        }

        /// <summary>A 1×1 world-unit white square, so local scale reads directly as world size.</summary>
        public static Sprite SquareSprite()
        {
            if (_square == null)
            {
                var texture = new Texture2D(1, 1) { filterMode = FilterMode.Point };
                texture.SetPixel(0, 0, Color.white);
                texture.Apply();
                _square = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
                _square.name = "PlaceholderSquare";
            }

            return _square;
        }
    }
}
