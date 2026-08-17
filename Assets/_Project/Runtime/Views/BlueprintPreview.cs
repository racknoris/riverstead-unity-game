using System.Collections.Generic;
using Contraption.Domain.Blueprints;
using Contraption.Runtime.Catalog;
using UnityEngine;

namespace Contraption.Runtime.Views
{
    /// <summary>
    /// Draws a blueprint while the player is editing it: parts as sprites, free holes as tappable
    /// markers. No rigidbodies, no joints, nothing that moves.
    ///
    /// This is deliberately *not* a paused simulation. Those are dynamic bodies with motors —
    /// left standing they would settle under gravity and drive themselves away. An editor needs a
    /// drawing of the machine, which is a different object from a machine.
    ///
    /// Rebuilt wholesale on every edit rather than diffed. A dozen parts is nothing to rebuild,
    /// and "destroy and redraw from the blueprint" is the same discipline the simulation follows
    /// (`ARCHITECTURE.md` §8) — there is no incremental state to get out of step.
    /// </summary>
    public sealed class BlueprintPreview : MonoBehaviour
    {
        /// <summary>Radius of a hole marker, in world units. Big enough to hit with a thumb.</summary>
        private const float HoleMarkerRadius = 0.22f;

        private static readonly Color HoleColour = new Color(0.95f, 0.85f, 0.35f, 0.85f);
        private static readonly Color SelectedTint = new Color(1f, 1f, 1f, 1f);
        private static readonly Color UnselectedTint = new Color(0.75f, 0.75f, 0.75f, 1f);

        private PartCatalog _catalog = null!;
        private readonly List<HoleMarker> _holeMarkers = new List<HoleMarker>();
        private readonly Dictionary<PartId, Transform> _partObjects = new Dictionary<PartId, Transform>();

        /// <summary>A free hole, and where it ended up in world space, so input can hit-test it.</summary>
        public readonly struct HoleMarker
        {
            public HoleMarker(PartId partId, HoleId holeId, Vector2 worldPosition)
            {
                PartId = partId;
                HoleId = holeId;
                WorldPosition = worldPosition;
            }

            public PartId PartId { get; }

            public HoleId HoleId { get; }

            public Vector2 WorldPosition { get; }
        }

        public IReadOnlyList<HoleMarker> HoleMarkers => _holeMarkers;

        public void Initialise(PartCatalog catalog) => _catalog = catalog;

        /// <summary>Clears and redraws everything from the blueprint.</summary>
        public void Render(ContraptionBlueprint blueprint, PartId? selectedPartId)
        {
            Clear();
            if (blueprint == null || _catalog == null)
            {
                return;
            }

            foreach (PlacedPart part in blueprint.Parts)
            {
                DrawPart(part, selectedPartId.HasValue && part.Id == selectedPartId.Value);
            }

            DrawFreeHoles(blueprint);
        }

        public void Clear()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Transform child = transform.GetChild(i);
                if (Application.isPlaying)
                {
                    Destroy(child.gameObject);
                }
                else
                {
                    DestroyImmediate(child.gameObject);
                }
            }

            _holeMarkers.Clear();
            _partObjects.Clear();
        }

        private void DrawPart(PlacedPart part, bool isSelected)
        {
            if (!_catalog.TryGetDefinition(part.Type, out PartDefinitionAsset definition))
            {
                return;
            }

            var partObject = new GameObject($"Preview_{part.Type}_{part.Id}");
            partObject.transform.SetParent(transform, worldPositionStays: false);
            partObject.transform.localPosition = new Vector3(part.Position.X, part.Position.Y, 0f);
            partObject.transform.localRotation = Quaternion.Euler(0f, 0f, part.Rotation.Degrees);
            _partObjects[part.Id] = partObject.transform;

            bool isRound = definition.Radius > 0f;
            var visual = new GameObject("Sprite");
            visual.transform.SetParent(partObject.transform, worldPositionStays: false);
            visual.transform.localScale = isRound
                ? new Vector3(definition.Radius * 2f, definition.Radius * 2f, 1f)
                : new Vector3(definition.Size.x, definition.Size.y, 1f);

            SpriteRenderer renderer = visual.AddComponent<SpriteRenderer>();
            renderer.sprite = isRound ? PrimitiveSprites.Circle() : PrimitiveSprites.Square();
            renderer.color = PartPalette.ColourFor(part.Type) * (isSelected ? SelectedTint : UnselectedTint);
            renderer.sortingOrder = isSelected ? 2 : 0;
        }

        /// <summary>
        /// Markers are drawn for holes with nothing attached. An occupied hole is not a target —
        /// showing one the player cannot use is a rejection waiting to happen.
        /// </summary>
        private void DrawFreeHoles(ContraptionBlueprint blueprint)
        {
            // Both sides: a child's mount hole is in use too, or a wheel's axle would be drawn
            // as an available target when it is already bolted to the chassis.
            var occupied = new HashSet<(PartId, HoleId)>();
            foreach (Attachment attachment in blueprint.Attachments)
            {
                occupied.Add((attachment.FromPartId, attachment.FromHoleId));
                occupied.Add((attachment.ToPartId, attachment.ToHoleId));
            }

            foreach (PlacedPart part in blueprint.Parts)
            {
                if (!_catalog.TryGetDefinition(part.Type, out PartDefinitionAsset definition))
                {
                    continue;
                }

                PartDefinition domainDefinition = definition.ToDomainDefinition();
                if (domainDefinition == null)
                {
                    continue;
                }

                foreach (AttachmentHole hole in domainDefinition.AttachmentHoles)
                {
                    if (occupied.Contains((part.Id, hole.Id)))
                    {
                        continue;
                    }

                    Vector2 world = ToWorld(part, hole.LocalPosition);
                    DrawHoleMarker(world);
                    _holeMarkers.Add(new HoleMarker(part.Id, hole.Id, world));
                }
            }
        }

        /// <summary>The same transform the domain's layout pass applies, expressed in Unity types.</summary>
        private static Vector2 ToWorld(PlacedPart part, EditorPosition holeLocal)
        {
            float radians = part.Rotation.Degrees * Mathf.Deg2Rad;
            float cos = Mathf.Cos(radians);
            float sin = Mathf.Sin(radians);

            return new Vector2(
                part.Position.X + ((holeLocal.X * cos) - (holeLocal.Y * sin)),
                part.Position.Y + ((holeLocal.X * sin) + (holeLocal.Y * cos)));
        }

        private void DrawHoleMarker(Vector2 worldPosition)
        {
            var marker = new GameObject("Hole");
            marker.transform.SetParent(transform, worldPositionStays: false);
            marker.transform.localPosition = worldPosition;
            marker.transform.localScale = new Vector3(HoleMarkerRadius * 2f, HoleMarkerRadius * 2f, 1f);

            SpriteRenderer renderer = marker.AddComponent<SpriteRenderer>();
            renderer.sprite = PrimitiveSprites.Circle();
            renderer.color = HoleColour;
            renderer.sortingOrder = 5;
        }
    }
}
