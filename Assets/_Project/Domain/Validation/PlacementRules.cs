using System.Collections.Generic;
using Contraption.Domain.Blueprints;

namespace Contraption.Domain.Validation
{
    /// <summary>
    /// Decides whether a machine the player is about to have is allowed
    /// (`docs/TASKS.md` Milestone 7).
    ///
    /// Every refusal returns a sentence written for the player, not a code. "Silent rejection is
    /// a defect" is a recorded lesson from the previous project (`docs/ISSUES.md`), and the
    /// weaker version of that failure — refusing with a message only a programmer understands —
    /// is just as unhelpful in the hand.
    ///
    /// The rules are deliberately few and predictable. An editor that refuses for reasons the
    /// player cannot anticipate is worse than one that permits an ugly machine: the physics will
    /// tell them soon enough, and it will be more fun when it does.
    /// </summary>
    public static class PlacementRules
    {
        /// <summary>
        /// How much two parts' bounding circles may interpenetrate before it counts as overlap.
        ///
        /// Bounding circles are generous — a box's is its half-diagonal — so comparing them
        /// directly would reject placements that are visibly fine. This factor buys that slack
        /// back. It is a feel value: too strict and the editor nags, too loose and parts stack.
        /// </summary>
        private const float OverlapAllowance = 0.6f;

        /// <summary>
        /// Returns a player-readable reason the machine is not allowed, or null if it is fine.
        /// </summary>
        public static string? FindProblem(
            ContraptionBlueprint candidate,
            IReadOnlyDictionary<PartType, PartDefinition> definitions,
            int maxParts)
        {
            return FindPartLimitProblem(candidate, maxParts) ?? FindOverlapProblem(candidate, definitions);
        }

        /// <summary>
        /// The chassis is not counted. It is not a part the player chose to spend — it is the
        /// thing they are building on, and charging for it would make the budget confusing.
        /// </summary>
        public static int CountPlayerParts(ContraptionBlueprint blueprint)
        {
            int count = 0;
            foreach (PlacedPart part in blueprint.Parts)
            {
                if (part.Type != PartType.Chassis)
                {
                    count++;
                }
            }

            return count;
        }

        private static string? FindPartLimitProblem(ContraptionBlueprint candidate, int maxParts)
        {
            int used = CountPlayerParts(candidate);
            return used > maxParts
                ? $"This level allows {maxParts} parts, and that would make {used}. Remove something first."
                : null;
        }

        private static string? FindOverlapProblem(
            ContraptionBlueprint candidate,
            IReadOnlyDictionary<PartType, PartDefinition> definitions)
        {
            HashSet<(PartId, PartId)> connected = ConnectedPairs(candidate);

            for (int i = 0; i < candidate.Parts.Count; i++)
            {
                for (int j = i + 1; j < candidate.Parts.Count; j++)
                {
                    PlacedPart first = candidate.Parts[i];
                    PlacedPart second = candidate.Parts[j];

                    // Attached parts meet at a hole by design; that is contact, not overlap.
                    if (connected.Contains((first.Id, second.Id)) || connected.Contains((second.Id, first.Id)))
                    {
                        continue;
                    }

                    if (!definitions.TryGetValue(first.Type, out PartDefinition firstDefinition)
                        || !definitions.TryGetValue(second.Type, out PartDefinition secondDefinition))
                    {
                        continue;
                    }

                    float allowed =
                        (firstDefinition.Shape.BoundingRadius + secondDefinition.Shape.BoundingRadius)
                        * OverlapAllowance;

                    float dx = first.Position.X - second.Position.X;
                    float dy = first.Position.Y - second.Position.Y;

                    if ((dx * dx) + (dy * dy) < allowed * allowed)
                    {
                        return $"That would put the {firstDefinition.DisplayName} inside the "
                            + $"{secondDefinition.DisplayName}.";
                    }
                }
            }

            return null;
        }

        private static HashSet<(PartId, PartId)> ConnectedPairs(ContraptionBlueprint blueprint)
        {
            var pairs = new HashSet<(PartId, PartId)>();
            foreach (Attachment attachment in blueprint.Attachments)
            {
                pairs.Add((attachment.FromPartId, attachment.ToPartId));
            }

            return pairs;
        }
    }
}
