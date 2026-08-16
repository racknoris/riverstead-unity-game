using System;

namespace Contraption.Domain.Blueprints
{
    /// <summary>
    /// A part's rotation, stored in whole degrees and normalised to [0, 360).
    ///
    /// Normalisation is model hygiene, not validation: it means 370° and 10° are the same value
    /// and therefore compare equal, which matters because blueprint equality drives the
    /// round-trip tests. Enforcing that a rotation lands on a legal increment is a *placement
    /// rule* and belongs to Milestone 7, not here.
    /// </summary>
    public readonly struct PartRotation : IEquatable<PartRotation>
    {
        private PartRotation(int degrees)
        {
            Degrees = degrees;
        }

        public int Degrees { get; }

        public static PartRotation None => new PartRotation(0);

        public static PartRotation FromDegrees(int degrees)
        {
            int wrapped = degrees % 360;
            if (wrapped < 0)
            {
                wrapped += 360;
            }

            return new PartRotation(wrapped);
        }

        public bool Equals(PartRotation other) => Degrees == other.Degrees;

        public override bool Equals(object? obj) => obj is PartRotation other && Equals(other);

        public override int GetHashCode() => Degrees;

        public static bool operator ==(PartRotation left, PartRotation right) => left.Equals(right);

        public static bool operator !=(PartRotation left, PartRotation right) => !left.Equals(right);

        public override string ToString() => $"{Degrees}°";
    }
}
