using System;
using System.Collections.Generic;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// One resource a settlement can live off (GL594, GL595), read from the <see cref="ResourceSurvey"/>: a row of
    /// <see cref="For"/>. Specialties name the resources they need by <see cref="Id"/>.
    /// </summary>
    public sealed class SurveyResource
    {
        public SurveyResource(string id, string name, Func<ResourceSurvey, int, int, float> amount, bool isType = true)
        {
            Id = id;
            Name = name;
            Amount = amount;
            IsType = isType;
        }

        public string Id { get; }
        public string Name { get; }
        /// <summary>How much of it a survey cell (x, z) holds; the unit is the resource's own.</summary>
        public Func<ResourceSurvey, int, int, float> Amount { get; }
        /// <summary>
        /// True for a resource type of its own (wood, ore, stone...), which sites are picked for and the capital must
        /// reach; false for a finer reading of one (a single ore, the precious ores).
        /// </summary>
        public bool IsType { get; }

        /// <summary>Names (lower case) that make an ore a precious one.</summary>
        public static readonly string[] PreciousNames = { "gold", "silver", "platinum" };

        /// <summary>
        /// The resources of a surveyed map: wood, ore (all ores together), stone, limestone, sand, clay, grazing, game and
        /// fresh water, then the precious ores and each ore by itself ("ore:copper").
        /// </summary>
        public static List<SurveyResource> For(ResourceSurvey survey)
        {
            var list = new List<SurveyResource>
            {
                new SurveyResource("wood", "wood", (s, x, z) => s.Cell(x, z).Wood),
                new SurveyResource("ore", "ore", (s, x, z) => OreSum(s, x, z, null)),
                new SurveyResource("stone", "stone", (s, x, z) => s.Cell(x, z).Stone * s.Cell(x, z).Land),
                new SurveyResource("limestone", "limestone", (s, x, z) => s.Cell(x, z).Limestone),
                new SurveyResource("sand", "sand", (s, x, z) => s.Cell(x, z).Sand * s.Cell(x, z).Land),
                new SurveyResource("clay", "clay", (s, x, z) => s.Cell(x, z).Clay * s.Cell(x, z).Land),
                new SurveyResource("grazing", "grazing land", (s, x, z) => s.Cell(x, z).Grazing),
                new SurveyResource("game", "game", (s, x, z) => s.Cell(x, z).Game),
                new SurveyResource("fresh-water", "fresh water", (s, x, z) => s.Cell(x, z).FreshWater),
                new SurveyResource("precious-ore", "precious ore", (s, x, z) => OreSum(s, x, z, PreciousNames), false),
            };
            for (int i = 0; i < survey.Ores.Count; i++)
            {
                int index = i;
                string name = OreName(survey.Ores[i]);
                list.Add(new SurveyResource("ore:" + name, name, (s, x, z) => s.Ore(index, x, z), false));
            }
            return list;
        }

        /// <summary>An ore's name in lower case, as resource ids and name matches use it.</summary>
        public static string OreName(VoxelMaterial ore)
        {
            return ore == null ? "" : ore.DisplayName.ToLowerInvariant();
        }

        private static float OreSum(ResourceSurvey survey, int x, int z, string[] onlyNamed)
        {
            float sum = 0f;
            for (int i = 0; i < survey.Ores.Count; i++)
            {
                if (onlyNamed == null || Matches(OreName(survey.Ores[i]), onlyNamed))
                {
                    sum += survey.Ore(i, x, z);
                }
            }
            return sum;
        }

        /// <summary>True when a name holds any of the words.</summary>
        public static bool Matches(string name, string[] words)
        {
            foreach (string word in words)
            {
                if (name.Contains(word))
                {
                    return true;
                }
            }
            return false;
        }
    }

    /// <summary>
    /// How much of each resource lies within reach of every survey cell (GL595): sums over a square of cells, from one
    /// summed-area table per resource, and the same as a share of the map's richest such square ("richness", 0 to 1).
    /// </summary>
    public sealed class ResourceReach
    {
        private readonly int size;
        private readonly double[][] tables;
        private readonly Dictionary<long, float[]> richness = new Dictionary<long, float[]>();

        public ResourceReach(ResourceSurvey survey, IReadOnlyList<SurveyResource> resources)
        {
            Survey = survey;
            Resources = resources;
            size = survey.Size;
            tables = new double[resources.Count][];
            Totals = new float[resources.Count];
            for (int r = 0; r < resources.Count; r++)
            {
                var table = new double[(size + 1) * (size + 1)];
                for (int z = 0; z < size; z++)
                {
                    double row = 0.0;
                    for (int x = 0; x < size; x++)
                    {
                        row += Math.Max(0f, resources[r].Amount(survey, x, z));
                        table[(x + 1) + (z + 1) * (size + 1)] = table[(x + 1) + z * (size + 1)] + row;
                    }
                }
                tables[r] = table;
                Totals[r] = (float)table[table.Length - 1];
            }
        }

        public ResourceSurvey Survey { get; }
        public IReadOnlyList<SurveyResource> Resources { get; }
        /// <summary>Each resource's amount over the whole map; 0 means the map has none.</summary>
        public float[] Totals { get; }

        /// <summary>Index of a resource by id, or -1.</summary>
        public int IndexOf(string id)
        {
            for (int i = 0; i < Resources.Count; i++)
            {
                if (Resources[i].Id == id)
                {
                    return i;
                }
            }
            return -1;
        }

        /// <summary>A resource's amount in the cells within <paramref name="radius"/> cells of (x, z), as a square.</summary>
        public float Sum(int resource, int x, int z, int radius)
        {
            int x0 = Mathf.Max(0, x - radius), z0 = Mathf.Max(0, z - radius);
            int x1 = Mathf.Min(size, x + radius + 1), z1 = Mathf.Min(size, z + radius + 1);
            double[] t = tables[resource];
            int w = size + 1;
            return (float)(t[x1 + z1 * w] - t[x0 + z1 * w] - t[x1 + z0 * w] + t[x0 + z0 * w]);
        }

        /// <summary>
        /// <see cref="Sum"/> for every cell (x + z * size) as the square root of its share of the richest cell's (0 to 1; the root keeps one great lode from hiding the rest); all 0 for a resource the
        /// map lacks. Kept, so asking again is free.
        /// </summary>
        public float[] Richness(int resource, int radius)
        {
            long key = ((long)resource << 32) | (uint)radius;
            if (richness.TryGetValue(key, out float[] map))
            {
                return map;
            }
            map = new float[size * size];
            float best = 0f;
            for (int z = 0; z < size; z++)
            {
                for (int x = 0; x < size; x++)
                {
                    float sum = Sum(resource, x, z, radius);
                    map[x + z * size] = sum;
                    best = Mathf.Max(best, sum);
                }
            }
            if (best > 0f)
            {
                for (int i = 0; i < map.Length; i++)
                {
                    map[i] = Mathf.Sqrt(map[i] / best);
                }
            }
            richness[key] = map;
            return map;
        }
    }
}
