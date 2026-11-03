using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// A merchant's table standing at a fixed place in the world (GL38): once the chunk column
    /// there has loaded, levels one build square of ground under it and puts the table on it.
    /// The leveling goes through <see cref="World.SetDensity"/> like any edit, so it's kept with
    /// the chunk and the borders agree. Done once per run.
    /// </summary>
    /// <remarks>
    /// The square is a cell of the build grid inside the chunk at x = 0, z = 0, so one chunk
    /// holds all of it. A ramp of one sample around the square lets the ground meet the level
    /// part without a lip.
    /// </remarks>
    public class MerchantSite : MonoBehaviour
    {
        // Metres of air left above the level square, for the table and the player at it.
        private const float Headroom = 3f;

        // Metres of ground made solid below it, so no gap shows if the ground was lower beside it.
        private const float Fill = 2f;

        [SerializeField]
        private WorldView worldView;

        [Tooltip("The table to stand on the level square.")]
        [SerializeField]
        private GameObject tablePrefab;

        [Tooltip("The build cell to level, counted from the world origin in build cells (x, z).")]
        [SerializeField]
        private Vector2Int cell = new Vector2Int(1, 1);

        private bool done;

        /// <summary>The table once placed, or null.</summary>
        public GameObject Table { get; private set; }

        private void Update()
        {
            if (done || worldView == null || !worldView.IsReady || tablePrefab == null)
            {
                return;
            }
            if (!ColumnLoaded())
            {
                return;
            }
            done = true;
            Place();
        }

        // Every chunk layer of the column holding the cell, which is also where the ground is read.
        private bool ColumnLoaded()
        {
            World world = worldView.World;
            Vector3 centre = CellCentre();
            Vector3Int column = world.Grid.WorldToChunk(new Vector3(centre.x, 0f, centre.z));
            for (int y = 0; y < worldView.Config.WorldHeightInChunks; y++)
            {
                if (!world.IsLoaded(new Vector3Int(column.x, y, column.z)))
                {
                    return false;
                }
            }
            return true;
        }

        private Vector3 CellCentre()
        {
            float size = worldView.Config.BuildCellSize;
            return new Vector3((cell.x + 0.5f) * size, 0f, (cell.y + 0.5f) * size);
        }

        private void Place()
        {
            World world = worldView.World;
            WorldGrid grid = world.Grid;
            float iso = worldView.Config.IsoLevel;
            float voxel = grid.VoxelSize;
            Vector3 centre = CellCentre();
            MaterialRegistry materials = worldView.Config.Materials;
            float ground = GroundHeight(world, materials, centre, iso, worldView.Config.WorldHeightInChunks);
            VoxelMaterial grass = materials != null ? materials.Find("Grass") : null;
            VoxelMaterial dirt = materials != null ? materials.Find("Dirt") : null;

            // The square, and one sample around it for the ramp.
            float half = worldView.Config.BuildCellSize * 0.5f + voxel;
            ClearTrees(world, materials, centre, half, ground, iso);
            Vector3Int low = grid.WorldToNearestSample(new Vector3(centre.x - half, ground - Fill, centre.z - half));
            Vector3Int high = grid.WorldToNearestSample(new Vector3(centre.x + half, ground + Headroom, centre.z + half));
            for (int z = low.z; z <= high.z; z++)
            {
                for (int y = low.y; y <= high.y; y++)
                {
                    for (int x = low.x; x <= high.x; x++)
                    {
                        var sample = new Vector3Int(x, y, z);
                        Vector3 position = grid.SampleToWorld(sample);
                        float depth = ground - position.y;
                        float before = world.GetDensity(sample) ?? 0f;
                        float density = TerrainDensity.FromDepth(depth);
                        world.SetDensity(sample, density);
                        // Ground that wasn't there gets topsoil; what was stays as it was.
                        if (before < iso && density >= iso)
                        {
                            VoxelMaterial made = depth < 0.6f ? grass : dirt;
                            if (made != null)
                            {
                                world.SetMaterial(sample, made.Id);
                            }
                        }
                    }
                }
            }

            Vector3 at = worldView.transform.TransformPoint(new Vector3(centre.x, ground, centre.z));
            Table = Instantiate(tablePrefab, at, Quaternion.identity, transform);
            Table.name = "Merchant table (spawn)";
        }

        // Most plant samples a tree is cleared by: well over the biggest generated tree.
        private const int MaxTreeSamples = 60000;

        private static bool IsPlant(World world, MaterialRegistry materials, Vector3Int sample)
        {
            byte? id = world.GetMaterial(sample);
            return materials != null && id.HasValue && materials.Get(id.Value) is VoxelMaterial material
                && material.Kind == MaterialKind.Plant;
        }

        // Trees are terrain data (GL30): any that stands on the square is cut down whole, every solid plant sample joined to
        // it above the ground, so no crown is left floating over the table (GL338).
        private static void ClearTrees(World world, MaterialRegistry materials, Vector3 centre, float half, float ground, float iso)
        {
            if (materials == null)
            {
                return;
            }
            WorldGrid grid = world.Grid;
            float floor = ground - grid.VoxelSize;
            Vector3Int low = grid.WorldToNearestSample(new Vector3(centre.x - half, floor, centre.z - half));
            Vector3Int high = grid.WorldToNearestSample(new Vector3(centre.x + half, ground + Headroom, centre.z + half));
            var front = new System.Collections.Generic.Stack<Vector3Int>();
            var seen = new System.Collections.Generic.HashSet<Vector3Int>();
            for (int z = low.z; z <= high.z; z++)
            {
                for (int y = low.y; y <= high.y; y++)
                {
                    for (int x = low.x; x <= high.x; x++)
                    {
                        var sample = new Vector3Int(x, y, z);
                        if ((world.GetDensity(sample) ?? 0f) >= iso && IsPlant(world, materials, sample) && seen.Add(sample))
                        {
                            front.Push(sample);
                        }
                    }
                }
            }
            int floorY = low.y;
            while (front.Count > 0 && seen.Count < MaxTreeSamples)
            {
                Vector3Int sample = front.Pop();
                world.SetDensity(sample, 0f);
                foreach (Vector3Int step in Around)
                {
                    Vector3Int next = sample + step;
                    if (next.y >= floorY && !seen.Contains(next) && (world.GetDensity(next) ?? 0f) >= iso && IsPlant(world, materials, next))
                    {
                        seen.Add(next);
                        front.Push(next);
                    }
                }
            }
        }

        private static readonly Vector3Int[] Around =
        {
            Vector3Int.up, Vector3Int.down, Vector3Int.right, Vector3Int.left, new Vector3Int(0, 0, 1), new Vector3Int(0, 0, -1),
        };

        // The surface height at a point: the highest solid sample of its column that isn't part of a tree, and where
        // between it and the air above the density crosses the iso level.
        private static float GroundHeight(World world, MaterialRegistry materials, Vector3 point, float iso, int heightInChunks)
        {
            WorldGrid grid = world.Grid;
            Vector3Int column = grid.WorldToNearestSample(point);
            int top = heightInChunks * grid.ChunkSize.y - 1;
            for (int y = top; y >= 0; y--)
            {
                float? below = world.GetDensity(new Vector3Int(column.x, y, column.z));
                if (!below.HasValue || below.Value < iso || IsPlant(world, materials, new Vector3Int(column.x, y, column.z)))
                {
                    continue;
                }
                float above = world.GetDensity(new Vector3Int(column.x, y + 1, column.z)) ?? 0f;
                float fraction = Mathf.Approximately(below.Value, above) ? 0f : Mathf.Clamp01((below.Value - iso) / (below.Value - above));
                return grid.SampleToWorld(new Vector3Int(column.x, y, column.z)).y + fraction * grid.VoxelSize;
            }
            return 0f;
        }
    }
}
