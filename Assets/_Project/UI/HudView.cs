using System;
using Contraption.Domain.Flow;
using UnityEngine;
using UnityEngine.UIElements;

namespace Contraption.UI
{
    /// <summary>
    /// The run HUD: run, pause, reset, and a status line.
    ///
    /// Built in code rather than from a UXML asset. For a HUD this small, a second file to keep
    /// in sync buys nothing, and the layout is easier to review as code. When the editor panels
    /// of Milestone 6 arrive and the UI stops being three buttons, UXML earns its place.
    ///
    /// This class knows the phase machine but not the simulation: it raises requests and shows
    /// state. It never builds a body or touches physics.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class HudView : MonoBehaviour
    {
        private Button _runButton = null!;
        private Button _pauseButton = null!;
        private Button _resetButton = null!;
        private Label _statusLabel = null!;
        private Label _resultLabel = null!;

        /// <summary>Imperative names: these are requests, not facts (`docs/CONVENTIONS.md`).</summary>
        public event Action? RunRequested;

        public event Action? PauseRequested;

        public event Action? ResetRequested;

        private void OnEnable()
        {
            VisualElement root = GetComponent<UIDocument>().rootVisualElement;
            root.Clear();
            Build(root);
        }

        private void Build(VisualElement root)
        {
            // Everything sits in one top row. The bottom belongs to the editor palette, and the
            // two were overlapping - Run/Pause/Reset drew on top of the part buttons.
            root.style.flexDirection = FlexDirection.Row;
            root.style.justifyContent = Justify.SpaceBetween;
            root.style.alignItems = Align.FlexStart;
            root.pickingMode = PickingMode.Ignore;

            VisualElement statusPanel = Panel();
            _statusLabel = TextLine("Editing", 20);
            _resultLabel = TextLine(string.Empty, 16);
            statusPanel.Add(_statusLabel);
            statusPanel.Add(_resultLabel);

            var buttonRow = new VisualElement();
            buttonRow.style.flexDirection = FlexDirection.Row;
            buttonRow.style.justifyContent = Justify.FlexEnd;
            buttonRow.style.marginTop = 16;
            buttonRow.style.marginRight = 16;

            _runButton = TouchButton("Run", () => RunRequested?.Invoke());
            _pauseButton = TouchButton("Pause", () => PauseRequested?.Invoke());
            _resetButton = TouchButton("Reset", () => ResetRequested?.Invoke());
            buttonRow.Add(_runButton);
            buttonRow.Add(_pauseButton);
            buttonRow.Add(_resetButton);

            root.Add(statusPanel);
            root.Add(buttonRow);
        }

        private static VisualElement Panel()
        {
            var panel = new VisualElement();
            panel.style.marginTop = 16;
            panel.style.marginLeft = 16;
            panel.style.paddingTop = 10;
            panel.style.paddingBottom = 10;
            panel.style.paddingLeft = 14;
            panel.style.paddingRight = 14;
            panel.style.backgroundColor = new Color(0f, 0f, 0f, 0.55f);
            panel.style.alignSelf = Align.FlexStart;
            panel.pickingMode = PickingMode.Ignore;
            return panel;
        }

        private static Label TextLine(string text, int fontSize)
        {
            var label = new Label(text);
            label.style.color = Color.white;
            label.style.fontSize = fontSize;
            return label;
        }

        /// <summary>
        /// Minimum 44 points on the short edge — below that a control is unreliable under a
        /// thumb, and Android is the platform where touch feel is judged (`ARCHITECTURE.md` §3).
        /// </summary>
        private static Button TouchButton(string text, Action onClick)
        {
            var button = new Button(onClick) { text = text };
            button.style.minWidth = 120;
            button.style.minHeight = 56;
            button.style.fontSize = 18;
            button.style.marginLeft = 8;
            return button;
        }

        /// <summary>
        /// Shows the phase. Buttons are *disabled* rather than hidden, so the control set does not
        /// jump around under the player's thumb between phases.
        /// </summary>
        public void Show(GameFlow flow, float elapsedSeconds, float timeLimitSeconds, bool hasMachine = true)
        {
            _runButton.SetEnabled(flow.CanStartRun && hasMachine);
            _pauseButton.SetEnabled(flow.CanPause || flow.CanResume);
            _pauseButton.text = flow.CanResume ? "Resume" : "Pause";
            _resetButton.SetEnabled(flow.CanReset);

            _statusLabel.text = flow.Phase switch
            {
                GamePhase.Editing => hasMachine
                    ? "Editing — press Run"
                    : "Tap a hole on the chassis to add a part",
                GamePhase.Running => $"Running — {elapsedSeconds:F1}s / {timeLimitSeconds:F0}s",
                GamePhase.Paused => $"Paused — {elapsedSeconds:F1}s",
                GamePhase.Completed => "Completed",
                GamePhase.Failed => "Failed",
                _ => flow.Phase.ToString()
            };

            RunResult? result = flow.LastResult;
            if (result is null)
            {
                _resultLabel.text = string.Empty;
                return;
            }

            _resultLabel.text = result.Outcome == RunOutcome.Completed
                ? $"Finished in {result.ElapsedSeconds:F1}s using {result.PartsUsed} parts"
                : $"{result.FailureReason} — after {result.ElapsedSeconds:F1}s";
            _resultLabel.style.color = result.Outcome == RunOutcome.Completed
                ? new Color(0.5f, 0.9f, 0.55f)
                : new Color(0.95f, 0.5f, 0.5f);
        }
    }
}
