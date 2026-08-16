using System;
using System.Collections.Generic;
using Contraption.Domain.Flow;
using NUnit.Framework;

namespace Contraption.Tests.EditMode
{
    /// <summary>
    /// Milestone 5: the phase machine. Pure domain, so the whole loop is testable without Unity.
    /// </summary>
    public sealed class GameFlowTests
    {
        private GameFlow _flow = null!;

        [SetUp]
        public void SetUp() => _flow = new GameFlow();

        [Test]
        public void Phase_Initially_IsEditing()
        {
            Assert.That(_flow.Phase, Is.EqualTo(GamePhase.Editing));
        }

        [Test]
        public void FullCycle_EditRunCompleteReturn_EndsBackInEditing()
        {
            _flow.StartRun();
            Assert.That(_flow.Phase, Is.EqualTo(GamePhase.Running));

            _flow.Pause();
            Assert.That(_flow.Phase, Is.EqualTo(GamePhase.Paused));

            _flow.Resume();
            Assert.That(_flow.Phase, Is.EqualTo(GamePhase.Running));

            _flow.Complete(Completed());
            Assert.That(_flow.Phase, Is.EqualTo(GamePhase.Completed));

            _flow.ReturnToEditing();
            Assert.That(_flow.Phase, Is.EqualTo(GamePhase.Editing));
        }

        [Test]
        public void PhaseChanged_OnEveryTransition_ReportsBothPhases()
        {
            var transitions = new List<(GamePhase From, GamePhase To)>();
            _flow.PhaseChanged += (from, to) => transitions.Add((from, to));

            _flow.StartRun();
            _flow.Reset();

            Assert.That(transitions, Is.EqualTo(new[]
            {
                (GamePhase.Editing, GamePhase.Running),
                (GamePhase.Running, GamePhase.Editing)
            }));
        }

        [Test]
        public void RunEnded_OnCompletion_CarriesTheResult()
        {
            RunResult? received = null;
            _flow.RunEnded += result => received = result;
            RunResult expected = Completed();

            _flow.StartRun();
            _flow.Complete(expected);

            Assert.That(received, Is.SameAs(expected));
            Assert.That(_flow.LastResult, Is.SameAs(expected));
        }

        [Test]
        public void Fail_FromRunning_MovesToFailedAndKeepsTheReason()
        {
            _flow.StartRun();

            _flow.Fail(RunResult.Failed("Out of time", 40f, 75f, 6));

            Assert.That(_flow.Phase, Is.EqualTo(GamePhase.Failed));
            Assert.That(_flow.LastResult!.FailureReason, Is.EqualTo("Out of time"));
        }

        [Test]
        public void Fail_WhilePaused_IsAllowed()
        {
            // A timeout can resolve while the player has the run paused for a look.
            _flow.StartRun();
            _flow.Pause();

            Assert.DoesNotThrow(() => _flow.Fail(RunResult.Failed("Out of time", 0f, 75f, 1)));
        }

        // -----------------------------------------------------------------------------------
        // Illegal transitions throw rather than silently doing nothing. A caller that pauses a
        // run which is not running has a bug, and swallowing it would hide the bug.
        // -----------------------------------------------------------------------------------

        [Test]
        public void StartRun_WhenAlreadyRunning_IsRejected()
        {
            _flow.StartRun();

            Assert.Throws<InvalidOperationException>(() => _flow.StartRun());
        }

        [Test]
        public void Pause_WhenEditing_IsRejected()
        {
            Assert.Throws<InvalidOperationException>(() => _flow.Pause());
        }

        [Test]
        public void Resume_WhenRunning_IsRejected()
        {
            _flow.StartRun();

            Assert.Throws<InvalidOperationException>(() => _flow.Resume());
        }

        [Test]
        public void Reset_WhenEditing_IsRejected()
        {
            Assert.Throws<InvalidOperationException>(() => _flow.Reset());
        }

        [Test]
        public void Complete_BeforeARunStarts_IsRejected()
        {
            Assert.Throws<InvalidOperationException>(() => _flow.Complete(Completed()));
        }

        [Test]
        public void Complete_GivenAFailedResult_IsRejected()
        {
            // Guards a genuinely confusing bug: a Completed phase holding a Failed result.
            _flow.StartRun();

            Assert.Throws<ArgumentException>(
                () => _flow.Complete(RunResult.Failed("Cargo destroyed", 0f, 10f, 3)));
        }

        [Test]
        public void Fail_GivenACompletedResult_IsRejected()
        {
            _flow.StartRun();

            Assert.Throws<ArgumentException>(() => _flow.Fail(Completed()));
        }

        [Test]
        public void Reset_AfterARun_ClearsTheLastResult()
        {
            _flow.StartRun();
            _flow.Complete(Completed());

            _flow.Reset();

            Assert.That(_flow.LastResult, Is.Null);
        }

        [Test]
        public void CanFlags_TrackThePhase()
        {
            Assert.That(_flow.CanStartRun, Is.True);
            Assert.That(_flow.CanPause, Is.False);
            Assert.That(_flow.CanReset, Is.False);

            _flow.StartRun();

            Assert.That(_flow.CanStartRun, Is.False);
            Assert.That(_flow.CanPause, Is.True);
            Assert.That(_flow.CanReset, Is.True);
        }

        private static RunResult Completed() =>
            RunResult.Completed(cargoHealthRemaining: 89f, elapsedSeconds: 22.5f, partsUsed: 4);
    }
}
