using System;

namespace Contraption.Domain.Blueprints
{
    /// <summary>
    /// Stable identifier for one predefined attachment hole on a part
    /// (`ARCHITECTURE.md` §10: a chassis has ~8-12 of them).
    /// </summary>
    public readonly struct HoleId : IEquatable<HoleId>
    {
        public HoleId(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("A hole id must be a non-empty identifier.", nameof(value));
            }

            Value = value;
        }

        public string Value { get; }

        public bool Equals(HoleId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);

        public override bool Equals(object? obj) => obj is HoleId other && Equals(other);

        public override int GetHashCode() => Value is null ? 0 : Value.GetHashCode();

        public static bool operator ==(HoleId left, HoleId right) => left.Equals(right);

        public static bool operator !=(HoleId left, HoleId right) => !left.Equals(right);

        public override string ToString() => Value ?? string.Empty;
    }
}
