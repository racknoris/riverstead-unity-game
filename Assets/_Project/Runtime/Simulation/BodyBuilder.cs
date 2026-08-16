using System.Collections.Generic;
using Contraption.Domain.Blueprints;
using Contraption.Runtime.Catalog;
using Contraption.Runtime.Views;
using UnityEngine;

namespace Contraption.Runtime.Simulation
{
    /// <summary>
    /// Turns one <see cref="PlacedPart"/> into a physics body under the simulation root.
    ///
    /// Reads its geometry, mass and friction from the catalog rather than from constants, so
    /// retuning a part is an asset edit (`ARCHITECTURE.md` §9).
    /// </summary>
    public sealed class BodyBuilder
    {
        private readonly PartCatalog _catalog;
        private readonly PartViewFactory _viewFactory;
        private readonly Dictionary<float, PhysicsMaterial2D> _materialsByFriction =
            new Dictionary<float, PhysicsMaterial2D>();

        public BodyBuilder(PartCatalog catalog, PartViewFactory viewFactory)
        {
            _catalog = catalog;
            _viewFactory = viewFactory;
        }

        /// <summary>Returns null if the part's type has no catalog entry.</summary>
        public Rigidbody2D Build(PlacedPart part, Transform root)
        {
            if (!_catalog.TryGetDefinition(part.Type, out PartDefinitionAsset definition))
            {
                Debug.LogError(
                    $"No catalog entry for '{part.Type}', so part '{part.Id}' cannot be built. "
                    + "PartCatalogValidator should have caught this before a build was attempted.");
                return null;
            }

            var body = new GameObject($"{part.Type}_{part.Id}");
            body.transform.SetParent(root, worldPositionStays: false);
            body.transform.localPosition = new Vector3(part.Position.X, part.Position.Y, 0f);
            body.transform.localRotation = Quaternion.Euler(0f, 0f, part.Rotation.Degrees);

            Rigidbody2D rigidbody = body.AddComponent<Rigidbody2D>();
            rigidbody.bodyType = RigidbodyType2D.Dynamic;
            rigidbody.useAutoMass = false;
            rigidbody.mass = definition.ToDomainDefinition()?.Mass ?? 1f;
            // Physics steps at 50 Hz against a faster display; without this, fast-moving parts
            // are drawn at stale positions and the game reads as stuttering. docs/ISSUES.md L3.
            rigidbody.interpolation = RigidbodyInterpolation2D.Interpolate;

            AddCollider(body, definition);
            _viewFactory.AttachView(body, definition);

            return rigidbody;
        }

        private void AddCollider(GameObject body, PartDefinitionAsset definition)
        {
            Collider2D collider;
            if (definition.Radius > 0f)
            {
                CircleCollider2D circle = body.AddComponent<CircleCollider2D>();
                circle.radius = definition.Radius;
                collider = circle;
            }
            else
            {
                BoxCollider2D box = body.AddComponent<BoxCollider2D>();
                box.size = definition.Size;
                collider = box;
            }

            collider.sharedMaterial = MaterialFor(definition.Friction);
        }

        /// <summary>
        /// Materials are shared per friction value rather than created per part. A material per
        /// body would be a fresh asset on every rebuild — a slow leak that only shows up after
        /// many resets.
        /// </summary>
        private PhysicsMaterial2D MaterialFor(float friction)
        {
            if (!_materialsByFriction.TryGetValue(friction, out PhysicsMaterial2D material))
            {
                material = new PhysicsMaterial2D($"Friction{friction}")
                {
                    friction = friction,
                    bounciness = 0f
                };
                _materialsByFriction[friction] = material;
            }

            return material;
        }
    }
}
