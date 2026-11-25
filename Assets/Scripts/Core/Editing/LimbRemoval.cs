using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// Takes a felled limb out of the world (<see cref="TreeFelling.Remove"/>: every sample of it, fringe included, becomes
    /// air) a slice at a time, so a big crown's tens of thousands of edits don't stall one frame. Only the limb's outer
    /// samples tell the world's listeners (<see cref="World.SetDensity(Vector3Int, float, bool)"/>): they wake everything
    /// beside the limb, which is all an inner sample's change could wake.
    /// </summary>
    public sealed class LimbRemoval
    {
        // Edits between looks at the clock.
        private const int Batch = 64;

        private readonly World world;
        private readonly List<Vector3Int> samples = new List<Vector3Int>();
        private readonly SampleMask all;
        private int next;

        public LimbRemoval(World world, FelledLimb limb)
        {
            this.world = world;
            foreach (FelledSample sample in limb.Solid)
            {
                samples.Add(sample.Sample);
            }
            foreach (FelledSample sample in limb.Fringe)
            {
                samples.Add(sample.Sample);
            }
            all = new SampleMask(samples);
        }

        /// <summary>True once every sample is air.</summary>
        public bool IsDone => next >= samples.Count;

        /// <summary>Removes on for about <paramref name="milliseconds"/>; true once it has finished.</summary>
        public bool Step(double milliseconds)
        {
            var watch = Stopwatch.StartNew();
            while (!IsDone)
            {
                int end = Mathf.Min(samples.Count, next + Batch);
                for (; next < end; next++)
                {
                    Vector3Int sample = samples[next];
                    world.SetDensity(sample, 0f, IsOuter(sample));
                }
                if (watch.Elapsed.TotalMilliseconds >= milliseconds)
                {
                    break;
                }
            }
            return IsDone;
        }

        /// <summary>Removes the rest at once.</summary>
        public void Finish()
        {
            Step(double.PositiveInfinity);
        }

        // A sample with a neighbour (face, edge or corner) that isn't part of the limb.
        private bool IsOuter(Vector3Int sample)
        {
            for (int dz = -1; dz <= 1; dz++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        if (!all.Contains(new Vector3Int(sample.x + dx, sample.y + dy, sample.z + dz)))
                        {
                            return true;
                        }
                    }
                }
            }
            return false;
        }
    }
}
