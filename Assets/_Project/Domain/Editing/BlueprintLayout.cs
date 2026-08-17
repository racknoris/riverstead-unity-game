using System.Collections.Generic;
using Contraption.Domain.Blueprints;

namespace Contraption.Domain.Editing
{
    /// <summary>
    /// Recomputes every attached part's position from the attachment tree.
    ///
    /// <para>
    /// `PlacedPart.Position` is a *cache* of a derived value, not an independent fact. The truth
    /// is the graph: which hole on which parent, plus the catalog's hole geometry, plus the
    /// part's own rotation. Anything that accepts a blueprint from outside the editor — a saved
    /// file, a hard-coded fixture, eventually a server payload — must run it through here, or it
    /// is trusting a number nobody has checked.
    /// </para>
    /// <para>
    /// This exists because of a real defect: a hand-written blueprint in Milestone 5 placed a
    /// chassis 0.47 units from where its own wheel holes were, and the joint resolved the
    /// disagreement by yanking the machine apart at the first physics step. Normalising makes the
    /// stored value follow the graph instead of arguing with it.
    /// </para>
    /// <para>
    /// Roots keep their authored position — a machine has to sit somewhere, and nothing derives
    /// that. Only attached parts are repositioned.
    /// </para>
    /// </summary>
    public static class BlueprintLayout
    {
        public static ContraptionBlueprint Normalise(
            ContraptionBlueprint blueprint,
            IReadOnlyDictionary<PartType, PartDefinition> definitions)
        {
            if (blueprint is null || definitions is null || blueprint.Attachments.Count == 0)
            {
                return blueprint!;
            }

            var byId = new Dictionary<PartId, PlacedPart>(blueprint.Parts.Count);
            foreach (PlacedPart part in blueprint.Parts)
            {
                byId[part.Id] = part;
            }

            var childrenOf = new Dictionary<PartId, List<Attachment>>();
            var hasParent = new HashSet<PartId>();
            foreach (Attachment attachment in blueprint.Attachments)
            {
                if (!childrenOf.TryGetValue(attachment.FromPartId, out List<Attachment> children))
                {
                    children = new List<Attachment>();
                    childrenOf[attachment.FromPartId] = children;
                }

                children.Add(attachment);
                hasParent.Add(attachment.ToPartId);
            }

            // Breadth-first from the roots, so a parent is always positioned before its children.
            var queue = new Queue<PartId>();
            foreach (PlacedPart part in blueprint.Parts)
            {
                if (!hasParent.Contains(part.Id))
                {
                    queue.Enqueue(part.Id);
                }
            }

            var visited = new HashSet<PartId>();
            while (queue.Count > 0)
            {
                PartId parentId = queue.Dequeue();
                if (!visited.Add(parentId) || !childrenOf.TryGetValue(parentId, out List<Attachment> children))
                {
                    continue;
                }

                PlacedPart parent = byId[parentId];
                foreach (Attachment attachment in children)
                {
                    if (!byId.TryGetValue(attachment.ToPartId, out PlacedPart child)
                        || !definitions.TryGetValue(parent.Type, out PartDefinition parentDefinition)
                        || !parentDefinition.TryGetHole(attachment.FromHoleId, out AttachmentHole parentHole)
                        || !definitions.TryGetValue(child.Type, out PartDefinition childDefinition)
                        || !childDefinition.TryGetHole(attachment.ToHoleId, out AttachmentHole mountHole))
                    {
                        // A hole the catalog does not have is a validation problem, not a layout
                        // one. Leave the part where it is and let Milestone 7 report it.
                        continue;
                    }

                    byId[child.Id] = new PlacedPart(
                        child.Id,
                        child.Type,
                        PartLayout.PositionChild(
                            parent.Position, parent.Rotation, parentHole.LocalPosition,
                            mountHole.LocalPosition, child.Rotation),
                        child.Rotation,
                        child.Configuration);

                    queue.Enqueue(child.Id);
                }
            }

            var repositioned = new List<PlacedPart>(blueprint.Parts.Count);
            foreach (PlacedPart part in blueprint.Parts)
            {
                repositioned.Add(byId[part.Id]);
            }

            return ContraptionBlueprint.Create(
                blueprint.LevelId, repositioned, new List<Attachment>(blueprint.Attachments));
        }
    }
}
