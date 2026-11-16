using System;
using System.Collections.Generic;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>A herd's place on the map (GL605): which species, how many, and where they live.</summary>
    public readonly struct HerdSite
    {
        public HerdSite(Vector2Int cell, Vector2 centre, AnimalSpecies species, int count, int seed)
        {
            Cell = cell;
            Centre = centre;
            Species = species;
            Count = count;
            Seed = seed;
        }

        /// <summary>The site's cell of <see cref="HerdSites.CellSize"/> metres: its identity, in saves too.</summary>
        public Vector2Int Cell { get; }

        /// <summary>The herd's home, on the map (x, z).</summary>
        public Vector2 Centre { get; }

        public AnimalSpecies Species { get; }

        public int Count { get; }

        /// <summary>Seeds the herd's animals: where each stands, how each behaves, what each drops.</summary>
        public int Seed { get; }

        /// <summary>Names one of the herd's animals, for remembering the dead: "cellX,cellZ,index".</summary>
        public string AnimalKey(int index)
        {
            return $"{Cell.x},{Cell.y},{index}";
        }
    }

    /// <summary>
    /// Where the herds are (GL605): the map is cut into cells, and the world's seed alone says whether a cell has a herd,
    /// where in the cell it lives, of what species and how many, so the same valley holds the same herd on every visit. The
    /// species is one that lives in the biome at the herd's home; a richer biome (grazing, game) has herds in more of its
    /// cells. Nothing is stored: the spawner asks for the sites round the player.
    /// </summary>
    public static class HerdSites
    {
        /// <summary>Side of a site's cell, in metres: at most one herd in each.</summary>
        public const float CellSize = 96f;

        /// <summary>The share of cells with a herd in the poorest biome that has animals, and in the richest.</summary>
        public const float PoorChance = 0.3f, RichChance = 0.75f;

        /// <summary>
        /// The herd of a cell, if it has one. <paramref name="biomeAt"/> gives the biome at a map point (x, z), or null
        /// where there is none (water, off the map).
        /// </summary>
        public static bool TryAt(
            int worldSeed, Vector2Int cell, Func<float, float, BiomeDefinition> biomeAt, IReadOnlyList<AnimalSpecies> species, out HerdSite site)
        {
            site = default;
            int seed = Hash(worldSeed, cell.x, cell.y);
            var random = new System.Random(seed);
            // Away from the cell's edges, so neighbouring herds don't stand together.
            var centre = new Vector2(
                (cell.x + 0.2f + 0.6f * (float)random.NextDouble()) * CellSize,
                (cell.y + 0.2f + 0.6f * (float)random.NextDouble()) * CellSize);
            double chance = random.NextDouble(), pick = random.NextDouble(), size = random.NextDouble();
            BiomeDefinition biome = biomeAt(centre.x, centre.y);
            if (biome == null || chance >= ChanceIn(biome))
            {
                return false;
            }
            AnimalSpecies chosen = Pick(biome.Name, species, pick);
            if (chosen == null)
            {
                return false;
            }
            int count = chosen.Herd.x + (int)(size * (chosen.Herd.y - chosen.Herd.x + 1));
            site = new HerdSite(cell, centre, chosen, Mathf.Clamp(count, chosen.Herd.x, chosen.Herd.y), seed);
            return true;
        }

        /// <summary>The herds whose homes are within <paramref name="radius"/> of a map point.</summary>
        public static void Near(
            int worldSeed, Vector2 position, float radius, Func<float, float, BiomeDefinition> biomeAt, IReadOnlyList<AnimalSpecies> species,
            List<HerdSite> into)
        {
            int x0 = Mathf.FloorToInt((position.x - radius) / CellSize), x1 = Mathf.FloorToInt((position.x + radius) / CellSize);
            int z0 = Mathf.FloorToInt((position.y - radius) / CellSize), z1 = Mathf.FloorToInt((position.y + radius) / CellSize);
            for (int z = z0; z <= z1; z++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    if (TryAt(worldSeed, new Vector2Int(x, z), biomeAt, species, out HerdSite site)
                        && (site.Centre - position).sqrMagnitude <= radius * radius)
                    {
                        into.Add(site);
                    }
                }
            }
        }

        /// <summary>How likely a cell in a biome is to have a herd: more where the grazing or the game is good.</summary>
        public static float ChanceIn(BiomeDefinition biome)
        {
            return Mathf.Lerp(PoorChance, RichChance, Mathf.Max(biome.Grazing, biome.Game));
        }

        /// <summary>One of the species living in a biome, by a roll of 0 to 1; null when none does.</summary>
        public static AnimalSpecies Pick(string biome, IReadOnlyList<AnimalSpecies> species, double roll)
        {
            int living = 0;
            foreach (AnimalSpecies each in species)
            {
                living += each.LivesIn(biome) ? 1 : 0;
            }
            if (living == 0)
            {
                return null;
            }
            int wanted = Mathf.Min(living - 1, (int)(roll * living));
            foreach (AnimalSpecies each in species)
            {
                if (each.LivesIn(biome) && wanted-- == 0)
                {
                    return each;
                }
            }
            return null;
        }

        /// <summary>
        /// The stand-in for a world with no biome map (WorldLab, GL605): the name of the biome a place counts as, from its
        /// ground. Sand is beach (no animals), bare rock highland, and the rest grassland or forest by its site's cell, so a
        /// small map still has both kinds of herds.
        /// </summary>
        public static string StandInBiome(bool sand, bool rock, Vector2Int cell)
        {
            if (sand)
            {
                return BiomeDefinition.BeachName;
            }
            if (rock)
            {
                return BiomeDefinition.HighlandName;
            }
            return (Hash(7919, cell.x, cell.y) & 1) == 0 ? BiomeDefinition.GrasslandName : BiomeDefinition.ForestName;
        }

        /// <summary>The cell holding a map point.</summary>
        public static Vector2Int CellOf(float x, float z)
        {
            return new Vector2Int(Mathf.FloorToInt(x / CellSize), Mathf.FloorToInt(z / CellSize));
        }

        private static int Hash(int seed, int x, int z)
        {
            unchecked
            {
                uint h = (uint)seed * 374761393u + (uint)x * 668265263u + (uint)z * 2147483647u;
                h = (h ^ (h >> 13)) * 1274126177u;
                return (int)(h ^ (h >> 16));
            }
        }
    }
}
