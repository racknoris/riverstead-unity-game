using System;

namespace Contraption.Domain.Blueprints
{
    /// <summary>
    /// Stable identifier for a placed part, unique within a blueprint.
    ///
    /// Typed rather than a bare string on purpose: attachments pair part ids with hole ids, and
    /// those are the two things easiest to transpose in an editor. The compiler catches it.
    ///
    /// Written as a hand-rolled readonly struct rather than a <c>record struct</c> because Unity
    /// 6.5 compiles at C# 9 — see `docs/CONVENTIONS.md`.
    /// </summary>
    public readonly struct PartId : IEquatable<PartId>
    {
        public PartId(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("A part id must be a non-empty identifier.", nameof(value));
            }

            Value = value;
        }

        public string Value { get; }

        public bool Equals(PartId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);

        public override bool Equals(object? obj) => obj is PartId other && Equals(other);

        public override int GetHashCode() => Value is null ? 0 : Value.GetHashCode();

        public static bool operator ==(PartId left, PartId right) => left.Equals(right);

        public static bool operator !=(PartId left, PartId right) => !left.Equals(right);

        public override string ToString() => Value ?? string.Empty;
    }
}
