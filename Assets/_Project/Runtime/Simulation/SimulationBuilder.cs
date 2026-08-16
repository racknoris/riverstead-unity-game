using Contraption.Domain.Blueprints;
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
        private readonly BodyBuilder _bodyBuilder;
        private readonly JointBuilder _jointBuilder;

        public SimulationBuilder(PartCatalog catalog)
            : this(new BodyBuilder(catalog, new PartViewFactory()), new JointBuilder(catalog))
        {
        }

        public SimulationBuilder(BodyBuilder bodyBuilder, JointBuilder jointBuilder)
        {
            _bodyBuilder = bodyBuilder;
            _jointBuilder = jointBuilder;
        }

        /// <summary>
        /// Builds a fresh simulation. The blueprint is only read — resetting means destroying the
        /// returned root and calling this again with the same, unchanged blueprint.
        /// </summary>
        public SimulationRoot Build(LevelDefinition level, ContraptionBlueprint blueprint)
        {
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
                if (_jointBuilder.Build(attachedBody, attachedType, anchorBody) != null)
                {
                    root.CountJoint();
                }
            }
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
