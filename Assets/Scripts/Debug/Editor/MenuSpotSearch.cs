using System.Collections.Generic;
using System.Text;
using Clube.Core;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace Clube.Debug.Editor
{
    /// <summary>
    /// Finds the seed for the title page's diorama (GL517): runs WorldLab's own terrain generator (its WorldConfig, only
    /// the seed changed) over the diorama's small area at the origin and scores each seed on a mountain's edge (a good
    /// height range that slopes one way, not a plateau) with a river cutting through it (river cells, crossing the middle).
    /// The best seeds are logged with their numbers; the winner goes into <c>MenuWorldConfig</c>.
    /// </summary>
    public static class MenuSpotSearch
    {
        private const string ConfigPath = "Assets/Config/WorldLabWorldConfig.asset";

        /// <summary>Scores seeds <paramref name="first"/> to <paramref name="last"/> over a square of <paramref name="size"/> m.</summary>
        public static string Search(int first, int last, float size, int keep)
        {
            var source = AssetDatabase.LoadAssetAtPath<WorldConfig>(ConfigPath);
            WorldConfig config = Object.Instantiate(source);
            var results = new List<(float score, string line)>();
            for (int seed = first; seed <= last; seed++)
            {
                config.Terrain.Seed = seed;
                ITerrainGenerator generator = TerrainGenerators.Create(config.Terrain);
                if (!(generator is HeightfieldGenerator ground))
                {
                    continue;
                }
                results.Add(Score(seed, ground, size));
            }
            Object.DestroyImmediate(config);
            results.Sort((a, b) => b.score.CompareTo(a.score));
            var text = new StringBuilder();
            for (int i = 0; i < results.Count && i < keep; i++)
            {
                text.AppendLine(results[i].line);
            }
            return text.ToString();
        }

        private static (float, string) Score(int seed, HeightfieldGenerator ground, float size)
        {
            WaterMap water = ground.Water;
            const int n = 33;
            float half = size * 0.5f;
            float min = float.MaxValue, max = float.MinValue;
            int river = 0, middleRiver = 0, lake = 0;
            float westSum = 0f, eastSum = 0f;
            for (int i = 0; i < n; i++)
            {
                for (int k = 0; k < n; k++)
                {
                    float x = -half + size * i / (n - 1);
                    float z = -half + size * k / (n - 1);
                    float h = ground.Height(x, z);
                    min = Mathf.Min(min, h);
                    max = Mathf.Max(max, h);
                    if (k < n / 2) westSum += h; else if (k > n / 2) eastSum += h;
                    if (water.TryCell(new float2(x, z), out int cell))
                    {
                        if (water.Kind[cell] == (byte)WaterKind.River)
                        {
                            river++;
                            if (Mathf.Abs(x) < size * 0.25f && Mathf.Abs(z) < size * 0.25f)
                            {
                                middleRiver++;
                            }
                        }
                        else if (water.Kind[cell] == (byte)WaterKind.Lake)
                        {
                            lake++;
                        }
                    }
                }
            }
            float range = max - min;
            float cells = n * n;
            float tilt = (eastSum - westSum) / (cells * 0.5f);
            // Wants: 10-22 m of relief, tilted (an edge), 6-20% river, some of it in the middle, no lake.
            float score = 0f;
            score -= Mathf.Abs(range - 14f) * 0.8f;
            score += Mathf.Clamp(tilt, -8f, 6f);
            score -= Mathf.Abs(river / cells - 0.12f) * 150f;
            score += Mathf.Min(middleRiver, 40) * 0.1f;
            score -= lake / cells * 40f;
            if (range < 6f)
            {
                score -= 20f;
            }
            string line = $"seed {seed}: score {score:0.0}  relief {range:0.0} ({min:0.0}-{max:0.0})  tilt {tilt:0.0}  river {river / cells:P0} (middle {middleRiver})  lake {lake / cells:P0}";
            return (score, line);
        }
    }
}
