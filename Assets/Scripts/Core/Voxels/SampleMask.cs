using System.Collections.Generic;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// A fixed set of global samples as one flag per sample of its bounding box: membership is an array read instead of a
    /// hash look-up, for searches that ask about the same set hundreds of thousands of times (tree felling, GL820).
    /// </summary>
    public sealed class SampleMask
    {
        private readonly Vector3Int min;
        private readonly Vector3Int size;
        private readonly bool[] flags;

        public SampleMask(ICollection<Vector3Int> samples)
        {
            if (samples.Count == 0)
            {
                flags = new bool[0];
                return;
            }
            Vector3Int low = Vector3Int.one * int.MaxValue;
            Vector3Int high = Vector3Int.one * int.MinValue;
            foreach (Vector3Int sample in samples)
            {
                low = Vector3Int.Min(low, sample);
                high = Vector3Int.Max(high, sample);
            }
            min = low;
            size = high - low + Vector3Int.one;
            flags = new bool[size.x * size.y * size.z];
            foreach (Vector3Int sample in samples)
            {
                flags[Index(sample - min)] = true;
            }
        }

        /// <summary>True when the sample was in the set.</summary>
        public bool Contains(Vector3Int sample)
        {
            int x = sample.x - min.x;
            int y = sample.y - min.y;
            int z = sample.z - min.z;
            if ((uint)x >= (uint)size.x || (uint)y >= (uint)size.y || (uint)z >= (uint)size.z)
            {
                return false;
            }
            return flags[x + size.x * (y + size.y * z)];
        }

        private int Index(Vector3Int local)
        {
            return local.x + size.x * (local.y + size.y * local.z);
        }
    }
}
