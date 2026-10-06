using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// A sparse octree of densities (K25). The sample grid is padded to a power-of-two cube;
    /// each node is a uniform leaf (one value for its whole cube), eight children, or, at the
    /// maximum depth, a dense brick of samples. Writing into a uniform leaf divides it on the
    /// way down; afterwards, eight uniform children with the same value collapse back into
    /// their parent, and a brick that became uniform turns back into a leaf. So solid rock and
    /// open air cost one node each, and detail is only stored near the surface.
    /// </summary>
    /// <remarks>
    /// <para><see cref="MaxDepth"/> is how many times the root may divide: deeper means smaller
    /// bricks (finer collapse) but more nodes. Samples in the padding stay 0, which can stop a
    /// region at the grid's edge from collapsing.</para>
    /// <para>Nodes live in parallel arrays, allocated eight siblings at a time and reused
    /// through free lists, so edits don't create garbage once the octree has grown.</para>
    /// </remarks>
    public sealed class OctreeVoxelStorage : IVoxelStorage, IReadableStorage
    {
        private const int None = -1;
        private const int ArrayOverhead = 24;

        // Serialized node tags, written in depth-first order.
        private const byte UniformTag = 0;
        private const byte BrickTag = 1;
        private const byte ParentTag = 2;

        private readonly int rootSize;
        private readonly int brickSize;
        private readonly int brickVolume;
        private readonly int[] path;

        // Node i: its first child (siblings are consecutive), its value when uniform, its brick.
        private int[] firstChild = new int[64];
        private float[] values = new float[64];
        private int[] bricks = new int[64];
        private int nodeCount;
        private readonly Stack<int> freeGroups = new Stack<int>();

        private float[] brickData = Array.Empty<float>();
        private int brickSlots;
        private readonly Stack<int> freeBricks = new Stack<int>();

        /// <param name="sampleCount">Samples per axis; at least 2 (one voxel) on each.</param>
        /// <param name="maxDepth">How many times the root may divide; clamped to 1 and to the depth of single samples.</param>
        public OctreeVoxelStorage(Vector3Int sampleCount, int maxDepth)
        {
            SampleGrid.Validate(sampleCount);
            SampleCount = sampleCount;

            int largest = Mathf.Max(sampleCount.x, Mathf.Max(sampleCount.y, sampleCount.z));
            rootSize = Mathf.NextPowerOfTwo(largest);
            DeepestDepth = (int)Math.Round(Math.Log(rootSize, 2));
            MaxDepth = Mathf.Clamp(maxDepth, 1, DeepestDepth);
            brickSize = rootSize >> MaxDepth;
            brickVolume = brickSize * brickSize * brickSize;
            path = new int[MaxDepth + 1];

            nodeCount = 1;
            firstChild[0] = None;
            values[0] = 0f;
            bricks[0] = None;
            NodeCount = 1;
        }

        public VoxelStorageType Type => VoxelStorageType.Octree;

        public Vector3Int SampleCount { get; }

        /// <summary>How many times the root may divide (K25).</summary>
        public int MaxDepth { get; }

        /// <summary>The deepest <see cref="MaxDepth"/> this size allows: where nodes are single samples.</summary>
        public int DeepestDepth { get; }

        /// <summary>Edge length of the padded cube the root covers, in samples.</summary>
        public int RootSize => rootSize;

        /// <summary>Edge length of a brick (a leaf at the maximum depth), in samples.</summary>
        public int BrickSize => brickSize;

        /// <summary>Nodes in use (K26).</summary>
        public int NodeCount { get; private set; }

        /// <summary>Bricks in use (K26).</summary>
        public int BrickCount { get; private set; }

        public long MemoryBytes =>
            3L * ArrayOverhead + (long)firstChild.Length * (sizeof(int) + sizeof(float) + sizeof(int))
            + ArrayOverhead + (long)brickData.Length * sizeof(float);

        public float GetDensity(int x, int y, int z)
        {
            SampleGrid.CheckSample(SampleCount, x, y, z);
            int node = 0;
            int size = rootSize;
            int ox = 0, oy = 0, oz = 0;
            while (firstChild[node] != None)
            {
                size >>= 1;
                node = firstChild[node] + Octant(x, y, z, size, ref ox, ref oy, ref oz);
            }
            return bricks[node] == None ? values[node] : brickData[BrickIndex(bricks[node], x - ox, y - oy, z - oz)];
        }

        public void SetDensity(int x, int y, int z, float density)
        {
            SampleGrid.CheckSample(SampleCount, x, y, z);
            int node = 0;
            int depth = 0;
            int size = rootSize;
            int ox = 0, oy = 0, oz = 0;
            path[0] = 0;

            while (true)
            {
                if (firstChild[node] == None)
                {
                    if (bricks[node] != None)
                    {
                        brickData[BrickIndex(bricks[node], x - ox, y - oy, z - oz)] = density;
                        CollapseBrickIfUniform(node);
                        break;
                    }
                    if (values[node] == density)
                    {
                        return;
                    }
                    if (depth == MaxDepth)
                    {
                        if (brickSize == 1)
                        {
                            values[node] = density;
                        }
                        else
                        {
                            MakeBrick(node);
                            brickData[BrickIndex(bricks[node], x - ox, y - oy, z - oz)] = density;
                        }
                        break;
                    }

                    // Divide: eight children with the leaf's value, then go on down. (Allocate first:
                    // it can grow the arrays, and an indexed assignment would hold on to the old one.)
                    int children = AllocateGroup(values[node]);
                    firstChild[node] = children;
                }

                size >>= 1;
                node = firstChild[node] + Octant(x, y, z, size, ref ox, ref oy, ref oz);
                depth++;
                path[depth] = node;
            }

            // Back up the path: a parent whose eight children are uniform and equal collapses.
            for (int d = depth - 1; d >= 0; d--)
            {
                if (!TryCollapse(path[d]))
                {
                    break;
                }
            }
        }

        public void ReadLayer(int z, Span<float> layer)
        {
            SampleGrid.CheckLayer(SampleCount, z);
            FillLayer(0, 0, 0, 0, rootSize, z, layer);
        }

        /// <remarks>One sample at a time; the octree is a comparison scheme (2G), not the streamed one (M12).</remarks>
        public void WriteLayer(int z, ReadOnlySpan<float> layer)
        {
            SampleGrid.CheckLayer(SampleCount, z);
            for (int y = 0; y < SampleCount.y; y++)
            {
                for (int x = 0; x < SampleCount.x; x++)
                {
                    SetDensity(x, y, z, layer[x + SampleCount.x * y]);
                }
            }
        }

        /// <summary>The kinds of node <see cref="VisitNodes"/> reports.</summary>
        public enum NodeKind
        {
            Uniform,
            Brick,
            Parent,
        }

        /// <summary>Calls <paramref name="visit"/> with every node's corner sample, edge length, depth and kind (K27).</summary>
        public void VisitNodes(Action<Vector3Int, int, int, NodeKind> visit)
        {
            Visit(0, Vector3Int.zero, rootSize, 0, visit);
        }

        public void WriteData(BinaryWriter writer)
        {
            WriteNode(0, writer);
        }

        void IReadableStorage.ReadData(BinaryReader reader)
        {
            ReadNode(0, 0, reader);
        }

        // Picks the child octant holding (x, y, z) at half size, moving the origin into it.
        private static int Octant(int x, int y, int z, int half, ref int ox, ref int oy, ref int oz)
        {
            int octant = 0;
            if (x >= ox + half)
            {
                octant |= 1;
                ox += half;
            }
            if (y >= oy + half)
            {
                octant |= 2;
                oy += half;
            }
            if (z >= oz + half)
            {
                octant |= 4;
                oz += half;
            }
            return octant;
        }

        private int BrickIndex(int brick, int lx, int ly, int lz)
        {
            return brick * brickVolume + lx + brickSize * (ly + brickSize * lz);
        }

        private bool TryCollapse(int node)
        {
            int first = firstChild[node];
            float value = values[first];
            for (int i = 0; i < 8; i++)
            {
                int child = first + i;
                if (firstChild[child] != None || bricks[child] != None || values[child] != value)
                {
                    return false;
                }
            }

            firstChild[node] = None;
            values[node] = value;
            freeGroups.Push(first);
            NodeCount -= 8;
            return true;
        }

        private void MakeBrick(int node)
        {
            int brick;
            if (freeBricks.Count > 0)
            {
                brick = freeBricks.Pop();
            }
            else
            {
                brick = brickSlots++;
                if (brickSlots * brickVolume > brickData.Length)
                {
                    Array.Resize(ref brickData, Math.Max(brickVolume * 4, brickData.Length * 2));
                }
            }

            Array.Fill(brickData, values[node], brick * brickVolume, brickVolume);
            bricks[node] = brick;
            BrickCount++;
        }

        private void CollapseBrickIfUniform(int node)
        {
            int start = bricks[node] * brickVolume;
            float value = brickData[start];
            for (int i = 1; i < brickVolume; i++)
            {
                if (brickData[start + i] != value)
                {
                    return;
                }
            }

            freeBricks.Push(bricks[node]);
            bricks[node] = None;
            values[node] = value;
            BrickCount--;
        }

        private int AllocateGroup(float value)
        {
            int first;
            if (freeGroups.Count > 0)
            {
                first = freeGroups.Pop();
            }
            else
            {
                first = nodeCount;
                nodeCount += 8;
                if (nodeCount > firstChild.Length)
                {
                    int capacity = firstChild.Length * 2;
                    Array.Resize(ref firstChild, capacity);
                    Array.Resize(ref values, capacity);
                    Array.Resize(ref bricks, capacity);
                }
            }

            for (int i = first; i < first + 8; i++)
            {
                firstChild[i] = None;
                values[i] = value;
                bricks[i] = None;
            }
            NodeCount += 8;
            return first;
        }

        // Fills the part of the layer this node covers: whole squares from uniform leaves,
        // a slice from bricks, and only the four children on z's side from parents.
        private void FillLayer(int node, int ox, int oy, int oz, int size, int z, Span<float> layer)
        {
            int sx = SampleCount.x;
            int sy = SampleCount.y;
            if (ox >= sx || oy >= sy)
            {
                return;
            }

            if (firstChild[node] != None)
            {
                int half = size >> 1;
                int zOctant = z >= oz + half ? 4 : 0;
                int childZ = zOctant != 0 ? oz + half : oz;
                for (int xy = 0; xy < 4; xy++)
                {
                    FillLayer(
                        firstChild[node] + (xy | zOctant),
                        ox + ((xy & 1) != 0 ? half : 0), oy + ((xy & 2) != 0 ? half : 0), childZ, half, z, layer);
                }
                return;
            }

            int xEnd = Math.Min(ox + size, sx);
            int yEnd = Math.Min(oy + size, sy);
            if (bricks[node] == None)
            {
                float value = values[node];
                for (int y = oy; y < yEnd; y++)
                {
                    layer.Slice(ox + sx * y, xEnd - ox).Fill(value);
                }
                return;
            }

            int brick = bricks[node];
            for (int y = oy; y < yEnd; y++)
            {
                for (int x = ox; x < xEnd; x++)
                {
                    layer[x + sx * y] = brickData[BrickIndex(brick, x - ox, y - oy, z - oz)];
                }
            }
        }

        private void Visit(int node, Vector3Int origin, int size, int depth, Action<Vector3Int, int, int, NodeKind> visit)
        {
            if (firstChild[node] == None)
            {
                visit(origin, size, depth, bricks[node] == None ? NodeKind.Uniform : NodeKind.Brick);
                return;
            }

            visit(origin, size, depth, NodeKind.Parent);
            int half = size >> 1;
            for (int octant = 0; octant < 8; octant++)
            {
                var childOrigin = new Vector3Int(
                    origin.x + ((octant & 1) != 0 ? half : 0),
                    origin.y + ((octant & 2) != 0 ? half : 0),
                    origin.z + ((octant & 4) != 0 ? half : 0));
                Visit(firstChild[node] + octant, childOrigin, half, depth + 1, visit);
            }
        }

        private void WriteNode(int node, BinaryWriter writer)
        {
            if (firstChild[node] != None)
            {
                writer.Write(ParentTag);
                for (int octant = 0; octant < 8; octant++)
                {
                    WriteNode(firstChild[node] + octant, writer);
                }
            }
            else if (bricks[node] != None)
            {
                writer.Write(BrickTag);
                int start = bricks[node] * brickVolume;
                for (int i = 0; i < brickVolume; i++)
                {
                    writer.Write(brickData[start + i]);
                }
            }
            else
            {
                writer.Write(UniformTag);
                writer.Write(values[node]);
            }
        }

        private void ReadNode(int node, int depth, BinaryReader reader)
        {
            byte tag = reader.ReadByte();
            switch (tag)
            {
                case UniformTag:
                    values[node] = reader.ReadSingle();
                    break;
                case BrickTag:
                    if (depth != MaxDepth || brickSize == 1)
                    {
                        throw new InvalidDataException($"A brick at depth {depth}, but bricks only sit at depth {MaxDepth}.");
                    }
                    MakeBrick(node);
                    int start = bricks[node] * brickVolume;
                    for (int i = 0; i < brickVolume; i++)
                    {
                        brickData[start + i] = reader.ReadSingle();
                    }
                    break;
                case ParentTag:
                    if (depth >= MaxDepth)
                    {
                        throw new InvalidDataException($"A parent at depth {depth}, deeper than the maximum {MaxDepth}.");
                    }
                    int first = AllocateGroup(0f);
                    firstChild[node] = first;
                    for (int octant = 0; octant < 8; octant++)
                    {
                        ReadNode(first + octant, depth + 1, reader);
                    }
                    break;
                default:
                    throw new InvalidDataException($"Unknown octree node tag {tag}.");
            }
        }
    }
}
