namespace Contraption.Domain.Blueprints
{
    /// <summary>
    /// The kinds of part a blueprint can contain. This is the key into the part catalog
    /// (`ARCHITECTURE.md` §9): a blueprint references a part *by type*, and the Unity layer looks
    /// up the prefab, sprite and tuning for it. Nothing here knows what a part looks like.
    /// </summary>
    public enum PartType
    {
        /// <summary>The base body every contraption is built on, carrying the attachment holes.</summary>
        Chassis,
        Wheel,
        PoweredWheel,
        Beam,
        RigidConnector,
        Hinge,
        Spring,
        ProtectivePlate
    }
}
