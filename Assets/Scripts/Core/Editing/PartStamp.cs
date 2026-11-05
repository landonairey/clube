using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// Writes tree parts (trunks, branches, leaf balls, logs) into the loaded world through its edit path (GL423: the
    /// town's trees and woodpile), the same shapes and rules the fill job's <see cref="TreeStamp"/> uses on a generating
    /// chunk: the deepest part sets each sample's density, and a sample inside wood is wood. Never lowers ground.
    /// </summary>
    public static class PartStamp
    {
        public static void Write(World world, IReadOnlyList<TreePart> parts, float iso)
        {
            WorldGrid grid = world.Grid;
            float reach = grid.VoxelSize;
            var deepest = new Dictionary<Vector3Int, (float Depth, byte Material)>();
            var wood = new Dictionary<Vector3Int, (float Depth, byte Material)>();
            foreach (TreePart part in parts)
            {
                float radius = math.max(part.FromRadius, part.ToRadius) + reach;
                float3 low = math.min(part.From, part.To) - radius;
                float3 high = math.max(part.From, part.To) + radius;
                Vector3Int from = grid.WorldToNearestSample(low);
                Vector3Int to = grid.WorldToNearestSample(high);
                for (int z = from.z; z <= to.z; z++)
                {
                    for (int y = from.y; y <= to.y; y++)
                    {
                        for (int x = from.x; x <= to.x; x++)
                        {
                            var sample = new Vector3Int(x, y, z);
                            float depth = part.Depth(grid.SampleToWorld(sample));
                            if (depth >= 0f && part.IsSegment && (!wood.TryGetValue(sample, out var w) || depth > w.Depth))
                            {
                                wood[sample] = (depth, part.Material);
                            }
                            if (depth < -reach || (deepest.TryGetValue(sample, out var d) && depth <= d.Depth))
                            {
                                continue;
                            }
                            deepest[sample] = (depth, part.Material);
                        }
                    }
                }
            }
            foreach (KeyValuePair<Vector3Int, (float Depth, byte Material)> pair in deepest)
            {
                float density = TerrainDensity.FromDepth(pair.Value.Depth);
                if (!world.TryGetOwner(pair.Key, out Chunk chunk, out Vector3Int local) || density <= chunk.GetDensity(local) + 1.5f / 255f)
                {
                    continue;
                }
                world.SetDensity(pair.Key, density);
                if (density >= iso)
                {
                    world.SetMaterial(pair.Key, wood.TryGetValue(pair.Key, out var w) ? w.Material : pair.Value.Material);
                }
            }
        }
    }
}
