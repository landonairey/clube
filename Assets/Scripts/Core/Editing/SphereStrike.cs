using System;
using System.Collections.Generic;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// Working the ground with an area-of-impact sphere (GL96, a collecting test beside
    /// <see cref="ToolStrike"/>'s voxel hits): a hit reaches every sample inside a sphere around
    /// where it lands, and either lowers each by an amount (<see cref="Lower"/>, so the ground gives
    /// way gradually and interpolated edges show it) or takes each solid one whole
    /// (<see cref="Take"/>). Through the world's edit paths (A7).
    /// </summary>
    public static class SphereStrike
    {
        /// <summary>
        /// Every sample inside the sphere that holds any ground (density above 0), nearest the centre
        /// first. <paramref name="centre"/> and <paramref name="radius"/> are in world units, relative
        /// to the world origin.
        /// </summary>
        public static void FindTargets(World world, Vector3 centre, float radius, List<Vector3Int> targets)
        {
            targets.Clear();
            float voxel = world.Grid.VoxelSize;
            Vector3Int min = Vector3Int.CeilToInt((centre - Vector3.one * radius) / voxel);
            Vector3Int max = Vector3Int.FloorToInt((centre + Vector3.one * radius) / voxel);
            float radiusSquared = radius * radius;
            for (int z = min.z; z <= max.z; z++)
            {
                for (int y = min.y; y <= max.y; y++)
                {
                    for (int x = min.x; x <= max.x; x++)
                    {
                        var sample = new Vector3Int(x, y, z);
                        if (((Vector3)sample * voxel - centre).sqrMagnitude > radiusSquared)
                        {
                            continue;
                        }
                        float? density = world.GetDensity(sample);
                        if (density.HasValue && density.Value > 0f)
                        {
                            targets.Add(sample);
                        }
                    }
                }
            }
            targets.Sort((a, b) => ((Vector3)a * voxel - centre).sqrMagnitude.CompareTo(((Vector3)b * voxel - centre).sqrMagnitude));
        }

        /// <summary>
        /// Lowers every target by <paramref name="amount"/> (to no less than 0). What's taken adds up per
        /// material in <paramref name="owed"/>, and each whole unit (one sample's worth) becomes a drop.
        /// Returns how many samples it lowered.
        /// </summary>
        /// <param name="owed">Ground taken but not yet a whole item, by material; carried between hits.</param>
        public static int Lower(
            World world, IReadOnlyList<Vector3Int> targets, float amount, MaterialRegistry materials,
            Dictionary<VoxelMaterial, float> owed, List<ItemDefinition> collected, List<MinedSample> minedFrom = null)
        {
            return Lower(world, targets, _ => amount, materials, owed, collected, minedFrom);
        }

        /// <summary>
        /// As <see cref="Lower(World, IReadOnlyList{Vector3Int}, float, MaterialRegistry, Dictionary{VoxelMaterial, float}, List{ItemDefinition}, List{MinedSample})"/>,
        /// with how much is taken from each sample depending on its material (GL107: a shovel takes dirt
        /// at 1 but stone at 0.2).
        /// </summary>
        public static int Lower(
            World world, IReadOnlyList<Vector3Int> targets, Func<VoxelMaterial, float> amountFor, MaterialRegistry materials,
            Dictionary<VoxelMaterial, float> owed, List<ItemDefinition> collected, List<MinedSample> minedFrom = null)
        {
            int lowered = 0;
            foreach (Vector3Int sample in targets)
            {
                float? density = world.GetDensity(sample);
                if (!density.HasValue || density.Value <= 0f)
                {
                    continue;
                }
                VoxelMaterial material = MaterialAt(world, sample, materials);
                float taken = Mathf.Min(density.Value, Mathf.Max(0f, amountFor(material)));
                if (taken <= 0f)
                {
                    continue;
                }
                world.SetDensity(sample, density.Value - taken);
                lowered++;
                if (material == null)
                {
                    continue;
                }
                owed.TryGetValue(material, out float total);
                total += taken;
                ItemDefinition drop = ToolStrike.FinalDrop(material);
                while (total >= 1f - 1e-4f)
                {
                    total -= 1f;
                    if (drop != null)
                    {
                        collected?.Add(drop);
                        minedFrom?.Add(new MinedSample(sample, material));
                    }
                }
                owed[material] = Mathf.Max(0f, total);
            }
            return lowered;
        }

        /// <summary>
        /// Takes every solid target whole (density to 0), one drop each; targets not yet solid are
        /// cleared with it but give nothing. Returns how many solid samples it took.
        /// </summary>
        public static int Take(
            World world, IReadOnlyList<Vector3Int> targets, MaterialRegistry materials, List<ItemDefinition> collected,
            float isoLevel = 0.5f, List<MinedSample> minedFrom = null)
        {
            int taken = 0;
            foreach (Vector3Int sample in targets)
            {
                float? density = world.GetDensity(sample);
                if (!density.HasValue || density.Value <= 0f)
                {
                    continue;
                }
                bool solid = density.Value >= isoLevel;
                VoxelMaterial material = MaterialAt(world, sample, materials);
                world.SetDensity(sample, 0f);
                if (!solid)
                {
                    continue;
                }
                taken++;
                ItemDefinition drop = ToolStrike.FinalDrop(material);
                if (drop != null)
                {
                    collected?.Add(drop);
                    minedFrom?.Add(new MinedSample(sample, material));
                }
            }
            return taken;
        }

        private static VoxelMaterial MaterialAt(World world, Vector3Int sample, MaterialRegistry materials)
        {
            byte? id = world.GetMaterial(sample);
            return id.HasValue && materials != null ? materials.Get(id.Value) : null;
        }
    }
}
