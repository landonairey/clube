using System;
using System.Collections.Generic;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// The ground's heights as generated, kept as they are asked for. Writing a road or a settlement into the terrain
    /// asks for the same square metres over and over (each piece takes a grid of heights round itself, and neighbours
    /// overlap), and a generator's height is costly: the heights at whole metres are kept, and a point between them is
    /// read from the four round it.
    /// </summary>
    public sealed class KeptHeights
    {
        private readonly Dictionary<long, float> heights = new Dictionary<long, float>();
        private readonly Func<float, float, float> heightAt;
        private readonly int most;

        /// <param name="most">Heights kept before the store is emptied (200,000 is a few megabytes).</param>
        public KeptHeights(Func<float, float, float> heightAt, int most = 200000)
        {
            this.heightAt = heightAt;
            this.most = most;
        }

        public int Count => heights.Count;

        /// <summary>The ground's height at (x, z).</summary>
        public float At(float x, float z)
        {
            int ix = Mathf.FloorToInt(x), iz = Mathf.FloorToInt(z);
            float fx = x - ix, fz = z - iz;
            return Mathf.Lerp(Mathf.Lerp(Kept(ix, iz), Kept(ix + 1, iz), fx), Mathf.Lerp(Kept(ix, iz + 1), Kept(ix + 1, iz + 1), fx), fz);
        }

        public void Clear()
        {
            heights.Clear();
        }

        private float Kept(int x, int z)
        {
            long key = ((long)x << 32) | (uint)z;
            if (!heights.TryGetValue(key, out float height))
            {
                if (heights.Count > most)
                {
                    heights.Clear();
                }
                height = heightAt(x, z);
                heights[key] = height;
            }
            return height;
        }
    }
}
