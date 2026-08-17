using System.Collections.Generic;
using Contraption.Domain.Blueprints;
using Contraption.Domain.Editing;
using Contraption.Runtime.Catalog;
using Contraption.Runtime.Views;
using UnityEngine;

namespace Contraption.Runtime.Simulation
{
    /// <summary>
    /// The one entry point that turns a blueprint into a running machine
    /// (`ARCHITECTURE.md` §8). UI code never constructs physics bodies; it calls this.
    ///
    /// Bodies are created for every part first, and only then are joints created in a second
    /// pass. That ordering is not stylistic: a joint needs both bodies to exist, so building them
    /// interleaved would make a blueprint's validity depend on the order its parts happen to be
    /// listed in. Both passes walk the blueprint's collections in order, so the same blueprint
    /// always produces the same object graph.
    /// </summary>
    public sealed class SimulationBuilder
    {
        private readonly PartCatalog _catalog;
        private readonly BodyBuilder _bodyBuilder;
        private readonly JointBuilder _jointBuilder;
        private Dictionary<PartType, PartDefinition> _domainDefinitions;

        public SimulationBuilder(PartCatalog catalog)
        {
            _catalog = catalog;
            _bodyBuilder = new BodyBuilder(catalog, new PartViewFactory());
            _jointBuilder = new JointBuilder(catalog);
        }

        /// <summary>
        /// Builds a fresh simulation. The blueprint is only read — resetting means destroying the
        /// returned root and calling this again with the same, unchanged blueprint.
        /// </summary>
        public SimulationRoot Build(LevelDefinition level, ContraptionBlueprint blueprint)
        {
            // Part positions are a cache of a derived value, and this method accepts blueprints
            // from anywhere - a fixture, a save file, the editor. Recompute them from the
            // attachment tree rather than trusting whatever was stored, or a stale position and
            // its joint fight each other at the first physics step (docs/TASKS.md D12).
            blueprint = BlueprintLayout.Normalise(blueprint, DomainDefinitions());

            var rootObject = new GameObject("SimulationRoot");
            SimulationRoot root = rootObject.AddComponent<SimulationRoot>();
            root.Initialise(level, blueprint);

            BuildBodies(blueprint, root);
            BuildJoints(blueprint, root);

            return root;
        }

        private void BuildBodies(ContraptionBlueprint blueprint, SimulationRoot root)
        {
            for (int i = 0; i < blueprint.Parts.Count; i++)
            {
                PlacedPart part = blueprint.Parts[i];
                Rigidbody2D body = _bodyBuilder.Build(part, root.transform);
                if (body != null)
                {
                    root.Register(part.Id, body);
                }
            }
        }

        private void BuildJoints(ContraptionBlueprint blueprint, SimulationRoot root)
        {
            for (int i = 0; i < blueprint.Attachments.Count; i++)
            {
                Attachment attachment = blueprint.Attachments[i];

                if (!root.TryGetBody(attachment.ToPartId, out Rigidbody2D attachedBody)
                    || !root.TryGetBody(attachment.FromPartId, out Rigidbody2D anchorBody))
                {
                    Debug.LogError(
                        $"Attachment '{attachment.Id}' references a part that was not built "
                        + $"('{attachment.FromPartId}' -> '{attachment.ToPartId}'). Skipping it.");
                    continue;
                }

                // The attached part decides the joint: attaching a hinge makes a hinge, attaching
                // a powered wheel makes a motorised one.
                PartType attachedType = TypeOf(blueprint, attachment.ToPartId);
                PartType anchorType = TypeOf(blueprint, attachment.FromPartId);
                Vector2 anchorHole = HolePosition(anchorType, attachment.FromHoleId);

                if (_jointBuilder.Build(attachedBody, attachedType, anchorBody, anchorHole) != null)
                {
                    root.CountJoint();
                }
            }
        }

        /// <summary>Projects the catalog into the plain domain definitions the layout pass needs.</summary>
        private IReadOnlyDictionary<PartType, PartDefinition> DomainDefinitions()
        {
            if (_domainDefinitions != null)
            {
                return _domainDefinitions;
            }

            _domainDefinitions = new Dictionary<PartType, PartDefinition>();
            foreach (PartDefinitionAsset asset in _catalog.Definitions)
            {
                if (asset == null)
                {
                    continue;
                }

                PartDefinition definition = asset.ToDomainDefinition();
                if (definition != null)
                {
                    _domainDefinitions[asset.PartType] = definition;
                }
            }

            return _domainDefinitions;
        }

        /// <summary>
        /// Where the anchor part's hole sits, in that part's local space. Falls back to its origin
        /// if the hole is unknown, and says so — a blueprint naming a hole the catalog does not
        /// have is a real defect, but it should not stop the rest of the machine from building.
        /// </summary>
        private Vector2 HolePosition(PartType anchorType, HoleId holeId)
        {
            if (!_catalog.TryGetDefinition(anchorType, out PartDefinitionAsset definition))
            {
                return Vector2.zero;
            }

            PartDefinition domainDefinition = definition.ToDomainDefinition();
            if (domainDefinition == null || !domainDefinition.TryGetHole(holeId, out AttachmentHole hole))
            {
                Debug.LogError(
                    $"'{anchorType}' has no hole '{holeId}'. Anchoring at the part's origin instead, "
                    + "which will place the joint in the wrong spot.");
                return Vector2.zero;
            }

            return new Vector2(hole.LocalPosition.X, hole.LocalPosition.Y);
        }

        private static PartType TypeOf(ContraptionBlueprint blueprint, PartId partId)
        {
            for (int i = 0; i < blueprint.Parts.Count; i++)
            {
                if (blueprint.Parts[i].Id == partId)
                {
                    return blueprint.Parts[i].Type;
                }
            }

            return PartType.RigidConnector;
        }
    }
}
