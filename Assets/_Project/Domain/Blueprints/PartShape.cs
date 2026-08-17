using System;

namespace Contraption.Domain.Blueprints
{
    /// <summary>
    /// How big a part is, in the only terms the domain needs: enough to tell whether two parts
    /// are sitting on top of each other.
    ///
    /// This is not the collider. The Unity layer builds physics shapes from the catalog asset;
    /// this exists so placement rules can be decided in the domain without seeing a Unity type
    /// (`ARCHITECTURE.md` §9). It is deliberately coarse — a bounding radius, not a polygon —
    /// because an editor rule only has to answer "is this obviously wrong", and a rule the player
    /// cannot predict is worse than a permissive one.
    /// </summary>
    public readonly struct PartShape : IEquatable<PartShape>
    {
        private PartShape(bool isCircle, float radius, float width, float height)
        {
            IsCircle = isCircle;
            Radius = radius;
            Width = width;
            Height = height;
        }

        public bool IsCircle { get; }

        public float Radius { get; }

        public float Width { get; }

        public float Height { get; }

        public static PartShape Circle(float radius)
        {
            if (radius <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(radius), radius, "A circular part needs a positive radius.");
            }

            return new PartShape(true, radius, radius * 2f, radius * 2f);
        }

        public static PartShape Box(float width, float height)
        {
            if (width <= 0f || height <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(width), width, "A box part needs positive dimensions.");
            }

            return new PartShape(false, 0f, width, height);
        }

        /// <summary>
        /// Radius of the smallest circle containing the part. For a box that is its half-diagonal,
        /// which is generous — a rotated beam sweeps that circle, and the alternative is
        /// rotation-aware polygon tests the POC does not need.
        /// </summary>
        public float BoundingRadius => IsCircle
            ? Radius
            : 0.5f * (float)Math.Sqrt((Width * Width) + (Height * Height));

        public bool Equals(PartShape other) =>
            IsCircle == other.IsCircle
            && Radius.Equals(other.Radius)
            && Width.Equals(other.Width)
            && Height.Equals(other.Height);

        public override bool Equals(object? obj) => obj is PartShape other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(IsCircle, Radius, Width, Height);

        public override string ToString() => IsCircle ? $"circle r={Radius}" : $"box {Width}x{Height}";
    }
}
