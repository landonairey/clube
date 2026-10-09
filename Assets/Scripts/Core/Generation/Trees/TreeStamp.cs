using Unity.Collections;
using Unity.Mathematics;

namespace Clube.Core
{
    /// <summary>
    /// Writes tree parts into a chunk being generated (GL30), after the terrain, ores and rocks
    /// (<see cref="ChunkFillKernel"/>). Each sample takes the part it lies deepest inside, so the
    /// result doesn't depend on the order parts arrive in; then, where that part's density beats
    /// what's there, the sample becomes the part's density and material (wood or leaves). Trees add
    /// to the ground and never carve it. Burst-compatible static code.
    /// </summary>
    public static class TreeStamp
    {
        /// <summary>Stamps <paramref name="parts"/>; returns true when any sample changed.</summary>
        public static bool Run(NativeArray<TreePart> parts, in ChunkSampleGrid grid, DensityFormat format, ref ChunkFillOutput output)
        {
            if (parts.Length == 0)
            {
                return false;
            }

            int length = grid.Length;
            var deepest = new NativeArray<float>(length, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            var material = new NativeArray<byte>(length, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            for (int i = 0; i < length; i++)
            {
                deepest[i] = float.MinValue;
            }

            // Only samples within a voxel of a part's surface or inside it matter: the mesh is cut
            // on edges whose two samples straddle the surface.
            float reach = grid.VoxelSize;
            int3 last = grid.SampleCount - 1;
            bool touched = false;
            for (int p = 0; p < parts.Length; p++)
            {
                TreePart part = parts[p];
                float radius = math.max(part.FromRadius, part.ToRadius) + reach;
                float3 low = math.min(part.From, part.To) - radius;
                float3 high = math.max(part.From, part.To) + radius;
                int3 from = math.max((int3)math.floor(low / grid.VoxelSize) - grid.FirstSample, 0);
                int3 to = math.min((int3)math.ceil(high / grid.VoxelSize) - grid.FirstSample, last);
                for (int z = from.z; z <= to.z; z++)
                {
                    for (int y = from.y; y <= to.y; y++)
                    {
                        for (int x = from.x; x <= to.x; x++)
                        {
                            float depth = part.Depth(grid.Position(x, y, z));
                            int i = grid.Index(x, y, z);
                            if (depth < -reach || depth <= deepest[i])
                            {
                                continue;
                            }
                            deepest[i] = depth;
                            material[i] = part.Material;
                            touched = true;
                        }
                    }
                }
            }
            if (!touched)
            {
                return false;
            }

            bool changed = false;
            for (int i = 0; i < length; i++)
            {
                if (deepest[i] == float.MinValue)
                {
                    continue;
                }
                float density = TerrainDensity.FromDepth(deepest[i]);
                if (format == DensityFormat.Byte)
                {
                    byte value = ByteVoxelStorage.Quantize(density);
                    if (value <= output.DensityBytes[i])
                    {
                        continue;
                    }
                    output.DensityBytes[i] = value;
                }
                else
                {
                    if (density <= output.Densities[i])
                    {
                        continue;
                    }
                    output.Densities[i] = density;
                }
                output.Materials[i] = material[i];
                changed = true;
            }
            return changed;
        }
    }
}
