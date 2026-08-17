using System;
using Contraption.Domain.Blueprints;
using Contraption.Runtime.Views;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Contraption.UI
{
    /// <summary>
    /// Turns a tap into either "the player picked this hole" or "the player picked this part".
    ///
    /// Hit-testing is done against the preview's own marker list in world space rather than with
    /// physics queries, because the preview has no colliders — it is a drawing, not a machine.
    /// Nearest-within-a-radius also gives a forgiving hit area, which matters far more with a
    /// thumb than with a mouse.
    /// </summary>
    public sealed class EditorTouchInput : MonoBehaviour
    {
        /// <summary>
        /// How close a tap must land, in world units. Generous on purpose: a fingertip covers
        /// roughly a centimetre, and an editor that demands precision is an editor nobody enjoys.
        /// </summary>
        private const float HoleTapRadius = 0.5f;

        /// <summary>Parts are only hit if no hole was, so this can be tighter.</summary>
        private const float PartTapRadius = 0.6f;

        private Camera _camera = null!;
        private BlueprintPreview _preview = null!;
        private Func<ContraptionBlueprint?> _blueprint = null!;
        private UIDocument[] _uiDocuments = new UIDocument[0];

        public event Action<PartId, HoleId>? HoleTapped;

        public event Action<PartId>? PartTapped;

        /// <summary>Raised when the player taps empty space, which means "deselect".</summary>
        public event Action? EmptySpaceTapped;

        public void Initialise(
            Camera camera,
            BlueprintPreview preview,
            Func<ContraptionBlueprint?> blueprint,
            params UIDocument[] uiDocuments)
        {
            _camera = camera;
            _preview = preview;
            _blueprint = blueprint;
            _uiDocuments = uiDocuments ?? new UIDocument[0];
        }

        /// <summary>Input is read in Update (`docs/CONVENTIONS.md`).</summary>
        private void Update()
        {
            if (!TryReadTap(out Vector2 screenPosition))
            {
                return;
            }

            // A tap that lands on a button is not a tap on the world. Without this, choosing a
            // part from the palette also registered as a tap on empty space, which cleared the
            // pending hole before the button's own callback ran - so the palette closed and
            // nothing was placed.
            if (IsOverUserInterface(screenPosition))
            {
                return;
            }

            Vector2 world = _camera.ScreenToWorldPoint(screenPosition);

            if (TryFindHole(world, out BlueprintPreview.HoleMarker marker))
            {
                HoleTapped?.Invoke(marker.PartId, marker.HoleId);
                return;
            }

            if (TryFindPart(world, out PartId partId))
            {
                PartTapped?.Invoke(partId);
                return;
            }

            EmptySpaceTapped?.Invoke();
        }

        /// <summary>
        /// Reads a tap from touch or mouse. Both are handled because the game is developed on a
        /// desktop and judged on a phone (`ARCHITECTURE.md` §3), and an editor that only responds
        /// to one of them cannot be iterated on.
        /// </summary>
        private static bool TryReadTap(out Vector2 screenPosition)
        {
            Touchscreen touchscreen = Touchscreen.current;
            if (touchscreen != null && touchscreen.primaryTouch.press.wasPressedThisFrame)
            {
                screenPosition = touchscreen.primaryTouch.position.ReadValue();
                return true;
            }

            Mouse mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame)
            {
                screenPosition = mouse.position.ReadValue();
                return true;
            }

            screenPosition = default;
            return false;
        }

        private bool IsOverUserInterface(Vector2 screenPosition)
        {
            foreach (UIDocument document in _uiDocuments)
            {
                if (document == null || !document.isActiveAndEnabled)
                {
                    continue;
                }

                IPanel? panel = document.rootVisualElement?.panel;
                if (panel == null)
                {
                    continue;
                }

                // Panel space has its origin at the top-left, screen space at the bottom-left.
                Vector2 panelPosition = RuntimePanelUtils.ScreenToPanel(
                    panel, new Vector2(screenPosition.x, Screen.height - screenPosition.y));

                if (panel.Pick(panelPosition) != null)
                {
                    return true;
                }
            }

            return false;
        }

        private bool TryFindHole(Vector2 world, out BlueprintPreview.HoleMarker found)
        {
            found = default;
            float bestDistance = HoleTapRadius;
            bool hit = false;

            foreach (BlueprintPreview.HoleMarker marker in _preview.HoleMarkers)
            {
                float distance = Vector2.Distance(world, marker.WorldPosition);
                if (distance <= bestDistance)
                {
                    bestDistance = distance;
                    found = marker;
                    hit = true;
                }
            }

            return hit;
        }

        private bool TryFindPart(Vector2 world, out PartId found)
        {
            found = default;
            ContraptionBlueprint? blueprint = _blueprint();
            if (blueprint == null)
            {
                return false;
            }

            float bestDistance = PartTapRadius;
            bool hit = false;

            foreach (PlacedPart part in blueprint.Parts)
            {
                float distance = Vector2.Distance(world, new Vector2(part.Position.X, part.Position.Y));
                if (distance <= bestDistance)
                {
                    bestDistance = distance;
                    found = part.Id;
                    hit = true;
                }
            }

            return hit;
        }
    }
}
