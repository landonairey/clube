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
    /// <remarks>Wet ground doesn't dry (GL334): while water touches a sample its clock stops, so clay dropped in a pond
    /// stays clay until the water leaves it.</remarks>
    public sealed class GroundDrying
    {
        private readonly List<Drying> waiting = new List<Drying>();
        private double? lastUpdate;

        private static readonly Vector3Int[] Around =
        {
            Vector3Int.zero, Vector3Int.up, Vector3Int.down, Vector3Int.right, Vector3Int.left, new Vector3Int(0, 0, 1), new Vector3Int(0, 0, -1),
        };

        /// <summary>True when water is in the sample or any of the six beside it (GL334).</summary>
        public static bool IsWet(World world, Vector3Int sample)
        {
            foreach (Vector3Int step in Around)
            {
                if (world.GetWater(sample + step) > 0f)
                {
                    return true;
                }
            }
            return false;
        }

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
            // Wet samples' clocks stop: their time to dry moves on by the time that passed.
            double passed = lastUpdate.HasValue ? System.Math.Max(0.0, now - lastUpdate.Value) : 0.0;
            lastUpdate = now;
            if (passed > 0.0)
            {
                for (int i = 0; i < waiting.Count; i++)
                {
                    Drying wet = waiting[i];
                    if (IsWet(world, wet.Sample))
                    {
                        waiting[i] = new Drying(wet.Sample, wet.Material, wet.ReadyAt + passed);
                    }
                }
            }
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

        /// <summary>Every sample still drying, with its material and the day it's ready, for saving (GL205).</summary>
        public IEnumerable<(Vector3Int Sample, VoxelMaterial Material, double ReadyAt)> Waiting
        {
            get
            {
                foreach (Drying drying in waiting)
                {
                    yield return (drying.Sample, drying.Material, drying.ReadyAt);
                }
            }
        }

        /// <summary>Puts back a sample that was drying when the game was saved (GL205).</summary>
        public void Restore(Vector3Int sample, VoxelMaterial material, double readyAt)
        {
            if (material != null && material.DriesInto != null)
            {
                waiting.Add(new Drying(sample, material, readyAt));
            }
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
