using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// Large boulders on the surface (GL339, reshaped in GL342): a rare few among the small rocks, metres across, stamped
    /// into the terrain after it's generated like trees are (<see cref="TreeStamp"/>, as <see cref="TreePart.Boulder"/>
    /// parts). At most one stands in a cell of <see cref="CellSize"/> metres; about <see cref="Share"/> of the boulders the
    /// rock chance gives are large. Each is a squashed ellipsoid turned to face any way, cut by two to four planes into flat
    /// faces (sometimes a flat top), and lifted so it often meets the ground on a base narrower than its waist; one in three
    /// has a smaller lobe leaning off it. Kept out of the start area and off water. Everything comes from the seed, the
    /// cell and the ground height, so every chunk that reaches a boulder builds the same one. Main thread only (cached).
    /// </summary>
    public sealed class BoulderField
    {
        /// <summary>Width of the cell that holds at most one large boulder, in metres.</summary>
        public const float CellSize = 48f;

        /// <summary>About this share of the boulders the rock chance gives are large ones.</summary>
        public const float Share = 0.02f;

        /// <summary>Smallest and largest half-width of a large boulder, in metres (2-5 times GL339's 2-3 m).</summary>
        public const float MinRadius = 4f;
        public const float MaxRadius = 12f;

        // A small rock is about one in this many columns' rocks (ChunkFillKernel.BoulderOneIn); kept in step.
        private const int BoulderOneIn = ChunkFillKernel.BoulderOneIn;

        private const int Salt = 0x424F554C;
        private const int MaxCachedCells = 16384;

        private readonly int seed;
        private readonly float perCell;
        private readonly HeightfieldGenerator ground;
        private readonly byte material;
        private readonly Rect keepClear;
        private readonly Dictionary<Vector2Int, TreePart[]> cells = new Dictionary<Vector2Int, TreePart[]>();
        private readonly List<TreePart> building = new List<TreePart>();

        /// <param name="rockChance">The small rocks' chance per column of <paramref name="voxelSize"/> (TerrainLayers).</param>
        public BoulderField(int seed, float rockChance, float voxelSize, HeightfieldGenerator ground, byte material, Rect keepClear = default)
        {
            this.seed = seed;
            this.ground = ground;
            this.material = material;
            this.keepClear = keepClear;
            float boulderPerSquareMetre = rockChance / BoulderOneIn / (voxelSize * voxelSize);
            perCell = math.saturate(boulderPerSquareMetre * CellSize * CellSize * Share);
        }

        /// <summary>The large boulders for a terrain, or null when it has no rocks or no heightfield to stand them on.</summary>
        public static BoulderField Create(TerrainSettings terrain, ITerrainGenerator generator, float voxelSize)
        {
            if (!(generator is HeightfieldGenerator heightfield) || terrain.Layers == null || terrain.Layers.RockChance <= 0f
                || terrain.Layers.Rock == null)
            {
                return null;
            }
            Rect clear = terrain.StartArea != null && terrain.StartArea.Enabled ? terrain.StartArea.Area : default;
            return new BoulderField(terrain.Seed, terrain.Layers.RockChance, voxelSize, heightfield, terrain.Layers.Rock.Id, clear);
        }

        /// <summary>Chance that a cell holds a large boulder.</summary>
        public float ChancePerCell => perCell;

        /// <summary>The parts of the boulder in a cell, empty when it has none. The same every time.</summary>
        public IReadOnlyList<TreePart> PartsIn(Vector2Int cell)
        {
            if (cells.TryGetValue(cell, out TreePart[] parts))
            {
                return parts;
            }
            if (cells.Count >= MaxCachedCells)
            {
                cells.Clear();
            }
            parts = Build(cell);
            cells[cell] = parts;
            return parts;
        }

        /// <summary>Adds every boulder part within <paramref name="margin"/> of the box to <paramref name="results"/>.</summary>
        public void CollectParts(Bounds box, List<TreePart> results, float margin)
        {
            // A boulder keeps inside its cell's margin, so the cells under the box and one round them hold all that reach it.
            int fromX = Mathf.FloorToInt((box.min.x - margin) / CellSize) - 1;
            int fromZ = Mathf.FloorToInt((box.min.z - margin) / CellSize) - 1;
            int toX = Mathf.FloorToInt((box.max.x + margin) / CellSize) + 1;
            int toZ = Mathf.FloorToInt((box.max.z + margin) / CellSize) + 1;
            for (int z = fromZ; z <= toZ; z++)
            {
                for (int x = fromX; x <= toX; x++)
                {
                    foreach (TreePart part in PartsIn(new Vector2Int(x, z)))
                    {
                        if (part.Bounds(margin).Intersects(box))
                        {
                            results.Add(part);
                        }
                    }
                }
            }
        }

        private float Roll(Vector2Int cell, int what)
        {
            return VoxelHash.Uniform(seed, cell.x, what, cell.y, Salt);
        }

        private TreePart[] Build(Vector2Int cell)
        {
            if (perCell <= 0f || Roll(cell, 0) >= perCell)
            {
                return System.Array.Empty<TreePart>();
            }
            // Mostly the smaller sizes; now and then a giant.
            float size = Roll(cell, 1);
            float radius = math.lerp(MinRadius, MaxRadius, size * size);
            // Anywhere in the cell that leaves the boulder (and its lobe) inside it.
            float margin = radius * 1.6f;
            float room = math.max(0f, CellSize - 2f * margin);
            var centre = new float2(
                cell.x * CellSize + margin + Roll(cell, 2) * room,
                cell.y * CellSize + margin + Roll(cell, 3) * room);
            if (!Clear(centre, radius))
            {
                return System.Array.Empty<TreePart>();
            }

            building.Clear();
            float turn = Roll(cell, 4) * 2f * math.PI;
            var radii = new float3(radius, radius * math.lerp(0.45f, 0.8f, Roll(cell, 5)), radius * math.lerp(0.6f, 1f, Roll(cell, 6)));
            // Stand it on the lowest ground under it, so the downhill side doesn't float; lifted, its base is narrower than its
            // waist (an overhang all round), sunk it's a low dome.
            float floor = LowestGround(centre, radius * 0.6f);
            float lift = radii.y * math.lerp(-0.25f, 0.5f, Roll(cell, 7));
            building.Add(Shape(cell, new float3(centre.x, floor + lift, centre.y), radii, turn, 10));

            // One in three leans a smaller lobe off its side: a ledge, a shoulder, a second stone.
            if (Roll(cell, 8) < 0.34f)
            {
                float angle = Roll(cell, 9) * 2f * math.PI;
                math.sincos(angle, out float s, out float c);
                float scale = math.lerp(0.4f, 0.65f, Roll(cell, 11));
                float3 lobeRadii = radii * scale;
                var at = new float2(centre.x + c * radius * 0.75f, centre.y + s * radius * 0.75f);
                float lobeFloor = LowestGround(at, lobeRadii.x * 0.6f);
                float lobeLift = lobeRadii.y * math.lerp(-0.1f, 0.6f, Roll(cell, 12));
                building.Add(Shape(cell, new float3(at.x, lobeFloor + lobeLift, at.y), lobeRadii, turn + angle, 20));
            }
            return building.ToArray();
        }

        // Two to four flat faces: steep side faces at a random bearing and tilt, cut a varying way in, and sometimes a flat top.
        private TreePart Shape(Vector2Int cell, float3 centre, float3 radii, float turn, int salt)
        {
            var cuts = new float4[4];
            int count = 2 + (int)(Roll(cell, salt) * 2.99f);
            for (int i = 0; i < count; i++)
            {
                bool top = i == 0 && Roll(cell, salt + 1) < 0.4f;
                float bearing = Roll(cell, salt + 2 + i * 3) * 2f * math.PI;
                float tilt = top ? math.lerp(1.2f, 1.57f, Roll(cell, salt + 3 + i * 3)) : math.lerp(-0.3f, 0.7f, Roll(cell, salt + 3 + i * 3));
                math.sincos(bearing, out float sb, out float cb);
                math.sincos(tilt, out float st, out float ct);
                var normal = new float3(cb * ct, st, sb * ct);
                // How far the ellipsoid reaches that way, and the face set between 55% and 85% of it.
                float reach = math.length(normal * radii);
                cuts[i] = new float4(normal, reach * math.lerp(0.55f, 0.85f, Roll(cell, salt + 4 + i * 3)));
            }
            return TreePart.Boulder(centre, radii, turn, cuts[0], cuts[1], cuts[2], cuts[3], material);
        }

        private float LowestGround(float2 centre, float spread)
        {
            float low = ground.Height(centre.x, centre.y);
            low = math.min(low, ground.Height(centre.x + spread, centre.y));
            low = math.min(low, ground.Height(centre.x - spread, centre.y));
            low = math.min(low, ground.Height(centre.x, centre.y + spread));
            return math.min(low, ground.Height(centre.x, centre.y - spread));
        }

        // Not in the start area (it's for building), and not in or right beside a river or lake.
        private bool Clear(float2 centre, float radius)
        {
            if (keepClear.width > 0f)
            {
                Rect grown = keepClear;
                grown.xMin -= radius * 1.6f;
                grown.yMin -= radius * 1.6f;
                grown.xMax += radius * 1.6f;
                grown.yMax += radius * 1.6f;
                if (grown.Contains(new Vector2(centre.x, centre.y)))
                {
                    return false;
                }
            }
            WaterMap water = ground.Water;
            if (water.IsCreated && water.TryCell(centre, out int cell)
                && (water.Kind[cell] != (byte)WaterKind.Dry
                    || math.min((int)water.LakeDistance[cell], (int)water.RiverDistance[cell]) < radius + 2f))
            {
                return false;
            }
            return true;
        }
    }
}
