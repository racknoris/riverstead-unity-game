using System;

namespace Contraption.Domain.Flow
{
    /// <summary>
    /// The phase state machine for the build → run → succeed or fail → modify loop
    /// (`ARCHITECTURE.md` §6.3).
    ///
    /// Plain C# with plain C# events, deliberately: no ScriptableObject event channels, no
    /// framework. It owns *which phase the game is in* and nothing else — it does not know about
    /// bodies, time, input or scenes. The view layer drives it and listens to it.
    ///
    /// Illegal transitions are rejected rather than ignored. A caller that tries to pause a run
    /// that is not running has a bug, and silently doing nothing would hide it — the same
    /// "silent rejection is a defect" rule the editor follows in Milestone 7.
    /// </summary>
    public sealed class GameFlow
    {
        private RunResult? _lastResult;

        /// <summary>Raised after the phase changes, with the phase just left and the one entered.</summary>
        public event Action<GamePhase, GamePhase>? PhaseChanged;

        /// <summary>Raised when a run ends, whether it succeeded or failed.</summary>
        public event Action<RunResult>? RunEnded;

        public GamePhase Phase { get; private set; } = GamePhase.Editing;

        /// <summary>The most recent run's result, or null if no run has finished this session.</summary>
        public RunResult? LastResult => _lastResult;

        public bool CanStartRun => Phase == GamePhase.Editing;

        public bool CanPause => Phase == GamePhase.Running;

        public bool CanResume => Phase == GamePhase.Paused;

        /// <summary>A run can be abandoned at any point once it has left the editor.</summary>
        public bool CanReset => Phase != GamePhase.Editing;

        public void StartRun()
        {
            Require(CanStartRun, "start a run", "the editor");
            _lastResult = null;
            MoveTo(GamePhase.Running);
        }

        public void Pause()
        {
            Require(CanPause, "pause", "a running simulation");
            MoveTo(GamePhase.Paused);
        }

        public void Resume()
        {
            Require(CanResume, "resume", "a paused simulation");
            MoveTo(GamePhase.Running);
        }

        /// <summary>
        /// Abandons the current run and returns to editing. This is the "reset" of
        /// `ARCHITECTURE.md` §8 as the domain sees it; destroying and rebuilding the simulation is
        /// the view layer's half of the same action.
        /// </summary>
        public void Reset()
        {
            Require(CanReset, "reset", "a run that has started");
            _lastResult = null;
            MoveTo(GamePhase.Editing);
        }

        public void Complete(RunResult result) => End(result, GamePhase.Completed, RunOutcome.Completed);

        public void Fail(RunResult result) => End(result, GamePhase.Failed, RunOutcome.Failed);

        /// <summary>Returns to the editor after a finished run, so the player can modify and retry.</summary>
        public void ReturnToEditing()
        {
            Require(
                Phase == GamePhase.Completed || Phase == GamePhase.Failed,
                "return to editing",
                "a finished run");
            MoveTo(GamePhase.Editing);
        }

        private void End(RunResult result, GamePhase phase, RunOutcome expectedOutcome)
        {
            if (result is null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            if (result.Outcome != expectedOutcome)
            {
                throw new ArgumentException(
                    $"A {phase} phase needs a {expectedOutcome} result, but was given {result.Outcome}.",
                    nameof(result));
            }

            // A run can end while paused - a timeout resolved from a paused inspection, say.
            Require(
                Phase == GamePhase.Running || Phase == GamePhase.Paused,
                "end a run",
                "a run in progress");

            _lastResult = result;
            MoveTo(phase);
            RunEnded?.Invoke(result);
        }

        private void MoveTo(GamePhase next)
        {
            GamePhase previous = Phase;
            Phase = next;
            PhaseChanged?.Invoke(previous, next);
        }

        private void Require(bool allowed, string action, string requiredState)
        {
            if (!allowed)
            {
                throw new InvalidOperationException(
                    $"Cannot {action} from the {Phase} phase; it requires {requiredState}.");
            }
        }
    }
}
