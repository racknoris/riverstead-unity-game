using UnityEngine;

namespace Contraption.Spike
{
    /// <summary>
    /// Throwaway primitive factory for the fun checkpoint.
    ///
    /// Everything is generated in code from 1x1 and circular textures built at runtime, so the
    /// spike ships no art assets. That is deliberate: it keeps the checkpoint deletable in a
    /// single commit and avoids adding binary files before Git LFS is installed
    /// (docs/ISSUES.md #2).
    ///
    /// The collider lives on the body at scale 1 and the sprite lives on a scaled child, so
    /// transform scale never silently multiplies collider dimensions.
    /// </summary>
    public static class SpikeVisuals
    {
        private static Sprite _squareSprite;
        private static Sprite _circleSprite;
        private static PhysicsMaterial2D _grip;

        /// <summary>Friction material. Wheels without one just spin (docs/CONVENTIONS.md).</summary>
        public static PhysicsMaterial2D Grip
        {
            get
            {
                if (_grip == null)
                {
                    _grip = new PhysicsMaterial2D("SpikeGrip") { friction = 1f, bounciness = 0f };
                }

                return _grip;
            }
        }

        public static GameObject CreateBox(
            string name,
            Transform parent,
            Vector2 position,
            Vector2 size,
            float rotationDegrees,
            Color color,
            RigidbodyType2D bodyType,
            float mass)
        {
            var box = new GameObject(name);
            box.transform.SetParent(parent, worldPositionStays: false);
            box.transform.localPosition = position;
            box.transform.localRotation = Quaternion.Euler(0f, 0f, rotationDegrees);

            var body = box.AddComponent<Rigidbody2D>();
            body.bodyType = bodyType;
            if (bodyType == RigidbodyType2D.Dynamic)
            {
                body.useAutoMass = false;
                body.mass = mass;
            }

            BoxCollider2D collider = box.AddComponent<BoxCollider2D>();
            collider.size = size;
            collider.sharedMaterial = Grip;

            AddSquareSprite(box.transform, Vector2.zero, size, color);
            return box;
        }

        public static GameObject CreateWheel(
            string name,
            Transform parent,
            Vector2 position,
            float radius,
            Color color,
            float mass)
        {
            var wheel = new GameObject(name);
            wheel.transform.SetParent(parent, worldPositionStays: false);
            wheel.transform.localPosition = position;

            var body = wheel.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Dynamic;
            body.useAutoMass = false;
            body.mass = mass;

            CircleCollider2D collider = wheel.AddComponent<CircleCollider2D>();
            collider.radius = radius;
            collider.sharedMaterial = Grip;

            var visual = new GameObject("Sprite");
            visual.transform.SetParent(wheel.transform, worldPositionStays: false);
            visual.transform.localScale = new Vector3(radius * 2f, radius * 2f, 1f);
            SpriteRenderer renderer = visual.AddComponent<SpriteRenderer>();
            renderer.sprite = CircleSprite();
            renderer.color = color;

            // A spoke, so wheel rotation is visible at a glance. Judging whether the drive
            // "feels controllable" is impossible if you cannot see the wheels turning.
            AddSquareSprite(wheel.transform, Vector2.zero, new Vector2(radius * 1.6f, radius * 0.25f), color * 0.45f, sortingOrder: 1);
            return wheel;
        }

        public static void AddSquareSprite(
            Transform parent,
            Vector2 localPosition,
            Vector2 size,
            Color color,
            int sortingOrder = 0)
        {
            var visual = new GameObject("Sprite");
            visual.transform.SetParent(parent, worldPositionStays: false);
            visual.transform.localPosition = localPosition;
            visual.transform.localScale = new Vector3(size.x, size.y, 1f);

            SpriteRenderer renderer = visual.AddComponent<SpriteRenderer>();
            renderer.sprite = SquareSprite();
            renderer.color = color;
            renderer.sortingOrder = sortingOrder;
        }

        private static Sprite SquareSprite()
        {
            if (_squareSprite == null)
            {
                var texture = new Texture2D(1, 1) { filterMode = FilterMode.Point };
                texture.SetPixel(0, 0, Color.white);
                texture.Apply();
                // pixelsPerUnit = 1 with a 1x1 texture gives a sprite exactly one world unit
                // across, so localScale reads directly as world size.
                _squareSprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
            }

            return _squareSprite;
        }

        private static Sprite CircleSprite()
        {
            if (_circleSprite == null)
            {
                const int Resolution = 64;
                var texture = new Texture2D(Resolution, Resolution) { filterMode = FilterMode.Bilinear };
                float centre = (Resolution - 1) * 0.5f;

                for (int y = 0; y < Resolution; y++)
                {
                    for (int x = 0; x < Resolution; x++)
                    {
                        float distance = Mathf.Sqrt(((x - centre) * (x - centre)) + ((y - centre) * (y - centre)));
                        bool inside = distance <= centre;
                        texture.SetPixel(x, y, inside ? Color.white : Color.clear);
                    }
                }

                texture.Apply();
                _circleSprite = Sprite.Create(
                    texture,
                    new Rect(0f, 0f, Resolution, Resolution),
                    new Vector2(0.5f, 0.5f),
                    Resolution);
            }

            return _circleSprite;
        }
    }
}
