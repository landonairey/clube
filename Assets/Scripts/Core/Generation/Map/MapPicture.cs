using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// The bounded map as a picture (GL599): each pixel's colour from the ground's height, biome and water there, with
    /// the survey's rock and woods, and the colours the plan's roads, water routes and settlements are drawn in. The
    /// world map screen and the Editor's preview both draw with it, so the two never drift apart.
    /// </summary>
    public static class MapPicture
    {
        public static readonly Color Sea = new Color(0.13f, 0.3f, 0.55f);
        public static readonly Color FreshWater = new Color(0.3f, 0.62f, 0.9f);
        public static readonly Color Rock = new Color(0.55f, 0.55f, 0.57f);
        public static readonly Color Woods = new Color(0.07f, 0.25f, 0.1f);
        public static readonly Color Bridge = Color.white;

        /// <summary>
        /// The map's pixels, <paramref name="resolution"/> a side, rows from the south (z up, as a texture has them),
        /// covering the map's bounds exactly.
        /// </summary>
        public static Color32[] Render(BoundedMapGenerator map, ResourceSurvey survey, int resolution)
        {
            resolution = Mathf.Max(8, resolution);
            Rect area = map.Layout.Bounds;
            float step = area.width / resolution;
            int count = resolution * resolution;
            var points = new NativeArray<float2>(count, Allocator.TempJob);
            var heights = new NativeArray<float>(count, Allocator.TempJob);
            var pixels = new Color32[count];
            try
            {
                for (int i = 0; i < count; i++)
                {
                    points[i] = new float2(area.xMin + (i % resolution + 0.5f) * step, area.yMin + (i / resolution + 0.5f) * step);
                }
                map.SchedulePointHeights(points, heights).Complete();

                BiomeField biomes = map.BiomeField;
                WaterMap water = map.Water;
                float seaLevel = map.Layout.SeaLevel;
                for (int z = 0; z < resolution; z++)
                {
                    for (int x = 0; x < resolution; x++)
                    {
                        int i = x + z * resolution;
                        float2 p = points[i];
                        float h = heights[i];
                        if (water.IsCreated && water.TryCell(p, out int wet) && water.Kind[wet] != 0)
                        {
                            pixels[i] = water.IsSea(wet)
                                ? Sea * Mathf.Lerp(0.6f, 1f, Mathf.InverseLerp(seaLevel - 30f, seaLevel, h))
                                : FreshWater;
                            continue;
                        }
                        Color colour = survey.Biomes[biomes.IndexAt(p.x, p.y, h)].MapColor;
                        if (survey.TryCell(new Vector2(p.x, p.y), out Vector2Int cell))
                        {
                            SurveyCell surveyed = survey.Cell(cell.x, cell.y);
                            colour = Color.Lerp(colour, Rock, surveyed.Stone);
                            colour = Color.Lerp(colour, Woods, Mathf.Clamp01(surveyed.Wood / 6f) * 0.6f);
                        }
                        // Lighter with height, and lit from the north-west so slopes read as relief.
                        float west = heights[(x > 0 ? x - 1 : x) + z * resolution], east = heights[(x + 1 < resolution ? x + 1 : x) + z * resolution];
                        float south = heights[x + (z > 0 ? z - 1 : z) * resolution], north = heights[x + (z + 1 < resolution ? z + 1 : z) * resolution];
                        float relief = Mathf.Clamp(((east - west) - (north - south)) / (2f * step) * 0.45f, -0.3f, 0.3f);
                        float shade = Mathf.Lerp(0.8f, 1.2f, Mathf.InverseLerp(seaLevel, seaLevel + 350f, h)) + relief;
                        colour *= shade;
                        colour.a = 1f;
                        pixels[i] = colour;
                    }
                }
            }
            finally
            {
                points.Dispose();
                heights.Dispose();
            }
            return pixels;
        }

        public static readonly Color Dirt = new Color(0.42f, 0.26f, 0.11f);
        public static readonly Color Cobble = new Color(0.62f, 0.62f, 0.64f);

        /// <summary>
        /// A road's colour on the map and on the ground (GL620): the footpath and the dirt cart path are dirt-brown,
        /// the paved cart path the grey of its cobbles.
        /// </summary>
        public static Color RoadColour(RoadClass roadClass)
        {
            return roadClass == RoadClass.PavedCartPath ? Cobble : Dirt;
        }

        /// <summary>
        /// A road's width on the map in pixels of a picture about 750 wide (GL620): the footpath narrow, both cart
        /// paths wider.
        /// </summary>
        public static float RoadWidth(RoadClass roadClass)
        {
            return roadClass == RoadClass.PavedCartPath ? 3f : roadClass == RoadClass.DirtCartPath ? 2.6f : 1f;
        }

        /// <summary>The dark edge of a cased line (<see cref="MapLineStyle.Casing"/>) and of the map's symbols.</summary>
        public static readonly Color Casing = new Color(0.1f, 0.08f, 0.06f);

        /// <summary>
        /// How a road class is drawn on the world map (GL635), three line types told apart without their colour: the
        /// walking path a thin dotted line, the dirt cart path a solid brown one, the paved cart path a wide pale line
        /// between dark edges.
        /// </summary>
        public static MapLineStyle RoadStyle(RoadClass roadClass)
        {
            switch (roadClass)
            {
                case RoadClass.PavedCartPath:
                    return new MapLineStyle("Paved cart path", Cobble, RoadWidth(roadClass), casing: 1f);
                case RoadClass.DirtCartPath:
                    return new MapLineStyle("Dirt cart path", Dirt, RoadWidth(roadClass));
                default:
                    return new MapLineStyle("Walking path", Dirt, 1.7f, dash: 2.4f, gap: 2.6f);
            }
        }

        /// <summary>How a water route is drawn on the world map: long dashes, white at sea and cyan on a river.</summary>
        public static MapLineStyle RouteStyle(WaterRouteKind kind)
        {
            return new MapLineStyle(kind == WaterRouteKind.Sea ? "Sea route" : "River route", RouteColour(kind), 1.2f, dash: 6f, gap: 6f);
        }

        /// <summary>A water route's colour: white at sea, cyan on a river.</summary>
        public static Color RouteColour(WaterRouteKind kind)
        {
            return kind == WaterRouteKind.Sea ? Color.white : Color.cyan;
        }

        /// <summary>A settlement's colour: yellow for the smallest to red for the largest, magenta when hostile.</summary>
        public static Color SettlementColour(SettlementType type)
        {
            return type.Hostile ? Color.magenta
                : Color.Lerp(new Color(1f, 0.95f, 0.2f), new Color(0.9f, 0.05f, 0.1f), Mathf.Sqrt(type.Parcels) / 5f);
        }

        /// <summary>The pixel (fractional, from the south-west corner) a world position falls on in a picture of the map's area.</summary>
        public static Vector2 PixelOf(Rect area, int resolution, Vector2 position)
        {
            return new Vector2((position.x - area.xMin) / area.width * resolution, (position.y - area.yMin) / area.height * resolution);
        }
    }
}
