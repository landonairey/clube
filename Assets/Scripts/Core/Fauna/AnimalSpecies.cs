using System;
using System.Collections.Generic;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>When a species is up and about (GL602, GL606).</summary>
    public enum AnimalRoutine
    {
        /// <summary>Grazes by day, gathers and sleeps at night.</summary>
        Day,

        /// <summary>Out at dawn and dusk; rests through midday and the deep of the night.</summary>
        Twilight,
    }

    /// <summary>One thing a species leaves in its carcass: an item by id and how many, from and to (GL602, GL603).</summary>
    public readonly struct AnimalDrop
    {
        public AnimalDrop(string itemId, int min, int max)
        {
            ItemId = itemId;
            Min = Mathf.Max(0, min);
            Max = Mathf.Max(Min, max);
        }

        public string ItemId { get; }
        public int Min { get; }
        public int Max { get; }
    }

    /// <summary>A rolled drop: so many of an item.</summary>
    public readonly struct AnimalLoot
    {
        public AnimalLoot(string itemId, int count)
        {
            ItemId = itemId;
            Count = count;
        }

        public string ItemId { get; }
        public int Count { get; }
    }

    /// <summary>
    /// An animal species as data (GL602): where it lives (by <see cref="BiomeDefinition"/> name), its herds, its body and
    /// speeds, how much it takes to kill, how easily it bolts, when it is awake and what its carcass holds. The rows are in
    /// <see cref="AnimalSpeciesTable"/>; a new species is a row there plus a model recipe (<c>AnimalLooks</c>, Game).
    /// </summary>
    public sealed class AnimalSpecies
    {
        /// <summary>Stable id, used in saves and to find its model recipe.</summary>
        public string Id { get; set; }

        public string Name { get; set; }

        /// <summary>The supply chain it feeds: <see cref="AnimalSpeciesTable.Textile"/> or <see cref="AnimalSpeciesTable.Tannery"/>.</summary>
        public string Feeds { get; set; }

        /// <summary>Names of the biomes it lives in.</summary>
        public string[] Biomes { get; set; } = Array.Empty<string>();

        /// <summary>Animals in a herd, from and to.</summary>
        public Vector2Int Herd { get; set; } = new Vector2Int(1, 1);

        /// <summary>Nose to tail, in metres.</summary>
        public float Length { get; set; } = 1f;

        /// <summary>Height of its back, in metres.</summary>
        public float ShoulderHeight { get; set; } = 1f;

        /// <summary>Metres a second.</summary>
        public float WalkSpeed { get; set; } = 1f;

        /// <summary>Metres a second, fleeing.</summary>
        public float RunSpeed { get; set; } = 5f;

        public float Health { get; set; } = 20f;

        /// <summary>How near the player gets before it bolts, in metres.</summary>
        public float Skittishness { get; set; } = 8f;

        public AnimalRoutine Routine { get; set; }

        public AnimalDrop[] Drops { get; set; } = Array.Empty<AnimalDrop>();

        /// <summary>How far from the herd's home its animals roam, in metres.</summary>
        public float Range => Mathf.Clamp(6f + Length * 5f + Herd.y * 1.5f, 8f, 30f);

        public bool LivesIn(string biome)
        {
            return Array.IndexOf(Biomes, biome) >= 0;
        }

        /// <summary>True at the hours it is up: grazing and wandering. The rest it gathers and lies down (GL606).</summary>
        public bool IsActiveAt(float hour)
        {
            hour = Mathf.Repeat(hour, 24f);
            if (Routine == AnimalRoutine.Twilight)
            {
                return (hour >= SkyPositions.SunriseHour - 1.5f && hour < SkyPositions.SunriseHour + 3.5f)
                       || (hour >= SkyPositions.SunsetHour - 3.5f && hour < SkyPositions.SunsetHour + 1.5f);
            }
            return hour >= SkyPositions.SunriseHour && hour < SkyPositions.SunsetHour;
        }

        /// <summary>What one carcass holds: each drop's amount rolled within its range; none of an item is left out.</summary>
        public void RollDrops(System.Random random, List<AnimalLoot> into)
        {
            foreach (AnimalDrop drop in Drops)
            {
                int count = random.Next(drop.Min, drop.Max + 1);
                if (count > 0)
                {
                    into.Add(new AnimalLoot(drop.ItemId, count));
                }
            }
        }
    }

    /// <summary>The species in the game (GL602): five for the textile chain, four for the tannery's.</summary>
    public static class AnimalSpeciesTable
    {
        public const string Textile = "textile";
        public const string Tannery = "tannery";

        /// <summary>Damage a tool does to an animal for each point of its power (GL607): a hand 5, a stone hatchet 12, steel 20.</summary>
        public const float DamagePerPower = 10f;

        private const string Grass = BiomeDefinition.GrasslandName, Forest = BiomeDefinition.ForestName, Steppe = BiomeDefinition.SteppeName,
            Highland = BiomeDefinition.HighlandName, Alpine = BiomeDefinition.AlpineName;

        public static readonly IReadOnlyList<AnimalSpecies> All = new[]
        {
            new AnimalSpecies
            {
                Id = "rabbit", Name = "Rabbit", Feeds = Textile, Biomes = new[] { Grass, Forest, Steppe }, Herd = new Vector2Int(2, 5),
                Length = 0.45f, ShoulderHeight = 0.22f, WalkSpeed = 1.2f, RunSpeed = 7.5f, Health = 8f, Skittishness = 12f,
                Routine = AnimalRoutine.Twilight, Drops = new[] { new AnimalDrop("rabbit-fur", 1, 2) },
            },
            new AnimalSpecies
            {
                Id = "goat", Name = "Goat", Feeds = Textile, Biomes = new[] { Highland, Alpine }, Herd = new Vector2Int(3, 6),
                Length = 1.1f, ShoulderHeight = 0.7f, WalkSpeed = 1.3f, RunSpeed = 6f, Health = 22f, Skittishness = 9f,
                Drops = new[] { new AnimalDrop("goat-wool", 1, 3) },
            },
            new AnimalSpecies
            {
                Id = "sheep", Name = "Sheep", Feeds = Textile, Biomes = new[] { Grass, Highland }, Herd = new Vector2Int(4, 8),
                Length = 1.2f, ShoulderHeight = 0.75f, WalkSpeed = 1f, RunSpeed = 5f, Health = 24f, Skittishness = 7f,
                Drops = new[] { new AnimalDrop("sheep-wool", 2, 4) },
            },
            new AnimalSpecies
            {
                Id = "yak", Name = "Yak", Feeds = Textile, Biomes = new[] { Alpine, Highland }, Herd = new Vector2Int(2, 5),
                Length = 2.4f, ShoulderHeight = 1.4f, WalkSpeed = 1f, RunSpeed = 5f, Health = 60f, Skittishness = 6f,
                Drops = new[] { new AnimalDrop("yak-wool", 2, 5) },
            },
            new AnimalSpecies
            {
                Id = "alpaca", Name = "Alpaca", Feeds = Textile, Biomes = new[] { Highland, Steppe }, Herd = new Vector2Int(3, 6),
                Length = 1.4f, ShoulderHeight = 0.95f, WalkSpeed = 1.2f, RunSpeed = 5.5f, Health = 28f, Skittishness = 8f,
                Drops = new[] { new AnimalDrop("alpaca-wool", 2, 4) },
            },
            new AnimalSpecies
            {
                Id = "cow", Name = "Cow", Feeds = Tannery, Biomes = new[] { Grass }, Herd = new Vector2Int(3, 7),
                Length = 2.2f, ShoulderHeight = 1.35f, WalkSpeed = 0.9f, RunSpeed = 4.5f, Health = 50f, Skittishness = 5f,
                Drops = new[] { new AnimalDrop("cow-hide", 1, 2) },
            },
            new AnimalSpecies
            {
                Id = "deer", Name = "Deer", Feeds = Tannery, Biomes = new[] { Forest, Grass }, Herd = new Vector2Int(2, 5),
                Length = 1.6f, ShoulderHeight = 1f, WalkSpeed = 1.4f, RunSpeed = 8f, Health = 30f, Skittishness = 14f,
                Routine = AnimalRoutine.Twilight, Drops = new[] { new AnimalDrop("deer-hide", 1, 1) },
            },
            new AnimalSpecies
            {
                Id = "elk", Name = "Elk", Feeds = Tannery, Biomes = new[] { Forest, Highland }, Herd = new Vector2Int(2, 4),
                Length = 2.3f, ShoulderHeight = 1.5f, WalkSpeed = 1.3f, RunSpeed = 7f, Health = 55f, Skittishness = 11f,
                Drops = new[] { new AnimalDrop("elk-hide", 1, 2) },
            },
            new AnimalSpecies
            {
                Id = "bison", Name = "Bison", Feeds = Tannery, Biomes = new[] { Steppe, Grass }, Herd = new Vector2Int(4, 9),
                Length = 2.8f, ShoulderHeight = 1.7f, WalkSpeed = 1f, RunSpeed = 6f, Health = 80f, Skittishness = 7f,
                Drops = new[] { new AnimalDrop("bison-hide", 1, 2) },
            },
        };

        /// <summary>The species of an id, or null.</summary>
        public static AnimalSpecies Find(string id)
        {
            foreach (AnimalSpecies species in All)
            {
                if (species.Id == id)
                {
                    return species;
                }
            }
            return null;
        }
    }
}
