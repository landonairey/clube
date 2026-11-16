using System;
using System.Collections.Generic;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// A bounded map (GL589): its size, the kind of edge on each side (set by hand or picked from the seed), the sea's
    /// level, its one great peak, the climate and the biome table. Part of <see cref="TerrainSettings"/>, read when the
    /// generator is <see cref="TerrainGeneratorType.BoundedMap"/>; the seed is the terrain's.
    /// <see cref="Resolve"/> turns it into the <see cref="MapLayout"/> everything else reads.
    /// </summary>
    [Serializable]
    public class MapDefinition
    {
        [Tooltip("Length of the map's sides in metres; the map is centred on the world's origin.")]
        [SerializeField, Min(256f)]
        private float size = 5000f;

        [Tooltip("On: each side's boundary is picked from the seed. Off: the four below are used.")]
        [SerializeField]
        private bool pickSidesFromSeed = true;

        [Tooltip("Boundary type names for the west (-x), east (+x), south (-z) and north (+z) sides.")]
        [SerializeField]
        private string[] sides =
        {
            MapBoundaryType.OceanName, MapBoundaryType.OceanName, MapBoundaryType.OceanName, MapBoundaryType.OceanName,
        };

        [Tooltip("The kinds of edge a side can have (GL590). Add an entry to add a kind.")]
        [SerializeField]
        private List<MapBoundaryType> boundaryTypes = MapBoundaryType.Defaults();

        [Tooltip("Metres the plains (the terrain's surface level) stand above the sea.")]
        [SerializeField, Min(0.5f)]
        private float coastHeight = 5f;

        [Tooltip("Metres the land rises from an ocean edge to the far side of the map, so rivers run to the sea.")]
        [SerializeField, Min(0f)]
        private float inlandRise = 40f;

        [Tooltip("Height of the map's one great peak over the land round it, in metres; 0 for none.")]
        [SerializeField, Min(0f)]
        private float peakHeight = 320f;

        [Tooltip("Radius of the great peak's foot, in metres (at most a fifth of the map).")]
        [SerializeField, Min(50f)]
        private float peakRadius = 1100f;

        [Tooltip("Metres of water map past each edge: how far out rivers and the sea are worked out cell by cell.")]
        [SerializeField, Range(32f, 1024f)]
        private float waterMargin = 256f;

        [Tooltip("Size of the cells the rivers' courses are found on, in metres.")]
        [SerializeField, Range(4f, 16f)]
        private float flowCellSize = 8f;

        [Tooltip("Size of the water map's cells in metres. 1 is the unbounded worlds' detail; a 5 km map at 1 m needs about 700 MB, at 2 m about 175 MB.")]
        [SerializeField, Range(1f, 4f)]
        private float waterCellSize = 2f;

        [SerializeField]
        private MapClimate climate = new MapClimate();

        [Tooltip("The biomes, first match wins (GL591). Empty: the built-in table, from the terrain's own layer materials.")]
        [SerializeField]
        private List<BiomeDefinition> biomes = new List<BiomeDefinition>();

        public float Size
        {
            get => Mathf.Max(256f, size);
            set => size = Mathf.Max(256f, value);
        }

        public bool PickSidesFromSeed
        {
            get => pickSidesFromSeed;
            set => pickSidesFromSeed = value;
        }

        /// <summary>The kinds of edge a side can have; add to it for more.</summary>
        public List<MapBoundaryType> BoundaryTypes => boundaryTypes;

        public float CoastHeight => Mathf.Max(0.5f, coastHeight);

        public float PeakHeight
        {
            get => Mathf.Max(0f, peakHeight);
            set => peakHeight = Mathf.Max(0f, value);
        }

        public float PeakRadius => Mathf.Clamp(peakRadius, 50f, Size * 0.2f);
        public float InlandRise => Mathf.Max(0f, inlandRise);
        public float WaterMargin => Mathf.Clamp(waterMargin, 32f, 1024f);
        public float FlowCellSize => Mathf.Clamp(flowCellSize, 4f, 16f);

        public float WaterCellSize
        {
            get => Mathf.Clamp(waterCellSize, 1f, 4f);
            set => waterCellSize = Mathf.Clamp(value, 1f, 4f);
        }

        public MapClimate Climate => climate ??= new MapClimate();

        /// <summary>The biome table as set; empty means the built-in one (<see cref="BiomeDefinition.Defaults"/>).</summary>
        public List<BiomeDefinition> Biomes => biomes;

        /// <summary>The boundary type name set by hand for a side (used when <see cref="PickSidesFromSeed"/> is off).</summary>
        public string SideName(MapSide side)
        {
            return sides != null && (int)side < sides.Length ? sides[(int)side] : MapBoundaryType.OceanName;
        }

        /// <summary>Sets a side's boundary by hand, by type name; turn <see cref="PickSidesFromSeed"/> off to use it.</summary>
        public void SetSide(MapSide side, string boundaryTypeName)
        {
            if (sides == null || sides.Length != 4)
            {
                Array.Resize(ref sides, 4);
            }
            sides[(int)side] = boundaryTypeName;
        }

        /// <summary>Sets all four sides by hand and turns the seed's pick off.</summary>
        public void SetSides(string west, string east, string south, string north)
        {
            pickSidesFromSeed = false;
            SetSide(MapSide.West, west);
            SetSide(MapSide.East, east);
            SetSide(MapSide.South, south);
            SetSide(MapSide.North, north);
        }

        /// <summary>Everything that changes the map's shape, for cache keys.</summary>
        public string Key(int seed)
        {
            MapLayout layout = Resolve(seed, 0f);
            var key = new System.Text.StringBuilder();
            key.Append(Size).Append('|').Append(InlandRise).Append('|').Append(CoastHeight).Append('|').Append(PeakHeight).Append('|').Append(PeakRadius)
                .Append('|').Append(WaterMargin).Append('|').Append(FlowCellSize).Append('|').Append(WaterCellSize);
            for (int i = 0; i < 4; i++)
            {
                BoundaryProfile p = layout.Boundary((MapSide)i).ToProfile();
                key.Append('|').Append(layout.Boundary((MapSide)i).Name).Append(p.EdgeHeight).Append(p.Flatten).Append(p.BlendWidth)
                    .Append(p.Sharpness).Append(p.Wobble).Append(p.Roughness);
            }
            return key.ToString();
        }

        private const int SideSalt = 0x53494445;
        private const int PeakSalt = 0x5045414B;

        /// <summary>
        /// The map this definition and a seed give. The sides come from the seed or from the names set by hand; either
        /// way at least one is an ocean (GL589): when none is, the seed picks the side that becomes one.
        /// </summary>
        /// <param name="plainsLevel">The terrain's surface level; the sea stands <see cref="CoastHeight"/> below it.</param>
        public MapLayout Resolve(int seed, float plainsLevel)
        {
            List<MapBoundaryType> types = boundaryTypes != null && boundaryTypes.Count > 0 ? boundaryTypes : MapBoundaryType.Defaults();
            MapBoundaryType ocean = types.Find(t => t != null && t.IsOcean);
            if (ocean == null)
            {
                ocean = MapBoundaryType.Defaults()[0];
            }
            var chosen = new MapBoundaryType[4];
            bool anyOcean = false;
            for (int i = 0; i < 4; i++)
            {
                if (pickSidesFromSeed)
                {
                    chosen[i] = types[Mathf.Min(types.Count - 1, (int)(VoxelHash.Uniform(seed, i, 0, 0, SideSalt) * types.Count))];
                }
                else
                {
                    string wanted = SideName((MapSide)i);
                    chosen[i] = types.Find(t => t != null && t.Name == wanted);
                }
                chosen[i] ??= ocean;
                anyOcean |= chosen[i].IsOcean;
            }
            if (!anyOcean)
            {
                chosen[Mathf.Min(3, (int)(VoxelHash.Uniform(seed, 4, 0, 0, SideSalt) * 4f))] = ocean;
            }

            float half = Size * 0.5f;
            // The peak stands in the middle part of the map, clear of every edge's reach.
            var peak = new Vector2(
                (VoxelHash.Uniform(seed, 0, 0, 0, PeakSalt) - 0.5f) * 2f * half * 0.3f,
                (VoxelHash.Uniform(seed, 1, 0, 0, PeakSalt) - 0.5f) * 2f * half * 0.3f);
            var shape = new MapShape
            {
                Seed = seed,
                Half = half,
                SeaLevel = plainsLevel - CoastHeight,
                West = chosen[0].ToProfile(),
                East = chosen[1].ToProfile(),
                South = chosen[2].ToProfile(),
                North = chosen[3].ToProfile(),
                PeakCentre = peak,
                PeakHeight = PeakHeight,
                PeakRadius = PeakRadius,
                InlandRise = InlandRise,
            };
            shape.Reach = 0f;
            foreach (MapBoundaryType type in chosen)
            {
                BoundaryProfile p = type.ToProfile();
                shape.Reach = Mathf.Max(shape.Reach, p.BlendWidth + p.Wobble);
            }
            return new MapLayout(seed, Size, chosen, shape);
        }
    }

    /// <summary>
    /// A resolved bounded map (GL589): the square it covers, each side's boundary, the sea level and the plain-value
    /// <see cref="MapShape"/> the height field and biomes read. Made by <see cref="MapDefinition.Resolve"/>.
    /// </summary>
    public sealed class MapLayout
    {
        private readonly MapBoundaryType[] sides;

        public MapLayout(int seed, float size, MapBoundaryType[] sides, MapShape shape)
        {
            Seed = seed;
            Size = size;
            this.sides = sides;
            Shape = shape;
        }

        public int Seed { get; }

        /// <summary>Length of the map's sides, in metres.</summary>
        public float Size { get; }

        /// <summary>The square the map covers (x and z), centred on the origin.</summary>
        public Rect Bounds => new Rect(-Size * 0.5f, -Size * 0.5f, Size, Size);

        public float SeaLevel => Shape.SeaLevel;

        /// <summary>Where the great peak stands (x, z).</summary>
        public Vector2 PeakPosition => new Vector2(Shape.PeakCentre.x, Shape.PeakCentre.y);

        public MapShape Shape { get; }

        /// <summary>The kind of edge on a side.</summary>
        public MapBoundaryType Boundary(MapSide side)
        {
            return sides[(int)side];
        }

        /// <summary>How many sides are ocean: 1 to 4 (4 is an island).</summary>
        public int OceanSides
        {
            get
            {
                int count = 0;
                foreach (MapBoundaryType side in sides)
                {
                    count += side.IsOcean ? 1 : 0;
                }
                return count;
            }
        }

        /// <summary>True inside the map's square.</summary>
        public bool Contains(float x, float z)
        {
            return Mathf.Abs(x) <= Size * 0.5f && Mathf.Abs(z) <= Size * 0.5f;
        }
    }

    /// <summary>The climate biomes are picked from (GL591, PK5): temperature in degrees, moisture 0-1.</summary>
    [Serializable]
    public class MapClimate
    {
        [Tooltip("Temperature at sea level in the middle of the map, in degrees.")]
        public float BaseTemperature = 13f;

        [Tooltip("Degrees lost per metre climbed.")]
        [Min(0f)]
        public float LapseRate = 0.045f;

        [Tooltip("Degrees colder at the north edge (and warmer at the south) than the middle.")]
        public float NorthCooling = 4f;

        [Tooltip("Degrees the temperature wanders either way from place to place.")]
        [Min(0f)]
        public float TemperatureNoise = 4f;

        [Tooltip("Width of the wet and dry regions, in metres.")]
        [Min(50f)]
        public float RegionSize = 1400f;
    }
}
