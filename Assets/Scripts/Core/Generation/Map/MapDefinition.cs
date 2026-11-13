using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// A bounded map (GL589): its size, its outline (GL638) and the kind of edge on each side (set by hand or picked
    /// from the seed), the sea's level, its main range, the climate and the biome table. Part of
    /// <see cref="TerrainSettings"/>, read when the generator is <see cref="TerrainGeneratorType.BoundedMap"/>; the
    /// seed is the terrain's. <see cref="Resolve"/> turns it into the <see cref="MapLayout"/> everything else reads.
    /// </summary>
    [Serializable]
    public class MapDefinition
    {
        [Tooltip("Length of the map's sides in metres; the map is centred on the world's origin.")]
        [SerializeField, Min(256f)]
        private float size = 5000f;

        [Tooltip("On: the seed picks which sides are sea (as many as the outline wants) and the kind of each other side. Off: the four below are used.")]
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

        [Tooltip("On: the seed picks the map's outline from the list below. Off: the one named is used.")]
        [SerializeField]
        private bool pickShapeFromSeed = true;

        [Tooltip("The outline used when the seed doesn't pick it, by name.")]
        [SerializeField]
        private string shape = MapShapeType.ContinentName;

        [Tooltip("The outlines a map can have (GL638): continent, peninsulas, islands. Add an entry to add one.")]
        [SerializeField]
        private List<MapShapeType> shapeTypes = MapShapeType.Defaults();

        [Tooltip("How far the playable edge wanders in and out, as a share of half the map (GL639). The edge keeps this far inside the map's square.")]
        [SerializeField, Range(0f, 0.2f)]
        private float edgeWobble = 0.1f;

        [Tooltip("Metres the plains (the terrain's surface level) stand above the sea.")]
        [SerializeField, Min(0.5f)]
        private float coastHeight = 5f;

        [Tooltip("Metres the land rises from the coast to deep inland, so rivers run to the sea.")]
        [SerializeField, Min(0f)]
        private float inlandRise = 40f;

        [Tooltip("Height of the main range's highest summit over the plains, in metres; 0 for no ranges. A range on a small island is lower.")]
        [SerializeField, Min(0f)]
        private float peakHeight = 320f;

        [Tooltip("Half the width of the main range at its foot, in metres (at most a fifth of the map).")]
        [SerializeField, Min(50f)]
        private float peakRadius = 700f;

        [Tooltip("Lowest ground the player can reach, as a world height: just over the voxel world's bottom (GL640).")]
        [SerializeField]
        private float voxelFloor = 2f;

        [Tooltip("Highest ground the player can reach, as a world height: under the voxel world's top (GL640).")]
        [SerializeField]
        private float voxelCeiling = 400f;

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

        public bool PickShapeFromSeed
        {
            get => pickShapeFromSeed;
            set => pickShapeFromSeed = value;
        }

        /// <summary>The outline's name used when <see cref="PickShapeFromSeed"/> is off.</summary>
        public string ShapeName
        {
            get => shape;
            set => shape = value;
        }

        /// <summary>The outlines a map can have (GL638); add to it for more.</summary>
        public List<MapShapeType> ShapeTypes => shapeTypes;

        public float EdgeWobble => Mathf.Clamp(edgeWobble, 0f, 0.2f);
        public float CoastHeight => Mathf.Max(0.5f, coastHeight);

        public float PeakHeight
        {
            get => Mathf.Max(0f, peakHeight);
            set => peakHeight = Mathf.Max(0f, value);
        }

        public float PeakRadius => Mathf.Clamp(peakRadius, 50f, Size * 0.2f);
        public float InlandRise => Mathf.Max(0f, inlandRise);

        /// <summary>Lowest and highest ground the player can reach, as world heights (GL640).</summary>
        public float VoxelFloor
        {
            get => voxelFloor;
            set => voxelFloor = value;
        }

        public float VoxelCeiling
        {
            get => Mathf.Max(voxelFloor + 16f, voxelCeiling);
            set => voxelCeiling = value;
        }

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

        /// <summary>Sets the outline by hand, by name, and turns the seed's pick off.</summary>
        public void SetShape(string shapeTypeName)
        {
            pickShapeFromSeed = false;
            shape = shapeTypeName;
        }

        /// <summary>Everything that changes the map's shape, for cache keys.</summary>
        public string Key(int seed)
        {
            MapLayout layout = Resolve(seed, 0f);
            MapShape resolved = layout.Shape;
            uint hash = 17u;
            void Mix(float value)
            {
                hash = math.hash(new uint2(hash, math.asuint(value)));
            }
            for (int i = 0; i < resolved.Lands.Length; i++)
            {
                float4 land = resolved.Lands[i];
                Mix(land.x);
                Mix(land.y);
                Mix(land.z);
                Mix(land.w);
            }
            for (int i = 0; i < resolved.Ranges.Length; i++)
            {
                float4 bump = resolved.Ranges[i];
                Mix(bump.x);
                Mix(bump.y);
                Mix(bump.z);
                Mix(bump.w);
            }
            var key = new System.Text.StringBuilder();
            key.Append(Size).Append('|').Append(InlandRise).Append('|').Append(CoastHeight).Append('|').Append(PeakHeight).Append('|').Append(PeakRadius)
                .Append('|').Append(WaterMargin).Append('|').Append(FlowCellSize).Append('|').Append(WaterCellSize)
                .Append('|').Append(EdgeWobble).Append('|').Append(VoxelFloor).Append('|').Append(VoxelCeiling)
                .Append('|').Append(layout.ShapeType.Name).Append(resolved.CoastWander).Append('#').Append(hash);
            for (int i = 0; i < 4; i++)
            {
                BoundaryProfile p = layout.Boundary((MapSide)i).ToProfile();
                key.Append('|').Append(layout.Boundary((MapSide)i).Name).Append(p.EdgeHeight).Append(p.Flatten).Append(p.BlendWidth)
                    .Append(p.BlendPast).Append(p.Sharpness).Append(p.Wobble).Append(p.Roughness).Append(p.LimitDistance);
            }
            return key.ToString();
        }

        private const int SideSalt = 0x53494445;
        private const int ShapeSalt = 0x53484150;

        // The order the sides stand in round the map, so sea sides picked together lie side by side.
        private static readonly MapSide[] Ring = { MapSide.West, MapSide.North, MapSide.East, MapSide.South };

        /// <summary>
        /// The map this definition and a seed give. From the seed: an outline, as many sea sides as it wants (lying
        /// side by side, now and then facing each other) and a kind for each other side by the kinds' weights. By
        /// hand: the sides named, and an outline that suits their count of sea sides. Either way at least one side is
        /// an ocean (GL589): when none is, the seed picks the side that becomes one.
        /// </summary>
        /// <param name="plainsLevel">The terrain's surface level; the sea stands <see cref="CoastHeight"/> below it.</param>
        public MapLayout Resolve(int seed, float plainsLevel)
        {
            List<MapBoundaryType> types = boundaryTypes != null && boundaryTypes.Count > 0 ? boundaryTypes : MapBoundaryType.Defaults();
            List<MapShapeType> shapes = shapeTypes != null && shapeTypes.Count > 0 ? shapeTypes : MapShapeType.Defaults();
            MapBoundaryType ocean = types.Find(t => t != null && t.IsOcean);
            if (ocean == null)
            {
                ocean = MapBoundaryType.Defaults()[0];
            }
            var chosen = new MapBoundaryType[4];
            MapShapeType shapeType;
            if (pickSidesFromSeed)
            {
                shapeType = PickShape(shapes, seed, -1);
                Vector2Int wanted = shapeType.OceanSides;
                int seas = Mathf.Min(wanted.y, wanted.x + (int)(VoxelHash.Uniform(seed, 0, 1, 0, SideSalt) * (wanted.y - wanted.x + 1)));
                int start = Mathf.Min(3, (int)(VoxelHash.Uniform(seed, 1, 1, 0, SideSalt) * 4f));
                bool facing = seas == 2 && VoxelHash.Uniform(seed, 2, 1, 0, SideSalt) < 0.2f;
                for (int i = 0; i < seas; i++)
                {
                    chosen[(int)Ring[(start + (facing ? i * 2 : i)) % 4]] = ocean;
                }
                for (int i = 0; i < 4; i++)
                {
                    chosen[i] ??= PickLandSide(types, seed, i) ?? ocean;
                }
            }
            else
            {
                bool anyOcean = false;
                for (int i = 0; i < 4; i++)
                {
                    string wanted = SideName((MapSide)i);
                    chosen[i] = types.Find(t => t != null && t.Name == wanted) ?? ocean;
                    anyOcean |= chosen[i].IsOcean;
                }
                if (!anyOcean)
                {
                    chosen[Mathf.Min(3, (int)(VoxelHash.Uniform(seed, 4, 0, 0, SideSalt) * 4f))] = ocean;
                }
                int seas = 0;
                foreach (MapBoundaryType side in chosen)
                {
                    seas += side.IsOcean ? 1 : 0;
                }
                shapeType = PickShape(shapes, seed, seas);
            }

            float half = Size * 0.5f;
            float wobble = half * EdgeWobble;
            float seaLevel = plainsLevel - CoastHeight;
            var mapShape = new MapShape
            {
                Seed = seed,
                Half = half,
                SeaLevel = seaLevel,
                PlainsLevel = plainsLevel,
                West = chosen[0].ToProfile(),
                East = chosen[1].ToProfile(),
                South = chosen[2].ToProfile(),
                North = chosen[3].ToProfile(),
                InlandRise = InlandRise,
                EdgeWobble = wobble,
                EdgeRadius = half - wobble - half * 0.012f,
                CoastWander = half * 0.11f * shapeType.CoastWander,
                Floor = VoxelFloor,
                Ceiling = VoxelCeiling,
            };
            float depth = 0f;
            foreach (MapBoundaryType type in chosen)
            {
                BoundaryProfile p = type.ToProfile();
                mapShape.Reach = Mathf.Max(mapShape.Reach, p.BlendWidth + p.Wobble);
                depth = p.Ocean ? Mathf.Max(depth, -p.EdgeHeight) : depth;
            }
            // The sea's floor is real ground the player can dive to, so it stays over the voxel world's bottom.
            mapShape.OceanDepth = Mathf.Clamp(depth, 2f, Mathf.Max(2f, seaLevel - VoxelFloor - 3f));
            MapLandBuilder.Build(ref mapShape, shapeType, chosen, PeakHeight, PeakRadius);
            return new MapLayout(seed, Size, chosen, mapShape, shapeType);
        }

        // The outline: the one named, or the seed's pick by weight among those that suit a count of sea sides
        // (any count when it is below 0).
        private MapShapeType PickShape(List<MapShapeType> shapes, int seed, int seas)
        {
            if (!pickShapeFromSeed)
            {
                return shapes.Find(s => s != null && s.Name == shape) ?? shapes[0];
            }
            int misfit = int.MaxValue;
            foreach (MapShapeType candidate in shapes)
            {
                misfit = Mathf.Min(misfit, seas < 0 ? 0 : candidate.MisfitOf(seas));
            }
            float total = 0f;
            foreach (MapShapeType candidate in shapes)
            {
                total += seas < 0 || candidate.MisfitOf(seas) == misfit ? candidate.Weight : 0f;
            }
            float roll = VoxelHash.Uniform(seed, 0, 0, 0, ShapeSalt) * total;
            MapShapeType picked = null;
            foreach (MapShapeType candidate in shapes)
            {
                if (seas >= 0 && candidate.MisfitOf(seas) != misfit)
                {
                    continue;
                }
                picked = candidate;
                roll -= candidate.Weight;
                if (roll < 0f)
                {
                    break;
                }
            }
            return picked ?? shapes[0];
        }

        // The kind of a side that isn't sea: the seed's pick by weight among the kinds that aren't oceans.
        private static MapBoundaryType PickLandSide(List<MapBoundaryType> types, int seed, int side)
        {
            float total = 0f;
            foreach (MapBoundaryType type in types)
            {
                total += type != null && !type.IsOcean ? type.Weight : 0f;
            }
            float roll = VoxelHash.Uniform(seed, side, 0, 0, SideSalt) * total;
            MapBoundaryType picked = null;
            foreach (MapBoundaryType type in types)
            {
                if (type == null || type.IsOcean || type.Weight <= 0f)
                {
                    continue;
                }
                picked = type;
                roll -= type.Weight;
                if (roll < 0f)
                {
                    break;
                }
            }
            return picked;
        }
    }

    /// <summary>
    /// A resolved bounded map (GL589): the square it covers, its outline, each side's boundary, the sea level and
    /// the plain-value <see cref="MapShape"/> the height field and biomes read. Made by
    /// <see cref="MapDefinition.Resolve"/>.
    /// </summary>
    public sealed class MapLayout
    {
        private readonly MapBoundaryType[] sides;

        public MapLayout(int seed, float size, MapBoundaryType[] sides, MapShape shape, MapShapeType shapeType)
        {
            Seed = seed;
            Size = size;
            this.sides = sides;
            Shape = shape;
            ShapeType = shapeType;
        }

        public int Seed { get; }

        /// <summary>Length of the map's sides, in metres.</summary>
        public float Size { get; }

        /// <summary>The square the map covers (x and z), centred on the origin; the playable edge wanders inside it.</summary>
        public Rect Bounds => new Rect(-Size * 0.5f, -Size * 0.5f, Size, Size);

        public float SeaLevel => Shape.SeaLevel;

        /// <summary>Where the main range's highest summit stands (x, z).</summary>
        public Vector2 PeakPosition => new Vector2(Shape.PeakCentre.x, Shape.PeakCentre.y);

        public MapShape Shape { get; }

        /// <summary>The map's outline (GL638).</summary>
        public MapShapeType ShapeType { get; }

        /// <summary>How many round lands and bays the outline was laid from, and how many bumps its ranges are.</summary>
        public int LandCount => Shape.Lands.Length;

        public int RangeBumpCount => Shape.Ranges.Length;

        /// <summary>The kind of edge on a side.</summary>
        public MapBoundaryType Boundary(MapSide side)
        {
            return sides[(int)side];
        }

        /// <summary>The kind of edge that holds a point: the side it lies towards, as the sides share the land.</summary>
        public MapBoundaryType BoundaryAt(float x, float z)
        {
            return sides[Shape.SideAt(x, z)];
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

        /// <summary>True inside the playable edge (GL639): a wandering line inside the map's square.</summary>
        public bool Contains(float x, float z)
        {
            return Shape.EdgeInside(x, z) >= 0f;
        }

        /// <summary>
        /// How far into an edge's limit a point is (GL640): 0 where the player walks freely, 1 at the line they
        /// cannot pass.
        /// </summary>
        public float LimitAt(float x, float z)
        {
            return Shape.LimitAt(x, z);
        }

        /// <summary>The direction out of the map at a point (x, z; length 1): the way a limit holds the player back from.</summary>
        public Vector2 OutwardAt(float x, float z)
        {
            float2 outward = Shape.OutwardAt(x, z);
            return new Vector2(outward.x, outward.y);
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
