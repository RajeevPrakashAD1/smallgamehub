using UnityEngine;

namespace TrailTrap
{
    /// <summary>
    /// Code-generated sprites/textures shared by the views (no image assets in this game).
    /// All shapes are white + alpha; consumers tint via SpriteRenderer/Image/material color.
    /// </summary>
    public static class ProcSprites
    {
        /// <summary>Soft filled dot: opaque centre fading to transparent at the rim.</summary>
        public static Texture2D MakeDotTexture(int sizePx)
            => Fill(sizePx, d => 1f - d);                    // d = 0 centre .. 1 rim

        public static Sprite MakeDot(int sizePx)
            => ToSprite(MakeDotTexture(sizePx), sizePx);

        /// <summary>Same dot, sized to a chosen pixels-per-unit (world size = sizePx / ppu).</summary>
        public static Sprite MakeDot(int sizePx, float pixelsPerUnit)
            => Sprite.Create(MakeDotTexture(sizePx), new Rect(0, 0, sizePx, sizePx),
                             new Vector2(0.5f, 0.5f), pixelsPerUnit);

        /// <summary>Soft hollow ring centred at ~80% of the radius (for the joystick base).</summary>
        public static Sprite MakeRing(int sizePx, float thickness = 0.15f)
            => ToSprite(Fill(sizePx, d => 1f - Mathf.Abs(d - 0.8f) / thickness), sizePx);

        // Shared pixel loop: alpha(d) decides the shape, d = distance from centre in 0..1.
        static Texture2D Fill(int sizePx, System.Func<float, float> alpha)
        {
            var tex = new Texture2D(sizePx, sizePx, TextureFormat.RGBA32, false);
            float r = sizePx * 0.5f;
            for (int y = 0; y < sizePx; y++)
                for (int x = 0; x < sizePx; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), new Vector2(r, r)) / r;
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(alpha(d))));
                }
            tex.Apply();
            return tex;
        }

        static Sprite ToSprite(Texture2D tex, int sizePx)
            => Sprite.Create(tex, new Rect(0, 0, sizePx, sizePx), new Vector2(0.5f, 0.5f), sizePx);
    }
}
