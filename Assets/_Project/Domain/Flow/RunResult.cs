using System;

namespace Contraption.Domain.Flow
{
    /// <summary>
    /// The immutable outcome of one run, produced when a simulation finishes
    /// (`ARCHITECTURE.md` §6.3).
    ///
    /// It carries the three scoring inputs §2 names — cargo health remaining, elapsed time, and
    /// parts used — as plain numbers. It deliberately does *not* compute a score: how those
    /// combine is a design question that will change repeatedly, and baking one formula into the
    /// result object makes past results unreadable when it does.
    /// </summary>
    public sealed record RunResult
    {
        private RunResult(
            RunOutcome outcome,
            string? failureReason,
            float cargoHealthRemaining,
            float elapsedSeconds,
            int partsUsed)
        {
            if (elapsedSeconds < 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(elapsedSeconds), elapsedSeconds, "A run cannot take negative time.");
            }

            if (partsUsed < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(partsUsed), partsUsed, "A run cannot use a negative number of parts.");
            }

            Outcome = outcome;
            FailureReason = failureReason;
            CargoHealthRemaining = cargoHealthRemaining;
            ElapsedSeconds = elapsedSeconds;
            PartsUsed = partsUsed;
        }

        public RunOutcome Outcome { get; }

        /// <summary>Player-readable, and non-null exactly when <see cref="Outcome"/> is Failed.</summary>
        public string? FailureReason { get; }

        public float CargoHealthRemaining { get; }

        public float ElapsedSeconds { get; }

        public int PartsUsed { get; }

        public static RunResult Completed(float cargoHealthRemaining, float elapsedSeconds, int partsUsed)
        {
            return new RunResult(RunOutcome.Completed, null, cargoHealthRemaining, elapsedSeconds, partsUsed);
        }

        public static RunResult Failed(
            string failureReason,
            float cargoHealthRemaining,
            float elapsedSeconds,
            int partsUsed)
        {
            if (string.IsNullOrWhiteSpace(failureReason))
            {
                // Silent rejection was a recorded failure of the previous project
                // (docs/ISSUES.md). A failed run always says why.
                throw new ArgumentException(
                    "A failed run must give a player-readable reason.", nameof(failureReason));
            }

            return new RunResult(RunOutcome.Failed, failureReason, cargoHealthRemaining, elapsedSeconds, partsUsed);
        }
    }
}
