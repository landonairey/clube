using Unity.Collections;
using Unity.Mathematics;

namespace Clube.Core
{
    /// <summary>What a cell of the <see cref="WaterMap"/> holds (GL307).</summary>
    public enum WaterKind : byte
    {
        Dry = 0,
        River = 1,
        Lake = 2,
    }

    /// <summary>
    /// Rivers and lakes of a world (GL307), as a grid of 1 m cells over the area <see cref="Hydrology"/>
    /// worked them out for: how far each river bed is cut into the ground, where the water stands and how
    /// deep it is, which way rivers flow, and how far each cell is from a river and from a lake. Built once
    /// per world; read by the height function (the cut beds are in the terrain data), the fill (sand and
    /// clay by the water) and the water surface. Burst-compatible: the arrays are read only, so any number
    /// of jobs share it. Outside its area, and when not created, everything is dry.
    /// </summary>
    public struct WaterMap
    {
        public float2 Origin;
        public float CellSize;
        public int2 Size;

        /// <summary>Metres the river beds are cut into the ground, per cell.</summary>
        [ReadOnly] public NativeArray<float> Carve;
        /// <summary>Height of the water's surface, per cell (meaningful where <see cref="Depth"/> is above 0).</summary>
        [ReadOnly] public NativeArray<float> Surface;
        /// <summary>Depth of water over the (cut) ground, per cell; 0 where dry.</summary>
        [ReadOnly] public NativeArray<float> Depth;
        [ReadOnly] public NativeArray<byte> Kind;
        /// <summary>A river's flow direction (x, z) times its speed, per cell; zero for lakes and dry land.</summary>
        [ReadOnly] public NativeArray<float2> Flow;
        /// <summary>Metres to the nearest lake water, capped at <see cref="MaxDistance"/>.</summary>
        [ReadOnly] public NativeArray<byte> LakeDistance;
        /// <summary>Metres to the nearest river water, capped at <see cref="MaxDistance"/>.</summary>
        [ReadOnly] public NativeArray<byte> RiverDistance;

        /// <summary>The materials and depths the fill puts by the water.</summary>
        public WaterBanks Banks;

        public const int MaxDistance = 15;

        /// <summary>How far, in metres, the sand's outer edge wanders either side of its limit (GL506).</summary>
        public const float ShoreJitter = 1.5f;

        /// <summary>What <see cref="MaterialAt"/> gives where the layer's material stays.</summary>
        public const byte Keep = 255;

        /// <summary>The least water a river cell stands over its bed, in metres (GL336): over a voxel at 0.25 m voxels.</summary>
        public const float MinRiverDepth = 0.3f;

        private static WaterMap none;

        /// <summary>
        /// No water: empty arrays rather than unassigned ones, since a job refuses unassigned containers even
        /// unread. Shared and never freed until the domain unloads.
        /// </summary>
        public static WaterMap None
        {
            get
            {
                if (!none.Carve.IsCreated)
                {
                    none = new WaterMap
                    {
                        Carve = new NativeArray<float>(0, Allocator.Persistent),
                        Surface = new NativeArray<float>(0, Allocator.Persistent),
                        Depth = new NativeArray<float>(0, Allocator.Persistent),
                        Kind = new NativeArray<byte>(0, Allocator.Persistent),
                        Flow = new NativeArray<float2>(0, Allocator.Persistent),
                        LakeDistance = new NativeArray<byte>(0, Allocator.Persistent),
                        RiverDistance = new NativeArray<byte>(0, Allocator.Persistent),
                    };
                    System.AppDomain.CurrentDomain.DomainUnload += (_, __) => none.Dispose();
                }
                return none;
            }
        }

        public bool IsCreated => Carve.IsCreated && Carve.Length > 0;

        /// <summary>The cell holding a position, or false outside the map.</summary>
        public bool TryCell(float2 position, out int index)
        {
            index = 0;
            if (!IsCreated)
            {
                return false;
            }
            int2 cell = (int2)math.floor((position - Origin) / CellSize);
            if (math.any(cell < 0) || math.any(cell >= Size))
            {
                return false;
            }
            index = cell.x + cell.y * Size.x;
            return true;
        }

        /// <summary>How far the ground is cut down at a position, smoothly between cells.</summary>
        public float CarveAt(float x, float z)
        {
            if (!IsCreated)
            {
                return 0f;
            }
            float2 p = (new float2(x, z) - Origin) / CellSize - 0.5f;
            int2 c = (int2)math.floor(p);
            if (c.x < -1 || c.y < -1 || c.x >= Size.x || c.y >= Size.y)
            {
                return 0f;
            }
            float2 t = p - c;
            float a = At(Carve, c.x, c.y), b = At(Carve, c.x + 1, c.y);
            float d = At(Carve, c.x, c.y + 1), e = At(Carve, c.x + 1, c.y + 1);
            return math.lerp(math.lerp(a, b, t.x), math.lerp(d, e, t.x), t.y);
        }

        /// <summary>
        /// The water's surface at a position in a wet cell, smoothly between the wet cells round it (so a river's surface
        /// slopes evenly instead of stepping cell by cell); false on dry land (GL315).
        /// </summary>
        public bool TrySurfaceAt(float2 position, out float surface, out int cell)
        {
            surface = 0f;
            if (!TryCell(position, out cell) || Kind[cell] == 0)
            {
                return false;
            }
            float2 p = (position - Origin) / CellSize - 0.5f;
            int2 c = (int2)math.floor(p);
            float2 t = p - c;
            float sum = 0f, weight = 0f;
            for (int dz = 0; dz <= 1; dz++)
            {
                for (int dx = 0; dx <= 1; dx++)
                {
                    int x = c.x + dx, z = c.y + dz;
                    if (x < 0 || z < 0 || x >= Size.x || z >= Size.y || Kind[x + z * Size.x] == 0)
                    {
                        continue;
                    }
                    float w = (dx == 0 ? 1f - t.x : t.x) * (dz == 0 ? 1f - t.y : t.y) + 1e-4f;
                    sum += Surface[x + z * Size.x] * w;
                    weight += w;
                }
            }
            // Never over the cell's own level: a river cell blended towards the cell above it stood as a slab over its bed.
            surface = weight > 0f ? math.min(sum / weight, Surface[cell]) : Surface[cell];
            // But a river always has some water over its bed (GL336): held down to the bank beside it, a steep stream's
            // water sat under half a voxel deep, and those stretches filled no sample at all, leaving gaps.
            if (Kind[cell] == (byte)WaterKind.River)
            {
                surface = math.max(surface, Surface[cell] - Depth[cell] + MinRiverDepth);
            }
            return true;
        }

        /// <summary>
        /// 0 at the water and on its banks, rising to 1 by <paramref name="reach"/> metres off (GL318): slope ridges fade
        /// out by the water, so a lake's rim stays where the drainage put it and the water never stands over a gully.
        /// </summary>
        public float ShoreFade(float2 position, float reach)
        {
            if (!IsCreated || !TryCell(position, out int cell))
            {
                return 1f;
            }
            float distance = math.min((int)LakeDistance[cell], (int)RiverDistance[cell]);
            return math.smoothstep(1f, reach, distance);
        }

        private float At(NativeArray<float> values, int x, int z)
        {
            x = math.clamp(x, 0, Size.x - 1);
            z = math.clamp(z, 0, Size.y - 1);
            return values[x + z * Size.x];
        }

        /// <summary>
        /// The material by the water for a sample <paramref name="depth"/> metres below the surface of a
        /// column at <paramref name="position"/>, or <see cref="Keep"/> to keep the layer's: sand on lake and river beds
        /// and lake shores, clay in patches along river banks and on deeper lake floors.
        /// </summary>
        public byte MaterialAt(float2 position, float depth, int seed)
        {
            if (depth < 0f || !TryCell(position, out int i))
            {
                return Keep;
            }
            WaterKind kind = (WaterKind)Kind[i];
            float water = Depth[i];
            float patch = ValueNoise.At(seed, position.x / Banks.PatchSize, position.y / Banks.PatchSize, 0, 0x5741);
            if (water > 0f)
            {
                if (kind == WaterKind.Lake && water >= Banks.ClayFloorDepth && patch > 0.45f)
                {
                    return depth < Banks.ClayDepth ? Banks.Clay : Keep;
                }
                return depth < Banks.SandDepth ? Banks.Sand : depth < Banks.SandDepth + Banks.ClayDepth && kind == WaterKind.Lake ? Banks.Clay : Keep;
            }
            // The shore's edge wanders with a smooth noise (GL506): the distance is whole metres, so a fixed limit stepped.
            float wander = (ValueNoise.At(seed, position.x / 2.5f, position.y / 2.5f, 0, 0x5348) - 0.5f) * 2f * ShoreJitter;
            if (LakeDistance[i] <= Banks.ShoreSand + wander)
            {
                return depth < Banks.SandDepth * 0.75f ? Banks.Sand : Keep;
            }
            if (RiverDistance[i] <= Banks.BankClay && patch > 1f - Banks.BankClayCover)
            {
                return depth < Banks.ClayDepth ? Banks.Clay : Keep;
            }
            return Keep;
        }

        public void Dispose()
        {
            if (!Carve.IsCreated)
            {
                return;
            }
            Carve.Dispose();
            Surface.Dispose();
            Depth.Dispose();
            Kind.Dispose();
            Flow.Dispose();
            LakeDistance.Dispose();
            RiverDistance.Dispose();
        }
    }

    /// <summary>The sand and clay the fill lays by rivers and lakes (GL307), as job data.</summary>
    public struct WaterBanks
    {
        public byte Sand;
        public byte Clay;
        /// <summary>Metres of sand on beds; lake shores take three quarters of it.</summary>
        public float SandDepth;
        /// <summary>Metres of clay where clay lies.</summary>
        public float ClayDepth;
        /// <summary>Lake floors under at least this much water have clay patches.</summary>
        public float ClayFloorDepth;
        /// <summary>Metres from lake water that are sandy shore.</summary>
        public float ShoreSand;
        /// <summary>Metres from river water that may be clay bank.</summary>
        public float BankClay;
        /// <summary>Share of the river banks that is clay, 0-1.</summary>
        public float BankClayCover;
        /// <summary>Size of the clay patches, in metres.</summary>
        public float PatchSize;
    }
}
