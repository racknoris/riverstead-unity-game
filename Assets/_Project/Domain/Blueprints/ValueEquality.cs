using System.Collections.Generic;

namespace Contraption.Domain.Blueprints
{
    /// <summary>
    /// Sequence equality helpers for the blueprint models.
    ///
    /// C# records synthesise structural equality over their *fields*, which for a list member
    /// means reference equality — two blueprints holding equal-but-distinct lists would compare
    /// unequal. That would quietly break every round-trip test in Milestone 2, since a
    /// deserialised blueprint necessarily holds new list instances. Hence the manual equality on
    /// the aggregate types.
    /// </summary>
    internal static class ValueEquality
    {
        public static bool SequenceEquals<T>(IReadOnlyList<T> left, IReadOnlyList<T> right)
        {
            if (ReferenceEquals(left, right))
            {
                return true;
            }

            if (left.Count != right.Count)
            {
                return false;
            }

            EqualityComparer<T> comparer = EqualityComparer<T>.Default;
            for (int i = 0; i < left.Count; i++)
            {
                if (!comparer.Equals(left[i], right[i]))
                {
                    return false;
                }
            }

            return true;
        }

        public static int SequenceHashCode<T>(IReadOnlyList<T> items)
        {
            var hash = new System.HashCode();
            for (int i = 0; i < items.Count; i++)
            {
                hash.Add(items[i]);
            }

            return hash.ToHashCode();
        }
    }
}
