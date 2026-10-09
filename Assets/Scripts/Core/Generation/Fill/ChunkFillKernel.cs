using Unity.Collections;
using Unity.Mathematics;

namespace Clube.Core
{
    /// <summary>
    /// Everything generation does once each sample's depth below the surface is known (K35):
    /// the density (<see cref="TerrainDensity"/>), the material layer (M10, bare rock on steep
    /// columns, GL21), the ore rolls (3D), the surface rocks (GL22), the trees (GL30), and the chunk's
    /// <see cref="ChunkFillSummary"/>. Every generation path ends here
    /// (<see cref="HeightfieldFillJob"/>, <see cref="VolumeFillJob{TVolume}"/> and
    /// <see cref="DepthFillJob"/>), so they can't disagree. Burst-compatible static code.
    /// </summary>
    public static class ChunkFillKernel
    {
        /// <param name="depths">Each sample's depth below the surface, in metres; read only.</param>
        /// <param name="steepColumns">One per column (x fastest, then z): non-zero where the surface is steep
        /// enough to be bare rock (GL21). Empty when the generator has no columns; nothing is steep then.</param>
        /// <param name="trees">Tree parts reaching the chunk (<see cref="TreeField"/>); empty for none.</param>
        public static void Run(
            NativeArray<float> depths, NativeArray<byte> steepColumns, in ChunkSampleGrid grid, in ChunkFillSettings settings,
            NativeArray<OreNodeData> ores, NativeArray<TreePart> trees, ref ChunkFillOutput output)
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

            WriteLayers(depths, steepColumns, grid, settings.Layers, output.Materials);
            if (ores.Length > 0)
            {
                StampOres(depths, grid, settings.Seed, ores, output.Materials);
            }
            if (settings.Layers.RockChance > 0f && StampRocks(depths, steepColumns, grid, settings, ref output))
            {
                ChunkFillSummary withRocks = output.Summary[0];
                withRocks.UniformDensity = false;
                output.Summary[0] = withRocks;
            }
            if (trees.Length > 0 && TreeStamp.Run(trees, grid, settings.Format, ref output))
            {
                ChunkFillSummary withTrees = output.Summary[0];
                withTrees.UniformDensity = false;
                output.Summary[0] = withTrees;
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

        private static void WriteLayers(
            NativeArray<float> depths, NativeArray<byte> steepColumns, in ChunkSampleGrid grid, in LayerTable layers, NativeArray<byte> materials)
        {
            if (steepColumns.Length == 0)
            {
                for (int i = 0; i < depths.Length; i++)
                {
                    materials[i] = layers.MaterialAt(depths[i]);
                }
                return;
            }

            int3 count = grid.SampleCount;
            for (int z = 0; z < count.z; z++)
            {
                for (int y = 0; y < count.y; y++)
                {
                    for (int x = 0; x < count.x; x++)
                    {
                        int i = grid.Index(x, y, z);
                        materials[i] = layers.MaterialAt(depths[i], steepColumns[x + count.x * z] != 0);
                    }
                }
            }
        }

        // Small rocks on the top layer (GL22): a column starts a cluster by a hash of its global
        // position and the seed, and a cluster covers 1-3 columns (itself, then +x, then +z).
        // In each covered column the first sample above the surface (depth in [-voxel, 0))
        // becomes solid rock, a small bump in the mesh. Everything depends only on global
        // positions, so chunks sharing a border agree. Returns true when it placed any.
        private static bool StampRocks(
            NativeArray<float> depths, NativeArray<byte> steepColumns, in ChunkSampleGrid grid, in ChunkFillSettings settings, ref ChunkFillOutput output)
        {
            LayerTable layers = settings.Layers;
            bool bytes = settings.Format == DensityFormat.Byte;
            bool any = false;
            int3 count = grid.SampleCount;
            for (int z = 0; z < count.z; z++)
            {
                for (int x = 0; x < count.x; x++)
                {
                    if (steepColumns.Length > 0 && steepColumns[x + count.x * z] != 0)
                    {
                        continue;
                    }
                    int2 column = grid.FirstSample.xz + new int2(x, z);
                    if (!IsRockColumn(settings.Seed, column, layers.RockChance))
                    {
                        continue;
                    }
                    for (int y = 0; y < count.y; y++)
                    {
                        int i = grid.Index(x, y, z);
                        float depth = depths[i];
                        if (depth >= 0f || depth < -grid.VoxelSize)
                        {
                            continue;
                        }
                        if (bytes)
                        {
                            output.DensityBytes[i] = 255;
                        }
                        else
                        {
                            output.Densities[i] = 1f;
                        }
                        output.Materials[i] = layers.Rock;
                        any = true;
                    }
                }
            }
            return any;
        }

        // Salts keep the rock hashes apart from the ore rolls'.
        private const int RockSalt = 0x524F434B;

        /// <summary>True when a global column is covered by a surface rock cluster (GL22).</summary>
        public static bool IsRockColumn(int seed, int2 column, float chance)
        {
            return ClusterSize(seed, column, chance) >= 1
                || ClusterSize(seed, column - new int2(1, 0), chance) >= 2
                || ClusterSize(seed, column - new int2(0, 1), chance) >= 3;
        }

        // How many columns the cluster starting at a column covers: 0 when none starts there.
        private static int ClusterSize(int seed, int2 column, float chance)
        {
            if (VoxelHash.Uniform(seed, column.x, 0, column.y, RockSalt) >= chance)
            {
                return 0;
            }
            return 1 + (int)(VoxelHash.Hash(seed, column.x, 0, column.y, RockSalt + 1) % 3u);
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
