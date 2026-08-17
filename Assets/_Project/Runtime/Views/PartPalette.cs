using Contraption.Domain.Blueprints;
using UnityEngine;

namespace Contraption.Runtime.Views
{
    /// <summary>
    /// Placeholder colours per part type, shared by the editor preview and the running simulation.
    ///
    /// Shared deliberately: a part the player placed must look like the part that appears when
    /// they press Run, or the preview stops being a preview. Two colour tables would drift the
    /// first time one was edited.
    ///
    /// Cosmetic only, and destined to be replaced by sprites on the catalog assets.
    /// </summary>
    public static class PartPalette
    {
        public static Color ColourFor(PartType partType)
        {
            switch (partType)
            {
                case PartType.Chassis: return new Color(0.85f, 0.62f, 0.25f);
                case PartType.Wheel: return new Color(0.30f, 0.32f, 0.36f);
                case PartType.PoweredWheel: return new Color(0.22f, 0.24f, 0.28f);
                case PartType.Beam: return new Color(0.55f, 0.70f, 0.90f);
                case PartType.RigidConnector: return new Color(0.70f, 0.70f, 0.75f);
                case PartType.Hinge: return new Color(0.95f, 0.80f, 0.35f);
                case PartType.Spring: return new Color(0.55f, 0.85f, 0.65f);
                case PartType.ProtectivePlate: return new Color(0.75f, 0.45f, 0.45f);
                default: return Color.magenta;
            }
        }
    }
}
