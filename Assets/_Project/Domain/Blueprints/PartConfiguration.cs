using System.Collections.Generic;
using Newtonsoft.Json;

namespace Contraption.Domain.Blueprints
{
    /// <summary>
    /// Per-part tuning the player has chosen — motor speed on a powered wheel, stiffness on a
    /// spring, and so on. Kept as an open key/value map rather than a fixed set of fields because
    /// which knobs exist is a property of the *part catalog* (Milestone 3), not of the blueprint
    /// format, and the format should not need a schema bump every time a part gains a setting.
    ///
    /// This map is also the concrete reason the project takes a dependency on Newtonsoft:
    /// `JsonUtility` cannot serialise dictionaries (`ARCHITECTURE.md` §4).
    /// </summary>
    public sealed record PartConfiguration
    {
        private readonly Dictionary<string, float> _values;

        [JsonConstructor]
        public PartConfiguration(IDictionary<string, float>? values)
        {
            _values = values is null
                ? new Dictionary<string, float>()
                : new Dictionary<string, float>(values);
        }

        /// <summary>
        /// A part with nothing tuned. Shared, because it is immutable.
        /// The cast disambiguates against the copy constructor records generate.
        /// </summary>
        public static PartConfiguration Empty { get; } = new PartConfiguration((IDictionary<string, float>?)null);

        public IReadOnlyDictionary<string, float> Values => _values;

        public bool TryGetValue(string key, out float value) => _values.TryGetValue(key, out value);

        /// <summary>Returns a new configuration; the existing one is never mutated.</summary>
        public PartConfiguration With(string key, float value)
        {
            var copy = new Dictionary<string, float>(_values) { [key] = value };
            return new PartConfiguration(copy);
        }

        public bool Equals(PartConfiguration? other)
        {
            if (other is null)
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            if (_values.Count != other._values.Count)
            {
                return false;
            }

            foreach (KeyValuePair<string, float> entry in _values)
            {
                if (!other._values.TryGetValue(entry.Key, out float otherValue))
                {
                    return false;
                }

                if (!entry.Value.Equals(otherValue))
                {
                    return false;
                }
            }

            return true;
        }

        public override int GetHashCode()
        {
            // Order-independent, because dictionary iteration order is not part of the value.
            int hash = _values.Count;
            foreach (KeyValuePair<string, float> entry in _values)
            {
                hash ^= System.HashCode.Combine(entry.Key, entry.Value);
            }

            return hash;
        }
    }
}
