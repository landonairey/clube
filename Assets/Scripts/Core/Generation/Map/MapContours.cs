using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// The bounded map as contour lines alone (GL636): a coarse grid of ground heights from the map's land
    /// (<see cref="BoundedMapGenerator.CreateDry"/>, so no water or survey is worked out), drawn as lines of equal
    /// height on paper, the shore heavier and the sea washed faintly so an island reads apart from a land-locked map.
    /// The new-game page shows it for the seed typed.
    /// </summary>
    public static class MapContours
    {
        public static readonly Color32 Paper = new Color32(232, 222, 196, 255);
        public static readonly Color32 SeaWash = new Color32(205, 214, 212, 255);
        public static readonly Color32 Ink = new Color32(96, 70, 44, 255);
        public static readonly Color32 ShoreInk = new Color32(44, 74, 110, 255);

        /// <summary>Metres between contour lines.</summary>
        public const float DefaultInterval = 25f;

        /// <summary>Every this many lines one is drawn heavier.</summary>
        public const int IndexEvery = 4;

        /// <summary>
        /// The map's ground heights on a grid <paramref name="resolution"/> a side, rows from the south, covering the
        /// map's bounds and <paramref name="margin"/> (a share of its side) beyond each, so a shore at the edge shows.
        /// </summary>
        public static float[] SampleHeights(BoundedMapGenerator map, int resolution, float margin = 0f)
        {
            resolution = Mathf.Max(8, resolution);
            Rect area = map.Layout.Bounds;
            float reach = area.width * Mathf.Max(0f, margin);
            area = new Rect(area.xMin - reach, area.yMin - reach, area.width + 2f * reach, area.height + 2f * reach);
            float step = area.width / resolution;
            int count = resolution * resolution;
            var points = new NativeArray<float2>(count, Allocator.TempJob);
            var heights = new NativeArray<float>(count, Allocator.TempJob);
            try
            {
                for (int i = 0; i < count; i++)
                {
                    points[i] = new float2(area.xMin + (i % resolution + 0.5f) * step, area.yMin + (i / resolution + 0.5f) * step);
                }
                map.SchedulePointHeights(points, heights).Complete();
                return heights.ToArray();
            }
            finally
            {
                points.Dispose();
                heights.Dispose();
            }
        }

        /// <summary>The contour picture of a seed's map: sampled and drawn in one call.</summary>
        public static Color32[] Render(BoundedMapGenerator map, int resolution, float interval = DefaultInterval, float margin = 0f)
        {
            resolution = Mathf.Max(8, resolution);
            return Render(SampleHeights(map, resolution, margin), resolution, map.Layout.SeaLevel, interval);
        }

        /// <summary>
        /// Contour lines of a square grid of heights: a line wherever the height crosses a multiple of
        /// <paramref name="interval"/> above the sea level, about a pixel wide whatever the slope, every
        /// <see cref="IndexEvery"/>th heavier; the sea level's own line is the shore. Ground under the sea level has
        /// no lines, only the wash.
        /// </summary>
        public static Color32[] Render(float[] heights, int resolution, float seaLevel, float interval = DefaultInterval)
        {
            interval = Mathf.Max(0.01f, interval);
            var pixels = new Color32[resolution * resolution];
            for (int z = 0; z < resolution; z++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    int i = x + z * resolution;
                    float h = heights[i] - seaLevel;
                    Color paper = h < 0f ? (Color)SeaWash : (Color)Paper;

                    // The height's change per pixel: the distance to a line in pixels is the height's distance to it over this.
                    float west = heights[(x > 0 ? x - 1 : x) + z * resolution], east = heights[(x + 1 < resolution ? x + 1 : x) + z * resolution];
                    float south = heights[x + (z > 0 ? z - 1 : z) * resolution], north = heights[x + (z + 1 < resolution ? z + 1 : z) * resolution];
                    float slope = Mathf.Max(1e-4f, 0.5f * Mathf.Sqrt((east - west) * (east - west) + (north - south) * (north - south)));

                    float shore = Mathf.Abs(h) / slope;
                    Color colour = Color.Lerp(ShoreInk, paper, Mathf.Clamp01(shore - 0.7f));
                    if (h > 0f && shore > 0.7f)
                    {
                        int line = Mathf.RoundToInt(h / interval);
                        if (line > 0)
                        {
                            bool index = line % IndexEvery == 0;
                            float away = Mathf.Abs(h - line * interval) / slope;
                            float ink = 1f - Mathf.Clamp01(away - (index ? 0.45f : 0.1f));
                            colour = Color.Lerp(paper, Ink, ink * (index ? 1f : 0.6f));
                        }
                    }
                    pixels[i] = colour;
                }
            }
            return pixels;
        }
    }
}
