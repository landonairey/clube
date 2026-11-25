using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// A map icon (<see cref="MapIcons"/>) painted into a small texture for the HUD canvas (GL867): a disc with a rim and
    /// the icon's triangles on it, so the minimap can show a thumbnail of a settlement's map badge or the waypoint pin.
    /// </summary>
    public static class MapIconTexture
    {
        /// <summary>Paints the icon on a disc (<paramref name="disc"/>, rimmed in <paramref name="rim"/>), <paramref name="pixels"/> a side.</summary>
        public static Texture2D Paint(string icon, int pixels, Color disc, Color rim, Color shape)
        {
            var texture = new Texture2D(pixels, pixels, TextureFormat.RGBA32, false) { name = "Map icon " + icon, wrapMode = TextureWrapMode.Clamp };
            Vector2[] triangles = MapIcons.Triangles(icon);
            float half = pixels * 0.5f;
            var colours = new Color[pixels * pixels];
            for (int y = 0; y < pixels; y++)
            {
                for (int x = 0; x < pixels; x++)
                {
                    // The square's middle is (0, 0), its edge 1; the disc fills it, the icon sits in its inner 0.62.
                    var p = new Vector2((x + 0.5f - half) / half, (y + 0.5f - half) / half);
                    float r = p.magnitude;
                    Color c = r > 1f ? Color.clear : r > 0.8f ? rim : disc;
                    if (r <= 0.8f && Inside(triangles, p / 0.62f))
                    {
                        c = shape;
                    }
                    colours[y * pixels + x] = c;
                }
            }
            texture.SetPixels(colours);
            texture.Apply();
            return texture;
        }

        private static bool Inside(Vector2[] triangles, Vector2 p)
        {
            for (int i = 0; i + 2 < triangles.Length; i += 3)
            {
                if (InTriangle(p, triangles[i], triangles[i + 1], triangles[i + 2]))
                {
                    return true;
                }
            }
            return false;
        }

        private static bool InTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float d1 = Cross(p, a, b), d2 = Cross(p, b, c), d3 = Cross(p, c, a);
            bool negative = d1 < 0f || d2 < 0f || d3 < 0f, positive = d1 > 0f || d2 > 0f || d3 > 0f;
            return !(negative && positive);
        }

        private static float Cross(Vector2 p, Vector2 a, Vector2 b)
        {
            return (p.x - b.x) * (a.y - b.y) - (a.x - b.x) * (p.y - b.y);
        }
    }
}
