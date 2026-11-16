using Unity.Mathematics;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// What the land far past each edge of a bounded map looks like from inside it (GL641): each kind of edge's
    /// horizon colour (<see cref="MapBoundaryType.HorizonColor"/>: a desert's sand, the larger range's rock), laid
    /// out as the sides share the land, coming in over the first few hundred metres past the playable edge. The far
    /// terrain colours its ground by it, so the horizon in each direction is that edge's own.
    /// </summary>
    public static class MapHorizon
    {
        /// <summary>Metres past the playable edge by which a side's horizon colour has fully come in.</summary>
        public const float BlendDistance = 600f;

        /// <summary>
        /// The horizon's colours on a square grid, rows from the south, covering <paramref name="extent"/> metres
        /// each way from the map's middle. Alpha is how much of the far ground takes the colour: 0 inside the
        /// playable edge and past an edge whose kind has no tint.
        /// </summary>
        public static Color32[] Colours(MapLayout layout, int resolution, float extent)
        {
            resolution = Mathf.Max(2, resolution);
            MapShape shape = layout.Shape;
            var colours = new Color[4];
            var tints = new float4();
            for (int i = 0; i < 4; i++)
            {
                MapBoundaryType kind = layout.Boundary((MapSide)i);
                colours[i] = kind.HorizonColor;
                tints[i] = kind.HorizonTint;
            }
            var pixels = new Color32[resolution * resolution];
            float step = 2f * extent / resolution;
            for (int z = 0; z < resolution; z++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    float px = -extent + (x + 0.5f) * step, pz = -extent + (z + 0.5f) * step;
                    float4 weights = shape.SideWeights(px, pz);
                    // The colour is the sides' own everywhere, so a texel half in and half out blends to the right one.
                    Color colour = colours[0] * weights[0] + colours[1] * weights[1] + colours[2] * weights[2] + colours[3] * weights[3];
                    colour.a = math.dot(weights, tints) * math.saturate(-shape.EdgeInside(px, pz) / BlendDistance);
                    pixels[x + z * resolution] = colour;
                }
            }
            return pixels;
        }
    }
}
