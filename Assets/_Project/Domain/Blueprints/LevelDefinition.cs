using System;

namespace Contraption.Domain.Blueprints
{
    /// <summary>
    /// The rules of one course, as the domain needs them: how long a run may take and how many
    /// parts the player may spend. Terrain, obstacles and the launcher are Unity-layer content
    /// (Milestone 8); nothing about their shape belongs here.
    /// </summary>
    public sealed record LevelDefinition
    {
        public LevelDefinition(
            string levelId,
            string displayName,
            float timeLimitSeconds,
            int maxParts)
        {
            if (string.IsNullOrWhiteSpace(levelId))
            {
                throw new ArgumentException("A level needs a stable id.", nameof(levelId));
            }

            if (string.IsNullOrWhiteSpace(displayName))
            {
                throw new ArgumentException("A level needs a display name.", nameof(displayName));
            }

            if (timeLimitSeconds <= 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(timeLimitSeconds), timeLimitSeconds, "A level time limit must be positive.");
            }

            if (maxParts <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maxParts), maxParts, "A level must allow at least one part.");
            }

            LevelId = levelId;
            DisplayName = displayName;
            TimeLimitSeconds = timeLimitSeconds;
            MaxParts = maxParts;
        }

        public string LevelId { get; }

        public string DisplayName { get; }

        public float TimeLimitSeconds { get; }

        /// <summary>`ARCHITECTURE.md` §10 sizes this at roughly 12 for the POC.</summary>
        public int MaxParts { get; }
    }
}
