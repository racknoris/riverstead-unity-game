using System;

namespace Contraption.Domain.Blueprints
{
    /// <summary>
    /// A position in editor space.
    ///
    /// This exists instead of <c>UnityEngine.Vector2</c> because the domain assembly cannot
    /// reference UnityEngine — the load-bearing rule of the project. Conversion to and from
    /// engine types happens in the Unity layer, at the boundary, and never in here.
    ///
    /// Note this is *editor* space: where the player placed the part while building. It is never
    /// updated from a simulated body's transform (`ARCHITECTURE.md` §5).
    /// </summary>
    public readonly struct EditorPosition : IEquatable<EditorPosition>
    {
        public EditorPosition(float x, float y)
        {
            X = x;
            Y = y;
        }

        public float X { get; }

        public float Y { get; }

        public static EditorPosition Origin => new EditorPosition(0f, 0f);

        public bool Equals(EditorPosition other) => X.Equals(other.X) && Y.Equals(other.Y);

        public override bool Equals(object? obj) => obj is EditorPosition other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(X, Y);

        public static bool operator ==(EditorPosition left, EditorPosition right) => left.Equals(right);

        public static bool operator !=(EditorPosition left, EditorPosition right) => !left.Equals(right);

        public override string ToString() => $"({X}, {Y})";
    }
}
