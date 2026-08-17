using Contraption.Domain.Blueprints;
using Contraption.Runtime.Catalog;
using UnityEngine;

namespace Contraption.Runtime.Views
{
    /// <summary>
    /// Builds the *visual* for a part and attaches it to an already-built body.
    ///
    /// Split from <c>BodyBuilder</c> on purpose: how a part looks and how it behaves physically
    /// change for different reasons and at different times. The view is also the disposable half —
    /// today it is a generated coloured box, tomorrow it is a prefab, and the body code does not
    /// care either way.
    ///
    /// The sprite lives on a *scaled child* rather than on the body itself, because transform
    /// scale multiplies collider dimensions. Scaling the body to size a sprite silently resizes
    /// its physics, which is a genuinely confusing bug to chase.
    /// </summary>
    public sealed class PartViewFactory
    {
        public void AttachView(GameObject body, PartDefinitionAsset definition)
        {
            if (definition.Prefab != null)
            {
                GameObject instance = Object.Instantiate(definition.Prefab, body.transform);
                instance.transform.localPosition = Vector3.zero;
                instance.transform.localRotation = Quaternion.identity;
                return;
            }

            AttachPlaceholderView(body, definition);
        }

        private static void AttachPlaceholderView(GameObject body, PartDefinitionAsset definition)
        {
            var visual = new GameObject("View");
            visual.transform.SetParent(body.transform, worldPositionStays: false);

            bool isRound = definition.Radius > 0f;
            visual.transform.localScale = isRound
                ? new Vector3(definition.Radius * 2f, definition.Radius * 2f, 1f)
                : new Vector3(definition.Size.x, definition.Size.y, 1f);

            SpriteRenderer renderer = visual.AddComponent<SpriteRenderer>();
            renderer.sprite = isRound ? PrimitiveSprites.Circle() : PrimitiveSprites.Square();
            renderer.color = PartPalette.ColourFor(definition.PartType);
        }

    }
}
