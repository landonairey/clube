using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// Shared look of the meteor's fire (GL869, GL871): one soft round glow texture and a vertex-coloured, alpha-blended
    /// material for particles and the trail, plus the grey stone material of the rock. Made once, on first use, from
    /// shaders every build has (no scene assets needed, since <see cref="MeteorEvent"/> is made at run time).
    /// </summary>
    public static class MeteorFx
    {
        private static Material glow;
        private static Material stone;

        /// <summary>Soft round glow; colour comes from each particle or trail vertex.</summary>
        public static Material Glow
        {
            get
            {
                if (glow == null)
                {
                    glow = new Material(Shader.Find("Sprites/Default")) { name = "Meteor glow", mainTexture = SoftDot(64) };
                }
                return glow;
            }
        }

        /// <summary>Grey stone for the meteor's core.</summary>
        public static Material Stone
        {
            get
            {
                if (stone == null)
                {
                    Shader shader = Shader.Find("Clube/Low Poly");
                    if (shader == null)
                    {
                        shader = Shader.Find("Universal Render Pipeline/Lit");
                    }
                    stone = new Material(shader) { name = "Meteor stone" };
                    Color grey = new Color(0.36f, 0.35f, 0.34f);
                    stone.color = grey;
                    if (stone.HasProperty("_BaseColor"))
                    {
                        stone.SetColor("_BaseColor", grey);
                    }
                }
                return stone;
            }
        }

        /// <summary>A gradient from <paramref name="from"/> to <paramref name="to"/>, fading in quickly and out to nothing.</summary>
        public static Gradient Fade(Color from, Color to, float peakAlpha = 1f)
        {
            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(from, 0f), new GradientColorKey(to, 1f) },
                new[] { new GradientAlphaKey(peakAlpha, 0f), new GradientAlphaKey(peakAlpha * 0.6f, 0.4f), new GradientAlphaKey(0f, 1f) });
            return g;
        }

        // White dot, alpha falling off smoothly from the middle.
        private static Texture2D SoftDot(int size)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "Meteor dot", wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[size * size];
            float half = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = new Vector2(x - half, y - half).magnitude / half;
                    float a = Mathf.Clamp01(1f - d);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * a * 255f));
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            return texture;
        }
    }
}
