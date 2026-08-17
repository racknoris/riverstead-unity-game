using System;
using System.Collections.Generic;
using Contraption.Domain.Blueprints;

namespace Contraption.Domain.Editing
{
    /// <summary>
    /// Owns the blueprint being built, and is the only thing that changes it
    /// (`ARCHITECTURE.md` §6.3).
    ///
    /// Every edit returns a *new* blueprint or a typed rejection carrying a player-readable
    /// reason; nothing is mutated in place and nothing fails quietly. The UI calls these methods
    /// and re-renders — it never reaches into a blueprint to adjust a part.
    ///
    /// A contraption is a tree (D11): each part hangs off exactly one parent hole, and the root
    /// is the chassis. Positions are recomputed from the root after every structural change, so a
    /// part's placement can never drift out of agreement with the hole it is attached to.
    ///
    /// The *rules* about what may be placed where arrive in Milestone 7. What lives here is only
    /// what the editor cannot function without: a hole must exist, and must be free.
    /// </summary>
    public sealed class ContraptionEditor
    {
        /// <summary>Twelve positions. Provisional; see `docs/TASKS.md` D10.</summary>
        public const int RotationStepDegrees = 30;

        private readonly IReadOnlyDictionary<PartType, PartDefinition> _definitions;
        private int _nextId = 1;

        public ContraptionEditor(
            ContraptionBlueprint blueprint,
            IReadOnlyDictionary<PartType, PartDefinition> definitions)
        {
            if (blueprint is null)
            {
                throw new ArgumentNullException(nameof(blueprint));
            }

            _definitions = definitions ?? throw new ArgumentNullException(nameof(definitions));
            // A blueprint handed in from outside - loaded from disk, hard-coded in a fixture - is
            // no more trustworthy than one being edited. Normalise it before anyone reads it.
            Blueprint = BlueprintLayout.Normalise(blueprint, _definitions);
        }

        public ContraptionBlueprint Blueprint { get; private set; }

        /// <summary>Raised after an accepted edit, so views can re-render.</summary>
        public event Action<ContraptionBlueprint>? BlueprintChanged;

        /// <summary>
        /// Attaches a new part to a hole on an existing part. Placement and connection are the
        /// same action (D11).
        /// </summary>
        public EditResult PlacePart(PartId parentPartId, HoleId parentHoleId, PartType partType)
        {
            if (!TryFindPart(Blueprint, parentPartId, out PlacedPart parent))
            {
                return EditResult.Reject("That part is no longer on the machine.");
            }

            if (!_definitions.TryGetValue(parent.Type, out PartDefinition parentDefinition)
                || !parentDefinition.TryGetHole(parentHoleId, out AttachmentHole parentHole))
            {
                return EditResult.Reject($"A {parent.Type} has no such attachment point.");
            }

            if (IsHoleOccupied(Blueprint, parentPartId, parentHoleId))
            {
                return EditResult.Reject("Something is already attached there.");
            }

            if (!_definitions.TryGetValue(partType, out PartDefinition definition)
                || definition.AttachmentHoles.Count == 0)
            {
                return EditResult.Reject($"A {partType} cannot be attached to anything.");
            }

            // The child hangs by its first hole. Which end of a beam mounts is a refinement for
            // Milestone 7, not something the editor needs to work.
            AttachmentHole mountHole = definition.AttachmentHoles[0];

            var newPart = new PlacedPart(
                NextPartId(partType),
                partType,
                PartLayout.PositionChild(
                    parent.Position, parent.Rotation, parentHole.LocalPosition,
                    mountHole.LocalPosition, PartRotation.None),
                PartRotation.None);

            var attachment = new Attachment(
                NextAttachmentId(), parentPartId, parentHoleId, newPart.Id, mountHole.Id);

            var parts = new List<PlacedPart>(Blueprint.Parts) { newPart };
            var attachments = new List<Attachment>(Blueprint.Attachments) { attachment };

            return Apply(ContraptionBlueprint.Create(Blueprint.LevelId, parts, attachments));
        }

        /// <summary>
        /// Turns a part by one snap step. The part pivots about its mount hole, so it stays
        /// bolted on, and everything hanging off it moves with it.
        /// </summary>
        public EditResult RotatePart(PartId partId, int steps = 1)
        {
            if (!TryFindPart(Blueprint, partId, out PlacedPart part))
            {
                return EditResult.Reject("That part is no longer on the machine.");
            }

            if (IsRoot(Blueprint, partId))
            {
                return EditResult.Reject("The chassis cannot be rotated.");
            }

            PartRotation rotated = PartRotation.FromDegrees(
                part.Rotation.Degrees + (steps * RotationStepDegrees));

            var parts = new List<PlacedPart>(Blueprint.Parts.Count);
            foreach (PlacedPart existing in Blueprint.Parts)
            {
                parts.Add(existing.Id == partId
                    ? new PlacedPart(existing.Id, existing.Type, existing.Position, rotated, existing.Configuration)
                    : existing);
            }

            return Apply(ContraptionBlueprint.Create(
                Blueprint.LevelId, parts, new List<Attachment>(Blueprint.Attachments)));
        }

        /// <summary>
        /// Removes a part, and everything hanging off it. Detaching is the same action: a part
        /// with no parent is not on the machine.
        ///
        /// Descendants go too, because leaving them would produce parts attached to nothing —
        /// which the builder cannot place and the player did not ask for.
        /// </summary>
        public EditResult RemovePart(PartId partId)
        {
            if (!TryFindPart(Blueprint, partId, out _))
            {
                return EditResult.Reject("That part is no longer on the machine.");
            }

            if (IsRoot(Blueprint, partId))
            {
                return EditResult.Reject("The chassis cannot be removed.");
            }

            HashSet<PartId> doomed = CollectSubtree(Blueprint, partId);

            var parts = new List<PlacedPart>();
            foreach (PlacedPart part in Blueprint.Parts)
            {
                if (!doomed.Contains(part.Id))
                {
                    parts.Add(part);
                }
            }

            var attachments = new List<Attachment>();
            foreach (Attachment attachment in Blueprint.Attachments)
            {
                if (!doomed.Contains(attachment.ToPartId) && !doomed.Contains(attachment.FromPartId))
                {
                    attachments.Add(attachment);
                }
            }

            return Apply(ContraptionBlueprint.Create(Blueprint.LevelId, parts, attachments));
        }

        /// <summary>Replaces a part's configuration, leaving everything else alone.</summary>
        public EditResult ConfigurePart(PartId partId, PartConfiguration configuration)
        {
            if (!TryFindPart(Blueprint, partId, out _))
            {
                return EditResult.Reject("That part is no longer on the machine.");
            }

            var parts = new List<PlacedPart>(Blueprint.Parts.Count);
            foreach (PlacedPart existing in Blueprint.Parts)
            {
                parts.Add(existing.Id == partId
                    ? new PlacedPart(existing.Id, existing.Type, existing.Position, existing.Rotation, configuration)
                    : existing);
            }

            return Apply(ContraptionBlueprint.Create(
                Blueprint.LevelId, parts, new List<Attachment>(Blueprint.Attachments)));
        }

        /// <summary>Holes on a part that nothing is attached to yet.</summary>
        public IReadOnlyList<AttachmentHole> FreeHoles(PartId partId)
        {
            var free = new List<AttachmentHole>();
            if (!TryFindPart(Blueprint, partId, out PlacedPart part)
                || !_definitions.TryGetValue(part.Type, out PartDefinition definition))
            {
                return free;
            }

            foreach (AttachmentHole hole in definition.AttachmentHoles)
            {
                if (!IsHoleOccupied(Blueprint, partId, hole.Id))
                {
                    free.Add(hole);
                }
            }

            return free;
        }

        /// <summary>
        /// Rebuilds every part's position from the attachment tree, then publishes the result.
        /// Running this after each edit is what keeps placement and holes in agreement.
        /// </summary>
        private EditResult Apply(ContraptionBlueprint blueprint)
        {
            Blueprint = BlueprintLayout.Normalise(blueprint, _definitions);
            BlueprintChanged?.Invoke(Blueprint);
            return EditResult.Accept(Blueprint);
        }

        private static bool IsRoot(ContraptionBlueprint blueprint, PartId partId)
        {
            foreach (Attachment attachment in blueprint.Attachments)
            {
                if (attachment.ToPartId == partId)
                {
                    return false;
                }
            }

            return true;
        }

        private static HashSet<PartId> CollectSubtree(ContraptionBlueprint blueprint, PartId rootId)
        {
            var collected = new HashSet<PartId> { rootId };
            var queue = new Queue<PartId>();
            queue.Enqueue(rootId);

            while (queue.Count > 0)
            {
                PartId current = queue.Dequeue();
                foreach (Attachment attachment in blueprint.Attachments)
                {
                    if (attachment.FromPartId == current && collected.Add(attachment.ToPartId))
                    {
                        queue.Enqueue(attachment.ToPartId);
                    }
                }
            }

            return collected;
        }

        /// <summary>
        /// A hole is in use if an attachment touches it from *either* side. Checking only the
        /// parent side leaves a child's own mount hole looking free — a wheel's axle is how the
        /// wheel is bolted on, and offering it as a target would let two parts occupy one point.
        /// </summary>
        private static bool IsHoleOccupied(ContraptionBlueprint blueprint, PartId partId, HoleId holeId)
        {
            foreach (Attachment attachment in blueprint.Attachments)
            {
                if ((attachment.FromPartId == partId && attachment.FromHoleId == holeId)
                    || (attachment.ToPartId == partId && attachment.ToHoleId == holeId))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryFindPart(ContraptionBlueprint blueprint, PartId partId, out PlacedPart found)
        {
            foreach (PlacedPart part in blueprint.Parts)
            {
                if (part.Id == partId)
                {
                    found = part;
                    return true;
                }
            }

            found = null!;
            return false;
        }

        private PartId NextPartId(PartType partType) => new PartId($"{partType}-{_nextId++}".ToLowerInvariant());

        private AttachmentId NextAttachmentId() => new AttachmentId($"attach-{_nextId++}");
    }
}
