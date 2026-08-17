using System;
using System.Collections.Generic;
using Contraption.Domain.Blueprints;
using UnityEngine;
using UnityEngine.UIElements;

namespace Contraption.UI
{
    /// <summary>
    /// The editing UI: a part palette that appears when a hole is chosen, a panel describing the
    /// selected part, and a line for anything the editor refused to do.
    ///
    /// The rejection line is not decoration. Silent rejection was a recorded failure of the
    /// previous project (`docs/ISSUES.md`); `EditResult` makes a refusal impossible to ignore in
    /// code, and this is where it becomes impossible to miss on screen.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class EditorPanelView : MonoBehaviour
    {
        /// <summary>How long a refusal stays on screen. Long enough to read, short enough not to nag.</summary>
        private const float RejectionSeconds = 3.5f;

        private static readonly PartType[] PlaceablePartTypes =
        {
            PartType.Wheel,
            PartType.PoweredWheel,
            PartType.Beam,
            PartType.RigidConnector,
            PartType.Hinge,
            PartType.Spring,
            PartType.ProtectivePlate
        };

        private VisualElement _palette = null!;
        private VisualElement _selectionPanel = null!;
        private Label _selectionLabel = null!;
        private Label _rejectionLabel = null!;
        private Button _rotateButton = null!;
        private Button _removeButton = null!;
        private float _rejectionUntil;

        public event Action<PartType>? PartTypeChosen;

        public event Action? RotateRequested;

        public event Action? RemoveRequested;

        public event Action? PaletteDismissed;

        private void OnEnable()
        {
            VisualElement root = GetComponent<UIDocument>().rootVisualElement;
            root.Clear();
            root.pickingMode = PickingMode.Ignore;
            Build(root);
            HidePalette();
            ShowSelection(null, null);
        }

        private void Update()
        {
            if (_rejectionUntil > 0f && Time.unscaledTime > _rejectionUntil)
            {
                _rejectionUntil = 0f;
                _rejectionLabel.text = string.Empty;
            }
        }

        private void Build(VisualElement root)
        {
            root.style.flexDirection = FlexDirection.Column;
            root.style.justifyContent = Justify.FlexEnd;

            _rejectionLabel = new Label(string.Empty);
            _rejectionLabel.style.color = new Color(1f, 0.55f, 0.55f);
            _rejectionLabel.style.fontSize = 18;
            _rejectionLabel.style.marginBottom = 8;
            _rejectionLabel.style.marginLeft = 16;
            _rejectionLabel.style.unityTextAlign = TextAnchor.MiddleLeft;

            _selectionPanel = BuildSelectionPanel();
            _palette = BuildPalette();

            root.Add(_rejectionLabel);
            root.Add(_selectionPanel);
            root.Add(_palette);
        }

        private VisualElement BuildPalette()
        {
            var palette = new VisualElement();
            palette.style.flexDirection = FlexDirection.Row;
            palette.style.flexWrap = Wrap.Wrap;
            palette.style.backgroundColor = new Color(0f, 0f, 0f, 0.65f);
            palette.style.paddingTop = 10;
            palette.style.paddingBottom = 10;
            palette.style.paddingLeft = 10;
            palette.style.paddingRight = 10;

            foreach (PartType partType in PlaceablePartTypes)
            {
                PartType captured = partType;
                var button = new Button(() => PartTypeChosen?.Invoke(captured))
                {
                    text = Readable(partType)
                };
                button.style.minWidth = 130;
                button.style.minHeight = 56;
                button.style.fontSize = 16;
                palette.Add(button);
            }

            var cancel = new Button(() => PaletteDismissed?.Invoke()) { text = "Cancel" };
            cancel.style.minWidth = 110;
            cancel.style.minHeight = 56;
            cancel.style.fontSize = 16;
            palette.Add(cancel);

            return palette;
        }

        private VisualElement BuildSelectionPanel()
        {
            var panel = new VisualElement();
            panel.style.flexDirection = FlexDirection.Row;
            panel.style.alignItems = Align.Center;
            panel.style.backgroundColor = new Color(0f, 0f, 0f, 0.6f);
            panel.style.paddingTop = 8;
            panel.style.paddingBottom = 8;
            panel.style.paddingLeft = 14;
            panel.style.paddingRight = 14;
            panel.style.alignSelf = Align.FlexStart;
            panel.style.marginLeft = 16;
            panel.style.marginBottom = 8;

            _selectionLabel = new Label(string.Empty);
            _selectionLabel.style.color = Color.white;
            _selectionLabel.style.fontSize = 17;
            _selectionLabel.style.minWidth = 210;

            _rotateButton = new Button(() => RotateRequested?.Invoke()) { text = "Rotate 30°" };
            _rotateButton.style.minHeight = 48;
            _rotateButton.style.minWidth = 120;

            _removeButton = new Button(() => RemoveRequested?.Invoke()) { text = "Remove" };
            _removeButton.style.minHeight = 48;
            _removeButton.style.minWidth = 110;

            panel.Add(_selectionLabel);
            panel.Add(_rotateButton);
            panel.Add(_removeButton);
            return panel;
        }

        public void ShowPalette() => _palette.style.display = DisplayStyle.Flex;

        public void HidePalette() => _palette.style.display = DisplayStyle.None;

        /// <summary>
        /// Describes the selected part, or hides the panel when nothing is selected. Also disables
        /// the actions the chassis does not support, rather than offering them and refusing.
        /// </summary>
        public void ShowSelection(PlacedPart? part, string? partDisplayName, bool isChassis = false)
        {
            if (part is null)
            {
                _selectionPanel.style.display = DisplayStyle.None;
                return;
            }

            _selectionPanel.style.display = DisplayStyle.Flex;
            _selectionLabel.text = $"{partDisplayName ?? part.Type.ToString()}   {part.Rotation.Degrees}°";
            _rotateButton.SetEnabled(!isChassis);
            _removeButton.SetEnabled(!isChassis);
        }

        public void ShowRejection(string reason)
        {
            _rejectionLabel.text = reason;
            _rejectionUntil = Time.unscaledTime + RejectionSeconds;
        }

        public void SetVisible(bool visible) =>
            GetComponent<UIDocument>().rootVisualElement.style.display =
                visible ? DisplayStyle.Flex : DisplayStyle.None;

        private static string Readable(PartType partType)
        {
            switch (partType)
            {
                case PartType.PoweredWheel: return "Powered Wheel";
                case PartType.RigidConnector: return "Connector";
                case PartType.ProtectivePlate: return "Plate";
                default: return partType.ToString();
            }
        }
    }
}
