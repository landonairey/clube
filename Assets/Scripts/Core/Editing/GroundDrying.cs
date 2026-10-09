using System.Collections.Generic;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// Material left out on the ground that changes with time (GL34): clay dropped on the
    /// ground dries into brick after its <see cref="VoxelMaterial.DryingDays"/>. Tracks the
    /// samples a drop placed and, once their time is up, turns the ones still there into what
    /// they dry into, through the world's edit path (A7). Ground that generated as clay stays clay;
    /// only what's put down dries. Time is in in-game days, from whatever clock the caller keeps.
    /// </summary>
    /// <remarks>Not saved yet (S2): drying in progress is lost with the world.</remarks>
    public sealed class GroundDrying
    {
        private readonly List<Drying> waiting = new List<Drying>();

        /// <summary>Samples still drying.</summary>
        public int Count => waiting.Count;

        /// <summary>
        /// Starts the samples drying if their material dries at all, from <paramref name="now"/>
        /// (in days). Returns how many it started.
        /// </summary>
        public int Add(IEnumerable<Vector3Int> samples, VoxelMaterial material, double now)
        {
            if (material == null || material.DriesInto == null)
            {
                return 0;
            }
            int added = 0;
            double readyAt = now + material.DryingDays;
            foreach (Vector3Int sample in samples)
            {
                waiting.Add(new Drying(sample, material, readyAt));
                added++;
            }
            return added;
        }

        /// <summary>
        /// Dries what's due by <paramref name="now"/> (in days): a sample still holding solid
        /// material of the kind dropped turns into what it dries into; a dug-out or replaced one
        /// is forgotten. Returns how many dried.
        /// </summary>
        public int Update(World world, double now, float isoLevel = 0.5f)
        {
            int dried = 0;
            for (int i = waiting.Count - 1; i >= 0; i--)
            {
                Drying drying = waiting[i];
                if (drying.ReadyAt > now)
                {
                    continue;
                }
                waiting.RemoveAt(i);
                float? density = world.GetDensity(drying.Sample);
                if (!density.HasValue)
                {
                    // Not loaded: wait for it to come back.
                    waiting.Add(drying);
                    continue;
                }
                if (density.Value >= isoLevel && world.GetMaterial(drying.Sample) == drying.Material.Id)
                {
                    world.SetMaterial(drying.Sample, drying.Material.DriesInto.Id);
                    dried++;
                }
            }
            return dried;
        }

        /// <summary>Days until the next sample dries, or null with nothing drying.</summary>
        public double? NextReadyAt
        {
            get
            {
                double? next = null;
                foreach (Drying drying in waiting)
                {
                    if (!next.HasValue || drying.ReadyAt < next.Value)
                    {
                        next = drying.ReadyAt;
                    }
                }
                return next;
            }
        }

        private readonly struct Drying
        {
            public Drying(Vector3Int sample, VoxelMaterial material, double readyAt)
            {
                Sample = sample;
                Material = material;
                ReadyAt = readyAt;
            }

            public Vector3Int Sample { get; }

            public VoxelMaterial Material { get; }

            public double ReadyAt { get; }
        }
    }
}
