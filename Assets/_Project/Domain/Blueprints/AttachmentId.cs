using System;

namespace Contraption.Domain.Blueprints
{
    /// <summary>
    /// Stable identifier for a connection between two parts. Attachments get their own ids so the
    /// editor can remove or reconfigure one connection without identifying it positionally
    /// (`ARCHITECTURE.md` §10).
    /// </summary>
    public readonly struct AttachmentId : IEquatable<AttachmentId>
    {
        public AttachmentId(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("An attachment id must be a non-empty identifier.", nameof(value));
            }

            Value = value;
        }

        public string Value { get; }

        public bool Equals(AttachmentId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);

        public override bool Equals(object? obj) => obj is AttachmentId other && Equals(other);

        public override int GetHashCode() => Value is null ? 0 : Value.GetHashCode();

        public static bool operator ==(AttachmentId left, AttachmentId right) => left.Equals(right);

        public static bool operator !=(AttachmentId left, AttachmentId right) => !left.Equals(right);

        public override string ToString() => Value ?? string.Empty;
    }
}
