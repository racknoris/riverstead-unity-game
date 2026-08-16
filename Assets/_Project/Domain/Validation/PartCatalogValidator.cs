using System;
using System.Collections.Generic;
using Contraption.Domain.Blueprints;

namespace Contraption.Domain.Validation
{
    /// <summary>
    /// Checks that a set of part definitions forms a usable catalog: every <see cref="PartType"/>
    /// covered, exactly once.
    ///
    /// This lives in the domain rather than beside the ScriptableObject because
    /// `ARCHITECTURE.md` §9 requires the domain to validate the catalog without touching Unity
    /// types. The practical payoff is that the rule is testable without loading an asset, and the
    /// asset test becomes a thin "does the real catalog satisfy the rule" check.
    ///
    /// Returns problems rather than throwing: a catalog with three gaps should report three, not
    /// the first one. An empty result means the catalog is sound.
    /// </summary>
    public static class PartCatalogValidator
    {
        public static IReadOnlyList<string> Validate(IReadOnlyList<PartDefinition?>? definitions)
        {
            var problems = new List<string>();

            if (definitions is null)
            {
                problems.Add("The catalog has no definitions at all.");
                return problems;
            }

            var seen = new Dictionary<PartType, int>();
            for (int i = 0; i < definitions.Count; i++)
            {
                PartDefinition? definition = definitions[i];
                if (definition is null)
                {
                    problems.Add($"Catalog entry {i} is empty.");
                    continue;
                }

                if (seen.TryGetValue(definition.Type, out int firstIndex))
                {
                    problems.Add(
                        $"'{definition.Type}' is defined more than once (entries {firstIndex} and {i}). "
                        + "The catalog is a map from part type to definition, so a duplicate makes the "
                        + "lookup ambiguous.");
                    continue;
                }

                seen.Add(definition.Type, i);
            }

            foreach (PartType required in Enum.GetValues(typeof(PartType)))
            {
                if (!seen.ContainsKey(required))
                {
                    problems.Add(
                        $"'{required}' has no catalog entry. Every part type must be buildable, or a "
                        + "blueprint referencing it cannot be turned into a simulation.");
                }
            }

            return problems;
        }
    }
}
