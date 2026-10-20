using System;
using System.Collections.Generic;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// What a chisel does to the ground (GL87, GL92; first pass of TL2's chisel): each hit carves
    /// the voxels it reaches out of the ground, on the voxel grid. A carved voxel is air: its own
    /// piece of the rounded surface goes, and the ground round it shows flat walls towards it
    /// (<see cref="World.SetCarved"/>, <see cref="CarvedVoxels"/>), so a lump is cut into blocks
    /// one voxel at a time. The chisel never adds ground. Works on any material; each voxel carved
    /// gives one drop of the material most of its solid corners hold.
    /// </summary>
    public static class ChiselStrike
    {
        private static readonly Dictionary<byte, int> Counts = new Dictionary<byte, int>();

        /// <summary>One chisel hit on every voxel in <paramref name="reach"/>. Returns how many it carved.</summary>
        /// <param name="collected">Gets one drop per voxel carved (what its material's last break stage gives); may be null.</param>
        /// <param name="minedFrom">Gets, for each drop, a corner sample and the material it came from; may be null.</param>
        /// <param name="canEdit">Whether a sample may be changed (locked land, GL42); a voxel with a locked solid corner is left. Null: all may.</param>
        public static ChiselResult Hit(
            World world, VoxelReach reach, MaterialRegistry materials, List<ItemDefinition> collected,
            float isoLevel = 0.5f, List<MinedSample> minedFrom = null, Func<Vector3Int, bool> canEdit = null)
        {
            int carved = 0;
            VoxelBox box = reach.Box;
            for (int z = box.Min.z; z <= box.Max.z; z++)
            {
                for (int y = box.Min.y; y <= box.Max.y; y++)
                {
                    for (int x = box.Min.x; x <= box.Max.x; x++)
                    {
                        var voxel = new Vector3Int(x, y, z);
                        if (reach.Contains(voxel) && Carve(world, voxel, materials, collected, isoLevel, minedFrom, canEdit))
                        {
                            carved++;
                        }
                    }
                }
            }
            return new ChiselResult(carved);
        }

        /// <summary>True when the voxel has ground to carve: it isn't carved yet and a corner is solid.</summary>
        public static bool CanCarve(World world, Vector3Int voxel, float isoLevel)
        {
            if (world.IsCarved(voxel))
            {
                return false;
            }
            for (int corner = 0; corner < 8; corner++)
            {
                if (Solid(world, voxel + Corner(corner), isoLevel))
                {
                    return true;
                }
            }
            return false;
        }

        private static bool Carve(
            World world, Vector3Int voxel, MaterialRegistry materials, List<ItemDefinition> collected,
            float isoLevel, List<MinedSample> minedFrom, Func<Vector3Int, bool> canEdit)
        {
            if (!CanCarve(world, voxel, isoLevel))
            {
                return false;
            }

            // The material most of its solid corners hold, and a corner of it.
            Counts.Clear();
            byte best = 0;
            int most = 0;
            Vector3Int from = voxel;
            for (int corner = 0; corner < 8; corner++)
            {
                Vector3Int sample = voxel + Corner(corner);
                if (!Solid(world, sample, isoLevel))
                {
                    continue;
                }
                if (canEdit != null && !canEdit(sample))
                {
                    return false;
                }
                byte id = world.GetMaterial(sample) ?? 0;
                Counts.TryGetValue(id, out int count);
                Counts[id] = ++count;
                if (count > most)
                {
                    most = count;
                    best = id;
                    from = sample;
                }
            }

            world.SetCarved(voxel, true);
            VoxelMaterial material = materials != null ? materials.Get(best) : null;
            ItemDefinition drop = ToolStrike.FinalDrop(material);
            if (drop != null)
            {
                collected?.Add(drop);
                minedFrom?.Add(new MinedSample(from, material));
            }
            return true;
        }

        private static bool Solid(World world, Vector3Int sample, float isoLevel)
        {
            float? density = world.GetDensity(sample);
            return density.HasValue && density.Value >= isoLevel;
        }

        private static Vector3Int Corner(int corner)
        {
            return new Vector3Int(corner & 1, (corner >> 1) & 1, (corner >> 2) & 1);
        }
    }

    /// <summary>What one <see cref="ChiselStrike.Hit"/> did.</summary>
    public readonly struct ChiselResult
    {
        public ChiselResult(int carved)
        {
            Carved = carved;
        }

        /// <summary>Voxels carved out by the hit (each one's drop collected).</summary>
        public int Carved { get; }
    }
}
