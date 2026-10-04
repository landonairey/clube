using System.Collections.Generic;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// The world's coordinate system (M1): world positions, global samples, chunk
    /// coordinates and chunk-local samples, and the conversions between them.
    /// Chunks are cubes on a 3D grid, so the world can stack them vertically.
    /// </summary>
    /// <remarks>
    /// All positions are relative to the world's origin, where global sample (0,0,0)
    /// sits. Chunk <c>c</c> holds global samples <c>c × size</c> to <c>(c + 1) × size</c>
    /// inclusive: neighbouring chunks share their border samples, each keeping its own
    /// copy (M2). A sample on a border therefore belongs to 2, 4 or 8 chunks.
    /// Coordinates are floored, never truncated, so negative positions work.
    /// </remarks>
    public readonly struct WorldGrid
    {
        public WorldGrid(Vector3Int chunkSize, float voxelSize)
        {
            ChunkSize = chunkSize;
            VoxelSize = voxelSize;
        }

        /// <summary>Voxels per chunk along each axis.</summary>
        public Vector3Int ChunkSize { get; }

        /// <summary>Edge length of one voxel in world units.</summary>
        public float VoxelSize { get; }

        /// <summary>World size of one chunk.</summary>
        public Vector3 ChunkWorldSize => (Vector3)ChunkSize * VoxelSize;

        /// <summary>World position of a global sample.</summary>
        public Vector3 SampleToWorld(Vector3Int sample)
        {
            return (Vector3)sample * VoxelSize;
        }

        /// <summary>The global sample nearest a world position.</summary>
        public Vector3Int WorldToNearestSample(Vector3 position)
        {
            return Vector3Int.RoundToInt(position / VoxelSize);
        }

        /// <summary>The voxel containing a world position (its lowest corner's global sample).</summary>
        public Vector3Int WorldToVoxel(Vector3 position)
        {
            return Vector3Int.FloorToInt(position / VoxelSize);
        }

        /// <summary>The chunk containing a world position.</summary>
        public Vector3Int WorldToChunk(Vector3 position)
        {
            return VoxelToChunk(WorldToVoxel(position));
        }

        /// <summary>The chunk that owns a voxel (by its lowest corner).</summary>
        public Vector3Int VoxelToChunk(Vector3Int voxel)
        {
            return new Vector3Int(
                FloorDiv(voxel.x, ChunkSize.x),
                FloorDiv(voxel.y, ChunkSize.y),
                FloorDiv(voxel.z, ChunkSize.z));
        }

        /// <summary>World position of a chunk's sample (0,0,0).</summary>
        public Vector3 ChunkOrigin(Vector3Int chunk)
        {
            return SampleToWorld(ChunkFirstSample(chunk));
        }

        /// <summary>The global sample at a chunk's local sample (0,0,0).</summary>
        public Vector3Int ChunkFirstSample(Vector3Int chunk)
        {
            return Vector3Int.Scale(chunk, ChunkSize);
        }

        /// <summary>A global sample as a sample of the given chunk (it may lie outside that chunk).</summary>
        public Vector3Int GlobalToLocal(Vector3Int sample, Vector3Int chunk)
        {
            return sample - ChunkFirstSample(chunk);
        }

        /// <summary>True when a chunk holds a copy of the global sample.</summary>
        public bool ChunkContainsSample(Vector3Int chunk, Vector3Int sample)
        {
            Vector3Int local = GlobalToLocal(sample, chunk);
            return local.x >= 0 && local.y >= 0 && local.z >= 0
                && local.x <= ChunkSize.x && local.y <= ChunkSize.y && local.z <= ChunkSize.z;
        }

        /// <summary>
        /// Every chunk holding a copy of a global sample: one inside a chunk, 2 on a
        /// face, 4 on an edge, 8 on a corner (M2).
        /// </summary>
        public void ChunksContainingSample(Vector3Int sample, List<Vector3Int> result)
        {
            result.Clear();
            Vector3Int owner = VoxelToChunk(sample);
            // On a border, the sample is also the last sample of the chunk below on that axis.
            bool onX = Mod(sample.x, ChunkSize.x) == 0;
            bool onY = Mod(sample.y, ChunkSize.y) == 0;
            bool onZ = Mod(sample.z, ChunkSize.z) == 0;
            for (int dz = onZ ? -1 : 0; dz <= 0; dz++)
            {
                for (int dy = onY ? -1 : 0; dy <= 0; dy++)
                {
                    for (int dx = onX ? -1 : 0; dx <= 0; dx++)
                    {
                        result.Add(owner + new Vector3Int(dx, dy, dz));
                    }
                }
            }
        }

        /// <summary>The chunks whose sample range overlaps a box of global samples, inclusive.</summary>
        public void ChunksOverlapping(Vector3Int minSample, Vector3Int maxSample, List<Vector3Int> result)
        {
            result.Clear();
            // A chunk overlaps if its range [c·n, (c+1)·n] meets [min, max].
            Vector3Int from = new Vector3Int(
                FloorDiv(minSample.x - 1, ChunkSize.x),
                FloorDiv(minSample.y - 1, ChunkSize.y),
                FloorDiv(minSample.z - 1, ChunkSize.z));
            Vector3Int to = VoxelToChunk(maxSample);
            for (int z = from.z; z <= to.z; z++)
            {
                for (int y = from.y; y <= to.y; y++)
                {
                    for (int x = from.x; x <= to.x; x++)
                    {
                        var chunk = new Vector3Int(x, y, z);
                        Vector3Int first = ChunkFirstSample(chunk);
                        Vector3Int last = first + ChunkSize;
                        if (last.x >= minSample.x && last.y >= minSample.y && last.z >= minSample.z
                            && first.x <= maxSample.x && first.y <= maxSample.y && first.z <= maxSample.z)
                        {
                            result.Add(chunk);
                        }
                    }
                }
            }
        }

        /// <summary>Integer division rounding towards negative infinity.</summary>
        public static int FloorDiv(int value, int divisor)
        {
            int quotient = value / divisor;
            return (value % divisor != 0 && (value < 0) != (divisor < 0)) ? quotient - 1 : quotient;
        }

        private static int Mod(int value, int divisor)
        {
            int remainder = value % divisor;
            return remainder < 0 ? remainder + divisor : remainder;
        }
    }
}
