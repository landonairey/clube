using System;
using System.IO;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// Run-length encoded densities (K24): each line of samples along one axis (the run axis)
    /// is stored as runs of equal values. Terrain is mostly solid below and air above, so runs
    /// along Y (up a column) are long; X and Z cross more surface. Neighbouring runs never
    /// hold the same value: an edit splits a run and merges it with equal neighbours.
    /// </summary>
    /// <remarks>
    /// A run is its end position along the line (exclusive) and its value; a run starts where
    /// the previous one ends. Lookups binary-search the ends. Values are compared exactly, so
    /// only identical densities share a run (smooth surfaces vary, solid and air don't).
    /// </remarks>
    public sealed class RunLengthVoxelStorage : IVoxelStorage, IReadableStorage
    {
        // Array header and the line struct's fields, for MemoryBytes.
        private const int ArrayOverhead = 24;
        private const int LineStructBytes = 24;

        private readonly int axis;
        private readonly int lineLength;
        private readonly Line[] lines;

        /// <param name="sampleCount">Samples per axis; at least 2 (one voxel) on each.</param>
        /// <param name="runAxis">The axis runs go along: 0 = X, 1 = Y, 2 = Z.</param>
        public RunLengthVoxelStorage(Vector3Int sampleCount, int runAxis)
        {
            SampleGrid.Validate(sampleCount);
            if (runAxis < 0 || runAxis > 2)
            {
                throw new ArgumentOutOfRangeException(nameof(runAxis), runAxis, "0 (X), 1 (Y) or 2 (Z).");
            }
            if (sampleCount[runAxis] > ushort.MaxValue)
            {
                throw new ArgumentOutOfRangeException(nameof(sampleCount), sampleCount, "Lines are limited to 65,535 samples.");
            }

            SampleCount = sampleCount;
            axis = runAxis;
            lineLength = sampleCount[runAxis];
            lines = new Line[sampleCount.x * sampleCount.y * sampleCount.z / lineLength];
            for (int i = 0; i < lines.Length; i++)
            {
                lines[i] = Line.Uniform(lineLength, 0f);
            }
        }

        public VoxelStorageType Type => (VoxelStorageType)((int)VoxelStorageType.RunLengthX + axis);

        public Vector3Int SampleCount { get; }

        /// <summary>The axis runs go along: 0 = X, 1 = Y, 2 = Z.</summary>
        public int RunAxis => axis;

        /// <summary>Total runs across every line (K26: fewer is better compressed).</summary>
        public int RunCount
        {
            get
            {
                int total = 0;
                foreach (Line line in lines)
                {
                    total += line.Count;
                }
                return total;
            }
        }

        public long MemoryBytes
        {
            get
            {
                long bytes = ArrayOverhead + (long)lines.Length * LineStructBytes;
                foreach (Line line in lines)
                {
                    bytes += 2 * ArrayOverhead + (long)line.Ends.Length * (sizeof(ushort) + sizeof(float));
                }
                return bytes;
            }
        }

        public float GetDensity(int x, int y, int z)
        {
            SampleGrid.CheckSample(SampleCount, x, y, z);
            ref Line line = ref lines[LineIndex(x, y, z)];
            return line.Values[line.Find(Position(x, y, z))];
        }

        public void SetDensity(int x, int y, int z, float density)
        {
            SampleGrid.CheckSample(SampleCount, x, y, z);
            lines[LineIndex(x, y, z)].Set(Position(x, y, z), density);
        }

        public void ReadLayer(int z, Span<float> layer)
        {
            SampleGrid.CheckLayer(SampleCount, z);
            int sx = SampleCount.x;
            int sy = SampleCount.y;
            switch (axis)
            {
                case 0:
                    // Each row of the layer is one line: decode its runs in place.
                    for (int y = 0; y < sy; y++)
                    {
                        lines[LineIndex(0, y, z)].Decode(layer.Slice(y * sx, sx), 1);
                    }
                    break;
                case 1:
                    // Each column of the layer is one line, strided by the row length.
                    for (int x = 0; x < sx; x++)
                    {
                        lines[LineIndex(x, 0, z)].Decode(layer.Slice(x), sx);
                    }
                    break;
                default:
                    // Lines cross the layer: one lookup per sample.
                    for (int y = 0; y < sy; y++)
                    {
                        for (int x = 0; x < sx; x++)
                        {
                            ref Line line = ref lines[LineIndex(x, y, 0)];
                            layer[x + sx * y] = line.Values[line.Find(z)];
                        }
                    }
                    break;
            }
        }

        /// <summary>Calls <paramref name="visit"/> with each run's first sample, length along the run axis, and value (K27).</summary>
        public void VisitRuns(Action<Vector3Int, int, float> visit)
        {
            for (int i = 0; i < lines.Length; i++)
            {
                Vector3Int lineStart = LineStart(i);
                Line line = lines[i];
                int start = 0;
                for (int run = 0; run < line.Count; run++)
                {
                    Vector3Int first = lineStart;
                    first[axis] = start;
                    visit(first, line.Ends[run] - start, line.Values[run]);
                    start = line.Ends[run];
                }
            }
        }

        public void WriteData(BinaryWriter writer)
        {
            foreach (Line line in lines)
            {
                writer.Write((ushort)line.Count);
                int start = 0;
                for (int run = 0; run < line.Count; run++)
                {
                    writer.Write((ushort)(line.Ends[run] - start));
                    writer.Write(line.Values[run]);
                    start = line.Ends[run];
                }
            }
        }

        void IReadableStorage.ReadData(BinaryReader reader)
        {
            for (int i = 0; i < lines.Length; i++)
            {
                int count = reader.ReadUInt16();
                var line = new Line { Ends = new ushort[count], Values = new float[count], Count = count };
                int end = 0;
                for (int run = 0; run < count; run++)
                {
                    end += reader.ReadUInt16();
                    line.Ends[run] = (ushort)end;
                    line.Values[run] = reader.ReadSingle();
                }
                if (end != lineLength)
                {
                    throw new InvalidDataException($"A line's runs cover {end} samples, not {lineLength}.");
                }
                lines[i] = line;
            }
        }

        private int Position(int x, int y, int z)
        {
            return axis == 0 ? x : axis == 1 ? y : z;
        }

        // Lines are numbered by the other two axes, lowest axis fastest.
        private int LineIndex(int x, int y, int z)
        {
            switch (axis)
            {
                case 0: return y + SampleCount.y * z;
                case 1: return x + SampleCount.x * z;
                default: return x + SampleCount.x * y;
            }
        }

        private Vector3Int LineStart(int index)
        {
            switch (axis)
            {
                case 0: return new Vector3Int(0, index % SampleCount.y, index / SampleCount.y);
                case 1: return new Vector3Int(index % SampleCount.x, 0, index / SampleCount.x);
                default: return new Vector3Int(index % SampleCount.x, index / SampleCount.x, 0);
            }
        }

        /// <summary>One line's runs: <c>Ends[i]</c> is where run i stops (exclusive), <c>Values[i]</c> its density.</summary>
        private struct Line
        {
            public ushort[] Ends;
            public float[] Values;
            public int Count;

            public static Line Uniform(int length, float value)
            {
                return new Line { Ends = new[] { (ushort)length }, Values = new[] { value }, Count = 1 };
            }

            /// <summary>The run holding <paramref name="position"/>: the first whose end is past it.</summary>
            public int Find(int position)
            {
                int low = 0;
                int high = Count - 1;
                while (low < high)
                {
                    int middle = (low + high) >> 1;
                    if (Ends[middle] > position)
                    {
                        high = middle;
                    }
                    else
                    {
                        low = middle + 1;
                    }
                }
                return low;
            }

            /// <summary>Writes every sample of the line to <paramref name="target"/>, <paramref name="stride"/> apart.</summary>
            public void Decode(Span<float> target, int stride)
            {
                int position = 0;
                for (int run = 0; run < Count; run++)
                {
                    float value = Values[run];
                    for (; position < Ends[run]; position++)
                    {
                        target[position * stride] = value;
                    }
                }
            }

            public void Set(int position, float value)
            {
                int i = Find(position);
                float old = Values[i];
                if (old == value)
                {
                    return;
                }

                int start = i == 0 ? 0 : Ends[i - 1];
                int end = Ends[i];
                bool joinsLeft = position == start && i > 0 && Values[i - 1] == value;
                bool joinsRight = position == end - 1 && i < Count - 1 && Values[i + 1] == value;

                if (end - start == 1)
                {
                    // A one-sample run changes value, and may join its neighbours.
                    if (joinsLeft && joinsRight)
                    {
                        Ends[i - 1] = Ends[i + 1];
                        RemoveAt(i);
                        RemoveAt(i);
                    }
                    else if (joinsLeft)
                    {
                        Ends[i - 1] = (ushort)end;
                        RemoveAt(i);
                    }
                    else if (joinsRight)
                    {
                        RemoveAt(i);
                    }
                    else
                    {
                        Values[i] = value;
                    }
                }
                else if (position == start)
                {
                    // The run's first sample: the left neighbour grows, or a new run goes in front.
                    if (joinsLeft)
                    {
                        Ends[i - 1] = (ushort)(position + 1);
                    }
                    else
                    {
                        Insert(i, (ushort)(position + 1), value);
                    }
                }
                else if (position == end - 1)
                {
                    // The run's last sample: it ends one sooner, and the right neighbour grows or a run is added.
                    Ends[i] = (ushort)position;
                    if (!joinsRight)
                    {
                        Insert(i + 1, (ushort)end, value);
                    }
                }
                else
                {
                    // Inside the run: split it around the new one-sample run.
                    Ends[i] = (ushort)position;
                    Insert(i + 1, (ushort)(position + 1), value);
                    Insert(i + 2, (ushort)end, old);
                }
            }

            private void Insert(int index, ushort end, float value)
            {
                if (Count == Ends.Length)
                {
                    int capacity = Math.Max(4, Ends.Length * 2);
                    Array.Resize(ref Ends, capacity);
                    Array.Resize(ref Values, capacity);
                }
                Array.Copy(Ends, index, Ends, index + 1, Count - index);
                Array.Copy(Values, index, Values, index + 1, Count - index);
                Ends[index] = end;
                Values[index] = value;
                Count++;
            }

            private void RemoveAt(int index)
            {
                Count--;
                Array.Copy(Ends, index + 1, Ends, index, Count - index);
                Array.Copy(Values, index + 1, Values, index, Count - index);
            }
        }
    }
}
