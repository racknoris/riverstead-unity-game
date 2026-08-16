using UnityEngine;

namespace Contraption.Runtime.Views
{
    /// <summary>
    /// Generated placeholder sprites, so the POC can build machines before any art exists.
    ///
    /// Textures are created once and cached statically. That matters for the rebuild leak test:
    /// a factory that allocated a texture per part would leak on every reset, and the leak would
    /// look like a builder bug rather than an asset-lifetime bug.
    /// </summary>
    internal static class PrimitiveSprites
    {
        private static Sprite _square;
        private static Sprite _circle;

        /// <summary>A 1×1 world-unit square, so a sprite's local scale reads directly as world size.</summary>
        public static Sprite Square()
        {
            if (_square == null)
            {
                var texture = new Texture2D(1, 1) { filterMode = FilterMode.Point };
                texture.SetPixel(0, 0, Color.white);
                texture.Apply();
                _square = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
                _square.name = "PrimitiveSquare";
            }

            return _square;
        }

        public static Sprite Circle()
        {
            if (_circle == null)
            {
                const int Resolution = 64;
                var texture = new Texture2D(Resolution, Resolution) { filterMode = FilterMode.Bilinear };
                float centre = (Resolution - 1) * 0.5f;

                for (int y = 0; y < Resolution; y++)
                {
                    for (int x = 0; x < Resolution; x++)
                    {
                        float dx = x - centre;
                        float dy = y - centre;
                        bool inside = Mathf.Sqrt((dx * dx) + (dy * dy)) <= centre;
                        texture.SetPixel(x, y, inside ? Color.white : Color.clear);
                    }
                }

                texture.Apply();
                _circle = Sprite.Create(
                    texture, new Rect(0f, 0f, Resolution, Resolution), new Vector2(0.5f, 0.5f), Resolution);
                _circle.name = "PrimitiveCircle";
            }

            return _circle;
        }
    }
}
