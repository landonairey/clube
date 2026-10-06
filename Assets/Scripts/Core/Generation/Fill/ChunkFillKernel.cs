using Unity.Collections;
using Unity.Mathematics;

namespace Clube.Core
{
    /// <summary>
    /// Everything generation does once each sample's depth below the surface is known (K35):
    /// the density (<see cref="TerrainDensity"/>), the material layer (M10), the ore rolls (3D),
    /// and the chunk's <see cref="ChunkFillSummary"/>. Every generation path ends here
    /// (<see cref="HeightfieldFillJob"/>, <see cref="VolumeFillJob{TVolume}"/> and
    /// <see cref="DepthFillJob"/>), so they can't disagree. Burst-compatible static code.
    /// </summary>
    public static class ChunkFillKernel
    {
        /// <param name="depths">Each sample's depth below the surface, in metres; read only.</param>
        public static void Run(
            NativeArray<float> depths, in ChunkSampleGrid grid, in ChunkFillSettings settings,
            NativeArray<OreNodeData> ores, ref ChunkFillOutput output)
        {
            if (settings.Format == DensityFormat.Byte)
            {
                WriteDensityBytes(depths, output.DensityBytes, out byte low, out byte high);
                output.Summary[0] = new ChunkFillSummary { UniformDensity = low == high, Density = ByteVoxelStorage.ToDensity(low) };
            }
            else
            {
                WriteDensities(depths, output.Densities, out float low, out float high);
                output.Summary[0] = new ChunkFillSummary { UniformDensity = low == high, Density = low };
            }

            WriteLayers(depths, settings.Layers, output.Materials);
            if (ores.Length > 0)
            {
                StampOres(depths, grid, settings.Seed, ores, output.Materials);
            }

            ChunkFillSummary summary = output.Summary[0];
            summary.UniformMaterial = IsUniform(output.Materials, out summary.Material);
            output.Summary[0] = summary;
        }

        /// <summary>
        /// The summary of a chunk that lies wholly above the surface: all air, all the top
        /// layer's material, no ore (ore never replaces samples above the surface). Written
        /// without touching the arrays.
        /// </summary>
        public static ChunkFillSummary AllAir(in LayerTable layers)
        {
            return new ChunkFillSummary
            {
                UniformDensity = true,
                Density = 0f,
                UniformMaterial = true,
                Material = layers.MaterialAt(-1f),
            };
        }

        private static void WriteDensities(NativeArray<float> depths, NativeArray<float> densities, out float low, out float high)
        {
            low = float.MaxValue;
            high = float.MinValue;
            for (int i = 0; i < depths.Length; i++)
            {
                float density = TerrainDensity.FromDepth(depths[i]);
                densities[i] = density;
                low = math.min(low, density);
                high = math.max(high, density);
            }
        }

        private static void WriteDensityBytes(NativeArray<float> depths, NativeArray<byte> densities, out byte low, out byte high)
        {
            int lowest = 255;
            int highest = 0;
            for (int i = 0; i < depths.Length; i++)
            {
                byte density = ByteVoxelStorage.Quantize(TerrainDensity.FromDepth(depths[i]));
                densities[i] = density;
                lowest = math.min(lowest, density);
                highest = math.max(highest, density);
            }
            low = (byte)lowest;
            high = (byte)highest;
        }

        private static void WriteLayers(NativeArray<float> depths, in LayerTable layers, NativeArray<byte> materials)
        {
            for (int i = 0; i < depths.Length; i++)
            {
                materials[i] = layers.MaterialAt(depths[i]);
            }
        }

        // Node by node over the samples each node reaches, rather than every node for every
        // sample: a node touches only (6σ / voxel)³ samples. Each sample keeps the best ore
        // so far (O5) and its host is always the layer material written above, so the result
        // is the same as rolling every node for every sample in node order.
        private static void StampOres(
            NativeArray<float> depths, in ChunkSampleGrid grid, int seed, NativeArray<OreNodeData> ores, NativeArray<byte> materials)
        {
            int length = grid.Length;
            var bestPriority = new NativeArray<int>(length, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            var bestChance = new NativeArray<float>(length, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            var bestMaterial = new NativeArray<byte>(length, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            for (int i = 0; i < length; i++)
            {
                bestPriority[i] = int.MinValue;
            }

            bool anyOre = false;
            int3 last = grid.SampleCount - 1;
            for (int n = 0; n < ores.Length; n++)
            {
                OreNodeData node = ores[n];

                // The samples inside the node's reach box, one extra each side for rounding.
                int3 from = math.max((int3)math.floor((node.Centre - node.Reach) / grid.VoxelSize) - grid.FirstSample - 1, 0);
                int3 to = math.min((int3)math.ceil((node.Centre + node.Reach) / grid.VoxelSize) - grid.FirstSample + 1, last);
                for (int z = from.z; z <= to.z; z++)
                {
                    for (int y = from.y; y <= to.y; y++)
                    {
                        for (int x = from.x; x <= to.x; x++)
                        {
                            int i = grid.Index(x, y, z);

                            // Ore only forms underground, and only in its host materials.
                            if (depths[i] <= 0f || !node.MayReplace(materials[i]))
                            {
                                continue;
                            }

                            float chance = OreRoll.Chance(node.Centre, node.InverseSpread, node.Peak, grid.Position(x, y, z));
                            if (!OreRoll.Rolls(seed, grid.FirstSample + new int3(x, y, z), node.Material, chance)
                                || !OreRoll.Beats(node.Priority, chance, bestPriority[i], bestChance[i]))
                            {
                                continue;
                            }
                            bestPriority[i] = node.Priority;
                            bestChance[i] = chance;
                            bestMaterial[i] = node.Material;
                            anyOre = true;
                        }
                    }
                }
            }

            if (anyOre)
            {
                for (int i = 0; i < length; i++)
                {
                    if (bestPriority[i] != int.MinValue)
                    {
                        materials[i] = bestMaterial[i];
                    }
                }
            }
        }

        private static bool IsUniform(NativeArray<byte> values, out byte value)
        {
            value = values.Length > 0 ? values[0] : (byte)0;
            for (int i = 1; i < values.Length; i++)
            {
                if (values[i] != value)
                {
                    return false;
                }
            }
            return true;
        }
    }
}
